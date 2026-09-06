"""Negative fixtures for the release art policy. Uses no game files or copied assets."""

import hashlib
import io
import json
import os
import tempfile
import unittest
from unittest import mock

from Art import check_wiring, check_xml_refs


class RaisingCeremonyTests(unittest.TestCase):
    """Source classification only; synthetic setup never proves a raising ceremony."""

    FIXTURE = os.path.join("Harness", "KingdomSubsidenceRungNativeFixture.cs")
    SYNTHETIC = (
        "namespace ThousandAndFirst.Harness\n"
        "// Synthetic initial work/home state, not construction, adoption, or lodging acceptance.\n"
        "internal sealed class KingdomSubsidenceRungNativeFixture {\n"
        'Work.SetIntProperty("KingdomBuilt", 1); }\n'
    )
    STAMP = 'Work.SetIntProperty("KingdomBuilt", 1);\n'
    CEREMONY = "KingdomCeremony.OnBuildingRaised(work);\n"

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        prior = os.getcwd()
        self.addCleanup(os.chdir, prior)
        os.chdir(temporary.name)
        self.write("Growth/KingdomScaffold.cs", self.STAMP + self.CEREMONY)
        self.write("Growth/KingdomPlot2.cs", self.STAMP + self.CEREMONY + "PlanQuote(plot);\n")
        self.write("Growth/KingdomPlanMarker.cs", "PlanQuote(plot);\n")
        self.write("Core/KingdomFounding.cs", self.STAMP)
        self.write(self.FIXTURE, self.SYNTHETIC)
        self.write("manifest.json", json.dumps({"Directories": [{"Paths": ["/Core/", "/Growth/"]}]}))
        patcher = mock.patch.object(
            check_wiring, "runtime_paths",
            return_value=["Core/KingdomFounding.cs", "Growth/KingdomScaffold.cs", "Growth/KingdomPlot2.cs"],
        )
        self.inventory = patcher.start()
        self.addCleanup(patcher.stop)

    def write(self, path, content):
        parent = os.path.dirname(path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        with io.open(path, "w", encoding="utf-8") as stream:
            stream.write(content)

    def test_exact_disclosed_developer_fixture_is_a_separate_category(self):
        self.assertNotIn(self.FIXTURE, check_xml_refs.RAISING_PATHS + check_xml_refs.ADOPTING_PATHS)
        self.assertEqual([], check_xml_refs.raising_ceremony_problems())
        self.inventory.assert_called_once_with(".cs")

    def test_unknown_harness_sibling_shard_and_production_stamper_still_refuse(self):
        for path in (
            "Harness/OtherFixture.cs",
            "Harness/KingdomSubsidenceRungNativeFixture.Extra.cs",
            "Growth/UnregisteredStamper.cs",
        ):
            with self.subTest(path=path):
                self.write(path, self.SYNTHETIC)
                problems = check_xml_refs.raising_ceremony_problems()
                self.assertTrue(any(path in row and "neither a known raising path" in row for row in problems))
                os.unlink(path)

    def test_each_production_raiser_still_requires_its_ceremony(self):
        for path in check_xml_refs.RAISING_PATHS:
            with self.subTest(path=path):
                original = check_xml_refs.read(path)
                self.write(path, original.replace(self.CEREMONY, ""))
                problems = check_xml_refs.raising_ceremony_problems()
                self.assertTrue(any(path in row and "without calling KingdomCeremony.OnBuildingRaised" in row for row in problems))
                self.write(path, original)

    def test_fixture_must_keep_its_exact_synthetic_disclosure_and_namespace(self):
        for token in (
            "namespace ThousandAndFirst.Harness",
            "internal sealed class KingdomSubsidenceRungNativeFixture",
            "Synthetic initial work/home state, not construction, adoption, or lodging acceptance.",
        ):
            with self.subTest(token=token):
                self.write(self.FIXTURE, self.SYNTHETIC.replace(token, ""))
                self.assertTrue(any("lacks its synthetic developer disclosure" in row
                                    for row in check_xml_refs.raising_ceremony_problems()))
        self.write(self.FIXTURE, self.SYNTHETIC)

    def test_ordinary_manifest_selection_or_staging_never_gets_fixture_exemption(self):
        for group in ({"Paths": ["/Harness/"]}, {"Path": "/harness/"}, {"Paths": ["/"]}):
            with self.subTest(group=group):
                self.write("manifest.json", json.dumps({"Directories": [group]}))
                self.assertTrue(any("selected by the ordinary manifest" in row
                                    for row in check_xml_refs.raising_ceremony_problems()))
        self.write("manifest.json", json.dumps({"Directories": [{"Paths": ["/Core/"]}]}))
        self.inventory.return_value.append(self.FIXTURE)
        self.assertTrue(any("leaked into ordinary staging" in row
                            for row in check_xml_refs.raising_ceremony_problems()))

    def test_unprovable_developer_containment_refuses(self):
        self.inventory.side_effect = RuntimeError("inventory unavailable")
        self.assertTrue(any("developer-only classification cannot be proved" in row
                            for row in check_xml_refs.raising_ceremony_problems()))
        self.inventory.side_effect = None
        self.write("manifest.json", "{}")
        self.assertTrue(any("developer-only classification cannot be proved" in row
                            for row in check_xml_refs.raising_ceremony_problems()))
        os.link(self.FIXTURE, self.FIXTURE + ".linked")
        self.assertTrue(any("ordinary contained file" in row
                            for row in check_xml_refs.raising_ceremony_problems()))
        os.unlink(self.FIXTURE + ".linked")
        os.rename(self.FIXTURE, self.FIXTURE + ".retained")
        os.symlink(os.path.basename(self.FIXTURE) + ".retained", self.FIXTURE)
        self.assertTrue(any("ordinary contained file" in row
                            for row in check_xml_refs.raising_ceremony_problems()))


class ArtPolicyTests(unittest.TestCase):
    def xml_file(self, body):
        handle, path = tempfile.mkstemp(suffix=".xml")
        os.close(handle)
        with io.open(path, "w", encoding="utf-8") as stream:
            stream.write(body)
        self.addCleanup(os.unlink, path)
        return path

    def packed_base(self, payload):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        coq_data = os.path.join(temporary.name, "CoQ_Data")
        base = os.path.join(coq_data, "StreamingAssets", "Base")
        os.makedirs(base)
        with open(os.path.join(coq_data, "resources.assets"), "wb") as stream:
            stream.write(payload)
        return base

    def test_chargen_icon_is_in_staged_xml_reference_set(self):
        path = self.xml_file(
            '<embarkmodules><icon Tile="ThousandAndFirst/preview.png" /></embarkmodules>'
        )
        references = check_wiring.referenced_tiles([path])
        self.assertIn("ThousandAndFirst/preview.png", references)

    def test_runtime_csharp_tile_literals_are_in_reference_set(self):
        handle, path = tempfile.mkstemp(suffix=".cs")
        os.close(handle)
        self.addCleanup(os.unlink, path)
        with io.open(path, "w", encoding="utf-8") as stream:
            stream.write(
                '// "Items/comment-only.bmp"\n'
                'var first = "Items/wrench.bmp";\n'
                'var second = @"Tiles/tile-dirt1.png";\n'
                'var fragment = "fragment.bmp";\n'
                'var extension = ".png";\n'
                'var glyph = \'x\'; /* "Items/block-comment.png" */\n'
            )
        references = check_wiring.referenced_csharp_tiles([path])
        self.assertEqual(
            {"Items/wrench.bmp", "Tiles/tile-dirt1.png", "fragment.bmp"}, set(references)
        )
        self.assertIn(":2:CSharpString", references["Items/wrench.bmp"][0])

    def test_reference_sets_merge_owners_without_losing_duplicates(self):
        merged = check_wiring.merge_references(
            {"Items/shared.bmp": ["xml"]},
            {"Items/shared.bmp": ["code"], "Items/other.bmp": ["code"]},
        )
        self.assertEqual(["xml", "code"], merged["Items/shared.bmp"])
        self.assertEqual(["code"], merged["Items/other.bmp"])

    def test_vanilla_tile_census_includes_root_chargen_xml(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        base = temporary.name
        blueprints = os.path.join(base, "ObjectBlueprints")
        os.makedirs(blueprints)
        with io.open(os.path.join(blueprints, "ZoneTerrain.xml"), "w",
                encoding="utf-8") as stream:
            stream.write('<part Name="Render" Tile="Terrain/ground.bmp" />')
        with io.open(os.path.join(base, "EmbarkModules.xml"), "w",
                encoding="utf-8") as stream:
            stream.write('<grid Tile="Terrain/chargen-only.bmp" />')
        self.assertEqual(
            {"Terrain/ground.bmp", "Terrain/chargen-only.bmp"},
            check_wiring.vanilla_tiles(base),
        )

    def test_reference_checks_read_split_source_authorities(self):
        self.assertEqual(
            {"agrarian", "market", "craft", "shrine", "garrison", "academy"},
            check_xml_refs.known_districts(),
        )
        self.assertEqual([], check_xml_refs.raising_ceremony_problems())
        self.assertEqual([], check_xml_refs.crop_chain_problems(None))
        problems, lots, manifests = check_xml_refs.hosted_arcology_authority()
        self.assertEqual([], problems)
        self.assertEqual("r_KingdomArcologyGrowbed",
                         lots["arcologyterrace"]["producer"])
        self.assertEqual(14, lots["arcologyterrace"]["count"])
        self.assertEqual("food:14", lots["arcologyterrace"]["supports"])
        self.assertEqual("HydroponicTerrace",
                         manifests["arcologyterrace"]["programme"])
        self.assertEqual(14, manifests["arcologyterrace"]["blueprints"].count(
            "r_KingdomArcologyGrowbed"))
        routes = check_xml_refs.architecture_expansion_routes()
        self.assertIn(("fieldrows", "grange", "Large"), routes)
        self.assertNotIn(("fieldrows", "grange", "Medium"), routes)
        self.assertEqual([], check_xml_refs.building_reference_problems())

        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = os.path.join(temporary.name, "Authority.cs")
        shard = os.path.join(temporary.name, "Authority.Split.cs")
        with io.open(root, "w", encoding="utf-8") as stream:
            stream.write("public static partial class Authority {}\n")
        with io.open(shard, "w", encoding="utf-8") as stream:
            stream.write("public static partial class Authority { const int Proof = 1; }\n")
        self.assertEqual([root, shard], check_xml_refs.source_family_paths(root))
        self.assertIn("const int Proof = 1", check_xml_refs.read_source_family(root))
        os.unlink(root)
        self.assertEqual([], check_xml_refs.source_family_paths(root))

    def test_no_base_mode_ignores_an_installed_default(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.assertIsNone(check_xml_refs.selected_base(["--no-base"], temporary.name))
        self.assertEqual(
            "/licensed/base",
            check_xml_refs.selected_base(["--base", "/licensed/base"], temporary.name),
        )
        self.assertEqual(temporary.name, check_xml_refs.selected_base([], temporary.name))
        with self.assertRaisesRegex(ValueError, "choose only one"):
            check_xml_refs.selected_base(
                ["--no-base", "--base", "/licensed/base"], temporary.name
            )

    def test_sealed_runtime_activation_contract_is_exact(self):
        self.assertTrue(
            check_xml_refs.is_sealed_runtime_activation(
                "assentingmoot", "assentingmoot-runtime-v1"
            )
        )
        self.assertTrue(
            check_xml_refs.is_sealed_runtime_activation(
                "stasisvault", "stasisvault-runtime-v1"
            )
        )
        self.assertFalse(
            check_xml_refs.is_sealed_runtime_activation(
                "assentingmoot", "stasisvault-runtime-v1"
            )
        )
        self.assertFalse(
            check_xml_refs.is_sealed_runtime_activation(
                "some-other-building", "assentingmoot-runtime-v1"
            )
        )
        self.assertEqual([], check_xml_refs.research_reference_problems(None))

    def test_repository_runtime_asset_manifest_is_canonical_and_complete(self):
        records, problems = check_wiring.runtime_asset_records()
        self.assertEqual([], problems)
        self.assertEqual({}, records)

    def test_repository_semantic_render_aliases_are_honest(self):
        self.assertEqual([], check_wiring.semantic_render_alias_problems())
        corpus = check_xml_refs.blueprint_corpus(check_wiring.DEFAULT_BASE)
        glyph_only = {
            "r_KingdomFounderStatue": "223",
            "r_KingdomWatchtower": "024",
            "r_KingdomHideRack": "209",
            "r_KingdomMud": ",",
            "r_KingdomSnapjawTrailDen": "127",
            "r_KingdomIssachariRiflePorch": "239",
            "r_KingdomTemplarPurityArsenal": "239",
            "r_KingdomWardensWatchLodge": "127",
        }
        for name, glyph in glyph_only.items():
            render = check_xml_refs.resolved_part(corpus, name, "Render")
            self.assertEqual(glyph, render.get("RenderString"), name)
            self.assertIsNone(render.get("Tile"), name)
        lamp = check_xml_refs.resolved_part(
            corpus, "r_KingdomArcologySpectrumLamp", "Render"
        )
        self.assertEqual("items/sw_hitech_lightsource1.bmp", lamp.get("Tile"))

    def test_installed_fixture_cannot_regress_to_body_or_portable_item_art(self):
        path = self.xml_file(
            '<objects><object Name="r_KingdomStasisVault" Inherits="Furniture">'
            '<part Name="Render" Tile="Items/sw_stasis_projector.bmp" '
            'RenderString="230" /></object></objects>'
        )
        problems = check_wiring.semantic_render_alias_problems(path)
        self.assertTrue(any("r_KingdomStasisVault" in row for row in problems))
        self.assertTrue(any("semantically foreign art" in row for row in problems))

    def test_hindren_fixture_cannot_claim_nachams_unique_machine(self):
        path = self.xml_file(
            '<objects><object Name="r_KingdomHindrenLoomSemantic" Inherits="Furniture">'
            '<part Name="Render" Tile="Furniture/chiliad-nacham-loom.png" '
            'RenderString="232" /></object></objects>'
        )
        problems = check_wiring.semantic_render_alias_problems(path)
        self.assertTrue(any("r_KingdomHindrenLoomSemantic" in row for row in problems))
        self.assertTrue(any("semantically foreign art" in row for row in problems))

    def test_snapjaw_cache_cannot_claim_chiliads_regional_basket(self):
        path = self.xml_file(
            '<objects><object Name="r_KingdomCreedSnapjawMeatCache" '
            'Inherits="Furniture"><part Name="Render" '
            'Tile="Furniture/chiliad-basket.png" RenderString="229" />'
            '</object></objects>'
        )
        problems = check_wiring.semantic_render_alias_problems(path)
        self.assertTrue(any("r_KingdomCreedSnapjawMeatCache" in row for row in problems))
        self.assertTrue(any("semantically foreign art" in row for row in problems))

    def test_original_runtime_asset_requires_exact_source_hash_and_metadata(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = temporary.name
        os.makedirs(os.path.join(root, "Textures", "architecture"))
        os.makedirs(os.path.join(root, "Art", "Sources"))
        runtime = os.path.join(root, "Textures", "architecture", "marker.png")
        source = os.path.join(root, "Art", "Sources", "marker.grid")
        with open(runtime, "wb") as stream:
            stream.write(b"original-test-raster")
        with io.open(source, "w", encoding="utf-8") as stream:
            stream.write("editable source\n")
        digest = hashlib.sha256(b"original-test-raster").hexdigest()
        manifest = {
            "schema": 1,
            "assets": [{
                "tile": "ThousandAndFirst/architecture/marker.png",
                "path": "Textures/architecture/marker.png",
                "sha256": digest,
                "creator": "fixture author",
                "created": "2026-08-26",
                "license": "MIT",
                "source": "Art/Sources/marker.grid",
                "method": "direct pixel authoring",
                "fallback": "#",
                "review": "fixture human review"
            }]
        }
        manifest_path = os.path.join(root, "Art", "runtime-assets.json")
        with io.open(manifest_path, "w", encoding="utf-8") as stream:
            json.dump(manifest, stream)
        prior = os.getcwd()
        try:
            os.chdir(root)
            records, problems = check_wiring.runtime_asset_records(
                os.path.join("Art", "runtime-assets.json")
            )
        finally:
            os.chdir(prior)
        self.assertEqual([], problems)
        self.assertIn("ThousandAndFirst/architecture/marker.png", records)

    def test_unlisted_runtime_asset_is_rejected(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = temporary.name
        os.makedirs(os.path.join(root, "Textures"))
        os.makedirs(os.path.join(root, "Art"))
        with open(os.path.join(root, "Textures", "orphan.PNG"), "wb") as stream:
            stream.write(b"orphan")
        with io.open(os.path.join(root, "Art", "runtime-assets.json"), "w",
                     encoding="utf-8") as stream:
            json.dump({"schema": 1, "assets": []}, stream)
        prior = os.getcwd()
        try:
            os.chdir(root)
            _records, problems = check_wiring.runtime_asset_records(
                os.path.join("Art", "runtime-assets.json")
            )
        finally:
            os.chdir(prior)
        self.assertTrue(any("absent from provenance manifest" in row for row in problems))

    def test_animation_frame_path_is_extracted(self):
        paths = check_wiring.tile_paths(
            "TileAnimationFrames",
            "0=default,100=ThousandAndFirst/frame.png,200=Items/known.bmp",
        )
        self.assertEqual(
            ["ThousandAndFirst/frame.png", "Items/known.bmp"],
            paths,
        )

    def test_render_string_rejects_scalar_outside_packed_glyph_atlas(self):
        path = self.xml_file(
            '<objects><object Name="Blank"><part Name="Render" '
            'DisplayName="blank" RenderString="&#9617;" /></object></objects>'
        )
        problems = check_wiring.render_string_problems([path])
        self.assertEqual(1, len(problems))
        self.assertIn("U+2591", problems[0])

    def test_every_staged_render_string_fits_packed_glyph_atlas(self):
        self.assertEqual(
            [],
            check_wiring.render_string_problems(check_wiring.runtime_xml_paths()),
        )

    def test_render_string_accepts_literal_and_decimal_scalars_through_255(self):
        path = self.xml_file(
            """<objects>
  <object Name="ASCII"><part Name="Render" RenderString="A" /></object>
  <object Name="Control"><part Name="Render" RenderString="009" /></object>
  <object Name="Decimal"><part Name="Render" RenderString="255" /></object>
  <object Name="Literal"><part Name="Render" RenderString="&#255;" /></object>
</objects>"""
        )
        self.assertEqual([], check_wiring.render_string_problems([path]))

    def test_render_string_rejects_decimal_256(self):
        path = self.xml_file(
            '<objects><object Name="Blank"><part Name="Render" '
            'RenderString="256" /></object></objects>'
        )
        problems = check_wiring.render_string_problems([path])
        self.assertEqual(1, len(problems))
        self.assertIn("U+0100", problems[0])

    def test_render_string_rejects_malformed_multi_character_value(self):
        path = self.xml_file(
            '<objects><object Name="Broken"><part Name="Render" '
            'RenderString="12x" /></object></objects>'
        )
        problems = check_wiring.render_string_problems([path])
        self.assertEqual(1, len(problems))
        self.assertIn("not a decimal scalar", problems[0])

    def test_packed_tile_check_accepts_engine_normalized_key(self):
        base = self.packed_base(
            b"header\0assets_content_textures_creatures_caste_20.bmp\0footer"
        )
        references = {"Creatures/caste_20.bmp": ["fixture"]}
        self.assertEqual([], check_wiring.packed_tile_problems(references, base))

    def test_packed_tile_check_accepts_source_case_variation_like_native(self):
        base = self.packed_base(
            b"header\0assets_content_textures_creatures_caste_20.bmp\0footer"
        )
        references = {"creatures/caste_20.bmp": ["fixture"]}
        self.assertEqual([], check_wiring.packed_tile_problems(references, base))

    def test_packed_tile_check_rejects_missing_normalized_key(self):
        base = self.packed_base(
            b"header\0assets_content_textures_creatures_other.bmp\0footer"
        )
        references = {"Creatures/caste_20.bmp": ["fixture"]}
        problems = check_wiring.packed_tile_problems(references, base)
        self.assertEqual(1, len(problems))
        self.assertIn("engine-normalized packed tile asset is missing", problems[0])

    def test_fixed_farmer_tile_requires_random_tile_removal(self):
        path = self.xml_file(
            """<objects>
  <object Name="LocalBase" Inherits="BaseFarmer" />
  <object Name="Fixed" Inherits="LocalBase">
    <part Name="Render" Tile="Creatures/example.bmp" />
  </object>
</objects>"""
        )
        self.assertEqual(
            ["Fixed fixes Render.Tile but inherits BaseFarmer RandomTile without removing it"],
            check_wiring.fixed_farmer_tile_problems(path),
        )

    def test_fixed_farmer_tile_accepts_vanilla_removal_shape(self):
        path = self.xml_file(
            """<objects>
  <object Name="LocalBase" Inherits="BaseFarmer" />
  <object Name="Fixed" Inherits="LocalBase">
    <removebuilder Name="RandomTile" />
    <part Name="Render" Tile="Creatures/example.bmp" />
  </object>
</objects>"""
        )
        self.assertEqual([], check_wiring.fixed_farmer_tile_problems(path))

    def test_blueprint_inheritance_rejects_missing_installed_parent(self):
        path = self.xml_file(
            '<objects><object Name="Child" Inherits="MissingBase" /></objects>'
        )
        self.assertEqual(
            [
                "blueprint Child inherits MissingBase, which neither TAF nor the installed "
                "base defines"
            ],
            check_xml_refs.blueprint_inheritance_problems(path, {"Object"}),
        )

    def test_blueprint_inheritance_accepts_local_and_installed_parents(self):
        path = self.xml_file(
            '<objects><object Name="Local" Inherits="Object" />'
            '<object Name="Child" Inherits="Local" /></objects>'
        )
        self.assertEqual(
            [],
            check_xml_refs.blueprint_inheritance_problems(path, {"Object"}),
        )

    def test_blueprint_inheritance_rejects_local_cycle_without_game_files(self):
        path = self.xml_file(
            '<objects><object Name="A" Inherits="B" />'
            '<object Name="B" Inherits="A" /></objects>'
        )
        problems = check_xml_refs.blueprint_inheritance_problems(path)
        self.assertEqual(1, len(problems))
        self.assertIn("blueprint inheritance loops", problems[0])


if __name__ == "__main__":
    unittest.main()
