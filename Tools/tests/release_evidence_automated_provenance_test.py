#!/usr/bin/env python3
"""Tests for the automated-only release evidence provenance policy (author ruling
2026-09-11): reviewedBy/testedBy may be an honestly labelled automated identity bound to a
real artefact instead of a human name, forged human-signature claims are always refused, and
native startup/save/reload proof is an automated-driver results artefact instead of a human
tester's word. Preview-media provenance (who captured/reviewed the public listing screenshot)
stays deliberately human.
"""

from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

CHECKER_PATH = Path(__file__).resolve().parents[1] / "check-structure.py"
SPEC = importlib.util.spec_from_file_location(
    "taf_check_structure_provenance", CHECKER_PATH
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {CHECKER_PATH}")
CHECKER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = CHECKER
SPEC.loader.exec_module(CHECKER)

from Tools import workshop_metadata as METADATA


class StructureReviewProvenanceTests(unittest.TestCase):
    """Tools/check-structure.py's exact-inventory semantic review ledger."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def source(self, relative: str, lines: int) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        rows = ["namespace Fixture;"] + [f"// {i}" for i in range(1, lines)]
        path.write_text("\n".join(rows) + "\n", encoding="utf-8")

    def review(self, **overrides) -> Path:
        self.source("Core/One.cs", 20)
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        payload = {
            "schemaVersion": 1,
            "inventorySha256": census.inventory_sha256,
            "exceptions": [],
            "reviewedBy": "codex-root structural review",
            "completedUtc": "2026-09-11T00:00:00Z",
            "oneResponsibility": {
                "status": "passed",
                "notes": "Reviewed ownership boundaries. run:review-77 log:docs/release-evidence/r.log",
            },
            "protocolsAtBoundaries": {
                "status": "passed",
                "notes": "Reviewed protocol boundaries. run:review-77 log:docs/release-evidence/r.log",
            },
        }
        payload.update(overrides)
        path = self.root / "review.json"
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path, census

    def test_automated_identity_with_bound_artifact_passes(self) -> None:
        path, census = self.review()
        self.assertEqual(CHECKER.review_issues(path, census), [])

    def test_bare_automated_name_without_bound_artifact_fails(self) -> None:
        path, census = self.review(
            oneResponsibility={"status": "passed", "notes": "Reviewed ownership boundaries fully."}
        )
        issues = CHECKER.review_issues(path, census)
        self.assertTrue(any("bound to a real" in issue for issue in issues), issues)

    def test_forged_human_signature_in_notes_fails(self) -> None:
        path, census = self.review(
            oneResponsibility={
                "status": "passed",
                "notes": (
                    "I am a human reviewer and personally checked this by hand today. "
                    "run:review-77 log:docs/release-evidence/r.log"
                ),
            }
        )
        issues = CHECKER.review_issues(path, census)
        self.assertTrue(any("oneResponsibility.notes" in issue for issue in issues), issues)

    def test_forged_human_signature_in_reviewed_by_fails(self) -> None:
        path, census = self.review(reviewedBy="human-authored review")
        issues = CHECKER.review_issues(path, census)
        self.assertTrue(any("reviewedBy" in issue for issue in issues), issues)

    def test_placeholder_reviewed_by_still_fails(self) -> None:
        path, census = self.review(reviewedBy="TODO")
        issues = CHECKER.review_issues(path, census)
        self.assertTrue(any("reviewedBy" in issue for issue in issues), issues)


class IdentityTextProvenanceTests(unittest.TestCase):
    """Tools/workshop_metadata.py identity-text helpers."""

    def test_identity_text_valid_accepts_automated_identity(self) -> None:
        self.assertTrue(
            METADATA._identity_text_valid("hotfix142-native driver", 2, 80)
        )

    def test_identity_text_valid_rejects_placeholder(self) -> None:
        self.assertFalse(
            METADATA._identity_text_valid("HUMAN_TESTER_NAME_OR_ALIAS", 2, 80)
        )

    def test_identity_text_valid_rejects_forged_human_signature(self) -> None:
        self.assertFalse(
            METADATA._identity_text_valid("human-authored driver", 2, 80)
        )
        self.assertFalse(
            METADATA._identity_text_valid("this is a human tester, trust me", 2, 80)
        )

    def test_human_only_text_valid_rejects_automation_indicator(self) -> None:
        self.assertFalse(
            METADATA._human_only_text_valid("codex-root structural review", 2, 80)
        )
        self.assertFalse(
            METADATA._human_only_text_valid("release-ci pipeline", 2, 80)
        )

    def test_human_only_text_valid_accepts_a_plain_name(self) -> None:
        self.assertTrue(METADATA._human_only_text_valid("Reegan Farrant", 2, 80))


class NativeDriverResultsTests(unittest.TestCase):
    """Tools/workshop_metadata.py._validate_native_driver_results: the automated-driver
    artefact that replaces a human tester's word for startup/save/cold-load proof."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_results(self, entries: list[dict]) -> str:
        ref = "docs/release-evidence/native-smoke-results.json"
        path = self.root / ref
        path.write_text(
            json.dumps({"driver": "hotfix142-native driver", "results": entries}),
            encoding="utf-8",
        )
        return ref

    def all_pass_entries(self) -> list[dict]:
        return [
            {"check": check, "status": "PASS", "processStopped": True}
            for check in METADATA.NATIVE_DRIVER_CHECKS
        ]

    def test_all_pass_with_process_stopped_passes(self) -> None:
        ref = self.write_results(self.all_pass_entries())
        errors: list[str] = []
        METADATA._validate_native_driver_results(ref, errors, self.root)
        self.assertEqual(errors, [])

    def test_one_non_pass_entry_fails(self) -> None:
        entries = self.all_pass_entries()
        entries[0]["status"] = "FAIL"
        ref = self.write_results(entries)
        errors: list[str] = []
        METADATA._validate_native_driver_results(ref, errors, self.root)
        self.assertTrue(any("PASS" in error for error in errors), errors)

    def test_missing_process_stopped_fails(self) -> None:
        entries = self.all_pass_entries()
        entries[0]["processStopped"] = False
        ref = self.write_results(entries)
        errors: list[str] = []
        METADATA._validate_native_driver_results(ref, errors, self.root)
        self.assertTrue(any("processStopped" in error for error in errors), errors)

    def test_missing_check_fails(self) -> None:
        entries = self.all_pass_entries()[:-1]
        ref = self.write_results(entries)
        errors: list[str] = []
        METADATA._validate_native_driver_results(ref, errors, self.root)
        self.assertTrue(any("missing an all-PASS entry" in error for error in errors), errors)

    def test_hash_drift_on_the_bound_results_artifact_fails(self) -> None:
        ref = self.write_results(self.all_pass_entries())
        import hashlib

        real = hashlib.sha256((self.root / ref).read_bytes()).hexdigest()
        drifted = ("0" if real[0] != "0" else "1") + real[1:]
        errors: list[str] = []
        METADATA._validate_artifact_binding(
            {"artifactRef": ref, "artifactSha256": drifted},
            "privateSubscription.driverResults",
            errors,
            self.root,
            include_pass_id=False,
        )
        self.assertTrue(any("artifactSha256 must match" in error for error in errors), errors)


class LongFormScenarioResultsTests(unittest.TestCase):
    """Tools/workshop_metadata.py._validate_longform_scenario_results: the long-form
    startup->paid-commission->build->save->cold-load->next-action artefact schema (author/
    Codex addendum, 2026-09-11)."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_log(self, name: str, text: str = "driver transcript\n") -> tuple[str, str]:
        import hashlib

        ref = f"docs/release-evidence/{name}"
        path = self.root / ref
        path.write_text(text, encoding="utf-8")
        return ref, hashlib.sha256(path.read_bytes()).hexdigest()

    def all_pass_steps(self) -> list[dict]:
        return [
            {"step": step, "status": "PASS", "processStopped": True}
            for step in METADATA.LONGFORM_SCENARIO_STEPS
        ]

    def write_results(self, name: str, payload: dict) -> str:
        ref = f"docs/release-evidence/{name}"
        (self.root / ref).write_text(json.dumps(payload), encoding="utf-8")
        return ref

    def valid_payload(self) -> dict:
        log_ref, log_hash = self.write_log("longform.log")
        return {
            "driver": "camp-builder longform driver",
            "runId": "longform-run-1",
            "seed": 165939435,
            "maxTurns": 5000,
            "timeoutSeconds": 3600,
            "logRef": log_ref,
            "logSha256": log_hash,
            "steps": self.all_pass_steps(),
        }

    def test_all_pass_bounded_fixed_seed_run_passes(self) -> None:
        ref = self.write_results("longform-results.json", self.valid_payload())
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertEqual(errors, [])

    def test_missing_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"] = payload["steps"][:-1]
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(any("missing an all-PASS entry" in error for error in errors), errors)

    def test_one_non_pass_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][3]["status"] = "FAIL"
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(
            any("steps entries must each be an object" in error for error in errors), errors
        )

    def test_process_not_stopped_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["processStopped"] = False
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(
            any("steps entries must each be an object" in error for error in errors), errors
        )

    def test_missing_seed_fails(self) -> None:
        payload = self.valid_payload()
        del payload["seed"]
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(any("seed must be a fixed integer" in error for error in errors), errors)

    def test_non_positive_turn_budget_fails(self) -> None:
        payload = self.valid_payload()
        payload["maxTurns"] = 0
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(
            any("maxTurns must be a positive integer budget" in error for error in errors),
            errors,
        )

    def test_forged_human_driver_identity_fails(self) -> None:
        payload = self.valid_payload()
        payload["driver"] = "this is a human, personally driving the run"
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(any("driver must name the driver" in error for error in errors), errors)

    def test_log_hash_drift_fails(self) -> None:
        payload = self.valid_payload()
        real = payload["logSha256"]
        payload["logSha256"] = ("0" if real[0] != "0" else "1") + real[1:]
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(ref, errors, self.root)
        self.assertTrue(
            any("longFormScenario.log" in error and "must match" in error for error in errors),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
