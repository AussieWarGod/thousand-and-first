"""Source-only contract for issue #151: any self-hosted Windows runner dotnet build must never
leave a shared-compilation VBCSCompiler.dll keepalive process as the only surviving descendant
after the build's own process exits, which hung the runner's outer `Start-Process -Wait` twice
on public run 34577908711 -- first at the launcher build (PID 42240, parent 28720, stopped by
hand), then again at the publisher CHECK step (PID 42476, parent gone, stopped by hand).

Every `dotnet build`/`dotnet run`/`dotnet publish` invocation reachable from a self-hosted
(taf-steam) release.yml job -- probe, launcher, and package-time helper builds alike -- must
disable the persistent MSBuild/VBCSCompiler/Razor build servers. This does not launch
processes or dotnet itself; it only pins the exact flags in source, mirroring
Tools/tests/scenario_process_source_test.py's source-only pattern.
"""

from __future__ import annotations

import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]

# Every dotnet build/run/publish invocation site reachable from a self-hosted (taf-steam)
# release.yml job, found by grepping every Tools/*.ps1 for "dotnet" and every
# .github/workflows/*.yml job for `runs-on: [self-hosted` plus what it launches via
# Start-Process. A new site MUST be added here (with its own flag assertion) or this test's
# own site-completeness check below fails -- it is not enough to fix the sites known today.
#
#   Tools/workshop-steam-probe.ps1       -- read-only Steam item probe's compiled helper
#   Tools/workshop-steam-upload.ps1      -- the launcher; recompiles its helper on every
#                                            invocation (probe/submit/verify/finalize all
#                                            route through this one script)
#   Tools/test-workshop-upload.ps1       -- SDK-free local test harness's own helper build
#                                            (already had --disable-build-servers -m:1
#                                            -nr:false before this fix; used as the reference
#                                            convention every other site now matches)
DOTNET_BUILD_SITES = (
    "workshop-steam-probe.ps1",
    "workshop-steam-upload.ps1",
    "test-workshop-upload.ps1",
)
# Scripts confirmed (by grep, re-checked by this test) to invoke NO dotnet build/run/publish
# on the self-hosted path. If one of these ever gains an invocation, this test fails and it
# must move into DOTNET_BUILD_SITES with the same flags.
NO_DOTNET_BUILD_SCRIPTS = (
    "test-workshop-release-launcher.ps1",
    "run-scenario.ps1",
    "workshop-package.sh",
)
REQUIRED_BUILD_FLAGS = ("--disable-build-servers", "-m:1", "-nr:false")


def read(name: str) -> str:
    return (ROOT / "Tools" / name).read_text(encoding="utf-8")


def dotnet_build_invocations(source: str) -> list[str]:
    """Every backtick-continued line starting at '& $Dotnet build', up to and including the
    first line that does NOT end with a line-continuation backtick."""
    lines = source.splitlines()
    blocks: list[str] = []
    index = 0
    while index < len(lines):
        if "& $Dotnet build" in lines[index]:
            block = [lines[index]]
            cursor = index
            while lines[cursor].rstrip().endswith("`"):
                cursor += 1
                block.append(lines[cursor])
            blocks.append("\n".join(block))
            index = cursor
        index += 1
    return blocks


class ProbeBuildNoSharedCompilationTest(unittest.TestCase):
    def test_every_known_dotnet_build_site_disables_shared_compilation(self) -> None:
        for name in DOTNET_BUILD_SITES:
            with self.subTest(script=name):
                invocations = dotnet_build_invocations(read(name))
                self.assertTrue(invocations, f"expected a '& $Dotnet build' invocation in {name}")
                for invocation in invocations:
                    for flag in REQUIRED_BUILD_FLAGS:
                        self.assertIn(
                            flag,
                            invocation,
                            f"{name}'s dotnet build must pass {flag}",
                        )

    def test_site_list_is_complete_across_every_ps1_script(self) -> None:
        """A new 'dotnet build/run/publish' invocation anywhere under Tools/*.ps1 must be
        either an already-covered site above or an explicitly-confirmed absent script; this
        fails the moment a new, unreviewed site appears instead of silently missing it."""
        known = set(DOTNET_BUILD_SITES)
        confirmed_absent = set(NO_DOTNET_BUILD_SCRIPTS)
        for path in sorted((ROOT / "Tools").glob("*.ps1")):
            if re.search(r"\bdotnet(\.exe)?\s+(build|run|publish)\b", path.read_text(encoding="utf-8"), re.IGNORECASE) \
                    or "& $Dotnet build" in path.read_text(encoding="utf-8"):
                self.assertIn(
                    path.name,
                    known,
                    f"{path.name} invokes dotnet build/run/publish but is not in "
                    "DOTNET_BUILD_SITES -- add it with the required flags",
                )
        for name in confirmed_absent:
            if name.endswith(".ps1"):
                source = read(name)
                self.assertIsNone(
                    re.search(r"\bdotnet(\.exe)?\s+(build|run|publish)\b", source, re.IGNORECASE),
                    f"{name} unexpectedly invokes dotnet build/run/publish; move it into "
                    "DOTNET_BUILD_SITES with the same flags",
                )

    def test_workshop_package_sh_has_no_dotnet_invocation(self) -> None:
        source = (ROOT / "Tools" / "workshop-package.sh").read_text(encoding="utf-8")
        self.assertIsNone(
            re.search(r"\bdotnet\s+(build|run|publish)\b", source, re.IGNORECASE),
            "workshop-package.sh unexpectedly invokes dotnet build/run/publish; give it the "
            "same no-shared-compilation flags if this changes",
        )

    def test_release_workflow_self_hosted_jobs_do_not_invoke_dotnet_directly(self) -> None:
        workflow = (ROOT / ".github" / "workflows" / "release.yml").read_text(encoding="utf-8")
        # The self-hosted (taf-steam) jobs never call `dotnet` directly in a workflow `run:`
        # step; they only launch powershell.exe running the launcher/probe scripts above,
        # which are covered by the pinned flags. The only literal `dotnet ...` workflow steps
        # are in the portable-tests job, which runs on ephemeral GitHub-hosted
        # ubuntu-latest/windows-latest runners (destroyed after the job), not the persistent
        # self-hosted runner, so a leaked keepalive there cannot repeat this incident.
        self_hosted_job_names = re.findall(
            r"^  ([a-zA-Z0-9_-]+):\n(?:.*\n)*?    runs-on: \[self-hosted",
            workflow,
            re.MULTILINE,
        )
        self.assertTrue(self_hosted_job_names, "expected at least one self-hosted job")
        for name in self_hosted_job_names:
            start = workflow.index(f"\n  {name}:\n")
            following = workflow.index("\n  ", start + 1)
            next_job = re.search(r"\n  [a-zA-Z0-9_-]+:\n", workflow[following:])
            end = following + next_job.start() if next_job else len(workflow)
            body = workflow[start:end]
            self.assertNotRegex(
                body,
                r"run:\s*dotnet\s+(build|run|publish)\b",
                f"self-hosted job {name!r} must not call dotnet directly in a run: step",
            )

    def test_every_self_hosted_job_launches_only_via_start_process_wait(self) -> None:
        """Every powershell.exe child the self-hosted jobs launch (probe, launcher submit/
        verify/finalize) is waited on synchronously, which is exactly why a leaked keepalive
        descendant hangs the job -- confirming this wiring is unchanged is what makes the
        flag fix in workshop-steam-probe.ps1/workshop-steam-upload.ps1 the actual fix."""
        workflow = (ROOT / ".github" / "workflows" / "release.yml").read_text(encoding="utf-8")
        starts = re.findall(
            r"Start-Process -FilePath 'powershell\.exe'[^\n]*", workflow
        )
        self.assertTrue(starts, "expected at least one Start-Process powershell.exe launch")
        for line in starts:
            # -Wait may be on the same line or a following continuation line; check the whole
            # multi-line statement instead of just this one matched line.
            pass
        # Every such launch statement (spanning its own backtick continuations) must carry
        # -Wait, since that is the exact mechanism the incident hung.
        for match in re.finditer(
            r"Start-Process -FilePath 'powershell\.exe'.*?(?=\n\s*(?:if|\$|\}|#|$))",
            workflow,
            re.DOTALL,
        ):
            self.assertIn("-Wait", match.group(0))


if __name__ == "__main__":
    unittest.main()
