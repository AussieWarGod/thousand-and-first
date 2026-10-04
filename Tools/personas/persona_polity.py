"""Host grammar for the #244/#257 polity dispatch window witness (CHECK=polity-window).

Each `polity-window-check` (Harness/KingdomPolityWindowNativeProvider.cs) journals, inside the
verb, one `polity-dispatch` observation read from the realm's dispatch receipt
(Harness/KingdomPolityWindowReading.cs), then its own row repeating that reading:

    polity-dispatch      OK  window=<N> revision=<R> count=<C> mask=<M> intents=<I>
    polity-window-check  OK  polity-window-check recorded polity-dispatch <the same reading>

The persona's positional EXPECT pins every row in order, so a missing, repeated or refused row is
already a failure there. This module judges what the readings say together:

- every observation is OK, matches the grammar exactly and is confirmed by the very next row;
- consecutive readings (one ordinary daily pass apart) stay in one window or advance by one;
- inside one window every field is identical: a reconciliation whose endpoint facts drifted
  writes nothing, re-keys nothing, mints nothing and withdraws nothing (constant revision);
- each window advance commits a newer revision;
- some window is read at least three times in a row - its state, then two later-in-window daily
  passes - and a later reading lies in a newer window: the 8400-tick boundary was crossed after.

Refusal-free reconciliation and the drift line itself are Player.log facts; the persona carries
them as LOG_FORBID and LOG_REQUIRE, never as journal inference.
"""

from __future__ import annotations

import re

OBSERVATION = "polity-dispatch"
VERB = "polity-window-check"
READING = re.compile(
    r"window=(0|[1-9][0-9]{0,19}) revision=(0|[1-9][0-9]{0,18}) "
    r"count=([1-3]) mask=([0-7]) intents=([0-3])"
)
# A daily cadence reads the same window at most seven times; three proves two later passes.
WITNESSED_RUN = 3


def readings(rows: list[tuple[str, str, str]]) -> tuple[list[tuple[int, ...]], list[str]]:
    """Every confirmed observation as (window, revision, count, mask, intents), plus faults."""
    found: list[tuple[int, ...]] = []
    problems: list[str] = []
    ordinal = 0
    for index, (verb, outcome, message) in enumerate(rows):
        if verb != OBSERVATION:
            continue
        ordinal += 1
        match = READING.fullmatch(message)
        if outcome != "OK" or match is None:
            problems.append("polity-dispatch reading %d is refused or malformed" % ordinal)
            continue
        window, revision, count, mask, intents = (int(group) for group in match.groups())
        if mask >= 1 << count or intents > count:
            problems.append("polity-dispatch reading %d is outside its frozen slots" % ordinal)
            continue
        confirm = rows[index + 1] if index + 1 < len(rows) else None
        if confirm != (VERB, "OK", VERB + " recorded " + OBSERVATION + " " + message):
            problems.append(
                "polity-dispatch reading %d is not confirmed by its %s row" % (ordinal, VERB)
            )
            continue
        found.append((window, revision, count, mask, intents))
    return found, problems


def assess(rows: list[tuple[str, str, str]]) -> list[str]:
    found, problems = readings(rows)
    if problems:
        return problems
    if len(found) < WITNESSED_RUN + 1:
        return ["polity-window needs at least %d readings, found %d"
                % (WITNESSED_RUN + 1, len(found))]
    for before, after in zip(found, found[1:]):
        if after[0] == before[0]:
            if after != before:
                problems.append(
                    "window %d receipt changed inside the window: %s then %s"
                    % (before[0], describe(before), describe(after))
                )
        elif after[0] == before[0] + 1:
            if after[1] <= before[1]:
                problems.append(
                    "window %d opened without a newer revision (%d after %d)"
                    % (after[0], after[1], before[1])
                )
        else:
            problems.append(
                "window moved from %d to %d between consecutive daily readings"
                % (before[0], after[0])
            )
    if problems:
        return problems
    run = 1
    for index in range(1, len(found)):
        run = run + 1 if found[index][0] == found[index - 1][0] else 1
        if run >= WITNESSED_RUN and found[-1][0] > found[index][0]:
            return []
    return ["no window was read %d times in a row before a later window opened" % WITNESSED_RUN]


def describe(reading: tuple[int, ...]) -> str:
    return "window=%d revision=%d count=%d mask=%d intents=%d" % reading
