# Epic Index

Ranked. Genre dependency per `docs/agile/working-agreement.md` §1.

| ID | Epic | Genre dep. | Phase | Detail |
|---|---|---|---|---|
| EPIC-01 | Project Foundation & Architecture | none | 0 | [file](EPIC-01-foundation.md) |
| EPIC-02 | Rules Core & Game State | **blocking** | 1 | [file](EPIC-02-rules-core.md) |
| EPIC-03 | Play Loop & Board Presentation | **blocking** | 2 | [file](EPIC-03-play-loop.md) |
| EPIC-04 | Card Data & Content Pipeline | partial | 1 | [file](EPIC-04-card-data.md) |
| EPIC-05 | Front End, Navigation & Screen Flow | none | 3 | [file](EPIC-05-front-end.md) |
| EPIC-06 | User Settings | none | 3 | [file](EPIC-06-settings.md) |
| EPIC-07 | Theming & Skinning | none | 3 | [file](EPIC-07-theming.md) |
| EPIC-08 | Persistence: Save, Load & Profiles | none | 3 | [file](EPIC-08-persistence.md) |
| EPIC-09 | Audio | none | 3 | [file](EPIC-09-audio.md) |
| EPIC-10 | Online Multiplayer | partial | 4 | [file](EPIC-10-multiplayer.md) |
| EPIC-11 | Tutorial System | partial | 5 | [file](EPIC-11-tutorials.md) |
| EPIC-12 | Graphics, Juice & Presentation Polish | partial | 5 | [file](EPIC-12-graphics.md) |
| EPIC-13 | Accessibility & Localization | none | 6 | [file](EPIC-13-accessibility.md) |
| EPIC-14 | Build, CI & Release | none | 6 | [file](EPIC-14-build-release.md) |

## Epic one-liners

**EPIC-01 — Foundation.** Assembly definitions, folder structure, the core/
presentation boundary, logging, deterministic RNG, test harness. Blocks everything.

**EPIC-02 — Rules core.** The pure-C# game state machine: zones, turn phases,
command validation and execution, win/loss. No UnityEngine. Blocked on genre.

**EPIC-03 — Play loop.** The board on screen: card views, drag and drop,
targeting, phase indication, the moment-to-moment feel. Blocked on genre.

**EPIC-04 — Card data.** ScriptableObject card definitions, the effect
composition model, content authoring tools, and a validator. Framework is
genre-agnostic; the card set is not.

**EPIC-05 — Front end.** Boot flow, main menu, mode select, deck select,
pause, results, and the screen-stack navigation service. UI Toolkit.

**EPIC-06 — Settings.** Video, audio, gameplay, controls/rebinding,
accessibility. Persisted, applied at boot, reachable from the pause menu.

**EPIC-07 — Theming.** One theme asset driving both UI stacks — USS for UI
Toolkit, a ScriptableObject theme + binder for uGUI. Card backs, board skins,
runtime theme switching with no restart.

**EPIC-08 — Persistence.** Player profile, settings, unlocks, statistics, and
in-progress match resume. Versioned, migratable, atomic writes.

**EPIC-09 — Audio.** Mixer groups, an SFX/music service driven by the core's
event stream, ducking, and per-category volume wired to settings.

**EPIC-10 — Multiplayer.** NGO + Relay/Lobby. Server-authoritative command
validation, hidden-information filtering, reconnect, and spectating.

**EPIC-11 — Tutorials.** A data-driven tutorial framework — scripted steps,
highlight masks, gated input, skip and replay — plus the tutorial series itself.

**EPIC-12 — Graphics.** Card animation and juice, VFX, screen shake,
transitions, and the 2D lighting pass. The layer that makes it feel good.

**EPIC-13 — Accessibility & localization.** Colour-blind safe palettes, text
scaling, reduced motion, full rebinding, string tables, and RTL readiness.

**EPIC-14 — Build & release.** CI, automated tests on PR, platform builds,
versioning, crash/analytics reporting, and store packaging.
