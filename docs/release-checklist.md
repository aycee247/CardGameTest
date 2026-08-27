# Release checklist — TestFlight

The exact sequence that shipped build 1.0 (1) on 2026-08-26, kept honest: every
step below was executed at least once, and the **Pitfalls** section is things
that actually went wrong that day, not speculation. STORY-6.7 AC3.

## One-time setup (already done; here for a new machine or team member)

- Unity Hub: **iOS Build Support** module installed for the project's editor
  version (`ProjectSettings/ProjectVersion.txt`).
- Xcode signed into the Apple ID (Xcode ▸ Settings ▸ Accounts) with the paid
  Apple Developer Program team visible.
- `ProjectSettings/ProjectSettings.asset` carries `appleDeveloperTeamID` and
  `appleEnableAutomaticSigning: 1`.
- The keychain has granted codesign access: the **first** archive must run from
  an interactive terminal so the "codesign wants to sign using key…" dialog can
  appear — answer with the **Mac login password** and **Always Allow** (not
  Allow). After that, headless runs sign fine.
- App Store Connect has the app record (**Foundry Dice**,
  `com.aaroncornwell.foundry`) and the account has **trader status declared**
  and the Program License Agreement **Active** — see Pitfalls.

## Every release

1. **Green suites first**: `tools/run-core-tests.sh` (all pass) and, editor
   closed, `tools/run-playmode-tests.sh` (all pass). The nightly PlayMode badge
   on `docs/README.md` should be green.
2. **Regenerate if needed**: any change to `StarterDeck`, the theme, fonts, or
   `SceneScaffolder` since the last release means re-running the generators
   (fonts → theme → scenes) and committing the results.
3. **Bump the build number**: `buildNumber.iPhone` in
   `ProjectSettings/ProjectSettings.asset`, +1 from the last upload. App Store
   Connect rejects a number it has already seen. Commit the bump.
4. **Build & upload** (editor closed):
   ```
   tools/build-ios.sh                # Unity export -> archive -> upload
   tools/build-ios.sh --skip-unity   # reuse Builds/iOS when only re-signing
   tools/build-ios.sh --archive      # stop before upload
   ```
   Ends with "Upload succeeded". Export compliance is answered permanently by
   `IosPostBuild` (`ITSAppUsesNonExemptEncryption = NO`, standard HTTPS only),
   so no questionnaire appears in the browser.
5. **Wait for processing** (15–60 min): App Store Connect ▸ Apps ▸ Foundry Dice
   ▸ TestFlight shows the build "Ready to Submit". Internal testers with
   automatic distribution get it immediately — no review for internal.
6. **Device sanity pass** (from TestFlight, on a real phone): icon + name,
   launch → wordmark with no Unity logo, stays portrait when rotated, one solo
   round plays. For a build with netcode changes: host a match on the device
   and join from a second one.
7. **External testers** (the friend group): promote the build to the external
   group — the **first** build per version needs Beta App Review (~1 day);
   subsequent builds of the same version usually skip it.

## Pitfalls actually hit on 2026-08-26

- **`errSecInternalComponent` at CodeSign**: archive ran from a detached
  process, so the keychain couldn't show its permission dialog. Fix: run the
  archive once interactively and click **Always Allow** (see one-time setup).
- **Terminal line-wrap breaking a pasted `xcodebuild` command**: multi-line
  paste ran each wrapped line as its own command. Use `tools/build-ios.sh`
  (short invocation) instead of pasting raw xcodebuild lines.
- **TestFlight invite silently never dispatched** — tester showed "Invited"
  with a blank date, no email, nothing in the TestFlight app, resends did
  nothing. Cause: **undeclared EU trader status** (the yellow DSA banner on the
  ASC Apps page). Declaring it (Business ▸ trader status) made the invite
  appear within a minute. Check that banner (and pending agreements) before
  debugging anything else about missing invites.
- **dSYM warning on upload** ("archive did not include a dSYM for
  UnityRuntime.framework"): benign for TestFlight; only affects symbolication
  of Unity-engine frames in crash reports. Revisit if engine-frame crashes
  ever need symbolizing.
