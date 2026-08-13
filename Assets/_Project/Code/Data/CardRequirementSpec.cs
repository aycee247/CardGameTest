using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Inspector-authorable description of a single dice requirement. Designers pick a
    /// <see cref="Kind"/> and fill the relevant fields; <see cref="Build"/> converts it into the
    /// pure <see cref="ICardRequirement"/> the rules engine evaluates. Flat by design (no nesting)
    /// so it serializes cleanly; combine multiple specs on a <see cref="CardDefinition"/> for AND/OR.
    /// </summary>
    [Serializable]
    public struct CardRequirementSpec
    {
        public enum Kind { NOfAKind, Run, Sum, ContainsFaces }

        public Kind kind;

        [Header("N Of A Kind")]
        [Tooltip("How many matching dice are required.")]
        public int n;
        [Tooltip("Specific face 1..6, or 0 for any face.")]
        public int face; // 0 = any

        [Header("Run")]
        [Tooltip("Length of the consecutive run, e.g. 4 for 1-2-3-4.")]
        public int runLength;

        [Header("Sum")]
        public int sumTarget;
        public ComparisonOp sumComparison;

        [Header("Contains Faces")]
        public List<int> requiredFaces;

        public ICardRequirement Build()
        {
            switch (kind)
            {
                case Kind.NOfAKind:
                    return new NOfAKindRequirement(Mathf.Max(1, n), face >= 1 && face <= 6 ? face : (int?)null);
                case Kind.Run:
                    return new RunRequirement(Mathf.Max(1, runLength));
                case Kind.Sum:
                    return new SumRequirement(sumTarget, sumComparison);
                case Kind.ContainsFaces:
                    return new ContainsFacesRequirement(requiredFaces ?? new List<int>());
                default:
                    return new SumRequirement(0, ComparisonOp.AtLeast); // always-true fallback
            }
        }
    }
}
