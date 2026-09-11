#!/usr/bin/env python3
"""Tests for the automated-only release evidence provenance policy (author ruling
2026-09-11, extended by the same-day media/listing correction and Codex root's protocol
review of the long-form scenario artefact): reviewedBy/testedBy/capturedBy may be an
honestly labelled automated identity bound to a real artefact instead of a human name,
forged human-signature claims are always refused, native startup/save/reload proof is an
automated-driver results artefact, and a full startup->paid-commission->build->save->
cold-load->next-action run must prove one real, continuous, bounded, identity-bound
execution -- never seven arbitrary PASS strings.
"""

from __future__ import annotations

import hashlib
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

    def review(self, **overrides) -> tuple[Path, object]:
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


class PreviewReviewProvenanceTests(unittest.TestCase):
    """Tools/workshop_metadata.py._validate_preview_review: preview-media provenance is an
    automated-capture-plus-artefact requirement, not a mandatory human capturer/reviewer
    (author correction, 2026-09-11 -- media/listing review is not authentication/legal
    acceptance and the "no manual test gate" ruling supersedes the earlier human-only rule)."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_log(self, name: str, text: str = "capture tool transcript\n") -> tuple[str, str]:
        ref = f"docs/release-evidence/{name}"
        path = self.root / ref
        path.write_text(text, encoding="utf-8")
        return ref, hashlib.sha256(path.read_bytes()).hexdigest()

    def valid_payload(self, **overrides) -> dict:
        provenance_ref, provenance_hash = self.write_log("preview-capture.log")
        payload = {
            "passId": METADATA.PREVIEW_REVIEW_PASS_ID,
            "artifactRef": "docs/release-evidence/final-native-preview-review.txt",
            "artifactSha256": "1" * 64,
            "source": "native-game-screenshot",
            "generativeAssistance": False,
            "previewSha256": "a" * 64,
            "capturedBy": "camp-builder capture tool",
            "captureUtc": "2026-09-11T00:00:00Z",
            "sourceSave": "quickstart-marsh-seed-1",
            "editSummary": "Cropped to 512x512, no other edits",
            "captureProvenanceRef": provenance_ref,
            "captureProvenanceSha256": provenance_hash,
            "reviewedBy": None,
            "completedUtc": None,
        }
        (self.root / "docs" / "release-evidence" / "final-native-preview-review.txt").write_text(
            "x", encoding="utf-8"
        )
        payload["artifactSha256"] = hashlib.sha256(
            (self.root / payload["artifactRef"]).read_bytes()
        ).hexdigest()
        payload.update(overrides)
        return payload

    def call(self, payload: dict) -> list[str]:
        errors: list[str] = []
        METADATA._validate_preview_review(payload, "a" * 64, errors, self.root)
        return errors

    def test_automated_capture_with_bound_provenance_and_no_review_passes(self) -> None:
        self.assertEqual(self.call(self.valid_payload()), [])

    def test_automated_capture_with_optional_automated_review_passes(self) -> None:
        payload = self.valid_payload(
            reviewedBy="camp-builder aesthetic review", completedUtc="2026-09-11T01:00:00Z"
        )
        self.assertEqual(self.call(payload), [])

    def test_unbound_preview_image_hash_fails(self) -> None:
        errors: list[str] = []
        METADATA._validate_preview_review(self.valid_payload(), "b" * 64, errors, self.root)
        self.assertTrue(any("previewSha256 must match" in error for error in errors), errors)

    def test_missing_capture_provenance_fails(self) -> None:
        payload = self.valid_payload(
            captureProvenanceRef="docs/release-evidence/does-not-exist.log"
        )
        errors = self.call(payload)
        self.assertTrue(
            any("captureProvenance" in error for error in errors), errors
        )

    def test_forged_human_captured_by_fails(self) -> None:
        payload = self.valid_payload(capturedBy="this is a human, I took it myself")
        errors = self.call(payload)
        self.assertTrue(any("capturedBy must name" in error for error in errors), errors)

    def test_forged_human_reviewed_by_fails(self) -> None:
        payload = self.valid_payload(
            reviewedBy="human-authored review", completedUtc="2026-09-11T01:00:00Z"
        )
        errors = self.call(payload)
        self.assertTrue(any("reviewedBy must name" in error for error in errors), errors)

    def test_reviewed_by_without_completed_utc_fails(self) -> None:
        payload = self.valid_payload(reviewedBy="camp-builder aesthetic review")
        errors = self.call(payload)
        self.assertTrue(any("must both be null" in error for error in errors), errors)


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
    """Tools/workshop_metadata.py._validate_longform_scenario_results: the exact ordered
    seven-step chain, session-level (not per-step) process lifecycle, candidate/build/
    continuity binding, and per-phase bounded execution required by Codex root's protocol
    review, 2026-09-11."""

    CANDIDATE = "a" * 40

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_log(self, name: str, text: str = "driver transcript\n") -> tuple[str, str]:
        ref = f"docs/release-evidence/{name}"
        path = self.root / ref
        path.write_text(text, encoding="utf-8")
        return ref, hashlib.sha256(path.read_bytes()).hexdigest()

    def all_pass_steps(self) -> list[dict]:
        return [
            {
                "step": step,
                "status": "PASS",
                "turnsUsed": 10,
                "elapsedSeconds": 5,
                "turnBudget": 100,
                "timeoutSeconds": 60,
            }
            for step in METADATA.LONGFORM_SCENARIO_STEPS
        ]

    def write_results(self, name: str, payload: dict) -> str:
        ref = f"docs/release-evidence/{name}"
        (self.root / ref).write_text(json.dumps(payload), encoding="utf-8")
        return ref

    def valid_payload(self) -> dict:
        log_ref, log_hash = self.write_log("longform.log")
        return {
            "schemaVersion": 1,
            "driver": "camp-builder longform driver",
            "runId": "longform-run-1",
            "seed": 165939435,
            "candidateCommit": self.CANDIDATE,
            "runtimeInventorySha256": "2" * 64,
            "gameBuildId": METADATA.GAME_CORE_BUILD,
            "logRef": log_ref,
            "logSha256": log_hash,
            "continuity": {
                "realmId": "realm-1",
                "cityId": "city-1",
                "jobId": "job-1",
                "buildingId": "building-1",
                "plotId": "plot-1",
                "saveId": "save-1",
            },
            "processes": [
                {
                    "role": "save-session",
                    "launchId": "launch-a",
                    "started": "2026-09-11T00:00:00Z",
                    "stopped": True,
                },
                {
                    "role": "cold-load-session",
                    "launchId": "launch-b",
                    "started": "2026-09-11T00:10:00Z",
                    "stopped": True,
                },
            ],
            "steps": self.all_pass_steps(),
        }

    def validate(self, payload: dict) -> list[str]:
        ref = self.write_results("longform-results.json", payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(
            ref,
            errors,
            self.root,
            expected_candidate_commit=self.CANDIDATE,
            expected_game_build=METADATA.GAME_CORE_BUILD,
        )
        return errors

    def test_exact_ordered_chain_bounded_execution_passes(self) -> None:
        self.assertEqual(self.validate(self.valid_payload()), [])

    def test_missing_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"] = payload["steps"][:-1]
        errors = self.validate(payload)
        self.assertTrue(any("exact ordered chain" in error for error in errors), errors)

    def test_duplicate_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][6] = dict(payload["steps"][5])
        errors = self.validate(payload)
        self.assertTrue(
            any("in the fixed chain order" in error for error in errors), errors
        )

    def test_wrong_order_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0], payload["steps"][1] = payload["steps"][1], payload["steps"][0]
        errors = self.validate(payload)
        self.assertTrue(
            any("in the fixed chain order" in error for error in errors), errors
        )

    def test_extra_unknown_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"].append(
            {
                "step": "extra",
                "status": "PASS",
                "turnsUsed": 1,
                "elapsedSeconds": 1,
                "turnBudget": 10,
                "timeoutSeconds": 10,
            }
        )
        errors = self.validate(payload)
        self.assertTrue(any("exact ordered chain" in error for error in errors), errors)

    def test_one_non_pass_step_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][3]["status"] = "FAIL"
        errors = self.validate(payload)
        self.assertTrue(any("must be PASS" in error for error in errors), errors)

    def test_per_step_stopped_flag_is_refused_not_ignored(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["processStopped"] = True
        errors = self.validate(payload)
        self.assertTrue(any("fields must be" in error for error in errors), errors)

    def test_missing_process_session_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"] = [payload["processes"][0]]
        errors = self.validate(payload)
        self.assertTrue(any("exactly the roles" in error for error in errors), errors)

    def test_duplicate_process_role_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][1]["role"] = "save-session"
        errors = self.validate(payload)
        self.assertTrue(any("no duplicates" in error for error in errors), errors)

    def test_process_not_stopped_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][0]["stopped"] = False
        errors = self.validate(payload)
        self.assertTrue(any("stopped true" in error for error in errors), errors)

    def test_missing_continuity_identity_fails(self) -> None:
        payload = self.valid_payload()
        payload["continuity"]["saveId"] = ""
        errors = self.validate(payload)
        self.assertTrue(any("continuity.saveId" in error for error in errors), errors)

    def test_wrong_candidate_commit_fails(self) -> None:
        payload = self.valid_payload()
        payload["candidateCommit"] = "b" * 40
        errors = self.validate(payload)
        self.assertTrue(
            any("must match the release evidence candidateCommit" in error for error in errors),
            errors,
        )

    def test_wrong_game_build_fails(self) -> None:
        payload = self.valid_payload()
        payload["gameBuildId"] = "0.0.0.0"
        errors = self.validate(payload)
        self.assertTrue(any("gameBuildId must be" in error for error in errors), errors)

    def test_missing_seed_fails(self) -> None:
        payload = self.valid_payload()
        del payload["seed"]
        errors = self.validate(payload)
        self.assertTrue(any("fields must exactly match" in error for error in errors), errors)

    def test_over_turn_budget_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][2]["turnsUsed"] = 999
        payload["steps"][2]["turnBudget"] = 10
        errors = self.validate(payload)
        self.assertTrue(any("exceeded its turnBudget" in error for error in errors), errors)

    def test_over_timeout_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][2]["elapsedSeconds"] = 999
        payload["steps"][2]["timeoutSeconds"] = 10
        errors = self.validate(payload)
        self.assertTrue(any("exceeded its timeoutSeconds" in error for error in errors), errors)

    def test_forged_human_driver_identity_fails(self) -> None:
        payload = self.valid_payload()
        payload["driver"] = "this is a human, personally driving the run"
        errors = self.validate(payload)
        self.assertTrue(any("driver must name the driver" in error for error in errors), errors)

    def test_log_hash_drift_fails(self) -> None:
        payload = self.valid_payload()
        real = payload["logSha256"]
        payload["logSha256"] = ("0" if real[0] != "0" else "1") + real[1:]
        errors = self.validate(payload)
        self.assertTrue(
            any("longFormScenario.log" in error and "must match" in error for error in errors),
            errors,
        )

    def test_duplicate_json_key_fails(self) -> None:
        ref = "docs/release-evidence/longform-dup-key.json"
        valid = self.valid_payload()
        rendered = json.dumps(valid)
        # Inject a duplicate top-level key by hand: JSON permits this on the wire even though
        # Python's dumps will not produce it, so simulate a harness that emits one.
        rendered = rendered.replace(
            '"schemaVersion": 1,', '"schemaVersion": 1, "schemaVersion": 1,', 1
        )
        (self.root / ref).write_text(rendered, encoding="utf-8")
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(
            ref,
            errors,
            self.root,
            expected_candidate_commit=self.CANDIDATE,
            expected_game_build=METADATA.GAME_CORE_BUILD,
        )
        self.assertTrue(any("duplicate JSON key" in error for error in errors), errors)


if __name__ == "__main__":
    unittest.main()
