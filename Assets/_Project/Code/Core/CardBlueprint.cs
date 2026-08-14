using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Which matcher a card's cost uses. Mirrors the authoring enum in the Data layer.</summary>
    public enum CostKind
    {
        NOfAKind,
        Run,
        Sum
    }

    /// <summary>
    /// A card as plain data: everything needed to build the rules-layer <see cref="Card"/> and to
    /// author the matching ScriptableObject.
    ///
    /// This exists so there is exactly one definition of the deck. The editor generator writes
    /// assets from these, and the balance harness builds matches from the same values, so tuning a
    /// card cannot silently disagree with what ships.
    /// </summary>
    public readonly struct CardBlueprint
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Tier;
        public readonly int Points;
        public readonly PowerFamily Family;

        public readonly CostKind CostKind;
        public readonly int N;
        public readonly int Face;
        public readonly int RunLength;
        public readonly int SumTarget;
        public readonly ComparisonOp Op;

        public readonly PowerKind PowerKind;
        public readonly int Magnitude;
        public readonly int WildFace;
        public readonly PowerFamily CountsFamily;

        public CardBlueprint(
            int id, string name, int tier, int points, PowerFamily family,
            CostKind costKind, int n, int face, int runLength, int sumTarget, ComparisonOp op,
            PowerKind powerKind, int magnitude, int wildFace, PowerFamily countsFamily)
        {
            Id = id; Name = name; Tier = tier; Points = points; Family = family;
            CostKind = costKind; N = n; Face = face; RunLength = runLength; SumTarget = sumTarget; Op = op;
            PowerKind = powerKind; Magnitude = magnitude; WildFace = wildFace; CountsFamily = countsFamily;
        }

        public ICardRequirement BuildCost()
        {
            switch (CostKind)
            {
                case CostKind.NOfAKind:
                    return new NOfAKindRequirement(Math.Max(1, N), Face >= 1 && Face <= 6 ? Face : (int?)null);
                case CostKind.Run:
                    return new RunRequirement(Math.Max(1, RunLength));
                case CostKind.Sum:
                    return new SumRequirement(SumTarget, Op);
                default:
                    return new SumRequirement(0, ComparisonOp.AtLeast);
            }
        }

        public CardPower BuildPower() => new CardPower(PowerKind, Magnitude, WildFace, CountsFamily);

        public Card ToCard() =>
            new Card(new CardId(Id), Name, BuildCost(), Points, BuildPower(), Family, Tier);

        public string DescribeCost() => BuildCost().Describe();
    }

    /// <summary>Fluent construction, so the deck below reads as identity, then cost, then power.</summary>
    public sealed class CardDraft
    {
        internal int Id, Tier, Points;
        internal string Name;
        internal PowerFamily Family;

        internal CostKind CostKind;
        internal int N, Face, RunLength, SumTarget;
        internal ComparisonOp Op = ComparisonOp.AtLeast;

        internal PowerKind PowerKind = PowerKind.None;
        internal int Magnitude, WildFace;
        internal PowerFamily CountsFamily;

        internal CardDraft(int id, string name, int tier, int points, PowerFamily family)
        {
            Id = id; Name = name; Tier = tier; Points = points; Family = family;
        }

        public CardDraft OfAKind(int n, int face = 0) { CostKind = CostKind.NOfAKind; N = n; Face = face; return this; }
        public CardDraft Run(int length) { CostKind = CostKind.Run; RunLength = length; return this; }

        public CardDraft Sum(int target, ComparisonOp op = ComparisonOp.AtLeast)
        {
            CostKind = CostKind.Sum; SumTarget = target; Op = op; return this;
        }

        public CardDraft Dice(int n) { PowerKind = PowerKind.ExtraDie; Magnitude = n; return this; }
        public CardDraft Reroll(int n) { PowerKind = PowerKind.FreeReroll; Magnitude = n; return this; }
        public CardDraft Nudge(int n) { PowerKind = PowerKind.FreeNudge; Magnitude = n; return this; }
        public CardDraft SetDie(int n) { PowerKind = PowerKind.FreeSet; Magnitude = n; return this; }
        public CardDraft Wild(int face) { PowerKind = PowerKind.WildFace; WildFace = face; return this; }
        public CardDraft WildDice(int n) { PowerKind = PowerKind.WildDie; Magnitude = n; return this; }
        public CardDraft Income(int n) { PowerKind = PowerKind.SparkIncome; Magnitude = n; return this; }
        public CardDraft Flat(int vp) { PowerKind = PowerKind.FlatScore; Magnitude = vp; return this; }

        public CardDraft PerFamily(int vp, PowerFamily family)
        {
            PowerKind = PowerKind.ScorePerFamily; Magnitude = vp; CountsFamily = family; return this;
        }

        public CardBlueprint Build() => new CardBlueprint(
            Id, Name, Tier, Points, Family,
            CostKind, N, Face, RunLength, SumTarget, Op,
            PowerKind, Magnitude, WildFace, CountsFamily);

        public static implicit operator CardBlueprint(CardDraft draft) => draft.Build();
    }

    /// <summary>
    /// The shipping deck: 48 cards over three tiers (CARD-1).
    ///
    /// Tier is deck order, not just flavour — the deck is dealt Tier 1 first, so the market
    /// escalates over the match (MKT-1). Costs rise with tier because dice pools grow: a five of a
    /// kind is unreachable on the four dice players start with and routine on eight.
    /// </summary>
    public static class StarterDeck
    {
        private static CardDraft Def(int id, string name, int tier, int points, PowerFamily family) =>
            new CardDraft(id, name, tier, points, family);

        public static IReadOnlyList<CardBlueprint> Blueprints => Cards;

        public static List<Card> Build()
        {
            var deck = new List<Card>(Cards.Count);
            foreach (var blueprint in Cards) deck.Add(blueprint.ToCard());
            return deck;
        }

        private static readonly List<CardBlueprint> Cards = new List<CardBlueprint>
        {
            // ================= Tier 1 — no Capacity, deliberately =================
            // Dice pay every cost, so a bigger pool helps with everything. When Tier 1 offered a
            // +1 die for any pair, the engine could be rushed in the opening rounds while it was
            // cheap and nobody contested it, and stacking capacity beat committing to anything
            // else. Capacity now starts at Tier 2, so the first rounds must be spent on a real
            // choice and the compounding window is shorter.
            Def( 1, "Whetstone",       1, 2, PowerFamily.Manipulation).Sum(12).Nudge(1),
            Def( 2, "Hand Crank",      1, 1, PowerFamily.Manipulation).Sum(10).Reroll(1),
            Def( 3, "Chalk Line",      1, 2, PowerFamily.Manipulation).Run(3).Nudge(1),
            Def( 4, "Rough Cast",      1, 1, PowerFamily.Manipulation).OfAKind(2).Reroll(1),
            Def( 5, "Sandbag",         1, 2, PowerFamily.Manipulation).OfAKind(2, 1).Nudge(2),

            Def( 6, "Tally Board",     1, 2, PowerFamily.Economy).Run(3).Income(1),
            Def( 7, "Coin Tray",       1, 2, PowerFamily.Economy).OfAKind(2, 5).Income(2),
            Def( 8, "Tallow Lamp",     1, 2, PowerFamily.Economy).OfAKind(2, 3).Income(1),
            Def( 9, "Clay Mould",      1, 2, PowerFamily.Economy).OfAKind(2, 4).Income(2),

            Def(10, "Chipped Die",     1, 3, PowerFamily.Wild).Sum(16).Wild(1),
            Def(11, "Pin Vice",        1, 3, PowerFamily.Wild).OfAKind(2, 6).WildDice(1),
            Def(12, "Glass Bead",      1, 2, PowerFamily.Wild).OfAKind(2).WildDice(1),

            Def(13, "Maker's Mark",    1, 2, PowerFamily.Scoring).OfAKind(3).Flat(2),
            Def(14, "Tin Whistle",     1, 2, PowerFamily.Scoring).Sum(10, ComparisonOp.AtMost).Flat(2),
            Def(15, "Ledger Stone",    1, 2, PowerFamily.Scoring).Run(3).Flat(2),
            Def(16, "Trade Token",     1, 1, PowerFamily.Scoring).OfAKind(2, 2).Flat(1),

            // ================= Tier 2 — the engine starts compounding =================
            Def(17, "Second Cast",     2, 1, PowerFamily.Capacity).OfAKind(3).Dice(1),
            Def(18, "Small Anvil",     2, 1, PowerFamily.Capacity).Sum(18).Dice(1),
            Def(19, "Sorting Rack",    2, 1, PowerFamily.Capacity).Run(4).Dice(1),

            Def(20, "Recaster",        2, 3, PowerFamily.Manipulation).Sum(20).Reroll(2),
            Def(21, "Draw Plate",      2, 3, PowerFamily.Manipulation).Run(4).SetDie(1),
            Def(22, "Jewellers Vice",  2, 3, PowerFamily.Manipulation).OfAKind(3).Nudge(2),
            Def(23, "Swage Block",     2, 2, PowerFamily.Manipulation).OfAKind(3, 4).Reroll(2),

            Def(24, "Loaded Die",      2, 3, PowerFamily.Wild).OfAKind(3).WildDice(1),
            Def(25, "Weighted Pip",    2, 4, PowerFamily.Wild).Run(4).Wild(1),
            Def(26, "Mercury Core",    2, 4, PowerFamily.Wild).OfAKind(3, 6).Wild(2),
            Def(27, "Crucible",        2, 3, PowerFamily.Wild).OfAKind(3, 5).WildDice(1),

            Def(28, "Strong Box",      2, 4, PowerFamily.Economy).OfAKind(3).Income(2),
            Def(29, "Toll Gate",       2, 3, PowerFamily.Economy).Sum(18).Income(2),
            Def(30, "Counting House",  2, 3, PowerFamily.Economy).Run(4).Income(3),

            Def(31, "Journeyman Seal", 2, 3, PowerFamily.Scoring).OfAKind(4).PerFamily(2, PowerFamily.Capacity),
            Def(32, "Set Square",      2, 3, PowerFamily.Scoring).Sum(22).Flat(3),

            // ================= Tier 3 — expensive payoffs and end-game scoring =================
            Def(33, "Twin Forge",      3, 2, PowerFamily.Capacity).OfAKind(4).Dice(1),
            Def(34, "Eighth Spindle",  3, 3, PowerFamily.Capacity).Run(5).Dice(1),
            Def(35, "Blast Furnace",   3, 4, PowerFamily.Capacity).Sum(28).Dice(2),

            Def(36, "The Overwrite",   3, 6, PowerFamily.Manipulation).OfAKind(5).SetDie(1),
            Def(37, "Master Gauge",    3, 5, PowerFamily.Manipulation).Run(5).SetDie(1),
            Def(38, "Trip Hammer",     3, 4, PowerFamily.Manipulation).OfAKind(4, 6).Reroll(3),

            Def(39, "Sixes Wild",      3, 5, PowerFamily.Wild).OfAKind(4).Wild(6),
            Def(40, "Quicksilver",     3, 5, PowerFamily.Wild).Sum(26).WildDice(2),
            Def(41, "Fool's Gold",     3, 4, PowerFamily.Wild).Run(5).Wild(2),

            Def(42, "Royal Mint",      3, 4, PowerFamily.Economy).OfAKind(4, 5).Income(3),
            Def(43, "Bonded Vault",    3, 4, PowerFamily.Economy).Sum(30).Income(3),

            Def(44, "Grand Array",     3, 4, PowerFamily.Scoring).Run(5).PerFamily(3, PowerFamily.Manipulation),
            Def(45, "Foundry Mark",    3, 4, PowerFamily.Scoring).OfAKind(4).PerFamily(2, PowerFamily.Capacity),
            Def(46, "Assay Office",    3, 4, PowerFamily.Scoring).Sum(24).PerFamily(3, PowerFamily.Economy),
            Def(47, "Crown Seal",      3, 6, PowerFamily.Scoring).OfAKind(5).Flat(5),
            Def(48, "Masterpiece",     3, 7, PowerFamily.Scoring).OfAKind(5, 6).Flat(6),
        };
    }
}
