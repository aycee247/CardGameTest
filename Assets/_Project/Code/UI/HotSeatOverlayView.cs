using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The full-screen panels that sit over the board in a pass-the-device match: the handoff
    /// screen, the reveal, the round summary, and the final standings.
    ///
    /// The handoff panel is not decoration — it is the privacy boundary. It must fully cover the
    /// board so the previous player's dice and claim are off screen before the device changes hands.
    /// </summary>
    public sealed class HotSeatOverlayView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject handoffPanel;
        [SerializeField] private GameObject summaryPanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Handoff")]
        [SerializeField] private TMP_Text handoffTitle;
        [SerializeField] private TMP_Text handoffBody;
        [SerializeField] private Button handoffButton;

        [Header("Summary")]
        [SerializeField] private TMP_Text summaryBody;
        [SerializeField] private Button summaryButton;

        [Header("Game over")]
        [SerializeField] private TMP_Text gameOverTitle;
        [SerializeField] private TMP_Text gameOverBody;
        [SerializeField] private Button gameOverButton;

        private readonly StringBuilder _sb = new StringBuilder();

        public event Action HandoffConfirmed;
        public event Action SummaryContinued;

        /// <summary>
        /// The player is done with the end-of-the-line panel and wants out. Every route to that
        /// panel — host gone, build mismatch, no seat, nothing arriving — used to be a dead end
        /// whose only exit was force-quitting the app.
        /// </summary>
        public event Action GameOverDismissed;

        private void Awake()
        {
            if (handoffButton != null) handoffButton.onClick.AddListener(() => HandoffConfirmed?.Invoke());
            if (summaryButton != null) summaryButton.onClick.AddListener(() => SummaryContinued?.Invoke());
            if (gameOverButton != null) gameOverButton.onClick.AddListener(() => GameOverDismissed?.Invoke());
        }

        /// <summary>
        /// Shows whichever panel the stage calls for, and hides the rest. Reveal and MatchOver show
        /// none of these — the snapshot-driven <see cref="RevealSpotlightView"/> and
        /// <see cref="EndScreenView"/> own those beats; the game-over panel here survives only for
        /// the host-lost ending (NET-4).
        /// </summary>
        public void Render(HotSeatDirector director)
        {
            var stage = director.Stage;

            Show(handoffPanel, stage == HotSeatStage.Handoff);
            Show(summaryPanel, stage == HotSeatStage.RoundSummary);

            switch (stage)
            {
                case HotSeatStage.Handoff: RenderHandoff(director); break;
                case HotSeatStage.RoundSummary: RenderSummary(director); break;
            }
        }

        /// <summary>True while a panel is covering the board, so the board must not accept input.</summary>
        public bool IsBlocking(HotSeatStage stage) => stage != HotSeatStage.Acting;

        /// <summary>
        /// Solo (STORY-7.1) reuses only the round summary — one human means no handoffs and no
        /// privacy panel. Reveal and MatchOver are owned by the spotlight and end screen, as ever.
        /// </summary>
        public void RenderSolo(bool showSummary, MatchState state)
        {
            Show(handoffPanel, false);
            Show(summaryPanel, showSummary);
            if (showSummary) RenderSummaryBody(state);
        }

        /// <summary>
        /// Ends an online match that lost its host (NET-4), showing the standings from the last
        /// snapshot this client received. Those standings are card points only — the end-of-match
        /// scoring powers are resolved by the server, which is exactly what has gone away — so the
        /// screen says as much rather than presenting a total it cannot stand behind.
        /// </summary>
        public void ShowAbandonedMatch(MatchSnapshot lastKnown)
        {
            OpenGameOver("Final standings");

            if (gameOverBody == null) return;

            _sb.Clear();
            _sb.Append("The host left the match.\n\n");

            var players = lastKnown.Players;
            if (players == null || players.Length == 0)
            {
                _sb.Append("No standings were received.");
            }
            else
            {
                _sb.Append("Standings after round ").Append(lastKnown.Round).Append(":\n\n");

                var ordered = new List<PlayerSnapshot>(players);
                ordered.Sort((a, b) => b.Score.CompareTo(a.Score));

                for (int i = 0; i < ordered.Count; i++)
                    _sb.Append(i + 1).Append(". ").Append(ordered[i].DisplayName)
                       .Append("   ").Append(ordered[i].Score).Append("vp\n");

                _sb.Append("\nCard points only — end-of-match bonuses were never scored.");
            }

            gameOverBody.text = _sb.ToString().TrimEnd();
        }

        /// <summary>
        /// The match cannot be joined or played at all — a mismatched build, no seat in a match
        /// already running, or nothing ever arriving from the host. Says which, and offers the way
        /// out, rather than leaving the player on a board that will never fill in.
        /// </summary>
        public void ShowMatchUnavailable(string title, string body)
        {
            OpenGameOver(title);
            if (gameOverBody != null) gameOverBody.text = body ?? string.Empty;
        }

        private void OpenGameOver(string title)
        {
            Show(handoffPanel, false);
            Show(summaryPanel, false);
            Show(gameOverPanel, true);

            if (gameOverTitle != null) gameOverTitle.text = title;
            Show(gameOverButton != null ? gameOverButton.gameObject : null, true);
        }

        private static void Show(GameObject panel, bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        private void RenderHandoff(HotSeatDirector director)
        {
            var actor = director.State.Find(director.CurrentActor);
            string name = actor?.DisplayName ?? director.CurrentActor.ToString();

            if (handoffTitle != null) handoffTitle.text = $"Pass to {name}";

            if (handoffBody != null)
                handoffBody.text = director.IsRepickPass
                    ? "You lost a contested card. Pick again from what is left.\n\nEveryone else: look away."
                    : "Everyone else: look away.\n\nTap when you are holding the device.";
        }

        private void RenderSummary(HotSeatDirector director) => RenderSummaryBody(director.State);

        private void RenderSummaryBody(MatchState state)
        {
            if (summaryBody == null) return;

            _sb.Clear();
            _sb.Append("End of round ").Append(state.Round).Append("\n\n");

            foreach (var player in state.Players)
            {
                _sb.Append(player.DisplayName)
                   .Append("   ").Append(player.Score).Append("vp")
                   .Append("   ").Append(player.Sparks).Append(" sparks")
                   .Append("   ").Append(player.Dice.Count).Append(" dice");

                if (!player.GainedCardThisRound) _sb.Append("   (consolation)");
                _sb.Append('\n');
            }

            summaryBody.text = _sb.ToString().TrimEnd();
        }

    }
}
