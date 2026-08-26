using System.IO;
using Game.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.SceneTools
{
    /// <summary>
    /// Renders the placeholder app icon from code — a blueprint-styled die face on the
    /// accent-900 ground — writes it to <see cref="IconPath"/> and assigns it as the project's
    /// default icon, which Unity scales for every platform (including all iOS sizes) at build
    /// time. Colors come from the generated <see cref="ThemeAsset"/> so the icon cannot drift
    /// from the palette. Placeholder until E5 settles an art direction (STORY-5.6); rerunning
    /// the menu item regenerates it in place.
    /// </summary>
    public static class AppIconGenerator
    {
        public const string IconPath = "Assets/_Project/Art/Icon/AppIcon.png";
        private const int Size = 1024;

        [MenuItem("Foundry/Generate App Icon")]
        public static void Generate()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemeGenerator.ThemePath);
            if (theme == null)
            {
                Debug.LogError("[Foundry] Theme asset not found — run Foundry ▸ Generate Theme first.");
                return;
            }

            var pixels = Render(theme);

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(IconPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Foundry] App icon written to {IconPath} and set as the default icon.");
        }

        /// <summary>A five-pip die face centred on the accent-900 ground, with the blueprint
        /// "+" corner marks. App icons must be fully opaque — iOS rejects alpha.</summary>
        private static Color32[] Render(ThemeAsset theme)
        {
            Color32 ground = theme.accentRamp[8];
            Color32 face = theme.surfaceBase;
            Color32 pip = theme.accentRamp[8];
            Color32 mark = theme.accentRamp[3];

            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = ground;

            const int center = Size / 2;
            FillRoundedSquare(pixels, center, center, half: 310, radius: 88, face);

            const int pipOffset = 165;
            const int pipRadius = 62;
            FillCircle(pixels, center, center, pipRadius, pip);
            FillCircle(pixels, center - pipOffset, center - pipOffset, pipRadius, pip);
            FillCircle(pixels, center + pipOffset, center - pipOffset, pipRadius, pip);
            FillCircle(pixels, center - pipOffset, center + pipOffset, pipRadius, pip);
            FillCircle(pixels, center + pipOffset, center + pipOffset, pipRadius, pip);

            const int inset = 132;
            FillPlusMark(pixels, inset, inset, mark);
            FillPlusMark(pixels, Size - inset, inset, mark);
            FillPlusMark(pixels, inset, Size - inset, mark);
            FillPlusMark(pixels, Size - inset, Size - inset, mark);

            return pixels;
        }

        private static void FillRoundedSquare(Color32[] pixels, int cx, int cy, int half, int radius, Color32 color)
        {
            int core = half - radius;
            for (int y = cy - half; y <= cy + half; y++)
            for (int x = cx - half; x <= cx + half; x++)
            {
                int dx = System.Math.Max(System.Math.Abs(x - cx) - core, 0);
                int dy = System.Math.Max(System.Math.Abs(y - cy) - core, 0);
                if (dx * dx + dy * dy <= radius * radius)
                    pixels[y * Size + x] = color;
            }
        }

        private static void FillCircle(Color32[] pixels, int cx, int cy, int radius, Color32 color)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                    pixels[y * Size + x] = color;
        }

        private static void FillPlusMark(Color32[] pixels, int cx, int cy, Color32 color)
        {
            const int arm = 38;
            const int thickness = 6;
            for (int y = cy - arm; y <= cy + arm; y++)
            for (int x = cx - arm; x <= cx + arm; x++)
                if (System.Math.Abs(x - cx) <= thickness || System.Math.Abs(y - cy) <= thickness)
                    pixels[y * Size + x] = color;
        }
    }
}
