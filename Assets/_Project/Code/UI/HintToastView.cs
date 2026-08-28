using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The first-time hint toast (handoff 6i): a dark bottom-anchored strip with one line of
    /// onboarding copy and a GOT IT dismiss. Shown at most once per hint; the presenter decides
    /// when, the app layer persists "seen".
    /// </summary>
    public sealed class HintToastView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button gotItButton;

        public event Action Dismissed;

        private UiAnimationService _anims;
        private Vector2 _restPosition;
        private bool _restCached;

        private void Awake()
        {
            if (gotItButton != null) gotItButton.onClick.AddListener(() =>
            {
                Hide();
                Dismissed?.Invoke();
            });
            _anims = GetComponentInParent<UiAnimationService>(true);
        }

        public void Show(string message)
        {
            if (bodyText != null) bodyText.text = message ?? string.Empty;
            if (root != null)
            {
                bool wasOpen = root.activeSelf;
                root.SetActive(true);

                var rt = (RectTransform)root.transform;
                if (!_restCached)
                {
                    _restPosition = rt.anchoredPosition;
                    _restCached = true;
                }
                if (!wasOpen) UiEntrance.SlideIn(_anims, rt, _restPosition, new Vector2(0f, -60f));
            }
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
