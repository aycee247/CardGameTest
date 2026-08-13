using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// A rule a rolled set of dice must satisfy for a card to be claimable.
    /// Pure and side-effect free so the same instance can be evaluated on server and client.
    /// </summary>
    public interface ICardRequirement
    {
        bool IsSatisfiedBy(DiceRoll roll);
        string Describe();
    }

    public enum ComparisonOp { Equal, AtLeast, AtMost }

    /// <summary>N dice showing the same face — optionally a specific face.</summary>
    public sealed class NOfAKindRequirement : ICardRequirement
    {
        public readonly int N;
        public readonly int? Face; // null = any face

        public NOfAKindRequirement(int n, int? face = null)
        {
            if (n < 1) throw new ArgumentOutOfRangeException(nameof(n));
            N = n; Face = face;
        }

        public bool IsSatisfiedBy(DiceRoll roll)
        {
            var counts = roll.FaceCounts();
            if (Face.HasValue)
                return counts.TryGetValue(Face.Value, out int c) && c >= N;
            return counts.Values.Any(c => c >= N);
        }

        public string Describe() =>
            Face.HasValue ? $"{N}× face {Face.Value}" : $"{N} of a kind";
    }

    /// <summary>A straight/run of consecutive faces of the given length (e.g. 1-2-3-4).</summary>
    public sealed class RunRequirement : ICardRequirement
    {
        public readonly int Length;

        public RunRequirement(int length)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            Length = length;
        }

        public bool IsSatisfiedBy(DiceRoll roll) => roll.LongestRun() >= Length;
        public string Describe() => $"run of {Length}";
    }

    /// <summary>The sum of all dice compared against a target.</summary>
    public sealed class SumRequirement : ICardRequirement
    {
        public readonly int Target;
        public readonly ComparisonOp Op;

        public SumRequirement(int target, ComparisonOp op = ComparisonOp.AtLeast)
        {
            Target = target; Op = op;
        }

        public bool IsSatisfiedBy(DiceRoll roll)
        {
            int sum = roll.Sum();
            switch (Op)
            {
                case ComparisonOp.Equal: return sum == Target;
                case ComparisonOp.AtLeast: return sum >= Target;
                case ComparisonOp.AtMost: return sum <= Target;
                default: return false;
            }
        }

        public string Describe()
        {
            string opStr = Op == ComparisonOp.Equal ? "=" : Op == ComparisonOp.AtLeast ? "≥" : "≤";
            return $"sum {opStr} {Target}";
        }
    }

    /// <summary>Roll must contain (as a multiset) all of the specified faces.</summary>
    public sealed class ContainsFacesRequirement : ICardRequirement
    {
        public readonly IReadOnlyList<int> RequiredFaces;

        public ContainsFacesRequirement(IReadOnlyList<int> requiredFaces)
        {
            RequiredFaces = requiredFaces ?? throw new ArgumentNullException(nameof(requiredFaces));
        }

        public bool IsSatisfiedBy(DiceRoll roll)
        {
            var available = new Dictionary<int, int>(roll.FaceCounts());
            foreach (int face in RequiredFaces)
            {
                if (!available.TryGetValue(face, out int c) || c <= 0) return false;
                available[face] = c - 1;
            }
            return true;
        }

        public string Describe() => "contains " + string.Join("+", RequiredFaces);
    }

    /// <summary>Combines child requirements with All (AND) or Any (OR) semantics.</summary>
    public sealed class CompositeRequirement : ICardRequirement
    {
        public enum Mode { All, Any }

        public readonly Mode CombineMode;
        public readonly IReadOnlyList<ICardRequirement> Children;

        public CompositeRequirement(Mode mode, params ICardRequirement[] children)
        {
            CombineMode = mode;
            Children = children ?? Array.Empty<ICardRequirement>();
        }

        public bool IsSatisfiedBy(DiceRoll roll) =>
            CombineMode == Mode.All
                ? Children.All(c => c.IsSatisfiedBy(roll))
                : Children.Any(c => c.IsSatisfiedBy(roll));

        public string Describe() =>
            "(" + string.Join(CombineMode == Mode.All ? " AND " : " OR ",
                Children.Select(c => c.Describe())) + ")";
    }
}
