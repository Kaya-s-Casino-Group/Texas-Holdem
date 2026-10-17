#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
compiler="${POKER_CSC:-mcs}"
runtime="${POKER_MONO:-mono}"
nunit="${POKER_NUNIT:-Library/PackageCache/com.unity.ext.nunit@2.0.3/net40/unity-custom/nunit.framework.dll}"
if [[ ! -f "$nunit" ]]; then
  echo 'Open the project in Unity to restore test dependencies, or set POKER_NUNIT.' >&2
  exit 1
fi
test_dir="$(mktemp -d "${TMPDIR:-/tmp}/poker-engine-tests.XXXXXX")"
trap 'rm -rf "$test_dir"' EXIT
modules=(Core Abilities Combat Scoring Holdem Draw Runs Progression Encounters Composition)
all_refs=()
for module in "${modules[@]}"; do
  dependencies=""
  case "$module" in
    Abilities|Scoring) dependencies="Core" ;;
    Progression|Encounters) dependencies="Core" ;;
    Combat|Holdem) dependencies="Core Abilities" ;;
    Draw|Runs) dependencies="Core Abilities Scoring" ;;
    Composition) dependencies="Core Abilities Combat Scoring Holdem Draw Runs Progression Encounters" ;;
  esac
  references=(-r:System.Runtime.Serialization)
  for dependency in $dependencies; do references+=("-r:$test_dir/PokerEngine.$dependency.dll"); done
  sources=()
  while IFS= read -r -d '' file; do sources+=("$file"); done < <(find "Assets/Scripts/Rules/PokerEngine/Runtime/$module" -name '*.cs' -print0)
  "$compiler" -langversion:7.2 -warnaserror -target:library "${references[@]}" \
    -out:"$test_dir/PokerEngine.$module.dll" "${sources[@]}"
  all_refs+=("-r:$test_dir/PokerEngine.$module.dll")
done
cp "$nunit" "$test_dir/nunit.framework.dll"
tests=()
while IFS= read -r -d '' file; do tests+=("$file"); done < <(find Assets/Scripts/Rules/PokerEngine/Tests/Editor -name '*Tests.cs' ! -name 'PackagingTests.cs' -print0)
"$compiler" -langversion:7.2 -warnaserror -r:System.Runtime.Serialization "${all_refs[@]}" -r:"$test_dir/nunit.framework.dll" \
  -out:"$test_dir/PokerEngine.Tests.exe" "${tests[@]}" Tools/PokerEngineTestRunner.cs
"$runtime" "$test_dir/PokerEngine.Tests.exe" "$@"
