using System;
using Game.Networking;
using Game.Persistence;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Presenter that connects the <see cref="MainMenuView"/> to the online session services.
    /// Lives in the MainMenu scene. Host creates a session and shows the join code; Join connects
    /// by code. Both then move to the Lobby.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        [SerializeField] private HowToPlayView howToPlay;
        [SerializeField] private SettingsController settings;

        [Tooltip("Seats the hosted session allows. Simultaneous play means six costs no more " +
                 "wall-clock time than two.")]
        [Range(2, 6)]
        [SerializeField] private int maxPlayers = 6;

        [Tooltip("Opponents in a solo match (STORY-7.1). An in-menu picker waits on the settings " +
                 "screen; until then this is the one place to change it.")]
        [Range(1, 5)]
        [SerializeField] private int soloBotCount = 3;

        private SessionManager _session;
        private SceneFlowService _sceneFlow;

        private void Start()
        {
            if (!GameServices.IsReady)
            {
                // Entered this scene directly without going through Boot; send the player back.
                Debug.LogWarning("[Menu] Services not ready — returning to Boot.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Boot);
                return;
            }

            _session = GameServices.Locator.Get<SessionManager>();
            _sceneFlow = GameServices.Locator.Get<SceneFlowService>();

            view.HostClicked += OnHost;
            view.JoinClicked += OnJoin;
            view.PassPlayClicked += OnPassPlay;
            view.SoloClicked += OnSolo;
            view.NameChanged += OnNameChanged;
            view.HowToPlayClicked += OnHowToPlay;
            view.SettingsClicked += OnSettings;

            if (howToPlay != null)
            {
                howToPlay.Finished += OnOnboardingSeen;
                howToPlay.Skipped += OnOnboardingSeen;
                howToPlay.PlaySoloRequested += OnSolo;
            }

            // The menu is where identity is set, until the settings screen exists (STORY-4.1).
            view.SetName(LocalIdentity.RawDisplayName);

            string status = "Ready";
            if (GameServices.Locator.TryGet<BootStatus>(out var boot))
            {
                if (boot.OnlineFailed)
                    status = "Online unavailable — couldn't reach Unity services. " +
                             "Host/Join will retry; Pass & Play works offline.";
                // One-shot by contract: ConsumeProfileReset clears the flag (STORY-4.2 AC3).
                if (boot.ConsumeProfileReset())
                    status = "Saved settings couldn't be read and were reset to defaults. " + status;
            }
            view.SetStatus(status);

            // Last, so a brand-new player meets the explainer over a menu that has already
            // finished settling rather than one still resolving its own state.
            ShowOnboardingIfNewPlayer();
        }

        private void OnDestroy()
        {
            // Guards only the view's own handlers. It used to wrap the whole method, which after
            // the explainer landed meant a missing view silently skipped unsubscribing from it
            // too — harmless while both die in the same unload, but not what the guard says.
            if (view != null)
            {
                view.HostClicked -= OnHost;
                view.JoinClicked -= OnJoin;
                view.PassPlayClicked -= OnPassPlay;
                view.SoloClicked -= OnSolo;
                view.NameChanged -= OnNameChanged;
                view.HowToPlayClicked -= OnHowToPlay;
                view.SettingsClicked -= OnSettings;
            }

            if (howToPlay != null)
            {
                howToPlay.Finished -= OnOnboardingSeen;
                howToPlay.Skipped -= OnOnboardingSeen;
                howToPlay.PlaySoloRequested -= OnSolo;
            }
        }

        /// <summary>
        /// Opens the explainer the first time this player reaches the menu, and again if its
        /// revision has moved past what they last read (STORY-3.5). Without a profile — a scene
        /// opened straight from the editor — it stays shut rather than nagging on every load.
        /// </summary>
        private void ShowOnboardingIfNewPlayer()
        {
            if (howToPlay == null) return;
            if (!GameServices.Locator.TryGet<ISaveService>(out var save) || save.Profile == null) return;

            if (save.Profile.OnboardingSeenVersion < HowToPlayView.Version) howToPlay.Open();
        }

        private void OnHowToPlay() => howToPlay?.Open();

        private void OnSettings() => settings?.Open();

        /// <summary>
        /// Read or skipped, both of which mean "do not open this by itself again". Skipping is a
        /// decision, not a misfire — the player who skipped it has the HOW TO PLAY button right
        /// there, and re-prompting them every launch is nagging.
        /// </summary>
        private void OnOnboardingSeen()
        {
            if (!GameServices.Locator.TryGet<ISaveService>(out var save) || save.Profile == null) return;
            if (save.Profile.OnboardingSeenVersion >= HowToPlayView.Version) return;

            save.Profile.OnboardingSeenVersion = HowToPlayView.Version;
            save.MarkDirty();
        }

        /// <summary>
        /// Persists the chosen name and echoes back what was actually stored (STORY-4.3), so a
        /// player who pads it with spaces or overruns the cap sees the name their opponents will
        /// see rather than the one they typed.
        /// </summary>
        private void OnNameChanged(string raw)
        {
            LocalIdentity.SetDisplayName(raw);
            view.SetName(LocalIdentity.RawDisplayName);
        }

        private void OnPassPlay()
        {
            // Hot-seat is fully local (NET-5): no session, no UGS — just load the Game scene and
            // let GameSceneBootstrap start the pass-the-device match.
            view.SetInteractable(false);
            view.SetStatus("Starting pass & play…");
            _sceneFlow.LoadGame();
        }

        private void OnSolo()
        {
            // Solo is fully local like hot-seat (NET-5): the bots live in Core, so no session,
            // no UGS, and it works with online services entirely unavailable.
            view.SetInteractable(false);
            view.SetStatus("Starting solo match…");
            _sceneFlow.LoadSoloGame(soloBotCount);
        }

        private async void OnHost()
        {
            try
            {
                view.SetInteractable(false);
                view.SetStatus("Creating match…");
                var code = await _session.CreateSessionAsync(maxPlayers, LocalIdentity.RawDisplayName);
                view.SetJoinCode(code);
                view.SetStatus("Match created. Waiting for players…");
                _sceneFlow.LoadLobby();
            }
            catch (Exception e)
            {
                view.SetStatus($"Failed to host: {e.Message}");
                view.SetInteractable(true);
            }
        }

        private async void OnJoin(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                view.SetStatus("Enter a join code first.");
                return;
            }

            try
            {
                view.SetInteractable(false);
                view.SetStatus("Joining…");
                await _session.JoinSessionByCodeAsync(code, LocalIdentity.RawDisplayName);
                view.SetStatus("Joined. Entering lobby…");
                _sceneFlow.LoadLobby();
            }
            catch (Exception e)
            {
                view.SetStatus($"Failed to join: {e.Message}");
                view.SetInteractable(true);
            }
        }
    }
}
