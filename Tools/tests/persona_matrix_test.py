"""Adversarial tests for the persona manifest grammar and the journal assertion it carries.

These execute the authoritative implementation in Tools/personas/persona_matrix.py, which is what
Tools/run-personas.sh calls for every verdict. The shell script owns the game; everything that
decides PASS or FAIL is here, so the matrix's judgement is testable without a licensed install.
"""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "persona_matrix", ROOT / "Tools" / "personas" / "persona_matrix.py"
)
matrix = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(matrix)

PROFILE_SPEC = importlib.util.spec_from_file_location(
    "scenario_profile", ROOT / "Tools" / "scenario_profile.py"
)
profile = importlib.util.module_from_spec(PROFILE_SPEC)
PROFILE_SPEC.loader.exec_module(profile)

GREEN = (
    "REQUEST=arch-gallery-slice;facing=north\n"
    "SCRIPT=flatten;realize;status\n"
    "EXPECT=flatten:OK,realize:OK,status:OK,COMPLETE\n"
)

P0_HOUSING_PERSONAS = {
    "arch-housing-tent-m.persona": "arch-housing-tent-m;facing=north",
    "arch-housing-hut-m.persona": "arch-housing-hut-m;facing=north",
    "arch-housing-tentrow-l.persona": "arch-housing-tentrow-l;facing=north",
    "arch-housing-hutyard-l.persona": "arch-housing-hutyard-l;facing=north",
    "arch-housing-tent-xl.persona": "arch-housing-tent-xl;facing=north",
    "arch-housing-tentrow-xl.persona": "arch-housing-tentrow-xl;facing=north",
    "arch-housing-blockhut-xl.persona": "arch-housing-blockhut-xl;facing=north",
    "arch-housing-blockyard-xl.persona": "arch-housing-blockyard-xl;facing=north",
}
P0_HOUSING_SCRIPT = "flatten;realize;advance 300;frame;status"
P0_HOUSING_EXPECT = "flatten:OK,realize:OK,advance:OK,frame:OK,status:OK,COMPLETE"

HUT_CARDINAL_PERSONAS = {
    "arch-housing-hut-m.persona": "arch-housing-hut-m;facing=north",
    "arch-housing-hut-m-east.persona": "arch-housing-hut-m;facing=east",
    "arch-housing-hut-m-south.persona": "arch-housing-hut-m;facing=south",
    "arch-housing-hut-m-west.persona": "arch-housing-hut-m;facing=west",
}

NATIVE_GALLERY_PERSONAS = {
    "arch-civic-hall-m.persona": (
        "arch-civic-hall-m;facing=north",
        "architecture,visual,civic,progression,taste",
    ),
    "arch-civic-heartmoot-l.persona": (
        "arch-civic-heartmoot-l;facing=north",
        "architecture,visual,civic,progression,taste",
    ),
    "arch-faith-temple-l.persona": (
        "arch-faith-temple-l;facing=north",
        "architecture,visual,faith,progression,taste",
    ),
}

NATIVE_GALLERY_CASES = {
    "arch-civic-hall-m": ("hall", "civic", "m", "fallback"),
    "arch-civic-heartmoot-l": ("heartmoot", "civic", "l", "fallback"),
    "arch-faith-temple-l": ("temple", "faith", "l", "fallback"),
}


def row(verb: str, outcome: str, message: str = "-") -> str:
    return "2026-08-30T00:00:00.000Z\t%s\t%s\t%s" % (verb, outcome, message)


def journal(*rows: str) -> str:
    return "\n".join(rows) + "\n"


class ManifestGrammarTest(unittest.TestCase):
    def test_green_manifest_parses(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        self.assertEqual("flatten realize status", found["SCRIPT_WORDS"])
        self.assertEqual(str(matrix.DEFAULT_TIMEOUT), found["TIMEOUT"])

    def test_missing_required_key_is_refused(self):
        for missing in ("REQUEST", "SCRIPT", "EXPECT"):
            text = "\n".join(
                line
                for line in GREEN.splitlines()
                if not line.startswith(missing + "=")
            )
            with self.assertRaises(SystemExit):
                matrix.parse_manifest(text, "x.persona")

    def test_unknown_key_is_refused_not_ignored(self):
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(GREEN + "SCRIPTT=flatten\n", "x.persona")

    def test_repeated_key_is_refused(self):
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(GREEN + "SCRIPT=status\n", "x.persona")

    def test_persona_may_not_freeze_its_own_seed(self):
        text = GREEN.replace(
            "REQUEST=arch-gallery-slice;facing=north",
            "REQUEST=arch-gallery-slice;facing=north;seed=#42",
        )
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(text, "x.persona")

    def test_timeout_bounds(self):
        self.assertEqual(60, matrix.parse_timeout("60", "x"))
        for bad in ("0", "-1", "abc", str(matrix.MAX_TIMEOUT + 1), "1.5"):
            with self.assertRaises(SystemExit):
                matrix.parse_timeout(bad, "x")

    def test_set_tags_parse_and_deduplicate_in_order(self):
        found = matrix.parse_manifest(
            GREEN + "SET=smoke, architecture,smoke\n", "x.persona"
        )
        self.assertEqual("smoke,architecture", found["SET"])

    def test_absent_set_means_untagged(self):
        self.assertEqual("", matrix.parse_manifest(GREEN, "x.persona")["SET"])

    def test_malformed_set_tag_is_refused(self):
        for bad in ("Smoke", "sm oke", "-smoke", "smoke;laws"):
            with self.assertRaises(SystemExit):
                matrix.parse_manifest(GREEN + "SET=%s\n" % bad, "x.persona")

    def test_unknown_check_is_refused(self):
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(GREEN + "CHECK=whatever\n", "x.persona")


class ExpectedLogTest(unittest.TestCase):
    def manifest(self, lines):
        return matrix.parse_manifest(
            GREEN + "LOG_EXPECT=" + json.dumps(lines) + "\n", "x.persona"
        )

    def test_optional_field_is_disabled_or_canonical_json(self):
        self.assertNotIn("LOG_EXPECT", matrix.parse_manifest(GREEN, "x"))
        found = matrix.parse_manifest(
            GREEN + 'LOG_EXPECT= [ "Error [.*]", "\\u5c3e" ]\n', "x"
        )
        self.assertEqual('["Error [.*]","尾"]', found["LOG_EXPECT"])
        self.assertEqual(["Error [.*]", "尾"], json.loads(found["LOG_EXPECT"]))

    def test_malformed_shapes_duplicates_and_nonprintable_lines_are_refused(self):
        bad = [
            "",
            "null",
            "{}",
            "[]",
            '"line"',
            "[1]",
            "[null]",
            "[[]]",
            '[""]',
            '["same","same"]',
            '["a",]',
        ]
        bad.extend(
            json.dumps(["before" + char + "after"])
            for char in (
                "\n",
                "\r",
                "\t",
                "\0",
                "\x1f",
                "\x7f",
                "\x85",
                "\u2028",
                "\ud800",
                "\udfff",
            )
        )
        for value in bad:
            with self.subTest(value=value), self.assertRaises(SystemExit):
                matrix.parse_manifest(GREEN + "LOG_EXPECT=" + value + "\n", "x")

    def test_line_count_length_and_total_input_bounds(self):
        lines = [str(index) * 1024 for index in range(4)]
        self.assertEqual(lines, json.loads(self.manifest(lines)["LOG_EXPECT"]))
        for lines in (["x" * 1025], [str(index) for index in range(5)]):
            with self.subTest(lines=lines), self.assertRaises(SystemExit):
                self.manifest(lines)
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(
                GREEN + 'LOG_EXPECT=["x",' + " " * 8192 + '"y"]\n', "x"
            )

    def test_matching_is_literal_and_requires_the_complete_line(self):
        line = r"expected [a-z]+.* (x)? ^$ \\"
        raw = (
            "prefix " + line + "\n" + line + " suffix\n" + line + "\nother\n"
        ).encode()
        self.assertEqual(
            ("prefix " + line + "\n" + line + " suffix\nother\n").encode(),
            matrix.expected_log(self.manifest([line]), raw, "x"),
        )
        with self.assertRaises(SystemExit):
            matrix.expected_log(self.manifest([line]), b"expected abc (x)\n", "x")

    def test_only_crlf_and_exact_expected_records_change(self):
        for raw, expected in (
            (
                b"one\r\nEXPECTED\r\n\r\ntwo\rlone\xff\x00\n",
                b"one\n\ntwo\rlone\xff\x00\n",
            ),
            (b"one\nEXPECTED", b"one\n"),
            (b"EXPECTED\none", b"one"),
            (b"EXPECTED", b""),
        ):
            with self.subTest(raw=raw):
                self.assertEqual(
                    expected, matrix.expected_log(self.manifest(["EXPECTED"]), raw, "x")
                )

    def test_cli_refuses_missing_duplicate_or_undeclared_expectations_without_stdout(
        self,
    ):
        cases = (
            (GREEN, b"EXPECTED\n"),
            (GREEN + "LOG_EXPECT=[]\n", b"EXPECTED\n"),
            (GREEN + 'LOG_EXPECT=["EXPECTED"]\n', b"other\n"),
            (GREEN + 'LOG_EXPECT=["EXPECTED"]\n', b"EXPECTED\r\nEXPECTED\n"),
            (GREEN + 'LOG_EXPECT=["EXPECTED","SECOND"]\n', b"EXPECTED\n"),
            (
                GREEN + 'LOG_EXPECT=["EXPECTED"]\n',
                b"EXPECTED\nMODERROR [Foreign] new\n",
            ),
        )
        with tempfile.TemporaryDirectory() as directory:
            persona, log = (
                pathlib.Path(directory) / "x.persona",
                pathlib.Path(directory) / "Player.log",
            )
            for text, raw in cases:
                with self.subTest(text=text, raw=raw):
                    persona.write_text(text, encoding="utf-8")
                    log.write_bytes(raw)
                    result = subprocess.run(
                        [
                            sys.executable,
                            str(SPEC.origin),
                            "expected-log",
                            str(persona),
                            str(log),
                        ],
                        capture_output=True,
                        check=False,
                    )
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual(b"", result.stdout)
                    self.assertTrue(result.stderr.startswith(b"persona: "))
                    self.assertEqual(raw, log.read_bytes())

    def test_cli_exports_canonical_fields_preserves_raw_and_retains_unlisted_stack_frame(
        self,
    ):
        with tempfile.TemporaryDirectory() as directory:
            persona, log = (
                pathlib.Path(directory) / "x.persona",
                pathlib.Path(directory) / "Player.log",
            )
            persona.write_text(
                GREEN
                + 'LOG_EXPECT= [ "MODERROR [The Thousand and First] expected" ]\n',
                encoding="utf-8",
            )
            raw = (
                b"[TAF] loaded\r\nMODERROR [The Thousand and First] expected\r\n"
                b"  at ThousandAndFirst.Unlisted.Call ()\r\nforeign exception\rlone\xff\n"
            )
            log.write_bytes(raw)
            command = [sys.executable, str(SPEC.origin)]
            fields = subprocess.run(
                command + ["fields", str(persona)], capture_output=True, check=True
            )
            self.assertIn(
                b'log_expect\t["MODERROR [The Thousand and First] expected"]\n',
                fields.stdout,
            )
            result = subprocess.run(
                command + ["expected-log", str(persona), str(log)],
                capture_output=True,
                check=True,
            )
            self.assertEqual(
                b"[TAF] loaded\n  at ThousandAndFirst.Unlisted.Call ()\nforeign exception\rlone\xff\n",
                result.stdout,
            )
            self.assertEqual(raw, log.read_bytes())
            derivative = pathlib.Path(directory) / "Player.checked.log"
            derivative.write_bytes(result.stdout)
            strict = subprocess.run(
                ["bash", str(ROOT / "Tools" / "check-player-log.sh"), str(derivative)],
                capture_output=True,
                check=False,
                env={**os.environ, "TAF_LOG_ALLOW": "", "TMPDIR": directory},
            )
            self.assertNotEqual(0, strict.returncode)
            self.assertIn(b"SMOKE LOG FAILED", strict.stderr)
            self.assertIn(b"ThousandAndFirst.Unlisted.Call", strict.stderr)
            persona.write_text(GREEN, encoding="utf-8")
            fields = subprocess.run(
                command + ["fields", str(persona)], capture_output=True, check=True
            )
            self.assertIn(b"log_expect\t\n", fields.stdout)

    def test_expected_mode_refuses_every_unmatched_mod_error_or_warning(self):
        manifest = self.manifest(
            ["MODERROR [The Thousand and First] expected", "MODWARN [Fixture] expected"]
        )
        expected = (
            b"MODERROR [The Thousand and First] expected\nMODWARN [Fixture] expected\n"
        )
        self.assertEqual(
            b"[TAF] loaded\n",
            matrix.expected_log(manifest, b"[TAF] loaded\n" + expected, "x"),
        )
        for marker in (b"MODERROR", b"MODWARN"):
            for title in (b"The Thousand and First", b"Foreign Mod"):
                with (
                    self.subTest(marker=marker, title=title),
                    self.assertRaises(SystemExit),
                ):
                    matrix.expected_log(
                        manifest,
                        expected + marker + b" [" + title + b"] unexpected\n",
                        "x",
                    )


class ForbiddenLogTest(unittest.TestCase):
    """LOG_FORBID is the opposite of LOG_EXPECT and is bounded exactly as tightly."""

    def manifest(self, lines):
        return matrix.parse_manifest(
            GREEN + "LOG_FORBID=" + json.dumps(lines) + "\n", "x.persona"
        )

    def test_optional_field_is_disabled_or_canonical_json(self):
        self.assertNotIn("LOG_FORBID", matrix.parse_manifest(GREEN, "x"))
        found = self.manifest(["a halt line", "another"])
        self.assertEqual('["a halt line","another"]', found["LOG_FORBID"])

    def test_malformed_shapes_duplicates_and_nonprintable_lines_are_refused(self):
        bad = ["", "null", "{}", "[]", '"line"', "[1]", "[null]", '[""]',
               '["same","same"]', json.dumps(["before\nafter"])]
        for value in bad:
            with self.subTest(value=value), self.assertRaises(SystemExit):
                matrix.parse_manifest(GREEN + "LOG_FORBID=" + value + "\n", "x")
        for lines in (["x" * 1025], [str(index) for index in range(5)]):
            with self.subTest(lines=lines), self.assertRaises(SystemExit):
                self.manifest(lines)

    def test_a_forbidden_substring_is_found_anywhere_in_the_log(self):
        manifest = self.manifest(["recovery requires inspection", "was not staged"])
        clean = b"[TAF] heart rung raised: 2 (heartwaterstone)\n[TAF] all is well\n"
        self.assertEqual([], matrix.forbidden_log(manifest, clean, "x"))
        dirty = (b"[TAF] heart rung raised: 2 (heartwaterstone)\n"
                 b"[TAF] construction: founding heart recovery requires inspection\n")
        seen = matrix.forbidden_log(manifest, dirty, "x")
        self.assertEqual(["line 2: recovery requires inspection"], seen)
        # Both halves of the #162 signature, each reported once with its own line number.
        both = dirty + b"[TAF] seal: settlement pass was not staged (a sealed work root)\n"
        self.assertEqual(
            ["line 2: recovery requires inspection", "line 3: was not staged"],
            matrix.forbidden_log(manifest, both, "x"))
        # Windows line endings are read the same way, and a persona without the field refuses
        # rather than silently passing.
        self.assertEqual(["line 1: was not staged"], matrix.forbidden_log(
            manifest, b"seal: settlement pass was not staged\r\nrest\r\n", "x"))
        with self.assertRaises(SystemExit):
            matrix.forbidden_log(matrix.parse_manifest(GREEN, "x"), clean, "x")


class ScriptGrammarTest(unittest.TestCase):
    def test_advance_folds_to_two_words(self):
        self.assertEqual(
            ["flatten", "advance", "300", "status"],
            matrix.script_words("flatten;advance 300;status", "x"),
        )

    def test_unsealable_verb_is_refused(self):
        for bad in (
            "arcology entry",
            "capture",
            "help",
            "nonsense",
            "advance",
            "advance 0",
            "advance x",
        ):
            with self.assertRaises(SystemExit):
                matrix.script_words("flatten;" + bad, "x")

    def test_advance_count_bound_matches_the_profile_tool(self):
        self.assertEqual(profile.MAX_ADVANCE_TURNS, matrix.MAX_ADVANCE_TURNS)
        with self.assertRaises(SystemExit):
            matrix.script_words("advance %d" % (matrix.MAX_ADVANCE_TURNS + 1), "x")

    def test_sealable_verb_sets_agree_with_the_profile_tool(self):
        self.assertEqual(tuple(profile.SCRIPT_VERBS), matrix.SCRIPT_VERBS)
        self.assertEqual(tuple(profile.RESERVED_VERBS), matrix.RESERVED_VERBS)
        self.assertEqual(profile.COUNTED_VERB, matrix.COUNTED_VERB)

    def test_frame_is_a_sealed_argument_free_builtin(self):
        self.assertEqual(
            ["flatten", "realize", "advance", "300", "frame", "status"],
            matrix.script_words(
                "flatten;realize;advance 300;frame;status", "visual.persona"
            ),
        )
        self.assertIn("frame", profile.SCRIPT_VERBS)
        self.assertIn("frame", profile.RESERVED_VERBS)


class ExtraVerbTest(unittest.TestCase):
    def test_declared_third_party_verb_becomes_sealable(self):
        found = matrix.parse_manifest(
            "REQUEST=arch-gallery-slice;facing=north\n"
            "SCRIPT=flatten;myverb\n"
            "EXPECT=flatten:OK,myverb:OK,COMPLETE\n"
            "VERBS=myverb\n",
            "x.persona",
        )
        self.assertEqual("flatten myverb", found["SCRIPT_WORDS"])
        self.assertEqual("myverb", found["VERBS"])

    def test_declared_native_rung_verbs_do_not_authorize_arguments(self):
        for verb in ("subsidence-rung-check", "subsidence-rung-death-check"):
            with self.subTest(verb=verb):
                text = (
                    "REQUEST=founding-first-city\n"
                    "SCRIPT=stagedigest;%s death-prepared;stagedigest\n"
                    "EXPECT=stagedigest:OK,%s:OK,stagedigest:OK,COMPLETE\n"
                    "VERBS=%s\n"
                ) % (verb, verb, verb)
                with self.assertRaises(SystemExit):
                    matrix.parse_manifest(text, "native-arguments.persona")
                with self.assertRaises(SystemExit):
                    profile.parse_script(
                        ["stagedigest", verb, "death-prepared", "stagedigest"], (verb,)
                    )

    def test_undeclared_third_party_verb_is_refused(self):
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(
                "REQUEST=arch-gallery-slice;facing=north\n"
                "SCRIPT=flatten;myverb\n"
                "EXPECT=flatten:OK,myverb:OK,COMPLETE\n",
                "x.persona",
            )

    def test_reserved_and_malformed_names_are_refused(self):
        for bad in (
            "realize",
            "arcology",
            "capture",
            "Status",
            "my verb",
            "my_verb",
            ",",
            "a,,b",
            "a,a",
        ):
            with self.assertRaises(SystemExit):
                matrix.parse_verbs(bad, "x")
        # An absent VERBS key is not a refusal: it is how most personas declare no extra verbs.
        self.assertEqual((), matrix.parse_verbs("", "x"))

    def test_profile_tool_refuses_the_same_names(self):
        for bad in ("realize", "arcology", "capture", "Status", "my_verb"):
            with self.assertRaises(SystemExit):
                profile.parse_extra_verbs(bad)
        self.assertEqual(("myverb",), profile.parse_extra_verbs("myverb"))


class ExpectGrammarTest(unittest.TestCase):
    def test_terminal_must_be_last_and_present(self):
        with self.assertRaises(SystemExit):
            matrix.parse_expect("COMPLETE,status:OK", "x")
        with self.assertRaises(SystemExit):
            matrix.parse_expect("status:OK", "x")

    def test_outcome_must_be_ok_or_refused(self):
        with self.assertRaises(SystemExit):
            matrix.parse_expect("status:MAYBE,COMPLETE", "x")
        with self.assertRaises(SystemExit):
            matrix.parse_expect("status,COMPLETE", "x")

    def test_substring_is_carried(self):
        parsed = matrix.parse_expect("status:OK~ineligible,COMPLETE", "x")
        self.assertEqual(("status", "OK", "ineligible"), parsed[0])
        self.assertEqual(("SCRIPT-COMPLETE", "", ""), parsed[1])


class JournalReadingTest(unittest.TestCase):
    def test_escapes_round_trip(self):
        self.assertEqual("a\nb\tc\\d", matrix.unescape("a\\nb\\tc\\\\d"))

    def test_malformed_row_is_a_fault_not_a_skip(self):
        with self.assertRaises(SystemExit):
            matrix.read_journal("a\tb\tOK\n")
        with self.assertRaises(SystemExit):
            matrix.read_journal(row("status", "MAYBE"))

    def test_bookkeeping_rows_are_dropped(self):
        rows = matrix.read_journal(
            journal(
                row("AUTOSTART", "OK"),
                row("TESTGROUND-BUILT", "OK"),
                row("VERB-REFUSED", "REFUSED"),
                row("RUNNER-ARMED", "OK"),
                row("SCRIPT-BEGIN", "OK"),
                row("status", "OK"),
                row("advance-progress", "OK"),
                row("advance-complete", "OK"),
                row("SCRIPT-COMPLETE", "OK"),
            )
        )
        self.assertEqual(
            ["status", "SCRIPT-COMPLETE"],
            [verb for verb, _, _ in matrix.significant(rows)],
        )

    def test_lifecycle_runner_row_is_bookkeeping_not_a_positional_expectation(self):
        """ZAP-034: LIFECYCLE-RUNNER lands right after QUICKSTART-BOOT-BEGIN on a
        quickstart-lifecycle boot (Harness/KingdomQuickstartLifecycleRunnerPatch.cs). It must drop
        out of significant() so a lifecycle persona's positional EXPECT never has to name it, and
        so it can never be mistaken for a verb the script itself asked for."""
        rows = matrix.read_journal(
            journal(
                row("QUICKSTART-BOOT-BEGIN", "OK"),
                row("LIFECYCLE-RUNNER", "OK"),
                row("QUICKSTART-BOOT-OBSERVED", "OK"),
            )
        )
        self.assertEqual(
            ["QUICKSTART-BOOT-BEGIN", "QUICKSTART-BOOT-OBSERVED"],
            [verb for verb, _, _ in matrix.significant(rows)],
        )

    def test_terminal_row_is_found(self):
        self.assertEqual(
            "GATE-REFUSED",
            matrix.terminal_row(
                matrix.read_journal(
                    journal(row("AUTOSTART", "OK"), row("GATE-REFUSED", "REFUSED"))
                )
            ),
        )
        self.assertEqual(
            "", matrix.terminal_row(matrix.read_journal(journal(row("status", "OK"))))
        )


class MatchingTest(unittest.TestCase):
    def green_journal(self):
        return journal(
            row("RUNNER-ARMED", "OK"),
            row("SCRIPT-BEGIN", "OK"),
            row("flatten", "OK"),
            row("realize", "OK"),
            row("status", "OK"),
            row("SCRIPT-COMPLETE", "OK"),
        )

    def test_green_run_meets_its_expectations(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        self.assertEqual([], matrix.assess(found, self.green_journal(), "x.persona"))

    def test_an_unexpected_ok_fails_as_loudly_as_a_refusal(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        extra = self.green_journal().replace(
            row("SCRIPT-COMPLETE", "OK"),
            row("list", "OK") + "\n" + row("SCRIPT-COMPLETE", "OK"),
        )
        problems = matrix.assess(found, extra, "x.persona")
        self.assertTrue(problems)
        self.assertIn("row 4", problems[0])

    def test_a_missing_row_fails(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        short = journal(
            row("flatten", "OK"), row("realize", "OK"), row("SCRIPT-COMPLETE", "OK")
        )
        self.assertTrue(matrix.assess(found, short, "x.persona"))

    def test_wrong_outcome_fails(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        flipped = self.green_journal().replace(
            row("realize", "OK"), row("realize", "REFUSED")
        )
        problems = matrix.assess(found, flipped, "x.persona")
        self.assertTrue(any("REFUSED, expected OK" in p for p in problems))

    def test_wrong_terminal_fails(self):
        found = matrix.parse_manifest(GREEN, "x.persona")
        stopped = self.green_journal().replace(
            row("SCRIPT-COMPLETE", "OK"), row("SCRIPT-STOPPED", "REFUSED")
        )
        self.assertTrue(matrix.assess(found, stopped, "x.persona"))

    def test_missing_reason_code_fails(self):
        text = (
            "REQUEST=arch-gallery-slice;facing=north\n"
            "SCRIPT=flatten;realize;realize\n"
            "EXPECT=flatten:OK,realize:OK,"
            "realize:REFUSED~taf-scenario-transaction-committed,STOPPED\n"
        )
        found = matrix.parse_manifest(text, "x.persona")
        coded = journal(
            row("flatten", "OK"),
            row("realize", "OK"),
            row("realize", "REFUSED", "[taf-scenario-transaction-committed] spent"),
            row("SCRIPT-STOPPED", "REFUSED"),
        )
        self.assertEqual([], matrix.assess(found, coded, "x.persona"))
        uncoded = coded.replace("[taf-scenario-transaction-committed] spent", "spent")
        self.assertTrue(matrix.assess(found, uncoded, "x.persona"))


class DigestStabilityTest(unittest.TestCase):
    A = "a" * 64
    B = "b" * 64

    def manifest(self):
        return matrix.parse_manifest(
            "REQUEST=arch-gallery-slice;facing=north\n"
            "SCRIPT=flatten;realize;status;advance 300;status\n"
            "EXPECT=flatten:OK,realize:OK,status:OK,advance:OK,status:OK,COMPLETE\n"
            "CHECK=status-digest-stable\n",
            "x.persona",
        )

    def run_journal(self, first: str, second: str):
        return journal(
            row("flatten", "OK"),
            row("realize", "OK"),
            row("status", "OK", "Measured key set: " + first),
            row("advance", "OK"),
            row("status", "OK", "Measured key set: " + second),
            row("SCRIPT-COMPLETE", "OK"),
        )

    def test_stable_digest_passes(self):
        self.assertEqual(
            [],
            matrix.assess(
                self.manifest(), self.run_journal(self.A, self.A), "x.persona"
            ),
        )

    def test_moved_digest_fails(self):
        problems = matrix.assess(
            self.manifest(), self.run_journal(self.A, self.B), "x.persona"
        )
        self.assertTrue(any("digests moved" in p for p in problems))

    def test_absent_digest_fails(self):
        problems = matrix.assess(
            self.manifest(), self.run_journal("none", "none"), "x.persona"
        )
        self.assertTrue(any("no 64-hex digest" in p for p in problems))


class ShippedPersonaTest(unittest.TestCase):
    """Every checked-in persona parses, and its expectations reference only real journal shapes."""

    def personas(self):
        return sorted((ROOT / "Tools" / "personas").glob("*.persona"))

    def architecture_scenarios(self):
        roster = ET.parse(ROOT / "Harness" / "KingdomScenarios.xml").getroot()
        return {
            scenario.attrib["Key"]
            for scenario in roster.findall("scenario")
            if scenario.attrib.get("Family") == "architecture"
        }

    def architecture_cases(self):
        cases = set()
        for path in sorted((ROOT / "Architecture").glob("KingdomArchitectures-*.xml")):
            root = ET.parse(path).getroot()
            for binding in root.findall("./plan/binding"):
                lot_type = binding.attrib.get("Type", "").lower()
                lot_size = binding.attrib.get("Size", "").lower()
                for tier in binding.findall("tier"):
                    build = tier.attrib.get("BuildKey", "")
                    for variant in tier.findall("variant"):
                        cases.add(
                            (build, lot_type, lot_size, variant.attrib.get("Key", ""))
                        )
        return cases

    def test_every_persona_parses(self):
        self.assertEqual(92, len(self.personas()))
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            self.assertTrue(found["REQUEST"])
            self.assertTrue(found["SCRIPT_WORDS"])

    def test_prepared_death_persona_declares_its_own_sealable_no_argument_verb(self):
        directory = ROOT / "Tools" / "personas"
        name = "subsidence-rung-death-native-checks.persona"
        found = matrix.parse_manifest(
            (directory / name).read_text(encoding="utf-8"), name
        )
        wear = matrix.parse_manifest(
            (directory / "subsidence-rung-native-checks.persona").read_text(
                encoding="utf-8"
            ),
            "subsidence-rung-native-checks.persona",
        )
        self.assertEqual("founding-first-city", found["REQUEST"])
        self.assertEqual("8.22@40,12", found["START"])
        self.assertEqual("subsidence-rung-death-check", found["VERBS"])
        self.assertEqual(
            "stagedigest;subsidence-rung-death-check;stagedigest", found["SCRIPT"]
        )
        self.assertEqual(
            ["stagedigest", "subsidence-rung-death-check", "stagedigest"],
            profile.parse_script(found["SCRIPT_WORDS"].split(), (found["VERBS"],)),
        )
        self.assertEqual(
            "stagedigest:OK~founded=false,subsidence-rung-death-check:OK~cases=6 passed=6 failed=0,"
            "stagedigest:OK~founded=true,COMPLETE",
            found["EXPECT"],
        )
        self.assertEqual(
            "stagedigest;subsidence-rung-check;stagedigest", wear["SCRIPT"]
        )
        self.assertEqual("subsidence-rung-check", wear["VERBS"])
        self.assertEqual(wear["LOG_EXPECT"], found["LOG_EXPECT"])
        self.assertNotIn("TAF_LOG_ALLOW", found)

    def test_rung_save_persona_is_a_separate_first_leg(self):
        directory = ROOT / "Tools" / "personas"
        name = "subsidence-rung-save-native-check.persona"
        found = matrix.parse_manifest(
            (directory / name).read_text(encoding="utf-8"), name
        )
        older = matrix.parse_manifest(
            (directory / "subsidence-save-native-check.persona").read_text(
                encoding="utf-8"
            ),
            "subsidence-save-native-check.persona",
        )
        self.assertEqual("founding-first-city", found["REQUEST"])
        self.assertEqual("8.22@40,12", found["START"])
        self.assertEqual(
            "stagedigest;subsidence-rung-save-check;stagedigest", found["SCRIPT"]
        )
        self.assertEqual("subsidence-rung-save-check", found["VERBS"])
        self.assertEqual(
            "stagedigest:OK~founded=false,subsidence-rung-save-check:OK~cases=1 passed=1 failed=0,"
            "stagedigest:OK~founded=true,COMPLETE",
            found["EXPECT"],
        )
        self.assertEqual("growth,native-regression,save-load", found["SET"])
        self.assertEqual(older["LOG_EXPECT"], found["LOG_EXPECT"])
        self.assertNotEqual(older["SCRIPT"], found["SCRIPT"])

    def test_p0_housing_personas_freeze_exact_north_cases_and_full_visual_script(self):
        self.assertEqual(8, len(P0_HOUSING_PERSONAS))
        self.assertTrue(
            {request.split(";", 1)[0] for request in P0_HOUSING_PERSONAS.values()}
            <= self.architecture_scenarios()
        )
        for name, request in P0_HOUSING_PERSONAS.items():
            path = ROOT / "Tools" / "personas" / name
            self.assertTrue(path.is_file(), name)
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), name)
            self.assertEqual(request, found["REQUEST"], name)
            self.assertEqual(P0_HOUSING_SCRIPT, found["SCRIPT"], name)
            self.assertEqual(P0_HOUSING_EXPECT, found["EXPECT"], name)
            self.assertEqual(
                "architecture,visual,housing,progression,taste",
                found["SET"],
                name,
            )

    def test_medium_hut_has_one_exact_persona_per_cardinal_pose(self):
        self.assertEqual(
            {"north", "east", "south", "west"},
            {request.rsplit("=", 1)[1] for request in HUT_CARDINAL_PERSONAS.values()},
        )
        for name, request in HUT_CARDINAL_PERSONAS.items():
            path = ROOT / "Tools" / "personas" / name
            self.assertTrue(path.is_file(), name)
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), name)
            self.assertEqual(request, found["REQUEST"], name)
            self.assertEqual(P0_HOUSING_SCRIPT, found["SCRIPT"], name)
            self.assertEqual(P0_HOUSING_EXPECT, found["EXPECT"], name)
            self.assertEqual(
                "architecture,visual,housing,progression,taste",
                found["SET"],
                name,
            )

    def test_native_gallery_additions_freeze_exact_north_cases(self):
        self.assertEqual(3, len(NATIVE_GALLERY_PERSONAS))
        roster = self.architecture_scenarios()
        for name, (request, tags) in NATIVE_GALLERY_PERSONAS.items():
            path = ROOT / "Tools" / "personas" / name
            self.assertTrue(path.is_file(), name)
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), name)
            self.assertIn(request.split(";", 1)[0], roster, name)
            self.assertEqual(request, found["REQUEST"], name)
            self.assertEqual(P0_HOUSING_SCRIPT, found["SCRIPT"], name)
            self.assertEqual(P0_HOUSING_EXPECT, found["EXPECT"], name)
            self.assertEqual(tags, found["SET"], name)

    def test_native_gallery_additions_keep_exact_case_and_trust_contracts(self):
        roster = ET.parse(ROOT / "Harness" / "KingdomScenarios.xml").getroot()
        for key, expected_case in NATIVE_GALLERY_CASES.items():
            scenario = roster.find("scenario[@Key='%s']" % key)
            self.assertIsNotNone(scenario, key)
            self.assertEqual("architecture", scenario.attrib.get("Family"), key)
            self.assertEqual(
                "architecture-stamper", scenario.attrib.get("AuthorityClass"), key
            )
            self.assertEqual("false", scenario.attrib.get("Synthetic"), key)
            self.assertEqual("anchor-" + key, scenario.attrib.get("AnchorId"), key)
            parameter = scenario.find("param")
            self.assertIsNotNone(parameter, key)
            self.assertEqual(
                {"Name": "facing", "Domain": "north|east|south|west"},
                parameter.attrib,
                key,
            )
            stage = scenario.find("step[@Verb='StageGalleryCase']")
            self.assertIsNotNone(stage, key)
            actual_case = tuple(
                stage.attrib[name] for name in ("Build", "Type", "Size", "Variant")
            )
            self.assertEqual(expected_case, actual_case, key)
            self.assertEqual("{facing}", stage.attrib.get("Facing"), key)

    def test_every_persona_script_is_sealable_by_the_profile_tool(self):
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            extra = tuple(v for v in found["VERBS"].split(",") if v)
            lines = profile.parse_script(found["SCRIPT_WORDS"].split(), extra)
            self.assertTrue(lines, path.name)

    def test_every_persona_expectation_matches_its_script(self):
        """A persona whose EXPECT does not name the verbs it seals could never go green."""
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            if found.get("RELOAD"):
                self.assertEqual(found["RELOAD"], "quickstart")
                self.assertEqual(found["EXPECT"], "RELOAD-COMPLETE")
                self.assertTrue(found["SCRIPT_WORDS"].startswith("quickstart-save "))
                self.assertTrue(
                    matrix.assess(found, "", path.name), "journal alone must refuse"
                )
                continue  # Host workflow has two strictly checked journals, never a script replay.
            extra = tuple(v for v in found["VERBS"].split(",") if v)
            expected = [
                verb
                for verb, outcome, _ in matrix.parse_expect(
                    found["EXPECT"], path.name, extra
                )
                if outcome
            ]
            sealed = [
                line.split()[0]
                for line in profile.parse_script(found["SCRIPT_WORDS"].split(), extra)
            ]
            # A quickstart-lifecycle persona's first sealed line is the boot command itself, never
            # an EXPECT verb name (KingdomScenarioAutoRunner never dispatches it) -- Quickstart's
            # own boot machinery instead journals a leading run of QUICKSTART_EVIDENCE_ROWS for
            # that ONE sealed line, before the AutoRunner's own verbs ever run. Strip that leading
            # run so the remaining comparison still holds the same prefix law the ordinary case
            # already enforces.
            if sealed and sealed[0] == matrix.QUICKSTART_LIFECYCLE_VERB:
                boot_rows = 0
                for verb in expected:
                    if verb not in matrix.QUICKSTART_EVIDENCE_ROWS:
                        break
                    boot_rows += 1
                self.assertGreater(boot_rows, 0, path.name)
                expected = expected[boot_rows:]
                sealed = sealed[1:]
            # The script may stop early on a declared refusal, so expectations are a PREFIX of the
            # sealed verbs - never a different list, and never longer.
            self.assertLessEqual(len(expected), len(sealed), path.name)
            self.assertEqual(sealed[: len(expected)], expected, path.name)

    def test_every_architecture_persona_names_a_roster_scenario(self):
        roster = self.architecture_scenarios()
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            if "architecture" not in found["SET"].split(","):
                continue
            scenario = found["REQUEST"].split(";", 1)[0]
            self.assertIn(scenario, roster, path.name)

    def test_every_architecture_scenario_has_native_visual_coverage(self):
        covered = set()
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            tags = found["SET"].split(",")
            if "architecture" in tags and "visual" in tags:
                covered.add(found["REQUEST"].split(";", 1)[0])
        # The generic tent slice predates visual tagging but its four cardinal personas are still
        # native captures; every dossier-specific scenario must opt into the visual set directly.
        covered.add("arch-gallery-slice")
        self.assertEqual(set(), self.architecture_scenarios() - covered)

    def test_every_architecture_scenario_freezes_a_real_gallery_case(self):
        catalogue = self.architecture_cases()
        roster = ET.parse(ROOT / "Harness" / "KingdomScenarios.xml").getroot()
        for scenario in roster.findall("scenario"):
            if scenario.attrib.get("Family") != "architecture":
                continue
            stage = scenario.find("step[@Verb='StageGalleryCase']")
            self.assertIsNotNone(stage, scenario.attrib["Key"])
            case = (
                stage.attrib.get("Build", ""),
                stage.attrib.get("Type", "").lower(),
                stage.attrib.get("Size", "").lower(),
                stage.attrib.get("Variant", ""),
            )
            self.assertIn(case, catalogue, scenario.attrib["Key"])
            self.assertEqual("{facing}", stage.attrib.get("Facing"))


if __name__ == "__main__":
    unittest.main()
