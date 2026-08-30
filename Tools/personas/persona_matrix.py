#!/usr/bin/env python3
"""Persona manifests and the host-side journal assertion they carry.

A PERSONA is one unattended scenario run declared as data: the request to freeze, the verbs to
seal, and what the journal must say afterwards. `Tools/run-personas.sh` owns the game; this module
owns the grammar and the verdict, so both are executable without a licensed install and are covered
by `Tools/tests/persona_matrix_test.py`.

WHAT AN EXPECTATION MAY BIND TO. The journal's stable columns are the verb and the `OK|REFUSED`
outcome; the message column carries a whole operator report and its wording is free to improve. An
expectation therefore binds to the outcome column and, optionally, to a stable REASON CODE
substring (`taf-scenario-transaction-committed`) or to a word the law itself owns (`ineligible`).
Never to a sentence.

STRICT IN BOTH DIRECTIONS. The significant rows must equal the declared expectations exactly: an
unexpected `OK` fails as loudly as an unexpected refusal, and a missing row fails as loudly as an
extra one. A matrix whose green means "at least this happened" is not a matrix.
"""

from __future__ import annotations

import os
import re
import sys

# Terminal tokens a persona may declare, and the journal verb each one names. Every unattended run
# ends in exactly one of these rows, so exactly one is the last expectation.
TERMINALS = {
    "COMPLETE": "SCRIPT-COMPLETE",
    "STOPPED": "SCRIPT-STOPPED",
    "GATE-REFUSED": "GATE-REFUSED",
}

# Rows the runner and the harness write about themselves rather than about a verb. They are
# conditional (no primer seam, no test-ground rebuild, no advance) so binding a persona to them
# would make the manifest describe the runner instead of the run.
BOOKKEEPING = frozenset(
    {
        "AUTOSTART",
        "TESTGROUND-BUILT",
        "RUNNER-ARMED",
        "SCRIPT-BEGIN",
        "advance-progress",
        "advance-complete",
        # A third-party verb provider the admission law refused. It describes the PROFILE a run was
        # launched into, not a step the script asked for, so a persona must not go red because
        # somebody else's mod shipped a broken provider. `Tools/run-personas.sh` surfaces these
        # rows in its report either way, pass or fail.
        "VERB-REFUSED",
    }
)

# Must equal scenario_profile.SCRIPT_VERBS and its one counted verb. A persona that seals a verb
# the profile tool refuses would spend a whole prepare discovering it.
SCRIPT_VERBS = ("anchor", "flatten", "ground", "list", "realize", "status")
COUNTED_VERB = "advance"
MAX_ADVANCE_TURNS = 10000

# Names the runtime dispatches itself, which no third-party provider may claim. Must equal
# Harness/KingdomScenarioVerbProvider.cs KingdomScenarioVerbApi.Reserved.
RESERVED_VERBS = (
    "advance",
    "anchor",
    "capture",
    "flatten",
    "ground",
    "help",
    "list",
    "realize",
    "status",
)

# The alphabet KingdomScenarioRules.SafeToken admits, restated so a persona is refused here
# rather than in a sealed profile nobody can retry.
VERB_ALPHABET = "abcdefghijklmnopqrstuvwxyz" + "0123456789" + "-."

OUTCOMES = ("OK", "REFUSED")
CHECKS = ("status-digest-stable",)

REQUIRED_KEYS = ("REQUEST", "SCRIPT", "EXPECT")
OPTIONAL_KEYS = ("START", "CHECK", "TIMEOUT", "DESCRIPTION", "VERBS")

DEFAULT_TIMEOUT = 300
MAX_TIMEOUT = 3600

DIGEST = re.compile(r"(?<![0-9a-f])[0-9a-f]{64}(?![0-9a-f])")


def fail(message: str) -> None:
    raise SystemExit("persona: " + message)


# --------------------------------------------------------------------------------------
# Manifest
# --------------------------------------------------------------------------------------


def parse_manifest(text: str, name: str) -> dict:
    """`KEY=VALUE` lines to a validated persona. Unknown keys are refused, never ignored."""
    found: dict[str, str] = {}
    for number, raw in enumerate(text.splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "\t" in line:
            fail("%s line %d contains a tab; values are tab-free" % (name, number))
        key, sep, value = line.partition("=")
        if not sep:
            fail("%s line %d is not KEY=VALUE: %r" % (name, number, raw))
        key = key.strip()
        if key not in REQUIRED_KEYS + OPTIONAL_KEYS:
            fail("%s line %d declares unknown key %r" % (name, number, key))
        if key in found:
            fail("%s declares %s more than once" % (name, key))
        found[key] = value.strip()
    for key in REQUIRED_KEYS:
        if not found.get(key):
            fail("%s declares no %s" % (name, key))
    if ";seed=" in found["REQUEST"]:
        fail(
            "%s names its own seed; the seed is frozen per profile by "
            "Tools/prepare-scenario.sh" % name
        )
    extra = parse_verbs(found.get("VERBS", ""), name)
    found["VERBS"] = ",".join(extra)
    found["SCRIPT_WORDS"] = " ".join(script_words(found["SCRIPT"], name, extra))
    parse_expect(found["EXPECT"], name, extra)
    check = found.get("CHECK", "")
    if check and check not in CHECKS:
        fail(
            "%s declares unknown CHECK %r; the set is %s"
            % (name, check, ", ".join(CHECKS))
        )
    found["TIMEOUT"] = str(parse_timeout(found.get("TIMEOUT", ""), name))
    return found


def parse_verbs(value: str, name: str) -> tuple[str, ...]:
    """Third-party verb names this persona seals, held to the shape the runtime admits.

    Same alphabet and same reserved set as `Tools/scenario_profile.py`, so a persona cannot seal a
    name the harness would refuse the provider for claiming - a refusal that would otherwise cost a
    whole non-retryable profile to discover.
    """
    if not value:
        return ()
    chosen: list[str] = []
    for raw in value.split(","):
        verb = raw.strip()
        if not verb:
            fail("%s VERBS declares an empty name" % name)
        if len(verb) > 96 or any(c not in VERB_ALPHABET for c in verb):
            fail("%s VERBS name %r is not a lowercase SafeToken" % (name, verb))
        if verb in SCRIPT_VERBS or verb in RESERVED_VERBS:
            fail("%s VERBS name %r is reserved by the harness" % (name, verb))
        if verb in chosen:
            fail("%s VERBS names %r more than once" % (name, verb))
        chosen.append(verb)
    return tuple(chosen)


def parse_timeout(value: str, name: str) -> int:
    if not value:
        return DEFAULT_TIMEOUT
    if not value.isdigit() or not value.isascii():
        fail("%s declares a non-decimal TIMEOUT %r" % (name, value))
    seconds = int(value)
    if seconds < 1 or seconds > MAX_TIMEOUT:
        fail("%s TIMEOUT %d is outside 1..%d" % (name, seconds, MAX_TIMEOUT))
    return seconds


def script_words(script: str, name: str, extra: tuple[str, ...] = ()) -> list[str]:
    """Semicolon-separated verbs to the shell words `Tools/prepare-scenario.sh` seals.

    A leading `@` names a sibling script file, one verb per line, for a persona whose verb list is
    long enough that a single line would hide a mistake.
    """
    if script.startswith("@"):
        path = os.path.join(os.path.dirname(os.path.abspath(__file__)), script[1:])
        if not os.path.isfile(path):
            fail("%s names missing script file %s" % (name, script[1:]))
        with open(path, encoding="utf-8") as handle:
            steps = [
                line.strip()
                for line in handle.read().splitlines()
                if line.strip() and not line.strip().startswith("#")
            ]
    else:
        steps = [step.strip() for step in script.split(";")]
    words: list[str] = []
    for step in steps:
        if not step:
            fail("%s SCRIPT declares an empty verb" % name)
        parts = step.split()
        if parts[0] == COUNTED_VERB:
            if len(parts) != 2:
                fail("%s SCRIPT step %r needs exactly 'advance <turns>'" % (name, step))
            count = parts[1]
            if not count.isdigit() or not count.isascii():
                fail(
                    "%s SCRIPT advance count must be decimal digits: %r" % (name, count)
                )
            if not 1 <= int(count) <= MAX_ADVANCE_TURNS:
                fail(
                    "%s SCRIPT advance count %s is outside 1..%d"
                    % (name, count, MAX_ADVANCE_TURNS)
                )
        elif len(parts) != 1 or (
            parts[0] not in SCRIPT_VERBS and parts[0] not in extra
        ):
            fail(
                "%s SCRIPT step %r is not a sealable verb; the set is %s, advance <turns>, "
                "plus any name this persona declares under VERBS"
                % (name, step, ", ".join(SCRIPT_VERBS))
            )
        words.extend(parts)
    return words


def parse_expect(
    spec: str, name: str, extra: tuple[str, ...] = ()
) -> list[tuple[str, str, str]]:
    """`verb:OK[~code], ..., TERMINAL[~code]` to `(verb-row, outcome, substring)` triples."""
    items = [item.strip() for item in spec.split(",")]
    parsed: list[tuple[str, str, str]] = []
    for index, item in enumerate(items):
        if not item:
            fail("%s EXPECT declares an empty item" % name)
        body, _, wanted = item.partition("~")
        body = body.strip()
        terminal = body in TERMINALS
        if terminal and index != len(items) - 1:
            fail("%s EXPECT names terminal %r before the end" % (name, body))
        if not terminal and index == len(items) - 1:
            fail(
                "%s EXPECT ends on %r; the last item must be one of %s"
                % (name, body, ", ".join(sorted(TERMINALS)))
            )
        if terminal:
            parsed.append((TERMINALS[body], "", wanted.strip()))
            continue
        verb, sep, outcome = body.partition(":")
        if not sep or outcome not in OUTCOMES:
            fail(
                "%s EXPECT item %r is not '<verb>:OK' or '<verb>:REFUSED'"
                % (name, item)
            )
        if verb not in SCRIPT_VERBS and verb != COUNTED_VERB and verb not in extra:
            fail("%s EXPECT item %r names an unsealable verb" % (name, item))
        parsed.append((verb, outcome, wanted.strip()))
    return parsed


# --------------------------------------------------------------------------------------
# Journal
# --------------------------------------------------------------------------------------


def unescape(value: str) -> str:
    """Reverses KingdomScenarioJournal.Escape, backslash last so the escape itself round-trips."""
    out: list[str] = []
    index = 0
    while index < len(value):
        char = value[index]
        if char == "\\" and index + 1 < len(value):
            nxt = value[index + 1]
            if nxt in "nrt\\":
                out.append({"n": "\n", "r": "\r", "t": "\t", "\\": "\\"}[nxt])
                index += 2
                continue
        out.append(char)
        index += 1
    return "".join(out)


def read_journal(text: str) -> list[tuple[str, str, str]]:
    """Every row as `(verb, outcome, message)`. A malformed row is a fault, never a skip."""
    rows: list[tuple[str, str, str]] = []
    for number, line in enumerate(text.splitlines(), 1):
        if not line.strip():
            continue
        fields = line.split("\t")
        if len(fields) != 4:
            fail("journal line %d has %d columns, not 4" % (number, len(fields)))
        if fields[2] not in OUTCOMES:
            fail("journal line %d has unknown outcome %r" % (number, fields[2]))
        rows.append((unescape(fields[1]), fields[2], unescape(fields[3])))
    return rows


def significant(rows: list[tuple[str, str, str]]) -> list[tuple[str, str, str]]:
    return [row for row in rows if row[0] not in BOOKKEEPING]


def terminal_row(rows: list[tuple[str, str, str]]) -> str:
    for verb, _, _ in rows:
        if verb in TERMINALS.values():
            return verb
    return ""


# --------------------------------------------------------------------------------------
# Verdict
# --------------------------------------------------------------------------------------


def match(
    expectations: list[tuple[str, str, str]], rows: list[tuple[str, str, str]]
) -> list[str]:
    problems: list[str] = []
    for index in range(max(len(expectations), len(rows))):
        if index >= len(expectations):
            problems.append(
                "row %d unexpected: %s/%s" % (index + 1, rows[index][0], rows[index][1])
            )
            continue
        verb, outcome, wanted = expectations[index]
        if index >= len(rows):
            problems.append(
                "row %d missing: expected %s%s"
                % (index + 1, verb, ":" + outcome if outcome else "")
            )
            continue
        actual_verb, actual_outcome, message = rows[index]
        if actual_verb != verb:
            problems.append(
                "row %d verb %s, expected %s" % (index + 1, actual_verb, verb)
            )
            continue
        if outcome and actual_outcome != outcome:
            problems.append(
                "row %d %s is %s, expected %s"
                % (index + 1, verb, actual_outcome, outcome)
            )
        if wanted and wanted not in message:
            problems.append("row %d %s message lacks %r" % (index + 1, verb, wanted))
    return problems


def status_digest_stable(rows: list[tuple[str, str, str]]) -> list[str]:
    """The measured digests a `status` reports must still read the same on a later `status`."""
    digests = [DIGEST.findall(message) for verb, _, message in rows if verb == "status"]
    if len(digests) < 2:
        return ["status-digest-stable needs two status rows, found %d" % len(digests)]
    if not digests[0]:
        return ["status-digest-stable found no 64-hex digest in the first status row"]
    if digests[0] != digests[-1]:
        return [
            "status digests moved: %s then %s"
            % (",".join(digests[0]), ",".join(digests[-1]))
        ]
    return []


def assess(manifest: dict, journal: str, name: str) -> list[str]:
    rows = significant(read_journal(journal))
    extra = tuple(v for v in manifest.get("VERBS", "").split(",") if v)
    problems = match(parse_expect(manifest["EXPECT"], name, extra), rows)
    if manifest.get("CHECK") == "status-digest-stable":
        problems.extend(status_digest_stable(rows))
    return problems


# --------------------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------------------


def load(path: str) -> tuple[dict, str]:
    name = os.path.basename(path)
    with open(path, encoding="utf-8") as handle:
        return parse_manifest(handle.read(), name), name


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        fail(
            "usage: persona_matrix.py <fields|assert|terminal|warnings> <persona|journal>"
            " [journal]"
        )
    action = argv[1]
    if action == "fields" and len(argv) == 3:
        manifest, _ = load(argv[2])
        for key in (
            "REQUEST",
            "SCRIPT_WORDS",
            "START",
            "CHECK",
            "TIMEOUT",
            "VERBS",
            "DESCRIPTION",
        ):
            print("%s\t%s" % (key.lower(), manifest.get(key, "")))
        return 0
    if action == "terminal" and len(argv) == 3:
        with open(argv[2], encoding="utf-8") as handle:
            print(terminal_row(read_journal(handle.read())))
        return 0
    if action == "warnings" and len(argv) == 3:
        # Bookkeeping rows an operator still has to see: a refused third-party verb provider does
        # not fail a persona, but a matrix report that never mentioned it would be lying by
        # omission about the profile the run happened in.
        with open(argv[2], encoding="utf-8") as handle:
            rows = read_journal(handle.read())
        notes = [message for verb, _, message in rows if verb == "VERB-REFUSED"]
        print(" | ".join(notes))
        return 0
    if action == "assert" and len(argv) == 4:
        manifest, name = load(argv[2])
        with open(argv[3], encoding="utf-8") as handle:
            problems = assess(manifest, handle.read(), name)
        if problems:
            print("; ".join(problems))
            return 1
        print("expectations met")
        return 0
    fail("unknown action or wrong argument count: " + " ".join(argv[1:]))
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
