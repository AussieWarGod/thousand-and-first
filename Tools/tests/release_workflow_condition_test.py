#!/usr/bin/env python3
"""Regression pin for the release.yml finalize/publish/verify condition shape (issue #96).

On the first staging run (34327428688) `finalize` was skipped because its `if` used a bare
`success()`, which is false whenever an ancestor job (`public-confirm`, skipped by design on
staging) did not itself succeed. Fixed in #92 by mirroring the explicit
`!cancelled() && needs.<job>.result == 'success'` form used by `publish`/`verify`.

The checker parses each condition into its top-level `&&` conjuncts (respecting parentheses,
so an OR-grouped alternative stays one conjunct) and requires each required conjunct to match
EXACTLY (after whitespace normalization) -- never merely "contains" or "is a substring of".
Substring/containment matching is not enough: `(needs.publish.result == 'success' || true)` or
`needs.verify.outputs.status == 'SubscribedInstallationVerified' || true` both CONTAIN the
required text while making the whole conjunct vacuously true, silently admitting a failed
dependency. Exact-conjunct matching (with exactly one allowlisted alternative form, for the
public-confirm staging-skip case) rejects both.

`&&` binds tighter than `||` (as in JS), so a top-level (unparenthesized) `||` anywhere in
the condition changes what the required `&&` operands actually gate: `!cancelled() &&
needs.publish.result == 'success' && false || true` parses as
`(!cancelled() && needs.publish.result == 'success' && false) || true` -- always true --
even though splitting naively on `&&` alone would still show every required conjunct present.
The checker rejects any such top-level `||` outright; the one allowed `||` is the
public-confirm staging-skip alternative, which is always fully parenthesized and therefore
never at depth 0.

For every job whose transitive `needs` include `public-confirm` the checker requires:
  (a) an `if` is present at all;
  (b) `!cancelled()` is a top-level conjunct, verbatim;
  (c) an EXACT `needs.<dep>.result == 'success'` conjunct for every direct dependency that is
      itself on the `public-confirm` chain -- with one allowlisted alternative form for
      `dep == public-confirm` itself, matching release.yml's staging-skip OR-expression
      exactly: `(needs.public-confirm.result == 'success' || (needs.release-checks.outputs.lane
      == 'staging' && needs.public-confirm.result == 'skipped'))`;
  (d) an EXACT `needs.release-checks.result == 'success'` / `needs.portable-tests.result ==
      'success'` conjunct for the specific jobs release.yml already gates on those non-chain
      dependencies (`publish` on both, `finalize` on release-checks) -- `verify` is
      deliberately NOT required to re-check `release-checks` in its own `if`, matching the
      real workflow, since by the time `verify` runs `publish` has already required it;
  (e) `finalize` names an EXACT `needs.verify.outputs.status ==
      'SubscribedInstallationVerified'` conjunct.
"""

from __future__ import annotations

import re
import unittest
from pathlib import Path
from typing import Any

import yaml

WORKFLOW_PATH = Path(__file__).resolve().parents[2] / ".github" / "workflows" / "release.yml"

# Per-job pin of the non-chain dependency successes release.yml's real `if` conditions already
# require, in addition to whatever the generic on-chain rule (see dependency_gate_violations)
# demands. This is a pin, not a derivation from `needs:` -- `verify` also directly `needs:
# release-checks` but its `if` deliberately never re-checks it, so "present in `needs:`" is not
# a safe proxy for "must appear in `if`".
REQUIRED_NON_CHAIN_SUCCESS_DEPS: dict[str, tuple[str, ...]] = {
    "publish": ("release-checks", "portable-tests"),
    "finalize": ("release-checks",),
}

_WHITESPACE_RUN = re.compile(r"\s+")


def normalize_conjunct(conjunct: str) -> str:
    """Collapse internal whitespace runs to a single space and strip. Exact-match comparisons
    below run on this normalized form so incidental formatting differences don't matter, while
    everything structural (parens, operators, an appended `|| true`) still must match exactly.
    """
    return _WHITESPACE_RUN.sub(" ", conjunct).strip()


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


def strip_template_wrapper(condition: str) -> str:
    """Strip a surrounding `${{ ... }}` wrapper if present."""
    expr = condition.strip()
    if expr.startswith("${{") and expr.endswith("}}"):
        expr = expr[3:-2].strip()
    return expr


def has_unparenthesized_top_level_or(condition: str) -> bool:
    """True if `condition` contains a `||` at parenthesis depth 0.

    `&&` binds tighter than `||` in GitHub Actions expressions (as in JS), so
    `A && B && false || true` parses as `(A && B && false) || true` -- an always-true
    condition -- even though splitting naively on top-level `&&` would still show `B` as one
    of the operands. The one allowed `||` is the staging-skip alternative, which is always
    fully parenthesized and therefore sits at depth >= 1, never depth 0.
    """
    expr = strip_template_wrapper(condition)
    depth = 0
    i = 0
    while i < len(expr):
        char = expr[i]
        if char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
        elif depth == 0 and expr[i : i + 2] == "||":
            return True
        i += 1
    return False


def top_level_conjuncts(condition: str) -> list[str]:
    """Split a GitHub Actions `if:` expression into its top-level `&&` operands.

    Strips a surrounding `${{ ... }}` wrapper if present. Splitting respects parenthesis
    depth, so an OR-grouped alternative such as
    `(needs.public-confirm.result == 'success' || (... && needs.public-confirm.result ==
    'skipped'))` stays a single conjunct instead of being torn apart by the `&&` inside it.

    Callers MUST check has_unparenthesized_top_level_or() first: a top-level `||` changes
    `&&`'s precedence (see that function's docstring), so naively splitting on `&&` alone can
    make an always-true expression look like it still contains every required operand.
    """
    expr = strip_template_wrapper(condition)
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


def exact_success_conjunct(dep: str) -> str:
    return normalize_conjunct(f"needs.{dep}.result == 'success'")


def staging_skip_alternative(gate_job: str) -> str:
    """The one allowlisted alternative to a plain success conjunct for `gate_job` itself,
    matching release.yml's staging-skip OR-expression exactly (whitespace-normalized)."""
    return normalize_conjunct(
        f"(needs.{gate_job}.result == 'success' || "
        f"(needs.release-checks.outputs.lane == 'staging' && needs.{gate_job}.result == 'skipped'))"
    )


EXACT_STATUS_CONJUNCT = normalize_conjunct(
    "needs.verify.outputs.status == 'SubscribedInstallationVerified'"
)


def dependency_gate_violations(jobs: dict[str, Any], gate_job: str) -> list[str]:
    """Return one violation message per contract breach for every job gated on `gate_job`.

    Every required conjunct below is matched EXACTLY (after whitespace normalization) against
    the job's top-level conjuncts -- never by substring/containment -- so an OR-true injection
    such as `(needs.publish.result == 'success' || true)` or
    `needs.verify.outputs.status == 'SubscribedInstallationVerified' || true` is rejected: it
    contains the required text but is not equal to it, and it vacuously satisfies the
    dependency instead of actually gating on it.
    """
    violations: list[str] = []
    for name in jobs_gated_on(jobs, gate_job):
        job = jobs[name]
        raw_condition = job.get("if")
        if not raw_condition:
            violations.append(f"job '{name}' has no `if` condition while gated on '{gate_job}'")
            continue
        if has_unparenthesized_top_level_or(str(raw_condition)):
            violations.append(
                f"job '{name}' has an unparenthesized top-level || in its if condition -- "
                f"&& binds tighter than ||, so this can make the whole condition vacuously "
                f"true regardless of the required conjuncts"
            )
            continue
        conjuncts = [normalize_conjunct(c) for c in top_level_conjuncts(str(raw_condition))]

        if "!cancelled()" not in conjuncts:
            violations.append(f"job '{name}' is missing a top-level !cancelled() conjunct")

        required_deps: list[str] = [
            dep for dep in _needs_list(job)
            if dep == gate_job or gate_job in transitive_needs(jobs, dep)
        ]
        required_deps += [
            dep for dep in REQUIRED_NON_CHAIN_SUCCESS_DEPS.get(name, ())
            if dep in _needs_list(job) and dep not in required_deps
        ]
        for dep in required_deps:
            allowed = {exact_success_conjunct(dep)}
            if dep == gate_job:
                allowed.add(staging_skip_alternative(gate_job))
            if not any(c in allowed for c in conjuncts):
                violations.append(
                    f"job '{name}' has no exact conjunct for dependency '{dep}' "
                    f"(expected one of: {sorted(allowed)!r})"
                )

        if name == "finalize" and EXACT_STATUS_CONJUNCT not in conjuncts:
            violations.append(f"job 'finalize' has no exact {EXACT_STATUS_CONJUNCT!r} conjunct")
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
        self.assertTrue(any("finalize" in v and "!cancelled()" in v for v in violations))
        self.assertTrue(any("finalize" in v and "'publish'" in v for v in violations))
        self.assertTrue(any("finalize" in v and "'verify'" in v for v in violations))
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
                        any(name in v and f"'{dep}'" in v for v in violations),
                        f"expected a missing exact conjunct violation for {name}/{dep}: {violations}",
                    )

    def test_dependency_conjuncts_only_missing_cancelled_fails(self) -> None:
        """The mirror case: every dependency-success conjunct present but !cancelled() dropped."""
        jobs = scaffold(verify={"if": "${{ needs.publish.result == 'success' }}"})
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn("job 'verify' is missing a top-level !cancelled() conjunct", violations)

    def test_missing_if_condition_entirely_fails(self) -> None:
        """A new job transitively gated on public-confirm with no `if` at all must fail --
        it would otherwise run unconditionally as soon as its needs are satisfied."""
        jobs = scaffold()
        jobs["notify"] = {"needs": ["verify"]}
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertIn("job 'notify' has no `if` condition while gated on 'public-confirm'", violations)

    def test_dropped_dependency_conjunct_on_publish_fails(self) -> None:
        """publish's public-confirm alternative dropped, everything else intact."""
        jobs = scaffold(publish={
            "if": (
                "${{ !cancelled() && needs.release-checks.result == 'success' && "
                "needs.portable-tests.result == 'success' }}"
            ),
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any("publish" in v and "'public-confirm'" in v for v in violations))

    def test_dropped_dependency_conjunct_on_verify_fails(self) -> None:
        jobs = scaffold(verify={"if": "${{ !cancelled() }}"})
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any("verify" in v and "'publish'" in v for v in violations))

    def test_finalize_missing_publish_or_verify_conjunct_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.verify.result == 'success' && "
                  "needs.verify.outputs.status == 'SubscribedInstallationVerified' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any("finalize" in v and "'publish'" in v for v in violations))

    def test_finalize_wrong_status_string_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.publish.result == 'success' && needs.verify.result == 'success' && "
                  "needs.verify.outputs.status == 'Verified' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any("finalize" in v and "exact" in v and "conjunct" in v for v in violations))

    def test_finalize_missing_status_string_entirely_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.publish.result == 'success' && needs.verify.result == 'success' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(any("finalize" in v and "exact" in v and "conjunct" in v for v in violations))

    def test_or_true_on_a_dependency_conjunct_fails(self) -> None:
        """`(needs.publish.result == 'success' || true)` CONTAINS the required text but is
        vacuously true regardless of publish's actual result -- a substring-matching checker
        would wrongly accept it. verify must still reject it."""
        jobs = scaffold(verify={
            "if": "${{ !cancelled() && (needs.publish.result == 'success' || true) }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("verify" in v and "'publish'" in v for v in violations),
            f"expected verify to reject the OR-true publish conjunct: {violations}",
        )

    def test_or_true_on_the_finalize_status_conjunct_fails(self) -> None:
        """`needs.verify.outputs.status == 'SubscribedInstallationVerified' || true` likewise
        contains the required status string while being vacuously true."""
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.release-checks.result == 'success' && "
                  "needs.publish.result == 'success' && needs.verify.result == 'success' && "
                  "(needs.verify.outputs.status == 'SubscribedInstallationVerified' || true) }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("finalize" in v and "exact" in v and "conjunct" in v for v in violations),
            f"expected finalize to reject the OR-true status conjunct: {violations}",
        )

    def test_dropped_release_checks_conjunct_on_publish_fails(self) -> None:
        jobs = scaffold(publish={
            "if": (
                "${{ !cancelled() && needs.portable-tests.result == 'success' && "
                "(needs.public-confirm.result == 'success' || "
                "(needs.release-checks.outputs.lane == 'staging' && "
                "needs.public-confirm.result == 'skipped')) }}"
            ),
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("publish" in v and "'release-checks'" in v for v in violations),
            f"expected publish to require release-checks success: {violations}",
        )

    def test_dropped_portable_tests_conjunct_on_publish_fails(self) -> None:
        jobs = scaffold(publish={
            "if": (
                "${{ !cancelled() && needs.release-checks.result == 'success' && "
                "(needs.public-confirm.result == 'success' || "
                "(needs.release-checks.outputs.lane == 'staging' && "
                "needs.public-confirm.result == 'skipped')) }}"
            ),
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("publish" in v and "'portable-tests'" in v for v in violations),
            f"expected publish to require portable-tests success: {violations}",
        )

    def test_unparenthesized_top_level_or_bypasses_everything_and_is_rejected(self) -> None:
        """`&&` binds tighter than `||`, so `!cancelled() && needs.publish.result ==
        'success' && false || true` parses as `(!cancelled() && ... && false) || true` --
        always true -- even though naive `&&`-only splitting would still show every required
        operand present as its own conjunct. This must be rejected outright, not conjunct-
        checked as if it were a well-formed AND chain."""
        jobs = scaffold(verify={
            "if": "${{ !cancelled() && needs.publish.result == 'success' && false || true }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("verify" in v and "unparenthesized top-level ||" in v for v in violations),
            f"expected verify to reject the top-level || bypass: {violations}",
        )

    def test_dropped_release_checks_conjunct_on_finalize_fails(self) -> None:
        jobs = scaffold(finalize={
            "if": "${{ !cancelled() && needs.publish.result == 'success' && "
                  "needs.verify.result == 'success' && "
                  "needs.verify.outputs.status == 'SubscribedInstallationVerified' }}",
        })
        violations = dependency_gate_violations(jobs, "public-confirm")
        self.assertTrue(
            any("finalize" in v and "'release-checks'" in v for v in violations),
            f"expected finalize to require release-checks success: {violations}",
        )


if __name__ == "__main__":
    unittest.main()
