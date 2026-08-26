# Foundry Documentation

[![core-tests](https://github.com/aycee247/CardGameTest/actions/workflows/core-tests.yml/badge.svg)](https://github.com/aycee247/CardGameTest/actions/workflows/core-tests.yml)
[![nightly-playmode](https://github.com/aycee247/CardGameTest/actions/workflows/nightly-playmode.yml/badge.svg)](https://github.com/aycee247/CardGameTest/actions/workflows/nightly-playmode.yml)

**Foundry** (working codename) — a simultaneous-roll dice engine builder for iOS.
2–6 players, 10 rounds, ~12 minutes, real-time online with friends by code.

## Start here

| If you want to… | Read |
|---|---|
| Know the rules and requirements | [`game-design.md`](game-design.md) — **canonical** |
| Understand the code | [`architecture/overview.md`](architecture/overview.md) |
| Find work to do | [`backlog/roadmap.md`](backlog/roadmap.md) |
| Know how we work | [`agile/working-agreement.md`](agile/working-agreement.md) |
| Know when something is finished | [`agile/definition-of-done.md`](agile/definition-of-done.md) |
| Build a screen | [`design/ui-conventions.md`](design/ui-conventions.md) |
| Add a colour | [`design/theming.md`](design/theming.md) |

`/CLAUDE.md` at the repository root is the condensed working reference.

## Status

M1–M5 complete; **M6 (polish) remaining**. Live story status is tracked on the
repo's GitHub Issues board; the path to TestFlight is
[`backlog/roadmap.md`](backlog/roadmap.md). The milestone table in
`game-design.md` §8 is the historical record — never update status in two
places.

Two caveats worth knowing before you trust the milestone table:

1. **Everything is proven under test, not by a player.** M2 awaits its first
   playtest; M3 and M4 say live play is untested; M5 is simulated.
2. **The proof stops at the assembly boundary.** `Game.EditModeTests` references
   only `Game.Core` and `Game.Data`, and `Tests/PlayMode/` holds an asmdef with
   no test files — so the networking layer has no automated coverage at all.

## Running it

```
tools/run-core-tests.sh          # 119 tests, ~2s, no Editor
tools/verify-unity-compile.sh    # type-check Unity assemblies
```

In the Editor: **Foundry ▸ Generate Starter Deck**, then **Foundry ▸ Generate
Scenes & Build Settings**, then open the Game scene and press Play.

> The committed scenes may be behind the generator. If the board does not start,
> re-run both generators — see [`backlog/E0-unblock.md`](backlog/E0-unblock.md).
