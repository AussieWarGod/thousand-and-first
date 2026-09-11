#!/usr/bin/env python3
"""The run record: what a scenario SESSION can honestly say about itself.

A long-form release artefact needs facts no journal carries -- which tree was exercised, which
process ran it, when it started and stopped, and what budget it was given. This module writes
those facts down at the moment they are true, in three separate acts:

    seal    at profile preparation: the exercised commit, the production structural digest
            COMPUTED from the frozen tree, the digest of the closed profile seal actually
            launched, the frozen seed, the sealed script, the role and the budgets.
    launch  at process start: the OWNED process's own identity -- the launch id, the UTC start,
            and an ownership block bound to the launcher's process-ownership.json receipt (pid,
            start ticks, executable, and that receipt's SHA-256).
    stop    after the OWNED process has ended: the UTC stop, the provenance of that ending, the
            exit code only when it was actually observed, and the game build string if the run's
            own Player.log states one.

EXIT PROVENANCE, NOT A DEFAULT. An exit code is evidence about one specific process. This module
will not write one that nobody watched: `--exit-code` is accepted only together with
`--exit-observed`, which asserts the caller held the owned process object and read its exit. When
no one watched, `exitProvenance` says so and no exitCode is written at all -- a default zero would
be a claim that the game ended cleanly, made by a wrapper that never saw it end. The stop half also
refuses unless the launch half recorded an ownership block, so an exit can always be bound back to
the pid and start ticks the launcher itself owned.

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


def ownership(root: str, receipt: str) -> dict:
    """The launcher's own ownership receipt, bound by its bytes.

    process-ownership.json is written CreateNew by Start-TafOwnedScenarioProcess for the exact
    process it started (schema, root, pid, startTicks, executable, arguments). Recording its pid
    and start ticks beside its SHA-256 is what later lets an exit be bound to that process rather
    than to a wrapper shell or to whoever happened to call stop.
    """
    path = Path(receipt)
    try:
        data = path.read_bytes()
        payload = json.loads(data.decode("utf-8"))
    except (OSError, UnicodeError, ValueError) as error:
        fail("cannot read the launcher's ownership receipt (%s)" % type(error).__name__)
    if not isinstance(payload, dict):
        fail("the ownership receipt is not a JSON object")
    for field in ("pid", "startTicks", "executable"):
        if field not in payload:
            fail("the ownership receipt carries no " + field)
    if type(payload["pid"]) is not int or payload["pid"] <= 0:
        fail("the ownership receipt has no positive integer pid")
    if not isinstance(payload["startTicks"], str) or not payload["startTicks"].isdigit():
        fail("the ownership receipt has malformed start ticks")
    return {
        "receiptRef": path.name,
        "receiptSha256": __import__("hashlib").sha256(data).hexdigest(),
        "pid": payload["pid"],
        "startTicks": payload["startTicks"],
        "executable": payload["executable"],
    }


def seal_load(arguments: argparse.Namespace) -> int:
    """Seal the COLD-LOAD session's record from the save session's own record.

    The cold-load profile is a copy: it exercises the same source tree as the session that wrote
    the save, so its candidateCommit and runtimeInventorySha256 must be the ones that session
    recorded -- read from <source>/run-record.json, never re-measured from whatever happens to be
    checked out now, which is exactly where drift would enter. Its profile seal, by contrast, is
    its OWN: the copier writes a different closed seal for the destination, because that profile
    carries the load request the source never had.

    With --tree, the inherited digest is re-verified against that frozen tree and a mismatch
    refuses rather than being written; without it, the digest is inherited and labelled as such.
    """
    if arguments.root == arguments.source:
        fail("the cold-load profile cannot be its own source")
    source = read(arguments.source)
    if source.get("role") != ROLES[0]:
        fail("the source record is not a %s record" % ROLES[0])
    for field in ("candidateCommit", "runtimeInventorySha256", "seed"):
        if not source.get(field):
            fail("the source record carries no " + field)
    commit = source["candidateCommit"]
    digest = source["runtimeInventorySha256"]
    if not COMMIT.fullmatch(str(commit)) or not SHA256.fullmatch(str(digest)):
        fail("the source record's commit or inventory digest is malformed")
    if arguments.tree:
        measured = inventory_digest(arguments.tree)
        if measured != digest:
            fail("the frozen tree measures %s, not the source record's %s" % (measured, digest))
        head = head_commit(arguments.tree)
        if head != commit:
            fail("the frozen tree is at %s, not the source record's %s" % (head, commit))
    root = Path(arguments.root)
    if not root.is_dir():
        fail("cold-load root %s is not a directory" % root)
    if record_path(arguments.root).exists():
        fail("this cold-load root already carries a run record")
    if arguments.turn_budget <= 0 or arguments.timeout_seconds <= 0:
        fail("turn budget and timeout must both be positive")
    payload = {
        "role": ROLES[1],
        "root": str(root),
        "seed": source["seed"],
        "script": arguments.script or load_script(arguments.root),
        "runtimeInventorySha256": digest,
        "candidateCommit": commit,
        "profileSeal": profile_seal_digest(arguments.root),
        "profileName": arguments.profile_name or root.name,
        "turnBudget": arguments.turn_budget,
        "timeoutSeconds": arguments.timeout_seconds,
        "sealedUtc": utc_now(),
        "inheritedFrom": str(Path(arguments.source).name),
        "inheritedVerified": bool(arguments.tree),
    }
    if source.get("harnessInventorySha256"):
        payload["harnessInventorySha256"] = source["harnessInventorySha256"]
    write(arguments.root, payload)
    print("sealed cold-load run record: " + str(record_path(arguments.root)))
    return 0


def load_script(root: str) -> str:
    """The script the copied profile actually carries, read from its own sealed Local."""
    path = Path(root) / "Local" / "scenario-script.txt"
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as error:
        fail("cannot read the cold-load profile's sealed script (%s)" % type(error).__name__)
    return " ".join(
        line.strip() for line in text.splitlines()
        if line.strip() and not line.strip().startswith("#")
    )


def launch(arguments: argparse.Namespace) -> int:
    payload = read(arguments.root)
    if payload.get("launchId"):
        fail("this run record already names a launch")
    if not arguments.launch_id:
        fail("a launch needs its own identity")
    owned = ownership(arguments.root, arguments.ownership)
    if ("-" + str(owned["pid"]) + "-") not in arguments.launch_id:
        fail("the launch identity does not name the owned process's pid")
    payload["launchId"] = arguments.launch_id
    payload["started"] = arguments.started or utc_now()
    payload["ownership"] = owned
    payload["exitProvenance"] = "unobserved"
    write(arguments.root, payload)
    print("run record launch: " + payload["launchId"] + " at " + payload["started"]
          + " owning pid " + str(owned["pid"]))
    return 0


def stop(arguments: argparse.Namespace) -> int:
    payload = read(arguments.root)
    if not payload.get("launchId"):
        fail("this run record names no launch to stop")
    if payload.get("stoppedUtc"):
        fail("this run record is already stopped")
    owned = payload.get("ownership")
    if not isinstance(owned, dict) or type(owned.get("pid")) is not int:
        fail("this run record has no ownership block, so an exit could not be bound to the "
             "process the launcher owned")
    if arguments.exit_code is not None and not arguments.exit_observed:
        fail("an exit code may only be recorded with --exit-observed: nobody may report an exit "
             "they did not watch")
    payload["stoppedUtc"] = arguments.stopped or utc_now()
    if arguments.exit_observed:
        if arguments.exit_code is None:
            fail("--exit-observed needs the exit code that was observed")
        payload["exitCode"] = arguments.exit_code
        payload["exitProvenance"] = "owned-process-exit-observed"
    else:
        payload.pop("exitCode", None)
        payload["exitProvenance"] = "owned-process-ended-exit-unobserved"
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
    loader = actions.add_parser("seal-load")
    loader.add_argument("root")
    loader.add_argument("--source", required=True)
    loader.add_argument("--tree", default="")
    loader.add_argument("--script", default="")
    loader.add_argument("--profile-name", default="")
    loader.add_argument("--turn-budget", type=int, required=True)
    loader.add_argument("--timeout-seconds", type=int, required=True)
    loader.set_defaults(handler=seal_load)
    launcher = actions.add_parser("launch")
    launcher.add_argument("root")
    launcher.add_argument("--launch-id", required=True)
    launcher.add_argument("--started", default="")
    launcher.add_argument("--ownership", required=True)
    launcher.set_defaults(handler=launch)
    stopper = actions.add_parser("stop")
    stopper.add_argument("root")
    stopper.add_argument("--stopped", default="")
    stopper.add_argument("--exit-code", type=int, default=None)
    stopper.add_argument("--exit-observed", action="store_true")
    stopper.set_defaults(handler=stop)
    parsed = parser.parse_args(argv[1:])
    return parsed.handler(parsed)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
