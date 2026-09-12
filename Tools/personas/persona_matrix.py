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

import json
import importlib.util
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
        "TESTGROUND-RESTRIP",
        "RUNNER-ARMED",
        "SCRIPT-BEGIN",
        "advance-progress",
        "advance-complete",
        # Native run 36 (13122f0) / run 39 investigation: the founder guard's own start/end rows
        # around EVERY scripted advance, on every road (Harness/KingdomScenarioFounderGuard.cs) -
        # the founder's cell and the guard state, read at arming and at release. Wiring, not a
        # verb the script asked for; Tools/upgrade_profile_witnesses.py expects exactly two.
        "advance-guard",
        "yield-frames-complete",
        "travel-out-complete",
        "travel-return-complete",
        # Written once per quickstart-lifecycle boot, right after QUICKSTART-BOOT-BEGIN
        # (Harness/KingdomQuickstartLifecycleRunnerPatch.cs), never for quickstart-boot/-save/
        # -build. It describes the runner's OWN wiring for this run, not a verb the script asked
        # for, so a lifecycle persona's positional EXPECT must not have to name it.
        "LIFECYCLE-RUNNER",
        # Native run 17 (f691ab4): the untruncated structured reading behind a "lifecycle-grown"
        # stall refusal (Harness/KingdomQuickstartLifecycleStall.cs DetailRow). It is an
        # observation about what the checker's own diagnosis found, never a verb the script
        # asked for, so it never belongs in a positional EXPECT.
        "lifecycle-grown-detail",
        # Run 46b/47 (investigation C): the lifecycle save's own pre-activation witness row
        # (Harness/KingdomQuickstartLifecycleLoad.cs BeforeActivation), landed by the load
        # witness before AfterGameLoaded handlers run. Wiring, never a verb the script asked for,
        # and deliberately NOT a QUICKSTART-LOAD-* row: Tools/check-quickstart-lifecycle.py picks
        # the cold-load row group by presence, and a lone Quickstart-named row beside
        # lifecycle-loaded would flip that choice into "partial evidence".
        "lifecycle-preactivation",
        "quickstart-settlement",
        # A third-party verb provider the admission law refused. It describes the PROFILE a run was
        # launched into, not a step the script asked for, so a persona must not go red because
        # somebody else's mod shipped a broken provider. `Tools/run-personas.sh` surfaces these
        # rows in its report either way, pass or fail.
        "VERB-REFUSED",
    }
)

# Must equal scenario_profile.SCRIPT_VERBS and its one counted verb. A persona that seals a verb
# the profile tool refuses would spend a whole prepare discovering it.
SCRIPT_VERBS = (
    "anchor",
    "fit",
    "flatten",
    "frame",
    "ground",
    "light",
    "list",
    "realize",
    "reload",
    "resourcedigest",
    "stagedigest",
    "standingdigest",
    "status",
)
COUNTED_VERB = "advance"
MAX_ADVANCE_TURNS = 10000

# The one Quickstart command a script may open with (Harness/KingdomQuickstartBootRequest.cs
# LifecycleVerb). Unlike every other sealable step it is three words long and may appear
# only as the very first step; the ordinary AutoRunner verbs that follow it are parsed
# exactly as they always were. Must equal Tools/scenario_profile.py
# QUICKSTART_LIFECYCLE_VERB/QUICKSTART_PROFILES.
QUICKSTART_LIFECYCLE_VERB = "quickstart-lifecycle"
QUICKSTART_PROFILES = ("marsh", "canyon", "dunes")

# The Quickstart production evidence rows a quickstart-lifecycle boot lands BEFORE the
# AutoRunner ever starts (Harness/KingdomQuickstartBootTest.cs, Harness/
# KingdomQuickstartBuildTest.cs). Not bookkeeping -- they are the substantive evidence a
# quickstart-flavoured persona pins -- but their SHOUTING-CASE names fall outside the
# lowercase-only VERBS= alphabet, so they are named here once instead of forcing every such
# persona to widen VERB_ALPHABET. Mirrors Tools/check-quickstart-lifecycle.py BOOT_ROWS +
# BUILD_ROWS, plus the lifecycle-only stamp row.
QUICKSTART_EVIDENCE_ROWS = (
    "QUICKSTART-BOOT-BEGIN",
    "QUICKSTART-BOOT-OBSERVED",
    "QUICKSTART-BOOT-COMPLETE",
    "QUICKSTART-BUILD-BEGIN",
    "QUICKSTART-BUILD-QUOTE",
    "QUICKSTART-BUILD-CANPAY",
    "QUICKSTART-BUILD-COMMISSION",
    "QUICKSTART-BUILD-COMPLETE",
    "QUICKSTART-LIFECYCLE-PROFILE",
)

# The second counted verb. `yield-frames <frames>` hands the engine back its own render loop, which
# an advance never does: advance keeps the engine out of XRLCore.PlayerTurn on purpose, and that is
# exactly where the per-frame BeforeRenderEvent dispatch lives. Must equal
# KingdomScenarioFrames.MaxFrames and scenario_profile.MAX_YIELD_FRAMES.
FRAMES_VERB = "yield-frames"
MAX_YIELD_FRAMES = 240
COUNTED_VERBS = {COUNTED_VERB: MAX_ADVANCE_TURNS, FRAMES_VERB: MAX_YIELD_FRAMES}

# Names the runtime dispatches itself, which no third-party provider may claim. Must equal
# Harness/KingdomScenarioVerbProvider.cs KingdomScenarioVerbApi.Reserved.
RESERVED_VERBS = (
    "advance",
    "anchor",
    "arcology",
    "capture",
    "fit",
    "flatten",
    "frame",
    "ground",
    "help",
    "light",
    "list",
    "realize",
    "reload",
    "resourcedigest",
    "stagedigest",
    "standingdigest",
    "status",
    "yield-frames",
)

# The alphabet KingdomScenarioRules.SafeToken admits, restated so a persona is refused here
# rather than in a sealed profile nobody can retry.
VERB_ALPHABET = "abcdefghijklmnopqrstuvwxyz" + "0123456789" + "-."

OUTCOMES = ("OK", "REFUSED")
CHECKS = (
    "status-digest-stable",
    "travel-away",
    "travel-present",
    "travel-economic-away",
    "travel-economic-present",
)

REQUIRED_KEYS = ("REQUEST", "SCRIPT", "EXPECT")
OPTIONAL_KEYS = (
    "START",
    "CHECK",
    "TIMEOUT",
    "DESCRIPTION",
    "VERBS",
    "SET",
    "LOG_EXPECT",
    "LOG_FORBID",
)

# Tags a persona may carry so `run-personas.sh --set <tag>` can run a named slice of the matrix.
# Same alphabet as verbs: lowercase, digits, hyphen, dot. Order inside SET= is not significant.
SET_TAG = re.compile(r"^[a-z0-9][a-z0-9.-]*$")

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
    if found["SCRIPT"].startswith("reload-descendant "):
        parts = found["SCRIPT"].split()
        if (
            len(parts) != 4
            or parts[:2] != ["reload-descendant", "quickstart"]
            or parts[2] not in ("marsh", "canyon", "dunes")
            or parts[3] not in ("yes", "no")
        ):
            fail(
                name
                + " reload requires exactly: reload-descendant quickstart <marsh|canyon|dunes> <yes|no>"
            )
        if (
            found["EXPECT"] != "RELOAD-COMPLETE"
            or found["REQUEST"] != "founding-first-city"
            or any(
                found.get(key)
                for key in ("START", "CHECK", "VERBS", "LOG_EXPECT", "LOG_FORBID")
            )
        ):
            fail(
                name
                + " reload requires founding-first-city, EXPECT=RELOAD-COMPLETE and no overrides"
            )
        found["SCRIPT_WORDS"] = "quickstart-save " + " ".join(parts[2:])
        found["RELOAD"] = "quickstart"
        found["TIMEOUT"] = str(parse_timeout(found.get("TIMEOUT", ""), name))
        found["SET"] = ",".join(parse_set(found.get("SET", ""), name))
        return found
    found["SCRIPT_WORDS"] = " ".join(script_words(found["SCRIPT"], name, extra))
    parse_expect(found["EXPECT"], name, extra)
    check = found.get("CHECK", "")
    if check and check not in CHECKS:
        fail(
            "%s declares unknown CHECK %r; the set is %s"
            % (name, check, ", ".join(CHECKS))
        )
    found["TIMEOUT"] = str(parse_timeout(found.get("TIMEOUT", ""), name))
    found["SET"] = ",".join(parse_set(found.get("SET", ""), name))
    if "LOG_EXPECT" in found:
        found["LOG_EXPECT"] = json.dumps(
            parse_log_expect(found["LOG_EXPECT"], name),
            ensure_ascii=False,
            separators=(",", ":"),
        )
    if "LOG_FORBID" in found:
        found["LOG_FORBID"] = json.dumps(
            parse_log_forbid(found["LOG_FORBID"], name),
            ensure_ascii=False, separators=(",", ":"),
        )
    return found


def parse_log_expect(value: str, name: str) -> list[str]:
    """A bounded list of complete literal lines, never a regex or substring allowance."""
    if len(value) > 8192:
        fail("%s LOG_EXPECT exceeds 8192 characters" % name)
    try:
        lines = json.loads(value)
    except (ValueError, RecursionError):
        fail("%s LOG_EXPECT must be a JSON array of literal lines" % name)
    if not isinstance(lines, list) or not 1 <= len(lines) <= 4:
        fail("%s LOG_EXPECT must contain 1..4 literal lines" % name)
    if any(
        not isinstance(line, str)
        or not 1 <= len(line) <= 1024
        or not line.isprintable()
        for line in lines
    ):
        fail("%s LOG_EXPECT lines must be 1..1024 printable characters" % name)
    if len(set(lines)) != len(lines) or sum(map(len, lines)) > 8192:
        fail(
            "%s LOG_EXPECT lines must be unique and total at most 8192 characters"
            % name
        )
    return lines


def expected_log(manifest: dict, raw: bytes, name: str) -> bytes:
    """Validate all expected diagnostics before returning a derivative; never alter raw input."""
    if "LOG_EXPECT" not in manifest:
        fail("%s requires LOG_EXPECT for expected-log" % name)
    expected = {
        line.encode("utf-8") for line in parse_log_expect(manifest["LOG_EXPECT"], name)
    }
    lines = raw.replace(b"\r\n", b"\n").split(b"\n")
    if any(lines.count(line) != 1 for line in expected):
        fail("%s LOG_EXPECT requires every declared line exactly once" % name)
    if any(
        line not in expected and (b"MODERROR" in line or b"MODWARN" in line)
        for line in lines
    ):
        fail("%s contains an undeclared MODERROR or MODWARN line" % name)
    return b"".join(
        line + (b"\n" if index < len(lines) - 1 else b"")
        for index, line in enumerate(lines)
        if line not in expected
    )


def parse_log_forbid(value: str, name: str) -> list[str]:
    """Diagnostics that must NOT appear in Player.log at all.

    LOG_EXPECT allows a known-benign line through the checker; this is its opposite and is not a
    weaker form of it. Each entry is a literal SUBSTRING, because the lines that matter carry a
    tick or an id the persona cannot know in advance, and one occurrence anywhere fails the run.
    Bounded exactly like LOG_EXPECT so a persona cannot smuggle a regex or an essay in here.
    """
    if len(value) > 8192:
        fail("%s LOG_FORBID exceeds 8192 characters" % name)
    try:
        lines = json.loads(value)
    except (ValueError, RecursionError):
        fail("%s LOG_FORBID must be a JSON array of literal substrings" % name)
    if not isinstance(lines, list) or not 1 <= len(lines) <= 4:
        fail("%s LOG_FORBID must contain 1..4 literal substrings" % name)
    if any(not isinstance(line, str) or not 1 <= len(line) <= 1024
           or not line.isprintable() for line in lines):
        fail("%s LOG_FORBID lines must be 1..1024 printable characters" % name)
    if len(set(lines)) != len(lines) or sum(map(len, lines)) > 8192:
        fail("%s LOG_FORBID lines must be unique and total at most 8192 characters" % name)
    return lines


def forbidden_log(manifest: dict, raw: bytes, name: str) -> list[str]:
    """Every forbidden substring that DID appear, with the first line each was seen on."""
    if "LOG_FORBID" not in manifest:
        fail("%s requires LOG_FORBID for forbidden-log" % name)
    found: list[str] = []
    lines = raw.replace(b"\r\n", b"\n").split(b"\n")
    for forbidden in parse_log_forbid(manifest["LOG_FORBID"], name):
        needle = forbidden.encode("utf-8")
        for number, line in enumerate(lines, 1):
            if needle in line:
                found.append("line %d: %s" % (number, forbidden))
                break
    return found


def parse_set(value: str, name: str) -> tuple[str, ...]:
    """Set tags, deduplicated in declaration order. An empty SET= means untagged."""
    chosen: list[str] = []
    for raw in value.split(","):
        tag = raw.strip()
        if not tag:
            continue
        if not SET_TAG.match(tag):
            fail("%s declares malformed set tag %r" % (name, tag))
        if tag not in chosen:
            chosen.append(tag)
    return tuple(chosen)


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
    for index, step in enumerate(steps):
        if not step:
            fail("%s SCRIPT declares an empty verb" % name)
        parts = step.split()
        if parts[0] == QUICKSTART_LIFECYCLE_VERB:
            if index != 0:
                fail(
                    "%s SCRIPT names %r after its first step; a Quickstart command may "
                    "appear only once, at the start" % (name, QUICKSTART_LIFECYCLE_VERB)
                )
            if (
                len(parts) != 3
                or parts[1] not in QUICKSTART_PROFILES
                or parts[2] not in ("yes", "no")
            ):
                fail(
                    "%s SCRIPT step %r needs exactly '%s <marsh|canyon|dunes> <yes|no>'"
                    % (name, step, QUICKSTART_LIFECYCLE_VERB)
                )
        elif parts[0] in COUNTED_VERBS:
            bound = COUNTED_VERBS[parts[0]]
            if len(parts) != 2:
                fail(
                    "%s SCRIPT step %r needs exactly '%s <count>'"
                    % (name, step, parts[0])
                )
            count = parts[1]
            if not count.isdigit() or not count.isascii():
                fail(
                    "%s SCRIPT %s count must be decimal digits: %r"
                    % (name, parts[0], count)
                )
            if not 1 <= int(count) <= bound:
                fail(
                    "%s SCRIPT %s count %s is outside 1..%d"
                    % (name, parts[0], count, bound)
                )
        elif len(parts) != 1 or (
            parts[0] not in SCRIPT_VERBS and parts[0] not in extra
        ):
            fail(
                "%s SCRIPT step %r is not a sealable verb; the set is %s, advance <turns>, "
                "yield-frames <frames>, plus any name this persona declares under VERBS"
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
        if (
            verb not in SCRIPT_VERBS
            and verb not in COUNTED_VERBS
            and verb not in extra
            and verb not in QUICKSTART_EVIDENCE_ROWS
        ):
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
    if manifest.get("RELOAD"):
        return [
            "reload requires both strict Quickstart checks and receipt-owned process workflow; journal alone is insufficient"
        ]
    rows = significant(read_journal(journal))
    extra = tuple(v for v in manifest.get("VERBS", "").split(",") if v)
    problems = match(parse_expect(manifest["EXPECT"], name, extra), rows)
    if manifest.get("CHECK") == "status-digest-stable":
        problems.extend(status_digest_stable(rows))
    if manifest.get("CHECK", "").startswith("travel-"):
        spec = importlib.util.spec_from_file_location(
            "taf_persona_travel",
            os.path.join(os.path.dirname(__file__), "persona_travel.py"),
        )
        travel = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(travel)
        mode = manifest["CHECK"][len("travel-") :]
        economic = mode.startswith("economic-")
        problems.extend(
            travel.assess(
                read_journal(journal),
                mode.removeprefix("economic-"),
                require_economic=economic,
            )
        )
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
            "usage: persona_matrix.py <fields|assert|terminal|warnings|expected-log|forbidden-log>"
            " <persona|journal>"
            " [journal|Player.log]"
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
            "SET",
            "LOG_EXPECT",
            "LOG_FORBID",
            "RELOAD",
        ):
            print("%s\t%s" % (key.lower(), manifest.get(key, "")))
        return 0
    if action == "expected-log" and len(argv) == 4:
        manifest, name = load(argv[2])
        with open(argv[3], "rb") as handle:
            filtered = expected_log(manifest, handle.read(), name)
        sys.stdout.buffer.write(filtered)
        return 0
    if action == "forbidden-log" and len(argv) == 4:
        manifest, name = load(argv[2])
        with open(argv[3], "rb") as handle:
            seen = forbidden_log(manifest, handle.read(), name)
        if seen:
            print("; ".join(seen))
            return 1
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
