# Foundry

An iOS-targeted, online-multiplayer **simultaneous-roll dice engine builder**,
built on Unity 6.5 (6000.5.0f1), URP 2D, the new Input System, Netcode for
GameObjects 2.x, and Unity Gaming Services (Multiplayer Sessions).

**The loop:** all players roll at once, every round, on a server-owned clock.
You shape your dice with powers and Sparks, then secretly commit one market card
and the exact dice paying for it. Commits flip together at Reveal; contested
cards go to the lowest-scoring claimant and losers re-pick. Cards permanently
modify your dice — more dice, free re-rolls, wild faces — so by round 10 you are
shaping toward a result on purpose. Ten rounds, highest points wins.

**Dice are server-authoritative.** Clients send intent and never generate,
predict, or re-derive a roll.

📖 **`docs/game-design.md` is the canonical spec.** `/CLAUDE.md` is the working
reference for conventions.

---

## Architecture

Assemblies, dependencies pointing **downward**:

```
Game.App          composition root, presenters, bootstrap, scene flow
  Game.UI         passive uGUI/TMP views, safe area      (Core, Data, Audio)
  Game.Networking NGO + UGS Sessions, host authority     (Core, Data + NGO/UGS)
  Game.Persistence Newtonsoft JSON profile save/load     (Core, Data)
  Game.Audio      AudioManager + mixer                   (Core, Data)
  Game.Data       ScriptableObjects (cards, dice skins)  (Core)
  Game.Core       PURE C# rules — no Unity, no netcode   (noEngineReferences)
```

`Game.Core` is compiler-guaranteed free of Unity, so the rules are deterministic,
unit-testable without the Editor, and identical on host, client and headless
harness. `Game.UI` depends on `IGameActions` / `IMatchView` in Core — never on
`Game.Networking` — which is why every screen runs identically online and offline.

### Key types

- `Game.Core.RulesEngine` — the single authority over state transitions
- `Game.Core.MatchState` / `PlayerState` / `MatchConfig` — round-indexed match
  state; there is no "current player", because play is simultaneous
- `Game.Core.RoundPhase` — `Roll → Shape → Commit → Reveal → Repick → Upkeep`
- `Game.Core.CardPower` / `PowerKind` / `PowerFamily` — powers are **data**
- `Game.Core.ICardRequirement` + matchers (`NOfAKind`, `Run`, `Sum`,
  `ContainsFaces`, `Composite`) — card costs
- `Game.Core.StarterDeck` — the single definition of all 48 cards
- `Game.Core.SeededDiceRoller` — portable deterministic xorshift64*
- `Game.Core.MatchSnapshot.For(state, observer, …)` — **per-recipient** view with
  hidden-information filtering
- `Game.Core.LocalMatchSession` / `HotSeatDirector` — offline hot-seat
- `Game.Networking.NetworkGameController` — host-authoritative `NetworkBehaviour`
- `Game.App.GameSceneBootstrap` — picks hot-seat vs online at scene load

---

## Setup

1. **Open the project.** Package Manager resolves everything from Unity's
   official registry — NGO, `com.unity.services.multiplayer`, Authentication,
   Addressables, Newtonsoft. No third-party or scoped registries.

2. **Import TMP essentials:** Window ▸ TextMeshPro ▸ Import TMP Essential Resources.

3. **Foundry ▸ Generate Starter Deck** — writes the 48 `CardDefinition` assets and
   the `CardDatabase`. Validates as it goes and errors on any cost no legal dice
   pool could pay.

4. **Foundry ▸ Generate Scenes & Build Settings** — rebuilds Boot / MainMenu /
   Lobby / Game, wires every component reference, and sets the build list in
   order. Run *after* step 3 so the Game scene can bind the database.

5. **Unity Gaming Services** (online only): Edit ▸ Project Settings ▸ Services —
   link a project, then enable Authentication, Relay and Lobby in the Unity Cloud
   dashboard. Anonymous sign-in is used by default.

> ⚠️ **Scenes are generated, not hand-authored.** Never edit a generated scene
> directly — change `SceneScaffolder` and regenerate. If the board does not start
> when you press Play, the committed scenes are behind the generator: re-run
> steps 3 and 4 and commit the results.

## Playing

**Hot-seat:** open the Game scene and press Play. `HotSeatHost.playerCount` sets
2–6 seats; `fixedSeed` replays an exact match. The handoff screen is load-bearing
— the director moves the private view to the next seat *before* the panel goes
up, so the previous player's commit is out of the snapshot before the device
changes hands.

**Online:** Boot → MainMenu → Host (shows a join code) or Join by code → Lobby →
host presses Start. Both modes share the entire presentation layer, because
`LocalMatchSession` and `NetworkGameController` implement the same
`IGameActions`/`IMatchView` pair. The only difference is who advances the phases
— the player, or the server's clock.

## Testing

```
tools/run-core-tests.sh              # 119 tests, ~2s, no Editor needed
tools/run-core-tests.sh Contention   # substring filter
FOUNDRY_BALANCE=1 tools/run-core-tests.sh Balance    # full balance report
tools/verify-unity-compile.sh        # type-check while the Editor holds its lock
```

`tools/CoreTests` compiles **the same source files** the Unity assemblies do, so
the headless run and Unity's Test Runner execute one suite, not two. The runner
supports only `[Test]`/`[TestFixture]`/`[SetUp]`/`[TearDown]` and exits with code
2 rather than silently reporting green on richer attributes it cannot run.

`verify-unity-compile.sh` verifies **types, not asmdef boundaries** — everything
compiles into one assembly, so a script reaching across an undeclared reference
passes there and fails in Unity.

Notable suites: `SecrecyGateTests` (two matches differing only in a secret commit
must produce byte-identical opponent snapshots, compared by reflective dump),
`BalanceGateTests` (no strategy exceeds 1.5× fair share), `DisconnectTests`,
`ContentionTests`.

## iOS build

- Bundle id `com.aaroncornwell.dicecards`, company `AaronCornwell`, min iOS 15.0
- IL2CPP + .NET Standard 2.1. Start at Managed Stripping Level = Low; if you
  raise it, add a `link.xml` preserving `Newtonsoft.Json`, `Unity.Services.*`
  and the `[Serializable]` save types.
- `SafeAreaFitter` handles notches. The Game scene is laid out for portrait
  1080×1920 — see `docs/backlog/E6-ship.md` on locking orientation.
- Build: File ▸ Build Profiles ▸ iOS, switch platform, build the Xcode project.

## Extending

- **New card:** add a line to `Game.Core.StarterDeck`, then re-run Generate
  Starter Deck. Never hand-edit a generated card asset — the deck is defined
  once so the balance harness and the shipped assets cannot diverge.
- **New cost pattern:** add an `ICardRequirement` in `Game.Core` and a
  `CardRequirementSpec.Kind` case in `Game.Data`.
- **New power:** add a `PowerKind` case and handle it in the consumers that
  switch on it (`PlayerState`, `RulesEngine`, `CostChecker`, `Scoring`).
- **Animations:** Unity's Animator or coroutine `Mathf.Lerp`/`SmoothStep` only —
  no third-party tween libraries, deliberately, to keep the dependency set on the
  official registry.
- **Dedicated server:** `NetworkGameController` moves to Multiplay with no
  changes to `Game.Core`.
