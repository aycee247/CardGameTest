using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The Shape phase: free allowances from powers are consumed before Sparks, and every
    /// manipulation is validated before anything is charged.
    /// </summary>
    public class ShapeTests
    {
        private MatchState _state;
        private LocalMatchSession _session;
        private PlayerState _p0;

        [SetUp]
        public void SetUp()
        {
            _state = Make.Match(Make.Config(), new[] { Make.Pair(1), Make.Pair(2) });
            _session = new LocalMatchSession(_state, new ConstantRoller(3));
            _p0 = _state.Players[0];
        }

        [Test]
        public void Reroll_SpendsFreeAllowanceBeforeSparks()
        {
            Make.Grant(_p0, Make.Pair(90, power: CardPower.FreeReroll(1), family: PowerFamily.Manipulation));
            _session.Advance();                 // Roll -> Shape, refills the allowance
            _p0.Sparks = 10;

            Assert.AreEqual(1, _p0.Allowance.Rerolls);

            Assert.IsTrue(_session.Shape(_p0.Id, ShapeAction.Reroll(0)).Success);
            Assert.AreEqual(0, _p0.Allowance.Rerolls);
            Assert.AreEqual(10, _p0.Sparks, "the free re-roll must not also charge Sparks");

            Assert.IsTrue(_session.Shape(_p0.Id, ShapeAction.Reroll(0)).Success);
            Assert.AreEqual(8, _p0.Sparks);
        }

        [Test]
        public void Reroll_WithoutAllowanceOrSparks_IsRejected()
        {
            _session.Advance();
            _p0.Sparks = 1;   // one short of RerollSparkCost

            var result = _session.Shape(_p0.Id, ShapeAction.Reroll(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.CannotAfford, result.Failure);
            Assert.AreEqual(1, _p0.Sparks, "a rejected action must not charge anything");
        }

        [Test]
        public void SetFace_CostsSparksAndWritesTheChosenFace()
        {
            _session.Advance();
            _p0.Sparks = 5;

            Assert.IsTrue(_session.Shape(_p0.Id, ShapeAction.SetFace(2, 6)).Success);

            Assert.AreEqual(6, _p0.Dice.FaceAt(2));
            Assert.AreEqual(1, _p0.Sparks);
        }

        [Test]
        public void SetFace_RejectsAFaceOutsideOneThroughSix()
        {
            _session.Advance();
            _p0.Sparks = 10;

            var result = _session.Shape(_p0.Id, ShapeAction.SetFace(0, 7));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.InvalidFace, result.Failure);
            Assert.AreEqual(10, _p0.Sparks, "validation must happen before the charge");
        }

        [Test]
        public void Nudge_MovesOneStepAndIsPowerOnly()
        {
            Make.Grant(_p0, Make.Pair(90, power: CardPower.FreeNudge(1), family: PowerFamily.Manipulation));
            _session.Advance();
            _p0.Sparks = 10;

            Assert.AreEqual(3, _p0.Dice.FaceAt(0));
            Assert.IsTrue(_session.Shape(_p0.Id, ShapeAction.Nudge(0, 1)).Success);
            Assert.AreEqual(4, _p0.Dice.FaceAt(0));

            // Sparks cannot buy a nudge, so the second one fails even with Sparks in hand.
            var second = _session.Shape(_p0.Id, ShapeAction.Nudge(0, 1));
            Assert.IsFalse(second.Success);
            Assert.AreEqual(MoveFailure.CannotAfford, second.Failure);
            Assert.AreEqual(10, _p0.Sparks);
        }

        [Test]
        public void Nudge_RejectsStepsOtherThanOneAndOffTheEnds()
        {
            Make.Grant(_p0, Make.Pair(90, power: CardPower.FreeNudge(4), family: PowerFamily.Manipulation));
            _session = new LocalMatchSession(_state, new ConstantRoller(6));
            _session.Advance();

            var tooBig = _session.Shape(_p0.Id, ShapeAction.Nudge(0, 2));
            Assert.AreEqual(MoveFailure.NudgeOutOfRange, tooBig.Failure);

            var pastSix = _session.Shape(_p0.Id, ShapeAction.Nudge(0, 1));
            Assert.AreEqual(MoveFailure.NudgeOutOfRange, pastSix.Failure);

            Assert.AreEqual(4, _p0.Allowance.Nudges, "rejected nudges must not consume the allowance");
        }

        [Test]
        public void Shape_OutsideTheShapePhase_IsRejected()
        {
            _session.AdvanceTo(RoundPhase.Commit);
            _p0.Sparks = 10;

            var result = _session.Shape(_p0.Id, ShapeAction.Reroll(0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.WrongPhase, result.Failure);
        }

        [Test]
        public void Shape_RejectsADieThatDoesNotExist()
        {
            _session.Advance();
            _p0.Sparks = 10;

            var result = _session.Shape(_p0.Id, ShapeAction.Reroll(99));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.NoSuchDie, result.Failure);
        }

        [Test]
        public void AllowancesRefillEveryRound()
        {
            Make.Grant(_p0, Make.Pair(90, power: CardPower.FreeReroll(2), family: PowerFamily.Manipulation));
            _session.Advance();

            Assert.IsTrue(_session.Shape(_p0.Id, ShapeAction.Reroll(0)).Success);
            Assert.AreEqual(1, _p0.Allowance.Rerolls);

            // Step off Shape first — AdvanceTo stops immediately if it is already there.
            _session.Advance();
            _session.AdvanceTo(RoundPhase.Shape);

            Assert.AreEqual(2, _state.Round);
            Assert.AreEqual(2, _p0.Allowance.Rerolls);
        }
    }
}
