#!/usr/bin/env python3
"""Profile-local edits and the closed inventory seal for the developer scenario harness.

Every operation here writes only inside a throwaway scenario profile. The repository manifest, the
authored scenario roster, and the shipped option set are never touched: keeping these edits out of
the tree is what lets Tools/check-manifest-directories.py and Tools/portable-check.sh keep proving
that the shipped selection carries no harness.

The seal is CLOSED and computed over the COMPLETE profile. Sealing an expected subset would let an
injected .cs or .xml compile while the seal stayed green, so verification compares the exact
normalized regular-file inventory in both directions and rejects links, reparse points, duplicate
normalizations, and anything that is not a regular file.
"""

from __future__ import annotations

import hashlib
import json
import os
import stat
import sys
import unicodedata

SEAL_HEADER = "taf-scenario-profile-seal-v1"
MIN_SEED = 0
MAX_SEED = 2147483647

# Windows FILE_ATTRIBUTE_REPARSE_POINT. Present on os.stat_result only on Windows builds; the
# launcher checks the same flag, and neither side may narrow the other's sealed inventory.
REPARSE_POINT = 0x400


def fail(message: str) -> None:
    raise SystemExit("scenario profile: " + message)


# --------------------------------------------------------------------------------------
# Seed
# --------------------------------------------------------------------------------------


def validate_seed(seed: str) -> str:
    """Exact ^#[0-9]+$ plus the engine's accepted range.

    A shell glob such as '#[0-9]*' admits non-digits after the first digit, which would let a
    malformed seed reach the request and make the gate's proof meaningless.

    The lower bound is ZERO, not one. The installed XRLGame.GetWorldSeed parses the digits with
    int.TryParse and returns the parsed value, so '#0' is an exact lawful world seed and refusing it
    would refuse a world the engine can actually reproduce. Signs, whitespace, and overflow stay
    rejected because int.TryParse under the harness's exact syntax never sees them.
    """
    if not seed or seed[0] != "#":
        fail("seed must start with '#': " + repr(seed))
    digits = seed[1:]
    if not digits or not all(c in "0123456789" for c in digits):
        fail("seed must be '#' followed by digits only: " + repr(seed))
    value = int(digits)
    if value < MIN_SEED or value > MAX_SEED:
        fail(
            "seed is outside the engine Int32 range %d..%d: %s"
            % (MIN_SEED, MAX_SEED, seed)
        )
    return seed


# --------------------------------------------------------------------------------------
# Profile-local edits
# --------------------------------------------------------------------------------------


def write_request(embark_path: str) -> None:
    request = os.environ.get("TAF_REQUEST", "")
    if not request:
        fail("TAF_REQUEST is empty")
    if ";seed=" not in request:
        fail("the generated request carries no frozen seed: " + request)
    validate_seed(request.split(";seed=", 1)[1])
    with open(embark_path, encoding="utf-8") as handle:
        text = handle.read()
    marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
    start = text.find(marker)
    if start < 0:
        fail("the embark module declares no scenario request state")
    start += len(marker)
    end = text.find('"', start)
    if end < 0:
        fail("the scenario request state is unterminated")
    with open(embark_path, "w", encoding="utf-8") as handle:
        handle.write(text[:start] + request + text[end:])
    print("generated request: " + request)


# The engine's biome arrays are exactly 80x25 parasangs and a surface zone is exactly 80x25 cells.
# An off-map parasang does not refuse, it crashes zone generation, so this is a hard bound on both.
WORLD_WIDTH = 80
WORLD_HEIGHT = 25
ZONE_WIDTH = 80
ZONE_HEIGHT = 25

# The surface layer of the middle 3x3 sub-cell, matching the shipped default and the only shape the
# ground scout walks (it refuses any z but 10).
START_ZONE_SUFFIX = "1.1.10"
START_DEFAULT_CELL = (40, 12)
START_MARKER = 'ID="TAFTestGround"'
START_ATTRIBUTE = 'Location="'


def parse_start(spec: str) -> str:
    """`<wx>.<wy>[@x,y]` to the engine's own GlobalLocation string, bounds-checked."""
    body, _, cell = spec.partition("@")
    parts = body.split(".")
    if len(parts) != 2 or not all(p.isdigit() and p.isascii() for p in parts):
        fail("start parasang must be '<wx>.<wy>': " + repr(spec))
    wx, wy = int(parts[0]), int(parts[1])
    if wx >= WORLD_WIDTH or wy >= WORLD_HEIGHT:
        fail(
            "start parasang %d.%d is off the %dx%d world map"
            % (wx, wy, WORLD_WIDTH, WORLD_HEIGHT)
        )
    x, y = START_DEFAULT_CELL
    if cell:
        coords = cell.split(",")
        if len(coords) != 2 or not all(c.isdigit() and c.isascii() for c in coords):
            fail("start cell must be '@x,y': " + repr(spec))
        x, y = int(coords[0]), int(coords[1])
        if x >= ZONE_WIDTH or y >= ZONE_HEIGHT:
            fail(
                "start cell %d,%d is outside the %dx%d zone"
                % (x, y, ZONE_WIDTH, ZONE_HEIGHT)
            )
    return "GlobalLocation:JoppaWorld.%d.%d.%s@%d,%d" % (
        wx,
        wy,
        START_ZONE_SUFFIX,
        x,
        y,
    )


def write_start(embark_path: str, spec: str) -> None:
    """Rewrites the profile copy's dev starting location, or reports the shipped default.

    An empty spec is not an error: it is how prepare-scenario.sh asks what the sealed profile is
    about to start on, so the chosen zone is always echoed beside the frozen seed whether or not
    the operator overrode it.
    """
    with open(embark_path, encoding="utf-8") as handle:
        text = handle.read()
    marker = text.find(START_MARKER)
    if marker < 0:
        fail("the embark module declares no dev starting location")
    start = text.find(START_ATTRIBUTE, marker)
    if start < 0:
        fail("the dev starting location declares no Location attribute")
    start += len(START_ATTRIBUTE)
    end = text.find('"', start)
    if end < 0:
        fail("the dev starting location's Location attribute is unterminated")
    if not spec:
        print("start zone: " + text[start:end] + " (profile default)")
        return
    location = parse_start(spec)
    with open(embark_path, "w", encoding="utf-8") as handle:
        handle.write(text[:start] + location + text[end:])
    print("start zone: " + location + " (TAF_SCENARIO_START)")


def write_manifest(source: str, destination: str) -> None:
    with open(source, encoding="utf-8") as handle:
        manifest = json.loads(handle.read())
    rows = manifest.get("Directories")
    if not isinstance(rows, list) or not rows:
        fail("manifest has no Directories rows")
    paths = rows[0].get("Paths")
    if not isinstance(paths, list) or not paths:
        fail("the first Directories row has no Paths")
    if "/Harness/" in paths:
        fail("the shipped manifest already selects the harness")
    paths.append("/Harness/")
    manifest["title"] = str(manifest.get("title", "")) + " [DEV SCENARIO HARNESS]"
    with open(destination, "w", encoding="utf-8") as handle:
        handle.write(json.dumps(manifest, indent=2) + "\n")
    print("dev manifest selects /Harness/")


# The verbs a SEALED script may name. Closed on purpose, and deliberately narrower than the wish:
#
#   - the bare help verb prints a usage page and proves nothing, so a script that named it would
#     spend a run saying what the operator already read;
#   - `capture` founds anchor evidence and is fail-closed on ORDINARY play, so inside a prepared
#     scenario profile it can only ever refuse. Sealing it would seal a guaranteed stop. Curating an
#     anchor stays a reviewer's deliberate act in an ordinary game, exactly as the ruling requires.
#
# The runtime verb set lives in Harness/KingdomScenarioVerbs.cs and stays the authority on what a
# verb DOES; this list only decides what may be sealed into an unattended run.
SCRIPT_VERBS = ("anchor", "flatten", "ground", "list", "realize", "status")

# The one verb that takes an argument. `advance <turns>` runs game turns with no player input, for
# behaviour that only happens on a clock (settlement simulation ticks on turns). The bound below
# must equal KingdomScenarioAdvance.MaxTurns: sealing a count the runtime refuses would spend a
# whole non-retryable profile discovering a disagreement between this file and that one.
COUNTED_VERB = "advance"
MAX_ADVANCE_TURNS = 10000

# The ordinary phase-1 run: prepare the ground, commit the single production transaction, then read
# back the durable stamp and its differential verdict.
DEFAULT_SCRIPT = ("flatten", "realize", "status")

# Names the runtime dispatches itself and no provider may claim. Must equal
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

SCRIPT_HEADER = (
    "# Sealed developer scenario script. One kingdom:scenario verb per line, executed once by\n"
    "# ThousandAndFirst.KingdomScenarioAutoRunner on the first player action opportunity in the\n"
    "# built world. 'advance <turns>' suspends the script for that many game turns and then\n"
    "# resumes at the next line. Execution stops at the first REFUSED verb. Results land in the\n"
    "# profile root's scenario-journal.tsv.\n"
    "# Written by Tools/prepare-scenario.sh BEFORE the profile seal, so this file is sealed content.\n"
)


def parse_extra_verbs(raw: str) -> tuple[str, ...]:
    """Comma-separated verb names another mod's provider contributes to THIS profile.

    The sealed set above stays closed on purpose, so a third-party verb has to be named for the
    profile that will run it rather than admitted globally. Names are held to the runtime's own
    SafeToken shape and may not shadow a verb the harness dispatches itself, because
    KingdomScenarioVerbApi refuses such a provider outright and sealing the name would guarantee a
    stopped run.
    """
    if not raw:
        return ()
    chosen: list[str] = []
    for name in raw.split(","):
        verb = name.strip()
        if not verb:
            fail("extra scenario verb list carries an empty name: " + repr(raw))
        if len(verb) > 96 or any(
            c not in "abcdefghijklmnopqrstuvwxyz0123456789-." for c in verb
        ):
            fail(
                "extra scenario verb %r is not a lowercase SafeToken; the runtime would "
                "refuse the provider that claimed it" % verb
            )
        if verb in SCRIPT_VERBS or verb in RESERVED_VERBS:
            fail("extra scenario verb %r is reserved by the harness" % verb)
        if verb in chosen:
            fail("extra scenario verb %r is named more than once" % verb)
        chosen.append(verb)
    return tuple(chosen)


def parse_script(tokens: list[str], extra: tuple[str, ...] = ()) -> list[str]:
    """Shell words to script LINES, closed on both the verb set and the one argument.

    The words arrive unquoted from prepare-scenario.sh, so `advance 1200` is two words that must
    become one line. The count is validated here rather than left to the runtime: a sealed profile
    is not retryable, and a malformed count discovered in game costs a whole run.
    """
    lines: list[str] = []
    index = 0
    while index < len(tokens):
        verb = tokens[index]
        index += 1
        if verb == COUNTED_VERB:
            if index >= len(tokens):
                fail("script verb 'advance' needs a turn count, as 'advance 1200'")
            count = tokens[index]
            index += 1
            if not count.isdigit() or not count.isascii():
                fail("advance turn count must be decimal digits only: " + repr(count))
            value = int(count)
            if value < 1 or value > MAX_ADVANCE_TURNS:
                fail(
                    "advance turn count %d is outside 1..%d"
                    % (value, MAX_ADVANCE_TURNS)
                )
            lines.append("%s %d" % (COUNTED_VERB, value))
            continue
        if verb not in SCRIPT_VERBS and verb not in extra:
            fail(
                "unknown scenario script verb %r; the sealed set is %s, advance <turns>"
                "%s"
                % (
                    verb,
                    ", ".join(SCRIPT_VERBS),
                    (
                        ", plus this profile's " + ", ".join(extra)
                        if extra
                        else " (TAF_SCENARIO_EXTRA_VERBS names third-party verbs)"
                    ),
                )
            )
        lines.append(verb)
    return lines


def write_script(destination: str, verbs: list[str]) -> None:
    extra = parse_extra_verbs(os.environ.get("TAF_SCENARIO_EXTRA_VERBS", ""))
    if extra:
        print("profile admits third-party scenario verbs: " + ", ".join(extra))
    chosen = parse_script(list(verbs), extra) if verbs else list(DEFAULT_SCRIPT)
    if not chosen:
        fail("the scenario script would declare no verbs")
    with open(destination, "w", encoding="utf-8") as handle:
        handle.write(SCRIPT_HEADER + "\n".join(chosen) + "\n")
    print("sealed scenario script: " + ", ".join(chosen))


def write_options(source: str, destination: str) -> None:
    with open(source, encoding="utf-8") as handle:
        options = json.loads(handle.read())
    if not isinstance(options, dict):
        fail("the smoke PlayerOptions template is not an object")
    # No shipped option declares OptionEnableSeed, so this exists only inside this profile. It
    # exposes the native world-seed field; the OPERATOR enters the sealed seed. Qud offers no
    # launcher-side injection, so this is never automatic.
    options["OptionEnableSeed"] = "Yes"
    with open(destination, "w", encoding="utf-8") as handle:
        handle.write(json.dumps(options, indent=2) + "\n")
    print("scenario profile exposes the native world-seed field for operator entry")


# --------------------------------------------------------------------------------------
# Closed inventory seal
# --------------------------------------------------------------------------------------


def normalize(relative: str) -> str:
    """One unambiguous spelling per path, so two entries cannot normalize to the same name."""
    return unicodedata.normalize("NFC", relative.replace(os.sep, "/")).casefold()


def refuse_links(full: str, directory: bool) -> None:
    """Refuse every alias for one byte stream, not only the symbolic ones.

    os.path.islink answers False for a HARD link, because a hard link is not a distinct kind of
    entry: it is a second name for the same inode. A hard-linked file therefore sails through a
    symlink check while still letting the sealed bytes be replaced from outside the profile. The
    link count is the fact that catches it, and the platform reparse flag catches the Windows case
    the launcher rejects on its own side.
    """
    status = os.lstat(full)
    if stat.S_ISLNK(status.st_mode):
        fail(
            "profile tree contains a %s: %s"
            % ("linked directory" if directory else "symlink", full)
        )
    if getattr(status, "st_file_attributes", 0) & REPARSE_POINT:
        fail("profile tree contains a reparse point: " + full)
    if not directory and status.st_nlink != 1:
        fail(
            "profile tree contains a hard-linked file with %d names: %s"
            % (status.st_nlink, full)
        )


def inventory(root: str) -> dict[str, str]:
    """Every regular file under root, keyed by normalized relative path.

    Refuses symlinks, hard links, reparse points, non-regular files, and any two paths that
    normalize to one name. The walk never follows links.
    """
    if not os.path.isdir(root):
        fail("profile tree is missing: " + root)
    found: dict[str, str] = {}
    spellings: dict[str, str] = {}
    for current, directories, files in os.walk(root, followlinks=False):
        for name in list(directories):
            refuse_links(os.path.join(current, name), True)
        for name in files:
            full = os.path.join(current, name)
            refuse_links(full, False)
            if not os.path.isfile(full):
                fail("profile tree contains a non-regular file: " + full)
            relative = os.path.relpath(full, root)
            key = normalize(relative)
            if key in spellings:
                fail(
                    "two profile paths normalize to one name: %s and %s"
                    % (spellings[key], relative)
                )
            spellings[key] = relative
            digest = hashlib.sha256()
            with open(full, "rb") as handle:
                for block in iter(lambda: handle.read(65536), b""):
                    digest.update(block)
            found[key] = digest.hexdigest()
    if not found:
        fail("profile tree holds no files: " + root)
    return found


def seal(root: str, seal_path: str) -> None:
    rows = inventory(root)
    lines = [SEAL_HEADER]
    lines.extend("%s  %s" % (rows[key], key) for key in sorted(rows))
    with open(seal_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    print("sealed %d profile files" % len(rows))


def read_seal(seal_path: str) -> dict[str, str]:
    with open(seal_path, encoding="utf-8") as handle:
        lines = handle.read().splitlines()
    if not lines or lines[0] != SEAL_HEADER:
        fail("seal header is missing or unknown: " + seal_path)
    rows: dict[str, str] = {}
    for line in lines[1:]:
        if not line.strip():
            continue
        parts = line.split("  ", 1)
        if len(parts) != 2:
            fail("seal line is malformed: " + line)
        digest, key = parts[0].strip(), parts[1].strip()
        if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
            fail("seal line has a malformed digest: " + line)
        if key in rows:
            fail("seal repeats a path: " + key)
        rows[key] = digest
    if not rows:
        fail("seal is empty: " + seal_path)
    return rows


def verify(root: str, seal_path: str) -> None:
    """Closed comparison in BOTH directions: no missing, no extra, no modified."""
    expected = read_seal(seal_path)
    actual = inventory(root)
    missing = sorted(set(expected) - set(actual))
    extra = sorted(set(actual) - set(expected))
    modified = sorted(
        k for k in set(expected) & set(actual) if expected[k] != actual[k]
    )
    problems = []
    if missing:
        problems.append("missing: " + ", ".join(missing[:5]))
    if extra:
        problems.append("extra: " + ", ".join(extra[:5]))
    if modified:
        problems.append("modified: " + ", ".join(modified[:5]))
    if problems:
        fail("profile does not match its seal (" + "; ".join(problems) + ")")
    print(
        "profile matches its seal exactly (%d files, closed both ways)" % len(expected)
    )


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        fail(
            "usage: scenario_profile.py "
            "<request|start|manifest|options|script|seal|verify|seed> ..."
        )
    action = argv[1]
    if action == "script" and len(argv) >= 3:
        write_script(argv[2], argv[3:])
    elif action == "request" and len(argv) == 3:
        write_request(argv[2])
    elif action == "start" and len(argv) == 4:
        write_start(argv[2], argv[3])
    elif action == "manifest" and len(argv) == 4:
        write_manifest(argv[2], argv[3])
    elif action == "options" and len(argv) == 4:
        write_options(argv[2], argv[3])
    elif action == "seal" and len(argv) == 4:
        seal(argv[2], argv[3])
    elif action == "verify" and len(argv) == 4:
        verify(argv[2], argv[3])
    elif action == "seed" and len(argv) == 3:
        print(validate_seed(argv[2]))
    else:
        fail("unknown action or wrong argument count: " + " ".join(argv[1:]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
