"""Negative fixtures for the release art policy. Uses no game files or copied assets."""

import hashlib
import io
import json
import os
import tempfile
import unittest

from Art import check_wiring, check_xml_refs


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

    def test_district_reference_check_reads_split_rules_authority(self):
        self.assertEqual(
            {"agrarian", "market", "craft", "shrine", "garrison", "academy"},
            check_xml_refs.known_districts(),
        )

    def test_repository_runtime_asset_manifest_is_canonical_and_complete(self):
        records, problems = check_wiring.runtime_asset_records()
        self.assertEqual([], problems)
        self.assertEqual({}, records)

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
