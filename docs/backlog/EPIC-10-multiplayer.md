# EPIC-10 — Online Multiplayer

**Genre dependency:** partial — the transport is agnostic, the command payloads are not
**Phase:** 4 — **early, before the rules get broad**

## Goal

Server-authoritative online play over Netcode for GameObjects with Relay and
Lobby, with hidden information provably never leaving the server.

## The sequencing argument

This epic lands **after the rules core is green and the vertical slice plays**,
but **before the rules get broad**. Both halves of that matter:

- *After the core* — a server-authoritative card game is just the rules core
  executed on the server with commands arriving over the wire. If the core is
  deterministic and tested first, multiplayer is a transport problem. If they are
  built in parallel, every rules bug presents as a desync, which is the most
  expensive class of bug to diagnose in a networked game.
- *Before the breadth* — hidden information and authority are **state-model
  properties**. Discovering in month 5 that `GameState` assumed every client sees
  everything is a rewrite, not a patch. Every rule written after this epic is
  written against a working authoritative pipeline.

## Scope boundary

**No host migration in the MVP.** On host disconnect the match ends and players
return to the lobby with a stored result. Host migration for an authoritative
hidden-information game is genuinely hard and is not what "feature-complete
alpha" should mean.

---

### STORY-10.1: Network bridge and command RPC
- AC1 `GameNetworkBridge : NetworkBehaviour` exposes exactly two RPCs:
  submit-command and apply-events.
- AC2 The server asserts `senderId == cmd.Actor` before validating — spoof guard.
- AC3 The server validates with the **identical** `Validate()` the client used
  for its UI affordances. No rule logic is duplicated across the boundary.
- AC4 An illegal command is rejected to the sender only, with a localization key.
- AC5 `GameState` is **not** replicated via `NetworkVariable`/`NetworkList` —
  those give all-or-nothing visibility and no redaction hook.

`partial` · Depends on: EPIC-02 · **L**

### STORY-10.2: Hidden information redaction
- AC1 `StateProjector.Project(state, viewer, policy)` is the only object a client
  ever receives.
- AC2 A hidden card projects to `{InstanceId, Zone, ZoneIndex, Owner, FaceUp:false}`
  with **no `CardId`**.
- AC3 The RNG seed and state are never transmitted, logged client-side, or saved
  client-side.
- AC4 Deck contents are never sent, only counts — unless the ruleset's
  `IVisibilityPolicy` declares composition public.
- AC5 **Leak tests assert on bytes**, not structure: the serialized payload for
  player B contains no `CardId` string from A's hand or deck. A structural
  assertion misses leaks through newly added fields.

`none` · **L** — the single most important story in this epic

### STORY-10.3: Session transport implementations
- AC1 `NetworkSessionTransport` and `HostSessionTransport` implement the same
  `ISessionTransport` the local loopback already implements.
- AC2 Presentation is unchanged between single-player and multiplayer.
- AC3 Switching modes is a composition-root change only.

`none` · Depends on: STORY-1.4 · **M**

### STORY-10.4: Relay, Lobby and authentication
- AC1 Anonymous sign-in via Unity Authentication.
- AC2 Create/join by code, and a public lobby list.
- AC3 Relay handles NAT traversal; no port forwarding required.
- AC4 Service failures surface actionable messages, not raw exception text.

`none` · **L**

### STORY-10.5: Connection lifecycle and reconnect
- AC1 A disconnected client rejoins and receives a fresh **redacted snapshot** —
  the same `Project()` path as mid-match join and as first load.
- AC2 A grace period holds the seat before forfeiting.
- AC3 Host disconnect ends the match cleanly with a stored result.
- AC4 Connection state is always visible to the player.

`partial` · **L**

### STORY-10.6: Desync detection
- AC1 The server hashes canonical state at each turn boundary and broadcasts it.
- AC2 Clients hash their redacted view against a redacted server recomputation.
- AC3 Development builds only.
- AC4 A mismatch is logged with enough context to reproduce, at the moment of
  divergence rather than three turns later.

`none` · **M**

### STORY-10.7: Turn timers and idle handling
- AC1 The **server** owns the clock; clients only display it.
- AC2 Timeout triggers a defined default action, not an undefined state.
- AC3 Repeated timeouts forfeit the match.
- AC4 A local pause never stops the server clock.

`partial` · **M**

### STORY-10.8: Netcode integration tests
- AC1 NGO's `NetcodeIntegrationTest` spins an in-process host + 2 clients.
- AC2 Asserts: an injected illegal command is rejected and does not mutate server
  state; a client's received bytes contain zero opponent `CardId`s; mid-match
  join produces a correct redacted view; the dev state hash matches all peers.
- AC3 Tested under simulated latency and packet loss.
- AC4 Runs nightly in CI.

`none` · **L**

### STORY-10.9: Multiplayer front end
- AC1 Lobby, matchmaking, and connection screens in UI Toolkit.
- AC2 The main menu's multiplayer entry point is enabled and reachable.
- AC3 Opponent presentation shows connection state and remaining time.

`none` · Depends on: EPIC-05 · **M**

### STORY-10.10: Spectating
- AC1 A spectator receives a projection with a spectator visibility policy.
- AC2 Spectators cannot submit commands — enforced server-side, not by UI.

`partial` · **M**
