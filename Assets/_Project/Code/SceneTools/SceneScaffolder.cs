using System.Collections.Generic;
using Game.App;
using Game.Audio;
using Game.Core;
using Game.Data;
using Game.Networking;
using Game.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.SceneTools
{
    /// <summary>
    /// One-click generator for the Boot → MainMenu → Lobby → Game scenes, fully wired to the
    /// scaffold's components, and registered in Build Settings in order. Re-runnable: it overwrites
    /// the four scenes. Restyle the generated UI in the editor afterwards.
    ///
    /// Menu: <b>Foundry ▸ Generate Scenes &amp; Build Settings</b>.
    /// Run <b>Foundry ▸ Generate Starter Deck</b> first so the Game scene can bind a CardDatabase.
    /// </summary>
    public static class SceneScaffolder
    {
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string DatabasePath = "Assets/_Project/ScriptableObjects/CardDatabase.asset";
        private static readonly Vector2 RefRes = new Vector2(1080, 1920);

        /// <summary>The active theme, loaded and validated at the top of every generation run.</summary>
        private static ThemeAsset _theme;

        [MenuItem("Foundry/Generate Scenes & Build Settings")]
        public static void Generate() => GenerateWith(ThemeGenerator.ThemePath);

        /// <summary>Same scenes, night shift (STORY-5.1 AC4). Generation-time theming per the
        /// E5 scope: regenerating is how the theme switches.</summary>
        [MenuItem("Foundry/Generate Scenes (Blueprint Dark)")]
        public static void GenerateDark() => GenerateWith(ThemeGenerator.DarkThemePath);

        private static void GenerateWith(string themePath)
        {
            // EditorSceneManager.NewScene throws in Play mode. Without this guard the prefabs are
            // rebuilt, the scenes are not, and the only clue is an InvalidOperationException — so
            // the scene silently stays on the previous version.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Generate Scenes",
                    "Stop Play mode first — Unity cannot create scenes while the game is running.\n\n" +
                    "Press Stop, run this again, then press Play.", "OK");
                return;
            }

            // In batchmode the dialog can't be answered and cancels the run, so skip it there —
            // a headless invocation (-executeMethod) has already opted in.
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Generate Scenes",
                "This creates/overwrites Boot, MainMenu, Lobby and Game scenes under " + SceneDir +
                " and sets the Build Settings scene list. Continue?", "Generate", "Cancel"))
                return;

            // Every colour and typeface below comes from the theme; refuse to build without one.
            _theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(themePath);
            if (_theme == null)
            {
                Debug.LogError($"[Scaffold] No theme at {themePath} — run " +
                               "Foundry ▸ Generate Font Assets, then Foundry ▸ Generate Theme, then this.");
                return;
            }
            ThemeValidator.ValidateOrThrow(_theme);
            UiFactory.Theme = _theme;

            EnsureFolder("Assets/_Project", "Scenes");

            var paths = new List<string>
            {
                BuildBootScene(),
                BuildMainMenuScene(),
                BuildLobbyScene(),
                BuildGameScene(),
            };

            var buildScenes = new List<EditorBuildSettingsScene>();
            foreach (var p in paths) buildScenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Scaffold] Generated 4 scenes and set Build Settings: Boot → MainMenu → Lobby → Game.");
        }

        // ---------------- Scenes ----------------

        private static string BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();

            // Networking singleton (persists across scenes via Bootstrap's DontDestroyOnLoad).
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            EditorUtility.SetDirty(nm);

            // Composition root.
            var bootGo = new GameObject("Bootstrap");
            var audio = bootGo.AddComponent<AudioManager>();
            var boot = bootGo.AddComponent<GameBootstrap>();
            SetRef(boot, "audioManager", audio);
            SetRef(boot, "networkManager", nm);

            // The boot beat is the wordmark alone on the theme surface — the same identity block
            // as the MainMenu hero (STORY-6.8), so launch screen → boot → menu reads as one
            // continuous surface rather than three products.
            CreateCanvas(out var content);
            UiFactory.Label(content, "Eyebrow", "SIMULTANEOUS DICE ENGINE BUILDER",
                new Vector2(0, 330), new Vector2(950, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            UiFactory.Label(content, "Wordmark", "FOUNDRY", new Vector2(0, 160), new Vector2(940, 230), 190f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            UiFactory.Label(content, "Loading", "STOKING THE FURNACE…", new Vector2(0, -120), new Vector2(900, 60), 38f,
                TextAlignmentOptions.Center, FontRole.BodyMedium, Muted(0.55f), 0.08f);

            return Save(scene, SceneNames.Boot);
        }

        private static string BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            // ---- Hero: the blueprint-framed identity block (handoff §1) ----
            // Top-anchored, now that everything under it hangs off the bottom edge. Two fixed
            // edges beat one floating centre: the vertical gap between hero and actions absorbs
            // the difference between a 16:9 phone and a tall one, instead of the action stack
            // drifting into the status line.
            var hero = UiFactory.Panel(content, "Hero", stretch: false);
            hero.sizeDelta = new Vector2(970, 780);
            Top(hero, -440);
            UiFactory.BlueprintFrame(hero, FrameEmphasis.Accent);

            UiFactory.Label(hero, "Eyebrow", "SIMULTANEOUS DICE ENGINE BUILDER",
                new Vector2(0, 300), new Vector2(950, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            UiFactory.Label(hero, "Wordmark", "FOUNDRY", new Vector2(0, 130), new Vector2(940, 230), 190f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            MiniDieGlyph(hero, "GlyphFive", 5, new Vector2(-390, -70));
            MiniDieGlyph(hero, "GlyphSix", 6, new Vector2(-300, -70));
            UiFactory.Label(hero, "Meta", "2–6 PLAYERS · 10 ROUNDS\n≈ 12 MINUTES",
                new Vector2(250, -80), new Vector2(520, 100), 38f,
                TextAlignmentOptions.Right, FontRole.BodyMedium, Muted(0.55f), 0.08f);
            UiFactory.Label(hero, "BodyCopy",
                "Roll together, shape your dice, commit in secret,\nthen claim the market's machines.",
                new Vector2(0, -270), new Vector2(880, 100), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.7f));

            // ---- Everything below the hero anchors to the bottom edge ----
            // It used to be a mix: the action stack sat at fixed offsets from the centre while
            // the status line and footer hung off the bottom, so the gap between them was a
            // function of screen height. On a tall phone it closed to nothing and the status line
            // landed across the offline row (with raycastTarget on, it ate that row's taps).
            // Anchoring the whole stack to one edge makes the spacing below a constant.
            const float FooterY = 60f;
            const float CodeLabelY = 135f;
            const float StatusY = 210f;
            const float OfflineRowY = 350f;
            const float JoinY = 520f;
            const float CodeInputY = 680f;
            const float HostY = 850f;
            const float IdentityY = 1010f;

            // ---- Identity: who the other seats will see (STORY-4.3) ----
            var nameInput = Bottom(UiFactory.InputField(content, "NameInput", "Your name (optional)",
                Vector2.zero, new Vector2(800, 120)), IdentityY);
            ((RectTransform)nameInput.transform).anchoredPosition += new Vector2(-85f, 0f);

            // Settings shares the identity row: both are things you set up before playing, and
            // the offline row below is already three buttons wide (STORY-4.1 AC2).
            // "SET", not a gear glyph: U+2699 is in none of the Barlow faces and not in the
            // LiberationSans fallback either, so TMP substituted a box — the button read as tofu
            // on device. An icon here needs a glyph added to the atlases first.
            var settingsButton = Bottom(UiFactory.Button(content, "SettingsButton", "SET",
                Vector2.zero, new Vector2(140, 120), ButtonStyle.Secondary, fontSize: 38f), IdentityY);
            ((RectTransform)settingsButton.transform).anchoredPosition += new Vector2(400f, 0f);
            UiFactory.BlueprintFrame((RectTransform)settingsButton.transform, marks: false);
            nameInput.characterLimit = PlayerName.MaxLength;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;

            // ---- Actions: one solid-accent primary, everything else recedes ----
            var host = Bottom(UiFactory.Button(content, "HostButton", "HOST MATCH",
                Vector2.zero, new Vector2(970, 144)), HostY);
            UiFactory.BlueprintFrame((RectTransform)host.transform, FrameEmphasis.AccentStrong);

            var codeInput = Bottom(UiFactory.InputField(content, "JoinCodeInput", "Enter join code",
                Vector2.zero, new Vector2(970, 120)), CodeInputY);
            var join = Bottom(UiFactory.Button(content, "JoinButton", "JOIN WITH CODE",
                Vector2.zero, new Vector2(970, 144), ButtonStyle.Secondary), JoinY);

            // Kept beyond the handoff: the offline paths — pass-the-device, and solo vs bots
            // (STORY-7.1) — sharing one row so the online actions keep their prominence. HOW TO
            // PLAY (STORY-3.5) joins them: it belongs with the things you do without a friend
            // waiting, and a new player looking for it looks here.
            var passPlay = OffsetX(Bottom(UiFactory.Button(content, "PassPlayButton", "PASS &\nPLAY",
                Vector2.zero, new Vector2(310, 144), ButtonStyle.Ghost, fontSize: 38f), OfflineRowY), -322f);
            UiFactory.BlueprintFrame((RectTransform)passPlay.transform, marks: false);
            var solo = Bottom(UiFactory.Button(content, "SoloButton", "SOLO VS\nBOTS",
                Vector2.zero, new Vector2(310, 144), ButtonStyle.Ghost, fontSize: 38f), OfflineRowY);
            UiFactory.BlueprintFrame((RectTransform)solo.transform, marks: false);
            var howTo = OffsetX(Bottom(UiFactory.Button(content, "HowToPlayButton", "HOW TO\nPLAY",
                Vector2.zero, new Vector2(310, 144), ButtonStyle.Ghost, fontSize: 38f), OfflineRowY), 322f);
            UiFactory.BlueprintFrame((RectTransform)howTo.transform, marks: false);

            var status = Bottom(UiFactory.Label(content, "Status", "", Vector2.zero,
                new Vector2(970, 70), 38f, TextAlignmentOptions.Center, FontRole.Body, Muted(0.8f)), StatusY);
            var codeLabel = Bottom(UiFactory.Label(content, "JoinCode", "", Vector2.zero,
                new Vector2(970, 60), 38f, TextAlignmentOptions.Center, FontRole.BodySemibold), CodeLabelY);
            Bottom(UiFactory.Label(content, "Footer", "REV 0.1 · FRESH FROM THE FOUNDRY", Vector2.zero,
                new Vector2(900, 48), 38f, TextAlignmentOptions.Center, FontRole.BodyMedium, Muted(0.62f), 0.18f), FooterY);

            var view = content.gameObject.AddComponent<MainMenuView>();
            SetRef(view, "hostButton", host);
            SetRef(view, "joinButton", join);
            SetRef(view, "passPlayButton", passPlay);
            SetRef(view, "soloButton", solo);
            SetRef(view, "howToPlayButton", howTo);
            SetRef(view, "settingsButton", settingsButton);
            SetRef(view, "joinCodeInput", codeInput);
            SetRef(view, "nameInput", nameInput);
            SetRef(view, "statusLabel", status);
            SetRef(view, "joinCodeLabel", codeLabel);

            var howToPlayView = BuildHowToPlayPanel(content);
            var settingsController = BuildSettingsPanel(content);

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            SetRef(controller, "view", view);
            SetRef(controller, "howToPlay", howToPlayView);
            SetRef(controller, "settings", settingsController);

            return Save(scene, SceneNames.MainMenu);
        }

        /// <summary>A small drawn die face, the hero block's identity glyph.</summary>
        private static void MiniDieGlyph(RectTransform parent, string name, int face, Vector2 position)
        {
            var glyph = UiFactory.Panel(parent, name, stretch: false);
            glyph.sizeDelta = new Vector2(66, 66);
            glyph.anchoredPosition = position;

            var frame = UiFactory.BlueprintFrame(glyph, marks: false);
            frame.SetBorderColor(_theme.Accent(700));

            var pipImages = new Image[9];
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var pipGo = new GameObject($"Pip{row * 3 + col}", typeof(RectTransform), typeof(Image));
                var pipRt = (RectTransform)pipGo.transform;
                pipRt.SetParent(glyph, false);
                pipRt.sizeDelta = new Vector2(10, 10);
                pipRt.anchoredPosition = new Vector2((col - 1) * 20, (1 - row) * 20);
                var image = pipGo.GetComponent<Image>();
                image.color = _theme.Accent(700);
                image.raycastTarget = false;
                pipImages[row * 3 + col] = image;
            }

            var pips = glyph.gameObject.AddComponent<DiePipGrid>();
            pips.Bind(pipImages);
            pips.SetFace(face);
        }

        /// <summary>The ink at partial strength, for secondary copy.</summary>
        private static Color Muted(float alpha)
        {
            var color = _theme.textPrimary;
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// The first-run explainer (STORY-3.5). A full-screen overlay on the menu's own canvas
        /// rather than a fifth scene: it is four pages of reading, and a scene load either side
        /// of that would cost more than it buys.
        /// </summary>
        private static HowToPlayView BuildHowToPlayPanel(Transform parent)
        {
            var panel = FullScreenPanel(parent, "HowToPlayPanel", _theme.surfaceOverlay);

            var title = UiFactory.Label(panel, "Title", "", new Vector2(0, 540), new Vector2(900, 200), 74f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var body = UiFactory.Label(panel, "Body", "", new Vector2(0, 40), new Vector2(880, 780), 48f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var progress = UiFactory.Label(panel, "Progress", "", new Vector2(0, -430), new Vector2(400, 60), 38f,
                TextAlignmentOptions.Center, FontRole.BodyMedium,
                UiFactory.WithAlpha(_theme.textInverse, 0.55f), 0.18f);

            var next = UiFactory.Button(panel, "NextButton", "NEXT",
                new Vector2(160, -560), new Vector2(360, 140));
            var nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
            var back = UiFactory.Button(panel, "BackButton", "BACK",
                new Vector2(-160, -560), new Vector2(360, 140), ButtonStyle.Ghost);
            var skip = UiFactory.Button(panel, "SkipButton", "SKIP",
                new Vector2(0, -720), new Vector2(360, 110), ButtonStyle.Ghost);

            // Deliberately NOT under NEXT. It used to share that rect, which meant a player
            // tapping quickly through the pages had their next tap — same thumb, a fraction of a
            // second later — land on PLAY SOLO and start a match they never asked for, from the
            // one screen aimed at people who do not yet know what the buttons do.
            var playSolo = UiFactory.Button(panel, "PlaySoloButton", "PLAY SOLO",
                new Vector2(0, -720), new Vector2(460, 130));
            UiFactory.BlueprintFrame((RectTransform)playSolo.transform, FrameEmphasis.AccentStrong);

            var view = panel.gameObject.AddComponent<HowToPlayView>();
            SetRef(view, "root", panel.gameObject);
            SetRef(view, "titleText", title);
            SetRef(view, "bodyText", body);
            SetRef(view, "progressText", progress);
            SetRef(view, "nextButton", next);
            SetRef(view, "nextLabel", nextLabel);
            SetRef(view, "backButton", back);
            SetRef(view, "skipButton", skip);
            SetRef(view, "playSoloButton", playSolo);
            return view;
        }

        /// <summary>
        /// The settings panel (STORY-4.1). One builder, dropped into both the menu and the Game
        /// scene, because AC2 wants it reachable from either and a scene load mid-match would
        /// end the match. Returns the controller so the caller can hang a button off it.
        /// </summary>
        private static SettingsController BuildSettingsPanel(Transform parent)
        {
            var panel = FullScreenPanel(parent, "SettingsPanel", _theme.surfaceOverlay);

            UiFactory.Label(panel, "Title", "SETTINGS", new Vector2(0, 640), new Vector2(900, 110), 69f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse, 0.1f);

            // Shown only over a live online match — the presenter decides, the layout always has
            // room for it so nothing shifts when it appears.
            var warning = UiFactory.Label(panel, "LiveMatchWarning",
                "The round clock keeps running —\nthere is no pause in a live match.",
                new Vector2(0, 545), new Vector2(900, 104), 38f,
                TextAlignmentOptions.Center, FontRole.BodyMedium, _theme.Accent(700));

            float y = 400f;
            y = SettingsSection(panel, "AUDIO", y);
            var master = SettingsSlider(panel, "Master", "Master", ref y);
            var music = SettingsSlider(panel, "Music", "Music", ref y);
            var sfxSlider = SettingsSlider(panel, "Sfx", "Effects", ref y);

            y -= 40f;
            y = SettingsSection(panel, "GAMEPLAY", y);

            UiFactory.Label(panel, "NameLabel", "Your name", new Vector2(-330, y), new Vector2(360, 60), 38f,
                TextAlignmentOptions.Left, FontRole.Body, _theme.textInverse);
            var nameInput = UiFactory.InputField(panel, "NameInput", "Your name (optional)",
                new Vector2(150, y), new Vector2(560, 100));
            nameInput.characterLimit = PlayerName.MaxLength;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            y -= 130f;

            var haptics = SettingsToggle(panel, "Haptics", "Haptics", ref y);

            y -= 40f;
            y = SettingsSection(panel, "ACCESSIBILITY", y);
            var reducedMotion = SettingsToggle(panel, "ReducedMotion", "Reduced motion", ref y);

            var close = UiFactory.Button(panel, "CloseButton", "DONE",
                new Vector2(0, -700), new Vector2(460, 140));

            var view = panel.gameObject.AddComponent<SettingsView>();
            SetRef(view, "root", panel.gameObject);
            SetRef(view, "closeButton", close);
            SetRef(view, "liveMatchWarning", warning);
            SetRef(view, "masterSlider", master);
            SetRef(view, "musicSlider", music);
            SetRef(view, "sfxSlider", sfxSlider);
            SetRef(view, "nameInput", nameInput);
            SetRef(view, "hapticsToggle", haptics);
            SetRef(view, "reducedMotionToggle", reducedMotion);
            SetRef(view, "theme", _theme);

            // The controller sits on the parent, not the panel: the panel starts inactive, and a
            // component there would not run Start() until something opened it — which is the
            // thing the controller is responsible for doing.
            var controller = parent.gameObject.AddComponent<SettingsController>();
            SetRef(controller, "view", view);
            SetRef(controller, "sfx", AssetDatabase.LoadAssetAtPath<SfxCatalog>("Assets/_Project/Audio/SfxCatalog.asset"));
            return controller;
        }

        private static float SettingsSection(Transform panel, string title, float y)
        {
            UiFactory.Label(panel, title + "Header", title, new Vector2(-330, y), new Vector2(400, 50), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.18f);
            return y - 90f;
        }

        private static UnityEngine.UI.Slider SettingsSlider(Transform panel, string name, string label,
            ref float y)
        {
            UiFactory.Label(panel, name + "Label", label, new Vector2(-330, y), new Vector2(360, 60), 38f,
                TextAlignmentOptions.Left, FontRole.Body, _theme.textInverse);
            var slider = UiFactory.Slider(panel, name + "Slider", new Vector2(160, y), new Vector2(540, 80));
            y -= 110f;
            return slider;
        }

        private static Button SettingsToggle(Transform panel, string name, string label, ref float y)
        {
            UiFactory.Label(panel, name + "Label", label, new Vector2(-330, y), new Vector2(420, 60), 38f,
                TextAlignmentOptions.Left, FontRole.Body, _theme.textInverse);
            var toggle = UiFactory.ToggleButton(panel, name + "Toggle", new Vector2(280, y),
                new Vector2(220, 92), on: true);
            y -= 120f;
            return toggle;
        }

        private static string BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            // ---- Header row: back out on the left, the mode named on the right ----
            var back = Top(UiFactory.Button(content, "BackButton", "< BACK",
                new Vector2(-370, 0), new Vector2(260, 90), ButtonStyle.Ghost), -110);

            var tag = UiFactory.Panel(content, "ModeTag", stretch: false);
            tag.sizeDelta = new Vector2(400, 70);
            Top(tag, -110);
            tag.anchoredPosition = new Vector2(300, tag.anchoredPosition.y);
            UiFactory.BlueprintFrame(tag, marks: false);
            UiFactory.Label(tag, "Text", "FRIENDS BY CODE", Vector2.zero, new Vector2(380, 60), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.14f);

            // ---- Code panel: the thing you read out loud ----
            var codePanel = UiFactory.Panel(content, "CodePanel", stretch: false);
            codePanel.sizeDelta = new Vector2(970, 440);
            Top(codePanel, -450);
            UiFactory.BlueprintFrame(codePanel, FrameEmphasis.Accent);
            UiFactory.Label(codePanel, "Eyebrow", "JOIN CODE", new Vector2(0, 150), new Vector2(900, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            var codeLabel = UiFactory.Label(codePanel, "Code", "—", new Vector2(0, 10), new Vector2(920, 180), 150f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, null, 0.12f);
            var status = UiFactory.Label(codePanel, "Caption", "", new Vector2(0, -150), new Vector2(900, 60), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.55f));

            // ---- Seats ----
            Top(UiFactory.Label(content, "SeatsLabel", "SEATS",
                new Vector2(-430, 0), new Vector2(200, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.14f), -730);
            var seatsCount = Top(UiFactory.Label(content, "SeatsCount", "0 / 6",
                new Vector2(430, 0), new Vector2(200, 48), 38f,
                TextAlignmentOptions.Right, FontRole.BodySemibold, Muted(0.55f), 0.14f), -730);

            var seatsRoot = UiFactory.Panel(content, "Seats", stretch: false);
            seatsRoot.sizeDelta = new Vector2(970, 900);
            Top(seatsRoot, -1250);
            Column(seatsRoot, spacing: 12);

            // ---- Start, host-only, states its own reason when waiting ----
            var start = Bottom(UiFactory.Button(content, "StartButton", "START MATCH",
                Vector2.zero, new Vector2(970, 144)), 140);
            UiFactory.BlueprintFrame((RectTransform)start.transform, FrameEmphasis.AccentStrong);
            var startFill = start.GetComponent<Image>();
            var startLabel = start.transform.Find("Text").GetComponent<TMP_Text>();

            // Seat row template, deactivated in-scene (the prefab-reference pattern).
            var templates = UiFactory.Panel(canvas.transform, "Templates");
            var seatTemplate = BuildSeatRowTemplate(templates);
            templates.gameObject.SetActive(false);

            var view = content.gameObject.AddComponent<LobbyView>();
            SetRef(view, "codeLabel", codeLabel);
            SetRef(view, "statusLabel", status);
            SetRef(view, "seatsCountLabel", seatsCount);
            SetRef(view, "seatsRoot", seatsRoot);
            SetRef(view, "seatRowTemplate", seatTemplate);
            SetRef(view, "startButton", start);
            SetRef(view, "startFill", startFill);
            SetRef(view, "startLabel", startLabel);
            SetRef(view, "backButton", back);
            SetRef(view, "theme", _theme);

            var controller = canvas.gameObject.AddComponent<LobbyController>();
            SetRef(controller, "view", view);

            return Save(scene, SceneNames.Lobby);
        }

        /// <summary>The reveal spotlight (UI-4, handoff 6l): full-bleed, above the board, below the
        /// hot-seat privacy panels. Everything inside renders from the snapshot's Reveals.</summary>
        private static RevealSpotlightView BuildRevealSpotlight(Canvas canvas)
        {
            var rootGo = new GameObject("RevealSpotlight", typeof(RectTransform), typeof(Image), typeof(Button));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(canvas.transform, false);
            UiFactory.Stretch(root);
            rootGo.GetComponent<Image>().color = _theme.surfaceOverlay;
            var tap = rootGo.GetComponent<Button>();
            tap.transition = Selectable.Transition.None;

            var headerLeft = Top(UiFactory.Label(root, "HeaderLeft", "", new Vector2(-250, 0), new Vector2(500, 64), 50f,
                TextAlignmentOptions.Left, FontRole.HeadingBold, _theme.textInverse), -120);
            var headerRight = Top(UiFactory.Label(root, "HeaderRight", "", new Vector2(330, 0), new Vector2(360, 50), 38f,
                TextAlignmentOptions.Right, FontRole.BodySemibold, UiFactory.WithAlpha(_theme.textInverse, 0.7f), 0.14f), -125);

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var card = (RectTransform)cardGo.transform;
            card.SetParent(root, false);
            card.sizeDelta = new Vector2(640, 860);
            card.anchoredPosition = new Vector2(0, 120);
            cardGo.GetComponent<Image>().color = _theme.surfaceBase;
            UiFactory.BlueprintFrame(card, FrameEmphasis.Accent);

            var cardTier = UiFactory.Label(card, "Tier", "", new Vector2(-180, 370), new Vector2(240, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.18f);
            var cardPoints = UiFactory.Label(card, "Points", "", new Vector2(200, 370), new Vector2(200, 62), 50f,
                TextAlignmentOptions.Right, FontRole.HeadingBold);
            var cardName = UiFactory.Label(card, "Name", "", new Vector2(0, 160), new Vector2(560, 240), 85f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            var cardPower = UiFactory.Label(card, "Power", "", new Vector2(0, -160), new Vector2(540, 320), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.8f));

            var claimants = UiFactory.Panel(root, "Claimants", stretch: false);
            claimants.sizeDelta = new Vector2(1000, 110);
            claimants.anchoredPosition = new Vector2(0, -430);
            Row(claimants, spacing: 12);

            var chipGo = new GameObject("ClaimantChip", typeof(RectTransform), typeof(Image));
            var chip = (RectTransform)chipGo.transform;
            chip.SetParent(root, false);
            // 380 wide: the label carries "{NAME} · {score}" and PlayerName allows 16 characters,
            // which is 440 units at 38f — 300 fit only the short names.
            chip.sizeDelta = new Vector2(380, 100);
            FixedSize(chipGo, 380, 100);
            chipGo.GetComponent<Image>().color = _theme.Accent(800);
            var chipText = UiFactory.Label(chip, "Text", "", Vector2.zero, new Vector2(360, 80), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.textInverse);
            // A 16-character name still overruns 360; ellipsis beats wrapping into two lines that
            // the 100-unit chip cannot hold.
            chipText.textWrappingMode = TextWrappingModes.NoWrap;
            chipText.overflowMode = TextOverflowModes.Ellipsis;
            chipGo.SetActive(false);

            var resultStamp = UiFactory.Label(root, "ResultStamp", "", new Vector2(0, -240), new Vector2(960, 110), 74f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var reasonLine = UiFactory.Label(root, "ReasonLine", "", new Vector2(0, -330), new Vector2(900, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, UiFactory.WithAlpha(_theme.textInverse, 0.8f), 0.14f);
            var prompt = Bottom(UiFactory.Label(root, "ContinuePrompt", "TAP TO CONTINUE", Vector2.zero, new Vector2(600, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, UiFactory.WithAlpha(_theme.textInverse, 0.6f), 0.18f), 90);

            rootGo.SetActive(false);

            var view = rootGo.AddComponent<RevealSpotlightView>();
            SetRef(view, "root", rootGo);
            SetRef(view, "tapCatcher", tap);
            SetRef(view, "headerLeft", headerLeft);
            SetRef(view, "headerRight", headerRight);
            SetRef(view, "cardPanel", card);
            SetRef(view, "cardTier", cardTier);
            SetRef(view, "cardPoints", cardPoints);
            SetRef(view, "cardName", cardName);
            SetRef(view, "cardPower", cardPower);
            SetRef(view, "claimantsRoot", claimants);
            SetRef(view, "claimantChipTemplate", chip);
            SetRef(view, "resultStamp", resultStamp);
            SetRef(view, "reasonLine", reasonLine);
            SetRef(view, "continuePrompt", prompt);
            SetRef(view, "anims", canvas.GetComponent<UiAnimationService>());
            SetRef(view, "theme", _theme);

            return view;
        }

        /// <summary>Match-end standings (handoff screen 4): a full page over everything.</summary>
        private static EndScreenView BuildEndScreen(Canvas canvas)
        {
            var rootGo = new GameObject("EndScreen", typeof(RectTransform), typeof(Image));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(canvas.transform, false);
            UiFactory.Stretch(root);
            rootGo.GetComponent<Image>().color = _theme.surfaceBase;

            var eyebrow = Top(UiFactory.Label(root, "Eyebrow", "", Vector2.zero, new Vector2(900, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f), -150);
            var headline = Top(UiFactory.Label(root, "Headline", "", Vector2.zero, new Vector2(1000, 150), 122f,
                TextAlignmentOptions.Center, FontRole.HeadingBold), -260);
            var note = Top(UiFactory.Label(root, "Note", "", Vector2.zero, new Vector2(900, 48), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.7f)), -370);

            var rowsRoot = UiFactory.Panel(root, "Rows", stretch: false);
            rowsRoot.sizeDelta = new Vector2(960, 900);
            rowsRoot.anchoredPosition = new Vector2(0, -60);
            Column(rowsRoot, spacing: 14);

            // Standing row template, deactivated in place.
            var rowGo = new GameObject("StandingRow", typeof(RectTransform), typeof(Image));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(root, false);
            row.sizeDelta = new Vector2(960, 130);
            FixedSize(rowGo, 960, 130);
            rowGo.GetComponent<Image>().color = _theme.surfaceRaised;
            UiFactory.BlueprintFrame(row, marks: false);
            UiFactory.Label(row, "Rank", "1", new Vector2(-420, 0), new Vector2(80, 64), 50f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, Muted(0.55f));
            var rowName = UiFactory.Label(row, "Name", "", new Vector2(-160, 0), new Vector2(400, 52), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold);
            rowName.textWrappingMode = TextWrappingModes.NoWrap;
            rowName.overflowMode = TextOverflowModes.Ellipsis;
            // "3 cards · 12 sparks · +5 end-game VP" is ~583 units at 38f, so it needs two lines
            // in this 340-unit column rather than running through the score beside it.
            UiFactory.Label(row, "Detail", "", new Vector2(160, 0), new Vector2(340, 100), 38f,
                TextAlignmentOptions.Left, FontRole.Body, Muted(0.55f));
            UiFactory.Label(row, "Score", "", new Vector2(390, 0), new Vector2(180, 72), 60f,
                TextAlignmentOptions.Right, FontRole.HeadingBold);
            rowGo.SetActive(false);

            var rematch = Bottom(UiFactory.Button(root, "RematchButton", "REMATCH",
                Vector2.zero, new Vector2(940, 132)), 330);
            UiFactory.BlueprintFrame((RectTransform)rematch.transform, FrameEmphasis.AccentStrong);
            var menu = Bottom(UiFactory.Button(root, "MenuButton", "MAIN MENU",
                Vector2.zero, new Vector2(940, 132), ButtonStyle.Secondary), 170);

            rootGo.SetActive(false);

            var view = rootGo.AddComponent<EndScreenView>();
            SetRef(view, "root", rootGo);
            SetRef(view, "eyebrow", eyebrow);
            SetRef(view, "headline", headline);
            SetRef(view, "note", note);
            SetRef(view, "rowsRoot", rowsRoot);
            SetRef(view, "rowTemplate", row);
            SetRef(view, "rematchButton", rematch);
            SetRef(view, "menuButton", menu);
            SetRef(view, "anims", canvas.GetComponent<UiAnimationService>());
            SetRef(view, "theme", _theme);

            return view;
        }

        /// <summary>The re-pick sheet (MKT-3, handoff 6k), reusing the market card template.</summary>
        private static RepickSheetView BuildRepickSheet(RectTransform content, CardButtonView cardTemplate)
        {
            var go = new GameObject("RepickSheet", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.sizeDelta = new Vector2(1020, 640);
            Bottom(rt, 380);
            go.GetComponent<Image>().color = _theme.surfaceBase;
            UiFactory.BlueprintFrame(rt, FrameEmphasis.Accent);

            UiFactory.Label(rt, "Eyebrow", "RE-PICK", new Vector2(-360, 260), new Vector2(240, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            UiFactory.Label(rt, "Copy", "You lost the contest — your dice are back.",
                new Vector2(-60, 205), new Vector2(760, 48), 38f,
                TextAlignmentOptions.Left, FontRole.Body, Muted(0.7f));
            var countdown = UiFactory.Label(rt, "Countdown", "", new Vector2(420, 240), new Vector2(150, 70), 55f,
                TextAlignmentOptions.Right, FontRole.HeadingBold);
            countdown.gameObject.SetActive(false);

            var cardsRoot = UiFactory.Panel(rt, "Cards", stretch: false);
            cardsRoot.sizeDelta = new Vector2(990, 340);
            cardsRoot.anchoredPosition = new Vector2(0, 10);
            Row(cardsRoot, spacing: 8);

            var pass = UiFactory.Button(rt, "PassButton", "PASS — TAKE 3 SPARKS",
                new Vector2(0, -240), new Vector2(940, 120), ButtonStyle.Secondary);

            go.SetActive(false);

            var view = go.AddComponent<RepickSheetView>();
            SetRef(view, "root", go);
            SetRef(view, "countdownText", countdown);
            SetRef(view, "cardsRoot", cardsRoot);
            SetRef(view, "cardTemplate", cardTemplate);
            SetRef(view, "passButton", pass);

            return view;
        }

        /// <summary>The upkeep dialog (handoff 6j): small, centred, informational only.</summary>
        private static UpkeepModalView BuildUpkeepModal(RectTransform content)
        {
            var go = new GameObject("UpkeepModal", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.sizeDelta = new Vector2(760, 460);
            rt.anchoredPosition = new Vector2(0, 100);
            go.GetComponent<Image>().color = _theme.surfaceBase;
            UiFactory.BlueprintFrame(rt, FrameEmphasis.Accent);

            UiFactory.Label(rt, "Eyebrow", "UPKEEP", new Vector2(0, 165), new Vector2(700, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            var body = UiFactory.Label(rt, "Body", "", new Vector2(0, -30), new Vector2(680, 320), 38f,
                TextAlignmentOptions.Center, FontRole.Body);

            go.SetActive(false);

            var view = go.AddComponent<UpkeepModalView>();
            SetRef(view, "root", go);
            SetRef(view, "bodyText", body);

            return view;
        }

        /// <summary>The card inspect sheet (handoff 6h): scrim + blueprint panel under the header.</summary>
        private static CardZoomSheetView BuildCardZoomSheet(RectTransform content)
        {
            var root = UiFactory.Panel(content, "CardZoomSheet");

            var scrimGo = new GameObject("Scrim", typeof(RectTransform), typeof(Image), typeof(Button));
            var scrimRt = (RectTransform)scrimGo.transform;
            scrimRt.SetParent(root, false);
            UiFactory.Stretch(scrimRt);
            scrimGo.GetComponent<Image>().color = UiFactory.WithAlpha(_theme.surfaceOverlay, 0.45f);
            var scrim = scrimGo.GetComponent<Button>();
            scrim.transition = Selectable.Transition.None;

            var sheetGo = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
            var sheet = (RectTransform)sheetGo.transform;
            sheet.SetParent(root, false);
            sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 1f);
            sheet.pivot = new Vector2(0.5f, 1f);
            sheet.sizeDelta = new Vector2(1000, 900);
            sheet.anchoredPosition = new Vector2(0, -140);
            sheetGo.GetComponent<Image>().color = _theme.surfaceBase;
            UiFactory.BlueprintFrame(sheet, FrameEmphasis.Accent);

            var tier = UiFactory.Label(sheet, "TierTag", "TIER 1", new Vector2(-360, 380), new Vector2(240, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.18f);
            var family = UiFactory.Label(sheet, "FamilyTag", "", new Vector2(-90, 380), new Vector2(280, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.18f);
            var close = UiFactory.Button(sheet, "CloseButton", "×", new Vector2(430, 380), new Vector2(90, 90),
                ButtonStyle.Ghost);

            var name = UiFactory.Label(sheet, "Name", "", new Vector2(-90, 280), new Vector2(760, 100), 72f,
                TextAlignmentOptions.Left, FontRole.HeadingBold);

            var vpBox = UiFactory.Panel(sheet, "VpBox", stretch: false);
            vpBox.sizeDelta = new Vector2(190, 110);
            vpBox.anchoredPosition = new Vector2(370, 280);
            UiFactory.BlueprintFrame(vpBox, marks: false);
            var points = UiFactory.Label(vpBox, "Value", "0 VP", Vector2.zero, new Vector2(180, 90), 50f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);

            UiFactory.Label(sheet, "CostHead", "COST", new Vector2(-300, 160), new Vector2(320, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.2f);
            var cost = UiFactory.Label(sheet, "Cost", "", new Vector2(-300, 40), new Vector2(320, 190), 38f,
                TextAlignmentOptions.TopLeft, FontRole.Body);
            UiFactory.Label(sheet, "PowerHead", "PERMANENT POWER", new Vector2(150, 160), new Vector2(560, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.2f);
            var power = UiFactory.Label(sheet, "Power", "", new Vector2(150, 40), new Vector2(560, 190), 38f,
                TextAlignmentOptions.TopLeft, FontRole.Body);

            var payStatus = UiFactory.Label(sheet, "PayStatus", "", new Vector2(0, -160), new Vector2(900, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodyMedium);

            var commit = UiFactory.Button(sheet, "CommitButton", "COMMIT · SECRET",
                new Vector2(0, -270), new Vector2(920, 130));
            UiFactory.BlueprintFrame((RectTransform)commit.transform, FrameEmphasis.AccentStrong);
            var commitFill = commit.GetComponent<Image>();
            var commitLabel = commit.transform.Find("Text").GetComponent<TMP_Text>();

            UiFactory.Label(sheet, "SmallPrint",
                "Commits are secret until Reveal.\nContested cards go to the lowest score.",
                new Vector2(0, -404), new Vector2(900, 104), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.55f));

            root.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<CardZoomSheetView>();
            SetRef(view, "root", root.gameObject);
            SetRef(view, "scrimButton", scrim);
            SetRef(view, "closeButton", close);
            SetRef(view, "tierTag", tier);
            SetRef(view, "familyTag", family);
            SetRef(view, "nameText", name);
            SetRef(view, "pointsText", points);
            SetRef(view, "costText", cost);
            SetRef(view, "powerText", power);
            SetRef(view, "payStatusText", payStatus);
            SetRef(view, "commitButton", commit);
            SetRef(view, "commitFill", commitFill);
            SetRef(view, "commitLabel", commitLabel);
            SetRef(view, "theme", _theme);

            return view;
        }

        /// <summary>The first-time hint toast (handoff 6i): dark strip above the action bar.</summary>
        private static HintToastView BuildHintToast(RectTransform content)
        {
            var go = new GameObject("HintToast", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.sizeDelta = new Vector2(980, 180);
            Bottom(rt, 480);
            go.GetComponent<Image>().color = _theme.surfaceOverlay;

            var body = UiFactory.Label(rt, "Body", "", new Vector2(-100, 0), new Vector2(700, 160), 38f,
                TextAlignmentOptions.Left, FontRole.Body, _theme.textInverse);

            var gotIt = UiFactory.Button(rt, "GotItButton", "GOT IT", new Vector2(390, 0), new Vector2(170, 84),
                ButtonStyle.Ghost);
            var gotItLabel = gotIt.GetComponentInChildren<TextMeshProUGUI>();
            if (gotItLabel != null) gotItLabel.color = _theme.textInverse;

            go.SetActive(false);

            var view = go.AddComponent<HintToastView>();
            SetRef(view, "root", go);
            SetRef(view, "bodyText", body);
            SetRef(view, "gotItButton", gotIt);

            return view;
        }

        /// <summary>The bottom bar's Done/timer square (handoff 6g): frame, two rings, two labels.</summary>
        private static DoneTimerButtonView BuildDoneTimer(RectTransform content, Canvas canvas)
        {
            float size = DesignTokens.Px(74);

            var go = new GameObject("DoneTimer", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(content, false);
            rt.sizeDelta = new Vector2(size, size);
            Bottom(rt, 190);
            rt.anchoredPosition = new Vector2(400, rt.anchoredPosition.y);

            var fill = go.GetComponent<Image>();
            fill.color = _theme.surfaceRaised;
            UiFactory.BlueprintFrame(rt);

            var track = TimerRing(rt, "Track", UiFactory.WithAlpha(_theme.Accent(200), 0.9f));
            var progress = TimerRing(rt, "Progress", _theme.accentPriority);

            var label = UiFactory.Label(rt, "Label", "—", new Vector2(0, 22), new Vector2(size - 20, 60), 48f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);
            var seconds = UiFactory.Label(rt, "Seconds", "", new Vector2(0, -44), new Vector2(size - 20, 73), 59f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);

            var view = go.AddComponent<DoneTimerButtonView>();
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "fill", fill);
            SetRef(view, "track", track);
            SetRef(view, "progress", progress);
            SetRef(view, "label", label);
            SetRef(view, "secondsLabel", seconds);
            SetRef(view, "anims", canvas.GetComponent<UiAnimationService>());
            SetRef(view, "theme", _theme);

            return view;
        }

        /// <summary>Vertical lines every 135 units across the width; horizontal lines marching
        /// down from the top far enough to cover the tallest phone. Non-interactive.</summary>
        private static void BuildDraftingGrid(RectTransform content)
        {
            var grid = UiFactory.Panel(content, "DraftingGrid");
            grid.SetAsFirstSibling();
            var ink = Muted(0.05f);

            for (int x = -405; x <= 405; x += 135)
            {
                var line = GridLine(grid, $"V{x}", ink);
                line.anchorMin = new Vector2(0.5f, 0f);
                line.anchorMax = new Vector2(0.5f, 1f);
                line.sizeDelta = new Vector2(2f, 0f);
                line.anchoredPosition = new Vector2(x, 0f);
            }

            for (int i = 0; i < 18; i++)
            {
                var line = GridLine(grid, $"H{i}", ink);
                line.anchorMin = new Vector2(0f, 1f);
                line.anchorMax = new Vector2(1f, 1f);
                line.sizeDelta = new Vector2(0f, 2f);
                line.anchoredPosition = new Vector2(0f, -60f - i * 135f);
            }
        }

        private static RectTransform GridLine(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        private static SquareTimerRing TimerRing(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            UiFactory.Stretch(rt);

            var ring = go.AddComponent<SquareTimerRing>();
            ring.color = color;
            ring.Thickness = 8f;
            ring.Fill01 = 1f;
            ring.raycastTarget = false;
            return ring;
        }

        private static RectTransform BuildPowerChipTemplate(Transform parent)
        {
            var go = new GameObject("PowerChip", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            // 250, not 330: four chips is routine mid-match (re-roll, nudge, set-face, a wild
            // face) and the row is 1020 wide, so 330 pushed the outer two off the screen. The
            // type is sized to the chip instead — the longest the HUD writes is "SET FACE FREE
            // ×2", which fits 240 units at 26f and ellipsized entirely at 38f.
            rt.sizeDelta = new Vector2(250, 56);
            FixedSize(go, 250, 56);
            go.GetComponent<Image>().color = _theme.Accent(100);

            var label = UiFactory.Label(rt, "Text", "POWER", Vector2.zero, new Vector2(250, 48), 26f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(800), 0.08f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            return rt;
        }

        private static SeatRowView BuildSeatRowTemplate(Transform parent)
        {
            const float RowWidth = 970f;
            const float RowHeight = 133f;

            var go = new GameObject("SeatRow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);
            FixedSize(go, RowWidth, RowHeight);

            var background = go.GetComponent<Image>();
            background.color = _theme.surfaceRaised;
            var frame = UiFactory.BlueprintFrame(rt, marks: false);

            var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            var avatarRt = (RectTransform)avatarGo.transform;
            avatarRt.SetParent(rt, false);
            avatarRt.sizeDelta = new Vector2(72, 72);
            avatarRt.anchoredPosition = new Vector2(-420, 0);
            var avatarTile = avatarGo.GetComponent<Image>();
            avatarTile.color = _theme.surfaceBase;
            var avatarFrame = UiFactory.BlueprintFrame(avatarRt, marks: false);
            var avatarInitial = UiFactory.Label(avatarRt, "Initial", "P", Vector2.zero, new Vector2(70, 70), 38f,
                TextAlignmentOptions.Center, FontRole.Heading);

            var nameLabel = UiFactory.Label(rt, "Name", "", new Vector2(-40, 0), new Vector2(620, 60), 48f,
                TextAlignmentOptions.Left, FontRole.BodySemibold);
            var chipLabel = UiFactory.Label(rt, "Chip", "", new Vector2(380, 0), new Vector2(180, 48), 38f,
                TextAlignmentOptions.Right, FontRole.BodySemibold, _theme.Accent(700), 0.14f);

            var view = go.AddComponent<SeatRowView>();
            SetRef(view, "background", background);
            SetRef(view, "frame", frame);
            SetRef(view, "avatarTile", avatarTile);
            SetRef(view, "avatarFrame", avatarFrame);
            SetRef(view, "avatarInitial", avatarInitial);
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "chipText", chipLabel);
            SetRef(view, "theme", _theme);

            return view;
        }

        private static string BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            // The drafting table (P5): a whisper-quiet blueprint grid behind everything, drawn
            // from the ink token at 5% so it survives any theme. First sibling = behind all bands.
            BuildDraftingGrid(content);

            // Everything anchors to the top or bottom edge rather than to the centre. Absolute
            // offsets from centre only work at the exact reference aspect: on a wider, shorter view
            // the outermost rows simply fall off the screen.

            // --- header row (handoff 6a): round + phase left, the Sparks tag right ---
            var round = Top(UiFactory.Label(content, "Round", "", new Vector2(-280, 0), new Vector2(480, 86), 68f,
                TextAlignmentOptions.Left, FontRole.HeadingBold), -60);
            var phase = Top(UiFactory.Label(content, "Phase", "", new Vector2(-280, 0), new Vector2(480, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.16f), -130);

            // The Game scene had no way out and no way in to anything (STORY-4.1 AC2): no back
            // button, nothing in the top bar. This gear takes the gap between the round label and
            // the Sparks chip, which is the only free space up there.
            // Same substituted-box problem as the menu's; see there.
            var settingsButton = Top(UiFactory.Button(content, "SettingsButton", "SET",
                new Vector2(70, 0), new Vector2(92, 76), ButtonStyle.Ghost, fontSize: 38f), -78);
            UiFactory.BlueprintFrame((RectTransform)settingsButton.transform, marks: false);

            var sparksChipGo = new GameObject("SparksChip", typeof(RectTransform), typeof(Image));
            var sparksChip = (RectTransform)sparksChipGo.transform;
            sparksChip.SetParent(content, false);
            sparksChip.sizeDelta = new Vector2(330, 76);
            Top(sparksChip, -78);
            sparksChip.anchoredPosition = new Vector2(340, sparksChip.anchoredPosition.y);
            sparksChipGo.GetComponent<Image>().color = _theme.Accent(100);

            // The sparks chip is a little pressure gauge (P5): a square ring that fills 0→cap,
            // with the reading beside it. The pop on gain lives in GameHudView.
            var gaugeHost = UiFactory.Panel(sparksChip, "Gauge", stretch: false);
            gaugeHost.sizeDelta = new Vector2(46, 46);
            gaugeHost.anchoredPosition = new Vector2(-128, 0);
            var gaugeTrack = TimerRing(gaugeHost, "Track", _theme.divider);
            gaugeTrack.Thickness = 5f;
            var sparksGauge = TimerRing(gaugeHost, "Fill", _theme.Accent(700));
            sparksGauge.Thickness = 5f;
            sparksGauge.Fill01 = 0f;

            var sparks = UiFactory.Label(sparksChip, "Value", "", new Vector2(24, 0), new Vector2(270, 66), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(800), 0.06f);

            // Opponent rail (UI-1, handoff 6b): a horizontal strip, one cell per player, seat order.
            var railRoot = UiFactory.Panel(content, "Rail", stretch: false);
            railRoot.sizeDelta = new Vector2(1020, 180);
            Top(railRoot, -310);
            Row(railRoot, spacing: 6);

            // Market label row + 5-card band (handoff 6c). Anchored proportionally, not to the
            // top edge: on a tall phone the extra height then breathes above AND below the
            // market instead of pooling in one dead gap between market and tray.
            AtHeight(UiFactory.Label(content, "MarketLabel", "MARKET", new Vector2(-430, 0), new Vector2(220, 48), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.8f), 0.2f), 0.60f, 232);
            var marketMeta = AtHeight(UiFactory.Label(content, "MarketMeta", "", new Vector2(150, 0), new Vector2(720, 48), 38f,
                TextAlignmentOptions.Right, FontRole.BodyMedium, Muted(0.55f), 0.06f), 0.60f, 232);

            var marketRoot = UiFactory.Panel(content, "Market", stretch: false);
            marketRoot.sizeDelta = new Vector2(1020, 380);
            AtHeight(marketRoot, 0.60f);
            Row(marketRoot, spacing: 12);

            // --- controls, from the bottom up (handoff 6d-6g) ---
            var message = Bottom(UiFactory.Label(content, "Message", "", new Vector2(0, 0), new Vector2(1000, 76), 38f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.8f)), 45);

            // Bottom action bar: conditional ghosts left, the Done/timer square right (6g).
            var withdraw = Bottom(UiFactory.Button(content, "WithdrawButton", "WITHDRAW",
                new Vector2(-380, 0), new Vector2(290, 110), ButtonStyle.Ghost), 190);
            var pass = Bottom(UiFactory.Button(content, "PassButton", "PASS THIS ROUND",
                new Vector2(-55, 0), new Vector2(340, 110), ButtonStyle.Ghost), 190);
            var doneTimer = BuildDoneTimer(content, canvas);

            // Shape row ⇄ face picker share one band (6f / 6f-alt). Sits 8 units higher than the
            // authored band so the row's touch areas — grown to the 44pt minimum, which reaches
            // below what is drawn — clear the top edge of the done square beneath it.
            var reroll = Bottom(UiFactory.Button(content, "RerollButton", "RE-ROLL",
                new Vector2(-370, 0), new Vector2(310, 110), ButtonStyle.Secondary), 358);
            var nudgeDown = Bottom(UiFactory.Button(content, "NudgeDownButton", "-1",
                new Vector2(-125, 0), new Vector2(140, 110), ButtonStyle.Secondary), 358);
            var nudgeUp = Bottom(UiFactory.Button(content, "NudgeUpButton", "+1",
                new Vector2(30, 0), new Vector2(140, 110), ButtonStyle.Secondary), 358);
            var setFace = Bottom(UiFactory.Button(content, "SetFaceButton", "SET FACE",
                new Vector2(300, 0), new Vector2(380, 110), ButtonStyle.Secondary), 358);

            var faceRoot = UiFactory.Panel(content, "FaceButtons", stretch: false);
            faceRoot.sizeDelta = new Vector2(880, 110);
            Bottom(faceRoot, 358);
            faceRoot.anchoredPosition = new Vector2(-70, faceRoot.anchoredPosition.y);
            Row(faceRoot, spacing: 10);
            for (int face = 1; face <= 6; face++)
            {
                var faceButton = UiFactory.Button(faceRoot, "Face" + face, face.ToString(),
                    Vector2.zero, new Vector2(135, 100), ButtonStyle.Secondary);
                FixedSize(faceButton.gameObject, 135, 100);
            }
            var faceCancel = Bottom(UiFactory.Button(content, "FaceCancelButton", "×",
                new Vector2(460, 0), new Vector2(110, 100), ButtonStyle.Ghost), 358);

            // Dice tray (6e): a bordered panel, hint pinned to its top, dice wrapping in a grid so
            // eight at the 62px size still fit the phone width in two rows.
            float dieSize = DesignTokens.Px(62);
            var trayGo = new GameObject("DiceTrayPanel", typeof(RectTransform), typeof(Image));
            var tray = (RectTransform)trayGo.transform;
            tray.SetParent(content, false);
            tray.sizeDelta = new Vector2(1020, 400);
            Bottom(tray, 630);
            trayGo.GetComponent<Image>().color = _theme.surfaceRaised;
            UiFactory.BlueprintFrame(tray, marks: false);

            var trayHint = UiFactory.Label(tray, "Hint", "YOUR DICE", Vector2.zero, new Vector2(960, 46), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.2f);
            trayHint.rectTransform.anchorMin = trayHint.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            trayHint.rectTransform.anchoredPosition = new Vector2(0, -34);

            var diceRoot = UiFactory.Panel(tray, "Dice", stretch: false);
            diceRoot.sizeDelta = new Vector2(1000, 340);
            diceRoot.anchoredPosition = new Vector2(0, -22);
            var diceGrid = diceRoot.gameObject.AddComponent<GridLayoutGroup>();
            diceGrid.cellSize = new Vector2(dieSize, dieSize);
            diceGrid.spacing = new Vector2(24, 20);
            diceGrid.childAlignment = TextAnchor.MiddleCenter;

            // Owned powers strip (6d): collapses entirely when nothing is usable.
            var powersRoot = UiFactory.Panel(content, "Powers", stretch: false);
            powersRoot.sizeDelta = new Vector2(1020, 60);
            Bottom(powersRoot, 870);
            Row(powersRoot, spacing: 10);

            // Row templates the HUD clones from. These are scene objects rather than prefab assets:
            // an asset reference assigned here did not survive being saved out, leaving the board
            // with no dice and no market, while every scene-to-scene reference serialised fine.
            // They live under a deactivated parent, so the templates themselves never render but
            // their clones are active the moment they are parented into a live row.
            var templates = UiFactory.Panel(canvas.transform, "Templates");
            var cardButtonTemplate = BuildCardButtonTemplate(templates);
            var dieTemplate = BuildDieTemplate(templates);
            var playerRowTemplate = BuildPlayerRowTemplate(templates);
            var powerChipTemplate = BuildPowerChipTemplate(templates);
            templates.gameObject.SetActive(false);

            var hud = content.gameObject.AddComponent<GameHudView>();
            SetRef(hud, "roundLabel", round);
            SetRef(hud, "phaseLabel", phase);
            SetRef(hud, "sparksLabel", sparks);
            SetRef(hud, "sparksGauge", sparksGauge);
            SetRef(hud, "messageLabel", message);
            SetRef(hud, "railRoot", railRoot);
            SetRef(hud, "playerRowPrefab", playerRowTemplate);
            SetRef(hud, "diceRoot", diceRoot);
            SetRef(hud, "trayHintLabel", trayHint);
            SetRef(hud, "powersRoot", powersRoot);
            SetRef(hud, "powerChipTemplate", powerChipTemplate);
            SetRef(hud, "marketRoot", marketRoot);
            SetRef(hud, "marketMetaLabel", marketMeta);
            SetRef(hud, "faceButtonsRoot", faceRoot);
            SetRef(hud, "faceCancelButton", faceCancel);
            SetRef(hud, "rerollButton", reroll);
            SetRef(hud, "nudgeUpButton", nudgeUp);
            SetRef(hud, "nudgeDownButton", nudgeDown);
            SetRef(hud, "setFaceButton", setFace);
            SetRef(hud, "passButton", pass);
            SetRef(hud, "withdrawButton", withdraw);
            SetRef(hud, "doneTimer", doneTimer);
            SetRef(hud, "anims", canvas.GetComponent<UiAnimationService>());
            SetRef(hud, "cardButtonPrefab", cardButtonTemplate);
            SetRef(hud, "diePrefab", dieTemplate);

            // Overlay layers: above the board (created later = drawn later), below the hot-seat
            // privacy panels, which are built after and must stay on top. The zoom sheet is the
            // topmost of the board's own layers.
            var repickSheet = BuildRepickSheet(content, cardButtonTemplate);
            var upkeepModal = BuildUpkeepModal(content);
            var hintToast = BuildHintToast(content);
            var zoomSheet = BuildCardZoomSheet(content);

            var presenter = canvas.gameObject.AddComponent<GameHudPresenter>();
            SetRef(presenter, "view", hud);
            SetRef(presenter, "zoomSheet", zoomSheet);
            SetRef(presenter, "hintToast", hintToast);
            SetRef(presenter, "repickSheet", repickSheet);
            SetRef(presenter, "upkeepModal", upkeepModal);

            var spotlight = BuildRevealSpotlight(canvas);
            var overlay = BuildHotSeatOverlay(canvas.transform);
            var endScreen = BuildEndScreen(canvas);

            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (db == null)
                Debug.LogWarning("[Scaffold] CardDatabase not found — run 'Foundry ▸ Generate Starter Deck' " +
                                 "and re-run, or assign it on HotSeatHost and MatchLauncher.");
            else
                SetRef(presenter, "cardDatabase", db);

            // Path matches FoundrySfxGenerator.CatalogPath (EditorTools — not referenced here).
            var sfxCatalog = AssetDatabase.LoadAssetAtPath<SfxCatalog>("Assets/_Project/Audio/SfxCatalog.asset");
            if (sfxCatalog == null)
                Debug.LogWarning("[Scaffold] SfxCatalog not found — run 'Foundry ▸ Generate Sound Effects' " +
                                 "and re-run for an audible board.");
            else
                SetRef(presenter, "sfx", sfxCatalog);

            // Hot-seat host: playable immediately, with no networking involved.
            var hotSeatGo = new GameObject("HotSeatHost");
            var hotSeat = hotSeatGo.AddComponent<HotSeatHost>();
            SetRef(hotSeat, "presenter", presenter);
            SetRef(hotSeat, "overlay", overlay);
            SetRef(hotSeat, "spotlight", spotlight);
            SetRef(hotSeat, "endScreen", endScreen);
            if (db != null) SetRef(hotSeat, "cardDatabase", db);

            // Solo host: the same board with bots in the other chairs (STORY-7.1).
            var soloGo = new GameObject("SoloHost");
            var soloHost = soloGo.AddComponent<SoloHost>();
            SetRef(soloHost, "presenter", presenter);
            SetRef(soloHost, "overlay", overlay);
            SetRef(soloHost, "spotlight", spotlight);
            SetRef(soloHost, "endScreen", endScreen);
            if (db != null) SetRef(soloHost, "cardDatabase", db);

            // In-scene networked controller (spawns when the host loads this scene via NGO).
            var ctrlGo = new GameObject("GameController");
            ctrlGo.AddComponent<NetworkObject>();
            var controller = ctrlGo.AddComponent<NetworkGameController>();

            // Host-side match orchestration for online play.
            var launcherGo = new GameObject("MatchLauncher");
            var launcher = launcherGo.AddComponent<MatchLauncher>();
            SetRef(launcher, "gameController", controller);
            if (db != null) SetRef(launcher, "cardDatabase", db);

            // Decides between the two modes at runtime and disables whichever is not in play.
            var modeGo = new GameObject("GameSceneBootstrap");
            var settingsController = BuildSettingsPanel(content);
            settingsButton.onClick.AddListener(settingsController.Open);

            var mode = modeGo.AddComponent<GameSceneBootstrap>();
            SetRef(mode, "presenter", presenter);
            SetRef(mode, "hotSeatHost", hotSeat);
            SetRef(mode, "soloHost", soloHost);
            SetRef(mode, "hotSeatOverlay", overlay);
            SetRef(mode, "revealSpotlight", spotlight);
            SetRef(mode, "endScreen", endScreen);
            SetRef(mode, "networkController", controller);
            SetRef(mode, "matchLauncher", launcher);

            return Save(scene, SceneNames.Game);
        }

        /// <summary>
        /// The pass-the-device panels. The handoff panel is opaque and full-screen on purpose: it is
        /// the privacy boundary that keeps one player's dice and claim off screen from the next.
        /// </summary>
        private static HotSeatOverlayView BuildHotSeatOverlay(Transform canvas)
        {
            var root = UiFactory.Panel(canvas, "HotSeatOverlay");
            var overlay = root.gameObject.AddComponent<HotSeatOverlayView>();

            var handoff = FullScreenPanel(root, "HandoffPanel", _theme.surfaceOverlay);
            var handoffTitle = UiFactory.Label(handoff, "Title", "", new Vector2(0, 220), new Vector2(950, 130), 76f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var handoffBody = UiFactory.Label(handoff, "Body", "", new Vector2(0, 20), new Vector2(900, 280), 38f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var handoffButton = UiFactory.Button(handoff, "ReadyButton", "I have the device",
                new Vector2(0, -260), new Vector2(680, 150));

            var summary = FullScreenPanel(root, "SummaryPanel", UiFactory.WithAlpha(_theme.surfaceOverlay, 0.97f));
            var summaryBody = UiFactory.Label(summary, "Body", "", new Vector2(0, 60), new Vector2(900, 480), 38f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var summaryButton = UiFactory.Button(summary, "NextRoundButton", "Next round",
                new Vector2(0, -300), new Vector2(560, 140));

            var gameOver = FullScreenPanel(root, "GameOverPanel", _theme.surfaceOverlay);
            var gameOverTitle = UiFactory.Label(gameOver, "Title", "Final standings",
                new Vector2(0, 340), new Vector2(900, 120), 69f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var gameOverBody = UiFactory.Label(gameOver, "Body", "", new Vector2(0, 40), new Vector2(900, 500), 48f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            // Every route to this panel is terminal, so it needs an exit; without one the only way
            // off a dead match was force-quitting the app.
            var gameOverButton = UiFactory.Button(gameOver, "BackToMenuButton", "BACK TO MENU",
                new Vector2(0, -400), new Vector2(560, 140));

            SetRef(overlay, "handoffPanel", handoff.gameObject);
            SetRef(overlay, "summaryPanel", summary.gameObject);
            SetRef(overlay, "gameOverPanel", gameOver.gameObject);
            SetRef(overlay, "handoffTitle", handoffTitle);
            SetRef(overlay, "handoffBody", handoffBody);
            SetRef(overlay, "handoffButton", handoffButton);
            SetRef(overlay, "summaryBody", summaryBody);
            SetRef(overlay, "summaryButton", summaryButton);
            SetRef(overlay, "gameOverTitle", gameOverTitle);
            SetRef(overlay, "gameOverBody", gameOverBody);
            SetRef(overlay, "gameOverButton", gameOverButton);

            return overlay;
        }

        private static RectTransform FullScreenPanel(Transform parent, string name, Color color)
        {
            var panel = UiFactory.Panel(parent, name);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = color;
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>Pins to the top edge, <paramref name="y"/> being a negative offset downward.</summary>
        private static T Top<T>(T target, float y) where T : Component
        {
            Anchor(target, new Vector2(0.5f, 1f), y);
            return target;
        }

        /// <summary>Pins to the bottom edge, <paramref name="y"/> being a positive offset upward.</summary>
        /// <summary>Nudges an already-anchored element sideways, keeping its edge anchoring.</summary>
        private static T OffsetX<T>(T target, float x) where T : Component
        {
            var rt = (RectTransform)target.transform;
            rt.anchoredPosition += new Vector2(x, 0f);
            return target;
        }

        private static T Bottom<T>(T target, float y) where T : Component
        {
            Anchor(target, new Vector2(0.5f, 0f), y);
            return target;
        }

        /// <summary>
        /// Anchors at a height *fraction* of the safe area, plus an offset. Edge-pinned bands let
        /// all of a tall phone's extra height pool into one dead gap (found on device, #66's
        /// sibling); a proportional band splits that surplus above and below itself instead.
        /// </summary>
        private static T AtHeight<T>(T target, float fraction, float y = 0f) where T : Component
        {
            Anchor(target, new Vector2(0.5f, fraction), y);
            return target;
        }

        private static RectTransform Anchor(Component target, Vector2 anchor, float y)
        {
            var rt = (RectTransform)target.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            return rt;
        }

        /// <summary>Spaces a row of already-anchored controls evenly about the horizontal centre.</summary>
        private static void Spread(float gap, params Component[] items)
        {
            float start = -gap * (items.Length - 1) / 2f;
            for (int i = 0; i < items.Length; i++)
            {
                var rt = (RectTransform)items[i].transform;
                rt.anchoredPosition = new Vector2(start + i * gap, rt.anchoredPosition.y);
            }
        }

        private static void Spread(Component a, Component b, Component c, float gap) => Spread(gap, a, b, c);

        private static void Row(RectTransform target, float spacing)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void Column(RectTransform target, float spacing)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        /// <summary>
        /// Pins a size for something living inside a layout group. Without this the group asks the
        /// child for its preferred size, a plain Image answers zero, and the row renders empty —
        /// sizeDelta alone is ignored once a layout group is driving the child.
        /// </summary>
        private static void FixedSize(GameObject target, float width, float height)
        {
            var element = target.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minWidth = width;
            element.minHeight = height;
        }

        // ---------------- Building blocks ----------------

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _theme.surfaceBase;
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // new Input System UI module
        }

        private static Canvas CreateCanvas(out RectTransform content)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Match WIDTH only: the app is portrait-locked and every layout is authored against
            // the 1080-unit reference width. A modern iPhone is taller than 16:9 — the 0.5 blend
            // spilled half the aspect difference off the left and right edges on device (#66);
            // matching width keeps 1080 units exactly on-screen and gives tall screens extra
            // vertical room instead.
            scaler.matchWidthOrHeight = 0f;

            // The one tween router every animating view in the scene shares (ui-conventions.md),
            // plus the applier that feeds it the profile's reduced-motion and speed settings.
            go.AddComponent<UiAnimationService>();
            go.AddComponent<UiMotionSettingsApplier>();

            // Safe-area panel that all content lives under.
            content = UiFactory.Panel(go.transform, "SafeArea");
            content.gameObject.AddComponent<SafeAreaFitter>();
            return canvas;
        }

        private static CardButtonView BuildCardButtonTemplate(Transform parent)
        {
            // 5 cards must fit a 1080-wide canvas: 5 x 190 plus four 12pt gaps leaves a little room.
            // Height grew 320 -> 360 when the market band went proportional (UI-character P1):
            // the room exists now, and the extra padding keeps five text runs from feeling packed.
            // Five cards share a 1020-unit band with 12 between them, so a card cannot exceed
            // 194 wide or the outer two hang off the screen — 214 did exactly that. The band is
            // 380 tall. The type below is therefore the largest that fits, not the largest that
            // is readable: at five across, this card cannot reach the 13pt floor, and the way out
            // is a market layout that shows fewer or reshapes the card. See IsTypeWaived.
            const float CardWidth = 192f;
            const float CardHeight = 360f;

            var go = new GameObject("CardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);
            go.GetComponent<Image>().color = _theme.surfaceRaised;
            go.AddComponent<PressableButton>();   // cards press like machine buttons too (P1)

            // A card has to show all three of cost, power and value — a player cannot choose without them.
            var tierLabel = UiFactory.Label(rt, "Tier", "T1", new Vector2(-58, 140), new Vector2(64, 34), 26f);
            var nameLabel = UiFactory.Label(rt, "Name", "Card", new Vector2(0, 104), new Vector2(176, 62), 28f);
            var costLabel = UiFactory.Label(rt, "Cost", "", new Vector2(0, 44), new Vector2(176, 44), 26f);
            var powerLabel = UiFactory.Label(rt, "Power", "", new Vector2(0, -40), new Vector2(176, 132), 24f);
            var pointsLabel = UiFactory.Label(rt, "Points", "0", new Vector2(0, -138), new Vector2(176, 50), 40f);

            FixedSize(go, CardWidth, CardHeight);

            var frame = UiFactory.BlueprintFrame(rt);
            var fade = go.AddComponent<CanvasGroup>();

            // Local-only echo of your own secret pick (handoff 6c, NET-2): a rotated stamp over
            // the whole cell, toggled by CardButtonView.SetCommitted for the observer only.
            var stampGo = new GameObject("CommittedStamp", typeof(RectTransform), typeof(Image));
            var stampRt = (RectTransform)stampGo.transform;
            stampRt.SetParent(rt, false);
            UiFactory.Stretch(stampRt);
            var stampImage = stampGo.GetComponent<Image>();
            stampImage.color = UiFactory.WithAlpha(_theme.Accent(200), 0.88f);
            stampImage.raycastTarget = false;
            var stampLabel = UiFactory.Label(stampRt, "Text", "COMMITTED", Vector2.zero, new Vector2(250, 50), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(800), 0.1f);
            stampLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            stampLabel.raycastTarget = false;
            stampGo.SetActive(false);

            var view = go.AddComponent<CardButtonView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "costText", costLabel);
            SetRef(view, "powerText", powerLabel);
            SetRef(view, "pointsText", pointsLabel);
            SetRef(view, "tierText", tierLabel);
            SetRef(view, "background", go.GetComponent<Image>());
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "frame", frame);
            SetRef(view, "fade", fade);
            SetRef(view, "committedStamp", stampGo);
            SetRef(view, "theme", _theme);

            return view;
        }

        private static PlayerRowView BuildPlayerRowTemplate(Transform parent)
        {
            // A rail cell (handoff 6b): six of these sit side by side in a 1020-wide strip, so the
            // cell is narrow and stacks name / score / holdings / state vertically. The hard
            // constraint is legibility at six players on the narrowest device (UI-1).
            const float CellWidth = 165f;
            const float CellHeight = 180f;

            var go = new GameObject("PlayerRow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(CellWidth, CellHeight);
            FixedSize(go, CellWidth, CellHeight);

            var background = go.GetComponent<Image>();
            background.color = _theme.surfaceRaised;
            var frame = UiFactory.BlueprintFrame(rt, marks: false);

            // Priority marker: a rotated square notch on the top-left corner of the priority
            // holder's cell — shape, not colour, per the accessibility rule.
            var markerGo = new GameObject("PriorityMarker", typeof(RectTransform), typeof(Image));
            var markerRt = (RectTransform)markerGo.transform;
            markerRt.SetParent(rt, false);
            markerRt.anchorMin = markerRt.anchorMax = new Vector2(0f, 1f);
            markerRt.sizeDelta = new Vector2(22, 22);
            markerRt.anchoredPosition = new Vector2(2, -2);
            markerRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var marker = markerGo.GetComponent<Image>();
            marker.color = _theme.accentPriority;
            marker.raycastTarget = false;

            // Four stacked runs in a 150-unit cell, six of these across the phone. At 38f the name
            // truncated to five characters, "THINKING" broke mid-word, and "12d · 10sp" wrapped
            // into its neighbours — so these are the largest that fit the cell, and the cell is
            // what a wider rail would have to change. Waived from the floor for that reason.
            var nameLabel = UiFactory.Label(rt, "Name", "PLAYER", new Vector2(0, 62), new Vector2(150, 34), 26f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var scoreLabel = UiFactory.Label(rt, "Score", "0", new Vector2(0, 16), new Vector2(150, 65), 52f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            var detailLabel = UiFactory.Label(rt, "Detail", "", new Vector2(0, -36), new Vector2(150, 30), 24f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.55f));
            var stateLabel = UiFactory.Label(rt, "State", "", new Vector2(0, -68), new Vector2(150, 30), 24f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);
            // NoWrap needs Ellipsis with it. Without it the overflow just moves sideways into the
            // neighbouring seat — "reconnecting 12s" is 180 units in a 150-unit cell.
            stateLabel.textWrappingMode = TextWrappingModes.NoWrap;
            stateLabel.overflowMode = TextOverflowModes.Ellipsis;
            detailLabel.textWrappingMode = TextWrappingModes.NoWrap;
            detailLabel.overflowMode = TextOverflowModes.Ellipsis;

            var view = go.AddComponent<PlayerRowView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "scoreText", scoreLabel);
            SetRef(view, "detailText", detailLabel);
            SetRef(view, "stateText", stateLabel);
            SetRef(view, "background", background);
            SetRef(view, "frame", frame);
            SetRef(view, "priorityMarker", marker);
            SetRef(view, "theme", _theme);

            return view;
        }

        private static DieView BuildDieTemplate(Transform parent)
        {
            // The handoff's 62px die, square-cornered, pips not digits. The Body child holds
            // everything visible so the selected-state lift moves the visuals while the hit area
            // stays put in the layout row.
            float size = DesignTokens.Px(62);

            var go = new GameObject("Die", typeof(RectTransform), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            FixedSize(go, size, size);

            var body = UiFactory.Panel(rt, "Body");

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.SetParent(body, false);
            UiFactory.Stretch(bgRt);
            var background = bgGo.GetComponent<Image>();
            background.color = _theme.surfaceBase;

            var frame = UiFactory.BlueprintFrame(body, marks: false);
            frame.SetBorderColor(_theme.Accent(700));

            // 3×3 pip grid, centres ±16px (handoff scale) around the middle.
            var pipsRt = UiFactory.Panel(body, "Pips");
            float pipSize = DesignTokens.Px(10);
            float pipStep = DesignTokens.Px(16);
            var pipImages = new Image[9];
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var pipGo = new GameObject($"Pip{row * 3 + col}", typeof(RectTransform), typeof(Image));
                var pipRt = (RectTransform)pipGo.transform;
                pipRt.SetParent(pipsRt, false);
                pipRt.sizeDelta = new Vector2(pipSize, pipSize);
                pipRt.anchoredPosition = new Vector2((col - 1) * pipStep, (1 - row) * pipStep);
                var pipImage = pipGo.GetComponent<Image>();
                pipImage.color = _theme.textPrimary;
                pipImage.raycastTarget = false;
                pipImages[row * 3 + col] = pipImage;
            }
            var pips = pipsRt.gameObject.AddComponent<DiePipGrid>();
            pips.Bind(pipImages);
            pips.SetFace(1);

            var spent = UiFactory.Label(body, "Spent", "SPENT", Vector2.zero, new Vector2(size, 48), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.stateSpent, 0.2f);
            spent.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            spent.gameObject.SetActive(false);

            // Wild faces announce themselves on the die (STORY-3.6 AC3) — a marker, not a
            // colour, so the state survives any palette.
            // A die is 172 units across and its pips own most of that, so this tag cannot carry
            // 13pt without landing on the bottom pip row. Waived with the card and the rail.
            var wild = UiFactory.Label(body, "WildTag", "WILD", Vector2.zero, new Vector2(size, 34), 26f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.18f);
            wild.rectTransform.anchorMin = wild.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            wild.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            wild.gameObject.SetActive(false);

            var view = go.AddComponent<DieView>();
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "body", body);
            SetRef(view, "background", background);
            SetRef(view, "frame", frame);
            SetRef(view, "pips", pips);
            SetRef(view, "spentWatermark", spent.gameObject);
            SetRef(view, "wildTag", wild.gameObject);
            SetRef(view, "theme", _theme);

            return view;
        }

        // ---------------- Utilities ----------------

        private static string Save(UnityEngine.SceneManagement.Scene scene, string name)
        {
            ExpandTouchTargets();
            ValidateInteraction(name);

            var path = $"{SceneDir}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ---------------- interaction validation ----------------

        /// <summary>
        /// Gives every control at least a fingertip to aim at, without redrawing the layout.
        ///
        /// Done as a sweep rather than at fifteen call sites because it is a property of the
        /// screen, not of any one button: the shape row, the face picker and the icon buttons are
        /// all a few units short of 44pt, and the rows they sit in have no vertical room to grow
        /// into. <see cref="ValidateInteraction"/> then checks the result, including that the
        /// enlarged areas do not start stealing from each other.
        /// </summary>
        private static void ExpandTouchTargets()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) return;

            foreach (var selectable in canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                UiFactory.ExpandHitArea(selectable);
        }

        /// <summary>
        /// Catches the two ways a generated screen becomes untappable, at the moment it is
        /// generated rather than on someone's phone:
        ///
        ///  1. <b>A label lying over a control.</b> Every graphic is a raycast target unless told
        ///     otherwise, so decorative text silently swallows taps meant for what is underneath.
        ///     On device this made the menu's offline row respond only around its edges, because
        ///     the full-width status line crossed it.
        ///  2. <b>A control smaller than a fingertip.</b> Apple's minimum is 44pt; see
        ///     <see cref="UiFactory.MinTouchUnits"/> for what that is in layout units.
        ///
        /// Measured against <see cref="RefRes"/> — 1080×1920, the shortest supported screen — which
        /// is where vertical crowding is worst. Rects are computed from the anchors directly
        /// rather than by asking Unity to lay the canvas out, so the check does not depend on the
        /// editor's current game-view size.
        /// </summary>
        private static void ValidateInteraction(string sceneName)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) return;

            var selectables = canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true);
            var graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            int problems = 0;

            foreach (var selectable in selectables)
            {
                if (!selectable.gameObject.activeInHierarchy) continue;
                if (!TryTouchRect(selectable, out var rect)) continue;

                if (rect.width < UiFactory.MinTouchUnits || rect.height < UiFactory.MinTouchUnits)
                {
                    problems++;
                    Debug.LogError($"[Scaffold] {sceneName}: '{Path(selectable.transform)}' is " +
                                   $"{rect.width:0}×{rect.height:0} units, under the " +
                                   $"{UiFactory.MinTouchUnits:0}-unit (44pt) touch minimum. " +
                                   "Enlarge it or call UiFactory.ExpandHitArea.");
                }
            }

            // Hit areas that grew into a neighbour. Deliberately narrow: only pairs whose *drawn*
            // rects are clear of each other but whose *touch* rects are not, which is exactly the
            // damage an enlargement can do.
            //
            // Controls that already overlap when drawn are left alone, because this check cannot
            // tell the two innocent reasons apart from a real one: mutually exclusive states that
            // are never on screen together (the face picker and the done button), and children of
            // a layout group, whose positions are decided at runtime and so all read as identical
            // here.
            for (int i = 0; i < selectables.Length; i++)
            {
                if (!selectables[i].gameObject.activeInHierarchy) continue;
                if (!TryReferenceRect((RectTransform)selectables[i].transform, out var drawnA)) continue;
                if (!TryTouchRect(selectables[i], out var a)) continue;

                for (int j = i + 1; j < selectables.Length; j++)
                {
                    if (!selectables[j].gameObject.activeInHierarchy) continue;
                    if (!TryReferenceRect((RectTransform)selectables[j].transform, out var drawnB)) continue;
                    if (!TryTouchRect(selectables[j], out var b)) continue;

                    if (drawnA.Overlaps(drawnB) || !a.Overlaps(b)) continue;

                    problems++;
                    Debug.LogError($"[Scaffold] {sceneName}: the enlarged touch areas of " +
                                   $"'{Path(selectables[i].transform)}' {Describe(a)} and " +
                                   $"'{Path(selectables[j].transform)}' {Describe(b)} overlap, so " +
                                   "taps in the overlap reach only one of them.");
                }
            }

            // Type that is too small to read, or a box too short for one line of it. The scale
            // came over from a 390px web handoff and landed at 8.6-12pt on device, which is under
            // every iOS system style — so the floor is checked here rather than trusted.
            // Every label, active or not. An earlier version skipped inactive labels outside a
            // control, which quietly excluded the end screen, the card zoom sheet, the reveal
            // theater, the hot-seat overlay and the whole Templates node — including the player
            // row cloned into the live rail. It reported "passed" while seven labels broke the
            // rule it exists to enforce.
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                // The waiver covers the size floor only. A waived label still has to fit its box:
                // exempting it from both is how the last version of this check went quiet.
                if (label.fontSize < DesignTokens.MinReadable)
                {
                    if (IsTypeWaived(label))
                    {
                        Debug.LogWarning($"[Scaffold] {sceneName}: '{Path(label.transform)}' is " +
                                         $"{label.fontSize / DesignTokens.UnitsPerPoint:0.0}pt — under the " +
                                         "readable floor by waiver, because its cell cannot hold 13pt " +
                                         "until the market and rail layouts change.");
                    }
                    else
                    {
                        problems++;
                        Debug.LogError($"[Scaffold] {sceneName}: '{Path(label.transform)}' is set at " +
                                       $"{label.fontSize:0} units ({label.fontSize / DesignTokens.UnitsPerPoint:0.0}pt " +
                                       $"on the narrowest phone), under the {DesignTokens.MinReadable:0}-unit " +
                                       "(13pt) readable minimum.");
                    }
                }

                if (!TryReferenceRect((RectTransform)label.transform, out var labelRect)) continue;

                // 1.2, not 1.15: all four Barlow faces are ascender 1000 / descender -200 with no
                // line gap, so a rendered line is 1.2em. At 1.15 the check blessed 44-unit boxes
                // holding 38-unit type, which are a unit and a half short of an actual line.
                float lineHeight = label.fontSize * 1.2f;

                // Against the authored line count, not against one. A label written with its own
                // newlines needs room for all of them, and checking a single line let a two-line
                // hero tagline pass in a box that fits neither.
                int authoredLines = string.IsNullOrEmpty(label.text)
                    ? 1
                    : label.text.Split('\n').Length;
                float neededHeight = lineHeight * authoredLines;

                if (labelRect.height < neededHeight)
                {
                    problems++;
                    Debug.LogError($"[Scaffold] {sceneName}: '{Path(label.transform)}' has a " +
                                   $"{labelRect.height:0}-unit box for {label.fontSize:0}-unit type — " +
                                   (authoredLines > 1
                                       ? $"{authoredLines} authored lines need {neededHeight:0}."
                                       : $"one line needs {neededHeight:0}."));
                }

                // Text that is already written can be measured, and most of the game's copy is
                // authored right here. Without this the check only ever asked whether a line fits
                // vertically, and never whether the words fit across — which is how a global type
                // raise broke a dozen labels that each individually still "fit".
                if (string.IsNullOrEmpty(label.text)) continue;

                // A glyph the face does not carry renders as a substituted box. The measurement
                // below bails on one, which meant the check went quietest exactly where the text
                // was unreadable — so it is reported here first, by name.
                if (TryFindMissingGlyph(label, out char missing))
                {
                    problems++;
                    Debug.LogError($"[Scaffold] {sceneName}: '{Path(label.transform)}' uses " +
                                   $"U+{(int)missing:X4} ('{missing}'), which {label.font.name} does " +
                                   "not carry — it renders as a substituted box.");
                    continue;
                }

                if (!TryMeasureWidth(label, out float needed)) continue;
                if (needed <= labelRect.width + 1f) continue;

                bool wraps = label.textWrappingMode == TextWrappingModes.Normal;
                bool fitsWrapped = wraps && labelRect.height >= lineHeight * 2f &&
                                   needed <= labelRect.width * 2f;
                if (fitsWrapped) continue;

                problems++;
                Debug.LogError($"[Scaffold] {sceneName}: '{Path(label.transform)}' needs " +
                               $"{needed:0} units for \"{Trim(label.text)}\" in a {labelRect.width:0}-unit " +
                               (wraps ? "box with no room to wrap." : "box and does not wrap."));
            }

            foreach (var graphic in graphics)
            {
                if (!graphic.raycastTarget || !graphic.gameObject.activeInHierarchy) continue;

                // A graphic inside a control is how the control gets drawn, and taps on it bubble
                // to the control. Only outsiders can steal input.
                if (graphic.GetComponentInParent<UnityEngine.UI.Selectable>(true) != null) continue;
                if (!TryReferenceRect((RectTransform)graphic.transform, out var graphicRect)) continue;

                foreach (var selectable in selectables)
                {
                    if (!selectable.gameObject.activeInHierarchy) continue;
                    if (!TryReferenceRect((RectTransform)selectable.transform, out var target)) continue;
                    if (!graphicRect.Overlaps(target)) continue;

                    problems++;
                    Debug.LogError($"[Scaffold] {sceneName}: '{Path(graphic.transform)}' takes " +
                                   $"raycasts and covers '{Path(selectable.transform)}'. Taps in " +
                                   "the overlap go to the graphic. Set raycastTarget = false, or " +
                                   "move it clear.");
                    break;
                }
            }

            if (problems == 0)
                Debug.Log($"[Scaffold] {sceneName}: interaction check passed at {RefRes.x:0}×{RefRes.y:0}.");
        }

        /// <summary>
        /// What a control actually accepts taps in: its own rect, or its HitArea child's if that
        /// is larger. The HitArea is the sanctioned way to keep a control visually small and
        /// still take a thumb.
        /// </summary>
        private static bool TryTouchRect(Component selectable, out Rect rect)
        {
            if (!TryReferenceRect((RectTransform)selectable.transform, out rect)) return false;

            var hit = selectable.transform.Find("HitArea") as RectTransform;
            if (hit != null && TryReferenceRect(hit, out var hitRect))
                rect = new Rect(
                    Mathf.Min(rect.xMin, hitRect.xMin), Mathf.Min(rect.yMin, hitRect.yMin),
                    Mathf.Max(rect.width, hitRect.width), Mathf.Max(rect.height, hitRect.height));

            return true;
        }

        /// <summary>
        /// An element's rect in reference space, resolved from its anchor chain. Walks up to the
        /// canvas, which is treated as exactly <see cref="RefRes"/>.
        /// </summary>
        private static bool TryReferenceRect(RectTransform rt, out Rect rect)
        {
            rect = default;
            if (rt == null) return false;

            Rect parent;
            if (rt.parent is RectTransform parentRt)
            {
                if (!TryReferenceRect(parentRt, out parent)) return false;
            }
            else
            {
                parent = new Rect(0f, 0f, RefRes.x, RefRes.y);
                rect = parent;
                return true;
            }

            float ax0 = parent.xMin + rt.anchorMin.x * parent.width;
            float ax1 = parent.xMin + rt.anchorMax.x * parent.width;
            float ay0 = parent.yMin + rt.anchorMin.y * parent.height;
            float ay1 = parent.yMin + rt.anchorMax.y * parent.height;

            float width = (ax1 - ax0) + rt.sizeDelta.x;
            float height = (ay1 - ay0) + rt.sizeDelta.y;

            float pivotX = Mathf.Lerp(ax0, ax1, rt.pivot.x) + rt.anchoredPosition.x;
            float pivotY = Mathf.Lerp(ay0, ay1, rt.pivot.y) + rt.anchoredPosition.y;

            rect = new Rect(pivotX - width * rt.pivot.x, pivotY - height * rt.pivot.y, width, height);
            return true;
        }

        /// <summary>
        /// The two places the 13pt floor cannot be met without a layout decision, waived
        /// deliberately and loudly rather than by a silent hole in the check.
        ///
        /// The market shows five cards across 1020 units, so a card is 192 wide whatever the type
        /// wants; the opponent rail gives each seat a 165-unit cell, a power chip 250, and a die
        /// face 172 of which the pips own most. All of them need a layout answer
        /// (fewer cards, a scrolling market, a row-shaped card, a taller rail) — that is a
        /// redesign question, not one to settle by shrinking a box here.
        /// </summary>
        private static bool IsTypeWaived(TMP_Text label)
        {
            var path = Path(label.transform);
            return path.Contains("/CardButton/") || path.Contains("/PlayerRow/") ||
                   path.EndsWith("/PowerChip/Text") || path.EndsWith("/WildTag");
        }

        /// <summary>
        /// The first character of a label's text that its own font asset has no glyph for.
        /// Whitespace is exempt — it is laid out, not drawn.
        /// </summary>
        private static bool TryFindMissingGlyph(TMP_Text label, out char missing)
        {
            missing = '\0';

            var font = label.font;
            if (font == null || string.IsNullOrEmpty(label.text)) return false;

            foreach (char c in label.text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (font.characterLookupTable.ContainsKey(c)) continue;

                missing = c;
                return true;
            }

            return false;
        }

        /// <summary>
        /// How wide a label's text actually is, summed from the font asset's own glyph advances.
        ///
        /// TMP's own GetPreferredValues cannot be used here: with no canvas and no laid-out text,
        /// batchmode returns about 27 units for a 32-character string, so a check built on it
        /// passes everything. Reading the advances directly is the only measurement that means
        /// anything at generation time.
        ///
        /// Returns false when the text cannot be measured honestly — an authored newline, or a
        /// glyph the face does not carry — because a guard that guesses is worse than one that
        /// says nothing.
        /// </summary>
        private static bool TryMeasureWidth(TMP_Text label, out float width)
        {
            width = 0f;

            var font = label.font;
            if (font == null || font.faceInfo.pointSize <= 0) return false;

            // Authored newlines are measured, not skipped. Bailing on them disabled this check
            // for precisely the labels that had been re-wrapped by hand to fix an overflow — so
            // none of those fixes were ever verified. The widest line is what has to fit.
            float scale = label.fontSize / font.faceInfo.pointSize;
            foreach (var line in label.text.Split('\n'))
            {
                float lineWidth = 0f;
                foreach (char c in line)
                {
                    if (!font.characterLookupTable.TryGetValue(c, out var character)) return false;
                    lineWidth += character.glyph.metrics.horizontalAdvance * scale;
                }

                // characterSpacing is in font units, em/100 — the convention UiFactory.Label
                // writes it in. It applies between glyphs, not after the last one.
                if (line.Length > 1)
                    lineWidth += label.characterSpacing / 100f * label.fontSize * (line.Length - 1);

                width = Mathf.Max(width, lineWidth);
            }

            return true;
        }

        private static string Trim(string text)
        {
            var flat = text.Replace("\n", " ");
            return flat.Length <= 42 ? flat : flat.Substring(0, 40) + "…";
        }

        private static string Describe(Rect r) =>
            $"(x {r.xMin:0}..{r.xMax:0}, y {r.yMin:0}..{r.yMax:0})";

        private static string Path(Transform t)
        {
            var name = t.name;
            for (var p = t.parent; p != null; p = p.parent) name = p.name + "/" + name;
            return name;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Scaffold] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
