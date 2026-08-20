# EPIC-01 — Project Foundation & Architecture

**Genre dependency:** none · **Phase:** 0 · **Blocks:** everything

## Goal

The assembly skeleton, the core/presentation boundary, and the test harness —
before any gameplay code exists.

## Why this must be commit #2

Once code lands in `Assembly-CSharp` and cross-references form, extracting a
pure rules core becomes a multi-week untangling. Fast tests, safe server
authority, determinism, save/resume and replay all depend on the boundary
existing *first*. This epic is days of work; skipping it costs months.

See `docs/architecture/overview.md` for the full design.

---

### STORY-1.1: Assembly definition skeleton
**As a** developer **I want** the assembly graph in place **so that** architectural
rules are enforced by the compiler instead of by code review.

- AC1 All assemblies exist per the architecture doc: `Core`, `Data`,
  `Persistence`, `Net`, `Presentation`, `UI`, `App`, `Editor`, and the three test
  assemblies plus `TestUtils`.
- AC2 `CardGame.Core.asmdef` has `noEngineReferences: true`, `references: []`,
  and `autoReferenced: false`.
- AC3 Dependencies point upward only; `App` is the sole composition root and
  nothing references it.
- AC4 `CardGame.UI` and `CardGame.Presentation` do not reference each other.
- AC5 The project compiles clean with all assemblies empty except placeholders.

`none` · **M**

### STORY-1.2: Core purity guardrail test
- AC1 A test asserts the Core assembly's referenced assemblies contain no
  `UnityEngine*` entry.
- AC2 The test runs in CI and fails the build on violation.
- AC3 A deliberate violation is confirmed to fail the test before merge.

`none` · **XS** — and among the highest-value stories in the backlog

### STORY-1.3: Folder structure
- AC1 The `Assets/` layout from the architecture doc is created.
- AC2 **No `Resources/` folder exists** — content goes through Addressables or
  direct references.
- AC3 `InputSystem_Actions.inputactions` is relocated to `Assets/Input/`.
- AC4 A README in each top-level folder states what belongs there.

`none` · **S**

### STORY-1.4: Core service interfaces
**As a** developer **I want** the pure service contracts defined in Core **so
that** implementations can be swapped without callers knowing.

- AC1 `IGameSession`, `IEventBus`, `ISessionTransport`, `ISaveStore`, `IClock`,
  `ILogSink`, `ICardDatabase` are defined in Core with no Unity types.
- AC2 `ISessionTransport` has a local loopback implementation.
- AC3 Presentation talks only to `IGameSession` and cannot tell local from
  networked. This is the seam multiplayer depends on.

`none` · **M**

### STORY-1.5: Deterministic RNG
- AC1 `Pcg32 : IRandomSource` implemented with capture/restore.
- AC2 `RngHub` provides independent named streams — Shuffle, Effects, Ai, Cosmetic.
- AC3 **Golden-vector tests**: fixed seed → hard-coded first 32 outputs; a
  fixed-seed 52-element shuffle → exact expected permutation.
- AC4 A lint/CI rule bans `System.Random` and `UnityEngine.Random` from Core and
  from any gameplay path.
- AC5 Fisher–Yates shuffle direction is fixed and documented.

`none` · **M** — see the desync rationale in the architecture doc

### STORY-1.6: Logging and diagnostics
- AC1 `ILogSink` in Core; `UnityLogSink` injected by App.
- AC2 Category-based log levels, configurable per build type.
- AC3 A CI rule fails on `Debug.Log` in Core or in runtime gameplay paths.

`none` · **S**

### STORY-1.7: Composition root and bootstrap
- AC1 `App` wires all services in a defined order at boot.
- AC2 Service construction is explicit — no service locator, no static singletons
  holding game state.
- AC3 A failed service init produces a readable error, not a silent hang.

`none` · **M**

### STORY-1.8: Test harness and fixtures
- AC1 All four test assemblies run green with a smoke test each.
- AC2 `TestUtils` provides state builders and a scripted `IDecisionProvider`.
- AC3 `CardGame.Core.Tests` completes in **under 5 seconds**, and this is
  measured, not assumed.

`none` · **M**

### STORY-1.9: Package installation
- AC1 Add: NGO, `com.unity.services.multiplayer`, authentication, Newtonsoft
  JSON, Addressables, Localization, Multiplayer Play Mode.
- AC2 Add a tweening library and wrap it behind `ICardAnimator` in Presentation.
- AC3 Remove `com.unity.visualscripting`.
- AC4 `packages-lock.json` is committed and the project opens clean.

`none` · **S**

### STORY-1.10: Reference ruleset harness
**As a** developer **I want** a real, public-domain game running on the core
**so that** the abstractions are proven before the real genre arrives.

- AC1 A public-domain ruleset (Hearts, or a minimal deckbuilder) is implemented
  as an `IRuleSet` — deliberately distant from the likely target genre.
- AC2 A full match plays to completion in an EditMode test, twice, with an
  identical state hash.
- AC3 It round-trips through save/load mid-match and continues correctly.
- AC4 It is retained permanently as a conformance suite once the real genre lands.

`none` · **L** — roughly a week, and the highest-leverage hedge against the
unknown genre. An abstract "genre-agnostic core" designed without a real game is
a fantasy; this surfaces the gaps in week 2 rather than on day 40.

### STORY-1.11: Editor conventions and .editorconfig
- AC1 `.editorconfig` encodes the C# conventions in `CLAUDE.md`.
- AC2 Warnings are errors in CI.
- AC3 Force Text serialization and visible meta files are verified as set.

`none` · **XS**
