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
        /// Server-only: load the Game scene for all clients via NGO. Safe to call only once the
        /// NetworkManager is listening (a session is active and this peer is the server/host).
        /// </summary>
        public bool LoadNetworkedGame()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return false;
            nm.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
            return true;
        }
    }
}
