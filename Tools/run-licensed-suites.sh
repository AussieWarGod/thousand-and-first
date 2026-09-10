#!/usr/bin/env bash
# Runs DevTests/test.ps1 for a given worktree via the pinned WSL/powershell.exe recipe, and
# reports the licensed suites' real exit code as THIS SCRIPT'S OWN process exit code.
#
# Issue #134 root cause: ad hoc wrappers built during builder sessions ended with
# `... ; echo "LICENSED_SUITES_EXIT=$?"` (or `; echo "rc=$?"; grep ... LOG`) as their LAST
# statement. That correctly printed the real exit code to stdout -- `dotnet run` and
# test.ps1 both already propagate a launch/test failure as a non-zero exit -- but a trailing
# echo/grep always itself exits 0, so the WRAPPER SCRIPT's own process exit status (the only
# thing an automated caller checking `$?` after it actually observes) was 0 regardless of
# what the licensed suites did. `LICENSED_SUITES_EXIT=<n>` in the log was correct the whole
# time; nothing downstream of it was ever consulting the number it printed.
#
# Usage: Tools/run-licensed-suites.sh <worktree-root> [TAF_QUD_BASE_WIN]
#   <worktree-root>    absolute Linux path to the worktree containing DevTests/test.ps1
#   TAF_QUD_BASE_WIN   optional; defaults to the pinned licensed-suite base path
#
# Exits with the licensed suites' own exit code. The last stdout line is always
# `LICENSED_SUITES_EXIT=<n>` so a caller reading only the log tail sees the same number.
set -u

WT="${1:?usage: run-licensed-suites.sh <worktree-root> [TAF_QUD_BASE_WIN]}"
QUD_BASE_WIN="${2:-F:\\SteamLibrary\\steamapps\\common\\Caves of Qud\\CoQ_Data\\StreamingAssets\\Base}"
SUFFIX="$(printf '%s' "$WT/DevTests/test.ps1" | sed 's#/#\\#g')"
TEST_SCRIPT_WIN='\\wsl.localhost\Ubuntu'"$SUFFIX"

TAF_QUD_BASE_WIN="$QUD_BASE_WIN" TAF_TEST_SCRIPT_WIN="$TEST_SCRIPT_WIN" \
    WSLENV=TAF_QUD_BASE_WIN/w:TAF_TEST_SCRIPT_WIN/w \
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \
    '$env:TAF_QUD_BASE=$env:TAF_QUD_BASE_WIN; & $env:TAF_TEST_SCRIPT_WIN; exit $LASTEXITCODE'
rc=$?

# The capture above and this echo/exit are the ENTIRE remainder of the script on purpose:
# nothing may run between capturing $rc and the final `exit "$rc"`, or this wrapper
# reproduces the exact defect it exists to fix.
echo "LICENSED_SUITES_EXIT=$rc"
exit "$rc"
