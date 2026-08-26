// Compiled only when the active build target is iOS: the UnityEditor.iOS.Xcode types below ship
// with the iOS Build Support module and do not exist otherwise.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Post-build pass over the exported Xcode project (STORY-6.7):
    ///
    /// - Answers App Store export compliance permanently in the Info.plist: the app uses only
    ///   standard HTTPS/TLS, which is exempt, so TestFlight builds never stall on the
    ///   questionnaire (AC4). Answered here, once, instead of per-upload in the browser.
    /// - Verifies Unity emitted the engine privacy manifest rather than assuming it did
    ///   (STORY-6.2 AC7 said "verify rather than assume" — this is that verification).
    /// </summary>
    public sealed class IosPostBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            string projectPath = report.summary.outputPath;

            string plistPath = Path.Combine(projectPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);
            Debug.Log("[Foundry] Info.plist: ITSAppUsesNonExemptEncryption = NO (standard HTTPS only — exempt).");

            var manifests = Directory.GetFiles(projectPath, "PrivacyInfo.xcprivacy", SearchOption.AllDirectories);
            if (manifests.Length > 0)
                Debug.Log("[Foundry] Privacy manifest present in the exported project: " +
                          string.Join(", ", manifests));
            else
                Debug.LogWarning("[Foundry] No PrivacyInfo.xcprivacy in the exported Xcode project — " +
                                 "check Apple's privacy-manifest requirements before submitting (STORY-6.2 AC7).");
        }
    }
}
#endif
