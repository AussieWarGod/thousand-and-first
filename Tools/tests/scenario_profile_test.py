"""Adversarial tests for the scenario profile seal, seed, and derived launcher inputs.

These execute the authoritative implementation in Tools/scenario_profile.py. The PowerShell
launcher mirrors the same closed rule for Windows; its correctness is asserted separately as a
source contract in DevTests/KingdomScenarioLauncherSourceTests.cs.
"""

from __future__ import annotations

import builtins
import contextlib
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import tempfile
import threading
import unittest
from unittest import mock

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "scenario_profile", ROOT / "Tools" / "scenario_profile.py"
)
profile = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(profile)


class SeedValidationTest(unittest.TestCase):
    def test_exact_form_is_accepted(self):
        self.assertEqual("#4242", profile.validate_seed("#4242"))
        self.assertEqual("#1", profile.validate_seed("#1"))
        self.assertEqual("#2147483647", profile.validate_seed("#2147483647"))

    def test_zero_is_lawful(self):
        # GetWorldSeed parses the digits with int.TryParse and returns the parsed value, so '#0'
        # names a world the engine reproduces. Refusing it would refuse a lawful seed.
        self.assertEqual("#0", profile.validate_seed("#0"))
        self.assertEqual("#00", profile.validate_seed("#00"))

    def test_glob_style_near_misses_are_rejected(self):
        # A shell glob '#[0-9]*' accepts every one of these; exact syntax must not.
        for seed in ("#4a2", "#42x", "#4 2", "#42;rm", "#42\n", "#+42", "#4.2", "#42_"):
            with self.subTest(seed=seed):
                with self.assertRaises(SystemExit):
                    profile.validate_seed(seed)

    def test_shape_and_range_are_rejected(self):
        for seed in (
            "",
            "#",
            "4242",
            "##42",
            "-#42",
            "#-1",
            "#2147483648",
            "#99999999999",
        ):
            with self.subTest(seed=seed):
                with self.assertRaises(SystemExit):
                    profile.validate_seed(seed)


class ProfileSealTest(unittest.TestCase):
    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-seal-test."))
        self.tree = self.tmp / "Local"
        (self.tree / "Mods" / "ThousandAndFirst" / "Harness").mkdir(parents=True)
        self.mod = self.tree / "Mods" / "ThousandAndFirst"
        (self.mod / "manifest.json").write_text('{"id":"x"}', encoding="utf-8")
        (self.mod / "Core").mkdir()
        (self.mod / "Core" / "A.cs").write_text("class A {}", encoding="utf-8")
        (self.mod / "Harness" / "H.cs").write_text("class H {}", encoding="utf-8")
        (self.tree / "PlayerOptions.json").write_text("{}", encoding="utf-8")
        self.seal = self.tmp / "profile.sha256"
        profile.seal(str(self.tree), str(self.seal))

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def verify(self):
        profile.verify(str(self.tree), str(self.seal))

    def test_untouched_profile_verifies(self):
        self.verify()

    def test_added_file_is_rejected(self):
        # The defect a subset seal misses: an injected source compiles but is unsealed.
        (self.mod / "Harness" / "Injected.cs").write_text(
            "class I {}", encoding="utf-8"
        )
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("extra", str(caught.exception))

    def test_added_file_outside_harness_is_rejected(self):
        (self.mod / "Core" / "Injected.cs").write_text("class I {}", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.verify()

    def test_removed_file_is_rejected(self):
        (self.mod / "Core" / "A.cs").unlink()
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("missing", str(caught.exception))

    def test_modified_file_is_rejected(self):
        (self.mod / "Core" / "A.cs").write_text("class A { int x; }", encoding="utf-8")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("modified", str(caught.exception))

    def test_renamed_file_is_rejected(self):
        (self.mod / "Core" / "A.cs").rename(self.mod / "Core" / "B.cs")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        message = str(caught.exception)
        self.assertIn("missing", message)
        self.assertIn("extra", message)

    def test_symlinked_extra_is_rejected(self):
        target = self.tmp / "outside.cs"
        target.write_text("class Outside {}", encoding="utf-8")
        try:
            os.symlink(target, self.mod / "Harness" / "Linked.cs")
        except (OSError, NotImplementedError):
            self.skipTest("symlinks unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("symlink", str(caught.exception))

    def test_symlinked_directory_is_rejected(self):
        other = self.tmp / "elsewhere"
        other.mkdir()
        (other / "X.cs").write_text("class X {}", encoding="utf-8")
        try:
            os.symlink(other, self.mod / "Linked", target_is_directory=True)
        except (OSError, NotImplementedError):
            self.skipTest("symlinks unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("link", str(caught.exception))

    def test_symlinked_tree_root_is_rejected(self):
        linked_root = self.tmp / "linked-local"
        try:
            os.symlink(self.tree, linked_root, target_is_directory=True)
        except (OSError, NotImplementedError):
            self.skipTest("symlinks unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            profile.inventory(str(linked_root))
        self.assertIn("linked directory", str(caught.exception))

    def test_hard_linked_file_is_rejected(self):
        # os.path.islink is False for a hard link: it is a second NAME for the sealed inode, and
        # writing through the other name changes the sealed bytes from outside the profile.
        target = self.tmp / "outside.cs"
        target.write_text("class Outside {}", encoding="utf-8")
        try:
            os.link(target, self.mod / "Harness" / "Hard.cs")
        except (OSError, NotImplementedError, AttributeError):
            self.skipTest("hard links unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("hard-linked", str(caught.exception))

    def test_hard_linking_a_sealed_file_is_rejected(self):
        # The other direction: the sealed file itself gains a second name after sealing.
        alias = self.tmp / "alias.cs"
        try:
            os.link(self.mod / "Core" / "A.cs", alias)
        except (OSError, NotImplementedError, AttributeError):
            self.skipTest("hard links unavailable on this platform")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("hard-linked", str(caught.exception))

    def test_duplicate_normalized_paths_are_rejected(self):
        try:
            (self.mod / "Core" / "a.cs").write_text("class A2 {}", encoding="utf-8")
        except OSError:
            self.skipTest("case-insensitive filesystem")
        if (
            not (self.mod / "Core" / "a.cs").exists()
            or (self.mod / "Core" / "A.cs").read_text(encoding="utf-8") == "class A2 {}"
        ):
            self.skipTest("case-insensitive filesystem")
        with self.assertRaises(SystemExit) as caught:
            self.verify()
        self.assertIn("normalize", str(caught.exception))

    def test_tampered_seal_header_is_rejected(self):
        self.seal.write_text("bogus-header\n", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.verify()

    def test_malformed_seal_digest_is_rejected(self):
        self.seal.write_text(profile.SEAL_HEADER + "\nnothex  a.cs\n", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.verify()

    def test_empty_tree_is_rejected(self):
        empty = self.tmp / "empty"
        empty.mkdir()
        with self.assertRaises(SystemExit):
            profile.inventory(str(empty))


class ParallelInventoryTest(unittest.TestCase):
    WAIT_SECONDS = 15

    def test_bounded_overlapping_reads_preserve_every_digest(self):
        with tempfile.TemporaryDirectory(prefix="taf-inventory-workers.") as temporary:
            root = pathlib.Path(temporary)
            expected = {}
            for index in range(18):
                name = "Input%d.XML" % index
                data = ("file %d\n" % index).encode() * (10000 if index == 0 else 11)
                (root / name).write_bytes(data)
                expected[name.casefold()] = hashlib.sha256(data).hexdigest()
            lock, barrier = threading.Lock(), threading.Barrier(4, timeout=self.WAIT_SECONDS)
            active = peak = started = completed = 0

            @contextlib.contextmanager
            def observed_open(*args, **kwargs):
                nonlocal active, peak, started, completed
                with lock:
                    active += 1
                    started += 1
                    ordinal = started
                    peak = max(peak, active)
                try:
                    if ordinal <= 4:
                        barrier.wait()
                    with builtins.open(*args, **kwargs) as handle:
                        yield handle
                    with lock:
                        completed += 1
                finally:
                    with lock:
                        active -= 1

            with mock.patch.object(profile, "open", observed_open, create=True):
                self.assertEqual(expected, profile.inventory(str(root)))
            self.assertEqual((4, 18, 18, 0), (peak, started, completed, active))

    def test_read_failure_joins_siblings_and_preserves_previous_seal(self):
        with tempfile.TemporaryDirectory(prefix="taf-inventory-failure.") as temporary:
            root = pathlib.Path(temporary) / "Local"
            root.mkdir()
            for index in range(4):
                (root / ("Input%d.xml" % index)).write_bytes(b"sealed bytes")
            seal = pathlib.Path(temporary) / "profile.sha256"
            profile.seal(str(root), str(seal))
            previous = seal.read_bytes()
            barrier = threading.Barrier(4, timeout=self.WAIT_SECONDS)
            failed, release, finished = threading.Event(), threading.Event(), threading.Event()
            completed, errors = [], []

            @contextlib.contextmanager
            def observed_open(path, *args, **kwargs):
                barrier.wait()
                if pathlib.Path(path).name == "Input0.xml":
                    failed.set()
                    raise OSError("synthetic hashing failure")
                if not release.wait(self.WAIT_SECONDS * 2):
                    raise TimeoutError("sibling reader was not released")
                with builtins.open(path, *args, **kwargs) as handle:
                    yield handle
                completed.append(path)

            def run_seal():
                try:
                    profile.seal(str(root), str(seal))
                except BaseException as error:
                    errors.append(error)
                finally:
                    finished.set()

            ordered_files = ["Input%d.xml" % index for index in range(4)]
            with mock.patch.object(profile, "open", observed_open, create=True), \
                 mock.patch.object(profile.os, "walk", return_value=[(str(root), [], ordered_files)]):
                caller = threading.Thread(target=run_seal)
                caller.start()
                try:
                    self.assertTrue(failed.wait(self.WAIT_SECONDS), "four readers must overlap")
                    self.assertFalse(finished.wait(0.2), "failure escaped before sibling reads ended")
                finally:
                    release.set()
                    caller.join(self.WAIT_SECONDS)
                self.assertFalse(caller.is_alive())
            self.assertEqual(3, len(completed))
            self.assertEqual(1, len(errors))
            self.assertIsInstance(errors[0], OSError)
            self.assertEqual("synthetic hashing failure", str(errors[0]))
            self.assertEqual(previous, seal.read_bytes())


class LauncherTrustSourceTest(unittest.TestCase):
    def test_prepare_hashes_once_and_launcher_rechecks_at_use(self):
        prepare = (ROOT / "Tools" / "prepare-scenario.sh").read_text(encoding="utf-8")
        launcher = (ROOT / "Tools" / "run-scenario.ps1").read_text(encoding="utf-8")
        self.assertIn('seal "$LOCAL" "$SEAL_DIR/profile.sha256"', prepare)
        self.assertNotIn('verify "$LOCAL"', prepare)
        self.assertIn(
            "Assert-ClosedSeal -TreeRoot $localRoot -SealPath $profileSeal", launcher
        )

    def test_windows_link_proof_is_in_process_and_brackets_the_hash(self):
        launcher = (ROOT / "Tools" / "run-scenario.ps1").read_text(encoding="utf-8")
        trust = (ROOT / "Tools" / "ScenarioFileTrust.cs").read_text(encoding="utf-8")
        self.assertNotIn("fsutil hardlink list", launcher)
        self.assertIn("Add-Type -Path $trustSource", launcher)
        self.assertIn("Get-Item -LiteralPath $TreeRoot -Force", launcher)
        self.assertIn("Profile tree root is a reparse point", launcher)
        self.assertIn("GetFileInformationByHandleEx", trust)
        self.assertIn("return information.NumberOfLinks", trust)
        before = launcher.index(
            "$hardLinkCount = [ThousandAndFirst.Tools.ScenarioFileTrust]::GetLinkCount"
        )
        digest = launcher.index("$digest = $sha256.ComputeHash($stream)", before)
        after = launcher.index(
            "$hardLinkCountAfterHash = [ThousandAndFirst.Tools.ScenarioFileTrust]::GetLinkCount",
            digest,
        )
        self.assertLess(before, digest)
        self.assertLess(digest, after)
        self.assertIn("if ($hardLinkCount -ne 1)", launcher)
        self.assertIn("if ($hardLinkCountAfterHash -ne 1)", launcher)


class DerivedInputTest(unittest.TestCase):
    def setUp(self):
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-derived-test."))

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_dev_manifest_selects_harness_and_shipped_one_does_not(self):
        destination = self.tmp / "manifest.json"
        profile.write_manifest(str(ROOT / "manifest.json"), str(destination))
        shipped = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
        derived = json.loads(destination.read_text(encoding="utf-8"))
        self.assertNotIn("/Harness/", shipped["Directories"][0]["Paths"])
        self.assertIn("/Harness/", derived["Directories"][0]["Paths"])

    def test_dev_manifest_refuses_a_shipped_manifest_that_already_selects_harness(self):
        source = self.tmp / "already.json"
        source.write_text(
            json.dumps({"Directories": [{"Paths": ["/Core/", "/Harness/"]}]}),
            encoding="utf-8",
        )
        with self.assertRaises(SystemExit):
            profile.write_manifest(str(source), str(self.tmp / "out.json"))

    def test_options_expose_seed_and_pin_native_capture_rendering(self):
        destination = self.tmp / "PlayerOptions.json"
        profile.write_options(
            str(ROOT / "Tools" / "smoke" / "PlayerOptions.json"), str(destination)
        )
        options = json.loads(destination.read_text(encoding="utf-8"))
        self.assertEqual("Yes", options["OptionEnableSeed"])
        self.assertEqual(
            {
                "OptionPrereleaseStageScale": "auto",
                "OptionPlayScale": "Fit",
                "OptionTileScale": "1",
                "OptionDisplayBrightness": "0",
                "OptionDisplayContrast": "0",
                "OptionDisplayScanlines": "Yes",
            },
            {
                key: options[key]
                for key in (
                    "OptionPrereleaseStageScale",
                    "OptionPlayScale",
                    "OptionTileScale",
                    "OptionDisplayBrightness",
                    "OptionDisplayContrast",
                    "OptionDisplayScanlines",
                )
            },
        )

    PAUSE_SCRIPT = ("stagedigest realize advance 1200 beta-local-pause advance 1200 "
                    "beta-master-pause advance 1 beta-stress beta-present advance 1200 "
                    "beta-return advance 39 yield-frames 1 beta-check status")

    def engine_bag(self, extra_keys):
        """The engine's NameValueBag flush: one "key":"value" per line, no trailing newline."""
        template = json.loads((ROOT / "Tools" / "smoke" / "PlayerOptions.json").read_text(encoding="utf-8"))
        template["OptionEnableSeed"] = "Yes"
        for key in extra_keys:
            template[key] = "Yes"
        return ("{\n" + ",\n".join(json.dumps(k) + ":" + json.dumps(v) for k, v in template.items())
                + "\n}").encode()

    def test_pause_recipe_authors_both_pause_options_in_engine_format(self):
        local = self.tmp / "Local"
        local.mkdir()
        destination = local / "PlayerOptions.json"
        with mock.patch.dict(os.environ, {"TAF_SCENARIO_SCRIPT": self.PAUSE_SCRIPT}):
            profile.write_options(str(ROOT / "Tools" / "smoke" / "PlayerOptions.json"), str(destination))
        expected = self.engine_bag(("r_TAF_OptionGrowth", "r_TAF_OptionMaster"))
        self.assertEqual(expected, destination.read_bytes())
        self.assertEqual(["r_TAF_OptionGrowth", "r_TAF_OptionMaster"],
                         list(json.loads(destination.read_text(encoding="utf-8")))[-2:])
        seal = self.tmp / "pause.sha256"
        profile.seal(str(local), str(seal))
        # PauseController restores both to "Yes"; the engine's flush then rewrites identical bytes.
        destination.write_bytes(expected)
        profile.verify(str(local), str(seal))
        for drift in (expected.replace(b'"r_TAF_OptionMaster":"Yes"', b'"r_TAF_OptionMaster":"No"'),
                      expected.replace(b'"r_TAF_OptionGrowth":"Yes"', b'"r_TAF_OptionGrowth":"No"'),
                      expected + b"\n"):
            with self.subTest(drift=drift[-40:]):
                destination.write_bytes(drift)
                with self.assertRaises(SystemExit):
                    profile.verify(str(local), str(seal))

    WATER_SCRIPT = ("stagedigest water-maintenance-setup advance 2400 water-maintenance-enroll advance 1200 "
                    "water-maintenance-check advance 1200 water-maintenance-check advance 1200 "
                    "water-maintenance-refill advance 1200 water-maintenance-check stagedigest")

    def test_water_recipe_authors_final_growth_and_raids_values_in_engine_format(self):
        local = self.tmp / "Local"
        local.mkdir()
        destination = local / "PlayerOptions.json"
        with mock.patch.dict(os.environ, {"TAF_SCENARIO_SCRIPT": self.WATER_SCRIPT}):
            profile.write_options(str(ROOT / "Tools" / "smoke" / "PlayerOptions.json"), str(destination))
        template = json.loads((ROOT / "Tools" / "smoke" / "PlayerOptions.json").read_text(encoding="utf-8"))
        template["OptionEnableSeed"] = "Yes"
        template["r_TAF_OptionGrowth"] = "No"
        template["r_TAF_OptionRaids"] = "No"
        expected = ("{\n" + ",\n".join(json.dumps(k) + ":" + json.dumps(v) for k, v in template.items())
                    + "\n}").encode()
        self.assertEqual(expected, destination.read_bytes())
        self.assertNotIn(b"r_TAF_OptionMaster", expected)
        seal = self.tmp / "water.sha256"
        profile.seal(str(local), str(seal))
        destination.write_bytes(expected)
        profile.verify(str(local), str(seal))
        for drift in (expected.replace(b'"r_TAF_OptionGrowth":"No"', b'"r_TAF_OptionGrowth":"Yes"'),
                      expected.replace(b'"r_TAF_OptionRaids":"No"', b'"r_TAF_OptionRaids":"Yes"')):
            destination.write_bytes(drift)
            with self.assertRaises(SystemExit):
                profile.verify(str(local), str(seal))

    def test_conflicting_option_writers_refuse_before_writing(self):
        destination = self.tmp / "PlayerOptions.json"
        destination.write_text("retained", encoding="utf-8")
        script = self.PAUSE_SCRIPT + " water-maintenance-setup"
        with mock.patch.dict(os.environ, {"TAF_SCENARIO_SCRIPT": script}), self.assertRaises(SystemExit):
            profile.write_options(str(ROOT / "Tools" / "smoke" / "PlayerOptions.json"), str(destination))
        self.assertEqual("retained", destination.read_text(encoding="utf-8"))

    def test_non_pause_recipe_options_are_unchanged(self):
        destination = self.tmp / "PlayerOptions.json"
        script = "stagedigest realize stagedigest resourcedigest status"
        with mock.patch.dict(os.environ, {"TAF_SCENARIO_SCRIPT": script}):
            profile.write_options(str(ROOT / "Tools" / "smoke" / "PlayerOptions.json"), str(destination))
        template = json.loads((ROOT / "Tools" / "smoke" / "PlayerOptions.json").read_text(encoding="utf-8"))
        template["OptionEnableSeed"] = "Yes"
        self.assertEqual((json.dumps(template, indent=2) + "\n").encode(), destination.read_bytes())
        self.assertNotIn(b"r_TAF_OptionGrowth", destination.read_bytes())
        self.assertNotIn(b"r_TAF_OptionMaster", destination.read_bytes())

    def test_request_requires_a_valid_frozen_seed(self):
        embark = self.tmp / "EmbarkModules.xml"
        embark.write_text(
            '<x Name="r_TAF_ScenarioRequest_v1" Value="old" />', encoding="utf-8"
        )
        for bad in (
            "arch-gallery-slice;facing=north",
            "arch;seed=#4a2",
            "arch;seed=#2147483648",
        ):
            with self.subTest(request=bad):
                os.environ["TAF_REQUEST"] = bad
                with self.assertRaises(SystemExit):
                    profile.write_request(str(embark))
        os.environ["TAF_REQUEST"] = "arch-gallery-slice;facing=north;seed=#4242"
        profile.write_request(str(embark))
        self.assertIn(
            'Value="arch-gallery-slice;facing=north;seed=#4242"',
            embark.read_text(encoding="utf-8"),
        )
        del os.environ["TAF_REQUEST"]


class QuickstartBootPreparationTest(unittest.TestCase):
    """Execute profile preparation only; these tests do not boot or prove the native game."""

    def setUp(self):
        self.environment = mock.patch.dict(os.environ, {}, clear=True)
        self.environment.start()
        self.addCleanup(self.environment.stop)
        self.tmp = pathlib.Path(tempfile.mkdtemp(prefix="taf-quickstart-options-test."))
        self.addCleanup(shutil.rmtree, self.tmp, True)
        self.source = self.tmp / "source.json"
        self.original = {"OtherOption": "retained", profile.QUICKSTART_ADVISOR_OPTION: "old"}
        self.source.write_text(json.dumps(self.original), encoding="utf-8")
        self.local = self.tmp / "Local"
        self.local.mkdir()
        self.options = self.local / "PlayerOptions.json"
        self.script = self.local / "scenario-script.txt"

    def choose(self, terrain="marsh", advisor="yes", verb="quickstart-boot"):
        tokens = [verb, terrain, advisor]
        os.environ["TAF_SCENARIO_SCRIPT"] = " ".join(tokens)
        os.environ[profile.QUICKSTART_ADVISOR_ENV] = advisor
        return tokens

    def write_options(self):
        profile.write_options(str(self.source), str(self.options))

    def assert_options_refuse_unchanged(self):
        self.options.write_text("retained destination", encoding="utf-8")
        with self.assertRaises(SystemExit):
            self.write_options()
        self.assertEqual("retained destination", self.options.read_text(encoding="utf-8"))

    def assert_script_refuse_unchanged(self, tokens):
        self.script.write_text("retained script", encoding="utf-8")
        with self.assertRaises(SystemExit):
            profile.write_script(str(self.script), tokens)
        self.assertEqual("retained script", self.script.read_text(encoding="utf-8"))

    def test_six_exact_boot_commands_are_canonical_single_lines(self):
        for terrain in ("marsh", "canyon", "dunes"):
            for advisor in ("yes", "no"):
                with self.subTest(terrain=terrain, advisor=advisor):
                    tokens = ["quickstart-boot", terrain, advisor]
                    self.assertEqual([" ".join(tokens)], profile.parse_script(tokens))

    def test_lifecycle_options_script_and_closed_seal_all_six_combinations(self):
        for terrain in ("marsh", "canyon", "dunes"):
            for advisor in ("yes", "no"):
                with self.subTest(terrain=terrain, advisor=advisor):
                    prefix = self.choose(terrain, advisor, "quickstart-lifecycle")
                    tokens = prefix + ["advance", "2400", "lifecycle-grown", "lifecycle-save"]
                    os.environ["TAF_SCENARIO_SCRIPT"] = " ".join(tokens)
                    os.environ["TAF_SCENARIO_EXTRA_VERBS"] = "lifecycle-grown,lifecycle-save"
                    self.write_options()
                    profile.write_script(str(self.script), tokens)
                    options = json.loads(self.options.read_text(encoding="utf-8"))
                    self.assertEqual(advisor.title(), options[profile.QUICKSTART_ADVISOR_OPTION])
                    self.assertEqual("retained", options["OtherOption"])
                    self.assertTrue(self.script.read_text(encoding="utf-8").endswith(
                        " ".join(prefix) + "\nadvance 2400\nlifecycle-grown\nlifecycle-save\n"))
                    seal = self.tmp / "lifecycle.sha256"
                    profile.seal(str(self.local), str(seal))
                    profile.verify(str(self.local), str(seal))

    def test_lifecycle_options_refuse_invalid_tail_before_writing(self):
        for tail in ("advance", "advance 0", "unknown", "quickstart-lifecycle marsh no",
                     "quickstart-boot marsh no"):
            with self.subTest(tail=tail):
                self.choose("marsh", "no", "quickstart-lifecycle")
                os.environ["TAF_SCENARIO_SCRIPT"] += " " + tail
                self.assert_options_refuse_unchanged()

    def test_lifecycle_verb_cannot_be_claimed_as_an_extra_provider(self):
        with self.assertRaises(SystemExit):
            profile.parse_extra_verbs("quickstart-lifecycle")

    def test_lifecycle_tail_still_requires_explicit_matching_advisor(self):
        self.choose("marsh", "no", "quickstart-lifecycle")
        os.environ["TAF_SCENARIO_SCRIPT"] += " advance 2400"
        os.environ[profile.QUICKSTART_ADVISOR_ENV] = "yes"
        self.assert_options_refuse_unchanged()
        del os.environ[profile.QUICKSTART_ADVISOR_ENV]
        self.assert_options_refuse_unchanged()

    def test_invalid_boot_shape_refuses_even_when_claimed_as_extra(self):
        for tokens in (
            ["quickstart-boot"], ["quickstart-boot", "marsh"],
            ["quickstart-boot", "marsh", "yes", "status"],
            ["status", "quickstart-boot", "marsh", "yes"],
            ["quickstart-boot", "marsh", "yes", "quickstart-boot", "dunes", "no"],
            ["quickstart-boot marsh yes"],
        ):
            with self.subTest(tokens=tokens), self.assertRaises(SystemExit):
                profile.parse_script(tokens, ("quickstart-boot",))

    def test_profiles_and_advisor_values_are_exact(self):
        for terrain in ("", "Marsh", "marsh ", "swamp", "canyon\n", "dunes\x00"):
            with self.subTest(terrain=terrain), self.assertRaises(SystemExit):
                profile.parse_script(["quickstart-boot", terrain, "yes"])
        for advisor in ("", "Yes", "NO", "1", "true", " yes", "no\n", "yes\x00"):
            with self.subTest(advisor=advisor), self.assertRaises(SystemExit):
                profile.parse_script(["quickstart-boot", "marsh", advisor])

    def test_boot_command_is_not_a_provider_or_ordinary_script_verb(self):
        self.assertNotIn("quickstart-boot", profile.SCRIPT_VERBS)
        self.assertNotIn("quickstart-boot", profile.RESERVED_VERBS)
        with self.assertRaises(SystemExit):
            profile.parse_extra_verbs("quickstart-boot")

    def test_six_cli_preparations_change_only_advisor_and_seed_visibility(self):
        original_bytes = self.source.read_bytes()
        for terrain in ("marsh", "canyon", "dunes"):
            for advisor in ("yes", "no"):
                with self.subTest(terrain=terrain, advisor=advisor):
                    tokens = self.choose(terrain, advisor)
                    self.assertEqual(0, profile.main([
                        "scenario_profile.py", "options", str(self.source), str(self.options)
                    ]))
                    self.assertEqual(0, profile.main([
                        "scenario_profile.py", "script", str(self.script), *tokens
                    ]))
                    expected = dict(self.original, OptionEnableSeed="Yes")
                    expected[profile.QUICKSTART_ADVISOR_OPTION] = advisor.title()
                    self.assertEqual(expected, json.loads(self.options.read_text(encoding="utf-8")))
                    text = self.script.read_text(encoding="utf-8")
                    self.assertEqual(profile.QUICKSTART_SCRIPT_HEADER + " ".join(tokens) + "\n", text)
                    self.assertEqual([" ".join(tokens)], [
                        line for line in text.splitlines() if line and not line.startswith("#")
                    ])
                    self.assertEqual(original_bytes, self.source.read_bytes())

    def test_ordinary_options_and_script_are_unchanged_without_override(self):
        self.write_options()
        self.assertEqual(dict(self.original, OptionEnableSeed="Yes"),
                         json.loads(self.options.read_text(encoding="utf-8")))
        profile.write_script(str(self.script), [])
        self.assertEqual(profile.SCRIPT_HEADER + "flatten\nrealize\nstatus\n",
                         self.script.read_text(encoding="utf-8"))
        self.assertEqual(["status", "advance 12", "status"],
                         profile.parse_script(["status", "advance", "0012", "status"]))

    def test_physical_guest_walk_authors_engine_options_before_sealing(self):
        self.source.write_text('{"OptionShowQuickstart":"Yes"}', encoding="utf-8")
        self.choose("marsh", "yes", "quickstart-lifecycle")
        os.environ["TAF_SCENARIO_SCRIPT"] += " guest-save-supply"
        os.environ["TAF_SCENARIO_EXTRA_VERBS"] = "guest-save-supply"
        self.write_options()
        engine_wire = ('{\n"OptionShowQuickstart":"Yes",\n"OptionEnableSeed":"Yes",\n'
                       '"r_TAF_OptionQuickstartAdvisor":"Yes",\n"OptionLookLocked":"No"\n}')
        self.assertEqual(engine_wire.encode(), self.options.read_bytes())
        seal = self.tmp / "walking.sha256"
        profile.seal(str(self.local), str(seal))
        # An engine write retaining the initial option remains byte-identical.
        self.options.write_text(engine_wire, encoding="utf-8")
        profile.verify(str(self.local), str(seal))
        self.options.write_text(engine_wire.replace('"OptionLookLocked":"No"',
                                                    '"OptionLookLocked":"Yes"'), encoding="utf-8")
        with self.assertRaises(SystemExit): profile.verify(str(self.local), str(seal))

    def test_physical_guest_walk_invalid_script_does_not_rewrite_options(self):
        self.choose("marsh", "yes", "quickstart-lifecycle")
        os.environ["TAF_SCENARIO_SCRIPT"] += " guest-save-supply advance 0"
        os.environ["TAF_SCENARIO_EXTRA_VERBS"] = "guest-save-supply"
        self.assert_options_refuse_unchanged()

    def test_completed_heart_render_authors_look_default_before_sealing(self):
        self.choose("marsh", "yes", "quickstart-lifecycle")
        os.environ["TAF_SCENARIO_SCRIPT"] += " heart-sight-inspect yield-frames 3"
        os.environ["TAF_SCENARIO_EXTRA_VERBS"] = "heart-sight-inspect"
        self.write_options()
        original = self.options.read_text(encoding="utf-8")
        self.assertEqual("No", json.loads(original)["OptionLookLocked"])
        self.assertTrue(original.startswith('{\n"') and original.endswith('\n}'))
        seal = self.tmp / "heart-render.sha256"
        profile.seal(str(self.local), str(seal))
        profile.verify(str(self.local), str(seal))
        self.options.write_text(original + "\n", encoding="utf-8")
        with self.assertRaises(SystemExit): profile.verify(str(self.local), str(seal))

    def test_completed_heart_render_invalid_tail_preserves_options(self):
        self.choose("marsh", "yes", "quickstart-lifecycle")
        os.environ["TAF_SCENARIO_SCRIPT"] += " heart-sight-inspect yield-frames 0"
        os.environ["TAF_SCENARIO_EXTRA_VERBS"] = "heart-sight-inspect"
        self.assert_options_refuse_unchanged()

    def test_ordinary_options_do_not_invent_advisor_key(self):
        self.source.write_text('{"OtherOption":"retained"}', encoding="utf-8")
        self.write_options()
        self.assertNotIn(profile.QUICKSTART_ADVISOR_OPTION,
                         json.loads(self.options.read_text(encoding="utf-8")))

    def test_missing_advisor_override_refuses_before_options_write(self):
        self.choose()
        del os.environ[profile.QUICKSTART_ADVISOR_ENV]
        self.assert_options_refuse_unchanged()

    def test_mismatched_or_noncanonical_override_refuses_before_options_write(self):
        self.choose()
        for value in ("no", "Yes", "", " true", "yes\n", "1"):
            with self.subTest(value=value):
                os.environ[profile.QUICKSTART_ADVISOR_ENV] = value
                self.assert_options_refuse_unchanged()

    def test_override_without_exact_boot_script_refuses_before_options_write(self):
        self.choose()
        for script in (None, "", "none", "status", "flatten realize status",
                       "quickstart-boot marsh yes status", "quickstart-boot  marsh yes",
                       "quickstart-boot marsh yes\n", "quickstart-boot\tmarsh yes"):
            with self.subTest(script=script):
                if script is None:
                    os.environ.pop("TAF_SCENARIO_SCRIPT", None)
                else:
                    os.environ["TAF_SCENARIO_SCRIPT"] = script
                self.assert_options_refuse_unchanged()

    def test_override_rejects_ordinary_script_before_writing(self):
        self.choose()
        self.assert_script_refuse_unchanged(["status"])
        self.assert_script_refuse_unchanged([])
        del os.environ[profile.QUICKSTART_ADVISOR_ENV]
        self.assert_script_refuse_unchanged(["status"])
        self.assert_script_refuse_unchanged([])

    def test_script_requires_existing_matching_sibling_options(self):
        tokens = self.choose()
        self.assert_script_refuse_unchanged(tokens)
        for raw in ("not json", "[]", "null", "{}", "true",
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "No"}),
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: True}),
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "yes"})):
            with self.subTest(raw=raw):
                self.options.write_text(raw, encoding="utf-8")
                self.assert_script_refuse_unchanged(tokens)

    def test_script_rejects_environment_or_argument_mismatch(self):
        tokens = self.choose()
        self.write_options()
        self.assert_script_refuse_unchanged(["quickstart-boot", "dunes", "yes"])
        os.environ[profile.QUICKSTART_ADVISOR_ENV] = "no"
        self.assert_script_refuse_unchanged(tokens)
        del os.environ[profile.QUICKSTART_ADVISOR_ENV]
        self.assert_script_refuse_unchanged(tokens)

    def test_explicit_script_cli_still_requires_override_and_matching_options(self):
        tokens = self.choose("dunes", "no")
        self.write_options()
        del os.environ["TAF_SCENARIO_SCRIPT"]
        profile.write_script(str(self.script), tokens)
        self.assertTrue(self.script.read_text(encoding="utf-8").endswith("quickstart-boot dunes no\n"))

    def test_seal_binds_both_script_and_advisor_option(self):
        tokens = self.choose("canyon", "no")
        self.write_options()
        profile.write_script(str(self.script), tokens)
        seal = self.tmp / "profile.sha256"
        profile.seal(str(self.local), str(seal))
        profile.verify(str(self.local), str(seal))
        options_before, script_before = self.options.read_bytes(), self.script.read_bytes()
        self.options.write_text(json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "Yes"}), encoding="utf-8")
        with self.assertRaises(SystemExit):
            profile.verify(str(self.local), str(seal))
        self.options.write_bytes(options_before)
        self.script.write_text("quickstart-boot canyon yes\n", encoding="utf-8")
        with self.assertRaises(SystemExit):
            profile.verify(str(self.local), str(seal))
        self.script.write_bytes(script_before)
        profile.verify(str(self.local), str(seal))

    def test_frozen_descriptor_request_and_seed_are_not_rewritten(self):
        embark = self.local / "EmbarkModules.xml"
        original = (ROOT / "Harness" / "EmbarkModules.xml").read_bytes()
        embark.write_bytes(original)
        tokens = self.choose("marsh", "no")
        os.environ["TAF_REQUEST"] = "arch-gallery-slice;facing=north;seed=#4242"
        profile.write_request(str(embark))
        frozen = embark.read_bytes()
        self.write_options()
        profile.write_script(str(self.script), tokens)
        self.assertEqual(frozen, embark.read_bytes())
        self.assertIn(b";seed=#4242", frozen)
        self.assertEqual("#4242", profile.validate_seed("#4242"))

    def test_six_save_cli_selections_bind_exact_options_and_single_script_line(self):
        original_bytes = self.source.read_bytes()
        for terrain in ("marsh", "canyon", "dunes"):
            for advisor in ("yes", "no"):
                with self.subTest(terrain=terrain, advisor=advisor):
                    tokens = self.choose(terrain, advisor, "quickstart-save")
                    self.assertEqual([" ".join(tokens)], profile.parse_script(tokens))
                    self.assertEqual(0, profile.main([
                        "scenario_profile.py", "options", str(self.source), str(self.options)
                    ]))
                    self.assertEqual(0, profile.main([
                        "scenario_profile.py", "script", str(self.script), *tokens
                    ]))
                    expected = dict(self.original, OptionEnableSeed="Yes")
                    expected[profile.QUICKSTART_ADVISOR_OPTION] = advisor.title()
                    self.assertEqual(expected, json.loads(self.options.read_text(encoding="utf-8")))
                    self.assertEqual(profile.QUICKSTART_SAVE_SCRIPT_HEADER + " ".join(tokens) + "\n",
                                     self.script.read_text(encoding="utf-8"))
                    self.assertEqual(original_bytes, self.source.read_bytes())

    def test_malformed_or_mixed_save_family_refuses_without_writing(self):
        self.choose(verb="quickstart-save")
        for tokens in (
            ["quickstart-save"], ["quickstart-save", "marsh"],
            ["quickstart-save", "marsh", "yes", "status"],
            ["status", "quickstart-save", "marsh", "yes"],
            ["quickstart-save", "marsh", "yes", "quickstart-boot", "marsh", "yes"],
            ["quickstart-boot", "marsh", "yes", "quickstart-save", "marsh", "yes"],
            ["quickstart-save", "marsh", "yes", "quickstart-save", "dunes", "no"],
        ):
            with self.subTest(tokens=tokens):
                with self.assertRaises(SystemExit):
                    profile.parse_script(tokens, ("quickstart-save", "quickstart-boot"))
                os.environ["TAF_SCENARIO_SCRIPT"] = " ".join(tokens)
                self.assert_options_refuse_unchanged()
                self.assert_script_refuse_unchanged(tokens)
        with self.assertRaises(SystemExit):
            profile.parse_script(["quickstart-save marsh yes"])
        self.assert_script_refuse_unchanged(["quickstart-save marsh yes"])

    def test_save_fields_are_exact_and_cannot_be_provider_registered(self):
        for terrain in ("", "Marsh", "marsh ", "swamp", "canyon\n", "dunes\x00"):
            with self.subTest(terrain=terrain), self.assertRaises(SystemExit):
                profile.parse_script(["quickstart-save", terrain, "yes"])
        for advisor in ("", "Yes", "NO", "1", "true", " yes", "no\n", "yes\x00"):
            with self.subTest(advisor=advisor), self.assertRaises(SystemExit):
                profile.parse_script(["quickstart-save", "marsh", advisor])
        self.assertNotIn("quickstart-save", profile.SCRIPT_VERBS)
        self.assertNotIn("quickstart-save", profile.RESERVED_VERBS)
        with self.assertRaises(SystemExit):
            profile.parse_extra_verbs("quickstart-save")

    def test_save_requires_exact_advisor_override_for_both_writers(self):
        tokens = self.choose(verb="quickstart-save")
        self.write_options()
        for value in (None, "", "no", "Yes", "true", "yes\n"):
            with self.subTest(value=value):
                if value is None:
                    os.environ.pop(profile.QUICKSTART_ADVISOR_ENV, None)
                else:
                    os.environ[profile.QUICKSTART_ADVISOR_ENV] = value
                self.assert_options_refuse_unchanged()
                self.assert_script_refuse_unchanged(tokens)

    def test_save_script_requires_actual_matching_sibling_option(self):
        tokens = self.choose("dunes", "no", "quickstart-save")
        self.assert_script_refuse_unchanged(tokens)
        for raw in ("bad json", "null", "[]", "{}",
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "Yes"}),
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "no"}),
                    json.dumps({profile.QUICKSTART_ADVISOR_OPTION: False})):
            with self.subTest(raw=raw):
                self.options.write_text(raw, encoding="utf-8")
                self.assert_script_refuse_unchanged(tokens)

    def test_save_script_refuses_boot_environment_and_other_selection_mismatches(self):
        tokens = self.choose(verb="quickstart-save")
        self.write_options()
        for script in ("quickstart-boot marsh yes", "quickstart-save dunes yes",
                       "quickstart-save marsh no", "quickstart-save  marsh yes",
                       "quickstart-save marsh yes\n", "quickstart-save\tmarsh yes"):
            with self.subTest(script=script):
                os.environ["TAF_SCENARIO_SCRIPT"] = script
                self.assert_script_refuse_unchanged(tokens)
        self.choose(verb="quickstart-save")
        self.assert_script_refuse_unchanged(["quickstart-boot", "marsh", "yes"])

    def test_save_environment_cannot_fall_back_to_ordinary_default_script(self):
        self.choose(verb="quickstart-save")
        for override_present in (True, False):
            if not override_present:
                del os.environ[profile.QUICKSTART_ADVISOR_ENV]
            self.assert_script_refuse_unchanged([])
            self.assert_script_refuse_unchanged(["status"])

    def test_seal_detects_save_to_boot_change_and_advisor_change(self):
        tokens = self.choose("canyon", "yes", "quickstart-save")
        self.write_options()
        profile.write_script(str(self.script), tokens)
        seal = self.tmp / "save-profile.sha256"
        profile.seal(str(self.local), str(seal))
        profile.verify(str(self.local), str(seal))
        original_script, original_options = self.script.read_bytes(), self.options.read_bytes()
        self.script.write_bytes(original_script.replace(b"quickstart-save", b"quickstart-boot"))
        with self.assertRaises(SystemExit):
            profile.verify(str(self.local), str(seal))
        self.script.write_bytes(original_script)
        self.options.write_text(json.dumps({profile.QUICKSTART_ADVISOR_OPTION: "No"}), encoding="utf-8")
        with self.assertRaises(SystemExit):
            profile.verify(str(self.local), str(seal))
        self.options.write_bytes(original_options)
        profile.verify(str(self.local), str(seal))

    def test_existing_boot_only_parser_does_not_silently_request_save(self):
        self.assertEqual("Yes", profile.parse_quickstart_boot(["quickstart-boot", "marsh", "yes"]))
        with self.assertRaises(SystemExit):
            profile.parse_quickstart_boot(["quickstart-save", "marsh", "yes"])


if __name__ == "__main__":
    unittest.main()
