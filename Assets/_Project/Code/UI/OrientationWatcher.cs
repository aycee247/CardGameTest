using System;
using UnityEngine;

namespace Game.UI
{
    public enum ScreenLayout { Portrait, Landscape }

    /// <summary>
    /// Raises an event when the screen switches between portrait and landscape, so responsive
    /// layouts can swap anchors / reflow without polling. Decides by aspect ratio, which also
    /// covers resizable windows in the Editor and on iPad multitasking.
    /// </summary>
    public sealed class OrientationWatcher : MonoBehaviour
    {
        public ScreenLayout Current { get; private set; }

        /// <summary>Fired whenever the layout changes; also fired once on enable with the initial value.</summary>
        public event Action<ScreenLayout> LayoutChanged;

        private void OnEnable()
        {
            Current = Evaluate();
            LayoutChanged?.Invoke(Current);
        }

        private void Update()
        {
            var next = Evaluate();
            if (next != Current)
            {
                Current = next;
                LayoutChanged?.Invoke(Current);
            }
        }

        private static ScreenLayout Evaluate() =>
            Screen.width >= Screen.height ? ScreenLayout.Landscape : ScreenLayout.Portrait;
    }
}
