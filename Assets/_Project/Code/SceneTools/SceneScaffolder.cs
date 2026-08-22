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

            // ---- Hero: the blueprint-framed identity block (handoff §1) ----
            var hero = UiFactory.Panel(content, "Hero", stretch: false);
            hero.sizeDelta = new Vector2(970, 780);
            hero.anchoredPosition = new Vector2(0, 300);
            UiFactory.BlueprintFrame(hero, FrameEmphasis.Accent);

            UiFactory.Label(hero, "Eyebrow", "SIMULTANEOUS DICE ENGINE BUILDER",
                new Vector2(0, 300), new Vector2(900, 44), 28f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            UiFactory.Label(hero, "Wordmark", "FOUNDRY", new Vector2(0, 130), new Vector2(940, 230), 190f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            MiniDieGlyph(hero, "GlyphFive", 5, new Vector2(-390, -70));
            MiniDieGlyph(hero, "GlyphSix", 6, new Vector2(-300, -70));
            UiFactory.Label(hero, "Meta", "2–6 PLAYERS · 10 ROUNDS\n≈ 12 MINUTES",
                new Vector2(230, -80), new Vector2(460, 90), 30f,
                TextAlignmentOptions.Right, FontRole.BodyMedium, Muted(0.55f), 0.08f);
            UiFactory.Label(hero, "BodyCopy",
                "Roll together, shape your dice, commit in secret,\nthen claim the market's machines.",
                new Vector2(0, -270), new Vector2(880, 100), 36f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.7f));

            // ---- Actions: one solid-accent primary, everything else recedes ----
            var host = UiFactory.Button(content, "HostButton", "HOST MATCH",
                new Vector2(0, -330), new Vector2(970, 144));
            UiFactory.BlueprintFrame((RectTransform)host.transform, FrameEmphasis.AccentStrong);

            var codeInput = UiFactory.InputField(content, "JoinCodeInput", "Enter join code",
                new Vector2(0, -500), new Vector2(970, 120));
            var join = UiFactory.Button(content, "JoinButton", "JOIN WITH CODE",
                new Vector2(0, -660), new Vector2(970, 144), ButtonStyle.Secondary);

            // Kept beyond the handoff: the only offline path in the build.
            var passPlay = UiFactory.Button(content, "PassPlayButton", "PASS & PLAY — OFFLINE",
                new Vector2(0, -830), new Vector2(970, 144), ButtonStyle.Ghost);
            UiFactory.BlueprintFrame((RectTransform)passPlay.transform, marks: false);

            var status = Bottom(UiFactory.Label(content, "Status", "", Vector2.zero,
                new Vector2(970, 70), 32f, TextAlignmentOptions.Center, FontRole.Body, Muted(0.8f)), 200);
            var codeLabel = Bottom(UiFactory.Label(content, "JoinCode", "", Vector2.zero,
                new Vector2(970, 60), 32f, TextAlignmentOptions.Center, FontRole.BodySemibold), 130);
            Bottom(UiFactory.Label(content, "Footer", "REV 0.1 · ONLINE DEMO", Vector2.zero,
                new Vector2(900, 44), 26f, TextAlignmentOptions.Center, FontRole.BodyMedium, Muted(0.4f), 0.18f), 55);

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
            UiFactory.Label(tag, "Text", "FRIENDS BY CODE", Vector2.zero, new Vector2(380, 60), 26f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.14f);

            // ---- Code panel: the thing you read out loud ----
            var codePanel = UiFactory.Panel(content, "CodePanel", stretch: false);
            codePanel.sizeDelta = new Vector2(970, 440);
            Top(codePanel, -450);
            UiFactory.BlueprintFrame(codePanel, FrameEmphasis.Accent);
            UiFactory.Label(codePanel, "Eyebrow", "JOIN CODE", new Vector2(0, 150), new Vector2(900, 44), 28f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(700), 0.22f);
            var codeLabel = UiFactory.Label(codePanel, "Code", "—", new Vector2(0, 10), new Vector2(920, 180), 150f,
                TextAlignmentOptions.Center, FontRole.HeadingBold, null, 0.12f);
            var status = UiFactory.Label(codePanel, "Caption", "", new Vector2(0, -150), new Vector2(900, 60), 33f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.55f));

            // ---- Seats ----
            Top(UiFactory.Label(content, "SeatsLabel", "SEATS",
                new Vector2(-430, 0), new Vector2(200, 44), 30f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.55f), 0.14f), -730);
            var seatsCount = Top(UiFactory.Label(content, "SeatsCount", "0 / 6",
                new Vector2(430, 0), new Vector2(200, 44), 30f,
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

            var label = UiFactory.Label(rt, "Label", "—", new Vector2(0, 22), new Vector2(size - 20, 56), 38f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);
            var seconds = UiFactory.Label(rt, "Seconds", "", new Vector2(0, -44), new Vector2(size - 20, 56), 47f,
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
            rt.sizeDelta = new Vector2(250, 56);
            FixedSize(go, 250, 56);
            go.GetComponent<Image>().color = _theme.Accent(100);

            var label = UiFactory.Label(rt, "Text", "POWER", Vector2.zero, new Vector2(240, 48), 24f,
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
            var avatarInitial = UiFactory.Label(avatarRt, "Initial", "P", Vector2.zero, new Vector2(70, 70), 36f,
                TextAlignmentOptions.Center, FontRole.Heading);

            var nameLabel = UiFactory.Label(rt, "Name", "", new Vector2(-40, 0), new Vector2(620, 60), 38f,
                TextAlignmentOptions.Left, FontRole.BodySemibold);
            var chipLabel = UiFactory.Label(rt, "Chip", "", new Vector2(380, 0), new Vector2(180, 44), 28f,
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

            // Everything anchors to the top or bottom edge rather than to the centre. Absolute
            // offsets from centre only work at the exact reference aspect: on a wider, shorter view
            // the outermost rows simply fall off the screen.

            // --- header row (handoff 6a): round + phase left, the Sparks tag right ---
            var round = Top(UiFactory.Label(content, "Round", "", new Vector2(-280, 0), new Vector2(480, 70), 61f,
                TextAlignmentOptions.Left, FontRole.HeadingBold), -60);
            var phase = Top(UiFactory.Label(content, "Phase", "", new Vector2(-280, 0), new Vector2(480, 40), 28f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, _theme.Accent(700), 0.16f), -130);

            var sparksChipGo = new GameObject("SparksChip", typeof(RectTransform), typeof(Image));
            var sparksChip = (RectTransform)sparksChipGo.transform;
            sparksChip.SetParent(content, false);
            sparksChip.sizeDelta = new Vector2(330, 76);
            Top(sparksChip, -78);
            sparksChip.anchoredPosition = new Vector2(340, sparksChip.anchoredPosition.y);
            sparksChipGo.GetComponent<Image>().color = _theme.Accent(100);
            var sparks = UiFactory.Label(sparksChip, "Value", "", Vector2.zero, new Vector2(320, 66), 30f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.Accent(800), 0.06f);

            // Opponent rail (UI-1, handoff 6b): a horizontal strip, one cell per player, seat order.
            var railRoot = UiFactory.Panel(content, "Rail", stretch: false);
            railRoot.sizeDelta = new Vector2(1020, 180);
            Top(railRoot, -310);
            Row(railRoot, spacing: 6);

            // Market label row + 5-card band (handoff 6c).
            Top(UiFactory.Label(content, "MarketLabel", "MARKET", new Vector2(-430, 0), new Vector2(220, 40), 28f,
                TextAlignmentOptions.Left, FontRole.BodySemibold, Muted(0.8f), 0.2f), -440);
            var marketMeta = Top(UiFactory.Label(content, "MarketMeta", "", new Vector2(150, 0), new Vector2(720, 40), 26f,
                TextAlignmentOptions.Right, FontRole.BodyMedium, Muted(0.55f), 0.06f), -440);

            var marketRoot = UiFactory.Panel(content, "Market", stretch: false);
            marketRoot.sizeDelta = new Vector2(1020, 340);
            Top(marketRoot, -650);
            Row(marketRoot, spacing: 12);

            // --- controls, from the bottom up (handoff 6d-6g) ---
            var message = Bottom(UiFactory.Label(content, "Message", "", new Vector2(0, 0), new Vector2(1000, 76), 30f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.8f)), 45);

            // Bottom action bar: conditional ghosts left, the Done/timer square right (6g).
            var withdraw = Bottom(UiFactory.Button(content, "WithdrawButton", "WITHDRAW",
                new Vector2(-380, 0), new Vector2(290, 110), ButtonStyle.Ghost), 190);
            var pass = Bottom(UiFactory.Button(content, "PassButton", "PASS THIS ROUND",
                new Vector2(-55, 0), new Vector2(340, 110), ButtonStyle.Ghost), 190);
            var doneTimer = BuildDoneTimer(content, canvas);

            // Shape row ⇄ face picker share one band (6f / 6f-alt).
            var reroll = Bottom(UiFactory.Button(content, "RerollButton", "RE-ROLL",
                new Vector2(-370, 0), new Vector2(310, 110), ButtonStyle.Secondary), 350);
            var nudgeDown = Bottom(UiFactory.Button(content, "NudgeDownButton", "−1",
                new Vector2(-125, 0), new Vector2(140, 110), ButtonStyle.Secondary), 350);
            var nudgeUp = Bottom(UiFactory.Button(content, "NudgeUpButton", "+1",
                new Vector2(30, 0), new Vector2(140, 110), ButtonStyle.Secondary), 350);
            var setFace = Bottom(UiFactory.Button(content, "SetFaceButton", "SET FACE",
                new Vector2(300, 0), new Vector2(380, 110), ButtonStyle.Secondary), 350);

            var faceRoot = UiFactory.Panel(content, "FaceButtons", stretch: false);
            faceRoot.sizeDelta = new Vector2(880, 110);
            Bottom(faceRoot, 350);
            faceRoot.anchoredPosition = new Vector2(-70, faceRoot.anchoredPosition.y);
            Row(faceRoot, spacing: 10);
            for (int face = 1; face <= 6; face++)
            {
                var faceButton = UiFactory.Button(faceRoot, "Face" + face, face.ToString(),
                    Vector2.zero, new Vector2(135, 100), ButtonStyle.Secondary);
                FixedSize(faceButton.gameObject, 135, 100);
            }
            var faceCancel = Bottom(UiFactory.Button(content, "FaceCancelButton", "×",
                new Vector2(460, 0), new Vector2(110, 100), ButtonStyle.Ghost), 350);

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

            var trayHint = UiFactory.Label(tray, "Hint", "YOUR DICE", Vector2.zero, new Vector2(960, 34), 24f,
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

            // The one tween router every animating view in the scene shares (ui-conventions.md).
            go.AddComponent<UiAnimationService>();

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
            var stampLabel = UiFactory.Label(stampRt, "Text", "COMMITTED", Vector2.zero, new Vector2(220, 50), 28f,
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

            var nameLabel = UiFactory.Label(rt, "Name", "PLAYER", new Vector2(0, 62), new Vector2(150, 32), 23f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            var scoreLabel = UiFactory.Label(rt, "Score", "0", new Vector2(0, 16), new Vector2(150, 60), 53f,
                TextAlignmentOptions.Center, FontRole.HeadingBold);
            var detailLabel = UiFactory.Label(rt, "Detail", "", new Vector2(0, -36), new Vector2(150, 30), 23f,
                TextAlignmentOptions.Center, FontRole.Body, Muted(0.55f));
            var stateLabel = UiFactory.Label(rt, "State", "", new Vector2(0, -68), new Vector2(150, 30), 23f,
                TextAlignmentOptions.Center, FontRole.BodySemibold);

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

            var spent = UiFactory.Label(body, "Spent", "SPENT", Vector2.zero, new Vector2(size, 44), 26f,
                TextAlignmentOptions.Center, FontRole.BodySemibold, _theme.stateSpent, 0.2f);
            spent.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            spent.gameObject.SetActive(false);

            var view = go.AddComponent<DieView>();
            SetRef(view, "button", go.GetComponent<Button>());
            SetRef(view, "body", body);
            SetRef(view, "background", background);
            SetRef(view, "frame", frame);
            SetRef(view, "pips", pips);
            SetRef(view, "spentWatermark", spent.gameObject);
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
