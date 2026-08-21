using System;

namespace Game.Core
{
    /// <summary>
    /// The phases of a single round. Every player acts in the same phase at the same time —
    /// there is no turn order and no per-player phase.
    /// </summary>
    public enum RoundPhase
    {
        /// <summary>Automatic. The server rolls every player's pool at once.</summary>
        Roll,
        /// <summary>Input. Players re-roll, nudge and set dice using powers and Sparks.</summary>
        Shape,
        /// <summary>Input. Players secretly commit to one market card, or pass.</summary>
        Commit,
        /// <summary>Commits are revealed together and the first contention pass resolves.</summary>
        Reveal,
        /// <summary>Input. Players who lost a contested card pick again from what remains.</summary>
        Repick,
        /// <summary>Automatic. Sparks, market refill, priority, round advance.</summary>
        Upkeep,
        /// <summary>The configured number of rounds has been played.</summary>
        MatchOver
    }

    /// <summary>
    /// Tunable match rules, owned by the authoritative server and replicated to clients so both
    /// sides evaluate legality identically. Defaults are the values specced in docs/game-design.md.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        /// <summary>Fixed-length match (CORE-1). Every player gets the same number of rounds.</summary>
        public int Rounds = 10;

        public int StartingDice = 4;

        /// <summary>Hard ceiling on the dice pool (CORE-3), for balance and phone layout.</summary>
        public int MaxDice = 8;

        public int MarketSize = 5;

        public int SparkCap = 10;

        /// <summary>Sparks awarded per unspent die at Upkeep, so no roll is ever wasted (CORE-4).</summary>
        public int SparksPerUnspentDie = 1;

        /// <summary>Awarded to any player who ends a round without gaining a card (MKT-5).</summary>
        public int ConsolationSparks = 3;

        public int RerollSparkCost = 2;
        public int SetFaceSparkCost = 4;

        /// <summary>
        /// Phase durations in seconds. The rules layer does not tick a clock — it is pure and
        /// synchronous — but it carries the durations so the server timer and the UI agree
        /// on one authoritative source (CORE-2).
        /// </summary>
        public int ShapeSeconds = 20;
        public int CommitSeconds = 15;
        public int RepickSeconds = 10;

        /// <summary>
        /// Automatic-phase beats in seconds. Reveal scales with the number of claimed cards so the
        /// one-contest-at-a-time reveal sequence fits inside the phase (UI-4).
        /// </summary>
        public float RollSeconds = 1.5f;
        public float RevealBaseSeconds = 2.5f;
        public float RevealPerClaimSeconds = 2.4f;
        public float UpkeepSeconds = 4f;

        /// <summary>
        /// Full duration of a phase. The server clock and the client's timer ring both read this,
        /// so neither side ever guesses a denominator (CORE-2).
        /// </summary>
        public float DurationOf(RoundPhase phase, int revealClaims = 0)
        {
            switch (phase)
            {
                case RoundPhase.Roll: return RollSeconds;
                case RoundPhase.Shape: return ShapeSeconds;
                case RoundPhase.Commit: return CommitSeconds;
                case RoundPhase.Reveal: return RevealBaseSeconds + RevealPerClaimSeconds * revealClaims;
                case RoundPhase.Repick: return RepickSeconds;
                case RoundPhase.Upkeep: return UpkeepSeconds;
                default: return float.PositiveInfinity;
            }
        }

        public static MatchConfig Default => new MatchConfig();
    }
}
