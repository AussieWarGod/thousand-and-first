"""Negative controls for the developer-harness compile route.

Each test breaks one property and requires the guard to fire. A route audit that stays green when
the route is removed is describing a helper nothing runs, which is exactly how the previous version
passed while `Tools/gate.sh` could have dropped all three dev call sites.

Static only: nothing here invokes a compiler, the gate, or a game.
"""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import sys
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

# The repository-unchanged guard lives in Tools/tests/conftest.py as a pytest SESSION fixture.
# unittest discover - the runner this project's own Evidence blocks use - never loads conftest.py,
# so the guard is ALSO installed here for that runner.
#
# Scope, stated exactly rather than implied: under pytest the session fixture brackets ALL modules.
# Under unittest discover these hooks bracket only THIS module - 1 of the 10 modules in
# Tools/tests - so the other 9 run unguarded there. The pytest run is the one that covers
# everything; this is the belt on the runner that would otherwise have none.
_GUARD_BEFORE: dict = {}


def setUpModule():
    import conftest_guard
    _GUARD_BEFORE.update(conftest_guard.sweep())


def tearDownModule():
    import conftest_guard
    conftest_guard.assert_unchanged(_GUARD_BEFORE)


class RouteWiringTest(unittest.TestCase):
    """Defect: the audit bound to gate TEXT, so seven in-place neutralisations all stayed green.

    Each case below is one of those seven, applied to the real gate. A substring audit survived
    every one of them because the strings it searched for were still in the file; position,
    nesting, uniqueness, and the function bodies are what the edits actually change.
    """

    def audit(self, gate):
        problems: list[str] = []
        CHECKER.assert_route_wiring(problems, gate)
        return problems

    def test_the_real_gate_is_wired(self):
        self.assertEqual([], self.audit(GATE))

    # ----- the seven neutralisations ---------------------------------------------------------

    def test_neutralisation_1_dev_block_wrapped_in_if_false(self):
        gate = GATE.replace(
            "prepare_dev_harness\ncompile_dev_harness baseline",
            "if false; then\nprepare_dev_harness\ncompile_dev_harness baseline",
            1,
        ).replace(
            "compile_dev_harness compatibility && dev_compatibility_rc=0 || failed=1",
            "compile_dev_harness compatibility && dev_compatibility_rc=0 || failed=1\nfi",
            1,
        )
        self.assertTrue(self.audit(gate), "an if-false wrapper left the audit green")

    def test_neutralisation_2_early_exit_zero(self):
        gate = GATE.replace("failed=0\n", "exit 0\nfailed=0\n", 1)
        self.assertTrue(self.audit(gate), "an early exit 0 left the audit green")

    def test_neutralisation_3_prepare_stubbed_to_return_zero(self):
        start = GATE.index("prepare_dev_harness() {")
        end = GATE.index("\n}", start) + 2
        gate = GATE[:start] + "prepare_dev_harness() {\n\treturn 0\n}" + GATE[end:]
        self.assertTrue(self.audit(gate), "a stubbed prepare left the audit green")

    def test_neutralisation_4_compile_status_forced_to_zero(self):
        gate = GATE.replace('\treturn "$rc"\n}', "\treturn 0\n}")
        self.assertTrue(self.audit(gate), "a forced rc left the audit green")

    def test_neutralisation_5_compiler_never_invoked(self):
        gate = GATE.replace("csc.dll", "true.dll")
        self.assertTrue(self.audit(gate), "removing the compiler left the audit green")

    def test_neutralisation_6_gate_always_exits_zero(self):
        gate = GATE.replace('exit "$failed"', "exit 0")
        self.assertTrue(self.audit(gate), "a forced exit 0 left the audit green")

    def test_neutralisation_7_cleanup_trap_removed(self):
        gate = GATE.replace("trap cleanup EXIT", "trap - EXIT")
        self.assertTrue(self.audit(gate), "removing the trap left the audit green")

    # ----- the earlier controls, kept ---------------------------------------------------------

    def test_removing_any_driver_call_site_fails_the_audit(self):
        for needle in CHECKER.DRIVER_SEQUENCE:
            with self.subTest(call=needle):
                self.assertTrue(
                    self.audit(GATE.replace(needle, "", 1)),
                    "removing %r left the audit green" % needle,
                )

    def test_a_derived_dev_tree_fails_the_audit(self):
        problems = self.audit(
            GATE.replace(
                'DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"', 'DEV="$STAGE.dev"', 1
            )
        )
        self.assertTrue(
            any(
                "derived sibling" in row or "independently allocated" in row
                for row in problems
            ),
            problems,
        )

    def test_a_removal_outside_the_allowlisted_cleanup_fails(self):
        problems = self.audit(
            GATE.replace(
                "prepare_dev_harness() {", 'prepare_dev_harness() {\n\trm -rf "$DEV"', 1
            )
        )
        self.assertTrue(
            any("outside the allowlisted cleanup body" in row for row in problems), problems
        )

    def test_the_named_bypass_that_used_to_audit_green_now_fires(self):
        problems = self.audit(
            GATE.replace(CHECKER.CLEANUP_ONE_LINE, 'cleanup() { :; }; rm -rf "$OTHER"')
        )
        self.assertTrue(problems, "the amendment's own named bypass still audits green")

    # A LAWFUL line inside a real function body. Injecting after it puts the hostile statement
    # where only the rm tokeniser can see it - it trips none of the other checks, so each control
    # below pins the tokeniser and nothing else.
    LAWFUL_ANCHOR = (
        '\tpython3 "$REPO/Tools/scenario_profile.py" manifest '
        '"$REPO/manifest.json" "$DEV/manifest.json"\n'
    )

    @staticmethod
    def _rm_spellings():
        return {
            "rm -fr": 'rm -fr "$REPO"',
            "rm -r -f": 'rm -r -f "$REPO"',
            "rm -Rf": 'rm -Rf "$REPO"',
            "rm -rvf": 'rm -rvf "$REPO"',
            "rm --recursive --force": 'rm --recursive --force "$REPO"',
            "rm double space": 'rm  -rf "$REPO"',
            "absolute /bin/rm": '/bin/rm -rf "$REPO"',
            "env-prefixed rm": 'FOO=1 rm -rf "$REPO"',
            "function-def indirection": 'cleanup2() { rm -fr "$REPO"; }; cleanup2',
            "subshell": '( rm -rf "$REPO" )',
            "brace group": '{ rm -rf "$REPO"; }',
            "if wrapper": 'if true; then rm -rf "$REPO"; fi',
            "for wrapper": 'for x in 1; do rm -rf "$REPO"; done',
            "while wrapper": 'while false; do rm -rf "$REPO"; done',
            "xargs pipeline": 'echo "$REPO" | xargs rm -rf',
            # Case arms: the arm's `)` used to be absorbed into the pattern token, so the arm's
            # body stayed inside the pattern's command and argv[0] came back as the pattern.
            "case arm bare": 'case "$x" in *) rm -rf "$REPO";; esac',
            "case arm parenthesised": 'case "$x" in (*) rm -rf "$REPO";; esac',
            "case arm multi-pattern": 'case "$x" in a|b) rm -fr "$REPO";; esac',
            "case arm fallthrough": 'case "$x" in *) rm -Rf "$REPO";;& esac',
            # Command substitution: gate.sh uses $( 31 times, so an assignment whose value RUNS a
            # command looked like an ordinary assignment and was skipped.
            "substitution dollar-paren": 'X=$(rm -rf "$REPO")',
            "substitution backtick": 'X=`rm -rf "$REPO"`',
            "substitution inline": 'echo "$(rm -rf "$REPO")"',
            "substitution process": 'cat <(rm -rf "$REPO")',
            "substitution spaced": 'Y=$( rm -fr "$REPO" )',
        }

    def _inject(self, statement):
        self.assertIn(self.LAWFUL_ANCHOR, GATE, "the lawful anchor moved")
        return GATE.replace(self.LAWFUL_ANCHOR, self.LAWFUL_ANCHOR + "\t" + statement + "\n", 1)

    def test_every_rm_spelling_fires(self):
        """The question is not how the flags are written; it is whether the command is rm."""
        for name, statement in self._rm_spellings().items():
            with self.subTest(spelling=name):
                self.assertTrue(
                    self.audit(self._inject(statement)),
                    name + " audited green while destructive",
                )

    def test_every_rm_control_dies_when_the_tokeniser_is_neutered(self):
        """Each control must PIN the tokeniser, not trip some other check by accident.

        The previous nine controls replaced the allowlisted cleanup line, so they fired on the
        exact-form and body checks and would have passed with argv_zero returning "" - pinning
        nothing of the thing they were named for.
        """
        original = CHECKER.argv_zero
        CHECKER.argv_zero = lambda command: ""
        try:
            for name, statement in self._rm_spellings().items():
                with self.subTest(spelling=name):
                    self.assertEqual(
                        [], self.audit(self._inject(statement)),
                        name + " still fires with the tokeniser neutered, so it pins something "
                        "other than the tokeniser",
                    )
        finally:
            CHECKER.argv_zero = original

    def test_cleanup_removing_the_repository_fires(self):
        problems = self.audit(
            GATE.replace(CHECKER.CLEANUP_ONE_LINE, 'cleanup() { rm -rf "$REPO"; }')
        )
        self.assertTrue(problems, "cleanup could still delete the repository")

    @staticmethod
    def _mutants():
        pre = GATE.index("prepare_dev_harness() {")
        return {
            "exit quoted zero": GATE.replace("failed=0\n", 'exit "0"\nfailed=0\n', 1),
            "exit semicolon": GATE.replace("failed=0\n", "exit 0;\nfailed=0\n", 1),
            "second failed=0": GATE.replace('if [ "$failed" -eq 0 ]; then',
                                            'failed=0\nif [ "$failed" -eq 0 ]; then', 1),
            "nested failed reset": GATE.replace('if [ "$failed" -eq 0 ]; then',
                                                'if true; then\nfailed=0\nfi\n'
                                                'if [ "$failed" -eq 0 ]; then', 1),
            "one-line shadow": GATE.replace("RECEIPT=",
                                            "compile_dev_harness() { :; }\nRECEIPT=", 1),
            "all three shadowed": GATE.replace(
                "RECEIPT=", "compile_mode() { :; }\nprepare_dev_harness() { :; }\n"
                            "compile_dev_harness() { :; }\nRECEIPT=", 1),
            "rc initialiser forged": GATE.replace("dev_baseline_rc=1", "dev_baseline_rc=0", 1),
            "receipt before compiles": GATE.replace(
                "failed=0\n", 'printf "x" > "$RECEIPT"\nfailed=0\n', 1),
            "trap via variable": GATE.replace("trap cleanup EXIT", "T='- EXIT'; trap $T"),
            "trap emptied": GATE.replace("trap cleanup EXIT", 'trap "" EXIT'),
            "final exit zero": GATE.replace('exit "$failed"', "exit 0"),
            "compiler removed": GATE.replace("csc.dll", "true.dll"),
            "prepare stubbed": GATE[:pre] + "prepare_dev_harness() {\n\treturn 0\n}"
                               + GATE[GATE.index("\n}", pre) + 2:],
            "inventory digest dropped": GATE.replace("--inventory-digest", "--list-harness"),
            "derived dev tree": GATE.replace(
                'DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"', 'DEV="$STAGE.dev"'),
        }

    def test_every_known_neutralisation_class_fires(self):
        for name, mutant in self._mutants().items():
            with self.subTest(neutralisation=name):
                self.assertTrue(self.audit(mutant), name + " left the audit green")


class RouteReceiptTest(unittest.TestCase):
    """Defect: the verdict claimed a compile that had never happened.

    Wiring is a claim about the script; only a receipt is a claim about a run. Neutralising the
    gate now stops producing a receipt rather than producing a green one.
    """

    def test_the_verdict_matches_whether_a_receipt_exists(self):
        """Conditional on the receipt, not on today's absence of one.

        Asserting "no receipt exists" would fail the moment root lawfully runs the gate - a test
        that breaks on the success it is waiting for.
        """
        receipt = CHECKER.route_receipt()
        if CHECKER.RECEIPT.is_file():
            if receipt is not None:
                self.assertEqual(0, receipt["gateStatus"])
                self.assertIn("recordedUtc", receipt)
        else:
            self.assertIsNone(
                receipt, "no receipt file exists, so nothing may verify as one"
            )

    def test_a_receipt_must_match_the_current_shard_set_and_outcome(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-receipt-test."))
        try:
            shards = INVENTORY.harness_shards()
            good = {
                "schema": INVENTORY.RECEIPT_SCHEMA,
                "recordedUtc": "2026-08-29T00:00:00Z",
                "harnessShards": shards,
                "inventoryDigest": INVENTORY.inventory_digest(),
                "devModes": {"baseline": 0, "compatibility": 0},
                "gateStatus": 0,
            }
            path = tmp / "receipt.json"
            path.write_text(json.dumps(good), encoding="utf-8")
            self.assertIsNotNone(INVENTORY.read_receipt(str(path)))
            for broken in (
                {"harnessShards": shards[:-1]},
                {"inventoryDigest": "0" * 64},
                {"recordedUtc": "not-a-time"},
                {"recordedUtc": 17},
                {"devModes": {"baseline": 1, "compatibility": 0}},
                {"devModes": {"baseline": 0}},
                {"gateStatus": 1},
                {"schema": "other"},
            ):
                with self.subTest(broken=sorted(broken)):
                    body = dict(good)
                    body.update(broken)
                    path.write_text(json.dumps(body), encoding="utf-8")
                    self.assertIsNone(INVENTORY.read_receipt(str(path)))
            path.write_text("not json", encoding="utf-8")
            self.assertIsNone(INVENTORY.read_receipt(str(path)))
            self.assertIsNone(INVENTORY.read_receipt(str(tmp / "absent.json")))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


class InventoryRefusalTest(unittest.TestCase):
    """A refusal from the shared helper is a problem line at the audit level, not a traceback."""

    def test_a_refusing_helper_becomes_a_problem_line(self):
        original = CHECKER.dev_route_shards

        def refuse():
            raise CHECKER.InventoryRefused("harness tree contains a subdirectory: Sub")

        CHECKER.dev_route_shards = refuse
        try:
            problems: list[str] = []
            try:
                CHECKER.assert_inventory(problems)
            except CHECKER.InventoryRefused as refusal:
                problems.append("dev-harness inventory: " + str(refusal))
            self.assertTrue(
                any(row.startswith("dev-harness inventory:") for row in problems), problems
            )
            self.assertFalse(
                any("Traceback" in row for row in problems), "a stack escaped as a finding"
            )
        finally:
            CHECKER.dev_route_shards = original

    def test_the_audit_main_catches_the_refusal(self):
        source = (ROOT / "Tools" / "check-harness-registration.py").read_text(encoding="utf-8")
        self.assertIn("except InventoryRefused as refusal:", source)
        self.assertIn('problems.append("dev-harness inventory: " + str(refusal))', source)


class NestedShardTest(unittest.TestCase):
    """Defect: a non-recursive glob dropped Harness/Sub/*.cs - compiled by nothing, guarded by none.

    The previous version of this control created a directory INSIDE the frozen Harness tree. A
    killed run would have left it there and bricked the gate, the checker, and the inventory. A test
    that can damage the thing it audits is not a control; everything here happens on a tmp copy.
    """

    def test_a_nested_shard_refuses_rather_than_disappearing(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-nested-test."))
        original = INVENTORY.HARNESS
        try:
            fake = tmp / "Harness"
            fake.mkdir()
            (fake / "KingdomScenarioHarness.cs").write_text("class H {}", encoding="utf-8")
            (fake / "Sub").mkdir()
            (fake / "Sub" / "Sneak.cs").write_text("class Sneak {}", encoding="utf-8")
            INVENTORY.HARNESS = fake
            with self.assertRaises(SystemExit) as caught:
                INVENTORY.harness_shards()
            self.assertIn("subdirectory", str(caught.exception))
        finally:
            INVENTORY.HARNESS = original
            shutil.rmtree(tmp, ignore_errors=True)

    def test_a_nested_refusal_is_a_problem_line_not_a_traceback(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-nested-msg."))
        original = INVENTORY.HARNESS
        try:
            fake = tmp / "Harness"
            (fake / "Sub").mkdir(parents=True)
            INVENTORY.HARNESS = fake
            with self.assertRaises(SystemExit) as caught:
                INVENTORY.harness_shards()
            message = str(caught.exception)
            self.assertNotIn("Traceback", message)
            self.assertTrue(message.strip(), "a refusal must name itself")
        finally:
            INVENTORY.HARNESS = original
            shutil.rmtree(tmp, ignore_errors=True)

    def test_a_case_folded_overlay_directory_is_not_counted_as_ordinary(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-casefold-test."))
        try:
            stage = tmp / "stage"
            (stage / "Core").mkdir(parents=True)
            (stage / "Core" / "A.cs").write_text("class A {}", encoding="utf-8")
            sneaky = stage / "harness"
            sneaky.mkdir()
            (sneaky / "Sneak.cs").write_text("class Sneak {}", encoding="utf-8")
            rows = [
                p.relative_to(stage).as_posix()
                for p in INVENTORY.ordinary_sources(stage, "baseline")
            ]
            self.assertEqual(["Core/A.cs"], rows)
            with self.assertRaises(SystemExit):
                INVENTORY.emit(str(stage), str(tmp / "out"), "baseline", False)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


class ReceiptMintingTest(unittest.TestCase):
    """Ruling: no CLI may mint a receipt. A flag that writes one is a compiler nobody ran."""

    def test_the_minting_flags_are_gone(self):
        source = (ROOT / "Tools" / "dev-harness-inventory.py").read_text(encoding="utf-8")
        for flag in ("--receipt", "--mode-status", "--gate-status", "def write_receipt"):
            self.assertNotIn(flag, source, flag + " can still mint a receipt")

    def test_the_minting_invocation_now_fails(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-mint-test."))
        try:
            target = tmp / "receipt.json"
            result = subprocess.run(
                [
                    sys.executable,
                    str(ROOT / "Tools" / "dev-harness-inventory.py"),
                    "--receipt", str(target),
                    "--mode-status", "baseline=0",
                    "--mode-status", "compatibility=0",
                    "--gate-status", "0",
                ],
                capture_output=True, text=True, cwd=str(ROOT),
            )
            self.assertNotEqual(0, result.returncode, "the minting invocation still succeeds")
            self.assertFalse(target.exists(), "a receipt was minted with no gate run")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def test_the_inventory_digest_command_writes_nothing(self):
        result = subprocess.run(
            [sys.executable, str(ROOT / "Tools" / "dev-harness-inventory.py"),
             "--inventory-digest"],
            capture_output=True, text=True, cwd=str(ROOT),
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertRegex(result.stdout.strip(), r"^[0-9a-f]{64}$")
        self.assertFalse(
            (ROOT / "Tools" / "PortableOutput" / "dev-harness-receipt.json").exists(),
            "reading a digest must not create a receipt",
        )


class OverlayBytesTest(unittest.TestCase):
    """Defect: the overlay was proved by filename, never by content."""

    def test_a_tampered_overlay_shard_refuses(self):
        tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-bytes-test."))
        try:
            stage = tmp / "stage"
            (stage / "Harness").mkdir(parents=True)
            for name in INVENTORY.harness_shards():
                shutil.copy(ROOT / "Harness" / name, stage / "Harness" / name)
            self.assertEqual(
                len(INVENTORY.harness_shards()),
                len(INVENTORY.overlay_sources(stage)),
            )
            victim = INVENTORY.harness_shards()[0]
            with open(stage / "Harness" / victim, "a", encoding="utf-8") as handle:
                handle.write("// tampered\n")
            with self.assertRaises(SystemExit) as caught:
                INVENTORY.overlay_sources(stage)
            self.assertIn("differs from the repository source", str(caught.exception))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
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
