# Gameplay Design

> ## ⛔ STUB — this document does not yet exist as design
>
> The game's rules have not been defined. This file is the template that
> unblocks EPIC-02, EPIC-03, and the `blocking` stories elsewhere in the backlog.
> Until it is filled in, **do not invent rules, cards, costs, or mechanics.**

## Why this file gates so much

The architecture is deliberately genre-agnostic: the core knows about zones,
cards, commands, phases and decisions, and nothing else. That buys real freedom —
but a phase graph, a zone set, and a visibility policy cannot be written without
answers to the questions below. Roughly one third of the alpha is blocked here;
the other two thirds is not, and is being built in the meantime.

---

## Questions the design must answer

### Identity

- What is the game called, and what is the one-sentence pitch?
- What is the closest existing game, and what is the one thing this does differently?
- Session length target — 3 minutes, 20 minutes, an hour?

### Players

- How many players per match? Fixed, or a range?
- Is play strictly sequential, or simultaneous? *(This has an outsized effect on
  the netcode — simultaneous play needs a commit/reveal protocol.)*
- Is there an AI opponent, and does it need to be good or merely legal?

### Zones

- Which zones exist — deck, hand, discard, board/tableau, exile, market, trick…?
- Which are ordered and which are unordered?
- **Which are hidden, and from whom?** *(This becomes the `IVisibilityPolicy` and
  drives every redaction decision in multiplayer.)*
- Is deck composition public knowledge? *(Public in most deckbuilders, secret in
  most TCGs. One knob, large consequences.)*

### Turn structure

- What is a turn made of — fixed phases, or free-form actions until you pass?
- Are there nested decisions inside a turn? *(Choose-a-card-to-discard, respond-
  to-a-spell, follow-suit. These become phase-stack pushes.)*
- Can a player act on another player's turn?

### Resources

- What constrains play — mana, energy, action points, hand size, nothing?
- Does the constraint refresh, accumulate, or both?

### Cards

- What is on a card? List every field.
- What keywords exist, and are they composable?
- How many cards in the launch set, and how many in a deck?
- Do effects **stack and respond** to each other, or resolve immediately?
  *(A response stack is significantly more machinery — worth knowing early.)*

### Winning

- What is the win condition? The loss condition? Can a match draw?
- What ends a match early — concede, timeout, deck-out?

### Modes and progression

- Which modes ship in the alpha?
- What is unlockable, and what unlocks it?
- Is there meta-progression between matches?

---

## When this file is filled in

1. Re-check every `blocking` tag in `docs/backlog/` — most should flip to `none`.
2. Expand EPIC-02 and EPIC-03 from placeholder tables into real stories with
   acceptance criteria.
3. Write the lesson list for STORY-11.7 (currently `XL`; split it per lesson).
4. Decide the **primary interaction verb** — drag-to-play, click-to-play, or
   drag-to-target. This drives the entire input layer and its accessibility
   alternatives, and it is easier to decide once than to change later.
5. Keep the reference ruleset from STORY-1.10 permanently — it plugs in beside
   the real ruleset as a second `IRuleSet` and keeps the core honest against
   over-fitting to one genre.
