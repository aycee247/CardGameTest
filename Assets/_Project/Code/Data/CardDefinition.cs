using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Designer-authored card asset. Wraps the pure <see cref="Card"/> the rules engine uses,
    /// adding presentation data (art, name) that the engine never sees.
    ///
    /// A card is three things: what it costs in dice, what it permanently does for its owner, and
    /// what it is worth at the end.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "Foundry/Card Definition")]
    public sealed class CardDefinition : ScriptableObject
    {
        [Tooltip("Stable, unique integer id. Used as the network/save key — do not reuse across cards.")]
        [SerializeField] private int cardId;

        [SerializeField] private string displayName;
        [SerializeField] private Sprite artwork;

        [Header("Placement")]
        [Tooltip("1-3. The deck is built in tier order, which is what makes the market escalate.")]
        [Range(1, 3)]
        [SerializeField] private int tier = 1;

        [Tooltip("Category. Counted by ScorePerFamily powers and used to group the UI.")]
        [SerializeField] private PowerFamily family = PowerFamily.Capacity;

        [Header("Value")]
        [Min(0)]
        [SerializeField] private int points = 1;

        [Header("Power")]
        [SerializeField] private CardPowerSpec power = new CardPowerSpec { kind = PowerKind.None };

        [Header("Cost")]
        [Tooltip("All requirements must combine per the mode below. A single entry is the common case.")]
        [SerializeField] private CompositeRequirement.Mode combineMode = CompositeRequirement.Mode.All;

        [SerializeField] private List<CardRequirementSpec> requirements = new List<CardRequirementSpec>();

        public int CardId => cardId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Artwork => artwork;
        public int Points => points;
        public int Tier => tier;
        public PowerFamily Family => family;

        /// <summary>Builds the netcode-agnostic rules-layer card.</summary>
        public Card ToCard() =>
            new Card(new CardId(cardId), DisplayName, BuildRequirement(), points, power.Build(), family, tier);

        private ICardRequirement BuildRequirement()
        {
            if (requirements == null || requirements.Count == 0)
                return new SumRequirement(0, ComparisonOp.AtLeast); // trivially payable placeholder

            if (requirements.Count == 1)
                return requirements[0].Build();

            var built = new ICardRequirement[requirements.Count];
            for (int i = 0; i < requirements.Count; i++)
                built[i] = requirements[i].Build();
            return new CompositeRequirement(combineMode, built);
        }
    }
}
