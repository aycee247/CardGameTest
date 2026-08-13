using System;
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
    /// <see cref="GameState"/> from the <see cref="CardDatabase"/> and starts the networked match.
    /// Wire the "Start" flow (e.g. the Lobby's ready-up) to call this on the host only.
    /// </summary>
    public sealed class MatchLauncher : MonoBehaviour
    {
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private NetworkGameController gameController;

        [Header("Match config")]
        [SerializeField] private int dicePerPlayer = 6;
        [SerializeField] private int rollsPerTurn = 3;
        [SerializeField] private int marketSize = 5;
        [SerializeField] private int targetScore = 15;

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
                Debug.LogError("[Match] ServerBeginMatch must be called on the server/host.");
                return;
            }
            if (gameController == null || cardDatabase == null)
            {
                Debug.LogError("[Match] MatchLauncher is missing its controller or card database reference.");
                return;
            }

            var clientIds = new List<ulong>(nm.ConnectedClientsIds);
            if (clientIds.Count == 0) clientIds.Add(nm.LocalClientId);

            var players = new List<PlayerState>(clientIds.Count);
            for (int i = 0; i < clientIds.Count; i++)
                players.Add(new PlayerState(new PlayerId(i), $"Player {i + 1}"));

            var config = new GameConfig
            {
                DicePerPlayer = dicePerPlayer,
                RollsPerTurn = rollsPerTurn,
                MarketSize = marketSize,
                TargetScore = targetScore
            };

            var deck = cardDatabase.BuildDeck();
            var shuffleRng = new System.Random(unchecked((int)NewSeed()));
            Shuffle(deck, shuffleRng);

            var state = new GameState(config, players, deck);
            var roller = new SeededDiceRoller(NewSeed());

            gameController.ServerStartMatch(state, roller, clientIds);
        }

        // Server-owned entropy; clients never roll, so cross-platform determinism isn't required.
        private static ulong NewSeed() => (ulong)DateTime.UtcNow.Ticks ^ (ulong)Environment.TickCount;

        private static void Shuffle(IList<Card> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1); // 0..i
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
