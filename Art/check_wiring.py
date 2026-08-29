"""Enforce the release art boundary and validate every tile reference.

Vanilla references are preferred and verified against the installed game. Original local art is
allowed only when an exact record in Art/runtime-assets.json proves its file hash, source,
creator, license, fallback, and human review. This check proves both sides:

  bundled art       every runtime raster is allowlisted and hash/provenance exact
  local reference   every ThousandAndFirst/ Tile= resolves to that allowlist
  unknown vanilla   any external tile path absent from the installed base XML corpus is a typo

References are discovered from both staged XML attributes and staged C# string literals. Runtime
code can assign ``Render.Tile``/``PaintTile`` directly; ignoring those paths would make the XML
inventory a partial truth while still printing a release-wide count.

The last tests do not unpack or copy game assets. They check that Qud names each path in its XML,
that the engine-normalized packed asset key exists, and that every text fallback fits Qud's
256-character renderer. Qud's `SpriteManager` replaces path separators and lower-cases the whole
lookup key before resolving it, so requiring source-case equality would reject vanilla's own
`tiles/sw_torch_nofire.png` reference. Run from the repository root:
python3 Art/check_wiring.py
"""

import hashlib
import io
import json
import mmap
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


DEFAULT_BASE = "/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
STAGE_TOOL = os.path.join("Tools", "stage.sh")
RASTER_EXTENSIONS = (
    ".bmp", ".dds", ".gif", ".jpeg", ".jpg", ".png", ".tga", ".tif", ".tiff", ".webp",
)
LOCAL_PREFIX = "ThousandAndFirst/"
RUNTIME_ASSET_MANIFEST = os.path.join("Art", "runtime-assets.json")
TEXTURE_ROOT = "Textures"
REQUIRED_ASSET_FIELDS = (
    "tile", "path", "sha256", "creator", "created", "license", "source", "method",
    "fallback", "review",
)


def read(path):
    with io.open(path, encoding="utf-8-sig") as handle:
        return handle.read()


def runtime_paths(extension):
    """Return one canonical staged suffix set instead of maintaining another inventory."""
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
        if line.strip().lower().endswith(extension.lower())
    )
    if not paths:
        raise RuntimeError("canonical runtime inventory contains no %s files" % extension)
    missing = [path for path in paths if not os.path.isfile(path)]
    if missing:
        raise RuntimeError("staged %s is missing: %s" % (extension, ", ".join(missing)))
    return paths


def runtime_xml_paths():
    """Return the canonical staged XML set."""
    return runtime_paths(".xml")


def runtime_csharp_paths():
    """Return the canonical staged C# set, including optional integration shards."""
    return runtime_paths(".cs")


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


def csharp_string_literals(source):
    """Yield decoded-enough C# string literals with their starting line.

    This is a bounded lexer, not a C# parser. It deliberately skips line/block comments and
    character literals, understands ordinary/verbatim/interpolated prefixes, and handles raw
    string delimiters. Tile paths use no semantic escapes beyond slash/backslash, so decoding the
    ordinary quote and backslash escapes is sufficient for exact path inventory. Interpolation is
    retained literally; a dynamic value cannot accidentally equal a known vanilla path.
    """
    index = 0
    line = 1
    length = len(source)
    while index < length:
        if source.startswith("//", index):
            end = source.find("\n", index + 2)
            if end < 0:
                return
            index = end
            continue
        if source.startswith("/*", index):
            end = source.find("*/", index + 2)
            if end < 0:
                return
            line += source.count("\n", index, end + 2)
            index = end + 2
            continue
        if source[index] == "'":
            index += 1
            while index < length:
                if source[index] == "\\":
                    index += 2
                    continue
                if source[index] == "'":
                    index += 1
                    break
                if source[index] == "\n":
                    line += 1
                index += 1
            continue

        prefix_length = 0
        verbatim = False
        if source.startswith("$@\"", index) or source.startswith("@$\"", index):
            prefix_length = 3
            verbatim = True
        elif source.startswith("@\"", index):
            prefix_length = 2
            verbatim = True
        elif source.startswith("$\"", index):
            prefix_length = 2
        elif source[index] == '"':
            prefix_length = 1

        if prefix_length:
            quote_at = index + prefix_length - 1
            delimiter = 1
            while quote_at + delimiter < length and source[quote_at + delimiter] == '"':
                delimiter += 1
            start_line = line
            if delimiter >= 3:
                body_at = quote_at + delimiter
                end_token = '"' * delimiter
                end = source.find(end_token, body_at)
                if end < 0:
                    return
                body = source[body_at:end]
                yield body, start_line
                line += source.count("\n", index, end + delimiter)
                index = end + delimiter
                continue

            cursor = quote_at + 1
            pieces = []
            while cursor < length:
                char = source[cursor]
                if verbatim and char == '"' and cursor + 1 < length \
                        and source[cursor + 1] == '"':
                    pieces.append('"')
                    cursor += 2
                    continue
                if char == '"':
                    cursor += 1
                    break
                if not verbatim and char == "\\" and cursor + 1 < length:
                    escaped = source[cursor + 1]
                    pieces.append("\\" if escaped == "\\" else escaped)
                    cursor += 2
                    continue
                if char == "\n":
                    line += 1
                pieces.append(char)
                cursor += 1
            yield "".join(pieces), start_line
            index = cursor
            continue

        if source[index] == "\n":
            line += 1
        index += 1


def referenced_csharp_tiles(paths):
    """Return direct runtime tile literals from staged C# in the same shape as XML refs."""
    references = {}
    for path in paths:
        for value, line in csharp_string_literals(read(path)):
            tile = value.strip()
            if (not tile.lower().endswith(RASTER_EXTENSIONS)
                    or (tile.startswith(".") and "/" not in tile and "\\" not in tile)):
                continue
            references.setdefault(tile, []).append("%s:%d:CSharpString" % (path, line))
    return references


def merge_references(*reference_sets):
    merged = {}
    for references in reference_sets:
        for tile, owners in references.items():
            merged.setdefault(tile, []).extend(owners)
    return merged


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


def fixed_farmer_tile_problems(blueprint_path="RuntimeData/ObjectBlueprints.xml"):
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


def _safe_relative(path):
    if not isinstance(path, str) or not path or "\\" in path or path.startswith("/"):
        return False
    parts = path.split("/")
    return all(part not in ("", ".", "..") for part in parts)


def runtime_asset_records(manifest_path=RUNTIME_ASSET_MANIFEST):
    """Return exact local-tile records and all self-contained provenance problems."""
    problems = []
    records = {}
    try:
        with io.open(manifest_path, encoding="utf-8") as handle:
            document = json.load(handle)
    except (OSError, ValueError) as error:
        return records, ["runtime asset manifest cannot be read: %s" % error]
    if not isinstance(document, dict) or document.get("schema") != 1:
        return records, ["runtime asset manifest must be an object with schema 1"]
    if set(document) != {"schema", "assets"} or not isinstance(document.get("assets"), list):
        return records, ["runtime asset manifest must contain only schema and an assets array"]

    paths = {}
    tiles_folded = {}
    paths_folded = {}
    for index, row in enumerate(document["assets"]):
        where = "runtime asset record %d" % index
        if not isinstance(row, dict) or set(row) != set(REQUIRED_ASSET_FIELDS):
            problems.append(
                "%s must contain exactly: %s" % (where, ", ".join(REQUIRED_ASSET_FIELDS))
            )
            continue
        if any(not isinstance(row[field], str) or not row[field].strip()
               for field in REQUIRED_ASSET_FIELDS):
            problems.append("%s has an empty or non-string field" % where)
            continue
        tile = row["tile"]
        path = row["path"]
        source = row["source"]
        expected_path = TEXTURE_ROOT + "/" + tile[len(LOCAL_PREFIX):]
        if (not tile.startswith(LOCAL_PREFIX) or not _safe_relative(tile)
                or not tile.lower().endswith(RASTER_EXTENSIONS)):
            problems.append("%s has an invalid local tile path: %s" % (where, tile))
            continue
        if not _safe_relative(path) or path != expected_path:
            problems.append(
                "%s path must be the exact staged mapping %s" % (where, expected_path)
            )
            continue
        if not _safe_relative(source) or source.startswith(TEXTURE_ROOT + "/"):
            problems.append("%s source must be a safe non-runtime repository path" % where)
            continue
        digest = row["sha256"]
        if not re.fullmatch(r"[0-9a-f]{64}", digest):
            problems.append("%s sha256 must be 64 lowercase hexadecimal characters" % where)
            continue
        tile_key = tile.casefold()
        path_key = path.casefold()
        if tile_key in tiles_folded:
            problems.append("local tile collision: %s and %s" % (tiles_folded[tile_key], tile))
            continue
        if path_key in paths_folded:
            problems.append("runtime asset path collision: %s and %s" % (paths_folded[path_key], path))
            continue
        tiles_folded[tile_key] = tile
        paths_folded[path_key] = path
        if not os.path.isfile(path) or os.path.islink(path):
            problems.append("allowlisted runtime asset is missing or linked: %s" % path)
            continue
        if not os.path.isfile(source) or os.path.islink(source):
            problems.append("allowlisted editable source is missing or linked: %s" % source)
            continue
        with open(path, "rb") as stream:
            actual = hashlib.sha256(stream.read()).hexdigest()
        if actual != digest:
            problems.append("runtime asset hash differs from manifest: %s" % path)
            continue
        records[tile] = row
        paths[path] = tile

    for path in bundled_rasters():
        if path not in paths:
            problems.append("bundled runtime art is absent from provenance manifest: %s" % path)
    return records, problems


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
    """Prove Qud's normalized tile keys exist in installed resources.assets.

    `Kobold.SpriteManager` performs `Replace('\\', '_').Replace('/', '_').ToLower()`
    before lookup. Mirror that native boundary exactly instead of imposing a casing rule the
    installed base XML itself does not satisfy.
    """
    resource_path = os.path.abspath(os.path.join(base, os.pardir, os.pardir, "resources.assets"))
    if not os.path.isfile(resource_path):
        return ["installed packed asset database not found: %s" % resource_path]
    problems = []
    wanted = {}
    for tile, owners in sorted(references.items()):
        if tile.startswith(LOCAL_PREFIX):
            continue
        key = packed_asset_key(tile).lower()
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
                "engine-normalized packed tile asset is missing: %s -> %s (%s)"
                % (tile, key, ", ".join(owners))
            )
    return problems


def main():
    base = os.environ.get("TAF_QUD_BASE", DEFAULT_BASE)
    problems = []

    try:
        runtime_xml = runtime_xml_paths()
        runtime_csharp = runtime_csharp_paths()
    except RuntimeError as error:
        problems.append(str(error))
        references = {}
    else:
        references = merge_references(
            referenced_tiles(runtime_xml), referenced_csharp_tiles(runtime_csharp)
        )
        if not references:
            problems.append("staged runtime XML/C# contains no tile references")
        problems.extend(render_string_problems(runtime_xml))

    local_assets, asset_problems = runtime_asset_records()
    problems.extend(asset_problems)

    problems.extend(fixed_farmer_tile_problems())

    for tile, owners in sorted(references.items()):
        if tile.startswith(LOCAL_PREFIX) and tile not in local_assets:
            problems.append(
                "local tile reference has no exact provenance record: %s (%s)"
                % (tile, ", ".join(owners))
            )

    referenced_local = {tile for tile in references if tile.startswith(LOCAL_PREFIX)}
    for tile, row in sorted(local_assets.items()):
        if tile not in referenced_local:
            problems.append(
                "allowlisted runtime asset is not referenced by staged XML: %s (%s)"
                % (tile, row["path"])
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
        "ART POLICY CLEAN: %d allowlisted local tile paths; %d vanilla tile paths verified"
        % (len(local_assets), len(references) - len(referenced_local))
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
