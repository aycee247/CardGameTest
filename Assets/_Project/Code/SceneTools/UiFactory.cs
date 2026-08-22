using Game.Data;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SceneTools
{
    /// <summary>Which face of the theme's type set a label uses.</summary>
    internal enum FontRole { Body, BodyMedium, BodySemibold, Heading, HeadingBold }

    /// <summary>
    /// Primary is the only solid-accent object on any screen (Industry convention); Secondary is a
    /// raised surface; Ghost is bare text on nothing.
    /// </summary>
    internal enum ButtonStyle { Primary, Secondary, Ghost }

    /// <summary>
    /// Small helpers for building functional uGUI + TMP widgets from an editor script. Every
    /// colour and typeface comes from the active <see cref="Theme"/> — no literals here, per
    /// docs/design/ui-conventions.md. SceneScaffolder assigns the theme before building.
    /// </summary>
    internal static class UiFactory
    {
        /// <summary>The theme scenes are generated against. Set by SceneScaffolder.Generate.</summary>
        internal static ThemeAsset Theme;

        public static RectTransform Panel(Transform parent, string name, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            return rt;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text,
            Vector2 anchoredPos, Vector2 size, float fontSize = 42f,
            TextAlignmentOptions align = TextAlignmentOptions.Center,
            FontRole role = FontRole.Body, Color? color = null, float letterSpacingEm = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.font = FontFor(role);
            tmp.color = color ?? Theme.textPrimary;

            // TMP's characterSpacing is in font units ≈ em/100.
            if (letterSpacingEm != 0f) tmp.characterSpacing = letterSpacingEm * 100f;

            return tmp;
        }

        public static Button Button(Transform parent, string name, string label,
            Vector2 anchoredPos, Vector2 size, ButtonStyle style = ButtonStyle.Primary)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            Color labelColor;
            switch (style)
            {
                case ButtonStyle.Primary:
                    image.color = Theme.accentPriority;
                    labelColor = Theme.textInverse;
                    break;
                case ButtonStyle.Secondary:
                    image.color = Theme.surfaceRaised;
                    labelColor = Theme.textPrimary;
                    break;
                default:   // Ghost: invisible fill that still catches taps.
                    image.color = WithAlpha(Theme.surfaceBase, 0f);
                    labelColor = Theme.textPrimary;
                    break;
            }

            var textRt = Label(rt, "Text", label, Vector2.zero, size, 40f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, labelColor, 0.04f).rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return go.GetComponent<Button>();
        }

        public static TMP_InputField InputField(Transform parent, string name, string placeholder,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = Theme.surfaceBase;

            var input = go.AddComponent<TMP_InputField>();

            var textArea = Panel(rt, "TextArea");
            textArea.offsetMin = new Vector2(12, 6);
            textArea.offsetMax = new Vector2(-12, -6);

            var placeholderTmp = Label(textArea, "Placeholder", placeholder, Vector2.zero, size, 34f,
                TextAlignmentOptions.Left, FontRole.Body, Theme.textMuted);
            Stretch(placeholderTmp.rectTransform);

            var textTmp = Label(textArea, "Text", "", Vector2.zero, size, 34f,
                TextAlignmentOptions.Left, FontRole.Body, Theme.textPrimary);
            Stretch(textTmp.rectTransform);

            input.textViewport = textArea;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.text = "";
            return input;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The Industry system's signature motif: a hairline border plus four "+" corner
        /// registration marks, straddling the host's corners. One builder for every screen.
        /// </summary>
        public static BlueprintFrame BlueprintFrame(RectTransform host,
            FrameEmphasis emphasis = FrameEmphasis.Divider, bool marks = true)
        {
            const float edge = 3f;        // ~1 handoff px
            const float markLength = 19f; // ~7 px
            const float markThick = 3f;

            var frameRt = Panel(host, "Frame");

            var edges = new Image[4];
            edges[0] = EdgeImage(frameRt, "EdgeTop", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, edge));
            edges[1] = EdgeImage(frameRt, "EdgeBottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, edge));
            edges[2] = EdgeImage(frameRt, "EdgeLeft", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(edge, 0));
            edges[3] = EdgeImage(frameRt, "EdgeRight", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), new Vector2(edge, 0));

            var markImages = new Image[8];
            var corners = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
            for (int i = 0; i < corners.Length; i++)
            {
                markImages[i * 2] = CornerBar(frameRt, $"Mark{i}H", corners[i], new Vector2(markLength, markThick));
                markImages[i * 2 + 1] = CornerBar(frameRt, $"Mark{i}V", corners[i], new Vector2(markThick, markLength));
            }

            var frame = frameRt.gameObject.AddComponent<BlueprintFrame>();
            frame.Bind(edges, markImages, Theme);
            frame.SetEmphasis(emphasis);
            frame.SetMarksVisible(marks);
            return frame;
        }

        private static Image EdgeImage(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Image CornerBar(RectTransform parent, string name, Vector2 corner, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = corner;
            rt.anchorMax = corner;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public static TMP_FontAsset FontFor(FontRole role)
        {
            switch (role)
            {
                case FontRole.BodyMedium: return Theme.bodyMedium;
                case FontRole.BodySemibold: return Theme.bodySemibold;
                case FontRole.Heading: return Theme.headingSemibold;
                case FontRole.HeadingBold: return Theme.headingBold;
                default: return Theme.bodyRegular;
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
