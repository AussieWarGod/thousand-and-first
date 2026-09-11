"""Executable cases for the session run record and the checker's use of the two records.

Synthetic journals and records only; no game, no process. What these prove is that the record
refuses rather than invents, and that turns and seconds reach the artefact as MEASUREMENTS
derived from the journals, never as a copy of the budget they are supposed to be checked against.
"""

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
REPO = TOOLS.parent


def load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, TOOLS / filename)
    module = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(TOOLS))
    try:
        spec.loader.exec_module(module)
    finally:
        sys.path.pop(0)
    return module


record = load("scenario_run_record", "scenario_run_record.py")
checker = load("quickstart_lifecycle", "check-quickstart-lifecycle.py")

SAVE_STAMP = "profile=taf-scenario.save seal=" + "d" * 64
LOAD_STAMP = "profile=taf-scenario.load seal=" + "e" * 64
STAMPS = [
    "2026-09-11T10:00:00.000Z",
    "2026-09-11T10:00:30.000Z",
    "2026-09-11T10:02:30.000Z",
    "2026-09-11T10:03:00.000Z",
    "2026-09-11T10:10:00.000Z",
    "2026-09-11T10:10:20.000Z",
]
IDS = "realmId=r1; cityId=c1"


def journal(rows) -> str:
    return "".join(
        "%s\t%s\tOK\t%s\n" % (stamp, verb, message) for stamp, verb, message in rows
    )


def save_journal() -> str:
    return journal(
        [
            (STAMPS[0], "realize", "founded"),
            (STAMPS[1], "lifecycle-open", "step=startup; " + IDS + "; turns=10; " + SAVE_STAMP),
            (STAMPS[2], "lifecycle-build",
             "step=paid-commission; " + IDS + "; jobId=job-1; turns=30; " + SAVE_STAMP),
            (
                STAMPS[3],
                "lifecycle-grown",
                "step=engine-turn-build; " + IDS + "; buildingId=b1; plotId=p1;"
                " completedReceiptId=job-1; forJobId=job-1; turns=90; " + SAVE_STAMP,
            ),
            (STAMPS[4], "lifecycle-save",
             "step=save; " + IDS + "; saveId=save-1; turns=90; " + SAVE_STAMP),
        ]
    )


def load_journal() -> str:
    return journal(
        [
            (
                STAMPS[5],
                "lifecycle-loaded",
                "step=cold-load; " + IDS + "; saveId=save-1; buildingId=b1; plotId=37,10-44,15;"
                " completedReceiptId=job-1; turns=90; "
                + LOAD_STAMP,
            ),
            (
                STAMPS[5],
                "lifecycle-next",
                "step=next-action; " + IDS + "; saveId=save-1; jobId=job-2; turns=90; " + LOAD_STAMP,
            ),
        ]
    )


class RunRecordSeal(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)

    def tearDown(self):
        self.temporary.cleanup()

    def make_receipt(self, root: Path, pid: int = 4242, ticks: str = "638000000000000000") -> None:
        (root / "process-ownership.json").write_text(
            json.dumps({
                "schema": "taf-scenario-process-v1", "root": str(root), "pid": pid,
                "startTicks": ticks, "executable": "CoQ.exe", "arguments": ["-savepath", str(root)],
            }),
            encoding="utf-8",
        )

    def make_seal(self, root: Path) -> None:
        seal = Path(str(root) + ".seal")
        seal.mkdir(parents=True, exist_ok=True)
        (seal / "profile.sha256").write_text(
            "taf-scenario-profile-seal-v1\n" + "a" * 64 + "  Local/scenario-script.txt\n",
            encoding="utf-8",
        )

    def seal(self, **extra):
        self.make_seal(self.root)
        arguments = [
            "seal", str(self.root), "--tree", str(REPO), "--role", "save-session",
            "--seed", "#43101", "--turn-budget", "3000", "--timeout-seconds", "1200",
        ]
        for key, value in extra.items():
            arguments += ["--" + key.replace("_", "-"), str(value)]
        return record.main(["scenario_run_record.py"] + arguments)

    def test_a_seal_measures_the_tree_rather_than_reading_the_review_ledger(self):
        self.assertEqual(self.seal(), 0)
        payload = json.loads((self.root / "run-record.json").read_text())
        head = subprocess.run(
            ["git", "-C", str(REPO), "rev-parse", "HEAD"],
            check=True, capture_output=True, text=True,
        ).stdout.strip()
        measured = json.loads(subprocess.run(
            [sys.executable, str(TOOLS / "check-structure.py"), "--json"],
            check=True, capture_output=True, text=True, cwd=str(REPO),
        ).stdout)["inventorySha256"]
        self.assertEqual(payload["candidateCommit"], head)
        self.assertEqual(payload["runtimeInventorySha256"], measured)
        # The review ledger is a review record and may lag the candidate deliberately; it must
        # never be the source of what was exercised.
        ledger = json.loads((REPO / "docs" / "STRUCTURE_REVIEW.json").read_text())
        if ledger.get("inventorySha256") != measured:
            self.assertNotEqual(payload["runtimeInventorySha256"], ledger["inventorySha256"])
        self.assertEqual(payload["turnBudget"], 3000)
        self.assertEqual(payload["role"], "save-session")
        self.assertNotIn("launchId", payload)

    def test_the_launched_profile_seal_is_recorded_separately_from_the_production_digest(self):
        self.seal()
        payload = json.loads((self.root / "run-record.json").read_text())
        seal = (Path(str(self.root) + ".seal") / "profile.sha256").read_bytes()
        import hashlib

        self.assertEqual(payload["profileSeal"], hashlib.sha256(seal).hexdigest())
        self.assertEqual(payload["profileName"], self.root.name)
        self.assertNotEqual(payload["profileSeal"], payload["runtimeInventorySha256"])
        # The dev-harness inventory is a third, separately named thing, and is recorded only
        # when the caller can state it rather than derived from either of the other two.
        self.assertNotIn("harnessInventorySha256", payload)

    def test_a_tree_without_the_structure_tool_refuses_rather_than_guessing(self):
        empty = Path(self.temporary.name) / "empty-tree"
        empty.mkdir()
        with self.assertRaises(SystemExit) as raised:
            self.seal(tree=str(empty))
        self.assertIn("check-structure.py", str(raised.exception))

    def test_a_profile_without_a_closed_seal_refuses(self):
        bare = Path(self.temporary.name) / "bare"
        bare.mkdir()
        with self.assertRaises(SystemExit) as raised:
            record.main([
                "scenario_run_record.py", "seal", str(bare), "--tree", str(REPO),
                "--role", "save-session", "--seed", "#1", "--turn-budget", "10",
                "--timeout-seconds", "60",
            ])
        self.assertIn("profile seal", str(raised.exception))

    def test_a_nonpositive_budget_refuses(self):
        self.make_seal(self.root)
        with self.assertRaises(SystemExit):
            record.main([
                "scenario_run_record.py", "seal", str(self.root), "--tree", str(REPO),
                "--role", "save-session", "--seed", "#1", "--turn-budget", "0",
                "--timeout-seconds", "60",
            ])

    def launch(self, launch_id: str = "pid-4242-20260911T100000Z"):
        self.make_receipt(self.root)
        return record.main([
            "scenario_run_record.py", "launch", str(self.root), "--launch-id", launch_id,
            "--started", "2026-09-11T10:00:00Z",
            "--ownership", str(self.root / "process-ownership.json"),
        ])

    def test_launch_and_stop_are_written_once_each(self):
        self.seal()
        self.launch()
        with self.assertRaises(SystemExit):
            self.launch("pid-4242-20260911T100001Z")
        record.main([
            "scenario_run_record.py", "stop", str(self.root),
            "--stopped", "2026-09-11T10:05:00Z", "--exit-code", "0", "--exit-observed",
        ])
        payload = json.loads((self.root / "run-record.json").read_text())
        self.assertEqual(payload["launchId"], "pid-4242-20260911T100000Z")
        self.assertEqual(payload["stoppedUtc"], "2026-09-11T10:05:00Z")
        self.assertEqual(payload["exitCode"], 0)
        self.assertEqual(payload["exitProvenance"], "owned-process-exit-observed")
        self.assertEqual(payload["ownership"]["pid"], 4242)
        self.assertEqual(payload["ownership"]["receiptRef"], "process-ownership.json")
        with self.assertRaises(SystemExit):
            record.main(["scenario_run_record.py", "stop", str(self.root)])

    def test_a_launch_whose_identity_is_not_the_owned_pid_refuses(self):
        self.seal()
        with self.assertRaises(SystemExit) as raised:
            self.launch("pid-9999-20260911T100000Z")
        self.assertIn("owned process's pid", str(raised.exception))

    def test_a_launch_without_an_ownership_receipt_refuses(self):
        self.seal()
        with self.assertRaises(SystemExit):
            record.main([
                "scenario_run_record.py", "launch", str(self.root), "--launch-id", "pid-4242-x",
                "--ownership", str(self.root / "absent.json"),
            ])

    def test_an_exit_code_without_an_observed_exit_refuses(self):
        self.seal()
        self.launch()
        with self.assertRaises(SystemExit) as raised:
            record.main([
                "scenario_run_record.py", "stop", str(self.root), "--exit-code", "0",
            ])
        self.assertIn("an exit they did not watch", str(raised.exception))

    def test_an_unwatched_stop_records_no_exit_code_at_all(self):
        self.seal()
        self.launch()
        record.main(["scenario_run_record.py", "stop", str(self.root)])
        payload = json.loads((self.root / "run-record.json").read_text())
        self.assertNotIn("exitCode", payload)
        self.assertEqual(payload["exitProvenance"], "owned-process-ended-exit-unobserved")

    def test_a_stop_without_an_ownership_block_refuses(self):
        self.seal()
        self.launch()
        payload = json.loads((self.root / "run-record.json").read_text())
        del payload["ownership"]
        (self.root / "run-record.json").write_text(json.dumps(payload), encoding="utf-8")
        with self.assertRaises(SystemExit) as raised:
            record.main(["scenario_run_record.py", "stop", str(self.root)])
        self.assertIn("no ownership block", str(raised.exception))

    def test_the_game_build_is_read_from_the_runs_own_log_or_left_out(self):
        self.seal()
        self.launch()
        (self.root / "Player.log").write_text("Qud version 2.0.211.51 build\n", encoding="utf-8")
        record.main(["scenario_run_record.py", "stop", str(self.root)])
        self.assertEqual(
            json.loads((self.root / "run-record.json").read_text())["gameBuildId"], "2.0.211.51"
        )


class TwoRecordEmission(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        # Each session owns a scenario root, and its journal sits beside its own run record --
        # the same shape a real pair of profiles has.
        self.save_root = self.root / "taf-scenario.save"
        self.load_root = self.root / "taf-scenario.load"
        self.save_root.mkdir()
        self.load_root.mkdir()
        (self.save_root / "scenario-journal.tsv").write_text(save_journal(), encoding="utf-8")
        (self.load_root / "scenario-journal.tsv").write_text(load_journal(), encoding="utf-8")
        self.journals = [
            self.save_root / "scenario-journal.tsv",
            self.load_root / "scenario-journal.tsv",
        ]

    def tearDown(self):
        self.temporary.cleanup()

    def records(self, **overrides):
        first = {
            "role": "save-session", "launchId": "L1", "started": "2026-09-11T10:00:00Z",
            "stoppedUtc": "2026-09-11T10:05:00Z", "seed": "#43101",
            "candidateCommit": "a" * 40, "runtimeInventorySha256": "b" * 64,
            "gameBuildId": "2.0.211.51", "logRef": "docs/release-evidence/longform.log",
            "logSha256": "c" * 64, "continuity": {"realmId": "r1", "cityId": "c1"},
            "turnBudget": 3000, "timeoutSeconds": 1200,
            "profileSeal": "d" * 64, "profileName": "taf-scenario.save",
            "root": str(self.save_root),
            "ownership": {
                "receiptRef": "process-ownership.json", "receiptSha256": "1" * 64,
                "pid": 4242, "startTicks": "638000000000000000", "executable": "CoQ.exe",
            },
            "exitProvenance": "owned-process-exit-observed", "exitCode": 0,
            "launchId": "pid-4242-20260911T100000Z",
        }
        second = dict(first)
        second.update({
            "role": "cold-load-session", "started": "2026-09-11T10:09:00Z",
            "launchId": "pid-5353-20260911T100900Z",
            "ownership": {
                "receiptRef": "process-ownership.json", "receiptSha256": "2" * 64,
                "pid": 5353, "startTicks": "638000000000009999", "executable": "CoQ.exe",
            },
            "exitProvenance": "owned-process-ended-exit-unobserved",
            "stoppedUtc": "2026-09-11T10:12:00Z", "turnBudget": 10, "timeoutSeconds": 600,
            "profileSeal": "e" * 64, "profileName": "taf-scenario.load",
            "root": str(self.load_root),
        })
        second.pop("exitCode", None)
        second.update(overrides.pop("load", {}))
        first.update(overrides.pop("save", {}))
        return [first, second]

    def emit(self, records):
        for root, payload in zip((self.save_root, self.load_root), records):
            (root / "run-record.json").write_text(json.dumps(payload), encoding="utf-8")
        report = checker.judge(checker.rows_of(self.journals))
        options = {
            "results": str(self.root / "results.json"),
            "run-record": str(self.save_root / "run-record.json") + ","
            + str(self.load_root / "run-record.json"),
        }
        problems = checker.emit(report, options, self.journals)
        return json.loads((self.root / "results.json").read_text()), problems, report

    def test_turns_and_seconds_are_derived_from_the_journals_not_the_budgets(self):
        payload, problems, report = self.emit(self.records())
        self.assertEqual(report["verdict"], checker.PASS)
        self.assertEqual(problems, [])
        steps = {entry["step"]: entry for entry in payload["steps"]}
        self.assertEqual(steps["startup"]["turnsUsed"], 10)
        # Quote and commission are one production call and share one row, so the turns between
        # the startup census and that row are attributed once, to the commission that spent them.
        self.assertEqual(steps["quote"]["turnsUsed"], 20)
        self.assertEqual(steps["paid-commission"]["turnsUsed"], 0)
        self.assertEqual(steps["engine-turn-build"]["turnsUsed"], 60)
        self.assertEqual(steps["quote"]["elapsedSeconds"], 120)
        self.assertEqual(steps["save"]["elapsedSeconds"], 420)
        self.assertEqual(steps["startup"]["turnBudget"], 3000)
        self.assertEqual(steps["cold-load"]["turnBudget"], 10)
        self.assertEqual(steps["engine-turn-build"]["observed"]["forJobId"], "job-1")
        self.assertEqual(steps["next-action"]["observed"]["jobId"], "job-2")

    def test_each_session_names_its_own_profile_seal(self):
        payload, problems, _ = self.emit(self.records())
        self.assertEqual(problems, [])
        seals = {entry["role"]: entry["profileSeal"] for entry in payload["processes"]}
        names = {entry["role"]: entry["profileName"] for entry in payload["processes"]}
        self.assertNotEqual(seals["save-session"], seals["cold-load-session"])
        self.assertEqual(names["cold-load-session"], "taf-scenario.load")
        # No single seal is hoisted to the top level, where it would claim both sessions.
        self.assertNotIn("profileSeal", payload)
        self.assertNotIn("profileSealSha256", payload)

    def test_a_session_without_its_own_seal_is_named_not_defaulted(self):
        records = self.records()
        del records[1]["profileSeal"]
        _, problems, _ = self.emit(records)
        self.assertIn("processes.cold-load-session.profileSeal", problems)

    def test_an_exit_code_without_observed_provenance_is_refused(self):
        _, problems, _ = self.emit(self.records(load={"exitCode": 0}))
        self.assertTrue(
            any("exitCode (recorded without an observed exit)" in problem for problem in problems),
            problems,
        )

    def test_a_session_without_an_ownership_block_is_refused(self):
        records = self.records()
        del records[0]["ownership"]
        _, problems, _ = self.emit(records)
        self.assertTrue(
            any("no owned-process receipt" in problem for problem in problems), problems
        )

    def test_a_launch_identity_that_is_not_the_owned_pid_is_refused(self):
        _, problems, _ = self.emit(self.records(
            load={"launchId": "pid-7777-20260911T100900Z"}))
        self.assertTrue(
            any("the ownership receipt does not" in problem for problem in problems), problems
        )

    def test_a_journal_without_its_own_run_record_is_refused(self):
        stray = self.root / "stray"
        stray.mkdir()
        (stray / "scenario-journal.tsv").write_text(save_journal(), encoding="utf-8")
        self.journals = [stray / "scenario-journal.tsv", self.journals[1]]
        _, problems, _ = self.emit(self.records())
        self.assertTrue(
            any("no run record for its own profile" in problem for problem in problems), problems
        )

    def test_every_step_is_attributed_to_the_profile_that_ran_it(self):
        _, problems, report = self.emit(self.records())
        self.assertEqual(problems, [])
        by_step = report["profilesByStep"]
        self.assertEqual(by_step["paid-commission"]["profileName"], "taf-scenario.save")
        self.assertEqual(by_step["cold-load"]["profileName"], "taf-scenario.load")
        self.assertNotEqual(
            by_step["save"]["profileSeal"], by_step["next-action"]["profileSeal"]
        )

    def test_a_row_stamped_with_another_profile_is_refused(self):
        text = (self.save_root / "scenario-journal.tsv").read_text(encoding="utf-8")
        (self.save_root / "scenario-journal.tsv").write_text(
            text.replace("profile=taf-scenario.save", "profile=taf-scenario.other", 1),
            encoding="utf-8",
        )
        _, problems, _ = self.emit(self.records())
        self.assertTrue(
            any("names profile taf-scenario.other" in problem for problem in problems), problems
        )

    def test_a_row_stamped_with_another_seal_is_refused(self):
        text = (self.load_root / "scenario-journal.tsv").read_text(encoding="utf-8")
        (self.load_root / "scenario-journal.tsv").write_text(
            text.replace("seal=" + "e" * 64, "seal=" + "f" * 64), encoding="utf-8"
        )
        _, problems, _ = self.emit(self.records())
        self.assertTrue(
            any("names a seal its session's record does not" in problem for problem in problems),
            problems,
        )

    def test_a_row_with_no_profile_stamp_is_refused(self):
        text = (self.save_root / "scenario-journal.tsv").read_text(encoding="utf-8")
        (self.save_root / "scenario-journal.tsv").write_text(
            text.replace("; " + SAVE_STAMP, "", 1), encoding="utf-8"
        )
        _, problems, _ = self.emit(self.records())
        self.assertTrue(
            any("carries no profile stamp" in problem for problem in problems), problems
        )

    def test_two_sessions_sharing_a_launch_id_are_refused(self):
        payload, problems, _ = self.emit(self.records(load={
            "launchId": "pid-4242-20260911T100000Z",
            "ownership": {
                "receiptRef": "process-ownership.json", "receiptSha256": "2" * 64,
                "pid": 4242, "startTicks": "638000000000009999", "executable": "CoQ.exe",
            },
        }))
        self.assertTrue(any("distinct launch ids" in problem for problem in problems))
        self.assertNotIn("processes", payload)

    def test_a_cold_load_that_began_before_the_save_stopped_is_refused(self):
        payload, problems, _ = self.emit(
            self.records(load={"started": "2026-09-11T10:01:00Z"})
        )
        self.assertTrue(any("began before" in problem for problem in problems))
        self.assertNotIn("processes", payload)

    def test_two_sessions_from_different_trees_are_refused(self):
        _, problems, _ = self.emit(self.records(load={"candidateCommit": "d" * 40}))
        self.assertTrue(
            any("different trees" in problem for problem in problems), problems
        )

    def test_a_missing_second_record_leaves_the_chain_blocked(self):
        (self.load_root / "scenario-journal.tsv").write_text("", encoding="utf-8")
        payload, problems, report = self.emit(self.records())
        self.assertEqual(report["verdict"], checker.BLOCKER)
        self.assertIn("steps.cold-load", problems)
        self.assertNotIn("cold-load", [entry["step"] for entry in payload["steps"]])

    def test_an_over_budget_run_reports_the_measurement_it_made(self):
        payload, _, _ = self.emit(self.records(save={"turnBudget": 5}))
        built = next(
            entry for entry in payload["steps"] if entry["step"] == "engine-turn-build"
        )
        # 60 turns against a budget of 5: emitted as measured, for the validator to refuse.
        self.assertEqual(built["turnsUsed"], 60)
        self.assertEqual(built["turnBudget"], 5)


class LauncherSourceContracts(unittest.TestCase):
    """SOURCE-ONLY pins on the PowerShell halves.

    These do not launch a process and prove no Windows behaviour: they only hold the launcher to
    writing the two halves it can honestly witness, and to refusing rather than overwriting. The
    executable proof of the launch and stop halves is a native run, which is owed.
    """

    def launcher(self) -> str:
        return (TOOLS / "run-scenario.ps1").read_text(encoding="utf-8")

    def test_the_launcher_writes_the_launch_half_from_the_launched_process(self):
        source = self.launcher()
        self.assertIn("$record['launchId'] = \"pid-$($process.Id)", source)
        self.assertIn("$record['started'] = $utcStart.ToString('yyyy-MM-ddTHH:mm:ssZ')", source)
        self.assertIn("if ($record.ContainsKey('launchId')) { throw", source)

    def test_the_launcher_writes_the_stop_half_only_after_the_process_ended(self):
        source = self.launcher()
        self.assertIn("if ($StopRecord) {", source)
        self.assertIn("A Qud process is still running; stop it before recording the stop.", source)
        self.assertIn("if ($record.ContainsKey('stoppedUtc')) { throw", source)
        self.assertIn("$record['stoppedUtc'] = [DateTime]::UtcNow", source)
        self.assertIn("Get-TafGameBuildId -LogPath (Join-Path $rootPath 'Player.log')", source)

    def test_the_launcher_binds_the_owned_process_rather_than_its_own_shell(self):
        source = self.launcher()
        # The launch half reads the launcher's own ownership receipt and refuses if it names a
        # different process than the one just started.
        self.assertIn("function Get-TafOwnershipBlock {", source)
        self.assertIn("'process-ownership.json'", source)
        self.assertIn("if ($ownership.pid -ne $process.Id) {", source)
        self.assertIn("$record['ownership'] = $ownership", source)
        # The stop half re-reads the receipt, refuses a changed one, and refuses while the owned
        # pid (with its own start ticks) is still alive -- a pid alone is not identity.
        self.assertIn("function Test-TafOwnedProcessEnded {", source)
        self.assertIn("$alive.StartTime.ToUniversalTime().Ticks.ToString() -ne $Ownership.startTicks",
                      source)
        self.assertIn("The ownership receipt changed since launch", source)
        self.assertIn("The owned process is still running; its exit cannot be recorded yet.", source)

    def test_the_launcher_writes_no_exit_code_nobody_watched(self):
        source = self.launcher()
        self.assertIn("[switch]$ExitObserved,", source)
        self.assertIn("[int]$ExitCode", source)
        self.assertNotIn("[int]$ExitCode = 0", source)
        self.assertIn("$record['exitProvenance'] = 'owned-process-exit-observed'", source)
        self.assertIn("$record['exitProvenance'] = 'owned-process-ended-exit-unobserved'", source)
        self.assertIn("$record.Remove('exitCode')", source)

    def test_the_launcher_never_rewrites_what_preparation_recorded(self):
        source = self.launcher()
        for field in ("runtimeInventorySha256", "candidateCommit", "profileSeal", "turnBudget"):
            self.assertNotIn("$record['" + field + "']", source)

    def test_preparation_seals_the_record_only_when_a_role_is_asked_for(self):
        source = (TOOLS / "prepare-scenario.sh").read_text(encoding="utf-8")
        self.assertIn('if [ -n "${TAF_SCENARIO_ROLE:-}" ]; then', source)
        self.assertIn("scenario_run_record.py\" seal", source)
        self.assertIn('--tree "$REPO" --role "$TAF_SCENARIO_ROLE" --seed "$SEED"', source)


if __name__ == "__main__":
    unittest.main()
