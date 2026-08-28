using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Machine-button press feel (UI-character P1): pointer-down sinks the button, release
    /// springs it back with overshoot — every button in the game reads as a physical control.
    /// Added by <c>UiFactory.Button</c>, so it is universal by construction.
    ///
    /// The animation service is resolved from the canvas in <see cref="Awake"/> rather than
    /// SetRef-wired: this component is on every button, and per-button wiring does not scale.
    /// With no service in the parents (tests, orphaned prefabs) it degrades to a snap.
    /// </summary>
    public sealed class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.96f;

        private UiAnimationService _anims;
        private AnimHandle _tween;
        private bool _down;

        private void Awake()
        {
            _anims = GetComponentInParent<UiAnimationService>(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            ScaleTo(PressedScale, UiEase.OutCubic, 0.06f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
            ScaleTo(1f, UiEase.OutBack, 0.18f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Dragging off a held button releases it visually, matching Unity's click semantics.
            if (!_down) return;
            _down = false;
            ScaleTo(1f, UiEase.OutCubic, 0.1f);
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            _down = false;
        }

        private void ScaleTo(float target, UiEase ease, float duration)
        {
            if (_anims == null)
            {
                transform.localScale = Vector3.one * target;
                return;
            }

            _anims.Skip(_tween);
            float from = transform.localScale.x;
            _tween = _anims.Play(duration, ease, t =>
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, t));
        }
    }
}
