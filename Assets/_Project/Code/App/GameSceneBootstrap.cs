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
        [SerializeField] private SoloHost soloHost;
        [SerializeField] private HotSeatOverlayView hotSeatOverlay;
        [SerializeField] private RevealSpotlightView revealSpotlight;
        [SerializeField] private EndScreenView endScreen;
        [SerializeField] private NetworkGameController networkController;
        [SerializeField] private MatchLauncher matchLauncher;

        [Tooltip("Seconds a client waits for its first snapshot before deciding the match is not " +
                 "coming. Generous — it covers the host's own scene load and the ready-up gate's " +
                 "ten-second fallback — because the cost of being early is a false alarm on a slow " +
                 "network, and the cost of being late is only a few more seconds of waiting.")]
        [SerializeField] private float firstSnapshotTimeoutSeconds = 25f;

        private RoundPhase _lastOnlinePhase = (RoundPhase)(-1);

        private bool _awaitingFirstSnapshot;
        private float _firstSnapshotDeadline;

        /// <summary>True when this scene was loaded as part of a live network session.</summary>
        public static bool IsOnline
        {
            get
            {
                var nm = NetworkManager.Singleton;
                return nm != null && (nm.IsClient || nm.IsServer);
            }
        }

        /// <summary>
        /// Seeds the local display name before NGO can spawn anything (STORY-4.3).
        ///
        /// This runs during scene load, ahead of the controller's own OnNetworkSpawn, so the very
        /// first identity announcement already carries the name. That ordering matters: the same
        /// announcement is what clears the host's ready-up gate, and the host builds the match the
        /// moment it clears — a name that arrives one message later arrives after the seats have
        /// been named, and PlayerState.DisplayName is immutable by then.
        /// </summary>
        private void Awake()
        {
            if (IsOnline && networkController != null)
                networkController.AnnounceIdentity(LocalIdentity.RawDisplayName);
        }

        private void Start()
        {
            ConfigureHints();

            if (endScreen != null) endScreen.MenuClicked += OnMenuFromEndScreen;

            if (IsOnline)
            {
                StartOnline();
                return;
            }

            // A pending solo request (set by the menu, carried on the locator-registered flow
            // service — never a static) turns this local load into a bots match (STORY-7.1).
            int soloBots = GameServices.IsReady
                ? GameServices.Locator.Get<SceneFlowService>().ConsumeSoloRequest()
                : 0;

            if (soloBots > 0) StartSolo(soloBots);
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
            if (soloHost != null) soloHost.enabled = false;

            if (hotSeatHost == null)
            {
                Debug.LogError("[Foundry] GameSceneBootstrap has no HotSeatHost to start.");
                return;
            }

            hotSeatHost.enabled = true;
            hotSeatHost.StartMatch();
        }

        private void StartSolo(int botCount)
        {
            if (networkController != null) networkController.gameObject.SetActive(false);
            if (matchLauncher != null) matchLauncher.gameObject.SetActive(false);
            if (hotSeatHost != null) hotSeatHost.enabled = false;

            if (soloHost == null)
            {
                Debug.LogError("[Foundry] GameSceneBootstrap has no SoloHost to start.");
                return;
            }

            soloHost.enabled = true;
            soloHost.StartMatch(botCount);
        }

        private void StartOnline()
        {
            // The handoff and reveal panels are a hot-seat device-passing idea; online, the server
            // clock drives the round and every player watches the same board at once.
            if (hotSeatOverlay != null) hotSeatOverlay.gameObject.SetActive(false);
            if (hotSeatHost != null) hotSeatHost.enabled = false;
            if (soloHost != null) soloHost.enabled = false;

            if (networkController == null || presenter == null)
            {
                Debug.LogError("[Foundry] GameSceneBootstrap is missing its controller or presenter.");
                return;
            }

            presenter.Bind(networkController, networkController);
            networkController.HostLost += OnHostLost;

            // The correction path for the other ordering. Awake normally gets the name in before
            // the spawn announcement goes out; if the spawn somehow won, this fixes it, and if it
            // changed nothing AnnounceIdentity sends nothing. Set here rather than left to the
            // controller because only the composition root can see Game.Persistence.
            networkController.AnnounceIdentity(LocalIdentity.RawDisplayName);

            // Online, DONE is a real engine intent (#44): the server marks the seat done and closes
            // Shape early once everyone is. Hot-seat routes the same event to the director instead.
            presenter.DoneRequested += OnOnlineDone;

            // Host-only in practice: the button only renders on the server (see OnOnlineSnapshot).
            if (endScreen != null) endScreen.RematchClicked += OnOnlineRematch;

            // The reveal spotlight plays inside the server's Reveal window; the phase change
            // tears it down if the player out-waits the beats.
            networkController.Changed += OnOnlineSnapshot;

            // Online there is no handoff, so the board is always live for the local player. What
            // they may actually do is still gated by the rules engine on the server.
            presenter.SetContext(canAct: true, shapingAllowed: true);
            presenter.ShowMessage("Waiting for the match to start…");

            networkController.MatchUnavailable += OnMatchUnavailable;
            if (hotSeatOverlay != null) hotSeatOverlay.GameOverDismissed += OnMenuFromEndScreen;

            // Clients only. The host is the one that starts the match, so it cannot be waiting on
            // itself, and its own board fills in the moment it does.
            var manager = NetworkManager.Singleton;
            if (manager != null && !manager.IsServer)
            {
                _awaitingFirstSnapshot = true;
                _firstSnapshotDeadline = Time.time + firstSnapshotTimeoutSeconds;
            }

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
            if (endScreen != null) endScreen.RematchClicked -= OnOnlineRematch;
            if (hotSeatOverlay != null) hotSeatOverlay.GameOverDismissed -= OnMenuFromEndScreen;

            if (networkController != null)
            {
                networkController.HostLost -= OnHostLost;
                networkController.Changed -= OnOnlineSnapshot;
                networkController.MatchUnavailable -= OnMatchUnavailable;
            }
        }

        private void Update()
        {
            if (!_awaitingFirstSnapshot || Time.time < _firstSnapshotDeadline) return;

            // Nothing has arrived and nothing has said why. The server may be on another build, so
            // its messages never resolve against this one's objects and it cannot tell us — which
            // is precisely why this is decided locally rather than waited on.
            _awaitingFirstSnapshot = false;
            ShowMatchUnavailable(MatchUnavailableReason.NoResponse);
        }

        /// <summary>
        /// The server has told this client it cannot take part. Named on screen, with a way out —
        /// the alternative, and what shipped before this, is an empty board and a force-quit.
        /// </summary>
        private void OnMatchUnavailable(MatchUnavailableReason reason)
        {
            _awaitingFirstSnapshot = false;
            ShowMatchUnavailable(reason);
        }

        private void ShowMatchUnavailable(MatchUnavailableReason reason)
        {
            if (presenter != null)
            {
                presenter.SetContext(canAct: false, shapingAllowed: false);
                presenter.ShowMessage(string.Empty);
            }

            string title, body;
            switch (reason)
            {
                case MatchUnavailableReason.VersionMismatch:
                    title = "Different version";
                    body = "The host is running a different build of Foundry.\n\n" +
                           "Update from TestFlight — everyone at the table has to be on the same " +
                           "build for a match to work.";
                    break;

                case MatchUnavailableReason.NoSeat:
                    title = "Match already started";
                    body = "This match was already under way, so there was no seat left to take.\n\n" +
                           "Ask the host to start a new one.";
                    break;

                default:
                    title = "Couldn't reach the match";
                    body = "Nothing arrived from the host.\n\n" +
                           "The most likely cause is that one of you is on an older build — check " +
                           "TestFlight — or that the host left before the match began.";
                    break;
            }

            if (hotSeatOverlay != null)
            {
                hotSeatOverlay.gameObject.SetActive(true);
                hotSeatOverlay.ShowMatchUnavailable(title, body);
            }
            else
            {
                Debug.LogError($"[Foundry] Match unavailable ({reason}) and no overlay to say so.");
            }
        }

        private void OnOnlineDone()
        {
            if (networkController != null) networkController.RequestDone();
        }

        private void OnOnlineRematch()
        {
            if (endScreen != null) endScreen.Hide();
            if (matchLauncher != null) matchLauncher.ServerBeginRematch();
        }

        private void OnOnlineSnapshot(MatchSnapshot snapshot)
        {
            // The match is talking to us, so the watchdog has nothing left to watch for.
            _awaitingFirstSnapshot = false;

            if (revealSpotlight != null)
            {
                if (snapshot.Phase == RoundPhase.Reveal && _lastOnlinePhase != RoundPhase.Reveal)
                    revealSpotlight.Play(snapshot);
                else if (snapshot.Phase != RoundPhase.Reveal && revealSpotlight.IsOpen)
                    revealSpotlight.Hide();
            }

            // Online clients get a real end screen from the Standings projection. REMATCH is the
            // host's button (#42): only the server can rebuild the match; clients follow the fresh
            // snapshots it produces, so their standings dismiss themselves below.
            if (endScreen != null)
            {
                if (snapshot.IsMatchOver && !endScreen.IsOpen)
                {
                    var nm = NetworkManager.Singleton;
                    endScreen.SetRematchVisible(nm != null && nm.IsServer);
                    endScreen.Show(snapshot);
                }
                else if (!snapshot.IsMatchOver && endScreen.IsOpen)
                {
                    endScreen.Hide();   // the host called a rematch; the new board takes over
                }
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
