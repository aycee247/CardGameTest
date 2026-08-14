using System;
using System.Linq;

namespace Game.Core
{
    /// <summary>Read-only view of a market card for presentation.</summary>
    [Serializable]
    public struct CardSnapshot
    {
        public int CardId;
        public string DisplayName;
        public int Tier;
        public string CostText;
        public string PowerText;
        public int Points;

        /// <summary>True if the observing player's unspent dice could pay for it right now (UI-3).</summary>
        public bool AffordableNow;
    }

    /// <summary>Read-only view of one owned card.</summary>
    [Serializable]
    public struct OwnedCardSnapshot
    {
        public int CardId;
        public string DisplayName;
        public string PowerText;
        public int Points;
    }

    /// <summary>
    /// Read-only view of one player.
    ///
    /// Dice faces are public information — seeing that an opponent rolled three 5s is exactly the
    /// read the contested-claim design is built on. The secret is the *commit*, so
    /// <see cref="PendingCardId"/> is populated only for the observer until Reveal (NET-2).
    /// </summary>
    [Serializable]
    public struct PlayerSnapshot
    {
        public int PlayerId;
        public string DisplayName;
        public int SeatIndex;
        public int Score;
        public int Sparks;
        public int CardCount;
        public int[] DiceFaces;
        public bool[] DiceSpent;
        public bool IsConnected;

        /// <summary>How present this player is. Public — the rail shows who has dropped (NET-3).</summary>
        public SeatStatus Status;

        /// <summary>Seconds left on their reconnect window, or zero when not applicable.</summary>
        public float ReconnectSecondsLeft;

        /// <summary>Priority position, 0 = first pick. Public — the whole table reasons about it.</summary>
        public int PriorityRank;

        /// <summary>True once this player has committed or passed. Public; the choice itself is not.</summary>
        public bool HasDecided;

        /// <summary>True if they committed rather than passed. Public from Reveal onward.</summary>
        public bool HasCommitted;

        /// <summary>The claimed card, or -1 when hidden from this observer.</summary>
        public int PendingCardId;

        /// <summary>The dice offered, or empty when hidden from this observer.</summary>
        public int[] PendingDice;

        /// <summary>Free Shape actions left. Only populated for the observer.</summary>
        public int RerollsLeft;
        public int NudgesLeft;
        public int SetsLeft;

        public OwnedCardSnapshot[] Owned;
    }

    /// <summary>
    /// Immutable, serializable projection of <see cref="MatchState"/>, filtered for one recipient.
    ///
    /// This replaces the prototype's single global snapshot. Simultaneous secret commits mean every
    /// player must be sent a *different* view of the same state — broadcasting one projection to
    /// everyone would hand each client its opponents' pending claims and make every contest a
    /// formality (NET-2).
    /// </summary>
    [Serializable]
    public struct MatchSnapshot
    {
        public int ObserverId;
        public RoundPhase Phase;
        public int Round;
        public int TotalRounds;

        public PlayerSnapshot[] Players;
        public CardSnapshot[] Market;
        public int DrawPileCount;

        /// <summary>Priority order by player id, best first.</summary>
        public int[] PriorityOrder;

        /// <summary>Player ids entitled to act during <see cref="RoundPhase.Repick"/>.</summary>
        public int[] RepickContenders;

        public bool IsMatchOver;
        public int WinnerId;

        /// <summary>
        /// Builds the view <paramref name="observer"/> is allowed to see. Callers must build one per
        /// recipient — never build once and broadcast.
        ///
        /// <paramref name="seats"/> is optional and only the server has one; without it a player
        /// reads simply as connected or not, rather than distinguishing a seat that may still come
        /// back from one that has gone.
        /// </summary>
        public static MatchSnapshot For(MatchState state, PlayerId observer, SeatRegistry seats = null, float now = 0f)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var observerState = state.Find(observer);

            // Commits become public the moment they are revealed; before that only their owner sees them.
            bool commitsArePublic = state.Phase == RoundPhase.Reveal || state.Phase == RoundPhase.MatchOver;

            var winner = Scoring.Winner(state);

            return new MatchSnapshot
            {
                ObserverId = observer.Value,
                Phase = state.Phase,
                Round = state.Round,
                TotalRounds = state.Config.Rounds,

                Players = state.Players
                    .Select(p => BuildPlayer(state, p, observer, commitsArePublic, seats, now))
                    .ToArray(),

                Market = state.Market
                    .Select(c => BuildCard(c, observerState))
                    .ToArray(),

                DrawPileCount = state.DrawPileCount,
                PriorityOrder = state.PriorityOrder.Select(id => id.Value).ToArray(),
                RepickContenders = state.RepickContenders.Select(id => id.Value).ToArray(),

                IsMatchOver = state.Phase == RoundPhase.MatchOver,
                WinnerId = winner?.Value ?? -1
            };
        }

        private static PlayerSnapshot BuildPlayer(
            MatchState state, PlayerState p, PlayerId observer, bool commitsArePublic,
            SeatRegistry seats, float now)
        {
            bool isObserver = p.Id == observer;
            bool maySeeCommit = isObserver || commitsArePublic;

            var status = seats != null
                ? seats.StatusOf(p.Id, now)
                : (p.IsConnected ? SeatStatus.Connected : SeatStatus.Abandoned);

            var snapshot = new PlayerSnapshot
            {
                PlayerId = p.Id.Value,
                DisplayName = p.DisplayName,
                SeatIndex = p.SeatIndex,
                Score = p.Score,
                Sparks = p.Sparks,
                CardCount = p.Owned.Count,
                DiceFaces = p.Dice.FacesCopy(),
                DiceSpent = p.Dice.SpentCopy(),
                IsConnected = p.IsConnected,
                Status = status,
                ReconnectSecondsLeft = seats != null ? seats.ReconnectSecondsLeft(p.Id, now) : 0f,
                PriorityRank = state.PriorityRank(p.Id),

                // Whether someone has locked in is public — it is what the opponent rail shows (UI-1).
                HasDecided = p.HasCommitted || p.HasPassed,
                HasCommitted = maySeeCommit && p.HasCommitted,

                PendingCardId = -1,
                PendingDice = Array.Empty<int>(),

                Owned = p.Owned.Select(c => new OwnedCardSnapshot
                {
                    CardId = c.Id.Value,
                    DisplayName = c.DisplayName,
                    PowerText = c.Power.Describe(),
                    Points = c.Points
                }).ToArray()
            };

            if (maySeeCommit && p.Pending.HasValue)
            {
                var pending = p.Pending.Value;
                snapshot.PendingCardId = pending.CardId.Value;
                snapshot.PendingDice = (int[])pending.DiceIndices.Clone();
            }

            if (isObserver)
            {
                snapshot.RerollsLeft = p.Allowance.Rerolls;
                snapshot.NudgesLeft = p.Allowance.Nudges;
                snapshot.SetsLeft = p.Allowance.Sets;
            }

            return snapshot;
        }

        private static CardSnapshot BuildCard(Card card, PlayerState observer)
        {
            bool affordable = false;
            if (observer != null)
            {
                var unspent = observer.Dice.UnspentRoll();
                affordable = unspent.Count > 0 &&
                             CostChecker.Satisfies(card.Cost, unspent.Values, observer.WildFaces(), observer.WildDice());
            }

            return new CardSnapshot
            {
                CardId = card.Id.Value,
                DisplayName = card.DisplayName,
                Tier = card.Tier,
                CostText = card.DescribeCost(),
                PowerText = card.Power.Describe(),
                Points = card.Points,
                AffordableNow = affordable
            };
        }

        /// <summary>This snapshot's own player row. A plain loop — a struct cannot capture `this` in a lambda.</summary>
        public PlayerSnapshot Observer
        {
            get
            {
                var players = Players;
                if (players == null) return default;

                int id = ObserverId;
                for (int i = 0; i < players.Length; i++)
                    if (players[i].PlayerId == id) return players[i];

                return default;
            }
        }
    }
}
