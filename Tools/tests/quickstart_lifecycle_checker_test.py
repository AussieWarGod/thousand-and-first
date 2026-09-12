"""Executable regression for the construction-lifecycle verdict.

These cases are ABOUT THE ORACLE, not about the game: they feed synthetic journals to
Tools/check-quickstart-lifecycle.py and prove the three verdicts it may reach. The point of
the tool is that a chain which was never driven to the end cannot read as success, so the
BLOCKER cases here are the load-bearing ones.
"""

import importlib.util
import sys
import tempfile
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


class FounderDeath(unittest.TestCase):
    """A founder who died mid-run is its own FAIL class, never a blocker and never a pass."""

    DIED = (
        "DIED bitten to death; reason=You were bitten to death by a snapjaw.;"
        " founderCell=40,12; zone=JoppaWorld.8.22.1.1.10; guard=armed-at-death;"
        " ignoreMe=True; walk=none; scope=quickstart-lifecycle"
    )

    def mid_advance(self, message):
        """The Quickstart road exactly as native run 36 left it: boot and build rows landed,
        lifecycle-open landed, the advance armed, then the run stopped."""
        driven = rows(*checker.BOOT_ROWS, *checker.BUILD_ROWS, "lifecycle-open", "advance")
        driven.append(("advance-guard", "OK", "start; founderCell=40,12; guard=ignoreme-armed"))
        driven.append(("SCRIPT-STOPPED", "REFUSED", message))
        return driven

    def test_a_died_row_is_the_founder_died_fail_class(self):
        report = checker.judge(self.mid_advance(self.DIED))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertEqual(report["failClass"], checker.FOUNDER_DIED)
        self.assertEqual(report["founderDeath"], self.DIED)
        self.assertTrue(report["reason"].startswith("founder-died: DIED bitten to death"))
        self.assertEqual(checker.EXITS[report["verdict"]], 4)

    def test_the_death_outranks_the_blocker_the_unreached_links_would_read(self):
        report = checker.judge(self.mid_advance(self.DIED))
        states = {entry["link"]: entry["state"] for entry in report["links"]}
        self.assertEqual(states["engine-turn-build"], checker.BLOCKER)
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn(checker.FOUNDER_DIED, report["reason"])

    def test_the_death_outranks_a_chain_fail_too(self):
        # Kills the `if death is not None and verdict != FAIL` mutant: a refused lifecycle-grown
        # row followed by the death must still be attributed to the death, the root cause.
        driven = whole_chain(refused=("QUICKSTART-BUILD-CANPAY",))
        self.assertEqual(checker.judge(driven)["failClass"], checker.CHAIN_FAIL)  # a real chain FAIL
        driven.append(("SCRIPT-STOPPED", "REFUSED", self.DIED))
        report = checker.judge(driven)
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertEqual(report["failClass"], checker.FOUNDER_DIED)
        self.assertTrue(report["reason"].startswith("founder-died: "))
        self.assertNotIn("refused row(s)", report["reason"])

    def test_an_ordinary_stopped_row_is_not_a_founder_death(self):
        stopped = "refused at verb 5 of 7: lifecycle-grown"
        report = checker.judge(self.mid_advance(stopped))
        self.assertIsNone(report["founderDeath"])
        self.assertNotEqual(report["failClass"], checker.FOUNDER_DIED)
        self.assertEqual(report["verdict"], checker.BLOCKER)
        self.assertIn("engine-turn-build", report["reason"])

    def test_the_died_prefix_is_read_only_off_the_stopped_row(self):
        driven = rows("lifecycle-open")
        driven.append(("lifecycle-grown", "REFUSED", "DIED is just a word in a stall reading"))
        self.assertIsNone(checker.founder_death(driven))

    def test_a_chain_fail_carries_the_chain_class_and_a_pass_carries_none(self):
        failed = checker.judge(whole_chain(refused=("QUICKSTART-BUILD-CANPAY",)))
        self.assertEqual(failed["failClass"], checker.CHAIN_FAIL)
        passed = checker.judge(whole_chain())
        self.assertIsNone(passed["failClass"])
        self.assertIsNone(passed["founderDeath"])


class StoreIdentity(unittest.TestCase):
    """Every step must pay from the one store lifecycle-open bound (native run 38)."""

    def chain(self, *ids):
        driven = whole_chain()
        for index, store in enumerate(ids):
            driven.append(("lifecycle-step-%d" % index, "OK", "step; storeId=%s; stores=2" % store))
        return driven

    def test_one_store_named_throughout_is_reported_and_passes(self):
        report = checker.judge(self.chain("495", "495", "495"))
        self.assertEqual(report["verdict"], checker.PASS)
        self.assertEqual(report["storeIds"], ["495"])

    def test_two_stores_across_steps_is_the_store_drift_fail_class(self):
        report = checker.judge(self.chain("495", "1203"))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertEqual(report["failClass"], checker.STORE_DRIFT)
        self.assertIn("495,1203", report["reason"])
        self.assertEqual(checker.EXITS[report["verdict"]], 4)

    def test_a_refused_row_naming_another_store_does_not_count(self):
        driven = self.chain("495")
        driven.append(("lifecycle-save", "REFUSED", "refused; storeId=1203; stores=2"))
        report = checker.judge(driven)
        self.assertEqual(report["storeIds"], ["495"])
        self.assertNotEqual(report["failClass"], checker.STORE_DRIFT)

    def test_the_store_field_is_read_exactly(self):
        self.assertEqual(checker.store_in("x; storeId=495; stores=2"), "495")
        self.assertIsNone(checker.store_in("x; stockpile=495; stores=2"))


class StallClassification(unittest.TestCase):
    """The four unfinished-job cases, surfaced verbatim and never softened into a pass."""

    def journal(self, message):
        return [("lifecycle-grown", "REFUSED", message)]

    def test_each_classification_is_read_back_verbatim(self):
        for name in checker.STALL_CLASSES:
            with self.subTest(stall=name):
                message = (
                    "native-lifecycle refused at lifecycle-grown: the job has not completed;"
                    " turns=2400; stall=" + name + "; phase=Working; startedTick=1200;"
                    " remainingTicks=2250; lastWorkedTick=1200; lastSemanticTick=3600"
                )
                self.assertEqual(checker.stall_in(message), name)

    def test_a_stalled_row_fails_its_link_and_never_passes(self):
        driven = [
            "realize", "lifecycle-open", "lifecycle-build", "lifecycle-grown",
        ]
        report = checker.judge(rows(*driven, refused=("lifecycle-grown",)))
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("engine-turn-build", report["reason"])

    def test_an_unknown_classification_is_marked_rather_than_accepted(self):
        self.assertIsNone(checker.stall_in("no stall here"))
        self.assertEqual(checker.stall_in("stall=made-up-word"), "made-up-word")
        self.assertNotIn("made-up-word", checker.STALL_CLASSES)


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
                    "observed": {
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
                    "timeoutSeconds", "observed"]),
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
        by_step = {entry["step"]: entry["observed"] for entry in payload["steps"]}
        self.assertEqual(sorted(by_step["startup"]), ["cityId", "realmId"])
        self.assertNotIn("jobId", by_step["quote"])
        self.assertIn("jobId", by_step["paid-commission"])
        self.assertNotIn("buildingId", by_step["paid-commission"])
        self.assertIn("buildingId", by_step["engine-turn-build"])
        self.assertIn("saveId", by_step["save"])
        # Read at that step, not copied forward: each value still names its own step.
        self.assertEqual(by_step["save"]["saveId"], "saveId-observed-at-save")
        # The finished job need not survive the load, and the next action is free to be a new
        # one, so neither step is asked to repeat the completed job's identity.
        self.assertNotIn("jobId", checker.EXPECTED_IDENTITIES["cold-load"])
        self.assertNotIn("jobId", checker.EXPECTED_IDENTITIES["next-action"])
        self.assertIn("saveId", checker.EXPECTED_IDENTITIES["next-action"])
        self.assertIn("buildingId", checker.EXPECTED_IDENTITIES["cold-load"])

    def test_an_identity_offered_before_it_exists_is_dropped_and_named(self):
        record = self.run_record()
        record["phases"]["startup"]["observed"]["jobId"] = "job-from-the-future"
        record["phases"]["quote"]["observed"]["saveId"] = "save-from-the-future"
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        startup = next(entry for entry in payload["steps"] if entry["step"] == "startup")
        quote = next(entry for entry in payload["steps"] if entry["step"] == "quote")
        self.assertNotIn("jobId", startup["observed"])
        self.assertNotIn("saveId", quote["observed"])
        self.assertIn("startup.observed.jobId (before it exists)", unresolved)
        self.assertIn("quote.observed.saveId (before it exists)", unresolved)

    def test_a_step_missing_a_required_identity_leaves_the_chain_incomplete(self):
        record = self.run_record()
        del record["phases"]["engine-turn-build"]["observed"]["buildingId"]
        record["phases"]["save"]["observed"] = {}
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        self.assertIn("engine-turn-build.observed.buildingId", unresolved)
        self.assertIn("save.observed", unresolved)
        emitted = [entry["step"] for entry in payload["steps"]]
        # Neither step is emitted with a hole in it, and no id is invented to fill one.
        self.assertNotIn("engine-turn-build", emitted)
        self.assertNotIn("save", emitted)

    def test_the_completed_work_must_link_back_to_the_paid_job(self):
        self.assertIn("completedReceiptId", checker.EXPECTED_IDENTITIES["engine-turn-build"])
        self.assertIn("forJobId", checker.EXPECTED_IDENTITIES["engine-turn-build"])
        record = self.run_record()
        del record["phases"]["engine-turn-build"]["observed"]["forJobId"]
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        self.assertIn("engine-turn-build.observed.forJobId", unresolved)
        self.assertNotIn(
            "engine-turn-build", [entry["step"] for entry in payload["steps"]]
        )

    def test_paid_commission_and_cold_load_identities_are_not_optional(self):
        for step, identity in (
            ("paid-commission", "jobId"),
            ("cold-load", "saveId"),
            ("cold-load", "buildingId"),
            ("cold-load", "plotId"),
        ):
            with self.subTest(step=step, identity=identity):
                self.assertIn(identity, checker.EXPECTED_IDENTITIES[step])
                record = self.run_record()
                del record["phases"][step]["observed"][identity]
                payload, unresolved = checker.results(checker.judge(whole_chain()), record)
                self.assertIn(step + ".observed." + identity, unresolved)
                self.assertNotIn(step, [entry["step"] for entry in payload["steps"]])

    def test_two_sessions_sharing_a_launch_id_are_refused_as_one_session(self):
        record = self.run_record()
        record["processes"][1]["launchId"] = record["processes"][0]["launchId"]
        payload, unresolved = checker.results(checker.judge(whole_chain()), record)
        self.assertNotIn("processes", payload)
        self.assertIn("processes (sessions must have distinct launch ids)", unresolved)

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

    def test_the_quickstart_lifecycle_profile_chain_reaches_pass(self):
        # Boot and build rows from the Quickstart phases, then the AutoRunner verbs.
        driven = (
            list(checker.BOOT_ROWS)
            + list(checker.BUILD_ROWS)
            + ["lifecycle-grown", "lifecycle-save", "lifecycle-loaded", "lifecycle-next"]
        )
        report = checker.judge(rows(*driven))
        self.assertEqual(report["verdict"], checker.PASS)
        payload, unresolved = checker.results(report, self.run_record())
        self.assertEqual(unresolved, [])
        self.assertEqual([entry["step"] for entry in payload["steps"]], list(checker.STEPS))

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


class StampedRefusalsBindRatherThanReportUnbindable(unittest.TestCase):
    """A refusal row that carries its stamp binds to its session exactly like a stamped success;
    only a row missing the stamp entirely is unbindable. This is the producer/consumer fix for
    Harness/KingdomQuickstartLifecycleSteps.Refuse(...) and the hand-built refusal rows in
    Harness/KingdomQuickstartLifecycleLoad.cs: before it, an honest refusal was misreported as
    an unbindable row rather than attributed to its session."""

    SAVE_SESSION = "founding-first-city"
    SAVE_SEAL = "a" * 64
    LOAD_SESSION = "founding-first-city-load"
    LOAD_SEAL = "b" * 64

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-lifecycle-stamps-test.")
        self.addCleanup(self.temp.cleanup)
        self.journal_path = Path(self.temp.name) / "journal.tsv"

    def write(self, *rows):
        lines = ["2026-09-11T00:00:00.000000Z\t" + verb + "\t" + outcome + "\t" + message
                 for verb, outcome, message in rows]
        self.journal_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return [self.journal_path]

    def records(self):
        return [
            {"role": "save-session", "profileName": self.SAVE_SESSION, "profileSeal": self.SAVE_SEAL},
            {"role": "cold-load-session", "profileName": self.LOAD_SESSION, "profileSeal": self.LOAD_SEAL},
        ]

    def stamped_stall_refusal(self):
        """What Harness/KingdomQuickstartLifecycleSteps.Refuse(...) now writes for a stalled
        engine-turn-build: the stall reason, then the profile stamp Stamped(...) appends."""
        return (
            "lifecycle-grown", "REFUSED",
            "native-lifecycle refused at engine-turn-build: the job has not completed; turns=2400;"
            " stall=insufficient-turns; phase=Working; startedTick=1200; remainingTicks=2250;"
            " lastWorkedTick=1200; lastSemanticTick=3600"
            "; profile=" + self.SAVE_SESSION + " seal=" + self.SAVE_SEAL,
        )

    def stamped_load_refusal(self):
        """What Harness/KingdomQuickstartLifecycleLoad.cs now writes via the shared
        Refuse(...) helper for a failed cold-load proof."""
        return (
            "lifecycle-loaded", "REFUSED",
            "native-lifecycle refused at cold-load: the loaded realm identity differs from the"
            " saved one; profile=" + self.LOAD_SESSION + " seal=" + self.LOAD_SEAL,
        )

    def test_a_stamped_stall_refusal_binds_rather_than_reporting_unbindable(self):
        paths = self.write(self.stamped_stall_refusal())
        problems = checker.check_stamps(paths, self.records())
        self.assertEqual(problems, [])

    def test_a_stamped_load_refusal_binds_rather_than_reporting_unbindable(self):
        paths = self.write(self.stamped_load_refusal())
        problems = checker.check_stamps(paths, self.records())
        self.assertEqual(problems, [])

    def test_a_row_missing_its_stamp_is_still_reported_as_unbindable(self):
        """Mutation evidence: strip the stamp the fix adds, and the checker must still catch it
        rather than passing an unstamped row silently."""
        verb, outcome, message = self.stamped_stall_refusal()
        unstamped = message.split("; profile=")[0]
        paths = self.write((verb, outcome, unstamped))
        problems = checker.check_stamps(paths, self.records())
        self.assertEqual(problems, ["row lifecycle-grown carries no profile stamp"])


class RepeatedLifecycleOpenWaitAttemptsAreJudgedHonestly(unittest.TestCase):
    """ZAP-034: lifecycle-open now retries under a bounded wait for the founder to have
    dedicated a stockpile (Harness/KingdomQuickstartLifecycleOpenWait), journaling each attempt
    as its own lifecycle-open row. The checker introduces no new row kind for this -- these
    cases prove the EXISTING judge() logic already reads a repeated verb name honestly: a
    string of OK "still waiting" rows never reads as complete on its own, and the final row's
    own outcome (not the first attempt's) decides the link."""

    def waiting_rows(self, count, final_outcome, final_message):
        rows = [("realize", "OK", "m")]
        for i in range(count):
            rows.append(("lifecycle-open", "OK", "step=open-wait; chunk=%d of %d" % (i + 1, count)))
        rows.append(("lifecycle-open", final_outcome, final_message))
        return rows

    def test_a_budget_exhausted_after_several_waits_fails_rather_than_blocks_or_passes(self):
        rows = self.waiting_rows(
            5, "REFUSED",
            "no dedicated stockpile appeared within 500 ordinary engine turns of waiting;"
            " last reading: this settlement has no dedicated stockpile to pay from",
        )
        report = checker.judge(rows)
        self.assertEqual(report["verdict"], checker.FAIL)
        self.assertIn("startup", report["reason"])
        self.assertIn("refused row(s): lifecycle-open", report["reason"])

    def test_dedication_arriving_mid_wait_still_passes_the_startup_link(self):
        # The predicate becoming true on, say, the third attempt: two "still waiting" rows, then
        # the real startup census row -- exactly what a fixed persona (no more waits needed once
        # dedicated) would journal.
        rows = self.waiting_rows(2, "OK", "step=startup; realmId=r1; cityId=c1; turns=200")
        report = checker.judge(rows)
        self.assertEqual(report["verdict"], checker.BLOCKER)
        startup = next(link for link in report["links"] if link["link"] == "startup")
        self.assertEqual(startup["state"], checker.PASS)

    def test_a_lone_waiting_row_with_no_conclusion_yet_blocks_the_later_links(self):
        # An interrupted run (game stopped mid-wait): only "still waiting" rows exist, no final
        # outcome. startup itself still reads PASS (every row is OK, in order), but nothing
        # downstream was ever driven -- exactly the same shape as today's single-row startup.
        rows = self.waiting_rows(3, "OK", "step=open-wait; chunk=4 of 5")
        report = checker.judge(rows[:-1] + [rows[-1]])
        startup = next(link for link in report["links"] if link["link"] == "startup")
        self.assertEqual(startup["state"], checker.PASS)
        self.assertEqual(report["verdict"], checker.BLOCKER)



class LifecycleGrownDetailRowIsTolerated(unittest.TestCase):
    """ZAP-034 native run 17: lifecycle-grown-detail carries the untruncated stall reading
    behind a Bounded(...) 300-char lifecycle-grown refusal (Harness/
    KingdomQuickstartLifecycleStall.cs). It must never affect judge()'s verdict or trip
    check_stamps() -- it names no link and carries no stamp by design."""

    def test_the_detail_row_does_not_change_the_engine_turn_build_verdict(self):
        rows = [
            ("realize", "OK", "m"),
            ("lifecycle-open", "OK", "step=startup"),
            ("lifecycle-grown-detail", "OK", "selectedId=x; candidates=1; roots=3; free=0"),
            ("lifecycle-grown", "REFUSED", "stall=no-labour-ever; turns=2400"),
        ]
        with_detail = checker.judge(rows)
        without_detail = checker.judge([row for row in rows if row[0] != "lifecycle-grown-detail"])
        self.assertEqual(with_detail["verdict"], without_detail["verdict"])
        self.assertEqual(with_detail["reason"], without_detail["reason"])
        self.assertEqual(with_detail["verdict"], checker.FAIL)

    def test_the_detail_row_is_never_demanded_a_stamp(self):
        rows = [
            ("lifecycle-open", "OK",
             "step=startup; profile=founding-first-city seal=" + "a" * 64),
            ("lifecycle-grown-detail", "OK", "selectedId=x; candidates=1"),
        ]
        paths = [self._journal(rows)]
        records = [{"role": "save-session", "profileName": "founding-first-city",
                    "profileSeal": "a" * 64}]
        self.assertEqual(checker.check_stamps(paths, records), [])

    def _journal(self, rows):
        import tempfile
        from pathlib import Path
        temp = tempfile.TemporaryDirectory(prefix="taf-lifecycle-detail-row-test.")
        self.addCleanup(temp.cleanup)
        path = Path(temp.name) / "journal.tsv"
        lines = ["2026-09-11T00:00:00.000000Z\t" + verb + "\t" + outcome + "\t" + message
                 for verb, outcome, message in rows]
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return path




if __name__ == "__main__":
    unittest.main()
