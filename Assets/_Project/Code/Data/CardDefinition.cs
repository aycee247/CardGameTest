using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Designer-authored card asset. Wraps the pure <see cref="Card"/> the rules engine uses,
    /// adding presentation data (art, name) that the engine never sees.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "DiceCards/Card Definition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Tooltip("Stable, unique integer id. Used as the network/save key — do not reuse across cards.")]
        [SerializeField] private int cardId;

        [SerializeField] private string displayName;
        [SerializeField] private Sprite artwork;

        [Min(0)]
        [SerializeField] private int points = 1;

        [Tooltip("All requirements must combine per the mode below. A single entry is the common case.")]
        [SerializeField] private CompositeRequirement.Mode combineMode = CompositeRequirement.Mode.All;

        [SerializeField] private List<CardRequirementSpec> requirements = new List<CardRequirementSpec>();

        public int CardId => cardId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Artwork => artwork;
        public int Points => points;

        /// <summary>Builds the netcode-agnostic rules-layer card.</summary>
        public Card ToCard()
        {
            ICardRequirement requirement = BuildRequirement();
            return new Card(new CardId(cardId), DisplayName, requirement, points);
        }

        private ICardRequirement BuildRequirement()
        {
            if (requirements == null || requirements.Count == 0)
                return new SumRequirement(0, ComparisonOp.AtLeast); // trivially claimable placeholder

            if (requirements.Count == 1)
                return requirements[0].Build();

            var built = new ICardRequirement[requirements.Count];
            for (int i = 0; i < requirements.Count; i++)
                built[i] = requirements[i].Build();
            return new CompositeRequirement(combineMode, built);
        }
    }
}
