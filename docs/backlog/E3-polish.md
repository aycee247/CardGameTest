# E3 — M6 polish

**Phase:** 3 · **Depends on:** E2

The specced final milestone: reveal choreography, dice and claim animation,
audio, and first-time-player onboarding.

## Constraint

**No third-party tween libraries.** Unity's Animator or coroutine
`Mathf.Lerp`/`SmoothStep` only — a deliberate policy to keep the dependency set
to the official registry.

Presentation never drives rules. Any animation must be skippable with the game
state unchanged.

---

### STORY-3.1: Reveal choreography (UI-4)
Today `HotSeatOverlayView.RenderReveal` builds a `StringBuilder` of
`"{name} won/lost {card}"` and assigns it to one TMP field. The design doc calls
Reveal "the emotional peak of the round" and requires it not be instant.

- AC1 Commits flip together as one beat.
- AC2 Contested cards are visibly contested before resolving.
- AC3 Priority is shown deciding, not just announced.
- AC4 Losers' dice visibly return to them.
- AC5 The whole beat fits the ~8s Reveal window and is skippable.

**L** — the highest-value story in this epic.

### STORY-3.2: Dice and claim animation
`DieView.Set` writes `faceText.text` and swaps a flat colour. Nothing moves
anywhere in the game.

- AC1 Dice tumble on roll and settle on their face.
- AC2 Re-roll, nudge and set each read as distinct actions.
- AC3 A claimed card travels from market to owner.
- AC4 60 fps at six seats on the target device.

**L**

### STORY-3.3: Audio
`IAudioService` and `AudioManager` are written, registered and placed in the Boot
scene — and have **zero call sites**. There is no mixer asset, so `SetVolumes` is
a silent no-op, and no clips exist.

- AC1 An `AudioMixer` with the exposed params `MasterVolume`, `MusicVolume`,
  `SfxVolume` that `AudioManager` already expects.
- AC2 SFX for roll, select, commit, reveal, claim, contest-lost, round-end.
- AC3 Music per context with crossfade.
- AC4 Driven from phase transitions and view events — the rules layer stays
  silent and unaware.

**L**

### STORY-3.4: Haptics
`GameSettings.Haptics` is persisted and read by nothing.

- AC1 Haptic feedback on commit, claim and contest resolution.
- AC2 Honours the setting.

**S**

### STORY-3.5: First-time onboarding
A new player is currently never told what Sparks are, what a cost means, or that
dice are what pay for cards. The only instructional text is six phase strings and
13 reactive error messages shown *after* a mistake.

- AC1 A first-run flow covering: the round's six phases, that dice pay costs,
  what Sparks are and how they accrue, how contention and priority resolve.
- AC2 Skippable and replayable.
- AC3 Completion tracked in `PlayerProfile`.
- AC4 Playtested with someone who has never seen the game.

**L**

### STORY-3.6: Cost legibility (UI-3)
- AC1 Tapping a market card highlights exactly which dice would pay for it.
- AC2 Dice that cannot contribute are visibly greyed.
- AC3 Wild faces and wild dice are shown as satisfying a cost they otherwise
  would not — the wild rules are the least obvious part of the game.

**M**
