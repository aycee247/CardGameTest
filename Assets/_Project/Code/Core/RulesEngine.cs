using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// The single authority over match state transitions.
    ///
    /// Two kinds of entry point live here. Phase transitions (<see cref="BeginRound"/>,
    /// <see cref="BeginCommit"/>, <see cref="BeginReveal"/>, <see cref="ResolveReveal"/>,
    /// <see cref="ResolveRepick"/>, <see cref="RunUpkeep"/>) are driven by whoever owns the clock —
    /// the server in an online match, the session in hot-seat — and are never invoked by a client.
    /// Player commands (<see cref="ApplyShape"/>, <see cref="Commit"/>, <see cref="Pass"/>) validate
    /// before mutating, so a hostile client cannot reach an illegal state.
    ///
    /// Everything is synchronous and side-effect free beyond the passed-in state, which is what
    /// lets the same code run on a host, a dedicated server, and the headless test suite.
    /// </summary>
    public static class RulesEngine
    {
        // ------------------------------------------------------------------ round clock

        /// <summary>
        /// Starts the next round: resizes pools to honour any Capacity cards won last round, rolls
        /// every player's dice at once, and refills free Shape allowances.
        ///
        /// Capacity increases deliberately land here rather than at the moment the card is claimed,
        /// so a card won in round 4 first pays out in round 5 and never adds an unrolled die mid-round.
        /// </summary>
        public static void BeginRound(MatchState state, IDiceRoller roller)
        {
            if (state.Phase != RoundPhase.Roll) return;

            state.Round++;

            foreach (var player in state.Players)
            {
                player.Dice.Resize(player.DiceCapacity(state.Config));
                player.Dice.RollAll(roller);
                player.BeginRound();
            }

            state.SetRepickContenders(null);
            state.Phase = RoundPhase.Shape;
        }

        /// <summary>Closes Shape and opens the secret commit window.</summary>
        public static void BeginCommit(MatchState state)
        {
            if (state.Phase != RoundPhase.Shape) return;
            state.Phase = RoundPhase.Commit;
        }

        /// <summary>
        /// Closes commits and flips them face-up. Nothing is resolved yet — this is the beat the UI
        /// plays before outcomes land (UI-4), and the phase in which every pending commit becomes
        /// visible to every player.
        /// </summary>
        public static void BeginReveal(MatchState state)
        {
            if (state.Phase != RoundPhase.Commit) return;
            state.Phase = RoundPhase.Reveal;
        }

        /// <summary>
        /// Resolves the first contention pass. Moves to <see cref="RoundPhase.Repick"/> when someone
        /// lost a contested card and the market still has something to offer, otherwise to Upkeep.
        /// </summary>
        public static ResolutionReport ResolveReveal(MatchState state)
        {
            if (state.Phase != RoundPhase.Reveal) return ResolutionReport.Empty;

            var report = ResolveOnePass(state);

            bool canRepick = report.Losers.Count > 0 && state.Market.Count > 0;
            state.SetRepickContenders(canRepick ? report.Losers : null);
            state.Phase = canRepick ? RoundPhase.Repick : RoundPhase.Upkeep;

            return report;
        }

        /// <summary>
        /// Resolves the second pass. Anyone still contesting loses for good this round and is left
        /// to the consolation Sparks paid at Upkeep.
        /// </summary>
        public static ResolutionReport ResolveRepick(MatchState state)
        {
            if (state.Phase != RoundPhase.Repick) return ResolutionReport.Empty;

            var report = ResolveOnePass(state);
            state.SetRepickContenders(null);
            state.Phase = RoundPhase.Upkeep;

            return report;
        }

        /// <summary>
        /// Pays out Sparks, refills the market, recomputes priority and advances the round — or ends
        /// the match once the configured round count is played out.
        /// </summary>
        public static void RunUpkeep(MatchState state)
        {
            if (state.Phase != RoundPhase.Upkeep) return;

            var config = state.Config;

            foreach (var player in state.Players)
            {
                int gain = player.Dice.UnspentCount * config.SparksPerUnspentDie;
                gain += player.SparkIncome();

                // A round can be disappointing but never empty (MKT-5).
                if (!player.GainedCardThisRound) gain += config.ConsolationSparks;

                int sparks = player.Sparks + gain;
                player.Sparks = sparks > config.SparkCap ? config.SparkCap : sparks;
            }

            state.RefillMarket();
            state.RecomputePriority();
            state.SetRepickContenders(null);

            state.Phase = state.Round >= config.Rounds ? RoundPhase.MatchOver : RoundPhase.Roll;
        }

        // ------------------------------------------------------------------ player commands

        /// <summary>
        /// Manipulates one die during Shape. Free allowances from powers are always consumed before
        /// Sparks, so a player never pays for something they already had.
        /// </summary>
        public static MoveResult ApplyShape(MatchState state, PlayerId playerId, ShapeAction action, IDiceRoller roller)
        {
            if (state.Phase == RoundPhase.MatchOver) return MoveResult.Fail(MoveFailure.MatchOver);
            if (state.Phase != RoundPhase.Shape) return MoveResult.Fail(MoveFailure.WrongPhase);

            var player = state.Find(playerId);
            if (player == null) return MoveResult.Fail(MoveFailure.UnknownPlayer);

            // Dice backing a pending commit are pledged: re-rolling them afterwards would change
            // what the commit is paying with. Withdraw first to keep shaping.
            if (player.Pending.HasValue) return MoveResult.Fail(MoveFailure.AlreadyCommitted);

            var dice = player.Dice;
            if (!dice.IsValidIndex(action.DieIndex)) return MoveResult.Fail(MoveFailure.NoSuchDie);
            if (dice.IsSpent(action.DieIndex)) return MoveResult.Fail(MoveFailure.DieAlreadySpent);

            var config = state.Config;

            switch (action.Kind)
            {
                case ShapeActionKind.Reroll:
                {
                    if (!TrySpend(player, ref player.Allowance.Rerolls, config.RerollSparkCost))
                        return MoveResult.Fail(MoveFailure.CannotAfford);

                    dice.SetFace(action.DieIndex, roller.Roll(1)[0]);
                    return MoveResult.Ok;
                }

                case ShapeActionKind.Nudge:
                {
                    if (action.Value != 1 && action.Value != -1)
                        return MoveResult.Fail(MoveFailure.NudgeOutOfRange);

                    int target = dice.FaceAt(action.DieIndex) + action.Value;
                    if (target < DiceRoll.MinFace || target > DiceRoll.MaxFace)
                        return MoveResult.Fail(MoveFailure.NudgeOutOfRange);

                    // Nudging is a power only — Sparks cannot buy it.
                    if (player.Allowance.Nudges <= 0) return MoveResult.Fail(MoveFailure.CannotAfford);
                    player.Allowance.Nudges--;

                    dice.SetFace(action.DieIndex, target);
                    return MoveResult.Ok;
                }

                case ShapeActionKind.SetFace:
                {
                    if (action.Value < DiceRoll.MinFace || action.Value > DiceRoll.MaxFace)
                        return MoveResult.Fail(MoveFailure.InvalidFace);

                    if (!TrySpend(player, ref player.Allowance.Sets, config.SetFaceSparkCost))
                        return MoveResult.Fail(MoveFailure.CannotAfford);

                    dice.SetFace(action.DieIndex, action.Value);
                    return MoveResult.Ok;
                }

                default:
                    return MoveResult.Fail(MoveFailure.WrongPhase);
            }
        }

        /// <summary>
        /// Records a secret claim on a market card. The named dice are validated against the card's
        /// cost here, on the server, so an invalid commit never survives to Reveal (MKT-2).
        /// Dice are not marked spent until the claim is actually granted — a player who loses a
        /// contest gets their dice back intact for the re-pick.
        /// </summary>
        public static MoveResult Commit(MatchState state, PlayerId playerId, CardId cardId, IReadOnlyList<int> diceIndices)
        {
            var gate = CheckCommitWindow(state, playerId, out var player);
            if (!gate.Success) return gate;

            if (player.Pending.HasValue) return MoveResult.Fail(MoveFailure.AlreadyCommitted);

            var card = state.FindInMarket(cardId);
            if (card == null) return MoveResult.Fail(MoveFailure.CardNotInMarket);

            if (diceIndices == null || diceIndices.Count == 0) return MoveResult.Fail(MoveFailure.NoDiceOffered);

            // Bound the work before doing any. A player cannot offer more dice than they hold, so
            // anything longer is malformed — and rejecting it up front stops a hostile client from
            // making the server allocate and scan an arbitrarily large array.
            if (diceIndices.Count > player.Dice.Count) return MoveResult.Fail(MoveFailure.DuplicateDie);

            var seen = new HashSet<int>();
            for (int i = 0; i < diceIndices.Count; i++)
            {
                int index = diceIndices[i];
                if (!player.Dice.IsValidIndex(index)) return MoveResult.Fail(MoveFailure.NoSuchDie);
                if (player.Dice.IsSpent(index)) return MoveResult.Fail(MoveFailure.DieAlreadySpent);
                if (!seen.Add(index)) return MoveResult.Fail(MoveFailure.DuplicateDie);
            }

            var offered = player.Dice.Subset(diceIndices);
            if (!CostChecker.Satisfies(card.Cost, offered.Values, player.WildFaces(), player.WildDice()))
                return MoveResult.Fail(MoveFailure.CostNotMet);

            player.Pending = new PendingCommit(cardId, diceIndices.ToArray());
            player.HasPassed = false;
            return MoveResult.Ok;
        }

        /// <summary>Declines to claim anything this pass.</summary>
        public static MoveResult Pass(MatchState state, PlayerId playerId)
        {
            var gate = CheckCommitWindow(state, playerId, out var player);
            if (!gate.Success) return gate;

            player.Pending = null;
            player.HasPassed = true;
            return MoveResult.Ok;
        }

        /// <summary>
        /// Takes back a commit or a pass, freeing the player to shape again and decide differently.
        /// Legal right up until the window closes; after Reveal there is nothing to withdraw.
        /// </summary>
        public static MoveResult Withdraw(MatchState state, PlayerId playerId)
        {
            var gate = CheckCommitWindow(state, playerId, out var player);
            if (!gate.Success) return gate;

            player.Pending = null;
            player.HasPassed = false;
            return MoveResult.Ok;
        }

        /// <summary>
        /// Marks every player who has neither committed nor passed as passed. The server calls this
        /// when a phase timer expires so one stalled or disconnected device cannot hold the table
        /// (CORE-2, NET-3).
        /// </summary>
        public static void AutoPassUndecided(MatchState state)
        {
            if (state.Phase != RoundPhase.Commit && state.Phase != RoundPhase.Repick) return;

            foreach (var player in state.Players)
            {
                if (state.Phase == RoundPhase.Repick && !state.RepickContenders.Contains(player.Id)) continue;
                if (player.Pending.HasValue || player.HasPassed) continue;
                player.HasPassed = true;
            }
        }

        /// <summary>
        /// True once every player who still has a decision to make has made it. Lets a driver close
        /// a window early instead of waiting out the clock — which is how hot-seat play advances.
        /// </summary>
        public static bool AllDecided(MatchState state)
        {
            if (state.Phase == RoundPhase.Shape || state.Phase == RoundPhase.Commit)
                return state.Players.All(p => p.Pending.HasValue || p.HasPassed);

            if (state.Phase == RoundPhase.Repick)
                return state.RepickContenders
                    .Select(state.Find)
                    .All(p => p == null || p.Pending.HasValue || p.HasPassed);

            return false;
        }

        // ------------------------------------------------------------------ internals

        private static MoveResult CheckCommitWindow(MatchState state, PlayerId playerId, out PlayerState player)
        {
            player = null;

            if (state.Phase == RoundPhase.MatchOver) return MoveResult.Fail(MoveFailure.MatchOver);

            // Shape is included deliberately. Locking in before the deadline is a legitimate
            // choice online, and it is what lets a hot-seat player shape and commit in one sitting
            // instead of the device going round the table twice per round.
            if (state.Phase != RoundPhase.Shape &&
                state.Phase != RoundPhase.Commit &&
                state.Phase != RoundPhase.Repick)
                return MoveResult.Fail(MoveFailure.WrongPhase);

            player = state.Find(playerId);
            if (player == null) return MoveResult.Fail(MoveFailure.UnknownPlayer);

            if (state.Phase == RoundPhase.Repick && !state.RepickContenders.Contains(playerId))
                return MoveResult.Fail(MoveFailure.NotAContender);

            return MoveResult.Ok;
        }

        /// <summary>Consumes a free allowance if there is one, otherwise the Spark price.</summary>
        private static bool TrySpend(PlayerState player, ref int allowance, int sparkCost)
        {
            if (allowance > 0)
            {
                allowance--;
                return true;
            }

            if (player.Sparks >= sparkCost)
            {
                player.Sparks -= sparkCost;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves every pending commit. Cards drawing more than one claimant go to the player with
        /// the best priority — lowest score first (MKT-3, MKT-4). Losers keep their dice and are
        /// returned so the caller can offer them a re-pick.
        /// </summary>
        private static ResolutionReport ResolveOnePass(MatchState state)
        {
            var outcomes = new List<ClaimOutcome>();
            var losers = new List<PlayerId>();
            bool hadContention = false;

            // Grouped and ordered by card id so resolution is deterministic on every peer.
            var claims = state.Players
                .Where(p => p.Pending.HasValue)
                .GroupBy(p => p.Pending.Value.CardId.Value)
                .OrderBy(g => g.Key);

            foreach (var group in claims)
            {
                var contenders = group
                    .OrderBy(p => state.PriorityRank(p.Id))
                    .ToList();

                if (contenders.Count > 1) hadContention = true;

                var cardId = new CardId(group.Key);
                var card = state.TakeFromMarket(cardId);

                if (card == null)
                {
                    // Defensive: the market is only drained here, so this should be unreachable.
                    foreach (var p in contenders)
                    {
                        outcomes.Add(new ClaimOutcome(p.Id, cardId, false));
                        losers.Add(p.Id);
                    }
                    continue;
                }

                var winner = contenders[0];
                winner.OwnedCards.Add(card);
                winner.Dice.MarkSpent(winner.Pending.Value.DiceIndices);
                winner.GainedCardThisRound = true;
                outcomes.Add(new ClaimOutcome(winner.Id, cardId, true));

                for (int i = 1; i < contenders.Count; i++)
                {
                    outcomes.Add(new ClaimOutcome(contenders[i].Id, cardId, false));
                    losers.Add(contenders[i].Id);
                }
            }

            state.ClearPendingCommits();
            return new ResolutionReport(outcomes, losers, hadContention);
        }
    }
}
