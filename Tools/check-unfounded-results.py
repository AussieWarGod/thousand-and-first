#!/usr/bin/env python3
"""Read-only verdict for sealed developer unfounded save and cold-load evidence (#272, #271).

The save leg is the ordinary sealed script stagedigest, unfounded-save, stagedigest on the
requested start. The load leg is a separate fresh profile that cold-loads that exact save, founds
the first city through the production transaction and writes a second real save. Does not prove
process custody, ordinary play, historical saves or release readiness. The log checker uses a
temporary exact copy; no profile or source file is written.
"""

from __future__ import annotations

from datetime import datetime
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

TOOLS = Path(__file__).resolve().parent
_SPEC = importlib.util.spec_from_file_location(
    "unfounded_safe_files", TOOLS / "prepare-scenario-load.py"
)
files = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(files)
require = files.require

REQUEST_KEY = "founding-first-city"
SCRIPT = tuple(persona_matrix.UNFOUNDED_RELOAD_SCRIPT)
VERB = persona_matrix.UNFOUNDED_RELOAD_VERB
SNAPSHOT_PREFIX = "taf-unfounded-save-v1:"
DORMANT_FRAME = "5"
SAVE_EXPECT = (
    "stagedigest:OK~founded=false,unfounded-save:OK~real-save=true dormant-frame=5,"
    "stagedigest:OK~founded=false,COMPLETE"
)
LOAD_ROWS = (
    "LOAD-BEGIN",
    "unfounded-preactivation",
    "unfounded-loaded",
    "unfounded-founded",
    "unfounded-resaved",
    "SCRIPT-COMPLETE",
)
DORMANT = "founded=false; load-failed=false; pristine-lifecycle-and-carry=true; lifecycle-frame=5"
FOUNDED = "founded=true; lifecycle-frame=10; growth-authority=true"
LOAD_PREFIXES = {
    "unfounded-preactivation": "before-AfterGameLoaded=true; " + DORMANT,
    "unfounded-loaded": "cold-load=true; after-activation=true; " + DORMANT,
    "unfounded-founded": "production-founding=true; " + FOUNDED + "; faction=",
    "unfounded-resaved": "real-save=true; " + FOUNDED + "; save-error=false"
    "; backup-is-imported-save=true; primary-changed=true; second-save-reload=false",
    "SCRIPT-COMPLETE": "native-unfounded cold-load session complete; real-save-quit-load=true"
    "; production-founding=true; second-real-save=true; new-game-script-replayed=false",
}
# The #272 failure text and the engine's serialization wrapper; also refused by the strict checker.
FORBIDDEN = ("growth envelope is not bounded and writable", "exception serializing object")
LOCATION = re.compile(
    r"GlobalLocation:JoppaWorld\.(0|[1-9][0-9]?)\.(0|[1-9][0-9]?)\.1\.1\.10"
    r"@(0|[1-9][0-9]?),(0|[1-9][0-9]?)\Z"
)
ZONE = re.compile(r"[A-Za-z0-9._-]{1,128}\Z")
TICKS = re.compile(r"(?:0|[1-9][0-9]{0,18})\Z")
STAMP = re.compile(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\Z")
LOAD_FILES = ("Cache.db", "Primary.json", "Primary.sav.gz", "Primary.sav.gz.bak")


def start_of(embark: str) -> tuple[str, str]:
    """The sealed TAF test-ground start as `<wx>.<wy>@<x>,<y>` and its surface zone id."""
    marker = embark.find(profile.START_MARKER)
    require(marker >= 0 and embark.count(profile.START_MARKER) == 1, "embark overlay lacks one test ground")
    begin = embark.find(profile.START_ATTRIBUTE, marker)
    require(begin >= 0, "embark test ground lacks a location")
    begin += len(profile.START_ATTRIBUTE)
    end = embark.find('"', begin)
    found = LOCATION.match(embark[begin:end]) if end > begin else None
    require(found, "embark test ground location is not a canonical surface start")
    wx, wy, x, y = found.groups()
    require(int(wx) < profile.WORLD_WIDTH and int(wy) < profile.WORLD_HEIGHT
            and int(x) < profile.ZONE_WIDTH and int(y) < profile.ZONE_HEIGHT, "start is off the map")
    return wx + "." + wy + "@" + x + "," + y, "JoppaWorld." + wx + "." + wy + ".1.1.10"


def snapshot_fields(raw: bytes, game_id: str, zone: str) -> list[str]:
    text = raw.decode("ascii")
    require(text.startswith(SNAPSHOT_PREFIX) and len(text) <= 512 and "\n" not in text
            and "\r" not in text, "malformed unfounded snapshot")
    fields = text[len(SNAPSHOT_PREFIX):].split(";")
    require(len(fields) == 6 and files.GUID.fullmatch(fields[0]) and ZONE.fullmatch(fields[1])
            and fields[2] == DORMANT_FRAME and files.SHA.fullmatch(fields[3])
            and files.SHA.fullmatch(fields[4]) and TICKS.fullmatch(fields[5]),
            "malformed unfounded snapshot")
    require(fields[0] == game_id, "snapshot belongs to another save")
    require(fields[1] == zone, "snapshot was taken on another ground than the sealed start")
    return fields


def rows_of(raw: bytes) -> tuple[str, list[tuple[str, str, str]]]:
    text = raw.decode("utf-8").replace("\r\n", "\n")
    require(text.endswith("\n") and "\r" not in text, "journal must be LF/CRLF terminated")
    lines = text[:-1].split("\n")
    require(1 <= len(lines) <= 64, "journal row bound exceeded")
    stamps = []
    for line in lines:
        columns = line.split("\t")
        require(len(columns) == 4 and all(columns), "journal needs four nonempty columns")
        require(STAMP.fullmatch(columns[0]), "journal timestamp is not exact UTC milliseconds")
        stamps.append(datetime.strptime(columns[0], "%Y-%m-%dT%H:%M:%S.%fZ"))
        require(len(columns[3]) <= 32768
                and re.fullmatch(r"(?:[^\\\x00-\x1f\x7f]|\\[\\nrt])*", columns[3]),
                "invalid journal message escaping or bound")
    require(stamps == sorted(stamps), "journal timestamps run backwards")
    return text, persona_matrix.read_journal(text)


def save_journal(raw: bytes, game_id: str, lifecycle: str) -> None:
    text, rows = rows_of(raw)
    manifest = persona_matrix.parse_manifest(
        "REQUEST=" + REQUEST_KEY + "\nSCRIPT=" + ";".join(SCRIPT) + "\nVERBS=" + VERB
        + "\nEXPECT=" + SAVE_EXPECT + "\n", "unfounded-save-leg")
    problems = persona_matrix.assess(manifest, text, "unfounded-save-leg")
    require(not problems, "save journal: " + "; ".join(problems))
    saved = [message for verb, _, message in rows if verb == VERB]
    require(len(saved) == 1 and "; founded=false;" in saved[0] and "; save-error=false;" in saved[0]
            and "; game-id=" + game_id + ";" in saved[0]
            and "; lifecycle-sha256=" + lifecycle + ";" in saved[0],
            "unfounded-save row does not bind the receipt and snapshot")


def load_journal(raw: bytes, game_id: str, lifecycle: str) -> None:
    _, rows = rows_of(raw)
    significant = persona_matrix.significant(rows)
    require(tuple(verb for verb, _, _ in significant) == LOAD_ROWS,
            "load journal has missing, extra or reordered rows: "
            + ",".join(verb for verb, _, _ in significant))
    require(all(outcome == "OK" for _, outcome, _ in significant), "load journal contains a refusal")
    messages = {verb: message for verb, _, message in significant}
    require(messages["LOAD-BEGIN"] == "exact sealed save; game-id=" + game_id
            + "; new-game=false; mod-restore=false", "load did not begin on the exact sealed save")
    for verb, prefix in LOAD_PREFIXES.items():
        require(messages[verb].startswith(prefix), verb + " lacks its exact claims")
    for verb in ("unfounded-preactivation", "unfounded-loaded"):
        require("; lifecycle-sha256=" + lifecycle + ";" in messages[verb]
                and messages[verb].endswith("; game-id=" + game_id),
                verb + " does not bind the saved dormant book")


def verify_log(raw: bytes) -> None:
    require(raw, "Player.log is empty")
    decoded = raw.decode("utf-8", errors="replace")
    for text in FORBIDDEN:
        require(text not in decoded, "Player.log carries a forbidden diagnostic: " + text)
    with tempfile.TemporaryDirectory(prefix="taf-unfounded-log.") as scratch:
        path = Path(scratch) / "Player.log"
        path.write_bytes(raw)
        checked = subprocess.run(
            ["bash", str(TOOLS / "check-player-log.sh"), str(path)],
            env=dict(os.environ, TAF_LOG_ALLOW=""),
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120)
    require(checked.returncode == 0, "strict Player.log checker refused (no allowances)")


def verify(root: Path, phase: str) -> dict:
    require(phase in ("save", "load") and root.is_absolute() and files.ROOT_NAME.fullmatch(root.name),
            "expected absolute taf-scenario.<alnum> root and save/load phase")
    files.directory(root)
    local, seal = root / "Local", Path(str(root) + ".seal")
    frozen = {}

    def read(path, maximum):
        data = files.read_bytes(path, maximum)
        require(data, "empty evidence: " + path.name)
        frozen[path] = (maximum, data, files.stamp(files.file_status(path, maximum)))
        return data

    seal_path = seal / "profile.sha256"
    seal_raw = read(seal_path, 4 * 1024 * 1024)
    expected = profile.read_seal(str(seal_path))
    canonical = profile.SEAL_HEADER + "\n" + "".join(
        expected[key] + "  " + key + "\n" for key in sorted(expected))
    require(seal_raw.decode("utf-8").replace("\r\n", "\n") == canonical,
            "seal parser did not observe the exact captured canonical seal")
    files.tree_files(local, files.MAX_LOCAL_FILE)
    require(profile.inventory(str(local)) == expected, "Local differs from closed seal")
    script = read(local / "scenario-script.txt", 65536).decode("utf-8")
    commands = tuple(line for line in script.splitlines() if line and not line.startswith("#"))
    require(commands == SCRIPT, "sealed script is not stagedigest, unfounded-save, stagedigest")
    request = files.request_text(read(seal / "request.txt", 1024))
    require(request.split(";")[0] == REQUEST_KEY, "sealed request is not the founding-first-city plan")
    seed = request.rsplit(";seed=", 1)[1]
    embark = read(local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml",
                  files.MAX_LOCAL_FILE).decode("utf-8")
    marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
    require(embark.count(marker) == 1 and embark.split(marker)[1].split('"', 1)[0] == request,
            "frozen seed request differs from sealed embark descriptor")
    start, zone = start_of(embark)
    loading = phase == "load"
    require(loading == all(os.path.lexists(local / name)
                           for name in ("scenario-load.txt", "scenario-load-snapshot.txt"))
            and (loading or not any(os.path.lexists(local / name)
                                    for name in ("scenario-load.txt", "scenario-load-snapshot.txt"))),
            "load request presence does not match the phase")
    receipt = read(local / "scenario-load.txt" if loading else root / "scenario-save-receipt.txt",
                   512).decode("ascii").split("\n")
    count, header = (7, "taf-scenario-load-v1") if loading else (6, "taf-scenario-save-v1")
    require(len(receipt) == count and receipt[-1] == "" and receipt[0] == header
            and files.GUID.fullmatch(receipt[1]) and all(files.SHA.fullmatch(s) for s in receipt[2:-1]),
            "malformed exact save/load receipt")
    game_id = receipt[1]
    raw = read(local / "scenario-load-snapshot.txt" if loading else root / "scenario-save-snapshot.txt",
               4096)
    require(hashlib.sha256(raw).hexdigest() == receipt[-2], "snapshot hash mismatch")
    snapshot = snapshot_fields(raw, game_id, zone)
    saves = root / "Synced/Saves"
    files.directory(saves)
    require(sorted(p.name for p in saves.iterdir()) == [game_id], "foreign or missing save directory")
    save = saves / game_id
    files.directory(save)
    names = tuple(sorted(LOAD_FILES if loading else files.SAVE_FILES))
    require(tuple(sorted(p.name for p in save.iterdir())) == names, "save artifact inventory differs")
    hashes = {}
    for name in names:
        require(files.file_status(save / name).st_size > 0, "empty save artifact")
        hashes[name] = (files.digest(save / name), files.stamp(files.file_status(save / name)))
    primary = files.read_bytes(save / "Primary.sav.gz", files.MAX_FILE)
    require(primary[:2] == b"\x1f\x8b", "primary save is not gzip")
    if loading:
        require(hashes["Primary.sav.gz.bak"][0] == receipt[2],
                "the second save backup is not the imported unfounded save")
        require(hashes["Primary.sav.gz"][0] != receipt[2],
                "the cold-loaded world did not replace its imported primary save")
    else:
        require(hashes["Primary.sav.gz"][0] == receipt[2] and hashes["Primary.json"][0] == receipt[3],
                "primary/info hash mismatch")
    journal_raw = read(root / "scenario-journal.tsv", 1048576)
    log_raw = read(root / "Player.log", 64 * 1024 * 1024)
    (load_journal if loading else save_journal)(journal_raw, game_id, snapshot[3])
    verify_log(log_raw)
    files.tree_files(local, files.MAX_LOCAL_FILE)
    require(profile.inventory(str(local)) == expected, "Local changed during verdict")
    for path, (maximum, before, stamp) in frozen.items():
        require(files.read_bytes(path, maximum) == before
                and files.stamp(files.file_status(path, maximum)) == stamp,
                "evidence changed during verdict: " + path.name)
    require(sorted(p.name for p in saves.iterdir()) == [game_id]
            and tuple(sorted(p.name for p in save.iterdir())) == names, "save inventory changed")
    for name, (digest, stamp) in hashes.items():
        require(files.digest(save / name) == digest and files.stamp(files.file_status(save / name)) == stamp,
                "save artifact changed during verdict")
    result = {
        "verdict": "PASS",
        "phase": phase,
        "command": VERB + " " + start,
        "seed": seed,
        "gameId": game_id,
        "zoneId": zone,
        "lifecycleSHA256": snapshot[3],
        "journalSHA256": hashlib.sha256(journal_raw).hexdigest(),
        "playerLogSHA256": hashlib.sha256(log_raw).hexdigest(),
        "scope": "sealed-developer-unfounded-evidence",
        "processAuthority": False,
        "ordinaryAcceptance": False,
        "historicalSaveCompatibility": False,
        "releaseAcceptance": False,
    }
    if loading:
        result.update(
            importedSaveHashes={"Primary.sav.gz": receipt[2], "Primary.json": receipt[3]},
            secondSaveHashes={name: hashes[name][0] for name in ("Primary.sav.gz", "Primary.json")},
            backupSaveHash=hashes["Primary.sav.gz.bak"][0],
            foundedAfterLoad=True)
    else:
        result["saveHashes"] = {name: digest for name, (digest, _) in hashes.items()}
    return result


def main(argv: list[str]) -> int:
    try:
        require(len(argv) == 4 and argv[2] == "--phase",
                "usage: check-unfounded-results.py ROOT --phase save|load")
        result = verify(Path(argv[1]), argv[3])
    except (OSError, ValueError, SystemExit, UnicodeDecodeError, subprocess.SubprocessError) as error:
        print(json.dumps({"verdict": "REFUSED", "reason": str(error),
                          "ordinaryAcceptance": False, "releaseAcceptance": False}))
        return 1
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
