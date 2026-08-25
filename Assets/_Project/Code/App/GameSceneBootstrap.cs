using Game.Core;
using Game.Networking;
using Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Decides, once, whether this Game scene is a pass-the-device match or a networked one, and
    /// wires the board to whichever drives it.
    ///
    /// The two modes share the entire presentation layer because both
    /// <see cref="Game.Core.LocalMatchSession"/> and <see cref="NetworkGameController"/> implement
    /// <see cref="Game.Core.IGameActions"/> and <see cref="Game.Core.IMatchView"/>. What differs is
    /// only who advances the phases: the player, or the server's clock.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private GameHudPresenter presenter;
        [SerializeField] private HotSeatHost hotSeatHost;
        [SerializeField] private HotSeatOverlayView hotSeatOverlay;
        [SerializeField] private RevealSpotlightView revealSpotlight;
        [SerializeField] private EndScreenView endScreen;
        [SerializeField] private NetworkGameController networkController;
        [SerializeField] private MatchLauncher matchLauncher;

        private RoundPhase _lastOnlinePhase = (RoundPhase)(-1);

        /// <summary>True when this scene was loaded as part of a live network session.</summary>
        public static bool IsOnline
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && (nm.IsClient || nm.IsServer);
            }
        }

        private void Start()
        {
            ConfigureHints();

            if (endScreen != null) endScreen.MenuClicked += OnMenuFromEndScreen;

            if (IsOnline) StartOnline();
            else StartHotSeat();
        }

        /// <summary>Back to the menu from the standings; online, the session is left first.</summary>
        private async void OnMenuFromEndScreen()
        {
            if (GameServices.IsReady)
            {
                if (GameServices.Locator.TryGet<SessionManager>(out var session))
                    await session.LeaveSessionAsync();
                GameServices.Locator.Get<SceneFlowService>().LoadMainMenu();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu);
            }
        }

        /// <summary>
        /// Feeds the presenter the profile's seen-flags and persists dismissals. Game.UI cannot
        /// reference Game.Persistence, so this is where the two meet. Opening the Game scene
        /// directly in the editor has no service graph — hints then show but are not persisted.
        /// </summary>
        private void ConfigureHints()
        {
            if (presenter == null) return;

            if (GameServices.IsReady &&
                GameServices.Locator.TryGet<Game.Persistence.ISaveService>(out var save))
            {
                presenter.SetHintFlags(save.Profile.ShapeHintSeen, save.Profile.CommitHintSeen);
                presenter.HintDismissed += kind =>
                {
                    if (kind == Game.Core.RoundPhase.Shape) save.Profile.ShapeHintSeen = true;
                    else save.Profile.CommitHintSeen = true;
                    save.MarkDirty();
                };
            }
            else
            {
                presenter.SetHintFlags(false, false);
            }
        }

        private void StartHotSeat()
        {
            if (networkController != null) networkController.gameObject.SetActive(false);
            if (matchLauncher != null) matchLauncher.gameObject.SetActive(false);

            if (hotSeatHost == null)
            {
                Debug.LogError("[Foundry] GameSceneBootstrap has no HotSeatHost to start.");
                return;
            }

            hotSeatHost.enabled = true;
            hotSeatHost.StartMatch();
        }

        private void StartOnline()
        {
            // The handoff and reveal panels are a hot-seat device-passing idea; online, the server
            // clock drives the round and every player watches the same board at once.
            if (hotSeatOverlay != null) hotSeatOverlay.gameObject.SetActive(false);
            if (hotSeatHost != null) hotSeatHost.enabled = false;

            if (networkController == null || presenter == null)
            {
                Debug.LogError("[Foundry] GameSceneBootstrap is missing its controller or presenter.");
                return;
            }

            presenter.Bind(networkController, networkController);
            networkController.HostLost += OnHostLost;

            // Online, DONE is a real engine intent (#44): the server marks the seat done and closes
            // Shape early once everyone is. Hot-seat routes the same event to the director instead.
            presenter.DoneRequested += OnOnlineDone;

            // The reveal spotlight plays inside the server's Reveal window; the phase change
            // tears it down if the player out-waits the beats.
            networkController.Changed += OnOnlineSnapshot;

            // Online there is no handoff, so the board is always live for the local player. What
            // they may actually do is still gated by the rules engine on the server.
            presenter.SetContext(canAct: true, shapingAllowed: true);
            presenter.ShowMessage("Waiting for the match to start…");

            if (matchLauncher != null)
            {
                matchLauncher.gameObject.SetActive(true);

                // Only the host builds the match; clients receive it.
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer) matchLauncher.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (presenter != null) presenter.DoneRequested -= OnOnlineDone;

            if (networkController != null)
            {
                networkController.HostLost -= OnHostLost;
                networkController.Changed -= OnOnlineSnapshot;
            }
        }

        private void OnOnlineDone()
        {
            if (networkController != null) networkController.RequestDone();
        }

        private void OnOnlineSnapshot(MatchSnapshot snapshot)
        {
            if (revealSpotlight != null)
            {
                if (snapshot.Phase == RoundPhase.Reveal && _lastOnlinePhase != RoundPhase.Reveal)
                    revealSpotlight.Play(snapshot);
                else if (snapshot.Phase != RoundPhase.Reveal && revealSpotlight.IsOpen)
                    revealSpotlight.Hide();
            }

            // Online clients get a real end screen from the Standings projection. REMATCH stays
            // hot-seat-only for now — an online restart is a follow-up (logged).
            if (endScreen != null && snapshot.IsMatchOver && !endScreen.IsOpen)
            {
                endScreen.SetRematchVisible(false);
                endScreen.Show(snapshot);
            }

            _lastOnlinePhase = snapshot.Phase;
        }

        /// <summary>
        /// There is no host migration in this build (NET-4), so the match ends where it stands and
        /// the last standings this client received are shown. The alternative is leaving the player
        /// on a board that will never update again, which reads as a freeze rather than an ending.
        /// </summary>
        private void OnHostLost(Game.Core.MatchSnapshot lastKnown)
        {
            if (presenter != null)
            {
                presenter.SetContext(canAct: false, shapingAllowed: false);
                presenter.ShowMessage("The host left. Final standings below.");
            }

            if (hotSeatOverlay != null)
            {
                hotSeatOverlay.gameObject.SetActive(true);
                hotSeatOverlay.ShowAbandonedMatch(lastKnown);
            }
        }
    }
}
