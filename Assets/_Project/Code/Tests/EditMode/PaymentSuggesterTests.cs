using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The card sheet's auto-suggest (UI-3). The contract: cheapest paying subset — fewest dice
    /// first, lowest pip total as the tie-break — never a spent die, wild powers honoured, empty
    /// when nothing pays.
    /// </summary>
    public class PaymentSuggesterTests
    {
        private static readonly HashSet<int> NoWilds = new HashSet<int>();

        private static int[] Suggest(ICardRequirement cost, int[] faces, bool[] spent = null,
            HashSet<int> wildFaces = null, int wildDice = 0) =>
            PaymentSuggester.Suggest(cost, faces, spent, wildFaces ?? NoWilds, wildDice);

        [Test]
        public void PicksTheSmallestSubsetThatPays()
        {
            var pair = new NOfAKindRequirement(2);

            var suggestion = Suggest(pair, new[] { 3, 5, 3, 3 });

            Assert.AreEqual(2, suggestion.Length, "a pair costs two dice, not three");
            foreach (int i in suggestion) Assert.AreEqual(3, new[] { 3, 5, 3, 3 }[i]);
        }

        [Test]
        public void BreaksTiesTowardTheLowestPips()
        {
            var sumAtLeast4 = new SumRequirement(4);

            // Both singles pay; the 4 must be preferred so the 6 stays free for something better.
            CollectionAssert.AreEqual(new[] { 1 }, Suggest(sumAtLeast4, new[] { 6, 4 }));
        }

        [Test]
        public void NeverSuggestsASpentDie()
        {
            var sumAtLeast5 = new SumRequirement(5);

            var suggestion = Suggest(sumAtLeast5, new[] { 5, 5, 2 }, new[] { true, false, false });

            CollectionAssert.AreEqual(new[] { 1 }, suggestion);
        }

        [Test]
        public void UsesWildDiceToCompleteASet()
        {
            var threeOfAKind = new NOfAKindRequirement(3);

            // A wild die floats a rolled die's value — it never adds a die — so the triple still
            // needs three physical dice: the pair of 2s plus the 5 floated to a 2.
            CollectionAssert.AreEqual(new[] { 0, 1, 2 },
                Suggest(threeOfAKind, new[] { 2, 2, 5 }, wildDice: 1));
        }

        [Test]
        public void TreatsWildFacesAsAnyValue()
        {
            var fourAndFive = new ContainsFacesRequirement(new[] { 4, 5 });

            // The 6 is wild and stands in for the 5.
            CollectionAssert.AreEqual(new[] { 0, 1 },
                Suggest(fourAndFive, new[] { 4, 6 }, wildFaces: new HashSet<int> { 6 }));
        }

        [Test]
        public void ReturnsEmptyWhenNothingPays()
        {
            Assert.IsEmpty(Suggest(new SumRequirement(30), new[] { 1, 1 }));
            Assert.IsEmpty(Suggest(new NOfAKindRequirement(2), Array.Empty<int>()));
            Assert.IsEmpty(Suggest(null, new[] { 1, 2 }));
        }

        [Test]
        public void IndicesComeBackAscending()
        {
            var run3 = new RunRequirement(3);

            var suggestion = Suggest(run3, new[] { 3, 6, 1, 2 });

            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, suggestion, "1-2-3 run from indices 2,3,0");
        }
    }
}
