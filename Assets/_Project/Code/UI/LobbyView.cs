using System;
using System.Collections.Generic;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>One seat's worth of lobby data, prepared by the controller.</summary>
    public readonly struct SeatEntry
    {
        public readonly string Name;
        public readonly string Chip;
        public readonly bool IsLocal;

        public SeatEntry(string name, string chip, bool isLocal)
        {
            Name = name;
            Chip = chip;
            IsLocal = isLocal;
        }
    }

    /// <summary>
    /// The lobby: the shareable join code, six seat rows filling as friends arrive, and the
    /// host's start button, which states plainly what it is waiting for. Passive.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour
    {
        private const int TotalSeatRows = 6;

        [SerializeField] private TMP_Text codeLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text seatsCountLabel;
        [SerializeField] private Transform seatsRoot;
        [SerializeField] private SeatRowView seatRowTemplate;
        [SerializeField] private Button startButton;
        [SerializeField] private Image startFill;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private ThemeAsset theme;

        private readonly List<SeatRowView> _rows = new List<SeatRowView>();

        public event Action StartClicked;
        public event Action BackClicked;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(() => StartClicked?.Invoke());
            if (backButton != null) backButton.onClick.AddListener(() => BackClicked?.Invoke());
        }

        public void SetCode(string code)
        {
            if (codeLabel != null) codeLabel.text = string.IsNullOrEmpty(code) ? "—" : code;
        }

        public void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message ?? string.Empty;
        }

        /// <summary>
        /// Renders the seat list: filled rows first, open rows up to <paramref name="capacity"/>,
        /// closed rows for the rest of the six.
        /// </summary>
        public void RenderSeats(IReadOnlyList<SeatEntry> filled, int capacity)
        {
            int filledCount = filled?.Count ?? 0;

            if (seatsCountLabel != null)
                seatsCountLabel.text = $"{filledCount} / {Mathf.Max(capacity, filledCount)}";

            if (seatsRoot == null || seatRowTemplate == null) return;

            while (_rows.Count < TotalSeatRows)
                _rows.Add(Instantiate(seatRowTemplate, seatsRoot));

            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].gameObject.SetActive(true);

                if (i < filledCount)
                {
                    var entry = filled[i];
                    _rows[i].SetFilled(entry.Name, entry.Chip, entry.IsLocal);
                }
                else if (i < capacity) _rows[i].SetOpen();
                else _rows[i].SetClosed();
            }
        }

        /// <summary>Only the host sees the button; it explains its own disabled state.</summary>
        public void SetStartState(int joined, int capacity, bool isHost)
        {
            if (startButton == null) return;

            startButton.gameObject.SetActive(isHost);
            if (!isHost) return;

            // The handoff enables Start only when full, but sessions are always created with six
            // seats (no player-count picker yet) — full-only would make a 3-friend match
            // unstartable. Two players is the rules floor.
            bool ready = joined >= 2;
            startButton.interactable = ready;

            if (startLabel != null)
                startLabel.text = ready
                    ? $"START MATCH · {joined} PLAYERS"
                    : $"WAITING — {joined}/{capacity} JOINED";

            if (theme != null)
            {
                if (startFill != null)
                    startFill.color = ready ? theme.accentPriority : theme.surfaceRaised;
                if (startLabel != null)
                    startLabel.color = ready ? theme.textInverse : theme.textMuted;
            }
        }
    }
}
