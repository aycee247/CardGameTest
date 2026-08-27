using System;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// How a die reads right now. Dimmed means a card's cost is being inspected and this die
    /// doesn't contribute; Spent means the die is pledged to a commit and out of play.
    /// </summary>
    public enum DieVisualState { Idle, Selected, Dimmed, Spent }

    /// <summary>
    /// One die in the player's tray. Passive: it renders a face and a state, and reports taps by
    /// index. Faces are pip layouts (never colour alone); the border, fill and a small lift carry
    /// the state, so every state differs by shape as well as colour.
    /// </summary>
    public sealed class DieView : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("Everything visible lives here; it lifts when selected while the hit area stays put.")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Image background;
        [SerializeField] private BlueprintFrame frame;
        [SerializeField] private DiePipGrid pips;
        [SerializeField] private GameObject spentWatermark;
        [SerializeField] private ThemeAsset theme;

        /// <summary>Selected dice lift by the handoff's 3px, in canvas units.</summary>
        private const float SelectedLift = 8f;

        public int Index { get; private set; }

        public event Action<int> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(Index));
        }

        public void Set(int index, int face, DieVisualState state, bool interactable)
        {
            Index = index;

            if (pips != null) pips.SetFace(face);
            if (button != null) button.interactable = interactable && state != DieVisualState.Spent;
            if (spentWatermark != null) spentWatermark.SetActive(state == DieVisualState.Spent);

            if (body != null)
                body.anchoredPosition = state == DieVisualState.Selected
                    ? new Vector2(0f, SelectedLift)
                    : Vector2.zero;

            if (theme == null) return;

            switch (state)
            {
                case DieVisualState.Selected:
                    Paint(theme.accentPriority, theme.Accent(800), theme.textInverse);
                    break;

                case DieVisualState.Dimmed:
                    Paint(Clear(theme.surfaceBase), theme.divider, theme.textMuted);
                    break;

                case DieVisualState.Spent:
                    Paint(Clear(theme.surfaceBase), theme.divider, theme.stateSpent);
                    break;

                default:
                    Paint(theme.surfaceBase, theme.Accent(700), theme.textPrimary);
                    break;
            }
        }

        /// <summary>
        /// Roll-phase presentation only: cycles the displayed face while the server's authoritative
        /// roll is in flight (NET-1). Never called with a face the player could mistake for state —
        /// the next Render overwrites it with the real one.
        /// </summary>
        /// <summary>
        /// Scales the fixed-size internals (pips, SPENT watermark) when the tray shrinks its
        /// cells to keep a grown pool on one row (#68). The background and frame stretch with
        /// the rect on their own; only the absolutely-positioned children need this.
        /// </summary>
        public void SetContentScale(float scale)
        {
            var s = new Vector3(scale, scale, 1f);
            if (pips != null) pips.transform.localScale = s;
            if (spentWatermark != null) spentWatermark.transform.localScale = s;
        }

        public void PreviewFace(int face)
        {
            if (pips != null) pips.SetFace(face);
        }

        /// <summary>Roll-phase shake, degrees around z. Zero restores rest.</summary>
        public void SetWobble(float degrees)
        {
            if (body != null) body.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private void Paint(Color fill, Color border, Color pipColor)
        {
            if (background != null) background.color = fill;
            if (frame != null) frame.SetBorderColor(border);
            if (pips != null) pips.SetColor(pipColor);
        }

        private static Color Clear(Color color)
        {
            color.a = 0f;
            return color;
        }
    }
}
