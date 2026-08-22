using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The nine pip positions of a die face, row-major from top-left. Drawn, not bitmapped —
    /// square pips fit the blueprint aesthetic and the colour-blind rule: a face reads by pip
    /// layout, never by colour (STORY-4.5 AC3).
    /// </summary>
    public sealed class DiePipGrid : MonoBehaviour
    {
        [SerializeField] private Image[] pips = new Image[9];

        private static readonly int[][] Patterns =
        {
            new[] { 4 },                  // 1
            new[] { 0, 8 },               // 2
            new[] { 0, 4, 8 },            // 3
            new[] { 0, 2, 6, 8 },         // 4
            new[] { 0, 2, 4, 6, 8 },      // 5
            new[] { 0, 3, 6, 2, 5, 8 },   // 6 — two columns
        };

        /// <summary>Editor-time wiring; serializes with the generated scene.</summary>
        public void Bind(Image[] pipImages) => pips = pipImages;

        public void SetFace(int face)
        {
            if (pips == null) return;

            var pattern = Patterns[Mathf.Clamp(face, 1, 6) - 1];
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                bool lit = System.Array.IndexOf(pattern, i) >= 0;
                pips[i].gameObject.SetActive(lit);
            }
        }

        public void SetColor(Color color)
        {
            if (pips == null) return;
            foreach (var pip in pips)
                if (pip != null) pip.color = color;
        }
    }
}
