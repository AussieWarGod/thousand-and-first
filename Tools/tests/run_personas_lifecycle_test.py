"""Execute the real persona shell lifecycle with harmless external-command fixtures.

The runner, persona parser, and log checker are copied unchanged. Windows process ownership is
not simulated as proof: the PowerShell boundary merely returns chosen success/refusal outcomes.
All files and subprocesses belong to one TemporaryDirectory; no game or Windows process runs.
"""

from __future__ import annotations

import csv
import json
import os
import pathlib
import shutil
import signal
import subprocess
import tempfile
import time
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
JOURNAL = (
    "2026-09-05T00:00:00.000Z\tstatus\tOK\tfixture-observed\n"
    "2026-09-05T00:00:01.000Z\tSCRIPT-COMPLETE\tOK\tdone\n"
)
PLAYER_LOG = "[TAF] fixture loaded cleanly\n"
PERSONA = "REQUEST=lifecycle-{name}\nSCRIPT=status\nEXPECT=status:OK~fixture-observed,COMPLETE\n"
EXPECTED_MOD_ERROR = (
    "MODERROR [The Thousand and First] - ThousandAndFirst: subsidence: departure summary failed "
    "(native subsidence summary interruption)"
)
EXPECTED_TAF_ERROR = (
    "[TAF] subsidence: departure summary failed (native subsidence summary interruption)"
)
EXPECTED_DIAGNOSTICS = [
    EXPECTED_MOD_ERROR,
    EXPECTED_TAF_ERROR,
]
EXPECTED_PLAYER_LOG = PLAYER_LOG + EXPECTED_MOD_ERROR + "\n" + EXPECTED_TAF_ERROR + "\n"
OWNED_TITLES = (
    "The Thousand and First",
    "The Thousand and First [ALPHA]",
    "The Thousand and First [DEV SCENARIO HARNESS]",
    "The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]",
)
# Exact diagnostic retained in the 2026-09-05 native launch A Player.log, line 74.
NATIVE_CS0114_WARNING = (
    "MODWARN [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]] - "
    "C:/taf-scenario.PLGFPB/Local/Mods/ThousandAndFirst/Harness/r_TAF_RaidMintProbe.cs(132,24): "
    "warning CS0114: 'r_TAF_RaidMintProbe.Reset()' hides inherited member 'IPart.Reset()'. "
    "To make the current member override that implementation, add the override keyword. "
    "Otherwise add the new keyword."
)

# Installed only inside the fixture. Unknown calls fail instead of falling through to real tools.
EXTERNAL = r'''#!/usr/bin/env python3
import json
import os
import pathlib
import sys
import tempfile
import time

base = pathlib.Path(os.environ["LIFECYCLE_FIXTURE"])
mode = os.environ["LIFECYCLE_MODE"]
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]

def event(kind, **facts):
    with (base / "events.jsonl").open("a", encoding="utf-8") as output:
        output.write(json.dumps(dict(kind=kind, **facts)) + "\n")

def option(flag):
    return args[args.index(flag) + 1]

def refuse(message):
    print(message, file=sys.stderr)
    raise SystemExit(1)

if name == "wslpath":
    assert len(args) == 2 and args[0] == "-w", args
    print(args[1])
elif name == "mktemp":
    if args == ["-d", "/mnt/c/taf-scenario.XXXXXX"]:
        root = tempfile.mkdtemp(prefix="taf-scenario.", dir=base / "profiles")
        event("allocate", root=root)
        print(root)
    elif not args:
        descriptor, path = tempfile.mkstemp(dir=base / "scratch")
        os.close(descriptor)
        print(path)
    else:
        raise AssertionError("Unexpected mktemp call: " + repr(args))
elif name == "prepare-scenario.sh":
    root = pathlib.Path(args[0])
    assert root.parent == base / "profiles"
    event("prepare", root=str(root), arguments=args,
          request=os.environ["TAF_REQUEST"], script=os.environ["TAF_SCENARIO_SCRIPT"])
    for directory in (root / "Local", root / "Save", root / "Synced", pathlib.Path(str(root) + ".seal")):
        directory.mkdir()
        (directory / "sentinel.txt").write_text("retained fixture\n", encoding="utf-8")
    (root / "request.txt").write_text(os.environ["TAF_REQUEST"], encoding="utf-8")
    if mode == "prepare_refusal":
        refuse("fixture prepare refusal")
elif name == "powershell.exe":
    target = pathlib.Path(option("-File")).name
    game = option("-Game")
    assert game == str(base / "fake install" / "CoQ.exe"), game
    if target == "scenario-process-control.ps1" and option("-Mode") == "idle":
        assert "-Root" not in args
        event("idle", game=game, argv=args)
        if mode == "existing_game":
            refuse("Existing fixture game; launch refused without stopping it")
    elif target == "run-scenario.ps1":
        root = pathlib.Path(option("-Root"))
        assert root.parent == base / "profiles"
        event("launch", root=str(root), game=game, argv=args)
        (root / "Player.log").write_text(os.environ["LIFECYCLE_PLAYER_LOG"], encoding="utf-8")
        if mode != "missing_receipt":
            receipt = "{malformed" if mode == "malformed_receipt" else "fixture ownership only"
            (root / "process-ownership.json").write_text(receipt, encoding="utf-8")
        if mode == "launcher_refusal":
            refuse("fixture launcher refusal after current root activation")
        if mode != "term":
            journal = os.environ["LIFECYCLE_JOURNAL"]
            if mode == "journal_refusal":
                journal = journal.replace("\tstatus\tOK\t", "\tstatus\tREFUSED\t")
            (root / "scenario-journal.tsv").write_text(journal, encoding="utf-8")
    elif target == "scenario-process-control.ps1" and option("-Mode") == "stop":
        root = pathlib.Path(option("-Root"))
        assert root.parent == base / "profiles"
        event("stop", root=str(root), game=game, argv=args,
              archives=sorted(path.name for path in (base / "report").glob("journal-*.tsv")))
        if mode in ("missing_receipt", "malformed_receipt"):
            refuse("fixture ownership receipt refused: " + mode)
        # Stopping changes only fixture outputs. An early stop would poison archive/assertion.
        (root / "scenario-journal.tsv").write_text("changed after scoped stop\n", encoding="utf-8")
        (root / "Player.log").write_text("changed after scoped stop\n", encoding="utf-8")
        print("STOPPED fixture root=" + str(root))
    else:
        raise AssertionError("Unexpected PowerShell call: " + repr(args))
elif name == "sleep":
    assert args == ["5"] and mode == "term", args
    event("poll-ready")
    deadline = time.monotonic() + 10
    while not (base / "release-poll").exists():
        if time.monotonic() >= deadline:
            refuse("fixture poll was not released")
        time.sleep(0.01)
else:
    raise AssertionError("Unknown fixture executable: " + name)
'''


class PersonaRunnerLifecycleTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="taf-persona-lifecycle-")
        self.addCleanup(self.temporary.cleanup)
        self.base = pathlib.Path(self.temporary.name)
        self.tools = self.base / "repo" / "Tools"
        self.bin = self.base / "bin"
        for directory in (self.tools / "personas", self.bin, self.base / "profiles",
                          self.base / "scratch", self.base / "report", self.base / "fake install"):
            directory.mkdir(parents=True)
        for relative in ("run-personas.sh", "personas/persona_matrix.py", "check-player-log.sh"):
            shutil.copy2(ROOT / "Tools" / relative, self.tools / relative)
        for name in ("run-scenario.ps1", "scenario-process-control.ps1"):
            (self.tools / name).write_text("fixture boundary; never executed\n", encoding="utf-8")
        for name in ("powershell.exe", "wslpath", "mktemp", "sleep"):
            self.write_executable(self.bin / name, EXTERNAL)
        self.write_executable(self.tools / "prepare-scenario.sh", EXTERNAL)
        self.game = self.base / "fake install" / "CoQ.exe"
        self.game.write_text("inert fixture, not executable\n", encoding="utf-8")
        for name in ("alpha", "beta"):
            (self.tools / "personas" / (name + ".persona")).write_text(
                PERSONA.format(name=name), encoding="utf-8")
        self.report = self.base / "report" / "matrix.tsv"
        self.env = {key: value for key, value in os.environ.items() if not key.startswith("TAF_")}
        self.env.update(PATH=str(self.bin) + os.pathsep + os.environ["PATH"],
                        TAF_QUD_ROOT=str(self.game.parent), TAF_PERSONA_REPORT=str(self.report),
                        TAF_PERSONA_CAPTURE_DIR="", TAF_PERSONA_TIMEOUT="10",
                        LIFECYCLE_FIXTURE=str(self.base), LIFECYCLE_MODE="success",
                        LIFECYCLE_JOURNAL=JOURNAL, LIFECYCLE_PLAYER_LOG=PLAYER_LOG,
                        PYTHONDONTWRITEBYTECODE="1")

    @staticmethod
    def write_executable(path, text):
        path.write_text(text, encoding="utf-8")
        path.chmod(0o700)

    def command(self, names):
        return ["bash", str(self.tools / "run-personas.sh"), *names]

    def run_cli(self, mode="success", names=("alpha",)):
        self.env["LIFECYCLE_MODE"] = mode
        result = subprocess.run(self.command(names), env=self.env, cwd=self.base,
                                capture_output=True, text=True, timeout=20)
        self.assertNotIn("Traceback", result.stdout + result.stderr)
        return result

    def events(self, kind=None):
        path = self.base / "events.jsonl"
        found = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()] if path.exists() else []
        return found if kind is None else [entry for entry in found if entry["kind"] == kind]

    def rows(self):
        with self.report.open(encoding="utf-8", newline="") as source:
            return list(csv.DictReader(source, delimiter="\t"))

    def assert_scoped_calls(self):
        launched = {entry["root"] for entry in self.events("launch")}
        for entry in self.events("stop"):
            self.assertIn(entry["root"], launched)
            self.assertEqual(str(self.game), entry["game"])
            self.assertIn("-Root", entry["argv"])
        for entry in self.events("launch"):
            self.assertEqual(str(self.game), entry["game"])

    def assert_profiles_retained(self):
        for entry in self.events("prepare"):
            root = pathlib.Path(entry["root"])
            for directory in (root / "Local", root / "Save", root / "Synced", pathlib.Path(str(root) + ".seal")):
                self.assertEqual("retained fixture\n", (directory / "sentinel.txt").read_text(encoding="utf-8"))

    def configure_expected_diagnostics(self, name, player_log):
        manifest = PERSONA.format(name=name) + "LOG_EXPECT=" + json.dumps(EXPECTED_DIAGNOSTICS) + "\n"
        (self.tools / "personas" / (name + ".persona")).write_text(manifest, encoding="utf-8")
        self.env["LIFECYCLE_PLAYER_LOG"] = player_log

    def assert_new_scoped_cycle(self, events_before):
        events = self.events()[events_before:]
        self.assertEqual(["idle", "allocate", "prepare", "launch", "stop"],
                         [entry["kind"] for entry in events])
        self.assertEqual(events[3]["root"], events[4]["root"])
        self.assert_scoped_calls()
        self.assert_profiles_retained()

    def assert_unexpected_log_refused(self, name, player_log):
        manifest = PERSONA.format(name=name)
        self.assertNotIn("LOG_EXPECT", manifest)
        (self.tools / "personas" / (name + ".persona")).write_text(manifest, encoding="utf-8")
        self.env["LIFECYCLE_PLAYER_LOG"] = player_log
        events_before = len(self.events())
        result = self.run_cli(names=(name,))
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertEqual("FAIL", self.rows()[0]["verdict"])
        self.assertIn("Player.log rejected: SMOKE LOG FAILED", self.rows()[0]["detail"])
        self.assertNotIn("PERSONA MATRIX GREEN", result.stdout)
        self.assertEqual(player_log.encode("utf-8"),
                         (self.report.parent / ("player-" + name + ".log")).read_bytes())
        self.assertEqual(JOURNAL, (self.report.parent / ("journal-" + name + ".tsv")).read_text())
        self.assert_new_scoped_cycle(events_before)

    def run_log_checker(self, player_log, allow=""):
        path = self.base / "direct-check.Player.log"
        path.write_text(player_log, encoding="utf-8")
        return subprocess.run(["bash", str(self.tools / "check-player-log.sh"), str(path)],
                              env={**self.env, "TAF_LOG_ALLOW": allow}, cwd=self.base,
                              capture_output=True, text=True, timeout=10)

    def test_unexpected_owned_warnings_and_errors_refuse_every_supported_title(self):
        for index, title in enumerate(OWNED_TITLES):
            for level in ("WARN", "ERROR"):
                with self.subTest(title=title, level=level):
                    # No separate TAF line: this also requires the exact title to prove load evidence.
                    self.assert_unexpected_log_refused(
                        "title-" + str(index) + "-" + level.lower(),
                        "MOD" + level + " [" + title + "] - unexpected compiler diagnostic\n")

    def test_actual_native_cs0114_warning_refuses_without_log_expect(self):
        self.assert_unexpected_log_refused("native-cs0114", PLAYER_LOG + NATIVE_CS0114_WARNING + "\r\n")

    def test_unexpected_taf_stack_frames_refuse_without_log_expect(self):
        for index, frame in enumerate((
                "  at ThousandAndFirst.UnknownCallback.Run()\n",
                "--- ThousandAndFirst.UnknownCallback:Run()\n")):
            with self.subTest(frame=frame):
                self.assert_unexpected_log_refused("unknown-frame-" + str(index), PLAYER_LOG + frame)

    def test_foreign_dlc_and_near_name_warnings_remain_outside_ordinary_scope(self):
        player_log = PLAYER_LOG + (
            "MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, please convert it "
            "to use the Dependencies field.\n"
            "MODWARN [The Thousand and First Helper] - fixture compiler warning CS0114\n"
            "MODWARN [The Thousand and First [ALPHA] Helper] - fixture compiler warning CS0114\n"
            "MODWARN [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS] Helper] - fixture warning\n"
        )
        self.env["LIFECYCLE_PLAYER_LOG"] = player_log
        result = self.run_cli()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("PASS", self.rows()[0]["verdict"])
        self.assertIn("PERSONA MATRIX GREEN", result.stdout)
        self.assertEqual(player_log, (self.report.parent / "player-alpha.log").read_text())
        self.assert_new_scoped_cycle(0)

    def test_load_evidence_requires_complete_supported_title(self):
        for title in OWNED_TITLES:
            with self.subTest(title=title):
                result = self.run_log_checker("INFO - Loaded [" + title + "]\n")
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("SMOKE LOG CLEAN", result.stdout)
        for title in ("The Thousand and First Helper",
                      "The Thousand and First [ALPHA] Helper",
                      "The Thousand and First [ALPHA] [DEV SCENARIO HARNESS] Helper"):
            with self.subTest(foreign_title=title):
                result = self.run_log_checker("INFO - Loaded [" + title + "]\n")
                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn("no Thousand and First load/runtime evidence", result.stderr)

    def test_explicit_allow_regex_still_allows_only_matching_diagnostic(self):
        expected = "MODWARN [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]] - expected diagnostic\n"
        allow = (r"^MODWARN [[]The Thousand and First [[]ALPHA[]] [[]DEV SCENARIO HARNESS[]][]]"
                 r" - expected diagnostic$")
        result = self.run_log_checker(expected, allow)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        result = self.run_log_checker(expected + "  at ThousandAndFirst.UnknownCallback.Run()\n", allow)
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("SMOKE LOG FAILED", result.stderr)
        self.assertIn("UnknownCallback.Run", result.stderr)
        self.assertNotIn(expected.strip(), result.stderr)

    def test_existing_game_refuses_before_allocating_preparing_or_stopping(self):
        result = self.run_cli("existing_game")
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertEqual(["idle"], [entry["kind"] for entry in self.events()])
        self.assertEqual("FAIL", self.rows()[0]["verdict"])
        self.assertIn("launch preflight refused", self.rows()[0]["detail"])
        self.assertEqual([], list((self.base / "profiles").iterdir()))

    def test_success_archives_and_asserts_before_stopping_each_exact_root(self):
        result = self.run_cli(names=("alpha", "beta"))
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(["PASS", "PASS"], [row["verdict"] for row in self.rows()])
        self.assertEqual(["idle", "allocate", "prepare", "launch", "stop"] * 2,
                         [entry["kind"] for entry in self.events()])
        self.assertEqual(2, len({entry["root"] for entry in self.events("launch")}))
        for name, launch, stop in zip(("alpha", "beta"), self.events("launch"), self.events("stop")):
            self.assertEqual(launch["root"], stop["root"])
            self.assertIn("journal-" + name + ".tsv", stop["archives"])
            self.assertEqual(JOURNAL, (self.report.parent / ("journal-" + name + ".tsv")).read_text())
            self.assertEqual(PLAYER_LOG, (self.report.parent / ("player-" + name + ".log")).read_text())
            self.assertEqual("changed after scoped stop\n",
                             (pathlib.Path(launch["root"]) / "scenario-journal.tsv").read_text())
        self.assert_scoped_calls()
        self.assert_profiles_retained()

    def assert_stop_refusal_blocks_next_persona(self, mode):
        result = self.run_cli(mode, names=("alpha", "beta"))
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        rows = self.rows()
        self.assertEqual(["alpha"], [row["persona"] for row in rows])
        self.assertEqual("FAIL", rows[0]["verdict"])
        self.assertIn("owned shutdown refused", rows[0]["detail"])
        self.assertEqual(1, len(self.events("idle")))
        self.assertEqual(1, len(self.events("launch")))
        # Normal shutdown refuses, then EXIT retries only that same still-active root.
        self.assertEqual(2, len(self.events("stop")))
        self.assertEqual(1, len({entry["root"] for entry in self.events("stop")}))
        self.assertEqual(JOURNAL, (self.report.parent / "journal-alpha.tsv").read_text())
        self.assertNotIn("PERSONA MATRIX GREEN", result.stdout)
        self.assert_scoped_calls()
        self.assert_profiles_retained()

    def test_missing_receipt_turns_asserted_pass_into_failure_and_blocks_next_launch(self):
        self.assert_stop_refusal_blocks_next_persona("missing_receipt")

    def test_malformed_receipt_turns_asserted_pass_into_failure_and_blocks_next_launch(self):
        self.assert_stop_refusal_blocks_next_persona("malformed_receipt")

    def test_prepare_refusal_never_attempts_process_stop(self):
        result = self.run_cli("prepare_refusal")
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertEqual(["idle", "allocate", "prepare"], [entry["kind"] for entry in self.events()])
        self.assertIn("prepare refused", self.rows()[0]["detail"])
        self.assert_profiles_retained()

    def test_launcher_failure_attempts_only_current_root_stop(self):
        unrelated = self.base / "profiles" / "unrelated"
        unrelated.mkdir()
        sentinel = unrelated / "sentinel.txt"
        sentinel.write_text("foreign fixture\n")
        result = self.run_cli("launcher_refusal")
        self.assertEqual(1, result.returncode, result.stderr)
        self.assertEqual(["idle", "allocate", "prepare", "launch", "stop"],
                         [entry["kind"] for entry in self.events()])
        self.assertIn("launch refused", self.rows()[0]["detail"])
        self.assertEqual("foreign fixture\n", sentinel.read_text())
        self.assert_scoped_calls()
        self.assert_profiles_retained()

    def test_real_journal_assertion_refuses_wrong_outcome_and_still_stops_owned_root(self):
        result = self.run_cli("journal_refusal")
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertEqual("FAIL", self.rows()[0]["verdict"])
        self.assertIn("REFUSED", self.rows()[0]["detail"])
        self.assertEqual(1, len(self.events("stop")))
        self.assert_scoped_calls()
        self.assert_profiles_retained()

    def test_expected_diagnostics_preserve_raw_archive_and_filter_only_derived_input(self):
        self.configure_expected_diagnostics("alpha", EXPECTED_PLAYER_LOG)
        result = self.run_cli()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("PASS", self.rows()[0]["verdict"])
        raw = self.report.parent / "player-alpha.log"
        checked = self.report.parent / "checked-alpha.Player.log"
        self.assertEqual(EXPECTED_PLAYER_LOG, raw.read_text(encoding="utf-8"))
        derived = checked.read_text(encoding="utf-8")
        self.assertIn(PLAYER_LOG.strip(), derived)
        self.assertNotIn(EXPECTED_MOD_ERROR, derived)
        self.assertNotIn(EXPECTED_TAF_ERROR, derived)
        self.assertEqual(JOURNAL, (self.report.parent / "journal-alpha.tsv").read_text())
        self.assert_new_scoped_cycle(0)

    def test_missing_or_duplicate_expected_diagnostics_fail_and_stop_owned_root(self):
        variants = {
            "missing-mod": PLAYER_LOG + EXPECTED_TAF_ERROR + "\n",
            "missing-taf": PLAYER_LOG + EXPECTED_MOD_ERROR + "\n",
            "duplicate-mod": EXPECTED_PLAYER_LOG + EXPECTED_MOD_ERROR + "\n",
            "duplicate-taf": EXPECTED_PLAYER_LOG + EXPECTED_TAF_ERROR + "\n",
        }
        for name, player_log in variants.items():
            with self.subTest(name=name):
                self.configure_expected_diagnostics(name, player_log)
                events_before = len(self.events())
                result = self.run_cli(names=(name,))
                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertEqual("FAIL", self.rows()[0]["verdict"])
                self.assertIn("expected diagnostic check refused", self.rows()[0]["detail"])
                self.assertEqual(player_log, (self.report.parent / ("player-" + name + ".log")).read_text())
                self.assertNotIn("PERSONA MATRIX GREEN", result.stdout)
                self.assert_new_scoped_cycle(events_before)

    def test_unrelated_errors_remain_fatal_beside_exact_expected_diagnostics(self):
        variants = {
            "extra-taf": "[TAF] error: unrelated operation failed\n",
            "extra-mod": "MODERROR [Unrelated Mod] - unexpected fixture diagnostic\n",
            "extra-warning": "MODWARN [The Thousand and First] - unexpected compiler warning\n",
            "gate-frame": "  at ThousandAndFirst.KingdomScenarioNewGameGate.mutate()\n",
        }
        for name, unexpected in variants.items():
            with self.subTest(name=name):
                player_log = EXPECTED_PLAYER_LOG + unexpected
                self.configure_expected_diagnostics(name, player_log)
                if name == "gate-frame":
                    path = self.tools / "personas" / (name + ".persona")
                    path.write_text(path.read_text().replace(
                        "EXPECT=status:OK~fixture-observed,COMPLETE", "EXPECT=GATE-REFUSED"),
                        encoding="utf-8")
                    self.env["LIFECYCLE_JOURNAL"] = (
                        "2026-09-05T00:00:01.000Z\tGATE-REFUSED\tREFUSED\tfixture gate\n")
                events_before = len(self.events())
                result = self.run_cli(names=(name,))
                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertEqual("FAIL", self.rows()[0]["verdict"])
                self.assertTrue("Player.log rejected" in self.rows()[0]["detail"]
                                or "expected diagnostic check refused" in self.rows()[0]["detail"],
                                self.rows()[0]["detail"])
                self.assertEqual(player_log, (self.report.parent / ("player-" + name + ".log")).read_text())
                self.assertNotIn("PERSONA MATRIX GREEN", result.stdout)
                self.assert_new_scoped_cycle(events_before)

    def test_matching_diagnostics_cannot_override_refused_journal(self):
        self.configure_expected_diagnostics("alpha", EXPECTED_PLAYER_LOG)
        result = self.run_cli("journal_refusal")
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertEqual("FAIL", self.rows()[0]["verdict"])
        self.assertIn("REFUSED", self.rows()[0]["detail"])
        self.assertEqual(EXPECTED_PLAYER_LOG, (self.report.parent / "player-alpha.log").read_text())
        self.assertNotIn(EXPECTED_MOD_ERROR, (self.report.parent / "checked-alpha.Player.log").read_text())
        self.assertIn("\tstatus\tREFUSED\t", (self.report.parent / "journal-alpha.tsv").read_text())
        self.assert_new_scoped_cycle(0)

    def test_term_trap_stops_only_held_runner_root_and_preserves_profiles(self):
        self.env["LIFECYCLE_MODE"] = "term"
        process = subprocess.Popen(self.command(("alpha", "beta")), env=self.env, cwd=self.base,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 10
            while not self.events("poll-ready") and process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.01)
            self.assertTrue(self.events("poll-ready"), "Runner never reached the controlled polling boundary")
            # This is the directly held disposable bash subprocess, never a name/PID lookup or game.
            process.send_signal(signal.SIGTERM)
            (self.base / "release-poll").touch()
            stdout, stderr = process.communicate(timeout=10)
        finally:
            (self.base / "release-poll").touch()
            if process.poll() is None:
                process.terminate()
                process.communicate(timeout=10)
        self.assertEqual(143, process.returncode, stdout + stderr)
        self.assertEqual(1, len(self.events("launch")))
        self.assertEqual(1, len(self.events("stop")))
        self.assertNotIn("PERSONA MATRIX GREEN", stdout)
        self.assertEqual([], self.rows())
        self.assert_scoped_calls()
        self.assert_profiles_retained()


if __name__ == "__main__":
    unittest.main()
