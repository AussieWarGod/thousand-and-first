#!/usr/bin/env bash
# Reproduction for issue #134's actual root cause: not test.ps1 or powershell.exe (both
# already propagate a licensed-suite failure as a non-zero exit -- see
# Tools/tests/test_ps1_exit_propagation_test.py), but the ad hoc bash wrapper builders used
# to invoke it from WSL, e.g. (the exact historical shape, camp-native-licensed-3.log):
#
#   powershell.exe ... -Command '...; exit $LASTEXITCODE' > LOG 2>&1; echo "rc=$?"
#   grep -n 'ALL GREEN\|FAIL\|does not exist' LOG
#
# This script stands in for that wrapper: a stub inner command (`stub_inner`) exits 154 in
# place of powershell.exe and writes a log containing a real "does not exist" line (as the
# portable leg's actual launch failure does), so the trailing grep genuinely MATCHES and
# exits 0 on its own -- no `|| true` needed, matching the historical command exactly. The
# gap between the inner command's real exit code (154, correctly printed via `rc=`) and the
# wrapper's own final exit status (0, from the matching grep) is the trap.
set -u

stub_inner() {
	echo "ALL GREEN: 1 cases passed, 0 skipped (1 discovered)"
	echo "The application to execute does not exist: stub.dll"
	return 154
}

LOG="$(mktemp)"
trap 'rm -f "$LOG"' EXIT

echo "=== reproducing the historical wrapper tail (camp-native-licensed-3.log shape) ==="
stub_inner > "$LOG" 2>&1
inner_rc=$?
echo "rc=$inner_rc"
grep -n 'ALL GREEN\|FAIL\|does not exist' "$LOG"
grep_rc=$?

echo "INNER_REAL_EXIT=$inner_rc"
echo "WRAPPER_OWN_EXIT_IF_ENDED_HERE=$grep_rc"
echo "This is the trap: the inner command's real exit code ($inner_rc, printed above via"
echo "rc=) never reaches the wrapper's own exit status, because the matching grep"
echo "(exit $grep_rc) ran last."
exit "$grep_rc"
