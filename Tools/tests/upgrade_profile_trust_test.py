"""Source contracts and extracted grammar examples, NOT PowerShell/native execution.

These do not prove Windows leases, process exclusion, copies, or save compatibility.
The operator must run actual Windows adversarial fixtures separately.
"""

from __future__ import annotations

import json
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
PS = ROOT / "Tools" / "upgrade-profile-trust.ps1"
CS = ROOT / "Tools" / "UpgradeProfileTrust.cs"


def literal(name):
    source = PS.read_text(encoding="utf-8")
    match = re.search(r"^\s*\$" + name + r" = '((?:[^']|'')*)'$", source, re.M)
    if match is None:
        raise AssertionError("actual PowerShell token missing: " + name)
    return match.group(1).replace("''", "'")


def plan_pattern():
    """Python regex mirrors only the source-pinned compact shape concatenation."""
    string = literal("stringToken")
    integer = literal("integerToken")
    strings = r"\[(?:" + string + "(?:," + string + r")*)?\]"
    row = r'\{"path":' + string + ',"size":' + integer + ',"sha256":' + string + r"\}"
    rows = r"\[(?:" + row + "(?:," + row + r")*)?\]"
    return re.compile(r'\A\{"schema":' + string + ',"source":' + string
                      + ',"destination":(?:null|' + string + '),"roots":' + strings
                      + ',"files":' + rows + ',"directories":' + strings
                      + ',"selected":' + strings + r"\}\Z")


def example(**updates):
    value = dict(schema="taf-upgrade-copy-v1", source=r"C:\taf-scenario.Source1",
                 destination=None, roots=["Synced"], files=[
                     dict(path="Synced/HighScores.json", size=0, sha256="0" * 64)],
                 directories=["Synced"], selected=[])
    value.update(updates)
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


class UpgradeProfileTrustGrammarTests(unittest.TestCase):
    def test_actual_tokens_bound_to_complete_fixed_order_shape(self):
        source = PS.read_text(encoding="utf-8")
        self.assertIn("$strings = '\\[(?:' + $stringToken + '(?:,' + $stringToken + ')*)?\\]'", source)
        self.assertIn("$row = '\\{\"path\":' + $stringToken + ',\"size\":' + $integerToken + ',\"sha256\":' + $stringToken + '\\}'", source)
        self.assertIn("$rows = '\\[(?:' + $row + '(?:,' + $row + ')*)?\\]'", source)
        self.assertIn("$planPattern = '\\A\\{\"schema\":' + $stringToken + ',\"source\":' + $stringToken + ',\"destination\":(?:null|' + $stringToken + '),\"roots\":' + $strings + ',\"files\":' + $rows + ',\"directories\":' + $strings + ',\"selected\":' + $strings + '\\}\\z'", source)
        self.assertLess(source.index("[regex]::IsMatch"), source.index("ConvertFrom-Json -InputObject"))
        self.assertIn("[TimeSpan]::FromSeconds(2)", source)

    def test_compact_plan_shapes_unicode_and_empty_arrays(self):
        for wire in (example(), example(files=[]), example(roots=["Local", "Synced"]),
                     example(destination=r"C:\taf-scenario.New1"),
                     example(files=[dict(path="Synced/雪 é.txt", size=14, sha256="a" * 64)])):
            with self.subTest(wire=wire):
                self.assertIsNotNone(plan_pattern().fullmatch(wire))

    def test_duplicate_unknown_reordered_keys_refuse_before_json(self):
        wire = example()
        bad = (wire.replace('"schema":', '"extra":0,"schema":'),
               wire.replace('"schema":', '"source":"x","schema":'),
               wire.replace('"size":0', '"size":0,"size":1'),
               wire.replace('"size":0', '"size":0,"other":1'),
               wire.replace('"directories":["Synced"],"selected":[]',
                            '"selected":[],"directories":["Synced"]'))
        for value in bad:
            with self.subTest(value=value):
                self.assertIsNone(plan_pattern().fullmatch(value))

    def test_noncanonical_numeric_json_and_nonstring_arrays_refuse(self):
        for token in ("-1", "01", "1.0", "1e3", "true", "null", '"0"', "[]"):
            with self.subTest(token=token):
                self.assertIsNone(plan_pattern().fullmatch(example().replace('"size":0', '"size":' + token)))
        for roots in ("[null]", "[1]", '["Synced",false]', "{}"):
            with self.subTest(roots=roots):
                self.assertIsNone(plan_pattern().fullmatch(example().replace('["Synced"]', roots, 1)))

    def test_bom_whitespace_truncation_and_trailing_content_refuse(self):
        wire = example()
        for value in ("\ufeff" + wire, " " + wire, wire + "\n", wire + "{}", wire.replace('{"schema"', '{ "schema"')):
            self.assertIsNone(plan_pattern().fullmatch(value))
        for cut in range(len(wire)):
            with self.subTest(cut=cut):
                self.assertIsNone(plan_pattern().fullmatch(wire[:cut]))

    def test_standard_json_string_syntax_not_path_authority(self):
        token = re.compile(r"\A" + literal("stringToken") + r"\Z")
        for value in ('"雪"', r'"a\\b"', r'"a\"b"', r'"\u00e9"', r'"\/\b\f\n\r\t"'):
            self.assertIsNotNone(token.fullmatch(value))
        for value in ('"a\nb"', r'"\x41"', r'"\uXYZ0"', '"unclosed', '"a"b"'):
            self.assertIsNone(token.fullmatch(value))


class UpgradeProfileTrustSourceTests(unittest.TestCase):
    def setUp(self):
        self.cs = CS.read_text(encoding="utf-8")
        self.ps = PS.read_text(encoding="utf-8")

    def test_invalid_mode_refuses_before_native_loading_or_plan_access(self):
        self.assertIn("$Mode -cnotin @('Inspect', 'Copy', 'Slots')", self.ps)
        self.assertLess(self.ps.index("unsupported_mode"), self.ps.index("Add-Type -Path"))
        self.assertLess(self.ps.index("Add-Type -Path"), self.ps.index("$session = New-Object"))

    def test_helper_stdout_utf8_pinned_before_native_work_not_profile_writes(self):
        self.assertIn("[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false, $true)", self.ps)
        self.assertLess(self.ps.index("[Console]::OutputEncoding"), self.ps.index("Add-Type -Path"))
        self.assertIn("no continuing custody after kill", self.ps)
        self.assertNotIn("Set-Content", self.ps)
        self.assertNotIn("WriteAllText", self.ps)

    def test_plan_bytes_held_strict_utf8_and_rehashed(self):
        self.assertIn("Plan = Hold(parent.Physical", self.cs)
        self.assertIn("Plan.Stream.Length <= 16777216", self.cs)
        self.assertIn("PlanHash = Hash(Plan, Plan.Stream.Length)", self.cs)
        self.assertIn("new UTF8Encoding(false, true).GetString(bytes)", self.cs)
        self.assertIn('Require(Hash(Plan, Plan.Stream.Length) == PlanHash, "plan_changed")', self.cs)
        self.assertNotIn("Get-Content", self.ps)

    def test_real_read_access_no_write_delete_sharing_and_no_follow(self):
        self.assertIn("create ? 0xc0000000u : 0x80000000u, create ? 0u : 1u", self.cs)
        self.assertIn("create ? 1u : 3u, 0x00200000u | (directory ? 0x02000000u : 0u)", self.cs)
        self.assertIn("GetFileType(handle) == 1", self.cs)
        self.assertIn("lease.Original.Attributes & (0x400u | 0x40u)", self.cs)
        self.assertIn("directory || lease.Original.Links == 1", self.cs)
        self.assertIn('Require(info.Links == 1, "hardlink_changed")', self.cs)

    def test_full_identity_and_normalized_volume_guid_no_unc_fallback(self):
        self.assertIn("GetFileInformationByHandleEx(handle, 18, out value, 24)", self.cs)
        self.assertIn("GetFinalPathNameByHandleW(handle, value, 32768, 1)", self.cs)
        self.assertIn("id.Volume == lease.Id.Volume && id.Low == lease.Id.Low && id.High == lease.Id.High", self.cs)
        self.assertIn("Physical(lease.Handle) == lease.Physical", self.cs)
        self.assertIn('"directory_alias"', self.cs)
        self.assertIn('"volume_guid_required"', self.cs)

    def test_only_scenario_roots_and_declared_complete_subtrees(self):
        self.assertIn(r'@"\AC:\\taf-scenario\.[A-Za-z0-9]{1,96}\z"', self.cs)
        self.assertNotIn("taf-smoke", self.cs)
        self.assertIn('mode == "Inspect" && roots.Length == 2 && roots[0] == "Local" && roots[1] == "Synced"', self.cs)
        self.assertIn("actualDirs.SetEquals(directories) && actualFiles.SetEquals(files)", self.cs)
        self.assertIn("Inventory(input, roots, dirs, files)", self.cs)
        self.assertEqual(2, self.cs.count("Inventory(input, roots, dirs, files)"))
        self.assertIn('"unexpected_directory"', self.cs)
        self.assertIn('"unexpected_file"', self.cs)

    def test_path_aliases_controls_devices_and_missing_parents_refuse(self):
        for pin in ("NormalizationForm.FormC", "StringComparer.OrdinalIgnoreCase", 'part != "."', 'part != ".."',
                    'part.EndsWith("."', 'part.EndsWith(" "', "COM[0-9¹²³]", "LPT[0-9¹²³]", '"missing_parent"',
                    "parts.Length <= 32", "new UTF8Encoding(false, true).GetByteCount(part)"):
            self.assertIn(pin, self.cs)

    def test_limits_bounded_reads_growth_and_truncation_not_stream_to_eof(self):
        for pin in ("MaximumFile = 536870912", "MaximumTotal = 2147483648", "MaximumEntries = 16384",
                    "checked(total + sizes[i])", "Math.Min(remaining, buffer.Length)",
                    'Require(count > 0, "file_truncated")', 'Require(lease.Stream.ReadByte() == -1, "file_grew")'):
            self.assertIn(pin, self.cs)
        self.assertNotIn(".CopyTo(", self.cs)
        self.assertNotIn("ComputeHash(lease.Stream)", self.cs)

    def test_source_proof_retained_for_every_file_in_slots_mode(self):
        self.assertIn('mode == "Slots" ? selected.Length >= 1 && selected.Length <= 2 : selected.Length == 0', self.cs)
        self.assertIn(r'@"\ASynced/ThousandAndFirst/Stages/([A-Za-z0-9_-]{1,96})\.([ab])\.seal\z"', self.cs)
        self.assertIn("origin == null || origin == match.Groups[1].Value", self.cs)
        self.assertIn("files.Contains(path) && copying.Add(path)", self.cs)
        self.assertIn("Lease[] inputs = new Lease[paths.Length]", self.cs)
        self.assertIn("for (int i = 0; i < inputs.Length; i++)", self.cs)
        self.assertIn("proofs[i].after = Hash(inputs[i], sizes[i])", self.cs)
        self.assertIn('if (mode == "Copy") copying.UnionWith(files)', self.cs)
        self.assertIn("if (copying.Contains(paths[i]))", self.cs)

    def test_exclusive_copy_preserves_empty_directories_and_selected_only_ancestors(self):
        for pin in ('"destination_not_empty"', "CreateDirectoryW(synced", "if (allDirectories) copyDirs.UnionWith(sourceDirs)",
                    'while (parent.Contains("/"))', "Hold(path, false, true)", "file.Stream.Flush(true)",
                    "proofs[i].copy = Hash(file, sizes[i])", '"source_changed_during_copy"',
                    'Inventory(output, new[] { "Synced" }, copyDirs, copying)'):
            self.assertIn(pin, self.cs)
        self.assertIn("current.WriteLow == inputs[i].Original.WriteLow && current.WriteHigh == inputs[i].Original.WriteHigh", self.cs)

    def test_both_game_names_endpoint_only_and_no_process_mutation(self):
        self.assertIn('new[] { "CoQ", "CavesOfQud" }', self.cs)
        self.assertIn("Process.GetProcessesByName(name)", self.cs)
        self.assertIn('Require(processes.Length == 0, "game_process_present")', self.cs)
        self.assertIn("Idle(); Lease input", self.cs)
        self.assertIn('"plan_changed"); Idle();', self.cs)
        for forbidden in ("Process.Start", ".Kill(", "Start-Process", "Stop-Process", "SteamAPI", "SendInput", "SetForegroundWindow"):
            self.assertNotIn(forbidden, self.cs + self.ps)
        for false_claim in ("continuousIdleVerified = true", "gracefulQuitVerified = true", "sourceVersionVerified = true", "saveCompatibilityVerified = true"):
            self.assertNotIn(false_claim, self.cs + self.ps)

    def test_inspect_null_destination_survives_the_dotnet_string_boundary(self):
        """PowerShell turns a bare $null into "" for a [string] parameter.

        Run() requires destination == null for Inspect, so a bare $null made EVERY Inspect
        (and therefore every prepare-upgrade-profile.py mode) refuse with destination_invalid.
        """
        self.assertIn("$destination = [NullString]::Value", self.ps)
        self.assertNotIn("$destination = $null", self.ps)
        self.assertLess(self.ps.index("$destination = [NullString]::Value"),
                        self.ps.index("$result = $session.Run("))
        self.assertIn('mode == "Inspect" ? destination == null', self.cs)

    def test_independent_cleanup_no_success_on_failure_no_partial_deletion(self):
        self.assertIn("if (Disposed) return; Disposed = true", self.cs)
        self.assertIn("Held[i].Stream.Dispose(); } catch (Exception e) { failures.Add(e); }", self.cs)
        self.assertIn("if (!CloseHandle(Held[i].Handle)) failures.Add", self.cs)
        self.assertIn('throw new AggregateException("handle_cleanup_failed", failures)', self.cs)
        self.assertLess(self.ps.index("$session.Dispose()"), self.ps.index("$result.cleanupComplete = $true"))
        self.assertLess(self.ps.index("exit 2"), self.ps.index("$result | ConvertTo-Json"))
        self.assertIn("$null -ne $failure -or $null -ne $cleanupFailure", self.ps)
        self.assertIn("partialDestinationDeleted = $false", self.ps)
        for forbidden in ("File.Delete", "Directory.Delete", "Remove-Item", "File.Move", "Directory.Move", "FileMode.Create,", "FileMode.Truncate"):
            self.assertNotIn(forbidden, self.cs + self.ps)


if __name__ == "__main__":
    unittest.main()
