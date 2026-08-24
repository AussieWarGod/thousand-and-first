"""Enforce the release art boundary and validate every vanilla tile reference.

The public package contains no original runtime bitmap sprites. `Tile=` values may point at the
base game's packed assets, but no copy of those assets and no retired custom draft belongs under
Textures/. This check proves both sides:

  bundled art       any runtime PNG/BMP in Textures is a release failure
  local reference   any ThousandAndFirst/ Tile= is a release failure
  unknown vanilla   any external tile path absent from the installed base XML corpus is a typo

The last tests do not unpack or copy game assets. They check that Qud names each path in its XML,
that the exact-cased packed asset key exists, and that every text fallback fits Qud's 256-character
renderer. Run from the repository root: python3 Art/check_wiring.py
"""

import io
import mmap
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


DEFAULT_BASE = "/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
STAGE_TOOL = os.path.join("Tools", "stage.sh")
RASTER_EXTENSIONS = (".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tga", ".webp")
LOCAL_PREFIX = "ThousandAndFirst/"


def read(path):
    with io.open(path, encoding="utf-8-sig") as handle:
        return handle.read()


def runtime_xml_paths():
    """Return the canonical staged XML set instead of maintaining a second inventory."""
    if not os.path.isfile(STAGE_TOOL):
        raise RuntimeError("canonical runtime inventory is missing: %s" % STAGE_TOOL)
    result = subprocess.run(
        [STAGE_TOOL, "list"],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if result.returncode != 0:
        detail = result.stderr.strip() or "exit %d" % result.returncode
        raise RuntimeError("canonical runtime inventory failed: %s" % detail)
    paths = sorted(
        line.strip() for line in result.stdout.splitlines()
        if line.strip().lower().endswith(".xml")
    )
    if not paths:
        raise RuntimeError("canonical runtime inventory contains no XML")
    missing = [path for path in paths if not os.path.isfile(path)]
    if missing:
        raise RuntimeError("staged XML is missing: %s" % ", ".join(missing))
    return paths


def tile_paths(attribute, value):
    """Yield paths from Tile and animation-frame attributes, ignoring `default`."""
    if "tile" not in attribute.lower():
        return []
    values = [value] if attribute.lower() == "tile" else value.split(",")
    paths = []
    for token in values:
        token = token.strip()
        if "=" in token:
            token = token.split("=", 1)[1].strip()
        if token.lower().endswith((".bmp", ".png")):
            paths.append(token)
    return paths


def referenced_tiles(paths):
    references = {}
    for path in paths:
        root = ET.parse(path).getroot()
        for element in root.iter():
            owner = element.get("DisplayName") or element.get("Name") or element.get("Key")
            for attribute, value in element.attrib.items():
                for tile in tile_paths(attribute, value):
                    references.setdefault(tile, []).append(
                        "%s:%s:%s" % (path, owner or element.tag, attribute)
                    )
    return references


def render_string_problems(paths):
    """Reject glyphs the Unity renderer turns into spaces.

    Render.Initialize converts a multi-character decimal RenderString to one character. A
    one-character value is already the intended scalar. GameManager's current Unity renderer has
    only Text_0.bmp through Text_255.bmp and explicitly replaces anything above U+00FF with a
    space, so both authored forms must resolve into that range.
    """
    problems = []
    for path in paths:
        root = ET.parse(path).getroot()
        for element in root.iter():
            if "RenderString" not in element.attrib:
                continue
            value = element.get("RenderString")
            owner = element.get("DisplayName") or element.get("Name") or element.get("Key")
            location = "%s:%s:RenderString" % (path, owner or element.tag)
            if not value:
                problems.append("empty RenderString cannot produce a glyph: %s" % location)
                continue
            if len(value) == 1:
                scalar = ord(value)
            else:
                if not re.fullmatch(r"[0-9]+", value):
                    problems.append(
                        "multi-character RenderString is not a decimal scalar: %r (%s)"
                        % (value, location)
                    )
                    continue
                scalar = int(value, 10)
            if scalar < 0 or scalar > 255:
                problems.append(
                    "RenderString scalar is outside Qud's 0..255 glyph atlas: %r -> U+%04X (%s)"
                    % (value, scalar, location)
                )
    return problems


def fixed_farmer_tile_problems(blueprint_path="ObjectBlueprints.xml"):
    """A BaseFarmer descendant's RandomTile builder otherwise overwrites its fixed render."""
    if not os.path.isfile(blueprint_path):
        return ["required runtime XML is missing: %s" % blueprint_path]
    objects = {
        element.get("Name"): element
        for element in ET.parse(blueprint_path).getroot().iter("object")
        if element.get("Name")
    }
    problems = []
    for name, element in sorted(objects.items()):
        fixed = any(
            part.get("Name") == "Render" and part.get("Tile")
            for part in element.findall("part")
        )
        if not fixed:
            continue
        at = element
        seen = set()
        inherits_farmer = False
        removes_random_tile = False
        while at is not None:
            current_name = at.get("Name")
            if current_name in seen:
                break
            seen.add(current_name)
            if any(
                child.tag == "removebuilder" and child.get("Name") == "RandomTile"
                for child in at
            ):
                removes_random_tile = True
            parent = at.get("Inherits")
            if parent == "BaseFarmer":
                inherits_farmer = True
                break
            at = objects.get(parent)
        if inherits_farmer and not removes_random_tile:
            problems.append(
                "%s fixes Render.Tile but inherits BaseFarmer RandomTile without removing it"
                % name
            )
    return problems


def bundled_rasters():
    found = []
    if not os.path.isdir("Textures"):
        return found
    for root, _dirs, files in os.walk("Textures"):
        for name in files:
            if name.lower().endswith(RASTER_EXTENSIONS):
                found.append(os.path.join(root, name))
    return sorted(found)


def vanilla_tiles(base):
    folder = os.path.join(base, "ObjectBlueprints")
    if not os.path.isdir(folder):
        return None
    paths = set()
    for name in sorted(os.listdir(folder)):
        if name.endswith(".xml"):
            text = re.sub(r"<!--.*?-->", "", read(os.path.join(folder, name)), flags=re.S)
            for value in re.findall(r'["\']([^"\']+\.(?:bmp|png)(?:,[^"\']*)?)["\']',
                    text, flags=re.I):
                for token in value.split(","):
                    token = token.strip()
                    if "=" in token:
                        token = token.split("=", 1)[1].strip()
                    if token.lower().endswith((".bmp", ".png")):
                        paths.add(token)
    return paths


def packed_asset_key(tile):
    """Return exact Unity resource name corresponding to a blueprint Tile value."""
    if tile.lower().startswith("assets_content_textures_"):
        return tile.replace("/", "_").replace("\\", "_")
    return "Assets_Content_Textures_" + tile.replace("/", "_").replace("\\", "_")


def packed_tile_problems(references, base):
    """Prove exact-cased tile keys exist in installed resources.assets."""
    resource_path = os.path.abspath(os.path.join(base, os.pardir, os.pardir, "resources.assets"))
    if not os.path.isfile(resource_path):
        return ["installed packed asset database not found: %s" % resource_path]
    problems = []
    wanted = {}
    for tile, owners in sorted(references.items()):
        if tile.startswith(LOCAL_PREFIX):
            continue
        key = packed_asset_key(tile)
        try:
            needle = key.encode("ascii")
        except UnicodeEncodeError:
            problems.append(
                "tile path cannot name an ASCII packed asset: %s (%s)"
                % (tile, ", ".join(owners))
            )
            continue
        wanted[needle] = (tile, key, owners)
    if not wanted:
        return problems
    with open(resource_path, "rb") as handle:
        packed = mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ)
        try:
            pattern = re.compile(
                b"|".join(re.escape(needle) for needle in sorted(wanted, key=len, reverse=True))
            )
            found = {match.group(0) for match in pattern.finditer(packed)}
        finally:
            packed.close()
    for needle, (tile, key, owners) in wanted.items():
        if needle not in found:
            problems.append(
                "exact packed tile asset is missing: %s -> %s (%s)"
                % (tile, key, ", ".join(owners))
            )
    return problems


def main():
    base = os.environ.get("TAF_QUD_BASE", DEFAULT_BASE)
    problems = []

    try:
        runtime_xml = runtime_xml_paths()
    except RuntimeError as error:
        problems.append(str(error))
        references = {}
    else:
        references = referenced_tiles(runtime_xml)
        if not references:
            problems.append("staged runtime XML contains no tile references")
        problems.extend(render_string_problems(runtime_xml))

    for path in bundled_rasters():
        problems.append("bundled runtime art is forbidden by release policy: %s" % path)

    problems.extend(fixed_farmer_tile_problems())

    for tile, owners in sorted(references.items()):
        if tile.startswith(LOCAL_PREFIX):
            problems.append(
                "local tile reference is forbidden by release policy: %s (%s)"
                % (tile, ", ".join(owners))
            )

    known_tiles = vanilla_tiles(base)
    if known_tiles is None:
        problems.append("installed base ObjectBlueprints directory not found: %s" % base)
    else:
        known_tiles_folded = {tile.casefold() for tile in known_tiles}
        for tile, owners in sorted(references.items()):
            if tile.startswith(LOCAL_PREFIX):
                continue
            if tile.casefold() not in known_tiles_folded:
                problems.append(
                    "tile path is not named by installed base XML: %s (%s)"
                    % (tile, ", ".join(owners))
                )

    problems.extend(packed_tile_problems(references, base))

    if problems:
        print("ART POLICY FAILED")
        for problem in problems:
            print("  " + problem)
        return 1

    print(
        "ART POLICY CLEAN: 0 bundled runtime rasters; %d vanilla tile paths verified"
        % len(references)
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
