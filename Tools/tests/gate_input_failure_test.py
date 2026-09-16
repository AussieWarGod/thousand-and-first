"""Inventory/ref failures must stop compilation even when bash errexit is suppressed."""
from pathlib import Path
import subprocess
import tempfile
import unittest

GATE = Path(__file__).resolve().parents[1] / "gate.sh"

class GateInputFailureTest(unittest.TestCase):
    def test_failed_or_empty_inventory_never_reaches_compiler(self):
        source = GATE.read_text()
        for function in ("compile_mode", "compile_dev_harness"):
            body = source.split(function + "() {", 1)[1].split("\n}\n", 1)[0]
            for failure in ("render", "inventory", "empty"):
                if function == "compile_dev_harness" and failure == "render":
                    continue
                with self.subTest(function=function, failure=failure), tempfile.TemporaryDirectory() as root:
                    script = r'''set -euo pipefail
REPO="$1"; STAGE="$1"; DEV="$1"; MANAGED="$1"; MANAGED_WIN="$1"
failure="$2"
python3() {
    if [[ "$*" == *render-qud-refs* ]]; then
        [ "$failure" != render ]; return
    fi
    [ "$failure" != inventory ] || return 42
    local target="${@: -1}"
    if [ "$failure" = empty ]; then : > "$target"; else echo fixture.cs > "$target"; fi
}
unc() { printf '%s' "$1"; }
powershell.exe() { echo COMPILER_REACHED; return 0; }
''' + function + "() {" + body + "\n}\n" + function + " baseline && exit 0 || exit 17\n"
                    result = subprocess.run(["bash", "-c", script, "fixture", root, failure],
                                            text=True, capture_output=True, timeout=10)
                    self.assertEqual(17, result.returncode, result.stdout + result.stderr)
                    self.assertNotIn("COMPILE CLEAN", result.stdout)
                    self.assertNotIn("COMPILER_REACHED", result.stdout)
