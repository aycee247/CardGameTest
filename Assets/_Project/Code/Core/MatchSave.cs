using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// Full-fidelity capture and rehydration of a running match (STORY-6.5): every field the rules
    /// engine can read, including secrets and the roller's live state, so a restored match plays
    /// forward move-for-move identically to one that was never interrupted.
    ///
    /// This is the server's artifact. A save contains every pending commit in the clear — it must
    /// never be sent to a client; clients are only ever handed the per-observer
    /// <see cref="MatchSnapshot"/>.
    ///
    /// Cards travel by id. The caller supplies the resolver on restore — the
    /// <c>CardDatabase</c> in the app, the test's own deck in the suite — which keeps saves small
    /// and keeps card content in its single source of truth (<c>StarterDeck</c>) instead of
    /// freezing a copy into every save file.
    ///
    /// The format is explicit field-by-field binary (little-endian, length-prefixed UTF-8
    /// strings): no reflection, no JSON dependency in Core, and a version stamp so a stale save
    /// fails loudly instead of misreading.
    /// </summary>
    public static class MatchSave
    {
        /// <summary>Bump on any layout change, and keep old readers only if resume-across-updates ships.</summary>
        public const ushort Version = 1;

        private const uint Magic = 0x59524446; // "FDRY", little-endian

        // ------------------------------------------------------------------ capture

        public static byte[] Capture(MatchState state, ulong rollerState)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(Version);
                w.Write(rollerState);

                WriteConfig(w, state.Config);

                w.Write(state.Round);
                w.Write((byte)state.Phase);

                w.Write(state.Players.Count);
                foreach (var p in state.Players) WritePlayer(w, p);

                WriteCardIds(w, state.Market, c => c.Id);
                WriteCardIds(w, state.DrawPileCards(), c => c.Id);
                WritePlayerIds(w, state.PriorityOrder);
                WritePlayerIds(w, state.RepickContenders);

                w.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------ restore

        /// <summary>
        /// Rehydrates a capture. <paramref name="resolveCard"/> must return the card for every id
        /// in the save — a null is a corrupt save or the wrong deck, and throws.
        /// </summary>
        public static MatchState Restore(byte[] data, Func<CardId, Card> resolveCard, out SeededDiceRoller roller)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (resolveCard == null) throw new ArgumentNullException(nameof(resolveCard));

            using (var ms = new MemoryStream(data, writable: false))
            using (var r = new BinaryReader(ms, Encoding.UTF8))
            {
                if (r.ReadUInt32() != Magic)
                    throw new InvalidDataException("Not a Foundry match save.");

                ushort version = r.ReadUInt16();
                if (version != Version)
                    throw new InvalidDataException($"Save version {version}; this build reads {Version}.");

                roller = SeededDiceRoller.FromState(r.ReadUInt64());

                var config = ReadConfig(r);

                int round = r.ReadInt32();
                var phase = (RoundPhase)r.ReadByte();

                int playerCount = r.ReadInt32();
                var players = new List<PlayerState>(playerCount);
                for (int i = 0; i < playerCount; i++) players.Add(ReadPlayer(r, resolveCard));

                var market = ReadCards(r, resolveCard);
                var drawPile = new Queue<Card>(ReadCards(r, resolveCard));
                var priority = ReadPlayerIds(r);
                var repick = ReadPlayerIds(r);

                return new MatchState(config, players, market, drawPile, priority, repick, phase, round);
            }
        }

        // ------------------------------------------------------------------ pieces

        private static void WriteConfig(BinaryWriter w, MatchConfig c)
        {
            w.Write(c.Rounds);
            w.Write(c.StartingDice);
            w.Write(c.MaxDice);
            w.Write(c.MarketSize);
            w.Write(c.SparkCap);
            w.Write(c.SparksPerUnspentDie);
            w.Write(c.ConsolationSparks);
            w.Write(c.RerollSparkCost);
            w.Write(c.SetFaceSparkCost);
            w.Write(c.ShapeSeconds);
            w.Write(c.CommitSeconds);
            w.Write(c.RepickSeconds);
            w.Write(c.RollSeconds);
            w.Write(c.RevealBaseSeconds);
            w.Write(c.RevealPerClaimSeconds);
            w.Write(c.UpkeepSeconds);
        }

        private static MatchConfig ReadConfig(BinaryReader r) => new MatchConfig
        {
            Rounds = r.ReadInt32(),
            StartingDice = r.ReadInt32(),
            MaxDice = r.ReadInt32(),
            MarketSize = r.ReadInt32(),
            SparkCap = r.ReadInt32(),
            SparksPerUnspentDie = r.ReadInt32(),
            ConsolationSparks = r.ReadInt32(),
            RerollSparkCost = r.ReadInt32(),
            SetFaceSparkCost = r.ReadInt32(),
            ShapeSeconds = r.ReadInt32(),
            CommitSeconds = r.ReadInt32(),
            RepickSeconds = r.ReadInt32(),
            RollSeconds = r.ReadSingle(),
            RevealBaseSeconds = r.ReadSingle(),
            RevealPerClaimSeconds = r.ReadSingle(),
            UpkeepSeconds = r.ReadSingle()
        };

        private static void WritePlayer(BinaryWriter w, PlayerState p)
        {
            w.Write(p.Id.Value);
            w.Write(p.DisplayName ?? string.Empty);
            w.Write(p.SeatIndex);
            w.Write(p.Sparks);
            w.Write(p.IsConnected);
            w.Write(p.HasPassed);
            w.Write(p.DoneShaping);
            w.Write(p.GainedCardThisRound);

            w.Write(p.Allowance.Rerolls);
            w.Write(p.Allowance.Nudges);
            w.Write(p.Allowance.Sets);

            var faces = p.Dice.FacesCopy();
            var spent = p.Dice.SpentCopy();
            w.Write(faces.Length);
            for (int i = 0; i < faces.Length; i++)
            {
                w.Write((byte)faces[i]);
                w.Write(spent[i]);
            }

            w.Write(p.Pending.HasValue);
            if (p.Pending.HasValue)
            {
                w.Write(p.Pending.Value.CardId.Value);
                var dice = p.Pending.Value.DiceIndices;
                w.Write(dice.Length);
                for (int i = 0; i < dice.Length; i++) w.Write(dice[i]);
            }

            w.Write(p.Owned.Count);
            for (int i = 0; i < p.Owned.Count; i++) w.Write(p.Owned[i].Id.Value);
        }

        private static PlayerState ReadPlayer(BinaryReader r, Func<CardId, Card> resolveCard)
        {
            var player = new PlayerState(new PlayerId(r.ReadInt32()), r.ReadString(), r.ReadInt32())
            {
                Sparks = r.ReadInt32(),
                IsConnected = r.ReadBoolean(),
                HasPassed = r.ReadBoolean(),
                DoneShaping = r.ReadBoolean(),
                GainedCardThisRound = r.ReadBoolean()
            };

            player.Allowance.Rerolls = r.ReadInt32();
            player.Allowance.Nudges = r.ReadInt32();
            player.Allowance.Sets = r.ReadInt32();

            int diceCount = r.ReadInt32();
            var faces = new int[diceCount];
            var spent = new bool[diceCount];
            for (int i = 0; i < diceCount; i++)
            {
                faces[i] = r.ReadByte();
                spent[i] = r.ReadBoolean();
            }
            player.Dice = new DicePool(faces, spent);

            if (r.ReadBoolean())
            {
                var cardId = new CardId(r.ReadInt32());
                var indices = new int[r.ReadInt32()];
                for (int i = 0; i < indices.Length; i++) indices[i] = r.ReadInt32();
                player.Pending = new PendingCommit(cardId, indices);
            }

            int owned = r.ReadInt32();
            for (int i = 0; i < owned; i++) player.OwnedCards.Add(Resolve(resolveCard, r.ReadInt32()));

            return player;
        }

        private static void WriteCardIds(BinaryWriter w, IReadOnlyList<Card> cards, Func<Card, CardId> id)
        {
            w.Write(cards.Count);
            for (int i = 0; i < cards.Count; i++) w.Write(id(cards[i]).Value);
        }

        private static List<Card> ReadCards(BinaryReader r, Func<CardId, Card> resolveCard)
        {
            int count = r.ReadInt32();
            var cards = new List<Card>(count);
            for (int i = 0; i < count; i++) cards.Add(Resolve(resolveCard, r.ReadInt32()));
            return cards;
        }

        private static void WritePlayerIds(BinaryWriter w, IReadOnlyList<PlayerId> ids)
        {
            w.Write(ids.Count);
            for (int i = 0; i < ids.Count; i++) w.Write(ids[i].Value);
        }

        private static List<PlayerId> ReadPlayerIds(BinaryReader r)
        {
            int count = r.ReadInt32();
            var ids = new List<PlayerId>(count);
            for (int i = 0; i < count; i++) ids.Add(new PlayerId(r.ReadInt32()));
            return ids;
        }

        private static Card Resolve(Func<CardId, Card> resolveCard, int rawId)
        {
            var id = new CardId(rawId);
            return resolveCard(id)
                ?? throw new InvalidDataException($"Save references card {id} which the resolver does not know.");
        }
    }
}
