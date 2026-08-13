using System;
using Game.Core;
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

        // Both stay dark: the labels are light, so a light "affordable" fill renders white on white.
        // Affordability reads as a lift in brightness against the dimmed state, not as a colour flip.
        [Header("State colours")]
        [SerializeField] private Color affordableColor = new Color(0.22f, 0.27f, 0.36f);
        [SerializeField] private Color unaffordableColor = new Color(0.13f, 0.14f, 0.18f);

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

            if (background != null)
                background.color = card.AffordableNow ? affordableColor : unaffordableColor;

            if (button != null) button.interactable = interactable;
        }
    }
}
