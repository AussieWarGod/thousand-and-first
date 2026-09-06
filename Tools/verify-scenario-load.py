#!/usr/bin/env python3
"""Read-only journal/log verdict for one synthetic native save/load profile.

Does not launch/stop processes or prove process ownership, historical-save compatibility,
or ordinary acceptance. Only a temporary log derivative is written; profile bytes stay intact.
"""
from __future__ import annotations

from datetime import datetime
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile

from personas import persona_matrix

TOOLS = Path(__file__).resolve().parent
PERSONA = TOOLS / "personas/subsidence-save-native-check.persona"
ROOT = re.compile(r"/mnt/c/taf-scenario\.[A-Za-z0-9]+\Z")
GAME_ID = r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}"
TIMESTAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\Z")
UNEXPECTED_DIAGNOSTIC = re.compile(
    rb"\b(?:MODWARN|MODERROR|WARN(?:ING)?|ERROR|FATAL)\b|\b(?:[\w.]+)?Exception\b|^\s*(?:at\s|---)"
    rb"|^\s*[\w.$+`<>]+[.:][\w.$+`<>]+\s*\(", re.IGNORECASE)
PREACTIVATION = ("exact ss5 bytes and 49 loaded bodies; one credit of five; original anchor unpaid; autoonce=true"
                 "; before-AfterGameLoaded-handlers-and-zone-activation=true")
RECOVERY = "remaining-four=proved; population=45; original-step-retired=true; replay=false; status=clear; activation-already-recovered="
COMPLETE = ("native-load cases=2 passed=2 failed=0; real-save-quit-load=true; new-game-script-replayed=false"
            "; recovery={route}; ordinary-acceptance=false; profiles-and-effects-retained=true")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def verify_journal(text: str) -> dict[str, str]:
    """Validate the real journal grammar and exact current four-row native-load protocol."""
    normalized = text.replace("\r\n", "\n")
    lines = normalized.split("\n")
    require("\r" not in normalized and len(lines) == 5 and lines[-1] == "",
            "journal must contain exactly four LF/CRLF-terminated rows, without blank/extra rows")
    rows = persona_matrix.read_journal(normalized)
    expected = ["LOAD-BEGIN", "LOAD-PREACTIVATION", "LOAD-RECOVERY", "SCRIPT-COMPLETE"]
    require([row[0] for row in rows] == expected, "journal rows are missing, repeated, reordered, or foreign")
    require(all(row[1] == "OK" for row in rows), "every native load row must be OK")
    for line in lines[:-1]:
        timestamp = line.split("\t", 1)[0]
        require(TIMESTAMP.fullmatch(timestamp), "journal timestamp is not canonical UTC milliseconds")
        datetime.strptime(timestamp, "%Y-%m-%dT%H:%M:%S.%fZ")
    begin = re.fullmatch("exact sealed save; game-id=(" + GAME_ID
                         + "); new-game=false; mod-restore=false", rows[0][2])
    require(begin is not None, "load intent lacks its canonical game-id or exact no-new-game/no-mod-restore proof")
    require(rows[1][2] == PREACTIVATION, "pre-activation saved-state/autoonce proof differs")
    require(rows[2][2] in (RECOVERY + "true", RECOVERY + "false"), "remaining-four/no-replay/clear-status proof differs")
    route = "native-zone-activation" if rows[2][2] == RECOVERY + "true" else "explicit-production-prepass"
    require(rows[3][2] == COMPLETE.format(route=route), "completion cases, retention, or recovery route contradict the witness")
    return {"game_id": begin.group(1), "recovery": route}


def verify_log(raw: bytes) -> None:
    manifest, name = persona_matrix.load(str(PERSONA))
    expected = persona_matrix.parse_log_expect(manifest.get("LOG_EXPECT", ""), name)
    require(len(expected) == 2
            and expected[0].startswith("MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, ")
            and expected[1].startswith("MODWARN [Pets of Harvest Dawn] - XmlDataHelper:: ")
            and expected[1].endswith("Freehold_Pet_Ercolano/PopulationTables.xml line 4 char 6")
            and all("MODERROR" not in line for line in expected),
            "load verdict requires the source-save persona's exact manual-order and Pets XML warning pair")
    # A load need not build the new-game population table. Only that exact XML line is optional.
    lines = raw.replace(b"\r\n", b"\n").split(b"\n")
    selected = expected if expected[1].encode("utf-8") in lines else expected[:1]
    load_manifest = dict(manifest, LOG_EXPECT=json.dumps(selected, ensure_ascii=False))
    derivative = persona_matrix.expected_log(load_manifest, raw, name)
    for number, line in enumerate(derivative.split(b"\n"), 1):
        require(not UNEXPECTED_DIAGNOSTIC.search(line), "unexpected native-load diagnostic at derivative line "
                + str(number) + ": " + line.decode("utf-8", errors="replace"))
    environment = os.environ.copy()
    environment["TAF_LOG_ALLOW"] = ""  # Never inherit a gate-refusal/frame allowance.
    with tempfile.TemporaryDirectory(prefix="taf-load-verdict.") as scratch:
        checked = Path(scratch) / "checked.Player.log"
        checked.write_bytes(derivative)
        result = subprocess.run(["bash", str(TOOLS / "check-player-log.sh"), str(checked)],
                                env=environment, capture_output=True, text=True, check=False)
    require(result.returncode == 0, "strict Player.log check refused: " + (result.stderr or result.stdout).strip())


def read_bounded(path: Path, limit: int) -> bytes:
    for parent in (path.parent, *path.parent.parents):
        status = parent.lstat()
        require(stat.S_ISDIR(status.st_mode) and not stat.S_ISLNK(status.st_mode)
                and not getattr(status, "st_file_attributes", 0) & 0x400, "linked/reparse log ancestor")
    require(os.path.realpath(path.parent) == str(path.parent), "aliased log directory")
    before = path.lstat()
    require(stat.S_ISREG(before.st_mode) and before.st_nlink == 1
            and not getattr(before, "st_file_attributes", 0) & 0x400
            and 0 < before.st_size <= limit, "journal/log must be bounded, nonempty, ordinary single-link files")
    with path.open("rb") as handle:
        data = handle.read(limit + 1)
    after = path.lstat()
    require(len(data) == before.st_size and len(data) <= limit
            and (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns)
            == (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns),
            "journal/log changed during read")
    return data


def verify_profile(root: Path) -> dict[str, str]:
    result = verify_journal(read_bounded(root / "scenario-journal.tsv", 1048576).decode("utf-8"))
    verify_log(read_bounded(root / "Player.log", 64 * 1024 * 1024))
    return result


def main(argv: list[str]) -> int:
    try:
        require(len(argv) == 2 and ROOT.fullmatch(argv[1]), "usage: verify-scenario-load.py /mnt/c/taf-scenario.<alnum>")
        result = verify_profile(Path(argv[1]))
    except (ValueError, OSError, SystemExit, subprocess.SubprocessError) as error:
        print("NATIVE LOAD VERDICT REFUSED: " + str(error), file=sys.stderr)
        return 2
    print("NATIVE LOAD VERDICT PASS: cases=2; game-id=" + result["game_id"] + "; recovery=" + result["recovery"]
          + "; historical-save-compatibility=untested; ordinary-acceptance=false; process-authority=unproved")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
