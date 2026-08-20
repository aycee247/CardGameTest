# EPIC-05 — Front End, Navigation & Screen Flow

**Genre dependency:** none · **Phase:** 3 · **UI stack:** UI Toolkit

## Goal

Every screen outside the match itself, plus the navigation service that moves
between them. A player can boot the game, reach any screen, and back out of it
without a dead end.

## Why it can start now

None of these screens need to know the rules. Mode select lists modes from a
data asset; deck select lists decks from a data asset. Both are empty
collections today and populate themselves when EPIC-04 lands.

---

### STORY-5.1: Boot and initialization sequence
**As a** player **I want** the game to start up cleanly **so that** I reach the
main menu without seeing a broken frame.

- AC1 Given a cold start, when the app launches, then a boot scene initializes
  services in a defined order and hands off to the main menu.
- AC2 Given initialization fails, when a service throws, then the player sees a
  readable error screen, not a frozen splash.
- AC3 Settings are loaded and applied **before** the first visible frame — no
  resolution or volume pop-in.

`none` · Depends on: STORY-1.x foundation · **M**

### STORY-5.2: Screen stack navigation service
**As a** developer **I want** a single navigation service **so that** screens
push, pop and layer consistently instead of each one hard-referencing the next.

- AC1 `Push`, `Pop`, `Replace`, and `PopToRoot` are supported.
- AC2 The hardware/keyboard back action pops the top screen.
- AC3 Modal screens block input to screens beneath them.
- AC4 A screen cannot reference another screen's concrete type — navigation is
  by screen id/asset, not by direct reference.
- AC5 Transitions are async and cannot be double-triggered by a rapid double-click.

`none` · **L**

### STORY-5.3: Main menu
- AC1 Play, Settings, Tutorials, Quit are present and reachable.
- AC2 Fully navigable by keyboard and gamepad; a sensible element is focused on entry.
- AC3 Multiplayer entry point is present but disabled with a clear reason until EPIC-10.

`none` · **S**

### STORY-5.4: Mode select
- AC1 Modes are listed from a `GameModeDefinition` collection, not hard-coded.
- AC2 Locked modes show their unlock condition rather than being hidden.
- AC3 An empty mode list renders an explicit empty state, not a blank panel.

`partial` — framework now, mode content later · **M**

### STORY-5.5: Deck / loadout select
- AC1 Available decks are listed with name, art, and a summary stat line.
- AC2 The last-used deck is preselected.
- AC3 Renders correctly with 0, 1, and 50+ decks.

`partial` · **M**

### STORY-5.6: Pause menu
- AC1 Reachable from a match via Escape / Start at any time input is accepted.
- AC2 Offers Resume, Settings, Restart, Concede/Quit.
- AC3 Pausing halts presentation but never mutates rules state.
- AC4 In a networked match, pause is local-only and does **not** stop the server clock.

`none` · **M**

### STORY-5.7: Results / post-match screen
- AC1 Shows outcome, a summary stat set, and rewards if any.
- AC2 Offers Play Again, Change Deck, and Main Menu.
- AC3 Reads its data from a match-summary object emitted by the core, not by
  querying live game state after the match ended.

`partial` · **M**

### STORY-5.8: Loading and transition screens
- AC1 Any transition over 250 ms shows a loading state.
- AC2 Scene loads are async with a progress indicator that never runs backwards.
- AC3 No transition can strand the player on an unresponsive screen — a timeout
  routes back to a safe screen.

`none` · **S**
