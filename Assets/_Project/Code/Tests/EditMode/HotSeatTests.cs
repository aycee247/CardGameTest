using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Pass-the-device play. The awkward parts are the privacy boundary between seats and the
    /// re-pick pass, which only some players are entitled to join.
    /// </summary>
    public class HotSeatTests
    {
        private static HotSeatDirector Director(out MatchState state, int players = 3, IDiceRoller roller = null)
        {
            state = Make.Match(Make.Config(rounds: 2, marketSize: 3),
                new[] { Make.Pair(1, points: 5), Make.Pair(2, points: 3), Make.Pair(3, points: 1) },
                players);

            var session = new LocalMatchSession(state, roller ?? new ConstantRoller(4));
            return new HotSeatDirector(session);
        }

        /// <summary>
        /// A hot-seat match is untimed by design (STORY-2.7): SecondsLeft must be negative, the
        /// signal every timer surface reads as "hide". Zero would be worse than wrong — the Done
        /// square would render an expired ring pulsing with urgency for the whole match.
        /// </summary>
        [Test]
        public void UntimedMatchReportsNegativeSecondsLeft()
        {
            var state = Make.Match(Make.Config(rounds: 2, marketSize: 3),
                new[] { Make.Pair(1, points: 5) }, 2);

            var session = new LocalMatchSession(state, new ConstantRoller(4));
            Assert.Less(session.SecondsLeft, 0f);
        }

        [Test]
        public void BeginStartsRoundOneWaitingForTheFirstSeat()
        {
            var director = Director(out var state);
            director.Begin();

            Assert.AreEqual(HotSeatStage.Handoff, director.Stage);
            Assert.AreEqual(new PlayerId(0), director.CurrentActor);
            Assert.AreEqual(1, state.Round);
            Assert.AreEqual(RoundPhase.Shape, state.Phase);
            Assert.IsFalse(director.IsRepickPass);
        }

        [Test]
        public void TheDeviceVisitsEverySeatThenReveals()
        {
            var director = Director(out var state);
            director.Begin();

            var visited = new System.Collections.Generic.List<int>();
            while (director.Stage != HotSeatStage.Reveal)
            {
                visited.Add(director.CurrentActor.Value);
                director.ConfirmHandoff();
                director.EndActing();
            }

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, visited);
            Assert.AreEqual(HotSeatStage.Reveal, director.Stage);
        }

        [Test]
        public void APlayerWhoDoesNothingIsPassed()
        {
            var director = Director(out var state);
            director.Begin();

            director.ConfirmHandoff();
            Assert.IsFalse(director.CurrentActorHasDecided);
            director.EndActing();

            Assert.IsTrue(state.Players[0].HasPassed);
        }

        [Test]
        public void ShapeAndCommitHappenInOneSittingPerSeat()
        {
            var director = Director(out var state);
            director.Begin();
            director.ConfirmHandoff();

            var actor = state.Find(director.CurrentActor);
            actor.Sparks = 10;

            // Both halves of a turn while the engine is still in the Shape phase — this is the
            // whole reason commits are legal during Shape.
            Assert.IsTrue(director.Session.Shape(actor.Id, ShapeAction.SetFace(0, 6)).Success);
            Assert.IsTrue(director.Session.Shape(actor.Id, ShapeAction.SetFace(1, 6)).Success);
            Assert.IsTrue(director.Session.Commit(actor.Id, new CardId(1), new[] { 0, 1 }).Success);

            Assert.IsTrue(director.CurrentActorHasDecided);
            Assert.AreEqual(RoundPhase.Shape, state.Phase);
        }

        [Test]
        public void OneSeatsCommitIsGoneFromTheViewBeforeTheNextSeatLooks()
        {
            var director = Director(out var state);
            director.Begin();
            director.ConfirmHandoff();

            director.Session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            Assert.AreEqual(1, director.Session.Current.Observer.PendingCardId);

            director.EndActing();

            // The handoff screen is up for P1, and P0's pick must already be out of the snapshot.
            Assert.AreEqual(HotSeatStage.Handoff, director.Stage);
            Assert.AreEqual(new PlayerId(1), director.CurrentActor);

            var view = director.Session.Current;
            Assert.AreEqual(1, view.ObserverId);

            var p0Row = view.Players.First(p => p.PlayerId == 0);
            Assert.AreEqual(-1, p0Row.PendingCardId);
            Assert.IsEmpty(p0Row.PendingDice);
            Assert.IsTrue(p0Row.HasDecided, "that P0 has locked in is still public");
        }

        [Test]
        public void ARepickPassOnlyQueuesTheLosers()
        {
            var director = Director(out var state, players: 3);
            director.Begin();

            // All three chase the same card.
            for (int seat = 0; seat < 3; seat++)
            {
                director.ConfirmHandoff();
                director.Session.Commit(new PlayerId(seat), new CardId(1), new[] { 0, 1 });
                director.EndActing();
            }

            // The pass holds at Reveal: nothing is resolved yet, but the snapshot carries the
            // preview the spotlight shows — and the preview already knows the contest.
            Assert.AreEqual(HotSeatStage.Reveal, director.Stage);
            Assert.AreEqual(RoundPhase.Reveal, state.Phase);
            Assert.AreEqual(0, state.Players[0].Owned.Count, "nothing resolves until the reveal is left");
            var preview = director.Session.Current.Reveals;
            Assert.AreEqual(1, preview.Length);
            Assert.IsTrue(preview[0].Contested);

            director.ContinueFromReveal();

            Assert.IsTrue(director.LastResolution.HadContention, "leaving the reveal applies it");
            Assert.AreEqual(HotSeatStage.Handoff, director.Stage);
            Assert.IsTrue(director.IsRepickPass);
            CollectionAssert.AreEqual(new[] { 1, 2 }, director.Queue.Select(p => p.Value).ToArray());
            Assert.AreEqual(1, state.Players[0].Owned.Count, "the priority holder has now won");
        }

        [Test]
        public void RepickResolvesThenReachesTheSummary()
        {
            var director = Director(out var state, players: 3);
            director.Begin();

            for (int seat = 0; seat < 3; seat++)
            {
                director.ConfirmHandoff();
                director.Session.Commit(new PlayerId(seat), new CardId(1), new[] { 0, 1 });
                director.EndActing();
            }

            director.ContinueFromReveal();

            // Both losers take something else this time.
            director.ConfirmHandoff();
            director.Session.Commit(new PlayerId(1), new CardId(2), new[] { 0, 1 });
            director.EndActing();

            director.ConfirmHandoff();
            director.Session.Commit(new PlayerId(2), new CardId(3), new[] { 0, 1 });
            director.EndActing();

            // The second pass has no reveal window (its outcome shows in the summary), so the
            // repick close resolves immediately and lands on the summary.
            Assert.AreEqual(HotSeatStage.RoundSummary, director.Stage);
            Assert.AreEqual(1, state.Players[1].Owned.Count);
            Assert.AreEqual(1, state.Players[2].Owned.Count);
        }

        [Test]
        public void SummaryStartsTheNextRoundAndRequeuesEveryone()
        {
            var director = Director(out var state);
            director.Begin();

            for (int seat = 0; seat < 3; seat++)
            {
                director.ConfirmHandoff();
                director.EndActing();
            }

            director.ContinueFromReveal();
            Assert.AreEqual(HotSeatStage.RoundSummary, director.Stage);

            director.ContinueFromSummary();

            Assert.AreEqual(HotSeatStage.Handoff, director.Stage);
            Assert.AreEqual(2, state.Round);
            Assert.AreEqual(RoundPhase.Shape, state.Phase);
            Assert.IsFalse(director.IsRepickPass);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, director.Queue.Select(p => p.Value).ToArray());
        }

        [Test]
        public void AWholeHotSeatMatchReachesMatchOver()
        {
            var state = Make.Match(new MatchConfig { Rounds = 4, MarketSize = 5, StartingDice = 4 },
                Enumerable.Range(1, 40)
                    .Select(i => Make.Card(i, new NOfAKindRequirement(2), points: 1,
                        power: CardPower.ExtraDie(), family: PowerFamily.Capacity))
                    .ToList(),
                playerCount: 4);

            var director = new HotSeatDirector(new LocalMatchSession(state, new SeededDiceRoller(4242)));
            director.Begin();

            int guard = 0;
            while (director.Stage != HotSeatStage.MatchOver)
            {
                switch (director.Stage)
                {
                    case HotSeatStage.Handoff:
                        director.ConfirmHandoff();
                        break;

                    case HotSeatStage.Acting:
                        var actor = state.Find(director.CurrentActor);
                        foreach (var card in state.Market.ToList())
                        {
                            var payment = Pay.Find(actor, card);
                            if (payment == null) continue;
                            if (director.Session.Commit(actor.Id, card.Id, payment).Success) break;
                        }
                        director.EndActing();
                        break;

                    case HotSeatStage.Reveal:
                        director.ContinueFromReveal();
                        break;

                    case HotSeatStage.RoundSummary:
                        director.ContinueFromSummary();
                        break;
                }

                if (++guard > 2000) Assert.Fail("hot-seat flow failed to terminate");
            }

            Assert.AreEqual(4, state.Round);
            Assert.AreEqual(RoundPhase.MatchOver, state.Phase);
            Assert.AreEqual(4, director.FinalScores().Count);
            Assert.Greater(state.Players.Sum(p => p.Owned.Count), 0);
        }
    }
}
