using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Tests.EditMode
{
    /// <summary>Returns pre-scripted rolls so a test controls the dice exactly.</summary>
    internal sealed class ScriptedRoller : IDiceRoller
    {
        private readonly Queue<int[]> _rolls;

        public ScriptedRoller(params int[][] rolls) => _rolls = new Queue<int[]>(rolls);

        public DiceRoll Roll(int count)
        {
            if (_rolls.Count == 0)
                throw new InvalidOperationException($"ScriptedRoller exhausted; {count} dice were requested.");

            var next = _rolls.Dequeue();
            if (next.Length != count)
                throw new InvalidOperationException(
                    $"Scripted roll has {next.Length} dice but {count} were requested. " +
                    "Rolls are consumed in player order, one per pool, plus one per re-roll.");

            return new DiceRoll(next);
        }
    }

    /// <summary>Every die comes up the same face. For tests about bookkeeping, not about values.</summary>
    internal sealed class ConstantRoller : IDiceRoller
    {
        private readonly int _face;

        public ConstantRoller(int face = 1) => _face = face;

        public DiceRoll Roll(int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++) values[i] = _face;
            return new DiceRoll(values);
        }
    }

    internal static class Make
    {
        public static Card Card(
            int id,
            ICardRequirement cost,
            int points = 1,
            CardPower power = default,
            PowerFamily family = PowerFamily.Capacity,
            int tier = 1,
            string name = null) =>
            new Card(new CardId(id), name ?? "Card" + id, cost, points, power, family, tier);

        /// <summary>A card bought with any pair — the cheapest useful cost in the game.</summary>
        public static Card Pair(
            int id,
            int points = 1,
            CardPower power = default,
            PowerFamily family = PowerFamily.Capacity) =>
            Card(id, new NOfAKindRequirement(2), points, power, family);

        public static MatchState Match(MatchConfig config, IEnumerable<Card> deck, int playerCount = 2)
        {
            var players = new List<PlayerState>();
            for (int i = 0; i < playerCount; i++)
                players.Add(new PlayerState(new PlayerId(i), "P" + i, i));

            return new MatchState(config, players, deck);
        }

        /// <summary>Small, fast defaults: 4 dice, a 2-card market, short match.</summary>
        public static MatchConfig Config(int rounds = 3, int marketSize = 2, int startingDice = 4) =>
            new MatchConfig { Rounds = rounds, MarketSize = marketSize, StartingDice = startingDice };

        /// <summary>Grants a card outright, bypassing the market. Setup only.</summary>
        public static void Grant(PlayerState player, Card card) => player.OwnedCards.Add(card);
    }

    internal static class Pay
    {
        /// <summary>
        /// Finds a set of unspent dice that pays for a card, or null if the player cannot afford it.
        /// Brute-forces subsets, which is fine for at most 8 dice and doubles as a stress test of
        /// <see cref="CostChecker"/> in the full-match run.
        /// </summary>
        public static int[] Find(PlayerState player, Card card)
        {
            var pool = player.Dice;
            var available = new List<int>();
            for (int i = 0; i < pool.Count; i++)
                if (!pool.IsSpent(i)) available.Add(i);

            var wildFaces = player.WildFaces();
            int wildDice = player.WildDice();

            // Ascending subset size, so the cheapest payment that works is the one returned.
            for (int size = 1; size <= available.Count; size++)
            {
                var indices = new int[size];
                var found = Search(0, 0);
                if (found != null) return found;

                int[] Search(int start, int depth)
                {
                    if (depth == size)
                    {
                        var faces = new int[size];
                        for (int i = 0; i < size; i++) faces[i] = pool.FaceAt(indices[i]);
                        return CostChecker.Satisfies(card.Cost, faces, wildFaces, wildDice)
                            ? (int[])indices.Clone()
                            : null;
                    }

                    for (int i = start; i < available.Count; i++)
                    {
                        indices[depth] = available[i];
                        var result = Search(i + 1, depth + 1);
                        if (result != null) return result;
                    }
                    return null;
                }
            }

            return null;
        }
    }
}
