"""Catalogue omissions, stale links and evidence/mapping confusion must be visible."""

import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from check_architecture_test import ARCHITECTURE, BUILDINGS

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/coverage"))
import building_catalogue as catalogue
import building_coverage as coverage
import check_coverage


class BuildingInventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        # Deliberately noncanonical filenames: Qud reads these by XML root.
        (self.root / "A.xml").write_text(BUILDINGS)
        (self.root / "B.xml").write_text(ARCHITECTURE)

    def read(self):
        return catalogue.inventory(self.root, [path.name for path in self.root.glob("*.xml")])

    def test_all_sizes_and_poses_and_retained_readers_are_enumerated(self):
        path = self.root / "B.xml"
        path.write_text(ARCHITECTURE.replace('Key="housing-s"', 'Key="housing-s" Retained="yes"'))
        cases = self.read()["configurations"]
        self.assertEqual(16, len(cases))
        self.assertEqual({"S", "M", "L", "XL"}, {case["size"] for case in cases})
        self.assertEqual(set(catalogue.POSES), {case["pose"] for case in cases})
        self.assertEqual(4, sum(case["retainedReader"] for case in cases))

    def test_new_network_yard_and_unbound_plot_cannot_disappear(self):
        (self.root / "C.xml").write_text('''<kingdombuildings>
          <building Key="wall" Blueprint="Wall" Category="defense" />
          <building Key="missing-layout" Blueprint="Workshop" Category="craft" Plot="M" />
        </kingdombuildings>''')
        (self.root / "D.xml").write_text('''<kingdomyardworks Schema="1">
          <yardwork Key="vine" Blueprint="Vine" Shades="food:1" />
        </kingdomyardworks>''')
        cases = {case["id"]: case for case in self.read()["configurations"]}
        self.assertEqual("network", cases["building/wall/unbound"]["kind"])
        self.assertEqual("unbound-plot", cases["building/missing-layout/unbound"]["kind"])
        self.assertEqual("yardwork", cases["yardwork/vine"]["kind"])

    def test_keyed_partial_merge_retains_fields_and_changes_fingerprint(self):
        before = self.read()["configurations"]
        (self.root / "Z.xml").write_text('<kingdombuildings><building Key="hut" Staff="2" /></kingdombuildings>')
        after = self.read()["configurations"]
        self.assertEqual([case["id"] for case in before], [case["id"] for case in after])
        for a, b in zip(before, after):
            self.assertEqual("r_TestHut", b["attributes"]["Blueprint"])
            self.assertEqual("2", b["attributes"]["Staff"])
            self.assertNotEqual(a["definitionDigest"], b["definitionDigest"])

    def test_unrelated_building_does_not_invalidate_existing_definitions(self):
        before = self.read()["configurations"]
        (self.root / "Z.xml").write_text('<kingdombuildings><building Key="other" Blueprint="Wall" /></kingdombuildings>')
        after = {case["id"]: case for case in self.read()["configurations"]}
        for case in before:
            self.assertEqual(case["definitionDigest"], after[case["id"]]["definitionDigest"])

    def test_geometry_palette_and_selection_changes_invalidate_mapping(self):
        def selected():
            return next(case["definitionDigest"] for case in self.read()["configurations"]
                        if case["size"] == "S" and case["pose"] == "north")
        original = selected()
        for old, new in [('Cells="#@,,,#"', 'Cells="#,@,,#"'),
                         ('Blueprint="TestBed"', 'Blueprint="OtherBed"'),
                         ('Priority="0"', 'Priority="1"')]:
            with self.subTest(change=new):
                (self.root / "B.xml").write_text(ARCHITECTURE.replace(old, new))
                self.assertNotEqual(original, selected())

    def test_missing_references_and_malformed_xml_refuse_instead_of_omitting_cases(self):
        for xml in [ARCHITECTURE.replace('BuildKey="hut"', 'BuildKey="absent"'),
                    ARCHITECTURE.replace('Map="test-map"', 'Map="missing"'), '<broken']:
            with self.subTest(xml=xml[:50]):
                (self.root / "B.xml").write_text(xml)
                with self.assertRaises(ValueError):
                    self.read()

    def test_only_staged_paths_are_used(self):
        (self.root / "ignored.xml").write_text('<broken')
        self.assertEqual(16, len(catalogue.inventory(self.root, ["A.xml", "B.xml"])["configurations"]))

    def test_empty_catalogue_cannot_appear_complete(self):
        (self.root / "A.xml").write_text("<kingdombuildings />")
        with self.assertRaisesRegex(ValueError, "empty"):
            catalogue.inventory(self.root, ["A.xml"])

    def test_reordered_input_paths_produce_identical_inventory(self):
        self.assertEqual(catalogue.inventory(self.root, ["A.xml", "B.xml"]),
                         catalogue.inventory(self.root, ["B.xml", "A.xml"]))


class BuildingCoverageTests(unittest.TestCase):
    def setUp(self):
        self.case = {"id": "building/hut/unbound", "definitionDigest": "a" * 64,
                     "building": "hut", "kind": "network"}
        self.catalogue = {"configurations": [self.case], "buildings": 1, "yardworks": 0,
                          "sourceDigest": "b" * 64, "sources": [], "scope": "test inventory"}
        self.matrix = {"rows": [{"id": 6, "status": "NATIVE_PASS"}]}
        self.mapping = {"schemaVersion": 1, "bindings": []}
        self.binding = {"configuration": self.case["id"], "definitionDigest": "a" * 64,
                        "obligation": "operation", "rows": [6],
                        "reason": "Exact native scenario scope must be inspected."}

    def test_broad_native_pass_never_automatically_covers_any_building(self):
        result = coverage.report(self.catalogue, self.mapping, self.matrix)
        self.assertEqual({"UNMAPPED": 7}, result["summary"]["mappingStatuses"])
        self.assertEqual("not established by this inventory", result["nativeAcceptance"])

    def test_exact_link_retains_underlying_status_without_granting_acceptance(self):
        self.mapping["bindings"] = [self.binding]
        for status in ["NATIVE_PASS", "NONE", "COVERAGE_GAP", "DEFECT"]:
            self.matrix["rows"][0]["status"] = status
            result = coverage.report(self.catalogue, self.mapping, self.matrix)
            operation = result["configurations"][0]["obligations"][2]
            self.assertEqual("MAPPED", operation["mappingStatus"])
            self.assertEqual([{"id": 6, "status": status}], operation["rows"])
            self.assertEqual("not established by this inventory", result["nativeAcceptance"])

    def test_stale_mapping_remains_visible(self):
        self.binding["definitionDigest"] = "c" * 64
        self.mapping["bindings"] = [self.binding]
        self.assertEqual({"STALE_MAPPING": 1, "UNMAPPED": 6},
                         coverage.report(self.catalogue, self.mapping, self.matrix)["summary"]["mappingStatuses"])

    def test_invalid_or_duplicate_scope_is_refused(self):
        for field, value in [("configuration", "missing"), ("obligation", "looks-good"),
                             ("obligation", []), ("rows", [999]), ("rows", [6, 6]),
                             ("rows", []), ("rows", [True]), ("reason", ""),
                             ("definitionDigest", "bad")]:
            with self.subTest(field=field, value=value):
                binding = {**self.binding, field: value}
                with self.assertRaises(ValueError):
                    coverage.report(self.catalogue, {"schemaVersion": 1, "bindings": [binding]}, self.matrix)
        self.mapping["bindings"] = [self.binding, copy.deepcopy(self.binding)]
        with self.assertRaisesRegex(ValueError, "duplicate"):
            coverage.report(self.catalogue, self.mapping, self.matrix)

    def test_cli_writes_missing_coverage_before_failing_required_mapping(self):
        with tempfile.TemporaryDirectory() as root:
            output = Path(root) / "report.json"
            with patch("building_catalogue.inventory", return_value=self.catalogue):
                code = check_coverage.main(["buildings", "--out", str(output), "--require-mapped"])
            self.assertEqual(2, code)
            result = json.loads(output.read_text())
            self.assertEqual({"UNMAPPED": 7}, result["summary"]["mappingStatuses"])

    def test_live_staged_catalogue_and_checked_in_mapping(self):
        inventory = catalogue.inventory(ROOT)
        mapping = json.loads((ROOT / "Tools/coverage/buildings.json").read_text())
        matrix = check_coverage.load(ROOT / "Tools/coverage/matrix.json")
        self.assertEqual([], check_coverage.validate(matrix))
        result = coverage.report(inventory, mapping, matrix)
        self.assertEqual(0, result["summary"]["unboundPlots"])
        self.assertEqual(144, result["summary"]["buildings"])
        self.assertEqual(1390, result["summary"]["configurations"])
        self.assertEqual(4, result["summary"]["yardworks"])
        self.assertEqual(9730, result["summary"]["obligations"])
        self.assertEqual("not established by this inventory", result["nativeAcceptance"])


if __name__ == "__main__":
    unittest.main()
