import importlib.util
import pathlib
import shutil
import tempfile
from unittest.mock import patch
import subprocess
import sys
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]


def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / "Tools" / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


MANIFEST = load("check-manifest-directories")
ABI = load("check-hearthpyre-abi")


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
            "present-2.2.4",
            "future-version",
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

    def test_reintroducing_version_gate_or_dropping_adapter_fails(self):
        candidates = {"Core/A.cs", "Integrations/Hearthpyre223/Bridge.cs"}
        mutants = [
            [(("Core",), {}), (("Integrations/Hearthpyre223",), {"Hearthpyre": "2.2.4"})],
            [(("Core",), {})],
        ]
        for rows in mutants:
            with self.subTest(rows=rows), patch.object(MANIFEST, "rows", return_value=rows), \
                    patch.object(MANIFEST, "staged_paths", return_value=candidates):
                with self.assertRaises(SystemExit):
                    MANIFEST.main()

    def test_foreign_static_reference_or_reflection_write_cannot_enter_adapter(self):
        with tempfile.TemporaryDirectory(prefix="taf-capability-boundary.") as directory:
            bridge = pathlib.Path(directory)
            for path in ABI.BRIDGE.glob("*.cs"):
                shutil.copyfile(path, bridge / path.name)
            with patch.object(ABI, "BRIDGE", bridge):
                ABI.prove_bridge_boundary()
                for mutant in ("using Hearthpyre;", "Hearthpyre.Realm.Home value;",
                               "field.SetValue(target, value);", "method.Invoke(target, null);",
                               "RealmSystem.Homes.Clear();"):
                    with self.subTest(mutant=mutant):
                        (bridge / "Bad.cs").write_text(mutant, encoding="utf-8")
                        with self.assertRaises(SystemExit):
                            ABI.prove_bridge_boundary()
                        (bridge / "Bad.cs").unlink()

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
