using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Decides whether a set of committed dice pays a card's cost, accounting for the player's
    /// wild powers.
    ///
    /// Wilds are resolved by search rather than by teaching every <see cref="ICardRequirement"/>
    /// about them, which is what lets the requirement matchers carry over from the prototype
    /// untouched. The search is over face *multisets* rather than per-die assignments — requirements
    /// only ever look at counts, runs and sums, so die order is irrelevant. That collapses what
    /// would be 6^k permutations into C(k+5,5) combinations: 1287 for eight wild dice, which is
    /// trivial for a validation that runs a handful of times per round.
    /// </summary>
    public static class CostChecker
    {
        /// <summary>Convenience overload for a player with no wild powers.</summary>
        public static bool Satisfies(ICardRequirement requirement, IReadOnlyList<int> faces) =>
            Satisfies(requirement, faces, null, 0);

        /// <summary>
        /// True if *some* roll of exactly <paramref name="diceCount"/> dice could pay this cost.
        ///
        /// Content validation: a card whose cost no legal dice pool can meet is dead weight in the
        /// market, and the mistake is easy to make (a run of 6 costs nothing to type but needs six
        /// dice). Callers check against the pool size a player could actually reach.
        /// </summary>
        public static bool IsSatisfiableWith(ICardRequirement requirement, int diceCount)
        {
            if (requirement == null || diceCount < 0) return false;
            return TryAssign(requirement, new List<int>(), diceCount);
        }

        /// <summary>
        /// True if <paramref name="faces"/> can satisfy <paramref name="requirement"/> when dice
        /// showing a face in <paramref name="wildFaces"/>, plus up to <paramref name="wildDice"/>
        /// dice of the player's choosing, may stand in for any face.
        /// </summary>
        public static bool Satisfies(
            ICardRequirement requirement,
            IReadOnlyList<int> faces,
            HashSet<int> wildFaces,
            int wildDice)
        {
            if (requirement == null) return false;
            if (faces == null) return false;

            // Split the committed dice into those already wild by virtue of their face, and the rest.
            var fixedFaces = new List<int>(faces.Count);
            int wildCount = 0;

            for (int i = 0; i < faces.Count; i++)
            {
                if (wildFaces != null && wildFaces.Contains(faces[i])) wildCount++;
                else fixedFaces.Add(faces[i]);
            }

            if (wildCount == 0 && wildDice <= 0)
                return requirement.IsSatisfiedBy(new DiceRoll(faces));

            // A wild die can always be assigned the face it already showed, so converting the
            // maximum number of dice dominates converting fewer — only the largest removal is tried.
            int convert = wildDice < fixedFaces.Count ? wildDice : fixedFaces.Count;
            if (convert < 0) convert = 0;

            var distinctFixed = new List<int>(new SortedSet<int>(fixedFaces));

            // Which faces to pull out of the fixed set and hand to the wild pool.
            foreach (var removal in Removals(distinctFixed, fixedFaces, convert))
            {
                var remaining = new List<int>(fixedFaces);
                for (int i = 0; i < removal.Count; i++) remaining.Remove(removal[i]);

                if (TryAssign(requirement, remaining, wildCount + removal.Count)) return true;
            }

            return false;
        }

        /// <summary>
        /// Every distinct multiset of <paramref name="size"/> faces that can be removed from
        /// <paramref name="pool"/>. Yields one empty removal when size is zero.
        /// </summary>
        private static IEnumerable<List<int>> Removals(List<int> distinct, List<int> pool, int size)
        {
            var chosen = new List<int>(size);
            return Recurse(0, size);

            IEnumerable<List<int>> Recurse(int startFace, int remaining)
            {
                if (remaining == 0)
                {
                    yield return new List<int>(chosen);
                    yield break;
                }

                for (int f = startFace; f < distinct.Count; f++)
                {
                    int face = distinct[f];
                    if (CountOf(chosen, face) >= CountOf(pool, face)) continue;

                    chosen.Add(face);
                    // Start from the same face so multisets (e.g. two 5s) are reachable, but never
                    // revisit an earlier face, which is what keeps combinations distinct.
                    foreach (var r in Recurse(f, remaining - 1)) yield return r;
                    chosen.RemoveAt(chosen.Count - 1);
                }
            }
        }

        /// <summary>
        /// True if some assignment of <paramref name="wildCount"/> wild dice to faces, combined with
        /// <paramref name="fixedFaces"/>, satisfies the requirement.
        /// </summary>
        private static bool TryAssign(ICardRequirement requirement, List<int> fixedFaces, int wildCount)
        {
            if (wildCount <= 0)
                return requirement.IsSatisfiedBy(new DiceRoll(fixedFaces));

            var buffer = new List<int>(fixedFaces.Count + wildCount);
            buffer.AddRange(fixedFaces);
            return Assign(DiceRoll.MinFace, wildCount);

            bool Assign(int minFace, int remaining)
            {
                if (remaining == 0)
                    return requirement.IsSatisfiedBy(new DiceRoll(buffer));

                // Non-decreasing faces only — the requirement cannot tell two orderings apart,
                // so this enumerates multisets instead of permutations.
                for (int face = minFace; face <= DiceRoll.MaxFace; face++)
                {
                    buffer.Add(face);
                    bool ok = Assign(face, remaining - 1);
                    buffer.RemoveAt(buffer.Count - 1);
                    if (ok) return true;
                }
                return false;
            }
        }

        private static int CountOf(List<int> list, int value)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] == value) n++;
            return n;
        }
    }
}
