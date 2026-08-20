# EPIC-08 — Persistence: Save, Load & Profiles

**Genre dependency:** none · **Phase:** 3

## Goal

Nothing the player earns or configures is ever lost — including to a crash, a
power cut, or a game update that changes the data format.

## Design constraints

- **Atomic writes.** Write to a temp file, flush, then move into place. A crash
  mid-save must never corrupt the existing file.
- **Versioned schema with migrations.** Every save carries a version number, and
  loading an older version runs forward migrations in sequence.
- **Never use `PlayerPrefs` for anything meaningful.** It is unversioned,
  unmigratable, size-limited and platform-inconsistent. Settings and profile
  data live in JSON files under `Application.persistentDataPath`.
- Saves are **not** trusted input in multiplayer — the server never accepts
  client-supplied game state.

---

### STORY-8.1: Save system core
- AC1 Generic save/load with atomic writes and a `.bak` of the previous good file.
- AC2 Every payload carries a schema version.
- AC3 A corrupt file falls back to the backup, then to defaults, and reports which.
- AC4 All file I/O is async and never stalls the main thread.

`none` · **L**

### STORY-8.2: Schema migration framework
- AC1 Migrations are registered per version step and run in order on load.
- AC2 A save two or more versions old migrates through every intermediate step.
- AC3 A save **newer** than the build refuses to load with a clear message rather
  than silently discarding fields.
- AC4 Migrations are unit-tested against captured fixture files.

`none` · **M**

### STORY-8.3: Player profile
- AC1 Stores display name, unlocks, currency if any, and cosmetics selection.
- AC2 Multiple local profiles are supported and switchable.
- AC3 Deleting a profile requires explicit confirmation.

`none` · **M**

### STORY-8.4: Statistics tracking
- AC1 Aggregate stats (games played, win rate, per-mode and per-deck breakdowns).
- AC2 Stats are fed from the core's event stream, so tracking needs no rules changes.
- AC3 Viewable on a stats screen.

`partial` — the interesting stats depend on the rules · **M**

### STORY-8.5: Match resume
- AC1 An in-progress single-player match survives quitting the app.
- AC2 Resume restores the exact game state, **including the RNG seed and draw
  position** — a resumed match cannot be used to reroll a bad draw.
- AC3 Resume is offered on boot when a match is in progress.
- AC4 Networked matches are explicitly out of scope here; reconnect is EPIC-10.

`partial` · Depends on: EPIC-02 · **L**

### STORY-8.6: Cloud save readiness
- AC1 The storage layer sits behind an interface so a cloud backend can be added
  without touching callers.
- AC2 A conflict-resolution hook exists even though the alpha resolves locally.

`none` · **S**
