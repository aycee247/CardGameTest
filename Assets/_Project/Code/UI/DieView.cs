using System;
using Game.Data;
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

        [Tooltip("Colour tokens, re-read every render so a theme swap shows without regenerating.")]
        [SerializeField] private ThemeAsset theme;

        public int Index { get; private set; }

        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(Index));
        }

        public void Set(int index, int face, bool spent, bool selected, bool interactable)
        {
            Index = index;

            if (faceText != null)
            {
                faceText.text = face.ToString();
                if (theme != null) faceText.color = selected ? theme.textInverse : theme.textPrimary;
            }

            if (button != null) button.interactable = interactable && !spent;

            if (background != null && theme != null)
                background.color = spent ? theme.stateSpent
                    : selected ? theme.accentPriority
                    : theme.surfaceBase;
        }
    }
}
