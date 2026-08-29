#!/usr/bin/env python3
"""Prove Qud's directory selection leaves the optional typed bridge isolated."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys


ROOT = Path(__file__).resolve().parent.parent
BRIDGE = "Integrations/Hearthpyre223"
DEPENDENCY_ID = "Hearthpyre"
DEPENDENCY_VERSION = "2.2.3"
RUNTIME_SUFFIXES = {".cs", ".xml"}


def fail(message: str) -> None:
    raise SystemExit("manifest directory proof failed: " + message)


def canonical_directory(raw: str) -> str:
    if not isinstance(raw, str) or not raw.startswith("/") or not raw.endswith("/"):
        fail("directory path is not rooted and slash-terminated: " + repr(raw))
    value = raw.strip("/")
    path = PurePosixPath(value)
    if not value or path.is_absolute() or ".." in path.parts or str(path) != value:
        fail("directory path is unsafe: " + repr(raw))
    resolved = (ROOT / value).resolve()
    if ROOT.resolve() not in resolved.parents:
        fail("directory path escapes checkout: " + raw)
    return value


def rows() -> list[tuple[tuple[str, ...], dict[str, str]]]:
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    raw_rows = manifest.get("Directories")
    if not isinstance(raw_rows, list) or not raw_rows:
        fail("manifest has no Directories rows")
    result = []
    for index, row in enumerate(raw_rows):
        if not isinstance(row, dict):
            fail(f"Directories[{index}] is not an object")
        raw_paths = row.get("Paths")
        if raw_paths is None and "Path" in row:
            raw_paths = [row["Path"]]
        if not isinstance(raw_paths, list) or not raw_paths:
            fail(f"Directories[{index}] has no paths")
        paths = tuple(canonical_directory(value) for value in raw_paths)
        dependencies = row.get("Dependencies", {})
        if not isinstance(dependencies, dict) or not all(
            isinstance(key, str) and isinstance(value, str)
            for key, value in dependencies.items()
        ):
            fail(f"Directories[{index}] dependencies are malformed")
        result.append((paths, dependencies))
    return result


def staged_paths() -> set[str]:
    output = subprocess.check_output(
        [str(ROOT / "Tools" / "stage.sh"), "list"], cwd=ROOT, text=True
    )
    return {line for line in output.splitlines() if line}


def loader_paths(staged: set[str]) -> set[str]:
    # stage.sh ships only root package metadata plus runtime C#/XML/assets. Qud reads root
    # manifest/config/preview separately; Directories owns every path below a directory.
    return {path for path in staged if "/" in path}


def selected(
    manifest_rows: list[tuple[tuple[str, ...], dict[str, str]]],
    candidates: set[str],
    dependency_version: str | None,
    dependency_enabled: bool,
    dependency_failed: bool,
    dependency_loads_first: bool,
) -> set[str]:
    result = set()
    for paths, dependencies in manifest_rows:
        valid = True
        for dependency_id, required in dependencies.items():
            valid = valid and dependency_id == DEPENDENCY_ID
            valid = valid and dependency_version == required
            valid = valid and dependency_enabled and not dependency_failed
            valid = valid and dependency_loads_first
        if not valid:
            continue
        for directory in paths:
            prefix = directory + "/"
            result.update(path for path in candidates if path.startswith(prefix))
    return result


def digest(paths: set[str]) -> str:
    body = "\n".join(sorted(paths)).encode("utf-8")
    return hashlib.sha256(body).hexdigest()


def main() -> int:
    manifest_rows = rows()
    staged = staged_paths()
    candidates = loader_paths(staged)
    bridge_paths = {path for path in candidates if path.startswith(BRIDGE + "/")}
    common_paths = candidates - bridge_paths
    if not bridge_paths:
        fail("cold install contains no bridge runtime shard")
    foreign_rows = [
        (paths, dependencies)
        for paths, dependencies in manifest_rows
        if dependencies
    ]
    if foreign_rows != [((BRIDGE,), {DEPENDENCY_ID: DEPENDENCY_VERSION})]:
        fail("optional row is not the one exact Hearthpyre 2.2.3 bridge row")
    for paths, dependencies in manifest_rows:
        if not dependencies and any(
            path == "Integrations" or BRIDGE.startswith(path + "/") for path in paths
        ):
            fail("a common path recursively subsumes the bridge")
    matrix = {
        "absent": (None, False, False, False, common_paths),
        "present-2.2.3": (DEPENDENCY_VERSION, True, False, True, candidates),
        "wrong-version": ("2.2.4", True, False, True, common_paths),
        "disabled": (DEPENDENCY_VERSION, False, False, True, common_paths),
        "failed": (DEPENDENCY_VERSION, True, True, True, common_paths),
        "loads-after-taf": (DEPENDENCY_VERSION, True, False, False, common_paths),
    }
    for name, (version, enabled, failed, loads_first, expected) in matrix.items():
        actual = selected(
            manifest_rows, candidates, version, enabled, failed, loads_first
        )
        if actual != expected:
            missing = sorted(expected - actual)[:3]
            extra = sorted(actual - expected)[:3]
            fail(f"{name} differs; missing={missing}, extra={extra}")
        print(f"{name}: {len(actual)} loader files; sha256={digest(actual)}")
    root_runtime = {
        path for path in staged
        if "/" not in path and PurePosixPath(path).suffix.lower() in RUNTIME_SUFFIXES
    }
    if root_runtime:
        fail("runtime C#/XML remains at root and cannot be selected safely: "
             + ", ".join(sorted(root_runtime)))
    selected_union = selected(
        manifest_rows, candidates, DEPENDENCY_VERSION, True, False, True
    )
    if selected_union != candidates:
        fail("present cold install drops current C#/XML/assets loader content")
    print(
        f"cold install: {len(staged)} files; common={len(common_paths)}; "
        f"bridge={len(bridge_paths)}; no loader file dropped"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
