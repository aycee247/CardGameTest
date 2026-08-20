# E5 — Theming & skinning

**Phase:** 5 · Requested by the user; **absent from the design doc's milestones**

## Starting position

`Data/DiceSkin.cs` is a ScriptableObject holding six face sprites, and
`PlayerProfile` already carries `OwnedDiceSkinIds` and `SelectedDiceSkinId`.
**Both have zero consumers.** `DieView` renders `face.ToString()` as text, never
a sprite; no `DiceSkin` asset exists; `Assets/_Project/Art/` is empty.

## The problem to solve first

Every colour in the game is a hard-coded literal, and they are **duplicated
across two independent sources that must be hand-synced**:

- Runtime views — `DieView` (idle/selected/spent), `CardButtonView`
  (affordable/unaffordable), `PlayerRowView` (row/ready/thinking/trouble)
- The editor generator — `SceneScaffolder` (camera, panels, overlays) and
  `UiFactory`, where **every button in the game** is
  `new Color(0.20f, 0.42f, 0.85f, 1f)`

Restyling today means editing five files and keeping two of them in step by hand.

## Art direction

Undecided per §11 of the design doc. Per the user's decision, **build the system
now against placeholder art** so that choosing a direction later is a content
change rather than a code change.

## Scope decisions for the demo

- **Generation-time theming is the deliverable.** `UiFactory` and
  `SceneScaffolder` read the `ThemeAsset` when scenes are generated. Runtime
  theme *switching* is optional for the demo — build it only if it falls out
  free.
- **Everything is unlocked.** All skins and cards are available to every
  player; there is no meta-economy, no earn/grant mechanism, and therefore no
  IAP review surface. `OwnedDiceSkinIds` simply contains everything.

---

### STORY-5.1: Theme asset and token vocabulary
- AC1 A `ThemeAsset` defines semantic tokens — `surface.raised`, `text.muted`,
  `state.affordable` — never literal names like `blue2`.
- AC2 Both `UiFactory`/`SceneScaffolder` and the runtime views read from it, so
  there is one source of truth instead of two.
- AC3 A validator flags any unassigned token.
- AC4 Two complete themes ship, to prove the system is real.

**L**

### STORY-5.2: Retire hard-coded colours
- AC1 No `new Color(...)` literal remains in `UiFactory`, `SceneScaffolder`,
  `DieView`, `CardButtonView` or `PlayerRowView`.
- AC2 Regenerating scenes produces the same look as before the change.

**M**

### STORY-5.3: Wire DiceSkin
- AC1 `DieView` renders `DiceSkin.FaceSprite(face)` instead of text.
- AC2 The skin is read from `PlayerProfile.SelectedDiceSkinId`.
- AC3 Face values stay legible at six seats — the rules only ever deal in face
  values, so a skin can never change what a die is worth.
- AC4 A missing or unset skin falls back to the current text rendering.

**M**

### STORY-5.4: Card and board skins
- AC1 `CardButtonView.artwork` is fed a sprite — `GameHudView.RenderMarket`
  currently never passes one.
- AC2 Card frame and board surface are themed.
- AC3 In online play each player sees their own chosen skins; a cosmetic can
  never convey hidden information.

**M**

### STORY-5.5: Skin picker
- AC1 A picker screen driven by `OwnedDiceSkinIds`.
- AC2 Unlock state read from the profile; the selection persists.

**M**

### STORY-5.6: Placeholder art set
- AC1 One placeholder dice skin and one card frame set, proving the swap end to
  end.
- AC2 Documented so an artist can replace them without touching code.

**M**
