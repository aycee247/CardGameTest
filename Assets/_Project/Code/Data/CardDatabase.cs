using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The catalog of all cards in the game. Acts as the deck source for a match and the
    /// lookup table for turning network/save <see cref="CardId"/>s back into presentation data.
    /// Load via Addressables for patchable content later.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Foundry/Card Database")]
    public sealed class CardDatabase : ScriptableObject
    {
        [SerializeField] private List<CardDefinition> cards = new List<CardDefinition>();

        public IReadOnlyList<CardDefinition> Cards => cards;

        private Dictionary<int, CardDefinition> _byId;

        private void OnEnable() => RebuildIndex();

        public void RebuildIndex()
        {
            _byId = new Dictionary<int, CardDefinition>();
            foreach (var c in cards)
            {
                if (c == null) continue;
                _byId[c.CardId] = c;
            }
        }

        public CardDefinition Find(CardId id)
        {
            if (_byId == null) RebuildIndex();
            _byId.TryGetValue(id.Value, out var def);
            return def;
        }

        /// <summary>Materializes every card, in authoring order.</summary>
        public List<Card> BuildDeck()
        {
            var deck = new List<Card>(cards.Count);
            foreach (var c in cards)
                if (c != null) deck.Add(c.ToCard());
            return deck;
        }

        /// <summary>
        /// Materializes the deck the <see cref="MatchState"/> is built from: shuffled *within* each
        /// tier, then concatenated Tier 1 → Tier 3.
        ///
        /// Shuffling within tiers rather than across them is what makes the market escalate over
        /// the match (MKT-1) while still dealing a different sequence every game.
        ///
        /// Driven by the portable <see cref="XorShift64Star"/>, never <see cref="System.Random"/>,
        /// so the deck order a seed produces is identical on every platform and runtime the way
        /// dice already are (STORY-6.5).
        /// </summary>
        public List<Card> BuildShuffledDeck(ref XorShift64Star rng)
        {
            var byTier = new SortedDictionary<int, List<Card>>();

            foreach (var def in cards)
            {
                if (def == null) continue;
                if (!byTier.TryGetValue(def.Tier, out var bucket))
                {
                    bucket = new List<Card>();
                    byTier[def.Tier] = bucket;
                }
                bucket.Add(def.ToCard());
            }

            var deck = new List<Card>(cards.Count);
            foreach (var bucket in byTier.Values)
            {
                rng.Shuffle(bucket);
                deck.AddRange(bucket);
            }
            return deck;
        }
    }
}
