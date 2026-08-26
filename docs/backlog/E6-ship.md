# E6 — Ship

**Phase:** 6

---

### STORY-6.1: Nightly PlayMode lane
Per-push CI for the core suite moved to STORY-0.6. What remains here is the
slow lane.

- AC1 PlayMode/netcode tests (STORY-2.1) run nightly in CI, not per-push.
- AC2 A red nightly run is visible without opening the Actions tab (badge or
  notification).

**M**

### STORY-6.2: iOS build readiness
Player settings are partly filled in but the project is not build-ready.

- AC1 `productName` is the game's name — it is currently `CardGameTest`, which
  is what would appear on the device and in TestFlight.
- AC2 `buildNumber.iPhone` ≥ 1 and monotonic per upload; it is currently `0`.
- AC3 Signing team and provisioning configured; both are empty.
- AC4 App icons and a launch screen exist; neither does.
- AC5 A `link.xml` preserves `Newtonsoft.Json`, `Unity.Services.*` and the
  `[Serializable]` save types under IL2CPP stripping.
- AC6 `applicationIdentifier.Standalone` no longer carries the template's
  `com.DefaultCompany.2D-URP`.
- AC7 Apple privacy compliance: a privacy manifest (`PrivacyInfo.xcprivacy`)
  ships in the Xcode project, and the App Store Connect data-collection
  declarations cover what UGS actually collects (auth identifiers, IP for
  Relay). Unity 6 generates much of this — verify rather than assume.

**L**

### STORY-6.3: Lock orientation
Auto-rotate is enabled and all four rotations are allowed, but the Game scene is
laid out for portrait 1080×1920 with fixed offsets, and `OrientationWatcher` —
though correctly written — is placed in **no scene** and subscribed to by
nothing. Landscape on a phone will be badly cropped.

- AC1 Portrait locked for the demo, **or** `OrientationWatcher` wired and
  landscape actually laid out.
- AC2 Verified on the narrowest supported device.

**S**

### STORY-6.4: Dependency hygiene
- AC1 `com.unity.addressables` is installed with **zero** usage — no
  `AssetReference`, no `Addressables.` call, no settings asset. Remove it, or
  adopt it for the E5 art.
- AC2 `com.unity.ai.assistant` is a **pre-release** package; remove before a
  shipping build.
- AC3 `com.unity.visualscripting` and `com.unity.timeline` reviewed for use.

**S**

### STORY-6.5: Core gaps blocking resume and replay *(optional for the demo)*
- AC1 `SeededDiceRoller` accepts a state so a roller can be reconstructed
  mid-match; it currently exposes live state with no way back in.
- AC2 `MatchState` serializes and rehydrates — only the lossy per-observer
  `MatchSnapshot` exists today.
- AC3 Deck shuffling uses the portable xorshift rather than `System.Random`, so
  deck order is reproducible from a seed the way dice already are.
- AC4 Round-trip and determinism tests cover all three.

**L** — nothing in the demo needs this; it is the price of admission for
save/resume, replay and bug-report reproduction later.

### STORY-6.6: Crash and error reporting
`UnityAnalyticsSettings` and `CrashReportingSettings` are both disabled.

- AC1 Unhandled exceptions captured with context in release builds.
- AC2 No personal data in reports.

**M**

### STORY-6.7: TestFlight
- AC1 Build uploaded and installable.
- AC2 A full online match completes on two physical devices from TestFlight
  builds.
- AC3 Release checklist written and dry-run once.
- AC4 Export compliance answered in App Store Connect (standard HTTPS-only
  encryption → exempt), so TestFlight builds don't stall on the questionnaire.

**M**

### STORY-6.8: Branded boot and splash
The scaffolded Boot scene showed a stale "Dice Cards" label, and the Unity
splash ran on template defaults over a dark background that clashed with the
blueprint surface.

- AC1 The boot label matches the product name (**Foundry**) in the wordmark
  styling the theme/MainMenu already use — no new color or font literals.
- AC2 Splash settings deliberately chosen: Unity 6 allows disabling the Unity
  logo even on a Personal license — decided **off**, background on the theme
  surface so any residual flash matches.
- AC3 The iOS launch-screen background matches the theme background, so
  launch → splash → Boot reads as one continuous surface.
- AC4 Scenes regenerated and committed.

**S** — filed as issue #57, after the E6 epic was first written.
