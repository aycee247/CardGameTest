using System;

namespace Game.Core
{
    /// <summary>
    /// Broad category a card belongs to. Drives <see cref="PowerKind.ScorePerFamily"/> counting
    /// and lets the UI group a player's engine without inspecting individual powers.
    /// </summary>
    public enum PowerFamily
    {
        Capacity,
        Manipulation,
        Wild,
        Economy,
        Scoring
    }

    /// <summary>
    /// What a card's persistent power actually does. Cards are data, not code (CARD-2): a designer
    /// picks a kind and a magnitude, and the rules engine interprets it.
    /// </summary>
    public enum PowerKind
    {
        None = 0,

        /// <summary>Adds <see cref="CardPower.Magnitude"/> dice to the pool, up to MaxDice.</summary>
        ExtraDie,

        /// <summary>Grants that many free re-rolls each Shape phase.</summary>
        FreeReroll,

        /// <summary>Grants that many free ±1 nudges each Shape phase.</summary>
        FreeNudge,

        /// <summary>Grants that many free set-to-any-face actions each Shape phase.</summary>
        FreeSet,

        /// <summary>Dice showing <see cref="CardPower.Face"/> may count as any face when paying a cost.</summary>
        WildFace,

        /// <summary>That many dice of the player's choosing may count as any face when paying a cost.</summary>
        WildDie,

        /// <summary>Adds that many Sparks every Upkeep.</summary>
        SparkIncome,

        /// <summary>At match end, scores Magnitude VP per owned card in <see cref="CardPower.CountsFamily"/>.</summary>
        ScorePerFamily,

        /// <summary>At match end, scores a flat Magnitude VP.</summary>
        FlatScore
    }

    /// <summary>
    /// A card's persistent effect. Immutable and engine-agnostic; owning the card is what applies it,
    /// so powers are always derived from <see cref="PlayerState.Owned"/> rather than cached as mutable
    /// counters that could drift out of sync with the card list.
    /// </summary>
    [Serializable]
    public readonly struct CardPower : IEquatable<CardPower>
    {
        public readonly PowerKind Kind;
        public readonly int Magnitude;

        /// <summary>Which face is wild. Meaningful only for <see cref="PowerKind.WildFace"/>.</summary>
        public readonly int Face;

        /// <summary>Which family is counted. Meaningful only for <see cref="PowerKind.ScorePerFamily"/>.</summary>
        public readonly PowerFamily CountsFamily;

        public CardPower(PowerKind kind, int magnitude = 0, int face = 0, PowerFamily countsFamily = PowerFamily.Capacity)
        {
            Kind = kind;
            Magnitude = magnitude;
            Face = face;
            CountsFamily = countsFamily;
        }

        public static CardPower None => new CardPower(PowerKind.None);

        public static CardPower ExtraDie(int count = 1) => new CardPower(PowerKind.ExtraDie, count);
        public static CardPower FreeReroll(int count) => new CardPower(PowerKind.FreeReroll, count);
        public static CardPower FreeNudge(int count) => new CardPower(PowerKind.FreeNudge, count);
        public static CardPower FreeSet(int count) => new CardPower(PowerKind.FreeSet, count);
        public static CardPower WildFace(int face) => new CardPower(PowerKind.WildFace, 0, face);
        public static CardPower WildDie(int count) => new CardPower(PowerKind.WildDie, count);
        public static CardPower SparkIncome(int count) => new CardPower(PowerKind.SparkIncome, count);
        public static CardPower FlatScore(int vp) => new CardPower(PowerKind.FlatScore, vp);
        public static CardPower ScorePerFamily(int vp, PowerFamily family) =>
            new CardPower(PowerKind.ScorePerFamily, vp, 0, family);

        public string Describe()
        {
            switch (Kind)
            {
                case PowerKind.None: return "no power";
                case PowerKind.ExtraDie: return Magnitude == 1 ? "+1 die" : $"+{Magnitude} dice";
                case PowerKind.FreeReroll: return $"re-roll {Magnitude} {Dice(Magnitude)} free each round";
                case PowerKind.FreeNudge: return $"±1 to {Magnitude} {Dice(Magnitude)} each round";
                case PowerKind.FreeSet: return $"set {Magnitude} {Dice(Magnitude)} to any face each round";
                case PowerKind.WildFace: return $"{Face}s count as any face";
                case PowerKind.WildDie: return Magnitude == 1 ? "one die is wild" : $"{Magnitude} dice are wild";
                case PowerKind.SparkIncome: return $"+{Magnitude} {(Magnitude == 1 ? "Spark" : "Sparks")} each round";
                case PowerKind.ScorePerFamily: return $"+{Magnitude} VP per {CountsFamily} card";
                case PowerKind.FlatScore: return $"+{Magnitude} VP";
                default: return Kind.ToString();
            }
        }

        private static string Dice(int n) => n == 1 ? "die" : "dice";

        public bool Equals(CardPower other) =>
            Kind == other.Kind && Magnitude == other.Magnitude &&
            Face == other.Face && CountsFamily == other.CountsFamily;

        public override bool Equals(object obj) => obj is CardPower other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + Magnitude;
                hash = hash * 31 + Face;
                hash = hash * 31 + (int)CountsFamily;
                return hash;
            }
        }

        public override string ToString() => Describe();
    }
}
