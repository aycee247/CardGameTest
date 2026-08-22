using System;
using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A single market card. Passive: it renders a <see cref="CardSnapshot"/> and raises
    /// <see cref="Clicked"/> with its card id. The presenter decides what a click means.
    ///
    /// A card shows three things because a player needs all three to choose: what it costs,
    /// what it permanently does, and what it is worth.
    /// </summary>
    public sealed class CardButtonView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text tierText;
        [SerializeField] private Image artwork;
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private BlueprintFrame frame;
        [SerializeField] private CanvasGroup fade;

        [Tooltip("Colour tokens, re-read every render so a theme swap shows without regenerating.")]
        [SerializeField] private ThemeAsset theme;

        public int CardId { get; private set; }

        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(CardId));
        }

        /// <summary>
        /// <paramref name="interactable"/> is whether a claim can be attempted at all right now
        /// (the right phase, the device in the right hands). Affordability is shown separately, as
        /// colour, so a player can still read a card they cannot yet pay for.
        /// </summary>
        public void Set(in CardSnapshot card, bool interactable, Sprite art = null)
        {
            CardId = card.CardId;

            if (nameText != null) nameText.text = card.DisplayName;
            if (costText != null) costText.text = card.CostText;
            if (powerText != null) powerText.text = card.PowerText;
            if (pointsText != null) pointsText.text = card.Points.ToString();
            if (tierText != null) tierText.text = "T" + card.Tier;
            if (artwork != null && art != null) artwork.sprite = art;

            // Affordability is opacity + border + fill together — never colour alone (UI-3,
            // theming.md's accessibility constraint): payable cards sit at full strength on paper
            // with an accent border; unpayable ones recede to half opacity behind a hairline.
            if (fade != null) fade.alpha = card.AffordableNow ? 1f : 0.5f;

            if (theme != null)
            {
                if (frame != null)
                    frame.SetBorderColor(card.AffordableNow ? theme.Accent(400) : theme.divider,
                        theme.accentPriority);

                if (background != null)
                {
                    var fill = theme.surfaceBase;
                    if (!card.AffordableNow) fill.a = 0f;
                    background.color = fill;
                }
            }

            if (button != null) button.interactable = interactable;
        }
    }
}
