"""Source contracts and Python regex/integrity examples; NOT PowerShell execution.

No launcher, native filesystem, game, or C# serializer runs here. The small Python
envelope model demonstrates the checked corpus; source pins separately bind the
PowerShell implementation. Real Windows refusal/acceptance needs its own evidence.
"""
from __future__ import annotations

import hashlib
import pathlib
import re
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


def source(name: str) -> str:
    return (ROOT / name).read_text(encoding="utf-8-sig")


def function(text: str, name: str) -> str:
    start = text.index("function " + name + " {")
    following = text.find("\nfunction ", start + 1)
    return text[start:] if following < 0 else text[start:following]


def pattern(text: str, variable: str) -> re.Pattern:
    found = re.findall(r"\$" + re.escape(variable) + r"\s*=\s*'([^']*)'", text)
    if len(found) != 1:
        raise AssertionError("expected one literal pattern: " + variable)
    return re.compile(found[0].replace(r"\z", r"\Z"))


def frame(schema: int = 6, body: str = '{"kind":"record","origin":"world-1"}') -> bytes:
    payload = body.encode("utf-8", errors="strict")
    digest = hashlib.sha256(payload).hexdigest()
    return f"taf-seal {schema}\nsha256 {digest}\nlength {len(payload)}\n{body}\n".encode("utf-8")


def python_integrity_example(wire: bytes, envelope: re.Pattern) -> bool:
    """Framing/hash-only oracle. Does NOT validate the record or the file tree."""
    try:
        match = envelope.fullmatch(wire.decode("utf-8", errors="strict"))
        if match is None:
            return False
        payload = match[4].encode("utf-8", errors="strict")
        return int(match[3]) == len(payload) and hashlib.sha256(payload).hexdigest() == match[2]
    except UnicodeError:
        return False


class SmokeSyncedStoreTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.launcher = source("Tools/run-smoke.ps1")
        cls.envelope = pattern(cls.launcher, "envelopePattern")
        cls.ids = pattern(cls.launcher, "idPattern")

    def ordered(self, text: str, *tokens: str) -> None:
        cursor = 0
        for token in tokens:
            position = text.find(token, cursor)
            self.assertGreaterEqual(position, cursor, token)
            cursor = position + len(token)

    def test_extracted_envelope_schema_range_matches_real_record_constants(self):
        record = source("Core/KingdomSealRecord.cs")
        first = int(re.search(r"const int FirstSchema = (\d+);", record)[1])
        current = int(re.search(r"const int CurrentSchema = (\d+);", record)[1])
        self.assertEqual((4, 6), (first, current))
        for schema in range(0, current + 3):
            with self.subTest(schema=schema):
                self.assertEqual(first <= schema <= current,
                                 python_integrity_example(frame(schema), self.envelope))
        self.assertIn("FirstSchema", self.launcher)
        self.assertIn("CurrentSchema", self.launcher)

    def test_current_record_and_receipt_writers_use_shared_envelope(self):
        self.assertIn("KingdomSealFormat.Compose(WireSchema, WriteBody(WireSchema))",
                      source("Core/KingdomSealRecord.Writing.cs"))
        receipt = source("Core/KingdomSealReceipt.cs")
        self.assertIn("WireSchema = KingdomSealRecord.CurrentSchema", receipt)
        self.assertIn("KingdomSealFormat.Compose(WireSchema, body)", receipt)
        self.assertIn("KingdomSealRecord.FirstSchema", receipt)
        self.assertIn("KingdomSealRecord.CurrentSchema", receipt)

    def test_python_examples_accept_all_supported_schemas_and_multibyte_payload(self):
        for schema in (4, 5, 6):
            for body in ('{"kind":"record","founder":"Aeru"}',
                         '{"kind":"receipt","legacy":"a_1"}',
                         '{"kind":"record","founder":"é水𝄞"}'):
                with self.subTest(schema=schema, body=body):
                    self.assertTrue(python_integrity_example(frame(schema, body), self.envelope))

    def test_extracted_regex_refuses_future_and_noncanonical_framing(self):
        good = frame().decode("ascii")
        digest = self.envelope.fullmatch(good)[2]
        bad = [good.replace("taf-seal 6", schema) for schema in
               ("taf-seal 7", "taf-seal 3", "taf-seal 06", "taf-seal +6", "taf-seal -6", "TAF-SEAL 6")]
        bad += [good.replace("\n", "\r\n"), "\ufeff" + good, good[:-1], good + "\n",
                good + "tail", "prefix" + good, good.replace(digest, digest.upper()),
                good.replace(digest, "g" * 64), good.replace(digest, digest[:-1]),
                good.replace("length ", "length +"), good.replace("length ", "length 0"),
                good.replace("sha256 ", "sha256  "), good.replace("length ", "Length ")]
        for wire in bad:
            with self.subTest(wire=wire[:95]):
                self.assertIsNone(self.envelope.fullmatch(wire))

    def test_python_examples_refuse_every_truncation_and_extra_bytes(self):
        good = frame(body='{"kind":"record","founder":"é水𝄞"}')
        for end in range(len(good)):
            with self.subTest(end=end):
                self.assertFalse(python_integrity_example(good[:end], self.envelope))
        for suffix in (b"\x00", b"\n", b"x", good):
            with self.subTest(suffix=suffix[:8]):
                self.assertFalse(python_integrity_example(good + suffix, self.envelope))

    def test_python_examples_refuse_wrong_hash_length_or_utf8_without_normalizing(self):
        good = frame(body='{"kind":"record","founder":"é水𝄞"}')
        lines = good.split(b"\n")
        changed_hash = lines.copy()
        changed_hash[1] = b"sha256 " + b"0" * 64
        wrong_length = lines.copy()
        wrong_length[2] = b"length " + str(len(lines[3].decode("utf-8"))).encode("ascii")
        zero_length = lines.copy()
        zero_length[2] = b"length 0"
        for wire in (b"\n".join(changed_hash), b"\n".join(wrong_length),
                     b"\n".join(zero_length), good.replace(b"record", b"recOrd"),
                     good.replace("é".encode("utf-8"), b"\xff"), b"\xef\xbb\xbf" + good):
            with self.subTest(wire=wire[:95]):
                self.assertFalse(python_integrity_example(wire, self.envelope))

    def test_python_integrity_is_explicitly_not_json_or_record_semantic_proof(self):
        # A correct digest cannot bless the body; PowerShell must still parse and bind it.
        self.assertTrue(python_integrity_example(frame(body="not-json"), self.envelope))

    def test_python_writer_shape_refuses_hashed_mixed_arrays(self):
        # Extract literal tokens, pin their actual composition; no PowerShell evaluation.
        text = pattern(self.launcher, "stringToken").pattern
        number = pattern(self.launcher, "numberToken").pattern
        helper = re.sub(r"\s+", " ", function(self.launcher, "Read-TafSealEnvelope"))
        self.assertIn("$scalarToken = '(?:' + $stringToken + '|' + $numberToken + ')'", helper)
        self.assertIn("$arrayToken = '\\[(?:' + $stringToken + '(?:,' + $stringToken + ')*|' + "
                      "$numberToken + '(?:,' + $numberToken + ')*)?\\]'", helper)
        self.assertIn("$fieldToken = '\"(?<key>[a-z_]+)\":(?:' + $scalarToken + '|' + $arrayToken + ')'", helper)
        self.assertIn("$payloadPattern = '\\A\\{' + $fieldToken + '(?:,' + $fieldToken + ')*\\}\\z'", helper)
        scalar = "(?:" + text + "|" + number + ")"
        array = r"\[(?:" + text + "(?:," + text + ")*|" + number + "(?:," + number + r")*)?\]"
        field = '"[a-z_]+":(?:' + scalar + "|" + array + ")"
        payload = re.compile(r"\A\{" + field + "(?:," + field + r")*\}\Z")
        for value, accepted in (("[]", True), ('["x"]', True), ('["é","水"]', True),
                                ("[0,-1,12]", True), ('[1,"x"]', False), ('["x",1]', False),
                                ("[null]", False), ("[true]", False)):
            body = '{"kind":"record","list":' + value + "}"
            with self.subTest(array=value):
                self.assertTrue(python_integrity_example(frame(body=body), self.envelope))
                self.assertEqual(accepted, payload.fullmatch(body) is not None)

    def test_checked_in_real_seal_fixtures_match_extracted_envelope_and_python_integrity(self):
        fixtures = ROOT / "DevTests/Fixtures/SealProfile"
        names = ("reserved-receipt-0.3.1.seal", "schema0-0.3.1.seal",
                 "schema1-0.3.1.seal", "schema1-promoted-0.3.1.seal")
        self.assertEqual(sorted(names), sorted(path.name for path in fixtures.glob("*.seal")))
        for name in names:
            with self.subTest(name=name):
                self.assertTrue(python_integrity_example((fixtures / name).read_bytes(), self.envelope))

    def test_extracted_string_token_allows_only_native_reader_escape_syntax(self):
        token = pattern(self.launcher, "stringToken")
        # Literal wire escape only: this does not bless the decoded control value's semantics.
        for value in (r'"a\"b"', r'"a\\b"', r'"\u000a"', '"é水𝄞"', '"a/b"'):
            with self.subTest(value=value):
                self.assertIsNotNone(token.fullmatch(value))
        for escape in ("/", "b", "f", "n", "r", "t"):
            value = '"a\\' + escape + 'b"'
            with self.subTest(value=value):
                self.assertIsNone(token.fullmatch(value))

    def test_extracted_id_regex_matches_writer_id_bounds(self):
        record = source("Core/KingdomSealRecord.cs")
        maximum = int(re.search(r"const int MaxIdChars = (\d+);", record)[1])
        self.assertEqual(96, maximum)
        for value in ("a", "A0_-", "world-1", "a" * maximum):
            with self.subTest(value=value):
                self.assertIsNotNone(self.ids.fullmatch(value))
        for value in ("", "a" * (maximum + 1), "../a", "a/b", "a\\b", "a:b", ".", "..",
                      "a b", "a.", "é", "a\n", "a\r", "a\x00", "*", "a%2fb"):
            with self.subTest(value=value):
                self.assertIsNone(self.ids.fullmatch(value))

    def test_shared_envelope_helper_proves_raw_utf8_bytes_before_json(self):
        body = function(self.launcher, "Read-TafSealEnvelope")
        self.ordered(body, "ReadAllBytes", "GetString", "$envelopePattern",
                     "GetBytes", "LongLength", "ComputeHash", "ConvertFrom-Json")
        self.assertIn("[Text.UTF8Encoding]::new($false, $true)", body)
        framing = body[:body.index("$bodyBytes")]
        self.assertNotRegex(framing, r"\.Trim(?:Start|End)?\(|-replace|\.Replace\(")

    def test_writer_layout_and_tuple_are_not_a_stages_only_or_split_guess(self):
        store = source("Core/KingdomSealStore.cs")
        dispatch = function(self.launcher, "Assert-TafSyncedState")
        for folder in ("Stages", "Legacies", "Receipts", "Claims"):
            with self.subTest(folder=folder):
                self.assertIn('= "' + folder + '";', store)
                self.assertIn("'" + folder + "'", dispatch)
        paths = source("Core/KingdomSealStore.Paths.cs")
        self.assertIn("Legacy.Length.ToString(CultureInfo.InvariantCulture)", paths)
        self.assertIn("Target.Length.ToString(CultureInfo.InvariantCulture)", paths)
        tuple_name = function(self.launcher, "Get-TafReceiptFileName")
        self.assertIn(".Length", tuple_name)
        self.assertIn(".receipt", tuple_name)
        self.assertNotIn(".Split(", tuple_name)

    def test_source_profile_path_and_native_link_checks_still_precede_synced_validation(self):
        profile = function(self.launcher, "Assert-SmokeProfile")
        self.ordered(profile, "[IO.FileAttributes]::ReparsePoint",
                     "Get-SafeTreeItems -TreeRoot $ProfileRoot",
                     "Assert-StagedMod -StageRoot $stageRoot -SealPath $stageSeal",
                     "Assert-TafSyncedState -SyncedPath $syncedPath -OriginId $saveItem.Name")
        walk = function(self.launcher, "Get-SafeTreeItems")
        self.ordered(walk, "[IO.FileAttributes]::ReparsePoint",
                     'throw "Refusing reparse point',
                     "[TafSmokeNative.FileLinks]::Count($child.FullName)",
                     "if ($links -ne 1)", 'throw "Refusing hard-linked file',
                     "[void]$items.Add($child)")

    def test_source_resume_still_requires_one_complete_save_and_fresh_log(self):
        profile = function(self.launcher, "Assert-SmokeProfile")
        self.assertIn("$saveEntries.Count -ne 1", profile)
        self.assertIn("@('Cache.db', 'Primary.json', 'Primary.sav.gz')", profile)
        self.assertIn("Assert-PrimaryJson -Path $primaryJson -DirectoryId $saveItem.Name", profile)
        self.ordered(self.launcher, "Assert-SmokeLogVacant -Path $logPath",
                     "Assert-SmokeProfile -ProfileRoot $rootPath -IsResume ([bool]$Resume)",
                     "if ($ValidateOnly)", "Start-Process -FilePath $gamePath")

    def test_source_envelope_hash_and_bounded_length_refuse_before_parsing(self):
        body = function(self.launcher, "Read-TafSealEnvelope")
        self.ordered(body, "$rawBytes.LongLength -ne $File.Length", "$expectedHash = $Matches[2]",
                     "[long]::TryParse($Matches[3]", "$body = $Matches[4]",
                     "$declaredLength -gt 262000 -or $declaredLength -ne $bodyBytes.LongLength",
                     'throw "Resume TAF seal length differs', "$sha.ComputeHash($bodyBytes)",
                     "$actualHash -cne $expectedHash", 'throw "Resume TAF seal digest differs',
                     "ConvertFrom-Json")
        limits = source("Core/KingdomSealFormat.cs")
        self.assertIn("MaxFileChars = 262144;", limits)
        self.assertIn("MaxPayloadBytes = 262000;", limits)
        self.assertIn("$File.Length -gt 262144", body)

    def test_source_duplicate_keys_are_rejected_before_json_collapses_them(self):
        body = function(self.launcher, "Read-TafSealEnvelope")
        self.ordered(body, "$payloadPattern", "[TimeSpan]::FromSeconds(1)",
                     "if (-not $payload.Success)", "HashSet[string]",
                     "$payload.Groups['key'].Captures", "if (-not $keys.Add($key.Value))",
                     'throw "Resume TAF seal repeats a body key', "$body | ConvertFrom-Json",
                     "$record -isnot [pscustomobject]", "return $record")

    def test_extracted_stage_and_legacy_names_refuse_aliases_and_partial_files(self):
        dispatch = function(self.launcher, "Assert-TafSyncedState")
        literals = re.findall(r"\$name -cnotmatch '([^']+)'", dispatch)
        self.assertEqual(2, len(literals))
        for literal, valid in zip(literals, ("world_1.a.seal", "legacy_1.seal")):
            regex = re.compile(literal.replace(r"\z", r"\Z"))
            with self.subTest(valid=valid):
                self.assertIsNotNone(regex.fullmatch(valid))
            for invalid in ("../" + valid, "sub/" + valid, "sub\\" + valid,
                            valid.upper(), valid + ":stream", valid + "\n", valid + " ",
                            valid + ".writing.abc", valid + ".backup.abc", valid + ".released.abc"):
                with self.subTest(invalid=invalid):
                    self.assertIsNone(regex.fullmatch(invalid))
        self.assertIsNone(re.compile(literals[0].replace(r"\z", r"\Z")).fullmatch("world_1.c.seal"))

    def test_source_closed_folder_dispatch_and_empty_gates_preserve_refusals(self):
        body = function(self.launcher, "Assert-TafSyncedState")
        for token in ("$folder.Name -cnotin $allowedFolders", "$entries.Count -gt $limit",
                      "@($entries | Where-Object PSIsContainer).Count -ne 0",
                      "Assert-TafClaimFileName -Name $name", "$entry.Length -ne 0",
                      "'.legacies.lock'", "'.claims.lock'", '".journal-${origin}.lock"',
                      "$record.origin -cne $origin", "$record.legacy -cne $legacy",
                      "$record.status -cnotin @('living', 'terminal', 'retired')",
                      "$record.status -cne 'promoted'",
                      "$record.state -cnotin @('held', 'faded', 'abandoned', 'ruined')",
                      "$record.roll -lt 0 -or $record.roll -gt 99",
                      "$name -cne (Get-TafReceiptFileName -Record $record)",
                      '$_.Name -ceq $gateName',
                      'throw "Resume TAF store record lacks its writer gate'):
            with self.subTest(token=token):
                self.assertIn(token, body)
        self.assertIn("Where-Object { $_.Name -ceq $gateName }).Count -ne 1", body)
        self.assertNotIn("$origin -cne $OriginId", body)
        self.assertNotRegex(body, r"Remove-Item|WriteAll|Set-Content|\.Delete\(")

    def test_source_receipts_bind_exact_fields_filename_and_promoted_legacy(self):
        receipt = function(self.launcher, "Get-TafReceiptFileName")
        for token in ("'kind,legacy,lineage,state,target,written'", "$Record.kind -cne 'receipt'",
                      "Test-TafSealId $Record.lineage", "Test-TafSealId $Record.legacy",
                      "Test-TafSealId $Record.target", "@('reserved', 'committed', 'declined')",
                      "Test-JsonInteger $Record.written", "$Record.written -lt 0",
                      '$($Record.legacy.Length)_$($Record.legacy)$($Record.target.Length)_$($Record.target).receipt'):
            self.assertIn(token, receipt)
        dispatch = function(self.launcher, "Assert-TafSyncedState")
        self.assertIn("-not $legacies.ContainsKey($receipt.legacy)", dispatch)
        self.assertIn("$legacies[$receipt.legacy].lineage -cne $receipt.lineage", dispatch)
        self.assertIn("if (-not $claimed.Add($receipt.legacy))", dispatch)
        self.assertNotIn("$receipt.state -cne 'declined'", dispatch)

    def test_source_claim_tuple_lengths_reprove_without_claiming_a_live_lease(self):
        body = function(self.launcher, "Assert-TafClaimFileName")
        self.ordered(body, "$Name -cnotmatch $claimPattern", "$legacyLength -gt 96",
                     "$stem.Substring(0, $legacyLength)", "$stem.Substring($legacyLength)",
                     "$tail -cnotmatch $targetPattern", "$targetLength -ne $target.Length",
                     '$Name -cne "${legacyLength}_${legacy}${targetLength}_${target}.receipt.live"')
        claims = pattern(self.launcher, "claimPattern")
        target = pattern(self.launcher, "targetPattern")
        self.assertIsNotNone(claims.fullmatch("3_a_23_b-4.receipt.live"))
        self.assertIsNotNone(target.fullmatch("3_b-4"))
        for value in ("0_a3_b-4.receipt.live", "03_a_23_b-4.receipt.live",
                      "3_a_23_b-4.receipt.live\n", "../3_a_23_b-4.receipt.live"):
            self.assertIsNone(claims.fullmatch(value))
        self.assertIn("It proves no live lease", function(self.launcher, "Assert-TafSyncedState"))


if __name__ == "__main__":
    unittest.main()
