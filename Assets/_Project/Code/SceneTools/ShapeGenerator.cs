using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.SceneTools
{
    /// <summary>
    /// Writes the two sprites the chunky direction needs: a rounded rectangle to fill, and a
    /// rounded rectangle outline to draw around it.
    ///
    /// uGUI cannot round a corner without a sprite — an <see cref="UnityEngine.UI.Image"/> with no
    /// sprite is always a hard rectangle — so the shape has to exist as an asset. Both are
    /// generated rather than drawn by hand so the radius and the outline weight stay values in
    /// this file rather than facts baked into a PNG nobody can re-derive.
    ///
    /// Nine-sliced: the corners keep their radius at any control size, and the straight edges
    /// stretch. That is what lets one 96px sprite serve a 970-unit button and a 92-unit gear.
    /// </summary>
    public static class ShapeGenerator
    {
        private const string Dir = "Assets/_Project/Art/UI";
        public const string FillPath = Dir + "/RoundedFill.png";
        public const string OutlinePath = Dir + "/RoundedOutline.png";

        /// <summary>
        /// Authored size. Large enough that the corner curve is smooth when a small control
        /// squeezes it, small enough to stay a trivial asset.
        /// </summary>
        private const int Size = 96;

        /// <summary>Corner radius in sprite pixels; the 9-slice border is set to match.</summary>
        private const int Radius = 28;

        /// <summary>Outline weight in sprite pixels, at the authored size.</summary>
        private const int Outline = 10;

        [MenuItem("Foundry/Generate UI Shapes")]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir);

            Write(FillPath, filled: true);
            Write(OutlinePath, filled: false);

            AssetDatabase.Refresh();
            Debug.Log($"[Shapes] Wrote {FillPath} and {OutlinePath} — radius {Radius}, outline {Outline}.");
        }

        private static void Write(string path, bool filled)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float outer = RoundedCoverage(x, y, 0f);

                    // The outline is the rounded rect minus a smaller one inset by its weight,
                    // so both edges of the ring stay concentric and equally smooth.
                    float alpha = filled ? outer : outer - RoundedCoverage(x, y, Outline);

                    pixels[y * Size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;

            // The border must exceed the radius, or a stretched edge would eat into the curve.
            float border = Radius + 2;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// How much of a pixel the rounded rectangle covers, inset from the edge by
        /// <paramref name="inset"/>. Sampled on a 2×2 grid inside the pixel rather than tested at
        /// its centre, which is what keeps the curve from stepping visibly at small sizes.
        /// </summary>
        private static float RoundedCoverage(int x, int y, float inset)
        {
            float radius = Mathf.Max(0f, Radius - inset);
            float min = inset;
            float max = Size - inset;

            float covered = 0f;
            for (int sy = 0; sy < 2; sy++)
            {
                for (int sx = 0; sx < 2; sx++)
                {
                    float px = x + 0.25f + sx * 0.5f;
                    float py = y + 0.25f + sy * 0.5f;

                    if (px < min || px > max || py < min || py > max) continue;

                    // Distance past the corner circle's centre, on each axis independently.
                    float dx = Mathf.Max(0f, Mathf.Max(min + radius - px, px - (max - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(min + radius - py, py - (max - radius)));

                    if (dx * dx + dy * dy <= radius * radius) covered += 0.25f;
                }
            }

            return covered;
        }
    }
}
