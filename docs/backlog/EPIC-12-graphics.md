# EPIC-12 — Graphics, Juice & Presentation Polish

**Genre dependency:** partial · **Phase:** 5

## Goal

The layer that separates a working card game from one that feels good. Card
movement with weight, readable state changes, and a coherent 2D lit look.

## Constraint that shapes everything here

Presentation **never** drives rules. An animation may be skipped, sped up, or
disabled entirely (reduced motion, instant animation setting, or a fast-
forwarded netcode catch-up) and the game state must be byte-identical. Every
animation is therefore a *visualisation of a state change that already
happened*, not the cause of it.

---

### STORY-12.1: Tweening and animation service
- AC1 A single service owns all gameplay tweens.
- AC2 Global speed multiplier honours the animation-speed setting, including
  instant.
- AC3 Reduced-motion mode substitutes fades for movement.
- AC4 Any animation is safely interruptible; interrupting snaps to the end state.

`none` · **M**

### STORY-12.2: Card motion and feel
- AC1 Deal, draw, play, discard and return all have distinct, readable motion.
- AC2 Cards arc rather than sliding linearly; hand fan layout responds to count.
- AC3 Hover raises and previews; drag has weight and a settle.
- AC4 A 30-card cascade stays above 60 fps on the minimum spec.

`partial` · **L**

### STORY-12.3: State-change feedback
- AC1 Damage, buffs, resource changes and zone moves each have clear feedback.
- AC2 Numeric changes animate and are legible at the smallest supported text scale.
- AC3 Feedback is queued so simultaneous changes are readable, not simultaneous mush.

`blocking` — depends on what states exist · **L**

### STORY-12.4: VFX and shader work
- AC1 A URP 2D-compatible VFX set: card glow, targeting arcs, impacts.
- AC2 Shader Graph materials respect the theme palette.
- AC3 A quality tier drops VFX density on low-end hardware.

`partial` · **L**

### STORY-12.5: Camera, lighting and transitions
- AC1 2D lighting pass gives the board depth without hurting card readability.
- AC2 Screen shake on significant moments, fully disableable.
- AC3 Consistent screen transitions shared with EPIC-05.

`none` · **M**

### STORY-12.6: Performance budget
- AC1 60 fps sustained on the defined minimum spec during a full match.
- AC2 No per-frame allocation in the match loop — verified with the Profiler.
- AC3 Sprite atlasing configured; draw calls measured and documented.

`none` · **M**
