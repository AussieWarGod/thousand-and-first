"""Full merged-corpus contract for physical benefit providers."""

from __future__ import annotations

import re
import runpy
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CHECKER = ROOT / "Tools" / "check-benefit-provider-content.py"
GENERATOR = ROOT / "Tools" / "generate-benefit-providers.py"


class BenefitProviderContentTests(unittest.TestCase):
    def test_every_catalogue_and_yard_role_has_exact_physical_evidence(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(CHECKER), str(ROOT)],
            cwd=ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr or completed.stdout)
        self.assertIn("114 catalogue rows, 187 variants, 105 unique fixtures", completed.stdout)
        self.assertIn("exact caps, scopes, operations, and acquisition", completed.stdout)

    def test_generated_provider_blocks_are_current_and_deterministic(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(GENERATOR), "--check", "--repo-root", str(ROOT)],
            cwd=ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr or completed.stdout)
        self.assertIn("generated content clean", completed.stdout)

    def test_palette_upsert_canonicalizes_whitespace_and_duplicate_slots(self) -> None:
        sys.path.insert(0, str(ROOT / "Tools"))
        upsert = runpy.run_path(str(GENERATOR))["upsert_palette_slot"]
        source = (
            '<KingdomArchitectures>\n'
            '  <palette Key="test">\n'
            '\t<slot Key="benefit-test-main" Blueprint="old-a" />\n'
            '    <slot Key="benefit-test-main" Blueprint="old-b" />\n'
            '  </palette>\n'
            '</KingdomArchitectures>\n'
        )
        line = '    <slot Key="benefit-test-main" Blueprint="reviewed" />'
        updated = upsert(source, "test", "benefit-test-main", line)
        self.assertEqual(1, updated.count('Key="benefit-test-main"'))
        self.assertIn(line, updated)

    def test_fixture_prose_is_authored_bounded_and_emitted_verbatim(self) -> None:
        sys.path.insert(0, str(ROOT / "Tools"))
        from benefit_provider_manifest import (
            BUILDING_FIXTURES, PROVIDER_DESCRIPTIONS, YARD_FIXTURES,
        )

        fixtures = BUILDING_FIXTURES + YARD_FIXTURES
        self.assertEqual(
            {spec.blueprint for spec in fixtures}, set(PROVIDER_DESCRIPTIONS),
            "description inventory must exactly match the reviewed fixture inventory",
        )
        prose = [spec.description.strip() for spec in fixtures]
        self.assertTrue(all(120 <= len(text) <= 260 for text in prose))
        self.assertEqual(len(prose), len(set(text.casefold() for text in prose)))

        forbidden = (
            "physical fixture", "settlement inspects", "room name", "marker cannot",
            "built from local", "blueprint", "provider key", "build key",
            "implementation metadata", "catalogue identity", "api",
        )
        for spec in fixtures:
            lowered = spec.description.casefold()
            for phrase in forbidden:
                self.assertNotIn(phrase, lowered, spec.blueprint)
            self.assertGreaterEqual(len(re.findall(r"[.!?](?:\s|$)", spec.description)), 2,
                                    spec.blueprint)

        openings = Counter(
            tuple(re.findall(r"[a-z]+", text.casefold())[:4]) for text in prose
        )
        self.assertLessEqual(max(openings.values()), 2)

        state_words = {
            "HeldFreshWater": (("fresh water",), ("empty", "dry")),
            "HeldFreshWaterAndStaffed": (
                ("fresh water",), ("empty", "dry"),
                ("tender", "reader", "attendant", "keeper", "hands"),
            ),
            "OpenFreshWater": (("fresh water",), ("empty", "dry"), ("open", "openly")),
            "WetOffal": (("corpse-stock",), ("liquid",), ("empty",)),
            "OpenBrine": (("brine",), ("empty",)),
            "RootSown": (("unsown",), ("sown", "root"), ("dry",)),
            "MirrorPair": (("unpaired",), ("both", "two"), ("powered",), ("reciprocal",)),
        }
        for spec in BUILDING_FIXTURES:
            lowered = spec.description.casefold()
            for alternatives in state_words.get(spec.state, ()):
                self.assertTrue(any(word in lowered for word in alternatives),
                                f"{spec.blueprint} needs one of {alternatives}")
            if spec.state:
                construction_words = (
                    "construction", "built", "installed", "raised", "finished",
                    "completed", "begins", "begin", "starts",
                )
                self.assertTrue(any(word in lowered for word in construction_words),
                                f"{spec.blueprint} does not name its construction state")
            if spec.native_part:
                self.assertIn("newly installed", lowered, spec.blueprint)
                self.assertIn("capacitor", lowered, spec.blueprint)
                self.assertIn("charge", lowered, spec.blueprint)
            if spec.operation.casefold() == "powered":
                self.assertIn("capacitor", lowered, spec.blueprint)
                self.assertIn("power", lowered, spec.blueprint)

        objects = {
            item.get("Name", ""): item
            for item in ET.parse(ROOT / "RuntimeData" / "ObjectBlueprints.xml")
            .getroot().findall("object")
        }
        for spec in BUILDING_FIXTURES:
            node = objects[spec.blueprint + "Semantic"]
            description = node.find("part[@Name='Description']")
            self.assertIsNotNone(description, spec.blueprint)
            self.assertEqual(spec.description, description.get("Short"), spec.blueprint)

            material = next(
                tag.get("Value", "") for tag in node.findall("tag")
                if tag.get("Name") == "r_KingdomProviderMaterial"
            )
            material_words = {
                "workedmetal": ("metal", "plate", "plated", "worked"),
                "scrap": ("salvaged", "scrap", "reclaimed", "recovered", "metal", "plate"),
                "marble": ("marble",),
                "shapedstone": ("stone",),
                "stone": ("stone",),
                "shapedtimber": ("timber", "wood"),
                "timber": ("timber", "wood", "stakes", "fuelwood"),
                "canvas": ("canvas", "cord", "cloth"),
                "mud": ("earth", "mud", "reed"),
                "brush": ("brush", "stakes", "wood"),
            }[material]
            lowered = spec.description.casefold()
            self.assertTrue(any(word in lowered for word in material_words),
                            f"{spec.blueprint} does not name or embody {material}")

    def test_provider_furniture_keeps_role_readable_vanilla_silhouettes(self) -> None:
        sys.path.insert(0, str(ROOT / "Tools"))
        from benefit_provider_manifest import BUILDING_FIXTURES

        objects = {
            item.get("Name", ""): item
            for item in ET.parse(ROOT / "RuntimeData" / "ObjectBlueprints.xml")
            .getroot().findall("object")
        }
        signatures = Counter()
        tiles = {}
        for spec in BUILDING_FIXTURES:
            render = objects[spec.blueprint + "Semantic"].find("part[@Name='Render']")
            self.assertIsNotNone(render, spec.blueprint)
            tile = render.get("Tile", "")
            self.assertTrue(tile, spec.blueprint)
            tiles[spec.blueprint] = tile.casefold()
            signatures[(tile.casefold(), render.get("RenderString", ""),
                        render.get("ColorString", ""))] += 1

        # Material-only generation once flattened 49 unrelated roles to one low table and 42
        # advanced roles to one cabinet.  Shared silhouettes are now bounded by semantic class.
        self.assertGreaterEqual(len(signatures), 30)
        self.assertLessEqual(max(signatures.values()), 15)
        expected = {
            "r_KingdomRiteFire": "items/sw_campfire_noflame.png",
            "r_KingdomRiteOven": "items/sw_oven.bmp",
            "r_KingdomForgeHallBankedForge": "items/sw_forge.bmp",
            "r_KingdomForgeHallCastingAnvil": "items/sw_anvil.bmp",
            "r_KingdomGreatFoundryFurnace": "items/sw_glass_furnace.bmp",
            "r_KingdomDeepBoreHead": "creatures/natural-weapon-drill.bmp",
            "r_KingdomDeepCutFace": "tiles2/sw_rubble_2.bmp",
            "r_KingdomCrankMillMachine": "items/sw_millstone_1.bmp",
            "r_KingdomWaterWheelMachine": "items/sw_waterwheel_1.bmp",
            "r_KingdomSailvaneMachine": "items/sw_windmill_1.bmp",
            "r_KingdomHindrenLoom": "items/sw_sewing_machine.bmp",
            "r_KingdomMirrorGateCore": "items/sw_teleporter_pad.bmp",
            "r_KingdomCrownWitnessDais": "items/sw_chair_throne.bmp",
            "r_KingdomArcologyCouncilDais": "items/sw_table_sleek.bmp",
        }
        for blueprint, tile in expected.items():
            self.assertEqual(tile, tiles[blueprint], blueprint)
        self.assertNotEqual(
            tiles["r_KingdomForgeHallBankedForge"],
            tiles["r_KingdomForgeHallCastingAnvil"],
        )


if __name__ == "__main__":
    unittest.main()
