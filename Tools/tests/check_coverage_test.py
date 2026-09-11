import hashlib
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "coverage"))
import check_coverage  # noqa: E402


REPO_ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
MATRIX_PATH = os.path.join(REPO_ROOT, "Tools", "coverage", "matrix.json")
_DIGEST_A = "a" * 64
_DIGEST_B = "b" * 64
_SHA_X = "1" * 64


def _base_doc():
    return {
        "schemaVersion": 3,
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
        "artifactPath": "some-evidence-dir/report.tsv",
        "artifactSha256": _SHA_X,
        "artifactBytes": 42,
        "verdict": "PASS - real thing happened",
        "scope": "synthetic setup, disclosed",
        "historicalScope": True,
        "currentDevCoverage": False,
        "inventoryDigest": None,
    }
    entry.update(overrides)
    return entry


class CoverageMatrixSchemaTest(unittest.TestCase):
    """Pure schema checks: no filesystem access, no dependency on this machine's layout --
    exactly what test_checked_in_matrix runs offline in a clean checkout / public CI."""

    def test_the_real_matrix_json_validates_clean_offline(self):
        doc = check_coverage.load(MATRIX_PATH)
        self.assertEqual(check_coverage.validate(doc), [])

    def test_the_real_matrix_json_counts_are_derived_not_hand_typed(self):
        doc = check_coverage.load(MATRIX_PATH)
        tally = check_coverage.counts(doc)
        self.assertEqual(tally["TOTAL"], len(doc["rows"]))
        self.assertEqual(
            sum(v for k, v in tally.items() if k != "TOTAL"), tally["TOTAL"]
        )

    def test_checked_in_matrix_against_the_live_inventory_digest(self):
        """Computes the digest for THIS checked-in tree at test time -- never a hardcoded
        constant that could silently outlive dev changes."""
        doc = check_coverage.load(MATRIX_PATH)
        digest = check_coverage.compute_inventory_digest(os.path.abspath(REPO_ROOT))
        self.assertRegex(digest, r"^[0-9a-f]{64}$")
        self.assertEqual(check_coverage.validate(doc, current_digest=digest), [])

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
        del entry["artifactSha256"]
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
            _evidence_entry(currentDevCoverage=True, inventoryDigest=_DIGEST_A)
        ]
        problems = check_coverage.validate(doc, current_digest=_DIGEST_B)
        self.assertTrue(
            any(
                "does not equal the digest of the tree being validated" in p
                for p in problems
            )
        )

    def test_current_dev_coverage_true_with_the_matching_digest_is_accepted(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(currentDevCoverage=True, inventoryDigest=_DIGEST_A)
        ]
        self.assertEqual(check_coverage.validate(doc, current_digest=_DIGEST_A), [])

    def test_current_dev_coverage_true_with_no_current_digest_supplied_only_checks_shape(
        self,
    ):
        # Schema-only mode (no --repo-root/--inventory-digest given): cannot assert equality
        # against nothing, but a malformed digest is still rejected.
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(currentDevCoverage=True, inventoryDigest=_DIGEST_A)
        ]
        self.assertEqual(check_coverage.validate(doc), [])

    def test_current_dev_coverage_true_with_no_digest_at_all_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(currentDevCoverage=True, inventoryDigest=None)
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("no inventoryDigest to prove" in p for p in problems))

    def test_artifact_path_bare_description_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath="build journal (COMPLETE OK)")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("does not look like a run-relative" in p for p in problems))

    def test_artifact_path_absolute_unix_path_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath="/home/someone/evidence/report.tsv")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("must be run-relative" in p for p in problems))

    def test_artifact_path_absolute_windows_path_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath=r"C:\evidence\report.tsv")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("must be run-relative" in p for p in problems))

    def test_artifact_path_run_relative_is_accepted(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath="hotfix142-native.IqS6Jj/results.json")
        ]
        self.assertEqual(check_coverage.validate(doc), [])

    def test_artifact_path_dotdot_segment_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath="synthetic-run/../../etc/passwd")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("traversal" in p for p in problems))

    def test_artifact_path_dot_segment_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [
            _evidence_entry(artifactPath="synthetic-run/./report.tsv")
        ]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("traversal" in p for p in problems))

    def test_malformed_sha256_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [_evidence_entry(artifactSha256="not-a-hash")]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("64-char lowercase hex digest" in p for p in problems))

    def test_non_positive_artifact_bytes_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        doc["rows"][0]["evidence"] = [_evidence_entry(artifactBytes=0)]
        problems = check_coverage.validate(doc)
        self.assertTrue(any("positive integer" in p for p in problems))

    def test_absolute_path_anywhere_in_a_row_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["note"] = (
            "see /home/r/work/taf-scratch/some-evidence-dir for details"
        )
        problems = check_coverage.validate(doc)
        self.assertTrue(any("absolute host path" in p for p in problems))

    def test_windows_absolute_path_anywhere_in_a_row_is_rejected(self):
        doc = _base_doc()
        doc["rows"][0]["note"] = r"see C:\Users\someone\evidence for details"
        problems = check_coverage.validate(doc)
        self.assertTrue(any("absolute host path" in p for p in problems))

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
        self.assertGreater(len(doc.get("combinations", [])), 0)
        self.assertEqual(tally["TOTAL"], len(doc["rows"]))

    def test_empty_rows_is_rejected(self):
        doc = _base_doc()
        doc["rows"] = []
        problems = check_coverage.validate(doc)
        self.assertTrue(any("non-empty list" in p for p in problems))

    def test_generated_markdown_leaks_no_private_absolute_paths(self):
        doc = check_coverage.load(MATRIX_PATH)
        markdown = check_coverage.render_markdown(doc)
        self.assertNotIn("/home/", markdown)
        self.assertNotIn("/mnt/", markdown)
        self.assertNotIn("C:\\", markdown)


class EvidenceAuditTest(unittest.TestCase):
    """The audit entry point is SEPARATE from schema validation and is never required by
    test_checked_in_matrix -- it resolves artifactPath under a caller-supplied root and
    verifies the retained bytes' own SHA-256, against a synthetic evidence root here (never
    against a real machine's private evidence archive)."""

    def _doc_with_one_entry(self, content: bytes, **overrides):
        sha = hashlib.sha256(content).hexdigest()
        doc = _base_doc()
        doc["rows"][0]["status"] = "NATIVE_PASS"
        entry = _evidence_entry(
            artifactPath="synthetic-run/report.tsv",
            artifactSha256=sha,
            artifactBytes=len(content),
        )
        entry.update(overrides)
        doc["rows"][0]["evidence"] = [entry]
        return doc

    def test_audit_verifies_matching_bytes(self):
        content = b"synthetic native run journal\n"
        doc = self._doc_with_one_entry(content)
        with tempfile.TemporaryDirectory() as root:
            run_dir = os.path.join(root, "synthetic-run")
            os.makedirs(run_dir)
            with open(os.path.join(run_dir, "report.tsv"), "wb") as handle:
                handle.write(content)
            results = check_coverage.audit(doc, root)
        self.assertEqual(len(results), 1)
        self.assertEqual(results[0]["result"], "VERIFIED")

    def test_audit_detects_hash_drift(self):
        content = b"synthetic native run journal\n"
        doc = self._doc_with_one_entry(content)
        with tempfile.TemporaryDirectory() as root:
            run_dir = os.path.join(root, "synthetic-run")
            os.makedirs(run_dir)
            with open(os.path.join(run_dir, "report.tsv"), "wb") as handle:
                handle.write(b"drifted bytes, not the retained content\n")
            results = check_coverage.audit(doc, root)
        self.assertEqual(results[0]["result"], "HASH_DRIFT")

    def test_audit_detects_missing_file(self):
        content = b"synthetic native run journal\n"
        doc = self._doc_with_one_entry(content)
        with tempfile.TemporaryDirectory() as root:
            results = check_coverage.audit(doc, root)
        self.assertEqual(results[0]["result"], "MISSING")

    def test_audit_refuses_a_symlink_escaping_the_evidence_root(self):
        content = b"synthetic native run journal\n"
        doc = self._doc_with_one_entry(content)
        with (
            tempfile.TemporaryDirectory() as outside,
            tempfile.TemporaryDirectory() as root,
        ):
            secret = os.path.join(outside, "secret.tsv")
            with open(secret, "wb") as handle:
                handle.write(b"not evidence, a private file outside the root\n")
            run_dir = os.path.join(root, "synthetic-run")
            os.makedirs(run_dir)
            os.symlink(secret, os.path.join(run_dir, "report.tsv"))
            results = check_coverage.audit(doc, root)
        self.assertEqual(results[0]["result"], "ESCAPES_ROOT")

    def test_audit_aborts_before_any_byte_check_when_schema_is_invalid(self):
        content = b"synthetic native run journal\n"
        doc = self._doc_with_one_entry(content)
        doc["rows"][0]["driver"] = "owned-lane, banned placeholder makes this invalid"
        with tempfile.TemporaryDirectory() as root:
            run_dir = os.path.join(root, "synthetic-run")
            os.makedirs(run_dir)
            with open(os.path.join(run_dir, "report.tsv"), "wb") as handle:
                handle.write(content)
            with self.assertRaises(check_coverage.SchemaFailedForAudit):
                check_coverage.audit(doc, root)


if __name__ == "__main__":
    unittest.main()
