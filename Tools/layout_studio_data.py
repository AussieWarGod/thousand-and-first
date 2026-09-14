"""Use the ordinary architecture parser and blueprint resolver for layout drafts."""

import copy
import hashlib
import importlib.util
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from layout_rooms import analyse


def load_checker(repo):
    spec = importlib.util.spec_from_file_location("taf_layout_checker", repo / "Tools/check-architecture.py")
    checker = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = checker
    spec.loader.exec_module(checker)
    return checker


class Studio:
    def __init__(self, repo, qud_base=None):
        self.repo = Path(repo).resolve()
        self.checker = load_checker(self.repo)
        issues, notices = [], []
        paths = self.checker._discover(self.repo, "KingdomArchitectures*.xml")
        self.model = self.checker.load_architectures(paths, self.repo, issues)
        self.buildings = self.checker.load_buildings(
            self.checker._discover(self.repo, "KingdomBuildings*.xml"), self.repo, issues)
        if issues:
            raise ValueError("source parse refused: " + "; ".join(item.render() for item in issues[:8]))
        if qud_base:
            _, _, self.shapes, _ = self.checker.load_blueprints(self.repo, Path(qud_base), issues, notices)
        else:
            names, records = self.checker.load_local_blueprints(self.repo, issues, notices)
            self.shapes = self.checker._resolve_blueprint_shapes(records, names)
        if issues:
            raise ValueError("blueprint parse refused: " + "; ".join(item.render() for item in issues[:8]))
        self.verified_base = bool(qud_base)
        self.sources = {}
        self.cases = []
        for path in paths:
            for item in ET.parse(path).getroot().findall("map"):
                self.sources[item.get("Key")] = (copy.deepcopy(item), path.relative_to(self.repo).as_posix())
        for tier in sorted(self.model.tiers, key=lambda t: (t.build_key, t.binding.size, t.key)):
            for variant in tier.variants:
                amap = self.model.maps[variant.map_key or tier.map_key]
                palette = self.model.palettes[variant.palette_key or tier.palette_key]
                building = self.buildings[tier.build_key]
                self.cases.append({"id": len(self.cases), "label":
                                   f"{tier.build_key} / {tier.binding.size} / {variant.key} / {amap.key}"
                                   + (" (retained reader)" if tier.binding.retained else ""),
                                   "map": amap.key, "palette": palette.key, "building": building.key,
                                   "size": tier.binding.size, "category": building.category,
                                   "retained": tier.binding.retained, "minimum_size": building.plot,
                                   "source": self.sources[amap.key][1]})

    def case(self, index):
        if type(index) is not int or not 0 <= index < len(self.cases):
            raise ValueError("select an existing layout case")
        return self.cases[index]

    def source_xml(self, index):
        element, _ = self.sources[self.case(index)["map"]]
        return ET.tostring(element, encoding="unicode").strip()

    def review(self, index, xml=None, pose="north"):
        case = self.case(index)
        original = self.source_xml(index)
        xml = original if xml is None else xml
        if not isinstance(xml, str) or len(xml.encode("utf-8")) > 65536:
            raise ValueError("draft XML exceeds 64 KiB")
        if "<!DOCTYPE" in xml.upper() or "<!ENTITY" in xml.upper():
            raise ValueError("draft cannot declare XML entities or a document type")
        try:
            element = ET.fromstring(xml)
        except ET.ParseError as error:
            raise ValueError(str(error)) from error
        if element.tag != "map":
            raise ValueError("edit one ordinary <map> element")
        issues = []
        amap = self.checker._parse_map(element, self.repo / "draft.xml", self.repo, 0, issues,
                                       validate_topology=False)
        if amap is None or issues:
            raise ValueError("; ".join(item.render() for item in issues[:8]) or "invalid map")
        self.checker._validate_map_topology(amap, issues)
        if pose not in self.checker.POSES:
            raise ValueError("unsupported pose")
        palette = self.model.palettes[case["palette"]]
        building = self.buildings[case["building"]]
        reading = analyse(amap, palette, building, self.shapes, self.checker, pose, self.model.poses)
        reading["source_sha256"] = hashlib.sha256(original.encode()).hexdigest()
        reading["issues"] = [item.render() for item in issues]
        for x, y in reading["blocked_doorways"]:
            reading["issues"].append(f'draft.furniture-doorway: furniture occupies the doorway at {x},{y}. '
                                      'Keep the doorway clear even when another entrance is usable.')
        lot_width, lot_height = self.checker.LOT_DIMENSIONS[case["size"]]
        reading["selected_lot"] = case["size"]
        reading["fits_selected_lot"] = amap.width <= lot_width and amap.height <= lot_height
        reading["minimum_lot"] = next((size for size, (w, h) in self.checker.LOT_DIMENSIONS.items()
                                       if amap.width <= w and amap.height <= h), None)
        if not reading["fits_selected_lot"]:
            reading["issues"].append(f'draft.envelope: {amap.width}x{amap.height} needs '
                                      f'{reading["minimum_lot"]} or larger; selected {case["size"]} '
                                      f'is {lot_width}x{lot_height}. Update bindings and bills before installation.')
        reading["source"] = case["source"]
        reading["base_supplied"] = self.verified_base
        reading["width"], reading["height"] = amap.width, amap.height
        reading["pose"] = pose
        reading["rows"] = list(amap.rows)
        reading["glyphs"] = [dict(item.attrib) for item in element.findall("glyph")]
        reading["xml"] = ET.tostring(element, encoding="unicode").strip()
        reading["map"] = amap.key
        return reading
