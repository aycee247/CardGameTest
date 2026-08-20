# CardGameTest Documentation

## Start here

| If you want to… | Read |
|---|---|
| Understand the code structure | [`architecture/overview.md`](architecture/overview.md) |
| Know how we work | [`agile/working-agreement.md`](agile/working-agreement.md) |
| Know when something is finished | [`agile/definition-of-done.md`](agile/definition-of-done.md) |
| See the plan | [`backlog/roadmap.md`](backlog/roadmap.md) |
| Find work to do | [`backlog/epics.md`](backlog/epics.md) |
| Know the game's rules | [`design/gameplay.md`](design/gameplay.md) — **stub** |
| Build a screen | [`design/ui-conventions.md`](design/ui-conventions.md) |
| Add a colour | [`design/theming.md`](design/theming.md) |

`/CLAUDE.md` in the repository root is the condensed working reference. This
directory is the detail behind it.

## Current status

Pre-alpha. The repository is a Unity 6 2D/URP template with no gameplay code;
these documents describe the code that is about to be written.

**The game's rules are not yet defined.** `design/gameplay.md` lists the
questions that need answers. Backlog items are tagged by their dependency on
those answers:

- **`none`** — buildable today
- **`partial`** — framework buildable today, content blocked
- **`blocking`** — cannot start

Roughly two thirds of the alpha is buildable before the rules are settled.
