using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The match board: your dice tray, the market, the standings rail, and the controls for
    /// shaping and deciding.
    ///
    /// Passive. It renders a <see cref="MatchSnapshot"/> and raises intent events; a presenter
    /// connects those to <see cref="IGameActions"/>. It knows nothing about networking or about
    /// hot-seat, so the same board serves an online match and a pass-the-device one.
    ///
    /// The one piece of state it does own is <see cref="SelectedDice"/>, because which dice you
    /// have highlighted is a property of the screen, not of the match.
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private TMP_Text roundLabel;
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private TMP_Text sparksLabel;
        [SerializeField] private TMP_Text allowanceLabel;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text railLabel;

        [Header("Dice tray")]
        [SerializeField] private Transform diceRoot;
        [SerializeField] private DieView diePrefab;

        [Header("Shape controls")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button nudgeUpButton;
        [SerializeField] private Button nudgeDownButton;
        [SerializeField] private Transform faceButtonsRoot;

        [Header("Decide controls")]
        [SerializeField] private Button passButton;
        [SerializeField] private Button withdrawButton;
        [SerializeField] private Button doneButton;

        [Header("Market")]
        [SerializeField] private Transform marketRoot;
        [SerializeField] private CardButtonView cardButtonPrefab;

        private readonly List<DieView> _dice = new List<DieView>();
        private readonly List<CardButtonView> _cards = new List<CardButtonView>();
        private readonly List<int> _selected = new List<int>();
        private readonly StringBuilder _sb = new StringBuilder();

        private bool _canAct;

        /// <summary>Dice the player has highlighted, ascending. This is what a claim offers.</summary>
        public IReadOnlyList<int> SelectedDice => _selected;

        /// <summary>Raised when the player highlights or clears a die, so the board can re-render.</summary>
        public event Action SelectionChanged;

        public event Action RerollSelected;
        public event Action<int> NudgeSelected;
        public event Action<int> SetSelected;
        public event Action<int> CardChosen;
        public event Action PassClicked;
        public event Action WithdrawClicked;
        public event Action DoneClicked;

        private void Awake()
        {
            Hook(rerollButton, () => RerollSelected?.Invoke());
            Hook(nudgeUpButton, () => NudgeSelected?.Invoke(1));
            Hook(nudgeDownButton, () => NudgeSelected?.Invoke(-1));
            Hook(passButton, () => PassClicked?.Invoke());
            Hook(withdrawButton, () => WithdrawClicked?.Invoke());
            Hook(doneButton, () => DoneClicked?.Invoke());

            // Face buttons are ordered children: the first sets 1, the sixth sets 6.
            if (faceButtonsRoot != null)
            {
                for (int i = 0; i < faceButtonsRoot.childCount; i++)
                {
                    int face = i + 1;
                    var button = faceButtonsRoot.GetChild(i).GetComponent<Button>();
                    Hook(button, () => SetSelected?.Invoke(face));
                }
            }
        }

        private static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        public void ShowMessage(string message)
        {
            if (messageLabel != null) messageLabel.text = message ?? string.Empty;
        }

        public void ClearSelection()
        {
            _selected.Clear();
        }

        /// <summary>
        /// Renders the board. <paramref name="canAct"/> is false whenever the device is not in this
        /// player's hands — during a handoff, a reveal, or another seat's turn — and disables every
        /// control rather than relying on the engine to reject stray taps.
        /// </summary>
        public void Render(in MatchSnapshot snapshot, bool canAct, bool shapingAllowed)
        {
            _canAct = canAct;

            var me = snapshot.Observer;

            if (roundLabel != null)
                roundLabel.text = snapshot.IsMatchOver
                    ? "Match over"
                    : $"Round {snapshot.Round} / {snapshot.TotalRounds}";

            if (phaseLabel != null) phaseLabel.text = DescribePhase(snapshot);
            if (sparksLabel != null) sparksLabel.text = $"Sparks {me.Sparks}";
            if (allowanceLabel != null) allowanceLabel.text = DescribeAllowance(me);
            if (railLabel != null) railLabel.text = DescribeRail(snapshot);

            RenderDice(me, canAct && shapingAllowed);
            RenderMarket(snapshot, canAct);
            RenderControls(me, canAct, shapingAllowed);
        }

        // ------------------------------------------------------------------ sections

        private void RenderDice(in PlayerSnapshot me, bool interactable)
        {
            if (diceRoot == null || diePrefab == null) return;

            var faces = me.DiceFaces ?? Array.Empty<int>();
            var spent = me.DiceSpent ?? Array.Empty<bool>();

            // Drop selections that no longer point at a live, unspent die — the pool resizes
            // between rounds and dice get spent underneath us.
            _selected.RemoveAll(i => i >= faces.Length || (i < spent.Length && spent[i]));

            while (_dice.Count < faces.Length)
            {
                var die = Instantiate(diePrefab, diceRoot);
                die.Clicked += OnDieClicked;
                _dice.Add(die);
            }

            for (int i = 0; i < _dice.Count; i++)
            {
                bool active = i < faces.Length;
                _dice[i].gameObject.SetActive(active);
                if (!active) continue;

                _dice[i].Set(i, faces[i], i < spent.Length && spent[i], _selected.Contains(i), interactable);
            }
        }

        private void RenderMarket(in MatchSnapshot snapshot, bool interactable)
        {
            if (marketRoot == null || cardButtonPrefab == null) return;

            var market = snapshot.Market ?? Array.Empty<CardSnapshot>();

            while (_cards.Count < market.Length)
            {
                var card = Instantiate(cardButtonPrefab, marketRoot);
                card.Clicked += OnCardClicked;
                _cards.Add(card);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                bool active = i < market.Length;
                _cards[i].gameObject.SetActive(active);
                if (active) _cards[i].Set(market[i], interactable);
            }
        }

        private void RenderControls(in PlayerSnapshot me, bool canAct, bool shapingAllowed)
        {
            bool hasSelection = _selected.Count > 0;
            bool committed = me.HasCommitted;
            bool decided = me.HasDecided;

            SetInteractable(rerollButton, canAct && shapingAllowed && hasSelection);
            SetInteractable(nudgeUpButton, canAct && shapingAllowed && hasSelection);
            SetInteractable(nudgeDownButton, canAct && shapingAllowed && hasSelection);

            if (faceButtonsRoot != null)
                faceButtonsRoot.gameObject.SetActive(canAct && shapingAllowed && hasSelection);

            SetInteractable(passButton, canAct && !decided);
            SetInteractable(withdrawButton, canAct && decided);
            SetInteractable(doneButton, canAct);

            // Once committed the dice are pledged, so shaping is off until the player withdraws.
            if (committed && shapingAllowed) ShowMessage("Committed — withdraw to change your dice.");
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        // ------------------------------------------------------------------ text

        private static string DescribePhase(in MatchSnapshot s)
        {
            switch (s.Phase)
            {
                case RoundPhase.Shape: return "Shape your dice, then claim";
                case RoundPhase.Commit: return "Locking in";
                case RoundPhase.Reveal: return "Reveal";
                case RoundPhase.Repick: return "Re-pick";
                case RoundPhase.Upkeep: return "Upkeep";
                case RoundPhase.MatchOver: return "Match over";
                default: return s.Phase.ToString();
            }
        }

        private static string DescribeAllowance(in PlayerSnapshot me)
        {
            var parts = new List<string>(3);
            if (me.RerollsLeft > 0) parts.Add($"{me.RerollsLeft} re-roll");
            if (me.NudgesLeft > 0) parts.Add($"{me.NudgesLeft} nudge");
            if (me.SetsLeft > 0) parts.Add($"{me.SetsLeft} set");
            return parts.Count == 0 ? "No free actions" : "Free: " + string.Join(", ", parts);
        }

        private string DescribeRail(in MatchSnapshot s)
        {
            if (s.Players == null) return string.Empty;

            _sb.Clear();
            foreach (var p in s.Players)
            {
                if (_sb.Length > 0) _sb.Append('\n');

                _sb.Append(p.PriorityRank == 0 ? "▶ " : "  ");
                _sb.Append(p.DisplayName);
                _sb.Append("  ").Append(p.Score).Append("vp");
                _sb.Append("  ").Append(p.CardCount).Append(p.CardCount == 1 ? " card" : " cards");
                _sb.Append("  ").Append(p.DiceFaces?.Length ?? 0).Append(" dice");
                _sb.Append("  ").Append(p.Sparks).Append("sp");
                if (p.HasDecided) _sb.Append("   ✓");
            }
            return _sb.ToString();
        }

        // ------------------------------------------------------------------ input

        private void OnDieClicked(int index)
        {
            if (!_canAct) return;

            if (!_selected.Remove(index)) _selected.Add(index);
            _selected.Sort();

            SelectionChanged?.Invoke();
        }

        private void OnCardClicked(int cardId)
        {
            if (!_canAct) return;
            CardChosen?.Invoke(cardId);
        }
    }
}
