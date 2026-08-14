using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// M5's gate: no dominant strategy.
    ///
    /// Six committed strategies play thousands of simulated matches against the real
    /// <see cref="StarterDeck"/>. If any of them wins far more than its share, the deck rewards one
    /// idea too much and the other cards are decoration.
    ///
    /// This is a regression test, not the tuning tool — the exploratory numbers live in
    /// <see cref="BalanceReportTests"/>. The thresholds are deliberately loose: they are meant to
    /// catch a card change that breaks the game open, not to pin today's exact percentages.
    /// </summary>
    public class BalanceGateTests
    {
        /// <summary>
        /// A strategy may win up to half again its fair share. Tighter than this and ordinary
        /// variance fails the build; looser and a genuinely dominant line slips through.
        /// </summary>
        private const float DominanceRatio = 1.5f;

        private const int Matches = 150;

        private static List<Policy> Contenders() => new List<Policy>
        {
            new CapacityFirst(), new ManipulationFirst(), new EconomyFirst(),
            new ScoringFirst(), new GreedyPoints()
        };

        private static void AssertNoDominance(int playerCount)
        {
            var policies = Contenders().Take(playerCount).ToList();
            var wins = Sim.WinCounts(policies, Matches);

            float fairShare = 1f / policies.Count;
            float ceiling = fairShare * DominanceRatio;

            var worst = wins.OrderByDescending(k => k.Value).First();
            float rate = (float)worst.Value / Matches;

            Assert.LessOrEqual(rate, ceiling,
                $"at {playerCount} players '{worst.Key}' won {rate:P0}, over the {ceiling:P0} ceiling. " +
                $"Full table: {string.Join(", ", wins.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {(float)k.Value / Matches:P0}"))}");
        }

        [Test] public void NoDominantStrategyAtTwoPlayers() => AssertNoDominance(2);
        [Test] public void NoDominantStrategyAtThreePlayers() => AssertNoDominance(3);
        [Test] public void NoDominantStrategyAtFourPlayers() => AssertNoDominance(4);
        [Test] public void NoDominantStrategyAtFivePlayers() => AssertNoDominance(5);

        [Test]
        public void EveryStrategyWinsSometimesAtAFullTable()
        {
            // Dominance is only half of it. A strategy that never wins is a trap: those cards read
            // as a plan and are not one.
            var policies = Contenders();
            var wins = Sim.WinCounts(policies, Matches);

            foreach (var kv in wins)
                Assert.Greater(kv.Value, 0, $"'{kv.Key}' never won a single match out of {Matches}");
        }

        [Test]
        public void ClaimingNothingIsNeverCompetitive()
        {
            // The consolation payout exists so a round is never empty (MKT-5). It must not make
            // doing nothing a viable line — a live worry when Sparks were specced.
            var duel = new List<Policy> { new GreedyPoints(), new AlwaysPass() };
            var wins = Sim.WinCounts(duel, Matches);

            Assert.AreEqual(0, wins["always-pass"],
                "passing every round beat a player who actually claimed cards");
        }

        [Test]
        public void TheDeckIsWhatWasSpecced()
        {
            var blueprints = StarterDeck.Blueprints;

            Assert.AreEqual(48, blueprints.Count, "CARD-1 asks for 48 cards");
            CollectionAssert.AllItemsAreUnique(blueprints.Select(b => b.Id).ToList(), "duplicate card ids");

            for (int tier = 1; tier <= 3; tier++)
                Assert.AreEqual(16, blueprints.Count(b => b.Tier == tier), $"tier {tier} should hold 16 cards");

            // Every family has to appear at every tier's worth of play, or a strategy has nothing
            // to buy for stretches of the match.
            foreach (PowerFamily family in System.Enum.GetValues(typeof(PowerFamily)))
                Assert.Greater(blueprints.Count(b => b.Family == family), 2,
                    $"the {family} family has too few cards to be a strategy");
        }

        [Test]
        public void NoCardIsUnplayable()
        {
            var config = MatchConfig.Default;

            foreach (var blueprint in StarterDeck.Blueprints)
            {
                var card = blueprint.ToCard();

                Assert.IsTrue(CostChecker.IsSatisfiableWith(card.Cost, config.MaxDice),
                    $"'{card.DisplayName}' costs {card.DescribeCost()}, which {config.MaxDice} dice can never pay");

                Assert.AreNotEqual(PowerKind.None, card.Power.Kind,
                    $"'{card.DisplayName}' has no power");
            }
        }

        [Test]
        public void TierOneIsAffordableFromTheOpeningRoll()
        {
            // Round one is four raw dice with no Sparks and no powers. If Tier 1 cannot be paid
            // from that, the opening round has nothing in it at all.
            var config = MatchConfig.Default;

            int affordable = StarterDeck.Blueprints
                .Where(b => b.Tier == 1)
                .Count(b => CostChecker.IsSatisfiableWith(b.BuildCost(), config.StartingDice));

            Assert.GreaterOrEqual(affordable, 12,
                "too few Tier 1 cards can be paid for with the dice players start on");
        }
    }
}
