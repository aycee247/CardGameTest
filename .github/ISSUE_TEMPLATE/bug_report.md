---
name: Bug report
about: Report a defect in Foundry's rules, presentation, or netcode
title: "[BUG] "
labels: type:bug
assignees: ''
---

<!--
Before filing: check the "Card Game Test Board" project and search open issues
so this isn't a duplicate. Fill in every section below, then delete this
comment block.
-->

## Summary

One or two sentences: what's wrong.

## Environment

- **Mode:** Hot-seat / Online
- **Build / commit:** (git SHA if known, otherwise "latest main")
- **Seats in the match, and which seat you were:**
- **Platform:** Unity Editor / iOS device / other

## Steps to Reproduce

1.
2.
3.

## Expected Result



## Actual Result



## Screenshot / Video

<!-- Drag and drop an image or video into the issue box — GitHub hosts it
     automatically and embeds it inline. -->

## Game State at the Time

- **Round / Phase:** e.g. Round 6/10, Re-pick
- Anything else on screen worth noting (scores, dice, cards, connection state)

## Severity

- [ ] **Blocker** — the match cannot continue
- [ ] **Critical** — a rule the design doc promises (cite the CORE-n / MKT-n /
      CARD-n / NET-n / UI-n id) is broken or silently bypassed
- [ ] **Major** — significantly degrades play but there's a workaround
- [ ] **Minor** — cosmetic or an edge case

## Suspected Location (optional)

File and method, if you have a guess — see `CLAUDE.md` → "Communicating
locations" for the format this project expects (platform, exact repo, full
path, and a link where one exists).

## Additional Context

Logs, related issues, anything else.

---

**Process note:** once triaged this moves onto the kanban board like any other
piece of work (`docs/agile/working-agreement.md`). Its Definition of Done is
whichever checklist in `docs/agile/definition-of-done.md` matches the fix —
Logic and rules changes, Presentation and UI changes, or Networked changes —
not a separate bug-specific bar.
