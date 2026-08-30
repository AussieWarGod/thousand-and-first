#!/usr/bin/env python3
"""Host-side mirror of the in-game architecture gallery case numbering.

Ports the enumeration in Debug/KingdomArchitectureGalleryWishes.Staging.cs
(Cases over KingdomArchitecture.InspectMappings) so tests and tooling can
turn a build key/variant/facing into the case number that the in-game wish
`kingdom:archgallery <N>` accepts, without paging the in-game list. The
ordering laws mirrored here:

- one record per plan binding tier (Growth/KingdomArchitecture.Records.cs);
- records sorted by BuildKey ordinal, folded TypeKey ordinal, then LotSize
  (Growth/KingdomArchitecture.Helpers.cs OrderedRecords);
- variant keys ordinal-sorted per record (KingdomArchitectureMapping ctor);
- facings North(0) East(1) South(2) West(3); numbering starts at 1.

Usage:
  gallery_cases.py                  # census: total cases / records / streams
  gallery_cases.py --find court     # cases whose build key contains "court"
  gallery_cases.py --key court      # cases whose build key equals "court"
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

SIZE_VALUES = {
    "s": 1,
    "small": 1,
    "m": 2,
    "medium": 2,
    "l": 3,
    "large": 3,
    "xl": 4,
    "huge": 4,
}
SIZE_NAMES = {1: "Small", 2: "Medium", 3: "Large", 4: "Huge"}
FACINGS = ("North", "East", "South", "West")


def fold(value: str | None) -> str | None:
    if value is None or not value.strip():
        return None
    return value.strip().lower()


@dataclass(frozen=True)
class Record:
    build_key: str
    type_key: str
    size: int
    variants: tuple[str, ...]


@dataclass(frozen=True)
class Case:
    number: int
    record: Record
    variant: str
    facing: str

    def line(self) -> str:
        return (
            f"{self.number}\t{self.record.build_key}|{self.record.type_key}"
            f"|{SIZE_NAMES[self.record.size]}|{self.variant}|{self.facing}"
        )


def load_records(root_dir: Path) -> tuple[list[Record], int]:
    records: list[Record] = []
    streams = 0
    for path in sorted(root_dir.rglob("*.xml")):
        head = path.read_text(encoding="utf-8", errors="replace")[:4096]
        if "<!DOCTYPE" in head or "<!ENTITY" in head:
            raise SystemExit(f"{path}: DTD/entity declarations are refused")
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        if root.tag != "KingdomArchitectures":
            continue
        streams += 1
        for plan in root.iter("plan"):
            for binding in plan.iter("binding"):
                type_key = fold(binding.get("Type"))
                size = SIZE_VALUES.get(fold(binding.get("Size")) or "")
                if type_key is None or size is None:
                    raise SystemExit(
                        f"{path}: malformed binding in plan {plan.get('Key')!r}"
                    )
                for tier in binding.iter("tier"):
                    variants = tuple(
                        sorted(v.get("Key") or "" for v in tier.iter("variant"))
                    )
                    records.append(
                        Record(tier.get("BuildKey") or "", type_key, size, variants)
                    )
    records.sort(key=lambda r: (r.build_key, r.type_key, r.size))
    return records, streams


def enumerate_cases(records: list[Record]) -> list[Case]:
    cases: list[Case] = []
    for record in records:
        for variant in record.variants:
            for facing in FACINGS:
                cases.append(Case(len(cases) + 1, record, variant, facing))
    return cases


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--find", help="substring match on build key")
    parser.add_argument("--key", help="exact build key")
    parser.add_argument(
        "--root",
        default=str(Path(__file__).resolve().parent.parent),
        help="mod root to scan for KingdomArchitectures XML",
    )
    args = parser.parse_args()

    records, streams = load_records(Path(args.root))
    cases = enumerate_cases(records)
    if args.find or args.key:
        for case in cases:
            key = case.record.build_key
            if (args.key and key == args.key) or (
                args.find and not args.key and args.find in key
            ):
                print(case.line())
        return 0
    print(f"streams={streams} records={len(records)} cases={len(cases)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
