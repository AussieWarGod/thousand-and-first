import pathlib
import subprocess
import sys
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


class OptionalBridgeProofTests(unittest.TestCase):
    def run_tool(self, name, *arguments):
        return subprocess.run(
            [sys.executable, str(ROOT / "Tools" / name), *arguments],
            cwd=ROOT,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        ).stdout

    def test_manifest_dependency_matrix_and_cold_union(self):
        output = self.run_tool("check-manifest-directories.py")
        for state in (
            "absent",
            "present-2.2.3",
            "wrong-version",
            "disabled",
            "failed",
            "loads-after-taf",
        ):
            self.assertIn(state + ":", output)
        self.assertIn("no loader file dropped", output)

    def test_tracked_abi_and_foreign_reference_boundary(self):
        output = self.run_tool("check-hearthpyre-abi.py", "--fixture-only")
        self.assertIn("ABI fixture: clean", output)
        self.assertIn("read-only boundary: clean", output)

    def test_stage_inventory_excludes_local_dependency_cache(self):
        script = (ROOT / "Tools" / "stage.sh").read_text(encoding="utf-8")
        self.assertRegex(script, r"EXCLUDE_DIRS=\([^\n]*\.nuget(?:[ )])")
        output = subprocess.run(
            [str(ROOT / "Tools" / "stage.sh"), "list"],
            cwd=ROOT,
            check=True,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        ).stdout
        self.assertFalse(
            any(path.startswith(".nuget/") for path in output.splitlines()),
            "restored package documentation must never enter the Qud or Workshop payload",
        )


if __name__ == "__main__":
    unittest.main()
