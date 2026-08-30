#!/usr/bin/env bash
# The persona matrix: every Tools/personas/*.persona run end to end, one game at a time.
#
# WHAT ONE PERSONA IS. A declared unattended run - the request to freeze, the verbs to seal, and
# what the journal must say afterwards. This script owns the game; Tools/personas/persona_matrix.py
# owns the grammar and the verdict, so the assertion engine is executable without a licensed
# install and is covered by Tools/tests/persona_matrix_test.py.
#
# SERIAL, ALWAYS. Qud is a single Unity process reading one profile, and the launcher refuses to
# start onto an existing journal for exactly the reason two runs' rows in one file cannot be told
# apart. Personas therefore run one at a time, each in a fresh sealed profile.
#
# IDEMPOTENT. Every persona starts by killing any running game and wiping every
# /mnt/c/taf-scenario.* root and seal, so a previous aborted run cannot lend this one a profile, a
# journal, or a stamped save.
#
#   Tools/run-personas.sh                run every persona
#   Tools/run-personas.sh arch-tent-north realize-replay-poisoned
#   Tools/run-personas.sh --list         name the personas and their expectations
#   Tools/run-personas.sh --check        parse every manifest and stop; launches nothing
#
#   TAF_PERSONA_REPORT=<path>            default Tools/PortableOutput/personas-report.tsv
#   TAF_PERSONA_TIMEOUT=<seconds>        overrides every persona's own TIMEOUT
#   TAF_QUD_ROOT=<path>                  another licensed install
#
# Exit status is nonzero when any persona is not PASS.

set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PERSONA_DIR="$REPO/Tools/personas"
MATRIX="$PERSONA_DIR/persona_matrix.py"
PREPARE="$REPO/Tools/prepare-scenario.sh"
LAUNCHER="$REPO/Tools/run-scenario.ps1"
REPORT="${TAF_PERSONA_REPORT:-$REPO/Tools/PortableOutput/personas-report.tsv}"
POLL_SECONDS=5

QUD_ROOT_DEFAULT="/mnt/f/SteamLibrary/steamapps/common/Caves of Qud"
if [ -n "${TAF_QUD_ROOT:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_ROOT" && pwd)"
elif [ -n "${TAF_QUD_BASE:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_BASE/../../.." && pwd)"
else
	QUD_ROOT="$QUD_ROOT_DEFAULT"
fi
GAME="$QUD_ROOT/CoQ.exe"

die() { echo "run-personas: $*" >&2; exit 2; }

[ -d "$PERSONA_DIR" ] || die "no persona directory: $PERSONA_DIR"
[ -f "$MATRIX" ] || die "no persona matrix engine: $MATRIX"

# The persona set: named arguments, or every manifest on disk in ordinal order.
MODE=run
PERSONAS=()
for argument in "$@"; do
	case "$argument" in
		--list) MODE=list ;;
		--check) MODE=check ;;
		-*) die "unknown option: $argument" ;;
		*) PERSONAS+=("$argument") ;;
	esac
done
if [ "${#PERSONAS[@]}" -eq 0 ]; then
	while IFS= read -r path; do
		PERSONAS+=("$(basename "$path" .persona)")
	done < <(find "$PERSONA_DIR" -maxdepth 1 -name '*.persona' -type f | sort)
fi
[ "${#PERSONAS[@]}" -gt 0 ] || die "no personas to run"

# ---- persona fields -------------------------------------------------------------------------

persona_path() { printf '%s/%s.persona\n' "$PERSONA_DIR" "$1"; }

# Loads one manifest into the P_* variables. persona_matrix.py validates every field, so a
# malformed persona is refused HERE - before a profile is prepared - rather than in a sealed run
# nobody can retry.
load_persona() {
	local path fields key value
	path="$(persona_path "$1")"
	[ -f "$path" ] || die "no such persona: $1 ($path)"
	P_REQUEST=""; P_SCRIPT=""; P_START=""; P_CHECK=""; P_TIMEOUT=""; P_VERBS=""; P_DESC=""
	fields="$(python3 "$MATRIX" fields "$path")" || die "persona $1 is malformed"
	while IFS=$'\t' read -r key value; do
		case "$key" in
			request) P_REQUEST="$value" ;;
			script_words) P_SCRIPT="$value" ;;
			start) P_START="$value" ;;
			check) P_CHECK="$value" ;;
			timeout) P_TIMEOUT="$value" ;;
			verbs) P_VERBS="$value" ;;
			description) P_DESC="$value" ;;
		esac
	done <<< "$fields"
	[ -n "$P_REQUEST" ] || die "persona $1 declares no request"
	[ -n "$P_SCRIPT" ] || die "persona $1 declares no script"
}

if [ "$MODE" = list ] || [ "$MODE" = check ]; then
	for persona in "${PERSONAS[@]}"; do
		load_persona "$persona"
		printf '%s\n  request %s\n  script  %s\n  expect  %s\n' "$persona" "$P_REQUEST" \
			"$P_SCRIPT" "$(grep -m1 '^EXPECT=' "$(persona_path "$persona")" | cut -d= -f2-)"
	done
	echo "${#PERSONAS[@]} persona(s) parsed clean"
	exit 0
fi

[ -f "$GAME" ] || die "configured Caves of Qud executable not found: $GAME"
[ -f "$LAUNCHER" ] || die "no scenario launcher: $LAUNCHER"

# ---- the game, and the ground it runs on ------------------------------------------------------

# Windows-side stop. The launcher starts CoQ.exe detached and never waits on it, so the matrix owns
# ending it: a game left running would hold the previous profile's save open and its journal would
# keep growing under the next persona's assertion.
stop_game() {
	powershell.exe -NoProfile -Command \
		"Get-Process -Name CoQ -ErrorAction SilentlyContinue | Stop-Process -Force" \
		> /dev/null 2>&1 || true
	sleep 2
}

# Every scenario root AND its sibling seal directory. Both are matched by exact pattern rather than
# by a glob over /mnt/c: this removes only paths prepare-scenario.sh itself is allowed to allocate.
wipe_profiles() {
	local path
	for path in /mnt/c/taf-scenario.*; do
		[ -e "$path" ] || continue
		case "$path" in
			/mnt/c/taf-scenario.*) rm -rf -- "$path" ;;
		esac
	done
}

# ---- one persona ------------------------------------------------------------------------------

# Sets VERDICT and DETAIL. Never exits: one persona's fault must not end the matrix.
run_persona() {
	local persona="$1" root journal timeout waited terminal problems warnings
	VERDICT=FAIL
	DETAIL=""
	load_persona "$persona"
	timeout="${TAF_PERSONA_TIMEOUT:-${P_TIMEOUT:-300}}"

	stop_game
	wipe_profiles
	root="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)" || { DETAIL="no scenario root"; return; }
	journal="$root/scenario-journal.tsv"

	if ! TAF_REQUEST="$P_REQUEST" \
		TAF_SCENARIO_SCRIPT="$P_SCRIPT" \
		TAF_SCENARIO_START="$P_START" \
		TAF_SCENARIO_EXTRA_VERBS="$P_VERBS" \
		TAF_QUD_ROOT="$QUD_ROOT" \
		"$PREPARE" "$root" > "$root.prepare.log" 2>&1
	then
		DETAIL="prepare refused: $(tail -n 3 "$root.prepare.log" | tr '\n\t' '  ')"
		return
	fi

	if ! powershell.exe -NoProfile -ExecutionPolicy Bypass \
		-File "$(wslpath -w "$LAUNCHER")" \
		-Root "$(wslpath -w "$root")" \
		-Game "$(wslpath -w "$GAME")" > "$root.launch.log" 2>&1
	then
		DETAIL="launch refused: $(tail -n 3 "$root.launch.log" | tr '\n\t' '  ')"
		stop_game
		return
	fi

	# Wait for a terminal row. SCRIPT-COMPLETE, SCRIPT-STOPPED and GATE-REFUSED are the only three
	# ways an unattended run ends; anything else is this persona timing out, which is its own
	# verdict rather than a silent pass.
	waited=0
	terminal=""
	while [ "$waited" -lt "$timeout" ]; do
		if [ -f "$journal" ]; then
			terminal="$(python3 "$MATRIX" terminal "$journal" 2>/dev/null)"
			[ -n "$terminal" ] && break
		fi
		sleep "$POLL_SECONDS"
		waited=$((waited + POLL_SECONDS))
	done
	stop_game

	if [ ! -f "$journal" ]; then
		VERDICT=TIMEOUT
		DETAIL="no journal after ${timeout}s; the runner never armed"
		return
	fi
	if [ -z "$terminal" ]; then
		VERDICT=TIMEOUT
		DETAIL="no terminal row after ${timeout}s; last row: $(tail -n 1 "$journal" \
			| cut -c1-160 | tr '\t' ' ')"
		return
	fi

	warnings="$(python3 "$MATRIX" warnings "$journal" 2>/dev/null)"
	if problems="$(python3 "$MATRIX" assert "$(persona_path "$persona")" "$journal" 2>&1)"; then
		VERDICT=PASS
		DETAIL="$terminal; $(grep -c . "$journal") journal row(s)"
	else
		DETAIL="$problems"
	fi
	[ -z "$warnings" ] || DETAIL="$DETAIL; verb providers refused: $warnings"
}

# ---- the matrix -------------------------------------------------------------------------------

mkdir -p "$(dirname "$REPORT")"
printf 'persona\tverdict\tdetail\n' > "$REPORT"
failed=0
for persona in "${PERSONAS[@]}"; do
	echo "=== $persona"
	run_persona "$persona"
	printf '%s\t%s\t%s\n' "$persona" "$VERDICT" "$(printf '%s' "$DETAIL" | tr '\n\t' '  ')" \
		>> "$REPORT"
	echo "    $VERDICT  $DETAIL"
	[ "$VERDICT" = PASS ] || failed=1
done
stop_game
echo
echo "persona matrix report: $REPORT"
if [ "$failed" -eq 0 ]; then
	echo "PERSONA MATRIX GREEN (${#PERSONAS[@]} persona(s))"
else
	echo "PERSONA MATRIX RED"
fi
exit "$failed"
