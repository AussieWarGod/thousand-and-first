#!/usr/bin/env python3
"""Run one fresh sealed V2 profile unattended; preserve evidence and stop only its owned process."""
from __future__ import annotations

import argparse
import importlib.util
import os
from pathlib import Path
import re
import subprocess
import sys
import time

import scenario_profile
from upgrade_profile_inputs import CONFIG, OLD_PIN, PROFILE_V2, json_bytes, local_inputs, parse_config, recipe_request, require, sha
import upgrade_profile_state as state
from upgrade_profile_witnesses import DIAGNOSTIC

fs = state.fs
SECONDS = 600
INTENT = "upgrade-run-intent.json"
ARTIFACTS = {
    "source-donor": ("upgrade-donor-receipt.txt",),
    "source": ("upgrade-save-receipt.txt", "upgrade-save-snapshot.txt", "upgrade-source-link.txt"),
    "stage-source": ("upgrade-stage-receipt.txt",),
    "upgrade": (), "downgrade": ("downgrade-reader-report.txt",),
}
FAILURES = ("upgrade-donor-failure.txt", "upgrade-source-failure.txt", "upgrade-save-failure.txt",
            "upgrade-stage-failure.txt", "upgrade-load-failure.txt")


def _fresh(root: Path) -> None:
    outputs = {INTENT, "process-ownership.json", "Player.log", "scenario-journal.tsv", *FAILURES}
    outputs.update(name for names in ARTIFACTS.values() for name in names)
    outputs.update("upgrade-run-" + phase + suffix for phase in ("launch", "stop")
                   for suffix in (".stdout", ".stderr"))
    require(not any(os.path.lexists(root / name) for name in outputs), "profile has prior run evidence; no retry")


def _ancestor(args, root: Path, mode: str, pin: str, probe: str) -> tuple[dict, dict]:
    state.windows(root)
    require(str(root).casefold() != str(args.root).casefold(), "run profile cannot be its own source")
    state.stopped_source(root, args.game)
    config, observed = state.authenticate_source(args.repo, root, mode, pin, game=args.game)
    require(config["probe"] == probe, "source observer pin was not approved")
    return config, observed


def _birth(args, config: dict) -> tuple[dict[str, bytes], list[dict], list[str]]:
    mode, extra, donor = config["mode"], {}, None
    files, dirs = [], ["Synced", "Synced/Saves"]
    if mode == "source":
        reference = config["donor"]
        root = Path(reference["root"])
        parent, observed = _ancestor(args, root, "source-donor", OLD_PIN, reference["probe"])
        donor = state.capture_donor(root, parent, observed)
        require(donor["receiptSha256"] == reference["receiptSha256"], "retained donor receipt differs")
        files, dirs = observed["files"], observed["directories"]
    elif mode in ("upgrade", "downgrade"):
        require(args.source is not None, "terminal profiles require --source")
        parent, observed = _ancestor(args, args.source, "source" if mode == "upgrade" else "stage-source",
            OLD_PIN if mode == "upgrade" else args.candidate, args.source_probe_pin or args.candidate)
        require(parent["seed"] == config["seed"], "source and run seeds differ")
        if mode == "upgrade":
            snapshot, request, _ = state.capture(args.source, parent, observed)
            require(parent["case"] == config["case"], "source and upgrade cases differ")
            extra = {"scenario-load-snapshot.txt": snapshot, "scenario-load.txt": request}
            files, dirs = observed["files"], observed["directories"]
        else:
            witness = state.capture_stage(args.source, parent, observed)
            origin, slots = witness["origin"], []
            available = {row["path"]: row for row in observed["files"]}
            for slot in "ab":
                path = "Synced/ThousandAndFirst/Stages/" + origin + "." + slot + ".seal"
                row = available.get(path)
                slots.append(slot + " absent" if row is None else slot + " " + str(row["size"]) + " " + row["sha256"])
                if row is not None:
                    files.append(row)
            require(files, "native stage source has no retained slots")
            dirs += ["Synced/ThousandAndFirst", "Synced/ThousandAndFirst/Stages"]
            extra["taf-downgrade-request.txt"] = ("\n".join(["taf-downgrade-observe-v1",
                state.windows(args.root), origin, args.candidate, OLD_PIN, *slots]) + "\n").encode("ascii")
    else:
        require(args.source is None and args.source_probe_pin is None, "fresh source mode takes no external source")
    if mode == "source":
        require(args.source is None and args.source_probe_pin is None, "inheritor uses its sealed donor reference")
    return local_inputs(args.repo, config, extra, donor_witness=donor), files, dirs


def preflight(args) -> dict:
    state.windows(args.root); fs.directory(args.root); fs.file_status(args.game)
    _fresh(args.root)
    config = parse_config(fs.read_bytes(args.root / "Local" / CONFIG, 4096))
    require(config["schema"] == PROFILE_V2 and config["probe"] == args.candidate,
            "run requires a fresh V2 profile for the exact candidate")
    fs.directory(args.root / "Save")
    require(not any((args.root / "Save").iterdir()), "fresh profile Save directory is occupied")
    expected, history, history_dirs = _birth(args, config)
    files, dirs = state.inventory(args.root, ["Local", "Synced"])
    actual = {row["path"][6:]: row["sha256"] for row in files if row["path"].startswith("Local/")}
    require(actual == {path: sha(raw) for path, raw in expected.items()}, "birth Local differs from pinned recipe")
    require([row for row in files if row["path"].startswith("Synced/")] == history
            and [path for path in dirs if path == "Synced" or path.startswith("Synced/")] == history_dirs,
            "fresh history differs from exact empty, donor, save, or slot transfer")
    require(scenario_profile.read_seal(str(args.root) + ".seal/profile.sha256")
            == {scenario_profile.normalize(path): digest for path, digest in actual.items()}, "birth seal differs")
    require(fs.read_bytes(Path(str(args.root) + ".seal/request.txt"), 1024)
            == (recipe_request(args.repo, config) + "\n").encode("ascii"), "birth request seal differs")
    require(expected.get("scenario-script.txt"), "unattended profile has no exact sealed script")
    state.native("Inspect", state.native_plan(args.root, None, ["Local", "Synced"], files, dirs))
    _fresh(args.root)
    return config


def _windows(path: Path) -> str:
    return subprocess.run(["wslpath", "-w", str(path)], check=True, stdout=subprocess.PIPE,
                          text=True, timeout=15).stdout.strip()


def _argv(args, stop: bool = False) -> list[str]:
    helper = "scenario-process-control.ps1" if stop else "run-scenario.ps1"
    result = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
              _windows(Path(__file__).with_name(helper))]
    if stop:
        result += ["-Mode", "stop"]
    return result + ["-Root", state.windows(args.root), "-Game", _windows(args.game)]


def _command(root: Path, phase: str, argv: list[str], timeout: float) -> bytes:
    # Separate exclusive files retain output even if a launcher times out or refuses.
    fs.directory(root)
    with (root / ("upgrade-run-" + phase + ".stdout")).open("xb") as stdout:
        with (root / ("upgrade-run-" + phase + ".stderr")).open("xb") as stderr:
            subprocess.run(argv, check=True, stdout=stdout, stderr=stderr, timeout=timeout)
    return fs.read_bytes(root / ("upgrade-run-" + phase + ".stdout"), 4 * 1024**2)


def _receipt(root: Path) -> bytes:
    raw = fs.read_bytes(root / "process-ownership.json", 16384)
    fs.ownership_shape(raw, root)
    return raw


def _peek(path: Path, maximum: int) -> bytes | None:
    if not os.path.lexists(path):
        return None
    try:
        return fs.read_bytes(path, maximum)
    except ValueError as error:
        # Appending live output may overlap a read. This is only a poll hint; stopped validators
        # must prove complete stable bytes. Never soften a link, type, size or parse refusal.
        if str(error).startswith(("source identity changed:", "source changed while reading:", "source path changed:")):
            return None
        raise


def _ready(root: Path, mode: str) -> bool:
    require(not any(os.path.lexists(root / name) for name in FAILURES), "native failure artifact retained")
    log = _peek(root / "Player.log", 64 * 1024**2) or b""
    for line in log.split(b"\n")[:-1]:
        require(not DIAGNOSTIC.search(line), "native log reported a failure: " + line.decode("utf-8", errors="replace")[:300])
    journal = _peek(root / "scenario-journal.tsv", 1024**2) or b""
    rows = [line.rstrip(b"\r").split(b"\t") for line in journal.split(b"\n")[:-1]]
    require(all(len(row) == 4 and row[2] == b"OK" for row in rows), "native journal refused or malformed")
    if mode == "downgrade":
        report = _peek(root / ARTIFACTS[mode][0], 16384)
        terminal = ("native-downgrade-reader cases=1 passed=1 failed=0; main-menu=true; game-created=false; "
                    + "save-loaded=false; report-sha256=" + sha(report or b"")).encode("ascii")
        return bool(report and log.count(terminal) == 1)
    terminals = [row for row in rows if row[1] in (b"SCRIPT-COMPLETE", b"SCRIPT-STOPPED", b"GATE-REFUSED")]
    if not terminals:
        return False
    require(len(terminals) == 1 and terminals[0] is rows[-1] and terminals[0][1] == b"SCRIPT-COMPLETE",
            "native journal has a refused, repeated or nonterminal completion")
    require(all(_peek(root / name, 4 * 1024**2) for name in ARTIFACTS[mode]), "native completion lacks required artifacts")
    return True


def _wait(args, mode: str, receipt: bytes, deadline: float) -> None:
    while time.monotonic() < deadline:
        require(_receipt(args.root) == receipt, "ownership receipt changed while running")
        if _ready(args.root, mode):
            return
        time.sleep(1)
    raise ValueError("native terminal evidence timed out after 600 seconds")


def _stop(args, argv: list[str], receipt: bytes | None) -> bytes:
    actual = _receipt(args.root)  # Missing/reused/changed ownership never licenses a guessed kill.
    require(receipt is None or actual == receipt, "ownership receipt changed; stop refused")
    output = _command(args.root, "stop", argv, 60).decode("utf-8-sig").strip()
    root = re.escape(state.windows(args.root))
    require(re.fullmatch(r"ALREADY_EXITED root=" + root + r"|STOPPED pid=[1-9][0-9]* root=" + root
                         + r"; profile and seal retained", output), "owned stop returned no exact completion")
    state.stopped_source(args.root, args.game)
    require(_receipt(args.root) == actual, "ownership changed across exact stop")
    return actual


def _source_verdict(args, config: dict) -> str:
    observed = []
    for _ in range(2):
        state.stopped_source(args.root, args.game)
        current, facts = state.authenticate_source(args.repo, args.root, config["mode"], config["runtime"], game=args.game)
        require(current == config, "source recipe changed during native run")
        value = (state.capture_donor(args.root, current, facts) if config["mode"] == "source-donor" else
                 state.capture_stage(args.root, current, facts) if config["mode"] == "stage-source" else
                 state.capture(args.root, current, facts)[2])
        observed.append((facts["files"], facts["directories"], facts["localHashes"], facts.get("donorAuthority"), value))
    require(observed[0] == observed[1], "native source evidence changed during final reproof")
    return ("NATIVE SOURCE CAPTURE PASS: mode=" + config["mode"] + "; game-id=" + observed[0][-1]["gameId"]
            + "; candidate=" + args.candidate + "; upgrade-not-yet-proven=true; ordinary-ui-acceptance=false")


def _verdict(args, config: dict) -> str:
    if config["mode"] in ("source", "source-donor", "stage-source"):
        return _source_verdict(args, config)
    spec = importlib.util.spec_from_file_location("taf_upgrade_verdict", Path(__file__).with_name("verify-upgrade-profile.py"))
    require(spec is not None and spec.loader is not None, "terminal verifier unavailable")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.verify(args)


def run(args) -> str:
    config = preflight(args)
    launch, stop = _argv(args), _argv(args, True)  # Resolve helpers before any launch uncertainty.
    fs.write_new(args.root / INTENT, json_bytes(dict(schema="taf-upgrade-run-intent-v1", root=str(args.root), config=config)))
    receipt, error = None, None
    try:
        deadline = time.monotonic() + SECONDS
        _command(args.root, "launch", launch, min(120, SECONDS))
        receipt = _receipt(args.root)
        _wait(args, config["mode"], receipt, deadline)
    except BaseException as caught:
        error = caught
    try:
        receipt = _stop(args, stop, receipt)
    except BaseException as cleanup:
        raise ValueError("owned stop not proved; launch/ownership may be uncertain; original failure="
                         + str(error) + "; stop failure=" + str(cleanup)) from cleanup
    if error is not None:
        raise error
    result = _verdict(args, config)
    require(_receipt(args.root) == receipt, "ownership changed during final evidence validation")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--game", required=True, type=Path)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--source-probe-pin")
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    try:
        print(run(parser.parse_args()))
        return 0
    except (Exception, KeyboardInterrupt) as error:
        print("UNATTENDED CROSS-VERSION RUN REFUSED: " + str(error) + "; all evidence retained", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
