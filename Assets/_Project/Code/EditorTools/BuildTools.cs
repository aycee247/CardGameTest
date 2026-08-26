using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the iOS Xcode project headlessly (STORY-6.7). The scene list is whatever
    /// <c>Foundry ▸ Generate Scenes &amp; Build Settings</c> last wrote — this deliberately has no
    /// scene list of its own, so a build can never ship scenes the generator didn't.
    ///
    /// Editor menu for a human; <c>-batchmode -buildTarget iOS -executeMethod
    /// Game.EditorTools.BuildTools.BuildIos</c> for tools/build-ios.sh.
    /// </summary>
    public static class BuildTools
    {
        public const string IosOutputPath = "Builds/iOS";

        [MenuItem("Foundry/Build iOS (Xcode Project)")]
        public static void BuildIos()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Foundry] Build Settings has no scenes — run Foundry ▸ Generate Scenes & Build Settings first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.iOS,
                locationPathName = IosOutputPath
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Foundry] iOS Xcode project written to {IosOutputPath} " +
                          $"({summary.totalSize / (1024 * 1024)} MB in {summary.totalTime:mm\\:ss}).");
            }
            else
            {
                Debug.LogError($"[Foundry] iOS build {summary.result}: {summary.totalErrors} error(s) — see the log above.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
