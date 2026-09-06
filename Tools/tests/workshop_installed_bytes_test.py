"""Temporary synthetic standard folders prove bytes only, never Steam delivery."""
from __future__ import annotations

import builtins
import contextlib
import io
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import unittest
from unittest import mock

from Tools import workshop_installed_bytes as installed
from Tools import workshop_upload_plan as upload
from Tools.tests import workshop_upload_plan_test as fixtures


class WorkshopInstalledBytesTests(unittest.TestCase):
    def setUp(self):
        self.source = fixtures.WorkshopUploadPlanTests()
        self.source.setUp()
        self.addCleanup(self.source.doCleanups)
        self.relocate()

    def relocate(self):
        root = self.source.base / ("Library " + str(self.source.serial)) / "steamapps/workshop/content/333640" / self.source.item
        root.parent.mkdir(parents=True)
        self.source.package.rename(root)
        self.source.package, self.source.carrier = root, root / "Core/Payload.cs"
        self.approval = fixtures.sha(self.source.receipt.read_bytes())

    def fresh(self, mode="test", item=123456789):
        self.source.fixture(mode, item)
        self.relocate()

    def verify(self, **changes):
        arguments = dict(installed_root=self.source.package, receipt=self.source.receipt,
                         receipt_sha=self.approval, mode=self.source.mode,
                         expected_item=self.source.item, expected_version="0.3.0")
        arguments.update(changes)
        return installed.verify_installed_bytes(**arguments)

    def refuses(self, **changes):
        before = fixtures.snapshot(self.source.base)
        with self.assertRaises((upload.ValidationError, OSError, UnicodeError)):
            self.verify(**changes)
        self.assertEqual(before, fixtures.snapshot(self.source.base))

    def test_exact_bytes_reuse_real_planner_and_make_only_truthful_claims(self):
        before = fixtures.snapshot(self.source.base)
        with mock.patch.object(upload, "build_plan", wraps=upload.build_plan) as planner:
            result = self.verify()
        planner.assert_called_once_with(self.source.package, self.source.receipt, "test", self.source.item, "0.3.0")
        self.assertEqual({"schema": "taf-workshop-installed-bytes-v1", "status": "installed_bytes_match",
                          "item": self.source.item, "version": "0.3.0", "receiptSHA": self.approval,
                          "installedRoot": str(self.source.package), "files": 4,
                          "bytes": sum(p.stat().st_size for p in self.source.package.rglob("*") if p.is_file()),
                          "subscriptionVerified": False, "freshTransferVerified": False, "delivered": False}, result)
        self.assertEqual(before, fixtures.snapshot(self.source.base))
        self.assertEqual(result, self.verify(), "same cached version never becomes fresh delivery")

    def test_alpha_lane_uses_same_closed_bytes_checker(self):
        self.fresh("alpha", upload.ALPHA_ITEM)
        self.assertEqual(str(upload.ALPHA_ITEM), self.verify()["item"])
        self.refuses(mode="test")

    def test_receipt_approval_is_canonical_and_checked_before_planner(self):
        for value in (None, True, "", "0" * 63, "0" * 65, self.approval.upper(),
                      "g" * 64, self.approval + "\n", " " + self.approval, "0" * 64):
            with self.subTest(value=value), mock.patch.object(upload, "build_plan", side_effect=AssertionError("unapproved plan")):
                self.refuses(receipt_sha=value)

    def test_receipt_hash_changes_refuse_even_when_receipt_rows_remain_valid(self):
        self.source.receipt.write_bytes(b"".join(reversed(self.source.receipt.read_bytes().splitlines(keepends=True))))
        self.refuses()

    def test_approved_receipt_still_needs_valid_closed_rows_and_matching_file_hashes(self):
        valid = self.source.receipt.read_bytes()
        first = valid.splitlines(keepends=True)[0]
        for payload in (b"0" * 64 + valid[64:], valid[len(first):], valid + first,
                        valid.replace(b"./Core/", b"../Core/"), valid[:-1]):
            with self.subTest(payload=payload[:70]):
                self.source.receipt.write_bytes(payload)
                self.approval = fixtures.sha(payload)
                self.refuses()

    def test_data_mismatch_refuses_before_metadata_validation(self):
        self.source.carrier.write_bytes(b"different installed bytes\n")
        with mock.patch.object(upload.metadata, "load_manifest", side_effect=AssertionError("hashes first")):
            self.refuses()

    def test_missing_files_and_extra_files_including_copied_receipts_refuse(self):
        for name in ("manifest.json", "workshop.json", "preview.png", "Core/Payload.cs"):
            with self.subTest(missing=name):
                self.fresh()
                (self.source.package / name).unlink()
                self.refuses()
        for name in ("extra.cs", "package.sha256"):
            with self.subTest(extra=name):
                self.fresh()
                (self.source.package / name).write_bytes(self.source.receipt.read_bytes())
                self.refuses()

    def test_receipt_inside_installed_root_is_not_external_authority(self):
        receipt = self.source.package / "installed.sha256"
        receipt.write_bytes(self.source.receipt.read_bytes())
        self.refuses(receipt=receipt)

    def test_exact_path_tail_app_and_item_are_required_even_for_identical_bytes(self):
        for tail in ("steamapps/workshop/content/333641/123456789", "steamapps/workshop/content/333640/123456788",
                     "SteamApps/workshop/content/333640/123456789", "steamapps/workshop/Content/333640/123456789",
                     "workshop/content/333640/123456789", "steamapps/workshop/content/333640/123456789/nested"):
            with self.subTest(tail=tail):
                target = self.source.base / ("wrong" + str(len(tuple(self.source.base.iterdir())))) / tail
                shutil.copytree(self.source.package, target)
                self.refuses(installed_root=target)
        for path in ("relative", "/", "/tmp", str(self.source.package) + "/",
                     str(self.source.package) + "/../" + self.source.item):
            with self.subTest(path=path):
                self.refuses(installed_root=path)

    def test_item_version_and_lane_never_default_or_create(self):
        for item in (None, True, "", "0", "01", "+1", " 1", "١", str(1 << 64), str(upload.ALPHA_ITEM)):
            with self.subTest(item=item):
                self.refuses(expected_item=item)
        self.refuses(mode="alpha")
        self.refuses(mode="other")
        for version in ("0.3.1", "0.03.0", "0.3", "0.3.0-alpha", None):
            with self.subTest(version=version):
                self.refuses(expected_version=version)
        self.fresh("test", 1 << 64)
        self.refuses()

    def test_content_metadata_must_match_expected_item_even_under_correct_folder(self):
        self.source.workshop["WorkshopId"] = 987654321
        (self.source.package / "workshop.json").write_bytes(upload.metadata.canonical_workshop_bytes(self.source.workshop))
        self.source.seal()
        self.approval = fixtures.sha(self.source.receipt.read_bytes())
        self.refuses()

    def test_root_and_library_symlinks_refuse(self):
        original = self.source.package.with_name("retained")
        self.source.package.rename(original)
        self.source.package.symlink_to(original, target_is_directory=True)
        self.refuses()
        self.fresh()
        linked = self.source.base / "linked-library"
        linked.symlink_to(self.source.package.parents[4], target_is_directory=True)
        self.refuses(installed_root=linked / "steamapps/workshop/content/333640" / self.source.item)

    def test_content_links_hardlinks_and_special_files_refuse(self):
        for kind in ("symlink", "directory-link", "hardlink", "fifo"):
            with self.subTest(kind=kind):
                self.fresh()
                target = self.source.package / "unexpected"
                if kind == "symlink":
                    target.symlink_to(self.source.carrier)
                elif kind == "directory-link":
                    target.symlink_to(self.source.base, target_is_directory=True)
                elif kind == "hardlink":
                    os.link(self.source.carrier, self.source.base / ("outside" + str(self.source.serial)))
                else:
                    os.mkfifo(target)
                self.refuses()

    def test_receipt_links_hardlinks_and_linked_parents_refuse(self):
        for kind in ("symlink", "hardlink", "parent-link"):
            with self.subTest(kind=kind):
                self.fresh()
                target = self.source.receipt.with_suffix(".retained")
                self.source.receipt.rename(target)
                if kind == "symlink":
                    self.source.receipt.symlink_to(target)
                elif kind == "hardlink":
                    os.link(target, self.source.receipt)
                else:
                    parent = self.source.base / ("linked" + str(self.source.serial))
                    parent.symlink_to(self.source.base, target_is_directory=True)
                    self.source.receipt = parent / target.name
                self.refuses()

    def test_approved_hash_is_rechecked_against_actual_returned_plan(self):
        build = upload.build_plan

        def changed_receipt(*args, **kwargs):
            self.source.receipt.write_bytes(b"".join(reversed(self.source.receipt.read_bytes().splitlines(keepends=True))))
            return build(*args, **kwargs)

        with mock.patch.object(upload, "build_plan", side_effect=changed_receipt):
            with self.assertRaisesRegex(upload.ValidationError, "SHA mismatch in verified plan"):
                self.verify()

    def test_receipt_mutation_or_same_byte_replacement_after_plan_refuses(self):
        build = upload.build_plan
        for kind in ("mutate", "replace"):
            with self.subTest(kind=kind):
                self.fresh()

                def after_plan(*args, **kwargs):
                    result = build(*args, **kwargs)
                    if kind == "mutate":
                        self.source.receipt.write_bytes(b"changed after planner")
                    else:
                        replacement = self.source.base / ("replacement" + str(self.source.serial))
                        replacement.write_bytes(self.source.receipt.read_bytes())
                        replacement.replace(self.source.receipt)
                    return result

                with mock.patch.object(upload, "build_plan", side_effect=after_plan):
                    with self.assertRaises(upload.ValidationError):
                        self.verify()

    def test_no_network_process_state_writes_or_acf_reads(self):
        acf = self.source.package.parents[2] / "appworkshop_333640.acf"
        acf.write_bytes(b"synthetic state must not be read or edited")
        before = fixtures.snapshot(self.source.base)
        descriptor_open, file_open, path_open = os.open, builtins.open, io.open

        def read_descriptor(name, flags, *args, **kwargs):
            self.assertFalse(flags & (os.O_WRONLY | os.O_RDWR | os.O_CREAT | os.O_TRUNC | os.O_APPEND))
            self.assertFalse(str(name).endswith(".acf"))
            return descriptor_open(name, flags, *args, **kwargs)

        def read_file(real):
            def read(name, mode="r", *args, **kwargs):
                self.assertFalse(any(flag in mode for flag in "wax+"))
                self.assertFalse(str(name).endswith(".acf"))
                return real(name, mode, *args, **kwargs)
            return read

        with contextlib.ExitStack() as stack:
            stack.enter_context(mock.patch.object(os, "open", side_effect=read_descriptor))
            stack.enter_context(mock.patch.object(builtins, "open", side_effect=read_file(file_open)))
            stack.enter_context(mock.patch.object(io, "open", side_effect=read_file(path_open)))
            for owner, name in ((socket, "socket"), (subprocess, "Popen"), (os, "write"),
                                (os, "unlink"), (os, "rename"), (os, "replace"), (os, "mkdir")):
                stack.enter_context(mock.patch.object(owner, name, side_effect=AssertionError("no external action")))
            self.assertEqual("installed_bytes_match", self.verify()["status"])
        self.assertEqual(before, fixtures.snapshot(self.source.base))

    def test_cli_json_success_and_no_success_output_on_refusal(self):
        args = ["--installed-root", str(self.source.package), "--receipt", str(self.source.receipt),
                "--receipt-sha", self.approval, "--mode", "test", "--expected-item", self.source.item,
                "--expected-version", "0.3.0"]
        output, errors = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            self.assertEqual(0, installed.main(args))
        self.assertEqual(self.verify(), json.loads(output.getvalue()))
        self.assertEqual("", errors.getvalue())
        output, errors = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            self.assertEqual(2, installed.main(args[:-1] + ["0.3.1"]))
        self.assertEqual("", output.getvalue())
        self.assertIn("refused", errors.getvalue())


if __name__ == "__main__":
    unittest.main()
