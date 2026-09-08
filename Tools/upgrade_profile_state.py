"""Closed, bounded historical-state inventory and native transfer plans.

This transports history, not just the selected Primary. It never normalizes a seal, deletes a
claim, repairs a save, interprets a leftover .live as ownership, or copies compiled/runtime caches.
"""
from __future__ import annotations

import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

import scenario_profile
from upgrade_profile_inputs import (CONFIG, OLD_PIN, SHA, json_bytes, local_inputs, parse_config,
                                    portable, recipe_request, require, sha)

_spec = importlib.util.spec_from_file_location("taf_load_files", Path(__file__).with_name("prepare-scenario-load.py"))
assert _spec and _spec.loader
fs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fs)
GUID = fs.GUID
ID = r"[A-Za-z0-9_-]{1,96}"
SAVE_LEAF = re.compile(r"(?:(?:Primary|Checkpoint|Quick)\.(?:json|sav\.gz(?:\.bak)?)|(?:Cache|PrimaryCache|CheckpointCache|QuickCache)\.db)\Z")
STORE_FOLDERS = ("Stages", "Legacies", "Receipts", "Claims")
WSLPATH_TIMEOUT_SECONDS = 15  # matches run-upgrade-profile.py's bounded path conversion
STOP_ASSERT_TIMEOUT_SECONDS = 60  # matches run-upgrade-profile.py's bounded stop-mode call


def receipt_tuple(name: str) -> bool:
    if not name.endswith(".receipt"):
        return False
    rest = name[:-8]
    for _ in range(2):
        match = re.match(r"([1-9][0-9]{0,2})_", rest)
        if not match:
            return False
        length = int(match[1])
        rest = rest[match.end():]
        value, rest = rest[:length], rest[length:]
        if len(value) != length or not re.fullmatch(ID, value):
            return False
    return not rest


def state_path(path: str, is_directory: bool) -> bool:
    """Writer paths: SealStore.Paths/Claims/Legacy; engine XRLGame1630,2050,2111,2337.

    Session backups live in Local/Session and are intentionally NOT save-authority inputs for
    LoadGame(Session:false). Unknown Synced files, SQLite WAL/SHM and interrupted writes refuse;
    they are never omitted or removed to turn a failed copy into success.
    """
    parts = path.split("/")
    if is_directory:
        return (parts in (["Synced"], ["Synced", "Saves"], ["Synced", "ThousandAndFirst"])
                or len(parts) == 3 and (parts[:2] == ["Synced", "Saves"] and bool(GUID.fullmatch(parts[2]))
                    or parts[:2] == ["Synced", "ThousandAndFirst"] and parts[2] in STORE_FOLDERS))
    if parts == ["Synced", "HighScores.json"]:
        return True  # XRL/Core/Scoreboard2.cs52; reconciliation also consumes this death evidence.
    if len(parts) != 4:
        return False
    if parts[:2] == ["Synced", "Saves"]:
        return bool(GUID.fullmatch(parts[2]) and SAVE_LEAF.fullmatch(parts[3]))
    if parts[:2] != ["Synced", "ThousandAndFirst"]:
        return False
    folder, name = parts[2:]
    if folder == "Stages":
        return bool(re.fullmatch(ID + r"\.[ab]\.seal", name)
                    or re.fullmatch(r"\.journal-" + ID + r"\.lock", name))
    if folder == "Legacies":
        return name == ".legacies.lock" or bool(re.fullmatch(ID + r"\.seal", name))
    if folder == "Receipts":
        return name == ".claims.lock" or receipt_tuple(name)
    return folder == "Claims" and name.endswith(".live") and receipt_tuple(name[:-5])


def inventory(root: Path, roots: list[str]) -> tuple[list[dict], list[str]]:
    files, directories, seen, total = [], [], set(), 0
    for top in roots:
        fs.directory(root / top)
        for current, children, leaves in os.walk(root / top, followlinks=False):
            current_path = Path(current)
            fs.directory(current_path)
            relative = portable(current_path.relative_to(root).as_posix())
            require(relative.casefold() not in seen, "duplicate/colliding directory")
            seen.add(relative.casefold())
            directories.append(relative)
            for child in children:
                fs.directory(current_path / child)
            for leaf in leaves:
                path = current_path / leaf
                relative = portable(path.relative_to(root).as_posix())
                require(relative.casefold() not in seen, "duplicate/colliding file")
                seen.add(relative.casefold())
                size = fs.file_status(path).st_size
                total += size
                files.append(dict(path=relative, size=size, sha256=fs.digest(path)))
            require(len(files) <= fs.MAX_FILES and len(directories) <= fs.MAX_FILES
                    and total <= fs.MAX_TREE_BYTES, "profile inventory exceeds bounds")
    return sorted(files, key=lambda row: row["path"]), sorted(directories)


def validate_state(files: list[dict], directories: list[str]) -> None:
    for path in directories:
        require(state_path(path, True), "unexpected historical-state directory: " + path)
    for row in files:
        path = row["path"]
        require(state_path(path, False), "unexpected historical-state file: " + path)
        if path.endswith((".lock", ".live")):
            require(row["size"] == 0, "nonempty lock/claim marker: " + path)
    require("Synced/Saves" in directories, "source save directory missing")


def windows(root: Path) -> str:
    require(bool(fs.CLI_ROOT.fullmatch(str(root))), "use exact /mnt/c/taf-scenario.<id> roots")
    return "C:\\" + root.name


def native_plan(source: Path, destination: Path | None, roots: list[str],
                files: list[dict], directories: list[str], selected: list[str] | None = None) -> dict:
    return dict(schema="taf-upgrade-copy-v1", source=windows(source),
                destination=windows(destination) if destination else None, roots=roots,
                files=files, directories=directories, selected=selected or [])


def native(mode: str, plan: dict) -> dict:
    """No skip/override CLI. Temporary plan retained for diagnosis; no destructive cleanup."""
    scratch = Path(tempfile.mkdtemp(prefix="taf-upgrade-plan.", dir="/mnt/c"))
    path = scratch / "plan.json"
    fs.write_new(path, json.dumps(plan, ensure_ascii=True, separators=(",", ":")).encode("utf-8"))
    helper = Path(__file__).with_name("upgrade-profile-trust.ps1")
    helper_win = subprocess.run(["wslpath", "-w", str(helper)], check=True,
                                stdout=subprocess.PIPE, text=True,
                                timeout=WSLPATH_TIMEOUT_SECONDS).stdout.strip()
    plan_win = "C:\\" + scratch.name + "\\plan.json"
    run = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                          helper_win, "-Mode", mode, "-Plan", plan_win], check=True,
                         stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=120)
    proof = json.loads(run.stdout.decode("utf-8-sig"))
    require(proof.get("schema") == "taf-upgrade-copy-proof-v1" and proof.get("status") == "verified"
            and proof.get("mode") == mode and proof.get("source") == plan["source"]
            and proof.get("destination") == plan["destination"] and proof.get("cleanupComplete") is True,
            "native copy returned wrong request or incomplete cleanup")
    require(proof.get("planSHA256") == sha(fs.read_bytes(path, 16 * 1024**2))
            and proof.get("roots") == plan["roots"] and proof.get("directories") == plan["directories"]
            and proof.get("selected") == plan["selected"], "native plan/inventory identity mismatch")
    require(proof.get("endpointIdle") is True and proof.get("continuousIdleVerified") is False
            and proof.get("gracefulQuitVerified") is False, "native identity/idle proof missing")
    require(len(proof.get("files", [])) == len(plan["files"]), "native proof inventory length mismatch")
    actual = {row["path"]: row for row in proof["files"]}
    require(len(actual) == len(plan["files"]), "native proof repeats file")
    for row in plan["files"]:
        observed = actual.get(row["path"], {})
        copied = mode == "Copy" or mode == "Slots" and row["path"] in plan["selected"]
        require(observed.get("size") == row["size"] and observed.get("before") == row["sha256"]
                and observed.get("after") == row["sha256"]
                and observed.get("copy") == (row["sha256"] if copied else None), "native proof hash mismatch")
    proof["plan"] = str(path)
    return proof


def stopped_source(source: Path, game: Path) -> None:
    """Exact owned-process exit, including retained donor ancestors; no process is stopped here."""
    fs.ownership_shape(fs.read_bytes(source / "process-ownership.json", 16384), source)
    helper = Path(__file__).with_name("assert-scenario-source-stopped.ps1")
    convert = lambda path: subprocess.run(["wslpath", "-w", str(path)], check=True,
        stdout=subprocess.PIPE, text=True, timeout=WSLPATH_TIMEOUT_SECONDS).stdout.strip()
    subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    convert(helper), "-Root", windows(source), "-Game", convert(game)], check=True,
                   timeout=STOP_ASSERT_TIMEOUT_SECONDS)


def capture_donor(source: Path, config: dict, state: dict) -> dict:
    from upgrade_profile_witnesses import capture_donor as observed
    return observed(source, config, state)


def capture_stage(source: Path, config: dict, state: dict) -> dict:
    from upgrade_profile_witnesses import capture_stage as observed
    return observed(source, config, state)


def authenticate_source(repo: Path, source: Path, mode: str, pin: str | None = None,
                        game: Path | None = None) -> tuple[dict, dict]:
    config = parse_config(fs.read_bytes(source / "Local" / CONFIG, 4096))
    require(config["mode"] == mode and (pin is None or config["runtime"] == pin),
            "source profile provenance is not the requested runtime/mode")
    donor_witness, donor_authority = None, None
    if config.get("donor") is not None:
        require(mode == "source" and config["case"] == "inheritance" and game is not None,
                "retained donor authentication requires the exact source mode and game executable")
        reference = config["donor"]
        donor_root = Path(reference["root"])
        require(str(donor_root).casefold() != str(source).casefold(), "source is its own donor")
        stopped_source(donor_root, game)
        # Finite chain: configuration forbids any donor reference on source-donor itself.
        donor_config, donor_state = authenticate_source(repo, donor_root, "source-donor", OLD_PIN, game=game)
        require(donor_config["probe"] == reference["probe"], "retained donor probe was not explicitly bound")
        donor_witness = capture_donor(donor_root, donor_config, donor_state)
        require(donor_witness["receiptSha256"] == reference["receiptSha256"],
                "retained native donor receipt changed")
        donor_authority = sha(json_bytes(dict(config=donor_config, files=donor_state["files"],
            directories=donor_state["directories"], localHashes=donor_state["localHashes"], witness=donor_witness)))
    expected = local_inputs(repo, config, donor_witness=donor_witness)
    # Upgrade destinations carry load inputs and cannot masquerade as fresh stage sources.
    # New source recipes also prove their exact unattended scripts and retained native ancestor.
    files, directories = inventory(source, ["Local", "Synced"])
    local = {row["path"][6:]: row["sha256"] for row in files if row["path"].startswith("Local/")}
    birth = {p: sha(raw) for p, raw in expected.items()}
    observed_expected = dict(birth)
    if config["schema"] == "taf-upgrade-profile-v2":
        from upgrade_profile_options import validate_options
        options = fs.read_bytes(source / "Local/PlayerOptions.json", 1024**2)
        validate_options(config, expected["PlayerOptions.json"], options)
        observed_expected["PlayerOptions.json"] = sha(options)
    require(local == observed_expected,
            "source Local differs from independently pinned runtime/probe inputs")
    seal = scenario_profile.read_seal(str(source) + ".seal/profile.sha256")
    require(seal == {scenario_profile.normalize(p): h for p, h in birth.items()},
            "source original closed seal disagrees with pinned birth inputs")
    request = (recipe_request(repo, config) + "\n").encode("ascii")
    require(fs.read_bytes(Path(str(source) + ".seal/request.txt"), 1024) == request,
            "source request seal mismatch")
    state_files = [row for row in files if row["path"].startswith("Synced/")]
    state_dirs = [p for p in directories if p == "Synced" or p.startswith("Synced/")]
    validate_state(state_files, state_dirs)
    proof = native("Inspect", native_plan(source, None, ["Local", "Synced"], files, directories))
    return config, dict(files=state_files, directories=state_dirs, inspect=proof,
                        localHashes=local, donorWitness=donor_witness, donorAuthority=donor_authority)


def capture(source: Path, config: dict, state: dict) -> tuple[bytes, bytes, dict]:
    require(not os.path.lexists(source / "upgrade-save-failure.txt"), "source native observer refused")
    raw = fs.read_bytes(source / "upgrade-save-receipt.txt", 1024)
    lines = raw.decode("ascii").split("\n")
    require(len(lines) == 9 and lines[-1] == "" and lines[0] == "taf-upgrade-save-receipt-v1"
            and lines[1] == OLD_PIN and GUID.fullmatch(lines[2]) and lines[3] == config["case"]
            and all(SHA.fullmatch(v) for v in (lines[4], lines[5], lines[7]))
            and lines[6] == "cache-bind-after-quit", "invalid native upgrade-save receipt")
    snapshot = fs.read_bytes(source / "upgrade-save-snapshot.txt", 4 * 1024**2)
    header = snapshot.decode("utf-8").split("\n")
    require(len(header) == 12 and header[-1] == "" and header[:4] ==
            ["taf-upgrade-save-v1", lines[2], lines[3], OLD_PIN] and sha(snapshot) == lines[7],
            "native snapshot/receipt identity mismatch")
    rows = {row["path"]: row for row in state["files"]}
    for leaf, expected in zip(fs.SAVE_FILES[:2], lines[4:6]):
        row = rows.get("Synced/Saves/" + lines[2] + "/" + leaf)
        require(row is not None and row["size"] > 0 and row["sha256"] == expected,
                "selected native-save bytes changed: " + leaf)
    info = json.loads(fs.read_bytes(source / "Synced/Saves" / lines[2] / "Primary.json", 4 * 1024**2))
    require(info.get("ID") == lines[2] and info.get("SaveVersion") == 408
            and info.get("GameVersion") == "2.0.211.51"
            and isinstance(info.get("ModsEnabled"), list)
            and "r_ThousandAndFirst" in info["ModsEnabled"]
            and set(info["ModsEnabled"]) <= {"r_ThousandAndFirst", "FreeholdGames_DLC_PetsPack1"},
            "selected save has wrong engine, identity, or unrelated mods")
    cache = rows.get("Synced/Saves/" + lines[2] + "/Cache.db")
    require(cache is not None and cache["size"] > 0, "selected post-quit cache missing")
    request = ("taf-scenario-load-v1\n" + lines[2] + "\n" + lines[4] + "\n" + lines[5]
               + "\n" + cache["sha256"] + "\n" + lines[7] + "\n").encode("ascii")
    witness = dict(gameId=lines[2], receiptSha256=sha(raw), snapshotSha256=sha(snapshot))
    if config["schema"] == "taf-upgrade-profile-v2":
        from upgrade_profile_witnesses import source_link
        witness.update(source_link(source, config, state, lines[2], sha(snapshot), sha(raw)))
    return snapshot, request, witness
