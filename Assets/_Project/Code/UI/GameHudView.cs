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
        [SerializeField] private TMP_Text messageLabel;

        [Header("Standings rail")]
        [SerializeField] private Transform railRoot;
        [SerializeField] private PlayerRowView playerRowPrefab;

        [Header("Dice tray")]
        [SerializeField] private Transform diceRoot;
        [SerializeField] private DieView diePrefab;
        [SerializeField] private TMP_Text trayHintLabel;

        [Header("Owned powers strip")]
        [SerializeField] private Transform powersRoot;
        [SerializeField] private RectTransform powerChipTemplate;

        [Header("Shape controls")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button nudgeUpButton;
        [SerializeField] private Button nudgeDownButton;
        [SerializeField] private Button setFaceButton;
        [SerializeField] private Transform faceButtonsRoot;
        [SerializeField] private Button faceCancelButton;

        [Header("Decide controls")]
        [SerializeField] private Button passButton;
        [SerializeField] private Button withdrawButton;
        [SerializeField] private DoneTimerButtonView doneTimer;

        [Header("Motion")]
        [SerializeField] private UiAnimationService anims;

        [Header("Market")]
        [SerializeField] private Transform marketRoot;
        [SerializeField] private CardButtonView cardButtonPrefab;
        [SerializeField] private TMP_Text marketMetaLabel;

        private readonly List<DieView> _dice = new List<DieView>();
        private readonly List<CardButtonView> _cards = new List<CardButtonView>();
        private readonly List<PlayerRowView> _rows = new List<PlayerRowView>();
        private readonly List<int> _order = new List<int>();
        private readonly List<int> _selected = new List<int>();

        // While a card's cost is being inspected, dice outside the paying set render Dimmed.
        private readonly HashSet<int> _costFocus = new HashSet<int>();
        private bool _costFocusActive;

        private readonly List<string> _powerTexts = new List<string>();
        private readonly List<RectTransform> _powerChips = new List<RectTransform>();
        private readonly List<TMP_Text> _powerChipLabels = new List<TMP_Text>();

        private bool _canAct;
        private bool _facePickerOpen;
        private float _phaseDuration = -1f;
        private RoundPhase _lastPhase = (RoundPhase)(-1);
        private AnimHandle _rollAnim;
        private TMP_Text _rerollLabel;
        private TMP_Text _setFaceLabel;

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
            // Each shape action gets its own beat on the selected dice (STORY-3.2 AC2): re-roll
            // spins, nudge ratchets, set-face stamps. Presentation only — the engine's answer
            // arrives as the next snapshot regardless.
            Hook(rerollButton, () => { PlayOnSelected(d => d.PlaySpin(anims)); RerollSelected?.Invoke(); });
            Hook(nudgeUpButton, () => { PlayOnSelected(d => d.PlayTick(anims, 1)); NudgeSelected?.Invoke(1); });
            Hook(nudgeDownButton, () => { PlayOnSelected(d => d.PlayTick(anims, -1)); NudgeSelected?.Invoke(-1); });
            Hook(passButton, () => PassClicked?.Invoke());
            Hook(withdrawButton, () => WithdrawClicked?.Invoke());
            if (doneTimer != null) doneTimer.Clicked += () => DoneClicked?.Invoke();

            // The picker replaces the shape row; SelectionChanged doubles as "please re-render".
            Hook(setFaceButton, () =>
            {
                _facePickerOpen = true;
                SelectionChanged?.Invoke();
            });
            Hook(faceCancelButton, () =>
            {
                _facePickerOpen = false;
                SelectionChanged?.Invoke();
            });

            // Face buttons are ordered children: the first sets 1, the sixth sets 6.
            if (faceButtonsRoot != null)
            {
                for (int i = 0; i < faceButtonsRoot.childCount; i++)
                {
                    int face = i + 1;
                    var button = faceButtonsRoot.GetChild(i).GetComponent<Button>();
                    Hook(button, () =>
                    {
                        _facePickerOpen = false;
                        PlayOnSelected(d => d.PlayStamp(anims));
                        SetSelected?.Invoke(face);
                    });
                }
            }

            if (rerollButton != null) _rerollLabel = rerollButton.GetComponentInChildren<TMP_Text>(true);
            if (setFaceButton != null) _setFaceLabel = setFaceButton.GetComponentInChildren<TMP_Text>(true);
        }

        private static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void PlayOnSelected(Action<DieView> beat)
        {
            for (int i = 0; i < _selected.Count; i++)
                if (_selected[i] < _dice.Count) beat(_dice[_selected[i]]);
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
        /// Replaces the selection outright — the card sheet's auto-suggest uses this. Raises
        /// <see cref="SelectionChanged"/> so the presenter re-renders, same as a tap would.
        /// </summary>
        public void SetSelection(IReadOnlyList<int> indices)
        {
            _selected.Clear();
            if (indices != null)
                for (int i = 0; i < indices.Count; i++) _selected.Add(indices[i]);
            _selected.Sort();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Dims every die outside <paramref name="focus"/> while a card's cost is open (UI-3).
        /// Pass inactive to restore the tray. Takes effect on the next render.
        /// </summary>
        public void SetCostFocus(bool active, IReadOnlyList<int> focus = null)
        {
            _costFocusActive = active;
            _costFocus.Clear();
            if (active && focus != null)
                for (int i = 0; i < focus.Count; i++) _costFocus.Add(focus[i]);
        }

        /// <summary>
        /// Updates only the clock — the Done button's perimeter ring and seconds readout. Online
        /// the clock ticks between server snapshots, so this runs every frame; the rest of the
        /// board only changes on a new snapshot and should not be rebuilt that often (STORY-2.8).
        /// The duration denominator is cached by the last Render from the config echo.
        /// </summary>
        public void Tick(float secondsLeft, bool matchOver)
        {
            if (doneTimer == null) return;
            doneTimer.Tick(matchOver ? -1f : secondsLeft, _phaseDuration);
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

            var me = snapshot.Observer;

            if (roundLabel != null)
                roundLabel.text = snapshot.IsMatchOver
                    ? "MATCH OVER"
                    : $"ROUND {snapshot.Round:00}/{snapshot.TotalRounds}";

            if (phaseLabel != null) phaseLabel.text = DescribePhase(snapshot);

            // Non-breaking space so the tag never collapses between label and value (handoff 6a).
            if (sparksLabel != null)
            {
                int cap = snapshot.Config?.SparkCap ?? 0;
                sparksLabel.text = cap > 0 ? $"SPARKS {me.Sparks}/{cap}" : $"SPARKS {me.Sparks}";
            }

            if (marketMetaLabel != null)
                marketMetaLabel.text = $"DECK {snapshot.DrawPileCount} · TAP A CARD TO INSPECT";

            _phaseDuration = snapshot.Config?.DurationOf(snapshot.Phase, snapshot.Reveals?.Length ?? 0) ?? -1f;

            if (trayHintLabel != null)
            {
                string hint = snapshot.Phase == RoundPhase.Roll ? "SERVER ROLLING"
                    : _costFocusActive ? "HIGHLIGHTED DICE PAY THE COST"
                    : _selected.Count > 0 ? $"{_selected.Count} SELECTED"
                    : "TAP TO SELECT";
                trayHintLabel.text = "YOUR DICE — " + hint;
            }

            // Remember the latest reveal outcomes: they name the winners the claim flights
            // deliver to once the cards leave the market (P2).
            if (snapshot.Reveals != null && snapshot.Reveals.Length > 0)
                _lastReveals = snapshot.Reveals;

            RenderRail(snapshot);
            // Dice selection tracks "can I act" (Shape, Commit, or Repick — CORE-5, MKT-3), not
            // "can I shape": shapingAllowed only gates the free reroll/nudge/set powers below,
            // which are Shape-only. Tying it to dice interactivity here disabled the whole tray
            // during a re-pick pass, since HotSeatHost forces shapingAllowed false there.
            RenderDice(me, canAct);
            RenderMarket(snapshot, canAct);
            RenderPowers(me, snapshot.Phase);
            RenderControls(me, snapshot, canAct, shapingAllowed);
            RenderDoneButton(me, snapshot, canAct);
            Tick(secondsLeft, snapshot.IsMatchOver);

            // The server roll is authoritative; while it lands, the tray plays pure animation —
            // and when it lands, the dice settle left to right instead of teleporting (AC1).
            if (anims != null && snapshot.Phase != _lastPhase)
            {
                bool wasRolling = _lastPhase == RoundPhase.Roll;
                if (snapshot.Phase == RoundPhase.Roll) StartRollAnimation();
                else
                {
                    StopRollAnimation();
                    if (wasRolling)
                        for (int i = 0; i < _dice.Count; i++)
                            if (_dice[i].gameObject.activeSelf)
                                _dice[i].PlaySettle(anims, i * 0.05f);
                }
            }

            // The observer gaining Sparks gets a chip pop — the payout beat (P2; the gauge with
            // its needle jiggle arrives in P5).
            if (anims != null && sparksLabel != null && me.Sparks > _lastSparks && _lastSparks >= 0)
            {
                var chipTransform = sparksLabel.transform;
                anims.Play(0.3f, UiEase.OutBack, t =>
                    chipTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.12f, 1f, t));
            }
            _lastSparks = me.Sparks;

            _lastPhase = snapshot.Phase;
        }

        private int _lastSparks = -1;

        // ------------------------------------------------------------------ sections

        /// <summary>
        /// One cell per player in seat order — stable positions, so a cell never jumps mid-round;
        /// priority is the marker on a cell, not the ordering (handoff 6b). Cells are pooled.
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
            _order.Sort((a, b) => players[a].SeatIndex.CompareTo(players[b].SeatIndex));

            for (int i = 0; i < _rows.Count; i++)
            {
                bool active = i < players.Length;
                _rows[i].gameObject.SetActive(active);
                if (!active) continue;

                var player = players[_order[i]];
                _rows[i].Set(player, player.PlayerId == snapshot.ObserverId, snapshot.Phase);
            }
        }

        private void RenderDice(in PlayerSnapshot me, bool interactable)
        {
            if (diceRoot == null || diePrefab == null) return;

            var faces = me.DiceFaces ?? Array.Empty<int>();
            var spent = me.DiceSpent ?? Array.Empty<bool>();

            FitDiceToOneRow(faces.Length);

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

                var state = i < spent.Length && spent[i] ? DieVisualState.Spent
                    : _selected.Contains(i) ? DieVisualState.Selected
                    : _costFocusActive && !_costFocus.Contains(i) ? DieVisualState.Dimmed
                    : DieVisualState.Idle;

                _dice[i].Set(i, faces[i], state, interactable);
                _dice[i].SetContentScale(_dieContentScale);
            }
        }

        /// <summary>
        /// Shrinks the tray's grid cells so every die fits ONE row. The tray band was sized for
        /// the 4-die start; a grown engine (capacity max 8) used to wrap onto the shape-action
        /// buttons below it (#68). Cells scale down from the authored size only when needed, and
        /// the authored size is recovered from the grid itself so this stays in sync with the
        /// scaffolder rather than duplicating the design token.
        /// </summary>
        private void FitDiceToOneRow(int diceCount)
        {
            var grid = diceRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (grid == null || diceCount <= 0) return;

            if (_authoredCell <= 0f) _authoredCell = grid.cellSize.x;

            float width = ((RectTransform)diceRoot).rect.width;
            float available = width - grid.spacing.x * (diceCount - 1);
            float cell = Mathf.Min(_authoredCell, Mathf.Floor(available / diceCount));
            grid.cellSize = new Vector2(cell, cell);
            _dieContentScale = cell / _authoredCell;
        }

        private float _authoredCell;
        private float _dieContentScale = 1f;

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

            // Claimed cards fly to their winner before the belt moves (P2, STORY-3.2 AC3);
            // restocks slide the whole belt in like a conveyor.
            bool restocked = false;
            if (anims != null && _lastMarketIds.Count > 0)
            {
                foreach (int id in _lastMarketIds)
                    if (!MarketContains(market, id))
                        TryFlyClaim(id, snapshot);
                for (int i = 0; i < market.Length; i++)
                    if (!_lastMarketIds.Contains(market[i].CardId)) restocked = true;
            }

            _lastMarketIds.Clear();
            for (int i = 0; i < market.Length; i++) _lastMarketIds.Add(market[i].CardId);

            if (restocked)
            {
                var belt = (RectTransform)marketRoot;
                anims.Skip(_beltTween);
                _beltTween = anims.Play(0.4f, UiEase.OutCubic, t =>
                    belt.anchoredPosition = new Vector2(202f * (1f - t), belt.anchoredPosition.y));
            }

            // The stamp is a local echo of the observer's own secret pick — never anyone else's.
            int myPendingCard = snapshot.Observer.PendingCardId;

            for (int i = 0; i < _cards.Count; i++)
            {
                bool active = i < market.Length;
                _cards[i].gameObject.SetActive(active);
                if (!active) continue;

                _cards[i].Set(market[i], interactable);
                _cards[i].SetCommitted(market[i].CardId == myPendingCard);
                _marketCellPos[market[i].CardId] = _cards[i].transform.position;
            }
        }

        private readonly HashSet<int> _lastMarketIds = new HashSet<int>();
        private readonly Dictionary<int, Vector3> _marketCellPos = new Dictionary<int, Vector3>();
        private RevealSnapshot[] _lastReveals;
        private AnimHandle _beltTween;

        private static bool MarketContains(CardSnapshot[] market, int cardId)
        {
            for (int i = 0; i < market.Length; i++)
                if (market[i].CardId == cardId) return true;
            return false;
        }

        /// <summary>
        /// A card that just left the market flies from its old cell to its winner's rail cell —
        /// a clone of the real card template, shrinking as it goes. Needs a reveal that names a
        /// winner; anything else (deck churn, no animation service) just skips.
        /// </summary>
        private void TryFlyClaim(int cardId, in MatchSnapshot snapshot)
        {
            if (_lastReveals == null || !_marketCellPos.TryGetValue(cardId, out var fromPos)) return;

            int winnerId = -1;
            RevealSnapshot reveal = default;
            for (int i = 0; i < _lastReveals.Length; i++)
                if (_lastReveals[i].CardId == cardId && _lastReveals[i].WinnerId >= 0)
                {
                    winnerId = _lastReveals[i].WinnerId;
                    reveal = _lastReveals[i];
                    break;
                }
            if (winnerId < 0) return;

            Transform target = null;
            var players = snapshot.Players ?? Array.Empty<PlayerSnapshot>();
            for (int i = 0; i < players.Length && i < _rows.Count; i++)
                if (players[i].PlayerId == winnerId) { target = _rows[i].transform; break; }
            if (target == null) return;

            var fly = Instantiate(cardButtonPrefab, marketRoot.parent);
            fly.transform.SetAsLastSibling();
            fly.gameObject.SetActive(true);
            fly.Set(new CardSnapshot
            {
                CardId = reveal.CardId,
                DisplayName = reveal.DisplayName,
                Tier = reveal.Tier,
                Points = reveal.Points,
                PowerText = reveal.PowerText,
                Family = reveal.Family,
                CostText = string.Empty,
                AffordableNow = true
            }, false);

            var group = fly.gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = fly.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            var flyTransform = fly.transform;
            flyTransform.position = fromPos;
            Vector3 toPos = target.position;
            var flyGo = fly.gameObject;

            anims.Play(0.45f, UiEase.OutCubic, t =>
            {
                if (flyGo == null) return;
                flyTransform.position = Vector3.LerpUnclamped(fromPos, toPos, t);
                flyTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);
            }, () => { if (flyGo != null) Destroy(flyGo); });
        }

        /// <summary>Only-when-usable chips for the observer's powers (handoff 6d, UI-5).</summary>
        private void RenderPowers(in PlayerSnapshot me, RoundPhase phase)
        {
            if (powersRoot == null || powerChipTemplate == null) return;

            _powerTexts.Clear();
            bool inputPhase = phase == RoundPhase.Shape || phase == RoundPhase.Commit;
            if (inputPhase)
            {
                if (me.RerollsLeft > 0) _powerTexts.Add($"FREE RE-ROLL ×{me.RerollsLeft}");
                if (me.NudgesLeft > 0) _powerTexts.Add($"±1 NUDGE ×{me.NudgesLeft}");
                if (me.SetsLeft > 0) _powerTexts.Add($"SET FACE FREE ×{me.SetsLeft}");
                if (me.WildFaces != null)
                    foreach (int face in me.WildFaces) _powerTexts.Add($"{face}s ARE WILD");
                if (me.WildDice > 0) _powerTexts.Add($"WILD DIE ×{me.WildDice}");
            }

            // Nothing usable → the whole row collapses; no empty band is reserved.
            powersRoot.gameObject.SetActive(_powerTexts.Count > 0);
            if (_powerTexts.Count == 0) return;

            while (_powerChips.Count < _powerTexts.Count)
            {
                var chip = Instantiate(powerChipTemplate, powersRoot);
                _powerChips.Add(chip);
                _powerChipLabels.Add(chip.GetComponentInChildren<TMP_Text>(true));
            }

            for (int i = 0; i < _powerChips.Count; i++)
            {
                bool active = i < _powerTexts.Count;
                _powerChips[i].gameObject.SetActive(active);
                if (active && _powerChipLabels[i] != null) _powerChipLabels[i].text = _powerTexts[i];
            }
        }

        private void RenderControls(in PlayerSnapshot me, in MatchSnapshot snapshot, bool canAct, bool shapingAllowed)
        {
            bool hasSelection = _selected.Count > 0;
            bool oneSelected = _selected.Count == 1;
            bool committed = me.HasCommitted;
            bool decided = me.HasDecided;

            bool shaping = canAct && shapingAllowed && !decided;
            if (!shaping) _facePickerOpen = false;

            // The picker replaces the shape row rather than stacking under it (handoff 6f-alt).
            bool shapeRow = shaping && !_facePickerOpen;
            SetRowVisible(rerollButton, shapeRow);
            SetRowVisible(nudgeUpButton, shapeRow);
            SetRowVisible(nudgeDownButton, shapeRow);
            SetRowVisible(setFaceButton, shapeRow);
            if (faceButtonsRoot != null) faceButtonsRoot.gameObject.SetActive(shaping && _facePickerOpen);
            SetRowVisible(faceCancelButton, shaping && _facePickerOpen);

            int rerollCost = snapshot.Config?.RerollSparkCost ?? 2;
            int setFaceCost = snapshot.Config?.SetFaceSparkCost ?? 4;

            // Disabled functionally per legality, never just visually (handoff 6f). Nudges have
            // no Spark price — allowance only — and require exactly one die.
            SetInteractable(rerollButton, shaping && hasSelection && (me.RerollsLeft > 0 || me.Sparks >= rerollCost));
            SetInteractable(nudgeUpButton, shaping && oneSelected && me.NudgesLeft > 0);
            SetInteractable(nudgeDownButton, shaping && oneSelected && me.NudgesLeft > 0);
            SetInteractable(setFaceButton, shaping && hasSelection && (me.SetsLeft > 0 || me.Sparks >= setFaceCost));

            if (_rerollLabel != null)
                _rerollLabel.text = me.RerollsLeft > 0 ? $"RE-ROLL · {me.RerollsLeft} FREE" : $"RE-ROLL −{rerollCost}sp";
            if (_setFaceLabel != null)
                _setFaceLabel.text = me.SetsLeft > 0 ? "SET FACE · FREE" : $"SET FACE −{setFaceCost}sp";

            // Withdraw exists once committed (CORE-5) or done shaping — either lock is taken back
            // the same way; Pass only while undecided — all live in Shape and Commit alike, since
            // a player may decide early. DoneShaping is already phase-gated by the snapshot.
            bool retractable = committed || me.DoneShaping;
            SetRowVisible(passButton, canAct && !decided);
            SetInteractable(passButton, canAct && !decided);
            SetRowVisible(withdrawButton, canAct && retractable);
            SetInteractable(withdrawButton, canAct && retractable);

            // Once committed the dice are pledged, so shaping is off until the player withdraws.
            if (committed && shapingAllowed) ShowMessage("Committed — withdraw to change your dice.");
        }

        private void RenderDoneButton(in PlayerSnapshot me, in MatchSnapshot snapshot, bool canAct)
        {
            if (doneTimer == null) return;

            var state = DoneButtonState.Inactive;
            if (canAct)
            {
                switch (snapshot.Phase)
                {
                    case RoundPhase.Shape:
                        state = me.HasDecided ? DoneButtonState.Locked : DoneButtonState.Done;
                        break;
                    case RoundPhase.Commit:
                        state = me.HasDecided ? DoneButtonState.Locked : DoneButtonState.Pick;
                        break;
                    case RoundPhase.Repick:
                        state = me.HasDecided ? DoneButtonState.Locked
                            : IsRepickContender(snapshot, me.PlayerId) ? DoneButtonState.Pick
                            : DoneButtonState.Inactive;
                        break;
                }
            }

            doneTimer.SetState(state);
        }

        private static bool IsRepickContender(in MatchSnapshot snapshot, int playerId)
        {
            var contenders = snapshot.RepickContenders;
            if (contenders == null) return false;
            for (int i = 0; i < contenders.Length; i++)
                if (contenders[i] == playerId) return true;
            return false;
        }

        private void StartRollAnimation()
        {
            StopRollAnimation();
            _rollAnim = anims.Loop(0.9f, t =>
            {
                for (int i = 0; i < _dice.Count; i++)
                {
                    if (!_dice[i].gameObject.activeSelf) continue;
                    _dice[i].PreviewFace(1 + (i + (int)(t * 6f)) % 6);
                    _dice[i].SetWobble(Mathf.Sin((t * 4f + i * 0.37f) * Mathf.PI * 2f) * 4f);
                }
            });
        }

        private void StopRollAnimation()
        {
            if (anims != null) anims.Skip(_rollAnim);
            _rollAnim = default;
            for (int i = 0; i < _dice.Count; i++) _dice[i].SetWobble(0f);
        }

        private static void SetRowVisible(Button button, bool visible)
        {
            if (button != null) button.gameObject.SetActive(visible);
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
                case RoundPhase.Roll: return "Rolling…";
                case RoundPhase.Shape: return "Shape phase";
                case RoundPhase.Commit: return "Commit — secret";
                case RoundPhase.Reveal: return "Reveal";
                case RoundPhase.Repick: return "Re-pick";
                case RoundPhase.Upkeep: return "Upkeep";
                case RoundPhase.MatchOver: return "Match over";
                default: return s.Phase.ToString();
            }
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
