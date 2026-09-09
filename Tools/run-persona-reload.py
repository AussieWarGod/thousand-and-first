#!/usr/bin/env python3
"""Host-only persona verb. A terminal JSON verdict is not a fabricated native journal row."""
import argparse
import json
import os
from pathlib import Path
import signal
import tempfile

from persona_reload import NativeBackend, execute, require
from personas.persona_matrix import load


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("persona", type=Path)
    parser.add_argument("--game", type=Path, required=True)
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    evidence = None
    try:
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


def interrupted(*_):
    raise KeyboardInterrupt("terminated")


if __name__ == "__main__":
    signal.signal(signal.SIGTERM, interrupted)
    raise SystemExit(main())
