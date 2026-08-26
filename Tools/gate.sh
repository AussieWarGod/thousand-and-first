#!/usr/bin/env bash
# Compile gate over the EXACT set the game will compile.
#
# This gate compiles Tools/stage.sh's runtime set, so the game, deployment, this
# script, and DevTests/build.ps1 all ask the same question.
#
#   Tools/gate.sh            compile the staged runtime set
#   Tools/gate.sh --keep     leave the staged tree in place and print its path
#   TAF_QUD_ROOT=/path       use another licensed install
#
# Both a clean baseline symbol set and the tracked optional-mod compatibility set compile. Managed
# reference names are tracked; absolute paths are rendered for the selected local installation.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DISTRO="${WSL_DISTRO_NAME:-Ubuntu}"
QUD_ROOT_DEFAULT="/mnt/f/SteamLibrary/steamapps/common/Caves of Qud"
if [ -n "${TAF_QUD_ROOT:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_ROOT" && pwd)"
elif [ -n "${TAF_QUD_BASE:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_BASE/../../.." && pwd)"
else
	QUD_ROOT="$QUD_ROOT_DEFAULT"
fi
MANAGED="$QUD_ROOT/CoQ_Data/Managed"
[ -f "$MANAGED/Assembly-CSharp.dll" ] || {
	echo "configured Qud root is incomplete: $MANAGED/Assembly-CSharp.dll" >&2
	exit 2
}
MANAGED_WIN="$(wslpath -w "$MANAGED")"
STAGE="$(mktemp -d /tmp/taf-stage.XXXXXX)"
[ "${1:-}" = "--keep" ] || trap 'rm -rf "$STAGE"' EXIT

"$REPO/Tools/stage.sh" copy "$STAGE"

# WSL path -> UNC path csc.exe can open.
unc() { printf '\\\\wsl.localhost\\%s%s\n' "$DISTRO" "$(printf '%s' "$1" | tr '/' '\\')"; }

COUNT="$(find "$STAGE" -name '*.cs' | wc -l)"
echo "staged sources: $COUNT"

compile_mode() {
	local mode="$1" rendered="$STAGE/refs-$1.rsp" rsp="$STAGE/gate-$1.rsp"
	python3 "$REPO/Tools/render-qud-refs.py" \
		--template "$REPO/DevTests/refs.rsp" \
		--managed "$MANAGED" \
		--managed-windows "$MANAGED_WIN" \
		--mode "$mode" \
		--output "$rendered"
	{
		printf '@"%s"\n' "$(unc "$rendered")"
		printf -- '-out:"%s"\n' "$(unc "$STAGE/r_ThousandAndFirst-$mode.dll")"
		find "$STAGE" -name '*.cs' | LC_ALL=C sort | while IFS= read -r f; do
			printf '"%s"\n' "$(unc "$f")"
		done
	} > "$rsp"
	local output rc
	set +e
	output="$(cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
		"dotnet exec 'C:\\Program Files\\dotnet\\sdk\\9.0.306\\Roslyn\\bincore\\csc.dll' '@$(unc "$rsp")'; exit \$LASTEXITCODE" 2>&1)"
	rc=$?
	set -e
	printf '%s\n' "$output" | grep -v 'warning CS2023' || true
	if [ "$rc" -eq 0 ]; then
		echo "STAGED $mode COMPILE CLEAN ($COUNT sources)"
	else
		echo "STAGED $mode COMPILE FAILED ($rc)"
	fi
	return "$rc"
}

failed=0
compile_mode baseline || failed=1
compile_mode compatibility || failed=1
[ "${1:-}" = "--keep" ] && echo "staged tree: $STAGE"
if [ "$failed" -eq 0 ]; then
	echo "STAGED COMPILE CLEAN ($COUNT sources; baseline + compatibility symbols)"
else
	echo "STAGED COMPILE FAILED"
fi
exit "$failed"
