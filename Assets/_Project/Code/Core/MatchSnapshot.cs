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
        public PowerFamily Family;

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
        public PowerFamily Family;
    }

    /// <summary>
    /// One claimed market card during <see cref="RoundPhase.Reveal"/> — who contests it and who
    /// will take it. A projection of <see cref="RulesEngine.PreviewResolution"/>, present only
    /// while commits are public anyway, so it reveals nothing the phase has not (NET-2).
    /// </summary>
    [Serializable]
    public struct RevealSnapshot
    {
        public int CardId;
        public string DisplayName;
        public int Tier;
        public int Points;
        public string PowerText;
        public PowerFamily Family;

        /// <summary>Everyone who committed to this card, best priority first — the winner leads.</summary>
        public int[] ClaimantIds;
        public int WinnerId;
        public bool Contested;
    }

    /// <summary>One row of the final standings, tie-breaks already applied (CARD-3).</summary>
    [Serializable]
    public struct FinalScoreSnapshot
    {
        public int PlayerId;
        public string DisplayName;
        public int CardPoints;
        public int PowerPoints;
        public int Total;
        public int Sparks;
        public int CardCount;

        /// <summary>0 = winner. Ordering matches <see cref="Scoring.FinalScores"/> exactly.</summary>
        public int Rank;
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

        /// <summary>
        /// True once this player has committed or passed — or, during Shape, said they are done
        /// shaping. Public; the choice itself is not.
        /// </summary>
        public bool HasDecided;

        /// <summary>
        /// True once this player has said their dice are final (Shape only). Public, like
        /// <see cref="HasDecided"/> — the rail shows who is holding the phase open. It is what
        /// the Withdraw affordance keys on for a player who is done but has not committed.
        /// </summary>
        public bool DoneShaping;

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

        /// <summary>Faces this player's Wild powers let stand in for anything. Observer-only.</summary>
        public int[] WildFaces;

        /// <summary>How many dice this player may float to any face. Observer-only.</summary>
        public int WildDice;

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
        /// Match rules echo, identical for every observer, so the client and server agree on caps,
        /// costs and phase durations without a second source of truth (CORE-2).
        /// </summary>
        public MatchConfig Config;

        /// <summary>
        /// The pending resolution, one entry per claimed card. Populated only during
        /// <see cref="RoundPhase.Reveal"/> — the same gate that makes commits public.
        /// </summary>
        public RevealSnapshot[] Reveals;

        /// <summary>Final standings, winner first. Populated only once the match is over.</summary>
        public FinalScoreSnapshot[] Standings;

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
                WinnerId = winner?.Value ?? -1,

                Config = state.Config,
                Reveals = state.Phase == RoundPhase.Reveal
                    ? BuildReveals(state)
                    : Array.Empty<RevealSnapshot>(),
                Standings = state.Phase == RoundPhase.MatchOver
                    ? BuildStandings(state)
                    : Array.Empty<FinalScoreSnapshot>()
            };
        }

        private static RevealSnapshot[] BuildReveals(MatchState state)
        {
            var report = RulesEngine.PreviewResolution(state);
            if (report.Outcomes.Count == 0) return Array.Empty<RevealSnapshot>();

            var reveals = new System.Collections.Generic.List<RevealSnapshot>();

            // Outcomes arrive grouped per card, winner first — walk the runs.
            for (int start = 0; start < report.Outcomes.Count;)
            {
                var cardId = report.Outcomes[start].Card;
                int end = start;
                while (end < report.Outcomes.Count && report.Outcomes[end].Card.Value == cardId.Value) end++;

                var claimants = new int[end - start];
                int winnerId = -1;
                for (int i = start; i < end; i++)
                {
                    claimants[i - start] = report.Outcomes[i].Player.Value;
                    if (report.Outcomes[i].Granted) winnerId = report.Outcomes[i].Player.Value;
                }

                var reveal = new RevealSnapshot
                {
                    CardId = cardId.Value,
                    DisplayName = string.Empty,
                    PowerText = string.Empty,
                    ClaimantIds = claimants,
                    WinnerId = winnerId,
                    Contested = claimants.Length > 1
                };

                // The card is still in the market during Reveal; it only leaves when the pass resolves.
                var card = state.Market.FirstOrDefault(c => c.Id.Value == cardId.Value);
                if (card != null)
                {
                    reveal.DisplayName = card.DisplayName;
                    reveal.Tier = card.Tier;
                    reveal.Points = card.Points;
                    reveal.PowerText = card.Power.Describe();
                    reveal.Family = card.Family;
                }

                reveals.Add(reveal);
                start = end;
            }

            return reveals.ToArray();
        }

        private static FinalScoreSnapshot[] BuildStandings(MatchState state)
        {
            var finals = Scoring.FinalScores(state);
            var standings = new FinalScoreSnapshot[finals.Count];

            for (int i = 0; i < finals.Count; i++)
            {
                var s = finals[i];
                standings[i] = new FinalScoreSnapshot
                {
                    PlayerId = s.Player.Value,
                    DisplayName = s.DisplayName,
                    CardPoints = s.CardPoints,
                    PowerPoints = s.PowerPoints,
                    Total = s.Total,
                    Sparks = s.Sparks,
                    CardCount = s.CardCount,
                    Rank = i
                };
            }

            return standings;
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
                // Done-shaping counts only while Shape is the phase being decided (CORE follow-up #44).
                HasDecided = p.HasCommitted || p.HasPassed ||
                             (state.Phase == RoundPhase.Shape && p.DoneShaping),
                DoneShaping = state.Phase == RoundPhase.Shape && p.DoneShaping,
                HasCommitted = maySeeCommit && p.HasCommitted,

                PendingCardId = -1,
                PendingDice = Array.Empty<int>(),
                WildFaces = Array.Empty<int>(),

                Owned = p.Owned.Select(c => new OwnedCardSnapshot
                {
                    CardId = c.Id.Value,
                    DisplayName = c.DisplayName,
                    PowerText = c.Power.Describe(),
                    Points = c.Points,
                    Family = c.Family
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
                snapshot.WildFaces = p.WildFaces().OrderBy(f => f).ToArray();
                snapshot.WildDice = p.WildDice();
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
                Family = card.Family,
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
