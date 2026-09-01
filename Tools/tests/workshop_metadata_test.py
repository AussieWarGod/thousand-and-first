#!/usr/bin/env python3
"""Focused release-evidence grammar and human-proof tests."""

from __future__ import annotations

import hashlib
import json
import struct
import tempfile
import unittest
import zlib
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

    def write_preview(self) -> Path:
        def chunk(kind: bytes, body: bytes) -> bytes:
            return (
                struct.pack(">I", len(body))
                + kind
                + body
                + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
            )

        raw = b"".join(b"\x00" + b"\x18\x24\x30" * 512 for _ in range(512))
        payload = (
            b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b"")
        )
        path = self.root / "preview.png"
        path.write_bytes(payload)
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

    def test_alpha_candidate_binds_machine_provenance_without_release_evidence(self) -> None:
        manifest = {
            "id": METADATA.MOD_ID,
            "title": METADATA.TITLE,
            "description": (
                "Found a faction, govern settlements, manage physical water and food, "
                "and optionally leave a legacy across worlds."
            ),
            "version": "1.0.0",
            "author": METADATA.AUTHOR,
            "tags": ",".join(METADATA.TAGS),
            "PreviewImage": METADATA.PREVIEW,
        }
        preview = self.write_preview()
        workshop = self.root / "workshop.json"
        workshop.write_bytes(
            METADATA.canonical_workshop_bytes(
                METADATA.canonical_workshop_data(manifest, 123456789, "2")
            )
        )
        readme = self.root / "README.md"
        changelog = self.root / "CHANGELOG.md"
        readme.write_text(
            "# Fixture\n\n**Status: 1.0.0 public Alpha playtest.**\n",
            encoding="utf-8",
        )
        changelog.write_text(
            "# Changelog\n\n## [1.0.0] — 2026-08-31 (Alpha)\n\nInitial public test.\n",
            encoding="utf-8",
        )
        receipt_hash = "a" * 64
        candidate = "b" * 40
        record = self.root / "ALPHA_CANDIDATE.json"
        payload = {
            "schemaVersion": METADATA.ALPHA_CANDIDATE_SCHEMA,
            "releaseChannel": METADATA.ALPHA_RELEASE_CHANNEL,
            "releaseVersion": "1.0.0",
            "candidateCommit": candidate,
            "gameMarketingVersion": METADATA.GAME_MARKETING_VERSION,
            "gameCoreBuild": METADATA.GAME_CORE_BUILD,
            "workshopId": 123456789,
            "previewSha256": hashlib.sha256(preview.read_bytes()).hexdigest(),
            "privatePackageReceiptSha256": receipt_hash,
        }
        record.write_text(json.dumps(payload) + "\n", encoding="utf-8")

        self.assertEqual(
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            ),
            (candidate, receipt_hash),
        )

        payload["previewSha256"] = "0" * 64
        record.write_text(json.dumps(payload) + "\n", encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "previewSha256"):
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            )

        payload["previewSha256"] = hashlib.sha256(preview.read_bytes()).hexdigest()
        record.write_text(json.dumps(payload) + "\n", encoding="utf-8")
        changelog.write_text(
            "# Changelog\n\n## [Unreleased] — 1.0.0 work in progress\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "Alpha release date"):
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            )

    def test_alpha_candidate_accepts_patch_update_and_keeps_exact_version_binding(self) -> None:
        version = "1.0.1"
        manifest = {
            "id": METADATA.MOD_ID,
            "title": METADATA.TITLE,
            "description": (
                "Found a faction, govern settlements, manage physical water and food, "
                "and optionally leave a legacy across worlds."
            ),
            "version": version,
            "author": METADATA.AUTHOR,
            "tags": ",".join(METADATA.TAGS),
            "PreviewImage": METADATA.PREVIEW,
        }
        preview = self.write_preview()
        workshop = self.root / "workshop.json"
        readme = self.root / "README.md"
        changelog = self.root / "CHANGELOG.md"
        record = self.root / "ALPHA_CANDIDATE.json"
        candidate = "c" * 40
        receipt_hash = "d" * 64

        def write_version_bound_fixture(candidate_version: str) -> None:
            manifest["version"] = candidate_version
            workshop.write_bytes(
                METADATA.canonical_workshop_bytes(
                    METADATA.canonical_workshop_data(manifest, 123456789, "2")
                )
            )
            readme.write_text(
                f"# Fixture\n\n**Status: {candidate_version} public Alpha playtest.**\n",
                encoding="utf-8",
            )
            changelog.write_text(
                f"# Changelog\n\n## [{candidate_version}] — 2026-09-01 (Alpha)\n",
                encoding="utf-8",
            )
            record.write_text(
                json.dumps(
                    {
                        "schemaVersion": METADATA.ALPHA_CANDIDATE_SCHEMA,
                        "releaseChannel": METADATA.ALPHA_RELEASE_CHANNEL,
                        "releaseVersion": candidate_version,
                        "candidateCommit": candidate,
                        "gameMarketingVersion": METADATA.GAME_MARKETING_VERSION,
                        "gameCoreBuild": METADATA.GAME_CORE_BUILD,
                        "workshopId": 123456789,
                        "previewSha256": hashlib.sha256(preview.read_bytes()).hexdigest(),
                        "privatePackageReceiptSha256": receipt_hash,
                    }
                )
                + "\n",
                encoding="utf-8",
            )

        write_version_bound_fixture(version)
        self.assertEqual(
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            ),
            (candidate, receipt_hash),
        )

        payload = json.loads(record.read_text(encoding="utf-8"))
        payload["releaseVersion"] = "1.0.0"
        record.write_text(json.dumps(payload) + "\n", encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "version must match manifest"):
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            )

        for invalid_version in ("1.0.01", "1.1.0", "2.0.0"):
            with self.subTest(version=invalid_version):
                write_version_bound_fixture(invalid_version)
                with self.assertRaisesRegex(
                    METADATA.ValidationError, "1.0.0 or a later canonical 1.0.x patch"
                ):
                    METADATA.validate_alpha_candidate(
                        manifest, preview, workshop, record, readme, changelog
                    )

    def test_workshop_lanes_enforce_absence_and_visibility(self) -> None:
        manifest = {
            "description": (
                "Found a faction, govern settlements, manage physical water and food, "
                "and optionally leave a legacy across worlds."
            )
        }
        workshop = self.root / "workshop.json"

        self.assertIsNone(METADATA.validate_workshop(workshop, manifest, "test"))
        for public_mode in ("alpha", "release"):
            with self.subTest(mode=public_mode, state="absent"):
                with self.assertRaisesRegex(METADATA.ValidationError, "requires workshop.json"):
                    METADATA.validate_workshop(workshop, manifest, public_mode)

        workshop.write_bytes(
            METADATA.canonical_workshop_bytes(
                METADATA.canonical_workshop_data(manifest, 123456789, "0")
            )
        )
        METADATA.validate_workshop(workshop, manifest, "test")
        for public_mode in ("alpha", "release"):
            with self.subTest(mode=public_mode, state="private"):
                with self.assertRaisesRegex(METADATA.ValidationError, "Visibility"):
                    METADATA.validate_workshop(workshop, manifest, public_mode)

        workshop.write_bytes(
            METADATA.canonical_workshop_bytes(
                METADATA.canonical_workshop_data(manifest, 123456789, "2")
            )
        )
        for public_mode in ("alpha", "release"):
            with self.subTest(mode=public_mode, state="public"):
                METADATA.validate_workshop(workshop, manifest, public_mode)
        with self.assertRaisesRegex(METADATA.ValidationError, "Visibility"):
            METADATA.validate_workshop(workshop, manifest, "test")
        with self.assertRaisesRegex(METADATA.ValidationError, "release mode"):
            METADATA.validate_workshop(workshop, manifest, "preview")

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
