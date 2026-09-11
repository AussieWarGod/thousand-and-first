"""Regression for Tools/coverage/check_coverage.py: the schema validator must
reject a bad status token, a PASS-shaped status with no evidence id, and a
duplicate row id -- never silently accept them -- and the real matrix.json
checked into this repo must itself validate clean."""

import copy
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "coverage"))
import check_coverage as cc  # noqa: E402

MATRIX_PATH = os.path.join(
    os.path.dirname(__file__), "..", "coverage", "matrix.json"
)


def _good_doc():
    return {
        "schemaVersion": 1,
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
                "evidence_id": None,
                "note": "x",
            }
        ],
    }


class CoverageMatrixSchemaTest(unittest.TestCase):
    def test_the_real_matrix_json_validates_clean(self):
        doc = cc.load(MATRIX_PATH)
        self.assertEqual([], cc.validate(doc))

    def test_the_real_matrix_json_counts_are_derived_not_hand_typed(self):
        doc = cc.load(MATRIX_PATH)
        tally = cc.counts(doc)
        self.assertEqual(len(doc["rows"]), tally["TOTAL"])
        self.assertEqual(
            tally["TOTAL"],
            sum(v for k, v in tally.items() if k != "TOTAL"),
            "the per-status counts must sum to the row total",
        )

    def test_unknown_status_token_is_rejected(self):
        doc = _good_doc()
        doc["rows"][0]["status"] = "MAYBE"
        problems = cc.validate(doc)
        self.assertTrue(
            any("unknown status token" in p for p in problems), problems
        )

    def test_pass_status_with_no_evidence_id_is_rejected(self):
        doc = _good_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence_id"] = None
        problems = cc.validate(doc)
        self.assertTrue(
            any("no evidence_id" in p for p in problems), problems
        )

    def test_pass_status_with_an_evidence_id_is_accepted(self):
        doc = _good_doc()
        doc["rows"][0]["status"] = "NEGATIVE_PASS"
        doc["rows"][0]["evidence_id"] = "run-1234"
        self.assertEqual([], cc.validate(doc))

    def test_duplicate_row_id_is_rejected(self):
        doc = _good_doc()
        doc["rows"].append(copy.deepcopy(doc["rows"][0]))
        problems = cc.validate(doc)
        self.assertTrue(any("duplicate row id" in p for p in problems), problems)

    def test_missing_required_key_is_rejected(self):
        doc = _good_doc()
        del doc["rows"][0]["driver"]
        problems = cc.validate(doc)
        self.assertTrue(any("missing keys" in p for p in problems), problems)

    def test_empty_rows_is_rejected(self):
        doc = {"schemaVersion": 1, "rows": []}
        problems = cc.validate(doc)
        self.assertTrue(any("non-empty" in p for p in problems), problems)


if __name__ == "__main__":
    unittest.main()
