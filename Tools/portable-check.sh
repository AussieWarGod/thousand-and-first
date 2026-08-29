#!/usr/bin/env bash
# Public CI repository audit. The workflow runs both engine-free .NET suites separately.
# Full compile, installed-base tiles, release checks, and any Qud path remain local-only.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO"

git diff --check
"$REPO/Tools/stage.sh" verify
python3 Tools/check-doc-freshness.py
python3 Tools/check-structure.py --report
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py --repo-root .
python3 Art/check_xml_refs.py --no-base

python3 - <<'PY'
import subprocess
import xml.etree.ElementTree as etree
import json
from pathlib import Path

for relative in subprocess.check_output(["git", "ls-files", "*.xml"], text=True).splitlines():
    etree.parse(relative)

project_paths = [Path("DevTests/PortableTests.csproj"), Path("DevTests/TafTests.csproj")]
lock_path = Path("DevTests/packages.lock.json")
paths = Path("DevTests/Directory.Build.props").read_text(encoding="utf-8")
workflow = Path(".github/workflows/portable.yml").read_text(encoding="utf-8")
global_sdk = json.loads(Path("global.json").read_text(encoding="utf-8"))
if global_sdk != {
    "sdk": {
        "version": "9.0.306",
        "rollForward": "disable",
        "allowPrerelease": False,
    }
}:
    raise SystemExit("global.json must pin exact .NET SDK 9.0.306 with roll-forward disabled")
if "dotnet-version: 9.0.306" not in workflow or "dotnet-version: 9.0.x" in workflow:
    raise SystemExit("portable workflow must install exact .NET SDK 9.0.306")
if "${{ runner.temp }}" in workflow:
    raise SystemExit("portable workflow uses runner.temp in a parse-time-unsafe context")
if "NUGET_PACKAGES: ${{ github.workspace }}/.nuget/packages" not in workflow:
    raise SystemExit("portable workflow lacks its cross-runner checkout-local package cache")
license_text = Path("LICENSE").read_text(encoding="utf-8-sig").strip()
if not license_text.startswith("MIT License\n") or not license_text.endswith("SOFTWARE."):
    raise SystemExit("LICENSE must contain only the canonical MIT license text")
if not Path("NOTICE").is_file():
    raise SystemExit("trademark and distribution notice is missing")
for required in ("'$(MSBuildProjectName)' == 'PortableTests'", "Tools\\PortableOutput\\bin",
                 "Tools\\PortableOutput\\obj"):
    if required not in paths:
        raise SystemExit("portable output isolation is missing: " + required)
for project_path in project_paths:
    project = project_path.read_text(encoding="utf-8")
    for forbidden in ("<Reference", "HintPath", "Caves of Qud", "CoQ_Data", ".dll"):
        if forbidden.lower() in project.lower():
            raise SystemExit(f"{project_path.name} has forbidden game reference: {forbidden}")
    if project.count("<PackageReference Include=\"NUnit\" Version=\"[3.14.0]\" />") != 1:
        raise SystemExit(f"{project_path.name} must pin exactly NUnit [3.14.0]")
    for node in etree.parse(project_path).getroot().findall(".//Compile"):
        include = node.get("Include", "").replace("\\", "/")
        candidate = project_path.parent / include
        if not candidate.is_file():
            raise SystemExit(f"{project_path.name} references missing source: {include}")

taf_tree = etree.parse(Path("DevTests/TafTests.csproj")).getroot()
taf_local = {
    Path(node.get("Include", "").replace("\\", "/")).name
    for node in taf_tree.findall(".//Compile")
    if not node.get("Include", "").startswith("..")
}
expected_tests = {path.name for path in Path("DevTests").glob("*Tests.cs")}
expected_tests.remove("PortableRepositoryRootTests.cs")
missing_tests = sorted(expected_tests - taf_local)
if missing_tests:
    raise SystemExit("TafTests.csproj omits test sources: " + ", ".join(missing_tests))
lock = json.loads(lock_path.read_text(encoding="utf-8"))
dependency = lock["dependencies"]["net9.0"]["NUnit"]
if dependency["requested"] != "[3.14.0, 3.14.0]" or dependency["resolved"] != "3.14.0":
    raise SystemExit("portable lock must resolve exactly NUnit 3.14.0")

staged = subprocess.check_output(["Tools/stage.sh", "list"], text=True).splitlines()
if "LICENSE" not in staged or "NOTICE" not in staged:
    raise SystemExit("runtime package omits LICENSE or NOTICE")
for relative in staged:
    if relative.startswith(("DevTests/", "Harness/", "Tools/", "Art/", "docs/")):
        raise SystemExit("development-only path entered runtime inventory: " + relative)
PY

python3 Tools/check-harness-registration.py

python3 -m unittest discover -s Tools/tests -p '*_test.py'

echo "PORTABLE REPOSITORY AUDIT CLEAN"
