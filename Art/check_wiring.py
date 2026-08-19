"""Verify every drawn tile is reachable in game, and every referenced tile exists.

This exists because four tiles shipped with PNGs, source grids, previews, and a contact sheet,
and no `Tile=` attribute in any blueprint. Everything the art pipeline checks passed. The art
was correct and rendered nowhere, which is the failure mode a build step that only looks at
art cannot see.

Two directions, both of which are silent at runtime:

  orphaned art       a PNG with no blueprint referencing it -- drawn, shipped, invisible
  dangling reference a Tile= pointing at a file that is not there -- Qud falls back to the
                     ASCII glyph without complaint, so it looks like a styling choice

Also checks that a Render carrying highlight pixels declares DetailColor, since a two-tone
tile with no DetailColor renders its highlight in the default and quietly loses the second
tone the grid was drawn to have.

Run from the repository root:  python3 Art/check_wiring.py
"""

import io
import os
import re
import sys
import xml.etree.ElementTree as ET

TEXTURE_DIR = os.path.join("Textures", "ThousandAndFirst")
SOURCE_DIR = os.path.join("Art", "src")
BLUEPRINTS = "ObjectBlueprints.xml"
PREFIX = "ThousandAndFirst/"


def highlight_glyphs(tile_path):
    """True if the source grid contains any pixel that resolves to the highlight tone."""
    with io.open(tile_path, encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("#!"):
                continue
            # 'o' is a solid highlight; 'd'/'D' checker and 's'/'S' stipple both mix highlight in.
            if any(glyph in line for glyph in ("o", "d", "D")):
                return True
    return False


def main():
    if not os.path.isfile(BLUEPRINTS):
        sys.exit("run from the repository root: %s not found" % BLUEPRINTS)

    on_disk = set()
    for name in sorted(os.listdir(TEXTURE_DIR)):
        if name.endswith(".png"):
            on_disk.add(name)

    referenced = {}
    tree = ET.parse(BLUEPRINTS)
    for obj in tree.getroot().iter("object"):
        blueprint = obj.get("Name", "<unnamed>")
        for part in obj.iter("part"):
            if part.get("Name") != "Render":
                continue
            tile = part.get("Tile")
            if not tile or not tile.startswith(PREFIX):
                continue
            referenced.setdefault(os.path.basename(tile), []).append(
                (blueprint, part.get("DetailColor"))
            )

    problems = []

    for name in sorted(on_disk - set(referenced)):
        problems.append(
            "orphaned art: %s has no blueprint Tile= and renders nowhere" % name)

    for name in sorted(set(referenced) - on_disk):
        users = ", ".join(b for b, _ in referenced[name])
        problems.append(
            "dangling reference: %s is referenced by %s but does not exist" % (name, users))

    for name in sorted(set(referenced) & on_disk):
        source = os.path.join(SOURCE_DIR, name[:-4] + ".tile")
        if not os.path.isfile(source):
            problems.append("missing source grid: %s has no .tile in %s" % (name, SOURCE_DIR))
            continue
        if not highlight_glyphs(source):
            continue
        for blueprint, detail in referenced[name]:
            if not detail:
                problems.append(
                    "%s draws highlight pixels but %s declares no DetailColor, so the second "
                    "tone renders in the default" % (name, blueprint))

    if problems:
        print("TILE WIRING FAILED")
        for problem in problems:
            print("  " + problem)
        return 1

    print("TILE WIRING CLEAN: %d tiles drawn, %d referenced, every one reachable"
          % (len(on_disk), len(referenced)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
