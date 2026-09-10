#!/usr/bin/env python3
"""Compare two retained native journals after each persona's own strict matrix assessment."""
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent / "personas"))
from persona_matrix import read_journal
from persona_travel import compare

if __name__ == "__main__":
    try:
        if len(sys.argv) != 3:
            raise ValueError("usage: compare-travel-personas.py PRESENT-JOURNAL AWAY-JOURNAL")
        rows = []
        for value in sys.argv[1:]:
            with Path(value).open("rb") as stream:
                raw = stream.read(1024 * 1024 + 1)
            if len(raw) > 1024 * 1024:
                raise ValueError("journal exceeds 1 MiB bound")
            rows.append(read_journal(raw.decode("utf-8")))
        print(json.dumps(compare(*rows), sort_keys=True))
    except (OSError, ValueError) as error:
        print("travel comparison refused: " + str(error), file=sys.stderr)
        raise SystemExit(2)
