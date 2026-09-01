#!/usr/bin/env python3
"""Public repository/package documentation and Alpha-lane contracts."""

from __future__ import annotations

import json
import re
import subprocess
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class ReleaseReadinessTests(unittest.TestCase):
    def read(self, relative: str) -> str:
        return (ROOT / relative).read_text(encoding="utf-8-sig")

    def staged(self) -> set[str]:
        return set(
            subprocess.check_output(
                [str(ROOT / "Tools/stage.sh"), "list"], cwd=ROOT, text=True
            ).splitlines()
        )

    def test_player_docs_ship_and_have_no_broken_relative_links(self) -> None:
        staged = self.staged()
        public_docs = {
            "README.md",
            "PLAYTESTING.md",
            "SUPPORT.md",
            "CHANGELOG.md",
            "LICENSE",
            "NOTICE",
        }
        self.assertTrue(public_docs <= staged)
        self.assertNotIn("CONTRIBUTING.md", staged)
        self.assertFalse(any(path.startswith("docs/") for path in staged))

        link_pattern = re.compile(r"\[[^]]+\]\(([^)]+)\)")
        for relative in ("README.md", "PLAYTESTING.md", "SUPPORT.md", "CHANGELOG.md"):
            with self.subTest(document=relative):
                for target in link_pattern.findall(self.read(relative)):
                    if target.startswith(("https://", "http://", "#")):
                        continue
                    target_path = target.split("#", 1)[0]
                    self.assertIn(
                        target_path,
                        staged,
                        f"{relative} links non-shipped relative path {target!r}",
                    )

    def test_readme_status_is_single_and_not_a_volatile_census(self) -> None:
        readme = self.read("README.md")
        statuses = [
            line.strip()
            for line in readme.splitlines()
            if line.strip().casefold().startswith("**status:")
        ]
        self.assertEqual(len(statuses), 1)
        self.assertTrue(
            "pre-release source" in statuses[0]
            or re.fullmatch(
                r"\*\*Status: [0-9]+\.[0-9]+\.[0-9]+ public Alpha playtest\.\*\*",
                statuses[0],
            )
        )
        for stale_shape in (
            "static census",
            "cold-install inventory",
            "cases pass",
            "files still breach",
        ):
            self.assertNotIn(stale_shape, readme)

    def test_alpha_example_is_machine_only_and_schema_exact(self) -> None:
        example = json.loads(self.read("docs/ALPHA_CANDIDATE.example.json"))
        self.assertEqual(
            set(example),
            {
                "schemaVersion",
                "releaseChannel",
                "releaseVersion",
                "candidateCommit",
                "gameMarketingVersion",
                "gameCoreBuild",
                "workshopId",
                "previewSha256",
                "privatePackageReceiptSha256",
            },
        )
        self.assertEqual(example["schemaVersion"], 1)
        self.assertEqual(example["releaseChannel"], "v1.0 Alpha")
        forbidden = {"testedBy", "reviewedBy", "verification", "privateSubscription"}
        self.assertFalse(forbidden & set(example))

    def test_public_templates_collect_install_source_and_playtest_feedback(self) -> None:
        for relative in (
            ".github/ISSUE_TEMPLATE/bug.yml",
            ".github/ISSUE_TEMPLATE/compatibility.yml",
        ):
            with self.subTest(template=relative):
                text = self.read(relative)
                self.assertIn("id: install_source", text)
                self.assertIn("manifest ID r_ThousandAndFirst", text)
        playtest = self.read(".github/ISSUE_TEMPLATE/playtest.yml")
        for required in (
            "Alpha playtest feedback",
            "id: taf_version_source",
            "id: entry_path",
            "id: session",
            "id: feedback",
            "id: safety",
        ):
            self.assertIn(required, playtest)

    def test_ci_and_package_help_expose_explicit_alpha_lane(self) -> None:
        workflow = self.read(".github/workflows/portable.yml")
        self.assertIn("workflow_dispatch:", workflow)
        help_text = subprocess.check_output(
            [str(ROOT / "Tools/workshop-package.sh"), "--help"],
            cwd=ROOT,
            text=True,
        )
        self.assertIn("--alpha", help_text)
        self.assertIn("final human release-evidence record", help_text)

    def test_release_check_modes_and_git_guard_execute_in_fixtures(self) -> None:
        output = subprocess.check_output(
            [str(ROOT / "Tools/test-release-check.sh")], cwd=ROOT, text=True
        )
        self.assertIn("RELEASE CHECK HARNESS CLEAN", output)

    def test_release_docs_route_private_and_public_checks_explicitly(self) -> None:
        releasing = self.read("docs/RELEASING.md")
        alpha_plan = self.read("docs/ALPHA-RELEASE-PLAN.md")
        self.assertIn("./Tools/release-check.sh --test", releasing)
        self.assertIn("./Tools/release-check.sh --alpha", releasing)
        self.assertIn("./Tools/release-check.sh --release", releasing)
        self.assertIn("./Tools/release-check.sh --test", alpha_plan)
        self.assertIn("./Tools/release-check.sh --alpha", alpha_plan)

    def test_preview_provenance_source_is_not_executable(self) -> None:
        mode = (ROOT / "docs/release-evidence/preview-source.png").stat().st_mode
        self.assertEqual(mode & 0o111, 0)


if __name__ == "__main__":
    unittest.main()
