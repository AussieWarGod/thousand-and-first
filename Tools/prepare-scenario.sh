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
# manifest, generated request, options, auto-runner script - and only then sealed, ONCE, as one
# exact closed inventory. Sealing the runtime before deriving the dev manifest would seal the
# shipped manifest and then replace it, leaving a profile that could never verify. The script obeys
# the same rule for the same reason: an unattended run must execute sealed content, never a file
# that appeared after the inventory was taken.
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
#
#    TAF_REQUEST names the request WITHOUT its seed - `arch-gallery-slice;facing=south`, say - so a
#    persona can select another declared parameter value without editing this file or the roster.
#    The seed stays this script's to freeze: it is the one field the launcher owns and the gate
#    independently proves, and letting a caller name it would let two runs claim one world.
#    A caller that tries anyway is refused rather than having its seed quietly replaced.
DEFAULT_REQUEST="arch-gallery-slice;facing=north"
REQUEST_BASE="${TAF_REQUEST:-$DEFAULT_REQUEST}"
case "$REQUEST_BASE" in
	*";seed="*)
		echo "refusing TAF_REQUEST that names its own seed: $REQUEST_BASE" >&2
		echo "the seed is frozen per profile by this script; pass it as argument 2 instead" >&2
		exit 2
		;;
esac
REQUEST="$REQUEST_BASE;seed=$SEED"
TAF_REQUEST="$REQUEST" python3 "$PROFILE_TOOL" request "$MOD/Harness/EmbarkModules.xml"

# 3b. The dev start parasang, rewritten inside the profile copy only.
#
#     TAF_SCENARIO_START=<wx>.<wy>[@x,y] - for example 14.18 or 14.18@40,12 - moves the harness's
#     [Dev] TAF test ground before the profile is sealed, so a persona can be run against another
#     terrain without editing the tree. Bounds are refused hard rather than clamped: an off-map
#     parasang does not make the engine refuse, it makes zone generation crash on biome arrays that
#     are exactly 80x25. Unset asks for and echoes the shipped default.
START_LINE="$(python3 "$PROFILE_TOOL" start "$MOD/Harness/EmbarkModules.xml" "${TAF_SCENARIO_START:-}")"
printf '%s\n' "$START_LINE"
START_ZONE="${START_LINE#start zone: }"
START_ZONE="${START_ZONE%% (*}"

# 4. The dev manifest that selects the harness. The repository manifest is never edited, so the
#    shipped selection stays provably harness-free and check-manifest-directories.py is unaffected.
python3 "$PROFILE_TOOL" manifest "$REPO/manifest.json" "$MOD/manifest.json"

# 5. Options. Diagnostics on, plus the native world-seed field exposed for OPERATOR entry.
python3 "$PROFILE_TOOL" options "$REPO/Tools/smoke/PlayerOptions.json" "$LOCAL/PlayerOptions.json"
cp "$REPO/Tools/smoke/ModSettings.json" "$LOCAL/ModSettings.json"

# 6. The script the in-game auto-runner executes on the first player turn. Written HERE, inside
#    Local/, so it is covered by the single closed seal below: the script an unattended run
#    executes is then exactly the script this preparation sealed, and a file dropped in afterwards
#    fails the launcher's two-direction inventory. Unquoted on purpose - the verbs are separate
#    arguments, and scenario_profile.py refuses any word outside its closed set.
#
#    TAF_SCENARIO_SCRIPT=none prepares an ATTENDED profile: no script file, so the auto-runner
#    stays inert and the operator drives kingdom:scenario by hand exactly as before.
#
#    One verb takes an argument: `advance <turns>` runs that many game turns with no player input,
#    for behaviour that only happens on a clock. Write it as two words - for example
#    TAF_SCENARIO_SCRIPT="flatten realize advance 1200 status" - and scenario_profile.py folds them
#    into one sealed line and refuses a count outside 1..10000.
#
#    TAF_SCENARIO_EXTRA_VERBS="myverb,other" widens the sealable set for THIS profile only, so a
#    verb another mod contributes through IKingdomScenarioVerbProvider can be sealed into a script.
#    The base set stays closed: a third-party verb is admitted for the profile that will run it,
#    never globally, and a name the harness reserves is refused here rather than in game.
SCENARIO_SCRIPT="${TAF_SCENARIO_SCRIPT:-flatten realize status}"
if [ "$SCENARIO_SCRIPT" = none ]; then
	echo "no scenario script sealed; this profile is attended-only"
else
	python3 "$PROFILE_TOOL" script "$LOCAL/scenario-script.txt" $SCENARIO_SCRIPT
fi

# ---- the profile is complete: seal it once, closed ------------------------------------------

# One exact inventory over every launcher input: the runtime, the harness overlay, the generated
# manifest, the generated request (inside the overlay), and both option files. Save/ and
# Synced/Saves are outside it because play mutates them.
python3 "$PROFILE_TOOL" seal "$LOCAL" "$SEAL_DIR/profile.sha256"
python3 "$PROFILE_TOOL" verify "$LOCAL" "$SEAL_DIR/profile.sha256"
printf '%s\n' "$REQUEST" > "$SEAL_DIR/request.txt"

echo "SCENARIO PROFILE READY: $ROOT"
echo "Frozen seed:  $SEED"
echo "Start zone:   $START_ZONE"
echo "Request:      $REQUEST"
echo "Profile seal: $SEAL_DIR/profile.sha256"
echo "Launch: powershell.exe -NoProfile -ExecutionPolicy Bypass -File '$(wslpath -w "$REPO/Tools/run-scenario.ps1")' -Root '$(wslpath -w "$ROOT")' -Game '$(wslpath -w "$GAME")'"
echo
echo "In game: start a new game in the [Dev] TAF scenario mode and ENTER world seed $SEED"
echo "yourself at character creation - Qud has no launcher-side seed injection. The gate refuses"
echo "to stamp anything generated under another seed."
if [ "$SCENARIO_SCRIPT" = none ]; then
	echo "No script is sealed, so drive the harness by hand: kingdom:scenario realize."
else
	echo "The sealed script then runs itself on your first turn in the world; read the results in"
	echo "$ROOT/scenario-journal.tsv. Every kingdom:scenario verb journals there, scripted or typed."
fi
