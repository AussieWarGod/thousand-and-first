"""Synthetic transport/receipt laws only; these tests never claim a native run."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
try:
    import upgrade_profile_inputs as inputs
    import upgrade_profile_state as state
    import upgrade_profile_witnesses as witnesses
finally:
    sys.path.pop(0)

PIN = "b" * 40
GAME = "12345678-1234-1234-1234-123456789abc"
NEXT_GAME = "22345678-1234-1234-1234-123456789abc"


class PersonaCommit:
    def __init__(self, repo, pin):
        self.pin = pin

    def runtime(self):
        return {"manifest.json": b'{"title":"TAF","Directories":[{"Paths":["/Core/"]}]}',
                "Core/Runtime.cs": self.pin.encode()}

    def harness(self):
        return {"EmbarkModules.xml": b'<root><stringgamestate Name="r_TAF_ScenarioRequest_v1" Value="old"/>'
                b'<location ID="TAFTestGround" Location="old"/></root>', "OldHarness.cs": self.pin.encode()}

    def blob(self, path):
        if path.startswith("Tools/personas/"):
            return (TOOLS.parent / path).read_bytes()
        if path in ("Tools/smoke/PlayerOptions.json", "Tools/smoke/ModSettings.json"):
            return b"{}\n"
        if path.startswith("Harness/"):
            return (self.pin + ":" + path).encode()
        raise AssertionError(path)


def configuration(mode, receipt="a" * 64):
    return inputs.configuration(mode, inputs.OLD_PIN if mode in ("source-donor", "source", "downgrade") else PIN,
        PIN, "reader" if mode in ("stage-source", "downgrade") else "inheritance", "#123",
        donor=dict(root="/mnt/c/taf-scenario.Donor", probe=PIN, receiptSha256=receipt) if mode == "source" else None)


def journal(mode):
    verb = "upgrade-source-donor" if mode == "source-donor" else "upgrade-source-reserved"
    entries = [("RUNNER-ARMED", "armed by BeginTakeActionEvent; popups suppressed from IGameSystem.OnAdded"),
               ("SCRIPT-BEGIN", "3 verb(s) from owned sealed profile"),
               ("stagedigest", "founded=false"), (verb, "native fixture evidence"),
               ("stagedigest", "observed"), ("SCRIPT-COMPLETE", "3 verb(s) ran without a refusal")]
    if mode == "stage-source":
        entries[1] = ("SCRIPT-BEGIN", "5 verb(s) from owned sealed profile")
        entries[3:4] = [("upgrade-stage-setup", "founded"), ("advance", "2400 turns"),
                        ("advance-progress", "100 turns"), ("advance-complete", "2400 turns"),
                        ("upgrade-stage-save", "native fixture evidence")]
        entries[-1] = ("SCRIPT-COMPLETE", "5 verb(s) ran without a refusal")
    entries[0:0] = [("AUTOSTART", "exact sealed native scenario"),
                    ("TESTGROUND-BUILT", "native test zone"), ("TESTGROUND-RESTRIP", "native test zone")]
    return "".join("2026-09-08T11:00:00.000Z\t" + name + "\tOK\t" + message + "\n"
                   for name, message in entries).encode()


class UnattendedRecipeTest(unittest.TestCase):
    def test_v2_modes_roundtrip_with_explicit_donor_chain(self):
        for mode in ("source-donor", "source", "stage-source", "upgrade", "downgrade"):
            with self.subTest(mode=mode):
                config = configuration(mode)
                self.assertEqual(inputs.parse_config(inputs.json_bytes(config)), config)

    def test_donor_may_not_point_to_another_donor(self):
        config = configuration("source-donor")
        config["donor"] = configuration("source")["donor"]
        with self.assertRaises(ValueError):
            inputs.parse_config(inputs.json_bytes(config))

    def test_source_requires_donor_and_rejects_detached_fallback(self):
        for case in ("inheritance", "detached-transition"):
            with self.subTest(case=case), self.assertRaises(ValueError):
                inputs.configuration("source", inputs.OLD_PIN, PIN, case, "#123")

    def test_retained_v1_is_not_reinterpreted_as_a_scripted_recipe(self):
        config = inputs.configuration("source", inputs.OLD_PIN, PIN, "inheritance", "#123", schema=inputs.PROFILE_V1)
        with mock.patch.object(inputs, "Commit", PersonaCommit):
            local = inputs.local_inputs(TOOLS.parent, config)
        self.assertNotIn("scenario-script.txt", local)
        self.assertEqual(len([name for name in local if name.endswith(tuple(inputs.SOURCE_DRIVERS))]), 0)

    def test_world_producers_and_menu_reader_get_exact_candidate_scripts(self):
        scripts = {"source-donor": b"stagedigest\nupgrade-source-donor\nstagedigest\n",
                   "stage-source": b"stagedigest\nupgrade-stage-setup\nadvance 2400\nupgrade-stage-save\nstagedigest\n",
                   "downgrade": b"upgrade-downgrade-check\n"}
        for mode, script in scripts.items():
            with self.subTest(mode=mode), mock.patch.object(inputs, "Commit", PersonaCommit):
                local = inputs.local_inputs(TOOLS.parent, configuration(mode))
                self.assertEqual(local["scenario-script.txt"], script)
                self.assertIn("upgrade-persona.txt", local)
                self.assertNotIn("scenario-load.txt", local)
                runtime = PIN if mode == "stage-source" else inputs.OLD_PIN
                self.assertEqual(local["Mods/ThousandAndFirst/Core/Runtime.cs"], runtime.encode())

    def test_inheritor_refuses_untrusted_donor_input(self):
        with mock.patch.object(inputs, "Commit", PersonaCommit), self.assertRaises(ValueError):
            inputs.local_inputs(TOOLS.parent, configuration("source"))

    def test_donor_path_alias_or_unpinned_probe_refuses(self):
        for field, bad in (("root", "/tmp/donor"), ("root", "/mnt/c/taf-scenario.Donor/../Elsewhere"),
                           ("probe", "dev"), ("receiptSha256", "a" * 63)):
            config = configuration("source")
            config["donor"][field] = bad
            with self.subTest(field=field, bad=bad), self.assertRaises(ValueError):
                inputs.parse_config(inputs.json_bytes(config))


class NativeSourceReceiptTest(unittest.TestCase):
    def fixture(self, root, mode="source-donor"):
        donor = mode == "source-donor"
        config = configuration(mode)
        save = root / "Synced/Saves" / GAME
        save.mkdir(parents=True)
        records = root / "Synced/ThousandAndFirst"
        (records / "Stages").mkdir(parents=True)
        (records / "Legacies").mkdir()
        data = {"Primary.sav.gz": b"synthetic save, never native evidence", "Cache.db": b"stopped cache",
                "Primary.json": json.dumps(dict(ID=GAME, SaveVersion=408, GameVersion="2.0.211.51",
                                                 ModsEnabled=["r_ThousandAndFirst"])).encode()}
        for name, raw in data.items():
            (save / name).write_bytes(raw)
        stage, legacy = b"synthetic stage", b"synthetic promoted legacy"
        (records / "Stages" / (GAME + ".a.seal")).write_bytes(stage)
        (records / "Legacies/legacy.seal").write_bytes(legacy)
        kind = "donor" if donor else "stage"
        values = ["taf-upgrade-" + kind + "-receipt-v1", config["runtime"], GAME, GAME,
                  "legacy", "lineage", "0", inputs.sha(stage)]
        if donor:
            values.append(inputs.sha(legacy))
        values += [inputs.sha(data["Primary.sav.gz"]), inputs.sha(data["Primary.json"]), "cache-bind-after-quit"]
        (root / ("upgrade-" + kind + "-receipt.txt")).write_bytes(("\n".join(values) + "\n").encode())
        (root / "scenario-journal.tsv").write_bytes(journal(mode))
        (root / "Player.log").write_bytes(b"INFO - native script completed\n")
        rows, dirs = state.inventory(root, ["Synced"])
        return config, dict(files=rows, directories=dirs)

    def test_actual_retained_hashes_and_script_are_required(self):
        for mode in ("source-donor", "stage-source"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                config, frozen = self.fixture(root, mode)
                capture = witnesses.capture_donor if mode == "source-donor" else witnesses.capture_stage
                result = capture(root, config, frozen)
                self.assertEqual(result["gameId"], GAME)
                self.assertEqual(result["cacheSha256"], inputs.sha(b"stopped cache"))
                frozen["files"] = [row for row in frozen["files"] if not row["path"].endswith(".a.seal")]
                with self.assertRaisesRegex(ValueError, "stage"):
                    capture(root, config, frozen)

    def test_missing_completion_refusal_and_foreign_rows_fail(self):
        for raw in (journal("source-donor").replace(b"\tOK\t", b"\tREFUSED\t", 1),
                    journal("source-donor").replace(b"SCRIPT-COMPLETE", b"SCRIPT-STOPPED"),
                    journal("source-donor") + journal("source-donor")):
            with self.subTest(raw=raw[:80]), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / "scenario-journal.tsv").write_bytes(raw)
                with self.assertRaises(ValueError):
                    witnesses.script_journal(root, "source-donor")

    def test_forged_native_receipt_cannot_replace_missing_promoted_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config, frozen = self.fixture(root)
            frozen["files"] = [row for row in frozen["files"] if "/Legacies/" not in row["path"]]
            with self.assertRaisesRegex(ValueError, "promoted legacy"):
                witnesses.capture_donor(root, config, frozen)

    def test_known_failure_artifact_and_raw_diagnostics_are_never_waived(self):
        for artifact in ("upgrade-donor-failure.txt", "Player.log"):
            with self.subTest(artifact=artifact), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                config, frozen = self.fixture(root)
                (root / artifact).write_bytes(b"MODERROR old defect\n")
                with self.assertRaises(ValueError):
                    witnesses.capture_donor(root, config, frozen)

    def test_native_source_link_requires_a_new_game_and_exact_donor(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config, frozen = self.fixture(root)
            donor = witnesses.capture_donor(root, config, frozen)
            config = configuration("source", donor["receiptSha256"])
            frozen["donorWitness"] = donor
            (root / "scenario-journal.tsv").write_bytes(journal("source"))
            fields = ["taf-upgrade-source-link-v1", inputs.OLD_PIN, NEXT_GAME, GAME, GAME, "legacy", "lineage", "0",
                      donor["stageSha256"], donor["legacySha256"], donor["receiptSha256"], "d" * 64, "e" * 64]
            link = root / "upgrade-source-link.txt"
            link.write_bytes(("\n".join(fields) + "\n").encode())
            result = witnesses.source_link(root, config, frozen, NEXT_GAME, "d" * 64, "e" * 64)
            self.assertEqual(result["donorReceiptSha256"], donor["receiptSha256"])
            for index, bad in ((2, GAME), (4, "foreign-origin"), (9, "f" * 64), (11, "f" * 64)):
                changed = list(fields)
                changed[index] = bad
                link.write_bytes(("\n".join(changed) + "\n").encode())
                with self.subTest(index=index), self.assertRaises(ValueError):
                    witnesses.source_link(root, config, frozen, NEXT_GAME, "d" * 64, "e" * 64)

    def test_no_native_or_attended_fallback_bypass_is_added(self):
        text = (TOOLS / "upgrade_profile_state.py").read_text()
        for token in ("stopped_source(donor_root, game)", '"source-donor", OLD_PIN, game=game',
                      'donor_witness["receiptSha256"] == reference["receiptSha256"]',
                      "donorAuthority=donor_authority", "source_link(source, config, state"):
            self.assertIn(token, text)

    def test_boot_prefix_cannot_be_missing_duplicated_reordered_or_trailing(self):
        original = journal("source-donor").splitlines(keepends=True)
        variants = [b"".join(original[3:]), b"".join(original[:1] + original),
                    b"".join([original[1], original[0], *original[2:]]),
                    b"".join(original + original[:1])]
        for raw in variants:
            with self.subTest(raw=raw[:80]), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / "scenario-journal.tsv").write_bytes(raw)
                with self.assertRaises(ValueError):
                    witnesses.script_journal(root, "source-donor")

    def test_complete_recaptured_source_witness_is_compared(self):
        text = (TOOLS / "verify-upgrade-profile.py").read_text()
        self.assertIn("after_witness == source_witness", text)
        self.assertIn("source_snapshot, source_request, source_witness = capture", text)


if __name__ == "__main__":
    unittest.main()
