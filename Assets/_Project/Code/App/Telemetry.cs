using System.Collections.Generic;
using UnityEngine;
#if FOUNDRY_UGS_ANALYTICS
using Unity.Services.Analytics;
#endif

namespace Game.App
{
    /// <summary>
    /// App-side telemetry sink (docs/product-plan.md, F1). Lives here in the composition root and
    /// nowhere lower: Game.Core is pure C# and must never see it, and views report through their
    /// presenters, not directly.
    ///
    /// Every call is fire-and-forget and safe before collection starts — a match played offline or
    /// before UGS initialises simply records nothing.
    /// </summary>
    public interface ITelemetry
    {
        /// <summary>Records one event. Parameter values may be string, bool, int, long, float or double.</summary>
        void Record(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }

    /// <summary>
    /// UGS Analytics implementation.
    ///
    /// Compiled against the SDK only when the <c>com.unity.services.analytics</c> package is
    /// resolved (the FOUNDRY_UGS_ANALYTICS version define on Game.App.asmdef) — without it, and in
    /// tools/verify-unity-compile.sh which predates package resolution, events fall through to a
    /// Debug.Log so an Editor run still shows what would have been sent.
    ///
    /// NOTE (version-sensitive, same caveat as SessionManager): StartDataCollection /
    /// CustomEvent.Add / RecordEvent are the 5.x surface. Confirm against the installed package
    /// version after it resolves in-editor. Custom events must also be defined in the UGS
    /// dashboard's Event Manager or the service silently drops them — the list lives in
    /// docs/product-plan.md.
    /// </summary>
    public sealed class UgsTelemetry : ITelemetry
    {
        private bool _collecting;

        /// <summary>
        /// Begins collection. Called by <see cref="GameBootstrap"/> once UGS initialisation has
        /// succeeded — the SDK throws if used before UnityServices is ready, so this is the same
        /// gate the session manager already passes through.
        /// </summary>
        public void StartCollection()
        {
            if (_collecting) return;

#if FOUNDRY_UGS_ANALYTICS
            AnalyticsService.Instance.StartDataCollection();
#endif
            _collecting = true;
        }

        public void Record(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!_collecting)
            {
                return;
            }

#if FOUNDRY_UGS_ANALYTICS
            var e = new CustomEvent(eventName);
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    switch (pair.Value)
                    {
                        case string s: e.Add(pair.Key, s); break;
                        case bool b: e.Add(pair.Key, b); break;
                        case int i: e.Add(pair.Key, i); break;
                        case long l: e.Add(pair.Key, l); break;
                        case float f: e.Add(pair.Key, f); break;
                        case double d: e.Add(pair.Key, d); break;
                        default:
                            e.Add(pair.Key, pair.Value?.ToString() ?? string.Empty);
                            break;
                    }
                }
            }
            AnalyticsService.Instance.RecordEvent(e);
#else
            // Package not resolved (fresh checkout, or the verify script): show the event rather
            // than lose it, so an Editor session still demonstrates the funnel.
            Debug.Log($"[Telemetry] {eventName} {Describe(parameters)}");
#endif
        }

#if !FOUNDRY_UGS_ANALYTICS
        private static string Describe(IReadOnlyDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return string.Empty;
            var parts = new List<string>(parameters.Count);
            foreach (var pair in parameters) parts.Add($"{pair.Key}={pair.Value}");
            return "{" + string.Join(", ", parts) + "}";
        }
#endif
    }
}
