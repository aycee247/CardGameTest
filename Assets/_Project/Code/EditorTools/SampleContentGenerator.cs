using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor menu that generates a starter set of <see cref="CardDefinition"/> assets and a
    /// <see cref="CardDatabase"/> so the game has content to run against on day one. Uses
    /// SerializedObject so it stays in sync with the assets' private serialized fields.
    /// Menu: <b>DiceCards ▸ Generate Sample Content</b>.
    /// </summary>
    public static class SampleContentGenerator
    {
        private const string Folder = "Assets/_Project/ScriptableObjects";

        private struct Spec
        {
            public int id;
            public string name;
            public int points;
            public CardRequirementSpec.Kind kind;
            public int n, face, runLength, sumTarget;
            public ComparisonOp op;
        }

        [MenuItem("DiceCards/Generate Sample Content")]
        public static void Generate()
        {
            EnsureFolder();

            var specs = new List<Spec>
            {
                new Spec { id = 1, name = "Twin Sixes",   points = 2, kind = CardRequirementSpec.Kind.NOfAKind, n = 2, face = 6 },
                new Spec { id = 2, name = "Three Aces",    points = 3, kind = CardRequirementSpec.Kind.NOfAKind, n = 3, face = 1 },
                new Spec { id = 3, name = "Small Straight",points = 3, kind = CardRequirementSpec.Kind.Run,      runLength = 4 },
                new Spec { id = 4, name = "Grand Straight",points = 6, kind = CardRequirementSpec.Kind.Run,      runLength = 6 },
                new Spec { id = 5, name = "High Roller",   points = 4, kind = CardRequirementSpec.Kind.Sum,      sumTarget = 24, op = ComparisonOp.AtLeast },
                new Spec { id = 6, name = "Minimalist",    points = 4, kind = CardRequirementSpec.Kind.Sum,      sumTarget = 10, op = ComparisonOp.AtMost },
                new Spec { id = 7, name = "Four of a Kind",points = 5, kind = CardRequirementSpec.Kind.NOfAKind, n = 4 },
                new Spec { id = 8, name = "Five of a Kind",points = 8, kind = CardRequirementSpec.Kind.NOfAKind, n = 5 },
            };

            var database = ScriptableObject.CreateInstance<CardDatabase>();
            var dbCards = new List<CardDefinition>();

            foreach (var spec in specs)
            {
                var card = ScriptableObject.CreateInstance<CardDefinition>();
                ApplySpec(card, spec);
                var path = $"{Folder}/Card_{spec.id:00}_{Sanitize(spec.name)}.asset";
                AssetDatabase.CreateAsset(card, path);
                dbCards.Add(card);
            }

            // Fill the database's private card list.
            var dbSo = new SerializedObject(database);
            var list = dbSo.FindProperty("cards");
            list.arraySize = dbCards.Count;
            for (int i = 0; i < dbCards.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = dbCards[i];
            dbSo.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(database, $"{Folder}/CardDatabase.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaffold] Generated {dbCards.Count} cards + CardDatabase in {Folder}.");
            Selection.activeObject = database;
        }

        private static void ApplySpec(CardDefinition card, Spec spec)
        {
            var so = new SerializedObject(card);
            so.FindProperty("cardId").intValue = spec.id;
            so.FindProperty("displayName").stringValue = spec.name;
            so.FindProperty("points").intValue = spec.points;

            var reqs = so.FindProperty("requirements");
            reqs.arraySize = 1;
            var r0 = reqs.GetArrayElementAtIndex(0);
            r0.FindPropertyRelative("kind").enumValueIndex = (int)spec.kind;
            r0.FindPropertyRelative("n").intValue = spec.n;
            r0.FindPropertyRelative("face").intValue = spec.face;
            r0.FindPropertyRelative("runLength").intValue = spec.runLength;
            r0.FindPropertyRelative("sumTarget").intValue = spec.sumTarget;
            r0.FindPropertyRelative("sumComparison").enumValueIndex = (int)spec.op;

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
