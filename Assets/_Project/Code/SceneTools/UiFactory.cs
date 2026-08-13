using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.SceneTools
{
    /// <summary>
    /// Small helpers for building functional uGUI + TMP widgets from an editor script.
    /// Deliberately utilitarian — enough to get a runnable, tappable UI; restyle in the editor.
    /// </summary>
    internal static class UiFactory
    {
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
            Vector2 anchoredPos, Vector2 size, float fontSize = 42f, TextAlignmentOptions align = TextAlignmentOptions.Center)
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
            return tmp;
        }

        public static Button Button(Transform parent, string name, string label,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            go.GetComponent<Image>().color = new Color(0.20f, 0.42f, 0.85f, 1f);

            var textRt = Label(rt, "Text", label, Vector2.zero, size, 40f).rectTransform;
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
            go.GetComponent<Image>().color = Color.white;

            var input = go.AddComponent<TMP_InputField>();

            var textArea = Panel(rt, "TextArea");
            textArea.offsetMin = new Vector2(12, 6);
            textArea.offsetMax = new Vector2(-12, -6);

            var placeholderTmp = Label(textArea, "Placeholder", placeholder, Vector2.zero, size, 34f, TextAlignmentOptions.Left);
            Stretch(placeholderTmp.rectTransform);
            placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            var textTmp = Label(textArea, "Text", "", Vector2.zero, size, 34f, TextAlignmentOptions.Left);
            Stretch(textTmp.rectTransform);
            textTmp.color = Color.black;

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
    }
}
