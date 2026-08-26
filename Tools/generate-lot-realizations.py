#!/usr/bin/env python3
"""Materialise every larger exact lot binding as concrete, inspectable XML.

The authored minimum-size maps remain the topology authority. This build-time tool places that
authored footprint inside each larger canonical lot, leaving explicit ``.`` yard cells. It never
runs in Qud and the runtime never synthesises or falls back to these records.
"""

from __future__ import annotations

import argparse
import copy
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Tuple


LOT_DIMENSIONS: Dict[str, Tuple[int, int]] = {
    "S": (5, 4),
    "M": (8, 6),
    "L": (12, 9),
    "XL": (20, 14),
}
LOT_ORDER = tuple(LOT_DIMENSIONS)
HEART_BUILD_KEYS = {"heartbasin", "heartwaterstone", "heartmoot", "heartcourt"}
OUTPUT_NAME = "KingdomArchitectures-LotRealizations.xml"
IDENTITY_SELECTOR_ATTRIBUTES = ("Cultures", "Species", "Genotypes", "Bodies")


class GenerationError(RuntimeError):
    pass


def _source_roots(repository: Path) -> Iterable[Tuple[Path, ET.Element]]:
    architecture = repository / "Architecture"
    paths = sorted(architecture.glob("KingdomArchitectures*.xml"))
    for path in paths:
        if path.name == OUTPUT_NAME:
            continue
        yield path, ET.parse(path).getroot()


def _public_entrances(architecture_map: ET.Element) -> List[Tuple[int, int]]:
    entrance_chars = {
        glyph.get("Char", "")
        for glyph in architecture_map.findall("glyph")
        if "entrance:public" in glyph.get("Anchors", "").split(",")
    }
    result: List[Tuple[int, int]] = []
    for y, row in enumerate(architecture_map.findall("row")):
        for x, char in enumerate(row.get("Cells", "")):
            if char in entrance_chars:
                result.append((x, y))
    return result


def _road_side(architecture_map: ET.Element) -> str:
    width = int(architecture_map.get("Width", "0"))
    height = int(architecture_map.get("Height", "0"))
    entrances = _public_entrances(architecture_map)
    if not entrances:
        raise GenerationError(
            f"road map {architecture_map.get('Key')!r} has no public entrance"
        )
    common = {"N", "E", "S", "W"}
    for x, y in entrances:
        common &= {
            side
            for side, touches in (
                ("N", y == 0),
                ("E", x == width - 1),
                ("S", y == height - 1),
                ("W", x == 0),
            )
            if touches
        }
    if not common:
        raise GenerationError(
            f"road map {architecture_map.get('Key')!r} public entrances do not share "
            "one exterior frontage"
        )
    return next(side for side in ("S", "E", "W", "N") if side in common)


def _expanded_map(
    source: ET.Element, target_size: str, facing: str, generated_key: str
) -> ET.Element:
    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    target_width, target_height = LOT_DIMENSIONS[target_size]
    if source_width > target_width or source_height > target_height:
        raise GenerationError(
            f"map {source.get('Key')!r} {source_width}x{source_height} does not fit "
            f"target {target_size} {target_width}x{target_height}"
        )
    rows = [row.get("Cells", "") for row in source.findall("row")]
    if len(rows) != source_height or any(len(row) != source_width for row in rows):
        raise GenerationError(f"map {source.get('Key')!r} has malformed source rows")

    # Heart-facing lots keep the complete authored minimum-lot coordinate block against the
    # canonical north (heart) side. Rotation carries it to the chosen world side, and all added
    # ground lies behind it. Road lots instead pin the source block to its authored door side so
    # each transformed public entrance has a real exterior road cell.
    if facing == "heart":
        offset_x = (target_width - source_width) // 2
        offset_y = 0
    elif facing == "road":
        side = _road_side(source)
        if side == "S":
            offset_x = (target_width - source_width) // 2
            offset_y = target_height - source_height
        elif side == "N":
            offset_x = (target_width - source_width) // 2
            offset_y = 0
        elif side == "E":
            offset_x = target_width - source_width
            offset_y = (target_height - source_height) // 2
        else:
            offset_x = 0
            offset_y = (target_height - source_height) // 2
    else:
        raise GenerationError(f"binding has unsupported Facing={facing!r}")

    canvas = [["." for _x in range(target_width)] for _y in range(target_height)]
    for y, row in enumerate(rows):
        for x, char in enumerate(row):
            canvas[offset_y + y][offset_x + x] = char

    result = ET.Element(
        "map",
        {
            "Key": generated_key,
            "Width": str(target_width),
            "Height": str(target_height),
            "DefaultCover": source.get("DefaultCover", ""),
        },
    )
    for glyph in source.findall("glyph"):
        result.append(copy.deepcopy(glyph))
    for row in canvas:
        ET.SubElement(result, "row", {"Cells": "".join(row)})
    return result


def generate(repository: Path) -> Tuple[str, int, int, int]:
    roots = list(_source_roots(repository))
    if not roots:
        raise GenerationError("no authored KingdomArchitectures XML sources found")

    maps: Dict[str, ET.Element] = {}
    occupied_plan_keys = set()
    for path, root in roots:
        if root.tag != "KingdomArchitectures" or root.get("Schema") != "1":
            raise GenerationError(f"{path} is not KingdomArchitectures schema 1")
        for architecture_map in root.findall("map"):
            key = architecture_map.get("Key", "")
            if not key or key in maps:
                raise GenerationError(f"duplicate or empty source map key {key!r}")
            maps[key] = architecture_map
        for plan in root.findall("plan"):
            key = plan.get("Key", "")
            if not key or key in occupied_plan_keys:
                raise GenerationError(f"duplicate or empty source plan key {key!r}")
            occupied_plan_keys.add(key)

    output = ET.Element("KingdomArchitectures", {"Schema": "1"})
    generated_maps: Dict[Tuple[str, str, str], str] = {}
    occupied_map_keys = set(maps)
    plans: List[ET.Element] = []
    tier_count = 0

    def map_for(source_key: str, target_size: str, facing: str) -> str:
        identity = (source_key, target_size, facing)
        existing = generated_maps.get(identity)
        if existing is not None:
            return existing
        source = maps.get(source_key)
        if source is None:
            raise GenerationError(f"unknown source map {source_key!r}")
        key = f"{source_key}-lot-{target_size.lower()}-{facing}"
        if len(key) > 128:
            raise GenerationError(f"generated map key exceeds 128 characters: {key!r}")
        if key in occupied_map_keys:
            raise GenerationError(f"generated map key collides with another map: {key!r}")
        occupied_map_keys.add(key)
        generated_maps[identity] = key
        output.append(_expanded_map(source, target_size, facing, key))
        return key

    for _path, root in roots:
        for source_plan in root.findall("plan"):
            source_plan_key = source_plan.get("Key", "")
            for source_binding in source_plan.findall("binding"):
                tiers = source_binding.findall("tier")
                if not tiers:
                    continue
                build_keys = {tier.get("BuildKey", "") for tier in tiers}
                if build_keys & HEART_BUILD_KEYS:
                    if not build_keys <= HEART_BUILD_KEYS:
                        raise GenerationError(
                            f"binding {source_binding.get('Key')!r} mixes heart and ordinary tiers"
                        )
                    continue
                source_size = source_binding.get("Size", "")
                facing = source_binding.get("Facing", "")
                if source_size not in LOT_DIMENSIONS:
                    raise GenerationError(
                        f"binding {source_binding.get('Key')!r} has unknown size {source_size!r}"
                    )
                source_index = LOT_ORDER.index(source_size)
                for target_size in LOT_ORDER[source_index + 1 :]:
                    binding_key = source_binding.get("Key", "")
                    plan_key = f"lot-{target_size.lower()}-{source_plan_key}-{binding_key}"
                    generated_binding_key = f"lot-{target_size.lower()}-{binding_key}"
                    if len(plan_key) > 128 or len(generated_binding_key) > 128:
                        raise GenerationError(
                            f"generated plan/binding key exceeds 128 characters: {plan_key!r}"
                        )
                    if plan_key in occupied_plan_keys:
                        raise GenerationError(
                            f"generated plan key collides with another plan: {plan_key!r}"
                        )
                    occupied_plan_keys.add(plan_key)
                    plan = ET.Element("plan", {"Key": plan_key})
                    binding_attributes = dict(source_binding.attrib)
                    binding_attributes["Key"] = generated_binding_key
                    binding_attributes["Size"] = target_size
                    binding = ET.SubElement(plan, "binding", binding_attributes)
                    for source_tier in tiers:
                        tier = copy.deepcopy(source_tier)
                        # Identity selectors remain attached to each concrete exact-lot copy.
                        # Assert before rewriting map refs so a future selective copier cannot
                        # silently turn culture/body variants into fallbacks.
                        source_variants = source_tier.findall("variant")
                        copied_variants = tier.findall("variant")
                        if len(source_variants) != len(copied_variants):
                            raise GenerationError(
                                f"variants were not preserved for tier {source_tier.get('Key')!r}"
                            )
                        for source_variant, variant in zip(source_variants, copied_variants):
                            for attribute in IDENTITY_SELECTOR_ATTRIBUTES:
                                if source_variant.get(attribute) != variant.get(attribute):
                                    raise GenerationError(
                                        f"identity selector {attribute} was not preserved for "
                                        f"tier {source_tier.get('Key')!r}"
                                    )
                        source_map_key = tier.get("Map", "")
                        tier.set("Map", map_for(source_map_key, target_size, facing))
                        for variant in tier.findall("variant"):
                            variant_map_key = variant.get("Map")
                            if variant_map_key:
                                variant.set(
                                    "Map", map_for(variant_map_key, target_size, facing)
                                )
                        binding.append(tier)
                        tier_count += 1
                    plans.append(plan)

    for plan in plans:
        output.append(plan)
    ET.indent(output, space="  ")
    body = ET.tostring(output, encoding="unicode", short_empty_elements=True)
    header = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<!-- GENERATED by Tools/generate-lot-realizations.py. Do not hand-edit.\n"
        "     Concrete exact maps below preserve authored topology and expose all added yard cells.\n"
        "     Regenerate by running the generator in write mode. -->\n"
    )
    return header + body + "\n", len(generated_maps), len(plans), tier_count


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="repository root (default: parent of Tools)",
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true", help="write the checked-in XML")
    mode.add_argument("--check", action="store_true", help="fail if checked-in XML is stale")
    arguments = parser.parse_args(argv)
    repository = arguments.repo_root.resolve()
    target = repository / "Architecture" / OUTPUT_NAME
    try:
        text, map_count, plan_count, tier_count = generate(repository)
    except (ET.ParseError, OSError, ValueError, GenerationError) as error:
        print(f"lot realization generation failed: {error}", file=sys.stderr)
        return 1
    if arguments.write:
        target.write_text(text, encoding="utf-8")
        print(
            f"wrote {target}: {map_count} maps, {plan_count} bindings, {tier_count} tiers"
        )
        return 0
    try:
        current = target.read_text(encoding="utf-8")
    except OSError as error:
        print(f"lot realization XML is missing or unreadable: {error}", file=sys.stderr)
        return 1
    if current != text:
        print(
            "lot realization XML is stale; run "
            "python3 Tools/generate-lot-realizations.py --write",
            file=sys.stderr,
        )
        return 1
    print(
        f"lot realization XML current: {map_count} maps, {plan_count} bindings, {tier_count} tiers"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
