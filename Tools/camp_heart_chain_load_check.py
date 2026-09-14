"""Higher-heart source/load evidence oracle; require separate seals, strict logs and owned stops."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

import camp_heart_chain_snapshot as snapshot_codec
from guest_save_check import field, one, require
from personas import persona_matrix
import scenario_advance_check

TOOLS = Path(__file__).resolve().parent
SOURCE_WAITS = (1200, 3600, 1200, 1200, 1200, 7200, 1200, 6600, 6600, 1200)


def equal_fields(detail, expected):
    for name, value in expected.items():
        require(field(detail, name) == str(value), "higher-heart invariant differs: " + name)


def identity(snapshot, clocks=True):
    result = {name: snapshot[name]["id"] for name in ("heart", "basin", "store", "track")}
    result.update(rung=snapshot["rung"], job=snapshot["job_id"], resident=snapshot["resident_id"],
                  population=snapshot["population"])
    if clocks:
        result.update(turns=snapshot["turns"], **{"time-ticks": snapshot["time_ticks"]})
    return result


def judge(source, loaded, snapshot, digest):
    for rows in (source, loaded):
        terminal, _ = one(rows, "SCRIPT-COMPLETE")
        require(terminal == len(rows) - 1 and all(outcome == "OK" for _, outcome, _ in rows),
                "session refused or continued after its single completion")
    spec, name = persona_matrix.load(str(TOOLS / "personas/camp-heart-chain-save.persona"))
    expected = persona_matrix.parse_expect(spec["EXPECT"], name, set(spec["VERBS"].split(",")))
    require(not persona_matrix.match(expected, persona_matrix.significant(source)), "source chain persona did not pass")
    waits = scenario_advance_check.judge(source, SOURCE_WAITS)
    clocks = scenario_advance_check.bind_chain_clocks(source, waits)
    require(clocks["chainEndTurns"] == snapshot["turns"], "source save advanced the chain clock")
    _, saved = one(source, "camp-heart-chain-save")
    equal_fields(saved, dict(identity(snapshot), **{"paid-heart-chain-save": "true", "save": snapshot["game_id"],
                 "synthetic-next-job-timber": "1", "brush": "21", "snapshot-sha256": digest,
                 "physical-state-preserved": "true", "normal-next-quote": "true", "outside-final-heart": "true"}))
    _, preflight = one(source, "camp-heart-chain-next-preflight")
    equal_fields(preflight, {"spare-site-preflight": "true", "ordinary-quote-price": "true", "outside-final-heart": "true",
                            "water": "2", "timber": "1", "no-debit": "true"})
    names = ("LOAD-BEGIN", "camp-heart-chain-load-input", "camp-heart-chain-preactivation",
             "camp-heart-chain-loaded", "camp-heart-chain-next", "camp-heart-chain-resume",
             "advance-complete", "camp-heart-chain-completed", "SCRIPT-COMPLETE")
    observations = [one(loaded, name) for name in names]
    require([at for at, _ in observations] == sorted(at for at, _ in observations), "load witnesses reordered")
    allowed = set(names) | {"advance", "advance-guard", "advance-progress"}
    require(all(event in allowed for event, _, _ in loaded), "foreign event or source script replay in load journal")
    begin, isolation, preactivation, activated, paid, resume, _, complete, terminal = [detail for _, detail in observations]
    equal_fields(begin, {"game-id": snapshot["game_id"], "new-game": "false", "mod-restore": "false"})
    equal_fields(isolation, {"original-update-skipped": "true", "scope": "dedicated-game-lifetime",
                             "installed": "True", "owned": "True", "original-ran": "0"})
    expected_saved = dict(identity(snapshot), **{"snapshot-sha256": digest})
    equal_fields(preactivation, dict(expected_saved, **{"before-AfterGameLoaded": "true"}))
    equal_fields(activated, dict(expected_saved, brush=21, timber=1))
    equal_fields(paid, {"prior-job": snapshot["job_id"], "water-debited": "2", "timber-debited": "1",
                        "synthetic-materials-after-load": "0", "prior-jobs-retained": "true"})
    next_job = field(paid, "new-job")
    require(next_job != snapshot["job_id"], "loaded job reused prior heart job")
    equal_fields(resume, {"vanilla-Continue": "true", "saved-script-considered": "true", "requested-turns": "3600"})
    after_waits = scenario_advance_check.judge(loaded, (3600,))
    wait_intent, _ = one(loaded, "advance")
    require(observations[4][0] < after_waits[0]["start"] < wait_intent < observations[5][0],
            "wait began before payment or resume preceded its wait intent")
    equal_fields(complete, dict(identity(snapshot, clocks=False), **{"new-job": next_job, "phase": "Complete",
                 "effects-settled": "true", "brush": "21", "prior-jobs-retained": "true", "original-bodies-retained": "true"}))
    turns, ticks = int(field(complete, "turns")), int(field(complete, "time-ticks"))
    require(turns - snapshot["turns"] == after_waits[0]["elapsed"] and ticks > snapshot["time_ticks"],
            "loaded world clock does not match the actual ordinary wait")
    output = field(complete, "output")
    require(output not in {snapshot[key]["id"] for key in ("heart", "basin", "store", "track")}
            | {snapshot["resident_id"]}, "loaded output aliases an original body")
    equal_fields(terminal, {"real-save-quit-load": "true", "next-paid-job-complete": "true",
                           "new-game-script-replayed": "false", "popup-restored": "true", "ordinary-acceptance": "false"})
    return dict(verdict="PASS", gameId=snapshot["game_id"], snapshotSha256=digest, newJobId=next_job,
                outputId=output, sourceRequestedTurns=31200, sourceActualTurns=clocks["elapsedTurns"],
                loadedRequestedTurns=3600, loadedActualTurns=after_waits[0]["elapsed"],
                scope="Synthetic paid court save/load evidence only; require full profile/source bindings, strict logs and owned stops.")


def judge_facts(snapshot, source, phases, next_job):
    require(set(source) == set(snapshot_codec.DOMAINS) and set(phases) == {"preactivation", "activated", "completed"},
            "missing source or load fact domains/phases")
    for phase in ("preactivation", "activated"):
        require(phases[phase] == source, "physical facts changed at " + phase)
    completed = phases["completed"]
    require(set(completed) == set(source), "completed fact domain missing")
    require(next_job not in source["jobs"] and snapshot["job_id"] in source["jobs"]
            and set(completed["jobs"]) == set(source["jobs"]) | {next_job}, "next job reused or lost paid history")
    require(all(completed["jobs"][key] == row for key, row in source["jobs"].items()), "prior paid receipt changed")
    bodies = {key: row for key, row in source["residents"].items() if key.startswith("body:")}
    after = {key: row for key, row in completed["residents"].items() if key.startswith("body:")}
    require(len(bodies) == snapshot["population"] and set(bodies) == set(after)
            and "body:" + snapshot["resident_id"] in bodies, "original resident body identities changed")
    require(all(len(row) == 5 and len(after[key]) == 5 and row[:2] == after[key][:2] and after[key][4]
                for key, row in bodies.items()), "resident blueprint/binding changed or roof was lost")
    stakes = {key: row for key, row in source["support"].items() if key.startswith("stake:")}
    after_stakes = {key: row for key, row in completed["support"].items() if key.startswith("stake:")}
    require(len(stakes) == 4 and stakes == after_stakes, "original founding stakes changed")
    brush, timber = {}, []
    for key, row in source["custody"].items():
        require(len(row) == 3 and row[1:] == ["inv:" + snapshot["store"]["id"], "1"], "saved material custody differs")
        if row[0] == "r_KingdomBrush":
            brush[key] = row
        else:
            require(row[0] == "r_KingdomTimber", "unexpected saved material")
            timber.append(key)
    require(len(brush) == 21 and len(timber) == 1 and completed["custody"] == brush,
            "next job failed to spend only its timber and preserve all original brush")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("loaded", type=Path)
    parser.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location("heart_chain_profile_reader", TOOLS / "prepare-scenario-load.py")
    reader = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reader)
    try:
        wire = reader.read_bytes(args.source / "scenario-save-snapshot.txt", snapshot_codec.MAX_SNAPSHOT)
        snapshot = snapshot_codec.decode_snapshot(wire)
        require(wire == reader.read_bytes(args.loaded / "Local/scenario-load-snapshot.txt", snapshot_codec.MAX_SNAPSHOT),
                "imported snapshot differs from source")
        journals = [persona_matrix.read_journal(reader.read_bytes(root / "scenario-journal.tsv", 16777216).decode("utf-8-sig"))
                    for root in (args.source, args.loaded)]
        result = judge(*journals, snapshot, hashlib.sha256(wire).hexdigest())
        source, phases = {}, {name: {} for name in ("preactivation", "activated", "completed")}
        for domain in snapshot_codec.DOMAINS:
            raw = reader.read_bytes(args.source / snapshot_codec.fact_name(domain), snapshot_codec.MAX_FACT_WIRE)
            source[domain] = snapshot_codec.decode_facts(raw, domain, snapshot[domain + "_digest"])
            require(raw == reader.read_bytes(args.loaded / "Local" / snapshot_codec.fact_name(domain), snapshot_codec.MAX_FACT_WIRE),
                    "imported fact bytes differ: " + domain)
            for phase in phases:
                raw = reader.read_bytes(args.loaded / snapshot_codec.fact_name(domain, phase), snapshot_codec.MAX_FACT_WIRE)
                digest = hashlib.sha256(raw).hexdigest() if phase == "completed" else snapshot[domain + "_digest"]
                phases[phase][domain] = snapshot_codec.decode_facts(raw, domain, digest)
        judge_facts(snapshot, source, phases, result["newJobId"])
        result["retainedPhysicalFacts"] = "PASS"
    except (OSError, ValueError) as error:
        result = dict(verdict="FAIL", reason=str(error))
    with args.results.open("x") as output:
        output.write(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))
    return 0 if result["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
