#!/usr/bin/env python3
"""Pins the host-side gallery-case mirror to live in-game anchors.

The expected rows below were transcribed from the running game's
`kingdom:archgallery list` ordering and the current deterministic catalogue
census. If this test fails, the mirror's ordering laws have
drifted from Debug/KingdomArchitectureGalleryWishes.Staging.cs — fix the
mirror, never the anchors, unless the in-game enumeration itself changed.
"""

from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MIRROR_PATH = ROOT / "Tools" / "gallery_cases.py"

LIVE_ANCHORS = {
    1: "airwellcourt|storage|Medium|eater-reuse|North",
    259: "deepbore|craft|Huge|fallback|South",
    273: "dromadcaravanshade|storage|Small|fallback|North",
    276: "dromadcaravanshade|storage|Small|fallback|West",
    295: "entropyblind|knowledge|Medium|fallback|South",
    302: "entropyblind|knowledge|Huge|fallback|East",
    429: "goatfolkhornmoot|civic|Large|fallback|North",
    1372: "ydvinebower|food|Large|fallback|West",
    1376: "ydvinebower|food|Huge|fallback|West",
}
LIVE_TOTAL_CASES = 1376
LIVE_PAGES = 77
PAGE_ROWS = 18


def load_mirror():
    spec = importlib.util.spec_from_file_location("gallery_cases", MIRROR_PATH)
    module = importlib.util.module_from_spec(spec)
    import sys

    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class GalleryCasesMirrorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.mirror = load_mirror()
        records, streams = cls.mirror.load_records(ROOT)
        cls.streams = streams
        cls.cases = cls.mirror.enumerate_cases(records)

    def test_live_anchor_rows_match(self) -> None:
        for number, expected in LIVE_ANCHORS.items():
            line = self.cases[number - 1].line()
            self.assertEqual(line, f"{number}\t{expected}")

    def test_census_matches_live_game(self) -> None:
        self.assertEqual(len(self.cases), LIVE_TOTAL_CASES)
        self.assertEqual(-(-len(self.cases) // PAGE_ROWS), LIVE_PAGES)

    def test_numbering_is_dense_and_one_based(self) -> None:
        numbers = [case.number for case in self.cases]
        self.assertEqual(numbers, list(range(1, len(self.cases) + 1)))

    def test_every_stream_was_read(self) -> None:
        authored = [
            path
            for path in (ROOT / "Architecture").glob("*.xml")
            if "<KingdomArchitectures" in path.read_text(encoding="utf-8")
        ]
        self.assertEqual(self.streams, len(authored))

    def test_every_authored_scenario_selector_names_one_exact_case(self) -> None:
        checked, errors = self.mirror.validate_scenario_cases(ROOT, self.cases)
        self.assertEqual(errors, [])
        # Forty-four architecture scenarios each expand across the four-pose domain. Eight are the
        # M/L/XL housing-progression review matrix; their personas freeze North, but the roster
        # must retain all four legal poses for manual review.
        self.assertEqual(checked, 176)


if __name__ == "__main__":
    unittest.main()
