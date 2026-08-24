#!/usr/bin/env bash
# One-command release-candidate verification.  This mutates neither the live mod nor git.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
QUD_ROOT_DEFAULT="/mnt/f/SteamLibrary/steamapps/common/Caves of Qud"
if [ -n "${TAF_QUD_ROOT:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_ROOT" && pwd)"
elif [ -n "${TAF_QUD_BASE:-}" ]; then
	# Backward-compatible input; every release consumer still derives from one root.
	QUD_ROOT="$(cd "$TAF_QUD_BASE/../../.." && pwd)"
else
	QUD_ROOT="$QUD_ROOT_DEFAULT"
fi
BASE="$QUD_ROOT/CoQ_Data/StreamingAssets/Base"
GAME_EXE="$QUD_ROOT/CoQ.exe"
ASSEMBLY_CSHARP_PATH="$QUD_ROOT/CoQ_Data/Managed/Assembly-CSharp.dll"
[ -d "$BASE" ] || { echo "configured Qud root is incomplete: $BASE" >&2; exit 2; }
for required_file in "$GAME_EXE" "$ASSEMBLY_CSHARP_PATH"; do
	[ -f "$required_file" ] || {
		echo "configured Qud root is incomplete: $required_file" >&2; exit 2; }
done
GAME_EXE_WIN="$(wslpath -w "$GAME_EXE")"
ASSEMBLY_CSHARP_WIN="$(wslpath -w "$ASSEMBLY_CSHARP_PATH")"
REF_ASSEMBLY_CSHARP="$(sed -n 's/^-r:"\(.*Assembly-CSharp\.dll\)"$/\1/p' "$REPO/DevTests/refs.rsp")"
[ "$REF_ASSEMBLY_CSHARP" = "$ASSEMBLY_CSHARP_WIN" ] || {
	echo "configured Qud root diverges from DevTests/refs.rsp: $ASSEMBLY_CSHARP_WIN" >&2
	exit 2
}

cd "$REPO"

echo "[1/9] patch hygiene"
git diff --check

echo "[2/9] shipped IPart save ABI"
./Tools/check-ipart-abi.sh

echo "[3/9] cold-install inventory"
./Tools/stage.sh verify

echo "[4/9] exact staged compile"
./Tools/gate.sh

echo "[5/9] pure and source-contract tests"
TEST_SCRIPT="$(wslpath -w "$REPO/DevTests/test.ps1")"
(
	cd /mnt/c
	powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$TEST_SCRIPT"
)

echo "[6/9] XML and tile reachability"
python3 Art/check_xml_refs.py --base "$BASE"
python3 Art/check_wiring.py

echo "[7/9] deterministic balance model"
python3 _notes/balance-sim.py

echo "[8/9] executable isolated prepare/launcher harness"
SMOKE_SCRIPT="$(wslpath -w "$REPO/Tools/run-smoke.ps1")"
SMOKE_TEST="$(wslpath -w "$REPO/Tools/test-run-smoke.ps1")"

(
	fixture_id="release$$${RANDOM}"
	profile="/mnt/c/taf-smoke.${fixture_id}"
	seal="$profile.seal"
	nonempty="/mnt/c/taf-smoke.nonempty${fixture_id}"
	junction="/mnt/c/taf-smoke.junction${fixture_id}"
	junction_target="/mnt/c/taf-smoke-prepare-target.${fixture_id}"

	cleanup_prepare_fixtures() {
		original_status=$?
		trap - EXIT
		set +e
		cleanup_failed=0
		if [ -n "${junction:-}" ]; then
			if [ -e "$junction" ] || [ -L "$junction" ]; then
				if [ ! -L "$junction" ]; then
					echo "refusing non-link prepare-fixture cleanup path: $junction" >&2
					cleanup_failed=1
				else
					unlink -- "$junction" || cleanup_failed=1
				fi
			fi
			if [ -e "$junction" ] || [ -L "$junction" ]; then
				echo "prepare fixture cleanup left junction: $junction" >&2
				cleanup_failed=1
			fi
		fi
		for owned in "$profile" "$seal" "$nonempty" "$junction_target"; do
			case "$owned" in
				/mnt/c/taf-smoke.release[0-9]*|/mnt/c/taf-smoke.release[0-9]*.seal|\
				/mnt/c/taf-smoke.nonemptyrelease[0-9]*|/mnt/c/taf-smoke-prepare-target.release[0-9]*) ;;
				*) echo "refusing unexpected prepare-fixture cleanup path: $owned" >&2
					cleanup_failed=1; continue ;;
			esac
			if [ -e "$owned" ] || [ -L "$owned" ]; then
				find -P "$owned" -depth -delete || cleanup_failed=1
			fi
			if [ -e "$owned" ] || [ -L "$owned" ]; then
				echo "prepare fixture cleanup left path: $owned" >&2
				cleanup_failed=1
			fi
		done
		if [ "$cleanup_failed" -ne 0 ]; then
			echo "prepare fixture cleanup failed" >&2
			exit 1
		fi
		exit "$original_status"
	}

	for absent in "$profile" "$seal" "$nonempty" "$junction" "$junction_target"; do
		[ ! -e "$absent" ] && [ ! -L "$absent" ] || {
			echo "prepare fixture path already exists: $absent" >&2; exit 1; }
	done
	# Cleanup owns only paths proven absent before this harness creates them.
	trap cleanup_prepare_fixtures EXIT

	prepare_output="$(./Tools/prepare-smoke.sh "$profile")"
	case "$prepare_output" in
		*"SMOKE PROFILE READY: $profile"*"Trusted stage seal: $seal/stage.sha256"*"-Game '$GAME_EXE_WIN'"*) ;;
		*) echo "prepare-smoke receipt omitted configured profile/seal/game" >&2; exit 1 ;;
	esac
	[ "$(find "$profile" -mindepth 1 -maxdepth 1 -printf '%f\n' | LC_ALL=C sort | paste -sd, -)" = \
		"Local,Save,Synced" ]
	./Tools/stage.sh verify "$profile/Local/Mods/ThousandAndFirst" >/dev/null
	cmp -s Tools/smoke/PlayerOptions.json "$profile/Local/PlayerOptions.json"
	cmp -s Tools/smoke/ModSettings.json "$profile/Local/ModSettings.json"
	[ "$(find "$seal" -mindepth 1 -maxdepth 1 -type f -printf '%f\n')" = "stage.sha256" ]
	cmp -s <(./Tools/stage.sh manifest) "$seal/stage.sha256"
	[ -z "$(find "$profile/Save" "$profile/Synced/Saves" -mindepth 1 -print -quit)" ]
	missing_game="C:\\taf-smoke-prepare-missing.${fixture_id}\\CoQ.exe"
	set +e
	validation="$(powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$SMOKE_SCRIPT" \
		-Root "$(wslpath -w "$profile")" -Game "$missing_game" -ValidateOnly 2>&1 | tr -d '\r')"
	validation_status=$?
	set -e
	[ "$validation_status" -ne 0 ]
	case "$validation" in *"Caves of Qud executable not found: $missing_game"*) ;; *) exit 1 ;; esac

	mkdir "$nonempty"
	printf 'owned fixture\n' > "$nonempty/sentinel"
	set +e
	nonempty_output="$(./Tools/prepare-smoke.sh "$nonempty" 2>&1)"
	nonempty_status=$?
	set -e
	[ "$nonempty_status" -eq 2 ]
	case "$nonempty_output" in *"refusing non-empty smoke root:"*) ;; *) exit 1 ;; esac
	[ "$(<"$nonempty/sentinel")" = "owned fixture" ]

	mkdir "$junction_target"
	powershell.exe -NoProfile -Command \
		"New-Item -ItemType Junction -Path '$(wslpath -w "$junction")' -Target '$(wslpath -w "$junction_target")' | Out-Null"
	set +e
	junction_output="$(./Tools/prepare-smoke.sh "$junction" 2>&1)"
	junction_status=$?
	set -e
	[ "$junction_status" -eq 2 ]
	case "$junction_output" in
		*"refusing linked smoke root:"*|*"refusing reparse-point smoke root:"*) ;;
		*) exit 1 ;;
	esac
	[ -z "$(find "$junction_target" -mindepth 1 -print -quit)" ]
	echo "PREPARE SMOKE HARNESS CLEAN"
)

smoke_args=(
	-Launcher "$SMOKE_SCRIPT"
	-ConfiguredGame "$GAME_EXE_WIN"
	-AssemblyCSharp "$ASSEMBLY_CSHARP_WIN"
)
if [ -n "${TAF_KNOWN_GOOD_SAVE_FIXTURE:-}" ]; then
	[ -d "$TAF_KNOWN_GOOD_SAVE_FIXTURE" ] || {
		echo "known-good save fixture directory not found: $TAF_KNOWN_GOOD_SAVE_FIXTURE" >&2; exit 2; }
	smoke_args+=( -KnownGoodSaveFixture "$(wslpath -w "$TAF_KNOWN_GOOD_SAVE_FIXTURE")" )
fi
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$SMOKE_TEST" "${smoke_args[@]}"

echo "[9/9] deployment dry run"
./Tools/stage.sh deploy

echo "RELEASE CHECK CLEAN"
echo "After the isolated in-game run: Tools/check-player-log.sh PLAYER_LOG"
