# EPIC-06 — User Settings

**Genre dependency:** none · **Phase:** 3 · **UI stack:** UI Toolkit

## Goal

A complete, persisted settings system reachable from the main menu and the
pause menu, applied at boot before the first visible frame.

## Design constraints

- One `SettingDefinition` model drives both the UI and persistence. Adding a
  setting must not require touching three files.
- Every setting has a **default**, and "Reset to defaults" is per-tab and global.
- Settings apply **live** where possible; where a restart is genuinely required
  (rare), the UI says so explicitly rather than silently deferring.
- Settings are player-scoped and travel with the profile (EPIC-08).

---

### STORY-6.1: Settings data model and persistence
**As a** developer **I want** settings defined as data **so that** adding one is
a single change and the UI builds itself.

- AC1 A setting declares: id, category, type (bool/enum/float/int/binding),
  default, range, and whether it applies live.
- AC2 Values persist across restarts and survive an unclean shutdown.
- AC3 An unknown/removed setting id in a saved file is ignored, not fatal.
- AC4 A missing settings file produces defaults rather than an error.

`none` · **M**

### STORY-6.2: Settings screen shell with tabbed categories
- AC1 Tabs: Video, Audio, Gameplay, Controls, Accessibility.
- AC2 The screen is generated from the setting definitions, not hand-authored per control.
- AC3 Fully keyboard and gamepad navigable, including tab switching.
- AC4 Unsaved-changes prompt on exit where a setting is not applied live.

`none` · **L**

### STORY-6.3: Video settings
- AC1 Resolution, display mode (fullscreen/borderless/windowed), target monitor,
  V-Sync, frame-rate cap, render scale.
- AC2 Resolution changes show a **15-second revert countdown** and auto-revert if
  not confirmed — protects against an unusable display mode.
- AC3 Settings are clamped to what the current hardware actually reports.

`none` · **M**

### STORY-6.4: Audio settings
- AC1 Master, Music, SFX, UI, and Voice sliders, each mapped to a mixer group.
- AC2 Sliders use a logarithmic curve — a linear volume slider feels wrong.
- AC3 Adjusting a slider plays a preview sound from that category.
- AC4 Mute-on-focus-loss toggle.

`none` · Depends on: EPIC-09 · **S**

### STORY-6.5: Gameplay settings
- AC1 Animation speed (including an instant option), auto-pass/auto-confirm
  behaviour, confirmation prompts, tooltip verbosity, card text size.
- AC2 Each option has a one-line explanation — no unexplained jargon.
- AC3 Gameplay settings are **presentation-only** and can never alter rules
  outcomes; anything that would is a game mode, not a setting.

`partial` — the option set firms up with the rules · **M**

### STORY-6.6: Controls and rebinding
- AC1 Every action is rebindable for keyboard, mouse and gamepad via the Input
  System's interactive rebinding.
- AC2 Conflicting bindings are detected and surfaced before they are accepted.
- AC3 Per-device reset to defaults.
- AC4 Rebindings persist and reapply at boot.
- AC5 The rebinding UI listens for the next input rather than requiring the
  player to know a key's internal name.

`none` · **L**

### STORY-6.7: Accessibility settings
- AC1 Colour-blind mode (protanopia/deuteranopia/tritanopia palettes),
  UI scale, reduced motion, high-contrast card text, hold-vs-toggle inputs,
  and a disable-screen-shake toggle.
- AC2 Reduced motion is honoured by **every** animation, including tutorials
  and transitions — enforced by routing all tweens through one service.

`none` · Depends on: EPIC-13 · **M**

### STORY-6.8: Settings applied at boot
- AC1 All persisted settings apply before the main menu renders.
- AC2 A corrupt settings file falls back to defaults and reports it once.

`none` · **S**
