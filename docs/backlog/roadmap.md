# Roadmap — from M5 to TestFlight

## Where the project stands

Milestones M1–M5 are complete per `docs/game-design.md`. The rules core is
production quality: 119 headless tests, a balance harness with a dominance gate,
and a reflective secrecy gate. **M6 (polish) is the remaining specced milestone.**

Two things qualify that picture, and they shape the whole plan:

1. **Everything is proven under test, not by a human.** M2 says "awaiting first
   playtest", M3 and M4 say "live play untested", M5 says "simulated, not
   played." Three consecutive milestones have never met a player.
2. **The proof stops at the assembly boundary.** `Game.EditModeTests` references
   only `Game.Core` and `Game.Data`; `Tests/PlayMode/` holds an asmdef and no
   test files. So `NetworkGameController`, `SessionManager` and `SnapshotCodec`
   have **zero automated coverage**. The M3/M4 claims are proven for
   `MatchSnapshot.For` and `SeatRegistry` in isolation — never through the RPC
   plumbing that calls them.

## Phases

```
P0  Unblock            the board does not currently start — fix this first
P1  Docs truth pass    align the planning docs with the code
P2  Cover + validate   test the untested seam, then play the game
P3  M6 polish          reveal beat, animation, audio, onboarding
P4  Settings           requested; absent from the milestone plan
P5  Skinning           requested; absent from the milestone plan
P6  Ship               CI, iOS readiness, TestFlight
```

```
P0 ─→ P1
 └──→ P2 ─→ P3 ─┐
      P4, P5 ───┴─→ P6
```

P0 gates everything. P2 gates P3, because polishing a reveal beat that
playtesting says to restructure is wasted work. P4 and P5 both touch
`SceneScaffolder`, so they belong alongside P3 rather than in parallel with it.

## Epics

| ID | Epic | Phase | Detail |
|---|---|---|---|
| E0 | Unblock the build | P0 | [file](E0-unblock.md) |
| E1 | Documentation truth pass | P1 | ✅ done |
| E2 | Netcode coverage & live validation | P2 | [file](E2-validation.md) |
| E3 | M6 polish | P3 | [file](E3-polish.md) |
| E4 | Settings | P4 | [file](E4-settings.md) |
| E5 | Theming & skinning | P5 | [file](E5-skinning.md) |
| E6 | Ship | P6 | [file](E6-ship.md) |

CI for the core suite lives in **E0** (STORY-0.6), not E6 — it needs no Unity
licence and protects everything after it. E6 keeps only the nightly PlayMode
lane.

## What is deliberately not in scope

- **Host migration** — explicitly out of scope for the demo (NET-4).
- **Localization** — the package is not installed and no strings are keyed.
- **Save/resume of a match in progress** — blocked on three Core gaps
  (restorable RNG state, `MatchState` serialization, portable deck shuffle).
  Tracked in E6 as optional; nothing in the demo needs it.
- **Art direction** — undecided per §11 of the design doc. E5 builds the system
  against placeholder art so that choosing a direction later is a content change.
