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

            // Wrap by default. Left unset, long prose rendered as one line and ran off both edges
            // of the phone — the explainer was the first screen with a sentence that had no
            // hand-placed newline in it, so nothing had caught it. The handful of labels that
            // genuinely must not wrap (rail names, chips) set NoWrap after this returns.
            tmp.textWrappingMode = TextWrappingModes.Normal;

            // Text never takes input. A TMP graphic defaults to raycastTarget = true, and this
            // factory builds every label in the game, so that default meant any label lying over
            // a button quietly ate the taps: on device the menu's full-width status line sat
            // across the offline row and only the button edges responded. A label inside a button
            // is unaffected — the event bubbles to the Button either way.
            tmp.raycastTarget = false;

            return tmp;
        }

        public static Button Button(Transform parent, string name, string label,
            Vector2 anchoredPos, Vector2 size, ButtonStyle style = ButtonStyle.Primary,
            float fontSize = 40f)
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

            var textRt = Label(rt, "Text", label, Vector2.zero, size, fontSize,
                TextAlignmentOptions.Center, FontRole.BodySemibold, labelColor, 0.04f).rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Every button is a machine button (UI-character P1): press sinks, release springs.
            go.AddComponent<PressableButton>();

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
            // surfaceBase is a hair off the page colour, which left the field invisible on device
            // — "Your name" read as a caption rather than something to tap. Raised fill plus the
            // project's own border makes it look like the control it is.
            go.GetComponent<Image>().color = Theme.surfaceRaised;
            BlueprintFrame(rt, marks: false);

            var input = go.AddComponent<TMP_InputField>();

            var textArea = Panel(rt, "TextArea");
            textArea.offsetMin = new Vector2(12, 6);
            textArea.offsetMax = new Vector2(-12, -6);

            var placeholderTmp = Label(textArea, "Placeholder", placeholder, Vector2.zero, size, 40f,
                TextAlignmentOptions.Left, FontRole.Body, Theme.textMuted);
            Stretch(placeholderTmp.rectTransform);

            var textTmp = Label(textArea, "Text", "", Vector2.zero, size, 40f,
                TextAlignmentOptions.Left, FontRole.Body, Theme.textPrimary);
            Stretch(textTmp.rectTransform);

            input.textViewport = textArea;
            input.textComponent = textTmp;
            input.placeholder = placeholderTmp;
            input.text = "";
            return input;
        }

        /// <summary>
        /// A themed slider: track, fill and handle. The first continuous control in the project —
        /// volume is a real range, and stepping it with buttons would be worse for the one thing
        /// a player adjusts by ear.
        /// </summary>
        public static Slider Slider(Transform parent, string name, Vector2 anchoredPos, Vector2 size,
            float value = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            // The track is thinner than the control: the whole rect stays tappable, so a thumb
            // that misses the line by a few pixels still moves the slider.
            var track = Panel(rt, "Track", stretch: false);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(0f, 12f);
            track.anchoredPosition = Vector2.zero;
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = Theme.divider;

            var fillArea = Panel(rt, "FillArea", stretch: false);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0f, 12f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = Panel(fillArea, "Fill", stretch: false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Theme.accentPriority;

            var handleArea = Panel(rt, "HandleArea", stretch: false);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(22f, 0f);
            handleArea.offsetMax = new Vector2(-22f, 0f);

            var handle = Panel(handleArea, "Handle", stretch: false);
            handle.sizeDelta = new Vector2(44f, 44f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Theme.accentPriority;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        /// <summary>
        /// An on/off control built from the ordinary <see cref="Button"/> rather than Unity's
        /// Toggle. A toggle would be a second interaction model for the sake of one boolean;
        /// this way the press sinks and springs like every other button in the game, and the
        /// label carries the state.
        ///
        /// The caller owns the value — call <see cref="SetToggleLabel"/> when it changes.
        /// </summary>
        public static Button ToggleButton(Transform parent, string name, Vector2 anchoredPos,
            Vector2 size, bool on)
        {
            var button = Button(parent, name, on ? "ON" : "OFF", anchoredPos, size,
                on ? ButtonStyle.Primary : ButtonStyle.Secondary, fontSize: 38f);
            BlueprintFrame((RectTransform)button.transform, marks: false);
            return button;
        }

        /// <summary>Repaints a <see cref="ToggleButton"/> for its new state.</summary>
        public static void SetToggleLabel(Button toggle, bool on)
        {
            if (toggle == null) return;

            var label = toggle.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = on ? "ON" : "OFF";
                label.color = on ? Theme.textInverse : Theme.textPrimary;
            }

            var image = toggle.GetComponent<Image>();
            if (image != null) image.color = on ? Theme.accentPriority : Theme.surfaceRaised;
        }

        /// <summary>
        /// Apple's minimum touch target is 44pt. Layouts here are authored against a 1080-unit
        /// reference width that maps to the device's width in points, so on a ~400pt phone one
        /// unit is ~0.37pt and 44pt is ~120 units.
        /// </summary>
        public const float MinTouchUnits = 120f;

        /// <summary>
        /// Grows a control's *touchable* area to <see cref="MinTouchUnits"/> without changing how
        /// big it looks, by parenting an invisible graphic that catches the taps its neighbour
        /// misses. Taps on the child bubble to the Selectable, so a small icon button stays small
        /// and still takes a thumb.
        ///
        /// Preferred over simply drawing the control bigger: the top bar and the shape row have
        /// no room to spare, and a 44pt icon would crowd them.
        /// </summary>
        public static void ExpandHitArea(Component control, float minUnits = MinTouchUnits)
        {
            if (control == null) return;

            var rt = (RectTransform)control.transform;

            // sizeDelta, not rect: rect is only correct after a layout pass, and at generation
            // time there has not been one — reading it here handed out hit areas to controls that
            // were already big enough. For a point-anchored control (which every control this
            // generator makes is) sizeDelta is the exact size.
            bool pointAnchored = rt.anchorMin == rt.anchorMax;
            var size = pointAnchored ? rt.sizeDelta : rt.rect.size;
            if (size.x >= minUnits && size.y >= minUnits) return;

            var go = new GameObject("HitArea", typeof(RectTransform), typeof(Image));
            var hit = (RectTransform)go.transform;
            hit.SetParent(rt, false);
            hit.anchorMin = new Vector2(0.5f, 0.5f);
            hit.anchorMax = new Vector2(0.5f, 0.5f);
            hit.sizeDelta = new Vector2(Mathf.Max(size.x, minUnits), Mathf.Max(size.y, minUnits));
            hit.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            // Behind the visible fill, so it can never draw over the control it is enlarging.
            hit.SetAsFirstSibling();
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
