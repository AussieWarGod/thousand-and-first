"""Native-verdict grammar/chain pins; synthetic bytes are never native acceptance."""
from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
import unittest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
try:
    spec = importlib.util.spec_from_file_location("taf_upgrade_verdict", TOOLS / "verify-upgrade-profile.py")
    verdict = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verdict)
finally:
    sys.path.pop(0)

GAME = "12345678-1234-1234-1234-123456789abc"
PIN = "b" * 40
ROOT = Path("/mnt/c/taf-scenario.Test01")
CONFIG = dict(case="inheritance", probe=PIN)


class VerifyUpgradeProfileTest(unittest.TestCase):
    def journal(self):
        messages = [
            ("LOAD-BEGIN", "exact sealed save; game-id=" + GAME + "; new-game=false; mod-restore=false"),
            ("UPGRADE-PREACTIVATION", "case=inheritance; raw-source-exact=true; pre-normalization=true; pre-repair=true; repair-calls=0"
             + "; seal-stage-readable=true; stage-origin=old-origin; stage-sha256=" + "a" * 64),
            ("SCRIPT-COMPLETE", "native-upgrade cases=1 passed=1 failed=0; case=inheritance; actual-source-save=true; old-pin="
             + verdict.OLD_PIN + "; pre-normalization-and-pre-repair=true; legacy-canonical=true; repair-calls=0"
             + "; source-graph-preserved=true; ordinary-ui-acceptance=false; no-world-state-written-by-witness=true"),
        ]
        return "".join("2026-09-08T08:00:00.000Z\t" + name + "\tOK\t" + message + "\n"
                       for name, message in messages).encode()

    def test_exact_stage_and_reader_journal(self):
        verdict.journal(self.journal(), CONFIG, GAME)

    def test_missing_stage_no_longer_passes_old_completion_shape(self):
        wire = self.journal().replace(b"; seal-stage-readable=true; stage-origin=old-origin; stage-sha256=" + b"a" * 64, b"")
        with self.assertRaises(ValueError):
            verdict.journal(wire, CONFIG, GAME)

    def test_repair_missing_reordered_or_repeated_row_refuses(self):
        original = self.journal()
        variants = [original.replace(b"repair-calls=0", b"repair-calls=1"),
                    original.replace(b"\tOK\t", b"\tREFUSED\t", 1),
                    b"\n".join(original.split(b"\n")[1:]), original + original.split(b"\n")[0] + b"\n"]
        for raw in variants:
            with self.subTest(raw=raw[:90]), self.assertRaises(ValueError):
                verdict.journal(raw, CONFIG, GAME)

    def downgrade(self):
        request = ("taf-downgrade-observe-v1\nC:\\taf-scenario.Test01\norigin\n" + PIN + "\n"
                   + verdict.OLD_PIN + "\na 100 " + "a" * 64 + "\nb absent\n").encode()
        report = ("taf-downgrade-report-v1\nroot=C:\\taf-scenario.Test01\norigin=origin\nsource-pin=" + PIN
            + "\nold-pin=" + verdict.OLD_PIN + "\nrequest-sha256=" + verdict.sha(request)
            + "\nsource-pin-authority=host-inventory\nold-runtime-authority=host-inventory\nentry=MainMenu.Show-postfix\n"
            + "a 100 " + "a" * 64 + "\nslot-result=OutOfBounds; 'profile_schema' is 2, outside 0 to 1\n"
            + "b absent\nslot-result=absent\nread-stage=absent\nschema2-rejected=1\ninput-bytes-unchanged=true\n"
            + "no-escaped-parser-or-store-exception=true\ngame-created=false\nsave-loaded=false\nstatus=observed\n").encode()
        terminal = ("INFO - native-downgrade-reader cases=1 passed=1 failed=0; main-menu=true; game-created=false; "
                    + "save-loaded=false; report-sha256=" + verdict.sha(report) + "\n").encode()
        return report, request, terminal

    def test_new_old_runtime_provenance_row_is_required(self):
        report, request, terminal = self.downgrade()
        self.assertEqual(verdict.downgrade(report, request, terminal, ROOT, CONFIG), "absent")
        with self.assertRaises(ValueError):
            verdict.downgrade(report.replace(b"old-runtime-authority=host-inventory\n", b""), request, terminal, ROOT, CONFIG)

    def test_report_without_post_cleanup_terminal_never_passes(self):
        report, request, terminal = self.downgrade()
        for log in (b"", terminal + terminal, terminal.replace(verdict.sha(report).encode(), b"f" * 64)):
            with self.subTest(log=log[:70]), self.assertRaises(ValueError):
                verdict.downgrade(report, request, log, ROOT, CONFIG)

    def test_retained_source_and_approved_producer_are_not_optional(self):
        source = (TOOLS / "verify-upgrade-profile.py").read_text()
        for token in ('parser.add_argument("--source", required=True',
                      'authenticate_source(args.repo, args.source, source_mode, source_pin,',
                      'game=args.game',
                      'after_state.get("donorAuthority") == source_state.get("donorAuthority")',
                      'source_config["probe"] == (args.source_probe_pin or args.candidate)',
                      'source_snapshot == extra["scenario-load-snapshot.txt"]',
                      'source_request == extra["scenario-load.txt"]',
                      'request_rows[5:7] == expected_slots', 'after_snapshot == source_snapshot'):
            self.assertIn(token, source)

    def test_third_party_modwarn_is_retained_and_never_refuses(self):
        self.assertEqual(verdict.diagnostics(b"INFO clean\nMODWARN [Pets of Harvest Dawn] - notice\n"),
                         ["MODWARN [Pets of Harvest Dawn] - notice"])

    def test_taf_tagged_modwarn_refuses(self):
        with self.assertRaisesRegex(ValueError, "Thousand and First diagnostic"):
            verdict.diagnostics(b"INFO clean\nMODWARN [The Thousand and First] - refused\n")

    def test_shares_the_taf_diagnostic_contract_not_a_copy(self):
        source = (TOOLS / "verify-upgrade-profile.py").read_text()
        self.assertIn("from upgrade_profile_witnesses import diagnostics", source)
        self.assertNotIn("DIAGNOSTIC = re.compile", source)


if __name__ == "__main__":
    unittest.main()
