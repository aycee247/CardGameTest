using System.Collections.Generic;
using Game.Networking;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Presenter for the Lobby scene. Renders the join code and the live seat roster from the
    /// session, and on the host only, a Start button that loads the networked Game scene for
    /// everyone via NGO. Seats are named from the display name each player published as a session
    /// property (STORY-4.3), falling back to "PLAYER n" for anyone who never chose one.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private LobbyView view;

        private SessionManager _session;
        private SceneFlowService _sceneFlow;
        private readonly List<SeatEntry> _entries = new List<SeatEntry>();

        private void Start()
        {
            if (!GameServices.IsReady)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Boot);
                return;
            }

            _session = GameServices.Locator.Get<SessionManager>();
            _sceneFlow = GameServices.Locator.Get<SceneFlowService>();

            view.SetCode(_session.JoinCode);
            view.SetStatus(_session.IsHost
                ? "Share it — seats fill as friends arrive."
                : "Waiting for the host to start…");

            view.StartClicked += OnStart;
            view.BackClicked += OnBack;
            _session.RosterChanged += OnRoster;

            OnRoster();
        }

        private void OnDestroy()
        {
            if (view != null)
            {
                view.StartClicked -= OnStart;
                view.BackClicked -= OnBack;
            }

            if (_session != null) _session.RosterChanged -= OnRoster;
        }

        private void OnRoster()
        {
            var session = _session.CurrentSession;
            if (session == null) return;

            _entries.Clear();
            var players = session.Players;
            string localId = session.CurrentPlayer?.Id;

            for (int i = 0; i < players.Count; i++)
            {
                bool isHost = players[i].Id == session.Host;
                bool isLocal = players[i].Id == localId;
                // Uppercased for the rail's voice, not by the sanitizer — what was typed is what
                // is stored, and casing is a presentation decision.
                string name = SessionManager.DisplayNameOf(players[i], i).ToUpperInvariant() +
                              (isLocal ? " — YOU" : string.Empty);
                _entries.Add(new SeatEntry(name, isHost ? "HOST" : "READY", isLocal));
            }

            view.RenderSeats(_entries, _session.MaxPlayers);
            view.SetStartState(_entries.Count, _session.MaxPlayers, _session.IsHost);
        }

        private void OnStart()
        {
            if (!_sceneFlow.LoadNetworkedGame())
                view.SetStatus("Can't start — not the host or no active session.");
        }

        private async void OnBack()
        {
            await _session.LeaveSessionAsync();
            _sceneFlow.LoadMainMenu();
        }
    }
}
