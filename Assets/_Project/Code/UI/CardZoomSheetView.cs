using System;
using Game.Core;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The card inspect sheet (handoff 6h, UI-3): tap a market card, read its full cost and power,
    /// see a suggested paying selection highlighted in the tray below, adjust, and commit from
    /// here. Passive — the presenter owns the suggestion, validation and the commit itself.
    /// </summary>
    public sealed class CardZoomSheetView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button scrimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text tierTag;
        [SerializeField] private TMP_Text familyTag;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private TMP_Text payStatusText;
        [SerializeField] private Button commitButton;
        [SerializeField] private Image commitFill;
        [SerializeField] private TMP_Text commitLabel;
        [SerializeField] private ThemeAsset theme;

        public event Action CommitConfirmed;
        public event Action Dismissed;

        public int CardId { get; private set; } = -1;
        public bool IsOpen => root != null && root.activeSelf;

        private UiAnimationService _anims;

        private void Awake()
        {
            if (scrimButton != null) scrimButton.onClick.AddListener(() => Dismissed?.Invoke());
            if (closeButton != null) closeButton.onClick.AddListener(() => Dismissed?.Invoke());
            if (commitButton != null) commitButton.onClick.AddListener(() => CommitConfirmed?.Invoke());
            _anims = GetComponentInParent<UiAnimationService>(true);
        }

        public void Show(in CardSnapshot card)
        {
            CardId = card.CardId;

            if (tierTag != null) tierTag.text = $"TIER {card.Tier}";
            if (familyTag != null) familyTag.text = card.Family.ToString().ToUpperInvariant();
            if (nameText != null) nameText.text = card.DisplayName;
            if (pointsText != null) pointsText.text = $"{card.Points} VP";
            if (costText != null) costText.text = card.CostText;
            if (powerText != null) powerText.text = card.PowerText;

            if (root != null)
            {
                bool wasOpen = root.activeSelf;
                root.SetActive(true);
                if (!wasOpen) UiEntrance.StampIn(_anims, root.transform);
            }
        }

        /// <summary>Whether the current dice selection validates against the cost.</summary>
        public void SetPayState(bool canPay)
        {
            if (payStatusText != null)
            {
                payStatusText.text = canPay
                    ? "Selected dice pay this cost"
                    : "Select dice below that pay the cost";
                if (theme != null) payStatusText.color = canPay ? theme.Accent(700) : theme.textMuted;
            }

            if (commitButton != null) commitButton.interactable = canPay;
            if (commitLabel != null) commitLabel.text = canPay ? "COMMIT · SECRET" : "CANNOT PAY";

            if (theme != null)
            {
                if (commitFill != null) commitFill.color = canPay ? theme.accentPriority : theme.surfaceRaised;
                if (commitLabel != null) commitLabel.color = canPay ? theme.textInverse : theme.textMuted;
            }
        }

        public void Hide()
        {
            CardId = -1;
            if (root != null) root.SetActive(false);
        }
    }
}
