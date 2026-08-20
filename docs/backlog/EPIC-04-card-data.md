# EPIC-04 — Card Data & Content Pipeline

**Genre dependency:** partial — the pipeline is buildable now, the cards are not
**Phase:** 1

## Goal

How a card is defined, authored, validated and loaded. Designers add and balance
cards without a programmer and without a code change.

## The split that makes this work

```
CardDefinition (ScriptableObject)     CardInstance (pure C#, in the core)
─────────────────────────────────     ──────────────────────────────────
Immutable authored data               Mutable per-match runtime state
Name, cost, art, effect list          Instance id, owner, zone, counters
Shared by every copy in play          One per physical card in a match
Lives in Unity, edited by designers   Lives in the rules core, no Unity types
```

Conflating these is the most common structural mistake in a Unity card game.
If runtime state lives on the ScriptableObject, then two copies of the same card
share it, edits persist between play sessions in the editor, and the core cannot
be Unity-free. The core therefore never sees a `CardDefinition` — it sees a
plain data record converted at load time.

---

### STORY-4.1: Card definition asset and the core-facing record
- AC1 `CardDefinition` is a ScriptableObject holding authored data only.
- AC2 A converter produces an immutable, Unity-free record for the core.
- AC3 Card ids are stable strings, never array indices — reordering a list must
  not invalidate saves.
- AC4 Round-trip conversion is unit-tested.

`partial` · **M**

### STORY-4.2: Effect composition model
- AC1 Effects are composable data, not one C# subclass per card.
- AC2 A designer builds a new card from existing effect primitives with no code.
- AC3 Effects serialize as part of the definition and are executable by the core.

`blocking` — the primitives depend on the rules · **L**

### STORY-4.3: Card database and lookup
- AC1 All definitions load at boot into an id-keyed registry.
- AC2 Duplicate and missing ids fail loudly at import, not at runtime.
- AC3 Lookup is O(1) and allocation-free on the match path.

`none` · **M**

### STORY-4.4: Content validation tooling
- AC1 An editor menu validates the entire card set.
- AC2 Flags: missing art, missing text, unassigned effects, duplicate ids,
  unlocalized strings, out-of-range costs.
- AC3 Runs in CI so bad content cannot merge.

`partial` · **M**

### STORY-4.5: Deck definition and deck building
- AC1 Decks are data assets referencing card ids with counts.
- AC2 Deck legality is validated by the core, not the UI — the UI only surfaces it.
- AC3 A deck referencing a removed card reports it clearly instead of crashing.

`partial` · **M**

### STORY-4.6: Asset loading strategy
- AC1 Addressables configured for card art.
- AC2 Art loads on demand and unloads with the match — the full set is never
  resident at once.
- AC3 A missing address renders a placeholder rather than throwing.

`none` · **M**

### STORY-4.7: Bulk authoring workflow
- AC1 Cards can be imported from a spreadsheet/CSV into definition assets.
- AC2 Re-import updates existing assets by id without breaking references.
- AC3 The workflow is documented for non-programmers.

`partial` · **L**
