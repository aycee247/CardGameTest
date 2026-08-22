using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One player in the standings rail (UI-1). Passive.
    ///
    /// In a simultaneous game the vital question is not whose turn it is but who has locked in and
    /// who is still thinking, so the state chip carries as much weight as the score. Connection
    /// trouble shares that chip, because "they have not decided" and "they are not there" need to
    /// be told apart at a glance.
    /// </summary>
    public sealed class PlayerRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Image background;
        [SerializeField] private Image priorityMarker;

        [Tooltip("Colour tokens, re-read every render so a theme swap shows without regenerating.")]
        [SerializeField] private ThemeAsset theme;

        public void Set(in PlayerSnapshot player, bool isObserver)
        {
            if (nameText != null) nameText.text = isObserver ? player.DisplayName + "  (you)" : player.DisplayName;
            if (scoreText != null) scoreText.text = player.Score.ToString();

            if (detailText != null)
                detailText.text = $"{player.DiceFaces?.Length ?? 0}d   {player.Sparks}sp   {player.CardCount}c";

            if (background != null && theme != null)
                background.color = isObserver ? theme.Accent(100) : theme.surfaceRaised;

            // Priority is public and worth reading at a glance: it is who wins a contested card.
            if (priorityMarker != null) priorityMarker.enabled = player.PriorityRank == 0;

            if (stateText != null)
            {
                stateText.text = DescribeState(player, out var color);
                stateText.color = color;
            }
        }

        private string DescribeState(in PlayerSnapshot player, out Color color)
        {
            var trouble = theme != null ? theme.stateTrouble : Color.white;

            switch (player.Status)
            {
                case SeatStatus.Reconnecting:
                    color = trouble;
                    return player.ReconnectSecondsLeft > 0f
                        ? $"reconnecting {Mathf.CeilToInt(player.ReconnectSecondsLeft)}s"
                        : "reconnecting";

                case SeatStatus.Abandoned:
                    color = trouble;
                    return "left";
            }

            color = theme == null ? Color.white
                : player.HasDecided ? theme.stateReady
                : theme.stateThinking;
            return player.HasDecided ? "ready" : "thinking";
        }
    }
}
