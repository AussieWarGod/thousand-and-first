"""Strict travel witness framing; this does not authenticate fabricated native journals."""
from __future__ import annotations

import re

KEYS = {"mode", "seed", "home", "observed-tick", "wait-turns", "travel-turns", "containers", "envelope",
        "drain-turns", "peak-thirds", "peak-heavy", "measured-demand", "demand-observed", "processed",
        "semantic", "growth-mirror", "schedule-observations", "remaining-demand", "pause-effects-proved",
        "full-envelope-stress", "ordinary-acceptance"}
ECONOMIC_KEYS = {"pause-disabled", "pause-resumed", "paused-ticks", "resume-arrival", "resume-applications",
                 "pause-local-start", "pause-prior", "arrival-interval", "stress-initial-thirds",
                 "stress-residents", "synthetic-fixture"}


def witness(rows):
    checks = [row for row in rows if row[0] == "beta-check"]
    if len(checks) != 1 or checks[0][1] != "OK" or not checks[0][2].startswith("taf-travel-checked "):
        raise ValueError("one successful travel witness is required")
    result = {}
    for item in checks[0][2][len("taf-travel-checked "):].split("; "):
        key, sep, value = item.partition("=")
        if not sep or key in result or not value:
            raise ValueError("travel witness field is empty, malformed or repeated")
        result[key] = value
    economic = result.get("pause-effects-proved") == "true"
    if set(result) != (KEYS | ECONOMIC_KEYS if economic else KEYS) or result["mode"] not in ("away", "present"):
        raise ValueError("travel witness schema differs")
    if not re.fullmatch(r"#-?[0-9]+", result["seed"]):
        raise ValueError("travel seed is not explicit")
    if not re.fullmatch(r"[^.\s]+\.[0-9]+\.[0-9]+\.[0-2]\.[0-2]\.10", result["home"]):
        raise ValueError("travel home is not an ordinary surface zone")
    if (result["ordinary-acceptance"] != "false"
            or result["full-envelope-stress"] != ("true" if economic else "false")
            or result["pause-effects-proved"] != ("true" if economic else "false")
            or economic and result["synthetic-fixture"] != "true"):
        raise ValueError("travel witness overclaims acceptance")
    for key in set(result) - {"mode", "seed", "home", "demand-observed", "ordinary-acceptance", "full-envelope-stress", "pause-effects-proved", "synthetic-fixture"}:
        if not re.fullmatch(r"0|[1-9][0-9]{0,18}", result[key]):
            raise ValueError("travel number is not bounded canonical decimal: " + key)
        result[key] = int(result[key])
        if result[key] > 9223372036854775807:
            raise ValueError("travel number exceeds engine Int64")
    if (result["wait-turns"] != 1200 or result["envelope"] != 252 or result["containers"] > 252
            or result["drain-turns"] > 39 or result["peak-thirds"] > 24 or result["peak-heavy"] > 4
            or result["measured-demand"] > 936 or result["processed"] != result["growth-mirror"]
            or max(result["processed"], result["semantic"]) > result["observed-tick"]
            or result["demand-observed"] != "True" or result["remaining-demand"] != 0
            or result["schedule-observations"] < 1):
        raise ValueError("travel clock/budget envelope failed")
    if result["mode"] == "present" and result["travel-turns"] != 0:
        raise ValueError("present leg reports travel")
    if economic and (result["containers"] != 252 or result["stress-initial-thirds"] != 732
            or result["stress-residents"] != 0 or result["resume-applications"] != 1
            or not result["pause-local-start"] <= result["pause-disabled"] < result["pause-resumed"] <= result["observed-tick"]
            or result["paused-ticks"] != result["pause-prior"] + result["pause-resumed"] - result["pause-local-start"]
            or result["arrival-interval"] <= 0
            or result["resume-arrival"] != result["pause-resumed"] + result["arrival-interval"]):
        raise ValueError("independent pause arithmetic or populated-container evidence differs")
    return result


def assess(rows, mode, require_economic=False):
    try:
        result = witness(rows)
        economic = result["pause-effects-proved"] == "true"
        if require_economic and not economic:
            raise ValueError("economic persona cannot accept a continuity-only witness")
        # Provider admission describes the installed profile, not a failed scripted step.
        # Keep these rows available for operator warnings, but exempt only their exact
        # legal refusal outcome. Other bookkeeping and travel failures still refuse.
        failed_outcome = any(outcome != "OK" and (verb, outcome) != ("VERB-REFUSED", "REFUSED")
                             for verb, outcome, _message in rows)
        if result["mode"] != mode or failed_outcome:
            raise ValueError("travel mode or a journal outcome differs")
        advances = [row[2] for row in rows if row[0] == "advance-complete"]
        prefix = (1200, 1, 1200) if economic else (1200,)
        warmup = re.fullmatch(r"([1-9][0-9]{0,18}) turn\(s\) elapsed of 1200 requested",
                              advances[0] if advances else "")
        drain = re.fullmatch(r"([1-9][0-9]{0,18}) turn\(s\) elapsed of 39 requested",
                             advances[-1] if advances else "")
        if (warmup is None or not 1200 <= int(warmup[1]) <= 9223372036854775807
                or advances[1:-1] != [f"{n} turn(s) elapsed of {n} requested" for n in prefix]
                or drain is None or not 39 <= int(drain[1]) <= 9223372036854775807):
            raise ValueError("travel requires completed 1200-turn warmup, exact intervening waits, and completed requested 39-turn drain advance")
        names = [row[0] for row in rows]
        expected = ["realize", "advance-complete", "beta-" + mode]
        if economic:
            expected = ["realize", "advance-complete", "beta-local-pause", "advance-complete", "beta-master-pause",
                        "advance-complete", "beta-stress", "beta-" + mode]
        if mode == "away":
            expected.append("travel-out-complete")
        expected += ["advance-complete", "beta-return"]
        if mode == "away":
            expected.append("travel-return-complete")
        expected += ["advance-complete", "yield-frames-complete", "beta-check", "SCRIPT-COMPLETE"]
        relevant = set(expected) | {"beta-away", "beta-present", "travel-out-complete", "travel-return-complete",
                                    "beta-local-pause", "beta-master-pause", "beta-stress"}
        if [name for name in names if name in relevant] != expected:
            raise ValueError("travel phases and completed advances are out of order")
        if mode == "away":
            steps = []
            for name in ("travel-out-complete", "travel-return-complete"):
                message = next(row[2] for row in rows if row[0] == name)
                match = re.fullmatch(r"normal-walk=true; steps=([1-9][0-9]{0,2})", message)
                if not match or not 1 <= int(match[1]) <= 240:
                    raise ValueError("travel lacks bounded normal-walk evidence")
                steps.append(int(match[1]))
            if steps[0] != steps[1]:
                raise ValueError("travel route lengths differ")
        for name in ("beta-" + mode, "beta-return", "beta-check", "SCRIPT-COMPLETE", "yield-frames-complete"):
            if names.count(name) != 1:
                raise ValueError("missing or duplicate travel milestone: " + name)
        for name, begin, end in (("travel-out-complete", "beta-away", "beta-return"),
                                 ("travel-return-complete", "beta-return", "beta-check")):
            if mode == "away":
                if names.count(name) != 1 or not names.index(begin) < names.index(name) < names.index(end):
                    raise ValueError("travel movement completion missing, repeated or out of order")
            elif name in names:
                raise ValueError("present leg contains a movement milestone")
        if not names.index("beta-return") < names.index("yield-frames-complete") < names.index("beta-check"):
            raise ValueError("render yield is outside the return/check leg")
        return []
    except (ValueError, KeyError) as error:
        return [str(error)]


def compare(present, away):
    for rows, mode in ((present, "present"), (away, "away")):
        errors = assess(rows, mode)
        if errors:
            raise ValueError("; ".join(errors))
    left, right = witness(present), witness(away)
    for key in ("seed", "home", "wait-turns"):
        if left[key] != right[key]:
            raise ValueError("presence pair is not matched: " + key)
    if left["pause-effects-proved"] != right["pause-effects-proved"]:
        raise ValueError("presence pair mixes economic and continuity-only evidence")
    return {"scope": "developer-presence-pair", "seed": left["seed"], "home": left["home"],
            "waitTurns": left["wait-turns"], "awayTravelTurns": right["travel-turns"],
            "deltas": {key: right[key] - left[key] for key in ("containers", "drain-turns", "peak-thirds", "peak-heavy", "measured-demand")},
            "equalTotalElapsedClaimed": False, "fullEnvelopeStress": left["full-envelope-stress"] == "true",
            "residentStress": False, "releaseAcceptance": False}
