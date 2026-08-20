# CLAUDE.md — CardGameTest

Guidance for Claude Code and for humans working in this repository.

## Project state

**Pre-alpha, greenfield.** As of this document the repository is a Unity 6
"2D (URP)" template with no gameplay code. Everything below is the contract for
the code that is about to be written, not a description of code that exists.

- **Unity 6000.5.0f1** — do not change the version without a team decision
- **URP 17.5.0, 2D Renderer**
- **New Input System only** — the legacy Input Manager is disabled; never use
  `Input.GetKey`
- **Hybrid UI** — uGUI + TextMeshPro for the board, UI Toolkit for menus
- **Multiplayer is in the MVP** — server-authoritative, NGO

## ⚠️ The game's rules are not yet defined

`docs/design/gameplay.md` is a stub. Until it is filled in:

- Do **not** invent game rules, card names, costs, or mechanics
- Do **not** implement anything tagged `blocking` in the backlog
- Work tagged `none` or `partial` is fair game and is roughly two thirds of the
  alpha

If a task requires knowing how the game plays, stop and ask rather than guessing.

## Read these first

| Document | What it covers |
|---|---|
| `docs/architecture/overview.md` | Assembly graph, core patterns, netcode, testing. **The authority.** |
| `docs/agile/working-agreement.md` | Epics/stories, estimation, branching, Unity process rules |
| `docs/agile/definition-of-done.md` | What "Done" means here |
| `docs/backlog/roadmap.md` | Phasing and sequencing rationale |
| `docs/backlog/epics.md` | Epic index |
| `docs/design/gameplay.md` | Game rules — **stub, pending design** |

---

## The one architectural rule

> **The rules are a pure C# library that happens to run inside Unity.**

`CardGame.Core` has `noEngineReferences: true`. It cannot see `UnityEngine`, and
that is deliberate — determinism, fast tests, server authority, save/resume,
replay and hidden-information safety all depend on it.

**In Core you may not use:** `Debug.Log`, `Vector2`/`Vector3`, `Mathf`,
`UnityEngine.Random`, `System.Random`, `[SerializeField]`, `MonoBehaviour`,
`ScriptableObject`, `Coroutine`, `Time.*`, `JsonUtility`.

**Use instead:** `ILogSink`, plain structs, `System.Math`, `IRandomSource`,
plain C# properties, `IClock`, Newtonsoft JSON.

Adding a `UnityEngine` reference to Core to solve a problem is never the answer.
The problem belongs on the other side of the boundary.

## Dependency direction

```
Core ← Data, Persistence, Net, Presentation, UI ← App
```

- Arrows point **upward only**. `App` is the sole composition root; nothing
  references `App`.
- `Presentation` (uGUI) and `UI` (UI Toolkit) **never reference each other**.
- `Net` never references `Presentation`.
- Service interfaces live in **Core**, because they are pure. Presentation calls
  `IGameSession.Submit(cmd)` and cannot tell whether the implementation is a
  local loopback or a network client. That indirection is what makes multiplayer
  a swap rather than a rewrite — do not bypass it.

## The command/event loop

Every state change follows exactly this path. There are no shortcuts.

```
player input → intent → command → Core validation → state change → event → view update
```

- A view **never** mutates game state. It raises an intent.
- A view **never** updates itself optimistically ahead of Core's confirmation.
  In single-player that is a bug; in multiplayer it is a desync.
- Events describe **what happened**, never what to draw. There is no
  `PlayCardAnimationEvent` — there is `CardMoved`, and presentation decides that
  hand→board means a 0.25 s arc.
- Events buffer during `Execute` and publish only after the command commits. A
  subscriber must never observe half-applied state.
- Presentation consumes **batches** — one command's events are one animation
  sequence.

## Reusable patterns (use these; do not invent alternatives)

### Card data: three types, not two

```
CardDefinitionAsset (Unity SO)  →  CardDefinition (Core)  →  CardInstance (Core)
authored, has Sprite/AudioClip     immutable content        runtime identity+state
```

**Never store runtime state on a ScriptableObject.** Playing a match in the
Editor would permanently edit your content asset — silently. Runtime state lives
on `CardInstance`, keyed by `InstanceId`.

Card ids are **stable GUID strings, never array indices** — reordering a list
must not invalidate saves.

### Commands

Serializable, with a stable string `CommandTypeId` (`"core.play_card"`), never a
reflected C# type name — a refactor must not break stored replays.

`Validate()` returns a localization key, never a formatted sentence.

**Undo is snapshot + replay-to-N, not inverse commands.** Inverse commands are a
lie the moment an effect involves randomness or hidden information, and they
double the surface area of every card.

### Phases

`PhaseMachine` holds a **stack**, not a single current phase — nested decisions
are pushes. The core **blocks on a `PendingDecision`** rather than asking a
player object for input; `IDecisionProvider` is then implemented identically by
human UI, AI, network, tests and tutorials.

### Randomness

`RngHub` with named streams: `Shuffle`, `Effects`, `Ai`, `Cosmetic`.

- **Never** `System.Random` (implementation differs across Mono/IL2CPP/CoreCLR —
  same seed, different platform, different sequence) or `UnityEngine.Random`
  (a global static shared with particle systems; a cosmetic call perturbs the
  next shuffle).
- Cosmetic randomness uses `RngStream.Cosmetic`, always. One shared generator
  means adding a card-wiggle offset invalidates every stored replay.
- **The shuffle seed is a secret.** Never send it to a client, never log it
  client-side, never write it into a client-side save of a multiplayer match.
- Save the RNG **state**, not the seed — restoring from a seed replays the draw
  sequence from turn 1.

### Theming

Components reference **semantic tokens** (`surface.raised`, `text.muted`), never
raw colours. UI Toolkit consumes them as USS variables; uGUI consumes them via
`ThemeBinder` components. A hard-coded hex in either stack is a review rejection.

### Hidden information

`CardId` is the secret; `InstanceId` is safe. A hidden card projects with
`Definition == null`, which is exactly what a card view renders as a card back.

Never send data the client shouldn't see and hide it in the UI — an obfuscated
packet is one cheat away from a wallhack, and it will be found.

---

## The uGUI / UI Toolkit boundary

> **If the element must be spatially interleaved with, anchored to, or
> drag-and-drop hit-tested against 2D sprite content in the board's sorting
> layers, it is uGUI. Everything else is UI Toolkit.**

Apply in order, stop at the first yes:

1. Renders between sprite sorting layers? → uGUI
2. Follows a world position every frame? → uGUI
3. Drag source or drop target for cards? → uGUI
4. Shares per-frame material/shader effects with cards? → uGUI
5. Otherwise → **UI Toolkit**

Menu scenes contain zero `Canvas`. The board hierarchy contains zero
`UIDocument` outside the designated full-screen overlay root.

If you find yourself wanting a reference between `CardGame.UI` and
`CardGame.Presentation`, something is on the wrong side of the boundary.

---

## C# conventions

- **Namespaces** mirror assemblies: `CardGame.Core.Commands`, `CardGame.Presentation.Board`
- `PascalCase` for types/methods/properties; `camelCase` for locals and
  parameters; `_camelCase` for private fields
- **`readonly` and immutability by default** in Core — mutation happens through
  `GameStateMutator`, not by assigning to state objects
- Prefer `record` for events and DTOs, `readonly struct` for ids
- **No `null` returns for "not found"** — use `TryGet` or an explicit result type
- **No static mutable state.** Ever. It breaks determinism, tests, and multiple
  simultaneous sessions (host + client in one process)
- XML doc comments on public types and any non-obvious logic
- One type per file, named for the file

### Unity-specific

- `[SerializeField] private` over `public` fields
- Cache component lookups in `Awake`; never `GetComponent` in `Update`
- Never `Find`, `FindObjectOfType`, or `SendMessage`
- Unsubscribe every event in `OnDisable`/`OnDestroy` — a leaked subscription
  across a scene load is the most common Unity memory bug
- No allocation in the match loop; verify with the Profiler, don't assume
- MonoBehaviours are thin: they bind, animate, and forward input. Logic lives in
  Core.

---

## Testing

| Suite | Speed | Contents |
|---|---|---|
| `CardGame.Core.Tests` (EditMode) | **< 5 s total** | All rules logic. `new`s objects directly — no scene, no play mode. |
| `CardGame.EditMode.Tests` | seconds | Content/asset validation, scene audits, the Core-purity guardrail |
| `CardGame.PlayMode.Tests` | slow | Binder fidelity, input, scene flow, NGO integration |

**Write rules tests in `Core.Tests`.** If a rules test needs a scene, the logic
is in the wrong assembly.

Required test categories, all of which exist because of the architecture:

- **RNG golden vectors** — fixed seed → hard-coded outputs. This test exists
  solely to fail loudly if someone "optimizes" the generator, which would
  silently invalidate every stored replay and save.
- **Determinism** — same seed + commands twice ⇒ identical state hash.
- **Hidden-information leak tests** — assert on the serialized **bytes**, not the
  structure. A structural assertion misses leaks through newly added fields.
- **Fuzz/invariant walker** — a seeded random legal-move chooser plays thousands
  of matches asserting invariants after every command. ~60 lines, and it finds
  rule-interaction bugs no hand-written test will. Store failing seeds as
  regression fixtures.

CI gates merges on EditMode only. Gate on the slow suite and contributors start
skipping tests.

---

## Git and process

- Branch per story: `feat/STORY-3.2-card-drag-drop`
- `feat(rules): add discard shuffle-back on empty draw` — imperative, scoped;
  story id in the body, not the subject
- **Never commit `Library/`, `Temp/`, `Logs/`, or generated `.csproj`/`.sln`**
- **A new asset's `.meta` file is part of the commit.** A missing meta file
  reassigns the GUID on next import and silently breaks every reference to it.
- **Never hand-delete a `.meta` file.**
- One person edits a given scene at a time. Prefer prefabs over scene objects,
  and ScriptableObjects over prefabs, for anything data-shaped.

## Working notes for Claude

- Check the story's **genre dependency** tag before starting. `blocking` means
  stop.
- The architecture doc is the authority; if this file and it disagree, the
  architecture doc wins and this file needs fixing.
- When adding a new system, state which assembly it belongs in and why, before
  writing code.
- Don't add a package without a stated reason — `docs/architecture/overview.md`
  lists what's approved and what is explicitly rejected, with rationale.
