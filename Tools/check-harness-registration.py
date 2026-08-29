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
import importlib.util
import os
import re
import shlex
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


class InventoryRefused(Exception):
    """The shared inventory helper refused. Surfaced as a problem line, never as a traceback."""


def dev_route_shards() -> set[str]:
    """Exactly the shards the licensed dev-profile compile inventory covers.

    Asked of the one shared helper rather than re-globbed here: a second hand-maintained list is a
    second thing to drift, and the whole point is that the checker and the gate agree about which
    shards a compiler will see.
    """
    result = subprocess.run(
        [sys.executable, str(ROOT / "Tools" / "dev-harness-inventory.py"), "--list-harness"],
        capture_output=True,
        text=True,
        cwd=str(ROOT),
    )
    if result.returncode != 0:
        # A refusal from the shared helper is this audit's finding too, not a traceback escaping
        # through it. The operator should read one problem line, not a stack.
        raise InventoryRefused((result.stderr or result.stdout).strip() or "unknown fault")
    return {row for row in result.stdout.splitlines() if row}


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


RECEIPT = ROOT / "Tools" / "PortableOutput" / "dev-harness-receipt.json"

# Driver statements that must appear, in order, at nesting depth zero and exactly once each.
DRIVER_SEQUENCE = (
    "failed=0",
    "dev_baseline_rc=1",
    "dev_compatibility_rc=1",
    "compile_mode baseline || failed=1",
    "compile_mode compatibility || failed=1",
    "prepare_dev_harness",
    "compile_dev_harness baseline && dev_baseline_rc=0 || failed=1",
    "compile_dev_harness compatibility && dev_compatibility_rc=0 || failed=1",
    'exit "$failed"',
)

# Assignments that must occur exactly ONCE in the whole script. A second `failed=0` after the
# compiles, or a `dev_*_rc=0` initialiser, forges success without touching anything the ordered
# scan looks at.
SINGLE_ASSIGNMENTS = ("failed=0", "dev_baseline_rc=1", "dev_compatibility_rc=1")
FORBIDDEN_ASSIGNMENTS = ("dev_baseline_rc=0", "dev_compatibility_rc=0")

FUNCTION_BODIES = {
    "prepare_dev_harness": (
        ('"$REPO/Tools/stage.sh" copy "$DEV"', "the dev profile must be staged"),
        ("--list-harness", "the overlay must come from the shared inventory"),
        ("scenario_profile.py", "the dev manifest must be derived"),
    ),
    "compile_dev_harness": (
        ("--dev-sources", "the dev list must come from the shared primitive, per mode"),
        ("csc.dll", "the compiler must actually be invoked"),
        ("rc=$?", "the compiler's status must be captured"),
        ('return "$rc"', "the compiler's status must be returned, not discarded"),
    ),
    "compile_mode": (
        ("csc.dll", "the ordinary compiler must actually be invoked"),
        ('return "$rc"', "the ordinary compile status must be returned"),
    ),
}

# The lane's only destructive action. The body is an ALLOWLIST: exactly the two paths this run
# allocated, nothing else. The previous rule exempted any line mentioning cleanup, so the
# amendment's own named bypass and `cleanup() { rm -rf "$REPO"; }` both audited green.
CLEANUP_BODY = 'rm -rf "$STAGE" "$DEV"'
CLEANUP_ONE_LINE = 'cleanup() { rm -rf "$STAGE" "$DEV"; }'

OPENERS = ("if ", "if\t", "while ", "for ", "case ", "until ")

# Everything that ends one command and begins another. A destructive command hiding behind a
# compound-command keyword is still a destructive command.
COMMAND_SEPARATORS = frozenset({
    ";", "&&", "||", "|", "&", "{", "}", "(", ")", "((", "))",
    "then", "else", "elif", "do", "done", "fi", "esac", "in", "!",
})
COMPOUND_OPENERS = "{(`"
COMPOUND_CLOSERS = "})`"

# Command-substitution and process-substitution openers. These are multi-character, so a
# single-character `token[0] in COMPOUND_OPENERS` test never peeled them - and gate.sh itself uses
# $( 31 times, so `X=$(rm -rf ...)` looked like an ordinary assignment.
SUBSTITUTION_OPENERS = ("$((", "$(", "<(", ">(", "`")
FUNCTION_PREFIX = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*\(\)")
ASSIGNMENT_SUBSTITUTION = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*=(?=[$`<>]\()")
FUNCTION_NAME = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)\s*\(\)\s*\{")


def code_lines(text: str) -> list[str]:
    """Statements only: comments stripped, blank lines dropped."""
    rows = []
    for raw in text.split("\n"):
        if raw.lstrip().startswith("#"):
            continue
        statement = raw.split("#", 1)[0].strip()
        if statement:
            rows.append(statement)
    return rows


def normalise_exit(statement: str) -> str | None:
    """The exit ARGUMENT, quoting and trailing punctuation removed.

    `exit "0"` and `exit 0;` are the same instruction as `exit 0`; an audit that compares spellings
    is testing its own vocabulary rather than the script.
    """
    if not statement.startswith("exit"):
        return None
    rest = statement[4:].strip().rstrip(";").strip()
    if rest.startswith(("'", '"')) and rest.endswith(("'", '"')) and len(rest) >= 2:
        rest = rest[1:-1]
    return rest


def shell_regions(text: str):
    """Top-level statements at depth zero, plus every function body keyed by name.

    One-line definitions are recognised. A `compile_dev_harness() { :; }` shadow above the driver
    was previously filed as an ordinary line while the original multi-line body still matched -
    bash uses the last definition, so the shadow won at runtime and the audit never saw it.
    """
    driver: list[str] = []
    bodies: dict[str, list[list[str]]] = {}
    current = None
    depth = 0
    for statement in code_lines(text):
        match = FUNCTION_NAME.match(statement)
        if current is None and match:
            name = match.group(1)
            rest = statement[match.end():].strip()
            if rest.endswith("}"):
                bodies.setdefault(name, []).append([rest[:-1].strip().rstrip(";").strip()])
                continue
            current = name
            bodies.setdefault(current, []).append([])
            continue
        if current is not None:
            if statement == "}":
                current = None
                continue
            bodies[current][-1].append(statement)
            continue
        if depth == 0:
            driver.append(statement)
        if statement.startswith(OPENERS) or statement == "if":
            depth += 1
        elif statement in ("fi", "done", "esac"):
            depth = max(0, depth - 1)
    return driver, bodies


def assert_route_wiring(problems: list[str], gate: str | None = None) -> None:
    """BEST-EFFORT tripwire over the known neutralisation classes.

    Per the standing ruling this can never establish that a compile happened. A structural audit of
    a shell script cannot win a mutation arms race, and three rounds proved it; what it can do is
    catch the classes below and fail loudly when the route stops being able to fail. The compiled
    claim belongs to the receipt, written by an actual gate run.
    """
    text = gate if gate is not None else read(ROOT / "Tools" / "gate.sh")
    statements = code_lines(text)
    driver, bodies = shell_regions(text)
    cursor = -1
    for needle in DRIVER_SEQUENCE:
        try:
            cursor = driver.index(needle, cursor + 1)
        except ValueError:
            problems.append("gate route wiring: the top-level driver does not run " + needle)
    for needle in SINGLE_ASSIGNMENTS:
        name, _, value = needle.partition("=")
        # Cheap extra spellings of the same assignment. Arithmetic and indirect forms still
        # survive: this is a tripwire over known classes, not a bash evaluator, and section 4 of
        # the amendment says so rather than implying completeness.
        base = {needle, name + '="' + value + '"', name + "='" + value + "'"}
        variants = base | {row + ";" for row in base}
        seen = sum(
            1 for row in statements
            if row in variants or any(row.endswith("; " + v) for v in variants)
        )
        if seen != 1:
            problems.append(
                "gate route wiring: %s occurs %d times; a second assignment forges the outcome"
                % (needle, seen)
            )
    for needle in FORBIDDEN_ASSIGNMENTS:
        if any(row == needle for row in statements):
            problems.append(
                "gate route wiring: " + needle + " initialises a compile result as success"
            )
    if not driver or driver[-1] != 'exit "$failed"':
        problems.append(
            "gate route wiring: the gate does not end by propagating its own failure status"
        )
    for statement in statements:
        argument = normalise_exit(statement)
        if argument == "0":
            problems.append(
                "gate route wiring: an early exit 0 reports success without running the route"
            )
    for name, requirements in FUNCTION_BODIES.items():
        definitions = bodies.get(name, [])
        if len(definitions) != 1:
            problems.append(
                "gate route wiring: %s is defined %d times; a later definition shadows the route"
                % (name, len(definitions))
            )
            continue
        body = "\n".join(definitions[0])
        for needle, why in requirements:
            if needle not in body:
                problems.append("gate route wiring: " + name + " - " + why)
    assert_cleanup_allowlist(problems, text, statements, bodies)
    # The trap must be the LITERAL form. `T='- EXIT'; trap $T` and `trap "" EXIT` both disarm it
    # while a startswith-scan for "trap" sees nothing wrong.
    traps = [row for row in statements if "trap" in row.split()]
    if not any(row.endswith("trap cleanup EXIT") for row in traps):
        problems.append("gate route wiring: cleanup is not bound to a literal trap")
    for statement in traps:
        if not statement.endswith("trap cleanup EXIT"):
            problems.append("gate route wiring: a trap other than the literal cleanup trap")
    if 'DEV="$(mktemp -d /tmp/taf-devharness.XXXXXX)"' not in statements:
        problems.append("gate route wiring: the dev tree is not independently allocated")
    if not any("--inventory-digest" in row for row in statements):
        problems.append("gate route wiring: the receipt does not bind the compile inventory")
    receipt_at = next((i for i, row in enumerate(statements) if "$RECEIPT" in row and ">" in row), -1)
    compile_at = max(
        (i for i, row in enumerate(statements) if row.startswith("compile_dev_harness ")),
        default=-1,
    )
    if receipt_at < 0:
        problems.append("gate route wiring: no receipt is written")
    elif compile_at < 0 or receipt_at < compile_at:
        problems.append("gate route wiring: the receipt is written before the compiles run")
    for statement in statements:
        if '"$STAGE.dev"' in statement:
            problems.append(
                "gate route wiring: the dev tree is a derived sibling, not an allocation"
            )


def assert_cleanup_allowlist(problems, text, statements, bodies) -> None:
    """The one destructive action, allowlisted by TOKEN rather than by spelling.

    Matching the literal "rm -rf" tested one spelling of one command: `rm -fr`, `rm -r -f`,
    `rm -Rf`, `rm -rvf`, `rm --recursive --force`, a double space, and a `cleanup2()` indirection
    were all destructive and all green. The question is not how the flags are written, it is whether
    the command being run is `rm` at all.
    """
    if CLEANUP_ONE_LINE not in text:
        problems.append("gate route wiring: cleanup is not the exact allowlisted one-line form")
    definitions = bodies.get("cleanup", [])
    if len(definitions) != 1 or [CLEANUP_BODY] != definitions[0]:
        problems.append(
            "gate route wiring: cleanup's body is not exactly the two allocated paths"
        )
    for statement in statements:
        if statement == CLEANUP_ONE_LINE:
            continue
        for command in split_commands(statement):
            if not command:
                continue
            if argv_zero(command) == "rm":
                problems.append(
                    "gate route wiring: a removal outside the allowlisted cleanup body: "
                    + statement[:80]
                )
                break


# Every lexical thing that ends one command and begins another, longest-match first.
BOUNDARY = re.compile(r"(;;&|;;|;|&&|\|\||\||&|\$\(\(|\$\(|<\(|>\(|\(|\)|\{|\}|`)")

# Words that end a command in shell grammar without any punctuation.
BOUNDARY_WORDS = frozenset({
    "then", "else", "elif", "do", "done", "fi", "esac", "in", "!", "case", "while",
    "until", "if", "for", "select",
})


def split_commands(statement: str) -> list[list[str]]:
    """Every command in one statement, found by LEXICAL split rather than token surgery.

    Peeling characters off shlex tokens could not see three whole classes: a `case` arm's `)` was
    absorbed into the pattern token so the arm's body stayed in the pattern's command; a quoted
    `"$(rm -rf ...)"` arrived as ONE token with spaces inside it; and `X=$(rm ...)` looked like an
    ordinary assignment. Spacing every boundary out of the raw statement first, then splitting on
    whitespace, makes all three ordinary.

    Quotes are deliberately not honoured here. This is not a shell; it is looking for whether `rm`
    is invoked anywhere in a statement, and a quoted separator that hides a command from this scan
    would hide it from a reader too.
    """
    commands: list[list[str]] = [[]]
    for token in BOUNDARY.sub(r" \1 ", statement).split():
        if BOUNDARY.fullmatch(token) or token in BOUNDARY_WORDS:
            commands.append([])
            continue
        token = token.strip("\"'")
        if token:
            commands[-1].append(token)
    return commands


# Wrappers that run some OTHER command named in their own arguments. `xargs rm -rf` runs rm.
DELEGATING = ("xargs", "nice", "ionice", "timeout", "stdbuf")


def argv_zero(command: list[str]) -> str:
    """The command actually executed, past env assignments, prefixes, and delegating wrappers."""
    for index, token in enumerate(command):
        if "=" in token and not token.startswith("="):
            name = token.split("=", 1)[0]
            if name.replace("_", "").isalnum():
                continue
        if token in ("sudo", "command", "exec", "nohup", "time", "env", "-"):
            continue
        name = os.path.basename(token.strip("\"'"))
        if name in DELEGATING:
            for candidate in command[index + 1:]:
                # Skip the wrapper's own options and operands (timeout's duration, nice's level).
                if candidate.startswith("-") or "=" in candidate:
                    continue
                if candidate.replace(".", "", 1).rstrip("smhd").isdigit():
                    continue
                return os.path.basename(candidate.strip("\"'"))
            return name
        return name
    return ""


def route_receipt():
    """A verified receipt, or None. Only a real gate run can produce one."""
    spec = importlib.util.spec_from_file_location(
        "dev_harness_inventory", ROOT / "Tools" / "dev-harness-inventory.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    try:
        return module.read_receipt(str(RECEIPT))
    except SystemExit:
        return None


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
    try:
        assert_inventory(problems)
    except InventoryRefused as refusal:
        problems.append("dev-harness inventory: " + str(refusal))
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
    receipt = route_receipt()
    state = ("compiled by a gate run recorded " + receipt["recordedUtc"]) if receipt else (
        "WIRED BUT NEVER EXECUTED - no verified gate receipt, so nothing here has met a compiler"
    )
    print(
        "harness registration audit clean (%d shards; %d engine-free in both public projects, "
        "%d engine-touching %s)" % (len(shards), len(shards) - len(engine), len(engine), state)
    )
    print(
        "  route wiring is a BEST-EFFORT tripwire over known neutralisation classes, never proof "
        "of execution; the compiled claim rests on the receipt a real gate run writes."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
