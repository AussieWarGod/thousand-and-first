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

import scenario_profile
from personas import persona_matrix

OLD_PIN = "a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e"
PIN = re.compile(r"[0-9a-f]{40}\Z")
SHA = re.compile(r"[0-9a-f]{64}\Z")
# V1 recipes retain their exact original overlay sets for approved historical sources.
SOURCE_PROBES = (
    "KingdomUpgradeSnapshot.cs", "KingdomUpgradeSnapshotCodec.cs", "KingdomUpgradeGraph.cs",
    "KingdomUpgradeState.cs", "KingdomUpgradeFiles.cs", "KingdomUpgradeSource.cs",
    "KingdomUpgradeSourcePatches.cs",
)
DOWNGRADE_PROBES = (
    "KingdomDowngradeProbe.cs", "KingdomDowngradeRequest.cs", "KingdomDowngradeFiles.cs",
)
SOURCE_DRIVERS = ("KingdomUpgradeSourceProvider.cs", "KingdomUpgradeSourceDriver.cs")
PROFILE_V1, PROFILE_V2 = "taf-upgrade-profile-v1", "taf-upgrade-profile-v2"
PERSONAS = {
    "source-donor": "upgrade-source-donor", "source": "upgrade-source-reserved",
    "stage-source": "upgrade-stage-source", "downgrade": "upgrade-downgrade-check",
}
DONOR_FIELDS = ("gameId", "origin", "legacyId", "lineageId", "generation", "stageSha256",
                "legacySha256", "primarySha256", "infoSha256", "receiptSha256")
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


def configuration(mode: str, runtime: str, probe: str, case: str, seed: str,
                  *, schema: str = PROFILE_V2, donor: dict | None = None) -> dict:
    require(schema in (PROFILE_V1, PROFILE_V2), "unknown profile recipe")
    modes = ("source", "stage-source", "upgrade", "downgrade")
    require(mode in modes or schema == PROFILE_V2 and mode == "source-donor", "unknown profile mode")
    require(PIN.fullmatch(runtime) and PIN.fullmatch(probe), "noncanonical profile pins")
    require(case in ("inheritance", "detached-transition", "reader"), "unknown case")
    require(re.fullmatch(r"#(?:0|[1-9][0-9]*)", seed) and int(seed[1:]) <= 2147483647,
            "seed must be a canonical #Int32")
    require((mode in ("downgrade", "stage-source")) == (case == "reader"), "case does not match profile mode")
    require(mode not in ("source", "source-donor", "downgrade") or runtime == OLD_PIN,
            "source/reader runtime must be tag v0.3.1")
    require(mode not in ("upgrade", "stage-source") or runtime == probe,
            "current runtime and probes must share one commit")
    value = dict(schema=schema, mode=mode, runtime=runtime, probe=probe, case=case, seed=seed)
    if schema == PROFILE_V1:
        require(donor is None, "historical attended recipe has no donor descriptor")
        return value
    if mode in ("source", "source-donor"):
        require(case == "inheritance", "unattended detached-transition source is not implemented")
    if mode == "source":
        require(isinstance(donor, dict) and set(donor) == {"root", "probe", "receiptSha256"},
                "inheritor needs exact retained donor provenance")
        require(isinstance(donor["root"], str)
                and re.fullmatch(r"/mnt/c/taf-scenario\.[A-Za-z0-9]+", donor["root"])
                and isinstance(donor["probe"], str) and PIN.fullmatch(donor["probe"])
                and isinstance(donor["receiptSha256"], str) and SHA.fullmatch(donor["receiptSha256"]),
                "donor provenance is not canonical")
    else:
        require(donor is None, "only an inheritor may name a donor")
    value["donor"] = dict(donor) if donor is not None else None
    return value


def parse_config(raw: bytes) -> dict:
    value = json.loads(raw)
    require(isinstance(value, dict), "malformed profile provenance")
    keys = {"schema", "mode", "runtime", "probe", "case", "seed"}
    if value.get("schema") == PROFILE_V2:
        keys.add("donor")
    require(set(value) == keys, "malformed profile provenance")
    expected = configuration(value["mode"], value["runtime"], value["probe"], value["case"], value["seed"],
                             schema=value["schema"], donor=value.get("donor"))
    require(value == expected and raw == json_bytes(expected), "noncanonical profile provenance")
    return expected


def recipe(repo: Path, config: dict, probe: Commit | None = None) -> tuple[bytes, dict] | None:
    """Only fixed candidate persona blobs; no caller-supplied recipe or environment verbs."""
    if config["schema"] == PROFILE_V1 or config["mode"] == "upgrade":
        return None
    name = "cross-version/" + PERSONAS[config["mode"]] + ".persona"
    raw = (probe or Commit(repo, config["probe"])).blob("Tools/personas/" + name)
    require(len(raw) <= 65536, "persona exceeds script input bound")
    try:
        parsed = persona_matrix.parse_manifest(raw.decode("utf-8"), name)
        extra = scenario_profile.parse_extra_verbs(parsed["VERBS"])
        parsed["LINES"] = scenario_profile.parse_script(parsed["SCRIPT_WORDS"].split(), extra)
        require(0 < len(parsed["LINES"]) <= 32, "persona script is empty or oversized")
        require(re.fullmatch(r"[a-z0-9][a-z0-9-]*(?:;facing=(?:north|south|east|west))?",
                             parsed["REQUEST"]), "persona request is not canonical")
        if "START" in parsed:
            parsed["LOCATION"] = scenario_profile.parse_start(parsed["START"])
        else:
            require(config["mode"] == "downgrade", "world-producing persona needs an exact start")
    except SystemExit as error:
        raise ValueError("pinned persona refused: " + str(error)) from error
    return raw, parsed


def recipe_request(repo: Path, config: dict) -> str:
    selected = recipe(repo, config)
    # The persona's REQUEST is used VERBATIM, exactly as Tools/run-personas.sh uses it. Every
    # declared scenario parameter is required and no undeclared one is tolerated
    # (Harness/KingdomScenarioRules.cs TryBind), so a facing this recipe adds on its own is a
    # boot refusal for any scenario that declares none: `founding-first-city` refused with
    # "Scenario founding-first-city declares no parameter 'facing'." on the first native donor
    # run. Personas that need a facing carry it themselves, as the arch-* matrix already does.
    return (selected[1]["REQUEST"] if selected else "arch-gallery-slice;facing=north") \
        + ";seed=" + config["seed"]


def donor_wire(config: dict, witness: dict | None) -> bytes:
    proof_hashes = ("cacheSha256", "journalSha256", "logSha256")
    require(isinstance(witness, dict) and set(witness) == set(DONOR_FIELDS + proof_hashes),
            "native donor witness missing or malformed")
    require(isinstance(witness["gameId"], str) and re.fullmatch(
        r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}", witness["gameId"]), "donor game ID is not canonical")
    for name in ("origin", "legacyId", "lineageId"):
        require(isinstance(witness[name], str) and re.fullmatch(r"[A-Za-z0-9_-]{1,96}", witness[name]),
                "donor identity is not canonical: " + name)
    require(type(witness["generation"]) is int and 0 <= witness["generation"] <= 1024,
            "donor generation is not canonical")
    for name in DONOR_FIELDS[5:] + proof_hashes:
        require(isinstance(witness[name], str) and SHA.fullmatch(witness[name]), "donor digest malformed: " + name)
    require(witness["receiptSha256"] == config["donor"]["receiptSha256"], "donor receipt differs from pinned provenance")
    return ("\n".join(["taf-upgrade-source-donor-v1", OLD_PIN,
                       *(str(witness[name]) for name in DONOR_FIELDS)]) + "\n").encode("ascii")


def local_inputs(repo: Path, config: dict, extra: dict[str, bytes] | None = None,
                 *, donor_witness: dict | None = None) -> dict[str, bytes]:
    config = parse_config(json_bytes(config))
    runtime, probe = Commit(repo, config["runtime"]), Commit(repo, config["probe"])
    production = runtime.runtime()
    harness = runtime.harness()
    if config["mode"] in ("source", "source-donor"):
        overlay = SOURCE_PROBES + (SOURCE_DRIVERS if config["schema"] == PROFILE_V2 else ())
    elif config["mode"] == "downgrade":
        overlay = DOWNGRADE_PROBES + (("KingdomDowngradeScript.cs",) if config["schema"] == PROFILE_V2 else ())
    else:
        overlay = ()  # all current Harness bytes already come from the current production pin
    for name in overlay:
        require(name not in harness, "old runtime already contains a new observer: " + name)
        harness[name] = probe.blob("Harness/" + name)
    selected = recipe(repo, config, probe)
    request = recipe_request(repo, config)
    embark = harness["EmbarkModules.xml"].decode("utf-8")
    marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
    require(embark.count(marker) == 1, "missing/duplicate pinned embark request")
    before, rest = embark.split(marker)
    _, after = rest.split('"', 1)
    harness["EmbarkModules.xml"] = (before + marker + request + '"' + after).encode("utf-8")
    if selected and "LOCATION" in selected[1]:
        embark = harness["EmbarkModules.xml"].decode("utf-8")
        pattern = r'(<[^>]*\bID="TAFTestGround"[^>]*\bLocation=")[^"]*(")'
        embark, replaced = re.subn(pattern, lambda match: match[1] + selected[1]["LOCATION"] + match[2], embark)
        require(replaced == 1, "missing/duplicate pinned test-ground start")
        harness["EmbarkModules.xml"] = embark.encode("utf-8")
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
    if config["schema"] == PROFILE_V2 and config["mode"] in ("source", "source-donor"):
        # The donor births a fresh non-inheriting old realm, so it starts with import off and opts
        # into sealing later. The Reserved inheritor is the opposite: it must be BORN opted in.
        # QudGameBootModule.BootGame runs every IGameStateSingleton.Initialize (which is where
        # KingdomInheritanceState reserves the copied legacy and requires KingdomInheritanceLifecycle)
        # strictly before the [PlayerMutator] step where 0.3.1's KingdomSaveSystemRosterNewGameLoader
        # commits its roster marker. Opting in after that point leaves the marker without the
        # Inheritance bit while the carrier exists, and 0.3.1's own SaveSystems prefix then refuses
        # the save with "UnexpectedMultiplicity [Inheritance: expected 0, observed 1]" (issue #87).
        options["r_TAF_OptionLegacyImport"] = "Yes" if config["mode"] == "source" else "No"
    result["PlayerOptions.json"] = json_bytes(options)
    settings = json.loads(runtime.blob("Tools/smoke/ModSettings.json"))
    require(isinstance(settings, dict), "pinned mod settings not an object")
    # This disables the installed optional Pets pack's CONTENT for the scenario (population
    # tables, etc.) -- it does NOT and cannot suppress the pack's own MODWARN lines. The engine
    # emits those at mod DISCOVERY, before this ModSettings.json Enabled flag is ever read, so a
    # cold load always logs them regardless of this setting. The TAF-only failure contract (see
    # Tools/check-player-log.sh and upgrade_profile_witnesses.diagnostics) is what actually
    # tolerates them: a third-party MODWARN is retained in the report, never fatal. This derived
    # profile setting is independent of the pinned production/Harness byte inventory.
    settings["FreeholdGames_DLC_PetsPack1"] = {"Title": "Pets of Harvest Dawn", "Enabled": False}
    result["ModSettings.json"] = json_bytes(settings)
    result[CONFIG] = json_bytes(config)
    if selected:
        result["upgrade-persona.txt"] = selected[0]
        result["scenario-script.txt"] = ("\n".join(selected[1]["LINES"]) + "\n").encode("utf-8")
    if config["mode"] == "source":
        result["upgrade-save-request.txt"] = (
            "taf-upgrade-save-request-v1\n" + OLD_PIN + "\n" + config["case"] + "\n").encode("ascii")
    if config["mode"] == "stage-source":
        result["upgrade-stage-source.txt"] = ("taf-upgrade-stage-source-v1\n" + config["runtime"] + "\n").encode("ascii")
    if config["mode"] == "upgrade":
        # LoadEntry intercepts Continue before AutoStart. Script only selects the existing owned,
        # unfocused launcher; a falling-through script is NOT upgrade success.
        result["scenario-script.txt"] = b"stagedigest\n"
    if config["schema"] == PROFILE_V2 and config["mode"] == "source":
        result["upgrade-source-donor.txt"] = donor_wire(config, donor_witness)
    else:
        require(donor_witness is None, "this profile cannot consume donor evidence")
    for path, raw in (extra or {}).items():
        require(path not in result and "/" not in path, "profile extra overwrites pinned input")
        result[portable(path)] = raw
    require(len(result) <= 16384 and sum(map(len, result.values())) <= 2 * 1024**3,
            "pinned profile exceeds bounded inventory")
    return result
