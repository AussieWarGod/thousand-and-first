#!/usr/bin/env python3
"""Guest-specific journal oracle; pair with check-quickstart-lifecycle.py and strict logs."""
import argparse
import json
from pathlib import Path
import re

from personas import persona_matrix


def require(value, reason):
    if not value:
        raise ValueError(reason)


def one(rows, name):
    matches = [(i, detail) for i, (event, outcome, detail) in enumerate(rows)
               if event == name and outcome == "OK"]
    require(len(matches) == 1 and sum(event == name for event, _, _ in rows) == 1,
            "missing, failed or repeated " + name)
    return matches[0]


def field(detail, name):
    found = re.findall(r"(?:^|;\s*)" + re.escape(name) + r"=([^;\s]+)(?=;|\s|$)", detail)
    require(len(found) == 1, "missing or repeated field " + name)
    return found[0]


def judge(source, loaded):
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == "SCRIPT-COMPLETE", "session did not complete")
        require(all(outcome == "OK" for _, outcome, _ in rows), "session contains a refusal")
    action, _ = one(source, "guest-actions-check")
    witness, written = one(source, "guest-save-witness")
    save, _ = one(source, "lifecycle-save")
    require(action < witness < save, "save witness is not after recruitment and before save")
    before, pre = one(loaded, "guest-load-preactivation")
    after, post = one(loaded, "guest-load-verified")
    life, _ = one(loaded, "lifecycle-loaded")
    next_action, _ = one(loaded, "lifecycle-next")
    require(before < after < life < next_action, "guest load phases are out of order")
    require(field(pre, "exact-guest-authority") == "true" and field(pre, "before-AfterGameLoaded") == "true",
            "preactivation authority was not verified")
    for name, value in (("same-citizen", "true"), ("stale-choice-refused", "2"),
                        ("duplicate-enrollment", "false"), ("extra-water-debit", "false")):
        require(field(post, name) == value, "guest retry invariant failed: " + name)
    for detail in (written, pre, post):
        require(field(detail, "world-repair") == "false", "witness repaired the world")
    values = {name: field(written, name) for name in ("guest", "population", "receipt-sha256")}
    require(values["population"].isascii() and values["population"].isdigit()
            and int(values["population"]) >= 5, "guest population is invalid")
    require(re.fullmatch(r"[0-9a-f]{64}", values["receipt-sha256"]), "guest digest is invalid")
    for detail in (pre, post):
        require(all(field(detail, name) == value for name, value in values.items()),
                "guest identity, population or saved authority changed across load")
    return {"verdict": "PASS", "guestId": values["guest"], "population": int(values["population"]),
            "snapshotSha256": values["receipt-sha256"], "staleActionRefusals": 2,
            "scope": "guest journal assertions only; require lifecycle result, source binding, strict logs and owned stops"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("loaded", type=Path)
    parser.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = judge(*(persona_matrix.read_journal(p.read_text(encoding="utf-8-sig"))
                         for p in (args.source, args.loaded)))
    except (OSError, ValueError) as error:
        result = {"verdict": "FAIL", "reason": str(error)}
    args.results.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))
    return 0 if result["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
