using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Fits a RectTransform to the device safe area so UI clears the notch / home indicator /
    /// rounded corners on iPhone. Re-applies when the safe area or orientation changes, so it
    /// works for both portrait and landscape (auto-rotate). Put it on a full-screen panel that
    /// wraps your content.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private ScreenOrientation _lastOrientation;
        private Vector2Int _lastResolution;

        private void Awake() => _rect = GetComponent<RectTransform>();

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.orientation != _lastOrientation ||
                Screen.width != _lastResolution.x ||
                Screen.height != _lastResolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rect == null) return;

            var safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastOrientation = Screen.orientation;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width == 0 || Screen.height == 0) return;

            Vector2 anchorMin = safe.position;
            Vector2 anchorMax = safe.position + safe.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
