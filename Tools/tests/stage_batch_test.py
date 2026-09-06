"""Execute the real batched stage hasher; no deployment, game, or publisher runs."""

from __future__ import annotations

import errno
import io
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
STAGE = ROOT / "Tools" / "stage.sh"
SOURCE = STAGE.read_text(encoding="utf-8")
FUNCTIONS = SOURCE[SOURCE.index("cmd_manifest() {"):SOURCE.index("identity() {")]
PYTHON = FUNCTIONS.split("python3 -c '\n", 1)[1].rsplit('\n\' "$tree" "$inventory"', 1)[0]


class StageBatchTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="taf-stage-batch-test.")
        self.addCleanup(self.temporary.cleanup)
        self.tree = Path(self.temporary.name)
        self.payloads = {
            "Core/Empty.cs": b"",
            "Core/space name.cs": b"class Spaced {}\r\n",
            "Core/é漢.cs": "// λ snowman ☃\n".encode("utf-8"),
            "RuntimeData/Test.xml": b"<x/>\n",
            "Textures/-binary.bin": bytes(range(256)) * 8193,
            "README.md": b"plain\x00binary\xfftail",
            "manifest.json": b'{"id":"r_ThousandAndFirst"}\n',
        }
        for relative, payload in self.payloads.items():
            path = self.tree / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
        self.paths = sorted(self.payloads, key=lambda path: path.encode("utf-8"))
        self.inventory = ("\n".join(self.paths) + "\n").encode("utf-8")

    def invoke(self, inventory=None, inventory_file=""):
        return subprocess.run(
            ["bash", "-c", "set -euo pipefail\n" + FUNCTIONS
             + '\ntree_manifest_from_inventory "$1" "$2"',
             "stage-batch-test", str(self.tree), str(inventory_file)],
            input=self.inventory if inventory is None else inventory,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10, check=False,
        )

    def golden(self):
        return subprocess.run(
            ["sha256sum", "--", *self.paths], cwd=self.tree,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10, check=True,
        ).stdout

    def test_stdin_is_byte_exact_gnu_for_space_unicode_binary_empty_and_multiblock(self):
        before = {name: (self.tree / name).stat() for name in self.paths}
        result = self.invoke()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(b"", result.stderr)
        self.assertEqual(self.golden(), result.stdout)
        for name in self.paths:
            with self.subTest(name=name):
                path = self.tree / name
                after = path.stat()
                self.assertEqual(self.payloads[name], path.read_bytes())
                for field in ("st_dev", "st_ino", "st_mode", "st_nlink", "st_size",
                              "st_mtime_ns", "st_ctime_ns"):
                    self.assertEqual(getattr(before[name], field), getattr(after, field))

    def test_file_inventory_ignores_stdin_and_matches_gnu(self):
        inventory = self.tree / "inventory.txt"
        inventory.write_bytes(self.inventory)
        result = self.invoke(b"this input must not be read\n", inventory)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(self.golden(), result.stdout)
        self.assertEqual(self.inventory, inventory.read_bytes())

    def test_real_manifest_command_keeps_canonical_inventory_and_bytes(self):
        tools = self.tree / "Tools"
        tools.mkdir()
        shutil.copyfile(STAGE, tools / "stage.sh")
        (tools / "Excluded.cs").write_text("excluded", encoding="utf-8")
        result = subprocess.run(
            ["bash", str(tools / "stage.sh"), "manifest"],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=10, check=False,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(self.golden(), result.stdout)

    def test_empty_stdin_and_file_inventories_remain_empty(self):
        inventory = self.tree / "empty-inventory.txt"
        inventory.write_bytes(b"")
        for supplied in ("", inventory):
            with self.subTest(supplied=str(supplied)):
                result = self.invoke(b"", supplied)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(b"", result.stdout)

    def test_missing_file_fails_loudly_without_a_false_hash_row(self):
        result = self.invoke(b"Core/Empty.cs\nCore/Missing.cs\n")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(b"Missing.cs", result.stderr)
        self.assertEqual(self.golden().splitlines(keepends=True)[0], result.stdout)

    def test_missing_inventory_file_fails_loudly(self):
        result = self.invoke(inventory_file=self.tree / "missing-inventory.txt")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(b"missing-inventory.txt", result.stderr)
        self.assertEqual(b"", result.stdout)

    def test_directory_and_fifo_refuse_without_blocking(self):
        os.mkfifo(self.tree / "pipe")
        for name in (b"Core\n", b"pipe\n"):
            with self.subTest(name=name):
                result = self.invoke(name)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(b"not a regular file", result.stderr)
                self.assertEqual(b"", result.stdout)

    def test_symlink_is_not_followed(self):
        (self.tree / "alias.cs").symlink_to(self.tree / "Core" / "Empty.cs")
        result = self.invoke(b"alias.cs\n")
        self.assertNotEqual(0, result.returncode)
        self.assertIn(b"alias.cs", result.stderr)
        self.assertEqual(b"", result.stdout)

    def test_empty_or_unterminated_records_refuse(self):
        for inventory in (b"\n", b"Core/Empty.cs"):
            with self.subTest(inventory=inventory):
                result = self.invoke(inventory)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(b"path record", result.stderr)
                self.assertEqual(b"", result.stdout)

    def test_each_pass_rehashes_current_bytes_without_cached_copy_digest(self):
        first = self.invoke()
        self.assertEqual(0, first.returncode, first.stderr)
        (self.tree / "Core/Empty.cs").write_bytes(b"changed between independent passes")
        second = self.invoke()
        self.assertEqual(0, second.returncode, second.stderr)
        self.assertEqual(self.golden(), second.stdout)
        self.assertNotEqual(first.stdout, second.stdout)

    def execute_read_fault(self, mutation=None, error=None):
        """Run the exact embedded Python with a deterministic fault at its real os.read seam."""
        target = self.tree / "Core/space name.cs"
        output = io.BytesIO()
        descriptors = []
        original_read = os.read

        def read(descriptor, count):
            if not descriptors:
                descriptors.append(descriptor)
                if error is not None:
                    raise error
                block = original_read(descriptor, count)
                mutation(target)
                return block
            return original_read(descriptor, count)

        with mock.patch.multiple(
            sys, argv=["stage-hash", str(self.tree), ""],
            stdin=SimpleNamespace(buffer=io.BytesIO(b"Core/space name.cs\n")),
            stdout=SimpleNamespace(buffer=output),
        ), mock.patch.object(os, "read", side_effect=read):
            with self.assertRaises(OSError if error else ValueError) as caught:
                exec(compile(PYTHON, str(STAGE) + ":batched-hash", "exec"), {})
        self.assertEqual(b"", output.getvalue())
        self.assertEqual(1, len(descriptors))
        with self.assertRaises(OSError) as closed:
            os.fstat(descriptors[0])
        self.assertEqual(errno.EBADF, closed.exception.errno)
        return caught.exception

    def test_in_place_mutation_during_read_refuses_and_closes_descriptor(self):
        def mutate(path):
            with path.open("ab") as stream:
                stream.write(b"new bytes during hash")
        error = self.execute_read_fault(mutate)
        self.assertIn("file changed while hashing", str(error))

    def test_equal_byte_path_replacement_during_read_refuses(self):
        replacement = self.tree / "replacement.cs"
        replacement.write_bytes(self.payloads["Core/space name.cs"])
        error = self.execute_read_fault(lambda path: os.replace(replacement, path))
        self.assertIn("file changed while hashing", str(error))

    def test_read_error_is_preserved_and_closes_descriptor(self):
        injected = OSError(errno.EIO, "injected read failure")
        self.assertIs(injected, self.execute_read_fault(error=injected))

    def test_hash_passes_and_atomic_publication_guards_remain_in_place(self):
        self.assertEqual(1, FUNCTIONS.count("python3 -c"))
        self.assertNotIn("sha256sum", FUNCTIONS)
        self.assertNotIn("cut -d", FUNCTIONS)
        copy = SOURCE[SOURCE.index("cmd_copy() ("):SOURCE.index("tree_list() {")]
        frozen = copy.index('frozen_manifest="$(tree_manifest_from_inventory')
        private = copy.index('verify_tree_frozen "$parent_real/$sibling_name"', frozen)
        publish = copy.index('python3 "$ATOMIC_TREE" publish', private)
        after = copy.index('verify_tree_frozen "$dest_lex"', publish)
        self.assertLess(frozen, private)
        self.assertLess(private, publish)
        self.assertLess(publish, after)
        self.assertIn('"$frozen_manifest"', copy[after:])
        self.assertIn('acquire_parent_lock "$parent_real"', copy)
        self.assertIn('python3 "$ATOMIC_TREE" materialize', copy)
        self.assertIn('cmp -s "$REPO/$f" "$tree/$f"', SOURCE)


if __name__ == "__main__":
    unittest.main()
