#!/usr/bin/env python3
"""Materialise only larger lots with an explicit, measurable spatial programme.

Authored minimum-size maps provide semantic authority: entrances, fixtures, custody anchors,
materials, cover, and structural relationships.  This build-time tool projects that authority
through a deterministic grammar into a coherent building and useful site.  Stateless fabric may
be renovated; objects and anchors are never cloned.  Larger bindings without an authored semantic
programme are omitted instead of stretching a minimum plan across generic padding.  ``.`` survives
only for the runtime's exact unclaimed frontage route or an explicit open-ground reason.  It never
runs in Qud; runtime never synthesises these layouts.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import heapq
import re
import sys
import xml.etree.ElementTree as ET
from collections import deque
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Set, Tuple


LOT_DIMENSIONS: Dict[str, Tuple[int, int]] = {
    "S": (6, 4),
    "M": (8, 6),
    "L": (12, 10),
    "XL": (20, 18),
}
LOT_ORDER = tuple(LOT_DIMENSIONS)
MAX_ROUTE_CELLS = 48
ROAD_MARGIN = 1
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

# Creed sites begin as compact paid sanctums. Larger exact lots keep that authored fabric and grow
# named low-material campuses around it: paired courts, work bands, gardens, muster lanes, service
# pads, and circulation with two fixtures at M, five at L, and ten at XL. Generated silhouettes
# resolve through this closed mapping to palette-declared inert wrappers. Never stretch paid walls
# or copy a functional S-core object: either would mint fabric, beds, fire, liquid, storage, or
# utility outside the BuildKey bill and anchor contract.
CREED_EXPANSION_FIXTURE_COUNTS: Dict[str, int] = {"M": 2, "L": 5, "XL": 10}
MAX_NEW_SITE_CELLS_PER_SEMANTIC_FIXTURE = 36
CREED_EXPANSION_INERT_OBJECTS: Dict[str, str] = {
    "$seedbin": "$practiceseed",
    "$spicejar": "$practicespice",
    "$meatcache": "$practicemeat",
    "$labelledbin": "$practicelabel",
    "$table": "$practicetable",
    "$hearth": "$practicehearth",
    "$light": "$practicelight",
    "$locker": "$practiceshelf",
    "$shelf": "$practiceshelf",
    "$stone": "$practicestone",
    "$basin": "$practicebasin",
    "$bench": "$practicebench",
    "$bed": "$practicepallet",
    "$spindle": "$spindle",
    "$contact": "$contact",
    # Source ring posts are anchored ritual boundaries. Larger campuses extend their witness
    # line with inert canvas pennons instead of forging more unanchored challenge rings.
    "$hornpost": "$goatpennon",
    "$altar": "$altar",
    "$armsrack": "$armsrack",
    "$orderedarmsrack": "$armsrack",
    "$brazier": "$brazier",
    "$trellis": "$trellis",
    "$trunk": "$trunk",
}

# Provider silhouettes added outside the paid S core remain inert. Most use a timber practice
# board; these exact creed bills omit timber, so use an already-declared paid stone/scrap prop.
CREED_BENEFIT_INERT_OVERRIDES: Dict[str, str] = {
    "cragmenschstonegarden": "$practicestone",
    "girshrotchapel": "$practicebasin",
    "gyrewightashcourt": "$practicestone",
    "robotchargebay": "$practicebasin",
    "robotservicebay": "$practicelight",
}
CREED_BENEFIT_INERT_BY_ANCHOR: Dict[str, str] = {
    "work:spindle-wheel": "$spindle",
    "shrine:scrap-altar": "$altar",
}


@dataclass(frozen=True)
class CreedCampusProgramme:
    """Reviewed site grammar for one creed, not a hash-selected decorative variation."""

    key: str
    fixture_pattern: str
    axis_bias: int
    court_bias: int
    bay_growth: int
    handedness: int
    rhythm: int


@dataclass(frozen=True)
class SiteExpansionProgramme:
    """Reviewed transformative renovation for one non-campus plan lineage."""

    key: str
    fixture_pattern: str
    axis_bias: int
    court_bias: int
    bay_growth: int
    handedness: int
    rhythm: int


# These choices are small authored overlays on the category grammar.  They say why two food or
# faith campuses differ spatially without copying a functional S-core fixture or pretending a
# random seed is design authority.  Biases are deliberately bounded to one cell; the public spine
# and useful court remain legible for every body and compass pose.
CREED_CAMPUS_PROGRAMMES: Dict[str, CreedCampusProgramme] = {
    "creed-joppa": CreedCampusProgramme("seed-row-yard", "outer-bays", 0, 0, 0, -1, 0),
    "creed-kyakukya": CreedCampusProgramme("smoke-drying-yard", "paired-bays", -1, 1, 1, 1, 1),
    "creed-ezra": CreedCampusProgramme("spindle-workyard", "paired-bays", -1, 0, 0, 1, 0),
    "creed-snapjaws": CreedCampusProgramme("trail-muster", "outer-bays", 0, 0, 0, -1, 0),
    "creed-cragmensch": CreedCampusProgramme("warm-stone-walk", "alternating-bays", 1, 1, 0, 1, 1),
    "creed-robots": CreedCampusProgramme("charge-inspection-grid", "outer-bays", 0, -1, 1, 1, 0),
    "creed-baetyls": CreedCampusProgramme("measured-offering-court", "court-edge", 0, 0, 0, -1, 0),
    "creed-dromad": CreedCampusProgramme("sample-loading-court", "outer-bays", -1, 0, 0, 1, 0),
    "creed-entropic": CreedCampusProgramme("witness-walk", "alternating-bays", -1, 0, 0, -1, 1),
    "creed-goatfolk": CreedCampusProgramme("horn-ring", "court-edge", 1, 0, 1, 1, 0),
    "creed-svardym": CreedCampusProgramme("brine-tending-bands", "outer-bays", 0, -1, 1, -1, 2),
    "creed-naphtaali": CreedCampusProgramme("scrap-procession", "alternating-bays", 1, -1, 0, 1, 1),
    "creed-trolls": CreedCampusProgramme("toll-bridge-court", "outer-bays", -1, 1, 0, -1, 1),
    "creed-issachari": CreedCampusProgramme("rifle-rest-lanes", "outer-bays", 1, 1, 0, 1, 1),
    "creed-strangers": CreedCampusProgramme("guest-pallet-courts", "paired-bays", -1, 0, 1, -1, 0),
    "creed-hindren": CreedCampusProgramme("moon-ring", "court-edge", 1, -1, 0, -1, 2),
    "creed-mopango": CreedCampusProgramme("paired-hearth-yard", "paired-bays", 1, 0, 0, 1, 0),
    "creed-girsh": CreedCampusProgramme("sealed-vessel-procession", "alternating-bays", 0, 0, 0, -1, 2),
    "creed-templar": CreedCampusProgramme("ordered-rack-muster", "outer-bays", -1, -1, 1, 1, 2),
    "creed-gyrewights": CreedCampusProgramme("ash-thresholds", "alternating-bays", 1, 0, 1, -1, 0),
    "creed-mamon": CreedCampusProgramme("tithe-witness-court", "court-edge", -1, -1, 1, 1, 1),
    "creed-seekers": CreedCampusProgramme("listening-alcoves", "alternating-bays", 1, 1, 0, 1, 2),
    "creed-wardens": CreedCampusProgramme("watch-muster", "outer-bays", 0, 1, 1, -1, 1),
    "creed-water": CreedCampusProgramme("gauge-service-court", "paired-bays", 0, 0, 0, -1, 1),
    "creed-merchants": CreedCampusProgramme("scale-and-board-court", "court-edge", 1, 1, 0, 1, 2),
    "creed-farmers": CreedCampusProgramme("threshing-bands", "outer-bays", -1, -1, 0, -1, 1),
    "creed-resheph": CreedCampusProgramme("clean-tending-courts", "paired-bays", 0, -1, 1, 1, 1),
    "creed-daughters": CreedCampusProgramme("named-parts-workyard", "paired-bays", 1, 0, 1, -1, 2),
    "creed-yd": CreedCampusProgramme("vine-table-bands", "outer-bays", 1, 1, 1, 1, 2),
    "creed-chavvah": CreedCampusProgramme("bough-school-branches", "court-edge", 0, -1, 1, -1, 2),
}

# These lineages need exact larger bindings for declared cross-plan renovation routes or creed
# catalogue coverage. Each entry is authored spatial authority: it grows source fabric into the
# larger envelope and lays out a named court from deterministic geometry. No stateful object is
# cloned and no unrelated ordinary plan becomes eligible merely because it has a small map.
SITE_EXPANSION_PROGRAMMES: Dict[str, SiteExpansionProgramme] = {
    "canvas-shelter": SiteExpansionProgramme(
        "canvas-dooryard", "threshold-court", -1, 0, 0, -1, 0
    ),
    "timber-hut": SiteExpansionProgramme(
        "timber-hearth-yard", "return-court", 0, 0, 1, 1, 1
    ),
    "mud-hut": SiteExpansionProgramme(
        "sun-dried-court", "threshold-court", 1, -1, 0, -1, 2
    ),
    "ruin-block-hut": SiteExpansionProgramme(
        "salvage-block-yard", "return-court", -1, 1, 1, 1, 2
    ),
    "deepend-underbench": SiteExpansionProgramme(
        "parts-loading-court", "work-court", 1, -1, 1, -1, 0
    ),
    "deepend-reliquary": SiteExpansionProgramme(
        "relic-processional-court", "processional-court", 0, 0, 0, 1, 1
    ),
    "deepend-factorhouse": SiteExpansionProgramme(
        "cask-counting-court", "public-court", 0, 1, 0, -1, 1
    ),
}


def _reviewed_spatial_programme(
    plan_key: str,
) -> Optional[CreedCampusProgramme | SiteExpansionProgramme]:
    return CREED_CAMPUS_PROGRAMMES.get(plan_key) or SITE_EXPANSION_PROGRAMMES.get(
        plan_key
    )


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
    footprint_width: int = 0
    footprint_height: int = 0


@dataclass(frozen=True)
class YardPolicy:
    key: str
    rationale: str
    boundary_period: int


@dataclass(frozen=True)
class YardParameters:
    axis_bias: int
    court_bias: int
    bay_growth: int
    handedness: int
    rhythm: int


@dataclass(frozen=True)
class RealizationRecord:
    generated_key: str
    source_key: str
    context: RealizationContext
    envelope_x: int
    envelope_y: int
    envelope_width: int
    envelope_height: int
    footprint_x: int
    footprint_y: int
    footprint_width: int
    footprint_height: int
    site_cells: int
    transformed_cells: int
    structure_cells: int
    feature_cells: int
    yard_cells: int
    path_cells: int
    boundary_cells: int
    fixture_cells: int
    programme_regions: int
    station_pairs: int
    composition_bays: int
    composition_thresholds: int
    blind_wall_blocks: int
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


@dataclass(frozen=True)
class EnvelopePolicy:
    """How much new lot area becomes building fabric instead of exterior site."""

    key: str
    cross_growth: float
    depth_growth: float


@dataclass(frozen=True)
class Envelope:
    x: int
    y: int
    width: int
    height: int


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

# Fractions apply only to growth beyond authored source dimensions.  Roofed civic and defensive
# buildings grow broad internal programs; food, memorial, and power sites reserve more exterior
# working landscape.  Open designs receive a later floor of 85% so fields and courts read as the
# building, not as a tiny object surrounded by generic yard.
CATEGORY_ENVELOPE_POLICIES: Dict[str, EnvelopePolicy] = {
    "housing": EnvelopePolicy("dwelling-renovation", 0.78, 0.74),
    "food": EnvelopePolicy("productive-landscape", 0.62, 0.62),
    "storage": EnvelopePolicy("store-and-apron", 0.68, 0.66),
    "craft": EnvelopePolicy("workshop-and-court", 0.66, 0.64),
    "power": EnvelopePolicy("machine-and-clearance", 0.58, 0.58),
    "civic": EnvelopePolicy("public-hall-renovation", 0.82, 0.78),
    "faith": EnvelopePolicy("sanctuary-renovation", 0.76, 0.74),
    "knowledge": EnvelopePolicy("study-complex", 0.72, 0.70),
    "defense": EnvelopePolicy("defended-compound", 0.84, 0.80),
    "memorial": EnvelopePolicy("monument-and-grove", 0.55, 0.58),
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


def _expansion_program(plan_key: str) -> Optional[str]:
    """Return the authored generator programme, or None when a larger binding must be authored.

    Creed practice campuses add size-scaled inert fixtures. Reviewed site renovations transform
    named housing and high-tier creed lineages without cloning stateful objects. Other buildings
    remain at their declared size until they receive equivalent spatial authority.
    """

    if plan_key in CREED_CAMPUS_PROGRAMMES:
        return "creed-practice-campus"
    if plan_key in SITE_EXPANSION_PROGRAMMES:
        return "reviewed-site-renovation"
    return None


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


def _declared_footprint(building: Dict[str, str]) -> Tuple[int, int]:
    raw = building.get("Footprint", "").strip()
    if not raw:
        return 0, 0
    match = re.fullmatch(r"([1-9][0-9]*)x([1-9][0-9]*)", raw)
    if match is None:
        raise GenerationError(
            f"building {building.get('Key', '')!r} has malformed Footprint={raw!r}"
        )
    return int(match.group(1)), int(match.group(2))


def _authored_map_footprint(architecture_map: ET.Element) -> Envelope:
    """Read one canonical authored ``X,Y,WxH`` footprint and prove its bounds."""

    raw = architecture_map.get("Footprint", "").strip()
    match = re.fullmatch(
        r"(0|[1-9][0-9]*),(0|[1-9][0-9]*),([1-9][0-9]*)x([1-9][0-9]*)",
        raw,
    )
    if match is None:
        raise GenerationError(
            f"explicit-footprint source map {architecture_map.get('Key')!r} needs "
            "canonical Footprint='X,Y,WxH'"
        )
    footprint = Envelope(*(int(value, 10) for value in match.groups()))
    width = int(architecture_map.get("Width", "0"))
    height = int(architecture_map.get("Height", "0"))
    if (
        footprint.x + footprint.width > width
        or footprint.y + footprint.height > height
    ):
        raise GenerationError(
            f"source map {architecture_map.get('Key')!r} footprint {raw!r} exceeds "
            f"its {width}x{height} map"
        )
    return footprint


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
        source_anchors = tuple(
            token for token in source_glyph.get("Anchors", "").split(",") if token
        )
        inert_object = CREED_EXPANSION_INERT_OBJECTS.get(source_object)
        # A paid benefit fixture may lend its silhouette to the larger creed realization, but
        # never its provider part. Expansion receives a palette-declared inert timber surface.
        if source_object.startswith("$benefit-"):
            inert_object = next(
                (reference for anchor, reference in CREED_BENEFIT_INERT_BY_ANCHOR.items()
                 if anchor in source_anchors),
                CREED_BENEFIT_INERT_OVERRIDES.get(context.build_key, "$practicetable"),
            )
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
    side: str,
    envelope: Envelope,
) -> Tuple[Tuple[int, int], ...]:
    """Compose reviewed path-side stations; never scatter fixtures by hash."""

    wanted = CREED_EXPANSION_FIXTURE_COUNTS.get(context.target_size, 0)
    if not context.plan_key.startswith("creed-") or not wanted:
        return ()
    programme = CREED_CAMPUS_PROGRAMMES.get(context.plan_key)
    if programme is None:
        raise GenerationError(
            f"creed plan {context.plan_key!r} has no reviewed campus programme"
        )

    def in_compact_core(cell: Tuple[int, int]) -> bool:
        return (
            envelope.x <= cell[0] < envelope.x + envelope.width
            and envelope.y <= cell[1] < envelope.y + envelope.height
        )

    eligible = [
        cell
        for cell in yard_cells
        if not in_compact_core(cell)
        and all(abs(cell[0] - ex) + abs(cell[1] - ey) > 1 for ex, ey in entrances)
        and any(
            abs(cell[0] - px) + abs(cell[1] - py) == 1
            for px, py in path_cells
        )
    ]
    oriented = {
        cell: _oriented(cell[0], cell[1], width, height, side)[:2]
        for cell in eligible
    }
    span = width if side in {"N", "S"} else height
    axis = max(
        1,
        min(span - 2, (span - 1) // 2 + programme.axis_bias),
    )
    size_rank = LOT_ORDER.index(context.target_size)
    preferred_radius = {
        "paired-bays": 2,
        "court-edge": min(max(2, size_rank + 1), max(2, span // 3)),
        "outer-bays": max(2, span // 3),
        "alternating-bays": min(3, max(2, span // 3)),
    }.get(programme.fixture_pattern)
    if preferred_radius is None:
        raise GenerationError(
            f"creed programme {programme.key!r} has unknown fixture pattern "
            f"{programme.fixture_pattern!r}"
        )

    def programme_region(cell: Tuple[int, int]) -> Tuple[int, int]:
        cross, depth = oriented[cell]
        span = width if side in {"N", "S"} else height
        depth_span = height if side in {"N", "S"} else width
        return (
            0 if cross * 2 < span else 1,
            min(2, depth * 3 // depth_span),
        )

    def checked_layout(
        cells: Sequence[Tuple[int, int]],
    ) -> Tuple[Tuple[int, int], ...]:
        result = tuple(cells)
        required_regions = {"M": 2, "L": 4, "XL": 5}[context.target_size]
        regions = {programme_region(cell) for cell in result}
        cross_regions = {region[0] for region in regions}
        depth_regions = {region[1] for region in regions}
        required_depth_regions = {"M": 1, "L": 2, "XL": 3}[context.target_size]
        if (
            len(result) != wanted
            or len(set(result)) != wanted
            or len(regions) < required_regions
            or len(cross_regions) != 2
            or len(depth_regions) < required_depth_regions
        ):
            raise GenerationError(
                f"generated creed map for {context.build_key!r} {context.target_size} "
                f"does not distribute its named programme: fixtures={len(result)}/{wanted} "
                f"regions={len(regions)}/{required_regions} "
                f"cross={len(cross_regions)}/2 depth={len(depth_regions)}/"
                f"{required_depth_regions} programme={programme.key}"
            )
        return result

    if context.target_size == "M":
        # The compact forecourt has only one added row and two side strips.  Its two complementary
        # practices mark different approaches; forcing an XL-style pair here creates diagonal
        # nonsense when the sanctum consumes the middle four rows.
        candidates = []
        for first_index, first in enumerate(eligible):
            for second in eligible[first_index + 1 :]:
                first_cross, first_depth = oriented[first]
                second_cross, second_depth = oriented[second]
                candidates.append(
                    (
                        -int((first_cross < axis) != (second_cross < axis)),
                        -int((first_depth < height // 2) != (second_depth < height // 2)),
                        -(
                            abs(first[0] - second[0])
                            + abs(first[1] - second[1])
                        ),
                        int(
                            _outward(first[0], first[1], width, height)
                            and _outward(second[0], second[1], width, height)
                        ),
                        first[1],
                        first[0],
                        second[1],
                        second[0],
                        first,
                        second,
                    )
                )
        if not candidates:
            raise GenerationError(
                f"generated creed map for {context.build_key!r} M has fewer than two "
                f"lawful forecourt stations; programme={programme.key} eligible={len(eligible)}"
            )
        first, second = min(candidates)[-2:]
        return checked_layout(
            (first, second) if programme.handedness < 0 else (second, first)
        )

    pair_count = wanted // 2
    pair_candidates = []
    eligible_set = set(eligible)
    for path_cell in sorted(path_cells, key=lambda cell: (cell[1], cell[0])):
        neighbours = sorted(
            (
                cell
                for cell in (
                    (path_cell[0], path_cell[1] - 1),
                    (path_cell[0] + 1, path_cell[1]),
                    (path_cell[0], path_cell[1] + 1),
                    (path_cell[0] - 1, path_cell[1]),
                )
                if cell in eligible_set
            ),
            key=lambda cell: (cell[1], cell[0]),
        )
        path_cross, path_depth = _oriented(
            path_cell[0], path_cell[1], width, height, side
        )[:2]
        for first_index, first in enumerate(neighbours):
            for second in neighbours[first_index + 1 :]:
                first_vector = (
                    first[0] - path_cell[0],
                    first[1] - path_cell[1],
                )
                second_vector = (
                    second[0] - path_cell[0],
                    second[1] - path_cell[1],
                )
                opposite = (
                    first_vector[0] + second_vector[0] == 0
                    and first_vector[1] + second_vector[1] == 0
                )
                corner = (
                    first_vector[0] * second_vector[0]
                    + first_vector[1] * second_vector[1]
                    == 0
                )
                relation_penalty = (
                    int(not opposite)
                    if programme.fixture_pattern in {"paired-bays", "outer-bays"}
                    else int(not corner)
                )
                pair_side = (
                    _oriented(first[0], first[1], width, height, side)[0]
                    + _oriented(second[0], second[1], width, height, side)[0]
                    - 2 * axis
                )
                handed_penalty = (
                    0
                    if programme.fixture_pattern != "alternating-bays"
                    or pair_side == 0
                    or pair_side * programme.handedness > 0
                    else 1
                )
                pair_candidates.append(
                    (
                        path_depth,
                        relation_penalty,
                        abs(abs(path_cross - axis) - preferred_radius),
                        handed_penalty,
                        int(
                            _outward(first[0], first[1], width, height)
                            or _outward(second[0], second[1], width, height)
                        ),
                        path_cell[1],
                        path_cell[0],
                        first[1],
                        first[0],
                        second[1],
                        second[0],
                        first,
                        second,
                    )
                )
    # A transverse work band has two useful ends, not necessarily two fixtures touching the same
    # paving cell. Admit only short, same-depth pairs; this covers field and muster bands without
    # reopening arbitrary across-lot pairing.
    for first_index, first in enumerate(eligible):
        first_cross, first_depth = oriented[first]
        for second in eligible[first_index + 1 :]:
            second_cross, second_depth = oriented[second]
            if (
                abs(first_depth - second_depth) > 1
                or abs(first[0] - second[0]) + abs(first[1] - second[1])
                > 2 * preferred_radius + 3
            ):
                continue
            path_depth = (first_depth + second_depth) // 2
            path_cross = (first_cross + second_cross) // 2
            pair_candidates.append(
                (
                    path_depth,
                    1
                    + int((first_cross - axis) * (second_cross - axis) >= 0),
                    abs(abs(path_cross - axis) - preferred_radius),
                    0,
                    int(
                        _outward(first[0], first[1], width, height)
                        or _outward(second[0], second[1], width, height)
                    ),
                    min(first[1], second[1]),
                    min(first[0], second[0]),
                    max(first[1], second[1]),
                    max(first[0], second[0]),
                    first[1],
                    first[0],
                    first,
                    second,
                )
            )
    if not pair_candidates:
        raise GenerationError(
            f"generated creed map for {context.build_key!r} {context.target_size} has "
            "no opposite-side path station pair; "
            f"programme={programme.key} eligible={len(eligible)}"
        )

    def distributed_depths(depths: Sequence[int], count: int) -> Tuple[int, ...]:
        if count <= 0:
            return ()
        if count == 1:
            return (depths[-1],)
        indices = [
            round(index * (len(depths) - 1) / (count - 1))
            for index in range(count)
        ]
        return tuple(depths[index] for index in indices)

    chosen: List[Tuple[int, int]] = []
    station_depths = sorted({candidate[0] for candidate in pair_candidates})
    depth_targets = distributed_depths(station_depths, pair_count)
    left_target = max(1, axis - preferred_radius)
    right_target = min(span - 2, axis + preferred_radius)
    cross_targets = tuple(
        (
            left_target
            if (index + int(programme.handedness > 0)) % 2 == 0
            else right_target
        )
        for index in range(pair_count)
    )
    for target_depth, target_cross in zip(depth_targets, cross_targets):
        available = [
            candidate
            for candidate in pair_candidates
            if candidate[-2] not in chosen and candidate[-1] not in chosen
        ]
        if not available:
            raise GenerationError(
                f"generated creed map for {context.build_key!r} {context.target_size} "
                f"cannot compose {pair_count} disjoint station pairs"
            )
        pair = min(
            available,
            key=lambda candidate: (
                -len(
                    {
                        programme_region(candidate[-2]),
                        programme_region(candidate[-1]),
                    }
                    - {
                        programme_region(chosen_cell)
                        for chosen_cell in chosen
                    }
                ),
                candidate[1],
                abs(candidate[0] - target_depth),
                abs(
                    (
                        oriented[candidate[-2]][0]
                        + oriented[candidate[-1]][0]
                    )
                    // 2
                    - target_cross
                ),
                candidate[2],
                candidate[3],
                candidate[4],
                candidate[5],
                candidate[6],
                candidate[7],
                candidate[8],
                candidate[9],
                candidate[10],
            ),
        )
        first, second = pair[-2], pair[-1]
        chosen.extend(
            (first, second) if programme.handedness < 0 else (second, first)
        )

    if wanted % 2:
        remaining = [cell for cell in eligible if cell not in chosen]
        if not remaining:
            raise GenerationError(
                f"generated creed map for {context.build_key!r} {context.target_size} "
                "has no lawful focal station"
            )
        maximum_depth = max(oriented[cell][1] for cell in remaining)
        focal_depth = (
            maximum_depth
            if programme.fixture_pattern in {"court-edge", "alternating-bays"}
            else sorted(oriented[cell][1] for cell in remaining)[len(remaining) // 2]
        )
        remaining.sort(
            key=lambda cell: (
                int(programme_region(cell) in {
                    programme_region(chosen_cell) for chosen_cell in chosen
                }),
                abs(oriented[cell][1] - focal_depth),
                abs(abs(oriented[cell][0] - axis) - preferred_radius),
                0
                if (oriented[cell][0] - axis) * programme.handedness > 0
                else 1,
                int(_outward(cell[0], cell[1], width, height)),
                cell[1],
                cell[0],
            )
        )
        chosen.append(remaining[0])

    return checked_layout(chosen)


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


def _runtime_authored_lane(
    canvas: List[List[str]], entrance: Tuple[int, int]
) -> Optional[Tuple[Tuple[Tuple[int, int], ...], Tuple[int, int]]]:
    """Canonical equivalent of runtime TryAuthoredLane for generator release gates."""

    route = _unclaimed_route(canvas, entrance)
    if route is None:
        return None
    width = len(canvas[0])
    height = len(canvas)
    edge = route[-1] if route else entrance
    x, y = edge
    exit_step = (
        (0, -1)
        if y == 0
        else (1, 0)
        if x == width - 1
        else (0, 1)
        if y == height - 1
        else (-1, 0)
        if x == 0
        else None
    )
    if exit_step is None:
        return None
    intermediates = list(route)
    for distance in range(1, ROAD_MARGIN + 1):
        intermediates.append(
            (
                x + exit_step[0] * distance,
                y + exit_step[1] * distance,
            )
        )
    lane = (
        x + exit_step[0] * (ROAD_MARGIN + 1),
        y + exit_step[1] * (ROAD_MARGIN + 1),
    )
    if len(intermediates) > MAX_ROUTE_CELLS:
        return None
    return tuple(intermediates), lane


def _campus_frontage_threshold(
    map_key: str,
    canvas: List[List[str]],
    glyphs: Dict[str, ET.Element],
    generated_glyphs: List[ET.Element],
    side: str,
    path_char: str,
    yard_cells: Set[Tuple[int, int]],
    path_cells: Set[Tuple[int, int]],
    excluded: Set[Tuple[int, int]],
    frontage_edges: Sequence[Tuple[int, int]],
) -> Tuple[int, int]:
    """Give a generated campus one visible, claimed path threshold on its road facade.

    The source sanctum door remains a public entrance with its exact unclaimed final approach.
    This additional site threshold makes the larger campus circulation legible and owned without
    inventing a nearby-road allowance: runtime still leaves the edge by the exact DoorToLane law.
    """

    width = len(canvas[0])
    height = len(canvas)
    if side == "N":
        boundary = tuple((x, 0) for x in range(1, width - 1))
    elif side == "E":
        boundary = tuple((width - 1, y) for y in range(1, height - 1))
    elif side == "S":
        boundary = tuple((x, height - 1) for x in range(1, width - 1))
    elif side == "W":
        boundary = tuple((0, y) for y in range(1, height - 1))
    else:
        raise GenerationError(f"campus threshold has unknown frontage side {side!r}")
    allowed = (yard_cells | path_cells) - excluded
    candidates = [cell for cell in boundary if cell in allowed]
    if not candidates or not path_cells:
        raise GenerationError(
            f"campus {map_key!r} has no lawful claimed path threshold"
        )

    best: Optional[
        Tuple[Tuple[int, int, int, int], Tuple[int, int], Tuple[Tuple[int, int], ...]]
    ] = None
    for candidate in candidates:
        parents: Dict[Tuple[int, int], Optional[Tuple[int, int]]] = {candidate: None}
        queue = deque((candidate,))
        target: Optional[Tuple[int, int]] = None
        while queue and target is None:
            cell = queue.popleft()
            if cell != candidate and cell in path_cells:
                target = cell
                break
            for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
                neighbor = (cell[0] + dx, cell[1] + dy)
                if neighbor in allowed and neighbor not in parents:
                    parents[neighbor] = cell
                    queue.append(neighbor)
        if target is None:
            neighbors = [
                (candidate[0] + dx, candidate[1] + dy)
                for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                if (candidate[0] + dx, candidate[1] + dy) in allowed
            ]
            if not neighbors:
                continue
            route = (candidate, min(neighbors, key=lambda cell: (cell[1], cell[0])))
        else:
            reverse: List[Tuple[int, int]] = []
            step: Optional[Tuple[int, int]] = target
            while step is not None:
                reverse.append(step)
                step = parents[step]
            route = tuple(reversed(reverse))
        frontage_distance = min(
            abs(candidate[0] - edge[0]) + abs(candidate[1] - edge[1])
            for edge in frontage_edges
        )
        score = (frontage_distance, len(route), candidate[1], candidate[0])
        if best is None or score < best[0]:
            best = (score, candidate, route)
    if best is None:
        raise GenerationError(
            f"campus {map_key!r} frontage threshold cannot reach its visible path"
        )

    _score, threshold, route = best
    for cell in route:
        canvas[cell[1]][cell[0]] = path_char
        yard_cells.discard(cell)
        path_cells.add(cell)
    attributes = {
        key: value
        for key, value in glyphs[path_char].attrib.items()
        if key != "Char"
    }
    attributes["Anchors"] = "entrance:public"
    threshold_char = _glyph_for_attributes(
        glyphs, generated_glyphs, attributes, "e"
    )
    canvas[threshold[1]][threshold[0]] = threshold_char
    return threshold


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
        # Multi-door compounds may deliberately address two streets. Use their deterministic
        # dominant facade for siting, then reserve every entrance's own exact egress below.
        side = _dominant_frontage_side(source)
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


def _yard_parameters(context: RealizationContext) -> YardParameters:
    programme = _reviewed_spatial_programme(context.plan_key)
    if programme is not None:
        return YardParameters(
            programme.axis_bias,
            programme.court_bias,
            programme.bay_growth,
            programme.handedness,
            programme.rhythm,
        )
    phase = _stable_phase(context)
    return YardParameters(
        phase % 5 - 2,
        (phase // 5) % 3 - 1,
        (phase // 15) % 2,
        -1 if (phase // 31) % 2 else 1,
        (phase // 97) % 3,
    )


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
    parameters = _yard_parameters(context)
    size_rank = LOT_ORDER.index(context.target_size)
    centre = max(
        1,
        min(span - 2, (span - 1) // 2 + parameters.axis_bias),
    )
    court_depth = max(
        1,
        min(depth_span - 2, depth_span // 2 + parameters.court_bias),
    )
    pocket_half = min(
        max(1, size_rank + 1 + parameters.bay_growth),
        max(1, span // 3),
    )
    if policy.key == "field-bands":
        period = 3 if context.target_size in {"L", "XL"} else 2
        return (
            "path"
            if cross == centre or (depth + parameters.rhythm) % period == 0
            else "yard"
        )
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
            min(
                span - 1,
                centre + ((depth + parameters.rhythm) // 3) % 3 - 1,
            ),
        )
        previous_axis = max(
            0,
            min(
                span - 1,
                centre
                + ((max(0, depth - 1) + parameters.rhythm) // 3) % 3
                - 1,
            ),
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
            min(
                span - 1,
                centre + ((depth + parameters.rhythm) // 4) % 3 - 1,
            ),
        )
        previous_walk = max(
            0,
            min(
                span - 1,
                centre
                + ((max(0, depth - 1) + parameters.rhythm) // 4) % 3
                - 1,
            ),
        )
        turn = (
            grove_walk != previous_walk
            and min(grove_walk, previous_walk) <= cross <= max(grove_walk, previous_walk)
        )
        remembrance_bay = (
            depth == court_depth
            and abs(cross - centre) <= pocket_half
            and (cross + parameters.rhythm) % 2 == 0
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
        service_axis = max(
            0,
            min(span - 1, centre + parameters.handedness),
        )
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
    return_side = parameters.handedness
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

    programme = CREED_CAMPUS_PROGRAMMES.get(context.plan_key)
    if programme is not None:
        # Short transverse pads turn path ends into visible work stations.  Their depths are an
        # even progression beyond the four-row sanctum, so M/L/XL add one/two/five usable bays
        # instead of hash-selected paving nubs.
        pair_count = CREED_EXPANSION_FIXTURE_COUNTS[context.target_size] // 2
        available_depths = tuple(range(min(5, depth_span - 1), depth_span))
        if not available_depths:
            return frozenset()
        if pair_count == 1:
            target_depths = (available_depths[-1],)
        else:
            target_depths = tuple(
                available_depths[
                    round(index * (len(available_depths) - 1) / (pair_count - 1))
                ]
                for index in range(pair_count)
            )
        axis = max(
            1,
            min(span - 2, (span - 1) // 2 + programme.axis_bias),
        )
        chosen: Set[Tuple[int, int]] = set()
        used_depths: Set[int] = set()
        for target_depth in target_depths:
            depth_options = sorted(
                range(depth_span),
                key=lambda depth: (abs(depth - target_depth), depth),
            )
            selected: Optional[Tuple[int, int]] = None
            for depth in depth_options:
                if depth in used_depths or depth < min(5, depth_span - 1):
                    continue
                path_crosses = [
                    cross
                    for cross in range(span)
                    if _base_yard_kind_oriented(
                        policy, context, cross, depth, span, depth_span
                    )
                    == "path"
                ]
                if path_crosses:
                    selected = (
                        min(path_crosses, key=lambda cross: (abs(cross - axis), cross)),
                        depth,
                    )
                    break
            if selected is None:
                continue
            path_cross, depth = selected
            used_depths.add(depth)
            if programme.fixture_pattern == "paired-bays":
                left_radius = right_radius = 1 + int(
                    context.target_size == "XL" and programme.bay_growth > 0
                )
            elif programme.fixture_pattern == "court-edge":
                left_radius = right_radius = 2 + int(
                    context.target_size == "XL" and programme.bay_growth > 0
                )
            elif programme.fixture_pattern == "outer-bays":
                left_radius = right_radius = min(
                    3,
                    2 + programme.bay_growth,
                )
            else:
                long_side = programme.handedness
                if (len(used_depths) + programme.rhythm) % 2:
                    long_side *= -1
                left_radius = 2 if long_side < 0 else 1
                right_radius = 2 if long_side > 0 else 1
            for cross in range(
                max(0, path_cross - left_radius),
                min(span, path_cross + right_radius + 1),
            ):
                if _base_yard_kind_oriented(
                    policy, context, cross, depth, span, depth_span
                ) == "yard":
                    chosen.add((cross, depth))
            return_depth = depth - 1 if depth > 0 else depth + 1
            left_edge = max(0, path_cross - left_radius)
            right_edge = min(span - 1, path_cross + right_radius)
            if programme.fixture_pattern == "court-edge":
                returns = (left_edge, right_edge)
            elif programme.fixture_pattern == "outer-bays":
                returns = (
                    left_edge if programme.handedness < 0 else right_edge,
                )
            elif programme.fixture_pattern == "alternating-bays":
                returns = (
                    left_edge
                    if (len(used_depths) + programme.rhythm) % 2
                    else right_edge,
                )
            else:
                returns = ()
            for cross in returns:
                if (
                    0 <= return_depth < depth_span
                    and _base_yard_kind_oriented(
                        policy,
                        context,
                        cross,
                        return_depth,
                        span,
                        depth_span,
                    )
                    == "yard"
                ):
                    chosen.add((cross, return_depth))
        return frozenset(chosen)
    if context.plan_key in SITE_EXPANSION_PROGRAMMES:
        # One authored terminal bay makes added land useful and legible. Geometry comes only from
        # reviewed programme parameters; receipt identity cannot add hash-selected paving nubs.
        programme = SITE_EXPANSION_PROGRAMMES[context.plan_key]
        axis = max(
            1,
            min(span - 2, (span - 1) // 2 + programme.axis_bias),
        )
        depth = max(1, depth_span - 2 - programme.rhythm % 2)
        radius = min(
            max(1, span // 2 - 1),
            max(2, span // 4 + programme.bay_growth),
        )
        chosen: Set[Tuple[int, int]] = set()
        for cross in range(max(0, axis - radius), min(span, axis + radius + 1)):
            if _base_yard_kind_oriented(
                policy, context, cross, depth, span, depth_span
            ) == "yard":
                chosen.add((cross, depth))
        return_cross = 0 if programme.handedness < 0 else span - 1
        for return_depth in range(1, max(2, depth + 1)):
            if _base_yard_kind_oriented(
                policy, context, return_cross, return_depth, span, depth_span
            ) == "yard":
                chosen.add((return_cross, return_depth))
        return frozenset(chosen)
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


def _site_choice_key(
    context: RealizationContext,
    side: str,
    cell: Tuple[int, int],
    width: int,
    height: int,
    purpose: str,
) -> Tuple[int, int, int, int]:
    """Geometric tie-break for authored campuses; hashes remain only for unreviewed families."""

    programme = _reviewed_spatial_programme(context.plan_key)
    if programme is not None:
        cross, depth, span = _oriented(cell[0], cell[1], width, height, side)
        depth_span = height if side in {"N", "S"} else width
        axis = max(
            1,
            min(span - 2, (span - 1) // 2 + programme.axis_bias),
        )
        return (
            abs(cross - axis),
            depth if programme.rhythm % 2 == 0 else depth_span - 1 - depth,
            cross if programme.handedness > 0 else span - 1 - cross,
            cell[1] * width + cell[0],
        )
    digest = hashlib.sha256(
        f"{_stable_identity(context)}|{purpose}|{cell[0]}|{cell[1]}".encode("utf-8")
    ).digest()
    return (int.from_bytes(digest[:8], "big"), 0, 0, 0)


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


def _site_mask_fingerprint(
    path_cells: Set[Tuple[int, int]], width: int, height: int
) -> str:
    """Fingerprint the path mask actually written by an exact-footprint site plan."""

    mask = "".join(
        "p" if (x, y) in path_cells else "y"
        for y in range(height)
        for x in range(width)
    )
    return hashlib.sha256(mask.encode("ascii")).hexdigest()[:16]


def _from_oriented(
    cross: int, depth: int, width: int, height: int, side: str
) -> Tuple[int, int]:
    """Inverse of :func:`_oriented` for one in-bounds site coordinate."""

    if side == "S":
        return cross, height - 1 - depth
    if side == "E":
        return width - 1 - depth, cross
    if side == "W":
        return depth, cross
    return cross, depth


def _compact_housing_path_cells(
    context: RealizationContext,
    entrance: Tuple[int, int],
    available: Set[Tuple[int, int]],
    width: int,
    height: int,
    side: str,
) -> Set[Tuple[int, int]]:
    """Lay one paid-truthful domestic court, not a formal grid across future land.

    Exact-footprint housing keeps the authored shelter at every reservation size. Its natural
    ground may show use, but the invariant building bill cannot imply streets, extra rooms, or
    furnished households. These four reviewed silhouettes stay close to the public threshold:
    canvas has a modest dooryard, timber a returning court, mud a packed drying apron, and salvage
    block an angular swept corner. The public road lane itself remains unclaimed and is not counted.
    """

    programme = SITE_EXPANSION_PROGRAMMES.get(context.plan_key)
    if programme is None:
        raise GenerationError(
            f"housing plan {context.plan_key!r} has no reviewed site programme"
        )
    entrance_cross, entrance_depth, span = _oriented(
        entrance[0], entrance[1], width, height, side
    )
    depth_span = height if side in {"N", "S"} else width
    size = context.target_size
    half = {"M": 2, "L": 3, "XL": 4}[size]
    reach = {"M": 2, "L": 5, "XL": 9}[size]
    offsets: Set[Tuple[int, int]] = set()

    if context.plan_key == "canvas-shelter":
        rows = {"M": 2, "L": 3, "XL": 4}[size]
        radius = {"M": 2, "L": 2, "XL": 3}[size]
        for relative_depth in range(rows):
            for relative_cross in range(-radius, radius + 1):
                if relative_cross:
                    offsets.add((relative_cross, relative_depth))
        rail_depth = {"M": 1, "L": 4, "XL": 9}[size]
        rail_cross = programme.handedness * radius
        offsets.update((rail_cross, depth) for depth in range(rail_depth + 1))
    elif context.plan_key == "timber-hut":
        for relative_depth in range(reach + 1):
            offsets.add((-half, relative_depth))
            offsets.add((half, relative_depth))
        for relative_cross in range(-half, half + 1):
            if relative_cross:
                offsets.add((relative_cross, 0))
                offsets.add((relative_cross, reach))
    elif context.plan_key == "mud-hut":
        rows = {"M": 2, "L": 3, "XL": 4}[size]
        apron_half = {"M": 2, "L": 3, "XL": 3}[size]
        for relative_depth in range(rows):
            for relative_cross in range(-apron_half, apron_half + 1):
                if relative_cross:
                    offsets.add((relative_cross, relative_depth))
        rail_depth = {"M": 1, "L": 4, "XL": 9}[size]
        rail_cross = programme.handedness * apron_half
        offsets.update((rail_cross, depth) for depth in range(rail_depth + 1))
    elif context.plan_key == "ruin-block-hut":
        handed = -1 if programme.handedness < 0 else 1
        for relative_depth in range(reach + 1):
            offsets.add((-handed * half, relative_depth))
            if relative_depth <= reach // 2:
                offsets.add((handed * half, relative_depth))
        for relative_depth in {max(1, reach // 2), reach}:
            for relative_cross in range(-half, half + 1):
                if relative_cross:
                    offsets.add((relative_cross, relative_depth))
    else:
        raise GenerationError(
            f"housing plan {context.plan_key!r} has no compact court silhouette"
        )

    # A shoulder one row behind the threshold keeps the M court visible in two depth bands even
    # where the exact six-column source consumes almost all of its front half.
    offsets.update({(-1, -1), (1, -1)})
    chosen: Set[Tuple[int, int]] = set()
    for relative_cross, relative_depth in offsets:
        cross = entrance_cross + relative_cross
        depth = entrance_depth + relative_depth
        if 0 <= cross < span and 0 <= depth < depth_span:
            cell = _from_oriented(cross, depth, width, height, side)
            if cell in available:
                chosen.add(cell)

    minimum = {"M": 5, "L": 9, "XL": 25}[size]
    maximum = {"M": 10, "L": 22, "XL": 35}[size]
    if not minimum <= len(chosen) <= maximum:
        raise GenerationError(
            f"housing site {context.plan_key!r} {size} has an implausible compact court: "
            f"paths={len(chosen)} expected={minimum}..{maximum}"
        )
    return chosen


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

    found = _reachable_claimed_walk(canvas, glyphs, entrances, allowed_open)
    return generated_walk <= found


def _reachable_claimed_walk(
    canvas: List[List[str]],
    glyphs: Dict[str, ET.Element],
    entrances: Sequence[Tuple[int, int]],
    allowed_open: Set[Tuple[int, int]],
) -> Set[Tuple[int, int]]:
    width = len(canvas[0])
    height = len(canvas)
    # A valid public route reaches plot exterior. Reserved one-cell circulation lane then reaches
    # walkable lot-edge yard facts without treating intervening ``.`` as authored floor. Exact
    # threshold-to-edge proof is separate; this wider exterior model closes isolated courtyards.
    boundary_walk = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if _outward(x, y, width, height) and _walkable(char, glyphs)
    }
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
    return found


def _strict_claimed_walk(
    canvas: List[List[str]],
    glyphs: Dict[str, ET.Element],
    entrances: Sequence[Tuple[int, int]],
) -> Set[Tuple[int, int]]:
    """Mirror checker/runtime anchor access: claimed walk only, starting at public doors."""

    width = len(canvas[0])
    height = len(canvas)
    found = {
        entrance
        for entrance in entrances
        if _walkable(canvas[entrance[1]][entrance[0]], glyphs)
    }
    queue: deque[Tuple[int, int]] = deque(found)
    while queue:
        x, y = queue.popleft()
        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
            neighbor = (x + dx, y + dy)
            nx, ny = neighbor
            if (
                not (0 <= nx < width and 0 <= ny < height)
                or neighbor in found
                or not _walkable(canvas[ny][nx], glyphs)
            ):
                continue
            found.add(neighbor)
            queue.append(neighbor)
    return found


def _claimed_boundary_reachable(
    canvas: List[List[str]],
    glyphs: Dict[str, ET.Element],
    entrance: Tuple[int, int],
) -> bool:
    """Prove one public threshold reaches exact lot edge through claimed walk."""

    width = len(canvas[0])
    height = len(canvas)
    return any(
        _outward(x, y, width, height)
        for x, y in _strict_claimed_walk(canvas, glyphs, (entrance,))
    )


def _project_coordinate(index: int, source_span: int, target_span: int) -> int:
    """Project one grid coordinate monotonically without duplicating source positions."""

    if source_span <= 1 or target_span <= 1:
        return 0
    return (index * (target_span - 1) + (source_span - 1) // 2) // (source_span - 1)


def _inverse_project_coordinate(index: int, source_span: int, target_span: int) -> int:
    if source_span <= 1 or target_span <= 1:
        return 0
    return (index * (source_span - 1) + (target_span - 1) // 2) // (target_span - 1)


def _entrance_exit_side(
    source: ET.Element, entrance: Tuple[int, int]
) -> str:
    """Return exact source egress side, including entrances set behind an open apron."""

    width = int(source.get("Width", "0"))
    height = int(source.get("Height", "0"))
    x, y = entrance
    for side, touches in (
        ("N", y == 0),
        ("E", x == width - 1),
        ("S", y == height - 1),
        ("W", x == 0),
    ):
        if touches:
            return side
    canvas = [list(row.get("Cells", "")) for row in source.findall("row")]
    route = _unclaimed_route(canvas, entrance)
    if route:
        edge_x, edge_y = route[-1]
        for side, touches in (
            ("N", edge_y == 0),
            ("E", edge_x == width - 1),
            ("S", edge_y == height - 1),
            ("W", edge_x == 0),
        ):
            if touches:
                return side
    # Malformed source topology is reported by checker.  Generation stays deterministic so one
    # bad source cannot silently pick a different facade between machines.
    distances = ((y, "N"), (width - 1 - x, "E"), (height - 1 - y, "S"), (x, "W"))
    return min(distances)[1]


def _dominant_frontage_side(source: ET.Element) -> str:
    entrances = _public_entrances(source)
    if not entrances:
        raise GenerationError(f"map {source.get('Key')!r} has no public entrance")
    counts: Dict[str, int] = {side: 0 for side in ("S", "E", "W", "N")}
    for entrance in entrances:
        counts[_entrance_exit_side(source, entrance)] += 1
    return min(counts, key=lambda side: (-counts[side], ("S", "E", "W", "N").index(side)))


def _envelope_for(
    source: ET.Element,
    target_size: str,
    facing: str,
    context: RealizationContext,
) -> Envelope:
    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    target_width, target_height = LOT_DIMENSIONS[target_size]
    policy = CATEGORY_ENVELOPE_POLICIES.get(context.category)
    if policy is None:
        raise GenerationError(
            f"building {context.build_key!r} has no envelope policy for {context.category!r}"
        )
    if _expansion_program(context.plan_key) == "creed-practice-campus":
        # The BuildKey carries one S-sanctum material bill, so larger bindings cannot multiply its
        # walls. Growth is additive low-material ground: named courts/bands and two, five, then ten
        # inert practice stations. The distribution gate below refuses empty padding or scatter.
        envelope_width = source_width
        envelope_height = source_height
    elif context.plan_key in SITE_EXPANSION_PROGRAMMES:
        # Reviewed renovations divide added land between usable interior and named exterior court.
        # Growth is capped by target tier: an XL lot gains rooms plus a broad working court instead
        # of stretching one bed or machine across an empty hangar-sized shell.
        growth_x = target_width - source_width
        growth_y = target_height - source_height
        growth_cap = {"M": 1, "L": 2, "XL": 3}[target_size]
        envelope_width = source_width + min(max(0, growth_x), growth_cap)
        envelope_height = source_height + min(max(0, growth_y), growth_cap)
    else:
        growth_x = target_width - source_width
        growth_y = target_height - source_height
        envelope_width = source_width + (
            0
            if growth_x <= 0
            else max(1, int(growth_x * policy.cross_growth + 0.5))
        )
        envelope_height = source_height + (
            0
            if growth_y <= 0
            else max(1, int(growth_y * policy.depth_growth + 0.5))
        )
    envelope_width = min(target_width, envelope_width)
    envelope_height = min(target_height, envelope_height)
    if facing == "heart":
        x = (target_width - envelope_width) // 2
        y = 0
    elif facing == "road":
        side = _dominant_frontage_side(source)
        margin = 1
        if side == "S":
            x = (target_width - envelope_width) // 2
            y = max(0, target_height - envelope_height - margin)
        elif side == "N":
            x = (target_width - envelope_width) // 2
            y = min(margin, target_height - envelope_height)
        elif side == "E":
            x = max(0, target_width - envelope_width - margin)
            y = (target_height - envelope_height) // 2
        else:
            x = min(margin, target_width - envelope_width)
            y = (target_height - envelope_height) // 2
    else:
        raise GenerationError(f"binding has unsupported Facing={facing!r}")
    return Envelope(x, y, envelope_width, envelope_height)


def _glyph_attributes_without_custody(
    glyph: ET.Element, *, preserve_structure: bool
) -> Dict[str, str]:
    attributes = {
        key: value
        for key, value in glyph.attrib.items()
        if key not in {"Char", "Anchors", "Stateful"}
    }
    _remove_layer(attributes, "Object")
    if not preserve_structure:
        _remove_layer(attributes, "Structure")
    return attributes


def _remove_layer(attributes: Dict[str, str], layer: str) -> None:
    """Remove one placement and its inseparable layer-local pose authority."""

    attributes.pop(layer, None)
    attributes.pop(layer + "Orientation", None)


def _replace_layer(
    attributes: Dict[str, str], layer: str, reference: str
) -> None:
    """Replace semantic scenery without leaking the old family's local orientation."""

    if attributes.get(layer) != reference:
        attributes.pop(layer + "Orientation", None)
    attributes[layer] = reference


def _glyph_for_attributes(
    glyphs: Dict[str, ET.Element],
    generated: List[ET.Element],
    attributes: Dict[str, str],
    preferred: str,
) -> str:
    for char, glyph in sorted(glyphs.items()):
        if {key: value for key, value in glyph.attrib.items() if key != "Char"} == attributes:
            return char
    char = _free_char(glyphs, preferred)
    element = ET.Element("glyph", {"Char": char, **attributes})
    glyphs[char] = element
    generated.append(element)
    return char


def _plain_source_cells(
    source: ET.Element, glyphs: Dict[str, ET.Element]
) -> Tuple[Tuple[int, int, str, ET.Element], ...]:
    result = []
    for y, row in enumerate(source.findall("row")):
        for x, char in enumerate(row.get("Cells", "")):
            glyph = glyphs.get(char)
            if (
                glyph is not None
                and not glyph.get("Structure")
                and not glyph.get("Object")
                and not glyph.get("Anchors")
                and glyph.get("Pass") == "walk"
                and glyph.get("Claim") in WALK_CLAIMS
            ):
                result.append((x, y, char, glyph))
    return tuple(result)


def _background_char(
    raw_char: str,
    source_x: int,
    source_y: int,
    glyphs: Dict[str, ET.Element],
    plain_cells: Sequence[Tuple[int, int, str, ET.Element]],
    generated: List[ET.Element],
) -> Optional[str]:
    if raw_char == ".":
        return None
    raw = glyphs.get(raw_char)
    if raw is None:
        raise GenerationError(f"source map uses undefined glyph {raw_char!r}")
    if not raw.get("Structure") and not raw.get("Object"):
        if not raw.get("Anchors"):
            return raw_char
        return _glyph_for_attributes(
            glyphs,
            generated,
            _glyph_attributes_without_custody(raw, preserve_structure=False),
            "u",
        )
    if plain_cells:
        def score(item: Tuple[int, int, str, ET.Element]) -> Tuple[object, ...]:
            x, y, char, candidate = item
            return (
                0 if candidate.get("Claim") == raw.get("Claim") else 1,
                0 if candidate.get("Cover") == raw.get("Cover") else 1,
                0 if candidate.get("Ground") == raw.get("Ground") else 1,
                abs(x - source_x) + abs(y - source_y),
                y,
                x,
                char,
            )

        return min(plain_cells, key=score)[2]
    attributes = _glyph_attributes_without_custody(raw, preserve_structure=False)
    if attributes.get("Pass") != "walk":
        attributes["Pass"] = "walk"
    return _glyph_for_attributes(glyphs, generated, attributes, "u")


def _line_between(
    first: Tuple[int, int], second: Tuple[int, int]
) -> Tuple[Tuple[int, int], ...]:
    if first[0] == second[0]:
        low, high = sorted((first[1], second[1]))
        return tuple((first[0], y) for y in range(low, high + 1))
    if first[1] == second[1]:
        low, high = sorted((first[0], second[0]))
        return tuple((x, first[1]) for x in range(low, high + 1))
    raise GenerationError(f"structural neighbors projected diagonally: {first}->{second}")


def _frontage_route(
    entrance: Tuple[int, int],
    preferred_side: str,
    width: int,
    height: int,
    blocked: Set[Tuple[int, int]],
    transformed: Set[Tuple[int, int]],
) -> Tuple[Tuple[int, int], ...]:
    """Find deterministic least-renovation route to the door's authored facade edge."""

    preferred = {"N": (0, -1), "E": (1, 0), "S": (0, 1), "W": (-1, 0)}[
        preferred_side
    ]
    perpendicular = (
        ((-1, 0), (1, 0)) if preferred[0] == 0 else ((0, -1), (0, 1))
    )
    directions = (preferred, *perpendicular, (-preferred[0], -preferred[1]))
    distances: Dict[Tuple[int, int], Tuple[int, int]] = {entrance: (0, 0)}
    parents: Dict[Tuple[int, int], Optional[Tuple[int, int]]] = {entrance: None}
    queue: List[Tuple[int, int, int, int]] = [(0, 0, entrance[1], entrance[0])]
    boundary: Optional[Tuple[int, int]] = None

    def preferred_boundary(x: int, y: int) -> bool:
        return (
            (preferred_side == "N" and y == 0)
            or (preferred_side == "E" and x == width - 1)
            or (preferred_side == "S" and y == height - 1)
            or (preferred_side == "W" and x == 0)
        )

    while queue:
        renovation_cost, steps, y, x = heapq.heappop(queue)
        position = (x, y)
        if distances.get(position) != (renovation_cost, steps):
            continue
        if position != entrance and preferred_boundary(x, y):
            boundary = position
            break
        for dx, dy in directions:
            neighbor = (x + dx, y + dy)
            nx, ny = neighbor
            if not (0 <= nx < width and 0 <= ny < height):
                continue
            if neighbor in blocked and neighbor != entrance:
                continue
            candidate = (
                renovation_cost + (4 if neighbor in transformed else 0),
                steps + 1,
            )
            if candidate >= distances.get(neighbor, (10**9, 10**9)):
                continue
            distances[neighbor] = candidate
            parents[neighbor] = position
            heapq.heappush(queue, (candidate[0], candidate[1], ny, nx))
    if boundary is None:
        raise GenerationError(f"projected entrance {entrance} has no frontage route")
    reverse: List[Tuple[int, int]] = []
    step: Optional[Tuple[int, int]] = boundary
    while step is not None and step != entrance:
        reverse.append(step)
        step = parents[step]
    if step != entrance:
        raise GenerationError(f"projected entrance {entrance} has broken route parentage")
    return tuple(reversed(reverse))


def _connector_char(
    first: ET.Element,
    second: ET.Element,
    glyphs: Dict[str, ET.Element],
    generated: List[ET.Element],
) -> str:
    for glyph in (first, second):
        if glyph.get("Structure") and not glyph.get("Object") and not glyph.get("Anchors"):
            return glyph.get("Char", "")
    return _glyph_for_attributes(
        glyphs,
        generated,
        _glyph_attributes_without_custody(first, preserve_structure=True),
        "w",
    )


@dataclass(frozen=True)
class SiteModule:
    """One roofed, unfurnished bay made only from the source palette's paid fabric."""

    x: int
    y: int
    width: int
    height: int
    door_side: str


DEEPEND_COMPOSED_SITE_PROGRAMMES = frozenset(
    {"deepend-underbench", "deepend-reliquary", "deepend-factorhouse"}
)
DEEPEND_COMPOSITION_BAYS: Dict[str, int] = {"L": 3, "XL": 5}


def _actual_site_module(
    cross: int,
    depth: int,
    cross_span: int,
    depth_span: int,
    door_axis: str,
    width: int,
    height: int,
    side: str,
) -> SiteModule:
    """Turn a frontage-oriented room rectangle into map coordinates."""

    direction_maps = {
        "N": {"cross-": "W", "cross+": "E", "depth-": "N", "depth+": "S"},
        "S": {"cross-": "W", "cross+": "E", "depth-": "S", "depth+": "N"},
        "E": {"cross-": "N", "cross+": "S", "depth-": "E", "depth+": "W"},
        "W": {"cross-": "N", "cross+": "S", "depth-": "W", "depth+": "E"},
    }
    if side == "N":
        return SiteModule(
            cross, depth, cross_span, depth_span, direction_maps[side][door_axis]
        )
    if side == "S":
        return SiteModule(
            cross,
            height - depth - depth_span,
            cross_span,
            depth_span,
            direction_maps[side][door_axis],
        )
    if side == "E":
        return SiteModule(
            width - depth - depth_span,
            cross,
            depth_span,
            cross_span,
            direction_maps[side][door_axis],
        )
    if side == "W":
        return SiteModule(
            depth, cross, depth_span, cross_span, direction_maps[side][door_axis]
        )
    raise GenerationError(f"site module has unknown frontage side {side!r}")


def _site_modules(
    context: RealizationContext, width: int, height: int, side: str
) -> Tuple[SiteModule, ...]:
    """Return reviewed room/campus geometry; no receipt hash is design authority."""

    cross_span = width if side in {"N", "S"} else height
    if context.plan_key == "deepend-reliquary":
        specifications = (
            (0, 10, 5, 3, "cross+"),
            (cross_span - 5, 10, 5, 3, "cross-"),
            (2, 14, 5, 4, "depth-"),
            (cross_span - 7, 14, 5, 4, "depth-"),
        )
    elif context.target_size == "L":
        # A three-row roofed loading/counting bay is deliberate here.  The exact M core already
        # occupies six of ten rows; the intervening open row is what keeps the annexes legible.
        specifications = (
            (0, 7, 5, 3, "cross+"),
            (cross_span - 5, 7, 5, 3, "cross-"),
        )
    else:
        specifications = (
            (2, 8, 5, 4, "cross+"),
            (cross_span - 7, 8, 5, 4, "cross-"),
            (2, 13, 5, 4, "cross+"),
            (cross_span - 7, 13, 5, 4, "cross-"),
        )
    return tuple(
        _actual_site_module(
            cross,
            depth,
            module_width,
            module_height,
            door_axis,
            width,
            height,
            side,
        )
        for cross, depth, module_width, module_height, door_axis in specifications
    )


def _site_core_offset(
    source_width: int,
    source_height: int,
    width: int,
    height: int,
    side: str,
    facing: str,
) -> Tuple[int, int]:
    """Place the exact authored core at its declared frontage without stretching it."""

    if facing == "heart":
        return (width - source_width) // 2, 0
    if side == "S":
        return (width - source_width) // 2, height - source_height - 1
    if side == "N":
        return (width - source_width) // 2, 1
    if side == "E":
        return width - source_width - 1, (height - source_height) // 2
    if side == "W":
        return 1, (height - source_height) // 2
    raise GenerationError(f"site core has unknown frontage side {side!r}")


def _building_component_count(
    canvas: List[List[str]], glyphs: Dict[str, ET.Element]
) -> int:
    cells = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if (glyph := glyphs.get(char)) is not None
        and glyph.get("Claim") == "building"
    }
    components = 0
    while cells:
        components += 1
        queue = deque((min(cells, key=lambda cell: (cell[1], cell[0])),))
        cells.remove(queue[0])
        while queue:
            x, y = queue.popleft()
            for neighbor in ((x, y - 1), (x + 1, y), (x, y + 1), (x - 1, y)):
                if neighbor in cells:
                    cells.remove(neighbor)
                    queue.append(neighbor)
    return components


def _solid_wall_block_count(
    canvas: List[List[str]], glyphs: Dict[str, ET.Element]
) -> int:
    """Count blind 3x3 masses, not ordinary one-cell walls or useful partitions."""

    width = len(canvas[0])
    height = len(canvas)
    blocked = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if (glyph := glyphs.get(char)) is not None
        and glyph.get("Structure")
        and glyph.get("Pass") == "blocked"
    }
    return sum(
        all((x + dx, y + dy) in blocked for dy in range(3) for dx in range(3))
        for y in range(height - 2)
        for x in range(width - 2)
    )


def _explicit_footprint_site_map(
    source: ET.Element,
    target_size: str,
    facing: str,
    generated_key: str,
    context: RealizationContext,
    palette: ET.Element,
    records: Optional[List[RealizationRecord]],
) -> ET.Element:
    """Embed one exact authored shelter and compose only its larger reserved yard."""

    if context.category != "housing":
        raise GenerationError(
            f"explicit-footprint generated plan {context.plan_key!r} has no reviewed "
            "site composition"
        )
    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    width, height = LOT_DIMENSIONS[target_size]
    rows = [row.get("Cells", "") for row in source.findall("row")]
    if len(rows) != source_height or any(len(row) != source_width for row in rows):
        raise GenerationError(f"map {source.get('Key')!r} has malformed source rows")

    source_footprint = _authored_map_footprint(source)
    if (source_footprint.width, source_footprint.height) != (
        context.footprint_width,
        context.footprint_height,
    ):
        raise GenerationError(
            f"source map {source.get('Key')!r} footprint "
            f"{source_footprint.width}x{source_footprint.height} differs from "
            f"catalogue {context.footprint_width}x{context.footprint_height}"
        )

    glyphs = _glyphs(source)
    source_scope: Set[Tuple[int, int]] = set()
    protected_source: Set[Tuple[int, int]] = set()
    for source_y, row in enumerate(rows):
        for source_x, char in enumerate(row):
            if char == ".":
                continue
            glyph = glyphs.get(char)
            if glyph is None:
                raise GenerationError(
                    f"source map {source.get('Key')!r} uses unknown glyph {char!r}"
                )
            anchors = glyph.get("Anchors", "").split(",")
            if glyph.get("Claim") == "building" or "main" in anchors:
                source_scope.add((source_x, source_y))
            if (
                glyph.get("Object")
                or glyph.get("Anchors")
                or glyph.get("Stateful") == "yes"
            ):
                protected_source.add((source_x, source_y))
    source_footprint_cells = {
        (x, y)
        for y in range(
            source_footprint.y, source_footprint.y + source_footprint.height
        )
        for x in range(
            source_footprint.x, source_footprint.x + source_footprint.width
        )
    }
    if not source_scope or not source_scope <= source_footprint_cells:
        raise GenerationError(
            f"source map {source.get('Key')!r} leaks building/main scope outside its "
            "canonical footprint"
        )

    policy = _policy_for(context)
    if policy is None:
        raise GenerationError(
            f"building {context.build_key!r} has no yard policy for {context.category!r}"
        )
    generated_glyphs: List[ET.Element] = []
    yard_reference = _ground_reference(source, palette, want_path=False)
    path_reference = _ground_reference(source, palette, want_path=True)
    slots = {
        "$" + slot.get("Key", ""): slot
        for slot in palette.findall("slot")
        if slot.get("Key")
    }
    yard_blueprint = slots[yard_reference].get("Blueprint", "")
    path_blueprint = slots[path_reference].get("Blueprint", "")
    if _visual_blueprint_key(yard_blueprint) == _visual_blueprint_key(path_blueprint):
        raise GenerationError(
            f"explicit-footprint site {generated_key!r} has no visible path authority"
        )
    yard_char, yard_glyph = _neutral_open_char(source, yard_reference, glyphs, "y")
    path_char, path_glyph = _neutral_open_char(source, path_reference, glyphs, "p")
    if yard_glyph is not None:
        generated_glyphs.append(yard_glyph)
    if path_glyph is not None:
        generated_glyphs.append(path_glyph)

    side = "N" if facing == "heart" else _dominant_frontage_side(source)
    source_x, source_y = _site_core_offset(
        source_width, source_height, width, height, side, facing
    )
    footprint = Envelope(
        source_x + source_footprint.x,
        source_y + source_footprint.y,
        source_footprint.width,
        source_footprint.height,
    )
    footprint_cells = {
        (x, y)
        for y in range(footprint.y, footprint.y + footprint.height)
        for x in range(footprint.x, footprint.x + footprint.width)
    }

    canvas = [[yard_char for _x in range(width)] for _y in range(height)]
    yard_cells: Set[Tuple[int, int]] = {
        (x, y) for y in range(height) for x in range(width)
    }
    path_cells: Set[Tuple[int, int]] = set()

    transformed_cells: Set[Tuple[int, int]] = set()
    structure_cells: Set[Tuple[int, int]] = set()
    feature_cells: Set[Tuple[int, int]] = set()
    for local_y, row in enumerate(rows):
        for local_x, char in enumerate(row):
            if char == ".":
                continue
            position = (source_x + local_x, source_y + local_y)
            glyph = glyphs[char]
            canvas[position[1]][position[0]] = char
            yard_cells.discard(position)
            path_cells.discard(position)
            transformed_cells.add(position)
            if glyph.get("Structure"):
                structure_cells.add(position)
            if (
                glyph.get("Object")
                or glyph.get("Anchors")
                or glyph.get("Stateful") == "yes"
            ):
                feature_cells.add(position)

    source_entrances = _public_entrances(source)
    if not source_entrances:
        raise GenerationError(f"explicit-footprint site {generated_key!r} has no entrance")
    entrances = [
        (source_x + entrance_x, source_y + entrance_y)
        for entrance_x, entrance_y in source_entrances
    ]
    route_cells: Set[Tuple[int, int]] = set()
    for source_entrance, entrance in zip(source_entrances, entrances):
        route = _frontage_route(
            entrance,
            _entrance_exit_side(source, source_entrance),
            width,
            height,
            feature_cells | structure_cells | footprint_cells,
            transformed_cells,
        )
        for position in route:
            canvas[position[1]][position[0]] = "."
            yard_cells.discard(position)
            path_cells.discard(position)
            transformed_cells.discard(position)
            structure_cells.discard(position)
            feature_cells.discard(position)
            route_cells.add(position)

    path_cells = _compact_housing_path_cells(
        context, entrances[0], yard_cells, width, height, side
    )
    for x, y in path_cells:
        canvas[y][x] = path_char
        yard_cells.remove((x, y))

    generated_walk = yard_cells | path_cells
    if not yard_cells or not path_cells or not _all_generated_walk_physically_reachable(
        canvas, glyphs, entrances, generated_walk, route_cells
    ):
        raise GenerationError(
            f"explicit footprint {generated_key!r} has disconnected yard/circulation"
        )
    strict_reachable = _strict_claimed_walk(canvas, glyphs, entrances)
    for position in feature_cells:
        glyph = glyphs[canvas[position[1]][position[0]]]
        accessible = (
            position in strict_reachable
            if glyph.get("Pass") == "walk"
            else any(
                (position[0] + dx, position[1] + dy) in strict_reachable
                for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
            )
        )
        if not accessible:
            raise GenerationError(
                f"explicit footprint {generated_key!r} strands feature {position}"
            )
    for entrance in entrances:
        if _runtime_authored_lane(canvas, entrance) is None:
            raise GenerationError(
                f"explicit footprint {generated_key!r} blocks exact lane from {entrance}"
            )

    target_scope = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if char != "."
        and (
            glyphs[char].get("Claim") == "building"
            or "main" in glyphs[char].get("Anchors", "").split(",")
        )
    }
    if not target_scope or not target_scope <= footprint_cells:
        raise GenerationError(
            f"explicit footprint {generated_key!r} leaks building/main scope outside "
            f"{footprint.x},{footprint.y},{footprint.width}x{footprint.height}"
        )

    protected_target = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if char != "."
        and (
            glyphs[char].get("Object")
            or glyphs[char].get("Anchors")
            or glyphs[char].get("Stateful") == "yes"
        )
    }
    expected_protected = {
        (source_x + x, source_y + y) for x, y in protected_source
    }
    if protected_target != expected_protected:
        raise GenerationError(
            f"explicit footprint {generated_key!r} moved or cloned protected source facts"
        )

    span = width if side in {"N", "S"} else height
    depth_span = height if side in {"N", "S"} else width
    programme_regions = len(
        {
            (
                min(2, _oriented(x, y, width, height, side)[0] * 3 // span),
                min(2, _oriented(x, y, width, height, side)[1] * 3 // depth_span),
            )
            for x, y in path_cells
        }
    )
    all_cells = {(x, y) for y in range(height) for x in range(width)}
    partition = transformed_cells | yard_cells | path_cells | route_cells
    if partition != all_cells or sum(
        cell in transformed_cells
        for cell in yard_cells | path_cells | route_cells
    ):
        raise GenerationError(
            f"explicit footprint {generated_key!r} has invalid cell census"
        )
    blind_wall_blocks = _solid_wall_block_count(canvas, glyphs)
    if records is not None:
        records.append(
            RealizationRecord(
                generated_key,
                source.get("Key", ""),
                context,
                footprint.x,
                footprint.y,
                footprint.width,
                footprint.height,
                footprint.x,
                footprint.y,
                footprint.width,
                footprint.height,
                len(all_cells),
                len(transformed_cells),
                len(structure_cells),
                len(feature_cells),
                len(yard_cells),
                len(path_cells),
                0,
                0,
                programme_regions,
                0,
                1,
                len(entrances),
                blind_wall_blocks,
                len(route_cells),
                0,
                0,
                "",
                False,
                _site_mask_fingerprint(path_cells, width, height),
            )
        )
    result = ET.Element(
        "map",
        {
            "Key": generated_key,
            "Width": str(width),
            "Height": str(height),
            "DefaultCover": source.get("DefaultCover", ""),
            "Footprint": (
                f"{footprint.x},{footprint.y},{footprint.width}x{footprint.height}"
            ),
        },
    )
    for glyph in source.findall("glyph"):
        result.append(copy.deepcopy(glyph))
    for glyph in generated_glyphs:
        result.append(copy.deepcopy(glyph))
    for row in canvas:
        ET.SubElement(result, "row", {"Cells": "".join(row)})
    return result


def _composed_site_map(
    source: ET.Element,
    target_size: str,
    facing: str,
    generated_key: str,
    context: RealizationContext,
    palette: ET.Element,
    records: Optional[List[RealizationRecord]],
) -> ET.Element:
    """Compose exact paid cores, roofed bays, and circulation instead of inflating one room."""

    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    width, height = LOT_DIMENSIONS[target_size]
    rows = [row.get("Cells", "") for row in source.findall("row")]
    policy = _policy_for(context)
    if policy is None:
        raise GenerationError(
            f"building {context.build_key!r} has no yard policy for {context.category!r}"
        )
    glyphs = _glyphs(source)
    generated_glyphs: List[ET.Element] = []
    yard_reference = _ground_reference(source, palette, want_path=False)
    path_reference = _ground_reference(source, palette, want_path=True)
    slots = {
        "$" + slot.get("Key", ""): slot
        for slot in palette.findall("slot")
        if slot.get("Key")
    }
    yard_blueprint = slots[yard_reference].get("Blueprint", "")
    path_blueprint = slots[path_reference].get("Blueprint", "")
    if _visual_blueprint_key(yard_blueprint) == _visual_blueprint_key(path_blueprint):
        raise GenerationError(
            f"composed site {generated_key!r} has no visibly distinct path authority"
        )
    yard_char, yard_glyph = _neutral_open_char(source, yard_reference, glyphs, "y")
    path_char, path_glyph = _neutral_open_char(source, path_reference, glyphs, "p")
    if yard_glyph is not None:
        generated_glyphs.append(yard_glyph)
    if path_glyph is not None:
        generated_glyphs.append(path_glyph)

    wall_candidates = [
        (char, glyph)
        for char, glyph in glyphs.items()
        if glyph.get("Claim") == "building"
        and glyph.get("Structure")
        and glyph.get("Pass") == "blocked"
        and not glyph.get("Object")
        and not glyph.get("Anchors")
    ]
    floor_candidates = [
        (char, glyph)
        for char, glyph in glyphs.items()
        if glyph.get("Claim") == "building"
        and glyph.get("Pass") == "walk"
        and not glyph.get("Structure")
        and not glyph.get("Object")
        and not glyph.get("Anchors")
    ]
    if not wall_candidates or not floor_candidates:
        raise GenerationError(
            f"composed site {generated_key!r} needs plain wall and floor source fabric"
        )
    wall_char = min(wall_candidates, key=lambda item: item[0])[0]
    floor_char = min(floor_candidates, key=lambda item: item[0])[0]
    entrance_chars = {
        rows[y][x] for x, y in _public_entrances(source)
    }
    entrance_glyphs = [glyphs[char] for char in sorted(entrance_chars)]
    if not entrance_glyphs:
        raise GenerationError(f"composed site {generated_key!r} has no source entrance")
    door_char = _glyph_for_attributes(
        glyphs,
        generated_glyphs,
        _glyph_attributes_without_custody(
            entrance_glyphs[0], preserve_structure=True
        ),
        "d",
    )
    # Annexes are exterior-accessed bays, so an adjacent-use machine copied from the paid core
    # would be unreachable under the claim-only use contract. Give every bay one stateless,
    # walkable earthen service pad instead: distinct from its finished floor, palette-lawful, and
    # physically usable without minting an object, anchor, or stateful fixture.
    service_attributes = {
        key: value
        for key, value in glyphs[floor_char].attrib.items()
        if key != "Char"
    }
    floor_reference = glyphs[floor_char].get("Ground", "")
    service_reference = (
        yard_reference if yard_reference != floor_reference else path_reference
    )
    _replace_layer(service_attributes, "Ground", service_reference)
    service_char = _glyph_for_attributes(
        glyphs, generated_glyphs, service_attributes, "v"
    )

    side = "N" if facing == "heart" else _dominant_frontage_side(source)
    canvas = [[yard_char for _x in range(width)] for _y in range(height)]
    yard_cells: Set[Tuple[int, int]] = set()
    path_cells: Set[Tuple[int, int]] = set()
    for y in range(height):
        for x in range(width):
            cell = (x, y)
            if _yard_kind(policy, context, side, x, y, width, height) == "path":
                canvas[y][x] = path_char
                path_cells.add(cell)
            else:
                yard_cells.add(cell)

    transformed_cells: Set[Tuple[int, int]] = set()
    structure_cells: Set[Tuple[int, int]] = set()
    feature_cells: Set[Tuple[int, int]] = set()

    def paint(position: Tuple[int, int], char: str) -> None:
        x, y = position
        canvas[y][x] = char
        yard_cells.discard(position)
        path_cells.discard(position)
        transformed_cells.add(position)
        glyph = glyphs[char]
        if glyph.get("Structure"):
            structure_cells.add(position)
        if glyph.get("Object") or glyph.get("Anchors"):
            feature_cells.add(position)

    core_x, core_y = _site_core_offset(
        source_width, source_height, width, height, side, facing
    )
    for source_y, row in enumerate(rows):
        for source_x, char in enumerate(row):
            if char != ".":
                paint((core_x + source_x, core_y + source_y), char)

    modules = _site_modules(context, width, height, side)
    module_doors: List[Tuple[Tuple[int, int], str]] = []
    for module in modules:
        if (
            module.width < 4
            or module.height < 3
            or module.x < 0
            or module.y < 0
            or module.x + module.width > width
            or module.y + module.height > height
        ):
            raise GenerationError(
                f"composed site {generated_key!r} has invalid module {module}"
            )
        cells = {
            (x, y)
            for y in range(module.y, module.y + module.height)
            for x in range(module.x, module.x + module.width)
        }
        overlap = cells & transformed_cells
        if overlap:
            raise GenerationError(
                f"composed site {generated_key!r} overlaps paid core/module at "
                f"{sorted(overlap)[:4]}"
            )
        for x, y in sorted(cells, key=lambda cell: (cell[1], cell[0])):
            perimeter = (
                x in {module.x, module.x + module.width - 1}
                or y in {module.y, module.y + module.height - 1}
            )
            paint((x, y), wall_char if perimeter else floor_char)
        if module.door_side == "E":
            door = (module.x + module.width - 1, module.y + module.height // 2)
            service = (module.x + 1, module.y + 1)
        elif module.door_side == "W":
            door = (module.x, module.y + module.height // 2)
            service = (module.x + module.width - 2, module.y + 1)
        elif module.door_side == "N":
            door = (module.x + module.width // 2, module.y)
            service = (module.x + 1, module.y + module.height - 2)
        else:
            door = (
                module.x + module.width // 2,
                module.y + module.height - 1,
            )
            service = (module.x + 1, module.y + 1)
        paint(door, door_char)
        if service != door:
            paint(service, service_char)
        module_doors.append((door, module.door_side))

    source_entrances = _public_entrances(source)
    entrances = [(core_x + x, core_y + y) for x, y in source_entrances]
    route_cells: Set[Tuple[int, int]] = set()
    for source_entrance, entrance in zip(source_entrances, entrances):
        route = _frontage_route(
            entrance,
            _entrance_exit_side(source, source_entrance),
            width,
            height,
            transformed_cells,
            transformed_cells,
        )
        for position in route:
            canvas[position[1]][position[0]] = "."
            yard_cells.discard(position)
            path_cells.discard(position)
            transformed_cells.discard(position)
            structure_cells.discard(position)
            route_cells.add(position)

    direction = {"N": (0, -1), "E": (1, 0), "S": (0, 1), "W": (-1, 0)}

    def connect_module(door: Tuple[int, int], door_side: str) -> None:
        dx, dy = direction[door_side]
        start = (door[0] + dx, door[1] + dy)
        if start not in yard_cells | path_cells:
            raise GenerationError(
                f"composed site {generated_key!r} module door {door} has no open threshold"
            )
        if start in path_cells:
            return
        parents: Dict[Tuple[int, int], Optional[Tuple[int, int]]] = {start: None}
        queue = deque((start,))
        goal: Optional[Tuple[int, int]] = None
        while queue and goal is None:
            current = queue.popleft()
            for step_x, step_y in ((0, -1), (1, 0), (0, 1), (-1, 0)):
                neighbor = (current[0] + step_x, current[1] + step_y)
                if neighbor in parents or neighbor not in yard_cells | path_cells:
                    continue
                parents[neighbor] = current
                if neighbor in path_cells:
                    goal = neighbor
                    break
                queue.append(neighbor)
        if goal is None:
            raise GenerationError(
                f"composed site {generated_key!r} module door {door} cannot reach site path"
            )
        step: Optional[Tuple[int, int]] = goal
        connector: List[Tuple[int, int]] = []
        while step is not None:
            connector.append(step)
            step = parents[step]
        for position in connector:
            yard_cells.discard(position)
            path_cells.add(position)
            canvas[position[1]][position[0]] = path_char

    for module_door, module_side in module_doors:
        connect_module(module_door, module_side)

    generated_walk = yard_cells | path_cells
    if not yard_cells or not path_cells:
        raise GenerationError(
            f"composed site {generated_key!r} needs both court and visible circulation"
        )
    if not _all_generated_walk_physically_reachable(
        canvas, glyphs, entrances, generated_walk, route_cells
    ):
        raise GenerationError(
            f"composed site {generated_key!r} has disconnected court or roofed bay"
        )
    strict_reachable = _strict_claimed_walk(canvas, glyphs, entrances)
    for y, row in enumerate(canvas):
        for x, char in enumerate(row):
            glyph = glyphs.get(char)
            if glyph is None or glyph.get("Pass") != "adjacent":
                continue
            if not any(
                (x + dx, y + dy) in strict_reachable
                for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
            ):
                raise GenerationError(
                    f"composed site {generated_key!r} strands adjacent-use cell {(x, y)}"
                )
    for position in feature_cells:
        glyph = glyphs[canvas[position[1]][position[0]]]
        if glyph.get("Pass") == "walk":
            accessible = position in strict_reachable
        else:
            accessible = any(
                (position[0] + dx, position[1] + dy) in strict_reachable
                for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
            )
        if not accessible:
            raise GenerationError(
                f"composed site {generated_key!r} strands protected feature {position}"
            )
    for entrance in entrances:
        if _runtime_authored_lane(canvas, entrance) is None:
            raise GenerationError(
                f"composed site {generated_key!r} blocks exact runtime lane from {entrance}"
            )

    composition_bays = _building_component_count(canvas, glyphs)
    expected_bays = DEEPEND_COMPOSITION_BAYS[target_size]
    if composition_bays != expected_bays:
        raise GenerationError(
            f"composed site {generated_key!r} has {composition_bays}/{expected_bays} "
            "readable roofed bays"
        )
    blind_wall_blocks = _solid_wall_block_count(canvas, glyphs)
    if blind_wall_blocks:
        raise GenerationError(
            f"composed site {generated_key!r} contains {blind_wall_blocks} blind 3x3 wall masses"
        )
    span = width if side in {"N", "S"} else height
    depth_span = height if side in {"N", "S"} else width
    programme_regions = len(
        {
            (
                min(2, _oriented(x, y, width, height, side)[0] * 3 // span),
                min(2, _oriented(x, y, width, height, side)[1] * 3 // depth_span),
            )
            for x, y in path_cells
        }
    )
    all_cells = {(x, y) for y in range(height) for x in range(width)}
    partition = transformed_cells | yard_cells | path_cells | route_cells
    if partition != all_cells or any(
        first & second
        for index, first in enumerate((transformed_cells, yard_cells, path_cells, route_cells))
        for second in (transformed_cells, yard_cells, path_cells, route_cells)[index + 1 :]
    ):
        raise GenerationError(f"composed site {generated_key!r} has invalid cell census")
    if records is not None:
        records.append(
            RealizationRecord(
                generated_key,
                source.get("Key", ""),
                context,
                core_x,
                core_y,
                source_width,
                source_height,
                0,
                0,
                width,
                height,
                len(all_cells),
                len(transformed_cells),
                len(structure_cells),
                len(feature_cells),
                len(yard_cells),
                len(path_cells),
                0,
                0,
                programme_regions,
                0,
                composition_bays,
                len(module_doors) + len(entrances),
                blind_wall_blocks,
                len(route_cells),
                0,
                0,
                "",
                False,
                _overlay_fingerprint(policy, context, width, height),
            )
        )

    result = ET.Element(
        "map",
        {
            "Key": generated_key,
            "Width": str(width),
            "Height": str(height),
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
    if context is None or palette is None:
        raise GenerationError("concrete lot realization needs context and palette authority")
    policy = _policy_for(context)
    if policy is None:
        raise GenerationError(
            f"building {context.build_key!r} has no yard policy for {context.category!r}"
        )
    if context.footprint_width or context.footprint_height:
        return _explicit_footprint_site_map(
            source, target_size, facing, generated_key, context, palette, records
        )
    if context.plan_key in DEEPEND_COMPOSED_SITE_PROGRAMMES:
        return _composed_site_map(
            source, target_size, facing, generated_key, context, palette, records
        )
    envelope = _envelope_for(source, target_size, facing, context)
    source_glyphs = _glyphs(source)
    generated_glyphs: List[ET.Element] = []
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
        _visual_blueprint_key(yard_blueprint) == _visual_blueprint_key(path_blueprint)
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

    side = "N" if facing == "heart" else _dominant_frontage_side(source)
    canvas = [[yard_char for _x in range(target_width)] for _y in range(target_height)]
    yard_cells: Set[Tuple[int, int]] = set()
    path_cells: Set[Tuple[int, int]] = set()
    for y in range(target_height):
        for x in range(target_width):
            if _yard_kind(policy, context, side, x, y, target_width, target_height) == "path":
                canvas[y][x] = path_char
                path_cells.add((x, y))
            else:
                yard_cells.add((x, y))

    transformed_cells: Set[Tuple[int, int]] = set()
    structure_cells: Set[Tuple[int, int]] = set()
    feature_cells: Set[Tuple[int, int]] = set()
    boundary_cells: Set[Tuple[int, int]] = set()
    fixture_cells: Set[Tuple[int, int]] = set()
    programme_regions = 0
    station_pairs = 0
    route_cells: Set[Tuple[int, int]] = set()
    intentional_open: Set[Tuple[int, int]] = set()
    inaccessible_open: Set[Tuple[int, int]] = set()
    plain_cells = _plain_source_cells(source, source_glyphs)

    def projected(source_x: int, source_y: int) -> Tuple[int, int]:
        return (
            envelope.x
            + _project_coordinate(source_x, source_width, envelope.width),
            envelope.y
            + _project_coordinate(source_y, source_height, envelope.height),
        )

    def claim_transformed(position: Tuple[int, int], char: str) -> None:
        x, y = position
        canvas[y][x] = char
        yard_cells.discard(position)
        path_cells.discard(position)
        transformed_cells.add(position)

    # Renovate interior surfaces through inverse semantic projection.  Dots stay part of the
    # category site grammar; structural/object/anchor cells provide background but are overlaid
    # exactly once below.
    for local_y in range(envelope.height):
        source_y = _inverse_project_coordinate(local_y, source_height, envelope.height)
        for local_x in range(envelope.width):
            source_x = _inverse_project_coordinate(local_x, source_width, envelope.width)
            raw_char = rows[source_y][source_x]
            background = _background_char(
                raw_char,
                source_x,
                source_y,
                source_glyphs,
                plain_cells,
                generated_glyphs,
            )
            if background is not None:
                claim_transformed((envelope.x + local_x, envelope.y + local_y), background)

    # One-cell structural lines stretch between projected neighboring source cells.  This expands
    # rooms and enclosures without turning walls into nearest-neighbor blocks several tiles thick.
    for source_y, row in enumerate(rows):
        for source_x, char in enumerate(row):
            glyph = source_glyphs.get(char)
            if glyph is None or not glyph.get("Structure"):
                continue
            for neighbor_x, neighbor_y in ((source_x + 1, source_y), (source_x, source_y + 1)):
                if neighbor_x >= source_width or neighbor_y >= source_height:
                    continue
                neighbor_char = rows[neighbor_y][neighbor_x]
                neighbor = source_glyphs.get(neighbor_char)
                if neighbor is None or not neighbor.get("Structure"):
                    continue
                connector = _connector_char(
                    glyph, neighbor, source_glyphs, generated_glyphs
                )
                for position in _line_between(
                    projected(source_x, source_y), projected(neighbor_x, neighbor_y)
                )[1:-1]:
                    claim_transformed(position, connector)
                    structure_cells.add(position)

    projected_features: Dict[Tuple[int, int], Tuple[int, int, str]] = {}
    for source_y, row in enumerate(rows):
        for source_x, char in enumerate(row):
            glyph = source_glyphs.get(char)
            if glyph is None or not (
                glyph.get("Structure") or glyph.get("Object") or glyph.get("Anchors")
            ):
                continue
            position = projected(source_x, source_y)
            previous = projected_features.get(position)
            if previous is not None:
                raise GenerationError(
                    f"map {source.get('Key')!r} features {previous[:2]} and "
                    f"{(source_x, source_y)} collide at projected {position}"
                )
            projected_features[position] = (source_x, source_y, char)
            claim_transformed(position, char)
            if glyph.get("Structure"):
                structure_cells.add(position)
            if glyph.get("Object") or glyph.get("Anchors"):
                feature_cells.add(position)

    source_entrances = _public_entrances(source)
    entrances = [projected(x, y) for x, y in source_entrances]
    frontage_edges: List[Tuple[int, int]] = []
    for source_entrance, entrance in zip(source_entrances, entrances):
        route = _frontage_route(
            entrance,
            _entrance_exit_side(source, source_entrance),
            target_width,
            target_height,
            feature_cells | structure_cells,
            transformed_cells,
        )
        frontage_edges.append(route[-1] if route else entrance)
        for position in route:
            x, y = position
            # Runtime's exact authored-lane contract crosses only unclaimed walk after the
            # claimed threshold. The road pass paints this reserved approach in-world; declaring
            # it as claimed site path here makes the compiled building unroutable.
            canvas[y][x] = "."
            yard_cells.discard(position)
            path_cells.discard(position)
            transformed_cells.discard(position)
            route_cells.add(position)

    def declared_opening(glyph: Optional[ET.Element]) -> bool:
        return bool(
            glyph is not None
            and any(
                anchor.split(":", 1)[0]
                in {"door", "entrance", "exit", "threshold"}
                for anchor in glyph.get("Anchors", "").split(",")
                if anchor
            )
        )

    # Stretching a wall lattice can expose newly widened interior floor between projected posts.
    # Grow a one-cell wall around only those leaks.  This is real renovation of stateless fabric,
    # not blanket padding: openings, routes, objects, and anchored custody remain immutable.
    for _pass in range(target_width * target_height):
        patch: Optional[Tuple[Tuple[int, int], str]] = None
        structural_candidates = [
            cell
            for cell in structure_cells
            if (
                (candidate := source_glyphs.get(canvas[cell[1]][cell[0]])) is not None
                and candidate.get("Structure")
                and not candidate.get("Object")
                and not candidate.get("Anchors")
            )
        ]
        for y in range(target_height):
            if patch is not None:
                break
            for x in range(target_width):
                position = (x, y)
                glyph = source_glyphs.get(canvas[y][x])
                if (
                    glyph is None
                    or glyph.get("Claim") != "building"
                    or glyph.get("Cover") != "walled"
                    or glyph.get("Structure")
                    or declared_opening(glyph)
                ):
                    continue
                for neighbor in (
                    (x - 1, y),
                    (x + 1, y),
                    (x, y - 1),
                    (x, y + 1),
                ):
                    nx, ny = neighbor
                    inside = 0 <= nx < target_width and 0 <= ny < target_height
                    neighbor_glyph = (
                        source_glyphs.get(canvas[ny][nx]) if inside else None
                    )
                    exterior = neighbor_glyph is None or neighbor_glyph.get("Claim") != "building"
                    if neighbor_glyph is not None and neighbor_glyph.get("Cover") != "walled":
                        exterior = not neighbor_glyph.get("Structure") and not declared_opening(
                            neighbor_glyph
                        )
                    if not exterior:
                        continue
                    target = neighbor
                    if (
                        not inside
                        or neighbor in route_cells
                        or neighbor in feature_cells
                    ):
                        target = position
                    if target in feature_cells or target in route_cells:
                        raise GenerationError(
                            f"generated map {generated_key!r} cannot enclose protected "
                            f"walled feature at {position} toward {neighbor}"
                        )
                    if not structural_candidates:
                        raise GenerationError(
                            f"generated map {generated_key!r} has walled floor but no plain "
                            "structural fabric for renovation"
                        )
                    nearest = min(
                        structural_candidates,
                        key=lambda cell: (
                            0
                            if source_glyphs[canvas[cell[1]][cell[0]]].get("Claim")
                            == "building"
                            else 1,
                            0
                            if source_glyphs[canvas[cell[1]][cell[0]]].get("Cover")
                            == "walled"
                            else 1,
                            abs(cell[0] - target[0]) + abs(cell[1] - target[1]),
                            cell[1],
                            cell[0],
                        ),
                    )
                    patch = (target, canvas[nearest[1]][nearest[0]])
                    break
                if patch is not None:
                    break
        if patch is None:
            break
        target, structural_char = patch
        canvas[target[1]][target[0]] = structural_char
        yard_cells.discard(target)
        path_cells.discard(target)
        transformed_cells.add(target)
        structure_cells.add(target)
    else:
        raise GenerationError(
            f"generated map {generated_key!r} enclosure renovation did not converge"
        )

    open_reason = INTENTIONAL_OPEN_REASONS.get(context.build_key, "")
    if open_reason:
        generated_walk = yard_cells | path_cells
        candidates = sorted(
            (
                cell
                for cell in generated_walk
                if _intentional_open_candidate(
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
    boundary_char = _boundary_char(source)
    if boundary_char and allow_generated_boundary:
        generated_walk = yard_cells | path_cells
        campus_programme = _reviewed_spatial_programme(context.plan_key)
        phase = (
            campus_programme.rhythm
            if campus_programme is not None
            else _stable_phase(context)
        )
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

    # A stretched isolated wall post can leave a one-cell phantom pocket where its source cell
    # used to be.  Close only unreachable, featureless pockets with nearest plain structural
    # fabric.  Anchored/object-bearing rooms remain hard failures; custody is never wallpapered.
    current_walk = {
        (x, y)
        for y, row in enumerate(canvas)
        for x, char in enumerate(row)
        if _walkable(char, source_glyphs)
    }
    reachable = _reachable_claimed_walk(
        canvas, source_glyphs, entrances, route_cells
    )
    missing_walk = sorted(current_walk - reachable, key=lambda cell: (cell[1], cell[0]))
    plain_structure = [
        cell
        for cell in structure_cells
        if (
            (glyph := source_glyphs.get(canvas[cell[1]][cell[0]])) is not None
            and glyph.get("Structure")
            and glyph.get("Pass") != "walk"
            and not glyph.get("Object")
            and not glyph.get("Anchors")
        )
    ]
    for cell in missing_walk:
        glyph = source_glyphs.get(canvas[cell[1]][cell[0]])
        if glyph is None or glyph.get("Object") or glyph.get("Anchors"):
            continue  # final access-corridor pass preserves and reconnects protected features
        if not plain_structure:
            raise GenerationError(
                f"generated map {generated_key!r} strands floor at {cell} without lawful "
                "structural fabric for closure"
            )
        nearest = min(
            plain_structure,
            key=lambda candidate: (
                0
                if source_glyphs[canvas[candidate[1]][candidate[0]]].get("Claim")
                == glyph.get("Claim")
                else 1,
                0
                if source_glyphs[canvas[candidate[1]][candidate[0]]].get("Cover")
                == glyph.get("Cover")
                else 1,
                abs(candidate[0] - cell[0]) + abs(candidate[1] - cell[1]),
                candidate[1],
                candidate[0],
            ),
        )
        canvas[cell[1]][cell[0]] = canvas[nearest[1]][nearest[0]]
        yard_cells.discard(cell)
        path_cells.discard(cell)
        transformed_cells.add(cell)
        structure_cells.add(cell)

    # Projection and named clearances may consume more of one surface than another. Restore
    # checker-visible contrast by widening an existing edge of the minority network, never by
    # scattering decorative pixels.
    if context.target_size in {"M", "L", "XL"}:
        minimum_site_cells = 6 if context.target_size == "M" else 2
        reclaimable = sorted(
            (
                cell
                for cell in transformed_cells
                if (
                    (glyph := source_glyphs.get(canvas[cell[1]][cell[0]])) is not None
                    and glyph.get("Claim") == "yard"
                    and glyph.get("Pass") == "walk"
                    and glyph.get("Cover") == "open"
                    and not glyph.get("Structure")
                    and not glyph.get("Object")
                    and not glyph.get("Anchors")
                )
            ),
            key=lambda cell: (
                0 if _outward(cell[0], cell[1], target_width, target_height) else 1,
                min(
                    (
                        abs(cell[0] - entrance[0]) + abs(cell[1] - entrance[1])
                        for entrance in entrances
                    ),
                    default=0,
                ),
                _site_choice_key(
                    context,
                    side,
                    cell,
                    target_width,
                    target_height,
                    "site-reclaim",
                ),
            ),
        )
        while (
            len(yard_cells) + len(path_cells) < minimum_site_cells
            and reclaimable
        ):
            cell = reclaimable.pop(0)
            transformed_cells.discard(cell)
            target = path_cells if len(path_cells) <= len(yard_cells) else yard_cells
            target.add(cell)
            canvas[cell[1]][cell[0]] = path_char if target is path_cells else yard_char

        surface_total = len(yard_cells) + len(path_cells)
        if context.target_size == "M":
            requested_surface_minimum = max(
                3,
                CREED_EXPANSION_FIXTURE_COUNTS.get(context.target_size, 0)
                if context.plan_key.startswith("creed-")
                else 0,
            )
        else:
            requested_surface_minimum = (surface_total + 13) // 14
        required_surface_minimum = min(
            requested_surface_minimum, surface_total // 2
        )

        def surfaces_unbalanced() -> bool:
            return min(len(yard_cells), len(path_cells)) < (
                required_surface_minimum
            )

        for _surface_pass in range(surface_total + 1):
            if not surfaces_unbalanced():
                break
            minority = path_cells if len(path_cells) <= len(yard_cells) else yard_cells
            majority = yard_cells if minority is path_cells else path_cells
            replacement = path_char if minority is path_cells else yard_char
            candidates = [
                cell
                for cell in majority
                if not minority
                or any(
                    (cell[0] + dx, cell[1] + dy) in minority
                    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                )
            ]
            if not candidates:
                # Envelope can divide two exterior courts. Seed a compact threshold/apron in
                # the majority court nearest public access, then subsequent cells widen it.
                candidates = list(majority)
            if not candidates:
                raise GenerationError(
                    f"generated map {generated_key!r} has no site surface left to balance"
                )
            candidates.sort(
                key=lambda cell: (
                    min(
                        (
                            abs(cell[0] - entrance[0])
                            + abs(cell[1] - entrance[1])
                            for entrance in entrances
                        ),
                        default=0,
                    ),
                    _site_choice_key(
                        context,
                        side,
                        cell,
                        target_width,
                        target_height,
                        "surface-balance",
                    ),
                )
            )
            chosen = candidates[0]
            majority.remove(chosen)
            minority.add(chosen)
            canvas[chosen[1]][chosen[0]] = replacement
        else:
            raise GenerationError(
                f"generated map {generated_key!r} site-surface balancing did not converge"
            )

    if context.target_size in {"L", "XL"}:
        yard_visual = _visual_blueprint_key(yard_blueprint)
        path_visual = _visual_blueprint_key(path_blueprint)

        def visual_surfaces() -> Dict[str, Set[Tuple[int, int]]]:
            result: Dict[str, Set[Tuple[int, int]]] = {}
            for y, row in enumerate(canvas):
                for x, char in enumerate(row):
                    glyph = source_glyphs.get(char)
                    if (
                        glyph is None
                        or glyph.get("Claim") != "yard"
                        or glyph.get("Pass") != "walk"
                        or glyph.get("Cover") != "open"
                        or glyph.get("Structure")
                        or glyph.get("Object")
                        or glyph.get("Anchors")
                    ):
                        continue
                    reference = glyph.get("Ground", "")
                    slot = slots.get(reference)
                    concrete = slot.get("Blueprint", "") if slot is not None else reference
                    result.setdefault(_visual_blueprint_key(concrete), set()).add((x, y))
            return result

        for _balance_pass in range(target_width * target_height):
            surfaces = visual_surfaces()
            total = sum(len(cells) for cells in surfaces.values())
            majority_key, majority_cells = max(
                surfaces.items(), key=lambda item: (len(item[1]), item[0])
            )
            minority_cells = set().union(
                *(cells for key, cells in surfaces.items() if key != majority_key)
            )
            if len(minority_cells) * 14 >= total:
                break
            replacement = path_char if majority_key == yard_visual else yard_char
            replacement_visual = (
                path_visual if replacement == path_char else yard_visual
            )
            candidates = [
                cell
                for cell in majority_cells
                if any(
                    (cell[0] + dx, cell[1] + dy) in minority_cells
                    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                )
            ]
            if not candidates:
                candidates = list(majority_cells)
            if not candidates or replacement_visual == majority_key:
                raise GenerationError(
                    f"generated map {generated_key!r} cannot balance concrete visual surfaces"
                )
            candidates.sort(
                key=lambda cell: (
                    min(
                        (
                            abs(cell[0] - entrance[0])
                            + abs(cell[1] - entrance[1])
                            for entrance in entrances
                        ),
                        default=0,
                    ),
                    _site_choice_key(
                        context,
                        side,
                        cell,
                        target_width,
                        target_height,
                        "visual-surface-balance",
                    ),
                )
            )
            chosen = candidates[0]
            canvas[chosen[1]][chosen[0]] = replacement
            if chosen in yard_cells or chosen in path_cells:
                yard_cells.discard(chosen)
                path_cells.discard(chosen)
                (path_cells if replacement == path_char else yard_cells).add(chosen)
        else:
            raise GenerationError(
                f"generated map {generated_key!r} visual-surface balance did not converge"
            )

    if fixture_chars:
        chosen_fixtures = _creed_expansion_fixture_cells(
            context,
            yard_cells,
            path_cells,
            entrances,
            target_width,
            target_height,
            side,
            envelope,
        )
        for index, (x, y) in enumerate(chosen_fixtures):
            canvas[y][x] = fixture_chars[index % len(fixture_chars)]
            fixture_cells.add((x, y))
        programme_regions = len(
            {
                (
                    0
                    if _oriented(x, y, target_width, target_height, side)[0] * 2
                    < (
                        target_width
                        if side in {"N", "S"}
                        else target_height
                    )
                    else 1,
                    min(
                        2,
                        _oriented(x, y, target_width, target_height, side)[1]
                        * 3
                        // (
                            target_height
                            if side in {"N", "S"}
                            else target_width
                        ),
                    ),
                )
                for x, y in chosen_fixtures
            }
        )
        station_pairs = len(chosen_fixtures) // 2

    generated_walk = yard_cells | path_cells
    if policy.key not in NO_PATH_POLICIES:
        if not yard_cells or not path_cells:
            raise GenerationError(
                f"generated map {generated_key!r} policy {policy.key!r} does not retain "
                f"both visible yard and path topology: yard={len(yard_cells)} "
                f"path={len(path_cells)} transformed={len(transformed_cells)}"
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
    def reconnect_protected_walk(
        start: Tuple[int, int], reachable: Set[Tuple[int, int]]
    ) -> None:
        start_glyph = source_glyphs.get(canvas[start[1]][start[0]])
        if start_glyph is None:
            raise GenerationError(
                f"generated map {generated_key!r} cannot resolve protected cell {start}"
            )
        floor_options = [
            (char, glyph)
            for char, glyph in source_glyphs.items()
            if (
                glyph.get("Pass") == "walk"
                and glyph.get("Claim") == start_glyph.get("Claim")
                and glyph.get("Cover") == start_glyph.get("Cover")
                and not glyph.get("Structure")
                and not glyph.get("Object")
                and not glyph.get("Anchors")
            )
        ]
        if floor_options:
            floor_char = min(floor_options, key=lambda item: item[0])[0]
        else:
            attributes = _glyph_attributes_without_custody(
                start_glyph, preserve_structure=False
            )
            attributes["Pass"] = "walk"
            floor_char = _glyph_for_attributes(
                source_glyphs, generated_glyphs, attributes, "i"
            )
        door_options: List[str] = []
        for candidate in list(source_glyphs.values()):
            if (
                not candidate.get("Structure")
                or candidate.get("Pass") != "walk"
                or candidate.get("Object")
            ):
                continue
            if candidate.get("Anchors"):
                door_options.append(
                    _glyph_for_attributes(
                        source_glyphs,
                        generated_glyphs,
                        _glyph_attributes_without_custody(
                            candidate, preserve_structure=True
                        ),
                        "d",
                    )
                )
            else:
                door_options.append(candidate.get("Char", ""))
        door_char = min(door_options) if door_options else ""
        distance: Dict[Tuple[int, int], Tuple[int, int, int]] = {start: (0, 0, 0)}
        parent: Dict[Tuple[int, int], Optional[Tuple[int, int]]] = {start: None}
        queue: List[Tuple[int, int, int, int, int]] = [(0, 0, 0, start[1], start[0])]
        goal: Optional[Tuple[int, int]] = None
        while queue:
            demolition, mismatch, steps, y, x = heapq.heappop(queue)
            position = (x, y)
            if distance.get(position) != (demolition, mismatch, steps):
                continue
            if position != start and position in reachable:
                goal = position
                break
            for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
                neighbor = (x + dx, y + dy)
                nx, ny = neighbor
                if not (0 <= nx < target_width and 0 <= ny < target_height):
                    continue
                if neighbor in route_cells or (
                    neighbor in feature_cells
                    and neighbor != start
                    and neighbor not in reachable
                ):
                    continue
                glyph = source_glyphs.get(canvas[ny][nx])
                structural = bool(glyph is not None and glyph.get("Structure"))
                candidate = (
                    demolition + (4 if structural else 0),
                    mismatch
                    + (
                        0
                        if glyph is not None
                        and glyph.get("Claim") == start_glyph.get("Claim")
                        else 1
                    ),
                    steps + 1,
                )
                if candidate >= distance.get(neighbor, (10**9, 10**9, 10**9)):
                    continue
                distance[neighbor] = candidate
                parent[neighbor] = position
                heapq.heappush(
                    queue,
                    (candidate[0], candidate[1], candidate[2], ny, nx),
                )
        if goal is None:
            raise GenerationError(
                f"generated map {generated_key!r} cannot reconnect protected feature at {start}"
            )
        reverse: List[Tuple[int, int]] = []
        step: Optional[Tuple[int, int]] = goal
        while step is not None and step != start:
            reverse.append(step)
            step = parent[step]
        path = tuple(reversed(reverse))
        for position in path[:-1]:
            glyph = source_glyphs.get(canvas[position[1]][position[0]])
            if glyph is not None and glyph.get("Structure"):
                canvas[position[1]][position[0]] = door_char or floor_char
                if door_char:
                    structure_cells.add(position)
                else:
                    structure_cells.discard(position)
            elif glyph is not None and _walkable(
                canvas[position[1]][position[0]], source_glyphs
            ):
                continue
            else:
                canvas[position[1]][position[0]] = floor_char
                structure_cells.discard(position)
            yard_cells.discard(position)
            path_cells.discard(position)
            transformed_cells.add(position)

    for _closure_pass in range(target_width * target_height * 2 + 1):
        all_walk = {
            (x, y)
            for y, row in enumerate(canvas)
            for x, char in enumerate(row)
            if _walkable(char, source_glyphs)
        }
        reachable = _reachable_claimed_walk(
            canvas, source_glyphs, entrances, route_cells
        )
        missing = sorted(all_walk - reachable, key=lambda cell: (cell[1], cell[0]))
        if not missing:
            break
        cell = missing[0]
        glyph = source_glyphs.get(canvas[cell[1]][cell[0]])
        if glyph is None or glyph.get("Object") or glyph.get("Anchors"):
            reconnect_protected_walk(cell, reachable)
            continue
        candidates = [
            position
            for position in structure_cells
            if (
                (candidate := source_glyphs.get(canvas[position[1]][position[0]]))
                is not None
                and candidate.get("Structure")
                and candidate.get("Pass") != "walk"
                and not candidate.get("Object")
                and not candidate.get("Anchors")
            )
        ]
        if not candidates:
            raise GenerationError(
                f"generated map {generated_key!r} contains inaccessible floor at {cell} "
                "without lawful structural closure"
            )
        nearest = min(
            candidates,
            key=lambda candidate: (
                abs(candidate[0] - cell[0]) + abs(candidate[1] - cell[1]),
                candidate[1],
                candidate[0],
            ),
        )
        canvas[cell[1]][cell[0]] = canvas[nearest[1]][nearest[0]]
        yard_cells.discard(cell)
        path_cells.discard(cell)
        transformed_cells.add(cell)
        structure_cells.add(cell)
    else:
        raise GenerationError(
            f"generated map {generated_key!r} accessibility closure did not converge"
        )

    def access_ok(
        position: Tuple[int, int], glyph: ET.Element, reachable: Set[Tuple[int, int]]
    ) -> bool:
        if glyph.get("Pass") == "walk":
            return position in reachable
        return any(
            (position[0] + dx, position[1] + dy) in reachable
            for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
        )

    for _access_pass in range(target_width * target_height * 2 + 1):
        strict_reachable = _strict_claimed_walk(
            canvas, source_glyphs, entrances
        )
        inaccessible: Optional[Tuple[Tuple[int, int], ET.Element]] = None
        for y, row in enumerate(canvas):
            if inaccessible is not None:
                break
            for x, char in enumerate(row):
                glyph = source_glyphs.get(char)
                if glyph is None or not (
                    glyph.get("Anchors") or glyph.get("Pass") == "adjacent"
                ):
                    continue
                if not access_ok((x, y), glyph, strict_reachable):
                    inaccessible = ((x, y), glyph)
                    break
        if inaccessible is None:
            break
        position, glyph = inaccessible
        if glyph.get("Pass") == "walk":
            reconnect_protected_walk(position, strict_reachable)
            continue
        neighbors = [
            (position[0] + dx, position[1] + dy)
            for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
            if 0 <= position[0] + dx < target_width
            and 0 <= position[1] + dy < target_height
            and (position[0] + dx, position[1] + dy) not in route_cells
            and (position[0] + dx, position[1] + dy) not in feature_cells
        ]
        if not neighbors:
            raise GenerationError(
                f"generated map {generated_key!r} cannot make adjacent feature at "
                f"{position} usable"
            )
        neighbor = min(
            neighbors,
            key=lambda cell: (
                0
                if _walkable(canvas[cell[1]][cell[0]], source_glyphs)
                else 1,
                0
                if (
                    source_glyphs.get(canvas[cell[1]][cell[0]]) is not None
                    and not source_glyphs[canvas[cell[1]][cell[0]]].get("Structure")
                )
                else 1,
                0
                if (
                    source_glyphs.get(canvas[cell[1]][cell[0]]) is not None
                    and source_glyphs[canvas[cell[1]][cell[0]]].get("Claim")
                    == glyph.get("Claim")
                )
                else 1,
                cell[1],
                cell[0],
            ),
        )
        neighbor_glyph = source_glyphs.get(canvas[neighbor[1]][neighbor[0]])
        if not _walkable(canvas[neighbor[1]][neighbor[0]], source_glyphs):
            if neighbor_glyph is not None and neighbor_glyph.get("Structure"):
                doors: List[str] = []
                for candidate in list(source_glyphs.values()):
                    if (
                        not candidate.get("Structure")
                        or candidate.get("Pass") != "walk"
                        or candidate.get("Object")
                    ):
                        continue
                    if candidate.get("Anchors"):
                        doors.append(
                            _glyph_for_attributes(
                                source_glyphs,
                                generated_glyphs,
                                _glyph_attributes_without_custody(
                                    candidate, preserve_structure=True
                                ),
                                "d",
                            )
                        )
                    else:
                        doors.append(candidate.get("Char", ""))
                if not doors:
                    attributes = _glyph_attributes_without_custody(
                        neighbor_glyph, preserve_structure=False
                    )
                    attributes["Claim"] = glyph.get("Claim", "yard")
                    attributes["Cover"] = glyph.get("Cover", "open")
                    attributes["Pass"] = "walk"
                    canvas[neighbor[1]][neighbor[0]] = _glyph_for_attributes(
                        source_glyphs, generated_glyphs, attributes, "i"
                    )
                    structure_cells.discard(neighbor)
                else:
                    canvas[neighbor[1]][neighbor[0]] = min(doors)
                    structure_cells.add(neighbor)
            else:
                floors = [
                    char
                    for char, candidate in source_glyphs.items()
                    if candidate.get("Pass") == "walk"
                    and candidate.get("Claim") == glyph.get("Claim")
                    and candidate.get("Cover") == glyph.get("Cover")
                    and not candidate.get("Structure")
                    and not candidate.get("Object")
                    and not candidate.get("Anchors")
                ]
                if floors:
                    canvas[neighbor[1]][neighbor[0]] = min(floors)
                else:
                    attributes = _glyph_attributes_without_custody(
                        glyph, preserve_structure=False
                    )
                    attributes["Pass"] = "walk"
                    canvas[neighbor[1]][neighbor[0]] = _glyph_for_attributes(
                        source_glyphs, generated_glyphs, attributes, "i"
                    )
                structure_cells.discard(neighbor)
            yard_cells.discard(neighbor)
            path_cells.discard(neighbor)
            transformed_cells.add(neighbor)
        reconnect_protected_walk(neighbor, strict_reachable)
    else:
        raise GenerationError(
            f"generated map {generated_key!r} protected-access renovation did not converge"
        )

    if context.plan_key.startswith("creed-") and facing == "road":
        threshold = _campus_frontage_threshold(
            generated_key,
            canvas,
            source_glyphs,
            generated_glyphs,
            side,
            path_char,
            yard_cells,
            path_cells,
            route_cells
            | feature_cells
            | fixture_cells
            | structure_cells
            | boundary_cells
            | intentional_open
            | inaccessible_open,
            frontage_edges,
        )
        entrances.append(threshold)
        feature_cells.add(threshold)

    if context.plan_key in SITE_EXPANSION_PROGRAMMES:
        span = target_width if side in {"N", "S"} else target_height
        depth_span = target_height if side in {"N", "S"} else target_width
        site_regions = {
            (
                min(
                    2,
                    _oriented(x, y, target_width, target_height, side)[0]
                    * 3
                    // span,
                ),
                min(
                    2,
                    _oriented(x, y, target_width, target_height, side)[1]
                    * 3
                    // depth_span,
                ),
            )
            for x, y in path_cells
        }
        if context.target_size in {"L", "XL"} and (
            len({region[0] for region in site_regions}) < 2
            or len({region[1] for region in site_regions}) < 2
        ):
            raise GenerationError(
                f"generated renovation {generated_key!r} has no two-axis court distribution: "
                f"regions={sorted(site_regions)}"
            )
        programme_regions = len(site_regions)

    for entrance in entrances:
        if _runtime_authored_lane(canvas, entrance) is None:
            raise GenerationError(
                f"generated map {generated_key!r} decoration blocks exact runtime lane "
                f"from {entrance[0]},{entrance[1]}"
            )

    if records is not None:
        all_cells = {
            (x, y) for y in range(target_height) for x in range(target_width)
        }
        partition = (
            transformed_cells
            | yard_cells
            | path_cells
            | boundary_cells
            | route_cells
            | intentional_open
            | inaccessible_open
        )
        if partition != all_cells:
            missing = sorted(all_cells - partition)
            overlap = sorted(
                (transformed_cells & (yard_cells | path_cells | route_cells))
                | (yard_cells & path_cells)
            )
            raise GenerationError(
                f"generated map {generated_key!r} has invalid site census: "
                f"missing={missing[:4]} overlap={overlap[:4]}"
            )
        records.append(
            RealizationRecord(
                generated_key,
                source.get("Key", ""),
                context,
                envelope.x,
                envelope.y,
                envelope.width,
                envelope.height,
                0,
                0,
                target_width,
                target_height,
                len(all_cells),
                len(transformed_cells),
                len(structure_cells),
                len(feature_cells),
                len(yard_cells),
                len(path_cells),
                len(boundary_cells),
                len(fixture_cells),
                programme_regions,
                station_pairs,
                0,
                0,
                0,
                len(route_cells),
                len(intentional_open),
                len(inaccessible_open),
                open_reason,
                False,
                _overlay_fingerprint(
                    policy,
                    context,
                    target_width,
                    target_height,
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


LEGACY_LOT_DIMENSIONS: Dict[Tuple[int, int], str] = {
    (5, 4): "S",
    (12, 9): "L",
    (20, 14): "XL",
}


def _stretched_authored_map(source: ET.Element, target_size: str) -> ET.Element:
    """Renovate a legacy envelope when the richer exterior grammar is too tight.

    This is a semantic grid stretch, not row/column padding: stateless surfaces and structural
    lines grow through inverse projection, while every object and anchor is overlaid exactly once
    at its monotone projected coordinate.  It preserves deliberately compact S compositions and
    unusual authored frontage whose four-row depth leaves no room for a generated apron.
    """

    source_width = int(source.get("Width", "0"))
    source_height = int(source.get("Height", "0"))
    target_width, target_height = LOT_DIMENSIONS[target_size]
    rows = [row.get("Cells", "") for row in source.findall("row")]
    if len(rows) != source_height or any(len(row) != source_width for row in rows):
        raise GenerationError(f"map {source.get('Key')!r} has malformed source rows")
    glyphs = _glyphs(source)
    generated_glyphs: List[ET.Element] = []
    plain_cells = _plain_source_cells(source, glyphs)
    canvas: List[List[str]] = []
    for target_y in range(target_height):
        source_y = _inverse_project_coordinate(
            target_y, source_height, target_height
        )
        row: List[str] = []
        for target_x in range(target_width):
            source_x = _inverse_project_coordinate(
                target_x, source_width, target_width
            )
            char = rows[source_y][source_x]
            glyph = glyphs.get(char)
            if glyph is not None and (glyph.get("Object") or glyph.get("Anchors")):
                background = _background_char(
                    char,
                    source_x,
                    source_y,
                    glyphs,
                    plain_cells,
                    generated_glyphs,
                )
                row.append(background or ".")
            else:
                row.append(char)
        canvas.append(row)
    for source_y, source_row in enumerate(rows):
        for source_x, char in enumerate(source_row):
            glyph = glyphs.get(char)
            if glyph is None or not (glyph.get("Object") or glyph.get("Anchors")):
                continue
            target_x = _project_coordinate(source_x, source_width, target_width)
            target_y = _project_coordinate(source_y, source_height, target_height)
            canvas[target_y][target_x] = char

    result = ET.Element(
        "map",
        {
            "Key": source.get("Key", ""),
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


def _repair_normalized_authored_map(source: ET.Element) -> bool:
    """Close unreachable stateless pockets and bare perimeter leaks after envelope migration."""

    glyphs = _glyphs(source)
    canvas = [list(row.get("Cells", "")) for row in source.findall("row")]
    if not canvas:
        return False
    width = len(canvas[0])
    height = len(canvas)
    entrances = _public_entrances(source)
    changed = False

    def blocked_for(glyph: ET.Element, *, exclude_walk: bool = True) -> str:
        candidates = [
            candidate
            for candidate in glyphs.values()
            if candidate.get("Structure")
            and not candidate.get("Object")
            and not candidate.get("Anchors")
            and (not exclude_walk or candidate.get("Pass") != "walk")
        ]
        if not candidates:
            raise GenerationError(
                f"map {source.get('Key')!r} needs structural closure but has no plain fabric"
            )
        return min(
            candidates,
            key=lambda candidate: (
                0 if candidate.get("Claim") == glyph.get("Claim") else 1,
                0 if candidate.get("Cover") == glyph.get("Cover") else 1,
                0 if candidate.get("Ground") == glyph.get("Ground") else 1,
                candidate.get("Char", ""),
            ),
        ).get("Char", "")

    # Checker/runtime connectivity begins only at public entrances.  Legacy layouts sometimes
    # treated any lot-edge floor as an independent start, leaving ornamental side strips isolated
    # after projection.  Close those stateless pockets into the nearest authored boundary fabric.
    for _pass in range(width * height + 1):
        reachable = _strict_claimed_walk(canvas, glyphs, entrances)
        missing = sorted(
            (
                (x, y)
                for y, row in enumerate(canvas)
                for x, char in enumerate(row)
                if _walkable(char, glyphs) and (x, y) not in reachable
            ),
            key=lambda cell: (cell[1], cell[0]),
        )
        if not missing:
            break
        progress = False
        for x, y in missing:
            glyph = glyphs.get(canvas[y][x])
            if glyph is None or glyph.get("Object") or glyph.get("Anchors"):
                continue
            canvas[y][x] = blocked_for(glyph)
            progress = True
            changed = True
        if not progress:
            protected = missing[0]
            raise GenerationError(
                f"map {source.get('Key')!r} strands protected walk cell {protected}"
            )
    else:
        raise GenerationError(
            f"map {source.get('Key')!r} connectivity repair did not converge"
        )

    # A projected roof may end one cell deeper than its old wall line. Bare walled floor on the
    # envelope is never a doorway; turn it into the map's own wall material. Then close any plain
    # door that no longer joins two walk cells after the repair above.
    for y, row in enumerate(canvas):
        for x, char in enumerate(row):
            glyph = glyphs.get(char)
            if (
                glyph is not None
                and _outward(x, y, width, height)
                and glyph.get("Claim") == "building"
                and glyph.get("Cover") == "walled"
                and glyph.get("Pass") == "walk"
                and not glyph.get("Structure")
                and not glyph.get("Object")
                and not glyph.get("Anchors")
            ):
                canvas[y][x] = blocked_for(glyph)
                changed = True
    for _pass in range(width * height + 1):
        progress = False
        for y, row in enumerate(canvas):
            for x, char in enumerate(row):
                glyph = glyphs.get(char)
                if (
                    glyph is None
                    or not glyph.get("Structure")
                    or glyph.get("Pass") != "walk"
                    or glyph.get("Object")
                    or glyph.get("Anchors")
                ):
                    continue
                walk_neighbors = sum(
                    _walkable(canvas[y + dy][x + dx], glyphs)
                    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                    if 0 <= x + dx < width and 0 <= y + dy < height
                )
                if walk_neighbors >= 2:
                    continue
                canvas[y][x] = blocked_for(glyph)
                progress = True
                changed = True
        if not progress:
            break
    if changed:
        for row, cells in zip(source.findall("row"), canvas):
            row.set("Cells", "".join(cells))
    return changed


def normalize_authored_sources(
    repository: Path, source_names: Sequence[str]
) -> Tuple[int, int]:
    """Rewrite named legacy catalogues to canonical envelopes without blank padding."""

    roots = list(_source_roots(repository))
    buildings = _buildings(repository)
    upgrade_families = _upgrade_families(buildings)
    palettes: Dict[str, ET.Element] = {}
    uses: Dict[str, List[RealizationContext]] = {}
    for _path, root in roots:
        for palette in root.findall("palette"):
            palettes[palette.get("Key", "")] = palette
        for plan in root.findall("plan"):
            for binding in plan.findall("binding"):
                for tier in binding.findall("tier"):
                    build_key = tier.get("BuildKey", "")
                    building = buildings.get(build_key)
                    if building is None:
                        raise GenerationError(
                            f"tier {tier.get('Key')!r} has unknown BuildKey {build_key!r}"
                        )
                    pairs = [(tier.get("Map", ""), tier.get("Palette", ""))]
                    pairs.extend(
                        (
                            variant.get("Map", ""),
                            variant.get("Palette") or tier.get("Palette", ""),
                        )
                        for variant in tier.findall("variant")
                        if variant.get("Map")
                    )
                    for map_key, palette_key in pairs:
                        if not map_key:
                            continue
                        footprint_width, footprint_height = _declared_footprint(
                            building
                        )
                        uses.setdefault(map_key, []).append(
                            RealizationContext(
                                plan.get("Key", ""),
                                binding.get("Key", ""),
                                build_key,
                                upgrade_families[build_key],
                                binding.get("Type", ""),
                                binding.get("Size", ""),
                                binding.get("Size", ""),
                                binding.get("Facing", ""),
                                palette_key,
                                building.get("Open", "").strip().lower() == "yes",
                                frozenset(),
                                footprint_width,
                                footprint_height,
                            )
                        )

    architecture_dir = (repository / "Architecture").resolve()
    rewritten_maps = 0
    rewritten_files = 0
    pending_writes: List[Tuple[Path, str, int]] = []
    for source_name in source_names:
        path = (architecture_dir / source_name).resolve()
        if path.parent != architecture_dir or path.name == OUTPUT_NAME:
            raise GenerationError(f"refusing non-source architecture path {source_name!r}")
        root = ET.parse(path).getroot()
        text = path.read_text(encoding="utf-8")
        replacements: List[Tuple[str, ET.Element]] = []
        for source in root.findall("map"):
            dimensions = (int(source.get("Width", "0")), int(source.get("Height", "0")))
            target_size = LEGACY_LOT_DIMENSIONS.get(dimensions)
            if target_size is None:
                normalized = copy.deepcopy(source)
            else:
                contexts = uses.get(source.get("Key", ""), [])
                if not contexts:
                    raise GenerationError(
                        f"legacy map {source.get('Key')!r} has no tier/variant context"
                    )
                context = sorted(
                    contexts,
                    key=lambda item: (
                        item.plan_key,
                        item.binding_key,
                        item.build_key,
                        item.palette_key,
                    ),
                )[0]
                context = RealizationContext(
                    context.plan_key,
                    context.binding_key,
                    context.build_key,
                    context.upgrade_family,
                    context.category,
                    context.source_size,
                    target_size,
                    context.facing,
                    context.palette_key,
                    context.open_design,
                    context.reserved_route_cells,
                    context.footprint_width,
                    context.footprint_height,
                )
                palette = palettes.get(context.palette_key)
                if palette is None:
                    raise GenerationError(
                        f"legacy map {source.get('Key')!r} has unknown palette "
                        f"{context.palette_key!r}"
                    )
                try:
                    normalized = _expanded_map(
                        source,
                        target_size,
                        context.facing,
                        source.get("Key", ""),
                        context,
                        palette,
                        [],
                    )
                except GenerationError:
                    normalized = _stretched_authored_map(source, target_size)
            repaired = _repair_normalized_authored_map(normalized)
            if target_size is not None or repaired:
                replacements.append((source.get("Key", ""), normalized))
        for key, normalized in replacements:
            normalized.tail = None
            ET.indent(normalized, space="  ", level=1)
            fragment = "  " + ET.tostring(
                normalized, encoding="unicode", short_empty_elements=True
            ).rstrip()
            pattern = re.compile(
                rf'^  <map Key="{re.escape(key)}"(?:\s|>).*?^  </map>',
                re.MULTILINE | re.DOTALL,
            )
            text, count = pattern.subn(fragment, text, count=1)
            if count != 1:
                raise GenerationError(
                    f"could not replace exact source map fragment {key!r} in {path.name}"
                )
        if replacements:
            validated = ET.fromstring(text)
            if validated.tag != "KingdomArchitectures" or validated.get("Schema") != "1":
                raise GenerationError(
                    f"normalized catalogue {path.name!r} lost its schema root"
                )
            pending_writes.append((path, text, len(replacements)))
    # Validate every requested catalogue before mutating any of them. A bad final file cannot
    # otherwise leave the earlier files half-migrated.
    for path, text, replacement_count in pending_writes:
        path.write_text(text, encoding="utf-8")
        rewritten_files += 1
        rewritten_maps += replacement_count
    return rewritten_files, rewritten_maps


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
                or previous.footprint_width != context.footprint_width
                or previous.footprint_height != context.footprint_height
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
        program = _expansion_program(context.plan_key)
        if program is None:
            raise GenerationError(
                f"generated map {key!r} has no explicit semantic expansion programme"
            )
        source_area = int(source.get("Width", "0")) * int(source.get("Height", "0"))
        if program == "creed-practice-campus":
            expected_fixtures = CREED_EXPANSION_FIXTURE_COUNTS[target_size]
            expected_regions = {"M": 2, "L": 4, "XL": 5}[target_size]
            if record.fixture_cells != expected_fixtures:
                raise GenerationError(
                    f"generated map {key!r} has {record.fixture_cells} semantic fixtures; "
                    f"{program} {target_size} requires exactly {expected_fixtures}"
                )
            if (
                record.programme_regions < expected_regions
                or record.station_pairs != expected_fixtures // 2
            ):
                raise GenerationError(
                    f"generated map {key!r} pads rather than composing its practice campus: "
                    f"regions={record.programme_regions}/{expected_regions} "
                    f"pairs={record.station_pairs}/{expected_fixtures // 2}"
                )
            new_site_cells = record.site_cells - source_area
            if new_site_cells > record.fixture_cells * MAX_NEW_SITE_CELLS_PER_SEMANTIC_FIXTURE:
                raise GenerationError(
                    f"generated map {key!r} spreads {record.fixture_cells} semantic fixtures "
                    f"across {new_site_cells} new cells; maximum is "
                    f"{MAX_NEW_SITE_CELLS_PER_SEMANTIC_FIXTURE} cells per fixture"
                )
        else:
            expected_regions = {"M": 2, "L": 3, "XL": 3}[target_size]
            if record.fixture_cells != 0 or record.station_pairs != 0:
                raise GenerationError(
                    f"generated renovation {key!r} cloned unpriced semantic fixtures"
                )
            source_glyphs = _glyphs(source)
            source_fabric = sum(
                source_glyphs.get(char) is not None
                and source_glyphs[char].get("Claim") == "building"
                for row in source.findall("row")
                for char in row.get("Cells", "")
            )
            if context.footprint_width:
                if (
                    record.footprint_width != context.footprint_width
                    or record.footprint_height != context.footprint_height
                    or record.composition_bays != 1
                    or record.composition_thresholds < 1
                    or record.transformed_cells < (
                        context.footprint_width * context.footprint_height
                    )
                ):
                    raise GenerationError(
                        f"generated explicit footprint {key!r} fails compact-shelter law: "
                        f"scope={record.footprint_width}x{record.footprint_height} "
                        f"declared={context.footprint_width}x{context.footprint_height} "
                        f"bays={record.composition_bays} "
                        f"thresholds={record.composition_thresholds}"
                    )
            elif record.transformed_cells <= source_fabric:
                raise GenerationError(
                    f"generated renovation {key!r} did not add usable fabric"
                )
            if context.footprint_width:
                pass
            elif context.plan_key in DEEPEND_COMPOSED_SITE_PROGRAMMES:
                expected_bays = DEEPEND_COMPOSITION_BAYS[target_size]
                if (
                    record.envelope_width != int(source.get("Width", "0"))
                    or record.envelope_height != int(source.get("Height", "0"))
                    or record.composition_bays != expected_bays
                    or record.composition_thresholds < expected_bays
                    or record.blind_wall_blocks != 0
                ):
                    raise GenerationError(
                        f"generated renovation {key!r} fails composed-campus taste law: "
                        f"core={record.envelope_width}x{record.envelope_height} "
                        f"bays={record.composition_bays}/{expected_bays} "
                        f"thresholds={record.composition_thresholds}/{expected_bays} "
                        f"blind-walls={record.blind_wall_blocks}"
                    )
            elif (
                record.envelope_width <= int(source.get("Width", "0"))
                and record.envelope_height <= int(source.get("Height", "0"))
            ):
                raise GenerationError(
                    f"generated renovation {key!r} did not transform its building envelope"
                )
            if record.programme_regions < expected_regions:
                raise GenerationError(
                    f"generated renovation {key!r} pads rather than composing its court: "
                    f"regions={record.programme_regions}/{expected_regions}"
                )
        policy = _policy_for(context) or YardPolicy("held", "separate hosted redesign", 1)
        envelope_policy = CATEGORY_ENVELOPE_POLICIES[context.category]
        campus_programme = _reviewed_spatial_programme(context.plan_key)
        composition_facts = (
            f"bays={record.composition_bays}; "
            f"thresholds={record.composition_thresholds}; "
            f"blind-wall-blocks={record.blind_wall_blocks}; "
            if context.plan_key in DEEPEND_COMPOSED_SITE_PROGRAMMES
            else ""
        )
        footprint_facts = (
            f"footprint={record.footprint_x},{record.footprint_y},"
            f"{record.footprint_width}x{record.footprint_height}; "
            if context.footprint_width
            else ""
        )
        comment = (
            f" realization source={source_key}; design={context.build_key}; "
            f"grammar={envelope_policy.key}; envelope="
            f"{record.envelope_width}x{record.envelope_height}@"
            f"{record.envelope_x},{record.envelope_y}; "
            f"transformed={record.transformed_cells}; "
            f"structure={record.structure_cells}; features={record.feature_cells}; "
            f"site-policy={policy.key}; site-reason={policy.rationale}; "
            f"yard={record.yard_cells}; path={record.path_cells}; "
            f"boundary={record.boundary_cells}; "
            f"fixtures={record.fixture_cells}; "
            f"campus={campus_programme.key}; formation="
            f"{campus_programme.fixture_pattern}; "
            f"regions={record.programme_regions}; pairs={record.station_pairs}; "
            f"{footprint_facts}"
            f"{composition_facts}"
            f"route={record.route_cells}; "
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
        footprint_width, footprint_height = _declared_footprint(building)
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
            footprint_width,
            footprint_height,
        )

    for _path, root in roots:
        for source_plan in root.findall("plan"):
            source_plan_key = source_plan.get("Key", "")
            authored_bindings = source_plan.findall("binding")
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
                expansion_program = _expansion_program(source_plan_key)
                if expansion_program is None:
                    # Exact authored bindings remain available at their declared size. A larger
                    # lot is offered only when its spatial programme adds real category use.
                    continue
                for target_size in LOT_ORDER[source_index + 1 :]:
                    authored_targets = []
                    for candidate in authored_bindings:
                        if (
                            candidate is source_binding
                            or candidate.get("Size", "") != target_size
                            or candidate.get("Type", "")
                            != source_binding.get("Type", "")
                            or candidate.get("Facing", "") != facing
                        ):
                            continue
                        candidate_builds = {
                            tier.get("BuildKey", "")
                            for tier in candidate.findall("tier")
                        }
                        if build_keys <= candidate_builds:
                            authored_targets.append(candidate)
                    if len(authored_targets) > 1:
                        raise GenerationError(
                            f"plan {source_plan_key!r} has multiple authored {target_size} "
                            f"bindings covering {sorted(build_keys)}"
                        )
                    if authored_targets:
                        # An explicitly authored larger binding is the semantic authority for
                        # this lineage.  It may renovate, expand, or replace the smaller fabric;
                        # synthesizing a second padded copy would both erase that decision and
                        # violate exact typed-lot coverage.
                        continue
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
                        if source_tier.get("Transition") != tier.get("Transition"):
                            raise GenerationError(
                                f"authored Transition was not preserved for tier "
                                f"{source_tier.get('Key')!r}"
                            )
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
        "     Exact maps project semantic source authority through explicit useful spatial programmes.\n"
        "     Paid sanctums retain custody; reviewed renovations add named courts and rooms.\n"
        "     Programme-region gates reject cosmetic path fill and fixture scatter.\n"
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
    mode.add_argument(
        "--normalize-authored",
        nargs="+",
        metavar="ARCHITECTURE_XML",
        help="renovate named legacy authored catalogues to canonical lot envelopes",
    )
    arguments = parser.parse_args(argv)
    repository = arguments.repo_root.resolve()
    target = repository / "Architecture" / OUTPUT_NAME
    if arguments.normalize_authored:
        try:
            file_count, map_count = normalize_authored_sources(
                repository, arguments.normalize_authored
            )
        except (ET.ParseError, OSError, ValueError, GenerationError) as error:
            print(f"authored lot normalization failed: {error}", file=sys.stderr)
            return 1
        print(
            f"normalized {map_count} maps across {file_count} authored catalogues"
        )
        return 0
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
    fixtures = sum(record.fixture_cells for record in result.records)
    regions = sum(record.programme_regions for record in result.records)
    pairs = sum(record.station_pairs for record in result.records)
    transformed = sum(record.transformed_cells for record in result.records)
    census = (
        f"{map_count} maps, {plan_count} bindings, {tier_count} tiers, "
        f"{transformed} transformed fabric cells, {decorated} exterior site facts, "
        f"{fixtures} creed fixtures in {pairs} pairs across {regions} programme regions"
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
