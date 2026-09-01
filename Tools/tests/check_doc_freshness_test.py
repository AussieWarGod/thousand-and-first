#!/usr/bin/env python3
"""Focused tests for maintained-document freshness contracts."""

from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


CHECKER_PATH = Path(__file__).resolve().parents[1] / "check-doc-freshness.py"
SPEC = importlib.util.spec_from_file_location("taf_check_doc_freshness", CHECKER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


class DocumentationFreshnessTests(unittest.TestCase):
    def test_current_research_disposition_is_machine_guarded(self) -> None:
        problems = []
        CHECKER.audit_research_alignment_contract(problems)
        self.assertEqual([], problems)

    def test_archive_contract_tracks_current_v17_and_frozen_v1_to_v16(self) -> None:
        problems = []
        CHECKER.audit_archive_contract(problems)
        self.assertEqual([], problems)

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
