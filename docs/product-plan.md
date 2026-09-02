# Product plan — feedback and monetization

`docs/backlog/roadmap.md` ends at P6 ("Ship: CI, iOS readiness, TestFlight") and the
milestone table in `game-design.md` §8 ends at M6 with the gate "TestFlight build".
This document is the continuation: how the game collects feedback from here, and how
it eventually earns money. Written 2026-09-02, at build 11, friends-group TestFlight.

It deliberately borrows three frameworks and names them where they apply, so a
decision can be traced back to a principle rather than a mood:

- ***Hooked*** (Nir Eyal) — the Trigger → Action → Variable Reward → Investment loop,
  used for the session-return loop and for feedback collection itself.
- ***Nudge*** (Thaler & Sunstein) — choice architecture: defaults, framing, salience.
  Held to the book's own standard ("nudge for good"): every nudge here is transparent
  and one tap to decline.
- ***Actionable Gamification*** (Yu-kai Chou) — the Octalysis core drives. Foundry's
  live drives are **Unpredictability** (dice), **Accomplishment** (engine building),
  **Social Influence / Relatedness** (friends by code — the strongest, and the only
  acquisition channel), and **Ownership** (your engine). Everything below amplifies
  those white-hat drives; nothing below may add black-hat pressure (loss-aversion
  timers, FOMO, gacha).

---

## Part 1 — Feedback

Current stage: **friends & family TestFlight** (~10–30 known testers, coordinated
sessions). At this scale, feedback is read by eye and conversation — the tooling
exists to make those conversations concrete, not to build dashboards.

### F1. Instrument before asking

Fifteen friends' opinions can't be interpreted without knowing what actually happened
in their sessions. A minimal telemetry layer (UGS Analytics — same vendor as auth,
Relay, Lobby; one dashboard) records, per device:

| Event | Parameters | What it answers |
|---|---|---|
| `match_started` | mode, players | Does anyone play at all? Which mode? |
| `phase_ended` | phase, round, seconds | Where does time actually go? A Shape/Commit phase that runs its full timer online is the **confusion proxy** — someone didn't understand or couldn't decide. |
| `round_completed` | round, seconds | Is the 11–13 minute target (design doc §11) real? |
| `match_completed` | mode, players, seconds, local_rank | The funnel's floor: do matches *finish*? |
| `match_abandoned` | round | Where matches die. |
| `match_pulse` / `match_pulse_comment` | rating, comment | The post-match one-tap rating (F2). |
| `feedback_opened` | — | Is the feedback door being used? |

The funnel to watch: install → first match started → first match **completed** →
second session → second *week*. The last step is the only one that predicts anything.

Implementation notes: the telemetry service lives in `Game.App` (the composition
root) behind an `ITelemetry` interface — never in `Game.Core`, which is pure C#.
Custom events must also be **defined in the UGS dashboard Event Manager** (Analytics
▸ Event Manager) or the SDK silently drops them. Crash reporting rides on Unity
Cloud Diagnostics (STORY-6.6).

### F2. In-app feedback entry points

- **Post-match pulse** on the end screen: "HOW WAS THAT MATCH?", five one-tap rating
  buttons, then an optional one-line comment. *Hooked*: the match end is the peak
  emotion (peak-end rule) — the moment with the highest honest response rate.
  *Nudge*: shown by default, one tap to answer, ignorable without cost, and it never
  blocks REMATCH — the pulse must never tax the thing it measures.
- **SEND FEEDBACK** in Settings: opens a pre-addressed email with build and device
  info filled in. TestFlight's native screenshot feedback (take a screenshot →
  "Share Beta Feedback") is mentioned in every build's What to Test notes.

### F3. The playtest program

Telemetry says *what* happened; play nights say *why*. Issues #9–#12 are the unrun
playtests; `game-design.md` §11 holds the questions they exist to answer (priority
feel, whether Sparks are worth tracking, whether 11–13 minutes holds at six players).

**Cadence**: weekly. Build ships early in the week → scheduled group play night
(the game needs full lobbies — ad-hoc solo testers cannot exercise it) → findings
become GitHub issues (E2 STORY-2.3 AC3).

**Session protocol**:

1. First-timers play match 1 with **no verbal help**. That is the onboarding test —
   if it needs narration, the game failed, not the player.
2. Observe, don't rescue. Note every timer expiry and every rules question asked
   aloud; each one is a finding.
3. Fixed five-question debrief, mapped to §11: Where was the fun peak? Where were
   you confused? Did priority feel fair? Would you play again tomorrow? Who would
   you invite?
4. Findings go onto the GitHub Issues board — that is where story status lives.

**Close the loop** (*Hooked* — investment): every build's TestFlight release notes
name what tester feedback changed ("You said the reveal was too fast → the reveal
beat is now staged"). Testers who see their input land keep giving it, and keep
showing up. **Nudge for response rate**: the pulse is default-shown rather than an
opt-in survey link, and play-night invites state the social norm ("5 of 7 rated
last week's matches").

**Gate to public beta** (next stage, deliberately later): N consecutive matches
without desync or crash; first-timers finish a match unaided; the §11 questions have
answers; and at least half the group returns to a second play night *unprompted* —
the only retention signal that matters at this scale.

---

## Part 2 — Monetization

Ambition: **meaningful side income** (hundreds to low thousands of dollars a month),
no paid user acquisition. That rules the model choice as much as taste does.

### The model: free base game + paid expansion decks; cosmetics second. No ads, no loot boxes, no energy.

1. **The friend-code loop is the business.** Social Influence is the game's strongest
   Octalysis drive and its only acquisition channel. A paid-up-front price turns
   every invite into a $5 ask and kills the loop; free-to-join keeps every player an
   acquisition channel.
2. **Expansion decks are the natural SKU.** Cards are already data — the `StarterDeck`
   DSL, the generator, the balance harness. A deck is the sellable unit this
   architecture was accidentally built for, and buying one is board-game buyer
   psychology: transparent one-time purchase, white-hat Ownership + Accomplishment,
   no loot-box review risk.
3. **"The host owns the box."** An expansion owned by the *host* is in play for
   everyone at the table that match. This kills pay-to-win in shared lobbies, makes
   each purchase a gift to the whole group — the buyer gets to be the one who brings
   the new deck — and creates the honest reciprocity nudge that sells the second
   copy: others buy it to host their own nights. Everyone experiences the content
   before anyone is asked to pay for it.
4. **Cosmetics as the second line.** `DiceSkin` and the profile's owned/selected
   fields already exist (E5, issues #28–#31 finish rendering); themes exist but are
   baked at scene generation and need runtime switching. Pure Ownership drive, zero
   balance risk.
5. **Rejected**: ads (no natural slot in a simultaneous six-phase round, and they
   poison the premium-board-game feel), loot boxes/gacha (black-hat, wrong
   audience), energy/timers (black-hat), subscriptions (no content cadence to
   justify one yet).

**Store choice architecture** (*Nudge*, for when a store exists): real-currency
prices only — no gem intermediary; one clearly marked default bundle as the anchor;
restore-purchases prominent; family sharing on; declining always one frictionless
tap; no countdowns, ever.

**Pricing sketch** (validate at soft launch): base game free with all 48 starter
cards · expansion decks $3.99–4.99 · dice skin packs $0.99–1.99 · themes $1.99 ·
launch "Founder's Bundle" ~$9.99. Reality check: ~$1k/month ≈ ~300 expansion sales
a month after Apple's 15% small-business cut — plausible only on top of a working
retention loop and organic friend-invite growth. Which is why:

### Sequencing — retention before revenue

A store bolted onto a game nobody returns to earns nothing. The *Hooked* loop has
two weak links today: **external triggers** (nothing brings a player back) and
**investment** (nothing persists — no stats, no history, no loadout).

| Phase | What | Gate to the next phase |
|---|---|---|
| **1 — now** | F1 telemetry, F2 feedback UI, F3 play nights | F3's gate criteria |
| **2** | Retention: persistent stats in the profile (matches, wins — the cheapest investment feature), rematch flow polish, onboarding fixes from play nights; public TestFlight | Testers return week over week unprompted |
| **3** | Durable identity (Sign in with Apple / Game Center link + Cloud Save — purchases cannot live in an editable local `profile.json` that dies on reinstall), finish E5 cosmetics with runtime selection, App Store launch **free** with cosmetic IAP | Stable public matches, first organic installs |
| **4** | Deck abstraction in Core (deck ids, multiple databases, deck-selection API, balance harness extended to mixed pools — the balance work, not the data entry, is the real cost), first expansion deck under "the host owns the box", Unity IAP + server-validated entitlements | — |

Prerequisites are filed as labeled GitHub issues so they are visible, but nothing in
phases 2–4 is built until the phase before it passes its gate.
