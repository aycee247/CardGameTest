namespace Game.Core
{
    /// <summary>
    /// Rules-layer view of a card: what it costs in dice, what it permanently does for its owner,
    /// and what it is worth. The Data layer (ScriptableObjects) authors these and hands them to the
    /// rules engine; the rules engine never touches Unity assets.
    ///
    /// The cost reuses <see cref="ICardRequirement"/> — the same matchers the turn-based prototype
    /// used to gate claims now describe what a card is bought with.
    /// </summary>
    public sealed class Card
    {
        public CardId Id { get; }
        public string DisplayName { get; }

        /// <summary>1..3. Determines deck ordering, so the market escalates over the match (MKT-1).</summary>
        public int Tier { get; }

        /// <summary>The dice pattern that must be spent to claim this card.</summary>
        public ICardRequirement Cost { get; }

        /// <summary>Persistent effect granted to the owner for the rest of the match.</summary>
        public CardPower Power { get; }

        /// <summary>Category, used by <see cref="PowerKind.ScorePerFamily"/> scoring and UI grouping.</summary>
        public PowerFamily Family { get; }

        /// <summary>Victory points scored just for owning it.</summary>
        public int Points { get; }

        public Card(
            CardId id,
            string displayName,
            ICardRequirement cost,
            int points,
            CardPower power = default,
            PowerFamily family = PowerFamily.Capacity,
            int tier = 1)
        {
            Id = id;
            DisplayName = displayName ?? id.ToString();
            Cost = cost;
            Points = points;
            Power = power;
            Family = family;
            Tier = tier;
        }

        public string DescribeCost() => Cost?.Describe() ?? "free";

        public override string ToString() => $"{DisplayName} (T{Tier}, {DescribeCost()}, {Points}vp)";
    }
}
