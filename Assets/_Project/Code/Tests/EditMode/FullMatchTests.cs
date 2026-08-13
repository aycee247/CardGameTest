using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// End-to-end runs of a whole match at full table size. These do not pin exact values — they
    /// assert the invariants that must hold no matter how the dice fall, which is what catches the
    /// bugs that only appear after several rounds of compounding powers.
    /// </summary>
    public class FullMatchTests
    {
        /// <summary>A deck deep enough for ten rounds of six players, tier-ordered like the real one.</summary>
        private static List<Card> BuildDeck()
        {
            var deck = new List<Card>();
            int id = 1;

            for (int i = 0; i < 20; i++)
                deck.Add(Make.Card(id++, new NOfAKindRequirement(2), points: 1,
                    power: CardPower.ExtraDie(), family: PowerFamily.Capacity, tier: 1));

            for (int i = 0; i < 20; i++)
                deck.Add(Make.Card(id++, new NOfAKindRequirement(3), points: 3,
                    power: CardPower.FreeReroll(1), family: PowerFamily.Manipulation, tier: 2));

            for (int i = 0; i < 20; i++)
                deck.Add(Make.Card(id++, new SumRequirement(14), points: 4,
                    power: CardPower.ScorePerFamily(1, PowerFamily.Capacity), family: PowerFamily.Scoring, tier: 3));

            return deck;
        }

        /// <summary>
        /// Plays a match to completion with a simple greedy policy: everyone spends free re-rolls,
        /// then takes the most valuable card they can pay for.
        /// </summary>
        private static MatchState PlayOut(int playerCount, ulong seed, int rounds = 10)
        {
            var config = new MatchConfig { Rounds = rounds, MarketSize = 5, StartingDice = 4, MaxDice = 8 };
            var state = Make.Match(config, BuildDeck(), playerCount);
            var session = new LocalMatchSession(state, new SeededDiceRoller(seed));

            int guard = 0;
            while (state.Phase != RoundPhase.MatchOver)
            {
                switch (state.Phase)
                {
                    case RoundPhase.Shape:
                        foreach (var player in state.Players)
                            while (player.Allowance.Rerolls > 0)
                                session.Shape(player.Id, ShapeAction.Reroll(0));
                        break;

                    case RoundPhase.Commit:
                        foreach (var player in state.Players) TryClaim(session, state, player);
                        break;

                    case RoundPhase.Repick:
                        foreach (var id in state.RepickContenders.ToList())
                            TryClaim(session, state, state.Find(id));
                        break;
                }

                session.Advance();
                if (++guard > 500) Assert.Fail("the phase machine failed to terminate");
            }

            return state;
        }

        private static void TryClaim(LocalMatchSession session, MatchState state, PlayerState player)
        {
            foreach (var card in state.Market.OrderByDescending(c => c.Points))
            {
                var payment = Pay.Find(player, card);
                if (payment == null) continue;
                if (session.Commit(player.Id, card.Id, payment).Success) return;
            }
            session.Pass(player.Id);
        }

        [Test]
        public void SixPlayerMatchRunsToCompletion()
        {
            var state = PlayOut(playerCount: 6, seed: 20260813);

            Assert.AreEqual(RoundPhase.MatchOver, state.Phase);
            Assert.AreEqual(10, state.Round);
            Assert.AreEqual(6, Scoring.FinalScores(state).Count);
        }

        [Test]
        public void EveryPlayerCountFromTwoToSixCompletes()
        {
            for (int players = 2; players <= 6; players++)
            {
                var state = PlayOut(players, seed: (ulong)(1000 + players));
                Assert.AreEqual(RoundPhase.MatchOver, state.Phase, $"{players}-player match did not finish");
                Assert.AreEqual(players, Scoring.FinalScores(state).Count);
            }
        }

        [Test]
        public void NoCardIsEverOwnedByTwoPlayers()
        {
            var state = PlayOut(playerCount: 6, seed: 777);

            var owned = state.Players.SelectMany(p => p.Owned.Select(c => c.Id.Value)).ToList();

            CollectionAssert.AllItemsAreUnique(owned,
                "contention resolution handed the same card to more than one player");
        }

        [Test]
        public void DicePoolsNeverExceedTheConfiguredMaximum()
        {
            var state = PlayOut(playerCount: 6, seed: 4242);

            foreach (var player in state.Players)
            {
                Assert.LessOrEqual(player.Dice.Count, state.Config.MaxDice);
                Assert.LessOrEqual(player.DiceCapacity(state.Config), state.Config.MaxDice);
            }
        }

        [Test]
        public void SparksStayWithinBounds()
        {
            var state = PlayOut(playerCount: 4, seed: 31337);

            foreach (var player in state.Players)
            {
                Assert.GreaterOrEqual(player.Sparks, 0, "Sparks must never go negative");
                Assert.LessOrEqual(player.Sparks, state.Config.SparkCap);
            }
        }

        [Test]
        public void EnginesActuallyGrowOverAMatch()
        {
            // Guards the premise of the whole design: by the end, players are meaningfully
            // stronger than they started, otherwise the ten rounds have not earned themselves.
            var state = PlayOut(playerCount: 4, seed: 99);

            int totalCards = state.Players.Sum(p => p.Owned.Count);
            Assert.Greater(totalCards, 4, "four players over ten rounds should claim more than four cards");

            Assert.IsTrue(state.Players.Any(p => p.DiceCapacity(state.Config) > state.Config.StartingDice),
                "no player ever grew their dice pool");
        }

        [Test]
        public void StandingsAreATotalOrder()
        {
            var state = PlayOut(playerCount: 6, seed: 5150);
            var standings = Scoring.FinalScores(state);

            for (int i = 1; i < standings.Count; i++)
                Assert.GreaterOrEqual(standings[i - 1].Total, standings[i].Total,
                    "standings must be sorted by total, best first");

            Assert.AreEqual(standings[0].Player, Scoring.Winner(state));
        }

        [Test]
        public void MatchIsDeterministicForAGivenSeed()
        {
            var a = PlayOut(playerCount: 4, seed: 8675309);
            var b = PlayOut(playerCount: 4, seed: 8675309);

            CollectionAssert.AreEqual(
                Scoring.FinalScores(a).Select(s => s.Total).ToArray(),
                Scoring.FinalScores(b).Select(s => s.Total).ToArray());
        }
    }
}
