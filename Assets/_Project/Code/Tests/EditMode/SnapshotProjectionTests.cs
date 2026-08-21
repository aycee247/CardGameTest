using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The snapshot projections added for the mobile UI: the config echo, the Reveal preview and
    /// the final standings. Each must be phase-gated, identical for every observer, and agree
    /// exactly with the engine that later applies it.
    /// </summary>
    public class SnapshotProjectionTests
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
                new[] { 5, 5, 3, 4 },
                new[] { 1, 2, 3, 4 },    // round 2, for the tests that play to MatchOver
                new[] { 2, 3, 4, 5 }));
        }

        private void CommitBothToCard1()
        {
            _session.AdvanceTo(RoundPhase.Commit);
            _session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            _session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1 });
        }

        [Test]
        public void ConfigEchoReachesEveryObserver()
        {
            var snapshot = MatchSnapshot.For(_state, new PlayerId(1));

            Assert.IsNotNull(snapshot.Config);
            Assert.AreEqual(_state.Config.SparkCap, snapshot.Config.SparkCap);
            Assert.AreEqual(_state.Config.RerollSparkCost, snapshot.Config.RerollSparkCost);
            Assert.AreEqual(_state.Config.ShapeSeconds,
                snapshot.Config.DurationOf(RoundPhase.Shape), 0.001f);
        }

        [Test]
        public void RevealsAreEmptyBeforeReveal()
        {
            CommitBothToCard1();

            Assert.AreEqual(RoundPhase.Commit, _state.Phase);
            Assert.IsEmpty(MatchSnapshot.For(_state, new PlayerId(0)).Reveals);
            Assert.IsEmpty(MatchSnapshot.For(_state, new PlayerId(1)).Reveals);
        }

        [Test]
        public void RevealProjectionShowsTheContestAndItsWinner()
        {
            CommitBothToCard1();
            _session.Advance();   // Commit -> Reveal

            var asP0 = MatchSnapshot.For(_state, new PlayerId(0));
            var asP1 = MatchSnapshot.For(_state, new PlayerId(1));

            // Identical for every observer — commits are public in this phase anyway.
            Assert.AreEqual(Dump.Of(asP0.Reveals), Dump.Of(asP1.Reveals));

            Assert.AreEqual(1, asP0.Reveals.Length);
            var reveal = asP0.Reveals[0];
            Assert.AreEqual(1, reveal.CardId);
            Assert.AreEqual(5, reveal.Points);
            Assert.IsTrue(reveal.Contested);
            Assert.AreEqual(2, reveal.ClaimantIds.Length);
            Assert.AreEqual(reveal.ClaimantIds[0], reveal.WinnerId, "winner leads the claimants");
            Assert.IsNotEmpty(reveal.DisplayName, "display data must come from the live market");
        }

        [Test]
        public void PreviewMatchesWhatResolutionThenDoes()
        {
            CommitBothToCard1();
            _session.Advance();   // Commit -> Reveal

            var preview = RulesEngine.PreviewResolution(_state);
            _session.Advance();   // Reveal -> resolve
            var applied = _session.LastResolution;

            Assert.AreEqual(applied.HadContention, preview.HadContention);
            CollectionAssert.AreEqual(
                applied.Losers.Select(l => l.Value).ToArray(),
                preview.Losers.Select(l => l.Value).ToArray());
            CollectionAssert.AreEqual(
                applied.Outcomes.Select(o => (o.Player.Value, o.Card.Value, o.Granted)).ToArray(),
                preview.Outcomes.Select(o => (o.Player.Value, o.Card.Value, o.Granted)).ToArray());
        }

        [Test]
        public void StandingsAppearOnlyAtMatchOverAndMatchScoring()
        {
            Assert.IsEmpty(MatchSnapshot.For(_state, new PlayerId(0)).Standings);

            _session.AdvanceTo(RoundPhase.MatchOver);

            var snapshot = MatchSnapshot.For(_state, new PlayerId(1));
            var expected = Scoring.FinalScores(_state);

            Assert.AreEqual(expected.Count, snapshot.Standings.Length);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Player.Value, snapshot.Standings[i].PlayerId);
                Assert.AreEqual(expected[i].Total, snapshot.Standings[i].Total);
                Assert.AreEqual(expected[i].PowerPoints, snapshot.Standings[i].PowerPoints);
                Assert.AreEqual(i, snapshot.Standings[i].Rank);
            }

            Assert.AreEqual(snapshot.Standings[0].PlayerId, snapshot.WinnerId,
                "rank 0 and WinnerId must agree");
        }
    }
}
