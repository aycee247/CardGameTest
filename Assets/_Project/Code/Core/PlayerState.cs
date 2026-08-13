using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// How many free Shape actions a player has left this round. Refilled at the start of every
    /// round from the powers they own; spending past it falls back to Sparks where the rules allow.
    /// </summary>
    public sealed class ShapeAllowance
    {
        public int Rerolls;
        public int Nudges;
        public int Sets;

        internal void Clear()
        {
            Rerolls = 0;
            Nudges = 0;
            Sets = 0;
        }
    }

    /// <summary>
    /// A player's secret intent during Commit: the card they want and the exact dice paying for it.
    /// Hidden from every other player until Reveal (NET-2).
    /// </summary>
    public readonly struct PendingCommit
    {
        public readonly CardId CardId;
        public readonly int[] DiceIndices;

        public PendingCommit(CardId cardId, int[] diceIndices)
        {
            CardId = cardId;
            DiceIndices = diceIndices ?? Array.Empty<int>();
        }
    }

    /// <summary>
    /// Everything the rules layer knows about one player. Powers are always derived from
    /// <see cref="Owned"/> rather than stored as counters, so there is no cached state that can
    /// disagree with the card list.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerId Id { get; }
        public string DisplayName { get; }

        /// <summary>Stable seat position. Only used as the final, deterministic priority tiebreak.</summary>
        public int SeatIndex { get; }

        public DicePool Dice { get; internal set; }
        public int Sparks { get; internal set; }
        public ShapeAllowance Allowance { get; } = new ShapeAllowance();

        internal readonly List<Card> OwnedCards = new List<Card>();
        public IReadOnlyList<Card> Owned => OwnedCards;

        /// <summary>Secret commit for the current pass. Never leaves the server unfiltered.</summary>
        internal PendingCommit? Pending { get; set; }

        /// <summary>True once the player has explicitly passed this pass.</summary>
        public bool HasPassed { get; internal set; }

        /// <summary>Set when a claim is granted, so Upkeep knows who is owed consolation Sparks.</summary>
        public bool GainedCardThisRound { get; internal set; }

        /// <summary>
        /// False while the player is disconnected. The rules layer only reads it to decide who is
        /// auto-passed; reconnection windows are a server concern (NET-3).
        /// </summary>
        public bool IsConnected { get; internal set; } = true;

        public PlayerState(PlayerId id, string displayName, int seatIndex = 0)
        {
            Id = id;
            DisplayName = displayName ?? id.ToString();
            SeatIndex = seatIndex;
            Dice = new DicePool(0);
        }

        /// <summary>Running score during the match: victory points from owned cards.</summary>
        public int Score
        {
            get
            {
                int total = 0;
                for (int i = 0; i < OwnedCards.Count; i++) total += OwnedCards[i].Points;
                return total;
            }
        }

        public bool HasCommitted => Pending.HasValue;

        /// <summary>Dice this player should have, honouring Capacity powers and the MaxDice ceiling.</summary>
        public int DiceCapacity(MatchConfig config)
        {
            int n = config.StartingDice + SumPower(PowerKind.ExtraDie);
            if (n > config.MaxDice) n = config.MaxDice;
            return n < 0 ? 0 : n;
        }

        /// <summary>Faces that count as any face for this player when paying a cost.</summary>
        public HashSet<int> WildFaces()
        {
            var faces = new HashSet<int>();
            for (int i = 0; i < OwnedCards.Count; i++)
            {
                var power = OwnedCards[i].Power;
                if (power.Kind == PowerKind.WildFace && power.Face >= DiceRoll.MinFace && power.Face <= DiceRoll.MaxFace)
                    faces.Add(power.Face);
            }
            return faces;
        }

        /// <summary>How many arbitrary dice this player may treat as wild when paying a cost.</summary>
        public int WildDice() => SumPower(PowerKind.WildDie);

        public int SparkIncome() => SumPower(PowerKind.SparkIncome);

        internal void RefillAllowance()
        {
            Allowance.Rerolls = SumPower(PowerKind.FreeReroll);
            Allowance.Nudges = SumPower(PowerKind.FreeNudge);
            Allowance.Sets = SumPower(PowerKind.FreeSet);
        }

        internal void BeginRound()
        {
            Pending = null;
            HasPassed = false;
            GainedCardThisRound = false;
            RefillAllowance();
        }

        internal int SumPower(PowerKind kind)
        {
            int total = 0;
            for (int i = 0; i < OwnedCards.Count; i++)
                if (OwnedCards[i].Power.Kind == kind) total += OwnedCards[i].Power.Magnitude;
            return total;
        }

        public int CountFamily(PowerFamily family)
        {
            int n = 0;
            for (int i = 0; i < OwnedCards.Count; i++) if (OwnedCards[i].Family == family) n++;
            return n;
        }

        public override string ToString() => $"{DisplayName} {Score}vp {Sparks}sp {Dice}";
    }
}
