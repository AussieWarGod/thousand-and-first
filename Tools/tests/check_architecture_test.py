#!/usr/bin/env python3
"""Isolated tests for Tools/check-architecture.py."""

from __future__ import annotations

import copy
import importlib.util
import re
import sys
import tempfile
import textwrap
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


sys.dont_write_bytecode = True
CHECKER_PATH = Path(__file__).resolve().parents[1] / "check-architecture.py"
SPEC = importlib.util.spec_from_file_location("taf_check_architecture", CHECKER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)


BUILDINGS = """\
<kingdombuildings>
  <building Key="hut" Blueprint="r_TestHut" Category="housing" Plot="S" Materials="stone:1,timber:1" />
</kingdombuildings>
"""

ARCHITECTURE = """\
<KingdomArchitectures Schema="1">
  <palette Key="test-palette">
    <slot Key="floor" Blueprint="DirtFloor" Role="floor" Material="mud" MinTech="hands" Natural="yes" />
    <slot Key="wall" Blueprint="TestWall" Role="wall" Material="stone" MinTech="hands" Natural="no" />
    <slot Key="door" Blueprint="TestDoor" Role="door" Material="timber" MinTech="hands" Natural="no" />
    <slot Key="bed" Blueprint="TestBed" Role="sleep" Material="timber" MinTech="hands" Natural="no" />
  </palette>
  <map Key="test-map" Width="5" Height="4" DefaultCover="walled">
    <glyph Char="#" Ground="$floor" Structure="$wall" Claim="building" Pass="blocked" Cover="walled" />
    <glyph Char="," Ground="$floor" Claim="building" Pass="walk" Cover="walled" />
    <glyph Char="+" Ground="$floor" Structure="$door" Claim="building" Pass="walk" Cover="walled" Anchors="entrance:public" />
    <glyph Char="b" Ground="$floor" Object="$bed" Claim="building" Pass="adjacent" Cover="walled" Anchors="fixture:bed" Stateful="yes" />
    <glyph Char="@" Ground="$floor" Object="$building" Claim="building" Pass="walk" Cover="walled" Anchors="main,function:dwelling" Stateful="yes" />
    <row Cells="#####" />
    <row Cells="#@,,#" />
    <row Cells="#,b,+" />
    <row Cells="#####" />
  </map>
  <map Key="test-map-m" Width="8" Height="6" DefaultCover="walled">
    <glyph Char="#" Ground="$floor" Structure="$wall" Claim="building" Pass="blocked" Cover="walled" />
    <glyph Char="," Ground="$floor" Claim="building" Pass="walk" Cover="walled" />
    <glyph Char="+" Ground="$floor" Structure="$door" Claim="building" Pass="walk" Cover="walled" Anchors="entrance:public" />
    <glyph Char="b" Ground="$floor" Object="$bed" Claim="building" Pass="adjacent" Cover="walled" Anchors="fixture:bed" Stateful="yes" />
    <glyph Char="@" Ground="$floor" Object="$building" Claim="building" Pass="walk" Cover="walled" Anchors="main,function:dwelling" Stateful="yes" />
    <row Cells="...#####" />
    <row Cells="...#@,,#" />
    <row Cells="...#,b,+" />
    <row Cells="...#####" />
    <row Cells="........" />
    <row Cells="........" />
  </map>
  <map Key="test-map-l" Width="12" Height="9" DefaultCover="walled">
    <glyph Char="#" Ground="$floor" Structure="$wall" Claim="building" Pass="blocked" Cover="walled" />
    <glyph Char="," Ground="$floor" Claim="building" Pass="walk" Cover="walled" />
    <glyph Char="+" Ground="$floor" Structure="$door" Claim="building" Pass="walk" Cover="walled" Anchors="entrance:public" />
    <glyph Char="b" Ground="$floor" Object="$bed" Claim="building" Pass="adjacent" Cover="walled" Anchors="fixture:bed" Stateful="yes" />
    <glyph Char="@" Ground="$floor" Object="$building" Claim="building" Pass="walk" Cover="walled" Anchors="main,function:dwelling" Stateful="yes" />
    <row Cells=".......#####" />
    <row Cells=".......#@,,#" />
    <row Cells=".......#,b,+" />
    <row Cells=".......#####" />
    <row Cells="............" />
    <row Cells="............" />
    <row Cells="............" />
    <row Cells="............" />
    <row Cells="............" />
  </map>
  <map Key="test-map-xl" Width="20" Height="14" DefaultCover="walled">
    <glyph Char="#" Ground="$floor" Structure="$wall" Claim="building" Pass="blocked" Cover="walled" />
    <glyph Char="," Ground="$floor" Claim="building" Pass="walk" Cover="walled" />
    <glyph Char="+" Ground="$floor" Structure="$door" Claim="building" Pass="walk" Cover="walled" Anchors="entrance:public" />
    <glyph Char="b" Ground="$floor" Object="$bed" Claim="building" Pass="adjacent" Cover="walled" Anchors="fixture:bed" Stateful="yes" />
    <glyph Char="@" Ground="$floor" Object="$building" Claim="building" Pass="walk" Cover="walled" Anchors="main,function:dwelling" Stateful="yes" />
    <row Cells="...............#####" />
    <row Cells="...............#@,,#" />
    <row Cells="...............#,b,+" />
    <row Cells="...............#####" />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
    <row Cells="...................." />
  </map>
  <plan Key="dwelling">
    <binding Key="housing-s" Type="housing" Size="S" Facing="road">
      <tier Key="hut-t0" BuildKey="hut" Level="0" Map="test-map" Palette="test-palette">
        <require Role="main" Min="1" Max="1" />
        <require Role="entrance:public" Min="1" />
        <require Role="fixture:bed" Min="1" Max="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
    <binding Key="housing-m" Type="housing" Size="M" Facing="road">
      <tier Key="hut-m-t0" BuildKey="hut" Level="0" Map="test-map-m" Palette="test-palette">
        <require Role="main" Min="1" Max="1" />
        <require Role="entrance:public" Min="1" />
        <require Role="fixture:bed" Min="1" Max="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
    <binding Key="housing-l" Type="housing" Size="L" Facing="road">
      <tier Key="hut-l-t0" BuildKey="hut" Level="0" Map="test-map-l" Palette="test-palette">
        <require Role="main" Min="1" Max="1" />
        <require Role="entrance:public" Min="1" />
        <require Role="fixture:bed" Min="1" Max="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
    <binding Key="housing-xl" Type="housing" Size="XL" Facing="road">
      <tier Key="hut-xl-t0" BuildKey="hut" Level="0" Map="test-map-xl" Palette="test-palette">
        <require Role="main" Min="1" Max="1" />
        <require Role="entrance:public" Min="1" />
        <require Role="fixture:bed" Min="1" Max="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
  </plan>
</KingdomArchitectures>
"""


class ArchitectureCheckerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.repo = self.root / "repo"
        self.base = self.root / "Base"
        self.repo.mkdir()
        self.base.mkdir()
        (self.base / "ObjectBlueprints").mkdir()
        self.write_repo(BUILDINGS, ARCHITECTURE)
        (self.repo / "ObjectBlueprints.xml").write_text(
            '<objects><object Name="r_TestHut"><part Name="Physics" Solid="false" />'
            '</object><object Name="r_KingdomDelveDown" Inherits="StairsDown" />'
            '<object Name="r_KingdomDelveUp" Inherits="StairsUp" /></objects>\n',
            encoding="utf-8",
        )
        (self.base / "ObjectBlueprints.xml").write_text(
            textwrap.dedent(
                """\
                <objects>
                  <object Name="DirtFloor"><part Name="Physics" Solid="false" /></object>
                  <object Name="TestWall"><part Name="Physics" Solid="true" /></object>
                  <object Name="TestDoor"><part Name="Physics" Solid="true" /><part Name="Door" /></object>
                  <object Name="TestBed"><part Name="Physics" Solid="false" /></object>
                  <object Name="StairsUp"><part Name="Physics" Solid="false" /></object>
                  <object Name="StairsDown"><part Name="Physics" Solid="false" /></object>
                </objects>
                """
            ),
            encoding="utf-8",
        )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_repo(self, buildings: str, architecture: str) -> None:
        (self.repo / "KingdomBuildings.xml").write_text(buildings, encoding="utf-8")
        (self.repo / "KingdomArchitectures-Test.xml").write_text(
            architecture, encoding="utf-8"
        )

    def check(self, output: Path | None = None):
        return CHECKER.run_check(self.repo, self.base, output)

    def write_upgrade_repo(
        self, buildings: str | None = None, architecture: str | None = None
    ):
        if buildings is None or architecture is None:
            buildings = BUILDINGS.replace(
                'Materials="stone:1,timber:1"',
                'Materials="stone:1,timber:1" UpgradesTo="hut2"',
            ).replace(
                "</kingdombuildings>",
                '  <building Key="hut2" Blueprint="r_TestHut2" Category="housing" '
                'Plot="S" Materials="stone:2,timber:2" />\n</kingdombuildings>',
            )
            root = ET.fromstring(ARCHITECTURE)
            successor_maps = {}
            plan_offset = next(
                index for index, item in enumerate(list(root)) if item.tag == "plan"
            )
            for source_map in root.findall("map"):
                target_map = copy.deepcopy(source_map)
                target_key = source_map.get("Key", "") + "-successor"
                target_map.set("Key", target_key)
                first_row = next(
                    index
                    for index, item in enumerate(list(target_map))
                    if item.tag == "row"
                )
                target_map.insert(
                    first_row,
                    ET.Element(
                        "glyph",
                        {
                            "Char": ";",
                            "Ground": "$floor",
                            "Claim": "building",
                            "Pass": "walk",
                            "Cover": "walled",
                            "Anchors": "tier:successor",
                        },
                    ),
                )
                for row in target_map.findall("row"):
                    cells = row.get("Cells", "")
                    at = cells.rfind(",")
                    if at >= 0:
                        row.set("Cells", cells[:at] + ";" + cells[at + 1 :])
                        break
                root.insert(plan_offset, target_map)
                plan_offset += 1
                successor_maps[source_map.get("Key", "")] = target_key
            for binding in root.findall("./plan/binding"):
                source = binding.find("tier")
                self.assertIsNotNone(source)
                target = copy.deepcopy(source)
                target.set("Key", source.get("Key", "") + "-successor")
                target.set("BuildKey", "hut2")
                target.set("Level", "1")
                target.set("Map", successor_maps[source.get("Map", "")])
                binding.append(target)
            architecture = ET.tostring(root, encoding="unicode")
        self.write_repo(buildings, architecture)
        blueprints = self.repo / "ObjectBlueprints.xml"
        text = blueprints.read_text(encoding="utf-8")
        if 'Name="r_TestHut2"' not in text:
            blueprints.write_text(
                text.replace(
                    "</objects>",
                    '<object Name="r_TestHut2"><part Name="Physics" Solid="false" /></object>'
                    "</objects>",
                ),
                encoding="utf-8",
            )
        return buildings, architecture

    def write_adjacent_repo(self):
        """Create consecutive tiers without a catalogue edge, exercising binding law alone."""

        buildings, architecture = self.write_upgrade_repo()
        buildings = buildings.replace(' UpgradesTo="hut2"', "")
        return self.write_upgrade_repo(buildings, architecture)

    @staticmethod
    def successor_tier(root: ET.Element, size: str = "S") -> ET.Element:
        binding = next(
            item for item in root.findall("./plan/binding") if item.get("Size") == size
        )
        return next(
            item for item in binding.findall("tier") if item.get("Level") == "1"
        )

    @classmethod
    def successor_map(cls, root: ET.Element, size: str = "S") -> ET.Element:
        map_key = cls.successor_tier(root, size).get("Map")
        return next(item for item in root.findall("map") if item.get("Key") == map_key)

    @staticmethod
    def codes(result) -> set[str]:
        return {issue.code for issue in result.issues}

    @staticmethod
    def notice_codes(result) -> set[str]:
        return {notice.code for notice in result.notices}

    def test_valid_fixture_is_deterministic_and_read_only_by_default(self) -> None:
        before = sorted(path.relative_to(self.repo) for path in self.repo.rglob("*"))
        first = self.check()
        second = self.check()
        after = sorted(path.relative_to(self.repo) for path in self.repo.rglob("*"))
        self.assertTrue(first.ok, first.report())
        self.assertEqual(first.report(), second.report())
        self.assertEqual(before, after)
        self.assertEqual(first.blueprint_count, 9)
        self.assertEqual(len(first.goldens), 16)
        self.assertFalse(first.goldens_written)

    def write_ground_runtime(
        self, surface: str = "surface", underground: str = "deep"
    ) -> None:
        growth = self.repo / "Growth"
        growth.mkdir(exist_ok=True)
        (growth / "KingdomZoningStrataRules.cs").write_text(
            textwrap.dedent(
                f'''\
                namespace ThousandAndFirst
                {{
                    public static partial class KingdomZoningRules
                    {{
                        public const string StratumSurface = "{surface}";
                        public const string StratumDeep = "{underground}";
                        public static string StratumOfGround(bool Underground)
                        {{
                            return Underground ? StratumDeep : StratumSurface;
                        }}
                    }}
                }}
                '''
            ),
            encoding="utf-8",
        )

    @staticmethod
    def with_strata_selector(expression: str) -> str:
        return ARCHITECTURE.replace(
            '<variant Key="fallback" Priority="0" />',
            '<variant Key="fallback" Priority="0" />\n'
            f'        <variant Key="strata" Priority="10" Strata="{expression}" />',
            1,
        )

    def test_strata_selector_accepts_only_reachable_names_and_positive_wildcards(
        self,
    ) -> None:
        self.write_ground_runtime()
        for expression in (
            "surface",
            "deep",
            "SURFACE,!deep",
            "!surface",
            "all",
            "*",
            "all,!deep",
        ):
            with self.subTest(expression=expression):
                self.write_repo(BUILDINGS, self.with_strata_selector(expression))
                self.assertNotIn("variant.strata-unreachable", self.codes(self.check()))

    def test_strata_selector_rejects_names_stratum_of_ground_cannot_return(
        self,
    ) -> None:
        self.write_ground_runtime()
        for expression, unreachable in (
            ("underground", "underground"),
            ("sky", "sky"),
            ("arcology", "arcology"),
            ("seabed", "seabed"),
            ("!sky", "sky"),
            ("all,!arcology", "arcology"),
        ):
            with self.subTest(expression=expression):
                self.write_repo(BUILDINGS, self.with_strata_selector(expression))
                result = self.check()
                self.assertIn("variant.strata-unreachable", self.codes(result))
                self.assertIn(repr(unreachable), result.report())
                self.assertIn("KingdomZoningRules.StratumOfGround", result.report())

    def test_ground_selector_vocabulary_is_derived_from_runtime_source(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        self.assertEqual(
            ("surface", "deep"),
            CHECKER._runtime_ground_strata(source_repo),
        )
        self.write_ground_runtime(surface="open", underground="cavern")
        self.write_repo(BUILDINGS, self.with_strata_selector("surface"))
        result = self.check()
        self.assertIn("variant.strata-unreachable", self.codes(result))
        self.assertIn("('open', 'cavern')", result.report())

    def test_present_but_unreadable_ground_runtime_contract_fails_closed(self) -> None:
        growth = self.repo / "Growth"
        growth.mkdir()
        (growth / "KingdomZoningStrataRules.cs").write_text(
            'public static string StratumOfGround(bool Underground) => "surface";\n',
            encoding="utf-8",
        )
        self.assertIn("selector.strata-runtime-source", self.codes(self.check()))

    def test_upgrade_routes_exist_in_every_exact_frozen_binding(self) -> None:
        self.write_upgrade_repo()
        result = self.check()
        self.assertTrue(result.ok, result.report())

    def test_upgrade_route_missing_from_one_larger_binding_is_reported(self) -> None:
        buildings, architecture = self.write_upgrade_repo()
        root = ET.fromstring(architecture)
        binding = next(
            item for item in root.findall("./plan/binding") if item.get("Size") == "M"
        )
        binding.remove(
            next(
                item
                for item in binding.findall("tier")
                if item.get("BuildKey") == "hut2"
            )
        )
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        self.assertIn("upgrade.exact-route", self.codes(self.check()))

    def test_upgrade_route_rejects_cross_size_function_and_level_skip(self) -> None:
        buildings, architecture = self.write_upgrade_repo()
        self.write_upgrade_repo(
            buildings.replace(
                'Key="hut2" Blueprint="r_TestHut2" Category="housing" Plot="S"',
                'Key="hut2" Blueprint="r_TestHut2" Category="housing" Plot="M"',
            ),
            architecture,
        )
        self.assertIn("upgrade.catalogue-shape", self.codes(self.check()))

        buildings, architecture = self.write_upgrade_repo()
        root = ET.fromstring(architecture)
        target = next(
            item
            for item in root.findall("./plan/binding/tier")
            if item.get("BuildKey") == "hut2"
        )
        target.set("Level", "2")
        target.insert(0, ET.Element("require", {"Role": "function:market", "Min": "1"}))
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        codes = self.codes(self.check())
        self.assertIn("upgrade.level", codes)
        self.assertIn("upgrade.function", codes)

    def test_upgrade_route_rejects_selector_roster_drift(self) -> None:
        buildings, architecture = self.write_upgrade_repo()
        root = ET.fromstring(architecture)
        target = next(
            item
            for item in root.findall("./plan/binding/tier")
            if item.get("BuildKey") == "hut2"
        )
        target.find("variant").set("Key", "changed-fallback")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        self.assertIn("upgrade.selector-roster", self.codes(self.check()))

    def test_adjacent_tiers_reject_variant_key_mismatch_without_catalogue_edge(
        self,
    ) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        self.successor_tier(root).find("variant").set("Key", "changed-fallback")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        result = self.check()
        self.assertIn("upgrade.variant-keys", self.codes(result))
        self.assertNotIn("upgrade.selector-roster", self.codes(result))

    def test_adjacent_tiers_reject_moved_main_anchor_without_catalogue_edge(
        self,
    ) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        source_map = self.successor_map(root)
        moved = copy.deepcopy(source_map)
        moved.set("Key", "test-map-moved-main")
        moved.findall("row")[1].set("Cells", "#,@;#")
        root.insert(list(root).index(source_map) + 1, moved)
        self.successor_tier(root).set("Map", "test-map-moved-main")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        self.assertIn("upgrade.main-coordinate", self.codes(self.check()))

    def test_adjacent_tiers_reject_moved_stateful_fixture(self) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        architecture_map = self.successor_map(root)
        self.assertEqual("#,b,+", architecture_map.findall("row")[2].get("Cells"))
        architecture_map.findall("row")[2].set("Cells", "#b,,+")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        result = self.check()
        self.assertIn("upgrade.stateful-fixture", self.codes(result))
        self.assertIn("same-role successor coordinates=[(1, 2)]", result.report())

    def test_adjacent_tiers_reject_lost_stateful_fixture(self) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        architecture_map = self.successor_map(root)
        architecture_map.findall("row")[2].set("Cells", "#,,,+")
        tier = self.successor_tier(root)
        tier.remove(
            next(
                item
                for item in tier.findall("require")
                if item.get("Role") == "fixture:bed"
            )
        )
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        result = self.check()
        self.assertIn("upgrade.stateful-fixture", self.codes(result))
        self.assertNotIn("require.count", self.codes(result))

    def test_adjacent_tiers_reject_stateful_fixture_material_mutation(self) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        palette = root.find("palette")
        self.assertIsNotNone(palette)
        successor_palette = copy.deepcopy(palette)
        successor_palette.set("Key", "successor-palette")
        next(
            item
            for item in successor_palette.findall("slot")
            if item.get("Key") == "bed"
        ).set("Material", "stone")
        root.insert(list(root).index(palette) + 1, successor_palette)
        self.successor_tier(root).set("Palette", "successor-palette")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        result = self.check()
        self.assertIn("upgrade.stateful-fixture", self.codes(result))
        self.assertIn("material='timber'", result.report())
        self.assertIn("material='stone'", result.report())

    def test_adjacent_tiers_allow_new_stateful_fixture(self) -> None:
        buildings, architecture = self.write_adjacent_repo()
        root = ET.fromstring(architecture)
        palette = root.find("palette")
        self.assertIsNotNone(palette)
        ET.SubElement(
            palette,
            "slot",
            {
                "Key": "chair",
                "Blueprint": "TestBed",
                "Role": "seat",
                "Material": "timber",
                "MinTech": "hands",
                "Natural": "no",
            },
        )
        architecture_map = self.successor_map(root)
        first_row = next(
            index
            for index, item in enumerate(list(architecture_map))
            if item.tag == "row"
        )
        architecture_map.insert(
            first_row,
            ET.Element(
                "glyph",
                {
                    "Char": "c",
                    "Ground": "$floor",
                    "Object": "$chair",
                    "Claim": "building",
                    "Pass": "walk",
                    "Cover": "walled",
                    "Anchors": "fixture:chair",
                    "Stateful": "yes",
                },
            ),
        )
        architecture_map.findall("row")[1].set("Cells", "#@c;#")
        self.write_upgrade_repo(buildings, ET.tostring(root, encoding="unicode"))
        result = self.check()
        self.assertTrue(result.ok, result.report())

    def test_goldens_require_explicit_existing_empty_output(self) -> None:
        output = self.root / "goldens"
        output.mkdir()
        result = self.check(output)
        self.assertTrue(result.ok, result.report())
        self.assertTrue(result.goldens_written)
        files = sorted(output.iterdir())
        self.assertEqual(
            [path.name for path in files],
            [
                "hut__housing-l__fallback__east.txt",
                "hut__housing-l__fallback__north.txt",
                "hut__housing-l__fallback__south.txt",
                "hut__housing-l__fallback__west.txt",
                "hut__housing-m__fallback__east.txt",
                "hut__housing-m__fallback__north.txt",
                "hut__housing-m__fallback__south.txt",
                "hut__housing-m__fallback__west.txt",
                "hut__housing-s__fallback__east.txt",
                "hut__housing-s__fallback__north.txt",
                "hut__housing-s__fallback__south.txt",
                "hut__housing-s__fallback__west.txt",
                "hut__housing-xl__fallback__east.txt",
                "hut__housing-xl__fallback__north.txt",
                "hut__housing-xl__fallback__south.txt",
                "hut__housing-xl__fallback__west.txt",
            ],
        )
        rendered = (output / "hut__housing-s__fallback__north.txt").read_text(
            encoding="utf-8"
        )
        self.assertIn("object=r_TestHut", rendered)
        self.assertIn("pose: north", rendered)
        east = (output / "hut__housing-s__fallback__east.txt").read_text(
            encoding="utf-8"
        )
        self.assertIn("posed-dimensions: 4x5", east)
        self.assertIn("####\n#,@#\n#b,#\n#,,#\n#+##", east)
        with self.assertRaises(CHECKER.OutputDirectoryError):
            self.check(output)

    def test_failure_never_writes_requested_goldens(self) -> None:
        architecture = ARCHITECTURE.replace(
            '<variant Key="fallback" Priority="0" />',
            '<variant Key="fallback" Priority="0" />\n'
            '        <variant Key="labeled-only" Priority="10" Creeds="Example" />',
        )
        self.write_repo(BUILDINGS, architecture)
        output = self.root / "failed-goldens"
        output.mkdir()
        result = self.check(output)
        self.assertFalse(result.ok)
        self.assertIn("variant.no-op", self.codes(result))
        self.assertEqual(list(output.iterdir()), [])

    def test_malformed_row_is_reported_without_crashing_golden_generation(self) -> None:
        architecture = ARCHITECTURE.replace(
            '<row Cells="#@,,#" />', '<row Cells="#@,,##" />', 1
        )
        self.write_repo(BUILDINGS, architecture)
        result = self.check()
        self.assertFalse(result.ok)
        self.assertIn("map.row-width", self.codes(result))
        self.assertEqual(len(result.goldens), 12)

    def test_purpose_portfolio_requires_exact_anchors_and_dedicated_stores(
        self,
    ) -> None:
        buildings = BUILDINGS.replace(
            'Key="hut" Blueprint="r_TestHut" Category="housing" Plot="S"',
            'Key="deepbore" Blueprint="r_KingdomDeepBore" Category="craft" '
            'Plot="S" Purpose="deep"',
        )
        architecture = (
            ARCHITECTURE.replace('BuildKey="hut"', 'BuildKey="deepbore"')
            .replace('Type="housing"', 'Type="craft"')
            .replace('Anchors="fixture:bed"', 'Anchors="purpose:input"')
            .replace('Role="fixture:bed"', 'Role="purpose:input"')
        )
        self.write_repo(buildings, architecture)
        result = self.check()
        self.assertFalse(result.ok)
        self.assertIn("purpose.anchor-contract", self.codes(result))
        self.assertIn("purpose.fixture-blueprint", self.codes(result))

    def test_reopened_exotic_requires_hidden_activation_and_exact_runtime_seams(
        self,
    ) -> None:
        buildings = BUILDINGS.replace(
            'Key="hut" Blueprint="r_TestHut" Category="housing" Plot="S"',
            'Key="stasisvault" Blueprint="r_KingdomStasisVault" Category="craft" '
            'Plot="S" Knowledge="node:chimerism"',
        )
        architecture = ARCHITECTURE.replace(
            'BuildKey="hut"', 'BuildKey="stasisvault"'
        ).replace('Type="housing"', 'Type="craft"')
        self.write_repo(buildings, architecture)
        result = self.check()
        self.assertFalse(result.ok)
        self.assertIn("reopened.activation-gate", self.codes(result))
        self.assertIn("reopened.anchor-contract", self.codes(result))

    def test_shipped_reopened_activation_keys_are_ungranted(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        research_path = source_repo / "RuntimeData" / "KingdomResearch.xml"
        research = research_path.read_text(encoding="utf-8")
        self.assertIn('Requires="node:listening,rite:Chavvah"', research)
        published = ",".join(
            value
            for node in CHECKER.ET.parse(research_path).getroot().iter("node")
            for value in (node.get("Grants", ""), node.get("Reveals", ""))
        )
        for activation in CHECKER.REOPENED_ACTIVATION_KEYS.values():
            self.assertNotIn(activation, published)

    def test_style_creed_or_identity_variant_cannot_be_palette_only(self) -> None:
        second_palette = """\
  <palette Key="second-palette">
    <slot Key="floor" Blueprint="DirtFloor" Role="floor" Material="mud" MinTech="hands" Natural="yes" />
    <slot Key="wall" Blueprint="TestWall" Role="wall" Material="stone" MinTech="hands" Natural="no" />
    <slot Key="door" Blueprint="TestDoor" Role="door" Material="timber" MinTech="hands" Natural="no" />
    <slot Key="bed" Blueprint="TestBed" Role="sleep" Material="timber" MinTech="hands" Natural="no" />
  </palette>
"""
        for attribute, value in (
            ("Styles", "verdant"),
            ("Creeds", "Kyakukya"),
            ("Cultures", "Hindren"),
            ("Species", "hindren"),
            ("Genotypes", "True Kin"),
            ("Bodies", "robot"),
        ):
            with self.subTest(attribute=attribute):
                architecture = ARCHITECTURE.replace(
                    '  <map Key="test-map"', second_palette + '  <map Key="test-map"'
                ).replace(
                    '<variant Key="fallback" Priority="0" />',
                    '<variant Key="fallback" Priority="0" />\n'
                    f'        <variant Key="selected" Priority="10" {attribute}="{value}" '
                    'Palette="second-palette" />',
                )
                self.write_repo(BUILDINGS, architecture)
                result = self.check()
                self.assertIn("quality.selector-palette-only", self.codes(result))
                self.assertNotIn("schema.variant-attr", self.codes(result))

    def test_repeated_coordinate_keyed_roles_are_legal(self) -> None:
        architecture = ARCHITECTURE.replace(
            '<row Cells="#####" />', '<row Cells="+####" />', 1
        )
        self.write_repo(BUILDINGS, architecture)
        result = self.check()
        self.assertTrue(result.ok, result.report())

    def test_doctrine_floors_flag_pointless_doors_and_featureless_rooms(self) -> None:
        clean = self.check()
        self.assertTrue(clean.ok, clean.report())
        self.assertNotIn("doctrine.pointless-door", self.notice_codes(clean))
        self.assertNotIn("doctrine.featureless-room", self.notice_codes(clean))

        # A perimeter door on the M hut that opens onto nothing inside: one walk
        # neighbour only, so it joins fewer than two walkable cells.
        pointless = ARCHITECTURE.replace(
            '<row Cells="...#,b,+" />\n    <row Cells="...#####" />',
            '<row Cells="...#,b,+" />\n    <row Cells="...#+###" />',
        )
        self.assertNotEqual(pointless, ARCHITECTURE)
        self.write_repo(BUILDINGS, pointless)
        result = self.check()
        self.assertTrue(result.ok, result.report())
        self.assertIn("doctrine.pointless-door", self.notice_codes(result))

        # An annex behind an interior door holding no anchor, object, or ground
        # variety: a room a door serves for nothing.
        featureless = ARCHITECTURE.replace(
            '<row Cells="...#,b,+" />\n'
            '    <row Cells="...#####" />\n'
            '    <row Cells="........" />\n'
            '    <row Cells="........" />',
            '<row Cells="...#,b,+" />\n'
            '    <row Cells="...##+##" />\n'
            '    <row Cells="#,,,,,,#" />\n'
            '    <row Cells="########" />',
        )
        self.assertNotEqual(featureless, ARCHITECTURE)
        self.write_repo(BUILDINGS, featureless)
        result = self.check()
        self.assertIn("doctrine.featureless-room", self.notice_codes(result))

    def test_schema_coverage_dimensions_and_topology_faults(self) -> None:
        lower_root = ARCHITECTURE.replace(
            "KingdomArchitectures", "kingdomarchitectures"
        )
        self.write_repo(BUILDINGS, lower_root)
        self.assertIn("architecture.root", self.codes(self.check()))

        extra = BUILDINGS.replace(
            "</kingdombuildings>",
            '  <building Key="shed" Blueprint="r_TestHut" Category="housing" Plot="S" />\n'
            "</kingdombuildings>",
        )
        self.write_repo(extra, ARCHITECTURE)
        self.assertIn("coverage.exact-lot", self.codes(self.check()))

        narrow = (
            ARCHITECTURE.replace('Width="5"', 'Width="4"')
            .replace('Cells="#####"', 'Cells="####"')
            .replace('Cells="#@,,#"', 'Cells="#@,#"')
            .replace('Cells="#,b,+"', 'Cells="#b,+"')
        )
        self.write_repo(BUILDINGS, narrow)
        self.assertIn("binding.map-fit", self.codes(self.check()))

        disconnected = ARCHITECTURE.replace('Cells="#@,,#"', 'Cells="#@#,#"')
        self.write_repo(BUILDINGS, disconnected)
        self.assertIn("topology.disconnected", self.codes(self.check()))

        interior_road = ARCHITECTURE.replace(
            '<row Cells="#@,,#" />', '<row Cells="#@,+#" />', 1
        ).replace('<row Cells="#,b,+" />', '<row Cells="#,b,#" />', 1)
        self.write_repo(BUILDINGS, interior_road)
        self.assertIn("entrance.boundary", self.codes(self.check()))

        enclosed_egress = ARCHITECTURE.replace(
            '<row Cells="#,b,+" />', '<row Cells="#,+.#" />', 1
        )
        self.write_repo(BUILDINGS, enclosed_egress)
        self.assertIn("entrance.road-route", self.codes(self.check()))

    def test_generated_yard_reachability_cannot_cross_unrouted_empty_ground(
        self,
    ) -> None:
        self.assertEqual(
            CHECKER.VISUAL_BLUEPRINT_EQUIVALENTS["DirtFloor"],
            CHECKER.VISUAL_BLUEPRINT_EQUIVALENTS["DirtPath"],
        )
        generated = ARCHITECTURE.replace(
            '  <map Key="test-map-m" Width="8" Height="6" DefaultCover="walled">',
            '  <map Key="test-map-m" Width="8" Height="6" DefaultCover="walled">\n'
            '    <glyph Char="y" Ground="$floor" Claim="yard" Pass="walk" Cover="open" />\n',
            1,
        ).replace(
            '    <row Cells="...#,b,+" />\n'
            '    <row Cells="...#####" />\n'
            '    <row Cells="........" />',
            '    <row Cells="...#,b,+" />\n'
            '    <row Cells="...###.." />\n'
            '    <row Cells="...y...." />',
            1,
        )
        self.write_repo(BUILDINGS, generated)
        source = self.repo / "KingdomArchitectures-Test.xml"
        source.rename(self.repo / CHECKER.GENERATED_ARCHITECTURE_NAME)
        codes = self.codes(self.check())
        self.assertIn("topology.disconnected", codes)
        self.assertIn("generated.surface-monoculture", codes)

    def test_functional_verticality_needs_runtime_owned_external_pair(self) -> None:
        decorative = (
            ARCHITECTURE.replace(
                "  </palette>",
                '    <slot Key="stair" Blueprint="Sunken Room Stairs" Role="vertical-core" '
                'Material="stone" MinTech="hands" Natural="no" />\n  </palette>',
                1,
            )
            .replace(
                '    <glyph Char="b"',
                '    <glyph Char="^" Ground="$stair" Claim="building" Pass="walk" '
                'Cover="walled" Anchors="function:vertical-core" />\n    <glyph Char="b"',
                1,
            )
            .replace('Cells="#@,,#"', 'Cells="#@^,#"')
        )
        self.write_repo(BUILDINGS, decorative)
        codes = self.codes(self.check())
        self.assertIn("travel.blueprint", codes)
        self.assertIn("travel.runtime-owner", codes)

        same_map = (
            ARCHITECTURE.replace(
                "  </palette>",
                '    <slot Key="up" Blueprint="StairsUp" Role="vertical-up" Material="stone" '
                'MinTech="hands" Natural="no" />\n'
                '    <slot Key="down" Blueprint="StairsDown" Role="vertical-down" Material="stone" '
                'MinTech="hands" Natural="no" />\n  </palette>',
                1,
            )
            .replace(
                '    <glyph Char="b"',
                '    <glyph Char="^" Object="$up" Claim="building" Pass="walk" Cover="walled" '
                'Anchors="travel:up" Stateful="yes" />\n'
                '    <glyph Char="v" Object="$down" Claim="building" Pass="walk" Cover="walled" '
                'Anchors="travel:down" Stateful="yes" />\n    <glyph Char="b"',
                1,
            )
            .replace('Cells="#@,,#"', 'Cells="#@^,#"')
            .replace('Cells="#,b,+"', 'Cells="#vb,+"')
        )
        self.write_repo(BUILDINGS, same_map)
        codes = self.codes(self.check())
        self.assertIn("travel.link-owner", codes)
        self.assertIn("travel.same-map", codes)

        delve_building = BUILDINGS.replace('Key="hut"', 'Key="delve"')
        externally_paired = (
            ARCHITECTURE.replace('BuildKey="hut"', 'BuildKey="delve"')
            .replace(
                "  </palette>",
                '    <slot Key="down" Blueprint="r_KingdomDelveDown" Role="vertical-down" '
                'Material="stone" MinTech="hands" Natural="no" />\n  </palette>',
                1,
            )
            .replace(
                '    <glyph Char="b"',
                '    <glyph Char="v" Object="$down" Claim="building" Pass="walk" '
                'Cover="walled" Anchors="travel:down" Stateful="yes" />\n    <glyph Char="b"',
                1,
            )
            .replace('Cells="#,b,+"', 'Cells="#vb,+"')
        )
        self.write_repo(delve_building, externally_paired)
        result = self.check()
        self.assertTrue(result.ok, result.report())

    def test_different_build_keys_cannot_alias_the_same_compiled_layout(self) -> None:
        buildings = BUILDINGS.replace(
            "</kingdombuildings>",
            '  <building Key="shed" Blueprint="r_TestHut" Category="housing" Plot="S" />\n'
            "</kingdombuildings>",
        )
        second_plan = """\
  <plan Key="shed-plan">
    <binding Key="housing-s-shed" Type="housing" Size="S" Facing="heart">
      <tier Key="shed-t0" BuildKey="shed" Level="0" Map="test-map" Palette="test-palette">
        <require Role="main" Min="1" Max="1" />
        <require Role="entrance:public" Min="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
  </plan>
"""
        architecture = ARCHITECTURE.replace(
            "</KingdomArchitectures>", second_plan + "</KingdomArchitectures>"
        )
        self.write_repo(buildings, architecture)
        result = self.check()
        self.assertIn("quality.architecture-alias", self.codes(result))

    def test_old_procedural_shell_signature_is_a_quality_fault(self) -> None:
        architecture = (
            ARCHITECTURE.replace('<row Cells="#####" />', '<row Cells="##+##" />', 1)
            .replace('<row Cells="#@,,#" />', '<row Cells="#,,,#" />')
            .replace('<row Cells="#,b,+" />', '<row Cells="#,@,#" />')
            .replace('        <require Role="fixture:bed" Min="1" Max="1" />\n', "")
        )
        self.write_repo(BUILDINGS, architecture)
        result = self.check()
        self.assertIn("quality.procedural-shell", self.codes(result))

    def test_blueprint_fallback_recovers_names_without_masking_warning(self) -> None:
        broken = self.base / "ObjectBlueprints" / "Broken.xml"
        broken.write_text(
            '<objects><object Name="Recovered Fixture" /><text V="&#11;" /></objects>\n',
            encoding="utf-8",
        )
        architecture = ARCHITECTURE.replace(
            'Blueprint="TestBed"', 'Blueprint="Recovered Fixture"'
        )
        self.write_repo(BUILDINGS, architecture)
        result = self.check()
        self.assertTrue(result.ok, result.report())
        self.assertEqual(
            [notice.code for notice in result.notices], ["blueprint.xml-fallback"]
        )
        self.assertNotIn("blueprint.missing", self.codes(result))

    def test_missing_blueprint_and_dtd_are_hard_faults(self) -> None:
        architecture = ARCHITECTURE.replace(
            'Blueprint="TestBed"', 'Blueprint="Not A Blueprint"'
        )
        self.write_repo(BUILDINGS, architecture)
        self.assertIn("blueprint.missing", self.codes(self.check()))

        self.write_repo(
            '<!DOCTYPE x [<!ENTITY boom "x">]>\n' + BUILDINGS,
            ARCHITECTURE,
        )
        self.assertIn("xml.dtd", self.codes(self.check()))

    def test_paid_material_and_declared_technology_are_architecture_truth(self) -> None:
        unpaid = BUILDINGS.replace(
            'Materials="stone:1,timber:1"', 'Materials="stone:1"'
        )
        self.write_repo(unpaid, ARCHITECTURE)
        self.assertIn("material.unpaid", self.codes(self.check()))

        high_tech = ARCHITECTURE.replace(
            'Key="wall" Blueprint="TestWall" Role="wall" Material="stone" MinTech="hands"',
            'Key="wall" Blueprint="TestWall" Role="wall" Material="stone" MinTech="workshop"',
        )
        self.write_repo(BUILDINGS, high_tech)
        self.assertIn("palette.tech-underdeclared", self.codes(self.check()))

        declared = BUILDINGS.replace('Plot="S"', 'Plot="S" MinTech="workshop"')
        self.write_repo(declared, high_tech)
        result = self.check()
        self.assertTrue(result.ok, result.report())

    def test_population_furniture_and_animated_walls_need_stable_wrappers(self) -> None:
        unstable_bed = ARCHITECTURE.replace('Blueprint="TestBed"', 'Blueprint="Bed"')
        self.write_repo(BUILDINGS, unstable_bed)
        self.assertIn("fixture.unstable-blueprint", self.codes(self.check()))

        unstable_wall = ARCHITECTURE.replace(
            'Blueprint="TestWall"', 'Blueprint="LowSandstoneWall"'
        )
        self.write_repo(BUILDINGS, unstable_wall)
        self.assertIn("fixture.unstable-blueprint", self.codes(self.check()))

        unstable_base_rock = ARCHITECTURE.replace(
            'Blueprint="TestWall"', 'Blueprint="BaseWallRock"'
        )
        self.write_repo(BUILDINGS, unstable_base_rock)
        self.assertIn("fixture.unstable-blueprint", self.codes(self.check()))

    def test_pass_labels_must_match_inherited_blueprint_physics(self) -> None:
        solid_walk = ARCHITECTURE.replace(
            'Char="," Ground="$floor"',
            'Char="," Ground="$floor" Structure="$wall"',
        )
        self.write_repo(BUILDINGS, solid_walk)
        self.assertIn("passability.walk-solid", self.codes(self.check()))

        open_wall = (
            (self.base / "ObjectBlueprints.xml")
            .read_text(encoding="utf-8")
            .replace(
                'Name="TestWall"><part Name="Physics" Solid="true"',
                'Name="TestWall"><part Name="Physics" Solid="false"',
            )
        )
        (self.base / "ObjectBlueprints.xml").write_text(open_wall, encoding="utf-8")
        self.write_repo(BUILDINGS, ARCHITECTURE)
        self.assertIn("passability.blocked-open", self.codes(self.check()))

    def test_shipped_heart_keeps_rite_and_all_earlier_fabric(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        (self.repo / "KingdomBuildings.xml").write_text(
            (source_repo / "RuntimeData" / "KingdomBuildings.xml").read_text(
                encoding="utf-8"
            ),
            encoding="utf-8",
        )
        for source in sorted(
            (source_repo / "Architecture").glob("KingdomArchitectures*.xml")
        ):
            (self.repo / source.name).write_text(
                source.read_text(encoding="utf-8"), encoding="utf-8"
            )
        (self.repo / "KingdomArchitectures-Test.xml").unlink()
        result = CHECKER.run_check(self.repo)
        self.assertFalse(
            any(issue.code.startswith("heart.") for issue in result.issues),
            result.report(),
        )

        civic_path = self.repo / "KingdomArchitectures-CivicFaith.xml"
        civic = civic_path.read_text(encoding="utf-8").replace(
            '<row Cells="PPBPP" />', '<row Cells="PBPPP" />', 1
        )
        civic_path.write_text(civic, encoding="utf-8")
        self.assertIn("heart.basin-rite", self.codes(CHECKER.run_check(self.repo)))

    def test_immutable_first_basin_is_existing_authority_not_a_material_debit(
        self,
    ) -> None:
        architecture = ARCHITECTURE.replace(
            'Blueprint="TestBed" Role="sleep" Material="timber"',
            'Blueprint="r_KingdomFirstBasin" Role="sleep" Material="scrap"',
        )
        self.write_repo(BUILDINGS, architecture)
        self.assertNotIn("material.unpaid", self.codes(self.check()))

    def test_shipped_snapshots_fit_the_exact_runtime_codec_envelope(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        issues = []
        buildings = CHECKER.load_buildings(
            CHECKER._discover(source_repo, "KingdomBuildings.xml"), source_repo, issues
        )
        model = CHECKER.load_architectures(
            CHECKER._discover(source_repo, "KingdomArchitectures*.xml"),
            source_repo,
            issues,
        )
        self.assertEqual([], issues)
        payload, encoded = CHECKER._snapshot_maxima(buildings, model)
        keyed_payload, keyed_encoded, maximum_key = CHECKER._snapshot_maximum(
            buildings, model
        )
        self.assertEqual((payload, encoded), (keyed_payload, keyed_encoded))
        self.assertIn("arcology/civic-xl/fallback/", maximum_key)
        # Regression for the native-only failure this gate originally missed: several valid XL
        # maps are larger than the old 4,600-byte limit, but every shipped mapping fits the new
        # bounded codec contract with measured headroom.
        self.assertGreater(payload, 4600)
        self.assertLessEqual(payload, CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES)
        self.assertLessEqual(encoded, CHECKER.MAX_SNAPSHOT_CHARS)
        largest_possible_text = CHECKER.SNAPSHOT_TEXT_OVERHEAD + 4 * (
            (CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES + 2) // 3
        )
        self.assertLessEqual(largest_possible_text, CHECKER.MAX_SNAPSHOT_CHARS)

        result = CHECKER.run_check(source_repo)
        self.assertTrue(result.ok, result.report())
        self.assertEqual(result.max_snapshot_key, maximum_key)
        self.assertIn("maps: 513 (176 source / 337 generated)", result.report())
        self.assertIn(f"({maximum_key})", result.report())

    def test_shipped_entrance_census_is_live_routable_in_all_poses(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        issues = []
        model = CHECKER.load_architectures(
            CHECKER._discover(source_repo, "KingdomArchitectures*.xml"),
            source_repo,
            issues,
        )
        self.assertEqual([], issues)
        configs = 0
        entrances = 0
        interior = 0
        maximum = 0
        for tier in model.tiers:
            for variant in tier.variants:
                configs += 1
                architecture_map = model.maps[variant.map_key or tier.map_key]
                for x, y, glyph in CHECKER._cells(architecture_map):
                    if "entrance:public" not in glyph.anchors:
                        continue
                    route = CHECKER._entrance_egress(architecture_map, x, y)
                    self.assertIsNotNone(route, f"{architecture_map.key} {x},{y}")
                    maximum = max(maximum, len(route[0]))
                    entrances += 1
                    if (
                        0 < x < architecture_map.width - 1
                        and 0 < y < architecture_map.height - 1
                    ):
                        interior += 1
        self.assertEqual(530, configs)
        self.assertEqual(2120, configs * 4)
        # Housing boundary/door cleanup removed redundant entrance observations and moved the
        # surviving exact doors onto their intended lot boundaries.
        self.assertEqual(649, entrances)
        self.assertEqual(417, interior)
        self.assertEqual(9, maximum)

    def test_heart_checker_roster_matches_runtime_and_refuses_drift(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        self.assertEqual(
            CHECKER.HEART_BUILD_KEYS,
            CHECKER._runtime_heart_build_keys(source_repo),
        )
        self.assertEqual(5, len(CHECKER.HEART_BUILD_KEYS))
        growth = self.repo / "Growth"
        growth.mkdir()
        (growth / "KingdomPlotHeartRules.cs").write_text(
            'HeartRungKeys = new string[1] { "heartbasin" };\n', encoding="utf-8"
        )
        self.assertIn("heart.runtime-drift", self.codes(self.check()))

    def test_static_codec_contract_is_pinned_to_runtime_source(self) -> None:
        """Runtime codec edits must update independent gate in same change."""

        source_repo = CHECKER_PATH.parents[1]
        runtime = "\n".join(
            (source_repo / "Growth" / name).read_text(encoding="utf-8")
            for name in (
                "KingdomArchitectureRules.cs",
                "KingdomArchitectureCodecRules.cs",
                "KingdomArchitectureDecodeRules.cs",
            )
        )

        def runtime_int(name: str) -> int:
            match = re.search(
                rf"public const int {re.escape(name)}\s*=\s*(\d+)\s*;", runtime
            )
            self.assertIsNotNone(
                match, f"runtime constant {name} is absent or no longer literal"
            )
            return int(match.group(1))

        mirrored = {
            "MaxKeyChars": CHECKER.MAX_KEY_CHARS,
            "MaxBlueprintChars": CHECKER.MAX_BLUEPRINT_CHARS,
            "MaxPaletteSlots": CHECKER.MAX_PALETTE_SLOTS,
            "MaxMapArea": CHECKER.MAX_MAP_AREA,
            "MaxPlacements": CHECKER.MAX_PLACEMENTS,
            "MaxAnchors": CHECKER.MAX_ANCHORS,
            "MaxSnapshotPayloadBytes": CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES,
            "MaxSnapshotChars": CHECKER.MAX_SNAPSHOT_CHARS,
        }
        for name, static_value in mirrored.items():
            self.assertEqual(runtime_int(name), static_value, name)
        self.assertEqual(2, runtime_int("SnapshotSchema"))

        roads = (source_repo / "Growth" / "KingdomRoadRules.Routing.cs").read_text(
            encoding="utf-8"
        )
        margin = (source_repo / "Growth" / "KingdomPlotBoundsRules.cs").read_text(
            encoding="utf-8"
        )
        self.assertRegex(
            roads,
            rf"public const int MaxRouteCells\s*=\s*{CHECKER.MAX_ROUTE_CELLS}\s*;",
        )
        self.assertRegex(
            margin, rf"public const int RoadMargin\s*=\s*{CHECKER.ROAD_MARGIN}\s*;"
        )

        start = runtime.index("private static bool TryEncodeSnapshotVersion")
        end = runtime.index("public static bool TryDecodeSnapshot", start)
        writer = runtime[start:end]
        # These counts pin a2's four-byte header, seven metadata strings, five table
        # counts/tables, six pose bytes, three-byte cells, text+three-byte anchors,
        # and eleven-byte placements. Any writer-field change must update the mirror.
        self.assertEqual(34, writer.count("writer.Write("))
        self.assertEqual(13, writer.count("WriteText(writer,"))
        self.assertIn("Writer.Write((ushort)bytes.Length);", runtime)


if __name__ == "__main__":
    unittest.main()
