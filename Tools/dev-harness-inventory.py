#!/usr/bin/env python3
"""Exact compile inventory for the developer scenario profile.

The shipped Workshop inventory and the dev-profile inventory are two different questions, and this
answers the second WITHOUT touching the first. The dev profile is the shipped runtime plus the
excluded `Harness/` overlay, selected by a derived dev manifest that exists only inside a throwaway
profile. Compiling that exact set against the licensed game's references is the only place the
engine-touching harness shards meet a compiler; a public no-Qud test project cannot do it, and a
source-contract string cannot do it at all.

Refuses a missing shard, an extra row, a duplicate, and any harness path that has leaked into the
shipped runtime inventory. Prints the source list so the gate compiles exactly what was validated.

  Tools/dev-harness-inventory.py --check            validate against Tools/stage.sh list
  Tools/dev-harness-inventory.py --stage DIR --out F   emit the compile list for a staged tree
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import stat
import subprocess
import sys
import unicodedata

ROOT = Path(__file__).resolve().parent.parent
HARNESS = ROOT / "Harness"


def staged_rows() -> list[str]:
    listing = subprocess.check_output(
        [str(ROOT / "Tools" / "stage.sh"), "list"], text=True, cwd=str(ROOT)
    )
    return [row for row in listing.splitlines() if row]


def harness_shards() -> list[str]:
    """Every on-disk harness C# shard exactly once, normalized and link-free.

    The one place this rule lives. The scenario profile, the registration checker, and the licensed
    gate all ask this helper rather than keeping a second hand-maintained list that could drift from
    the tree the game would actually compile.
    """
    found: dict[str, str] = {}
    if not HARNESS.is_dir():
        raise SystemExit("harness tree is missing: " + str(HARNESS))
    for path in sorted(HARNESS.iterdir()):
        if path.is_symlink():
            raise SystemExit("harness tree contains a link: " + path.name)
        if not path.is_file() or path.suffix != ".cs":
            continue
        status = path.lstat()
        if status.st_nlink != 1:
            raise SystemExit("harness shard has %d names: %s" % (status.st_nlink, path.name))
        key = path.name.casefold()
        if key in found:
            raise SystemExit(
                "two harness shards normalize to one name: %s and %s" % (found[key], path.name)
            )
        found[key] = path.name
    return sorted(found.values())


def problems_for(
    runtime: list[str], shards: list[str], dev_manifest: dict
) -> list[str]:
    found: list[str] = []
    if not shards:
        found.append(
            "the harness tree holds no C# shard; an empty overlay proves nothing"
        )
    seen = set()
    for name in shards:
        if name in seen:
            found.append("duplicate dev-harness row: " + name)
        seen.add(name)
    for row in runtime:
        if row.startswith("Harness/"):
            found.append(
                "harness path leaked into the shipped runtime inventory: " + row
            )
    paths = []
    for group in dev_manifest.get("Directories", []):
        paths.extend(str(entry) for entry in group.get("Paths", []))
    if "/Harness/" not in paths:
        found.append("the derived dev manifest does not select /Harness/")
    if "/Core/" not in paths:
        found.append("the derived dev manifest does not select /Core/")
    return found


def dev_manifest() -> dict:
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    rows = manifest.get("Directories")
    if not isinstance(rows, list) or not rows:
        raise SystemExit("manifest has no Directories rows")
    paths = rows[0].get("Paths")
    if not isinstance(paths, list) or not paths:
        raise SystemExit("the first Directories row has no Paths")
    if "/Harness/" in paths:
        raise SystemExit("the shipped manifest already selects the harness directory")
    paths.append("/Harness/")
    return manifest


def check() -> int:
    runtime = staged_rows()
    shards = harness_shards()
    found = problems_for(runtime, shards, dev_manifest())
    runtime_cs = [row for row in runtime if row.endswith(".cs")]
    if found:
        for row in found:
            print("dev-harness inventory: " + row, file=sys.stderr)
        return 1
    print(
        "dev-harness compile inventory: %d runtime C# + %d harness shards"
        % (len(runtime_cs), len(shards))
    )
    return 0


# The ONE mode filter. The ordinary gate and the dev gate both read their source list from here, so
# an exclusion can never be changed for one and forgotten for the other. Ordinary baseline compiles
# a clean symbol set and therefore excludes the optional-mod bridge; ordinary compatibility compiles
# it against the tracked ABI stub and therefore includes it.
MODE_EXCLUSIONS = {
    "baseline": ("Integrations/Hearthpyre223/",),
    "compatibility": (),
}

# The overlay is never part of an ORDINARY inventory. Keeping it out here is what makes
# "ordinary sources plus all and only Harness" a checkable statement rather than a description.
OVERLAY = "Harness/"


def validated(paths: list[Path], stage: Path, label: str) -> list[str]:
    """Every compiled path is a normalized, regular, single-named file.

    This list is the exact compile authority, so a link, a second name for the same inode, or two
    paths that normalize together would each let the compiler read bytes the inventory did not
    validate. Checking only the repository-side harness source would leave the staged copy - the
    bytes actually handed to csc - unproven.
    """
    seen: dict[str, str] = {}
    rows: list[str] = []
    for path in paths:
        relative = path.relative_to(stage).as_posix()
        status = path.lstat()
        if stat.S_ISLNK(status.st_mode):
            raise SystemExit("%s contains a link: %s" % (label, relative))
        if not stat.S_ISREG(status.st_mode):
            raise SystemExit("%s contains a non-regular file: %s" % (label, relative))
        if status.st_nlink != 1:
            raise SystemExit(
                "%s entry has %d names: %s" % (label, status.st_nlink, relative)
            )
        key = unicodedata.normalize("NFC", relative).casefold()
        if key in seen:
            raise SystemExit(
                "%s: two paths normalize to one name: %s and %s"
                % (label, seen[key], relative)
            )
        seen[key] = relative
        rows.append(str(path))
    return rows


def walk(stage: Path) -> list[Path]:
    for parent in stage.rglob("*"):
        if parent.is_dir() and parent.is_symlink():
            raise SystemExit(
                "staged tree contains a linked directory: "
                + parent.relative_to(stage).as_posix()
            )
    return sorted(stage.rglob("*.cs"))


def ordinary_sources(stage: Path, mode: str) -> list[Path]:
    """Exactly what the ordinary gate compiles for this mode, overlay excluded."""
    if mode not in MODE_EXCLUSIONS:
        raise SystemExit("unknown compile mode: " + mode)
    excluded = MODE_EXCLUSIONS[mode]
    kept = []
    for path in walk(stage):
        relative = path.relative_to(stage).as_posix()
        if relative.startswith(OVERLAY):
            continue
        if any(relative.startswith(prefix) for prefix in excluded):
            continue
        kept.append(path)
    return kept


def overlay_sources(stage: Path) -> list[Path]:
    """All and only the harness shards, proved against the repository tree."""
    overlay = stage / OVERLAY.rstrip("/")
    expected = harness_shards()
    present = sorted(path.name for path in overlay.glob("*.cs")) if overlay.is_dir() else []
    missing = sorted(set(expected) - set(present))
    extra = sorted(set(present) - set(expected))
    if missing:
        raise SystemExit("dev profile is missing harness shards: " + ", ".join(missing))
    if extra:
        raise SystemExit(
            "dev profile carries unexpected harness shards: " + ", ".join(extra)
        )
    return sorted(overlay / name for name in present)


def emit(stage: str, out: str, mode: str, dev: bool) -> int:
    stage_root = Path(stage)
    if not stage_root.is_dir():
        raise SystemExit("staged tree is missing: " + stage)
    ordinary = ordinary_sources(stage_root, mode)
    label = ("dev" if dev else "ordinary") + " " + mode + " inventory"
    if not dev:
        overlay = stage_root / OVERLAY.rstrip("/")
        if overlay.is_dir():
            raise SystemExit(
                "an ordinary inventory was taken from a tree carrying the harness overlay"
            )
        rows = validated(ordinary, stage_root, label)
        Path(out).write_text("\n".join(rows) + "\n", encoding="utf-8")
        print("%s: %d sources" % (label, len(rows)))
        return 0
    shards = overlay_sources(stage_root)
    rows = validated(sorted(ordinary + shards), stage_root, label)
    Path(out).write_text("\n".join(rows) + "\n", encoding="utf-8")
    print(
        "%s: %d sources (%d ordinary + %d harness shards)"
        % (label, len(rows), len(ordinary), len(shards))
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--list-harness", action="store_true")
    parser.add_argument("--sources", action="store_true")
    parser.add_argument("--dev-sources", action="store_true")
    parser.add_argument("--mode", choices=sorted(MODE_EXCLUSIONS))
    parser.add_argument("--stage")
    parser.add_argument("--out")
    args = parser.parse_args()
    if args.list_harness:
        for name in harness_shards():
            print(name)
        return 0
    if args.check:
        return check()
    if not (args.sources or args.dev_sources):
        raise SystemExit("use --check, --list-harness, --sources, or --dev-sources")
    if not args.stage or not args.out or not args.mode:
        raise SystemExit("--sources/--dev-sources need --stage DIR --mode M --out FILE")
    return emit(args.stage, args.out, args.mode, args.dev_sources)


if __name__ == "__main__":
    sys.exit(main())
