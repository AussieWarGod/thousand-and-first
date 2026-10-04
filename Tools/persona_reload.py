"""One cold-process continuation per recipe; never reuse a spent profile or resume its script."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import time

# The unfounded save leg's exact sealed recipe (Tools/personas/persona_matrix.py
# UNFOUNDED_RELOAD_SCRIPT/UNFOUNDED_RELOAD_VERB, Harness/KingdomUnfoundedSave.cs).
UNFOUNDED_SCRIPT = "stagedigest unfounded-save stagedigest"
UNFOUNDED_VERB = "unfounded-save"
START = re.compile(r"(0|[1-9][0-9]?)\.(0|[1-9][0-9]?)@(0|[1-9][0-9]?),(0|[1-9][0-9]?)\Z")
START_BOUNDS = (80, 25, 80, 25)


def require(value, reason):
    if not value:
        raise ValueError(reason)


def valid_start(start) -> bool:
    found = START.match(start) if isinstance(start, str) else None
    return bool(found) and all(int(part) < bound for part, bound in zip(found.groups(), START_BOUNDS))


def execute(backend, location: str, advisor: str) -> dict:
    """Injected backend is a test seam only; production CLI always constructs NativeBackend."""
    require(location in ("marsh", "canyon", "dunes") and advisor in ("yes", "no"),
            "unsupported reload selection")

    def verify(saved, loaded):
        require(saved["command"] == "quickstart-save " + location + " " + advisor,
                "reload evidence belongs to another selection")
        for name in ("Primary.sav.gz", "Primary.json"):
            before = saved.get("saveHashes", {}).get(name)
            require(before and loaded.get("saveHashes", {}).get(name) == before,
                    "reload save bytes changed: " + name)

    return cycle(backend, (location, advisor), verify, "developer-quickstart-cold-reload", {})


def execute_unfounded(backend, start: str) -> dict:
    """#272/#271: an unfounded real save, exact owned stop, fresh-profile cold load, production
    founding and a second real save. Injected backend is a test seam only; the production CLI
    always constructs UnfoundedNativeBackend."""
    require(valid_start(start), "unsupported unfounded reload start")

    def verify(saved, loaded):
        require(saved["command"] == "unfounded-save " + start,
                "reload evidence belongs to another start")
        primary = saved.get("saveHashes", {}).get("Primary.sav.gz")
        for name in ("Primary.sav.gz", "Primary.json"):
            before = saved.get("saveHashes", {}).get(name)
            require(before and loaded.get("importedSaveHashes", {}).get(name) == before,
                    "reload imported other save bytes: " + name)
        require(loaded.get("backupSaveHash") == primary,
                "the second save backup is not the imported unfounded save")
        second = loaded.get("secondSaveHashes", {}).get("Primary.sav.gz")
        require(second and second != primary, "the cold-loaded world wrote no second real save")
        require(loaded.get("foundedAfterLoad") is True,
                "the cold-loaded world did not found its first city")

    return cycle(backend, (start,), verify, "developer-unfounded-cold-reload",
                 dict(foundedAfterLoad=True, secondRealSave=True))


def cycle(backend, selection, verify, scope, extra) -> dict:
    """The shared owned-process order: save, exact stop, fresh destination, stopped-source
    transport, cold load, strict checks on both journals, exact stop."""
    backend.idle("initial")
    source = backend.fresh("save")
    backend.prepare(source, *selection)
    active = None
    failure = None
    try:
        active = source  # A failed launch may still own a process; cleanup must prove its disposition.
        backend.launch(source, "save")
        backend.wait(source, "save")
        saved = backend.check(source, "save")
        require(saved.get("verdict") == "PASS" and saved.get("phase") == "save",
                "save evidence did not pass")
        backend.stop(source, "save")
        active = None
        backend.idle("between")
        destination = backend.fresh("load")
        require(destination != source, "reload must use a fresh profile")
        backend.transport(source, destination)  # Independently proves stopped-source authority.
        active = destination
        backend.launch(destination, "load")
        backend.wait(destination, "load")
        loaded = backend.check(destination, "load")
        require(loaded.get("verdict") == "PASS" and loaded.get("phase") == "load",
                "cold-load evidence did not pass")
        for key in ("gameId", "seed", "command"):
            require(saved.get(key) and loaded.get(key) == saved[key], "reload identity changed: " + key)
        verify(saved, loaded)
        backend.stop(destination, "load")
        active = None
        backend.idle("final")
        result = dict(verdict="PASS", scope=scope,
                      source=str(source), destination=str(destination), gameId=saved["gameId"],
                      sameProfileDirectory=False, scriptResumed=False, gracefulQuit=False,
                      ordinaryAcceptance=False, releaseAcceptance=False)
        result.update(extra)
        return result
    except BaseException as error:
        failure = error
        raise
    finally:
        if active is not None:
            try:
                backend.stop(active, "failure")
            except BaseException as cleanup:
                if failure is None:
                    raise
                raise RuntimeError("reload failed: " + str(failure)
                                   + "; owned-process cleanup also failed: " + str(cleanup)) from failure


class NativeBackend:
    def __init__(self, tools: Path, game: Path, evidence: Path, timeout: int, seed: str):
        self.tools, self.game, self.evidence = tools, game, evidence
        self.timeout, self.seed = timeout, seed

    def command(self, name, args, env=None):
        target = self.evidence / (name + ".log")
        with target.open("x", encoding="utf-8") as output:
            result = subprocess.run(args, stdout=output, stderr=subprocess.STDOUT, env=env,
                                    timeout=max(300, self.timeout))
        require(result.returncode == 0, name + " refused; inspect " + str(target))
        return target

    @staticmethod
    def windows(path):
        return subprocess.check_output(["wslpath", "-w", str(path)], text=True).strip()

    def control(self, mode, name, root=None):
        args = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                self.windows(self.tools / "scenario-process-control.ps1"), "-Mode", mode,
                "-Game", self.windows(self.game)]
        if root is not None:
            args += ["-Root", self.windows(root)]
        self.command(name, args)

    def idle(self, phase):
        self.control("idle", "idle-" + phase)

    def fresh(self, phase):
        root = Path(subprocess.check_output(["mktemp", "-d", "/mnt/c/taf-scenario.XXXXXX"], text=True).strip())
        (self.evidence / ("profile-" + phase + ".txt")).write_text(str(root) + "\n")
        return root

    def prepare(self, root, location, advisor):
        env = dict(os.environ, TAF_REQUEST="founding-first-city",
                   TAF_SCENARIO_SCRIPT="quickstart-save " + location + " " + advisor,
                   TAF_SCENARIO_QUICKSTART_ADVISOR=advisor, TAF_SCENARIO_START="",
                   TAF_SCENARIO_EXTRA_VERBS="", TAF_QUD_ROOT=str(self.game.parent))
        args = ["bash", str(self.tools / "prepare-scenario.sh"), str(root)]
        if self.seed:
            args.append(self.seed)
        self.command("prepare-save", args, env)

    def launch(self, root, phase):
        self.command("launch-" + phase, ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass",
                     "-File", self.windows(self.tools / "run-scenario.ps1"),
                     "-Root", self.windows(root), "-Game", self.windows(self.game)])

    def wait(self, root, phase):
        self.await_journal(root, phase, ("QUICKSTART-" + phase.upper() + "-COMPLETE").encode())

    def await_journal(self, root, phase, target):
        deadline = time.monotonic() + self.timeout
        journal = root / "scenario-journal.tsv"
        while time.monotonic() < deadline:
            if journal.is_file():
                with journal.open("rb") as stream:
                    raw = stream.read(1024 * 1024 + 1)
                require(len(raw) <= 1024 * 1024, "reload journal exceeds bound")
                # This only wakes the strict checker; a substring cannot grant a pass.
                if target in raw or b"\tREFUSED\t" in raw:
                    return
            time.sleep(1)
        raise ValueError("reload " + phase + " timed out; retained profile=" + str(root))

    def check(self, root, phase):
        path = self.command("check-" + phase, ["python3", str(self.tools / "check-quickstart-results.py"),
                            str(root), "--phase", phase], dict(os.environ, TAF_LOG_ALLOW=""))
        return json.loads(path.read_text(encoding="utf-8"))

    def stop(self, root, phase):
        self.control("stop", "stop-" + phase, root)

    def transport(self, source, destination):
        self.command("transport", ["python3", str(self.tools / "prepare-scenario-load.py"),
                                  str(source), str(destination)],
                     dict(os.environ, TAF_QUD_ROOT=str(self.game.parent)))


class UnfoundedNativeBackend(NativeBackend):
    """The same owned process, transport and stop authority as the Quickstart route; only the
    sealed recipe, the wake row and the strict checker (check-unfounded-results.py) differ."""

    def prepare(self, root, start):
        require(valid_start(start), "unsupported unfounded reload start")
        # A Quickstart advisor would make the profile tool refuse an ordinary script.
        env = {key: value for key, value in os.environ.items()
               if key != "TAF_SCENARIO_QUICKSTART_ADVISOR"}
        env.update(TAF_REQUEST="founding-first-city", TAF_SCENARIO_SCRIPT=UNFOUNDED_SCRIPT,
                   TAF_SCENARIO_START=start, TAF_SCENARIO_EXTRA_VERBS=UNFOUNDED_VERB,
                   TAF_QUD_ROOT=str(self.game.parent))
        args = ["bash", str(self.tools / "prepare-scenario.sh"), str(root)]
        if self.seed:
            args.append(self.seed)
        self.command("prepare-save", args, env)

    def wait(self, root, phase):
        self.await_journal(root, phase, b"\tSCRIPT-COMPLETE\t")

    def check(self, root, phase):
        path = self.command("check-" + phase, ["python3", str(self.tools / "check-unfounded-results.py"),
                            str(root), "--phase", phase], dict(os.environ, TAF_LOG_ALLOW=""))
        return json.loads(path.read_text(encoding="utf-8"))
