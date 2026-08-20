# EPIC-14 — Build, CI & Release

**Genre dependency:** none · **Phase:** 6 — but **STORY-14.1 and 14.2 should land in Phase 0**

## Goal

Every commit is verified automatically, and producing a build is one command,
not tribal knowledge.

## Note on sequencing

Although this epic is listed last, the test pipeline is worth standing up in
Phase 0. A rules core with 400 unit tests that nobody runs is worth very little.

---

### STORY-14.1: CI pipeline for tests
- AC1 GitHub Actions runs EditMode tests on every PR.
- AC2 The Unity version is pinned to `6000.5.0f1`, matching `ProjectVersion.txt`.
- AC3 Results are reported on the PR; a red suite blocks merge.
- AC4 The Library folder is cached — an uncached Unity CI run is unusably slow.

`none` · **L**

### STORY-14.2: Static analysis and conventions enforcement
- AC1 `.editorconfig` and analyzer rules enforce the C# conventions in `CLAUDE.md`.
- AC2 CI fails on: a `UnityEngine` reference inside the rules core, a hard-coded
  user-facing string, a hard-coded colour on a themed component.
- AC3 Warnings are errors in CI.

`none` · **M**

### STORY-14.3: Automated builds
- AC1 A one-command build for each target platform.
- AC2 Version and build number are stamped automatically from git.
- AC3 Development and release configurations differ in a documented way.

`none` · **M**

### STORY-14.4: Crash and error reporting
- AC1 Unhandled exceptions are captured with context in release builds.
- AC2 Players can submit a report from the pause menu.
- AC3 Reports carry no personal data.

`none` · **M**

### STORY-14.5: Analytics for balance
- AC1 Match outcomes and card usage are recorded for balance analysis.
- AC2 Opt-in, clearly disclosed, and fully functional when declined.

`partial` · **M**

### STORY-14.6: Release packaging
- AC1 Store assets, icons, and metadata are prepared per target platform.
- AC2 A release checklist exists and has been dry-run at least once.

`none` · **M**
