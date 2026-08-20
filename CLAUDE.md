# CLAUDE.md — Foundry

Guidance for Claude Code and for humans working in this repository.

## What this is

**Foundry** (working codename) — a **simultaneous-roll dice engine builder for
iOS**. 2–6 players, 10 fixed rounds, 11–13 minutes, real-time online with
friends by code.

`docs/game-design.md` is the **canonical spec**. If this file and it disagree,
it wins and this file needs fixing.

- **Unity 6000.5.0f1** · URP 17.5.0 (2D Renderer) · **New Input System only**
- **uGUI + TextMeshPro only.** There is no UI Toolkit in this project — no
  `.uxml`, no `.uss`, no `UIDocument`. Do not introduce one.
- **Netcode for GameObjects 2.13** + Unity Gaming Services (Multiplayer Sessions)
- Milestones M1–M5 complete; **M6 (polish) is the remaining specced milestone**

## The one architectural rule

> **The rules are a pure C# library that happens to run inside Unity.**

`Game.Core` sets `noEngineReferences: true` with `references: []`. It cannot see
`UnityEngine`, and that is load-bearing: determinism, a 2-second headless test
suite, server authority, and hidden-information safety all depend on it.

**Never** add a Unity reference to `Game.Core` to solve a problem. The problem
belongs on the other side of the boundary.

In Core: no `Debug.Log`, no `Vector2`, no `Mathf`, no `UnityEngine.Random`, no
`System.Random`, no `[SerializeField]`, no `MonoBehaviour`, no `Coroutine`, no
`Time.*`, no `JsonUtility`. Use `System.Math`, `IDiceRoller`, plain properties,
and let the caller pass `now` for anything time-shaped (see `IntentLimiter`,
`SeatRegistry` — both pure, both therefore headless-testable).

## Assemblies

```
Game.App          composition root, presenters, bootstrap, scene flow
 ├ Game.UI        uGUI views (TMP)          ├ Game.Networking  NGO + UGS
 ├ Game.Audio     AudioManager + mixer      ├ Game.Persistence JSON profile
 ├ Game.Data      ScriptableObjects         └ Game.SceneTools  scene generation (Editor)
 └ Game.Core      PURE C# rules — noEngineReferences: true
```

Dependencies point **downward only**. `Game.UI` never references
`Game.Networking` — that is what lets one HUD serve both hot-seat and online.
`Game.SceneTools` and `Game.EditorTools` are Editor-only.

## The round

Six phases, not five. `Repick` is a real phase and is easy to forget.

```
Roll → Shape → Commit → Reveal → Repick → Upkeep    ×10 rounds → MatchOver
auto   20s      15s      ~8s      10s      auto
```

- Play is **simultaneous**. There is no turn order and no per-player phase.
- **Core never ticks a clock.** `MatchConfig` carries the durations so the server
  timer and the UI agree, but the rules layer is pure and synchronous. The driver
  (`LocalMatchSession.Advance()` offline, the server clock online) calls the
  engine.
- Commits are **secret until Reveal**. Contested cards go to priority (lowest
  score, then fewest cards, then seat order); losers keep their dice and get one
  re-pick pass.
- A player may commit during **Shape** as well as Commit (CORE-5) — that is what
  collapses hot-seat from eight device handoffs per round to one.

## The command/event path

```
view event → presenter → IGameActions intent → RulesEngine validation → state → snapshot → view
```

- A view **never** mutates match state and **never** predicts. `GameHudView`
  raises events; `GameHudPresenter` turns them into intents.
- `IGameActions` + `IMatchView` (both in `Core/IGameActions.cs`) are the whole
  boundary. `LocalMatchSession` and `NetworkGameController` each implement both,
  which is why the board is identical online and offline. **Do not bypass this.**
- Rejections come back as `MoveFailure` and are rendered by
  `GameHudPresenter.Explain` — a single place for that copy.

## Patterns in use (follow these; don't invent alternatives)

### Cards are data, not code

`PowerKind` / `PowerFamily` enums + a `CardPower` readonly struct. A designer
adds a card by picking a cost pattern and a power — no recompile, no subclass
per card (CARD-2).

Costs are the polymorphic half: `ICardRequirement` with `NOfAKind`, `Run`, `Sum`,
`ContainsFaces`, `Composite` matchers.

`StarterDeck` in `Core/CardBlueprint.cs` is the **single definition of the 48
cards**, written in a fluent DSL:

```csharp
Def(1, "Whetstone", tier: 1, points: 2, PowerFamily.Manipulation).Sum(12).Nudge(1),
```

The editor generator writes ScriptableObjects from it and the balance harness
simulates against it, so a card cannot be tuned in one place and ship from
another. **Edit `StarterDeck`, then regenerate — never hand-edit a card asset.**

### Powers are derived, never cached

`PlayerState.SumPower/WildFaces/DiceCapacity/...` recompute from `OwnedCards`
every call. Do not add mutable counters that can drift out of sync with the card
list.

### Determinism

`IDiceRoller` is the only entropy source in Core. `SeededDiceRoller` is a
hand-rolled xorshift64\* — portable across platform and runtime, which
`System.Random` is not (its algorithm has changed across .NET versions, so the
same seed yields different sequences on Mono vs IL2CPP vs CoreCLR).

`UnityEngine.Random` is never acceptable in gameplay: it is a global static
shared with particle systems, so a cosmetic call perturbs the next roll.

### Hidden information

`MatchSnapshot.For(state, observer, seats, now)` builds **one snapshot per
recipient**. `PendingCardId` is `-1` and `PendingDice` empty for everyone but the
owner until `Reveal`.

Deliberately **not** secret, and asserted as such: dice faces, owned cards,
priority order, and the fact that a player has decided. Reading that an opponent
rolled a pair of 5s is the whole basis for deciding whether to contest — only the
choice is hidden.

Never send data a client shouldn't see and hide it in the UI. If you add a field
to `MatchSnapshot`, `SecrecyGateTests` covers it automatically — it compares a
reflective dump of two matches differing only in a secret commit. Do not weaken
that test.

## Testing

```
tools/run-core-tests.sh              # 119 tests, ~2s, no Editor needed
tools/run-core-tests.sh Contention   # substring filter on Fixture.Method
FOUNDRY_BALANCE=1 tools/run-core-tests.sh Balance   # full balance report
tools/verify-unity-compile.sh        # type-check Unity assemblies while the Editor holds its lock
```

`tools/CoreTests` compiles **the same source files** Unity does — it does not
fork them — and runs the same NUnit `[Test]` methods by reflection.

The runner supports only `[Test]`, `[TestFixture]`, `[SetUp]`, `[TearDown]`, and
**exits with code 2 rather than silently reporting green** if it finds
`[TestCase]`, `[UnityTest]`, or similar. So: write plain `[Test]` methods.

`verify-unity-compile.sh` checks **types, not asmdef boundaries** — everything
compiles into one assembly, so a script reaching across an undeclared assembly
reference passes there and fails in Unity. Green means "no type errors", not
"Unity is happy".

Rules tests go in `Tests/EditMode`. If a rules test needs a scene, the logic is
in the wrong assembly.

## Running the game

Scenes are **generated by code**, not hand-authored — `Game.SceneTools`.

1. **Foundry ▸ Generate Starter Deck** — writes the 48 card assets + `CardDatabase`
2. **Foundry ▸ Generate Scenes & Build Settings** — rebuilds Boot / MainMenu /
   Lobby / Game and sets the build list
3. Open **Game** and press Play. `HotSeatHost.playerCount` sets seats;
   `fixedSeed` replays an exact match.

**Re-run both generators after touching `SceneScaffolder` or `StarterDeck`, and
commit the regenerated assets.** A stale committed scene means the board does not
start — this has already happened once.

Online: Boot → MainMenu → Host/Join by code → Lobby → Start. Requires UGS linked
with Authentication, Relay and Lobby enabled.

## Conventions

- Namespaces mirror assemblies: `Game.Core`, `Game.UI`, `Game.Networking`
- `PascalCase` types/methods/properties, `camelCase` locals/params,
  `_camelCase` private fields
- `readonly struct` for ids and value types; `internal` mutators on state so only
  `RulesEngine` can drive transitions
- XML doc comments on public types and any non-obvious logic — the existing code
  does this well and explains *why*, not *what*. Match that.
- **No static mutable state.** It breaks determinism, tests, and host+client in
  one process.

### Unity-specific

- `[SerializeField] private` over public fields
- Cache lookups in `Awake`; never `GetComponent` in `Update`
- Never `Find`, `FindObjectOfType`, or `SendMessage`
- Unsubscribe every event in `OnDisable`/`OnDestroy`
- **No third-party tween libraries.** Use Unity's Animator or coroutine
  `Mathf.Lerp`/`SmoothStep`. This keeps the dependency set to the official
  registry and is a deliberate project policy.
- MonoBehaviours stay thin: bind, animate, forward input. Logic lives in Core.

## Netcode

- **All dice originate on the server** (NET-1). Clients send intent only and
  never generate, predict, or re-derive a roll.
- NGO 2.x universal `[Rpc]` attribute — no legacy `[ServerRpc]`/`[ClientRpc]`.
- Every client→server RPC resolves `SenderClientId` → seat **before** anything
  else, so a client can only ever act as itself.
- Every server→client RPC checks `FromServer(rpc)`. Nothing in the transport
  stops a peer sending one directly to another client.
- Broadcasts are **coalesced to one per frame** in `LateUpdate`. Each replication
  costs a snapshot encode *per recipient*; do not replicate inline from a
  mutation path.
- A disconnected player **counts as decided** so the phase closes immediately
  instead of burning a full timer on an absent device.
- Seats are owned by a **stable key** (the UGS auth id), not a transport id —
  that is what makes reconnect work.
- **No host migration.** Out of scope (NET-4).

## Git and process

- Branch per story: `feat/M6.1-reveal-choreography`
- `feat(ui): stage the reveal beat` — imperative, scoped; story id in the body
- Never commit `Library/`, `Temp/`, `Logs/`, or generated `.csproj`/`.sln`
- **A new asset's `.meta` file is part of the commit.** A missing meta file
  reassigns the GUID on next import and silently breaks every reference.
- Never hand-delete a `.meta` file.
- Regenerated scenes and card assets are commits like any other — see above.

## Working notes for Claude

- Read `docs/game-design.md` before touching rules or UI. The requirement ids
  (CORE-n, MKT-n, CARD-n, NET-n, UI-n) are referenced throughout the code.
- State which assembly a new system belongs in, and why, before writing it.
- Run `tools/run-core-tests.sh` before claiming anything works.
- Don't add a package without a stated reason. Addressables is already installed
  and entirely unused; `com.unity.ai.assistant` is a pre-release. Both are
  removal candidates, not precedents.
