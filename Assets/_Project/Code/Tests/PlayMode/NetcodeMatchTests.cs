using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Game.Core;
using Game.Networking;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// STORY-2.1: the networked match, exercised over a real in-process NGO host and clients —
    /// intent validation, per-recipient secrecy asserted on the wire bytes, seat reclaim by key,
    /// and the FromServer guard against forged server→client RPCs.
    ///
    /// Uses NGO's own <see cref="NetcodeIntegrationTest"/> harness (the package is in the
    /// manifest's testables list for exactly this). Phase timers are set absurdly long so the
    /// server clock never advances a phase mid-assertion; every transition here is driven by the
    /// test or by AllDecided.
    /// </summary>
    [TestFixture(HostOrServer.Host)]
    internal class NetcodeMatchTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        public NetcodeMatchTests(HostOrServer hostOrServer) : base(hostOrServer) { }

        private const string HostKey = "seat-key-host";
        private const string ClientKeyPrefix = "seat-key-client-";

        private GameObject _controllerPrefab;
        private NetworkGameController _server;
        private readonly Dictionary<NetworkManager, string> _keys = new Dictionary<NetworkManager, string>();
        private string _nextClientKey = "seat-key-uninvited";

        protected override void OnServerAndClientsCreated()
        {
            _controllerPrefab = CreateNetworkObjectPrefab("GameController");
            _controllerPrefab.AddComponent<NetworkGameController>();

            // Each in-process peer gets a distinct stable key, standing in for the UGS auth id.
            _keys.Clear();
            _keys[m_ServerNetworkManager] = HostKey;
            for (int i = 0; i < m_ClientNetworkManagers.Length; i++)
                _keys[m_ClientNetworkManagers[i]] = ClientKeyPrefix + i;

            NetworkGameController.SeatKeyProviderForTests = nm =>
                _keys.TryGetValue(nm, out var key) ? key : _nextClientKey;

            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnTearDown()
        {
            NetworkGameController.SeatKeyProviderForTests = null;
            _server = null;
            return base.OnTearDown();
        }

        // ------------------------------------------------------------------ match bootstrap

        /// <summary>Spawns the controller everywhere, waits out the ready-up gate, starts a match.</summary>
        private IEnumerator StartMatch()
        {
            var serverGo = SpawnObject(_controllerPrefab, m_ServerNetworkManager);
            _server = serverGo.GetComponent<NetworkGameController>();

            yield return WaitForConditionOrTimeOut(() =>
                m_ClientNetworkManagers.All(nm => ControllerOn(nm) != null));
            AssertOnTimeout("the controller never spawned on every client");

            yield return WaitForConditionOrTimeOut(() => _server.ServerRosterReady());
            AssertOnTimeout("the ready-up gate never cleared (identity announcements missing)");

            _server.ServerBuildRoster(out var clientIds, out var seatKeys);
            _server.ServerStartMatch(BuildState(clientIds.Length), new SeededDiceRoller(4242), clientIds, seatKeys);

            yield return WaitForConditionOrTimeOut(() =>
                AllPeers().All(c => c.Current.Players != null && c.Current.Phase == RoundPhase.Shape));
            AssertOnTimeout("not every peer received the opening snapshot");
        }

        /// <summary>
        /// Fixed, testable rules: any two dice pay for any card, and the phase clock is frozen so
        /// only the test (or AllDecided) advances the round.
        /// </summary>
        private static MatchState BuildState(int seats)
        {
            var config = new MatchConfig
            {
                Rounds = 3,
                MarketSize = 3,
                StartingDice = 4,
                ShapeSeconds = 100000,
                CommitSeconds = 100000,
                RepickSeconds = 100000
            };

            var players = new List<PlayerState>();
            for (int i = 0; i < seats; i++)
                players.Add(new PlayerState(new PlayerId(i), "P" + i, i));

            var deck = new List<Card>();
            for (int id = 1; id <= 6; id++)
                deck.Add(new Card(new CardId(id), "Card" + id, new SumRequirement(2), points: id));

            return new MatchState(config, players, deck);
        }

        private static NetworkGameController ControllerOn(NetworkManager nm)
        {
            foreach (var obj in nm.SpawnManager.SpawnedObjects.Values)
            {
                var controller = obj.GetComponent<NetworkGameController>();
                if (controller != null) return controller;
            }
            return null;
        }

        /// <summary>Every peer's controller instance — the host's plus one per connected client.</summary>
        private List<NetworkGameController> AllPeers()
        {
            var peers = new List<NetworkGameController> { _server };
            foreach (var nm in m_ClientNetworkManagers)
            {
                var c = ControllerOn(nm);
                if (c != null) peers.Add(c);
            }
            return peers;
        }

        private static string Payload(NetworkGameController controller) =>
            controller.LastStateBytes == null ? string.Empty : Encoding.UTF8.GetString(controller.LastStateBytes);

        // ------------------------------------------------------------------ AC1

        [UnityTest]
        public IEnumerator HostAndTwoClientsStartAMatchWithDistinctSeats()
        {
            yield return StartMatch();

            var seats = AllPeers().Select(c => c.LocalPlayer.Value).ToList();
            Assert.AreEqual(3, seats.Count);
            CollectionAssert.AllItemsAreUnique(seats, "every peer must be assigned its own seat");
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, seats);
        }

        // ------------------------------------------------------------------ AC2

        [UnityTest]
        public IEnumerator AnIllegalIntentIsRejectedAndMutatesNothing()
        {
            yield return StartMatch();

            var client = ControllerOn(m_ClientNetworkManagers[0]);
            var facesBefore = client.Current.Players.Select(p => p.DiceFaces.ToArray()).ToArray();

            MoveFailure? rejection = null;
            client.MoveRejected += f => rejection = f;

            client.RequestShape(ShapeAction.Reroll(99));   // no such die

            yield return WaitForConditionOrTimeOut(() => rejection.HasValue);
            AssertOnTimeout("the illegal intent was never answered with a rejection");

            Assert.AreEqual(MoveFailure.NoSuchDie, rejection.Value);

            // A rejected intent must not have replicated any change: same dice, everywhere.
            var facesAfter = client.Current.Players.Select(p => p.DiceFaces.ToArray()).ToArray();
            for (int i = 0; i < facesBefore.Length; i++)
                CollectionAssert.AreEqual(facesBefore[i], facesAfter[i]);
        }

        // ------------------------------------------------------------------ AC3

        [UnityTest]
        public IEnumerator StateBytesNeverCarryAnOpponentsCommit()
        {
            yield return StartMatch();

            var committer = ControllerOn(m_ClientNetworkManagers[0]);
            var opponent = ControllerOn(m_ClientNetworkManagers[1]);

            committer.RequestCommit(new CardId(1), new[] { 0, 1 });

            yield return WaitForConditionOrTimeOut(() =>
                committer.Current.Observer.PendingCardId == 1 &&
                opponent.Current.Players.First(p => p.PlayerId == committer.LocalPlayer.Value).HasDecided);
            AssertOnTimeout("the commit was never acknowledged and re-broadcast");

            // The standard SecrecyGateTests set: assert on the actual bytes each client received.
            StringAssert.Contains("\"PendingCardId\":1", Payload(committer),
                "the committer's own view must carry their claim");
            StringAssert.DoesNotContain("\"PendingCardId\":1", Payload(opponent),
                "an opponent's bytes must never contain the claimed card before Reveal");
            StringAssert.DoesNotContain("\"PendingDice\":[0,1]", Payload(opponent),
                "an opponent's bytes must never contain the dice backing the claim");
        }

        // ------------------------------------------------------------------ AC4 + AC5

        [UnityTest]
        public IEnumerator ADroppedPlayerReclaimsTheirSeatByKeyWithARedactedView()
        {
            yield return StartMatch();

            // The host commits secretly, so the rejoiner's first view has something to redact.
            _server.RequestCommit(new CardId(2), new[] { 0, 1 });
            yield return WaitForConditionOrTimeOut(() => _server.Current.Observer.PendingCardId == 2);
            AssertOnTimeout("the host's commit was never acknowledged");

            var dropped = m_ClientNetworkManagers[1];
            int droppedSeat = ControllerOn(dropped).LocalPlayer.Value;
            string droppedKey = _keys[dropped];

            var watcher = ControllerOn(m_ClientNetworkManagers[0]);
            yield return StopOneClient(dropped, destroy: true);

            yield return WaitForConditionOrTimeOut(() =>
                watcher.Current.Players.First(p => p.PlayerId == droppedSeat).Status == SeatStatus.Reconnecting);
            AssertOnTimeout("the table never learned the player dropped");

            // The same key walks back in through the completely normal join path (AC5)...
            _nextClientKey = droppedKey;
            yield return CreateAndStartNewClient();

            var rejoinedManager = m_ClientNetworkManagers[m_ClientNetworkManagers.Length - 1];
            NetworkGameController rejoined = null;
            yield return WaitForConditionOrTimeOut(() =>
                (rejoined = ControllerOn(rejoinedManager)) != null &&
                rejoined.Current.Players != null &&
                rejoined.LocalPlayer.Value == droppedSeat);
            AssertOnTimeout("the returning client never reclaimed its seat by key");

            yield return WaitForConditionOrTimeOut(() =>
                watcher.Current.Players.First(p => p.PlayerId == droppedSeat).Status == SeatStatus.Connected);
            AssertOnTimeout("the table never saw the seat come back");

            // ...and the mid-match view it receives is correctly redacted (AC4): the host's
            // pending claim is absent from the rejoiner's wire bytes.
            StringAssert.DoesNotContain("\"PendingCardId\":2", Payload(rejoined),
                "a mid-match join must not leak another player's pending commit");
            Assert.AreEqual(RoundPhase.Shape, rejoined.Current.Phase);
        }

        // ------------------------------------------------------------------ AC6

        [UnityTest]
        public IEnumerator AForgedServerRpcFromAPeerIsIgnored()
        {
            yield return StartMatch();

            var attacker = ControllerOn(m_ClientNetworkManagers[0]);
            var victim = ControllerOn(m_ClientNetworkManagers[1]);
            ulong victimClientId = m_ClientNetworkManagers[1].LocalClientId;

            int roundBefore = victim.Current.Round;

            // A forged snapshot claiming round 77. Nothing in the transport stops a peer from
            // addressing a server->client RPC at another client, so FromServer has to.
            var forged = SnapshotCodec.Encode(new MatchSnapshot { Round = 77 });

            var stateRpc = typeof(NetworkGameController)
                .GetMethod("StateRpc", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(stateRpc, "StateRpc not found — was it renamed?");

            RpcParams target = attacker.RpcTarget.Single(victimClientId, RpcTargetUse.Temp);
            stateRpc.Invoke(attacker, new object[] { forged, 5f, target });

            // Give the message every chance to arrive and be (rightly) discarded.
            for (int i = 0; i < 30; i++) yield return null;

            Assert.AreEqual(roundBefore, victim.Current.Round,
                "a state forgery from a non-server peer must never reach the view");
            Assert.AreNotEqual(77, victim.Current.Round);
        }
    }
}
