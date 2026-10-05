"""#283 heal route host tests: fake effects and synthetic trees only, never native acceptance."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import persona_reload_heal as heal
from personas.persona_matrix import parse_manifest, assess

MANIFEST = "REQUEST=founding-first-city\nSCRIPT=reload-descendant renovate-heal 8.22@40,12\nEXPECT=RELOAD-COMPLETE\n"


class Fake:
    def __init__(self, fault="", change=None):
        self.events, self.fault, self.change = [], fault, change

    def event(self, name):
        self.events.append(name)
        if name == self.fault:
            raise ValueError("injected " + name)

    def idle(self, phase): self.event("idle-" + phase)
    def fresh(self, phase):
        self.event("fresh-" + phase)
        return "source" if phase == "save" else "destination"
    def prepare(self, *args): self.event("prepare")
    def launch(self, root, phase): self.event("launch-" + phase)
    def wait(self, root, phase): self.event("wait-" + phase)
    def stop(self, root, phase): self.event("stop-" + phase)
    def transport(self, *args): self.event("transport")

    def check(self, root, phase):
        self.event("check-" + phase)
        result = dict(verdict="PASS", phase=phase, gameId="game-id", seed="#922453088",
                      command="renovate-heal 8.22@40,12", defect="A")
        if phase == "save":
            result["saveHashes"] = {"Primary.sav.gz": "stuck", "Primary.json": "info", "Cache.db": "cache"}
            result["stallSealReason"] = "stall reading"
        else:
            result.update(importedSaveHashes={"Primary.sav.gz": "stuck", "Primary.json": "info"},
                          secondSaveHashes={"Primary.sav.gz": "healed", "Primary.json": "healed-info"},
                          backupSaveHash="stuck", readmittedDefect="A", healedAfterLoad=True, crossBuild=True,
                          preHealSealReasons=["stall reading"])
            if self.change:
                self.change(result)
        return result


class HealOrchestrationTests(unittest.TestCase):
    def test_owned_effect_order_and_explicit_limits(self):
        fake = Fake()
        result = heal.execute_renovate_heal(fake, "8.22@40,12")
        self.assertEqual(fake.events, ["idle-initial", "fresh-save", "prepare", "launch-save",
            "wait-save", "check-save", "stop-save", "idle-between", "fresh-load", "transport",
            "launch-load", "wait-load", "check-load", "stop-load", "idle-final"])
        self.assertEqual(result["scope"], "developer-renovate-heal-cold-reload")
        for flag in ("healedAfterLoad", "secondRealSave", "crossBuild"):
            self.assertIs(result[flag], True)
        for flag in ("sameProfileDirectory", "scriptResumed", "gracefulQuit", "ordinaryAcceptance", "releaseAcceptance"):
            self.assertIs(result[flag], False)

    def test_every_failed_stage_refuses_and_cleans_the_active_process(self):
        events = Fake()
        heal.execute_renovate_heal(events, "8.22@40,12")
        for fault in events.events:
            with self.subTest(fault=fault):
                fake = Fake(fault)
                with self.assertRaises(ValueError): heal.execute_renovate_heal(fake, "8.22@40,12")
                if fault.startswith(("launch-", "wait-", "check-", "stop-")):
                    self.assertEqual(fake.events[-1], "stop-failure")

    def test_drifted_identity_defect_bytes_or_heal_cannot_pass(self):
        changes = [lambda r, key=key: r.update({key: "foreign"}) for key in ("gameId", "seed", "command")]
        changes += [lambda r: r.update(readmittedDefect="B"), lambda r: r.pop("readmittedDefect"),
                    lambda r: r["importedSaveHashes"].update({"Primary.sav.gz": "other"}),
                    lambda r: r.update(backupSaveHash="other"),
                    lambda r: r["secondSaveHashes"].update({"Primary.sav.gz": "stuck"}),
                    lambda r: r.update(healedAfterLoad=False), lambda r: r.pop("crossBuild"),
                    lambda r: r.update(preHealSealReasons=["stall reading", "another reading"])]
        for change in changes:
            fake = Fake(change=change)
            with self.assertRaises((ValueError, KeyError)): heal.execute_renovate_heal(fake, "8.22@40,12")
            self.assertEqual(fake.events[-1], "stop-failure")

    def test_noncanonical_start_refuses_before_any_effect(self):
        for start in ("8.22", "08.22@40,12", "8.22@40,25", "", None):
            with self.subTest(start=start):
                fake = Fake()
                with self.assertRaises(ValueError): heal.execute_renovate_heal(fake, start)
                self.assertEqual(fake.events, [])

    def test_manifest_normalizes_session_one_exactly(self):
        manifest = parse_manifest(MANIFEST, "test")
        self.assertEqual(manifest["RELOAD"], "renovate-heal")
        self.assertEqual(manifest["SCRIPT_WORDS"], heal.SCRIPT_WORDS)
        self.assertEqual(manifest["VERBS"], heal.VERBS)
        self.assertEqual(manifest["START"], "8.22@40,12")
        self.assertTrue(assess(manifest, "", "test"), "a journal alone never grants a reload pass")
        for text in (MANIFEST.replace("8.22@40,12", "8.22"), MANIFEST.replace("12", "12 extra"),
                     MANIFEST + "VERBS=renovate-heal-save\n", MANIFEST + 'LOG_EXPECT=["x"]\n',
                     MANIFEST.replace("RELOAD-COMPLETE", "COMPLETE")):
            with self.subTest(text=text), self.assertRaises(SystemExit): parse_manifest(text, "test")


class HealBackendTests(unittest.TestCase):
    def backend(self, seed=""):
        return heal.RenovateHealNativeBackend(TOOLS, Path("/licensed/CoQ.exe"), Path("/evidence"), 600,
                                              seed, Path("/unfixed"))

    def test_session_one_is_prepared_from_the_unfixed_tree_with_the_default_seed(self):
        backend = self.backend()
        calls = []
        backend.command = lambda name, args, env=None: calls.append((name, args, env))
        with patch.dict("os.environ", {"TAF_SCENARIO_QUICKSTART_ADVISOR": "yes", "TAF_SCENARIO_ROLE": "x"}):
            backend.prepare(Path("/source"), "8.22@40,12")
        name, args, env = calls[0]
        self.assertEqual(name, "prepare-save")
        self.assertEqual(args, ["bash", "/unfixed/Tools/prepare-scenario.sh", "/source", "#922453088"])
        self.assertNotIn("TAF_SCENARIO_QUICKSTART_ADVISOR", env)
        self.assertNotIn("TAF_SCENARIO_ROLE", env)
        self.assertEqual((env["TAF_REQUEST"], env["TAF_SCENARIO_SCRIPT"], env["TAF_SCENARIO_START"],
                          env["TAF_SCENARIO_EXTRA_VERBS"], env["TAF_QUD_ROOT"]),
                         ("founding-first-city", heal.SCRIPT_WORDS, "8.22@40,12", heal.VERBS, "/licensed"))
        with self.assertRaises(ValueError): backend.prepare(Path("/source"), "8.22")

    def test_transport_prepares_a_fixed_runtime_donor_then_copies_across_builds(self):
        backend = self.backend("#4242")
        calls = []
        backend.command = lambda name, args, env=None: calls.append((name, args, env))
        backend.fresh = lambda phase: calls.append(("fresh-" + phase, None, None)) or Path("/mnt/c/taf-scenario.R")
        backend.prepare(Path("/mnt/c/taf-scenario.S"), "8.22@40,12")
        backend.transport(Path("/mnt/c/taf-scenario.S"), Path("/mnt/c/taf-scenario.D"))
        self.assertEqual([call[0] for call in calls], ["prepare-save", "fresh-runtime", "prepare-runtime", "transport"])
        self.assertEqual(calls[2][1], ["bash", str(TOOLS / "prepare-scenario.sh"), "/mnt/c/taf-scenario.R", "#4242"])
        self.assertEqual(calls[2][2]["TAF_SCENARIO_SCRIPT"], heal.SCRIPT_WORDS)
        self.assertEqual(calls[3][1], ["python3", str(TOOLS / "prepare-scenario-load.py"), "--runtime",
                                       "/mnt/c/taf-scenario.R", "/mnt/c/taf-scenario.S", "/mnt/c/taf-scenario.D"])

    def test_both_verdicts_come_from_the_strict_heal_checker(self):
        backend = self.backend()

        class Json:
            def read_text(self, **kwargs): return '{"verdict":"PASS"}'
        with patch.object(backend, "command", return_value=Json()) as command:
            backend.check(Path("/destination"), "load")
            args = command.call_args.args
        self.assertEqual(args[1], ["python3", str(TOOLS / "check-renovate-heal-results.py"),
                                   "/destination", "--phase", "load"])
        self.assertEqual(args[2]["TAF_LOG_ALLOW"], "")

    def test_wait_wakes_only_on_the_terminal_or_a_refusal(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            backend = heal.RenovateHealNativeBackend(TOOLS, Path("/licensed/CoQ.exe"), root, 1, "", root)
            journal = root / "scenario-journal.tsv"
            for row in ("x\tSCRIPT-COMPLETE\tOK\tdone\n", "x\trenovate-heal-save\tREFUSED\tno\n"):
                journal.write_text(row, encoding="utf-8")
                backend.wait(root, "save")
            journal.write_text("x\trenovate-heal-save\tOK\trenovate-heal-save=true\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "timed out"): backend.wait(root, "load")

    def test_cli_requires_and_proves_the_unfixed_tree_before_any_backend(self):
        source = (TOOLS / "run-persona-reload.py").read_text(encoding="utf-8")
        proof = source.index("validate_source_tree(Path(source_tree), tools.parent)")
        self.assertLess(source.index('os.environ.get("TAF_RELOAD_SOURCE_TREE", "")'), proof)
        self.assertLess(proof, source.index("RenovateHealNativeBackend(tools, args.game.resolve()"))
        self.assertIn('execute_renovate_heal(backend, manifest["START"])', source)


class SourceTreeTests(unittest.TestCase):
    """Synthetic git histories: session one's tree must be the branch's own detector commit, an
    earlier clean commit of the fixed tree, never a separate copy or a working tree with edits."""

    ENV = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@example.invalid",
               GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@example.invalid")

    def git(self, root, *args):
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-C", str(root), *args], check=True,
                       capture_output=True, env=self.ENV)

    def write(self, root, manifest, harness=b"class H {}\n", tools=b"# tool\n", notes="notes\n",
              host_test=b"# host test\n"):
        (root / "Tools" / "tests").mkdir(parents=True, exist_ok=True)
        (root / "Harness").mkdir(exist_ok=True)
        (root / "Tools" / "tests" / "heal_test.py").write_bytes(host_test)
        (root / "Tools" / "stage.sh").write_text('#!/usr/bin/env bash\ncat "$(dirname "$0")/../manifest.txt"\n')
        (root / "Tools" / "prepare-scenario.sh").write_bytes(tools)
        (root / "Harness" / "KingdomH.cs").write_bytes(harness)
        (root / "notes.txt").write_text(notes)
        (root / "manifest.txt").write_text("".join(digest + "  " + path + "\n"
                                                   for path, digest in sorted(manifest.items())))

    def commit(self, root, message):
        self.git(root, "add", "-A")
        self.git(root, "commit", "-q", "--allow-empty", "-m", message)

    def history(self, base, source, fixed, **source_files):
        """One repository: the detector state committed first, the fixed state on top. The
        detector is checked out as a separate detached worktree, as the native lane snapshots it."""
        tree = Path(base) / "fixed"
        tree.mkdir()
        self.git(tree, "init", "-q")
        self.write(tree, source, **source_files)
        self.commit(tree, "detector")
        detector = Path(base) / "detector"
        self.git(tree, "worktree", "add", "-q", "--detach", str(detector), "HEAD")
        self.write(tree, fixed)
        self.commit(tree, "fix")
        return detector, tree

    def manifests(self, release=False):
        fixed = {"Core/A.cs": "a" * 64}
        fixed.update({path: "f" * 64 for path in heal.FIX_NEW + heal.FIX_CHANGED})
        fixed.update({path: "e" * 64 for path in heal.RELEASE_METADATA})
        source = {path: digest for path, digest in fixed.items() if path not in heal.FIX_NEW}
        source.update({path: "0" * 64 for path in heal.FIX_CHANGED})
        if release:
            source.update({path: "d" * 64 for path in heal.RELEASE_METADATA})
        return fixed, source

    def test_the_detector_commit_with_exactly_the_fix_passes(self):
        fixed, source = self.manifests()
        with tempfile.TemporaryDirectory() as base:
            detector, tree = self.history(base, source, fixed)
            proof = heal.validate_source_tree(detector, tree)
            head = subprocess.run(["git", "-C", str(detector), "rev-parse", "HEAD"],
                                  capture_output=True, text=True, check=True).stdout.strip()
        self.assertEqual(proof["runtimeDelta"], sorted(heal.FIX_NEW + heal.FIX_CHANGED))
        self.assertEqual(proof["releaseMetadataDelta"], [])
        self.assertEqual(proof["sourceCommit"], head)
        self.assertIs(proof["harnessIdentical"], True)
        self.assertIs(proof["toolsIdentical"], True)

    def test_the_release_metadata_may_also_differ(self):
        fixed, source = self.manifests(release=True)
        with tempfile.TemporaryDirectory() as base:
            proof = heal.validate_source_tree(*self.history(base, source, fixed))
        self.assertEqual(proof["runtimeDelta"], sorted(heal.FIX_NEW + heal.FIX_CHANGED + heal.RELEASE_METADATA))
        self.assertEqual(proof["releaseMetadataDelta"], sorted(heal.RELEASE_METADATA))

    def test_host_tests_alone_may_differ(self):
        """Neither session runs Tools/tests; every other tool must still be identical."""
        fixed, source = self.manifests()
        with tempfile.TemporaryDirectory() as base:
            proof = heal.validate_source_tree(*self.history(base, source, fixed, host_test=b"# older test\n"))
        self.assertIs(proof["toolsIdentical"], True)

    def test_any_other_difference_refuses(self):
        fixed, source = self.manifests()
        extra = dict(source, **{"Core/A.cs": "b" * 64})
        kept = dict(source, **{heal.FIX_NEW[0]: "f" * 64})
        unrestored = dict(source, **{heal.FIX_CHANGED[0]: "f" * 64})
        only_release = dict(unrestored, **{path: "d" * 64 for path in heal.RELEASE_METADATA})
        missing = {path: digest for path, digest in source.items() if path != "manifest.json"}
        for changed, kwargs, pattern in ((extra, {}, "outside exactly"), (kept, {}, "outside exactly"),
                                         (unrestored, {}, "outside exactly"),
                                         (only_release, {}, "outside exactly"),
                                         (missing, {}, "release metadata file"),
                                         (source, {"harness": b"class Other {}\n"}, "harness"),
                                         (source, {"tools": b"# other\n"}, "tools")):
            with self.subTest(pattern=pattern), tempfile.TemporaryDirectory() as base:
                with self.assertRaisesRegex(ValueError, pattern):
                    heal.validate_source_tree(*self.history(base, changed, fixed, **kwargs))
        with tempfile.TemporaryDirectory() as base:
            _, fixed_tree = self.history(base, source, fixed)
            for candidate in (fixed_tree, Path("relative/tree")):
                with self.subTest(candidate=candidate), self.assertRaisesRegex(ValueError, "separate TAF checkout"):
                    heal.validate_source_tree(candidate, fixed_tree)

    def test_only_a_clean_earlier_commit_of_this_history_passes(self):
        fixed, source = self.manifests()
        with tempfile.TemporaryDirectory() as base:
            detector, tree = self.history(base, source, fixed)
            copy = Path(base) / "copy"
            copy.mkdir()
            self.git(copy, "init", "-q")
            self.write(copy, source)
            self.commit(copy, "detector copy")
            with self.assertRaisesRegex(ValueError, "earlier commit"):
                heal.validate_source_tree(copy, tree)
            sibling = Path(base) / "sibling"
            self.git(tree, "worktree", "add", "-q", "--detach", str(sibling), "HEAD~1")
            self.write(sibling, source, notes="a sibling commit\n")
            self.commit(sibling, "sibling")
            with self.assertRaisesRegex(ValueError, "earlier commit"):
                heal.validate_source_tree(sibling, tree)
            plain = Path(base) / "plain"
            plain.mkdir()
            self.write(plain, source)
            with self.assertRaisesRegex(ValueError, "no committed HEAD"):
                heal.validate_source_tree(plain, tree)
            (detector / "notes.txt").write_text("an uncommitted edit\n")
            with self.assertRaisesRegex(ValueError, "uncommitted changes"):
                heal.validate_source_tree(detector, tree)


if __name__ == "__main__":
    unittest.main()
