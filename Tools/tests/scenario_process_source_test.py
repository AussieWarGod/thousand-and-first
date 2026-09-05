"""Source-only contracts for scenario process ownership and its callers.

These checks do not launch processes or establish Windows behavior. Real PID, argv, refusal,
and retained-fixture assertions execute separately in Tools/test-scenario-process.ps1.
"""

from __future__ import annotations

import pathlib
import re
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


def read(name: str) -> str:
    return (ROOT / "Tools" / name).read_text(encoding="utf-8")


def flat(source: str) -> str:
    return re.sub(r"\s+", " ", source)


def section(source: str, start: str, end: str) -> str:
    begin = source.index(start)
    return source[begin:source.index(end, begin + len(start))]


class ScenarioProcessSourceTest(unittest.TestCase):
    def ordered(self, source: str, *tokens: str) -> None:
        cursor = 0
        for token in tokens:
            position = source.find(token, cursor)
            self.assertGreaterEqual(position, cursor, token)
            cursor = position + len(token)

    def test_scripted_launcher_uses_owned_start_after_closed_seal_and_idle_refusal(self):
        source = read("run-scenario.ps1")
        self.ordered(
            source,
            "Assert-ClosedSeal -TreeRoot $localRoot -SealPath $profileSeal",
            ". (Join-Path $PSScriptRoot 'scenario-process.ps1')",
            "Assert-TafScenarioIdle -Game $Game",
            "$process = Start-TafOwnedScenarioProcess -Root $rootPath -Game $Game",
            "$handedOff = $false",
            "$handedOff = $true",
            "if (-not $handedOff -and -not $process.HasExited)",
            "$process.Kill()",
            "$process.Dispose()",
        )
        self.assertNotIn("Start-Process", source)

    def test_receipt_precedes_launch_and_binds_native_handle_metadata(self):
        source = read("scenario-process.ps1")
        start = section(source, "function Start-TafOwnedScenarioProcess", "function Get-TafOwnedScenarioProcess")
        self.ordered(
            start,
            'if (Test-Path -LiteralPath $receipt) { throw "Ownership receipt already exists:',
            "::Executable($Game)",
            "::ExpectedArguments($Root, $gamePath)",
            "$process = Start-Process",
            "$null = $process.Handle",
            "Get-TafScenarioIdentity -Process $process",
            "::ValidIdentity($Root, $gamePath, $identity)",
            "Write-TafScenarioReceipt -Root $Root -Identity $identity",
            "Read-TafScenarioReceipt -Root $Root -Game $gamePath",
            "::Decide(",
            "$published = $true",
            "return $process",
        )
        identity = flat(section(source, "function Get-TafScenarioIdentity", "function Get-TafScenarioReceiptPath"))
        self.ordered(identity, "$null = $Process.Handle", "Get-CimInstance Win32_Process", "$Process.HasExited")
        self.assertIn("$Process.StartTime.ToUniversalTime().Ticks", identity)
        self.assertIn("$Process.MainModule.FileName", identity)
        self.assertIn("$information.CommandLine", identity)
        native = read("ScenarioProcessWindows.cs")
        self.assertIn('DllImport("shell32.dll"', native)
        self.assertIn("IntPtr block = CommandLineToArgvW(CommandLine, out count);", native)
        self.assertIn("finally { LocalFree(block); }", native)
        self.assertIn("GetFinalPathNameByHandle(file.SafeFileHandle.DangerousGetHandle()", native)

    def test_receipt_is_create_new_canonical_and_not_merely_a_pid_file(self):
        source = read("scenario-process.ps1")
        write = section(source, "function Write-TafScenarioReceipt", "function Read-TafScenarioReceipt")
        self.assertIn("[IO.FileMode]::CreateNew", write)
        self.assertIn("[IO.FileShare]::None", write)
        readback = flat(section(source, "function Read-TafScenarioReceipt", "function Start-TafOwnedScenarioProcess"))
        self.assertIn("'schema,root,pid,startTicks,executable,arguments'", readback)
        self.assertIn("$text -cne ($record | ConvertTo-Json -Compress)", readback)
        self.assertIn("[IO.FileAttributes]::ReparsePoint", readback)
        self.assertIn("$file.Length -gt 16384", readback)
        self.assertIn("$record.arguments -isnot [Array]", readback)
        self.assertIn("::ValidIdentity($Root, $Game, $identity)", readback)

    def test_stop_uses_only_revalidated_held_process_and_preserves_receipt(self):
        source = read("scenario-process.ps1")
        lookup = section(source, "function Get-TafOwnedScenarioProcess", "function Stop-TafOwnedScenarioProcess")
        self.ordered(lookup, "Read-TafScenarioReceipt", "::GetProcessById($recorded.Pid)",
                     "Get-TafScenarioIdentity -Process $process", "::Decide(", "return $process")
        stop = source[source.index("function Stop-TafOwnedScenarioProcess"):]
        self.ordered(stop, "Get-TafOwnedScenarioProcess -Root $Root -Game $Game",
                     'if ($null -eq $process) { return "ALREADY_EXITED',
                     "$process.Kill()", "$process.WaitForExit(10000)", "$process.Dispose()")
        self.assertNotIn("Get-Process", stop)
        self.assertNotRegex(stop, r"WriteAll|Set-Content|Remove-Item|\.Delete\(")
        idle = section(source, "function Assert-TafScenarioIdle", "function Get-TafScenarioIdentity")
        self.assertIn("if ($existing.Count -ne 0)", idle)
        self.assertIn("refusing launch without stopping it", idle)
        self.assertNotIn(".Kill(", idle)

    def test_runner_scopes_stop_and_capture_and_preserves_profiles_on_failure(self):
        source = read("run-personas.sh")
        stop = section(source, "stop_owned() {", "on_exit() {")
        self.ordered(stop, '[ -n "$ACTIVE_ROOT" ] && [ "$ACTIVE_LAUNCH" = 1 ] || return 0',
                     '-Mode stop', '-Root "$(wslpath -w "$ACTIVE_ROOT")"',
                     '-Game "$(wslpath -w "$GAME")"', "VERDICT=FAIL", "LIFECYCLE_BROKEN=1")
        self.assertIn("trap 'on_exit $?' EXIT", source)
        run = section(source, "run_persona() {", "# ---- the matrix")
        self.ordered(run, 'if [ "$LIFECYCLE_BROKEN" = 1 ]', '-Mode idle',
                     'root="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"', 'ACTIVE_ROOT="$root"',
                     'ACTIVE_LAUNCH=1', '-File "$(wslpath -w "$LAUNCHER")"')
        capture = run[run.index('-File "$(wslpath -w "$CAPTURE")"'):]
        self.ordered(capture, '-ScenarioRoot "$(wslpath -w "$root")"',
                     '-ScenarioGame "$(wslpath -w "$GAME")"', '-Output ', "\n\tstop_owned")
        self.assertIn('profile=$root (retained)', run)

    def test_owned_capture_cannot_fall_back_to_an_unrelated_named_window(self):
        source = flat(read("capture-game-window.ps1"))
        self.assertIn("[Parameter(Mandatory = $true)][string]$ScenarioRoot", source)
        self.assertIn("[Parameter(Mandatory = $true)][string]$ScenarioGame", source)
        self.assertIn("$process = Get-TafOwnedScenarioProcess -Root $ScenarioRoot -Game $ScenarioGame if ($null -eq $process) { throw", source)
        self.assertNotIn("Get-Process", source)
        self.assertNotIn("$ProcessName", source)
        self.assertIn("$windowOwner -ne $process.Id", source)
        self.ordered(source, "$handle = Get-CaptureHandle", "::SetWindowPos(",
                     "$handle = Get-CaptureHandle", "::GetWindowRect(",
                     "if ((Get-CaptureHandle) -ne $handle)", "::PrintWindow(",
                     "if ((Get-CaptureHandle) -ne $handle)", "$bitmap.Save(")
        self.assertIn("if ($null -ne $process) { $process.Dispose() }", source)

    def test_scenario_lifecycle_has_no_name_kill_or_recursive_profile_wipe(self):
        for name in ("run-scenario.ps1", "scenario-process.ps1", "scenario-process-control.ps1",
                     "run-personas.sh", "capture-game-window.ps1", "test-scenario-process.ps1"):
            with self.subTest(name=name):
                source = read(name)
                self.assertNotRegex(source, r"(?i)\b(?:Stop-Process|taskkill|pkill|killall)\b")
                self.assertNotRegex(source, r"(?m)^\s*kill\s")
                self.assertNotRegex(source, r"\brm(?:\s+-[^\s]+)*\s+-[A-Za-z]*[rR]")
                self.assertNotRegex(source, r"(?i)\bRemove-Item\b|\[IO\.Directory\]::Delete")

    def test_windows_probe_is_real_and_limits_cleanup_to_direct_fixture_handles(self):
        source = read("test-scenario-process.ps1")
        self.assertIn("'TafScenarioProbe' + [guid]::NewGuid().ToString('N')", source)
        self.assertIn("System.Threading.Thread.Sleep(600000)", source)
        self.assertIn("-OutputType WindowsApplication", source)
        self.assertIn("$owned = Start-TafOwnedScenarioProcess -Root $rootA -Game $game", source)
        self.assertIn("$decoy = Start-Process -FilePath $game", source)
        self.assertIn("Get-TafScenarioIdentity -Process $pair[1]", source)
        self.assertIn("$identity.Arguments[$i] -ceq $expected[$i]", source)
        self.assertIn("foreach ($index in @(0, 2, 4, 6, 8))", source)
        self.assertIn("finally { [IO.File]::WriteAllBytes($receiptA, $authBytes) }", source)
        cleanup = source[source.index("$cleanupFailures ="):]
        self.ordered(cleanup, "foreach ($probe in $script:HeldProbes)",
                     "$probe.HasExited", "$probe.Kill()", "$probe.WaitForExit(10000)", "$probe.Dispose()")
        self.assertNotRegex(cleanup, r"GetProcessById|Get-Process")
        self.assertIn("$script:ProbeCases -eq 17 -and $script:ProbePassed -eq 17", source)
        self.assertIn("game-launched=false; fixtures-retained=true", source)


if __name__ == "__main__":
    unittest.main()
