using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The "I'm finished shaping" intent (follow-up #44). Lighter than commit-or-pass: it lets the
    /// driver close Shape early once everyone is done, without forcing anyone to decide their claim
    /// before the Commit window.
    /// </summary>
    public class DoneIntentTests
    {
        private MatchState _state;
        private LocalMatchSession _session;

        [SetUp]
        public void SetUp()
        {
            _state = Make.Match(Make.Config(), new[] { Make.Pair(1), Make.Pair(2) });
            _session = new LocalMatchSession(_state, new ConstantRoller(3));
            _session.AdvanceTo(RoundPhase.Shape);
        }

        [Test]
        public void ShapeClosesEarlyOnceEveryoneIsDone()
        {
            Assert.IsFalse(RulesEngine.AllDecided(_state));

            Assert.IsTrue(_session.Done(new PlayerId(0)).Success);
            Assert.IsFalse(RulesEngine.AllDecided(_state), "one seat still shaping");

            Assert.IsTrue(_session.Done(new PlayerId(1)).Success);
            Assert.IsTrue(RulesEngine.AllDecided(_state));
        }

        [Test]
        public void DoneMixesWithRealDecisions()
        {
            // One player locks in a pass during Shape (CORE-5); the other is merely done shaping.
            Assert.IsTrue(_session.Pass(new PlayerId(0)).Success);
            Assert.IsTrue(_session.Done(new PlayerId(1)).Success);

            Assert.IsTrue(RulesEngine.AllDecided(_state));
        }

        [Test]
        public void DoneCountsForNothingInCommit()
        {
            _session.Done(new PlayerId(0));
            _session.Done(new PlayerId(1));
            _session.Advance();

            Assert.AreEqual(RoundPhase.Commit, _state.Phase);
            Assert.IsFalse(RulesEngine.AllDecided(_state),
                "done shaping is not a claim decision; Commit must still wait");
        }

        [Test]
        public void DoneIsRejectedOutsideShape()
        {
            _session.Advance();
            Assert.AreEqual(RoundPhase.Commit, _state.Phase);

            var result = _session.Done(new PlayerId(0));
            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.WrongPhase, result.Failure);
        }

        [Test]
        public void WithdrawTakesDoneBack()
        {
            _session.Done(new PlayerId(0));
            _session.Done(new PlayerId(1));
            Assert.IsTrue(RulesEngine.AllDecided(_state));

            Assert.IsTrue(_session.Withdraw(new PlayerId(0)).Success);
            Assert.IsFalse(RulesEngine.AllDecided(_state));
        }

        [Test]
        public void SnapshotReportsDoneAsDecidedDuringShapeOnly()
        {
            _session.Done(new PlayerId(0));

            // Public to everyone, like HasDecided — the rail shows who is holding the phase open.
            var shapeView = MatchSnapshot.For(_state, new PlayerId(1));
            Assert.IsTrue(shapeView.Players[0].HasDecided);
            Assert.IsTrue(shapeView.Players[0].DoneShaping);

            _session.Advance();
            Assert.AreEqual(RoundPhase.Commit, _state.Phase);

            // The leftover flag must not read as a Commit-phase decision.
            var commitView = MatchSnapshot.For(_state, new PlayerId(1));
            Assert.IsFalse(commitView.Players[0].HasDecided);
            Assert.IsFalse(commitView.Players[0].DoneShaping);
        }
    }
}
