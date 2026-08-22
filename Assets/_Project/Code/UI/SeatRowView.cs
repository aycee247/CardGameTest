using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One seat in the lobby: an avatar tile, a name, and a state chip. Filled seats read solid,
    /// open seats read as a hairline placeholder, seats beyond the session's capacity read closed.
    /// Passive — the controller decides what each seat is.
    /// </summary>
    public sealed class SeatRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private BlueprintFrame frame;
        [SerializeField] private Image avatarTile;
        [SerializeField] private BlueprintFrame avatarFrame;
        [SerializeField] private TMP_Text avatarInitial;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private ThemeAsset theme;

        public void SetFilled(string name, string chip, bool isLocal)
        {
            if (nameText != null)
            {
                nameText.text = name ?? string.Empty;
                if (theme != null) nameText.color = theme.textPrimary;
            }

            if (chipText != null)
            {
                chipText.text = chip ?? string.Empty;
                if (theme != null) chipText.color = theme.Accent(700);
            }

            if (avatarTile != null) avatarTile.gameObject.SetActive(true);
            if (avatarInitial != null)
                avatarInitial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);

            if (theme == null) return;

            if (background != null)
                background.color = isLocal ? theme.Accent(100) : theme.surfaceRaised;
            if (frame != null)
                frame.SetBorderColor(isLocal ? theme.accentPriority : theme.divider);
            if (avatarFrame != null)
                avatarFrame.SetBorderColor(isLocal ? theme.accentPriority : theme.Accent(700));
        }

        public void SetOpen()
        {
            SetPlaceholder("Waiting for player…", string.Empty);
        }

        public void SetClosed()
        {
            SetPlaceholder("Seat closed", string.Empty);
        }

        private void SetPlaceholder(string text, string chip)
        {
            if (nameText != null)
            {
                nameText.text = text;
                if (theme != null) nameText.color = theme.textMuted;
            }

            if (chipText != null) chipText.text = chip;
            if (avatarTile != null) avatarTile.gameObject.SetActive(false);

            if (theme == null) return;

            if (background != null)
            {
                var clear = theme.surfaceRaised;
                clear.a = 0f;
                background.color = clear;
            }

            if (frame != null) frame.SetBorderColor(theme.divider);
        }
    }
}
