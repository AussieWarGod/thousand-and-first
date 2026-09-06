"""Executable synthetic journal/log fixtures, not engine execution or save/load acceptance."""
from __future__ import annotations

import contextlib
import importlib.util
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("scenario_rung_load_verdict", TOOLS / "verify-scenario-rung-load.py")
verdict = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(verdict)
finally:
    sys.path.pop(0)

GAME_ID = "01234567-89ab-cdef-0123-456789abcdef"
WARNINGS = [
    "MODWARN [Pets of Harvest Dawn] - Mod defining manual load order, please convert it to use the Dependencies field.",
    "MODWARN [Pets of Harvest Dawn] - XmlDataHelper:: <...>/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/DLC/PetsPack1/Freehold_Pet_Ercolano/PopulationTables.xml line 4 char 6",
]


def journal(activation=False):
    # Independent literal fixture of the agreed native witness messages.
    messages = [
        ("LOAD-BEGIN", "exact sealed save; game-id=" + GAME_ID + "; new-game=false; mod-restore=false"),
        ("LOAD-PREACTIVATION", "exact ss5/sr2 bytes and 35 loaded bodies; release=Intent; native-write-cut=2; wear-fields=10; autoonce=true; before-AfterGameLoaded-handlers-and-zone-activation=true"),
        ("LOAD-RECOVERY", "release=Released; population=35; original-step-retired=true; replay=false; status=clear; activation-already-recovered=" + str(activation).lower()),
        ("SCRIPT-COMPLETE", "native-load cases=2 passed=2 failed=0; real-save-quit-load=true; new-game-script-replayed=false; recovery="
         + ("native-zone-activation" if activation else "explicit-production-prepass")
         + "; ordinary-acceptance=false; profiles-and-effects-retained=true"),
    ]
    return "".join("2026-09-06T12:34:56.123Z\t" + verb + "\tOK\t" + message + "\n" for verb, message in messages)


def log(include_xml=True):
    return ("[TAF] loaded synthetic fixture\n" + "\n".join(WARNINGS if include_xml else WARNINGS[:1]) + "\n").encode("utf-8")


class ScenarioRungLoadVerdictTest(unittest.TestCase):
    def test_source_persona_is_save_only_and_retains_exact_diagnostics(self):
        manifest, name = verdict.shared.persona_matrix.load(str(verdict.PERSONA))
        self.assertEqual("founding-first-city", manifest["REQUEST"])
        self.assertEqual("8.22@40,12", manifest["START"])
        self.assertEqual("stagedigest;subsidence-rung-save-check;stagedigest", manifest["SCRIPT"])
        self.assertEqual("subsidence-rung-save-check", manifest["VERBS"])
        self.assertEqual("stagedigest:OK~founded=false,subsidence-rung-save-check:OK~cases=1 passed=1 failed=0,stagedigest:OK~founded=true,COMPLETE", manifest["EXPECT"])
        self.assertEqual(WARNINGS, verdict.shared.persona_matrix.parse_log_expect(manifest["LOG_EXPECT"], name))
        self.assertIs(verdict.shared.read_bounded, verdict.read_bounded)

    def test_both_exact_recovery_routes_and_line_endings_pass(self):
        for activation in (False, True):
            for newline in ("\n", "\r\n"):
                with self.subTest(activation=activation, newline=newline):
                    result = verdict.verify_journal(journal(activation).replace("\n", newline))
                    self.assertEqual(GAME_ID, result["game_id"])
                    self.assertEqual("native-zone-activation" if activation else "explicit-production-prepass", result["recovery"])

    def test_every_frozen_release_and_completion_fact_is_required(self):
        changes = [("ss5/sr2", "ss3/sr2"), ("ss5/sr2", "ss4/sr2"), ("ss5/sr2", "ss5/sr1"), ("35 loaded bodies", "49 loaded bodies"),
                   ("release=Intent", "release=Pending"), ("native-write-cut=2", "native-write-cut=1"),
                   ("native-write-cut=2", "native-write-cut=3"), ("wear-fields=10", "wear-fields=9"),
                   ("autoonce=true", "autoonce=false"), ("new-game=false", "new-game=true"),
                   ("mod-restore=false", "mod-restore=true"), ("release=Released", "release=Intent"),
                   ("release=Released", "release=Cleared"), ("population=35", "population=34"),
                   ("original-step-retired=true", "original-step-retired=false"), ("replay=false", "replay=true"),
                   ("status=clear", "status=pending"), ("cases=2", "cases=1"), ("passed=2", "passed=1"),
                   ("failed=0", "failed=1"), ("real-save-quit-load=true", "real-save-quit-load=false"),
                   ("new-game-script-replayed=false", "new-game-script-replayed=true"),
                   ("ordinary-acceptance=false", "ordinary-acceptance=true"),
                   ("profiles-and-effects-retained=true", "profiles-and-effects-retained=false"),
                   ("before-AfterGameLoaded-handlers-and-zone-activation=true", "before-AfterGameLoaded-handlers-and-zone-activation=false"),
                   ("activation-already-recovered=false", "activation-already-recovered=true"),
                   ("explicit-production-prepass", "native-zone-activation")]
        for before, after in changes:
            with self.subTest(before=before, after=after):
                self.assertIn(before, journal())
                with self.assertRaises(ValueError):
                    verdict.verify_journal(journal().replace(before, after))

    def test_distinct_current_49_body_protocol_is_not_a_fallback(self):
        old = journal().replace(
            "exact ss5/sr2 bytes and 35 loaded bodies; release=Intent; native-write-cut=2; wear-fields=10",
            "exact ss5 bytes and 49 loaded bodies; one credit of five; original anchor unpaid").replace(
            "release=Released; population=35", "remaining-four=proved; population=45")
        self.assertEqual(GAME_ID, verdict.shared.verify_journal(old)["game_id"])
        with self.assertRaisesRegex(ValueError, "preactivation proof differs"):
            verdict.verify_journal(old)

    def test_historical_ss4_journal_is_not_current_ss5_acceptance(self):
        historical = journal().replace("ss5/sr2", "ss4/sr2")
        with self.assertRaisesRegex(ValueError, "preactivation proof differs"):
            verdict.verify_journal(historical)
        self.assertIn("exact ss4/sr2", historical)

    def test_missing_duplicate_reordered_refused_and_extra_rows_fail(self):
        rows = journal().splitlines(keepends=True)
        variants = ["".join(rows[:i] + rows[i + 1:]) for i in range(4)]
        variants += ["".join(rows[:i] + [rows[i]] + rows[i:]) for i in range(4)]
        variants += ["".join(rows[:i] + [rows[i].replace("\tOK\t", "\tREFUSED\t")] + rows[i + 1:]) for i in range(4)]
        variants += ["".join([rows[1], rows[0], *rows[2:]]), "".join([*rows[:2], rows[3], rows[2]]),
                     journal() + "2026-09-06T12:34:56.123Z\tEXTRA\tOK\tforeign\n"]
        for value in variants:
            with self.subTest(value=value), self.assertRaises((ValueError, SystemExit)):
                verdict.verify_journal(value)

    def test_malformed_grammar_dates_and_noncanonical_ids_fail(self):
        variants = [journal().rstrip("\n"), journal() + "\n", "\n" + journal(), journal().replace("\n", "\r", 1),
                    journal().replace("\tOK\t", "\tUNKNOWN\t", 1), journal().replace("\tOK\t", "\tOK\textra\t", 1),
                    journal().replace("2026-09-06", "2026-02-31", 1), journal().replace("56.123Z", "56Z", 1)]
        variants += [journal().replace(GAME_ID, value) for value in
                     (GAME_ID.upper(), "{" + GAME_ID + "}", GAME_ID.replace("-", ""), GAME_ID + "x")]
        for value in variants:
            with self.subTest(value=value), self.assertRaises((ValueError, SystemExit)):
                verdict.verify_journal(value)

    def test_actual_shared_log_checker_accepts_only_exact_known_warnings(self):
        for include_xml in (False, True):
            for newline in (b"\n", b"\r\n"):
                with self.subTest(include_xml=include_xml, newline=newline):
                    verdict.verify_log(log(include_xml).replace(b"\n", newline))
        manual, xml = [line.encode("utf-8") + b"\n" for line in WARNINGS]
        for value in (log().replace(manual, b""), log() + manual, log() + xml, log().replace(b"char 6", b"char 7")):
            with self.subTest(value=value), self.assertRaises((ValueError, SystemExit)):
                verdict.verify_log(value)

    def test_unrelated_errors_and_engine_frames_refuse_despite_ambient_allowance(self):
        diagnostics = ["MODERROR [Foreign Mod] - failed", "modwarn [Foreign Mod] - notice",
                       "MODWARN [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]] - CS0114",
                       "AfterGameLoaded: System.FormatException: String was not recognized as a valid Boolean.",
                       "  at XRL.World.MinEvent.CascadeTo (System.Int32 Level)", "UnityEngine.Debug:LogError (object)",
                       "[TAF] inspection required", "[TAF] quarantine", "--- End of stack trace ---"]
        for diagnostic in diagnostics:
            with self.subTest(diagnostic=diagnostic), mock.patch.dict(os.environ, {"TAF_LOG_ALLOW": ".*"}):
                with self.assertRaises((ValueError, SystemExit)):
                    verdict.verify_log(log(False) + diagnostic.encode("utf-8") + b"\n")

    def test_actual_profile_files_are_unchanged_on_pass_and_refusal(self):
        with tempfile.TemporaryDirectory(prefix="taf-rung-load-verdict-test.") as temporary:
            root = Path(temporary)
            (root / "scenario-journal.tsv").write_bytes(journal(True).replace("\n", "\r\n").encode("utf-8"))
            raw = root / "Player.log"
            raw.write_bytes(log().replace(b"\n", b"\r\n"))
            before = {p.name: p.read_bytes() for p in root.iterdir()}
            self.assertEqual("native-zone-activation", verdict.verify_profile(root)["recovery"])
            self.assertEqual(before, {p.name: p.read_bytes() for p in root.iterdir()})
            raw.write_bytes(raw.read_bytes() + b"[TAF] unexpected exception\r\n")
            before = {p.name: p.read_bytes() for p in root.iterdir()}
            with self.assertRaises(ValueError):
                verdict.verify_profile(root)
            self.assertEqual(before, {p.name: p.read_bytes() for p in root.iterdir()})

    def test_shared_reader_refuses_empty_oversize_and_linked_files(self):
        with tempfile.TemporaryDirectory(prefix="taf-rung-load-reader-test.") as temporary:
            root = Path(temporary)
            path = root / "witness"
            for payload, bound in ((b"", 4), (b"12345", 4)):
                path.write_bytes(payload)
                with self.assertRaises(ValueError):
                    verdict.read_bounded(path, bound)
                self.assertEqual(payload, path.read_bytes())
            path.write_bytes(b"valid")
            link = root / "symlink"
            link.symlink_to(path)
            with self.assertRaises(ValueError):
                verdict.read_bounded(link, 10)
            os.link(path, root / "hardlink")
            with self.assertRaises(ValueError):
                verdict.read_bounded(path, 10)
            self.assertEqual(b"valid", path.read_bytes())

    def test_cli_restricts_roots_before_reading_and_reports_scope(self):
        for root in ("/tmp/taf-scenario.A", "/mnt/c/taf-scenario.A/", "/mnt/c/taf-scenario.A/../taf-scenario.B", "/mnt/c/ordinary"):
            with self.subTest(root=root), mock.patch.object(verdict, "verify_profile") as verify, contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, verdict.main(["verify", root]))
                verify.assert_not_called()
        output = io.StringIO()
        with mock.patch.object(verdict, "verify_profile", return_value={"game_id": GAME_ID, "recovery": "native-zone-activation"}) as verify, contextlib.redirect_stdout(output):
            self.assertEqual(0, verdict.main(["verify", "/mnt/c/taf-scenario.Fixture"]))
            verify.assert_called_once_with(Path("/mnt/c/taf-scenario.Fixture"))
        for marker in ("NATIVE RUNG LOAD VERDICT PASS: cases=2", "checkpoint=synthetic", "historical-save-compatibility=untested",
                       "ordinary-acceptance=false", "process-authority=unproved"):
            self.assertIn(marker, output.getvalue())

    def test_real_cli_invalid_invocation_never_prints_pass(self):
        result = subprocess.run([sys.executable, "-B", str(TOOLS / "verify-scenario-rung-load.py"), "/tmp/ordinary"],
                                capture_output=True, text=True, check=False)
        self.assertEqual(2, result.returncode)
        self.assertEqual("", result.stdout)
        self.assertIn("NATIVE RUNG LOAD VERDICT REFUSED:", result.stderr)


if __name__ == "__main__":
    unittest.main()
