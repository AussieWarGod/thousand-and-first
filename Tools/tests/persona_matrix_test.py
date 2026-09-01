"""Adversarial tests for the persona manifest grammar and the journal assertion it carries.

These execute the authoritative implementation in Tools/personas/persona_matrix.py, which is what
Tools/run-personas.sh calls for every verdict. The shell script owns the game; everything that
decides PASS or FAIL is here, so the matrix's judgement is testable without a licensed install.
"""

from __future__ import annotations

import importlib.util
import pathlib
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
P0_HOUSING_EXPECT = (
    "flatten:OK,realize:OK,advance:OK,frame:OK,status:OK,COMPLETE"
)

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
        self.assertEqual(56, len(self.personas()))
        for path in self.personas():
            found = matrix.parse_manifest(path.read_text(encoding="utf-8"), path.name)
            self.assertTrue(found["REQUEST"])
            self.assertTrue(found["SCRIPT_WORDS"])

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
