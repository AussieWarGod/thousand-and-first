"""One cold-process Quickstart continuation; never reuse a spent profile or resume its script."""
from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import time


def require(value, reason):
    if not value:
        raise ValueError(reason)


def execute(backend, location: str, advisor: str) -> dict:
    """Injected backend is a test seam only; production CLI always constructs NativeBackend."""
    require(location in ("marsh", "canyon", "dunes") and advisor in ("yes", "no"),
            "unsupported reload selection")
    backend.idle("initial")
    source = backend.fresh("save")
    backend.prepare(source, location, advisor)
    active = None
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
        require(saved["command"] == "quickstart-save " + location + " " + advisor,
                "reload evidence belongs to another selection")
        for name in ("Primary.sav.gz", "Primary.json"):
            before = saved.get("saveHashes", {}).get(name)
            require(before and loaded.get("saveHashes", {}).get(name) == before,
                    "reload save bytes changed: " + name)
        backend.stop(destination, "load")
        active = None
        backend.idle("final")
        return dict(verdict="PASS", scope="developer-quickstart-cold-reload",
                    source=str(source), destination=str(destination), gameId=saved["gameId"],
                    sameProfileDirectory=False, scriptResumed=False, gracefulQuit=False,
                    ordinaryAcceptance=False, releaseAcceptance=False)
    finally:
        if active is not None:
            backend.stop(active, "failure")


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
        deadline = time.monotonic() + self.timeout
        target = "QUICKSTART-" + phase.upper() + "-COMPLETE"
        journal = root / "scenario-journal.tsv"
        while time.monotonic() < deadline:
            if journal.is_file():
                with journal.open("rb") as stream:
                    raw = stream.read(1024 * 1024 + 1)
                require(len(raw) <= 1024 * 1024, "reload journal exceeds bound")
                # This only wakes the strict checker; a substring cannot grant a pass.
                if target.encode() in raw or b"\tREFUSED\t" in raw:
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
