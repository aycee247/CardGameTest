using System;
using Game.Networking;
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
        }

        private void OnDestroy()
        {
            if (view == null) return;
            view.HostClicked -= OnHost;
            view.JoinClicked -= OnJoin;
            view.PassPlayClicked -= OnPassPlay;
            view.SoloClicked -= OnSolo;
            view.NameChanged -= OnNameChanged;
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
