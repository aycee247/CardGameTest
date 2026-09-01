using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One player's cell in the opponent rail (UI-1) — a horizontal strip, one cell per player.
    ///
    /// In a simultaneous game the vital question is not whose turn it is but who has locked in and
    /// who is still thinking, so the state chip carries as much weight as the score. Connection
    /// trouble shares that chip, because "they have not decided" and "they are not there" need to
    /// be told apart at a glance. The state line only renders during input phases; reconnecting
    /// and left always show.
    /// </summary>
    public sealed class PlayerRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Image background;
        [SerializeField] private BlueprintFrame frame;
        [SerializeField] private Image priorityMarker;

        [Tooltip("Colour tokens, re-read every render so a theme swap shows without regenerating.")]
        [SerializeField] private ThemeAsset theme;

        private UiAnimationService _anims;
        private bool _hadPriority;

        public void Set(in PlayerSnapshot player, bool isObserver, RoundPhase phase)
        {
            if (nameText != null)
                nameText.text = isObserver
                    ? (player.DisplayName + " — YOU").ToUpperInvariant()
                    : (player.DisplayName ?? string.Empty).ToUpperInvariant();

            if (scoreText != null) scoreText.text = player.Score.ToString();

            if (detailText != null)
                detailText.text = $"{player.DiceFaces?.Length ?? 0}d · {player.Sparks}sp";

            if (theme != null)
            {
                if (background != null)
                    background.color = isObserver ? theme.Accent(100) : theme.surfaceRaised;
                if (frame != null)
                    frame.SetBorderColor(isObserver ? theme.accentPriority : theme.divider);
            }

            // Priority is public and worth reading at a glance: it is who wins a contested card.
            // Ratcheting onto a new cell gets a pop, so the handover is watchable (P5).
            if (priorityMarker != null)
            {
                bool hasPriority = player.PriorityRank == 0;
                priorityMarker.enabled = hasPriority;
                if (hasPriority && !_hadPriority)
                {
                    if (_anims == null) _anims = GetComponentInParent<UiAnimationService>(true);
                    if (_anims != null)
                    {
                        var marker = priorityMarker.transform;
                        _anims.Play(0.3f, UiEase.OutBack, t =>
                            marker.localScale = Vector3.one * Mathf.LerpUnclamped(1.7f, 1f, t));
                    }
                }
                _hadPriority = hasPriority;
            }

            if (stateText != null)
            {
                stateText.text = DescribeState(player, phase, out var color);
                stateText.color = color;
            }
        }

        private string DescribeState(in PlayerSnapshot player, RoundPhase phase, out Color color)
        {
            var trouble = theme != null ? theme.stateTrouble : Color.white;

            switch (player.Status)
            {
                case SeatStatus.Reconnecting:
                    color = trouble;
                    // "back in 8s", not "reconnecting 8s": the rail cell is 150 units wide, and
                    // the longer wording truncated to "reconnecting…" — losing the countdown,
                    // which is the only part of it that changes or tells you anything.
                    return player.ReconnectSecondsLeft > 0f
                        ? $"back in {Mathf.CeilToInt(player.ReconnectSecondsLeft)}s"
                        : "reconnecting";

                case SeatStatus.Abandoned:
                    color = trouble;
                    return "left";
            }

            // Decided-ness only means something while there is a decision to make.
            bool inputPhase = phase == RoundPhase.Shape || phase == RoundPhase.Commit || phase == RoundPhase.Repick;
            if (!inputPhase)
            {
                color = theme != null ? theme.textMuted : Color.white;
                return string.Empty;
            }

            color = theme == null ? Color.white
                : player.HasDecided ? theme.stateReady
                : theme.stateThinking;
            return player.HasDecided ? "READY" : "THINKING";
        }
    }
}
