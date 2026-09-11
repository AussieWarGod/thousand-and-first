#!/usr/bin/env python3
"""The run record: what a scenario SESSION can honestly say about itself.

A long-form release artefact needs facts no journal carries -- which tree was exercised, which
process ran it, when it started and stopped, and what budget it was given. This module writes
those facts down at the moment they are true, in three separate acts:

    seal    at profile preparation: the exercised commit, the production structural digest
            COMPUTED from the frozen tree, the digest of the closed profile seal actually
            launched, the frozen seed, the sealed script, the role and the budgets.
    launch  at process start: the launch identity and the UTC start.
    stop    after the process has ended: the UTC stop and the exit code, plus the game build
            string if the run's own Player.log states one.

Nothing here measures gameplay. Turns and elapsed time per step are derived by
Tools/check-quickstart-lifecycle.py from the journal the run itself wrote, so a budget can never
become a measurement: this file records only the budget it was given.

WHY THE REVIEW LEDGER IS NOT A SOURCE. docs/STRUCTURE_REVIEW.json is a REVIEW record, and an
active candidate deliberately keeps it stale until the review happens; reading it here would
label a native run with the previous release's digest. The exercised bytes are therefore measured
directly: Tools/check-structure.py --json over the frozen tree. Structural-review freshness
remains a separate release requirement and is never a source for what was actually run.

THREE DIGESTS, NEVER CONFLATED, EACH SEPARATELY NAMED.

  runtimeInventorySha256  the PRODUCTION structural digest of the frozen source tree, measured
                          by Tools/check-structure.py --json. Shared by both sessions, because
                          both exercised the same source.
  harnessInventorySha256  the dev-harness inventory of the staged tree, when the caller can
                          state it. It is NOT derived here: no tool in this repository measures
                          it yet, and inventing one would be worse than leaving it out.
  profileSeal             this SESSION's own closed profile seal (<root>.seal/profile.sha256,
                          header taf-scenario-profile-seal-v1), with profileName beside it. The
                          save session and the cold-load session legitimately differ -- load
                          authority, script, import metadata -- so this belongs to the session
                          and is never hoisted to a single value expected to match across both,
                          or to match the public package.

Fail-closed by design. An unreadable tree, a digest that is not a lowercase SHA-256, a missing
profile seal, a non-positive budget -- each refuses the seal rather than writing a record with a
plausible-looking hole in it.
"""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True

ROLES = ("save-session", "cold-load-session")
SHA256 = re.compile(r"[0-9a-f]{64}")
COMMIT = re.compile(r"[0-9a-f]{40}")
BUILD = re.compile(r"\b(\d+\.\d+\.\d+\.\d+)\b")
FILE_NAME = "run-record.json"
MAX_LOG_BYTES = 64 * 1024 * 1024


def fail(message: str) -> None:
    raise SystemExit("run record refused: " + message)


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def record_path(root: str) -> Path:
    return Path(root) / FILE_NAME


def read(root: str) -> dict:
    path = record_path(root)
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        fail("cannot read %s (%s)" % (path, type(error).__name__))
    if not isinstance(payload, dict):
        fail("%s is not a JSON object" % path)
    return payload


def write(root: str, payload: dict) -> None:
    """Whole-file replacement, so a half-written record can never be read as a whole one."""
    path = record_path(root)
    temporary = path.with_suffix(".json.partial")
    temporary.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def head_commit(tree: str) -> str:
    try:
        result = subprocess.run(
            ["git", "-C", tree, "rev-parse", "HEAD"],
            check=True, capture_output=True, text=True, timeout=60,
        )
    except (OSError, subprocess.SubprocessError) as error:
        fail("cannot read the exercised tree's commit (%s)" % type(error).__name__)
    commit = result.stdout.strip()
    if not COMMIT.fullmatch(commit):
        fail("the exercised tree's HEAD is not a full lowercase commit id")
    return commit


def inventory_digest(tree: str) -> str:
    """The production structural digest MEASURED from the frozen tree, never read from a ledger."""
    checker = Path(tree) / "Tools" / "check-structure.py"
    if not checker.is_file():
        fail("the exercised tree carries no Tools/check-structure.py to measure")
    try:
        result = subprocess.run(
            [sys.executable, str(checker), "--json"],
            check=True, capture_output=True, text=True, timeout=900, cwd=tree,
        )
        payload = json.loads(result.stdout)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        fail("cannot measure the exercised tree's structure (%s)" % type(error).__name__)
    digest = payload.get("inventorySha256") if isinstance(payload, dict) else None
    if not isinstance(digest, str) or not SHA256.fullmatch(digest):
        fail("Tools/check-structure.py reported no lowercase inventorySha256")
    return digest


def profile_seal_digest(root: str) -> str:
    """The SHA-256 of the closed profile seal this session launched.

    The seal file is the existing identifier of what was staged -- every profile file and its
    hash, under the taf-scenario-profile-seal-v1 header -- so the record names the dev profile by
    the artefact the preparation step already wrote and printed, rather than by a new invention.
    """
    seal = Path(str(root).rstrip("/") + ".seal") / "profile.sha256"
    try:
        data = seal.read_bytes()
    except OSError as error:
        fail("cannot read the closed profile seal (%s)" % type(error).__name__)
    if not data.startswith(b"taf-scenario-profile-seal-v1"):
        fail("the profile seal does not carry its own header")
    return __import__("hashlib").sha256(data).hexdigest()


def game_build(log: Path) -> str | None:
    """The game build string the run's own log states, or None. Never guessed."""
    try:
        if not log.is_file() or log.stat().st_size > MAX_LOG_BYTES:
            return None
        text = log.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    for line in text.splitlines():
        if "version" not in line.lower() and "build" not in line.lower():
            continue
        found = BUILD.search(line)
        if found:
            return found.group(1)
    return None


def seal(arguments: argparse.Namespace) -> int:
    if arguments.role not in ROLES:
        fail("role must be one of " + ", ".join(ROLES))
    root = Path(arguments.root)
    if not root.is_dir():
        fail("scenario root %s is not a directory" % root)
    if record_path(arguments.root).exists():
        fail("this scenario root already carries a run record")
    if arguments.turn_budget <= 0 or arguments.timeout_seconds <= 0:
        fail("turn budget and timeout must both be positive")
    payload = {
        "role": arguments.role,
        "root": str(root),
        "seed": arguments.seed,
        "script": arguments.script,
        "runtimeInventorySha256": inventory_digest(arguments.tree),
        "candidateCommit": head_commit(arguments.tree),
        "profileSeal": profile_seal_digest(arguments.root),
        "profileName": arguments.profile_name or Path(arguments.root).name,
        "turnBudget": arguments.turn_budget,
        "timeoutSeconds": arguments.timeout_seconds,
        "sealedUtc": utc_now(),
    }
    if arguments.harness_inventory_sha256:
        if not SHA256.fullmatch(arguments.harness_inventory_sha256):
            fail("harness inventory digest must be a lowercase SHA-256")
        payload["harnessInventorySha256"] = arguments.harness_inventory_sha256
    write(arguments.root, payload)
    print("sealed run record: " + str(record_path(arguments.root)))
    return 0


def launch(arguments: argparse.Namespace) -> int:
    payload = read(arguments.root)
    if payload.get("launchId"):
        fail("this run record already names a launch")
    if not arguments.launch_id:
        fail("a launch needs its own identity")
    payload["launchId"] = arguments.launch_id
    payload["started"] = arguments.started or utc_now()
    write(arguments.root, payload)
    print("run record launch: " + payload["launchId"] + " at " + payload["started"])
    return 0


def stop(arguments: argparse.Namespace) -> int:
    payload = read(arguments.root)
    if not payload.get("launchId"):
        fail("this run record names no launch to stop")
    if payload.get("stoppedUtc"):
        fail("this run record is already stopped")
    payload["stoppedUtc"] = arguments.stopped or utc_now()
    payload["exitCode"] = arguments.exit_code
    build = game_build(Path(arguments.root) / "Player.log")
    if build:
        payload["gameBuildId"] = build
    write(arguments.root, payload)
    print("run record stopped: " + payload["stoppedUtc"])
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="write a scenario session's run record")
    actions = parser.add_subparsers(dest="action", required=True)
    sealer = actions.add_parser("seal")
    sealer.add_argument("root")
    sealer.add_argument("--tree", required=True)
    sealer.add_argument("--role", required=True)
    sealer.add_argument("--seed", required=True)
    sealer.add_argument("--script", default="")
    sealer.add_argument("--turn-budget", type=int, required=True)
    sealer.add_argument("--timeout-seconds", type=int, required=True)
    sealer.add_argument("--profile-name", default="")
    sealer.add_argument("--harness-inventory-sha256", default="")
    sealer.set_defaults(handler=seal)
    launcher = actions.add_parser("launch")
    launcher.add_argument("root")
    launcher.add_argument("--launch-id", required=True)
    launcher.add_argument("--started", default="")
    launcher.set_defaults(handler=launch)
    stopper = actions.add_parser("stop")
    stopper.add_argument("root")
    stopper.add_argument("--stopped", default="")
    stopper.add_argument("--exit-code", type=int, default=0)
    stopper.set_defaults(handler=stop)
    parsed = parser.parse_args(argv[1:])
    return parsed.handler(parsed)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
