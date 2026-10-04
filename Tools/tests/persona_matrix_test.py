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
import re
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

HARNESS = ROOT / "Harness"
# String and char literals and line comments, blanked to equal width before braces are matched,
# so offsets into the blanked text are offsets into the source.
CS_NOISE = re.compile(r'@?"(?:[^"\\\n]|\\.)*"|\'(?:[^\'\\\n]|\\.)\'|//[^\n]*')
CS_JOURNAL_ROW = re.compile(r'KingdomScenarioJournal\.Append\("([a-z0-9-]+)"')


def harness_method(name: str) -> str:
    """The source body of the ONE Harness method declared `void <name>(`, braces balanced."""
    hits = []
    for path in sorted(HARNESS.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for found in re.finditer(r"\bvoid\s+%s\s*\(" % re.escape(name), text):
            hits.append((text, found.end()))
    if len(hits) != 1:
        raise AssertionError("%d Harness declarations of void %s(" % (len(hits), name))
    text, start = hits[0]
    blank = CS_NOISE.sub(lambda m: " " * len(m.group(0)), text)
    opened = blank.index("{", start)
    depth = 0
    for index in range(opened, len(blank)):
        depth += {"{": 1, "}": -1}.get(blank[index], 0)
        if depth == 0:
            return text[opened : index + 1]
    raise AssertionError("void %s( never closes" % name)


def supply_rows(target: int) -> list[str]:
    """The observation rows SupplyChain(target) journals, in the order the harness writes them:
    its per-rung probes in statement order, each resolved through its own journal Append. Derived
    from Harness/KingdomCampHeartChainPayment.cs rather than restated, so a persona EXPECT that
    lists them in any other order fails offline (#264 review: exotics is journalled first)."""
    body = harness_method("SupplyChain")
    calls = [
        (found.start(), found.group(2))
        for found in re.finditer(r"if \(Target == (\d)\) (\w+)\(\);", body)
        if int(found.group(1)) == target
    ]
    chain = re.search(
        r"if \(Target == 3\) \{([^{}]*)\}\s*else if \(Target == 4\) \{([^{}]*)\}"
        r"\s*else \{([^{}]*)\}",
        body,
    )
    if chain is None:
        raise AssertionError("SupplyChain lost its per-rung probe chain")
    group = {3: 1, 4: 2, 5: 3}[target]
    calls.extend(
        (chain.start(group) + found.start(), found.group(1))
        for found in re.finditer(r"(\w+)\(\);", chain.group(group))
    )
    rows: list[str] = []
    for _, method in sorted(calls):
        journalled = CS_JOURNAL_ROW.findall(harness_method(method))
        if not journalled:
            raise AssertionError("%s journals no observation row" % method)
        rows.extend(journalled)
    return rows


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
        # Both ends of the inclusive band, by value: the ceiling moved for the rung-5 persona and a
        # test that only checked MAX_TIMEOUT + 1 would have passed at any ceiling at all.
        self.assertEqual(7200, matrix.MAX_TIMEOUT)
        self.assertEqual(1, matrix.parse_timeout("1", "x"))
        self.assertEqual(matrix.MAX_TIMEOUT,
                         matrix.parse_timeout(str(matrix.MAX_TIMEOUT), "x"))
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


class ScriptVerbBoundTest(unittest.TestCase):
    """The runner's sealed-script verb bound, mirrored so `load` and `fields` refuse a persona the
    runner would refuse before its first verb (native run 4ce2a6a1: 35 verbs against the old 32)."""

    RUNTIME = ROOT / "Harness" / "KingdomScenarioScriptRules.cs"
    RUNG5 = ROOT / "Tools" / "personas" / "camp-heart-rung5-native-check.persona"

    @staticmethod
    def script(verbs: int) -> str:
        # The last step is a counted verb: two shell words, one sealed line, ONE runtime verb.
        return ";".join(["status"] * (verbs - 1) + ["advance 1200"])

    @staticmethod
    def manifest(verbs: int) -> str:
        return (
            "REQUEST=founding-first-city\nSCRIPT=%s\nEXPECT=status:OK,COMPLETE\n"
            % ScriptVerbBoundTest.script(verbs)
        )

    def test_the_bound_is_one_value_in_the_runtime_and_both_tools(self):
        declared = re.findall(
            r"^\s*internal const int MaxVerbs = (\d+);\r?$",
            self.RUNTIME.read_text(encoding="utf-8"),
            re.M,
        )
        self.assertEqual(["48"], declared)
        self.assertEqual(48, matrix.MAX_SCRIPT_VERBS)
        self.assertEqual(matrix.MAX_SCRIPT_VERBS, profile.MAX_SCRIPT_VERBS)

    def test_exactly_the_bound_is_accepted_and_advance_counts_once(self):
        lines = matrix.script_lines(self.script(48), "x")
        self.assertEqual(48, len(lines))
        self.assertEqual("advance 1200", lines[-1])
        self.assertEqual(49, len(matrix.script_words(self.script(48), "x")))
        found = matrix.parse_manifest(self.manifest(48), "x")
        self.assertEqual(49, len(found["SCRIPT_WORDS"].split()))

    def test_one_verb_past_the_bound_is_refused_by_every_entry(self):
        for call in (
            lambda: matrix.script_words(self.script(49), "x"),
            lambda: matrix.script_lines(self.script(49), "x"),
            lambda: matrix.parse_manifest(self.manifest(49), "x"),
        ):
            with self.subTest(call=call), self.assertRaises(SystemExit) as caught:
                call()
            self.assertIn("declares 49 verbs, over the 48-verb bound", str(caught.exception))

    def test_fields_refuses_a_persona_past_the_bound(self):
        with tempfile.TemporaryDirectory() as directory:
            persona = pathlib.Path(directory) / "long.persona"
            for verbs, code in ((48, 0), (49, 1)):
                persona.write_text(self.manifest(verbs), encoding="utf-8")
                result = subprocess.run(
                    [sys.executable, str(SPEC.origin), "fields", str(persona)],
                    capture_output=True,
                    check=False,
                )
                with self.subTest(verbs=verbs):
                    self.assertEqual(code, result.returncode, result.stderr)
            self.assertIn(b"declares 49 verbs, over the 48-verb bound", result.stderr)
            self.assertEqual(b"", result.stdout)

    def test_the_rung_five_persona_seals_thirty_five_verbs(self):
        found = matrix.parse_manifest(self.RUNG5.read_text(encoding="utf-8"), self.RUNG5.name)
        extra = tuple(found["VERBS"].split(","))
        lines = matrix.script_lines(found["SCRIPT"], self.RUNG5.name, extra)
        self.assertEqual(35, len(lines))
        sealed = profile.SCRIPT_HEADER + "\n".join(lines) + "\n"
        self.assertEqual(lines, matrix.runtime_verbs(sealed))
        self.assertEqual(lines, profile.runtime_verbs(sealed))
        self.assertLessEqual(len(lines), matrix.MAX_SCRIPT_VERBS)

    def test_the_count_reads_lines_exactly_as_the_runner_does(self):
        for count in (matrix.runtime_verbs, profile.runtime_verbs):
            with self.subTest(tool=count.__module__):
                self.assertEqual(
                    ["status", "advance 1200"],
                    count("# header\n\n   \n  status  \r\nadvance 1200\r\t# indented\n"),
                )
                # .NET white space trims (NBSP, line separator, vertical tab, form feed) without
                # splitting the line; U+001C stays a verb although str.isspace calls it white space.
                self.assertEqual(["status"], count("\u00a0status\u2028\n"))
                self.assertEqual(["status"], count("\x0bstatus\x0c"))
                self.assertEqual(["\x1c"], count("\x1c\n"))
                self.assertEqual(["a # b"], count(" a # b \n"))
                self.assertEqual([], count(""))

    def test_both_tools_agree_on_white_space_for_every_basic_plane_character(self):
        for code in range(0x10000):
            char = chr(code)
            self.assertEqual(
                profile.dotnet_white_space(char), matrix.dotnet_white_space(char), hex(code)
            )

    def test_every_shipped_persona_seals_the_lines_the_profile_tool_writes(self):
        top = sorted((ROOT / "Tools" / "personas").glob("*.persona"))
        cross = sorted((ROOT / "Tools" / "personas" / "cross-version").glob("*.persona"))
        # The count itself is ShippedPersonaTest's one pin; this proves both trees are walked.
        self.assertIn(self.RUNG5, top)
        self.assertTrue(cross)
        for path in top + cross:
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            if found.get("RELOAD"):
                continue  # a reload seals the one fixed Quickstart save command
            extra = tuple(verb for verb in found["VERBS"].split(",") if verb)
            lines = matrix.script_lines(found["SCRIPT"], path.name, extra)
            with self.subTest(persona=path.name):
                self.assertEqual(profile.parse_script(found["SCRIPT_WORDS"].split(), extra), lines)
                self.assertLessEqual(len(lines), matrix.MAX_SCRIPT_VERBS)


class HarnessJournalOrderTest(unittest.TestCase):
    """Observation-row orders read from the harness that writes them (#264 review: the rung-5
    persona listed the supply rows in an order SupplyChain never journals, and the old host
    table restated the persona, so a correct native run would have failed at row 41)."""

    RUNG5 = ROOT / "Tools" / "personas" / "camp-heart-rung5-native-check.persona"

    def test_the_derivation_reads_the_natively_accepted_four_rung_orders(self):
        # Native30's accepted journal: spatial (HoldChainTent, before SupplyChain), occupancy,
        # road-wear before supply 3; renovation, survey-stakes before supply 4.
        self.assertEqual(["camp-heart-chain-occupancy", "camp-heart-chain-road-wear"], supply_rows(3))
        self.assertEqual(["camp-heart-chain-renovation", "camp-heart-chain-survey-stakes"], supply_rows(4))

    def test_the_rung_five_supply_rows_follow_the_harness_call_order(self):
        rows = supply_rows(5)
        self.assertEqual(3, len(rows))
        self.assertEqual(3, len(set(rows)))
        payment = harness_method("SupplyChain")
        self.assertLess(payment.index("if (Target == 5) SupplyChainHighCraft();"),
                        payment.index("ProveChainRetainedStakes();"))
        found = matrix.parse_manifest(self.RUNG5.read_text(encoding="utf-8"), self.RUNG5.name)
        expected = matrix.parse_expect(found["EXPECT"], self.RUNG5.name,
                                       tuple(found["VERBS"].split(",")))
        supply = [index for index, item in enumerate(expected)
                  if item[0] == "camp-heart-chain-supply" and item[2] == "target=5"]
        self.assertEqual(1, len(supply))
        start = supply[0] - len(rows)
        self.assertEqual(rows, [item[0] for item in expected[start:supply[0]]])

    def test_a_journal_in_harness_order_passes_and_the_old_order_fails(self):
        found = matrix.parse_manifest(self.RUNG5.read_text(encoding="utf-8"), self.RUNG5.name)
        expected = matrix.parse_expect(found["EXPECT"], self.RUNG5.name,
                                       tuple(found["VERBS"].split(",")))
        journal = [(verb, outcome or "OK", wanted) for verb, outcome, wanted in expected]
        self.assertEqual([], matrix.match(expected, journal))
        rows = supply_rows(5)
        at = [item[0] for item in journal].index(rows[0])
        swapped = list(journal)
        swapped[at : at + 3] = [journal[at + 1], journal[at + 2], journal[at]]
        self.assertTrue(matrix.match(expected, swapped))

    def test_the_method_reader_balances_braces_past_literals_and_comments(self):
        body = harness_method("SupplyChainHighCraft")
        self.assertTrue(body.startswith("{") and body.endswith("}"))
        self.assertEqual(["camp-heart-chain-exotics"], CS_JOURNAL_ROW.findall(body))
        with self.assertRaises(AssertionError):
            harness_method("Require")  # declared by several harness classes: never guessed


class HandoverWaitPlacementTest(unittest.TestCase):
    """Which scripted wait each paid heart handover lands in, derived from the quoted ticks (#264
    review: the rung-5 persona listed the arcology's renovation rows after a fourth 6600-turn
    wait, while that handover falls inside the third, so a correct native run would have failed
    its positional match about 32000 turns in).

    The rule. A paid heart improvement is quoted its successor's authored Ticks at
    KingdomUpgradeRules.BuildTicksPercent, after the district factor, which stays neutral because
    only the charter assigns a district (Core/KingdomCharterPart.Civic.cs). The settlement pass
    that begins it is the first daily boundary after the supply verb, so the paid check, which
    closes the next `advance 1200`, comes 0..1200 turns after it. Receipt-bearing labour advances
    only at daily passes (Growth/KingdomScaffold.cs TurnTick), so it completes at the first pass
    at or after its due tick, and the handover, where the post-payment probe journals its witness
    rows, runs at the pass after that. Native30 recorded exactly this for all three paid handovers.
    """

    PERSONAS = ("camp-heart-chain.persona", "camp-heart-rung5-native-check.persona")
    # Native30 (beta-heart-chain/a6e23f74/paid-court-renovation-chain-1/run-Player.log, relative
    # to the evidence root): the semantic boundary each paid improvement began on ("improvement
    # begun ... ticks=N" inside that boundary's pass), its quoted ticks, and the boundary whose
    # pass logged "heart rung raised".
    NATIVE30 = ((414000, 2250, 417600), (421200, 4500, 427200), (429600, 9000, 440400))
    PAID = re.compile(r"paid-heart-chain paid; target=(\d)\Z")

    @staticmethod
    def constant(relative: str, pattern: str) -> int:
        found = re.findall(pattern, (ROOT / relative).read_text(encoding="utf-8"))
        if len(found) != 1:
            raise AssertionError("%s: %d matches for %s" % (relative, len(found), pattern))
        return int(found[0])

    def day(self) -> int:
        clock = (ROOT / "Simulation" / "City" / "KingdomSemanticClockRules.cs").read_text(encoding="utf-8")
        self.assertIn("public const long CadenceTicks = KingdomRules.TicksPerDay;", clock)
        return self.constant("Core/KingdomRules.Economy.cs", r"public const long TicksPerDay = (\d+)L;")

    @staticmethod
    def scale(ticks: int, percent: int) -> int:
        # KingdomUpgradeRules.ScaleTicks, with production's integer floors.
        return ticks // 100 * percent + ticks % 100 * percent // 100

    def quoted(self, rung: int) -> int:
        """BuildTicks of the paid improvement that raises the heart to `rung`."""
        root = ET.parse(ROOT / "RuntimeData" / "KingdomBuildings.xml").getroot()
        designs = {item.get("Key"): item for item in root.iter("building")}
        chain, key = [], "heartbasin"
        while key:
            chain.append(key)
            key = designs[key].get("UpgradesTo")
        self.assertEqual(5, len(chain))
        override = int(designs[chain[rung - 2]].get("UpgradeTicks") or 0)
        if override > 0:
            return override
        neutral = self.constant("Core/KingdomRules.Districts.cs",
                                r"public const int DistrictNeutralPercent = (\d+);")
        percent = self.constant("Growth/KingdomUpgradeRules.cs",
                                r"public const int BuildTicksPercent = (\d+);")
        fresh = int(designs[chain[rung - 1]].get("Ticks"))
        return max(1, self.scale(max(1, self.scale(fresh, neutral)), percent))

    def handover_after_begin(self, ticks: int) -> int:
        day = self.day()
        return (-(-ticks // day) + 1) * day

    def window(self, rung: int) -> tuple[int, int]:
        """Earliest and latest handover, in turns after the paid check. The check closes an
        `advance 1200` that may run 1201 turns, and a pass runs on the first turn at or after its
        boundary, so one turn of slack is allowed at each end."""
        latest = self.handover_after_begin(self.quoted(rung))
        return latest - self.day() - 1, latest + 1

    @staticmethod
    def holding_wait(waits: list[int], earliest: int, latest: int) -> tuple[int, int, int]:
        """(1-based wait, turns before, turns after) for the one wait holding the whole window."""
        start = 0
        for index, turns in enumerate(waits):
            # Each earlier wait may run one turn long, so this one may start that much later.
            late_start, end = start + index, start + turns
            if late_start < earliest and latest < end:
                return index + 1, earliest - late_start, end - latest
            start = end
        return 0, 0, 0

    def placements(self, name: str) -> dict[int, tuple[list[int], int]]:
        """Per paid rung: the script's waits between its paid and completion checks, and how many
        of those waits the persona's EXPECT lists before the handover's witness rows."""
        path = ROOT / "Tools" / "personas" / name
        found = matrix.parse_manifest(path.read_text(encoding="utf-8"), name)
        extra = tuple(found["VERBS"].split(","))
        expected = matrix.parse_expect(found["EXPECT"], name, extra)
        steps = matrix.script_lines(found["SCRIPT"], name, extra)
        checks = [at for at, step in enumerate(steps) if step == "camp-heart-chain-check"]
        rows = [at for at, item in enumerate(expected) if item[0] == "camp-heart-chain-check"]
        self.assertEqual(len(checks), len(rows), name)
        result = {}
        for ordinal, at in enumerate(rows):
            paid = self.PAID.search(expected[at][2])
            if paid is None:
                continue
            rung = int(paid.group(1))
            between = steps[checks[ordinal] + 1 : checks[ordinal + 1]]
            self.assertTrue(between and all(step.startswith("advance ") for step in between), name)
            listed = [item[0] for item in expected[at + 1 : rows[ordinal + 1]]]
            witnesses = [index for index, verb in enumerate(listed) if verb != "advance"]
            self.assertTrue(witnesses, "%s rung %d lists no handover witness" % (name, rung))
            self.assertEqual(list(range(witnesses[0], witnesses[-1] + 1)), witnesses, name)
            self.assertEqual(len(between), len(listed) - len(witnesses), name)
            result[rung] = ([int(step.split()[1]) for step in between], witnesses[0])
        return result

    def test_the_rule_reproduces_native30s_three_paid_handovers(self):
        for rung, (begin, ticks, raised) in zip((2, 3, 4), self.NATIVE30):
            with self.subTest(rung=rung):
                self.assertEqual(ticks, self.quoted(rung))
                self.assertEqual(raised, begin + self.handover_after_begin(ticks))

    def test_every_paid_handover_lands_inside_the_wait_its_witnesses_follow(self):
        day = self.day()
        seen = set()
        for name in self.PERSONAS:
            for rung, (waits, listed) in self.placements(name).items():
                seen.add(rung)
                earliest, latest = self.window(rung)
                held, before, after = self.holding_wait(waits, earliest, latest)
                with self.subTest(persona=name, rung=rung):
                    self.assertNotEqual(0, held, "handover %d..%d straddles waits %r"
                                        % (earliest, latest, waits))
                    self.assertEqual(held, listed, "EXPECT lists the witnesses after wait %d; the "
                                     "handover lands in wait %d of %r" % (listed, held, waits))
                    # A daily pass of slack before the earliest handover, so no drift of the pass
                    # phase can put the witnesses ahead of their wait's advance row; and when
                    # another wait follows, a pass of slack after the latest one, so a labour
                    # shortfall or a retry adding a pass cannot carry them past the next advance
                    # row. After the last wait comes the completion check, which refuses an
                    # unfinished handover by name.
                    self.assertGreaterEqual(before, day)
                    if held < len(waits):
                        self.assertGreaterEqual(after, day)
        self.assertEqual({3, 4, 5}, seen)

    def test_the_fixture_assigns_no_district_so_the_quote_stays_neutral(self):
        writes = re.compile(r"ZoneDistricts\s*(?:\[[^\]]*\]\s*=(?!=)|\.(?:Add|Remove|Clear)\(|=(?!=))")
        for path in sorted(HARNESS.glob("*.cs")):
            with self.subTest(shard=path.name):
                self.assertIsNone(writes.search(path.read_text(encoding="utf-8")))
        self.assertTrue(writes.search("System.ZoneDistricts[zone.ZoneID] = district;"))
        self.assertIsNone(writes.search("System.ZoneDistricts != null && System.ZoneDistricts.Count == 0"))
        self.assertIn("System.ZoneDistricts[zone.ZoneID] = district;",
                      (ROOT / "Core" / "KingdomCharterPart.Civic.cs").read_text(encoding="utf-8"))
        self.assertIn("// Receipt-bearing work advances only from KingdomConstruction.OnSettlementPass.",
                      (ROOT / "Growth" / "KingdomScaffold.cs").read_text(encoding="utf-8"))


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
    def test_camp_diagnostics_do_not_hide_failed_observations(self):
        expected = [("camp-heart-check", "OK", ""), ("SCRIPT-COMPLETE", "OK", "")]
        for diagnostic in ("TESTGROUND-CENSUS", "camp-resident-movement", "camp-vortex-origin"):
            for outcome in ("OK", "REFUSED"):
                with self.subTest(diagnostic=diagnostic, outcome=outcome):
                    rows = [(diagnostic, outcome, "observation"), *expected]
                    problems = matrix.match(expected, matrix.significant(rows))
                    if outcome == "OK":
                        self.assertEqual([], problems)
                    else:
                        self.assertTrue(problems)

    def test_home_damage_requires_exact_paired_witness_with_recovery(self):
        manifest = matrix.parse_manifest(
            "REQUEST=founding-first-city\nSCRIPT=home-map-damage-native\n"
            "VERBS=home-map-damage-native\nEXPECT=home-map-damage-native:OK~cases=1 passed=1 failed=0,COMPLETE\n"
            "SET=growth,native-regression\n", "home damage")
        detail = ("resident=1; body=511; captured=3; absent-owner-matches=1; recorded=3; due=192327; "
                  "absent-brink=true; home-cleared=true; chronology-retained=true; ordinary-rehousing=true; "
                  "synthetic-damage=true; synthetic-repair=true; synthetic-housing=false; paid-repair=false")
        prefix = "native-home-damage cases=1 passed=1 failed=0; "
        observation = row("home-map-damage-observation", "OK", detail)
        success = row("home-map-damage-native", "OK", prefix + detail)
        terminal = row("SCRIPT-COMPLETE", "OK")
        self.assertEqual([], matrix.assess(manifest, journal(observation, success, terminal), "home damage"))
        bad_rows = [(success, terminal), (observation, observation, success, terminal),
                    (success, observation, terminal), (observation, success, success, terminal),
                    (row("home-map-damage-observation", "REFUSED", detail), success, terminal),
                    (observation, row("home-map-damage-native", "REFUSED", prefix + detail), terminal)]
        for changed in (detail.replace("captured=3", "captured=2"),
                        detail.replace("recorded=3", "recorded=2"),
                        detail.replace("chronology-retained=true", "chronology-retained=false"),
                        detail.replace("ordinary-rehousing=true", "ordinary-rehousing=false"),
                        detail.replace("due=192327", "due=-1"),
                        detail.replace("resident=1", "resident=0"),
                        detail.replace("body=511", "body="),
                        detail + "; body=511", detail + "; unexpected=true",
                        detail.replace("paid-repair=false", "paid-repair=true")):
            bad_rows.append((row("home-map-damage-observation", "OK", changed),
                             row("home-map-damage-native", "OK", prefix + changed), terminal))
        bad_rows.append((observation, row("home-map-damage-native", "OK", prefix + detail.replace("due=192327", "due=192328")), terminal))
        for rows in bad_rows:
            with self.subTest(rows=rows):
                self.assertTrue(matrix.assess(manifest, journal(*rows), "home damage"))


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
                row("town-lots", "OK"),
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
    def test_paid_housing_retry_is_required_once_with_physical_verdict(self):
        name = "paid-housing-native-check.persona"
        found = matrix.parse_manifest((ROOT / "Tools/personas" / name).read_text(), name)
        expected = matrix.parse_expect(found["EXPECT"], name, found["VERBS"].split(","))
        rows = [(verb, outcome or "OK", wanted) for verb, outcome, wanted in expected]
        self.assertEqual([], matrix.match(expected, rows))
        for witness, good, bad in (("paid-housing-water", "conserved=true", "conserved=false"),
                                   ("paid-housing-physical-probes", "restored=exact", "restored=false"),
                                   ("paid-housing-retry", "phase=Outstanding", "phase=Working"),
                                   ("paid-housing-floor-access", "restored=exact", "restored=false"),
                                   ("paid-housing-cohort", "housed=4", "housed=2")):
            index = next(i for i, item in enumerate(rows) if item[0] == witness)
            self.assertTrue(matrix.match(expected, rows[:index] + rows[index + 1:]))
            self.assertTrue(matrix.match(expected, rows[:index] + [rows[index]] + rows[index:]))
            for outcome, reason in (("REFUSED", good), ("OK", bad)):
                changed = list(rows)
                changed[index] = (witness, outcome, reason)
                self.assertTrue(matrix.match(expected, changed))

    def test_native_room_witnesses_are_required_once_in_order_with_their_verdicts(self):
        name = "lodging-room-native.persona"
        found = matrix.parse_manifest((ROOT / "Tools/personas" / name).read_text(), name)
        expected = matrix.parse_expect(found["EXPECT"], name, ("lodging-room-native",))
        witnesses = [item[0] for item in expected if item[0].startswith("room-")]
        self.assertEqual(list(matrix.ROOM_EVIDENCE_ROWS), witnesses)
        self.assertEqual(29, len(witnesses))
        rows = [(verb, outcome or "OK", wanted) for verb, outcome, wanted in expected]
        self.assertEqual([], matrix.match(expected, rows))
        for index, (verb, outcome, wanted) in enumerate(rows):
            if verb not in witnesses:
                continue
            with self.subTest(witness=verb):
                self.assertTrue(matrix.match(expected, rows[:index] + rows[index + 1:]))
                self.assertTrue(matrix.match(expected, rows[:index] + [rows[index]] + rows[index:]))
                changed = list(rows)
                changed[index] = (verb, "REFUSED", wanted)
                self.assertTrue(matrix.match(expected, changed))
                changed[index] = (verb, outcome, "wrong physical result")
                self.assertTrue(matrix.match(expected, changed))

    def test_paid_handover_witnesses_cannot_be_missing_repeated_or_refused(self):
        witnesses = ("camp-heart-chain-handover-refusals", "camp-heart-chain-handover-cleared",
                     "camp-heart-chain-retry-obstruction", "camp-heart-chain-retry-outstanding",
                     "camp-heart-chain-retry-removal", "camp-heart-chain-renovation",
                     "camp-heart-chain-renovation-refusals", "camp-heart-chain-renovation-cleared")
        spec = "advance:OK," + ",".join(name + ":OK~proved" for name in witnesses) + ",COMPLETE"
        expected = matrix.parse_expect(spec, "handover")
        rows = [("advance", "OK", "")] + [(name, "OK", "proved") for name in witnesses]
        rows.append(("SCRIPT-COMPLETE", "OK", ""))
        self.assertEqual([], matrix.match(expected, rows))
        for index in range(1, len(witnesses) + 1):
            with self.subTest(witness=rows[index][0]):
                self.assertTrue(matrix.match(expected, rows[:index] + rows[index + 1:]))
                self.assertTrue(matrix.match(expected, rows[:index] + [rows[index]] + rows[index:]))
                refused = list(rows)
                refused[index] = (rows[index][0], "REFUSED", "proved")
                self.assertTrue(matrix.match(expected, refused))
                wrong = list(rows)
                wrong[index] = (rows[index][0], "OK", "unwitnessed")
                self.assertTrue(matrix.match(expected, wrong))
        trace = ("camp-heart-chain-removal", "OK", "natural reproof")
        self.assertEqual(rows, matrix.significant(rows[:1] + [trace, trace] + rows[1:]))
        refusal = ("camp-heart-chain-removal", "REFUSED", "identity changed")
        self.assertIn(refusal, matrix.significant(rows + [refusal]))
        self.assertTrue(matrix.match(expected, matrix.significant(rows[:1] + [trace] + rows[2:])))

    def test_arcology_witnesses_cannot_be_missing_repeated_or_refused(self):
        """The rung-5 persona's own EXPECT binds its three new observation rows - the crown seed's
        book read, the high-craft supply and the arcology's preflight and standing reads - by
        position, outcome and reading (#264 review; docs/DEVELOPMENT.md: test missing, duplicate
        and refused evidence for every new observation row)."""
        name = "camp-heart-rung5-native-check.persona"
        found = matrix.parse_manifest((ROOT / "Tools/personas" / name).read_text(encoding="utf-8"), name)
        expected = matrix.parse_expect(found["EXPECT"], name, tuple(found["VERBS"].split(",")))
        rows = [(verb, outcome or "OK", wanted) for verb, outcome, wanted in expected]
        self.assertEqual([], matrix.match(expected, rows))
        # Never bookkeeping and never a non-positional diagnostic, so a match sees every one.
        self.assertEqual(rows, matrix.significant(rows))
        new = ("camp-heart-chain-crown", "camp-heart-chain-exotics", "camp-heart-chain-arcology")
        for verb in new:
            self.assertIn(verb, matrix.CAMP_HEART_EVIDENCE_ROWS)
        indices = [index for index, row in enumerate(rows) if row[0] in new]
        self.assertEqual(list(new) + ["camp-heart-chain-arcology"], [rows[index][0] for index in indices])
        for index in indices:
            verb, outcome, wanted = rows[index]
            with self.subTest(witness=verb, row=index + 1):
                self.assertTrue(wanted, "the witness binds a reading, not only its name")
                self.assertTrue(matrix.match(expected, rows[:index] + rows[index + 1:]))
                self.assertTrue(matrix.match(expected, rows[:index] + [rows[index]] + rows[index:]))
                changed = list(rows)
                changed[index] = (verb, "REFUSED", wanted)
                self.assertTrue(matrix.match(expected, changed))
                changed[index] = (verb, outcome, "unwitnessed")
                self.assertTrue(matrix.match(expected, changed))
        # The two arcology reads are not interchangeable: preflight at rung four, standing at five.
        first, second = indices[2], indices[3]
        swapped = list(rows)
        swapped[first] = rows[first][:2] + (rows[second][2],)
        swapped[second] = rows[second][:2] + (rows[first][2],)
        self.assertTrue(matrix.match(expected, swapped))

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

    def test_terminal_shorthand_requires_its_fixed_outcome(self):
        for terminal, verb, good in (
            ("COMPLETE", "SCRIPT-COMPLETE", "OK"),
            ("STOPPED", "SCRIPT-STOPPED", "REFUSED"),
            ("GATE-REFUSED", "GATE-REFUSED", "REFUSED"),
        ):
            with self.subTest(terminal=terminal):
                expected = matrix.parse_expect(terminal, "x.persona")
                self.assertEqual([], matrix.match(expected, [(verb, good, "")]))
                bad = "REFUSED" if good == "OK" else "OK"
                self.assertTrue(matrix.match(expected, [(verb, bad, "")]))

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
        self.assertEqual(109, len(self.personas()))
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            self.assertTrue(found["REQUEST"])
            self.assertTrue(found["SCRIPT_WORDS"])

    def test_quickstart_sight_accepts_full_boot_and_refuses_missing_or_failed_steps(self):
        name = "quickstart-sight-native-check.persona"
        found = matrix.parse_manifest(
            (ROOT / "Tools" / "personas" / name).read_text(encoding="utf-8"), name
        )
        boot = [
            "QUICKSTART-BOOT-BEGIN", "QUICKSTART-BOOT-OBSERVED", "QUICKSTART-BOOT-COMPLETE",
            "QUICKSTART-BUILD-BEGIN", "QUICKSTART-BUILD-QUOTE", "QUICKSTART-BUILD-CANPAY",
            "QUICKSTART-BUILD-COMMISSION", "QUICKSTART-LIFECYCLE-PROFILE",
        ]
        rows = [row(verb, "OK") for verb in boot] + [
            row("QUICKSTART-BUILD-COMPLETE", "OK", "commissioned=true"),
            row("quickstart-sight-start", "OK", "attached-at-founding=true"),
            row("yield-frames", "OK"),
            row("quickstart-sight-check", "OK", "whole-zone-drawn=true"),
            row("stagedigest", "OK"), row("SCRIPT-COMPLETE", "OK"),
        ]
        self.assertEqual([], matrix.assess(found, journal(*rows), name))
        for i in range(len(rows)):
            with self.subTest(missing=i):
                self.assertTrue(matrix.assess(found, journal(*(rows[:i] + rows[i + 1:])), name))
            with self.subTest(refused=i):
                changed = list(rows)
                changed[i] = changed[i].replace("\tOK\t", "\tREFUSED\t")
                self.assertTrue(matrix.assess(found, journal(*changed), name))

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
            # The shortage observation is emitted inside the supply verb; it has no dispatch line.
            if "guest-save-shortage" in expected:
                self.assertIn("guest-save-supply", sealed, path.name)
                self.assertEqual(expected.index("guest-save-shortage") + 1,
                                 expected.index("guest-save-supply"), path.name)
                expected.remove("guest-save-shortage")
            for observation, verb in (("camp-heart-save-custody", "camp-heart-save"),
                                      ("camp-heart-chain-founder", "camp-heart-chain-setup"),
                                      ("camp-heart-chain-input", "stagedigest")):
                if observation in expected:
                    self.assertIn(verb, sealed, path.name)
                    self.assertEqual(expected.index(observation) + 1,
                                     expected.index(verb), path.name)
                    expected.remove(observation)
            for observations, next_verb in (
                (("camp-heart-chain-spatial", "camp-heart-chain-occupancy",
                  "camp-heart-chain-road-wear"), "camp-heart-chain-supply"),
                (("camp-heart-chain-handover-refusals", "camp-heart-chain-handover-cleared",
                  "camp-heart-chain-retry-obstruction", "camp-heart-chain-retry-outstanding",
                  "camp-heart-chain-retry-removal"), "camp-heart-chain-check"),
                (("camp-heart-chain-renovation", "camp-heart-chain-survey-stakes"), "camp-heart-chain-supply"),
                (("camp-heart-chain-renovation-refusals", "camp-heart-chain-renovation-cleared"),
                 "camp-heart-chain-check"),
                # The arcology leg: the crown seed, then the fifth rung's supply rows in the order
                # SupplyChain journals them (derived from the harness, never restated), then the
                # standing read before the final check.
                (("camp-heart-chain-crown",), "camp-heart-chain-capital"),
                (tuple(supply_rows(5)), "camp-heart-chain-supply"),
                (("camp-heart-chain-renovation-refusals", "camp-heart-chain-renovation-cleared"),
                 "camp-heart-chain-check"),
                (("camp-heart-chain-arcology",), "camp-heart-chain-check"),
                (matrix.ROOM_EVIDENCE_ROWS, "lodging-room-native"),
                (matrix.PAID_HOUSING_EVIDENCE_ROWS[:2], "paid-housing-pay"),
                (matrix.PAID_HOUSING_EVIDENCE_ROWS[2:], "paid-housing-complete"),
            ):
                if any(name in expected for name in observations):
                    start = expected.index(observations[0])
                    self.assertEqual(list(observations) + [next_verb],
                                     expected[start:start + len(observations) + 1], path.name)
                    del expected[start:start + len(observations)]
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
