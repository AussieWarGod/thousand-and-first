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


if __name__ == "__main__":
    unittest.main()
