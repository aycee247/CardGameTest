# Agile Working Agreement

This is the process contract for CardGameTest. It is deliberately lightweight —
the team is small and the codebase is young. Rules here are binding; everything
else is a preference.

## 1. The hierarchy

```
Epic        A capability that delivers player-visible value. Weeks of work.
 └─ Story   A vertical slice a player could describe. 1–3 days.
     └─ Task Implementation step. Hours. Lives in the PR, not the backlog.
```

**Rule:** a Story must be phrased from the player's or developer's point of view
and must be independently demonstrable. "Add a class" is a Task, not a Story.
If you cannot demo it, it is not a Story.

### Story format

```
STORY-<epic>.<n>: <short title>   e.g. STORY-3.1

As a <role>
I want <capability>
So that <benefit>

Acceptance Criteria
  AC1  Given <context>, when <action>, then <observable outcome>
  AC2  ...

Epic: E0 | E2 | E3 | E4 | E5 | E6
Implements: CORE-n, UI-n, ...   (design-doc requirement ids, if any)
Depends on: STORY-x.y, ...
Estimate: XS | S | M | L | XL
```

Stories carry their **epic id** (`E0`–`E6`) and their dependencies. The rules are
fully specced in `docs/game-design.md` — cite its requirement ids (CORE-n, MKT-n,
CARD-n, NET-n, UI-n) in acceptance criteria wherever a story implements one.

## 2. Estimation

Relative sizes, not hours. Anchor: **S = one focused day.**

| Size | Meaning |
|---|---|
| XS | Trivial, mechanical. Config, a rename, one asmdef. |
| S  | One clear change, obvious approach, tests are easy. |
| M  | Several files, some design judgement, needs a test plan. |
| L  | New subsystem or crosses the core/presentation boundary. |
| XL | **Too big — split it.** An XL in the backlog is a bug in the backlog. |

## 3. Definition of Ready

A story may not be started until:

- [ ] Acceptance criteria are written and testable
- [ ] Dependencies are listed and are already `Done`
- [ ] It is `L` or smaller
- [ ] The assembly it belongs in is decided — and if that is `Game.Core`, it
      carries no Unity types
- [ ] Any design-doc requirement it implements is cited by id

## 4. Definition of Done

See `docs/agile/definition-of-done.md`. A story is not Done because the code
works. It is Done when it meets that checklist in full.

## 5. Branching and commits

- One branch per story: `feat/E3.1-reveal-choreography`,
  `fix/E2.8-per-frame-hud-refresh`
- Branch from the default branch; never stack onto a merged branch
- Conventional-style commit subjects, imperative mood:
  `feat(ui): stage the reveal beat`
- Reference the story id in the commit body, not the subject
- **Never commit Unity `Library/`, `Temp/`, `Logs/`, or `.csproj`/`.sln` output** —
  `.gitignore` already covers these; do not override it

## 6. Unity-specific process rules

These exist because Unity punishes teams that ignore them.

- **Scenes are generated, not authored.** `SceneScaffolder` builds all four.
  Never hand-edit a generated scene — change the scaffolder, regenerate, and
  commit the result. A committed scene that has fallen behind the generator has
  already broken the board once.
- **Regenerate after touching `SceneScaffolder` or `StarterDeck`**, and commit
  the regenerated scenes and card assets together.
- **Force Text serialization and visible meta files are mandatory** and already
  set. Never change them.
- **Never delete a `.meta` file by hand.** Delete assets through the Unity
  editor, or delete the asset and its meta together.
- **A `.meta` file for a new asset is part of the commit.** A missing meta file
  reassigns the GUID on the next import and silently breaks every reference.
- Assembly definition changes require a full recompile — batch them.

## 7. Ceremonies

Kept minimal:

- **Sprint length: 2 weeks.**
- **Planning** — pull from the top of the ranked backlog; only Ready stories.
- **Demo** — every sprint ends with a build that runs. A sprint with no
  runnable build is a failed sprint, regardless of story points closed.
- **Playtest** — the project's standing risk is that everything is proven under
  test and unproven by a player. Put a human on the build every sprint.
- **Retro** — 15 minutes, three columns: keep / drop / try.

## 8. Backlog hygiene

- The backlog is ranked, not prioritized into buckets. There is exactly one
  "next story."
- Anything not touched in 3 sprints gets deleted or moved to
  `docs/backlog/icebox.md`. Stale backlogs hide real work.
