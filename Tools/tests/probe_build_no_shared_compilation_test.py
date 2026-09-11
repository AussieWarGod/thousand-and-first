"""Source-only contract for issue #151: the self-hosted Windows runner's launcher build must
never leave a shared-compilation VBCSCompiler.dll keepalive process as the only surviving
descendant after the build's own process exits, which hung the runner's outer
`Start-Process -Wait` on public run 34577908711 (PID 42240, parent 28720, stopped by hand).

Every `dotnet build`/`dotnet run`/`dotnet publish` invocation made by a script the self-hosted
release.yml jobs launch must disable shared compilation and MSBuild node reuse. This does not
launch processes or dotnet itself; it only pins the exact flags/env vars in source, mirroring
Tools/tests/scenario_process_source_test.py's source-only pattern.
"""

from __future__ import annotations

import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]

# Scripts the self-hosted (taf-steam) release.yml jobs invoke, transitively or directly, that
# could spawn a dotnet build/run/publish process. Verified by grep of .github/workflows/*.yml
# and every Tools/*.ps1/*.sh it calls: only Tools/workshop-steam-upload.ps1 (the launcher
# recompiling its helper on every invocation) contains a dotnet build/run/publish call on that
# path. Tools/test-workshop-release-launcher.ps1 and Tools/run-scenario.ps1 contain none
# (confirmed below), so they are pinned as absent rather than flag-checked.
LAUNCHER_SCRIPT = "workshop-steam-upload.ps1"
NO_DOTNET_BUILD_SCRIPTS = (
    "test-workshop-release-launcher.ps1",
    "run-scenario.ps1",
)

def read(name: str) -> str:
    return (ROOT / "Tools" / name).read_text(encoding="utf-8")


def dotnet_build_invocation(source: str) -> str:
    """Every backtick-continued line starting at '& $Dotnet build', up to and including the
    first line that does NOT end with a line-continuation backtick."""
    lines = source.splitlines()
    for index, line in enumerate(lines):
        if "& $Dotnet build" in line:
            block = [line]
            cursor = index
            while lines[cursor].rstrip().endswith("`"):
                cursor += 1
                block.append(lines[cursor])
            return "\n".join(block)
    raise AssertionError("no '& $Dotnet build' invocation found")


class ProbeBuildNoSharedCompilationTest(unittest.TestCase):
    def test_launcher_dotnet_build_disables_shared_compilation_and_node_reuse(self) -> None:
        invocation = dotnet_build_invocation(read(LAUNCHER_SCRIPT))
        self.assertIn("-p:UseSharedCompilation=false", invocation)
        self.assertIn("-nodeReuse:false", invocation)

    def test_launcher_sets_msbuild_no_node_reuse_env_around_the_build(self) -> None:
        source = read(LAUNCHER_SCRIPT)
        build_index = source.index("& $Dotnet build")
        preceding = source[:build_index]
        self.assertIn("$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'", preceding)
        self.assertIn("$env:MSBUILDDISABLENODEREUSE = '1'", preceding)

    def test_launcher_restores_prior_environment_after_the_build(self) -> None:
        source = read(LAUNCHER_SCRIPT)
        build_index = source.index("& $Dotnet build")
        following = source[build_index:]
        self.assertIn("$env:DOTNET_CLI_USE_MSBUILD_SERVER = $PriorUseMsBuildServer", following)
        self.assertIn("$env:MSBUILDDISABLENODEREUSE = $PriorDisableNodeReuse", following)

    def test_other_self_hosted_path_scripts_have_no_bare_dotnet_build(self) -> None:
        for name in NO_DOTNET_BUILD_SCRIPTS:
            with self.subTest(script=name):
                source = read(name)
                self.assertIsNone(
                    re.search(r"\bdotnet(\.exe)?\s+(build|run|publish)\b", source, re.IGNORECASE),
                    f"{name} unexpectedly invokes dotnet build/run/publish; give it the same "
                    "no-shared-compilation flags if this changes",
                )

    def test_release_workflow_self_hosted_jobs_do_not_invoke_dotnet_directly(self) -> None:
        workflow = (ROOT / ".github" / "workflows" / "release.yml").read_text(encoding="utf-8")
        # The self-hosted (taf-steam) jobs never call `dotnet` directly in a workflow `run:`
        # step; they only launch powershell.exe running the launcher script above, which is
        # covered by the pinned flags. The only literal `dotnet ...` workflow steps are in the
        # portable-tests job, which runs on ubuntu-latest/windows-latest ephemeral GitHub-hosted
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
            # Find where the NEXT top-level job begins to bound this job's own body.
            next_job = re.search(r"\n  [a-zA-Z0-9_-]+:\n", workflow[following:])
            end = following + next_job.start() if next_job else len(workflow)
            body = workflow[start:end]
            self.assertNotRegex(
                body,
                r"run:\s*dotnet\s+(build|run|publish)\b",
                f"self-hosted job {name!r} must not call dotnet directly in a run: step",
            )


if __name__ == "__main__":
    unittest.main()
