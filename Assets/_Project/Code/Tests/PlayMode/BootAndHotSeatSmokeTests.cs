using System.Collections;
using Game.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Scene-level smoke tests for the E0 unblock stories: boot must reach the menu even when
    /// UGS is unavailable (STORY-0.2), and the generated Game scene must start a hot-seat match
    /// with a populated standings rail (STORY-0.1). These run against the committed scenes, so
    /// they also catch scenes drifting out of date with the scaffolder.
    /// </summary>
    public sealed class BootAndHotSeatSmokeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Boot_ReachesMainMenu_EvenIfOnlineInitFails()
        {
            // A UGS failure logs an error by design; that must not fail the test.
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(SceneNames.Boot);

            float deadline = Time.realtimeSinceStartup + 90f;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline,
                    "Boot never reached the MainMenu scene — the boot hang is back.");
                yield return null;
            }

            Assert.IsTrue(GameServices.IsReady, "Service graph was not built during boot.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator GameScene_LoadedWithoutNetwork_StartsHotSeatMatch()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(SceneNames.Game);
            yield return null;   // scene load completes, Start() callbacks run

            var host = Object.FindFirstObjectByType<HotSeatHost>();
            Assert.IsNotNull(host, "Game scene has no HotSeatHost — regenerate the scenes.");
            Assert.IsNotNull(host.Director,
                "Hot-seat match did not start. Is CardDatabase wired into the scene?");

            var rail = GameObject.Find("Rail");
            Assert.IsNotNull(rail, "Standings rail missing from the Game scene.");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (rail.transform.childCount < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.GreaterOrEqual(rail.transform.childCount, 2,
                "Standings rail has no player rows after match start.");
        }
    }
}
