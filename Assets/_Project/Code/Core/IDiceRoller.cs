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
        private ulong _state;

        public SeededDiceRoller(ulong seed)
        {
            // Avoid the degenerate all-zero state that xorshift cannot escape.
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong Seed => _state;

        public DiceRoll Roll(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var values = new int[count];
            for (int i = 0; i < count; i++)
                values[i] = (int)(NextUInt64() % 6UL) + 1; // 1..6
            return new DiceRoll(values);
        }

        private ulong NextUInt64()
        {
            // xorshift64* — fast, deterministic, good enough for game dice.
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }
}
