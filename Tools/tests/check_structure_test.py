#!/usr/bin/env python3
"""Isolated tests for Tools/check-structure.py."""

from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path


CHECKER_PATH = Path(__file__).resolve().parents[1] / "check-structure.py"
SPEC = importlib.util.spec_from_file_location("taf_check_structure", CHECKER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


class StructureCheckerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def source(self, relative: str, lines: int, *, xrl: bool = False) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        rows = ["using XRL.World;" if xrl else "namespace Fixture;"]
        rows.extend(f"// {index}" for index in range(1, lines))
        path.write_text("\n".join(rows) + "\n", encoding="utf-8")

    def valid_review(self, census) -> Path:
        path = self.root / "review.json"
        path.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "inventorySha256": census.inventory_sha256,
                    "exceptions": [],
                    "reviewedBy": "fixture-reviewer",
                    "completedUtc": "2026-08-26T12:00:00Z",
                    "oneResponsibility": {
                        "status": "passed",
                        "notes": "Reviewed each fixture ownership boundary.",
                    },
                    "protocolsAtBoundaries": {
                        "status": "passed",
                        "notes": "Reviewed fixture dependency direction.",
                    },
                }
            ),
            encoding="utf-8",
        )
        return path

    def test_strict_limit_counts_exactly_300_as_release_debt(self) -> None:
        self.source("Core/Small.cs", 299)
        self.source("Core/Exact.cs", 300)
        self.source("Growth/Large.cs", 301, xrl=True)
        census = CHECKER.build_census(
            self.root,
            ["Core/Small.cs", "Core/Exact.cs", "Growth/Large.cs"],
        )
        self.assertEqual(census.physical_lines, 900)
        self.assertEqual(len(census.at_or_over_limit), 2)
        self.assertEqual(len(census.over_limit), 1)
        self.assertEqual(len(census.exactly_at_limit), 1)
        self.assertEqual(len(census.large_direct_xrl_imports), 1)

    def test_release_passes_only_with_small_files_and_exact_semantic_review(self) -> None:
        self.source("Core/One.cs", 20)
        self.source("Api/Two.cs", 42)
        census = CHECKER.build_census(self.root, ["Core/One.cs", "Api/Two.cs"])
        review = self.valid_review(census)
        self.assertEqual(CHECKER.release_issues(census, review), [])

        inventory = self.root / "inventory.txt"
        inventory.write_text("Core/One.cs\nApi/Two.cs\n", encoding="utf-8")
        output = io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
            result = CHECKER.main(
                [
                    "--release",
                    "--repo-root",
                    str(self.root),
                    "--inventory-file",
                    str(inventory),
                    "--review-ledger",
                    str(review),
                ]
            )
        self.assertEqual(result, 0)
        self.assertIn("STRUCTURE RELEASE GATE CLEAN", output.getvalue())

    def test_release_reports_line_debt_and_missing_review_together(self) -> None:
        self.source("Core/Large.cs", 300)
        census = CHECKER.build_census(self.root, ["Core/Large.cs"])
        issues = CHECKER.release_issues(census, self.root / "missing.json")
        self.assertTrue(any("not strictly under 300" in issue for issue in issues))
        self.assertTrue(any("semantic review is missing" in issue for issue in issues))

    def test_stale_review_and_exceptions_are_refused(self) -> None:
        self.source("Core/One.cs", 20)
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        review = self.valid_review(census)
        payload = json.loads(review.read_text(encoding="utf-8"))
        payload["inventorySha256"] = "0" * 64
        payload["exceptions"] = ["Core/One.cs"]
        review.write_text(json.dumps(payload), encoding="utf-8")
        issues = CHECKER.review_issues(review, census)
        self.assertTrue(any("does not bind" in issue for issue in issues))
        self.assertTrue(any("exceptions must be empty" in issue for issue in issues))

    def test_review_schema_rejects_unknown_and_missing_keys(self) -> None:
        self.source("Core/One.cs", 20)
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        review = self.valid_review(census)
        payload = json.loads(review.read_text(encoding="utf-8"))
        payload["unexpected"] = True
        payload["oneResponsibility"]["extra"] = "passed"
        del payload["protocolsAtBoundaries"]["notes"]
        review.write_text(json.dumps(payload), encoding="utf-8")
        issues = CHECKER.review_issues(review, census)
        self.assertIn("semantic review fields must exactly match schema version 1", issues)
        self.assertIn(
            "semantic review oneResponsibility fields must be notes and status", issues
        )
        self.assertIn(
            "semantic review protocolsAtBoundaries fields must be notes and status", issues
        )

    def test_review_rejects_placeholder_and_nonprintable_human_text(self) -> None:
        self.source("Core/One.cs", 20)
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        substitutions = (
            ("reviewedBy", "HUMAN_REVIEWER_NAME_OR_ALIAS"),
            ("reviewedBy", "Example Reviewer"),
            ("reviewedBy", "N/A"),
            ("oneResponsibility", "TODO: inspect every source boundary before release."),
            ("protocolsAtBoundaries", "Reviewed the API boundary.\nSecond synthetic line."),
        )
        for target, value in substitutions:
            with self.subTest(target=target, value=value):
                review = self.valid_review(census)
                payload = json.loads(review.read_text(encoding="utf-8"))
                if target == "reviewedBy":
                    payload[target] = value
                else:
                    payload[target]["notes"] = value
                review.write_text(json.dumps(payload), encoding="utf-8")
                issues = CHECKER.review_issues(review, census)
                self.assertTrue(
                    any(target in issue or "reviewedBy" in issue for issue in issues),
                    issues,
                )

    def test_review_timestamp_requires_real_second_precision_utc(self) -> None:
        self.source("Core/One.cs", 20)
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        for timestamp in (
            "2026-08-26T12:00:00.000Z",
            "2026-02-30T12:00:00Z",
            "2026-08-26T12:00:00+00:00",
        ):
            with self.subTest(timestamp=timestamp):
                review = self.valid_review(census)
                payload = json.loads(review.read_text(encoding="utf-8"))
                payload["completedUtc"] = timestamp
                review.write_text(json.dumps(payload), encoding="utf-8")
                self.assertTrue(
                    any("completedUtc" in issue for issue in CHECKER.review_issues(review, census))
                )

    def test_inventory_rejects_escape_duplicate_and_non_csharp_paths(self) -> None:
        self.source("Core/One.cs", 1)
        with self.assertRaisesRegex(CHECKER.StructureError, "duplicate"):
            CHECKER.build_census(self.root, ["Core/One.cs", "Core/One.cs"])
        for unsafe in ("../One.cs", "/tmp/One.cs", "Core\\One.cs", "Core/One.xml"):
            with self.subTest(unsafe=unsafe):
                with self.assertRaises(CHECKER.StructureError):
                    CHECKER.build_census(self.root, [unsafe])


if __name__ == "__main__":
    unittest.main()
