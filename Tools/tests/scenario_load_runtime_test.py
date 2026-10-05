"""#283 cross-build load profile: synthetic bytes only, no native save/load evidence.

prepare-scenario-load.py --runtime keeps every stopped-source rule and replaces only the copied
Local with a second sealed, never-launched profile from another tree. Process authority is
injected; nothing here launches or stops a real process.
"""
from __future__ import annotations

import contextlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("scenario_load_runtime", TOOLS / "prepare-scenario-load.py")
load = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(load)
finally:
    sys.path.pop(0)
sys.path.insert(0, str(Path(__file__).resolve().parent))
import scenario_load_profile_test as single  # noqa: E402

snapshot = single.snapshot


class ScenarioLoadRuntimeTest(unittest.TestCase):
    # The same source fixture the single-build tests use, borrowed without inheriting their tests.
    setUp = single.ScenarioLoadProfileTest.setUp
    fixture = single.ScenarioLoadProfileTest.fixture
    stopped = single.ScenarioLoadProfileTest.stopped
    before = single.ScenarioLoadProfileTest.before

    def donor(self, script=None, request=None, harness=None, core=b"class A { /* fixed */ }\r\n",
              options=None):
        self.runtime = self.base / ("taf-scenario.Runtime" + str(self.number))
        local = self.runtime / "Local"
        mod = local / "Mods/ThousandAndFirst"
        (mod / "Harness").mkdir(parents=True)
        (mod / "Core").mkdir()
        (local / "Empty").mkdir()
        (mod / "Harness/EmbarkModules.xml").write_bytes(
            harness or (self.mod / "Harness/EmbarkModules.xml").read_bytes())
        (mod / "Core/A.cs").write_bytes(core)
        (mod / "Core/B.cs").write_bytes(b"class B {}\r\n")
        (local / "scenario-script.txt").write_bytes(script or (self.local / "scenario-script.txt").read_bytes())
        if options is not None:
            (local / "PlayerOptions.json").write_bytes(options)
        seal = Path(str(self.runtime) + ".seal")
        seal.mkdir()
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(local), str(seal / "profile.sha256"))
        (seal / "request.txt").write_bytes(request or self.request)
        (self.runtime / "Save").mkdir()

    def test_runtime_local_replaces_the_source_local_and_only_the_runtime_differs(self):
        self.fixture()
        self.donor()
        before, runtime_before = self.before(), snapshot(self.runtime)
        evidence = load.prepare(self.source, self.destination, self.stopped, runtime=self.runtime)
        self.assertEqual([self.source], self.calls)
        self.assertEqual(before, self.before())
        self.assertEqual(runtime_before, snapshot(self.runtime))
        target = self.destination / "Local"
        inventory = load.scenario_profile.inventory(str(target))
        donor = load.scenario_profile.inventory(str(self.runtime / "Local"))
        self.assertEqual(set(donor) | {"scenario-load.txt", "scenario-load-snapshot.txt"}, set(inventory))
        self.assertEqual(donor, {key: value for key, value in inventory.items() if key in donor})
        self.assertEqual(b"class A { /* fixed */ }\r\n", (target / "Mods/ThousandAndFirst/Core/A.cs").read_bytes())
        self.assertEqual(inventory, load.scenario_profile.read_seal(str(self.destination_seal / "profile.sha256")))
        self.assertEqual(self.request, (self.destination_seal / "request.txt").read_bytes())
        for name in load.SAVE_FILES:
            self.assertEqual((self.save / name).read_bytes(),
                             (self.destination / "Synced/Saves" / self.game_id / name).read_bytes())
        written = json.loads((self.destination / "load-runtime-evidence.json").read_text(encoding="utf-8"))
        self.assertEqual(written, evidence["runtime"])
        self.assertEqual(["mods/thousandandfirst/core/a.cs", "mods/thousandandfirst/core/b.cs"],
                         written["runtimeDelta"])
        self.assertIs(True, written["crossBuild"])
        self.assertIs(True, written["harnessIdentical"])

    def refuses(self, **donor):
        self.fixture()
        self.donor(**donor)
        before, runtime_before = self.before(), snapshot(self.runtime)
        with self.assertRaises((ValueError, OSError, SystemExit)):
            load.prepare(self.source, self.destination, self.stopped, runtime=self.runtime)
        self.assertEqual(before, self.before())
        self.assertEqual(runtime_before, snapshot(self.runtime))
        self.assertFalse((self.destination / "Local").exists())
        self.assertFalse((self.destination_seal / "profile.sha256").exists())

    def test_runtime_must_share_request_seed_script_and_harness(self):
        cases = {"seed": dict(request=b"founding-first-city;seed=#4243\n"),
                 "script": dict(script=b"stagedigest\nother\n"),
                 "harness": dict(harness=b'<state Name="r_TAF_ScenarioRequest_v1" Value="founding-first-city;'
                                         b'seed=#4242"/>\n<extra/>\n'),
                 "identical": dict(core=b"class A {}\r\n"),
                 "outside runtime": dict(options=b"{}")}
        for name, donor in cases.items():
            with self.subTest(name=name):
                if name == "identical":
                    self.fixture()
                    self.donor(**donor)
                    (self.runtime / "Local/Mods/ThousandAndFirst/Core/B.cs").unlink()
                    seal = Path(str(self.runtime) + ".seal") / "profile.sha256"
                    seal.unlink()
                    with contextlib.redirect_stdout(io.StringIO()):
                        load.scenario_profile.seal(str(self.runtime / "Local"), str(seal))
                    with self.assertRaisesRegex(ValueError, "not at all"):
                        load.prepare(self.source, self.destination, self.stopped, runtime=self.runtime)
                else:
                    self.refuses(**donor)

    def test_a_launched_saved_or_resealed_runtime_refuses(self):
        for name in ("Player.log", "process-ownership.json", "scenario-journal.tsv", "save", "unsealed"):
            with self.subTest(name=name):
                self.fixture()
                self.donor()
                if name == "save":
                    (self.runtime / "Synced/Saves/x").mkdir(parents=True)
                elif name == "unsealed":
                    (self.runtime / "Local/extra.txt").write_bytes(b"late")
                else:
                    (self.runtime / name).write_bytes(b"x")
                with self.assertRaises((ValueError, OSError, SystemExit)):
                    load.prepare(self.source, self.destination, self.stopped, runtime=self.runtime)
                self.assertFalse((self.destination / "Local").exists())

    def test_runtime_root_must_be_a_distinct_canonical_sibling(self):
        self.fixture()
        self.donor()
        for candidate in (self.source, self.destination, self.base / "nested" / self.runtime.name,
                          self.base / "taf-scenario.bad name"):
            with self.subTest(candidate=candidate), self.assertRaises((ValueError, OSError)):
                load.prepare(self.source, self.destination, self.stopped, runtime=candidate)

    def test_cli_passes_the_runtime_only_through_its_option(self):
        with mock.patch.object(load, "prepare") as prepare, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, load.main(["x", "--runtime", "/mnt/c/taf-scenario.R",
                                           "/mnt/c/taf-scenario.S", "/mnt/c/taf-scenario.D"]))
        prepare.assert_called_once_with(Path("/mnt/c/taf-scenario.S"), Path("/mnt/c/taf-scenario.D"),
                                        load.assert_source_stopped, runtime=Path("/mnt/c/taf-scenario.R"))
        with mock.patch.object(load, "prepare") as prepare, contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(2, load.main(["x", "--runtime", "/tmp/taf-scenario.R",
                                           "/mnt/c/taf-scenario.S", "/mnt/c/taf-scenario.D"]))
            self.assertEqual(2, load.main(["x", "--runtime", "/mnt/c/taf-scenario.R", "/mnt/c/taf-scenario.S"]))
        prepare.assert_not_called()


if __name__ == "__main__":
    unittest.main()
