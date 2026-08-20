#!/usr/bin/env bash
#
# Runs the Game.Core test suite headlessly, without opening the Unity Editor.
#
#   tools/run-core-tests.sh              # whole suite
#   tools/run-core-tests.sh Contention   # only tests whose Fixture.Method contains "Contention"
#
# Uses the .NET SDK and nunit.framework.dll that ship inside the Unity installation, so it
# needs no network access and no separately installed dotnet.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

find_first() {
  # Prints the newest match of a glob, or nothing.
  local match
  match="$(ls -d $1 2>/dev/null | sort -V | tail -n 1 || true)"
  [ -n "$match" ] && printf '%s' "$match"
}

DOTNET="$(find_first '/Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet')"
if [ -z "$DOTNET" ]; then
  # No Unity Hub install found — fall back to whatever dotnet is on PATH, so the
  # suite still runs on a machine (or CI runner) with no Unity licence at all.
  DOTNET="$(command -v dotnet || true)"
fi
if [ -z "$DOTNET" ]; then
  echo "error: could not find Unity's bundled .NET SDK under /Applications/Unity/Hub/Editor/*" >&2
  echo "       and no 'dotnet' was found on PATH either." >&2
  echo "       install a Unity editor, install the .NET SDK, or set DOTNET to your own 'dotnet'." >&2
  exit 1
fi

# Prefer the copy resolved into this project; fall back to the editor's built-in package.
NUNIT="$(find_first "$REPO/Library/PackageCache/com.unity.ext.nunit*/net472/unity-custom/nunit.framework.dll")"
if [ -z "$NUNIT" ]; then
  NUNIT="$(find_first '/Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll')"
fi
if [ -z "$NUNIT" ]; then
  # No Unity-bundled nunit.framework.dll available — warn and proceed without
  # NUnitPath. CoreTests.csproj falls back to restoring NUnit from NuGet in
  # that case, so the suite still runs on a machine that has never opened the
  # project in Unity.
  echo "warning: could not find nunit.framework.dll (com.unity.ext.nunit); falling back to NuGet." >&2
fi

# No --nologo here: `dotnet run` does not define it and forwards unrecognised options straight
# to the program, where it would be read as a test filter. DOTNET_NOLOGO above does the job.
ARGS=("$DOTNET" run
  --project "$REPO/tools/CoreTests/CoreTests.csproj"
  -v quiet)

# Only pass -p:NUnitPath when we actually resolved one — an empty value would
# defeat CoreTests.csproj's own '$(NUnitPath)' == '' check and skip the NuGet
# fallback.
if [ -n "$NUNIT" ]; then
  ARGS+=(-p:NUnitPath="$NUNIT")
fi

# Only add the `--` separator when there is actually a filter to forward, otherwise dotnet
# hands the bare separator to the program as an argument.
if [ "$#" -gt 0 ]; then
  ARGS+=(-- "$@")
fi

exec "${ARGS[@]}"
