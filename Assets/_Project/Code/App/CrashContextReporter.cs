using UnityEngine;
using UnityEngine.CrashReportHandler;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Attaches non-personal context to crash and exception reports (STORY-6.6). Lives on the
    /// persistent bootstrap object so the metadata survives scene loads and is present whenever
    /// a report is sent.
    ///
    /// Privacy contract (STORY-6.6 AC2): metadata here is scene names and counters ONLY. Never
    /// the display name, never the UGS auth id, never a session join code — nothing typed by or
    /// identifying a player may be added to a report.
    /// </summary>
    public sealed class CrashContextReporter : MonoBehaviour
    {
        private int _unhandledExceptions;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceived += OnLogMessage;
            CrashReportHandler.SetUserMetadata("scene", SceneManager.GetActiveScene().name);
            CrashReportHandler.SetUserMetadata("version", Application.version);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CrashReportHandler.SetUserMetadata("scene", scene.name);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            // The report pipeline already carries the exception itself; the counter tells a
            // reader whether the crash was the first fault or the end of a cascade.
            if (type != LogType.Exception) return;
            _unhandledExceptions++;
            CrashReportHandler.SetUserMetadata("unhandledExceptions", _unhandledExceptions.ToString());
        }
    }
}
