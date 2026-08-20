# E0 — Unblock the build

**Phase:** 0 · **Blocks:** everything

Nothing can be validated until these are true. All of it is small.

---

### STORY-0.1: Regenerate and commit scenes and card assets
**As a** developer **I want** the committed assets to match the generators **so
that** opening the Game scene actually starts a match.

The committed `Game.unity` was last regenerated at `4c8538e`, while
`SceneScaffolder` was last changed at `22c69eb` (M4). The `GameSceneBootstrap`
GUID appears in no committed asset, and there is no `PlayerRow` template and no
`Timer` object. Since `HotSeatHost.autoStartOnLoad` is `false` and
`GameSceneBootstrap` is what calls `StartMatch()`, pressing Play yields a dead
board. Separately, only 36 of the 48 cards are committed as assets.

- AC1 **Foundry ▸ Generate Starter Deck** run; all 48 card assets committed with
  their `.meta` files.
- AC2 **Foundry ▸ Generate Scenes & Build Settings** run; all four scenes committed.
- AC3 Opening Game and pressing Play starts a hot-seat match.
- AC4 The M4 standings rail is present and correct after regeneration.
- AC5 `Assets/Scenes/SampleScene.unity` (template leftover) removed.

**S**

### STORY-0.2: Fix the boot hang
`GameBootstrap.cs:53` carries the repo's only TODO. If `UnityServices
.InitializeAsync()` or sign-in throws — no network, or UGS unlinked, which is the
current state — the app logs an error and sits on "Loading…" forever. This is the
most likely first-run failure on a TestFlight device.

- AC1 A UGS failure surfaces a readable message with a retry.
- AC2 The player can reach hot-seat play without UGS.
- AC3 Verified by launching with networking disabled.

**M**

### STORY-0.3: Link Unity Gaming Services
`UnityConnectSettings.m_Enabled: 0` — the project is not linked, so online cannot
work in a build at all.

- AC1 Project linked to a Unity Cloud org.
- AC2 Authentication, Relay and Lobby enabled in the dashboard.
- AC3 Anonymous sign-in succeeds in a device build.

**S**

### STORY-0.4: Type-check SessionManager against the real package
`SessionManager`'s own doc comment warns that `SessionOptions`,
`WithRelayNetwork()`, `CreateSessionAsync`, `JoinSessionByCodeAsync` and
`ISession.Code` are version-sensitive and that Unity has renamed members across
minors. **This code has never been compiled against the installed package.**

- AC1 `tools/verify-unity-compile.sh` passes with `Game.Networking` included.
- AC2 Any renamed member fixed against `com.unity.services.multiplayer` 2.2.1.

**S** — but it is the riskiest unknown in the repo, so do it early.

### STORY-0.5: Rewrite the project README
`Assets/_Project/README.md` is stale by four milestones — it documents
`GameState`, `LocalGameSession`, `GameStateSnapshot`, a `DiceCards` menu, 8
cards, and a turn-based Yahtzee loop. None of it exists. It is actively
misleading as a setup checklist.

- AC1 Rewritten against the actual code, menus and types.
- AC2 Setup steps followed end to end on a clean clone.

**S**

### STORY-0.6: CI for the core suite
Moved forward from E6 — the core suite needs no Unity licence, so there is no
reason to wait. `tools/CoreTests` is plain .NET 8 plus `nunit.framework.dll`;
with a NuGet fallback for NUnit it runs on any machine and any hosted runner.

- AC1 `tools/CoreTests/CoreTests.csproj` restores NUnit from NuGet when
  `NUnitPath` is not supplied, and keeps the Unity-bundled DLL when it is.
- AC2 `tools/run-core-tests.sh` falls back to `dotnet` on PATH when no Unity
  Hub install is found, and runs without a `Library/` folder.
- AC3 A GitHub Actions workflow runs the suite on every push and PR; a red
  suite blocks merge.
- AC4 Machine-readable test output (for PR annotations) — may ship later; the
  exit code alone gates CI correctly today.

**M**
