using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Entrance beats shared by the sheets and toasts (UI-character P1). Exits stay instant on
    /// purpose: an exit tween races the SetActive lifecycle for no expressive gain, and a
    /// dismissal should feel immediate anyway. Both helpers no-op gracefully without a service,
    /// and reduced motion collapses them inside <see cref="UiAnimationService.Play"/>.
    /// </summary>
    internal static class UiEntrance
    {
        /// <summary>A stamped arrival: lands slightly large and settles. For modals and sheets.</summary>
        public static void StampIn(UiAnimationService anims, Transform target)
        {
            if (target == null) return;
            if (anims == null)
            {
                target.localScale = Vector3.one;
                return;
            }

            anims.Play(0.2f, UiEase.OutCubic, t => target.localScale = Vector3.one * (1.06f - 0.06f * t));
        }

        /// <summary>Slides in from <paramref name="fromOffset"/> to the authored rest position.</summary>
        public static void SlideIn(UiAnimationService anims, RectTransform target, Vector2 restPosition, Vector2 fromOffset)
        {
            if (target == null) return;
            if (anims == null)
            {
                target.anchoredPosition = restPosition;
                return;
            }

            anims.Play(0.22f, UiEase.OutCubic, t => target.anchoredPosition = restPosition + fromOffset * (1f - t));
        }
    }
}
