using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
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
        [SerializeField] private CardZoomSheetView zoomSheet;
        [SerializeField] private HintToastView hintToast;

        [Tooltip("Rebuilds a card's structured cost client-side for suggestion and validation (UI-3).")]
        [SerializeField] private CardDatabase cardDatabase;

        private IGameActions _actions;
        private IMatchView _match;

        private bool _canAct;
        private bool _shapingAllowed;

        // Zoom-sheet state: which cost is open, so selection changes re-validate against it.
        private ICardRequirement _zoomCost;

        // Hints: seen-flags injected by the app layer (the profile lives behind an asmdef wall).
        private bool _shapeHintSeen = true;
        private bool _commitHintSeen = true;
        private RoundPhase _activeHint = RoundPhase.MatchOver;
        private RoundPhase _lastHintPhase = (RoundPhase)(-1);

        /// <summary>Raised when the player says they are finished — hot-seat uses it to move on.</summary>
        public event Action DoneRequested;

        /// <summary>Raised when a first-time hint is dismissed, so the app layer can persist it.</summary>
        public event Action<RoundPhase> HintDismissed;

        private void Awake()
        {
            if (zoomSheet != null)
            {
                zoomSheet.CommitConfirmed += OnZoomCommit;
                zoomSheet.Dismissed += CloseZoom;
            }

            if (hintToast != null) hintToast.Dismissed += OnHintDismissed;
        }

        /// <summary>Which hints this player has already seen; set before the first snapshot.</summary>
        public void SetHintFlags(bool shapeSeen, bool commitSeen)
        {
            _shapeHintSeen = shapeSeen;
            _commitHintSeen = commitSeen;
        }

        public void Bind(IGameActions actions, IMatchView match)
        {
            Unbind();

            _actions = actions;
            _match = match;

            if (view != null)
            {
                view.SelectionChanged += OnSelectionChanged;
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
                view.SelectionChanged -= OnSelectionChanged;
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
            // A new actor (hot-seat handoff) must inherit neither the tray nor an open sheet.
            CloseZoomSilently();
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
        /// per frame rather than only when a snapshot arrives. This only touches the timer label
        /// (STORY-2.8) — the rest of the board re-renders on <see cref="OnMatchChanged"/>, not here.
        /// Offline SecondsLeft is negative and this does nothing visible.
        /// </summary>
        private void Update()
        {
            if (view == null || _match == null) return;
            float secondsLeft = _match.SecondsLeft;
            if (secondsLeft >= 0f) view.Tick(secondsLeft, _match.Current.IsMatchOver);
        }

        private void OnMatchChanged(MatchSnapshot snapshot)
        {
            // The sheet dies with its moment: decision made, phase moved on, or card gone.
            if (zoomSheet != null && zoomSheet.IsOpen)
            {
                bool inputPhase = snapshot.Phase == RoundPhase.Shape ||
                                  snapshot.Phase == RoundPhase.Commit ||
                                  snapshot.Phase == RoundPhase.Repick;
                if (!inputPhase || snapshot.Observer.HasDecided || FindCard(snapshot, zoomSheet.CardId) == null)
                    CloseZoomSilently();
            }

            MaybeShowHint(snapshot);
            Refresh();
        }

        private void MaybeShowHint(in MatchSnapshot snapshot)
        {
            if (hintToast == null || snapshot.Phase == _lastHintPhase) return;
            _lastHintPhase = snapshot.Phase;

            if (snapshot.Phase == RoundPhase.Shape && !_shapeHintSeen)
            {
                _shapeHintSeen = true;   // once per session even if never persisted
                _activeHint = RoundPhase.Shape;
                hintToast.Show("Shape phase — tap dice to select them, then re-roll, nudge or set " +
                               "faces to build a combination. You can commit to a card early.");
            }
            else if (snapshot.Phase == RoundPhase.Commit && !_commitHintSeen)
            {
                _commitHintSeen = true;
                _activeHint = RoundPhase.Commit;
                hintToast.Show("Commit — tap a market card, pick the dice that pay its cost, and " +
                               "lock in. Choices stay secret until the Reveal.");
            }
        }

        private void OnHintDismissed()
        {
            if (_activeHint == RoundPhase.MatchOver) return;
            HintDismissed?.Invoke(_activeHint);
            _activeHint = RoundPhase.MatchOver;
        }

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

        /// <summary>
        /// A card tap opens the inspect sheet with a suggested paying selection (UI-3) — the
        /// commit itself happens from the sheet, never from the bare tap.
        /// </summary>
        private void OnCardChosen(int cardId)
        {
            if (zoomSheet == null || cardDatabase == null || _match == null)
                return;

            var snapshot = _match.Current;
            var card = FindCard(snapshot, cardId);
            if (card == null) return;

            // Rebuild the structured cost locally; CostText is display-only.
            var definition = cardDatabase.Find(new CardId(cardId));
            _zoomCost = definition != null ? definition.ToCard().Cost : null;

            var me = snapshot.Observer;
            var suggestion = _zoomCost != null
                ? PaymentSuggester.Suggest(_zoomCost, me.DiceFaces, me.DiceSpent, WildSet(me), me.WildDice)
                : Array.Empty<int>();

            zoomSheet.Show(card.Value);
            view.SetCostFocus(true, suggestion);
            view.SetSelection(suggestion);   // raises SelectionChanged → pay-state + re-render
            UpdatePayState();
        }

        private void OnSelectionChanged()
        {
            if (zoomSheet != null && zoomSheet.IsOpen)
            {
                // Dimming follows the live selection while the sheet is open.
                view.SetCostFocus(true, view.SelectedDice);
                UpdatePayState();
            }

            Refresh();
        }

        private void UpdatePayState()
        {
            if (zoomSheet == null || !zoomSheet.IsOpen || _match == null) return;

            bool pays = false;
            if (_zoomCost != null)
            {
                var me = _match.Current.Observer;
                var faces = new List<int>();
                var selected = view.SelectedDice;
                for (int i = 0; i < selected.Count; i++)
                {
                    int die = selected[i];
                    if (me.DiceFaces != null && die < me.DiceFaces.Length) faces.Add(me.DiceFaces[die]);
                }

                pays = faces.Count > 0 && CostChecker.Satisfies(_zoomCost, faces, WildSet(me), me.WildDice);
            }

            zoomSheet.SetPayState(pays);
        }

        private void OnZoomCommit()
        {
            if (zoomSheet == null || !zoomSheet.IsOpen) return;

            int cardId = zoomSheet.CardId;
            var payment = SelectionCopy();
            CloseZoomSilently();

            if (payment.Length == 0)
            {
                ShowMessage("Tap the dice you want to pay with first.");
                return;
            }

            _actions?.RequestCommit(new CardId(cardId), payment);
        }

        private void CloseZoom()
        {
            CloseZoomSilently();
            Refresh();
        }

        private void CloseZoomSilently()
        {
            _zoomCost = null;
            if (zoomSheet != null) zoomSheet.Hide();
            if (view != null) view.SetCostFocus(false);
        }

        private static CardSnapshot? FindCard(in MatchSnapshot snapshot, int cardId)
        {
            var market = snapshot.Market;
            if (market == null) return null;
            for (int i = 0; i < market.Length; i++)
                if (market[i].CardId == cardId) return market[i];
            return null;
        }

        private static HashSet<int> WildSet(in PlayerSnapshot me)
        {
            var set = new HashSet<int>();
            if (me.WildFaces != null)
                foreach (int face in me.WildFaces) set.Add(face);
            return set;
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
