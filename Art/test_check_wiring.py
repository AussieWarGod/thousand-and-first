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
