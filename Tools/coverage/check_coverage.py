#!/usr/bin/env python3
"""Machine-checkable behavioural coverage matrix.

Validates Tools/coverage/matrix.json against a small schema and (optionally)
regenerates docs/BEHAVIOUR_COVERAGE.md from it. Every status token is closed:
an unknown token, or a PASS-shaped status with no TYPED evidence, is a hard
error, never silently accepted. Counts in the generated markdown are always
derived from this data, never typed by hand.

Evidence is a TYPED object, never a bare string: a placeholder like
"prior-status-md-rows-not-rerun" or "owned-lane" cannot stand in for a real
receipt. Each PASS-shaped row carries a non-empty list of evidence entries,
each with: commit, evidenceDir, artifactRef, verdict, scope, historicalScope
(bool: was this receipt captured on a different commit/branch than the one
cited?), currentDevCoverage (bool: was content-identity actually checked
against a current-dev-ancestor commit, e.g. via `git diff`?).
"""

import argparse
import json
import os
import sys

STATUS_TOKENS = {
    "NONE",
    "IMPLEMENTED_UNEXECUTED",
    "NATIVE_PASS",
    "NEGATIVE_PASS",
    "COVERAGE_GAP",
    "DEFECT",
    "EVIDENCE_UNVERIFIED",
}
PASS_STATUSES = {"NATIVE_PASS", "NEGATIVE_PASS"}
REQUIRED_ROW_KEYS = (
    "id",
    "behaviour",
    "prerequisites",
    "transitions",
    "effects",
    "save_cold_load",
    "negative",
    "driver",
    "status",
    "evidence",
    "note",
)
EVIDENCE_KEYS = (
    "commit",
    "evidenceDir",
    "artifactRef",
    "verdict",
    "scope",
    "historicalScope",
    "currentDevCoverage",
    "inventoryDigest",
)
# The current dev worktree's own structural inventory digest, from
# `python3 Tools/dev-harness-inventory.py --inventory-digest`. currentDevCoverage may only be
# true when an evidence entry's own inventoryDigest equals this -- i.e. the ENTIRE exercised
# runtime+harness inventory matched, never inferred from one provider file's content-identity
# or from commit ancestry.
CURRENT_DEV_DIGEST = "026de326a6506f4eba36b361c82c68dafe7c8384aecf938e7043313446fb891d"
import re as _re
_ARTIFACT_REF_PATTERN = _re.compile(r"^(/|[A-Za-z]:[\\/]|\.{1,2}/|[A-Za-z0-9_.-]+[/\\])")
# Vague placeholder tokens that must never stand in for typed evidence.
BANNED_EVIDENCE_SUBSTRINGS = ("prior-status", "owned-lane", "not-rerun")

COMBINATION_STATUS_TOKENS = {"NONE", "IMPLEMENTED_UNEXECUTED", "NATIVE_PASS", "NEGATIVE_PASS"}
COMBINATION_PASS_STATUSES = {"NATIVE_PASS", "NEGATIVE_PASS"}
REQUIRED_COMBINATION_KEYS = (
    "id",
    "name",
    "rows",
    "coupling",
    "prerequisites",
    "invariants",
    "seed_turn_matrix",
    "expected_failure_rows",
    "reusable_seams",
    "status",
    "evidence",
)


def _string_fields(value):
    """Yields every string found anywhere inside a JSON-shaped value."""
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for v in value.values():
            for s in _string_fields(v):
                yield s
    elif isinstance(value, list):
        for item in value:
            for s in _string_fields(item):
                yield s


def _validate_evidence_entry(row_id, entry, problems):
    if not isinstance(entry, dict):
        problems.append("row %r has a non-object evidence entry" % (row_id,))
        return
    missing = [k for k in EVIDENCE_KEYS if k not in entry]
    if missing:
        problems.append(
            "row %r evidence entry is missing keys: %s" % (row_id, missing)
        )
        return
    if not isinstance(entry["historicalScope"], bool) or not isinstance(
        entry["currentDevCoverage"], bool
    ):
        problems.append(
            "row %r evidence entry's historicalScope/currentDevCoverage must be bool"
            % (row_id,)
        )
    for field in ("commit", "artifactRef", "verdict", "scope"):
        v = entry.get(field)
        if not isinstance(v, str) or not v.strip():
            problems.append(
                "row %r evidence entry field %r must be a non-empty string"
                % (row_id, field)
            )
    ev_dir = entry.get("evidenceDir")
    if ev_dir is not None and (not isinstance(ev_dir, str) or not ev_dir.strip()):
        problems.append(
            "row %r evidence entry's evidenceDir must be null or a non-empty string"
            % (row_id,)
        )
    digest = entry.get("inventoryDigest")
    if digest is not None and (not isinstance(digest, str) or not digest.strip()):
        problems.append(
            "row %r evidence entry's inventoryDigest must be null or a non-empty string"
            % (row_id,)
        )
    if entry.get("currentDevCoverage") is True and digest != CURRENT_DEV_DIGEST:
        problems.append(
            "row %r claims currentDevCoverage=true but inventoryDigest %r does not equal "
            "the current dev digest %r -- currentDevCoverage requires whole-inventory "
            "identity, not commit ancestry or one file's content-identity"
            % (row_id, digest, CURRENT_DEV_DIGEST)
        )
    artifact_ref = entry.get("artifactRef")
    if isinstance(artifact_ref, str) and artifact_ref.strip():
        if not _ARTIFACT_REF_PATTERN.match(artifact_ref):
            problems.append(
                "row %r evidence entry's artifactRef %r does not look like a path "
                "(a bare description like \"build journal (COMPLETE OK)\" is not a ref)"
                % (row_id, artifact_ref)
            )
        elif artifact_ref.startswith("/") or _re.match(r"^[A-Za-z]:[\\/]", artifact_ref):
            if not os.path.exists(artifact_ref):
                problems.append(
                    "row %r evidence entry's artifactRef %r is an absolute path that does "
                    "not exist on this disk" % (row_id, artifact_ref)
                )


def validate(doc):
    """Returns a list of problem strings; empty means the document is valid."""
    problems = []
    if not isinstance(doc, dict) or doc.get("schemaVersion") != 2:
        problems.append("missing or wrong schemaVersion (expected 2, typed evidence)")
        return problems
    rows = doc.get("rows")
    if not isinstance(rows, list) or not rows:
        problems.append("rows must be a non-empty list")
        return problems
    seen_ids = set()
    for row in rows:
        if not isinstance(row, dict):
            problems.append("a row is not an object")
            continue
        missing = [k for k in REQUIRED_ROW_KEYS if k not in row]
        if missing:
            problems.append(
                "row %r is missing keys: %s" % (row.get("id", "?"), missing)
            )
            continue
        row_id = row["id"]
        if row_id in seen_ids:
            problems.append("duplicate row id %r" % (row_id,))
        seen_ids.add(row_id)
        status = row["status"]
        evidence = row["evidence"]
        if status not in STATUS_TOKENS:
            problems.append("row %r has an unknown status token %r" % (row_id, status))
            continue
        if status in PASS_STATUSES:
            if not isinstance(evidence, list) or not evidence:
                problems.append(
                    "row %r claims %s with no typed evidence list" % (row_id, status)
                )
            else:
                for entry in evidence:
                    _validate_evidence_entry(row_id, entry, problems)
        elif evidence is not None:
            problems.append(
                "row %r has status %s but a non-null evidence field (only PASS "
                "statuses carry evidence)" % (row_id, status)
            )
        # No placeholder string, anywhere in the row, ever stands in for evidence.
        for field_name, field_value in row.items():
            for s in _string_fields(field_value):
                lowered = s.lower()
                for banned in BANNED_EVIDENCE_SUBSTRINGS:
                    if banned in lowered:
                        problems.append(
                            "row %r field %r contains banned placeholder substring %r: %r"
                            % (row_id, field_name, banned, s)
                        )
    return problems


def validate_combinations(doc, valid_row_ids):
    """Validates the BACKLOG `combinations` section (issue #58). These are never coverage:
    every combination row's status is drawn from the same closed vocabulary as behaviour rows,
    but a combination NEVER counts toward behaviour coverage counts."""
    problems = []
    combos = doc.get("combinations")
    if combos is None:
        return problems
    if not isinstance(combos, list):
        problems.append("combinations must be a list")
        return problems
    seen_ids = set()
    for combo in combos:
        if not isinstance(combo, dict):
            problems.append("a combination is not an object")
            continue
        missing = [k for k in REQUIRED_COMBINATION_KEYS if k not in combo]
        if missing:
            problems.append(
                "combination %r is missing keys: %s" % (combo.get("id", "?"), missing)
            )
            continue
        cid = combo["id"]
        if cid in seen_ids:
            problems.append("duplicate combination id %r" % (cid,))
        seen_ids.add(cid)
        rows = combo["rows"]
        if not isinstance(rows, list) or not rows:
            problems.append("combination %r must reference a non-empty list of row ids" % (cid,))
        else:
            for rid in rows:
                if rid not in valid_row_ids:
                    problems.append(
                        "combination %r references unknown behaviour row id %r" % (cid, rid)
                    )
        status = combo["status"]
        evidence = combo["evidence"]
        if status not in COMBINATION_STATUS_TOKENS:
            problems.append(
                "combination %r has an unknown status token %r" % (cid, status)
            )
        elif status in COMBINATION_PASS_STATUSES:
            if not isinstance(evidence, list) or not evidence:
                problems.append(
                    "combination %r claims %s with no typed evidence list" % (cid, status)
                )
            else:
                for entry in evidence:
                    _validate_evidence_entry(cid, entry, problems)
        elif evidence is not None:
            problems.append(
                "combination %r has status %s but a non-null evidence field" % (cid, status)
            )
        for field_name, field_value in combo.items():
            for s in _string_fields(field_value):
                lowered = s.lower()
                for banned in BANNED_EVIDENCE_SUBSTRINGS:
                    if banned in lowered:
                        problems.append(
                            "combination %r field %r contains banned placeholder substring %r: %r"
                            % (cid, field_name, banned, s)
                        )
    return problems


def counts(doc):
    tally = {token: 0 for token in STATUS_TOKENS}
    for row in doc["rows"]:
        tally[row["status"]] = tally.get(row["status"], 0) + 1
    tally["TOTAL"] = len(doc["rows"])
    return tally


def combination_counts(doc):
    tally = {token: 0 for token in COMBINATION_STATUS_TOKENS}
    for combo in doc.get("combinations", []) or []:
        tally[combo["status"]] = tally.get(combo["status"], 0) + 1
    tally["TOTAL"] = len(doc.get("combinations", []) or [])
    return tally


def _evidence_summary(evidence):
    if not evidence:
        return "NONE"
    parts = []
    for e in evidence:
        parts.append(
            "%s/%s (%s)"
            % (e.get("commit", "?"), e.get("evidenceDir") or "-", e.get("verdict", "?"))
        )
    return "; ".join(parts).replace("|", "\\|")


def render_markdown(doc):
    tally = counts(doc)
    lines = [
        "# Behaviour coverage matrix (generated)",
        "",
        "Generated by `Tools/coverage/check_coverage.py --render` from "
        "`Tools/coverage/matrix.json`. Do not hand-edit this file; edit the "
        "JSON and regenerate. Status tokens: "
        + ", ".join(sorted(STATUS_TOKENS))
        + ". `NATIVE_PASS`/`NEGATIVE_PASS` always carry a non-empty list of TYPED evidence "
        "entries (commit, evidenceDir, artifactRef, verdict, scope, historicalScope, "
        "currentDevCoverage) -- never a bare placeholder string. `EVIDENCE_UNVERIFIED` means a "
        "narrative claim of a native PASS exists somewhere in project history but no typed, "
        "content-verified receipt was located for the CURRENT dev bytes. `COVERAGE_GAP` means "
        "no driver was found but no failed transition or production proof of unreachability was "
        "found either; `DEFECT` means a production proof of unreachability exists, cited in "
        "`note`.",
        "",
        "| # | Behaviour | Driver | Status | Evidence | Note |",
        "|---|---|---|---|---|---|",
    ]
    for row in doc["rows"]:
        lines.append(
            "| %s | %s | %s | %s | %s | %s |"
            % (
                row["id"],
                row["behaviour"],
                row["driver"].replace("|", "\\|"),
                row["status"],
                _evidence_summary(row["evidence"]),
                row["note"].replace("|", "\\|"),
            )
        )
    lines.append("")
    lines.append("## Counts (derived from the checker, never hand-typed)")
    lines.append("")
    for token in sorted(STATUS_TOKENS) + ["TOTAL"]:
        lines.append("- %s: %d" % (token, tally.get(token, 0)))
    lines.append("")
    combos = doc.get("combinations") or []
    if combos:
        combo_tally = combination_counts(doc)
        lines.append("## Combinations (BACKLOG for issue #58 -- NEVER coverage)")
        lines.append("")
        lines.append(
            "These rows are pairwise/chain interaction gaps across shared authority or "
            "resources. None of them count toward the behaviour coverage counts above, "
            "regardless of status."
        )
        lines.append("")
        lines.append("| id | name | rows | status | evidence | coupling |")
        lines.append("|---|---|---|---|---|---|")
        for combo in combos:
            lines.append(
                "| %s | %s | %s | %s | %s | %s |"
                % (
                    combo["id"],
                    combo["name"],
                    ",".join(str(r) for r in combo["rows"]),
                    combo["status"],
                    _evidence_summary(combo["evidence"]),
                    combo["coupling"].replace("|", "\\|"),
                )
            )
        lines.append("")
        lines.append("### Combination counts (derived, never hand-typed, never coverage)")
        lines.append("")
        for token in sorted(COMBINATION_STATUS_TOKENS) + ["TOTAL"]:
            lines.append("- %s: %d" % (token, combo_tally.get(token, 0)))
        lines.append("")
    return "\n".join(lines)


def load(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def main(argv):
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--matrix",
        default=os.path.join(os.path.dirname(__file__), "matrix.json"),
    )
    parser.add_argument("--render", action="store_true")
    parser.add_argument(
        "--out",
        default=os.path.join(
            os.path.dirname(__file__), "..", "..", "docs", "BEHAVIOUR_COVERAGE.md"
        ),
    )
    args = parser.parse_args(argv)
    doc = load(args.matrix)
    problems = validate(doc)
    valid_row_ids = {row["id"] for row in doc.get("rows", []) if isinstance(row, dict)}
    problems.extend(validate_combinations(doc, valid_row_ids))
    if problems:
        for problem in problems:
            print(problem, file=sys.stderr)
        return 1
    if args.render:
        with open(args.out, "w", encoding="utf-8") as handle:
            handle.write(render_markdown(doc))
    else:
        tally = counts(doc)
        print("COVERAGE MATRIX VALID:", tally)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
