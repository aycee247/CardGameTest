# EPIC-09 — Audio

**Genre dependency:** none · **Phase:** 3

## Goal

A complete audio layer wired to the mixer and driven by the core's event
stream — so that adding a sound to a game action requires no rules changes.

## Key principle

The rules core **emits events**; the audio service **listens**. The core never
plays a sound and never references an `AudioClip`. This keeps the core pure and
means audio works identically in single-player, multiplayer, and replays.

---

### STORY-9.1: Audio service and mixer setup
- AC1 Mixer groups: Master → Music, SFX, UI, Voice, Ambience.
- AC2 A service exposes play-by-id; callers never touch `AudioSource` directly.
- AC3 Pooled audio sources — no per-sound allocation.
- AC4 Volumes are driven by the settings system on a logarithmic curve.

`none` · **M**

### STORY-9.2: Event-driven SFX binding
- AC1 A data asset maps core event types to sound ids.
- AC2 Adding a sound for an existing event is a data change only, no code.
- AC3 Rapid repeated events are throttled so a 20-card cascade does not clip.
- AC4 Variation support — a sound id may hold several clips chosen at random,
  using the presentation RNG, never the gameplay RNG.

`partial` — the event list firms up with the rules · **M**

### STORY-9.3: Music system
- AC1 Music per screen/context with crossfade.
- AC2 Intensity layering or a stinger system for match milestones.
- AC3 Music state survives scene loads without restarting the track.

`none` · **M**

### STORY-9.4: Ducking and mixing polish
- AC1 Music ducks under significant SFX and voice.
- AC2 UI sounds are audible over everything.
- AC3 A snapshot handles pause — audio audibly changes state when paused.

`none` · **S**
