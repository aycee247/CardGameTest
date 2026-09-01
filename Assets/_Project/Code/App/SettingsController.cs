using Game.Audio;
using Game.Data;
using Game.Persistence;
using Game.UI;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// The presenter behind <see cref="SettingsView"/> (STORY-4.1, STORY-4.2). Lives in the
    /// composition root because it touches <c>Game.Persistence</c>, which <c>Game.UI</c> cannot
    /// see — the view renders numbers and knows nothing about where they came from.
    ///
    /// One of these is generated into each scene that offers settings: the menu and the Game
    /// scene (AC2). Both edit the same profile through the same service, so a change made
    /// mid-match is the change the menu shows afterwards.
    /// </summary>
    public sealed class SettingsController : MonoBehaviour
    {
        [SerializeField] private SettingsView view;

        [Tooltip("Preview clips for the volume sliders. Optional — without it the sliders still " +
                 "work, they just adjust silently.")]
        [SerializeField] private SfxCatalog sfx;

        [Tooltip("Seconds between preview sounds while dragging. Without this a slider drag " +
                 "fires a clip per frame, which is a noise, not a preview.")]
        [SerializeField] private float previewCooldownSeconds = 0.18f;

        private ISaveService _save;
        private IAudioService _audio;
        private float _nextPreviewAt;

        private void Start()
        {
            if (view == null) return;

            view.VolumeChanged += OnVolumeChanged;
            view.NameChanged += OnNameChanged;
            view.HapticsChanged += OnHapticsChanged;
            view.ReducedMotionChanged += OnReducedMotionChanged;
            view.UiScaleChanged += OnUiScaleChanged;

            if (!GameServices.IsReady) return;
            GameServices.Locator.TryGet<ISaveService>(out _save);
            GameServices.Locator.TryGet<IAudioService>(out _audio);
        }

        private void OnDestroy()
        {
            if (view == null) return;

            view.VolumeChanged -= OnVolumeChanged;
            view.NameChanged -= OnNameChanged;
            view.HapticsChanged -= OnHapticsChanged;
            view.ReducedMotionChanged -= OnReducedMotionChanged;
            view.UiScaleChanged -= OnUiScaleChanged;
        }

        /// <summary>
        /// Opens the panel, filled from the profile. Called by whichever button the scene put on
        /// screen. Without a profile — a scene opened straight from the editor — the panel still
        /// opens on the defaults rather than refusing, so the layout can be looked at.
        /// </summary>
        public void Open()
        {
            if (view == null) return;

            var settings = _save?.Profile?.Settings ?? new GameSettings();

            view.Render(settings.MasterVolume, settings.MusicVolume, settings.SfxVolume,
                LocalIdentity.RawDisplayName, settings.Haptics, settings.ReducedMotion,
                settings.UiScale);

            // Only an online match runs a clock that will not wait for this panel (STORY-2.7:
            // hot-seat and solo are untimed by design, so the warning would be a lie there).
            view.SetLiveMatchWarning(GameSceneBootstrap.IsOnline);
            view.Open();
        }

        private void OnVolumeChanged(SettingsView.VolumeChannel channel, float value)
        {
            if (_save?.Profile == null) return;

            var settings = _save.Profile.Settings;
            switch (channel)
            {
                case SettingsView.VolumeChannel.Master: settings.MasterVolume = value; break;
                case SettingsView.VolumeChannel.Music: settings.MusicVolume = value; break;
                default: settings.SfxVolume = value; break;
            }

            // MarkDirty, never Save: GameBootstrap already flushes on iOS pause and quit, which
            // is what survives the process being suspended rather than closed (#22 AC2). It also
            // raises ProfileChanged, which is what re-applies the mixer live (#22 AC4).
            _save.MarkDirty();

            PlayPreview(channel);
        }

        /// <summary>
        /// A sound per category, so a slider you cannot hear the effect of is not adjusted blind
        /// (#22 AC4). Rate-limited: a drag emits a value per frame, and one clip per frame is a
        /// buzz rather than a preview.
        /// </summary>
        private void PlayPreview(SettingsView.VolumeChannel channel)
        {
            if (_audio == null || sfx == null) return;
            if (Time.unscaledTime < _nextPreviewAt) return;

            _nextPreviewAt = Time.unscaledTime + Mathf.Max(0f, previewCooldownSeconds);

            switch (channel)
            {
                case SettingsView.VolumeChannel.Master: _audio.PlaySfx(sfx.dieSettle); break;
                case SettingsView.VolumeChannel.Music: _audio.PlaySfx(sfx.sparksChime); break;
                default: _audio.PlaySfx(sfx.claimTing); break;
            }
        }

        /// <summary>
        /// Mirrors the menu's name field. Both write through <see cref="LocalIdentity"/>, which
        /// sanitizes on the way in, so the two renderings cannot disagree about what was stored.
        /// </summary>
        private void OnNameChanged(string raw)
        {
            LocalIdentity.SetDisplayName(raw);

            // Echo back what was actually kept, so a name that was trimmed or capped shows the
            // player the version their opponents will see.
            var settings = _save?.Profile?.Settings ?? new GameSettings();
            view.Render(settings.MasterVolume, settings.MusicVolume, settings.SfxVolume,
                LocalIdentity.RawDisplayName, settings.Haptics, settings.ReducedMotion,
                settings.UiScale);
        }

        private void OnHapticsChanged(bool on)
        {
            if (_save?.Profile == null) return;

            _save.Profile.Settings.Haptics = on;
            _save.MarkDirty();
        }

        /// <summary>
        /// Writes the scale and lets the applier repaint the canvas. The panel the slider lives
        /// on resizes underneath the thumb, which is the clearest preview available.
        /// </summary>
        private void OnUiScaleChanged(float scale)
        {
            if (_save?.Profile == null) return;

            _save.Profile.Settings.UiScale = Mathf.Clamp(scale, UiScaleApplier.MinScale, UiScaleApplier.MaxScale);
            _save.MarkDirty();
        }

        private void OnReducedMotionChanged(bool on)
        {
            if (_save?.Profile == null) return;

            _save.Profile.Settings.ReducedMotion = on;

            // UiMotionSettingsApplier is listening for this and re-applies to the scene's
            // animation service, so the next tween is already calmer — no scene reload.
            _save.MarkDirty();
        }
    }
}
