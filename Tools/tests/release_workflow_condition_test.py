#!/usr/bin/env python3
"""Regression pin for the release.yml finalize/publish/verify condition shape (issue #96).

On the first staging run (34327428688) `finalize` was skipped because its `if` used a bare
`success()`, which is false whenever an ancestor job (`public-confirm`, skipped by design on
staging) did not itself succeed. Fixed in #92 by mirroring the explicit
`!cancelled() && needs.<job>.result == 'success'` form used by `publish`/`verify`.

The checker below parses each condition into its top-level `&&` conjuncts (respecting
parentheses, so an OR-grouped alternative stays one conjunct) rather than substring-matching
the whole condition string. For every job whose transitive `needs` include `public-confirm`
it requires: an `if` is present at all; `!cancelled()` is one of the conjuncts; an explicit
`needs.<dep>.result == 'success'` conjunct for every direct dependency that is itself on the
`public-confirm` chain (`dep == public-confirm` or `public-confirm` in `dep`'s own transitive
needs); and, for `finalize` specifically, an exact
`needs.verify.outputs.status == 'SubscribedInstallationVerified'` conjunct. A condition that
merely lacks the literal substring `success()` -- e.g. a bare `!cancelled()` with every
dependency-success conjunct silently dropped -- must still fail.
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


def top_level_conjuncts(condition: str) -> list[str]:
    """Split a GitHub Actions `if:` expression into its top-level `&&` operands.

    Strips a surrounding `${{ ... }}` wrapper if present. Splitting respects parenthesis
    depth, so an OR-grouped alternative such as
    `(needs.public-confirm.result == 'success' || (... && needs.public-confirm.result ==
    'skipped'))` stays a single conjunct instead of being torn apart by the `&&` inside it.
    """
    expr = condition.strip()
    if expr.startswith("${{") and expr.endswith("}}"):
        expr = expr[3:-2].strip()
    conjuncts: list[str] = []
    depth = 0
    current: list[str] = []
    i = 0
    while i < len(expr):
        char = expr[i]
        if char == "(":
            depth += 1
            current.append(char)
        elif char == ")":
            depth -= 1
            current.append(char)
        elif depth == 0 and expr[i : i + 2] == "&&":
            conjuncts.append("".join(current).strip())
            current = []
            i += 2
            continue
        else:
            current.append(char)
        i += 1
    tail = "".join(current).strip()
    if tail:
        conjuncts.append(tail)
    return [c for c in conjuncts if c]


def dependency_gate_violations(jobs: dict[str, Any], gate_job: str) -> list[str]:
    """Return one violation message per contract breach for every job gated on `gate_job`.

    Checked per job `name` whose transitive `needs` include `gate_job`:
      (a) an `if` condition is present at all -- a job silently added to the chain with no
          `if` runs unconditionally once its needs are satisfied, which is exactly the
          "ran when it shouldn't have" failure mode this contract exists to prevent.
      (b) `!cancelled()` is one of the top-level conjuncts.
      (c) for every job `dep` in `name`'s DIRECT `needs` that is itself on the `gate_job`
          chain (`dep == gate_job`, or `gate_job` is in `dep`'s own transitive needs), some
          conjunct contains the exact text `needs.<dep>.result == 'success'`. This is what a
          bare `success()` -- or a `!cancelled()`-only condition -- silently drops.
      (d) `finalize` specifically also names `needs.verify.outputs.status ==
          'SubscribedInstallationVerified'` in some conjunct.
    """
    violations: list[str] = []
    for name in jobs_gated_on(jobs, gate_job):
        job = jobs[name]
        raw_condition = job.get("if")
        if not raw_condition:
            violations.append(f"job '{name}' has no `if` condition while gated on '{gate_job}'")
            continue
        conjuncts = top_level_conjuncts(str(raw_condition))
        if not any(c == "!cancelled()" for c in conjuncts):
            violations.append(f"job '{name}' is missing a top-level !cancelled() conjunct")
        for dep in _needs_list(job):
            on_chain = dep == gate_job or gate_job in transitive_needs(jobs, dep)
            if not on_chain:
                continue
            required = f"needs.{dep}.result == 'success'"
            if not any(required in c for c in conjuncts):
                violations.append(
                    f"job '{name}' is missing an explicit {required} conjunct for its "
                    f"dependency '{dep}', which is on the '{gate_job}' chain"
                )
        if name == "finalize":
            required_status = "needs.verify.outputs.status == 'SubscribedInstallationVerified'"
            if not any(required_status in c for c in conjuncts):
                violations.append(f"job 'finalize' is missing an exact {required_status} conjunct")
    return violations


# Shared synthetic scaffold: the real chain shape (public-confirm -> publish -> verify ->
# finalize) with everything except the job under test wired to the known-good #92 form, so
# each negative case isolates exactly one broken job.
GOOD_PUBLISH_IF = (
    "${{ !cancelled() && needs.release-checks.result == 'success' && "
    "needs.portable-tests.result == 'success' && "
    "(needs.public-confirm.result == 'success' || "
    "(needs.release-checks.outputs.lane == 'staging' && "
    "needs.public-confirm.result == 'skipped')) }}"
)
GOOD_VERIFY_IF = "${{ !cancelled() && needs.publish.result == 'success' }}"
GOOD_FINALIZE_IF = (
    "${{ !cancelled() && needs.release-checks.result == 'success' && "
    "needs.publish.result == 'success' && needs.verify.result == 'success' && "
    "needs.verify.outputs.status == 'SubscribedInstallationVerified' }}"
)


def scaffold(**overrides: dict[str, Any]) -> dict[str, Any]:
    jobs = {
        "release-checks": {},
        "portable-tests": {},
        "public-confirm": {"needs": ["release-checks", "portable-tests"]},
        "publish": {
            "needs": ["release-checks", "portable-tests", "public-confirm"],
            "if": GOOD_PUBLISH_IF,
        },
        "verify": {"needs": ["release-checks", "publish"], "if": GOOD_VERIFY_IF},
        "finalize": {
            "needs": ["release-checks", "publish", "verify"],
            "if": GOOD_FINALIZE_IF,
        },
    }
    for name, override in overrides.items():
        jobs[name] = {**jobs.get(name, {}), **override}
    return jobs


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

    def test_actual_release_workflow_has_no_dependency_gate_violations(self) -> None:
        violations = dependency_gate_violations(self.jobs, "public-confirm")
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

    def test_good_scaffold_has_no_violations(self) -> None:
        # Sanity check on the shared fixture itself, so every negative test below is known to
        # be isolating exactly the one thing it breaks.
        self.assertEqual(dependency_gate_violations(scaffold(), "public-confirm"), [])

    def test_synthetic_old_bare_success_condition_fails_the_check(self) -> None:
        """Construct the pre-#92 finalize condition text and confirm the guard rejects it."""
        jobs = scaffold(finalize={"if": "${{ success() }}"})
        violations = dependency_gate_violations(jobs, "public-confirm")
        # A bare success() supplies none of the required conjuncts: !cancelled() is absent,
        # every dependency-success conjunct is absent, and the verify-status conjunct is absent.
        self.assertTrue(any("finalize" in v and "!cancelled()" in v for v in violations))
        self.assertTrue(any("finalize" in v and "needs.publish.result" in v for v in violations))
        self.assertTrue(any("finalize" in v and "needs.verify.result" in v for v in violations))
        self.assertTrue(any("finalize" in v and "SubscribedInstallationVerified" in v for v in violations))

    def test_synthetic_fixed_condition_passes_the_check(self) -> None:
        """The #92 fix form for finalize must pass the same guard."""
        self.assertEqual(dependency_gate_violations(scaffold(), "public-confirm"), [])

    def test_cancelled_only_condition_with_dependency_conjuncts_dropped_fails(self) -> None:
        """A `!cancelled()`-only condition contains no literal `success()` substring at all,
        so a checker that only searches for that substring would wrongly accept it even
        though every dependency-success gate has been silently removed."""
        for name, deps in (("publish", ["public-confirm"]), ("verify", ["publish"])):
            with self.subTest(job=name):
                jobs = scaffold(**{name: {"if": "${{ !cancelled() }}"}})
                violations = dependency_gate_violations(jobs, "public-confirm")
                for dep in deps:
                    self.assertTrue(
                        any(name in v and f"needs.{dep}.result" in v for v in violations),
                        f"expected a missing needs.{dep}.result violation for {name}: {violations}",
                    )

    def test_dependency_conjuncts_only_missing_cancelled_fails(self) -> None:
        """The mirror case: every dependency-success conjunct present but !cancelled() dropped."""
        jobs = scaffold(verify={"if": "${{ needs.publish.result == 'success' }}"})
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any(v == "job 'verify' is missing a top-level !cancelled() conjunct" for v in violations))

    def test_missing_if_condition_entirely_fails(self) -> None:
        """A new job transitively gated on public-confirm with no `if` at all must fail --
        it would otherwise run unconditionally as soon as its needs are satisfied."""
        jobs = scaffold()
        jobs["notify"] = {"needs": ["verify"]}
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn("job 'notify' has no `if` condition while gated on 'public-confirm'", violations)

    def test_dropped_dependency_conjunct_on_publish_fails(self) -> None:
        """publish's needs.public-confirm.result == 'success' term (embedded in the
        staging-skip OR alternative) removed, everything else intact."""
        jobs = scaffold(publish={
            "if": (
                "${{ !cancelled() && needs.release-checks.result == 'success' && "
                "needs.portable-tests.result == 'success' }}"
            ),
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn(
            "job 'publish' is missing an explicit needs.public-confirm.result == 'success' "
            "conjunct for its dependency 'public-confirm', which is on the 'public-confirm' chain",
            violations,
        )

    def test_dropped_dependency_conjunct_on_verify_fails(self) -> None:
        jobs = scaffold(verify={"if": "${{ !cancelled() }}"})
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn(
            "job 'verify' is missing an explicit needs.publish.result == 'success' conjunct "
            "for its dependency 'publish', which is on the 'public-confirm' chain",
            violations,
        )

    def test_finalize_missing_publish_or_verify_conjunct_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.verify.result == 'success' && "
                  "needs.verify.outputs.status == 'SubscribedInstallationVerified' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn(
            "job 'finalize' is missing an explicit needs.publish.result == 'success' conjunct "
            "for its dependency 'publish', which is on the 'public-confirm' chain",
            violations,
        )

    def test_finalize_wrong_status_string_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.publish.result == 'success' && needs.verify.result == 'success' && "
                  "needs.verify.outputs.status == 'Verified' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn(
            "job 'finalize' is missing an exact needs.verify.outputs.status == "
            "'SubscribedInstallationVerified' conjunct",
            violations,
        )

    def test_finalize_missing_status_string_entirely_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.publish.result == 'success' && needs.verify.result == 'success' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn(
            "job 'finalize' is missing an exact needs.verify.outputs.status == "
            "'SubscribedInstallationVerified' conjunct",
            violations,
        )


if __name__ == "__main__":
    unittest.main()
