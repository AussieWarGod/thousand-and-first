"""Exercise the focused runner's command and failure boundaries with harmless executables."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


class DevelopmentCheckTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        (self.root / "Tools/tests").mkdir(parents=True)
        self.script = self.root / "Tools/dev-check.sh"
        shutil.copyfile(Path(__file__).resolve().parents[1] / "dev-check.sh", self.script)
        self.fake = self.root / "dotnet"
        self.fake.write_text("#!" + sys.executable + "\n" + '''
import json, os, sys
with open(os.environ["COMMAND_LOG"], "a") as log:
    log.write(json.dumps({"args": sys.argv[1:], "filter": os.getenv("TAF_TEST_FILTER"),
        "forbid": os.getenv("TAF_FORBID_SKIPS"), "allowed": os.getenv("TAF_ALLOWED_SKIPS"),
        "repo": os.getenv("TAF_REPO_ROOT")}) + "\\n")
if sys.argv[1:] == ["--version"]:
    print(os.getenv("FAKE_SDK", "9.0.306"))
elif sys.argv[1] == os.getenv("FAIL_STEP"):
    sys.exit(19)
''')
        self.fake.chmod(0o755)
        self.log = self.root / "commands.jsonl"

    def run_check(self, *args, **extra):
        env = dict(os.environ, TAF_DOTNET=str(self.fake), COMMAND_LOG=str(self.log),
                   TAF_TEST_FILTER="ambient", TAF_ALLOWED_SKIPS="ambient")
        env.update(extra)
        return subprocess.run(["bash", str(self.script), *args], env=env,
                              text=True, capture_output=True)

    def calls(self):
        return [json.loads(row) for row in self.log.read_text().splitlines()]

    def test_focused_legs_restore_then_run_with_explicit_zero_skip_filter(self):
        for mode, project in (("main", "TafTests"), ("portable", "PortableTests")):
            with self.subTest(mode=mode):
                if self.log.exists():
                    self.log.unlink()
                result = self.run_check(mode, "KingdomQuickstart")
                self.assertEqual(result.returncode, 0, result.stderr)
                calls = self.calls()
                self.assertEqual([row["args"][0] for row in calls], ["--version", "restore", "run"])
                self.assertIn(f"DevTests/{project}.csproj", calls[1]["args"])
                self.assertIn("--locked-mode", calls[1]["args"])
                self.assertIn("--no-restore", calls[2]["args"])
                self.assertEqual(calls[2]["filter"], "KingdomQuickstart")
                self.assertEqual(calls[2]["forbid"], "1")
                self.assertIsNone(calls[2]["allowed"])
                self.assertEqual(calls[2]["repo"], str(self.root))
                self.assertIn("not release acceptance", result.stdout)

    def test_restore_failure_prevents_run(self):
        result = self.run_check("main", "Fixture", FAIL_STEP="restore")
        self.assertEqual(result.returncode, 19)
        self.assertEqual([row["args"][0] for row in self.calls()], ["--version", "restore"])
        self.assertIn("exit=19", result.stdout)

    def test_test_failure_propagates(self):
        result = self.run_check("portable", "Fixture", FAIL_STEP="run")
        self.assertEqual(result.returncode, 19)
        self.assertIn("exit=19", result.stdout)

    def test_wrong_sdk_runs_no_tests(self):
        self.assertEqual(self.run_check("main", "Fixture", FAKE_SDK="8.0.100").returncode, 2)
        self.assertEqual(len(self.calls()), 1)

    def test_invalid_selection_never_invokes_dotnet(self):
        for args in ((), ("main",), ("main", " "), ("main", "X", "Y"),
                     ("tools", "missing_test.py"), ("tools", "../escape_test.py"),
                     ("docs", "extra"), ("unknown",)):
            with self.subTest(args=args):
                self.assertEqual(self.run_check(*args).returncode, 2)
                self.assertFalse(self.log.exists())
