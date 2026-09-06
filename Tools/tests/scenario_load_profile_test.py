"""Executable filesystem contracts; synthetic bytes, no native save/load evidence.

The real copier/seal parser run inside TemporaryDirectory. Only Windows process
authority is injected; these tests never launch, inspect, or stop real processes.
"""
from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("scenario_load_profile", TOOLS / "prepare-scenario-load.py")
load = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(load)
finally:
    sys.path.pop(0)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def snapshot(root):
    result = {}
    for current, directories, files in os.walk(root, followlinks=False):
        for name in directories + files:
            path = Path(current) / name
            key = str(path.relative_to(root))
            result[key] = ("link", os.readlink(path)) if path.is_symlink() else (
                ("dir",) if path.is_dir() else ("file", path.read_bytes()))
    return result


class ScenarioLoadProfileTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="taf-load-profile-test.")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.number = 0

    def fixture(self):
        self.number += 1
        self.source = self.base / ("taf-scenario.Source" + str(self.number))
        self.destination = self.base / ("taf-scenario.Dest" + str(self.number))
        self.seal = Path(str(self.source) + ".seal")
        self.destination_seal = Path(str(self.destination) + ".seal")
        self.local = self.source / "Local"
        self.mod = self.local / "Mods/ThousandAndFirst"
        (self.mod / "Harness").mkdir(parents=True)
        (self.mod / "Core").mkdir()
        (self.local / "Empty").mkdir()
        self.request = b"founding-first-city;seed=#4242\n"
        (self.mod / "Harness/EmbarkModules.xml").write_bytes(
            b'<state Name="r_TAF_ScenarioRequest_v1" Value="' + self.request[:-1] + b'"/>\n')
        (self.mod / "Core/A.cs").write_bytes(b"class A {}\r\n")
        (self.local / "scenario-script.txt").write_bytes(b"stagedigest\nsubsidence-save-check\nstagedigest\n")
        self.seal.mkdir()
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        (self.seal / "request.txt").write_bytes(self.request)
        self.game_id = "01234567-89ab-cdef-0123-456789abcdef"
        self.save = self.source / "Synced/Saves" / self.game_id
        self.save.mkdir(parents=True)
        for name, content in zip(load.SAVE_FILES, (b"synthetic-primary", b'{"fixture":true}', b"synthetic-cache")):
            (self.save / name).write_bytes(content)
        self.saved_snapshot = b"synthetic exact resident/work snapshot\n"
        (self.source / "scenario-save-snapshot.txt").write_bytes(self.saved_snapshot)
        receipt = "\n".join(("taf-scenario-save-v1", self.game_id,
            sha((self.save / "Primary.sav.gz").read_bytes()), sha((self.save / "Primary.json").read_bytes()),
            sha(self.saved_snapshot))) + "\n"
        (self.source / "scenario-save-receipt.txt").write_text(receipt, encoding="ascii")
        root = "C:\\" + self.source.name
        exe = r"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ.exe"
        arguments = [exe, "-savepath", root + r"\Save", "-sharedpath", root + r"\Local",
                     "-syncedpath", root + r"\Synced", "-logFile", root + r"\Player.log",
                     "NOMETRICS", "STEAM:NO", "GALAXY:NO"]
        ownership = dict(schema="taf-scenario-process-v1", root=root, pid=1234,
                         startTicks="638900000000000000", executable=exe, arguments=arguments)
        (self.source / "process-ownership.json").write_text(json.dumps(ownership, separators=(",", ":")), encoding="utf-8")
        (self.source / "Player.log").write_bytes(b"retained original log\r\n")
        (self.source / "scenario-journal.tsv").write_bytes(b"retained\tfirst\tjournal\n")
        self.calls = []

    def stopped(self, source):
        self.assertEqual(self.source, source)
        for path in (self.destination, self.destination_seal):
            self.assertTrue(not path.exists() or not list(path.iterdir()))
        self.calls.append(source)

    def before(self):
        return snapshot(self.source), snapshot(self.seal)

    def assert_refuses_unchanged(self):
        before = self.before()
        with self.assertRaises((ValueError, OSError, SystemExit)):
            load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual(before, self.before())
        self.assertFalse(self.destination.exists())
        self.assertFalse(self.destination_seal.exists())

    def test_copies_only_named_save_and_original_local_then_seals_exact_additions(self):
        for precreated in (False, True):
            with self.subTest(precreated=precreated):
                self.fixture()
                (self.save / "Primary.sav.gz.bak").write_bytes(b"retained backup, never copied")
                before = self.before()
                original = load.scenario_profile.inventory(str(self.local))
                if precreated:
                    self.destination.mkdir()
                    self.destination_seal.mkdir()
                evidence = load.prepare(self.source, self.destination, self.stopped)
                self.assertEqual([self.source], self.calls)
                self.assertEqual(before, self.before())
                copied = self.destination / "Synced/Saves" / self.game_id
                self.assertEqual(set(load.SAVE_FILES), {path.name for path in copied.iterdir()})
                for name in load.SAVE_FILES:
                    self.assertEqual((self.save / name).read_bytes(), (copied / name).read_bytes())
                target = self.destination / "Local"
                inventory = load.scenario_profile.inventory(str(target))
                self.assertEqual(original, {key: value for key, value in inventory.items() if key in original})
                self.assertEqual(set(original) | {"scenario-load.txt", "scenario-load-snapshot.txt"}, set(inventory))
                self.assertTrue((target / "Empty").is_dir())
                expected_request = "\n".join(("taf-scenario-load-v1", self.game_id,
                    *(sha((self.save / name).read_bytes()) for name in load.SAVE_FILES), sha(self.saved_snapshot))) + "\n"
                self.assertEqual(expected_request.encode("ascii"), (target / "scenario-load.txt").read_bytes())
                self.assertEqual(self.saved_snapshot, (target / "scenario-load-snapshot.txt").read_bytes())
                self.assertEqual(inventory, load.scenario_profile.read_seal(str(self.destination_seal / "profile.sha256")))
                self.assertEqual(self.request, (self.destination_seal / "request.txt").read_bytes())
                self.assertEqual({"Local", "Save", "Synced", "load-source-evidence.json"}, {p.name for p in self.destination.iterdir()})
                self.assertIs(evidence["processAuthority"], False)
                for name in ("process-ownership.json", "scenario-save-receipt.txt"):
                    self.assertEqual(sha((self.source / name).read_bytes()), evidence["sourceHashes"][name])
                self.assertEqual(sha((self.seal / "profile.sha256").read_bytes()), evidence["sourceHashes"][".seal/profile.sha256"])
                self.assertEqual(evidence, json.loads((self.destination / "load-source-evidence.json").read_bytes()))

    def test_occupied_destination_or_seal_refuses_without_overwrite(self):
        for which in ("destination", "destination_seal"):
            with self.subTest(which=which):
                self.fixture()
                target = getattr(self, which)
                target.mkdir()
                (target / "foreign").write_bytes(b"do not overwrite")
                before = self.before(), snapshot(target)
                with self.assertRaisesRegex(ValueError, "occupied"):
                    load.prepare(self.source, self.destination, self.stopped)
                self.assertEqual(before, (self.before(), snapshot(target)))
                self.assertEqual([], self.calls)

    def test_source_seal_is_closed_both_ways_and_request_must_match(self):
        for mutation in ("modify", "extra", "missing", "request", "load"):
            with self.subTest(mutation=mutation):
                self.fixture()
                path = self.mod / "Core/A.cs"
                if mutation == "modify": path.write_bytes(b"changed")
                elif mutation == "extra": (self.local / "Injected.cs").write_bytes(b"extra")
                elif mutation == "missing": path.unlink()
                elif mutation == "request": (self.seal / "request.txt").write_bytes(b"founding-first-city;seed=#4243\n")
                else:
                    (self.local / "scenario-load.txt").write_bytes(b"already load")
                    with contextlib.redirect_stdout(io.StringIO()):
                        load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
                self.assert_refuses_unchanged()

    def test_hash_mismatch_and_noncanonical_native_receipts_refuse(self):
        for mutation in ("Primary.sav.gz", "Primary.json", "snapshot", "crlf", "six-lines", "uppercase", "oversized"):
            with self.subTest(mutation=mutation):
                self.fixture()
                receipt = self.source / "scenario-save-receipt.txt"
                if mutation in load.SAVE_FILES: (self.save / mutation).write_bytes(b"changed")
                elif mutation == "snapshot": (self.source / "scenario-save-snapshot.txt").write_bytes(b"changed")
                elif mutation == "crlf": receipt.write_bytes(receipt.read_bytes().replace(b"\n", b"\r\n"))
                elif mutation == "six-lines": receipt.write_bytes(receipt.read_bytes() + b"extra\n")
                elif mutation == "uppercase": receipt.write_bytes(receipt.read_bytes().replace(self.game_id.encode(), self.game_id.upper().encode()))
                else: receipt.write_bytes(b"x" * 513)
                self.assert_refuses_unchanged()

    def test_second_save_unknown_children_and_unsettled_cache_are_rejected(self):
        for extra in ("second-save", "Cache.db-wal", "Cache.db-shm", "unknown.bin"):
            with self.subTest(extra=extra):
                self.fixture()
                if extra == "second-save": (self.save.parent / "fedcba98-7654-3210-fedc-ba9876543210").mkdir()
                else: (self.save / extra).write_bytes(b"unproved")
                self.assert_refuses_unchanged()

    def test_original_ownership_receipt_must_exist_and_be_canonical(self):
        for mutation in ("missing", "pretty", "boolean-pid", "foreign-root", "ticks-alias", "extra-argument"):
            with self.subTest(mutation=mutation):
                self.fixture()
                path = self.source / "process-ownership.json"
                value = json.loads(path.read_bytes())
                if mutation == "missing": path.unlink()
                elif mutation == "pretty": path.write_text(json.dumps(value, indent=2), encoding="utf-8")
                else:
                    if mutation == "boolean-pid": value["pid"] = True
                    elif mutation == "foreign-root": value["root"] = r"C:\taf-scenario.Foreign"
                    elif mutation == "ticks-alias": value["startTicks"] = "0" + value["startTicks"]
                    else: value["arguments"].append("extra")
                    path.write_text(json.dumps(value, separators=(",", ":")), encoding="utf-8")
                self.assert_refuses_unchanged()
                self.assertEqual([], self.calls)

    def test_links_and_hardlinks_in_copied_or_uncopied_source_are_rejected(self):
        for where in ("local", "save", "uncopied", "directory", "hardlink"):
            with self.subTest(where=where):
                self.fixture()
                outside = self.base / ("outside" + str(self.number))
                outside.write_bytes(b"external target retained")
                if where == "local": (self.local / "link").symlink_to(outside)
                elif where == "uncopied": (self.source / "link").symlink_to(outside)
                elif where == "directory": (self.local / "linked-directory").symlink_to(self.save, target_is_directory=True)
                elif where == "hardlink": os.link(outside, self.source / "hardlink")
                else:
                    (self.save / "Cache.db").unlink()
                    (self.save / "Cache.db").symlink_to(outside)
                self.assert_refuses_unchanged()
                self.assertEqual(b"external target retained", outside.read_bytes())

    def test_linked_source_ancestor_or_destination_is_refused(self):
        self.fixture()
        alias = self.base / "alias"
        alias.symlink_to(self.base, target_is_directory=True)
        before = self.before()
        with self.assertRaises(ValueError):
            load.prepare(alias / self.source.name, alias / self.destination.name, self.stopped)
        self.destination.symlink_to(self.base, target_is_directory=True)
        with self.assertRaises(ValueError):
            load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual(before, self.before())
        self.assertEqual([], self.calls)

    def test_stopped_authority_failure_precedes_save_reads_and_every_output(self):
        self.fixture()
        before = self.before()
        def refuse(source):
            self.stopped(source)
            raise ValueError("still live or global process ownership unknown")
        with mock.patch.object(load, "digest", side_effect=AssertionError("save/cache read before stop proof")):
            with self.assertRaisesRegex(ValueError, "still live"):
                load.prepare(self.source, self.destination, refuse)
        self.assertEqual([self.source], self.calls)
        self.assertEqual(before, self.before())
        self.assertFalse(self.destination.exists())
        self.assertFalse(self.destination_seal.exists())

    def test_postcopy_source_changes_refuse_and_retain_partial_destination(self):
        self.fixture()
        original = load.copy_new
        changed = []
        def copy_then_change(source, destination, expected, limit):
            original(source, destination, expected, limit)
            if source == self.save / "Cache.db":
                (self.save / "Primary.sav.gz").write_bytes(b"external concurrent change")
                changed.append(self.before())
        with mock.patch.object(load, "copy_new", side_effect=copy_then_change):
            with self.assertRaisesRegex(ValueError, "source save file changed"):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual([self.before()], changed)
        self.assertTrue((self.destination / "Synced/Saves" / self.game_id / "Cache.db").exists())
        self.assertFalse((self.destination_seal / "profile.sha256").exists())
        self.assertFalse((self.destination / "load-source-evidence.json").exists())

    def test_unproved_destination_injection_is_not_sealed_or_overwritten(self):
        for race in ("extra", "occupied-copy"):
            with self.subTest(race=race):
                self.fixture()
                before = self.before()
                original = load.copy_new
                injected = []
                def inject(source, destination, expected, limit):
                    if not injected and (race == "occupied-copy" or source == self.save / "Cache.db"):
                        path = destination if race == "occupied-copy" else self.destination / "Local/Injected.cs"
                        path.write_bytes(b"foreign bytes retained")
                        injected.append(path)
                    original(source, destination, expected, limit)
                with mock.patch.object(load, "copy_new", side_effect=inject):
                    with self.assertRaises((ValueError, FileExistsError)):
                        load.prepare(self.source, self.destination, self.stopped)
                self.assertEqual(before, self.before())
                self.assertEqual(b"foreign bytes retained", injected[0].read_bytes())
                self.assertFalse((self.destination_seal / "profile.sha256").exists())

    def test_cli_has_exact_root_domain_and_always_uses_real_stopped_verifier(self):
        for root in ("/tmp/taf-scenario.A", "/mnt/c/taf-scenario.A/", "/mnt/c/taf-scenario.A/../taf-scenario.B", "/mnt/c/taf-scenario.a-b"):
            with self.subTest(root=root), mock.patch.object(load, "prepare") as prepare, contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, load.main(["prepare", root, "/mnt/c/taf-scenario.Dest"]))
                prepare.assert_not_called()
        with mock.patch.object(load, "prepare") as prepare, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, load.main(["prepare", "/mnt/c/taf-scenario.Source", "/mnt/c/taf-scenario.Dest"]))
            prepare.assert_called_once_with(Path("/mnt/c/taf-scenario.Source"), Path("/mnt/c/taf-scenario.Dest"), load.assert_source_stopped)
        with mock.patch.dict(os.environ, {}, clear=True), mock.patch.object(load, "windows_path", side_effect=lambda p: "WIN:" + str(p)), mock.patch.object(load.subprocess, "run") as run:
            load.assert_source_stopped(Path("/mnt/c/taf-scenario.Source"))
            command = run.call_args.args[0]
            self.assertEqual(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"], command[:5])
            self.assertEqual("WIN:" + str(TOOLS / "assert-scenario-source-stopped.ps1"), command[5])
            self.assertEqual(["-Root", "WIN:/mnt/c/taf-scenario.Source", "-Game", r"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ.exe"], command[6:])
            self.assertTrue(run.call_args.kwargs["check"])


if __name__ == "__main__":
    unittest.main()
