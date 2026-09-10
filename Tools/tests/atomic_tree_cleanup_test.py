"""Cleanup identity-pin protocol (issue #115).

The pre-fix protocol split admission, removal and post-proof across separate helper
processes. The last descriptor on the removed inode therefore closed before the proof
ran, the kernel freed the inode number immediately, and an unrelated directory created
by a concurrent producer (DevTests/KingdomSealStoreTests.cs:153 creates
``taf-seal-store-<guid>`` under the same ``/tmp``) could be recycled onto it and be
reported as a retained exact identity, refusing a clean gate with exit 5.

Pre-fix evidence is retained and re-runnable at Tools/tests/repro_issue_115.sh.

These tests prove the fixed protocol: one process; the descriptor the removal already
opens on the QUARANTINE entry, after the sequester rename, held across the removal;
positive deletion proof via ``st_nlink == 0``; and the identity search run while that
descriptor is still open, so a match can only be a genuine second link. The descriptor
is taken post-rename because a 9p/drvfs mount keeps a stale dentry for a name held open
across its own rename, after which every lookup of the renamed entry returns ENOENT.
"""

from __future__ import annotations

import contextlib
import fcntl
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import threading
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
HELPER = ROOT / "Tools" / "atomic_tree_publish.py"
REPRO = ROOT / "Tools" / "tests" / "repro_issue_115.sh"

# The exact prefix an unrelated licensed suite creates under the shared parent.
FOREIGN_PREFIX = "taf-seal-store-"
# Generous bound: the freed inode was measured to be recycled on the FIRST creation.
REUSE_ATTEMPTS = 2000
# Bound for the non-reuse probe taken while a descriptor is held: this must assert an
# exact zero, so it stays small enough to run on a 9p parent as well.
REUSE_PROBE_ATTEMPTS = 300

_spec = importlib.util.spec_from_file_location("taf_atomic_tree_publish", HELPER)
publish = importlib.util.module_from_spec(_spec)
assert _spec.loader is not None
_spec.loader.exec_module(publish)


def _identity(status: os.stat_result) -> str:
    return f"{status.st_dev}:{status.st_ino}"


def _drvfs_base() -> str | None:
    candidate = "/mnt/c/Users/Public"
    if os.path.isdir(candidate) and os.access(candidate, os.W_OK):
        return candidate
    return None


class LockedParent:
    """A private publication parent holding the exclusive transaction flock."""

    def __init__(self, base: str | None = None) -> None:
        self.path = tempfile.mkdtemp(dir=base, prefix="taf-cleanup-test-")
        self.fd = os.open(self.path, os.O_RDONLY | os.O_DIRECTORY)
        fcntl.flock(self.fd, fcntl.LOCK_EX)
        self.identity = _identity(os.fstat(self.fd))

    def close(self) -> None:
        fcntl.flock(self.fd, fcntl.LOCK_UN)
        os.close(self.fd)
        shutil.rmtree(self.path, ignore_errors=True)

    def run(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(HELPER),
                *arguments,
                "--parent",
                self.path,
                "--parent-id",
                self.identity,
                "--lock-fd",
                str(self.fd),
            ],
            capture_output=True,
            text=True,
            pass_fds=(self.fd,),
            check=False,
        )

    def cleanup(self, name: str, kind: str, expected: str):
        return self.run(
            "cleanup", "--name", name, "--kind", kind, "--expected-id", expected
        )

    def entries(self) -> list[str]:
        return sorted(os.listdir(self.fd))

    def make_directory(self, name: str) -> str:
        os.mkdir(name, dir_fd=self.fd)
        return _identity(os.stat(name, dir_fd=self.fd, follow_symlinks=False))

    def make_file(self, name: str) -> str:
        descriptor = os.open(name, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600, dir_fd=self.fd)
        os.close(descriptor)
        return _identity(os.stat(name, dir_fd=self.fd, follow_symlinks=False))

    def try_reuse(self, target: str, attempts: int = REUSE_ATTEMPTS) -> str | None:
        """Let an unrelated producer try to be recycled onto ``target``'s inode."""
        for index in range(attempts):
            name = f"{FOREIGN_PREFIX}{index:032x}"
            os.mkdir(name, dir_fd=self.fd)
            if _identity(os.stat(name, dir_fd=self.fd, follow_symlinks=False)) == target:
                return name
            os.rmdir(name, dir_fd=self.fd)
        return None


class CleanupIdentityPinTest(unittest.TestCase):
    # Subclasses that run against another filesystem set this.
    BASE: str | None = None

    def setUp(self) -> None:
        self.parent = LockedParent(base=self.BASE)
        self.addCleanup(self.parent.close)

    def sibling_directory(self) -> str:
        """A scratch directory on the SAME filesystem as the publication parent."""
        path = tempfile.mkdtemp(dir=self.BASE, prefix="taf-cleanup-elsewhere-")
        self.addCleanup(shutil.rmtree, path, True)
        return path

    def test_retained_evidence_script_is_kept(self) -> None:
        """The pre-fix reproduction stays in the tree, executable and re-runnable."""
        self.assertTrue(REPRO.is_file())
        self.assertTrue(os.access(REPRO, os.X_OK))

    def test_pin_denies_inode_recycling_and_yields_no_false_identity(self) -> None:
        """(a) The mechanism: while the removal's own descriptor is open the released
        inode cannot be recycled, so the identity search cannot produce a false match."""
        observed: list[tuple[int, str, tuple[tuple[str, str], ...], str | None]] = []

        def prove(parent_fd, held_fd, quarantine, expected, kind):
            pinned = os.fstat(held_fd)
            recycled = self.parent.try_reuse(expected)
            observed.append(
                (
                    pinned.st_nlink,
                    _identity(pinned),
                    publish._locate_matches(parent_fd, expected, kind),
                    recycled,
                )
            )
            publish._prove_identity_released(
                parent_fd, held_fd, quarantine, expected, kind
            )

        victim = self.parent.make_directory("victim")
        publish._remove_named_tree(self.parent.fd, "victim", victim, prove)
        self.assertEqual(len(observed), 1)
        links, pinned_id, matches, recycled = observed[0]
        self.assertEqual(links, 0, "removal must be positively provable")
        self.assertEqual(pinned_id, victim, "the descriptor must stay bound")
        self.assertIsNone(recycled, f"pinned inode {victim} was recycled onto {recycled}")
        self.assertEqual(matches, ())
        self.assertIsNone(publish._entry_status(self.parent.fd, "victim"))

    def test_held_descriptor_denies_inode_recycling_for_a_file(self) -> None:
        """(a) The same mechanism on a regular file. This case runs on EVERY supported
        filesystem, including drvfs, so the held-descriptor guarantee is never left
        unexercised by the directory-seal limitation."""
        observed: list[tuple[int, str, tuple[tuple[str, str], ...], str | None]] = []

        def prove(parent_fd, held_fd, quarantine, expected, kind):
            pinned = os.fstat(held_fd)
            recycled = self.parent.try_reuse(expected, attempts=REUSE_PROBE_ATTEMPTS)
            observed.append(
                (
                    pinned.st_nlink,
                    _identity(pinned),
                    publish._locate_matches(
                        parent_fd, expected, kind, tolerate_vanished=True
                    ),
                    recycled,
                )
            )
            publish._prove_identity_released(
                parent_fd, held_fd, quarantine, expected, kind
            )

        victim = self.parent.make_file("victim")
        publish._remove_named_file(self.parent.fd, "victim", victim, prove)
        self.assertEqual(len(observed), 1)
        links, pinned_id, matches, recycled = observed[0]
        self.assertEqual(links, 0, "removal must be positively provable")
        self.assertEqual(pinned_id, victim, "the descriptor must stay bound")
        self.assertIsNone(recycled, f"pinned inode {victim} was recycled onto {recycled}")
        self.assertEqual(matches, ())
        self.assertIsNone(publish._entry_status(self.parent.fd, "victim"))
        self.assertEqual(self.parent.entries(), [])

    def test_unpinned_inode_is_recyclable(self) -> None:
        """The environmental precondition the pre-fix protocol tripped over."""
        victim = self.parent.make_directory("victim")
        publish._remove_named_tree(self.parent.fd, "victim", victim)
        recycled = self.parent.try_reuse(victim)
        if recycled is None:
            self.skipTest("this filesystem did not recycle the freed inode")
        # With no descriptor held, the OLD post-proof would have matched this foreign
        # entry and refused a clean gate.
        self.assertEqual(
            publish._locate_matches(self.parent.fd, victim, "directory"),
            ((recycled, victim),),
        )

    def test_native_directory_cleanup_removes_and_proves(self) -> None:
        victim = self.parent.make_directory("victim")
        os.mkdir("victim/nested", dir_fd=self.parent.fd)
        result = self.parent.cleanup("victim", "directory", victim)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(result.stdout.strip(), "removed")
        self.assertEqual(self.parent.entries(), [])

    def test_native_file_cleanup_removes_and_proves(self) -> None:
        victim = self.parent.make_file("victim")
        result = self.parent.cleanup("victim", "file", victim)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(result.stdout.strip(), "removed")
        self.assertEqual(self.parent.entries(), [])

    def test_cleanup_removes_under_a_concurrent_foreign_producer(self) -> None:
        """(a) End to end: a concurrent producer cannot make a clean removal refuse."""
        victim = self.parent.make_directory("victim")
        stop = threading.Event()

        def churn() -> None:
            index = 0
            while not stop.is_set() and index < 4000:
                name = f"{FOREIGN_PREFIX}churn{index:026x}"
                try:
                    os.mkdir(name, dir_fd=self.parent.fd)
                    os.rmdir(name, dir_fd=self.parent.fd)
                except OSError:
                    return
                index += 1

        producer = threading.Thread(target=churn, daemon=True)
        producer.start()
        try:
            result = self.parent.cleanup("victim", "directory", victim)
        finally:
            stop.set()
            producer.join(timeout=10)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(result.stdout.strip(), "removed")

    def test_absent_entry_is_reported_not_refused(self) -> None:
        result = self.parent.cleanup("victim", "directory", "1:1")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), "absent")

    def test_absent_identity_is_reported_absent(self) -> None:
        """The genuine absent case still succeeds once the identity is nowhere here."""
        victim = self.parent.make_directory("victim")
        os.rmdir("victim", dir_fd=self.parent.fd)
        result = self.parent.cleanup("victim", "directory", victim)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), "absent")

    def test_root_review_disappearing_scan_entry_cannot_prove_owned_identity_absent(self):
        expected = self.parent.make_directory("moved")
        with os.scandir(self.parent.fd) as scan:
            entry = next(item for item in scan if item.name == "moved")

        class RenameDuringStat:
            name = "moved"

            def stat(inner, **kwargs):
                os.rename("moved", "far", src_dir_fd=self.parent.fd,
                          dst_dir_fd=self.parent.fd)
                return entry.stat(**kwargs)

        class Scan:
            def __enter__(inner):
                return iter([RenameDuringStat()])

            def __exit__(inner, *args):
                return False

        with mock.patch.object(publish.os, "scandir", return_value=Scan()):
            with self.assertRaises((FileNotFoundError, publish.RetainedEntry)):
                publish._locate_matches(self.parent.fd, expected, "directory")
        self.assertTrue(os.path.isdir(os.path.join(self.parent.path, "far")))

    def test_release_context_tolerates_unrelated_churn(self) -> None:
        """The ONLY place a vanishing entry may be skipped: after st_nlink == 0."""
        victim = self.parent.make_directory("victim")
        seen: list[tuple[tuple[str, str], ...]] = []

        def prove(parent_fd, held_fd, quarantine, expected, kind):
            self.parent.make_directory(f"{FOREIGN_PREFIX}transient")
            real_scandir = publish.os.scandir

            def vanishing(*arguments, **keywords):
                entries = list(real_scandir(*arguments, **keywords))
                for item in entries:
                    if item.name.endswith("transient"):
                        os.rmdir(item.name, dir_fd=parent_fd)
                return contextlib.nullcontext(iter(entries))

            with mock.patch.object(publish.os, "scandir", vanishing):
                seen.append(
                    publish._locate_matches(
                        parent_fd, expected, kind, tolerate_vanished=True
                    )
                )
            publish._prove_identity_released(
                parent_fd, held_fd, quarantine, expected, kind
            )

        publish._remove_named_tree(self.parent.fd, "victim", victim, prove)
        self.assertEqual(seen, [()])

    def test_root_review_renamed_owned_directory_is_not_absent_success(self) -> None:
        """Root review negative: the admitted identity moved aside is still retained."""
        expected = self.parent.make_directory("victim")
        os.rename("victim", "moved", src_dir_fd=self.parent.fd, dst_dir_fd=self.parent.fd)
        result = self.parent.cleanup("victim", "directory", expected)
        self.assertEqual(5, result.returncode, result.stdout + result.stderr)

    def test_renamed_owned_entry_names_the_retained_identity(self) -> None:
        """"absent" would lie, and the refusal must say where the identity now is."""
        victim = self.parent.make_directory("victim")
        os.rename("victim", "moved", src_dir_fd=self.parent.fd, dst_dir_fd=self.parent.fd)
        result = self.parent.cleanup("victim", "directory", victim)
        self.assertEqual(result.returncode, 5, result.stdout + result.stderr)
        self.assertIn("moved", result.stderr)
        self.assertIn(victim, result.stderr)
        self.assertEqual(self.parent.entries(), ["moved"], "nothing may be deleted")

    def test_replaced_entry_is_refused(self) -> None:
        """(b) A same-named entry that is no longer the admitted inode is refused."""
        decoy = self.parent.make_directory("decoy")
        current = self.parent.make_directory("victim")
        self.assertNotEqual(decoy, current)
        result = self.parent.cleanup("victim", "directory", decoy)
        self.assertEqual(result.returncode, 5, result.stderr)
        self.assertIn("cleanup identity ambiguous", result.stderr)
        self.assertIn(current, result.stderr)
        self.assertEqual(self.parent.entries(), ["decoy", "victim"])

    def test_kind_change_is_refused(self) -> None:
        """(b) A file standing where a directory was admitted is refused."""
        victim = self.parent.make_file("victim")
        result = self.parent.cleanup("victim", "directory", victim)
        self.assertEqual(result.returncode, 5, result.stderr)
        self.assertIn("kind=file", result.stderr)
        self.assertEqual(self.parent.entries(), ["victim"])

    def test_retained_second_link_is_refused(self) -> None:
        """(c) A genuine surviving hardlink must still refuse, and be named."""
        victim = self.parent.make_file("victim")
        os.link("victim", "shadow", src_dir_fd=self.parent.fd, dst_dir_fd=self.parent.fd)
        result = self.parent.cleanup("victim", "file", victim)
        self.assertEqual(result.returncode, 5, result.stdout + result.stderr)
        self.assertIn("links on identity", result.stderr)
        self.assertIn("shadow", result.stderr)
        self.assertEqual(
            os.stat("shadow", dir_fd=self.parent.fd, follow_symlinks=False).st_nlink, 1
        )

    def test_link_held_outside_the_parent_is_refused(self) -> None:
        """The name search cannot see a link in another directory; the count must."""
        victim = self.parent.make_file("victim")
        os.link(
            "victim",
            os.path.join(self.sibling_directory(), "kept"),
            src_dir_fd=self.parent.fd,
        )
        result = self.parent.cleanup("victim", "file", victim)
        self.assertEqual(result.returncode, 5, result.stdout + result.stderr)
        self.assertIn("cleanup left 1 links on identity", result.stderr)

    def test_pinned_locate_match_is_refused_not_ignored(self) -> None:
        """Defence in depth: nlink 0 and a named match cannot both be true, but if the
        identity search ever does report one it must refuse rather than be discarded."""

        def prove(parent_fd, held_fd, quarantine, expected, kind):
            with mock.patch.object(
                publish, "_locate_matches", return_value=(("intruder", expected),)
            ):
                publish._prove_identity_released(
                    parent_fd, held_fd, quarantine, expected, kind
                )

        victim = self.parent.make_directory("victim")
        with self.assertRaises(publish.RetainedEntry) as raised:
            publish._remove_named_tree(self.parent.fd, "victim", victim, prove)
        self.assertIn("cleanup retained exact identities", str(raised.exception))
        self.assertIn("intruder", str(raised.exception))

    def test_rebound_pin_is_refused(self) -> None:
        """If the held descriptor no longer names the admitted inode, nothing is proved."""

        def prove(parent_fd, held_fd, quarantine, expected, kind):
            real_fstat = publish.os.fstat

            def rebound(descriptor, *arguments, **keywords):
                status = real_fstat(descriptor, *arguments, **keywords)
                if descriptor != held_fd:
                    return status
                fields = list(status)
                fields[1] = status.st_ino + 1  # the descriptor names another inode
                return os.stat_result(tuple(fields))

            with mock.patch.object(publish.os, "fstat", rebound):
                publish._prove_identity_released(
                    parent_fd, held_fd, quarantine, expected, kind
                )

        victim = self.parent.make_file("victim")
        with self.assertRaises(publish.RetainedEntry) as raised:
            publish._remove_named_file(self.parent.fd, "victim", victim, prove)
        self.assertIn("rebound from identity", str(raised.exception))

    def test_filesystem_admits_the_release_proof(self) -> None:
        publish._require_identity_pin_semantics(self.parent.fd)
        self.assertEqual(self.parent.entries(), [], "no probe may be left behind")

    def test_unsupported_filesystem_semantics_refuse(self) -> None:
        """(d) A filesystem that cannot witness an unlinked inode is refused, not relaxed."""
        real_fstat = publish.os.fstat

        def stubborn_nlink(descriptor, *arguments, **keywords):
            status = real_fstat(descriptor, *arguments, **keywords)
            fields = list(status)
            fields[3] = 1  # st_nlink never reaches zero
            return os.stat_result(tuple(fields))

        with mock.patch.object(publish.os, "fstat", stubborn_nlink):
            with self.assertRaises(publish.UnsupportedPublish) as raised:
                publish._require_identity_pin_semantics(self.parent.fd)
        message = str(raised.exception)
        self.assertIn("cleanup release proof is unsupported on filesystem", message)
        self.assertIn(publish._fd_mount_fstype(self.parent.fd), message)
        self.assertIn("still reports 1 links", message)
        self.assertEqual(self.parent.entries(), [], "no probe may be left behind")

    def test_unsupported_semantics_refuse_the_whole_cleanup(self) -> None:
        """No fallback protocol: the removal itself must refuse, and delete nothing."""
        victim = self.parent.make_directory("victim")
        with mock.patch.object(
            publish,
            "_require_identity_pin_semantics",
            side_effect=publish.UnsupportedPublish("stubbed unsupported filesystem"),
        ):
            with self.assertRaises(publish.UnsupportedPublish):
                publish._cleanup(
                    SimpleNamespace(
                        parent=Path(self.parent.path),
                        parent_id=self.parent.identity,
                        lock_fd=self.parent.fd,
                        name="victim",
                        kind="directory",
                        expected_id=victim,
                    )
                )
        self.assertEqual(self.parent.entries(), ["victim"])

    def test_root_review_probe_cleanup_preserves_foreign_replacement(self) -> None:
        """Root review negative: probe cleanup is identity-bound, never name-bound.

        A concurrent peer recreates a directory at the probe's OLD name the instant the
        rehearsal rename frees it. Discarding by name would delete that foreign entry.
        """
        original = publish._renameat2
        substituted: list[str] = []

        def replace_old_name(parent_fd, source, destination, flags):
            original(parent_fd, source, destination, flags)
            os.mkdir(source, dir_fd=parent_fd)
            substituted.append(source)

        with mock.patch.object(publish, "_renameat2", side_effect=replace_old_name):
            with self.assertRaises(publish.RetainedEntry) as raised:
                publish._require_identity_pin_semantics(self.parent.fd)
        self.assertEqual(1, len(substituted))
        self.assertIn("refused to discard identity-pin probe", str(raised.exception))
        self.assertTrue(
            os.path.isdir(os.path.join(self.parent.path, substituted[0])),
            "probe cleanup removed a foreign empty replacement",
        )


@unittest.skipUnless(_drvfs_base(), "no writable drvfs (9p) mount is available")
class CleanupIdentityPinDrvfsTest(CleanupIdentityPinTest):
    """(2) drvfs is an explicitly supported parent, verified rather than assumed.

    The descriptor is opened on the quarantine entry AFTER the sequester rename, so the
    stale-old-dentry behaviour a 9p mount shows for a name held open across its own
    rename never arises, and the release proof holds here exactly as it does on ext4.
    The whole local suite is re-run against a drvfs parent.
    """

    BASE = _drvfs_base()

    # Directory removal on a 9p parent is blocked BEFORE any of this change's code by
    # _remove_named_tree's permission seal: 9p mode bits are synthetic, chmod is a
    # no-op, and the seal check therefore cannot hold. Verified identical on
    # origin/dev, so it is out of scope for #115 and recorded rather than papered over.
    DIRECTORY_REMOVAL_UNSUPPORTED = (
        "9p mode bits are synthetic, so _remove_named_tree's pre-existing permission "
        "seal refuses directory removal on drvfs (unchanged from origin/dev)"
    )

    def test_unpinned_inode_is_recyclable(self) -> None:
        self.skipTest("9p inode numbers are Windows file ids and are not promptly reused")

    def test_native_directory_cleanup_removes_and_proves(self) -> None:
        self.skipTest(self.DIRECTORY_REMOVAL_UNSUPPORTED)

    def test_cleanup_removes_under_a_concurrent_foreign_producer(self) -> None:
        self.skipTest(self.DIRECTORY_REMOVAL_UNSUPPORTED)

    def test_pin_denies_inode_recycling_and_yields_no_false_identity(self) -> None:
        self.skipTest(self.DIRECTORY_REMOVAL_UNSUPPORTED)

    def test_pinned_locate_match_is_refused_not_ignored(self) -> None:
        self.skipTest(self.DIRECTORY_REMOVAL_UNSUPPORTED)

    def test_release_context_tolerates_unrelated_churn(self) -> None:
        self.skipTest(self.DIRECTORY_REMOVAL_UNSUPPORTED)

    def test_drvfs_directory_removal_is_blocked_by_the_pre_existing_seal(self) -> None:
        """Pin down the out-of-scope limitation so a later change cannot hide it."""
        victim = self.parent.make_directory("victim")
        result = self.parent.cleanup("victim", "directory", victim)
        self.assertEqual(result.returncode, 5, result.stdout + result.stderr)
        self.assertIn("cleanup root lost its permission seal", result.stderr)

    def test_drvfs_parent_is_really_9p(self) -> None:
        self.assertEqual(publish._fd_mount_fstype(self.parent.fd), "9p")


if __name__ == "__main__":
    unittest.main()
