# CardGameTest — Roadmap to Feature-Complete Alpha

## Where the project actually stands

Commit #1. The repository is an untouched Unity 6 "2D (URP)" template: no C#
scripts, no card data, no UI, no tests, no assembly definitions. The scene
contains a camera and a light. Everything below is greenfield.

## Target

**Feature-complete alpha** — all systems present and broad content in place:
multiple decks/modes, full settings, skinning, a tutorial series, save/load,
audio, and **online multiplayer inside the MVP**.

## Locked technical decisions

| Decision | Choice |
|---|---|
| MVP bar | Feature-complete alpha |
| UI stack | **Hybrid** — uGUI + TextMeshPro for the board and cards; UI Toolkit for menus and settings |
| Multiplayer | **In the MVP** — Netcode for GameObjects, server-authoritative |
| Genre / rules | **Not yet defined** — see `docs/design/gameplay.md` |

## Sequencing

The ordering below de-risks the two hardest constraints (feature-complete *and*
multiplayer, both from a standing start). It cuts no scope; it only orders it.

```
PHASE 0  Skeleton              EPIC-01            days, not weeks — commit #2
PHASE 1  Core, headless        EPIC-02            pure C#, no Unity, fully tested
PHASE 2  Content pipeline      EPIC-04            unblocks designers early
PHASE 3  Vertical slice        EPIC-03            thin, ugly, real — first "it's a game"
PHASE 4  Netcode, EARLY        EPIC-10            before the rules get broad
PHASE 5  Meta shell            EPIC-05..09        parallel with 3-4, shares no files
PHASE 6  Rules breadth         EPIC-02 cont.      modes, decks, AI — cheap by now
PHASE 7  Teaching & polish     EPIC-11, EPIC-12
PHASE 8  Hardening & release   EPIC-13, EPIC-14
```

### Why netcode is Phase 4 and not Phase 8

This is the single most important ordering decision in the plan, and it is not a
scope cut — EPIC-10 still ships in the alpha. It lands **after** the rules core is
green, and **before** the rules get broad. Both halves matter.

*After the core:* a server-authoritative card game is just the rules core executed
on the server with commands arriving over the wire. If the core is deterministic
and unit-tested first, multiplayer is a transport problem. If netcode is built in
parallel with the rules, every rules bug presents as a desync — the most expensive
class of bug to diagnose in a networked game, because you cannot tell whether
state diverged from a race, a serialization mismatch, or a genuine logic error.

*Before the breadth:* hidden information and authority are **state-model
properties**, not features. Discovering in month 5 that `GameState` assumed every
client sees everything is a rewrite, not a patch. Every rule written after Phase 4
is written against a working authoritative pipeline, so "does this work over the
network" is answered continuously instead of once, late, and catastrophically.

The structural mitigation is in EPIC-01: the rules core assembly carries **no
reference to UnityEngine**. That one constraint is what makes Phase 1 testable in
milliseconds and Phase 4 tractable.

### The genre-unknown hedge (STORY-1.10)

Implement a public-domain reference ruleset — Hearts, or a minimal deckbuilder —
against the core in Phase 0/1, deliberately chosen to be *distant* from the likely
target genre. An abstract "genre-agnostic core" designed without a real game is a
fantasy: you discover on day 40 that the zone model cannot express a trick, or the
phase machine cannot express a nested choice. A working reference game surfaces
those gaps in week 2. It costs about a week, it gives every later phase something
real to play, and it stays permanently as a conformance suite that keeps the core
honest once the real ruleset plugs in beside it.

## Critical path

```
EPIC-01 Foundation
   └─> EPIC-02 Rules core ──> EPIC-03 Play loop ──> EPIC-11 Tutorials
         │                          │
         └─> EPIC-10 Multiplayer    └─> EPIC-12 Graphics & juice
   └─> EPIC-05 Front end ──> EPIC-06 Settings ──> EPIC-07 Theming
   └─> EPIC-08 Persistence
```

`EPIC-05` through `EPIC-08` run in parallel with the rules work — they share no
files with the core, which is the point of the assembly split.

## Genre dependency

Until the game design lands, epics are tagged:

| Tag | Meaning | Epics |
|---|---|---|
| **none** | Buildable today | 01, 05, 06, 07, 08, 09, 13, 14 |
| **partial** | Framework buildable today, content blocked | 04, 10, 11, 12 |
| **blocking** | Cannot start | 02, 03 |

Roughly two thirds of the alpha is buildable before the rules are settled.
