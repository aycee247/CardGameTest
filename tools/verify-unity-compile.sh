#!/usr/bin/env bash
#
# Type-checks every first-party script against Unity's managed DLLs, without opening the Editor.
#
#   tools/verify-unity-compile.sh
#
# Why this exists: Unity holds an exclusive lock on the project, so `Unity -batchmode -runTests`
# is unavailable whenever the Editor is open — which is most of the time. This gives a fast
# "does it still compile?" answer for the Unity-dependent assemblies. tools/run-core-tests.sh
# already covers Game.Core, which needs no engine references at all.
#
# It compiles all our source into ONE assembly using the reference paths Unity wrote into its
# generated .csproj files. That means it verifies every API call and type, but NOT asmdef
# boundaries — a script reaching across an assembly reference it does not declare will pass here
# and fail in Unity. Treat a green run as "no type errors", not as "Unity is happy".
#
# The generated .csproj files must exist. Unity writes them on open/recompile.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$REPO/tools/.unity-verify"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

DOTNET="$(ls -d /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet 2>/dev/null | sort -V | tail -n 1 || true)"
if [ -z "$DOTNET" ]; then
  echo "error: could not find Unity's bundled .NET SDK." >&2
  exit 1
fi

shopt -s nullglob
PROJECTS=("$REPO"/Game.*.csproj)
if [ ${#PROJECTS[@]} -eq 0 ]; then
  echo "error: no Unity-generated Game.*.csproj found at the repo root." >&2
  echo "       open the project in Unity once so it regenerates them." >&2
  exit 1
fi

mkdir -p "$OUT"

# Two reference sources, because Unity uses two mechanisms:
#   1. <HintPath> for the engine's own managed DLLs.
#   2. <ProjectReference> for package and first-party assemblies, whose built output lands in
#      Library/ScriptAssemblies. Those are where TMPro, uGUI and Netcode live.
#
# Our own Game.*.dll are excluded: they are a stale build of the very source being compiled here,
# and referencing them makes every one of our types ambiguous.
{
  grep -ho '<HintPath>[^<]*</HintPath>' "${PROJECTS[@]}" \
    | sed -e 's|<HintPath>||' -e 's|</HintPath>||'

  find "$REPO/Library/ScriptAssemblies" -maxdepth 1 -name '*.dll' 2>/dev/null \
    | grep -v '/Game\.[^/]*\.dll$'

  # 3. Plain managed DLLs shipped inside packages (Newtonsoft is the one we use). Skip the AOT
  #    variants, which are the same types again and would collide.
  find "$REPO/Library/PackageCache" -name '*.dll' -path '*/Runtime/*' 2>/dev/null \
    | grep -v '/AOT/'
} | sort -u | awk -F/ '!seen[$NF]++' > "$OUT/refs.txt"

REF_COUNT=$(wc -l < "$OUT/refs.txt" | tr -d ' ')

{
  echo '<Project>'
  echo '  <ItemGroup>'
  while IFS= read -r dll; do
    [ -f "$dll" ] || continue
    name="$(basename "$dll" .dll)"
    printf '    <Reference Include="%s"><HintPath>%s</HintPath><Private>false</Private></Reference>\n' "$name" "$dll"
  done < "$OUT/refs.txt"
  echo '  </ItemGroup>'
  echo '</Project>'
} > "$OUT/refs.props"

# Unity's own define set, so anything behind UNITY_EDITOR or a version gate compiles the same way.
DEFINES="$(grep -o '<DefineConstants>[^<]*' "$REPO/Game.EditorTools.csproj" 2>/dev/null | head -1 | sed 's|<DefineConstants>||')"
[ -n "$DEFINES" ] || DEFINES="UNITY_EDITOR;UNITY_2021_1_OR_NEWER"

cat > "$OUT/Verify.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>UnityVerify</AssemblyName>
    <DefineConstants>$DEFINES</DefineConstants>
    <!-- CS0436: our types also exist in the referenced Unity-built DLLs of our own assemblies. -->
    <NoWarn>\$(NoWarn);CS0436;CS0618;CS0649;CS0169;CS0414;NU1701</NoWarn>
    <RestorePackages>false</RestorePackages>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$REPO/Assets/_Project/Code/**/*.cs" Exclude="$REPO/Assets/_Project/Code/Tests/**/*.cs" />
  </ItemGroup>
  <Import Project="refs.props" />
</Project>
EOF

echo "Verifying against $REF_COUNT Unity assemblies…"
echo

set +e
"$DOTNET" build "$OUT/Verify.csproj" -v quiet --nologo 2>&1 \
  | grep -vE "^\s*$|Determining projects|Restored |^Build succeeded|warning MSB3277" \
  | sed "s|$REPO/||g"
STATUS=${PIPESTATUS[0]}
set -e

echo
if [ "$STATUS" -eq 0 ]; then
  echo "OK — no type errors. (asmdef boundaries are NOT checked; Unity still has the last word.)"
else
  echo "FAILED — see errors above."
fi
exit "$STATUS"
