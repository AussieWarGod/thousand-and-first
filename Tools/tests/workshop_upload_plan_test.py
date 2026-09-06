"""Executable temporary-package proofs; no upload, authentication, or release proof."""
from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import socket
import stat
import subprocess
import tempfile
import unittest
from unittest import mock

from Tools import workshop_metadata as metadata
from Tools import workshop_upload_plan as upload
from Tools.tests import workshop_metadata_test as fixtures


def sha(payload):
    return hashlib.sha256(payload).hexdigest()


def snapshot(root):
    result = {}
    for path in root.rglob("*"):
        status = path.lstat()
        value = os.readlink(path) if path.is_symlink() else (
            path.read_bytes() if stat.S_ISREG(status.st_mode) else None)
        result[str(path.relative_to(root))] = (status.st_mode, status.st_nlink, value)
    return result


class WorkshopUploadPlanTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="taf-upload-plan-test.")
        self.addCleanup(temporary.cleanup)
        self.base, self.serial = Path(temporary.name), 0
        self.fixture()

    def fixture(self, mode="test", item=123456789):
        self.serial += 1
        self.package = self.base / ("package" + str(self.serial))
        self.package.mkdir()
        helper = fixtures.WorkshopMetadataTests()
        helper.root = self.package
        manifest = metadata.load_manifest(helper.write_manifest(
            "Found a faction and govern settlements across Qud. Cross-world legacy is opt-in."))
        helper.write_preview()
        self.workshop = metadata.canonical_workshop_data(manifest, item, "0" if mode == "test" else "2")
        (self.package / "workshop.json").write_bytes(metadata.canonical_workshop_bytes(self.workshop))
        (self.package / "Core").mkdir()
        self.carrier = self.package / "Core/Payload.cs"
        self.carrier.write_bytes(b"// synthetic package bytes\n")
        self.receipt = Path(str(self.package) + ".sha256")
        self.mode, self.item = mode, str(item)
        self.seal()

    def seal(self):
        files = sorted((p for p in self.package.rglob("*") if p.is_file()),
                       key=lambda p: p.relative_to(self.package).as_posix().encode("utf-8"))
        self.receipt.write_text("".join(sha(p.read_bytes()) + "  ./" +
            p.relative_to(self.package).as_posix() + "\n" for p in files), encoding="utf-8")

    def plan(self, **changes):
        arguments = dict(package=self.package, receipt=self.receipt, mode=self.mode,
                         expected_item=self.item, expected_version="0.3.0")
        arguments.update(changes)
        return upload.build_plan(**arguments)

    def refuses(self, **changes):
        before = snapshot(self.base)
        with self.assertRaises((upload.ValidationError, OSError, UnicodeError)):
            self.plan(**changes)
        self.assertEqual(before, snapshot(self.base))

    def test_private_plan_binds_actual_closed_bytes_and_calls_existing_metadata_validators(self):
        before = snapshot(self.base)
        with mock.patch.object(metadata, "load_manifest", wraps=metadata.load_manifest) as manifest, \
                mock.patch.object(metadata, "validate_workshop", wraps=metadata.validate_workshop) as workshop, \
                mock.patch.object(metadata, "validate_preview", wraps=metadata.validate_preview) as preview:
            plan = self.plan()
        for helper in (manifest, workshop, preview):
            helper.assert_called_once()
        self.assertEqual(before, snapshot(self.base))
        self.assertEqual("taf-workshop-upload-plan-v1", plan["schema"])
        self.assertTrue(plan["planOnly"])
        self.assertEqual((333640, self.item, "test"), (plan["appId"], plan["targetItem"], plan["mode"]))
        self.assertEqual(("0", 2), (plan["qudVisibility"], plan["steamVisibility"]))
        self.assertEqual((metadata.MOD_ID, "0.3.0"), (plan["manifestId"], plan["version"]))
        self.assertEqual(self.workshop["Title"], plan["title"])
        self.assertEqual(self.workshop["Description"], plan["description"])
        self.assertEqual(list(metadata.TAGS), plan["tags"])
        self.assertEqual(str(self.package), plan["contentPath"])
        self.assertEqual(str(self.package / "preview.png"), plan["previewPath"])
        self.assertEqual(sha(self.receipt.read_bytes()), plan["receiptSHA"])
        for field, name in (("manifestSHA", "manifest.json"), ("workshopSHA", "workshop.json")):
            self.assertEqual(sha((self.package / name).read_bytes()), plan[field])
        expected = [{"path": p.relative_to(self.package).as_posix(), "sha256": sha(p.read_bytes()),
                     "size": p.stat().st_size} for p in self.package.rglob("*") if p.is_file()]
        self.assertEqual(sorted(expected, key=lambda row: row["path"]), plan["files"])
        self.assertEqual(plan, self.plan())

    def test_alpha_only_accepts_known_public_id_and_maps_qud_visibility_to_steam(self):
        self.fixture("alpha", upload.ALPHA_ITEM)
        plan = self.plan()
        self.assertEqual(("2", 0), (plan["qudVisibility"], plan["steamVisibility"]))
        self.assertEqual(str(upload.ALPHA_ITEM), plan["targetItem"])
        self.refuses(mode="test")
        self.refuses(expected_item="123456789")

    def test_canonical_item_and_version_arguments_never_default_or_create(self):
        for value in ("0", "-1", "+1", "01", " 1", "1 ", "1.0", str(1 << 64), "", "١", True):
            with self.subTest(item=value):
                self.refuses(expected_item=value)
        for value in ("0.03.0", "00.3.0", "0.3.00", "0.3", "0.3.0-alpha", "v0.3.0", "0.3.0\n", None):
            with self.subTest(version=value):
                self.refuses(expected_version=value)
        self.refuses(mode="release")
        self.refuses(mode="alpha")
        self.refuses(expected_item=str(upload.ALPHA_ITEM))
        self.refuses(expected_item="123456788")
        self.refuses(expected_version="0.3.1")

    def test_metadata_or_preview_errors_refuse_even_with_matching_package_hashes(self):
        for field, value in (("Title", "Wrong title"), ("Description", "Old listing"),
                             ("Tags", "Script"), ("Visibility", "2"), ("WorkshopId", 0)):
            with self.subTest(field=field):
                self.fixture()
                self.workshop[field] = value
                (self.package / "workshop.json").write_bytes(metadata.canonical_workshop_bytes(self.workshop))
                self.seal()
                self.refuses()
        self.fixture()
        (self.package / "preview.png").write_bytes(b"not a png")
        self.seal()
        self.refuses()
        self.fixture()
        manifest = json.loads((self.package / "manifest.json").read_bytes())
        manifest["version"] = "00.3.0"
        (self.package / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        self.seal()
        self.refuses()

    def test_receipt_hash_mismatch_refuses_before_metadata_validation(self):
        self.carrier.write_bytes(b"changed package bytes\n")
        with mock.patch.object(metadata, "load_manifest", side_effect=AssertionError("must verify hashes first")):
            self.refuses()

    def test_inventory_is_closed_and_all_three_metadata_files_are_required(self):
        for missing in ("manifest.json", "workshop.json", "preview.png", "Core/Payload.cs"):
            with self.subTest(missing=missing):
                self.fixture()
                (self.package / missing).unlink()
                self.refuses()
                if missing != "Core/Payload.cs":
                    self.seal()
                    self.refuses()
        self.fixture()
        (self.package / "unlisted.cs").write_bytes(b"unlisted")
        self.refuses()

    def test_sha256sum_binary_rows_and_plain_relative_names_are_supported(self):
        self.receipt.write_bytes(self.receipt.read_bytes().replace(b"  ./", b" *"))
        self.assertEqual(4, len(self.plan()["files"]))

    def test_receipt_duplicates_escaping_noncanonical_hashes_and_control_paths_refuse(self):
        valid = self.receipt.read_bytes()
        first = valid.splitlines()[0] + b"\n"
        for payload in (valid + first, valid + first.replace(b"./", b""), valid[:-1], valid + b"\n",
                        valid.replace(b"\n", b"\r\n"), valid.upper(), b"\\" + valid, b"\xff\n"):
            with self.subTest(payload=payload[:80]):
                self.receipt.write_bytes(payload)
                self.refuses()
        for name in ("../outside", "./../outside", "/tmp/outside", "C:/outside", "a\\b", "a//b",
                     "././a", "a/./b", "a/../b", "a\tb", "a\x00b", "a\x7fb", "a\u202eb"):
            with self.subTest(name=name):
                self.receipt.write_bytes(valid + ("0" * 64 + "  " + name + "\n").encode("utf-8"))
                self.refuses()

    def test_casefold_collisions_include_directory_components_and_unlisted_control_names(self):
        self.carrier.with_name("payload.CS").write_bytes(b"collision")
        self.seal()
        self.refuses()
        self.fixture()
        (self.package / "core").mkdir()
        (self.package / "core/other.cs").write_bytes(b"different leaf, colliding directory")
        self.seal()
        self.refuses()
        self.fixture()
        (self.package / "bad\nname").write_bytes(b"control")
        self.refuses()

    def test_windows_unsafe_components_refuse_in_both_receipt_and_actual_inventory(self):
        devices = ("CON", "prn", "Aux", "nul", "CONIN$", "conout$") + tuple(
            prefix + digit for prefix in ("cOm", "LpT") for digit in "123456789¹²³")
        names = tuple(name + suffix for name in devices for suffix in ("", ".cs")) + (
            "payload.", "payload ", "dir./payload.cs", "dir /payload.cs", "cOn/payload.cs",
            "Lpt²/payload.cs", "NUL .txt") + tuple("a" + char + "b.cs" for char in '<>"|?*')
        for name in names:
            for listed in (False, True):
                with self.subTest(name=name, listed=listed):
                    self.fixture()
                    target = self.package / name
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(b"real bytes under a Windows-unsafe name")
                    if listed:
                        self.seal()
                    before = snapshot(self.base)
                    with self.assertRaisesRegex(upload.ValidationError, "Windows-unsafe"):
                        self.plan()
                    self.assertEqual(before, snapshot(self.base))

    def test_windows_safe_near_matches_and_interior_spaces_remain_exact_names(self):
        names = ("CONSOLE.cs", "_CON.cs", "COM0.cs", "COM10.cs", "LPT0.cs", "LPT10.cs",
                 "COM⁴.cs", "normal file.cs", "mixed.name.cs")
        for name in names:
            (self.package / name).write_bytes(name.encode("utf-8"))
        self.seal()
        rows = {row["path"]: row["sha256"] for row in self.plan()["files"]}
        for name in names:
            self.assertEqual(sha(name.encode("utf-8")), rows[name])

    def test_windows_unsafe_package_and_receipt_path_components_also_refuse(self):
        for field, name in (("package", "unsafe."), ("package", "AUX"), ("receipt", "CON.txt")):
            with self.subTest(field=field, name=name):
                self.fixture()
                source = self.package if field == "package" else self.receipt
                target = self.base / name
                source.rename(target)
                with self.assertRaisesRegex(upload.ValidationError, "Windows-unsafe"):
                    self.plan(**{field: target})

    def test_symlinks_hardlinks_and_nonregular_entries_are_never_followed(self):
        for kind in ("file-link", "dir-link", "file-hardlink", "fifo", "receipt-link", "receipt-hardlink"):
            with self.subTest(kind=kind):
                self.fixture()
                target = self.package / "foreign"
                if kind == "file-link":
                    target.symlink_to(self.receipt)
                elif kind == "dir-link":
                    target.symlink_to(self.base, target_is_directory=True)
                elif kind == "file-hardlink":
                    os.link(self.carrier, target)
                elif kind == "fifo":
                    os.mkfifo(target)
                else:
                    original = self.receipt.with_suffix(".original")
                    self.receipt.rename(original)
                    if kind == "receipt-link":
                        self.receipt.symlink_to(original)
                    else:
                        os.link(original, self.receipt)
                self.refuses()

    def test_linked_package_and_receipt_parents_and_broad_roots_are_rejected(self):
        linked = self.base / "linked"
        linked.symlink_to(self.package, target_is_directory=True)
        self.refuses(package=linked)
        parent_link = self.base / "parent-link"
        parent_link.symlink_to(self.base, target_is_directory=True)
        self.refuses(package=parent_link / self.package.name)
        self.refuses(receipt=parent_link / self.receipt.name)
        for path in ("/", "/tmp", str(Path.home()), str(Path(upload.__file__).parents[1]), "relative"):
            with self.subTest(path=path):
                self.refuses(package=path)
                self.refuses(receipt=path)
        self.refuses(package=str(self.package) + "/../" + self.package.name)
        self.refuses(receipt=self.package / "inside.sha256")

    def test_required_metadata_links_refuse_even_when_target_bytes_match_receipt(self):
        for name in upload.REQUIRED:
            with self.subTest(name=name):
                self.fixture()
                path = self.package / name
                retained = self.base / (str(self.serial) + "-" + name)
                path.rename(retained)
                path.symlink_to(retained)
                self.refuses()

    def test_actual_hashing_enforces_exact_total_receipt_and_file_count_boundaries(self):
        total = sum(p.stat().st_size for p in self.package.rglob("*") if p.is_file())
        receipt_size = self.receipt.stat().st_size
        self.assertEqual((10000, 512 * 1024 * 1024, 1024 * 1024),
                         (upload.MAX_FILES, upload.MAX_TOTAL_BYTES, upload.MAX_RECEIPT_BYTES))
        for constant, boundary in (("MAX_TOTAL_BYTES", total), ("MAX_RECEIPT_BYTES", receipt_size), ("MAX_FILES", 4)):
            with self.subTest(constant=constant), mock.patch.object(upload, constant, boundary):
                self.assertEqual(4, len(self.plan()["files"]))
            with self.subTest(over=constant), mock.patch.object(upload, constant, boundary - 1):
                self.refuses()
        with mock.patch.object(upload, "MAX_FILES", 4):
            (self.package / "unlisted-fifth").write_bytes(b"fifth")
            self.refuses()
        self.fixture()
        self.receipt.write_bytes(b"x" * (upload.MAX_RECEIPT_BYTES + 1))
        self.refuses()

    def test_real_input_changes_during_metadata_validation_refuse(self):
        for change in ("bytes", "mode", "replace", "new-file", "receipt", "receipt-replace", "package-replace"):
            with self.subTest(change=change):
                self.fixture()
                validate = metadata.validate_preview

                def mutate(path):
                    validate(path)
                    if change == "bytes":
                        old = self.carrier.stat()
                        self.carrier.write_bytes(b"x" * old.st_size)
                        os.utime(self.carrier, ns=(old.st_atime_ns, old.st_mtime_ns))
                    elif change == "mode":
                        self.carrier.chmod(self.carrier.stat().st_mode ^ stat.S_IXUSR)
                    elif change == "new-file":
                        (self.package / "late.cs").write_bytes(b"late")
                    elif change == "receipt":
                        self.receipt.write_bytes(b"".join(reversed(self.receipt.read_bytes().splitlines(keepends=True))))
                    elif change == "package-replace":
                        retained = self.package.with_name(self.package.name + "-retained")
                        self.package.rename(retained)
                        shutil.copytree(retained, self.package)
                    else:
                        target = self.receipt if change == "receipt-replace" else self.carrier
                        replacement = self.base / ("replacement" + str(self.serial))
                        replacement.write_bytes(target.read_bytes())
                        replacement.replace(target)

                with mock.patch.object(metadata, "validate_preview", side_effect=mutate):
                    with self.assertRaises((upload.ValidationError, OSError)):
                        self.plan()

    def test_actual_oversized_sparse_file_refuses_before_reading_or_allocating_its_payload(self):
        with self.carrier.open("wb") as stream:
            stream.truncate(upload.MAX_TOTAL_BYTES + 1)
        before = self.carrier.stat()
        with self.assertRaisesRegex(upload.ValidationError, "byte bound"):
            self.plan()
        self.assertEqual(before, self.carrier.stat())

    def test_changed_file_during_hash_read_is_detected_by_post_stat(self):
        read = os.read
        inode, mutated = self.carrier.stat().st_ino, []

        def changing_read(fd, count):
            result = read(fd, count)
            if result and not mutated and os.fstat(fd).st_ino == inode:
                mutated.append(True)
                self.carrier.write_bytes(b"x" * len(result))
            return result

        with mock.patch.object(upload.os, "read", side_effect=changing_read):
            with self.assertRaisesRegex(upload.ValidationError, "changed"):
                self.plan()
        self.assertEqual([True], mutated)

    def test_final_stat_detects_change_after_second_package_hash_sweep(self):
        read = upload._read
        inode, calls = self.receipt.stat().st_ino, []

        def after_read(fd, limit, retain=False):
            result = read(fd, limit, retain)
            if os.fstat(fd).st_ino == inode:
                calls.append(True)
                if len(calls) == 2:
                    self.carrier.write_bytes(b"changed after the second package hash")
            return result

        with mock.patch.object(upload, "_read", side_effect=after_read):
            with self.assertRaisesRegex(upload.ValidationError, "changed after hashing"):
                self.plan()
        self.assertEqual(2, len(calls))

    def test_cli_emits_only_json_on_success_and_no_partial_plan_on_refusal(self):
        arguments = ["--package", str(self.package), "--receipt", str(self.receipt), "--mode", "test",
                     "--expected-item", self.item, "--expected-version", "0.3.0"]
        before = snapshot(self.base)
        output, errors = io.StringIO(), io.StringIO()
        with mock.patch.object(subprocess, "run", side_effect=AssertionError("no process")), \
                mock.patch.object(socket, "socket", side_effect=AssertionError("no network")), \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            self.assertEqual(0, upload.main(arguments))
        self.assertEqual(self.plan(), json.loads(output.getvalue()))
        self.assertEqual("", errors.getvalue())
        self.assertEqual(before, snapshot(self.base))
        output, errors = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            self.assertEqual(2, upload.main(arguments[:-1] + ["0.3.1"]))
        self.assertEqual("", output.getvalue())
        self.assertIn("refused", errors.getvalue())
        self.assertEqual(before, snapshot(self.base))


if __name__ == "__main__":
    unittest.main()
