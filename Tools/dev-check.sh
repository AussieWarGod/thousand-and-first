#!/usr/bin/env bash
# Focused development checks. Never a substitute for CI or release acceptance.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"
usage() {
	printf '%s\n' 'Usage: Tools/dev-check.sh docs | tools BASENAME_PATTERN | main FILTER | portable FILTER | audit' >&2
	exit 2
}
[ "$#" -ge 1 ] || usage
mode="$1"; shift
SECONDS=0
trap 'rc=$?; printf "DEVELOPMENT_CHECK mode=%s exit=%s elapsed_seconds=%s (not release acceptance)\n" "$mode" "$rc" "$SECONDS"' EXIT
case "$mode" in
	docs)
		[ "$#" -eq 0 ] || usage
		git diff --check
		python3 Tools/check-doc-freshness.py
		python3 -m unittest discover -s Tools/tests -p release_readiness_test.py
		;;
	tools)
		[ "$#" -eq 1 ] && [[ "$1" == *_test.py && "$1" != */* ]] || usage
		compgen -G "Tools/tests/$1" > /dev/null || { echo 'No matching tool tests' >&2; exit 2; }
		python3 -m unittest discover -s Tools/tests -p "$1"
		;;
	main|portable)
		[ "$#" -eq 1 ] && [[ -n "${1//[[:space:]]/}" ]] || usage
		dotnet_bin="${TAF_DOTNET:-dotnet}"
		[ "$("$dotnet_bin" --version)" = 9.0.306 ] || { echo 'Use .NET 9.0.306; set TAF_DOTNET to its executable.' >&2; exit 2; }
		project=DevTests/TafTests.csproj
		[ "$mode" != portable ] || project=DevTests/PortableTests.csproj
		export TAF_REPO_ROOT="$repo" TAF_TEST_FILTER="$1" TAF_FORBID_SKIPS=1
		unset TAF_ALLOWED_SKIPS
		"$dotnet_bin" restore "$project" --locked-mode -v q --nologo
		"$dotnet_bin" run --project "$project" --no-restore -v q --nologo
		;;
	audit)
		[ "$#" -eq 0 ] || usage
		Tools/portable-check.sh
		;;
	*) usage ;;
esac
