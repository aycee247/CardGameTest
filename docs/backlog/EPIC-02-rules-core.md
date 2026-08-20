# EPIC-02 — Rules Core & Game State

**Genre dependency:** BLOCKING · **Phase:** 1

> ## ⛔ Blocked
> This epic cannot be written in detail until the game design lands. Every story
> below is a placeholder describing the *shape* of the work, not the work itself.
> See `docs/design/gameplay.md`.

## Goal

The complete game rules as **pure C# with no reference to UnityEngine** — the
single source of truth for what is legal, what happens, and who wins. Runs
identically in the editor, on a headless server, and inside a unit test.

## Non-negotiable properties

These hold regardless of which genre the game turns out to be, and they are the
reason this epic is sequenced before everything else:

1. **Deterministic.** Same initial state + same seed + same command sequence ⇒
   same final state, always. No `DateTime.Now`, no unseeded RNG, no dictionary
   iteration order dependence, no floating-point accumulation in rules maths.
2. **No Unity types.** Not even `Vector2` or `Random`. This is enforced by the
   assembly definition, which references nothing from Unity.
3. **Commands in, events out.** All state change flows through validated
   commands; all observers learn about it through emitted events.
4. **Serializable at any instant.** Required for save/resume, netcode, replay,
   and deterministic tutorial scenarios.

## Placeholder story shape

| Story | Description | Status |
|---|---|---|
| STORY-2.1 | Game state model — zones, players, the card instance model | blocked |
| STORY-2.2 | Turn and phase state machine | blocked |
| STORY-2.3 | Command definition, validation and execution pipeline | blocked |
| STORY-2.4 | Event emission for every state change | blocked |
| STORY-2.5 | Effect resolution — the stack/queue and its ordering rules | blocked |
| STORY-2.6 | Win, loss and draw condition evaluation | blocked |
| STORY-2.7 | Deterministic shuffle and draw | **partially unblocked** — the seeded RNG utility is EPIC-01 |
| STORY-2.8 | State serialization and hashing | **partially unblocked** — the mechanism is genre-agnostic |
| STORY-2.9 | AI opponent — legal move generation and evaluation | blocked |
| STORY-2.10 | Rules core test suite | blocked |

## What to nail down before unblocking

The design material needs to answer, at minimum:

- What **zones** exist (deck, hand, discard, board/tableau, exile…) and which are
  hidden from whom?
- What is a **turn** made of? Fixed phases or free-form actions?
- What **resources** constrain play (mana, energy, action points, none)?
- What is the **win condition**?
- Is there **simultaneous** play or is it strictly sequential? (This has an
  outsized effect on the netcode in EPIC-10.)
- Do effects **stack and respond** to each other, or resolve immediately?
- How many **players** per match?
