#!/usr/bin/env bash
# Materialise an isolated Qud profile containing only the staged TAF runtime.
# With no argument, creates a fresh C:\taf-smoke.* directory and prints it.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="${1:-}"

if [ -z "$ROOT" ]; then
	ROOT="$(mktemp -d /mnt/c/taf-smoke.XXXXXX)"
else
	case "$ROOT" in
		/mnt/c/taf-smoke.*) ;;
		*) echo "refusing smoke root outside /mnt/c/taf-smoke.*: $ROOT" >&2; exit 2 ;;
	esac
	if [ -e "$ROOT" ] && find "$ROOT" -mindepth 1 -print -quit | grep -q .; then
		echo "refusing non-empty smoke root: $ROOT" >&2
		exit 2
	fi
	mkdir -p "$ROOT"
fi

mkdir -p "$ROOT/Save" "$ROOT/Local/Mods" "$ROOT/Synced/Saves"
"$REPO/Tools/stage.sh" copy "$ROOT/Local/Mods/ThousandAndFirst"
"$REPO/Tools/stage.sh" verify "$ROOT/Local/Mods/ThousandAndFirst"

# Only smoke-critical choices are pinned. Qud supplies defaults for everything else.
# Diagnostics are deliberately on in this isolated profile even though release default is off.
cp "$REPO/Tools/smoke/PlayerOptions.json" "$ROOT/Local/PlayerOptions.json"
cp "$REPO/Tools/smoke/ModSettings.json" "$ROOT/Local/ModSettings.json"

WIN_ROOT="$(wslpath -w "$ROOT")"
echo "SMOKE PROFILE READY: $ROOT"
echo "Launch: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$(wslpath -w "$REPO/Tools/run-smoke.ps1")' -Root '$WIN_ROOT'"
