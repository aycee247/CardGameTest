#!/usr/bin/env bash
#
# Builds the iOS app and uploads it to App Store Connect / TestFlight (STORY-6.7).
# The Unity Editor must be CLOSED — batchmode cannot share the project lock.
#
# Prerequisites (once):
#   - iOS Build Support module installed for the Unity version in use
#   - Xcode signed into the enrolled Apple ID (Xcode ▸ Settings ▸ Accounts)
#   - appleDeveloperTeamID set in ProjectSettings/ProjectSettings.asset
#   - the app record exists in App Store Connect (bundle id com.aaroncornwell.foundry)
#
#   tools/build-ios.sh              # full pipeline: Unity export -> archive -> upload
#   tools/build-ios.sh --archive    # stop after the .xcarchive (no upload)
#
# Remember to bump buildNumber.iPhone in ProjectSettings before each upload — App Store
# Connect rejects a build number it has already seen.

set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$REPO/Builds/iOS"
ARCHIVE="$REPO/Builds/Foundry.xcarchive"
LOG="$REPO/Builds/unity-ios-build.log"

UNITY="$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -n 1 || true)"
if [[ -z "$UNITY" ]]; then
  echo "error: no Unity Hub install found under /Applications/Unity/Hub/Editor." >&2
  exit 1
fi

TEAM_ID="$(sed -n 's/^ *appleDeveloperTeamID: *//p' "$REPO/ProjectSettings/ProjectSettings.asset" | tr -d '[:space:]')"
if [[ -z "$TEAM_ID" ]]; then
  echo "error: appleDeveloperTeamID is empty in ProjectSettings/ProjectSettings.asset." >&2
  echo "       Set it to the 10-character Team ID from developer.apple.com/account (Membership)." >&2
  exit 1
fi

mkdir -p "$REPO/Builds"

echo "== Unity: exporting the Xcode project (first run after a platform switch is slow) =="
"$UNITY" -batchmode -quit -projectPath "$REPO" -buildTarget iOS \
  -executeMethod Game.EditorTools.BuildTools.BuildIos -logFile "$LOG"

if [[ ! -d "$OUT" ]]; then
  echo "error: Unity produced no Xcode project at $OUT — see $LOG" >&2
  exit 1
fi

# Unity emits a workspace when CocoaPods-style dependencies exist; prefer it if present.
XCODE_TARGET=(-project "$OUT/Unity-iPhone.xcodeproj")
if [[ -d "$OUT/Unity-iPhone.xcworkspace" ]]; then
  XCODE_TARGET=(-workspace "$OUT/Unity-iPhone.xcworkspace")
fi

echo "== xcodebuild: archiving =="
xcodebuild "${XCODE_TARGET[@]}" -scheme Unity-iPhone -configuration Release \
  -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" archive \
  -allowProvisioningUpdates DEVELOPMENT_TEAM="$TEAM_ID"

if [[ "${1:-}" == "--archive" ]]; then
  echo "Archive ready at $ARCHIVE (upload skipped)."
  exit 0
fi

echo "== xcodebuild: uploading to App Store Connect =="
EXPORT_PLIST="$REPO/Builds/export-options.plist"
cat > "$EXPORT_PLIST" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key><string>app-store-connect</string>
  <key>destination</key><string>upload</string>
  <key>teamID</key><string>$TEAM_ID</string>
</dict>
</plist>
EOF

xcodebuild -exportArchive -archivePath "$ARCHIVE" \
  -exportOptionsPlist "$EXPORT_PLIST" -allowProvisioningUpdates

echo "Uploaded. Watch processing at https://appstoreconnect.apple.com (TestFlight tab; ~15-60 min)."
