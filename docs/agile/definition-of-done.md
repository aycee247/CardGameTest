# Definition of Done

A story is **Done** only when every applicable box is ticked. "It works on my
machine" is not a state this project recognises.

## Universal (every story)

- [ ] Acceptance criteria all demonstrably met
- [ ] Code compiles with **zero warnings** in the affected assemblies
- [ ] No new `Debug.Log` left in runtime code paths (use the logging wrapper)
- [ ] Public types and non-obvious logic have XML doc comments
- [ ] No `UnityEngine` reference has leaked into the rules core assembly
- [ ] `.meta` files for all new assets are committed
- [ ] Branch merged via PR with at least one review

## Logic and rules changes

- [ ] EditMode unit tests cover the new behaviour, including edge cases
- [ ] Tests are deterministic — no wall-clock time, no unseeded RNG
- [ ] Existing test suite is green

## Presentation and UI changes

- [ ] Works at 16:9 and 4:3, and at 1280x720 through 3840x2160
- [ ] Works under both themes (light/dark or whatever the theme set is)
- [ ] Keyboard and gamepad navigation reach every interactive element
- [ ] No hard-coded colours or fonts — all values come from the theme asset
- [ ] No hard-coded user-facing strings — all routed through localization

## Networked changes

- [ ] Validated on the server; client cannot force an illegal state
- [ ] Hidden information is never sent to a client that must not see it
- [ ] Tested with simulated latency and packet loss
- [ ] Tested with a mid-match disconnect and rejoin
- [ ] Host and client behave identically in the host-plus-client case

## Content and data changes

- [ ] Data validates against the content validator with no errors
- [ ] Save data written by the previous build still loads (or a migration exists)

## Definitely not Done

- Commented-out code left "for later"
- A `TODO` with no story id next to it
- A test marked `[Ignore]` without a linked story explaining why
