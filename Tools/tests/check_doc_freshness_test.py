#!/usr/bin/env python3
"""Focused tests for optional ignored-note freshness checks."""

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
