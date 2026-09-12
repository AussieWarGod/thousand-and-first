#!/usr/bin/env python3
"""Read-only verdict over the FIRST COMPLETE CONSTRUCTION LIFECYCLE on the Quickstart path.

The lifecycle this tool judges is one chain, not a set of independent legs:

    startup -> stockpile quote -> CanPay -> paid commission with physical timber/water debit
    -> engine turns until the job completes into a functional building -> real save
    -> cold load -> a further action on the loaded game (a second quote/commission).

Three verdicts, and only one of them is success:

  PASS     every link's rows are present, in order, and OK.
  FAIL     a link's rows are present but refused, out of order, or self-contradictory
           (failClass "chain"), the founder died mid-run (failClass "founder-died"), or the
           steps named different stores (failClass "store-drift").
  BLOCKER  a link has NO rows at all. Missing reachability is never a pass: an absent link
           means the chain was never driven that far, which is exactly the state this tool
           exists to make visible.

It reads journals that a native run produced; it proves nothing on its own and never writes
to a profile, a save or a source file. `Tools/check-quickstart-results.py` remains the
per-leg acceptance oracle (boot/save/load/build); this tool only judges the whole chain.
"""

from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path
import re
import sys

sys.dont_write_bytecode = True
from personas import persona_matrix

PASS = "PASS"
FAIL = "FAIL"
BLOCKER = "BLOCKER"

# Row names each link must land. Keep in exact step with the harness constants in
# Harness/KingdomQuickstartLifecycleRows.cs (DevTests pins the two lists against each other).
BOOT_ROWS = ("QUICKSTART-BOOT-BEGIN", "QUICKSTART-BOOT-OBSERVED", "QUICKSTART-BOOT-COMPLETE")
BUILD_ROWS = (
    "QUICKSTART-BUILD-BEGIN",
    "QUICKSTART-BUILD-QUOTE",
    "QUICKSTART-BUILD-CANPAY",
    "QUICKSTART-BUILD-COMMISSION",
    "QUICKSTART-BUILD-COMPLETE",
)
GROW_ROWS = ("QUICKSTART-GROW-BEGIN", "QUICKSTART-GROW-TURNS", "QUICKSTART-GROW-BUILT")
SAVE_ROWS = ("QUICKSTART-SAVE-BEGIN", "QUICKSTART-SAVE-COMPLETE")
LOAD_ROWS = ("QUICKSTART-LOAD-PREACTIVATION", "QUICKSTART-LOAD-COMPLETE")
NEXT_ROWS = ("QUICKSTART-NEXT-BEGIN", "QUICKSTART-NEXT-QUOTE", "QUICKSTART-NEXT-COMPLETE")

# The seven ordered steps, and every key name the emitted artefact uses, in ONE place: the
# release validator's shape is still settling, so a rename must be a single edit here rather
# than a hunt through the emitter. STEPS is the order the chain runs in; each step appears
# exactly once and no extra step is ever emitted.
STEPS = (
    "startup",
    "quote",
    "paid-commission",
    "engine-turn-build",
    "save",
    "cold-load",
    "next-action",
)
KEYS = {
    "schema": "schemaVersion",
    "driver": "driver",
    "run": "runId",
    "seed": "seed",
    "candidate": "candidateCommit",
    "inventory": "runtimeInventorySha256",
    "build": "gameBuildId",
    "log_ref": "logRef",
    "log_hash": "logSha256",
    "processes": "processes",
    "role": "role",
    "steps": "steps",
    "step": "step",
    "status": "status",
    "turns_used": "turnsUsed",
    "turn_budget": "turnBudget",
    "elapsed": "elapsedSeconds",
    "timeout": "timeoutSeconds",
    "continuity": "continuity",
    # Per-step identities as the driver actually read them at that step. The exact field name
    # is still settling on the validator side; it is one edit here.
    "observed": "observed",
}
SCHEMA_VERSION = 1
SESSIONS = ("save-session", "cold-load-session")
# Which session each step belongs to, so the two launches can be checked against the steps
# they are supposed to cover rather than asserted in prose.
# An identity a step MAY carry beyond what it must. Two steps can share one journal row (the
# quote and the commission are one production call), and a row states everything it knew; the
# derivation therefore keeps only what each step is entitled to say, rather than letting a later
# identity leak backwards into an earlier step.
ALLOWED_EXTRA = {
    "engine-turn-build": ("jobId",),
    "save": ("buildingId", "plotId"),
    "next-action": ("jobId", "buildingId", "plotId"),
}
OBSERVED_KEYS = (
    "realmId", "cityId", "jobId", "buildingId", "plotId", "saveId",
    "completedReceiptId", "forJobId",
)
SESSION_OF = {
    "startup": "save-session",
    "quote": "save-session",
    "paid-commission": "save-session",
    "engine-turn-build": "save-session",
    "save": "save-session",
    "cold-load": "cold-load-session",
    "next-action": "cold-load-session",
}
# The same chain can be driven two ways, and the journal says which. The Quickstart-lifecycle
# profile (Tools/personas/lifecycle-stockpile-native-check.persona) lands the QUICKSTART-*
# rows its boot phases write; the founding-first-city profile
# (Tools/personas/lifecycle-founding-road-refusal.persona) lands one row per scenario verb it
# ran, so the verb names ARE the row names there. A link is judged against whichever
# vocabulary this journal actually used; a journal that mixes them is judged on the fuller one
# and still has to be complete and in order.
LIFECYCLE_ROWS = {
    # The Quickstart lifecycle profile lands the boot rows first, then its build rows, then one
    # row per AutoRunner verb; the founded profile lands verb rows throughout. A link is judged
    # on whichever vocabulary the journal in hand actually used.
    "startup": ("realize", "lifecycle-open"),
    "quote": ("lifecycle-build",),
    "paid-commission": ("lifecycle-build",),
    "engine-turn-build": ("lifecycle-grown",),
    "save": ("lifecycle-save",),
    "cold-load": ("lifecycle-loaded",),
    "next-action": ("lifecycle-next",),
}
# Which identities a step must have observed BY THE TIME IT RAN, following the real
# lifecycle rather than the shape of the schema. A job does not exist before it is
# commissioned; a plot and a building do not exist before the turns that raise them; a save
# id does not exist before the save. Demanding an id earlier than that would invite either a
# fabricated placeholder or a value copied forward from a later step, and both are worse than
# an absent field.
#
# After the cold load the completed job may be gone from the live registry, so jobId is not
# demanded there; and the next action is free to quote or commission a NEW job with its own
# identity, so nothing here asks it to repeat the finished one. What carries across is the
# realm and city, the plot and building that still stand, and the save id -- which is what
# the release validator compares.
EXPECTED_IDENTITIES = {
    "startup": ("realmId", "cityId"),
    "quote": ("realmId", "cityId"),
    "paid-commission": ("realmId", "cityId", "jobId"),
    # The completed registry row's own id, and the paid job it fulfils, so the finished work
    # links back to what was paid for.
    "engine-turn-build": ("realmId", "cityId", "buildingId", "plotId",
                          "completedReceiptId", "forJobId"),
    "save": ("realmId", "cityId", "saveId"),
    "cold-load": ("realmId", "cityId", "buildingId", "plotId", "saveId"),
    # The next action's own job is deliberately unlinked from the completed one, so nothing here
    # asks it to repeat an identity; what must still hold is the world it acted on.
    "next-action": ("realmId", "cityId", "saveId"),
}

# An identity a step CANNOT honestly have observed, because the thing it names does not exist
# yet. Emitting one here would be a placeholder or a value borrowed from the future, so the
# emitter drops it and says so rather than passing it on.
FORBIDDEN_IDENTITIES = {
    "startup": ("jobId", "buildingId", "plotId", "saveId"),
    "quote": ("jobId", "buildingId", "plotId", "saveId"),
    "paid-commission": ("buildingId", "plotId", "saveId"),
    "engine-turn-build": ("saveId",),
    "save": (),
    "cold-load": (),
    "next-action": (),
}
LINKS = (
    ("startup", BOOT_ROWS),
    ("quote", BUILD_ROWS[:2]),
    ("paid-commission", BUILD_ROWS[2:]),
    ("engine-turn-build", GROW_ROWS),
    ("save", SAVE_ROWS),
    ("cold-load", LOAD_ROWS),
    ("next-action", NEXT_ROWS),
)


def rows_of(paths: list[Path]) -> list[tuple[str, str, str]]:
    """Every journal row across the given journals, in the order the runs produced them."""
    rows: list[tuple[str, str, str]] = []
    for path in paths:
        text = path.read_bytes().decode("utf-8").replace("\r\n", "\n")
        rows.extend(persona_matrix.read_journal(text))
    return rows


def stamped_rows(paths: list[Path]) -> list[tuple[str, str, str]]:
    """Every row as (utc stamp, verb, message).

    The stamp is the harness's own clock, written when the row was journalled, which is why
    elapsed time is derived from it rather than from a second clock: two clocks can disagree,
    and the journal's is the one that witnessed the step.
    """
    stamped: list[tuple[str, str, str]] = []
    for path in paths:
        text = path.read_bytes().decode("utf-8").replace("\r\n", "\n")
        for line in text.splitlines():
            if not line.strip():
                continue
            fields = line.split("\t")
            if len(fields) != 4:
                raise ValueError("journal row does not have four columns")
            stamped.append(
                (fields[0], persona_matrix.unescape(fields[1]), persona_matrix.unescape(fields[3]))
            )
    return stamped


def seconds_between(first: str, second: str) -> int:
    """Whole seconds between two journal stamps, never negative."""
    shape = "%Y-%m-%dT%H:%M:%S.%fZ"
    start = datetime.strptime(first, shape)
    end = datetime.strptime(second, shape)
    return max(0, int((end - start).total_seconds()))


# The four ways a paid job can be unfinished when the turns run out, as the harness names them
# (Harness/KingdomQuickstartLifecycleStall.cs). Surfaced verbatim in the verdict so the reason is
# read rather than guessed; every one of them is a FAIL for its link, never a pass or a waiver.
STALL_CLASSES = (
    "pass-never-ran",
    "no-labour-ever",
    "labour-stalled",
    "insufficient-turns",
)


# The founder's death is its own FAIL class, distinct from every job-stall class above and from a
# link that merely refused: native run 36 (13122f0) lost the founder to a wandering creature
# mid-`advance` and, until the AutoRunner learned to journal it, the run simply hung. The runner
# now lands a SCRIPT-STOPPED row whose message opens with DIED_PREFIX and the engine's own death
# category (Harness/KingdomScenarioAutoRunner.Death.cs); this tool names it founder-died, never a
# pass, never a waiver, and it outranks every other verdict because it is the root cause of
# whatever the chain failed to reach afterwards.
STOPPED_ROW = "SCRIPT-STOPPED"
DIED_PREFIX = "DIED "
FOUNDER_DIED = "founder-died"
CHAIN_FAIL = "chain"
# Native run 38 (2fa563c): the heart's own dry store joined the bootstrap chest mid-advance and a
# scan-based "exactly one" refused the save. Every lifecycle step now names the store it paid from
# (storeId=, Harness/KingdomQuickstartLifecycleSteps.cs StoreClause); the chain must name ONE store
# from open to next-action, and a drift between steps is its own FAIL class.
STORE_DRIFT = "store-drift"


def store_in(message: str) -> str | None:
    """The store id a lifecycle row named, if it named one."""
    found = re.search(r"\bstoreId=([^;\s]+)", message)
    return found.group(1) if found else None


def store_ids(rows: list[tuple[str, str, str]]) -> list[str]:
    """Every distinct store id the OK lifecycle rows named, in first-seen order."""
    seen: list[str] = []
    for verb, outcome, message in rows:
        name = store_in(message)
        if outcome == "OK" and name is not None and name not in seen:
            seen.append(name)
    return seen


def founder_death(rows: list[tuple[str, str, str]]) -> str | None:
    """The message of the first SCRIPT-STOPPED row that reports the founder's death, if any."""
    for verb, _, message in rows:
        if verb == STOPPED_ROW and message.startswith(DIED_PREFIX):
            return message
    return None


def stall_in(message: str) -> str | None:
    """The stall classification a refusal row named, if it named one."""
    found = re.search(r"\bstall=([a-z-]+)", message)
    return found.group(1) if found else None


def stalls(paths: list[Path]) -> list[str]:
    """Every stall a journal reported, quoted as the row stated it."""
    reported: list[str] = []
    for stamp, verb, message in stamped_rows(paths):
        name = stall_in(message)
        if name is None:
            continue
        reported.append(
            verb + ": stall=" + name
            + ("" if name in STALL_CLASSES else " (unknown classification)")
            + "; " + message.split("stall=", 1)[1]
        )
    return reported


def stamp_in(message: str) -> tuple[str | None, str | None]:
    """The profile name and seal a lifecycle row stamped on itself, if it stamped one."""
    name = re.search(r"\bprofile=([^;\s]+)", message)
    seal = re.search(r"\bseal=([0-9a-f]{64})\b", message)
    return (name.group(1) if name else None, seal.group(1) if seal else None)


def turns_in(message: str) -> int | None:
    """The turn counter a lifecycle row states, if it states one."""
    found = re.search(r"\bturns=(\d+)\b", message)
    return int(found.group(1)) if found else None


def observed_in(message: str) -> dict:
    """The identities a lifecycle row states, exactly as it stated them."""
    seen = {}
    for name in OBSERVED_KEYS:
        found = re.search(r"\b" + name + r"=([^;\s]+)", message)
        if found and found.group(1) not in ("unassigned", ""):
            seen[name] = found.group(1)
    return seen


def check_stamps(paths: list[Path], records: list[dict]) -> list[str]:
    """Every lifecycle row must name the profile that ran it, and name it correctly.

    The row stamps profile= and seal= from the launched profile's own sealed root; the run
    record sealed the same two values at preparation time. A row that names a different profile
    did not come from this session, and a row that names none cannot be bound to one -- both are
    refusals rather than something to be taken on trust.
    """
    problems: list[str] = []
    by_role = {record.get("role"): record for record in records}
    # Only rows the lifecycle verbs themselves write can carry the stamp; the built-in verbs
    # this chain leans on (realize, and the Quickstart boot phases) are not ours to restamp,
    # and demanding it of them would be demanding evidence nobody produces.
    named = {
        name for names in LIFECYCLE_ROWS.values() for name in names
        if name.startswith("lifecycle-")
    }
    for verb_stamp, verb, message in stamped_rows(paths):
        if verb not in named:
            continue
        step = next((name for name, rows in LIFECYCLE_ROWS.items() if verb in rows), None)
        record = by_role.get(SESSION_OF.get(step, ""), {})
        name, seal = stamp_in(message)
        if name is None or seal is None:
            problems.append("row " + verb + " carries no profile stamp")
            continue
        if record.get("profileName") != name:
            problems.append(
                "row " + verb + " names profile " + name + ", not its session's "
                + str(record.get("profileName"))
            )
        if record.get("profileSeal") != seal:
            problems.append("row " + verb + " names a seal its session's record does not")
    return problems


def bind_journals(paths: list[Path], records: list[dict]) -> tuple[dict, list[str]]:
    """Which session wrote each journal, proved rather than assumed.

    A journal is bound to the run record that sits in its own scenario root, and that record
    must be one of the two handed in. Without this, two journals could be read as one session's
    work, or a step could be attributed to a profile that never ran it -- so a journal with no
    record beside it, or one whose record is not in the pair, is refused rather than guessed at.
    """
    problems: list[str] = []
    bound: dict = {}
    known = {}
    for entry in records:
        root = entry.get("root")
        if isinstance(root, str) and root:
            known[str(Path(root).resolve())] = entry
    for path in paths:
        root = str(path.resolve().parent)
        entry = known.get(root)
        if entry is None:
            problems.append("journal " + path.name + " has no run record for its own profile")
            continue
        bound[str(path.resolve())] = entry
    return bound, problems


def measure(paths: list[Path], records: list[dict]) -> dict:
    """Per-step turns and seconds, derived from the journals and bounded by the records.

    Turns are the difference between the turn counters two consecutive lifecycle rows state;
    seconds are the difference between their stamps. Budgets come from the records and are
    never used as a measurement -- an over-budget run must be able to say so.
    """
    stamped = stamped_rows(paths)
    by_role = {record.get("role"): record for record in records}
    profiles = {
        record.get("role"): (record.get("profileName"), record.get("profileSeal"))
        for record in records
    }
    phases: dict = {}
    last_turns = 0
    last_stamp = None
    for step, _ in LINKS:
        names = LIFECYCLE_ROWS.get(step, ())
        rows = [row for row in stamped if row[1] in names]
        if not rows:
            continue
        stamp, _, message = rows[-1]
        record = by_role.get(SESSION_OF[step], {})
        # The step's measurements belong to the profile that ran it, named here from that
        # session's own record rather than from whichever record happened to be first.
        name, seal = profiles.get(SESSION_OF[step], (None, None))
        phase = {
            "profileName": name,
            "profileSeal": seal,
            "turnBudget": record.get("turnBudget"),
            "timeoutSeconds": record.get("timeoutSeconds"),
            KEYS["observed"]: {
                name: value for name, value in observed_in(message).items()
                if name in EXPECTED_IDENTITIES[step] + ALLOWED_EXTRA.get(step, ())
            },
        }
        turns = turns_in(message)
        if turns is not None:
            phase["turnsUsed"] = max(0, turns - last_turns)
            last_turns = turns
        first_stamp = last_stamp if last_stamp else stamp
        phase["elapsedSeconds"] = seconds_between(first_stamp, stamp)
        last_stamp = stamp
        phases[step] = {key: value for key, value in phase.items() if value is not None}
    return phases


def judge(rows: list[tuple[str, str, str]]) -> dict:
    """The whole-chain verdict. Order of evaluation is the order of the chain itself."""
    seen = [(verb, outcome) for verb, outcome, _ in rows]
    names = [verb for verb, _ in seen]
    links: list[dict] = []
    verdict = PASS
    reason = None
    position = -1
    for link, primary in LINKS:
        wanted = primary
        alternative = LIFECYCLE_ROWS.get(link)
        if alternative is not None:
            chosen = max(
                (primary, alternative),
                key=lambda option: len([name for name in option if name in names]),
            )
            wanted = chosen
        present = [name for name in wanted if name in names]
        if not present:
            state = BLOCKER
            detail = "no rows for this link; it was never driven"
        else:
            missing = [name for name in wanted if name not in names]
            refused = [name for name, outcome in seen if name in wanted and outcome != "OK"]
            order = [names.index(name) for name in wanted if name in names]
            if missing:
                state, detail = FAIL, "partial evidence; missing " + ", ".join(missing)
            elif refused:
                state, detail = FAIL, "refused row(s): " + ", ".join(refused)
            elif order != sorted(order):
                state, detail = FAIL, "rows are out of order"
            elif min(order) < position:
                state, detail = FAIL, "rows interleave an earlier link"
            else:
                state, detail = PASS, "complete and in order"
                position = max(order)
        links.append({"link": link, "state": state, "detail": detail, "rows": list(wanted)})
        if state == FAIL and verdict != FAIL:
            verdict, reason = FAIL, link + ": " + detail
        elif state == BLOCKER and verdict == PASS:
            verdict, reason = BLOCKER, link + ": " + detail
    fail_class = CHAIN_FAIL if verdict == FAIL else None
    stores = store_ids(rows)
    if len(stores) > 1:
        verdict, fail_class = FAIL, STORE_DRIFT
        reason = STORE_DRIFT + ": the steps paid from different stores " + ",".join(stores)
    death = founder_death(rows)
    if death is not None:
        verdict, reason, fail_class = FAIL, FOUNDER_DIED + ": " + death, FOUNDER_DIED
    return {
        "tool": "check-quickstart-lifecycle",
        "verdict": verdict,
        "reason": reason,
        "failClass": fail_class,
        "founderDeath": death,
        "storeIds": stores,
        "links": links,
        "rowsRead": len(rows),
    }


EXITS = {PASS: 0, BLOCKER: 3, FAIL: 4}


def results(report: dict, run: dict) -> tuple[dict, list[str]]:
    """The long-form release artefact, and the list of what could not be produced honestly.

    The artefact carries EXACTLY the validator's key set and nothing else: schemaVersion,
    driver, runId, seed, candidateCommit, runtimeInventorySha256, gameBuildId, logRef,
    logSha256, continuity, processes (two ordered sessions), steps (the seven in chain
    order). Blockers are NOT smuggled into it -- they are returned separately and printed
    with the verdict, because an artefact that carries its own excuses is still an artefact
    claiming to be complete.

    HONESTY RULES. A step is emitted only when this journal proves that link complete, and
    only as PASS; an undriven or refused link is left out, and the validator then refuses the
    whole artefact for not being the exact ordered chain. Measurements come from the driver's
    run record and are emitted as measured -- `turnsUsed` is never clamped to its budget,
    since an overrun is exactly what the budget exists to reveal.
    """
    states = {entry["link"]: entry["state"] for entry in report["links"]}
    measured = run.get("phases") or {}
    unresolved: list[str] = []
    steps = []
    for step in STEPS:
        if states.get(step) != PASS:
            unresolved.append("steps." + step)
            continue
        phase = measured.get(step) or {}
        entry = {KEYS["step"]: step, KEYS["status"]: PASS}
        for key in ("turns_used", "elapsed", "turn_budget", "timeout"):
            field = KEYS[key]
            if field in phase:
                entry[field] = phase[field]
            else:
                unresolved.append(step + "." + field)
        observed = phase.get(KEYS["observed"])
        if isinstance(observed, dict):
            early = [name for name in FORBIDDEN_IDENTITIES[step] if name in observed]
            if early:
                observed = {
                    name: value for name, value in observed.items()
                    if name not in FORBIDDEN_IDENTITIES[step]
                }
                unresolved.extend(
                    step + "." + KEYS["observed"] + "." + name + " (before it exists)"
                    for name in early
                )
        # Passed through exactly as the driver read it at THAT step. Nothing is copied
        # forward from an earlier step and nothing is filled in from the top-level
        # continuity block: an identity that only appears because a previous step saw it
        # would prove continuity by construction rather than by observation.
        if isinstance(observed, dict) and observed:
            entry[KEYS["observed"]] = observed
        else:
            unresolved.append(step + "." + KEYS["observed"])
        # A required identity that is absent is NOT quietly dropped from the chain: the step is
        # left out of the artefact entirely and named here, so the validator reads the chain as
        # incomplete. An id is never minted to fill the hole -- the honest producer of a missing
        # identity is a refusal row from the verb that could not read it.
        missing = [
            identity for identity in EXPECTED_IDENTITIES[step]
            if not isinstance(observed, dict) or identity not in observed
        ]
        if missing:
            unresolved.extend(step + "." + KEYS["observed"] + "." + name for name in missing)
            continue
        steps.append(entry)
    payload: dict = {KEYS["schema"]: SCHEMA_VERSION}
    for key in ("driver", "run", "seed", "candidate", "inventory", "build", "log_ref",
                "log_hash", "continuity"):
        field = KEYS[key]
        if field in run:
            payload[field] = run[field]
        else:
            unresolved.append(field)
    sessions = run.get(KEYS["processes"]) or []
    ordered = [
        session for role in SESSIONS
        for session in sessions
        if isinstance(session, dict) and session.get("role") == role
    ]
    launches = [
        session.get("launchId") for session in ordered
        if isinstance(session.get("launchId"), str) and session["launchId"]
    ]
    if len(ordered) == len(SESSIONS) and len(set(launches)) == len(SESSIONS):
        payload[KEYS["processes"]] = ordered
    elif len(ordered) == len(SESSIONS):
        # Two sessions that share a launch id are one session described twice. Emitting them
        # would claim a cold load that never had its own process.
        unresolved.append(KEYS["processes"] + " (sessions must have distinct launch ids)")
    else:
        present = {session.get("role") for session in ordered}
        unresolved.extend(
            KEYS["processes"] + "." + role for role in SESSIONS if role not in present
        )
    payload[KEYS["steps"]] = steps
    return payload, unresolved


USAGE = (
    "usage: check-quickstart-lifecycle.py JOURNAL [JOURNAL ...]\n"
    "       [--results FILE --run-record FILE]\n"
    "\n"
    "The run record is the driver's own JSON measurement of the run it just owned: driver,\n"
    "runId, seed, candidateCommit, runtimeInventoryDigest, gameBuildId, logRef, logSha256,\n"
    "continuity, processes{save-session,cold-load-session}, and phases{<step>:{turnsUsed,\n"
    "turnBudget,elapsedSeconds,timeoutSeconds}}. Anything it omits is emitted nowhere and\n"
    "listed under unresolvedFields; this tool measures nothing itself."
)


def parse(argv: list[str]) -> tuple[list[str], dict]:
    """Journals first, then options. An unknown or repeated option is a usage error rather
    than a silently ignored request."""
    journals: list[str] = []
    options: dict = {}
    index = 1
    while index < len(argv) and not argv[index].startswith("--"):
        journals.append(argv[index])
        index += 1
    while index < len(argv):
        name = argv[index]
        if not name.startswith("--") or index + 1 >= len(argv):
            raise ValueError("malformed option " + name)
        key = name[2:]
        if key in options:
            raise ValueError("repeated option " + name)
        if key not in ("results", "run-record", "driver", "run-id"):
            raise ValueError("unknown option " + name)
        options[key] = argv[index + 1]
        index += 2
    return journals, options


# What a session may say about how it ended. An exit code is only ever evidence when somebody
# held the owned process and watched it exit; "ended, exit unobserved" is the honest reading of a
# detached launch that was stopped from outside, and no exit code may accompany it.
EXIT_OBSERVED = "owned-process-exit-observed"
EXIT_UNOBSERVED = "owned-process-ended-exit-unobserved"


def exit_provenance(record: dict) -> list[str]:
    """Whether this session's ending is bound to the process the launcher actually owned.

    The launcher writes an ownership block from its own process-ownership.json receipt: the pid
    it started, that process's start ticks, the executable, and the receipt's SHA-256. Without
    it, an exitCode is a number from whichever shell called stop. With it, the pid in the block
    must be the pid the launch identity names, and an exitCode may appear only under the observed
    provenance -- never beside a default.
    """
    problems: list[str] = []
    role = str(record.get("role"))
    owned = record.get("ownership")
    if not isinstance(owned, dict):
        return [role + ".ownership (no owned-process receipt; an exit cannot be bound)"]
    for field in ("receiptRef", "receiptSha256", "pid", "startTicks"):
        if not owned.get(field):
            problems.append(role + ".ownership." + field)
    digest = owned.get("receiptSha256")
    if isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest) is None:
        problems.append(role + ".ownership.receiptSha256 (not a lowercase SHA-256)")
    pid = owned.get("pid")
    launch = record.get("launchId")
    if isinstance(pid, int) and isinstance(launch, str) and ("-" + str(pid) + "-") not in launch:
        problems.append(
            role + ".launchId (names a process the ownership receipt does not: pid " + str(pid) + ")"
        )
    provenance = record.get("exitProvenance")
    if provenance not in (EXIT_OBSERVED, EXIT_UNOBSERVED):
        problems.append(role + ".exitProvenance (" + str(provenance) + ")")
    if "exitCode" in record and provenance != EXIT_OBSERVED:
        problems.append(role + ".exitCode (recorded without an observed exit)")
    return problems


def sessions(records: list[dict]) -> tuple[list[dict], list[str]]:
    """The two process sessions, and what is wrong with them if anything is.

    Two records that share a launch identity are one session described twice, and a cold load
    that began before the save session stopped never loaded that save at all. Either way the
    pair is refused rather than emitted.
    """
    problems: list[str] = []
    by_role = {record.get("role"): record for record in records}
    ordered = [by_role[role] for role in SESSIONS if role in by_role]
    if len(ordered) != len(SESSIONS):
        return [], [
            KEYS["processes"] + "." + role for role in SESSIONS if role not in by_role
        ]
    for record in ordered:
        problems.extend(exit_provenance(record))
    launches = [record.get("launchId") for record in ordered]
    if any(not isinstance(value, str) or not value for value in launches):
        problems.append(KEYS["processes"] + " (every session needs its own launch id)")
    elif len(set(launches)) != len(launches):
        problems.append(KEYS["processes"] + " (sessions must have distinct launch ids)")
    stopped = ordered[0].get("stoppedUtc")
    started = ordered[1].get("started")
    if not isinstance(stopped, str) or not isinstance(started, str):
        problems.append(KEYS["processes"] + " (both sessions need started and stoppedUtc)")
    elif stopped > started:
        problems.append(
            KEYS["processes"] + " (the cold-load session began before the save session stopped)"
        )
    if problems:
        return [], problems
    entries = []
    for record in ordered:
        entry = {
            "role": record["role"],
            "launchId": record["launchId"],
            "started": record["started"],
            "stoppedUtc": record["stoppedUtc"],
        }
        # The two profiles legitimately differ -- load authority, script, import metadata -- so
        # each session names its OWN seal here. Nothing hoists a single seal to the top level,
        # where it would falsely claim the two sessions, or the public package, were one thing.
        # The artefact's process entry keeps the key set the validator fixed; the ownership and
        # exit provenance are VALIDATED above and reported with the verdict, not emitted here.
        for field in ("profileSeal", "profileName"):
            if record.get(field):
                entry[field] = record[field]
            else:
                problems.append(KEYS["processes"] + "." + record["role"] + "." + field)
        entries.append(entry)
    return entries, problems


def run_from(records: list[dict], phases: dict, driver: str, run_id: str) -> tuple[dict, list[str]]:
    """One run description assembled from the two session records and the derived phases."""
    problems: list[str] = []
    save = next((record for record in records if record.get("role") == SESSIONS[0]), {})
    run: dict = {"phases": phases, "driver": driver, "runId": run_id}
    for field, source in (("seed", "seed"), ("candidateCommit", "candidateCommit"),
                          ("runtimeInventorySha256", "runtimeInventorySha256"),
                          ("harnessInventorySha256", "harnessInventorySha256"),
                          ("gameBuildId", "gameBuildId"), ("logRef", "logRef"),
                          ("logSha256", "logSha256"), ("continuity", "continuity")):
        value = save.get(source)
        if value is not None:
            run[field] = value
    if isinstance(run.get("seed"), str):
        digits = run["seed"].lstrip("#")
        if digits.isdigit():
            run["seed"] = int(digits)
    for other in records:
        for field in ("candidateCommit", "runtimeInventorySha256"):
            if field in save and field in other and save[field] != other[field]:
                problems.append(field + " (the two sessions exercised different trees)")
    ordered, session_problems = sessions(records)
    problems.extend(session_problems)
    if ordered:
        run[KEYS["processes"]] = ordered
    return run, problems


def emit(report: dict, options: dict, journals: list[Path]) -> list[str]:
    """Writes the long-form artefact beside the verdict, from the two session run records."""
    if set(options) < {"results", "run-record"}:
        raise ValueError("results emission needs both --results and --run-record")
    paths = [Path(name) for name in options["run-record"].split(",") if name]
    if len(paths) != len(SESSIONS):
        raise ValueError("--run-record takes the two run-record.json paths, comma separated")
    records = []
    for path in paths:
        payload = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("run record must be a JSON object")
        records.append(payload)
    bound, binding_problems = bind_journals(journals, records)
    binding_problems.extend(check_stamps(journals, records))
    # A stall is never a waiver: it is reported verbatim beside a verdict that already refuses
    # the link whose row carried it.
    report["stalls"] = stalls(journals)
    phases = measure(journals, records)
    # Visible in the verdict, not in the artefact: the validator's step key set is fixed, so the
    # profile a step's evidence came from is reported beside the verdict instead of inside it.
    report["profilesByStep"] = {
        step: {
            "profileName": phase.get("profileName"),
            "profileSeal": phase.get("profileSeal"),
            "journals": sorted({Path(name).name for name in bound}),
        }
        for step, phase in phases.items()
    }
    run, problems = run_from(
        records, phases,
        options.get("driver", "automated lifecycle driver"),
        options.get("run-id", records[0].get("runId", "")),
    )
    payload, unresolved = results(report, run)
    Path(options["results"]).write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    return binding_problems + problems + unresolved


def main(argv: list[str]) -> int:
    try:
        journals, options = parse(argv)
        if not journals:
            print(USAGE, file=sys.stderr)
            return 2
        paths = [Path(name).resolve(strict=True) for name in journals]
        report = judge(rows_of(paths))
        if options:
            report["unresolvedFields"] = emit(report, options, paths)
    except (OSError, ValueError) as error:
        print(json.dumps({"verdict": "REFUSED", "reason": str(error)}, sort_keys=True))
        return 2
    print(json.dumps(report, indent=2, sort_keys=True))
    return EXITS[report["verdict"]]


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
