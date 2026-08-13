using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Commit validation (MKT-2) and contested-claim resolution (MKT-3, MKT-4) — the mechanic the
    /// whole simultaneous design rests on.
    /// </summary>
    public class ContentionTests
    {
        /// <summary>Two players, four dice each, both holding a pair, with a two-card market.</summary>
        private static LocalMatchSession TwoWayContest(out MatchState state, int prizePoints = 5)
        {
            var config = Make.Config(rounds: 3, marketSize: 2);
            state = Make.Match(config, new[]
            {
                Make.Pair(1, points: prizePoints),
                Make.Pair(2, points: 1)
            });

            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 },
                new[] { 5, 5, 3, 4 }));

            session.AdvanceTo(RoundPhase.Commit);
            return session;
        }

        [Test]
        public void UncontestedClaims_AllLand()
        {
            var session = TwoWayContest(out var state);

            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 }).Success);
            Assert.IsTrue(session.Commit(new PlayerId(1), new CardId(2), new[] { 0, 1 }).Success);

            session.Advance();                       // Commit -> Reveal
            var report = session.LastResolution;
            Assert.IsNull(report, "nothing resolves until Reveal is processed");

            session.Advance();                       // Reveal -> resolve
            report = session.LastResolution;

            Assert.IsFalse(report.HadContention);
            Assert.IsEmpty(report.Losers);
            Assert.AreEqual(1, state.Players[0].Owned.Count);
            Assert.AreEqual(1, state.Players[1].Owned.Count);
            Assert.AreEqual(RoundPhase.Upkeep, state.Phase);
        }

        [Test]
        public void ContestedCard_GoesToThePriorityHolder()
        {
            var session = TwoWayContest(out var state);

            // Both players start on zero, so priority falls to the lower seat.
            Assert.AreEqual(0, state.PriorityRank(new PlayerId(0)));

            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1 });

            session.AdvanceTo(RoundPhase.Repick);
            var report = session.LastResolution;

            Assert.IsTrue(report.HadContention);
            Assert.AreEqual(1, state.Players[0].Owned.Count);
            Assert.AreEqual(0, state.Players[1].Owned.Count);
            CollectionAssert.AreEqual(new[] { new PlayerId(1) }, report.Losers.ToArray());
        }

        [Test]
        public void Loser_KeepsDiceUnspent_AndCanRepick()
        {
            var session = TwoWayContest(out var state);

            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1 });
            session.AdvanceTo(RoundPhase.Repick);

            var loser = state.Players[1];
            Assert.AreEqual(4, loser.Dice.UnspentCount, "a losing bid must not consume dice");
            CollectionAssert.AreEqual(new[] { 1 }, state.RepickContenders.Select(p => p.Value).ToArray());

            Assert.IsTrue(session.Commit(loser.Id, new CardId(2), new[] { 0, 1 }).Success);
            session.Advance();

            Assert.AreEqual(1, loser.Owned.Count);
            Assert.AreEqual(2, loser.Dice.UnspentCount, "the granted claim now spends its two dice");
            Assert.AreEqual(RoundPhase.Upkeep, state.Phase);
        }

        [Test]
        public void Repick_IsClosedToPlayersWhoDidNotLose()
        {
            var session = TwoWayContest(out var state);

            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1 });
            session.AdvanceTo(RoundPhase.Repick);

            var result = session.Commit(new PlayerId(0), new CardId(2), new[] { 2, 3 });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.NotAContender, result.Failure);
        }

        [Test]
        public void NoRepickOffered_WhenTheMarketIsEmpty()
        {
            var config = Make.Config(rounds: 3, marketSize: 1);
            var state = Make.Match(config, new[] { Make.Pair(1) });
            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 },
                new[] { 5, 5, 3, 4 }));

            session.AdvanceTo(RoundPhase.Commit);
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.Commit(new PlayerId(1), new CardId(1), new[] { 0, 1 });

            session.Advance();  // -> Reveal
            session.Advance();  // resolve

            Assert.IsNotEmpty(session.LastResolution.Losers);
            Assert.AreEqual(RoundPhase.Upkeep, state.Phase, "there is nothing left to re-pick from");
        }

        [Test]
        public void Commit_RejectsDiceThatDoNotPayTheCost()
        {
            var config = Make.Config(rounds: 3, marketSize: 1);
            var state = Make.Match(config, new[] { Make.Card(1, new NOfAKindRequirement(3)) });
            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 },
                new[] { 5, 5, 3, 4 }));
            session.AdvanceTo(RoundPhase.Commit);

            var result = session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.CostNotMet, result.Failure);
            Assert.IsFalse(state.Players[0].HasCommitted);
        }

        [Test]
        public void Commit_RejectsMalformedOffers()
        {
            var session = TwoWayContest(out var state);
            var p0 = new PlayerId(0);

            Assert.AreEqual(MoveFailure.CardNotInMarket, session.Commit(p0, new CardId(99), new[] { 0, 1 }).Failure);
            Assert.AreEqual(MoveFailure.NoDiceOffered, session.Commit(p0, new CardId(1), new int[0]).Failure);
            Assert.AreEqual(MoveFailure.DuplicateDie, session.Commit(p0, new CardId(1), new[] { 0, 0 }).Failure);
            Assert.AreEqual(MoveFailure.NoSuchDie, session.Commit(p0, new CardId(1), new[] { 0, 42 }).Failure);
        }

        [Test]
        public void Commit_RejectsASecondCommitInTheSamePass()
        {
            var session = TwoWayContest(out var state);

            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 }).Success);
            var second = session.Commit(new PlayerId(0), new CardId(2), new[] { 0, 1 });

            Assert.IsFalse(second.Success);
            Assert.AreEqual(MoveFailure.AlreadyCommitted, second.Failure);
        }

        [Test]
        public void Commit_OutsideACommitWindow_IsRejected()
        {
            var config = Make.Config();
            var state = Make.Match(config, new[] { Make.Pair(1), Make.Pair(2) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));

            session.Advance();   // -> Shape

            var result = session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.WrongPhase, result.Failure);
        }

        [Test]
        public void PriorityFollowsTheTrailingPlayer()
        {
            var session = TwoWayContest(out var state, prizePoints: 5);

            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.AdvanceTo(RoundPhase.Upkeep);
            session.Advance();   // Upkeep recomputes priority

            // P0 now leads on points, so P1 takes first pick next round (MKT-4).
            Assert.AreEqual(5, state.Players[0].Score);
            Assert.AreEqual(0, state.Players[1].Score);
            Assert.AreEqual(0, state.PriorityRank(new PlayerId(1)));
            Assert.AreEqual(1, state.PriorityRank(new PlayerId(0)));
        }

        [Test]
        public void PriorityTiesBreakByFewestCardsThenSeat()
        {
            var config = Make.Config();
            var state = Make.Match(config, new[] { Make.Pair(1) }, playerCount: 3);

            // All three are level on 4 points, but P0 spread them across two cards.
            Make.Grant(state.Players[0], Make.Card(50, new SumRequirement(0), points: 2));
            Make.Grant(state.Players[0], Make.Card(51, new SumRequirement(0), points: 2));
            Make.Grant(state.Players[1], Make.Card(52, new SumRequirement(0), points: 4));
            Make.Grant(state.Players[2], Make.Card(53, new SumRequirement(0), points: 4));

            state.RecomputePriority();

            // Fewest cards wins the tie, so P0 drops behind the two single-card players,
            // and the remaining P1/P2 tie falls to seat order.
            CollectionAssert.AreEqual(
                new[] { 1, 2, 0 },
                state.PriorityOrder.Select(p => p.Value).ToArray());
        }
    }
}
