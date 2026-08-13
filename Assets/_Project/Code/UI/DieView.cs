using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One die in the player's tray. Passive: it renders a face and a state, and reports taps by
    /// index. Selection means "this die is part of what I am about to spend or shape" — the view
    /// above owns that decision, not this one.
    /// </summary>
    public sealed class DieView : MonoBehaviour
    {
        [SerializeField] private TMP_Text faceText;
        [SerializeField] private Button button;
        [SerializeField] private Image background;

        [Header("State colours")]
        [SerializeField] private Color idleColor = new Color(0.96f, 0.96f, 0.94f);
        [SerializeField] private Color selectedColor = new Color(0.94f, 0.72f, 0.29f);
        [SerializeField] private Color spentColor = new Color(0.55f, 0.57f, 0.60f);

        public int Index { get; private set; }

        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(Index));
        }

        public void Set(int index, int face, bool spent, bool selected, bool interactable)
        {
            Index = index;

            if (faceText != null) faceText.text = face.ToString();
            if (button != null) button.interactable = interactable && !spent;

            if (background != null)
                background.color = spent ? spentColor : selected ? selectedColor : idleColor;
        }
    }
}
