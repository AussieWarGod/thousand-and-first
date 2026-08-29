"""Negative controls for the developer-harness compile route.

Each test breaks one property and requires the guard to fire. A route audit that stays green when
the route is removed is describing a helper nothing runs, which is exactly how the previous version
passed while `Tools/gate.sh` could have dropped all three dev call sites.

Static only: nothing here invokes a compiler, the gate, or a game.
"""

from __future__ import annotations

import importlib.util
import os
import pathlib
import shutil
import tempfile
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]


def load(name: str, relative: str):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


CHECKER = load("harness_registration", "Tools/check-harness-registration.py")
INVENTORY = load("dev_harness_inventory", "Tools/dev-harness-inventory.py")
GATE = (ROOT / "Tools" / "gate.sh").read_text(encoding="utf-8")


class RouteWiringTest(unittest.TestCase):
    """Defect 3: the clean verdict must bind to the gate, not to an unused helper."""

    def test_the_real_gate_is_wired(self):
        problems: list[str] = []
        CHECKER.assert_route_wiring(problems)
        self.assertEqual([], problems)

    def test_removing_any_dev_call_site_fails_the_audit(self):
        for needle, _why in CHECKER.ROUTE_REQUIREMENTS:
            with self.subTest(call=needle.splitlines()[0]):
                problems: list[str] = []
                CHECKER.assert_route_wiring(problems, GATE.replace(needle, "", 1))
                self.assertTrue(problems, "removing %r left the audit green" % needle)

    def test_a_derived_dev_tree_fails_the_audit(self):
        problems: list[str] = []
        CHECKER.assert_route_wiring(
            problems,
            GATE.replace(
                'DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"', 'DEV="$STAGE.dev"', 1
            ),
        )
        self.assertTrue(any("derived sibling" in row for row in problems), problems)

    def test_a_removal_outside_the_trap_fails_the_audit(self):
        problems: list[str] = []
        CHECKER.assert_route_wiring(
            problems,
            GATE.replace(
                "prepare_dev_harness() {", 'prepare_dev_harness() {\n\trm -rf "$DEV"', 1
            ),
        )
        self.assertTrue(
            any("outside the single cleanup trap" in row for row in problems), problems
        )


class ModeInventoryTest(unittest.TestCase):
    """Defect 2: dev baseline must be ordinary baseline plus Harness, not plus everything."""

    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-route-test."))
        self.stage = self.tmp / "stage"
        (self.stage / "Core").mkdir(parents=True)
        (self.stage / "Integrations" / "Hearthpyre223").mkdir(parents=True)
        (self.stage / "Core" / "A.cs").write_text("class A {}", encoding="utf-8")
        (self.stage / "Integrations" / "Hearthpyre223" / "Bridge.cs").write_text(
            "class Bridge {}", encoding="utf-8"
        )

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def relative(self, paths):
        return sorted(p.relative_to(self.stage).as_posix() for p in paths)

    def test_baseline_excludes_the_optional_mod_bridge(self):
        self.assertEqual(
            ["Core/A.cs"],
            self.relative(INVENTORY.ordinary_sources(self.stage, "baseline")),
        )

    def test_compatibility_includes_the_optional_mod_bridge(self):
        self.assertEqual(
            ["Core/A.cs", "Integrations/Hearthpyre223/Bridge.cs"],
            self.relative(INVENTORY.ordinary_sources(self.stage, "compatibility")),
        )

    def test_the_overlay_is_never_part_of_an_ordinary_inventory(self):
        (self.stage / "Harness").mkdir()
        (self.stage / "Harness" / "H.cs").write_text("class H {}", encoding="utf-8")
        for mode in ("baseline", "compatibility"):
            with self.subTest(mode=mode):
                rows = self.relative(INVENTORY.ordinary_sources(self.stage, mode))
                self.assertNotIn("Harness/H.cs", rows)

    def test_an_unknown_mode_refuses(self):
        with self.assertRaises(SystemExit):
            INVENTORY.ordinary_sources(self.stage, "whatever")

    def test_an_ordinary_inventory_refuses_a_tree_carrying_the_overlay(self):
        (self.stage / "Harness").mkdir()
        with self.assertRaises(SystemExit):
            INVENTORY.emit(str(self.stage), str(self.tmp / "out"), "baseline", False)


class EmitHardeningTest(unittest.TestCase):
    """Defect 4: the compile authority validates the STAGED bytes, not just the repo source."""

    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-emit-test."))
        self.stage = self.tmp / "stage"
        (self.stage / "Core").mkdir(parents=True)
        (self.stage / "Core" / "A.cs").write_text("class A {}", encoding="utf-8")

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def sources(self):
        return INVENTORY.ordinary_sources(self.stage, "baseline")

    def test_a_clean_tree_validates(self):
        rows = INVENTORY.validated(self.sources(), self.stage, "test inventory")
        self.assertEqual(1, len(rows))

    def test_a_symlinked_source_is_refused(self):
        target = self.tmp / "outside.cs"
        target.write_text("class Outside {}", encoding="utf-8")
        try:
            os.symlink(target, self.stage / "Core" / "Linked.cs")
        except (OSError, NotImplementedError):
            self.skipTest("symlinks unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.validated(self.sources(), self.stage, "test inventory")
        self.assertIn("link", str(caught.exception))

    def test_a_hard_linked_source_is_refused(self):
        try:
            os.link(self.stage / "Core" / "A.cs", self.stage / "Core" / "B.cs")
        except (OSError, NotImplementedError, AttributeError):
            self.skipTest("hard links unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.validated(self.sources(), self.stage, "test inventory")
        self.assertIn("names", str(caught.exception))

    def test_a_case_fold_collision_is_refused(self):
        (self.stage / "Core" / "a.cs").write_text("class A2 {}", encoding="utf-8")
        if (self.stage / "Core" / "A.cs").read_text(encoding="utf-8") == "class A2 {}":
            self.skipTest("case-insensitive filesystem")
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.validated(self.sources(), self.stage, "test inventory")
        self.assertIn("normalize to one name", str(caught.exception))

    def test_a_linked_directory_is_refused(self):
        other = self.tmp / "elsewhere"
        other.mkdir()
        (other / "X.cs").write_text("class X {}", encoding="utf-8")
        try:
            os.symlink(other, self.stage / "Linked", target_is_directory=True)
        except (OSError, NotImplementedError):
            self.skipTest("symlinks unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.ordinary_sources(self.stage, "baseline")
        self.assertIn("linked directory", str(caught.exception))


class DevOverlayTest(unittest.TestCase):
    """All and only Harness: a missing shard and an extra one both refuse."""

    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-overlay-test."))
        self.stage = self.tmp / "stage"
        (self.stage / "Harness").mkdir(parents=True)
        (self.stage / "Core").mkdir(parents=True)
        (self.stage / "Core" / "A.cs").write_text("class A {}", encoding="utf-8")
        for name in INVENTORY.harness_shards():
            shutil.copy(ROOT / "Harness" / name, self.stage / "Harness" / name)

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_a_complete_overlay_is_accepted(self):
        self.assertEqual(
            len(INVENTORY.harness_shards()),
            len(INVENTORY.overlay_sources(self.stage)),
        )

    def test_a_missing_shard_refuses(self):
        victim = INVENTORY.harness_shards()[0]
        (self.stage / "Harness" / victim).unlink()
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.overlay_sources(self.stage)
        self.assertIn("missing harness shards", str(caught.exception))

    def test_an_extra_shard_refuses(self):
        (self.stage / "Harness" / "Injected.cs").write_text(
            "class I {}", encoding="utf-8"
        )
        with self.assertRaises(SystemExit) as caught:
            INVENTORY.overlay_sources(self.stage)
        self.assertIn("unexpected harness shards", str(caught.exception))

    def test_the_dev_list_is_the_ordinary_list_plus_all_and_only_harness(self):
        out = self.tmp / "dev.list"
        INVENTORY.emit(str(self.stage), str(out), "baseline", True)
        rows = [row for row in out.read_text(encoding="utf-8").splitlines() if row]
        relative = sorted(
            pathlib.Path(row).relative_to(self.stage).as_posix() for row in rows
        )
        expected = sorted(
            ["Core/A.cs"] + ["Harness/" + n for n in INVENTORY.harness_shards()]
        )
        self.assertEqual(expected, relative)


if __name__ == "__main__":
    unittest.main()
