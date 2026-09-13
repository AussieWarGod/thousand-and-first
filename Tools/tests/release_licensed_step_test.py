"""Execute the real source-test stage against a harmless runner at its external boundary."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ReleaseLicensedStepTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "Tools").mkdir()
        self.log = self.root / "call.json"
        stub = self.root / "Tools/dev-check.sh"
        stub.write_text("#!" + sys.executable + "\n" + '''
import json, os, sys
with open(os.environ["CALL_LOG"], "w") as log:
    json.dump({"args": sys.argv[1:], "base": os.getenv("TAF_QUD_BASE"),
               "dotnet": os.getenv("TAF_DOTNET"), "filter": os.getenv("TAF_TEST_FILTER")}, log)
sys.exit(int(os.getenv("RUNNER_EXIT", "0")))
''')
        stub.chmod(0o755)
        source = (ROOT / "Tools/release-check.sh").read_text()
        start = source.index('echo "[5/11] pure and source-contract tests"')
        end = source.index('echo "[6/11] XML and tile reachability"', start)
        self.stage = source[start:end]

    def run_stage(self, code=0, **extra):
        env = dict(os.environ, REPO=str(self.root), BASE=str(self.root / "licensed Base"),
                   CALL_LOG=str(self.log), RUNNER_EXIT=str(code), TAF_QUD_BASE="ambient-wrong-base")
        env.pop("TAF_DOTNET", None)
        env.update(extra)
        return subprocess.run(["bash", "-euc", self.stage + '\necho NEXT_STAGE\n'],
                              env=env, text=True, capture_output=True)

    def test_runs_complete_shared_lane_with_configured_licensed_base(self):
        result = self.run_stage(TAF_DOTNET="/selected/sdk/dotnet")
        self.assertEqual(result.returncode, 0, result.stderr)
        call = json.loads(self.log.read_text())
        self.assertEqual(call["args"], ["licensed"])
        self.assertEqual(call["base"], str(self.root / "licensed Base"))
        self.assertEqual(call["dotnet"], "/selected/sdk/dotnet")
        self.assertIn("NEXT_STAGE", result.stdout)

    def test_failure_prevents_later_release_stages(self):
        result = self.run_stage(154)
        self.assertEqual(result.returncode, 154)
        self.assertNotIn("NEXT_STAGE", result.stdout)

    def test_default_sdk_is_native_home_install(self):
        self.assertEqual(self.run_stage().returncode, 0)
        self.assertEqual(json.loads(self.log.read_text())["dotnet"],
                         str(Path.home() / ".dotnet/dotnet"))

    def test_ambient_filter_reaches_shared_lane_refusal(self):
        self.assertEqual(self.run_stage(2, TAF_TEST_FILTER="unwanted-subset").returncode, 2)
        self.assertEqual(json.loads(self.log.read_text())["filter"], "unwanted-subset")
