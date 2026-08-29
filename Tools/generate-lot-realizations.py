#!/usr/bin/env python3
"""Materialise every larger exact lot binding as concrete, inspectable XML.

Authored minimum-size maps remain building authority. This build-time tool keeps that coordinate
block byte-for-byte, then realizes added lot bands as deterministic, palette-lawful courts,
service ground, crop ground, and sparse boundaries. ``.`` survives only for a proved frontage
route or an explicit open-ground reason. It never runs in Qud; runtime never synthesises these.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import sys
import xml.etree.ElementTree as ET
from collections import deque
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Set, Tuple


LOT_DIMENSIONS: Dict[str, Tuple[int, int]] = {
    "S": (5, 4),
    "M": (8, 6),
    "L": (12, 9),
    "XL": (20, 14),
}
LOT_ORDER = tuple(LOT_DIMENSIONS)
HEART_BUILD_KEYS = {
    "heartbasin", "heartwaterstone", "heartmoot", "heartcourt", "arcology"
}
HOSTED_ARCOLOGY_BUILD_KEYS = {"arcologyward", "arcologyterrace"}
OUTPUT_NAME = "KingdomArchitectures-LotRealizations.xml"
IDENTITY_SELECTOR_ATTRIBUTES = ("Cultures", "Species", "Genotypes", "Bodies")
WALK_CLAIMS = {"building", "yard"}

# Empty by design. A future exception must name a whole topology policy and explain why its
# generated lots have no visible worked path. Individual palettes/buildings cannot silently opt out.
NO_PATH_POLICIES: Dict[str, str] = {}

# Upgrade families may repaint shared exterior cells only when catalogue costs name and pay an
# exterior overlay. No ordinary family currently has one, so generated successor masks stay fixed.
PAID_EXTERIOR_OVERLAY_FAMILIES: Dict[str, str] = {}

# Installed Qud 2.0.211.51 declares DirtPath : DirtFloor with only DisplayName changed. These
# names therefore share one native tile/color treatment and cannot satisfy a visible-path gate.
VISUAL_BLUEPRINT_EQUIVALENTS: Dict[str, str] = {
    "DirtFloor": "vanilla-random-dirt",
    "DirtPath": "vanilla-random-dirt",
    "DirtRoad": "vanilla-random-dirt",
}

# Creed sites begin as compact practice cores. Larger exact lots keep that authored core but grow
# visible work around their category path: two fixtures at M, five at L, ten at XL. Generated
# silhouettes resolve through this closed mapping to direct-Furniture inert wrappers. Never copy a
# functional S-core object: that would duplicate beds, fire, liquid, storage, or other utility
# without a contract anchor.
CREED_EXPANSION_FIXTURE_COUNTS: Dict[str, int] = {"M": 2, "L": 5, "XL": 10}
CREED_EXPANSION_INERT_OBJECTS: Dict[str, str] = {
    "$basket": "$practicebasket",
    "$table": "$practicetable",
    "$hearth": "$practicehearth",
    "$shelf": "$practiceshelf",
    "$stone": "$practicestone",
    "$basin": "$practicebasin",
    "$bench": "$practicebench",
    "$bed": "$practicepallet",
    "$spindle": "$spindle",
    "$contact": "$contact",
    "$hornpost": "$hornpost",
    "$altar": "$altar",
    "$armsrack": "$armsrack",
    "$brazier": "$brazier",
    "$trellis": "$trellis",
    "$trunk": "$trunk",
}


@dataclass(frozen=True)
class RealizationContext:
    plan_key: str
    binding_key: str
    build_key: str
    upgrade_family: str
    category: str
    source_size: str
    target_size: str
    facing: str
    palette_key: str
    open_design: bool
    reserved_route_cells: frozenset[Tuple[int, int]]


@dataclass(frozen=True)
class YardPolicy:
    key: str
    rationale: str
    boundary_period: int


@dataclass(frozen=True)
class RealizationRecord:
    generated_key: str
    source_key: str
    context: RealizationContext
    offset_x: int
    offset_y: int
    added_cells: int
    yard_cells: int
    path_cells: int
    boundary_cells: int
    fixture_cells: int
    route_cells: int
    intentional_open_cells: int
    inaccessible_open_cells: int
    open_reason: str
    hosted_hold: bool
    overlay_fingerprint: str


@dataclass(frozen=True)
class GenerationResult:
    text: str
    records: Tuple[RealizationRecord, ...]
    plan_count: int
    tier_count: int

    @property
    def map_count(self) -> int:
        return len(self.records)


CATEGORY_POLICIES: Dict[str, YardPolicy] = {
    "housing": YardPolicy("house-court", "domestic court and approach", 5),
    "food": YardPolicy("field-bands", "cultivation rows and tending lanes", 4),
    "storage": YardPolicy("loading-apron", "loading apron and dry service ground", 5),
    "craft": YardPolicy("working-court", "working court and loading approach", 4),
    "power": YardPolicy("clearance-court", "machine clearance and inspection walk", 4),
    "civic": YardPolicy("public-court", "public court and processional approach", 5),
    "faith": YardPolicy("processional-court", "ritual court and approach", 6),
    "knowledge": YardPolicy("quiet-court", "reading court and quiet approach", 6),
    "defense": YardPolicy("drill-court", "muster court and guarded edge", 4),
    "memorial": YardPolicy("memorial-grove", "memorial walk and planting ground", 6),
}
ENCLOSED_FOOD_POLICY = YardPolicy(
    "loading-apron", "dry loading court beside enclosed food work", 5
)

# Only these declarations may create optional ``.`` cells beyond required egress/unreachable
# geometry. Catalogue Open=Yes is not enough by itself: open cover still deserves authored soil.
INTENTIONAL_OPEN_REASONS: Dict[str, str] = {
    "waterwheel": "millrace flow clearance",
    "sailvane": "unobstructed vane sweep",
    "gravegrove": "future memorial planting",
    "reservoir": "unlined catchment margin",
}


class GenerationError(RuntimeError):
    pass


def _policy_for(context: RealizationContext) -> Optional[YardPolicy]:
    if context.category == "food" and not context.open_design:
        return ENCLOSED_FOOD_POLICY
    return CATEGORY_POLICIES.get(context.category)


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


def _buildings(repository: Path) -> Dict[str, Dict[str, str]]:
    result: Dict[str, Dict[str, str]] = {}
    paths = sorted(repository.rglob("KingdomBuildings.xml"))
    for path in paths:
        if ".git" in path.relative_to(repository).parts or not path.is_file():
            continue
        root = ET.parse(path).getroot()
        if root.tag != "kingdombuildings":
            raise GenerationError(f"{path} is not a kingdombuildings catalogue")
        for building in root.findall("building"):
            key = building.get("Key", "")
            if not key:
                raise GenerationError(f"{path} contains an empty building key")
            result.setdefault(key, {}).update(building.attrib)
    return result


def _upgrade_families(buildings: Dict[str, Dict[str, str]]) -> Dict[str, str]:
    """Return exact weak components of catalogue ``UpgradesTo`` edges.

    Family identity includes directed edge text, so topology stays stable through an upgrade while
    still changing deterministically if catalogue succession changes. Singleton identity is the
    catalogue key because its exact graph component has no edges.
    """

    adjacency: Dict[str, Set[str]] = {key: set() for key in buildings}
    directed: Set[Tuple[str, str]] = set()
    for source, building in buildings.items():
        targets = [
            target.strip()
            for target in building.get("UpgradesTo", "").split(",")
            if target.strip()
        ]
        for target in targets:
            if target not in buildings:
                raise GenerationError(
                    f"building {source!r} UpgradesTo unknown catalogue key {target!r}"
                )
            if target == source:
                raise GenerationError(f"building {source!r} upgrades to itself")
            adjacency[source].add(target)
            adjacency[target].add(source)
            directed.add((source, target))

    result: Dict[str, str] = {}
    for start in sorted(buildings):
        if start in result:
            continue
        component: Set[str] = set()
        queue = deque((start,))
        while queue:
            key = queue.popleft()
            if key in component:
                continue
            component.add(key)
            queue.extend(sorted(adjacency[key] - component))
        edges = sorted(edge for edge in directed if edge[0] in component)
        family = (
            ";".join(f"{source}>{target}" for source, target in edges)
            if edges
            else start
        )
        for key in component:
            result[key] = family
    return result


def _glyphs(architecture_map: ET.Element) -> Dict[str, ET.Element]:
    return {glyph.get("Char", ""): glyph for glyph in architecture_map.findall("glyph")}


def _free_char(glyphs: Dict[str, ET.Element], preferred: str) -> str:
    for char in preferred + "ypc;:_/%!?0123456789abcdefghijklmnopqrstuvwxyz":
        if char != "." and char not in glyphs and not char.isspace():
            return char
    raise GenerationError("map has no readable free glyph character for generated yard facts")


def _slot_roles(palette: ET.Element) -> Dict[str, str]:
    return {
        "$" + slot.get("Key", ""): slot.get("Role", "").lower()
        for slot in palette.findall("slot")
        if slot.get("Key")
    }


def _walk_ground_role(role: str) -> bool:
    """Reject natural palette slots whose semantic role is retained structure, not ground."""

    structural = (
        "wall", "rock", "face", "support", "barrier", "rim", "screen", "rail",
        "casing", "prop", "shell", "structure", "lid",
    )
    return not any(token in role for token in structural)


def _visual_blueprint_key(blueprint: str) -> str:
    return VISUAL_BLUEPRINT_EQUIVALENTS.get(blueprint, blueprint)


def _ground_reference(
    source: ET.Element, palette: ET.Element, want_path: bool
) -> str:
    roles = _slot_roles(palette)
    used = {
        glyph.get("Ground", "")
        for glyph in source.findall("glyph")
        if glyph.get("Ground")
    }
    references: Dict[str, int] = {}
    for glyph in source.findall("glyph"):
        reference = glyph.get("Ground", "")
        if not reference or reference not in roles:
            continue
        role = roles[reference]
        if not _walk_ground_role(role):
            continue
        neutral_yard = (
            glyph.get("Claim") == "yard"
            and glyph.get("Pass") == "walk"
            and glyph.get("Cover") == "open"
            and not glyph.get("Structure")
            and not glyph.get("Object")
            and not glyph.get("Anchors")
        )
        path_weight = (
            120
            if any(token in role for token in ("path", "walk", "lane", "approach", "service"))
            else 60 if any(token in role for token in ("floor", "court")) else 0
        )
        earth_weight = (
            120
            if any(token in role for token in ("ground", "soil", "earth", "crop", "plant"))
            else 0
        )
        score = 20
        if neutral_yard:
            score += 20
        if glyph.get("Claim") == "yard":
            score += 10
        if want_path:
            score += path_weight
        else:
            score += earth_weight
        if want_path and earth_weight:
            score -= 20
        if not want_path and path_weight:
            score -= 30
        references[reference] = max(references.get(reference, -1000), score)
    for slot in palette.findall("slot"):
        reference = "$" + slot.get("Key", "")
        role = slot.get("Role", "").lower()
        if (
            not reference[1:]
            or not _walk_ground_role(role)
            or (reference not in used and slot.get("Natural") != "yes")
        ):
            continue
        path_weight = (
            120
            if any(token in role for token in ("path", "walk", "lane", "approach", "service"))
            else 60 if any(token in role for token in ("floor", "court")) else 0
        )
        earth_weight = (
            120
            if any(token in role for token in ("ground", "soil", "earth", "crop", "plant"))
            else 0
        )
        # Familiar ground is useful, but cannot erase the palette's visible distinction
        # between natural earth and a worked path/floor.
        score = 40 if reference in used else 0
        if slot.get("Natural") == "yes":
            score += 10
        if want_path and "lot-path" in role:
            score += 1000
        if want_path:
            score += path_weight
        else:
            score += earth_weight
        if want_path and earth_weight:
            score -= 20
        if not want_path and path_weight:
            score -= 30
        references[reference] = max(references.get(reference, -1000), score)
    if not references:
        raise GenerationError(
            f"map {source.get('Key')!r} has no used palette ground for yard realization"
        )
    return sorted(references, key=lambda item: (-references[item], item))[0]


def _neutral_open_char(
    source: ET.Element, reference: str, glyphs: Dict[str, ET.Element], preferred: str,
    force_new: bool = False,
) -> Tuple[str, Optional[ET.Element]]:
    if not force_new:
        for char, glyph in sorted(glyphs.items()):
            if (
                glyph.get("Ground") == reference
                and glyph.get("Claim") == "yard"
                and glyph.get("Pass") == "walk"
                and glyph.get("Cover") == "open"
                and not glyph.get("Structure")
                and not glyph.get("Object")
                and not glyph.get("Anchors")
            ):
                return char, None
    char = _free_char(glyphs, preferred)
    element = ET.Element(
        "glyph",
        {
            "Char": char,
            "Ground": reference,
            "Claim": "yard",
            "Pass": "walk",
            "Cover": "open",
        },
    )
    glyphs[char] = element
    return char, element


def _boundary_char(source: ET.Element) -> str:
    candidates: List[Tuple[int, str]] = []
    for glyph in source.findall("glyph"):
        if (
            glyph.get("Claim") == "yard"
            and glyph.get("Structure")
            and not glyph.get("Object")
            and not glyph.get("Anchors")
            and glyph.get("Pass") in {"blocked", "adjacent"}
            and glyph.get("Cover") in {"open", "soft"}
        ):
            score = 1 if glyph.get("Pass") == "blocked" else 0
            candidates.append((score, glyph.get("Char", "")))
    candidates.sort(key=lambda item: (-item[0], item[1]))
    return candidates[0][1] if candidates else ""


def _outward(x: int, y: int, width: int, height: int) -> bool:
    return x in {0, width - 1} or y in {0, height - 1}


def _creed_expansion_fixture_glyphs(
    source: ET.Element,
    context: RealizationContext,
    yard_reference: str,
    glyphs: Dict[str, ET.Element],
) -> Tuple[str, ...]:
    """Create open-yard glyphs through the closed inert-silhouette mapping."""

    if not context.plan_key.startswith("creed-"):
        return ()
    candidates = []
    for glyph in source.findall("glyph"):
        anchors = tuple(
            token for token in glyph.get("Anchors", "").split(",") if token
        )
        if (
            not glyph.get("Object")
            or glyph.get("Object") == "$building"
            or glyph.get("Pass") != "walk"
            or not anchors
            or any(
                token == "main"
                or token.startswith("entrance:")
                or token.startswith("exit:")
                for token in anchors
            )
        ):
            continue
        candidates.append(glyph)
    candidates.sort(
        key=lambda item: (
            item.get("Anchors", ""), item.get("Object", ""), item.get("Char", "")
        )
    )
    result = []
    for source_glyph in candidates:
        source_object = source_glyph.get("Object", "")
        inert_object = CREED_EXPANSION_INERT_OBJECTS.get(source_object)
        if not inert_object:
            raise GenerationError(
                f"creed map {source.get('Key')!r} expansion object {source_object!r} has "
                "no explicit inert Furniture wrapper"
            )
        char = _free_char(glyphs, "fvoklq")
        attributes = {
            "Char": char,
            "Ground": yard_reference,
            "Object": inert_object,
            "Claim": "yard",
            "Pass": "walk",
            "Cover": "open",
        }
        generated = ET.Element("glyph", attributes)
        glyphs[char] = generated
        result.append(char)
    return tuple(result)


def _creed_expansion_fixture_cells(
    context: RealizationContext,
    yard_cells: Set[Tuple[int, int]],
    path_cells: Set[Tuple[int, int]],
    entrances: Sequence[Tuple[int, int]],
    width: int,
    height: int,
) -> Tuple[Tuple[int, int], ...]:
    """Choose spaced work bays beside visible paths in larger creed lots."""

    wanted = CREED_EXPANSION_FIXTURE_COUNTS.get(context.target_size, 0)
    if not context.plan_key.startswith("creed-") or not wanted:
        return ()
    eligible = [
        cell
        for cell in yard_cells
        if all(abs(cell[0] - ex) + abs(cell[1] - ey) > 0 for ex, ey in entrances)
        and any(
            abs(cell[0] - px) + abs(cell[1] - py) <= 2
            for px, py in path_cells
        )
    ]
    identity = _stable_identity(context)
    eligible.sort(
        key=lambda cell: hashlib.sha256(
            f"{identity}|creed-fixture|{cell[0]}|{cell[1]}".encode("utf-8")
        ).digest()
    )
    chosen: List[Tuple[int, int]] = []
    gap = 3 if context.target_size == "XL" else 2
    for cell in eligible:
        if all(abs(cell[0] - x) + abs(cell[1] - y) >= gap for x, y in chosen):
            chosen.append(cell)
            if len(chosen) == wanted:
                return tuple(chosen)
    for cell in eligible:
        if cell not in chosen:
            chosen.append(cell)
            if len(chosen) == wanted:
                return tuple(chosen)
    raise GenerationError(
        f"generated creed map for {context.build_key!r} {context.target_size} has only "
        f"{len(chosen)} lawful path-side fixture bays; needs {wanted}"
    )


def _unclaimed_route(
    canvas: List[List[str]], entrance: Tuple[int, int]
) -> Optional[Tuple[Tuple[int, int], ...]]:
    width = len(canvas[0])
    height = len(canvas)
    if _outward(entrance[0], entrance[1], width, height):
        return ()
    parent: Dict[Tuple[int, int], Optional[Tuple[int, int]]] = {entrance: None}
    queue = deque((entrance,))
    boundary: Optional[Tuple[int, int]] = None
    while queue and boundary is None:
        x, y = queue.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            neighbor = (x + dx, y + dy)
            nx, ny = neighbor
            if (
                neighbor in parent
                or not (0 <= nx < width and 0 <= ny < height)
                or canvas[ny][nx] != "."
            ):
                continue
            parent[neighbor] = (x, y)
            queue.append(neighbor)
            if _outward(nx, ny, width, height):
                boundary = neighbor
                break
    if boundary is None:
        return None
    reverse: List[Tuple[int, int]] = []
    step: Optional[Tuple[int, int]] = boundary
    while step is not None and step != entrance:
        reverse.append(step)
        step = parent[step]
    if step != entrance:
        return None
    return tuple(reversed(reverse))


def _ordinary_source_route_cells(
    source: ET.Element, target_size: str, facing: str
) -> frozenset[Tuple[int, int]]:
    """Place one ordinary source block and return its exact pre-decoration egress cells."""

    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    target_width, target_height = LOT_DIMENSIONS[target_size]
    if facing == "heart":
        offset_x = (target_width - source_width) // 2
        offset_y = 0
    elif facing == "road":
        side = _road_side(source)
        if side == "S":
            offset_x = (target_width - source_width) // 2
            offset_y = target_height - source_height - 1
        elif side == "N":
            offset_x = (target_width - source_width) // 2
            offset_y = 1
        elif side == "E":
            offset_x = target_width - source_width - 1
            offset_y = (target_height - source_height) // 2
        else:
            offset_x = 1
            offset_y = (target_height - source_height) // 2
    else:
        raise GenerationError(f"binding has unsupported Facing={facing!r}")
    canvas = [["." for _x in range(target_width)] for _y in range(target_height)]
    rows = [row.get("Cells", "") for row in source.findall("row")]
    for y, row in enumerate(rows):
        for x, char in enumerate(row):
            canvas[offset_y + y][offset_x + x] = char
    routes: Set[Tuple[int, int]] = set()
    for x, y in _public_entrances(source):
        entrance = (offset_x + x, offset_y + y)
        route = _unclaimed_route(canvas, entrance)
        if route is None:
            raise GenerationError(
                f"map {source.get('Key')!r} has no pre-decoration exact frontage route"
            )
        routes.update(route)
    return frozenset(routes)


def _walkable(char: str, glyphs: Dict[str, ET.Element]) -> bool:
    glyph = glyphs.get(char)
    return bool(
        glyph is not None
        and glyph.get("Pass") == "walk"
        and glyph.get("Claim") in WALK_CLAIMS
    )


def _stable_identity(context: RealizationContext) -> str:
    return "|".join(
        (
            context.plan_key,
            context.binding_key,
            context.upgrade_family,
            context.category,
            context.source_size,
            context.target_size,
            context.facing,
        )
    )


def _stable_phase(context: RealizationContext) -> int:
    digest = hashlib.sha256(_stable_identity(context).encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big")


def _oriented(
    x: int, y: int, width: int, height: int, side: str
) -> Tuple[int, int, int]:
    if side == "S":
        return x, height - 1 - y, width
    if side == "E":
        return y, width - 1 - x, height
    if side == "W":
        return y, x, height
    return x, y, width


def _base_yard_kind_oriented(
    policy: YardPolicy,
    context: RealizationContext,
    cross: int,
    depth: int,
    span: int,
    depth_span: int,
) -> str:
    phase = _stable_phase(context)
    size_rank = LOT_ORDER.index(context.target_size)
    centre = max(1, min(span - 2, (span - 1) // 2 + phase % 5 - 2))
    court_depth = max(
        1,
        min(depth_span - 2, depth_span // 2 + (phase // 5) % 3 - 1),
    )
    pocket_half = min(
        max(1, size_rank + 1 + (phase // 15) % 2),
        max(1, span // 3),
    )
    if policy.key == "field-bands":
        period = 3 if context.target_size in {"L", "XL"} else 2
        return "path" if cross == centre or (depth + phase) % period == 0 else "yard"
    if policy.key == "public-court":
        court_half = min(pocket_half + 1, max(1, span // 3))
        court_bar = depth in {court_depth, min(depth_span - 1, court_depth + 3)} \
            and abs(cross - centre) <= court_half
        court_side = abs(cross - centre) == court_half \
            and court_depth <= depth <= min(depth_span - 1, court_depth + 3)
        return "path" if cross == centre or court_bar or court_side else "yard"
    if policy.key == "processional-court":
        axis_half = 1 if context.target_size == "XL" else 0
        threshold = depth in {court_depth, min(depth_span - 1, court_depth + 3)} \
            and abs(cross - centre) <= pocket_half
        return "path" if abs(cross - centre) <= axis_half or threshold else "yard"
    if policy.key == "quiet-court":
        quiet_axis = max(
            0,
            min(span - 1, centre + ((depth + phase // 5) // 3) % 3 - 1),
        )
        previous_axis = max(
            0,
            min(span - 1, centre + ((max(0, depth - 1) + phase // 5) // 3) % 3 - 1),
        )
        turn = (
            quiet_axis != previous_axis
            and min(quiet_axis, previous_axis) <= cross <= max(quiet_axis, previous_axis)
        )
        reading_bay = (
            depth in {court_depth, min(depth_span - 1, court_depth + 2)}
            and centre - pocket_half <= cross <= centre
        )
        return "path" if cross == quiet_axis or turn or reading_bay else "yard"
    if policy.key == "memorial-grove":
        grove_walk = max(
            0,
            min(span - 1, centre + ((depth + phase // 7) // 4) % 3 - 1),
        )
        previous_walk = max(
            0,
            min(span - 1, centre + ((max(0, depth - 1) + phase // 7) // 4) % 3 - 1),
        )
        turn = (
            grove_walk != previous_walk
            and min(grove_walk, previous_walk) <= cross <= max(grove_walk, previous_walk)
        )
        remembrance_bay = (
            depth == court_depth
            and abs(cross - centre) <= pocket_half
            and (cross + phase) % 2 == 0
        )
        return "path" if cross == grove_walk or turn or remembrance_bay else "yard"
    if policy.key == "house-court":
        court_half = min(pocket_half + 1, max(1, span // 3))
        threshold = depth in {court_depth, min(depth_span - 1, court_depth + 3)} \
            and abs(cross - centre) <= court_half
        court_side = abs(cross - centre) == court_half and depth >= court_depth
        return "path" if cross == centre or threshold or court_side else "yard"
    if policy.key == "drill-court":
        lane_half = 1 if size_rank >= 2 else 0
        muster_bar = depth == court_depth and abs(cross - centre) <= pocket_half + 1
        return "path" if abs(cross - centre) <= lane_half or muster_bar else "yard"
    if policy.key == "clearance-court":
        clearance_half = min(pocket_half + 1, max(1, span // 3))
        side_walk = abs(cross - centre) == clearance_half
        inspection_bar = (
            depth in {court_depth, min(depth_span - 1, court_depth + 2)}
            and abs(cross - centre) <= clearance_half
        )
        return "path" if cross == centre or side_walk or inspection_bar else "yard"
    if policy.key == "loading-apron":
        service_axis = max(0, min(span - 1, centre + (1 if phase % 2 else -1)))
        loading_pad = (
            depth in {court_depth, min(depth_span - 1, court_depth + 2)}
            and abs(cross - service_axis) <= pocket_half
        )
        return_lane = (
            abs(cross - service_axis) == 2
            and depth >= min(depth_span - 1, court_depth + 2)
        )
        return "path" if cross == service_axis or loading_pad or return_lane else "yard"
    # Craft keeps a direct work lane plus two short, alternating bench pads. Unlike crop rows,
    # neither pad crosses the lot, and its silhouette remains distinct from a loading apron.
    work_axis = centre
    pad_depth = min(depth_span - 1, court_depth + 2)
    working_pad = (
        depth in {court_depth, pad_depth}
        and abs(cross - work_axis) <= pocket_half
        and (cross - work_axis) * (1 if depth == court_depth else -1) >= 0
    )
    return_side = -1 if (phase // 31) % 2 else 1
    return_lane = (
        cross == work_axis + return_side * min(3, pocket_half + 1)
        and depth >= court_depth
    )
    return "path" if cross == work_axis or working_pad or return_lane else "yard"


@lru_cache(maxsize=None)
def _family_spurs(
    policy: YardPolicy,
    context: RealizationContext,
    span: int,
    depth_span: int,
) -> frozenset[Tuple[int, int]]:
    """Choose connected, family-stable court/path spurs from otherwise plain yard cells."""

    phase = _stable_phase(context)
    length = 2 + (phase // 97) % (3 if context.target_size in {"L", "XL"} else 2)
    candidates: List[frozenset[Tuple[int, int]]] = []
    for depth in range(1, max(2, depth_span - 1)):
        for start in range(1, max(2, span - length)):
            cells = frozenset((cross, depth) for cross in range(start, start + length))
            if all(
                _base_yard_kind_oriented(
                    policy, context, cross, depth, span, depth_span
                ) == "yard"
                for cross, depth in cells
            ) and any(
                0 <= neighbor_cross < span
                and 0 <= neighbor_depth < depth_span
                and _base_yard_kind_oriented(
                    policy,
                    context,
                    neighbor_cross,
                    neighbor_depth,
                    span,
                    depth_span,
                ) == "path"
                for cross, cell_depth in cells
                for neighbor_cross, neighbor_depth in (
                    (cross - 1, cell_depth),
                    (cross + 1, cell_depth),
                    (cross, cell_depth - 1),
                    (cross, cell_depth + 1),
                )
            ):
                candidates.append(cells)
    if not candidates:
        return frozenset()
    chosen: Set[Tuple[int, int]] = set()
    count = 2
    cursor = (phase // 389) % len(candidates)
    stride = 1 + (phase // 1543) % max(1, len(candidates) - 1)
    for _index in range(count):
        for _attempt in range(len(candidates)):
            segment = candidates[cursor]
            cursor = (cursor + stride) % len(candidates)
            if not (segment <= chosen):
                chosen.update(segment)
                break
    # Small edge bays widen existing lanes at family-specific turns. Each remains attached to
    # recognizable policy topology; combined positions provide a high-entropy family fingerprint
    # without scattering decorative dots or inventing fixtures.
    edge_bays: List[Tuple[int, int]] = []
    for depth in range(1, max(2, depth_span - 1)):
        for cross in range(1, max(2, span - 1)):
            if _base_yard_kind_oriented(
                policy, context, cross, depth, span, depth_span
            ) != "yard":
                continue
            if any(
                0 <= neighbor_cross < span
                and 0 <= neighbor_depth < depth_span
                and _base_yard_kind_oriented(
                    policy,
                    context,
                    neighbor_cross,
                    neighbor_depth,
                    span,
                    depth_span,
                ) == "path"
                for neighbor_cross, neighbor_depth in (
                    (cross - 1, depth),
                    (cross + 1, depth),
                    (cross, depth - 1),
                    (cross, depth + 1),
                )
            ):
                edge_bays.append((cross, depth))
    identity = _stable_identity(context)
    edge_bays.sort(
        key=lambda cell: hashlib.sha256(
            f"{identity}|edge-bay|{cell[0]}|{cell[1]}".encode("utf-8")
        ).digest()
    )
    chosen.update(edge_bays[: min(4, len(edge_bays))])
    return frozenset(chosen)


def _yard_kind(
    policy: YardPolicy, context: RealizationContext, side: str,
    x: int, y: int, width: int, height: int
) -> str:
    cross, depth, span = _oriented(x, y, width, height, side)
    depth_span = height if side in {"N", "S"} else width
    base = _base_yard_kind_oriented(
        policy, context, cross, depth, span, depth_span
    )
    if base == "path":
        return base
    return (
        "path"
        if (cross, depth) in _family_spurs(policy, context, span, depth_span)
        else "yard"
    )


def _overlay_fingerprint(
    policy: YardPolicy,
    context: RealizationContext,
    width: int,
    height: int,
) -> str:
    # Canonical north orientation makes family comparison independent of road-side rotation.
    mask = "".join(
        "p" if _yard_kind(policy, context, "N", x, y, width, height) == "path" else "y"
        for y in range(height)
        for x in range(width)
    )
    return hashlib.sha256(mask.encode("ascii")).hexdigest()[:16]


def _intentional_open_candidate(
    context: RealizationContext, side: str, x: int, y: int, width: int, height: int
) -> bool:
    cross, depth, span = _oriented(x, y, width, height, side)
    phase = _stable_phase(context)
    if context.build_key == "waterwheel":
        return cross == (span - 1) // 2
    if context.build_key == "sailvane":
        return _outward(x, y, width, height) and (cross + phase) % 3 != 0
    if context.build_key == "gravegrove":
        return (cross + phase) % 4 == 0 and (depth + phase // 4) % 3 == 0
    if context.build_key == "reservoir":
        return depth == (height - 1 if side in {"N", "S"} else width - 1)
    return False


def _all_generated_walk_physically_reachable(
    canvas: List[List[str]], glyphs: Dict[str, ET.Element],
    entrances: Sequence[Tuple[int, int]], generated_walk: Set[Tuple[int, int]],
    allowed_open: Set[Tuple[int, int]],
) -> bool:
    """Prove generated floor through claimed walk, the exact route, and the exterior lane.

    Intentional-open cells are deliberately absent from ``allowed_open``: named scenery may be
    traversable in Qud, but it cannot make a disconnected claimed yard pass this construction gate.
    """

    width = len(canvas[0])
    height = len(canvas)
    # A valid public route reaches the plot exterior. The reserved one-cell circulation lane then
    # reaches any walkable lot-edge yard fact without treating intervening ``.`` as authored floor.
    boundary_walk = {cell for cell in generated_walk if _outward(*cell, width, height)}
    found: Set[Tuple[int, int]] = set(entrances) | boundary_walk
    queue: deque[Tuple[int, int]] = deque(found)
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            neighbor = (x + dx, y + dy)
            nx, ny = neighbor
            if not (0 <= nx < width and 0 <= ny < height) or neighbor in found:
                continue
            char = canvas[ny][nx]
            if char == "." and neighbor not in allowed_open:
                continue
            if char != "." and not _walkable(char, glyphs):
                continue
            found.add(neighbor)
            queue.append(neighbor)
    return generated_walk <= found


def _expanded_map(
    source: ET.Element, target_size: str, facing: str, generated_key: str,
    context: Optional[RealizationContext] = None, palette: Optional[ET.Element] = None,
    records: Optional[List[RealizationRecord]] = None,
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
    # ground lies behind it. Road lots retain one unclaimed frontage cell between the source door
    # side and lot edge. New authored-road routing freezes that exact, cardinal apron instead of
    # pretending an interior door itself touched the road.
    if facing == "heart":
        offset_x = (target_width - source_width) // 2
        offset_y = 0
    elif facing == "road":
        side = _road_side(source)
        frontage_margin = (
            0
            if context is not None
            and context.build_key in HOSTED_ARCOLOGY_BUILD_KEYS
            else 1
        )
        if side == "S":
            offset_x = (target_width - source_width) // 2
            offset_y = target_height - source_height - frontage_margin
        elif side == "N":
            offset_x = (target_width - source_width) // 2
            offset_y = frontage_margin
        elif side == "E":
            offset_x = target_width - source_width - frontage_margin
            offset_y = (target_height - source_height) // 2
        else:
            offset_x = frontage_margin
            offset_y = (target_height - source_height) // 2
    else:
        raise GenerationError(f"binding has unsupported Facing={facing!r}")

    canvas = [["." for _x in range(target_width)] for _y in range(target_height)]
    for y, row in enumerate(rows):
        for x, char in enumerate(row):
            canvas[offset_y + y][offset_x + x] = char

    source_glyphs = _glyphs(source)
    entrances = [
        (offset_x + x, offset_y + y) for x, y in _public_entrances(source)
    ]
    added = {
        (x, y)
        for y in range(target_height)
        for x in range(target_width)
        if not (
            offset_x <= x < offset_x + source_width
            and offset_y <= y < offset_y + source_height
        )
    }
    route_cells: Set[Tuple[int, int]] = set()
    for entrance in entrances:
        route = _unclaimed_route(canvas, entrance)
        if route is None:
            raise GenerationError(
                f"generated map {generated_key!r} has no pre-decoration frontage route "
                f"from {entrance[0]},{entrance[1]}"
            )
        route_cells.update(route)
    if (
        context is not None
        and context.build_key not in HOSTED_ARCOLOGY_BUILD_KEYS
    ):
        route_cells.update(context.reserved_route_cells & added)

    generated_glyphs: List[ET.Element] = []
    yard_cells: Set[Tuple[int, int]] = set()
    path_cells: Set[Tuple[int, int]] = set()
    boundary_cells: Set[Tuple[int, int]] = set()
    fixture_cells: Set[Tuple[int, int]] = set()
    intentional_open: Set[Tuple[int, int]] = set()
    inaccessible_open: Set[Tuple[int, int]] = set()
    open_reason = ""
    hosted_hold = bool(
        context is not None and context.build_key in HOSTED_ARCOLOGY_BUILD_KEYS
    )

    if context is not None and palette is not None and not hosted_hold:
        policy = _policy_for(context)
        if policy is None:
            raise GenerationError(
                f"building {context.build_key!r} has no yard policy for {context.category!r}"
            )
        yard_reference = _ground_reference(source, palette, want_path=False)
        path_reference = _ground_reference(source, palette, want_path=True)
        slots = {
            "$" + slot.get("Key", ""): slot
            for slot in palette.findall("slot")
            if slot.get("Key")
        }
        yard_blueprint = slots[yard_reference].get("Blueprint", "")
        path_blueprint = slots[path_reference].get("Blueprint", "")
        if (
            _visual_blueprint_key(yard_blueprint)
            == _visual_blueprint_key(path_blueprint)
            and policy.key not in NO_PATH_POLICIES
        ):
            raise GenerationError(
                f"generated map {generated_key!r} policy {policy.key!r} resolves yard and "
                f"path to the same native visual treatment "
                f"{_visual_blueprint_key(yard_blueprint)!r} through concrete blueprints "
                f"{yard_blueprint!r}/{path_blueprint!r} and palette "
                f"{context.palette_key!r}; add a lawful visible path slot or declare a named "
                "no-path policy"
            )
        yard_char, yard_glyph = _neutral_open_char(
            source, yard_reference, source_glyphs, "y"
        )
        path_char, path_glyph = _neutral_open_char(
            source,
            path_reference,
            source_glyphs,
            "p",
            force_new=path_reference == yard_reference and yard_char in source_glyphs,
        )
        if yard_glyph is not None:
            generated_glyphs.append(yard_glyph)
        if path_glyph is not None:
            generated_glyphs.append(path_glyph)
        fixture_chars = _creed_expansion_fixture_glyphs(
            source, context, yard_reference, source_glyphs
        )
        generated_glyphs.extend(source_glyphs[char] for char in fixture_chars)
        if context.plan_key.startswith("creed-") and not fixture_chars:
            raise GenerationError(
                f"creed map {source.get('Key')!r} has no lawful semantic expansion fixture"
            )
        boundary_char = _boundary_char(source)
        side = "N" if facing == "heart" else _road_side(source)

        prospective = added - route_cells
        for x, y in prospective:
            kind = _yard_kind(
                policy, context, side, x, y, target_width, target_height
            )
            if kind == "path":
                canvas[y][x] = path_char
                path_cells.add((x, y))
            else:
                canvas[y][x] = yard_char
                yard_cells.add((x, y))

        open_reason = INTENTIONAL_OPEN_REASONS.get(context.build_key, "")
        if open_reason:
            generated_walk = yard_cells | path_cells
            candidates = sorted(
                (
                    cell
                    for cell in generated_walk
                    if cell in added
                    and _intentional_open_candidate(
                        context,
                        side,
                        cell[0],
                        cell[1],
                        target_width,
                        target_height,
                    )
                    and all(
                        abs(cell[0] - ex) + abs(cell[1] - ey) > 1
                        for ex, ey in entrances
                    )
                ),
                key=lambda cell: (cell[1], cell[0]),
            )
            for cell in candidates:
                x, y = cell
                previous = canvas[y][x]
                canvas[y][x] = "."
                remaining = generated_walk - {cell}
                if _all_generated_walk_physically_reachable(
                    canvas, source_glyphs, entrances, remaining, route_cells
                ):
                    generated_walk.remove(cell)
                    yard_cells.discard(cell)
                    path_cells.discard(cell)
                    intentional_open.add(cell)
                else:
                    canvas[y][x] = previous

        family_changes = ">" in context.upgrade_family
        allow_generated_boundary = (
            not family_changes
            or context.upgrade_family in PAID_EXTERIOR_OVERLAY_FAMILIES
        )
        if boundary_char and allow_generated_boundary:
            generated_walk = yard_cells | path_cells
            phase = _stable_phase(context)
            candidates = sorted(
                (
                    cell
                    for cell in yard_cells
                    if _outward(cell[0], cell[1], target_width, target_height)
                    and (cell[0] + cell[1] + phase) % policy.boundary_period == 0
                ),
                key=lambda cell: (cell[1], cell[0]),
            )
            for cell in candidates:
                x, y = cell
                canvas[y][x] = boundary_char
                remaining = generated_walk - {cell}
                if _all_generated_walk_physically_reachable(
                    canvas, source_glyphs, entrances, remaining, route_cells
                ):
                    generated_walk.remove(cell)
                    yard_cells.remove(cell)
                    boundary_cells.add(cell)
                else:
                    canvas[y][x] = yard_char

        if fixture_chars:
            chosen_fixtures = _creed_expansion_fixture_cells(
                context,
                yard_cells,
                path_cells,
                entrances,
                target_width,
                target_height,
            )
            for index, (x, y) in enumerate(chosen_fixtures):
                canvas[y][x] = fixture_chars[index % len(fixture_chars)]
                fixture_cells.add((x, y))

        generated_walk = yard_cells | path_cells
        if policy.key not in NO_PATH_POLICIES:
            if not yard_cells or not path_cells:
                raise GenerationError(
                    f"generated map {generated_key!r} policy {policy.key!r} does not retain "
                    "both visible yard and path topology"
                )
            if (
                context.target_size in {"L", "XL"}
                and min(len(yard_cells), len(path_cells)) * 14
                < len(yard_cells) + len(path_cells)
            ):
                raise GenerationError(
                    f"generated map {generated_key!r} is a near-monoculture fill: "
                    f"yard={len(yard_cells)} path={len(path_cells)}"
                )
        if not (generated_walk & added):
            raise GenerationError(
                f"generated map {generated_key!r} cannot realize any accessible added yard"
            )
        if not _all_generated_walk_physically_reachable(
            canvas, source_glyphs, entrances, generated_walk, route_cells
        ):
            raise GenerationError(
                f"generated map {generated_key!r} contains a physically inaccessible yard fact"
            )
        for entrance in entrances:
            if _unclaimed_route(canvas, entrance) is None:
                raise GenerationError(
                    f"generated map {generated_key!r} decoration blocks frontage route "
                    f"from {entrance[0]},{entrance[1]}"
                )
    elif hosted_hold:
        open_reason = "hosted arcology floor is held for separate authored redesign"
        intentional_open = added - route_cells

    if context is not None and records is not None:
        partition = (
            (yard_cells & added)
            | (path_cells & added)
            | (boundary_cells & added)
            | (route_cells & added)
            | intentional_open
            | inaccessible_open
        )
        if partition != added:
            missing = sorted(added - partition)
            raise GenerationError(
                f"generated map {generated_key!r} has unclassified added cells: {missing[:4]}"
            )
        records.append(
            RealizationRecord(
                generated_key,
                source.get("Key", ""),
                context,
                offset_x,
                offset_y,
                len(added),
                len(yard_cells & added),
                len(path_cells & added),
                len(boundary_cells & added),
                len(fixture_cells & added),
                len(route_cells & added),
                len(intentional_open),
                len(inaccessible_open),
                open_reason,
                hosted_hold,
                (
                    "hosted-hold"
                    if hosted_hold or context is None or palette is None
                    else _overlay_fingerprint(
                        _policy_for(context),
                        context,
                        target_width,
                        target_height,
                    )
                ),
            )
        )

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
    for glyph in generated_glyphs:
        result.append(copy.deepcopy(glyph))
    for row in canvas:
        ET.SubElement(result, "row", {"Cells": "".join(row)})
    return result


def materialize(repository: Path) -> GenerationResult:
    roots = list(_source_roots(repository))
    if not roots:
        raise GenerationError("no authored KingdomArchitectures XML sources found")

    maps: Dict[str, ET.Element] = {}
    palettes: Dict[str, ET.Element] = {}
    buildings = _buildings(repository)
    upgrade_families = _upgrade_families(buildings)
    occupied_plan_keys = set()
    for path, root in roots:
        if root.tag != "KingdomArchitectures" or root.get("Schema") != "1":
            raise GenerationError(f"{path} is not KingdomArchitectures schema 1")
        for architecture_map in root.findall("map"):
            key = architecture_map.get("Key", "")
            if not key or key in maps:
                raise GenerationError(f"duplicate or empty source map key {key!r}")
            maps[key] = architecture_map
        for palette in root.findall("palette"):
            key = palette.get("Key", "")
            if not key or key in palettes:
                raise GenerationError(f"duplicate or empty source palette key {key!r}")
            palettes[key] = palette
        for plan in root.findall("plan"):
            key = plan.get("Key", "")
            if not key or key in occupied_plan_keys:
                raise GenerationError(f"duplicate or empty source plan key {key!r}")
            occupied_plan_keys.add(key)

    output = ET.Element("KingdomArchitectures", {"Schema": "1"})
    generated_maps: Dict[Tuple[str, str, str], str] = {}
    occupied_map_keys = set(maps)
    plans: List[ET.Element] = []
    records: List[RealizationRecord] = []
    contexts: Dict[Tuple[str, str, str], RealizationContext] = {}
    tier_count = 0

    def map_for(
        source_key: str, target_size: str, facing: str, context: RealizationContext
    ) -> str:
        identity = (source_key, target_size, facing)
        existing = generated_maps.get(identity)
        if existing is not None:
            previous = contexts[identity]
            if (
                previous.build_key != context.build_key
                or previous.category != context.category
                or previous.palette_key != context.palette_key
            ):
                raise GenerationError(
                    f"source map {source_key!r} is shared by incompatible yard contexts"
                )
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
        contexts[identity] = context
        palette = palettes.get(context.palette_key)
        if palette is None:
            raise GenerationError(f"unknown source palette {context.palette_key!r}")
        architecture_map = _expanded_map(
            source, target_size, facing, key, context, palette, records
        )
        record = records[-1]
        policy = _policy_for(context) or YardPolicy("held", "separate hosted redesign", 1)
        comment = (
            f" realization source={source_key}; design={context.build_key}; "
            f"policy={policy.key}; yard-reason={policy.rationale}; "
            f"yard={record.yard_cells}; path={record.path_cells}; "
            f"boundary={record.boundary_cells}; "
            f"fixtures={record.fixture_cells}; "
            f"route-open={record.route_cells}; "
            f"intentional-open={record.intentional_open_cells}; "
            f"inaccessible-open={record.inaccessible_open_cells}; "
            f"family-fingerprint={record.overlay_fingerprint}; "
            f"reason={record.open_reason or 'none'} "
        )
        output.append(ET.Comment(comment))
        output.append(architecture_map)
        return key

    def context_for(
        source_plan_key: str,
        source_binding: ET.Element,
        source_tier: ET.Element,
        target_size: str,
        palette_key: str,
        reserved_route_cells: frozenset[Tuple[int, int]],
    ) -> RealizationContext:
        build_key = source_tier.get("BuildKey", "")
        building = buildings.get(build_key)
        if building is None:
            raise GenerationError(f"unknown catalogue BuildKey {build_key!r}")
        category = source_binding.get("Type", "")
        if building.get("Category", "") != category:
            raise GenerationError(
                f"building {build_key!r} category differs from binding {category!r}"
            )
        return RealizationContext(
            source_plan_key,
            source_binding.get("Key", ""),
            build_key,
            upgrade_families[build_key],
            category,
            source_binding.get("Size", ""),
            target_size,
            source_binding.get("Facing", ""),
            palette_key,
            building.get("Open", "").strip().lower() == "yes",
            reserved_route_cells,
        )

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
                    family_routes: Dict[str, Set[Tuple[int, int]]] = {}
                    for route_tier in tiers:
                        route_build_key = route_tier.get("BuildKey", "")
                        if route_build_key in HOSTED_ARCOLOGY_BUILD_KEYS:
                            continue
                        family = upgrade_families[route_build_key]
                        route_maps = [route_tier.get("Map", "")]
                        route_maps.extend(
                            variant.get("Map", "")
                            for variant in route_tier.findall("variant")
                            if variant.get("Map")
                        )
                        union = family_routes.setdefault(family, set())
                        for route_map_key in route_maps:
                            route_map = maps.get(route_map_key)
                            if route_map is None:
                                raise GenerationError(
                                    f"unknown source map {route_map_key!r}"
                                )
                            union.update(
                                _ordinary_source_route_cells(
                                    route_map, target_size, facing
                                )
                            )
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
                        tier_palette = tier.get("Palette", "")
                        tier_context = context_for(
                            source_plan_key,
                            source_binding,
                            source_tier,
                            target_size,
                            tier_palette,
                            frozenset(
                                family_routes.get(
                                    upgrade_families[source_tier.get("BuildKey", "")],
                                    set(),
                                )
                            ),
                        )
                        tier.set(
                            "Map",
                            map_for(source_map_key, target_size, facing, tier_context),
                        )
                        for variant in tier.findall("variant"):
                            variant_map_key = variant.get("Map")
                            if variant_map_key:
                                variant_palette = variant.get("Palette") or tier_palette
                                variant_context = context_for(
                                    source_plan_key,
                                    source_binding,
                                    source_tier,
                                    target_size,
                                    variant_palette,
                                    frozenset(
                                        family_routes.get(
                                            upgrade_families[
                                                source_tier.get("BuildKey", "")
                                            ],
                                            set(),
                                        )
                                    ),
                                )
                                variant.set(
                                    "Map",
                                    map_for(
                                        variant_map_key,
                                        target_size,
                                        facing,
                                        variant_context,
                                    ),
                                )
                        binding.append(tier)
                        tier_count += 1
                    plans.append(plan)

    fingerprints: Dict[Tuple[str, str], RealizationRecord] = {}
    for record in records:
        if record.hosted_hold:
            continue
        key = (record.context.target_size, record.overlay_fingerprint)
        previous = fingerprints.get(key)
        if (
            previous is not None
            and previous.context.upgrade_family != record.context.upgrade_family
        ):
            raise GenerationError(
                f"unrelated generated families share overlay fingerprint "
                f"{record.overlay_fingerprint}: {previous.context.build_key!r} and "
                f"{record.context.build_key!r}"
            )
        fingerprints[key] = record

    for plan in plans:
        output.append(plan)
    ET.indent(output, space="  ")
    body = ET.tostring(output, encoding="unicode", short_empty_elements=True)
    header = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<!-- GENERATED by Tools/generate-lot-realizations.py. Do not hand-edit.\n"
        "     Exact maps preserve source buildings and realize added yards from lawful palette facts.\n"
        "     Each map comment accounts for open route/clearance cells and its explicit reason.\n"
        "     Regenerate by running the generator in write mode. -->\n"
    )
    return GenerationResult(header + body + "\n", tuple(records), len(plans), tier_count)


def generate(repository: Path) -> Tuple[str, int, int, int]:
    result = materialize(repository)
    return result.text, result.map_count, result.plan_count, result.tier_count


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
        result = materialize(repository)
    except (ET.ParseError, OSError, ValueError, GenerationError) as error:
        print(f"lot realization generation failed: {error}", file=sys.stderr)
        return 1
    text = result.text
    map_count = result.map_count
    plan_count = result.plan_count
    tier_count = result.tier_count
    decorated = sum(
        record.yard_cells + record.path_cells + record.boundary_cells
        for record in result.records
    )
    held = sum(1 for record in result.records if record.hosted_hold)
    fixtures = sum(record.fixture_cells for record in result.records)
    census = (
        f"{map_count} maps, {plan_count} bindings, {tier_count} tiers, "
        f"{decorated} added yard facts, {fixtures} creed fixtures, {held} hosted holds"
    )
    if arguments.write:
        target.write_text(text, encoding="utf-8")
        print(f"wrote {target}: {census}")
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
    print(f"lot realization XML current: {census}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
