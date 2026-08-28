using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The reveal, one contest at a time (UI-4, handoff 6l): a full-bleed takeover that flips each
    /// claimed card in, staggers its claimants under it, then stamps the result with the priority
    /// rule made visible. Renders entirely from the snapshot's Reveals preview, so hot-seat and
    /// online play the identical beat. Tap skips the current animation, then advances; every tween
    /// routes through the animation service, so reduced motion collapses the whole sequence.
    /// </summary>
    public sealed class RevealSpotlightView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button tapCatcher;
        [SerializeField] private TMP_Text headerLeft;
        [SerializeField] private TMP_Text headerRight;
        [SerializeField] private RectTransform cardPanel;
        [SerializeField] private TMP_Text cardTier;
        [SerializeField] private TMP_Text cardPoints;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private TMP_Text cardPower;
        [SerializeField] private Transform claimantsRoot;
        [SerializeField] private RectTransform claimantChipTemplate;
        [SerializeField] private TMP_Text resultStamp;
        [SerializeField] private TMP_Text reasonLine;
        [SerializeField] private TMP_Text continuePrompt;
        [SerializeField] private UiAnimationService anims;
        [SerializeField] private ThemeAsset theme;

        // Compressed to fit the phase clock (STORY-3.1 AC5): per claim the beat spends
        // StageDelay + knockout (~0.2s per loser) + AdvanceDelay ≈ 2.4–3s against the server's
        // 2.5s base + 2.4s per claim, so the sequence never outruns the window.
        private const float StageDelay = 1.3f;
        private const float AdvanceDelay = 1.0f;

        private readonly List<RectTransform> _chips = new List<RectTransform>();
        private readonly List<TMP_Text> _chipLabels = new List<TMP_Text>();

        private MatchSnapshot _snapshot;
        private RevealSnapshot[] _reveals = Array.Empty<RevealSnapshot>();
        private int _stageIndex;
        private bool _resultShown;
        private bool _advancing;
        private AnimHandle _flip;
        private AnimHandle _auto;
        private AnimHandle _prompt;
        private AnimHandle _knockout;

        /// <summary>Raised after the last beat. Hot-seat gates ContinueFromReveal on this.</summary>
        public event Action Finished;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (tapCatcher != null) tapCatcher.onClick.AddListener(OnTap);
        }

        public void Play(in MatchSnapshot snapshot)
        {
            _snapshot = snapshot;
            _reveals = snapshot.Reveals ?? Array.Empty<RevealSnapshot>();
            _stageIndex = 0;

            if (root != null) root.SetActive(true);
            if (anims != null && continuePrompt != null)
            {
                anims.Skip(_prompt);
                _prompt = anims.Loop(1.4f, t =>
                    continuePrompt.alpha = 0.45f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f));
            }

            ShowStage();
        }

        public void Hide()
        {
            if (anims != null)
            {
                _advancing = true;     // a skipped knockout must not stamp into a closing view
                anims.Skip(_flip);
                anims.Skip(_auto);
                anims.Skip(_prompt);
                anims.Skip(_knockout);
                _advancing = false;
            }
            if (root != null) root.SetActive(false);
        }

        private void ShowStage()
        {
            if (headerLeft != null) headerLeft.text = $"REVEAL — ROUND {_snapshot.Round:00}";
            if (headerRight != null)
                headerRight.text = _reveals.Length > 1 ? $"CLAIM {_stageIndex + 1} OF {_reveals.Length}" : string.Empty;

            if (_reveals.Length == 0)
            {
                // Everyone passed: one quiet beat instead of a card.
                if (cardPanel != null) cardPanel.gameObject.SetActive(false);
                RenderChips(0);
                ShowResultTexts("NO CLAIMS", "Everyone passed this round");
                _resultShown = true;
                return;
            }

            var reveal = _reveals[_stageIndex];

            if (cardPanel != null) cardPanel.gameObject.SetActive(true);
            if (cardTier != null) cardTier.text = $"TIER {reveal.Tier}";
            if (cardPoints != null) cardPoints.text = $"{reveal.Points} VP";
            if (cardName != null) cardName.text = reveal.DisplayName;
            if (cardPower != null) cardPower.text = reveal.PowerText;

            HideResultTexts();
            RenderChips(reveal.ClaimantIds?.Length ?? 0, reveal);

            _resultShown = false;

            if (anims != null)
            {
                anims.Skip(_flip);
                if (cardPanel != null)
                    _flip = anims.Play(0.4f, UiEase.OutCubic, t =>
                        cardPanel.localEulerAngles = new Vector3(0f, 90f * (1f - t), 0f));

                anims.Skip(_auto);
                _auto = anims.Play(StageDelay, UiEase.Linear, _ => { }, ShowResult);
            }
            else ShowResult();
        }

        private void ShowResult()
        {
            if (_resultShown) return;
            _resultShown = true;

            if (anims != null) anims.Skip(_flip);
            if (cardPanel != null) cardPanel.localEulerAngles = Vector3.zero;

            var reveal = _reveals[_stageIndex];
            int claimants = reveal.ClaimantIds?.Length ?? 0;

            // Contested: priority DECIDES on screen (STORY-3.1 AC3) — losing chips are knocked
            // out one by one, highest score first because lowest wins, each getting its dice
            // back (AC4), before the stamp lands on the survivor.
            if (anims != null && reveal.Contested && claimants > 1 && reveal.WinnerId >= 0)
                PlayKnockout(reveal);
            else
                StampResult();
        }

        private void PlayKnockout(RevealSnapshot reveal)
        {
            var order = new List<int>();
            for (int i = 0; i < reveal.ClaimantIds.Length; i++)
                if (reveal.ClaimantIds[i] != reveal.WinnerId) order.Add(i);
            order.Sort((a, b) => ScoreOf(reveal.ClaimantIds[b]).CompareTo(ScoreOf(reveal.ClaimantIds[a])));

            const float step = 0.18f, knock = 0.25f;
            float total = (order.Count - 1) * step + knock;
            var announced = new bool[order.Count];
            var claimantIds = reveal.ClaimantIds;

            anims.Skip(_knockout);
            _knockout = anims.Play(total, UiEase.Linear, t =>
            {
                for (int k = 0; k < order.Count; k++)
                {
                    float u = Mathf.Clamp01((t * total - k * step) / knock);
                    if (u <= 0f) continue;

                    int chipIndex = order[k];
                    if (chipIndex >= _chips.Count) continue;

                    if (!announced[k])
                    {
                        announced[k] = true;
                        MarkDiceReturned(chipIndex, claimantIds[chipIndex]);
                    }

                    var chip = _chips[chipIndex];
                    float e = 1f - (1f - u) * (1f - u);
                    chip.localScale = new Vector3(1f, 1f - 0.18f * e, 1f);
                    chip.localEulerAngles = new Vector3(0f, 0f, -7f * e);
                    ChipGroup(chip).alpha = 1f - 0.6f * e;
                }
            }, StampResult);
        }

        /// <summary>The knocked claimant's dice come back: the chip says so, and two pips arc
        /// from the card down to it (STORY-3.1 AC4).</summary>
        private void MarkDiceReturned(int chipIndex, int claimantId)
        {
            if (chipIndex < _chipLabels.Count && _chipLabels[chipIndex] != null)
                _chipLabels[chipIndex].text =
                    $"{NameOf(claimantId).ToUpperInvariant()} · {ScoreOf(claimantId)} — DICE BACK";

            if (cardPanel == null || root == null || theme == null || chipIndex >= _chips.Count) return;

            Vector3 from = cardPanel.position;
            Vector3 to = _chips[chipIndex].position;

            for (int p = 0; p < 2; p++)
            {
                var pipGo = new GameObject("ReturnPip", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)pipGo.transform;
                rt.SetParent(root.transform, false);
                rt.sizeDelta = new Vector2(16f, 16f);
                var image = pipGo.GetComponent<Image>();
                image.color = theme.textInverse;
                image.raycastTarget = false;
                rt.position = from;

                float side = p == 0 ? -1f : 1f;
                float sway = 30f * rt.lossyScale.x;
                var captured = pipGo;
                anims.Play(0.4f + p * 0.08f, UiEase.OutCubic, t =>
                {
                    if (captured == null) return;
                    Vector3 pos = Vector3.LerpUnclamped(from, to, t);
                    pos.x += side * sway * Mathf.Sin(Mathf.PI * t);
                    rt.position = pos;
                }, () => { if (captured != null) Destroy(captured); });
            }
        }

        private void StampResult()
        {
            if (_advancing) return;

            var reveal = _reveals[_stageIndex];
            string stamp = reveal.WinnerId < 0 ? "NOBODY CLAIMS IT"
                : reveal.WinnerId == _snapshot.ObserverId ? "YOU CLAIM IT"
                : $"{NameOf(reveal.WinnerId).ToUpperInvariant()} CLAIMS IT";
            string reason = reveal.Contested ? "CONTESTED — LOWEST SCORE TAKES IT" : "UNCONTESTED CLAIM";
            ShowResultTexts(stamp, reason);

            if (anims == null) return;

            // The survivor gets a pop as the stamp lands.
            if (reveal.ClaimantIds != null)
                for (int i = 0; i < reveal.ClaimantIds.Length && i < _chips.Count; i++)
                    if (reveal.ClaimantIds[i] == reveal.WinnerId)
                    {
                        var winnerChip = _chips[i];
                        anims.Play(0.3f, UiEase.OutBack, t =>
                            winnerChip.localScale = Vector3.one * Mathf.LerpUnclamped(1.15f, 1f, t));
                        break;
                    }

            if (resultStamp != null)
            {
                var stampRt = resultStamp.rectTransform;
                anims.Play(0.35f, UiEase.OutBack, t =>
                {
                    stampRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, t);
                    stampRt.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(-10f, -4f, t));
                });
            }

            anims.Skip(_auto);
            _auto = anims.Play(AdvanceDelay, UiEase.Linear, _ => { }, Advance);
        }

        private static CanvasGroup ChipGroup(RectTransform chip)
        {
            var group = chip.GetComponent<CanvasGroup>();
            return group != null ? group : chip.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnTap()
        {
            // First tap completes the beat (skip snaps to end state); the next advances.
            if (!_resultShown && anims != null) anims.Skip(_auto);
            else Advance();
        }

        private void Advance()
        {
            if (anims != null)
            {
                _advancing = true;     // a mid-knockout skip must not stamp the outgoing stage
                anims.Skip(_auto);
                anims.Skip(_knockout);
                _advancing = false;
            }
            _stageIndex++;

            if (_reveals.Length == 0 || _stageIndex >= _reveals.Length)
            {
                Hide();
                Finished?.Invoke();
                return;
            }

            ShowStage();
        }

        private void RenderChips(int count, RevealSnapshot reveal = default)
        {
            if (claimantsRoot == null || claimantChipTemplate == null) return;

            while (_chips.Count < count)
            {
                var chip = Instantiate(claimantChipTemplate, claimantsRoot);
                _chips.Add(chip);
                _chipLabels.Add(chip.GetComponentInChildren<TMP_Text>(true));
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                bool active = i < count;
                _chips[i].gameObject.SetActive(active);
                if (!active) continue;

                int claimantId = reveal.ClaimantIds[i];
                if (_chipLabels[i] != null)
                    _chipLabels[i].text = $"{NameOf(claimantId).ToUpperInvariant()} · {ScoreOf(claimantId)}";

                // Staggered flip-in (~0.18s apart). Reset any knockout residue first — chips
                // are pooled, and the previous stage may have squashed, tilted and dimmed them.
                var chip = _chips[i];
                chip.localScale = Vector3.one;
                chip.localEulerAngles = Vector3.zero;
                ChipGroup(chip).alpha = 1f;
                if (anims != null)
                {
                    int index = i;
                    anims.Play(0.25f + index * 0.18f, UiEase.OutCubic, t =>
                    {
                        float local = Mathf.Clamp01((t * (0.25f + index * 0.18f) - index * 0.18f) / 0.25f);
                        chip.localScale = new Vector3(1f, local, 1f);
                    });
                }
            }
        }

        private void ShowResultTexts(string stamp, string reason)
        {
            if (resultStamp != null)
            {
                resultStamp.gameObject.SetActive(true);
                resultStamp.text = stamp;
            }
            if (reasonLine != null)
            {
                reasonLine.gameObject.SetActive(true);
                reasonLine.text = reason;
            }
        }

        private void HideResultTexts()
        {
            if (resultStamp != null) resultStamp.gameObject.SetActive(false);
            if (reasonLine != null) reasonLine.gameObject.SetActive(false);
        }

        private string NameOf(int playerId)
        {
            var players = _snapshot.Players;
            if (players != null)
                for (int i = 0; i < players.Length; i++)
                    if (players[i].PlayerId == playerId) return players[i].DisplayName ?? $"Player {playerId}";
            return $"Player {playerId}";
        }

        private int ScoreOf(int playerId)
        {
            var players = _snapshot.Players;
            if (players != null)
                for (int i = 0; i < players.Length; i++)
                    if (players[i].PlayerId == playerId) return players[i].Score;
            return 0;
        }
    }
}
