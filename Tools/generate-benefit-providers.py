#!/usr/bin/env python3
"""Materialise reviewed physical benefit fixtures into blueprints and authored maps.

The catalogue is deliberately not read as supply.  This tool only installs the declarations in
``benefit_provider_manifest.py`` on one real object per authored map variant, emits portable
siblings for ordinary roles, and keeps the generated blocks deterministic.  Architecture edits
are narrow map-fragment replacements so unrelated hand-authored formatting survives.
"""

from __future__ import annotations

import argparse
import copy
import html
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Dict, Iterable, List, Mapping, MutableMapping, Optional, Sequence, Tuple

from benefit_provider_manifest import (
    BUILDING_FIXTURES, NATIVE_PORTABLES, YARD_FIXTURES, ProviderFixture,
)


ARCHITECTURE_OUTPUT = "KingdomArchitectures-LotRealizations.xml"
OBJECT_BEGIN = "  <!-- BENEFIT-PROVIDER-CONTENT:BEGIN (generated) -->"
OBJECT_END = "  <!-- BENEFIT-PROVIDER-CONTENT:END -->"
POP_BEGIN = "  <!-- BENEFIT-PROVIDER-ACQUISITION:BEGIN (generated) -->"
POP_END = "  <!-- BENEFIT-PROVIDER-ACQUISITION:END -->"
TECH_ORDER = {"hands": 0, "salvage": 1, "workshop": 2, "foundry": 3, "arclight": 4}
TECH_TIER = {"hands": 1, "salvage": 2, "workshop": 3, "foundry": 4, "arclight": 5}
MATERIAL_PRIORITY = {
    "arclight": ("workedmetal", "shapedstone", "marble", "scrap", "stone", "timber"),
    "foundry": ("workedmetal", "shapedstone", "marble", "scrap", "stone", "timber"),
    "workshop": ("shapedtimber", "shapedstone", "marble", "stone", "timber", "scrap"),
    "salvage": ("scrap", "stone", "timber", "mud", "canvas"),
    "hands": ("marble", "stone", "timber", "canvas", "mud", "brush"),
}
MATERIAL_ALIASES = {
    "scrapmetal": "scrap", "sawntimber": "shapedtimber",
    "dressedstone": "shapedstone", "brush": "brush",
}
CHARACTERS = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ~!%?;:"


class GenerationError(RuntimeError):
    pass


def parser() -> ET.XMLParser:
    return ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))


def parsed(path: Path) -> ET.Element:
    return ET.parse(path, parser=parser()).getroot()


def architecture_roots(repo: Path) -> Iterable[Tuple[Path, ET.Element]]:
    for path in sorted((repo / "Architecture").glob("KingdomArchitectures*.xml")):
        if path.name != ARCHITECTURE_OUTPUT:
            yield path, parsed(path)


def building_rows(repo: Path) -> Dict[str, ET.Element]:
    return {
        row.get("Key", ""): row
        for row in parsed(repo / "RuntimeData" / "KingdomBuildings.xml").findall("building")
    }


def tally_materials(row: ET.Element) -> Tuple[str, ...]:
    found: List[str] = []
    for token in row.get("Materials", "").split(","):
        key = token.split(":", 1)[0].strip().lower()
        key = MATERIAL_ALIASES.get(key, key)
        if key and key not in found:
            found.append(key)
    return tuple(found)


def tech_and_material(row: ET.Element) -> Tuple[str, str]:
    tech = row.get("MinTech", "hands").strip().lower() or "hands"
    if tech not in TECH_ORDER:
        tech = "hands"
    materials = tally_materials(row)
    for candidate in MATERIAL_PRIORITY[tech]:
        if candidate in materials:
            return tech, candidate
    return tech, (materials[0] if materials else "timber")


def map_pairs(root: ET.Element) -> Iterable[Tuple[str, str, str]]:
    for plan in root.findall("plan"):
        for binding in plan.findall("binding"):
            for tier in binding.findall("tier"):
                building = tier.get("BuildKey", "")
                pairs = [(tier.get("Map", ""), tier.get("Palette", ""))]
                pairs.extend((variant.get("Map") or tier.get("Map", ""),
                              variant.get("Palette") or tier.get("Palette", ""))
                             for variant in tier.findall("variant"))
                seen = set()
                for map_key, palette_key in pairs:
                    if map_key and palette_key and (map_key, palette_key) not in seen:
                        seen.add((map_key, palette_key))
                        yield building, map_key, palette_key


def scope_matches(spec: ProviderFixture, glyph: ET.Element) -> bool:
    claim = glyph.get("Claim", "").lower()
    cover = glyph.get("Cover", "open").lower()
    mode = glyph.get("Pass", "blocked").lower()
    scope = spec.scope.lower()
    if scope == "building":
        return claim == "building"
    if scope == "yard":
        return claim == "yard"
    if scope == "covered":
        return cover != "open"
    if scope in {"interior", "habitable"}:
        return claim == "building" and cover != "open" and mode != "blocked"
    if scope == "plot":
        return claim in {"building", "yard"}
    return False


def candidate_score(spec: ProviderFixture, glyph: ET.Element, count: int) -> int:
    obj = glyph.get("Object", "")
    anchors = glyph.get("Anchors", "").lower()
    if obj == "$building" or any(term in anchors for term in ("sleep", "bed:")):
        return -10_000
    score = 0
    if count == 1:
        score += 40
    if obj:
        score += 30
    if glyph.get("Anchors"):
        score += 15
    if not glyph.get("Structure"):
        score += 8
    if glyph.get("Pass", "").lower() == "adjacent":
        score += 4
    for rank, term in enumerate(spec.anchors):
        if term.lower() in anchors:
            score += 500 - rank * 5
    return score


def route_reserved(glyph: ET.Element) -> bool:
    return any(token.startswith("entrance:") or token.startswith("exit:")
               for token in glyph.get("Anchors", "").split(","))


def runtime_reserved(glyph: ET.Element) -> bool:
    """Keep fixtures whose exact blueprint is consumed by another runtime contract."""

    return any(token == "purpose:machine" or token == "fixture:first-basin"
               for token in glyph.get("Anchors", "").split(","))


def restored_runtime_object(spec: ProviderFixture, glyph: ET.Element) -> str:
    anchors = set(glyph.get("Anchors", "").split(","))
    if "fixture:first-basin" in anchors:
        return "$basin"
    if "purpose:machine" in anchors and spec.building == "deepbore":
        return "$bore"
    if "purpose:machine" in anchors and spec.building == "greatfoundry":
        return "$furnace"
    return ""


def eligible_shape(spec: ProviderFixture, glyph: ET.Element) -> bool:
    if not spec.blank_only:
        return True
    return not glyph.get("Object") and not glyph.get("Structure") \
        and not glyph.get("Anchors")


def choose_glyph(spec: ProviderFixture, architecture_map: ET.Element,
                 used: set[str]) -> Tuple[ET.Element, bool]:
    counts = Counter("".join(row.get("Cells", "") for row in architecture_map.findall("row")))
    candidates = [glyph for glyph in architecture_map.findall("glyph")
                  if glyph.get("Char") not in used and counts[glyph.get("Char", "")] > 0
                  and glyph.get("Pass", "blocked").lower() != "blocked"
                  and not route_reserved(glyph)
                  and not runtime_reserved(glyph)
                  and eligible_shape(spec, glyph)
                  and scope_matches(spec, glyph)]
    widened = False
    if not candidates and spec.scope.lower() == "building":
        # Open machinery can occupy one authored building cell even when its older map called the
        # whole apron yard. The copied glyph changes only that exact provider cell.
        candidates = [glyph for glyph in architecture_map.findall("glyph")
                      if glyph.get("Char") not in used
                      and counts[glyph.get("Char", "")] > 0
                      and glyph.get("Pass", "blocked").lower() != "blocked"
                      and not route_reserved(glyph)
                      and not runtime_reserved(glyph)
                      and eligible_shape(spec, glyph)
                      and glyph.get("Claim", "").lower() == "yard"]
        widened = bool(candidates)
    if not candidates:
        raise GenerationError(
            f"{spec.building}/{spec.component} has no {spec.scope} fixture cell in "
            f"{architecture_map.get('Key')!r}"
        )
    chosen = max(candidates, key=lambda glyph: (
        candidate_score(spec, glyph, counts[glyph.get("Char", "")]),
        -architecture_map.findall("glyph").index(glyph),
    ))
    if candidate_score(spec, chosen, counts[chosen.get("Char", "")]) < -1000:
        raise GenerationError(
            f"{spec.building}/{spec.component} could only replace a root or sleeping fixture in "
            f"{architecture_map.get('Key')!r}"
        )
    return chosen, widened


def unused_char(architecture_map: ET.Element) -> str:
    occupied = {glyph.get("Char", "") for glyph in architecture_map.findall("glyph")}
    occupied.update("".join(row.get("Cells", "") for row in architecture_map.findall("row")))
    for char in CHARACTERS:
        if char not in occupied:
            return char
    raise GenerationError(f"map {architecture_map.get('Key')!r} has no spare provider glyph")


def append_anchor(glyph: ET.Element, spec: ProviderFixture) -> None:
    anchor = f"benefit:{spec.building.replace(':', '-')}-{spec.component}"
    anchors = [item for item in glyph.get("Anchors", "").split(",") if item]
    if spec.installed_anchor and spec.installed_anchor not in anchors:
        anchors.append(spec.installed_anchor)
    if anchor not in anchors:
        anchors.append(anchor)
    glyph.set("Anchors", ",".join(anchors))


def install_on_map(spec: ProviderFixture, architecture_map: ET.Element,
                   slot_key: str, used: set[str]) -> bool:
    reference = "$" + slot_key
    existing = [glyph for glyph in architecture_map.findall("glyph")
                if glyph.get("Object") == reference]
    if existing:
        if len(existing) != 1:
            raise GenerationError(
                f"map {architecture_map.get('Key')!r} repeats provider slot {reference!r}"
            )
        current = existing[0]
        custody_anchor = f"benefit:{spec.building.replace(':', '-')}-{spec.component}"
        allowed_blank_anchors = {custody_anchor}
        if spec.installed_anchor:
            allowed_blank_anchors.add(spec.installed_anchor)
        current_anchors = {item for item in current.get("Anchors", "").split(",") if item}
        if current.get("Pass", "blocked").lower() != "blocked" \
                and not route_reserved(current) and not runtime_reserved(current) \
                and (not spec.blank_only or current_anchors.issubset(allowed_blank_anchors)) \
                and scope_matches(spec, current):
            before = dict(current.attrib)
            if spec.stateful:
                current.set("Stateful", "yes")
            else:
                current.attrib.pop("Stateful", None)
            append_anchor(current, spec)
            used.add(current.get("Char", ""))
            return before != current.attrib
        restored = restored_runtime_object(spec, current)
        current.attrib.pop("Object", None)
        current.attrib.pop("Stateful", None)
        anchor = f"benefit:{spec.building.replace(':', '-')}-{spec.component}"
        anchors = [item for item in current.get("Anchors", "").split(",")
                   if item and item != anchor]
        if anchors:
            current.set("Anchors", ",".join(anchors))
        else:
            current.attrib.pop("Anchors", None)
        if restored:
            current.set("Object", restored)
            current.set("Stateful", "yes")
    chosen, widened = choose_glyph(spec, architecture_map, used)
    old_char = chosen.get("Char", "")
    counts = Counter("".join(row.get("Cells", "") for row in architecture_map.findall("row")))
    target = chosen
    if counts[old_char] > 1:
        target = copy.deepcopy(chosen)
        target.set("Char", unused_char(architecture_map))
        position = list(architecture_map).index(chosen)
        architecture_map.insert(position + 1, target)
        replaced = False
        for row in architecture_map.findall("row"):
            cells = row.get("Cells", "")
            if not replaced and old_char in cells:
                row.set("Cells", cells.replace(old_char, target.get("Char", ""), 1))
                replaced = True
        if not replaced:
            raise GenerationError(f"map {architecture_map.get('Key')!r} lost glyph {old_char!r}")
    target.set("Object", reference)
    if spec.stateful:
        target.set("Stateful", "yes")
    else:
        target.attrib.pop("Stateful", None)
    if widened:
        target.set("Claim", "building")
    append_anchor(target, spec)
    used.add(target.get("Char", ""))
    if counts[old_char] == 1:
        used.add(old_char)
    return True


def slot_line(slot_key: str, spec: ProviderFixture, material: str, tech: str) -> str:
    role = f"physical-benefit-{spec.building.replace(':', '-')}-{spec.component}"
    return (f'    <slot Key="{html.escape(slot_key)}" Blueprint="{html.escape(spec.blueprint)}" '
            f'Role="{role}" Material="{material}" MinTech="{tech}" '
            f'Natural="{"yes" if spec.natural else "no"}" />')


def upsert_palette_slot(text: str, palette_key: str, slot_key: str, line: str) -> str:
    pattern = re.compile(
        rf'(^  <palette Key="{re.escape(palette_key)}">.*?)(^  </palette>)',
        re.MULTILINE | re.DOTALL,
    )
    match = pattern.search(text)
    if match is None:
        raise GenerationError(f"could not find palette fragment {palette_key!r}")
    body = match.group(1)
    slot_pattern = re.compile(
        rf'^[ \t]*<slot Key="{re.escape(slot_key)}"[^\n]*/>\r?\n?', re.MULTILINE
    )
    if slot_pattern.search(body):
        body = slot_pattern.sub("", body).rstrip() + "\n" + line + "\n"
    else:
        body = body.rstrip() + "\n" + line + "\n"
    return text[:match.start()] + body + match.group(2) + text[match.end():]


def replace_map(text: str, architecture_map: ET.Element) -> str:
    key = architecture_map.get("Key", "")
    node = copy.deepcopy(architecture_map)
    node.tail = None
    ET.indent(node, space="  ", level=1)
    fragment = "  " + ET.tostring(node, encoding="unicode", short_empty_elements=True).rstrip()
    pattern = re.compile(
        rf'^  <map Key="{re.escape(key)}"(?:\s|>).*?^  </map>',
        re.MULTILINE | re.DOTALL,
    )
    text, count = pattern.subn(fragment, text, count=1)
    if count != 1:
        raise GenerationError(f"could not replace source map {key!r}")
    return text


def replace_block(text: str, begin: str, end: str, block: str, closing: str) -> str:
    pattern = re.compile(re.escape(begin) + r".*?" + re.escape(end), re.DOTALL)
    complete = begin + "\n" + block.rstrip() + "\n" + end
    if pattern.search(text):
        return pattern.sub(complete, text, count=1)
    marker = "\n" + closing
    if marker not in text:
        raise GenerationError(f"could not find closing element {closing!r}")
    return text.replace(marker, "\n" + complete + marker, 1)


RENDER_PROFILES: Mapping[str, Mapping[str, str]] = {
    # All paths are shipped Qud 2.0.211.51 art.  Generated provider objects copy render only;
    # they never inherit a vanilla object's inventory, liquid, power, fire, or work behavior.
    "amenity": {"RenderString": "240", "Tile": "Assets_Content_Textures_Tiles_sw_chest.bmp",
                "ColorString": "&y", "TileColor": "&y", "DetailColor": "W"},
    "altar": {"RenderString": "228", "Tile": "Terrain/sw_monument7.bmp",
              "ColorString": "&Y", "TileColor": "&y", "DetailColor": "C"},
    "anvil": {"RenderString": "214", "Tile": "Items/sw_anvil.bmp",
              "ColorString": "&K", "TileColor": "&y", "DetailColor": "W"},
    "basin": {"RenderString": "229", "Tile": "Items/sw_catchbasin.bmp",
              "ColorString": "&B", "TileColor": "&b", "DetailColor": "C"},
    "bench": {"RenderString": "190", "Tile": "Items/sw_bench.bmp",
              "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "bore": {"RenderString": "004", "Tile": "Creatures/natural-weapon-drill.bmp",
             "ColorString": "&K", "TileColor": "&c", "DetailColor": "w"},
    "chair": {"RenderString": "190", "Tile": "Items/sw_folding_chair.bmp",
              "ColorString": "&c", "TileColor": "&c", "DetailColor": "C"},
    "charger": {"RenderString": "197", "Tile": "Items/sw_induction_station.bmp",
                "ColorString": "&c", "TileColor": "&c", "DetailColor": "C"},
    "council": {"RenderString": "227", "Tile": "Items/sw_table_sleek.bmp",
                "ColorString": "&C", "TileColor": "&c", "DetailColor": "W"},
    "cutface": {"RenderString": "177", "Tile": "Tiles2/sw_rubble_2.bmp",
                "ColorString": "&w", "TileColor": "&w", "DetailColor": "K"},
    "dais": {"RenderString": "254", "Tile": "Items/sw_chair_throne.bmp",
             "ColorString": "&W", "TileColor": "&W", "DetailColor": "Y"},
    "desk": {"RenderString": "227", "Tile": "Items/sw_table_desk.bmp",
             "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "fire": {"RenderString": "249", "Tile": "Items/sw_campfire_noflame.png",
             "ColorString": "&R", "TileColor": "&r", "DetailColor": "w"},
    "forge": {"RenderString": "*", "Tile": "Items/sw_forge.bmp",
              "ColorString": "&R^K", "TileColor": "&w^k", "DetailColor": "R"},
    "furnace": {"RenderString": "234", "Tile": "Items/sw_glass_furnace.bmp",
                "ColorString": "&R", "TileColor": "&y", "DetailColor": "Y"},
    "gauge": {"RenderString": "232", "Tile": "Items/sw_fluxthing.bmp",
              "ColorString": "&W", "TileColor": "&w", "DetailColor": "C"},
    "loom": {"RenderString": "247", "Tile": "Items/sw_sewing_machine.bmp",
             "ColorString": "&M", "TileColor": "&w", "DetailColor": "C"},
    "machine": {"RenderString": "254", "Tile": "Items/sw_multi_cabinet_1.bmp",
                "ColorString": "&c", "TileColor": "&c", "DetailColor": "C"},
    "memorial": {"RenderString": "239", "Tile": "Terrain/tile_tombstone1.png",
                 "ColorString": "&y", "TileColor": "&y", "DetailColor": "K"},
    "mill": {"RenderString": "15", "Tile": "Items/sw_millstone_1.bmp",
             "ColorString": "&y", "TileColor": "&y", "DetailColor": "W"},
    "oven": {"RenderString": "234", "Tile": "Items/sw_oven.bmp",
             "ColorString": "&w", "TileColor": "&w", "DetailColor": "R"},
    "rack": {"RenderString": "209", "Tile": "Items/sw_weapons_rack.bmp",
             "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "rostrum": {"RenderString": "227", "Tile": "Items/sw_table_low_drawers.bmp",
                "ColorString": "&w", "TileColor": "&w", "DetailColor": "W"},
    "ornate": {"RenderString": "227", "Tile": "Items/sw_table_ornate_1.bmp",
               "ColorString": "&C", "TileColor": "&y", "DetailColor": "C"},
    "lectern": {"RenderString": "227", "Tile": "Items/sw_table_cylinder.bmp",
                "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "screen": {"RenderString": "197", "Tile": "Items/sw_fence_gates_2_open.bmp",
               "ColorString": "&K", "TileColor": "&c", "DetailColor": "C"},
    "shelf": {"RenderString": "182", "Tile": "Items/sw_bookshelf1.bmp",
              "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "shrine": {"RenderString": "228", "Tile": "Terrain/tile_tombstone1.png",
               "ColorString": "&y", "TileColor": "&y", "DetailColor": "g"},
    "standard": {"RenderString": "|", "Tile": "Terrain/sw_monument1.bmp",
                 "ColorString": "&y", "TileColor": "&w", "DetailColor": "W"},
    "table": {"RenderString": "227", "Tile": "Assets_Content_Textures_Tiles_sw_table.bmp",
              "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "teleporter": {"RenderString": "008", "Tile": "Items/sw_teleporter_pad.bmp",
                   "ColorString": "&K^m", "TileColor": "&K", "DetailColor": "M"},
    "vase": {"RenderString": "009", "Tile": "Items/sw_vase_long_broken.bmp",
             "ColorString": "&y", "TileColor": "&y", "DetailColor": "W"},
    "wheel": {"RenderString": "21", "Tile": "Items/sw_waterwheel_1.bmp",
              "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "windmill": {"RenderString": "21", "Tile": "Items/sw_windmill_1.bmp",
                 "ColorString": "&w", "TileColor": "&y", "DetailColor": "W"},
    "workbench": {"RenderString": "227", "Tile": "Items/sw_table_metal.bmp",
                  "ColorString": "&c", "TileColor": "&c", "DetailColor": "C"},
}


PROFILE_EXCEPTIONS: Mapping[str, str] = {
    "r_KingdomArcologyCouncilDais": "council",
    "r_KingdomArcologyWardAmenity": "amenity",
    "r_KingdomBaetylLedgerFrame": "standard",
    "r_KingdomBecomingCharger": "charger",
    "r_KingdomBoughSchoolDesk": "desk",
    "r_KingdomCourtAmenity": "amenity",
    "r_KingdomCrownWitnessDais": "dais",
    "r_KingdomDeepBoreHead": "bore",
    "r_KingdomDeepCutFace": "cutface",
    "r_KingdomEntropyWitnessPlate": "gauge",
    "r_KingdomFineHouseAmenity": "amenity",
    "r_KingdomGarthOffering": "table",
    "r_KingdomGreatCourtRostrum": "ornate",
    "r_KingdomGranaryColossusLedger": "desk",
    "r_KingdomGraveGroveMemorial": "memorial",
    "r_KingdomGuestScreenAmenity": "amenity",
    "r_KingdomGyreAshStandard": "standard",
    "r_KingdomHornChallengeStandard": "standard",
    "r_KingdomHospiceRite": "table",
    "r_KingdomListeningSlab": "bench",
    "r_KingdomManorAmenity": "amenity",
    "r_KingdomMirrorGateCore": "teleporter",
    "r_KingdomMootCharterStand": "rostrum",
    "r_KingdomNaphtaaliWitnessAltar": "altar",
    "r_KingdomPreservationTable": "table",
    "r_KingdomQuietBaffle": "screen",
    "r_KingdomRefugeServingBoard": "table",
    "r_KingdomReliquaryWitnessMachine": "machine",
    "r_KingdomRifleRest": "bench",
    "r_KingdomRiteFire": "fire",
    "r_KingdomRotChapelRite": "shrine",
    "r_KingdomRotChapelVessel": "vase",
    "r_KingdomShrineOffering": "vase",
    "r_KingdomSpiceHearthWork": "table",
    "r_KingdomTempleSanctum": "lectern",
    "r_KingdomTemplarOrderedRack": "rack",
    "r_KingdomTitheBasin": "basin",
    "r_KingdomTravellerTable": "table",
    "r_KingdomTrollTollStone": "memorial",
    "r_KingdomUnderBenchWorkface": "workbench",
    "r_KingdomWardenSightlinePost": "standard",
    "r_KingdomWatchMuster": "shelf",
    "r_KingdomWetUnderfloor": "basin",
}


def render_profile(spec: ProviderFixture) -> str:
    """Choose one closed, role-readable vanilla silhouette; unknown roles fail generation."""

    if spec.blueprint in PROFILE_EXCEPTIONS:
        return PROFILE_EXCEPTIONS[spec.blueprint]
    words = (spec.blueprint + " " + spec.display + " " + " ".join(spec.anchors)).lower()
    rules = (
        (("basin", "bath", "washstand", "wetwell", "vat", "condensate", "reservoir",
          "fungal bed", "gallery bed"), "basin"),
        (("anvil",), "anvil"), (("forge",), "forge"),
        (("furnace", "smelting"), "furnace"), (("oven",), "oven"),
        (("campfire", "communal rite fire", "brazier"), "fire"),
        (("sailvane",), "windmill"),
        (("waterwheel", "spindle wheel", "spindle-wheel"), "wheel"),
        (("millstone", "crank mill", "grinding mill"), "mill"), (("loom",), "loom"),
        (("chair",), "chair"), (("bench", "listening seat", "rifle rest"), "bench"),
        (("rostrum", "dais"), "rostrum"), (("altar",), "altar"), (("shrine",), "shrine"),
        (("cairn", "memorial", "tomb", "witness stone", "plinth", "judgment stone",
          "toll-stone", "listening slab"), "memorial"),
        (("standard", "sightline post", "ledger-frame"), "standard"),
        (("weapons rack", "arms rack"), "rack"),
        (("rack", "shelf", "cabinet", "amenity", "chest"), "shelf"),
        (("gauge", "scale", "measure", "plate"), "gauge"),
        (("charger", "charging", "contact", "rail", "core", "accumulator", "machine relic"),
         "machine"),
        (("desk", "ledger"), "desk"),
        (("table", "board", "counter", "workface", "slab", "trestle", "banker"), "table"),
    )
    for needles, profile in rules:
        if any(needle in words for needle in needles):
            return profile
    raise GenerationError(f"provider fixture {spec.blueprint!r} lacks a reviewed render profile")


def object_render(spec: ProviderFixture) -> Mapping[str, str]:
    return RENDER_PROFILES[render_profile(spec)]


def description(spec: ProviderFixture) -> str:
    """Return reviewed player-facing prose verbatim; never synthesize it from metadata."""

    if not spec.description.strip():
        raise GenerationError(f"provider fixture {spec.blueprint!r} lacks authored prose")
    return spec.description


def add_part(parent: ET.Element, name: str, **attrs: str) -> ET.Element:
    values = {"Name": name}
    values.update({key: value for key, value in attrs.items() if value})
    return ET.SubElement(parent, "part", values)


def spec_objects(spec: ProviderFixture, tech: str, material: str) -> List[ET.Element]:
    semantic_name = spec.blueprint + "Semantic"
    semantic = ET.Element("object", {"Name": semantic_name,
                                      "Inherits": "r_KingdomBenefitFixtureBase"})
    render = {"DisplayName": spec.display}
    render.update(object_render(spec))
    add_part(semantic, "Render", **render)
    add_part(semantic, "Description", Short=description(spec))
    weight = "180" if material in {"workedmetal", "scrap", "stone", "shapedstone", "marble"} else "45"
    add_part(semantic, "Physics", Solid="false", Takeable="false", Weight=weight,
             Organic="false" if material in {"workedmetal", "scrap", "stone", "shapedstone", "marble"} else "true")
    ET.SubElement(semantic, "tag", {"Name": "r_KingdomProviderBuildKey", "Value": spec.building})
    ET.SubElement(semantic, "tag", {"Name": "r_KingdomProviderComponent", "Value": spec.component})
    ET.SubElement(semantic, "tag", {"Name": "r_KingdomProviderMaterial", "Value": material})
    ET.SubElement(semantic, "tag", {"Name": "r_KingdomProviderMinTech", "Value": tech})
    if material in {"workedmetal", "scrap"}:
        add_part(semantic, "Metal")
    provider_key = f"taf:{spec.building.replace(':', '-')}-{spec.component}"
    if spec.state:
        add_part(semantic, "r_KingdomStateBenefitProvider", ProviderKey=provider_key,
                 Carries=spec.carries, Provides=spec.provides, Scope=spec.scope,
                 State=spec.state)
        if spec.state in {"HeldFreshWater", "HeldFreshWaterAndStaffed", "WetOffal"}:
            add_part(semantic, "Container", Preposition="in")
            add_part(semantic, "Inventory")
        elif spec.state in {"OpenBrine", "OpenFreshWater"}:
            add_part(semantic, "LiquidVolume", MaxVolume="-1", Volume="0", StartVolume="0",
                     InitialLiquid="")
    elif spec.native_part == "UniversalCharger":
        add_part(semantic, "UniversalCharger", ChargeRate="150")
        add_part(semantic, "Capacitor", MaxCharge="4000", ChargeRate="0",
                 MinimumChargeToExplode="0")
        add_part(semantic, "Container", Preposition="on")
        add_part(semantic, "Inventory")
    else:
        add_part(semantic, "r_KingdomBenefitProvider", ProviderKey=provider_key,
                 Carries=spec.carries, Provides=spec.provides, Scope=spec.scope,
                 Operation=spec.operation)
        if spec.operation.lower() == "powered":
            add_part(semantic, "Capacitor", MaxCharge="2000", ChargeRate="0",
                     MinimumChargeToExplode="0")
    installed = ET.Element("object", {"Name": spec.blueprint, "Inherits": semantic_name})
    add_part(installed, "Physics", Takeable="false")
    result = [semantic, installed]
    if spec.portable:
        portable = ET.Element("object", {"Name": spec.blueprint + "Portable",
                                         "Inherits": semantic_name})
        add_part(portable, "Physics", Takeable="true", Solid="false",
                 Weight="40" if material not in {"workedmetal", "stone", "shapedstone", "marble"} else "90")
        add_part(portable, "Commerce", Value=str(6 + TECH_TIER[tech] * 6))
        ET.SubElement(portable, "tag", {"Name": "r_KingdomPortableProvider", "Value": "yes"})
        result.append(portable)
    return result


def object_block(spec_materials: Mapping[ProviderFixture, Tuple[str, str]]) -> str:
    nodes: List[ET.Element] = []
    base = ET.Element("object", {"Name": "r_KingdomBenefitFixtureBase", "Inherits": "Furniture"})
    add_part(base, "Physics", Solid="false", Takeable="false", Weight="50")
    ET.SubElement(base, "stat", {"Name": "Hitpoints", "Value": "80"})
    nodes.append(base)
    for spec in BUILDING_FIXTURES:
        tech, material = spec_materials[spec]
        nodes.extend(spec_objects(spec, tech, material))
    for source, portable_name, tier in NATIVE_PORTABLES:
        node = ET.Element("object", {"Name": portable_name, "Inherits": source})
        add_part(node, "Physics", Takeable="true", Solid="false", Weight="60")
        add_part(node, "Commerce", Value=str(6 + tier * 6))
        ET.SubElement(node, "tag", {"Name": "r_KingdomPortableProvider", "Value": "yes"})
        nodes.append(node)
    for spec in YARD_FIXTURES:
        node = ET.Element("object", {"Name": spec.blueprint + "Portable",
                                     "Inherits": spec.blueprint})
        add_part(node, "Physics", Takeable="true", Solid="false", Weight="35")
        add_part(node, "Commerce", Value="12")
        ET.SubElement(node, "tag", {"Name": "r_KingdomPortableProvider", "Value": "yes"})
        nodes.append(node)
    for node in nodes:
        ET.indent(node, space="  ", level=1)
    return "\n".join("  " + ET.tostring(node, encoding="unicode", short_empty_elements=True).rstrip()
                      for node in nodes)


def population_block(spec_materials: Mapping[ProviderFixture, Tuple[str, str]]) -> str:
    by_tier: MutableMapping[int, List[str]] = defaultdict(list)
    for spec in BUILDING_FIXTURES:
        if spec.portable:
            by_tier[TECH_TIER[spec_materials[spec][0]]].append(spec.blueprint + "Portable")
    for _source, portable, tier in NATIVE_PORTABLES:
        by_tier[tier].append(portable)
    for spec in YARD_FIXTURES:
        by_tier[1].append(spec.blueprint + "Portable")
    lines = [
        "  <!-- Ordinary provider furniture is independently rare merchant stock. Dropping one",
        "       inside an exact designation is the placement route; catalogue identity grants zero. -->",
    ]
    for tier in sorted(by_tier):
        lines.append(f'  <population Name="Tier{tier}Wares" Load="Merge">')
        for blueprint in sorted(set(by_tier[tier])):
            lines.append(f'    <object Blueprint="{blueprint}" Number="1" Chance="1" />')
        lines.append("  </population>")
    return "\n".join(lines)


def write(repo: Path) -> Tuple[int, int]:
    roots = list(architecture_roots(repo))
    rows = building_rows(repo)
    maps: Dict[str, ET.Element] = {}
    palettes: Dict[str, ET.Element] = {}
    owners: Dict[int, Path] = {}
    uses: MutableMapping[str, List[Tuple[str, str]]] = defaultdict(list)
    for path, root in roots:
        for architecture_map in root.findall("map"):
            key = architecture_map.get("Key", "")
            if key in maps:
                raise GenerationError(f"duplicate source map {key!r}")
            maps[key] = architecture_map
            owners[id(architecture_map)] = path
        for palette in root.findall("palette"):
            key = palette.get("Key", "")
            if key in palettes:
                raise GenerationError(f"duplicate source palette {key!r}")
            palettes[key] = palette
            owners[id(palette)] = path
        for building, map_key, palette_key in map_pairs(root):
            uses[building].append((map_key, palette_key))

    changed_maps: MutableMapping[Path, List[ET.Element]] = defaultdict(list)
    pending_slots: MutableMapping[Path, List[Tuple[str, str, str]]] = defaultdict(list)
    used: MutableMapping[str, set[str]] = defaultdict(set)
    spec_materials: Dict[ProviderFixture, Tuple[str, str]] = {}
    for spec in BUILDING_FIXTURES:
        row = rows.get(spec.building)
        if row is None:
            raise GenerationError(f"provider fixture names unknown building {spec.building!r}")
        tech, material = tech_and_material(row)
        spec_materials[spec] = (tech, spec.material or material)
        pairs = sorted(set(uses.get(spec.building, ())))
        if not pairs:
            raise GenerationError(f"provider building {spec.building!r} has no authored map")
        for map_key, palette_key in pairs:
            architecture_map = maps.get(map_key)
            palette = palettes.get(palette_key)
            if architecture_map is None or palette is None:
                raise GenerationError(
                    f"provider building {spec.building!r} names missing map/palette "
                    f"{map_key!r}/{palette_key!r}"
                )
            slot_key = f"benefit-{spec.building}-{spec.component}".replace(":", "-")
            tech, material = spec_materials[spec]
            pending_slots[owners[id(palette)]].append(
                (palette_key, slot_key, slot_line(slot_key, spec, material, tech)))
            if install_on_map(spec, architecture_map, slot_key, used[map_key]):
                if architecture_map not in changed_maps[owners[id(architecture_map)]]:
                    changed_maps[owners[id(architecture_map)]].append(architecture_map)

    changed_files = set(changed_maps) | set(pending_slots)
    for path in sorted(changed_files):
        text = path.read_text(encoding="utf-8-sig")
        for palette_key, slot_key, line in pending_slots[path]:
            text = upsert_palette_slot(text, palette_key, slot_key, line)
        for architecture_map in changed_maps[path]:
            text = replace_map(text, architecture_map)
        ET.fromstring(text)
        path.write_text(text, encoding="utf-8")

    objects_path = repo / "RuntimeData" / "ObjectBlueprints.xml"
    objects = objects_path.read_text(encoding="utf-8-sig")
    objects = replace_block(objects, OBJECT_BEGIN, OBJECT_END,
                            object_block(spec_materials), "</objects>")
    ET.fromstring(objects)
    objects_path.write_text(objects, encoding="utf-8")

    populations_path = repo / "RuntimeData" / "PopulationTables.xml"
    populations = populations_path.read_text(encoding="utf-8-sig")
    populations = replace_block(populations, POP_BEGIN, POP_END,
                                population_block(spec_materials), "</populations>")
    ET.fromstring(populations)
    populations_path.write_text(populations, encoding="utf-8")
    return sum(len(value) for value in changed_maps.values()), len(BUILDING_FIXTURES)


def check(repo: Path) -> None:
    rows = building_rows(repo)
    spec_materials: Dict[ProviderFixture, Tuple[str, str]] = {}
    for spec in BUILDING_FIXTURES:
        row = rows.get(spec.building)
        if row is None:
            raise GenerationError(f"provider fixture names unknown building {spec.building!r}")
        tech, material = tech_and_material(row)
        spec_materials[spec] = (tech, spec.material or material)

    object_text = (repo / "RuntimeData" / "ObjectBlueprints.xml").read_text(
        encoding="utf-8-sig")
    expected_objects = (OBJECT_BEGIN + "\n" + object_block(spec_materials).rstrip()
                        + "\n" + OBJECT_END)
    if expected_objects not in object_text:
        raise GenerationError("generated provider object block is stale or nondeterministic")

    population_text = (repo / "RuntimeData" / "PopulationTables.xml").read_text(
        encoding="utf-8-sig")
    expected_population = (POP_BEGIN + "\n" + population_block(spec_materials).rstrip()
                           + "\n" + POP_END)
    if expected_population not in population_text:
        raise GenerationError("generated provider acquisition block is stale or nondeterministic")

    objects = parsed(repo / "RuntimeData" / "ObjectBlueprints.xml")
    names = {obj.get("Name", "") for obj in objects.findall("object")}
    missing = [spec.blueprint for spec in BUILDING_FIXTURES if spec.blueprint not in names]
    if missing:
        raise GenerationError("missing generated provider blueprints: " + ", ".join(missing))
    populations = parsed(repo / "RuntimeData" / "PopulationTables.xml")
    stocked = {obj.get("Blueprint", "") for table in populations.findall("population")
               for obj in table.findall("object")}
    missing_routes = [spec.blueprint + "Portable" for spec in BUILDING_FIXTURES
                      if spec.portable and spec.blueprint + "Portable" not in stocked]
    missing_routes.extend(spec.blueprint + "Portable" for spec in YARD_FIXTURES
                          if spec.blueprint + "Portable" not in stocked)
    if missing_routes:
        raise GenerationError("missing provider acquisition routes: " + ", ".join(missing_routes))
    map_objects: MutableMapping[str, List[str]] = defaultdict(list)
    for _path, root in architecture_roots(repo):
        palette_slots = {palette.get("Key", ""): {slot.get("Key", ""): slot.get("Blueprint", "")
                                                   for slot in palette.findall("slot")}
                         for palette in root.findall("palette")}
        maps = {architecture_map.get("Key", ""): architecture_map
                for architecture_map in root.findall("map")}
        for building, map_key, palette_key in map_pairs(root):
            architecture_map = maps.get(map_key)
            slots = palette_slots.get(palette_key, {})
            if architecture_map is None:
                continue
            counts = Counter("".join(row.get("Cells", "") for row in architecture_map.findall("row")))
            for glyph in architecture_map.findall("glyph"):
                ref = glyph.get("Object", "")
                if ref.startswith("$") and ref[1:] in slots:
                    map_objects[building].extend([slots[ref[1:]]] * counts[glyph.get("Char", "")])
    failures = []
    for spec in BUILDING_FIXTURES:
        count = map_objects[spec.building].count(spec.blueprint)
        variants = len(set())
        if count <= 0:
            failures.append(f"{spec.building}/{spec.component}=0")
    if failures:
        raise GenerationError("provider maps missing fixtures: " + ", ".join(failures))


def main(argv: Optional[Sequence[str]] = None) -> int:
    args_parser = argparse.ArgumentParser()
    mode = args_parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--write", action="store_true")
    mode.add_argument("--check", action="store_true")
    args_parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    args = args_parser.parse_args(argv)
    repo = args.repo_root.resolve()
    try:
        if args.write:
            changed, fixtures = write(repo)
            print(f"benefit providers materialised: {fixtures} fixtures; {changed} source maps changed")
        else:
            check(repo)
            print("benefit provider generated content clean")
        return 0
    except (GenerationError, ET.ParseError, OSError, ValueError) as error:
        print(f"benefit provider generation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
