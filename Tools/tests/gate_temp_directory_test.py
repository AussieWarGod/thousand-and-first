"""Execute real gate allocation; stop at a harmless stage stub, never compile or launch."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

GATE = Path(__file__).resolve().parents[1] / "gate.sh"


class GateTempDirectoryTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-gate-temp-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        tools = self.root / "Tools"
        tools.mkdir()
        self.gate = tools / "gate.sh"
        self.gate.write_text(GATE.read_text())
        self.executable(tools / "stage.sh", "#!/bin/sh\nexit 42\n")
        binaries = self.root / "bin"
        binaries.mkdir()
        self.executable(binaries / "wslpath", '#!/bin/sh\nprintf "%s\\n" "$2"\n')
        game = self.root / "game"
        managed = game / "CoQ_Data" / "Managed"
        managed.mkdir(parents=True)
        (managed / "Assembly-CSharp.dll").write_bytes(b"fixture, never compiled")
        self.private = self.root / "private"
        self.private.mkdir()
        self.env = dict(os.environ, TAF_QUD_ROOT=str(game), TMPDIR=str(self.private),
                        PATH=str(binaries) + os.pathsep + os.environ["PATH"])

    @staticmethod
    def executable(path, text):
        path.write_text(text)
        path.chmod(0o700)

    def run_gate(self):
        return subprocess.run(["bash", str(self.gate), "--keep"], env=self.env,
                              capture_output=True, text=True, timeout=10)

    def test_real_gate_allocates_independent_trees_in_private_parent(self):
        result = self.run_gate()
        self.assertEqual(42, result.returncode, result.stderr)
        paths = [Path(line.split(": ", 1)[1]) for line in result.stdout.splitlines()
                 if line.startswith(("staged tree: ", "dev profile: "))]
        self.assertEqual(2, len(paths), result.stdout)
        self.assertNotEqual(paths[0], paths[1])
        self.assertTrue(paths[0].name.startswith("taf-stage."))
        self.assertTrue(paths[1].name.startswith("taf-devharness."))
        for path in paths:
            self.assertEqual(self.private, path.parent)
            self.assertTrue(path.is_dir())

    def test_invalid_private_parent_refuses_without_falling_back_to_shared_tmp(self):
        self.env["TMPDIR"] = str(self.root / "missing")
        result = self.run_gate()
        self.assertNotEqual(0, result.returncode)
        self.assertNotEqual(42, result.returncode, "must refuse before stage execution")
        self.assertNotIn("staged tree:", result.stdout)
