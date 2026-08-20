# E2 — Netcode coverage & live validation

**Phase:** 2 · **Depends on:** E0

## Why this comes before polish

Three milestones are marked done and have never met a player, and the networking
layer has no automated coverage at all. Animating a reveal beat that playtesting
says to restructure is wasted work; shipping RPC code that has never been
executed is worse.

---

### STORY-2.1: PlayMode netcode integration tests
`Tests/PlayMode/` contains an asmdef referencing `Game.Networking`, `Game.App`
and `Unity.Netcode.Runtime` — and **no test files**. This story fills it.

- AC1 NGO's `NetcodeIntegrationTest` spins an in-process host + 2 clients.
- AC2 An illegal intent is rejected and does not mutate server state.
- AC3 A client's received `StateRpc` bytes contain no opponent `PendingCardId`
  before Reveal — asserted on the **bytes**, matching the standard set by
  `SecrecyGateTests`.
- AC4 A mid-match join produces a correct redacted view.
- AC5 A client that drops and returns reclaims its seat by auth key.
- AC6 A forged server→client RPC from a peer is rejected by `FromServer`.

**L** — the single biggest coverage gap in the repo.

### STORY-2.2: Fix seat-key ordering and the ready-up gate
- AC1 `MatchLauncher` passes `orderedSeatKeys` to `ServerStartMatch`; reconnect
  no longer depends on a race with `RegisterIdentityRpc`.
- AC2 A ready-up gate replaces `autoStartOnServer` firing on `Start()`, so a
  client that finishes the NGO scene load late still gets a seat.
- AC3 Covered by STORY-2.1's harness.

**M**

### STORY-2.3: Hot-seat playtest (M2's open gate)
- AC1 Played at 2 and 4 seats by someone who did not write it.
- AC2 The handoff privacy boundary holds in practice, not just in
  `HotSeatTests`.
- AC3 Findings written up; anything blocking becomes a story.

**M**

### STORY-2.4: Two-device online over Relay (M3's open gate)
- AC1 A full 10-round match completes between two physical devices.
- AC2 Join-by-code works from a cold start on both.
- AC3 Commit secrecy holds — verified by observation, not only by test.

**M**

### STORY-2.5: Six-seat table with a real drop (M4's open gate)
- AC1 Six seats complete a match.
- AC2 A player force-quits mid-Commit; the phase closes immediately rather than
  burning the timer, and they still appear in final standings.
- AC3 They reconnect and reclaim their seat, cards and score.
- AC4 Host drop shows the standings screen and says the scoring powers are
  unresolved.
- AC5 Wall-clock timing recorded per player count — §11 flags that six-player
  rounds resolve slower because contention triggers re-picks more often, and
  simulation cannot measure that.

**L**

### STORY-2.6: Answer the design doc's human questions
§10 and §11 explicitly mark these as questions the balance harness cannot settle.

- AC1 Does lowest-score priority *feel* bad to a leader? The numbers say it does
  not over-correct; whether being caught feels unfair is a human read. Fallback
  is rotating priority with a smaller consolation bonus.
- AC2 Do Sparks feel worth tracking, or are they one system too many?
- AC3 Is the 11–13 minute target met at six players in practice?

**M**

### STORY-2.7: Hot-seat phase clock
UI-2 is wired but only visible online — `SecondsLeft` is negative in hot-seat so
the label hides itself. Decide whether hot-seat wants a clock at all and make the
behaviour deliberate rather than incidental.

**S**

### STORY-2.8: Fix per-frame HUD refresh
`GameHudPresenter.Update()` calls `Refresh()` every frame whenever
`SecondsLeft >= 0f`, so the entire board re-renders per frame online.

- AC1 The board re-renders on snapshot change; only the countdown ticks per frame.
- AC2 Verified in the Profiler at six seats.

**S**
