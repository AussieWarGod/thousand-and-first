"""Mocked unattended-run contracts only; no game, Windows process or native evidence is used."""
from __future__ import annotations

from contextlib import ExitStack, contextmanager
import importlib.util
from pathlib import Path
import subprocess
import sys
from types import SimpleNamespace
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
try:
    spec = importlib.util.spec_from_file_location("taf_unattended_run", TOOLS / "run-upgrade-profile.py")
    runner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(runner)
finally:
    sys.path.pop(0)

PIN = "b" * 40
ROOT = Path("/mnt/c/taf-scenario.Run")
GAME = Path("/mnt/f/Game/CoQ.exe")
JOURNAL = b"2026-09-08T12:00:00.000Z\tSCRIPT-COMPLETE\tOK\tcomplete\n"


def args():
    return SimpleNamespace(root=ROOT, game=GAME, candidate=PIN, source=None,
                           source_probe_pin=None, repo=TOOLS.parent)


@contextmanager
def flow():
    with ExitStack() as stack:
        mocks = {}
        for name, result in (("preflight", {"mode": "source-donor"}), ("_argv", ["mock command"]),
                             ("_command", b"launched"), ("_receipt", b"exact receipt"),
                             ("_wait", None), ("_stop", b"exact receipt"),
                             ("_verdict", "native validator result")):
            mocks[name] = stack.enter_context(mock.patch.object(runner, name, return_value=result))
        mocks["intent"] = stack.enter_context(mock.patch.object(runner.fs, "write_new"))
        stack.enter_context(mock.patch.object(runner.time, "monotonic", return_value=1))
        yield mocks


class UnattendedRunControlTest(unittest.TestCase):
    def test_exact_quiet_launcher_and_receipt_controller_commands(self):
        with mock.patch.object(runner, "_windows", side_effect=lambda path: "WIN:" + path.name):
            launch, stop = runner._argv(args()), runner._argv(args(), True)
        self.assertEqual(launch, ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            "WIN:run-scenario.ps1", "-Root", "C:\\taf-scenario.Run", "-Game", "WIN:CoQ.exe"])
        self.assertEqual(stop[5:], ["WIN:scenario-process-control.ps1", "-Mode", "stop",
                                   "-Root", "C:\\taf-scenario.Run", "-Game", "WIN:CoQ.exe"])
        self.assertNotIn("-OwnAttended", launch)
        self.assertNotIn("-Id", stop)

    def test_no_launch_or_stop_when_fresh_admission_refuses(self):
        with flow() as calls:
            calls["preflight"].side_effect = ValueError("stale candidate")
            with self.assertRaisesRegex(ValueError, "stale candidate"):
                runner.run(args())
            calls["_command"].assert_not_called()
            calls["_stop"].assert_not_called()

    def test_exclusive_intent_failure_never_stops_another_run(self):
        with flow() as calls:
            calls["intent"].side_effect = FileExistsError("another runner owns this profile")
            with self.assertRaises(FileExistsError):
                runner.run(args())
            calls["_command"].assert_not_called()
            calls["_stop"].assert_not_called()

    def test_success_stops_before_native_validator_not_from_monitor_hint(self):
        events = []
        with flow() as calls:
            calls["_stop"].side_effect = lambda *unused: events.append("stop") or b"exact receipt"
            calls["_verdict"].side_effect = lambda *unused: events.append("validate") or "scoped native proof"
            self.assertEqual(runner.run(args()), "scoped native proof")
            self.assertEqual(events, ["stop", "validate"])
            self.assertEqual(calls["_wait"].call_args.args[-1], 601)
            self.assertEqual(calls["_command"].call_args.args[-1], 120)

    def test_launch_failure_and_timeout_still_use_only_owned_stop(self):
        for error in (OSError("launch refused"), subprocess.TimeoutExpired(["launcher"], 120)):
            with self.subTest(error=type(error).__name__), flow() as calls:
                calls["_command"].side_effect = error
                with self.assertRaises(type(error)):
                    runner.run(args())
                calls["_stop"].assert_called_once_with(mock.ANY, ["mock command"], None)
                calls["_verdict"].assert_not_called()

    def test_monitor_refusal_or_interrupt_still_stops_without_verdict(self):
        for error in (ValueError("native refused"), KeyboardInterrupt()):
            with self.subTest(error=type(error).__name__), flow() as calls:
                calls["_wait"].side_effect = error
                with self.assertRaises(type(error)):
                    runner.run(args())
                calls["_stop"].assert_called_once()
                calls["_verdict"].assert_not_called()

    def test_stop_failure_is_not_swallowed_by_prior_failure_or_success(self):
        for first in (None, ValueError("earlier refusal")):
            with self.subTest(first=first), flow() as calls:
                calls["_wait"].side_effect = first
                calls["_stop"].side_effect = ValueError("receipt missing or owner differs")
                with self.assertRaisesRegex(ValueError, "owned stop not proved"):
                    runner.run(args())
                calls["_verdict"].assert_not_called()

    def test_late_ownership_drift_refuses_after_native_validation(self):
        with flow() as calls:
            calls["_receipt"].side_effect = [b"exact receipt", b"changed receipt"]
            with self.assertRaisesRegex(ValueError, "ownership changed during final"):
                runner.run(args())

    def test_stop_rejects_absent_or_changed_receipt_before_controller(self):
        for value in (FileNotFoundError("absent"), b"different"):
            with self.subTest(value=value), mock.patch.object(runner, "_receipt") as receipt, \
                    mock.patch.object(runner, "_command") as command:
                if isinstance(value, Exception):
                    receipt.side_effect = value
                else:
                    receipt.return_value = value
                with self.assertRaises((ValueError, FileNotFoundError)):
                    runner._stop(args(), ["exact controller"], b"original")
                command.assert_not_called()

    def test_stop_requires_exact_controller_output_and_poststop_proof(self):
        for output in (b"ALREADY_EXITED root=C:\\taf-scenario.Run\r\n",
                       b"STOPPED pid=123 root=C:\\taf-scenario.Run; profile and seal retained\r\n"):
            with self.subTest(output=output), mock.patch.object(runner, "_receipt", return_value=b"owned"), \
                    mock.patch.object(runner, "_command", return_value=output), \
                    mock.patch.object(runner.state, "stopped_source") as stopped:
                self.assertEqual(runner._stop(args(), ["controller"], b"owned"), b"owned")
                stopped.assert_called_once_with(ROOT, GAME)
        with mock.patch.object(runner, "_receipt", return_value=b"owned"), \
                mock.patch.object(runner, "_command", return_value=b"STOPPED pid=123 root=C:\\taf-scenario.Foreign"), \
                mock.patch.object(runner.state, "stopped_source") as stopped:
            with self.assertRaises(ValueError):
                runner._stop(args(), ["controller"], b"owned")
            stopped.assert_not_called()


class UnattendedRunEvidenceTest(unittest.TestCase):
    def test_fixed_timeout_and_receipt_drift_stop_polling(self):
        with mock.patch.object(runner.time, "monotonic", side_effect=[0, 601]), \
                mock.patch.object(runner.time, "sleep") as sleep, \
                mock.patch.object(runner, "_receipt", return_value=b"owned"), \
                mock.patch.object(runner, "_ready", return_value=False):
            with self.assertRaisesRegex(ValueError, "600 seconds"):
                runner._wait(args(), "source", b"owned", 600)
            sleep.assert_called_once_with(1)
        with mock.patch.object(runner.time, "monotonic", return_value=1), \
                mock.patch.object(runner, "_receipt", return_value=b"foreign"), \
                mock.patch.object(runner, "_ready") as ready:
            with self.assertRaisesRegex(ValueError, "ownership receipt changed"):
                runner._wait(args(), "source", b"owned", 600)
            ready.assert_not_called()

    def test_terminal_journal_needs_all_native_artifacts(self):
        content = {"Player.log": b"INFO clean\n", "scenario-journal.tsv": JOURNAL,
                   "upgrade-save-receipt.txt": b"receipt", "upgrade-save-snapshot.txt": b"snapshot"}
        with mock.patch.object(runner.os.path, "lexists", return_value=False), \
                mock.patch.object(runner, "_peek", side_effect=lambda path, limit: content.get(path.name)):
            with self.assertRaisesRegex(ValueError, "lacks required artifacts"):
                runner._ready(ROOT, "source")
            content["upgrade-source-link.txt"] = b"link"
            self.assertTrue(runner._ready(ROOT, "source"))

    def test_native_refusals_errors_duplicates_and_trailing_rows_fail(self):
        for log, journal in ((b"MODERROR [The Thousand and First] defect\n", JOURNAL),
                             (b"", JOURNAL.replace(b"\tOK\t", b"\tREFUSED\t")),
                             (b"", JOURNAL + JOURNAL), (b"", JOURNAL + JOURNAL.replace(b"SCRIPT-COMPLETE", b"extra"))):
            content = {"Player.log": log, "scenario-journal.tsv": journal}
            with self.subTest(log=log, journal=journal), mock.patch.object(runner.os.path, "lexists", return_value=False), \
                    mock.patch.object(runner, "_peek", side_effect=lambda path, limit: content.get(path.name)):
                with self.assertRaises(ValueError):
                    runner._ready(ROOT, "upgrade")

    def test_third_party_diagnostic_never_refuses_only_a_taf_one_does(self):
        # The installed Pets of Harvest Dawn pack's own MODWARN (emitted at mod discovery, before
        # any ModSettings.json Enabled flag can gate it -- see upgrade_profile_inputs.py) must
        # never refuse the native run; only a MODERROR/MODWARN naming The Thousand and First does
        # (the same contract Tools/check-player-log.sh enforces for smoke/persona runs).
        pets = (b"INFO clean\nMODWARN [Pets of Harvest Dawn] - Mod defining manual load order, "
                b"please convert it to use the Dependencies field.\n")
        content = {"Player.log": pets, "scenario-journal.tsv": JOURNAL}
        with mock.patch.object(runner.os.path, "lexists", return_value=False), \
                mock.patch.object(runner, "_peek", side_effect=lambda path, limit: content.get(path.name)):
            self.assertTrue(runner._ready(ROOT, "upgrade"))
        for tagged in (b"MODWARN [The Thousand and First] - refused\n",
                       b"MODERROR [The Thousand and First] - refused\n"):
            content = {"Player.log": b"INFO clean\n" + tagged, "scenario-journal.tsv": JOURNAL}
            with self.subTest(tagged=tagged), mock.patch.object(runner.os.path, "lexists", return_value=False), \
                    mock.patch.object(runner, "_peek", side_effect=lambda path, limit: content.get(path.name)):
                with self.assertRaisesRegex(ValueError, "Thousand and First diagnostic"):
                    runner._ready(ROOT, "upgrade")

    def test_reader_waits_for_actual_report_bound_native_log(self):
        report = b"synthetic report"
        terminal = ("native-downgrade-reader cases=1 passed=1 failed=0; main-menu=true; game-created=false; "
                    + "save-loaded=false; report-sha256=" + runner.sha(report)).encode()
        content = {"downgrade-reader-report.txt": report, "Player.log": b""}
        with mock.patch.object(runner.os.path, "lexists", return_value=False), \
                mock.patch.object(runner, "_peek", side_effect=lambda path, limit: content.get(path.name)):
            self.assertFalse(runner._ready(ROOT, "downgrade"))
            content["Player.log"] = terminal + b"\n"
            self.assertTrue(runner._ready(ROOT, "downgrade"))
            content["downgrade-reader-report.txt"] = b"changed"
            self.assertFalse(runner._ready(ROOT, "downgrade"))

    def test_live_append_retry_does_not_waive_file_type_refusal(self):
        with mock.patch.object(runner.os.path, "lexists", return_value=True), \
                mock.patch.object(runner.fs, "read_bytes", side_effect=ValueError("source changed while reading: log")):
            self.assertIsNone(runner._peek(ROOT / "Player.log", 1024))
        with mock.patch.object(runner.os.path, "lexists", return_value=True), \
                mock.patch.object(runner.fs, "read_bytes", side_effect=ValueError("file is linked, reparse, or non-regular")):
            with self.assertRaises(ValueError):
                runner._peek(ROOT / "Player.log", 1024)

    def test_source_final_capture_reproves_all_state_and_witness_hashes(self):
        config = {"mode": "source-donor", "runtime": runner.OLD_PIN}
        facts = dict(files=[], directories=[], localHashes={}, donorAuthority=None)
        original = dict(gameId="game", journalSha256="a" * 64)
        changed = dict(original, journalSha256="b" * 64)
        with mock.patch.object(runner.state, "stopped_source"), \
                mock.patch.object(runner.state, "authenticate_source", return_value=(config, facts)), \
                mock.patch.object(runner.state, "capture_donor", side_effect=[original, changed]):
            with self.assertRaisesRegex(ValueError, "changed during final reproof"):
                runner._source_verdict(args(), config)

    def test_freshness_refuses_receipt_intent_or_existing_terminal(self):
        for leaf in (runner.INTENT, "process-ownership.json", "scenario-journal.tsv", "downgrade-reader-report.txt"):
            with self.subTest(leaf=leaf), mock.patch.object(runner.os.path, "lexists", side_effect=lambda path: path.name == leaf):
                with self.assertRaisesRegex(ValueError, "no retry"):
                    runner._fresh(ROOT)

    def test_wrong_candidate_or_v1_never_reaches_launch(self):
        for config in ({"schema": "taf-upgrade-profile-v1", "probe": PIN},
                       {"schema": runner.PROFILE_V2, "probe": "c" * 40}):
            with self.subTest(config=config), mock.patch.object(runner, "_fresh"), \
                    mock.patch.object(runner.fs, "directory"), mock.patch.object(runner.fs, "file_status"), \
                    mock.patch.object(runner.fs, "read_bytes", return_value=b"config"), \
                    mock.patch.object(runner, "parse_config", return_value=config), mock.patch.object(runner, "_birth") as birth:
                with self.assertRaisesRegex(ValueError, "fresh V2"):
                    runner.preflight(args())
                birth.assert_not_called()

    def test_birth_local_and_full_history_must_both_match_before_native_inspect(self):
        config = {"schema": runner.PROFILE_V2, "probe": PIN, "mode": "stage-source"}
        sealed = {"scenario-script.txt": b"stagedigest\n"}
        local = dict(path="Local/scenario-script.txt", sha256=runner.sha(sealed["scenario-script.txt"]), size=12)
        cases = ([local], [dict(local, sha256="c" * 64)],
                 [local, dict(path="Synced/Saves/foreign/Primary.sav.gz", size=1, sha256="d" * 64)])
        for index, files in enumerate(cases):
            with self.subTest(index=index), ExitStack() as stack:
                stack.enter_context(mock.patch.object(runner, "_fresh"))
                stack.enter_context(mock.patch.object(runner.fs, "directory"))
                stack.enter_context(mock.patch.object(runner.fs, "file_status"))
                stack.enter_context(mock.patch.object(Path, "iterdir", return_value=[]))
                stack.enter_context(mock.patch.object(runner.fs, "read_bytes", return_value=b"request\n"))
                stack.enter_context(mock.patch.object(runner, "parse_config", return_value=config))
                stack.enter_context(mock.patch.object(runner, "_birth", return_value=(sealed, [], ["Synced", "Synced/Saves"])))
                stack.enter_context(mock.patch.object(runner.state, "inventory", return_value=(files, ["Local", "Synced", "Synced/Saves"])))
                stack.enter_context(mock.patch.object(runner.scenario_profile, "read_seal",
                    return_value={"scenario-script.txt": local["sha256"]}))
                stack.enter_context(mock.patch.object(runner, "recipe_request", return_value="request"))
                native = stack.enter_context(mock.patch.object(runner.state, "native"))
                if index == 0:
                    self.assertEqual(runner.preflight(args()), config)
                    self.assertEqual(native.call_args.args[0], "Inspect")
                else:
                    with self.assertRaises(ValueError):
                        runner.preflight(args())
                    native.assert_not_called()

    def test_ancestor_requires_exact_stopped_owner_and_approved_probe(self):
        source = Path("/mnt/c/taf-scenario.Source")
        with mock.patch.object(runner.state, "stopped_source") as stopped, \
                mock.patch.object(runner.state, "authenticate_source", return_value=({"probe": "c" * 40}, {})) as auth:
            with self.assertRaisesRegex(ValueError, "observer pin"):
                runner._ancestor(args(), source, "source", runner.OLD_PIN, PIN)
            stopped.assert_called_once_with(source, GAME)
            auth.assert_called_once_with(TOOLS.parent, source, "source", runner.OLD_PIN, game=GAME)

    def test_terminal_without_explicit_source_refuses_before_recipe(self):
        for mode in ("upgrade", "downgrade"):
            with self.subTest(mode=mode), mock.patch.object(runner, "local_inputs") as local:
                with self.assertRaisesRegex(ValueError, "require --source"):
                    runner._birth(args(), {"mode": mode})
                local.assert_not_called()


if __name__ == "__main__":
    unittest.main()
