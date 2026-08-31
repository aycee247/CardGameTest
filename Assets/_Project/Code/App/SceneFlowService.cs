using Game.Networking;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Owns scene transitions. Menu/lobby scenes are ordinary local loads; the networked Game scene
    /// is loaded through NGO's <see cref="NetworkSceneManager"/> by the server so it replicates to
    /// every connected client.
    /// </summary>
    public sealed class SceneFlowService
    {
        public void LoadMainMenu() => SceneManager.LoadSceneAsync(SceneNames.MainMenu, LoadSceneMode.Single);

        public void LoadLobby() => SceneManager.LoadSceneAsync(SceneNames.Lobby, LoadSceneMode.Single);

        public void LoadBoot() => SceneManager.LoadSceneAsync(SceneNames.Boot, LoadSceneMode.Single);

        /// <summary>
        /// Local (hot-seat) load of the Game scene. No NGO session is involved, so
        /// <see cref="GameSceneBootstrap"/> sees no live NetworkManager and starts the hot-seat
        /// match. Works with UGS entirely unavailable.
        /// </summary>
        public void LoadGame()
        {
            _pendingSoloBots = 0;
            SceneManager.LoadSceneAsync(SceneNames.Game, LoadSceneMode.Single);
        }

        // Carried here — instance state on the locator-registered service — rather than on a
        // static, which the project bans. The Game scene consumes it exactly once.
        private int _pendingSoloBots;

        /// <summary>
        /// Local load of the Game scene as a solo-vs-bots match (STORY-7.1). Like
        /// <see cref="LoadGame"/>, fully offline.
        /// </summary>
        public void LoadSoloGame(int botCount)
        {
            _pendingSoloBots = botCount < 1 ? 1 : botCount > 5 ? 5 : botCount;
            SceneManager.LoadSceneAsync(SceneNames.Game, LoadSceneMode.Single);
        }

        /// <summary>The requested bot count (0 = no solo request), cleared by the read.</summary>
        public int ConsumeSoloRequest()
        {
            int bots = _pendingSoloBots;
            _pendingSoloBots = 0;
            return bots;
        }

        /// <summary>
        /// Server-only: load the Game scene for all clients via NGO. Safe to call only once the
        /// NetworkManager is listening (a session is active and this peer is the server/host).
        /// </summary>
        public bool LoadNetworkedGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return false;

            // Close the door on the way through it. The seats are decided from who is connected
            // when the match is built, so a code that still works after this point only leads
            // someone to a table with no room for them.
            if (GameServices.IsReady && GameServices.Locator.TryGet<SessionManager>(out var session))
                _ = session.LockAsync();

            nm.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
            return true;
        }
    }
}
