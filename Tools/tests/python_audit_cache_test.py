"""Execute the audit bootstrap against an intentionally stale timestamp-based cache."""
import json
import os
from pathlib import Path
import py_compile
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]


class AuditCacheTests(unittest.TestCase):
    def test_audit_ignores_same_size_same_timestamp_bytecode(self):
        with tempfile.TemporaryDirectory(prefix="taf-cache-test-") as directory:
            root = Path(directory)
            source = root / "cache_probe.py"
            source.write_text("VALUE = 'old'\n")
            os.utime(source, (1700000000, 1700000000))
            cache = root / "__pycache__" / ("cache_probe." + sys.implementation.cache_tag + ".pyc")
            cache.parent.mkdir()
            py_compile.compile(str(source), cfile=str(cache), doraise=True,
                               invalidation_mode=py_compile.PycInvalidationMode.TIMESTAMP)
            source.write_text("VALUE = 'new'\n")
            os.utime(source, (1700000000, 1700000000))
            env = dict(os.environ)
            env.pop("PYTHONPYCACHEPREFIX", None)
            env["PYTHONDONTWRITEBYTECODE"] = "1"
            control = subprocess.run([sys.executable, "-c", "import cache_probe; print(cache_probe.VALUE)"],
                                     cwd=root, env=env, text=True, capture_output=True, check=True)
            self.assertEqual("old", control.stdout.strip(), "fixture must prove stale-cache reuse")
            binary = root / "bin"
            binary.mkdir()
            git = binary / "git"
            git.write_text("#!/usr/bin/env python3\n"
                           "import json, os, pathlib, sys\n"
                           "root = pathlib.Path(os.environ['CACHE_TEST_ROOT'])\n"
                           "assert sys.argv[1:] == ['diff', '--check']\n"
                           "sys.path.insert(0, str(root))\n"
                           "import cache_probe\n"
                           "result = dict(value=cache_probe.VALUE, prefix=sys.pycache_prefix, "
                           "disabled=sys.dont_write_bytecode)\n"
                           "(root / 'result.json').write_text(json.dumps(result))\n"
                           "raise SystemExit(42)\n")
            git.chmod(0o700)
            env.update(PATH=str(binary) + os.pathsep + env["PATH"], CACHE_TEST_ROOT=str(root))
            result = subprocess.run(["bash", str(ROOT / "Tools/portable-check.sh")],
                                    env=env, text=True, capture_output=True, timeout=15)
            self.assertEqual(42, result.returncode, result.stdout + result.stderr)
            observed = json.loads((root / "result.json").read_text())
            self.assertEqual("new", observed["value"])
            self.assertTrue(observed["disabled"])
            self.assertIsNotNone(observed["prefix"])
            self.assertFalse(Path(observed["prefix"]).exists(), "empty lookup root must be cleaned")


if __name__ == "__main__":
    unittest.main()
