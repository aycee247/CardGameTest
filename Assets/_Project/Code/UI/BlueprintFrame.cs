using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Border weight: hairline, accent tint, or full accent.</summary>
    public enum FrameEmphasis { Divider, Accent, AccentStrong }

    /// <summary>
    /// The Industry system's signature motif: a 1px border with four "+" corner registration
    /// marks. One implementation for the menu hero, market cards, sheets, dialogs, the Done
    /// button and standings rows — built by UiFactory, restyled at runtime through this.
    /// </summary>
    public sealed class BlueprintFrame : MonoBehaviour
    {
        [SerializeField] private Image[] edges;         // top, bottom, left, right
        [SerializeField] private Image[] cornerMarks;   // 8 bars forming four "+"
        [SerializeField] private ThemeAsset theme;

        /// <summary>Editor-time wiring; values serialize with the generated scene.</summary>
        public void Bind(Image[] borderEdges, Image[] marks, ThemeAsset themeAsset)
        {
            edges = borderEdges;
            cornerMarks = marks;
            theme = themeAsset;
        }

        public void SetEmphasis(FrameEmphasis emphasis)
        {
            if (theme == null) return;

            switch (emphasis)
            {
                case FrameEmphasis.Accent:
                    SetBorderColor(theme.Accent(400), theme.accentPriority);
                    break;
                case FrameEmphasis.AccentStrong:
                    SetBorderColor(theme.accentPriority, theme.accentPriority);
                    break;
                default:
                    SetBorderColor(theme.divider, theme.divider);
                    break;
            }
        }

        /// <summary>Direct colour control for states the three emphases don't cover.</summary>
        public void SetBorderColor(Color border, Color? marks = null)
        {
            if (edges != null)
                foreach (var edge in edges)
                    if (edge != null) edge.color = border;

            var markColor = marks ?? border;
            if (cornerMarks != null)
                foreach (var mark in cornerMarks)
                    if (mark != null) mark.color = markColor;
        }

        public void SetMarksVisible(bool visible)
        {
            if (cornerMarks == null) return;
            foreach (var mark in cornerMarks)
                if (mark != null) mark.gameObject.SetActive(visible);
        }
    }
}
