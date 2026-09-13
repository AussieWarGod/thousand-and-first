#!/usr/bin/env python3
"""Host-only persona verb. A terminal JSON verdict is not a fabricated native journal row."""
import argparse
import ctypes
import ctypes.util
import json
import os
from pathlib import Path
import signal
import tempfile

from persona_reload import NativeBackend, execute, require
from personas.persona_matrix import load

_PR_SET_PDEATHSIG = 1


class ParentDeathSignalUnavailable(RuntimeError):
    """The kernel guarantee that this process is told to stop when its exact-owned parent
    (run-personas.sh) dies could not be armed. Refused outright, never a silent fallback: this
    host has no other route to that guarantee - run-personas.sh's own TERM handling never sends
    this process a raw kill (Tools/tests/scenario_process_source_test.py pins that for the
    whole scenario lifecycle surface) - so proceeding unprotected risks an orphaned process
    still holding a licensed game profile with nothing left to stop it."""


def arm_parent_death_signal():
    """Linux-only: arms PR_SET_PDEATHSIG=SIGTERM so the kernel signals this process the instant
    its direct parent (run-personas.sh) dies, however it dies - a clean SIGTERM relayed through
    bash's own trap, a crash, or a SIGKILL no bash trap could ever observe.
    <para>
    Two races are handled rather than assumed away. The parent may already be gone BEFORE this
    runs at all: run-personas.sh records its own pid in TAF_RELOAD_PARENT_PID before
    backgrounding this process, and that recorded value is compared against os.getppid() before
    any arming is attempted. The parent may also exit DURING the call, in the narrow window
    between that first check and prctl() taking effect: os.getppid() is re-read once more
    immediately afterward. Either mismatch means the original parent is already gone and the
    newly-armed signal now targets whatever reparented us (commonly pid 1), which will never
    deliver it - so a mismatch is treated exactly like an already-received parent-death signal,
    not silently ignored.
    </para>
    <para>
    A missing libc, a failed load, or prctl() itself refusing (EPERM and friends) all raise
    ParentDeathSignalUnavailable rather than continuing unprotected - see its docstring. The
    explicit SIGTERM handler registered by main() before this is called is what actually runs
    cleanup in every case (a delivered parent-death signal is indistinguishable from an
    ordinary SIGTERM once the handler fires); this function only decides whether it is safe to
    proceed at all.
    </para>
    """
    recorded_parent = os.environ.get("TAF_RELOAD_PARENT_PID")
    expected_parent = int(recorded_parent) if recorded_parent else os.getppid()
    if os.getppid() != expected_parent:
        refuse_as_orphaned("orphaned before the parent-death signal could be armed")
        return
    library = ctypes.util.find_library("c")
    if not library:
        raise ParentDeathSignalUnavailable("libc not found; cannot arm PR_SET_PDEATHSIG")
    try:
        handle = ctypes.CDLL(library, use_errno=True)
    except OSError as error:
        raise ParentDeathSignalUnavailable("libc could not be loaded: " + str(error)) from error
    ctypes.set_errno(0)
    if handle.prctl(_PR_SET_PDEATHSIG, signal.SIGTERM, 0, 0, 0) != 0:
        code = ctypes.get_errno()
        raise ParentDeathSignalUnavailable(
            "prctl(PR_SET_PDEATHSIG) refused: errno %d (%s)" % (code, os.strerror(code)))
    if os.getppid() != expected_parent:
        refuse_as_orphaned("orphaned while the parent-death signal was being armed")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("persona", type=Path)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    evidence = None
    try:
        signal.signal(signal.SIGTERM, interrupted)
        arm_parent_death_signal()
        manifest, _ = load(str(args.persona))
        require(manifest.get("RELOAD") == "quickstart", "not a cold-reload persona")
        require(not os.environ.get("TAF_PERSONA_CAPTURE_DIR"),
                "reload persona has no screenshot contract; omit TAF_PERSONA_CAPTURE_DIR")
        require(args.game.is_file(), "configured game executable is missing")
        timeout = os.environ.get("TAF_PERSONA_TIMEOUT", manifest["TIMEOUT"])
        require(timeout.isascii() and timeout.isdecimal() and 1 <= int(timeout) <= 3600,
                "reload timeout must be 1..3600 seconds")
        args.report_dir.mkdir(parents=True, exist_ok=True)
        evidence = Path(tempfile.mkdtemp(prefix="reload-", dir=args.report_dir))
        _, location, advisor = manifest["SCRIPT_WORDS"].split()
        backend = NativeBackend(Path(__file__).resolve().parent, args.game.resolve(), evidence,
                                int(timeout), os.environ.get("TAF_PERSONA_SEED", ""))
        result = execute(backend, location, advisor)
        result["evidence"] = str(evidence)
        print(json.dumps(result, sort_keys=True))
        return 0
    except (Exception, KeyboardInterrupt) as error:
        print(json.dumps(dict(verdict="REFUSED", reason=str(error), evidence=str(evidence),
                              releaseAcceptance=False, ordinaryAcceptance=False)))
        return 1


def interrupted(*_signal_args):
    """The registered SIGTERM handler. Two argv per the signal module contract
    (signum, frame); both are ignored - only that a signal arrived matters."""
    raise KeyboardInterrupt("terminated")


def refuse_as_orphaned(reason):
    """Called directly (never as a signal handler) when arm_parent_death_signal() itself
    detects the parent is already gone. Same outcome as interrupted() - a KeyboardInterrupt
    main() catches and reports as REFUSED - but with a reason that names which race fired,
    since no actual signal was delivered to explain itself."""
    raise KeyboardInterrupt(reason)


if __name__ == "__main__":
    raise SystemExit(main())
