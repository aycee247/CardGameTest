using System;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The settings panel (STORY-4.1): Audio, Gameplay, Accessibility.
    ///
    /// Passive, like every view here. It renders values it is handed and raises an event per
    /// change; it never reads or writes the profile, which lives behind an assembly wall it
    /// cannot see. <see cref="Game.App.SettingsController"/> is the half that persists.
    ///
    /// The same panel is generated into both the menu and the Game scene (AC2). In a live online
    /// match the phase clock keeps running behind it — there is no pause in a simultaneous game —
    /// so <see cref="SetLiveMatchWarning"/> puts that on screen rather than letting someone lose
    /// a round to a volume slider.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text liveMatchWarning;

        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Gameplay")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button hapticsToggle;

        [Header("Accessibility")]
        [SerializeField] private Button reducedMotionToggle;

        [Tooltip("Colours for the on/off buttons. UiFactory is Editor-only, so the runtime repaint " +
                 "lives here — the same arrangement SeatRowView uses.")]
        [SerializeField] private ThemeAsset theme;

        /// <summary>Which volume moved, so the presenter knows which preview to play.</summary>
        public enum VolumeChannel { Master, Music, Sfx }

        public event Action<VolumeChannel, float> VolumeChanged;
        public event Action<string> NameChanged;
        public event Action<bool> HapticsChanged;
        public event Action<bool> ReducedMotionChanged;
        public event Action Closed;

        private bool _haptics;
        private bool _reducedMotion;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (masterSlider != null)
                masterSlider.onValueChanged.AddListener(v => VolumeChanged?.Invoke(VolumeChannel.Master, v));
            if (musicSlider != null)
                musicSlider.onValueChanged.AddListener(v => VolumeChanged?.Invoke(VolumeChannel.Music, v));
            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(v => VolumeChanged?.Invoke(VolumeChannel.Sfx, v));

            if (nameInput != null)
                nameInput.onEndEdit.AddListener(value => NameChanged?.Invoke(value ?? string.Empty));

            if (hapticsToggle != null) hapticsToggle.onClick.AddListener(() =>
            {
                _haptics = !_haptics;
                PaintToggle(hapticsToggle, _haptics);
                HapticsChanged?.Invoke(_haptics);
            });

            if (reducedMotionToggle != null) reducedMotionToggle.onClick.AddListener(() =>
            {
                _reducedMotion = !_reducedMotion;
                PaintToggle(reducedMotionToggle, _reducedMotion);
                ReducedMotionChanged?.Invoke(_reducedMotion);
            });

            if (closeButton != null) closeButton.onClick.AddListener(() =>
            {
                Close();
                Closed?.Invoke();
            });

            // No Close() here: this component lives on the panel the generator leaves inactive,
            // so Awake does not run until Open() activates it. Same arrangement as HowToPlayView.
        }

        /// <summary>
        /// Fills every control from the values the presenter read out of the profile. Uses the
        /// without-notify setters throughout: seeding a control must never read back as the
        /// player having changed it, which would write the value straight back and play a
        /// preview sound at whoever just opened the panel.
        /// </summary>
        public void Render(float master, float music, float sfx, string displayName,
            bool haptics, bool reducedMotion)
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
            if (nameInput != null) nameInput.SetTextWithoutNotify(displayName ?? string.Empty);

            _haptics = haptics;
            _reducedMotion = reducedMotion;
            PaintToggle(hapticsToggle, haptics);
            PaintToggle(reducedMotionToggle, reducedMotion);
        }

        /// <summary>
        /// Shown only over a live server-clocked match, where the round does not wait. Hot-seat
        /// and solo are untimed by design (STORY-2.7), so the line stays hidden there.
        /// </summary>
        public void SetLiveMatchWarning(bool visible)
        {
            if (liveMatchWarning != null) liveMatchWarning.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Repaints an on/off button for its state. The label carries the value, and the fill
        /// carries it a second time — a control whose only signal is colour is unreadable to a
        /// good number of players, and #25 has the rest of that work.
        /// </summary>
        private void PaintToggle(Button toggle, bool on)
        {
            if (toggle == null) return;

            var label = toggle.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = on ? "ON" : "OFF";

            if (theme == null) return;
            if (label != null) label.color = on ? theme.textInverse : theme.textPrimary;

            // The colour lives on the Fill child; the root image is the button's drop shadow.
            var fill = toggle.transform.Find("Fill");
            var image = fill != null ? fill.GetComponent<Image>() : toggle.GetComponent<Image>();
            if (image != null) image.color = on ? theme.accentPriority : theme.surfaceRaised;
        }

        public void Open()
        {
            if (root != null) root.SetActive(true);
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
