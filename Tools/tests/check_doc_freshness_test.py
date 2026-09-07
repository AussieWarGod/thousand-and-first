#!/usr/bin/env python3
"""Focused tests for maintained-document freshness contracts."""

from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


CHECKER_PATH = Path(__file__).resolve().parents[1] / "check-doc-freshness.py"
SPEC = importlib.util.spec_from_file_location("taf_check_doc_freshness", CHECKER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


class DocumentationFreshnessTests(unittest.TestCase):
    MARKET_DOCUMENTS = (
        "VISION.md", "docs/STATUS.md", "TESTING.md", "docs/API.md", "MODDING.md",
        "CHANGELOG.md", "_notes/BRIEF-IMPLEMENTATION-AUDIT.md", "_notes/balance-sim-output.txt",
    )
    MARKET_LOCAL_NOTE = "_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md"

    def copy_market_documents(self, root: Path) -> None:
        for relative in self.MARKET_DOCUMENTS:
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((CHECKER_PATH.parent.parent / relative).read_bytes())

    def test_market_contract_accepts_absent_ignored_local_notes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_market_documents(root)
            self.assertFalse((root / self.MARKET_LOCAL_NOTE).exists())
            with mock.patch.object(CHECKER, "ROOT", root):
                problems = []
                CHECKER.audit_market_contract(problems)
            self.assertEqual([], problems)

    def test_market_contract_audits_present_local_note_requirements_and_forbidden_claims(self) -> None:
        required = (
            "Native TradeUI `_stock` is sole ordinary ware authority",
            "`ShopTier` is current reach and may fall to zero",
            "Completed/dormant legendary merchants survive civic loss/accession without civic authority",
        )
        current = "\n".join(required)
        stale = "stock tier rises with the settlement"
        cases = (
            (current, []),
            ("\n".join(required[:2]), [
                f"{self.MARKET_LOCAL_NOTE} is missing current contract text: {required[2]}"
            ]),
            (current + "\n" + stale, [
                f"{self.MARKET_LOCAL_NOTE} retains stale current-status text: {stale}"
            ]),
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_market_documents(root)
            for body, expected in cases:
                with self.subTest(body=body), mock.patch.object(CHECKER, "ROOT", root):
                    note = root / self.MARKET_LOCAL_NOTE
                    note.write_text(body, encoding="utf-8")
                    problems = []
                    CHECKER.audit_market_contract(problems)
                    self.assertEqual(expected, problems)
                    self.assertEqual(body, note.read_text(encoding="utf-8"))

    def test_market_contract_keeps_public_documents_and_tracked_note_mandatory(self) -> None:
        for relative in self.MARKET_DOCUMENTS[:-1]:
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                self.copy_market_documents(root)
                missing = root / relative
                missing.unlink()
                with mock.patch.object(CHECKER, "ROOT", root):
                    with self.assertRaises(FileNotFoundError) as refused:
                        CHECKER.audit_market_contract([])
                self.assertEqual(str(missing), refused.exception.filename)

    def test_public_alpha_status_rejects_the_pre_publication_claim(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                readme = CHECKER.ROOT / "README.md"
                status = CHECKER.ROOT / "docs" / "STATUS.md"
                alpha_plan = CHECKER.ROOT / "docs" / "ALPHA-RELEASE-PLAN.md"
                playtesting = CHECKER.ROOT / "PLAYTESTING.md"
                testing = CHECKER.ROOT / "TESTING.md"
                modding = CHECKER.ROOT / "MODDING.md"
                status.parent.mkdir(parents=True)
                status.write_text(
                    "0.3.1 public Alpha playtest. Its old counts do not sign later bytes. "
                    f"{CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}\n",
                    encoding="utf-8",
                )
                readme.write_text(
                    f"Public Alpha is published at {CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}.\n",
                    encoding="utf-8",
                )
                alpha_plan.write_text(
                    "Completed state. The annotated tag `v0.3.0` is published at "
                    f"{CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}.\n",
                    encoding="utf-8",
                )
                playtesting.write_text(
                    f"Install the public `0.3.0` Alpha at {CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}.\n",
                    encoding="utf-8",
                )
                testing.write_text(
                    "The current public Alpha manifest is `0.3.0`.\n",
                    encoding="utf-8",
                )
                modding.write_text(
                    'Fixture dependency: "r_ThousandAndFirst": "0.3.0".\n',
                    encoding="utf-8",
                )
                problems = []
                CHECKER.audit_public_release_status(problems)
                self.assertEqual([], problems)

                readme.write_text(
                    f"Public Alpha is not published yet. {CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}\n",
                    encoding="utf-8",
                )
                problems = []
                CHECKER.audit_public_release_status(problems)
                self.assertEqual(1, len(problems))
                self.assertIn("Public Alpha is not published yet", problems[0])

                readme.write_text(
                    f"Public Alpha is published at {CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}.\n",
                    encoding="utf-8",
                )
                status.write_text(
                    "0.2.0 work in progress. Its old counts do not sign later bytes. "
                    f"{CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}\n",
                    encoding="utf-8",
                )
                problems = []
                CHECKER.audit_public_release_status(problems)
                self.assertEqual(2, len(problems))
                self.assertTrue(
                    any("0.3.1 public Alpha playtest" in problem for problem in problems)
                )
                self.assertTrue(any("0.2.0 work in progress" in problem for problem in problems))

                status.write_text(
                    "0.3.1 public Alpha playtest. Its old counts do not sign later bytes. "
                    f"{CHECKER.PUBLIC_ALPHA_WORKSHOP_URL}\n",
                    encoding="utf-8",
                )
                stale_cases = (
                    (
                        alpha_plan,
                        "`manifest.json` remains `0.2.0`",
                    ),
                    (
                        playtesting,
                        "Once the public Alpha item exists",
                    ),
                    (
                        testing,
                        "manifest remains `0.2.0`",
                    ),
                    (
                        modding,
                        '"r_ThousandAndFirst": "0.2.0"',
                    ),
                )
                for path, stale_text in stale_cases:
                    with self.subTest(path=path.name):
                        current_text = path.read_text(encoding="utf-8")
                        path.write_text(stale_text + "\n", encoding="utf-8")
                        problems = []
                        CHECKER.audit_public_release_status(problems)
                        self.assertTrue(any(stale_text in problem for problem in problems))
                        path.write_text(current_text, encoding="utf-8")
            finally:
                CHECKER.ROOT = original_root

    def test_current_research_disposition_is_machine_guarded(self) -> None:
        problems = []
        CHECKER.audit_research_alignment_contract(problems)
        self.assertEqual([], problems)

    def test_archive_contract_tracks_current_v19_and_historical_v1_to_v18(self) -> None:
        problems = []
        CHECKER.audit_archive_contract(problems)
        self.assertEqual([], problems)

    def test_archive_contract_rejects_stale_alias_reader_or_subsidence_payload(self) -> None:
        mutations = (
            ("Core/KingdomArchivedSettlementCodec.cs",
             "public const int CurrentVersion = SubsidenceStorageVersion;",
             "public const int CurrentVersion = ExpeditionResultVersion;"),
            ("Core/KingdomArchivedSettlementCodec.cs",
             "public const int SubsidenceStorageVersion = 19;",
             "public const int SubsidenceStorageVersion = 18;"),
            ("Core/KingdomArchivedSettlementCodec.EncodeV1ToV4.cs",
             "TryEncodeExpeditionResultV18ForTests", "RemovedHistoricalWriter"),
            ("Core/KingdomArchivedSettlementCodec.DecodeCloneHash.cs",
             "&& version != ExpeditionResultVersion", "&& version != SubsidenceStorageVersion"),
            ("Core/KingdomArchivedSettlementCodec.Schema.cs",
             'string.Equals(Name, "SubsidenceModel", StringComparison.Ordinal)) return false;',
             'string.Equals(Name, "UnprovedModel", StringComparison.Ordinal)) return false;'),
            ("Core/KingdomArchivedSettlementCodec.ValueReader.cs",
             "KingdomSubsidenceStepCodec.MaxWireChars", "MaxStringBytes"),
            ("Core/KingdomArchivedSettlementCodec.ValueWriter.cs",
             "KingdomSubsidenceStepCodec.MaxWireChars", "MaxStringBytes"),
            ("Core/KingdomArchivedSettlementCodec.ValueReader.cs",
             "if (!city.TryMigrateSubsidenceStorage())", "if (false)"),
        )
        read_text = Path.read_text
        for relative, before, after in mutations:
            with self.subTest(relative=relative, before=before):
                target = CHECKER.ROOT / relative
                self.assertIn(before, read_text(target, encoding="utf-8"))

                def changed(path, *args, **kwargs):
                    actual = read_text(path, *args, **kwargs)
                    return actual.replace(before, after) if path == target else actual

                # Overlay only this source read. All real document requirements still execute;
                # no private notes are invented/copied and no source file is changed.
                with mock.patch.object(Path, "read_text", changed):
                    problems = []
                    CHECKER.audit_archive_contract(problems)
                self.assertTrue(problems, "stale archive contract escaped the audit")
                self.assertTrue(any("archive source" in problem or relative in problem
                                    for problem in problems), problems)

    def test_green_line_cap_keeps_human_structure_review_open(self) -> None:
        terms = CHECKER.changelog_structure_status_terms(2458, 0)
        self.assertEqual(
            (
                "Current 2458-file census is line-cap green",
                "Addendum 9 line-cap debt is cleared",
                "exact-inventory human semantic review remains a release blocker",
            ),
            terms,
        )
        self.assertNotIn("remains red", " ".join(terms))

    def test_line_cap_breaches_keep_executable_gate_red(self) -> None:
        self.assertEqual(
            (
                "Current 42-file census remains red",
                "Addendum 9 structural debt is now an executable release blocker",
            ),
            CHECKER.changelog_structure_status_terms(42, 1),
        )

    def test_stale_resolvable_source_citation_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                (CHECKER.ROOT / "Source.cs").write_text("one\ntwo\n", encoding="utf-8")
                (CHECKER.ROOT / "Guide.md").write_text(
                    "Evidence: `Source.cs:2-3`.\n", encoding="utf-8"
                )
                problems = []
                CHECKER.audit_source_citations(problems)
                self.assertEqual(1, len(problems))
                self.assertIn(
                    "Guide.md:1 has stale source citation `Source.cs:2-3`", problems[0]
                )
                self.assertIn("has 2 lines", problems[0])
            finally:
                CHECKER.ROOT = original_root

    def test_current_and_external_alias_citations_are_accepted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                (CHECKER.ROOT / "Source.cs").write_text("one\ntwo\n", encoding="utf-8")
                (CHECKER.ROOT / "Guide.md").write_text(
                    "Local `Source.cs:1-2`; installed `B/Source.cs:999`.\n",
                    encoding="utf-8",
                )
                problems = []
                CHECKER.audit_source_citations(problems)
                self.assertEqual([], problems)
            finally:
                CHECKER.ROOT = original_root

    def test_in_range_citation_to_declaration_only_split_anchor_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                (CHECKER.ROOT / "Source.cs").write_text(
                    "namespace Example\n{\npublic static partial class Source\n{\n}\n}\n",
                    encoding="utf-8",
                )
                (CHECKER.ROOT / "Source.00.Moved.cs").write_text(
                    "namespace Example { public static partial class Source { public static void Moved() {} } }\n",
                    encoding="utf-8",
                )
                (CHECKER.ROOT / "Guide.md").write_text(
                    "Stale member evidence: `Source.cs:3-4`.\n", encoding="utf-8"
                )
                problems = []
                CHECKER.audit_source_citations(problems)
                self.assertEqual(1, len(problems))
                self.assertIn("cites declaration-only split anchor", problems[0])
                self.assertIn("cite the exact shard or logical family plus symbol", problems[0])
            finally:
                CHECKER.ROOT = original_root

    def test_frozen_research_citations_are_not_repointed_to_current_sources(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                (CHECKER.ROOT / "Source.cs").write_text("one\n", encoding="utf-8")
                frozen = CHECKER.ROOT / "_notes" / "COVERAGE-GAP-MAP.md"
                frozen.parent.mkdir(parents=True)
                frozen.write_text("Pinned evidence: `Source.cs:99`.\n", encoding="utf-8")
                (CHECKER.ROOT / "Current.md").write_text(
                    "Live evidence: `Source.cs:99`.\n", encoding="utf-8"
                )
                problems = []
                CHECKER.audit_source_citations(problems)
                self.assertEqual(1, len(problems))
                self.assertIn("Current.md:1", problems[0])
                self.assertNotIn("COVERAGE-GAP-MAP", problems[0])
            finally:
                CHECKER.ROOT = original_root

    def test_read_only_audit_snapshots_keep_pinned_source_citations(self) -> None:
        self.assertIn(
            "_notes/ARCOLOGY-AUTHORED-INTERIOR-PLAN.md",
            CHECKER.FROZEN_SOURCE_CITATION_DOCUMENTS,
        )
        self.assertIn(
            "_notes/ARCHITECTURE-POLISH-DISK-AUDIT.md",
            CHECKER.FROZEN_SOURCE_CITATION_DOCUMENTS,
        )
        self.assertIn(
            "_notes/FOUNDATION-RUNTIME-FULL-AUDIT-CLAUDE.md",
            CHECKER.FROZEN_SOURCE_CITATION_DOCUMENTS,
        )

    def test_optional_local_note_is_skipped_when_public_checkout_omits_it(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                problems = []
                CHECKER.require_if_present(problems, "_notes/LOCAL.md", "current contract")
                self.assertEqual([], problems)
            finally:
                CHECKER.ROOT = original_root

    def test_optional_local_note_is_audited_when_present(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            original_root = CHECKER.ROOT
            CHECKER.ROOT = Path(temporary)
            try:
                note = CHECKER.ROOT / "_notes" / "LOCAL.md"
                note.parent.mkdir(parents=True)
                note.write_text("stale text\n", encoding="utf-8")
                problems = []
                CHECKER.require_if_present(problems, "_notes/LOCAL.md", "current contract")
                self.assertEqual(
                    ["_notes/LOCAL.md is missing current contract text: current contract"],
                    problems,
                )
            finally:
                CHECKER.ROOT = original_root


if __name__ == "__main__":
    unittest.main()
