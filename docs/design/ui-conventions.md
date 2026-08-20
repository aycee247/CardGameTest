# UI Conventions

## The stack

**uGUI + TextMeshPro. That is the whole answer.** There is no UI Toolkit in this
project — no `.uxml`, no `.uss`, no `UIDocument` — and none should be added.
Mixing stacks would mean two styling systems, two input paths and two sets of
layout bugs for a game that has one screen shape.

## Scenes are generated, not authored

`SceneTools/SceneScaffolder.cs`, menu **Foundry ▸ Generate Scenes & Build
Settings**, builds Boot / MainMenu / Lobby / Game and wires every serialized
reference. `UiFactory` is the only widget builder.

Consequences to respect:

- **Do not hand-edit a generated scene.** The next regeneration discards it.
  Change the scaffolder, regenerate, and commit the result.
- Adding a widget means adding it to the scaffolder *and* wiring its reference
  there with `SetRef`.
- The Game scene uses in-scene deactivated **templates** for card / die /
  player-row rather than prefab assets, because prefab references did not survive
  save. Follow that pattern for new pooled widgets.
- **Committed scenes can go stale against the generator.** This has already
  happened once and left the board unable to start. Regenerate and commit
  whenever the scaffolder changes.

## Layout

- Portrait 1080×1920 reference, `CanvasScaler.ScaleWithScreenSize`, match 0.5.
- Three bands: opponent rail across the top, market through the middle, dice tray
  filling the bottom third where thumbs reach.
- Anchor to screen edges rather than using absolute offsets from centre — the
  scaffolder learned this the hard way; absolute offsets only work at the exact
  reference aspect.
- `SafeAreaFitter` goes on the SafeArea panel of every scene. It is correct and
  already applied everywhere.
- **UI-1 is a hard constraint:** the rail must stay legible at **six players on
  the narrowest supported device**. Check it there, not on a tablet.

## What the UI must communicate

Simultaneous play changes the vital question. In a turn-based game it is *whose
turn is it*; here it is **who has locked in, and how long do I have**. So commit
state and the phase clock are permanent fixtures, not transient toasts (UI-1,
UI-2).

## Views are passive

A view raises events and renders a snapshot. It never mutates match state, never
predicts, and never knows a rule.

```
GameHudView (events) → GameHudPresenter → IGameActions → RulesEngine
RulesEngine → MatchSnapshot → IMatchView.Changed → GameHudView.Render
```

Rejections arrive as `MoveFailure` and are rendered by
`GameHudPresenter.Explain` — keep that copy in one place.

## Animation

- **No third-party tween libraries.** Unity Animator or coroutine
  `Mathf.Lerp`/`SmoothStep`. Deliberate policy, to keep dependencies on the
  official registry.
- Route every gameplay tween through one service, so the animation-speed and
  reduced-motion settings apply to *all* of them rather than most.
- Every animation is interruptible; interrupting snaps to the end state.
- **Presentation never drives rules.** Skip, speed up or disable any animation
  and the resulting match state must be identical.

## Text

- Card text and dice must stay legible at six seats on the smallest screen.
- There is no localization in this project — the package is not installed and no
  strings are keyed. Do not half-introduce it; it is out of scope for the demo.

## Colour

No `new Color(...)` literal in new UI code — see `docs/design/theming.md`. Until
E5 lands, follow the existing pattern of a serialized field rather than an inline
literal, so the later migration is mechanical.

State must never be communicated by colour alone. `DieView` and `CardButtonView`
currently violate this; new work should not add to it.
