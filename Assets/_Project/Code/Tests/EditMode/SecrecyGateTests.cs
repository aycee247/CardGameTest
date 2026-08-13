using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// M3's gate: no information leaks.
    ///
    /// <see cref="SnapshotSecrecyTests"/> asserts that specific fields are hidden. That catches the
    /// leaks you thought of. This asserts something stronger and harder to fool: if two matches
    /// differ <i>only</i> in what an opponent secretly chose, everything the other player receives
    /// must be identical. If their view cannot distinguish the choices, there is nothing in it to
    /// exploit — no field, no array length, no ordering, including fields added later.
    ///
    /// The comparison is a full reflective dump, so a future field is covered without anyone
    /// remembering to write an assertion for it.
    /// </summary>
    public class SecrecyGateTests
    {
        /// <summary>
        /// A fixed two-player setup. Both players roll the same dice every time, so the only thing
        /// that can differ between runs is what the test makes differ.
        /// </summary>
        private static LocalMatchSession Match(out MatchState state)
        {
            var config = new MatchConfig { Rounds = 3, MarketSize = 4, StartingDice = 4 };

            state = Make.Match(config, new[]
            {
                Make.Pair(1, points: 5),
                Make.Pair(2, points: 3),
                Make.Pair(3, points: 1),
                Make.Card(4, new NOfAKindRequirement(2), points: 2)
            }, playerCount: 2);

            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 4, 4 },
                new[] { 5, 5, 2, 2 }));

            session.AdvanceTo(RoundPhase.Commit);
            return session;
        }

        /// <summary>What player 1 receives after player 0 secretly commits to the given card.</summary>
        private static string OpponentViewAfterCommit(CardId card, int[] dice)
        {
            var session = Match(out var state);

            Assert.IsTrue(session.Commit(new PlayerId(0), card, dice).Success,
                $"setup failed: P0 could not commit to {card}");

            return Dump.Of(MatchSnapshot.For(state, new PlayerId(1)));
        }

        [Test]
        public void OpponentCannotTellWhichCardWasClaimed()
        {
            var claimedTheExpensiveOne = OpponentViewAfterCommit(new CardId(1), new[] { 0, 1 });
            var claimedTheCheapOne = OpponentViewAfterCommit(new CardId(3), new[] { 0, 1 });

            Assert.AreEqual(claimedTheExpensiveOne, claimedTheCheapOne,
                "player 1's view differs depending on which card player 0 secretly picked");
        }

        [Test]
        public void OpponentCannotTellWhichDiceWerePledged()
        {
            var paidWithSixes = OpponentViewAfterCommit(new CardId(1), new[] { 0, 1 });
            var paidWithFours = OpponentViewAfterCommit(new CardId(1), new[] { 2, 3 });

            Assert.AreEqual(paidWithSixes, paidWithFours,
                "player 1's view differs depending on which dice player 0 pledged");
        }

        [Test]
        public void OpponentCannotTellAcrossEveryCombination()
        {
            // Every legal commit P0 could make with this roll must look the same to P1.
            var cards = new[] { new CardId(1), new CardId(2), new CardId(3), new CardId(4) };
            var payments = new[] { new[] { 0, 1 }, new[] { 2, 3 } };

            string baseline = null;

            foreach (var card in cards)
            {
                foreach (var payment in payments)
                {
                    var view = OpponentViewAfterCommit(card, payment);

                    if (baseline == null) baseline = view;
                    else Assert.AreEqual(baseline, view,
                        $"P1's view leaked P0's choice of {card} paid with [{string.Join(",", payment)}]");
                }
            }
        }

        [Test]
        public void TheComparisonCanActuallyDetectADifference()
        {
            // Negative control. Without this the three tests above would pass just as happily if
            // the dump were empty, or if every snapshot rendered identically for some unrelated
            // reason. At Reveal the commits are public, so the views MUST differ.
            string RevealedView(CardId card)
            {
                var session = Match(out var state);
                session.Commit(new PlayerId(0), card, new[] { 0, 1 });
                session.Advance();   // Commit -> Reveal, commits become public

                Assert.AreEqual(RoundPhase.Reveal, state.Phase);
                return Dump.Of(MatchSnapshot.For(state, new PlayerId(1)));
            }

            Assert.AreNotEqual(RevealedView(new CardId(1)), RevealedView(new CardId(3)),
                "the dump cannot distinguish two genuinely different snapshots, so the secrecy " +
                "tests above prove nothing");
        }

        [Test]
        public void PassingIsDistinguishableFromCommitting_AndThatIsIntended()
        {
            // Not a leak: the opponent rail has to show who has locked in (UI-1). What must stay
            // hidden is *what* they locked in, which the tests above cover.
            var session = Match(out var state);
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            var committed = MatchSnapshot.For(state, new PlayerId(1));

            var other = Match(out var otherState);
            other.Pass(new PlayerId(0));
            var passed = MatchSnapshot.For(otherState, new PlayerId(1));

            Assert.IsTrue(Row(committed, 0).HasDecided);
            Assert.IsTrue(Row(passed, 0).HasDecided);
            Assert.AreEqual(-1, Row(committed, 0).PendingCardId);
            Assert.AreEqual(-1, Row(passed, 0).PendingCardId);
        }

        [Test]
        public void TheObserverStillSeesTheirOwnChoice()
        {
            // The mirror of the gate: filtering must not be so aggressive that a player loses
            // sight of what they themselves committed to.
            var a = OwnViewAfterCommit(new CardId(1));
            var b = OwnViewAfterCommit(new CardId(3));

            Assert.AreNotEqual(a, b, "a player must be able to see their own pending commit");

            string OwnViewAfterCommit(CardId card)
            {
                var session = Match(out var state);
                session.Commit(new PlayerId(0), card, new[] { 0, 1 });
                return Dump.Of(MatchSnapshot.For(state, new PlayerId(0)));
            }
        }

        [Test]
        public void EveryPlayerCountKeepsCommitsSecret()
        {
            for (int players = 2; players <= 6; players++)
            {
                var first = ViewOfSeatOne(players, new CardId(1));
                var second = ViewOfSeatOne(players, new CardId(3));

                Assert.AreEqual(first, second, $"commit leaked at {players} players");
            }

            string ViewOfSeatOne(int playerCount, CardId card)
            {
                var config = new MatchConfig { Rounds = 3, MarketSize = 4, StartingDice = 4 };
                var state = Make.Match(config, new[]
                {
                    Make.Pair(1, points: 5), Make.Pair(2, points: 3),
                    Make.Pair(3, points: 1), Make.Pair(4, points: 2)
                }, playerCount);

                var session = new LocalMatchSession(state, new ConstantRoller(4));
                session.AdvanceTo(RoundPhase.Commit);
                session.Commit(new PlayerId(0), card, new[] { 0, 1 });

                return Dump.Of(MatchSnapshot.For(state, new PlayerId(1)));
            }
        }

        private static PlayerSnapshot Row(MatchSnapshot snapshot, int playerId)
        {
            foreach (var row in snapshot.Players)
                if (row.PlayerId == playerId) return row;

            throw new InvalidOperationException("no such player in snapshot: " + playerId);
        }
    }
}
