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
#   Tools/run-personas.sh --set smoke    run the personas tagged SET=...smoke...
#   Tools/run-personas.sh --personas a,b same as naming a b positionally
#   Tools/run-personas.sh --list         name the personas, sets and expectations
#   Tools/run-personas.sh --check        parse every manifest and stop; launches nothing
#
#   TAF_PERSONA_REPORT=<path>            default Tools/PortableOutput/personas-report.tsv
#   TAF_PERSONA_TIMEOUT=<seconds>        overrides every persona's own TIMEOUT
#   TAF_PERSONA_CAPTURE_DIR=<path>        publish one native PNG only after an asserted PASS;
#                                         failed assertion/capture keeps the prior good PNG
#   TAF_PERSONA_CAPTURE_WIDTH/HEIGHT      native capture window size (default 2560x1440); a
#                                         taller window shows a taller lot at the same tile size
#   TAF_PERSONA_SEED=<#int>               optional exact seed reused by every selected persona
#   TAF_QUD_ROOT=<path>                  another licensed install
#
# Exit status is nonzero when any persona is not PASS.

set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PERSONA_DIR="$REPO/Tools/personas"
MATRIX="$PERSONA_DIR/persona_matrix.py"
PREPARE="$REPO/Tools/prepare-scenario.sh"
LAUNCHER="$REPO/Tools/run-scenario.ps1"
CAPTURE="$REPO/Tools/capture-game-window.ps1"
LOG_CHECK="$REPO/Tools/check-player-log.sh"
REPORT="${TAF_PERSONA_REPORT:-$REPO/Tools/PortableOutput/personas-report.tsv}"
REPORT_DIR="$(dirname "$REPORT")"
CAPTURE_DIR="${TAF_PERSONA_CAPTURE_DIR:-}"
mkdir -p "$REPORT_DIR"
[ -z "$CAPTURE_DIR" ] || mkdir -p "$CAPTURE_DIR"
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

# The persona selection: named arguments (or --personas a,b,c), a tagged --set slice, or every
# manifest on disk in ordinal order. Names and sets compose: both filters must admit a persona.
MODE=run
PERSONAS=()
SETS=""
expect_value=""
for argument in "$@"; do
	if [ -n "$expect_value" ]; then
		case "$expect_value" in
			set) SETS="$SETS,$argument" ;;
			personas) PERSONAS+=(${argument//,/ }) ;;
		esac
		expect_value=""
		continue
	fi
	case "$argument" in
		--list) MODE=list ;;
		--check) MODE=check ;;
		--set) expect_value=set ;;
		--personas) expect_value=personas ;;
		-*) die "unknown option: $argument" ;;
		*) PERSONAS+=("$argument") ;;
	esac
done
[ -z "$expect_value" ] || die "--$expect_value needs a value"
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
	P_SET=""; P_GATE=0
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
			set) P_SET="$value" ;;
		esac
	done <<< "$fields"
	[ -n "$P_REQUEST" ] || die "persona $1 declares no request"
	[ -n "$P_SCRIPT" ] || die "persona $1 declares no script"
	# A persona whose terminal is the new-game gate's refusal EXPECTS the gate to throw: the
	# engine logs that throw as a boot ERROR, and the Player.log check must not read the very
	# refusal the persona asserts as a mod defect. Only that one line is allowed, only here.
	P_GATE=0
	if grep -qE '^EXPECT=GATE-REFUSED' "$path"; then P_GATE=1; fi
}

# The --set slice: keep only personas whose SET tags intersect the requested tags. Runs after
# load_persona exists and before list/check so every mode sees the same selection.
if [ -n "$SETS" ]; then
	SELECTED=()
	for persona in "${PERSONAS[@]}"; do
		load_persona "$persona"
		for tag in ${SETS//,/ }; do
			[ -n "$tag" ] || continue
			case ",$P_SET," in
				*",$tag,"*) SELECTED+=("$persona"); break ;;
			esac
		done
	done
	[ "${#SELECTED[@]}" -gt 0 ] || die "no personas carry set tag(s):${SETS}"
	PERSONAS=("${SELECTED[@]}")
fi

if [ "$MODE" = list ] || [ "$MODE" = check ]; then
	for persona in "${PERSONAS[@]}"; do
		load_persona "$persona"
		printf '%s\n  request %s\n  script  %s\n  sets    %s\n  expect  %s\n' "$persona" \
			"$P_REQUEST" "$P_SCRIPT" "${P_SET:-<untagged>}" \
			"$(grep -m1 '^EXPECT=' "$(persona_path "$persona")" | cut -d= -f2-)"
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

# Copy a live output through a same-directory temporary name. A retry gets its own target name, so
# its evidence cannot erase the first attempt that caused it. The caller decides whether absence is
# lawful; a present file that cannot be archived is always a runner failure.
archive_file() {
	local source="$1" target="$2" temp="${2}.tmp.$$"
	rm -f -- "$temp"
	if ! cp -- "$source" "$temp" || ! mv -f -- "$temp" "$target"; then
		rm -f -- "$temp"
		return 1
	fi
}

# ---- one persona ------------------------------------------------------------------------------

# Sets VERDICT and DETAIL. Never exits: one persona's fault must not end the matrix.
run_persona() {
	local persona="$1" attempt="${2:-1}" root journal archived_journal player_log
	local archived_player_log log_problem
	local timeout waited terminal problems warnings capture_problem archive_problem artifact
	local capture_temp capture_target prepare_log launch_log capture_log
	local -a prepare_args
	VERDICT=FAIL
	DETAIL=""
	load_persona "$persona"
	timeout="${TAF_PERSONA_TIMEOUT:-${P_TIMEOUT:-300}}"
	artifact="$persona"
	[ "$attempt" -le 1 ] || artifact="$persona-retry$attempt"
	prepare_log="$REPORT_DIR/prepare-$artifact.log"
	launch_log="$REPORT_DIR/launch-$artifact.log"
	capture_log="$REPORT_DIR/capture-$artifact.log"

	stop_game
	wipe_profiles
	root="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)" || { DETAIL="no scenario root"; return; }
	journal="$root/scenario-journal.tsv"
	player_log="$root/Player.log"
	prepare_args=("$root")
	[ -z "${TAF_PERSONA_SEED:-}" ] || prepare_args+=("$TAF_PERSONA_SEED")

	if ! TAF_REQUEST="$P_REQUEST" \
		TAF_SCENARIO_SCRIPT="$P_SCRIPT" \
		TAF_SCENARIO_START="$P_START" \
		TAF_SCENARIO_EXTRA_VERBS="$P_VERBS" \
		TAF_QUD_ROOT="$QUD_ROOT" \
		"$PREPARE" "${prepare_args[@]}" > "$prepare_log" 2>&1
	then
		DETAIL="prepare refused: $(tail -n 3 "$prepare_log" | tr '\n\t' '  ')"
		return
	fi

	if ! powershell.exe -NoProfile -ExecutionPolicy Bypass \
		-File "$(wslpath -w "$LAUNCHER")" \
		-Root "$(wslpath -w "$root")" \
		-Game "$(wslpath -w "$GAME")" > "$launch_log" 2>&1
	then
		DETAIL="launch refused: $(tail -n 3 "$launch_log" | tr '\n\t' '  ')"
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

	# Freeze every available diagnostic while Qud is still alive. Assertion reads the archived
	# terminal snapshot, not a mutable file that the next profile wipe will destroy. Player.log is
	# especially important when the runner never armed and produced no journal.
	archive_problem=""
	archived_journal="$REPORT_DIR/journal-$artifact.tsv"
	archived_player_log="$REPORT_DIR/player-$artifact.log"
	if [ -f "$journal" ] && ! archive_file "$journal" "$archived_journal"; then
		archive_problem="could not archive live journal: $archived_journal"
	fi
	if [ ! -f "$player_log" ]; then
		[ -z "$archive_problem" ] || archive_problem="$archive_problem; "
		archive_problem="${archive_problem}live Player.log is absent"
	elif ! archive_file "$player_log" "$archived_player_log"; then
		[ -z "$archive_problem" ] || archive_problem="$archive_problem; "
		archive_problem="${archive_problem}could not archive live Player.log"
	fi
	if [ -n "$archive_problem" ]; then
		DETAIL="$archive_problem"
		stop_game
		return
	fi
	if [ ! -x "$LOG_CHECK" ]; then
		DETAIL="TAF Player.log checker is unavailable: $LOG_CHECK"
		stop_game
		return
	fi
	local log_allow=""
	[ "$P_GATE" != 1 ] || log_allow="scenario harness refused to open|KingdomScenarioNewGameGate[.]mutate"
	if ! log_problem="$(TAF_LOG_ALLOW="$log_allow" "$LOG_CHECK" "$archived_player_log" 2>&1)"; then
		DETAIL="Player.log rejected: $(printf '%s\n' "$log_problem" | tail -n 8 \
			| tr '\n\t' '  ')"
		stop_game
		return
	fi
	if [ ! -f "$journal" ]; then
		VERDICT=TIMEOUT
		DETAIL="no journal after ${timeout}s; the runner never armed"
		stop_game
		return
	fi
	if [ -z "$terminal" ]; then
		VERDICT=TIMEOUT
		DETAIL="no terminal row after ${timeout}s; last row: $(tail -n 1 "$archived_journal" \
			| cut -c1-160 | tr '\t' ' ')"
		stop_game
		return
	fi
	if ! terminal="$(python3 "$MATRIX" terminal "$archived_journal" 2>/dev/null)" || \
		[ -z "$terminal" ]
	then
		DETAIL="archived journal has no readable terminal row: $archived_journal"
		stop_game
		return
	fi
	warnings="$(python3 "$MATRIX" warnings "$archived_journal" 2>/dev/null)"
	if problems="$(python3 "$MATRIX" assert "$(persona_path "$persona")" \
		"$archived_journal" 2>&1)"
	then
		VERDICT=PASS
		DETAIL="$terminal; $(grep -c . "$archived_journal") journal row(s)"
	else
		DETAIL="$problems"
	fi
	[ -z "$warnings" ] || DETAIL="$DETAIL; verb providers refused: $warnings"

	# A PNG is evidence for this exact asserted run, not merely for a process that reached a terminal
	# row. Keep the prior published image until both the assertion and new capture have succeeded.
	capture_problem=""
	if [ "$VERDICT" = PASS ] && [ -n "$CAPTURE_DIR" ]; then
		capture_target="$CAPTURE_DIR/$persona.png"
		capture_temp="$CAPTURE_DIR/.$artifact.$$.png"
		rm -f -- "$capture_temp"
		if [ ! -f "$CAPTURE" ]; then
			capture_problem="capture helper is missing: $CAPTURE"
		elif ! powershell.exe -NoProfile -ExecutionPolicy Bypass \
			-File "$(wslpath -w "$CAPTURE")" \
			-Output "$(wslpath -w "$capture_temp")" \
			-Width "${TAF_PERSONA_CAPTURE_WIDTH:-2560}" \
			-Height "${TAF_PERSONA_CAPTURE_HEIGHT:-1440}" \
			> "$capture_log" 2>&1
		then
			capture_problem="capture refused: $(tail -n 3 "$capture_log" | tr '\n\t' '  ')"
			rm -f -- "$capture_temp"
		elif [ "$(od -An -tx1 -N8 "$capture_temp" 2>/dev/null | tr -d ' \n')" != \
			"89504e470d0a1a0a" ]
		then
			capture_problem="capture helper returned a non-PNG file"
			rm -f -- "$capture_temp"
		elif ! mv -f -- "$capture_temp" "$capture_target"; then
			capture_problem="capture succeeded but atomic publication failed: $capture_target"
			rm -f -- "$capture_temp"
		fi
	fi
	if [ -n "$capture_problem" ]; then
		[ -z "$DETAIL" ] || DETAIL="$DETAIL; "
		DETAIL="$DETAIL$capture_problem"
		VERDICT=FAIL
	fi
	stop_game
}

# ---- the matrix -------------------------------------------------------------------------------

mkdir -p "$(dirname "$REPORT")"
printf 'persona\tverdict\tdetail\n' > "$REPORT"
failed=0
for persona in "${PERSONAS[@]}"; do
	echo "=== $persona"
	run_persona "$persona" 1
	# One retry absorbs launch-level flakes (a Unity boot race killed one clean persona in
	# eight live launches); a persona that fails twice is a real red, and the retry is named.
	if [ "$VERDICT" = TIMEOUT ]; then
		run_persona "$persona" 2
		[ "$VERDICT" != PASS ] || DETAIL="$DETAIL (passed on retry after a launch flake)"
	fi
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
