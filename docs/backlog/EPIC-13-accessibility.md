# EPIC-13 — Accessibility & Localization

**Genre dependency:** none · **Phase:** 6

## Goal

The game is playable by people who cannot see certain colours, cannot read small
text, cannot use a mouse, or do not read English. Built in, not retrofitted.

## Why this is its own epic and not a settings tab

Accessibility is mostly *enforcement*, not features. The settings toggles are
cheap (EPIC-06); making every screen actually honour them is the work. Doing it
late means auditing every screen twice.

---

### STORY-13.1: Localization infrastructure
- AC1 `com.unity.localization` installed and configured.
- AC2 **No user-facing string literal exists in code or scene assets** — a CI
  check fails the build if one appears.
- AC3 Language is switchable at runtime with no restart.
- AC4 Missing keys render a visible placeholder in dev builds, never an empty string.

`none` · **L**

### STORY-13.2: String extraction and pseudo-localization
- AC1 All strings are in string tables with translator context notes.
- AC2 A pseudo-locale (accented, ~40% longer) exercises layout overflow.
- AC3 Every screen is verified against the pseudo-locale — no clipping, no ellipsis
  on essential information.

`none` · **M**

### STORY-13.3: Colour-blind safe design
- AC1 No game state is communicated by colour alone — shape, icon or text always
  co-signals it.
- AC2 Protanopia/deuteranopia/tritanopia palettes ship as themes (EPIC-07).
- AC3 Verified with a colour-blindness simulator.

`partial` · **M**

### STORY-13.4: Text scaling and readability
- AC1 UI scale from 80% to 150% with no clipping on any screen.
- AC2 Card text remains legible at the minimum supported resolution.
- AC3 A high-contrast text mode is available.

`none` · **M**

### STORY-13.5: Full input accessibility
- AC1 Every action is reachable by keyboard alone and by gamepad alone.
- AC2 No action requires a click-and-drag — every drag has a click-click alternative.
- AC3 No action requires a timed or repeated input.
- AC4 Focus is always visible and never lost.

`none` · **L**

### STORY-13.6: RTL and CJK readiness
- AC1 TMP fonts include the required glyph ranges, with fallback chains.
- AC2 Layouts mirror correctly for RTL.
- AC3 CJK line-breaking is correct in card text.

`none` · **M**
