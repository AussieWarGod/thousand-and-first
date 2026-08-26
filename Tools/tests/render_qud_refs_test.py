import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools"))
SPEC = importlib.util.spec_from_file_location(
    "render_qud_refs", ROOT / "Tools" / "render-qud-refs.py"
)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class RenderQudReferencesTests(unittest.TestCase):
    def fixture(self, directory: Path) -> tuple[str, Path]:
        managed = directory / "CoQ_Data" / "Managed"
        managed.mkdir(parents=True)
        for name in ("Assembly-CSharp.dll", "Another.dll"):
            (managed / name).write_bytes(b"fixture")
        template = (
            "\ufeff-nologo\n"
            "-define:VERSION_0_0;BUILD_0_0_0;MOD_ALPHA;MOD_BETA\n"
            '-r:"F:\\Old\\Assembly-CSharp.dll"\n'
            '-r:"F:\\Old\\Another.dll"\n'
        )
        return template, managed

    def test_baseline_uses_release_symbols_and_local_reference_root(self):
        with tempfile.TemporaryDirectory() as raw:
            template, managed = self.fixture(Path(raw))
            rendered = MODULE.render(template, managed, r"D:\Games\Qud\CoQ_Data\Managed", "baseline")
        self.assertIn("-define:VERSION_1_0;BUILD_2_0_211\n", rendered)
        self.assertNotIn("MOD_ALPHA", rendered)
        self.assertIn(r'-r:"D:\Games\Qud\CoQ_Data\Managed\Assembly-CSharp.dll"', rendered)

    def test_compatibility_retains_mod_symbols_but_replaces_stale_build_symbols(self):
        with tempfile.TemporaryDirectory() as raw:
            template, managed = self.fixture(Path(raw))
            rendered = MODULE.render(template, managed, r"E:\Qud\CoQ_Data\Managed", "compatibility")
        self.assertIn("-define:VERSION_1_0;BUILD_2_0_211;MOD_ALPHA;MOD_BETA\n", rendered)
        self.assertNotIn("VERSION_0_0", rendered)
        self.assertNotIn("BUILD_0_0_0", rendered)

    def test_missing_reference_refuses_before_writing_a_partial_contract(self):
        with tempfile.TemporaryDirectory() as raw:
            template, managed = self.fixture(Path(raw))
            (managed / "Another.dll").unlink()
            with self.assertRaisesRegex(MODULE.ReferenceError, "Another.dll"):
                MODULE.render(template, managed, r"D:\Qud\Managed", "baseline")


if __name__ == "__main__":
    unittest.main()
