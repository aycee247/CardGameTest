using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The round clock (CORE-1..4): every player acts in every phase, phases advance in a fixed
    /// order, and the match runs a fixed number of rounds.
    /// </summary>
    public class RoundFlowTests
    {
        [Test]
        public void BeginRound_RollsEveryPlayerSimultaneously()
        {
            var state = Make.Match(Make.Config(), new[] { Make.Pair(1) });
            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 1, 2, 3, 4 },
                new[] { 5, 6, 1, 2 }));

            Assert.AreEqual(RoundPhase.Roll, state.Phase);
            Assert.AreEqual(0, state.Round);

            session.Advance();

            Assert.AreEqual(RoundPhase.Shape, state.Phase);
            Assert.AreEqual(1, state.Round);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, state.Players[0].Dice.FacesCopy());
            CollectionAssert.AreEqual(new[] { 5, 6, 1, 2 }, state.Players[1].Dice.FacesCopy());
        }

        [Test]
        public void PhasesAdvanceInOrder_AndSkipRepickWhenUncontested()
        {
            var state = Make.Match(Make.Config(), new[] { Make.Pair(1), Make.Pair(2) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));

            Assert.AreEqual(RoundPhase.Shape, session.Advance());
            Assert.AreEqual(RoundPhase.Commit, session.Advance());
            Assert.AreEqual(RoundPhase.Reveal, session.Advance());

            // Nobody claimed anything, so there is nothing to contest and no re-pick.
            Assert.AreEqual(RoundPhase.Upkeep, session.Advance());
            Assert.AreEqual(RoundPhase.Roll, session.Advance());

            // Upkeep parks the match at Roll; the round counter only moves when that Roll resolves.
            Assert.AreEqual(1, state.Round);
            session.Advance();
            Assert.AreEqual(2, state.Round);
        }

        [Test]
        public void UndecidedPlayersAreAutoPassedWhenTheCommitWindowCloses()
        {
            var state = Make.Match(Make.Config(), new[] { Make.Pair(1), Make.Pair(2) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));
            session.AdvanceTo(RoundPhase.Commit);

            Assert.IsFalse(RulesEngine.AllDecided(state));

            // One player answers, the other stalls; the window closing must not block the table.
            session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            session.Advance();

            Assert.AreEqual(RoundPhase.Reveal, state.Phase);
            Assert.IsTrue(state.Players[1].HasPassed);
        }

        [Test]
        public void CapacityCardAppliesFromTheFollowingRound()
        {
            var config = Make.Config(rounds: 3, marketSize: 1);
            var capacity = Make.Pair(1, points: 1, power: CardPower.ExtraDie(), family: PowerFamily.Capacity);
            var state = Make.Match(config, new[] { capacity, Make.Pair(2) });

            var session = new LocalMatchSession(state, new ScriptedRoller(
                new[] { 6, 6, 1, 2 }, new[] { 1, 2, 3, 4 },   // round 1: four dice each
                new[] { 1, 1, 1, 1, 1 }, new[] { 2, 2, 2, 2 })); // round 2: P0 now has five

            session.AdvanceTo(RoundPhase.Commit);
            Assert.IsTrue(session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 }).Success);

            session.AdvanceTo(RoundPhase.Upkeep);

            // The card is owned immediately, but the extra die must not appear mid-round —
            // it would be an unrolled die the player could spend.
            Assert.AreEqual(1, state.Players[0].Owned.Count);
            Assert.AreEqual(4, state.Players[0].Dice.Count);
            Assert.AreEqual(5, state.Players[0].DiceCapacity(config));

            session.Advance();  // Upkeep -> Roll
            session.Advance();  // Roll    -> Shape, pools resized

            Assert.AreEqual(5, state.Players[0].Dice.Count);
            Assert.AreEqual(4, state.Players[1].Dice.Count);
        }

        [Test]
        public void DiceCapacityIsCappedAtMaxDice()
        {
            var config = new MatchConfig { StartingDice = 4, MaxDice = 5 };
            var player = new PlayerState(new PlayerId(0), "P0");

            Make.Grant(player, Make.Pair(1, power: CardPower.ExtraDie()));
            Make.Grant(player, Make.Pair(2, power: CardPower.ExtraDie()));
            Make.Grant(player, Make.Pair(3, power: CardPower.ExtraDie()));

            Assert.AreEqual(5, player.DiceCapacity(config));
        }

        [Test]
        public void MatchEndsAfterConfiguredRounds()
        {
            var config = Make.Config(rounds: 2);
            var state = Make.Match(config, new[] { Make.Pair(1), Make.Pair(2) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));

            for (int i = 0; i < 32 && state.Phase != RoundPhase.MatchOver; i++) session.Advance();

            Assert.AreEqual(RoundPhase.MatchOver, state.Phase);
            Assert.AreEqual(2, state.Round);
        }

        [Test]
        public void CommandsAreRejectedOnceTheMatchIsOver()
        {
            var state = Make.Match(Make.Config(rounds: 1), new[] { Make.Pair(1) });
            var session = new LocalMatchSession(state, new ConstantRoller(3));

            for (int i = 0; i < 32 && state.Phase != RoundPhase.MatchOver; i++) session.Advance();

            var shaped = session.Shape(new PlayerId(0), ShapeAction.Reroll(0));
            Assert.IsFalse(shaped.Success);
            Assert.AreEqual(MoveFailure.MatchOver, shaped.Failure);

            var committed = session.Commit(new PlayerId(0), new CardId(1), new[] { 0, 1 });
            Assert.IsFalse(committed.Success);
            Assert.AreEqual(MoveFailure.MatchOver, committed.Failure);
        }
    }
}
