using System.Collections;
using Game.App;
using Game.Persistence;
using Game.UI;
using TMPro;
using UnityEngine.UI;
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
            // Restored in the finally below rather than at the end of the happy path: production
            // marks the profile dirty when onboarding completes and the bootstrap flushes it on
            // quit, so a failed assertion here would otherwise write "already seen" to the
            // developer's real save — suppressing the very first run the next manual pass needs.
            int original = save.Profile.OnboardingSeenVersion;

            try
            {
                save.Profile.OnboardingSeenVersion = 0;

                SceneManager.LoadScene(SceneNames.MainMenu);
                yield return null;      // Start() runs

                var explainer = Object.FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
                Assert.IsNotNull(explainer, "MainMenu has no HowToPlayView — regenerate the scenes.");
                Assert.IsTrue(explainer.IsOpen, "A new player was never shown the explainer.");

                // Skipping is a real exit and must count as seen — the button is on every page but
                // the last, and someone who taps it should not be asked again next launch.
                explainer.Skip();
                Assert.IsFalse(explainer.IsOpen, "SKIP left the explainer open.");
                Assert.AreEqual(HowToPlayView.Version, save.Profile.OnboardingSeenVersion,
                    "Skipping must be recorded in the profile (AC2/AC3).");

                // ...and a returning player is left alone.
                SceneManager.LoadScene(SceneNames.MainMenu);
                yield return null;

                explainer = Object.FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
                Assert.IsFalse(explainer.IsOpen,
                    "The explainer reopened for a player who had seen it.");

                // Replay, then read to the end. DONE on the last page is the exit that keeps the
                // player on the menu; PLAY SOLO is the other one, and it loads a scene, so this
                // is the completion path a test can drive without leaving the menu behind.
                save.Profile.OnboardingSeenVersion = 0;
                explainer.Open();
                Assert.IsTrue(explainer.IsOpen, "The explainer would not reopen on request.");

                for (int i = 0; i < HowToPlayView.PageCount - 1; i++) explainer.Next();
                explainer.Next();   // the last page's DONE

                Assert.IsFalse(explainer.IsOpen, "DONE left the explainer open on the last page.");
                Assert.AreEqual(HowToPlayView.Version, save.Profile.OnboardingSeenVersion,
                    "Reaching the end must be recorded in the profile (AC3).");
            }
            finally
            {
                save.Profile.OnboardingSeenVersion = original;
            }
        }

        /// <summary>
        /// Every page of the explainer has to fit the screen it is drawn on. The scene generator
        /// cannot check this — the copy is set at runtime, so at generation time the label is
        /// empty — and the first build of it shipped with the text rendering as one long line off
        /// both edges of the phone, because nothing set a wrapping mode.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator HowToPlay_EveryPageFitsItsPanel()
        {
            LogAssert.ignoreFailingMessages = true;

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

            SceneManager.LoadScene(SceneNames.MainMenu);
            yield return null;

            var explainer = Object.FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
            Assert.IsNotNull(explainer, "MainMenu has no HowToPlayView — regenerate the scenes.");

            explainer.Open();
            yield return null;

            var body = explainer.transform.Find("Body")?.GetComponent<TMP_Text>();
            var title = explainer.transform.Find("Title")?.GetComponent<TMP_Text>();
            Assert.IsNotNull(body, "The explainer has no Body label.");
            Assert.IsNotNull(title, "The explainer has no Title label.");

            for (int page = 1; page <= HowToPlayView.PageCount; page++)
            {
                yield return null;   // let the layout settle on the page just shown

                AssertFits(title, $"page {page} title");
                AssertFits(body, $"page {page} body");

                if (page < HowToPlayView.PageCount) explainer.Next();
            }

            explainer.Close();
        }

        /// <summary>
        /// Asserts a label's laid-out text stays inside the box it was given. Rendered bounds, not
        /// preferred size: preferred width ignores wrapping and would fail on text that wraps
        /// perfectly well.
        /// </summary>
        private static void AssertFits(TMP_Text label, string what)
        {
            label.ForceMeshUpdate();

            var rect = ((RectTransform)label.transform).rect;
            var bounds = label.textBounds.size;

            Assert.LessOrEqual(bounds.x, rect.width + 1f,
                $"{what}: text is {bounds.x:0} wide in a {rect.width:0} box — it will run off the screen.");
            Assert.LessOrEqual(bounds.y, rect.height + 1f,
                $"{what}: text is {bounds.y:0} tall in a {rect.height:0} box — it will be clipped.");
        }

        /// <summary>
        /// STORY-4.1/4.2: the settings panel writes to the profile and reads back from it. Every
        /// value in GameSettings has worked since the plumbing commit and none of it was
        /// reachable, so this covers the wiring rather than the settings themselves.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Settings_WriteToTheProfileAndRenderBackFromIt()
        {
            LogAssert.ignoreFailingMessages = true;

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
                "No save service — settings have nowhere to persist to.");

            var settings = save.Profile.Settings;
            float originalMaster = settings.MasterVolume;
            bool originalHaptics = settings.Haptics;

            try
            {
                SceneManager.LoadScene(SceneNames.MainMenu);
                yield return null;

                var controller = Object.FindFirstObjectByType<SettingsController>(FindObjectsInactive.Include);
                var view = Object.FindFirstObjectByType<SettingsView>(FindObjectsInactive.Include);
                Assert.IsNotNull(controller, "MainMenu has no SettingsController — regenerate the scenes.");
                Assert.IsNotNull(view, "MainMenu has no SettingsView — regenerate the scenes.");

                controller.Open();
                Assert.IsTrue(view.IsOpen, "Settings would not open from the menu (AC2).");

                // Drive the controls the way a player does, through the view's own events.
                var master = view.transform.Find("MasterSlider")?.GetComponent<Slider>();
                Assert.IsNotNull(master, "The master volume slider is missing from the panel.");

                master.value = 0.25f;
                Assert.AreEqual(0.25f, save.Profile.Settings.MasterVolume, 0.001f,
                    "Moving a slider must write GameSettings (#22 AC1).");

                var haptics = view.transform.Find("HapticsToggle")?.GetComponent<Button>();
                Assert.IsNotNull(haptics, "The haptics toggle is missing from the panel.");

                bool before = save.Profile.Settings.Haptics;
                haptics.onClick.Invoke();
                Assert.AreNotEqual(before, save.Profile.Settings.Haptics,
                    "The haptics toggle did not reach the profile.");

                // Reopening renders what was stored rather than the authored defaults.
                view.Close();
                controller.Open();
                Assert.AreEqual(0.25f, master.value, 0.001f,
                    "Settings reopened on the authored default instead of the stored value.");
            }
            finally
            {
                // Production marks the profile dirty and the bootstrap flushes on quit, so a
                // failed assertion above must not leave test values in the developer's real save.
                save.Profile.Settings.MasterVolume = originalMaster;
                save.Profile.Settings.Haptics = originalHaptics;
            }
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
