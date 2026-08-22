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

        private const float ResultDelay = 2.4f;

        private readonly List<RectTransform> _chips = new List<RectTransform>();
        private readonly List<TMP_Text> _chipLabels = new List<TMP_Text>();

        private MatchSnapshot _snapshot;
        private RevealSnapshot[] _reveals = Array.Empty<RevealSnapshot>();
        private int _stageIndex;
        private bool _resultShown;
        private AnimHandle _flip;
        private AnimHandle _auto;
        private AnimHandle _prompt;

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
                anims.Skip(_flip);
                anims.Skip(_auto);
                anims.Skip(_prompt);
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
                _auto = anims.Play(ResultDelay, UiEase.Linear, _ => { }, ShowResult);
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
            string stamp = reveal.WinnerId < 0 ? "NOBODY CLAIMS IT"
                : reveal.WinnerId == _snapshot.ObserverId ? "YOU CLAIM IT"
                : $"{NameOf(reveal.WinnerId).ToUpperInvariant()} CLAIMS IT";
            string reason = reveal.Contested ? "CONTESTED — PRIORITY: LOWEST SCORE WINS" : "UNCONTESTED CLAIM";
            ShowResultTexts(stamp, reason);

            if (anims != null && resultStamp != null)
            {
                var stampRt = resultStamp.rectTransform;
                anims.Play(0.35f, UiEase.OutBack, t =>
                {
                    stampRt.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, t);
                    stampRt.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(-10f, -4f, t));
                });

                anims.Skip(_auto);
                _auto = anims.Play(ResultDelay, UiEase.Linear, _ => { }, Advance);
            }
        }

        private void OnTap()
        {
            // First tap completes the beat (skip snaps to end state); the next advances.
            if (!_resultShown && anims != null) anims.Skip(_auto);
            else Advance();
        }

        private void Advance()
        {
            if (anims != null) anims.Skip(_auto);
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

                // Staggered flip-in (~0.18s apart).
                var chip = _chips[i];
                chip.localScale = Vector3.one;
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
