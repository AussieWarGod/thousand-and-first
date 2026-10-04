"""Synthetic checker fixtures only: not a native unfounded save, cold load or ordinary acceptance."""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("unfounded_results", TOOLS / "check-unfounded-results.py")
check = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(check)
finally:
    sys.path.pop(0)
GAME = "01234567-89ab-cdef-0123-456789abcdef"
OTHER = "11234567-89ab-cdef-0123-456789abcdef"
LIFECYCLE = "a" * 64
CARRY = "b" * 64
STAMP = "2026-10-04T12:34:56.123Z"


def snapshot(game=GAME, zone="JoppaWorld.8.22.1.1.10", frame="5", lifecycle=LIFECYCLE):
    return ("taf-unfounded-save-v1:" + ";".join((game, zone, frame, lifecycle, CARRY, "1234"))).encode()


def save_rows(game=GAME, lifecycle=LIFECYCLE):
    return [("AUTOSTART", "OK", "sealed script present; test game started without input"),
            ("stagedigest", "OK", "founded=false; taf-scenario-digest"),
            ("unfounded-save", "OK", "real-save=true dormant-frame=5; founded=false; "
             "pristine-lifecycle-and-carry=true; game-id=" + game + "; lifecycle-bytes=139; lifecycle-sha256="
             + lifecycle + "; save-error=false; cold-load=false; ordinary-acceptance=false"),
            ("stagedigest", "OK", "founded=false; taf-scenario-digest"),
            ("SCRIPT-COMPLETE", "OK", "script complete")]


def load_rows(game=GAME, lifecycle=LIFECYCLE):
    dormant = ("founded=false; load-failed=false; pristine-lifecycle-and-carry=true; lifecycle-frame=5"
               "; lifecycle-sha256=" + lifecycle + "; carry-sha256=" + CARRY + "; game-id=" + game)
    founded = "founded=true; lifecycle-frame=10; growth-authority=true"
    return [("LOAD-BEGIN", "OK", "exact sealed save; game-id=" + game + "; new-game=false; mod-restore=false"),
            ("unfounded-preactivation", "OK", "before-AfterGameLoaded=true; " + dormant),
            ("unfounded-loaded", "OK", "cold-load=true; after-activation=true; " + dormant),
            ("unfounded-founded", "OK", "production-founding=true; " + founded + "; faction=Checker Realm"),
            ("unfounded-resaved", "OK", "real-save=true; " + founded + "; save-error=false"
             "; backup-is-imported-save=true; primary-changed=true; second-save-reload=false"
             "; ordinary-acceptance=false"),
            ("SCRIPT-COMPLETE", "OK", "native-unfounded cold-load session complete; real-save-quit-load=true"
             "; production-founding=true; second-real-save=true; new-game-script-replayed=false"
             "; ordinary-acceptance=false")]


def journal(rows):
    return "".join(STAMP + "\t" + verb + "\t" + outcome + "\t" + message + "\n"
                   for verb, outcome, message in rows).encode()


class UnfoundedResultsTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-unfounded-checker-tests.")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "taf-scenario.CheckerTests"
        self.seal = Path(str(self.root) + ".seal")
        self.local = self.root / "Local"

    def write(self, path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw if isinstance(raw, bytes) else raw.encode())

    def reseal(self):
        with contextlib.redirect_stdout(io.StringIO()):
            check.profile.seal(str(self.local), str(self.seal / "profile.sha256"))

    def fixture(self, phase="save", start="8.22@40,12", request="founding-first-city;seed=#4242"):
        self.write(self.local / "scenario-script.txt",
                   "# Synthetic checker test; not native proof\nstagedigest\nunfounded-save\nstagedigest\n")
        self.write(self.seal / "request.txt", request + "\n")
        wx, rest = start.split(".")
        wy, cell = rest.split("@")
        x, y = cell.split(",")
        location = "GlobalLocation:JoppaWorld.%s.%s.1.1.10@%s,%s" % (wx, wy, x, y)
        self.write(self.local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml",
                   '<stringgamestate Name="r_TAF_ScenarioRequest_v1" Value="' + request + '" />\n'
                   '<location ID="TAFTestGround" Name="[Dev] TAF" Location="' + location + '" />\n')
        self.save = self.root / "Synced/Saves" / GAME
        imported = b"\x1f\x8bchecker-unfounded-save"
        info = b'{"checkerTest":true}'
        snap = snapshot(zone="JoppaWorld.%s.%s.1.1.10" % (wx, wy))
        self.imported = hashlib.sha256(imported).hexdigest()
        if phase == "save":
            for name, raw in (("Primary.sav.gz", imported), ("Primary.json", info),
                              ("Cache.db", b"checker-cache")):
                self.write(self.save / name, raw)
            self.receipt = self.root / "scenario-save-receipt.txt"
            self.snapshot = self.root / "scenario-save-snapshot.txt"
            rows = ["taf-scenario-save-v1", GAME, self.imported, hashlib.sha256(info).hexdigest(),
                    hashlib.sha256(snap).hexdigest()]
            self.write(self.root / "scenario-journal.tsv", journal(save_rows()))
        else:
            for name, raw in (("Primary.sav.gz", b"\x1f\x8bchecker-founded-save"),
                              ("Primary.json", b'{"checkerTest":"founded"}'),
                              ("Primary.sav.gz.bak", imported), ("Cache.db", b"checker-cache-after")):
                self.write(self.save / name, raw)
            self.receipt = self.local / "scenario-load.txt"
            self.snapshot = self.local / "scenario-load-snapshot.txt"
            rows = ["taf-scenario-load-v1", GAME, self.imported, hashlib.sha256(info).hexdigest(),
                    hashlib.sha256(b"checker-cache").hexdigest(), hashlib.sha256(snap).hexdigest()]
            self.write(self.root / "scenario-journal.tsv", journal(load_rows()))
        self.write(self.receipt, "\n".join(rows) + "\n")
        self.write(self.snapshot, snap)
        self.write(self.root / "Player.log", "[TAF] loaded synthetic checker fixture only\n")
        self.reseal()

    def refused(self, phase, pattern=None):
        with self.assertRaises((ValueError, OSError, SystemExit)) as raised:
            check.verify(self.root, phase)
        if pattern:
            self.assertRegex(str(raised.exception), pattern)

    def test_both_phases_pass_with_explicit_limits_and_bound_identities(self):
        for phase in ("save", "load"):
            with self.subTest(phase=phase), tempfile.TemporaryDirectory(dir=self.temp.name) as nested:
                self.root = Path(nested) / "taf-scenario.Control"
                self.seal, self.local = Path(str(self.root) + ".seal"), self.root / "Local"
                self.fixture(phase)
                result = check.verify(self.root, phase)
                self.assertEqual("PASS", result["verdict"])
                self.assertEqual("unfounded-save 8.22@40,12", result["command"])
                self.assertEqual("#4242", result["seed"])
                self.assertEqual(GAME, result["gameId"])
                self.assertEqual(LIFECYCLE, result["lifecycleSHA256"])
                for key in ("ordinaryAcceptance", "releaseAcceptance", "processAuthority",
                            "historicalSaveCompatibility"):
                    self.assertIs(False, result[key])
                if phase == "save":
                    self.assertEqual(self.imported, result["saveHashes"]["Primary.sav.gz"])
                else:
                    self.assertEqual(self.imported, result["importedSaveHashes"]["Primary.sav.gz"])
                    self.assertEqual(self.imported, result["backupSaveHash"])
                    self.assertNotEqual(self.imported, result["secondSaveHashes"]["Primary.sav.gz"])
                    self.assertIs(True, result["foundedAfterLoad"])

    def test_another_start_is_its_own_command(self):
        self.fixture("save", start="14.18@3,7")
        self.assertEqual("unfounded-save 14.18@3,7", check.verify(self.root, "save")["command"])

    def test_script_request_start_and_phase_mismatches_refuse(self):
        mutations = (
            ("script", lambda: self.write(self.local / "scenario-script.txt", "stagedigest\nunfounded-save\n")),
            ("request", lambda: self.write(self.seal / "request.txt", "arch-gallery-slice;seed=#4242\n")),
            ("zone", lambda: self.write(self.snapshot, snapshot(zone="JoppaWorld.9.22.1.1.10"))),
            ("load request in save", lambda: self.write(self.local / "scenario-load.txt", "x\n")),
        )
        for name, mutate in mutations:
            with self.subTest(name=name):
                self.fixture("save")
                mutate()
                if name == "zone":
                    rows = self.receipt.read_text().split("\n")
                    rows[4] = hashlib.sha256(self.snapshot.read_bytes()).hexdigest()
                    self.write(self.receipt, "\n".join(rows))
                self.reseal()
                self.refused("save")

    def test_receipt_snapshot_and_save_artifacts_are_exact(self):
        mutations = (
            lambda: self.write(self.snapshot, snapshot(frame="10")),
            lambda: self.write(self.snapshot, snapshot(game=OTHER)),
            lambda: self.write(self.snapshot, snapshot(lifecycle="A" * 64)),
            lambda: self.write(self.save / "Primary.sav.gz", b"\x1f\x8bchanged"),
            lambda: self.write(self.save / "Primary.sav.gz.bak", b"extra"),
            lambda: (self.save / "Cache.db").unlink(),
            lambda: self.write(self.receipt, self.receipt.read_text().replace(GAME, OTHER)),
            lambda: self.write(self.root / "Synced/Saves" / OTHER / "Primary.json", b"{}"),
        )
        for index, mutate in enumerate(mutations):
            with self.subTest(index=index):
                self.fixture("save")
                mutate()
                self.refused("save")

    def test_load_requires_the_imported_backup_and_a_replaced_primary(self):
        for name, mutate in (("backup", lambda: self.write(self.save / "Primary.sav.gz.bak", b"\x1f\x8bother")),
                             ("unchanged", lambda: self.write(self.save / "Primary.sav.gz",
                                                              b"\x1f\x8bchecker-unfounded-save")),
                             ("no backup", lambda: (self.save / "Primary.sav.gz.bak").unlink()),
                             ("not gzip", lambda: self.write(self.save / "Primary.sav.gz", b"plain"))):
            with self.subTest(name=name):
                self.fixture("load")
                mutate()
                self.refused("load")

    def test_save_journal_is_strict_in_both_directions(self):
        base = save_rows()
        variants = [base[:2] + base[3:], base + [base[-1]], [base[0], base[2], base[1]] + base[3:],
                    base[:2] + [(base[2][0], "REFUSED", base[2][2])] + base[3:],
                    base[:2] + [(base[2][0], "OK", base[2][2].replace("dormant-frame=5", "dormant-frame=10"))] + base[3:],
                    base[:2] + [(base[2][0], "OK", base[2][2].replace(GAME, OTHER))] + base[3:],
                    base[:2] + [(base[2][0], "OK", base[2][2].replace(LIFECYCLE, "c" * 64))] + base[3:],
                    base[:1] + [("stagedigest", "OK", "founded=true")] + base[2:]]
        for index, rows in enumerate(variants):
            with self.subTest(index=index):
                self.fixture("save")
                self.write(self.root / "scenario-journal.tsv", journal(rows))
                self.refused("save")

    def test_load_journal_rows_and_claims_are_exact(self):
        base = load_rows()
        variants = [base[1:], base[:-1], base + [base[-1]], [base[0], base[2], base[1]] + base[3:],
                    base[:3] + [(base[3][0], "REFUSED", base[3][2])] + base[4:],
                    [("LOAD-BEGIN", "OK", base[0][2].replace(GAME, OTHER))] + base[1:],
                    base[:3] + [(base[3][0], "OK", base[3][2].replace("growth-authority=true", "growth-authority=false"))] + base[4:],
                    base[:4] + [(base[4][0], "OK", base[4][2].replace("backup-is-imported-save=true", "x"))] + base[5:],
                    base[:2] + [(base[2][0], "OK", base[2][2].replace(LIFECYCLE, "c" * 64))] + base[3:],
                    base[:1] + [(base[1][0], "OK", base[1][2].replace("before-AfterGameLoaded=true", "late"))] + base[2:]]
        for index, rows in enumerate(variants):
            with self.subTest(index=index):
                self.fixture("load")
                self.write(self.root / "scenario-journal.tsv", journal(rows))
                self.refused("load")

    def test_player_log_rejects_the_272_text_and_any_taf_exception(self):
        for line in ("growth envelope is not bounded and writable (identity-unbound)\n",
                     "exception serializing object ThousandAndFirst.KingdomLifecycleBook of KingdomLifecycleBook\n",
                     "[TAF] exception while saving\n"):
            with self.subTest(line=line):
                self.fixture("save")
                self.write(self.root / "Player.log", "[TAF] loaded synthetic checker fixture only\n" + line)
                self.refused("save")

    def test_local_must_still_equal_its_closed_seal(self):
        self.fixture("load")
        self.write(self.local / "late.txt", "unsealed")
        self.refused("load", "closed seal")

    def test_cli_reports_refusals_as_json(self):
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            self.assertEqual(1, check.main(["check-unfounded-results.py", str(self.root)]))
        self.assertEqual("REFUSED", json.loads(stdout.getvalue())["verdict"])
        self.fixture("save")
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            self.assertEqual(0, check.main(["check-unfounded-results.py", str(self.root), "--phase", "save"]))
        self.assertEqual("PASS", json.loads(stdout.getvalue())["verdict"])

    def test_harness_emits_every_claim_the_checker_binds(self):
        save = (TOOLS.parent / "Harness/KingdomUnfoundedSave.cs").read_text(encoding="utf-8")
        load = (TOOLS.parent / "Harness/KingdomUnfoundedLoad.cs").read_text(encoding="utf-8")
        entry = (TOOLS.parent / "Harness/KingdomScenarioLoadEntry.cs").read_text(encoding="utf-8")
        snapshot_source = (TOOLS.parent / "Harness/KingdomUnfoundedSaveSnapshot.cs").read_text(encoding="utf-8")
        self.assertIn('"' + check.SNAPSHOT_PREFIX + '"', snapshot_source)
        for token in ("real-save=true dormant-frame=5; founded=false;", "; save-error=false;",
                      '; game-id="', '"; lifecycle-sha256="'):
            self.assertIn(token, save)
        for row in check.LOAD_ROWS[1:-1]:
            self.assertIn('"' + row + '"', load)
        for claim in ("before-AfterGameLoaded=true; ", "cold-load=true; after-activation=true; ",
                      "founded=false; load-failed=false; pristine-lifecycle-and-carry=true; lifecycle-frame=",
                      "production-founding=true; ", "founded=true; lifecycle-frame=", "; growth-authority=true",
                      "real-save=true; ", "; save-error=false", "; backup-is-imported-save=true; primary-changed=true",
                      "; second-save-reload=false", "native-unfounded cold-load session complete; real-save-quit-load=true",
                      "; production-founding=true; second-real-save=true; new-game-script-replayed=false"):
            self.assertIn(claim, load)
        self.assertIn('"exact sealed save; game-id=" + Request.GameId + "; new-game=false; mod-restore=false"', entry)


if __name__ == "__main__":
    unittest.main()
