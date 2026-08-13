using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A single claimable market card. Passive: it renders a <see cref="Game.Core.CardSnapshot"/>
    /// and raises <see cref="Clicked"/> with its card id. The presenter decides what a click means.
    /// </summary>
    public sealed class CardButtonView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private Image artwork;
        [SerializeField] private Button button;

        public int CardId { get; private set; }
        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(CardId));
        }

        public void Set(in Game.Core.CardSnapshot card, Sprite art = null)
        {
            CardId = card.CardId;
            if (nameText != null) nameText.text = card.DisplayName;
            if (requirementText != null) requirementText.text = card.RequirementText;
            if (pointsText != null) pointsText.text = card.Points.ToString();
            if (artwork != null && art != null) artwork.sprite = art;
            if (button != null) button.interactable = card.ClaimableNow;
        }
    }
}
