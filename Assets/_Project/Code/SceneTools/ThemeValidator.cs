using System.Collections.Generic;
using System.Reflection;
using Game.Data;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.SceneTools
{
    /// <summary>
    /// Flags any theme token still at its default — an unassigned colour, a null font, an empty
    /// ramp slot (STORY-5.1 AC3). Reflection over the public fields, so a token added to
    /// <see cref="ThemeAsset"/> later is covered without anyone remembering to extend this.
    /// Scene generation refuses to run against an incomplete theme.
    /// </summary>
    public static class ThemeValidator
    {
        [MenuItem("Foundry/Validate Theme")]
        public static void ValidateMenu()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemeGenerator.ThemePath);
            if (theme == null)
            {
                Debug.LogError($"[Foundry] No theme at {ThemeGenerator.ThemePath} — run Foundry ▸ Generate Theme.");
                return;
            }

            var issues = Collect(theme);
            if (issues.Count > 0)
                Debug.LogError($"[Foundry] Theme has {issues.Count} unassigned token(s):\n  " +
                               string.Join("\n  ", issues));
            else
                Debug.Log("[Foundry] Theme valid — every token assigned.");
        }

        /// <summary>Throws when a token is unassigned, so a bad theme fails generation loudly.</summary>
        public static void ValidateOrThrow(ThemeAsset theme)
        {
            var issues = Collect(theme);
            if (issues.Count > 0)
                throw new System.InvalidOperationException(
                    "Theme has unassigned tokens: " + string.Join(", ", issues));
        }

        public static List<string> Collect(ThemeAsset theme)
        {
            var issues = new List<string>();
            if (theme == null)
            {
                issues.Add("theme asset is null");
                return issues;
            }

            foreach (var field in typeof(ThemeAsset).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(theme);

                if (field.FieldType == typeof(Color))
                {
                    if ((Color)value == default) issues.Add(field.Name);
                }
                else if (field.FieldType == typeof(Color[]))
                {
                    var colors = (Color[])value;
                    if (colors == null || colors.Length == 0) { issues.Add(field.Name); continue; }
                    for (int i = 0; i < colors.Length; i++)
                        if (colors[i] == default) issues.Add($"{field.Name}[{i}]");
                }
                else if (field.FieldType == typeof(TMP_FontAsset))
                {
                    if ((TMP_FontAsset)value == null) issues.Add(field.Name);
                }
            }

            return issues;
        }
    }
}
