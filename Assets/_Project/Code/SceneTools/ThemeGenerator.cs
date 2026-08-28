using Game.Data;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.SceneTools
{
    /// <summary>
    /// Writes the game's themes — the Industry design system's token sheets — into
    /// <see cref="ThemeAsset"/>s. Values live here in code, matching the project's
    /// assets-are-generated philosophy, so a hand-edited asset never silently drifts.
    ///
    /// Two complete themes ship (STORY-5.1 AC4): <b>Blueprint Light</b>, the drafting-table
    /// default from the mobile handoff, and <b>Blueprint Dark</b>, the night shift — deep
    /// blue-black ground, the same accent ramp reversed so low steps read as surfaces and high
    /// steps as emphasis, exactly the contract the views already rely on.
    /// </summary>
    public static class ThemeGenerator
    {
        public const string ThemePath = "Assets/_Project/ScriptableObjects/Theme_BlueprintLight.asset";
        public const string DarkThemePath = "Assets/_Project/ScriptableObjects/Theme_BlueprintDark.asset";
        private const string FontDir = "Assets/_Project/Art/Fonts/Generated";

        [MenuItem("Foundry/Generate Theme")]
        public static void Generate()
        {
            WriteTheme(ThemePath, dark: false);
            WriteTheme(DarkThemePath, dark: true);
            AssetDatabase.SaveAssets();
        }

        private static void WriteTheme(string path, bool dark)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(path);
            bool fresh = theme == null;
            if (fresh) theme = ScriptableObject.CreateInstance<ThemeAsset>();

            if (dark) FillDark(theme); else FillLight(theme);

            theme.bodyRegular = LoadFont("Barlow-Regular SDF");
            theme.bodyMedium = LoadFont("Barlow-Medium SDF");
            theme.bodySemibold = LoadFont("Barlow-SemiBold SDF");
            theme.headingSemibold = LoadFont("BarlowCondensed-SemiBold SDF");
            theme.headingBold = LoadFont("BarlowCondensed-Bold SDF");

            if (fresh) AssetDatabase.CreateAsset(theme, path);
            else EditorUtility.SetDirty(theme);

            var issues = ThemeValidator.Collect(theme);
            if (issues.Count > 0)
                Debug.LogError($"[Foundry] Theme at {path} has {issues.Count} unassigned token(s):\n  " +
                               string.Join("\n  ", issues));
            else
                Debug.Log($"[Foundry] Theme written to {path}; every token assigned.");
        }

        private static void FillLight(ThemeAsset theme)
        {
            theme.surfaceBase = Hex("#f2f2f3");
            theme.surfaceRaised = Hex("#e9e9ea");
            // The one token the handoff table leaves unassigned; the reveal spotlight's full-bleed
            // accent-900 field is its only full-surface use, so that is the value.
            theme.surfaceOverlay = Hex("#1d2d3d");

            theme.textPrimary = Hex("#1d1f20");
            theme.textMuted = Hex("#8d8e8f");
            theme.textInverse = Hex("#f2f2f3");

            theme.stateAffordable = Hex("#94bce3");            // accent-400 border on payable cards
            theme.stateUnaffordable = Fade(Hex("#1d1f20"));    // neutral hairline when dice can't pay
            theme.stateSpent = Hex("#b7b7ba");                 // neutral-400, dashed-border dice
            theme.stateReady = Hex("#416180");                 // accent-700 "✓ READY"
            theme.stateThinking = Hex("#8d8e8f");              // text.muted "○ THINKING"
            theme.stateTrouble = Hex("#e58c59");               // carried from the old PlayerRowView

            theme.accentPriority = Hex("#5980a6");
            theme.accentRamp = Ramp();
            theme.divider = Fade(Hex("#1d1f20"));
        }

        private static void FillDark(ThemeAsset theme)
        {
            theme.surfaceBase = Hex("#16222e");                // the night-shift floor
            theme.surfaceRaised = Hex("#1d2d3d");              // light theme's overlay, now a surface
            theme.surfaceOverlay = Hex("#0d151d");             // the spotlight goes deeper still

            theme.textPrimary = Hex("#eef6ff");
            theme.textMuted = Hex("#8fa1b3");
            theme.textInverse = Hex("#1d1f20");

            theme.stateAffordable = Hex("#94bce3");
            theme.stateUnaffordable = Fade(Hex("#eef6ff"));
            theme.stateSpent = Hex("#5a6b7c");
            theme.stateReady = Hex("#b5d9fd");                 // brighter reads as lit on dark
            theme.stateThinking = Hex("#8fa1b3");
            theme.stateTrouble = Hex("#e58c59");               // the ember carries both shifts

            theme.accentPriority = Hex("#749dc4");
            // The same ramp reversed: consumers use low steps as quiet surfaces and high steps
            // as emphasis, and reversal preserves that contract on a dark ground.
            var ramp = Ramp();
            System.Array.Reverse(ramp);
            theme.accentRamp = ramp;
            theme.divider = Fade(Hex("#eef6ff"));
        }

        private static Color[] Ramp() => new[]
        {
            Hex("#eef6ff"), Hex("#d6ebff"), Hex("#b5d9fd"),
            Hex("#94bce3"), Hex("#749dc4"), Hex("#597ea3"),
            Hex("#416180"), Hex("#2c455d"), Hex("#1d2d3d"),
        };

        /// <summary>Hairline borders: the ink at 16% (the prototype's color-mix divider).</summary>
        private static Color Fade(Color ink)
        {
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
