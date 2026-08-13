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
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "DiceCards/Card Database")]
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

        /// <summary>Materializes the rules-layer deck the <see cref="MatchState"/> is built from.</summary>
        public List<Card> BuildDeck()
        {
            var deck = new List<Card>(cards.Count);
            foreach (var c in cards)
                if (c != null) deck.Add(c.ToCard());
            return deck;
        }
    }
}
