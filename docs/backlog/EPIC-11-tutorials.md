# EPIC-11 — Tutorial System

**Genre dependency:** partial — the framework is genre-agnostic, the lessons are not
**Phase:** 5

## Goal

A data-driven tutorial framework plus a series of built-in tutorials that take a
player who has never seen the game to competent play, without a wall of text.

## Design principles

- **Teach by doing.** A step asks the player to perform the action, then
  confirms it. Reading is a fallback, not the mechanism.
- **Tutorials are data, not code.** A `TutorialDefinition` asset is a sequence of
  steps. Adding a lesson must never require a new MonoBehaviour.
- **Deterministic scenarios.** A tutorial sets an exact game state and a fixed
  RNG seed, so step 4 always follows step 3. This is only possible because the
  rules core is deterministic and state is constructible — a direct payoff from
  EPIC-01.
- **Never trap the player.** Every tutorial is skippable and replayable at any time.

---

### STORY-11.1: Tutorial framework core
- AC1 A `TutorialDefinition` holds an ordered step list.
- AC2 A step declares: instruction text, highlight target, allowed input, an
  advance condition, and an optional forced game action.
- AC3 Advance conditions are data-driven predicates over core events.
- AC4 The framework is driven by the same event stream as audio and UI — it does
  not poll game state.

`none` · Depends on: EPIC-02, EPIC-03 · **L**

### STORY-11.2: Scripted scenario loading
- AC1 A tutorial can specify an exact starting state: zones, hands, and board.
- AC2 The RNG seed is fixed per tutorial so the sequence is reproducible.
- AC3 A scenario that becomes invalid after a rules change fails loudly in CI,
  not silently at runtime in front of a player.

`blocking` — needs the state model · **L**

### STORY-11.3: Highlight, masking and input gating
- AC1 A dimming overlay with a cut-out highlights the target element.
- AC2 Highlighting works over **both** uGUI board elements and UI Toolkit panels.
- AC3 Input outside the allowed set is blocked, with a gentle nudge rather than
  silent rejection.
- AC4 Highlights follow moving targets (a card being dealt).

`none` · **L**

### STORY-11.4: Instruction presentation
- AC1 Callout bubbles position themselves near the target and never off-screen.
- AC2 Text is localized and honours text-scaling and reduced-motion settings.
- AC3 Optional voice-over hook.

`none` · **M**

### STORY-11.5: Tutorial progress and gating
- AC1 Completion is stored in the profile.
- AC2 The first launch offers the basics tutorial; it is skippable.
- AC3 Advanced tutorials unlock as their prerequisites complete.
- AC4 All tutorials are replayable from a dedicated menu.

`none` · **M**

### STORY-11.6: Contextual hints
- AC1 Optional just-in-time hints on first encountering a mechanic.
- AC2 Each hint fires once, tracked in the profile, and is globally disableable.

`partial` · **M**

### STORY-11.7: The tutorial series content
The lesson list is **blocked on the game design**. The expected shape:

1. Basics — the goal, the screen, taking a turn
2. Core actions — playing a card, the primary interaction verb
3. Resources and costs
4. Reading a card — anatomy, keywords, targeting
5. Advanced interactions — the genre's signature mechanic
6. Deck/loadout building
7. Multiplayer etiquette — timers, conceding, reconnecting

- AC1 Each lesson is under 3 minutes.
- AC2 A player completing the series can finish a full match unaided.
- AC3 Playtested with someone who has never seen the game.

`blocking` · **XL — must be split per lesson once the design lands**
