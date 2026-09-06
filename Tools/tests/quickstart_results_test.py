"""Synthetic checker fixtures only: not native boot, save/load or ordinary acceptance."""
import base64
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("quickstart_results", TOOLS / "check-quickstart-results.py")
check = importlib.util.module_from_spec(SPEC)
sys.path.insert(0, str(TOOLS))
try:
    SPEC.loader.exec_module(check)
finally:
    sys.path.pop(0)
GAME = "01234567-89ab-cdef-0123-456789abcdef"
OTHER = "11234567-89ab-cdef-0123-456789abcdef"


def snapshot(selected="marsh", advisor="yes", game=GAME, seed="#4242"):
    def text(value):
        if value is None:
            return struct.pack("<i", -1)
        raw = value.encode("utf-8")
        return struct.pack("<i", len(raw)) + raw
    raw = struct.pack("<ii", 0x31535154, 1) + text(game) + text(seed) + text(selected)
    raw += struct.pack("<Bi", int(advisor == "yes"), 42) + text(None) + struct.pack("<qqqq", 0, 1, 2, 3)
    # Opaque nested strings intentionally do not claim native receipt semantics.
    raw += b"".join(text(v) for v in ("checker-only-receipt", "checker-only-heart", "hs1-" + "a" * 64,
                                     None, "checker-only-reservations"))
    return b"taf-quickstart-save-v1:" + base64.b64encode(raw)


def journal(phase="boot", selected="marsh", advisor="yes", game=GAME, seed="#4242"):
    command = ("quickstart-boot" if phase == "boot" else "quickstart-save") + " " + selected + " " + advisor
    rows = [
        ("QUICKSTART-BOOT-BEGIN", command + "; seed=" + seed + "; genuine-production-boot=true"),
        ("QUICKSTART-BOOT-OBSERVED", "actual founded heart, finite single grants and advisor verified after GAMESTARTING"),
        ("QUICKSTART-BOOT-COMPLETE", command + "; boot-only=true; save-load=false; ordinary-acceptance=false"),
    ]
    if phase == "save":
        rows += [("QUICKSTART-SAVE-BEGIN", "genuine-world=true; game-id=" + game + "; synthetic-state=false"),
                 ("QUICKSTART-SAVE-COMPLETE", "real-save=true; exact-owner-and-stock=true; no-bootstrap-replay=true; cold-load=false; ordinary-acceptance=false")]
    elif phase == "load":
        rows = [("LOAD-BEGIN", "exact sealed save; game-id=" + game + "; new-game=false; mod-restore=false"),
                ("QUICKSTART-LOAD-PREACTIVATION", "exact-saved-heart-stock-and-IDs=true; before-player-GameRestored-and-activation=true"),
                ("QUICKSTART-LOAD-COMPLETE", "real-save-owned-stop-cold-load=true; graceful-quit=false; unchanged-heart-stock-and-IDs=true; bootstrap-replay=false; ordinary-acceptance=false")]
    return "".join("2026-09-07T12:34:56.123Z\t" + verb + "\tOK\t" + message + "\n" for verb, message in rows).encode()


class QuickstartResultsTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="taf-quickstart-checker-tests.")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "taf-scenario.CheckerTests"
        self.root.mkdir()
        self.seal = Path(str(self.root) + ".seal")
        self.seal.mkdir()
        self.local = self.root / "Local"
        self.local.mkdir()

    def write(self, path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw if isinstance(raw, bytes) else raw.encode())

    def reseal(self):
        with contextlib.redirect_stdout(io.StringIO()):
            check.profile.seal(str(self.local), str(self.seal / "profile.sha256"))

    def fixture(self, phase="boot", selected="marsh", advisor="yes"):
        command = ("quickstart-boot" if phase == "boot" else "quickstart-save") + " " + selected + " " + advisor
        self.write(self.local / "scenario-script.txt", "# Synthetic checker test; not native proof\n" + command + "\n")
        self.write(self.local / "PlayerOptions.json", json.dumps({"r_TAF_OptionQuickstartAdvisor": "Yes" if advisor == "yes" else "No"}))
        request = "founding-first-city;seed=#4242"
        self.write(self.seal / "request.txt", request + "\n")
        self.write(self.local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml",
                   '<state Name="r_TAF_ScenarioRequest_v1" Value="' + request + '" />\n')
        if phase != "boot":
            self.save = self.root / "Synced/Saves" / GAME
            for name, raw in (("Primary.sav.gz", b"\x1f\x8bchecker-not-a-real-save"),
                              ("Primary.json", b'{"checkerTest":true}'), ("Cache.db", b"checker-not-a-real-cache")):
                self.write(self.save / name, raw)
            hashes = [hashlib.sha256((self.save / name).read_bytes()).hexdigest() for name in check.files.SAVE_FILES]
            snap = snapshot(selected, advisor)
            if phase == "load":
                self.receipt = self.local / "scenario-load.txt"
                self.snapshot = self.local / "scenario-load-snapshot.txt"
                rows = ["taf-scenario-load-v1", GAME, *hashes, hashlib.sha256(snap).hexdigest()]
            else:
                self.receipt = self.root / "scenario-save-receipt.txt"
                self.snapshot = self.root / "scenario-save-snapshot.txt"
                rows = ["taf-scenario-save-v1", GAME, *hashes[:2], hashlib.sha256(snap).hexdigest()]
            self.write(self.receipt, "\n".join(rows) + "\n")
            self.write(self.snapshot, snap)
        self.write(self.root / "scenario-journal.tsv", journal(phase, selected, advisor))
        self.write(self.root / "Player.log", "[TAF] loaded synthetic checker fixture only\n")
        self.reseal()

    def refused(self, phase="boot"):
        with self.assertRaises((ValueError, OSError, SystemExit)):
            check.verify(self.root, phase)

    def test_six_selections_all_three_phases_and_explicit_limits(self):
        for phase in ("boot", "save", "load"):
            for selected in ("marsh", "canyon", "dunes"):
                for advisor in ("yes", "no"):
                    with self.subTest(phase=phase, selected=selected, advisor=advisor):
                        with tempfile.TemporaryDirectory(dir=self.temp.name) as nested:
                            prior = self.root, self.local, self.seal
                            self.root = Path(nested) / "taf-scenario.Control"
                            self.local, self.seal = self.root / "Local", Path(str(self.root) + ".seal")
                            self.fixture(phase, selected, advisor)
                            result = check.verify(self.root, phase)
                            self.assertEqual("PASS", result["verdict"])
                            for key in ("ordinaryAcceptance", "releaseAcceptance", "processAuthority", "historicalSaveCompatibility"):
                                self.assertIs(False, result[key])
                            self.assertEqual(phase == "load", result["cacheReceiptBound"])
                            self.root, self.local, self.seal = prior

    def test_every_phase_missing_duplicate_reordered_and_trailing_refused(self):
        for phase in ("boot", "save", "load"):
            raw = journal(phase); rows = raw.splitlines(keepends=True)
            variants = [b"".join(rows[:i] + rows[i + 1:]) for i in range(len(rows))]
            variants += [b"".join(rows[:i] + [rows[i]] + rows[i:]) for i in range(len(rows))]
            variants += [b"".join([rows[1], rows[0], *rows[2:]]), raw + rows[-1].replace(b"\tOK\t", b"\tREFUSED\t")]
            variants += [raw.replace(b"\tOK\t", b"\tREFUSED\t", 1), raw + rows[-1].replace(b"COMPLETE", b"EXTRA")]
            for bad in variants:
                with self.subTest(phase=phase, bad=bad[:80]), self.assertRaises(ValueError):
                    check.journal(bad, phase, "quickstart-" + ("boot" if phase == "boot" else "save") + " marsh yes", "#4242", GAME)

    def test_every_boolean_and_matched_seed_script_game_id_are_exact(self):
        for phase in ("boot", "save", "load"):
            raw = journal(phase)
            fragments = set(raw.decode().split("; "))
            variants = [raw.replace(b"=true", b"=false", 1), raw.replace(b"=false", b"=true", 1),
                        raw.replace(b"marsh yes", b"dunes no"), raw.replace(b"#4242", b"#4243"), raw.replace(GAME.encode(), OTHER.encode())]
            for fragment in fragments:
                if "=true" in fragment or "=false" in fragment:
                    variants.append(raw.replace(fragment.encode(), fragment.replace("=true", "=WRONG").replace("=false", "=WRONG").encode()))
            for bad in variants:
                if bad == raw:
                    continue
                with self.subTest(phase=phase), self.assertRaises(ValueError):
                    check.journal(bad, phase, "quickstart-" + ("boot" if phase == "boot" else "save") + " marsh yes", "#4242", GAME)

    def test_journal_strict_columns_dates_escapes_controls_and_line_endings(self):
        raw = journal()
        check.journal(raw.replace(b"\n", b"\r\n"), "boot", "quickstart-boot marsh yes", "#4242", None)
        for bad in (raw[:-1], raw + b"\n", raw.replace(b"\tOK\t", b"\tOK\t\t"), raw.replace(b"2026-09", b"2026-19"),
                    raw.replace(b".123Z", b"Z"), raw.replace(b"genuine", b"\\qgenuine"), raw.replace(b"genuine", b"\x00genuine"),
                    raw.replace(b"BEGIN", b"begin"), raw.replace(b".123Z", b".124Z", 1), raw + b"\xff"):
            with self.subTest(bad=bad[:50]), self.assertRaises(ValueError):
                check.journal(bad, "boot", "quickstart-boot marsh yes", "#4242", None)

    def test_script_options_malformed_duplicate_json_and_mixed_phase_refuse(self):
        self.fixture()
        for script in ("quickstart-save marsh yes\n", "quickstart-boot marsh yes\nstatus\n", "quickstart-boot  marsh yes\n", "quickstart-boot marsh maybe\n"):
            self.write(self.local / "scenario-script.txt", script); self.reseal(); self.refused()
        self.fixture()
        for options in ('{"r_TAF_OptionQuickstartAdvisor":"No"}', '{"r_TAF_OptionQuickstartAdvisor":"Yes","r_TAF_OptionQuickstartAdvisor":"Yes"}', '[]'):
            self.write(self.local / "PlayerOptions.json", options); self.reseal(); self.refused()

    def test_closed_seal_extra_modified_and_seed_descriptor_mismatch_refuse(self):
        self.fixture(); self.write(self.local / "foreign.cs", "unexpected"); self.refused()
        self.reseal(); self.write(self.local / "foreign.cs", "changed"); self.refused()
        self.reseal(); self.write(self.seal / "request.txt", "founding-first-city;seed=#4243\n"); self.refused()

    def test_reopened_seal_parser_must_match_captured_bytes(self):
        self.fixture()
        rows = check.profile.read_seal(str(self.seal / "profile.sha256"))
        rows[next(iter(rows))] = "f" * 64
        with mock.patch.object(check.profile, "read_seal", return_value=rows):
            self.refused()

    def test_all_persona_bookkeeping_is_not_silently_dropped(self):
        for verb in ("TESTGROUND-RESTRIP", "RUNNER-ARMED", "SCRIPT-BEGIN", "AUTOSTART"):
            extra = ("2026-09-07T12:34:56.123Z\t" + verb + "\tOK\tunexpected\n").encode()
            for raw in (extra + journal(), journal() + extra):
                with self.subTest(verb=verb), self.assertRaises(ValueError):
                    check.journal(raw, "boot", "quickstart-boot marsh yes", "#4242", None)

    def test_only_exact_single_leading_autostart_is_optional_for_boot_and_save(self):
        prefix = b"2026-09-07T12:34:56.123Z\tAUTOSTART\tOK\tsealed script present; test game started without input\n"
        for phase in ("boot", "save", "load"):
            command = "quickstart-" + ("boot" if phase == "boot" else "save") + " marsh yes"
            raw = journal(phase)
            self.assertFalse(check.journal(raw, phase, command, "#4242", GAME))
            if phase != "load":
                self.assertTrue(check.journal(prefix + raw, phase, command, "#4242", GAME))
            variants = [prefix + prefix + raw, raw + prefix, prefix.replace(b"\tOK\t", b"\tREFUSED\t") + raw,
                        prefix.replace(b"without input", b"with input") + raw]
            if phase == "load":
                variants.append(prefix + raw)
            for bad in variants:
                with self.subTest(phase=phase), self.assertRaises(ValueError):
                    check.journal(bad, phase, command, "#4242", GAME)

    def test_snapshot_hash_identity_and_framing_refuse(self):
        self.fixture("save")
        for raw in (snapshot(game=OTHER), snapshot(seed="#4243"), snapshot(selected="dunes"), snapshot(advisor="no"),
                    snapshot() + b"\n", snapshot().replace(b"v1:", b"v2:"), b"bad"):
            self.write(self.snapshot, raw)
            self.refused("save")
            lines = self.receipt.read_text().splitlines(); lines[-1] = hashlib.sha256(raw).hexdigest()
            self.write(self.receipt, "\n".join(lines) + "\n"); self.refused("save")

    def test_receipt_hash_missing_extra_and_changed_save_files_refuse(self):
        self.fixture("save")
        original = self.receipt.read_bytes()
        for bad in (original[:-1], original + b"\n", original.replace(b"save-v1", b"save-v2"), original.replace(GAME.encode(), OTHER.encode())):
            self.write(self.receipt, bad); self.refused("save")
        self.write(self.receipt, original)
        self.write(self.save / "Primary.sav.gz", "changed"); self.refused("save")
        self.fixture("save"); (self.save / "Primary.json").unlink(); self.refused("save")
        self.fixture("save"); self.write(self.save / "Cache.db", b""); self.refused("save")
        self.fixture("save"); self.write(self.save / "extra", "not allowed"); self.refused("save")

    def test_load_cache_and_sealed_request_snapshot_hashes_refuse(self):
        self.fixture("load"); self.write(self.save / "Cache.db", "changed"); self.refused("load")
        self.fixture("load"); self.write(self.snapshot, snapshot(game=OTHER)); self.reseal(); self.refused("load")
        self.fixture("load"); self.write(self.receipt, self.receipt.read_bytes().replace(GAME.encode(), OTHER.encode())); self.reseal(); self.refused("load")

    def test_actual_log_checker_ignores_ambient_allow_and_never_filters_raw(self):
        self.fixture()
        with mock.patch.dict(os.environ, {"TAF_LOG_ALLOW": ".*"}):
            for raw in (b"", b"foreign-only log\n", b"[TAF] loaded\nMODWARN [The Thousand and First [ALPHA] [DEV SCENARIO HARNESS]] CS0114 warning\n",
                        b"[TAF] loaded\n  at ThousandAndFirst.UnknownMethod()\n"):
                self.write(self.root / "Player.log", raw); self.refused()
        self.assertEqual(raw, (self.root / "Player.log").read_bytes())

    def test_links_and_empty_evidence_refuse(self):
        self.fixture(); path = self.root / "Player.log"; raw = path.read_bytes(); path.unlink()
        target = Path(self.temp.name) / "foreign.log"; target.write_bytes(raw); path.symlink_to(target); self.refused()
        path.unlink(); os.link(target, path); self.refused()
        path.unlink(); path.write_bytes(raw)
        self.write(self.root / "scenario-journal.tsv", b""); self.refused()

    def test_late_log_change_refuses_and_success_preserves_files(self):
        self.fixture()
        before = {p: p.read_bytes() for tree in (self.root, self.seal) for p in tree.rglob("*") if p.is_file()}
        check.verify(self.root, "boot")
        self.assertEqual(before, {p: p.read_bytes() for p in before})
        original = check.verify_log
        def change(raw):
            original(raw)
            self.write(self.root / "Player.log", raw + b"late change\n")
        with mock.patch.object(check, "verify_log", side_effect=change):
            self.refused()

    def test_late_journal_failure_and_byte_identical_touch_refuse(self):
        self.fixture()
        path = self.root / "scenario-journal.tsv"
        original = check.verify_log
        for touch_only in (False, True):
            self.write(path, journal())
            def change(raw):
                original(raw)
                if touch_only:
                    status = path.stat()
                    os.utime(path, ns=(status.st_atime_ns, status.st_mtime_ns + 1000000))
                else:
                    self.write(path, journal() + journal().splitlines(keepends=True)[-1].replace(b"\tOK\t", b"\tREFUSED\t"))
            with mock.patch.object(check, "verify_log", side_effect=change):
                self.refused()

    def test_cli_is_json_scoped_and_invalid_invocation_never_passes(self):
        self.fixture()
        result = subprocess.run([sys.executable, str(TOOLS / "check-quickstart-results.py"), str(self.root), "--phase", "boot"],
                                capture_output=True, text=True, check=False)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual("PASS", json.loads(result.stdout)["verdict"])
        for args in ([], [str(self.root)], [str(self.root), "--phase", "other"], ["/", "--phase", "boot"]):
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                self.assertEqual(2, check.main(["checker", *args]))
            self.assertEqual("REFUSED", json.loads(output.getvalue())["verdict"])


if __name__ == "__main__":
    unittest.main()
