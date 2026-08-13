using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the Foundry starter deck as <see cref="CardDefinition"/> assets plus a
    /// <see cref="CardDatabase"/>, so the game has real content to play against.
    /// Menu: <b>Foundry ▸ Generate Starter Deck</b>.
    ///
    /// This is a playtest deck, not the shipping one: 36 cards over three tiers, enough to keep a
    /// six-player market stocked for ten rounds and to exercise all five power families. Balancing
    /// it and growing it to the specced 48 is M5.
    /// </summary>
    public static class SampleContentGenerator
    {
        private const string Folder = "Assets/_Project/ScriptableObjects";

        /// <summary>Fluent card description. Read as: identity, then cost, then power.</summary>
        private sealed class Spec
        {
            public int Id;
            public string Name;
            public int Tier;
            public int Points;
            public PowerFamily Family;

            public CardRequirementSpec.Kind CostKind;
            public int N, Face, RunLength, SumTarget;
            public ComparisonOp Op;

            public PowerKind Power;
            public int Magnitude, WildFace;
            public PowerFamily Counts;

            // --- cost ---
            public Spec OfAKind(int n, int face = 0)
            {
                CostKind = CardRequirementSpec.Kind.NOfAKind; N = n; Face = face; return this;
            }

            public Spec Run(int length)
            {
                CostKind = CardRequirementSpec.Kind.Run; RunLength = length; return this;
            }

            public Spec Sum(int target, ComparisonOp op = ComparisonOp.AtLeast)
            {
                CostKind = CardRequirementSpec.Kind.Sum; SumTarget = target; Op = op; return this;
            }

            // --- power ---
            public Spec Dice(int n) { Power = PowerKind.ExtraDie; Magnitude = n; return this; }
            public Spec Reroll(int n) { Power = PowerKind.FreeReroll; Magnitude = n; return this; }
            public Spec Nudge(int n) { Power = PowerKind.FreeNudge; Magnitude = n; return this; }
            public Spec SetDie(int n) { Power = PowerKind.FreeSet; Magnitude = n; return this; }
            public Spec Wild(int face) { Power = PowerKind.WildFace; WildFace = face; return this; }
            public Spec WildDice(int n) { Power = PowerKind.WildDie; Magnitude = n; return this; }
            public Spec Income(int n) { Power = PowerKind.SparkIncome; Magnitude = n; return this; }
            public Spec Flat(int vp) { Power = PowerKind.FlatScore; Magnitude = vp; return this; }
            public Spec PerFamily(int vp, PowerFamily f)
            {
                Power = PowerKind.ScorePerFamily; Magnitude = vp; Counts = f; return this;
            }
        }

        private static Spec Def(int id, string name, int tier, int points, PowerFamily family) =>
            new Spec { Id = id, Name = name, Tier = tier, Points = points, Family = family };

        private static List<Spec> BuildSpecs() => new List<Spec>
        {
            // ---------------- Tier 1 — cheap, small, gets the engine turning ----------------
            Def( 1, "Second Cast",     1, 1, PowerFamily.Capacity).OfAKind(2).Dice(1),
            Def( 2, "Sandbag",         1, 1, PowerFamily.Capacity).OfAKind(2, 1).Dice(1),
            Def( 3, "Small Anvil",     1, 2, PowerFamily.Capacity).Sum(14).Dice(1),
            Def( 4, "Sorting Rack",    1, 2, PowerFamily.Capacity).Run(3).Dice(1),

            Def( 5, "Whetstone",       1, 2, PowerFamily.Manipulation).Sum(12).Nudge(1),
            Def( 6, "Hand Crank",      1, 1, PowerFamily.Manipulation).Sum(10).Reroll(1),
            Def( 7, "Chalk Line",      1, 2, PowerFamily.Manipulation).Run(3).Nudge(1),
            Def( 8, "Rough Cast",      1, 1, PowerFamily.Manipulation).OfAKind(2).Reroll(1),

            Def( 9, "Tally Board",     1, 1, PowerFamily.Economy).Run(3).Income(1),
            Def(10, "Coin Tray",       1, 2, PowerFamily.Economy).OfAKind(2, 5).Income(1),
            Def(11, "Tallow Lamp",     1, 1, PowerFamily.Economy).OfAKind(2, 3).Income(1),
            Def(12, "Tin Whistle",     1, 1, PowerFamily.Economy).Sum(10, ComparisonOp.AtMost).Income(1),

            // ---------------- Tier 2 — the engine starts compounding ----------------
            Def(13, "Twin Forge",      2, 3, PowerFamily.Capacity).OfAKind(4).Dice(1),
            Def(14, "Great Bellows",   2, 3, PowerFamily.Capacity).Sum(20).Dice(1),
            Def(15, "Drop Hammer",     2, 2, PowerFamily.Capacity).Run(4).Dice(1),

            Def(16, "Recaster",        2, 3, PowerFamily.Manipulation).Sum(20).Reroll(2),
            Def(17, "Draw Plate",      2, 3, PowerFamily.Manipulation).Run(4).SetDie(1),
            Def(18, "Jewellers Vice",  2, 3, PowerFamily.Manipulation).OfAKind(3).Nudge(2),
            Def(19, "Swage Block",     2, 2, PowerFamily.Manipulation).OfAKind(3, 4).Reroll(2),

            Def(20, "Loaded Die",      2, 3, PowerFamily.Wild).OfAKind(3).WildDice(1),
            Def(21, "Weighted Pip",    2, 4, PowerFamily.Wild).Run(4).Wild(1),
            Def(22, "Mercury Core",    2, 4, PowerFamily.Wild).OfAKind(3, 6).Wild(2),

            Def(23, "Strong Box",      2, 3, PowerFamily.Economy).OfAKind(3).Income(2),
            Def(24, "Toll Gate",       2, 2, PowerFamily.Economy).Sum(18).Income(2),

            // ---------------- Tier 3 — expensive payoffs and end-game scoring ----------------
            Def(25, "Sixes Wild",      3, 5, PowerFamily.Wild).OfAKind(4).Wild(6),
            Def(26, "Quicksilver",     3, 5, PowerFamily.Wild).Sum(26).WildDice(2),

            Def(27, "The Overwrite",   3, 6, PowerFamily.Manipulation).OfAKind(5).SetDie(1),
            Def(28, "Master Gauge",    3, 5, PowerFamily.Manipulation).Run(5).SetDie(1),
            Def(29, "Trip Hammer",     3, 4, PowerFamily.Manipulation).OfAKind(4, 6).Reroll(3),

            Def(30, "Eighth Spindle",  3, 4, PowerFamily.Capacity).Run(5).Dice(1),
            Def(31, "Blast Furnace",   3, 5, PowerFamily.Capacity).Sum(28).Dice(2),

            Def(32, "Grand Array",     3, 4, PowerFamily.Scoring).Run(5).PerFamily(2, PowerFamily.Manipulation),
            Def(33, "Foundry Mark",    3, 4, PowerFamily.Scoring).OfAKind(4).PerFamily(2, PowerFamily.Capacity),
            Def(34, "Assay Office",    3, 4, PowerFamily.Scoring).Sum(24).PerFamily(2, PowerFamily.Economy),
            Def(35, "Crown Seal",      3, 6, PowerFamily.Scoring).OfAKind(5).Flat(4),
            Def(36, "Guild Charter",   3, 5, PowerFamily.Scoring).Run(6).PerFamily(3, PowerFamily.Wild),
        };

        [MenuItem("Foundry/Generate Starter Deck")]
        public static void Generate()
        {
            EnsureFolder();

            var specs = BuildSpecs();
            var database = ScriptableObject.CreateInstance<CardDatabase>();
            var created = new List<CardDefinition>(specs.Count);

            foreach (var spec in specs)
            {
                var card = ScriptableObject.CreateInstance<CardDefinition>();
                ApplySpec(card, spec);
                AssetDatabase.CreateAsset(card, $"{Folder}/Card_{spec.Id:00}_{Sanitize(spec.Name)}.asset");
                created.Add(card);
            }

            var dbSo = new SerializedObject(database);
            var list = dbSo.FindProperty("cards");
            list.arraySize = created.Count;
            for (int i = 0; i < created.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = created[i];
            dbSo.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(database, $"{Folder}/CardDatabase.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Validate(database);

            Debug.Log($"[Foundry] Generated {created.Count} cards + CardDatabase in {Folder}.");
            Selection.activeObject = database;
        }

        /// <summary>
        /// Warns about content that cannot work in play: a cost no legal dice pool can pay, a card
        /// with no power, or a duplicate id (ids are the network and save key).
        /// </summary>
        private static void Validate(CardDatabase database)
        {
            var config = MatchConfig.Default;
            var seenIds = new HashSet<int>();

            foreach (var def in database.Cards)
            {
                if (def == null) continue;
                var card = def.ToCard();

                if (!seenIds.Add(card.Id.Value))
                    Debug.LogError($"[Foundry] Duplicate card id {card.Id.Value} on '{card.DisplayName}'.");

                if (!CostChecker.IsSatisfiableWith(card.Cost, config.MaxDice))
                    Debug.LogError(
                        $"[Foundry] '{card.DisplayName}' costs {card.DescribeCost()}, which no pool of " +
                        $"{config.MaxDice} dice can ever pay. It would sit dead in the market.");
                else if (!CostChecker.IsSatisfiableWith(card.Cost, config.StartingDice) && card.Tier == 1)
                    Debug.LogWarning(
                        $"[Foundry] Tier 1 card '{card.DisplayName}' costs {card.DescribeCost()}, " +
                        $"unpayable with the {config.StartingDice} dice players start on.");

                if (card.Power.Kind == PowerKind.None)
                    Debug.LogWarning($"[Foundry] '{card.DisplayName}' has no power — it only scores.");
            }
        }

        private static void ApplySpec(CardDefinition card, Spec spec)
        {
            var so = new SerializedObject(card);
            so.FindProperty("cardId").intValue = spec.Id;
            so.FindProperty("displayName").stringValue = spec.Name;
            so.FindProperty("tier").intValue = spec.Tier;
            so.FindProperty("points").intValue = spec.Points;
            so.FindProperty("family").enumValueIndex = (int)spec.Family;

            var power = so.FindProperty("power");
            power.FindPropertyRelative("kind").enumValueIndex = (int)spec.Power;
            power.FindPropertyRelative("magnitude").intValue = spec.Magnitude;
            power.FindPropertyRelative("face").intValue = spec.WildFace;
            power.FindPropertyRelative("countsFamily").enumValueIndex = (int)spec.Counts;

            var reqs = so.FindProperty("requirements");
            reqs.arraySize = 1;
            var r0 = reqs.GetArrayElementAtIndex(0);
            r0.FindPropertyRelative("kind").enumValueIndex = (int)spec.CostKind;
            r0.FindPropertyRelative("n").intValue = spec.N;
            r0.FindPropertyRelative("face").intValue = spec.Face;
            r0.FindPropertyRelative("runLength").intValue = spec.RunLength;
            r0.FindPropertyRelative("sumTarget").intValue = spec.SumTarget;
            r0.FindPropertyRelative("sumComparison").enumValueIndex = (int)spec.Op;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
                AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
