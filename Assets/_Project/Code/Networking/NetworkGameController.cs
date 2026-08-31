using System;
using System.Collections.Generic;
using Game.Core;
using Unity.Netcode;
using Unity.Services.Authentication;
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
        private readonly SeatRegistry _seats = new SeatRegistry();

        /// <summary>Transport id to stable key, learned as clients announce themselves.</summary>
        private readonly Dictionary<ulong, string> _clientToKey = new Dictionary<ulong, string>();

        /// <summary>
        /// Transport id to chosen display name, learned the same way (STORY-4.3). Already
        /// sanitized: a name is untrusted input from a peer, and it is cleaned the moment it
        /// arrives rather than at each of the places that later read it. Empty means "no name
        /// chosen", which the seat default fills in at roster time.
        /// </summary>
        private readonly Dictionary<ulong, string> _clientToName = new Dictionary<ulong, string>();

        /// <summary>
        /// Every transport id that has sent <see cref="RegisterIdentityRpc"/>. An announcement is
        /// also proof the client's Game-scene controller has spawned — i.e. its NGO scene load
        /// finished — which is what the ready-up gate waits for (STORY-2.2).
        /// </summary>
        private readonly HashSet<ulong> _announcedClients = new HashSet<ulong>();

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
        public void ServerStartMatch(
            MatchState state,
            IDiceRoller roller,
            IReadOnlyList<ulong> orderedClientIds,
            IReadOnlyList<string> orderedSeatKeys = null)
        {
            if (!IsServer) { Debug.LogError("[Net] ServerStartMatch called on a non-server peer."); return; }

            _server = new LocalMatchSession(state, roller);
            _clientToPlayer.Clear();

            for (int i = 0; i < orderedClientIds.Count && i < state.Players.Count; i++)
            {
                var seat = state.Players[i].Id;
                _clientToPlayer[orderedClientIds[i]] = seat;

                // The stable key is what owns the seat across a reconnect, since a returning client
                // arrives with a brand new transport id. Prefer what the caller supplied, then what
                // the client announced on spawn, and only then fall back to the transport id —
                // which is safe but means reconnects will not resolve.
                string key = orderedSeatKeys != null && i < orderedSeatKeys.Count && !string.IsNullOrEmpty(orderedSeatKeys[i])
                    ? orderedSeatKeys[i]
                    : _clientToKey.TryGetValue(orderedClientIds[i], out var announced) && !string.IsNullOrEmpty(announced)
                        ? announced
                        : orderedClientIds[i].ToString();

                if (key == orderedClientIds[i].ToString())
                    Debug.LogWarning($"[Net] {seat} is bound to transport id {orderedClientIds[i]} — " +
                                     "no stable key was supplied or announced, so this seat cannot reconnect.");

                _seats.Bind(key, seat);
            }

            foreach (var kv in _clientToPlayer)
                AssignPlayerRpc(kv.Value.Value, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));

            _server.Advance();          // Roll -> Shape
            SchedulePhaseEnd();
            BroadcastState();
        }

        /// <summary>
        /// The ready-up gate (STORY-2.2): true once every connected client has announced itself via
        /// <see cref="RegisterIdentityRpc"/>, which it does the moment its Game-scene controller
        /// spawns. Starting before this races the announcements: seats fall back to transport-id
        /// keys (breaking reconnect), and a client still loading the scene would be sent its
        /// AssignPlayerRpc before the object exists on its end and never learn which seat is its own.
        /// </summary>
        public bool ServerRosterReady()
        {
            if (!IsServer) return false;

            // The inherited NetworkManager, never the Singleton: with several in-process peers
            // (host + clients in the PlayMode netcode suite) the Singleton is whichever manager
            // was created last, not necessarily ours.
            var nm = NetworkManager;
            if (nm == null) return false;

            foreach (ulong clientId in nm.ConnectedClientsIds)
                if (!_announcedClients.Contains(clientId))
                    return false;

            return true;
        }

        /// <summary>
        /// The connected clients in a deterministic order — ascending transport id, which puts the
        /// host first — each paired with its announced stable key, or the empty string where none
        /// arrived. This is what the launcher passes to <see cref="ServerStartMatch"/>, so seat
        /// assignment no longer depends on when each announcement happened to land.
        /// </summary>
        public void ServerBuildRoster(out ulong[] orderedClientIds, out string[] orderedSeatKeys,
            out string[] orderedNames)
        {
            var nm = NetworkManager;

            var ids = new List<ulong>(nm.ConnectedClientsIds);
            if (ids.Count == 0) ids.Add(nm.LocalClientId);   // a server-only editor run
            ids.Sort();

            orderedClientIds = ids.ToArray();
            orderedSeatKeys = new string[ids.Count];
            orderedNames = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                orderedSeatKeys[i] = _clientToKey.TryGetValue(ids[i], out var key) ? key : string.Empty;

                // Already sanitized on arrival; all that is left is the seat's own fallback for a
                // player who never chose a name, or never announced one (STORY-4.3 AC3).
                orderedNames[i] = PlayerName.Sanitize(
                    _clientToName.TryGetValue(ids[i], out var name) ? name : string.Empty, i);
            }
        }

        /// <summary>
        /// Server-only. The names the seats are playing under, in seat order — what a rematch
        /// rebuilds the table with so nobody loses their name at the round-10 boundary.
        /// </summary>
        public string[] ServerSeatNames()
        {
            if (!IsServer || _server == null) return System.Array.Empty<string>();

            var players = _server.State.Players;
            var names = new string[players.Count];
            for (int i = 0; i < players.Count; i++)
                names[i] = PlayerName.Sanitize(players[i].DisplayName, i);
            return names;
        }

        /// <summary>Seats in the running (or just-finished) match; zero when none has started.</summary>
        public int ServerSeatCount => _server != null ? _server.State.Players.Count : 0;

        /// <summary>
        /// Server-side: replaces the finished match with a fresh one on the same table (#42). The
        /// seat registry's key→seat bindings are kept verbatim — the rematch state must therefore
        /// have the same seat count and ids — so connected clients keep their seats, and a player
        /// who dropped during the previous endgame can still reclaim theirs through the normal
        /// reconnect path.
        /// </summary>
        public void ServerStartRematch(MatchState state, IDiceRoller roller)
        {
            if (!IsServer) { Debug.LogError("[Net] ServerStartRematch called on a non-server peer."); return; }
            if (_server == null) { Debug.LogError("[Net] No previous match to rematch."); return; }

            _server = new LocalMatchSession(state, roller);

            // A seat with nobody connected starts the rematch absent: the clock will not wait for
            // it (NET-3), and its reconnect window opens now rather than on a phantom drop later.
            foreach (var player in state.Players)
            {
                bool connected = false;
                foreach (var kv in _clientToPlayer)
                    if (kv.Value == player.Id) { connected = true; break; }

                if (!connected)
                {
                    RulesEngine.SetConnected(state, player.Id, false);
                    _seats.MarkDisconnected(player.Id, Time.time);
                }
            }

            foreach (var kv in _clientToPlayer)
                AssignPlayerRpc(kv.Value.Value, RpcTarget.Single(kv.Key, RpcTargetUse.Temp));

            _server.Advance();          // Roll -> Shape
            SchedulePhaseEnd();
            BroadcastState();
        }

        // ---------------- connection lifecycle ----------------

        public override void OnNetworkSpawn()
        {
            var nm = NetworkManager;
            if (nm == null) return;

            if (IsServer) nm.OnClientDisconnectCallback += OnServerSawClientLeave;
            else nm.OnClientDisconnectCallback += OnClientLostConnection;

            // Announce who we are. Before the match this is how the server learns each player's
            // stable key; after a drop, the very same message reclaims the seat.
            if (IsClient)
            {
                RegisterIdentityRpc(LocalSeatKey(), LocalDisplayName);
                _announcedName = LocalDisplayName;
            }
        }

        public override void OnNetworkDespawn()
        {
            var nm = NetworkManager;
            if (nm == null) return;

            nm.OnClientDisconnectCallback -= OnServerSawClientLeave;
            nm.OnClientDisconnectCallback -= OnClientLostConnection;

            // A reconnect is a new transport id and a server that has forgotten this client, so
            // the next spawn must announce again even if the name has not changed.
            _announcedName = null;
        }

        /// <summary>
        /// A player dropped. They keep their seat, cards and score; they simply stop being waited
        /// for, so the table carries on at full speed instead of burning a whole phase timer on a
        /// device that is not there (NET-3).
        /// </summary>
        private void OnServerSawClientLeave(ulong clientId)
        {
            // Forget the transport id whether or not a match is running yet — a returning client
            // arrives with a brand new one and re-announces.
            _announcedClients.Remove(clientId);
            _clientToKey.Remove(clientId);
            _clientToName.Remove(clientId);

            if (_server == null) return;
            if (!_clientToPlayer.TryGetValue(clientId, out var player)) return;

            _clientToPlayer.Remove(clientId);
            _seats.MarkDisconnected(player, Time.time);
            RulesEngine.SetConnected(_server.State, player, false);

            Debug.Log($"[Net] {player} dropped; holding their seat for {_seats.ReconnectWindowSeconds:0}s.");
            _broadcastPending = true;
        }

        /// <summary>
        /// The host went away. There is no host migration in this build, so the match ends where it
        /// stands and the last known standings are shown (NET-4) rather than the client hanging on a
        /// board that will never update again.
        /// </summary>
        private void OnClientLostConnection(ulong clientId)
        {
            var nm = NetworkManager;
            if (nm == null || clientId != NetworkManager.ServerClientId) return;

            HostLost?.Invoke(Current);
        }

        /// <summary>
        /// Raised on a client when the host disappears, carrying the last snapshot it received.
        /// </summary>
        public event Action<MatchSnapshot> HostLost;

        /// <summary>
        /// Test seam: hands each in-process peer a distinct stable key, since a test run has no
        /// UGS sign-in. Static out of necessity — the client-side controller is instantiated by
        /// the spawn message itself, leaving no window to inject per-instance — null in
        /// production, and cleared by the suite's teardown.
        /// </summary>
        internal static Func<NetworkManager, string> SeatKeyProviderForTests;

        /// <summary>
        /// The identity that survives a reconnect. The UGS authentication id is stable for the
        /// signed-in player, where the transport id is regenerated on every connection.
        /// </summary>
        private string LocalSeatKey()
        {
            var provider = SeatKeyProviderForTests;
            if (provider != null) return provider(NetworkManager) ?? string.Empty;

            try
            {
                return AuthenticationService.Instance.IsSignedIn
                    ? AuthenticationService.Instance.PlayerId
                    : string.Empty;
            }
            catch (Exception)
            {
                // Playing without UGS (a local NGO test, say). Reconnection then cannot resolve a
                // seat, which is a lost feature rather than a broken match.
                return string.Empty;
            }
        }

        /// <summary>
        /// The name this device's player chose, as it will be announced (STORY-4.3). Assigned by
        /// the composition root, which owns the profile — this assembly cannot see Game.Persistence
        /// and should not learn to.
        /// </summary>
        public string LocalDisplayName { get; private set; } = string.Empty;

        /// <summary>
        /// What the last announcement actually carried, or null if none has been sent. Null rather
        /// than empty so that "announced an empty name" is distinguishable from "never announced".
        /// </summary>
        private string _announcedName;

        /// <summary>
        /// Sets the local display name and announces it if that changes what the server was last
        /// told. Safe to call before or after <see cref="OnNetworkSpawn"/>, which is the point:
        /// the scene bootstrap and NGO's spawn race each other. The bootstrap sets the name in
        /// Awake, so the spawn's own announcement already carries it; this call is what covers
        /// the other ordering, and a missed name cannot be corrected once the match is built.
        ///
        /// The change check matters on reconnect: the spawn announcement is what reclaims the
        /// seat, and a second identical one would run that whole branch again — rebinding the
        /// seat and costing a full snapshot encode per recipient for nothing.
        /// </summary>
        public void AnnounceIdentity(string displayName)
        {
            LocalDisplayName = PlayerName.Sanitize(displayName, string.Empty);

            if (!IsSpawned || !IsClient) return;
            if (_announcedName == LocalDisplayName) return;

            RegisterIdentityRpc(LocalSeatKey(), LocalDisplayName);
            _announcedName = LocalDisplayName;
        }

        /// <summary>
        /// Announces who this client is. Sent on spawn, so the server knows every player's stable
        /// key and chosen name before the match starts, and sent again on rejoin, where the same
        /// message is what reclaims the seat.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RegisterIdentityRpc(string seatKey, string displayName, RpcParams rpc = default)
        {
            ulong clientId = rpc.Receive.SenderClientId;
            _announcedClients.Add(clientId);
            if (!string.IsNullOrEmpty(seatKey)) _clientToKey[clientId] = seatKey;

            // Untrusted: this string came off the wire and will be drawn on every other player's
            // device, so it is capped and stripped here, on the server, before it is stored
            // (AC4). A client that sanitizes on its own end has simply chosen to; it proves
            // nothing about the next one.
            _clientToName[clientId] = PlayerName.Sanitize(displayName, string.Empty);

            // Before the match starts there is no seat to take yet; the key is simply remembered
            // so ServerStartMatch can bind it.
            if (_server == null) return;

            if (!_seats.TryResolve(seatKey, out var player))
            {
                Debug.LogWarning("[Net] A client announced a key that owns no seat; ignoring.");
                return;
            }

            // Drop any stale binding for this seat before rebinding, so a seat is never owned by
            // two transport ids at once.
            foreach (var existing in new List<ulong>(_clientToPlayer.Keys))
                if (_clientToPlayer[existing] == player) _clientToPlayer.Remove(existing);

            _clientToPlayer[clientId] = player;
            _seats.MarkConnected(player);
            RulesEngine.SetConnected(_server.State, player, true);

            AssignPlayerRpc(player.Value, RpcTarget.Single(clientId, RpcTargetUse.Temp));

            Debug.Log($"[Net] {player} took their seat back.");
            _broadcastPending = true;
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
            var state = _server.State;

            // Reveal lasts long enough for the client's per-claim beats (UI-4); the count feeds
            // the same DurationOf the clients read from the config echo, so both ends agree.
            int revealClaims = state.Phase == RoundPhase.Reveal ? state.PendingClaimCount() : 0;

            _phaseEndsAt = Time.time + state.Config.DurationOf(state.Phase, revealClaims);
        }

        // ---------------- IGameActions (called by the UI on the local client) ----------------

        public void RequestShape(ShapeAction action) =>
            SubmitShapeRpc((int)action.Kind, action.DieIndex, action.Value);

        public void RequestCommit(CardId cardId, IReadOnlyList<int> diceIndices) =>
            SubmitCommitRpc(cardId.Value, ToArray(diceIndices));

        public void RequestPass() => SubmitPassRpc();

        public void RequestDone() => SubmitDoneRpc();

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
        private void SubmitDoneRpc(RpcParams rpc = default)
        {
            if (!TryAcceptIntent(rpc, out var player)) return;
            Resolve(_server.Done(player), rpc.Receive.SenderClientId);
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
                var snapshot = MatchSnapshot.For(_server.State, kv.Value, _seats, Time.time);
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

        /// <summary>
        /// The last state payload this peer received over the wire, before decoding. Test seam:
        /// the secrecy assertions (STORY-2.1 AC3) run against the actual bytes a client was sent,
        /// not against a locally rebuilt snapshot.
        /// </summary>
        internal byte[] LastStateBytes { get; private set; }

        [Rpc(SendTo.SpecifiedInParams)]
        private void StateRpc(byte[] snapshotBytes, float secondsLeft, RpcParams rpc = default)
        {
            if (!FromServer(rpc)) return;

            LastStateBytes = snapshotBytes;
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
