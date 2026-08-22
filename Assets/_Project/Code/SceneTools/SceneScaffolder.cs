using System.Collections.Generic;
using Game.App;
using Game.Audio;
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
        public static void Generate()
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
            _theme = AssetDatabase.LoadAssetAtPath<ThemeAsset>(ThemeGenerator.ThemePath);
            if (_theme == null)
            {
                Debug.LogError($"[Scaffold] No theme at {ThemeGenerator.ThemePath} — run " +
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

            CreateCanvas(out var content);
            UiFactory.Label(content, "Title", "Dice Cards", new Vector2(0, 200), new Vector2(900, 120), 72f);
            UiFactory.Label(content, "Loading", "Loading…", Vector2.zero, new Vector2(900, 80), 40f);

            return Save(scene, SceneNames.Boot);
        }

        private static string BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            UiFactory.Label(content, "Title", "Dice Cards", new Vector2(0, 700), new Vector2(900, 120), 72f);

            var host = UiFactory.Button(content, "HostButton", "Host Game", new Vector2(0, 300), new Vector2(560, 130));
            var codeInput = UiFactory.InputField(content, "JoinCodeInput", "Enter join code", new Vector2(0, 120), new Vector2(560, 110));
            var join = UiFactory.Button(content, "JoinButton", "Join Game", new Vector2(0, -40), new Vector2(560, 130));
            var passPlay = UiFactory.Button(content, "PassPlayButton", "Pass & Play", new Vector2(0, -220), new Vector2(560, 130));
            var status = UiFactory.Label(content, "Status", "", new Vector2(0, -400), new Vector2(900, 80), 36f);
            var codeLabel = UiFactory.Label(content, "JoinCode", "", new Vector2(0, -520), new Vector2(900, 80), 36f);

            var view = content.gameObject.AddComponent<MainMenuView>();
            SetRef(view, "hostButton", host);
            SetRef(view, "joinButton", join);
            SetRef(view, "passPlayButton", passPlay);
            SetRef(view, "joinCodeInput", codeInput);
            SetRef(view, "statusLabel", status);
            SetRef(view, "joinCodeLabel", codeLabel);

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            SetRef(controller, "view", view);

            return Save(scene, SceneNames.MainMenu);
        }

        private static string BuildLobbyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            UiFactory.Label(content, "Title", "Lobby", new Vector2(0, 700), new Vector2(900, 120), 72f);
            var codeLabel = UiFactory.Label(content, "JoinCode", "", new Vector2(0, 400), new Vector2(900, 90), 48f);
            var status = UiFactory.Label(content, "Status", "", new Vector2(0, 250), new Vector2(900, 80), 36f);
            var start = UiFactory.Button(content, "StartButton", "Start Match", new Vector2(0, 0), new Vector2(560, 130));

            var view = content.gameObject.AddComponent<LobbyView>();
            SetRef(view, "codeLabel", codeLabel);
            SetRef(view, "statusLabel", status);
            SetRef(view, "startButton", start);

            var controller = canvas.gameObject.AddComponent<LobbyController>();
            SetRef(controller, "view", view);

            return Save(scene, SceneNames.Lobby);
        }

        private static string BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            // Everything anchors to the top or bottom edge rather than to the centre. Absolute
            // offsets from centre only work at the exact reference aspect: on a wider, shorter view
            // the outermost rows simply fall off the screen.

            // --- status, from the top down ---
            var round = Top(UiFactory.Label(content, "Round", "", Vector2.zero, new Vector2(1000, 70), 46f), -50);
            var phase = Top(UiFactory.Label(content, "Phase", "", Vector2.zero, new Vector2(1000, 60), 32f), -115);

            // Online only: hidden whenever there is no phase clock (UI-2).
            var timer = Top(UiFactory.Label(content, "Timer", "", new Vector2(430, 0), new Vector2(180, 70), 44f), -50);

            // Standings rail: one row per player, stacked. Sized for six on the narrowest device.
            var railRoot = UiFactory.Panel(content, "Rail", stretch: false);
            railRoot.sizeDelta = new Vector2(1000, 300);
            Top(railRoot, -300);
            Column(railRoot, spacing: 6);

            var marketRoot = UiFactory.Panel(content, "Market", stretch: false);
            marketRoot.sizeDelta = new Vector2(1020, 400);
            Top(marketRoot, -640);
            Row(marketRoot, spacing: 12);

            // --- controls, from the bottom up ---
            var message = Bottom(UiFactory.Label(content, "Message", "", Vector2.zero, new Vector2(1000, 80), 32f), 45);

            var pass = Bottom(UiFactory.Button(content, "PassButton", "Pass", Vector2.zero, new Vector2(300, 120)), 150);
            var withdraw = Bottom(UiFactory.Button(content, "WithdrawButton", "Withdraw", Vector2.zero, new Vector2(300, 120)), 150);
            var done = Bottom(UiFactory.Button(content, "DoneButton", "Done", Vector2.zero, new Vector2(300, 120)), 150);
            Spread(pass, withdraw, done, gap: 320);

            var reroll = Bottom(UiFactory.Button(content, "RerollButton", "Re-roll", Vector2.zero, new Vector2(300, 120)), 285);
            var nudgeUp = Bottom(UiFactory.Button(content, "NudgeUpButton", "+1", Vector2.zero, new Vector2(300, 120)), 285);
            var nudgeDown = Bottom(UiFactory.Button(content, "NudgeDownButton", "-1", Vector2.zero, new Vector2(300, 120)), 285);
            Spread(reroll, nudgeUp, nudgeDown, gap: 320);

            // Face picker, shown only while dice are selected.
            var faceRoot = UiFactory.Panel(content, "FaceButtons", stretch: false);
            faceRoot.sizeDelta = new Vector2(1000, 110);
            Bottom(faceRoot, 410);
            Row(faceRoot, spacing: 10);
            for (int face = 1; face <= 6; face++)
            {
                var faceButton = UiFactory.Button(faceRoot, "Face" + face, face.ToString(), Vector2.zero, new Vector2(140, 100));
                FixedSize(faceButton.gameObject, 140, 100);
            }

            var diceRoot = UiFactory.Panel(content, "DiceTray", stretch: false);
            diceRoot.sizeDelta = new Vector2(1000, 170);
            Bottom(diceRoot, 555);
            Row(diceRoot, spacing: 14);

            var sparks = Bottom(UiFactory.Label(content, "Sparks", "", new Vector2(-280, 0), new Vector2(420, 60), 34f), 680);
            var allowance = Bottom(UiFactory.Label(content, "Allowance", "", new Vector2(280, 0), new Vector2(520, 60), 30f), 680);

            // Row templates the HUD clones from. These are scene objects rather than prefab assets:
            // an asset reference assigned here did not survive being saved out, leaving the board
            // with no dice and no market, while every scene-to-scene reference serialised fine.
            // They live under a deactivated parent, so the templates themselves never render but
            // their clones are active the moment they are parented into a live row.
            var templates = UiFactory.Panel(canvas.transform, "Templates");
            var cardButtonTemplate = BuildCardButtonTemplate(templates);
            var dieTemplate = BuildDieTemplate(templates);
            var playerRowTemplate = BuildPlayerRowTemplate(templates);
            templates.gameObject.SetActive(false);

            var hud = content.gameObject.AddComponent<GameHudView>();
            SetRef(hud, "roundLabel", round);
            SetRef(hud, "phaseLabel", phase);
            SetRef(hud, "sparksLabel", sparks);
            SetRef(hud, "allowanceLabel", allowance);
            SetRef(hud, "messageLabel", message);
            SetRef(hud, "timerLabel", timer);
            SetRef(hud, "railRoot", railRoot);
            SetRef(hud, "playerRowPrefab", playerRowTemplate);
            SetRef(hud, "diceRoot", diceRoot);
            SetRef(hud, "marketRoot", marketRoot);
            SetRef(hud, "faceButtonsRoot", faceRoot);
            SetRef(hud, "rerollButton", reroll);
            SetRef(hud, "nudgeUpButton", nudgeUp);
            SetRef(hud, "nudgeDownButton", nudgeDown);
            SetRef(hud, "passButton", pass);
            SetRef(hud, "withdrawButton", withdraw);
            SetRef(hud, "doneButton", done);
            SetRef(hud, "cardButtonPrefab", cardButtonTemplate);
            SetRef(hud, "diePrefab", dieTemplate);

            var presenter = canvas.gameObject.AddComponent<GameHudPresenter>();
            SetRef(presenter, "view", hud);

            var overlay = BuildHotSeatOverlay(canvas.transform);

            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (db == null)
                Debug.LogWarning("[Scaffold] CardDatabase not found — run 'Foundry ▸ Generate Starter Deck' " +
                                 "and re-run, or assign it on HotSeatHost and MatchLauncher.");

            // Hot-seat host: playable immediately, with no networking involved.
            var hotSeatGo = new GameObject("HotSeatHost");
            var hotSeat = hotSeatGo.AddComponent<HotSeatHost>();
            SetRef(hotSeat, "presenter", presenter);
            SetRef(hotSeat, "overlay", overlay);
            if (db != null) SetRef(hotSeat, "cardDatabase", db);

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
            var mode = modeGo.AddComponent<GameSceneBootstrap>();
            SetRef(mode, "presenter", presenter);
            SetRef(mode, "hotSeatHost", hotSeat);
            SetRef(mode, "hotSeatOverlay", overlay);
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
            var handoffTitle = UiFactory.Label(handoff, "Title", "", new Vector2(0, 220), new Vector2(950, 130), 68f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var handoffBody = UiFactory.Label(handoff, "Body", "", new Vector2(0, 20), new Vector2(900, 280), 36f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var handoffButton = UiFactory.Button(handoff, "ReadyButton", "I have the device",
                new Vector2(0, -260), new Vector2(680, 150));

            var reveal = FullScreenPanel(root, "RevealPanel", UiFactory.WithAlpha(_theme.surfaceOverlay, 0.97f));
            UiFactory.Label(reveal, "Title", "Reveal", new Vector2(0, 320), new Vector2(900, 110), 62f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var revealBody = UiFactory.Label(reveal, "Body", "", new Vector2(0, 40), new Vector2(900, 420), 34f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var revealButton = UiFactory.Button(reveal, "ContinueButton", "Continue",
                new Vector2(0, -300), new Vector2(560, 140));

            var summary = FullScreenPanel(root, "SummaryPanel", UiFactory.WithAlpha(_theme.surfaceOverlay, 0.97f));
            var summaryBody = UiFactory.Label(summary, "Body", "", new Vector2(0, 60), new Vector2(900, 480), 34f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);
            var summaryButton = UiFactory.Button(summary, "NextRoundButton", "Next round",
                new Vector2(0, -300), new Vector2(560, 140));

            var gameOver = FullScreenPanel(root, "GameOverPanel", _theme.surfaceOverlay);
            UiFactory.Label(gameOver, "Title", "Final standings", new Vector2(0, 340), new Vector2(900, 120), 62f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, _theme.textInverse);
            var gameOverBody = UiFactory.Label(gameOver, "Body", "", new Vector2(0, 40), new Vector2(900, 500), 38f,
                TextAlignmentOptions.Center, FontRole.Body, _theme.textInverse);

            SetRef(overlay, "handoffPanel", handoff.gameObject);
            SetRef(overlay, "revealPanel", reveal.gameObject);
            SetRef(overlay, "summaryPanel", summary.gameObject);
            SetRef(overlay, "gameOverPanel", gameOver.gameObject);
            SetRef(overlay, "handoffTitle", handoffTitle);
            SetRef(overlay, "handoffBody", handoffBody);
            SetRef(overlay, "handoffButton", handoffButton);
            SetRef(overlay, "revealBody", revealBody);
            SetRef(overlay, "revealButton", revealButton);
            SetRef(overlay, "summaryBody", summaryBody);
            SetRef(overlay, "summaryButton", summaryButton);
            SetRef(overlay, "gameOverBody", gameOverBody);

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
        private static T Bottom<T>(T target, float y) where T : Component
        {
            Anchor(target, new Vector2(0.5f, 0f), y);
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
            scaler.matchWidthOrHeight = 0.5f;

            // Safe-area panel that all content lives under.
            content = UiFactory.Panel(go.transform, "SafeArea");
            content.gameObject.AddComponent<SafeAreaFitter>();
            return canvas;
        }

        private static CardButtonView BuildCardButtonTemplate(Transform parent)
        {
            // 5 cards must fit a 1080-wide canvas: 5 x 190 plus four 12pt gaps leaves a little room.
            const float CardWidth = 190f;
            const float CardHeight = 320f;

            var go = new GameObject("CardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);
            go.GetComponent<Image>().color = _theme.surfaceRaised;

            // A card has to show all three of cost, power and value — a player cannot choose without them.
            var tierLabel = UiFactory.Label(rt, "Tier", "T1", new Vector2(-64, 138), new Vector2(56, 40), 22f);
            var nameLabel = UiFactory.Label(rt, "Name", "Card", new Vector2(0, 106), new Vector2(174, 56), 24f);
            var costLabel = UiFactory.Label(rt, "Cost", "", new Vector2(0, 46), new Vector2(174, 56), 21f);
            var powerLabel = UiFactory.Label(rt, "Power", "", new Vector2(0, -28), new Vector2(174, 112), 19f);
            var pointsLabel = UiFactory.Label(rt, "Points", "0", new Vector2(0, -126), new Vector2(174, 56), 34f);

            FixedSize(go, CardWidth, CardHeight);

            var view = go.AddComponent<CardButtonView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "costText", costLabel);
            SetRef(view, "powerText", powerLabel);
            SetRef(view, "pointsText", pointsLabel);
            SetRef(view, "tierText", tierLabel);
            SetRef(view, "background", go.GetComponent<Image>());
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "theme", _theme);

            return view;
        }

        private static PlayerRowView BuildPlayerRowTemplate(Transform parent)
        {
            // Six of these have to stack legibly, so the row is short and reads left to right:
            // who, how well they are doing, what they hold, and whether they have acted.
            const float RowWidth = 1000f;
            const float RowHeight = 46f;

            var go = new GameObject("PlayerRow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(RowWidth, RowHeight);
            FixedSize(go, RowWidth, RowHeight);

            var background = go.GetComponent<Image>();
            background.color = _theme.surfaceRaised;

            // A thin bar marking who currently holds first pick.
            var markerGo = new GameObject("PriorityMarker", typeof(RectTransform), typeof(Image));
            var markerRt = (RectTransform)markerGo.transform;
            markerRt.SetParent(rt, false);
            markerRt.sizeDelta = new Vector2(8, RowHeight);
            markerRt.anchoredPosition = new Vector2(-(RowWidth / 2f) + 4, 0);
            var marker = markerGo.GetComponent<Image>();
            marker.color = _theme.accentPriority;

            var nameLabel = UiFactory.Label(rt, "Name", "Player", new Vector2(-310, 0), new Vector2(320, 40), 24f,
                TextAlignmentOptions.Left);
            var scoreLabel = UiFactory.Label(rt, "Score", "0", new Vector2(-110, 0), new Vector2(90, 40), 28f);
            var detailLabel = UiFactory.Label(rt, "Detail", "", new Vector2(60, 0), new Vector2(240, 40), 22f);
            var stateLabel = UiFactory.Label(rt, "State", "", new Vector2(340, 0), new Vector2(300, 40), 22f,
                TextAlignmentOptions.Right);

            var view = go.AddComponent<PlayerRowView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "scoreText", scoreLabel);
            SetRef(view, "detailText", detailLabel);
            SetRef(view, "stateText", stateLabel);
            SetRef(view, "background", background);
            SetRef(view, "priorityMarker", marker);
            SetRef(view, "theme", _theme);

            return view;
        }

        private static DieView BuildDieTemplate(Transform parent)
        {
            var go = new GameObject("Die", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(140, 140);

            var image = go.GetComponent<Image>();
            image.color = _theme.surfaceBase;

            var faceLabel = UiFactory.Label(rt, "Face", "1", Vector2.zero, new Vector2(130, 130), 72f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);

            FixedSize(go, 140, 140);

            var view = go.AddComponent<DieView>();
            SetRef(view, "faceText", faceLabel);
            SetRef(view, "background", image);
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "theme", _theme);

            return view;
        }

        // ---------------- Utilities ----------------

        private static string Save(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var path = $"{SceneDir}/{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
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
