"""Executable journal/log contracts, not native execution or historical-save proof."""
from __future__ import annotations

import contextlib
import importlib.util
import io
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("scenario_load_verdict", TOOLS / "verify-scenario-load.py")
verdict = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(verdict)
finally:
    sys.path.pop(0)

GAME_ID = "01234567-89ab-cdef-0123-456789abcdef"


def journal(activation=False):
    # Independent literal fixture of the production messages, not copied from verdict constants.
    messages = [
        ("LOAD-BEGIN", "exact sealed save; game-id=" + GAME_ID + "; new-game=false; mod-restore=false"),
        ("LOAD-PREACTIVATION", "exact ss5 bytes and 49 loaded bodies; one credit of five; original anchor unpaid; autoonce=true; before-AfterGameLoaded-handlers-and-zone-activation=true"),
        ("LOAD-RECOVERY", "remaining-four=proved; population=45; original-step-retired=true; replay=false; status=clear; activation-already-recovered=" + str(activation).lower()),
        ("SCRIPT-COMPLETE", "native-load cases=2 passed=2 failed=0; real-save-quit-load=true; new-game-script-replayed=false; recovery="
         + ("native-zone-activation" if activation else "explicit-production-prepass")
         + "; ordinary-acceptance=false; profiles-and-effects-retained=true"),
    ]
    return "".join("2026-09-06T12:34:56.123Z\t" + verb + "\tOK\t" + message + "\n" for verb, message in messages)


def persona_warnings():
    manifest, name = verdict.persona_matrix.load(str(verdict.PERSONA))
    return verdict.persona_matrix.parse_log_expect(manifest["LOG_EXPECT"], name)


def log(include_xml=True):
    expected = persona_warnings()
    if not include_xml:
        expected = expected[:1]
    return ("[TAF] loaded synthetic fixture\n" + "\n".join(expected) + "\n").encode("utf-8")


class ScenarioLoadVerdictTest(unittest.TestCase):
    def test_historical_ss4_wording_cannot_claim_current_ss5_acceptance(self):
        historical = journal().replace("exact ss5 bytes", "exact ss4 bytes")
        with self.assertRaisesRegex(ValueError, "pre-activation saved-state"):
            verdict.verify_journal(historical)

    def test_both_exact_recovery_routes_and_crlf_pass(self):
        for activation in (False, True):
            for newline in ("\n", "\r\n"):
                with self.subTest(activation=activation, newline=newline):
                    result = verdict.verify_journal(journal(activation).replace("\n", newline))
                    self.assertEqual(GAME_ID, result["game_id"])
                    self.assertEqual("native-zone-activation" if activation else "explicit-production-prepass", result["recovery"])

    def test_missing_duplicate_reordered_extra_and_refused_rows_fail(self):
        rows = journal().splitlines(keepends=True)
        variants = ["".join(rows[:i] + rows[i + 1:]) for i in range(4)]
        variants += ["".join(rows[:i] + [rows[i]] + rows[i:]) for i in range(4)]
        variants += ["".join([rows[1], rows[0], *rows[2:]]), "".join([*rows[:2], rows[3], rows[2]]),
                     journal() + "2026-09-06T12:34:56.123Z\tEXTRA\tOK\tforeign\n"]
        variants += ["".join(rows[:i] + [rows[i].replace("\tOK\t", "\tREFUSED\t")] + rows[i + 1:]) for i in range(4)]
        for value in variants:
            with self.subTest(value=value):
                with self.assertRaises((ValueError, SystemExit)):
                    verdict.verify_journal(value)

    def test_every_acceptance_fact_and_route_consistency_are_required(self):
        mutations = [("new-game=false", "new-game=true"), ("mod-restore=false", "mod-restore=true"),
            ("49 loaded bodies", "48 loaded bodies"), ("one credit of five", "two credits of five"),
            ("original anchor unpaid", "original anchor paid"), ("autoonce=true", "autoonce=false"),
            ("before-AfterGameLoaded-handlers-and-zone-activation=true", "before-AfterGameLoaded-handlers-and-zone-activation=false"),
            ("remaining-four=proved", "remaining-four=unproved"), ("population=45", "population=44"),
            ("original-step-retired=true", "original-step-retired=false"), ("replay=false", "replay=true"),
            ("status=clear", "status=pending"), ("cases=2", "cases=1"), ("passed=2", "passed=1"),
            ("failed=0", "failed=1"), ("real-save-quit-load=true", "real-save-quit-load=false"),
            ("new-game-script-replayed=false", "new-game-script-replayed=true"),
            ("ordinary-acceptance=false", "ordinary-acceptance=true"),
            ("profiles-and-effects-retained=true", "profiles-and-effects-retained=false"),
            ("activation-already-recovered=false", "activation-already-recovered=true"),
            ("explicit-production-prepass", "native-zone-activation")]
        for before, after in mutations:
            with self.subTest(before=before):
                self.assertIn(before, journal())
                with self.assertRaises(ValueError):
                    verdict.verify_journal(journal().replace(before, after))

    def test_malformed_grammar_timestamps_and_noncanonical_game_ids_fail(self):
        variants = [journal().rstrip("\n"), journal() + "\n", "\n" + journal(),
                    journal().replace("\tOK\t", "\tUNKNOWN\t", 1),
                    journal().replace("\tOK\t", "\tOK\textra\t", 1),
                    journal().replace("2026-09-06T12:34:56.123Z", "not-a-time", 1),
                    journal().replace("2026-09-06", "2026-02-31", 1)]
        variants += [journal().replace(GAME_ID, value) for value in
                     (GAME_ID.upper(), "{" + GAME_ID + "}", GAME_ID.replace("-", ""), GAME_ID + "x")]
        for value in variants:
            with self.subTest(value=value):
                with self.assertRaises((ValueError, SystemExit)):
                    verdict.verify_journal(value)

    def test_real_log_checker_accepts_exact_load_warnings_with_or_without_xml(self):
        for include_xml in (False, True):
            for newline in (b"\n", b"\r\n"):
                with self.subTest(include_xml=include_xml, newline=newline):
                    verdict.verify_log(log(include_xml).replace(b"\n", newline))

    def test_manual_order_warning_is_required_exactly_once_with_or_without_xml(self):
        manual = persona_warnings()[0].encode("utf-8") + b"\n"
        for include_xml in (False, True):
            for value in (log(include_xml).replace(manual, b""), log(include_xml) + manual):
                with self.subTest(include_xml=include_xml, value=value):
                    with self.assertRaises((ValueError, SystemExit)):
                        verdict.verify_log(value)

    def test_optional_xml_warning_is_not_allowed_twice_or_as_an_altered_literal(self):
        optional_xml = persona_warnings()[1].encode("utf-8") + b"\n"
        for value in (log() + optional_xml, log().replace(b"char 6", b"char 7"),
                      log(False) + optional_xml.replace(b"MODWARN", b"MODWARN altered"),
                      log().replace(b"MODWARN", b"MODWARN altered", 1)):
            with self.subTest(value=value):
                with self.assertRaises((ValueError, SystemExit)):
                    verdict.verify_log(value)

    def test_real_checker_rejects_errors_and_frames_despite_ambient_allowance(self):
        for include_xml in (False, True):
            for diagnostic in (b"MODERROR [Foreign Mod] - failed\n", b"MODWARN [Foreign Mod] - warning\n",
                               b"modwarn [Foreign Mod] - notice\n", b"moderror [Foreign Mod] - failed\n",
                               b"MODERROR [The Thousand and First [DEV SCENARIO HARNESS]] - failure\n",
                               b"[TAF] unexpected exception\n", b"  at ThousandAndFirst.KingdomScenarioNewGameGate.mutate ()\n",
                               b"[TAF] quarantine\n", b"[TAF] inspection required\n"):
                with self.subTest(include_xml=include_xml, diagnostic=diagnostic), mock.patch.dict(os.environ, {"TAF_LOG_ALLOW": ".*"}):
                    with self.assertRaises((ValueError, SystemExit)):
                        verdict.verify_log(log(include_xml) + diagnostic)

    def test_actual_files_remain_identical_on_pass_and_log_refusal(self):
        with tempfile.TemporaryDirectory(prefix="taf-load-verdict-test.") as temporary:
            root = Path(temporary) / "taf-scenario.Fixture"
            root.mkdir()
            path = root / "scenario-journal.tsv"
            raw = root / "Player.log"
            path.write_bytes(journal(True).replace("\n", "\r\n").encode("utf-8"))
            raw.write_bytes(log().replace(b"\n", b"\r\n"))
            before = {p.name: p.read_bytes() for p in root.iterdir()}
            self.assertEqual("native-zone-activation", verdict.verify_profile(root)["recovery"])
            self.assertEqual(before, {p.name: p.read_bytes() for p in root.iterdir()})
            raw.write_bytes(raw.read_bytes() + b"[TAF] unexpected exception\r\n")
            before = {p.name: p.read_bytes() for p in root.iterdir()}
            with self.assertRaisesRegex(ValueError, "unexpected native-load diagnostic"):
                verdict.verify_profile(root)
            self.assertEqual(before, {p.name: p.read_bytes() for p in root.iterdir()})

    def test_exact_old_engine_only_exception_header_and_each_frame_refuse(self):
        # Frozen from /tmp/taf-save-load-native.fzkItN/player-load.log:184-190.
        # None requires a TAF namespace, and every isolated line must fail without its header.
        old = [
            "AfterGameLoaded: System.NullReferenceException: Object reference not set to an instance of an object",
            "  at System.Globalization.NumberFormatInfo.GetInstance (System.IFormatProvider formatProvider) [0x00000] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.SByte.Parse (System.String s, System.Globalization.NumberStyles style, System.IFormatProvider provider) [0x00016] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.Convert.ToSByte (System.String value, System.IFormatProvider provider) [0x00000] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.String.System.IConvertible.ToSByte (System.IFormatProvider provider) [0x00000] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at (wrapper dynamic-method) XRL.World.AfterGameLoadedEvent.XRL.World.AfterGameLoadedEvent.Send_Patch1(XRL.XRLGame)",
            "  at XRL.XRLGame.LoadGame (System.String Path, System.Boolean Session, System.Boolean ShowPopup, System.Collections.Generic.Dictionary`2[TKey,TValue] GameState) [0x00e10] in <c12b7579c9a74df3859b832ba394160b>:0 ",
        ]
        for line in old + ["\n".join(old), "WARN - XRL unexpected load warning", "ERROR - System failed",
                           "[Warning] engine recovery", "Exception: engine failure",
                           "UnityEngine.Debug:LogError (object)", "--- End of stack trace ---"]:
            with self.subTest(line=line), mock.patch.dict(os.environ, {"TAF_LOG_ALLOW": ".*"}):
                with self.assertRaisesRegex(ValueError, "unexpected native-load diagnostic"):
                    verdict.verify_log(log() + line.encode("utf-8") + b"\n")

    def test_exact_engine_only_boolean_stack_refuses_without_optional_xml_warning(self):
        # Frozen from /tmp/taf-save-load-fixed-native.lFsf13/player-load.log:182-191.
        # Each isolated frame must refuse, even when the load never emitted the optional XML warning.
        frames = [
            "AfterGameLoaded: System.FormatException: String was not recognized as a valid Boolean.",
            "  at System.Boolean.Parse (System.ReadOnlySpan`1[T] value) [0x0000a] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.Boolean.Parse (System.String value) [0x00014] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.Convert.ToBoolean (System.String value, System.IFormatProvider provider) [0x00005] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at System.String.System.IConvertible.ToBoolean (System.IFormatProvider provider) [0x00000] in <4449e9424e7e4d4d81a660907c68cb97>:0 ",
            "  at XRL.World.MinEvent.CascadeTo (System.Int32 Level) [0x00000] in <c12b7579c9a74df3859b832ba394160b>:0 ",
            "  at XRL.World.Zone.HandleEvent (XRL.World.MinEvent E) [0x00094] in <c12b7579c9a74df3859b832ba394160b>:0 ",
            "  at XRL.World.ZoneManager.HandleEvent[T] (T E) [0x00008] in <c12b7579c9a74df3859b832ba394160b>:0 ",
            "  at (wrapper dynamic-method) XRL.World.AfterGameLoadedEvent.XRL.World.AfterGameLoadedEvent.Send_Patch1(XRL.XRLGame)",
            "  at XRL.XRLGame.LoadGame (System.String Path, System.Boolean Session, System.Boolean ShowPopup, System.Collections.Generic.Dictionary`2[TKey,TValue] GameState) [0x00e10] in <c12b7579c9a74df3859b832ba394160b>:0 ",
        ]
        for line in frames + ["\n".join(frames)]:
            with self.subTest(line=line), mock.patch.dict(os.environ, {"TAF_LOG_ALLOW": ".*"}):
                with self.assertRaisesRegex(ValueError, "unexpected native-load diagnostic"):
                    verdict.verify_log(log(False) + line.encode("utf-8") + b"\n")

    def test_cli_restricts_root_and_reports_evidence_limits(self):
        for value in ("/tmp/taf-scenario.A", "/mnt/c/taf-scenario.A/", "/mnt/c/taf-scenario.A/../taf-scenario.B", "/mnt/c/ordinary"):
            with self.subTest(value=value), mock.patch.object(verdict, "verify_profile") as verify, contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, verdict.main(["verify", value]))
                verify.assert_not_called()
        output = io.StringIO()
        with mock.patch.object(verdict, "verify_profile", return_value={"game_id": GAME_ID, "recovery": "native-zone-activation"}) as verify, contextlib.redirect_stdout(output):
            self.assertEqual(0, verdict.main(["verify", "/mnt/c/taf-scenario.A"]))
            verify.assert_called_once_with(Path("/mnt/c/taf-scenario.A"))
        for marker in ("historical-save-compatibility=untested", "ordinary-acceptance=false", "process-authority=unproved"):
            self.assertIn(marker, output.getvalue())


if __name__ == "__main__":
    unittest.main()
