#!/usr/bin/env python3
"""Check the two native home-save journals; ownership/recipe/strict logs remain separate proofs."""
import argparse
import json
from pathlib import Path
import re
from guest_save_check import field, one, require
from personas import persona_matrix


def visit(detail, reused):
    prefix = "native-home-map cases=1 passed=1 failed=0; "
    require(detail.startswith(prefix), "native home visit did not pass")
    detail = detail[len(prefix):]
    for key, value in (("physical-return", "true"), ("home-repair", "false"),
                       ("return-settlement-pass", "true"), ("reservation-owner-exact", "true"),
                       ("synthetic-housing", "false"), ("active-away", "true"),
                       ("claim-reused", "true" if reused else "false")):
        require(field(detail, key) == value, "home visit invariant failed: " + key)
    before = field(detail, "occupied-before")
    require(re.fullmatch(r"[1-9][0-9]*", before) and field(detail, "occupied-away") == before,
            "absent owner released reserved capacity")
    require(field(detail, "original-home") == field(detail, "visited-home")
            and re.fullmatch(r"[1-9][0-9]*", field(detail, "original-home")), "home root changed or absent")
    require(field(detail, "plot") == field(detail, "visited-plot"), "home plot changed")
    require(field(detail, "home-zone") != field(detail, "away-zone")
            and field(detail, "visited-bound") == field(detail, "away-zone"), "actual visiting map differs")
    return {key: field(detail, key) for key in ("home-zone", "away-zone", "resident", "body", "original-home", "plot", "occupied-before")}


def judge(source, loaded):
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == "SCRIPT-COMPLETE", "session did not complete")
        require(all(outcome == "OK" for _, outcome, _ in rows), "session contains a refusal")
        one(rows, "SCRIPT-COMPLETE")
    warm, warm_visit = one(source, "home-map-native")
    witness, written = one(source, "home-map-save-witness")
    save, saved = one(source, "lifecycle-save")
    require(warm < witness < save and field(saved, "real-save") == "true", "visit/witness/save order differs")
    before, pre = one(loaded, "home-map-load-preactivation")
    after, post = one(loaded, "home-map-load-verified")
    travel, cold_visit = one(loaded, "home-map-load-travel")
    life, _ = one(loaded, "lifecycle-loaded")
    next_action, _ = one(loaded, "lifecycle-next")
    require(before < after < travel < life < next_action, "cold home phases are out of order")
    require(field(pre, "before-AfterGameLoaded") == "true" and field(written, "witness") == "recorded",
            "original and preactivation witnesses absent")
    digest = field(written, "receipt-sha256")
    require(re.fullmatch(r"[0-9a-f]{64}", digest), "invalid home witness hash")
    for detail in (written, pre, post):
        for key, value in (("residents", "4"), ("maps", "2"), ("exact-homes", "true"),
                           ("world-repair", "false"), ("receipt-sha256", digest)):
            require(field(detail, key) == value, "home witness changed: " + key)
    warm_identity, cold_identity = visit(warm_visit, False), visit(cold_visit, True)
    require(warm_identity == cold_identity, "cold visit used different bodies, homes, maps or capacity")
    return dict(verdict="PASS", resident=warm_identity["resident"], body=warm_identity["body"],
                home=warm_identity["plot"], receiptSha256=digest,
                scope="four founder homes before/after activation and controlled visiting/return after cold load; no ordinary walking or save while absent")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("loaded", type=Path)
    parser.add_argument("--results", type=Path)
    args = parser.parse_args()
    try:
        result = judge(*(persona_matrix.read_journal(p.read_text(encoding="utf-8-sig"))
                         for p in (args.source, args.loaded)))
    except (OSError, ValueError) as error:
        print(json.dumps(dict(verdict="FAIL", failure=str(error))))
        return 1
    if args.results: args.results.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
