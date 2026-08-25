using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.App
{
    /// <summary>
    /// Builds an authoritative <see cref="MatchState"/> from authored content.
    ///
    /// Shared by the hot-seat host and the networked launcher so both modes deal the same kind of
    /// game — the deck is shuffled within tiers, never across them, which is what keeps the market
    /// escalating while still dealing a different sequence every match (MKT-1).
    /// </summary>
    public static class MatchFactory
    {
        public static MatchState Build(
            MatchConfig config,
            CardDatabase database,
            IReadOnlyList<string> playerNames,
            int? deckSeed = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (playerNames == null || playerNames.Count == 0)
                throw new ArgumentException("A match needs at least one player.", nameof(playerNames));

            var players = new List<PlayerState>(playerNames.Count);
            for (int i = 0; i < playerNames.Count; i++)
                players.Add(new PlayerState(new PlayerId(i), playerNames[i], i));

            // The deck stream is decorrelated from the dice stream (which is seeded with the raw
            // match seed) by a fixed xor, so one saved seed reproduces both without either
            // sequence mirroring the other.
            var rng = new XorShift64Star(unchecked((ulong)(deckSeed ?? NewSeed())) ^ 0xDEC0DEC0DEC0DEC0UL);
            var deck = database.BuildShuffledDeck(ref rng);

            return new MatchState(config ?? MatchConfig.Default, players, deck);
        }

        public static IReadOnlyList<string> DefaultNames(int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = "Player " + (i + 1);
            return names;
        }

        /// <summary>
        /// Server-owned entropy. Dice determinism only has to hold within a single match, from a
        /// seed the server picks, so wall-clock entropy is fine here.
        /// </summary>
        public static int NewSeed() =>
            unchecked((int)(DateTime.UtcNow.Ticks ^ Environment.TickCount));
    }
}
