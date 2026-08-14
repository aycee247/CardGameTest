using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Writes the deck defined by <see cref="StarterDeck"/> out as <see cref="CardDefinition"/>
    /// assets plus a <see cref="CardDatabase"/>.
    /// Menu: <b>Foundry ▸ Generate Starter Deck</b>.
    ///
    /// The card values live in Core, not here. That is deliberate: the balance harness plays
    /// matches against the same definitions, so a card cannot be tuned in one place and ship from
    /// another. This file only knows how to turn a blueprint into a serialized asset.
    /// </summary>
    public static class SampleContentGenerator
    {
        private const string Folder = "Assets/_Project/ScriptableObjects";

        [MenuItem("Foundry/Generate Starter Deck")]
        public static void Generate()
        {
            EnsureFolder();

            var blueprints = StarterDeck.Blueprints;
            var database = ScriptableObject.CreateInstance<CardDatabase>();
            var created = new List<CardDefinition>(blueprints.Count);

            foreach (var blueprint in blueprints)
            {
                var card = ScriptableObject.CreateInstance<CardDefinition>();
                Apply(card, blueprint);
                AssetDatabase.CreateAsset(card, $"{Folder}/Card_{blueprint.Id:00}_{Sanitize(blueprint.Name)}.asset");
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

        private static void Apply(CardDefinition card, CardBlueprint blueprint)
        {
            var so = new SerializedObject(card);
            so.FindProperty("cardId").intValue = blueprint.Id;
            so.FindProperty("displayName").stringValue = blueprint.Name;
            so.FindProperty("tier").intValue = blueprint.Tier;
            so.FindProperty("points").intValue = blueprint.Points;
            so.FindProperty("family").enumValueIndex = (int)blueprint.Family;

            var power = so.FindProperty("power");
            power.FindPropertyRelative("kind").enumValueIndex = (int)blueprint.PowerKind;
            power.FindPropertyRelative("magnitude").intValue = blueprint.Magnitude;
            power.FindPropertyRelative("face").intValue = blueprint.WildFace;
            power.FindPropertyRelative("countsFamily").enumValueIndex = (int)blueprint.CountsFamily;

            var reqs = so.FindProperty("requirements");
            reqs.arraySize = 1;
            var r0 = reqs.GetArrayElementAtIndex(0);
            r0.FindPropertyRelative("kind").enumValueIndex = (int)ToSpecKind(blueprint.CostKind);
            r0.FindPropertyRelative("n").intValue = blueprint.N;
            r0.FindPropertyRelative("face").intValue = blueprint.Face;
            r0.FindPropertyRelative("runLength").intValue = blueprint.RunLength;
            r0.FindPropertyRelative("sumTarget").intValue = blueprint.SumTarget;
            r0.FindPropertyRelative("sumComparison").enumValueIndex = (int)blueprint.Op;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CardRequirementSpec.Kind ToSpecKind(CostKind kind)
        {
            switch (kind)
            {
                case CostKind.Run: return CardRequirementSpec.Kind.Run;
                case CostKind.Sum: return CardRequirementSpec.Kind.Sum;
                default: return CardRequirementSpec.Kind.NOfAKind;
            }
        }

        /// <summary>
        /// Reads the written assets back and checks them, so a mistake in the serialization above
        /// is caught here rather than as a dead card in the market.
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

                if (card.Power.Kind == PowerKind.None)
                    Debug.LogWarning($"[Foundry] '{card.DisplayName}' has no power — it only scores.");
            }
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
            return s.Replace(' ', '_').Replace("'", string.Empty);
        }
    }
}
