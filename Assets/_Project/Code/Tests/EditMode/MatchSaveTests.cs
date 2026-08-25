using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Save/resume and replay determinism (STORY-6.5): the roller restores mid-sequence, the
    /// portable shuffle replays from a seed, and a captured match rehydrates exactly — then plays
    /// forward identically to one that was never interrupted.
    /// </summary>
    public class MatchSaveTests
    {
        // ------------------------------------------------------------------ roller (AC1)

        [Test]
        public void RollerRestoresMidSequence()
        {
            var original = new SeededDiceRoller(1234);
            original.Roll(8);
            original.Roll(8);

            var resumed = SeededDiceRoller.FromState(original.State);

            CollectionAssert.AreEqual(Faces(original.Roll(8)), Faces(resumed.Roll(8)));
            CollectionAssert.AreEqual(Faces(original.Roll(4)), Faces(resumed.Roll(4)));
        }

        // ------------------------------------------------------------------ shuffle (AC3)

        [Test]
        public void ShuffleReplaysFromASeed()
        {
            var a = MakeRange(10);
            var b = MakeRange(10);

            var rngA = new XorShift64Star(42);
            var rngB = new XorShift64Star(42);
            rngA.Shuffle(a);
            rngB.Shuffle(b);

            CollectionAssert.AreEqual(a, b);

            var c = MakeRange(10);
            var rngC = new XorShift64Star(43);
            rngC.Shuffle(c);
            CollectionAssert.AreNotEqual(a, c, "a different seed should deal a different order");
        }

        /// <summary>
        /// The algorithm is a compatibility contract: this exact output for this exact seed,
        /// forever. If this test breaks, every recorded seed and save in the wild breaks with it —
        /// fix the regression, never the expectation.
        /// </summary>
        [Test]
        public void ShuffleGoldenSequenceIsFrozen()
        {
            var list = MakeRange(10);
            var rng = new XorShift64Star(42);
            rng.Shuffle(list);

            CollectionAssert.AreEqual(new[] { 1, 4, 3, 8, 9, 2, 7, 6, 5, 0 }, list);
        }

        // ------------------------------------------------------------------ round trip (AC2)

        [Test]
        public void CaptureRestoreCaptureIsByteStable()
        {
            var (state, roller, resolver) = MidMatch();

            byte[] first = MatchSave.Capture(state, roller.State);
            var restored = MatchSave.Restore(first, resolver, out var restoredRoller);
            byte[] second = MatchSave.Capture(restored, restoredRoller.State);

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void RestoreRebuildsEveryObservableField()
        {
            var (state, roller, resolver) = MidMatch();
            var restored = MatchSave.Restore(MatchSave.Capture(state, roller.State), resolver, out _);

            Assert.AreEqual(state.Phase, restored.Phase);
            Assert.AreEqual(state.Round, restored.Round);
            Assert.AreEqual(state.DrawPileCount, restored.DrawPileCount);
            CollectionAssert.AreEqual(
                state.Market.Select(c => c.Id.Value), restored.Market.Select(c => c.Id.Value));
            CollectionAssert.AreEqual(state.PriorityOrder, restored.PriorityOrder);

            for (int i = 0; i < state.Players.Count; i++)
            {
                var was = state.Players[i];
                var now = restored.Players[i];

                Assert.AreEqual(was.Id, now.Id);
                Assert.AreEqual(was.DisplayName, now.DisplayName);
                Assert.AreEqual(was.Sparks, now.Sparks);
                Assert.AreEqual(was.Score, now.Score);
                Assert.AreEqual(was.HasPassed, now.HasPassed);
                Assert.AreEqual(was.HasCommitted, now.HasCommitted);
                CollectionAssert.AreEqual(was.Dice.FacesCopy(), now.Dice.FacesCopy());
                CollectionAssert.AreEqual(was.Dice.SpentCopy(), now.Dice.SpentCopy());
                CollectionAssert.AreEqual(
                    was.Owned.Select(c => c.Id.Value), now.Owned.Select(c => c.Id.Value));
            }

            // The secret must survive too — a restored server still owes the table its Reveal.
            var observerView = MatchSnapshot.For(restored, new PlayerId(0));
            Assert.AreEqual(1, observerView.Observer.PendingCardId,
                "player 0's pending commit should have survived the round trip");
        }

        [Test]
        public void RestoredMatchPlaysForwardIdentically()
        {
            var (state, roller, resolver) = MidMatch();

            var saved = MatchSave.Capture(state, roller.State);
            var restored = MatchSave.Restore(saved, resolver, out var restoredRoller);

            // Run both servers to the end on their own clocks. Neither takes further input, so
            // identical states and identical rollers must produce identical matches.
            var originalSession = new LocalMatchSession(state, roller);
            var restoredSession = new LocalMatchSession(restored, restoredRoller);

            while (state.Phase != RoundPhase.MatchOver)
            {
                originalSession.Advance();
                restoredSession.Advance();

                Assert.AreEqual(state.Phase, restored.Phase);
                for (int i = 0; i < state.Players.Count; i++)
                    CollectionAssert.AreEqual(
                        state.Players[i].Dice.FacesCopy(), restored.Players[i].Dice.FacesCopy(),
                        $"player {i} dice diverged at round {state.Round} {state.Phase}");
            }

            var originalFinal = Scoring.FinalScores(state);
            var restoredFinal = Scoring.FinalScores(restored);
            for (int i = 0; i < originalFinal.Count; i++)
            {
                Assert.AreEqual(originalFinal[i].Total, restoredFinal[i].Total);
                Assert.AreEqual(originalFinal[i].Player, restoredFinal[i].Player);
            }
        }

        // ------------------------------------------------------------------ failure modes

        [Test]
        public void WrongVersionFailsLoudly()
        {
            var (state, roller, resolver) = MidMatch();
            var bytes = MatchSave.Capture(state, roller.State);

            bytes[4] ^= 0xFF; // the version stamp sits right after the 4-byte magic

            Assert.Throws<InvalidDataException>(() => MatchSave.Restore(bytes, resolver, out _));
        }

        [Test]
        public void NotASaveFailsLoudly()
        {
            var resolver = (Func<CardId, Card>)(_ => null);
            Assert.Throws<InvalidDataException>(
                () => MatchSave.Restore(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, resolver, out _));
        }

        [Test]
        public void UnknownCardFailsLoudly()
        {
            var (state, roller, _) = MidMatch();
            var bytes = MatchSave.Capture(state, roller.State);

            Assert.Throws<InvalidDataException>(
                () => MatchSave.Restore(bytes, _ => null, out _));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// A match two rounds in with real texture: an owned card, sparks income, a secret pending
        /// commit, one passed player, and a part-consumed roller — the state a resume must honour.
        /// </summary>
        private static (MatchState state, SeededDiceRoller roller, Func<CardId, Card> resolver) MidMatch()
        {
            // Sum(2) so any two dice pay, whatever the seeded roller dealt — the texture this
            // fixture wants is state variety, not payment puzzles.
            var deck = new[]
            {
                Make.Card(1, new SumRequirement(2), points: 5),
                Make.Card(2, new SumRequirement(2), points: 3),
                Make.Card(3, new SumRequirement(2), points: 2),
                Make.Card(4, new SumRequirement(2), points: 1)
            };

            var state = Make.Match(Make.Config(rounds: 3, marketSize: 2), deck, 2);
            var roller = new SeededDiceRoller(777);
            var session = new LocalMatchSession(state, roller);

            // Round 1: player 0 buys card 2 uncontested; player 1 passes.
            session.AdvanceTo(RoundPhase.Commit);
            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(2), new[] { 0, 1 }).Success,
                "test setup: player 0's round-1 commit must be legal");
            session.Pass(new PlayerId(1));

            // Round 2, mid-Commit: player 0 has a secret pending claim on card 1. The first
            // Advance leaves round 1's Commit (AdvanceTo would no-op on the phase it is in).
            session.Advance();
            session.AdvanceTo(RoundPhase.Commit);
            Assert.AreEqual(2, state.Round);
            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 }).Success,
                "test setup: player 0's round-2 commit must be legal");

            var byId = deck.ToDictionary(c => c.Id.Value);
            return (state, roller, id => byId.TryGetValue(id.Value, out var card) ? card : null);
        }

        private static int[] Faces(DiceRoll roll)
        {
            var values = new int[roll.Count];
            for (int i = 0; i < roll.Count; i++) values[i] = roll[i];
            return values;
        }

        private static List<int> MakeRange(int n)
        {
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(i);
            return list;
        }
    }
}
