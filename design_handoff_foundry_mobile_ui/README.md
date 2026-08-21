# Handoff: Foundry Mobile UI (Game board, lobby, menu, reveal, standings)

## Overview
Mobile UI for **Foundry**, the simultaneous-roll dice engine builder. Covers Main Menu → Lobby (host/join code) → Game board (Roll/Shape/Commit/Reveal/Upkeep) → Match-end standings, for the existing Unity 6.5 / uGUI + TextMeshPro codebase (`Game.App`, `Game.UI`, `Game.Core`). This closes out visual/UX design for M6 (Polish) and gives E5 (Skinning/Theming) its first real `ThemeAsset` values.

## About the Design Files
The bundled file (`Foundry Prototype.html`) is a **design reference built in HTML/React**, not code to port. It is a fully interactive click-through prototype (with scripted bot opponents standing in for real network players) meant to communicate exact layout, states, motion, and copy. **The task is to recreate this design inside the existing Unity/uGUI stack** — `GameHudView`, `DieView`, `CardButtonView`, `PlayerRowView`, `SceneScaffolder`, `UiFactory` — not to embed a WebView or ship the HTML. Where the prototype implies new views (the card zoom sheet, the reveal spotlight, the repick sheet, the hint toast), add new passive `MonoBehaviour` views following the same event-raising, snapshot-rendering pattern as the rest of `Game.UI`.

## Fidelity
**High-fidelity.** Colors, type, spacing, and component shapes are final (they come from the bound "Industry" design system's token sheet — see Design Tokens below). Copy/microcopy is close to final; treat card names beyond the 10 canonical ones in `StarterDeck` as placeholders. Motion timings are specified and should be treated as final targets, implemented via the existing `Mathf.Lerp`/`SmoothStep`/Animator conventions (no tween libraries, per `docs/design/ui-conventions.md`).

## Screens / Views

### 1. Main Menu (`MainMenuView` — existing)
**Purpose:** Entry point; host a match or join by code.
**Layout:** Single column, portrait, safe-area padded. Vertical flow: top spacer → framed hero block → bottom spacer → two full-width stacked buttons → footer caption.
**Components:**
- **Hero frame**: blueprint-style bordered panel (`.blueprint` equivalent: 1px border, 4 corner "+" registration marks) containing:
  - Eyebrow label, uppercase, 10px, letter-spacing 0.22em, weight 600, accent-700 color: "SIMULTANEOUS DICE ENGINE BUILDER"
  - Wordmark "FOUNDRY": Barlow Condensed, weight 700, 72px, line-height 0.95
  - Two small 3×3 dot-grid glyphs suggesting dice, plus meta text right-aligned: "2–6 PLAYERS · 10 ROUNDS" / "≈ 12 MINUTES" (11px, ~55% text opacity)
  - Body copy, 13px, ~70% opacity, 2 lines
- **HOST MATCH** button: primary (solid accent fill), blueprint corner marks, full width, 52px tall, 16px label, letter-spacing 0.06em
- **JOIN WITH CODE** button: secondary style, same size, no corner marks
- Footer: "REV 0.1 · ONLINE DEMO", 10px, letter-spacing 0.18em, ~40% opacity, centered

### 2. Lobby (new — needs a `LobbyView`, referenced as existing stub in `Game.App/LobbyController.cs`)
**Purpose:** Show join code, seat fill (host + up to 5 joiners), start when full.
**Layout:** Safe-area top padding **~62px** (status bar clearance — see Layout Notes). Header row: "← Back" ghost button, spacer, "FRIENDS BY CODE" outline tag. Below: a bordered code panel, then a scrollable seat list (max 6 rows), then a bottom-pinned primary "START MATCH" button.
**Components:**
- **Code panel**: blueprint frame, centered text. Eyebrow "JOIN CODE" (accent-700, 10px). Code itself: Barlow Condensed 700, 54px, letter-spacing 0.12em (format `XXX-###`, e.g. `FDY-483`). Caption below, 12px, ~55% opacity: "Share it — seats fill as friends arrive."
- **Seats header**: small label row, "SEATS" left / "N / 6" right, 11px uppercase, letter-spacing 0.14em, ~55% opacity.
- **Seat row** (×6, one per potential seat): height ~48px, horizontal flex, gap 10px:
  - 26×26 square avatar tile, 1.5px border, initial letter, Barlow Condensed 600 13px
  - Name, 14px weight 600, flex 1
  - Right-aligned state chip, 10px, letter-spacing 0.14em, weight 600: `HOST` (you), `READY` (joined bot/player, accent-700), empty (open/waiting), or "Seat closed" grey dashed row for seats beyond the configured player count
  - Filled/joined rows: solid divider border + surface fill (host row gets accent border + accent-tinted fill). Open rows: 1px **dashed** border, transparent fill, ~55%-opacity placeholder text "Waiting for player…"
- **Start button**: disabled state reads "WAITING — X/N JOINED" (surface-fill look); once full, becomes primary blueprint button "START MATCH · N PLAYERS".

### 3. Game Board (`GameHudView` — existing, extend heavily)
**Purpose:** The core simultaneous round loop — this is the screen that must satisfy UI-1 through UI-6.
**Layout:** Safe-area top padding **~56px**, then five vertically stacked bands (top to bottom), each full width, tight gaps (~6-8px):
1. Header row (round + phase + Sparks)
2. Opponent rail (horizontal strip, one cell per player incl. you)
3. Market label row + 5-card grid
4. Owned-powers chip strip (conditional, see below)
5. Dice tray (flexible middle, fills remaining space)
6. Contextual shape controls row (conditional)
7. Bottom action bar (Withdraw/Pass · status line · Done/timer button)

Plus overlay layers (in z-order): card **zoom sheet**, **hint toast**, **upkeep modal**, **repick sheet**, **reveal spotlight** (full-screen, highest).

**6a. Header row**
- "ROUND 06/10" — Barlow Condensed 700, 22px, **must stay on one line** (`white-space: nowrap`); zero-padded round number.
- Phase label, 10px uppercase, letter-spacing 0.16em, accent-700, single line with ellipsis overflow if the row is tight. Phase copy: "Rolling…" / "Shape phase" / "Commit — secret" / "Reveal" / "Re-pick" / "Upkeep" / "Match over".
- Right: Sparks tag ("SPARKS 5/10" — use a non-breaking space between label and value so it never collapses), pill/tag styling, accent-tinted.

**6b. Opponent rail (UI-1)** — flex row, one cell per player (up to 6), each `flex:1`, min-width:0, text overflow-safe:
- Small triangular **priority marker** (top-left corner) on the cell with the current priority holder (lowest score → seat order tiebreak)
- Name, 8.5px uppercase, truncated with ellipsis, bold
- Score, Barlow Condensed 700, ~19px
- Sub-line: "{dice}d · {sparks}sp", 8.5px, muted
- State line, 8.5px bold: "✓ READY" (accent-700) or "○ THINKING" (muted) during input phases; blank otherwise. Must also support **"reconnecting Ns"** and **"left"** states per `PlayerRowView.DescribeState` (troubleColor) — the prototype doesn't show these; carry them over from the existing component.
- Observer's (your) own cell gets an accent border + accent-tinted fill to stand out from the rest.

**6c. Market (MKT-1, UI-3)** — label row: "MARKET" (10px, letter-spacing 0.2em) left, "DECK {n} · TAP A CARD TO INSPECT" right (10px, lighter). Below: `grid-template-columns: repeat(5, 1fr)`, gap ~5px, 5 card cells, min-height ~96px each:
- Each cell is a blueprint frame (square corners + 4 corner marks), tappable.
- Top row inside: "T{tier}" (accent-700, bold, 8px) left, "{vp}VP" (Barlow Condensed 700, 13px, accent-800) right
- Card name: Barlow Condensed 600, 11.5px, 2-line clamp
- Bottom: cost shorthand (e.g. "3 OF A KIND", "SUM ≥ 20", "RUN OF 4", "2 · 4 · 6"), 9px bold, top border rule
- **Affordability**: opacity 1 + accent border + white/paper fill when the player's current dice can pay the cost; opacity 0.5 + neutral divider border + transparent fill when not. Never rely on color alone elsewhere, but this card-level affordability signal is allowed per existing `CardButtonView` convention (still paired with the opacity drop).
- **Your own committed card**: full-cell accent-tinted overlay stamp, rotated -6°, reading "COMMITTED", plus a slow pulse glow animation (~1.8s) so you can find it again without revealing it to others (this stamp is a *local-only* echo of your own secret pick, not visible to opponents — a secrecy-critical detail, see Interactions).

**6d. Owned powers strip (UI-5)** — **only rendered when at least one owned power is currently usable** (per the answered "auto-shown only when usable" design decision), e.g. "FREE RE-ROLL ×1", "±1 NUDGE ×1", "SET FACE FREE ×1" as small accent tag chips, wrapping row. When nothing is usable this whole row collapses to zero height — don't reserve empty space.

**6e. Dice tray** — bordered panel, label "YOUR DICE — {hint}" pinned top-left (8-9px, letter-spacing 0.2em) where `{hint}` is one of: "SERVER ROLLING" / "HIGHLIGHTED DICE PAY THE COST" (while a card's zoom sheet is open) / "{n} SELECTED" / "TAP TO SELECT". Dice wrap in a centered flex row, gap ~9px, each die is a 62×62 square (not rounded — blueprint aesthetic), pip layout via 3×3 grid (standard die-face pip patterns for 1–6), states:
  - **Idle**: paper background, accent-700 border
  - **Selected**: accent-fill background, dark border, pips flip to paper color, lifts up 3px (`translateY(-3px)`)
  - **Dimmed** (a card's cost is open and this die doesn't contribute): transparent background, neutral divider border, unchanged pips
  - **Spent** (already pledged to a commit): dashed divider border, semi-opaque "SPENT" diagonal watermark overlay
  - **Rolling**: brief shake animation + rapid face-cycling for ~1s at round start (server roll, CORE-2/NET-1 — the client shows this as pure animation while waiting on the authoritative result)

**6f. Shape controls row** (visible only during Shape phase, before you've locked in) — 4 buttons in a row: "RE-ROLL {n} [−cost]" (secondary style, dynamic label showing free vs Spark cost), "−1" / "+1" nudge buttons (require exactly one die selected), "SET FACE −4" (opens the face picker below). Each disables per Spark/selection legality, never purely by color.

**6f-alt. Face picker** (replaces the shape row when "Set Face" is tapped) — "SET TO:" label + six numbered buttons (1–6) + a "✕" cancel ghost button.

**6g. Bottom action bar** — flex row: conditional "Withdraw" ghost button (only if committed, per CORE-5), conditional "Pass this round" ghost button (only if undecided), a flexible status line (12px, describes current state in plain language — e.g. "Committed to Recaster — secret until Reveal.", "Shape your dice, or commit early.", "Pick a card and the dice that pay."), and the **Done/Timer button** on the far right:
  - 74×74 square button, **timer ring drawn as an SVG square outline stroke** (`stroke-dasharray`/`stroke-dashoffset` proportional to time remaining), track in faint accent, progress in full accent — turns to a dark/urgent accent color and the whole button pulses (~0.55s) in the **last 5 seconds** (UI-2 "escalating urgency")
  - Label states: "DONE" (Shape phase, actionable, solid accent fill) / "PICK" (Commit phase, undecided — must render as an inactive/disabled-looking state, NOT the actionable accent fill, since the player hasn't committed yet — this was a fixed defect, keep it disabled-styled) / "LOCKED ✓" (once committed/passed/done, muted surface fill) / "—" outside input phases
  - Numeric seconds readout below the label, Barlow Condensed 700, 17px

**6h. Card zoom sheet (new — needed for UI-3)** — modal-like panel pinned near the top (not a full dialog backdrop centered — anchored under the header, ~58px down to clear the status bar), dismissible by tapping the scrim or a "✕" icon button. Contents:
  - Header: TIER tag + power-family outline tag, card name (Barlow Condensed 700, 30px), VP figure in its own bordered box top-right
  - Two-column info block: "COST" (left, ~35% width) with the full-text cost description; "PERMANENT POWER" (right, ~65%) with the power's plain-English effect
  - Footer row: pay-status line ("✓ Selected dice pay this cost" in accent, or "Select dice below that pay the cost" muted) + "COMMIT · SECRET" primary button (disabled + labeled "CANNOT PAY" when the current dice selection doesn't validate)
  - Small print: "Commits are secret until Reveal. Contested cards go to the lowest score."
  - **Opening this sheet auto-highlights** a valid dice combination in the tray below (via the dimming behavior in 6e) as a starting suggestion — the player can still adjust the selection manually before committing.

**6i. First-round hint toast (onboarding, in scope per your answer)** — bottom-anchored (above the action bar, ~104px from bottom), dark accent-900 fill, reversed (paper) text, dismiss button labeled "GOT IT". Two hints only, shown once each on their first occurrence: entering Shape phase, and entering Commit phase (with copy explaining what the phase means and how to act). Persist "seen" state per session (or per player profile if you want it permanent — recommend `PlayerProfile`).

**6j. Upkeep modal** — small centered blueprint dialog, ~2.6s auto-dismiss, no interaction required. Eyebrow "UPKEEP", then a short list of what happened this round (Sparks gained from unspent dice / Economy powers / consolation, what was claimed, "Market refilled · priority recalculated"). Purely informational, matches Upkeep's ~4s phase duration (auto phase per game-design.md).

**6k. Re-pick sheet (MKT-3)** — appears only if you lost a contested claim. Bottom-anchored blueprint panel, ~10s countdown shown top-right (Barlow Condensed 700, 20px). Header: "RE-PICK" + explanatory copy "You lost the contest — your dice are back." Grid of remaining affordable/unaffordable market cards (2-4 columns depending on remaining count) with compact name/cost/VP, tap to claim. A "PASS — TAKE 3 SPARKS" secondary full-width button covers the consolation path (MKT-5). Auto-resolves to pass if the timer runs out (server-enforced, per CORE-2).

**6l. Reveal spotlight (UI-4 — "one contest at a time" per your chosen treatment)** — full-screen takeover, accent-900 background, paper text, tap-anywhere-to-advance:
  - Top bar: "REVEAL — ROUND {n}" left, "CLAIM {i} OF {total}" right (only relevant once >1 card was claimed this round — a single-claim round can skip straight to the result beat)
  - Center: the contested/claimed card in a blueprint frame with a **3D flip-in animation** (~0.4s, `rotateY`), showing tier/VP tags, name (Barlow Condensed 700, 38px), and power text
  - Below: the claimants (1 = uncontested, 2+ = contested), each in a small bordered chip with name + score, flip-in staggered (~0.18s delay per chip)
  - After a beat (~2.4s per stage, auto-advancing but also tap-to-skip), a **stamp animation** (scale+rotate settle, ~0.35s) reveals the result: "YOU CLAIM IT" / "{NAME} CLAIMS IT", plus the reason line: "CONTESTED — PRIORITY: LOWEST SCORE WINS" or "UNCONTESTED CLAIM" (MKT-4's priority rule made visible, per UI-4's requirement that this be a deliberate, non-instant beat)
  - Footer: pulsing "TAP TO CONTINUE" prompt
  - Sequence repeats per contested card, then proceeds to Upkeep.

### 4. Match-End Standings (new — needs an `EndScreenView`)
**Purpose:** Final ranking after round 10 (CARD-3 tie-break: VP → Sparks → cards).
**Layout:** Safe-area top padding ~62px. Centered header block: eyebrow "MATCH OVER — 10 ROUNDS", big winner headline "{NAME} WINS" (Barlow Condensed 700, 44px), a one-line note (e.g. "Your engine paid out." or a tie-break explainer). Below: a vertical list of standing rows (one per player, ranked), each a blueprint-framed card with a staggered rise-in animation (~0.35s, ~0.08s stagger). Bottom: two stacked full-width buttons, "REMATCH" (primary blueprint) and "MAIN MENU" (secondary).
**Standing row:** rank number (Barlow Condensed 700, 20px, muted unless #1), name + detail line ("{cards} cards · {sparks} sparks", plus "+N end-game VP" when scoring powers paid out), right-aligned final score ("{n} VP", Barlow Condensed 700, 26px). Winner's row gets the accent border/fill treatment used elsewhere for "this one matters."

## Layout Notes (safe area — important)
Every screen except Main Menu (which already has generous top spacer content) needs **~56–64px of top padding** before any content, to clear the iOS status bar / dynamic island. This maps directly to your existing `SafeAreaFitter` — apply it exactly as already used elsewhere in the project; don't hardcode the px value, use the device's actual safe-area inset the way `SafeAreaFitter` already does. The prototype's fixed px values are for an iPhone-sized reference viewport only.

## Interactions & Behavior
- **Roll (auto, ~3s)**: client plays a rapid face-cycling + shake animation while waiting for the server's authoritative roll (NET-1) — this is presentation only, never a predicted value.
- **Shape (20s)**: tap dice to select (multi-select, toggle); Re-roll spends 2 Sparks per die beyond your free allotment (from owned Manipulation powers); Nudge ±1 requires exactly one selected die and consumes one of your nudge allotment; Set Face (4 Sparks, or free if you own a "set face free" power) opens the face picker. All three actions are disabled (not just visually, but functionally) once you've hit Done, committed, or passed.
- **Commit (15s)**: tapping a market card opens the zoom sheet and auto-suggests a valid paying combination from your current dice (cost matchers: n-of-a-kind, run, sum≥, specific faces — reuse `Game.Core`'s `ICardRequirement` matchers directly, do not reimplement). Committing marks those dice **spent** for the round and locks further Shaping (CORE-5). Withdraw un-spends them and re-opens Shaping. A player may also commit or pass during **Shape** itself (CORE-5) — the bottom bar's Withdraw/Pass affordances must be available in both phases, not just Commit.
- **Secrecy (NET-2)**: your own "COMMITTED" stamp on the market card is a **local-only echo** — it must never be broadcast or inferable by other clients before Reveal. Opponents' rail cells only ever show a boolean "decided" state, never what they picked.
- **Phase clock (CORE-2)**: server-owned; on expiry the server auto-resolves (Shape → no-op, Commit → pass). The client-side ring/timer is a rendering of the authoritative deadline, not a client-side timer that could drift.
- **Reveal (MKT-3, UI-4)**: uncontested claims resolve immediately; contested claims go to lowest-score priority (ties: fewest cards, then seat order — MKT-4), losers get one 10s re-pick pass from what's left, and any card still contested after that resolves again with remaining losers passing.
- **Upkeep (auto, ~4s)**: unspent dice → 1 Spark each (cap 10, CORE-4); Economy powers add flat Sparks; no-claim consolation is +3 Sparks (MKT-5); market refills from the tiered deck (MKT-1); priority recalculates (MKT-4).
- **Navigation**: Main Menu → Lobby → Game → Standings → (Rematch → Game, or Main Menu → Main Menu). Back button only available from Lobby before the match starts.
- **Reduced motion / animation speed**: the prototype exposes tunables for this (a "reduced motion" toggle collapses all easing to near-instant, and a speed multiplier scales all timers) — mirror this with your existing "Presentation never drives rules" principle (`docs/design/ui-conventions.md`): skipping/speeding animation must never change match state, only how fast it's shown.

## State Management
Render this UI from `MatchSnapshot` (per-observer, hidden-information-filtered) exactly as today — the design adds **no new server-authoritative state**, only local, ephemeral UI state:
- `selectedDiceIndices: int[]` — which of your own dice are currently highlighted (already exists as `GameHudView.SelectedDice`)
- `zoomedCardId: int?` — which market card's inspect sheet is open (local only)
- `facePickerOpen: bool`
- `hintsSeen: { shape: bool, commit: bool }` — persist to `PlayerProfile` if hints should stay dismissed across matches
- `revealStageIndex, revealStage` — which contested-card beat and sub-stage (flip vs result) the Reveal spotlight is currently showing; purely a presentation sequencer over the already-public post-Reveal snapshot data
- Everything else (round, phase, seconds left, Sparks, dice faces, spent flags, market, deck count, owned powers, per-player score/dice/sparks/priority/decided/connection status) is already exposed via `MatchSnapshot`/`PlayerSnapshot`/`CardSnapshot` — no schema changes needed.

## Design Tokens
Sourced from the bound **Industry** design system (`_ds/.../styles.css` / `theme.json`). Map these to your planned `ThemeAsset` (see `docs/design/theming.md`) using the **same semantic names already specified there** — do not invent new token names:

| Semantic token (per theming.md) | Value | Notes |
|---|---|---|
| `surface.base` | `#f2f2f3` | page/board background |
| `surface.raised` | `#e9e9ea` | cards, panels, dice-tray fill |
| `text.primary` | `#1d1f20` | body/heading text |
| `text.muted` | `color-mix(text 55%, transparent)` ≈ `#8d8e8f` | secondary labels, sub-lines |
| `text.inverse` | `#f2f2f3` | text on the accent-900 reveal/menu-hero fields |
| `accent.priority` | `#5980a6` (base) | priority marker, links, focus ring |
| accent ramp steps used | `--color-accent-100…900` (light OKLCH ramp from `#5980a6`) | 100–300 for tints/borders, 700–800 for text-on-tint and "affordable" states, 900 for full-bleed reveal background |
| `state.affordable` | accent-400 border + paper fill, opacity 1 | market card / repick card when dice can pay |
| `state.unaffordable` | neutral-divider border + transparent fill, opacity 0.5 | must pair opacity change with the border/fill change — never color alone (accessibility constraint in theming.md) |
| `state.spent` | dashed divider border + semi-opaque "SPENT" watermark | on dice |
| `state.ready` | accent-700 | "✓ READY" rail state |
| `state.thinking` | text.muted | "○ THINKING" rail state |
| `state.trouble` | (carry over `troubleColor` from existing `PlayerRowView`) | reconnecting / left states — not shown in this prototype's mock data but must be supported |

**Typography**
- Headings: **Barlow Condensed**, weight 600 (default) / 700 (large numerals, wordmark) — `--font-heading`
- Body/UI: **Barlow**, weight 400/500/600 — `--font-body`
- Scale used in this design: 8.5px (rail micro-labels) · 9-11px (eyebrows, tags, costs) · 13-15px (buttons, status line) · 17-22px (round header, dialog title) · 26-44px (VP totals, winner headline, wordmark) · 54-72px (join code, wordmark)
- Letter-spacing: 0.02-0.06em on button labels, 0.14-0.24em on all-caps eyebrow/label text

**Spacing / radius / elevation** — use the existing scale verbatim, do not introduce new values:
`--space-1..8` (3.4/6.8/10.2/13.6/20.4/27.2px), `--radius-sm/md/lg` (2/4/7px — note the game board's cards and dice intentionally use **square corners, not radius**, matching the "blueprint" wireframe-object convention), `--shadow-sm/md/lg` for the reveal/zoom/dialog elevation only (everything else on the board is flat/bordered, no shadows).

**Component conventions**
- **Blueprint frame**: 1px solid border (accent or divider depending on emphasis) + 4 small "+"-shaped corner registration marks — used on the menu hero, market cards, the zoom sheet, the upkeep dialog, the repick sheet, the Done button, and standings rows. This is the system's signature motif — implement it as a single reusable prefab/component (a bordered `RectTransform` + 4 corner-mark `Image`s) rather than redrawing per screen.
- **Primary button**: the *only* solid-fill accent object on any screen, always paired with the blueprint corner marks.
- **Tags/chips**: small pill/rect labels, accent-tinted background + accent-800 text, used for Sparks readout, tier/family labels, owned-power chips, "seen" states.

## Assets
No bitmap assets used. All visuals are CSS-drawn: dice pips (3×3 grid of circles), corner registration marks (small "+"-shaped divs/SVGs), priority marker (CSS triangle), timer ring (inline SVG `<rect>` with `stroke-dasharray`). No icon font/library was used in the prototype; production should use **Lucide at stroke-width 1.5** per the Industry system guide for any icons introduced (e.g. a settings gear, a connection-trouble glyph) — none were strictly required by this design.

## Files
- `Foundry Prototype.html` — the full interactive click-through prototype (self-contained, open in any browser). Reference for every screen, state, and animation described above.
- This `README.md`.

## Cross-references to existing code (for the implementer)
- Cost/requirement logic: reuse `Game.Core.ICardRequirement` + matchers (`NOfAKind`, `Run`, `Sum`, `ContainsFaces`, `Composite`) — do not reimplement the "does this dice selection pay this cost" logic; the prototype's own JS validator exists only because it has no access to your C#.
- Rail/state semantics: extend `PlayerRowView`/`GameHudView` rather than replacing them; the new "auto-shown only when usable" powers strip and the zoom sheet are additive.
- Card display fields: `CardSnapshot.DisplayName/CostText/PowerText/Points/Tier/AffordableNow` already carry everything the market grid and zoom sheet need.
- Theming: this handoff is the first real design pass to feed `ThemeAsset` (E5) — use the token table above as its initial values.
