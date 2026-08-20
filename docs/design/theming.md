# Theming

One `ThemeDefinitionAsset` is the authority. Two thin adapters render it — USS
variables for UI Toolkit, `ThemeBinder` components for uGUI.

## Token naming

Tokens are **semantic**, describing role rather than appearance:

```
✅  surface.base      surface.raised     surface.overlay
    text.primary      text.muted         text.inverse
    state.danger      state.success      state.disabled
    accent.primary    accent.secondary
    card.frame        card.back          board.felt

❌  offWhite    blue2    darkGrey    theRedOne
```

A literal token (`offWhite`) survives exactly until the first redesign. A
semantic one (`text.muted`) survives the redesign unchanged — that is the whole
point.

## Adding a token

1. Add it to `ThemeDefinitionAsset` and assign a value in **every** shipped theme
2. Regenerate the USS variable block via the Editor tool
3. Consume it — `var(--color-text-muted)` in USS, or a `ThemeBinder` in uGUI
4. The parity test confirms both stacks agree

## The failure mode this system exists to prevent

**Token drift between the two stacks is the number one failure mode of hybrid
UI.** The menus slowly stop matching the board, and nobody notices until a
screenshot goes out.

The defence is mechanical, not diligence-based: the Editor tool *generates* the
USS `:root { --… }` block from the theme asset, and an EditMode test regenerates
and compares. A failing test on commit is the cheapest possible version of this
problem. If full generation is deferred, the minimum viable version is a parity
test asserting the token key set in the asset equals the `--` variable set in the
USS.

## What is not themed

**Card art is not a theme token.** Card faces come from the card database and are
content, not chrome. Only frames, backs, table surface, and UI chrome are themed —
otherwise a theme change silently reskins the game's actual content.

## Shipped themes

| Theme | Purpose |
|---|---|
| `default` | The primary look |
| `dark` | Low-light play |
| `highcontrast` | Accessibility (EPIC-13) |
| `protanopia` / `deuteranopia` / `tritanopia` | Colour-blind palettes |

Colour-blind support ships as themes rather than as a shader post-process,
because the palettes need per-token judgement, not a global filter.

## Rule that makes it all work

> **No component anywhere holds a colour, font, or chrome sprite of its own.**

Enforced by CI: a themed uGUI graphic without a binder fails, and a literal hex
in USS outside the generated block fails.
