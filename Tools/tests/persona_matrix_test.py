"""Adversarial tests for the persona manifest grammar and the journal assertion it carries.

These execute the authoritative implementation in Tools/personas/persona_matrix.py, which is what
Tools/run-personas.sh calls for every verdict. The shell script owns the game; everything that
decides PASS or FAIL is here, so the matrix's judgement is testable without a licensed install.
"""

from __future__ import annotations

import importlib.util
import pathlib
import unittest

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

    def test_unknown_check_is_refused(self):
        with self.assertRaises(SystemExit):
            matrix.parse_manifest(GREEN + "CHECK=whatever\n", "x.persona")


class ScriptGrammarTest(unittest.TestCase):
    def test_advance_folds_to_two_words(self):
        self.assertEqual(
            ["flatten", "advance", "300", "status"],
            matrix.script_words("flatten;advance 300;status", "x"),
        )

    def test_unsealable_verb_is_refused(self):
        for bad in ("capture", "help", "nonsense", "advance", "advance 0", "advance x"):
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
        for bad in ("realize", "capture", "Status", "my_verb"):
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

    def test_every_persona_parses(self):
        self.assertGreaterEqual(len(self.personas()), 6)
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            self.assertTrue(found["REQUEST"])
            self.assertTrue(found["SCRIPT_WORDS"])

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
            # The script may stop early on a declared refusal, so expectations are a PREFIX of the
            # sealed verbs - never a different list, and never longer.
            self.assertLessEqual(len(expected), len(sealed), path.name)
            self.assertEqual(sealed[: len(expected)], expected, path.name)


if __name__ == "__main__":
    unittest.main()
