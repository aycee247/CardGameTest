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
