using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Immutable snapshot of a set of rolled dice values (each 1..6).
    /// Provides the aggregate queries the requirement matchers need (counts, runs, sum).
    /// </summary>
    [Serializable]
    public readonly struct DiceRoll : IEquatable<DiceRoll>
    {
        public const int MinFace = 1;
        public const int MaxFace = 6;

        private readonly int[] _values;

        public DiceRoll(IReadOnlyList<int> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            _values = new int[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                int v = values[i];
                if (v < MinFace || v > MaxFace)
                    throw new ArgumentOutOfRangeException(nameof(values), $"Die face {v} out of range {MinFace}..{MaxFace}");
                _values[i] = v;
            }
        }

        public static DiceRoll Empty => new DiceRoll(Array.Empty<int>());

        public int Count => _values?.Length ?? 0;
        public int this[int index] => _values[index];
        public IReadOnlyList<int> Values => _values ?? Array.Empty<int>();

        public int Sum()
        {
            int total = 0;
            var v = _values;
            if (v == null) return 0;
            for (int i = 0; i < v.Length; i++) total += v[i];
            return total;
        }

        /// <summary>Returns face (1..6) -> number of dice showing it.</summary>
        public IReadOnlyDictionary<int, int> FaceCounts()
        {
            var counts = new Dictionary<int, int>();
            var v = _values;
            if (v == null) return counts;
            for (int i = 0; i < v.Length; i++)
            {
                counts.TryGetValue(v[i], out int c);
                counts[v[i]] = c + 1;
            }
            return counts;
        }

        /// <summary>Largest group of identical faces, e.g. 3 for a three-of-a-kind.</summary>
        public int LargestGroup()
        {
            int best = 0;
            foreach (var kv in FaceCounts())
                if (kv.Value > best) best = kv.Value;
            return best;
        }

        /// <summary>Length of the longest run of consecutive distinct faces present.</summary>
        public int LongestRun()
        {
            var present = new HashSet<int>(Values);
            int best = 0, current = 0;
            for (int face = MinFace; face <= MaxFace; face++)
            {
                if (present.Contains(face)) { current++; if (current > best) best = current; }
                else current = 0;
            }
            return best;
        }

        public DiceRoll SortedAscending()
        {
            var copy = (_values ?? Array.Empty<int>()).ToArray();
            Array.Sort(copy);
            return new DiceRoll(copy);
        }

        public bool Equals(DiceRoll other)
        {
            var a = _values ?? Array.Empty<int>();
            var b = other._values ?? Array.Empty<int>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is DiceRoll other && Equals(other);

        public override int GetHashCode()
        {
            var v = _values;
            if (v == null) return 0;
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < v.Length; i++) hash = hash * 31 + v[i];
                return hash;
            }
        }

        public override string ToString() => "[" + string.Join(",", Values) + "]";
    }
}
