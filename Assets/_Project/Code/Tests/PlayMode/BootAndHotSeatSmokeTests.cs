using System.Collections;
using Game.App;
using Game.Persistence;
using Game.UI;
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

        /// <summary>
        /// STORY-3.5: the explainer opens once for a new player, and not again once they have
        /// been through it. Driven through the profile rather than the file on disk — the
        /// editor's persistentDataPath survives between runs, so a test that relied on a genuine
        /// first launch would pass once and then never again.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator MainMenu_ShowsTheExplainerToANewPlayerOnlyOnce()
        {
            LogAssert.ignoreFailingMessages = true;

            // Boot only if nothing has booted yet. GameBootstrap destroys itself on re-entry
            // once the singleton graph exists, so a second trip through Boot never reaches the
            // menu — which made this test pass alone and hang when it ran after another.
            if (!GameServices.IsReady)
            {
                SceneManager.LoadScene(SceneNames.Boot);

                float deadline = Time.realtimeSinceStartup + 90f;
                while (SceneManager.GetActiveScene().name != SceneNames.MainMenu)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, "Boot never reached the MainMenu.");
                    yield return null;
                }
            }

            Assert.IsTrue(GameServices.Locator.TryGet<ISaveService>(out var save),
                "No save service — the profile is what records that onboarding was seen.");

            // Whoever ran the suite before us may already have a profile; make this a new player.
            int original = save.Profile.OnboardingSeenVersion;
            save.Profile.OnboardingSeenVersion = 0;

            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;      // Start() runs

            var explainer = Object.FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
            Assert.IsNotNull(explainer, "MainMenu has no HowToPlayView — regenerate the scenes.");
            Assert.IsTrue(explainer.IsOpen, "A new player was never shown the explainer.");

            // Page through it. The last page's PLAY SOLO also raises Finished, so walking to the
            // end with Next is the path that leaves the player on the menu.
            for (int i = 0; i < HowToPlayView.PageCount; i++) explainer.Next();

            Assert.IsFalse(explainer.IsOpen, "The explainer never closed at the end.");
            Assert.AreEqual(HowToPlayView.Version, save.Profile.OnboardingSeenVersion,
                "Reaching the end must be recorded in the profile (AC3).");

            // ...and a returning player is left alone.
            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;

            explainer = Object.FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
            Assert.IsFalse(explainer.IsOpen, "The explainer reopened for a player who had seen it.");

            save.Profile.OnboardingSeenVersion = original;
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
