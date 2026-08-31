using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Runs a pass-the-device match. Owns the <see cref="HotSeatDirector"/> and translates its
    /// stage into what the screen shows and whether the board accepts input.
    ///
    /// Deliberately thin: all the flow logic lives in the director, which is pure C# and covered by
    /// the headless test suite. What is left here is Unity wiring.
    /// </summary>
    public sealed class HotSeatHost : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private CardDatabase cardDatabase;

        [Header("Screen")]
        [SerializeField] private GameHudPresenter presenter;
        [SerializeField] private HotSeatOverlayView overlay;
        [SerializeField] private RevealSpotlightView spotlight;
        [SerializeField] private EndScreenView endScreen;

        [Header("Match")]
        [Range(2, 6)]
        [SerializeField] private int playerCount = 2;
        [SerializeField] private MatchConfig config = new MatchConfig();

        [Tooltip("Leave at 0 for a different match every time; set a value to replay an exact one.")]
        [SerializeField] private int fixedSeed;

        [Tooltip("Leave off in the generated scene: GameSceneBootstrap decides between hot-seat and " +
                 "online and starts this itself. Turn on only for a scene that is hot-seat only.")]
        [SerializeField] private bool autoStartOnLoad;

        private HotSeatDirector _director;
        private LocalMatchSession _session;
        private HotSeatStage _lastStage = HotSeatStage.MatchOver;

        public HotSeatDirector Director => _director;

        private void Start()
        {
            if (autoStartOnLoad) StartMatch();
        }

        public void StartMatch()
        {
            if (cardDatabase == null)
            {
                Debug.LogError("[Foundry] HotSeatHost has no CardDatabase. " +
                               "Run Foundry ▸ Generate Starter Deck, then assign it.");
                return;
            }

            int seed = fixedSeed != 0 ? fixedSeed : MatchFactory.NewSeed();

            // Seat 1 is whoever owns the device; the rest keep their seat defaults (STORY-4.3).
            var names = MatchFactory.NamesWithLocalPlayer(playerCount, LocalIdentity.RawDisplayName);

            var state = MatchFactory.Build(config, cardDatabase, names, seed);
            _session = new LocalMatchSession(state, new SeededDiceRoller(unchecked((ulong)seed)));
            _director = new HotSeatDirector(_session);

            if (presenter != null)
            {
                presenter.Bind(_session, _session);
                presenter.DoneRequested -= OnDone;
                presenter.DoneRequested += OnDone;
            }

            if (overlay != null)
            {
                overlay.HandoffConfirmed -= OnHandoffConfirmed;
                overlay.SummaryContinued -= OnSummaryContinued;

                overlay.HandoffConfirmed += OnHandoffConfirmed;
                overlay.SummaryContinued += OnSummaryContinued;
            }

            if (spotlight != null)
            {
                // The spotlight holds the round: resolution applies only after its last beat.
                spotlight.Finished -= OnRevealFinished;
                spotlight.Finished += OnRevealFinished;
            }

            if (endScreen != null)
            {
                endScreen.Hide();
                endScreen.SetRematchVisible(true);
                endScreen.RematchClicked -= OnRematch;
                endScreen.RematchClicked += OnRematch;
            }

            _director.Begin();
            _lastStage = HotSeatStage.MatchOver;   // force the first Refresh to count as a change
            Refresh();
        }

        private void OnHandoffConfirmed()
        {
            _director.ConfirmHandoff();
            Refresh();
        }

        private void OnDone()
        {
            _director.EndActing();
            Refresh();
        }

        private void OnRevealFinished()
        {
            _director.ContinueFromReveal();
            Refresh();
        }

        private void OnRematch()
        {
            if (endScreen != null) endScreen.Hide();
            StartMatch();
        }

        private void OnSummaryContinued()
        {
            _director.ContinueFromSummary();
            Refresh();
        }

        private void Refresh()
        {
            if (_director == null) return;

            var stage = _director.Stage;

            if (overlay != null) overlay.Render(_director);

            if (spotlight != null)
            {
                if (stage == HotSeatStage.Reveal && !spotlight.IsOpen)
                    spotlight.Play(_session.Current);
                else if (stage != HotSeatStage.Reveal && spotlight.IsOpen)
                    spotlight.Hide();
            }

            if (endScreen != null)
            {
                if (stage == HotSeatStage.MatchOver && !endScreen.IsOpen)
                    endScreen.Show(_session.Current);
                else if (stage != HotSeatStage.MatchOver && endScreen.IsOpen)
                    endScreen.Hide();
            }

            bool acting = stage == HotSeatStage.Acting;

            if (presenter != null)
            {
                // A new seat starts with a clean tray — otherwise the previous player's highlighted
                // dice would carry over and read as this player's choice.
                if (stage != _lastStage && acting) presenter.ClearSelection();

                presenter.SetContext(acting, acting && !_director.IsRepickPass);

                if (stage != _lastStage)
                    presenter.ShowMessage(acting && _director.IsRepickPass
                        ? "Re-pick: choose from what is left."
                        : string.Empty);
            }

            _lastStage = stage;
        }
    }
}
