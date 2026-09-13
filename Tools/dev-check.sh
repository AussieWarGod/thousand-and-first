#!/usr/bin/env bash
# Focused development checks. Never a substitute for CI or release acceptance.
set -euo pipefail
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"
usage() {
	printf '%s\n' 'Usage: Tools/dev-check.sh docs | tools BASENAME_PATTERN | main FILTER | portable FILTER | licensed | audit' >&2
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
	main|portable|licensed)
		if [ "$mode" = licensed ]; then
			[ "$#" -eq 0 ] || usage
			[ -z "${TAF_TEST_FILTER:-}" ] || { echo 'Full licensed checks refuse ambient TAF_TEST_FILTER' >&2; exit 2; }
			unset TAF_TEST_FILTER
			projects=(DevTests/TafTests.csproj DevTests/PortableTests.csproj)
		else
			[ "$#" -eq 1 ] && [[ -n "${1//[[:space:]]/}" ]] || usage
			export TAF_TEST_FILTER="$1"
			projects=(DevTests/TafTests.csproj)
			[ "$mode" != portable ] || projects=(DevTests/PortableTests.csproj)
		fi
		dotnet_bin="${TAF_DOTNET:-dotnet}"
		[ "$("$dotnet_bin" --version)" = 9.0.306 ] || { echo 'Use .NET 9.0.306; set TAF_DOTNET to its executable.' >&2; exit 2; }
		export TAF_REPO_ROOT="$repo" TAF_FORBID_SKIPS=1
		unset TAF_ALLOWED_SKIPS
		for project in "${projects[@]}"; do
			"$dotnet_bin" restore "$project" --locked-mode -v q --nologo
			"$dotnet_bin" run --project "$project" --no-restore -v q --nologo
		done
		;;
	audit)
		[ "$#" -eq 0 ] || usage
		Tools/portable-check.sh
		;;
	*) usage ;;
esac
