#!/usr/bin/env python3
"""Cross-language guard for canonical settlement lot geometry."""

from __future__ import annotations

import importlib.util
import re
import sys
import unittest
from pathlib import Path


REPOSITORY = Path(__file__).resolve().parents[2]


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


CHECKER = load("taf_geometry_checker", REPOSITORY / "Tools" / "check-architecture.py")
GENERATOR = load(
    "taf_geometry_generator", REPOSITORY / "Tools" / "generate-lot-realizations.py"
)


class ArchitectureGeometryAuthorityTests(unittest.TestCase):
    def test_python_mirrors_match_canonical_csharp_plot_authority(self) -> None:
        source = (REPOSITORY / "Growth" / "KingdomPlotRules.cs").read_text(
            encoding="utf-8"
        )

        def constant(name: str) -> int:
            match = re.search(
                rf"public\s+const\s+int\s+{re.escape(name)}\s*=\s*(\d+)\s*;",
                source,
            )
            self.assertIsNotNone(match, name)
            return int(match.group(1))

        canonical = {
            "S": (constant("SmallWidth"), constant("SmallHeight")),
            "M": (constant("MediumWidth"), constant("MediumHeight")),
            "L": (constant("LargeWidth"), constant("LargeHeight")),
            "XL": (constant("HugeWidth"), constant("HugeHeight")),
        }
        expected = {"S": (6, 4), "M": (8, 6), "L": (12, 10), "XL": (20, 18)}
        self.assertEqual(expected, canonical)
        self.assertEqual(canonical, dict(CHECKER.LOT_DIMENSIONS))
        self.assertEqual(canonical, GENERATOR.LOT_DIMENSIONS)
        self.assertTrue(
            all(width % 2 == 0 and height % 2 == 0 for width, height in canonical.values())
        )

    def test_architecture_runtime_delegates_dimensions_to_plot_authority(self) -> None:
        source = (REPOSITORY / "Growth" / "KingdomArchitectureRules.cs").read_text(
            encoding="utf-8"
        )
        for size in ("Small", "Medium", "Large", "Huge"):
            self.assertRegex(
                source,
                rf"case ArchitectureLotSize\.{size}:\s*"
                rf"return KingdomPlotRules\.TryDimensions\("
                rf"KingdomPlotRules\.PlotSize\.{size},\s*out Width, out Height\);",
            )

    def test_capacity_mirrors_are_exact_and_geometry_bounded(self) -> None:
        self.assertEqual(20 * 18, CHECKER.MAX_MAP_AREA)
        self.assertEqual(CHECKER.MAX_MAP_AREA * 2, CHECKER.MAX_PLACEMENTS)
        self.assertEqual(12 * 1024, CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES)
        exact_text_envelope = CHECKER.SNAPSHOT_TEXT_OVERHEAD + 4 * (
            (CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES + 3) // 3
        )
        self.assertEqual(exact_text_envelope, CHECKER.MAX_SNAPSHOT_CHARS)


if __name__ == "__main__":
    unittest.main()
