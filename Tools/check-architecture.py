#!/usr/bin/env python3
"""Independent static gate for Thousand And First authored architecture.

The checker is read-only unless --output-dir names an existing, empty directory.  In that
case it writes deterministic ASCII goldens for every compass pose there and nowhere else.  It
deliberately does not import game or mod runtime code: this is an external check over the shipped
XML contract.
"""

from __future__ import annotations

import argparse
import html
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, List, Mapping, Optional, Sequence, Set, Tuple


SCHEMA_VERSION = "1"
ARCHITECTURE_ROOT = "KingdomArchitectures"
BUILDING_ROOT = "kingdombuildings"

LOT_DIMENSIONS: Mapping[str, Tuple[int, int]] = {
    "S": (5, 4),
    "M": (8, 6),
    "L": (12, 9),
    "XL": (20, 14),
}
LOT_ORDER = tuple(LOT_DIMENSIONS)
TECH_ORDER: Mapping[str, int] = {
    "hands": 0,
    "salvage": 1,
    "workshop": 2,
    "foundry": 3,
    "arclight": 4,
}
MATERIAL_ALIASES: Mapping[str, str] = {
    "mud": "mud",
    "brush": "brush",
    "canvas": "brush",
    "timber": "timber",
    "stone": "stone",
    "marble": "marble",
    "scrap": "scrap",
    "scrapmetal": "scrap",
    "shapedtimber": "shapedtimber",
    "sawntimber": "shapedtimber",
    "shapedstone": "shapedstone",
    "dressedstone": "shapedstone",
    "workedmetal": "workedmetal",
}
STAGE_ORDER: Mapping[str, int] = {
    "camp": 0,
    "steading": 1,
    "village": 2,
    "town": 3,
    "city": 4,
}
COVERS = {"open", "soft", "walled", "natural"}
CLAIMS = {"building", "yard"}
PASS_MODES = {"walk", "blocked", "adjacent"}
YES_NO = {"yes", "no"}

MAX_KEY_CHARS = 128
MAX_BLUEPRINT_CHARS = 256
MAX_SELECTOR_CHARS = 256
MAX_SELECTOR_TOKENS = 16
MAX_PALETTE_SLOTS = 128
MAX_GLYPHS = 96
MAX_MAP_AREA = 280
MAX_PLACEMENTS = 512
MAX_ANCHORS = 64
MAX_BINDINGS_PER_PLAN = 16
MAX_TIERS_PER_BINDING = 16
MAX_VARIANTS_PER_TIER = 32
MAX_REQUIREMENTS_PER_TIER = 32
MAX_SNAPSHOT_PAYLOAD_BYTES = 8192
MAX_SNAPSHOT_CHARS = 11264
SNAPSHOT_TEXT_OVERHEAD = len("a2||") + 64  # version, separators, SHA-256

# External-input bounds.  The schema bounds above remain the product contract; these stop a
# malformed checkout or caller path from turning the checker itself into an unbounded parser.
MAX_XML_FILES = 256
MAX_ARCHITECTURE_RECORDS = 4096
MAX_XML_BYTES = 8 * 1024 * 1024
MAX_BLUEPRINT_FILES = 256
MAX_BLUEPRINTS = 65536
MAX_GOLDENS = 8192
MAX_GOLDEN_BYTES = 64 * 1024 * 1024

KEY_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:+-]*$")
ANCHOR_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:+-]*$")
SELECTOR_ATTRIBUTES = (
    "Styles",
    "Creeds",
    "Cultures",
    "Species",
    "Genotypes",
    "Bodies",
    "Terrains",
    "Strata",
    "MinStage",
    "MaxStage",
    "MinTech",
    "MaxTech",
)

# A name that merely looks like stairs is not travel. Authored delves own exactly one wrapper Down;
# runtime pairs its Up in the already-built zone below. Raw vanilla endpoints and same-map pairs
# are cosmetic evidence: they do not carry the durable link ownership contract.
VERTICAL_BLUEPRINT_PAIRS: Mapping[str, Tuple[str, str]] = {
    "r_KingdomDelveUp": ("up", "r_KingdomDelveDown"),
    "r_KingdomDelveDown": ("down", "r_KingdomDelveUp"),
}
RAW_VERTICAL_BLUEPRINTS = ("StairsUp", "StairsDown")
VERTICAL_ROLE_TERMS = ("vertical", "stair", "travel", "elevator", "lift")
POSES = ("north", "east", "south", "west")
HEART_BUILD_KEYS = ("heartbasin", "heartwaterstone", "heartmoot", "heartcourt")

# These vanilla records are useful population content, not stable authored-map components. Their
# inheritance can roll Animated/RandomTile builders or leaves the fixture takeable. Shipped maps
# use local vanilla-art wrappers that remove chance and freeze empty/installed state. Keeping the
# raw names here makes a future content merge fail before a wall, door, bed, or basin quietly
# becomes a creature, rerolls, or walks away.
UNSAFE_AUTHORED_BLUEPRINTS: Mapping[str, str] = {
    "Wall": "base wall can roll Animated",
    "BaseWallMud": "wall can roll Animated",
    "BrickWall": "wall can roll Animated",
    "BrinestalkWall": "wall can roll Animated",
    "CanvasWall": "wall can roll Animated",
    "Concrete": "wall can roll Animated",
    "Limestone": "wall can roll Animated",
    "LowSandstoneWall": "wall can roll Animated and is non-solid despite blocked-map use",
    "MetalWall": "wall can roll Animated and tech mods",
    "MushroomWall-White": "wall can roll Animated and creation-time colours",
    "Petal-Strewn WoodWall": "wall can roll Animated and creation-time colours",
    "RustedMetalWall": "wall can roll Animated and tech mods",
    "StoneHalfWall": "wall can roll Animated",
    "WornWoodWall": "wall can roll Animated",
    "Door": "door can roll Animated",
    "Windowed Door": "door can roll Animated",
    "Striped Door": "door can roll Animated",
    "Metal Door": "door can roll Animated",
    "Security Door": "door can roll Animated",
    "Brinestalk Gate": "gate inherits the Animated door builder",
    "Bed": "bed can roll Animated",
    "Bedroll": "bedroll is takeable",
    "Chair": "chair can roll Animated",
    "Bench": "bench can roll Animated",
    "Floor Cushion": "cushion rolls Animated and RandomTile",
    "Low Table": "table can roll Animated",
    "Bookshelf": "bookshelf rolls RandomTile",
    "Catchbasin": "basin is takeable",
    "Woven Basket": "raw shared container does not freeze empty architecture authority",
}


@dataclass
class BlueprintRecord:
    """The two pieces of ObjectBlueprint inheritance needed to prove map movement truth."""

    name: str
    parent: str = ""
    solid: Optional[bool] = None
    door: Optional[bool] = None


@dataclass(frozen=True)
class BlueprintShape:
    solid: Optional[bool]
    door: Optional[bool]


@dataclass(frozen=True, order=True)
class Issue:
    location: str
    code: str
    message: str

    def render(self) -> str:
        return f"ERROR [{self.code}] {self.location}: {self.message}"


@dataclass(frozen=True, order=True)
class Notice:
    location: str
    code: str
    message: str

    def render(self) -> str:
        return f"WARN [{self.code}] {self.location}: {self.message}"


@dataclass
class Building:
    key: str
    attributes: Dict[str, str]
    location: str

    @property
    def plot(self) -> str:
        return self.attributes.get("Plot", "").strip()

    @property
    def category(self) -> str:
        return self.attributes.get("Category", "").strip()

    @property
    def blueprint(self) -> str:
        return self.attributes.get("Blueprint", "").strip()


@dataclass
class Slot:
    key: str
    blueprint: str
    role: str
    material: str
    min_tech: str
    knowledge: str
    power: str
    natural: str
    location: str


@dataclass
class Palette:
    key: str
    slots: Dict[str, Slot]
    location: str


@dataclass
class Glyph:
    char: str
    ground: str
    structure: str
    object: str
    claim: str
    pass_mode: str
    cover: str
    anchors: Tuple[str, ...]
    stateful: bool
    location: str

    def layers(self) -> Tuple[Tuple[str, str], ...]:
        return tuple(
            (name, value)
            for name, value in (
                ("Ground", self.ground),
                ("Structure", self.structure),
                ("Object", self.object),
            )
            if value
        )


@dataclass
class ArchitectureMap:
    key: str
    width: int
    height: int
    default_cover: str
    glyphs: Dict[str, Glyph]
    rows: Tuple[str, ...]
    location: str

    def glyph_at(self, x: int, y: int) -> Optional[Glyph]:
        char = self.rows[y][x]
        return None if char == "." else self.glyphs.get(char)


@dataclass
class Requirement:
    role: str
    minimum: int
    maximum: int
    location: str


@dataclass
class Variant:
    key: str
    priority: int
    selectors: Dict[str, str]
    map_key: str
    palette_key: str
    location: str

    @property
    def fallback(self) -> bool:
        return all(not self.selectors.get(name, "").strip() for name in SELECTOR_ATTRIBUTES)


@dataclass
class Tier:
    key: str
    build_key: str
    level: int
    map_key: str
    palette_key: str
    requirements: List[Requirement]
    variants: List[Variant]
    location: str
    binding: "Binding" = field(init=False, repr=False)


@dataclass
class Binding:
    key: str
    type_key: str
    size: str
    facing: str
    tiers: List[Tier]
    location: str
    plan: "Plan" = field(init=False, repr=False)


@dataclass
class Plan:
    key: str
    bindings: List[Binding]
    location: str


@dataclass
class ArchitectureModel:
    palettes: Dict[str, Palette] = field(default_factory=dict)
    maps: Dict[str, ArchitectureMap] = field(default_factory=dict)
    plans: Dict[str, Plan] = field(default_factory=dict)

    @property
    def bindings(self) -> List[Binding]:
        return [binding for plan in self.plans.values() for binding in plan.bindings]

    @property
    def tiers(self) -> List[Tier]:
        return [tier for binding in self.bindings for tier in binding.tiers]

    @property
    def variants(self) -> List[Variant]:
        return [variant for tier in self.tiers for variant in tier.variants]


@dataclass
class CheckResult:
    repo_root: Path
    building_files: List[Path]
    architecture_files: List[Path]
    buildings: Dict[str, Building]
    model: ArchitectureModel
    issues: List[Issue]
    notices: List[Notice]
    blueprint_resolution: str
    blueprint_count: int
    goldens: Dict[str, str]
    goldens_written: bool
    max_snapshot_payload_bytes: int
    max_snapshot_encoded_chars: int

    @property
    def ok(self) -> bool:
        return not self.issues

    def report(self) -> str:
        plot_count = sum(1 for item in self.buildings.values() if item.plot)
        lines = [
            "ARCHITECTURE CHECK v1",
            f"building-files: {len(self.building_files)}",
            f"architecture-files: {len(self.architecture_files)}",
            f"buildings: {len(self.buildings)}",
            f"plot-buildings: {plot_count}",
            f"palettes: {len(self.model.palettes)}",
            f"maps: {len(self.model.maps)}",
            f"plans: {len(self.model.plans)}",
            f"bindings: {len(self.model.bindings)}",
            f"tiers: {len(self.model.tiers)}",
            f"variants: {len(self.model.variants)}",
            f"blueprints: {self.blueprint_resolution} ({self.blueprint_count})",
            f"largest-snapshot: {self.max_snapshot_payload_bytes} bytes / "
            f"{self.max_snapshot_encoded_chars} characters",
            f"goldens: {len(self.goldens)} ({'written' if self.goldens_written else 'not written'})",
            f"warnings: {len(self.notices)}",
            f"issues: {len(self.issues)}",
        ]
        lines.extend(notice.render() for notice in sorted(self.notices))
        lines.extend(issue.render() for issue in sorted(self.issues))
        lines.append("RESULT: PASS" if self.ok else "RESULT: FAIL")
        return "\n".join(lines) + "\n"


class OutputDirectoryError(ValueError):
    pass


def _relative(path: Path, root: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return path.name


def _location(path: Path, root: Path, suffix: str = "") -> str:
    base = _relative(path, root)
    return base if not suffix else f"{base}:{suffix}"


def _discover(root: Path, pattern: str) -> List[Path]:
    found = []
    for path in root.rglob(pattern):
        try:
            relative_parts = path.relative_to(root).parts
        except ValueError:
            continue
        if ".git" in relative_parts or not path.is_file():
            continue
        found.append(path)
    return sorted(found, key=lambda item: item.relative_to(root).as_posix())


def _parse_xml(
    path: Path,
    root: Path,
    issues: List[Issue],
    maximum_bytes: int = MAX_XML_BYTES,
) -> Optional[ET.Element]:
    location = _location(path, root)
    try:
        if path.is_symlink():
            issues.append(Issue(location, "input.symlink", "XML inputs must not be symbolic links"))
            return None
        size = path.stat().st_size
        if size > maximum_bytes:
            issues.append(
                Issue(location, "input.size", f"XML is {size} bytes; cap is {maximum_bytes}")
            )
            return None
        data = path.read_bytes()
    except OSError as error:
        issues.append(Issue(location, "input.read", str(error)))
        return None
    upper = data.upper()
    if b"<!DOCTYPE" in upper or b"<!ENTITY" in upper:
        issues.append(Issue(location, "xml.dtd", "DTD and entity declarations are forbidden"))
        return None
    try:
        return ET.fromstring(data)
    except ET.ParseError as error:
        issues.append(Issue(location, "xml.parse", str(error)))
        return None


def _unknown_attributes(
    element: ET.Element,
    allowed: Set[str],
    location: str,
    issues: List[Issue],
) -> None:
    for name in sorted(set(element.attrib) - allowed):
        issues.append(Issue(location, "schema.attribute", f"unknown attribute {name!r}"))


def _required_attribute(
    element: ET.Element,
    name: str,
    location: str,
    issues: List[Issue],
) -> str:
    value = element.get(name, "").strip()
    if not value:
        issues.append(Issue(location, "schema.required", f"missing non-empty {name}"))
    return value


def _valid_key(value: str, location: str, label: str, issues: List[Issue]) -> bool:
    if not value:
        return False
    if len(value) > MAX_KEY_CHARS:
        issues.append(
            Issue(location, "cap.key", f"{label} is {len(value)} characters; cap is {MAX_KEY_CHARS}")
        )
        return False
    if not KEY_RE.fullmatch(value):
        issues.append(Issue(location, "schema.key", f"invalid {label} {value!r}"))
        return False
    return True


def _canonical_int(
    text: str,
    location: str,
    label: str,
    issues: List[Issue],
    minimum: int,
    maximum: int,
) -> Optional[int]:
    try:
        value = int(text, 10)
    except (TypeError, ValueError):
        issues.append(Issue(location, "schema.integer", f"{label} must be a canonical integer"))
        return None
    if str(value) != text or not minimum <= value <= maximum:
        issues.append(
            Issue(
                location,
                "schema.integer",
                f"{label} must be canonical and between {minimum} and {maximum}",
            )
        )
        return None
    return value


def _selector_tokens(value: str, location: str, name: str, issues: List[Issue]) -> Tuple[str, ...]:
    if not value:
        return ()
    if len(value) > MAX_SELECTOR_CHARS:
        issues.append(
            Issue(
                location,
                "cap.selector",
                f"{name} is {len(value)} characters; cap is {MAX_SELECTOR_CHARS}",
            )
        )
    raw = value.split(",")
    tokens = tuple(token.strip() for token in raw)
    if any(not token for token in tokens):
        issues.append(Issue(location, "selector.token", f"{name} contains an empty token"))
    if len(tokens) > MAX_SELECTOR_TOKENS:
        issues.append(
            Issue(
                location,
                "cap.selector-tokens",
                f"{name} has {len(tokens)} tokens; cap is {MAX_SELECTOR_TOKENS}",
            )
        )
    if any(any(ord(char) < 32 for char in token) for token in tokens):
        issues.append(Issue(location, "selector.control", f"{name} contains a control character"))
    return tokens


def _canonical_material(value: str) -> Optional[str]:
    folded = re.sub(r"\s+", "", value.strip().lower())
    return MATERIAL_ALIASES.get(folded)


def _material_cost(building: Building, issues: List[Issue]) -> Optional[Set[str]]:
    raw = building.attributes.get("Materials", "").strip()
    if not raw:
        return set()
    result: Set[str] = set()
    for term in raw.split(","):
        term = term.strip()
        split = term.rfind(":")
        if split <= 0 or split == len(term) - 1:
            issues.append(
                Issue(building.location, "material.cost", f"bad Materials term {term!r}")
            )
            return None
        material = _canonical_material(term[:split])
        amount = term[split + 1 :].strip()
        if material is None or not amount.isdigit() or int(amount, 10) <= 0:
            issues.append(
                Issue(building.location, "material.cost", f"bad Materials term {term!r}")
            )
            return None
        if material in result:
            issues.append(
                Issue(
                    building.location,
                    "material.cost-duplicate",
                    f"Materials names {material!r} more than once",
                )
            )
            return None
        result.add(material)
    return result


def load_buildings(paths: Sequence[Path], repo_root: Path, issues: List[Issue]) -> Dict[str, Building]:
    buildings: Dict[str, Building] = {}
    declarations = 0
    for path in paths:
        xml = _parse_xml(path, repo_root, issues)
        if xml is None:
            continue
        location = _location(path, repo_root)
        if xml.tag != BUILDING_ROOT:
            issues.append(
                Issue(location, "building.root", f"expected <{BUILDING_ROOT}>, found <{xml.tag}>")
            )
            continue
        for index, element in enumerate(xml):
            if element.tag != "building":
                continue
            declarations += 1
            child_location = _location(path, repo_root, f"building[{index}]")
            key = _required_attribute(element, "Key", child_location, issues)
            if not _valid_key(key, child_location, "building Key", issues):
                continue
            attributes = dict(element.attrib)
            attributes.pop("Key", None)
            if key in buildings:
                # KingdomBuildings merge-by-key: omitted fields survive and named fields replace.
                buildings[key].attributes.update(attributes)
                buildings[key].location = child_location
            else:
                buildings[key] = Building(key, attributes, child_location)
    if declarations > MAX_ARCHITECTURE_RECORDS:
        issues.append(
            Issue(
                "KingdomBuildings.xml",
                "cap.buildings",
                f"{declarations} building declarations exceed cap {MAX_ARCHITECTURE_RECORDS}",
            )
        )
    return buildings


def _parse_slot(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Slot]:
    _unknown_attributes(
        element,
        {"Key", "Blueprint", "Role", "Material", "MinTech", "Knowledge", "Power", "Natural"},
        location,
        issues,
    )
    key = _required_attribute(element, "Key", location, issues)
    blueprint = _required_attribute(element, "Blueprint", location, issues)
    role = _required_attribute(element, "Role", location, issues)
    material = element.get("Material", "").strip()
    min_tech = _required_attribute(element, "MinTech", location, issues)
    knowledge = element.get("Knowledge", "").strip()
    power = element.get("Power", "").strip()
    natural = _required_attribute(element, "Natural", location, issues)
    if not _valid_key(key, location, "slot Key", issues):
        return None
    _valid_key(role, location, "slot Role", issues)
    if len(blueprint) > MAX_BLUEPRINT_CHARS:
        issues.append(
            Issue(
                location,
                "cap.blueprint",
                f"Blueprint is {len(blueprint)} characters; cap is {MAX_BLUEPRINT_CHARS}",
            )
        )
    if blueprint.startswith("$"):
        issues.append(Issue(location, "palette.blueprint", "palette Blueprint must be concrete"))
    if _canonical_material(material) is None:
        issues.append(
            Issue(
                location,
                "palette.material",
                "Material must name a settlement material, including on retained natural fabric",
            )
        )
    if min_tech not in TECH_ORDER:
        issues.append(Issue(location, "palette.tech", f"unknown MinTech {min_tech!r}"))
    if knowledge:
        _valid_key(knowledge, location, "slot Knowledge", issues)
    if power:
        _valid_key(power, location, "slot Power", issues)
    if natural not in YES_NO:
        issues.append(Issue(location, "schema.yes-no", "Natural must be 'yes' or 'no'"))
    return Slot(key, blueprint, role, material, min_tech, knowledge, power, natural, location)


def _parse_palette(
    element: ET.Element,
    path: Path,
    repo_root: Path,
    index: int,
    issues: List[Issue],
) -> Optional[Palette]:
    base_location = _location(path, repo_root, f"palette[{index}]")
    _unknown_attributes(element, {"Key"}, base_location, issues)
    key = _required_attribute(element, "Key", base_location, issues)
    if not _valid_key(key, base_location, "palette Key", issues):
        return None
    location = _location(path, repo_root, f"palette[{key}]")
    slots: Dict[str, Slot] = {}
    children = list(element)
    if len(children) > MAX_PALETTE_SLOTS:
        issues.append(
            Issue(
                location,
                "cap.palette-slots",
                f"{len(children)} slots exceed cap {MAX_PALETTE_SLOTS}",
            )
        )
    for child_index, child in enumerate(children):
        child_location = f"{location}/slot[{child_index}]"
        if child.tag != "slot":
            issues.append(
                Issue(child_location, "schema.element", f"palette child must be <slot>, found <{child.tag}>")
            )
            continue
        slot = _parse_slot(child, child_location, issues)
        if slot is None:
            continue
        if slot.key in slots:
            issues.append(
                Issue(child_location, "palette.duplicate-slot", f"duplicate slot Key {slot.key!r}")
            )
            continue
        slots[slot.key] = slot
    if not slots:
        issues.append(Issue(location, "palette.empty", "palette must declare at least one slot"))
    return Palette(key, slots, location)


def _layer_reference(
    value: str,
    layer: str,
    location: str,
    issues: List[Issue],
) -> str:
    value = value.strip()
    if not value:
        return ""
    if len(value) > MAX_BLUEPRINT_CHARS:
        issues.append(
            Issue(
                location,
                "cap.blueprint",
                f"{layer} reference is {len(value)} characters; cap is {MAX_BLUEPRINT_CHARS}",
            )
        )
    if value == "$building" and layer != "Object":
        issues.append(
            Issue(location, "glyph.building-layer", "$building is permitted only on Object")
        )
    if value.startswith("$") and value != "$building":
        _valid_key(value[1:], location, f"{layer} slot reference", issues)
    return value


def _parse_glyph(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Glyph]:
    _unknown_attributes(
        element,
        {
            "Char",
            "Ground",
            "Structure",
            "Object",
            "Claim",
            "Pass",
            "Cover",
            "Anchors",
            "Stateful",
        },
        location,
        issues,
    )
    char = _required_attribute(element, "Char", location, issues)
    if len(char) != 1 or char == "." or char.isspace() or ord(char) < 32:
        issues.append(
            Issue(location, "glyph.char", "Char must be one non-whitespace character other than '.'")
        )
        return None
    ground = _layer_reference(element.get("Ground", ""), "Ground", location, issues)
    structure = _layer_reference(element.get("Structure", ""), "Structure", location, issues)
    object_ref = _layer_reference(element.get("Object", ""), "Object", location, issues)
    if not any((ground, structure, object_ref)):
        issues.append(Issue(location, "glyph.empty", "glyph must place at least one permanent layer"))
    claim = _required_attribute(element, "Claim", location, issues)
    pass_mode = _required_attribute(element, "Pass", location, issues)
    cover = _required_attribute(element, "Cover", location, issues)
    if claim not in CLAIMS:
        issues.append(Issue(location, "glyph.claim", f"Claim must be one of {sorted(CLAIMS)}"))
    if pass_mode not in PASS_MODES:
        issues.append(Issue(location, "glyph.pass", f"Pass must be one of {sorted(PASS_MODES)}"))
    if cover not in COVERS:
        issues.append(Issue(location, "glyph.cover", f"Cover must be one of {sorted(COVERS)}"))
    raw_anchors = element.get("Anchors", "")
    anchors = tuple(item.strip() for item in raw_anchors.split(",") if item.strip())
    if raw_anchors and len(anchors) != len(raw_anchors.split(",")):
        issues.append(Issue(location, "anchor.empty", "Anchors contains an empty token"))
    if len(set(anchors)) != len(anchors):
        issues.append(Issue(location, "anchor.duplicate", "glyph repeats an anchor"))
    for anchor in anchors:
        if len(anchor) > MAX_KEY_CHARS or not ANCHOR_RE.fullmatch(anchor):
            issues.append(Issue(location, "anchor.key", f"invalid anchor {anchor!r}"))
    stateful_text = element.get("Stateful", "no").strip()
    if stateful_text not in YES_NO:
        issues.append(Issue(location, "schema.yes-no", "Stateful must be 'yes' or 'no'"))
    stateful = stateful_text == "yes"
    if stateful and not object_ref:
        issues.append(Issue(location, "stateful.object", "Stateful=yes requires an Object layer"))
    stable = [
        anchor
        for anchor in anchors
        if anchor != "main" and not anchor.startswith("entrance:")
    ]
    if object_ref == "$building" and not stateful:
        issues.append(Issue(location, "stateful.building", "$building must be Stateful=yes"))
    if object_ref and object_ref != "$building" and stable and not stateful:
        issues.append(
            Issue(
                location,
                "stateful.functional-object",
                "an Object with a functional anchor must be Stateful=yes",
            )
        )
    if stateful and object_ref != "$building":
        if len(stable) != 1:
            issues.append(
                Issue(
                    location,
                    "stateful.anchor",
                    "a stateful Object needs exactly one non-main, non-entrance anchor",
                )
            )
    return Glyph(
        char,
        ground,
        structure,
        object_ref,
        claim,
        pass_mode,
        cover,
        anchors,
        stateful,
        location,
    )


def _parse_map(
    element: ET.Element,
    path: Path,
    repo_root: Path,
    index: int,
    issues: List[Issue],
) -> Optional[ArchitectureMap]:
    base_location = _location(path, repo_root, f"map[{index}]")
    _unknown_attributes(element, {"Key", "Width", "Height", "DefaultCover"}, base_location, issues)
    key = _required_attribute(element, "Key", base_location, issues)
    if not _valid_key(key, base_location, "map Key", issues):
        return None
    location = _location(path, repo_root, f"map[{key}]")
    width_text = _required_attribute(element, "Width", location, issues)
    height_text = _required_attribute(element, "Height", location, issues)
    width = _canonical_int(width_text, location, "Width", issues, 1, LOT_DIMENSIONS["XL"][0])
    height = _canonical_int(height_text, location, "Height", issues, 1, LOT_DIMENSIONS["XL"][1])
    width = width or 0
    height = height or 0
    if width * height > MAX_MAP_AREA:
        issues.append(
            Issue(location, "cap.map-area", f"map area {width * height} exceeds cap {MAX_MAP_AREA}")
        )
    default_cover = _required_attribute(element, "DefaultCover", location, issues)
    if default_cover not in COVERS:
        issues.append(
            Issue(location, "map.cover", f"DefaultCover must be one of {sorted(COVERS)}")
        )
    glyphs: Dict[str, Glyph] = {}
    rows: List[str] = []
    saw_row = False
    glyph_count = 0
    for child_index, child in enumerate(element):
        child_location = f"{location}/{child.tag}[{child_index}]"
        if child.tag == "glyph":
            glyph_count += 1
            if saw_row:
                issues.append(
                    Issue(child_location, "map.order", "glyph declarations must precede rows")
                )
            glyph = _parse_glyph(child, child_location, issues)
            if glyph is None:
                continue
            if glyph.char in glyphs:
                issues.append(
                    Issue(child_location, "glyph.duplicate", f"duplicate glyph Char {glyph.char!r}")
                )
                continue
            glyphs[glyph.char] = glyph
        elif child.tag == "row":
            saw_row = True
            _unknown_attributes(child, {"Cells"}, child_location, issues)
            rows.append(_required_attribute(child, "Cells", child_location, issues))
        else:
            issues.append(
                Issue(child_location, "schema.element", f"map child must be <glyph> or <row>, found <{child.tag}>")
            )
    if glyph_count > MAX_GLYPHS:
        issues.append(
            Issue(location, "cap.glyphs", f"{glyph_count} glyphs exceed cap {MAX_GLYPHS}")
        )
    if len(rows) != height:
        issues.append(
            Issue(location, "map.rows", f"declares Height={height} but has {len(rows)} rows")
        )
    for row_index, row in enumerate(rows):
        row_location = f"{location}/row[{row_index}]"
        if len(row) != width:
            issues.append(
                Issue(row_location, "map.row-width", f"row width {len(row)} does not equal Width={width}")
            )
        for char in row:
            if char != "." and char not in glyphs:
                issues.append(
                    Issue(row_location, "glyph.undeclared", f"row uses undeclared glyph {char!r}")
                )
    architecture_map = ArchitectureMap(
        key, width, height, default_cover, glyphs, tuple(rows), location
    )
    _validate_map_topology(architecture_map, issues)
    return architecture_map


def _cells(architecture_map: ArchitectureMap) -> Iterable[Tuple[int, int, Glyph]]:
    for y, row in enumerate(architecture_map.rows):
        for x, char in enumerate(row):
            if char == ".":
                continue
            glyph = architecture_map.glyphs.get(char)
            if glyph is not None:
                yield x, y, glyph


def _neighbors(x: int, y: int, width: int, height: int) -> Iterable[Tuple[int, int]]:
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if 0 <= nx < width and 0 <= ny < height:
            yield nx, ny


def _access_ok(
    position: Tuple[int, int],
    glyph: Glyph,
    reachable: Set[Tuple[int, int]],
    architecture_map: ArchitectureMap,
) -> bool:
    if glyph.pass_mode == "walk":
        return position in reachable
    x, y = position
    return any(
        neighbor in reachable
        for neighbor in _neighbors(x, y, architecture_map.width, architecture_map.height)
    )


def _validate_map_topology(architecture_map: ArchitectureMap, issues: List[Issue]) -> None:
    # Skip positional checks when malformed dimensions/rows would make indexing unsafe.  The
    # structural faults were already reported by _parse_map.
    if (
        architecture_map.width <= 0
        or architecture_map.height <= 0
        or len(architecture_map.rows) != architecture_map.height
        or any(len(row) != architecture_map.width for row in architecture_map.rows)
    ):
        return
    placements = 0
    anchors: List[Tuple[str, int, int, Glyph]] = []
    anchor_count = 0
    for x, y, glyph in _cells(architecture_map):
        placements += len(glyph.layers())
        for anchor in glyph.anchors:
            anchor_count += 1
            anchors.append((anchor, x, y, glyph))
    if placements > MAX_PLACEMENTS:
        issues.append(
            Issue(
                architecture_map.location,
                "cap.placements",
                f"{placements} permanent placements exceed cap {MAX_PLACEMENTS}",
            )
        )
    if anchor_count > MAX_ANCHORS:
        issues.append(
            Issue(
                architecture_map.location,
                "cap.anchors",
                f"{anchor_count} anchors exceed cap {MAX_ANCHORS}",
            )
        )
    main_cells = [(x, y, glyph) for name, x, y, glyph in anchors if name == "main"]
    entrance_cells = [
        (x, y, glyph) for name, x, y, glyph in anchors if name == "entrance:public"
    ]
    if len(main_cells) != 1:
        issues.append(
            Issue(
                architecture_map.location,
                "anchor.main",
                f"map needs exactly one main anchor; found {len(main_cells)}",
            )
        )
    elif main_cells[0][2].object != "$building":
        issues.append(
            Issue(
                architecture_map.location,
                "anchor.main-building",
                "main anchor must share an Object=$building glyph",
            )
        )
    building_cells = [(x, y) for x, y, glyph in _cells(architecture_map) if glyph.object == "$building"]
    if len(building_cells) != 1:
        issues.append(
            Issue(
                architecture_map.location,
                "glyph.building-count",
                f"map must place Object=$building exactly once; found {len(building_cells)}",
            )
        )
    if not entrance_cells:
        issues.append(
            Issue(
                architecture_map.location,
                "anchor.entrance",
                "map needs at least one entrance:public anchor",
            )
        )
        return
    valid_entrances: List[Tuple[int, int]] = []
    for entrance_x, entrance_y, entrance_glyph in entrance_cells:
        if entrance_glyph.pass_mode != "walk" or entrance_glyph.claim not in CLAIMS:
            issues.append(
                Issue(
                    architecture_map.location,
                    "entrance.walk",
                    f"entrance:public at {entrance_x},{entrance_y} must be claimed and Pass=walk",
                )
            )
            continue
        boundary = (
            entrance_x in {0, architecture_map.width - 1}
            or entrance_y in {0, architecture_map.height - 1}
        )
        for nx, ny in _neighbors(
            entrance_x, entrance_y, architecture_map.width, architecture_map.height
        ):
            neighbor = architecture_map.glyph_at(nx, ny)
            if neighbor is None:
                boundary = True
        if not boundary:
            issues.append(
                Issue(
                    architecture_map.location,
                    "entrance.boundary",
                    f"entrance:public at {entrance_x},{entrance_y} must stand on a map edge or claim boundary",
                )
            )
            continue
        valid_entrances.append((entrance_x, entrance_y))
    walkable = {
        (x, y)
        for x, y, glyph in _cells(architecture_map)
        if glyph.pass_mode == "walk" and glyph.claim in CLAIMS
    }
    reachable: Set[Tuple[int, int]] = set()
    queue: deque[Tuple[int, int]] = deque()
    for entrance_position in valid_entrances:
        if entrance_position in walkable and entrance_position not in reachable:
            reachable.add(entrance_position)
            queue.append(entrance_position)
    while queue:
        x, y = queue.popleft()
        for neighbor in _neighbors(x, y, architecture_map.width, architecture_map.height):
            if neighbor in walkable and neighbor not in reachable:
                reachable.add(neighbor)
                queue.append(neighbor)
    missing_walk = sorted(walkable - reachable, key=lambda value: (value[1], value[0]))
    if missing_walk:
        preview = ", ".join(f"{x},{y}" for x, y in missing_walk[:8])
        tail = "" if len(missing_walk) <= 8 else f" (+{len(missing_walk) - 8} more)"
        issues.append(
            Issue(
                architecture_map.location,
                "topology.disconnected",
                f"walk cells are unreachable from entrance:public: {preview}{tail}",
            )
        )
    for x, y, glyph in _cells(architecture_map):
        if glyph.pass_mode == "adjacent" and not _access_ok(
            (x, y), glyph, reachable, architecture_map
        ):
            issues.append(
                Issue(
                    architecture_map.location,
                    "topology.use-cell",
                    f"adjacent-use cell {x},{y} has no reachable orthogonal walk cell",
                )
            )
        for anchor in glyph.anchors:
            if not _access_ok((x, y), glyph, reachable, architecture_map):
                issues.append(
                    Issue(
                        architecture_map.location,
                        "topology.anchor",
                        f"anchor {anchor!r} at {x},{y} is not accessible from entrance:public",
                    )
                )
    snapshot = _canonical_map_snapshot(architecture_map)
    if len(snapshot) > MAX_SNAPSHOT_CHARS:
        issues.append(
            Issue(
                architecture_map.location,
                "cap.snapshot",
                f"canonical map snapshot is {len(snapshot)} characters; cap is {MAX_SNAPSHOT_CHARS}",
            )
        )


def _canonical_map_snapshot(architecture_map: ArchitectureMap) -> str:
    parts = [
        "a1",
        architecture_map.key,
        str(architecture_map.width),
        str(architecture_map.height),
        architecture_map.default_cover,
    ]
    for char in sorted(architecture_map.glyphs):
        glyph = architecture_map.glyphs[char]
        parts.append(
            "~".join(
                (
                    char,
                    glyph.ground,
                    glyph.structure,
                    glyph.object,
                    glyph.claim,
                    glyph.pass_mode,
                    glyph.cover,
                    ",".join(glyph.anchors),
                    "1" if glyph.stateful else "0",
                )
            )
        )
    parts.extend(architecture_map.rows)
    return "|".join(parts)


def _parse_requirement(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Requirement]:
    _unknown_attributes(element, {"Role", "Min", "Max"}, location, issues)
    role = _required_attribute(element, "Role", location, issues)
    _valid_key(role, location, "require Role", issues)
    minimum_text = _required_attribute(element, "Min", location, issues)
    maximum_text = element.get("Max", "0").strip() or "0"
    minimum = _canonical_int(minimum_text, location, "Min", issues, 0, MAX_ANCHORS)
    maximum = _canonical_int(maximum_text, location, "Max", issues, 0, MAX_ANCHORS)
    if minimum is None or maximum is None:
        return None
    if maximum > 0 and minimum > maximum:
        issues.append(Issue(location, "require.range", "Min must not exceed Max"))
    return Requirement(role, minimum, maximum, location)


def _parse_variant(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Variant]:
    allowed = {"Key", "Priority", "Map", "Palette", *SELECTOR_ATTRIBUTES}
    _unknown_attributes(element, allowed, location, issues)
    key = _required_attribute(element, "Key", location, issues)
    if not _valid_key(key, location, "variant Key", issues):
        return None
    priority_text = _required_attribute(element, "Priority", location, issues)
    priority = _canonical_int(priority_text, location, "Priority", issues, 0, 2147483647)
    selectors: Dict[str, str] = {}
    for name in SELECTOR_ATTRIBUTES:
        value = element.get(name, "").strip()
        _selector_tokens(value, location, name, issues)
        selectors[name] = value
    min_stage = selectors["MinStage"].lower()
    max_stage = selectors["MaxStage"].lower()
    min_tech = selectors["MinTech"].lower()
    max_tech = selectors["MaxTech"].lower()
    if min_stage and min_stage not in STAGE_ORDER:
        issues.append(Issue(location, "variant.stage", f"unknown MinStage {selectors['MinStage']!r}"))
    if max_stage and max_stage not in STAGE_ORDER:
        issues.append(Issue(location, "variant.stage", f"unknown MaxStage {selectors['MaxStage']!r}"))
    if min_stage in STAGE_ORDER and max_stage in STAGE_ORDER and STAGE_ORDER[min_stage] > STAGE_ORDER[max_stage]:
        issues.append(Issue(location, "variant.stage-range", "MinStage exceeds MaxStage"))
    if min_tech and min_tech not in TECH_ORDER:
        issues.append(Issue(location, "variant.tech", f"unknown MinTech {selectors['MinTech']!r}"))
    if max_tech and max_tech not in TECH_ORDER:
        issues.append(Issue(location, "variant.tech", f"unknown MaxTech {selectors['MaxTech']!r}"))
    if min_tech in TECH_ORDER and max_tech in TECH_ORDER and TECH_ORDER[min_tech] > TECH_ORDER[max_tech]:
        issues.append(Issue(location, "variant.tech-range", "MinTech exceeds MaxTech"))
    return Variant(
        key,
        priority if priority is not None else 0,
        selectors,
        element.get("Map", "").strip(),
        element.get("Palette", "").strip(),
        location,
    )


def _parse_tier(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Tier]:
    _unknown_attributes(element, {"Key", "BuildKey", "Level", "Map", "Palette"}, location, issues)
    key = _required_attribute(element, "Key", location, issues)
    build_key = _required_attribute(element, "BuildKey", location, issues)
    map_key = _required_attribute(element, "Map", location, issues)
    palette_key = _required_attribute(element, "Palette", location, issues)
    if not _valid_key(key, location, "tier Key", issues):
        return None
    _valid_key(build_key, location, "tier BuildKey", issues)
    _valid_key(map_key, location, "tier Map", issues)
    _valid_key(palette_key, location, "tier Palette", issues)
    level_text = _required_attribute(element, "Level", location, issues)
    level = _canonical_int(level_text, location, "Level", issues, 0, MAX_TIERS_PER_BINDING - 1)
    requirements: List[Requirement] = []
    variants: List[Variant] = []
    saw_variant = False
    requirement_roles: Set[str] = set()
    variant_keys: Set[str] = set()
    for child_index, child in enumerate(element):
        child_location = f"{location}/{child.tag}[{child_index}]"
        if child.tag == "require":
            if saw_variant:
                issues.append(
                    Issue(child_location, "tier.order", "require elements must precede variants")
                )
            requirement = _parse_requirement(child, child_location, issues)
            if requirement is None:
                continue
            if requirement.role in requirement_roles:
                issues.append(
                    Issue(child_location, "require.duplicate", f"duplicate Role {requirement.role!r}")
                )
            else:
                requirement_roles.add(requirement.role)
                requirements.append(requirement)
        elif child.tag == "variant":
            saw_variant = True
            variant = _parse_variant(child, child_location, issues)
            if variant is None:
                continue
            if variant.key in variant_keys:
                issues.append(
                    Issue(child_location, "variant.duplicate", f"duplicate variant Key {variant.key!r}")
                )
            else:
                variant_keys.add(variant.key)
                variants.append(variant)
        else:
            issues.append(
                Issue(child_location, "schema.element", f"tier child must be <require> or <variant>, found <{child.tag}>")
            )
    if len(requirements) > MAX_REQUIREMENTS_PER_TIER:
        issues.append(
            Issue(
                location,
                "cap.requirements",
                f"{len(requirements)} requirements exceed cap {MAX_REQUIREMENTS_PER_TIER}",
            )
        )
    if len(variants) > MAX_VARIANTS_PER_TIER:
        issues.append(
            Issue(
                location,
                "cap.variants",
                f"{len(variants)} variants exceed cap {MAX_VARIANTS_PER_TIER}",
            )
        )
    fallbacks = [variant for variant in variants if variant.fallback]
    if len(fallbacks) != 1:
        issues.append(
            Issue(
                location,
                "variant.fallback",
                f"tier needs exactly one unconditional fallback variant; found {len(fallbacks)}",
            )
        )
    return Tier(
        key,
        build_key,
        level if level is not None else 0,
        map_key,
        palette_key,
        requirements,
        variants,
        location,
    )


def _parse_binding(
    element: ET.Element,
    location: str,
    issues: List[Issue],
) -> Optional[Binding]:
    _unknown_attributes(element, {"Key", "Type", "Size", "Facing"}, location, issues)
    key = _required_attribute(element, "Key", location, issues)
    type_key = _required_attribute(element, "Type", location, issues)
    size = _required_attribute(element, "Size", location, issues)
    facing = _required_attribute(element, "Facing", location, issues)
    if not _valid_key(key, location, "binding Key", issues):
        return None
    _valid_key(type_key, location, "binding Type", issues)
    if size not in LOT_DIMENSIONS:
        issues.append(Issue(location, "binding.size", f"Size must be one of {sorted(LOT_DIMENSIONS)}"))
    if facing not in {"heart", "road"}:
        issues.append(
            Issue(
                location,
                "binding.facing",
                "wave-A authored Facing must be 'heart' or 'road', never a fixed compass pose",
            )
        )
    tiers: List[Tier] = []
    tier_keys: Set[str] = set()
    levels: Set[int] = set()
    for child_index, child in enumerate(element):
        child_location = f"{location}/{child.tag}[{child_index}]"
        if child.tag != "tier":
            issues.append(
                Issue(child_location, "schema.element", f"binding child must be <tier>, found <{child.tag}>")
            )
            continue
        tier = _parse_tier(child, child_location, issues)
        if tier is None:
            continue
        if tier.key in tier_keys:
            issues.append(Issue(child_location, "tier.duplicate", f"duplicate tier Key {tier.key!r}"))
            continue
        if tier.level in levels:
            issues.append(
                Issue(child_location, "tier.level", f"duplicate tier Level {tier.level}")
            )
        tier_keys.add(tier.key)
        levels.add(tier.level)
        tiers.append(tier)
    if len(tiers) > MAX_TIERS_PER_BINDING:
        issues.append(
            Issue(
                location,
                "cap.tiers",
                f"{len(tiers)} tiers exceed cap {MAX_TIERS_PER_BINDING}",
            )
        )
    if not tiers:
        issues.append(Issue(location, "binding.empty", "binding must declare at least one tier"))
    binding = Binding(key, type_key, size, facing, tiers, location)
    for tier in tiers:
        tier.binding = binding
    return binding


def _parse_plan(
    element: ET.Element,
    path: Path,
    repo_root: Path,
    index: int,
    issues: List[Issue],
) -> Optional[Plan]:
    base_location = _location(path, repo_root, f"plan[{index}]")
    _unknown_attributes(element, {"Key"}, base_location, issues)
    key = _required_attribute(element, "Key", base_location, issues)
    if not _valid_key(key, base_location, "plan Key", issues):
        return None
    location = _location(path, repo_root, f"plan[{key}]")
    bindings: List[Binding] = []
    binding_keys: Set[str] = set()
    for child_index, child in enumerate(element):
        child_location = f"{location}/{child.tag}[{child_index}]"
        if child.tag != "binding":
            issues.append(
                Issue(child_location, "schema.element", f"plan child must be <binding>, found <{child.tag}>")
            )
            continue
        binding = _parse_binding(child, child_location, issues)
        if binding is None:
            continue
        if binding.key in binding_keys:
            issues.append(
                Issue(child_location, "binding.duplicate", f"duplicate binding Key {binding.key!r}")
            )
            continue
        binding_keys.add(binding.key)
        bindings.append(binding)
    if len(bindings) > MAX_BINDINGS_PER_PLAN:
        issues.append(
            Issue(
                location,
                "cap.bindings",
                f"{len(bindings)} bindings exceed cap {MAX_BINDINGS_PER_PLAN}",
            )
        )
    if not bindings:
        issues.append(Issue(location, "plan.empty", "plan must declare at least one binding"))
    plan = Plan(key, bindings, location)
    for binding in bindings:
        binding.plan = plan
    return plan


def load_architectures(
    paths: Sequence[Path], repo_root: Path, issues: List[Issue]
) -> ArchitectureModel:
    model = ArchitectureModel()
    record_count = 0
    binding_keys: Set[str] = set()
    for path in paths:
        xml = _parse_xml(path, repo_root, issues)
        if xml is None:
            continue
        location = _location(path, repo_root)
        if xml.tag != ARCHITECTURE_ROOT:
            issues.append(
                Issue(
                    location,
                    "architecture.root",
                    f"expected <{ARCHITECTURE_ROOT}>, found <{xml.tag}>",
                )
            )
            continue
        _unknown_attributes(xml, {"Schema"}, location, issues)
        if xml.get("Schema", "").strip() != SCHEMA_VERSION:
            issues.append(
                Issue(location, "architecture.schema", f"Schema must be {SCHEMA_VERSION!r}")
            )
        for index, element in enumerate(xml):
            record_count += 1
            if element.tag == "palette":
                palette = _parse_palette(element, path, repo_root, index, issues)
                if palette is None:
                    continue
                if palette.key in model.palettes:
                    issues.append(
                        Issue(palette.location, "palette.duplicate", f"duplicate palette Key {palette.key!r}")
                    )
                else:
                    model.palettes[palette.key] = palette
            elif element.tag == "map":
                architecture_map = _parse_map(element, path, repo_root, index, issues)
                if architecture_map is None:
                    continue
                if architecture_map.key in model.maps:
                    issues.append(
                        Issue(architecture_map.location, "map.duplicate", f"duplicate map Key {architecture_map.key!r}")
                    )
                else:
                    model.maps[architecture_map.key] = architecture_map
            elif element.tag == "plan":
                plan = _parse_plan(element, path, repo_root, index, issues)
                if plan is None:
                    continue
                if plan.key in model.plans:
                    issues.append(Issue(plan.location, "plan.duplicate", f"duplicate plan Key {plan.key!r}"))
                    continue
                for binding in plan.bindings:
                    if binding.key in binding_keys:
                        issues.append(
                            Issue(
                                binding.location,
                                "binding.global-duplicate",
                                f"binding Key {binding.key!r} is not globally unique",
                            )
                        )
                    binding_keys.add(binding.key)
                model.plans[plan.key] = plan
            else:
                issues.append(
                    Issue(
                        _location(path, repo_root, f"{element.tag}[{index}]"),
                        "schema.element",
                        f"root child must be <palette>, <map>, or <plan>; found <{element.tag}>",
                    )
                )
    if record_count > MAX_ARCHITECTURE_RECORDS:
        issues.append(
            Issue(
                "KingdomArchitectures*.xml",
                "cap.records",
                f"{record_count} top-level records exceed cap {MAX_ARCHITECTURE_RECORDS}",
            )
        )
    return model


def _anchors_in_map(architecture_map: ArchitectureMap) -> List[str]:
    result: List[str] = []
    for _x, _y, glyph in _cells(architecture_map):
        result.extend(glyph.anchors)
    return result


def _resolve_reference(reference: str, palette: Palette, building: Building) -> str:
    if not reference:
        return ""
    if reference == "$building":
        return building.blueprint
    if reference.startswith("$"):
        slot = palette.slots.get(reference[1:])
        return "" if slot is None else slot.blueprint
    return reference


def _used_palette_slots(
    architecture_map: ArchitectureMap, palette: Palette
) -> List[Slot]:
    result: Dict[str, Slot] = {}
    for _x, _y, glyph in _cells(architecture_map):
        for _layer, reference in glyph.layers():
            if not reference or reference == "$building" or not reference.startswith("$"):
                continue
            slot = palette.slots.get(reference[1:])
            if slot is not None:
                result[slot.key] = slot
    return [result[key] for key in sorted(result)]


def _binary_text_size(value: str) -> int:
    """Bytes written by KingdomArchitectureRules.WriteText (ushort length + strict UTF-8)."""

    return 2 + len(value.encode("utf-8", errors="strict"))


def _compiled_snapshot_size(
    tier: Tier,
    variant: Variant,
    building: Building,
    architecture_map: ArchitectureMap,
    palette: Palette,
) -> Optional[Tuple[int, int]]:
    """Reproduce the a2 runtime codec's exact byte and outer-string size without importing it.

    Invalid references return None because their own schema/reference findings are the primary
    fault. Valid content must fit both bounds here; otherwise the runtime loader would discard the
    whole mapping even though the authored XML gate appeared green.
    """

    if (
        architecture_map.width <= 0
        or architecture_map.height <= 0
        or len(architecture_map.rows) != architecture_map.height
        or any(len(row) != architecture_map.width for row in architecture_map.rows)
    ):
        return None
    anchors: List[str] = []
    placements: List[Tuple[str, str, str, str, str, str]] = []
    for x, y, glyph in _cells(architecture_map):
        cell_anchors = [
            role if role == "main" else f"{role}@{x},{y}"
            for role in glyph.anchors
        ]
        anchors.extend(cell_anchors)
        for layer, reference in glyph.layers():
            if reference == "$building":
                continue
            if not reference.startswith("$"):
                return None
            slot = palette.slots.get(reference[1:])
            material = None if slot is None else _canonical_material(slot.material)
            if slot is None or material is None or slot.min_tech not in TECH_ORDER:
                return None
            stateful_anchor = ""
            if layer == "Object" and glyph.stateful:
                stable = [
                    key
                    for key in cell_anchors
                    if key != "main" and not key.startswith("entrance:")
                ]
                if len(stable) != 1:
                    return None
                stateful_anchor = stable[0]
            placements.append(
                (
                    slot.blueprint,
                    material,
                    slot.min_tech,
                    stateful_anchor,
                    slot.knowledge,
                    slot.power,
                )
            )

    blueprints = sorted({placement[0] for placement in placements})
    materials = sorted({placement[1] for placement in placements})
    techs = sorted({placement[2] for placement in placements})
    knowledge = sorted({placement[4] for placement in placements if placement[4]})
    powers = sorted({placement[5] for placement in placements if placement[5]})
    metadata = (
        tier.binding.plan.key,
        tier.binding.key,
        tier.build_key,
        tier.key,
        variant.key,
        palette.key,
        tier.binding.type_key.strip().lower(),
    )
    payload = 4  # TAF + schema
    payload += sum(_binary_text_size(value) for value in metadata)
    payload += 6  # lot, facing, dimensions, main coordinate
    for table in (blueprints, materials, techs, knowledge, powers):
        payload += 1 + sum(_binary_text_size(value) for value in table)
    payload += 2 + architecture_map.width * architecture_map.height * 3
    payload += 1 + sum(_binary_text_size(key) + 3 for key in anchors)
    payload += 2 + len(placements) * 11
    encoded = SNAPSHOT_TEXT_OVERHEAD + 4 * ((payload + 2) // 3)
    return payload, encoded


def _snapshot_maxima(
    buildings: Mapping[str, Building], model: ArchitectureModel
) -> Tuple[int, int]:
    maximum_payload = 0
    maximum_encoded = 0
    for tier in model.tiers:
        building = buildings.get(tier.build_key)
        if building is None:
            continue
        for variant in tier.variants:
            architecture_map = model.maps.get(variant.map_key or tier.map_key)
            palette = model.palettes.get(variant.palette_key or tier.palette_key)
            if architecture_map is None or palette is None:
                continue
            size = _compiled_snapshot_size(tier, variant, building, architecture_map, palette)
            if size is not None:
                maximum_payload = max(maximum_payload, size[0])
                maximum_encoded = max(maximum_encoded, size[1])
    return maximum_payload, maximum_encoded


def _looks_like_vertical_travel(value: str) -> bool:
    folded = value.lower()
    if "stair" in folded or "elevator" in folded:
        return True
    tokens = re.findall(r"[a-z]+", folded)
    return any(token in VERTICAL_ROLE_TERMS for token in tokens)


def _validate_vertical_evidence(
    architecture_map: ArchitectureMap,
    palette: Palette,
    building: Building,
    location: str,
    issues: List[Issue],
) -> None:
    endpoint_counts: Counter[str] = Counter()
    runtime_owned_counts: Counter[str] = Counter()
    claimed_vertical = False
    for x, y, glyph in _cells(architecture_map):
        if any(
            anchor == "function:vertical-core" or anchor.startswith("function:vertical-core:")
            for anchor in glyph.anchors
        ):
            claimed_vertical = True
        for layer, reference in glyph.layers():
            slot = palette.slots.get(reference[1:]) if reference.startswith("$") else None
            blueprint = _resolve_reference(reference, palette, building)
            role = "" if slot is None else slot.role
            suspect = _looks_like_vertical_travel(blueprint) or _looks_like_vertical_travel(role)
            if not suspect:
                continue
            claimed_vertical = True
            allowed = VERTICAL_BLUEPRINT_PAIRS.get(blueprint)
            if allowed is None:
                issues.append(
                    Issue(
                        location,
                        "travel.link-owner" if blueprint in RAW_VERTICAL_BLUEPRINTS else "travel.blueprint",
                        f"map {architecture_map.key!r} cell {x},{y} uses {blueprint!r} as vertical travel; "
                        + (
                            "raw vanilla stairs carry no delve link ownership"
                            if blueprint in RAW_VERTICAL_BLUEPRINTS
                            else "it is not in the functional endpoint allow-list"
                        ),
                    )
                )
                if blueprint == "StairsUp":
                    endpoint_counts["up"] += 1
                elif blueprint == "StairsDown":
                    endpoint_counts["down"] += 1
                continue
            direction, _counterpart = allowed
            endpoint_counts[direction] += 1
            runtime_owned_counts[direction] += 1
            if layer != "Object":
                issues.append(
                    Issue(
                        location,
                        "travel.layer",
                        f"functional {blueprint!r} at {x},{y} must occupy Object, not {layer}",
                    )
                )
            required_anchor = f"travel:{direction}"
            if required_anchor not in glyph.anchors:
                issues.append(
                    Issue(
                        location,
                        "travel.anchor",
                        f"functional {blueprint!r} at {x},{y} needs anchor {required_anchor!r}",
                    )
                )
    if not claimed_vertical:
        return
    if endpoint_counts["up"] != 0:
        issues.append(
            Issue(
                location,
                "travel.same-map",
                f"authored vertical head must not place its paired Up in the same map; found "
                f"up={endpoint_counts['up']}",
            )
        )
    if building.key != "delve":
        issues.append(
            Issue(
                location,
                "travel.runtime-owner",
                "functional vertical architecture has no engine-coupled external pairing owner",
            )
        )
    elif endpoint_counts["down"] != 1 or runtime_owned_counts["down"] != 1:
        issues.append(
            Issue(
                location,
                "travel.head",
                "delve architecture needs exactly one runtime-owned r_KingdomDelveDown head; "
                f"found down={endpoint_counts['down']}, owned={runtime_owned_counts['down']}",
            )
        )


def _framed(value: str) -> str:
    return f"{len(value)}:{value}"


def _effective_architecture_snapshot(
    architecture_map: ArchitectureMap,
    palette: Palette,
    building: Building,
) -> str:
    """Canonical compiled visual/topology identity, excluding the BuildKey root blueprint."""

    fields = [
        "effective-a1",
        str(architecture_map.width),
        str(architecture_map.height),
    ]
    for y, row in enumerate(architecture_map.rows):
        for x, char in enumerate(row):
            glyph = None if char == "." else architecture_map.glyphs.get(char)
            if glyph is None:
                fields.extend((str(x), str(y), "unclaimed", "walk", "open", "", "", "", "no"))
                continue

            def resolved(reference: str) -> str:
                if reference == "$building":
                    return "$building"
                return _resolve_reference(reference, palette, building)

            fields.extend(
                (
                    str(x),
                    str(y),
                    glyph.claim,
                    glyph.pass_mode,
                    glyph.cover or architecture_map.default_cover,
                    resolved(glyph.ground),
                    resolved(glyph.structure),
                    resolved(glyph.object),
                    "yes" if glyph.stateful else "no",
                    *sorted(glyph.anchors),
                )
            )
    return "".join(_framed(field) for field in fields)


def _has_procedural_shell_signature(
    architecture_map: ArchitectureMap,
    palette: Palette,
    building: Building,
) -> bool:
    """Recognize the old generic rectangle without relying on names or aesthetic judgment."""

    if (
        architecture_map.width <= 0
        or architecture_map.height <= 0
        or len(architecture_map.rows) != architecture_map.height
        or any(len(row) != architecture_map.width for row in architecture_map.rows)
    ):
        return False
    cells = list(_cells(architecture_map))
    if len(cells) != architecture_map.width * architecture_map.height:
        return False
    entrances = [
        (x, y)
        for x, y, glyph in cells
        for anchor in glyph.anchors
        if anchor == "entrance:public"
    ]
    if len(entrances) != 1:
        return False
    entrance = entrances[0]
    border = [
        (x, y, glyph)
        for x, y, glyph in cells
        if x in {0, architecture_map.width - 1}
        or y in {0, architecture_map.height - 1}
    ]
    enclosure_signatures: Set[Tuple[str, str, str, str]] = set()
    for x, y, glyph in border:
        if (x, y) == entrance:
            continue
        if glyph.pass_mode != "blocked" or not glyph.structure or glyph.object:
            return False
        enclosure_signatures.add(
            (
                _resolve_reference(glyph.ground, palette, building),
                _resolve_reference(glyph.structure, palette, building),
                glyph.claim,
                glyph.cover or architecture_map.default_cover,
            )
        )
    if len(enclosure_signatures) != 1:
        return False
    mains = [
        (x, y)
        for x, y, glyph in cells
        for anchor in glyph.anchors
        if anchor == "main"
    ]
    if len(mains) != 1:
        return False
    center_x = {(architecture_map.width - 1) // 2, architecture_map.width // 2}
    center_y = {(architecture_map.height - 1) // 2, architecture_map.height // 2}
    main = mains[0]
    if main[0] not in center_x or main[1] not in center_y:
        return False

    # Any authored fixture, internal partition, use cell, role away from the root/door, or
    # variation in interior material/claim/cover is meaningful differentiation.
    interior_ground: Set[str] = set()
    interior_claim: Set[str] = set()
    interior_cover: Set[str] = set()
    for x, y, glyph in cells:
        stable_anchors = [
            anchor
            for anchor in glyph.anchors
            if anchor != "main" and not anchor.startswith("entrance:")
        ]
        if (x, y) != main and stable_anchors:
            return False
        if x in {0, architecture_map.width - 1} or y in {
            0,
            architecture_map.height - 1,
        }:
            continue
        if (x, y) == main:
            continue
        if glyph.structure or glyph.object or glyph.pass_mode != "walk":
            return False
        interior_ground.add(_resolve_reference(glyph.ground, palette, building))
        interior_claim.add(glyph.claim)
        interior_cover.add(glyph.cover or architecture_map.default_cover)
    return (
        len(interior_ground) <= 1
        and len(interior_claim) <= 1
        and len(interior_cover) <= 1
    )


def _validate_tier_variant(
    tier: Tier,
    variant: Variant,
    building: Building,
    model: ArchitectureModel,
    issues: List[Issue],
) -> None:
    map_key = variant.map_key or tier.map_key
    palette_key = variant.palette_key or tier.palette_key
    architecture_map = model.maps.get(map_key)
    palette = model.palettes.get(palette_key)
    location = variant.location
    if architecture_map is None:
        issues.append(Issue(location, "reference.map", f"unknown map {map_key!r}"))
    if palette is None:
        issues.append(Issue(location, "reference.palette", f"unknown palette {palette_key!r}"))
    if architecture_map is None or palette is None:
        return
    paid = _material_cost(building, issues)
    used_slots = _used_palette_slots(architecture_map, palette)
    concrete_scenery = sorted(
        {
            reference
            for _x, _y, glyph in _cells(architecture_map)
            for _layer, reference in glyph.layers()
            if reference and reference != "$building" and not reference.startswith("$")
        }
    )
    if concrete_scenery:
        issues.append(
            Issue(
                location,
                "material.unclassified",
                "scenery must resolve through palette slots carrying material, technology, and natural truth: "
                + ", ".join(repr(item) for item in concrete_scenery),
            )
        )
    if paid is not None:
        used_materials = {
            material
            for slot in used_slots
            if slot.natural != "yes" and slot.blueprint != "r_KingdomFirstBasin"
            for material in [_canonical_material(slot.material)]
            if material is not None
        }
        unpaid = sorted(used_materials - paid)
        if unpaid:
            issues.append(
                Issue(
                    location,
                    "material.unpaid",
                    f"effective map uses non-natural material {', '.join(unpaid)} absent from "
                    f"building {building.key!r} Materials",
                )
            )
    building_tech = building.attributes.get("MinTech", "hands").strip().lower() or "hands"
    variant_tech = variant.selectors.get("MinTech", "").strip().lower()
    declared_tech = max(
        TECH_ORDER.get(building_tech, -1), TECH_ORDER.get(variant_tech, -1)
    )
    used_tech = max((TECH_ORDER.get(slot.min_tech, -1) for slot in used_slots), default=0)
    if building_tech not in TECH_ORDER:
        issues.append(
            Issue(location, "building.tech", f"unknown building MinTech {building_tech!r}")
        )
    elif used_tech > declared_tech:
        required = next(name for name, rank in TECH_ORDER.items() if rank == used_tech)
        declared = next(name for name, rank in TECH_ORDER.items() if rank == declared_tech)
        issues.append(
            Issue(
                location,
                "palette.tech-underdeclared",
                f"effective map needs {required} craft but building/variant admits {declared}",
            )
        )
    lot = LOT_DIMENSIONS.get(tier.binding.size)
    if lot is not None and (architecture_map.width, architecture_map.height) != lot:
        issues.append(
            Issue(
                location,
                "binding.map-fit",
                f"map {map_key!r} is {architecture_map.width}x{architecture_map.height}; "
                f"{tier.binding.size} canonical lot must be exactly {lot[0]}x{lot[1]}",
            )
        )
    if tier.binding.facing == "road":
        entrances = [
            (x, y)
            for x, y, glyph in _cells(architecture_map)
            if "entrance:public" in glyph.anchors
        ]
        for pose in POSES:
            posed_width = (
                architecture_map.height
                if pose in {"east", "west"}
                else architecture_map.width
            )
            posed_height = (
                architecture_map.width
                if pose in {"east", "west"}
                else architecture_map.height
            )
            for entrance_x, entrance_y in entrances:
                if pose == "north":
                    world_x, world_y = entrance_x, entrance_y
                elif pose == "east":
                    world_x, world_y = architecture_map.height - 1 - entrance_y, entrance_x
                elif pose == "south":
                    world_x = architecture_map.width - 1 - entrance_x
                    world_y = architecture_map.height - 1 - entrance_y
                else:
                    world_x, world_y = entrance_y, architecture_map.width - 1 - entrance_x
                if world_x not in {0, posed_width - 1} and world_y not in {
                    0,
                    posed_height - 1,
                }:
                    issues.append(
                        Issue(
                            location,
                            "frontage.road-exterior",
                            f"road-facing entrance:public at canonical {entrance_x},{entrance_y} "
                            f"lands at interior {world_x},{world_y} in {pose} pose; every public "
                            "entrance must have an orthogonally adjacent exterior road cell",
                        )
                    )
    for glyph in architecture_map.glyphs.values():
        for layer, reference in glyph.layers():
            if reference.startswith("$") and reference != "$building":
                slot_key = reference[1:]
                if slot_key not in palette.slots:
                    issues.append(
                        Issue(
                            location,
                            "reference.slot",
                            f"map {map_key!r} {layer} uses {reference!r}, absent from palette {palette_key!r}",
                        )
                    )
    anchors = _anchors_in_map(architecture_map)
    for requirement in tier.requirements:
        count = sum(
            1
            for anchor in anchors
            if anchor == requirement.role or anchor.startswith(requirement.role + ":")
        )
        too_many = requirement.maximum > 0 and count > requirement.maximum
        if count < requirement.minimum or too_many:
            issues.append(
                Issue(
                    location,
                    "require.count",
                    f"map {map_key!r} has {count} anchors for Role={requirement.role!r}; "
                    f"required at least {requirement.minimum}"
                    + (
                        f" and at most {requirement.maximum}"
                        if requirement.maximum > 0
                        else " (no upper bound)"
                    ),
                )
            )
    snapshot_size = _compiled_snapshot_size(
        tier, variant, building, architecture_map, palette
    )
    if snapshot_size is not None:
        payload_bytes, encoded_chars = snapshot_size
        if payload_bytes > MAX_SNAPSHOT_PAYLOAD_BYTES:
            issues.append(
                Issue(
                    location,
                    "cap.snapshot-payload",
                    f"compiled a2 snapshot is {payload_bytes} bytes; runtime cap is "
                    f"{MAX_SNAPSHOT_PAYLOAD_BYTES}",
                )
            )
        if encoded_chars > MAX_SNAPSHOT_CHARS:
            issues.append(
                Issue(
                    location,
                    "cap.snapshot-encoding",
                    f"compiled a2 snapshot is {encoded_chars} characters; runtime cap is "
                    f"{MAX_SNAPSHOT_CHARS}",
                )
            )


def _validate_heart_accretion(
    buildings: Dict[str, Building], model: ArchitectureModel, issues: List[Issue]
) -> None:
    """The four civic-heart snapshots must be one nested monument, not four rebuilds."""

    plan = model.plans.get("civic-heart")
    if plan is None:
        return
    by_build = {
        tier.build_key: tier
        for binding in plan.bindings
        for tier in binding.tiers
        if tier.build_key in HEART_BUILD_KEYS
    }
    missing = [key for key in HEART_BUILD_KEYS if key not in by_build]
    if missing:
        issues.append(
            Issue(
                plan.location,
                "heart.rungs",
                "civic-heart must carry all four ordered rungs; missing "
                + ", ".join(repr(key) for key in missing),
            )
        )
        return

    snapshots: List[
        Tuple[
            Tier,
            ArchitectureMap,
            Palette,
            Tuple[int, int],
            Tuple[int, int],
            Dict[Tuple[str, int, int], Tuple[str, str, str, str, str, str, str]],
        ]
    ] = []
    for key in HEART_BUILD_KEYS:
        tier = by_build[key]
        architecture_map = model.maps.get(tier.map_key)
        palette = model.palettes.get(tier.palette_key)
        building = buildings.get(key)
        if architecture_map is None or palette is None or building is None:
            return
        mains = [
            (x, y)
            for x, y, glyph in _cells(architecture_map)
            if "main" in glyph.anchors
        ]
        if len(mains) != 1:
            issues.append(
                Issue(
                    tier.location,
                    "heart.main",
                    f"heart rung {key!r} needs exactly one main cell; found {len(mains)}",
                )
            )
            continue
        main = mains[0]
        centre = ((architecture_map.width - 1) // 2, (architecture_map.height - 1) // 2)
        basin_cells: List[Tuple[int, int]] = []
        placements: Dict[
            Tuple[str, int, int], Tuple[str, str, str, str, str, str, str]
        ] = {}
        for x, y, glyph in _cells(architecture_map):
            stable_anchors = [
                anchor
                for anchor in glyph.anchors
                if anchor != "main" and not anchor.startswith("entrance:")
            ]
            stateful_anchor = (
                stable_anchors[0]
                if glyph.stateful and len(stable_anchors) == 1
                else ""
            )
            for layer, reference in glyph.layers():
                if reference == "$building":
                    continue
                blueprint = _resolve_reference(reference, palette, building)
                slot = palette.slots.get(reference[1:]) if reference.startswith("$") else None
                if not blueprint:
                    continue
                if blueprint == "r_KingdomFirstBasin":
                    basin_cells.append((x, y))
                placements[(layer, x - main[0], y - main[1])] = (
                    blueprint,
                    "" if slot is None else (_canonical_material(slot.material) or ""),
                    "" if slot is None else slot.min_tech,
                    "" if slot is None else slot.knowledge,
                    "" if slot is None else slot.power,
                    "" if slot is None else slot.natural,
                    stateful_anchor if layer == "Object" else "",
                )
        basin_cells = sorted(set(basin_cells))
        if len(basin_cells) != 1:
            issues.append(
                Issue(
                    tier.location,
                    "heart.basin-count",
                    f"heart rung {key!r} needs exactly one immutable first-basin placement; "
                    f"found {len(basin_cells)}",
                )
            )
            continue
        basin = basin_cells[0]
        basin_world_offset = (basin[0] - centre[0], basin[1] - centre[1])
        if basin_world_offset != (0, 0):
            issues.append(
                Issue(
                    tier.location,
                    "heart.basin-rite",
                    f"heart rung {key!r} puts its immutable basin at canonical {basin[0]},{basin[1]}; "
                    f"the centred lot maps {centre[0]},{centre[1]} to the recorded rite",
                )
            )
        snapshots.append(
            (
                tier,
                architecture_map,
                palette,
                (main[0] - centre[0], main[1] - centre[1]),
                (basin[0] - main[0], basin[1] - main[1]),
                placements,
            )
        )
    if len(snapshots) != len(HEART_BUILD_KEYS):
        return
    main_offset = snapshots[0][3]
    basin_relative = snapshots[0][4]
    for tier, _architecture_map, _palette, next_main, next_basin, _placements in snapshots[1:]:
        if next_main != main_offset:
            issues.append(
                Issue(
                    tier.location,
                    "heart.main-moves",
                    f"heart rung {tier.build_key!r} moves main from rite-relative {main_offset} "
                    f"to {next_main}",
                )
            )
        if next_basin != basin_relative:
            issues.append(
                Issue(
                    tier.location,
                    "heart.basin-moves",
                    f"heart rung {tier.build_key!r} moves the basin relative to main from "
                    f"{basin_relative} to {next_basin}",
                )
            )
    for index in range(len(snapshots) - 1):
        before = snapshots[index]
        after = snapshots[index + 1]
        for coordinate, identity in sorted(before[5].items()):
            next_identity = after[5].get(coordinate)
            if next_identity != identity:
                layer, dx, dy = coordinate
                issues.append(
                    Issue(
                        after[0].location,
                        "heart.fabric-replaced",
                        f"{before[0].build_key!r}->{after[0].build_key!r} does not retain "
                        f"{layer} fabric at main-relative {dx},{dy}: {identity[0]!r} becomes "
                        + ("absent" if next_identity is None else repr(next_identity[0])),
                    )
                )


def validate_model(
    buildings: Dict[str, Building], model: ArchitectureModel, issues: List[Issue]
) -> None:
    plot_buildings = {key: item for key, item in buildings.items() if item.plot}
    for palette in model.palettes.values():
        for slot in palette.slots.values():
            reason = UNSAFE_AUTHORED_BLUEPRINTS.get(slot.blueprint)
            if reason is not None:
                issues.append(
                    Issue(
                        slot.location,
                        "fixture.unstable-blueprint",
                        f"raw Blueprint {slot.blueprint!r} is not stable authored architecture: "
                        f"{reason}; use a settlement-owned vanilla-art wrapper",
                    )
                )
    build_counts: Counter[Tuple[str, str, str]] = Counter()
    compiled_identities: Dict[str, List[Tuple[str, str, str, str, str]]] = {}
    procedural_shells: Dict[Tuple[str, str, str], List[Tuple[str, str]]] = {}
    for tier in model.tiers:
        build_counts[(tier.build_key, tier.binding.type_key, tier.binding.size)] += 1
        building = buildings.get(tier.build_key)
        if building is None:
            issues.append(
                Issue(tier.location, "tier.build-key", f"unknown BuildKey {tier.build_key!r}")
            )
            continue
        if not building.plot:
            issues.append(
                Issue(
                    tier.location,
                    "tier.non-plot",
                    f"BuildKey {tier.build_key!r} is not a plot building",
                )
            )
        if building.category != tier.binding.type_key:
            issues.append(
                Issue(
                    tier.location,
                    "binding.type",
                    f"binding Type={tier.binding.type_key!r} does not match building Category={building.category!r}",
                )
            )
        if (
            building.plot in LOT_DIMENSIONS
            and tier.binding.size in LOT_DIMENSIONS
            and LOT_ORDER.index(tier.binding.size) < LOT_ORDER.index(building.plot)
        ):
            issues.append(
                Issue(
                    tier.location,
                    "binding.size-minimum",
                    f"binding Size={tier.binding.size!r} is smaller than building "
                    f"Plot minimum={building.plot!r}",
                )
            )
        if not building.blueprint:
            issues.append(
                Issue(tier.location, "building.blueprint", "plot building has no concrete Blueprint")
            )
        if tier.map_key not in model.maps:
            issues.append(Issue(tier.location, "reference.map", f"unknown tier map {tier.map_key!r}"))
        if tier.palette_key not in model.palettes:
            issues.append(
                Issue(tier.location, "reference.palette", f"unknown tier palette {tier.palette_key!r}")
            )
        fallbacks = [variant for variant in tier.variants if variant.fallback]
        if len(fallbacks) == 1:
            fallback = fallbacks[0]
            fallback_pair = (
                fallback.map_key or tier.map_key,
                fallback.palette_key or tier.palette_key,
            )
            for variant in tier.variants:
                effective_pair = (
                    variant.map_key or tier.map_key,
                    variant.palette_key or tier.palette_key,
                )
                if not variant.fallback and effective_pair == fallback_pair:
                    selectors = ", ".join(
                        f"{name}={variant.selectors[name]!r}"
                        for name in SELECTOR_ATTRIBUTES
                        if variant.selectors.get(name)
                    )
                    issues.append(
                        Issue(
                            variant.location,
                            "variant.no-op",
                            f"selector variant changes neither effective Map nor Palette versus fallback "
                            f"({fallback_pair[0]!r}, {fallback_pair[1]!r}); {selectors}",
                        )
                    )
                if (
                    not variant.fallback
                    and effective_pair[0] == fallback_pair[0]
                    and any(
                        variant.selectors.get(name)
                        for name in (
                            "Styles",
                            "Creeds",
                            "Cultures",
                            "Species",
                            "Genotypes",
                            "Bodies",
                        )
                    )
                ):
                    issues.append(
                        Issue(
                            variant.location,
                            "quality.selector-palette-only",
                            "style/creed/identity architecture variant reuses fallback topology; "
                            "author a distinct Map that changes circulation, use, access, or anchors",
                        )
                    )
        checked_effective_pairs: Set[Tuple[str, str]] = set()
        for variant in tier.variants:
            _validate_tier_variant(tier, variant, building, model, issues)
            effective_pair = (
                variant.map_key or tier.map_key,
                variant.palette_key or tier.palette_key,
            )
            if effective_pair in checked_effective_pairs:
                continue
            checked_effective_pairs.add(effective_pair)
            architecture_map = model.maps.get(effective_pair[0])
            palette = model.palettes.get(effective_pair[1])
            if architecture_map is not None and palette is not None:
                _validate_vertical_evidence(
                    architecture_map, palette, building, variant.location, issues
                )
                identity = _effective_architecture_snapshot(
                    architecture_map, palette, building
                )
                compiled_identities.setdefault(identity, []).append(
                    (
                        tier.build_key,
                        variant.key,
                        variant.location,
                        effective_pair[0],
                        effective_pair[1],
                    )
                )
                if _has_procedural_shell_signature(
                    architecture_map, palette, building
                ):
                    procedural_shells.setdefault(
                        (tier.build_key, effective_pair[0], effective_pair[1]), []
                    ).append((variant.key, variant.location))
    for key, building in sorted(plot_buildings.items()):
        if building.plot not in LOT_DIMENSIONS:
            continue
        # Civic-heart rungs are internal rite successors, not stakeable commission choices.
        # Every other plot size at or above the declared minimum is UI-reachable and therefore
        # needs its own exact typed binding. A missing larger map must never be hidden by nearest
        # or minimum-size fallback.
        sizes = (
            (building.plot,)
            if key in HEART_BUILD_KEYS
            else LOT_ORDER[LOT_ORDER.index(building.plot) :]
        )
        for size in sizes:
            count = build_counts[(key, building.category, size)]
            if count != 1:
                issues.append(
                    Issue(
                        building.location,
                        "coverage.exact-lot",
                        f"plot BuildKey {key!r} typed lot {building.category!r}/{size} must "
                        f"appear exactly once as a plan tier; found {count}",
                    )
                )
    _validate_heart_accretion(buildings, model, issues)
    for records in compiled_identities.values():
        by_build: Dict[str, List[Tuple[str, str, str, str]]] = {}
        for build_key, variant_key, location, map_key, palette_key in records:
            by_build.setdefault(build_key, []).append(
                (variant_key, location, map_key, palette_key)
            )
        if len(by_build) < 2:
            continue
        descriptions: List[str] = []
        locations: List[str] = []
        for build_key in sorted(by_build):
            variants = sorted(by_build[build_key])
            locations.extend(item[1] for item in variants)
            rendered = ",".join(
                f"{variant}[{map_key}+{palette_key}]"
                for variant, _location_text, map_key, palette_key in variants[:4]
            )
            if len(variants) > 4:
                rendered += f",+{len(variants) - 4} more"
            descriptions.append(f"{build_key}: {rendered}")
        preview = "; ".join(descriptions[:8])
        if len(descriptions) > 8:
            preview += f"; +{len(descriptions) - 8} BuildKeys more"
        issues.append(
            Issue(
                min(locations),
                "quality.architecture-alias",
                "different BuildKeys compile to byte-identical effective layout and anchor topology "
                f"without an Alias contract: {preview}",
            )
        )
    for (build_key, map_key, palette_key), variants in sorted(procedural_shells.items()):
        variant_names = ",".join(sorted(item[0] for item in variants))
        issues.append(
            Issue(
                min(item[1] for item in variants),
                "quality.procedural-shell",
                f"BuildKey {build_key!r} variants {variant_names!r} use {map_key!r}+{palette_key!r}: "
                "full claimed envelope, homogeneous border enclosure, one entrance, central main, "
                "and no interior fixture, partition, use cell, semantic role, or material/cover zoning",
            )
        )


def _find_base_root(path: Path) -> Optional[Path]:
    resolved = path.resolve()
    if resolved.is_file():
        if resolved.name != "ObjectBlueprints.xml":
            return None
        return resolved.parent
    candidates = [
        resolved,
        resolved / "Base",
        resolved / "StreamingAssets" / "Base",
        resolved / "CoQ_Data" / "StreamingAssets" / "Base",
    ]
    if resolved.name == "ObjectBlueprints":
        candidates.insert(0, resolved.parent)
    for candidate in candidates:
        if (candidate / "ObjectBlueprints.xml").is_file() and (
            candidate / "ObjectBlueprints"
        ).is_dir():
            return candidate
    return None


def _blueprint_files(base_root: Path) -> List[Path]:
    result = [base_root / "ObjectBlueprints.xml"]
    result.extend(sorted((base_root / "ObjectBlueprints").rglob("*.xml")))
    return result


_NUMERIC_ENTITY_RE = re.compile(r"&#(?:(?:x|X)([0-9A-Fa-f]+)|([0-9]+));")


def _valid_xml_character(codepoint: int) -> bool:
    return (
        codepoint in {0x9, 0xA, 0xD}
        or 0x20 <= codepoint <= 0xD7FF
        or 0xE000 <= codepoint <= 0xFFFD
        or 0x10000 <= codepoint <= 0x10FFFF
    )


def _repair_invalid_xml_characters(value: str) -> str:
    """Qud ships a few XML-1.0-forbidden control references; replace only those."""

    def replace_entity(match: re.Match[str]) -> str:
        raw = match.group(1) or match.group(2)
        codepoint = int(raw, 16 if match.group(1) is not None else 10)
        return match.group(0) if _valid_xml_character(codepoint) else "\uFFFD"

    repaired = _NUMERIC_ENTITY_RE.sub(replace_entity, value)
    return "".join(
        character if _valid_xml_character(ord(character)) else "\uFFFD"
        for character in repaired
    )


def _blueprint_data_from_file(
    path: Path,
    root: Path,
    issues: List[Issue],
    notices: List[Notice],
) -> Tuple[Set[str], List[ET.Element]]:
    location = _location(path, root)
    try:
        if path.is_symlink():
            issues.append(Issue(location, "input.symlink", "XML inputs must not be symbolic links"))
            return set(), []
        size = path.stat().st_size
        if size > MAX_XML_BYTES:
            issues.append(
                Issue(location, "input.size", f"XML is {size} bytes; cap is {MAX_XML_BYTES}")
            )
            return set(), []
        data = path.read_bytes()
    except OSError as error:
        issues.append(Issue(location, "input.read", str(error)))
        return set(), []
    upper = data.upper()
    if b"<!DOCTYPE" in upper or b"<!ENTITY" in upper:
        issues.append(Issue(location, "xml.dtd", "DTD and entity declarations are forbidden"))
        return set(), []
    try:
        xml = ET.fromstring(data)
    except ET.ParseError as error:
        # Shipped Qud data contains a few numeric control references forbidden by strict XML 1.0.
        # The game accepts them. Replace only invalid characters and retain full object records so
        # passability can still be inherited and checked; a names-only recovery is the last resort.
        decoded = data.decode("utf-8", errors="replace")
        repaired = _repair_invalid_xml_characters(decoded)
        try:
            xml = ET.fromstring(repaired)
        except ET.ParseError:
            xml = None
        if xml is not None:
            elements = list(xml.iter("object"))
            names = {
                element.get("Name", "").strip()
                for element in elements
                if element.get("Name", "").strip()
            }
            notices.append(
                Notice(
                    location,
                    "blueprint.xml-fallback",
                    f"strict XML parse failed ({error}); recovered {len(names)} object definitions tolerantly",
                )
            )
            return names, elements
        without_comments = re.sub(r"<!--.*?-->", "", decoded, flags=re.DOTALL)
        matches = re.finditer(
            r"<object\b[^>]*\bName\s*=\s*(?:\"([^\"]*)\"|'([^']*)')",
            without_comments,
            flags=re.IGNORECASE | re.DOTALL,
        )
        names = {
            html.unescape(match.group(1) if match.group(1) is not None else match.group(2)).strip()
            for match in matches
        }
        names.discard("")
        if names:
            notices.append(
                Notice(
                    location,
                    "blueprint.xml-fallback",
                    f"strict XML parse failed ({error}); recovered {len(names)} object names tolerantly",
                )
            )
            return names, []
        issues.append(Issue(location, "xml.parse", str(error)))
        return set(), []
    elements = list(xml.iter("object"))
    names = {
        element.get("Name", "").strip()
        for element in elements
        if element.get("Name", "").strip()
    }
    return names, elements


def _bool_attribute(value: str) -> Optional[bool]:
    folded = value.strip().lower()
    if folded in {"true", "yes", "1"}:
        return True
    if folded in {"false", "no", "0"}:
        return False
    return None


def _merge_blueprint_record(
    records: Dict[str, BlueprintRecord], element: ET.Element
) -> None:
    name = element.get("Name", "").strip()
    if not name:
        return
    record = records.get(name)
    if record is None:
        record = BlueprintRecord(name)
        records[name] = record
    if "Inherits" in element.attrib:
        record.parent = element.get("Inherits", "").strip()
    for child in element:
        part_name = child.get("Name", "").strip()
        if child.tag == "removepart":
            if part_name == "Physics":
                record.solid = False
            elif part_name == "Door":
                record.door = False
        elif child.tag == "part":
            if part_name == "Physics" and "Solid" in child.attrib:
                solid = _bool_attribute(child.get("Solid", ""))
                if solid is not None:
                    record.solid = solid
            elif part_name == "Door":
                record.door = True


def _resolve_blueprint_shapes(
    records: Mapping[str, BlueprintRecord], known: Set[str]
) -> Dict[str, BlueprintShape]:
    cache: Dict[str, BlueprintShape] = {}
    visiting: Set[str] = set()

    def resolve(name: str) -> BlueprintShape:
        cached = cache.get(name)
        if cached is not None:
            return cached
        if name in visiting:
            return BlueprintShape(None, None)
        record = records.get(name)
        if record is None:
            return BlueprintShape(None, None)
        visiting.add(name)
        if record.parent:
            parent = resolve(record.parent)
        else:
            parent = BlueprintShape(False, False)
        visiting.remove(name)
        shape = BlueprintShape(
            record.solid if record.solid is not None else parent.solid,
            record.door if record.door is not None else parent.door,
        )
        cache[name] = shape
        return shape

    for name in known:
        resolve(name)
    return cache


def load_blueprints(
    repo_root: Path,
    qud_base: Path,
    issues: List[Issue],
    notices: List[Notice],
) -> Tuple[Set[str], int, Dict[str, BlueprintShape]]:
    base_root = _find_base_root(qud_base)
    if base_root is None:
        issues.append(
            Issue(
                "--qud-base",
                "blueprint.base",
                "path does not resolve to Base/ObjectBlueprints.xml and Base/ObjectBlueprints/",
            )
        )
        return set(), 0, {}
    local_files = _discover(repo_root, "ObjectBlueprints*.xml")
    base_files = _blueprint_files(base_root)
    all_files = local_files + base_files
    if len(all_files) > MAX_BLUEPRINT_FILES:
        issues.append(
            Issue(
                "--qud-base",
                "cap.blueprint-files",
                f"{len(all_files)} blueprint XML files exceed cap {MAX_BLUEPRINT_FILES}",
            )
        )
        return set(), len(all_files), {}
    names: Set[str] = set()
    records: Dict[str, BlueprintRecord] = {}
    # Base declarations establish inheritance; local mod declarations then add or override it.
    all_files = base_files + local_files
    for path in all_files:
        parse_root = repo_root if path in local_files else base_root
        file_names, elements = _blueprint_data_from_file(
            path, parse_root, issues, notices
        )
        names.update(file_names)
        for element in elements:
            _merge_blueprint_record(records, element)
        if len(names) > MAX_BLUEPRINTS:
            issues.append(
                Issue(
                    "--qud-base",
                    "cap.blueprints",
                    f"blueprint count exceeds cap {MAX_BLUEPRINTS}",
                )
            )
            return names, len(all_files), _resolve_blueprint_shapes(records, names)
    return names, len(all_files), _resolve_blueprint_shapes(records, names)


def validate_blueprints(
    buildings: Dict[str, Building],
    model: ArchitectureModel,
    known: Set[str],
    issues: List[Issue],
) -> None:
    references: Dict[str, Set[str]] = {}

    def note(blueprint: str, location: str) -> None:
        if blueprint:
            references.setdefault(blueprint, set()).add(location)

    for palette in model.palettes.values():
        for slot in palette.slots.values():
            note(slot.blueprint, slot.location)
    for architecture_map in model.maps.values():
        for glyph in architecture_map.glyphs.values():
            for _layer, reference in glyph.layers():
                if reference and not reference.startswith("$"):
                    note(reference, glyph.location)
    for tier in model.tiers:
        building = buildings.get(tier.build_key)
        if building is not None:
            note(building.blueprint, tier.location)
    for blueprint in sorted(references):
        if blueprint not in known:
            locations = sorted(references[blueprint])
            issues.append(
                Issue(
                    locations[0],
                    "blueprint.missing",
                    f"concrete Blueprint {blueprint!r} does not resolve in local plus target Base/ObjectBlueprints",
                )
            )


def validate_passability(
    buildings: Dict[str, Building],
    model: ArchitectureModel,
    shapes: Mapping[str, BlueprintShape],
    issues: List[Issue],
) -> None:
    """Prove authored Pass labels against effective ObjectBlueprint inheritance."""

    checked_layouts: Set[Tuple[str, str, str]] = set()
    for tier in model.tiers:
        building = buildings.get(tier.build_key)
        if building is None:
            continue
        for variant in tier.variants:
            map_key = variant.map_key or tier.map_key
            palette_key = variant.palette_key or tier.palette_key
            architecture_map = model.maps.get(map_key)
            palette = model.palettes.get(palette_key)
            if architecture_map is None or palette is None:
                continue
            identity = (map_key, palette_key, building.blueprint)
            if identity in checked_layouts:
                continue
            checked_layouts.add(identity)
            checked_glyphs: Set[str] = set()
            for x, y, glyph in _cells(architecture_map):
                if glyph.char in checked_glyphs or glyph.pass_mode == "adjacent":
                    continue
                checked_glyphs.add(glyph.char)
                resolved: List[Tuple[str, str, BlueprintShape]] = []
                for layer, reference in glyph.layers():
                    blueprint = _resolve_reference(reference, palette, building)
                    if blueprint:
                        resolved.append(
                            (
                                layer,
                                blueprint,
                                shapes.get(blueprint, BlueprintShape(None, None)),
                            )
                        )
                if not resolved:
                    continue
                any_door = any(shape.door is True for _layer, _name, shape in resolved)
                any_solid = any(shape.solid is True for _layer, _name, shape in resolved)
                unknown_solid = [
                    name for _layer, name, shape in resolved if shape.solid is None
                ]
                unknown_door = [
                    name for _layer, name, shape in resolved if shape.door is None
                ]
                rendered = ", ".join(
                    f"{layer}={name!r}" for layer, name, _shape in resolved
                )
                prefix = (
                    f"map {map_key!r} glyph {glyph.char!r} at first cell {x},{y} "
                    f"({rendered})"
                )
                if glyph.pass_mode == "walk":
                    if any_door:
                        continue
                    if unknown_solid or (any_solid and unknown_door):
                        unknown = sorted(set(unknown_solid + unknown_door))
                        issues.append(
                            Issue(
                                variant.location,
                                "passability.unknown",
                                prefix + " cannot resolve inherited Physics.Solid/Door truth for "
                                + ", ".join(repr(name) for name in unknown),
                            )
                        )
                    elif any_solid:
                        issues.append(
                            Issue(
                                variant.location,
                                "passability.walk-solid",
                                prefix + " declares Pass=walk but contains a solid non-door",
                            )
                        )
                elif glyph.pass_mode == "blocked":
                    if any_door:
                        issues.append(
                            Issue(
                                variant.location,
                                "passability.blocked-door",
                                prefix + " declares Pass=blocked but contains an authored Door",
                            )
                        )
                    elif unknown_solid or unknown_door:
                        unknown = sorted(set(unknown_solid + unknown_door))
                        issues.append(
                            Issue(
                                variant.location,
                                "passability.unknown",
                                prefix + " cannot resolve inherited Physics.Solid/Door truth for "
                                + ", ".join(repr(name) for name in unknown),
                            )
                        )
                    elif not any_solid:
                        issues.append(
                            Issue(
                                variant.location,
                                "passability.blocked-open",
                                prefix + " declares Pass=blocked but every layer is physically open",
                            )
                        )


def _golden_name(tier: Tier, variant: Variant, pose: str) -> str:
    raw = (
        f"{tier.build_key}__{tier.binding.type_key}-{tier.binding.size.lower()}"
        f"__{variant.key}__{pose}"
    )
    safe = re.sub(r"[^A-Za-z0-9_.-]+", "_", raw).strip("._") or "architecture"
    return safe + ".txt"


def _posed_rows(architecture_map: ArchitectureMap, pose: str) -> Tuple[str, ...]:
    width = architecture_map.width
    height = architecture_map.height
    world_width = height if pose in {"east", "west"} else width
    world_height = width if pose in {"east", "west"} else height
    result = [["." for _x in range(world_width)] for _y in range(world_height)]
    for v, row in enumerate(architecture_map.rows):
        for u, char in enumerate(row):
            if pose == "north":
                x, y = u, v
            elif pose == "east":
                x, y = height - 1 - v, u
            elif pose == "south":
                x, y = width - 1 - u, height - 1 - v
            elif pose == "west":
                x, y = v, width - 1 - u
            else:
                raise ValueError(f"unknown architecture pose {pose!r}")
            result[y][x] = char
    return tuple("".join(row) for row in result)


def _resolved_layer(reference: str, palette: Palette, building: Building) -> str:
    resolved = _resolve_reference(reference, palette, building)
    return resolved if resolved else "-"


def _render_golden(
    tier: Tier,
    variant: Variant,
    building: Building,
    architecture_map: ArchitectureMap,
    palette: Palette,
    pose: str,
) -> str:
    posed_rows = _posed_rows(architecture_map, pose)
    lines = [
        "architecture-golden-v2",
        f"plan: {tier.binding.plan.key}",
        f"binding: {tier.binding.key}",
        f"type: {tier.binding.type_key}",
        f"size: {tier.binding.size}",
        f"facing-rule: {tier.binding.facing}",
        f"pose: {pose}",
        f"tier: {tier.key}",
        f"level: {tier.level}",
        f"build-key: {tier.build_key}",
        f"building-blueprint: {building.blueprint}",
        f"variant: {variant.key}",
        f"fallback: {'yes' if variant.fallback else 'no'}",
        f"map: {architecture_map.key}",
        f"palette: {palette.key}",
        f"canonical-dimensions: {architecture_map.width}x{architecture_map.height}",
        f"posed-dimensions: {len(posed_rows[0])}x{len(posed_rows)}",
        f"default-cover: {architecture_map.default_cover}",
        "rows:",
    ]
    lines.extend(posed_rows)
    lines.append("legend:")
    lines.append(". ground=- structure=- object=- claim=unclaimed pass=walk cover=open anchors=- stateful=no")
    for char in sorted(architecture_map.glyphs):
        glyph = architecture_map.glyphs[char]
        anchors = ",".join(glyph.anchors) or "-"
        lines.append(
            f"{char} "
            f"ground={_resolved_layer(glyph.ground, palette, building)} "
            f"structure={_resolved_layer(glyph.structure, palette, building)} "
            f"object={_resolved_layer(glyph.object, palette, building)} "
            f"claim={glyph.claim} pass={glyph.pass_mode} cover={glyph.cover} "
            f"anchors={anchors} stateful={'yes' if glyph.stateful else 'no'}"
        )
    lines.append("requirements:")
    if tier.requirements:
        for requirement in sorted(tier.requirements, key=lambda item: item.role):
            lines.append(
                f"{requirement.role} min={requirement.minimum} max={requirement.maximum}"
            )
    else:
        lines.append("-")
    lines.append("selectors:")
    selector_lines = [
        f"{name}={variant.selectors[name]}"
        for name in SELECTOR_ATTRIBUTES
        if variant.selectors.get(name)
    ]
    lines.extend(selector_lines or ["-"])
    return "\n".join(lines) + "\n"


def make_goldens(
    buildings: Dict[str, Building], model: ArchitectureModel, issues: List[Issue]
) -> Dict[str, str]:
    result: Dict[str, str] = {}
    total_bytes = 0
    for tier in sorted(model.tiers, key=lambda item: (item.build_key, item.key)):
        building = buildings.get(tier.build_key)
        if building is None:
            continue
        for variant in sorted(tier.variants, key=lambda item: (item.priority, item.key)):
            map_key = variant.map_key or tier.map_key
            palette_key = variant.palette_key or tier.palette_key
            architecture_map = model.maps.get(map_key)
            palette = model.palettes.get(palette_key)
            if architecture_map is None or palette is None:
                continue
            for pose in POSES:
                name = _golden_name(tier, variant, pose)
                if name in result:
                    issues.append(
                        Issue(
                            variant.location,
                            "golden.collision",
                            f"golden filename {name!r} collides with another tier variant pose",
                        )
                    )
                    continue
                content = _render_golden(
                    tier, variant, building, architecture_map, palette, pose
                )
                result[name] = content
                total_bytes += len(content.encode("utf-8"))
    if len(result) > MAX_GOLDENS:
        issues.append(
            Issue("goldens", "cap.goldens", f"{len(result)} goldens exceed cap {MAX_GOLDENS}")
        )
    if total_bytes > MAX_GOLDEN_BYTES:
        issues.append(
            Issue(
                "goldens",
                "cap.golden-bytes",
                f"goldens total {total_bytes} bytes; cap is {MAX_GOLDEN_BYTES}",
            )
        )
    return result


def _prepare_output_directory(path: Path) -> Path:
    if path.is_symlink():
        raise OutputDirectoryError("output directory must not be a symbolic link")
    if not path.exists() or not path.is_dir():
        raise OutputDirectoryError("output directory must already exist and be a directory")
    try:
        if any(path.iterdir()):
            raise OutputDirectoryError("output directory must be empty")
    except OSError as error:
        raise OutputDirectoryError(str(error)) from error
    return path.resolve()


def _write_goldens(path: Path, goldens: Mapping[str, str]) -> None:
    for name in sorted(goldens):
        target = path / name
        with target.open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(goldens[name])


def run_check(
    repo_root: Path,
    qud_base: Optional[Path] = None,
    output_dir: Optional[Path] = None,
) -> CheckResult:
    repo_root = repo_root.resolve()
    if not repo_root.is_dir():
        raise ValueError("repo root must be an existing directory")
    prepared_output = _prepare_output_directory(output_dir) if output_dir is not None else None
    issues: List[Issue] = []
    notices: List[Notice] = []
    building_files = _discover(repo_root, "KingdomBuildings.xml")
    architecture_files = _discover(repo_root, "KingdomArchitectures*.xml")
    if len(building_files) > MAX_XML_FILES:
        issues.append(
            Issue(
                "KingdomBuildings.xml",
                "cap.files",
                f"{len(building_files)} files exceed cap {MAX_XML_FILES}",
            )
        )
    if len(architecture_files) > MAX_XML_FILES:
        issues.append(
            Issue(
                "KingdomArchitectures*.xml",
                "cap.files",
                f"{len(architecture_files)} files exceed cap {MAX_XML_FILES}",
            )
        )
    if not building_files:
        issues.append(Issue("KingdomBuildings.xml", "input.missing", "no building catalogue found"))
    if not architecture_files:
        issues.append(
            Issue("KingdomArchitectures*.xml", "input.missing", "no architecture files found")
        )
    buildings = load_buildings(building_files[:MAX_XML_FILES], repo_root, issues)
    model = load_architectures(architecture_files[:MAX_XML_FILES], repo_root, issues)
    validate_model(buildings, model, issues)
    maximum_payload, maximum_encoded = _snapshot_maxima(buildings, model)
    blueprint_resolution = "skipped"
    blueprint_count = 0
    if qud_base is not None:
        known_blueprints, _file_count, blueprint_shapes = load_blueprints(
            repo_root, qud_base, issues, notices
        )
        blueprint_count = len(known_blueprints)
        blueprint_resolution = "checked"
        if known_blueprints:
            validate_blueprints(buildings, model, known_blueprints, issues)
            validate_passability(buildings, model, blueprint_shapes, issues)
    goldens = make_goldens(buildings, model, issues)
    issues = sorted(set(issues))
    written = False
    if prepared_output is not None and not issues:
        _write_goldens(prepared_output, goldens)
        written = True
    return CheckResult(
        repo_root,
        building_files,
        architecture_files,
        buildings,
        model,
        issues,
        sorted(set(notices)),
        blueprint_resolution,
        blueprint_count,
        goldens,
        written,
        maximum_payload,
        maximum_encoded,
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Validate all KingdomBuildings.xml and KingdomArchitectures*.xml files"
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="repository root (default: parent of Tools)",
    )
    parser.add_argument(
        "--qud-base",
        type=Path,
        help="Qud game root, StreamingAssets/Base, ObjectBlueprints directory, or ObjectBlueprints.xml",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="existing empty directory to receive deterministic ASCII goldens",
    )
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    arguments = _parser().parse_args(argv)
    try:
        result = run_check(arguments.repo_root, arguments.qud_base, arguments.output_dir)
    except (OutputDirectoryError, ValueError) as error:
        print("ARCHITECTURE CHECK v1")
        print(f"ERROR [output] --output-dir: {error}")
        print("RESULT: FAIL")
        return 2
    print(result.report(), end="")
    return 0 if result.ok else 1


if __name__ == "__main__":
    sys.exit(main())
