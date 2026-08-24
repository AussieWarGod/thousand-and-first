#!/usr/bin/env bash
# One-command release-candidate verification.  This mutates neither the live mod nor git.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASE_DEFAULT="/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
BASE="${TAF_QUD_BASE:-$BASE_DEFAULT}"

cd "$REPO"

echo "[1/8] patch hygiene"
git diff --check

echo "[2/8] shipped IPart save ABI"
./Tools/check-ipart-abi.sh

echo "[3/8] cold-install inventory"
./Tools/stage.sh verify

echo "[4/8] exact staged compile"
./Tools/gate.sh

echo "[5/8] pure and source-contract tests"
TEST_SCRIPT="$(wslpath -w "$REPO/DevTests/test.ps1")"
(
	cd /mnt/c
	powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$TEST_SCRIPT"
)

echo "[6/8] XML and tile reachability"
python3 Art/check_xml_refs.py --base "$BASE"
python3 Art/check_wiring.py

echo "[7/8] deterministic balance model"
python3 _notes/balance-sim.py

echo "[8/8] deployment dry run"
./Tools/stage.sh deploy

echo "RELEASE CHECK CLEAN"
echo "After the isolated in-game run: Tools/check-player-log.sh PLAYER_LOG"
