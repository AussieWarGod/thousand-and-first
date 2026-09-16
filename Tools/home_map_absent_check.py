#!/usr/bin/env python3
"""Judge a real two-process absent-home chain; profile, save and owned-stop proofs stay separate."""
import argparse
import json
from pathlib import Path
import re
from guest_save_check import one, require
from home_map_save_check import field, visit
from personas import persona_matrix


def judge(source, loaded):
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == "SCRIPT-COMPLETE", "session did not complete")
        require(all(outcome == "OK" for _, outcome, _ in rows), "session contains a refusal")
        one(rows, "SCRIPT-COMPLETE")
    initial, initial_visit = one(source, "home-map-native")
    grown, _ = one(source, "lifecycle-grown")
    witness, written = one(source, "home-map-absent-save-witness")
    save, saved = one(source, "lifecycle-save")
    require(initial < grown < witness < save and field(saved, "real-save") == "true", "departure/save order differs")
    before, pre = one(loaded, "home-map-absent-preactivation")
    after, post = one(loaded, "home-map-absent-loaded")
    returned, back = one(loaded, "home-map-absent-return")
    life, _ = one(loaded, "lifecycle-loaded")
    next_action, _ = one(loaded, "lifecycle-next")
    require(before < after < returned < life < next_action, "absent cold phases out of order")
    identity = visit(initial_visit, False)
    digest = field(written, "receipt-sha256")
    require(re.fullmatch(r"[0-9a-f]{64}", digest), "invalid absent witness hash")
    for detail in (written, pre, post, back):
        for key, value in (("residents", "4"), ("maps", "2"), ("world-repair", "false"), ("receipt-sha256", digest)):
            require(field(detail, key) == value, "absent witness changed: " + key)
        for key in ("home-zone", "away-zone", "body", "resident", "original-home", "plot"):
            require(field(detail, key) == identity[key], "absent traveler identity changed: " + key)
        require(field(detail, "occupied") == identity["occupied-before"], "absent capacity changed")
    for detail in (written, pre, post):
        require(field(detail, "local") == "3" and field(detail, "away") == "1", "citizen not absent at save/load boundary")
    require(field(back, "local") == "4" and field(back, "away") == "0", "citizen not returned")
    for detail, key in ((written, "saved-away"), (written, "synthetic-transfer"), (pre, "before-AfterGameLoaded"),
                        (pre, "before-remote-lookup"), (post, "before-remote-lookup"), (post, "capacity-retained"),
                        (back, "exact-body"), (back, "return-settlement-pass"), (back, "capacity-retained")):
        require(field(detail, key) == "true", "absent invariant failed: " + key)
    require(field(written, "witness") == "recorded", "source witness absent")
    return dict(verdict="PASS", resident=identity["resident"], body=identity["body"], receiptSha256=digest,
                scope="save while original citizen is away; exact authority before/after activation; capacity before remote lookup; controlled return, not ordinary walking")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("loaded", type=Path)
    parser.add_argument("--results", type=Path)
    args = parser.parse_args()
    try:
        result = judge(*(persona_matrix.read_journal(p.read_text(encoding="utf-8-sig")) for p in (args.source, args.loaded)))
    except (OSError, ValueError) as error:
        print(json.dumps(dict(verdict="FAIL", failure=str(error))))
        return 1
    if args.results: args.results.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
