using System;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The re-pick sheet (MKT-3, handoff 6k): shown only to a player who lost a contested claim —
    /// the remaining market, a countdown when the server runs one, and the consolation pass
    /// (MKT-5). Card taps route back into the normal inspect/commit flow; passive throughout.
    /// </summary>
    public sealed class RepickSheetView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private Transform cardsRoot;
        [SerializeField] private CardButtonView cardTemplate;
        [SerializeField] private Button passButton;

        private readonly List<CardButtonView> _cards = new List<CardButtonView>();

        public event Action<int> CardChosen;
        public event Action PassClicked;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (passButton != null) passButton.onClick.AddListener(() => PassClicked?.Invoke());
        }

        public void Show(in MatchSnapshot snapshot)
        {
            if (root != null) root.SetActive(true);
            Render(snapshot);
        }

        public void Render(in MatchSnapshot snapshot)
        {
            if (cardsRoot == null || cardTemplate == null) return;

            var market = snapshot.Market ?? Array.Empty<CardSnapshot>();

            while (_cards.Count < market.Length)
            {
                var card = Instantiate(cardTemplate, cardsRoot);
                card.Clicked += id => CardChosen?.Invoke(id);
                _cards.Add(card);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                bool active = i < market.Length;
                _cards[i].gameObject.SetActive(active);
                if (!active) continue;

                _cards[i].Set(market[i], interactable: true);
                _cards[i].SetCommitted(false);
            }
        }

        /// <summary>Server countdown; negative hides it (hot-seat has no re-pick clock).</summary>
        public void SetCountdown(float secondsLeft)
        {
            if (countdownText == null) return;
            bool ticking = secondsLeft >= 0f;
            countdownText.gameObject.SetActive(ticking);
            if (ticking) countdownText.text = Mathf.CeilToInt(secondsLeft).ToString();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
