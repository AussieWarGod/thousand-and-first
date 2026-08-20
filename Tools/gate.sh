#!/usr/bin/env bash
# Compile gate over the EXACT set the game will compile.
#
# DevTests/build.ps1 compiles every .cs in the repo except DevTests/. The game
# compiles every .cs beneath the deployed mod root, which is a different set.
# This gate compiles Tools/stage.sh's runtime set, so gate and deployment ask
# the same question.
#
#   Tools/gate.sh            compile the staged runtime set
#   Tools/gate.sh --keep     leave the staged tree in place and print its path

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DISTRO="${WSL_DISTRO_NAME:-Ubuntu}"
STAGE="$(mktemp -d /tmp/taf-stage.XXXXXX)"
[ "${1:-}" = "--keep" ] || trap 'rm -rf "$STAGE"' EXIT

"$REPO/Tools/stage.sh" copy "$STAGE"

# WSL path -> UNC path csc.exe can open.
unc() { printf '\\\\wsl.localhost\\%s%s\n' "$DISTRO" "$(printf '%s' "$1" | tr '/' '\\')"; }

RSP="$STAGE/gate.rsp"
{
	printf '@"%s"\n' "$(unc "$REPO/DevTests/refs.rsp")"
	printf -- '-out:"%s"\n' "$(unc "$STAGE/r_ThousandAndFirst.dll")"
	find "$STAGE" -name '*.cs' | LC_ALL=C sort | while IFS= read -r f; do
		printf '"%s"\n' "$(unc "$f")"
	done
} > "$RSP"

COUNT="$(find "$STAGE" -name '*.cs' | wc -l)"
echo "staged sources: $COUNT"

cd /mnt/c
OUT="$(powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
	"dotnet exec 'C:\\Program Files\\dotnet\\sdk\\9.0.306\\Roslyn\\bincore\\csc.dll' '@$(unc "$RSP")'; exit \$LASTEXITCODE" 2>&1)" && RC=0 || RC=$?

printf '%s\n' "$OUT" | grep -v 'warning CS2023' || true
[ "${1:-}" = "--keep" ] && echo "staged tree: $STAGE"

if [ "$RC" -eq 0 ]; then
	echo "STAGED COMPILE CLEAN ($COUNT sources)"
else
	echo "STAGED COMPILE FAILED ($RC)"
fi
exit "$RC"
