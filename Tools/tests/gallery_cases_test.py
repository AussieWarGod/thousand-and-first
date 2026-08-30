#!/usr/bin/env python3
"""Pins the host-side gallery-case mirror to live in-game anchors.

The expected rows below were transcribed from the running game's
`kingdom:archgallery list` pages (2026-08-30 session, pages 1, 14, 16) and
its 118-page census. If this test fails, the mirror's ordering laws have
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
    235: "caproof|housing|Small|fallback|South",
    249: "caravanserai|civic|Large|fallback|North",
    252: "caravanserai|civic|Large|fallback|West",
    271: "carvedcell|housing|Huge|fallback|South",
    285: "catchment|storage|Small|fallback|North",
    397: "court|housing|Huge|fallback|North",
}
LIVE_TOTAL_CASES = 2120
LIVE_PAGES = 118
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


if __name__ == "__main__":
    unittest.main()
