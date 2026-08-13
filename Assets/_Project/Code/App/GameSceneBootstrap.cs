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
        [SerializeField] private NetworkGameController networkController;
        [SerializeField] private MatchLauncher matchLauncher;

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
            if (IsOnline) StartOnline();
            else StartHotSeat();
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
    }
}
