"""Taste guards for the two authored becoming-annexe XL plans."""

from __future__ import annotations

import itertools
import json
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ARCHITECTURE = ROOT / "Architecture" / "KingdomArchitectures-DeepEndgame.xml"
REFERENCE = ROOT / "Tools" / "architecture-quality-reference.json"
MAP_KEYS = (
    "deepend-becomingannexe-xl0",
    "deepend-becomingannexe-truekin-xl0",
)
REFERENCE_MAP = "CrematoryMachineRoom.rpm"


class BecomingAnnexeTasteTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = ET.parse(ARCHITECTURE).getroot()
        cls.maps = {item.get("Key"): item for item in cls.root.findall("map")}
        manifest = json.loads(REFERENCE.read_text(encoding="utf-8"))
        cls.reference = next(
            item
            for item in manifest["named_map_references"]
            if item["path"] == REFERENCE_MAP
        )

    @staticmethod
    def light_cells(architecture_map: ET.Element) -> list[tuple[int, int, ET.Element]]:
        glyphs = {
            glyph.get("Char", ""): glyph
            for glyph in architecture_map.findall("glyph")
        }
        cells = []
        for y, row in enumerate(architecture_map.findall("row")):
            for x, char in enumerate(row.get("Cells", "")):
                glyph = glyphs.get(char)
                if glyph is not None and glyph.get("Object") == "$light":
                    cells.append((x, y, glyph))
        return cells

    def test_light_budget_is_below_the_pinned_vanilla_machine_room(self) -> None:
        evidence = self.reference["taste_evidence"]
        self.assertEqual("Tomb Techlight2", evidence["blueprint"])
        self.assertEqual(5, evidence["count"])
        self.assertEqual(evidence["count"], len(evidence["coordinates"]))

        # The installed named machine room spends five arc sconces. These smaller,
        # single-purpose 20x18 annexes stay below that whole-map fixture budget.
        annexe_cap = evidence["count"] - 1
        for key in MAP_KEYS:
            lights = self.light_cells(self.maps[key])
            self.assertGreaterEqual(len(lights), 2, key)
            self.assertLessEqual(len(lights), annexe_cap, key)

    def test_lights_are_spaced_instead_of_forming_a_fixture_carpet(self) -> None:
        for key in MAP_KEYS:
            positions = [(x, y) for x, y, _glyph in self.light_cells(self.maps[key])]
            for left, right in itertools.combinations(positions, 2):
                chebyshev = max(abs(left[0] - right[0]), abs(left[1] - right[1]))
                self.assertGreaterEqual(chebyshev, 4, (key, left, right))

    def test_exactly_one_light_is_a_required_durable_anchor(self) -> None:
        tier = self.root.find("./plan[@Key='deepend-becomingannexe']/binding/tier")
        self.assertIsNotNone(tier)
        requirements = [
            item
            for item in tier.findall("require")
            if item.get("Role") == "light:clean-room"
        ]
        self.assertEqual(1, len(requirements))
        self.assertEqual("1", requirements[0].get("Min"))
        self.assertEqual("1", requirements[0].get("Max"))

        for key in MAP_KEYS:
            lights = self.light_cells(self.maps[key])
            anchored = [
                glyph
                for _x, _y, glyph in lights
                if "light:clean-room" in glyph.get("Anchors", "").split(",")
            ]
            self.assertEqual(1, len(anchored), key)
            self.assertEqual("yes", anchored[0].get("Stateful"), key)
            for _x, _y, glyph in lights:
                self.assertEqual("$clean", glyph.get("Ground"), key)
                self.assertEqual("walk", glyph.get("Pass"), key)
                if glyph is not anchored[0]:
                    self.assertNotEqual("yes", glyph.get("Stateful"), key)
                    self.assertFalse(glyph.get("Anchors"), key)


if __name__ == "__main__":
    unittest.main()
