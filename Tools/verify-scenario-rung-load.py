#!/usr/bin/env python3
"""Read-only verdict for the synthetic D5 native release-intent save/load witness.

Journal assertions are not independent engine execution, historical-save compatibility,
ordinary acceptance or process ownership. Shared log checking writes only a temporary derivative.
"""
from __future__ import annotations

from datetime import datetime
import importlib.util
from pathlib import Path
import re
import subprocess
import sys

TOOLS = Path(__file__).resolve().parent
PERSONA = TOOLS / "personas/subsidence-rung-save-native-check.persona"
_SPEC = importlib.util.spec_from_file_location("scenario_load_shared", TOOLS / "verify-scenario-load.py")
shared = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(shared)
require = shared.require
read_bounded = shared.read_bounded
PREACTIVATION = ("exact ss5/sr2 bytes and 35 loaded bodies; release=Intent; native-write-cut=2; wear-fields=10; autoonce=true"
                 "; before-AfterGameLoaded-handlers-and-zone-activation=true")
RECOVERY = ("release=Released; population=35; original-step-retired=true; replay=false; status=clear"
            "; activation-already-recovered=")
COMPLETE = shared.COMPLETE


def verify_journal(text: str) -> dict[str, str]:
    """Require the separate four-row rung witness; never admit the older 49-body contract."""
    normalized = text.replace("\r\n", "\n")
    lines = normalized.split("\n")
    require("\r" not in normalized and len(lines) == 5 and lines[-1] == "",
            "rung journal must contain exactly four LF/CRLF-terminated rows")
    rows = shared.persona_matrix.read_journal(normalized)
    require([row[0] for row in rows] == ["LOAD-BEGIN", "LOAD-PREACTIVATION", "LOAD-RECOVERY", "SCRIPT-COMPLETE"],
            "rung journal rows are missing, repeated, reordered, or foreign")
    require(all(row[1] == "OK" for row in rows), "every native rung load row must be OK")
    for line in lines[:-1]:
        timestamp = line.split("\t", 1)[0]
        require(shared.TIMESTAMP.fullmatch(timestamp), "journal timestamp is not canonical UTC milliseconds")
        datetime.strptime(timestamp, "%Y-%m-%dT%H:%M:%S.%fZ")
    begin = re.fullmatch("exact sealed save; game-id=(" + shared.GAME_ID
                         + "); new-game=false; mod-restore=false", rows[0][2])
    require(begin is not None, "load intent lacks exact game-id/no-new-game/no-mod-restore proof")
    require(rows[1][2] == PREACTIVATION, "saved ss5/sr2, cut-two, ten-field, 35-body preactivation proof differs")
    require(rows[2][2] in (RECOVERY + "true", RECOVERY + "false"), "Released/retirement/no-replay proof differs")
    route = "native-zone-activation" if rows[2][2] == RECOVERY + "true" else "explicit-production-prepass"
    require(rows[3][2] == COMPLETE.format(route=route), "completion count, retention, or recovery route contradicts witness")
    return {"game_id": begin.group(1), "recovery": route}


def verify_log(raw: bytes) -> None:
    manifest, name = shared.persona_matrix.load(str(PERSONA))
    original, original_name = shared.persona_matrix.load(str(shared.PERSONA))
    require(shared.persona_matrix.parse_log_expect(manifest.get("LOG_EXPECT", ""), name)
            == shared.persona_matrix.parse_log_expect(original.get("LOG_EXPECT", ""), original_name),
            "rung source persona must retain the exact shared Pets warning pair")
    shared.verify_log(raw)


def verify_profile(root: Path) -> dict[str, str]:
    result = verify_journal(read_bounded(root / "scenario-journal.tsv", 1048576).decode("utf-8"))
    verify_log(read_bounded(root / "Player.log", 64 * 1024 * 1024))
    return result


def main(argv: list[str]) -> int:
    try:
        require(len(argv) == 2 and shared.ROOT.fullmatch(argv[1]),
                "usage: verify-scenario-rung-load.py /mnt/c/taf-scenario.<alnum>")
        result = verify_profile(Path(argv[1]))
    except (ValueError, OSError, SystemExit, subprocess.SubprocessError) as error:
        print("NATIVE RUNG LOAD VERDICT REFUSED: " + str(error), file=sys.stderr)
        return 2
    print("NATIVE RUNG LOAD VERDICT PASS: cases=2; game-id=" + result["game_id"] + "; recovery=" + result["recovery"]
          + "; checkpoint=synthetic; historical-save-compatibility=untested; ordinary-acceptance=false; process-authority=unproved")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
