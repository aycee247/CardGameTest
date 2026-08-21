using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// NET-2, the load-bearing security requirement. Commits are secret until Reveal, so a snapshot
    /// must be built per recipient — a client that can see an opponent's pending claim wins every
    /// contest for free.
    /// </summary>
    public class SnapshotSecrecyTests
    {
        private MatchState _state;
        private LocalMatchSession _session;

        [SetUp]
        public void SetUp()
        {
            _state = Make.Match(Make.Config(rounds: 2, marketSize: 2),
                new[] { Make.Pair(1, points: 5), Make.Pair(2) });

            _session = new LocalMatchSession(_state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 },
                new[] { 5, 5, 3, 4 }));

            _session.AdvanceTo(RoundPhase.Commit);
            _session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
        }

        private static PlayerSnapshot RowFor(MatchSnapshot snapshot, int playerId) =>
            snapshot.Players.First(p => p.PlayerId == playerId);

        [Test]
        public void OpponentsCannotSeeAPendingCommit()
        {
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));
            var p0Row = RowFor(asP1, 0);

            Assert.AreEqual(-1, p0Row.PendingCardId, "the claimed card must not leak before Reveal");
            Assert.IsEmpty(p0Row.PendingDice, "the dice backing the claim must not leak either");
            Assert.IsFalse(p0Row.HasCommitted);
        }

        [Test]
        public void ThatSomeoneHasDecidedIsStillPublic()
        {
            // The opponent rail has to show who is still thinking (UI-1); only the choice is secret.
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));

            Assert.IsTrue(RowFor(asP1, 0).HasDecided);
            Assert.IsFalse(RowFor(asP1, 1).HasDecided);
        }

        [Test]
        public void APlayerSeesTheirOwnCommit()
        {
            var asP0 = MatchSnapshot.For(_state, new PlayerId(0));

            Assert.AreEqual(1, asP0.Observer.PendingCardId);
            Assert.IsTrue(asP0.Observer.HasCommitted);
            CollectionAssert.AreEqual(new[] { 0, 1 }, asP0.Observer.PendingDice);
        }

        [Test]
        public void CommitsBecomeVisibleToEveryoneAtReveal()
        {
            _session.Advance();   // Commit -> Reveal

            Assert.AreEqual(RoundPhase.Reveal, _state.Phase);

            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));
            var p0Row = RowFor(asP1, 0);

            Assert.IsTrue(p0Row.HasCommitted);
            Assert.AreEqual(1, p0Row.PendingCardId);
            CollectionAssert.AreEqual(new[] { 0, 1 }, p0Row.PendingDice);
        }

        [Test]
        public void DiceFacesArePublic()
        {
            // Deliberate: reading that an opponent rolled a pair of 5s is the whole basis of
            // deciding whether to contest a card. Only the commit is hidden.
            var asP0 = MatchSnapshot.For(_state, new PlayerId(0));

            CollectionAssert.AreEqual(new[] { 5, 5, 3, 4 }, RowFor(asP0, 1).DiceFaces);
        }

        [Test]
        public void FreeAllowancesAreOnlyReportedToTheirOwner()
        {
            Make.Grant(_state.Players[0], Make.Pair(90, power: CardPower.FreeReroll(2), family: PowerFamily.Manipulation));
            _state.Players[0].RefillAllowance();

            var asP0 = MatchSnapshot.For(_state, new PlayerId(0));
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));

            Assert.AreEqual(2, asP0.Observer.RerollsLeft);
            Assert.AreEqual(0, RowFor(asP1, 0).RerollsLeft);
        }

        [Test]
        public void WildPowersAreOnlyReportedToTheirOwner()
        {
            // Knowing an opponent holds a wild lets you price their reach into every contest —
            // same class of leak as the free allowances above.
            Make.Grant(_state.Players[0], Make.Pair(91, power: CardPower.WildFace(6), family: PowerFamily.Wild));
            Make.Grant(_state.Players[0], Make.Pair(92, power: CardPower.WildDie(1), family: PowerFamily.Wild));

            var asP0 = MatchSnapshot.For(_state, new PlayerId(0));
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));

            CollectionAssert.AreEqual(new[] { 6 }, asP0.Observer.WildFaces);
            Assert.AreEqual(1, asP0.Observer.WildDice);

            Assert.IsEmpty(RowFor(asP1, 0).WildFaces);
            Assert.AreEqual(0, RowFor(asP1, 0).WildDice);
        }

        [Test]
        public void AffordabilityIsComputedAgainstTheObserversOwnDice()
        {
            var threeOfAKind = Make.Card(3, new NOfAKindRequirement(3));
            var state = Make.Match(Make.Config(rounds: 2, marketSize: 1), new[] { threeOfAKind });

            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 4, 4, 4, 1 },
                new[] { 1, 2, 3, 5 }));
            session.Advance();

            Assert.IsTrue(MatchSnapshot.For(state, new PlayerId(0)).Market[0].AffordableNow);
            Assert.IsFalse(MatchSnapshot.For(state, new PlayerId(1)).Market[0].AffordableNow);
        }

        [Test]
        public void SnapshotCarriesTheRoundAndPriorityContext()
        {
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));

            Assert.AreEqual(1, asP1.ObserverId);
            Assert.AreEqual(1, asP1.Round);
            Assert.AreEqual(2, asP1.TotalRounds);
            Assert.AreEqual(RoundPhase.Commit, asP1.Phase);
            CollectionAssert.AreEqual(new[] { 0, 1 }, asP1.PriorityOrder);
            Assert.IsFalse(asP1.IsMatchOver);
        }
    }
}
