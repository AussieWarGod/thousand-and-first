#!/usr/bin/env python3
"""Generate the exact object-property teardown allowlist from production writes."""

from __future__ import annotations

import argparse
import pathlib
import re
import sys


ROOT = pathlib.Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Core" / "KingdomRemovalCoverage.Generated.cs"
OWNED_STARTS = ("Kingdom", "TAF", "r_TAF_", "ThousandAndFirst.")
SKIP_DIRS = {".git", "DevTests", "Tools"}
CLASS_CENSUS_SKIP_DIRS = {"DevTests", "Tools", "Harness", "Integrations"}

DECLARATION = re.compile(
    r"\b(?:const|static\s+readonly)\s+string\s+"
    r"(?P<name>\w*(?:Property|Key)\w*)\s*=\s*\"(?P<value>[^\"]*)\""
)
DIRECT_WRITE = re.compile(
    r"\.Set(?:String|Int)Property\(\s*\"(?P<value>[^\"]+)\""
)
XML_PROPERTY = re.compile(
    r"<(?:property|intproperty)\b[^>]*\bName=\"(?P<value>[^\"]+)\"",
    re.IGNORECASE,
)
XML_BLUEPRINT = re.compile(
    r"<object\b[^>]*\bName=\"(?P<value>r_(?:Kingdom|Founder|Founding)[^\"]*)\"",
    re.IGNORECASE,
)
SOURCE_BLUEPRINT = re.compile(
    r"\b(?:const|static\s+readonly)\s+string\s+\w*Blueprint\s*=\s*"
    r"\"(?P<value>r_(?:Kingdom|Founder|Founding)[^\"]*)\""
)
CLASS_DECLARATION = re.compile(
    r"\bclass\s+(?P<name>[A-Za-z_]\w*)\s*:\s*(?P<bases>[^\{\r\n]+)"
)
MANUAL_OBJECT_PROPERTIES = {
    *(f"KingdomGatehouseSatelliteId{i}" for i in range(6)),
    *(f"KingdomGatehouseSatelliteState{i}" for i in range(6)),
}
MANUAL_BLUEPRINTS = {"r_FounderBasin", "r_FoundingBook"}


def production_files(suffix: str):
    for path in ROOT.rglob(f"*{suffix}"):
        relative = path.relative_to(ROOT)
        if any(part in SKIP_DIRS for part in relative.parts):
            continue
        yield path


def collect() -> list[str]:
    # This function's output is checked in; keep iteration and formatting deterministic.
    values: set[str] = set(MANUAL_OBJECT_PROPERTIES)
    for path in production_files(".cs"):
        text = path.read_text(encoding="utf-8")
        for pattern in (DECLARATION, DIRECT_WRITE):
            for match in pattern.finditer(text):
                value = match.group("value")
                if value.startswith(OWNED_STARTS):
                    values.add(value)
    for path in production_files(".xml"):
        text = path.read_text(encoding="utf-8")
        for match in XML_PROPERTY.finditer(text):
            value = match.group("value")
            if value.startswith(OWNED_STARTS):
                values.add(value)
    return sorted(values)


def collect_blueprints() -> list[str]:
    values: set[str] = set(MANUAL_BLUEPRINTS)
    for path in production_files(".cs"):
        text = path.read_text(encoding="utf-8")
        values.update(match.group("value") for match in SOURCE_BLUEPRINT.finditer(text))
    for path in production_files(".xml"):
        text = path.read_text(encoding="utf-8")
        values.update(match.group("value") for match in XML_BLUEPRINT.finditer(text))
    return sorted(values)


def collect_custom_parts() -> list[str]:
    """Mirror the source gate's inheritance census, including split partial classes."""
    bases: dict[str, set[str]] = {}
    for path in ROOT.rglob("*.cs"):
        relative = path.relative_to(ROOT)
        if relative.parts and relative.parts[0] in CLASS_CENSUS_SKIP_DIRS:
            continue
        text = path.read_text(encoding="utf-8")
        for match in CLASS_DECLARATION.finditer(text):
            values = bases.setdefault(match.group("name"), set())
            for value in match.group("bases").split(","):
                clean = value.strip().split("<")[0].strip().rsplit(".", 1)[-1]
                if clean:
                    values.add(clean)

    roots = {"IPart", "TeleporterPair"}
    derived = set(roots)
    changed = True
    while changed:
        changed = False
        for name, values in bases.items():
            if name not in derived and values & derived:
                derived.add(name)
                changed = True
    return sorted(name for name in derived - roots if name == "KingdomCharterPart"
                  or name.startswith("r_Kingdom") or name.startswith("r_Founder"))


GENERATED_LINE_WIDTH = 200


def append_array(lines: list[str], name: str, values: list[str]) -> None:
    lines.extend([
        f"\t\tpublic static readonly string[] {name} = new string[]",
        "\t\t{",
    ])
    current = "\t\t\t"
    for value in values:
        item = f'\"{value}\",'
        # Generated inventories favor a bounded source-file size over hand wrapping. Keeping each
        # deterministic line readable at ordinary wide-review widths leaves the runtime shard
        # strictly below the production 300-line cap as the exact registries grow.
        if len(current) > 3 and len(current) + len(item) + 1 > GENERATED_LINE_WIDTH:
            lines.append(current.rstrip())
            current = "\t\t\t"
        current += item + " "
    if len(current) > 3:
        lines.append(current.rstrip())
    lines.append("\t\t};")


def render(values: list[str], blueprints: list[str], custom_parts: list[str]) -> str:
    lines = [
        "// <auto-generated />",
        "// Tools/generate-removal-coverage.py -- exact literals and shipped custom-part census;",
        "// dynamic grammars live in KingdomRemovalCoverage.OwnedObjectPropertyPrefixes.",
        "namespace ThousandAndFirst",
        "{",
        "\tpublic static partial class KingdomRemovalCoverage",
        "\t{",
    ]
    append_array(lines, "DiscoveredCustomParts", custom_parts)
    lines.append("")
    append_array(lines, "OwnedObjectProperties", values)
    lines.append("")
    append_array(lines, "OwnedBlueprints", blueprints)
    lines.extend(["\t}", "}", ""])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    expected = render(collect(), collect_blueprints(), collect_custom_parts())
    if args.check:
        actual = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if actual != expected:
            print("generated removal coverage is stale", file=sys.stderr)
            return 1
        return 0
    OUTPUT.write_text(expected, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
