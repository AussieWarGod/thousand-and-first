"""Synthetic wire builders for host contract tests; no engine acceptance."""
import base64
import hashlib
from pathlib import Path
import struct

import camp_heart_chain_snapshot as codec

FIXTURES = Path(__file__).resolve().parents[2] / "DevTests/Fixtures"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def number(value):
    return struct.pack("<i", value)


def text(value):
    if value is None:
        return number(-1)
    raw = value.encode("utf-8")
    return number(len(raw)) + raw


def facts(domain, rows):
    body = number(1) + text(domain) + number(len(rows))
    for row in rows:
        body += number(len(row)) + b"".join(text(value) for value in row)
    return codec.FACT_PREFIX + base64.b64encode(body)


def saved(**changes):
    result = codec.decode_snapshot((FIXTURES / "heart-chain-save-v1.wire").read_bytes())
    result.update(changes)
    body = number(0x31434354) + number(1)
    for key in ("game_id", "realm_id", "city_id", "zone_id", "resident_id", "job_id",
                "jobs_digest", "residents_digest", "support_digest", "custody_digest"):
        body += text(result[key])
    for key in ("heart", "basin", "store", "track"):
        body += text(result[key]["id"]) + number(result[key]["x"]) + number(result[key]["y"])
    body += b"".join(number(result[key]) for key in ("rung", "population", "water", "food"))
    body += struct.pack("<qq", result["turns"], result["time_ticks"])
    return codec.PREFIX + base64.b64encode(body)


def physical():
    rows = dict(jobs={"paid-court": ["prior receipt"], "paid-tent": ["older receipt"]},
                residents={"body:resident-" + str(i): ["Citizen", "stable-" + str(i), "10", "20", "roof"]
                           for i in range(1, 51)},
                support={"stake:" + str(i): [str(i), "authentic"] for i in range(4)},
                custody={"brush-" + str(i): ["r_KingdomBrush", "inv:store", "1"] for i in range(21)})
    rows["residents"]["book:stable-50"] = ["exact resident record"]
    rows["support"]["water:basin"] = ["4848"]
    rows["custody"]["timber"] = ["r_KingdomTimber", "inv:store", "1"]
    wires = {domain: facts(domain, [[key] + row for key, row in sorted(values.items())])
             for domain, values in rows.items()}
    wire = saved(**{domain + "_digest": sha(raw) for domain, raw in wires.items()})
    return wire, rows, wires


def detail(values):
    return "; ".join(str(key) + "=" + str(value) for key, value in values.items())


def journal_fixture():
    import camp_heart_chain_load_check as check
    from scenario_advance_check_test import AdvanceCheckTests
    wire, physical_rows, wires = physical()
    snapshot = codec.decode_snapshot(wire)
    spec, name = check.persona_matrix.load(str(check.TOOLS / "personas/camp-heart-chain-save.persona"))
    expected = check.persona_matrix.parse_expect(spec["EXPECT"], name, set(spec["VERBS"].split(",")))
    source, clock, wait_index = [], snapshot["turns"] - sum(check.SOURCE_WAITS) - 1, 0
    anchors = {"camp-heart-chain-setup", "camp-heart-chain-supply", "camp-heart-chain-check"}
    for event, status, message in expected:
        if event == "advance":
            count = check.SOURCE_WAITS[wait_index]
            rows = AdvanceCheckTests().fixture((count,), extra=0)
            extra = 1 if wait_index == 0 else 0
            rows[-1] = "advance-complete", "OK", f"{count + extra} turn(s) elapsed of {count} requested"
            source.extend(rows)
            clock += count + extra
            wait_index += 1
            continue
        if event in anchors: message += "; turns=" + str(clock)
        if event == "camp-heart-chain-next-preflight":
            message += "; ordinary-quote-price=true; outside-final-heart=true; water=2; timber=1; no-debit=true"
        if event == "camp-heart-chain-save":
            message = detail(dict({"paid-heart-chain-save": "true", "rung": snapshot["rung"]}, **check.identity(snapshot),
                                 save=snapshot["game_id"], **{"synthetic-next-job-timber": 1,
                                 "brush": 21, "snapshot-sha256": sha(wire), "physical-state-preserved": "true", "normal-next-quote": "true", "outside-final-heart": "true"}))
        source.append((event, status or "OK", message))
    def row(event, **values): return event, "OK", detail(values)
    saved_values = dict(check.identity(snapshot), **{"snapshot-sha256": sha(wire)})
    loaded = [row("LOAD-BEGIN", **{"game-id": snapshot["game_id"], "new-game": "false", "mod-restore": "false"}),
              row("camp-heart-chain-load-input", installed="True", owned="True", **{"original-ran": 0,
                  "original-update-skipped": "true", "scope": "dedicated-game-lifetime"}),
              row("camp-heart-chain-preactivation", **saved_values, **{"before-AfterGameLoaded": "true"}),
              row("camp-heart-chain-loaded", **saved_values, brush=21, timber=1),
              row("camp-heart-chain-next", **{"new-job": "next-fire", "prior-job": snapshot["job_id"],
                  "water-debited": 2, "timber-debited": 1, "synthetic-materials-after-load": 0, "prior-jobs-retained": "true"})]
    wait = AdvanceCheckTests().fixture((3600,), extra=0)
    wait[-1] = "advance-complete", "OK", "3601 turn(s) elapsed of 3600 requested"
    loaded += wait[:2] + [row("camp-heart-chain-resume", **{"vanilla-Continue": "true",
                  "saved-script-considered": "true", "requested-turns": 3600})] + wait[2:]
    completed = dict(check.identity(snapshot, clocks=False), **{"new-job": "next-fire", "output": "new-fire",
                     "turns": snapshot["turns"] + 3601, "time-ticks": snapshot["time_ticks"] + 36010,
                     "phase": "Complete", "effects-settled": "true", "brush": 21,
                     "prior-jobs-retained": "true", "original-bodies-retained": "true"})
    loaded += [row("camp-heart-chain-completed", **completed), row("SCRIPT-COMPLETE", **{
               "real-save-quit-load": "true", "next-paid-job-complete": "true", "new-game-script-replayed": "false",
               "popup-restored": "true", "ordinary-acceptance": "false"})]
    return source, loaded, snapshot, sha(wire)
