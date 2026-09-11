#!/usr/bin/env python3
"""Machine-checkable behavioural coverage matrix.

Validates Tools/coverage/matrix.json against a small schema and (optionally)
regenerates docs/BEHAVIOUR_COVERAGE.md from it. Every status token is closed:
an unknown token, or a PASS-shaped status with no TYPED evidence, is a hard
error, never silently accepted. Counts are always derived, never typed by hand.

Evidence is a TYPED, HOST-INDEPENDENT object, never a bare string and never an
absolute path: a placeholder like "prior-status-md-rows-not-rerun" cannot stand
in for a real receipt, and neither can a private machine path like
"/home/r/..." or "/mnt/c/..." or "C:\\...", which would fail on any other
machine (including public CI) and leaks host layout into a public doc.
`validate()` does NO filesystem access and depends on no local machine state --
it is the schema check `test_checked_in_matrix` runs offline in a clean
checkout. Each PASS-shaped row carries a non-empty list of evidence entries:
commit, artifactPath (run-relative, e.g. "hotfix142-native.IqS6Jj/results.json"),
artifactSha256, artifactBytes, verdict, scope, historicalScope,
currentDevCoverage, inventoryDigest.

Actually resolving artifactPath under a real evidence archive and verifying
its bytes' SHA-256 is a SEPARATE, optional operation: `audit --evidence-root
<dir>`. That is an "evidence audit" result, distinct from schema PASS, and is
never required by `test_checked_in_matrix` (which has no evidence archive to
resolve against in a bare checkout or in public CI).
"""

import argparse
import hashlib
import json
import os
import re
import subprocess
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
    "artifactPath",
    "artifactSha256",
    "artifactBytes",
    "verdict",
    "scope",
    "historicalScope",
    "currentDevCoverage",
    "inventoryDigest",
)
_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_ABSOLUTE_PATH_PATTERN = re.compile(r"(?:^|\s)(/[A-Za-z0-9_.\-]|[A-Za-z]:[\\/])")
_ARTIFACT_PATH_PATTERN = re.compile(r"^[A-Za-z0-9_.-]+(/[A-Za-z0-9_.\-]+)+$")


def _has_dot_or_dotdot_segment(path):
    return any(part in (".", "..") for part in path.replace("\\", "/").split("/"))

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


def _check_no_absolute_paths(row_id, row, problems):
    """No field anywhere in a row may contain a private absolute host path --
    this is what keeps validate() (and the generated docs) host-independent."""
    for field_name, field_value in row.items():
        for s in _string_fields(field_value):
            if _ABSOLUTE_PATH_PATTERN.search(s):
                problems.append(
                    "row %r field %r contains an absolute host path (not portable): %r"
                    % (row_id, field_name, s)
                )


def _validate_evidence_entry(row_id, entry, current_digest, problems):
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
    for field in ("commit", "artifactPath", "verdict", "scope"):
        v = entry.get(field)
        if not isinstance(v, str) or not v.strip():
            problems.append(
                "row %r evidence entry field %r must be a non-empty string"
                % (row_id, field)
            )
    artifact_path = entry.get("artifactPath")
    if isinstance(artifact_path, str) and artifact_path.strip():
        if _ABSOLUTE_PATH_PATTERN.match(artifact_path):
            problems.append(
                "row %r evidence entry's artifactPath %r is an absolute path -- it must be "
                "run-relative (e.g. \"hotfix142-native.IqS6Jj/results.json\") so schema "
                "validation never depends on this machine's layout" % (row_id, artifact_path)
            )
        elif not _ARTIFACT_PATH_PATTERN.match(artifact_path):
            problems.append(
                "row %r evidence entry's artifactPath %r does not look like a run-relative "
                "\"dir/file\" name (a bare description like \"build journal (COMPLETE OK)\" "
                "is not a ref)" % (row_id, artifact_path)
            )
        elif _has_dot_or_dotdot_segment(artifact_path):
            problems.append(
                "row %r evidence entry's artifactPath %r contains a \".\" or \"..\" segment "
                "-- it must be a plain run-relative name with no traversal" % (row_id, artifact_path)
            )
    sha = entry.get("artifactSha256")
    if not isinstance(sha, str) or not _SHA256_PATTERN.match(sha):
        problems.append(
            "row %r evidence entry's artifactSha256 must be a 64-char lowercase hex digest"
            % (row_id,)
        )
    size = entry.get("artifactBytes")
    if not isinstance(size, int) or isinstance(size, bool) or size <= 0:
        problems.append(
            "row %r evidence entry's artifactBytes must be a positive integer" % (row_id,)
        )
    digest = entry.get("inventoryDigest")
    if digest is not None and (not isinstance(digest, str) or not _SHA256_PATTERN.match(digest)):
        problems.append(
            "row %r evidence entry's inventoryDigest must be null or a 64-char lowercase hex "
            "digest" % (row_id,)
        )
    if entry.get("currentDevCoverage") is True:
        if digest is None:
            problems.append(
                "row %r claims currentDevCoverage=true but has no inventoryDigest to prove "
                "whole-inventory identity against" % (row_id,)
            )
        elif current_digest is not None and digest != current_digest:
            problems.append(
                "row %r claims currentDevCoverage=true but inventoryDigest %r does not equal "
                "the digest of the tree being validated (%r) -- currentDevCoverage requires "
                "whole-inventory identity, not commit ancestry or one file's content-identity"
                % (row_id, digest, current_digest)
            )


def validate(doc, current_digest=None):
    """Returns a list of problem strings; empty means the document is valid.

    Pure schema check: no filesystem access, no dependency on this machine's
    layout. `current_digest`, when given, additionally checks that any
    currentDevCoverage=true row's inventoryDigest actually equals it (the
    caller computes this via Tools/dev-harness-inventory.py against whatever
    tree it wants to validate against -- never hardcoded here).
    """
    problems = []
    if not isinstance(doc, dict) or doc.get("schemaVersion") != 3:
        problems.append("missing or wrong schemaVersion (expected 3, host-independent evidence)")
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
                    _validate_evidence_entry(row_id, entry, current_digest, problems)
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
        _check_no_absolute_paths(row_id, row, problems)
    return problems


def validate_combinations(doc, valid_row_ids, current_digest=None):
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
                    _validate_evidence_entry(cid, entry, current_digest, problems)
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
        _check_no_absolute_paths(cid, combo, problems)
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
            % (e.get("commit", "?"), e.get("artifactPath") or "-", e.get("verdict", "?"))
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
        + ". `NATIVE_PASS`/`NEGATIVE_PASS` always carry a non-empty list of TYPED, "
        "host-independent evidence entries (commit, artifactPath -- a run-relative name, "
        "never an absolute path -- artifactSha256, artifactBytes, verdict, scope, "
        "historicalScope, currentDevCoverage) -- never a bare placeholder string, never a "
        "private machine path. Resolving artifactPath against a real evidence archive and "
        "verifying its bytes is a SEPARATE `check_coverage.py audit` step, not part of this "
        "schema. `EVIDENCE_UNVERIFIED` means a narrative claim of a native PASS exists "
        "somewhere in project history but no typed receipt was confirmed for the CURRENT dev "
        "bytes. `COVERAGE_GAP` means no driver was found but no failed transition or "
        "production proof of unreachability was found either; `DEFECT` means a production "
        "proof of unreachability exists, cited in `note`.",
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


def compute_inventory_digest(repo_root):
    """Invokes Tools/dev-harness-inventory.py --inventory-digest against repo_root. Never
    called from validate() itself -- only from the CLI, and only when the caller asks for a
    live digest rather than schema-only validation."""
    script = os.path.join(repo_root, "Tools", "dev-harness-inventory.py")
    result = subprocess.run(
        [sys.executable, script, "--inventory-digest"],
        cwd=repo_root, capture_output=True, text=True, check=True,
    )
    return result.stdout.strip()


def _all_evidence_entries(doc):
    for row in doc.get("rows", []):
        for entry in row.get("evidence") or []:
            yield row.get("id"), entry
    for combo in doc.get("combinations", []) or []:
        for entry in combo.get("evidence") or []:
            yield combo.get("id"), entry


def _resolve_contained(evidence_root, artifact_path):
    """Resolves artifact_path under evidence_root, following symlinks (os.path.realpath), and
    returns None if the result escapes the root (os.path.commonpath) -- containment is proved
    on the REAL path, so a symlink pointing outside evidence_root cannot be used to read
    arbitrary files."""
    root_real = os.path.realpath(evidence_root)
    candidate = os.path.realpath(os.path.join(evidence_root, artifact_path))
    try:
        if os.path.commonpath([root_real, candidate]) != root_real:
            return None
    except ValueError:
        # Different drives on Windows, or otherwise incomparable -- never contained.
        return None
    return candidate


class SchemaFailedForAudit(Exception):
    """Raised by audit() when the matrix does not validate cleanly -- audit REFUSES to check
    any retained bytes against a document that is not even schema-valid. Schema PASS is a
    hard prerequisite for evidence audit, never a parallel, independent check."""


def audit(doc, evidence_root, current_digest=None):
    """Resolves each evidence entry's artifactPath under evidence_root and verifies the
    retained bytes' SHA-256 and size. This is a SEPARATE result from schema validation --
    "evidence audit", not "schema PASS" -- and is never required by test_checked_in_matrix,
    which has no evidence archive to resolve against in a bare checkout or public CI. Schema
    validation is still a PREREQUISITE for running an audit at all: raises SchemaFailedForAudit
    if the document itself does not validate, before touching the filesystem."""
    problems = validate(doc, current_digest=current_digest)
    valid_row_ids = {row["id"] for row in doc.get("rows", []) if isinstance(row, dict)}
    problems.extend(validate_combinations(doc, valid_row_ids, current_digest=current_digest))
    if problems:
        raise SchemaFailedForAudit("; ".join(problems))
    results = []
    for owner_id, entry in _all_evidence_entries(doc):
        artifact_path = entry.get("artifactPath", "")
        record = {"id": owner_id, "artifactPath": artifact_path}
        resolved = _resolve_contained(evidence_root, artifact_path)
        if resolved is None:
            record["result"] = "ESCAPES_ROOT"
            results.append(record)
            continue
        if not os.path.isfile(resolved):
            record["result"] = "MISSING"
            results.append(record)
            continue
        actual_size = os.path.getsize(resolved)
        with open(resolved, "rb") as handle:
            actual_sha = hashlib.sha256(handle.read()).hexdigest()
        expected_sha = entry.get("artifactSha256")
        expected_size = entry.get("artifactBytes")
        if actual_sha != expected_sha:
            record["result"] = "HASH_DRIFT"
        elif actual_size != expected_size:
            record["result"] = "SIZE_DRIFT"
        else:
            record["result"] = "VERIFIED"
        record["actualSha256"] = actual_sha
        record["actualBytes"] = actual_size
        results.append(record)
    return results


def main(argv):
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command")

    validate_parser = sub.add_parser("validate", help="schema-only, offline (default)")
    validate_parser.add_argument(
        "--matrix", default=os.path.join(os.path.dirname(__file__), "matrix.json"))
    validate_parser.add_argument("--render", action="store_true")
    validate_parser.add_argument(
        "--out", default=os.path.join(
            os.path.dirname(__file__), "..", "..", "docs", "BEHAVIOUR_COVERAGE.md"))
    validate_parser.add_argument("--repo-root", default=None,
        help="compute the live inventory digest against this repo root, for "
             "currentDevCoverage=true checks (never hardcoded)")
    validate_parser.add_argument("--inventory-digest", default=None,
        help="use this digest directly instead of computing one")

    audit_parser = sub.add_parser("audit", help="resolve+verify evidence bytes (never required)")
    audit_parser.add_argument("--matrix", default=os.path.join(os.path.dirname(__file__), "matrix.json"))
    audit_parser.add_argument("--evidence-root", required=True)
    audit_parser.add_argument("--repo-root", default=None)
    audit_parser.add_argument("--inventory-digest", default=None)

    # Back-compat: no subcommand behaves as "validate" with the old flat flags.
    parser.add_argument("--matrix", default=os.path.join(os.path.dirname(__file__), "matrix.json"))
    parser.add_argument("--render", action="store_true")
    parser.add_argument(
        "--out", default=os.path.join(
            os.path.dirname(__file__), "..", "..", "docs", "BEHAVIOUR_COVERAGE.md"))
    parser.add_argument("--repo-root", default=None)
    parser.add_argument("--inventory-digest", default=None)

    args = parser.parse_args(argv)

    if args.command == "audit":
        doc = load(args.matrix)
        current_digest = args.inventory_digest
        if current_digest is None and args.repo_root is not None:
            current_digest = compute_inventory_digest(args.repo_root)
        try:
            results = audit(doc, args.evidence_root, current_digest=current_digest)
        except SchemaFailedForAudit as error:
            print("EVIDENCE AUDIT ABORTED: schema validation failed before any byte check:",
                  file=sys.stderr)
            print(str(error), file=sys.stderr)
            return 3
        bad = [r for r in results if r["result"] != "VERIFIED"]
        for r in results:
            print("evidence audit:", r)
        if bad:
            print("EVIDENCE AUDIT: %d/%d entries failed" % (len(bad), len(results)), file=sys.stderr)
            return 2
        print("EVIDENCE AUDIT: %d/%d entries verified" % (len(results), len(results)))
        return 0

    doc = load(args.matrix)
    current_digest = args.inventory_digest
    if current_digest is None and args.repo_root is not None:
        current_digest = compute_inventory_digest(args.repo_root)
    problems = validate(doc, current_digest=current_digest)
    valid_row_ids = {row["id"] for row in doc.get("rows", []) if isinstance(row, dict)}
    problems.extend(validate_combinations(doc, valid_row_ids, current_digest=current_digest))
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
