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
import shutil
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
                self.assertEqual({"Local", "Save", "Synced", "load-source-evidence.json", "load-phase-timings.json"},
                                 {p.name for p in self.destination.iterdir()})
                self.assertIs(evidence["processAuthority"], False)
                for name in ("process-ownership.json", "scenario-save-receipt.txt"):
                    self.assertEqual(sha((self.source / name).read_bytes()), evidence["sourceHashes"][name])
                self.assertEqual(sha((self.seal / "profile.sha256").read_bytes()), evidence["sourceHashes"][".seal/profile.sha256"])
                self.assertEqual(evidence, json.loads((self.destination / "load-source-evidence.json").read_bytes()))

    def test_every_destination_directory_is_opened_exactly_once_before_any_worker_starts(self):
        self.fixture()
        for index in range(load.MAX_COPY_WORKERS * 3):
            (self.mod / ("Core/Many%d.cs" % index)).write_bytes(("class Many%d {}" % index).encode("ascii"))
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        real_open_root, real_open_chain, real_copy_new = (
            load.open_validated_root, load.open_directory_chain, load.copy_new)
        events = []
        real_tree, real_copies = load.make_directory_tree, load.copy_new_files
        def tree_spy(*args):
            events.clear()
            return real_tree(*args)
        def copies_spy(*args):
            try:
                return real_copies(*args)
            finally:
                events.append(("end_parallel", None))
        def open_root_spy(path):
            events.append(("open_root", path))
            return real_open_root(path)
        def open_chain_spy(root_fd, components):
            events.append(("open_chain", components))
            return real_open_chain(root_fd, components)
        def copy_new_spy(source, destination, expected, limit, dir_fd=None):
            events.append(("copy", destination))
            real_copy_new(source, destination, expected, limit, dir_fd)
        with mock.patch.object(load, "open_validated_root", side_effect=open_root_spy), \
             mock.patch.object(load, "open_directory_chain", side_effect=open_chain_spy), \
             mock.patch.object(load, "copy_new", side_effect=copy_new_spy), \
             mock.patch.object(load, "make_directory_tree", side_effect=tree_spy), \
             mock.patch.object(load, "copy_new_files", side_effect=copies_spy):
            load.prepare(self.source, self.destination, self.stopped)
        events = events[:events.index(("end_parallel", None))]
        first_copy_index = next(index for index, event in enumerate(events) if event[0] == "copy")
        opens_before_any_copy = [event for event in events[:first_copy_index] if event[0] != "copy"]
        opens_after = [event for event in events[first_copy_index:] if event[0] != "copy"]
        # Every directory open (root anchor + every one-component descent) happens strictly before
        # the first worker's copy_new call -- not interleaved with copies, and never repeated once
        # per file. Exactly one open per unique directory: one root, one chain-open per
        # subdirectory (Mods, Mods/ThousandAndFirst, Mods/ThousandAndFirst/Harness,
        # Mods/ThousandAndFirst/Core, Empty) -- five subdirectories in this fixture.
        self.assertEqual([], opens_after)
        self.assertEqual(1, sum(1 for kind, _ in opens_before_any_copy if kind == "open_root"))
        chain_opens = [components for kind, components in opens_before_any_copy if kind == "open_chain"]
        # open_validated_root itself now walks from "/" via ONE open_directory_chain call carrying
        # every component of target_local's absolute path (see its docstring); make_directory_tree
        # then opens each of the five subdirectories with its OWN single-component call. Both are
        # real open_directory_chain calls; they are told apart here only by component count.
        root_anchor_calls = [c for c in chain_opens if len(c) > 1]
        subdirectory_calls = [c for c in chain_opens if len(c) == 1]
        self.assertEqual(1, len(root_anchor_calls))
        self.assertEqual(5, len(subdirectory_calls))
        self.assertEqual(len(subdirectory_calls), len(set(subdirectory_calls)))

    def test_phase_timings_are_recorded_in_order_and_never_gate_success(self):
        self.fixture()
        load.prepare(self.source, self.destination, self.stopped)
        timings = json.loads((self.destination / "load-phase-timings.json").read_bytes())
        self.assertEqual("taf-scenario-load-phase-timings-v1", timings["schema"])
        phases = timings["phases"]
        expected_names = ["preflight-and-stop-authority", "source-validation", "destination-setup",
                           "local-copy", "post-copy-target-inventory", "save-copy", "source-reproof",
                           "seal-computation"]
        self.assertEqual(expected_names, [phase["phase"] for phase in phases])
        for phase in phases:
            self.assertGreaterEqual(phase["seconds"], 0)
            self.assertLessEqual(phase["beginISO"], phase["endISO"])
        local_copy = next(phase for phase in phases if phase["phase"] == "local-copy")
        self.assertEqual(len(load.scenario_profile.inventory(str(self.local))), local_copy["files"])
        self.assertGreaterEqual(local_copy["bytes"], 0)

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
        def copy_then_change(source, destination, expected, limit, dir_fd=None):
            original(source, destination, expected, limit, dir_fd)
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
        self.assertFalse((self.destination / "load-phase-timings.json").exists())

    def test_timings_write_failure_leaves_no_seal_or_evidence_behind(self):
        self.fixture()
        before = self.before()
        real_write_new = load.write_new
        def flaky_write_new(path, data):
            if path.name == "load-phase-timings.json":
                raise OSError("synthetic disk error writing phase timings")
            real_write_new(path, data)
        with mock.patch.object(load, "write_new", side_effect=flaky_write_new):
            with self.assertRaisesRegex(OSError, "synthetic disk error"):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual(before, self.before())
        # The timings write happens BEFORE the seal/request/evidence writes precisely so that its
        # own failure refuses the whole profile instead of leaving an already-sealed destination
        # behind a refusal that would look like the load itself failed.
        self.assertFalse((self.destination / "load-phase-timings.json").exists())
        self.assertFalse((self.destination_seal / "profile.sha256").exists())
        self.assertFalse((self.destination_seal / "request.txt").exists())
        self.assertFalse((self.destination / "load-source-evidence.json").exists())

    def test_leaf_directory_symlink_swap_after_its_own_open_cannot_escape_via_dir_fd(self):
        self.fixture()
        for index in range(load.MAX_COPY_WORKERS * 2):
            (self.mod / ("Core/Swap%d.cs" % index)).write_bytes(("class Swap%d {}" % index).encode("ascii"))
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        outside = self.base / ("outside-escape" + str(self.number))
        outside.mkdir()
        # Swap the LEAF "Core" directory specifically, right after ITS OWN open_directory_chain
        # call: it directly holds many copied files and has no descendant directory, so nothing
        # else in this walk would independently notice the swap first.
        target_core = self.destination / "Local/Mods/ThousandAndFirst/Core"
        real_open_chain = load.open_directory_chain
        swapped = []
        def swap_after_open(root_fd, components):
            # The fd this returns is already open and anchored to the ORIGINAL inode (see
            # open_directory_chain's docstring). Deleting and resymlinking the NAME right after
            # simulates an attacker winning the exact race this fix closes: every worker that
            # submits after this point still only holds (and creates through) that original fd.
            fd = real_open_chain(root_fd, components)
            if components == ("Core",) and not swapped:
                shutil.rmtree(target_core)
                target_core.symlink_to(outside, target_is_directory=True)
                swapped.append(target_core)
            return fd
        with mock.patch.object(load, "open_directory_chain", side_effect=swap_after_open):
            # A later phase (e.g. scenario_profile.inventory's own by-name walk) correctly refuses
            # once "Core" is a symlink -- that is defense in depth, not the property under test.
            # The property under test is what happens to the in-flight Core workers themselves.
            with self.assertRaises((ValueError, OSError)):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual([target_core], swapped)
        # The actual security property: not one byte from any of the many files destined for the
        # hijacked "Core" name was ever written into the attacker-controlled directory, regardless
        # of how the rest of prepare() reacts afterward.
        self.assertEqual([], list(outside.iterdir()))

    def test_intermediate_ancestor_symlink_swap_after_its_own_open_cannot_escape_via_dir_fd(self):
        self.fixture()
        for index in range(load.MAX_COPY_WORKERS * 2):
            (self.mod / ("Core/Swap%d.cs" % index)).write_bytes(("class Swap%d {}" % index).encode("ascii"))
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        outside = self.base / ("outside-escape" + str(self.number))
        # A REAL "Core" directory at the position an attacker would want it: if anything below the
        # swapped ancestor ever re-resolved "Core" by a full path instead of by the fd this walk
        # already holds, the lookup would silently succeed against this decoy and leak files here.
        (outside / "Core").mkdir(parents=True)
        # Swap "ThousandAndFirst" -- an INTERMEDIATE ancestor of both Core and Harness, not a leaf
        # -- right after its OWN open_directory_chain call returns, before Core or Harness (its
        # children) are created. This is the exact gap the leaf-only test above cannot exercise:
        # both children are opened AFTER this swap, by resolving one component ("Core"/"Harness")
        # relative to the fd this call already returned, never by re-walking "ThousandAndFirst"'s
        # NAME again.
        target_intermediate = self.destination / "Local/Mods/ThousandAndFirst"
        real_open_chain = load.open_directory_chain
        swapped = []
        def swap_after_open(root_fd, components):
            fd = real_open_chain(root_fd, components)
            if components == ("ThousandAndFirst",) and not swapped:
                shutil.rmtree(target_intermediate)
                target_intermediate.symlink_to(outside, target_is_directory=True)
                swapped.append(target_intermediate)
            return fd
        with mock.patch.object(load, "open_directory_chain", side_effect=swap_after_open):
            with self.assertRaises((ValueError, OSError)):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual([target_intermediate], swapped)
        # The decoy "Core" inside the attacker-controlled "outside" tree must stay completely
        # empty: every create for Core/Harness (children of the swapped name) still resolves
        # through the fd this walk already held for the TRUE "ThousandAndFirst" directory.
        self.assertEqual([], list((outside / "Core").iterdir()))
        self.assertEqual(["Core"], [p.name for p in outside.iterdir()])

    def test_ancestor_of_root_symlink_swap_before_root_open_is_refused_with_zero_outside_writes(self):
        self.fixture()
        for index in range(load.MAX_COPY_WORKERS * 2):
            (self.mod / ("Core/Swap%d.cs" % index)).write_bytes(("class Swap%d {}" % index).encode("ascii"))
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        outside = self.base / ("outside-escape" + str(self.number))
        # A REAL "Local" directory at the position a by-name reopen of target_local would have
        # found it before this fix's from-"/" walk existed -- if the fix were insufficient, this
        # decoy would silently absorb the whole copy instead of the walk refusing outright.
        (outside / "Local").mkdir(parents=True)
        real_open_validated_root = load.open_validated_root
        swapped = []
        def swap_before_root_open(path):
            if path == self.destination / "Local" and not swapped:
                # Simulates ANOTHER PROCESS winning the race between the earlier directory()
                # ancestor proof (inside empty_destination, on `destination`'s own parent chain)
                # and this call: swap `destination` itself -- an ANCESTOR of target_local, not
                # target_local's own name, and not something any leaf/intermediate-inside-Local
                # test above touches -- for a symlink pointing at a tree with a matching decoy
                # "Local". target_local (created moments earlier, still empty) is removed with it.
                shutil.rmtree(self.destination)
                self.destination.symlink_to(outside, target_is_directory=True)
                swapped.append(self.destination)
            return real_open_validated_root(path)
        with mock.patch.object(load, "open_validated_root", side_effect=swap_before_root_open):
            with self.assertRaises((ValueError, OSError)):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual([self.destination], swapped)
        # Walking from "/" makes the swapped `destination` name itself just another single
        # component with its own O_NOFOLLOW|O_DIRECTORY check: the walk refuses AT that component,
        # before ever attempting to open "Local" beneath it, so nothing was ever written anywhere,
        # and the decoy "Local" planted to match stays completely untouched.
        self.assertEqual([], list((outside / "Local").iterdir()))

    def test_one_bad_local_file_among_many_refuses_sealing_but_joins_every_worker(self):
        self.fixture()
        # Widen past a single worker's share so the fan-out actually spans multiple threads.
        for index in range(load.MAX_COPY_WORKERS * 3):
            (self.mod / ("Core/Extra%d.cs" % index)).write_bytes(("class Extra%d {}" % index).encode("ascii"))
        with contextlib.redirect_stdout(io.StringIO()):
            load.scenario_profile.seal(str(self.local), str(self.seal / "profile.sha256"))
        before = self.before()
        bad_file = self.mod / "Core/Extra1.cs"
        real_copy_new = load.copy_new
        completed = []
        def flaky(source, destination, expected, limit, dir_fd=None):
            if source == bad_file:
                raise ValueError("synthetic single-worker failure")
            real_copy_new(source, destination, expected, limit, dir_fd)
            completed.append(destination)
        with mock.patch.object(load, "copy_new", side_effect=flaky):
            with self.assertRaisesRegex(ValueError, "synthetic single-worker failure"):
                load.prepare(self.source, self.destination, self.stopped)
        self.assertEqual(before, self.before())
        self.assertFalse((self.destination_seal / "profile.sha256").exists())
        self.assertFalse((self.destination / "load-source-evidence.json").exists())
        self.assertFalse((self.destination / "load-phase-timings.json").exists())
        # Every sibling worker still ran to completion (was joined) despite the one failure.
        self.assertGreater(len(completed), load.MAX_COPY_WORKERS)

    def test_unproved_destination_injection_is_not_sealed_or_overwritten(self):
        for race in ("extra", "occupied-copy"):
            with self.subTest(race=race):
                self.fixture()
                before = self.before()
                original = load.copy_new
                injected = []
                def inject(source, destination, expected, limit, dir_fd=None):
                    if not injected and (race == "occupied-copy" or source == self.save / "Cache.db"):
                        path = destination if race == "occupied-copy" else self.destination / "Local/Injected.cs"
                        path.write_bytes(b"foreign bytes retained")
                        injected.append(path)
                    original(source, destination, expected, limit, dir_fd)
                with mock.patch.object(load, "copy_new", side_effect=inject):
                    with self.assertRaises((ValueError, FileExistsError)):
                        load.prepare(self.source, self.destination, self.stopped)
                self.assertEqual(before, self.before())
                self.assertEqual(b"foreign bytes retained", injected[0].read_bytes())
                self.assertFalse((self.destination_seal / "profile.sha256").exists())
                self.assertFalse((self.destination / "load-phase-timings.json").exists())

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




class CopyNewFilesTest(unittest.TestCase):
    """Direct contracts for the bounded parallel fan-out, independent of the full profile.

    Pairs carry a real dir_fd opened via open_validated_root/open_directory_chain, exactly as
    prepare()'s make_directory_tree would hand them to copy_new_files -- these tests never pass
    None here, that path belongs to the three serial save-file copies only.
    """

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="taf-copy-new-files-test.")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.source_dir = self.base / "source"
        self.dest_dir = self.base / "dest"
        self.source_dir.mkdir()
        self.dest_dir.mkdir()
        self.dest_fd = load.open_validated_root(self.dest_dir)
        self.addCleanup(lambda: os.close(self.dest_fd))

    def make_pairs(self, count, dir_fd=None):
        pairs = []
        for index in range(count):
            content = ("payload-%d" % index).encode("ascii")
            source = self.source_dir / ("file-%03d.bin" % index)
            source.write_bytes(content)
            pairs.append((source, self.dest_dir / source.name, sha(content), dir_fd if dir_fd is not None else self.dest_fd))
        return pairs

    def test_copies_every_pair_with_matching_inventory_more_files_than_workers(self):
        pairs = self.make_pairs(load.MAX_COPY_WORKERS * 3 + 1)
        load.copy_new_files(pairs, load.MAX_LOCAL_FILE)
        for source, destination, expected, _ in pairs:
            self.assertEqual(source.read_bytes(), destination.read_bytes())
            self.assertEqual(expected, sha(destination.read_bytes()))
        source_inventory = load.scenario_profile.inventory(str(self.source_dir))
        dest_inventory = load.scenario_profile.inventory(str(self.dest_dir))
        self.assertEqual(source_inventory, dest_inventory)

    def test_empty_pairs_is_a_noop(self):
        load.copy_new_files([], load.MAX_LOCAL_FILE)
        self.assertEqual([], list(self.dest_dir.iterdir()))

    def test_worker_count_is_bounded_and_never_exceeds_pair_count(self):
        seen = []
        real_executor = load.concurrent.futures.ThreadPoolExecutor
        def spy(max_workers=None, **kwargs):
            seen.append(max_workers)
            return real_executor(max_workers=max_workers, **kwargs)
        few_pairs = self.make_pairs(2)
        (self.dest_dir / "many").mkdir()
        many_fd = load.open_directory_chain(self.dest_fd, ("many",))
        self.addCleanup(lambda: os.close(many_fd))
        many_pairs = self.make_pairs(load.MAX_COPY_WORKERS * 5, dir_fd=many_fd)
        many_pairs = [(source, self.dest_dir / "many" / destination.name, expected, dir_fd)
                      for source, destination, expected, dir_fd in many_pairs]
        with mock.patch.object(load.concurrent.futures, "ThreadPoolExecutor", side_effect=spy):
            load.copy_new_files(few_pairs, load.MAX_LOCAL_FILE)
            load.copy_new_files(many_pairs, load.MAX_LOCAL_FILE)
        self.assertEqual([2, load.MAX_COPY_WORKERS], seen)

    def test_one_failing_worker_is_reported_after_every_worker_is_joined(self):
        pairs = self.make_pairs(load.MAX_COPY_WORKERS * 4)
        bad_source, bad_destination, _, bad_dir_fd = pairs[len(pairs) // 2]
        pairs[len(pairs) // 2] = (bad_source, bad_destination, "0" * 64, bad_dir_fd)  # wrong hash
        with self.assertRaisesRegex(ValueError, "copy differs from frozen source"):
            load.copy_new_files(pairs, load.MAX_LOCAL_FILE)
        # Every OTHER worker still ran to completion (was joined) before the failure propagated:
        # ThreadPoolExecutor's context manager waits for every submitted future either way.
        completed = [destination for _, destination, _, _ in pairs if destination != bad_destination]
        for destination in completed:
            self.assertTrue(destination.exists(), "sibling worker output missing: " + str(destination))
        # The module's stated contract is "refusals retain any partial destination, no cleanup
        # mode": the bytes were faithfully copied from source, only the (deliberately wrong)
        # expected hash supplied by this test made copy_new's readback proof fail afterward.
        self.assertTrue(bad_destination.exists())
        self.assertEqual(bad_source.read_bytes(), bad_destination.read_bytes())

    def test_source_read_failure_in_one_worker_does_not_abort_siblings(self):
        pairs = self.make_pairs(load.MAX_COPY_WORKERS * 2)
        missing_source, missing_destination, expected, _ = pairs[0]
        missing_source.unlink()
        with self.assertRaises(OSError):
            load.copy_new_files(pairs, load.MAX_LOCAL_FILE)
        for source, destination, _, _ in pairs[1:]:
            self.assertTrue(destination.exists(), "sibling worker output missing: " + str(destination))


class AnchoredDestinationWritesTest(unittest.TestCase):
    def test_every_output_kind_refuses_swapped_ancestor_without_outside_write(self):
        for kind in ("directory", "receipt", "save"):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                ancestor, outside = root / "ancestor", root / "outside"
                parent = ancestor / "parent"
                parent.mkdir(parents=True)
                (outside / "parent").mkdir(parents=True)
                source = root / "source"
                source.write_bytes(b"sealed bytes")
                real_open = load.open_validated_root
                swapped = []
                def swap(path):
                    if path == parent and not swapped:
                        ancestor.rename(root / "retained")
                        ancestor.symlink_to(outside, target_is_directory=True)
                        swapped.append(True)
                    return real_open(path)
                with mock.patch.object(load, "open_validated_root", side_effect=swap):
                    with self.assertRaises((ValueError, OSError)):
                        if kind == "directory":
                            load.create_directory(parent / "new")
                        elif kind == "receipt":
                            load.write_new(parent / "new", b"receipt")
                        else:
                            load.copy_new(source, parent / "new", sha(b"sealed bytes"), 100)
                self.assertEqual([True], swapped)
                self.assertEqual([], list((outside / "parent").iterdir()))


if __name__ == "__main__":
    unittest.main()
