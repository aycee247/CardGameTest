using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Wild powers are resolved by searching face assignments rather than by teaching the
    /// requirement matchers about them. These tests pin that search: it must find a satisfying
    /// assignment when one exists, and must not invent one when it does not.
    /// </summary>
    public class WildCostTests
    {
        private static HashSet<int> Wild(params int[] faces) => new HashSet<int>(faces);

        [Test]
        public void WithoutWilds_BehavesExactlyLikeTheMatcher()
        {
            var threeOfAKind = new NOfAKindRequirement(3);

            Assert.IsTrue(CostChecker.Satisfies(threeOfAKind, new[] { 4, 4, 4 }));
            Assert.IsFalse(CostChecker.Satisfies(threeOfAKind, new[] { 4, 4, 2 }));
        }

        [Test]
        public void WildFace_StandsInForAnotherFace()
        {
            var threeOfAKind = new NOfAKindRequirement(3);

            // Two 4s and a wild 6 make a three of a kind.
            Assert.IsFalse(CostChecker.Satisfies(threeOfAKind, new[] { 4, 4, 6 }));
            Assert.IsTrue(CostChecker.Satisfies(threeOfAKind, new[] { 4, 4, 6 }, Wild(6), 0));
        }

        [Test]
        public void WildFace_CompletesARun()
        {
            var runOfFive = new RunRequirement(5);

            Assert.IsFalse(CostChecker.Satisfies(runOfFive, new[] { 1, 2, 3, 5, 6 }));
            Assert.IsTrue(CostChecker.Satisfies(runOfFive, new[] { 1, 2, 3, 6, 5 }, Wild(6), 0),
                "the wild 6 should become the missing 4");
        }

        [Test]
        public void WildDie_LetsAnyOneDieFloat()
        {
            var fourOfAKind = new NOfAKindRequirement(4);

            Assert.IsFalse(CostChecker.Satisfies(fourOfAKind, new[] { 2, 2, 2, 5 }, null, 0));
            Assert.IsTrue(CostChecker.Satisfies(fourOfAKind, new[] { 2, 2, 2, 5 }, null, 1));
        }

        [Test]
        public void WildDiceCountIsRespected()
        {
            var fiveOfAKind = new NOfAKindRequirement(5);

            Assert.IsFalse(CostChecker.Satisfies(fiveOfAKind, new[] { 3, 3, 3, 1, 6 }, null, 1));
            Assert.IsTrue(CostChecker.Satisfies(fiveOfAKind, new[] { 3, 3, 3, 1, 6 }, null, 2));
        }

        [Test]
        public void WildFaceAndWildDiceCombine()
        {
            var sixOfAKind = new NOfAKindRequirement(6);

            // Three natural 5s, one wild 6, and two floating wilds.
            Assert.IsTrue(CostChecker.Satisfies(sixOfAKind, new[] { 5, 5, 5, 6, 1, 2 }, Wild(6), 2));
            Assert.IsFalse(CostChecker.Satisfies(sixOfAKind, new[] { 5, 5, 5, 6, 1, 2 }, Wild(6), 1));
        }

        [Test]
        public void WildsCannotSatisfyAnUpperBoundTheyBreak()
        {
            // A wild has to become *some* face, and every face is at least 1, so a low sum
            // requirement must not become satisfiable just because dice are wild.
            var sumAtMostThree = new SumRequirement(3, ComparisonOp.AtMost);

            Assert.IsFalse(CostChecker.Satisfies(sumAtMostThree, new[] { 6, 6, 6, 6 }, Wild(6), 0));
            Assert.IsTrue(CostChecker.Satisfies(sumAtMostThree, new[] { 6, 6, 6 }, Wild(6), 0),
                "three wilds can all become 1s, summing to 3");
        }

        [Test]
        public void WildsSatisfyAnExactSumOnlyWhenItIsReachable()
        {
            var sumIsTwelve = new SumRequirement(12, ComparisonOp.Equal);

            // 6+6+6 = 18.
            Assert.IsFalse(CostChecker.Satisfies(sumIsTwelve, new[] { 6, 6, 6 }, null, 0));

            // One wild floats between 1 and 6, so the reachable range is 13..18 — 12 is still out.
            Assert.IsFalse(CostChecker.Satisfies(sumIsTwelve, new[] { 6, 6, 6 }, null, 1));

            // Two wilds reach 8..18, so 12 becomes attainable (6+5+1).
            Assert.IsTrue(CostChecker.Satisfies(sumIsTwelve, new[] { 6, 6, 6 }, null, 2));
        }

        [Test]
        public void EightWildDiceStillResolveQuickly()
        {
            // Guards the combinatorial shape of the search: multisets, not permutations.
            var runOfSix = new RunRequirement(6);
            var faces = new[] { 6, 6, 6, 6, 6, 6, 6, 6 };

            Assert.IsTrue(CostChecker.Satisfies(runOfSix, faces, Wild(6), 0));
        }

        [Test]
        public void SatisfiabilityCatchesCostsNoPoolCanPay()
        {
            // Content validation: these are the mistakes that are cheap to type and dead in play.
            Assert.IsFalse(CostChecker.IsSatisfiableWith(new RunRequirement(6), 5), "a run of 6 needs six dice");
            Assert.IsTrue(CostChecker.IsSatisfiableWith(new RunRequirement(6), 6));

            Assert.IsFalse(CostChecker.IsSatisfiableWith(new NOfAKindRequirement(5), 4));
            Assert.IsTrue(CostChecker.IsSatisfiableWith(new NOfAKindRequirement(5), 5));

            // Eight dice top out at 48.
            Assert.IsFalse(CostChecker.IsSatisfiableWith(new SumRequirement(49), 8));
            Assert.IsTrue(CostChecker.IsSatisfiableWith(new SumRequirement(48), 8));

            // A run of 7 is impossible at any pool size — there are only six faces.
            Assert.IsFalse(CostChecker.IsSatisfiableWith(new RunRequirement(7), 8));
        }

        [Test]
        public void CommitAppliesTheCommittingPlayersWilds()
        {
            var config = Make.Config(rounds: 2, marketSize: 1);
            var state = Make.Match(config, new[] { Make.Card(1, new NOfAKindRequirement(4)) });

            // Only P0 owns the wild power.
            Make.Grant(state.Players[0], Make.Card(90, new SumRequirement(0), power: CardPower.WildFace(6), family: PowerFamily.Wild));

            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 3, 3, 3, 6 },
                new[] { 3, 3, 3, 6 }));
            session.AdvanceTo(RoundPhase.Commit);

            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1, 2, 3 }).Success,
                "P0's wild 6 should complete the four of a kind");

            var withoutWild = session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1, 2, 3 });
            Assert.IsFalse(withoutWild.Success);
            Assert.AreEqual(MoveFailure.CostNotMet, withoutWild.Failure);
        }
    }
}
