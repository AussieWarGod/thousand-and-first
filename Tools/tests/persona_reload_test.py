"""Host orchestration tests use fake effects; they are not native reload acceptance."""
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from persona_reload import execute, execute_unfounded, NativeBackend, UnfoundedNativeBackend, valid_start
from personas.persona_matrix import parse_manifest, assess, reload_start
import scenario_profile

MANIFEST = "REQUEST=founding-first-city\nSCRIPT=reload-descendant quickstart marsh yes\nEXPECT=RELOAD-COMPLETE\n"


class Fake:
    def __init__(self, fault="", change=None):
        self.events, self.fault, self.change = [], fault, change

    def event(self, name):
        self.events.append(name)
        if name == self.fault:
            raise ValueError("injected " + name)

    def idle(self, phase): self.event("idle-" + phase)
    def fresh(self, phase):
        self.event("fresh-" + phase)
        return "source" if phase == "save" else "destination"
    def prepare(self, *args): self.event("prepare")
    def launch(self, root, phase): self.event("launch-" + phase)
    def wait(self, root, phase): self.event("wait-" + phase)
    def stop(self, root, phase): self.event("stop-" + phase)
    def transport(self, *args): self.event("transport")
    def check(self, root, phase):
        self.event("check-" + phase)
        result = dict(verdict="PASS", phase=phase, gameId="game-id", seed="#4242",
                      command="quickstart-save marsh yes",
                      saveHashes={"Primary.sav.gz": "save-hash", "Primary.json": "info-hash"})
        if phase == "load" and self.change:
            self.change(result)
        return result


class ReloadTests(unittest.TestCase):
    def test_real_effect_order_is_stop_before_transport_and_no_script_resume(self):
        fake = Fake()
        result = execute(fake, "marsh", "yes")
        self.assertEqual(fake.events, ["idle-initial", "fresh-save", "prepare", "launch-save",
            "wait-save", "check-save", "stop-save", "idle-between", "fresh-load", "transport",
            "launch-load", "wait-load", "check-load", "stop-load", "idle-final"])
        self.assertEqual(result["verdict"], "PASS")
        for flag in ("sameProfileDirectory", "scriptResumed", "gracefulQuit", "ordinaryAcceptance", "releaseAcceptance"):
            self.assertIs(result[flag], False)

    def test_every_failed_stage_refuses_and_active_process_is_cleaned(self):
        events = Fake()
        execute(events, "marsh", "yes")
        for fault in events.events:
            with self.subTest(fault=fault):
                fake = Fake(fault)
                with self.assertRaises(ValueError): execute(fake, "marsh", "yes")
                if fault.startswith(("launch-", "wait-", "check-", "stop-")):
                    self.assertEqual(fake.events[-1], "stop-failure")

    def test_failed_source_stop_never_allocates_or_launches_destination(self):
        fake = Fake("stop-save")
        with self.assertRaises(ValueError): execute(fake, "marsh", "yes")
        self.assertNotIn("fresh-load", fake.events)
        self.assertNotIn("transport", fake.events)
        self.assertNotIn("launch-load", fake.events)

    def test_changed_identity_selection_or_save_hash_cannot_pass(self):
        changes = [lambda r, key=key: r.update({key: "foreign"})
                   for key in ("gameId", "seed", "command", "phase", "verdict")]
        changes += [lambda r, key=key: r["saveHashes"].update({key: "changed"})
                    for key in ("Primary.sav.gz", "Primary.json")]
        for change in changes:
            fake = Fake(change=change)
            with self.assertRaises(ValueError): execute(fake, "marsh", "yes")
            self.assertEqual(fake.events[-1], "stop-failure")

    def test_same_profile_refused_before_transport(self):
        fake = Fake()
        fake.fresh = lambda phase: "same"
        with self.assertRaisesRegex(ValueError, "fresh profile"): execute(fake, "marsh", "yes")
        self.assertNotIn("transport", fake.events)

    def test_manifest_admits_only_bounded_standalone_recipe(self):
        manifest = parse_manifest(MANIFEST, "test")
        self.assertEqual(manifest["RELOAD"], "quickstart")
        self.assertTrue(assess(manifest, "", "test"))
        for location in ("marsh", "canyon", "dunes"):
            for advisor in ("yes", "no"):
                self.assertEqual(parse_manifest(MANIFEST.replace("marsh yes", location + " " + advisor), "test")["RELOAD"], "quickstart")
        for text in (MANIFEST.replace("marsh", "arbitrary"), MANIFEST.replace("yes", "maybe"),
                     MANIFEST.replace("marsh yes", "marsh yes;status"), MANIFEST.replace("RELOAD-COMPLETE", "COMPLETE"),
                     MANIFEST + "START=testground\n", MANIFEST + "VERBS=foreign\n",
                     MANIFEST + 'LOG_EXPECT=["ignored"]\n',
                     # The same promise, for the opposite field: a reload persona declares no
                     # overrides, and a forbidden-diagnostic list is an override too. Without
                     # this the branch returns before the field is normalised and its raw text
                     # would reach the tab-separated field output.
                     MANIFEST + 'LOG_FORBID=["ignored"]\n',
                     # And for its positive twin: a required witness is an override as well.
                     MANIFEST + 'LOG_REQUIRE=["ignored"]\n',
                     MANIFEST + "CHECK=status-digest-stable\n",
                     MANIFEST.replace("founding-first-city", "arch-gallery-slice")):
            with self.subTest(text=text), self.assertRaises(SystemExit): parse_manifest(text, "test")

    def test_bare_live_reload_is_sealable_but_native_dispatch_explicitly_refuses(self):
        manifest = parse_manifest("REQUEST=founding-first-city\nSCRIPT=reload\nEXPECT=reload:REFUSED,STOPPED\n", "test")
        self.assertNotIn("RELOAD", manifest)
        source = (TOOLS.parent / "Harness/KingdomScenarioReload.cs").read_text()
        self.assertIn("Ok = false;", source)
        self.assertIn("taf-reload-requires-cold-process", source)
        self.assertNotIn("SaveGame(", source)
        self.assertNotIn("LoadGame(", source)

    def test_production_backend_uses_strict_checkers_and_stopped_source_transport(self):
        backend = NativeBackend(TOOLS, Path("/licensed/CoQ.exe"), Path("/evidence"), 600, "#123")
        calls = []
        backend.command = lambda name, args, env=None: calls.append((name, args, env))
        backend.prepare(Path("/source"), "marsh", "yes")
        backend.transport(Path("/source"), Path("/destination"))
        self.assertEqual(calls[0][2]["TAF_SCENARIO_SCRIPT"], "quickstart-save marsh yes")
        self.assertEqual(calls[0][2]["TAF_SCENARIO_QUICKSTART_ADVISOR"], "yes")
        self.assertEqual(calls[0][2]["TAF_SCENARIO_EXTRA_VERBS"], "")
        self.assertEqual(calls[0][1][-1], "#123")
        self.assertEqual(calls[1][1], ["python3", str(TOOLS / "prepare-scenario-load.py"), "/source", "/destination"])
        class Json:
            def read_text(self, **kwargs): return '{"verdict":"PASS"}'
        with patch.object(backend, "command", return_value=Json()) as command:
            backend.check(Path("/destination"), "load")
            args = command.call_args.args
            self.assertEqual(args[1], ["python3", str(TOOLS / "check-quickstart-results.py"),
                                     "/destination", "--phase", "load"])
            self.assertEqual(args[2]["TAF_LOG_ALLOW"], "")

    def test_unknown_recipe_refuses_before_any_effect(self):
        fake = Fake()
        with self.assertRaises(ValueError): execute(fake, "foreign", "yes")
        self.assertEqual(fake.events, [])

    def test_cli_arms_a_kernel_parent_death_signal_before_running_and_never_forwards_a_raw_kill(self):
        """A killed run-personas.sh must not orphan this host; see run-persona-reload.py's
        arm_parent_death_signal() docstring. The runner side of that contract (no forwarded
        kill, RELOAD_PID tracked only to report an interrupted reload) is pinned in
        Tools/tests/scenario_process_source_test.py's blanket
        test_scenario_lifecycle_has_no_name_kill_or_recursive_profile_wipe, which already
        covers run-personas.sh; this test pins the CLI side of the same contract. Executable
        proof that the mechanism actually disposes of an orphan, refuses on a failed prctl, and
        that run-personas.sh's TERM path forwards and reports lives in
        Tools/tests/persona_reload_signal_test.py; this is the source-wiring pin only."""
        source = (TOOLS / "run-persona-reload.py").read_text(encoding="utf-8")
        self.assertIn("_PR_SET_PDEATHSIG = 1", source)
        self.assertIn("class ParentDeathSignalUnavailable(RuntimeError):", source)
        armed = source.index("def arm_parent_death_signal")
        environ_check = source.index('os.environ.get("TAF_RELOAD_PARENT_PID")', armed)
        prctl = source.index(".prctl(_PR_SET_PDEATHSIG, signal.SIGTERM, 0, 0, 0)", armed)
        recheck = source.index("if os.getppid() != expected_parent:", prctl)
        self.assertGreater(prctl, environ_check,
            "the pre-recorded parent must be checked before any arming is attempted")
        self.assertGreater(recheck, prctl,
            "getppid() must be re-checked after arming to catch a parent lost during the call")
        main_block = source[source.index("def main():"):source.index("def interrupted(")]
        handler = main_block.index("signal.signal(signal.SIGTERM, interrupted)")
        arm_call = main_block.index("arm_parent_death_signal()")
        manifest_load = main_block.index('load(str(args.persona))')
        self.assertGreater(arm_call, handler,
            "the explicit SIGTERM handler must be registered before the kernel is armed to send it")
        self.assertGreater(manifest_load, arm_call,
            "parent-death signal must be armed before any persona work runs")
        self.assertNotIn('signal.signal(signal.SIGTERM, interrupted)',
            source[source.index('if __name__ == "__main__":'):],
            "signal registration belongs inside main()'s own try, not a bare pre-main call "
            "main()'s except clause cannot see")


UNFOUNDED = "REQUEST=founding-first-city\nSCRIPT=reload-descendant unfounded 8.22@40,12\nEXPECT=RELOAD-COMPLETE\n"


class UnfoundedFake(Fake):
    """The unfounded route's two strict verdicts; the load leg saved again, so its own
    primary differs while the engine backup still equals the imported save."""

    def check(self, root, phase):
        self.event("check-" + phase)
        result = dict(verdict="PASS", phase=phase, gameId="game-id", seed="#4242",
                      command="unfounded-save 8.22@40,12")
        if phase == "save":
            result["saveHashes"] = {"Primary.sav.gz": "save-hash", "Primary.json": "info-hash",
                                    "Cache.db": "cache-hash"}
        else:
            result.update(importedSaveHashes={"Primary.sav.gz": "save-hash", "Primary.json": "info-hash"},
                          secondSaveHashes={"Primary.sav.gz": "founded-hash", "Primary.json": "founded-info"},
                          backupSaveHash="save-hash", foundedAfterLoad=True)
            if self.change:
                self.change(result)
        return result


class UnfoundedReloadTests(unittest.TestCase):
    def test_unfounded_route_keeps_the_owned_effect_order(self):
        fake = UnfoundedFake()
        result = execute_unfounded(fake, "8.22@40,12")
        self.assertEqual(fake.events, ["idle-initial", "fresh-save", "prepare", "launch-save",
            "wait-save", "check-save", "stop-save", "idle-between", "fresh-load", "transport",
            "launch-load", "wait-load", "check-load", "stop-load", "idle-final"])
        self.assertEqual(result["verdict"], "PASS")
        self.assertEqual(result["scope"], "developer-unfounded-cold-reload")
        self.assertIs(result["foundedAfterLoad"], True)
        self.assertIs(result["secondRealSave"], True)
        for flag in ("sameProfileDirectory", "scriptResumed", "gracefulQuit", "ordinaryAcceptance", "releaseAcceptance"):
            self.assertIs(result[flag], False)

    def test_unfounded_failed_stages_refuse_and_clean_the_active_process(self):
        events = UnfoundedFake()
        execute_unfounded(events, "8.22@40,12")
        for fault in events.events:
            with self.subTest(fault=fault):
                fake = UnfoundedFake(fault)
                with self.assertRaises(ValueError): execute_unfounded(fake, "8.22@40,12")
                if fault.startswith(("launch-", "wait-", "check-", "stop-")):
                    self.assertEqual(fake.events[-1], "stop-failure")

    def test_unfounded_identity_imported_bytes_backup_second_save_or_founding_cannot_drift(self):
        changes = [lambda r, key=key: r.update({key: "foreign"}) for key in ("gameId", "seed", "command")]
        changes += [lambda r, key=key: r["importedSaveHashes"].update({key: "changed"})
                    for key in ("Primary.sav.gz", "Primary.json")]
        changes += [lambda r: r.update(backupSaveHash="changed"),
                    lambda r: r["secondSaveHashes"].update({"Primary.sav.gz": "save-hash"}),
                    lambda r: r["secondSaveHashes"].pop("Primary.sav.gz"),
                    lambda r: r.update(foundedAfterLoad=False),
                    lambda r: r.pop("foundedAfterLoad"),
                    lambda r: r.pop("importedSaveHashes")]
        for change in changes:
            fake = UnfoundedFake(change=change)
            with self.assertRaises((ValueError, KeyError)): execute_unfounded(fake, "8.22@40,12")
            self.assertEqual(fake.events[-1], "stop-failure")

    def test_unfounded_evidence_for_another_start_cannot_pass(self):
        fake = UnfoundedFake()
        with self.assertRaisesRegex(ValueError, "another start"):
            execute_unfounded(fake, "8.23@40,12")
        self.assertEqual(fake.events[-1], "stop-failure")

    def test_unfounded_noncanonical_start_refuses_before_any_effect(self):
        for start in ("8.22", "08.22@40,12", "80.22@40,12", "8.22@40,25", "8.22@40,12 ", "", None):
            with self.subTest(start=start):
                fake = UnfoundedFake()
                with self.assertRaises(ValueError): execute_unfounded(fake, start)
                self.assertEqual(fake.events, [])

    def test_unfounded_manifest_normalizes_the_exact_save_leg(self):
        manifest = parse_manifest(UNFOUNDED, "test")
        self.assertEqual(manifest["RELOAD"], "unfounded")
        self.assertEqual(manifest["SCRIPT_WORDS"], "stagedigest unfounded-save stagedigest")
        self.assertEqual(manifest["VERBS"], "unfounded-save")
        self.assertEqual(manifest["START"], "8.22@40,12")
        self.assertTrue(assess(manifest, "", "test"), "a journal alone never grants a reload pass")
        self.assertEqual(scenario_profile.parse_script(manifest["SCRIPT_WORDS"].split(), ("unfounded-save",)),
                         ["stagedigest", "unfounded-save", "stagedigest"])
        for text in (UNFOUNDED.replace("8.22@40,12", "8.22"), UNFOUNDED.replace("8.22@40,12", "8.22@40,12 extra"),
                     UNFOUNDED.replace("8.22@40,12", "8.22@40,12;status"), UNFOUNDED.replace("unfounded ", "unfounded  quickstart "),
                     UNFOUNDED.replace("RELOAD-COMPLETE", "COMPLETE"), UNFOUNDED + "START=8.22@40,12\n",
                     UNFOUNDED + "VERBS=unfounded-save\n", UNFOUNDED + 'LOG_FORBID=["ignored"]\n',
                     UNFOUNDED + 'LOG_EXPECT=["ignored"]\n', UNFOUNDED + "CHECK=status-digest-stable\n",
                     UNFOUNDED.replace("founding-first-city", "arch-gallery-slice;facing=north")):
            with self.subTest(text=text), self.assertRaises(SystemExit): parse_manifest(text, "test")

    def test_unfounded_start_grammar_agrees_with_the_profile_tool(self):
        for start in ("0.0@0,0", "8.22@40,12", "79.24@79,24", "14.18@40,12"):
            with self.subTest(start=start):
                self.assertTrue(reload_start(start))
                self.assertTrue(valid_start(start))
                wx, rest = start.split(".")
                wy, cell = rest.split("@")
                x, y = cell.split(",")
                self.assertEqual(scenario_profile.parse_start(start),
                                 "GlobalLocation:JoppaWorld.%s.%s.1.1.10@%s,%s" % (wx, wy, x, y))
        for start in ("80.0@0,0", "0.25@0,0", "0.0@80,0", "0.0@0,25"):
            with self.subTest(start=start):
                self.assertFalse(reload_start(start))
                self.assertFalse(valid_start(start))
                with self.assertRaises(SystemExit): scenario_profile.parse_start(start)
        for start in ("8.22", "8.22@040,12", "8.22@40,12,1", "a.b@c,d", "8.22@40;12"):
            with self.subTest(start=start):
                self.assertFalse(reload_start(start))
                self.assertFalse(valid_start(start))

    def test_unfounded_backend_prepares_the_exact_leg_without_a_quickstart_advisor(self):
        backend = UnfoundedNativeBackend(TOOLS, Path("/licensed/CoQ.exe"), Path("/evidence"), 600, "#123")
        calls = []
        backend.command = lambda name, args, env=None: calls.append((name, args, env))
        with patch.dict("os.environ", {"TAF_SCENARIO_QUICKSTART_ADVISOR": "yes"}):
            backend.prepare(Path("/source"), "8.22@40,12")
        backend.transport(Path("/source"), Path("/destination"))
        name, args, env = calls[0]
        self.assertEqual(name, "prepare-save")
        self.assertEqual(args, ["bash", str(TOOLS / "prepare-scenario.sh"), "/source", "#123"])
        self.assertNotIn("TAF_SCENARIO_QUICKSTART_ADVISOR", env)
        self.assertEqual(env["TAF_REQUEST"], "founding-first-city")
        self.assertEqual(env["TAF_SCENARIO_SCRIPT"], "stagedigest unfounded-save stagedigest")
        self.assertEqual(env["TAF_SCENARIO_START"], "8.22@40,12")
        self.assertEqual(env["TAF_SCENARIO_EXTRA_VERBS"], "unfounded-save")
        self.assertEqual(env["TAF_QUD_ROOT"], "/licensed")
        self.assertEqual(calls[1][1], ["python3", str(TOOLS / "prepare-scenario-load.py"), "/source", "/destination"])
        with self.assertRaises(ValueError): backend.prepare(Path("/source"), "8.22")
        class Json:
            def read_text(self, **kwargs): return '{"verdict":"PASS"}'
        with patch.object(backend, "command", return_value=Json()) as command:
            backend.check(Path("/destination"), "load")
            args = command.call_args.args
            self.assertEqual(args[1], ["python3", str(TOOLS / "check-unfounded-results.py"),
                                     "/destination", "--phase", "load"])
            self.assertEqual(args[2]["TAF_LOG_ALLOW"], "")

    def test_unfounded_wait_wakes_on_the_terminal_or_a_refusal_never_an_ordinary_row(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            backend = UnfoundedNativeBackend(TOOLS, Path("/licensed/CoQ.exe"), root, 1, "")
            journal = root / "scenario-journal.tsv"
            for row in ("x\tSCRIPT-COMPLETE\tOK\tdone\n", "x\tunfounded-save\tREFUSED\tno\n",
                        "x\tSCRIPT-STOPPED\tREFUSED\tno\n"):
                journal.write_text(row, encoding="utf-8")
                backend.wait(root, "save")
            journal.write_text("x\tunfounded-save\tOK\treal-save=true dormant-frame=5\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "timed out"): backend.wait(root, "load")

    def test_cli_dispatches_the_unfounded_route_to_its_own_backend(self):
        source = (TOOLS / "run-persona-reload.py").read_text(encoding="utf-8")
        self.assertIn('require(route in ("quickstart", "unfounded", "renovate-heal"), "not a cold-reload persona")', source)
        self.assertIn("UnfoundedNativeBackend(tools, args.game.resolve(), evidence, int(timeout), seed)", source)
        self.assertIn('execute_unfounded(backend, manifest["START"])', source)


if __name__ == "__main__":
    unittest.main()
