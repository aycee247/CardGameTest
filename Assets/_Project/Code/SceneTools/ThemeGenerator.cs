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

        /// <summary>
        /// The chunky arcade direction: a warm ground, flat saturated colour, and a solid ink
        /// edge where the blueprint drew a hairline. Same token names throughout — the palette is
        /// a content change, which is the whole reason the theme exists.
        /// </summary>
        private static void FillLight(ThemeAsset theme)
        {
            theme.surfaceBase = Hex("#fdf3e3");        // warm ground, not cold grey
            theme.surfaceRaised = Hex("#ffffff");
            // The one token the handoff table leaves unassigned; the reveal spotlight's full-bleed
            // accent-900 field is its only full-surface use, so that is the value.
            theme.surfaceOverlay = Hex("#1d2d3d");

            theme.textPrimary = Hex("#16212e");
            theme.textMuted = Hex("#6f6a5e");           // warm grey, to sit on the cream
            theme.textInverse = Hex("#fdf3e3");

            theme.stateAffordable = Hex("#94bce3");            // accent-400 border on payable cards
            theme.stateUnaffordable = Fade(Hex("#1d1f20"));    // neutral hairline when dice can't pay
            theme.stateSpent = Hex("#b7b7ba");                 // neutral-400, dashed-border dice
            theme.stateReady = Hex("#34b27b");                 // green reads as done at a glance
            theme.stateThinking = Hex("#6f6a5e");
            theme.stateTrouble = Hex("#f0803c");               // the old ember, saturated

            theme.accentPriority = Hex("#2f6fd0");
            theme.accentRamp = Ramp();

            // A solid ink edge, not a 16% hairline: the outline is the direction.
            theme.divider = Hex("#16212e");
            theme.outlineWidth = 8f;
            theme.cornerRadius = 34f;
            theme.dropOffset = 14f;
        }

        private static void FillDark(ThemeAsset theme)
        {
            theme.surfaceBase = Hex("#16222e");                // the night-shift floor
            theme.surfaceRaised = Hex("#22364a");              // raised further, to read under an outline
            theme.surfaceOverlay = Hex("#0d151d");             // the spotlight goes deeper still

            theme.textPrimary = Hex("#eef6ff");
            theme.textMuted = Hex("#8fa1b3");
            theme.textInverse = Hex("#1d1f20");

            theme.stateAffordable = Hex("#94bce3");
            theme.stateUnaffordable = Fade(Hex("#eef6ff"));
            theme.stateSpent = Hex("#5a6b7c");
            theme.stateReady = Hex("#46c98d");                 // the same green, lifted for dark
            theme.stateThinking = Hex("#8fa1b3");
            theme.stateTrouble = Hex("#f0803c");               // the ember carries both shifts

            theme.accentPriority = Hex("#4f92e8");
            // The same ramp reversed: consumers use low steps as quiet surfaces and high steps
            // as emphasis, and reversal preserves that contract on a dark ground.
            var ramp = Ramp();
            System.Array.Reverse(ramp);
            theme.accentRamp = ramp;
            // On dark, the outline is the ink of the ground rather than of the text: a near-black
            // edge is what makes a lit panel sit on top of the floor instead of merging with it.
            theme.divider = Hex("#0b1119");
            theme.outlineWidth = 8f;
            theme.cornerRadius = 34f;
            theme.dropOffset = 14f;
        }

        private static Color[] Ramp() => new[]
        {
            Hex("#eef6ff"), Hex("#cfe2ff"), Hex("#9dc2f5"),
            Hex("#6a9be8"), Hex("#2f6fd0"), Hex("#245aac"),
            Hex("#1b4685"), Hex("#12325f"), Hex("#16212e"),
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
