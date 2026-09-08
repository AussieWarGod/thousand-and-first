"""Synthetic host contracts only. No test result here is native upgrade acceptance."""
from __future__ import annotations

import importlib.util
import json
import os
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
    spec = importlib.util.spec_from_file_location("taf_upgrade_profile", TOOLS / "prepare-upgrade-profile.py")
    host = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(host)
finally:
    sys.path.pop(0)

CURRENT = "b" * 40
GAME = "12345678-1234-1234-1234-123456789abc"


class FakeCommit:
    def __init__(self, repo, pin):
        self.pin = pin

    def runtime(self):
        return {"manifest.json": b'{"title":"The Thousand and First","Directories":[{"Paths":["/Core/"]}]}',
                "Core/Runtime.cs": self.pin.encode()}

    def harness(self):
        return {"EmbarkModules.xml": b'<stringgamestate Name="r_TAF_ScenarioRequest_v1" Value="old"/>',
                "OldHarness.cs": self.pin.encode()}

    def blob(self, path):
        if path == "Tools/smoke/PlayerOptions.json":
            return b'{"Diagnostics":"Yes"}'
        if path == "Tools/smoke/ModSettings.json":
            return b"{}\n"
        if path.startswith("Harness/"):
            return (self.pin + ":" + path).encode()
        raise AssertionError(path)


class UpgradeProfileInputTest(unittest.TestCase):
    def config(self, mode="source"):
        return inputs.configuration(mode, inputs.OLD_PIN if mode in ("source", "downgrade") else CURRENT,
                                    CURRENT, "reader" if mode in ("downgrade", "stage-source") else "inheritance", "#123",
                                    schema=inputs.PROFILE_V1)

    def test_old_runtime_pin_not_a_version_label(self):
        for pin in ("v0.3.1", "dev", "a" * 40):
            with self.subTest(pin=pin), self.assertRaises(ValueError):
                inputs.configuration("source", pin, CURRENT, "inheritance", "#123")

    def test_current_runtime_and_observers_share_pin(self):
        with self.assertRaises(ValueError):
            inputs.configuration("upgrade", "c" * 40, CURRENT, "inheritance", "#123")

    def test_canonical_config_roundtrip(self):
        for mode in ("source", "upgrade", "stage-source", "downgrade"):
            value = self.config(mode)
            self.assertEqual(inputs.parse_config(inputs.json_bytes(value)), value)
        with self.assertRaises(ValueError):
            inputs.parse_config(json.dumps(self.config()).encode())

    def test_path_refusals(self):
        for path in ("../save", "a\\b", "a:stream", "a/CON.txt", "a//b", "a/..", "x. ", "/root", "A/COM¹.cs"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                inputs.portable(path)

    def test_source_overlay_is_explicit_old_compatible_list(self):
        with mock.patch.object(inputs, "Commit", FakeCommit):
            value = inputs.local_inputs(TOOLS.parent, self.config())
        mod = "Mods/ThousandAndFirst/"
        self.assertEqual(value[mod + "Core/Runtime.cs"], inputs.OLD_PIN.encode())
        self.assertEqual(value[mod + "Harness/OldHarness.cs"], inputs.OLD_PIN.encode())
        for name in inputs.SOURCE_PROBES:
            self.assertEqual(value[mod + "Harness/" + name], (CURRENT + ":Harness/" + name).encode())
        self.assertNotIn("scenario-script.txt", value)
        self.assertIn("upgrade-save-request.txt", value)
        self.assertIs(json.loads(value["ModSettings.json"])["FreeholdGames_DLC_PetsPack1"]["Enabled"], False)

    def test_upgrade_runtime_never_copies_old_local(self):
        with mock.patch.object(inputs, "Commit", FakeCommit):
            value = inputs.local_inputs(TOOLS.parent, self.config("upgrade"), {"scenario-load.txt": b"receipt"})
        self.assertEqual(value["Mods/ThousandAndFirst/Core/Runtime.cs"], CURRENT.encode())
        self.assertNotIn("upgrade-save-request.txt", value)
        self.assertNotIn("Cache.db", value)
        self.assertEqual(value["scenario-script.txt"], b"stagedigest\n")

    def test_extra_cannot_overwrite_pin_or_escape_local(self):
        with mock.patch.object(inputs, "Commit", FakeCommit):
            for key in ("PlayerOptions.json", "../Player.log"):
                with self.subTest(key=key), self.assertRaises(ValueError):
                    inputs.local_inputs(TOOLS.parent, self.config(), {key: b"replacement"})

    def test_stage_source_has_no_auto_world(self):
        with mock.patch.object(inputs, "Commit", FakeCommit):
            value = inputs.local_inputs(TOOLS.parent, self.config("stage-source"))
        self.assertIn("upgrade-stage-source.txt", value)
        self.assertNotIn("scenario-script.txt", value)
        self.assertNotIn("taf-downgrade-request.txt", value)


class UpgradeProfileStateTest(unittest.TestCase):
    def test_all_synced_history_paths_are_in_scope(self):
        for name in ("Primary.sav.gz", "Primary.sav.gz.bak", "Primary.json", "Cache.db",
                     "Checkpoint.sav.gz", "Checkpoint.sav.gz.bak", "Checkpoint.json", "CheckpointCache.db",
                     "Quick.sav.gz", "Quick.json", "QuickCache.db"):
            self.assertTrue(state.state_path("Synced/Saves/" + GAME + "/" + name, False), name)
        for path in ("Synced/HighScores.json", "Synced/ThousandAndFirst/Stages/world.a.seal",
                     "Synced/ThousandAndFirst/Stages/.journal-world.lock",
                     "Synced/ThousandAndFirst/Legacies/legacy.seal",
                     "Synced/ThousandAndFirst/Legacies/.legacies.lock",
                     "Synced/ThousandAndFirst/Receipts/.claims.lock",
                     "Synced/ThousandAndFirst/Receipts/6_legacy5_world.receipt",
                     "Synced/ThousandAndFirst/Claims/6_legacy5_world.receipt.live"):
            self.assertTrue(state.state_path(path, False), path)

    def test_unknown_or_interrupted_state_refuses_not_omits(self):
        for path in ("Synced/unknown.json", "Synced/Saves/" + GAME + "/Cache.db-wal",
                     "Synced/Saves/" + GAME + "/Cache.db-shm", "Synced/Saves/" + GAME + "/evil.cs",
                     "Synced/ThousandAndFirst/Stages/world.a.seal.writing.abc",
                     "Synced/ThousandAndFirst/Receipts/06_legacy5_world.receipt",
                     "Synced/ThousandAndFirst/Claims/6_legacy5_world.receipt.live.backup"):
            self.assertFalse(state.state_path(path, False), path)

    def test_empty_folders_preserved_and_nested_store_refuses(self):
        for folder in state.STORE_FOLDERS:
            self.assertTrue(state.state_path("Synced/ThousandAndFirst/" + folder, True))
            self.assertFalse(state.state_path("Synced/ThousandAndFirst/" + folder + "/nested", True))

    def test_nonempty_lock_is_not_accepted_as_lease(self):
        with self.assertRaises(ValueError):
            state.validate_state([dict(path="Synced/ThousandAndFirst/Receipts/.claims.lock", size=1)],
                                 ["Synced", "Synced/Saves"])

    def fixture(self, root):
        config = inputs.configuration("source", inputs.OLD_PIN, CURRENT, "inheritance", "#123",
                                      schema=inputs.PROFILE_V1)
        save = root / "Synced/Saves" / GAME
        save.mkdir(parents=True)
        payloads = {"Primary.sav.gz": b"synthetic-not-a-native-save", "Cache.db": b"post-quit-cache",
                    "Primary.json": json.dumps(dict(ID=GAME, SaveVersion=408, GameVersion="2.0.211.51",
                                                    ModsEnabled=["r_ThousandAndFirst"])).encode()}
        for leaf, data in payloads.items():
            (save / leaf).write_bytes(data)
        wire = ("\n".join(["taf-upgrade-save-v1", GAME, "inheritance", inputs.OLD_PIN,
                           "1", "2", "3", "4", "AA==", "AA==", "AA=="]) + "\n").encode()
        receipt = ("\n".join(["taf-upgrade-save-receipt-v1", inputs.OLD_PIN, GAME, "inheritance",
                              inputs.sha(payloads["Primary.sav.gz"]), inputs.sha(payloads["Primary.json"]),
                              "cache-bind-after-quit", inputs.sha(wire)]) + "\n").encode()
        (root / "upgrade-save-snapshot.txt").write_bytes(wire)
        (root / "upgrade-save-receipt.txt").write_bytes(receipt)
        rows, dirs = state.inventory(root, ["Synced"])
        return config, dict(files=rows, directories=dirs), payloads

    def test_post_quit_cache_binds_destination_request(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config, frozen, data = self.fixture(root)
            _, request, _ = state.capture(root, config, frozen)
            self.assertEqual(request.decode().splitlines()[4], inputs.sha(data["Cache.db"]))
            self.assertNotIn(b"cache-bind-after-quit", request)

    def test_source_refusal_and_snapshot_tamper_never_pass(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config, frozen, _ = self.fixture(root)
            (root / "upgrade-save-snapshot.txt").write_bytes(b"tampered")
            with self.assertRaises(ValueError):
                state.capture(root, config, frozen)
            (root / "upgrade-save-failure.txt").write_bytes(b"failed")
            with self.assertRaisesRegex(ValueError, "observer refused"):
                state.capture(root, config, frozen)

    def test_plan_contract_has_fixed_keys_and_no_skip_flag(self):
        plan = state.native_plan(Path("/mnt/c/taf-scenario.Source"), None, ["Synced"], [], ["Synced"])
        self.assertEqual(list(plan), ["schema", "source", "destination", "roots", "files", "directories", "selected"])
        self.assertEqual(plan["selected"], [])
        self.assertIsNone(plan["destination"])

    def test_attended_ownership_still_follows_closed_seal(self):
        text = (TOOLS / "run-scenario.ps1").read_text()
        self.assertIn("[switch]$OwnAttended", text)
        self.assertIn("if ((Test-Path -LiteralPath $scriptPath) -or $OwnAttended)", text)
        self.assertLess(text.index("Assert-ClosedSeal -"), text.index("if ($OwnAttended)"))
        self.assertIn("@('CoQ', 'CavesOfQud')", text)
        self.assertIn("Start-TafOwnedScenarioProcess -Root $rootPath -Game $Game", text)


if __name__ == "__main__":
    unittest.main()
