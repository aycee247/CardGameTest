using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// How a simulated player decides. Each policy is a caricature of a real strategy — a player
    /// committed to one idea — which is what makes them useful: if one caricature beats the others
    /// consistently, the deck rewards that idea too much.
    /// </summary>
    internal abstract class Policy
    {
        public abstract string Name { get; }

        /// <summary>Higher is more wanted. Negative means never take it.</summary>
        protected abstract float Value(Card card, PlayerState me, MatchState state);

        /// <summary>Spend free re-rolls only; Sparks are left for the economy policies to exploit.</summary>
        public virtual bool ShouldSpendSparks => false;

        public void TakeTurn(LocalMatchSession session, MatchState state, PlayerState me)
        {
            Shape(session, state, me);

            var best = state.Market
                .Select(card => new { card, score = Value(card, me, state), pay = Pay.Find(me, card) })
                .Where(x => x.pay != null && x.score >= 0f)
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            if (best == null) { session.Pass(me.Id); return; }
            if (!session.Commit(me.Id, best.card.Id, best.pay).Success) session.Pass(me.Id);
        }

        private void Shape(LocalMatchSession session, MatchState state, PlayerState me)
        {
            if (state.Phase != RoundPhase.Shape) return;

            // Re-roll the lowest dice, which are the least useful for the sums and sets most costs
            // ask for. Free actions first; Sparks only if the policy is willing.
            while (me.Allowance.Rerolls > 0 && TryRerollWorst(session, me)) { }

            if (!ShouldSpendSparks) return;
            while (me.Sparks >= state.Config.RerollSparkCost && TryRerollWorst(session, me)) { }
        }

        private static bool TryRerollWorst(LocalMatchSession session, PlayerState me)
        {
            int worst = -1, worstFace = int.MaxValue;
            for (int i = 0; i < me.Dice.Count; i++)
            {
                if (me.Dice.IsSpent(i)) continue;
                if (me.Dice.FaceAt(i) < worstFace) { worstFace = me.Dice.FaceAt(i); worst = i; }
            }

            return worst >= 0 && session.Shape(me.Id, ShapeAction.Reroll(worst)).Success;
        }
    }

    /// <summary>Takes whatever is worth the most points right now.</summary>
    internal sealed class GreedyPoints : Policy
    {
        public override string Name => "greedy-points";
        protected override float Value(Card card, PlayerState me, MatchState state) => card.Points;
    }

    /// <summary>Builds the biggest dice pool it can, then converts late.</summary>
    internal sealed class CapacityFirst : Policy
    {
        public override string Name => "capacity";

        protected override float Value(Card card, PlayerState me, MatchState state)
        {
            float bonus = card.Family == PowerFamily.Capacity ? 20f : 0f;

            // Once the pool is capped, more capacity is worthless and points are all that is left.
            if (card.Power.Kind == PowerKind.ExtraDie && me.DiceCapacity(state.Config) >= state.Config.MaxDice)
                bonus = 0f;

            return bonus + card.Points;
        }
    }

    /// <summary>Buys control over the dice — re-rolls, nudges, wilds.</summary>
    internal sealed class ManipulationFirst : Policy
    {
        public override string Name => "manipulation";

        protected override float Value(Card card, PlayerState me, MatchState state) =>
            (card.Family == PowerFamily.Manipulation || card.Family == PowerFamily.Wild ? 20f : 0f) + card.Points;
    }

    /// <summary>Banks Sparks and spends them freely to hit costs.</summary>
    internal sealed class EconomyFirst : Policy
    {
        public override string Name => "economy";
        public override bool ShouldSpendSparks => true;

        protected override float Value(Card card, PlayerState me, MatchState state) =>
            (card.Family == PowerFamily.Economy ? 20f : 0f) + card.Points;
    }

    /// <summary>Chases end-game scoring, ignoring the engine.</summary>
    internal sealed class ScoringFirst : Policy
    {
        public override string Name => "scoring";

        protected override float Value(Card card, PlayerState me, MatchState state) =>
            (card.Family == PowerFamily.Scoring ? 20f : 0f) + card.Points;
    }

    /// <summary>
    /// Claims nothing, ever. The control: it exists to measure whether the consolation payout makes
    /// doing nothing competitive, which was a live worry when Sparks were specced.
    /// </summary>
    internal sealed class AlwaysPass : Policy
    {
        public override string Name => "always-pass";
        protected override float Value(Card card, PlayerState me, MatchState state) => -1f;
    }

    internal sealed class MatchResult
    {
        public string[] PolicyNames;
        public int[] Totals;
        public int[] CardCounts;
        public int[] FinalDice;
        public int WinnerSeat;
        public int Rounds;
        public int CardsClaimed;
    }

    /// <summary>
    /// Plays whole matches headlessly so balance can be measured instead of guessed. Uses the real
    /// <see cref="StarterDeck"/>, so what it measures is what ships.
    /// </summary>
    internal static class Sim
    {
        public static MatchResult PlayMatch(IReadOnlyList<Policy> policies, int seed, MatchConfig config = null)
        {
            config ??= MatchConfig.Default;

            var players = new List<PlayerState>(policies.Count);
            for (int i = 0; i < policies.Count; i++)
                players.Add(new PlayerState(new PlayerId(i), policies[i].Name, i));

            var deck = ShuffleWithinTiers(StarterDeck.Build(), new Random(seed));
            var state = new MatchState(config, players, deck);
            var session = new LocalMatchSession(state, new SeededDiceRoller((ulong)seed));

            int guard = 0;
            while (state.Phase != RoundPhase.MatchOver)
            {
                if (state.Phase == RoundPhase.Shape || state.Phase == RoundPhase.Commit)
                {
                    for (int i = 0; i < players.Count; i++)
                        if (!players[i].HasCommitted && !players[i].HasPassed)
                            policies[i].TakeTurn(session, state, players[i]);
                }
                else if (state.Phase == RoundPhase.Repick)
                {
                    foreach (var id in state.RepickContenders.ToList())
                    {
                        var seat = state.Find(id);
                        if (seat != null && !seat.HasCommitted && !seat.HasPassed)
                            policies[id.Value].TakeTurn(session, state, seat);
                    }
                }

                session.Advance();
                if (++guard > 4000) throw new InvalidOperationException("simulated match did not terminate");
            }

            var standings = Scoring.FinalScores(state);

            return new MatchResult
            {
                PolicyNames = policies.Select(p => p.Name).ToArray(),
                Totals = players.Select(p => Scoring.Score(p).Total).ToArray(),
                CardCounts = players.Select(p => p.Owned.Count).ToArray(),
                FinalDice = players.Select(p => p.Dice.Count).ToArray(),
                WinnerSeat = standings[0].Player.Value,
                Rounds = state.Round,
                CardsClaimed = players.Sum(p => p.Owned.Count)
            };
        }

        /// <summary>Mirrors CardDatabase.BuildShuffledDeck: shuffled within tiers, never across.</summary>
        public static List<Card> ShuffleWithinTiers(List<Card> deck, Random rng)
        {
            var result = new List<Card>(deck.Count);

            foreach (var tier in deck.GroupBy(c => c.Tier).OrderBy(g => g.Key))
            {
                var bucket = tier.ToList();
                for (int i = bucket.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (bucket[i], bucket[j]) = (bucket[j], bucket[i]);
                }
                result.AddRange(bucket);
            }

            return result;
        }

        /// <summary>
        /// Plays a set of policies against each other many times, rotating seats so seat order
        /// cannot be mistaken for strategy strength.
        /// </summary>
        public static Dictionary<string, int> WinCounts(IReadOnlyList<Policy> policies, int matches, MatchConfig config = null)
        {
            var wins = policies.ToDictionary(p => p.Name, _ => 0);

            for (int match = 0; match < matches; match++)
            {
                // Rotate the seating each match: priority ties break by seat, so a fixed order
                // would quietly hand one policy an advantage.
                var seated = new List<Policy>(policies.Count);
                for (int i = 0; i < policies.Count; i++)
                    seated.Add(policies[(i + match) % policies.Count]);

                var result = PlayMatch(seated, seed: 1000 + match, config: config);
                wins[seated[result.WinnerSeat].Name]++;
            }

            return wins;
        }
    }
}
