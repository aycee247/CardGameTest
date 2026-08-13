using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class RequirementTests
    {
        private static DiceRoll Roll(params int[] v) => new DiceRoll(v);

        [Test]
        public void NOfAKind_AnyFace_MatchesLargestGroup()
        {
            var req = new NOfAKindRequirement(3);
            Assert.IsTrue(req.IsSatisfiedBy(Roll(2, 2, 2, 5, 1, 6)));
            Assert.IsFalse(req.IsSatisfiedBy(Roll(2, 2, 5, 5, 1, 6)));
        }

        [Test]
        public void NOfAKind_SpecificFace_RequiresThatFace()
        {
            var req = new NOfAKindRequirement(2, face: 6);
            Assert.IsTrue(req.IsSatisfiedBy(Roll(6, 6, 1, 2, 3, 4)));
            Assert.IsFalse(req.IsSatisfiedBy(Roll(5, 5, 1, 2, 3, 4)));
        }

        [Test]
        public void Run_DetectsConsecutiveFaces()
        {
            var req = new RunRequirement(4);
            Assert.IsTrue(req.IsSatisfiedBy(Roll(1, 2, 3, 4, 6, 6)));
            Assert.IsTrue(req.IsSatisfiedBy(Roll(3, 4, 5, 6, 1, 1)));
            Assert.IsFalse(req.IsSatisfiedBy(Roll(1, 2, 3, 5, 6, 6))); // gap at 4
        }

        [Test]
        public void Sum_ComparisonOperators()
        {
            Assert.IsTrue(new SumRequirement(20, ComparisonOp.AtLeast).IsSatisfiedBy(Roll(6, 6, 6, 2, 1, 1)));
            Assert.IsFalse(new SumRequirement(30, ComparisonOp.AtLeast).IsSatisfiedBy(Roll(1, 1, 1, 1, 1, 1)));
            Assert.IsTrue(new SumRequirement(6, ComparisonOp.Equal).IsSatisfiedBy(Roll(1, 1, 1, 1, 1, 1)));
            Assert.IsTrue(new SumRequirement(6, ComparisonOp.AtMost).IsSatisfiedBy(Roll(1, 1, 1, 1, 1, 1)));
        }

        [Test]
        public void ContainsFaces_RespectsMultiplicity()
        {
            var req = new ContainsFacesRequirement(new[] { 6, 6 });
            Assert.IsTrue(req.IsSatisfiedBy(Roll(6, 6, 1, 2, 3, 4)));
            Assert.IsFalse(req.IsSatisfiedBy(Roll(6, 1, 2, 3, 4, 5))); // only one 6
        }

        [Test]
        public void Composite_AllAndAny()
        {
            var all = new CompositeRequirement(CompositeRequirement.Mode.All,
                new NOfAKindRequirement(2, 6),
                new SumRequirement(24, ComparisonOp.AtLeast));
            Assert.IsTrue(all.IsSatisfiedBy(Roll(6, 6, 6, 6, 1, 1)));   // pair+ of 6s, sum 26
            Assert.IsFalse(all.IsSatisfiedBy(Roll(6, 6, 1, 1, 1, 1)));  // pair of 6s but sum 16 < 24

            var any = new CompositeRequirement(CompositeRequirement.Mode.Any,
                new RunRequirement(6),
                new NOfAKindRequirement(4));
            Assert.IsTrue(any.IsSatisfiedBy(Roll(3, 3, 3, 3, 1, 2)));   // four of a kind
            Assert.IsFalse(any.IsSatisfiedBy(Roll(3, 3, 1, 1, 2, 2)));  // neither
        }
    }
}
