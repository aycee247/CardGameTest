using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Finds which of a player's dice could pay a cost, for the card sheet's suggested selection
    /// (UI-3). Pure and bounded: at most 2^8−1 subsets of the unspent dice, checked cheapest-first
    /// so the first hit is the answer — fewest dice, then lowest pip total, so the suggestion never
    /// burns a big die a smaller one could cover. This is a suggestion only; the engine
    /// re-validates the actual commit (CORE-5).
    /// </summary>
    public static class PaymentSuggester
    {
        /// <summary>
        /// Die indices (ascending) of the cheapest unspent subset that pays
        /// <paramref name="requirement"/>, or an empty array when nothing does.
        /// </summary>
        public static int[] Suggest(
            ICardRequirement requirement,
            IReadOnlyList<int> faces,
            IReadOnlyList<bool> spent,
            HashSet<int> wildFaces,
            int wildDice)
        {
            if (requirement == null || faces == null || faces.Count == 0) return Array.Empty<int>();

            var available = new List<int>(faces.Count);
            for (int i = 0; i < faces.Count; i++)
                if (spent == null || i >= spent.Count || !spent[i]) available.Add(i);

            if (available.Count == 0) return Array.Empty<int>();

            // Order every subset by (die count, pip total) before validating any of them, so the
            // first satisfying subset is the preferred one by construction.
            int n = available.Count;
            var order = new List<(int count, int pips, int mask)>((1 << n) - 1);
            for (int mask = 1; mask < 1 << n; mask++)
            {
                int count = 0, pips = 0;
                for (int b = 0; b < n; b++)
                {
                    if ((mask & (1 << b)) == 0) continue;
                    count++;
                    pips += faces[available[b]];
                }
                order.Add((count, pips, mask));
            }
            order.Sort((a, b) => a.count != b.count ? a.count - b.count : a.pips - b.pips);

            var subsetFaces = new List<int>(n);
            foreach (var (count, _, mask) in order)
            {
                subsetFaces.Clear();
                for (int b = 0; b < n; b++)
                    if ((mask & (1 << b)) != 0) subsetFaces.Add(faces[available[b]]);

                if (!CostChecker.Satisfies(requirement, subsetFaces, wildFaces, wildDice)) continue;

                var indices = new int[count];
                int w = 0;
                for (int b = 0; b < n; b++)
                    if ((mask & (1 << b)) != 0) indices[w++] = available[b];
                return indices;   // available is ascending, so indices are too
            }

            return Array.Empty<int>();
        }
    }
}
