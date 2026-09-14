"""Synthetic adversarial evidence tests; no claim of native save/load acceptance."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import camp_heart_chain_load_check as check
from heart_chain_fixture import facts, journal_fixture, physical


class HeartChainLoadCheckTests(unittest.TestCase):
    def test_full_source_and_loaded_wait_keep_actual_overshoot(self):
        result = check.judge(*journal_fixture())
        self.assertEqual(("PASS", "next-fire", "new-fire", 31201, 3601), tuple(result[key] for key in
                         ("verdict", "newJobId", "outputId", "sourceActualTurns", "loadedActualTurns")))

    def test_missing_duplicate_refused_and_reordered_required_witnesses_fail(self):
        fixture = journal_fixture()
        for side in (0, 1):
            indices = [i for i, row in enumerate(fixture[side])
                       if row[0] not in ("advance-progress", "advance-guard", "advance-complete", "advance")]
            for index in indices:
                for mode in ("remove", "duplicate", "refuse", "reorder"):
                    rows = copy.deepcopy(fixture)
                    if mode == "remove": del rows[side][index]
                    elif mode == "duplicate": rows[side].insert(index, rows[side][index])
                    elif mode == "refuse": rows[side][index] = rows[side][index][0], "REFUSED", rows[side][index][2]
                    elif index == 0: continue
                    else: rows[side][index - 1], rows[side][index] = rows[side][index], rows[side][index - 1]
                    # Non-positional bookkeeping can sit on either side of a source observation.
                    if mode == "reorder" and side == 0 and fixture[side][index - 1][0] == "advance-complete": continue
                    with self.subTest(side=side, index=index, mode=mode), self.assertRaises(ValueError): check.judge(*rows)

    def test_loaded_identity_payment_clock_input_and_completion_changes_fail(self):
        mutations = {
            "LOAD-BEGIN": [("new-game=false", "new-game=true"), ("game-id=01234567", "game-id=11234567")],
            "camp-heart-chain-load-input": [("original-ran=0", "original-ran=1"), ("owned=True", "owned=False")],
            "camp-heart-chain-preactivation": [("heart=heart", "heart=replaced"), ("turns=31204", "turns=31205")],
            "camp-heart-chain-loaded": [("timber=1", "timber=2"), ("resident=resident-50", "resident=other")],
            "camp-heart-chain-next": [("new-job=next-fire", "new-job=paid-court"), ("water-debited=2", "water-debited=0"),
                ("timber-debited=1", "timber-debited=0"), ("synthetic-materials-after-load=0", "synthetic-materials-after-load=1")],
            "camp-heart-chain-resume": [("vanilla-Continue=true", "vanilla-Continue=false"), ("requested-turns=3600", "requested-turns=1200")],
            "advance-complete": [("3601 turn(s)", "3599 turn(s)")],
            "camp-heart-chain-completed": [("turns=34805", "turns=34804"), ("time-ticks=481210", "time-ticks=445200"),
                ("output=new-fire", "output=store"), ("new-job=next-fire", "new-job=other"), ("phase=Complete", "phase=Working"),
                ("brush=21", "brush=20"), ("original-bodies-retained=true", "original-bodies-retained=false")],
            "SCRIPT-COMPLETE": [("popup-restored=true", "popup-restored=false"), ("ordinary-acceptance=false", "ordinary-acceptance=true")]
        }
        for event, changes in mutations.items():
            for before, after in changes:
                args = journal_fixture()
                index = next(i for i, row in enumerate(args[1]) if row[0] == event)
                self.assertIn(before, args[1][index][2])
                args[1][index] = event, "OK", args[1][index][2].replace(before, after)
                with self.subTest(event=event, before=before), self.assertRaises(ValueError): check.judge(*args)

    def test_replay_duplicate_fields_and_observations_after_terminal_fail(self):
        for event in ("camp-heart-chain-setup", "camp-heart-chain-save", "stagedigest", "unknown"):
            args = journal_fixture()
            args[1].insert(3, (event, "OK", "unexpected"))
            with self.assertRaises(ValueError): check.judge(*args)
        args = journal_fixture()
        event, status, message = args[1][3]
        args[1][3] = event, status, message + "; brush=21"
        with self.assertRaises(ValueError): check.judge(*args)
        args = journal_fixture()
        args[1].append(("advance-progress", "OK", "after terminal"))
        with self.assertRaises(ValueError): check.judge(*args)

    def fact_fixture(self):
        wire, source, _ = physical()
        phases = {phase: copy.deepcopy(source) for phase in ("preactivation", "activated", "completed")}
        completed = phases["completed"]
        completed["jobs"]["next-fire"] = ["new paid receipt"]
        del completed["custody"]["timber"]
        completed["residents"]["body:resident-50"][2:] = ["11", "22", "other-live-roof"]
        completed["support"]["water:basin"] = ["ordinary consumption"]
        return check.snapshot_codec.decode_snapshot(wire), source, phases, "next-fire"

    def test_physical_evidence_allows_normal_movement_and_resource_use(self):
        check.judge_facts(*self.fact_fixture())

    def test_physical_loss_duplication_replacement_and_pre_activation_repair_fail(self):
        for phase in ("preactivation", "activated", "completed"):
            for domain, key in (("jobs", "paid-court"), ("residents", "body:resident-50"),
                                ("support", "stake:0"), ("custody", "brush-0")):
                for mode in ("remove", "replace", "duplicate"):
                    args = self.fact_fixture()
                    rows = args[2][phase][domain]
                    if mode == "remove": del rows[key]
                    elif mode == "replace": rows[key] = ["replacement"]
                    else: rows[key + "-duplicate"] = list(rows[key])
                    with self.subTest(phase=phase, domain=domain, mode=mode), self.assertRaises(ValueError): check.judge_facts(*args)
        for domain in check.snapshot_codec.DOMAINS:
            args = self.fact_fixture()
            del args[2]["completed"][domain]
            with self.assertRaises(ValueError): check.judge_facts(*args)
        args = self.fact_fixture()
        args[2]["completed"]["custody"]["timber"] = args[1]["custody"]["timber"]
        with self.assertRaises(ValueError): check.judge_facts(*args)

    def test_cli_reads_exact_imported_facts_and_retains_failure_without_overwriting(self):
        for changed in (None, "imported", "activated", "completed"):
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as folder:
                source, loaded = Path(folder) / "source", Path(folder) / "loaded"
                source.mkdir(); (loaded / "Local").mkdir(parents=True)
                wire, _, wires = physical()
                (source / "scenario-save-snapshot.txt").write_bytes(wire)
                (loaded / "Local/scenario-load-snapshot.txt").write_bytes(wire)
                for root, rows in zip((source, loaded), journal_fixture()[:2]):
                    (root / "scenario-journal.tsv").write_text("".join("2026-09-14T00:00:00Z\t" + "\t".join(row) + "\n" for row in rows))
                phases = self.fact_fixture()[2]
                for domain, raw in wires.items():
                    name = check.snapshot_codec.fact_name(domain)
                    (source / name).write_bytes(raw)
                    (loaded / "Local" / name).write_bytes(raw)
                    for phase, records in phases.items():
                        encoded = facts(domain, [[key] + row for key, row in sorted(records[domain].items())])
                        (loaded / check.snapshot_codec.fact_name(domain, phase)).write_bytes(encoded)
                if changed:
                    bad = (loaded / "Local" / check.snapshot_codec.fact_name("jobs") if changed == "imported" else
                           loaded / check.snapshot_codec.fact_name("jobs", changed))
                    bad.write_bytes(facts("jobs", [["replacement", "receipt"]]))
                output = Path(folder) / "result.json"
                command = [sys.executable, str(Path(check.__file__)), str(source), str(loaded), "--results", str(output)]
                result = subprocess.run(command, capture_output=True, text=True)
                self.assertEqual(0 if changed is None else 1, result.returncode, result.stderr)
                self.assertEqual("PASS" if changed is None else "FAIL", json.loads(output.read_text())["verdict"])
                original = output.read_bytes()
                self.assertNotEqual(0, subprocess.run(command, capture_output=True).returncode)
                self.assertEqual(original, output.read_bytes())


if __name__ == "__main__": unittest.main()
