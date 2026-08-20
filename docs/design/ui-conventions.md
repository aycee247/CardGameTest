# UI Conventions

The full boundary rule and its rationale live in
`docs/architecture/overview.md`. This document covers day-to-day authoring.

## Which stack

> **If the element must be spatially interleaved with, anchored to, or
> drag-and-drop hit-tested against 2D sprite content in the board's sorting
> layers, it is uGUI. Everything else is UI Toolkit.**

When genuinely unsure, ask: *would this element ever need to be occluded by a
card sprite?* If yes, uGUI.

## uGUI authoring (board)

- One `Canvas` root per gameplay scene for board content
- Card views are prefab variants of a single `CardVisual` prefab
- `CanvasGroup` for fades — never mutate colour alpha per graphic
- All colours and fonts come from `ThemeBinder` components. A hard-coded colour
  fails review and fails the theming CI check.
- Drag uses the Input System, never `Input.*`
- **Every drag has a click-click alternative** — required for accessibility, not
  optional (STORY-13.5)

## UI Toolkit authoring (menus)

- One `.uxml` per screen, under `Assets/UIToolkit/Documents/`
- Shared component styles in `Styles/components/`, never inline
- Colours come from USS variables (`var(--color-surface)`). A literal hex outside
  the generated theme variable block fails review.
- One `PanelSettings` asset per context; sort order is set once and never touched
- Every interactive element is reachable by keyboard and gamepad, with a visible
  focus state

## Text

- **No user-facing string literal in code or scene assets.** All text is a
  localization key. CI fails on violations (STORY-13.1).
- Card body text must stay legible at the minimum supported resolution and at
  80% UI scale
- Test every screen against the pseudo-locale (~40% longer) before calling it done

## Layout

- Support 16:9 through 4:3, 1280×720 through 3840×2160
- Nothing essential in the outer 5% — safe areas matter on mobile and TV
- UI scale range 80%–150% with no clipping

## Animation

- All gameplay tweens go through the animation service, never direct coroutines —
  this is what makes the global speed multiplier and reduced-motion honour
  *every* animation rather than most of them
- Every animation is interruptible; interrupting snaps to the end state
- **Presentation never drives rules.** An animation may be skipped, sped up, or
  disabled entirely and the resulting game state must be byte-identical.
