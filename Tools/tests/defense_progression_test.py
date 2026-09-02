#!/usr/bin/env python3
"""Pins the two separate defense lineages: garrison ground and frontier fabric."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
BUILDINGS = ROOT / "RuntimeData" / "KingdomBuildings.xml"
ARCHITECTURE = ROOT / "Architecture" / "KingdomArchitectures-CivicFaith.xml"


class DefenseProgressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.buildings = {
            item.get("Key"): item
            for item in ET.parse(BUILDINGS).getroot().findall("building")
        }
        cls.architecture = ET.parse(ARCHITECTURE).getroot()

    def test_frontier_fabric_upgrades_without_becoming_a_plot(self) -> None:
        self.assertEqual("rampart", self.buildings["palisade"].get("UpgradesTo"))
        self.assertEqual("rampart", self.buildings["rubblewall"].get("UpgradesTo"))
        for key in ("palisade", "rubblewall", "rampart"):
            self.assertIsNone(self.buildings[key].get("Plot"), key)
            self.assertIsNotNone(self.buildings[key].get("Defence"), key)

    def test_watchhouse_grows_into_barracks_on_one_authored_lineage(self) -> None:
        watchhouse = self.buildings["watchhouse"]
        barracks = self.buildings["barracks"]
        self.assertEqual("barracks", watchhouse.get("UpgradesTo"))
        self.assertEqual(
            "stone:14,shapedtimber:6",
            watchhouse.get("UpgradeMaterials"),
        )
        self.assertEqual(("M", "Village", "salvage"), (
            watchhouse.get("Plot"),
            watchhouse.get("MinStage"),
            watchhouse.get("MinTech"),
        ))
        self.assertEqual(("L", "Town", "workshop"), (
            barracks.get("Plot"),
            barracks.get("MinStage"),
            barracks.get("MinTech"),
        ))
        self.assertIn("scrap:6", (barracks.get("Materials") or "").split(","))
        self.assertNotIn("scrap", watchhouse.get("UpgradeMaterials") or "")
        self.assertIsNone(watchhouse.get("Defence"))
        self.assertIsNone(barracks.get("Defence"))

        plan = self.architecture.find("plan[@Key='defense-garrison']")
        self.assertIsNotNone(plan)
        bindings = {item.get("Size"): item for item in plan.findall("binding")}
        self.assertEqual({"M", "L"}, set(bindings))
        watch_tier = bindings["M"].find("tier")
        large_tiers = {
            item.get("BuildKey"): item for item in bindings["L"].findall("tier")
        }
        self.assertEqual({"watchhouse", "barracks"}, set(large_tiers))
        barracks_tier = large_tiers["barracks"]
        self.assertEqual(("watchhouse", "0", None), (
            watch_tier.get("BuildKey"),
            watch_tier.get("Level"),
            watch_tier.get("Transition"),
        ))
        self.assertEqual(("0", "defense-watchhouse-l0"), (
            large_tiers["watchhouse"].get("Level"),
            large_tiers["watchhouse"].get("Map"),
        ))
        self.assertEqual(("barracks", "1", "renovate-expand"), (
            barracks_tier.get("BuildKey"),
            barracks_tier.get("Level"),
            barracks_tier.get("Transition"),
        ))

    def test_expansion_keeps_root_alignment_and_adds_real_programme(self) -> None:
        maps = {
            item.get("Key"): item for item in self.architecture.findall("map")
        }

        def glyph_position(architecture_map: ET.Element, character: str) -> tuple[int, int]:
            found = [
                (x, y)
                for y, row in enumerate(architecture_map.findall("row"))
                for x, value in enumerate(row.get("Cells") or "")
                if value == character
            ]
            self.assertEqual(1, len(found), architecture_map.get("Key"))
            return found[0]

        watch = maps["defense-watchhouse-m0"]
        large_watch = maps["defense-watchhouse-l0"]
        barracks = maps["defense-barracks-l0"]
        self.assertEqual((8, 6), (int(watch.get("Width")), int(watch.get("Height"))))
        self.assertEqual((12, 10), (
            int(large_watch.get("Width")), int(large_watch.get("Height"))
        ))
        self.assertEqual((12, 10), (
            int(barracks.get("Width")), int(barracks.get("Height"))
        ))
        self.assertEqual((3, 1), glyph_position(watch, "@"))
        self.assertEqual((5, 1), glyph_position(large_watch, "@"))
        self.assertEqual((5, 1), glyph_position(barracks, "@"))

        def count(architecture_map: ET.Element, character: str) -> int:
            return sum(
                (row.get("Cells") or "").count(character)
                for row in architecture_map.findall("row")
            )

        self.assertEqual(2, count(watch, "B"))
        self.assertEqual(2, count(large_watch, "B"))
        self.assertEqual(4, count(barracks, "B"))
        self.assertEqual(1, count(watch, "T"))
        self.assertEqual(1, count(barracks, "l"))

    def test_expansion_retains_protected_furniture_and_replaces_only_the_roster(self) -> None:
        maps = {item.get("Key"): item for item in self.architecture.findall("map")}
        palettes = {
            item.get("Key"): {
                slot.get("Key"): slot for slot in item.findall("slot")
            }
            for item in self.architecture.findall("palette")
        }
        medium = maps["defense-watchhouse-m0"]
        large_watch = maps["defense-watchhouse-l0"]
        target = maps["defense-barracks-l0"]

        medium_rows = [item.get("Cells") or "" for item in medium.findall("row")]
        large_rows = [item.get("Cells") or "" for item in large_watch.findall("row")]
        self.assertEqual(medium_rows, [row[2:10] for row in large_rows[:6]])

        def glyphs(architecture_map: ET.Element) -> dict[str, ET.Element]:
            return {
                item.get("Char") or "": item
                for item in architecture_map.findall("glyph")
            }

        def cells(architecture_map: ET.Element):
            roster = glyphs(architecture_map)
            for y, row in enumerate(architecture_map.findall("row")):
                for x, character in enumerate(row.get("Cells") or ""):
                    if character in roster:
                        yield x, y, roster[character]

        def main(architecture_map: ET.Element) -> tuple[int, int]:
            found = [
                (x, y) for x, y, glyph in cells(architecture_map)
                if "main" in (glyph.get("Anchors") or "").split(",")
            ]
            self.assertEqual(1, len(found))
            return found[0]

        def at(architecture_map: ET.Element, x: int, y: int) -> ET.Element:
            found = [glyph for cx, cy, glyph in cells(architecture_map)
                     if (cx, cy) == (x, y)]
            self.assertEqual(1, len(found), (architecture_map.get("Key"), x, y))
            return found[0]

        def custody(glyph: ET.Element) -> str:
            anchors = [
                item for item in (glyph.get("Anchors") or "").split(",") if item
            ]
            benefits = [item for item in anchors if item.startswith("benefit:")]
            if benefits:
                self.assertEqual(1, len(benefits))
                return benefits[0]
            stable = [item for item in anchors
                      if item != "main" and not item.startswith("entrance:")]
            self.assertEqual(1, len(stable))
            return stable[0]

        def truth(glyph: ET.Element, palette_key: str) -> tuple[str, ...]:
            reference = glyph.get("Object") or ""
            self.assertTrue(reference.startswith("$"), reference)
            slot = palettes[palette_key][reference[1:]]
            return tuple(slot.get(key) or "" for key in (
                "Blueprint", "Material", "MinTech", "Knowledge", "Power", "Natural"
            ))

        before_main = main(medium)
        after_main = main(target)
        target_palette = "defense-barracks-workshop"
        retained = 0
        for x, y, glyph in cells(medium):
            if glyph.get("Stateful") != "yes" or glyph.get("Object") == "$building":
                continue
            tx = after_main[0] + x - before_main[0]
            ty = after_main[1] + y - before_main[1]
            successor = at(target, tx, ty)
            self.assertEqual("yes", successor.get("Stateful"), (x, y, tx, ty))
            self.assertEqual(custody(glyph), custody(successor))
            self.assertEqual(
                truth(glyph, "defense-watchhouse-salvage"),
                truth(successor, target_palette),
            )
            retained += 1
        self.assertEqual(5, retained)  # two slings, mess table, occupied locker, hearth

        before_provider = next(
            (x, y, glyph) for x, y, glyph in cells(medium)
            if "benefit:watchhouse-main" in (glyph.get("Anchors") or "")
        )
        after_provider = next(
            (x, y, glyph) for x, y, glyph in cells(target)
            if "benefit:barracks-main" in (glyph.get("Anchors") or "")
        )
        self.assertNotEqual("yes", before_provider[2].get("Stateful"))
        self.assertEqual("yes", after_provider[2].get("Stateful"))
        self.assertEqual(
            (after_main[0] + before_provider[0] - before_main[0],
             after_main[1] + before_provider[1] - before_main[1]),
            after_provider[:2],
        )


if __name__ == "__main__":
    unittest.main()
