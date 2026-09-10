"""Regression for issue #134: DevTests/test.ps1 must exit non-zero and name the failing
leg whenever either licensed leg (TafTests, PortableTests) fails to restore or launch.

Primary coverage is a deterministic, engine-free structural parse of the script (no
PowerShell required, so it runs everywhere `python3 -m unittest discover` runs). A second,
optional test additionally executes the real script under `pwsh` with a stub `dotnet` on
PATH when `pwsh` is available on this host -- exercising the actual propagation semantics,
not just the source shape.
"""

import os
import re
import shutil
import stat
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[2] / "DevTests" / "test.ps1"

# The script frames each licensed leg's restore+run pair through one reusable block, keyed
# by a leg name used in both the ALL GREEN-independent failure marker and (implicitly) the
# project passed to dotnet. This is deliberately structural, not a literal function-name
# pin, so a rename/refactor of the block doesn't defeat the regression -- only removing the
# per-invocation exit-code check or the leg-naming marker does.
DOTNET_CALL = re.compile(r"&\s*\$dotnet\s+(restore|run)\b[^\n]*", re.MULTILINE)
EXIT_CHECK_BLOCK = re.compile(
    r"if\s*\(\s*\$LASTEXITCODE\s+-ne\s+0\s*\)\s*\{([^}]*)\}", re.MULTILINE | re.DOTALL
)
LEG_FAILURE_MARKER = re.compile(r"LICENSED_LEG_FAILED=")
EXIT_LASTEXITCODE = re.compile(r"exit\s+\$LASTEXITCODE")
# Accepts a -LegName argument written as a single-quoted literal ('x'), a
# double-quoted literal ("x"), or a bare unquoted identifier (x) -- PowerShell
# accepts all three for a static string argument, so a regex pinned to only one
# quoting style would pass or fail this regression for reasons that have nothing
# to do with #134's actual propagation defect.
LEG_NAME_ARG = re.compile(
    r"-LegName\s+(?:'([^']+)'|\"([^\"]+)\"|(\S+))"
)


def read_script(path=SCRIPT):
    return path.read_text(encoding="utf-8")


def dotnet_invocations_without_named_leg_propagation(text):
    """Return one problem string per `& $dotnet (restore|run)` call that is not immediately
    followed by an `if ($LASTEXITCODE -ne 0) { ... }` block naming the failing leg and
    exiting with that same code. Empty means every invocation propagates correctly."""
    problems = []
    calls = list(DOTNET_CALL.finditer(text))
    if len(calls) < 2:
        problems.append(
            "expected at least two `& $dotnet` invocations (restore/run per leg)"
        )
        return problems
    for call in calls:
        tail = text[call.end() : call.end() + 400]
        block_match = EXIT_CHECK_BLOCK.search(tail)
        if block_match is None or block_match.start() > 40:
            problems.append(
                "no immediate `if ($LASTEXITCODE -ne 0) { ... }` guard after: "
                + call.group(0).strip()
            )
            continue
        block = block_match.group(1)
        if not LEG_FAILURE_MARKER.search(block):
            problems.append(
                "exit-code guard after `"
                + call.group(0).strip()
                + "` does not emit a LICENSED_LEG_FAILED= marker"
            )
        if not EXIT_LASTEXITCODE.search(block):
            problems.append(
                "exit-code guard after `"
                + call.group(0).strip()
                + "` does not `exit $LASTEXITCODE`"
            )
    return problems


def extract_leg_names(text):
    """Every -LegName argument in `text`, quote-agnostic: 'x', "x", or a bare x all
    resolve to the same string "x" -- PowerShell itself treats all three identically
    for a static-literal argument, so the regression must not be narrower than the
    language it is pinning."""
    names = []
    for match in LEG_NAME_ARG.finditer(text):
        single, double, bare = match.groups()
        names.append(single if single is not None else double if double is not None else bare)
    return names


class TestPs1ExitPropagationStructureTest(unittest.TestCase):
    def test_every_dotnet_invocation_propagates_a_named_leg_failure(self):
        problems = dotnet_invocations_without_named_leg_propagation(read_script())
        self.assertEqual([], problems)

    def test_two_distinct_legs_are_named_in_failure_markers(self):
        # Every LICENSED_LEG_FAILED= site must reference a leg identifier, and at least two
        # distinct project references (TafTests / PortableTests projects) must exist so a
        # single shared leg name can't quietly cover both.
        text = read_script()
        self.assertIn("TafTests.csproj", text)
        self.assertIn("PortableTests.csproj", text)
        leg_names = extract_leg_names(text)
        self.assertEqual(2, len(leg_names), "expected exactly two leg invocations")
        self.assertEqual(2, len(set(leg_names)), "leg names must be distinct")

    def test_leg_name_regex_accepts_single_quoted_static_name(self):
        self.assertEqual(["taf"], extract_leg_names("Invoke-LicensedLeg -LegName 'taf' -Project $p"))

    def test_leg_name_regex_accepts_double_quoted_static_name(self):
        self.assertEqual(["taf"], extract_leg_names('Invoke-LicensedLeg -LegName "taf" -Project $p'))

    def test_leg_name_regex_accepts_bare_unquoted_static_name(self):
        self.assertEqual(["taf"], extract_leg_names("Invoke-LicensedLeg -LegName taf -Project $p"))

    def test_leg_name_regex_still_flags_two_identical_leg_names(self):
        # Mixing quoting styles for a duplicate name must not disguise it as distinct:
        # the whole point of the exact-two-distinct-names check is defeated if it can't
        # see through quoting to compare the underlying strings.
        text = (
            'Invoke-LicensedLeg -LegName \'taf\' -Project $a\n'
            'Invoke-LicensedLeg -LegName "taf" -Project $b\n'
        )
        leg_names = extract_leg_names(text)
        self.assertEqual(2, len(leg_names))
        self.assertEqual(1, len(set(leg_names)), "duplicate leg names must collapse to one")

    def test_script_ends_with_an_explicit_success_exit(self):
        text = read_script().rstrip()
        self.assertTrue(
            text.endswith("exit 0"),
            "script must end with an explicit exit 0 once both legs pass",
        )

    def test_mutation_old_unnamed_propagation_is_caught(self):
        # The pre-#134 script propagated $LASTEXITCODE correctly in the reproduced failure
        # (confirmed natively: dotnet run's own exit code was already non-zero when the
        # launch target was missing) but named no failing leg, so a caller had to infer
        # which leg failed by counting ALL GREEN lines. This mutant reproduces that shape
        # exactly and must fail the structural regression above.
        old_script = """# Engine-free pure/source-contract and portable-kernel suites using the locked NUnit package.
$fullProject = Join-Path $PSScriptRoot "TafTests.csproj"
$portableProject = Join-Path $PSScriptRoot "PortableTests.csproj"
$dotnet = (Get-Command dotnet -CommandType Application).Source
& $dotnet restore $fullProject --locked-mode -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project $fullProject --no-restore -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet restore $portableProject --locked-mode -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project $portableProject --no-restore -v q --nologo
exit $LASTEXITCODE
"""
        problems = dotnet_invocations_without_named_leg_propagation(old_script)
        self.assertNotEqual([], problems, "mutation was not caught: old script passed")
        self.assertTrue(any("LICENSED_LEG_FAILED" in p for p in problems))


@unittest.skipUnless(shutil.which("pwsh"), "pwsh is not installed on this host")
class TestPs1RealExecutionTest(unittest.TestCase):
    """Executes the real script under pwsh with a stub dotnet, proving the propagation
    end-to-end (not just in source shape) for all four restore/run call points across both
    legs: taf-restore, taf-run, portable-restore, portable-run.

    The stub is keyed purely by call ORDER (1: taf restore, 2: taf run, 3: portable
    restore, 4: portable run), not by parsing dotnet's argument text -- test.ps1 always
    calls restore-then-run for taf, then restore-then-run for portable, in that fixed
    order, so a counter file is a strictly more robust discriminator than pattern-matching
    reconstructed argv text, which is sensitive to how a given shell/host quotes args (this
    project's own first attempt at argument-text matching was wrong; see git history)."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-test-ps1-exit-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.devtests = self.root / "DevTests"
        self.devtests.mkdir()
        (self.devtests / "test.ps1").write_text(read_script(), encoding="utf-8")
        (self.devtests / "TafTests.csproj").write_text("<Project />", encoding="utf-8")
        (self.devtests / "PortableTests.csproj").write_text(
            "<Project />", encoding="utf-8"
        )
        # Scope PATH to exactly [stub_dir, pwsh's own directory]: if a real `dotnet` is
        # ALSO reachable on PATH (as it typically is in CI), `Get-Command dotnet
        # -CommandType Application` returns every match, and PowerShell's member
        # enumeration on the resulting array silently turns `.Source` into a
        # space-joined, unusable multi-path string -- test.ps1 then fails at the very
        # first `& $dotnet restore ...` with a "term not recognized" parser error that
        # never touches $LASTEXITCODE at all. That is a real fixture-PATH pitfall, not
        # the behaviour under test, so exactly one `dotnet` must be resolvable here.
        pwsh_path = shutil.which("pwsh")
        pwsh_dir = str(Path(pwsh_path).resolve().parent) if pwsh_path else ""
        self.env = dict(os.environ)
        self.env["PATH"] = pwsh_dir
        self.env.pop("TAF_TEST_FILTER", None)

    def write_dotnet_stub(self, stub_dir, cases_posix, cases_cmd):
        """Writes the SAME call-numbered dispatch as both a POSIX shell script named
        `dotnet` and a Windows `dotnet.cmd` batch file, so `pwsh` resolves exactly one
        Application named `dotnet` on either host: on POSIX, `Get-Command dotnet
        -CommandType Application` finds the executable-bit-set extensionless file; on
        Windows, PATHEXT resolution finds `dotnet.cmd` (an extensionless file is never an
        Application there, which is exactly PRRT_kwDOT9Vt7c6hA-dj). Both bodies encode
        the identical fail_at/fail_message/fail_code dispatch, generated from one shared
        case list, so the two platforms can never silently drift apart. The batch file
        dispatches via `goto`, one label per call number, rather than parenthesised
        `if (...)` blocks: a stub message containing its own literal parentheses (the
        real ALL GREEN text does) breaks cmd.exe's block-nesting parser, so a goto/label
        dispatch is the only shape that stays correct for arbitrary message text."""
        stub_dir.mkdir(exist_ok=True)
        posix_stub = stub_dir / "dotnet"
        posix_stub.write_text(
            "#!/bin/sh\n"
            'read -r n < "$TAF_STUB_COUNTER"\n'
            "n=$((n + 1))\n"
            'echo "$n" > "$TAF_STUB_COUNTER"\n'
            'case "$n" in\n'
            + "\n".join(cases_posix)
            + "\n  *) exit 0 ;;\nesac\n",
            encoding="utf-8",
        )
        posix_stub.chmod(posix_stub.stat().st_mode | stat.S_IEXEC | stat.S_IXGRP | stat.S_IXOTH)
        cmd_stub = stub_dir / "dotnet.cmd"
        gotos = "\r\n".join("if %%N%%==%d goto n%d" % (n, n) for n in (1, 2, 3, 4))
        labels = "".join(
            ":n%d\r\n%s\r\n" % (n, body) for n, body in cases_cmd
        )
        cmd_stub.write_text(
            "@echo off\r\n"
            'set /p N=<"%TAF_STUB_COUNTER%"\r\n'
            "set /a N=%N%+1\r\n"
            'echo %N% >"%TAF_STUB_COUNTER%"\r\n'
            + gotos + "\r\n"
            + "exit /b 0\r\n"
            + labels,
            encoding="utf-8",
        )

    def make_stub(self, fail_at, fail_message, fail_code):
        """A dotnet stub that succeeds every call except call number `fail_at` (1-4, in
        test.ps1's fixed restore/run-per-leg order), which prints `fail_message` and exits
        `fail_code`. Calls before the failure that are `run` steps print ALL GREEN, exactly
        like a real successful suite run, so a passing leg's marker is distinguishable from
        the failing one's."""
        counter_file = self.root / "call-count"
        counter_file.write_text("0", encoding="utf-8")
        stub_dir = self.root / "stub-bin"
        cases_posix = []
        cases_cmd = []
        for n in (1, 2, 3, 4):
            if n == fail_at:
                cases_posix.append('  %d) echo "%s"; exit %d ;;' % (n, fail_message, fail_code))
                cases_cmd.append((n, "echo %s\r\nexit /b %d" % (fail_message, fail_code)))
            elif n in (2, 4):
                cases_posix.append(
                    '  %d) echo "ALL GREEN: 1 cases passed, 0 skipped (1 discovered)"; exit 0 ;;'
                    % n
                )
                cases_cmd.append(
                    (n, "echo ALL GREEN: 1 cases passed, 0 skipped (1 discovered)\r\nexit /b 0")
                )
            else:
                cases_posix.append("  %d) exit 0 ;;" % n)
                cases_cmd.append((n, "exit /b 0"))
        self.write_dotnet_stub(stub_dir, cases_posix, cases_cmd)
        self.env["PATH"] = str(stub_dir) + os.pathsep + self.env["PATH"]
        self.env["TAF_STUB_COUNTER"] = str(counter_file)

    def run_script(self):
        return subprocess.run(
            ["pwsh", "-NoProfile", "-File", str(self.devtests / "test.ps1")],
            cwd=self.devtests,
            env=self.env,
            capture_output=True,
            text=True,
            timeout=30,
        )

    def assert_leg_failure(self, fail_at, fail_message, fail_code, expected_marker):
        self.make_stub(fail_at, fail_message, fail_code)
        result = self.run_script()
        diagnostic = (
            "returncode=" + str(result.returncode)
            + "\n--- stdout ---\n" + result.stdout
            + "\n--- stderr ---\n" + result.stderr
        )
        self.assertNotEqual(0, result.returncode, diagnostic)
        self.assertIn(expected_marker, result.stdout, diagnostic)

    def test_taf_restore_failure_exits_non_zero_and_names_taf(self):
        self.assert_leg_failure(1, "restore failed", 41, "LICENSED_LEG_FAILED=taf rc=41 step=restore")

    def test_taf_run_failure_exits_non_zero_and_names_taf(self):
        self.assert_leg_failure(2, "ALL GREEN was a lie", 42, "LICENSED_LEG_FAILED=taf rc=42 step=run")

    def test_portable_restore_failure_exits_non_zero_and_names_portable(self):
        self.assert_leg_failure(3, "restore failed", 43, "LICENSED_LEG_FAILED=portable rc=43 step=restore")

    def test_portable_run_failure_exits_non_zero_and_names_portable(self):
        self.make_stub(4, "The application to execute does not exist: stub.dll", 74)
        result = self.run_script()
        diagnostic = (
            "returncode=" + str(result.returncode)
            + "\n--- stdout ---\n" + result.stdout
            + "\n--- stderr ---\n" + result.stderr
        )
        self.assertNotEqual(0, result.returncode, diagnostic)
        self.assertIn("LICENSED_LEG_FAILED=portable rc=74 step=run", result.stdout, diagnostic)
        self.assertIn("ALL GREEN", result.stdout, diagnostic)

    def test_both_legs_succeed_exits_zero(self):
        counter_file = self.root / "call-count"
        counter_file.write_text("0", encoding="utf-8")
        stub_dir = self.root / "stub-bin"
        cases_posix = ["  1) exit 0 ;;",
            "  2) echo \"ALL GREEN: 1 cases passed, 0 skipped (1 discovered)\"; exit 0 ;;",
            "  3) exit 0 ;;",
            "  4) echo \"ALL GREEN: 1 cases passed, 0 skipped (1 discovered)\"; exit 0 ;;"]
        all_green_cmd = "echo ALL GREEN: 1 cases passed, 0 skipped (1 discovered)\r\nexit /b 0"
        cases_cmd = [
            (1, "exit /b 0"),
            (2, all_green_cmd),
            (3, "exit /b 0"),
            (4, all_green_cmd),
        ]
        self.write_dotnet_stub(stub_dir, cases_posix, cases_cmd)
        self.env["PATH"] = str(stub_dir) + os.pathsep + self.env["PATH"]
        self.env["TAF_STUB_COUNTER"] = str(counter_file)
        result = self.run_script()
        diagnostic = (
            "returncode=" + str(result.returncode)
            + "\n--- stdout ---\n" + result.stdout
            + "\n--- stderr ---\n" + result.stderr
        )
        self.assertEqual(0, result.returncode, diagnostic)
        self.assertNotIn("LICENSED_LEG_FAILED", result.stdout, diagnostic)
        self.assertEqual(2, result.stdout.count("ALL GREEN"), diagnostic)


if __name__ == "__main__":
    unittest.main()
