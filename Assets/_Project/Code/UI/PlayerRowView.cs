using Game.Core;
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

        [Header("Colours")]
        [SerializeField] private Color rowColor = new Color(0.13f, 0.15f, 0.19f, 1f);
        [SerializeField] private Color observerRowColor = new Color(0.19f, 0.23f, 0.30f, 1f);
        [SerializeField] private Color readyColor = new Color(0.42f, 0.76f, 0.55f);
        [SerializeField] private Color thinkingColor = new Color(0.62f, 0.65f, 0.70f);
        [SerializeField] private Color troubleColor = new Color(0.90f, 0.55f, 0.35f);

        public void Set(in PlayerSnapshot player, bool isObserver)
        {
            if (nameText != null) nameText.text = isObserver ? player.DisplayName + "  (you)" : player.DisplayName;
            if (scoreText != null) scoreText.text = player.Score.ToString();

            if (detailText != null)
                detailText.text = $"{player.DiceFaces?.Length ?? 0}d   {player.Sparks}sp   {player.CardCount}c";

            if (background != null) background.color = isObserver ? observerRowColor : rowColor;

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
            switch (player.Status)
            {
                case SeatStatus.Reconnecting:
                    color = troubleColor;
                    return player.ReconnectSecondsLeft > 0f
                        ? $"reconnecting {Mathf.CeilToInt(player.ReconnectSecondsLeft)}s"
                        : "reconnecting";

                case SeatStatus.Abandoned:
                    color = troubleColor;
                    return "left";
            }

            color = player.HasDecided ? readyColor : thinkingColor;
            return player.HasDecided ? "ready" : "thinking";
        }
    }
}
