# EPIC-07 — Theming & Skinning

**Genre dependency:** none · **Phase:** 3 · **UI stack:** both

## Goal

One source of truth for the game's visual identity, driving **both** UI stacks,
switchable at runtime with no restart and no hard-coded colour anywhere in the
codebase.

## The problem this epic solves

The hybrid UI decision means two rendering systems style themselves in
completely different ways: UI Toolkit uses USS stylesheets, uGUI uses per-
component serialized fields. Left alone, that guarantees two drifting palettes.
The fix is a single `ThemeAsset` that is the authority, with two thin adapters.

```
                   ThemeAsset  (ScriptableObject — the authority)
                   ├── palette, typography, spacing, radii
                   ├── card back, board surface, table felt
                   └── named semantic tokens (not raw colours)
                          │
          ┌───────────────┴───────────────┐
          ▼                               ▼
   UI Toolkit adapter              uGUI adapter
   generates / swaps USS           ThemeBinder components
   variables on the root           resolve tokens at bind time
```

**Rule:** components reference *semantic tokens* (`surface.raised`,
`text.muted`, `state.danger`), never raw colours. A designer changing a brand
colour edits one asset; nobody greps for hex codes.

---

### STORY-7.1: Theme asset and token vocabulary
- AC1 A `ThemeAsset` defines the full token set: colour, typography, spacing,
  corner radii, and card/board art references.
- AC2 Tokens are **semantic**, not literal — `text.primary`, not `offWhite`.
- AC3 Two complete themes ship (a light and a dark) to prove the system.
- AC4 A validator flags any token left unassigned.

`none` · **L**

### STORY-7.2: UI Toolkit theme adapter
- AC1 The active `ThemeAsset` maps onto USS custom properties on the root element.
- AC2 Switching theme restyles every open UI Toolkit screen without a reload.
- AC3 No `.uss` file contains a literal colour outside the generated variable block.

`none` · **M**

### STORY-7.3: uGUI theme binder
- AC1 A `ThemeBinder` component resolves a token to a `Graphic`, `TMP_Text`, or
  `Image` at bind time and on theme change.
- AC2 Binders subscribe to a theme-changed event and update live.
- AC3 An editor validation pass fails any uGUI graphic with a hard-coded colour
  and no binder.

`none` · **M**

### STORY-7.4: Card back and board skins
- AC1 Card backs are selectable and applied to every card view instantly.
- AC2 Board/table surface skins are selectable independently of card backs.
- AC3 Skins are unlockable content that reads its unlock state from the profile.
- AC4 In multiplayer, each player sees **their own** chosen skins for their own
  cards; a cosmetic can never convey hidden information.

`partial` — art content follows the genre · **M**

### STORY-7.5: Runtime theme switching
- AC1 Changing theme in Settings updates the entire app immediately.
- AC2 The choice persists and reapplies at boot.
- AC3 No frame of unstyled or half-styled content during the swap.

`none` · **S**

### STORY-7.6: Theme authoring documentation
- AC1 `docs/design/theming.md` documents every token and its intended use.
- AC2 A worked example shows adding a third theme end to end.

`none` · **S**
