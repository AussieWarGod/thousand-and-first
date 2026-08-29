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
import datetime
import hashlib
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

    Subdirectories are REFUSED rather than walked. A non-recursive glob silently dropped
    `Harness/Sub/*.cs`: compiled by nothing, registered nowhere, and guarded by no rule. The tree is
    flat by design, so an explicit refusal is the fail-closed reading - a nested shard stops the
    gate instead of disappearing from it.
    """
    found: dict[str, str] = {}
    if not HARNESS.is_dir():
        raise SystemExit("harness tree is missing: " + str(HARNESS))
    for path in sorted(HARNESS.iterdir()):
        if path.is_symlink():
            raise SystemExit("harness tree contains a link: " + path.name)
        if path.is_dir():
            raise SystemExit(
                "harness tree contains a subdirectory, which no compile route covers: "
                + path.name
            )
        # Case-folded, and here is the fact stated correctly at last, verified by running it:
        # `find -name '*.cs'` is CASE-SENSITIVE and does NOT select Bar.CS, so stage.sh never
        # ships one. The ordinary inventory therefore stays case-sensitive to match what the gate
        # compiles. The overlay is different: the gate copies it from THIS list, not from stage.sh,
        # so folding here surfaces an oddly-cased dev shard instead of leaving it invisible to
        # every route. Two earlier amendments got this backwards in opposite directions.
        if not path.is_file() or path.suffix.casefold() != ".cs":
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
        # Case-folded: a directory named "harness/" escaped both this exclusion and the
        # ordinary-mode guard below, and was counted as an ordinary source.
        if relative.casefold().startswith(OVERLAY.casefold()):
            continue
        if any(relative.startswith(prefix) for prefix in excluded):
            continue
        kept.append(path)
    return kept


def digest(path: Path) -> str:
    sha = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(65536), b""):
            sha.update(block)
    return sha.hexdigest()


def overlay_sources(stage: Path) -> list[Path]:
    """All and only the harness shards, proved against the repository tree BY BYTES.

    Matching filenames is not matching shards: the copy is what a compiler reads, so the overlay is
    compared against the repository content it claims to be, not merely against its directory
    listing.
    """
    overlay = stage / OVERLAY.rstrip("/")
    expected = harness_shards()
    if overlay.is_dir():
        for child in sorted(overlay.iterdir()):
            if child.is_dir():
                raise SystemExit(
                    "the dev overlay carries a subdirectory no compile route covers: " + child.name
                )
    # Folded to match harness_shards(), or a lawfully-named Odd.CS shard is reported "missing"
    # from an overlay that in fact carries it.
    present = sorted(
        path.name for path in overlay.iterdir()
        if path.is_file() and path.suffix.casefold() == ".cs"
    ) if overlay.is_dir() else []
    missing = sorted(set(expected) - set(present))
    extra = sorted(set(present) - set(expected))
    if missing:
        raise SystemExit("dev profile is missing harness shards: " + ", ".join(missing))
    if extra:
        raise SystemExit(
            "dev profile carries unexpected harness shards: " + ", ".join(extra)
        )
    for name in present:
        if digest(overlay / name) != digest(HARNESS / name):
            raise SystemExit("dev overlay shard differs from the repository source: " + name)
    return sorted(overlay / name for name in present)


def emit(stage: str, out: str, mode: str, dev: bool) -> int:
    stage_root = Path(stage)
    if not stage_root.is_dir():
        raise SystemExit("staged tree is missing: " + stage)
    ordinary = ordinary_sources(stage_root, mode)
    label = ("dev" if dev else "ordinary") + " " + mode + " inventory"
    if not dev:
        for child in sorted(stage_root.iterdir()):
            if child.is_dir() and child.name.casefold() == OVERLAY.rstrip("/").casefold():
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


# --------------------------------------------------------------------------------------
# Route receipt
# --------------------------------------------------------------------------------------

RECEIPT_SCHEMA = "taf-dev-harness-receipt-v1"


def inventory_digest(_unused=None) -> str:
    """Content digest over the FULL dev compile inventory, computed from the repository.

    Binds the whole 2600-plus file compile set, not just the 22 overlay shards: a receipt that only
    pinned the shards would still verify after any runtime source changed underneath it. Paths are
    relative and sorted, so the digest is identical in a /tmp stage and in the repo.
    """
    rows = []
    for relative in sorted(staged_rows()):
        if not relative.endswith(".cs"):
            continue
        rows.append((relative, digest(ROOT / relative)))
    for name in harness_shards():
        rows.append((OVERLAY + name, digest(HARNESS / name)))
    rows.sort()
    sha = hashlib.sha256()
    for relative, content in rows:
        sha.update(relative.encode("utf-8"))
        sha.update(b"\x00")
        sha.update(content.encode("ascii"))
        sha.update(b"\x00")
    return sha.hexdigest()


def read_receipt(path: str):
    """The receipt, or None. A stale, malformed, or failing one is None: never a weaker green.

    Every field is read defensively. A receipt is untrusted input like any other durable artifact,
    so a missing key must be a refusal rather than a traceback, and its text is never echoed.
    """
    target = Path(path)
    if not target.is_file():
        return None
    try:
        body = json.loads(target.read_text(encoding="utf-8"))
    except (ValueError, OSError):
        return None
    if not isinstance(body, dict) or body.get("schema") != RECEIPT_SCHEMA:
        return None
    stamped = body.get("recordedUtc")
    if not isinstance(stamped, str):
        return None
    try:
        datetime.datetime.strptime(stamped, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        return None
    if body.get("harnessShards") != harness_shards():
        return None
    if body.get("inventoryDigest") != inventory_digest():
        return None
    modes = body.get("devModes")
    if not isinstance(modes, dict) or sorted(modes) != sorted(MODE_EXCLUSIONS):
        return None
    if any(modes.get(name) != 0 for name in MODE_EXCLUSIONS):
        return None
    if body.get("gateStatus") != 0:
        return None
    return body


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--list-harness", action="store_true")
    parser.add_argument("--sources", action="store_true")
    parser.add_argument("--dev-sources", action="store_true")
    parser.add_argument("--mode", choices=sorted(MODE_EXCLUSIONS))
    parser.add_argument("--inventory-digest", action="store_true")
    parser.add_argument("--stage")
    parser.add_argument("--out")
    args = parser.parse_args()
    if args.list_harness:
        for name in harness_shards():
            print(name)
        return 0
    if args.check:
        return check()
    if args.inventory_digest:
        # Read-only. This PRINTS a digest; it cannot create a receipt. The gate assembles the
        # receipt itself from its own return codes, so no CLI can mint a compile that never ran.
        print(inventory_digest())
        return 0
    if not (args.sources or args.dev_sources):
        raise SystemExit(
            "use --check, --list-harness, --inventory-digest, --sources, or --dev-sources"
        )
    if not args.stage or not args.out or not args.mode:
        raise SystemExit("--sources/--dev-sources need --stage DIR --mode M --out FILE")
    return emit(args.stage, args.out, args.mode, args.dev_sources)


if __name__ == "__main__":
    sys.exit(main())
