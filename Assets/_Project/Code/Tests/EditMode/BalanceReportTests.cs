using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Prints the balance picture rather than asserting on it. Not a pass/fail gate — it is how the
    /// numbers behind the tuning decisions were read, kept so they can be re-read after a change.
    /// </summary>
    public class BalanceReportTests
    {
        private static List<Policy> AllPolicies() => new List<Policy>
        {
            new CapacityFirst(), new ManipulationFirst(), new EconomyFirst(),
            new ScoringFirst(), new GreedyPoints(), new AlwaysPass()
        };

        [Test]
        public void Report()
        {
            // Half a minute of simulation, which does not belong in a suite that otherwise runs in
            // milliseconds. Opt in when tuning:  FOUNDRY_BALANCE=1 tools/run-core-tests.sh Balance
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_BALANCE")))
                Assert.Ignore("set FOUNDRY_BALANCE=1 to run the balance report");

            const int Matches = 360;

            Console.WriteLine("\n=== win rate by strategy, 6 players ===");
            var policies = AllPolicies();
            var wins = Sim.WinCounts(policies, Matches);
            foreach (var kv in wins.OrderByDescending(k => k.Value))
                Console.WriteLine($"  {kv.Key,-14} {kv.Value,4}  {100f * kv.Value / Matches,5:0.0}%");

            Console.WriteLine("\n=== win rate at other player counts ===");
            for (int count = 2; count <= 5; count++)
            {
                var subset = AllPolicies().Take(count).ToList();
                var w = Sim.WinCounts(subset, Matches);
                Console.WriteLine($"  {count} players: " +
                    string.Join("  ", w.OrderByDescending(k => k.Value)
                        .Select(k => $"{k.Key}={100f * k.Value / Matches:0}%")));
            }

            // Shape metrics exclude always-pass: a seat that scores zero by construction drags the
            // spread and the last-place figure into meaninglessness.
            Console.WriteLine("\n=== match shape, 6 players (no always-pass) ===");
            var playing = AllPolicies().Where(p => !(p is AlwaysPass)).ToList();
            playing.Add(new GreedyPoints());
            var shapes = Enumerable.Range(0, 60)
                .Select(i => Sim.PlayMatch(playing, 5000 + i))
                .ToList();

            Console.WriteLine($"  cards claimed per match : {shapes.Average(s => s.CardsClaimed):0.0}");
            Console.WriteLine($"  winning total           : {shapes.Average(s => s.Totals.Max()):0.0}");
            Console.WriteLine($"  last-place total        : {shapes.Average(s => s.Totals.Min()):0.0}");
            Console.WriteLine($"  final dice (avg / max)  : {shapes.Average(s => s.FinalDice.Average()):0.0} / {shapes.Max(s => s.FinalDice.Max())}");

            Console.WriteLine("\n=== always-pass, head to head ===");
            foreach (var rival in new Policy[] { new CapacityFirst(), new ManipulationFirst(), new EconomyFirst(), new ScoringFirst() })
            {
                var duel = new List<Policy> { rival, new AlwaysPass() };
                var w = Sim.WinCounts(duel, Matches);
                Console.WriteLine($"  {rival.Name,-14} {100f * w[rival.Name] / Matches,5:0.0}%   vs always-pass {100f * w["always-pass"] / Matches,5:0.0}%");
            }

            // Dice pay every cost, so the size of the pool is the real lever on how strong
            // stacking capacity is. Sweep it rather than guessing at card values.
            Console.WriteLine("\n=== capacity win rate vs dice ceiling ===");
            foreach (int maxDice in new[] { 5, 6, 7, 8 })
            {
                foreach (int startingDice in new[] { 4, 5 })
                {
                    if (startingDice > maxDice) continue;

                    var config = new MatchConfig { MaxDice = maxDice, StartingDice = startingDice };
                    var w = Sim.WinCounts(AllPolicies(), Matches, config);
                    Console.WriteLine($"  start {startingDice} max {maxDice}: " +
                        string.Join("  ", w.OrderByDescending(k => k.Value)
                            .Where(k => k.Value > 0)
                            .Select(k => $"{k.Key}={100f * k.Value / Matches:0}%")));
                }
            }

            Console.WriteLine("\n=== round count vs match length ===");
            foreach (int rounds in new[] { 8, 10, 12 })
            {
                var config = new MatchConfig { Rounds = rounds };
                var sample = Enumerable.Range(0, 40).Select(i => Sim.PlayMatch(playing, 7000 + i, config)).ToList();
                Console.WriteLine($"  {rounds} rounds: {sample.Average(s => s.CardsClaimed):0.0} cards, " +
                                  $"winner {sample.Average(s => s.Totals.Max()):0.0}vp, " +
                                  $"spread {sample.Average(s => s.Totals.Max() - s.Totals.Min()):0.0}vp");
            }

            Assert.Pass();
        }
    }
}
