"""Preflight exercises the real release validators, including the failed 0.3.5 wording."""
import hashlib
import json
import unittest

from Tools import release_metadata_preflight as preflight
from Tools import workshop_metadata as metadata
from Tools.tests import workshop_metadata_test as fixtures


class ReleaseMetadataPreflightTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.WorkshopMetadataTests()
        self.fixture.setUp()
        self.addCleanup(self.fixture.tearDown)
        self.root = self.fixture.root
        path = self.fixture.write_manifest(
            "Found a faction, govern settlements, manage physical water and food, "
            "and optionally a legacy across worlds.")
        manifest = json.loads(path.read_text())
        manifest["version"] = "0.3.1"
        path.write_text(json.dumps(manifest))
        preview = self.fixture.write_preview()
        record, self.private, public = self.fixture.write_alpha_binding_fixture()
        self.workshop = self.root / "workshop.json"
        self.workshop.write_bytes(public.read_bytes())
        data = json.loads(record.read_text())
        data["previewSha256"] = hashlib.sha256(preview.read_bytes()).hexdigest()
        (self.root / "docs").mkdir()
        (self.root / "docs/ALPHA_CANDIDATE.json").write_text(json.dumps(data))
        self.readme = self.root / "README.md"
        self.readme.write_text("**Status: 0.3.1 public Alpha playtest.**\n")
        (self.root / "CHANGELOG.md").write_text("## [0.3.1] — 2026-09-13 (Alpha)\n")

    def test_public_uses_real_candidate_validator(self):
        self.assertEqual(preflight.check(self.root), "alpha")

    def test_pre_release_status_shape_that_passed_docs_still_refuses(self):
        self.readme.write_text("**Status: pre-release source for 0.3.1 public Alpha playtest; public promotion pending.**\n")
        with self.assertRaisesRegex(metadata.ValidationError, "Alpha-bound status"):
            preflight.check(self.root)

    def test_private_checks_metadata_without_requiring_public_candidate(self):
        self.workshop.write_bytes(self.private.read_bytes())
        (self.root / "docs/ALPHA_CANDIDATE.json").unlink()
        self.assertEqual(preflight.check(self.root), "test")

    def test_public_missing_candidate_refuses(self):
        (self.root / "docs/ALPHA_CANDIDATE.json").unlink()
        with self.assertRaises(metadata.ValidationError):
            preflight.check(self.root)

    def test_unknown_visibility_cannot_skip_validation(self):
        data = json.loads(self.workshop.read_text())
        data["Visibility"] = "1"
        self.workshop.write_text(json.dumps(data))
        with self.assertRaisesRegex(metadata.ValidationError, "visibility"):
            preflight.check(self.root)

    def test_preview_mismatch_refuses(self):
        path = self.root / "docs/ALPHA_CANDIDATE.json"
        data = json.loads(path.read_text())
        data["previewSha256"] = "a" * 64
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(metadata.ValidationError, "previewSha256"):
            preflight.check(self.root)
