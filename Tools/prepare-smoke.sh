#!/usr/bin/env bash
# Materialise an isolated Qud profile containing only the staged TAF runtime.
# With no argument, creates a fresh C:\taf-smoke.* directory and prints it.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="${1:-}"
QUD_ROOT_DEFAULT="/mnt/f/SteamLibrary/steamapps/common/Caves of Qud"
if [ -n "${TAF_QUD_ROOT:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_ROOT" && pwd)"
elif [ -n "${TAF_QUD_BASE:-}" ]; then
	QUD_ROOT="$(cd "$TAF_QUD_BASE/../../.." && pwd)"
else
	QUD_ROOT="$QUD_ROOT_DEFAULT"
fi
GAME="$QUD_ROOT/CoQ.exe"
[ -f "$GAME" ] || { echo "configured Caves of Qud executable not found: $GAME" >&2; exit 2; }

if [ -z "$ROOT" ]; then
	ROOT="$(mktemp -d /mnt/c/taf-smoke.XXXXXX)"
else
	if [[ ! "$ROOT" =~ ^/mnt/c/taf-smoke\.[A-Za-z0-9]+$ ]]; then
		echo "refusing smoke root outside an exact /mnt/c/taf-smoke.<id> path: $ROOT" >&2
		exit 2
	fi
	if [ -L "$ROOT" ] || { [ -e "$ROOT" ] && [ "$(readlink -f -- "$ROOT")" != "$ROOT" ]; }; then
		echo "refusing linked smoke root: $ROOT" >&2
		exit 2
	fi
	if [ -e "$ROOT" ]; then
		WIN_ROOT="$(wslpath -w "$ROOT")"
		set +e
		powershell.exe -NoProfile -Command \
			"\$item = Get-Item -LiteralPath '$WIN_ROOT' -Force; if ((\$item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { exit 10 }; exit 0"
		REPARSE_STATUS=$?
		set -e
		if [ "$REPARSE_STATUS" -eq 10 ]; then
			echo "refusing reparse-point smoke root: $ROOT" >&2
			exit 2
		elif [ "$REPARSE_STATUS" -ne 0 ]; then
			echo "could not inspect smoke root reparse state: $ROOT" >&2
			exit 2
		fi
	fi
	if [ -e "$ROOT" ] && find "$ROOT" -mindepth 1 -print -quit | grep -q .; then
		echo "refusing non-empty smoke root: $ROOT" >&2
		exit 2
	fi
	mkdir -p "$ROOT"
fi

SEAL_DIR="$ROOT.seal"
SEAL="$SEAL_DIR/stage.sha256"
if [ -e "$SEAL_DIR" ] || [ -L "$SEAL_DIR" ]; then
	echo "refusing existing smoke stage seal directory: $SEAL_DIR" >&2
	exit 2
fi
mkdir "$SEAL_DIR"

mkdir -p "$ROOT/Save" "$ROOT/Local/Mods" "$ROOT/Synced/Saves"
"$REPO/Tools/stage.sh" copy "$ROOT/Local/Mods/ThousandAndFirst"
"$REPO/Tools/stage.sh" verify "$ROOT/Local/Mods/ThousandAndFirst"
"$REPO/Tools/stage.sh" manifest > "$SEAL"

# Only smoke-critical choices are pinned. Qud supplies defaults for everything else.
# Diagnostics are deliberately on in this isolated profile even though release default is off.
cp "$REPO/Tools/smoke/PlayerOptions.json" "$ROOT/Local/PlayerOptions.json"
cp "$REPO/Tools/smoke/ModSettings.json" "$ROOT/Local/ModSettings.json"

WIN_ROOT="$(wslpath -w "$ROOT")"
WIN_GAME="$(wslpath -w "$GAME")"
echo "SMOKE PROFILE READY: $ROOT"
echo "Trusted stage seal: $SEAL"
echo "Launch: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$(wslpath -w "$REPO/Tools/run-smoke.ps1")' -Root '$WIN_ROOT' -Game '$WIN_GAME'"
