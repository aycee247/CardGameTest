using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// The authoritative mutable match state. It is only ever mutated through <see cref="RulesEngine"/>,
    /// so every transition passes validation. Clients read it through <see cref="MatchSnapshot"/>,
    /// which filters hidden information per recipient.
    ///
    /// State is indexed by round, not by turn: there is no "current player".
    /// </summary>
    public sealed class MatchState
    {
        private readonly List<PlayerState> _players;
        private readonly List<Card> _market;
        private readonly Queue<Card> _drawPile;
        private readonly List<PlayerId> _priority;
        private readonly List<PlayerId> _repickContenders = new List<PlayerId>();

        public MatchConfig Config { get; }
        public RoundPhase Phase { get; internal set; }

        /// <summary>1-based. Zero before the first round has begun.</summary>
        public int Round { get; internal set; }

        public IReadOnlyList<PlayerState> Players => _players;
        public IReadOnlyList<Card> Market => _market;
        public int DrawPileCount => _drawPile.Count;

        /// <summary>Claim order, best first. Recomputed every Upkeep (MKT-4).</summary>
        public IReadOnlyList<PlayerId> PriorityOrder => _priority;

        /// <summary>Players entitled to commit again during <see cref="RoundPhase.Repick"/>.</summary>
        public IReadOnlyList<PlayerId> RepickContenders => _repickContenders;

        /// <summary>
        /// Builds a match. <paramref name="deck"/> is consumed in order, so it should already be
        /// tier-ordered (Tier 1 first) — that ordering is what makes the market escalate (MKT-1).
        /// </summary>
        public MatchState(MatchConfig config, IEnumerable<PlayerState> players, IEnumerable<Card> deck)
        {
            Config = config ?? MatchConfig.Default;

            _players = players?.ToList() ?? throw new ArgumentNullException(nameof(players));
            if (_players.Count < 1) throw new ArgumentException("At least one player required", nameof(players));

            _drawPile = new Queue<Card>(deck ?? Enumerable.Empty<Card>());
            _market = new List<Card>();
            RefillMarket();

            foreach (var p in _players)
                p.Dice = new DicePool(p.DiceCapacity(Config));

            _priority = _players.Select(p => p.Id).ToList();
            RecomputePriority();

            Round = 0;
            Phase = RoundPhase.Roll;
        }

        public PlayerState Find(PlayerId id)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Id == id) return _players[i];
            return null;
        }

        /// <summary>Position in the claim order — lower wins a contested card.</summary>
        public int PriorityRank(PlayerId id)
        {
            for (int i = 0; i < _priority.Count; i++)
                if (_priority[i] == id) return i;
            return int.MaxValue;
        }

        public bool IsFinalRound => Round >= Config.Rounds;

        /// <summary>
        /// Orders players by lowest score first, then fewest cards, then seat index. Handing first
        /// pick to whoever is behind is the catch-up mechanism, built into the core loop (MKT-4).
        /// Seat index makes the order total, so the same state always yields the same order on
        /// server and client.
        /// </summary>
        internal void RecomputePriority()
        {
            _priority.Clear();
            _priority.AddRange(_players
                .OrderBy(p => p.Score)
                .ThenBy(p => p.OwnedCards.Count)
                .ThenBy(p => p.SeatIndex)
                .Select(p => p.Id));
        }

        internal Card FindInMarket(CardId id)
        {
            for (int i = 0; i < _market.Count; i++)
                if (_market[i].Id == id) return _market[i];
            return null;
        }

        internal Card TakeFromMarket(CardId id)
        {
            for (int i = 0; i < _market.Count; i++)
            {
                if (_market[i].Id != id) continue;
                var card = _market[i];
                _market.RemoveAt(i);
                return card;
            }
            return null;
        }

        internal void RefillMarket()
        {
            while (_market.Count < Config.MarketSize && _drawPile.Count > 0)
                _market.Add(_drawPile.Dequeue());
        }

        internal void SetRepickContenders(IEnumerable<PlayerId> contenders)
        {
            _repickContenders.Clear();
            if (contenders != null) _repickContenders.AddRange(contenders);
        }

        internal void ClearPendingCommits()
        {
            foreach (var p in _players)
            {
                p.Pending = null;
                p.HasPassed = false;
            }
        }

        public override string ToString() => $"Round {Round}/{Config.Rounds} {Phase}";
    }
}
