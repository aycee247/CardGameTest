using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>One player's final tally, broken down so the results screen can show the arithmetic.</summary>
    public readonly struct FinalScore
    {
        public readonly PlayerId Player;
        public readonly string DisplayName;

        /// <summary>Victory points printed on the cards themselves.</summary>
        public readonly int CardPoints;

        /// <summary>Victory points contributed by end-game scoring powers.</summary>
        public readonly int PowerPoints;

        public readonly int CardCount;
        public readonly int Sparks;

        public int Total => CardPoints + PowerPoints;

        public FinalScore(PlayerId player, string displayName, int cardPoints, int powerPoints, int cardCount, int sparks)
        {
            Player = player;
            DisplayName = displayName;
            CardPoints = cardPoints;
            PowerPoints = powerPoints;
            CardCount = cardCount;
            Sparks = sparks;
        }

        public override string ToString() =>
            $"{DisplayName} {Total}vp ({CardPoints}+{PowerPoints}), {CardCount} cards, {Sparks} sparks";
    }

    /// <summary>
    /// End-of-match scoring (CARD-3). Kept separate from <see cref="RulesEngine"/> because it is a
    /// pure projection — it reads final state and never mutates it, so a results screen or a
    /// reconnecting client can compute it independently and get the same answer.
    /// </summary>
    public static class Scoring
    {
        /// <summary>
        /// Final standings, winner first. Ordered by total, then Sparks, then cards held, then seat —
        /// a total order, so there is never an ambiguous winner.
        /// </summary>
        public static IReadOnlyList<FinalScore> FinalScores(MatchState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            return state.Players
                .Select(Score)
                .OrderByDescending(s => s.Total)
                .ThenByDescending(s => s.Sparks)
                .ThenByDescending(s => s.CardCount)
                .ThenBy(s => SeatOf(state, s.Player))
                .ToList();
        }

        /// <summary>The winner once the match is over, or null while it is still running.</summary>
        public static PlayerId? Winner(MatchState state)
        {
            if (state.Phase != RoundPhase.MatchOver) return null;
            var standings = FinalScores(state);
            return standings.Count > 0 ? standings[0].Player : (PlayerId?)null;
        }

        public static FinalScore Score(PlayerState player)
        {
            int cardPoints = 0;
            int powerPoints = 0;

            for (int i = 0; i < player.Owned.Count; i++)
                cardPoints += player.Owned[i].Points;

            for (int i = 0; i < player.Owned.Count; i++)
            {
                var power = player.Owned[i].Power;
                switch (power.Kind)
                {
                    case PowerKind.FlatScore:
                        powerPoints += power.Magnitude;
                        break;

                    // Counts every owned card of that family, including the scoring card itself
                    // when the families happen to match.
                    case PowerKind.ScorePerFamily:
                        powerPoints += power.Magnitude * player.CountFamily(power.CountsFamily);
                        break;
                }
            }

            return new FinalScore(player.Id, player.DisplayName, cardPoints, powerPoints, player.Owned.Count, player.Sparks);
        }

        private static int SeatOf(MatchState state, PlayerId id) => state.Find(id)?.SeatIndex ?? int.MaxValue;
    }
}
