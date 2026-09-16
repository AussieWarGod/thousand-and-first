import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import home_map_absent_check as oracle
import home_map_save_check_test as saved_home_tests


class HomeMapAbsentCheckTests(unittest.TestCase):
    def fixture(self):
        warm = saved_home_tests.HomeMapSaveCheckTests().fixture()[0][0]
        stamp = ("residents=4; local=3; away=1; maps=2; body=511 resident=1; home-zone=home away-zone=away; "
                 "original-home=123 plot=tentrow occupied=1; receipt-sha256=" + "a" * 64 + "; world-repair=false")
        source = [warm, ("lifecycle-grown", "OK", "grown"),
                  ("home-map-absent-save-witness", "OK", stamp + "; witness=recorded; saved-away=true; synthetic-transfer=true"),
                  ("lifecycle-save", "OK", "real-save=true"), ("SCRIPT-COMPLETE", "OK", "done")]
        loaded = [("home-map-absent-preactivation", "OK", stamp + "; before-AfterGameLoaded=true; before-remote-lookup=true"),
                  ("home-map-absent-loaded", "OK", stamp + "; before-remote-lookup=true; capacity-retained=true"),
                  ("home-map-absent-return", "OK", stamp.replace("local=3; away=1", "local=4; away=0")
                   + "; exact-body=true; return-settlement-pass=true; capacity-retained=true"),
                  ("lifecycle-loaded", "OK", "loaded"), ("lifecycle-next", "OK", "paid"), ("SCRIPT-COMPLETE", "OK", "done")]
        return source, loaded

    def test_complete_chain(self):
        self.assertEqual("PASS", oracle.judge(*self.fixture())["verdict"])

    def test_each_required_row_must_exist_once_and_pass(self):
        original = self.fixture()
        for side in (0, 1):
            for index in range(len(original[side])):
                for change in ("missing", "duplicate", "refused"):
                    rows = copy.deepcopy(original)
                    if change == "missing": rows[side].pop(index)
                    elif change == "duplicate": rows[side].insert(index, rows[side][index])
                    else:
                        event, _, detail = rows[side][index]; rows[side][index] = event, "REFUSED", detail
                    with self.subTest(side=side, index=index, change=change), self.assertRaises(ValueError):
                        oracle.judge(*rows)

    def test_all_phase_boundaries_require_order(self):
        for side in (0, 1):
            for index in range(len(self.fixture()[side]) - 2):
                rows = self.fixture(); rows[side][index], rows[side][index + 1] = rows[side][index + 1], rows[side][index]
                with self.subTest(side=side, index=index), self.assertRaises(ValueError): oracle.judge(*rows)

    def test_wrong_identity_capacity_early_return_or_repair_refuse(self):
        changes = [(0, 2, "saved-away=true", "saved-away=false"), (0, 3, "real-save=true", "real-save=false"),
                   (1, 0, "before-AfterGameLoaded=true", "before-AfterGameLoaded=false"),
                   (1, 1, "before-remote-lookup=true", "before-remote-lookup=false"),
                   (1, 2, "exact-body=true", "exact-body=false"),
                   (1, 2, "return-settlement-pass=true", "return-settlement-pass=false")]
        for index in range(3):
            changes.extend((1, index, old, new) for old, new in
                           (("body=511", "body=512"), ("resident=1", "resident=2"), ("occupied=1", "occupied=0"),
                            ("home-zone=home", "home-zone=away"), ("away-zone=away", "away-zone=home"),
                            ("original-home=123", "original-home=0"), ("plot=tentrow", "plot=other"),
                            ("a" * 64, "b" * 64), ("world-repair=false", "world-repair=true"),
                            ("local=3; away=1", "local=4; away=0") if index != 2 else ("local=4; away=0", "local=3; away=1")))
        for side, index, old, new in changes:
            rows = self.fixture(); event, outcome, detail = rows[side][index]
            self.assertIn(old, detail)
            rows[side][index] = event, outcome, detail.replace(old, new)
            with self.subTest(side=side, index=index, old=old), self.assertRaises(ValueError): oracle.judge(*rows)

    def test_repeated_fields_cannot_mask_failure(self):
        for side, index in ((0, 2), (1, 0), (1, 1), (1, 2)):
            rows = self.fixture(); event, outcome, detail = rows[side][index]
            rows[side][index] = event, outcome, detail + " world-repair=false"
            with self.assertRaises(ValueError): oracle.judge(*rows)

    def test_cli_reads_journals_and_writes_verdict(self):
        with tempfile.TemporaryDirectory() as folder:
            paths = []
            for name, rows in zip(("source.tsv", "loaded.tsv"), self.fixture()):
                path = Path(folder) / name
                path.write_text("".join("2026-09-15T00:00:00.000Z\t" + "\t".join(row) + "\n" for row in rows))
                paths.append(str(path))
            result_path = Path(folder) / "result.json"
            result = subprocess.run([sys.executable, oracle.__file__, *paths, "--results", str(result_path)], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual("PASS", json.loads(result_path.read_text())["verdict"])
