#!/usr/bin/env python3
"""Prepare old-native sources, full-history upgrades, or isolated old-reader fixtures.

Only fresh /mnt/c/taf-scenario.<id> destinations. Never launches/stops Qud, overwrites/reseals a
profile, copies old runtime into an upgrade, or calls transport verification native acceptance.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

import scenario_profile
from upgrade_profile_inputs import OLD_PIN, configuration, json_bytes, local_inputs, require, sha
from upgrade_profile_state import (ID, authenticate_source, capture, fs, inventory, native,
                                   native_plan, windows)


def stopped(source: Path, game: Path) -> None:
    """Ownership receipt is mandatory; absence of unrelated processes alone is insufficient."""
    fs.ownership_shape(fs.read_bytes(source / "process-ownership.json", 16384), source)
    helper = Path(__file__).with_name("assert-scenario-source-stopped.ps1")
    convert = lambda path: subprocess.run(["wslpath", "-w", str(path)], check=True,
        stdout=subprocess.PIPE, text=True).stdout.strip()
    subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    convert(helper), "-Root", windows(source), "-Game", convert(game)], check=True)


def clean_log(source: Path) -> str:
    path = source / "Player.log"
    before = fs.digest(path)
    raw = fs.read_bytes(path, fs.MAX_FILE)
    require(raw and not re.search(rb"(?m)^MOD(?:ERROR|WARN)\b", raw), "native log contains MODERROR/MODWARN")
    env = dict(os.environ, TAF_LOG_ALLOW="")
    subprocess.run(["bash", str(Path(__file__).with_name("check-player-log.sh")), str(path)],
                   env=env, check=True)
    require(fs.digest(path) == before, "native log changed during validation")
    return before


def fresh(root: Path, inputs: dict[str, bytes]) -> None:
    windows(root)
    fs.empty_destination(root)
    seal = Path(str(root) + ".seal")
    require(not os.path.lexists(seal), "destination seal already exists; resealing is forbidden")
    if not root.exists():
        root.mkdir()
    for name in ("Save", "Local"):
        (root / name).mkdir()
    for relative, raw in sorted(inputs.items()):
        target = root / "Local" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        fs.write_new(target, raw)


def finish(root: Path, config: dict, inputs: dict[str, bytes], evidence: dict) -> None:
    files, _ = inventory(root, ["Local"])
    actual = {row["path"][6:]: row["sha256"] for row in files}
    require(actual == {p: sha(raw) for p, raw in inputs.items()}, "new pinned Local readback differs")
    full_files, full_dirs = inventory(root, ["Local", "Synced"])
    evidence["destinationInspect"] = native("Inspect", native_plan(root, None,
        ["Local", "Synced"], full_files, full_dirs))
    # This is the one and only seal, after EVERY launcher input and copied state is complete.
    seal = Path(str(root) + ".seal")
    require(not os.path.lexists(seal), "destination seal already exists")
    seal.mkdir()
    normalized = {scenario_profile.normalize(p): h for p, h in actual.items()}
    require(len(normalized) == len(actual), "new profile paths collide")
    wire = scenario_profile.SEAL_HEADER + "\n" + "".join(
        normalized[p] + "  " + p + "\n" for p in sorted(normalized))
    fs.write_new(seal / "profile.sha256", wire.encode("utf-8"))
    request = "arch-gallery-slice;facing=north;seed=" + config["seed"] + "\n"
    fs.write_new(seal / "request.txt", request.encode("ascii"))
    evidence.update(schema="taf-upgrade-transport-v1", destination=str(root),
                    config=config, localSha256=actual, nativeAcceptance=False)
    fs.write_new(root / "upgrade-transport.json", json_bytes(evidence))
    print("UPGRADE PROFILE READY (transport only): " + str(root))
    print("Runtime: " + config["runtime"] + "; observers: " + config["probe"])
    print("Native compatibility NOT RUN; root must run the pinned protocol and strict log checks.")


def prepare_source(args) -> None:
    case = "reader" if args.mode == "stage-source" else args.case
    runtime = args.candidate if args.mode == "stage-source" else OLD_PIN
    config = configuration(args.mode, runtime, args.candidate, case, args.seed)
    inputs = local_inputs(args.repo, config)
    fresh(args.destination, inputs)
    (args.destination / "Synced/Saves").mkdir(parents=True)
    finish(args.destination, config, inputs, dict(source=None, copiedFiles=[]))
    print("Launch using Tools/run-scenario.ps1 -OwnAttended. Do not add a scenario script.")


def source_evidence(args, mode: str, pin: str) -> tuple[dict, dict, str]:
    require(args.source is not None and args.game is not None, "--source and --game are required")
    windows(args.source)
    windows(args.destination)
    require(str(args.source).casefold() != str(args.destination).casefold(), "source equals destination")
    fs.directory(args.source)
    fs.file_status(args.game)
    stopped(args.source, args.game)  # Before reading save/cache bytes or creating output.
    config, state = authenticate_source(args.repo, args.source, mode, pin)
    require(config["probe"] == (args.source_probe_pin or args.candidate),
            "source observer pin was not explicitly approved")
    return config, state, clean_log(args.source)


def reprove(args, config: dict, state: dict, log_hash: str) -> None:
    stopped(args.source, args.game)
    again, after = authenticate_source(args.repo, args.source, config["mode"], config["runtime"])
    require(again == config and after["files"] == state["files"]
            and after["directories"] == state["directories"]
            and after["localHashes"] == state["localHashes"]
            and fs.digest(args.source / "Player.log") == log_hash, "source changed across transfer")


def prepare_upgrade(args) -> None:
    source_config, state, log_hash = source_evidence(args, "source", OLD_PIN)
    snapshot, request, witness = capture(args.source, source_config, state)
    config = configuration("upgrade", args.candidate, args.candidate, source_config["case"], source_config["seed"])
    inputs = local_inputs(args.repo, config, {"scenario-load-snapshot.txt": snapshot, "scenario-load.txt": request})
    fresh(args.destination, inputs)
    plan = native_plan(args.source, args.destination, ["Synced"], state["files"], state["directories"])
    proof = native("Copy", plan)
    reprove(args, source_config, state, log_hash)
    snapshot_after, request_after, witness_after = capture(args.source, source_config, state)
    require((snapshot_after, request_after, witness_after) == (snapshot, request, witness),
            "native source receipt/snapshot changed across transfer")
    destination_files, destination_dirs = inventory(args.destination, ["Synced"])
    require(destination_files == state["files"] and destination_dirs == state["directories"],
            "destination lost historical files or directory membership")
    finish(args.destination, config, inputs, dict(source=str(args.source), sourceLogSha256=log_hash,
        sourceConfig=source_config, sourceInspect=state["inspect"], witness=witness, copy=proof))


def prepare_downgrade(args) -> None:
    require(args.origin is not None and re.fullmatch(ID, args.origin), "--origin needs one safe stage origin")
    source_config, state, log_hash = source_evidence(args, "stage-source", args.candidate)
    by_path = {row["path"]: row for row in state["files"]}
    selected, slot_rows = [], []
    for slot in "ab":
        path = "Synced/ThousandAndFirst/Stages/" + args.origin + "." + slot + ".seal"
        row = by_path.get(path)
        if row is None:
            slot_rows.append(slot + " absent")
        else:
            selected.append(path)
            slot_rows.append(slot + " " + str(row["size"]) + " " + row["sha256"])
    require(selected, "source has neither declared stage slot")
    wire = "\n".join(["taf-downgrade-observe-v1", windows(args.destination), args.origin,
                        args.candidate, OLD_PIN, *slot_rows]) + "\n"
    config = configuration("downgrade", OLD_PIN, args.candidate, "reader", source_config["seed"])
    inputs = local_inputs(args.repo, config, {"taf-downgrade-request.txt": wire.encode("ascii")})
    fresh(args.destination, inputs)
    proof = native("Slots", native_plan(args.source, args.destination, ["Synced"],
                    state["files"], state["directories"], selected))
    # Main-menu probe separately asserts this is empty. Never copy any newer world/save.
    (args.destination / "Synced/Saves").mkdir()
    reprove(args, source_config, state, log_hash)
    finish(args.destination, config, inputs, dict(source=str(args.source), sourceLogSha256=log_hash,
        sourceConfig=source_config, sourceInspect=state["inspect"], copy=proof,
        scope="old-reader only; no newer save copied or loaded"))
    print("Launch using Tools/run-scenario.ps1 -OwnAttended; stay at the main menu.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("source", "stage-source", "upgrade", "downgrade"))
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument("--candidate", required=True, help="full immutable CURRENT commit, including integrated probes")
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--source-probe-pin", help="explicit approved source observer commit; defaults to candidate")
    parser.add_argument("--game", type=Path)
    parser.add_argument("--case", choices=("inheritance", "detached-transition"), default="inheritance")
    parser.add_argument("--seed", default="#1012037")
    parser.add_argument("--origin")
    args = parser.parse_args()
    try:
        if args.mode in ("source", "stage-source"):
            prepare_source(args)
        elif args.mode == "upgrade":
            prepare_upgrade(args)
        else:
            prepare_downgrade(args)
    except (ValueError, OSError, subprocess.SubprocessError, UnicodeError, KeyError, TypeError) as error:
        print("UPGRADE PROFILE REFUSED: " + str(error) + "; partial output retained, never resealed", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
