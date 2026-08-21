using System;
using System.Collections.Generic;
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
        [SerializeField] private TMP_Text timerLabel;

        [Header("Standings rail")]
        [SerializeField] private Transform railRoot;
        [SerializeField] private PlayerRowView playerRowPrefab;

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
        private readonly List<PlayerRowView> _rows = new List<PlayerRowView>();
        private readonly List<int> _order = new List<int>();
        private readonly List<int> _selected = new List<int>();

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

        /// <summary>Renders with no phase clock — the hot-seat case.</summary>
        public void Render(in MatchSnapshot snapshot, bool canAct, bool shapingAllowed) =>
            Render(snapshot, canAct, shapingAllowed, -1f);

        /// <summary>
        /// Renders the board. <paramref name="canAct"/> is false whenever the device is not in this
        /// player's hands, and disables every control rather than relying on the engine to reject
        /// stray taps. <paramref name="secondsLeft"/> is negative when there is no clock.
        /// </summary>
        public void Render(in MatchSnapshot snapshot, bool canAct, bool shapingAllowed, float secondsLeft)
        {
            _canAct = canAct;

            if (timerLabel != null)
            {
                bool ticking = secondsLeft >= 0f && !snapshot.IsMatchOver;
                timerLabel.gameObject.SetActive(ticking);
                if (ticking) timerLabel.text = Mathf.CeilToInt(secondsLeft).ToString();
            }

            var me = snapshot.Observer;

            if (roundLabel != null)
                roundLabel.text = snapshot.IsMatchOver
                    ? "Match over"
                    : $"Round {snapshot.Round} / {snapshot.TotalRounds}";

            if (phaseLabel != null) phaseLabel.text = DescribePhase(snapshot);
            if (sparksLabel != null) sparksLabel.text = $"Sparks {me.Sparks}";
            if (allowanceLabel != null) allowanceLabel.text = DescribeAllowance(me);

            RenderRail(snapshot);
            // Dice selection tracks "can I act" (Shape, Commit, or Repick — CORE-5, MKT-3), not
            // "can I shape": shapingAllowed only gates the free reroll/nudge/set powers below,
            // which are Shape-only. Tying it to dice interactivity here disabled the whole tray
            // during a re-pick pass, since HotSeatHost forces shapingAllowed false there.
            RenderDice(me, canAct);
            RenderMarket(snapshot, canAct);
            RenderControls(me, canAct, shapingAllowed);
        }

        // ------------------------------------------------------------------ sections

        /// <summary>
        /// One row per player, ordered by priority so the player about to win a contested card is
        /// always at the top. Rows are pooled rather than rebuilt, since this redraws every frame
        /// while a phase clock is running.
        /// </summary>
        private void RenderRail(in MatchSnapshot snapshot)
        {
            if (railRoot == null || playerRowPrefab == null) return;

            var players = snapshot.Players ?? Array.Empty<PlayerSnapshot>();

            while (_rows.Count < players.Length)
                _rows.Add(Instantiate(playerRowPrefab, railRoot));

            // Sorting a copy of the indices keeps the snapshot itself untouched.
            _order.Clear();
            for (int i = 0; i < players.Length; i++) _order.Add(i);
            _order.Sort((a, b) => players[a].PriorityRank.CompareTo(players[b].PriorityRank));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < players.Length;
                _rows[i].gameObject.SetActive(active);
                if (!active) continue;

                var player = players[_order[i]];
                _rows[i].Set(player, player.PlayerId == snapshot.ObserverId);
            }
        }

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
