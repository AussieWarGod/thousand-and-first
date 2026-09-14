"""Inventory shipped building configurations through the existing architecture parser.

This enumerates declared configurations, not all possible world states and not native
acceptance. XML roots, rather than filenames, identify staged extension streams.
"""

from dataclasses import asdict
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys


OBLIGATIONS = (
    "architecture", "construction", "operation", "failure-recovery",
    "upgrades-removal", "save-cold-load", "multi-map",
)
POSES = ("north", "east", "south", "west")


def digest(value):
    encoded = json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(",", ":"))
    return hashlib.sha256(encoded.encode("utf-8")).hexdigest()


def _checker():
    path = Path(__file__).resolve().parents[1] / "check-architecture.py"
    spec = importlib.util.spec_from_file_location("taf_building_inventory_parser", path)
    if spec is None or spec.loader is None:
        raise ImportError("Cannot load the architecture parser")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def runtime_paths(repo):
    result = subprocess.run(["bash", str(repo / "Tools/stage.sh"), "list"],
                            cwd=repo, check=True, text=True, capture_output=True)
    return result.stdout.splitlines()


def _definition(element):
    return {"tag": element.tag, "attributes": dict(element.attrib),
            "text": (element.text or "").strip(),
            "children": [_definition(child) for child in element]}


def _without_locations(value):
    if isinstance(value, dict):
        return {key: _without_locations(item) for key, item in value.items() if key != "location"}
    if isinstance(value, (list, tuple)):
        return [_without_locations(item) for item in value]
    return value


def inventory(repo, paths=None):
    repo = Path(repo).resolve()
    paths = runtime_paths(repo) if paths is None else paths
    checker = _checker()
    issues, sources, building_files, architecture_files = [], [], [], []
    declarations, styles, yards = {}, [], {}
    for relative in sorted(set(paths)):
        if not relative.endswith(".xml"):
            continue
        path = repo / relative
        if Path(relative).is_absolute() or ".." in Path(relative).parts or not path.resolve().is_relative_to(repo):
            raise ValueError("runtime XML path escapes repository: " + relative)
        root = checker._parse_xml(path, repo, issues)
        if root is None:
            continue
        sources.append({"path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        if root.tag == checker.BUILDING_ROOT:
            building_files.append(path)
            for element in root:
                if element.tag == "style":
                    styles.append(_definition(element))
                elif element.tag == "building":
                    declarations.setdefault(element.get("Key", ""), []).append(_definition(element))
        elif root.tag == checker.ARCHITECTURE_ROOT:
            architecture_files.append(path)
        elif root.tag == "kingdomyardworks":
            for element in root.findall("yardwork"):
                key = element.get("Key", "")
                if not key or "/" in key:
                    raise ValueError("yard work has an invalid key")
                yards[key] = _definition(element)
    if not building_files:
        raise ValueError("no staged building catalogue found")
    buildings = checker.load_buildings(building_files, repo, issues)
    model = checker.load_architectures(architecture_files, repo, issues)
    if issues:
        raise ValueError("catalogue parse refused: " + "; ".join(item.render() for item in issues[:8]))
    if not buildings:
        raise ValueError("staged building catalogue is empty")
    configurations, represented = [], set()
    for tier in sorted(model.tiers, key=lambda item: (item.build_key, item.binding.key, item.key)):
        building = buildings.get(tier.build_key)
        if building is None:
            raise ValueError("architecture refers to absent building: " + tier.build_key)
        if not tier.variants:
            raise ValueError("architecture tier has no variants: " + tier.key)
        for variant in sorted(tier.variants, key=lambda item: item.key):
            amap = model.maps.get(variant.map_key or tier.map_key)
            palette = model.palettes.get(variant.palette_key or tier.palette_key)
            if amap is None or palette is None:
                raise ValueError("architecture has unresolved map/palette: " + tier.key)
            for pose in POSES:
                identity = ["building", building.key, tier.binding.plan.key,
                            tier.binding.key, tier.key, variant.key, pose]
                case = {"id": "/".join(identity), "building": building.key,
                        "kind": "authored", "category": building.category,
                        "attributes": dict(building.attributes), "size": tier.binding.size,
                        "plan": tier.binding.plan.key, "binding": tier.binding.key,
                        "tier": tier.key, "level": tier.level, "transition": tier.transition,
                        "variant": variant.key, "selectors": dict(variant.selectors),
                        "pose": pose, "facingRule": tier.binding.facing, "type": tier.binding.type_key,
                        "retainedReader": tier.binding.retained,
                        "map": amap.key, "palette": palette.key,
                        "requiredRoles": _without_locations([asdict(value) for value in tier.requirements])}
                dependency = {"case": case, "buildingDeclarations": declarations[building.key],
                              "styles": styles, "map": asdict(amap), "palette": asdict(palette),
                              "poses": {key: asdict(value) for key, value in model.poses.items()},
                              "requirements": [asdict(value) for value in tier.requirements],
                              "selectionCandidates": [asdict(value) for value in tier.variants]}
                case["definitionDigest"] = digest(_without_locations(dependency))
                configurations.append(case)
                represented.add(building.key)
    for key, building in sorted(buildings.items()):
        if key in represented:
            continue
        case = {"id": "building/" + key + "/unbound", "building": key,
                "kind": "unbound-plot" if building.plot else "network",
                "category": building.category, "attributes": dict(building.attributes),
                "retainedReader": False}
        case["definitionDigest"] = digest({"case": case, "declarations": declarations[key], "styles": styles})
        configurations.append(case)
    for key, definition in sorted(yards.items()):
        case = {"id": "yardwork/" + key, "building": key, "kind": "yardwork",
                "category": "household-yard", "attributes": definition["attributes"],
                "retainedReader": False, "definitionDigest": digest(definition)}
        configurations.append(case)
    ids = [case["id"] for case in configurations]
    if len(ids) != len(set(ids)):
        raise ValueError("duplicate configuration identity")
    return {"schemaVersion": 1, "scope": "shipped declarations; not native acceptance",
            "sourceDigest": digest(sources), "sources": sources,
            "buildings": len(buildings), "yardworks": len(yards),
            "configurations": sorted(configurations, key=lambda case: case["id"])}
