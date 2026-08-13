using System;
using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// In-game HUD. Renders a <see cref="GameStateSnapshot"/> and raises intent events. It knows
    /// nothing about networking — a presenter connects these events to <see cref="IGameActions"/>
    /// and feeds it snapshots from <see cref="IGameStateView"/>, so the exact same view works
    /// online and against the offline <see cref="LocalGameSession"/>.
    /// </summary>
    public sealed class GameHudView : MonoBehaviour
    {
        [Header("Status")]
        [SerializeField] private TMP_Text turnLabel;
        [SerializeField] private TMP_Text phaseLabel;
        [SerializeField] private TMP_Text rollsLabel;
        [SerializeField] private TMP_Text diceLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private TMP_Text messageLabel;

        [Header("Controls")]
        [SerializeField] private Button rollButton;
        [SerializeField] private Button endTurnButton;

        [Header("Market")]
        [SerializeField] private Transform marketRoot;
        [SerializeField] private CardButtonView cardButtonPrefab;

        private readonly List<CardButtonView> _spawned = new List<CardButtonView>();
        private int _localPlayerId;

        public event Action RollClicked;
        public event Action EndTurnClicked;
        public event Action<int> ClaimClicked;

        private void Awake()
        {
            if (rollButton != null) rollButton.onClick.AddListener(() => RollClicked?.Invoke());
            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => EndTurnClicked?.Invoke());
        }

        public void SetLocalPlayer(PlayerId localPlayer) => _localPlayerId = localPlayer.Value;

        public void ShowMessage(string message)
        {
            if (messageLabel != null) messageLabel.text = message;
        }

        public void Render(in GameStateSnapshot s)
        {
            bool myTurn = s.CurrentPlayerId == _localPlayerId && !s.HasWinner;

            if (turnLabel != null)
                turnLabel.text = s.HasWinner
                    ? $"Winner: Player {s.WinnerId + 1}"
                    : (myTurn ? "Your turn" : $"Player {s.CurrentPlayerId + 1}'s turn");

            if (phaseLabel != null) phaseLabel.text = s.Phase.ToString();
            if (rollsLabel != null) rollsLabel.text = $"Rolls left: {s.RollsRemaining}";
            if (diceLabel != null) diceLabel.text = FormatLocalDice(s);
            if (scoreLabel != null) scoreLabel.text = FormatScores(s);

            if (rollButton != null) rollButton.interactable = myTurn && s.RollsRemaining > 0;
            if (endTurnButton != null) endTurnButton.interactable = myTurn && s.Phase == GamePhase.Rolled;

            RenderMarket(s);
        }

        private string FormatLocalDice(in GameStateSnapshot s)
        {
            if (s.Players == null) return string.Empty;
            foreach (var p in s.Players)
                if (p.PlayerId == _localPlayerId)
                    return "Dice: " + (p.CurrentRoll == null || p.CurrentRoll.Length == 0
                        ? "—" : string.Join(" ", p.CurrentRoll));
            return string.Empty;
        }

        private string FormatScores(in GameStateSnapshot s)
        {
            if (s.Players == null) return string.Empty;
            var parts = new List<string>(s.Players.Length);
            foreach (var p in s.Players) parts.Add($"P{p.PlayerId + 1}: {p.Score}");
            return string.Join("   ", parts);
        }

        private void RenderMarket(in GameStateSnapshot s)
        {
            if (marketRoot == null || cardButtonPrefab == null) return;

            var market = s.Market ?? Array.Empty<CardSnapshot>();

            // Grow the pool if needed.
            while (_spawned.Count < market.Length)
            {
                var view = Instantiate(cardButtonPrefab, marketRoot);
                view.Clicked += OnCardClicked;
                _spawned.Add(view);
            }

            for (int i = 0; i < _spawned.Count; i++)
            {
                bool active = i < market.Length;
                _spawned[i].gameObject.SetActive(active);
                if (active) _spawned[i].Set(market[i]);
            }
        }

        private void OnCardClicked(int cardId) => ClaimClicked?.Invoke(cardId);
    }
}
