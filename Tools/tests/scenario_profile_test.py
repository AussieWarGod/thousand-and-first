"""Adversarial tests for the scenario profile seal, seed, and derived launcher inputs.

These execute the authoritative implementation in Tools/scenario_profile.py. The PowerShell
launcher mirrors the same closed rule for Windows; its correctness is asserted separately as a
source contract in DevTests/KingdomScenarioLauncherSourceTests.cs.
"""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import shutil
import tempfile
import unittest

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


if __name__ == "__main__":
    unittest.main()
