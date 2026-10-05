#!/usr/bin/env python3
"""Read-only verdict for the #283 stuck-save heal route's two sealed sessions.

Session one ran on a build WITHOUT the fix and must have reproduced the retired handover stall
exactly: its only Thousand and First diagnostics are the seal's own reports of that stall, and
they are retained as the defect evidence. Session two cold-loaded that save on the fixed build:
everything from its one readmission line onward passes the unchanged strict Player.log checker,
and before that line the only diagnostics are the seal's reports of the loaded stall itself.
Does not prove process custody, ordinary play or release readiness. Writes nothing.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
import scenario_profile as profile
from personas import persona_matrix
from persona_reload_heal import FIX_CHANGED, FIX_NEW

TOOLS = Path(__file__).resolve().parent
_SPEC = importlib.util.spec_from_file_location("heal_safe_files", TOOLS / "prepare-scenario-load.py")
files = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(files)
require = files.require
_SPEC = importlib.util.spec_from_file_location("heal_unfounded", TOOLS / "check-unfounded-results.py")
shared = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(shared)

SCRIPT = tuple(persona_matrix.RENOVATE_HEAL_RELOAD_SCRIPT)
VERBS = ",".join(persona_matrix.RENOVATE_HEAL_RELOAD_VERBS)
SNAPSHOT = re.compile(r"taf-renovate-heal-v1:([0-9a-f-]{36});([A-Za-z0-9._-]{1,128});([0-9a-f]{32});"
                      r"([A-Za-z0-9._-]{1,128});([A-Za-z0-9._-]{1,128});([AB]);([0-5]);"
                      r"([A-Za-z0-9._-]{1,128});(0|[1-9][0-9]{0,18})\Z")
SAVE_EXPECT = (
    "stagedigest:OK~founded=false,tier-upgrade-setup:OK~native-tier-upgrade phase=1,advance:OK,"
    "tier-upgrade-check:OK~native-tier-upgrade phase=2,tier-upgrade-short:OK~native-tier-upgrade "
    "phase=3,advance:OK,tier-upgrade-check:OK~native-tier-upgrade phase=4,advance:OK,"
    "tier-upgrade-after-wait:OK~native-tier-upgrade after-wait,"
    "renovate-heal-save:OK~renovate-heal-save=true,COMPLETE")
LOAD_ROWS = ("LOAD-BEGIN", "renovate-heal-preactivation", "renovate-heal-resume", "renovate-heal-after",
             "renovate-heal-resaved", "SCRIPT-COMPLETE")
TEXT = {"A": "Founder marks did not settle exactly on the successor.",
        "B": "The paid improvement job no longer matches its exact physical endpoints."}
STALL = re.compile(r"an authored work has incomplete or changed frozen evidence: settled layout slot "
                   r"[a-z]:[0-9]{2}:[0-9]{2} is absent, moved, duplicated, or changed\Z")
REPORT = re.compile(r"MODERROR \[The Thousand and First( \[ALPHA\])?( \[DEV SCENARIO HARNESS\])?\] - "
                    r"ThousandAndFirst: seal (?P<stage>[A-Za-z ]+) failed closed: "
                    r"System\.InvalidOperationException: (?P<reason>.+)\Z")
FIX_KEYS = sorted("mods/thousandandfirst/" + path.casefold() for path in FIX_NEW + FIX_CHANGED)
ADVANCE = re.compile(r"(2400|2401) turn\(s\) elapsed of 2400 requested\Z")


def rows_of(raw: bytes) -> tuple[str, list[tuple[str, str, str]]]:
    """The unfounded checker's exact journal law with a bound that fits nine thousand turns."""
    text = raw.decode("utf-8").replace("\r\n", "\n")
    require(text.endswith("\n") and "\r" not in text, "journal must be LF/CRLF terminated")
    lines = text[:-1].split("\n")
    require(1 <= len(lines) <= 4096, "journal row bound exceeded")
    stamps = []
    for line in lines:
        columns = line.split("\t")
        require(len(columns) == 4 and all(columns) and shared.STAMP.fullmatch(columns[0])
                and len(columns[3]) <= 32768
                and re.fullmatch(r"(?:[^\\\x00-\x1f\x7f]|\\[\\nrt])*", columns[3]),
                "journal row malformed")
        stamps.append(columns[0])
    require(stamps == sorted(stamps), "journal timestamps run backwards")
    return text, persona_matrix.read_journal(text)


def flagged(line: str) -> bool:
    """The strict checker's own predicate (Tools/check-player-log.sh), restated to classify."""
    lower = line.lower()
    return bool(re.match(r"MOD(ERROR|WARN) \[The Thousand and First( \[ALPHA\])?( \[DEV SCENARIO "
                         r"HARNESS\])?\](\s|$)", line)
                or re.search(r"(\[taf\]|thousandandfirst|the thousand and first)", lower)
                and re.search(r"(exception|error|fault|quarantin|inspection required)", lower)
                or re.match(r"\s*(at|---).*thousandandfirst[.:]", lower))


def strict(lines: list[str]) -> None:
    with tempfile.TemporaryDirectory(prefix="taf-heal-log.") as scratch:
        path = Path(scratch) / "Player.log"
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        checked = subprocess.run(["bash", str(TOOLS / "check-player-log.sh"), str(path)],
                                 env=dict(os.environ, TAF_LOG_ALLOW=""), stdout=subprocess.PIPE,
                                 stderr=subprocess.STDOUT, timeout=120)
    require(checked.returncode == 0, "strict Player.log checker refused (no allowances): "
            + checked.stdout.decode("utf-8", errors="replace")[-400:])


def stall_reports(lines: list[str], stages: tuple[str, ...]) -> tuple[list[str], list[str]]:
    """Splits out the seal's reports of the stuck renovation; returns them and every other line."""
    reports, rest = [], []
    for line in lines:
        found = REPORT.match(line) if flagged(line) else None
        if found and found.group("stage") in stages and STALL.match(found.group("reason")):
            reports.append(found.group("reason"))
        else:
            rest.append(line)
    return reports, rest


def save_log(raw: bytes, reason: str) -> int:
    lines = raw.decode("utf-8", errors="replace").replace("\r\n", "\n").split("\n")
    require(any("[TAF] improvement begun: tent -> tentrow" in line for line in lines),
            "session one never began the paid tent -> tentrow renovate")
    require(not any("[TAF] improvement handover: " in line or "improvement readmitted:" in line
                    for line in lines), "session one handed over or readmitted: no stall to heal")
    reports, rest = stall_reports(lines, ("daily stage", "BeforeSave stage"))
    require(reports and set(reports) == {reason},
            "session one lacks the seal's report of exactly the journaled stall")
    strict(rest)
    return len(reports)


def load_log(raw: bytes, job: str, defect: str) -> tuple[str, list[str]]:
    lines = raw.decode("utf-8", errors="replace").replace("\r\n", "\n").split("\n")
    marker = "[TAF] improvement readmitted: job=" + job + " defect="
    found = [index for index, line in enumerate(lines) if "improvement readmitted:" in line]
    require(len(found) == 1 and marker in lines[found[0]], "session two lacks exactly one readmission line")
    readmitted = lines[found[0]].split(marker, 1)[1][:1]
    require(readmitted == defect, "session two readmitted another defect than the saved stall")
    tail = lines[found[0]:]
    require(any("[TAF] improvement handover: " in line for line in tail[1:]),
            "the readmitted handover did not complete")
    reports, rest = stall_reports(lines[:found[0]], ("loaded stage reconciliation", "daily stage"))
    require(not any(flagged(line) for line in rest),
            "session two logged a diagnostic before the heal other than the loaded stall's own report")
    strict(tail)
    return readmitted, sorted(set(reports))


def verify(root: Path, phase: str) -> dict:
    require(phase in ("save", "load") and root.is_absolute() and files.ROOT_NAME.fullmatch(root.name),
            "expected absolute taf-scenario.<alnum> root and save/load phase")
    files.directory(root)
    local, seal = root / "Local", Path(str(root) + ".seal")
    expected = profile.read_seal(str(seal / "profile.sha256"))
    files.tree_files(local, files.MAX_LOCAL_FILE)
    require(profile.inventory(str(local)) == expected, "Local differs from closed seal")
    script = files.read_bytes(local / "scenario-script.txt", 65536).decode("utf-8")
    commands = tuple(line for line in script.splitlines() if line and not line.startswith("#"))
    require(commands == SCRIPT, "sealed script is not the exact heal script")
    request = files.request_text(files.read_bytes(seal / "request.txt", 1024))
    require(request.split(";")[0] == "founding-first-city", "sealed request is not founding-first-city")
    embark = files.read_bytes(local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml",
                              files.MAX_LOCAL_FILE).decode("utf-8")
    start, zone = shared.start_of(embark)
    loading = phase == "load"
    present = [os.path.lexists(local / name) for name in ("scenario-load.txt", "scenario-load-snapshot.txt")]
    require(present == [loading, loading], "load request presence does not match the phase")
    receipt = files.read_bytes(local / "scenario-load.txt" if loading else root / "scenario-save-receipt.txt",
                               512).decode("ascii").split("\n")
    count, header = (7, "taf-scenario-load-v1") if loading else (6, "taf-scenario-save-v1")
    require(len(receipt) == count and receipt[-1] == "" and receipt[0] == header
            and files.GUID.fullmatch(receipt[1]) and all(files.SHA.fullmatch(s) for s in receipt[2:-1]),
            "malformed exact save/load receipt")
    game = receipt[1]
    raw = files.read_bytes(local / "scenario-load-snapshot.txt" if loading
                           else root / "scenario-save-snapshot.txt", 4096)
    require(hashlib.sha256(raw).hexdigest() == receipt[-2], "snapshot hash mismatch")
    snap = SNAPSHOT.match(raw.decode("ascii"))
    require(snap and snap.group(1) == game and snap.group(2) == zone, "snapshot does not bind save and ground")
    job, predecessor, successor, defect = snap.group(3), snap.group(4), snap.group(5), snap.group(6)
    save = root / "Synced/Saves" / game
    files.directory(save)
    names = tuple(sorted(shared.LOAD_FILES if loading else files.SAVE_FILES))
    require(tuple(sorted(p.name for p in save.iterdir())) == names
            and [p.name for p in (root / "Synced/Saves").iterdir()] == [game], "save artifact inventory differs")
    hashes = {name: files.digest(save / name) for name in names}
    journal = files.read_bytes(root / "scenario-journal.tsv", 1048576)
    log = files.read_bytes(root / "Player.log", 64 * 1024 * 1024)
    text, rows = rows_of(journal)
    result = dict(verdict="PASS", phase=phase, command="renovate-heal " + start,
                  seed=request.rsplit(";seed=", 1)[1], gameId=game, zoneId=zone, defect=defect,
                  journalSHA256=hashlib.sha256(journal).hexdigest(),
                  playerLogSHA256=hashlib.sha256(log).hexdigest(), scope="sealed-developer-heal-evidence",
                  processAuthority=False, ordinaryAcceptance=False, releaseAcceptance=False)
    if not loading:
        require(hashes["Primary.sav.gz"] == receipt[2] and hashes["Primary.json"] == receipt[3],
                "primary/info hash mismatch")
        manifest = persona_matrix.parse_manifest("REQUEST=founding-first-city\nSCRIPT=" + ";".join(SCRIPT)
                                                 + "\nVERBS=" + VERBS + "\nEXPECT=" + SAVE_EXPECT + "\n",
                                                 "heal-save-leg")
        problems = persona_matrix.assess(manifest, text, "heal-save-leg")
        require(not problems, "save journal: " + "; ".join(problems))
        after = [m for v, _, m in rows if v == "tier-upgrade-after-wait"][0]
        saved = [m for v, _, m in rows if v == "renovate-heal-save"][0]
        require("; job=" + job + ";" in after and "; phase=InspectionRequired;" in after
                and "; failure=" + TEXT[defect] + ";" in after and "; seal-fault=True;" in after,
                "the after-wait row does not show the stuck renovation")
        require(all(field in saved for field in ("defect=" + defect + ";", "; job=" + job + ";",
                                                 "; save=" + game + ";", "; primary-sha256=" + receipt[2],
                                                 "; snapshot-sha256=" + receipt[4])),
                "the heal save row does not bind its receipt and snapshot")
        reason = after.split("; seal-reason=", 1)[1]
        require(STALL.match(reason), "the stuck renovation's seal reason is not the stall reading")
        result.update(saveHashes=hashes, stallSealReason=reason, stallSealReports=save_log(log, reason))
        return result
    require(hashes["Primary.sav.gz.bak"] == receipt[2] and hashes["Primary.sav.gz"] != receipt[2],
            "the second save did not replace the imported stuck save")
    runtime = json.loads(files.read_bytes(root / "load-runtime-evidence.json", 1048576))
    require(runtime.get("crossBuild") is True and runtime.get("harnessIdentical") is True
            and runtime.get("runtimeDelta") == FIX_KEYS, "the load runtime is not this tree minus exactly the fix")
    significant = persona_matrix.significant(rows)
    require(tuple(v for v, _, _ in significant) == LOAD_ROWS and all(o == "OK" for _, o, _ in significant),
            "load journal rows differ: " + ",".join(v for v, _, _ in significant))
    message = {v: m for v, _, m in significant}
    require(message["LOAD-BEGIN"] == "exact sealed save; game-id=" + game + "; new-game=false; mod-restore=false",
            "load did not begin on the exact sealed save")
    require(message["renovate-heal-preactivation"].startswith("before-AfterGameLoaded=true; stuck=true; defect="
                                                             + defect + "; job=" + job + ";"),
            "pre-activation did not prove the saved stall")
    order = [v for v, _, _ in rows if v in ("renovate-heal-resume", "advance-complete", "renovate-heal-after")]
    waited = [m for v, _, m in rows if v == "advance-complete"]
    require(order == ["renovate-heal-resume", "advance-complete", "renovate-heal-after"]
            and len(waited) == 1 and ADVANCE.match(waited[0]), "the heal wait is not the sealed 2400 turns")
    after = message["renovate-heal-after"]
    require(all(field in after for field in ("; job=" + job + ";", "; phase=Complete;", "; physical=EffectsSettled;",
                                             "; predecessor=absent;", "; successor=" + successor + ";",
                                             "; pending=0;", "; seal-fault=False;"))
            and (defect != "A" or "; successor-yielding=1;" in after), "the heal after-row is not a finished handover")
    require(message["renovate-heal-resaved"].startswith("real-save=true; healed=true; defect=" + defect)
            and "; primary-sha256=" + hashes["Primary.sav.gz"] + ";" in message["renovate-heal-resaved"],
            "the second save row does not bind the written primary")
    require(message["SCRIPT-COMPLETE"].startswith("native-renovate-heal cold-load complete; "
                                                  "real-save-quit-load=true; readmitted-handover-complete=true"),
            "session two did not complete")
    readmitted, reasons = load_log(log, job, defect)
    result.update(importedSaveHashes={"Primary.sav.gz": receipt[2], "Primary.json": receipt[3]},
                  secondSaveHashes={n: hashes[n] for n in ("Primary.sav.gz", "Primary.json")},
                  backupSaveHash=hashes["Primary.sav.gz.bak"], readmittedDefect=readmitted,
                  preHealSealReasons=reasons, healedAfterLoad=True, crossBuild=True,
                  runtimeDelta=runtime["runtimeDelta"], predecessorId=predecessor)
    return result


def main(argv: list[str]) -> int:
    try:
        require(len(argv) == 4 and argv[2] == "--phase", "usage: check-renovate-heal-results.py ROOT --phase save|load")
        result = verify(Path(argv[1]), argv[3])
    except (OSError, ValueError, KeyError, IndexError, SystemExit, UnicodeDecodeError,
            subprocess.SubprocessError) as error:
        print(json.dumps({"verdict": "REFUSED", "reason": str(error),
                          "ordinaryAcceptance": False, "releaseAcceptance": False}))
        return 1
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
