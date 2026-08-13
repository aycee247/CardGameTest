using System;

namespace Game.Core
{
    /// <summary>Stable identifier for a player within a match. Pure value type, no engine deps.</summary>
    [Serializable]
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public readonly int Value;
        public PlayerId(int value) { Value = value; }

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"P{Value}";

        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }

    /// <summary>Stable identifier for a card definition. Backed by a content-defined int id.</summary>
    [Serializable]
    public readonly struct CardId : IEquatable<CardId>
    {
        public readonly int Value;
        public CardId(int value) { Value = value; }

        public bool Equals(CardId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CardId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Card#{Value}";

        public static bool operator ==(CardId a, CardId b) => a.Value == b.Value;
        public static bool operator !=(CardId a, CardId b) => a.Value != b.Value;
    }
}
