"""Host orchestration tests use fake effects; they are not native reload acceptance."""
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
from persona_reload import execute, NativeBackend
from personas.persona_matrix import parse_manifest, assess

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


if __name__ == "__main__":
    unittest.main()
