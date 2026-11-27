#!/usr/bin/env bash
set -euo pipefail
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
project=.github/scripts/CoverageChecker/CoverageChecker.csproj
dotnet build "$project" -c Release --warnaserror
checker=.github/scripts/CoverageChecker/bin/Release/net10.0/CoverageChecker.dll
run_case() {
  local name=$1 expected=$2 attrs=$3 mode=${4:-}
  local status=0
  printf '<coverage %s><sources><source>.</source></sources><packages><package><classes><class filename="Spellbook/Pages/Home.razor"/></classes></package></packages></coverage>' "$attrs" > "$tmp/$name.xml"
  if [[ -n "$mode" ]]; then
    dotnet "$checker" "$tmp/$name.xml" "$mode" || status=$?
  else
    dotnet "$checker" "$tmp/$name.xml" || status=$?
  fi
  if [[ "$status" -ne "$expected" ]]; then
    printf 'Case %s expected exit %s, got %s\n' "$name" "$expected" "$status" >&2
    exit 1
  fi
  printf 'PASS %s\n' "$name"
}
run_case boundary 0 'lines-valid="100" lines-covered="97" branches-valid="100" branches-covered="90"' --require-branch
run_case above 0 'lines-valid="100" lines-covered="98" branches-valid="100" branches-covered="91"' --require-branch
run_case below-line 1 'lines-valid="100" lines-covered="96" branches-valid="100" branches-covered="95"' --require-branch
run_case below-branch 1 'lines-valid="100" lines-covered="97" branches-valid="100" branches-covered="89"' --require-branch
run_case baseline-below 0 'lines-valid="100" lines-covered="97" branches-valid="100" branches-covered="89"'
run_case missing-branches 1 'lines-valid="100" lines-covered="97"'
run_case zero-branches 1 'lines-valid="100" lines-covered="97" branches-valid="0" branches-covered="0"'
run_case impossible-branches 1 'lines-valid="100" lines-covered="97" branches-valid="100" branches-covered="101"'
run_case malformed-count 1 'lines-valid="100" lines-covered="97" branches-valid="bad" branches-covered="90"'
run_case negative-count 1 'lines-valid="100" lines-covered="97" branches-valid="100" branches-covered="-1"'
run_case zero-lines 1 'lines-valid="0" lines-covered="0" branches-valid="100" branches-covered="90"'
