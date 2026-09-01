#!/usr/bin/env python3
"""Reference-grounded visual/function census for every shipped architecture pose.

This is deliberately a second gate beside check-architecture.py.  The schema checker proves
that a snapshot is legal; this audit records whether each posed configuration has a legible
program, usable circulation, coherent fabric, and the native/human evidence still owed.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from typing import Any, Iterable


POSES = ("north", "east", "south", "west")
SIZE_ORDER = {"S": 0, "M": 1, "L": 2, "XL": 3}
PROGRAM_PREFIXES = {
    "housing": ("function:", "bed:", "sleep:", "fixture:sleep"),
    "storage": ("function:", "storage:", "water:", "liquid:"),
    "food": ("function:", "food:", "crop:", "storage:"),
    "craft": ("function:", "work:", "machine:", "purpose:", "process:"),
    "civic": ("function:", "market:", "rite:", "seat:", "dais:"),
    "faith": ("function:", "ritual:", "offering:", "shrine:"),
    "knowledge": ("function:", "archive:", "register:", "learning:"),
    "power": ("function:", "power:", "machine:"),
    "memorial": ("function:", "memorial:"),
    "defense": ("function:", "defense:", "gate:"),
}


def _load_module(path: Path, name: str) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _reference_receipt(repo: Path, qud_base: Path | None) -> dict[str, Any]:
    manifest_path = repo / "Tools" / "architecture-quality-reference.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    checks: list[dict[str, Any]] = []
    if qud_base is not None:
        base = qud_base.resolve()
        if base.name == "ObjectBlueprints.xml":
            base = base.parent
        elif base.name == "ObjectBlueprints":
            base = base.parent
        for source in manifest["authoritative_sources"] + manifest["named_map_references"]:
            path = base / source["path"]
            actual = _sha256(path) if path.is_file() else "missing"
            checks.append(
                {
                    "path": source["path"],
                    "expected_sha256": source["sha256"],
                    "actual_sha256": actual,
                    "matches": actual == source["sha256"],
                }
            )
        assembly = base.parents[1] / "Managed" / "Assembly-CSharp.dll"
        actual = _sha256(assembly) if assembly.is_file() else "missing"
        expected = manifest["benchmark"]["assembly_sha256"]
        checks.append(
            {
                "path": "../../Managed/Assembly-CSharp.dll",
                "expected_sha256": expected,
                "actual_sha256": actual,
                "matches": actual == expected,
            }
        )
    return {
        "manifest": "Tools/architecture-quality-reference.json",
        "manifest_sha256": _sha256(manifest_path),
        "game": manifest["benchmark"]["game"],
        "version": manifest["benchmark"]["version"],
        "checks": checks,
        "verified": bool(checks) and all(item["matches"] for item in checks),
    }


def _cells(amap: Any) -> Iterable[tuple[int, int, Any]]:
    for y, row in enumerate(amap.rows):
        for x, _char in enumerate(row):
            glyph = amap.glyph_at(x, y)
            if glyph is not None:
                yield x, y, glyph


def _reachable(
    amap: Any, checker: Any
) -> tuple[set[tuple[int, int]], bool, list[str]]:
    reached, egress = checker._public_circulation(amap)
    inaccessible: list[str] = []
    for x, y, glyph in _cells(amap):
        if not glyph.anchors:
            continue
        accessible = (x, y) in reached
        if glyph.pass_mode == "adjacent":
            accessible = any(
                other in reached
                for other in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
            )
        if not accessible:
            inaccessible.extend(glyph.anchors)
    return reached, egress, sorted(set(inaccessible))


def _resolved_blueprint(reference: str, palette: Any, building: Any) -> str:
    if reference == "$building":
        return building.blueprint
    if reference.startswith("$"):
        slot = palette.slots.get(reference[1:])
        return "" if slot is None else slot.blueprint
    return reference


def _metrics(amap: Any, palette: Any, building: Any, checker: Any) -> dict[str, Any]:
    anchors: Counter[str] = Counter()
    roles: Counter[str] = Counter()
    materials: Counter[str] = Counter()
    blueprints: Counter[str] = Counter()
    occupied = blocked = covered = objects = structures = 0
    for _x, _y, glyph in _cells(amap):
        occupied += 1
        blocked += glyph.pass_mode == "blocked"
        covered += glyph.cover != "open"
        anchors.update(glyph.anchors)
        for layer, reference in glyph.layers():
            structures += layer == "Structure"
            objects += layer == "Object"
            blueprint = _resolved_blueprint(reference, palette, building)
            if blueprint:
                blueprints[blueprint] += 1
            if reference.startswith("$") and reference != "$building":
                slot = palette.slots.get(reference[1:])
                if slot is not None:
                    roles[slot.role] += 1
                    materials[slot.material] += 1
    reached, egress, inaccessible = _reachable(amap, checker)
    return {
        "area": amap.width * amap.height,
        "occupied_cells": occupied,
        "empty_cells": amap.width * amap.height - occupied,
        "blocked_cells": blocked,
        "covered_cells": covered,
        "structure_placements": structures,
        "object_placements": objects,
        "reachable_walk_cells": len(reached),
        "boundary_egress": egress,
        "inaccessible_anchors": inaccessible,
        "anchors": dict(sorted(anchors.items())),
        "roles": dict(sorted(roles.items())),
        "materials": dict(sorted(materials.items())),
        "blueprints": dict(sorted(blueprints.items())),
    }


def _has_prefix(values: Iterable[str], prefixes: Iterable[str]) -> bool:
    return any(value.startswith(prefix) for value in values for prefix in prefixes)


def _finding(code: str, severity: str, message: str) -> dict[str, str]:
    return {"code": code, "severity": severity, "message": message}


def _quality_findings(
    tier: Any,
    variant: Any,
    building: Any,
    metrics: dict[str, Any],
    palette: Any,
    readable_archive_blueprints: set[str],
) -> list[dict[str, str]]:
    anchors = metrics["anchors"]
    roles = metrics["roles"]
    findings: list[dict[str, str]] = []
    if not any(key.startswith("entrance:public") for key in anchors):
        findings.append(_finding("access.public-entrance", "error", "no public threshold"))
    if not metrics["boundary_egress"]:
        findings.append(
            _finding("access.boundary-egress", "error", "public circulation does not reach lot edge")
        )
    required = {requirement.role for requirement in tier.requirements}
    inaccessible = sorted(required.intersection(metrics["inaccessible_anchors"]))
    if inaccessible:
        findings.append(
            _finding(
                "access.required-anchor",
                "error",
                "required uses lack public circulation: " + ", ".join(inaccessible),
            )
        )
    prefixes = PROGRAM_PREFIXES.get(building.category, ("function:",))
    if not _has_prefix(anchors, prefixes):
        findings.append(
            _finding("program.category", "error", f"no legible {building.category} program anchor")
        )
    if metrics["occupied_cells"] == 0 or metrics["object_placements"] == 0:
        findings.append(_finding("silhouette.empty", "error", "no readable built object"))

    # Exact dossier rules: each is mechanically resolvable, so the finding disappears when the
    # corresponding architecture is genuinely furnished rather than when prose is edited.
    if tier.build_key == "larder" and not _has_prefix(anchors, ("storage:",)):
        findings.append(_finding("program.larder-shelving", "error", "larder has no physical storage fixture"))
    if tier.build_key == "smelter" and not _has_prefix(anchors, ("storage:output",)):
        findings.append(_finding("program.smelter-output", "error", "smelter has no output store"))
    if tier.build_key == "butcherslab" and not _has_prefix(anchors, ("storage:meat", "storage:output")):
        findings.append(_finding("program.butcher-store", "error", "butcher has no meat/output store"))
    if tier.build_key in {"bookshelf", "scriptorium"} and not (
        set(metrics["blueprints"]) & readable_archive_blueprints
    ):
        findings.append(
            _finding(
                "program.readable-archive",
                "error",
                "archive has no fixture that physically creates readable book/register content",
            )
        )
    if tier.build_key == "terrace":
        sleeps = sum(count for key, count in anchors.items() if key.startswith("fixture:sleep"))
        household_thresholds = sum(
            count
            for key, count in anchors.items()
            if key.startswith("threshold:household")
        )
        if household_thresholds and sleeps < household_thresholds:
            findings.append(
                _finding(
                    "program.terrace-households",
                    "error",
                    f"{household_thresholds} dwelling thresholds but only {sleeps} sleep fixtures",
                )
            )
    if tier.build_key == "reliquary":
        case = [slot.blueprint for slot in palette.slots.values() if slot.role == "recovered-relic-case"]
        relic = [slot.blueprint for slot in palette.slots.values() if slot.role == "retained-machine-relic"]
        if case and relic and case == relic:
            findings.append(
                _finding("readability.reliquary", "error", "relic case and machine relic use the same blueprint")
            )
    if tier.build_key == "hall" and "eater" in variant.key.lower():
        has_scrap = any(
            material in {"scrap", "workedmetal"}
            for material in metrics["materials"]
        )
        has_light = any("light" in role for role in metrics["roles"])
        if not has_scrap or not has_light:
            findings.append(
                _finding(
                    "culture.eater-hall",
                    "error",
                    "Eater hall lacks recovered metal fabric and a readable retained light",
                )
            )
    if tier.build_key in {"airwellcourt", "airwellfield"} and "eater" in variant.key.lower():
        if not any(key.startswith("light:") for key in anchors):
            findings.append(
                _finding("culture.eater-airwell", "warning", "Eater reuse has no retained-machine light/readout")
            )
    return findings


def _checker_findings(result: Any, tier: Any, variant: Any, amap: Any, palette: Any, building: Any) -> list[dict[str, str]]:
    locations = {
        tier.binding.plan.location,
        tier.binding.location,
        tier.location,
        variant.location,
        amap.location,
        palette.location,
        building.location,
    }
    findings = []
    for issue in result.issues:
        if issue.location in locations or amap.key in issue.message or palette.key in issue.message:
            findings.append(_finding("schema." + issue.code, "error", issue.message))
    return findings


def _auxiliary_cases(repo: Path, buildings: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    for building in sorted(buildings.values(), key=lambda item: item.key):
        if building.plot:
            continue
        result.append(
            {
                "kind": "network-work",
                "key": building.key,
                "category": building.category,
                "blueprint": building.blueprint,
                "static_verdict": "pass",
                "native_view": "required",
                "human_acceptance": "pending",
            }
        )
    for key in ("road-worn", "road-trodden", "road-path", "road-paved"):
        result.append(
            {
                "kind": "road-surface",
                "key": key,
                "static_verdict": "pass",
                "native_view": "required",
                "human_acceptance": "pending",
            }
        )
    yard_path = repo / "RuntimeData" / "KingdomYardWorks.xml"
    if yard_path.is_file():
        for item in ET.parse(yard_path).getroot().findall("yardwork"):
            result.append(
                {
                    "kind": "hosted-yard-work",
                    "key": item.get("Key", ""),
                    "blueprint": item.get("Blueprint", ""),
                    "static_verdict": "pass",
                    "native_view": "required",
                    "human_acceptance": "pending",
                }
            )
    return result


def run(repo: Path, qud_base: Path | None) -> dict[str, Any]:
    checker = _load_module(repo / "Tools" / "check-architecture.py", "taf_architecture_checker")
    checked = checker.run_check(repo, qud_base)
    object_root = ET.parse(repo / "RuntimeData" / "ObjectBlueprints.xml").getroot()
    readable_archive_blueprints = {
        item.get("Name", "")
        for item in object_root.iter("object")
        if item.get("Name")
        and any(
            part.get("Name") == "MarkovBookshelf"
            for part in item.findall("part")
        )
    }
    cases: list[dict[str, Any]] = []
    build_summary: dict[str, dict[str, Any]] = {}
    number = 0
    tiers = sorted(
        checked.model.tiers,
        key=lambda item: (
            item.build_key,
            item.binding.type_key,
            SIZE_ORDER.get(item.binding.size, 99),
            item.key,
        ),
    )
    for tier in tiers:
        building = checked.buildings.get(tier.build_key)
        if building is None:
            continue
        for variant in sorted(tier.variants, key=lambda item: item.key):
            amap = checked.model.maps.get(variant.map_key or tier.map_key)
            palette = checked.model.palettes.get(variant.palette_key or tier.palette_key)
            if amap is None or palette is None:
                continue
            metrics = _metrics(amap, palette, building, checker)
            findings = _checker_findings(checked, tier, variant, amap, palette, building)
            findings.extend(
                _quality_findings(
                    tier,
                    variant,
                    building,
                    metrics,
                    palette,
                    readable_archive_blueprints,
                )
            )
            findings = sorted(
                {json.dumps(item, sort_keys=True): item for item in findings}.values(),
                key=lambda item: (item["severity"], item["code"], item["message"]),
            )
            static = "fail" if any(item["severity"] == "error" for item in findings) else "pass"
            for pose in POSES:
                number += 1
                posed = checker._posed_rows(amap, pose)
                cases.append(
                    {
                        "case": number,
                        "build_key": tier.build_key,
                        "category": building.category,
                        "plot_size": tier.binding.size,
                        "plan": tier.binding.plan.key,
                        "binding": tier.binding.key,
                        "tier": tier.key,
                        "level": tier.level,
                        "transition": tier.transition,
                        "variant": variant.key,
                        "pose": pose,
                        "map": amap.key,
                        "palette": palette.key,
                        "canonical_dimensions": [amap.width, amap.height],
                        "posed_dimensions": [len(posed[0]), len(posed)],
                        "metrics": metrics,
                        "findings": findings,
                        "static_verdict": static,
                        "native_view": "required",
                        "human_acceptance": "pending",
                        "overall": "fail" if static == "fail" else "needs-native-view",
                    }
                )
            summary = build_summary.setdefault(
                tier.build_key,
                {
                    "category": building.category,
                    "configurations": 0,
                    "poses": 0,
                    "failed_poses": 0,
                    "finding_codes": set(),
                },
            )
            summary["configurations"] += 1
            summary["poses"] += len(POSES)
            summary["failed_poses"] += len(POSES) if static == "fail" else 0
            summary["finding_codes"].update(item["code"] for item in findings)
    for value in build_summary.values():
        value["finding_codes"] = sorted(value["finding_codes"])
    auxiliaries = _auxiliary_cases(repo, checked.buildings)
    failed = sum(case["static_verdict"] == "fail" for case in cases)
    source_digest = hashlib.sha256()
    for path in sorted(checked.architecture_files + checked.building_files):
        source_digest.update(path.relative_to(repo).as_posix().encode("utf-8"))
        source_digest.update(bytes.fromhex(_sha256(path)))
    return {
        "schema": 1,
        "reference": _reference_receipt(repo, qud_base),
        "source_digest": source_digest.hexdigest(),
        "rubric": {
            "static_pass": "schema-valid, public threshold reaches boundary and every required use, category program is legible, and exact dossier defects are absent",
            "needs_native_view": "static pass but Qud tile adjacency, lighting, interaction, and composition remain unsigned",
            "human_acceptance": "a human has reviewed the actual in-game pose at play scale",
        },
        "summary": {
            "buildings": len(checked.buildings),
            "plot_buildings": sum(bool(item.plot) for item in checked.buildings.values()),
            "maps": len(checked.model.maps),
            "tiers": len(checked.model.tiers),
            "configurations": len(cases) // len(POSES),
            "poses": len(cases),
            "static_pass_poses": len(cases) - failed,
            "static_fail_poses": failed,
            "native_view_required_poses": len(cases),
            "human_acceptance_pending_poses": len(cases),
            "checker_issues": len(checked.issues),
            "checker_warnings": len(checked.notices),
            "auxiliary_cases": len(auxiliaries),
        },
        "buildings": dict(sorted(build_summary.items())),
        "cases": cases,
        "auxiliary_cases": auxiliaries,
        "global_checker_issues": [issue.render() for issue in checked.issues],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--qud-base", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    repo = args.repo_root.resolve()
    result = run(repo, args.qud_base)
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output is not None:
        args.output.write_text(encoded, encoding="utf-8", newline="\n")
    print(json.dumps(result["summary"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
