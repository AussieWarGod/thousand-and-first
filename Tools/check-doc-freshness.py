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
FINAL_SUITE_CASES = "7,743"
PORTABLE_SUITE_CASES = "173"
TOOLS_TEST_CASES = "35"
ART_TEST_CASES = "23"
HARDENING_DECOMPOSITIONS = "144"
CUMULATIVE_DECOMPOSITIONS = "154"
CURRENT_TAF_CASES = "10,624"
CURRENT_PORTABLE_CASES = "2,325"
CURRENT_TOOLS_CASES = "296"
CURRENT_ART_CASES = "28"
CURRENT_FOCUSED_SURVEY_CASES = 14
CURRENT_VANILLA_TILE_PATHS = 125

# These notes are immutable attack/research snapshots whose local line citations belong to the
# pinned tree named in each document. Current authorities remain audited below. Repointing frozen
# evidence at today's shards would falsify what was inspected; _notes/README.md owns this policy.
FROZEN_SOURCE_CITATION_DOCUMENTS = frozenset(
    {
        "_notes/ASSIGNMENT-LOG.md",
        "_notes/ARCOLOGY-AUTHORED-INTERIOR-PLAN.md",
        "_notes/ARCHITECTURE-POLISH-DISK-AUDIT.md",
        "_notes/CLOCK-REWORK-CHANGE-MAP.md",
        "_notes/CODEX-ENGINE-TRUTH-BATCH-1-ANSWERS.md",
        "_notes/COVERAGE-GAP-MAP.md",
        "_notes/ECONOMY-ADVERSARIAL-AUDIT.md",
        "_notes/ECONOMY-MODEL.md",
        "_notes/FOUNDATION-RUNTIME-FULL-AUDIT-CLAUDE.md",
        "_notes/IDEA-INBOX.md",
        "_notes/STALE-COMMENT-INVENTORY.md",
        "_notes/THREAT-DIPLOMACY-AUDIT.md",
        "_notes/UX-PACING-AUDIT.md",
        "_notes/VANILLA-PRODUCTION-TRUTH.md",
    }
)


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
    buildings = list(
        ET.parse(ROOT / "RuntimeData/KingdomBuildings.xml").getroot().iter("building")
    )
    architecture = sorted((ROOT / "Architecture").glob("KingdomArchitectures-*.xml"))
    maps = 0
    variants = 0
    for path in architecture:
        root = ET.parse(path).getroot()
        maps += sum(1 for _ in root.iter("map"))
        variants += sum(1 for _ in root.iter("variant"))
    return (
        len(buildings),
        sum(bool(row.get("Plot")) for row in buildings),
        maps,
        variants,
    )


def structure_report():
    return json.loads(
        subprocess.check_output(
            [sys.executable, str(ROOT / "Tools" / "check-structure.py"), "--json"],
            cwd=ROOT,
            text=True,
        )
    )


def cold_install_count():
    paths = subprocess.check_output(
        [str(ROOT / "Tools" / "stage.sh"), "list"], cwd=ROOT, text=True
    )
    return len(paths.splitlines())


def structure_counts(payload=None):
    if payload is None:
        payload = structure_report()
    return (
        payload["files"],
        payload["over300"],
        payload["exactly300"],
        payload["atOrOver300"],
        payload["over1000"],
        payload["over2000"],
        payload["over5000"],
    )


def changelog_structure_status_terms(files, at_or_over):
    """Return truthful changelog wording for executable and human structure gates."""
    if at_or_over:
        return (
            f"Current {files}-file census remains red",
            "Addendum 9 structural debt is now an executable release blocker",
        )
    return (
        f"Current {files}-file census is line-cap green",
        "Addendum 9 line-cap debt is cleared",
        "exact-inventory human semantic review remains a release blocker",
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
        problems.append(
            "TESTING.md has ambiguous duplicate step IDs: " + "; ".join(details)
        )


def audit_source_citations(problems):
    """Reject local file:line evidence that no longer exists after a source split."""
    files = [
        path for path in ROOT.rglob("*") if path.is_file() and ".git" not in path.parts
    ]
    by_name = {}
    for path in files:
        by_name.setdefault(path.name, []).append(path)
    pattern = re.compile(
        r"`([^`\n]+?\.(?:cs|md|xml|py|sh|json)):(\d+)(?:-(\d+))?`",
        re.IGNORECASE,
    )
    line_counts = {}
    declaration_only_split_anchors = {
        path for path in files if _is_declaration_only_split_anchor(path)
    }
    for document in (path for path in files if path.suffix.lower() == ".md"):
        relative = document.relative_to(ROOT).as_posix()
        if relative in FROZEN_SOURCE_CITATION_DOCUMENTS:
            continue
        body = document.read_text(encoding="utf-8-sig", errors="replace")
        for match in pattern.finditer(body):
            raw = match.group(1).replace("\\", "/")
            candidate = Path(raw)
            if candidate.is_absolute():
                resolved = candidate
            elif (ROOT / candidate).is_file():
                resolved = ROOT / candidate
            elif "/" not in raw and len(by_name.get(raw, ())) == 1:
                resolved = by_name[raw][0]
            else:
                continue
            if not resolved.is_file():
                continue
            if resolved not in line_counts:
                with resolved.open(encoding="utf-8-sig", errors="replace") as source:
                    line_counts[resolved] = sum(1 for _ in source)
            last = int(match.group(3) or match.group(2))
            if last <= line_counts[resolved]:
                if resolved not in declaration_only_split_anchors:
                    continue
                line = body.count("\n", 0, match.start()) + 1
                problems.append(
                    f"{relative}:{line} cites declaration-only split anchor "
                    f"{match.group(0)}; cite the exact shard or logical family plus symbol"
                )
                continue
            line = body.count("\n", 0, match.start()) + 1
            relative = document.relative_to(ROOT)
            problems.append(
                f"{relative}:{line} has stale source citation {match.group(0)}; "
                f"{resolved} has {line_counts[resolved]} lines"
            )


def _is_declaration_only_split_anchor(path):
    """True when a split C# anchor retains declarations/comments but no owned members."""
    if path.suffix.lower() != ".cs" or not any(path.parent.glob(path.stem + ".*.cs")):
        return False
    source = path.read_text(encoding="utf-8-sig", errors="replace")
    if " partial class " not in " " + " ".join(source.split()) + " ":
        return False
    in_comment = False
    for raw in source.splitlines():
        line = raw.strip()
        if in_comment:
            if "*/" in line:
                in_comment = False
            continue
        if line.startswith("/*"):
            if "*/" not in line:
                in_comment = True
            continue
        if not line or line.startswith(("//", "using ", "namespace ", "#", "[")):
            continue
        if line in ("{", "}") or " partial class " in " " + line + " ":
            continue
        return False
    return True


def audit_archive_contract(problems):
    source = "\n".join(
        path.read_text(encoding="utf-8-sig")
        for path in sorted((ROOT / "Core").glob("KingdomArchivedSettlementCodec*.cs"))
    )
    expected = (
        "public const int FirstGuestVersion = 15;",
        "public const int PhysicalFirstGuestVersion = 16;",
        "public const int ArrivalCadenceVersion = 17;",
        "public const int ExpeditionResultVersion = 18;",
        "public const int CurrentVersion = ExpeditionResultVersion;",
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
        "TryEncodeHappeningCursorV12ForTests",
        "TryEncodeDeliveryDomainV13ForTests",
        "TryEncodeCivicAuthorityV14ForTests",
        "TryEncodeFirstGuestV15ForTests",
        "TryEncodePhysicalFirstGuestV16ForTests",
        "TryEncodeArrivalCadenceV17ForTests",
    )
    for term in expected:
        if term not in source:
            problems.append(
                f"archive source no longer proves documented v1-v17 -> v18 contract: {term}"
            )
    require(
        problems,
        "TESTING.md",
        "current settlement-archive reader is **v17**",
        "Independently frozen portable writers cover archive v1-v16",
        "marker** over the same reachable field envelope as v8",
        "realm Jobs v3/v4 fixtures own that proof",
        "stable v17 rewrite",
        "v11 predates per-source happening cursors",
        "v13 admits construction-input delivery authority",
        "v14 adds city-local named-cook and assenting-moot receipts",
        "v15 adds first-guest correspondence",
        "v16 adds the durable physical pre-citizen guest phase",
        "v17 adds fixed-rate arrival cadence authority",
    )
    require(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "settlement archive current reader is v17",
        "Independently frozen test writers exist for v1-v16",
        "v9 is an independently frozen marker-only epoch",
        "realm Jobs v3/v4 fixtures own logistics migration proof",
        "Every historical shape migrates through its current reader to a stable v17 rewrite",
        "one engine-owned pre-bump gate remains",
    )
    require(
        problems,
        "CHANGELOG.md",
        "nested settlement archive reader is now version 17",
        "frozen v1-v16 migration evidence",
    )
    forbid(
        problems,
        "TESTING.md",
        "current settlement-archive reader is **v12**",
        "Current nested archive v12",
        "stable v12 rewrite",
        "frozen portable writers cover archive v1-v11",
    )
    forbid(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "settlement archive current reader is v12",
        "current archive v12",
        "current-v12",
        "remains intact in v12",
        "frozen test writers exist for v1-v11",
    )
    forbid(
        problems,
        "CHANGELOG.md",
        "nested settlement archive is version 8",
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


def audit_research_alignment_contract(problems):
    """Keep current product truth aligned with the completed research disposition."""
    require(
        problems,
        "VISION.md",
        "Sim Settlements supplies one useful, narrower comparator lesson",
        "It does not prove a popularity superlative",
        "Resheph's harborage contributes poetic and cosmopolitan resonance as a cosmic port, not proof of sanctuary",
        "the histories of Abram and Resheph remain contested",
        "Where a history is deliberately authored as disputed",
        "ordinary events keep one owning record and are not mechanically duplicated",
        "Disputed history can have two registers",
        "there is no universal pair generator",
        "No renewal covenant",
        "every supported covenant, creed, and founding outcome remains playable",
        "Growth-1B/schema-5 oracle is not deferred runtime scope",
        "Installing it beside current cadence would create a parallel authority",
    )
    require(
        problems,
        "docs/STATUS.md",
        "Research disposition coverage",
        "4 community, 16 people, 2 polity, 7 order, 2 doctrine, 2 cult",
        "rejected as parallel architecture—not deferred v1 debt",
        "official/outsider entries",
        "reference-grounded quality audit 1,376 / 1,376 static pass, 0 fail",
    )
    require_if_present(
        problems,
        "_notes/DECISIONS.md",
        "survivor/graveyard ranking and “twenty games deep” causal-confidence claim are superseded",
        "Resheph's harborage is poetic and cosmopolitan cosmic-port resonance, not sanctuary proof",
    )
    require_if_present(
        problems,
        "_notes/AGENT-PLAYBOOK.md",
        "Comparator correction, 2026-09-01",
        "different audiences and cannot be treated as one natural experiment",
        "Lore correction, 2026-09-01",
        "Qud does not tell every history twice",
        "No renewal covenant is mandatory",
        "Resheph's harborage is cosmic-port resonance rather than",
        "sanctuary proof. See `LORE-SPIRIT-AUDIT.md`",
    )
    require_if_present(
        problems,
        "_notes/POSITIVE-FIRST-COMPARABLES.md",
        "Current-use correction, 2026-09-01",
        "Later factchecking and reconciliation reject its popularity, market-success, and",
        "causal-preference claims.",
        "typed plots, authored plans, and delegated growth",
        "it does not prove a \u201cmost-loved\u201d Fallout",
    )
    require_if_present(
        problems,
        "_notes/GENERATIVE-COMPARABLES-CLAUDE.md",
        "Current-use correction, 2026-09-01",
        "do not treat its popularity or success-causality language as evidence",
        "only typed plots, authored plans, and delegated growth survive",
        "\u201cmost-loved\u201d claim or inferred player preference survives",
    )
    require(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "No generated record remains held",
        "4 community / 16 people / 2 polity / 7 order / 2 doctrine / 2 cult",
        "water plus roof only",
        "Neseva Cask-Hand's Uru Ux 1000 AR copy",
        "full Art suite passes 28/28",
        "1,376/1,376 static pose passes and 0 fails",
    )
    require_if_present(
        problems,
        "_notes/ARCHITECTURE-POLISH-CONTRACT.md",
        "Gyre Wight work is a separate affiliation/people-gated, explicitly non-theological overlay",
    )
    forbid(
        problems,
        "_notes/ARCHITECTURE-POLISH-CONTRACT.md",
        "Gyre Wight work is a separate doctrine-gated overlay",
    )
    require_if_present(
        problems,
        "_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md",
        "Source-complete disposition crosswalk",
        "GENERATIVE-COMPARABLES-{CLAUDE,CODEX}.md",
        "FOOD-WATER-DISCOVERY-CLAUDE.md",
        "QUD-AFFORDANCE-CHALLENGE.md",
        "POLITY-EXPANSION-RECONCILIATION*",
        "Growth-1B/schema-5 oracle and cross-review chain",
        "rejected as a parallel arrival authority",
        "4 community / 16 people / 2 polity / 7 order / 2 doctrine / 2 cult",
        "Neseva Cask-Hand's Uru Ux 1000 AR Open Basin copy",
        "exact official/outsider rendered pair",
        "no passive rot/debit/support remains",
        "full Art suite now passes 28/28",
        "1,376/1,376 static pose pass, 0 fail",
    )
    require_if_present(
        problems,
        "_notes/FIXTURE-POSE-CENSUS-2026-09-01.md",
        "ordinary `Woven Basket` silhouette",
        "Hindren fixture is an honest treadle stitcher",
        "glyph-only `Ø` emblem",
        "Full `Art.test_check_wiring`: 28/28 passing",
        "Installed-Qud `Tools/audit-architecture-quality.py`: 1,376/1,376 static pose passes",
        "installed-Qud scan has zero issues plus three expected tolerant-recovery warnings",
        "Native capture remains the acceptance boundary for visual taste",
    )
    for relative in (
        "VISION.md",
        "docs/STATUS.md",
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
    ):
        forbid(
            problems,
            relative,
            "Three hosted-arcology maps remain explicit holds",
            "except one narrow visual use",
            "full Art suite passes 27/28",
            "full Art suite remains blocked",
        )


def audit_market_contract(problems):
    """Keep the physical native-TradeUI market law from regressing into generated stock."""
    require(
        problems,
        "VISION.md",
        "accepted, staffed `taf:market` provider",
        "may honestly open empty",
        "current operational standing and reach",
        "Only ordinary Qud TradeUI exchange creates or removes its physical `_stock`",
        "TAF generates no wares, consignments, periodic restock, passive output, or remote debit",
        "Completed or dormant legendary traders remain finite personal native merchants",
        "Succession excludes only an open prepared market handoff endpoint",
    )
    require(
        problems,
        "docs/STATUS.md",
        "accepted staffed `taf:market` provider",
        "may open empty",
        "`ShopTier` is current operational standing/reach and may fall to zero",
        "Native TradeUI sale/purchase is the only ordinary stock ingress/sink",
        "The sealed `GenericInventoryRestocker` is only an empty-trade adapter",
        "only open prepared handoff endpoints are temporarily succession-ineligible",
    )
    require(
        problems,
        "TESTING.md",
        "Native TradeUI opens honestly empty",
        "native TradeUI physical `_stock`",
        "Automatic output, population-generated wares, consignments, tier replacements, and periodic restock remain zero",
        "TAF retires only its own receipt/guards",
        "Only an open prepared handoff endpoint is temporarily succession-ineligible",
    )
    require(
        problems,
        "docs/API.md",
        "The ordinary civic market has the same physical law",
        "Native TradeUI sale is the sole ordinary ingress",
        "TAF never population-rolls, mints, consigns, replaces, remotely debits, or periodically restocks wares",
        "Completed or dormant legendary/native traders remain finite personal merchants",
    )
    require(
        problems,
        "MODDING.md",
        "It is only Qud's adapter for opening an empty native trade screen",
        "Do not add population tables, output tables, generated consignments, automatic replacement, or a restock schedule",
        "Completed/dormant legendary or native personal traders survive civic loss and accession",
    )
    require(
        problems,
        "CHANGELOG.md",
        "Markets now trade only physical goods through native Qud TradeUI",
        "TAF generates no market inventory, consignments, periodic restock, passive output, or remote debit",
    )
    require_if_present(
        problems,
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "opens honestly empty",
        "No tiered ware generation or restock exists",
        "only TAF marks retire",
        "prepared handoff endpoints pause succession",
    )
    require_if_present(
        problems,
        "_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md",
        "Native TradeUI `_stock` is sole ordinary ware authority",
        "`ShopTier` is current reach and may fall to zero",
        "Completed/dormant legendary merchants survive civic loss/accession without civic authority",
    )
    require_if_present(
        problems,
        "_notes/AGENT-PLAYBOOK.md",
        "current operational standing/reach seam",
        "sealed `GenericInventoryRestocker` is only an empty-screen adapter",
    )
    require_if_present(
        problems,
        "_notes/balance-sim-output.txt",
        "market stock: native TradeUI physical _stock only; generated output/restock = 0",
        "market custody: detached/personal/foreign goods stay physical; TAF marks retire only",
        "market succession: only an open prepared handoff endpoint is temporarily unavailable",
    )
    for relative in (
        "VISION.md",
        "docs/STATUS.md",
        "TESTING.md",
        "docs/API.md",
        "MODDING.md",
        "_notes/BRIEF-IMPLEMENTATION-AUDIT.md",
        "_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md",
    ):
        forbid(
            problems,
            relative,
            "stock tier rises with the settlement",
            "shops carry one tier above",
            "trade with them shows tier-1 stock",
            "stocks the trader from the city's current tier",
            "current-tier wares",
            "8 one-shot tier consignments",
        )


def audit_public(problems):
    buildings, plots, maps, variants = catalogue_counts()
    report = structure_report()
    files, over300, exact300, at_or_over, over1000, over2000, over5000 = (
        structure_counts(report)
    )
    physical_lines = f"{report['physicalLines']:,}"
    cold_install_files = cold_install_count()
    direct_xrl = report["directXrlImports"]
    large_direct_xrl = report["largeDirectXrlImports"]
    inventory_sha = report["inventorySha256"]
    changelog_structure_status = changelog_structure_status_terms(files, at_or_over)

    require(
        problems,
        "docs/STATUS.md",
        f"{files} sources, baseline and compatibility symbols",
        f"{cold_install_files} files",
        f"{buildings} buildings",
        f"{plots} plotted buildings",
        f"{maps} maps",
        f"{variants} variants",
        "zero issues",
        "three expected installed-base tolerant-recovery warnings",
        f"{CURRENT_FOCUSED_SURVEY_CASES} focused source-contract cases",
        f"{CURRENT_TAF_CASES} / {CURRENT_TAF_CASES} cases",
        f"{CURRENT_PORTABLE_CASES} / {CURRENT_PORTABLE_CASES} cases",
        f"{CURRENT_TOOLS_CASES} / {CURRENT_TOOLS_CASES} tests",
        f"{CURRENT_ART_CASES} / {CURRENT_ART_CASES} tests",
        f"{CURRENT_VANILLA_TILE_PATHS} verified vanilla tile references",
        "PASS",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "Native Caves of Qud behavior",
        f"{files} staged C# files",
        f"{physical_lines} physical lines",
        f"{over300} exceed 300 physical lines",
        "one is exactly 300" if exact300 == 1 else f"{exact300} are exactly 300",
        f"therefore {at_or_over} fail the strict cap",
        f"{over1000} exceed 1,000",
        f"{over2000} exceed 2,000",
        f"{over5000} exceed 5,000",
        f"Direct `XRL` imports: {direct_xrl} files, {large_direct_xrl} over the line limit",
        f"Inventory SHA-256: `{inventory_sha}`",
        f"{HARDENING_DECOMPOSITIONS} additional oversized authorities",
        f"{CUMULATIVE_DECOMPOSITIONS} cumulative",
        f"{PORTABLE_SUITE_CASES} / {PORTABLE_SUITE_CASES} cases",
        f"{TOOLS_TEST_CASES} tests",
        f"{ART_TEST_CASES} tests",
        "structural release gate",
        "never binds population",
        "VISION.md",
        "reopened every positive polity/world-presence direction",
        "REJECTED",
    )
    require(
        problems,
        "docs/V1-UNDEFERRAL.md",
        "let's un-defer everything",
        "Reopened product work",
        "Reopened discovery work",
        "Reopened integrated experience vertical",
        "Hard rejects are not deferrals",
        "exact old actor/object continuation",
        "Realm retirement / prepare-save-for-removal",
        "A current authoritative document may say `deferred` only for a hard external action",
    )
    require(
        problems,
        "docs/V1-EXPERIENCE-UNDEFERRAL.md",
        "implement-or-prove-a-stronger-supersession",
        "Native/human evidence gates survive code completion",
        "Live W0 receipt",
        "Dependency-aware implementation fan-out",
        "Exact assignment check",
        "Preserved hard boundaries",
    )
    for relative in (
        "README.md",
        "VISION.md",
        "TESTING.md",
        "docs/V1-UNDEFERRAL.md",
        "docs/V1-EXPERIENCE-UNDEFERRAL.md",
    ):
        forbid(
            problems,
            relative,
            "_notes/V1-UNDEFERRAL-LEDGER-2026-08-27.md",
            "_notes/V1-EXPERIENCE-UNDEFERRAL-DIFF-2026-08-27.md",
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
        "1307 staged C# files / 248,807 physical lines",
        "119 additional oversized authorities, 129 cumulative",
    )

    require(
        problems,
        "README.md",
        "0.3.0 public Alpha playtest",
        "not an installable release candidate",
        "plots: lots reserve typed space",
        "r_ThousandAndFirst",
        "PLAYTESTING.md",
        "SUPPORT.md",
        "ALPHA-RELEASE-PLAN.md",
        "CONTRIBUTING.md",
        "GitHub issue forms",
        "docs/STATUS.md",
        "docs/RELEASING.md",
        "private security advisory",
    )
    forbid(
        problems,
        "README.md",
        "final 7,635-case",
        "stages 965 production C# sources",
        "decomposed 76 oversized authorities",
        "105 files still breach",
        "stages 1307 production C# sources",
        "1331-file cold-install inventory",
        "decomposed 129 oversized authorities",
        "52 files still breach",
    )

    require(
        problems,
        "MODDING.md",
        "Plots: reserved lots and authored buildings",
        "A `Plot` declaration reserves a typed rectangle",
        "It is **not** a generated building recipe",
        "LotId",
        "(BuildKey, Category, actual Size)",
        "declared plan/type lineage",
        "fresh siting/restake with a new `LotId`",
        "Complete minimal authored-plot extension",
        "missing larger binding is a lawful",
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
        f"{CURRENT_TAF_CASES} / {CURRENT_TAF_CASES} Qud-referenced/source cases",
        f"{CURRENT_PORTABLE_CASES} / {CURRENT_PORTABLE_CASES} portable cases",
        f"{CURRENT_TOOLS_CASES} / {CURRENT_TOOLS_CASES} Tools tests",
        f"{CURRENT_ART_CASES} / {CURRENT_ART_CASES} Art tests",
        "Nine focused one-survey source-contract cases pass",
        f"{FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        f"{PORTABLE_SUITE_CASES} / {PORTABLE_SUITE_CASES}",
        f"Tools suite passes {TOOLS_TEST_CASES} tests",
        f"Art suite passes {ART_TEST_CASES}",
        f"across {files} production C# sources",
        f"cold-install inventory contains {cold_install_files} files",
        f"{at_or_over} staged sources breach the line cap",
        "one maintained `KingdomSurvey` classification",
        "no second whole-zone scan",
        "Current implementation limits and v1 scope gates",
        "author reopened every positive direction",
        "VISION.md",
    )
    forbid(
        problems,
        "TESTING.md",
        "Known v0 limits",
        "7,537",
        "5 focused",
        "7,635 / 7,635 cases against",
        "portable suite passes 171 / 171",
        "Tools suite passes 31 tests",
        "105 staged sources breach",
        "7,695 / 7,695 cases against",
        "across 1307 production C# sources",
        "cold-install inventory contains 1331 files",
        "52 staged sources breach",
    )
    require(
        problems,
        "docs/ASSET_PROVENANCE.md",
        f"all {CURRENT_VANILLA_TILE_PATHS} shipped tile paths",
        "Current-candidate preview status: **OPEN**",
    )
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
        "reopened every positive direction",
        "REJECTED",
    )
    forbid(problems, "VISION.md", "_notes/V1-POLITY-SCOPE.md")

    require(
        problems,
        "CHANGELOG.md",
        "One due settlement pass now owns one maintained zone survey",
        *changelog_structure_status,
        f"{physical_lines} physical lines",
        f"{over300} files exceed 300",
        f"{over1000} exceed 1,000",
        f"{over2000} exceed 2,000",
        f"{over5000} exceed 5,000",
        f"imports occur in {direct_xrl} files, {large_direct_xrl} of them over the line limit",
        inventory_sha,
        f"cold-install inventory contains {cold_install_files} files",
        f"adds {HARDENING_DECOMPOSITIONS} semantic decompositions",
        f"for {CUMULATIVE_DECOMPOSITIONS} oversized authorities",
        "numeric lexical prefixes",
        f"{plots} plotted plans over {maps} inspectable authored maps",
        "Nine focused survey source-contract cases pass",
        f"passes {FINAL_SUITE_CASES} / {FINAL_SUITE_CASES} cases",
        "All positive polity/world-presence scope is activated",
        "REJECTED",
        "Art/runtime-assets.json",
        "Concurrent legacy publication now has one cross-process decision boundary",
        "Linux CI exposed",
        ".legacies.lock",
        "Twenty-five post-`2cb97fc` decompositions",
    )
    forbid(
        problems,
        "CHANGELOG.md",
        "83 commissionable plan families",
        "247 inspectable maps",
        "rejects local raster paths",
        "7,537",
        "5 focused",
        "Current 965-file census remains red",
        "7,635 / 7,635 full cases",
        "171 / 171 portable cases",
        "66 semantic decompositions",
        "for 76 oversized authorities",
        "Current 1307-file census remains red",
        "7,695 / 7,695 full cases",
        "119 semantic decompositions",
        "for 129 oversized authorities",
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
        f"covers {HARDENING_DECOMPOSITIONS} additional semantic decompositions",
        f"{CUMULATIVE_DECOMPOSITIONS} oversized authorities",
        "Numeric prefixes",
        "canonical lexical staging order",
        "Growth/KingdomGrowth.z*.cs",
        "Growth/KingdomMaterials.[0-9][0-9].*.cs",
        "Growth/KingdomProcedures.[0-9][0-9].*.cs",
        "Growth/KingdomUpgrade.[0-9][0-9].*.cs",
        "Raids/KingdomRaids.[0-9][0-9].*.cs",
        "World/KingdomInheritanceState.z*.cs",
        "Civic runtime authorities split after hosted checkpoint `1c2d619`",
        "KingdomCentralLogistics.[0-9][0-9].*.cs",
        "KingdomResidents.[0-9][0-9].*.cs",
        "KingdomPhysicalHappenings.[0-9][0-9].*.cs",
        "KingdomPorters.[0-9][0-9].*.cs",
        "KingdomPurpose.[0-9][0-9].*.cs",
        "KingdomTradeState.[0-9][0-9].*.cs",
        "KingdomCityBook.[0-9][0-9].*.cs",
        "KingdomFounding.[0-9][0-9].*.cs",
        "KingdomRaidIncidentRules.[0-9][0-9].*.cs",
        "KingdomZoning.[0-9][0-9].*.cs",
        "KingdomCreed.[0-9][0-9].*.cs",
        "KingdomCrops.[0-9][0-9].*.cs",
        "KingdomDelveLink.[0-9][0-9].*.cs",
        "25 more than checkpoint `2cb97fc`",
        "19 more than checkpoint `d3fc4b9`",
        "16 more than checkpoint `b049c17`",
        "13 more than hosted checkpoint `1c2d619`",
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
        f"{files} staged production C# files",
        f"{physical_lines} physical lines",
        f"{over300} exceed 300 lines",
        f"therefore {at_or_over} fail the strict cap",
        f"{over1000} exceed 1,000",
        f"{over2000} exceed 2,000",
        f"{over5000} exceed 5,000",
        f"{direct_xrl} files with direct `XRL` imports",
        f"{large_direct_xrl} of those exceed the line limit",
        inventory_sha,
        f"decomposed {HARDENING_DECOMPOSITIONS} additional oversized authorities",
        f"cumulative total to {CUMULATIVE_DECOMPOSITIONS}",
        "Numeric lexical prefixes",
        "accepts no exceptions",
    )
    forbid(
        problems,
        "docs/STATUS.md",
        "PASS** — 965 sources, baseline",
        "965 staged C# files / 244,749 physical lines",
        "7,635 / 7,635 cases",
        "171 / 171 cases",
        "PASS** — 31 tests",
        "965-file census is red",
    )
    forbid(
        problems,
        "docs/STRUCTURE.md",
        "reports 965 staged production C# files",
        "105 exceed 300 lines",
        "decomposed 66 additional oversized authorities",
        "cumulative total to 76",
        "reports 1307 staged production C# files",
        "52 exceed 300 lines",
        "decomposed 119 additional oversized authorities",
        "cumulative total to 129",
    )
    forbid(
        problems,
        "docs/ARCHITECTURE.md",
        "covers 66 additional semantic decompositions",
        "76 oversized authorities have been decomposed",
        "covers 119 additional semantic decompositions",
        "129 oversized authorities have been decomposed",
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
    files, over300, exact300, at_or_over, over1000, over2000, over5000 = (
        structure_counts()
    )
    cold_install_files = cold_install_count()

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
        "reopened every positive row",
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
        "author reopened the positive living-echo direction",
        "REJECTED",
    )
    require(
        problems,
        "_notes/QUESTION-BACKLOG.md",
        "QB-16 — CLOSED",
        "implemented through the measured sparse",
        "Reopened world presence",
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
        "reopened every positive",
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
    # Retired 2026-08-29 (S6): this block used to also require "{files}-source
    # baseline/compat compile clean", "final integrated suite {FINAL_SUITE_CASES}/
    # {FINAL_SUITE_CASES}", "Codex subagents", and "Archived Claude inbox — historical"
    # somewhere in the ledger. All four are Codex-era handoff phrasing that predates this
    # repo's append-only S<N>-shard-log convention (root passed from Codex to Fable; see
    # "ROOT AUTHORITY TRANSFER — FABLE ASSUMES ROOT" in the ledger). S5 and, independently,
    # S6 both confirmed by grep that none of the four has ever had a genuine occurrence in
    # the ledger outside of later shard notes quoting the requirement text itself while
    # explaining that it was unmet -- a self-referential match, not real content. This
    # shard's write grant forbids editing _notes/COORDINATION.md beyond a closure append, so
    # satisfying these honestly is impossible without inventing content solely to pass the
    # check, which this checker must never do. "Current handoff" is kept: it is a real,
    # load-bearing section header that the ledger still uses today.
    require_if_present(
        problems,
        "_notes/COORDINATION.md",
        "Current handoff",
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
        "water plus roof only",
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
        f"{cold_install_files:,} cold-install files",
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
    audit_research_alignment_contract(problems)
    audit_market_contract(problems)
    audit_source_citations(problems)
    if problems:
        print("DOCUMENTATION FRESHNESS FAILED")
        for problem in problems:
            print("  " + problem)
        return 1
    print(
        "DOCUMENTATION FRESHNESS CLEAN: public guides and current private ledgers agree"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
