import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "coverage"))
import check_coverage  # noqa: E402


REPO_ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
MATRIX_PATH = os.path.join(REPO_ROOT, "Tools", "coverage", "matrix.json")


def _base_doc():
    return {
        "schemaVersion": 2,
        "rows": [
            {
                "id": 1,
                "behaviour": "x",
                "prerequisites": "x",
                "transitions": "x",
                "effects": "x",
                "save_cold_load": "x",
                "negative": "x",
                "driver": "x",
                "status": "NONE",
                "evidence": None,
                "note": "x",
            }
        ],
    }


def _evidence_entry(**overrides):
    entry = {
        "commit": "abc1234",
        "evidenceDir": "some-evidence-dir",
        "artifactRef": "some-evidence-dir/report.tsv",
        "verdict": "PASS - real thing happened",
        "scope": "synthetic setup, disclosed",
        "historicalScope": True,
        "currentDevCoverage": False,
        "inventoryDigest": None,
    }
    entry.update(overrides)
    return entry


class CoverageMatrixSchemaTest(unittest.TestCase):
    def test_the_real_matrix_json_validates_clean(self):
        doc = check_coverage.load(MATRIX_PATH)
        self.assertEqual(check_coverage.validate(doc), [])

    def test_the_real_matrix_json_counts_are_derived_not_hand_typed(self):
        doc = check_coverage.load(MATRIX_PATH)
        tally = check_coverage.counts(doc)
        self.assertEqual(tally["TOTAL"], len(doc["rows"]))
        self.assertEqual(
            sum(v for k, v in tally.items() if k != "TOTAL"), tally["TOTAL"]
        )

    def test_unknown_status_token_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "MOSTLY_FINE"
        problems = check_coverage.validate(doc)
        self.assertTrue(any("unknown status token" in p for p in problems))

    def test_pass_status_with_no_evidence_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = None
        problems = check_coverage.validate(doc)
        self.assertTrue(any("no typed evidence list" in p for p in problems))

    def test_pass_status_with_a_bare_string_evidence_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NEGATIVE_PASS"
        doc["rows"][0]["evidence"] = "prior-status-md-rows-not-rerun"
        problems = check_coverage.validate(doc)
        self.assertTrue(any("no typed evidence list" in p for p in problems))

    def test_pass_status_with_a_typed_evidence_list_is_accepted(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [_evidence_entry()]
        self.assertEqual(check_coverage.validate(doc), [])

    def test_evidence_entry_missing_a_required_key_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        entry = _evidence_entry()
        del entry["evidenceDir"]
        doc["rows"][0]["evidence"] = [entry]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("missing keys" in p for p in problems))

    def test_banned_placeholder_substring_is_rejected_anywhere_in_a_row(self):
        doc = _base_doc()
        doc["rows"][0]["note"] = "see prior-status doc for details"
        problems = check_coverage.validate(doc)
        self.assertTrue(any("banned placeholder substring" in p for p in problems))

    def test_owned_lane_placeholder_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["driver"] = "owned-lane, not independently assessed"
        problems = check_coverage.validate(doc)
        self.assertTrue(any("banned placeholder substring" in p for p in problems))

    def test_non_pass_status_with_non_null_evidence_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "EVIDENCE_UNVERIFIED"
        doc["rows"][0]["evidence"] = [_evidence_entry()]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("non-null evidence field" in p for p in problems))

    def test_current_dev_coverage_true_requires_matching_inventory_digest(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(
                currentDevCoverage=True, inventoryDigest="not-the-current-digest"
            )
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(
            any("does not equal the current dev digest" in p for p in problems)
        )

    def test_current_dev_coverage_true_with_the_real_current_digest_is_accepted(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(
                currentDevCoverage=True,
                inventoryDigest=check_coverage.CURRENT_DEV_DIGEST,
            )
        ]
        self.assertEqual(check_coverage.validate(doc), [])

    def test_artifact_ref_bare_description_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactRef="build journal (COMPLETE OK)")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("does not look like a path" in p for p in problems))

    def test_artifact_ref_absolute_path_that_does_not_exist_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(
                artifactRef="/definitely/not/a/real/path/on/this/machine.tsv"
            )
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("does not exist on this disk" in p for p in problems))

    def test_artifact_ref_absolute_path_that_exists_is_accepted(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [_evidence_entry(artifactRef=MATRIX_PATH)]
        self.assertEqual(check_coverage.validate(doc), [])

    def test_duplicate_row_id_is_rejected(self):
        doc = _base_doc()
        doc["rows"].append(dict(doc["rows"][0]))
        problems = check_coverage.validate(doc)
        self.assertTrue(any("duplicate row id" in p for p in problems))

    def test_missing_required_key_is_rejected(self):
        doc = _base_doc()
        del doc["rows"][0]["driver"]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("missing keys" in p for p in problems))

    def test_the_real_matrix_json_combinations_validate_clean(self):
        doc = check_coverage.load(MATRIX_PATH)
        valid_ids = {row["id"] for row in doc["rows"]}
        self.assertEqual(check_coverage.validate_combinations(doc, valid_ids), [])

    def test_combination_referencing_unknown_row_id_is_rejected(self):
        combo = {
            "id": "CX",
            "name": "x",
            "rows": [9999],
            "coupling": "x",
            "prerequisites": "x",
            "invariants": "x",
            "seed_turn_matrix": "x",
            "expected_failure_rows": "x",
            "reusable_seams": "x",
            "status": "NONE",
            "evidence": None,
        }
        problems = check_coverage.validate_combinations(
            {"combinations": [combo]}, {1, 2}
        )
        self.assertTrue(any("unknown behaviour row id" in p for p in problems))

    def test_combination_pass_status_with_no_evidence_is_rejected(self):
        combo = {
            "id": "CX",
            "name": "x",
            "rows": [1],
            "coupling": "x",
            "prerequisites": "x",
            "invariants": "x",
            "seed_turn_matrix": "x",
            "expected_failure_rows": "x",
            "reusable_seams": "x",
            "status": "NATIVE_PASS",
            "evidence": None,
        }
        problems = check_coverage.validate_combinations({"combinations": [combo]}, {1})
        self.assertTrue(any("no typed evidence list" in p for p in problems))

    def test_combination_status_never_counts_toward_behaviour_coverage(self):
        doc = check_coverage.load(MATRIX_PATH)
        tally = check_coverage.counts(doc)
        behaviour_total = sum(v for k, v in tally.items() if k != "TOTAL")
        self.assertEqual(behaviour_total, len(doc["rows"]))
        # Combinations exist but are excluded from the behaviour tally entirely.
        self.assertGreater(len(doc.get("combinations", [])), 0)
        self.assertEqual(tally["TOTAL"], len(doc["rows"]))

    def test_empty_rows_is_rejected(self):
        doc = _base_doc()
        doc["rows"] = []
        problems = check_coverage.validate(doc)
        self.assertTrue(any("non-empty list" in p for p in problems))


if __name__ == "__main__":
    unittest.main()
