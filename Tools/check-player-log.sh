#!/usr/bin/env bash
# Fail a controlled game smoke when The Thousand and First emitted a loader,
# compiler, catalogue, or runtime diagnostic.  Pass the Player.log produced by
# that smoke; the script never guesses which profile/log is current.

set -euo pipefail

LOG="${1:-}"
[ -n "$LOG" ] || { echo "usage: $0 PLAYER_LOG" >&2; exit 2; }
[ -f "$LOG" ] || { echo "player log not found: $LOG" >&2; exit 2; }
[ -s "$LOG" ] || { echo "player log is empty: $LOG" >&2; exit 2; }

TMP="$(mktemp)"
BAD="${TMP}.bad"
trap 'rm -f -- "$TMP" "$BAD"' EXIT

# Qud's log is CRLF on Windows.  Keep line numbers stable while normalising it.
tr -d '\r' < "$LOG" > "$TMP"

if ! grep -Eq '(\[TAF\]|\[The Thousand and First\]|[/\\]ThousandAndFirst[/\\]|\[The Thousand and First( \[[A-Z]+\])?\]|workshop[/\\]content[/\\]333640[/\\][0-9]+[/\\])' "$TMP"; then
	echo "SMOKE LOG INVALID: no Thousand and First load/runtime evidence" >&2
	exit 1
fi

# A compiler warning is release-relevant: Qud reports Roslyn diagnostics through
# MODWARN, including obsolete APIs that can disappear in the next game build.
# Stack frames are also fatal even when Unity printed the exception header before
# the first line naming our namespace.
# TAF_LOG_ALLOW: one extended regex for lines a controlled run EXPECTS in the log — the scenario
# gate's own refusal and its own stack frame, for a persona that asserts that refusal. Empty
# means nothing is allowed.
awk -v allow="${TAF_LOG_ALLOW:-}" '
	{
		if (allow != "" && $0 ~ allow) next
		lower = tolower($0)
		if ($0 ~ /MOD(ERROR|WARN) \[The Thousand and First\]/ ||
			(lower ~ /(\[taf\]|thousandandfirst|the thousand and first)/ &&
			 lower ~ /(exception|error|fault|quarantin|inspection required)/) ||
			(lower ~ /^[[:space:]]*(at|---).*thousandandfirst[.:]/)) {
			bad[NR] = $0
			count++
		}
	}
	END {
		for (line in bad) print line ":" bad[line]
		if (count) exit 1
	}
' "$TMP" > "$BAD" || {
	echo "SMOKE LOG FAILED: Thousand and First diagnostics found" >&2
	sort -n -t: -k1,1 "$BAD" >&2
	exit 1
}

echo "SMOKE LOG CLEAN: no Thousand and First warnings, errors, or exception frames"
