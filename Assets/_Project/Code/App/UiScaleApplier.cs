using Game.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace Game.App
{
    /// <summary>
    /// Applies the profile's UI scale to this scene's canvas (STORY-4.5 AC1).
    ///
    /// It moves the <see cref="CanvasScaler"/>'s reference width rather than any font size. The
    /// canvas matches width, so a narrower reference makes every authored unit render larger —
    /// type, controls, spacing and touch targets together, in exactly the proportions the layout
    /// was built in. Nothing can overflow its box, because the box grew with it.
    ///
    /// That makes this a UI scale rather than iOS Dynamic Type: text does not reflow into more
    /// lines, the whole interface simply gets bigger and less of it fits on screen. It is the
    /// honest option for a layout of fixed boxes, and it is the one a player can judge by looking.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class UiScaleApplier : MonoBehaviour
    {
        /// <summary>The authored reference width every layout is drawn against.</summary>
        public const float ReferenceWidth = 1080f;

        /// <summary>
        /// Beyond about a third larger, a six-seat rail and a five-card market stop fitting the
        /// screen at all — past that the setting would be trading one accessibility problem for
        /// another.
        /// </summary>
        public const float MinScale = 1f;
        public const float MaxScale = 1.3f;

        private CanvasScaler _scaler;
        private ISaveService _save;
        private float _authoredHeight;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            _authoredHeight = _scaler.referenceResolution.y;
        }

        private void Start()
        {
            if (!GameServices.IsReady) return;
            if (!GameServices.Locator.TryGet<ISaveService>(out _save)) return;

            // Re-apply on every profile change, so the slider moves the screen under the panel
            // the player is dragging it on.
            _save.ProfileChanged += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            if (_save != null) _save.ProfileChanged -= Apply;
        }

        private void Apply()
        {
            if (_scaler == null || _save?.Profile == null) return;

            float scale = Mathf.Clamp(_save.Profile.Settings.UiScale, MinScale, MaxScale);
            _scaler.referenceResolution = new Vector2(ReferenceWidth / scale, _authoredHeight);
        }
    }
}
