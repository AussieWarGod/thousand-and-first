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
import struct
import sys
import tempfile
import unittest
import zlib
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
    seven-step chain, session-level (not per-step) process lifecycle with distinct launches
    and ordered timestamps, candidate/build/runtime-inventory binding against the real
    exercised tree, per-phase bounded execution with no zero-budget bypass, and lifecycle-
    gated per-step continuity observations. Codex root's protocol review, 2026-09-11, plus
    the follow-up executable-validation-gap and continuity-clarification corrections."""

    CANDIDATE = "a" * 40

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)
        review = {
            "schemaVersion": 1,
            "inventorySha256": "3" * 64,
            "exceptions": [],
            "reviewedBy": "camp-builder structural review",
            "completedUtc": "2026-09-11T00:00:00Z",
            "oneResponsibility": {"status": "passed", "notes": "run:x log:docs/release-evidence/x.log"},
            "protocolsAtBoundaries": {"status": "passed", "notes": "run:x log:docs/release-evidence/x.log"},
        }
        (self.root / "docs" / "STRUCTURE_REVIEW.json").write_text(
            json.dumps(review), encoding="utf-8"
        )
        self.RUNTIME_INVENTORY = review["inventorySha256"]

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_log(self, name: str, text: str = "driver transcript\n") -> tuple[str, str]:
        ref = f"docs/release-evidence/{name}"
        path = self.root / ref
        path.write_text(text, encoding="utf-8")
        return ref, hashlib.sha256(path.read_bytes()).hexdigest()

    def observed_for(self, step: str) -> dict:
        base = {"realmId": "realm-1", "cityId": "city-1"}
        if step in ("paid-commission", "engine-turn-build", "next-action"):
            base["jobId"] = "job-1" if step != "next-action" else "job-2"
        if step in ("engine-turn-build", "save", "cold-load", "next-action"):
            base["buildingId"] = "building-1"
            base["plotId"] = "plot-1"
        if step in ("save", "cold-load", "next-action"):
            base["saveId"] = "save-1"
        return base

    def all_pass_steps(self) -> list[dict]:
        return [
            {
                "step": step,
                "status": "PASS",
                "turnsUsed": 10,
                "elapsedSeconds": 5,
                "turnBudget": 100,
                "timeoutSeconds": 60,
                "observed": self.observed_for(step),
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
            "runtimeInventorySha256": self.RUNTIME_INVENTORY,
            "gameBuildId": METADATA.GAME_CORE_BUILD,
            "logRef": log_ref,
            "logSha256": log_hash,
            "continuity": {"realmId": "realm-1", "cityId": "city-1"},
            "processes": [
                {
                    "role": "save-session",
                    "launchId": "launch-a",
                    "started": "2026-09-11T00:00:00Z",
                    "stoppedUtc": "2026-09-11T00:05:00Z",
                },
                {
                    "role": "cold-load-session",
                    "launchId": "launch-b",
                    "started": "2026-09-11T00:10:00Z",
                    "stoppedUtc": "2026-09-11T00:15:00Z",
                },
            ],
            "steps": self.all_pass_steps(),
        }

    def validate(self, payload: dict, *, ref_name: str = "longform-results.json") -> list[str]:
        ref = self.write_results(ref_name, payload)
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(
            ref,
            errors,
            self.root,
            expected_candidate_commit=self.CANDIDATE,
            expected_game_build=METADATA.GAME_CORE_BUILD,
            expected_runtime_inventory_sha256=self.RUNTIME_INVENTORY,
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
        self.assertTrue(any("in the fixed chain order" in error for error in errors), errors)

    def test_wrong_order_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0], payload["steps"][1] = payload["steps"][1], payload["steps"][0]
        errors = self.validate(payload)
        self.assertTrue(any("in the fixed chain order" in error for error in errors), errors)

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
                "observed": {"realmId": "realm-1", "cityId": "city-1"},
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
        del payload["processes"][0]["stoppedUtc"]
        payload["processes"][0]["stopped"] = True
        errors = self.validate(payload)
        self.assertTrue(any("stoppedUtc" in error for error in errors), errors)

    def test_process_started_after_stopped_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][0]["started"], payload["processes"][0]["stoppedUtc"] = (
            payload["processes"][0]["stoppedUtc"],
            payload["processes"][0]["started"],
        )
        errors = self.validate(payload)
        self.assertTrue(any("strictly before stoppedUtc" in error for error in errors), errors)

    def test_shared_launch_id_across_sessions_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][1]["launchId"] = payload["processes"][0]["launchId"]
        errors = self.validate(payload)
        self.assertTrue(any("two distinct launchId" in error for error in errors), errors)

    def test_cold_load_session_starting_before_save_session_stops_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][1]["started"] = "2026-09-11T00:01:00Z"
        errors = self.validate(payload)
        self.assertTrue(
            any("save-session must stop at or before" in error for error in errors), errors
        )

    def test_missing_continuity_identity_fails(self) -> None:
        payload = self.valid_payload()
        payload["continuity"]["cityId"] = ""
        errors = self.validate(payload)
        self.assertTrue(any("continuity.cityId" in error for error in errors), errors)

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

    def test_wrong_runtime_inventory_digest_fails(self) -> None:
        payload = self.valid_payload()
        payload["runtimeInventorySha256"] = "4" * 64
        errors = self.validate(payload)
        self.assertTrue(
            any(
                "must match the exercised tree's docs/STRUCTURE_REVIEW.json" in error
                for error in errors
            ),
            errors,
        )

    def test_unreadable_structure_review_fails_closed(self) -> None:
        (self.root / "docs" / "STRUCTURE_REVIEW.json").unlink()
        self.assertIsNone(METADATA._structure_review_inventory_sha256(self.root))
        ref = self.write_results("longform-results.json", self.valid_payload())
        errors: list[str] = []
        METADATA._validate_longform_scenario_results(
            ref,
            errors,
            self.root,
            expected_candidate_commit=self.CANDIDATE,
            expected_game_build=METADATA.GAME_CORE_BUILD,
            expected_runtime_inventory_sha256=METADATA._structure_review_inventory_sha256(
                self.root
            ),
        )
        self.assertTrue(any("cannot be verified" in error for error in errors), errors)

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

    def test_zero_budget_does_not_bypass_over_usage(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["turnsUsed"] = 999
        payload["steps"][0]["turnBudget"] = 0
        errors = self.validate(payload)
        self.assertTrue(any("exceeded its turnBudget" in error for error in errors), errors)

    def test_negative_budget_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["turnBudget"] = -1
        errors = self.validate(payload)
        self.assertTrue(any("turnBudget must be a non-negative integer" in error for error in errors), errors)

    def test_float_turns_used_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["turnsUsed"] = 1.5
        errors = self.validate(payload)
        self.assertTrue(any("turnsUsed must be a non-negative integer" in error for error in errors), errors)

    def test_string_budget_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["turnBudget"] = "unbounded"
        errors = self.validate(payload)
        self.assertTrue(any("turnBudget must be a non-negative integer" in error for error in errors), errors)

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
        valid = self.valid_payload()
        ref = "docs/release-evidence/longform-dup-key.json"
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
            expected_runtime_inventory_sha256=self.RUNTIME_INVENTORY,
        )
        self.assertTrue(any("duplicate JSON key" in error for error in errors), errors)

    # --- Lifecycle-gated continuity observations (author/Codex root clarification) ---

    def test_job_id_before_paid_commission_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["observed"]["jobId"] = "job-early"
        errors = self.validate(payload)
        self.assertTrue(any("observed jobId before its creation step" in error for error in errors), errors)

    def test_building_id_before_engine_turn_build_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][2]["observed"]["buildingId"] = "building-early"
        errors = self.validate(payload)
        self.assertTrue(
            any("observed buildingId before its creation step" in error for error in errors), errors
        )

    def test_save_id_before_save_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][3]["observed"]["saveId"] = "save-early"
        errors = self.validate(payload)
        self.assertTrue(any("observed saveId before its creation step" in error for error in errors), errors)

    def test_engine_turn_build_missing_building_or_plot_fails(self) -> None:
        payload = self.valid_payload()
        del payload["steps"][3]["observed"]["buildingId"]
        errors = self.validate(payload)
        self.assertTrue(
            any("must observe buildingId" in error for error in errors), errors
        )

    def test_save_step_missing_save_id_fails(self) -> None:
        payload = self.valid_payload()
        del payload["steps"][4]["observed"]["saveId"]
        errors = self.validate(payload)
        self.assertTrue(any("must observe saveId" in error for error in errors), errors)

    def test_building_id_changes_after_first_observed_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][4]["observed"]["buildingId"] = "a-different-building"
        errors = self.validate(payload)
        self.assertTrue(
            any("observed.buildingId must equal the value first observed" in error for error in errors),
            errors,
        )

    def test_save_id_changes_after_first_observed_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][5]["observed"]["saveId"] = "a-different-save"
        errors = self.validate(payload)
        self.assertTrue(
            any("observed.saveId must equal the value first observed" in error for error in errors), errors
        )

    def test_next_action_may_observe_a_brand_new_job_id(self) -> None:
        # No linkage required between the completed job and a fresh one next-action starts.
        payload = self.valid_payload()
        self.assertEqual(
            payload["steps"][6]["observed"]["jobId"], "job-2", "fixture sanity: distinct job"
        )
        self.assertEqual(self.validate(payload), [])

    def test_engine_turn_build_job_id_may_differ_from_paid_commission_receipt(self) -> None:
        # engine-turn-build's jobId may name the completed receipt, not the live job; no
        # equality is enforced against paid-commission's jobId.
        payload = self.valid_payload()
        payload["steps"][3]["observed"]["jobId"] = "receipt-99"
        self.assertEqual(self.validate(payload), [])

    def test_fabricated_looking_observed_id_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][3]["observed"]["buildingId"] = "PLACEHOLDER"
        errors = self.validate(payload)
        self.assertTrue(any("non-placeholder identity" in error for error in errors), errors)

    def test_observed_realm_or_city_mismatch_with_continuity_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["observed"]["realmId"] = "a-different-realm"
        errors = self.validate(payload)
        self.assertTrue(any("observed.realmId must match continuity" in error for error in errors), errors)

    def test_observed_unknown_key_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][0]["observed"]["notAField"] = "x"
        errors = self.validate(payload)
        self.assertTrue(any("keys must be a subset of" in error for error in errors), errors)


class EndToEndReleaseEvidenceFixtureTest(unittest.TestCase):
    """A full, real docs/RELEASE_EVIDENCE.json plus every retained artifact it binds, run
    through validate_release_evidence AND release_evidence_artifact_refs -- not helper-only
    unit tests. Proves the whole document validates, and that the artifact-ref collector
    freezes every nested/raw log dependency (driverResultsRef, captureProvenanceRef, and the
    longFormScenario results file's own nested logRef), per Codex root's protocol review."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "docs" / "release-evidence").mkdir(parents=True)
        self.CANDIDATE = "c" * 40
        self.INVENTORY = "5" * 64

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def sha(self, path: Path) -> str:
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def write_text_artifact(self, name: str, text: str) -> tuple[str, str]:
        ref = f"docs/release-evidence/{name}"
        path = self.root / ref
        path.write_text(text, encoding="utf-8")
        return ref, self.sha(path)

    def _e2e_observed_for(self, step: str) -> dict:
        base = {"realmId": "realm-1", "cityId": "city-1"}
        if step in ("paid-commission", "engine-turn-build", "next-action"):
            base["jobId"] = "job-1" if step != "next-action" else "job-2"
        if step in ("engine-turn-build", "save", "cold-load", "next-action"):
            base["buildingId"] = "building-1"
            base["plotId"] = "plot-1"
        if step in ("save", "cold-load", "next-action"):
            base["saveId"] = "save-1"
        return base

    def write_preview(self) -> Path:
        def chunk(kind: bytes, body: bytes) -> bytes:
            return (
                struct.pack(">I", len(body))
                + kind
                + body
                + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
            )

        raw = b"".join(b"\x00" + b"\x18\x24\x30" * 512 for _ in range(512))
        payload = (
            b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b"")
        )
        path = self.root / "preview.png"
        path.write_bytes(payload)
        return path

    def build(self) -> dict:
        """Every path/hash the fixture needs, plus the finished evidence dict."""
        manifest = {"version": "1.2.3"}
        readme = self.root / "README.md"
        readme.write_text(
            "# Fixture\n\n**Status: 1.2.3 public playtest release.**\n", encoding="utf-8"
        )
        changelog = self.root / "CHANGELOG.md"
        changelog.write_text("# Changelog\n\n## [1.2.3] — 2026-09-11\n", encoding="utf-8")

        testing = self.root / "TESTING.md"
        testing.write_text(
            "| Step | Action | Expect |\n|---|---|---|\n| 1 | Do it | Passed |\n"
            "| 2 | Do it | Passed |\n",
            encoding="utf-8",
        )

        workshop = self.root / "workshop.json"
        workshop.write_text(json.dumps({"WorkshopId": 3796495680}), encoding="utf-8")

        preview = self.write_preview()
        preview_hash = self.sha(preview)

        assembly_hash = "6" * 64
        native_compile_ref, native_compile_hash_actual = self.write_text_artifact(
            "native-compile-load.log", f"Assembly-CSharp SHA-256: {assembly_hash}\n"
        )
        gallery_ref, gallery_hash = self.write_text_artifact("architecture-gallery.zip", "gallery")
        controller_ref, controller_hash = self.write_text_artifact(
            "controller-color-accessibility.txt", "controller ok"
        )
        density_ref, density_hash = self.write_text_artifact("dense-city-performance.csv", "perf")
        survey_ref, survey_hash = self.write_text_artifact("one-survey-receipt.txt", "survey")
        matrix_ref, matrix_hash = self.write_text_artifact("compatibility-matrix.csv", "matrix")
        capture_provenance_ref, capture_provenance_hash = self.write_text_artifact(
            "preview-capture.log", "capture tool transcript"
        )
        preview_review_ref, preview_review_hash = self.write_text_artifact(
            "final-native-preview-review.txt", "preview review"
        )
        numbered_ref, numbered_hash = self.write_text_artifact(
            "numbered-protocols.txt", "numbered transcript"
        )

        native_results_ref, _ = self.write_text_artifact(
            "native-smoke-results.json",
            json.dumps(
                {
                    "driver": "camp-builder native driver",
                    "results": [
                        {"check": check, "status": "PASS", "processStopped": True}
                        for check in METADATA.NATIVE_DRIVER_CHECKS
                    ],
                }
            ),
        )
        native_results_hash = self.sha(self.root / native_results_ref)

        longform_log_ref, longform_log_hash = self.write_text_artifact(
            "longform.log", "longform driver transcript"
        )
        longform_payload = {
            "schemaVersion": 1,
            "driver": "camp-builder longform driver",
            "runId": "longform-run-1",
            "seed": 165939435,
            "candidateCommit": self.CANDIDATE,
            "runtimeInventorySha256": self.INVENTORY,
            "gameBuildId": METADATA.GAME_CORE_BUILD,
            "logRef": longform_log_ref,
            "logSha256": longform_log_hash,
            "continuity": {"realmId": "realm-1", "cityId": "city-1"},
            "processes": [
                {
                    "role": "save-session",
                    "launchId": "launch-a",
                    "started": "2026-09-11T00:00:00Z",
                    "stoppedUtc": "2026-09-11T00:05:00Z",
                },
                {
                    "role": "cold-load-session",
                    "launchId": "launch-b",
                    "started": "2026-09-11T00:10:00Z",
                    "stoppedUtc": "2026-09-11T00:15:00Z",
                },
            ],
            "steps": [
                {
                    "step": step,
                    "status": "PASS",
                    "turnsUsed": 1,
                    "elapsedSeconds": 1,
                    "turnBudget": 10,
                    "timeoutSeconds": 10,
                    "observed": self._e2e_observed_for(step),
                }
                for step in METADATA.LONGFORM_SCENARIO_STEPS
            ],
        }
        longform_ref, _ = self.write_text_artifact(
            "longform-results.json", json.dumps(longform_payload)
        )
        longform_hash = self.sha(self.root / longform_ref)

        review = {
            "schemaVersion": 1,
            "inventorySha256": self.INVENTORY,
            "exceptions": [],
            "reviewedBy": "camp-builder structural review",
            "completedUtc": "2026-09-11T00:00:00Z",
            "oneResponsibility": {"status": "passed", "notes": "run:x log:docs/release-evidence/x.log"},
            "protocolsAtBoundaries": {"status": "passed", "notes": "run:x log:docs/release-evidence/x.log"},
        }
        (self.root / "docs" / "STRUCTURE_REVIEW.json").write_text(
            json.dumps(review), encoding="utf-8"
        )

        evidence = {
            "schemaVersion": METADATA.RELEASE_EVIDENCE_SCHEMA,
            "releaseVersion": "1.2.3",
            "candidateCommit": self.CANDIDATE,
            "gameMarketingVersion": METADATA.GAME_MARKETING_VERSION,
            "gameCoreBuild": METADATA.GAME_CORE_BUILD,
            "gameAssemblySha256": assembly_hash,
            "workshopId": 3796495680,
            "previewSha256": preview_hash,
            "privatePackageReceiptSha256": "7" * 64,
            "verification": {
                "nativeCompileLoad": {
                    "passId": "native-compile-load",
                    "artifactRef": native_compile_ref,
                    "artifactSha256": native_compile_hash_actual,
                },
                "architectureGallery": {
                    "passId": "architecture-gallery",
                    "artifactRef": gallery_ref,
                    "artifactSha256": gallery_hash,
                },
                "controllerAndColor": {
                    "passId": "controller-color-accessibility",
                    "artifactRef": controller_ref,
                    "artifactSha256": controller_hash,
                },
                "denseCityPerformance": {
                    "passId": "dense-city-performance",
                    "artifactRef": density_ref,
                    "artifactSha256": density_hash,
                },
                "oneSurveyReceipt": {
                    "passId": "one-survey-receipt",
                    "artifactRef": survey_ref,
                    "artifactSha256": survey_hash,
                },
                "compatibilityMatrix": {
                    "passId": "compatibility-matrix",
                    "artifactRef": matrix_ref,
                    "artifactSha256": matrix_hash,
                },
                "previewReview": {
                    "passId": METADATA.PREVIEW_REVIEW_PASS_ID,
                    "artifactRef": preview_review_ref,
                    "artifactSha256": preview_review_hash,
                    "source": "native-game-screenshot",
                    "generativeAssistance": False,
                    "previewSha256": preview_hash,
                    "capturedBy": "camp-builder capture tool",
                    "captureUtc": "2026-09-11T00:00:00Z",
                    "sourceSave": "quickstart-marsh-seed-1",
                    "editSummary": "Cropped to 512x512, no other edits",
                    "captureProvenanceRef": capture_provenance_ref,
                    "captureProvenanceSha256": capture_provenance_hash,
                    "reviewedBy": None,
                    "completedUtc": None,
                },
                "numberedProtocols": {
                    "artifactRef": numbered_ref,
                    "artifactSha256": numbered_hash,
                    "passIds": ["1", "2"],
                    "waivers": [],
                },
            },
            "privateSubscription": {
                "source": "steam-subscription",
                "inventory": "clean",
                "receipt": "clean",
                "loader": "passed",
                "newGame": "passed",
                "saveReload": "passed",
                "oldSave": "passed",
                "representativeFeatures": "passed",
                "playerLog": "clean",
                "localDuplicatesRemoved": True,
                "uploadHiddenFiles": True,
                "testedBy": "camp-builder native driver",
                "driverResultsRef": native_results_ref,
                "driverResultsSha256": native_results_hash,
                "driverExitCode": 0,
                "completedUtc": "2026-09-11T00:00:00Z",
            },
            "longFormScenario": {
                "artifactRef": longform_ref,
                "artifactSha256": longform_hash,
            },
        }
        evidence_path = self.root / "docs" / "RELEASE_EVIDENCE.json"
        evidence_path.write_text(json.dumps(evidence), encoding="utf-8")

        return {
            "manifest": manifest,
            "preview": preview,
            "workshop": workshop,
            "evidence_path": evidence_path,
            "readme": readme,
            "changelog": changelog,
            "testing": testing,
            "native_results_ref": native_results_ref,
            "longform_ref": longform_ref,
            "longform_log_ref": longform_log_ref,
            "capture_provenance_ref": capture_provenance_ref,
        }

    def test_full_document_validates_end_to_end(self) -> None:
        fixture = self.build()
        candidate = METADATA.validate_release_evidence(
            fixture["manifest"],
            fixture["preview"],
            fixture["workshop"],
            fixture["evidence_path"],
            fixture["readme"],
            fixture["changelog"],
            repository_root=self.root,
            testing_path=fixture["testing"],
        )
        self.assertEqual(candidate, self.CANDIDATE)

    def test_artifact_ref_collector_freezes_every_nested_dependency(self) -> None:
        fixture = self.build()
        refs = METADATA.release_evidence_artifact_refs(
            fixture["evidence_path"], repository_root=self.root
        )
        for expected in (
            fixture["native_results_ref"],
            fixture["longform_ref"],
            fixture["longform_log_ref"],
            fixture["capture_provenance_ref"],
        ):
            self.assertIn(expected, refs, f"{expected!r} missing from frozen artifact refs")

    def test_unsafe_nested_logref_fails_extraction_collection(self) -> None:
        fixture = self.build()
        (self.root / fixture["longform_ref"]).write_text(
            json.dumps({"logRef": "docs/release-evidence/../../escape.log"}), encoding="utf-8"
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "unsafe logRef"):
            METADATA.release_evidence_artifact_refs(
                fixture["evidence_path"], repository_root=self.root
            )

    def test_missing_nested_logref_fails_full_validation(self) -> None:
        fixture = self.build()
        (self.root / fixture["longform_log_ref"]).unlink()
        with self.assertRaisesRegex(METADATA.ValidationError, "longFormScenario.log"):
            METADATA.validate_release_evidence(
                fixture["manifest"],
                fixture["preview"],
                fixture["workshop"],
                fixture["evidence_path"],
                fixture["readme"],
                fixture["changelog"],
                repository_root=self.root,
                testing_path=fixture["testing"],
            )

    def test_hash_drifted_nested_logref_fails_full_validation(self) -> None:
        fixture = self.build()
        (self.root / fixture["longform_log_ref"]).write_text(
            "tampered transcript", encoding="utf-8"
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "longFormScenario.log"):
            METADATA.validate_release_evidence(
                fixture["manifest"],
                fixture["preview"],
                fixture["workshop"],
                fixture["evidence_path"],
                fixture["readme"],
                fixture["changelog"],
                repository_root=self.root,
                testing_path=fixture["testing"],
            )




if __name__ == "__main__":
    unittest.main()
