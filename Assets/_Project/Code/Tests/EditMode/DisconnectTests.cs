using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// M4's gate: drops survived (NET-3). A dropped player must not stall the table, must keep
    /// everything they had won, and must be able to take their seat back.
    /// </summary>
    public class DisconnectTests
    {
        // ---------------------------------------------------------------- seat registry

        [Test]
        public void AReconnectingClientGetsItsOriginalSeat()
        {
            var seats = new SeatRegistry();
            seats.Bind("auth-alice", new PlayerId(0));
            seats.Bind("auth-bob", new PlayerId(1));

            seats.MarkDisconnected(new PlayerId(1), now: 0f);

            // Back with a brand new transport id, which is why the key is what owns the seat.
            Assert.IsTrue(seats.TryResolve("auth-bob", out var seat));
            Assert.AreEqual(new PlayerId(1), seat);
        }

        [Test]
        public void AnUnknownKeyGetsNoSeat()
        {
            var seats = new SeatRegistry();
            seats.Bind("auth-alice", new PlayerId(0));

            Assert.IsFalse(seats.TryResolve("auth-stranger", out _));
            Assert.IsFalse(seats.TryResolve("", out _));
            Assert.IsFalse(seats.TryResolve(null, out _));
        }

        [Test]
        public void StatusMovesFromReconnectingToAbandonedAtTheWindow()
        {
            var seats = new SeatRegistry(reconnectWindowSeconds: 45f);
            seats.Bind("a", new PlayerId(0));

            Assert.AreEqual(SeatStatus.Connected, seats.StatusOf(new PlayerId(0), 0f));

            seats.MarkDisconnected(new PlayerId(0), now: 10f);

            Assert.AreEqual(SeatStatus.Reconnecting, seats.StatusOf(new PlayerId(0), 20f));
            Assert.AreEqual(SeatStatus.Reconnecting, seats.StatusOf(new PlayerId(0), 55f));
            Assert.AreEqual(SeatStatus.Abandoned, seats.StatusOf(new PlayerId(0), 55.1f));
        }

        [Test]
        public void ReconnectSecondsLeftCountsDownAndFloorsAtZero()
        {
            var seats = new SeatRegistry(reconnectWindowSeconds: 45f);
            seats.Bind("a", new PlayerId(0));
            seats.MarkDisconnected(new PlayerId(0), now: 0f);

            Assert.AreEqual(45f, seats.ReconnectSecondsLeft(new PlayerId(0), 0f), 0.001f);
            Assert.AreEqual(15f, seats.ReconnectSecondsLeft(new PlayerId(0), 30f), 0.001f);
            Assert.AreEqual(0f, seats.ReconnectSecondsLeft(new PlayerId(0), 90f), 0.001f);
        }

        [Test]
        public void ASecondDropReportDoesNotRestartTheWindow()
        {
            var seats = new SeatRegistry(reconnectWindowSeconds: 45f);
            seats.Bind("a", new PlayerId(0));

            seats.MarkDisconnected(new PlayerId(0), now: 0f);
            seats.MarkDisconnected(new PlayerId(0), now: 40f);   // a duplicate callback

            Assert.AreEqual(SeatStatus.Abandoned, seats.StatusOf(new PlayerId(0), 46f),
                "a repeated disconnect report extended the window");
        }

        [Test]
        public void ReconnectingIsAllowedEvenAfterTheWindowCloses()
        {
            // Refusing would only punish someone whose connection took a long time to come back.
            // The seat is still theirs; the window governs what everyone is told, not ownership.
            var seats = new SeatRegistry(reconnectWindowSeconds: 45f);
            seats.Bind("a", new PlayerId(0));
            seats.MarkDisconnected(new PlayerId(0), now: 0f);

            Assert.AreEqual(SeatStatus.Abandoned, seats.StatusOf(new PlayerId(0), 500f));

            seats.MarkConnected(new PlayerId(0));

            Assert.AreEqual(SeatStatus.Connected, seats.StatusOf(new PlayerId(0), 500f));
            Assert.IsTrue(seats.IsConnected(new PlayerId(0)));
        }

        // ---------------------------------------------------------------- the table keeps moving

        private static LocalMatchSession SixPlayerMatch(out MatchState state)
        {
            var config = new MatchConfig { Rounds = 3, MarketSize = 5, StartingDice = 4 };
            state = Make.Match(config, Enumerable.Range(1, 30)
                .Select(i => Make.Pair(i, points: 1))
                .ToList(), playerCount: 6);

            return new LocalMatchSession(state, new ConstantRoller(4));
        }

        [Test]
        public void ADroppedPlayerDoesNotHoldTheTable()
        {
            var session = SixPlayerMatch(out var state);
            session.AdvanceTo(RoundPhase.Commit);

            // Everyone decides except the one who dropped.
            for (int seat = 0; seat < 5; seat++) session.Pass(new PlayerId(seat));

            Assert.IsFalse(RulesEngine.AllDecided(state), "still waiting on the sixth player");

            RulesEngine.SetConnected(state, new PlayerId(5), false);

            Assert.IsTrue(RulesEngine.AllDecided(state),
                "the table is still waiting out the phase timer for a device that is not there");
        }

        [Test]
        public void ADroppedPlayerIsAutoPassedAndKeepsWhatTheyWon()
        {
            var session = SixPlayerMatch(out var state);
            session.AdvanceTo(RoundPhase.Commit);

            var dropped = state.Players[3];
            Assert.IsTrue(session.Commit(dropped.Id, new CardId(1), new[] { 0, 1 }).Success);
            session.AdvanceTo(RoundPhase.Upkeep);
            session.Advance();

            int scoreBefore = dropped.Score;
            Assert.AreEqual(1, dropped.Owned.Count);

            RulesEngine.SetConnected(state, dropped.Id, false);

            // Play the rest of the match out around them.
            int guard = 0;
            while (state.Phase != RoundPhase.MatchOver && guard++ < 64) session.Advance();

            Assert.AreEqual(RoundPhase.MatchOver, state.Phase);
            Assert.AreEqual(scoreBefore, dropped.Score, "a dropped player lost points they had won");
            Assert.AreEqual(1, dropped.Owned.Count, "a dropped player lost a card they had claimed");
        }

        [Test]
        public void ADroppedPlayerIsAutoPassedWhenTheWindowCloses()
        {
            var session = SixPlayerMatch(out var state);
            session.AdvanceTo(RoundPhase.Commit);

            var dropped = state.Players[3];
            RulesEngine.SetConnected(state, dropped.Id, false);

            // Commit -> Reveal is where undecided players are passed. Checked here rather than at
            // the end of the match, because resolving a pass clears the flag again.
            session.Advance();

            Assert.AreEqual(RoundPhase.Reveal, state.Phase);
            Assert.IsTrue(dropped.HasPassed, "a dropped player should be auto-passed, not left pending");
            Assert.IsFalse(dropped.HasCommitted);
        }

        [Test]
        public void ADroppedPlayerStillAppearsInTheFinalStandings()
        {
            var session = SixPlayerMatch(out var state);
            RulesEngine.SetConnected(state, new PlayerId(2), false);

            int guard = 0;
            while (state.Phase != RoundPhase.MatchOver && guard++ < 64) session.Advance();

            var standings = Scoring.FinalScores(state);

            Assert.AreEqual(6, standings.Count);
            Assert.IsTrue(standings.Any(s => s.Player == new PlayerId(2)),
                "a dropped player was dropped from scoring too");
        }

        [Test]
        public void ReconnectingRestoresTheAbilityToAct()
        {
            var session = SixPlayerMatch(out var state);
            session.AdvanceTo(RoundPhase.Commit);

            var player = state.Players[2];
            RulesEngine.SetConnected(state, player.Id, false);

            RulesEngine.SetConnected(state, player.Id, true);

            Assert.IsFalse(RulesEngine.AllDecided(state), "the table should wait for them again");
            Assert.IsTrue(session.Commit(player.Id, new CardId(1), new[] { 0, 1 }).Success);
        }

        [Test]
        public void ARepickWaitsOnlyForContendersWhoAreStillThere()
        {
            var config = new MatchConfig { Rounds = 2, MarketSize = 3, StartingDice = 4 };
            var state = Make.Match(config,
                new[] { Make.Pair(1, points: 5), Make.Pair(2), Make.Pair(3) }, playerCount: 3);

            var session = new LocalMatchSession(state, new ConstantRoller(4));
            session.AdvanceTo(RoundPhase.Commit);

            // All three chase the same card, so two will be sent to a re-pick.
            for (int seat = 0; seat < 3; seat++)
                session.Commit(new PlayerId(seat), new CardId(1), new[] { 0, 1 });

            session.AdvanceTo(RoundPhase.Repick);
            Assert.AreEqual(2, state.RepickContenders.Count);

            var contenders = state.RepickContenders.ToList();
            session.Commit(contenders[0], new CardId(2), new[] { 0, 1 });

            Assert.IsFalse(RulesEngine.AllDecided(state));

            RulesEngine.SetConnected(state, contenders[1], false);

            Assert.IsTrue(RulesEngine.AllDecided(state),
                "the re-pick is stalling on a contender who has dropped");
        }
    }
}
