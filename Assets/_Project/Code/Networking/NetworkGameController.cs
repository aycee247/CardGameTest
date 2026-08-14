using System;
using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>
    /// Host-authoritative match controller. One instance is spawned as a NetworkObject; it runs on
    /// every peer but only the server owns and mutates the authoritative <see cref="MatchState"/>.
    ///
    /// Authority model:
    ///  - Clients send INTENT only — shape, commit, pass, withdraw. They never send dice values and
    ///    never advance a phase.
    ///  - The server validates every intent through <see cref="RulesEngine"/>, owns the phase clock,
    ///    and replicates the result.
    ///  - Replication is <b>per recipient</b>. Each client receives a snapshot built for it alone,
    ///    so a pending commit never reaches anyone but its owner before Reveal (NET-2). Building one
    ///    snapshot and broadcasting it would hand every client its opponents' claims and make each
    ///    contested card a formality.
    ///
    /// The phase clock reuses <see cref="LocalMatchSession"/> as the server-side driver, so the
    /// online match steps through exactly the same transitions the headless tests cover.
    /// </summary>
    public sealed class NetworkGameController : NetworkBehaviour, IGameActions, IMatchView
    {
        /// <summary>Seconds the automatic phases linger, so players can read what happened.</summary>
        private const float RollBeatSeconds = 1.5f;
        private const float RevealBeatSeconds = 4f;
        private const float UpkeepBeatSeconds = 2.5f;

        /// <summary>
        /// Intent budget per player. Generous enough that real play never touches it — selecting
        /// eight dice and pressing a shape button sends eight intents in one frame — while bounding
        /// a flood to a rate the server does not care about.
        /// </summary>
        private const float IntentBurst = 24f;
        private const float IntentsPerSecond = 12f;

        // ---- Server-only authoritative state ----
        private LocalMatchSession _server;
        private readonly Dictionary<ulong, PlayerId> _clientToPlayer = new Dictionary<ulong, PlayerId>();
        private readonly IntentLimiter _intents = new IntentLimiter(IntentBurst, IntentsPerSecond);
        private float _phaseEndsAt;

        /// <summary>
        /// Set when state has changed and a broadcast is owed. Several intents commonly land in one
        /// frame, and clients have no use for the intermediate states, so they are collapsed into a
        /// single replication at end of frame. Without this one tap on eight dice costs eight
        /// snapshot encodes per recipient.
        /// </summary>
        private bool _broadcastPending;

        // ---- Client-side view state ----
        private PlayerId _localPlayer;

        public PlayerId LocalPlayer => _localPlayer;
        public MatchSnapshot Current { get; private set; }

        /// <summary>Seconds left in the current phase, for the countdown (UI-2).</summary>
        public float SecondsLeft { get; private set; }

        public event Action<MatchSnapshot> Changed;
        public event Action<MoveFailure> MoveRejected;

        /// <summary>
        /// Server-side: install the freshly built match and the seeded roller, map connected clients
        /// to seats, then start round one. Call once, on the server, after all players have joined.
        /// </summary>
        public void ServerStartMatch(MatchState state, IDiceRoller roller, IReadOnlyList<ulong> orderedClientIds)
        {
            if (!IsServer) { Debug.LogError("[Net] ServerStartMatch called on a non-server peer."); return; }

            _server = new LocalMatchSession(state, roller);
            _clientToPlayer.Clear();

            for (int i = 0; i < orderedClientIds.Count && i < state.Players.Count; i++)
                _clientToPlayer[orderedClientIds[i]] = state.Players[i].Id;

            foreach (var kv in _clientToPlayer)
                AssignPlayerRpc(kv.Value.Value, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));

            _server.Advance();          // Roll -> Shape
            SchedulePhaseEnd();
            BroadcastState();
        }

        // ---------------- server phase clock ----------------

        private void Update()
        {
            // Clients only hear the remaining time when a snapshot arrives, which is far less often
            // than once a frame, so the countdown is ticked down locally between messages and
            // corrected by the next one.
            if (SecondsLeft > 0f) SecondsLeft = Mathf.Max(0f, SecondsLeft - Time.deltaTime);

            if (!IsServer || _server == null) return;

            var phase = _server.State.Phase;
            if (phase == RoundPhase.MatchOver) return;

            bool expired = Time.time >= _phaseEndsAt;

            // Close a decision window early once nobody has anything left to decide, rather than
            // making the whole table wait out a clock that cannot change the outcome.
            bool settled = RulesEngine.AllDecided(_server.State);

            if (!expired && !settled) return;

            _server.Advance();
            SchedulePhaseEnd();
            _broadcastPending = true;
        }

        /// <summary>
        /// Flushes at most one broadcast per frame. Everything that mutates state marks the flag
        /// rather than replicating immediately, so a burst of intents landing together costs one
        /// round of snapshot encodes instead of one per intent.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsServer || _server == null || !_broadcastPending) return;

            _broadcastPending = false;
            BroadcastState();
        }

        private void SchedulePhaseEnd()
        {
            var config = _server.State.Config;
            float duration;

            switch (_server.State.Phase)
            {
                case RoundPhase.Roll: duration = RollBeatSeconds; break;
                case RoundPhase.Shape: duration = config.ShapeSeconds; break;
                case RoundPhase.Commit: duration = config.CommitSeconds; break;
                case RoundPhase.Reveal: duration = RevealBeatSeconds; break;
                case RoundPhase.Repick: duration = config.RepickSeconds; break;
                case RoundPhase.Upkeep: duration = UpkeepBeatSeconds; break;
                default: duration = float.PositiveInfinity; break;
            }

            _phaseEndsAt = Time.time + duration;
        }

        // ---------------- IGameActions (called by the UI on the local client) ----------------

        public void RequestShape(ShapeAction action) =>
            SubmitShapeRpc((int)action.Kind, action.DieIndex, action.Value);

        public void RequestCommit(CardId cardId, IReadOnlyList<int> diceIndices) =>
            SubmitCommitRpc(cardId.Value, ToArray(diceIndices));

        public void RequestPass() => SubmitPassRpc();

        public void RequestWithdraw() => SubmitWithdrawRpc();

        private static int[] ToArray(IReadOnlyList<int> source)
        {
            if (source == null) return Array.Empty<int>();
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
            return copy;
        }

        // ---------------- Client -> Server intent ----------------

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitShapeRpc(int kind, int dieIndex, int value, RpcParams rpc = default)
        {
            if (!TryAcceptIntent(rpc, out var player)) return;

            ShapeAction action;
            switch ((ShapeActionKind)kind)
            {
                case ShapeActionKind.Reroll: action = ShapeAction.Reroll(dieIndex); break;
                case ShapeActionKind.Nudge: action = ShapeAction.Nudge(dieIndex, value); break;
                case ShapeActionKind.SetFace: action = ShapeAction.SetFace(dieIndex, value); break;
                default: return;
            }

            Resolve(_server.Shape(player, action), rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitCommitRpc(int cardId, int[] diceIndices, RpcParams rpc = default)
        {
            if (!TryAcceptIntent(rpc, out var player)) return;
            Resolve(_server.Commit(player, new CardId(cardId), diceIndices), rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitPassRpc(RpcParams rpc = default)
        {
            if (!TryAcceptIntent(rpc, out var player)) return;
            Resolve(_server.Pass(player), rpc.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void SubmitWithdrawRpc(RpcParams rpc = default)
        {
            if (!TryAcceptIntent(rpc, out var player)) return;
            Resolve(_server.Withdraw(player), rpc.Receive.SenderClientId);
        }

        /// <summary>
        /// Gate every intent goes through: resolve the sender's seat, then charge their budget.
        ///
        /// Seat resolution comes first deliberately. It bounds the limiter to actual players, so a
        /// peer that is not in the match cannot make the server allocate a bucket per fake identity.
        /// </summary>
        private bool TryAcceptIntent(RpcParams rpc, out PlayerId player)
        {
            player = default;
            if (_server == null) return false;

            if (!_clientToPlayer.TryGetValue(rpc.Receive.SenderClientId, out player)) return false;

            if (_intents.TryConsume(player, Time.time)) return true;

            // Dropped silently: answering would cost a message per dropped intent, which is exactly
            // the amplification being defended against.
            int dropped = _intents.DroppedFor(player);
            if (dropped == 1 || dropped % 100 == 0)
                Debug.LogWarning($"[Net] Throttling {player} — {dropped} intents dropped.");

            return false;
        }

        private void Resolve(MoveResult result, ulong senderClientId)
        {
            if (result.Success) _broadcastPending = true;
            else RejectRpc((int)result.Failure, RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
        }

        // ---------------- Server -> Clients replication ----------------

        /// <summary>
        /// Sends every client its own filtered view. One encode per recipient is the price of
        /// hidden information, and it is not optional.
        /// </summary>
        private void BroadcastState()
        {
            float secondsLeft = Mathf.Max(0f, _phaseEndsAt - Time.time);

            foreach (var kv in _clientToPlayer)
            {
                var snapshot = MatchSnapshot.For(_server.State, kv.Value);
                StateRpc(SnapshotCodec.Encode(snapshot), secondsLeft, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));
            }
        }

        /// <summary>
        /// These three travel server-to-client. Nothing in the transport stops a hostile peer from
        /// sending one directly to another client, so each verifies the sender really is the server
        /// before acting — otherwise a client could forge state, reassign someone's seat, or fake a
        /// rejection.
        /// </summary>
        private bool FromServer(RpcParams rpc) =>
            rpc.Receive.SenderClientId == NetworkManager.ServerClientId;

        [Rpc(SendTo.SpecifiedInParams)]
        private void StateRpc(byte[] snapshotBytes, float secondsLeft, RpcParams rpc = default)
        {
            if (!FromServer(rpc)) return;

            Current = SnapshotCodec.Decode(snapshotBytes);
            SecondsLeft = secondsLeft;
            Changed?.Invoke(Current);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignPlayerRpc(int playerId, RpcParams rpc = default)
        {
            if (!FromServer(rpc)) return;
            _localPlayer = new PlayerId(playerId);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void RejectRpc(int failure, RpcParams rpc = default)
        {
            if (!FromServer(rpc)) return;
            MoveRejected?.Invoke((MoveFailure)failure);
        }
    }
}
