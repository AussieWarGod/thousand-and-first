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

REPO_ROOT = Path(__file__).resolve().parents[2]
EXAMPLE_IDENTITY_FIELDS = ("capturedBy", "testedBy", "reviewedBy", "driver")


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
        (self.root / "Core").mkdir(parents=True, exist_ok=True)
        (self.root / "Core" / "One.cs").write_text("namespace Fixture;\n", encoding="utf-8")
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

    RUNTIME_INVENTORY = "3" * 64

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

    def observed_for(self, step: str) -> dict:
        base = {"realmId": "realm-1", "cityId": "city-1"}
        if step in ("paid-commission", "engine-turn-build", "next-action"):
            base["jobId"] = "job-1" if step != "next-action" else "job-2"
        if step in ("engine-turn-build", "save", "cold-load", "next-action"):
            base["buildingId"] = "building-1"
            base["plotId"] = "plot-1"
        if step == "engine-turn-build":
            base["completedReceiptId"] = "receipt-1"
            base["forJobId"] = "job-1"
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
            "processes": self.processes_for(),
            "steps": self.all_pass_steps(),
        }

    def processes_for(self) -> list[dict]:
        return [
            {
                "role": "save-session",
                "launchId": "launch-a",
                "started": "2026-09-11T00:00:00Z",
                "stoppedUtc": "2026-09-11T00:05:00Z",
                "profileName": "save-session profile",
                "profileSeal": "4" * 64,
            },
            {
                "role": "cold-load-session",
                "launchId": "launch-b",
                "started": "2026-09-11T00:10:00Z",
                "stoppedUtc": "2026-09-11T00:15:00Z",
                "profileName": "cold-load-session profile (deliberately different)",
                "profileSeal": "5" * 64,
            },
        ]

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

    # --- Per-session profile identity (author/Codex root precision, 2026-09-11) ---

    def test_differing_profile_seal_and_name_across_sessions_passes(self) -> None:
        # save-session and cold-load-session legitimately run different profiles/scripts; a
        # differing profileName/profileSeal alone must never fail.
        payload = self.valid_payload()
        self.assertNotEqual(
            payload["processes"][0]["profileSeal"], payload["processes"][1]["profileSeal"]
        )
        self.assertNotEqual(
            payload["processes"][0]["profileName"], payload["processes"][1]["profileName"]
        )
        self.assertEqual(self.validate(payload), [])

    def test_missing_profile_name_or_seal_fails(self) -> None:
        payload = self.valid_payload()
        del payload["processes"][0]["profileName"]
        errors = self.validate(payload)
        self.assertTrue(
            any("processes entries must each be an object" in error for error in errors), errors
        )

    def test_invalid_profile_seal_shape_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][0]["profileSeal"] = "not-a-hash"
        errors = self.validate(payload)
        self.assertTrue(
            any("profileSeal must be a nonzero lowercase SHA-256" in error for error in errors),
            errors,
        )

    def test_placeholder_profile_name_fails(self) -> None:
        payload = self.valid_payload()
        payload["processes"][0]["profileName"] = "TODO"
        errors = self.validate(payload)
        self.assertTrue(
            any(
                "processes[save-session].profileName must be a real" in error
                for error in errors
            ),
            errors,
        )

    def test_harness_inventory_sha256_absent_passes(self) -> None:
        payload = self.valid_payload()
        self.assertNotIn("harnessInventorySha256", payload)
        self.assertEqual(self.validate(payload), [])

    def test_harness_inventory_sha256_present_and_valid_passes(self) -> None:
        payload = self.valid_payload()
        payload["harnessInventorySha256"] = "c" * 64
        self.assertEqual(self.validate(payload), [])

    def test_harness_inventory_sha256_malformed_when_present_fails(self) -> None:
        payload = self.valid_payload()
        payload["harnessInventorySha256"] = "not-a-hash"
        errors = self.validate(payload)
        self.assertTrue(
            any(
                "harnessInventorySha256, if present, must be a nonzero lowercase SHA-256"
                in error
                for error in errors
            ),
            errors,
        )

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
                "must match the requested tree's own Tools/check-structure.py --json "
                "production structural digest" in error
                for error in errors
            ),
            errors,
        )

    def test_no_caller_supplied_inventory_digest_fails_closed(self) -> None:
        errors: list[str] = []
        ref = self.write_results("longform-results.json", self.valid_payload())
        METADATA._validate_longform_scenario_results(
            ref,
            errors,
            self.root,
            expected_candidate_commit=self.CANDIDATE,
            expected_game_build=METADATA.GAME_CORE_BUILD,
            expected_runtime_inventory_sha256=None,
        )
        self.assertTrue(any("cannot be verified" in error for error in errors), errors)

    def test_stale_structure_review_ledger_still_passes_longform_but_fails_freshness_separately(
        self,
    ) -> None:
        # Author/Codex correction, 2026-09-11: an active candidate's docs/STRUCTURE_REVIEW.json
        # is a separate, freshness-checked artefact -- it is never a source of exercised bytes
        # for this check. A stale ledger (its own digest != the real tree digest) must not
        # affect the longform result at all, and must be caught by check-structure.py's own
        # --release review-freshness gate instead, with its own error text.
        stale_review_digest = "9" * 64
        self.assertNotEqual(stale_review_digest, self.RUNTIME_INVENTORY)
        (self.root / "docs" / "STRUCTURE_REVIEW.json").write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "inventorySha256": stale_review_digest,
                    "exceptions": [],
                    "reviewedBy": "camp-builder structural review",
                    "completedUtc": "2026-09-11T00:00:00Z",
                    "oneResponsibility": {
                        "status": "passed",
                        "notes": "run:x log:docs/release-evidence/x.log",
                    },
                    "protocolsAtBoundaries": {
                        "status": "passed",
                        "notes": "run:x log:docs/release-evidence/x.log",
                    },
                }
            ),
            encoding="utf-8",
        )
        # The longform check passes when given the REAL exercised digest, unaffected by the
        # stale ledger sitting right next to it.
        self.assertEqual(self.validate(self.valid_payload()), [])
        # Separately, check-structure.py's own freshness gate (fed the real current tree's
        # inventory digest) refuses because the ledger does not bind to it.
        (self.root / "Core").mkdir(parents=True, exist_ok=True)
        (self.root / "Core" / "One.cs").write_text("namespace Fixture;\n", encoding="utf-8")
        census = CHECKER.build_census(self.root, ["Core/One.cs"])
        # A distinct real digest for "the current tree" that the stale ledger does not match.
        object.__setattr__(census, "inventory_sha256", self.RUNTIME_INVENTORY)
        review_issues = CHECKER.review_issues(
            self.root / "docs" / "STRUCTURE_REVIEW.json", census
        )
        self.assertTrue(
            any("does not bind the current staged C# inventory" in issue for issue in review_issues),
            review_issues,
        )

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

    def test_next_action_missing_save_id_fails(self) -> None:
        payload = self.valid_payload()
        del payload["steps"][6]["observed"]["saveId"]
        errors = self.validate(payload)
        self.assertTrue(
            any("(next-action) must observe saveId" in error for error in errors), errors
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

    def test_paid_commission_missing_job_id_fails(self) -> None:
        payload = self.valid_payload()
        del payload["steps"][2]["observed"]["jobId"]
        errors = self.validate(payload)
        self.assertTrue(
            any("(paid-commission) must observe jobId" in error for error in errors), errors
        )

    def test_cold_load_missing_save_building_or_plot_fails(self) -> None:
        for key in ("saveId", "buildingId", "plotId"):
            with self.subTest(key=key):
                payload = self.valid_payload()
                del payload["steps"][5]["observed"][key]
                errors = self.validate(payload)
                self.assertTrue(
                    any(f"(cold-load) must observe {key}" in error for error in errors), errors
                )

    def test_engine_turn_build_missing_completed_receipt_linkage_fails(self) -> None:
        for key in ("completedReceiptId", "forJobId"):
            with self.subTest(key=key):
                payload = self.valid_payload()
                del payload["steps"][3]["observed"][key]
                errors = self.validate(payload)
                self.assertTrue(
                    any(f"(engine-turn-build) must observe {key}" in error for error in errors),
                    errors,
                )

    def test_for_job_id_not_matching_paid_commission_job_fails(self) -> None:
        payload = self.valid_payload()
        payload["steps"][3]["observed"]["forJobId"] = "a-different-job"
        errors = self.validate(payload)
        self.assertTrue(
            any(
                "observed.forJobId must equal the jobId observed at paid-commission" in error
                for error in errors
            ),
            errors,
        )

    def test_completed_receipt_id_may_differ_from_job_id(self) -> None:
        # The receipt id itself is free to differ from both the live job and forJobId's value
        # -- only forJobId is required to link back to paid-commission's jobId.
        payload = self.valid_payload()
        payload["steps"][3]["observed"]["completedReceiptId"] = "totally-unrelated-receipt-id"
        self.assertEqual(self.validate(payload), [])


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
        if step == "engine-turn-build":
            base["completedReceiptId"] = "receipt-1"
            base["forJobId"] = "job-1"
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
                    "profileName": "save-session profile",
                    "profileSeal": "7" * 64,
                },
                {
                    "role": "cold-load-session",
                    "launchId": "launch-b",
                    "started": "2026-09-11T00:10:00Z",
                    "stoppedUtc": "2026-09-11T00:15:00Z",
                    "profileName": "cold-load-session profile",
                    "profileSeal": "8" * 64,
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
            exercised_inventory_sha256=self.INVENTORY,
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
                exercised_inventory_sha256=self.INVENTORY,
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
                exercised_inventory_sha256=self.INVENTORY,
            )




class AtCommitArtifactRefCollectionTest(unittest.TestCase):
    """release_evidence_artifact_refs(..., at_commit=...): the real Tools/workshop-package.sh
    invocation reads the evidence document and every nested .json artifact it references from
    the FROZEN HEAD git tree (git show), never the mutable working tree or an extraction
    scratch directory that has not been populated yet. Codex root's read of the actual caller
    (workshop-package.sh:957 calling before dependency extraction) found the filesystem-mode
    collector cannot be called safely there."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self._run("git", "init", "-q")
        self._run("git", "config", "user.email", "fixture@example.com")
        self._run("git", "config", "user.name", "Fixture")
        (self.root / "docs" / "release-evidence").mkdir(parents=True)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def _run(self, *args: str) -> None:
        import subprocess

        subprocess.run(args, cwd=self.root, check=True, capture_output=True)

    def _write(self, relative: str, content: object) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        text = content if isinstance(content, str) else json.dumps(content)
        path.write_text(text, encoding="utf-8")

    def _commit_everything(self) -> str:
        self._run("git", "add", "-A")
        return self._commit_staged()

    def _commit_staged(self) -> str:
        self._run("git", "commit", "-q", "-m", "fixture")
        import subprocess

        return subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=self.root, check=True, capture_output=True, text=True
        ).stdout.strip()

    def test_collects_nested_refs_from_git_objects_with_no_working_tree_files(self) -> None:
        self._write(
            "docs/release-evidence/longform-results.json",
            {"logRef": "docs/release-evidence/longform.log", "other": 1},
        )
        self._write("docs/release-evidence/longform.log", "transcript")
        self._write(
            "docs/RELEASE_EVIDENCE.json",
            {
                "longFormScenario": {
                    "artifactRef": "docs/release-evidence/longform-results.json",
                    "artifactSha256": "a" * 64,
                }
            },
        )
        commit = self._commit_everything()
        # Prove independence from the working tree: delete every extracted file after the
        # commit, exactly as the real pipeline's scratch directory has nothing on disk yet
        # when this call happens.
        for relative in (
            "docs/release-evidence/longform-results.json",
            "docs/release-evidence/longform.log",
            "docs/RELEASE_EVIDENCE.json",
        ):
            (self.root / relative).unlink()

        refs = METADATA.release_evidence_artifact_refs(
            "docs/RELEASE_EVIDENCE.json", repository_root=self.root, at_commit=commit
        )
        self.assertEqual(
            refs,
            (
                "docs/release-evidence/longform-results.json",
                "docs/release-evidence/longform.log",
            ),
        )

    def test_driver_results_ref_key_also_recurses_into_nested_json(self) -> None:
        # Proves recursion is not special-cased to "artifactRef": driverResultsRef is a
        # different declared ref key and must recurse the same way.
        self._write(
            "docs/release-evidence/driver-results.json",
            {"logRef": "docs/release-evidence/driver.log"},
        )
        self._write("docs/release-evidence/driver.log", "transcript")
        self._write(
            "docs/RELEASE_EVIDENCE.json",
            {
                "privateSubscription": {
                    "driverResultsRef": "docs/release-evidence/driver-results.json",
                    "driverResultsSha256": "b" * 64,
                }
            },
        )
        commit = self._commit_everything()
        refs = METADATA.release_evidence_artifact_refs(
            "docs/RELEASE_EVIDENCE.json", repository_root=self.root, at_commit=commit
        )
        self.assertIn("docs/release-evidence/driver.log", refs)

    def test_nested_artifact_only_in_working_tree_not_head_fails(self) -> None:
        # The nested .json artifact (the thing the collector must open to find ITS further
        # refs) is only ever written to the working tree, never committed -- exactly the
        # shape of a harness that forgot to add a new dependency file to the tree. Only the
        # top-level evidence document is committed and references it.
        self._write(
            "docs/RELEASE_EVIDENCE.json",
            {
                "longFormScenario": {
                    "artifactRef": "docs/release-evidence/longform-results.json",
                    "artifactSha256": "a" * 64,
                }
            },
        )
        self._run("git", "add", "docs/RELEASE_EVIDENCE.json")
        commit = self._commit_staged()
        self._write(
            "docs/release-evidence/longform-results.json",
            {"logRef": "docs/release-evidence/longform.log"},
        )
        with self.assertRaisesRegex(METADATA.ValidationError, "absent from HEAD"):
            METADATA.release_evidence_artifact_refs(
                "docs/RELEASE_EVIDENCE.json", repository_root=self.root, at_commit=commit
            )

    def test_top_level_evidence_missing_from_head_fails(self) -> None:
        self._write("README.md", "fixture repo, no evidence document committed")
        commit = self._commit_everything()
        with self.assertRaisesRegex(METADATA.ValidationError, "absent from HEAD"):
            METADATA.release_evidence_artifact_refs(
                "docs/RELEASE_EVIDENCE.json", repository_root=self.root, at_commit=commit
            )

    def test_at_commit_without_repository_root_fails(self) -> None:
        with self.assertRaisesRegex(METADATA.ValidationError, "requires a repository_root"):
            METADATA.release_evidence_artifact_refs(
                "docs/RELEASE_EVIDENCE.json", at_commit="HEAD"
            )




class PackagePathCliSequenceTest(unittest.TestCase):
    """Runs the exact Tools/workshop-package.sh sequence around evidence-artifact-refs -- via
    the same python CLI entry with the same argument shape, and a scratch dir populated in
    the same order the shell script uses -- to prove the fixed dependency-order bug (Codex
    root: the real caller extracts the evidence document and TESTING.md, THEN calls
    evidence-artifact-refs, THEN extracts every declared artifact; a filesystem-mode call at
    that point would look for nested .json artifacts that do not exist in the scratch
    directory yet). This exercises the CLI subprocess boundary, not only the python helper."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(self.temporary.name) / "repo"
        self.scratch = Path(self.temporary.name) / "scratch"
        self.repo.mkdir()
        self.scratch.mkdir()
        (self.repo / "docs" / "release-evidence").mkdir(parents=True)
        self._run("git", "init", "-q")
        self._run("git", "config", "user.email", "fixture@example.com")
        self._run("git", "config", "user.name", "Fixture")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def _run(self, *args: str) -> None:
        import subprocess

        subprocess.run(args, cwd=self.repo, check=True, capture_output=True)

    def _write_repo(self, relative: str, content: object) -> None:
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        text = content if isinstance(content, str) else json.dumps(content)
        path.write_text(text, encoding="utf-8")

    def _commit(self) -> str:
        import subprocess

        self._run("git", "add", "-A")
        self._run("git", "commit", "-q", "-m", "fixture")
        return subprocess.run(
            ["git", "rev-parse", "HEAD"], cwd=self.repo, check=True, capture_output=True, text=True
        ).stdout.strip()

    def _cli(self, *args: str) -> "subprocess.CompletedProcess":
        import subprocess

        return subprocess.run(
            [sys.executable, str(CHECKER_PATH.parent / "workshop_metadata.py"), *args],
            cwd=self.repo,
            capture_output=True,
            text=True,
        )

    def test_package_style_sequence_extracts_every_nested_dependency_before_use(self) -> None:
        self._write_repo(
            "docs/release-evidence/longform-results.json",
            {"logRef": "docs/release-evidence/longform.log"},
        )
        self._write_repo("docs/release-evidence/longform.log", "driver transcript")
        self._write_repo(
            "docs/RELEASE_EVIDENCE.json",
            {
                "longFormScenario": {
                    "artifactRef": "docs/release-evidence/longform-results.json",
                    "artifactSha256": "a" * 64,
                }
            },
        )
        head = self._commit()

        # Step 1 (workshop-package.sh): extract ONLY the evidence document itself into
        # scratch -- nothing else exists there yet.
        scratch_evidence = self.scratch / "evidence.json"
        scratch_evidence.write_text(
            (self.repo / "docs" / "RELEASE_EVIDENCE.json").read_text(encoding="utf-8"),
            encoding="utf-8",
        )

        # Step 2: discover the artifact graph from HEAD, not from the (still-empty) scratch
        # directory -- this is the exact CLI shape Tools/workshop-package.sh uses.
        result = self._cli(
            "evidence-artifact-refs",
            "docs/RELEASE_EVIDENCE.json",
            "--repository-root",
            str(self.repo),
            "--at-commit",
            head,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        refs = [line for line in result.stdout.splitlines() if line]
        self.assertEqual(
            refs,
            [
                "docs/release-evidence/longform-results.json",
                "docs/release-evidence/longform.log",
            ],
        )

        # Step 3: only NOW extract every declared dependency (as the shell loop does), and
        # confirm every one of them is a real, retrievable HEAD blob.
        for ref in refs:
            destination = self.scratch / ref
            destination.parent.mkdir(parents=True, exist_ok=True)
            import subprocess

            blob = subprocess.run(
                ["git", "-C", str(self.repo), "show", f"{head}:{ref}"],
                check=True,
                capture_output=True,
            ).stdout
            destination.write_bytes(blob)
        self.assertTrue((self.scratch / "docs/release-evidence/longform-results.json").is_file())
        self.assertTrue((self.scratch / "docs/release-evidence/longform.log").is_file())

    def test_package_style_sequence_fails_when_nested_dependency_only_in_working_tree(self) -> None:
        self._write_repo(
            "docs/RELEASE_EVIDENCE.json",
            {
                "longFormScenario": {
                    "artifactRef": "docs/release-evidence/longform-results.json",
                    "artifactSha256": "a" * 64,
                }
            },
        )
        head = self._commit()
        # The nested artifact the top-level document declares was never committed.
        self._write_repo(
            "docs/release-evidence/longform-results.json",
            {"logRef": "docs/release-evidence/longform.log"},
        )
        result = self._cli(
            "evidence-artifact-refs",
            "docs/RELEASE_EVIDENCE.json",
            "--repository-root",
            str(self.repo),
            "--at-commit",
            head,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("absent from HEAD", result.stderr)




class RunRecordTest(unittest.TestCase):
    """Tools/workshop_metadata.py.validate_run_record: the camp harness's exact-owned-process
    receipt (test/longform-reachability, Tools/scenario_run_record.py's seal/launch/stop,
    frozen at 8fe3093) -- a SEPARATE artefact from longFormScenario, whose processes[]
    entries never carry these fields. Fixture values (launchId "pid-4242-...", startTicks
    "638000000000000000", receiptRef "process-ownership.json") are copied from that branch's
    own Tools/tests/scenario_run_record_test.py positives, read-only, not invented here."""

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_receipt(self, name: str = "process-ownership.json") -> tuple[str, str]:
        path = self.root / name
        path.write_text(
            json.dumps({"schema": "taf-scenario-process-v1", "pid": 4242}), encoding="utf-8"
        )
        return name, hashlib.sha256(path.read_bytes()).hexdigest()

    def valid_record(self, **overrides) -> dict:
        receipt_ref, receipt_sha = self.write_receipt()
        record = {
            "role": "save-session",
            "root": str(self.root),
            "seed": "#43101",
            "script": "",
            "candidateCommit": "a" * 40,
            "runtimeInventorySha256": "b" * 64,
            "profileName": "taf-scenario.save",
            "profileSeal": "d" * 64,
            "turnBudget": 3000,
            "timeoutSeconds": 1200,
            "sealedUtc": "2026-09-11T09:59:00Z",
            "launchId": "pid-4242-20260911T100000Z",
            "started": "2026-09-11T10:00:00Z",
            "stoppedUtc": "2026-09-11T10:05:00Z",
            "ownership": {
                "receiptRef": receipt_ref,
                "receiptSha256": receipt_sha,
                "pid": 4242,
                "startTicks": "638000000000000000",
                "executable": "CoQ.exe",
            },
            "exitProvenance": METADATA.RUN_RECORD_EXIT_PROVENANCE_OBSERVED,
            "exitCode": 0,
        }
        record.update(overrides)
        return record

    def write(self, record: dict) -> Path:
        path = self.root / "run-record.json"
        path.write_text(json.dumps(record), encoding="utf-8")
        return path

    def test_valid_observed_record_passes(self) -> None:
        self.assertEqual(METADATA.validate_run_record(self.write(self.valid_record())), [])

    def test_valid_unobserved_record_with_no_exit_code_key_passes(self) -> None:
        record = self.valid_record(
            exitProvenance=METADATA.RUN_RECORD_EXIT_PROVENANCE_UNOBSERVED
        )
        del record["exitCode"]
        self.assertEqual(METADATA.validate_run_record(self.write(record)), [])

    def test_unknown_top_level_key_fails(self) -> None:
        record = self.valid_record()
        record["notAField"] = "x"
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("fields must be exactly the required set" in issue for issue in issues), issues)

    def test_missing_required_key_fails(self) -> None:
        record = self.valid_record()
        del record["script"]
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("fields must be exactly the required set" in issue for issue in issues), issues)

    def test_optional_harness_inventory_and_game_build_pass(self) -> None:
        record = self.valid_record(harnessInventorySha256="c" * 64, gameBuildId="2.0.211.51")
        self.assertEqual(METADATA.validate_run_record(self.write(record)), [])

    def test_role_not_in_enum_fails(self) -> None:
        record = self.valid_record(role="some-other-role")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("role must be one of" in issue for issue in issues), issues)

    def test_stopped_before_started_fails(self) -> None:
        record = self.valid_record(started="2026-09-11T10:10:00Z", stoppedUtc="2026-09-11T10:00:00Z")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("stoppedUtc must not be before started" in issue for issue in issues), issues)

    def test_stopped_equal_to_started_passes(self) -> None:
        # Coordinator rule: stoppedUtc >= started (not strictly greater).
        record = self.valid_record(stoppedUtc="2026-09-11T10:00:00Z")
        self.assertEqual(METADATA.validate_run_record(self.write(record)), [])

    def test_start_ticks_as_int_fails(self) -> None:
        record = self.valid_record()
        record["ownership"]["startTicks"] = 638000000000000000
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("startTicks must be a digit string" in issue for issue in issues), issues)

    def test_start_ticks_non_digit_string_fails(self) -> None:
        record = self.valid_record()
        record["ownership"]["startTicks"] = "not-digits"
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("startTicks must be a digit string" in issue for issue in issues), issues)

    def test_receipt_ref_with_path_separator_fails(self) -> None:
        record = self.valid_record()
        record["ownership"]["receiptRef"] = "docs/release-evidence/process-ownership.json"
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("receiptRef must be a bare file name" in issue for issue in issues), issues
        )

    def test_missing_ownership_block_fails(self) -> None:
        record = self.valid_record()
        record["ownership"] = None
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(any("ownership is required" in issue for issue in issues), issues)

    def test_exit_code_present_under_unobserved_provenance_fails(self) -> None:
        record = self.valid_record(
            exitProvenance=METADATA.RUN_RECORD_EXIT_PROVENANCE_UNOBSERVED
        )
        # exitCode key still present (0) from valid_record(), which is refused when unobserved.
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("exitCode must be entirely absent unless" in issue for issue in issues), issues
        )

    def test_missing_exit_code_under_observed_provenance_fails(self) -> None:
        record = self.valid_record()
        del record["exitCode"]
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any(
                "exitCode must be a present integer when exitProvenance is" in issue
                for issue in issues
            ),
            issues,
        )

    def test_launch_id_bare_substring_without_hyphens_fails(self) -> None:
        # A bare substring match ("42424242" contains "4242") must NOT be accepted: the
        # coordinator's rule is hyphen-delimited, matching srr.py:246 / cql.py:568 exactly.
        record = self.valid_record(launchId="pid-42424242-20260911T100000Z")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("launchId must name the owned pid (hyphen-delimited)" in issue for issue in issues),
            issues,
        )

    def test_malformed_receipt_sha_fails(self) -> None:
        record = self.valid_record()
        record["ownership"]["receiptSha256"] = "not-a-hash"
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("receiptSha256 must be a nonzero lowercase SHA-256" in issue for issue in issues),
            issues,
        )

    def test_unknown_exit_provenance_fails(self) -> None:
        record = self.valid_record(exitProvenance="something-else")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("exitProvenance must be one of" in issue for issue in issues), issues
        )

    def test_transient_unobserved_provenance_is_refused_for_a_stopped_record(self) -> None:
        # scenario_run_record.py:251 writes "unobserved" right after launch(), before stop()
        # runs; a record reaching this validator is always stopped, so that transient value
        # is not a third legal provenance.
        record = self.valid_record(exitProvenance="unobserved")
        del record["exitCode"]
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("exitProvenance must be one of" in issue for issue in issues), issues
        )

    def test_hash_drifted_receipt_fails(self) -> None:
        record = self.valid_record()
        real = record["ownership"]["receiptSha256"]
        record["ownership"]["receiptSha256"] = ("0" if real[0] != "0" else "1") + real[1:]
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("receiptSha256 must match the retained receipt" in issue for issue in issues),
            issues,
        )

    def test_missing_receipt_file_fails(self) -> None:
        record = self.valid_record()
        (self.root / "process-ownership.json").unlink()
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("cannot read the owned-process receipt" in issue for issue in issues), issues
        )

    def cold_load_record(self, **overrides) -> dict:
        record = self.valid_record(role="cold-load-session")
        record["inheritedFrom"] = "run-record.json"
        record["inheritedVerified"] = True
        record.update(overrides)
        return record

    def test_cold_load_record_with_inherited_fields_passes(self) -> None:
        self.assertEqual(METADATA.validate_run_record(self.write(self.cold_load_record())), [])

    def test_save_session_with_inherited_from_fails(self) -> None:
        record = self.valid_record()
        record["inheritedFrom"] = "run-record.json"
        record["inheritedVerified"] = True
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("fields must be exactly the required set" in issue for issue in issues), issues
        )

    def test_cold_load_record_missing_inherited_verified_fails(self) -> None:
        record = self.cold_load_record()
        del record["inheritedVerified"]
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("must both be present for a cold-load-session record" in issue for issue in issues),
            issues,
        )

    def test_cold_load_record_inherited_from_with_path_traversal_fails(self) -> None:
        record = self.cold_load_record(inheritedFrom="../outside/run-record.json")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("inheritedFrom must be a safe basename" in issue for issue in issues), issues
        )

    def test_cold_load_record_inherited_from_absolute_path_fails(self) -> None:
        record = self.cold_load_record(inheritedFrom="/etc/passwd")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("inheritedFrom must be a safe basename" in issue for issue in issues), issues
        )

    def test_cold_load_record_inherited_verified_non_bool_fails(self) -> None:
        record = self.cold_load_record(inheritedVerified="true")
        issues = METADATA.validate_run_record(self.write(record))
        self.assertTrue(
            any("inheritedVerified must be a boolean" in issue for issue in issues), issues
        )

    def test_cold_load_record_relative_source_root_inherited_from_passes(self) -> None:
        record = self.cold_load_record(inheritedFrom="fixtures/save-session/run-record.json")
        self.assertEqual(METADATA.validate_run_record(self.write(record)), [])

    def test_paired_save_record_candidate_commit_mismatch_fails(self) -> None:
        record = self.cold_load_record()
        save_record = self.valid_record(candidateCommit="c" * 40)
        issues = METADATA.validate_run_record(
            self.write(record), paired_save_record=save_record
        )
        self.assertTrue(
            any(
                "candidateCommit must match the paired save-session record's value" in issue
                for issue in issues
            ),
            issues,
        )

    def test_paired_save_record_matching_fields_pass(self) -> None:
        record = self.cold_load_record()
        save_record = self.valid_record()
        issues = METADATA.validate_run_record(
            self.write(record), paired_save_record=save_record
        )
        self.assertEqual(issues, [])

class ExampleTemplateIdentityPlaceholderTest(unittest.TestCase):
    """Copilot review, PR #154: docs/**/*.example.json ship identity placeholders shaped
    "..._NAME_OR_HONESTLY_LABELLED_..._IDENTITY", which _identity_text_valid ACCEPTS (it only
    rejects PLACEHOLDER_SENTINEL/FORGED_HUMAN_SIGNATURE matches), so an unedited template
    passes the provenance gate. Every identity field in every shipped example template must
    use the sentinel form instead ("REPLACE_WITH_...")."""

    def _example_identity_values(self):
        found = []
        for path in sorted(REPO_ROOT.joinpath("docs").rglob("*.example.json")):
            payload = json.loads(path.read_text(encoding="utf-8-sig"))
            found.extend(self._walk(payload, path))
        return found

    def _walk(self, node, path):
        found = []
        if isinstance(node, dict):
            for key, value in node.items():
                if key in EXAMPLE_IDENTITY_FIELDS and isinstance(value, str):
                    found.append((path, key, value))
                else:
                    found.extend(self._walk(value, path))
        elif isinstance(node, list):
            for item in node:
                found.extend(self._walk(item, path))
        return found

    def test_every_example_identity_field_rejects_as_a_placeholder(self) -> None:
        found = self._example_identity_values()
        self.assertGreater(len(found), 0, "no example identity fields found -- test is inert")
        for path, key, value in found:
            self.assertFalse(
                METADATA._identity_text_valid(value, 2, 80),
                f"{path}:{key} = {value!r} must NOT validate as a real identity "
                "(it is a template placeholder)",
            )


if __name__ == "__main__":
    unittest.main()
