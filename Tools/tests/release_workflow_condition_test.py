#!/usr/bin/env python3
"""Regression pin for the release.yml finalize/publish/verify condition shape (issue #96).

On the first staging run (34327428688) `finalize` was skipped because its `if` used a bare
`success()`, which is false whenever an ancestor job (`public-confirm`, skipped by design on
staging) did not itself succeed. Fixed in #92 by mirroring the explicit
`!cancelled() && needs.<job>.result == 'success'` form used by `publish`/`verify`. This test
fails if any job whose transitive `needs` chain includes `public-confirm` reintroduces a bare
`success()`, and pins the `finalize` job's explicit dependency on the verify status output.
"""

from __future__ import annotations

import unittest
from pathlib import Path
from typing import Any

import yaml

WORKFLOW_PATH = Path(__file__).resolve().parents[2] / ".github" / "workflows" / "release.yml"


def load_workflow(path: Path) -> dict[str, Any]:
    """Parse the workflow YAML. Only the `jobs:` mapping is used below, so the YAML 1.1
    boolean-folding quirk on the bare `on:` key (parsed as True by PyYAML's SafeLoader) is
    harmless here."""
    with path.open("r", encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def transitive_needs(jobs: dict[str, Any], job_name: str) -> set[str]:
    """All job names transitively reachable via `needs` from `job_name` (exclusive)."""
    seen: set[str] = set()
    stack = list(_needs_list(jobs[job_name]))
    while stack:
        name = stack.pop()
        if name in seen:
            continue
        seen.add(name)
        stack.extend(_needs_list(jobs.get(name, {})))
    return seen


def _needs_list(job: dict[str, Any]) -> list[str]:
    needs = job.get("needs", [])
    if isinstance(needs, str):
        return [needs]
    return list(needs)


def jobs_gated_on(jobs: dict[str, Any], gate_job: str) -> list[str]:
    """Job names whose transitive `needs` include `gate_job`."""
    return [
        name
        for name in jobs
        if name != gate_job and gate_job in transitive_needs(jobs, name)
    ]


def check_no_bare_success(jobs: dict[str, Any], gate_job: str) -> list[str]:
    """Return violation messages for jobs gated on `gate_job` using a bare `success()`.

    A job satisfies the contract only if its `if` condition contains `!cancelled()` AND, for
    every job it (directly) needs, an explicit `needs.<job>.result == 'success'` (or the
    staging-skip alternative for `gate_job` itself) term. A bare `success()` call anywhere in
    the condition is always a violation because it implicitly requires every ancestor --
    including `gate_job`, which is skipped by design on staging -- to have succeeded.
    """
    violations: list[str] = []
    for name in jobs_gated_on(jobs, gate_job):
        condition = str(jobs[name].get("if", ""))
        if "success()" in condition:
            violations.append(
                f"job '{name}' uses a bare success() while transitively depending on "
                f"'{gate_job}', which is skipped by design on staging"
            )
    return violations


class ReleaseWorkflowConditionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workflow = load_workflow(WORKFLOW_PATH)
        self.jobs = self.workflow["jobs"]

    def test_public_confirm_is_a_real_gate_with_dependents(self) -> None:
        # The regression only matters if jobs actually depend (transitively) on public-confirm.
        gated = jobs_gated_on(self.jobs, "public-confirm")
        self.assertIn("publish", gated)
        self.assertIn("verify", gated)
        self.assertIn("finalize", gated)

    def test_no_job_gated_on_public_confirm_uses_bare_success(self) -> None:
        violations = check_no_bare_success(self.jobs, "public-confirm")
        self.assertEqual(violations, [], "\n".join(violations))

    def test_publish_and_verify_use_explicit_cancelled_and_result_form(self) -> None:
        for name in ("publish", "verify"):
            condition = str(self.jobs[name].get("if", ""))
            self.assertIn("!cancelled()", condition, f"{name}.if is missing !cancelled()")
            self.assertNotIn("success()", condition, f"{name}.if uses bare success()")

    def test_finalize_names_the_verify_status_output(self) -> None:
        condition = str(self.jobs["finalize"].get("if", ""))
        self.assertIn("!cancelled()", condition)
        self.assertIn("needs.verify.result == 'success'", condition)
        self.assertIn(
            "needs.verify.outputs.status == 'SubscribedInstallationVerified'", condition
        )

    def test_synthetic_old_bare_success_condition_fails_the_check(self) -> None:
        """Construct the pre-#92 finalize condition text and confirm the guard rejects it."""
        old_finalize_condition = "${{ success() }}"
        synthetic_jobs = {
            "release-checks": {},
            "portable-tests": {},
            "public-confirm": {"needs": ["release-checks", "portable-tests"]},
            "publish": {
                "needs": ["release-checks", "portable-tests", "public-confirm"],
                "if": (
                    "${{ !cancelled() && needs.release-checks.result == 'success' && "
                    "needs.portable-tests.result == 'success' && "
                    "(needs.public-confirm.result == 'success' || "
                    "(needs.release-checks.outputs.lane == 'staging' && "
                    "needs.public-confirm.result == 'skipped')) }}"
                ),
            },
            "verify": {
                "needs": ["release-checks", "publish"],
                "if": "${{ !cancelled() && needs.publish.result == 'success' }}",
            },
            "finalize": {
                "needs": ["release-checks", "publish", "verify"],
                "if": old_finalize_condition,
            },
        }
        violations = check_no_bare_success(synthetic_jobs, "public-confirm")
        self.assertEqual(len(violations), 1)
        self.assertIn("finalize", violations[0])

    def test_synthetic_fixed_condition_passes_the_check(self) -> None:
        """The #92 fix form for finalize must pass the same guard."""
        fixed_finalize_condition = (
            "${{ !cancelled() && needs.release-checks.result == 'success' && "
            "needs.publish.result == 'success' && needs.verify.result == 'success' && "
            "needs.verify.outputs.status == 'SubscribedInstallationVerified' }}"
        )
        synthetic_jobs = {
            "release-checks": {},
            "portable-tests": {},
            "public-confirm": {"needs": ["release-checks", "portable-tests"]},
            "publish": {
                "needs": ["release-checks", "portable-tests", "public-confirm"],
                "if": (
                    "${{ !cancelled() && needs.release-checks.result == 'success' && "
                    "needs.portable-tests.result == 'success' && "
                    "(needs.public-confirm.result == 'success' || "
                    "(needs.release-checks.outputs.lane == 'staging' && "
                    "needs.public-confirm.result == 'skipped')) }}"
                ),
            },
            "verify": {
                "needs": ["release-checks", "publish"],
                "if": "${{ !cancelled() && needs.publish.result == 'success' }}",
            },
            "finalize": {
                "needs": ["release-checks", "publish", "verify"],
                "if": fixed_finalize_condition,
            },
        }
        violations = check_no_bare_success(synthetic_jobs, "public-confirm")
        self.assertEqual(violations, [])


if __name__ == "__main__":
    unittest.main()
