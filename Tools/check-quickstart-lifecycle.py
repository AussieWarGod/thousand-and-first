#!/usr/bin/env python3
"""Read-only verdict over the FIRST COMPLETE CONSTRUCTION LIFECYCLE on the Quickstart path.

The lifecycle this tool judges is one chain, not a set of independent legs:

    startup -> stockpile quote -> CanPay -> paid commission with physical timber/water debit
    -> engine turns until the job completes into a functional building -> real save
    -> cold load -> a further action on the loaded game (a second quote/commission).

Three verdicts, and only one of them is success:

  PASS     every link's rows are present, in order, and OK.
  FAIL     a link's rows are present but refused, out of order, or self-contradictory.
  BLOCKER  a link has NO rows at all. Missing reachability is never a pass: an absent link
           means the chain was never driven that far, which is exactly the state this tool
           exists to make visible.

It reads journals that a native run produced; it proves nothing on its own and never writes
to a profile, a save or a source file. `Tools/check-quickstart-results.py` remains the
per-leg acceptance oracle (boot/save/load/build); this tool only judges the whole chain.
"""

from __future__ import annotations

import json
from pathlib import Path
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
    "observed": "observedIdentities",
}
SCHEMA_VERSION = 1
SESSIONS = ("save-session", "cold-load-session")
# Which session each step belongs to, so the two launches can be checked against the steps
# they are supposed to cover rather than asserted in prose.
SESSION_OF = {
    "startup": "save-session",
    "quote": "save-session",
    "paid-commission": "save-session",
    "engine-turn-build": "save-session",
    "save": "save-session",
    "cold-load": "cold-load-session",
    "next-action": "cold-load-session",
}
# The same chain can be driven two ways, and the journal says which. A Quickstart profile
# lands the QUICKSTART-* rows its boot phases write; the founded lifecycle profile lands one
# row per scenario verb it ran (Tools/personas/lifecycle-stockpile-native-check.persona), so
# the verb names ARE the row names there. A link is judged against whichever vocabulary this
# journal actually used; a journal that mixes them is judged on the fuller one and still has
# to be complete and in order.
LIFECYCLE_ROWS = {
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
    "paid-commission": ("realmId", "cityId", "jobId", "plotId"),
    "engine-turn-build": ("realmId", "cityId", "jobId", "plotId", "buildingId"),
    "save": ("realmId", "cityId", "jobId", "plotId", "buildingId", "saveId"),
    "cold-load": ("realmId", "cityId", "plotId", "buildingId", "saveId"),
    "next-action": ("realmId", "cityId", "saveId"),
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
    return {
        "tool": "check-quickstart-lifecycle",
        "verdict": verdict,
        "reason": reason,
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
        # Passed through exactly as the driver read it at THAT step. Nothing is copied
        # forward from an earlier step and nothing is filled in from the top-level
        # continuity block: an identity that only appears because a previous step saw it
        # would prove continuity by construction rather than by observation.
        if isinstance(observed, dict) and observed:
            entry[KEYS["observed"]] = observed
        else:
            unresolved.append(step + "." + KEYS["observed"])
        for identity in EXPECTED_IDENTITIES[step]:
            if not isinstance(observed, dict) or identity not in observed:
                unresolved.append(step + "." + KEYS["observed"] + "." + identity)
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
    if len(ordered) == len(SESSIONS):
        payload[KEYS["processes"]] = ordered
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
        if key not in ("results", "run-record"):
            raise ValueError("unknown option " + name)
        options[key] = argv[index + 1]
        index += 2
    return journals, options


def emit(report: dict, options: dict) -> None:
    """Writes the long-form artefact beside the verdict, from the driver's run record."""
    if set(options) != {"results", "run-record"}:
        raise ValueError("results emission needs both --results and --run-record")
    run = json.loads(Path(options["run-record"]).read_text(encoding="utf-8"))
    if not isinstance(run, dict):
        raise ValueError("run record must be a JSON object")
    payload, unresolved = results(report, run)
    Path(options["results"]).write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    report["unresolvedFields"] = unresolved


def main(argv: list[str]) -> int:
    try:
        journals, options = parse(argv)
        if not journals:
            print(USAGE, file=sys.stderr)
            return 2
        paths = [Path(name).resolve(strict=True) for name in journals]
        report = judge(rows_of(paths))
        if options:
            emit(report, options)
    except (OSError, ValueError) as error:
        print(json.dumps({"verdict": "REFUSED", "reason": str(error)}, sort_keys=True))
        return 2
    print(json.dumps(report, indent=2, sort_keys=True))
    return EXITS[report["verdict"]]


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
