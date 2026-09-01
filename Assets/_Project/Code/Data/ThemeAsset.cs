using TMPro;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// The single authority for UI chrome (docs/design/theming.md): the 13 semantic colour tokens,
    /// the accent ramp, and the type faces. The scene generator reads it at generation time;
    /// runtime views re-read it on every render, so a theme swap shows up without regenerating.
    /// Values are written by Foundry ▸ Generate Theme — regenerate rather than hand-edit.
    /// </summary>
    [CreateAssetMenu(fileName = "Theme_", menuName = "Foundry/Theme Asset")]
    public sealed class ThemeAsset : ScriptableObject
    {
        [Header("Surface")]
        public Color surfaceBase;
        public Color surfaceRaised;
        public Color surfaceOverlay;

        [Header("Text")]
        public Color textPrimary;
        public Color textMuted;
        public Color textInverse;

        [Header("State")]
        public Color stateAffordable;
        public Color stateUnaffordable;
        public Color stateSpent;
        public Color stateReady;
        public Color stateThinking;
        public Color stateTrouble;

        [Header("Accent")]
        public Color accentPriority;

        [Tooltip("Tint ramp: index 0 = step 100 (palest) … index 8 = step 900 (deepest).")]
        public Color[] accentRamp = new Color[9];

        [Tooltip("Hairline borders — the text colour at low opacity.")]
        public Color divider;

        [Header("Shape")]
        [Tooltip("Outline weight in layout units. The chunky direction draws a solid ink edge " +
                 "where the blueprint direction drew a hairline and corner marks.")]
        public float outlineWidth = 8f;

        [Tooltip("Corner radius in layout units, applied through the generated rounded sprite.")]
        public float cornerRadius = 34f;

        [Tooltip("How far the hard drop shadow sits below a raised control, in layout units. " +
                 "Zero draws no shadow, which is what the flat blueprint direction wants.")]
        public float dropOffset = 14f;

        [Header("Type")]
        public TMP_FontAsset bodyRegular;      // Barlow 400
        public TMP_FontAsset bodyMedium;       // Barlow 500
        public TMP_FontAsset bodySemibold;     // Barlow 600
        public TMP_FontAsset headingSemibold;  // Barlow Condensed 600
        public TMP_FontAsset headingBold;      // Barlow Condensed 700

        /// <summary>Ramp accessor by design-token step: <c>Accent(700)</c> is the 700 tint.</summary>
        public Color Accent(int step)
        {
            int index = Mathf.Clamp(step / 100 - 1, 0, accentRamp.Length - 1);
            return accentRamp[index];
        }
    }
}
