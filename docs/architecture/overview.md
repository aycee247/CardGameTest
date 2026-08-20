# Architecture Overview

## The one rule everything else follows from

> **The rules are a pure C# library that happens to run inside Unity.**
> Unity is a rendering and input host. If a design choice makes the rules
> depend on Unity, it is the wrong choice.

Netcode simplicity, test speed, determinism, hidden-information safety, save/
resume and replay all fall out of that single constraint. Nothing else in this
document is as important.

Corollary: **genre-agnostic means the core knows about zones, cards, commands,
phases and decisions — and nothing else.** No "mana", no "trick", no "attack".
Those live in a ruleset module that plugs into the core.

## Assembly graph

```
                    ┌───────────────────────────────┐
                    │  CardGame.Core                │
                    │  noEngineReferences: true     │  ← THE INVARIANT
                    │  state, commands, phases,     │
                    │  RNG, event bus, contracts,   │
                    │  visibility, serialization    │
                    └───────────────┬───────────────┘
        ┌──────────────┬────────────┼─────────────┬──────────────┐
   ┌────▼─────┐  ┌─────▼──────┐ ┌───▼───┐  ┌──────▼──────┐ ┌─────▼────┐
   │ .Data    │  │.Persistence│ │ .Net  │  │.Presentation│ │ .UI      │
   │ SO assets│  │ save,      │ │ NGO,  │  │ uGUI board, │ │ UI Toolkit│
   │ card DB  │  │ settings   │ │ relay,│  │ card views, │ │ menus,    │
   │ themes   │  │            │ │ redact│  │ audio, fx   │ │ settings  │
   └────┬─────┘  └─────┬──────┘ └───┬───┘  └──────┬──────┘ └─────┬────┘
        └──────────────┴────────────┼─────────────┴──────────────┘
                    ┌───────────────▼───────────────┐
                    │  CardGame.App                 │
                    │  composition root: bootstrap, │
                    │  DI wiring, scene flow        │
                    └───────────────────────────────┘
```

Arrows point **upward only**. `App` is the single composition root; nothing
references `App`. `Presentation` and `UI` never reference each other. `Net`
never references `Presentation`.

**What keeps this acyclic without a separate Contracts assembly:** service
interfaces live in Core, because they are pure — `IGameSession`, `IEventBus`,
`ISessionTransport`, `ISaveStore`, `IClock`, `ILogSink`, `ICardDatabase`.
`Presentation` calls `IGameSession.Submit(cmd)` and never learns whether the
implementation behind it is a local loopback or an NGO client. **That single
indirection is what makes multiplayer a swap rather than a rewrite.**

### The Core asmdef

```json
{
  "name": "CardGame.Core",
  "rootNamespace": "CardGame.Core",
  "references": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": [ "Newtonsoft.Json.dll" ],
  "autoReferenced": false,
  "noEngineReferences": true
}
```

`noEngineReferences: true` makes the compiler *refuse* `UnityEngine.Debug`,
`Vector2`, `Mathf`, `Random`, `[SerializeField]`, `Coroutine`, `Time.deltaTime`
and `JsonUtility`. Consequences to accept up front:

- Logging goes through `ILogSink`; `App` injects a `UnityLogSink`.
- Time goes through `IClock` — though a turn-based core should mostly not care.
- JSON via Newtonsoft, referenced as a precompiled DLL. This is the only reason
  that package is added.
- `System.Math` covers the arithmetic a card game needs.

`autoReferenced: false` forces every consumer to declare the dependency, so a
stray script in `Assembly-CSharp` fails loudly instead of silently working.

### Test and editor assemblies

| Assembly | Platforms | References |
|---|---|---|
| `CardGame.Editor` | Editor | Core, Data, App, Persistence |
| `CardGame.TestUtils` | `UNITY_INCLUDE_TESTS` | Core |
| `CardGame.Core.Tests` | Editor | Core, TestUtils, nunit — **the fast one** |
| `CardGame.EditMode.Tests` | Editor | + Data, Persistence, Editor |
| `CardGame.PlayMode.Tests` | all | everything |

### Guardrail test — belongs in the first code commit

The invariant will be violated by a well-meaning `Debug.Log` within two weeks.
Automate it:

```csharp
[Test]
public void Core_Assembly_Does_Not_Reference_UnityEngine()
{
    var refs = typeof(GameState).Assembly.GetReferencedAssemblies();
    Assert.That(refs.Select(r => r.Name),
        Has.No.Member("UnityEngine").And.No.Member("UnityEngine.CoreModule"));
}
```

## Core patterns

### 1. Three card types, not two

```
CardDefinitionAsset (Unity)  →  CardDefinition (Core)  →  CardInstance (Core)
authored SO, has Sprite         immutable content        runtime identity+state
designer-facing                 no Unity types           one per physical card
```

Conflating definition and instance is the most common structural mistake in a
Unity card game. Five concrete failures the split prevents:

1. **SO mutation persists in the Editor.** Storing `currentPower` on the asset
   means playing a match permanently edits your content. Silent, and classic.
2. **Four copies of a card need four identities.** Only `InstanceId` expresses
   "the one in hand" vs "the one in the discard".
3. **Netcode sends ids, never assets.** A `Sprite` is meaningless on the server;
   redaction must strip `CardId` while keeping `InstanceId` — impossible if they
   are the same object.
4. **Saves survive content patches.** Saves store `CardId` GUID strings, so
   reordering the card list doesn't corrupt them. Array indices would.
5. **Core stays testable.** `CardDefinition` is one line in a unit test — no
   AssetDatabase, no Editor, no `ScriptableObject.CreateInstance`.

Card **art** never enters Core. `App` hands Core an `ICardDatabase` and hands
`Presentation` a separate `CardId → Sprite` map.

### 2. Commands — one mechanism, four consumers

```csharp
public interface ICommand
{
    CommandTypeId TypeId { get; }   // stable string, e.g. "core.play_card"
    PlayerId Actor { get; }
    ValidationResult Validate(IReadOnlyGameState state, IRuleSet rules);
    void Execute(GameStateMutator state, ExecutionContext ctx);
}
```

Commands must be serializable because four independent features depend on it:

- **Netcode** — the client sends a *command* (~20 bytes), not a state delta, and
  the server validates it with the identical `Validate()` the client used for its
  UI affordances. No rule duplication between client and server.
- **Replay** — `seed + ordered command list` reproduces a full match. Bug reports
  become a 4 KB file; soak tests replay thousands of matches a minute.
- **Undo** — see below.
- **Determinism auditing** — hash state after each command and compare peers.

**Undo is snapshot + replay-to-N, not inverse commands.** Inverse commands are a
lie the moment an effect involves randomness or hidden information, and they
double the surface area of every card. Keep a ring buffer of snapshots at turn
boundaries; undo restores a snapshot and replays commands to N−1. For a
turn-based game this is microseconds and always correct.

Use stable string `CommandTypeId`s, never reflected C# type names, so a refactor
doesn't break stored replays.

### 3. Phase machine — a stack, and blocking decisions

Genre-agnosticism lives here. Genres differ in their phase graph and decision
points, not in the machinery.

```csharp
public sealed class PhaseMachine
{
    // A STACK, not a single current phase. A TCG's response stack, a trick-taking
    // sub-decision, and a deckbuilder's "choose a card to trash" are all pushes.
    IReadOnlyList<PhaseId> Stack { get; }
}

// The core BLOCKS on a decision rather than asking a player object for input.
public sealed class PendingDecision
{
    public PlayerId Decider { get; }
    public string PromptKey { get; }
    public IReadOnlyList<ICommand> LegalChoices { get; }
}

public interface IDecisionProvider   // human UI | AI | network | test script | tutorial
{
    void RequestDecision(PendingDecision d, Action<ICommand> respond);
}
```

`IDecisionProvider` is why AI, network clients, tutorials and tests are
interchangeable. `GameSession` is a **pump** the App calls — it never blocks a
thread and never uses a coroutine.

### 4. Event bus — one-way, core → presentation

```csharp
public sealed record CardMoved(InstanceId Card, ZoneId From, ZoneId To,
                               int ToIndex, PlayerId Owner) : GameEvent;
```

Three rules that stop this rotting:

1. **Events buffer during `Execute` and publish only after the command commits.**
   A subscriber must never observe half-applied state.
2. **Events describe what happened, never what to draw.** There is no
   `PlayCardAnimationEvent`. `CardMoved` is the fact; presentation decides that
   hand→board means a 0.25 s arc.
3. **Presentation drains the queue on the Unity main thread**, via one
   `GameEventPump` in App. Core never calls a Unity API.

Presentation consumes **batches** (`OnCommandApplied(IReadOnlyList<GameEvent>)`),
because one command's events are one animation sequence.

### 5. Deterministic RNG

```csharp
public sealed class Pcg32 : IRandomSource   // ~40 lines. Own it.
public enum RngStream { Shuffle = 1, Effects = 2, Ai = 3, Cosmetic = 4 }
public sealed class RngHub                  // one master seed → N capturable streams
```

**Why `System.Random` is a desync hazard — mechanisms, not hand-waving:**

- **The algorithm is not part of the contract and has changed** between .NET
  Framework, Mono and .NET Core/5+. Unity ships Mono in the Editor and IL2CPP/
  CoreCLR in players. Same seed, different platform, different sequence.
- **`Next(min,max)` derivation is implementation-defined**, so even a matching
  generator can produce different bounded values.
- **It has no capturable state**, so you cannot save mid-match and resume with
  the same upcoming draws, and snapshot-undo is impossible.
- **`UnityEngine.Random` is worse — it's a global static** shared with particle
  systems and third-party assets. A cosmetic `Random.Range` for a card wiggle
  perturbs the next shuffle. Invisible in review; manifests as a desync.
- **The default constructor seeds from time** — two clients constructing "at the
  same moment" is a race, not a guarantee.
- **Stream separation matters**: with one generator, adding a cosmetic call
  anywhere shifts every later gameplay draw and invalidates stored replays.

**The seed is a secret.** Anyone with the shuffle seed can compute the deck
order. Never send it to clients, never log it client-side, never put it in a
client save of a multiplayer match. Generate it server-side from
`RandomNumberGenerator`, not the clock.

### 6. View binding

Core exposes read-only projections; Presentation owns an `InstanceId`-keyed
registry. `BoardBinder` is the **only** class that knows both worlds, and the
only place allowed to turn an event into a tween.

```csharp
public interface ICardView
{
    InstanceId Instance { get; }
    CardId? Definition { get; }   // NULL when hidden from this viewer ← key point
    ZoneId Zone { get; } int ZoneIndex { get; }
}
```

`Definition == null` is exactly what the card view renders as a card back — the
same code path as a face-down card in solitaire. Genre-agnostic for free.

This also gives `Reconcile()` from pure state, so **reconnect and mid-match join
use the same code path as first load**.

## Hybrid UI boundary

> **If the element must be spatially interleaved with, anchored to, or
> drag-and-drop hit-tested against 2D sprite content in the board's sorting
> layers, it is uGUI. Everything else is UI Toolkit.**

Decision procedure — stop at the first yes:

1. Renders **between** sprite sorting layers, or occludes / is occluded by a card? → uGUI
2. Follows a **world position** every frame? → uGUI
3. Is a **drag source or drop target** for cards? → uGUI
4. Needs per-frame **material/shader effects** shared with cards? → uGUI
5. Otherwise → **UI Toolkit**

| uGUI + TMP | UI Toolkit |
|---|---|
| Card faces/backs, hand fan | Main menu, mode select |
| Board zones, drop targets, piles | Settings (all tabs), rebinding UI |
| Floating numbers, card-anchored tooltips | Deck builder, collection browser |
| Turn timer ring on a portrait | Lobby, matchmaking |
| Tutorial spotlight over board elements | Tutorial lesson list and text panels |

**Enforcement, so the boundary doesn't blur:**

- The gameplay scene has exactly one uGUI `Canvas` root for board content and at
  most one `UIDocument` for overlays. Any `UIDocument` there must be full-screen
  and **entirely above or entirely below** the board canvas — never partially
  interleaved. Sort order is set once and never touched.
- `CardGame.UI` must not reference `CardGame.Presentation`, or vice versa. The
  asmdef graph enforces what the prose describes. Needing that reference means
  something is on the wrong side.
- Menu scenes contain zero `Canvas`; the board hierarchy contains zero
  `UIDocument` outside the overlay root. An EditMode test opens each scene and
  counts components.

## Theming across both stacks

One `ThemeDefinitionAsset` is the authority; two thin adapters render it.

- **UI Toolkit** — tokens are USS custom properties (`--color-surface`). A theme
  is a `.uss` redefining only the variable block; `ThemeService.Apply` swaps the
  stylesheet on the root and toggles a root class. Zero per-element code.
- **uGUI** — `ThemeBinder` components (`ThemedGraphic`, `ThemedSprite`,
  `ThemedText`) resolve a token on enable and on `ThemeChanged`. A scene-level
  `ThemeRoot` walks `GetComponentsInChildren<ThemeBinder>(true)` so inactive
  objects are covered too.

**Token drift between the two stacks is the #1 failure mode of hybrid UI.** The
fix: an Editor tool generates the `:root { --… }` block from the theme asset, and
an EditMode test regenerates and compares. A failing test on commit is the
cheapest possible version of this problem.

Card **art** is not a theme token — faces come from the card database. Only
frames, backs, table surface and chrome are themed.

## Netcode

### Why the simplest thing is correct here

A card game is turn-based, takes one input every few seconds, sends ~20 bytes per
action, has strict hidden information, and has zero tolerance for a wrong
outcome. It has **none** of the properties that motivate prediction and rollback.

**Request → validate → confirm. No client-side state prediction. No rollback.
No lockstep.** A 60–120 ms round trip is invisible when picking up a card takes
250 ms of animation.

```
CLIENT                                  SERVER (host)
─────────────────────────────────────────────────────────────────────
Submit(cmd)
 local Validate() for UI affordance
 only — greys out illegal plays,
 NOT authority
 play "pending" ghost visual
      │ SubmitCommandRpc(bytes → Server)
      ▼
                                        CommandCodec.Read(payload)
                                        assert senderId == cmd.Actor  ← spoof guard
                                        cmd.Validate(authoritativeState)
                                        ├ illegal → RejectRpc(seq, code) to sender
                                        └ legal   → Execute → event batch
                                                    per player p:
                                                      Redact(batch, p)
                                                      ApplyEventsRpc → Single(p)
                                        dev builds: StateHashRpc(turn, hash) → all
```

**Two RPCs and one bridge class is the entire netcode surface.**

**Do not replicate `GameState` via `NetworkVariable`/`NetworkList`.** Card game
state is a graph with hidden portions; those give all-or-nothing visibility and
no redaction hook. Replicate the **event stream** (already the animation source
of truth) plus a **full redacted snapshot** on connect/reconnect — reusing
`BoardBinder.Reconcile` from single-player.

### Hidden information — right on day one

Never send what the client shouldn't know. Not "send it and don't render it" — an
obfuscated packet is one cheat away from a wallhack, and it will be found.

- **`CardId` is the secret. `InstanceId` is safe** — an opponent may see *that*
  you hold five cards. Projection replaces `Definition` with `null`.
- **The RNG seed and state are never transmitted.** Deck order derives from them.
- **Deck contents are never transmitted**, only counts — unless the ruleset's
  `IVisibilityPolicy` says composition is public. The policy is the single knob.
- Redaction is **per recipient**: serialize once per player, not once per match.
  At 2–4 players that cost is irrelevant.
- Leak tests assert on **bytes**, not structure — see the testing section.

### Host model

Host-as-server via Relay is right for the alpha: no server cost, NAT traversal
solved, host authority acceptable. `CardGame.Net` talks only to
`ISessionTransport`, so a dedicated server later is a deployment change, not an
architecture change.

**No host migration in the MVP.** On host disconnect, end the match and return to
lobby with a stored result. Host migration for an authoritative
hidden-information game is genuinely hard and is not what "feature-complete
alpha" should mean.

### Desync detection — cheap, do it early

The server hashes canonical state at each turn boundary and broadcasts it;
clients hash their redacted view against a redacted server-side recomputation.
Development builds only. This catches the entire "client and server disagree"
class at the moment of divergence, rather than three turns later when the UI
merely looks wrong.

## Persistence

Two deliberately separate stores:

| | Settings | Game save |
|---|---|---|
| Scope | Device/player | Per match / profile |
| Path | `persistentDataPath/settings.json` | `…/saves/<slot>.save` |
| Format | Flat versioned JSON | `GameSnapshot` JSON + gzip |
| Migration | Field defaults on missing keys | Explicit `ISnapshotMigration` chain |

**Snapshot is primary; the command log is secondary.** Log-only saves ("seed +
commands, replay to restore") are seductive and wrong for a shipping game: they
require perfect determinism across every future version of the rules, so a
balance patch invalidates every save. Snapshots are version-tolerant and
migratable. Keep the command log optional, attached for replays and bug reports
where the rules version is pinned.

**Save the RNG *state*, not the original seed.** Restoring from a seed replays
the draw sequence from turn 1; saving `RngHubState` resumes exactly where it left
off. This is the concrete reason the generator must be capturable.

Save-scumming becomes a **policy lever, not an accident**: reloading reproduces
identical upcoming draws. A mode that wants anti-scum behaviour reseeds the
`Shuffle` stream on load — one explicit, documented line.

**In multiplayer only the server writes match saves.** Clients persist nothing
about a live match — they don't have full state and mustn't. Reconnect is served
by a fresh redacted projection, which is why `Project()` must handle mid-match
join: it is the same function.

Input rebinds persist as the Input System's own
`SaveBindingOverridesAsJson()` string inside the settings file. All writes are
atomic (temp file + `File.Replace`).

## Testing

**EditMode `CardGame.Core.Tests` is the workhorse — target: whole suite under
5 seconds.** Because Core has `noEngineReferences`, tests `new` up rules objects
directly: no scene, no play mode entry, no yielding setup. A full-game simulation
is a constructor call and a loop, so thousands run per test run. This is the
entire payoff of the assembly split.

- Command validation matrices — command × phase × precondition, asserting the
  exact `FailureCode`.
- **RNG golden vectors** — fixed seed → hard-coded first 32 outputs, and a
  fixed-seed 52-element shuffle → exact expected permutation. This test exists
  solely to fail loudly if someone "optimizes" the generator, which would
  silently invalidate every stored replay and save.
- **Determinism** — same seed + command list twice ⇒ identical state hash; then
  serialize → deserialize → continue ⇒ same hash again.
- **Snapshot round-trip and migration**, against checked-in fixture files.
- **Hidden-information leak tests** — `Project(state, playerB)` serialized to
  bytes must not contain any `CardId` string from A's hand or deck. Assert on
  the *bytes*: a structural assertion misses leaks through newly added fields.
- **Fuzz/invariant walker** — a seeded random legal-move chooser plays 10,000
  matches asserting invariants after every command: card count conserved, no card
  in two zones, no negative counters, bounded phase stack, termination within N
  turns. ~60 lines, and it finds rule-interaction bugs no hand-written test will.
  Store failing seeds as regression fixtures.

**EditMode `CardGame.EditMode.Tests`** turns content validation into tests: unique
non-empty card ids, non-null art/audio references, every deck's `CardId`s resolve,
theme USS/SO token parity, the scene component audit, and the
Core-references-no-UnityEngine guardrail.

**PlayMode** is reserved for what genuinely needs a player loop: binder fidelity
(drive a scripted session, assert every `CardVisual` matches `IGameStateView`),
Input System drag/rebinding via `InputTestFixture`, scene flow and subscription
leaks, settings round-trip and theme swap across both stacks, and NGO's
`NetcodeIntegrationTest` for in-process host + 2 clients.

**CI gates merges on EditMode only.** PlayMode and netcode integration run
nightly and on release branches — gate on the slow suite and contributors start
skipping tests.

## Packages to add

**Required**

| Package | Why |
|---|---|
| `com.unity.netcode.gameobjects` | The locked netcode choice. Install *through* the already-present `com.unity.multiplayer.center`, which wires matching companion versions. |
| `com.unity.services.multiplayer` | Unity 6's unified Relay + Lobby + Matchmaker SDK. Prefer over the legacy separate relay/lobby packages. |
| `com.unity.services.authentication` | Anonymous sign-in is a hard prerequisite for Lobby/Relay. |
| `com.unity.nuget.newtonsoft-json` | JSON that works from a `noEngineReferences` assembly. `JsonUtility` is `UnityEngine` and therefore unusable in Core. |
| `com.unity.addressables` | Multiple decks/modes/skins/tutorials is exactly the content-volume problem it exists for. Retrofitting it after content exists is a large, mechanical, miserable refactor. |
| `com.unity.localization` | A language selector is part of "full settings", and the tutorials are text-heavy. Core already stores `NameKey`/`FailureCode` keys — this package is their consumer. |
| `com.unity.multiplayer.playmode` | Multi-instance play from one Editor. Without it every multiplayer iteration costs a build. Biggest single velocity multiplier for the netcode work. |

**Recommended:** PrimeTween (allocation-free, struct-based — matters when 40
cards animate) or DOTween if the team already knows it; wrap either behind a thin
`ICardAnimator` so it stays swappable. Do **not** write a bespoke tween library.
Plus `com.unity.test-framework.performance` once content scales.

**Explicitly not adding:** Cinemachine (a 2D card board needs a static or lightly
tweened camera), DOTS/Netcode for Entities (wrong tool by orders of magnitude for
~60 entities at 0.2 Hz), any rollback/prediction framework.

**Consider removing:** `com.unity.visualscripting` — template cruft that costs
compile time and domain reload for zero benefit here.

## The genre-unknown mitigation

**Pick a public-domain reference ruleset now and implement it against the core as
a permanent test fixture** — Hearts (trick-taking: turn order, following suit,
hidden hands, scoring) or a minimal deckbuilder (deck/discard cycling, buy piles,
shuffle-on-empty). Deliberately choose one *distant* from the likely genre.

An abstract "genre-agnostic core" designed without a real game is a fantasy —
you discover on day 40 that the zone model can't express a trick, or the phase
machine can't express a nested choice. A working reference game proves the
abstractions in week 2 instead. Keep it forever as a conformance suite: when the
real genre arrives it plugs in as a second `IRuleSet` and the reference game
keeps the core honest against over-fitting.

Cost: about a week. It also gives every later phase something real to play.

## The two things that will hurt if skipped

- **Skipping the asmdef skeleton first.** Once code lands in `Assembly-CSharp`
  and cross-references form, extracting a pure core becomes a multi-week
  untangling — and with it go fast tests, safe server authority, and determinism.
- **Deferring netcode to the end.** Hidden information and authority are
  *state-model properties*. Discovering in month 5 that `GameState` assumes every
  client sees everything is a rewrite, not a patch.

Everything else in "feature-complete alpha" is additive and can slip a week
without cascading.
