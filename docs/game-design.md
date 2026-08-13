# Foundry — Game Requirements

> Working codename. Rev 0.1 — 2026-08-13.
> Canonical source of truth for the game design. A formatted read of this document is published as an Artifact.

**Players** 2–6 · **Match length** 10–15 min · **Networking** real-time online, friends by code · **Target** polished TestFlight demo
**Stack** Unity 6.5 (6000.5.0f1) · URP 2D · Netcode for GameObjects 2.x · Unity Gaming Services

A simultaneous-roll dice engine builder for iOS. Everyone rolls at once, every round — the cards you win don't just score, they rebuild how your dice work.

---

## 1. The problem this design solves

A 10–15 minute turn-based dice game played live online is mostly spent watching other people play. With six players and strict turn order, a player is idle ~83% of the match. On mobile, that is where players leave.

Every structural decision below follows from that:

- The game is **simultaneous**. All players roll, shape, and commit at the same time on a shared clock. There are no turns, so nobody waits for one. Player count then costs almost nothing — a six-player match takes the same wall-clock time as a two-player one, which is what makes 2–6 viable.
- Claimed cards **permanently modify your dice** (more dice, re-rolls, wild faces, conversions). Round 1 you have four raw dice and take what you're given; round 10 you have seven dice, two wilds and a free re-roll, and you are assembling a specific result on purpose. That compounding curve is what earns the fifteen minutes.

---

## 2. Core loop — the round

A match is **10 fixed rounds**, not a race to a score. Fixed length keeps duration predictable, gives every player the same number of actions, and stops a runaway leader ending the match before others can use the engine they built.

Each round runs five phases on a server-owned clock:

| # | Phase | Duration | Input | What happens |
|---|-------|----------|-------|--------------|
| 1 | Roll | ~3s | auto | Server rolls every player's dice pool simultaneously. Authoritative, never client-generated. |
| 2 | Shape | 20s | all players | Spend card powers and Sparks to re-roll, nudge, or set dice. |
| 3 | Commit | 15s | all players | Secretly choose one market card and the dice paying for it — or pass. |
| 4 | Reveal | ~8s | all players | Commits flip together. Uncontested claims land; contested go to priority, losers re-pick. |
| 5 | Upkeep | ~4s | auto | Market refills, unspent dice become Sparks, new powers come online, priority recalculates. |

≈50s per round × 10 rounds ≈ 8.5 min, plus lobby and scoring ≈ **11–13 min**.

### Requirements

| ID | Requirement |
|----|-------------|
| CORE-1 | A match is exactly 10 rounds. All players act every round; no player is ever idle waiting for another. |
| CORE-2 | Every phase has a server-owned countdown visible to all players. On expiry the server auto-resolves that player's phase — Shape does nothing, Commit becomes a pass. The clock must be authoritative on the server, or a stalled device holds five other people hostage. |
| CORE-3 | Players start with 4 dice, hard maximum 8. The cap keeps both balance and the phone layout tractable. |
| CORE-4 | Dice spent claiming a card are exhausted for that round. Unspent dice convert to Sparks at Upkeep, so no roll is ever wasted. |
| CORE-5 | A player may commit during **Shape** as well as Commit — locking in early is a legitimate choice. Once committed, shaping is locked, because the committed dice are pledged and re-rolling them would change what is being paid. `Withdraw` takes the commit back and frees the dice again. |

> **Why CORE-5 exists.** It was added during M2. Online, Shape and Commit are separate windows because everyone acts at once. On one device a player does both while holding it, so without this the device has to go round the table twice per round — eight handoffs at four players, eighty in a match. Allowing an early commit collapses that to one pass, and costs nothing online because commits are secret either way.

### Sparks — the soft currency

Sparks exist so a bad roll still produces something.

- Every unspent die becomes **1 Spark** at Upkeep. Cap: **10** held.
- **2 Sparks** re-rolls one die. **4 Sparks** sets one die to a chosen face.
- A player who ends a round with no card receives **3 Sparks** in consolation.

---

## 3. The market — contested claims

Simultaneous play removes downtime but creates a new problem: six players reaching for five cards at the same instant. Rather than engineer that collision away, the design makes it the primary source of player interaction.

Commits are **secret until reveal**. You pick without knowing who else wants it. Uncontested claims land. Where two or more players commit to the same card, **priority** decides, and losers get their dice back for a single 10-second re-pick from what remains.

This is where the reading happens. The strongest card is often the wrong pick precisely because everyone can see it is the strongest — and the priority holder knows they can take it safely, which is information the whole table has.

| ID | Requirement |
|----|-------------|
| MKT-1 | Five cards face-up at all times, refilled from a single deck ordered by tier (T1 on top → T3 at the bottom), so the market escalates naturally without a separate gating system. |
| MKT-2 | A commit names one card and the exact dice paying for it. The server validates the named dice satisfy the requirement; an invalid commit is rejected and treated as a pass. |
| MKT-3 | Contested cards resolve to the highest-priority claimant. Losers enter one re-pick pass; if still contested, priority resolves again and any remaining loser passes. |
| MKT-4 | Priority is held by the player with the **lowest score**, ties broken by fewest cards, then seat order. Recalculated every Upkeep. This is the catch-up mechanism, built into the core loop rather than bolted on. |
| MKT-5 | Any player ending a round with no card gains 3 Sparks. A round can be disappointing but never empty. |

---

## 4. Cards

Every card carries a **dice-pattern cost**, a **persistent power**, and a **VP value**.

Costs reuse the requirement matchers already in `Game.Core` — n-of-a-kind, runs, sums, specific faces, and composites. That layer survives the redesign intact.

Powers fall into five families. The tension between them is the strategic spine: capacity and manipulation make you stronger but score little; scoring cards are worth a lot and do nothing for your engine. Buy upgrades too long and you run out of rounds to cash them in.

| Family | Effect | Example power |
|--------|--------|---------------|
| Capacity | Grows the dice pool | +1 die (max 8) |
| Manipulation | Bends results in Shape | Re-roll 2 dice free each round |
| Wild | Loosens what counts | All 6s count as any face |
| Economy | Feeds the Spark engine | +2 Sparks each Upkeep |
| Scoring | Pays out at match end | +2 VP per Manipulation card owned |

### Representative cards

| Tier | Card | Cost | Power | VP |
|------|------|------|-------|----|
| 1 | Second Cast | one pair | +1 die | 1 |
| 1 | Whetstone | sum ≥ 12 | ±1 to one die each round | 2 |
| 1 | Tally Board | run of 3 | +1 Spark each Upkeep | 1 |
| 2 | Loaded Die | 3 of a kind | One die is wild each round | 3 |
| 2 | Recaster | sum ≥ 20 | Re-roll up to 2 dice free | 3 |
| 2 | Twin Forge | two pairs | +1 die | 3 |
| 3 | Sixes Wild | 4 of a kind | All 6s count as any face | 5 |
| 3 | Grand Array | run of 5 | +2 VP per Manipulation card | 4 |
| 3 | The Overwrite | 5 of a kind | Set one die to any face | 6 |

| ID | Requirement |
|----|-------------|
| CARD-1 | Ship 48 cards across 3 tiers of 16. Enough that no two matches present the same market sequence; small enough to hand-balance without a tuning pipeline. |
| CARD-2 | Card powers are data, not code. A designer adds a card in the editor by picking a cost pattern and a power from the five families — no recompile. |
| CARD-3 | Match ends after Round 10. Winner is highest total of card VP plus end-game scoring powers. Ties break to most Sparks, then most cards. |

---

## 5. Networking — authority and secrecy

Simultaneous secret commits impose a requirement the current architecture does not meet: **players must be sent different views of the same match state.**

Today `GameStateSnapshot` is a single global projection broadcast to everyone. That is correct for a turn-based game with no hidden information. It leaks the entire game here — during Commit, a client holding another player's pending pick trivially wins every contest.

| ID | Requirement |
|----|-------------|
| NET-1 | All dice values originate on the server. Clients send intent only and never generate, predict, or re-derive a roll. |
| NET-2 | Snapshots are filtered per recipient. A pending commit is visible only to its owner until Reveal; others see only that the player has committed. Replaces the global snapshot with `SnapshotFor(PlayerId)`. **This is the load-bearing security change.** |
| NET-3 | A disconnected player has a 45-second reconnect window, auto-passing while the match continues at full speed. Past the window they remain in the match on auto-pass so scoring stays intact. |
| NET-4 | If the host drops, the match ends gracefully with a result screen showing standings at the last completed round. Host migration is explicitly **out of scope** for the demo. |
| NET-5 | The rules layer stays free of Unity and netcode. The same engine runs the online match, local hot-seat, and the headless test suite. |

---

## 6. Interface — six players on a phone

Portrait is primary. Three bands: opponent rail across the top, market through the middle, your dice tray filling the bottom third where thumbs reach.

Simultaneous play changes what the UI must communicate. In a turn-based game the vital question is *whose turn is it*. Here it is *who has locked in and how long do I have* — so commit state and the phase clock are permanent fixtures, not transient toasts.

| ID | Requirement |
|----|-------------|
| UI-1 | The opponent rail shows per player: name, score, dice count, committed/thinking indicator. Must stay legible at six players on the narrowest supported device. |
| UI-2 | Phase name and remaining seconds visible at all times during input phases, with escalating urgency in the final 5 seconds. |
| UI-3 | Tapping a market card highlights exactly which of your dice would pay for it and grays those that cannot contribute. Cost legibility is the difference between a strategy game and a guessing game. |
| UI-4 | Reveal is staged as a deliberate beat — commits flip together, contests resolve visibly, priority is shown deciding. This is the emotional peak of the round and must not be instant. |
| UI-5 | Every active power a player owns is visible on their own screen without navigating away. By round 10 a player may hold six cards whose powers stack. |
| UI-6 | Safe-area insets and both orientations respected; existing `SafeAreaFitter` and `OrientationWatcher` carry over unchanged. |

---

## 7. Codebase impact

The existing scaffold is a turn-based game. Moving to simultaneous rounds invalidates the state model and rules engine, but leaves the foundations and the whole networking/presentation stack intact.

### Carries over (unchanged or nearly so)

- `DiceRoll` — immutable roll with counts, runs, sums. Exactly what Shape and cost validation need.
- `ICardRequirement` and all five matchers — these become card **costs** verbatim.
- `IDiceRoller` / `SeededDiceRoller` — deterministic server rolling, already correct.
- `PlayerId`, `CardId`, `Card` — identity and the rules-layer card view.
- Assembly layering, including `noEngineReferences` on `Game.Core`.
- `SessionManager`, transport, UGS sign-in, join-by-code — networking plumbing is orthogonal to the rules.
- `SafeAreaFitter`, `OrientationWatcher`, audio and persistence layers.

### Rewritten (turn-based assumptions that no longer hold)

- `GameState` — `CurrentPlayerIndex`, `AdvanceTurn`, `RollsUsedThisTurn` all disappear. State becomes round-indexed with per-player Sparks, owned powers, and pending commits.
- `GamePhase` — `AwaitingRoll`/`Rolled` becomes the five-phase round clock.
- `RulesEngine` — per-player turn validation gives way to collecting all commits, validating each, and resolving contention by priority.
- `GameStateSnapshot` — one global projection becomes `SnapshotFor(PlayerId)` with hidden-information filtering.
- `IGameActions` — Roll/Claim/EndTurn becomes Shape/Commit/Pass.
- `LocalGameSession` — must drive all seats simultaneously rather than one at a time.

### New (no equivalent exists yet)

- Card power system — data-driven `ICardPower` applying at Shape, Upkeep, or scoring.
- Spark economy — earn, cap, spend on dice manipulation.
- Priority calculation and the contention resolver, including the re-pick pass.
- Server-owned phase timers with automatic resolution on expiry.
- Auto-pass agent covering disconnected and timed-out players.
- End-game scoring pass for cards that pay out at match end.

> **Sequencing note.** The rewrite is confined to `Game.Core` and the snapshot boundary. Because that assembly has no Unity or netcode references, the entire redesign can be built and tested headlessly before the Unity editor is opened. That is the fastest available path: get the rules and contention resolver correct under unit test, then bind the existing presentation layer to a shape that is already proven.

---

## 8. Milestones

Ordered so the riskiest unknown — whether contested simultaneous claims are actually fun — is answered before any networking work is committed to.

| ID | Milestone | Gate | Scope |
|----|-----------|------|-------|
| M1 | Rules core | ✅ **done** — 67 tests green | Round clock, Spark economy, powers, priority, contention resolver. Pure C#, fully unit-tested, no editor required. |
| M2 | Hot-seat playable | ✅ **built** — awaiting first playtest | All seats on one device via `HotSeatDirector`. Answers the fun question before any netcode is trusted. |
| M3 | Online duel | ✅ **secrecy proven** — live play untested | Per-player filtered snapshots, hardened RPCs, and a differential secrecy gate. What remains is two devices over Relay. |
| M4 | Full table | 6 players, drops survived | Scale to six, server phase timers, disconnect and auto-pass handling, opponent rail at full width. |
| M5 | Content and balance | no dominant strategy | All 48 cards authored and tuned across player counts. Round count confirmed or adjusted against real match times. |
| M6 | Polish | TestFlight build | Reveal choreography, dice and claim animation, audio, first-time-player onboarding. |

---

## 9. Building and testing

### Running the rules tests

`Game.Core` is compiled with `noEngineReferences`, so the whole rules layer builds and tests without opening Unity:

```
tools/run-core-tests.sh              # whole suite, ~2s
tools/run-core-tests.sh Contention   # only matching fixtures/methods
```

It uses the .NET SDK and `nunit.framework.dll` bundled inside the Unity installation — no network, no separately installed `dotnet`. `tools/CoreTests` compiles **the same source files** the Unity assemblies do (it does not fork them) and runs the same NUnit `[Test]` methods via reflection, so the headless run and Unity's Test Runner execute one suite, not two.

The runner deliberately supports only `[Test]`, `[TestFixture]`, `[SetUp]` and `[TearDown]`. If a test uses anything richer (`[TestCase]`, `[UnityTest]`, …) it refuses to run rather than silently skipping and reporting green.

### Type-checking the Unity assemblies

Unity holds an exclusive lock on the project, so `Unity -batchmode -runTests` is unavailable whenever the Editor is open — which is most of the time. This compiles every first-party script against Unity's managed DLLs instead:

```
tools/verify-unity-compile.sh
```

It gathers references three ways, because Unity uses three mechanisms: `<HintPath>` for engine DLLs, `Library/ScriptAssemblies` for package and first-party assemblies (TMPro, uGUI, Netcode), and plain DLLs inside `Library/PackageCache` (Newtonsoft). It needs Unity's generated `Game.*.csproj` files to exist.

**It verifies types, not asmdef boundaries.** Everything compiles into one assembly, so a script reaching across an assembly reference it does not declare passes here and fails in Unity. Green means "no type errors", not "Unity is happy".

### Playing a hot-seat match

1. **Foundry ▸ Generate Starter Deck** — 36 cards over three tiers, plus the `CardDatabase`. It validates as it goes and logs an error for any cost no legal dice pool could pay.
2. **Foundry ▸ Generate Scenes & Build Settings** — rebuilds Boot / MainMenu / Lobby / Game.
3. Open the **Game** scene and press play. `HotSeatHost` starts a match immediately; set `playerCount` on it for 2–6 seats, or `fixedSeed` to replay an exact match.

### Playing online

Boot → MainMenu → **Host** (shows a join code) or **Join** by code → Lobby → host presses **Start**, which loads the Game scene for everyone over NGO.

`GameSceneBootstrap` decides the mode when the Game scene loads: if a network session is live it binds the board to `NetworkGameController` and hides the hot-seat handoff panels; otherwise it starts a hot-seat match. Both modes share the entire presentation layer, because `LocalMatchSession` and `NetworkGameController` implement the same `IGameActions`/`IMatchView` pair. The only difference is who advances the phases — the player, or the server's clock.

Online needs UGS configured: **Edit ▸ Project Settings ▸ Services**, link a project, then enable Authentication, Relay and Lobby in the Unity Cloud dashboard.

### Flow, and where it lives

`HotSeatDirector` is pure C# in `Game.Core` and carries the whole pass-the-device state machine — seat queue, handoff, reveal, re-pick passes. `HotSeatHost` is a thin MonoBehaviour over it. That split is deliberate: the awkward parts (a re-pick pass only some players may join; the privacy boundary between seats) are covered by the headless suite rather than only being exercisable by hand.

The handoff screen is load-bearing, not decoration. The director moves the private view to the next actor *before* the handoff panel goes up, and the panel is opaque and full-screen, so the previous player's dice and claim are out of the snapshot and off the screen before the device changes hands.

### How the secrecy gate is proven

`SnapshotSecrecyTests` asserts particular fields are hidden — that catches the leaks someone thought of. `SecrecyGateTests` asserts something stronger:

> If two matches differ **only** in what an opponent secretly committed to, everything the other player receives must be identical.

If your view cannot distinguish their choices, there is nothing in it to exploit — no field, no array length, no ordering, and no field added later. Snapshots are compared by a full reflective dump rather than hand-written assertions, so new state is covered without anyone remembering to write a test for it. It runs across every card × dice combination and every player count from 2 to 6.

The suite includes a **negative control** (`TheComparisonCanActuallyDetectADifference`): at Reveal the commits are public, so the two views must differ. Without it, the gate would pass just as happily if the dump were empty.

Deliberately *not* secret, and asserted as such: dice faces, owned cards, priority order, and the fact that a player has decided. Reading that an opponent rolled a pair of 5s is the whole basis of deciding whether to contest a card; only the choice itself is hidden.

### Hostile-client hardening

- Intents are mapped sender → seat, so a client can only ever act as itself.
- The three server-to-client RPCs verify the sender really is the server. Nothing in the transport prevents a peer sending one directly to another client, which would otherwise let it forge state, reassign a seat, or fake a rejection.
- A commit offering more dice than the player holds is rejected before any per-index work, so a hostile client cannot make the server scan an arbitrarily large array.
- Everything else is already gated by `RulesEngine`, which validates phase, ownership, dice validity and cost on the server.

Not yet addressed: no rate limiting on intent RPCs. A client can spam valid actions and force a re-broadcast each time.

### Still open

- **UI-1 is only partly met.** The standings rail is a formatted text block, not per-player chips. Fine for hot-seat, wants revisiting for online at six players.
- **UI-2, the phase countdown, is wired but only visible online.** Hot-seat has no clock, so `SecondsLeft` is negative there and the label hides itself.
- **UI-4, the reveal beat, is a static list.** It says who won and lost what; it does not animate.
- The deck is 36 cards, not the specced 48, and is unbalanced by design until M5.

---

## 10. Open questions

**What is the game actually about?**
Foundry is a working codename chosen to fit the engine-building metaphor. Theme, setting and art direction are undecided. They will reshape card names and reveal choreography — but not a single rule above.

**Are Sparks one system too many?**
They exist to stop bad rolls feeling dead. If M2 playtesting shows the dice-pattern economy already carries that weight, Sparks should be cut rather than tuned.

**Is 10 rounds right at every player count?**
Six-player rounds resolve slower in practice because contention triggers re-picks more often. The round count may need to scale with table size to hold the 10–15 minute target.

**Does lowest-score priority over-correct?**
Handing first pick to the trailing player is a strong rubber band. If it makes leading feel punishing, the fallback is rotating priority with a smaller consolation bonus.
