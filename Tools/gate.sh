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
# Both trees are independently allocated and owned by this run, and ONE trap removes exactly what
# this run created. A derived sibling path such as "$STAGE.dev" would be a name we never allocated,
# so recursively removing it would be removing somebody else's directory on a bad day.
STAGE="$(mktemp -d /tmp/taf-stage.XXXXXX)"
DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"
cleanup() { rm -rf "$STAGE" "$DEV"; }
[ "${1:-}" = "--keep" ] || trap cleanup EXIT

"$REPO/Tools/stage.sh" copy "$STAGE"
python3 "$REPO/Tools/generate-removal-coverage.py" --check
python3 "$REPO/Tools/check-manifest-directories.py"
HEARTHPYRE_SOURCE="${TAF_HEARTHPYRE_223_ROOT:-/mnt/f/SteamLibrary/steamapps/workshop/content/333640/1683847053}"
if [ -d "$HEARTHPYRE_SOURCE" ]; then
	python3 "$REPO/Tools/check-hearthpyre-abi.py" --source "$HEARTHPYRE_SOURCE"
else
	python3 "$REPO/Tools/check-hearthpyre-abi.py" --fixture-only
fi

# WSL path -> UNC path csc.exe can open.
unc() { printf '\\\\wsl.localhost\\%s%s\n' "$DISTRO" "$(printf '%s' "$1" | tr '/' '\\')"; }

COUNT="$(find "$STAGE" -name '*.cs' | wc -l)"
echo "staged sources: $COUNT"

compile_mode() {
	local mode="$1" rendered="$STAGE/refs-$1.rsp" rsp="$STAGE/gate-$1.rsp"
	local source_list="$STAGE/sources-$1.list" mode_count stub_rsp stub_dll
	python3 "$REPO/Tools/render-qud-refs.py" \
		--template "$REPO/DevTests/refs.rsp" \
		--managed "$MANAGED" \
		--managed-windows "$MANAGED_WIN" \
		--mode "$mode" \
		--output "$rendered"
	# One shared inventory primitive for ordinary and dev alike: the mode's exclusions live in
	# Tools/dev-harness-inventory.py, so baseline can never drop the optional-mod bridge here and
	# keep it there.
	python3 "$REPO/Tools/dev-harness-inventory.py" --sources \
		--stage "$STAGE" --mode "$mode" --out "$source_list"
	if [ "$mode" != baseline ]; then
		stub_dll="$STAGE/Hearthpyre-2.2.3-abi.dll"
		stub_rsp="$STAGE/hearthpyre-2.2.3-abi.rsp"
		{
			printf '@"%s"\n' "$(unc "$rendered")"
			printf -- '-target:library\n'
			printf -- '-out:"%s"\n' "$(unc "$stub_dll")"
			printf '"%s"\n' "$(unc "$REPO/DevTests/Compatibility/Hearthpyre223AbiStub.cs")"
		} > "$stub_rsp"
		local stub_output stub_rc
		set +e
		stub_output="$(cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass \
			-Command "dotnet exec 'C:\\Program Files\\dotnet\\sdk\\9.0.306\\Roslyn\\bincore\\csc.dll' '@$(unc "$stub_rsp")'; exit \$LASTEXITCODE" 2>&1)"
		stub_rc=$?
		set -e
		printf '%s\n' "$stub_output" | grep -v 'warning CS2023' || true
		if [ "$stub_rc" -ne 0 ]; then
			echo "HEARTHPYRE 2.2.3 ABI STUB COMPILE FAILED"
			return 1
		fi
	fi
	mode_count="$(wc -l < "$source_list")"
	{
		printf '@"%s"\n' "$(unc "$rendered")"
		printf -- '-out:"%s"\n' "$(unc "$STAGE/r_ThousandAndFirst-$mode.dll")"
		[ "$mode" != compatibility ] || printf -- '-r:"%s"\n' "$(unc "$stub_dll")"
		while IFS= read -r f; do
			printf '"%s"\n' "$(unc "$f")"
		done < "$source_list"
	} > "$rsp"
	local output rc
	set +e
	output="$(cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
		"dotnet exec 'C:\\Program Files\\dotnet\\sdk\\9.0.306\\Roslyn\\bincore\\csc.dll' '@$(unc "$rsp")'; exit \$LASTEXITCODE" 2>&1)"
	rc=$?
	set -e
	printf '%s\n' "$output" | grep -v 'warning CS2023' || true
	if [ "$rc" -eq 0 ]; then
		echo "STAGED $mode COMPILE CLEAN ($mode_count sources)"
	else
		echo "STAGED $mode COMPILE FAILED ($rc)"
	fi
	return "$rc"
}

# Build the dev profile once: the ordinary staged runtime PLUS all and only Harness/*.cs, selected
# by the derived dev manifest. Its inventory comes from the one shared helper, so it can never drift
# from the tree the game would compile, and it is a compile-only artifact: nothing here reaches
# stage, deploy, or the Workshop package.
prepare_dev_harness() {
	"$REPO/Tools/stage.sh" copy "$DEV"
	mkdir -p "$DEV/Harness"
	python3 "$REPO/Tools/dev-harness-inventory.py" --list-harness | while IFS= read -r shard; do
		cp -- "$REPO/Harness/$shard" "$DEV/Harness/$shard"
	done
	python3 "$REPO/Tools/scenario_profile.py" manifest "$REPO/manifest.json" "$DEV/manifest.json"
}

# The engine-touching harness shards meet a compiler ONLY here. Both public test projects are
# deliberately Qud-free, so nothing there can vouch for a namespace, signature, or API on this path.
# Each dev mode compiles ITS ordinary inventory plus the overlay: dev baseline must exclude the
# optional-mod bridge exactly as ordinary baseline does, or it is not baseline plus Harness.
compile_dev_harness() {
	local mode="$1" rendered="$STAGE/refs-$1.rsp" rsp="$STAGE/gate-devharness-$1.rsp"
	local source_list="$STAGE/sources-devharness-$1.list" count output rc
	python3 "$REPO/Tools/dev-harness-inventory.py" --dev-sources \
		--stage "$DEV" --mode "$mode" --out "$source_list"
	count="$(wc -l < "$source_list")"
	{
		printf '@"%s"\n' "$(unc "$rendered")"
		printf -- '-out:"%s"\n' "$(unc "$STAGE/r_ThousandAndFirst-devharness-$mode.dll")"
		[ "$mode" != compatibility ] || printf -- '-r:"%s"\n' "$(unc "$STAGE/Hearthpyre-2.2.3-abi.dll")"
		while IFS= read -r f; do
			printf '"%s"\n' "$(unc "$f")"
		done < "$source_list"
	} > "$rsp"
	set +e
	output="$(cd /mnt/c && powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
		"dotnet exec 'C:\\Program Files\\dotnet\\sdk\\9.0.306\\Roslyn\\bincore\\csc.dll' '@$(unc "$rsp")'; exit \$LASTEXITCODE" 2>&1)"
	rc=$?
	set -e
	printf '%s\n' "$output" | grep -v 'warning CS2023' || true
	if [ "$rc" -eq 0 ]; then
		echo "DEV-HARNESS PROFILE $mode COMPILE CLEAN ($count sources)"
	else
		echo "DEV-HARNESS PROFILE $mode COMPILE FAILED ($rc)"
	fi
	return "$rc"
}

failed=0
compile_mode baseline || failed=1
compile_mode compatibility || failed=1
prepare_dev_harness
compile_dev_harness baseline || failed=1
compile_dev_harness compatibility || failed=1
if [ "${1:-}" = "--keep" ]; then
	echo "staged tree: $STAGE"
	echo "dev profile: $DEV"
fi
if [ "$failed" -eq 0 ]; then
	echo "STAGED COMPILE CLEAN ($COUNT sources; baseline + compatibility symbols; dev profile both)"
else
	echo "STAGED COMPILE FAILED"
fi
exit "$failed"
