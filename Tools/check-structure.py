#!/usr/bin/env python3
"""Census staged C# structure and enforce Addendum 9 at release time.

Report mode is diagnostic and always succeeds after a valid census. Release mode
enforces the conservative, mechanically provable proxy (every staged production
C# file is strictly under 300 physical lines) and requires exact-inventory human
review evidence for the two semantic requirements automation cannot prove.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from typing import Iterable, Sequence


LINE_LIMIT = 300
REVIEW_SCHEMA = 1
DEFAULT_REVIEW_LEDGER = "docs/STRUCTURE_REVIEW.json"
XRL_IMPORT = re.compile(r"^\s*using\s+XRL(?:\.|;)", re.MULTILINE)
REVIEW_KEYS = {
    "schemaVersion", "inventorySha256", "exceptions", "reviewedBy", "completedUtc",
    "oneResponsibility", "protocolsAtBoundaries",
}
REVIEW_SECTION_KEYS = {"status", "notes"}
HUMAN_SENTINEL = re.compile(
    r"(?:^|[^a-z0-9])(?:placeholder|example|todo|tbd|unknown|n\s*/\s*a)"
    r"(?:$|[^a-z0-9])|human[_ -]*(?:reviewer|tester)|name[_ -]*the|"
    r"replace[_ -]*with|your[_ -]*name",
    re.IGNORECASE,
)


class StructureError(ValueError):
    """Invalid inventory, source, or review evidence."""


@dataclass(frozen=True)
class FileStat:
    path: str
    lines: int
    imports_xrl: bool
    sha256: str


@dataclass(frozen=True)
class Census:
    files: tuple[FileStat, ...]
    inventory_sha256: str

    @property
    def physical_lines(self) -> int:
        return sum(item.lines for item in self.files)

    @property
    def at_or_over_limit(self) -> tuple[FileStat, ...]:
        return tuple(item for item in self.files if item.lines >= LINE_LIMIT)

    @property
    def over_limit(self) -> tuple[FileStat, ...]:
        return tuple(item for item in self.files if item.lines > LINE_LIMIT)

    @property
    def exactly_at_limit(self) -> tuple[FileStat, ...]:
        return tuple(item for item in self.files if item.lines == LINE_LIMIT)

    def over(self, threshold: int) -> tuple[FileStat, ...]:
        return tuple(item for item in self.files if item.lines > threshold)

    @property
    def direct_xrl_imports(self) -> tuple[FileStat, ...]:
        return tuple(item for item in self.files if item.imports_xrl)

    @property
    def large_direct_xrl_imports(self) -> tuple[FileStat, ...]:
        return tuple(
            item
            for item in self.files
            if item.lines >= LINE_LIMIT and item.imports_xrl
        )


def _physical_line_count(payload: bytes, relative: str) -> int:
    try:
        source = payload.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise StructureError(f"runtime C# source is not UTF-8: {relative}") from error
    return len(source.splitlines())


def _safe_source(root: Path, relative: str) -> Path:
    pure = PurePosixPath(relative)
    if (
        not relative
        or pure.is_absolute()
        or "\\" in relative
        or any(part in ("", ".", "..") for part in pure.parts)
        or pure.suffix.lower() != ".cs"
    ):
        raise StructureError(f"invalid runtime C# inventory path: {relative!r}")
    candidate = root.joinpath(*pure.parts)
    if candidate.is_symlink() or not candidate.is_file():
        raise StructureError(f"runtime C# source is not a regular file: {relative}")
    try:
        candidate.resolve(strict=True).relative_to(root.resolve(strict=True))
    except ValueError as error:
        raise StructureError(f"runtime C# source escapes repository: {relative}") from error
    return candidate


def stage_inventory(root: Path) -> list[str]:
    stage = root / "Tools" / "stage.sh"
    if not stage.is_file() or stage.is_symlink():
        raise StructureError(f"canonical runtime inventory is unavailable: {stage}")
    try:
        completed = subprocess.run(
            [str(stage), "list"],
            cwd=root,
            check=True,
            capture_output=True,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        detail = getattr(error, "stderr", "") or str(error)
        raise StructureError(f"cannot read canonical runtime inventory: {detail.strip()}") from error
    return [row for row in completed.stdout.splitlines() if row.endswith(".cs")]


def inventory_file(path: Path) -> list[str]:
    if not path.is_file() or path.is_symlink():
        raise StructureError(f"inventory file is not a regular file: {path}")
    return [row for row in path.read_text(encoding="utf-8-sig").splitlines() if row]


def build_census(root: Path, relatives: Iterable[str]) -> Census:
    root = root.resolve(strict=True)
    rows = list(relatives)
    if not rows:
        raise StructureError("runtime C# inventory is empty")
    if len(rows) != len(set(rows)):
        raise StructureError("runtime C# inventory contains duplicate paths")

    stats: list[FileStat] = []
    digest = hashlib.sha256()
    for relative in sorted(rows):
        path = _safe_source(root, relative)
        payload = path.read_bytes()
        source_sha = hashlib.sha256(payload).hexdigest()
        try:
            source = payload.decode("utf-8-sig")
        except UnicodeDecodeError as error:
            raise StructureError(f"runtime C# source is not UTF-8: {relative}") from error
        stats.append(
            FileStat(
                path=relative,
                lines=_physical_line_count(payload, relative),
                imports_xrl=bool(XRL_IMPORT.search(source)),
                sha256=source_sha,
            )
        )
        digest.update(relative.encode("utf-8"))
        digest.update(b"\0")
        digest.update(source_sha.encode("ascii"))
        digest.update(b"\n")
    return Census(tuple(stats), digest.hexdigest())


def _utc_timestamp(value: object) -> bool:
    if (not isinstance(value, str)
            or re.fullmatch(
                r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z",
                value,
            ) is None):
        return False
    try:
        datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        return False
    return True


def _human_text(value: object, minimum: int, maximum: int) -> bool:
    return (
        isinstance(value, str)
        and value == value.strip()
        and minimum <= len(value) <= maximum
        and value.isprintable()
        and HUMAN_SENTINEL.search(value) is None
    )


def review_issues(path: Path, census: Census) -> list[str]:
    if not path.is_file() or path.is_symlink():
        return [f"exact-inventory semantic review is missing: {path}"]
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        return [f"semantic review is unreadable: {path}: {error}"]
    if not isinstance(payload, dict):
        return ["semantic review root must be an object"]

    issues: list[str] = []
    if set(payload) != REVIEW_KEYS:
        issues.append("semantic review fields must exactly match schema version 1")
    if type(payload.get("schemaVersion")) is not int or payload.get("schemaVersion") != REVIEW_SCHEMA:
        issues.append(f"semantic review schemaVersion must be {REVIEW_SCHEMA}")
    if payload.get("inventorySha256") != census.inventory_sha256:
        issues.append("semantic review does not bind the current staged C# inventory")
    if payload.get("exceptions") != []:
        issues.append("semantic review exceptions must be empty; only an author ruling may amend Addendum 9")
    if not _human_text(payload.get("reviewedBy"), 2, 80):
        issues.append("semantic review reviewedBy must name the human reviewer")
    if not _utc_timestamp(payload.get("completedUtc")):
        issues.append("semantic review completedUtc must be a real second-precision UTC date")
    for key in ("oneResponsibility", "protocolsAtBoundaries"):
        review = payload.get(key)
        if not isinstance(review, dict):
            issues.append(f"semantic review {key} must be an object")
            continue
        if set(review) != REVIEW_SECTION_KEYS:
            issues.append(f"semantic review {key} fields must be notes and status")
        if review.get("status") != "passed":
            issues.append(f"semantic review {key}.status must be 'passed'")
        if not _human_text(review.get("notes"), 20, 2000):
            issues.append(
                f"semantic review {key}.notes must be bounded human evidence notes"
            )
    return issues


def release_issues(census: Census, ledger: Path) -> list[str]:
    issues = []
    if census.at_or_over_limit:
        issues.append(
            f"{len(census.at_or_over_limit)} staged C# files are not strictly under "
            f"{LINE_LIMIT} physical lines"
        )
    issues.extend(review_issues(ledger, census))
    return issues


def census_payload(census: Census, ledger: Path) -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "lineLimitExclusive": LINE_LIMIT,
        "inventorySha256": census.inventory_sha256,
        "files": len(census.files),
        "physicalLines": census.physical_lines,
        "atOrOver300": len(census.at_or_over_limit),
        "over300": len(census.over_limit),
        "exactly300": len(census.exactly_at_limit),
        "over1000": len(census.over(1000)),
        "over2000": len(census.over(2000)),
        "over5000": len(census.over(5000)),
        "directXrlImports": len(census.direct_xrl_imports),
        "largeDirectXrlImports": len(census.large_direct_xrl_imports),
        "largest": [
            {"path": item.path, "lines": item.lines, "importsXrl": item.imports_xrl}
            for item in sorted(census.files, key=lambda item: (-item.lines, item.path))[:10]
        ],
        "lineLimitFailures": [
            {"path": item.path, "lines": item.lines, "importsXrl": item.imports_xrl}
            for item in sorted(census.at_or_over_limit, key=lambda item: (-item.lines, item.path))
        ],
        "semanticReviewIssues": review_issues(ledger, census),
    }


def print_report(census: Census, ledger: Path) -> None:
    print("STRUCTURE CENSUS (REPORT ONLY)")
    print(f"  staged production C# files: {len(census.files)}")
    print(f"  physical lines: {census.physical_lines}")
    print(
        f"  not strictly under {LINE_LIMIT}: {len(census.at_or_over_limit)} "
        f"({len(census.over_limit)} over; {len(census.exactly_at_limit)} exactly)"
    )
    print(
        f"  over 1,000 / 2,000 / 5,000: {len(census.over(1000))} / "
        f"{len(census.over(2000))} / {len(census.over(5000))}"
    )
    print(
        f"  direct XRL imports: {len(census.direct_xrl_imports)}; "
        f"at/over line limit: {len(census.large_direct_xrl_imports)}"
    )
    print(f"  inventory SHA-256: {census.inventory_sha256}")
    print("  largest:")
    for item in sorted(census.files, key=lambda row: (-row.lines, row.path))[:10]:
        print(f"    {item.lines:>6}  {item.path}")
    semantic = review_issues(ledger, census)
    if semantic:
        print("  semantic review: OPEN")
        for issue in semantic:
            print(f"    {issue}")
    else:
        print(f"  semantic review: CURRENT ({ledger})")
    print(
        "  NOTE: line counts and XRL imports are conservative signals; they do not prove "
        "one responsibility or protocol quality."
    )


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    mode = result.add_mutually_exclusive_group()
    mode.add_argument("--report", action="store_true", help="report debt and exit zero")
    mode.add_argument("--release", action="store_true", help="fail on any unresolved release requirement")
    result.add_argument("--json", action="store_true", help="emit machine-readable census")
    result.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parent.parent)
    result.add_argument("--inventory-file", type=Path, help="test/fixture inventory; default is Tools/stage.sh list")
    result.add_argument("--review-ledger", type=Path, help=f"default: {DEFAULT_REVIEW_LEDGER}")
    return result


def main(argv: Sequence[str] | None = None) -> int:
    args = parser().parse_args(argv)
    root = args.repo_root.resolve()
    ledger = args.review_ledger or root / DEFAULT_REVIEW_LEDGER
    if not ledger.is_absolute():
        ledger = root / ledger
    try:
        relatives = inventory_file(args.inventory_file) if args.inventory_file else stage_inventory(root)
        census = build_census(root, relatives)
    except (OSError, StructureError) as error:
        print(f"STRUCTURE CENSUS FAILED: {error}", file=sys.stderr)
        return 2

    if args.json:
        print(json.dumps(census_payload(census, ledger), indent=2, sort_keys=True))
    else:
        print_report(census, ledger)

    if not args.release:
        return 0
    issues = release_issues(census, ledger)
    if issues:
        print("STRUCTURE RELEASE GATE FAILED", file=sys.stderr)
        for issue in issues:
            print(f"  {issue}", file=sys.stderr)
        return 1
    print("STRUCTURE RELEASE GATE CLEAN")
    return 0


if __name__ == "__main__":
    sys.exit(main())
