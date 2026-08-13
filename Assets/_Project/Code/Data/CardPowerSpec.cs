using System;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Inspector-authorable description of a card's persistent power (CARD-2). A designer picks a
    /// <see cref="PowerKind"/> and fills the field that kind uses; <see cref="Build"/> converts it
    /// into the pure <see cref="CardPower"/> the rules engine reads.
    ///
    /// Flat by design so it serializes cleanly, and mirrors <see cref="CardRequirementSpec"/>:
    /// one authoring struct per rules-layer value type, no nesting.
    /// </summary>
    [Serializable]
    public struct CardPowerSpec
    {
        [Tooltip("What the card does for its owner, for the rest of the match.")]
        public PowerKind kind;

        [Tooltip("How much. Dice added, free actions granted, Sparks per round, or VP — depends on kind.")]
        public int magnitude;

        [Tooltip("Which face is wild. Used by WildFace only.")]
        [Range(0, 6)]
        public int face;

        [Tooltip("Which family is counted. Used by ScorePerFamily only.")]
        public PowerFamily countsFamily;

        public CardPower Build() => new CardPower(kind, magnitude, face, countsFamily);

        public static CardPowerSpec None => new CardPowerSpec { kind = PowerKind.None };
    }
}
