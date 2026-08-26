using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// STORY-7.1: solo play against bots — the whole flow headless, on a synthetic clock. Bots
    /// decide from their own snapshots only (the constructor guard is itself under test), act on
    /// scheduled delays rather than instantly, and a fixed seed replays the exact match.
    /// </summary>
    [TestFixture]
    public class SoloDirectorTests
    {
        private static Dictionary<int, Card> Deck(int count)
        {
            var deck = new Dictionary<int, Card>();
            for (int id = 1; id <= count; id++)
                deck[id] = Make.Pair(id, points: id);
            return deck;
        }

        private static SoloDirector Build(Dictionary<int, Card> deck, int botCount, IDiceRoller roller,
            ulong pacingSeed = 7, float minDelay = 1.4f, float maxDelay = 4.5f)
        {
            var state = Make.Match(Make.Config(rounds: 3, marketSize: 2), deck.Values, playerCount: 1 + botCount);
            var session = new LocalMatchSession(state, roller);

            var bots = new List<BotPlayer>();
            for (int seat = 1; seat <= botCount; seat++)
                bots.Add(new BotPlayer(new PlayerId(seat), id => deck.TryGetValue(id.Value, out var c) ? c : null,
                    seed: (ulong)(100 + seat)));

            return new SoloDirector(session, new PlayerId(0), bots, pacingSeed, minDelay, maxDelay);
        }

        /// <summary>Plays out the match with the human passing every pass; returns a score line.</summary>
        private static string RunToMatchOver(SoloDirector director, float now)
        {
            int guard = 0;
            while (director.Stage != SoloStage.MatchOver)
            {
                switch (director.Stage)
                {
                    case SoloStage.Acting:
                        director.HumanDone(now);
                        now += 0.5f;
                        director.Tick(now);
                        break;
                    case SoloStage.Reveal:
                        director.ContinueFromReveal(now);
                        break;
                    case SoloStage.RoundSummary:
                        director.ContinueFromSummary(now);
                        break;
                }

                if (++guard > 2000) throw new InvalidOperationException("solo match did not terminate");
            }

            return string.Join(",", director.FinalScores().Select(s => $"{s.Player.Value}:{s.Total}"));
        }

        [Test]
        public void AFullSoloMatchRunsToCompletion()
        {
            var director = Build(Deck(12), botCount: 3, new SeededDiceRoller(42));
            director.Begin(now: 0f);

            RunToMatchOver(director, now: 10f);

            Assert.AreEqual(RoundPhase.MatchOver, director.State.Phase);
            Assert.AreEqual(3, director.State.Round);
        }

        [Test]
        public void TheSameSeedsReplayTheExactMatch()
        {
            var first = Build(Deck(12), botCount: 2, new SeededDiceRoller(4242));
            first.Begin(0f);
            var second = Build(Deck(12), botCount: 2, new SeededDiceRoller(4242));
            second.Begin(0f);

            Assert.AreEqual(RunToMatchOver(first, 10f), RunToMatchOver(second, 10f),
                "identical seeds must produce identical standings (AC3)");
        }

        [Test]
        public void BotsNeverActBeforeTheirMinimumDelay()
        {
            var director = Build(Deck(12), botCount: 3, new SeededDiceRoller(1), minDelay: 1.4f);
            director.Begin(now: 100f);

            director.Tick(100.9f);   // before every bot's earliest possible moment
            foreach (var p in director.State.Players.Where(p => p.Id.Value != 0))
                Assert.IsFalse(p.HasCommitted || p.HasPassed,
                    $"bot {p.Id.Value} acted instantly — pacing is the point (AC2)");

            director.Tick(200f);     // long past every scheduled moment
            foreach (var p in director.State.Players.Where(p => p.Id.Value != 0))
                Assert.IsTrue(p.HasCommitted || p.HasPassed, $"bot {p.Id.Value} never acted");

            // The pass stays open for the human, then closes into the reveal hold.
            Assert.AreEqual(SoloStage.Acting, director.Stage);
            director.HumanDone(200f);
            Assert.AreEqual(SoloStage.Reveal, director.Stage);
        }

        [Test]
        public void ABotRefusesAnotherSeatsView()
        {
            var deck = Deck(4);
            var state = Make.Match(Make.Config(), deck.Values, playerCount: 2);
            var session = new LocalMatchSession(state, new SeededDiceRoller(9));
            session.Advance();       // Roll -> Shape, so there is a live view to hand over

            var bot = new BotPlayer(new PlayerId(1), id => deck[id.Value], seed: 5);

            Assert.Throws<InvalidOperationException>(
                () => bot.TakeTurn(() => MatchSnapshot.For(state, new PlayerId(0)), session),
                "a bot deciding from an opponent's view would see secret commits (AC5)");
        }

        [Test]
        public void BotsClaimContestAndRepickWithoutASingleRejection()
        {
            // Every die is a 3: every pair cost is payable, so both bots chase the same best card,
            // contest it, and the loser re-picks — the full awkward path, with zero rejections.
            var director = Build(Deck(6), botCount: 2, new ConstantRoller(3));

            int rejections = 0;
            director.Session.MoveRejected += _ => rejections++;

            director.Begin(0f);
            RunToMatchOver(director, 10f);

            var botCards = director.State.Players.Where(p => p.Id.Value != 0).Sum(p => p.Owned.Count);
            Assert.Greater(botCards, 1, "both bots should end the match owning cards");
            Assert.AreEqual(0, rejections,
                "every bot intent must be legal — the engine re-validates, bots must not lean on that");
        }

        [Test]
        public void TheHumanViewStaysPinnedForTheWholeMatch()
        {
            var director = Build(Deck(12), botCount: 2, new SeededDiceRoller(11));
            director.Begin(0f);

            director.Tick(50f);      // bots act
            Assert.AreEqual(0, director.Session.Current.ObserverId,
                "bot turns must never move the private view off the human seat");
            Assert.AreEqual(0, director.Session.LocalPlayer.Value);
        }
    }
}
