#!/usr/bin/env bash
# Public CI audit: engine-free tests plus repository-only inventory/XML/reference checks.
# Full compile, installed-base tiles, release checks, and any Qud path remain local-only.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"

git diff --check
"$REPO/Tools/stage.sh" verify

python3 - <<'PY'
import subprocess
import xml.etree.ElementTree as etree
import json
from pathlib import Path

for relative in subprocess.check_output(["git", "ls-files", "*.xml"], text=True).splitlines():
    etree.parse(relative)

project_path = Path("DevTests/PortableTests.csproj")
lock_path = Path("DevTests/packages.lock.json")
paths = Path("DevTests/Directory.Build.props").read_text(encoding="utf-8")
for required in ("'$(MSBuildProjectName)' == 'PortableTests'", "Tools\\PortableOutput\\bin",
                 "Tools\\PortableOutput\\obj"):
    if required not in paths:
        raise SystemExit("portable output isolation is missing: " + required)
project = project_path.read_text(encoding="utf-8")
for forbidden in ("<Reference", "HintPath", "Caves of Qud", "CoQ_Data", ".dll"):
    if forbidden.lower() in project.lower():
        raise SystemExit("portable project has forbidden game reference: " + forbidden)
if project.count("<PackageReference Include=\"NUnit\" Version=\"[3.14.0]\" />") != 1:
    raise SystemExit("portable project must pin exactly NUnit [3.14.0]")
lock = json.loads(lock_path.read_text(encoding="utf-8"))
dependency = lock["dependencies"]["net9.0"]["NUnit"]
if dependency["requested"] != "[3.14.0, 3.14.0]" or dependency["resolved"] != "3.14.0":
    raise SystemExit("portable lock must resolve exactly NUnit 3.14.0")

staged = subprocess.check_output(["Tools/stage.sh", "list"], text=True).splitlines()
for relative in staged:
    if relative.startswith(("DevTests/", "Tools/", "Art/", "docs/")):
        raise SystemExit("development-only path entered runtime inventory: " + relative)
PY

echo "PORTABLE AUDIT CLEAN"
