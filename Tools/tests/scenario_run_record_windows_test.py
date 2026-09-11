"""Execute the actual launcher helpers, not a source-string approximation.

Windows PowerShell 5.1 is the WSL launcher's default host; a PowerShell 7-only API
must fail this test before a sealed game profile is launched.
"""
from pathlib import Path
import os
import shutil
import subprocess
import unittest


class ScenarioRunRecordWindowsTest(unittest.TestCase):
    @unittest.skipUnless(shutil.which("powershell.exe"), "Windows PowerShell is unavailable")
    def test_actual_launcher_record_helpers_round_trip_and_refuse_bad_shapes(self):
        script = Path(__file__).resolve().parents[1] / "test-scenario-run-record.ps1"
        path = str(script)
        if os.name != "nt":
            self.assertIsNotNone(shutil.which("wslpath"), "Windows host needs a WSL path translator")
            path = subprocess.run(
                ["wslpath", "-w", path], capture_output=True, text=True,
                check=True, timeout=10,
            ).stdout.strip()
        completed = subprocess.run(
            [shutil.which("powershell.exe"), "-NoProfile", "-ExecutionPolicy", "Bypass",
             "-File", path], capture_output=True, text=True, timeout=60,
        )
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertIn("SCENARIO RUN RECORD HELPERS CLEAN", completed.stdout)


if __name__ == "__main__":
    unittest.main()
