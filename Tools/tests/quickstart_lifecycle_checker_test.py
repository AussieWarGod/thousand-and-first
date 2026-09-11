"""Executable regression for the construction-lifecycle verdict.

These cases are ABOUT THE ORACLE, not about the game: they feed synthetic journals to
Tools/check-quickstart-lifecycle.py and prove the three verdicts it may reach. The point of
the tool is that a chain which was never driven to the end cannot read as success, so the
BLOCKER cases here are the load-bearing ones.
"""

import importlib.util
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "quickstart_lifecycle", TOOLS / "check-quickstart-lifecycle.py"
)
checker = importlib.util.module_from_spec(_SPEC)
sys.path.insert(0, str(TOOLS))
try:
    _SPEC.loader.exec_module(checker)
finally:
    sys.path.pop(0)


def rows(*names, refused=()):
    return [(name, "REFUSED" if name in refused else "OK", "message") for name in names]


def whole_chain(**kwargs):
    names = []
    for _, wanted in checker.LINKS:
        names.extend(wanted)
    return rows(*names, **kwargs)


class LifecycleVerdict(unittest.TestCase):
    def test_the_whole_chain_passes(self):
        report = checker.judge(whole_chain())
        self.assertEqual(report["verdict"], checker.PASS)
        self.assertIsNone(report["reason"])
        self.assertEqual([link["state"] for link in report["links"]], [checker.PASS] * 7)

    def test_a_link_that_was_never_driven_blocks_rather_than_passes(self):
        for link, wanted in checker.LINKS[1:]:
            with self.subTest(link=link):
                kept = [
                    name
                    for _, names in checker.LINKS
                    for name in names
                    if name not in wanted
                ]
                report = checker.judge(rows(*kept))
                self.assertEqual(report["verdict"], checker.BLOCKER)
                self.assertIn(link, report["reason"])
                states = {entry["link"]: entry["state"] for entry in report["links"]}
                self.assertEqual(states[link], checker.BLOCKER)

    def test_the_missing_turn_and_post_load_links_are_reported_by_name(self):
        driven = list(checker.BOOT_ROWS) + list(checker.BUILD_ROWS)
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.BLOCKER)
        blocked = [
            entry["link"] for entry in report["links"] if entry["state"] == checker.BLOCKER
        ]
        self.assertEqual(
            blocked,
            ["engine-turn-build", "save", "cold-load", "next-action"],
        )

    def test_a_refused_row_fails_and_outranks_a_later_blocker(self):
        driven = list(checker.BOOT_ROWS) + list(checker.BUILD_ROWS)
        report = checker.judge(rows(*driven, refused=("QUICKSTART-BUILD-COMMISSION",)))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("paid-commission", report["reason"])
        self.assertIn("QUICKSTART-BUILD-COMMISSION", report["reason"])

    def test_partial_evidence_inside_a_link_fails_rather_than_blocks(self):
        driven = list(checker.BOOT_ROWS) + list(checker.BUILD_ROWS[:-1])
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("QUICKSTART-BUILD-COMPLETE", report["reason"])

    def test_rows_out_of_order_fail(self):
        driven = list(checker.BOOT_ROWS) + [
            checker.BUILD_ROWS[1],
            checker.BUILD_ROWS[0],
            *checker.BUILD_ROWS[2:],
        ]
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("out of order", report["reason"])

    def test_a_later_link_may_not_interleave_an_earlier_one(self):
        driven = (
            list(checker.BOOT_ROWS)
            + [checker.GROW_ROWS[0]]
            + list(checker.BUILD_ROWS)
            + list(checker.GROW_ROWS[1:])
        )
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.FAIL)

    def test_exit_codes_separate_the_three_verdicts(self):
        self.assertEqual(
            [checker.EXITS[name] for name in (checker.PASS, checker.BLOCKER, checker.FAIL)],
            [0, 3, 4],
        )

    def test_the_step_names_and_order_are_the_agreed_seven(self):
        self.assertEqual(
            checker.STEPS,
            (
                "startup",
                "quote",
                "paid-commission",
                "engine-turn-build",
                "save",
                "cold-load",
                "next-action",
            ),
        )
        self.assertEqual(len(set(checker.STEPS)), 7)
        self.assertEqual(
            sorted(set(checker.SESSION_OF.values())), sorted(checker.SESSIONS)
        )

    def run_record(self, **extra):
        record = {
            "driver": "automated lifecycle driver",
            "runId": "run-1",
            "seed": 43101,
            "candidateCommit": "a" * 40,
            "runtimeInventorySha256": "b" * 64,
            "gameBuildId": "2.0.211.51",
            "logRef": "docs/release-evidence/longform.log",
            "logSha256": "c" * 64,
            "continuity": {
                "realmId": "realm-1",
                "cityId": "city-1",
                "jobId": "job-1",
                "buildingId": "building-1",
                "plotId": "plot-1",
                "saveId": "save-1",
            },
            "processes": [
                {"role": "save-session", "launchId": "L1", "started": "t0", "stopped": True},
                {"role": "cold-load-session", "launchId": "L2", "started": "t1", "stopped": True},
            ],
            "phases": {
                step: {
                    "turnsUsed": 10,
                    "elapsedSeconds": 5,
                    "turnBudget": 100,
                    "timeoutSeconds": 60,
                    "observedIdentities": {
                        name: name + "-observed-at-" + step
                        for name in checker.EXPECTED_IDENTITIES[step]
                    },
                }
                for step in checker.STEPS
            },
        }
        record.update(extra)
        return record

    def test_a_complete_run_emits_the_validators_exact_key_set(self):
        payload, unresolved = checker.results(checker.judge(whole_chain()), self.run_record())
        self.assertEqual(unresolved, [])
        self.assertEqual(
            sorted(payload),
            sorted(
                [
                    "schemaVersion",
                    "driver",
                    "runId",
                    "seed",
                    "candidateCommit",
                    "runtimeInventorySha256",
                    "gameBuildId",
                    "logRef",
                    "logSha256",
                    "continuity",
                    "processes",
                    "steps",
                ]
            ),
        )
        self.assertEqual([entry["step"] for entry in payload["steps"]], list(checker.STEPS))
        self.assertEqual(
            [session["role"] for session in payload["processes"]],
            ["save-session", "cold-load-session"],
        )
        self.assertEqual(
            sorted(payload["steps"][0]),
            sorted(["step", "status", "turnsUsed", "elapsedSeconds", "turnBudget",
                    "timeoutSeconds", "observedIdentities"]),
        )
        self.assertEqual(payload["schemaVersion"], 1)

    def test_measured_turns_are_emitted_as_measured_even_over_budget(self):
        record = self.run_record()
        record["phases"]["quote"]["turnsUsed"] = 7500
        payload, _ = checker.results(checker.judge(whole_chain()), record)
        quote = next(entry for entry in payload["steps"] if entry["step"] == "quote")
        self.assertEqual(quote["turnsUsed"], 7500)
        self.assertEqual(quote["turnBudget"], 100)

    def test_an_unmeasured_field_is_omitted_and_listed_rather_than_invented(self):
        record = self.run_record()
        del record["candidateCommit"]
        del record["phases"]["save"]["turnsUsed"]
        record["processes"] = [record["processes"][0]]
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        self.assertNotIn("candidateCommit", payload)
        self.assertNotIn("processes", payload)
        self.assertIn("candidateCommit", unresolved)
        self.assertIn("processes.cold-load-session", unresolved)
        self.assertIn("save.turnsUsed", unresolved)
        save = next(entry for entry in payload["steps"] if entry["step"] == "save")
        self.assertNotIn("turnsUsed", save)

    def test_an_undriven_step_is_omitted_from_the_artefact_and_listed(self):
        driven = list(checker.BOOT_ROWS) + list(checker.BUILD_ROWS)
        payload, unresolved = checker.results(checker.judge(rows(*driven)), self.run_record())
        self.assertEqual(
            [entry["step"] for entry in payload["steps"]],
            ["startup", "quote", "paid-commission"],
        )
        for step in ("engine-turn-build", "save", "cold-load", "next-action"):
            self.assertIn("steps." + step, unresolved)

    def test_each_step_carries_only_the_identities_it_could_have_observed(self):
        payload, unresolved = checker.results(checker.judge(whole_chain()), self.run_record())
        self.assertEqual(unresolved, [])
        by_step = {entry["step"]: entry["observedIdentities"] for entry in payload["steps"]}
        self.assertEqual(sorted(by_step["startup"]), ["cityId", "realmId"])
        self.assertNotIn("jobId", by_step["quote"])
        self.assertIn("jobId", by_step["paid-commission"])
        self.assertNotIn("buildingId", by_step["paid-commission"])
        self.assertIn("buildingId", by_step["engine-turn-build"])
        self.assertIn("saveId", by_step["save"])
        # Read at that step, not copied forward: each value still names its own step.
        self.assertEqual(by_step["save"]["jobId"], "jobId-observed-at-save")
        # The finished job need not survive the load, and the next action is free to be a new
        # one, so neither step is asked to repeat the completed job's identity.
        self.assertNotIn("jobId", checker.EXPECTED_IDENTITIES["cold-load"])
        self.assertNotIn("jobId", checker.EXPECTED_IDENTITIES["next-action"])
        self.assertIn("saveId", checker.EXPECTED_IDENTITIES["next-action"])
        self.assertIn("buildingId", checker.EXPECTED_IDENTITIES["cold-load"])

    def test_a_step_that_observed_no_identities_is_listed_not_filled_in(self):
        record = self.run_record()
        del record["phases"]["engine-turn-build"]["observedIdentities"]["buildingId"]
        record["phases"]["save"]["observedIdentities"] = {}
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        self.assertIn("engine-turn-build.observedIdentities.buildingId", unresolved)
        self.assertIn("save.observedIdentities", unresolved)
        save = next(entry for entry in payload["steps"] if entry["step"] == "save")
        self.assertNotIn("observedIdentities", save)
        built = next(entry for entry in payload["steps"] if entry["step"] == "engine-turn-build")
        self.assertNotIn("buildingId", built["observedIdentities"])

    def test_the_artefact_never_carries_its_own_excuses(self):
        driven = list(checker.BOOT_ROWS)
        payload, _ = checker.results(checker.judge(rows(*driven)), self.run_record())
        for forbidden in ("blockedSteps", "unresolvedFields", "verdict", "reason"):
            self.assertNotIn(forbidden, payload)

    def test_results_never_emit_a_refused_step_as_pass(self):
        driven = list(checker.BOOT_ROWS) + list(checker.BUILD_ROWS)
        report = checker.judge(rows(*driven, refused=("QUICKSTART-BUILD-COMMISSION",)))
        payload, unresolved = checker.results(report, self.run_record())
        self.assertNotIn("paid-commission", [entry["step"] for entry in payload["steps"]])
        self.assertIn("steps.paid-commission", unresolved)

    def test_the_founded_lifecycle_profile_is_judged_on_its_own_verb_rows(self):
        driven = ["realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save"]
        report = checker.judge(rows(*driven))
        states = {entry["link"]: entry["state"] for entry in report["links"]}
        self.assertEqual(states["startup"], checker.PASS)
        self.assertEqual(states["paid-commission"], checker.PASS)
        self.assertEqual(states["engine-turn-build"], checker.PASS)
        self.assertEqual(states["save"], checker.PASS)
        self.assertEqual(states["cold-load"], checker.BLOCKER)
        self.assertEqual(states["next-action"], checker.BLOCKER)
        self.assertEqual(report["verdict"], checker.BLOCKER)

    def test_the_full_two_session_lifecycle_chain_reaches_pass(self):
        driven = [
            "realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save",
            "lifecycle-loaded", "lifecycle-next",
        ]
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.PASS)
        self.assertEqual([entry["state"] for entry in report["links"]], [checker.PASS] * 7)
        payload, unresolved = checker.results(report, self.run_record())
        self.assertEqual(unresolved, [])
        self.assertEqual([entry["step"] for entry in payload["steps"]], list(checker.STEPS))

    def test_a_refused_cold_load_row_fails_the_chain(self):
        driven = [
            "realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save",
            "lifecycle-loaded",
        ]
        report = checker.judge(rows(*driven, refused=("lifecycle-loaded",)))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("cold-load", report["reason"])

    def test_a_refused_next_action_fails_rather_than_blocking(self):
        driven = [
            "realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save",
            "lifecycle-loaded", "lifecycle-next",
        ]
        report = checker.judge(rows(*driven, refused=("lifecycle-next",)))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("next-action", report["reason"])

    def test_a_second_session_that_never_ran_still_blocks(self):
        driven = ["realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown", "lifecycle-save"]
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.BLOCKER)
        blocked = [entry["link"] for entry in report["links"] if entry["state"] == checker.BLOCKER]
        self.assertEqual(blocked, ["cold-load", "next-action"])

    def test_a_refused_lifecycle_verb_row_fails_that_link(self):
        driven = ["realize", "lifecycle-open", "lifecycle-build"]
        report = checker.judge(rows(*driven, refused=("lifecycle-build",)))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("lifecycle-build", report["reason"])

    def test_an_empty_journal_blocks_at_the_first_link(self):
        report = checker.judge([])
        self.assertEqual(report["verdict"], checker.BLOCKER)
        self.assertIn("startup", report["reason"])


if __name__ == "__main__":
    unittest.main()
