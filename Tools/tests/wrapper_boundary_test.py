"""Regression for issue #134's actual root cause: DevTests/test.ps1 (and powershell.exe
invoking it) already propagate a licensed-suite launch/test failure as a non-zero exit --
see test_ps1_exit_propagation_test.py. The false zero builders observed instead lived at
the bash WRAPPER boundary: an ad hoc invocation ending `... ; echo "rc=$?"; grep ... LOG`
(camp-native-licensed-3.log's exact shape) whose own process exit reflects the LAST
command run (the matching grep, exit 0), not the licensed suites' real exit code.

Coverage:
1. Reproduces the historical trap exactly via Tools/tests/repro_issue_134_wrapper_boundary.sh
   (kept as standalone, re-runnable evidence, mirroring repro_issue_115.sh) -- confirms an
   inner exit of 154 still yields wrapper-own-exit 0 under the old shape.
2. Exercises the shipped fix, Tools/run-licensed-suites.sh, with a stubbed `powershell.exe`
   on PATH standing in for the real WSL invocation -- confirms the wrapper's own process
   exit equals the stub's exit code, for both a failing (154) and a succeeding (0) stub.
3. A mutation: reverting run-licensed-suites.sh's tail to the historical trailing-echo shape
   (no final `exit "$rc"`) is caught by test 2's failing-stub case.
"""

import os
import stat
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPRO_SCRIPT = ROOT / "Tools" / "tests" / "repro_issue_134_wrapper_boundary.sh"
WRAPPER_SCRIPT = ROOT / "Tools" / "run-licensed-suites.sh"


def make_stub_powershell(directory, exit_code):
    """A stand-in for `powershell.exe -NoProfile -ExecutionPolicy Bypass -Command
    '...; exit $LASTEXITCODE'`: ignores its arguments (the wrapper's own invocation shape
    is exercised, not PowerShell's), and exits with the given code -- standing in for
    whatever real exit code a real dotnet-run launch failure would have produced."""
    stub = directory / "powershell.exe"
    stub.write_text("#!/bin/sh\nexit " + str(exit_code) + "\n", encoding="utf-8")
    stub.chmod(stub.stat().st_mode | stat.S_IEXEC | stat.S_IXGRP | stat.S_IXOTH)
    return stub


class WrapperBoundaryHistoricalTrapTest(unittest.TestCase):
    def test_old_wrapper_shape_swallows_a_real_inner_failure_to_zero(self):
        result = subprocess.run(
            ["bash", str(REPRO_SCRIPT)], capture_output=True, text=True, timeout=10
        )
        self.assertIn("INNER_REAL_EXIT=154", result.stdout)
        self.assertIn("WRAPPER_OWN_EXIT_IF_ENDED_HERE=0", result.stdout)
        self.assertEqual(
            0,
            result.returncode,
            "the historical trap must reproduce exactly: inner=154, wrapper-own-exit=0",
        )


class RunLicensedSuitesWrapperTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-run-licensed-suites-test-")
        self.addCleanup(self.temp.cleanup)
        self.stub_dir = Path(self.temp.name)
        self.env = dict(os.environ)
        self.env["PATH"] = str(self.stub_dir) + os.pathsep + self.env.get("PATH", "")

    def run_wrapper(self, worktree="/does/not/need/to/exist"):
        return subprocess.run(
            ["bash", str(WRAPPER_SCRIPT), worktree],
            env=self.env,
            capture_output=True,
            text=True,
            timeout=10,
        )

    def test_propagates_a_failing_stub_exit_code_as_its_own(self):
        make_stub_powershell(self.stub_dir, 154)
        result = self.run_wrapper()
        self.assertEqual(154, result.returncode)
        self.assertEqual(
            "LICENSED_SUITES_EXIT=154", result.stdout.strip().splitlines()[-1]
        )

    def test_propagates_a_succeeding_stub_exit_code_as_its_own(self):
        make_stub_powershell(self.stub_dir, 0)
        result = self.run_wrapper()
        self.assertEqual(0, result.returncode)
        self.assertEqual(
            "LICENSED_SUITES_EXIT=0", result.stdout.strip().splitlines()[-1]
        )

    def test_mutation_trailing_echo_without_final_exit_is_caught(self):
        make_stub_powershell(self.stub_dir, 154)
        mutant_text = WRAPPER_SCRIPT.read_text(encoding="utf-8")
        old_tail = 'echo "LICENSED_SUITES_EXIT=$rc"\nexit "$rc"\n'
        new_tail = 'echo "LICENSED_SUITES_EXIT=$rc"\n'
        self.assertEqual(1, mutant_text.count(old_tail))
        mutant_text = mutant_text.replace(old_tail, new_tail)
        mutant = self.stub_dir / "run-licensed-suites-mutant.sh"
        mutant.write_text(mutant_text, encoding="utf-8")
        mutant.chmod(mutant.stat().st_mode | stat.S_IEXEC)
        result = subprocess.run(
            ["bash", str(mutant), "/does/not/need/to/exist"],
            env=self.env,
            capture_output=True,
            text=True,
            timeout=10,
        )
        self.assertEqual(
            0,
            result.returncode,
            'mutation was not caught: removing the final `exit "$rc"` should reproduce '
            "the historical false-zero trap (mutant's own exit should fall back to the "
            "echo's exit, 0, even though the stub failed with 154)",
        )



def make_env_capturing_powershell(directory):
    """A stand-in powershell.exe that records the decompile hand-off it was given."""
    stub = directory / "powershell.exe"
    stub.write_text(
        "#!/bin/sh\n"
        'printf \'%s\\n\' "DECOMPILED=${TAF_QUD_DECOMPILED-<unset>}" "WSLENV=$WSLENV" '
        '> "$TAF_STUB_CAPTURE"\n'
        "exit 0\n",
        encoding="utf-8",
    )
    stub.chmod(stub.stat().st_mode | stat.S_IEXEC | stat.S_IXGRP | stat.S_IXOTH)
    return stub


class RunLicensedSuitesDecompileHandOffTest(unittest.TestCase):
    """The Windows leg cannot see the WSL home that holds the version-keyed decompile
    archive, so the wrapper passes an explicit TAF_QUD_DECOMPILED, or the pinned core's
    default archive when it exists, as a WSLENV-translated path, and nothing otherwise."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-run-licensed-decompile-test-")
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.stub_dir = root / "bin"
        self.stub_dir.mkdir()
        make_env_capturing_powershell(self.stub_dir)
        self.home = root / "home"
        self.home.mkdir()
        self.worktree = root / "worktree"
        (self.worktree / "Tools").mkdir(parents=True)
        (self.worktree / "Tools" / "workshop_metadata.py").write_text(
            'GAME_MARKETING_VERSION = "1.0.5"\nGAME_CORE_BUILD = "9.8.7.6"\n',
            encoding="utf-8",
        )
        self.capture = root / "capture.txt"
        self.env = dict(os.environ)
        self.env.pop("TAF_QUD_DECOMPILED", None)
        self.env["HOME"] = str(self.home)
        self.env["TAF_STUB_CAPTURE"] = str(self.capture)
        self.env["PATH"] = str(self.stub_dir) + os.pathsep + self.env.get("PATH", "")

    def archive(self, leaf):
        path = self.home / "coq" / "qud_helper" / "game_base" / "decompiled" / leaf
        path.mkdir(parents=True)
        return path

    def hand_off(self):
        result = subprocess.run(
            ["bash", str(WRAPPER_SCRIPT), str(self.worktree)],
            env=self.env,
            capture_output=True,
            text=True,
            timeout=10,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        rows = self.capture.read_text(encoding="utf-8").splitlines()
        return rows[0].split("=", 1)[1], rows[1].split("=", 1)[1].split(":")

    def test_passes_the_pinned_cores_default_archive_as_a_translated_path(self):
        default = self.archive("9.8.7.6-ilspy9.1")
        decompiled, wslenv = self.hand_off()
        self.assertEqual(str(default), decompiled)
        self.assertIn("TAF_QUD_DECOMPILED/p", wslenv)

    def test_never_passes_a_replaced_cores_archive(self):
        self.archive("2.0.211.51-ilspy9.1")
        self.archive("6000.0.41.4645959")
        decompiled, wslenv = self.hand_off()
        self.assertEqual("<unset>", decompiled)
        self.assertNotIn("TAF_QUD_DECOMPILED/p", wslenv)

    def test_an_explicit_root_wins_over_the_default(self):
        self.archive("9.8.7.6-ilspy9.1")
        self.env["TAF_QUD_DECOMPILED"] = "/explicit/decompile"
        decompiled, wslenv = self.hand_off()
        self.assertEqual("/explicit/decompile", decompiled)
        self.assertIn("TAF_QUD_DECOMPILED/p", wslenv)

    def test_keeps_the_existing_translated_inputs(self):
        decompiled, wslenv = self.hand_off()
        self.assertEqual("<unset>", decompiled)
        self.assertEqual(["TAF_QUD_BASE_WIN/w", "TAF_TEST_SCRIPT_WIN/w"], wslenv)


if __name__ == "__main__":
    unittest.main()
