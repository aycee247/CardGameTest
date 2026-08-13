using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>A single dice manipulation during the Shape phase.</summary>
    public enum ShapeActionKind
    {
        /// <summary>Re-roll one die. Free from a power, otherwise costs Sparks.</summary>
        Reroll,
        /// <summary>Move one die ±1. Powers only — Sparks cannot buy a nudge.</summary>
        Nudge,
        /// <summary>Set one die to a chosen face. Free from a power, otherwise costs Sparks.</summary>
        SetFace
    }

    /// <summary>Player intent to manipulate one die. Validated and applied by <see cref="RulesEngine"/>.</summary>
    [Serializable]
    public readonly struct ShapeAction
    {
        public readonly ShapeActionKind Kind;
        public readonly int DieIndex;

        /// <summary>The delta for a nudge (+1/-1), or the target face for a set. Unused for a re-roll.</summary>
        public readonly int Value;

        private ShapeAction(ShapeActionKind kind, int dieIndex, int value)
        {
            Kind = kind;
            DieIndex = dieIndex;
            Value = value;
        }

        public static ShapeAction Reroll(int dieIndex) => new ShapeAction(ShapeActionKind.Reroll, dieIndex, 0);
        public static ShapeAction Nudge(int dieIndex, int delta) => new ShapeAction(ShapeActionKind.Nudge, dieIndex, delta);
        public static ShapeAction SetFace(int dieIndex, int face) => new ShapeAction(ShapeActionKind.SetFace, dieIndex, face);

        public override string ToString() => $"{Kind}(die {DieIndex}{(Kind == ShapeActionKind.Reroll ? "" : ", " + Value)})";
    }

    /// <summary>Why the rules engine refused a command. Surfaced to the offending client only.</summary>
    public enum MoveFailure
    {
        None = 0,
        WrongPhase,
        UnknownPlayer,
        MatchOver,

        // Shape
        NoSuchDie,
        DieAlreadySpent,
        InvalidFace,
        NudgeOutOfRange,
        CannotAfford,

        // Commit
        AlreadyCommitted,
        CardNotInMarket,
        NoDiceOffered,
        DuplicateDie,
        CostNotMet,

        // Repick
        NotAContender
    }

    /// <summary>Result of applying a command through the rules engine.</summary>
    [Serializable]
    public readonly struct MoveResult
    {
        public readonly bool Success;
        public readonly MoveFailure Failure;

        private MoveResult(bool success, MoveFailure failure)
        {
            Success = success;
            Failure = failure;
        }

        public static readonly MoveResult Ok = new MoveResult(true, MoveFailure.None);
        public static MoveResult Fail(MoveFailure reason) => new MoveResult(false, reason);

        public override string ToString() => Success ? "OK" : $"FAIL({Failure})";
    }

    /// <summary>What happened to one player's commit when the pass resolved.</summary>
    public readonly struct ClaimOutcome
    {
        public readonly PlayerId Player;
        public readonly CardId Card;

        /// <summary>False when another player held higher priority on the same card.</summary>
        public readonly bool Granted;

        public ClaimOutcome(PlayerId player, CardId card, bool granted)
        {
            Player = player;
            Card = card;
            Granted = granted;
        }

        public override string ToString() => $"{Player} {(Granted ? "won" : "lost")} {Card}";
    }

    /// <summary>
    /// The outcome of one contention pass. <see cref="Losers"/> are the players eligible to re-pick;
    /// their dice were never spent, so they commit again from scratch.
    /// </summary>
    public sealed class ResolutionReport
    {
        public IReadOnlyList<ClaimOutcome> Outcomes { get; }
        public IReadOnlyList<PlayerId> Losers { get; }

        /// <summary>True when at least one card drew more than one claimant.</summary>
        public bool HadContention { get; }

        public ResolutionReport(IReadOnlyList<ClaimOutcome> outcomes, IReadOnlyList<PlayerId> losers, bool hadContention)
        {
            Outcomes = outcomes ?? Array.Empty<ClaimOutcome>();
            Losers = losers ?? Array.Empty<PlayerId>();
            HadContention = hadContention;
        }

        public static ResolutionReport Empty =>
            new ResolutionReport(Array.Empty<ClaimOutcome>(), Array.Empty<PlayerId>(), false);
    }
}
