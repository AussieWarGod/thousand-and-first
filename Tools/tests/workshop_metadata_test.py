#!/usr/bin/env python3
"""Focused release-evidence grammar and human-proof tests."""

from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from Tools import workshop_metadata as METADATA


class WorkshopMetadataTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_testing(self, rows: list[str]) -> Path:
        path = self.root / "TESTING.md"
        body = ["| Step | Action | Expect |", "|---|---|---|"]
        body.extend(f"| {pass_id} | Do it | Passed |" for pass_id in rows)
        path.write_text("\n".join(body) + "\n", encoding="utf-8")
        return path

    def test_testing_parser_accepts_one_optional_dotted_numeric_suffix(self) -> None:
        path = self.write_testing(["1", "16f1", "135a4h", "136j.1", "136j.10"])
        self.assertEqual(
            METADATA.testing_pass_ids(path),
            ("1", "16f1", "135a4h", "136j.1", "136j.10"),
        )

    def test_testing_parser_rejects_malformed_or_ambiguous_rows(self) -> None:
        for pass_id in ("1.2.3", "1.a", "1-2", "1A"):
            with self.subTest(pass_id=pass_id):
                with self.assertRaisesRegex(METADATA.ValidationError, "invalid individual pass ID"):
                    METADATA.testing_pass_ids(self.write_testing([pass_id]))
        with self.assertRaisesRegex(METADATA.ValidationError, "ambiguous duplicate pass IDs"):
            METADATA.testing_pass_ids(self.write_testing(["16f1", "16f1"]))

    def test_release_claims_are_bound_to_manifest_version_and_date(self) -> None:
        manifest = {"version": "1.2.3"}
        readme = self.root / "README.md"
        changelog = self.root / "CHANGELOG.md"
        readme.write_text(
            "# Fixture\n\n**Status: 1.2.3 public playtest release.**\n",
            encoding="utf-8",
        )
        changelog.write_text(
            "# Changelog\n\n## [1.2.3] — 2026-08-28\n",
            encoding="utf-8",
        )
        METADATA.validate_release_claims(manifest, readme, changelog)

        readme.write_text(
            "# Fixture\n\n**Status: 1.2.2 public playtest release.**\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "version-bound release status"):
            METADATA.validate_release_claims(manifest, readme, changelog)

        readme.write_text(
            "# Fixture\n\n**Status: 1.2.3 public playtest release.**\n",
            encoding="utf-8",
        )
        changelog.write_text(
            "# Changelog\n\n## [Unreleased] — 1.2.3 pending\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "first version heading"):
            METADATA.validate_release_claims(manifest, readme, changelog)

        changelog.write_text(
            "# Changelog\n\n## [1.2.3] — 2026-08-28\n",
            encoding="utf-8",
        )
        readme.write_text(
            "# Fixture\n\n**Status: 1.2.3 public playtest release.**\n"
            "This tree is not a release candidate.\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "not a release candidate"):
            METADATA.validate_release_claims(manifest, readme, changelog)

    def test_human_fields_reject_sentinels_and_nonprintable_text(self) -> None:
        self.assertTrue(METADATA._human_text_valid("Morgan Reviewer", 2, 80))
        for value in (
            "HUMAN_TESTER_NAME_OR_ALIAS",
            "Example Reviewer",
            "TODO",
            "TBD reviewer",
            "unknown person",
            "N/A",
            "Reviewer\nName",
        ):
            with self.subTest(value=value):
                self.assertFalse(METADATA._human_text_valid(value, 2, 80))

    def test_release_artifact_discovery_is_safe_sorted_and_unique(self) -> None:
        record = self.root / "evidence.json"
        record.write_text(
            json.dumps({
                "verification": {
                    "z": {"artifactRef": "docs/release-evidence/z.txt"},
                    "a": [{"artifactRef": "docs/release-evidence/a.txt"}],
                },
            }),
            encoding="utf-8",
        )
        self.assertEqual(
            METADATA.release_evidence_artifact_refs(record),
            ("docs/release-evidence/a.txt", "docs/release-evidence/z.txt"),
        )

        record.write_text(
            json.dumps({
                "first": {"artifactRef": "docs/release-evidence/a.txt"},
                "second": {"artifactRef": "docs/release-evidence/a.txt"},
            }),
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "reuses retained artifactRef"):
            METADATA.release_evidence_artifact_refs(record)

        record.write_text(
            json.dumps({"artifactRef": "docs/release-evidence/../escape.txt"}),
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "unsafe artifactRef"):
            METADATA.release_evidence_artifact_refs(record)


if __name__ == "__main__":
    unittest.main()
