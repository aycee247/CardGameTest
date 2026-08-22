using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Where a pass-the-device match currently stands.</summary>
    public enum HotSeatStage
    {
        /// <summary>Waiting for the device to reach <see cref="HotSeatDirector.CurrentActor"/>. Nobody may act.</summary>
        Handoff,

        /// <summary>The current actor is shaping and deciding, in private.</summary>
        Acting,

        /// <summary>All commits are face-up and resolved. Everyone looks at this together.</summary>
        Reveal,

        /// <summary>Upkeep has paid out. Shown to the whole table before the next round.</summary>
        RoundSummary,

        MatchOver
    }

    /// <summary>
    /// Drives a hot-seat match: one device, players taking it in turn to shape and commit in
    /// private, then everyone watching the reveal together.
    ///
    /// This is deliberately pure and free of Unity so the whole flow — including the awkward
    /// parts, like a re-pick pass that only some players are entitled to — can be tested headlessly.
    /// The MonoBehaviour layer is a thin skin over it: render <see cref="Stage"/>, and call the
    /// four Continue/Confirm methods from buttons.
    ///
    /// The privacy rule is what makes hot-seat honest: <see cref="HotSeatStage.Handoff"/> exists so
    /// the previous player's commit is off screen before the next player looks at the device.
    /// </summary>
    public sealed class HotSeatDirector
    {
        private readonly LocalMatchSession _session;
        private readonly List<PlayerId> _queue = new List<PlayerId>();

        public HotSeatDirector(LocalMatchSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public LocalMatchSession Session => _session;
        public MatchState State => _session.State;

        public HotSeatStage Stage { get; private set; } = HotSeatStage.Handoff;

        /// <summary>Who should be holding the device. Meaningless outside Handoff/Acting.</summary>
        public PlayerId CurrentActor { get; private set; }

        /// <summary>Seats still to act this pass, current actor first.</summary>
        public IReadOnlyList<PlayerId> Queue => _queue;

        /// <summary>
        /// True when this pass is a re-pick, in which case only losers act and shaping is over for
        /// the round. The UI hides the dice controls.
        /// </summary>
        public bool IsRepickPass { get; private set; }

        /// <summary>The most recent contention result, for the reveal screen.</summary>
        public ResolutionReport LastResolution => _session.LastResolution;

        /// <summary>Starts round one and queues every seat. Call once.</summary>
        public void Begin()
        {
            if (State.Phase != RoundPhase.Roll || State.Round != 0)
                throw new InvalidOperationException("HotSeatDirector.Begin must be called on a fresh match.");

            _session.Advance();            // Roll -> Shape
            StartPass(AllSeats(), repick: false);
        }

        /// <summary>The device has reached the current actor; let them start acting.</summary>
        public void ConfirmHandoff()
        {
            if (Stage != HotSeatStage.Handoff) return;
            Stage = HotSeatStage.Acting;
        }

        /// <summary>
        /// True once the current actor has committed or passed, so the UI can enable "done".
        /// </summary>
        public bool CurrentActorHasDecided
        {
            get
            {
                var player = State.Find(CurrentActor);
                return player != null && (player.HasCommitted || player.HasPassed);
            }
        }

        /// <summary>
        /// Hands the device on. Anyone who has not decided is treated as passing, mirroring what
        /// the server's phase timer does online.
        /// </summary>
        public void EndActing()
        {
            if (Stage != HotSeatStage.Acting) return;

            if (!CurrentActorHasDecided) _session.Pass(CurrentActor);

            _queue.RemoveAt(0);

            if (_queue.Count > 0)
            {
                EnterHandoff(_queue[0]);
                return;
            }

            ClosePass();
        }

        /// <summary>
        /// Leaves the reveal screen. The pass held at <see cref="RoundPhase.Reveal"/> while the
        /// spotlight played from the snapshot's preview — applying the resolution is deferred to
        /// here, so what was shown and what happens are the same computation (UI-4). Then either
        /// into a re-pick pass, or on to the summary.
        /// </summary>
        public void ContinueFromReveal()
        {
            if (Stage != HotSeatStage.Reveal) return;

            if (State.Phase == RoundPhase.Reveal)
                _session.Advance();        // apply the resolution the spotlight just showed

            if (State.Phase == RoundPhase.Repick)
            {
                StartPass(new List<PlayerId>(State.RepickContenders), repick: true);
                return;
            }

            Stage = HotSeatStage.RoundSummary;
        }

        /// <summary>Runs Upkeep and starts the next round, or ends the match.</summary>
        public void ContinueFromSummary()
        {
            if (Stage != HotSeatStage.RoundSummary) return;

            _session.Advance();            // Upkeep -> Roll, or MatchOver

            if (State.Phase == RoundPhase.MatchOver)
            {
                Stage = HotSeatStage.MatchOver;
                return;
            }

            _session.Advance();            // Roll -> Shape
            StartPass(AllSeats(), repick: false);
        }

        public IReadOnlyList<FinalScore> FinalScores() => _session.FinalScores();

        // ------------------------------------------------------------------ internals

        private List<PlayerId> AllSeats()
        {
            var seats = new List<PlayerId>(State.Players.Count);
            foreach (var p in State.Players) seats.Add(p.Id);
            return seats;
        }

        private void StartPass(List<PlayerId> seats, bool repick)
        {
            _queue.Clear();
            _queue.AddRange(seats);
            IsRepickPass = repick;

            if (_queue.Count == 0)
            {
                ClosePass();
                return;
            }

            EnterHandoff(_queue[0]);
        }

        /// <summary>
        /// Moves the private view to the next actor *before* the handoff screen goes up, so the
        /// previous player's commit is already out of the snapshot by the time the device changes
        /// hands. Waiting until they confirm would leave it on screen in between.
        /// </summary>
        private void EnterHandoff(PlayerId next)
        {
            CurrentActor = next;
            _session.SetViewAs(next);
            Stage = HotSeatStage.Handoff;
        }

        /// <summary>Everyone in this pass has acted; show the table what is about to resolve.</summary>
        private void ClosePass()
        {
            if (IsRepickPass)
            {
                // The second pass has no reveal window in the engine; its outcome surfaces in the
                // summary rather than a second spotlight (logged scope decision).
                _session.Advance();        // Repick -> resolve -> Upkeep
                Stage = HotSeatStage.RoundSummary;
                return;
            }

            // Shape -> Commit -> Reveal, and HOLD: the snapshot now carries the Reveals preview
            // for the spotlight. ContinueFromReveal applies the resolution afterwards.
            _session.AdvanceTo(RoundPhase.Reveal);
            Stage = HotSeatStage.Reveal;
        }
    }
}
