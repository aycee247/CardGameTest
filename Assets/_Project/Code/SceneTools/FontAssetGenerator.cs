using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.SceneTools
{
    /// <summary>
    /// Bakes the Barlow family TTFs into static TMP font assets. Static, not dynamic: the glyph
    /// set is fixed at generation time so a device never pays atlas-packing cost mid-match, and a
    /// missing glyph is a loud warning here instead of a silent tofu there. LiberationSans stays
    /// chained as the fallback for anything the family lacks.
    ///
    /// Order matters across the Foundry menu: fonts → theme → scenes, because each later step
    /// binds assets the earlier one wrote.
    /// </summary>
    public static class FontAssetGenerator
    {
        private const string SourceDir = "Assets/_Project/Art/Fonts";
        private const string OutDir = "Assets/_Project/Art/Fonts/Generated";
        private const string FallbackPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // ASCII plus every non-ASCII glyph the handoff copy uses. Missing glyphs are reported.
        private const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz{|}~…·–—≥≈±×✓○✕←’‘“”°";

        private static readonly (string source, string output)[] Faces =
        {
            ("Barlow-Regular.ttf", "Barlow-Regular SDF"),
            ("Barlow-Medium.ttf", "Barlow-Medium SDF"),
            ("Barlow-SemiBold.ttf", "Barlow-SemiBold SDF"),
            ("BarlowCondensed-SemiBold.ttf", "BarlowCondensed-SemiBold SDF"),
            ("BarlowCondensed-Bold.ttf", "BarlowCondensed-Bold SDF"),
        };

        [MenuItem("Foundry/Generate Font Assets")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(OutDir))
                AssetDatabase.CreateFolder(SourceDir, "Generated");

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            if (fallback == null)
                Debug.LogWarning($"[Foundry] No fallback font at {FallbackPath} — tofu risk for exotic glyphs.");

            int built = 0;
            foreach (var (source, output) in Faces)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{SourceDir}/{source}");
                if (font == null)
                {
                    Debug.LogError($"[Foundry] Missing source font {SourceDir}/{source} — skipped.");
                    continue;
                }

                string path = $"{OutDir}/{output}.asset";

                // Downstream binders (theme, scenes) re-link by path right after this runs, so a
                // fresh GUID from delete-and-recreate is fine and far simpler than in-place surgery.
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
                    AssetDatabase.DeleteAsset(path);

                var asset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, false);

                asset.name = output;
                asset.TryAddCharacters(Charset, out string missing);
                if (!string.IsNullOrEmpty(missing))
                    Debug.LogWarning($"[Foundry] {output} lacks '{missing}' — those glyphs fall back to LiberationSans.");

                asset.atlasPopulationMode = AtlasPopulationMode.Static;

                if (fallback != null)
                {
                    asset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                    asset.fallbackFontAssetTable.Add(fallback);
                }

                AssetDatabase.CreateAsset(asset, path);

                if (asset.material != null)
                {
                    asset.material.name = output + " Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }

                if (asset.atlasTextures != null)
                {
                    foreach (var texture in asset.atlasTextures)
                    {
                        if (texture == null) continue;
                        texture.name = output + " Atlas";
                        AssetDatabase.AddObjectToAsset(texture, asset);
                    }
                }

                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Foundry] Generated {built}/{Faces.Length} font assets in {OutDir}.");
        }
    }
}
