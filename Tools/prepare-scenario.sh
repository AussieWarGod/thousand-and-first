#!/usr/bin/env bash
# Materialise an isolated Qud profile that carries the staged TAF runtime PLUS the excluded
# developer scenario harness.
#
# This is a sibling of Tools/prepare-smoke.sh, never a widening of it. The release smoke profile
# asserts exactly one mod directory whose bytes match the stage seal, and Tools/release-check.sh
# depends on that assertion; a harness-bearing profile therefore gets its own root pattern, its own
# seal, and its own launcher. Nothing here changes what ships: Harness/ is excluded from
# Tools/stage.sh, so it never reaches the live mod folder or the Workshop package, and the shipped
# manifest.json never lists it.
#
# SEAL ORDER. The COMPLETE dev profile is built first - staged runtime, harness overlay, dev
# manifest, generated request, options - and only then sealed, ONCE, as one exact closed inventory.
# Sealing the runtime before deriving the dev manifest would seal the shipped manifest and then
# replace it, leaving a profile that could never verify.
#
# SEED AUTHORITY. Caves of Qud 2.0.211.51 exposes no launcher-reachable pre-generation seed
# injection: EmbarkInfo.GameSeed is set during character creation, the only entry point is a popup
# gated on Options.EnableSeed, and EmbarkBuilderModule.InitFromSeed has no caller in the shipped
# game. This script therefore freezes a seed, writes it into a generated request that exists only
# inside the throwaway profile, and exposes the native seed field so THE OPERATOR can enter it.
# This is manual operator entry, never automatic injection; the new-game gate proves what the
# engine actually generated under.
#
#   Tools/prepare-scenario.sh [ROOT] [SEED]

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE_TOOL="$REPO/Tools/scenario_profile.py"
ROOT="${1:-}"
SEED="${2:-}"
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
[ -d "$REPO/Harness" ] || { echo "no harness tree to overlay: $REPO/Harness" >&2; exit 2; }

# Containment proof, restated here so a profile can never be prepared from a tree whose shipped
# inventory or manifest has started carrying the harness.
if "$REPO/Tools/stage.sh" list | grep -q '^Harness/'; then
	echo "refusing: Harness/ is present in the staged runtime inventory" >&2
	exit 2
fi
if grep -q 'Harness' "$REPO/manifest.json"; then
	echo "refusing: shipped manifest.json mentions the harness directory" >&2
	exit 2
fi

# Exact seed syntax and engine Int32 range. A shell glob would admit non-digits after the first.
if [ -z "$SEED" ]; then
	SEED="#$(( ( RANDOM << 15 | RANDOM ) % 2000000000 + 1 ))"
fi
SEED="$(python3 "$PROFILE_TOOL" seed "$SEED")"

if [ -z "$ROOT" ]; then
	ROOT="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
else
	if [[ ! "$ROOT" =~ ^/mnt/c/taf-scenario\.[A-Za-z0-9]+$ ]]; then
		echo "refusing scenario root outside an exact /mnt/c/taf-scenario.<id> path: $ROOT" >&2
		exit 2
	fi
	if [ -L "$ROOT" ] || { [ -e "$ROOT" ] && [ "$(readlink -f -- "$ROOT")" != "$ROOT" ]; }; then
		echo "refusing linked scenario root: $ROOT" >&2
		exit 2
	fi
	if [ -e "$ROOT" ] && find "$ROOT" -mindepth 1 -print -quit | grep -q .; then
		echo "refusing non-empty scenario root: $ROOT" >&2
		exit 2
	fi
	mkdir -p "$ROOT"
fi

SEAL_DIR="$ROOT.seal"
if [ -e "$SEAL_DIR" ] || [ -L "$SEAL_DIR" ]; then
	echo "refusing existing scenario seal directory: $SEAL_DIR" >&2
	exit 2
fi
mkdir "$SEAL_DIR"

LOCAL="$ROOT/Local"
MOD="$LOCAL/Mods/ThousandAndFirst"
mkdir -p "$ROOT/Save" "$LOCAL/Mods" "$ROOT/Synced/Saves"

# ---- build the COMPLETE profile before sealing anything -------------------------------------

# 1. The ordinary staged runtime, byte-verified against the same stage the release path uses.
"$REPO/Tools/stage.sh" copy "$MOD"
"$REPO/Tools/stage.sh" verify "$MOD"

# 2. The excluded harness, overlaid into the throwaway profile only.
mkdir -p "$MOD/Harness"
find "$REPO/Harness" -mindepth 1 -maxdepth 1 -type f \( -name '*.cs' -o -name '*.xml' \) \
	-exec cp -- {} "$MOD/Harness/" \;

# 3. The frozen request, written into the profile's own embark module.
REQUEST="arch-gallery-slice;facing=north;seed=$SEED"
TAF_REQUEST="$REQUEST" python3 "$PROFILE_TOOL" request "$MOD/Harness/EmbarkModules.xml"

# 4. The dev manifest that selects the harness. The repository manifest is never edited, so the
#    shipped selection stays provably harness-free and check-manifest-directories.py is unaffected.
python3 "$PROFILE_TOOL" manifest "$REPO/manifest.json" "$MOD/manifest.json"

# 5. Options. Diagnostics on, plus the native world-seed field exposed for OPERATOR entry.
python3 "$PROFILE_TOOL" options "$REPO/Tools/smoke/PlayerOptions.json" "$LOCAL/PlayerOptions.json"
cp "$REPO/Tools/smoke/ModSettings.json" "$LOCAL/ModSettings.json"

# ---- the profile is complete: seal it once, closed ------------------------------------------

# One exact inventory over every launcher input: the runtime, the harness overlay, the generated
# manifest, the generated request (inside the overlay), and both option files. Save/ and
# Synced/Saves are outside it because play mutates them.
python3 "$PROFILE_TOOL" seal "$LOCAL" "$SEAL_DIR/profile.sha256"
python3 "$PROFILE_TOOL" verify "$LOCAL" "$SEAL_DIR/profile.sha256"
printf '%s\n' "$REQUEST" > "$SEAL_DIR/request.txt"

echo "SCENARIO PROFILE READY: $ROOT"
echo "Frozen seed:  $SEED"
echo "Request:      $REQUEST"
echo "Profile seal: $SEAL_DIR/profile.sha256"
echo "Launch: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$(wslpath -w "$REPO/Tools/run-scenario.ps1")' -Root '$(wslpath -w "$ROOT")' -Game '$(wslpath -w "$GAME")'"
echo
echo "In game: start a new game in the [Dev] TAF scenario mode and ENTER world seed $SEED"
echo "yourself at character creation - Qud has no launcher-side seed injection - then run"
echo "kingdom:scenario realize. The gate refuses to stamp anything generated under another seed."
