using Game.Core;
using Game.Data;
using Game.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Host-side match orchestration, placed in the Game scene. On the server it waits behind a
    /// ready-up gate — every connected client's Game-scene controller must have spawned and
    /// announced its stable key — then <see cref="ServerBeginMatch"/> builds the authoritative
    /// <see cref="MatchState"/> from the <see cref="CardDatabase"/> and hands it to the networked
    /// controller with the seat keys in a deterministic order (STORY-2.2).
    ///
    /// The gate replaces starting blindly from <c>Start()</c>, which raced the clients' NGO scene
    /// loads: a late loader would have its seat bound to a transport id (so it could never
    /// reconnect) and could miss its seat assignment entirely.
    /// </summary>
    public sealed class MatchLauncher : MonoBehaviour
    {
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private NetworkGameController gameController;

        [Header("Match")]
        [SerializeField] private MatchConfig config = new MatchConfig();

        [Tooltip("Seconds the host waits for every client to finish loading and announce itself " +
                 "before starting anyway. The gate normally clears in well under a second; the " +
                 "timeout only stops one stuck device from holding the whole table hostage.")]
        [SerializeField] private float readyTimeoutSeconds = 10f;

        private bool _started;
        private float _startDeadline;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                enabled = false;
                return;
            }

            _startDeadline = Time.time + readyTimeoutSeconds;
        }

        private void Update()
        {
            if (_started || gameController == null) return;

            if (gameController.ServerRosterReady())
            {
                ServerBeginMatch();
                return;
            }

            if (Time.time >= _startDeadline)
            {
                Debug.LogWarning("[Foundry] Not every client announced itself in time; starting " +
                                 "anyway. Unannounced seats bind to transport ids and cannot reconnect.");
                ServerBeginMatch();
            }
        }

        /// <summary>
        /// Server-only. Rebuilds the just-finished table with a fresh seed and the same seats
        /// (#42) — same seat count, same keys, so reconnect keeps working across the boundary.
        /// </summary>
        public void ServerBeginRematch()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                Debug.LogError("[Foundry] ServerBeginRematch must be called on the server/host.");
                return;
            }

            if (gameController == null || cardDatabase == null)
            {
                Debug.LogError("[Foundry] MatchLauncher is missing its controller or card database.");
                return;
            }

            int seatCount = gameController.ServerSeatCount;
            if (seatCount == 0)
            {
                ServerBeginMatch();     // nothing to rematch; fall back to a fresh start
                return;
            }

            // The same players under the same names (STORY-4.3): a rematch that renamed everyone
            // back to "Player n" would read as a different table.
            var names = gameController.ServerSeatNames();

            int seed = MatchFactory.NewSeed();
            var state = MatchFactory.Build(config, cardDatabase, names, seed);
            var roller = new SeededDiceRoller(unchecked((ulong)seed));

            gameController.ServerStartRematch(state, roller);
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

            _started = true;

            // Names come from the roster, where each client announced its own and the server
            // sanitized it (STORY-4.3 AC4). A seat whose player never chose one gets the seat
            // default, so the rail is never blank.
            gameController.ServerBuildRoster(out var clientIds, out var seatKeys, out var names);

            int seed = MatchFactory.NewSeed();
            var state = MatchFactory.Build(config, cardDatabase, names, seed);
            var roller = new SeededDiceRoller(unchecked((ulong)seed));

            gameController.ServerStartMatch(state, roller, clientIds, seatKeys);
        }
    }
}
