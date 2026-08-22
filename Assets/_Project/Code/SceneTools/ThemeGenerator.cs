using Game.Data;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.SceneTools
{
    /// <summary>
    /// Writes the Blueprint Light theme — the Industry design system's token sheet from
    /// design_handoff_foundry_mobile_ui/README.md — into a <see cref="ThemeAsset"/>. Values live
    /// here in code, matching the project's assets-are-generated philosophy, so a hand-edited
    /// asset never silently drifts from the handoff.
    /// </summary>
    public static class ThemeGenerator
    {
        public const string ThemePath = "Assets/_Project/ScriptableObjects/Theme_BlueprintLight.asset";
        private const string FontDir = "Assets/_Project/Art/Fonts/Generated";

        [MenuItem("Foundry/Generate Theme")]
        public static void Generate()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemePath);
            bool fresh = theme == null;
            if (fresh) theme = ScriptableObject.CreateInstance<ThemeAsset>();

            theme.surfaceBase = Hex("#f2f2f3");
            theme.surfaceRaised = Hex("#e9e9ea");
            // The one token the handoff table leaves unassigned; the reveal spotlight's full-bleed
            // accent-900 field is its only full-surface use, so that is the value.
            theme.surfaceOverlay = Hex("#1d2d3d");

            theme.textPrimary = Hex("#1d1f20");
            theme.textMuted = Hex("#8d8e8f");
            theme.textInverse = Hex("#f2f2f3");

            theme.stateAffordable = Hex("#94bce3");            // accent-400 border on payable cards
            theme.stateUnaffordable = Divider();               // neutral hairline when dice can't pay
            theme.stateSpent = Hex("#b7b7ba");                 // neutral-400, dashed-border dice
            theme.stateReady = Hex("#416180");                 // accent-700 "✓ READY"
            theme.stateThinking = Hex("#8d8e8f");              // text.muted "○ THINKING"
            theme.stateTrouble = Hex("#e58c59");               // carried from the old PlayerRowView

            theme.accentPriority = Hex("#5980a6");
            theme.accentRamp = new[]
            {
                Hex("#eef6ff"), Hex("#d6ebff"), Hex("#b5d9fd"),
                Hex("#94bce3"), Hex("#749dc4"), Hex("#597ea3"),
                Hex("#416180"), Hex("#2c455d"), Hex("#1d2d3d"),
            };
            theme.divider = Divider();

            theme.bodyRegular = LoadFont("Barlow-Regular SDF");
            theme.bodyMedium = LoadFont("Barlow-Medium SDF");
            theme.bodySemibold = LoadFont("Barlow-SemiBold SDF");
            theme.headingSemibold = LoadFont("BarlowCondensed-SemiBold SDF");
            theme.headingBold = LoadFont("BarlowCondensed-Bold SDF");

            if (fresh) AssetDatabase.CreateAsset(theme, ThemePath);
            else EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            var issues = ThemeValidator.Collect(theme);
            if (issues.Count > 0)
                Debug.LogError($"[Foundry] Theme generated with {issues.Count} unassigned token(s):\n  " +
                               string.Join("\n  ", issues));
            else
                Debug.Log($"[Foundry] Theme written to {ThemePath}; every token assigned.");
        }

        /// <summary>Hairline borders: the ink at 16% (the prototype's color-mix divider).</summary>
        private static Color Divider()
        {
            var ink = Hex("#1d1f20");
            ink.a = 0.16f;
            return ink;
        }

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;

        private static TMP_FontAsset LoadFont(string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{name}.asset");
            if (font == null)
                Debug.LogError($"[Foundry] Font asset '{name}' not found — run Foundry ▸ Generate Font Assets first.");
            return font;
        }
    }
}
