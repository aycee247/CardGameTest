using Game.Data;
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
