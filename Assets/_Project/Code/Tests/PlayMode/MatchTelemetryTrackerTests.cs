using System.Collections.Generic;
using Game.App;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// <see cref="MatchTelemetryTracker"/> is plain C# (no MonoBehaviour, no coroutine), so these
    /// are ordinary NUnit tests over a fake <see cref="ITelemetry"/> — no scene load needed. Lives
    /// in Game.PlayModeTests rather than the EditMode suite because it references Game.App, which
    /// is not noEngineReferences (Core-only) and so cannot be exercised by tools/run-core-tests.sh.
    ///
    /// Regression coverage for the #101 review: the final round used to go unreported, and solo
    /// used to record a fake round-0 match start.
    /// </summary>
    public sealed class MatchTelemetryTrackerTests
    {
        private sealed class FakeTelemetry : ITelemetry
        {
            public readonly List<(string Name, IReadOnlyDictionary<string, object> Parameters)> Events = new();

            public void Record(string eventName, IReadOnlyDictionary<string, object> parameters = null) =>
                Events.Add((eventName, parameters));
        }

        private static MatchSnapshot Snapshot(RoundPhase phase, int round, int totalRounds = 10,
            int players = 2, bool isMatchOver = false, int observerId = 0,
            FinalScoreSnapshot[] standings = null) => new MatchSnapshot
        {
            ObserverId = observerId,
            Phase = phase,
            Round = round,
            TotalRounds = totalRounds,
            Players = new PlayerSnapshot[players],
            IsMatchOver = isMatchOver,
            Standings = standings,
        };

        [Test]
        public void FirstSnapshot_RecordsMatchStarted()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "solo");

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1, players: 3));

            Assert.AreEqual(1, telemetry.Events.Count);
            Assert.AreEqual("match_started", telemetry.Events[0].Name);
            Assert.AreEqual("solo", telemetry.Events[0].Parameters["mode"]);
            Assert.AreEqual(3, telemetry.Events[0].Parameters["players"]);
        }

        [Test]
        public void ARoundBoundary_RecordsRoundCompletedForThePreviousRound()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "hotseat");

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 2));

            var roundCompleted = telemetry.Events.Find(e => e.Name == "round_completed");
            Assert.AreEqual(1, roundCompleted.Parameters["round"],
                "round_completed should name the round that just ended, not the one starting.");
        }

        /// <summary>
        /// Regression for the #101 review: RulesEngine.RunUpkeep leaves Round at the configured
        /// final value when it transitions straight to MatchOver, so a boundary keyed only on
        /// Round changing never fires for the last round.
        /// </summary>
        [Test]
        public void TheFinalRound_StillEmitsRoundCompleted()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "hotseat");

            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, totalRounds: 10));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, totalRounds: 10, isMatchOver: true));

            int roundCompletedCount = telemetry.Events.FindAll(e => e.Name == "round_completed").Count;
            Assert.AreEqual(1, roundCompletedCount,
                "the final round boundary (Round unchanged, IsMatchOver newly true) must still count.");

            var last = telemetry.Events.Find(e => e.Name == "round_completed");
            Assert.AreEqual(10, last.Parameters["round"]);
        }

        [Test]
        public void MatchOver_RecordsMatchCompletedExactlyOnce_EvenAcrossRepeatedSnapshots()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "online");
            var standings = new[]
            {
                new FinalScoreSnapshot { PlayerId = 0, Rank = 0, Total = 40 },
                new FinalScoreSnapshot { PlayerId = 1, Rank = 1, Total = 30 },
            };

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true, observerId: 0, standings: standings));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true, observerId: 0, standings: standings));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true, observerId: 0, standings: standings));

            var completions = telemetry.Events.FindAll(e => e.Name == "match_completed");
            Assert.AreEqual(1, completions.Count, "a MatchOver snapshot repeats every frame it stays on screen.");
            Assert.AreEqual(0, completions[0].Parameters["local_rank"]);
            Assert.AreEqual(true, completions[0].Parameters["local_won"]);
        }

        [Test]
        public void HotSeat_NeverRecordsALocalResult_TheDeviceIsSharedNotOneSeat()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "hotseat");
            var standings = new[] { new FinalScoreSnapshot { PlayerId = 0, Rank = 0, Total = 40 } };

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true, standings: standings));

            var completed = telemetry.Events.Find(e => e.Name == "match_completed");
            Assert.IsFalse(completed.Parameters.ContainsKey("local_rank"));
        }

        [Test]
        public void ASnapshotArrivingAlreadyOver_CountsAsStartedAndCompleted_NotAbandoned()
        {
            // A client joining a match's tail end — its first snapshot is already MatchOver.
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "online");

            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true));
            tracker.NotifyClosed();

            CollectionAssert.AreEqual(
                new[] { "match_started", "round_completed", "match_completed" },
                telemetry.Events.ConvertAll(e => e.Name));
        }

        [Test]
        public void ClosingBeforeMatchOver_RecordsAbandonedAtTheCurrentRound()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "solo");

            tracker.OnSnapshot(Snapshot(RoundPhase.Shape, round: 4));
            tracker.NotifyClosed();

            var abandoned = telemetry.Events.Find(e => e.Name == "match_abandoned");
            Assert.IsNotNull(abandoned.Name);
            Assert.AreEqual(4, abandoned.Parameters["round"]);
        }

        [Test]
        public void ClosingAfterMatchOver_RecordsNoAbandon()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "solo");

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true));
            tracker.NotifyClosed();

            Assert.IsNull(telemetry.Events.Find(e => e.Name == "match_abandoned").Name);
        }

        [Test]
        public void ARematch_StartsAFreshMatchStarted()
        {
            var telemetry = new FakeTelemetry();
            var tracker = new MatchTelemetryTracker(telemetry, "hotseat");

            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
            tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true));
            // The new match's first snapshot: MatchOver has cleared, Round is back to 1.
            tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));

            var starts = telemetry.Events.FindAll(e => e.Name == "match_started");
            Assert.AreEqual(2, starts.Count);
        }

        [Test]
        public void WithNoTelemetryService_EveryCallIsANoOp()
        {
            var tracker = new MatchTelemetryTracker(null, "solo");

            Assert.DoesNotThrow(() =>
            {
                tracker.OnSnapshot(Snapshot(RoundPhase.Roll, round: 1));
                tracker.OnSnapshot(Snapshot(RoundPhase.Upkeep, round: 10, isMatchOver: true));
                tracker.NotifyClosed();
            });
        }
    }
}
