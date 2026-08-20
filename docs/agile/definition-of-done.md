# Definition of Done

A story is **Done** only when every applicable box is ticked. "It works on my
machine" is not a state this project recognises.

## Universal (every story)

- [ ] Acceptance criteria all demonstrably met
- [ ] `tools/run-core-tests.sh` is green
- [ ] `tools/verify-unity-compile.sh` is green if Unity assemblies changed —
      remembering it checks types, **not** asmdef boundaries
- [ ] Code compiles with **zero warnings** in the affected assemblies
- [ ] Public types and non-obvious logic have XML doc comments explaining *why*
- [ ] No `UnityEngine` reference has leaked into `Game.Core`
- [ ] `.meta` files for all new assets are committed
- [ ] CI is green
- [ ] Self-review done: the full diff read end-to-end, tests exist for the
      change, any doc this touches was updated
- [ ] Netcode, migration, or secrecy-surface changes went through a story
      branch and PR; everything else may land on `main` directly

## Logic and rules changes

- [ ] EditMode tests cover the new behaviour, including edge cases
- [ ] Tests are plain `[Test]` methods — the headless runner rejects
      `[TestCase]`, `[UnityTest]` and friends with exit code 2
- [ ] Tests are deterministic — no wall-clock time, no unseeded RNG
- [ ] `SecrecyGateTests` and `BalanceGateTests` still pass, and neither was
      weakened to make them

## Presentation and UI changes

- [ ] Works at 16:9 and 4:3, and at 1280x720 through 3840x2160
- [ ] Legible at **six players on the narrowest supported device** (UI-1)
- [ ] No state communicated by colour alone
- [ ] No `new Color(...)` literal added
- [ ] Scenes regenerated and committed if `SceneScaffolder` changed

## Networked changes

- [ ] Validated on the server; client cannot force an illegal state
- [ ] Hidden information is never sent to a client that must not see it
- [ ] Tested with a mid-match disconnect and rejoin
- [ ] Host and client behave identically in the host-plus-client case
- [ ] Covered by a PlayMode integration test — the networking layer's standing
      gap is that it has none. **Bootstrap clause:** until STORY-2.1 lands the
      PlayMode suite, networked changes are instead verified manually with two
      editor instances, and the commit/PR says so explicitly.

## Content and data changes

- [ ] `StarterDeck` was edited, not the generated card assets
- [ ] Generators re-run and assets committed with their `.meta` files
- [ ] `BalanceGateTests` still passes; the full report reviewed with
      `FOUNDRY_BALANCE=1 tools/run-core-tests.sh Balance`

## Definitely not Done

- Commented-out code left "for later"
- A `TODO` with no story id next to it
- A test marked `[Ignore]` without a linked story explaining why. (The
  `FOUNDRY_BALANCE`-gated balance report's `Assert.Ignore` is the sanctioned
  exception — it is opt-in by design.)
