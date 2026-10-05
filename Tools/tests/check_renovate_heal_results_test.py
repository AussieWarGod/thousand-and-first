"""Synthetic checker fixtures only: not a native heal, cold load or ordinary acceptance (#283)."""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
SPEC = importlib.util.spec_from_file_location("renovate_heal_results", TOOLS / "check-renovate-heal-results.py")
check = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(check)
GAME = "01234567-89ab-cdef-0123-456789abcdef"
JOB = "0123456789abcdef0123456789abcdef"
STAMP = "2026-10-05T12:34:56.123Z"
STALL = ("an authored work has incomplete or changed frozen evidence: settled layout slot g:00:00 is "
         "absent, moved, duplicated, or changed")
TAG = "MODERROR [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]] - ThousandAndFirst: seal "
MARKS = "Founder marks did not settle exactly on the successor."


def report(stage, reason=STALL):
    return TAG + stage + " failed closed: System.InvalidOperationException: " + reason


def snapshot(game=GAME, defect="A"):
    return ("taf-renovate-heal-v1:" + ";".join((game, "JoppaWorld.8.22.1.1.10", JOB, "3001", "3002",
                                               defect, "5", "3003", "411600"))).encode()


def journal(rows):
    return "".join(STAMP + "\t" + verb + "\t" + outcome + "\t" + message + "\n"
                   for verb, outcome, message in rows).encode()


class RenovateHealResultsTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-heal-checker-tests.")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "taf-scenario.HealChecker"
        self.seal = Path(str(self.root) + ".seal")
        self.local = self.root / "Local"

    def write(self, path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw if isinstance(raw, bytes) else raw.encode())

    def reseal(self):
        with contextlib.redirect_stdout(io.StringIO()):
            check.profile.seal(str(self.local), str(self.seal / "profile.sha256"))

    def save_rows(self, after=None, saved=None):
        after = after or ("native-tier-upgrade after-wait; tick=414000; job=" + JOB + "; phase=InspectionRequired"
                          "; physical=None; failure=" + MARKS + "; updated=411600; predecessor=3001"
                          "; upgrade-phase=5; predecessor-yielding=1; successor=3002; successor-yielding=0"
                          "; seal-captured=False; seal-spatial=Malformed; seal-fault=True"
                          "; seal-observation-unchanged=True; seal-reason=" + STALL)
        saved = saved or ("renovate-heal-save=true; defect=A; job=" + JOB + "; predecessor=3001; successor=3002"
                          "; upgrade-phase=5; scaffold-intent=3003; save=" + GAME + "; time-ticks=414000"
                          "; primary-sha256=" + self.imported + "; snapshot-sha256=" + self.snap_hash)
        return [("AUTOSTART", "OK", "sealed"), ("stagedigest", "OK", "founded=false; digest"),
                ("tier-upgrade-setup", "OK", "native-tier-upgrade phase=1; synthetic"),
                ("advance", "OK", "Advancing 3600"), ("advance-complete", "OK", "3600 turn(s) elapsed of 3600 requested"),
                ("tier-upgrade-check", "OK", "native-tier-upgrade phase=2; synthetic"),
                ("tier-upgrade-short", "OK", "native-tier-upgrade phase=3; synthetic"),
                ("advance", "OK", "Advancing 2400"), ("tier-upgrade-check", "OK", "native-tier-upgrade phase=4; x"),
                ("advance", "OK", "Advancing 3600"), ("tier-upgrade-after-wait", "OK", after),
                ("renovate-heal-save", "OK", saved), ("SCRIPT-COMPLETE", "OK", "script complete")]

    def load_rows(self, after=None, resaved=None, waited="2401 turn(s) elapsed of 2400 requested"):
        after = after or ("native-renovate-heal after; tick=416400; job=" + JOB + "; phase=Complete"
                          "; physical=EffectsSettled; failure=none; updated=415200; predecessor=absent"
                          "; intent=absent; successor=3002; successor-yielding=1; layout-fault=none"
                          "; next-layer=3; pending=0; scaffold-intent=3003; seal-captured=False"
                          "; seal-spatial=Pending; seal-fault=False; seal-observation-unchanged=True; seal-reason=x")
        resaved = resaved or ("real-save=true; healed=true; defect=A; save-error=false; backup-is-imported-save=true"
                              "; primary-sha256=" + self.second + "; ordinary-acceptance=false")
        return [("LOAD-BEGIN", "OK", "exact sealed save; game-id=" + GAME + "; new-game=false; mod-restore=false"),
                ("renovate-heal-preactivation", "OK", "before-AfterGameLoaded=true; stuck=true; defect=A; job="
                 + JOB + "; predecessor=3001"),
                ("renovate-heal-resume", "OK", "vanilla-Continue=true; requested-turns=2400"),
                ("advance-progress", "OK", "100"), ("advance-complete", "OK", waited),
                ("renovate-heal-after", "OK", after), ("renovate-heal-resaved", "OK", resaved),
                ("SCRIPT-COMPLETE", "OK", "native-renovate-heal cold-load complete; real-save-quit-load=true"
                 "; readmitted-handover-complete=true; second-real-save=true")]

    SAVE_LOG = ["[TAF] improvement begun: tent -> tentrow cost=2 ticks=900 at 29,9",
                "[TAF] seal: settlement pass was not staged (" + STALL + ")", report("daily stage"),
                "[TAF] seal: daily stage failed closed (" + STALL + ")", report("BeforeSave stage")]
    LOAD_LOG = ["[TAF] survey: zone=JoppaWorld.8.22.1.1.10", report("loaded stage reconciliation"),
                "[TAF] seal: loaded stage reconciliation failed closed (" + STALL + ")",
                "[TAF] improvement readmitted: job=" + JOB + " defect=A design=tent->tentrow at 29,9",
                "[TAF] improvement handover: settler's tent -> r_KingdomTentRow liquid=0 items=0"]

    def fixture(self, phase, rows=None, log=None, runtime=None, defect="A"):
        request = "founding-first-city;seed=#922453088"
        self.write(self.local / "scenario-script.txt", "\n".join(check.SCRIPT) + "\n")
        self.write(self.seal / "request.txt", request + "\n")
        self.write(self.local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml",
                   '<stringgamestate Name="r_TAF_ScenarioRequest_v1" Value="' + request + '" />\n'
                   '<location ID="TAFTestGround" Name="[Dev] TAF" Location="GlobalLocation:'
                   'JoppaWorld.8.22.1.1.10@40,12" />\n')
        save = self.root / "Synced/Saves" / GAME
        imported, info, snap = b"\x1f\x8bstuck", b'{"stuck":true}', snapshot(defect=defect)
        self.imported = hashlib.sha256(imported).hexdigest()
        self.snap_hash = hashlib.sha256(snap).hexdigest()
        if phase == "save":
            for name, raw in (("Primary.sav.gz", imported), ("Primary.json", info), ("Cache.db", b"cache")):
                self.write(save / name, raw)
            receipt, snap_path = self.root / "scenario-save-receipt.txt", self.root / "scenario-save-snapshot.txt"
            lines = ["taf-scenario-save-v1", GAME, self.imported, hashlib.sha256(info).hexdigest(), self.snap_hash]
            rows = rows or self.save_rows()
        else:
            healed = b"\x1f\x8bhealed"
            self.second = hashlib.sha256(healed).hexdigest()
            for name, raw in (("Primary.sav.gz", healed), ("Primary.json", b'{"healed":true}'),
                              ("Primary.sav.gz.bak", imported), ("Cache.db", b"cache-after")):
                self.write(save / name, raw)
            receipt, snap_path = self.local / "scenario-load.txt", self.local / "scenario-load-snapshot.txt"
            lines = ["taf-scenario-load-v1", GAME, self.imported, hashlib.sha256(info).hexdigest(),
                     hashlib.sha256(b"cache").hexdigest(), self.snap_hash]
            rows = rows or self.load_rows()
            evidence = runtime if runtime is not None else dict(crossBuild=True, harnessIdentical=True,
                                                                runtimeDelta=check.FIX_KEYS)
            self.write(self.root / "load-runtime-evidence.json", json.dumps(evidence))
        self.write(receipt, "\n".join(lines) + "\n")
        self.write(snap_path, snap)
        self.write(self.root / "scenario-journal.tsv", journal(rows if not callable(rows) else rows()))
        self.write(self.root / "Player.log", "\r\n".join(log or (self.SAVE_LOG if phase == "save"
                                                                 else self.LOAD_LOG)) + "\r\n")
        self.reseal()

    def refused(self, phase, pattern):
        with self.assertRaisesRegex((ValueError, SystemExit, KeyError, IndexError), pattern):
            check.verify(self.root, phase)

    def test_save_phase_binds_the_stall_and_retains_its_seal_reports(self):
        self.fixture("save")
        result = check.verify(self.root, "save")
        self.assertEqual(("PASS", "save", "A", "renovate-heal 8.22@40,12", "#922453088"),
                         (result["verdict"], result["phase"], result["defect"], result["command"], result["seed"]))
        self.assertEqual((STALL, 2), (result["stallSealReason"], result["stallSealReports"]))
        for key in ("ordinaryAcceptance", "releaseAcceptance", "processAuthority"):
            self.assertIs(False, result[key])

    def test_load_phase_heals_on_the_fixed_runtime_and_is_strict_after_the_readmission(self):
        self.fixture("load")
        result = check.verify(self.root, "load")
        self.assertEqual(("PASS", "A", [STALL]), (result["verdict"], result["readmittedDefect"],
                                                  result["preHealSealReasons"]))
        self.assertIs(True, result["healedAfterLoad"])
        self.assertIs(True, result["crossBuild"])
        self.assertEqual(self.imported, result["backupSaveHash"])
        self.assertNotEqual(self.imported, result["secondSaveHashes"]["Primary.sav.gz"])

    def test_session_one_without_the_stall_cannot_pass(self):
        cases = [(self.SAVE_LOG + ["[TAF] improvement handover: settler's tent -> r_KingdomTentRow"], "handed over"),
                 (self.SAVE_LOG[:2], "report of exactly"),
                 (self.SAVE_LOG + [TAG + "daily stage failed closed: System.InvalidOperationException: other"],
                  "strict Player.log"),
                 (self.SAVE_LOG + ["MODWARN [The Thousand and First] - compiler warning"], "strict Player.log"),
                 ([line for line in self.SAVE_LOG if "begun" not in line], "never began")]
        for log, pattern in cases:
            with self.subTest(pattern=pattern):
                self.fixture("save", log=log)
                self.refused("save", pattern)

    def test_session_one_rows_must_show_the_stuck_renovation(self):
        self.fixture("save")
        good = self.save_rows()
        for index, swap, pattern in ((10, ("phase=InspectionRequired", "phase=Complete"), "after-wait row"),
                                     (10, ("seal-fault=True", "seal-fault=False"), "after-wait row"),
                                     (11, ("; primary-sha256=", "; primary="), "heal save row")):
            with self.subTest(pattern=pattern):
                rows = list(good)
                verb, outcome, message = rows[index]
                rows[index] = (verb, outcome, message.replace(*swap))
                self.fixture("save", rows=rows)
                self.refused("save", pattern)

    def test_session_two_log_is_strict_after_and_bounded_before_the_readmission(self):
        cases = [([line for line in self.LOAD_LOG if "readmitted" not in line], "exactly one readmission"),
                 (self.LOAD_LOG + [self.LOAD_LOG[3]], "exactly one readmission"),
                 ([line.replace(JOB, "f" * 32) for line in self.LOAD_LOG], "exactly one readmission"),
                 ([line.replace("defect=A", "defect=B") for line in self.LOAD_LOG], "another defect"),
                 (self.LOAD_LOG[:4], "did not complete"),
                 (self.LOAD_LOG + [report("daily stage")], "still refused"),
                 (self.LOAD_LOG + ["[TAF] seal: settlement pass was not staged (" + STALL + ")"], "still refused"),
                 ([self.LOAD_LOG[0], "MODERROR [The Thousand and First] - ThousandAndFirst: boom"]
                  + self.LOAD_LOG[1:], "before the heal"),
                 ([self.LOAD_LOG[0], report("loaded stage reconciliation", "a different reason")]
                  + self.LOAD_LOG[2:], "before the heal")]
        for log, pattern in cases:
            with self.subTest(pattern=pattern):
                self.fixture("load", log=log)
                self.refused("load", pattern)

    def test_session_two_must_run_the_fixed_runtime_and_finish_the_handover(self):
        cases = [(dict(crossBuild=True, harnessIdentical=True, runtimeDelta=check.FIX_KEYS[1:]), None, "minus exactly"),
                 (dict(crossBuild=False, harnessIdentical=True, runtimeDelta=check.FIX_KEYS), None, "minus exactly"),
                 (None, lambda: self.load_rows(after="native-renovate-heal after; job=" + JOB + "; phase=InspectionRequired"),
                  "finished handover"),
                 (None, lambda: self.load_rows(waited="1200 turn(s) elapsed of 1200 requested"), "2400 turns"),
                 (None, lambda: self.load_rows(resaved="real-save=true; healed=true; defect=A; primary-sha256=x;"),
                  "second save row")]
        for runtime, rows, pattern in cases:
            with self.subTest(pattern=pattern):
                self.fixture("load", rows=rows, runtime=runtime)
                self.refused("load", pattern)

    def test_the_restated_predicate_agrees_with_the_strict_shell_checker(self):
        samples = [report("daily stage"), "[TAF] seal: daily stage failed closed (" + STALL + ")",
                   "[TAF] improvement readmitted: job=" + JOB + " defect=A design=tent->tentrow at 29,9",
                   "MODWARN [The Thousand and First] - obsolete", "[TAF] an exception escaped",
                   "  at ThousandAndFirst.KingdomUpgrade.HandOver ()", "[TAF] quarantined the receipt",
                   "MODERROR [Pets of Harvest Dawn] - unrelated", "[TAF] survey: zone=x"]
        for line in samples:
            with self.subTest(line=line), tempfile.TemporaryDirectory() as scratch:
                path = Path(scratch) / "Player.log"
                path.write_text("[TAF] evidence\n" + line + "\n", encoding="utf-8")
                shell = subprocess.run(["bash", str(TOOLS / "check-player-log.sh"), str(path)],
                                       env=dict(os.environ, TAF_LOG_ALLOW=""), capture_output=True)
                self.assertEqual(shell.returncode != 0, check.flagged(line))


if __name__ == "__main__":
    unittest.main()
