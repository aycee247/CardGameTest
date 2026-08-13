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
        private const string PrefabDir = "Assets/_Project/Prefabs/UI";
        private const string DatabasePath = "Assets/_Project/ScriptableObjects/CardDatabase.asset";
        private static readonly Vector2 RefRes = new Vector2(1080, 1920);

        [MenuItem("Foundry/Generate Scenes & Build Settings")]
        public static void Generate()
        {
            if (!EditorUtility.DisplayDialog("Generate Scenes",
                "This creates/overwrites Boot, MainMenu, Lobby and Game scenes under " + SceneDir +
                " and sets the Build Settings scene list. Continue?", "Generate", "Cancel"))
                return;

            EnsureFolder("Assets/_Project", "Scenes");
            EnsureFolder("Assets/_Project", "Prefabs");
            EnsureFolder("Assets/_Project/Prefabs", "UI");

            var cardButtonPrefab = BuildCardButtonPrefab();
            var diePrefab = BuildDiePrefab();

            var paths = new List<string>
            {
                BuildBootScene(),
                BuildMainMenuScene(),
                BuildLobbyScene(),
                BuildGameScene(cardButtonPrefab, diePrefab),
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
            var status = UiFactory.Label(content, "Status", "", new Vector2(0, -240), new Vector2(900, 80), 36f);
            var codeLabel = UiFactory.Label(content, "JoinCode", "", new Vector2(0, -360), new Vector2(900, 80), 36f);

            var view = content.gameObject.AddComponent<MainMenuView>();
            SetRef(view, "hostButton", host);
            SetRef(view, "joinButton", join);
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

        private static string BuildGameScene(CardButtonView cardButtonPrefab, DieView diePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            // --- status ---
            var round = UiFactory.Label(content, "Round", "", new Vector2(0, 880), new Vector2(1000, 70), 46f);
            var phase = UiFactory.Label(content, "Phase", "", new Vector2(0, 810), new Vector2(1000, 60), 32f);

            // Standings rail. Left-aligned because it is a column of names and numbers, not prose.
            var rail = UiFactory.Label(content, "Rail", "", new Vector2(0, 640), new Vector2(1000, 260), 28f,
                TextAlignmentOptions.TopLeft);

            // --- market ---
            var marketRoot = UiFactory.Panel(content, "Market", stretch: false);
            marketRoot.sizeDelta = new Vector2(1020, 420);
            marketRoot.anchoredPosition = new Vector2(0, 250);
            Row(marketRoot, spacing: 12);

            var sparks = UiFactory.Label(content, "Sparks", "", new Vector2(-280, -20), new Vector2(420, 60), 34f);
            var allowance = UiFactory.Label(content, "Allowance", "", new Vector2(280, -20), new Vector2(520, 60), 30f);

            // --- dice tray ---
            var diceRoot = UiFactory.Panel(content, "DiceTray", stretch: false);
            diceRoot.sizeDelta = new Vector2(1000, 170);
            diceRoot.anchoredPosition = new Vector2(0, -160);
            Row(diceRoot, spacing: 14);

            // Face picker, shown only while dice are selected.
            var faceRoot = UiFactory.Panel(content, "FaceButtons", stretch: false);
            faceRoot.sizeDelta = new Vector2(1000, 110);
            faceRoot.anchoredPosition = new Vector2(0, -300);
            Row(faceRoot, spacing: 10);
            for (int face = 1; face <= 6; face++)
                UiFactory.Button(faceRoot, "Face" + face, face.ToString(), Vector2.zero, new Vector2(140, 100));

            // --- shape controls ---
            var reroll = UiFactory.Button(content, "RerollButton", "Re-roll", new Vector2(-330, -440), new Vector2(300, 120));
            var nudgeUp = UiFactory.Button(content, "NudgeUpButton", "+1", new Vector2(0, -440), new Vector2(240, 120));
            var nudgeDown = UiFactory.Button(content, "NudgeDownButton", "−1", new Vector2(300, -440), new Vector2(240, 120));

            // --- decide controls ---
            var pass = UiFactory.Button(content, "PassButton", "Pass", new Vector2(-330, -600), new Vector2(300, 120));
            var withdraw = UiFactory.Button(content, "WithdrawButton", "Withdraw", new Vector2(0, -600), new Vector2(300, 120));
            var done = UiFactory.Button(content, "DoneButton", "Done", new Vector2(330, -600), new Vector2(300, 120));

            var message = UiFactory.Label(content, "Message", "", new Vector2(0, -740), new Vector2(1000, 90), 32f);

            var hud = content.gameObject.AddComponent<GameHudView>();
            SetRef(hud, "roundLabel", round);
            SetRef(hud, "phaseLabel", phase);
            SetRef(hud, "sparksLabel", sparks);
            SetRef(hud, "allowanceLabel", allowance);
            SetRef(hud, "messageLabel", message);
            SetRef(hud, "railLabel", rail);
            SetRef(hud, "diceRoot", diceRoot);
            SetRef(hud, "marketRoot", marketRoot);
            SetRef(hud, "faceButtonsRoot", faceRoot);
            SetRef(hud, "rerollButton", reroll);
            SetRef(hud, "nudgeUpButton", nudgeUp);
            SetRef(hud, "nudgeDownButton", nudgeDown);
            SetRef(hud, "passButton", pass);
            SetRef(hud, "withdrawButton", withdraw);
            SetRef(hud, "doneButton", done);
            if (cardButtonPrefab != null) SetRef(hud, "cardButtonPrefab", cardButtonPrefab);
            if (diePrefab != null) SetRef(hud, "diePrefab", diePrefab);

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

            // In-scene networked controller (auto-spawns when the host loads this scene via NGO).
            var ctrlGo = new GameObject("GameController");
            ctrlGo.AddComponent<NetworkObject>();
            var controller = ctrlGo.AddComponent<NetworkGameController>();

            // Host-side match orchestration for online play. Disabled by default so the scene opens
            // straight into a hot-seat game; the Lobby flow enables it for a networked match.
            var launcherGo = new GameObject("MatchLauncher");
            launcherGo.SetActive(false);
            var launcher = launcherGo.AddComponent<MatchLauncher>();
            SetRef(launcher, "gameController", controller);
            if (db != null) SetRef(launcher, "cardDatabase", db);

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

            var handoff = FullScreenPanel(root, "HandoffPanel", new Color(0.06f, 0.07f, 0.10f, 1f));
            var handoffTitle = UiFactory.Label(handoff, "Title", "", new Vector2(0, 220), new Vector2(950, 130), 68f);
            var handoffBody = UiFactory.Label(handoff, "Body", "", new Vector2(0, 20), new Vector2(900, 280), 36f);
            var handoffButton = UiFactory.Button(handoff, "ReadyButton", "I have the device",
                new Vector2(0, -260), new Vector2(680, 150));

            var reveal = FullScreenPanel(root, "RevealPanel", new Color(0.07f, 0.09f, 0.13f, 0.97f));
            UiFactory.Label(reveal, "Title", "Reveal", new Vector2(0, 320), new Vector2(900, 110), 62f);
            var revealBody = UiFactory.Label(reveal, "Body", "", new Vector2(0, 40), new Vector2(900, 420), 34f);
            var revealButton = UiFactory.Button(reveal, "ContinueButton", "Continue",
                new Vector2(0, -300), new Vector2(560, 140));

            var summary = FullScreenPanel(root, "SummaryPanel", new Color(0.07f, 0.09f, 0.13f, 0.97f));
            var summaryBody = UiFactory.Label(summary, "Body", "", new Vector2(0, 60), new Vector2(900, 480), 34f);
            var summaryButton = UiFactory.Button(summary, "NextRoundButton", "Next round",
                new Vector2(0, -300), new Vector2(560, 140));

            var gameOver = FullScreenPanel(root, "GameOverPanel", new Color(0.06f, 0.07f, 0.10f, 1f));
            UiFactory.Label(gameOver, "Title", "Final standings", new Vector2(0, 340), new Vector2(900, 120), 62f);
            var gameOverBody = UiFactory.Label(gameOver, "Body", "", new Vector2(0, 40), new Vector2(900, 500), 38f);

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

        private static void Row(RectTransform target, float spacing)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        // ---------------- Building blocks ----------------

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.14f, 1f);
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

        private static CardButtonView BuildCardButtonPrefab()
        {
            var go = new GameObject("CardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220, 320);
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 1f);

            // A card has to show all three of cost, power and value — a player cannot choose without them.
            var tierLabel = UiFactory.Label(rt, "Tier", "T1", new Vector2(-80, 138), new Vector2(60, 40), 22f);
            var nameLabel = UiFactory.Label(rt, "Name", "Card", new Vector2(0, 108), new Vector2(200, 56), 26f);
            var costLabel = UiFactory.Label(rt, "Cost", "", new Vector2(0, 44), new Vector2(200, 60), 22f);
            var powerLabel = UiFactory.Label(rt, "Power", "", new Vector2(0, -30), new Vector2(200, 110), 21f);
            var pointsLabel = UiFactory.Label(rt, "Points", "0", new Vector2(0, -128), new Vector2(200, 56), 34f);

            var view = go.AddComponent<CardButtonView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "costText", costLabel);
            SetRef(view, "powerText", powerLabel);
            SetRef(view, "pointsText", pointsLabel);
            SetRef(view, "tierText", tierLabel);
            SetRef(view, "background", go.GetComponent<Image>());
            SetRef(view, "button", go.GetComponent<Button>());

            var path = PrefabDir + "/CardButton.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab != null ? prefab.GetComponent<CardButtonView>() : null;
        }

        private static DieView BuildDiePrefab()
        {
            var go = new GameObject("Die", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(140, 140);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.96f, 0.96f, 0.94f, 1f);

            var faceLabel = UiFactory.Label(rt, "Face", "1", Vector2.zero, new Vector2(130, 130), 72f);
            faceLabel.color = new Color(0.09f, 0.11f, 0.15f, 1f);

            var view = go.AddComponent<DieView>();
            SetRef(view, "faceText", faceLabel);
            SetRef(view, "background", image);
            SetRef(view, "button", go.GetComponent<Button>());

            var path = PrefabDir + "/Die.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab != null ? prefab.GetComponent<DieView>() : null;
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
