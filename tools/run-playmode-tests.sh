#!/usr/bin/env bash
#
# Runs the project's PlayMode suite headless. The Unity Editor must be CLOSED — batchmode
# cannot share the project lock.
#
# Always filters to Game.PlayModeTests: the manifest lists com.unity.netcode.gameobjects in
# "testables" (that is what makes NGO's NetcodeIntegrationTest harness compile for STORY-2.1),
# which also exposes NGO's own several-hundred-test suite to the runner. Without the filter a
# "run everything" pass runs Unity's tests, not ours.
#
#   tools/run-playmode-tests.sh                 # the whole project suite
#   tools/run-playmode-tests.sh Forged          # -testFilter substring on test names
#
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -n 1 || true)"
if [[ -z "$UNITY" ]]; then
  echo "error: no Unity Hub install found under /Applications/Unity/Hub/Editor." >&2
  exit 1
fi

RESULTS="$(mktemp -t playmode-results).xml"
LOG="$(mktemp -t playmode-log)"

ARGS=(-batchmode -projectPath "$REPO" -runTests -testPlatform PlayMode
      -assemblyNames Game.PlayModeTests -testResults "$RESULTS" -logFile "$LOG")
if [[ $# -ge 1 && -n "$1" ]]; then
  ARGS+=(-testFilter "$1")
fi

"$UNITY" "${ARGS[@]}" || true   # exit code is unreliable across versions; the XML is the truth

if [[ ! -f "$RESULTS" ]]; then
  echo "error: no results file was produced — is the Unity Editor still open? Log: $LOG" >&2
  exit 2
fi

python3 - "$RESULTS" <<'EOF'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
for tc in root.iter('test-case'):
    print(' ', tc.get('result', '?').ljust(7), tc.get('name'))
total, passed, failed = root.get('total'), root.get('passed'), root.get('failed')
print(f"\n{passed} passed, {failed} failed of {total}   ({root.get('result')})")
sys.exit(0 if root.get('result') == 'Passed' and int(total) > 0 else 1)
EOF
