using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Upkeep payouts (CORE-4, MKT-5) and end-of-match scoring (CARD-3).</summary>
    public class EconomyAndScoringTests
    {
        [Test]
        public void UnspentDiceBecomeSparks()
        {
            var state = Make.Match(Make.Config(rounds: 2, marketSize: 1), new[] { Make.Pair(1) });
            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 },
                new[] { 1, 2, 3, 4 }));

            session.AdvanceTo(RoundPhase.Commit);
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.AdvanceTo(RoundPhase.Upkeep);
            session.Advance();

            // P0 spent two of four dice and took a card, so no consolation.
            Assert.AreEqual(2, state.Players[0].Sparks);

            // P1 got nothing: four unspent dice plus the consolation payment.
            Assert.AreEqual(4 + state.Config.ConsolationSparks, state.Players[1].Sparks);
        }

        [Test]
        public void SparkIncomePaysEveryUpkeep()
        {
            var state = Make.Match(Make.Config(rounds: 3, marketSize: 1), new[] { Make.Pair(1) });
            Make.Grant(state.Players[0], Make.Card(90, new SumRequirement(0),
                power: CardPower.SparkIncome(2), family: PowerFamily.Economy));

            var session = new LocalMatchSession(state, new ConstantRoller(1));
            session.AdvanceTo(RoundPhase.Upkeep);
            session.Advance();

            // 4 unspent + 3 consolation + 2 income, capped at 10.
            Assert.AreEqual(9, state.Players[0].Sparks);
            Assert.AreEqual(7, state.Players[1].Sparks);
        }

        [Test]
        public void SparksAreCapped()
        {
            var config = Make.Config(rounds: 3, marketSize: 1);
            config.SparkCap = 5;
            var state = Make.Match(config, new[] { Make.Pair(1) });

            var session = new LocalMatchSession(state, new ConstantRoller(1));
            session.AdvanceTo(RoundPhase.Upkeep);
            session.Advance();

            Assert.AreEqual(5, state.Players[0].Sparks);
        }

        [Test]
        public void MarketRefillsAtUpkeep()
        {
            var state = Make.Match(Make.Config(rounds: 3, marketSize: 2),
                new[] { Make.Pair(1), Make.Pair(2), Make.Pair(3) });

            Assert.AreEqual(2, state.Market.Count);
            Assert.AreEqual(1, state.DrawPileCount);

            var session = new LocalMatchSession(state, new ConstantRoller(4));
            session.AdvanceTo(RoundPhase.Commit);
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.AdvanceTo(RoundPhase.Upkeep);

            Assert.AreEqual(1, state.Market.Count, "the claimed card leaves immediately");

            session.Advance();

            Assert.AreEqual(2, state.Market.Count);
            Assert.AreEqual(0, state.DrawPileCount);
        }

        [Test]
        public void FinalScoreAddsCardPointsAndFlatPowers()
        {
            var player = new PlayerState(new PlayerId(0), "P0");
            Make.Grant(player, Make.Card(1, new SumRequirement(0), points: 3));
            Make.Grant(player, Make.Card(2, new SumRequirement(0), points: 4,
                power: CardPower.FlatScore(2), family: PowerFamily.Scoring));

            var score = Scoring.Score(player);

            Assert.AreEqual(7, score.CardPoints);
            Assert.AreEqual(2, score.PowerPoints);
            Assert.AreEqual(9, score.Total);
        }

        [Test]
        public void ScorePerFamilyCountsOwnedCardsOfThatFamily()
        {
            var player = new PlayerState(new PlayerId(0), "P0");
            Make.Grant(player, Make.Card(1, new SumRequirement(0), points: 1, family: PowerFamily.Manipulation));
            Make.Grant(player, Make.Card(2, new SumRequirement(0), points: 1, family: PowerFamily.Manipulation));
            Make.Grant(player, Make.Card(3, new SumRequirement(0), points: 1, family: PowerFamily.Capacity));
            Make.Grant(player, Make.Card(4, new SumRequirement(0), points: 4,
                power: CardPower.ScorePerFamily(2, PowerFamily.Manipulation), family: PowerFamily.Scoring));

            var score = Scoring.Score(player);

            Assert.AreEqual(7, score.CardPoints);
            Assert.AreEqual(4, score.PowerPoints, "two Manipulation cards at 2 VP each");
            Assert.AreEqual(11, score.Total);
        }

        [Test]
        public void StandingsBreakTiesBySparksThenCardsThenSeat()
        {
            var state = Make.Match(Make.Config(), new[] { Make.Pair(1) }, playerCount: 3);

            // All three finish on 4 points.
            Make.Grant(state.Players[0], Make.Card(50, new SumRequirement(0), points: 4));
            Make.Grant(state.Players[1], Make.Card(51, new SumRequirement(0), points: 4));
            Make.Grant(state.Players[2], Make.Card(52, new SumRequirement(0), points: 2));
            Make.Grant(state.Players[2], Make.Card(53, new SumRequirement(0), points: 2));

            state.Players[0].Sparks = 1;
            state.Players[1].Sparks = 5;
            state.Players[2].Sparks = 5;

            var standings = Scoring.FinalScores(state);

            // P1 and P2 both hold 5 Sparks; P2 holds more cards, so it takes second.
            CollectionAssert.AreEqual(
                new[] { 2, 1, 0 },
                standings.Select(s => s.Player.Value).ToArray());
        }

        [Test]
        public void WinnerIsOnlyReportedOnceTheMatchIsOver()
        {
            var state = Make.Match(Make.Config(rounds: 1), new[] { Make.Pair(1) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));

            Assert.IsNull(Scoring.Winner(state));

            session.AdvanceTo(RoundPhase.Commit);
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            for (int i = 0; i < 16 && state.Phase != RoundPhase.MatchOver; i++) session.Advance();

            Assert.AreEqual(RoundPhase.MatchOver, state.Phase);
            Assert.AreEqual(new PlayerId(0), Scoring.Winner(state));
        }
    }
}
