using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The project's one PRNG: xorshift64* behind both dice and deck order. Hand-rolled because
    /// portability is the point — <see cref="System.Random"/>'s algorithm has changed across .NET
    /// versions, so the same seed yields different sequences on Mono vs IL2CPP vs CoreCLR, which
    /// would break replay the moment a save crossed devices.
    ///
    /// The struct's entire identity is <see cref="State"/>: capture it any time, feed it back
    /// through <see cref="FromState"/>, and the sequence continues exactly where it left off.
    /// That property is what makes mid-match save/resume possible (STORY-6.5).
    ///
    /// The algorithm is a compatibility contract. Changing it — or how consumers draw from it —
    /// silently invalidates every recorded seed and save; see the golden-sequence test.
    /// </summary>
    public struct XorShift64Star
    {
        private ulong _state;

        /// <summary>Seeds a fresh sequence. Zero is remapped — xorshift cannot escape it.</summary>
        public XorShift64Star(ulong seed)
        {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        /// <summary>The live internal state. Never zero, so it round-trips through <see cref="FromState"/>.</summary>
        public ulong State => _state;

        /// <summary>Rebuilds a generator mid-sequence from a captured <see cref="State"/>.</summary>
        public static XorShift64Star FromState(ulong state) => new XorShift64Star(state);

        public ulong NextUInt64()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>
        /// Uniform-enough integer in [0, <paramref name="exclusiveMax"/>). Plain modulo: the bias
        /// for the small bounds this game uses (≤ 64) is immeasurable, and the simple form is
        /// frozen forever by the portability contract.
        /// </summary>
        public int NextBelow(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextUInt64() % (ulong)exclusiveMax);
        }

        /// <summary>Fisher–Yates, driven by this generator so deck order replays from a seed.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextBelow(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
