#!/usr/bin/env python3
"""Reject maintained documentation that drifts from repository truth."""

from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parent.parent
FINAL_SUITE_CASES = "7,586"
FOCUSED_SURVEY_CASES = 9


def text(relative):
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def normalized(relative):
    return " ".join(text(relative).split())


def require(problems, relative, *terms):
    body = normalized(relative)
    for term in terms:
        if " ".join(term.split()) not in body:
            problems.append(f"{relative} is missing current contract text: {term}")


def require_if_present(problems, relative, *terms):
    """Audit ignored local research/coordination notes without requiring them in public CI."""
    if (ROOT / relative).is_file():
        require(problems, relative, *terms)


def forbid(problems, relative, *terms):
    body = normalized(relative)
    for term in terms:
        if " ".join(term.split()) in body:
            problems.append(f"{relative} retains stale current-status text: {term}")


def catalogue_counts():
    buildings = list(ET.parse(ROOT / "KingdomBuildings.xml").getroot().iter("building"))
    architecture = sorted((ROOT / "Architecture").glob("KingdomArchitectures-*.xml"))
    maps = 0
    variants = 0
    for path in architecture:
        root = ET.parse(path).getroot()
        maps += sum(1 for _ in root.iter("map"))
        variants += sum(1 for _ in root.iter("variant"))
    return len(buildings), sum(bool(row.get("Plot")) for row in buildings), maps, variants


def structure_counts():
    payload = json.loads(
        subprocess.check_output(
            [sys.executable, str(ROOT / "Tools" / "check-structure.py"), "--json"],
            cwd=ROOT,
            text=True,
        )
    )
    return (
        payload["files"],
        payload["over300"],
        payload["exactly300"],
        payload["atOrOver300"],
        payload["over1000"],
        payload["over2000"],
        payload["over5000"],
    )


def audit_testing_labels(problems):
    labels = []
    for line_number, line in enumerate(text("TESTING.md").splitlines(), 1):
        match = re.match(r"^\|\s*([0-9]+[a-z]*[0-9]*)\s*\|", line)
        if match:
            labels.append((match.group(1), line_number))
    counts = Counter(label for label, _ in labels)
    duplicate = sorted(label for label, count in counts.items() if count > 1)
    if duplicate:
        details = []
        for label in duplicate:
            lines = [str(line_number) for row, line_number in labels if row == label]
            details.append(f"{label} at lines {','.join(lines)}")
        problems.append("TESTING.md has ambiguous duplicate step IDs: " + "; ".join(details))


def audit_archive_contract(problems):
    source = text("Core/KingdomArchivedSettlementCodec.cs")
    expected = (
        "public const int CurrentVersion = HappeningCursorVersion;",
        "TryEncodeLegacyV1ForTests",
        "TryEncodePreviousV2ForTests",
        "TryEncodeRaidV3ForTests",
        "TryEncodeResidentIdentityV4ForTests",
        "TryEncodeExtensionIdentityV5ForTests",
        "TryEncodeSalvageV6ForTests",
        "TryEncodeBehaviourV7ForTests",
        "TryEncodePhysicalHappeningV8ForTests",
        "TryEncodeExactLogisticsV9ForTests",
        "TryEncodeDefensiveReservationV10ForTests",
        "TryEncodeSemanticSelectionV11ForTests",
    )
    for term in expected:
        if term not in source:
            problems.append(f"archive source no longer proves documented v1-v11 -> v12 contract: {term}")
    require(
        problems,
        "TESTING.md",
        "current settlement-archive reader is **v12**",
        "Independently frozen portable writers cover archive v1-v11",
        "marker** over the same reachable field envelope as v8",
        "realm Jobs v3/v4 fixtures own that proof",
        "stable v12 rewrite",
        "v11 predates per-source happening cursors",
    )
    require(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "Independently frozen test writers exist for v1-v11",
        "v9 is an independently frozen marker-only epoch",
        "realm Jobs v3/v4 fixtures own logistics migration proof",
        "Every historical shape migrates through its current reader to a stable rewrite",
        "one engine-owned pre-bump gate remains",
    )
    for relative in ("TESTING.md", "_notes/BRIEF-IMPLEMENTATION-AUDIT.md"):
        forbid(
            problems,
            relative,
            "Independent v8-v10 archive writers/goldens are absent",
            "independent v8-v10 writer/golden gap",
            "v8-v10 archive writers/goldens are not present",
            "frozen writers cover archive v1-v7",
        )


def audit_public(problems):
    buildings, plots, maps, variants = catalogue_counts()
    files, over300, exact300, at_or_over, over1000, over2000, over5000 = structure_counts()

    require(
        problems,
        "docs/STATUS.md",
        f"{files} sources, baseline and compatibility symbols",
        f"{buildings} buildings",
        f"{plots} plotted buildings",
        f"{maps} maps",
        f"{variants} variants",
        "zero issues",
        "3 expected installed-base tolerant-recovery warnings",
        f"{FOCUSED_SURVEY_CASES} focused source-contract cases",
        "PASS",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "Native Caves of Qud behavior",
        f"{files} staged C# files",
        f"{over300} exceed 300 physical lines",
        "one is exactly 300" if exact300 == 1 else f"{exact300} are exactly 300",
        "structural release gate",
        "stock never grants an indefinite passive bonus",
        "VISION.md",
        "AUTHOR-DEFERRED",
        "REJECTED",
    )
    forbid(
        problems,
        "docs/STATUS.md",
        "RERUN REQUIRED after current runtime edits",
        "7,537",
        "5 focused",
        "zero warnings/issues",
        "_notes/V1-POLITY-SCOPE.md",
        "final rerun pending",
    )

    require(
        problems,
        "README.md",
        "Nine focused one-survey cases",
        f"final {FINAL_SUITE_CASES}-case",
        "not a release candidate",
        "Plots are reservations, not buildings",
        "LotId",
        "MODDING.md#plots-reserved-lots-and-authored-buildings",
        "CONTRIBUTING.md",
        "issue tracker",
        "docs/STATUS.md",
        "docs/STRUCTURE.md",
        "structural release gate",
    )

    require(
        problems,
        "MODDING.md",
        "Plots: reserved lots and authored buildings",
        "A `Plot` declaration reserves a typed rectangle",
        "It is **not** a generated building recipe",
        "LotId",
        "(BuildKey, Category, actual Size)",
        "same declared plan set",
        "fresh siting/restake with a new `LotId`",
        "Complete minimal authored-plot extension",
        "only that bound size can be commissioned",
    )
    forbid(
        problems,
        "MODDING.md",
        "stakes out a rectangle, clears what stands on it, frames it, walls it",
        "roads are never drawn",
    )

    require(
        problems,
        "docs/API.md",
        "STATUS.md",
        "single-survey execution model",
        "reserved lot",
        "Occupied plan",
        "No current commission stretches a smaller map",
        "Same-set transitions preserve `LotId` only through an explicit transition receipt",
        "Food in storage creates no indefinite passive aura",
    )

    require(
        problems,
        "TESTING.md",
        "Nine focused one-survey source-contract cases pass",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "one maintained `KingdomSurvey` classification",
        "no second whole-zone scan",
        "Current implementation limits and v1 scope gates",
        "AUTHOR-DEFERRED",
        "VISION.md",
    )
    forbid(problems, "TESTING.md", "Known v0 limits", "7,537", "5 focused")
    audit_testing_labels(problems)

    require(
        problems,
        "VISION.md",
        "Canonical v1 polity scope matrix",
        "Prior kingdoms recur",
        "Prior NPC appears",
        "Rival kingdom",
        "War between opposites",
        "Polity-shaped NPC levels",
        "Scarce named people",
        "Unnamed guards, parties, diplomats, or armies",
        "Actual trade across tiles",
        "Kingdoms clash",
        "Population without naming a crowd",
        "Multiple-settlement traffic",
        "Food and water",
        "SHIP",
        "AUTHOR-DEFERRED",
        "REJECTED",
    )
    forbid(problems, "VISION.md", "_notes/V1-POLITY-SCOPE.md")

    require(
        problems,
        "CHANGELOG.md",
        "One due settlement pass now owns one maintained zone survey",
        f"Current {files}-file census remains red",
        f"{plots} plotted plans over {maps} inspectable authored maps",
        "Nine focused survey source-contract cases pass",
        f"passes {FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "AUTHOR-DEFERRED",
        "REJECTED",
        "Addendum 9 structural debt is now an executable release blocker",
        "Art/runtime-assets.json",
    )
    forbid(
        problems,
        "CHANGELOG.md",
        "83 commissionable plan families",
        "247 inspectable maps",
        "rejects local raster paths",
        "7,537",
        "5 focused",
    )

    require(
        problems,
        "CONTRIBUTING.md",
        "docs/STATUS.md",
        "A plotted building needs both catalogue metadata and authored architecture",
        "complete extension example",
        "Generative-image assistance is neither silently accepted nor categorically banned",
        "editable source",
        "pixel-level human revision",
        "Tools/check-structure.py --report",
        "strictly under 300 physical lines",
    )
    require(
        problems,
        ".github/PULL_REQUEST_TEMPLATE.md",
        "Generative-assisted originals must follow",
        "pixel-level human revision",
        "independent native review",
    )
    for relative in (
        ".github/ISSUE_TEMPLATE/bug.yml",
        ".github/ISSUE_TEMPLATE/compatibility.yml",
    ):
        require(problems, relative, "manifest version / full-or-short-commit")
        forbid(problems, relative, 'placeholder: "0.2.0"')

    require(
        problems,
        "STANDARDS.md",
        "One semantic survey per due active-seat reconciliation",
        "Art/runtime-assets.json",
        "Status has one short authority",
        "Tools/check-structure.py --report",
    )
    require(
        problems,
        "docs/ARCHITECTURE.md",
        "One classified active-seat survey",
        "STATUS.md",
        "structural release contract",
    )
    require(
        problems,
        "docs/ASSET_PROVENANCE.md",
        "Art/runtime-assets.json",
        "Original runtime asset manifest",
        "pixel-level human revision",
    )
    require(
        problems,
        "docs/RELEASING.md",
        "exact allowlisted runtime raster paths",
        "docs/STRUCTURE_REVIEW.json",
        "failed release",
    )
    require(
        problems,
        "docs/STRUCTURE.md",
        "strictly under 300",
        "Tools/check-structure.py --report",
        "Tools/check-structure.py --release",
        "exact staged source inventory digest",
        "accepts no exceptions",
    )
    for relative in (
        "STANDARDS.md",
        "CONTRIBUTING.md",
        "docs/ASSET_PROVENANCE.md",
        "docs/RELEASING.md",
    ):
        forbid(
            problems,
            relative,
            "bundles no original runtime bitmap sprites",
            "rejects bundled runtime rasters and local tile paths",
            "Current release policy rejects bundled runtime bitmap sprites",
        )


def audit_private(problems):
    if not (ROOT / "_notes").is_dir():
        return
    files, over300, exact300, at_or_over, over1000, over2000, over5000 = structure_counts()

    require(
        problems,
        "_notes/README.md",
        "Freshness and authority",
        "../docs/STATUS.md",
        "Canonical v1 polity scope matrix",
        "expanded private evidence/reopening worksheet",
        "Files explicitly labelled **historical**",
        "RESEARCH-RERUN.md",
        "COMPARABLES-RERUN.md",
        "Tools/stage.sh deploy",
        "git push` updates only the public remote",
    )
    require(
        problems,
        "_notes/V1-POLITY-SCOPE.md",
        "expanded private evidence worksheet",
        "V1-V11 reconciled matrix",
        "SHIP",
        "AUTHOR-DEFERRED",
        "REJECTED",
        "Ingredients, crops, larders, meals, and industry",
        "Passive bonus merely while ingredients remain stocked",
        "Runtime/evidence owner",
    )
    require(
        problems,
        "_notes/DECISIONS.md",
        "One survey per due active-seat reconciliation",
        "dense native instrumentation remains",
        "The people are not the old roll walking around",
        "AUTHOR-DEFERRED",
        "REJECTED",
    )
    require(
        problems,
        "_notes/QUESTION-BACKLOG.md",
        "QB-16 — CLOSED",
        "implemented through the measured sparse",
        "Author-deferred world presence",
        "Exact old actors",
        "offscreen conquest/loss remain rejected",
    )
    require(
        problems,
        "_notes/SESSION-HANDOFF.md",
        "current v1.0 test-candidate work",
        f"{files} staged sources",
        "Nine focused one-survey source-contract cases",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "Historical 0.2.0 candidate evidence",
        "Tools/stage.sh deploy",
        "maintainer has explicitly authorized push",
        "generative-assisted drafts",
        "AUTHOR-DEFERRED",
    )
    require_if_present(
        problems,
        "_notes/SESSION-LOG.md",
        "Current continuation",
        f"{files} staged sources",
        f"{over300} files exceed 300 physical lines",
        f"{over1000} exceed 1,000",
        "Nine focused one-survey cases",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
    )
    require_if_present(
        problems,
        "_notes/COORDINATION.md",
        "Current handoff",
        f"{files}-source baseline/compat compile clean",
        f"final integrated suite {FINAL_SUITE_CASES}/{FINAL_SUITE_CASES}",
        "Codex subagents",
        "Archived Claude inbox — historical",
    )
    require_if_present(
        problems,
        "_notes/RESEARCH.md",
        "historical early snapshot",
        "RESEARCH-RERUN.md",
        "COMPARABLES-RERUN.md",
        "design hypotheses",
        "CyberneticsTerminal.cs",
        "CyberneticsTerminal2.cs` does not contain that gate",
    )
    require_if_present(
        problems,
        "_notes/AGENT-PLAYBOOK.md",
        "Correction",
        "XRL/UI/CyberneticsTerminal.cs",
        "not `CyberneticsTerminal2.cs`",
        "native reachability remains unverified",
    )
    require_if_present(
        problems,
        "_notes/FOOD-WATER-FINAL-REVIEW.md",
        "historical research disposition, not current implementation truth",
        "physical crop/larder/meal runtime landed",
        "indefinite stocked-food aura",
    )
    require_if_present(
        problems,
        "_notes/IDEA-INBOX.md",
        "historical source/index",
        "not a live owner claim or implementation queue",
        "V1-POLITY-SCOPE.md",
    )
    require(
        problems,
        "_notes/COVERAGE-GAP-MAP.md",
        "Historical audit, not current status",
        "DO NOT TRIAGE THESE ROWS AS CURRENT GAPS",
        "KingdomPlot2",
        "KingdomExpeditions",
    )
    require_if_present(problems, "_notes/COVERAGE.md", "Historical")
    require(problems, "_notes/COVERAGE-GAP-MAP.md", "Historical")
    require_if_present(problems, "_notes/UX-PACING-AUDIT.md", "Historical")

    require(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        f"**{files}** production C# files",
        f"**{over300}** files exceed 300 physical lines",
        f"**{at_or_over}** fail",
        f"**{over1000}** exceed 1,000",
        f"**{over2000}** exceed 2,000",
        f"**{over5000}** exceed 5,000",
        f"**{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES}** cases",
        "nine focused survey cases",
        "Food and water are separate physical civic flows",
        "one positive settler-equivalent for one day",
        "Canonical disposition and evidence owner: `VISION.md`",
    )
    forbid(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "XML inventory: 106 buildings, 96 plot records",
        "Runtime raises the same border/floor/one-door rectangle",
        "`r_KingdomGatehouse` is one solid, occluding furniture object",
        "`r_KingdomDelve` has no physical travel part",
        "creates one fresh object for each prepared work",
        "FindProvokedFaction` treats raw standing",
        "no in-run founder-shrine route was found",
        "The shared suite passed **7,308**",
        "finds **155** production files over 300 lines",
        "The art policy is being upgraded",
        "zero warnings/issues",
    )

    require(
        problems,
        "_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md",
        f"{files} production C# files",
        f"{over300} exceed 300 physical lines",
        f"{at_or_over} fail strict",
        f"{over1000} exceed 1,000",
        f"{over2000} exceed 2,000",
        f"{over5000} exceed 5,000",
        f"passes {FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "nine focused survey cases",
    )
    forbid(
        problems,
        "_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md",
        "declares module checkboxes but no master checkbox",
        "The shipped catalogue still constructs raw",
        "it never asks the zone graph for intermediate hops",
        "The old runtime scans only `KingdomMaterials`",
        "assigns `Citizen.Brain.Factions =",
        "That also suppresses fetch, upkeep",
        "live steps then scan the full zone again",
        "259 production sources",
        "finds 169 production `.cs` files over 300 lines",
        "zero warnings/issues",
    )


def main():
    problems = []
    audit_public(problems)
    audit_private(problems)
    audit_archive_contract(problems)
    if problems:
        print("DOCUMENTATION FRESHNESS FAILED")
        for problem in problems:
            print("  " + problem)
        return 1
    print("DOCUMENTATION FRESHNESS CLEAN: public guides and current private ledgers agree")
    return 0


if __name__ == "__main__":
    sys.exit(main())
