"""Pinned Git inputs for isolated cross-version profiles; never copy an old Local tree.

The runtime selector reads the named commit's stage.sh arrays. It does not execute shell text
from a commit, trust a version label, or use the caller's dirty worktree as runtime authority.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import shlex
import subprocess
import unicodedata

OLD_PIN = "a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e"
PIN = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"[0-9a-f]{64}\Z")
SOURCE_PROBES = (
    "KingdomUpgradeSnapshot.cs", "KingdomUpgradeSnapshotCodec.cs", "KingdomUpgradeGraph.cs",
    "KingdomUpgradeState.cs", "KingdomUpgradeFiles.cs", "KingdomUpgradeSource.cs",
    "KingdomUpgradeSourcePatches.cs",
)
DOWNGRADE_PROBES = (
    "KingdomDowngradeProbe.cs", "KingdomDowngradeRequest.cs", "KingdomDowngradeFiles.cs",
)
CONFIG = "upgrade-profile.json"
MAX_BLOB = 32 * 1024 * 1024


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=True, indent=2) + "\n").encode("utf-8")


def portable(path: str) -> str:
    require(path and path == unicodedata.normalize("NFC", path), "noncanonical path")
    for component in path.split("/"):
        require(component not in ("", ".", "..") and not component.endswith((".", " "))
                and not any(c in '<>:"\\|?*' or unicodedata.category(c).startswith("C")
                            for c in component), "unsafe Windows path: " + path)
        stem = component.split(".")[0]
        require(not re.fullmatch(r"CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|COM[1-9¹²³]|LPT[1-9¹²³]",
                                 stem, re.IGNORECASE), "Windows reserved path: " + path)
    return path


class Commit:
    def __init__(self, repo: Path, pin: str):
        require(bool(PIN.fullmatch(pin)), "use a full immutable lowercase commit ID")
        self.repo, self.pin = repo, pin
        require(self.git("rev-parse", pin + "^{commit}").decode().strip() == pin,
                "commit cannot be resolved exactly")
        self.entries: dict[str, tuple[str, str]] = {}
        seen: set[str] = set()
        for row in self.git("ls-tree", "-r", "-z", pin).split(b"\0"):
            if not row:
                continue
            facts, raw_path = row.split(b"\t", 1)
            mode, kind, oid = facts.decode("ascii").split(" ")
            path = portable(raw_path.decode("utf-8"))
            require(path.casefold() not in seen, "case-colliding Git paths")
            seen.add(path.casefold())
            self.entries[path] = (mode if kind == "blob" else kind, oid)

    def git(self, *args: str) -> bytes:
        return subprocess.run(["git", "-C", str(self.repo), *args], check=True,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout

    def blob(self, path: str) -> bytes:
        require(path in self.entries, "pinned input missing: " + path)
        mode, oid = self.entries[path]
        require(mode in ("100644", "100755"), "pinned input is not a regular blob: " + path)
        size = int(self.git("cat-file", "-s", oid))
        require(0 <= size <= MAX_BLOB, "pinned input exceeds bound: " + path)
        raw = self.git("cat-file", "blob", oid)
        require(len(raw) == size, "Git blob length changed")
        return raw

    def runtime(self) -> dict[str, bytes]:
        script = self.blob("Tools/stage.sh").decode("utf-8")
        arrays = {}
        for name in ("EXCLUDE_DIRS", "ROOT_META", "ASSET_DIRS"):
            rows = re.findall(r"^" + name + r"=\(([^\n()]*)\)$", script, re.MULTILINE)
            require(len(rows) == 1, "unrecognized pinned stage selector: " + name)
            values = shlex.split(rows[0])
            require(values and all(re.fullmatch(r"[A-Za-z0-9_.-]+", v) for v in values),
                    "nonliteral pinned stage selector")
            arrays[name] = values
        result = {}
        for path in sorted(self.entries):
            top = path.split("/", 1)[0]
            if (path in arrays["ROOT_META"] or top in arrays["ASSET_DIRS"]
                    or (top not in arrays["EXCLUDE_DIRS"] and path.endswith((".cs", ".xml")))):
                require(top != "Harness", "pinned runtime stages the developer harness")
                result[path] = self.blob(path)
        require("manifest.json" in result and result, "empty pinned runtime")
        return result

    def harness(self) -> dict[str, bytes]:
        return {p[8:]: self.blob(p) for p in sorted(self.entries)
                if p.startswith("Harness/") and p.count("/") == 1 and p.endswith((".cs", ".xml"))}


def configuration(mode: str, runtime: str, probe: str, case: str, seed: str) -> dict:
    require(mode in ("source", "stage-source", "upgrade", "downgrade"), "unknown profile mode")
    require(PIN.fullmatch(runtime) and PIN.fullmatch(probe), "noncanonical profile pins")
    require(case in ("inheritance", "detached-transition", "reader"), "unknown case")
    require(re.fullmatch(r"#(?:0|[1-9][0-9]*)", seed) and int(seed[1:]) <= 2147483647,
            "seed must be a canonical #Int32")
    require((mode in ("downgrade", "stage-source")) == (case == "reader"), "case does not match profile mode")
    require(mode not in ("source", "downgrade") or runtime == OLD_PIN, "source/reader runtime must be tag v0.3.1")
    require(mode not in ("upgrade", "stage-source") or runtime == probe,
            "current runtime and probes must share one commit")
    return dict(schema="taf-upgrade-profile-v1", mode=mode, runtime=runtime, probe=probe,
                case=case, seed=seed)


def parse_config(raw: bytes) -> dict:
    value = json.loads(raw)
    require(isinstance(value, dict) and set(value) ==
            {"schema", "mode", "runtime", "probe", "case", "seed"}, "malformed profile provenance")
    expected = configuration(value["mode"], value["runtime"], value["probe"], value["case"], value["seed"])
    require(value == expected and raw == json_bytes(expected), "noncanonical profile provenance")
    return expected


def local_inputs(repo: Path, config: dict, extra: dict[str, bytes] | None = None) -> dict[str, bytes]:
    runtime, probe = Commit(repo, config["runtime"]), Commit(repo, config["probe"])
    production = runtime.runtime()
    harness = runtime.harness()
    if config["mode"] == "source":
        overlay = SOURCE_PROBES
    elif config["mode"] == "downgrade":
        overlay = DOWNGRADE_PROBES
    else:
        overlay = ()  # all current Harness bytes already come from the current production pin
    for name in overlay:
        require(name not in harness, "old runtime already contains a new observer: " + name)
        harness[name] = probe.blob("Harness/" + name)
    request = "arch-gallery-slice;facing=north;seed=" + config["seed"]
    embark = harness["EmbarkModules.xml"].decode("utf-8")
    marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
    require(embark.count(marker) == 1, "missing/duplicate pinned embark request")
    before, rest = embark.split(marker)
    _, after = rest.split('"', 1)
    harness["EmbarkModules.xml"] = (before + marker + request + '"' + after).encode("utf-8")
    manifest = json.loads(production["manifest.json"])
    rows = manifest.get("Directories")
    require(isinstance(rows, list) and rows and isinstance(rows[0].get("Paths"), list),
            "unrecognized pinned manifest")
    require("Harness" not in production["manifest.json"].decode("utf-8"), "shipped manifest selects Harness")
    rows[0]["Paths"].append("/Harness/")
    manifest["title"] = str(manifest.get("title", "")) + " [DEV SCENARIO HARNESS]"
    production["manifest.json"] = json_bytes(manifest)
    mod = "Mods/ThousandAndFirst/"
    result = {mod + p: raw for p, raw in production.items()}
    result.update({mod + "Harness/" + p: raw for p, raw in harness.items()})
    options = json.loads(runtime.blob("Tools/smoke/PlayerOptions.json"))
    require(isinstance(options, dict), "pinned options not an object")
    options["OptionEnableSeed"] = "Yes"
    result["PlayerOptions.json"] = json_bytes(options)
    settings = json.loads(runtime.blob("Tools/smoke/ModSettings.json"))
    require(isinstance(settings, dict), "pinned mod settings not an object")
    # The installed optional Pets pack emits known MODWARN lines. These deliberately TAF-only
    # fixtures disable it BEFORE sealing rather than filtering diagnostics after the game runs.
    # This derived profile setting is independent of the pinned production/Harness byte inventory.
    settings["FreeholdGames_DLC_PetsPack1"] = {"Title": "Pets of Harvest Dawn", "Enabled": False}
    result["ModSettings.json"] = json_bytes(settings)
    result[CONFIG] = json_bytes(config)
    if config["mode"] == "source":
        result["upgrade-save-request.txt"] = (
            "taf-upgrade-save-request-v1\n" + OLD_PIN + "\n" + config["case"] + "\n").encode("ascii")
    if config["mode"] == "stage-source":
        result["upgrade-stage-source.txt"] = ("taf-upgrade-stage-source-v1\n" + config["runtime"] + "\n").encode("ascii")
    if config["mode"] == "upgrade":
        # LoadEntry intercepts Continue before AutoStart. Script only selects the existing owned,
        # unfocused launcher; a falling-through script is NOT upgrade success.
        result["scenario-script.txt"] = b"stagedigest\n"
    for path, raw in (extra or {}).items():
        require(path not in result and "/" not in path, "profile extra overwrites pinned input")
        result[portable(path)] = raw
    require(len(result) <= 16384 and sum(map(len, result.values())) <= 2 * 1024**3,
            "pinned profile exceeds bounded inventory")
    return result
