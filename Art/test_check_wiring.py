"""Negative fixtures for the release art policy. Uses no game files or copied assets."""

import io
import os
import tempfile
import unittest

from Art import check_wiring


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

    def test_packed_tile_check_accepts_exact_canonical_case(self):
        base = self.packed_base(
            b"header\0Assets_Content_Textures_Creatures_caste_20.bmp\0footer"
        )
        references = {"Creatures/caste_20.bmp": ["fixture"]}
        self.assertEqual([], check_wiring.packed_tile_problems(references, base))

    def test_packed_tile_check_rejects_wrong_case(self):
        base = self.packed_base(
            b"header\0Assets_Content_Textures_Creatures_caste_20.bmp\0footer"
        )
        references = {"creatures/caste_20.bmp": ["fixture"]}
        problems = check_wiring.packed_tile_problems(references, base)
        self.assertEqual(1, len(problems))
        self.assertIn("exact packed tile asset is missing", problems[0])

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


if __name__ == "__main__":
    unittest.main()
