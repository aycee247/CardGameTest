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

            // Affordable reads as the paper lift; unaffordable recedes into the board at half
            // strength. The border-and-opacity treatment the design specifies lands with the
            // blueprint frame; until then the fill change carries the state.
            if (background != null && theme != null)
                background.color = card.AffordableNow
                    ? theme.surfaceBase
                    : new Color(theme.surfaceRaised.r, theme.surfaceRaised.g, theme.surfaceRaised.b, 0.5f);

            if (button != null) button.interactable = interactable;
        }
    }
}
