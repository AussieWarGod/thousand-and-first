"""Coverage and determinism guards for the full architecture quality ledger."""

from __future__ import annotations

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "Tools" / "audit-architecture-quality.py"


def load_audit():
    spec = importlib.util.spec_from_file_location("architecture_quality_audit", SCRIPT)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class ArchitectureQualityAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.audit = load_audit()
        cls.result = cls.audit.run(ROOT, None)

    def test_census_covers_every_configuration_and_pose(self) -> None:
        summary = self.result["summary"]
        self.assertEqual(summary["buildings"], 144)
        self.assertEqual(summary["plot_buildings"], 134)
        self.assertEqual(summary["configurations"], 344)
        self.assertEqual(summary["poses"], 1376)
        self.assertEqual(summary["auxiliary_cases"], 18)
        self.assertEqual(len(self.result["buildings"]), 134)

    def test_each_configuration_has_exactly_four_poses(self) -> None:
        counts = Counter(
            (
                case["plan"],
                case["binding"],
                case["tier"],
                case["variant"],
                case["map"],
            )
            for case in self.result["cases"]
        )
        self.assertEqual(set(counts.values()), {4})
        for key, group in __import__("itertools").groupby(
            self.result["cases"],
            key=lambda case: (
                case["plan"], case["binding"], case["tier"], case["variant"]
            ),
        ):
            self.assertEqual(
                {case["pose"] for case in group},
                {"north", "east", "south", "west"},
                key,
            )

    def test_no_static_pass_can_hide_a_required_error(self) -> None:
        self.assertEqual(0, self.result["summary"]["checker_issues"])
        self.assertEqual(0, self.result["summary"]["static_fail_poses"])
        warnings = [
            (case["case"], item)
            for case in self.result["cases"]
            for item in case["findings"]
            if item["severity"] == "warning"
        ]
        self.assertEqual([], warnings, "taste/function warnings are release blockers")
        for case in self.result["cases"]:
            errors = [item for item in case["findings"] if item["severity"] == "error"]
            self.assertEqual(case["static_verdict"] == "fail", bool(errors))
            self.assertEqual(case["native_view"], "required")
            self.assertEqual(case["human_acceptance"], "pending")

    def test_reference_manifest_is_complete(self) -> None:
        receipt = self.result["reference"]
        self.assertEqual(receipt["game"], "Caves of Qud")
        self.assertEqual(receipt["version"], "2.0.211.51")
        self.assertEqual(receipt["checks"], [])
        self.assertFalse(receipt["verified"])

    def test_archives_place_blueprints_with_real_readable_generation(self) -> None:
        object_root = ET.parse(ROOT / "RuntimeData" / "ObjectBlueprints.xml").getroot()
        objects = {
            item.get("Name"): item
            for item in object_root.iter("object")
            if item.get("Name")
        }
        expected = {
            "r_KingdomReadableArchiveShelf",
            "r_KingdomReadableRegisterShelf",
        }
        for name in expected:
            blueprint = objects[name]
            markov = [
                part
                for part in blueprint.findall("part")
                if part.get("Name") == "MarkovBookshelf"
            ]
            self.assertEqual(1, len(markov), name)
            self.assertEqual("MarkovCorpus_Village", markov[0].get("BookTable"))
        archive_cases = [
            case
            for case in self.result["cases"]
            if case["build_key"] in {"bookshelf", "scriptorium"}
        ]
        self.assertEqual(8, len(archive_cases))
        for case in archive_cases:
            self.assertFalse(
                any(
                    item["code"] == "program.readable-archive"
                    for item in case["findings"]
                ),
                (case["map"], case["palette"]),
            )
            self.assertTrue(
                expected.intersection(case["metrics"]["blueprints"]),
                (case["map"], case["palette"]),
            )

    def test_eater_hall_uses_recovered_fabric_and_a_real_light(self) -> None:
        cases = [
            case
            for case in self.result["cases"]
            if case["build_key"] == "hall" and "eater" in case["variant"]
        ]
        self.assertEqual(4, len(cases))
        for case in cases:
            self.assertIn("scrap", case["metrics"]["materials"])
            self.assertIn("retained-machine-light", case["metrics"]["roles"])
            self.assertIn("Techlight1", case["metrics"]["blueprints"])
            self.assertFalse(
                any(
                    item["code"] == "culture.eater-hall"
                    for item in case["findings"]
                ),
                case["map"],
            )

    def test_eater_airwells_keep_a_live_retained_machine_readout(self) -> None:
        cases = [
            case
            for case in self.result["cases"]
            if case["build_key"] in {"airwellcourt", "airwellfield"}
            and "eater" in case["variant"]
        ]
        self.assertEqual(8, len(cases))
        for case in cases:
            self.assertIn("retained-machine-light", case["metrics"]["roles"])
            self.assertIn("Techlight1", case["metrics"]["blueprints"])
            self.assertFalse(case["findings"], case["map"])


if __name__ == "__main__":
    unittest.main()
