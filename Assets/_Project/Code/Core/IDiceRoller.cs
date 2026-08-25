using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Source of dice values. The authoritative server owns the only real instance;
    /// clients never generate values, they receive the server's resolved <see cref="DiceRoll"/>.
    /// </summary>
    public interface IDiceRoller
    {
        /// <summary>Rolls <paramref name="count"/> dice, each in 1..6.</summary>
        DiceRoll Roll(int count);
    }

    /// <summary>
    /// Deterministic dice roller backed by a portable xorshift PRNG.
    /// Given the same seed it produces the same sequence on any platform/runtime,
    /// which makes the rules engine trivially unit-testable and lets the server
    /// reproduce/verify a roll from a seed if ever needed.
    /// </summary>
    public sealed class SeededDiceRoller : IDiceRoller
    {
        private XorShift64Star _rng;

        public SeededDiceRoller(ulong seed) => _rng = new XorShift64Star(seed);

        private SeededDiceRoller(XorShift64Star rng) => _rng = rng;

        /// <summary>
        /// The live generator state — capture it alongside the match to save, then rebuild the
        /// roller mid-sequence with <see cref="FromState"/> and the dice continue exactly where
        /// they left off (STORY-6.5).
        /// </summary>
        public ulong State => _rng.State;

        /// <summary>Rebuilds a roller from a captured <see cref="State"/>.</summary>
        public static SeededDiceRoller FromState(ulong state) =>
            new SeededDiceRoller(XorShift64Star.FromState(state));

        public DiceRoll Roll(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = (int)(_rng.NextUInt64() % 6UL) + 1; // 1..6
            return new DiceRoll(values);
        }
    }
}
