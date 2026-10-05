"""#283 stuck-save heal route: one cold-process continuation across two builds.

Session one runs the sealed heal script on a tree WITHOUT the fix (this tree with exactly the
A1-A3 production files restored from the pre-fix base), drives the paid tent -> tentrow renovate
into the retired handover stall and saves it. Session two cold-loads that exact save in a fresh
descendant profile whose staged runtime comes from THIS tree (prepare-scenario-load.py
--runtime), so the fixed build reads the stuck save. Host-only orchestration; the verdicts are
Tools/check-renovate-heal-results.py's. Not ordinary-play or release acceptance.
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import subprocess

from persona_reload import NativeBackend, cycle, require, valid_start
from personas.persona_matrix import RENOVATE_HEAL_RELOAD_SCRIPT, RENOVATE_HEAL_RELOAD_VERBS

# The seed of the #266 native run 4ea54bb9: it sites the tent inside the heart's survey, the
# yielding case the retired defect needs. Used when the operator supplies no TAF_PERSONA_SEED.
DEFAULT_SEED = "#922453088"
SCRIPT_WORDS = " ".join(RENOVATE_HEAL_RELOAD_SCRIPT)
VERBS = ",".join(RENOVATE_HEAL_RELOAD_VERBS)
# The #283 production change (design A1-A3) in staged paths. Session one's tree must differ from
# this tree in exactly these runtime files: the six new files absent, the nine changed files
# restored from the pre-fix base, every other staged file, the harness and the tools identical.
FIX_NEW = (
    "Growth/KingdomConstructionRules.Readmission.cs",
    "Growth/KingdomUpgrade.20b.LandedScaffold.cs",
    "Growth/KingdomUpgrade.27.RetiredDefectReadmission.cs",
    "Growth/KingdomUpgradeRules.FounderMarks.cs",
    "Growth/KingdomUpgradeRules.LandedScaffold.cs",
    "Growth/KingdomUpgradeRules.RetiredDefect.cs",
)
FIX_CHANGED = (
    "Core/KingdomRemovalCoverage.Generated.cs",
    "Growth/KingdomConstruction.Transitions.cs",
    "Growth/KingdomConstructionRules.Transitions.cs",
    "Growth/KingdomUpgrade.06.r_KingdomImprovement.Poll.cs",
    "Growth/KingdomUpgrade.20.HandOver.cs",
    "Growth/KingdomUpgrade.21.HandoverProofs.cs",
    "Growth/KingdomUpgrade.22.CarryMarks.cs",
    "Growth/KingdomUpgrade.24.HandoverContents.cs",
    "Growth/KingdomUpgrade.25.HandoverRemoval.cs",
)


def stage_manifest(tree: Path) -> dict[str, str]:
    """The staged runtime of one tree, path -> sha256, from that tree's own Tools/stage.sh."""
    listing = subprocess.run(["bash", str(tree / "Tools" / "stage.sh"), "manifest"], cwd=tree,
                             capture_output=True, text=True, timeout=900)
    require(listing.returncode == 0, "stage manifest refused for " + str(tree))
    found: dict[str, str] = {}
    for line in listing.stdout.splitlines():
        digest, _, path = line.partition("  ")
        require(len(digest) == 64 and path and path not in found, "malformed stage manifest row")
        found[path] = digest
    return found


def tracked(tree: Path, roots: tuple[str, ...]) -> dict[str, str]:
    """Tracked files under the named roots, hashed from the working tree (generated output and
    caches are untracked, so they never count)."""
    names = subprocess.run(["git", "-C", str(tree), "ls-files", "-z", "--", *roots],
                           capture_output=True, timeout=300)
    require(names.returncode == 0, "git ls-files refused for " + str(tree))
    found: dict[str, str] = {}
    for raw in names.stdout.split(b"\0"):
        if raw:
            path = raw.decode("utf-8")
            target = tree / path
            found[path] = hashlib.sha256(target.read_bytes()).hexdigest() if target.is_file() else "absent"
    return found


def validate_source_tree(source: Path, fixed: Path) -> dict:
    """Refuses before any effect unless session one's tree is this tree minus exactly the fix."""
    require(source.is_absolute() and source.is_dir() and source.resolve() != fixed.resolve()
            and (source / "Tools" / "prepare-scenario.sh").is_file()
            and (source / "Harness").is_dir(), "the unfixed source tree is not a separate TAF checkout")
    before, after = stage_manifest(source), stage_manifest(fixed)
    delta = sorted(path for path in set(before) | set(after) if before.get(path) != after.get(path))
    require(delta == sorted(FIX_NEW + FIX_CHANGED),
            "the source tree differs from this tree outside exactly the #283 fix: " + ",".join(delta))
    require(not any(path in before for path in FIX_NEW) and all(path in before for path in FIX_CHANGED),
            "the source tree does not carry the pre-fix runtime shape")
    require(tracked(source, ("Harness",)) == tracked(fixed, ("Harness",)),
            "the source tree's harness differs from this tree's")
    require(tracked(source, ("Tools",)) == tracked(fixed, ("Tools",)),
            "the source tree's tools differ from this tree's")
    return dict(sourceTree=str(source), runtimeDelta=delta, harnessIdentical=True, toolsIdentical=True)


def execute_renovate_heal(backend, start: str) -> dict:
    """Injected backend is a test seam only; the production CLI constructs RenovateHealNativeBackend."""
    require(valid_start(start), "unsupported heal reload start")

    def verify(saved, loaded):
        require(saved["command"] == "renovate-heal " + start, "heal evidence belongs to another start")
        require(saved.get("defect") in ("A", "B"), "session one reproduced no retired defect")
        require(loaded.get("readmittedDefect") == saved["defect"],
                "session two readmitted another defect than session one saved")
        for name in ("Primary.sav.gz", "Primary.json"):
            before = saved.get("saveHashes", {}).get(name)
            require(before and loaded.get("importedSaveHashes", {}).get(name) == before,
                    "heal reload imported other save bytes: " + name)
        primary = saved["saveHashes"]["Primary.sav.gz"]
        require(loaded.get("backupSaveHash") == primary, "the second save backup is not the stuck save")
        second = loaded.get("secondSaveHashes", {}).get("Primary.sav.gz")
        require(second and second != primary, "the healed world wrote no second real save")
        require(loaded.get("healedAfterLoad") is True and loaded.get("crossBuild") is True,
                "session two did not heal the stuck save on the fixed build")
        reason = saved.get("stallSealReason")
        require(reason and set(loaded.get("preHealSealReasons", [])) <= {reason},
                "session two's pre-heal seal reports name another reading than session one's stall")

    return cycle(backend, (start,), verify, "developer-renovate-heal-cold-reload",
                 dict(healedAfterLoad=True, secondRealSave=True, crossBuild=True))


class RenovateHealNativeBackend(NativeBackend):
    """Session one is prepared from the unfixed tree; session two's runtime from this tree."""

    def __init__(self, tools: Path, game: Path, evidence: Path, timeout: int, seed: str,
                 source_tree: Path):
        super().__init__(tools, game, evidence, timeout, seed or DEFAULT_SEED)
        self.source_tree = source_tree

    def environment(self, start):
        require(valid_start(start), "unsupported heal reload start")
        env = {key: value for key, value in os.environ.items()
               if key not in ("TAF_SCENARIO_QUICKSTART_ADVISOR", "TAF_SCENARIO_ROLE")}
        env.update(TAF_REQUEST="founding-first-city", TAF_SCENARIO_SCRIPT=SCRIPT_WORDS,
                   TAF_SCENARIO_START=start, TAF_SCENARIO_EXTRA_VERBS=VERBS,
                   TAF_QUD_ROOT=str(self.game.parent))
        return env

    def prepare(self, root, start):
        self.start = start
        self.command("prepare-save", ["bash", str(self.source_tree / "Tools" / "prepare-scenario.sh"),
                                      str(root), self.seed], self.environment(start))

    def wait(self, root, phase):
        self.await_journal(root, phase, b"\tSCRIPT-COMPLETE\t")

    def check(self, root, phase):
        import json
        path = self.command("check-" + phase, ["python3", str(self.tools / "check-renovate-heal-results.py"),
                            str(root), "--phase", phase], dict(os.environ, TAF_LOG_ALLOW=""))
        return json.loads(path.read_text(encoding="utf-8"))

    def transport(self, source, destination):
        """A fresh never-launched runtime donor prepared by THIS tree with session one's exact
        request, seed, start and script, then the stopped-source cross-build copy."""
        runtime = self.fresh("runtime")
        self.command("prepare-runtime", ["bash", str(self.tools / "prepare-scenario.sh"), str(runtime),
                                         self.seed], self.environment(self.start))
        self.command("transport", ["python3", str(self.tools / "prepare-scenario-load.py"), "--runtime",
                                   str(runtime), str(source), str(destination)],
                     dict(os.environ, TAF_QUD_ROOT=str(self.game.parent)))
