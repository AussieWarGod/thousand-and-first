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
