using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Host-side match orchestration, placed in the Game scene. Once the scene has loaded on the
    /// server and players are connected, <see cref="ServerBeginMatch"/> builds the authoritative
    /// <see cref="MatchState"/> from the <see cref="CardDatabase"/> and hands it to the networked
    /// controller. Wire the Lobby's "Start" flow to call this on the host only.
    /// </summary>
    public sealed class MatchLauncher : MonoBehaviour
    {
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private NetworkGameController gameController;

        [Header("Match")]
        [SerializeField] private MatchConfig config = new MatchConfig();

        [Tooltip("If true, the host begins the match automatically when this scene loads. " +
                 "Turn off if you want an explicit ready-up step instead.")]
        [SerializeField] private bool autoStartOnServer = true;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (autoStartOnServer && nm != null && nm.IsServer)
                ServerBeginMatch();
        }

        /// <summary>Server-only. Builds and starts the match for all connected clients.</summary>
        public void ServerBeginMatch()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                Debug.LogError("[Foundry] ServerBeginMatch must be called on the server/host.");
                return;
            }

            if (gameController == null || cardDatabase == null)
            {
                Debug.LogError("[Foundry] MatchLauncher is missing its controller or card database.");
                return;
            }

            var clientIds = new List<ulong>(nm.ConnectedClientsIds);
            if (clientIds.Count == 0) clientIds.Add(nm.LocalClientId);

            var names = new List<string>(clientIds.Count);
            for (int i = 0; i < clientIds.Count; i++) names.Add($"Player {i + 1}");

            int seed = MatchFactory.NewSeed();
            var state = MatchFactory.Build(config, cardDatabase, names, seed);
            var roller = new SeededDiceRoller(unchecked((ulong)seed));

            gameController.ServerStartMatch(state, roller, clientIds);
        }
    }
}
