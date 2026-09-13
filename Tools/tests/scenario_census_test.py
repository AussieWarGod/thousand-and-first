"""Bounded metadata fan-out; real filesystem checks, no native process or save acceptance."""
import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import threading
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
spec = importlib.util.spec_from_file_location("scenario_census", TOOLS / "prepare-scenario-load.py")
load = importlib.util.module_from_spec(spec)
spec.loader.exec_module(load)


class CensusTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="taf-census-test-")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.root = self.base / "tree"
        self.root.mkdir()
        self.paths = [self.root / ("file-" + str(i)) for i in range(8)]
        for path in self.paths:
            path.write_bytes(b"ab")

    def test_four_way_batch_preserves_every_path_check_and_sorted_result(self):
        real = load.file_status
        lock, started = threading.Lock(), threading.Event()
        active = peak = 0
        calls = []

        def observe(path, limit):
            nonlocal active, peak
            with lock:
                active += 1
                peak = max(peak, active)
                calls.append(path)
                if active == 4:
                    started.set()
            try:
                started.wait(2)
                return real(path, limit)
            finally:
                with lock:
                    active -= 1

        with mock.patch.object(load, "file_status", side_effect=observe):
            self.assertEqual(sorted(self.paths), load.tree_files(self.root, 2))
        self.assertEqual(4, peak)
        self.assertEqual(0, active)
        self.assertCountEqual(self.paths, calls)

    def test_file_total_and_directory_bounds_still_refuse(self):
        with self.assertRaises(ValueError):
            load.tree_files(self.root, 1)
        with mock.patch.object(load, "MAX_TREE_BYTES", 15), self.assertRaises(ValueError):
            load.tree_files(self.root, 2)
        with mock.patch.object(load, "MAX_FILES", 7), self.assertRaises(ValueError):
            load.tree_files(self.root, 2)
        for i in range(9):
            (self.root / ("dir-" + str(i))).mkdir()
        with mock.patch.object(load, "MAX_FILES", 8), self.assertRaises(ValueError):
            load.tree_files(self.root, 2)

    def test_refusal_finishes_only_the_current_bounded_batch(self):
        calls = []
        lock = threading.Lock()

        def refuse(path, limit):
            with lock:
                calls.append(path)
            raise ValueError("injected metadata refusal")

        with mock.patch.object(load, "file_status", side_effect=refuse):
            with self.assertRaisesRegex(ValueError, "injected metadata refusal"):
                load.tree_files(self.root, 2)
        self.assertGreater(len(calls), 0)
        self.assertLessEqual(len(calls), 4, "a refusal must not fan out over the remaining tree")
        self.assertEqual(len(calls), len(set(calls)))

    def test_linked_files_and_directories_still_refuse(self):
        link = self.root / "alias"
        os.link(self.paths[0], link)
        try:
            with self.assertRaises(ValueError):
                load.tree_files(self.root, 2)
        finally:
            link.unlink()
        for target in (self.paths[0], self.base):
            link.symlink_to(target)
            try:
                with self.assertRaises(ValueError):
                    load.tree_files(self.root, 2)
            finally:
                link.unlink()


if __name__ == "__main__":
    unittest.main()
