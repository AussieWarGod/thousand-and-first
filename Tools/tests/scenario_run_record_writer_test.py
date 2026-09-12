"""Run 46: run-scenario.ps1's Write-TafRunRecord must write UTF-8 WITHOUT a BOM.

The function is pinned as text, and - when Windows PowerShell 5.1 is reachable (powershell.exe on
a WSL host) - executed under 5.1 exactly as the runner executes it, because a PowerShell half
proved only by reading its source is not proved (house rule, 2026-09-11).
"""

import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
SCRIPT = TOOLS / "run-scenario.ps1"


def writer_function() -> str:
    text = SCRIPT.read_text(encoding="utf-8")
    found = re.search(r"function Write-TafRunRecord \{.*?\n\}\n", text, re.S)
    if not found:
        raise AssertionError("Write-TafRunRecord not found in run-scenario.ps1")
    return found.group(0)


class RunRecordWriterTest(unittest.TestCase):
    def test_the_writer_names_a_bom_free_utf8_encoding_and_never_set_content_utf8(self):
        body = writer_function()
        self.assertIn("New-Object Text.UTF8Encoding($false)", body)
        self.assertIn("[IO.File]::WriteAllText($partial, $json,", body)
        self.assertNotIn("Set-Content", body)
        self.assertNotIn("Out-File", body)

    def test_windows_powershell_5_1_writes_the_record_without_a_bom(self):
        exe = shutil.which("powershell.exe")
        if exe is None:
            self.skipTest("powershell.exe (Windows PowerShell 5.1) is not reachable")
        with tempfile.TemporaryDirectory() as tmp:
            script = Path(tmp) / "writer.ps1"
            script.write_text(
                "$ErrorActionPreference = 'Stop'\n" + writer_function()
                + "\n$target = Join-Path $env:TEMP ('taf-bom-' + [Guid]::NewGuid().ToString('N') + '.json')\n"
                "Write-TafRunRecord -Path $target -Record @{ schemaVersion = 1; profileName = 'taf-scenario.Test' }\n"
                "$bytes = [IO.File]::ReadAllBytes($target)\n"
                "Remove-Item -LiteralPath $target -Force\n"
                "Write-Output ($PSVersionTable.PSVersion.Major.ToString() + ' ' + "
                "([BitConverter]::ToString($bytes[0..2])))\n",
                encoding="utf-8")
            windows_path = subprocess.run(["wslpath", "-w", str(script)], capture_output=True,
                                          text=True, check=True).stdout.strip()
            result = subprocess.run(
                [exe, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", windows_path],
                capture_output=True, text=True, timeout=120)
        self.assertEqual(result.returncode, 0, result.stderr)
        major, first_bytes = result.stdout.strip().split(" ", 1)
        self.assertEqual(major, "5", "the runner executes under Windows PowerShell 5.1")
        self.assertNotEqual(first_bytes, "EF-BB-BF", "a UTF-8 BOM was written")
        self.assertEqual(first_bytes[:2], "7B", "the record must open with '{'")


if __name__ == "__main__":
    unittest.main()
