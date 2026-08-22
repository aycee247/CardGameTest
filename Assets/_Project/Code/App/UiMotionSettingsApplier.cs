using Game.Persistence;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Applies the profile's motion settings to this scene's animation service. Reduced motion and
    /// animation speed are enforced by routing — every gameplay tween runs through the one service
    /// this configures — rather than by diligence (STORY-4.5 AC2, docs/design/ui-conventions.md).
    /// Without a service graph (editor direct-open) the authored defaults stand.
    /// </summary>
    [RequireComponent(typeof(UiAnimationService))]
    public sealed class UiMotionSettingsApplier : MonoBehaviour
    {
        private void Start()
        {
            if (!GameServices.IsReady) return;
            if (!GameServices.Locator.TryGet<ISaveService>(out var save)) return;

            var anims = GetComponent<UiAnimationService>();
            anims.ReducedMotion = save.Profile.Settings.ReducedMotion;
            anims.SpeedMultiplier = Mathf.Max(0.1f, save.Profile.Settings.AnimationSpeed);
        }
    }
}
