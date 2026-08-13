using System;
using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-authoritative match controller. One instance is spawned as a NetworkObject; it runs
    /// on every peer but only the server owns and mutates the authoritative <see cref="GameState"/>.
    ///
    /// Authority model:
    ///  - Clients send INTENT only (roll / claim / end-turn) via <see cref="IGameActions"/>, which
    ///    forwards to server RPCs. Clients never send dice values.
    ///  - The server validates turn ownership + phase through <see cref="RulesEngine"/>, performs
    ///    the authoritative dice roll with a seeded roller, mutates state, then broadcasts a
    ///    <see cref="GameStateSnapshot"/> to everyone. Clients render snapshots; they are pure views.
    ///
    /// This class implements the same <see cref="IGameActions"/>/<see cref="IGameStateView"/> the
    /// offline <see cref="LocalGameSession"/> does, so the UI is identical online and offline.
    /// </summary>
    public sealed class NetworkGameController : NetworkBehaviour, IGameActions, IGameStateView
    {
        // ---- Server-only authoritative state ----
        private GameState _state;
        private IDiceRoller _roller;
        private readonly Dictionary<ulong, PlayerId> _clientToPlayer = new Dictionary<ulong, PlayerId>();

        // ---- Client-side view state ----
        private PlayerId _localPlayer;
        public PlayerId LocalPlayer => _localPlayer;
        public GameStateSnapshot Current { get; private set; }
        public event Action<GameStateSnapshot> Changed;
        public event Action<MoveFailure> MoveRejected;

        /// <summary>
        /// Server-side: install the freshly built match state and the seeded roller, map connected
        /// clients to player ids, then broadcast the opening snapshot. Call once, on the server,
        /// after all players have joined and the Game scene has loaded.
        /// </summary>
        public void ServerStartMatch(GameState state, IDiceRoller roller, IReadOnlyList<ulong> orderedClientIds)
        {
            if (!IsServer) { Debug.LogError("[Net] ServerStartMatch called on a non-server peer."); return; }

            _state = state;
            _roller = roller;
            _clientToPlayer.Clear();
            for (int i = 0; i < orderedClientIds.Count && i < state.Players.Count; i++)
                _clientToPlayer[orderedClientIds[i]] = state.Players[i].Id;

            // Tell each client which player it controls.
            foreach (var kv in _clientToPlayer)
                AssignPlayerRpc(kv.Value.Value, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));

            BroadcastState();
        }

        // ---------------- IGameActions (called by UI on the local client) ----------------

        public void RequestRoll() => SubmitRollRpc();
        public void RequestClaim(CardId cardId) => SubmitClaimRpc(cardId.Value);
        public void RequestEndTurn() => SubmitEndTurnRpc();

        // ---------------- Client -> Server intent ----------------

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitRollRpc(RpcParams rpc = default)
        {
            if (!TryResolvePlayer(rpc, out var player)) return;
            var result = RulesEngine.ApplyRoll(_state, new RollCommand(player), _roller, out _);
            Resolve(result, rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitClaimRpc(int cardId, RpcParams rpc = default)
        {
            if (!TryResolvePlayer(rpc, out var player)) return;
            var result = RulesEngine.Claim(_state, new ClaimCardCommand(player, new CardId(cardId)));
            Resolve(result, rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitEndTurnRpc(RpcParams rpc = default)
        {
            if (!TryResolvePlayer(rpc, out var player)) return;
            var result = RulesEngine.EndTurn(_state, new EndTurnCommand(player));
            Resolve(result, rpc.Receive.SenderClientId);
        }

        private bool TryResolvePlayer(RpcParams rpc, out PlayerId player)
        {
            return _clientToPlayer.TryGetValue(rpc.Receive.SenderClientId, out player);
        }

        private void Resolve(MoveResult result, ulong senderClientId)
        {
            if (result.Success) BroadcastState();
            else RejectRpc((int)result.Failure, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
        }

        // ---------------- Server -> Clients replication ----------------

        private void BroadcastState()
        {
            // Each observer gets claimability computed against their own roll, so we build and send
            // per-player snapshots addressed individually.
            foreach (var kv in _clientToPlayer)
            {
                var snapshot = GameStateSnapshot.From(_state, kv.Value);
                var bytes = SnapshotCodec.Encode(snapshot);
                StateRpc(bytes, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void StateRpc(byte[] snapshotBytes, RpcParams rpc = default)
        {
            Current = SnapshotCodec.Decode(snapshotBytes);
            Changed?.Invoke(Current);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignPlayerRpc(int playerId, RpcParams rpc = default)
        {
            _localPlayer = new PlayerId(playerId);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void RejectRpc(int failure, RpcParams rpc = default)
        {
            MoveRejected?.Invoke((MoveFailure)failure);
        }
    }
}
