"""Strict host bindings for unattended native source receipts; never mint save state."""
from __future__ import annotations

from datetime import datetime
import json
import os
from pathlib import Path
import re

from upgrade_profile_inputs import OLD_PIN, SHA, require, sha

# Any diagnostic-shaped line at all -- ours or a third party's. Deciding what gets RETAINED (never
# fatal on its own); TAF_DIAGNOSTIC below is what may actually refuse a run.
ANY_DIAGNOSTIC = re.compile(rb"\b(?:MODWARN|MODERROR|WARN(?:ING)?|ERROR|FATAL|REFUSED)\b|\b(?:[\w.]+)?Exception\b"
                            rb"|^\s*(?:at\s|---)", re.IGNORECASE)

# Same TAF-only failure contract Tools/check-player-log.sh enforces for every smoke/persona run:
# only a MODERROR/MODWARN line naming The Thousand and First, or an exception/stack frame naming
# it, ever refuses a native run. A third party's own MODWARN (for example the installed Pets of
# Harvest Dawn pack's manual-load-order warning, emitted at mod DISCOVERY -- before this profile's
# ModSettings.json Enabled flag for that pack can gate anything, see upgrade_profile_inputs.py) is
# not ours to refuse a run over.
TAF_DIAGNOSTIC = re.compile(
    rb"^MOD(?:ERROR|WARN) \[The Thousand and First(?: \[ALPHA\])?(?: \[DEV SCENARIO HARNESS\])?\](?:[ \t]|$)"
    rb"|(?i:(?=.*(?:\[taf\]|thousandandfirst|the thousand and first))"
    rb"(?=.*(?:exception|error|fault|quarantin|inspection required)))"
    rb"|(?i:^[ \t]*(?:at|---).*thousandandfirst[.:])")


# A retained non-TAF diagnostic line is decoded (UTF-8, replacement on error) and cut to this many
# characters before being folded into the caller's report -- it is not kept verbatim.
RETAINED_LINE_MAX_CHARS = 500


def diagnostics(raw: bytes) -> list[str]:
    """Enforce the TAF-only contract over a whole Player.log; return retained non-TAF lines.

    Raises on the first TAF-tagged MODERROR/MODWARN or TAF exception/stack frame. Any other
    diagnostic-shaped line (a third party's own MODWARN/MODERROR/WARN/ERROR/Exception) is
    collected here -- decoded (UTF-8, replacement on error) and truncated to
    RETAINED_LINE_MAX_CHARS characters, not verbatim -- for the caller to fold into its report;
    it is never fatal. A line matching neither diagnostic pattern is never decoded.
    """
    retained = []
    for line in raw.replace(b"\r\n", b"\n").split(b"\n"):
        is_taf = TAF_DIAGNOSTIC.search(line)
        if not (is_taf or ANY_DIAGNOSTIC.search(line)):
            continue
        text = line.decode("utf-8", errors="replace")[:RETAINED_LINE_MAX_CHARS]
        require(not is_taf, "native log reported a Thousand and First diagnostic: " + text)
        retained.append(text)
    return retained


def source_log(source: Path) -> str:
    from upgrade_profile_state import fs
    raw = fs.read_bytes(source / "Player.log", 64 * 1024**2)
    require(bool(raw), "native source log is missing or empty")
    diagnostics(raw)
    return sha(raw)


def _lines(source: Path, name: str, header: str, count: int) -> tuple[bytes, list[str]]:
    from upgrade_profile_state import fs
    raw = fs.read_bytes(source / name, 4096)
    lines = raw.decode("ascii").split("\n")
    require(len(lines) == count + 1 and lines[-1] == "" and lines[0] == header
            and b"\r" not in raw, "native source receipt framing differs: " + name)
    return raw, lines[:-1]


def script_journal(source: Path, mode: str) -> str:
    """Complete native AutoRunner transcript, not a success-shaped receipt alone."""
    from upgrade_profile_state import fs
    raw = fs.read_bytes(source / "scenario-journal.tsv", 1024**2)
    text = raw.decode("utf-8").replace("\r\n", "\n")
    require(text.endswith("\n") and "\r" not in text, "source journal framing differs")
    rows = [line.split("\t") for line in text[:-1].split("\n")]
    require(rows and all(len(row) == 4 and row[2] == "OK" for row in rows),
            "source script is refused, incomplete or malformed")
    for row in rows:
        require(re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z", row[0]),
                "source journal timestamp is not canonical")
        datetime.strptime(row[0], "%Y-%m-%dT%H:%M:%S.%fZ")
    verbs = {"source-donor": ["stagedigest", "upgrade-source-donor", "stagedigest"],
             "source": ["stagedigest", "upgrade-source-reserved", "stagedigest"],
             "stage-source": ["stagedigest", "upgrade-stage-setup", "advance",
                              "upgrade-stage-save", "stagedigest"]}[mode]
    progress = [row for row in rows if row[1] in ("advance-progress", "advance-complete")]
    if mode == "stage-source":
        require(sum(row[1] == "advance-complete" for row in progress) == 1,
                "stage source lacks one real completed advance")
        advance_index = next((i for i, row in enumerate(rows) if row[1] == "advance"), -1)
        save_index = next((i for i, row in enumerate(rows) if row[1] == "upgrade-stage-save"), -1)
        require(advance_index >= 0 and save_index > advance_index
                and all(advance_index < i < save_index for i, row in enumerate(rows)
                        if row[1] in ("advance-progress", "advance-complete")),
                "stage progress escaped its actual advance")
    else:
        require(not progress, "source script unexpectedly advanced the world")
    ordinary = [row for row in rows if row[1] not in ("advance-progress", "advance-complete")]
    prefix = ["AUTOSTART"]
    if len(ordinary) > 1 and ordinary[1][1] == "TESTGROUND-BUILT":
        prefix.append("TESTGROUND-BUILT")
    prefix.append("TESTGROUND-RESTRIP")
    require([row[1] for row in ordinary] == [*prefix, "RUNNER-ARMED", "SCRIPT-BEGIN", *verbs, "SCRIPT-COMPLETE"],
            "source script has missing, repeated, foreign or reordered verbs")
    require(ordinary[-1][3] == str(len(verbs)) + " verb(s) ran without a refusal",
            "source script terminal count differs")
    require("popups suppressed from " in ordinary[len(prefix)][3], "source boot required attended input")
    return sha(raw)


def _saved(source: Path, state: dict, game_id: str, primary: str, info_hash: str) -> str:
    from upgrade_profile_state import fs
    rows = {row["path"]: row for row in state["files"]}
    for leaf, digest in (("Primary.sav.gz", primary), ("Primary.json", info_hash)):
        row = rows.get("Synced/Saves/" + game_id + "/" + leaf)
        require(row is not None and row["size"] > 0 and row["sha256"] == digest,
                "native source save changed: " + leaf)
    info = json.loads(fs.read_bytes(source / "Synced/Saves" / game_id / "Primary.json", 4 * 1024**2))
    require(info.get("ID") == game_id and info.get("SaveVersion") == 408
            and info.get("GameVersion") == "2.0.211.51"
            and isinstance(info.get("ModsEnabled"), list)
            and "r_ThousandAndFirst" in info["ModsEnabled"]
            and set(info["ModsEnabled"]) <= {"r_ThousandAndFirst", "FreeholdGames_DLC_PetsPack1"},
            "native source save has foreign engine, game or mods")
    cache = rows.get("Synced/Saves/" + game_id + "/Cache.db")
    require(cache is not None and cache["size"] > 0, "native source post-quit cache is missing")
    return cache["sha256"]


def _capture(source: Path, config: dict, state: dict, donor: bool) -> dict:
    from upgrade_profile_state import GUID, ID
    mode, kind = ("source-donor", "donor") if donor else ("stage-source", "stage")
    require(config["schema"] == "taf-upgrade-profile-v2" and config["mode"] == mode,
            "native source receipt belongs to another recipe/mode")
    require(not os.path.lexists(source / ("upgrade-" + kind + "-failure.txt")),
            "native " + kind + " source explicitly refused")
    raw, values = _lines(source, "upgrade-" + kind + "-receipt.txt",
                         "taf-upgrade-" + kind + "-receipt-v1", 12 if donor else 11)
    require(values[1] == (OLD_PIN if donor else config["runtime"])
            and GUID.fullmatch(values[2]) and values[3] == values[2]
            and all(re.fullmatch(ID, value) for value in values[3:6])
            and re.fullmatch(r"0|[1-9][0-9]{0,9}", values[6])
            and int(values[6]) <= 1024 and values[-1] == "cache-bind-after-quit"
            and all(SHA.fullmatch(value) for value in values[7:-1]),
            "native " + kind + " source identities or hashes differ")
    rows = {row["path"]: row for row in state["files"]}
    stages = [rows.get("Synced/ThousandAndFirst/Stages/" + values[3] + "." + slot + ".seal")
              for slot in "ab"]
    require(any(row is not None and row["size"] > 0 and row["sha256"] == values[7] for row in stages),
            "native source stage is not among retained canonical slot bytes")
    if donor:
        legacy = rows.get("Synced/ThousandAndFirst/Legacies/" + values[4] + ".seal")
        require(legacy is not None and legacy["size"] > 0 and legacy["sha256"] == values[8],
                "native promoted legacy is absent or changed")
    primary, info = values[-3:-1]
    cache = _saved(source, state, values[2], primary, info)
    witness = dict(gameId=values[2], origin=values[3], legacyId=values[4], lineageId=values[5],
                   generation=int(values[6]), stageSha256=values[7], primarySha256=primary,
                   infoSha256=info, cacheSha256=cache, receiptSha256=sha(raw),
                   journalSha256=script_journal(source, mode), logSha256=source_log(source))
    if donor:
        witness["legacySha256"] = values[8]
    return witness


def capture_donor(source: Path, config: dict, state: dict) -> dict:
    return _capture(source, config, state, True)


def capture_stage(source: Path, config: dict, state: dict) -> dict:
    return _capture(source, config, state, False)


def source_link(source: Path, config: dict, state: dict, game_id: str,
                snapshot_hash: str, receipt_hash: str) -> dict:
    require(config["case"] == "inheritance" and isinstance(state.get("donorWitness"), dict),
            "unattended source lacks authenticated donor authority")
    require(not os.path.lexists(source / "upgrade-source-failure.txt"), "native source driver refused")
    donor = state["donorWitness"]
    raw, lines = _lines(source, "upgrade-source-link.txt", "taf-upgrade-source-link-v1", 13)
    expected = ["taf-upgrade-source-link-v1", OLD_PIN, game_id, donor["gameId"], donor["origin"],
                donor["legacyId"], donor["lineageId"], str(donor["generation"]), donor["stageSha256"],
                donor["legacySha256"], donor["receiptSha256"], snapshot_hash, receipt_hash]
    require(game_id != donor["gameId"] and lines == expected,
            "native Reserved source is not the exact retained donor/new-game/capture chain")
    return dict(linkSha256=sha(raw), journalSha256=script_journal(source, "source"),
                logSha256=source_log(source),
                donorReceiptSha256=donor["receiptSha256"])
