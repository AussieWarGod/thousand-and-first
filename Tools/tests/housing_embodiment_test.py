#!/usr/bin/env python3
"""Exact shipped housing-provider census across every tier, variant, and pose."""

from __future__ import annotations

import importlib.util
import re
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "taf_housing_architecture", ROOT / "Tools" / "check-architecture.py"
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("cannot load architecture checker")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


class HousingEmbodimentTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        issues = []
        cls.buildings = CHECKER.load_buildings(
            sorted(ROOT.rglob("KingdomBuildings*.xml")), ROOT, issues
        )
        cls.model = CHECKER.load_architectures(
            sorted(ROOT.rglob("KingdomArchitectures*.xml")), ROOT, issues
        )
        cls.assertFalse(cls, issues, "shipped architecture inputs must parse")

    def test_every_roof_ceiling_is_literal_in_every_variant_and_pose(self) -> None:
        witnessed = 0
        for tier in self.model.tiers:
            building = self.buildings.get(tier.build_key)
            if building is None:
                continue
            ceiling = CHECKER._roof_ceiling(building)
            if ceiling <= 0:
                continue
            for variant in tier.variants:
                architecture_map = self.model.maps[variant.map_key or tier.map_key]
                palette = self.model.palettes[variant.palette_key or tier.palette_key]
                cells = CHECKER._sleep_provider_cells(architecture_map, palette)
                label = f"{tier.build_key}/{tier.key}/{variant.key}"
                self.assertEqual(ceiling, len(cells), label)
                for pose in CHECKER.POSES:
                    posed = {
                        CHECKER._pose_point(
                            x, y, architecture_map.width, architecture_map.height, pose
                        )
                        for x, y, glyph in cells
                        if glyph.claim == "building"
                        and (glyph.cover or architecture_map.default_cover) != "open"
                    }
                    self.assertEqual(ceiling, len(posed), f"{label}/{pose}")
                witnessed += 1
        self.assertGreater(witnessed, 0)

    def test_arcology_shell_promises_no_unfurnished_roof(self) -> None:
        arcology = self.buildings["arcology"]
        self.assertEqual(0, CHECKER._roof_ceiling(arcology))
        self.assertNotRegex(arcology.attributes.get("Carries", ""), r"(^|,)roof:")


if __name__ == "__main__":
    unittest.main()
