#!/usr/bin/env python3
"""Pins lore/taste closures that must survive later creed progression work."""

from __future__ import annotations

import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
OBJECTS = ROOT / "RuntimeData" / "ObjectBlueprints.xml"
BUILDINGS = ROOT / "RuntimeData" / "KingdomBuildings.xml"
CREEDS = ROOT / "Architecture" / "KingdomArchitectures-Creeds.xml"
DEEP = ROOT / "Architecture" / "KingdomArchitectures-DeepEndgame.xml"
GENERATOR = ROOT / "Tools" / "generate-lot-realizations.py"


class CreedTasteDossierTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.objects = ET.parse(OBJECTS).getroot()
        cls.buildings = ET.parse(BUILDINGS).getroot()
        cls.creeds = ET.parse(CREEDS).getroot()
        cls.deep = ET.parse(DEEP).getroot()

    def blueprint(self, name: str) -> ET.Element:
        return next(item for item in self.objects.iter("object") if item.get("Name") == name)

    def architecture_map(self, key: str) -> ET.Element:
        return next(item for item in self.creeds.findall("map") if item.get("Key") == key)

    @staticmethod
    def count_glyph(architecture_map: ET.Element, character: str) -> int:
        return sum((row.get("Cells") or "").count(character) for row in architecture_map.findall("row"))

    @staticmethod
    def glyph_for_anchor(architecture_map: ET.Element, anchor: str) -> ET.Element:
        return next(
            glyph
            for glyph in architecture_map.findall("glyph")
            if anchor in (glyph.get("Anchors") or "").split(",")
        )

    def test_food_creeds_use_category_grounded_empty_container_presentations(self) -> None:
        expected = {
            "seedbin": (
                "r_KingdomCreedJoppaSeedBin", "Items/sw_basket.bmp", "timber", "&g"
            ),
            "spicejar": (
                "r_KingdomCreedKyakukyaSpiceJar", "Items/sw_vase.bmp", "mud", "&w"
            ),
            "meatcache": (
                "r_KingdomCreedSnapjawMeatCache",
                "Items/sw_basket.bmp",
                "timber",
                "&r",
            ),
            "labelledbin": (
                "r_KingdomCreedFarmersLabelledBin",
                "Assets_Content_Textures_Tiles_sw_chest.bmp",
                "timber",
                "&w",
            ),
        }
        palette = self.creeds.find("palette[@Key='creed-practice-hands']")
        self.assertIsNotNone(palette)
        slots = {item.get("Key"): item for item in palette.findall("slot")}
        tiles = set()
        presentations = set()
        for slot_key, (name, tile, material, tile_color) in expected.items():
            slot = slots[slot_key]
            self.assertEqual(name, slot.get("Blueprint"))
            self.assertEqual(material, slot.get("Material"))
            blueprint = self.blueprint(name)
            self.assertEqual("r_KingdomFixtureBasketEmpty", blueprint.get("Inherits"))
            render = blueprint.find("part[@Name='Render']")
            self.assertEqual(tile, render.get("Tile"))
            self.assertEqual(tile_color, render.get("TileColor"))
            self.assertEqual(
                tile, blueprint.find("tag[@Name='EmptyTile']").get("Value")
            )
            self.assertEqual(
                "*delete",
                blueprint.find("tag[@Name='InventoryPopulationTable']").get("Value"),
            )
            self.assertEqual([], blueprint.findall("inventoryobject"))
            tiles.add(tile.lower())
            presentations.add((tile.lower(), tile_color.lower()))
        self.assertEqual(3, len(tiles))
        self.assertEqual(4, len(presentations))

        references = {
            glyph.get("Object")
            for architecture_map in self.creeds.findall("map")
            for glyph in architecture_map.findall("glyph")
        }
        self.assertTrue(
            {"$seedbin", "$spicejar", "$meatcache", "$labelledbin"} <= references
        )

    def test_generated_food_and_goatfolk_fixtures_are_inert_wrappers(self) -> None:
        text = GENERATOR.read_text(encoding="utf-8")
        mappings = {
            '"$seedbin": "$practiceseed"',
            '"$spicejar": "$practicespice"',
            '"$meatcache": "$practicemeat"',
            '"$labelledbin": "$practicelabel"',
            '"$hornpost": "$goatpennon"',
        }
        for mapping in mappings:
            self.assertIn(mapping, text)
        self.assertNotIn('"$hornpost": "$hornpost"', text)
        for name in (
            "r_KingdomCreedPracticeSeedHamper",
            "r_KingdomCreedPracticeSpiceJar",
            "r_KingdomCreedPracticeMeatCache",
            "r_KingdomCreedPracticeLabelledBin",
            "r_KingdomCreedGoatfolkChallengePennon",
        ):
            blueprint = self.blueprint(name)
            self.assertEqual("Furniture", blueprint.get("Inherits"), name)
            parts = {item.get("Name") for item in blueprint.findall("part")}
            self.assertEqual({"Render", "Description", "Physics"}, parts, name)
            self.assertEqual("false", blueprint.find("part[@Name='Physics']").get("Solid"))

    def test_practice_props_do_not_borrow_live_affordance_silhouettes(self) -> None:
        expected = {
            "r_KingdomCreedPracticeDryBasin": (
                "Items/sw_regen_tank_broken2.bmp",
                "Items/sw_catchbasin.bmp",
                "breached",
            ),
            "r_KingdomCreedPracticePallet": (
                "Items/sw_scroll1.bmp",
                "Items/sw_bedroll.bmp",
                "roll",
            ),
            "r_KingdomCreedDryContact": (
                "Items/sw_copper_wire.bmp",
                "Items/sw_induction_station.bmp",
                "severed",
            ),
            "r_KingdomCreedPracticeArmsRack": (
                "Items/sw_fence_gates_2_open.bmp",
                "Items/sw_weapons_rack.bmp",
                "frame",
            ),
        }
        active_parts = {
            "Bed",
            "Capacitor",
            "Commerce",
            "Container",
            "ElectricalPowerTransmission",
            "EnergyCellRack",
            "HydraulicPowerTransmission",
            "InductionCharger",
            "Inventory",
            "LeakWhenBroken",
            "LightSource",
            "LiquidProducer",
            "LiquidVolume",
            "MechanicalPowerTransmission",
            "PowerSwitch",
        }
        replacement_tiles = set()
        for name, (tile, live_tile, visual_cue) in expected.items():
            blueprint = self.blueprint(name)
            self.assertEqual("Furniture", blueprint.get("Inherits"), name)
            render = blueprint.find("part[@Name='Render']")
            self.assertEqual(tile, render.get("Tile"), name)
            self.assertNotEqual(tile.casefold(), live_tile.casefold(), name)
            self.assertIn(visual_cue, render.get("DisplayName").casefold(), name)
            replacement_tiles.add(tile.casefold())

            parts = {item.get("Name") for item in blueprint.findall("part")}
            self.assertTrue(parts <= {"Render", "Description", "Physics", "Metal"}, name)
            self.assertTrue(active_parts.isdisjoint(parts), name)
            physics = blueprint.find("part[@Name='Physics']")
            self.assertEqual("false", physics.get("Takeable"), name)
            self.assertEqual("false", physics.get("Solid"), name)
            self.assertEqual([], blueprint.findall("builder"), name)
            self.assertEqual([], blueprint.findall("inventoryobject"), name)
            self.assertEqual([], blueprint.findall("property"), name)
            self.assertEqual([], blueprint.findall("tag"), name)
            self.assertEqual([], blueprint.findall("stag"), name)
        self.assertEqual(len(expected), len(replacement_tiles))

    def test_baetyl_and_dromad_frames_are_counted_structures(self) -> None:
        for map_key, anchor, expected in (
            ("creed-baetyl-frame-s0", "frame:measured-gantry", 4),
            ("creed-dromad-shade-s0", "frame:travelling-awning", 4),
        ):
            architecture_map = self.architecture_map(map_key)
            glyph = self.glyph_for_anchor(architecture_map, anchor)
            self.assertIsNotNone(glyph.get("Structure"))
            self.assertEqual(expected, self.count_glyph(architecture_map, glyph.get("Char")))
            tier = next(
                tier
                for tier in self.creeds.iter("tier")
                if tier.get("Map") == map_key
            )
            requirement = next(
                item for item in tier.findall("require") if item.get("Role") == anchor
            )
            self.assertEqual(str(expected), requirement.get("Min"))

    def test_gyre_and_chavvah_use_native_connected_wall_idioms(self) -> None:
        gyre = self.blueprint("r_KingdomStructureGyreOssuaryScreen")
        self.assertEqual("BaseWallBone", gyre.get("Inherits"))
        self.assertIsNone(gyre.find("part[@Name='Render']").get("Tile"))
        gyre_map = self.architecture_map("creed-gyre-ashcourt-s0")
        self.assertEqual(4, self.count_glyph(gyre_map, "o"))
        self.assertEqual("$ossuary", next(
            item for item in gyre_map.findall("glyph") if item.get("Char") == "o"
        ).get("Structure"))

        bough = self.blueprint("r_KingdomStructureChavvahTrunk")
        self.assertEqual("ChavvahTrunk", bough.get("Inherits"))
        self.assertIsNone(bough.find("part[@Name='Render']").get("Tile"))
        chavvah_map = self.architecture_map("creed-chavvah-school-s0")
        self.assertEqual(14, self.count_glyph(chavvah_map, "B"))
        self.assertEqual(2, self.count_glyph(chavvah_map, "t"))
        self.assertEqual(2, self.count_glyph(chavvah_map, "+"))

    def test_memorials_use_inspectable_rules_text_not_forged_secret_ids(self) -> None:
        lore = set()
        for name in (
            "r_KingdomCairn",
            "r_KingdomGraveGrove",
            "r_KingdomNicheTomb",
            "r_KingdomCragmenschStoneGarden",
        ):
            blueprint = self.blueprint(name)
            lore.add(blueprint.find("part[@Name='RulesDescription']").get("Text"))
            self.assertIsNotNone(blueprint.find("part[@Name='SmartuseLooks']"), name)
            self.assertIsNotNone(blueprint.find("part[@Name='Interesting']"), name)
            self.assertIsNone(blueprint.find("part[@Name='RevealVillageHistoryOnLook']"), name)
        self.assertEqual(4, len(lore))

    def test_reliquary_case_relic_and_mechanimist_scope_are_distinct(self) -> None:
        relic_case = self.blueprint("r_KingdomFixtureRelicCaseScrap")
        relic = self.blueprint("r_KingdomFixtureMachineRelic")
        self.assertNotEqual(
            relic_case.find("part[@Name='Render']").get("Tile"),
            relic.find("part[@Name='Render']").get("Tile"),
        )
        palette = self.deep.find("palette[@Key='deepend-reliquary-salvage']")
        roles = {
            item.get("Role"): item.get("Blueprint") for item in palette.findall("slot")
        }
        self.assertEqual("r_KingdomFixtureRelicCaseScrap", roles["recovered-relic-case"])
        self.assertEqual("r_KingdomFixtureMachineRelic", roles["retained-machine-relic"])

        works = [
            item
            for item in self.buildings.findall("building")
            if item.get("Creed") == "Mechanimists"
        ]
        self.assertEqual(1, len(works))
        self.assertEqual(("reliquary", "L", "Town", "workshop"), (
            works[0].get("Key"),
            works[0].get("Plot"),
            works[0].get("MinStage"),
            works[0].get("MinTech"),
        ))

    def test_creed_practices_are_standalone_unless_the_fiction_authors_a_successor(self) -> None:
        by_creed = {}
        for building in self.buildings.findall("building"):
            creed = building.get("Creed")
            if creed:
                by_creed.setdefault(creed, []).append(building)

        self.assertEqual(33, len(by_creed))
        self.assertEqual(
            {"Robots"},
            {creed for creed, works in by_creed.items() if len(works) > 1},
        )
        successors = {
            (building.get("Key"), building.get("UpgradesTo"))
            for works in by_creed.values()
            for building in works
            if building.get("UpgradesTo")
        }
        self.assertEqual({("robotchargebay", "robotservicebay")}, successors)
        self.assertEqual(
            {"robotchargebay", "robotservicebay"},
            {building.get("Key") for building in by_creed["Robots"]},
        )


if __name__ == "__main__":
    unittest.main()
