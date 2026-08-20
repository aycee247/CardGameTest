# EPIC-03 — Play Loop & Board Presentation

**Genre dependency:** BLOCKING · **Phase:** 2 · **UI stack:** uGUI + TextMeshPro

> ## ⛔ Blocked
> Blocked on the game design and on EPIC-02. Placeholder shape only.

## Goal

The match on screen: the board, the cards, and the moment-to-moment interaction
that turns a validated rules engine into a game someone plays.

## Architectural rule for this epic

The board is a **view of the core, never an owner of state**. A card view holds
a reference to a core card instance id and renders what the core reports. It
never stores game-meaningful data of its own, and it never mutates the core
directly — it raises an *intent*, which becomes a command, which the core
validates. This is what makes the same board work unchanged in multiplayer.

```
player input → intent → command → core validation → state change → event → view update
```

Note the loop is one-way. A view that updates itself optimistically before the
core confirms is a bug, and in multiplayer it is a desync.

## Placeholder story shape

| Story | Description |
|---|---|
| STORY-3.1 | Board layout and zone containers |
| STORY-3.2 | Card view — front, back, and the state it renders |
| STORY-3.3 | Hand presentation, fanning and reflow |
| STORY-3.4 | Card interaction — hover, select, drag and drop, and the click-click alternative |
| STORY-3.5 | Targeting and target validation feedback |
| STORY-3.6 | Turn and phase indication, and the pass/end-turn action |
| STORY-3.7 | Legal-move affordances — showing what can be played and why not |
| STORY-3.8 | Match flow — start, resolution steps, end |
| STORY-3.9 | Card detail/zoom inspection |
| STORY-3.10 | Opponent presentation, including hidden information |
| STORY-3.11 | Turn timer, where the mode requires one |

## Unblocking requires

The rules model from EPIC-02, plus a decision on the **primary interaction verb**
— drag-to-play, click-to-play, or drag-to-target — because that choice drives
the entire input layer and the accessibility alternatives in EPIC-13.
