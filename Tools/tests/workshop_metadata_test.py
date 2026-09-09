#!/usr/bin/env python3
"""Focused release-evidence grammar and human-proof tests."""

from __future__ import annotations

import hashlib
import io
import json
import struct
import tempfile
import unittest
import zlib
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

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

    def write_manifest(self, description: str) -> Path:
        path = self.root / "manifest.json"
        path.write_text(
            json.dumps(
                {
                    "id": METADATA.MOD_ID,
                    "title": METADATA.TITLE,
                    "description": description,
                    "version": "0.3.0",
                    "author": METADATA.AUTHOR,
                    "tags": ",".join(METADATA.TAGS),
                    "PreviewImage": METADATA.PREVIEW,
                }
            )
            + "\n",
            encoding="utf-8",
        )
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

    def write_alpha_binding_fixture(self, version: str = "0.3.1", schema: int = 2) -> tuple[Path, Path, Path]:
        manifest = {"version": version}
        public_id = 3794797472
        private_id = public_id if schema == 1 else 123456789
        record = self.root / "ALPHA_CANDIDATE.json"
        private = self.root / "private-workshop.json"
        public = self.root / "public-workshop.json"
        payload = {
            "schemaVersion": schema,
            "releaseChannel": METADATA.ALPHA_RELEASE_CHANNEL,
            "releaseVersion": version,
            "candidateCommit": "b" * 40,
            "gameMarketingVersion": METADATA.GAME_MARKETING_VERSION,
            "gameCoreBuild": METADATA.GAME_CORE_BUILD,
            "workshopId": public_id,
            "previewSha256": "c" * 64,
            "privatePackageReceiptSha256": "d" * 64,
        }
        if schema == 2:
            payload["privateWorkshopId"] = private_id
        record.write_text(json.dumps(payload), encoding="utf-8")
        private.write_bytes(METADATA.canonical_workshop_bytes(
            METADATA.canonical_workshop_data(manifest, private_id, "0")))
        public.write_bytes(METADATA.canonical_workshop_bytes(
            METADATA.canonical_workshop_data(manifest, public_id, "2")))
        return record, private, public

    def test_manifest_accepts_meaning_equivalent_optional_legacy_disclosures(self) -> None:
        descriptions = (
            "Found a faction, govern settlements, manage physical water and food, "
            "and optionally a legacy across worlds.",
            "Found a faction and govern settlements in Qud. Cross-world inheritance "
            "is opt-in and begins only when enabled before world creation.",
            "Found a faction and govern settlements in Qud. Carry your kingdom's layout "
            "and history into the next world, if you choose.",
            "Found a faction and govern settlements in Qud. You may choose whether your "
            "kingdom's legacy carries between worlds.",
            "Found a faction and govern settlements in Qud. Cross‑world inheritance "
            "is optional and begins only when enabled before world creation.",
            "Found a faction and govern settlements in Qud, with an opt-in legacy "
            "across worlds that you enable before world creation.",
            "Found a faction and govern settlements in Qud. Choose to carry your "
            "kingdom's history into a later world.",
            "Found a faction and govern settlements in Qud. Opt in to carry your "
            "kingdom's layout into the next world.",
            "Found a faction and govern settlements in Qud. Backups are not optional "
            "in Alpha, but cross-world legacy is optional.",
            "Found a faction and govern settlements in Qud. Save backups are never "
            "optional; cross-world legacy is opt-in.",
        )
        for description in descriptions:
            with self.subTest(description=description):
                manifest = METADATA.load_manifest(self.write_manifest(description))
                self.assertEqual(manifest["description"], description)

    def test_alpha_listing_leads_with_hook_and_implemented_feature_groups(self) -> None:
        description = METADATA.canonical_description({})
        self.assertTrue(description.startswith(
            "[b]Found a faction. Raise settlements. Leave a history behind.[/b]\n\n"
        ))
        for text in (
            "[b]Build a living realm[/b]",
            "optional Kingdom Quickstart",
            "a seat and up to two other cities",
            "typed plots in S, M, L, and XL sizes",
            "roads, shafts, utilities, porters, construction routes, and trade",
            "physical fresh water, crops, meals, materials, power, wear, repair, and cargo",
            "paying, fighting, fortifying, or talking",
            "research, certified machinery, laboratories, grafts, a becoming annexe",
            "a crown, mirror-gates, and a hosted arcology",
            "dated Chronicle and homecoming reports",
            "[b]Shape each settlement[/b]",
            "Affiliations and optional covenants are not cosmetic labels.",
            "separate options for growth, water scarcity, raids, trade",
        ):
            with self.subTest(text=text):
                self.assertIn(text, description)
        self.assertLess(description.index("[b]Build a living realm[/b]"),
                        description.index("[b]Shape each settlement[/b]"))
        self.assertLess(description.index("[b]Shape each settlement[/b]"),
                        description.index("[b]Alpha, compatibility, and support[/b]"))

    def test_alpha_listing_preserves_legacy_compatibility_and_release_caveats(self) -> None:
        description = METADATA.canonical_description({})
        for text in (
            "Cross-world legacy is opt-in and must be enabled before world creation.",
            "It may carry bounded layout and history into a later world.",
            "It never carries items, liquids, charge, or old actor identity.",
            "Expect bugs, rough edges, balance changes, and incomplete visual or compatibility coverage.",
            "This listing stays Alpha; Beta and Release will be separate Workshop items.",
            "Built for Caves of Qud v1.0.5, core build 2.0.211.51.",
            "Later game builds are unverified. No dependency is required.",
            "Optional exact-version Hearthpyre 2.2.3 integration is included when Hearthpyre loads first; "
            "native compatibility remains unverified.",
            "Back up saves before every Alpha install or update.",
            "Keep only one enabled copy of the mod; a local install plus a Workshop subscription can load the wrong one.",
            "[url=https://github.com/AussieWarGod/thousand-and-first/issues/new/choose]GitHub issue forms[/url]",
            "[url=https://github.com/AussieWarGod/thousand-and-first/blob/main/PLAYTESTING.md]Alpha Playtesting Guide[/url]",
        ):
            with self.subTest(text=text):
                self.assertIn(text, description)
        self.assertTrue(METADATA._discloses_optional_cross_world_legacy(description))
        self.assertNotIn("instead of corrupting it", description)
        self.assertNotIn("paid art and design pass", description)

    def assert_metadata_copy_sync(self, repository: Path) -> None:
        manifest = METADATA.load_manifest(repository / "manifest.json")
        workshop_path = repository / "workshop.json"
        tags = ("Building", "Faction", "Settlement", "World", "Script", "Lore")
        self.assertEqual(tags, METADATA.TAGS)
        self.assertEqual(",".join(tags), manifest["tags"])
        self.assertEqual("The Thousand and First [ALPHA]", manifest["title"])
        proposal = (repository / "docs" / "WORKSHOP-DESCRIPTION-TEMPLATES.md").read_text(encoding="utf-8")
        approved = proposal.split("```text\n", 1)[1].split("\n```", 1)[0]
        self.assertEqual(approved, METADATA.canonical_description(manifest))
        short_copy = proposal.split("**Manifest description:**\n\n", 1)[1].split("\n\n", 1)[0]
        self.assertEqual(" ".join(line.removeprefix("> ") for line in short_copy.splitlines()),
                         manifest["description"])
        self.assertIn("not packaged or published as a new", proposal)
        if not workshop_path.exists():
            self.assertFalse(workshop_path.is_symlink())
            self.assertIsNone(METADATA.validate_workshop(workshop_path, manifest, "test"))
            return
        visibility = json.loads(workshop_path.read_text(encoding="utf-8")).get("Visibility")
        self.assertIn(visibility, ("0", "2"))
        mode = "test" if visibility == "0" else "alpha"
        workshop = METADATA.validate_workshop(workshop_path, manifest, mode)
        self.assertEqual(manifest["tags"], workshop["Tags"])
        self.assertEqual(manifest["title"], workshop["Title"])
        self.assertEqual(approved, workshop["Description"])
        self.assertEqual(METADATA.canonical_workshop_bytes(workshop), workshop_path.read_bytes())

    def test_tracked_alpha_metadata_matches_approved_copy_and_exact_six_tags(self) -> None:
        self.assert_metadata_copy_sync(Path(__file__).resolve().parents[2])

    def test_bootstrap_without_workshop_still_requires_approved_manifest_and_copy(self) -> None:
        repository = Path(__file__).resolve().parents[2]
        (self.root / "docs").mkdir()
        for relative in ("manifest.json", "docs/WORKSHOP-DESCRIPTION-TEMPLATES.md"):
            (self.root / relative).write_bytes((repository / relative).read_bytes())
        self.assertFalse((self.root / "workshop.json").exists())
        self.assert_metadata_copy_sync(self.root)
        manifest_path = self.root / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["description"] += " This unapproved sentence changes the short copy."
        manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
        with self.assertRaises(AssertionError):
            self.assert_metadata_copy_sync(self.root)

    def test_canonicalization_preserves_public_and_private_item_authority(self) -> None:
        manifest = METADATA.load_manifest(self.write_manifest(
            "Found a faction and govern settlements across Qud. Cross-world legacy is opt-in."
        ))
        path = self.root / "workshop.json"
        for mode, item_id, visibility in (("alpha", 3794797472, "2"), ("test", 123456789, "0")):
            with self.subTest(mode=mode):
                original = METADATA.canonical_workshop_data(manifest, item_id, visibility)
                original.update(Description="Previous listing copy", Tags="Alpha,Script")
                path.write_bytes(METADATA.canonical_workshop_bytes(original))
                METADATA.canonicalize_workshop(path, manifest, mode)
                updated = METADATA.validate_workshop(path, manifest, mode)
                self.assertEqual(original["WorkshopId"], updated["WorkshopId"])
                self.assertEqual(original["Visibility"], updated["Visibility"])
                self.assertEqual(METADATA.canonical_description(manifest), updated["Description"])
                self.assertEqual(",".join(METADATA.TAGS), updated["Tags"])

    def test_manifest_rejects_missing_negated_or_unbound_optional_legacy(self) -> None:
        descriptions = (
            "Optional difficulty settings change founding. Your kingdom's legacy "
            "carries across worlds without a separate player choice.",
            "Optional difficulty settings change founding while your kingdom's legacy "
            "carries across worlds without a separate player choice.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "not optional; it is mandatory.",
            "Found a faction and govern settlements across Qud. An optional legacy "
            "records events from this world only.",
            "Found a faction and govern settlements across Qud. You may optionally "
            "visit settlements across worlds.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It is always carried.",
            "Found a faction and govern settlements across Qud. Do not carry your history "
            "into the next world, if you choose.",
            "Found a faction and govern settlements across Qud. Never carry your legacy "
            "into a later world, if you choose.",
            "Found a faction and govern settlements across Qud. Choose to delete history "
            "rather than carry it into the next world.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "opt-in but cannot be disabled.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, with no opt-out.",
            "Found a faction and govern settlements across Qud. Opt in to prevent your "
            "history from carrying into the next world.",
            "Found a faction and govern settlements across Qud. Opt in to delete your "
            "legacy before it carries between worlds.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It has no opt-out.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It is always on.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It cannot be disabled.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, but mandatory.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional but required.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, without an opt-out.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional but has no off switch.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, but always enabled.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It is mandatory.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It is required.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It has no off switch.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. It is automatically enabled.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, but you cannot turn it off.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional, but there is no off switch.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. The legacy cannot be disabled.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. This feature is mandatory.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. You cannot turn it off.",
            "Found a faction and govern settlements across Qud. Cross-world legacy is "
            "optional. There is no off switch.",
            "Found a faction and govern settlements across Qud. Mandatory cross-world "
            "legacy is optional.",
            "Found a faction and govern settlements across Qud. Required cross-world "
            "legacy is optional.",
            "Found a faction and govern settlements across Qud. Automatic cross-world "
            "legacy is optional.",
            "Found a faction and govern settlements across Qud. Always-on cross-world "
            "legacy is optional.",
            "Found a faction and govern settlements across Qud. Non-optional cross-world "
            "legacy is optional.",
        )
        for description in descriptions:
            with self.subTest(description=description):
                with self.assertRaisesRegex(
                    METADATA.ValidationError,
                    "cross-world legacy is optional",
                ):
                    METADATA.load_manifest(self.write_manifest(description))

        oversized = (
            "Found a faction and govern settlements in Qud. Cross-world legacy is "
            "optional. "
            + "x" * 8000
        )
        with mock.patch.object(
            METADATA,
            "_discloses_optional_cross_world_legacy",
            side_effect=AssertionError("semantic grammar must not scan oversized copy"),
        ):
            with self.assertRaisesRegex(
                METADATA.ValidationError,
                "under 8000 UTF-8 bytes",
            ):
                METADATA.load_manifest(self.write_manifest(oversized))

    def test_canonical_workshop_bytes_match_qud_serializer_contract(self) -> None:
        data = {
            "WorkshopId": 7,
            "Title": "Salt ☃",
            "Description": 'line one\n"line two" \\ path',
            "Tags": "Alpha,Script",
            "Visibility": "2",
            "ImagePath": "preview.png",
        }
        expected = (
            "{\r\n"
            '  "WorkshopId": 7,\r\n'
            '  "Title": "Salt ☃",\r\n'
            '  "Description": "line one\\n\\"line two\\" \\\\ path",\r\n'
            '  "Tags": "Alpha,Script",\r\n'
            '  "Visibility": "2",\r\n'
            '  "ImagePath": "preview.png"\r\n'
            "}"
        ).encode("utf-8")
        self.assertEqual(METADATA.canonical_workshop_bytes(data), expected)

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
            "version": "0.3.0",
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
            "# Fixture\n\n**Status: 0.3.0 public Alpha playtest.**\n",
            encoding="utf-8",
        )
        changelog.write_text(
            "# Changelog\n\n## [0.3.0] — 2026-08-31 (Alpha)\n\nInitial public test.\n",
            encoding="utf-8",
        )
        receipt_hash = "a" * 64
        candidate = "b" * 40
        record = self.root / "ALPHA_CANDIDATE.json"
        payload = {
            "schemaVersion": METADATA.LEGACY_ALPHA_CANDIDATE_SCHEMA,
            "releaseChannel": METADATA.ALPHA_RELEASE_CHANNEL,
            "releaseVersion": "0.3.0",
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
            "# Changelog\n\n## [Unreleased] — 0.3.0 work in progress\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "Alpha release date"):
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            )

        manifest["version"] = "0.3.1"
        readme.write_text("**Status: 0.3.1 public Alpha playtest.**\n", encoding="utf-8")
        changelog.write_text("## [0.3.1] — 2026-09-01 (Alpha)\n", encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "version must match manifest"):
            METADATA.validate_alpha_candidate(manifest, preview, workshop, record, readme, changelog)

    def test_alpha_candidate_accepts_patch_update_and_keeps_exact_version_binding(self) -> None:
        version = "0.3.1"
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
                        "privateWorkshopId": 987654321,
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

        good = json.loads(record.read_text(encoding="utf-8"))
        for private_id in (None, 0, -1, True, False, 1.0, "123", METADATA.MAX_WORKSHOP_ID + 1, 123456789):
            with self.subTest(private_id=private_id):
                bad = dict(good, privateWorkshopId=private_id)
                record.write_text(json.dumps(bad), encoding="utf-8")
                with self.assertRaisesRegex(METADATA.ValidationError, "privateWorkshopId"):
                    METADATA.validate_alpha_candidate(manifest, preview, workshop, record, readme, changelog)
        legacy = dict(good, schemaVersion=1)
        del legacy["privateWorkshopId"]
        record.write_text(json.dumps(legacy), encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "historical 0.3.0 only"):
            METADATA.validate_alpha_candidate(manifest, preview, workshop, record, readme, changelog)
        record.write_text(json.dumps(good), encoding="utf-8")

        payload = json.loads(record.read_text(encoding="utf-8"))
        payload["releaseVersion"] = "0.3.0"
        record.write_text(json.dumps(payload) + "\n", encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "version must match manifest"):
            METADATA.validate_alpha_candidate(
                manifest, preview, workshop, record, readme, changelog
            )

        for invalid_version in ("0.3.01", "0.4.0", "1.0.0"):
            with self.subTest(version=invalid_version):
                write_version_bound_fixture(invalid_version)
                with self.assertRaisesRegex(
                    METADATA.ValidationError, "0.3.0 or a later canonical 0.3.x patch"
                ):
                    METADATA.validate_alpha_candidate(
                        manifest, preview, workshop, record, readme, changelog
                    )

    def test_alpha_workshop_binding_proves_two_distinct_items_and_preserves_every_input(self) -> None:
        paths = self.write_alpha_binding_fixture()
        before = [path.read_bytes() for path in paths]
        self.assertEqual(METADATA.validate_alpha_workshop_binding(*paths), (123456789, 3794797472))
        self.assertEqual([path.read_bytes() for path in paths], before)
        public = json.loads(paths[2].read_text(encoding="utf-8"))
        self.assertEqual(public["WorkshopId"], 3794797472)
        self.assertEqual(public["Visibility"], "2")

    def test_alpha_binding_accepts_exact_unsigned_id_bounds_without_float_coercion(self) -> None:
        for private_id, public_id in ((1, METADATA.MAX_WORKSHOP_ID), (METADATA.MAX_WORKSHOP_ID, 1)):
            with self.subTest(private_id=private_id):
                paths = self.write_alpha_binding_fixture()
                record, private, public = [json.loads(path.read_text(encoding="utf-8")) for path in paths]
                record.update(privateWorkshopId=private_id, workshopId=public_id)
                private["WorkshopId"], public["WorkshopId"] = private_id, public_id
                for path, value in zip(paths, (record, private, public)):
                    path.write_text(json.dumps(value), encoding="utf-8")
                self.assertEqual(METADATA.validate_alpha_workshop_binding(*paths), (private_id, public_id))

    def test_alpha_binding_refuses_missing_wrong_typed_out_of_range_and_forged_ids(self) -> None:
        missing = object()
        for index, field in ((0, "privateWorkshopId"), (0, "workshopId"), (1, "WorkshopId"), (2, "WorkshopId")):
            for value in (missing, None, 0, -1, True, False, 1.0, "123", METADATA.MAX_WORKSHOP_ID + 1, 7654321):
                with self.subTest(index=index, field=field, value=str(value)):
                    paths = self.write_alpha_binding_fixture()
                    changed = json.loads(paths[index].read_text(encoding="utf-8"))
                    if value is missing:
                        del changed[field]
                    else:
                        changed[field] = value
                    paths[index].write_text(json.dumps(changed), encoding="utf-8")
                    before = [path.read_bytes() for path in paths]
                    with self.assertRaises(METADATA.ValidationError):
                        METADATA.validate_alpha_workshop_binding(*paths)
                    self.assertEqual([path.read_bytes() for path in paths], before)
        paths = self.write_alpha_binding_fixture()
        record = json.loads(paths[0].read_text(encoding="utf-8"))
        record["privateWorkshopId"] = record["workshopId"]
        paths[0].write_text(json.dumps(record), encoding="utf-8")
        private = json.loads(paths[1].read_text(encoding="utf-8"))
        private["WorkshopId"] = record["workshopId"]
        paths[1].write_text(json.dumps(private), encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "must differ"):
            METADATA.validate_alpha_workshop_binding(*paths)

    def test_alpha_binding_requires_exact_private_and_public_visibility(self) -> None:
        for index, values in ((1, (None, 0, False, "2", "1", "00")), (2, (None, 2, True, "0", "1", "02"))):
            for value in values:
                with self.subTest(index=index, value=value):
                    paths = self.write_alpha_binding_fixture()
                    payload = json.loads(paths[index].read_text(encoding="utf-8"))
                    if value is None:
                        del payload["Visibility"]
                    else:
                        payload["Visibility"] = value
                    paths[index].write_text(json.dumps(payload), encoding="utf-8")
                    with self.assertRaisesRegex(METADATA.ValidationError, "Visibility"):
                        METADATA.validate_alpha_workshop_binding(*paths)

    def test_alpha_binding_legacy_same_item_is_readable_only_for_historical_first_alpha(self) -> None:
        paths = self.write_alpha_binding_fixture("0.3.0", 1)
        original = [path.read_bytes() for path in paths]
        self.assertEqual(METADATA.validate_alpha_workshop_binding(*paths), (3794797472, 3794797472))
        self.assertEqual([path.read_bytes() for path in paths], original)
        private = json.loads(paths[1].read_text(encoding="utf-8"))
        private["WorkshopId"] = 123456789
        paths[1].write_text(json.dumps(private), encoding="utf-8")
        with self.assertRaisesRegex(METADATA.ValidationError, "IDs do not match"):
            METADATA.validate_alpha_workshop_binding(*paths)
        for version in ("0.3.1", "0.3.12", "0.3.01", "0.4.0"):
            with self.subTest(version=version):
                with self.assertRaises(METADATA.ValidationError):
                    METADATA.validate_alpha_workshop_binding(*self.write_alpha_binding_fixture(version, 1))

    def test_alpha_binding_validates_whole_record_schema_not_only_selected_ids(self) -> None:
        corruptions = {
            "schemaVersion": (None, True, 2.0, 0, 3),
            "releaseVersion": (None, "0.3.01", "0.4.0"),
            "releaseChannel": ("release",), "candidateCommit": ("not-a-commit",),
            "gameMarketingVersion": ("unknown",), "gameCoreBuild": ("unknown",),
            "previewSha256": ("0" * 64, METADATA.INTERIM_PREVIEW_SHA256),
            "privatePackageReceiptSha256": (None, "0" * 64), "unexpected": (True,),
        }
        for field, values in corruptions.items():
            for value in values:
                with self.subTest(field=field, value=value):
                    paths = self.write_alpha_binding_fixture()
                    record = json.loads(paths[0].read_text(encoding="utf-8"))
                    record[field] = value
                    paths[0].write_text(json.dumps(record), encoding="utf-8")
                    with self.assertRaises(METADATA.ValidationError):
                        METADATA.validate_alpha_workshop_binding(*paths)

    def test_alpha_binding_refuses_duplicate_authority_fields_and_missing_files(self) -> None:
        for index, field, value in ((0, "privateWorkshopId", 123456789), (1, "WorkshopId", 123456789),
                                    (2, "WorkshopId", 3794797472), (2, "Visibility", "2")):
            with self.subTest(index=index, field=field):
                paths = self.write_alpha_binding_fixture()
                text = paths[index].read_text(encoding="utf-8").rstrip()
                paths[index].write_text(text[:-1] + "," + json.dumps(field) + ":" + json.dumps(value) + "}", encoding="utf-8")
                with self.assertRaisesRegex(METADATA.ValidationError, "duplicate JSON field"):
                    METADATA.validate_alpha_workshop_binding(*paths)
        for index in range(3):
            with self.subTest(missing=index):
                paths = list(self.write_alpha_binding_fixture())
                paths[index] = self.root / "absent.json"
                with self.assertRaisesRegex(METADATA.ValidationError, "cannot read"):
                    METADATA.validate_alpha_workshop_binding(*paths)

    def test_alpha_binding_cli_is_silent_on_success_and_diagnostic_on_refusal(self) -> None:
        paths = self.write_alpha_binding_fixture()
        arguments = ["alpha-workshop-binding", *(str(path) for path in paths)]
        stdout, stderr = io.StringIO(), io.StringIO()
        with redirect_stdout(stdout), redirect_stderr(stderr):
            self.assertEqual(METADATA.main(arguments), 0)
        self.assertEqual(stdout.getvalue(), ""); self.assertEqual(stderr.getvalue(), "")
        public = paths[2].read_bytes()
        bad = json.loads(paths[1].read_text(encoding="utf-8")); bad["Visibility"] = "2"
        paths[1].write_text(json.dumps(bad), encoding="utf-8")
        stdout, stderr = io.StringIO(), io.StringIO()
        with redirect_stdout(stdout), redirect_stderr(stderr):
            self.assertEqual(METADATA.main(arguments), 1)
        self.assertEqual(stdout.getvalue(), ""); self.assertIn("Visibility", stderr.getvalue())
        self.assertEqual(paths[2].read_bytes(), public)

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
