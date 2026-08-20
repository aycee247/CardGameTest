# Architecture Overview

This document **describes the code that exists**. `docs/game-design.md` is the
requirements authority; this is the structural one.

## The governing rule

> **The rules are a pure C# library that happens to run inside Unity.**

`Assets/_Project/Code/Core/Game.Core.asmdef` sets `noEngineReferences: true`,
`references: []`, `autoReferenced: false`. There is no `using UnityEngine`
anywhere beneath it, and `tools/CoreTests/CoreTests.csproj` proves it by
compiling the same source files against net8.0.

Everything else follows: a 2-second headless test suite, determinism, server
authority, and provable hidden-information safety.

## Assembly graph

| Assembly | Path | References |
|---|---|---|
| `Game.Core` | `Code/Core` | *(none)* — `noEngineReferences` |
| `Game.Data` | `Code/Data` | Core |
| `Game.Audio` | `Code/Audio` | Core, Data |
| `Game.Persistence` | `Code/Persistence` | Core, Data |
| `Game.UI` | `Code/UI` | Core, Data, Audio, TMP, InputSystem |
| `Game.Networking` | `Code/Networking` | Core, Data, NGO, UGS |
| `Game.App` | `Code/App` | all of the above |
| `Game.EditorTools` | `Code/EditorTools` | Core, Data — Editor-only |
| `Game.SceneTools` | `Code/SceneTools` | most — Editor-only |
| `Game.EditModeTests` | `Code/Tests/EditMode` | Core, Data |
| `Game.PlayModeTests` | `Code/Tests/PlayMode` | Core, Data, Networking, App |

No cycles. **`Game.UI` does not reference `Game.Networking`** — that is precisely
what lets one HUD serve hot-seat and online without a branch.

> **Known gap.** `Game.EditModeTests` references only Core and Data, and
> `Tests/PlayMode/` contains an asmdef and **no test files**. So
> `NetworkGameController`, `SessionManager` and `SnapshotCodec` have no automated
> coverage of any kind. The secrecy and disconnect tests prove
> `MatchSnapshot.For` and `SeatRegistry` *in isolation*, never the RPC plumbing
> that calls them.

## The rules core

22 files under `Code/Core`. `RulesEngine` is the single authority over state
transitions.

### State

- `MatchState` — `Config`, `Phase`, `Round`, players, market, draw pile,
  priority order, repick contenders. **No "current player"** — state is
  round-indexed, because play is simultaneous. Mutators are `internal` so only
  `RulesEngine` can drive transitions.
- `PlayerState` — dice pool, Sparks, shape allowance, owned cards, pending
  commit, connection flag. Powers are **derived from `OwnedCards` on every call**
  rather than cached, so they cannot drift.
- `DicePool` (mutable) vs `DiceRoll` (immutable readonly struct with `Sum`,
  `FaceCounts`, `LargestGroup`, `LongestRun`). `Subset(indices)` bridges them.

### The round

`RoundPhase`: `Roll, Shape, Commit, Reveal, Repick, Upkeep, MatchOver` —
**six phases**, and `Repick` is load-bearing.

`RulesEngine` is a static state machine: `BeginRound`, `BeginCommit`,
`BeginReveal`, `ResolveReveal`, `ResolveRepick`, `RunUpkeep`. Each guards on
`if (state.Phase != X) return;`, so driving it out of order is safe.

**Core never ticks.** `MatchConfig` carries the phase durations so the server
timer and the UI agree on one source, but the rules layer is pure and
synchronous. `LocalMatchSession.Advance()` drives it offline; the server clock
drives it online. `RulesEngine.AllDecided` lets a driver close a window early.

Deliberate detail: capacity increases apply at the **start of the next round**,
not when the card is claimed.

### Cards

Powers are **data** — `PowerKind` / `PowerFamily` enums plus a `CardPower`
readonly struct, interpreted by consumers that switch on `Kind`. There is no
`ICardPower` interface and there should not be one (CARD-2).

Costs are the polymorphic half — `ICardRequirement` with `NOfAKind`, `Run`,
`Sum`, `ContainsFaces` and `Composite` matchers.

`StarterDeck` in `CardBlueprint.cs` is the single definition of all 48 cards,
16 per tier, via a fluent `CardDraft` DSL. **Tier 1 deliberately contains no
Capacity cards** — see the balance section of the design doc for why.

`CostChecker` resolves wilds by multiset search rather than teaching every
requirement about wilds; the header documents the C(k+5,5)=1287 bound at 8 dice.

### Contention

`ResolveOnePass` groups committed players by card (ordered for determinism),
sorts each group by priority rank, and awards to the first. **Losers' dice are
never marked spent**, so they enter the re-pick intact. `ResolveReveal` promotes
losers to repick contenders only if the market still holds cards.

Priority = lowest score, then fewest cards, then seat index. Seat index makes the
order total, hence identical on every peer.

## Hidden information

`MatchSnapshot.For(state, observer, seats, now)` builds one snapshot **per
recipient**. Before `Reveal`, `PendingCardId` is `-1` and `PendingDice` empty for
everyone but the owner; shape allowances are observer-only; `CardSnapshot
.AffordableNow` is computed against the observer's own dice.

Public by design: dice faces, owned cards, priority, and `HasDecided`. Reading
that an opponent rolled a pair of 5s is the whole basis for deciding whether to
contest — only the choice is hidden.

**The secrecy gate is the strongest test in the repo.** `SecrecyGateTests` builds
two matches differing *only* in one player's secret commit, renders the other
player's entire snapshot through a reflective field dump, and asserts the strings
are identical — across every card × dice combination and 2–6 players. Because the
dump is reflective, fields added later are covered without anyone remembering to
write a test. It includes a negative control proving the comparison can actually
detect a difference.

Do not weaken this test. If a change makes it fail, the change is the problem.

## Determinism

`IDiceRoller` is the only entropy source in Core. `SeededDiceRoller` is a
hand-rolled **xorshift64\***, portable across platform and runtime.

`System.Random` is unsuitable because its algorithm is not part of the .NET
contract and has changed across versions — the same seed yields different
sequences on Mono, IL2CPP and CoreCLR. `UnityEngine.Random` is worse: a global
static shared with particle systems, so a cosmetic call perturbs the next roll.
There are **zero uses of `UnityEngine.Random`** in the repo.

> **Known gaps.**
> 1. `SeededDiceRoller.Seed` exposes the *live* state but there is no state
>    constructor and no serialization, so a roller cannot be reconstructed
>    mid-match. This blocks save/resume and replay.
> 2. `MatchState` has no serializer — only the lossy per-observer
>    `MatchSnapshot`. An authoritative match cannot be persisted or rehydrated.
> 3. Deck shuffling uses `System.Random` in `MatchFactory` and `CardDatabase`,
>    not the portable xorshift, so deck order is not reproducible from a seed the
>    way dice are.

## Presentation

**uGUI + TextMeshPro only.** Zero UI Toolkit — no `.uxml`, `.uss` or
`UIDocument` exists, and none should be added.

Scenes are **generated by code**: `SceneTools/SceneScaffolder.cs` (menu
**Foundry ▸ Generate Scenes & Build Settings**) builds Boot / MainMenu / Lobby /
Game, wires every serialized reference, and rewrites the build settings list.
`UiFactory` is the only widget builder. The Game scene uses in-scene deactivated
templates for card / die / player-row rather than prefab assets, because prefab
references did not survive save.

> **Operational hazard.** The committed scenes can fall behind the generator. The
> committed `Game.unity` currently predates M4 — it has no `GameSceneBootstrap`,
> no `PlayerRow` template, no `Timer` — and since `HotSeatHost.autoStartOnLoad`
> is `false`, **pressing Play on it produces a dead board.** Re-run both
> generators and commit the results after touching `SceneScaffolder` or
> `StarterDeck`.

### The binding

`Core/IGameActions.cs` declares both halves of the boundary:

- `IGameActions` — `RequestShape`, `RequestCommit`, `RequestPass`, `RequestWithdraw`
- `IMatchView` — `LocalPlayer`, `Current`, `SecondsLeft`, `Changed`, `MoveRejected`

`LocalMatchSession` (offline) and `NetworkGameController` (online) each implement
**both**. `GameSceneBootstrap` picks which to bind at scene load.
`GameHudPresenter` subscribes view events → intents and `Changed` → `Refresh`.
This is the seam that makes multiplayer a swap rather than a rewrite.

Views are passive: `GameHudView` pools `DieView` / `CardButtonView` /
`PlayerRowView` and raises events. No view knows a rule.

## Netcode

Request → validate → confirm. No prediction, no rollback, no lockstep — correct
for a turn-structured game sending ~20 bytes every few seconds.

**Client → server** (`[Rpc(SendTo.Server, RequireOwnership = false)]`):
`RegisterIdentityRpc`, `SubmitShapeRpc`, `SubmitCommitRpc`, `SubmitPassRpc`,
`SubmitWithdrawRpc`.

**Server → client** (`SendTo.SpecifiedInParams`, unicast): `StateRpc`,
`AssignPlayerRpc`, `RejectRpc`.

Hardening, all of it deliberate:

- No intent carries a player id. `TryAcceptIntent` resolves
  `Receive.SenderClientId` → seat, so a client can only act as itself. **Seat
  resolution happens before the rate-limit charge**, so an unmapped peer cannot
  make the server allocate a bucket per forged identity.
- All three server→client RPCs verify `SenderClientId == ServerClientId`.
  Nothing in the transport prevents a peer sending one to another client.
- `RulesEngine.Commit` rejects `diceIndices.Count > player.Dice.Count` **before**
  any allocation or per-index scan.
- `IntentLimiter` is a per-player token bucket (burst 24, sustained 12/s) with an
  explicit guard against a backwards clock. Dropped intents get **no reply** —
  answering would be the amplification being defended against.
- Broadcasts are coalesced to one per frame in `LateUpdate`, because each
  replication costs a snapshot encode *per recipient*.

Wire format is Newtonsoft JSON over UTF-8 (`SnapshotCodec`), one encode per
recipient per broadcast. Accepted until a measurement says otherwise.

### Disconnect

A disconnected player **counts as decided**, so the phase closes immediately
rather than burning the timer on an absent device. Seats are keyed by the **UGS
auth id**, not the transport id, so a returning client with a brand-new transport
id reclaims its seat. The 45-second window governs only what the rail *displays*
(`Reconnecting` vs `left`) — a later return is still accepted, because refusing
would only punish someone whose connection took a while to come back.

**No host migration.** On host loss, clients show the standings from their last
snapshot and say so (NET-4).

> **Known gaps.** `MatchLauncher` never passes `orderedSeatKeys`, so reconnect
> depends on an unenforced race with `RegisterIdentityRpc`; and
> `autoStartOnServer` fires on `Start()` with no ready-up gate, so a late
> scene-loader gets no seat.

## Persistence

`JsonSaveService` writes `PlayerProfile` to
`persistentDataPath/profile.json` with an atomic temp-file swap, falling back to
defaults on a read failure. `GameBootstrap` flushes on iOS pause and quit.

> **Known gap.** Nothing ever mutates the profile — `MarkDirty` and `Save` have
> no call sites outside the service itself. Settings are read once at boot and
> can never be changed; `DisplayName` is ignored in favour of hard-coded
> `"Player 1..N"`.

## Tooling

| Script | What it does | Limitation |
|---|---|---|
| `tools/run-core-tests.sh` | 119 tests, ~2s, no Editor. Compiles the *same* Core + EditMode sources against net8.0 and runs them by reflection. | macOS/Unity-Hub paths; Core only; no machine-readable output |
| `tools/verify-unity-compile.sh` | Type-checks first-party scripts against Unity's DLLs while the Editor holds its lock | **Verifies types, not asmdef boundaries** — one assembly, so an undeclared cross-assembly reference passes here and fails in Unity |

`TestRunner` supports only `[Test]` / `[TestFixture]` / `[SetUp]` / `[TearDown]`
and **exits 2 rather than reporting green** if it finds `[TestCase]`,
`[UnityTest]` or similar. That refusal is the point: silent skipping is worse
than failing.

**There is no CI.** No `.github/`, no workflow, no yml anywhere in the repo.
