#!/usr/bin/env python3
"""Independent full-corpus proof for physical settlement benefit content."""

from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Dict, Iterable, List, Mapping, MutableMapping, Sequence, Tuple

from benefit_provider_manifest import (
    BUILDING_FIXTURES, NATIVE_PORTABLES, PROVIDER_DESCRIPTIONS, YARD_FIXTURES,
)


FOOD_WATER = {"food", "water"}
PROVIDER_PARTS = {"r_KingdomBenefitProvider", "r_KingdomStateBenefitProvider"}
CAPABILITY_ROLES = {
    "taf:cooking": {"fire", "oven", "kyakukyaspicehearth", "mopangorefugekitchen"},
    "taf:shrine": {"shrine", "shrinegarth", "temple", "reliquary",
                   "baetylofferingframe", "naphtaaliscrapaltar", "girshrotchapel",
                   "gyrewightashcourt", "mamontithecistern"},
    "taf:education": {"scriptorium", "entropyblind", "seekersquietcell",
                      "chavvahboughschool"},
    "taf:inquiry": {"scriptorium", "entropyblind", "seekersquietcell",
                    "chavvahboughschool"},
}
EXPECTED_CATALOGUE_ROWS = 114
EXPECTED_YARD_ROWS = 4


class AuditError(RuntimeError):
    pass


def parse(path: Path) -> ET.Element:
    return ET.parse(path).getroot()


def tally(raw: str) -> Dict[str, int]:
    result: Dict[str, int] = {}
    if not (raw or "").strip():
        return result
    for token in raw.split(","):
        pair = token.strip().lower().split(":")
        if len(pair) != 2 or not pair[0] or not pair[1].isdigit() or int(pair[1]) <= 0:
            raise AuditError(f"malformed tally {raw!r}")
        result[pair[0]] = result.get(pair[0], 0) + int(pair[1])
    return result


def tags(raw: str) -> set[str]:
    return {item.strip().lower() for item in (raw or "").split(",") if item.strip()}


class Blueprints:
    def __init__(self, root: ET.Element):
        self.nodes = {node.get("Name", ""): node for node in root.findall("object")}
        self.part_cache: Dict[str, Dict[str, Mapping[str, str]]] = {}
        self.tag_cache: Dict[str, Dict[str, str]] = {}

    def parts(self, name: str, trail: Tuple[str, ...] = ()) -> Dict[str, Mapping[str, str]]:
        if name in self.part_cache:
            return dict(self.part_cache[name])
        if name in trail:
            raise AuditError("blueprint inheritance cycle: " + " -> ".join(trail + (name,)))
        node = self.nodes.get(name)
        if node is None:
            return {}
        result = self.parts(node.get("Inherits", ""), trail + (name,))
        for removed in node.findall("removepart"):
            result.pop(removed.get("Name", ""), None)
        for part in node.findall("part"):
            result[part.get("Name", "")] = dict(part.attrib)
        self.part_cache[name] = dict(result)
        return result

    def tags(self, name: str, trail: Tuple[str, ...] = ()) -> Dict[str, str]:
        if name in self.tag_cache:
            return dict(self.tag_cache[name])
        if name in trail:
            raise AuditError("blueprint tag inheritance cycle: " + " -> ".join(trail + (name,)))
        node = self.nodes.get(name)
        if node is None:
            return {}
        result = self.tags(node.get("Inherits", ""), trail + (name,))
        for removed in node.findall("removetag"):
            result.pop(removed.get("Name", ""), None)
        for tag in node.findall("tag"):
            result[tag.get("Name", "")] = tag.get("Value", "")
        self.tag_cache[name] = dict(result)
        return result


def architecture(repo: Path):
    maps: Dict[str, ET.Element] = {}
    palettes: Dict[str, Dict[str, ET.Element]] = {}
    contexts: set[Tuple[str, str, str]] = set()
    for path in sorted((repo / "Architecture").glob("KingdomArchitectures*.xml")):
        if path.name == "KingdomArchitectures-LotRealizations.xml":
            continue
        root = parse(path)
        for node in root.findall("map"):
            key = node.get("Key", "")
            if key in maps:
                raise AuditError(f"duplicate authored map {key!r}")
            maps[key] = node
        for node in root.findall("palette"):
            key = node.get("Key", "")
            if key in palettes:
                raise AuditError(f"duplicate authored palette {key!r}")
            palettes[key] = {slot.get("Key", ""): slot for slot in node.findall("slot")}
        for tier in root.findall("./plan/binding/tier"):
            building = tier.get("BuildKey", "")
            base_map = tier.get("Map", "")
            base_palette = tier.get("Palette", "")
            contexts.add((building, base_map, base_palette))
            for variant in tier.findall("variant"):
                contexts.add((building, variant.get("Map") or base_map,
                              variant.get("Palette") or base_palette))
    return maps, palettes, sorted(contexts)


def accepts(scope: str, glyph: ET.Element) -> bool:
    claim = glyph.get("Claim", "").lower()
    cover = glyph.get("Cover", "open").lower()
    passage = glyph.get("Pass", "blocked").lower()
    scope = scope.lower()
    if scope == "building":
        return claim == "building"
    if scope == "yard":
        return claim == "yard"
    if scope == "covered":
        return cover != "open"
    if scope in {"interior", "habitable"}:
        return claim == "building" and cover != "open" and passage != "blocked"
    if scope == "plot":
        return claim in {"building", "yard"}
    return False


def context_objects(building: str, architecture_map: ET.Element,
                    palette: Mapping[str, ET.Element], root_blueprint: str):
    counts = Counter("".join(row.get("Cells", "") for row in architecture_map.findall("row")))
    for glyph in architecture_map.findall("glyph"):
        count = counts[glyph.get("Char", "")]
        reference = glyph.get("Object", "")
        if count <= 0 or not reference:
            continue
        if reference == "$building":
            blueprint = root_blueprint
        elif reference.startswith("$"):
            slot = palette.get(reference[1:])
            if slot is None:
                raise AuditError(f"{building}/{architecture_map.get('Key')} misses slot {reference}")
            blueprint = slot.get("Blueprint", "")
        else:
            blueprint = reference
        yield glyph, blueprint, count, reference


def explicit_declaration(part_name: str, part: Mapping[str, str]):
    operation = "custom" if part_name == "r_KingdomStateBenefitProvider" \
        else part.get("Operation", "present").lower()
    return tally(part.get("Carries", "")), tags(part.get("Provides", "")), \
        part.get("Scope", "building").lower(), operation


def description_contract(blueprints: Blueprints, failures: List[str]) -> None:
    fixtures = BUILDING_FIXTURES + YARD_FIXTURES
    expected = {spec.blueprint for spec in fixtures}
    if set(PROVIDER_DESCRIPTIONS) != expected:
        failures.append("authored provider description inventory does not match fixture inventory")

    descriptions = [spec.description.strip() for spec in fixtures]
    if any(not 120 <= len(text) <= 260 for text in descriptions):
        failures.append("provider descriptions must be 120-260 authored characters")
    duplicates = [text for text, count in Counter(text.casefold() for text in descriptions).items()
                  if count > 1]
    if duplicates:
        failures.append("provider descriptions contain duplicate prose")
    openings = Counter(tuple(re.findall(r"[a-z]+", text.casefold())[:4])
                       for text in descriptions)
    if openings and max(openings.values()) > 2:
        failures.append("provider descriptions over-repeat an identical four-word opening")

    forbidden = (
        "physical fixture", "settlement inspects", "room name", "marker cannot",
        "built from local", "blueprint", "provider key", "build key",
        "implementation metadata", "catalogue identity", "api",
    )
    state_words = {
        "HeldFreshWater": (("fresh water",), ("empty", "dry")),
        "HeldFreshWaterAndStaffed": (
            ("fresh water",), ("empty", "dry"),
            ("tender", "reader", "attendant", "keeper", "hands"),
        ),
        "OpenFreshWater": (("fresh water",), ("empty", "dry"), ("open", "openly")),
        "WetOffal": (("corpse-stock",), ("liquid",), ("empty",)),
        "OpenBrine": (("brine",), ("empty",)),
        "RootSown": (("unsown",), ("sown", "root"), ("dry",)),
        "MirrorPair": (("unpaired",), ("both", "two"), ("powered",), ("reciprocal",)),
    }
    for spec in fixtures:
        lowered = spec.description.casefold()
        bad = [phrase for phrase in forbidden if phrase in lowered]
        if bad:
            failures.append(f"{spec.blueprint} description exposes internal prose {bad}")
        if len(re.findall(r"[.!?](?:\s|$)", spec.description)) < 2:
            failures.append(f"{spec.blueprint} description needs complete authored sentences")
        for alternatives in state_words.get(spec.state, ()):
            if not any(word in lowered for word in alternatives):
                failures.append(f"{spec.blueprint} description does not explain {spec.state}")
        if spec.state and not any(word in lowered for word in (
                "construction", "built", "installed", "raised", "finished",
                "completed", "begins", "begin", "starts")):
            failures.append(f"{spec.blueprint} description omits its construction state")
        words = set(re.findall(r"[a-z]+", lowered))
        if spec.native_part and ("newly installed" not in lowered
                                 or not {"capacitor", "charge"}.issubset(words)):
            failures.append(f"{spec.blueprint} description does not explain its live charger")
        if spec.operation.casefold() == "powered" and not {"capacitor", "power"}.issubset(words):
            failures.append(f"{spec.blueprint} description does not explain powered operation")
        if spec in BUILDING_FIXTURES:
            actual = blueprints.parts(spec.blueprint).get("Description", {}).get("Short", "")
            if actual != spec.description:
                failures.append(f"{spec.blueprint} generated description drifted from manifest")

    print(f"provider prose: {len(descriptions)} authored descriptions; "
          f"{len(set(text.casefold() for text in descriptions))} unique")


def aggregate_context(building: str, architecture_map: ET.Element,
                      palette: Mapping[str, ET.Element], root_blueprint: str,
                      blueprints: Blueprints, failures: List[str]):
    amounts: MutableMapping[str, int] = defaultdict(int)
    explicit_tags: set[str] = set()
    native_tags: set[str] = set()
    installed: Counter[str] = Counter()
    for glyph, blueprint, count, reference in context_objects(
            building, architecture_map, palette, root_blueprint):
        if blueprint not in blueprints.nodes:
            # Vanilla blueprint. It cannot carry one of this mod's explicit provider parts;
            # every native capability used by this corpus has an explicit local wrapper.
            continue
        installed[blueprint] += count
        parts = blueprints.parts(blueprint)
        explicit_roof = False
        for part_name in PROVIDER_PARTS:
            if part_name not in parts:
                continue
            anchors = {item for item in glyph.get("Anchors", "").split(",") if item}
            custody = {item for item in anchors if item.startswith("benefit:")}
            if len(custody) != 1:
                failures.append(f"{building}/{architecture_map.get('Key')} provider {blueprint} "
                                f"needs one physical custody anchor, found {sorted(custody)}")
            if not anchors - custody:
                failures.append(f"{building}/{architecture_map.get('Key')} provider {blueprint} "
                                "has no authored functional anchor")
            if anchors.intersection({"purpose:machine", "fixture:first-basin"}):
                failures.append(f"{building}/{architecture_map.get('Key')} provider {blueprint} "
                                "displaced a runtime-owned fixture")
            if reference == "$building":
                failures.append(f"{building}/{architecture_map.get('Key')} grants benefits "
                                "through its catalogue root")
            offered, offered_tags, scope, _operation = explicit_declaration(
                part_name, parts[part_name])
            if part_name == "r_KingdomStateBenefitProvider" \
                    and glyph.get("Stateful", "no").lower() != "yes":
                failures.append(f"{building}/{architecture_map.get('Key')} custom provider "
                                f"{blueprint} lacks stateful custody")
            if not accepts(scope, glyph) or glyph.get("Pass", "blocked").lower() == "blocked":
                failures.append(f"{building}/{architecture_map.get('Key')} places {blueprint} "
                                f"outside live {scope} scope")
                continue
            if count != 1:
                failures.append(f"{building}/{architecture_map.get('Key')} repeats semantic "
                                f"fixture {blueprint} x{count}")
            for kind, amount in offered.items():
                if kind in FOOD_WATER:
                    failures.append(f"{blueprint} declares custody-only {kind}")
                amounts[kind] += amount * count
                explicit_roof |= kind == "roof"
            explicit_tags.update(offered_tags)
        interior = accepts("interior", glyph)
        building_cell = accepts("building", glyph)
        if "Bed" in parts and not explicit_roof and accepts("habitable", glyph):
            amounts["roof"] += count
        plot = accepts("plot", glyph)
        if "Campfire" in parts and plot:
            native_tags.add("taf:cooking")
        if "Shrine" in parts and plot:
            native_tags.add("taf:shrine")
        if "Shrine" in parts and interior:
            amounts["spirit"] += count
        if "MarkovBookshelf" in parts and interior:
            amounts["learning"] += count
            native_tags.add("taf:education")
        if "UniversalCharger" in parts and building_cell:
            native_tags.add("taf:charge")
        if "LiquidVolume" in parts and building_cell:
            native_tags.add("taf:damp")
            try:
                if int(parts["LiquidVolume"].get("MaxVolume", "0")) < 0:
                    native_tags.add("taf:openwater")
            except ValueError:
                failures.append(f"{blueprint} has malformed LiquidVolume MaxVolume")
    return dict(amounts), explicit_tags, native_tags, installed


def provider_contracts(repo: Path, rows: Mapping[str, ET.Element], blueprints: Blueprints,
                       contexts, maps, palettes, failures: List[str]) -> None:
    specs = defaultdict(list)
    for spec in BUILDING_FIXTURES:
        specs[spec.building].append(spec)
    expected_keys: set[str] = set()
    usages: MutableMapping[str, set[str]] = defaultdict(set)
    context_count = 0
    relevant = set()
    for key, row in rows.items():
        caps = {kind: amount for kind, amount in tally(row.get("Carries", "")).items()
                if kind not in FOOD_WATER}
        declared_tags = tags(row.get("Provides", ""))
        if caps or declared_tags:
            relevant.add(key)
    if len(relevant) != EXPECTED_CATALOGUE_ROWS:
        failures.append(f"catalogue census is {len(relevant)}, expected {EXPECTED_CATALOGUE_ROWS}")

    seen_buildings = set()
    for building, map_key, palette_key in contexts:
        row = rows.get(building)
        architecture_map = maps.get(map_key)
        palette = palettes.get(palette_key)
        if row is None or architecture_map is None or palette is None:
            failures.append(f"broken architecture context {building}/{map_key}/{palette_key}")
            continue
        amounts, explicit_tags, native_tags, installed = aggregate_context(building, architecture_map,
            palette, row.get("Blueprint", ""), blueprints, failures)
        expected_amounts = {kind: amount for kind, amount in tally(row.get("Carries", "")).items()
                            if kind not in FOOD_WATER}
        expected_tags = tags(row.get("Provides", ""))
        if amounts != expected_amounts:
            failures.append(f"{building}/{map_key}/{palette_key} physical amounts {amounts} "
                            f"!= caps {expected_amounts}")
        supplied_tags = explicit_tags | (native_tags & expected_tags)
        if explicit_tags - expected_tags:
            failures.append(f"{building}/{map_key}/{palette_key} explicitly overclaims tags "
                            f"{sorted(explicit_tags - expected_tags)}")
        if supplied_tags != expected_tags:
            failures.append(f"{building}/{map_key}/{palette_key} physical tags "
                            f"{sorted(supplied_tags)} != caps {sorted(expected_tags)}")
        for spec in specs.get(building, ()):
            count = installed[spec.blueprint]
            if count != 1:
                failures.append(f"{building}/{map_key}/{palette_key} installs "
                                f"{spec.blueprint} x{count}, expected 1")
            usages[spec.blueprint].add(building)
        if building in relevant:
            seen_buildings.add(building)
        context_count += 1
    missing_context = sorted(relevant - seen_buildings)
    if missing_context:
        failures.append("relevant catalogue rows lack authored contexts: " + ", ".join(missing_context))

    stocked = {node.get("Blueprint", "") for table in
               parse(repo / "RuntimeData" / "PopulationTables.xml").findall("population")
               for node in table.findall("object")}
    state_source = (repo / "Growth" / "r_KingdomStateBenefitProvider.cs").read_text()
    supported_states = set(re.findall(r'case "([A-Za-z]+)"', state_source))
    provider_keys: set[str] = set()
    for spec in BUILDING_FIXTURES:
        expected_keys.add(spec.blueprint)
        parts = blueprints.parts(spec.blueprint)
        part_name = "r_KingdomStateBenefitProvider" if spec.state else \
            "UniversalCharger" if spec.native_part else "r_KingdomBenefitProvider"
        if part_name not in parts:
            failures.append(f"{spec.blueprint} lacks {part_name}")
            continue
        if spec.native_part:
            continue
        part = parts[part_name]
        offered, offered_tags, scope, operation = explicit_declaration(part_name, part)
        if offered != tally(spec.carries) or offered_tags != tags(spec.provides) \
                or scope != spec.scope.lower() or operation != spec.operation.lower():
            failures.append(f"{spec.blueprint} declaration drifted from reviewed manifest")
        key = part.get("ProviderKey", "")
        if not key or key in provider_keys:
            failures.append(f"{spec.blueprint} has missing/shared provider key {key!r}")
        provider_keys.add(key)
        if spec.state and spec.state not in supported_states:
            failures.append(f"{spec.blueprint} names unsupported custom state {spec.state}")
        if spec.operation.lower() == "staffed" and int(rows[spec.building].get("Staff", "0")) <= 0:
            failures.append(f"{spec.blueprint} is Staffed without a catalogue staffing contract")
        if spec.operation.lower() == "powered" and "Capacitor" not in parts:
            failures.append(f"{spec.blueprint} is Powered without a physical charge sink")
        semantic_tags = blueprints.tags(spec.blueprint)
        if semantic_tags.get("r_KingdomProviderBuildKey") != spec.building \
                or semantic_tags.get("r_KingdomProviderComponent") != spec.component:
            failures.append(f"{spec.blueprint} lacks exact build/component provenance tags")
        material = semantic_tags.get("r_KingdomProviderMaterial", "")
        tech = semantic_tags.get("r_KingdomProviderMinTech", "")
        if tech == "hands" and material in {"scrap", "workedmetal"}:
            failures.append(f"{spec.blueprint} introduces metal at hands tech")
        if spec.portable:
            portable = spec.blueprint + "Portable"
            physics = blueprints.parts(portable).get("Physics", {})
            if portable not in stocked or physics.get("Takeable", "").lower() != "true" \
                    or blueprints.tags(portable).get("r_KingdomPortableProvider") != "yes":
                failures.append(f"{spec.blueprint} lacks a takeable stocked semantic sibling")
        elif not spec.nonportable_reason:
            failures.append(f"{spec.blueprint} is nonportable without a reviewed reason")
        if usages[spec.blueprint] != {spec.building}:
            failures.append(f"{spec.blueprint} semantic map use is {sorted(usages[spec.blueprint])}")

    for source, portable, _tier in NATIVE_PORTABLES:
        if source not in blueprints.nodes or portable not in stocked \
                or blueprints.parts(portable).get("Physics", {}).get("Takeable", "").lower() != "true":
            failures.append(f"native capability {source} lacks stocked takeable route {portable}")
    for key, row in rows.items():
        root_parts = blueprints.parts(row.get("Blueprint", ""))
        if PROVIDER_PARTS.intersection(root_parts):
            failures.append(f"catalogue root {key} carries a semantic provider part")
    print(f"provider content: {len(relevant)} catalogue rows, {context_count} variants, "
          f"{len(BUILDING_FIXTURES)} unique fixtures")


def yard_contract(repo: Path, blueprints: Blueprints, failures: List[str]) -> None:
    root = parse(repo / "RuntimeData" / "KingdomYardWorks.xml")
    rows = {row.get("Key", ""): row for row in root.findall("yardwork")}
    if len(rows) != EXPECTED_YARD_ROWS or set(rows) != {
            "vinelattice", "hiderack", "dyevat", "vellumpress"}:
        failures.append(f"yard census is {sorted(rows)}, expected four reviewed roles")
    if rows.get("vinelattice", {}).get("Shades") != "food:1" \
            or rows.get("dyevat", {}).get("Goods", "").lower() != "yes":
        failures.append("vine food or dye goods physical lane drifted")
    for spec in YARD_FIXTURES:
        row = rows.get(spec.building.split(":", 1)[1])
        parts = blueprints.parts(spec.blueprint)
        part = parts.get("r_KingdomBenefitProvider")
        if row is None or part is None:
            failures.append(f"yard role {spec.building} lacks exact provider object")
            continue
        offered, offered_tags, scope, operation = explicit_declaration(
            "r_KingdomBenefitProvider", part)
        if offered != tally(spec.carries) or offered_tags or scope != "yard" \
                or operation != "present" or row.get("Blueprint") != spec.blueprint:
            failures.append(f"yard role {spec.building} provider contract drifted")
    for key in ("vinelattice", "dyevat"):
        if PROVIDER_PARTS.intersection(blueprints.parts(rows[key].get("Blueprint", ""))):
            failures.append(f"yard {key} leaked into generic benefit providers")


def hosted_contract(repo: Path, failures: List[str]) -> None:
    source = (repo / "World" / "KingdomHostedArcologyProgrammeBuilder.cs").read_text()
    match = re.search(r"Ward = .*?\{(.*?)\};\s*\n\s*private static readonly .*? Terrace",
                      source, re.DOTALL)
    if match is None:
        failures.append("hosted Ward fixture manifest is unreadable")
        return
    ward = match.group(1)
    if ward.count('"r_KingdomFixtureBedMetal"') != 8 \
            or ward.count('"r_KingdomArcologyWardAmenity"') != 1:
        failures.append("hosted Ward must contain exactly eight beds and one amenity")


def capability_contract(repo: Path, rows: Mapping[str, ET.Element],
                        failures: List[str]) -> None:
    for capability, expected in CAPABILITY_ROLES.items():
        actual = {key for key, row in rows.items()
                  if capability in tags(row.get("Provides", ""))}
        if actual != expected:
            failures.append(f"{capability} catalogue roles {sorted(actual)} != "
                            f"{sorted(expected)}")
        fixture_roles = {spec.building for spec in BUILDING_FIXTURES
                         if capability in tags(spec.provides)}
        if fixture_roles != expected:
            failures.append(f"{capability} fixture roles {sorted(fixture_roles)} != "
                            f"{sorted(expected)}")
    native = (repo / "Growth" / "KingdomBenefitIndex.Native.cs").read_text()
    required = ("HasPart(\"Campfire\")", "KingdomBenefitCapabilities.Cooking",
                "KingdomBenefitCapabilities.Shrine",
                "KingdomBenefitCapabilities.Education",
                "KingdomBenefitOperation.Staffed")
    for token in required:
        if token not in native:
            failures.append(f"native capability adapter lacks {token}")
    if "KingdomBenefitCapabilities.Inquiry" in native:
        failures.append("generic native furniture must not become an inquiry bench")


def main(argv: Sequence[str]) -> int:
    repo = Path(argv[1]).resolve() if len(argv) > 1 else Path(__file__).resolve().parents[1]
    failures: List[str] = []
    try:
        buildings = parse(repo / "RuntimeData" / "KingdomBuildings.xml")
        rows = {row.get("Key", ""): row for row in buildings.findall("building")}
        blueprints = Blueprints(parse(repo / "RuntimeData" / "ObjectBlueprints.xml"))
        maps, palettes, contexts = architecture(repo)
        description_contract(blueprints, failures)
        provider_contracts(repo, rows, blueprints, contexts, maps, palettes, failures)
        yard_contract(repo, blueprints, failures)
        hosted_contract(repo, failures)
        capability_contract(repo, rows, failures)
    except (AuditError, ET.ParseError, OSError, ValueError) as error:
        failures.append(str(error))
    if failures:
        for failure in failures:
            print("benefit provider content failed: " + failure, file=sys.stderr)
        return 1
    print("benefit provider content clean: exact caps, scopes, operations, and acquisition")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
