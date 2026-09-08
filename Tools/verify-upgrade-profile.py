#!/usr/bin/env python3
"""Read-only terminal verdict: exact owned exit, pinned Local, native witness, strict raw log.

Transport metadata alone never passes this verifier. It launches/stops no game, applies no log
allowlist, and calls an old-reader fixture only reader compatibility, never a downgraded world.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import importlib.util
from pathlib import Path
import re
import subprocess
import sys

import scenario_profile
from upgrade_profile_inputs import CONFIG, OLD_PIN, SHA, local_inputs, parse_config, require, sha
from upgrade_profile_state import authenticate_source, capture, fs, inventory, native, native_plan, windows

_spec = importlib.util.spec_from_file_location("taf_upgrade_host", Path(__file__).with_name("prepare-upgrade-profile.py"))
assert _spec and _spec.loader
host = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(host)
DIAGNOSTIC = re.compile(rb"\b(?:MODWARN|MODERROR|WARN(?:ING)?|ERROR|FATAL|REFUSED)\b|\b(?:[\w.]+)?Exception\b"
                        rb"|^\s*(?:at\s|---)", re.IGNORECASE)


def journal(raw: bytes, config: dict, game_id: str) -> None:
    text = raw.decode("utf-8").replace("\r\n", "\n")
    require("\r" not in text and text.endswith("\n"), "journal line framing differs")
    rows = [line.split("\t") for line in text[:-1].split("\n")]
    require(len(rows) == 3 and all(len(row) == 4 for row in rows), "upgrade journal needs exactly three rows")
    require([row[1] for row in rows] == ["LOAD-BEGIN", "UPGRADE-PREACTIVATION", "SCRIPT-COMPLETE"]
            and all(row[2] == "OK" for row in rows), "native upgrade witness missing, foreign, repeated, or refused")
    for row in rows:
        require(re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z", row[0]),
                "journal timestamp is not canonical")
        datetime.strptime(row[0], "%Y-%m-%dT%H:%M:%S.%fZ")
    require(rows[0][3] == "exact sealed save; game-id=" + game_id + "; new-game=false; mod-restore=false",
            "load intent belongs to another save or route")
    preactivation = ("case=" + config["case"]
        + "; raw-source-exact=true; pre-normalization=true; pre-repair=true; repair-calls=0")
    require(re.fullmatch(re.escape(preactivation) + r"; seal-stage-readable=true; stage-origin=[A-Za-z0-9_-]{1,96}; stage-sha256=[0-9a-f]{64}", rows[1][3]),
            "pre-repair/pre-normalization observation differs")
    expected = ("native-upgrade cases=1 passed=1 failed=0; case=" + config["case"]
        + "; actual-source-save=true; old-pin=" + OLD_PIN
        + "; pre-normalization-and-pre-repair=true; legacy-canonical=true; repair-calls=0"
        + "; source-graph-preserved=true; ordinary-ui-acceptance=false; no-world-state-written-by-witness=true")
    require(rows[2][3] == expected, "upgrade completion verdict differs")


def downgrade(report: bytes, request: bytes, raw_log: bytes, root: Path, config: dict) -> str:
    text = report.decode("ascii")
    require(text.endswith("\n") and "\r" not in text, "old-reader report framing differs")
    rows = text[:-1].split("\n")
    wire = request.decode("ascii").split("\n")
    require(len(wire) == 8 and wire[0] == "taf-downgrade-observe-v1" and wire[-1] == ""
            and wire[1] == windows(root) and wire[3] == config["probe"] and wire[4] == OLD_PIN,
            "old-reader request pin or root differs")
    require(rows[:9] == ["taf-downgrade-report-v1", "root=" + wire[1], "origin=" + wire[2],
            "source-pin=" + wire[3], "old-pin=" + OLD_PIN, "request-sha256=" + sha(request),
            "source-pin-authority=host-inventory", "old-runtime-authority=host-inventory",
            "entry=MainMenu.Show-postfix"], "old-reader report identity differs")
    require(rows.count(wire[5]) == 1 and rows.count(wire[6]) == 1, "old-reader declared slot inventory differs")
    for slot in wire[5:7]:
        pos = rows.index(slot)
        require(pos + 1 < len(rows), "old-reader slot lacks observation")
        if slot.endswith(" absent"):
            require(rows[pos + 1] == "slot-result=absent", "absent slot was invented")
    selected = [line for line in rows if line.startswith("read-stage=")]
    require(len(selected) == 1 and selected[0] in ("read-stage=absent", "read-stage=a", "read-stage=b"),
            "actual ReadStage result missing")
    for required in ("input-bytes-unchanged=true", "no-escaped-parser-or-store-exception=true",
                     "game-created=false", "save-loaded=false", "status=observed"):
        require(rows.count(required) == 1, "old-reader proof missing: " + required)
    require(any(line in ("schema2-rejected=1", "schema2-rejected=2") for line in rows), "no actual old schema rejection")
    terminal = ("native-downgrade-reader cases=1 passed=1 failed=0; main-menu=true; game-created=false; "
                + "save-loaded=false; report-sha256=" + sha(report)).encode("ascii")
    require(raw_log.count(terminal) == 1, "old-reader post-cleanup terminal log/report binding missing")
    return selected[0].split("=", 1)[1]


def verify(args) -> str:
    windows(args.root)
    host.stopped(args.root, args.game)
    config = parse_config(fs.read_bytes(args.root / "Local" / CONFIG, 4096))
    require(config["mode"] in ("upgrade", "downgrade") and config["probe"] == args.candidate,
            "terminal profile has wrong mode or candidate")
    extra = {}
    if config["mode"] == "upgrade":
        for name in ("scenario-load.txt", "scenario-load-snapshot.txt"):
            extra[name] = fs.read_bytes(args.root / "Local" / name, 4 * 1024**2)
    else:
        extra["taf-downgrade-request.txt"] = fs.read_bytes(args.root / "Local/taf-downgrade-request.txt", 4096)
    # Neither a self-labelled snapshot nor a self-authored transport receipt proves old bytes.
    # Reopen the explicitly retained source and bind its actual native capture to this request.
    windows(args.source)
    require(str(args.source).casefold() != str(args.root).casefold(), "source equals terminal profile")
    host.stopped(args.source, args.game)
    source_mode = "source" if config["mode"] == "upgrade" else "stage-source"
    source_pin = OLD_PIN if config["mode"] == "upgrade" else args.candidate
    source_config, source_state = authenticate_source(args.repo, args.source, source_mode, source_pin)
    require(source_config["probe"] == (args.source_probe_pin or args.candidate),
            "retained source observer pin was not explicitly approved")
    source_log_hash = host.clean_log(args.source)
    require(source_config["seed"] == config["seed"], "source and destination preparation seeds differ")
    if config["mode"] == "upgrade":
        source_snapshot, source_request, _ = capture(args.source, source_config, source_state)
        require(source_config["case"] == config["case"] and source_snapshot == extra["scenario-load-snapshot.txt"]
                and source_request == extra["scenario-load.txt"], "destination inputs are not the retained old native capture")
    else:
        request_rows = extra["taf-downgrade-request.txt"].decode("ascii").split("\n")
        require(len(request_rows) == 8, "downgrade request framing differs")
        source_rows = {row["path"]: row for row in source_state["files"]}
        expected_slots = []
        for slot in "ab":
            path = "Synced/ThousandAndFirst/Stages/" + request_rows[2] + "." + slot + ".seal"
            row = source_rows.get(path)
            expected_slots.append(slot + " absent" if row is None else slot + " " + str(row["size"]) + " " + row["sha256"])
        require(request_rows[5:7] == expected_slots, "old-reader inputs are not the retained current stage pair")
    expected = local_inputs(args.repo, config, extra)
    files, dirs = inventory(args.root, ["Local", "Synced"])
    actual = {row["path"][6:]: row["sha256"] for row in files if row["path"].startswith("Local/")}
    require(actual == {p: sha(raw) for p, raw in expected.items()}, "terminal runtime/probes differ from candidate pins")
    require(scenario_profile.read_seal(str(args.root) + ".seal/profile.sha256")
            == {scenario_profile.normalize(p): h for p, h in actual.items()}, "terminal closed Local seal differs")
    native("Inspect", native_plan(args.root, None, ["Local", "Synced"], files, dirs))
    log_hash = host.clean_log(args.root)
    raw_log = fs.read_bytes(args.root / "Player.log", 64 * 1024**2)
    for line in raw_log.replace(b"\r\n", b"\n").split(b"\n"):
        require(not DIAGNOSTIC.search(line), "unexpected native diagnostic: " + line.decode("utf-8", errors="replace")[:500])
    if config["mode"] == "upgrade":
        request = extra["scenario-load.txt"].decode("ascii").split("\n")
        require(len(request) == 7 and request[0] == "taf-scenario-load-v1" and request[-1] == ""
                and fs.GUID.fullmatch(request[1]) and all(SHA.fullmatch(v) for v in request[2:6])
                and sha(extra["scenario-load-snapshot.txt"]) == request[5], "upgrade load request malformed")
        journal(fs.read_bytes(args.root / "scenario-journal.tsv", 1024**2), config, request[1])
        verdict = "NATIVE UPGRADE PASS: case=" + config["case"] + "; game-id=" + request[1]
    else:
        result = downgrade(fs.read_bytes(args.root / "downgrade-reader-report.txt", 16384),
                           extra["taf-downgrade-request.txt"], raw_log, args.root, config)
        verdict = "NATIVE OLD-READER PASS: read-stage=" + result + "; newer-save-loaded=false"
    host.stopped(args.root, args.game)
    host.stopped(args.source, args.game)
    after_config, after_state = authenticate_source(args.repo, args.source, source_mode, source_pin)
    require(after_config == source_config and after_state["files"] == source_state["files"]
            and after_state["directories"] == source_state["directories"]
            and fs.digest(args.source / "Player.log") == source_log_hash, "retained source changed during terminal proof")
    if config["mode"] == "upgrade":
        after_snapshot, after_request, _ = capture(args.source, after_config, after_state)
        require(after_snapshot == source_snapshot and after_request == source_request,
                "retained native source capture changed during terminal proof")
    require(fs.digest(args.root / "Player.log") == log_hash, "terminal native log changed")
    return verdict + "; candidate=" + args.candidate + "; ordinary-ui-acceptance=false"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path, help="retained old native save source, or current stage source")
    parser.add_argument("--source-probe-pin", help="explicit approved source observer commit; defaults to candidate")
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--game", required=True, type=Path)
    args = parser.parse_args()
    try:
        print(verify(args))
        return 0
    except (ValueError, OSError, UnicodeError, subprocess.SubprocessError, KeyError, TypeError) as error:
        print("NATIVE CROSS-VERSION VERDICT REFUSED: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
