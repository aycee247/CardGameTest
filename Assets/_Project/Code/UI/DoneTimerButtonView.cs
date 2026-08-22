using System;
using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>What the Done/timer button offers right now (handoff 6g).</summary>
    public enum DoneButtonState
    {
        /// <summary>Shape phase, actionable: solid accent "DONE".</summary>
        Done,
        /// <summary>Commit phase, undecided: a deliberately disabled-looking "PICK".</summary>
        Pick,
        /// <summary>Committed, passed or done: muted "LOCKED".</summary>
        Locked,
        /// <summary>No input phase: "—".</summary>
        Inactive
    }

    /// <summary>
    /// The bottom bar's square Done button with the phase clock drawn around its perimeter —
    /// commit state and time remaining are the two vital facts, so they live in one permanent
    /// fixture (UI-1, UI-2). The ring renders the server's authoritative deadline; the last five
    /// seconds turn urgent and pulse through the animation service.
    /// </summary>
    public sealed class DoneTimerButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private SquareTimerRing track;
        [SerializeField] private SquareTimerRing progress;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text secondsLabel;
        [SerializeField] private UiAnimationService anims;
        [SerializeField] private ThemeAsset theme;

        private AnimHandle _pulse;
        private bool _urgent;

        public event Action Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke());
        }

        public void SetState(DoneButtonState state)
        {
            if (theme == null) return;

            switch (state)
            {
                case DoneButtonState.Done:
                    Style("DONE", theme.accentPriority, theme.textInverse, interactable: true);
                    break;
                case DoneButtonState.Pick:
                    Style("PICK", theme.surfaceRaised, theme.textMuted, interactable: false);
                    break;
                case DoneButtonState.Locked:
                    Style("LOCKED", theme.surfaceRaised, theme.textMuted, interactable: false);
                    break;
                default:
                    Style("—", UiClear(theme.surfaceBase), theme.textMuted, interactable: false);
                    break;
            }
        }

        /// <summary>
        /// Renders the clock: <paramref name="duration"/> is the phase's full length from the
        /// config echo, so the ring never guesses its denominator. Negative seconds hide it.
        /// </summary>
        public void Tick(float secondsLeft, float duration)
        {
            bool ticking = secondsLeft >= 0f && duration > 0f && !float.IsInfinity(duration);

            if (track != null) track.enabled = ticking;
            if (progress != null) progress.enabled = ticking;
            if (secondsLabel != null)
            {
                secondsLabel.gameObject.SetActive(ticking);
                if (ticking) secondsLabel.text = Mathf.CeilToInt(secondsLeft).ToString();
            }

            if (!ticking)
            {
                SetUrgent(false);
                return;
            }

            if (progress != null) progress.Fill01 = Mathf.Clamp01(secondsLeft / duration);
            SetUrgent(secondsLeft <= 5f);
        }

        private void SetUrgent(bool urgent)
        {
            if (urgent == _urgent) return;
            _urgent = urgent;

            if (theme != null && progress != null)
                progress.color = urgent ? theme.Accent(900) : theme.accentPriority;

            if (anims != null)
            {
                anims.Skip(_pulse);
                _pulse = urgent
                    ? anims.Loop(0.55f, t =>
                        transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * Mathf.PI * 2f)))
                    : default;
                if (!urgent) transform.localScale = Vector3.one;
            }
        }

        private void Style(string text, Color fillColor, Color textColor, bool interactable)
        {
            if (label != null)
            {
                label.text = text;
                label.color = textColor;
            }

            if (fill != null) fill.color = fillColor;
            if (button != null) button.interactable = interactable;
        }

        private static Color UiClear(Color color)
        {
            color.a = 0f;
            return color;
        }
    }
}
