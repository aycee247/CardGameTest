# Dice-and-Card Game — Scaffold

An iOS-targeted, online-multiplayer dice-and-card game built on **Unity 6.5 (6000.5.0f1)**, URP 2D, the new Input System, Netcode for GameObjects 2.x, and Unity Gaming Services (Multiplayer Services / Sessions).

**Game loop:** each player owns 6 dice. On your turn you roll (up to N times, Yahtzee-style), then claim a market card whose dice requirement your roll satisfies. Points accumulate; first to the target score wins. Dice are **server-authoritative** — clients only send intent, never dice values.

---

## Architecture

Assemblies, with dependencies pointing **downward** (each layer only knows the ones below it):

```
Game.App          composition root / bootstrap / presenters   (refs everything below + NGO/UGS)
  Game.UI         passive views (TMP), safe-area/orientation   (refs Core, Data, Audio)
  Game.Networking NGO + MPS Sessions adapter, host authority    (refs Core, Data + NGO/UGS)
  Game.Persistence Newtonsoft JSON save/load                    (refs Core, Data)
  Game.Audio      AudioManager + mixer                          (refs Core, Data)
  Game.Data       ScriptableObjects (cards, dice, database)     (refs Core)
  Game.Core       PURE C# rules engine — no Unity, no netcode   (noEngineReferences = true)
```

**Why this shape:** `Game.Core` is compiler-guaranteed free of Unity/netcode (`noEngineReferences`), so the rules are deterministic and unit-testable, and the exact same rules run on host, dedicated server, or an offline mock. UI depends on the `IGameActions` / `IGameStateView` interfaces in Core — never on Networking — so every screen runs identically online and offline (bind it to `LocalGameSession` with zero networking).

### Key types
- `Game.Core.RulesEngine` — the single authority over state transitions (roll / claim / end-turn), validates turn + phase before mutating.
- `Game.Core.GameState` / `PlayerState` / `GameConfig` — authoritative mutable match state.
- `Game.Core.ICardRequirement` + matchers (`NOfAKind`, `Run`, `Sum`, `ContainsFaces`, `Composite`).
- `Game.Core.SeededDiceRoller` — portable, deterministic xorshift PRNG (server owns the seed).
- `Game.Core.GameStateSnapshot` — serializable read-only projection for UI + network replication.
- `Game.Core.LocalGameSession` — offline hot-seat session implementing the UI boundary.
- `Game.Networking.NetworkGameController` — host-authoritative `NetworkBehaviour`; clients send intent RPCs, server broadcasts snapshots.
- `Game.Networking.SessionManager` — UGS init + MPS Sessions (create/join by code).
- `Game.App.GameBootstrap` — initializes services, loads Main Menu, saves on iOS pause/quit.

---

## First-time setup (in the Unity Editor)

1. **Open the project.** On first open, Package Manager resolves the packages added to `Packages/manifest.json` — all from Unity's **official registry**: NGO, `com.unity.services.multiplayer`, Authentication, Addressables, Newtonsoft. (No third-party or scoped-registry packages are used.) Wait for compilation to finish with a clean Console. If any package version reports "not found for 6000.5", accept the nearest patch **within the same major**.

2. **Import TMP essentials:** **Window ▸ TextMeshPro ▸ Import TMP Essential Resources** (needed so generated text has a default font).

3. **Generate starter content:** menu **DiceCards ▸ Generate Sample Content**. Creates 8 `CardDefinition` assets + a `CardDatabase` under `Assets/_Project/ScriptableObjects/`.

4. **Generate scenes (one click):** menu **DiceCards ▸ Generate Scenes & Build Settings**. This creates and fully wires the Boot / MainMenu / Lobby / Game scenes (cameras, EventSystem, Canvas + safe area, all our components, a CardButton prefab) and sets the Build Settings list in order `Boot(0) → MainMenu → Lobby → Game`. Run *after* step 3 so the Game scene can bind the CardDatabase. Then remove the template `Assets/Scenes/SampleScene`. (The manual scene layout below is documented for reference / customization.)

5. **Configure Unity Gaming Services** (needed for online play): **Edit ▸ Project Settings ▸ Services** — link a Unity project/organization, then enable **Authentication**, **Relay**, and **Lobby** in the Unity Cloud dashboard. Anonymous sign-in is used by default (`SessionManager.InitializeAsync`).

---

## Scenes to author (editor-only; code is already wired)

These require the editor because they compose prefabs/components that depend on the resolved packages.

- **Boot** — an empty GameObject with `GameBootstrap` + an `AudioManager`, plus a `NetworkManager` (with `UnityTransport`) on a persistent object. Bootstrap loads Main Menu after UGS init.
- **MainMenu** — a Canvas with a `MainMenuView` (Host button, Join button, join-code `TMP_InputField`, status + code labels) and a `MainMenuController` referencing it. Wrap UI in a panel with `SafeAreaFitter`.
- **Lobby** — shows the join code and a Host-only "Start" button. On start, the host calls `SceneFlowService.LoadNetworkedGame()` (NGO replicates the Game scene to clients).
- **Game** — a Canvas with `GameHudView` (turn/phase/rolls/dice/score labels, Roll + End-Turn buttons, a market container + a `CardButtonView` prefab) and a `GameHudPresenter`. Add a `MatchLauncher` (with `CardDatabase` + `NetworkGameController` references); the host calls `MatchLauncher.ServerBeginMatch()` once players are in.

### Prefabs to create
- **NetworkManager** prefab: `NetworkManager` + `UnityTransport`; register the `NetworkGameController` prefab in its Network Prefabs list.
- **NetworkGameController** prefab: an empty GameObject with `NetworkObject` + `NetworkGameController`, spawned by the host at match start.
- **CardButton** prefab: `Button` + `CardButtonView` (name/requirement/points TMP texts, optional artwork `Image`).

### Audio
Create an `AudioMixer` with exposed float params `MasterVolume` / `MusicVolume` / `SfxVolume` and assign it to the `AudioManager`.

---

## iOS build

- Bundle id is set to `com.aaroncornwell.dicecards` and company to `AaronCornwell` (change in **Project Settings ▸ Player** if desired).
- Orientation is already **auto-rotate** (portrait + landscape); UI uses `SafeAreaFitter` for notch handling and `OrientationWatcher` for responsive reflow.
- iOS uses **IL2CPP** + **.NET Standard 2.1** (defaults). Start with **Managed Stripping Level = Low**; if you raise it, add a `link.xml` preserving `Newtonsoft.Json`, `Unity.Services.*`, and your `[Serializable]` save types.
- No camera/mic/location usage strings are needed. A default launch storyboard is generated by Unity.
- Build: **File ▸ Build Profiles ▸ iOS**, switch platform, build the Xcode project, sign, run on device/simulator.

---

## Testing

- **EditMode tests** (`Assets/_Project/Code/Tests/EditMode/`) cover the requirement matchers, deterministic roller, the full rules flow, and the offline `LocalGameSession`. Run via **Window ▸ General ▸ Test Runner ▸ EditMode**, or headless:
  ```
  /Applications/Unity/Hub/Editor/6000.5.0f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -projectPath . -runTests -testPlatform EditMode -logFile -
  ```
- **PlayMode tests** (`Tests/PlayMode/`) are set up for the networked host-authoritative flow (NGO test harness).
- **Multiplayer smoke test:** open two editor instances (Multiplayer Play Mode, or a ParrelSync clone) — one Host, one Join-by-code. Verify a roll requested out of turn is rejected and dice values come only from the server.

---

## Extending

- **New card rules:** add an `ICardRequirement` in `Game.Core` and a `CardRequirementSpec.Kind` case in `Game.Data`. The rules engine and UI pick it up automatically.
- **Apple Sign-In:** swap `SignInAnonymouslyAsync` in `SessionManager` for the Apple provider (upgrade the anonymous account).
- **Dedicated server:** the host-authoritative `NetworkGameController` moves to a Multiplay dedicated server with no changes to `Game.Core`.
- **Animations:** use registered/first-party options only — Unity's built-in **Animation/Animator** clips, or lightweight coroutine `Mathf.Lerp`/`SmoothStep` tweens, for dice-roll and card-claim motion in the views. (Avoid third-party tween libraries to keep the dependency set to the official registry.)
