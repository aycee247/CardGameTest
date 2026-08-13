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
    /// Menu: <b>DiceCards ▸ Generate Scenes &amp; Build Settings</b>.
    /// Run <b>DiceCards ▸ Generate Sample Content</b> first so the Game scene can bind a CardDatabase.
    /// </summary>
    public static class SceneScaffolder
    {
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string PrefabDir = "Assets/_Project/Prefabs/UI";
        private const string DatabasePath = "Assets/_Project/ScriptableObjects/CardDatabase.asset";
        private static readonly Vector2 RefRes = new Vector2(1080, 1920);

        [MenuItem("DiceCards/Generate Scenes & Build Settings")]
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

            var paths = new List<string>
            {
                BuildBootScene(),
                BuildMainMenuScene(),
                BuildLobbyScene(),
                BuildGameScene(cardButtonPrefab),
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

        private static string BuildGameScene(CardButtonView cardButtonPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas(out var content);

            var turn = UiFactory.Label(content, "Turn", "", new Vector2(0, 820), new Vector2(1000, 80), 48f);
            var phase = UiFactory.Label(content, "Phase", "", new Vector2(0, 740), new Vector2(1000, 60), 32f);
            var rolls = UiFactory.Label(content, "Rolls", "", new Vector2(0, 680), new Vector2(1000, 60), 32f);
            var score = UiFactory.Label(content, "Score", "", new Vector2(0, 620), new Vector2(1000, 60), 34f);
            var dice = UiFactory.Label(content, "Dice", "Dice: —", new Vector2(0, -560), new Vector2(1000, 90), 54f);
            var message = UiFactory.Label(content, "Message", "", new Vector2(0, -680), new Vector2(1000, 70), 34f);

            // Market row.
            var marketRoot = UiFactory.Panel(content, "Market", stretch: false);
            marketRoot.sizeDelta = new Vector2(1000, 460);
            marketRoot.anchoredPosition = new Vector2(0, 60);
            var layout = marketRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;

            var roll = UiFactory.Button(content, "RollButton", "Roll", new Vector2(-160, -820), new Vector2(300, 130));
            var endTurn = UiFactory.Button(content, "EndTurnButton", "End Turn", new Vector2(200, -820), new Vector2(320, 130));

            var hud = content.gameObject.AddComponent<GameHudView>();
            SetRef(hud, "turnLabel", turn);
            SetRef(hud, "phaseLabel", phase);
            SetRef(hud, "rollsLabel", rolls);
            SetRef(hud, "scoreLabel", score);
            SetRef(hud, "diceLabel", dice);
            SetRef(hud, "messageLabel", message);
            SetRef(hud, "rollButton", roll);
            SetRef(hud, "endTurnButton", endTurn);
            SetRef(hud, "marketRoot", marketRoot);
            if (cardButtonPrefab != null) SetRef(hud, "cardButtonPrefab", cardButtonPrefab);

            var presenter = canvas.gameObject.AddComponent<GameHudPresenter>();
            SetRef(presenter, "view", hud);

            // In-scene networked controller (auto-spawns when the host loads this scene via NGO).
            var ctrlGo = new GameObject("GameController");
            ctrlGo.AddComponent<NetworkObject>();
            var controller = ctrlGo.AddComponent<NetworkGameController>();

            // Host-side match orchestration.
            var launcherGo = new GameObject("MatchLauncher");
            var launcher = launcherGo.AddComponent<MatchLauncher>();
            SetRef(launcher, "gameController", controller);
            var db = AssetDatabase.LoadAssetAtPath<CardDatabase>(DatabasePath);
            if (db != null) SetRef(launcher, "cardDatabase", db);
            else Debug.LogWarning("[Scaffold] CardDatabase not found — run 'DiceCards ▸ Generate Sample Content' and re-run, or assign it on MatchLauncher.");

            return Save(scene, SceneNames.Game);
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

            var nameLabel = UiFactory.Label(rt, "Name", "Card", new Vector2(0, 120), new Vector2(200, 60), 30f);
            var reqLabel = UiFactory.Label(rt, "Requirement", "", new Vector2(0, 0), new Vector2(200, 120), 24f);
            var pointsLabel = UiFactory.Label(rt, "Points", "0", new Vector2(0, -120), new Vector2(200, 60), 34f);

            var view = go.AddComponent<CardButtonView>();
            SetRef(view, "nameText", nameLabel);
            SetRef(view, "requirementText", reqLabel);
            SetRef(view, "pointsText", pointsLabel);
            SetRef(view, "button", go.GetComponent<Button>());

            var path = PrefabDir + "/CardButton.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return prefab != null ? prefab.GetComponent<CardButtonView>() : null;
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
