#!/usr/bin/env python3
"""Read-only verdict for sealed developer Quickstart boot/save/load evidence.

Does not prove process custody, ordinary play, historical saves or release readiness.
The log checker uses a temporary exact copy; no profile or source file is written.
"""

from __future__ import annotations

import base64
from datetime import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
import scenario_profile as profile
from personas import persona_matrix

TOOLS = Path(__file__).resolve().parent
_SPEC = importlib.util.spec_from_file_location(
    "quickstart_safe_files", TOOLS / "prepare-scenario-load.py"
)
files = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(files)
require = files.require
SNAPSHOT_PREFIX = "taf-quickstart-save-v1:"
OBSERVED = (
    "actual founded heart, finite single grants and advisor verified after GAMESTARTING"
)
SAVE_COMPLETE = (
    "real-save=true; exact-owner-and-stock=true; no-bootstrap-replay=true; "
    "cold-load=false; ordinary-acceptance=false"
)
PREACTIVATION = "exact-saved-heart-stock-and-IDs=true; before-player-GameRestored-and-activation=true"
LOAD_COMPLETE = (
    "real-save-owned-stop-cold-load=true; graceful-quit=false; unchanged-heart-stock-and-IDs=true"
    "; bootstrap-replay=false; ordinary-acceptance=false"
)
AUTOSTART = (
    "AUTOSTART",
    "OK",
    "sealed script present; test game started without input",
)


def fire_cost_drams() -> int:
    """Derives the "fire" design's exact water cost (CostDrams) from the live catalogue XML,
    rather than pinning a second copy of the number that could silently drift from production:
    RuntimeData/KingdomBuildings.xml Key="fire" Cost="..."."""
    import xml.etree.ElementTree as ElementTree

    path = TOOLS.parent / "RuntimeData" / "KingdomBuildings.xml"
    tree = ElementTree.parse(path)
    for building in tree.getroot().findall("building"):
        if building.get("Key") == "fire":
            return int(building.get("Cost"))
    raise ValueError('no "fire" building entry found in ' + str(path))


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON property")
        result[key] = value
    return result


def journal(
    raw: bytes, phase: str, command: str, seed: str, game_id: str | None
) -> bool:
    text = raw.decode("utf-8").replace("\r\n", "\n")
    require(
        text.endswith("\n") and "\r" not in text, "journal must be LF/CRLF terminated"
    )
    lines = text[:-1].split("\n")
    require(1 <= len(lines) <= 32, "journal row bound exceeded")
    stamps = []
    for line in lines:
        columns = line.split("\t")
        require(
            len(columns) == 4 and all(columns), "journal needs four nonempty columns"
        )
        stamp, verb, outcome, message = columns
        require(
            re.fullmatch(
                r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z",
                stamp,
            ),
            "journal timestamp is not exact UTC milliseconds",
        )
        stamps.append(datetime.strptime(stamp, "%Y-%m-%dT%H:%M:%S.%fZ"))
        require(re.fullmatch(r"[A-Z][A-Z0-9-]{0,127}", verb), "invalid machine verb")
        require(
            outcome == "OK" or (phase == "build" and outcome == "REFUSED"),
            "journal contains REFUSED or an unknown outcome",
        )
        require(
            len(message) <= 32768
            and re.fullmatch(r"(?:[^\\\x00-\x1f\x7f]|\\[\\nrt])*", message),
            "invalid journal message escaping or bound",
        )
        require(
            len(persona_matrix.unescape(message).encode("utf-16-le")) // 2 <= 8192,
            "decoded journal message exceeds native bound",
        )
    require(stamps == sorted(stamps), "journal timestamps run backwards")
    rows = persona_matrix.read_journal(text)
    autostart = phase != "load" and rows[0] == AUTOSTART
    if autostart:
        rows = rows[1:]
    expected = [
        (
            "QUICKSTART-BOOT-BEGIN",
            command + "; seed=" + seed + "; genuine-production-boot=true",
        ),
        ("QUICKSTART-BOOT-OBSERVED", OBSERVED),
        (
            "QUICKSTART-BOOT-COMPLETE",
            command + "; boot-only=true; save-load=false; ordinary-acceptance=false",
        ),
    ]
    if phase == "save":
        expected += [
            (
                "QUICKSTART-SAVE-BEGIN",
                "genuine-world=true; game-id=" + game_id + "; synthetic-state=false",
            ),
            ("QUICKSTART-SAVE-COMPLETE", SAVE_COMPLETE),
        ]
    elif phase == "load":
        expected = [
            (
                "LOAD-BEGIN",
                "exact sealed save; game-id="
                + game_id
                + "; new-game=false; mod-restore=false",
            ),
            ("QUICKSTART-LOAD-PREACTIVATION", PREACTIVATION),
            ("QUICKSTART-LOAD-COMPLETE", LOAD_COMPLETE),
        ]
    elif phase == "build":
        # Fixed boot rows (already in `expected`), a fixed BUILD-BEGIN row, then zero or more of
        # the three production steps IN ORDER (each attempted step but the last must have
        # succeeded; only the last-attempted one may be the refused one), then exactly one
        # terminal COMPLETE row.
        expected += [
            (
                "QUICKSTART-BUILD-BEGIN",
                command + "; boot-only=false; genuine-production-commission=true",
            )
        ]
        require(
            len(rows) >= len(expected) + 2,
            "build journal must carry the boot rows, a BEGIN row and a terminal row",
        )
        # The outcome column is REQUIRED here, not dropped: a REFUSED boot row must never be
        # accepted just because its verb/message text happens to match (the per-row loop above
        # admits REFUSED for phase=="build" generally, to allow a refused BUILD row later, so
        # this specific comparison is what actually forecloses a refused BOOT row).
        require(
            [(verb, ok, message) for verb, ok, message in rows[: len(expected) - 1]]
            == [(verb, "OK", message) for verb, message in expected[:-1]],
            "journal has missing, duplicate, out-of-order, mixed-phase, contradictory or refused boot evidence",
        )
        begin_verb, begin_ok, begin_message = rows[len(expected) - 1]
        require(
            (begin_verb, begin_ok, begin_message)
            == (expected[-1][0], "OK", expected[-1][1]),
            "build journal's BEGIN row does not match the exact expected disclosure",
        )
        middle = rows[len(expected) : -1]
        step_verbs = [
            "QUICKSTART-BUILD-QUOTE",
            "QUICKSTART-BUILD-CANPAY",
            "QUICKSTART-BUILD-COMMISSION",
        ]
        require(
            len(middle) <= len(step_verbs),
            "build journal carries more step rows than the sequence defines",
        )
        require(
            [verb for verb, _, _ in middle] == step_verbs[: len(middle)],
            "build journal's step rows are out of order, duplicated, or torn",
        )
        for verb, ok, _ in middle[:-1]:
            require(
                ok == "OK",
                "an earlier build step row is refused; only the last-attempted step may fail",
            )
        terminal_verb, terminal_ok, terminal_message = rows[-1]
        require(
            terminal_verb == "QUICKSTART-BUILD-COMPLETE",
            "build journal's terminal row has the wrong verb",
        )
        success = command + (
            "; commissioned=true; timber-debit=exact; water-debit=exact"
            "; job-projected=true; survey-scope-clear=True; boot-only=false; build-refused=false"
        )
        step_names = {
            "QUICKSTART-BUILD-QUOTE": "quote",
            "QUICKSTART-BUILD-CANPAY": "canpay",
            "QUICKSTART-BUILD-COMMISSION": "commission",
        }
        all_steps_ok = len(middle) == len(step_verbs) and middle[-1][1] == "OK"
        if terminal_ok == "OK":
            require(
                all_steps_ok,
                "a full build success requires all three step rows, each OK",
            )
            expected_steps = [
                ("QUICKSTART-BUILD-QUOTE", "OK",
                 "boot-only=false; waterDrams=" + str(fire_cost_drams())),
                ("QUICKSTART-BUILD-CANPAY", "OK", "boot-only=false; blocked=false"),
                ("QUICKSTART-BUILD-COMMISSION", "OK", "boot-only=false; commissioned=true"),
            ]
            require(
                middle == expected_steps,
                "a full build success requires the exact disclosed content on all three step rows",
            )
            require(
                terminal_message == success,
                "build success row does not match the exact expected disclosure",
            )
        elif all_steps_ok:
            # All three production steps genuinely succeeded, but a live re-check immediately
            # before the success message (after the five census-after reads) found a leaked
            # survey scope: this is its own distinct attributed step, "post-success", never
            # folded into the last step row's own name.
            refused_prefix = (
                command
                + "; build-refused=true; boot-only=false; step=post-success; survey-scope-clear="
            )
            require(
                terminal_message.startswith(refused_prefix + "True; refusal=")
                or terminal_message.startswith(refused_prefix + "False; refusal="),
                "a post-success scope leak refusal does not attribute the exact post-success step",
            )
            require(
                terminal_message.rsplit("; refusal=", 1)[1] != "",
                "build refusal row carries no founder-facing reason",
            )
        else:
            require(
                len(middle) >= 1 and middle[-1][1] != "OK",
                "a build refusal requires the last-attempted step row to itself be refused",
            )
            expected_step = step_names[middle[-1][0]]
            refused_prefix = (
                command
                + "; build-refused=true; boot-only=false; step="
                + expected_step
                + "; survey-scope-clear="
            )
            require(
                terminal_message.startswith(refused_prefix + "True; refusal=")
                or terminal_message.startswith(refused_prefix + "False; refusal="),
                "build refusal row does not attribute the exact failed step",
            )
            require(
                terminal_message.rsplit("; refusal=", 1)[1] != "",
                "build refusal row carries no founder-facing reason",
            )
        # Every branch above is a DIAGNOSTIC: it explains a malformed or misattributed refusal
        # precisely. This final gate is the ACCEPTANCE gate: a well-formed, correctly-attributed
        # REFUSED build (or a well-formed post-success scope-leak refusal) is still NEVER
        # acceptance. journal() must raise here, exactly like every structural defect above --
        # a refused build must never let verify() reach its own PASS verdict.
        require(terminal_ok == "OK",
                "the build was refused (" + terminal_message + "); a refused build, however "
                "well-formed its journal, must never verify PASS")
        return autostart
    require(
        [(verb, message) for verb, _, message in rows] == expected,
        "journal has missing, duplicate, out-of-order, mixed-phase or contradictory evidence",
    )
    return autostart


def snapshot_identity(raw: bytes) -> tuple:
    """Validate framing and identity only; nested native receipts remain opaque witness text."""
    wire = raw.decode("utf-8")
    require(
        len(wire) <= 2097152 and wire.startswith(SNAPSHOT_PREFIX),
        "not a bounded Quickstart snapshot",
    )
    payload = wire[len(SNAPSHOT_PREFIX) :]
    data = base64.b64decode(payload, validate=True)
    require(
        base64.b64encode(data).decode("ascii") == payload,
        "noncanonical snapshot base64",
    )
    at = 0

    def number(fmt):
        nonlocal at
        size = struct.calcsize(fmt)
        require(at + size <= len(data), "truncated snapshot number")
        value = struct.unpack_from(fmt, data, at)[0]
        at += size
        return value

    def text(maximum, optional=False):
        nonlocal at
        length = number("<i")
        if length == -1 and optional:
            return None
        require(
            0 <= length <= maximum * 4 and at + length <= len(data),
            "snapshot text length refused",
        )
        value = data[at : at + length].decode("utf-8")
        at += length
        require(
            (value or optional)
            and len(value.encode("utf-16-le")) // 2 <= maximum
            and not any(ord(c) < 32 or 127 <= ord(c) <= 159 for c in value),
            "snapshot text refused",
        )
        return value

    require(
        number("<i") == 0x31535154 and number("<i") == 1,
        "snapshot magic/version differs",
    )
    game_id, seed, selected = text(36), text(97), text(6)
    advisor = number("B")
    require(
        files.GUID.fullmatch(game_id) and advisor in (0, 1) and number("<i") > 0,
        "snapshot identity refused",
    )
    profile.validate_seed(seed)
    require(selected in profile.QUICKSTART_PROFILES, "snapshot profile refused")
    text(512, True)
    require(all(number("<q") >= 0 for _ in range(4)), "negative snapshot clock")
    text(4096)
    text(524288)
    require(re.fullmatch(r"hs1-[0-9a-f]{64}", text(68)), "snapshot heart seal differs")
    text(131072, True)
    text(65536)
    require(at == len(data), "snapshot trailing bytes")
    return game_id, seed, selected, "yes" if advisor else "no"


def verify_log(raw: bytes) -> None:
    require(raw, "Player.log is empty")
    environment = os.environ.copy()
    environment["TAF_LOG_ALLOW"] = ""
    with tempfile.TemporaryDirectory(prefix="taf-quickstart-log.") as scratch:
        path = Path(scratch) / "Player.log"
        path.write_bytes(raw)
        checked = subprocess.run(
            ["bash", str(TOOLS / "check-player-log.sh"), str(path)],
            env=environment,
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
        )
    require(
        checked.returncode == 0, "strict Player.log checker refused (no allowances)"
    )


def verify(root: Path, phase: str) -> dict:
    require(
        phase in ("boot", "save", "load", "build")
        and root.is_absolute()
        and files.ROOT_NAME.fullmatch(root.name),
        "expected absolute taf-scenario.<alnum> root and boot/save/load/build phase",
    )
    files.directory(root)
    local, seal = root / "Local", Path(str(root) + ".seal")
    frozen = {}

    def read(path, maximum):
        data = files.read_bytes(path, maximum)
        require(data, "empty evidence: " + path.name)
        frozen[path] = (maximum, data, files.stamp(files.file_status(path, maximum)))
        return data

    seal_path = seal / "profile.sha256"
    seal_raw = read(seal_path, 4 * 1024 * 1024)
    expected = profile.read_seal(str(seal_path))
    canonical = (
        profile.SEAL_HEADER
        + "\n"
        + "".join(expected[key] + "  " + key + "\n" for key in sorted(expected))
    )
    require(
        seal_raw.decode("utf-8").replace("\r\n", "\n") == canonical,
        "seal parser did not observe the exact captured canonical seal",
    )
    files.tree_files(local, files.MAX_LOCAL_FILE)
    require(profile.inventory(str(local)) == expected, "Local differs from closed seal")
    script = read(local / "scenario-script.txt", 65536).decode("utf-8")
    commands = [
        line for line in script.splitlines() if line and not line.startswith("#")
    ]
    require(len(commands) == 1, "Quickstart script must contain one exact command")
    command = commands[0]
    tokens = command.split(" ")
    advisor = profile.parse_quickstart_command(tokens)
    require(
        tokens[0]
        == {"boot": "quickstart-boot", "build": "quickstart-build"}.get(
            phase, "quickstart-save"
        ),
        "script/phase mismatch",
    )
    options = json.loads(
        read(local / "PlayerOptions.json", 1048576), object_pairs_hook=unique_object
    )
    require(
        isinstance(options, dict)
        and options.get(profile.QUICKSTART_ADVISOR_OPTION) == advisor,
        "sealed script and PlayerOptions advisor disagree",
    )
    request = files.request_text(read(seal / "request.txt", 1024))
    seed = request.rsplit(";seed=", 1)[1]
    embark = read(
        local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml", files.MAX_LOCAL_FILE
    ).decode("utf-8")
    marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
    require(
        embark.count(marker) == 1
        and embark.split(marker)[1].split('"', 1)[0] == request,
        "frozen seed request differs from sealed embark descriptor",
    )
    game_id, hashes, save = None, {}, None
    load_path = local / "scenario-load.txt"
    require(
        phase == "load"
        or not any(
            os.path.lexists(local / name)
            for name in ("scenario-load.txt", "scenario-load-snapshot.txt")
        ),
        "non-load profile contains a load request",
    )
    if phase not in ("boot", "build"):
        receipt_path = (
            load_path if phase == "load" else root / "scenario-save-receipt.txt"
        )
        receipt = read(receipt_path, 512).decode("ascii").split("\n")
        count, header = (
            (7, "taf-scenario-load-v1")
            if phase == "load"
            else (6, "taf-scenario-save-v1")
        )
        require(
            len(receipt) == count
            and receipt[-1] == ""
            and receipt[0] == header
            and files.GUID.fullmatch(receipt[1])
            and all(files.SHA.fullmatch(s) for s in receipt[2:-1]),
            "malformed exact save/load receipt",
        )
        game_id = receipt[1]
        snapshot_path = (
            local / "scenario-load-snapshot.txt"
            if phase == "load"
            else root / "scenario-save-snapshot.txt"
        )
        raw = read(snapshot_path, 2097152)
        require(
            hashlib.sha256(raw).hexdigest() == receipt[-2], "snapshot hash mismatch"
        )
        require(
            snapshot_identity(raw) == (game_id, seed, tokens[1], tokens[2]),
            "snapshot/selection identity mismatch",
        )
        saves = root / "Synced/Saves"
        files.directory(saves)
        require(
            sorted(p.name for p in saves.iterdir()) == [game_id],
            "foreign or missing save directory",
        )
        save = saves / game_id
        files.directory(save)
        require(
            sorted(p.name for p in save.iterdir()) == sorted(files.SAVE_FILES),
            "save artifact inventory differs",
        )
        for name in files.SAVE_FILES:
            path = save / name
            require(files.file_status(path).st_size > 0, "empty save artifact")
            hashes[name] = (files.digest(path), files.stamp(files.file_status(path)))
        require(
            hashes[files.SAVE_FILES[0]][0] == receipt[2]
            and hashes[files.SAVE_FILES[1]][0] == receipt[3],
            "primary/info hash mismatch",
        )
        require(
            phase != "load" or hashes["Cache.db"][0] == receipt[4],
            "sealed cache hash mismatch",
        )
    journal_raw = read(root / "scenario-journal.tsv", 1048576)
    log_raw = read(root / "Player.log", 64 * 1024 * 1024)
    autostart = journal(journal_raw, phase, command, seed, game_id)
    verify_log(log_raw)
    files.tree_files(local, files.MAX_LOCAL_FILE)
    require(profile.inventory(str(local)) == expected, "Local changed during verdict")
    for path, (maximum, before, stamp) in frozen.items():
        require(
            files.read_bytes(path, maximum) == before
            and files.stamp(files.file_status(path, maximum)) == stamp,
            "evidence changed during verdict: " + path.name,
        )
    if save is not None:
        require(
            sorted(p.name for p in save.parent.iterdir()) == [game_id]
            and sorted(p.name for p in save.iterdir()) == sorted(files.SAVE_FILES),
            "save inventory changed",
        )
        for name, (digest, stamp) in hashes.items():
            require(
                files.digest(save / name) == digest
                and files.stamp(files.file_status(save / name)) == stamp,
                "save artifact changed during verdict",
            )
    return {
        "verdict": "PASS",
        "phase": phase,
        "command": command,
        "seed": seed,
        "gameId": game_id,
        "journalSHA256": hashlib.sha256(journal_raw).hexdigest(),
        "playerLogSHA256": hashlib.sha256(log_raw).hexdigest(),
        "saveHashes": {name: digest for name, (digest, _) in hashes.items()},
        "scope": "sealed-developer-quickstart-evidence",
        "snapshotSemantics": "nested-native-witness-not-reproved",
        "autostartObserved": autostart,
        "cacheReceiptBound": phase == "load",
        "processAuthority": False,
        "ordinaryAcceptance": False,
        "historicalSaveCompatibility": False,
        "releaseAcceptance": False,
    }


def main(argv: list[str]) -> int:
    try:
        require(
            len(argv) == 4 and argv[2] == "--phase",
            "usage: check-quickstart-results.py ROOT --phase boot|save|load|build",
        )
        result = verify(Path(argv[1]), argv[3])
    except (OSError, ValueError, SystemExit, subprocess.SubprocessError) as error:
        print(
            json.dumps(
                {
                    "verdict": "REFUSED",
                    "reason": str(error),
                    "ordinaryAcceptance": False,
                    "releaseAcceptance": False,
                },
                ensure_ascii=True,
            )
        )
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
