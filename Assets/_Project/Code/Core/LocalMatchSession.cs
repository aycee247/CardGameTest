using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A fully offline session that drives <see cref="RulesEngine"/> directly, with no networking
    /// and no clock. It plays two roles:
    ///
    /// - the <see cref="IGameActions"/>/<see cref="IMatchView"/> boundary the UI binds to, so hot-seat
    ///   play exercises exactly the same code path as an online match;
    /// - a synchronous match driver, via <see cref="Advance"/>, so a whole match can be played out in
    ///   a unit test without timers or coroutines.
    ///
    /// Phases advance only when <see cref="Advance"/> is called. In an online match the server's
    /// phase timer makes that call instead (CORE-2); here the caller decides, which is what keeps
    /// the rules layer free of any notion of real time.
    /// </summary>
    public sealed class LocalMatchSession : IGameActions, IMatchView
    {
        private readonly MatchState _state;
        private readonly IDiceRoller _roller;

        public LocalMatchSession(MatchState state, IDiceRoller roller, PlayerId? viewAs = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _roller = roller ?? throw new ArgumentNullException(nameof(roller));

            ViewAs = viewAs ?? (_state.Players.Count > 0 ? _state.Players[0].Id : default);
            RaiseChanged();
        }

        /// <summary>Authoritative state. Exposed for hosts and tests; the UI must use <see cref="Current"/>.</summary>
        public MatchState State => _state;

        /// <summary>
        /// Which seat <see cref="Current"/> is filtered for. In hot-seat play the controller moves
        /// this as the device is handed around; online it is fixed to the local player.
        /// </summary>
        public PlayerId ViewAs { get; private set; }

        public PlayerId LocalPlayer => ViewAs;
        public MatchSnapshot Current { get; private set; }

        /// <summary>No clock offline: phases advance when <see cref="Advance"/> is called.</summary>
        public float SecondsLeft => -1f;

        /// <summary>The report from the most recent contention pass, or null if none has run.</summary>
        public ResolutionReport LastResolution { get; private set; }

        public event Action<MatchSnapshot> Changed;
        public event Action<MoveFailure> MoveRejected;

        public void SetViewAs(PlayerId player)
        {
            ViewAs = player;
            RaiseChanged();
        }

        // ------------------------------------------------------------------ IGameActions

        public void RequestShape(ShapeAction action) => Shape(ViewAs, action);

        public void RequestCommit(CardId cardId, IReadOnlyList<int> diceIndices) => Commit(ViewAs, cardId, diceIndices);

        public void RequestPass() => Pass(ViewAs);

        public void RequestDone() => Done(ViewAs);

        public void RequestWithdraw() => Withdraw(ViewAs);

        // ------------------------------------------------------------------ per-seat commands

        /// <summary>Acts as a specific seat. Hot-seat and tests use this; online clients cannot.</summary>
        public MoveResult Shape(PlayerId player, ShapeAction action) =>
            Resolve(RulesEngine.ApplyShape(_state, player, action, _roller));

        public MoveResult Commit(PlayerId player, CardId cardId, IReadOnlyList<int> diceIndices) =>
            Resolve(RulesEngine.Commit(_state, player, cardId, diceIndices));

        public MoveResult Pass(PlayerId player) =>
            Resolve(RulesEngine.Pass(_state, player));

        public MoveResult Done(PlayerId player) =>
            Resolve(RulesEngine.Done(_state, player));

        public MoveResult Withdraw(PlayerId player) =>
            Resolve(RulesEngine.Withdraw(_state, player));

        // ------------------------------------------------------------------ clock

        /// <summary>
        /// Moves the match forward exactly one phase, performing whatever automatic work that
        /// transition entails, and returns the phase now in effect.
        ///
        /// Undecided players are auto-passed when a decision window closes, mirroring what the
        /// server does on timer expiry (CORE-2).
        /// </summary>
        public RoundPhase Advance()
        {
            switch (_state.Phase)
            {
                case RoundPhase.Roll:
                    RulesEngine.BeginRound(_state, _roller);
                    break;

                case RoundPhase.Shape:
                    RulesEngine.BeginCommit(_state);
                    break;

                case RoundPhase.Commit:
                    RulesEngine.AutoPassUndecided(_state);
                    RulesEngine.BeginReveal(_state);
                    break;

                case RoundPhase.Reveal:
                    LastResolution = RulesEngine.ResolveReveal(_state);
                    break;

                case RoundPhase.Repick:
                    RulesEngine.AutoPassUndecided(_state);
                    LastResolution = RulesEngine.ResolveRepick(_state);
                    break;

                case RoundPhase.Upkeep:
                    RulesEngine.RunUpkeep(_state);
                    break;

                case RoundPhase.MatchOver:
                    return _state.Phase;
            }

            RaiseChanged();
            return _state.Phase;
        }

        /// <summary>
        /// Advances until the given phase is reached, or the match ends. Note that it returns
        /// immediately if the match is already in that phase — to reach the *next* occurrence,
        /// call <see cref="Advance"/> once first.
        /// </summary>
        public void AdvanceTo(RoundPhase phase)
        {
            int guard = 0;
            while (_state.Phase != phase && _state.Phase != RoundPhase.MatchOver)
            {
                Advance();
                if (++guard > 64) throw new InvalidOperationException("Phase machine failed to reach " + phase);
            }
        }

        /// <summary>Final standings once the match is over.</summary>
        public IReadOnlyList<FinalScore> FinalScores() => Scoring.FinalScores(_state);

        // ------------------------------------------------------------------ internals

        private MoveResult Resolve(MoveResult result)
        {
            if (result.Success) RaiseChanged();
            else MoveRejected?.Invoke(result.Failure);
            return result;
        }

        private void RaiseChanged()
        {
            Current = MatchSnapshot.For(_state, ViewAs);
            Changed?.Invoke(Current);
        }
    }
}
