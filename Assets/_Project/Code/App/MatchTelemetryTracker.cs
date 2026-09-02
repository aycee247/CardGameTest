using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Derives the match funnel events (docs/product-plan.md, F1) from the snapshot stream, so one
    /// class serves hot-seat, solo and online without any of them growing telemetry calls of their
    /// own. Entirely observer-independent: it reads only Phase, Round and the standings, which are
    /// identical in every recipient's snapshot.
    ///
    /// Timing is wall-clock (<see cref="Time.realtimeSinceStartup"/>) measured here in the App
    /// layer — Core never ticks a clock, and this must not tempt it to.
    ///
    /// A rematch needs no special handling: the first snapshot after a completed match that is no
    /// longer over resets the tracker and counts as a new match.
    /// </summary>
    public sealed class MatchTelemetryTracker
    {
        private readonly ITelemetry _telemetry;
        private readonly string _mode;

        private bool _started;
        private bool _completed;
        private RoundPhase _lastPhase;
        private int _lastRound;
        private int _players;
        private float _matchStartedAt;
        private float _phaseStartedAt;
        private float _roundStartedAt;

        /// <param name="mode">"hotseat", "solo" or "online" — set by whichever bootstrap path ran.</param>
        public MatchTelemetryTracker(ITelemetry telemetry, string mode)
        {
            _telemetry = telemetry;
            _mode = mode;
        }

        /// <summary>Feed every snapshot here; signature matches <see cref="IMatchView.Changed"/>.</summary>
        public void OnSnapshot(MatchSnapshot snapshot)
        {
            if (_telemetry == null) return;

            if (!_started || (_completed && !snapshot.IsMatchOver))
            {
                BeginMatch(snapshot);
                return;
            }

            if (_completed) return;   // MatchOver snapshots keep arriving; one completion is enough

            float now = Time.realtimeSinceStartup;

            if (snapshot.Phase != _lastPhase)
            {
                _telemetry.Record("phase_ended", new Dictionary<string, object>
                {
                    ["phase"] = _lastPhase.ToString(),
                    ["round"] = _lastRound,
                    ["seconds"] = now - _phaseStartedAt,
                });
                _lastPhase = snapshot.Phase;
                _phaseStartedAt = now;
            }

            if (snapshot.Round != _lastRound)
            {
                _telemetry.Record("round_completed", new Dictionary<string, object>
                {
                    ["round"] = _lastRound,
                    ["seconds"] = now - _roundStartedAt,
                });
                _lastRound = snapshot.Round;
                _roundStartedAt = now;
            }

            if (snapshot.IsMatchOver) CompleteMatch(snapshot, now);
        }

        /// <summary>
        /// Call from the scene's teardown. A match that started but never reached MatchOver was
        /// abandoned — the round it died in is where matches are being lost.
        /// </summary>
        public void NotifyClosed()
        {
            if (_telemetry == null || !_started || _completed) return;

            _telemetry.Record("match_abandoned", new Dictionary<string, object>
            {
                ["mode"] = _mode,
                ["round"] = _lastRound,
            });
            _started = false;
        }

        private void BeginMatch(in MatchSnapshot snapshot)
        {
            float now = Time.realtimeSinceStartup;

            _started = true;
            _completed = false;
            _lastPhase = snapshot.Phase;
            _lastRound = snapshot.Round;
            _players = snapshot.Players?.Length ?? 0;
            _matchStartedAt = now;
            _phaseStartedAt = now;
            _roundStartedAt = now;

            _telemetry.Record("match_started", new Dictionary<string, object>
            {
                ["mode"] = _mode,
                ["players"] = _players,
            });

            // A snapshot can arrive already at MatchOver (a client joining the tail end); count it
            // started-and-done rather than leaving the tracker armed to misreport an abandon.
            if (snapshot.IsMatchOver) CompleteMatch(snapshot, now);
        }

        private void CompleteMatch(in MatchSnapshot snapshot, float now)
        {
            _completed = true;

            var parameters = new Dictionary<string, object>
            {
                ["mode"] = _mode,
                ["players"] = _players,
                ["seconds"] = now - _matchStartedAt,
                ["rounds"] = snapshot.TotalRounds,
            };

            // The local seat's result — meaningful solo and online, where the device is one
            // player. Hot-seat shares the device, so "local" would be whichever seat looked last.
            if (_mode != "hotseat" && snapshot.Standings != null)
            {
                foreach (var standing in snapshot.Standings)
                {
                    if (standing.PlayerId != snapshot.ObserverId) continue;
                    parameters["local_rank"] = standing.Rank;
                    parameters["local_won"] = standing.Rank == 0;
                    break;
                }
            }

            _telemetry.Record("match_completed", parameters);
        }
    }
}
