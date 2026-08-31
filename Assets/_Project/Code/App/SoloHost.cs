using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Runs a solo match against bots (STORY-7.1): the thin Unity skin over
    /// <see cref="SoloDirector"/>, exactly as <see cref="HotSeatHost"/> is over
    /// <see cref="HotSeatDirector"/>. The human keeps the private view for the whole match; bots
    /// live in Core and act on the director's schedule, which this host ticks with scene time.
    /// Fully local like hot-seat (NET-5): no session, no UGS, no networking.
    /// </summary>
    public sealed class SoloHost : MonoBehaviour
    {
        [Tooltip("Generated card database (Foundry ▸ Generate Starter Deck).")]
        [SerializeField] private CardDatabase cardDatabase;

        [SerializeField] private GameHudPresenter presenter;
        [SerializeField] private HotSeatOverlayView overlay;
        [SerializeField] private RevealSpotlightView spotlight;
        [SerializeField] private EndScreenView endScreen;

        [SerializeField] private MatchConfig config = new MatchConfig();

        [Tooltip("0 = a new match every time; anything else replays that exact match.")]
        [SerializeField] private int fixedSeed;

        private SoloDirector _director;
        private LocalMatchSession _session;
        private SoloStage _lastStage;
        private int _botCount;

        public SoloDirector Director => _director;

        public void StartMatch(int botCount)
        {
            _botCount = Mathf.Clamp(botCount, 1, 5);
            int seed = fixedSeed != 0 ? fixedSeed : MatchFactory.NewSeed();

            // The human takes seat 1 under their chosen name, falling back to "Player 1" (STORY-4.3).
            // Never "You": the rail appends its own "— YOU" marker to the local seat, and a seat
            // literally named You rendered as "YOU — YOU" on device (#66).
            var names = new List<string>(1 + _botCount) { LocalIdentity.NameForSeat(0) };
            for (int i = 1; i <= _botCount; i++) names.Add("Bot " + i);

            var state = MatchFactory.Build(config, cardDatabase, names, seed);
            _session = new LocalMatchSession(state, new SeededDiceRoller(unchecked((ulong)seed)));

            var bots = new List<BotPlayer>(_botCount);
            for (int seat = 1; seat <= _botCount; seat++)
                bots.Add(new BotPlayer(new PlayerId(seat), ResolveCard,
                    unchecked((ulong)seed * 31UL + (ulong)seat)));

            _director = new SoloDirector(_session, new PlayerId(0), bots,
                pacingSeed: unchecked((ulong)seed ^ 0xB07B07UL));

            if (presenter != null)
            {
                presenter.DoneRequested -= OnDone;     // re-entry safe on rematch
                presenter.Bind(_session, _session);
                presenter.DoneRequested += OnDone;
            }

            if (spotlight != null)
            {
                spotlight.Finished -= OnRevealFinished;
                spotlight.Finished += OnRevealFinished;
            }

            if (overlay != null)
            {
                overlay.SummaryContinued -= OnSummaryContinued;
                overlay.SummaryContinued += OnSummaryContinued;
            }

            if (endScreen != null)
            {
                endScreen.Hide();
                endScreen.SetRematchVisible(true);
                endScreen.RematchClicked -= OnRematch;
                endScreen.RematchClicked += OnRematch;
            }

            _director.Begin(Time.time);
            _lastStage = SoloStage.MatchOver;      // force the first Refresh to count as a change
            Refresh();
        }

        private Card ResolveCard(CardId id) => cardDatabase != null ? cardDatabase.Find(id)?.ToCard() : null;

        private void Update()
        {
            if (_director == null) return;

            var before = _director.Stage;
            _director.Tick(Time.time);
            if (_director.Stage != before) Refresh();
        }

        private void OnDone()
        {
            _director?.HumanDone(Time.time);
            Refresh();
        }

        private void OnRevealFinished()
        {
            _director?.ContinueFromReveal(Time.time);
            Refresh();
        }

        private void OnSummaryContinued()
        {
            _director?.ContinueFromSummary(Time.time);
            Refresh();
        }

        private void OnRematch()
        {
            if (endScreen != null) endScreen.Hide();
            StartMatch(_botCount);
        }

        private void Refresh()
        {
            if (_director == null) return;

            var stage = _director.Stage;

            if (overlay != null) overlay.RenderSolo(stage == SoloStage.RoundSummary, _director.State);

            if (spotlight != null)
            {
                if (stage == SoloStage.Reveal && !spotlight.IsOpen)
                    spotlight.Play(_session.Current);
                else if (stage != SoloStage.Reveal && spotlight.IsOpen)
                    spotlight.Hide();
            }

            if (endScreen != null)
            {
                if (stage == SoloStage.MatchOver && !endScreen.IsOpen)
                    endScreen.Show(_session.Current);
                else if (stage != SoloStage.MatchOver && endScreen.IsOpen)
                    endScreen.Hide();
            }

            // The human may be locked out of an Acting stage: a re-pick pass they are not part of.
            bool acting = stage == SoloStage.Acting && _director.HumanMayAct;

            if (presenter != null)
            {
                if (stage != _lastStage && acting) presenter.ClearSelection();

                presenter.SetContext(acting, acting && !_director.IsRepickPass);

                if (stage != _lastStage || acting != _lastActing)
                    presenter.ShowMessage(
                        stage == SoloStage.Acting && _director.IsRepickPass
                            ? (acting ? "Re-pick: choose from what is left." : "Bots are re-picking…")
                            : string.Empty);
            }

            _lastStage = stage;
            _lastActing = acting;
        }

        private bool _lastActing;
    }
}
