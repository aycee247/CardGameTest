using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A square outline stroke that fills clockwise from top-center — the uGUI equivalent of the
    /// prototype's SVG stroke-dashoffset timer ring. Pure generated mesh, no sprites: the stroke
    /// hugs the inside of this RectTransform. Two stacked instances make the ring: a full-fill
    /// track underneath, the live progress on top.
    /// </summary>
    public sealed class SquareTimerRing : MaskableGraphic
    {
        [SerializeField] private float thickness = 8f;
        [Range(0f, 1f)] [SerializeField] private float fill = 1f;

        public float Fill01
        {
            get => fill;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, fill)) return;
                fill = clamped;
                SetVerticesDirty();
            }
        }

        public float Thickness
        {
            get => thickness;
            set { thickness = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fill <= 0f) return;

            var r = GetPixelAdjustedRect();
            float t = Mathf.Min(thickness, Mathf.Min(r.width, r.height) * 0.5f);
            float cx = r.center.x;

            // Clockwise from top-center. Verticals run between the corner blocks the horizontal
            // bands own, so nothing double-draws when the colour is translucent.
            float topLen = r.width * 0.5f;
            float sideLen = Mathf.Max(0f, r.height - 2f * t);
            float bottomLen = r.width;
            float perimeter = 2f * topLen + 2f * sideLen + bottomLen;

            float remaining = fill * perimeter;

            // 1. Top-right half: rightward from top-center.
            float len = Mathf.Min(remaining, topLen);
            if (len > 0f) Quad(vh, cx, r.yMax - t, cx + len, r.yMax);
            remaining -= topLen;

            // 2. Right side: downward.
            if (remaining > 0f)
            {
                len = Mathf.Min(remaining, sideLen);
                if (len > 0f) Quad(vh, r.xMax - t, r.yMax - t - len, r.xMax, r.yMax - t);
                remaining -= sideLen;
            }

            // 3. Bottom: leftward, corners included.
            if (remaining > 0f)
            {
                len = Mathf.Min(remaining, bottomLen);
                if (len > 0f) Quad(vh, r.xMax - len, r.yMin, r.xMax, r.yMin + t);
                remaining -= bottomLen;
            }

            // 4. Left side: upward.
            if (remaining > 0f)
            {
                len = Mathf.Min(remaining, sideLen);
                if (len > 0f) Quad(vh, r.xMin, r.yMin + t, r.xMin + t, r.yMin + t + len);
                remaining -= sideLen;
            }

            // 5. Top-left half: rightward toward top-center, corner included.
            if (remaining > 0f)
            {
                len = Mathf.Min(remaining, topLen);
                if (len > 0f) Quad(vh, r.xMin, r.yMax - t, r.xMin + len, r.yMax);
            }
        }

        private void Quad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax)
        {
            int start = vh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(xMin, yMin); vh.AddVert(vertex);
            vertex.position = new Vector3(xMin, yMax); vh.AddVert(vertex);
            vertex.position = new Vector3(xMax, yMax); vh.AddVert(vertex);
            vertex.position = new Vector3(xMax, yMin); vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
