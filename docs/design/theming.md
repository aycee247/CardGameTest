# Theming

> **Status: built (STORY-5.1/5.2, mobile redesign).** `ThemeAsset` holds the
> semantic tokens below, `ThemeGenerator` writes `Theme_BlueprintLight.asset`
> from code, `ThemeValidator` gates scene generation, and no `new Color(...)`
> literal remains in non-test code. The second theme (AC4) is the one open item.

## The problem

Colours live in two independent places that must be kept in step by hand:

**Runtime views** (serialized defaults, tweakable per instance, no shared source
of truth):
- `UI/DieView.cs` — idle / selected / spent
- `UI/CardButtonView.cs` — affordable / unaffordable
- `UI/PlayerRowView.cs` — row / observer row / ready / thinking / trouble

**The editor generator** (duplicating the same values independently):
- `SceneTools/SceneScaffolder.cs` — camera background, card fill, row fill,
  priority marker, die fill, die face text, handoff / reveal / summary panels
- `SceneTools/UiFactory.cs` — **every button in the game** is
  `new Color(0.20f, 0.42f, 0.85f, 1f)`

Restyling means editing five files and hoping the two sources stay in sync.

## The design

One `ThemeAsset` ScriptableObject is the authority. `UiFactory` and
`SceneScaffolder` read it at generation time; the runtime views read it through a
binder and re-read on theme change.

Because scenes are generated rather than authored, the generator is the natural
place to apply theming — but the runtime views still need binders, since a theme
can change at runtime and a regenerated scene cannot.

## Token naming

Tokens are **semantic**, describing role rather than appearance:

```
✅  surface.base     surface.raised     surface.overlay
    text.primary     text.muted         text.inverse
    state.affordable state.unaffordable state.spent
    state.ready      state.thinking     state.trouble
    accent.priority

❌  blue2   offWhite   theOrangeOne
```

A literal name survives until the first redesign; a semantic one survives it.

## What is not themed

**Dice face values and card costs are not styling.** A skin may change how a die
looks but never what it is worth — the rules engine only ever deals in face
values, and `DiceSkin` is documented as cosmetic for exactly this reason.

Card art is content, not chrome. Only frames, backs, board surface and UI chrome
are themed.

## Accessibility constraint

State must never be communicated by colour alone. The shipped views honour
this — `DieView` marks selection with a position lift and spent with a rotated
watermark; `CardButtonView` marks affordability with opacity, border and fill
together — and new work must keep it true. See E4 STORY-4.5.

## Skins vs themes

- A **theme** restyles the UI chrome. One active at a time, a player setting.
- A **skin** is unlockable cosmetic content for dice and cards, tracked per
  player in `PlayerProfile.OwnedDiceSkinIds` / `SelectedDiceSkinId`.

In online play each player sees their own chosen skins. A cosmetic must never
convey hidden information.
