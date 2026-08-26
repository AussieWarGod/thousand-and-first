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
