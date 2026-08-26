#!/usr/bin/env python3
"""Determinism and concrete-output tests for the exact-lot materialiser."""

from __future__ import annotations

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


GENERATOR_PATH = Path(__file__).resolve().parents[1] / "generate-lot-realizations.py"
SPEC = importlib.util.spec_from_file_location("taf_generate_lot_realizations", GENERATOR_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {GENERATOR_PATH}")
GENERATOR = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = GENERATOR
SPEC.loader.exec_module(GENERATOR)


class LotRealizationGeneratorTests(unittest.TestCase):
    def test_checked_in_output_is_current_concrete_and_exact(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        expected, map_count, plan_count, tier_count = GENERATOR.generate(repository)
        target = repository / "Architecture" / GENERATOR.OUTPUT_NAME
        self.assertEqual(target.read_text(encoding="utf-8"), expected)
        self.assertEqual((map_count, plan_count, tier_count), (337, 242, 277))

        root = ET.fromstring(expected)
        maps = {item.get("Key"): item for item in root.findall("map")}
        self.assertEqual(len(maps), map_count)
        self.assertEqual(len(root.findall("plan")), plan_count)
        self.assertEqual(
            GENERATOR.IDENTITY_SELECTOR_ATTRIBUTES,
            ("Cultures", "Species", "Genotypes", "Bodies"),
        )
        variants = root.findall("./plan/binding/tier/variant")
        self.assertTrue(any(item.get("Cultures") == "Hindren" for item in variants))
        self.assertTrue(any(item.get("Species") == "hindren" for item in variants))
        self.assertTrue(any(item.get("Bodies") == "robot" for item in variants))
        for binding in root.findall("./plan/binding"):
            size = binding.get("Size")
            self.assertIn(size, GENERATOR.LOT_DIMENSIONS)
            dimensions = GENERATOR.LOT_DIMENSIONS[size]
            for tier in binding.findall("tier"):
                keys = [tier.get("Map")]
                keys.extend(
                    variant.get("Map")
                    for variant in tier.findall("variant")
                    if variant.get("Map")
                )
                for key in keys:
                    architecture_map = maps[key]
                    self.assertEqual(
                        (int(architecture_map.get("Width")), int(architecture_map.get("Height"))),
                        dimensions,
                    )

    def test_every_generated_road_entrance_has_an_exterior_cell(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        expected, _maps, _plans, _tiers = GENERATOR.generate(repository)
        root = ET.fromstring(expected)
        maps = {item.get("Key"): item for item in root.findall("map")}
        for binding in root.findall("./plan/binding"):
            if binding.get("Facing") != "road":
                continue
            for tier in binding.findall("tier"):
                keys = {tier.get("Map")}
                keys.update(
                    variant.get("Map")
                    for variant in tier.findall("variant")
                    if variant.get("Map")
                )
                for key in keys:
                    architecture_map = maps[key]
                    width = int(architecture_map.get("Width"))
                    height = int(architecture_map.get("Height"))
                    entrances = GENERATOR._public_entrances(architecture_map)
                    self.assertTrue(entrances, key)
                    for x, y in entrances:
                        self.assertTrue(
                            x in {0, width - 1} or y in {0, height - 1},
                            f"{key} entrance {x},{y} cannot reach exterior road evidence",
                        )


if __name__ == "__main__":
    unittest.main()
