"""The repository-unchanged sweep, importable by BOTH runners.

pytest loads conftest.py; `unittest discover` does not. Keeping the logic here lets the pytest
session fixture and the unittest module hooks share one implementation instead of drifting.
"""

from __future__ import annotations

import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]

IGNORED = (
    "/.git/",
    "/__pycache__/",
    "/.pytest_cache/",
    "/.ruff_cache/",
    "/Tools/PortableOutput/",
    "/.nuget/",
)


def sweep() -> dict:
    rows = {}
    for path in ROOT.rglob("*"):
        relative = "/" + path.relative_to(ROOT).as_posix()
        if any(marker in relative + "/" for marker in IGNORED):
            continue
        if not path.is_file():
            continue
        status = path.lstat()
        rows[relative] = (status.st_mtime, status.st_size)
    return rows


def assert_unchanged(before: dict) -> None:
    after = sweep()
    added = sorted(set(after) - set(before))
    removed = sorted(set(before) - set(after))
    changed = sorted(k for k in set(after) & set(before) if after[k] != before[k])
    problems = []
    if added:
        problems.append("added: " + ", ".join(added[:5]))
    if removed:
        problems.append("removed: " + ", ".join(removed[:5]))
    if changed:
        problems.append("changed: " + ", ".join(changed[:5]))
    if problems:
        raise AssertionError(
            "the test run wrote into the repository (" + "; ".join(problems) + ")"
        )
