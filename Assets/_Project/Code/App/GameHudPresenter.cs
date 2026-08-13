using System;
using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Binds <see cref="GameHudView"/> to an <see cref="IGameActions"/>/<see cref="IMatchView"/>
    /// pair. Because both interfaces are implemented by the offline session and the networked
    /// controller alike, the same HUD serves hot-seat and online play unchanged.
    ///
    /// The presenter owns the translation from "dice I have highlighted" to a claim, and from a
    /// rejection code to something a person can read.
    /// </summary>
    public sealed class GameHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameHudView view;

        private IGameActions _actions;
        private IMatchView _match;

        private bool _canAct;
        private bool _shapingAllowed;

        /// <summary>Raised when the player says they are finished — hot-seat uses it to move on.</summary>
        public event Action DoneRequested;

        public void Bind(IGameActions actions, IMatchView match)
        {
            Unbind();

            _actions = actions;
            _match = match;

            if (view != null)
            {
                view.SelectionChanged += Refresh;
                view.RerollSelected += OnReroll;
                view.NudgeSelected += OnNudge;
                view.SetSelected += OnSetFace;
                view.CardChosen += OnCardChosen;
                view.PassClicked += OnPass;
                view.WithdrawClicked += OnWithdraw;
                view.DoneClicked += OnDone;
            }

            if (_match != null)
            {
                _match.Changed += OnMatchChanged;
                _match.MoveRejected += OnMoveRejected;
            }

            Refresh();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (view != null)
            {
                view.SelectionChanged -= Refresh;
                view.RerollSelected -= OnReroll;
                view.NudgeSelected -= OnNudge;
                view.SetSelected -= OnSetFace;
                view.CardChosen -= OnCardChosen;
                view.PassClicked -= OnPass;
                view.WithdrawClicked -= OnWithdraw;
                view.DoneClicked -= OnDone;
            }

            if (_match != null)
            {
                _match.Changed -= OnMatchChanged;
                _match.MoveRejected -= OnMoveRejected;
            }
        }

        /// <summary>
        /// Sets whether the board should accept input at all. Hot-seat drives this from the
        /// director's stage, so the board is inert during a handoff or a reveal.
        /// </summary>
        public void SetContext(bool canAct, bool shapingAllowed)
        {
            _canAct = canAct;
            _shapingAllowed = shapingAllowed;
            Refresh();
        }

        public void ClearSelection()
        {
            if (view != null) view.ClearSelection();
            Refresh();
        }

        public void ShowMessage(string message)
        {
            if (view != null) view.ShowMessage(message);
        }

        private void Refresh()
        {
            if (view == null || _match == null) return;
            view.Render(_match.Current, _canAct, _shapingAllowed, _match.SecondsLeft);
        }

        /// <summary>
        /// Online the phase clock ticks between server messages, so the countdown has to be redrawn
        /// per frame rather than only when a snapshot arrives. Offline SecondsLeft is negative and
        /// this does nothing visible.
        /// </summary>
        private void Update()
        {
            if (_match != null && _match.SecondsLeft >= 0f) Refresh();
        }

        private void OnMatchChanged(MatchSnapshot snapshot) => Refresh();

        // ------------------------------------------------------------------ intents

        /// <summary>
        /// Snapshots the selection before acting. Each shape action raises Changed, which re-renders
        /// and can prune the live selection list — iterating it directly would mutate under us.
        /// </summary>
        private int[] SelectionCopy()
        {
            var selected = view != null ? view.SelectedDice : null;
            if (selected == null || selected.Count == 0) return Array.Empty<int>();

            var copy = new int[selected.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = selected[i];
            return copy;
        }

        private void OnReroll()
        {
            foreach (int die in SelectionCopy())
                _actions?.RequestShape(ShapeAction.Reroll(die));
        }

        private void OnNudge(int delta)
        {
            foreach (int die in SelectionCopy())
                _actions?.RequestShape(ShapeAction.Nudge(die, delta));
        }

        private void OnSetFace(int face)
        {
            foreach (int die in SelectionCopy())
                _actions?.RequestShape(ShapeAction.SetFace(die, face));
        }

        private void OnCardChosen(int cardId)
        {
            var payment = SelectionCopy();
            if (payment.Length == 0)
            {
                ShowMessage("Tap the dice you want to pay with first.");
                return;
            }

            _actions?.RequestCommit(new CardId(cardId), payment);
        }

        private void OnPass() => _actions?.RequestPass();

        private void OnWithdraw()
        {
            _actions?.RequestWithdraw();
            ClearSelection();
        }

        private void OnDone() => DoneRequested?.Invoke();

        private void OnMoveRejected(MoveFailure failure) => ShowMessage(Explain(failure));

        private static string Explain(MoveFailure failure)
        {
            switch (failure)
            {
                case MoveFailure.CostNotMet: return "Those dice don't pay for that card.";
                case MoveFailure.NoDiceOffered: return "Tap the dice you want to pay with first.";
                case MoveFailure.CannotAfford: return "Not enough Sparks or free actions.";
                case MoveFailure.AlreadyCommitted: return "Withdraw first to change your mind.";
                case MoveFailure.CardNotInMarket: return "That card has gone.";
                case MoveFailure.DieAlreadySpent: return "That die is already spent.";
                case MoveFailure.DuplicateDie: return "The same die can only be offered once.";
                case MoveFailure.NudgeOutOfRange: return "A nudge moves a die one step, within 1 to 6.";
                case MoveFailure.InvalidFace: return "Pick a face from 1 to 6.";
                case MoveFailure.NotAContender: return "Only players who lost a card re-pick.";
                case MoveFailure.WrongPhase: return "Not right now.";
                case MoveFailure.MatchOver: return "The match is over.";
                default: return string.Empty;
            }
        }
    }
}
