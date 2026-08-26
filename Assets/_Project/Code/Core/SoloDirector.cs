using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>Where a solo-vs-bots match currently stands.</summary>
    public enum SoloStage
    {
        /// <summary>The human is shaping and deciding; bots act on their own schedule.</summary>
        Acting,

        /// <summary>Holding at Reveal while the spotlight plays. Everyone's commit is public.</summary>
        Reveal,

        /// <summary>Upkeep is about to run; the round recap is on screen.</summary>
        RoundSummary,

        MatchOver
    }

    /// <summary>
    /// Drives a solo match: one human seat plus <see cref="BotPlayer"/>s in the other chairs
    /// (STORY-7.1). The private view stays pinned to the human for the whole match — there are no
    /// handoffs — while bots act through the same per-seat session commands a hot-seat player
    /// uses, each deciding from a snapshot built for its own seat.
    ///
    /// Pure and clockless like the rest of Core: the caller passes <c>now</c> into
    /// <see cref="Tick"/>, and bots act when their scheduled moment arrives — never instantly, so
    /// the table feels inhabited rather than resolved (AC2). Delays are drawn from a seeded
    /// xorshift, so a fixed seed replays the exact match.
    /// </summary>
    public sealed class SoloDirector
    {
        private readonly LocalMatchSession _session;
        private readonly List<BotPlayer> _bots;
        private readonly float _minDelay;
        private readonly float _maxDelay;
        private XorShift64Star _pacing;

        private readonly Dictionary<int, float> _dueAt = new Dictionary<int, float>();
        private readonly HashSet<int> _actedThisPass = new HashSet<int>();

        public SoloDirector(LocalMatchSession session, PlayerId human, IReadOnlyList<BotPlayer> bots,
            ulong pacingSeed, float minDelaySeconds = 1.4f, float maxDelaySeconds = 4.5f)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _bots = new List<BotPlayer>(bots ?? throw new ArgumentNullException(nameof(bots)));
            if (minDelaySeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minDelaySeconds),
                    "bots must never act instantly (STORY-7.1 AC2)");
            _minDelay = minDelaySeconds;
            _maxDelay = Math.Max(minDelaySeconds, maxDelaySeconds);
            _pacing = new XorShift64Star(pacingSeed);

            Human = human;
            _session.SetViewAs(human);     // the human owns the private view, permanently
        }

        public LocalMatchSession Session => _session;
        public MatchState State => _session.State;
        public PlayerId Human { get; }

        public SoloStage Stage { get; private set; } = SoloStage.Acting;

        /// <summary>True during a re-pick pass, when only contest losers act and shaping is over.</summary>
        public bool IsRepickPass { get; private set; }

        /// <summary>The most recent contention result, for the reveal screen.</summary>
        public ResolutionReport LastResolution => _session.LastResolution;

        /// <summary>False while a re-pick pass the human is not part of plays out.</summary>
        public bool HumanMayAct => Stage == SoloStage.Acting && (!IsRepickPass || IsContender(Human));

        /// <summary>Starts round one. Call once, on a fresh match.</summary>
        public void Begin(float now)
        {
            if (State.Phase != RoundPhase.Roll || State.Round != 0)
                throw new InvalidOperationException("SoloDirector.Begin must be called on a fresh match.");

            _session.Advance();            // Roll -> Shape
            StartPass(now, repick: false);
        }

        /// <summary>
        /// Advances the bot schedule. Call every frame (or with a synthetic clock in tests): due
        /// bots take their turn, and the pass closes once every participant has decided.
        /// </summary>
        public void Tick(float now)
        {
            if (Stage != SoloStage.Acting) return;

            foreach (var bot in _bots)
            {
                if (_actedThisPass.Contains(bot.Seat.Value)) continue;
                if (!Participates(bot.Seat)) continue;
                if (now < _dueAt[bot.Seat.Value]) continue;

                var seat = bot.Seat;
                bot.TakeTurn(() => MatchSnapshot.For(State, seat), _session);
                _actedThisPass.Add(seat.Value);
            }

            if (AllParticipantsDecided()) ClosePass();
        }

        /// <summary>
        /// The human is finished: auto-pass if undecided, mirroring hot-seat's EndActing and the
        /// server's phase timer. The pass still waits for any bot whose moment has not come.
        /// </summary>
        public void HumanDone(float now)
        {
            if (!HumanMayAct) return;

            var me = State.Find(Human);
            if (me != null && !me.HasCommitted && !me.HasPassed) _session.Pass(Human);

            Tick(now);
        }

        /// <summary>
        /// Leaves the reveal hold — applying the resolution the spotlight just showed — then into
        /// a re-pick pass or on to the summary. Mirrors <see cref="HotSeatDirector.ContinueFromReveal"/>.
        /// </summary>
        public void ContinueFromReveal(float now)
        {
            if (Stage != SoloStage.Reveal) return;

            if (State.Phase == RoundPhase.Reveal)
                _session.Advance();

            if (State.Phase == RoundPhase.Repick)
            {
                StartPass(now, repick: true);
                return;
            }

            Stage = SoloStage.RoundSummary;
        }

        /// <summary>Runs Upkeep and starts the next round, or ends the match.</summary>
        public void ContinueFromSummary(float now)
        {
            if (Stage != SoloStage.RoundSummary) return;

            _session.Advance();            // Upkeep -> Roll, or MatchOver

            if (State.Phase == RoundPhase.MatchOver)
            {
                Stage = SoloStage.MatchOver;
                return;
            }

            _session.Advance();            // Roll -> Shape
            StartPass(now, repick: false);
        }

        public IReadOnlyList<FinalScore> FinalScores() => _session.FinalScores();

        // ------------------------------------------------------------------ internals

        private void StartPass(float now, bool repick)
        {
            IsRepickPass = repick;
            _actedThisPass.Clear();
            _dueAt.Clear();

            foreach (var bot in _bots)
            {
                if (!Participates(bot.Seat)) continue;
                float spread = _pacing.NextBelow(1000) / 1000f;
                _dueAt[bot.Seat.Value] = now + _minDelay + spread * (_maxDelay - _minDelay);
            }

            Stage = SoloStage.Acting;

            // A pass with nobody left to decide (every contender somehow decided already) closes
            // immediately rather than stranding the match in Acting.
            if (AllParticipantsDecided()) ClosePass();
        }

        private bool Participates(PlayerId seat) => !IsRepickPass || IsContender(seat);

        private bool IsContender(PlayerId seat) => State.RepickContenders.Contains(seat);

        private bool AllParticipantsDecided()
        {
            if (IsRepickPass)
                return State.RepickContenders.All(id =>
                {
                    var p = State.Find(id);
                    return p == null || p.HasCommitted || p.HasPassed;
                });

            return State.Players.All(p => p.HasCommitted || p.HasPassed);
        }

        /// <summary>Everyone in this pass has decided; move to the reveal beat (or past it).</summary>
        private void ClosePass()
        {
            if (IsRepickPass)
            {
                // The engine holds at a second Reveal when any contender committed again (#43);
                // an all-pass re-pick resolves silently and goes straight to the summary.
                _session.Advance();
                Stage = State.Phase == RoundPhase.Reveal ? SoloStage.Reveal : SoloStage.RoundSummary;
                return;
            }

            // Shape -> Commit -> Reveal, and HOLD: the snapshot now carries the Reveals preview
            // for the spotlight. ContinueFromReveal applies the resolution afterwards.
            _session.AdvanceTo(RoundPhase.Reveal);
            Stage = SoloStage.Reveal;
        }
    }
}
