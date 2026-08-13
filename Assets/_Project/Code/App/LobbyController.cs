using Game.Networking;
using Game.UI;
using Unity.Netcode;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Presenter for the Lobby scene. Displays the join code and, on the host only, a Start button
    /// that loads the networked Game scene for everyone via NGO.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] private LobbyView view;

        private SessionManager _session;
        private SceneFlowService _sceneFlow;

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

            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            view.SetStartVisible(isHost);
            view.SetStatus(isHost ? "Share the code, then start when ready." : "Waiting for host to start…");

            view.StartClicked += OnStart;
        }

        private void OnDestroy()
        {
            if (view != null) view.StartClicked -= OnStart;
        }

        private void OnStart()
        {
            if (!_sceneFlow.LoadNetworkedGame())
                view.SetStatus("Can't start — not the host or no active session.");
        }
    }
}
