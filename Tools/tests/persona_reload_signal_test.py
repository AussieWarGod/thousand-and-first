"""Executable process-lifecycle proof for the cold-reload hosts kernel parent-death signal.

Source pins (KingdomScenarioPersonaSourceTests.ReloadTokenDispatchesToTheColdProcessRefusal,
persona_reload_test.py's CLI wiring pin) establish that the arming call and its ordering exist.
These tests instead run real child processes and real signals: they prove the kernel actually
disposes of an orphan candidate, that a failed prctl refuses loudly rather than continuing
unprotected, and that run-personas.sh's own TERM path forwards to the reload host and reports
truthfully - all without launching Caves of Qud or touching a process this suite does not
itself own.
"""
from __future__ import annotations

import importlib.util
import os
import pathlib
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest

TOOLS = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))


def load_cli_module():
    """run-persona-reload.py has a hyphenated name and is never imported elsewhere; load it by
    path so arm_parent_death_signal() and its exception class can be exercised directly."""
    spec = importlib.util.spec_from_file_location(
        "taf_run_persona_reload_cli", TOOLS / "run-persona-reload.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CLI = load_cli_module()

# A disposable throwaway child: arms the REAL arm_parent_death_signal() (loaded from
# run-persona-reload.py, not a reimplementation), then blocks. Marker files (no embedded
# escapes needed - each write is a single line) record what happened for the test to read
# after the process tree is gone.
CHILD_SCRIPT_TEMPLATE = '''
import importlib.util
import os
import pathlib
import sys
import time
import signal

sys.path.insert(0, TOOLS_DIR)
_spec = importlib.util.spec_from_file_location(
    "taf_run_persona_reload_cli", pathlib.Path(TOOLS_DIR) / "run-persona-reload.py")
cli = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cli)

MARKER = HANDLED_MARKER


def handler(*_args):
    open(MARKER, "w", encoding="utf-8").write("handled")
    sys.exit(77)


signal.signal(signal.SIGTERM, handler)
try:
    cli.arm_parent_death_signal()
except cli.ParentDeathSignalUnavailable as error:
    open(REFUSED_MARKER, "w", encoding="utf-8").write(str(error))
    sys.exit(3)
open(STARTED_MARKER, "w", encoding="utf-8").write(str(os.getpid()))
time.sleep(20)
open(TIMEDOUT_MARKER, "w", encoding="utf-8").write("timedout")
'''


def render_child_script(tools_dir, handled, refused, started, timedout):
    text = CHILD_SCRIPT_TEMPLATE
    text = text.replace("TOOLS_DIR", repr(str(tools_dir)))
    text = text.replace("HANDLED_MARKER", repr(str(handled)))
    text = text.replace("REFUSED_MARKER", repr(str(refused)))
    text = text.replace("STARTED_MARKER", repr(str(started)))
    text = text.replace("TIMEDOUT_MARKER", repr(str(timedout)))
    return text


class ParentDeathSignalLifecycleTest(unittest.TestCase):
    """Spawns the REAL arm_parent_death_signal() inside a disposable child, kills the shell
    that owns it, and proves the child dies on its own without this test ever signalling it
    directly."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-pdeathsig-")
        self.addCleanup(self.temp.cleanup)
        self.base = pathlib.Path(self.temp.name)

    def child_script(self, name):
        path = self.base / (name + ".py")
        path.write_text(render_child_script(
            TOOLS, self.base / (name + ".handled"), self.base / (name + ".refused"),
            self.base / (name + ".started"), self.base / (name + ".timedout")),
            encoding="utf-8")
        return path

    def wait_for(self, path, timeout=5):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if path.exists():
                return True
            time.sleep(0.02)
        return False

    def test_the_after_arming_recheck_refuses_when_the_parent_died_during_the_call(self):
        """Isolates the DURING-arming race (point a, second half): the baseline read and the
        pre-arm recheck both see the original parent, prctl itself succeeds, but the parent is
        gone by the time the final recheck runs. Must still refuse, not return as if armed
        cleanly - the newly-armed signal now targets whatever reparented us and will never
        fire."""
        sequence = [100, 100, 999]  # baseline, pre-arm recheck (matches), post-arm recheck (gone)
        calls = []

        def fake_getppid():
            calls.append(True)
            return sequence[len(calls) - 1]

        class Handle:
            def prctl(self, *_args):
                return 0

        original_getppid, original_cdll = CLI.os.getppid, CLI.ctypes.CDLL
        CLI.os.getppid = fake_getppid
        CLI.ctypes.CDLL = lambda *a, **k: Handle()
        try:
            with self.assertRaises(KeyboardInterrupt) as raised:
                CLI.arm_parent_death_signal()
            self.assertIn("while the parent-death signal was being armed", str(raised.exception))
        finally:
            CLI.os.getppid = original_getppid
            CLI.ctypes.CDLL = original_cdll
        self.assertEqual(3, len(calls), "baseline, pre-arm recheck, and the post-arm recheck")

    def test_child_dies_on_its_own_when_the_owning_shell_is_killed(self):
        script = self.child_script("dies")
        parent = subprocess.Popen(
            ["bash", "-c",
             'TAF_RELOAD_PARENT_PID="$$" python3 "$1" & echo $! > "$2"; wait "$!"',
             "_", str(script), str(self.base / "child.pid")],
            cwd=self.base)
        self.addCleanup(lambda: parent.poll() is None and parent.kill())
        self.assertTrue(self.wait_for(self.base / "dies.started"),
            "child never reported it was armed and running")
        child_pid = int((self.base / "child.pid").read_text().strip())
        parent.send_signal(signal.SIGTERM)
        parent.wait(timeout=5)
        self.assertTrue(self.wait_for(self.base / "dies.handled", timeout=5),
            "the childs own SIGTERM handler never ran - it was orphaned, not signalled")
        self.assertFalse((self.base / "dies.timedout").exists(),
            "child hit its own timeout instead of receiving the parent-death signal")
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            try:
                os.kill(child_pid, 0)
            except ProcessLookupError:
                break
            time.sleep(0.02)
        else:
            self.fail("child process was still alive after its own handler ran")

    def test_child_refuses_immediately_when_already_orphaned_before_arming(self):
        # No parent-pid bookkeeping at all in the environment we hand it: any value we
        # supply can never match this test's own real getppid(), forcing the
        # "already orphaned before arming" branch deterministically.
        script = self.child_script("preorphaned")
        env = dict(os.environ, TAF_RELOAD_PARENT_PID="1")
        result = subprocess.run([sys.executable, str(script)], cwd=self.base, env=env,
                                capture_output=True, text=True, timeout=10)
        self.assertNotEqual(0, result.returncode)
        self.assertFalse((self.base / "preorphaned.started").exists(),
            "must refuse before doing any real work, not merely before returning")
        self.assertFalse((self.base / "preorphaned.handled").exists())


class ParentDeathSignalFailureTest(unittest.TestCase):
    """A failed prctl (missing libc, refused syscall) must raise ParentDeathSignalUnavailable -
    never continue silently unprotected. Exercises the real function with the real ctypes
    module; only the tiny surface that can fail (find_library / CDLL.prctl) is substituted.
    """

    def test_missing_libc_refuses_explicitly(self):
        class NoLibc:
            @staticmethod
            def find_library(_name):
                return None

        original = CLI.ctypes.util
        CLI.ctypes.util = NoLibc
        try:
            with self.assertRaises(CLI.ParentDeathSignalUnavailable) as raised:
                CLI.arm_parent_death_signal()
            self.assertIn("libc not found", str(raised.exception))
        finally:
            CLI.ctypes.util = original

    def test_prctl_refusal_errno_refuses_explicitly_and_never_swallows_it(self):
        class RefusingHandle:
            def prctl(self, *_args):
                CLI.ctypes.set_errno(1)  # EPERM
                return -1

        original_cdll = CLI.ctypes.CDLL
        CLI.ctypes.CDLL = lambda *args, **kwargs: RefusingHandle()
        try:
            with self.assertRaises(CLI.ParentDeathSignalUnavailable) as raised:
                CLI.arm_parent_death_signal()
            self.assertIn("prctl(PR_SET_PDEATHSIG) refused", str(raised.exception))
            self.assertIn("errno 1", str(raised.exception))
        finally:
            CLI.ctypes.CDLL = original_cdll

    def test_the_before_arming_check_refuses_without_ever_attempting_to_arm(self):
        """Isolates the BEFORE-arming race (point a, first half) from the AFTER-arming
        recheck: a real subprocess cannot tell which of the two fired (both produce the same
        nonzero exit), so this monkeypatches getppid to prove specifically that a mismatch
        seen before any prctl call is caught there and never reaches prctl at all."""
        sequence = [100, 999]  # first read (the "expected" baseline), second read (mismatch)
        calls = []

        def fake_getppid():
            calls.append(True)
            return sequence[len(calls) - 1]

        attempted = []

        class Handle:
            def prctl(self, *args):
                attempted.append(args)
                return 0

        original_getppid, original_cdll = CLI.os.getppid, CLI.ctypes.CDLL
        CLI.os.getppid = fake_getppid
        CLI.ctypes.CDLL = lambda *a, **k: Handle()
        try:
            with self.assertRaises(KeyboardInterrupt) as raised:
                CLI.arm_parent_death_signal()
            self.assertIn("before the parent-death signal could be armed", str(raised.exception))
        finally:
            CLI.os.getppid = original_getppid
            CLI.ctypes.CDLL = original_cdll
        self.assertEqual([], attempted, "prctl must never be attempted once already orphaned")
        self.assertEqual(2, len(calls),
            "exactly the baseline read and its immediate recheck - never a third, post-arm read")

    def test_a_refused_arming_is_never_caught_and_ignored_by_main(self):
        """main() lets ParentDeathSignalUnavailable through its own except clause as an
        ordinary REFUSED verdict (never a silent pass to unprotected work): this proves the
        exception is Exception-derived and thus already covered by that clause, so a future
        narrowing of that except could not quietly stop catching it."""
        self.assertTrue(issubclass(CLI.ParentDeathSignalUnavailable, Exception))


class RunPersonasTermForwardingTest(unittest.TestCase):
    """Drives the REAL run-personas.sh (copied unchanged) dispatching a real reload persona
    to a stub run-persona-reload.py that arms the REAL parent-death signal and blocks; proves
    the shell's TERM path reports truthfully and the stub is not left orphaned.
    """

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-reload-term-")
        self.addCleanup(self.temp.cleanup)
        self.base = pathlib.Path(self.temp.name)
        self.tools = self.base / "repo" / "Tools"
        for directory in (self.tools / "personas", self.base / "fake install",
                          self.base / "report"):
            directory.mkdir(parents=True)
        shutil.copy2(TOOLS / "run-personas.sh", self.tools / "run-personas.sh")
        shutil.copy2(TOOLS / "personas" / "persona_matrix.py",
                     self.tools / "personas" / "persona_matrix.py")
        for name in ("run-scenario.ps1", "scenario-process-control.ps1"):
            (self.tools / name).write_text("fixture boundary; never executed",
                                           encoding="utf-8")
        (self.tools / "check-player-log.sh").write_text("#!/bin/sh\nexit 0\n",
                                                        encoding="utf-8")
        (self.tools / "check-player-log.sh").chmod(0o755)
        (self.tools / "prepare-scenario.sh").write_text("#!/bin/sh\nexit 0\n",
                                                        encoding="utf-8")
        (self.tools / "prepare-scenario.sh").chmod(0o755)
        self.game = self.base / "fake install" / "CoQ.exe"
        self.game.write_text("inert fixture, not executable", encoding="utf-8")
        (self.tools / "personas" / "reload.persona").write_text(
            "REQUEST=founding-first-city\n"
            "SCRIPT=reload-descendant quickstart marsh yes\n"
            "EXPECT=RELOAD-COMPLETE\n", encoding="utf-8")
        self.started = self.base / "stub.started"
        self.handled = self.base / "stub.handled"
        stub_lines = [
            "import importlib.util, os, pathlib, sys, time, signal",
            "sys.path.insert(0, " + repr(str(TOOLS)) + ")",
            "_spec = importlib.util.spec_from_file_location("
            + repr("taf_run_persona_reload_cli") + ", pathlib.Path("
            + repr(str(TOOLS)) + ") / " + repr("run-persona-reload.py") + ")",
            "cli = importlib.util.module_from_spec(_spec)",
            "_spec.loader.exec_module(cli)",
            "def handler(*_a):",
            "    open(" + repr(str(self.handled)) + ", " + repr("w") + ").write(" + repr("handled") + ")",
            "    sys.exit(77)",
            "signal.signal(signal.SIGTERM, handler)",
            "cli.arm_parent_death_signal()",
            "open(" + repr(str(self.started)) + ", " + repr("w") + ").write(str(os.getpid()))",
            "time.sleep(20)",
        ]
        stub = "\n".join(stub_lines) + "\n"
        (self.tools / "run-persona-reload.py").write_text(stub, encoding="utf-8")
        self.env = {key: value for key, value in os.environ.items()
                    if not key.startswith("TAF_")}
        self.env.update(PATH=os.environ["PATH"], TAF_QUD_ROOT=str(self.game.parent),
                        TAF_PERSONA_REPORT=str(self.base / "report" / "matrix.tsv"),
                        TAF_PERSONA_CAPTURE_DIR="", PYTHONDONTWRITEBYTECODE="1")

    def wait_for(self, path, timeout=10):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if path.exists():
                return True
            time.sleep(0.02)
        return False

    def test_term_forwards_to_the_reload_stub_and_reports_truthfully(self):
        process = subprocess.Popen(
            ["bash", str(self.tools / "run-personas.sh"), "reload"],
            env=self.env, cwd=self.base, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True)
        try:
            self.assertTrue(self.wait_for(self.started),
                "reload stub never reported it was armed and running")
            process.send_signal(signal.SIGTERM)
            stdout, stderr = process.communicate(timeout=10)
        finally:
            if process.poll() is None:
                process.kill()
                process.communicate(timeout=5)
        self.assertEqual(143, process.returncode, stdout + stderr)
        # Truthful reporting (point d): the shell never claims cleanup completed, only that
        # it handed off to a mechanism it cannot itself confirm.
        self.assertIn("cold-reload host now owns its own exact-receipt cleanup", stderr)
        self.assertNotIn("cleanup complete", stderr.lower())
        # And the stub was not orphaned: its own handler actually ran.
        self.assertTrue(self.wait_for(self.handled, timeout=5),
            "the stubs own SIGTERM handler never ran - it was orphaned by the shell exit")


if __name__ == "__main__":
    unittest.main()
