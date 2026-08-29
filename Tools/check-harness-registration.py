#!/usr/bin/env python3
"""Registration and compile-surface audit for the developer scenario harness.

Three failures this exists to make impossible:

1. A dev-harness shard drifts off the on-disk inventory without anyone registering it, so runtime
   code is vouched for by nothing but string assertions.
2. An ENGINE-FREE shard or its fixture lands in one test project only. Both suites are engine-free
   (`TAF_TESTS` compiles the `XRL` surface out of production files); registering a pure shard in one
   and not the other halves its executable proof for no reason.
3. A fixture names a type from a SIBLING namespace without a using. C# resolves outward from the
   enclosing namespace, so `ThousandAndFirst.Tests` sees `ThousandAndFirst.*` for free but never
   `ThousandAndFirst.Harness.*`. That miss is invisible until a compiler sees it.

Engine-touching shards are never "accepted residue". Both test projects are deliberately Qud-free
(`Tools/portable-check.sh` forbids a game reference in either), so the compiler that sees the engine
surface is the licensed one in `Tools/gate.sh`. Every engine-touching harness shard must therefore be
covered EXACTLY by the dev-profile compile inventory, and every engine-touching Core shard must be in
the shipped runtime inventory the ordinary gate already compiles.

Containment is proved separately and never by omission: the shipped manifest must still select no
harness directory, which `assert_containment` re-proves here rather than assuming.
"""

from __future__ import annotations

import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as etree

ROOT = Path(__file__).resolve().parent.parent
TAF = ROOT / "DevTests" / "TafTests.csproj"
PORTABLE = ROOT / "DevTests" / "PortableTests.csproj"

# Every directory whose C# is developer-harness code owned by the scenario lane.
HARNESS_DIRECTORIES = (ROOT / "Harness",)
HARNESS_CORE_PREFIXES = ("KingdomScenario", "KingdomRealized")

DECLARATION = re.compile(
    r"\b(?:class|struct|enum|interface)\s+([A-Za-z_][A-Za-z0-9_]*)"
)
NAMESPACE = re.compile(r"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)", re.M)
USING = re.compile(r"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;", re.M)
USING_XRL = re.compile(r"^\s*using\s+XRL", re.M)
GUARD = re.compile(r"#if\s+!TAF_TESTS")
STRINGS = re.compile(r'@?"(?:[^"\\]|\\.|"")*"')
LINE_COMMENT = re.compile(r"//.*$")
KINGDOM = re.compile(r"(?<![.\w])(Kingdom[A-Za-z0-9_]*)\b")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def rows(project: Path) -> list[str]:
    tree = etree.parse(project).getroot()
    return [
        node.get("Include", "").replace("\\", "/")
        for node in tree.findall(".//Compile")
    ]


def harness_sources() -> list[Path]:
    found = []
    for directory in HARNESS_DIRECTORIES:
        found.extend(sorted(directory.glob("*.cs")))
    for path in sorted((ROOT / "Core").glob("*.cs")):
        if path.name.startswith(HARNESS_CORE_PREFIXES):
            found.append(path)
    return found


def engine_free(path: Path) -> bool:
    """A shard both suites can compile: no XRL using outside a !TAF_TESTS guard."""
    text = read(path)
    if not USING_XRL.search(text):
        return True
    return False


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def dev_route_shards() -> set[str]:
    """Exactly the shards the licensed dev-profile compile inventory covers.

    Asked of the one shared helper rather than re-globbed here: a second hand-maintained list is a
    second thing to drift, and the whole point is that the checker and the gate agree about which
    shards a compiler will see.
    """
    listing = subprocess.check_output(
        [sys.executable, str(ROOT / "Tools" / "dev-harness-inventory.py"), "--list-harness"],
        text=True,
        cwd=str(ROOT),
    )
    return {row for row in listing.splitlines() if row}


def staged_runtime() -> set[str]:
    listing = subprocess.check_output(
        [str(ROOT / "Tools" / "stage.sh"), "list"], text=True, cwd=str(ROOT)
    )
    return {row for row in listing.splitlines() if row}


def assert_inventory(problems: list[str]) -> None:
    """Complete on-disk dev-harness inventory against the route that can actually compile it.

    Engine-free shards belong in BOTH public projects. Engine-touching shards belong to the licensed
    compile route and are checked to be exactly covered there - never waved through as unregistered.
    """
    taf = set(rows(TAF))
    portable = set(rows(PORTABLE))
    covered = dev_route_shards()
    runtime = staged_runtime()
    manifest_paths = shipped_manifest_paths()
    for path in harness_sources():
        include = "../" + relative(path)
        if engine_free(path):
            if include not in taf:
                problems.append(
                    "engine-free harness shard missing from TafTests: " + relative(path)
                )
            if include not in portable:
                problems.append(
                    "engine-free harness shard missing from PortableTests: "
                    + relative(path)
                )
            continue
        if include in taf or include in portable:
            problems.append(
                "engine-touching shard registered in a Qud-free project, where its engine "
                "surface cannot compile: " + relative(path)
            )
        if path.parent.name == "Harness":
            if path.name not in covered:
                problems.append(
                    "engine-touching harness shard outside the dev compile inventory: "
                    + relative(path)
                )
            continue
        # An engine-touching shard under a shipped directory is compiled by the ordinary gate,
        # which is only true while the manifest still selects that directory and the stage
        # still carries the file. Both are proved, never assumed.
        selected = "/" + path.parent.name + "/"
        if selected not in manifest_paths:
            problems.append(
                "engine-touching shard sits in a directory the shipped manifest does not "
                "select: " + relative(path)
            )
        if relative(path) not in runtime:
            problems.append(
                "engine-touching shard is absent from the staged runtime inventory the gate "
                "compiles: " + relative(path)
            )


def assert_fixture_parity(problems: list[str]) -> None:
    """A fixture built only from engine-free shards belongs in BOTH suites."""
    pure = {}
    for path in harness_sources():
        if engine_free(path):
            for symbol in DECLARATION.findall(read(path)):
                pure[symbol] = relative(path)
    taf = {r for r in rows(TAF)}
    portable = {r for r in rows(PORTABLE)}
    for name in sorted(taf):
        if name.startswith("..") or not name.endswith(".cs"):
            continue
        path = ROOT / "DevTests" / name
        if not path.is_file():
            continue
        text = read(path)
        if USING_XRL.search(text):
            continue
        if not any(symbol in pure for symbol in KINGDOM.findall(strip(text))):
            continue
        if name not in portable:
            problems.append(
                "engine-free harness fixture missing from PortableTests: " + name
            )


def strip(text: str) -> str:
    return "\n".join(
        STRINGS.sub('""', LINE_COMMENT.sub("", line)) for line in text.split("\n")
    )


def declarations() -> dict[str, str]:
    table: dict[str, str] = {}
    for base in sorted(ROOT.iterdir()):
        if not base.is_dir() or base.name.startswith((".", "_")):
            continue
        for path in sorted(base.rglob("*.cs")):
            text = read(path)
            spaces = NAMESPACE.findall(text)
            space = spaces[0] if spaces else ""
            # Strings first: a source-contract fixture quotes real declarations, and a quoted
            # declaration is evidence about another file, never a declaration in this one.
            for symbol in DECLARATION.findall(strip(text)):
                table.setdefault(symbol, space)
    return table


def visible(space: str, usings: set[str], target: str) -> bool:
    if not target or target in usings:
        return True
    parts = space.split(".")
    while parts:
        if ".".join(parts) == target:
            return True
        parts.pop()
    return False


def assert_namespaces(problems: list[str]) -> None:
    """Every referenced type must be reachable from the fixture's own namespace."""
    table = declarations()
    for path in sorted((ROOT / "DevTests").glob("*.cs")):
        text = read(path)
        spaces = NAMESPACE.findall(text)
        space = spaces[0] if spaces else ""
        usings = set(USING.findall(text)) | set(spaces)
        own = set(DECLARATION.findall(text))
        for name in sorted(set(KINGDOM.findall(strip(text)))):
            if name in own or name not in table:
                continue
            if not visible(space, usings, table[name]):
                problems.append(
                    "%s names %s from %s with no using"
                    % (path.name, name, table[name] or "<global>")
                )


def shipped_manifest_paths() -> set[str]:
    manifest = json.loads(read(ROOT / "manifest.json"))
    paths = set()
    for row in manifest.get("Directories", []):
        for selected in row.get("Paths", []):
            paths.add(str(selected))
    return paths


# The exact call sites the licensed dev route must perform. A helper nothing invokes proves nothing,
# so the clean verdict binds to these rather than to the helper's own opinion of the inventory.
ROUTE_REQUIREMENTS = (
    ('DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"',
     "the dev tree must be independently allocated, never derived from another path"),
    ('cleanup() { rm -rf "$STAGE" "$DEV"; }',
     "one trap must remove exactly the two trees this run allocated"),
    ("trap cleanup EXIT",
     "cleanup must be bound to a trap"),
    ("\nprepare_dev_harness\ncompile_dev_harness baseline",
     "the dev profile must be prepared before the dev compiles run"),
    ("compile_dev_harness baseline || failed=1",
     "dev baseline must compile and its failure must fail the gate"),
    ("compile_dev_harness compatibility || failed=1",
     "dev compatibility must compile and its failure must fail the gate"),
    ('--sources \\\n\t\t--stage "$STAGE" --mode "$mode"',
     "the ordinary inventory must come from the shared primitive"),
    ('--dev-sources \\\n\t\t--stage "$DEV" --mode "$mode"',
     "the dev inventory must come from the shared primitive, per mode"),
    ("compile_mode baseline || failed=1",
     "the ordinary baseline compile must stay wired"),
    ("compile_mode compatibility || failed=1",
     "the ordinary compatibility compile must stay wired"),
)


def assert_route_wiring(problems: list[str], gate: str = None) -> None:
    """The gate must actually perform both exact dev compiles.

    Removing the three dev call sites used to leave this checker green, which meant a clean verdict
    described a helper that nothing ran. Each requirement below is a call site, not a mention.
    """
    text = gate if gate is not None else read(ROOT / "Tools" / "gate.sh")
    for needle, why in ROUTE_REQUIREMENTS:
        if needle not in text:
            problems.append("gate route wiring: " + why)
    # Comments are allowed to explain the hazard; code is not allowed to be it.
    code = [
        line for line in text.split("\n") if not line.strip().startswith("#")
    ]
    for line in code:
        if '"$STAGE.dev"' in line:
            problems.append(
                "gate route wiring: the dev tree is a derived sibling, not an allocation"
            )
        if "rm -rf" in line and "cleanup() {" not in line:
            problems.append(
                "gate route wiring: a recursive removal outside the single cleanup trap"
            )


def assert_containment(problems: list[str]) -> None:
    """Containment is proved on its own terms, never by omitting code from compile coverage."""
    for selected in shipped_manifest_paths():
        if "harness" in selected.lower():
            problems.append("shipped manifest selects a harness directory: " + selected)
    if "Harness" in read(ROOT / "manifest.json"):
        problems.append("shipped manifest mentions the harness directory")
    for row in staged_runtime():
        if row.startswith("Harness/"):
            problems.append("harness path entered the shipped runtime inventory: " + row)


def main() -> int:
    problems: list[str] = []
    assert_inventory(problems)
    assert_fixture_parity(problems)
    assert_namespaces(problems)
    assert_containment(problems)
    assert_route_wiring(problems)
    if problems:
        for row in sorted(set(problems)):
            print("harness registration: " + row, file=sys.stderr)
        return 1
    shards = harness_sources()
    engine = [p for p in shards if not engine_free(p)]
    print(
        "harness registration audit clean (%d shards; %d engine-free in both public projects, "
        "%d engine-touching compiled by the wired licensed route)"
        % (len(shards), len(shards) - len(engine), len(engine))
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
