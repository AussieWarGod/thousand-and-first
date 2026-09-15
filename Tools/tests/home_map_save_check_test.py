import copy
import sys
from pathlib import Path
import unittest
import subprocess
import tempfile
import json
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import home_map_save_check as oracle


class HomeMapSaveCheckTests(unittest.TestCase):
    def fixture(self):
        stamp = "residents=4; maps=2; exact-homes=true; receipt-sha256=" + "a" * 64 + "; world-repair=false"
        visit = ("native-home-map cases=1 passed=1 failed=0; home-zone=home; away-zone=away; resident=1; body=511; "
                 "original-home=123; plot=tentrow; occupied-before=1; occupied-away=1; visited-home=123; visited-bound=away; "
                 "visited-plot=tentrow; physical-return=true; home-repair=false; return-settlement-pass=true; "
                 "reservation-owner-exact=true; synthetic-housing=false; active-away=true; claim-reused=")
        source = [("home-map-native", "OK", visit + "false"), ("home-map-save-witness", "OK", stamp + "; witness=recorded"),
                  ("lifecycle-save", "OK", "real-save=true"), ("SCRIPT-COMPLETE", "OK", "done")]
        loaded = [("home-map-load-preactivation", "OK", stamp + "; before-AfterGameLoaded=true"),
                  ("home-map-load-verified", "OK", stamp), ("home-map-load-travel", "OK", visit + "true"),
                  ("lifecycle-loaded", "OK", "loaded"), ("lifecycle-next", "OK", "paid"), ("SCRIPT-COMPLETE", "OK", "done")]
        return source, loaded

    def test_complete_chain(self):
        self.assertEqual("PASS", oracle.judge(*self.fixture())["verdict"])

    def test_missing_repeated_failed_and_reordered_phases(self):
        source, original = self.fixture()
        for index in range(len(original)):
            for change in ("missing", "duplicate", "failed"):
                loaded = copy.copy(original)
                if change == "missing": loaded.pop(index)
                elif change == "duplicate": loaded.insert(index, loaded[index])
                else: loaded[index] = loaded[index][0], "REFUSED", loaded[index][2]
                with self.subTest(index=index, change=change), self.assertRaises(ValueError):
                    oracle.judge(source, loaded)
        loaded = copy.copy(original); loaded[0], loaded[1] = loaded[1], loaded[0]
        with self.assertRaises(ValueError): oracle.judge(source, loaded)

    def test_changed_facts_and_masked_repair_refuse(self):
        for index, old, new in ((0, "a" * 64, "b" * 64), (0, "before-AfterGameLoaded=true", "before-AfterGameLoaded=false"),
                               (1, "residents=4", "residents=3"), (1, "world-repair=false", "world-repair=true"),
                               (2, "body=511", "body=512"), (2, "occupied-away=1", "occupied-away=0"),
                               (2, "home-repair=false", "home-repair=true"), (2, "visited-bound=away", "visited-bound=home"),
                               (2, "visited-home=123", "visited-home=0"), (2, "visited-plot=tentrow", "visited-plot=other"),
                               (2, "claim-reused=true", "claim-reused=false"), (2, "reservation-owner-exact=true", "reservation-owner-exact=false")):
            source, loaded = self.fixture(); event, outcome, detail = loaded[index]
            loaded[index] = event, outcome, detail.replace(old, new)
            with self.subTest(old=old), self.assertRaises(ValueError): oracle.judge(source, loaded)

    def test_duplicate_field_and_fake_save_refuse(self):
        source, loaded = self.fixture(); event, outcome, detail = source[1]
        source[1] = event, outcome, detail + "; maps=2"
        with self.assertRaises(ValueError): oracle.judge(source, loaded)
        source, loaded = self.fixture(); source[2] = "lifecycle-save", "OK", "real-save=false"
        with self.assertRaises(ValueError): oracle.judge(source, loaded)

    def test_cli_reads_actual_journal_files(self):
        with tempfile.TemporaryDirectory() as folder:
            paths = []
            for name, rows in zip(("source.tsv", "loaded.tsv"), self.fixture()):
                path = Path(folder) / name
                path.write_text("".join("2026-09-15T00:00:00.000Z\t" + "\t".join(row) + "\n" for row in rows))
                paths.append(str(path))
            result = subprocess.run([sys.executable, oracle.__file__, *paths], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual("PASS", json.loads(result.stdout)["verdict"])
