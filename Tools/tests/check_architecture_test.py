#!/usr/bin/env python3
"""Isolated tests for Tools/check-architecture.py."""

from __future__ import annotations

import importlib.util
import re
import sys
import tempfile
import textwrap
import unittest
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
            '<object Name="r_KingdomDelveUp" Inherits="StairsUp" /></objects>\n', encoding="utf-8"
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

    @staticmethod
    def codes(result) -> set[str]:
        return {issue.code for issue in result.issues}

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

    def test_schema_coverage_dimensions_and_topology_faults(self) -> None:
        lower_root = ARCHITECTURE.replace("KingdomArchitectures", "kingdomarchitectures")
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
        self.assertIn("frontage.road-exterior", self.codes(self.check()))

    def test_functional_verticality_needs_runtime_owned_external_pair(self) -> None:
        decorative = ARCHITECTURE.replace(
            "  </palette>",
            '    <slot Key="stair" Blueprint="Sunken Room Stairs" Role="vertical-core" '
            'Material="stone" MinTech="hands" Natural="no" />\n  </palette>',
            1,
        ).replace(
            '    <glyph Char="b"',
            '    <glyph Char="^" Ground="$stair" Claim="building" Pass="walk" '
            'Cover="walled" Anchors="function:vertical-core" />\n    <glyph Char="b"',
            1,
        ).replace('Cells="#@,,#"', 'Cells="#@^,#"')
        self.write_repo(BUILDINGS, decorative)
        codes = self.codes(self.check())
        self.assertIn("travel.blueprint", codes)
        self.assertIn("travel.runtime-owner", codes)

        same_map = ARCHITECTURE.replace(
            "  </palette>",
            '    <slot Key="up" Blueprint="StairsUp" Role="vertical-up" Material="stone" '
            'MinTech="hands" Natural="no" />\n'
            '    <slot Key="down" Blueprint="StairsDown" Role="vertical-down" Material="stone" '
            'MinTech="hands" Natural="no" />\n  </palette>',
            1,
        ).replace(
            '    <glyph Char="b"',
            '    <glyph Char="^" Object="$up" Claim="building" Pass="walk" Cover="walled" '
            'Anchors="travel:up" Stateful="yes" />\n'
            '    <glyph Char="v" Object="$down" Claim="building" Pass="walk" Cover="walled" '
            'Anchors="travel:down" Stateful="yes" />\n    <glyph Char="b"',
            1,
        ).replace('Cells="#@,,#"', 'Cells="#@^,#"').replace('Cells="#,b,+"', 'Cells="#vb,+"')
        self.write_repo(BUILDINGS, same_map)
        codes = self.codes(self.check())
        self.assertIn("travel.link-owner", codes)
        self.assertIn("travel.same-map", codes)

        delve_building = BUILDINGS.replace('Key="hut"', 'Key="delve"')
        externally_paired = ARCHITECTURE.replace(
            'BuildKey="hut"', 'BuildKey="delve"'
        ).replace(
            "  </palette>",
            '    <slot Key="down" Blueprint="r_KingdomDelveDown" Role="vertical-down" '
            'Material="stone" MinTech="hands" Natural="no" />\n  </palette>',
            1,
        ).replace(
            '    <glyph Char="b"',
            '    <glyph Char="v" Object="$down" Claim="building" Pass="walk" '
            'Cover="walled" Anchors="travel:down" Stateful="yes" />\n    <glyph Char="b"',
            1,
        ).replace('Cells="#,b,+"', 'Cells="#vb,+"')
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
        architecture = ARCHITECTURE.replace('Blueprint="TestBed"', 'Blueprint="Recovered Fixture"')
        self.write_repo(BUILDINGS, architecture)
        result = self.check()
        self.assertTrue(result.ok, result.report())
        self.assertEqual([notice.code for notice in result.notices], ["blueprint.xml-fallback"])
        self.assertNotIn("blueprint.missing", self.codes(result))

    def test_missing_blueprint_and_dtd_are_hard_faults(self) -> None:
        architecture = ARCHITECTURE.replace('Blueprint="TestBed"', 'Blueprint="Not A Blueprint"')
        self.write_repo(BUILDINGS, architecture)
        self.assertIn("blueprint.missing", self.codes(self.check()))

        self.write_repo(
            '<!DOCTYPE x [<!ENTITY boom "x">]>\n' + BUILDINGS,
            ARCHITECTURE,
        )
        self.assertIn("xml.dtd", self.codes(self.check()))

    def test_paid_material_and_declared_technology_are_architecture_truth(self) -> None:
        unpaid = BUILDINGS.replace('Materials="stone:1,timber:1"', 'Materials="stone:1"')
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
        unstable_bed = ARCHITECTURE.replace(
            'Blueprint="TestBed"', 'Blueprint="Bed"'
        )
        self.write_repo(BUILDINGS, unstable_bed)
        self.assertIn("fixture.unstable-blueprint", self.codes(self.check()))

        unstable_wall = ARCHITECTURE.replace(
            'Blueprint="TestWall"', 'Blueprint="LowSandstoneWall"'
        )
        self.write_repo(BUILDINGS, unstable_wall)
        self.assertIn("fixture.unstable-blueprint", self.codes(self.check()))

    def test_pass_labels_must_match_inherited_blueprint_physics(self) -> None:
        solid_walk = ARCHITECTURE.replace(
            'Char="," Ground="$floor"',
            'Char="," Ground="$floor" Structure="$wall"',
        )
        self.write_repo(BUILDINGS, solid_walk)
        self.assertIn("passability.walk-solid", self.codes(self.check()))

        open_wall = (self.base / "ObjectBlueprints.xml").read_text(
            encoding="utf-8"
        ).replace('Name="TestWall"><part Name="Physics" Solid="true"',
                  'Name="TestWall"><part Name="Physics" Solid="false"')
        (self.base / "ObjectBlueprints.xml").write_text(open_wall, encoding="utf-8")
        self.write_repo(BUILDINGS, ARCHITECTURE)
        self.assertIn("passability.blocked-open", self.codes(self.check()))

    def test_shipped_heart_keeps_rite_and_all_earlier_fabric(self) -> None:
        source_repo = CHECKER_PATH.parents[1]
        (self.repo / "KingdomBuildings.xml").write_text(
            (source_repo / "KingdomBuildings.xml").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
        for source in sorted((source_repo / "Architecture").glob("KingdomArchitectures*.xml")):
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

    def test_immutable_first_basin_is_existing_authority_not_a_material_debit(self) -> None:
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
            CHECKER._discover(source_repo, "KingdomArchitectures*.xml"), source_repo, issues
        )
        self.assertEqual([], issues)
        payload, encoded = CHECKER._snapshot_maxima(buildings, model)
        # Regression for the native-only failure this gate originally missed: several valid XL
        # maps are larger than the old 4,600-byte limit, but every shipped mapping fits the new
        # bounded codec contract with measured headroom.
        self.assertGreater(payload, 4600)
        self.assertLessEqual(payload, CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES)
        self.assertLessEqual(encoded, CHECKER.MAX_SNAPSHOT_CHARS)
        largest_possible_text = (
            CHECKER.SNAPSHOT_TEXT_OVERHEAD
            + 4 * ((CHECKER.MAX_SNAPSHOT_PAYLOAD_BYTES + 2) // 3)
        )
        self.assertLessEqual(largest_possible_text, CHECKER.MAX_SNAPSHOT_CHARS)

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
            self.assertIsNotNone(match, f"runtime constant {name} is absent or no longer literal")
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
