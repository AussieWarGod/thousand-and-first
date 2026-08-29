#!/usr/bin/env python3
"""Balance model for the settlement economy, re-run for the water re-grounding (Wave G1).

Four questions, in one model because they all answer to one equilibrium, plus one section of
invariants the water lane now has to keep:

  1. WATER, now that absence is charged honestly. `MaxUpkeepDaysCharged` is retired
     (BUILDING-CATALOGUE-BRIEF.md Addendum 8 clause 1), so upkeep AND fetch both run the
     full elapsed. The old cap hid whether the water economy bound at all; with it gone,
     the binding question is whether a settlement's own hauling covers its own drinking
     over an absence of ANY length. The doctrine's floor is that Camp does.
  2. REFINED MATERIALS, which the previous version of this file could not model at all
     (its own grep for `refin|timber|stone|scrap` returned nothing). The chain is priced
     against the shipped effort constants and against the catalogue's real Cost/Ticks.

  4. THE FEEDBACK LOOP between them, which section 5 closes. A lost rung ruins works, ruin
     lowers what a work carries, and the lower level pulls the settlement further down. It is
     bounded (`MaxWearPercent`, `FloorLevel`) and Q9 measures it. Re-run for Addendum 10(b):
     ruin used to reach the level ONLY through crewed works, so the loop's whole surface was
     the food lane (the one binding good that never automates). The ruling closed that door -
     every work now carries at its own condition, crewed or not - and Q9 measures what the
     change costs. It also measures the second, kind-appropriate consequence the same ruling
     added: a damaged STORE leaks what it holds, which lands on the water economy rather than
     on the level.

  3. LEVEL, which now has a consumer. `KingdomSubsidence` sums the catalogue's `Carries`
     over a settlement's finished works and hands them to
     `KingdomCatalogueRules.Equilibrium` through `KingdomSubsidenceRules.SupportedLevel`,
     which converts the water half out of drams into settlers at the settlement's own
     stage rate. That conversion is the cross-check the catalogue and `UpkeepDrams` x
     `StageUpkeepPercent` had never been put through, and sections 6 to 8 are it.

  5. THE WATER LANE'S OWN INVARIANTS (section 7), added for Addendum 11(a). Water production
     had to acquire a lore-visible reason, which in practice meant every declared dram became
     derivable from a vanilla `LiquidProducer` on the design's own blueprint and every store
     stopped declaring water at all. Three of those are things a later tuning pass could break
     without anyone noticing, so they are ASSERTED here rather than described: every water
     figure re-derives from the XML at `TicksPerDay / mean(VariableRate)`, no vessel declares
     water, and nothing in the lane is reachable at Camp while every rung above it stays
     holdable both cheaply and grandly. The leak table asserts the fourth: a vessel's capacity
     over `LeakDaysToEmptyAtCeiling` must stay under its rung's own daily bill.

  6. THE THIRD FACTOR, AND THE YARD'S OWN CONDITION (Addendum 10(b), QB-29,
     RESEARCH-SYSTEM-DESIGN 8.2). Two corrections that both move production numbers, so
     they are modelled in one pass. (a) The refining yard had never applied wear at all: it
     read the staffing pass's crew stretch and stopped there, while the crops and the
     networks folded their own condition in as the ruling requires. It folds it in now — into
     the EFFORT percent rather than into the head count, because every yard in the catalogue
     stands two and a condition folded into a head count of two truncates a damaged yard to
     nobody — and section 2 grows a wear ladder that prices the neglect. (b) The keepers'
     method (`KingdomProductionRules.Methoded`) is a THIRD factor — output = base x crew x
     wear x method — and it now reaches the city book's water and food rates and the crop
     harvest as well as the yard. It is 100 for a realm that has researched nothing, and 100
     is a no-op, so every table in this file is still a BASELINE table and not one number in
     it moved for the method: what is asserted below is that the baseline agrees and that the
     lane is a bonus and never a tax.

Every constant is read out of the C# and the model refuses to run if a body it depends on
has moved, so this cannot silently drift from the source. Run `python3 _notes/balance-sim.py`.

All three feed ONE equilibrium and are therefore re-run together, never separately: what a
rung costs in water (supply) decides how many hands are left over, what the catalogue
declares (level) decides how many people the place holds without those hands, and what a
grand design costs in shaped stock (refined) decides how long it takes to get there.
"""

from __future__ import annotations

import math
import os
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def source_family_paths(relative_directory: str, stem: str) -> tuple[str, ...]:
    """Return one root source plus its dot-named partial shards in stable filename order."""
    directory = os.path.join(ROOT, relative_directory)
    names = sorted(
        name for name in os.listdir(directory)
        if name == stem + ".cs"
        or (name.startswith(stem + ".") and name.endswith(".cs"))
    )
    if not names:
        raise SystemExit(f"source family not found: {relative_directory}/{stem}")
    return tuple(os.path.join(directory, name) for name in names)


RULES_CS = tuple(
    os.path.join(ROOT, "Core", name)
    for name in (
        "KingdomRules.cs",
        "KingdomRules.Dish.cs",
        "KingdomRules.Meals.cs",
        "KingdomRules.FoodIndustry.cs",
        "KingdomRules.Economy.cs",
        "KingdomRules.Clock.cs",
        "KingdomRules.Population.cs",
        "KingdomRules.Policy.cs",
        "KingdomRules.RaidsAndDefence.cs",
        "KingdomRules.TradeAndGrowth.cs",
        "KingdomRules.InheritanceSeal.cs",
        "KingdomRules.InheritanceResolution.cs",
        "KingdomRules.Scarcity.cs",
        "KingdomRules.Districts.cs",
        "KingdomRules.Catalogue.cs",
        "KingdomRules.Style.cs",
        "KingdomRules.RealmConflict.cs",
        "KingdomRules.Spatial.cs",
        "KingdomRules.Claims.cs",
    )
)
CROP_CS = source_family_paths("Growth", "KingdomCropRules")
MAT_CS = tuple(
    os.path.join(ROOT, "Growth", name)
    for name in (
        "KingdomMaterialRules.cs",
        "KingdomMaterialRules.Clearance.cs",
        "KingdomMaterialRules.Walls.cs",
        "KingdomMaterialRules.Refining.cs",
        "KingdomMaterialRules.Capability.cs",
        "KingdomMaterialRules.Bits.cs",
        "KingdomMaterialRules.Exotics.cs",
        "KingdomMaterialRules.Infrastructure.cs",
        "KingdomMaterialRules.Wear.cs",
    )
)
# The engine-coupled half of the yard, read only to PIN it. The arithmetic this model
# reproduces for refining is COMPOSED there rather than in the rules file, and QB-29 was
# exactly a line in there silently disagreeing with the rule it was meant to obey.
YARDIMPL_CS = source_family_paths("Growth", "KingdomMaterials")
PROD_CS = source_family_paths("Simulation/City", "KingdomProductionRules")
CITY_CS = source_family_paths("Simulation/City", "KingdomCityRules") + (
    "Simulation/City/KingdomCityAdvanceable.cs",
)
RESEARCH_CS = source_family_paths("Growth", "KingdomResearchRules")
CAT_CS = source_family_paths("Growth", "KingdomCatalogueRules")
SUB_CS = source_family_paths("Growth", "KingdomSubsidenceRules")
SUBIMPL_CS = source_family_paths("Growth", "KingdomSubsidence")
WEAR_CS = source_family_paths("Growth", "KingdomWearRules")
WEARIMPL_CS = source_family_paths("Growth", "KingdomWear")
LODGE_CS = source_family_paths("Growth", "KingdomLodgingRules")
REACH_CS = source_family_paths("Growth", "KingdomReachRules")
FOUNDATION_CS = os.path.join(ROOT, "Core", "KingdomSystem.z01.State.Foundation.cs")
SYSTEM_NORMALIZE_CS = os.path.join(ROOT, "Core", "KingdomSystem.z23.Normalization.cs")
SETTLEMENT_NORMALIZE_CS = os.path.join(ROOT, "Core", "KingdomSettlement.Normalize.cs")
YARD_CS = source_family_paths("Growth", "KingdomYardRules")
RUNTIME_DATA = os.path.join(ROOT, "RuntimeData")
YARD_XML = os.path.join(RUNTIME_DATA, "KingdomYardWorks.xml")
DEALS_XML = os.path.join(RUNTIME_DATA, "KingdomDeals.xml")
BUILD_XML = os.path.join(RUNTIME_DATA, "KingdomBuildings.xml")
BLUEPRINTS_XML = os.path.join(RUNTIME_DATA, "ObjectBlueprints.xml")
PROCEDURES_XML = os.path.join(RUNTIME_DATA, "KingdomProcedures.xml")
CIVIC_MEMORY_LIMITS_CS = os.path.join(ROOT, "Core", "KingdomCivicMemoryLimits.cs")
CIVIC_MEMORY_DERIVATION_CS = os.path.join(ROOT, "Core", "KingdomCivicMemoryDerivation.cs")
PURPOSE_KIND_CS = os.path.join(ROOT, "Growth", "KingdomPurposeKind.cs")
PURPOSE_CATALOGUE_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.Catalogue.cs")
PURPOSE_PAIR_CS = os.path.join(ROOT, "Growth", "KingdomPurposePairReceipt.cs")
PURPOSE_OPERATION_CS = os.path.join(ROOT, "Growth", "KingdomPurposeOperationReceipt.cs")
PURPOSE_ACCOUNTING_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.Accounting.cs")
PURPOSE_VALIDATION_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.OperationValidation.cs")
PURPOSE_TRANSITIONS_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.Transitions.cs")
PURPOSE_CARDINALITY_CS = os.path.join(ROOT, "Growth", "KingdomLabRules.Purpose.cs")
PURPOSE_FACTORIES_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.Factories.cs")
PURPOSE_TOPOLOGY_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.Topology.cs")
PURPOSE_RUNTIME_REGISTRY_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolio.RuntimeRegistry.cs")
PURPOSE_DRIVE_CS = os.path.join(ROOT, "Growth", "KingdomPurposePortfolio.OperationDrive.cs")
PURPOSE_CONTROL_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolio.OperationControl.cs")
PURPOSE_OUTPUT_CS = os.path.join(ROOT, "Growth", "KingdomPurposePortfolio.OutputRuntime.cs")
PURPOSE_EFFECT_RULES_CS = os.path.join(
    ROOT, "Growth", "KingdomPurposePortfolioRules.EffectStep.cs")
ANNEXE_RULES_CS = os.path.join(ROOT, "Growth", "KingdomAnnexeRules.cs")
ANNEXE_PURPOSE_CS = os.path.join(ROOT, "Growth", "KingdomAnnexe.Purpose.cs")
LAB_PURPOSE_SELECTION_CS = os.path.join(ROOT, "Growth", "KingdomLab.PurposeSelection.cs")
MIRROR_GATE_RULES_CS = os.path.join(ROOT, "Growth", "KingdomMirrorGateRules.cs")
MIRROR_GATE_PURPOSE_CS = os.path.join(ROOT, "Growth", "KingdomMirrorGate.Purpose.cs")
POWER_RULES_CS = os.path.join(ROOT, "Growth", "KingdomPowerRules.cs")
SETTLEMENT_TOPOLOGY_CS = os.path.join(
    ROOT, "Core", "KingdomSettlementTopologyRules.cs")
HOSTED_RULES_CS = os.path.join(ROOT, "Growth", "KingdomHostedArcologyRules.cs")
HOSTED_RUNTIME_CS = os.path.join(ROOT, "Growth", "KingdomHostedArcology.Runtime.cs")
HOSTED_CONSTRUCTION_CS = os.path.join(
    ROOT, "Growth", "KingdomHostedArcology.Construction.cs")
HOSTED_AUTHORITY_CS = os.path.join(ROOT, "Growth", "KingdomHostedArcology.Authority.cs")
HOSTED_LOT_CS = os.path.join(ROOT, "Growth", "KingdomHostedLotDefinition.cs")
HOSTED_VISUAL_CS = os.path.join(ROOT, "Growth", "KingdomHostedArcology.Visual.cs")
GREAT_ARCHIVE_CS = os.path.join(ROOT, "Growth", "KingdomGreatArchive.cs")
CONSTRUCTION_INPUT_RULES_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputRules.cs")
CONSTRUCTION_INPUT_DECLARATIONS_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInput.Declarations.cs")
CONSTRUCTION_INPUT_PLAN_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputPlan.Planning.cs")
CONSTRUCTION_INPUT_PLAN_VALIDATION_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputPlan.Validation.cs")
CONSTRUCTION_INPUT_STATE_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputRules.State.cs")
CONSTRUCTION_INPUT_TRANSITIONS_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputRules.Transitions.cs")
CONSTRUCTION_INPUT_RECOVERY_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputRules.Recovery.cs")
CONSTRUCTION_INPUT_COMMIT_CS = os.path.join(
    ROOT, "Growth", "KingdomConstructionInputRules.Commit.cs")
CONSTRUCTION_INPUT_DRIVE_CS = os.path.join(
    ROOT, "Growth", "KingdomConstruction.InputDrive.Open.cs")
CONSTRUCTION_INPUT_GLOBAL_RECOVERY_CS = os.path.join(
    ROOT, "Growth", "KingdomConstruction.GlobalRecovery.cs")
CONSTRUCTION_INPUT_CANCELLATION_CS = os.path.join(
    ROOT, "Growth", "KingdomConstruction.InputDrive.Cancellation.cs")
CENTRAL_CONSTRUCTION_RESERVATION_CS = os.path.join(
    ROOT, "Simulation", "City", "KingdomCentralLogistics.09.ConstructionInputReservation.cs")
LOGISTICS_RULES_CS = os.path.join(ROOT, "Simulation", "City", "KingdomLogisticsRules.cs")
CITY_MEMORY_RULES_CS = os.path.join(ROOT, "Simulation", "City", "KingdomCityMemoryRules.cs")
ITINERARY_RULES_CS = os.path.join(ROOT, "Simulation", "City", "KingdomItineraryRules.cs")
DISTANCE_RULES_CS = os.path.join(ROOT, "Simulation", "City", "KingdomDistanceRules.cs")
JOB_DRAWS_CS = os.path.join(ROOT, "Simulation", "City", "KingdomJobRegistry.z06.Draws.cs")
PORTERS_OPENING_CS = os.path.join(ROOT, "Simulation", "City", "KingdomPorters.00.Opening.cs")
CENTRAL_ROUTE_CS = os.path.join(
    ROOT, "Simulation", "City", "KingdomCentralLogistics.06.ManifestOwnershipAndRoute.cs")
CENTRAL_LEGS_CS = os.path.join(
    ROOT, "Simulation", "City", "KingdomCentralLogistics.07.RouteSegmentsAndPassages.cs")


# --------------------------------------------------------------------------------------
# 1. Read the real constants out of the source, and refuse to run if they have moved.
# --------------------------------------------------------------------------------------


def read_source(path: str | tuple[str, ...]) -> str:
    paths = (path,) if isinstance(path, str) else path
    return "\n".join(open(item, encoding="utf-8-sig").read() for item in paths)


def source_label(path: str | tuple[str, ...]) -> str:
    return path if isinstance(path, str) else ", ".join(path)


def read_const(path: str | tuple[str, ...], name: str) -> int:
    text = read_source(path)
    m = re.search(r"const\s+(?:int|long)\s+" + name + r"\s*=\s*([0-9]+)", text)
    if not m:
        raise SystemExit(f"constant {name} not found in {source_label(path)}")
    return int(m.group(1))


SRC = {
    "DramsPerArrival": read_const(RULES_CS, "DramsPerArrival"),
    "MaxArrivalsPerVisit": read_const(RULES_CS, "MaxArrivalsPerVisit"),
    "DryIntervalsToEmigrate": read_const(RULES_CS, "DryIntervalsToEmigrate"),
    "DryIntervalsToWither": read_const(RULES_CS, "DryIntervalsToWither"),
    "LoyalCoreSettlers": read_const(RULES_CS, "LoyalCoreSettlers"),
    "MaxPopulation": read_const(RULES_CS, "MaxPopulation"),
    "MaxBuildings": read_const(RULES_CS, "MaxBuildings"),
    "FoundingCostDrams": read_const(RULES_CS, "FoundingCostDrams"),
    "FetchDramsPerSettler": read_const(RULES_CS, "FetchDramsPerSettler"),
    "TicksPerDay": read_const(RULES_CS, "TicksPerDay"),
    # The retired cap's two jobs, now split. ReserveDays is a cushion DEPTH and never a
    # clock, and it is the only half that survived: P1 named the remaining capped counters
    # LegacyAbsenceCap and P4 retired that too, so nothing in the source caps elapsed time
    # any more. The old value lives on below as RETIRED_CAP_DAYS, a literal and not a rule.
    "ReserveDays": read_const(RULES_CS, "ReserveDays"),
    "RaisingHandsWanted": read_const(RULES_CS, "RaisingHandsWanted"),
    "RaidTributeDrams": read_const(RULES_CS, "RaidTributeDrams"),
    "RaidPlunderDrams": read_const(RULES_CS, "RaidPlunderDrams"),
    "PlantWaterCostDrams": read_const(CROP_CS, "PlantWaterCostDrams"),
    # Wave G2, Addendum 11(b): the food lane's denomination is now a CYCLE. A design stands
    # `Rows` rows, a row yields `YieldPerRow` when it is gathered, and a crop stands `CropDays`
    # days before it is. Those three are the whole of it, and `water_invariants`' food sibling
    # below re-derives every food design's declared `Carries` from them plus the rows tag on its
    # own blueprint - exactly as the water half re-derives from `LiquidProducer`.
    "CropDays": read_const(CROP_CS, "CropDays"),
    "YieldPerRow": read_const(CROP_CS, "YieldPerRow"),
    "SeedReturnChancePercent": read_const(CROP_CS, "SeedReturnChancePercent"),
    # Wave G3, Addendum 11(b)+(c): the meal and the mill. `PreserveMultiple` is vanilla's own
    # Vinewafer -> Vinewafer Sheaf figure; `MillCropsPerDay` is the batch that makes the grinding
    # mill's declared `food` come out exactly right; `FavoredMealShade` is what one day of eating
    # the settlement's own dish is worth to the level, and `FavoredMealPercent` is how much of the
    # day has to come off that dish before it counts as having been eaten.
    "PreserveMultiple": read_const(RULES_CS, "PreserveMultiple"),
    "MillCropsPerDay": read_const(RULES_CS, "MillCropsPerDay"),
    "FavoredMealShade": read_const(RULES_CS, "FavoredMealShade"),
    "FavoredMealPercent": read_const(RULES_CS, "FavoredMealPercent"),
    "MaxCyclesPerVisit": read_const(CROP_CS, "MaxCyclesPerVisit"),
    "EffortPerHandPerDay": read_const(MAT_CS, "EffortPerHandPerDay"),
    "StrikeBaseEffort": read_const(MAT_CS, "StrikeBaseEffort"),
    "StrikeEffortPerUnit": read_const(MAT_CS, "StrikeEffortPerUnit"),
    "RefineEffortPerUnit": read_const(MAT_CS, "RefineEffortPerUnit"),
    "RawPerRefined": read_const(MAT_CS, "RawPerRefined"),
    "MaxRefinedPerDay": read_const(MAT_CS, "MaxRefinedPerDay"),
    "MaxClearingHands": read_const(MAT_CS, "MaxClearingHands"),
    # Level and subsidence, from the package that consumes the catalogue's Carries.
    "LiftCapPercent": read_const(CAT_CS, "LiftCapPercent"),
    # A household's authored yard trade still rides the common lift term. Civic-office taste,
    # virtue, flaw, and preference constants are deliberately absent: title-only offices may not
    # enter the economy, and legacy NotableShade is retired during normalization below.
    "MaxShadePerWork": read_const(YARD_CS, "MaxShadePerWork"),
    "FloorLevel": read_const(CAT_CS, "FloorLevel"),
    "StartMarginPercent": read_const(SUB_CS, "StartMarginPercent"),
    "StageFallMarginPercent": read_const(SUB_CS, "StageFallMarginPercent"),
    "StepDays": read_const(SUB_CS, "StepDays"),
    # Addendum 10(c) retired the flat quota (`RuinedWorksPerBreakpoint`, two works a rung) for
    # a reach every standing work is asked against instead. `RuinChancePercent` now NAMES the
    # widest rung's own reach (City's, 50%) rather than a flat chance every rung shared, and
    # `ruin_chance_for` below scales it down the same way `RuinChanceFor` does.
    "RuinChancePercent": read_const(SUB_CS, "RuinChancePercent"),
    # The feedback loop section 5 closes: how far a work can be run down, and how far
    # one lost rung runs it.
    "MaxWearPercent": read_const(MAT_CS, "MaxWearPercent"),
    # Addendum 10(b): a store at the wear ceiling loses its whole capacity to the ground in
    # this many world days. The one number the leak is tuned on.
    "LeakDaysToEmptyAtCeiling": read_const(WEAR_CS, "LeakDaysToEmptyAtCeiling"),
    "RuinStandingFloorPercent": read_const(RULES_CS, "RuinStandingFloorPercent"),
    "RuinStandingCeilingPercent": read_const(RULES_CS, "RuinStandingCeilingPercent"),
    # Wave B: food became a flow. The mirror of the water constants above, and where the mirror
    # deliberately breaks (no stage rate, no stores policy) is Q11's whole first paragraph.
    "ForageRationsPerHand": read_const(RULES_CS, "ForageRationsPerHand"),
    "MaxForagedRationsPerDay": read_const(RULES_CS, "MaxForagedRationsPerDay"),
    "HungryIntervalsToEmigrate": read_const(RULES_CS, "HungryIntervalsToEmigrate"),
    "HungryIntervalsToFamine": read_const(RULES_CS, "HungryIntervalsToFamine"),
    "DefaultLarderCapacity": read_const(RULES_CS, "DefaultLarderCapacity"),
}

_rules_text = read_source(RULES_CS)

# Method bodies this model reproduces. Pinned so a change to any of them breaks the model
# loudly instead of quietly invalidating it.
_PINS = [
    # UpkeepDrams(pop, stage) = pop * stage% / 100
    ("int percent = StageUpkeepPercent[(int)Stage];", "UpkeepDrams body changed"),
    ("return Population * percent / 100;", "UpkeepDrams body changed"),
    # ArrivalIntervalTicks(pop)
    ("return 3600 + 600L * Population;", "ArrivalIntervalTicks body changed"),
    # FetchableDrams: hands x per-settler x days, uncapped days, bounded by pool and room
    (
        "long num = (long)Hands * FetchDramsPerSettler * Days;",
        "FetchableDrams body changed",
    ),
    # The uncapping itself. If either of these stops being true, this whole model is
    # describing rules the game no longer has.
    (
        "return SaturateToInt(ElapsedDays(ElapsedTicks) * (long)UpkeepDrams(Population));",
        "UpkeepForElapsed is no longer uncapped",
    ),
    (
        "return SaturateToInt(PolicyUpkeep(UpkeepDrams(Population, Stage), Stores) * (long)ElapsedDays(ElapsedTicks));",
        "PolicyUpkeepForElapsed is no longer uncapped",
    ),
    # --- the food lane (Wave B) ----------------------------------------------------------
    # RationsPerDay is flat, and the flatness is the whole reason the food arm of Equilibrium
    # can be read straight off as a daily bill. A stage term appearing here would silently
    # invalidate every food figure in the catalogue and every number in Q11.
    (
        "return (Population > 0) ? Population : 0;",
        "RationsPerDay is no longer one ration a settler a day - the food lane's denomination moved",
    ),
    (
        "return SaturateToInt(ElapsedDays(ElapsedTicks) * (long)RationsPerDay(Population));",
        "RationsForElapsed is no longer uncapped",
    ),
    # Foraging's ceiling is applied to the RATE, before the days multiply out. If that order
    # inverts, a long absence forages a season's worth in one go and Camp stops being the only
    # rung the wild can carry.
    (
        "long rate = (long)Hands * ForageRationsPerHand;",
        "ForagedRations body changed",
    ),
    (
        "return SaturateToInt(rate * Days);",
        "ForagedRations no longer clamps the rate before multiplying the days",
    ),
    # The composition rule: the worse of the two ladders, never their sum.
    (
        "verdict.Bite = (fromThirst > fromHunger) ? fromThirst : fromHunger;",
        "ComposeScarcity no longer takes the maximum - a dry AND starving city may now double-collapse",
    ),
]
for needle, complaint in _PINS:
    assert needle in _rules_text, complaint

_sub_text = read_source(SUB_CS)
_cat_text = read_source(CAT_CS)
_reach_text = read_source(REACH_CS)
_foundation_text = read_source(FOUNDATION_CS)
_system_normalize_text = read_source(SYSTEM_NORMALIZE_CS)
_settlement_normalize_text = read_source(SETTLEMENT_NORMALIZE_CS)
_SUB_PINS = [
    (
        _sub_text,
        "return (percent <= 100) ? Water : (Water * 100 / percent);",
        "LevelFromWater body changed - the drams-to-settlers conversion moved",
    ),
    (
        _sub_text,
        "return index + 1;",
        "SettlersPerStep body changed",
    ),
    (
        _sub_text,
        "return level + ((margin < 1) ? 1 : margin);",
        "SlideBeginsAbove body changed",
    ),
    (
        _cat_text,
        "int cap = least * LiftCapPercent / 100;",
        "Equilibrium's lift cap moved; it is supposed to be frozen",
    ),
    (
        _cat_text,
        "return (level < FloorLevel) ? FloorLevel : level;",
        "Equilibrium's floor moved; it is supposed to be frozen",
    ),
    (
        _cat_text,
        "int lift = ((Lift < 0) ? 0 : Lift) + ((Shade < 0) ? 0 : Shade);",
        "Equilibrium's transient shade term moved - attended meal lift belongs under the cap",
    ),
    (
        _cat_text,
        "public static int Equilibrium(int Water, int Food, int Roof, int Lift, int Shade)",
        "Equilibrium's signature moved; this model mirrors it argument for argument",
    ),
    (
        _reach_text,
        "return Scaled(Amount, reached * 100 / Homes);",
        "Landed body changed - what share of a lift reaches the level moved",
    ),
    (
        _reach_text,
        "int scaled = Amount * Percent / 100;\n\t\t\treturn (scaled < 1) ? 1 : scaled;",
        "Scaled body changed - the floor Landed inherits moved",
    ),
    (
        _sub_text,
        "LevelFromWater(Supports.Water, Stage), Supports.Food, Supports.Roof, Supports.Lift, Shade);",
        "SupportedLevel no longer hands attended transient shade to the frozen arithmetic",
    ),
    (
        _sub_text,
        "return RuinChancePercent * (index + 1) / ((int)GrowthStage.City + 1);",
        "RuinChanceFor body changed - the reach rule Addendum 10(c) shipped moved",
    ),
]
for text, needle, complaint in _SUB_PINS:
    assert needle in text, complaint

assert "return (MealShade < 0) ? 0 : MealShade;" in _foundation_text, (
    "KingdomSystem.Shade no longer excludes the legacy civic-office modifier"
)
assert "NotableShade = 0;" in _system_normalize_text, (
    "seat normalization no longer retires legacy civic-office economy"
)
assert "NotableShade = 0;" in _settlement_normalize_text, (
    "off-seat normalization no longer retires legacy civic-office economy"
)

# The retired constants may still be NAMED in the comments that mark where they lived (and
# should be); what must not come back is either declaration. MaxUpkeepDaysCharged went in P1,
# LegacyAbsenceCap - the holding pen P1 left for the counters it had not reached - in P4.
for _gone in ("MaxUpkeepDaysCharged", "LegacyAbsenceCap"):
    assert not re.search(r"const\s+int\s+" + _gone, _rules_text), (
        _gone + " is declared again; this model assumes elapsed time is charged in full"
    )

# What both of them held, kept as a plain number so the CAPPED-versus-UNCAPPED comparison
# below still has something to compare against. It is history, not a rule: no source file
# declares it any more, which is why it cannot be read out of one.
RETIRED_CAP_DAYS = 3

_stage_pct = re.search(
    r"StageUpkeepPercent\s*=\s*new int\[5\]\s*\{([^}]*)\}", _rules_text
)
assert _stage_pct, "StageUpkeepPercent table not found"
STAGE_PERCENT = tuple(int(x) for x in _stage_pct.group(1).split(","))

_mat_text = read_source(MAT_CS)
# Widened to long when the yard came off the visit clock: a big crew over a long stretch
# leaves int behind long before the stock or the rate do.
assert (
    "long effort = (long)Crew * Days * EffortPerHandPerDay * capability / 100L;"
    in _mat_text
), "RefinedThisPass body changed"
assert "long units = effort / RefineEffortPerUnit;" in _mat_text, (
    "RefinedThisPass body changed"
)
# The ceiling is a RATE, not a per-resolve bound. If this line goes back to a bare constant
# the yard is gated on homecomings again and Q4's whole reading is wrong.
assert "long ceiling = (long)MaxRefinedPerDay * Days;" in _mat_text, (
    "RefinedThisPass' throughput ceiling is no longer denominated per day"
)

# Mending uses the same uncapped effort clock as clearing and striking. These bodies are
# pinned together because an apparently harmless visit cap in either half would make a
# month away mend less than a month watched, while moving the hands gate would let time mend
# a work nobody is tending. Q4b prices the resulting schedules.
_wearimpl_text = read_source(WEARIMPL_CS)
_MENDING_PINS = [
    (
        _mat_text,
        "long effort = hands * Days * EffortPerHandPerDay;",
        "EffortWorked no longer multiplies the full day count by the bounded gang",
    ),
    (
        _mat_text,
        "return (effort > int.MaxValue) ? int.MaxValue : (int)effort;",
        "EffortWorked no longer saturates before returning to a long-absence caller",
    ),
    (
        _mat_text,
        "int effort = StrikeBaseEffort / 2 + units * StrikeEffortPerUnit;",
        "RepairEffort's material schedule moved",
    ),
    (
        _wearimpl_text,
        "if (Hands <= 0)",
        "the repair worker no longer stops before time can advance an unstaffed mend",
    ),
    (
        _wearimpl_text,
        "int left = WearPart.RepairEffortLeft - KingdomMaterialRules.EffortWorked(Hands, days);",
        "repair completion no longer spends the common hands-times-days effort law",
    ),
]
for text, needle, complaint in _MENDING_PINS:
    assert needle in text, complaint

# The feedback loop's own six bodies. Section 5 reproduces each of them exactly. The fifth and
# sixth are the ones that decide the whole shape of the answer, and Addendum 10(b) moved them:
# `Supports` now folds EVERY work at `KingdomWearRules.WorkEffectiveness`, whose staffless arm
# is the work's own condition rather than a flat 100. Wear reaches the level through staffless
# designs now, which is the ruling this model was re-pinned for.
_wear_text = read_source(WEAR_CS)
_subimpl_text = read_source(SUBIMPL_CS)
_FEEDBACK_PINS = [
    (_mat_text, "return 100 - wear;", "ConditionPercent body changed"),
    (
        _wear_text,
        "return stretch * KingdomMaterialRules.ConditionPercent(Wear) / 100;",
        "CombinedEffectiveness body changed - crew stretch and wear no longer multiply",
    ),
    (
        _sub_text,
        "int increment = KingdomMaterialRules.MaxWearPercent * (100 - standing) / 200;",
        "RuinIncrement body changed - what one lost rung costs a work moved",
    ),
    (
        _rules_text,
        "return RuinStandingCeilingPercent - roll * (RuinStandingCeilingPercent - RuinStandingFloorPercent) / 99;",
        "StandingPercent body changed - the adversity ramp RuinIncrement reads moved",
    ),
    (
        _cat_text,
        "return (EffectivenessPercent >= 100) ? Amount : (Amount * EffectivenessPercent / 100);",
        "Carried body changed - what a work running short actually contributes moved",
    ),
    (
        _subimpl_text,
        "int effectiveness = KingdomWear.EffectivenessOf(work);\n"
        "\t\t\t\ttally = KingdomCatalogueRules.FoldWork(tally, carries, effectiveness);",
        "Supports no longer folds every work at WorkEffectiveness; section 5 assumes it does",
    ),
    (
        _subimpl_text,
        "tally = KingdomCatalogueRules.FoldShade(tally, YardShadesOf(work), effectiveness);",
        "Supports no longer folds a household's yard trade into the level; section 6 counts it",
    ),
    (
        _subimpl_text,
        "tally.Lift = scoped;",
        "ScopedSupports no longer replaces the citywide lift with the reach-scoped one; "
        "section 6's coverage table assumes it does",
    ),
    (
        _wear_text,
        "\t\t\t\t? CombinedEffectiveness(CrewStretch, Wear)\n"
        "\t\t\t\t: KingdomMaterialRules.ConditionPercent(Wear);",
        "WorkEffectiveness body changed - the staffless arm of Addendum 10(b)'s ruling moved",
    ),
    # The kind-appropriate consequence: what a damaged STORE goes on losing. Section 5's
    # `leaked` reproduces this line for line.
    (
        _wear_text,
        "\t\t\tlong lost = (long)Capacity * wear * Days\n"
        "\t\t\t\t/ ((long)KingdomMaterialRules.MaxWearPercent * LeakDaysToEmptyAtCeiling);",
        "Leaked body changed - the leak rate moved",
    ),
]
for text, needle, complaint in _FEEDBACK_PINS:
    assert needle in text, complaint

# The third factor, and the yard's own condition. Both are composed in engine-coupled source
# rather than in a rules file, so both are PINNED rather than described: QB-29 was precisely
# the case of one of these lines disagreeing with the rule it was meant to be obeying, for
# the whole life of the mod, with nothing anywhere to notice.
_yardimpl_text = read_source(YARDIMPL_CS)
_prod_text = read_source(PROD_CS)
_city_text = read_source(CITY_CS)
_crop_text = read_source(CROP_CS)
_METHOD_PINS = [
    (
        _yardimpl_text,
        "int conditioned = capability * KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Yard)) / 100;",
        "the yard no longer applies its own wear - QB-29 has regressed and the wear ladder below is fiction",
    ),
    (
        _yardimpl_text,
        "int methoded = KingdomProductionRules.Methoded(conditioned, MethodPercent);",
        "the yard's third factor moved, or no longer rides the conditioned percent",
    ),
    (
        _yardimpl_text,
        'int crew = Yard.GetIntProperty("KingdomStaffNeeded") * Yard.GetIntProperty("KingdomEffectiveness") / 100;',
        "the yard's crew is no longer a bare head count off the staffing pass; the wear ladder "
        "assumes condition rides the EFFORT and never the count",
    ),
    (
        _prod_text,
        "int method = (methodPercent < BaselineMethodPercent) ? BaselineMethodPercent : methodPercent;",
        "Methoded is no longer a bonus lane - a realm that abstained can now be taxed",
    ),
    (
        _prod_text,
        "long scaled = (long)quantity * method / BaselineMethodPercent;",
        "Methoded body changed - what the keepers' method is worth moved",
    ),
    (
        _city_text,
        "return Methoded(RateOf(waterRatePerDay, index, row.WaterCarry));",
        "the city book's water rate no longer carries the method factor",
    ),
    (
        _city_text,
        "return Methoded(RateOf(foodRatePerDay, index, row.FoodCarry));",
        "the city book's food rate no longer carries the method factor",
    ),
    (
        _crop_text,
        "return KingdomProductionRules.Methoded((yield > int.MaxValue) ? int.MaxValue : (int)yield, MethodPercent);",
        "HarvestYield no longer carries the method factor",
    ),
]
for text, needle, complaint in _METHOD_PINS:
    assert needle in text, complaint

# Why every table in this file is still a baseline table: an empty roster answers the
# baseline, and the baseline is a no-op through Methoded.
BASELINE_METHOD = read_const(PROD_CS, "BaselineMethodPercent")
MAX_METHOD = read_const(RESEARCH_CS, "MaxMethodPercent")
assert BASELINE_METHOD == 100, (
    "the baseline method is no longer a hundred; every production table below moved with it"
)
assert MAX_METHOD >= BASELINE_METHOD, (
    "the method ceiling sits under the baseline, which would make the tree a tax"
)

# The loop's SECOND consequence, and the one that reaches works no crew ever stood in. A home
# worn this far stops counting as a roof for lodging - not for the level, which is a different
# reckoning. Derived rather than read, because the source derives it too.
assert (
    "public const int CondemnedWearPercent = 100 - KingdomRules.RuinStandingCeilingPercent;"
    in read_source(LODGE_CS)
), "CondemnedWearPercent is no longer the complement of the standing ceiling"
CONDEMNED_WEAR = 100 - SRC["RuinStandingCeilingPercent"]

# The material vocabulary, in enum order, read rather than guessed. The last three are the
# refined ones (KingdomMaterial.ShapedTimber / ShapedStone / WorkedMetal); everything
# before them comes off cleared ground.
_keys = re.search(
    r"MaterialKeys\s*=\s*new string\[MaterialCount\]\s*\{([^}]*)\}", _mat_text
)
assert _keys, "MaterialKeys table not found"
MATERIAL_KEYS = [k.strip().strip('"') for k in _keys.group(1).split(",")]
REFINED_KEYS = {"shapedtimber", "shapedstone", "workedmetal"}
assert REFINED_KEYS <= set(MATERIAL_KEYS), "the refined vocabulary moved"
RAW_KEYS = set(MATERIAL_KEYS) - REFINED_KEYS

# Charters, from the shipped registry.
_deals = re.findall(
    r'Income="(\d+)"\s+Interval="(\d+)"', open(DEALS_XML, encoding="utf-8-sig").read()
)
CHARTERS = [(int(i), int(t)) for i, t in _deals]  # (drams, ticks)

# Stage gates, read from StageFor.
STAGES = [  # (name, min_pop, min_storage_capacity)
    ("Camp", 0, 0),
    ("Steading", 5, 16),
    ("Village", 12, 64),
    ("Town", 25, 256),
    ("City", 50, 1024),
]


# --------------------------------------------------------------------------------------
# 2. Rule sets. CAPPED is what shipped before this wave; UNCAPPED is the doctrine.
# --------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Rules:
    name: str
    # None means "no ceiling": the whole elapsed is charged / fetched.
    max_upkeep_days: int | None
    fetch_max_days: int | None


CAPPED = Rules("capped (pre-rework)", RETIRED_CAP_DAYS, RETIRED_CAP_DAYS)
UNCAPPED = Rules("uncapped (doctrine)", None, None)


def stage_index(pop: int, capacity: int) -> int:
    idx = 0
    for i, (_n, mp, mc) in enumerate(STAGES):
        if pop >= mp and capacity >= mc:
            idx = i
    return idx


def upkeep_per_day(pop: int, capacity: int = 1024) -> int:
    """Integer drams/day, exactly as UpkeepDrams(pop, stage) would floor it."""
    return pop * STAGE_PERCENT[stage_index(pop, capacity)] // 100


def charged_days(r: Rules, elapsed: int) -> int:
    if r.max_upkeep_days is None:
        return elapsed
    return min(elapsed, r.max_upkeep_days)


def fetched_days(r: Rules, elapsed: int) -> int:
    if r.fetch_max_days is None:
        return elapsed
    return min(elapsed, r.fetch_max_days)


def daily_balance(
    r: Rules,
    pop: int,
    watered: bool,
    accounting_span: int,
    water_crew: int,
    capacity: int = 1024,
    charters: int = 0,
) -> dict:
    """Drams per day in and out, averaged over one elapsed accounting span.

    `water_crew` is System.WaterCrew: hands the founder put on the detail, and only those
    hands fetch. Everyone else is drinking without hauling. `accounting_span` is not the
    current visit cadence: it exists to expose the retired cap's distortion against the
    current uncapped elapsed arithmetic.
    """
    up = upkeep_per_day(pop, capacity)
    upkeep_rate = up * charged_days(r, accounting_span) / accounting_span

    hands = min(water_crew, pop)
    fetch_rate = (
        hands * SRC["FetchDramsPerSettler"] * fetched_days(r, accounting_span) / accounting_span
        if watered
        else 0.0
    )
    charter_rate = sum(
        CHARTERS[i][0] / (CHARTERS[i][1] / SRC["TicksPerDay"]) for i in range(charters)
    )
    arrival_rate = (
        SRC["DramsPerArrival"] / (3.0 + 0.5 * pop)
        if pop < SRC["MaxPopulation"]
        else 0.0
    )

    inflow = fetch_rate + charter_rate
    outflow = upkeep_rate + arrival_rate
    return dict(
        upkeep=upkeep_rate,
        fetch=fetch_rate,
        charter=charter_rate,
        arrivals=arrival_rate,
        inflow=inflow,
        outflow=outflow,
        surplus=inflow - outflow,
        ratio=(fetch_rate / upkeep_rate) if upkeep_rate else math.inf,
    )


def break_even_crew(pop: int, capacity: int = 1024) -> int:
    """Hands on the detail that make an uncapped settlement neutral on water alone."""
    return math.ceil(upkeep_per_day(pop, capacity) / SRC["FetchDramsPerSettler"])


# --------------------------------------------------------------------------------------
# 3. Reports
# --------------------------------------------------------------------------------------


def rule(title):
    print("\n" + "=" * 86)
    print(title)
    print("=" * 86)


def q1_headline():
    rule("Q1  The uncapped water balance: pop 5 / 20 / 40, watered and dry")
    print("""
Watered = pure open water standing in the zone, more than the detail can carry. Dry = none.
Absence length does not appear in this table on purpose: with both halves uncapped, the
per-day rate is the same at V=1 and V=400, which is the whole point of the change. Crew
columns are System.WaterCrew - hands the founder put on the detail.
""")
    print(
        f"{'pop':>4} {'stage':<9} {'ground':<8} {'upkeep/d':>9} "
        f"{'crew=0':>9} {'crew=half':>11} {'crew=all':>10} {'break-even crew':>16}"
    )
    for pop in (5, 20, 40):
        for watered in (True, False):
            up = upkeep_per_day(pop)
            st = STAGES[stage_index(pop, 1024)][0]
            cells = []
            for crew in (0, pop // 2, pop):
                b = daily_balance(UNCAPPED, pop, watered, 1, crew)
                cells.append(b["surplus"])
            need = break_even_crew(pop)
            need_s = f"{need} of {pop}" if need <= pop else f"impossible ({need})"
            print(
                f"{pop:>4} {st:<9} {'watered' if watered else 'DRY':<8} {up:>9} "
                f"{cells[0]:>+9.2f} {cells[1]:>+11.2f} {cells[2]:>+10.2f} "
                f"{(need_s if watered else '-'):>16}"
            )

    print("\nSurplus/day by elapsed accounting span, watered, half the settlement on the detail.")
    print("CAPPED rows are the pre-rework rules; UNCAPPED are the doctrine's.\n")
    print(
        f"{'pop':>4} {'rules':<22}"
        + "".join(f"{'V=' + str(v):>11}" for v in (1, 3, 7, 30, 400))
    )
    for pop in (5, 20, 40):
        for r in (CAPPED, UNCAPPED):
            row = f"{pop:>4} {r.name:<22}"
            for v in (1, 3, 7, 30, 400):
                b = daily_balance(r, pop, True, v, pop // 2)
                row += f"{b['surplus']:>+11.2f}"
            print(row)
    print("""
Read: under the cap, a long absence flattened BOTH halves toward zero, so the rate drifted
to nothing and the settlement neither gained nor lost - absence was inert, which is what
the doctrine calls forgiveness. The V columns are accounting spans, not the current visit
schedule. While the founder remains on claimed ground, the canonical physical pass runs at
absolute daily boundaries; while no claimed ground is attended, every city book still advances
on the heartbeat and physical stores reconcile on the next attended pass. Uncapped arithmetic
keeps the same elapsed rate whichever way that span is partitioned, which is clause 1 in one
number.""")


def q2_floor():
    rule("Q2  The floor: is Camp self-sustaining with the cap gone?")
    print("""
The doctrine's floor is Camp's own equilibrium: whatever else subsides, the smallest
settlement carries itself. At Camp the stage multiplier is 100%, so a settler drinks one
dram a day and a hand on the detail carries two - the ratio is 2:1 and half the camp on
water is exactly break-even.
""")
    print(
        f"{'pop':>4} {'stage':<9}{'drinks/d':>10}{'crew needed':>13}{'of pop':>9}  verdict"
    )
    for pop in range(2, 13):
        need = break_even_crew(pop)
        st = STAGES[stage_index(pop, 1024)][0]
        ok = "carries itself" if need <= pop else "CANNOT"
        print(
            f"{pop:>4} {st:<9}{upkeep_per_day(pop):>10}{need:>13}{need / pop:>9.0%}  {ok}"
        )

    print(
        "\nAnd at every size, watered ground, the share of the settlement the detail wants:"
    )
    print(f"{'pop':>4} {'stage':<9}{'drinks/d':>10}{'crew needed':>13}{'share':>8}")
    for pop in (5, 12, 25, 40, 50, 60):
        need = break_even_crew(pop)
        st = STAGES[stage_index(pop, 1024)][0]
        print(f"{pop:>4} {st:<9}{upkeep_per_day(pop):>10}{need:>13}{need / pop:>8.0%}")
    print("""
The stage ramp is the whole difficulty curve: Camp wants half its people on water, a City
wants 110% of them and therefore cannot be held by hauling at all. That is intended - the
city is meant to need infrastructure - but note what it means with the cap gone: the elapsed
bill continues instead of stopping after three days. The shipped bounds are two different
clocks. Subsidence folds every elapsed four-day step toward the supported level. Immediate
scarcity steps once per canonical physical resolve: daily while the founder remains on
claimed ground, once over the whole elapsed span when ground is next attended after an
absence (see Q3 and Q8).""")


def q3_ladder():
    rule("Q3  What scarcity and subsidence do with elapsed time")
    print("""
One long unattended span and the same span watched day by day are deliberately not the same
thing for immediate scarcity. `KingdomGrowth.ResolveHeartbeat` bills all elapsed days in the
next attended pass but steps `DryStreak` once per FAILED RESOLVE, not once per billed day.
While the founder remains on claimed ground, `KingdomSemanticDispatcher` supplies one such
resolve at each absolute daily boundary. Each resolve can walk out at most one settler, and
`Emigrate` floors at LoyalCoreSettlers.
""")
    print(
        f"{'elapsed days':>15}{'catch-up bill at pop 20':>25}{'away scarcity':>16}"
        f"{'watched resolves':>18}{'slide steps due':>17}"
    )
    for days in (1, 3, 10, 30, 90, 400):
        owed = upkeep_per_day(20) * days
        # One immediate scarcity resolve when an unattended physical span is reconciled.
        away_steps = 1
        watched_steps = days
        slide_steps = days // SRC["StepDays"]
        print(f"{days:>15}{owed:>25}{away_steps:>16}{watched_steps:>18}{slide_steps:>17}")
    print(f"""
    DryIntervalsToEmigrate = {SRC["DryIntervalsToEmigrate"]}, DryIntervalsToWither = {SRC["DryIntervalsToWither"]},
    LoyalCoreSettlers = {SRC["LoyalCoreSettlers"]}. The `away scarcity` column therefore means
    one warning from a fresh dry streak, not one automatic departure; repeated failed resolves
    can still walk people out down to the loyal core. The watched column is opportunities, not
    a prediction of total departures: population, stores and support all change between passes.

    Subsidence is separate and already shipped. Its own uncapped clock may cash every whole
    {SRC["StepDays"]}-day step shown above in the same homecoming reckoning, shedding multiple
    settlers toward what the works carry. Q8 computes those structural trajectories. Thus a long
    absence is bounded, but not by the old claim that a homecoming can cost only one settler.""")


def q4_refined():
    rule("Q4  The refined-material chain, priced")
    print(f"""
The chain: raw (timber / stone / scrap, off cleared ground) -> refined (shaped timber,
shaped stone, worked metal, through a staffed yard) -> spent on building costs.

    EffortPerHandPerDay  = {SRC["EffortPerHandPerDay"]}      one hand, one day
    RefineEffortPerUnit  = {SRC["RefineEffortPerUnit"]}      effort one refined unit costs
    RawPerRefined        = {SRC["RawPerRefined"]}      raw loads eaten per refined unit
    MaxRefinedPerDay     = {SRC["MaxRefinedPerDay"]}      throughput ceiling, per DAY of running
    MaxClearingHands     = {SRC["MaxClearingHands"]}      most hands one clearing gang uses
""")
    def refined_units(crew: int, days: int, percent: int = 100) -> int:
        """`KingdomMaterialRules.RefinedThisPass` over unlimited stock.

        `percent` is the EFFORT percent the bench hands in, which
        `KingdomMaterials.WorkYard` composes in this order and no other: the crew's own
        capability, scaled by the work's CONDITION, then lifted by the keepers' METHOD.
        A hundred is an ordinary crew at a sound bench in a realm that has researched
        nothing, and a hundred is what every other table in this file assumes.
        """
        effort = crew * days * SRC["EffortPerHandPerDay"] * percent // 100
        return min(effort // SRC["RefineEffortPerUnit"], SRC["MaxRefinedPerDay"] * days)

    DAY_COLUMNS = (1, 2, 3, 5, 10, 30)
    print("Refined units a yard turns out, by crew and days (effort 100%):\n")
    print(f"{'crew':>5} " + "".join(f"{'d=' + str(d):>8}" for d in DAY_COLUMNS))
    for crew in (1, 2, 3, 4, 6):
        row = f"{crew:>5} "
        for d in DAY_COLUMNS:
            row += f"{refined_units(crew, d):>8}"
        print(row)
    print(f"""
    THIS TABLE IS THE UNCAPPING, VISIBLE. It used to be flat across the day columns, because
    the ceiling was {SRC["MaxRefinedPerDay"]} units PER RESOLVE: a yard's best possible output was eight units per
    homecoming however long the absence, so a grand build was gated on how often the founder
    walked through the gate. It now climbs with the days, because the ceiling is
    MaxRefinedPerDay = {SRC["MaxRefinedPerDay"]} - the width of the saw-pit rather than a rule about visits.

    What still binds is the crew. A hand puts in {SRC["EffortPerHandPerDay"]} effort a day and a unit costs
    {SRC["RefineEffortPerUnit"]}, so one pair of hands makes {SRC["EffortPerHandPerDay"] * 2 // SRC["RefineEffortPerUnit"]} a day and it takes {math.ceil(SRC["MaxRefinedPerDay"] * SRC["RefineEffortPerUnit"] / SRC["EffortPerHandPerDay"])} hands to reach the
    bench's own width. A yard nobody stands in still makes nothing for thirty days, which is
    clause 2 exactly: time times LABOUR, never time alone.""")

    # QB-29, priced. The yard read the staffing pass's crew stretch and stopped there for the
    # whole life of the mod, so a holed saw-pit shaped exactly what a new one did. It applies
    # its own condition now, the way the crops and the networks always have.
    floor_condition = 100 - SRC["MaxWearPercent"]
    print(f"""
And what NEGLECT costs, which is new. Addendum 10(b) says damage degrades every work's
function in its own kind, and a yard's kind is what comes off it; the refining bench was the
one consumer that had never applied its own wear (QB-29). Condition is 100 - wear, floored at
{floor_condition} by MaxWearPercent = {SRC["MaxWearPercent"]}, and it rides the EFFORT percent rather than the head
count - a two-hand yard times a {floor_condition}% condition would truncate to nobody and report itself
UNSTAFFED, which is the wrong sentence for a bench people are standing at. Every yard in the
catalogue is Staff="2", so this is that yard:
""")
    print(
        f"{'wear':>5}{'cond':>6}  "
        + "".join(f"{'d=' + str(d):>8}" for d in DAY_COLUMNS)
    )
    for wear in (0, 20, 40, SRC["MaxWearPercent"]):
        condition = 100 - min(wear, SRC["MaxWearPercent"])
        row = f"{wear:>5}{condition:>6}  "
        for d in DAY_COLUMNS:
            row += f"{refined_units(2, d, condition):>8}"
        print(row)
    sound_month = refined_units(2, 30)
    worn_month = refined_units(2, 30, floor_condition)
    print(f"""
    Reading the ladder. A sound two-hand yard shapes {sound_month} units in a month; the same yard at
    the wear ceiling shapes {worn_month} - {100 * worn_month // sound_month}% of it, which is the condition floor and not a
    number chosen here. The consequence is bounded exactly where every other work's is, it
    is a SLOPE rather than a cliff (the row at wear {SRC["MaxWearPercent"]} is short, never zero, over any stretch
    a founder would notice), and mending ends it outright: the work is the same work again.
    The single-day column is the one place it reads as nothing, and that is the same
    integer truncation a sound one-hand yard already lived with.

    The catalogue table below still prices the SOUND yard, because that is the schedule a
    founder is quoted when they commission a design. A neglected one takes {sound_month / worn_month:.1f}x as long.""")

    print(f"""
    The third factor, for the record and for nothing else. The keepers' method
    (RESEARCH-SYSTEM-DESIGN 8.2) now rides this same effort percent, and reaches the city
    book's water and food rates and the crop harvest besides. A realm that has researched
    nothing carries {BASELINE_METHOD}%, {BASELINE_METHOD}% is a no-op through Methoded, and NO table in this file moved
    for it - they are all baseline tables and they are meant to stay that way. What the
    ceiling would be worth if a realm reached it: a sound two-hand yard shapes {refined_units(2, 30, MAX_METHOD)} units in
    a month at {MAX_METHOD}% against {sound_month} at the baseline. A factor on the effort, never on the days,
    and never on a draw.""")

    # Catalogue costs, split into what the ground gives and what a yard has to shape.
    entries = re.findall(
        r'<building\s+Key="([^"]+)"(.*?)/?>',
        open(BUILD_XML, encoding="utf-8-sig").read(),
        re.S,
    )
    priced = []
    for key, attrs in entries:
        cost = re.search(r'\sCost="(\d+)"', attrs)
        ticks = re.search(r'\sTicks="(\d+)"', attrs)
        mats = re.search(r'\sMaterials="([^"]*)"', attrs)
        bit_attr = re.search(r'\sBits="([0-9]*)"', attrs)
        if not cost or not ticks:
            continue
        raw_units = 0
        refined_units = 0
        for part in mats.group(1).split(",") if mats else []:
            bits = part.split(":")
            if len(bits) != 2 or not bits[1].strip().isdigit():
                continue
            name, amount = bits[0].strip(), int(bits[1])
            if name in REFINED_KEYS:
                refined_units += amount
            elif name in RAW_KEYS:
                raw_units += amount
        # Bits="0034" is two of the commonest and one each of tier three and four - vanilla's
        # own tinkering stock, drawn from the same dedicated stockpiles. Tier is the digit, so
        # the weight of a cost is the sum of (tier + 1): a tier-four bit is not a tier-zero one.
        bit_weight = sum(int(d) + 1 for d in (bit_attr.group(1) if bit_attr else ""))
        priced.append(
            (
                key,
                int(cost.group(1)),
                int(ticks.group(1)),
                raw_units,
                refined_units,
                bit_weight,
            )
        )

    def refined_per_day(crew: int) -> int:
        """A yard of `crew` hands, per day, at ordinary capability."""
        return min(
            crew * SRC["EffortPerHandPerDay"] // SRC["RefineEffortPerUnit"],
            SRC["MaxRefinedPerDay"],
        )

    if priced:
        pace = refined_per_day(2)
        print(f"""
What the catalogue asks for. Materials split into what CLEARING gives back (timber, stone,
scrap, marble, mud, canvas) and what a YARD has to shape - only the second half is on the
refining chain, and it eats {SRC["RawPerRefined"]} raw per unit on top of the raw the design
names outright. A two-hand yard shapes {pace} unit(s) a day.
""")
        print(
            f"{'design':<20}{'drams':>7}{'build d':>9}{'raw named':>11}"
            f"{'refined':>9}{'raw eaten':>11}{'raw total':>11}{'bit weight':>12}{'yard-days':>11}"
        )
        for key, cost, ticks, raw, refined, bit_weight in sorted(
            priced, key=lambda e: -e[4]
        )[:12]:
            eaten = refined * SRC["RawPerRefined"]
            yard_days = math.ceil(refined / pace) if (refined and pace) else 0
            print(
                f"{key:<20}{cost:>7}{ticks / SRC['TicksPerDay']:>9.1f}{raw:>11}"
                f"{refined:>9}{eaten:>11}{raw + eaten:>11}{bit_weight:>12}{yard_days:>11}"
            )
        bitted = [e for e in priced if e[5] > 0]
        print(f"""
    Bits, the third cost. {len(bitted)} of {len(priced)} designs ask for vanilla tinkering bits at all,
    and they are exactly the high-craft ones: {", ".join(sorted(e[0] for e in bitted))}.
    Nobody in the settlement makes a bit; they come off salvage and out of caravans, so a
    design that names them is gated on the founder's own scavenging rather than on any yard.
    That is the intended shape (the catalogue's header says so) and it means the bit line is
    a GATE and not a rate - it never appears in the yard-days column, and no amount of crew
    shortens it.""")
        top = max(priced, key=lambda e: e[4])
        _ = top
        print(f"""
    Reading. The heaviest design in the catalogue ({top[0]}) wants {top[4]} refined
    units: {math.ceil(top[4] / pace)} days of a two-hand yard, and {top[4] * SRC["RawPerRefined"]} raw loads eaten on top of the
    {top[3]} it names outright. Against a build time of {top[2] / SRC["TicksPerDay"]:.0f} days, the YARD is the schedule
    for anything grand - the scaffold is not what you wait for.

    The yard-days column is now TRUE rather than aspirational. It was not, for the whole of
    this wave until the material workers came off the visit clock: the ceiling was per
    resolve, so a grand design was gated on homecomings and the column was a floor nobody
    could reach. A yard that runs for thirty days now finishes thirty days of work, and a
    yard the founder walked past thirty times in one afternoon still finishes none.

    Refining is not water-priced twice. Building costs are drams AND materials, and the
    drams side is untouched by this wave.""")


def q4b_mending():
    rule("Q4b Mending: time times hands, never visits")

    def effort_worked(hands: int, days: int) -> int:
        if hands <= 0 or days <= 0:
            return 0
        gang = min(hands, SRC["MaxClearingHands"])
        return min(gang * days * SRC["EffortPerHandPerDay"], 2_147_483_647)

    def repair_effort(material_units: int, wear: int = 1) -> int:
        if wear <= 0:
            return 0
        units = max(0, material_units)
        return max(1, SRC["StrikeBaseEffort"] // 2 + units * SRC["StrikeEffortPerUnit"])

    print(f"""
A mend pays for its replacement materials and bits before this clock starts. Its labour bill
is half the strike base ({SRC["StrikeBaseEffort"]} / 2) plus {SRC["StrikeEffortPerUnit"]} effort for every material unit in
the original work. One resident removes {SRC["EffortPerHandPerDay"]} effort per day; the gang is bounded at
{SRC["MaxClearingHands"]} hands, but the calendar is not bounded. Wear decides whether a mend exists and what
physical stock it costs. It does not make the same building take a different number of hands
to put back together merely because the damage roll was larger.
""")
    print(f"{'material units':>14}{'effort':>10}" + "".join(f"{str(h) + ' hand':>12}" for h in (1, 2, 4, 6)))
    for units in (0, 8, 24, 64, 120):
        effort = repair_effort(units)
        row = f"{units:>14}{effort:>10}"
        for hands in (1, 2, 4, 6):
            per_day = effort_worked(hands, 1)
            row += f"{math.ceil(effort / per_day):>12}"
        print(row)

    # The table is also an executable receipt for the absence doctrine. One thirty-day
    # resolve must equal thirty one-day resolves, while empty hands must never become a
    # magical worker however large the elapsed count. A nonsense-length save saturates rather
    # than wrapping negative and adding effort back to the job.
    for hands in (1, 2, 4, SRC["MaxClearingHands"]):
        assert effort_worked(hands, 30) == 30 * effort_worked(hands, 1)
    assert effort_worked(0, 400) == 0
    assert effort_worked(-4, 400) == 0
    assert effort_worked(SRC["MaxClearingHands"], 2_147_483_647) == 2_147_483_647
    assert repair_effort(120, 0) == 0
    assert repair_effort(-5, 1) == SRC["StrikeBaseEffort"] // 2

    print(f"""
The cells are DAYS TO FINISH after the exact stock is in custody. Returning every day does
not improve them: thirty watched one-day passes and one thirty-day absence remove the same
effort. Leaving nobody assigned removes zero after 400 days. This closes the old 'uncapped
mending' note as a measured doctrine: HANDS are the rate; elapsed world days are the clock;
homecomings are neither.
""")


def q5_sensitivity():
    rule("Q5  Sensitivity: the two numbers the water economy stands on")
    print("""
A settler drinks `drink` drams a day at Camp rate; a hand on the detail carries `carry`.
The table is the share of the settlement the water detail must be, at Camp, for the
settlement to hold itself. Anything at or under 50% leaves half the people free for works.
""")
    print(f"{'drink':>7} " + "".join(f"{'carry ' + str(c):>12}" for c in (1, 2, 3, 4)))
    for drink in (0.5, 0.75, 1.0, 1.25, 1.5, 2.0):
        row = f"{drink:>7.2f} "
        for carry in (1, 2, 3, 4):
            row += f"{drink / carry:>11.0%} "
        print(row)
    print(f"""
Shipped is drink=1.0, carry={SRC["FetchDramsPerSettler"]} -> 50% at Camp. That is the knife edge the design wants:
half your people on water is a real cost and a survivable one. carry=3 makes water free
(33%); drink=1.5 makes a camp unable to spare anyone (75%). The tuning is not sensitive
inside drink 0.75-1.25 at carry 2, which is where it sits.

NOTHING WAS RETUNED IN THIS PASS. The uncapping does not move any of these constants; it
removes a ceiling that was hiding whether they bound. They bind.""")


# --------------------------------------------------------------------------------------
# 4. Level. The catalogue, read the way KingdomSubsidence now reads it.
# --------------------------------------------------------------------------------------


@dataclass(frozen=True)
class Design:
    key: str
    stage: int  # the rung it is actually reachable at: max(MinStage, plot tier)
    cost: int
    staff: int
    carries: dict
    capacity: int = 0  # drams its blueprint's LiquidVolume holds; 0 for anything that is not a store
    plot: str = ""  # S / M / L / XL, or "" for a single-cell design. KingdomReachRules.BandForSize
    larder: int = 0  # servings its blueprint's r_KingdomLarderCapacity tag holds; 0 if not a pantry
    rows: int = 0  # physical rows from its blueprint, or exact hosted producer count x producer tag
    styles: str = "all"  # the design's Styles tag list, verbatim (KingdomZoningRules.TagAccepts)


def _store_capacities() -> dict:
    """Blueprint -> MaxVolume, off the real ObjectBlueprints.xml.

    What a store LEAKS is denominated against its own capacity, not against the settlement's
    total, so the leak table below has to know which vessel is which.
    """
    text = open(BLUEPRINTS_XML, encoding="utf-8-sig").read()
    out = {}
    blocks = re.split(r"<object\s+", text)
    for block in blocks[1:]:
        name = re.match(r'Name="([^"]+)"', block)
        volume = re.search(r'<part\s+Name="LiquidVolume"[^>]*MaxVolume="(\d+)"', block)
        if name and volume:
            out[name.group(1)] = int(volume.group(1))
    return out


def _larder_capacities() -> dict:
    """Blueprint -> r_KingdomLarderCapacity, off the real ObjectBlueprints.xml, through Inherits.

    The food side of `_store_capacities`, and declared the same way and for the same reason:
    what a design adds to the sustainable LEVEL is a catalogue fact, and how much its vessel
    holds is a fact about the vessel.

    Walks the inheritance chain exactly as `_crop_rows` does, and for the same reason: the game's
    blueprint loader resolves an inherited tag, so a keeper that inherits a granary really does
    hold a granary's servings. Reading only the object's own block was a latent asymmetry with the
    rows half - it fired as "FOOD FROM NOWHERE" the first time a design inherited its pantry
    rather than re-declaring one, which is the styles wave's `sporecellar`.
    """
    return _inherited_tag_values("r_KingdomLarderCapacity")


def _inherited_tag_values(tag_name: str) -> dict:
    """Blueprint -> integer value of one tag, resolved through `Inherits` where the object does
    not declare it itself. The one walk both tag lookups in this file share."""
    text = open(BLUEPRINTS_XML, encoding="utf-8-sig").read()
    own, parent = {}, {}
    for block in re.split(r"<object\s+", text)[1:]:
        name = re.match(r'Name="([^"]+)"', block)
        if not name:
            continue
        inherits = re.match(r'Name="[^"]+"\s+Inherits="([^"]+)"', block)
        if inherits:
            parent[name.group(1)] = inherits.group(1)
        tag = re.search(r'<tag\s+Name="' + tag_name + r'"\s+Value="(\d+)"', block)
        if tag:
            own[name.group(1)] = int(tag.group(1))
    out = dict(own)
    for name in list(parent):
        seen, walk = set(), name
        while walk and walk not in own and walk not in seen:
            seen.add(walk)
            walk = parent.get(walk)
        if walk in own:
            out[name] = own[walk]
    return out


def _crop_rows() -> dict:
    """Blueprint -> r_KingdomCropRows, off the real ObjectBlueprints.xml, through Inherits.

    The food lane's `LiquidProducer VariableRate`. A design that GROWS declares how many rows
    physically stand in it when it is sown, and its catalogue `Carries="food:N"` is required to
    be exactly `Rows * YieldPerRow / CropDays`. Several designs inherit the tag from a smaller
    rung and override only their own, so the lookup walks the chain the way the engine's
    blueprint loader does - the same walk `_producer_rates` makes for the water half.
    """
    return _inherited_tag_values("r_KingdomCropRows")


def _hosted_crop_rows() -> dict:
    """Blueprint -> static hosted growbed rows.

    Hosted fixtures are physical evidence for one receipt-owned rate, not ordinary fields. They
    therefore never carry `r_KingdomCropRows` or `r_KingdomPlot`, which would run and count the
    surface crop lifecycle a second time.
    """
    return _inherited_tag_values("r_TAF_HostedCropRows")


def _read_catalogue() -> list[Design]:
    text = open(BUILD_XML, encoding="utf-8-sig").read()
    capacities = _store_capacities()
    larders = _larder_capacities()
    rows = _crop_rows()
    hosted_row_rates = _hosted_crop_rows()
    tier = {"S": 0, "M": 1, "L": 3, "XL": 4}  # KingdomPlotRules.StageForSize
    names = {n: i for i, (n, _p, _c) in enumerate(STAGES)}
    out = []
    for attrs in re.findall(r"<building\s+(.*?)/?>", text, re.S):
        key = re.search(r'Key="([^"]+)"', attrs)
        if not key:
            continue
        carries = {}
        raw = re.search(r'\sCarries="([^"]*)"', attrs)
        if raw:
            for part in raw.group(1).split(","):
                bits = part.split(":")
                if len(bits) == 2 and bits[1].strip().lstrip("-").isdigit():
                    carries[bits[0].strip()] = int(bits[1])
        blueprint_early = re.search(r'\sBlueprint="([^"]+)"', attrs)
        # A design earns a row here by carrying something OR by being a vessel. Storage stopped
        # carrying `water` in G1 (Addendum 11(a): a reservoir holds, it does not conjure), and a
        # cistern that fell out of this list entirely would take its capacity - the thing it is
        # now paid in, and a stage gate in its own right - out of the model with it.
        if not carries and not (
            blueprint_early and capacities.get(blueprint_early.group(1), 0)
        ):
            continue
        min_stage = re.search(r'\sMinStage="([^"]*)"', attrs)
        plot = re.search(r'\sPlot="([^"]*)"', attrs)
        cost = re.search(r'\sCost="(\d+)"', attrs)
        staff = re.search(r'\sStaff="(\d+)"', attrs)
        reachable = max(
            names.get(min_stage.group(1), 0) if min_stage else 0,
            tier.get(plot.group(1), 0) if plot else 0,
        )
        blueprint = re.search(r'\sBlueprint="([^"]+)"', attrs)
        hosted_blueprint = re.search(r'\sHostedProducerBlueprint="([^"]+)"', attrs)
        hosted_count = re.search(r'\sHostedProducerCount="(\d+)"', attrs)
        if bool(hosted_blueprint) != bool(hosted_count):
            raise AssertionError(
                f"HOSTED PRODUCER CONTRACT RAGGED: {key.group(1)} must name blueprint and count"
            )
        hosted_rows = 0
        if hosted_blueprint:
            producer = hosted_blueprint.group(1)
            count = int(hosted_count.group(1))
            if count < 1 or hosted_row_rates.get(producer, 0) < 1:
                raise AssertionError(
                    f"HOSTED PRODUCER CONTRACT EMPTY: {key.group(1)} names {count} x {producer}"
                )
            hosted_rows = count * hosted_row_rates[producer]
        if blueprint and rows.get(blueprint.group(1), 0) and hosted_rows:
            raise AssertionError(
                f"HOSTED PRODUCER DOUBLE COUNT: {key.group(1)} is both a surface and hosted grower"
            )
        styles = re.search(r'\sStyles="([^"]*)"', attrs)
        out.append(
            Design(
                key.group(1),
                reachable,
                int(cost.group(1)) if cost else 0,
                int(staff.group(1)) if staff else 0,
                carries,
                capacities.get(blueprint.group(1), 0) if blueprint else 0,
                plot.group(1) if plot else "",
                larders.get(blueprint.group(1), 0) if blueprint else 0,
                (rows.get(blueprint.group(1), 0) if blueprint else 0) or hosted_rows,
                styles.group(1) if styles else "all",
            )
        )
    return out


CATALOGUE = _read_catalogue()

BINDING = ("water", "food", "roof")


def level_from_water(water: int, stage: int) -> int:
    """KingdomSubsidenceRules.LevelFromWater: drams a day -> settlers at this stage's rate."""
    if water <= 0:
        return 0
    percent = STAGE_PERCENT[stage]
    return water if percent <= 100 else water * 100 // percent


def equilibrium(water: int, food: int, roof: int, lift: int, stage: int, shade: int = 0) -> int:
    """KingdomCatalogueRules.Equilibrium, with the water converted first.

    `shade` is attended transient lift. The shipped caller currently supplies MealShade only;
    the read-compatible NotableShade field is normalized to zero and never enters this model.
    Transient lift remains under the common cap, so a meal never outruns the binding supports.
    """
    least = max(0, min(level_from_water(water, stage), food, roof))
    cap = least * SRC["LiftCapPercent"] // 100
    level = least + min(max(lift, 0) + max(shade, 0), cap)
    return max(level, SRC["FloorLevel"])


def scaled(amount: int, percent: int) -> int:
    """KingdomReachRules.Scaled: a lift running at all keeps a point of what it declares."""
    if amount <= 0 or percent <= 0:
        return 0
    return max(1, amount * percent // 100)


def landed(amount: int, reached: int, homes: int) -> int:
    """KingdomReachRules.Landed: the share of the settlement's roofs a lift actually covers."""
    if amount <= 0 or reached <= 0 or homes <= 0:
        return 0
    return scaled(amount, min(reached, homes) * 100 // homes)


# KingdomReachRules.BandForSize x Derive: what a design of each plot tier shades. A single-cell
# design (no Plot=) reaches its own cell, which is the plot band exactly as a small plot is.
REACH_BAND = {"": "plot", "S": "plot", "M": "quarter", "L": "zone", "XL": "city"}


def _best(kind: str, stage: int, by):
    rows = [d for d in CATALOGUE if kind in d.carries and d.stage <= stage]
    return min(rows, key=by) if rows else None


def _plan(kind: str, stage: int, need: int, by):
    """How many of one design it takes to reach `need` points of one good."""
    design = _best(kind, stage, by)
    if design is None or need <= 0:
        return None
    count = math.ceil(need / design.carries[kind])
    return design, count


def q6_level():
    rule("Q6  Level: what it takes to HOLD each rung, now that something reads Carries")
    print(f"""
The denomination, restated because everything below turns on it: one point of `water` is one
dram a day sustained, which is one settler's thirst AT CAMP RATES.
`KingdomRules.UpkeepDrams` then bills {STAGE_PERCENT} per hundred by stage, so
`KingdomSubsidenceRules.LevelFromWater` divides the declared water by that percentage before
the frozen `Equilibrium` sees it. Food and roof are already denominated in people and are not
converted: a roof is a roof at any rung.

The cross-check, first: the drams a rung's own population drinks, converted back, must carry
that population. If these two columns disagree the catalogue and the upkeep table are
describing different games.
""")
    print(f"{'rung':<9}{'people':>8}{'drinks/d':>10}{'carries':>9}   verdict")
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            print(
                f"{name:<9}{SRC['FloorLevel']:>8}{'-':>10}{SRC['FloorLevel']:>9}   "
                f"the floor: Camp carries itself with nothing standing"
            )
            continue
        bill = (
            upkeep_per_day(floor, 10**9) if False else floor * STAGE_PERCENT[i] // 100
        )
        back = level_from_water(bill, i)
        ok = "exact" if back >= floor else f"SHORT by {floor - back}"
        print(f"{name:<9}{floor:>8}{bill:>10}{back:>9}   {ok}")

    print("""
Now the two ways a founder actually builds it. CHEAPEST is drams-per-point greedy (many small
plots); GRANDEST is the fewest works that will do it (the biggest design on the rung). Both
must be plausible, because both are things players do.
""")
    for label, by in (
        (
            "cheapest",
            lambda d: (
                d.cost / max(d.carries.get(_KIND, 1), 1),
                -d.carries.get(_KIND, 0),
            ),
        ),
        ("grandest", lambda d: -d.carries.get(_KIND, 0)),
    ):
        print(f"  --- {label} ---")
        print(f"  {'rung':<9}{'works':>7}{'drams':>8}{'staff':>7}{'level':>7}   plan")
        for i, (name, floor, _cap) in enumerate(STAGES):
            if i == 0:
                continue
            need_water = math.ceil(floor * STAGE_PERCENT[i] / 100)
            plans = []
            totals = {"works": 0, "cost": 0, "staff": 0}
            points = {}
            ok = True
            for kind, need in (("water", need_water), ("food", floor), ("roof", floor)):
                globals()["_KIND"] = kind
                got = _plan(kind, i, need, by)
                if got is None:
                    ok = False
                    break
                design, count = got
                plans.append(f"{design.key}x{count}")
                totals["works"] += count
                totals["cost"] += design.cost * count
                totals["staff"] += design.staff * count
                points[kind] = design.carries[kind] * count
            if not ok:
                print(
                    f"  {name:<9}{'-':>7}{'-':>8}{'-':>7}{'-':>7}   NO DESIGN CARRIES ONE OF THE THREE"
                )
                continue
            lvl = equilibrium(points["water"], points["food"], points["roof"], 0, i)
            verdict = "holds" if lvl >= floor else f"SHORT ({lvl})"
            print(
                f"  {name:<9}{totals['works']:>7}{totals['cost']:>8}{totals['staff']:>7}{lvl:>7}   "
                f"{' | '.join(plans)}  {verdict}"
            )
        print()

    print(f"""
Reading. Every rung is holdable both ways. That is now a stronger statement than it was,
because the whole water column changed hands underneath it.

WHAT MOVED (Addendum 11(a), "storage stores; producers produce"). Every store in the water
lane - `cistern`, `cisternvault`, `reservoir`, `waterworks` - stopped declaring `water`. It
was the largest unearned claim in the catalogue: `Carries="water:N"` is read BOTH as N
settlers carried and as N drams a day arriving in the casks, so a vessel declaring it was
conjuring the water it claimed only to be holding, and nothing in Qud falls from the sky to
fill it. The whole column is carried by producers now, and every producer's figure is derived
from the LiquidProducer on its own blueprint at 1200 / mean(VariableRate) - the check at the
bottom of this file re-derives all nine of them from the XML and fails if one drifts.

WHAT THAT COST, rung by rung. Nothing, and two rungs gained. The CHEAPEST column is unmoved at
every rung (6, 12, 27, 51). The GRANDEST column RISES at Steading (6 -> 8: `catchmentbank` at 5
replaces `cistern` at 8 on a rung whose bill is only 6, so two of them overshoot it) and at
Village (12 -> 18: `airwellcourt` at 15 against `cisternvault`'s 18, twice over). Town is
unchanged at 26 - `airwellfield` at 25 is one point off the old `reservoir`'s 26 and two of them
still clear the 45-dram bill. Only City slips, 70 -> 68: `condensery` at 50 against the old
`waterworks`' 52, three of them, 150 drams against a City's 110. Every plan is still Staff=0
from Village up, and the assertion in section 7 refuses any future tuning that makes a rung
unreachable.

WHAT IT BOUGHT is the rung below the table. Nothing in the water lane opens at Camp any more -
no `saltpan`, no `catchment` - so a camp drinks the founder's stock, what the detail hauls out
of the site's own finite pools, and what a charter pays. Q7 is that curve.

The food asymmetry this table used to expose stays closed. `grange` (L, Town) and `homefarm`
(XL, City) fill the two rungs that used to be missing at 26 and 40, the same two figures the
ROOF lane climbs, because a dinner and a bed are both counted in people and neither is divided
by the stage rate the way a dram is.

What did NOT change, deliberately: food still wants hands at every rung above the kitchen
garden, and it is the only binding good that does. Water and roof automate to Staff=0 at
their large and grand designs; food never automates, it only improves its rate - four
settlers fed per hand at a field, six at ploughed fields, nearly nine at a grange, ten at
the home farm. What automation buys is a discharge from the LABOUR term, and (since Addendum
10(b)) never a discharge from the CONDITION term: an air-well field still needs nobody and
still runs down when it is ruined. Q9 is what that costs when a season goes badly.

Comfort is not in these tables. `LiftCapPercent` = {SRC["LiftCapPercent"]} lets craft, spirit, learning, order and
luxury add up to half the binding level on top, and a settlement that has built its civic
works among its houses still reaches that cap easily - so a real City holds about 1.5x what
the table above says. That is authored (the frozen arithmetic's own doc) and it is why these
numbers are the FLOOR of what a rung costs rather than the expectation. What is new is that
reaching the cap is no longer automatic: a lift now lands in proportion to the roofs its work
covers. A civic-office title contributes zero; the only settlement-wide transient shade is
the last attended meal. Q10 is the authored reach reckoning.""")



# --------------------------------------------------------------------------------------
# 4b. Styles. The one diversity axis the code always honoured and the data never used.
# --------------------------------------------------------------------------------------


def _declared_styles() -> list:
    """Every `<style Name="x" />` the catalogue declares, in file order."""
    text = open(BUILD_XML, encoding="utf-8-sig").read()
    return re.findall(r'<style\s+Name="([^"]+)"', text)


def style_accepts(tags: str, style: str) -> bool:
    """`KingdomZoningRules.TagAccepts`, re-implemented here on purpose.

    This is the sim's whole job: if it imported the rule it would be asserting that the rule
    equals itself. Written independently from the same three sentences the C# doc-comment
    states, so a change to either side has to be made twice on purpose.

      1. an empty list accepts everything;
      2. a negation that matches refuses, whatever else the list says;
      3. otherwise accept on `all`, on the name, or on a list of nothing but refusals.
    """
    tokens = [t.strip().lower() for t in (tags or "").split(",")]
    tokens = [t for t in tokens if t]
    if not tokens:
        return True
    want = (style or "").strip().lower()
    welcomed = False
    any_welcome = False
    for token in tokens:
        if token.startswith("!"):
            refused = token[1:].strip()
            if refused and (refused == want or refused == "all"):
                return False
            continue
        any_welcome = True
        if token in ("all", want):
            welcomed = True
    return welcomed or not any_welcome


def _all_designs() -> list:
    """Every `<building>` in the catalogue as (key, category, styles, stage), including the
    walls and the plumbing that `_read_catalogue` drops for carrying nothing. The style pass has
    to see the whole file: a style that lost its only wall has lost a lane whether or not a wall
    contributes to the level."""
    text = open(BUILD_XML, encoding="utf-8-sig").read()
    names = {n: i for i, (n, _p, _c) in enumerate(STAGES)}
    tier = {"S": 0, "M": 1, "L": 3, "XL": 4}
    out = []
    for attrs in re.findall(r"<building\s+(.*?)/?>", text, re.S):
        key = re.search(r'Key="([^"]+)"', attrs)
        if not key:
            continue
        category = re.search(r'\sCategory="([^"]*)"', attrs)
        styles = re.search(r'\sStyles="([^"]*)"', attrs)
        min_stage = re.search(r'\sMinStage="([^"]*)"', attrs)
        plot = re.search(r'\sPlot="([^"]*)"', attrs)
        creed = re.search(r'\sCreed="([^"]*)"', attrs)
        reachable = max(
            names.get(min_stage.group(1), 0) if min_stage else 0,
            tier.get(plot.group(1), 0) if plot else 0,
        )
        out.append(
            (
                key.group(1),
                category.group(1) if category else "civic",
                styles.group(1) if styles else "all",
                reachable,
                creed.group(1) if creed else "",
            )
        )
    return out


def q12_styles():
    rule("Q12 Styles: what each of the five cities may raise, and that every one can still stand")
    styles = _declared_styles()
    designs = _all_designs()
    assert styles, "the catalogue declares no <style> at all"
    assert designs, "the catalogue declares no <building> at all"

    print(f"""
BUILDING-CATALOGUE-BRIEF Addendum 16: exercise Styles first. `KingdomRules.StyleAllows` has been
a complete, tested eligibility mechanism since the first wave and every design in the file said
`Styles="all"`, so the axis existed and meant nothing. It means something now, and this section is
the guardrail on what it is allowed to mean.

THE LAW THIS ENFORCES, and it is the one the brief states: a style may lose designs, and a style
may not lose a LANE. Concretely, three assertions:

  1. every style keeps at least one design in every category the catalogue has;
  2. every style can still HOLD EVERY RUNG - water, food and roof, at Steading through City, out
     of the designs that style is actually offered, at its own cheapest plan;
  3. no style reads as pure removal: a style that is refused anything is offered something
     nobody else can raise.

The third is not a balance rule, it is a design rule, and it is here because it is the one a data
pass silently breaks. A style is a place, not a penalty.
""")

    # ---- 1. no empty category, per style -------------------------------------------------
    categories = sorted({d[1] for d in designs})
    # A design is SHARED when more than one style is offered it. That distinction is the whole of
    # this table: being refused somebody else's exclusive is not a restriction, it is what an
    # exclusive means, and counting the two together would flatter every column equally and
    # measure nothing.
    reach = {d[0]: [st for st in styles if style_accepts(d[2], st)] for d in designs}
    shared = {key for key, offered_to in reach.items() if len(offered_to) > 1}
    print(f"  {'style':<9}{'offered':>9}{'refused':>9}{'own':>6}   categories missing")
    exclusives = {}
    refusals = {}
    for style in styles:
        offered = [d for d in designs if style_accepts(d[2], style)]
        refused = [d for d in designs if not style_accepts(d[2], style) and d[0] in shared]
        own = [d for d in offered if len(reach[d[0]]) == 1]
        exclusives[style] = [d[0] for d in own]
        refusals[style] = [d[0] for d in refused]
        held = {d[1] for d in offered}
        missing = [c for c in categories if c not in held]
        assert not missing, (
            f"STYLE LOST A LANE: the {style} city is offered nothing at all filed under "
            f"{', '.join(missing)}. A style filters the catalogue; it does not delete a family "
            "out of it."
        )
        print(
            f"  {style:<9}{len(offered):>9}{len(refused):>9}{len(own):>6}   "
            f"{'none' if not missing else ', '.join(missing)}"
        )

    # ---- 3. nothing reads as pure removal ------------------------------------------------
    print()
    for style in styles:
        if refusals[style] and not exclusives[style]:
            raise AssertionError(
                f"PURE REMOVAL: the {style} city is refused {len(refusals[style])} designs and is "
                "offered nothing of its own. Addendum 16 ships the exclusives in the same pass as "
                "the restrictions, for exactly this reason."
            )
        print(f"  {style:<9}is refused {', '.join(refusals[style]) or 'nothing the others have'}")
        print(f"  {'':<9}and raises {', '.join(exclusives[style]) or 'nothing nobody else can'}")

    # ---- 2. every style holds every rung -------------------------------------------------
    print(f"""
NOW THE RUNGS, which is Q6 run five times. Same arithmetic, same cheapest-first plan, but the
catalogue narrowed to what each style is offered. `common` is the control: it is refused nothing
in the binding lanes, so its column is Q6's own answer and every other column is read against it.
""")
    print(f"  {'rung':<9}{'style':<9}{'works':>7}{'drams':>8}{'level':>7}   plan")
    cheapest = {}
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            continue
        need_water = math.ceil(floor * STAGE_PERCENT[i] / 100)
        for style in styles:
            reach = [d for d in CATALOGUE if d.stage <= i and style_accepts(d.styles, style)]
            plans, works, cost = [], 0, 0
            points = {}
            for kind, need in (("water", need_water), ("food", floor), ("roof", floor)):
                rows = [d for d in reach if kind in d.carries]
                assert rows, (
                    f"STYLE CANNOT HOLD {name}: the {style} city is offered no design carrying "
                    f"{kind} at that rung. Every style holds every rung or the restriction is a "
                    "removal of the lane."
                )
                design = min(rows, key=lambda d: (d.cost / max(d.carries[kind], 1), -d.carries[kind]))
                count = math.ceil(need / design.carries[kind])
                plans.append(f"{design.key}x{count}")
                works += count
                cost += design.cost * count
                points[kind] = design.carries[kind] * count
            level = equilibrium(points["water"], points["food"], points["roof"], 0, i)
            assert level >= floor, (
                f"STYLE CANNOT HOLD {name}: the {style} city's cheapest plan reaches {level} "
                f"against a rung of {floor}."
            )
            cheapest[(i, style)] = cost
            print(
                f"  {name if style == styles[0] else '':<9}{style:<9}{works:>7}{cost:>8}{level:>7}   "
                f"{' | '.join(plans)}"
            )
        print()

    # ---- and the price of being somewhere in particular ----------------------------------
    control = styles[0]
    worst = 0
    for (i, style), cost in cheapest.items():
        base = cheapest[(i, control)]
        if base > 0:
            worst = max(worst, cost * 100 // base)
    assert worst <= 150, (
        f"A STYLE IS BEING PUNISHED: some style's cheapest binding plan costs {worst}% of the "
        f"{control} city's at the same rung. A style is a different set of answers, not a worse "
        "one; anything past half again is a restriction that should have been a shade."
    )
    print(f"""
  Costliest style's cheapest plan, against {control}'s at the same rung: {worst}% (ceiling 150%).

Reading. Every style holds every rung out of its own catalogue, and the dearest of them pays
{worst - 100}% more water than the plain city does for the same people. That number is the whole
balance consequence of the pass and it is the one to watch: the restrictions above are chosen so
that no style loses a LANE, only designs within one, and the moment a future restriction takes a
style's last cheap answer to a binding good this line moves before anything else does.

The creed-gated designs are deliberately absent from every column. They are gated on who the
city's PEOPLE are rather than on where it stands, they carry no binding good, and the visibility
law (Addendum 14) means most cities never see them at all - so a plan that leaned on one would be
a plan for a city that may not exist.
""")


def q7_handover():
    rule("Q7  Where automation has to take over: hands against the water bill")
    print(f"""
P1 flagged that a Town cannot be held by hauling alone. This is that flag, finished, with the
works' own contribution beside it. `FetchDramsPerSettler` = {SRC["FetchDramsPerSettler"]}, so a hand on the detail
carries two drams a day whatever the rung; the bill per head climbs with the rung.

"hauled" is the share of the settlement that must be on the water detail to cover the bill
with nobody's works helping. "works" is the same bill covered by the cheapest passive
(Staff=0) water designs the rung can raise - that is the automation the catalogue offers.

Read the Camp row first. It says "-", and that is the ruling rather than a gap: Addendum 11(a)
puts every water producer up the tree, behind resources and effort, so a camp has NOTHING it
can raise that makes a dram. Half its people on the detail is not the cheap option at Camp; it
is the only option, alongside the founder's own stock and what a charter pays in.
""")
    print(
        f"{'rung':<9}{'people':>8}{'drinks/d':>10}{'haulers':>9}{'share':>8}   "
        f"{'passive works':<18}{'their drams':>12}"
    )
    for i, (name, floor, _cap) in enumerate(STAGES):
        pop = max(floor, SRC["FloorLevel"])
        bill = pop * STAGE_PERCENT[i] // 100
        haulers = math.ceil(bill / SRC["FetchDramsPerSettler"])
        passive = [
            d
            for d in CATALOGUE
            if "water" in d.carries and d.stage <= i and d.staff == 0
        ]
        if passive:
            best = max(passive, key=lambda d: d.carries["water"])
            count = math.ceil(bill / best.carries["water"])
            plan = f"{best.key}x{count}"
            drams = best.carries["water"] * count
        else:
            plan, drams, count = "-", 0, 0
        print(
            f"{name:<9}{pop:>8}{bill:>10}{haulers:>9}{haulers / pop:>8.0%}   "
            f"{plan:<18}{drams:>12}"
        )
    print("""
THE CURVE HAS TWO ENDS NOW, and they are different problems.

THE EARLY END IS A FLOOR, NOT A HANDOVER. Camp raises nothing that produces, so its four
settlers drink four drams a day out of three channels and no fourth exists: the stock the
founder arrived with, the detail hauling out of the site's own pools, and charter income. Two
of four on the detail covers the bill exactly. Two things keep that honest rather than cruel.
Fetch MINTS NOTHING - `FetchableDrams` moves water that was already standing in the zone, and
`KingdomSurvey` counts only PURE water as a pool, so a camp pitched by a salt puddle has
hauled nothing at all. And fetch is clamped by the room left in dedicated stores, which is why
a cask rack and a cistern court are the two most useful things a young settlement owns. The
site's pools are a dowry, they do not refill, and the first producer has to be standing before
they run out. That is the whole early game and it is meant to be a clock.

THE FIRST PRODUCER IS AT STEADING, which wants five settlers and sixteen drams of dedicated
capacity before it opens at all. `catchmentbank` x2 covers a Steading's six for no hands.

THE HANDOVER IS STILL AT TOWN. Steading wants three-fifths of its people hauling if it does
not build, Village three quarters - survivable and unpleasant - Town over nine tenths, which
leaves nobody to crew anything, and City more hands than it has, which is arithmetically
impossible. So the rung where hauling stops being a strategy and becomes a stall is TOWN, and
the catalogue must have an answer by then. It does, and the answer is a producer rather than a
vessel: `airwellfield` condenses 25 drams a day for zero hands and two of them cover a Town's
45. `condensery` makes 50 and three cover a City's 110 - behind the foundry level and a
certified Solar Still, which is the point at which the settlement stops counting.

THE WIRING IS THERE, and it is why the flip mattered. A water work's `Carries` is measured
onto its zone row when that ground is attended. The city model then integrates every row's
water rate from its single `ProcessedThroughTick`, whether the founder is watching or not,
and bounded reify lands the debt in real vessels. `LastWaterWorkTick` is only the published
mirror of that model clock. At baseline, one point of `water` is one dram a day and one dram
a day is one settler at camp rates, so `airwellfield` x2 both raises a Town's level to 26 and
puts 50 drams a day in its casks against a bill of 45 - and it does it out of a LiquidProducer
whose mean rate is 48 turns a dram, which is 1200/48 = 25, the catalogue number.

FOOD IS NO LONGER THE HALF NOT WIRED, but it has two physical lanes rather than water's one.
The city model integrates `KingdomGrowth.FoodMadePerDay` off `ProcessedThroughTick`; that
figure deliberately subtracts sown fields and mills. Sown fields catch up their absolute
six-day crop cycles through `KingdomPlot`, and mills transform real larder stock through
`GrindHarvest` on the attended pass. `ResolveHeartbeat` bills
`KingdomRules.RationsForElapsed` against the real larders before the mill runs.
`LastFoodWorkTick` belongs to mills only, never to the fields or the city model. Q11 is the
food half of this table, and its handover reads differently on purpose: water hands over from
hauling to works and food never does.""")


def q8_trajectories():
    rule("Q8  What a slide actually costs, in days and in people")
    print(f"""
`KingdomSubsidenceRules`: a settlement standing more than {SRC["StartMarginPercent"]}% above its level begins to settle
back, one step every {SRC["StepDays"]} world days, shedding (rung + 1) settlers a step, until it reaches the
level exactly. The stage falls one rung per step and only on a clear shortfall
({SRC["StageFallMarginPercent"]}% benefit of the doubt on both of StageFor's readings). Every lost rung asks every
standing work once, at that rung's own reach ({ruin_chance_for(0)}% losing a Camp rung, up to {ruin_chance_for(4)}%
losing a City one) - damage on the mending system's own part, capped, and never a deletion.
""")

    def slide(
        pop: int, stage: int, capacity: int, water: int, food: int, roof: int, days: int
    ):
        steps = 0
        breakpoints = []
        for step in range(days // SRC["StepDays"]):
            level = equilibrium(water, food, roof, 0, stage)
            if pop <= level:
                break
            if step == 0 and pop <= level + max(
                1, level * SRC["StartMarginPercent"] // 100
            ):
                break
            pop -= min(stage + 1, pop - level)
            steps = step + 1
            forgiven_pop = pop * 100 // (100 - SRC["StageFallMarginPercent"])
            forgiven_cap = capacity * 100 // (100 - SRC["StageFallMarginPercent"])
            if stage > 0 and (
                pop <= SRC["FloorLevel"]
                and stage_index(pop, capacity) == 0
                or stage_index(forgiven_pop, forgiven_cap) < stage
            ):
                stage -= 1
                breakpoints.append((steps * SRC["StepDays"], stage, pop))
        return pop, stage, steps, breakpoints

    print(f"{'case':<38}{'days':>6}{'lost':>6}   {'ends':<14}rungs lost (day: rung)")
    cases = [
        ("City 50, nothing standing", 50, 4, 1024, 0, 0, 0),
        ("City 50, works for a Town", 50, 4, 1024, 52, 30, 30),
        ("City 50, works for a City", 50, 4, 1024, 130, 54, 66),
        ("Town 25, works for a Village", 25, 3, 256, 24, 14, 14),
        ("Village 20, works for twelve", 20, 2, 64, 18, 99, 99),
        ("Steading 8, one hut and a garden", 8, 1, 16, 0, 5, 5),
        ("Camp 4, nothing standing", 4, 0, 0, 0, 0, 0),
    ]
    for label, pop, stage, cap, water, food, roof in cases:
        end_pop, end_stage, steps, breaks = slide(
            pop, stage, cap, water, food, roof, 100000
        )
        rungs = ", ".join(f"{d}: {STAGES[st][0]}" for d, st, _p in breaks) or "none"
        ends = STAGES[end_stage][0] + " " + str(end_pop)
        print(
            f"{label:<38}{steps * SRC['StepDays']:>6}{pop - end_pop:>6}   {ends:<14}{rungs}"
        )
    print(f"""
Reading. A City with nothing standing is a Camp of {SRC["FloorLevel"]} in fifty-two days - a season, which is
the length the doctrine's own prose keeps reaching for ("a hundred days and a thousand days
write different chronicles, but both end at the same honest level"). A City whose works carry
a Town settles to the Town and stops, because the water bill per head falls with the rung and
the level RISES as the place shrinks: that is what makes this a convergence and not a
countdown to the floor. A settlement inside its band is untouched, and Camp never moves at
all.

Note what does NOT appear in this table: absence. The slide runs on world time and would run
identically under the founder's nose. What a homecoming changes is that somebody is told.

Nor does RUIN appear in it. This table holds the works' contribution fixed while the slide
runs, and the real thing does not: every lost rung damages works, and damage lowers what a
crewed one carries. So these rows are the FLOOR of a slide rather than the whole of it. Q9
closes that loop and measures what it adds.""")


# --------------------------------------------------------------------------------------
# 5. The loop. Ruin -> effectiveness -> level -> more ruin, and where it stops.
#
# Sections 6 to 8 assume every work runs whole. It does not: a lost rung ruins works
# (KingdomSubsidence.Ruin), ruin lowers what a crewed work carries
# (KingdomWearRules.CombinedEffectiveness -> KingdomCatalogueRules.Carried), and a lower
# level pulls the settlement further down. That is a feedback loop, and an unmeasured
# feedback loop is a balance answer nobody has. These are the five bodies it runs on,
# reproduced exactly and pinned above.
# --------------------------------------------------------------------------------------


def standing_percent(roll: int) -> int:
    """KingdomRules.StandingPercent(Ruins, roll). Roll is ADVERSITY: high is a hard fall."""
    r = 0 if roll < 0 else (99 if roll > 99 else roll)
    ceiling = SRC["RuinStandingCeilingPercent"]
    return ceiling - r * (ceiling - SRC["RuinStandingFloorPercent"]) // 99


def ruin_increment(roll: int) -> int:
    """KingdomSubsidenceRules.RuinIncrement: what one lost rung costs one work."""
    increment = SRC["MaxWearPercent"] * (100 - standing_percent(roll)) // 200
    return 1 if increment < 1 else increment


def ruin_chance_for(stage: int) -> int:
    """KingdomSubsidenceRules.RuinChanceFor (Addendum 10(c)): how far the rung being LOST
    reaches, out of the scales there are. No quota - every standing work is asked at this
    chance, independently, whatever it is."""
    index = 0 if stage < 0 else (len(STAGES) - 1 if stage >= len(STAGES) else stage)
    return SRC["RuinChancePercent"] * (index + 1) // len(STAGES)


def add_wear(wear: int, added: int) -> int:
    """KingdomMaterialRules.AddWear. The ceiling is the whole reason this loop terminates."""
    return min(max(wear, 0) + max(added, 0), SRC["MaxWearPercent"])


def condition_percent(wear: int) -> int:
    """KingdomMaterialRules.ConditionPercent. Never zero: the floor is 100 - MaxWearPercent."""
    return 100 - min(max(wear, 0), SRC["MaxWearPercent"])


def combined_effectiveness(crew_stretch: int, wear: int) -> int:
    """KingdomWearRules.CombinedEffectiveness: crew stretch TIMES condition, never either alone."""
    stretch = min(max(crew_stretch, 0), 100)
    return stretch * condition_percent(wear) // 100


def work_effectiveness(staff: int, crew_stretch: int, wear: int) -> int:
    """KingdomWearRules.WorkEffectiveness - Addendum 10(b)'s ruling, in one line.

    A work that asks for crew runs at crew TIMES condition; a work that asks for nobody runs at
    its condition alone. The second arm used to be a flat 100, which is what made ruin a food-lane
    problem only. Both arms are 100 for a sound work, so this is a strict refinement.
    """
    return (
        combined_effectiveness(crew_stretch, wear)
        if staff > 0
        else condition_percent(wear)
    )


def leaked(capacity: int, held: int, wear: int, days: int) -> int:
    """KingdomWearRules.Leaked: what a damaged STORE loses to the ground over world days.

    Linear in the wear, linear in the days, denominated against the store's own capacity, and
    never more than is in there. The division is done last, so a small store over a long stretch
    loses something rather than rounding to nothing every day.
    """
    if capacity <= 0 or held <= 0 or wear <= 0 or days <= 0:
        return 0
    wear = min(wear, SRC["MaxWearPercent"])
    lost = (
        capacity
        * wear
        * days
        // (SRC["MaxWearPercent"] * SRC["LeakDaysToEmptyAtCeiling"])
    )
    return min(lost, held)


def carried(amount: int, percent: int) -> int:
    """KingdomCatalogueRules.Carried. Floors honestly - a field at a tenth of its crew feeds
    nobody and says zero, rather than rounding a person into existence."""
    if amount <= 0 or percent <= 0:
        return 0
    return amount if percent >= 100 else amount * percent // 100


def supports(state, crew_stretch: int = 100):
    """KingdomSubsidence.Supports over a list of [design, wear] pairs.

    THE ASYMMETRY THAT USED TO DECIDE EVERYTHING BELOW IS GONE (Addendum 10(b)). `Supports` once
    read a work's effectiveness only when `KingdomStaffNeeded > 0` and handed a staffless design
    a flat 100, so wear reached the level exclusively through crewed works and a ruined reservoir
    carried its full twenty-six drams. It now folds every work at `WorkEffectiveness`, whose
    staffless arm is the work's own condition. Ruin bites the water and roof lanes too.
    """
    tally = {"water": 0, "food": 0, "roof": 0, "lift": 0}
    for design, wear in state:
        percent = work_effectiveness(design.staff, crew_stretch, wear)
        for kind, amount in design.carries.items():
            got = carried(amount, percent)
            if got <= 0:
                continue
            tally[kind if kind in BINDING else "lift"] += got
    return tally


def level_of(state, stage: int, crew_stretch: int = 100) -> int:
    tally = supports(state, crew_stretch)
    return equilibrium(
        tally["water"], tally["food"], tally["roof"], tally["lift"], stage
    )


def grandest_plan(stage: int):
    """The Q6 `grandest` plan for a rung, as a list of designs rather than a summary line."""
    floor = STAGES[stage][1]
    out = []
    for kind, need in (
        ("water", math.ceil(floor * STAGE_PERCENT[stage] / 100)),
        ("food", floor),
        ("roof", floor),
    ):
        globals()["_KIND"] = kind
        got = _plan(kind, stage, need, lambda d: -d.carries.get(_KIND, 0))
        if got is None:
            continue
        design, count = got
        out.extend([design] * count)
    return out


def cheapest_plan(stage: int):
    floor = STAGES[stage][1]
    out = []
    for kind, need in (
        ("water", math.ceil(floor * STAGE_PERCENT[stage] / 100)),
        ("food", floor),
        ("roof", floor),
    ):
        globals()["_KIND"] = kind
        got = _plan(
            kind,
            stage,
            need,
            lambda d: (
                d.cost / max(d.carries.get(_KIND, 1), 1),
                -d.carries.get(_KIND, 0),
            ),
        )
        if got is None:
            continue
        design, count = got
        out.extend([design] * count)
    return out


def _apply_reach(dist: list[float], reach: float) -> list[float]:
    """One rung's worth of `RuinChanceFor`, convolved onto a hit-count distribution.

    `dist[i]` is the chance a work has been hit exactly `i` times so far. This is the whole of
    the reach rule in expected-value form: `dist[0]` IS the work's survival odds, and each rung
    multiplies it by `1 - reach` the same way `RollRuin` fails it - independently, and (the
    rules file's own claim) identically for every work, since the reach depends only on the
    rung being lost and never on which work is asked.
    """
    out = [0.0] * (len(dist) + 1)
    for i, p in enumerate(dist):
        out[i] += p * (1 - reach)
        out[i + 1] += p * reach
    return out


def slide_with_ruin(pop, stage, capacity, designs, roll, days=100000):
    """Q8's slide with the loop closed, under the reach rule (Addendum 10(c)).

    `KingdomSubsidence.Ruin` no longer fills a quota: every standing work is asked once a rung,
    at that rung's own reach (`RuinChanceFor`), and there is no cursor and no order to be
    adversarial about - a wider rung reaches a strict superset of what a narrower one would
    have, out of the same works, whichever was raised first. Modelled here with `_apply_reach`:
    every work in `state` shares one hit-count distribution rather than its own wear, because
    they all face the identical sequence of rungs. A work's expected wear folds in the ceiling
    the same way `AddWear` does, one hit-count bucket at a time.

    `roll=None` cuts the loop outright - no rung damages anything - which is Q8's own answer and
    the only honest baseline to measure the loop against.
    """
    increment = 0 if roll is None else ruin_increment(roll)
    dist = [1.0]
    state = [[d, 0] for d in designs]
    steps = 0
    breaks = []

    def expected_wear() -> int:
        # Rounded, not left as a float: the real wear is always an int (AddWear's own type),
        # and everything downstream - level_of, pop arithmetic, StageFor - assumes one. The
        # expectation is our modelling device; the settlement it feeds is still integer people.
        raw = sum(
            p * min(i * increment, SRC["MaxWearPercent"]) for i, p in enumerate(dist)
        )
        return round(raw)

    for step in range(days // SRC["StepDays"]):
        wear = expected_wear()
        for work in state:
            work[1] = wear
        level = level_of(state, stage)
        if pop <= level:
            break
        if step == 0 and pop <= level + max(
            1, level * SRC["StartMarginPercent"] // 100
        ):
            break
        pop -= min(stage + 1, pop - level)
        steps = step + 1
        forgiven_pop = pop * 100 // (100 - SRC["StageFallMarginPercent"])
        forgiven_cap = capacity * 100 // (100 - SRC["StageFallMarginPercent"])
        if stage > 0 and (
            pop <= SRC["FloorLevel"]
            and stage_index(pop, capacity) == 0
            or stage_index(forgiven_pop, forgiven_cap) < stage
        ):
            reach = ruin_chance_for(stage) / 100
            stage -= 1
            breaks.append((steps * SRC["StepDays"], stage, pop))
            dist = _apply_reach(dist, reach)
    wear = expected_wear()
    for work in state:
        work[1] = wear
    hits = len(state) * (1 - dist[0])
    return pop, stage, steps, breaks, hits, state


def leak_table():
    """The SECOND consequence Addendum 10(b) added: a damaged store loses what it holds.

    This is not a level term. `Supports` reads a store's CAPACITY through the design's `Carries`,
    and the leak takes its CONTENTS - so it never touches the sustainable level and lands
    squarely on the water economy Q1 to Q3 measure: fewer drams in the casks, a thinner cushion,
    and the thirst ladder that much closer. Measured here against the daily bill of the rung the
    store belongs to, because "34 drams a day" means nothing until you know what the settlement
    drinks.
    """
    print(f"""
THE LEAK. A damaged STORE also loses what it is holding, on world time, until it is mended
(Addendum 10(b): "reservoirs leak"). `LeakDaysToEmptyAtCeiling` = {SRC["LeakDaysToEmptyAtCeiling"]}: at the wear ceiling a
store loses its whole capacity to the ground in {SRC["LeakDaysToEmptyAtCeiling"]} days, and proportionally less below it.
Loss, not transfer - this water is gone, not poured into a pool somebody can fetch back.

It is deliberately NOT a level term. The level reads a store's declared `Carries`, which is
what the vessel HOLDS; the leak takes what is IN it. So the leak lands on the water economy
(Q1-Q3) and never on the equilibrium (Q6-Q9), and the two consequences of one ruined cistern
are a lower level AND a thinner cushion, arrived at down two separate paths.
""")
    # Every vessel the settlement raises, producer and store alike. Before G1 this filtered on
    # `"water" in d.carries` and so listed only the things that claimed to make water; the
    # claim and the vessel are separate facts now, and what leaks is the vessel.
    stores = sorted(
        (d for d in CATALOGUE if d.capacity > 0),
        key=lambda d: d.capacity,
    )
    median = ruin_increment(49)
    wears = (median, median * 2, SRC["MaxWearPercent"])
    print(
        f"  {'store':<15}{'rung':<10}{'cap':>6}{'bill/day':>9}   drams/day, and as a share of the bill"
    )
    print(
        f"  {'':<15}{'':<10}{'':>6}{'':>9}   "
        + "   ".join(f"wear {w:>2}" for w in wears)
    )
    for d in stores:
        # The rung's own floor population against its own upkeep rate: what the place drinks
        # in a day when it is standing exactly on the rung this store unlocks.
        bill = upkeep_per_day(
            max(STAGES[d.stage][1], SRC["FloorLevel"]), STAGES[d.stage][2]
        )
        # Over a hundred days, then divided back: the rate is honest at any granularity, but a
        # small store's DAILY share floors to zero, which is why the engine banks days it could
        # not cash rather than spending them. Held is deliberately unbounded here - this is the
        # RATE, and the real thing is additionally clamped by whatever is actually in the vessel.
        rates = [leaked(d.capacity, 1 << 40, w, 100) / 100.0 for w in wears]
        cells = "   ".join(
            f"{r:>4.1f} {int(r * 100 // max(bill, 1)):>2}%" for r in rates
        )
        print(
            f"  {d.key:<15}{STAGES[d.stage][0]:<10}{d.capacity:>6}{bill:>9}   {cells}"
        )
    catchment = next(d for d in stores if d.key == "catchment")
    reservoir = next(d for d in stores if d.key == "reservoir")
    worst = max(stores, key=lambda d: leaked(d.capacity, 1 << 40, SRC["MaxWearPercent"], 100)
                / max(upkeep_per_day(max(STAGES[d.stage][1], SRC["FloorLevel"]), STAGES[d.stage][2]), 1))
    worst_share = (
        leaked(worst.capacity, 1 << 40, SRC["MaxWearPercent"], 100)
        // max(upkeep_per_day(max(STAGES[worst.stage][1], SRC["FloorLevel"]), STAGES[worst.stage][2]), 1)
    )
    # The sizing law the water lane is built to, asserted rather than described. A vessel whose
    # hole outruns its rung's drinking is a vessel that can make one bad season fatal.
    assert worst_share < 100, (
        f"STORE OVERSIZED: {worst.key} leaks {worst_share}% of its own rung's daily bill at the "
        "wear ceiling. Every vessel must stay under 100 - see the sizing note in "
        "ObjectBlueprints.xml's water works block."
    )
    print(f"""
Reading.

1. CAMP CANNOT LEAK, and now it has nothing to leak either. A store asks for no crew, and the
   only thing in the game that damages a staffless work is a lost rung
   (`KingdomSubsidence.Ruin` walks `Survey.Built`; raid damage walks `Survey.Works`, which is
   crewed works only). Camp is the floor and never subsides. Since G1 no water design opens at
   Camp at all, so the first row above is a Steading's, and every row is a state a settlement
   can only reach by first overreaching and losing a rung.

2. THE BITE IS PROPORTIONATE BECAUSE THE RATE IS. A leak is denominated against the store's OWN
   capacity, so the {catchment.capacity}-dram catchment a steading keeps loses about {leaked(catchment.capacity, 1 << 40, SRC["MaxWearPercent"], 100) / 100.0:.1f} drams a day at
   its worst and the {reservoir.capacity}-dram reservoir a town keeps loses {leaked(reservoir.capacity, 1 << 40, SRC["MaxWearPercent"], 100) // 100}. Big cushions leak big, small
   cushions leak small, both in the same proportion to what they were built to hold, and
   neither can lose more than is actually in there. This is also the law the vessels are SIZED
   to: `{worst.key}` is the worst row above at {worst_share}% of its own rung's bill, the
   assertion above this text refuses anything at 100 or over, and it is why the cistern court
   holds 256 rather than 512.

3. IT CANNOT DRINK A SETTLEMENT DRY BY ITSELF, and the rate is deliberately tuned so that it
   never could. Two reasons, and both matter.

   The first is ORDER. The day's upkeep is drawn during the growth pass and the leak is taken
   afterwards (`KingdomSystem` runs "growth", then "wear"), so a leak only ever takes what the
   settlement did not need that day. It cannot be the reason the thirst ladder fires; what it
   costs is the CUSHION (`ReserveDays` = {SRC["ReserveDays"]} days of it), which is exactly the thing a founder
   is meant to notice and mend.

   The second is the RATE. Every row above sits under its own rung's daily bill even at the
   absolute ceiling - around six-sevenths of it for the cisterns a real settlement keeps, a
   third or less for every producer - and at the wear ONE lost rung actually leaves, around a
   quarter. So a settlement that is making its water still banks some of it with a hole in the
   cistern, and a settlement that is not was already in trouble before the leak. `LeakDaysToEmptyAtCeiling` is the only constant this
   pass tuned, and that band is what it was tuned to: visible, mendable, and never the whole of
   a day's drinking.

4. AND IT IS BOUNDED BY THE MENDING, not by a timer. Mending restores function outright - the
   leak is a function of current wear and nothing else, so a mended store leaks zero the same
   day. Nothing here accumulates a debt, and nothing here remembers.""")


def q9_feedback():
    rule("Q9  The loop: ruin -> effectiveness -> level -> more ruin, and why it stops")
    median = ruin_increment(49)
    hardest = ruin_increment(99)
    mildest = ruin_increment(0)
    print(f"""
One lost rung asks EVERY standing work once, at that rung's own reach ({ruin_chance_for(0)}% Camp,
{ruin_chance_for(1)}% Steading, {ruin_chance_for(2)}% Village, {ruin_chance_for(3)}% Town, {ruin_chance_for(4)}% City) rather than filling a
quota, adding `RuinIncrement(roll)` wear to whichever it catches -
{mildest} on the kindest draw, {median} at the median, {hardest} on the worst. Wear stops at
`MaxWearPercent` = {SRC["MaxWearPercent"]}, so a work is as run down as it will ever be after {math.ceil(SRC["MaxWearPercent"] / median)} median rungs
or {math.ceil(SRC["MaxWearPercent"] / hardest)} hard ones, and a city that falls all the way to Camp is derelict rather than gone.

What that costs the LEVEL runs through two multiplications and one gate:
""")
    print(
        f"  {'wear':>6}{'condition':>11}{'crewed x100':>13}{'staffless':>11}   what the founder sees"
    )
    for wear in (0, median, median * 2, median * 3, SRC["MaxWearPercent"]):
        wear = min(wear, SRC["MaxWearPercent"])
        print(
            f"  {wear:>6}{condition_percent(wear):>11}{work_effectiveness(1, 100, wear):>13}"
            f"{work_effectiveness(0, 0, wear):>11}   {'sound' if wear == 0 else ('knocked about' if wear < 20 else ('badly used' if wear < 40 else 'half-wrecked'))}"
        )
    # The named staffless water work, whichever it currently is: the grandest Staff=0 design in
    # the water lane. It was `reservoir` until Addendum 11(a) flipped every store to storage-only;
    # reading it out of the catalogue rather than naming it keeps this paragraph true through the
    # next re-grounding as well.
    staffless = max(
        (d for d in CATALOGUE if "water" in d.carries and d.staff == 0),
        key=lambda d: d.carries["water"],
    )
    field = staffless.carries["water"]
    print(f"""
THE STAFFLESS COLUMN IS THE RULING (Addendum 10(b)). It used to read a flat 100 down its whole
length: `KingdomSubsidence.Supports` read `KingdomEffectiveness` only when the design wanted a
crew, so wear reached the level exclusively through CREWED works and a half-wrecked `{staffless.key}`
carried its full {field} drams. It now reads the work's own CONDITION, so the same work carries
{carried(field, condition_percent(SRC["MaxWearPercent"]))}. Both columns are 100 for a sound work, which is why this is a refinement of the
old ternary rather than a new tax on standing still. Note what the work now IS: since G1 the
grandest staffless water design is a PRODUCER rather than a vessel, so what a lost rung takes
is condensing capacity, and the settlement feels it as drams that stop arriving rather than as
a level that quietly re-reads lower.

Which means the loop no longer bottoms out in the food lane alone. Water and roof automate to
Staff=0 at their large and grand rungs and USED to be immune for exactly that reason; they are
not now. Here is each rung's grandest plan with every work run to the ceiling - the worst the
loop can do, by construction:
""")
    print(
        f"  {'rung':<9}{'staff':>6}{'sound':>7}{'wrecked':>9}{'floor':>7}   binds when wrecked"
    )
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            continue
        plan = grandest_plan(i)
        if not plan:
            continue
        sound = [[d, 0] for d in plan]
        wrecked = [[d, SRC["MaxWearPercent"]] for d in plan]
        tally = supports(wrecked)
        binder = min(
            ("water", level_from_water(tally["water"], i)),
            ("food", tally["food"]),
            ("roof", tally["roof"]),
            key=lambda pair: pair[1],
        )[0]
        print(
            f"  {name:<9}{sum(d.staff for d in plan):>6}{level_of(sound, i):>7}"
            f"{level_of(wrecked, i):>9}{SRC['FloorLevel']:>7}   {binder}"
        )
    print("""
And the cheapest plan - the many-small-plots build, which at every rung above Steading is
entirely staffless. Under the old ternary its wrecked column EQUALLED its sound one, which was
the seam: a city built out of staffless designs took no level damage from ruin at all.
""")
    print(f"  {'rung':<9}{'staff':>6}{'sound':>7}{'wrecked':>9}   plan")
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            continue
        plan = cheapest_plan(i)
        if not plan:
            continue
        sound = [[d, 0] for d in plan]
        wrecked = [[d, SRC["MaxWearPercent"]] for d in plan]
        keys = []
        for d in plan:
            if not keys or keys[-1][0] != d.key:
                keys.append([d.key, 0])
            keys[-1][1] += 1
        print(
            f"  {name:<9}{sum(d.staff for d in plan):>6}{level_of(sound, i):>7}"
            f"{level_of(wrecked, i):>9}   {' | '.join(k + 'x' + str(n) for k, n in keys)}"
        )

    print("""
Now the slide itself, run with the loop closed. Same shape as Q8, but the level is
recomputed from the works' actual condition every step, and every lost rung asks every
standing work independently at that rung's own reach - there is no cursor and no order left to
be adversarial about (the reach "does not depend on which work happened to be raised first").
`fair` runs the median draw, `worst` the hardest; `static` is Q8's answer, with the loop cut,
for the difference.
""")
    print(
        f"{'case':<30}{'static':>7}{'fair':>6}{'worst':>7}   "
        f"{'static ends':<13}{'worst ends':<13}{'hits':>5}"
    )
    # A settlement standing exactly on its own rung's plan is inside its band and never
    # slides, so every case here is an OVERREACH: the people of one rung, the works of a
    # lower one. That is the only state the loop can open in.
    cases = [
        ("City 50, a Town's grand works", 50, 4, 1024, 3, "grandest"),
        ("City 50, a Town's small works", 50, 4, 1024, 3, "cheapest"),
        ("City 50, a Village's works", 50, 4, 1024, 2, "grandest"),
        ("Town 25, a Steading's works", 25, 3, 256, 1, "grandest"),
        ("Town 25, nothing but gardens", 25, 3, 256, 1, "cheapest"),
    ]
    for label, pop, stage, cap, built_for, how in cases:
        plan = (
            cheapest_plan(built_for) if how == "cheapest" else grandest_plan(built_for)
        )
        s_pop, s_stage, s_steps, _b, _h, _s = slide_with_ruin(
            pop, stage, cap, plan, None
        )
        f_pop, f_stage, f_steps, _b, f_hits, _s = slide_with_ruin(
            pop, stage, cap, plan, 49
        )
        w_pop, w_stage, w_steps, _b, w_hits, _s = slide_with_ruin(
            pop, stage, cap, plan, 99
        )
        print(
            f"{label:<30}{s_steps * SRC['StepDays']:>7}{f_steps * SRC['StepDays']:>6}"
            f"{w_steps * SRC['StepDays']:>7}   "
            f"{STAGES[s_stage][0] + ' ' + str(s_pop):<13}"
            f"{STAGES[w_stage][0] + ' ' + str(w_pop):<13}{w_hits:>5.1f}"
        )
    print(f"""
Reading. Three things, and the third is the one worth keeping.

1. A settlement INSIDE its band never starts, so the loop never opens. It is a consequence of
   overbuilding, not a tax on standing still - the ruin only ever begins after the settlement
   has already lost a rung it could not hold.

2. The loop is real, bounded, and STILL worth about ONE RUNG after Addendum 10(b) widened
   what it reaches. Every adversarial row above lands one rung and a handful of days below its
   own loop-cut column, and not two. It cannot do much better than that, because it cannot
   outrun its two bounds: wear stops at {SRC["MaxWearPercent"]}, so no work ever carries less than {condition_percent(SRC["MaxWearPercent"])}% of its
   figure, and the level stops at `FloorLevel` = {SRC["FloorLevel"]}. Every case terminates, and the extra days
   the loop adds are days the settlement spends losing people it was never carrying. Nothing
   here needs tuning; it needed measuring, and then re-measuring when the ruling moved it.

3. THE DOOR OUT OF THE LEVEL IS CLOSED. Under the old ternary a city built entirely out of
   staffless designs took NO level damage from ruin - its wrecked column equalled its sound
   one - and the whole of the loop's surface was the food lane, which never automates. Addendum
   10(b) shut that door: the cheapest plan's wrecked column above is now well under its sound
   one at every rung, and the automation lanes pay for their ruin like everything else. That is
   the delta this pass was for, and it costs the adversarial cases roughly one further rung.

   It does not become a death spiral, for the reasons in (2): wear stops at {SRC["MaxWearPercent"]} so nothing
   ever carries less than {condition_percent(SRC["MaxWearPercent"])}% of its figure, the level stops at `FloorLevel` = {SRC["FloorLevel"]}, and
   Camp - which is where a floored settlement stands - never subsides and therefore never
   ruins anything.

   The staffless build was never untouched even before this. `KingdomLodgingRules.CondemnedWearPercent`
   = {CONDEMNED_WEAR} is a consequence on a DIFFERENT reckoning: a home worn that far stops counting as a
   roof for lodging - whoever built it, crewed or not - which blocks arrivals and records a roof
   brink for everyone under it. {math.ceil(CONDEMNED_WEAR / median)} median rungs reach it. Condemnation and the level are
   separate ladders and stay separate; this pass did not merge them.""")
    leak_table()


def w6_production_and_logistics():
    """Wave 6: the rates move onto the city model, and the logistics stop looking stupid.

    THE ONE THING THIS SECTION EXISTS TO PROVE. Every rung above was derived on a daily balance
    where the SEATED zone's works were credited for the settlement's whole elapsed, once per pass,
    out of `KingdomGrowth`. W6 moved that arithmetic onto `Simulation/City` - per zone, off the
    city model's single `ProcessedThroughTick`. A move is only safe if the number is the same
    number, so the first four checks are source facts, asserted rather than trusted: the old
    crediting is GONE from the settlement pass, the new one reads the SAME `Supports` tally the
    ladder is derived from. The model publishes the water stamp as its mirror; the food stamp
    remains the mills' separate physical clock.
    """
    rule("W6  Production on the model, and logistics that never look stupid")

    growth = read_source(source_family_paths("Growth", "KingdomGrowth"))
    city = read_source(source_family_paths("Simulation/City", "KingdomCity"))
    rules = read_source(CITY_CS)
    production = read_source(PROD_CS)
    budget_family = source_family_paths("Simulation/City", "KingdomBudgetRules")
    budget = read_source(budget_family)
    logistics = read_source(source_family_paths("Simulation/City", "KingdomLogisticsRules"))

    print("""
1. NO DAY IS BILLED TWICE, AND IT IS STRUCTURAL RATHER THAN CAREFUL. Two owners of one day is a
   day paid twice, so W6 leaves exactly one owner. The settlement pass no longer credits the
   water works or the fields at all; the model integrates every zone's carry off its own
   `ProcessedThroughTick`; and `KingdomCity.Stamp` writes `LastWaterWorkTick` FROM that tick, so
   the settlement's stamp is a published mirror of the model's clock rather than a second clock
   beside it. `LastFoodWorkTick` is left alone on purpose - see 3.
""")
    assert "survey.Store(KingdomSubsidence.Supports(survey).Water * madeDays)" not in growth, (
        "BILLED TWICE: the settlement pass still credits the water works on its own clock."
    )
    assert "StoreHarvest(System, survey, FoodMadePerDay(survey) * grownDays)" not in growth, (
        "BILLED TWICE: the settlement pass still credits the fields on its own clock."
    )
    assert "System.LastWaterWorkTick = state.ProcessedThroughTick" in city, (
        "the water work stamp is no longer the model's published mirror"
    )
    assert "System.LastFoodWorkTick = state.ProcessedThroughTick" not in city, (
        "THE MILLS WOULD STARVE: KingdomCity.Stamp must not write LastFoodWorkTick. The fields' "
        "clocked make moved onto the model; the MILLS did not, and that stamp is theirs. Written "
        "from the reckon it would read `now` on every check-in and no mill would ever grind."
    )
    print("   settlement pass credits water works:  no")
    print("   settlement pass credits fields:       no")
    print("   model stamps LastWaterWorkTick:       yes  (KingdomCity.Stamp)")
    print("   model stamps LastFoodWorkTick:        no   (it is the MILLS' stamp - see 3)")

    print("""
2. AND IT IS THE SAME NUMBER. The model's per-zone rate is not a new figure invented for the
   model: it is `KingdomSubsidence.Supports(Survey).Water` and `KingdomGrowth.FoodMadePerDay`,
   the exact two the level and every rung above are derived from. If those ever became two
   answers, a reservoir would be worth one thing to the ladder and another to the casks.
""")
    assert "KingdomSubsidence.Supports(Survey).Water" in city, (
        "the model's water rate is no longer the ladder's own Supports tally"
    )
    assert "KingdomGrowth.FoodMadePerDay(Survey)" in city, (
        "the model's food rate is no longer KingdomGrowth's own figure"
    )
    assert "KingdomCrops.MilledFoodPerDay(Survey)" in growth, (
        "FED TWICE: FoodMadePerDay no longer subtracts what the mills deliver physically."
    )
    print("   water rate  = KingdomSubsidence.Supports(Survey).Water    (the ladder's own tally)")
    print("   food rate   = KingdomGrowth.FoodMadePerDay(Survey)        (fields and mills already out)")

    print("""
3. THE MILL KEPT ITS OWN CLOCK, AND HAD TO. A mill does not make food out of the day - it takes
   real crops off real shelves and puts real staples back, where the shelves are. It was never in
   the model's rate (`MilledFoodPerDay` is subtracted out of `FoodMadePerDay`), so it keeps
   `LastFoodWorkTick`'s elapsed for itself. One clock each; neither can spend the other's days.
""")
    assert "GrindHarvest(System, survey, grownDays)" in growth, "the mill no longer runs"
    order_rations = growth.find("bool heartbeatHealthy = ResolveHeartbeat(")
    order_mill = growth.find("GrindHarvest(System, survey, grownDays)")
    assert order_rations > 0 and order_mill > order_rations, (
        "INDUSTRY EATS FIRST: GrindHarvest must still run after the ration draw."
    )

    print("""
4. THE BACKLOG IS BOUNDED BY THE CONTAINERS, NEVER BY THE ABSENCE. A season away cannot grow an
   unbounded claim, because production is clamped by the room the model believes the zone has and
   the overflow is SPILLED - the same loss a harvest with a full larder has always taken. That is
   what makes the amortised landing finite: the worst debt a quarter can present is its own
   capacity, and §0.0(b) prices draining a full backlog inside the 40-turn grace window.
""")
    assert "long room = (wanted > 0L) ? (capacity - level) : level;" in production, (
        "UNBOUNDED CLAIM: production is no longer clamped by the room the containers have."
    )
    assert "step = new KingdomProductionStep(nextLevel, (int)nextOwed, moved, wanted - moved);" in production, (
        "the spill is no longer reported, so a lost harvest would be silently absorbed"
    )
    assert "long nextOwed = nextLevel - groundLevel;" in production, (
        "I1 BROKEN: the reconcile no longer re-derives the debt, so `level - owed == ground` "
        "stops being true by construction and the audit line stops being exact."
    )
    reify_units = read_const(budget_family, "ReifyUnitsPerTurn")
    catchup = read_source(source_family_paths("Simulation/City", "KingdomCatchUpRules"))
    expected_envelope = (
        "KingdomRules.MaxCivicContainersPerZone + KingdomRules.MaxPopulation"
    )
    assert expected_envelope in catchup, (
        "WorstBacklogUnits no longer derives from the live container and population rails."
    )
    stage_cap = re.search(
        r"public static int MaxBuildingsForStage\(.*?default:\s*return\s+([0-9]+);",
        read_source(RULES_CS), re.S)
    assert stage_cap, "City building cap not found in MaxBuildingsForStage"
    worst_units = (
        int(stage_cap.group(1))
        + read_const(RULES_CS, "MaxDedicatedVessels")
        + read_const(RULES_CS, "MaxDedicatedLarders")
        + read_const(RULES_CS, "MaxPopulation")
    )
    drain_turns = -(-worst_units // reify_units)
    print(f"   reify budget:      {reify_units} units a turn")
    print(f"   worst backlog:     {worst_units} units")
    print(f"   turns to drain:    {drain_turns}   (§0.0(b) warns above 40)")
    assert drain_turns <= 40, "the worst backlog no longer drains inside its own warn rung"

    print("""
5. WHAT THE MOVE ACTUALLY CHANGED FOR A PLAYER, RE-DERIVED. Every column in Q1-Q11 above is a
   ONE-QUARTER balance and none of them moves: the same works make the same drams on the same
   day. What changes is that the city's OTHER quarters now make theirs too. Before W6 a work in a
   zone the founder was not standing in produced nothing at all, whatever it was built to do; the
   settlement's whole make was whatever the seated ground happened to carry. Below, one rung's
   binding water bill against what one, two, three and four producing quarters bring in at that
   rung's cheapest plan.
""")
    print(f"  {'rung':<10}{'bill/day':>10}{'1 quarter':>12}{'2':>8}{'3':>8}{'4':>8}   holds at")
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            print(f"  {'Camp':<10}{upkeep_per_day(4):>10}{0:>12}{0:>8}{0:>8}{0:>8}   nothing (11(a) gates the lane)")
            continue
        need = math.ceil(floor * STAGE_PERCENT[i] / 100)
        globals()["_KIND"] = "water"
        got = _plan(
            "water",
            i,
            need,
            lambda d: (d.cost / max(d.carries.get("water", 1), 1), -d.carries.get("water", 0)),
        )
        assert got, f"RUNG IMPOSSIBLE: no water design is reachable at {name}"
        design, count = got
        made = design.carries["water"] * count
        quarters = [made * q for q in (1, 2, 3, 4)]
        holds = next((q for q in (1, 2, 3, 4) if quarters[q - 1] >= need), None)
        assert holds is not None, (
            f"RUNG IMPOSSIBLE AFTER W6: {name} cannot be held by four quarters of its own "
            f"cheapest plan"
        )
        assert holds == 1, (
            f"REGRESSION: {name} used to be holdable by the seated quarter alone and now needs "
            f"{holds}. W6 may only ADD the other quarters' make."
        )
        print(
            f"  {name:<10}{need:>10}{quarters[0]:>12}{quarters[1]:>8}{quarters[2]:>8}{quarters[3]:>8}"
            f"   {holds} quarter"
        )
    print("""
   Read it as the strictly-additive change it is: one quarter still holds every rung on its own,
   exactly as Q1-Q11 derive, so nothing above needs re-tuning. A four-quarter City simply stops
   throwing away three quarters of what it built. That is the whole balance consequence of W6,
   and it is a ceiling being lifted rather than a floor being moved.
""")

    print("""
6. THE PLANNER'S BOUNDS ARE CONSTANTS, AND THEY MATCH THE CONSTITUTION. §3.10(4) prices one
   slice's routing at 16 jobs, 8 stops and 50 swap tests - about a thousand integer operations.
   Those three numbers live in `KingdomBudgetRules` and the planner reads them from there, so a
   tuning change cannot leave the budget table behind.
""")
    for name, want in (("PlannerMaxJobs", 16), ("PlannerMaxStops", 8), ("PlannerMaxSwapTests", 50)):
        got = read_const(budget_family, name)
        assert got == want, f"{name} moved to {got}; §3.10(4) prices the slice at {want}"
        print(f"   {name:<22}{got:>5}")
    assert "PlannerMaxDraws = 0" in budget, (
        "A DRAW IN THE PLANNER: routing is arithmetic, and §3.10(4) allows the lane no draws."
    )
    assert "KingdomBudgetRules.PlannerMaxJobs" in logistics, "the planner no longer reads its own budget"
    assert "KingdomBudgetRules.PlannerMaxStops" in logistics, "the planner no longer reads its own budget"
    assert "KingdomBudgetRules.PlannerMaxSwapTests" in logistics, "the planner no longer reads its own budget"
    print("   PlannerMaxDraws            0   (routing is arithmetic, never chance)")

    print("""
7. AND THE CARRY GOES TO THE NEAREST GROUND. §3.10(1): a shortfall where the founder is standing
   is met out of the closest quarter actually holding the resource, on the level-1 zone graph,
   tie-broken on the lower row index. Inside a quarter the oldest dedication still pays first
   (§3.9, I4) - the two rules answer different questions and both stay true.
""")
    assert "TryZoneDistances(state, seatedZoneId, shafts, cells, out fault)" in rules, (
        # The delve wave threaded the shaft set through the same call: still the seat,
        # still distance-ordered, and now honest about which strata connect at all.
        "WALKS PAST A NEARER STORE: the carry is no longer ordered by distance."
    )
    assert "sources[i] = new KingdomVesselRow(i, cells[i], kind, available, available, true);" in rules, (
        "the carry's ordering key is no longer the distance to the seat"
    )
    print("   carry order:  distance to the seat, then row index   (KingdomCityRules.TryPlanTransfer)")
    print("   drain order:  dedication ordinal, then vessel id     (KingdomDrainRules.TryOrder, unmoved)")


def w7_networks_and_power():
    """Wave 7: the networks, and the power lane's move onto one accounting.

    THE ONE THING THIS SECTION EXISTS TO PROVE. W6 proved production is billed once by leaving
    exactly one owner of a day. W7 does the same thing to CHARGE, and the shape of the proof is
    identical: the old owner is gone from the source, the new one is the model's own counter, and
    the arithmetic that used to be duplicated is now asserted equal rather than separately
    computed. Every check here is a source fact or a closed-form identity - nothing is trusted.
    """
    rule("W7  Networks, the flow solve, and power on one accounting")

    power = read_source(source_family_paths("Growth", "KingdomPower"))
    power_rules = read_source((
        os.path.join(ROOT, "Growth", "KingdomPowerRules.cs"),
        os.path.join(ROOT, "Growth", "KingdomPowerOperationsRules.cs"),
    ))
    flow = read_source(source_family_paths("Simulation/City", "KingdomFlowRules"))
    net = read_source(source_family_paths("Simulation/City", "KingdomNetworkRules") + (
        "Simulation/City/KingdomNetworkKind.cs",
        "Simulation/City/KingdomNetworkRole.cs",
        "Simulation/City/KingdomWorkTier.cs",
        "Simulation/City/KingdomNetworkNode.cs",
        "Simulation/City/KingdomNetworkEdge.cs",
        "Simulation/City/KingdomJoinVerdict.cs",
        "Simulation/City/KingdomNetworkGraph.cs",
        "Simulation/City/KingdomNetworkGraph.Build.cs",
        "Simulation/City/KingdomNetworkGraph.Bottleneck.cs",
    ))
    memory = read_source(source_family_paths("Simulation/City", "KingdomCityMemoryRules"))
    blueprints = open(BLUEPRINTS_XML, encoding="utf-8-sig").read()

    print("""
1. THE POWER LANE HAS ONE CLOCK, AND IT IS THE MODEL'S. Power used to count its own days, per
   work, off `KingdomRules.ElapsedDays` and a remainder-keeping checkpoint, and then do its own
   summing, its own store clamp and its own delivery. That was a second accounting standing
   beside the model's - exactly what W6 made unrepresentable for production. Days are now
   WORLD-DAY BOUNDARIES through the one counter every other lane uses.
""")
    assert "KingdomProductionRules.TryDaysBetween(through, timeTicks, KingdomRules.TicksPerDay" in power, (
        "BILLED TWICE: the power pass no longer counts days off the model's own boundary counter."
    )
    assert "int days = CreditDays(part.LastResolvedTick, TimeTicks, out var next);" not in power, (
        "BILLED TWICE: the per-work credit loop is still in KingdomPower."
    )
    assert "KingdomRules.AdvanceCheckpoint" not in power, (
        "the power lane still keeps a remainder-carrying checkpoint of its own"
    )
    print("   power counts days off:  KingdomProductionRules.TryDaysBetween   (world-day boundaries)")
    print("   per-work credit loop:   gone")
    print("   remainder checkpoint:   gone")

    print("""
2. AND ONE NETTING. The summing, the store clamp, the deficit and the stop list all happen in
   `KingdomFlowRules.TrySolve`. `KingdomPowerRules` keeps the RATES - which is what it was always
   for - and its span arithmetic is now the NAMED FORM of what the solve produces. The two are
   asserted equal in test rather than separately computed, which is what stops them drifting.
""")
    assert "KingdomFlowRules.TrySolve(" in power, "the power pass does not go through the flow solve"
    assert "KingdomPowerRules.ThroughputForDays(capacity, 1)" in power, (
        "the store's throughput is no longer the power rules' own constant"
    )
    for named in ("ChargeForDays", "Absorbable", "Releasable"):
        assert f"public static int {named}(" in power_rules, (
            f"KingdomPowerRules.{named} was deleted rather than kept as the named form of the solve"
        )
    print("   netting:                KingdomFlowRules.TrySolve")
    print("   store throughput:       KingdomPowerRules.ThroughputForDays  (one constant, one caller)")
    print("   ChargeForDays / Absorbable / Releasable:  kept, asserted equal to the solve in test")

    print("""
3. FLOW CONSERVATION IS AN IDENTITY, NOT A PROMISE. Generated + Discharged == Delivered +
   Charged + Spilled, in every branch. There is no fourth destination for a charge and nothing
   arrives from a fifth source, so a solve that invented or lost one would fail arithmetic rather
   than fail a review. Re-derived here from the source's own branch structure.
""")
    assert "solution = new KingdomFlowSolution(generated, demanded, delivered, charged, discharged, spilled, shortfall, stopped);" in flow
    for branch in ("charged = (net < chargeCap) ? net : chargeCap;", "spilled = net - charged;", "discharged = -net;"):
        assert branch in flow, f"the solve's conservation branch moved: {branch}"
    # The identity, evaluated over a grid rather than asserted about.
    def solve(supply_per_day, demands, level, capacity, throughput_per_day, days):
        demanded = sum(demands) * days
        generated = supply_per_day * days
        throughput = throughput_per_day * days
        charge_cap = min(capacity - level, throughput)
        discharge_cap = min(level, throughput)
        shortfall = max(0, demanded - generated - discharge_cap)
        relieved, stopped = 0, 0
        order = sorted(range(len(demands)), key=lambda i: -demands[i])
        while stopped < len(demands) and relieved < shortfall:
            relieved += demands[order[stopped]] * days
            stopped += 1
        delivered = max(0, demanded - relieved)
        net = generated - delivered
        charged = min(net, charge_cap) if net >= 0 else 0
        spilled = (net - charged) if net >= 0 else 0
        discharged = 0 if net >= 0 else -net
        return generated, delivered, charged, discharged, spilled, stopped
    checked = 0
    for supply in (0, 1200, 2400, 4800, 50000):
        for level in (0, 6000, 12000, 24000):
            for days in (1, 7, 90, 365):
                for demands in ([], [4000], [4000, 4000], [100] * 6):
                    g, d, c, dis, sp, st = solve(supply, demands, level, 24000, 12000, days)
                    assert g + dis == d + c + sp, (
                        f"FLOW CONSERVATION BROKE at supply={supply} level={level} days={days} "
                        f"demands={demands}: {g}+{dis} != {d}+{c}+{sp}"
                    )
                    checked += 1
    print(f"   conservation checked over {checked} (supply x store x span x demand) combinations: holds")

    print("""
4. NO TERM IN THE ELAPSED. A one-day span and a ninety-day span are the same arithmetic: days
   multiply the rates once and appear nowhere else. §0.0(a)'s identity, for this lane.
""")
    one = solve(2400, [1000], 0, 240000, 120000, 1)
    season = solve(2400, [1000], 0, 240000, 120000, 90)
    assert season[0] == one[0] * 90 and season[1] == one[1] * 90 and season[2] == one[2] * 90, (
        "the solve is not linear in the span; something in it counts days twice"
    )
    assert season[5] == one[5], "a longer span changed the stop set at constant rates"
    print(f"   one day:     generated={one[0]:>7}  delivered={one[1]:>7}  charged={one[2]:>7}  stopped={one[5]}")
    print(f"   ninety days: generated={season[0]:>7}  delivered={season[1]:>7}  charged={season[2]:>7}  stopped={season[5]}")

    print("""
5. THE SOLVE'S OP BOUND, COMPOSED. §0.0 prices one network solve at O(nodes + edges) and the
   caps at 32 nodes and 48 edges, so 80 node-visits; re-solves across a whole reckoning are
   bounded by B = 64 breakpoints, not by B x networks. That ceiling is only affordable because
   the traversal ORDER is precomputed when the topology is laid - a walk that had to find
   neighbours by scanning the edge array is nodes x edges, nineteen times over.
""")
    nodes, edges, breakpoints = 32, 48, 64
    per_solve = nodes + edges
    naive = nodes * edges
    assert per_solve == 80
    assert breakpoints * per_solve == 5120
    assert "internal const int NetworkTraversalBytesPerNode = 2;" in memory, (
        "the traversal order is no longer stored, so the solve cannot honour its own op bound"
    )
    print(f"   one solve, precomputed order:  {per_solve:>6} node-visits")
    print(f"   one solve, scanning edges:     {naive:>6} node-visits   ({naive // per_solve}x the ceiling)")
    print(f"   whole reckoning (B={breakpoints}):          {breakpoints * per_solve:>6} node-visits   (§0.0 budget: 5,120)")

    print("""
6. THE MEMORY THE ORDER COSTS, AND WHAT IT BUYS. Two bytes a node against 162 for a full
   adjacency index. §0.0(c) takes the edit; the ceiling does not move.
""")
    per_network = nodes * 16 + edges * 16 + nodes * 2 + 64
    assert per_network == 1408
    realm = 2 * 4 * per_network
    csr = (nodes + 1) * 2 + edges * 2
    print(f"   per network:      {per_network:>6} B   (nodes {nodes * 16} + edges {edges * 16} + order {nodes * 2} + header 64)")
    print(f"   per realm:        {realm:>6} B")
    print(f"   order bytes:      {nodes * 2:>6} B   vs a full adjacency index at {csr} B")

    print("""
7. THE LIQUID LAW HAS FOUR PIECES AND ONE REFUSAL, AND THEY ARE IN THE XML. Connection is
   DECLARED: a main says what runs in it and which faces it offers, a crossing types NOTHING so
   it can never be the place two liquids met, and a cross-liquid join refuses by name.
""")
    # New pieces start as useful east-west straights.  Their exact serialized mask is frozen on
    # the piece and the player-facing Configure action offers every legal cap/end/straight/corner/
    # tee/cross form; old saves that already hold NSEW remain readable.  Pin the creation default,
    # not the legacy value that W7 deliberately stopped writing.
    for blueprint, needle in (
        ("r_KingdomWaterMain", 'Liquid="water" Joins="EW"'),
        ("r_KingdomBrineMain", 'Liquid="salt" Joins="EW"'),
        ("r_KingdomLiquidCrossing", 'Pairs="NSEW"'),
        ("r_KingdomWaterTap", 'Liquid="water" Joins="EW"'),
        ("r_KingdomBrineTap", 'Liquid="salt" Joins="EW"'),
    ):
        assert f'<object Name="{blueprint}"' in blueprints, f"{blueprint} is not in ObjectBlueprints.xml"
        assert needle in blueprints, f"{blueprint} does not carry its declaration: {needle}"
    crossing = blueprints[blueprints.index('<object Name="r_KingdomLiquidCrossing"'):]
    crossing = crossing[: crossing.index("</object>")]
    # Comments are prose about the piece and not part of it; the DECLARATIONS are what the engine
    # reads and what this section is answerable for.
    crossing_parts = re.sub(r"<!--.*?-->", "", crossing, flags=re.S)
    assert 'Liquid="' not in crossing_parts, (
        "A CROSSING THAT TYPES SOMETHING CAN MERGE SOMETHING: the crossover must hold no liquid "
        "declaration at all, or it is a place two lines could meet."
    )
    assert '<part Name="LiquidVolume"' not in crossing_parts, (
        "the crossing is a route, not a length of main; it must hold nothing of its own"
    )
    assert 'return LiquidsMatch(liquidA, liquidB) ? KingdomJoinVerdict.Joined : KingdomJoinVerdict.RefusedLiquid;' in net, (
        "the typed-line refusal moved out of KingdomNetworkRules.JudgeJoin"
    )
    # The hydraulic family's own segment-volume idiom, extended by one verb and nothing else.
    assert 'MaxVolume="8"' in blueprints, "the mains no longer carry BaseHydraulicPipe's segment volume"
    print("   water main / brine main:  typed, four faces declared")
    print("   crossing piece:           no Liquid, no LiquidVolume  (it cannot merge anything)")
    print("   water tap / brine tap:    typed; a tap is the act of tapping, not proximity")
    print("   cross-liquid join:        RefusedLiquid, told by name, never merged")

    print("""
8. A LINE RUNS DOWNHILL AND STOPS LEVEL, IN CLOSED FORM. Moving m from a vessel at Lf/Cf into one
   at Lt/Ct levels them when m = (Ct*Lf - Cf*Lt) / (Cf + Ct). One expression, no loop, no draw,
   and it cannot overshoot into an inverted pair.
""")
    assert "long uphill = lowCap * fullLevel - fullCap * lowLevel;" in flow
    assert "long level = uphill / (fullCap + lowCap);" in flow
    rows = []
    for lf, cf, lt, ct in ((1000, 1000, 0, 1000), (300, 300, 0, 900), (500, 1000, 500, 1000), (900, 1000, 100, 200)):
        m = max(0, (ct * lf - cf * lt) // (cf + ct))
        after_f = (lf - m) / cf
        after_t = (lt + m) / ct
        assert after_f + 1e-9 >= after_t, "the line ran uphill"
        assert m <= lf, "the line gave away more than the vessel held"
        rows.append((lf, cf, lt, ct, m, after_f, after_t))
    print("      from       to      runs    fill after")
    for lf, cf, lt, ct, m, af, at in rows:
        print(f"   {lf:>5}/{cf:<5} {lt:>5}/{ct:<5} {m:>6}    {af:.3f} / {at:.3f}")

    print("""
9. THE BROWNOUT LADDER IS STATED, AND LODGING IS THE MIDDLE RUNG. industry -> refining ->
   amenity -> food -> water -> watch, lowest first, ties on the higher work id. It is the mod's
   own "stop at the loyal core" discipline (the thirst ladder's empty casks and one rung, never
   an empty town) applied to charge: a city gives up what it is DOING before what it IS. Lodging
   sits at amenity because a roof needs no charge to keep the rain off - whether a household
   keeps its home is the roof brink's question, and a brownout must not be able to answer it.
""")
    ladder = ["Industry", "Refining", "Amenity", "Food", "Water", "Watch"]
    for i, rung in enumerate(ladder):
        assert f"{rung} = {i}" in net, f"the brownout ladder moved: {rung} is no longer rung {i}"
    assert 'case "housing":' in flow, "lodging is no longer mapped onto a rung at all"
    housing_block = flow[flow.index('case "housing":'):]
    housing_block = housing_block[: housing_block.index("return")]
    assert "civic" in housing_block, "lodging drifted off the amenity rung"
    for i, rung in enumerate(ladder):
        print(f"   {i}  {rung:<9} {'<- lodging, comfort, civic, faith, memorial' if rung == 'Amenity' else ''}")


@dataclass(frozen=True)
class FounderGraftSlot:
    """One exact slot in the pinned 2.0.211.51 Humanoid anatomy stress fixture."""

    name: str
    slot_type: str
    bears_weapon: bool
    procedure_key: str
    source_magnitude: int | None = None


# D/../Bodies.xml:199-214: the non-abstract graftable Humanoid places, in anatomy order.
# Missile Weapon is abstract and Feet matches no shipped procedure. The two hands bear
# DefaultFist; no other fixture slot below is asked to host a weapon-attached grant.
FULLY_GRAFTED_FOUNDER = (
    FounderGraftSlot("head", "Head", False, "packstooth"),
    FounderGraftSlot("face", "Face", False, "sapskiss"),
    FounderGraftSlot("back", "Back", False, "mirrorcarapace", 100),
    FounderGraftSlot("right arm", "Arm", False, "tarrygrip"),
    FounderGraftSlot("right hand", "Hand", True, "leechpseudopod"),
    FounderGraftSlot("left arm", "Arm", False, "galvanicleech"),
    FounderGraftSlot("left hand", "Hand", True, "vintnersfang"),
    FounderGraftSlot("hands", "Hands", False, "trollkingsgrip"),
)


def _procedure_catalogue() -> dict[str, dict]:
    root = ET.parse(PROCEDURES_XML).getroot()
    assert root.tag.lower() == "kingdomprocedures", "procedure catalogue root moved"
    rows = {}
    for element in root.findall("procedure"):
        key = (element.get("Key") or "").strip().lower()
        assert key and key not in rows, f"duplicate or blank procedure key: {key!r}"
        row = dict(element.attrib)
        row["disclosures"] = [
            (item.get("Text") or "").strip() for item in element.findall("discloses")
        ]
        rows[key] = row
    return rows


def fully_grafted_founder():
    rule("C3  Fully-grafted founder structural stress case")
    catalogue = _procedure_catalogue()
    assert len(catalogue) == 16, "the lab catalogue count moved; re-author the stress fixture"
    assert len({slot.name for slot in FULLY_GRAFTED_FOUNDER}) == 8
    assert tuple(slot.slot_type for slot in FULLY_GRAFTED_FOUNDER) == (
        "Head", "Face", "Back", "Arm", "Hand", "Arm", "Hand", "Hands"
    ), "the pinned Humanoid anatomy fixture moved"

    total_cost = 0
    total_days = 0
    total_preserved = 0
    bits = {}
    grants = set()
    disclosures = 0
    print("  pinned founder: Qud 2.0.211.51 Humanoid; category Animal; eight occupied slots")
    print(f"  {'slot':<14}{'procedure':<20}{'grant':<23}{'water':>7}{'days':>7}{'kept':>7}")
    for slot in FULLY_GRAFTED_FOUNDER:
        row = catalogue.get(slot.procedure_key)
        assert row is not None, f"stress procedure disappeared: {slot.procedure_key}"
        accepted_slots = {value.strip().lower() for value in row["Slots"].split(",")}
        assert slot.slot_type.lower() in accepted_slots, (
            f"{slot.procedure_key} no longer admits the fixture's {slot.slot_type}"
        )
        categories = {
            value.strip() for value in row.get("SlotCategories", "").split(",") if value.strip()
        }
        assert not categories or "Animal" in categories, (
            f"{slot.procedure_key} no longer admits an Animal founder"
        )
        attach = row.get("Attach", "body").lower()
        assert attach != "weapon" or slot.bears_weapon, (
            f"{slot.procedure_key} needs a natural weapon at {slot.name}"
        )
        assert row.get("Source", "part").lower() == "part", (
            "this stress case compares exact copied vanilla parts, not unlike grant routes"
        )
        grant = row["Grants"]
        assert grant not in grants, f"duplicate runtime grant in stress stack: {grant}"
        grants.add(grant)
        band = row.get("Magnitude")
        if band:
            match = re.fullmatch(r"([^:]+):(\d+)-(\d+)", band)
            assert match and slot.source_magnitude is not None
            assert int(match.group(2)) <= slot.source_magnitude <= int(match.group(3)), (
                f"{slot.procedure_key} source magnitude left its authored price band"
            )
        else:
            assert slot.source_magnitude is None
        cost = int(row.get("Cost", "0"))
        days = int(row.get("StaffDays", "0"))
        kept = int(row.get("Preserved", "0"))
        assert cost > 0 and days > 0 and kept > 0
        authored = row["disclosures"]
        assert authored and all(authored), f"{slot.procedure_key} lost consequence disclosure"
        disclosures += len(authored)
        total_cost += cost
        total_days += days
        total_preserved += kept
        for bit in row.get("Bits", ""):
            assert bit.isdigit(), f"unmodelled lab bit token: {bit!r}"
            bits[bit] = bits.get(bit, 0) + 1
        print(f"  {slot.name:<14}{slot.procedure_key:<20}{grant:<23}{cost:>7}{days:>7}{kept:>7}")

    assert (total_cost, total_days, total_preserved) == (305, 66, 12), (
        "the fully-grafted bill moved; review the risk case rather than accepting drift"
    )
    assert len(grants) == len(FULLY_GRAFTED_FOUNDER)
    assert disclosures >= len(FULLY_GRAFTED_FOUNDER) * 2
    print(f"  total bill: {total_cost} drams, {total_days} staffed days, {total_preserved} kept parts")
    print("  bits: " + ", ".join(f"{key}x{bits[key]}" for key in sorted(bits)))
    print(f"  authored consequence lines read before commitment: {disclosures}")
    print("  ninth simultaneous graft: refused; every fixture slot already carries one exact manager")
    print("  structural verdict: PASS (finite slots, exact bands, full resource bill, disclosures)")
    print("  sub-adventuring invariant: UNSIGNED -- requires pinned native combat/session evidence;")
    print("  this simulator does not turn eight vanilla effects into invented damage or defence numbers.")


@dataclass(frozen=True)
class PurposeRecipe:
    source: str
    destination: str
    cargo: str
    water: int
    food: int
    materials: dict[str, int]
    embodied: str
    embodied_units: int
    carried_food: int


def _material_amounts(raw: str) -> dict[str, int]:
    """Parse one exact comma-separated material tally used by source or catalogue XML."""
    answer: dict[str, int] = {}
    if not raw:
        return answer
    for item in raw.split(","):
        fields = item.split(":")
        assert len(fields) == 2 and fields[0] and fields[1].isdigit(), (
            "malformed material tally in a structurally modelled authority: " + raw)
        key, amount = fields[0], int(fields[1])
        assert key in MATERIAL_KEYS and amount > 0 and key not in answer, (
            "unknown, zero, or duplicate material tally row: " + item)
        answer[key] = amount
    return answer


def _building_rows() -> dict[str, dict[str, str]]:
    root = ET.parse(BUILD_XML).getroot()
    answer: dict[str, dict[str, str]] = {}
    for element in root.findall("building"):
        key = element.get("Key")
        assert key and key not in answer, "duplicate or blank building key: " + str(key)
        answer[key] = dict(element.attrib)
    return answer


def q13_purpose_portfolio():
    """Structural closure for five purposeful works and their exact reciprocal cargo cycle.

    This deliberately does not add any catalogue Carries to W6/W7 or any water/food rate to
    G1/G2. It audits the operation-local bill, retained cargo, graph cardinality, and one-at-a-time
    receipt shape only. Native interruption and appearance evidence remain a separate gate.
    """
    rule("Q13 Purpose portfolio: five pairs, ten exact directions, one live operation")
    kind_text = read_source(PURPOSE_KIND_CS)
    catalogue_text = read_source(PURPOSE_CATALOGUE_CS)
    rules_family_text = read_source(
        source_family_paths("Growth", "KingdomPurposePortfolioRules"))
    pair_text = read_source(PURPOSE_PAIR_CS)
    operation_text = read_source(PURPOSE_OPERATION_CS)
    accounting_text = read_source(PURPOSE_ACCOUNTING_CS)
    validation_text = read_source(PURPOSE_VALIDATION_CS)
    transitions_text = read_source(PURPOSE_TRANSITIONS_CS)
    cardinality_text = read_source(PURPOSE_CARDINALITY_CS)
    factories_text = read_source(PURPOSE_FACTORIES_CS)
    topology_text = read_source(PURPOSE_TOPOLOGY_CS)
    registry_text = read_source(PURPOSE_RUNTIME_REGISTRY_CS)
    drive_text = read_source(PURPOSE_DRIVE_CS)
    control_text = read_source(PURPOSE_CONTROL_CS)
    output_text = read_source(PURPOSE_OUTPUT_CS)
    effect_text = read_source(PURPOSE_EFFECT_RULES_CS)
    annexe_text = read_source(ANNEXE_PURPOSE_CS)
    lab_text = read_source(LAB_PURPOSE_SELECTION_CS)
    gate_rules_text = read_source(MIRROR_GATE_RULES_CS)
    gate_runtime_text = read_source(MIRROR_GATE_PURPOSE_CS)

    portfolio_caps = {
        "debit_lines": read_const(
            source_family_paths("Growth", "KingdomPurposePortfolioRules"), "MaxDebitLines"),
        "drive_steps": read_const(PURPOSE_DRIVE_CS, "MaxPurposeOperationSteps"),
        "owned_settlements": read_const(SETTLEMENT_TOPOLOGY_CS, "MaxOwnedSettlements"),
        "gate_slots": read_const(MIRROR_GATE_RULES_CS, "MaxGates"),
        "post_charge": read_const(POWER_RULES_CS, "PostDailyNeedCharge"),
        "annexe_water": read_const(ANNEXE_RULES_CS, "EnrolmentDrams"),
        "annexe_licenses": read_const(ANNEXE_RULES_CS, "EnrolmentLicenses"),
    }
    assert portfolio_caps == {"debit_lines": 64, "drive_steps": 128,
        "owned_settlements": 3, "gate_slots": 8, "post_charge": 4000,
        "annexe_water": 180, "annexe_licenses": 2}, (
            "purpose portfolio structural constants moved")
    assert "OpenChargePerDay = 3 * KingdomPowerRules.PostDailyNeedCharge" in gate_rules_text
    open_charge_per_day = 3 * portfolio_caps["post_charge"]

    kind_match = re.search(r"enum\s+KingdomPurposeKind\s*:\s*byte\s*\{(.*?)\}",
        kind_text, re.S)
    assert kind_match, "KingdomPurposeKind wire enum moved"
    kinds = {name: int(value) for name, value in re.findall(
        r"\b([A-Za-z]+)\s*=\s*([0-9]+)", kind_match.group(1))}
    assert kinds == {"None": 0, "Flesh": 1, "Chrome": 2, "Deep": 3,
        "Forge": 4, "Harvest": 5}, "purpose-kind wire values moved"

    recipe_pattern = re.compile(
        r"R\(KingdomPurposeKind\.(\w+),\s*KingdomPurposeKind\.(\w+),\s*"
        r'"([^"]+)",\s*"[^"]+",\s*([0-9]+),\s*([0-9]+),\s*'
        r'"([^"]*)",\s*KingdomMaterial\.(\w+),\s*([0-9]+),\s*([0-9]+)\)',
        re.S)
    recipes = tuple(PurposeRecipe(source, destination, cargo, int(water), int(food),
        _material_amounts(materials), embodied.lower(), int(units), int(carried_food))
        for source, destination, cargo, water, food, materials, embodied, units, carried_food
        in recipe_pattern.findall(catalogue_text))
    assert len(recipes) == 10, "purpose recipe catalogue is no longer ten directed rows"
    directions = {(row.source, row.destination) for row in recipes}
    assert len(directions) == len(recipes), "purpose recipe catalogue duplicates a direction"
    purpose_names = {name for name, value in kinds.items() if value > 0}
    assert {row.source for row in recipes} == purpose_names
    assert {row.destination for row in recipes} == purpose_names
    for row in recipes:
        assert row.source != row.destination, "a purpose became compatible with itself"
        assert (row.destination, row.source) in directions, (
            "purpose direction lost its reciprocal: " + row.source + ">" + row.destination)
        assert sum(1 for other in recipes if other.source == row.source) == 2, (
            row.source + " no longer has exactly two cycle neighbours")
        assert row.embodied_units == 1
        assert row.materials.get(row.embodied, 0) >= row.embodied_units, (
            row.cargo + " embodies material its local bill did not supply")
        assert row.carried_food <= row.food, (
            row.cargo + " carries more food than its local bill supplied")

    undirected = {tuple(sorted((row.source, row.destination), key=lambda name: kinds[name]))
        for row in recipes}
    assert len(undirected) == 5, "purpose graph is no longer one five-edge cycle"
    assert all(sum(name in edge for edge in undirected) == 2 for name in purpose_names), (
        "purpose graph no longer gives every work degree two")

    build_key_method = re.search(
        r"public static string BuildKey\(KingdomPurposeKind Kind\)\s*\{(.*?)\n\t\t\}",
        catalogue_text, re.S)
    assert build_key_method, "purpose BuildKey switch moved"
    build_keys = dict(re.findall(
        r"case\s+KingdomPurposeKind\.(\w+):\s*return\s+\"([^\"]+)\";",
        build_key_method.group(1)))
    assert set(build_keys) == purpose_names, "purpose build-key switch moved"
    buildings = _building_rows()
    for name, key in build_keys.items():
        row = buildings.get(key)
        assert row is not None, "purpose building is absent: " + key
        assert row.get("Purpose", "").lower() == name.lower(), (
            key + " no longer declares its source-pinned purpose")
        assert row.get("Megastructure") == "yes" and row.get("Capital", "") != "yes", (
            key + " no longer spends one ordinary-city purpose slot")
        assert row.get("Plot") == "XL" and row.get("MinStage") == "City", (
            key + " escaped its authored XL City ground")

    source_pins = (
        (catalogue_text,
            "return TryRecipe(A, B, out _) && TryRecipe(B, A, out _);",
            "purpose compatibility is no longer exact and reciprocal"),
        (pair_text, "public KingdomPurposeOperationReceipt Operation;",
            "pair receipt no longer owns one singular operation"),
        (pair_text, "public string CreditCargoId;",
            "pair receipt no longer owns one singular cargo credit"),
        (accounting_text,
            "Water = Operation.WaterRequested - Operation.WaterSpent - Operation.WaterLost;",
            "purpose water accounting moved"),
        (accounting_text,
            "Food = Operation.FoodRequested - Operation.FoodSpent - Operation.FoodLost;",
            "purpose food accounting moved"),
        (validation_text, "&& (!FullyDebited(Operation) || Operation.WaterLost != 0",
            "purpose effects no longer wait for full lossless local debit"),
        (factories_text,
            "WaterRequested = recipe.WaterDrams,",
            "purpose operation quantities no longer come straight from its recipe"),
        (factories_text,
            "FoodRequested = recipe.FoodServings, MaterialRequested = recipe.MaterialClaim,",
            "purpose operation food/material quantities moved away from its recipe"),
        (rules_family_text, "if (body != procedure",
            "body-sourced purpose operations no longer require exactly one body authority"),
        (annexe_text, "WaterCost = KingdomAnnexeRules.EnrolmentDrams,",
            "Chrome purpose surcharge moved away from the enrolment authority"),
        (annexe_text, "survey.StoredWater >= Authority.WaterCost + PortfolioWater",
            "Chrome preflight no longer composes body and portfolio water"),
        (lab_text, "WaterCost = procedure.Cost,",
            "Flesh purpose surcharge moved away from the selected procedure"),
        (lab_text, "survey.StoredWater < Authority.WaterCost + PortfolioWater",
            "Flesh preflight no longer composes body and portfolio water"),
        (transitions_text,
            "Before.Phase == KingdomPurposeOperationPhase.Dispatching\n"
            "\t\t\t\t\t&& After.Phase == KingdomPurposeOperationPhase.PickupComplete",
            "purpose route lost its explicit pickup checkpoint"),
        (transitions_text,
            "Before.Phase == KingdomPurposeOperationPhase.PickupComplete\n"
            "\t\t\t\t\t&& After.Phase == KingdomPurposeOperationPhase.LandingPending",
            "purpose route lost its explicit landing checkpoint"),
        (cardinality_text,
            "return string.Equals(Kept, Key, System.StringComparison.OrdinalIgnoreCase)\n"
            "\t\t\t\t? KingdomPurposeVerdict.Allowed\n"
            "\t\t\t\t: KingdomPurposeVerdict.RefusedKept;",
            "ordinary-city one-purpose cardinality moved"),
        (cardinality_text,
            "return Crowned ? KingdomPurposeVerdict.Allowed : KingdomPurposeVerdict.RefusedUncrowned;",
            "capital-only megastructures no longer fail closed on the crown"),
        (topology_text,
            "ActiveSettlementIds.Count >\n\t\t\t\t\tKingdomSettlementTopologyRules.MaxOwnedSettlements",
            "purpose topology no longer follows the realm settlement cap"),
        (registry_text,
            'PortfolioStateKey = "r_TAF_PurposePortfolioPair";',
            "purpose portfolio no longer has one singular durable register"),
        (drive_text,
            "if (!KingdomPurposePortfolioRules.OperationPhaseIsCommitted(operation.Phase)\n"
            "\t\t\t\t\t&& !KingdomMaster.NewWorkAllowed(System))",
            "purpose drive no longer distinguishes committed recovery from new work under pause"),
        (control_text, "if (!KingdomMaster.NewWorkAllowed(system))",
            "purpose preflight no longer refuses brand-new work under the master pause"),
        (gate_runtime_text, "if (Gate.Dark || destinationGate.Dark)",
            "purpose route no longer requires both physical gates to be powered"),
        (gate_runtime_text,
            "now >= destinationGate.LastDrawTick + KingdomRules.TicksPerDay)",
            "purpose route no longer requires a fresh draw at both gates"),
    )
    for source, needle, complaint in source_pins:
        assert needle in source, complaint
    assert "List<KingdomPurposeOperationReceipt>" not in pair_text

    print("  fixed graph cap: 5 symmetric pairs / 10 directed recipes")
    print(f"  {'pair':<22}{'water':>8}{'food':>7}  {'local materials':<50}retained cargo")
    total_water = total_food = total_carried_food = 0
    total_materials = {key: 0 for key in MATERIAL_KEYS}
    retained_materials = {key: 0 for key in MATERIAL_KEYS}
    for first, second in sorted(undirected, key=lambda edge: (kinds[edge[0]], kinds[edge[1]])):
        rows = [row for row in recipes
            if {row.source, row.destination} == {first, second}]
        assert len(rows) == 2
        water = sum(row.water for row in rows)
        food = sum(row.food for row in rows)
        material = {key: sum(row.materials.get(key, 0) for row in rows)
            for key in MATERIAL_KEYS}
        material = {key: amount for key, amount in material.items() if amount}
        retained = []
        for row in rows:
            retained_materials[row.embodied] += row.embodied_units
            total_carried_food += row.carried_food
            retained.append(row.cargo + "=" + row.embodied + ":"
                + str(row.embodied_units) + ("+food:" + str(row.carried_food)
                    if row.carried_food else ""))
        total_water += water
        total_food += food
        for key, amount in material.items():
            total_materials[key] += amount
        material_text = ",".join(key + ":" + str(amount)
            for key, amount in material.items())
        print(f"  {(first + '<->' + second):<22}{water:>8}{food:>7}  "
            f"{material_text:<50}{'; '.join(retained)}")

    material_total_text = ",".join(key + ":" + str(total_materials[key])
        for key in MATERIAL_KEYS if total_materials[key])
    retained_text = ",".join(key + ":" + str(retained_materials[key])
        for key in MATERIAL_KEYS if retained_materials[key])
    sink_text = ",".join(key + ":"
        + str(total_materials[key] - retained_materials[key])
        for key in MATERIAL_KEYS if total_materials[key] - retained_materials[key])
    assert (total_water, total_food, total_carried_food) == (134, 24, 12)
    assert all(total_materials[key] >= retained_materials[key] for key in MATERIAL_KEYS)
    print(f"  ten-direction local bill: water={total_water}, food={total_food}, "
        f"materials={material_total_text}")
    print(f"  operation-boundary cargo: carried-food metadata={total_carried_food}, "
        f"embodied materials={retained_text}")
    print(f"  local claim less dispatched material objects: food remains {total_food}, "
        f"materials={sink_text}")

    production_roots = ("Api", "Core", "Experience", "Growth", "Polity",
        "Simulation", "Trade", "World")

    def source_hits(token: str) -> set[str]:
        hits = set()
        for relative in production_roots:
            directory = os.path.join(ROOT, relative)
            if not os.path.isdir(directory):
                continue
            for base, _dirs, names in os.walk(directory):
                for filename in sorted(name for name in names if name.endswith(".cs")):
                    path = os.path.join(base, filename)
                    if token in read_source(path):
                        hits.add(os.path.relpath(path, ROOT))
        return hits

    carried_food_sites = source_hits("CarriedFood")
    assert carried_food_sites == {
        "Growth/KingdomPurposeCargoReceipt.cs",
        "Growth/KingdomPurposePortfolio.ConstructionCargo.cs",
        "Growth/KingdomPurposePortfolio.LandingFood.cs",
        "Growth/KingdomPurposePortfolio.Open.cs",
        "Growth/KingdomPurposePortfolio.OperationControl.cs",
        "Growth/KingdomPurposePortfolio.OperationDrive.cs",
        "Growth/KingdomPurposePortfolio.OutputRuntime.cs",
        "Growth/KingdomPurposePortfolioRecipe.cs",
        "Growth/KingdomPurposePortfolioRules.Catalogue.cs",
        "Growth/KingdomPurposePortfolioRules.Codec.cs",
        "Growth/KingdomPurposePortfolioRules.Factories.cs",
        "Growth/KingdomPurposePortfolioRules.LandingAttempt.cs",
        "Growth/KingdomPurposePortfolioRules.LandingFood.cs",
        "Growth/KingdomPurposePortfolioRules.Validation.cs",
    }, "purpose CarriedFood reader set drifted; re-audit the landing lane before trusting food return"
    assert source_hits("r_TAF_PurposePairCargoFood") == {
        "Core/KingdomRemovalCoverage.Generated.cs",
        "Growth/KingdomPurposePortfolio.OperationControl.cs",
    }, "purpose cargo food metadata acquired a runtime consumer"
    assert "PortfolioCargoFoodProperty" in output_text
    carrying_rows = tuple(row for row in recipes if row.carried_food > 0)
    local_food_rows = tuple(row for row in recipes if row.carried_food == 0 and row.food > 0)
    carriage_debit = sum(row.food for row in carrying_rows)
    carriage_landed = sum(row.carried_food for row in carrying_rows)
    carriage_loss = sum(row.food - row.carried_food for row in carrying_rows)
    local_food_sink = sum(row.food for row in local_food_rows)
    assert (carriage_debit, carriage_landed, carriage_loss, local_food_sink) == (16, 12, 4, 8)
    assert total_food == carriage_landed + carriage_loss + local_food_sink == 24
    assert all(row.source == "Harvest" and (row.food, row.carried_food) == (8, 6)
        for row in carrying_rows)
    print(f"  food conservation: {total_food} debited = {carriage_landed} landed + "
        f"{carriage_loss} carriage loss + {local_food_sink} local process sink")
    print("  active Harvest edge: 8 debited = 6 landed + 2 carriage loss; "
        "landed food becomes physical servings via the purpose-food landing lane")

    effect_units = {
        "raw": read_const(PURPOSE_EFFECT_RULES_CS, "PurposeEffectRawUnits"),
        "refined": read_const(PURPOSE_EFFECT_RULES_CS, "PurposeEffectRefinedUnits"),
        "crops": read_const(PURPOSE_EFFECT_RULES_CS, "PurposeEffectCropUnits"),
        "seeds": read_const(PURPOSE_EFFECT_RULES_CS, "PurposeEffectSeedUnits"),
        "milled_crops": read_const(PURPOSE_EFFECT_RULES_CS, "PurposeEffectMilledCrops"),
        "staples_per_crop": read_const(
            PURPOSE_EFFECT_RULES_CS, "PurposeEffectStaplesPerCrop"),
    }
    assert effect_units == {"raw": 2, "refined": 1, "crops": 3, "seeds": 1,
        "milled_crops": 2, "staples_per_crop": 3}
    assert "KingdomMaterial.Stone" in effect_text and "KingdomMaterial.ShapedStone" in effect_text
    assert "KingdomMaterial.Scrap" in effect_text and "KingdomMaterial.WorkedMetal" in effect_text
    refine_rows = tuple(row for row in recipes if row.source in {"Deep", "Forge"})
    raw_debited = len(refine_rows) * effect_units["raw"]
    refined_credited = len(refine_rows) * effect_units["refined"]
    assert len(refine_rows) == 4 and raw_debited == refined_credited + 4 == 8
    assert sum(row.source == "Deep" for row in refine_rows) * effect_units["raw"] == 4
    assert sum(row.source == "Forge" for row in refine_rows) * effect_units["raw"] == 4
    print("  bounded refine effects: 8 raw debited = 4 refined credited + 4 process loss")

    harvest_rows = tuple(row for row in recipes if row.source == "Harvest")
    crops_debited = len(harvest_rows) * effect_units["crops"]
    seeds_credited = len(harvest_rows) * effect_units["seeds"]
    crops_ground = len(harvest_rows) * effect_units["milled_crops"]
    measures_credited = crops_ground * effect_units["staples_per_crop"]
    assert len(harvest_rows) == 2 and (crops_debited, seeds_credited,
        crops_ground, measures_credited) == (6, 2, 4, 12)
    assert measures_credited - crops_debited == 6
    print("  bounded Harvest effects: 6 crops = 2 seed corn + 4 ground -> "
        "12 preserved measures; net +6 servings and +2 seeds")

    procedure_root = ET.parse(PROCEDURES_XML).getroot()
    procedure_costs = sorted(int(row.get("Cost", "-1"))
        for row in procedure_root.findall("procedure"))
    assert len(procedure_costs) == 16 and procedure_costs[0] == 20
    assert procedure_costs[-1] == portfolio_caps["annexe_water"] == 180

    def body_surcharge(source_kind: str, pick: str) -> int:
        if source_kind == "Chrome":
            return portfolio_caps["annexe_water"]
        if source_kind == "Flesh":
            return procedure_costs[0] if pick == "cheapest" else procedure_costs[-1]
        return 0

    def operation_cost(row: PurposeRecipe, pick: str) -> tuple[int, int, int]:
        return (row.water + body_surcharge(row.source, pick), row.food,
            sum(row.materials.values()))

    def preactive_cost(first: str, second: str, pick: str) -> tuple[int, int, int]:
        outward = next(row for row in recipes
            if row.source == first and row.destination == second)
        returned = next(row for row in recipes
            if row.source == second and row.destination == first)
        legs = (operation_cost(outward, pick), operation_cost(returned, pick),
            operation_cost(outward, pick))
        return tuple(sum(row[index] for row in legs) for index in range(3))

    def round_trip_cost(first: str, second: str, pick: str) -> tuple[int, int, int]:
        legs = [operation_cost(row, pick) for row in recipes
            if {row.source, row.destination} == {first, second}]
        assert len(legs) == 2
        return tuple(sum(row[index] for row in legs) for index in range(3))

    catalogue_by_key = {design.key: design for design in CATALOGUE}
    print("  bootstrap -> reciprocal return -> activation (construction is a Q4 lookup):")
    print(f"  {'orientation':<20}{'build':>8}{'days':>7}{'recipe':>9}"
        f"{'cheap+body':>13}{'dear+body':>12}{'steady water':>15}")
    for first, second in sorted(directions, key=lambda edge: (kinds[edge[0]], kinds[edge[1]])):
        first_row = buildings[build_keys[first]]
        second_row = buildings[build_keys[second]]
        first_design = catalogue_by_key[build_keys[first]]
        second_design = catalogue_by_key[build_keys[second]]
        assert int(first_row["Cost"]) == first_design.cost
        assert int(second_row["Cost"]) == second_design.cost
        build_water = first_design.cost + second_design.cost
        build_days = (int(first_row["Ticks"]) + int(second_row["Ticks"])) // SRC["TicksPerDay"]
        recipe_only = (2 * next(row.water for row in recipes
                if row.source == first and row.destination == second)
            + next(row.water for row in recipes
                if row.source == second and row.destination == first))
        cheap = preactive_cost(first, second, "cheapest")[0]
        dear = preactive_cost(first, second, "dearest")[0]
        steady_cheap = round_trip_cost(first, second, "cheapest")[0]
        steady_dear = round_trip_cost(first, second, "dearest")[0]
        steady = str(steady_cheap) if steady_cheap == steady_dear \
            else str(steady_cheap) + ".." + str(steady_dear)
        assert recipe_only <= cheap <= dear
        print(f"  {(first + '->' + second):<20}{build_water:>8}{build_days:>7}"
            f"{recipe_only:>9}{cheap:>13}{dear:>12}{steady:>15}")

    repayment_ratios = []
    for row in recipes:
        local_units = sum(row.materials.values())
        assert row.embodied_units == 1 and row.embodied_units <= local_units
        repayment_ratios.append((row.source + "->" + row.destination,
            local_units, row.embodied, row.embodied_units))
    assert sum(units for _direction, _local, _kind, units in repayment_ratios) == len(recipes)
    print("  bootstrap repayment: every orientation funds exactly one embodied material unit;")
    print("  no row repays water, food, body surcharge, or more than its own local material claim")

    # Cap and cap+1: Recipes is a fixed lookup, not an extensible runtime list. All ten unique
    # directed rows resolve; an eleventh row can only duplicate one or occupy a forbidden edge.
    ordered_nonself = {(a, b) for a in purpose_names for b in purpose_names if a != b}
    forbidden = ordered_nonself - directions
    assert len(forbidden) == 10
    assert all((b, a) in forbidden for a, b in forbidden)
    cap_plus_one = recipes[0]
    assert (cap_plus_one.source, cap_plus_one.destination) in directions
    assert len(directions | {(cap_plus_one.source, cap_plus_one.destination)}) == 10
    assert all((name, name) not in directions for name in purpose_names)
    print("  cap case: all 10 unique directions resolve; cap+1 duplicate/non-edge/self refuses")

    live_edge_cap = min(portfolio_caps["owned_settlements"] // 2, 1)
    assert live_edge_cap == 1
    assert registry_text.count(
        'PortfolioStateKey = "r_TAF_PurposePortfolioPair"') == 1
    noncapital_megastructures = {key for key, row in buildings.items()
        if row.get("Megastructure") == "yes" and row.get("Capital") != "yes"}
    assert noncapital_megastructures == set(build_keys.values())
    assert portfolio_caps["owned_settlements"] >= 2
    assert portfolio_caps["owned_settlements"] < 2 * (live_edge_cap + 1)
    print(f"  live-edge cap: min({portfolio_caps['owned_settlements']} cities // 2, "
        f"one pair register) = {live_edge_cap}; edge {live_edge_cap + 1} refuses")

    normal_steps = 12 + portfolio_caps["debit_lines"]
    exempt_steps = 10 + portfolio_caps["debit_lines"]
    assert normal_steps == 76 and exempt_steps == 74
    assert normal_steps < portfolio_caps["drive_steps"]
    assert exempt_steps < portfolio_caps["drive_steps"]
    assert drive_text.count("KingdomMaster.NewWorkAllowed") == 1
    assert all(token not in read_source(
        source_family_paths("Growth", "KingdomPurposePortfolio"))
        for token in ("TimeTicks", "ElapsedDays", "AdvanceCheckpoint"))
    route_charge = 2 * open_charge_per_day
    assert route_charge == 24_000
    print(f"  drive bound: normal {normal_steps}, exempt {exempt_steps} < "
        f"slice {portfolio_caps['drive_steps']}; headroom "
        f"{portfolio_caps['drive_steps'] - normal_steps}")
    print(f"  route standing demand: 2 gates x {open_charge_per_day} = "
        f"{route_charge} charge/day (W7 owns supply/conservation)")

    # Boundary/sensitivity: full local debit has zero outstanding. A one-unit shortfall in every
    # nonzero scalar/material dimension remains outstanding, so no effect/output checkpoint opens.
    sensitivity_checks = 0
    for row in recipes:
        for requested in (row.water, row.food):
            if requested > 0:
                assert requested - requested - 0 == 0
                assert requested - (requested - 1) - 0 == 1
                sensitivity_checks += 1
        for amount in row.materials.values():
            assert amount - amount - 0 == 0
            assert amount - (amount - 1) - 0 == 1
            sensitivity_checks += 1
    assert sensitivity_checks == 31
    assert transitions_text.count("AdoptOnce(") == 11
    assert "After.WaterSpent < Before.WaterSpent" in transitions_text
    assert "After.FoodSpent < Before.FoodSpent" in transitions_text
    assert "!ClaimMonotone(Before.MaterialSpent, After.MaterialSpent)" in transitions_text
    assert all(body_surcharge("Chrome", pick) == portfolio_caps["annexe_water"]
        for pick in ("cheapest", "dearest"))
    assert all(body_surcharge("Flesh", pick) > 0
        for pick in ("cheapest", "dearest"))
    assert next(row for row in recipes
        if row.source == "Chrome" and row.destination == "Flesh").water \
        + body_surcharge("Chrome", "cheapest") > portfolio_caps["annexe_water"]
    phases = ("Prepared", "InputDebitPending", "InputDebited", "LocalDebitPending",
        "LocalDebited", "EffectPending", "EffectApplied", "OutputPending", "Dispatching",
        "PickupComplete", "LandingPending", "Delivered")
    assert all("KingdomPurposeOperationPhase." + phase in transitions_text
        or "KingdomPurposeOperationPhase." + phase in validation_text for phase in phases)
    print("  sensitivity: 31 scalar/material -1 boundaries remain outstanding before effect")
    print("  interruption: one parent -> one operation -> one cargo; explicit pickup and landing")
    print("  conservation: requested = spent + lost + outstanding; active path requires lost = 0")
    print("  W6/W7/G1/G2/C4 accounting reuse: yes; no Carries, rate, or save bytes added here")
    print("  native ten-direction/save/interruption/appearance evidence: UNSIGNED")


def q14_hosted_arcology():
    """Structural closure for one hosted capital shell and its bounded composite lots.

    Existing catalogue sections already count each declared building once. This section composes
    their exact bills and conditionally exposes receipt-owned supports; it does not add a second
    city-production or equilibrium pass.
    """
    rule("Q14 Hosted arcology: one shell, bounded lots, physical water-gated food")
    rules_text = read_source(HOSTED_RULES_CS)
    runtime_text = read_source(HOSTED_RUNTIME_CS)
    construction_text = read_source(HOSTED_CONSTRUCTION_CS)
    authority_text = read_source(HOSTED_AUTHORITY_CS)
    lot_text = read_source(HOSTED_LOT_CS)
    visual_text = read_source(HOSTED_VISUAL_CS)
    archive_text = read_source(GREAT_ARCHIVE_CS)
    subsidence_text = read_source(SUBIMPL_CS)
    folding_text = read_source(CAT_CS)

    max_lots = read_const(HOSTED_RULES_CS, "MaxHostedLots")
    catchup = read_const(HOSTED_RULES_CS, "MaxLaborCatchupTicks")
    assert (max_lots, catchup) == (16, 36_000), "hosted capacity/labour pins moved"
    slot_keys = re.findall(r'"r_TAF_HostedArcologyAuthorityV1:[0-9]+"', authority_text)
    assert slot_keys == ['"r_TAF_HostedArcologyAuthorityV1:0"',
        '"r_TAF_HostedArcologyAuthorityV1:1"'], "hosted authority fixed slots moved"

    built_in_blocks = re.findall(
        r"RegisterHostedLot\(new\s+KingdomHostedLotDefinition\s*\{(.*?)\}\s*,\s*out ignored\);",
        rules_text, re.S)
    assert len(built_in_blocks) == 2, "hosted paid-lot built-ins moved"

    def string_field(block: str, name: str, required: bool = True) -> str:
        match = re.search(r"\b" + name + r'\s*=\s*"([^"]*)"', block)
        assert match or not required, "hosted lot lost field " + name
        return match.group(1) if match else ""

    def int_field(block: str, name: str, required: bool = True) -> int:
        match = re.search(r"\b" + name + r"\s*=\s*([0-9]+)(?:L)?", block)
        assert match or not required, "hosted lot lost numeric field " + name
        return int(match.group(1)) if match else 0

    paid_lots = {}
    for block in built_in_blocks:
        key = string_field(block, "Key")
        assert key not in paid_lots
        paid_lots[key] = {
            "cell": string_field(block, "InteriorCell"),
            "material": string_field(block, "MaterialKey"),
            "ticks": int_field(block, "BuildTicks"),
            "crew": int_field(block, "Crew"),
            "supports": string_field(block, "Supports"),
            "water": bool(re.search(r"\bRequiresWater\s*=\s*true", block)),
            "producer": string_field(block, "PhysicalProducerBlueprint", False),
            "producers": int_field(block, "PhysicalProducerCount", False),
        }
    assert set(paid_lots) == {"arcologyward", "arcologyterrace"}
    assert paid_lots["arcologyward"] == {
        "cell": "TAFArcologyWard", "material": "arcologyward", "ticks": 9600,
        "crew": 2, "supports": "roof:26,luxury:2", "water": False,
        "producer": "", "producers": 0}
    assert paid_lots["arcologyterrace"] == {
        "cell": "TAFArcologyTerrace", "material": "arcologyterrace", "ticks": 7200,
        "crew": 2, "supports": "food:14", "water": True,
        "producer": "r_KingdomArcologyGrowbed", "producers": 14}

    source_pins = (
        (rules_text, "if (Lots.Count >= MaxHostedLots)",
            "hosted registry no longer refuses before cap+1"),
        (rules_text, "if (Carries(D.Supports, \"food\") && !hasProducer)",
            "hosted food no longer requires a physical producer"),
        (runtime_text, "if (!Operational(Work)) return answer;",
            "inoperative shell can now expose hosted supports"),
        (runtime_text,
            "|| (receipt.RequiresWater && !FreshWaterAvailable)) continue;",
            "hosted terrace no longer closes without physical fresh water"),
        (runtime_text, "&& receipt.Phase == KingdomHostedLotPhase.Working",
            "hosted construction staffing no longer follows the working receipt"),
        (construction_text,
            "KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);",
            "hosted lot no longer reserves its exact XML water bill"),
        (construction_text,
            "KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, cost);",
            "hosted lot no longer reserves its exact composite material bill"),
        (construction_text,
            "KingdomConstruction.HasActiveSubject(System, Z,\n"
            "\t\t\t\t\tKingdomConstructionRoute.HostedArcology, shell)",
            "hosted shell no longer limits paid construction to one live lot"),
        (construction_text,
            "Receipt.LastTick, System.MasterOptionTick, now, Receipt.StaffingBasis,",
            "hosted labour no longer clamps its receipt clock to the master edge"),
        (rules_text,
            "return AdvanceLabor(Remaining, Math.Max(LastTick, MasterOptionTick),\n"
            "\t\t\t\tNowTick, PriorEffectiveness, out NextTick);",
            "hosted labour no longer excludes time before the latest master edge"),
        (authority_text, "KingdomCrown.CrownedOn(System, ZoneId)",
            "hosted authority no longer requires exact capital ground"),
        (authority_text,
            '"This realm already has a hosted arcology authority; its shell is not duplicated."',
            "hosted authority no longer rejects a second current-realm shell"),
        (archive_text, "InteriorCell = \"TAFGreatArchive\", ReadOnly = true,",
            "Great Archive no longer registers as a read-only hosted view"),
        (subsidence_text, "Survey.StoredWater > 0",
            "hosted fresh-water gate no longer reads physical stored water"),
        (folding_text,
            "return (EffectivenessPercent >= 100) ? Amount : (Amount * EffectivenessPercent / 100);",
            "hosted support effectiveness no longer follows catalogue carried arithmetic"),
    )
    for source, needle, complaint in source_pins:
        assert needle in source, complaint
    assert "List<string> LotReceipts" in read_source(
        os.path.join(ROOT, "Growth", "r_KingdomArcology.cs"))
    assert "Dormant = 0" in lot_text and "Working = 1" in lot_text
    assert "Active = 2" in lot_text and "Quarantined = 3" in lot_text
    assert construction_text.count("ReserveExactWater(entry.CostDrams)") == 1
    assert construction_text.count("KingdomMaterials.ReserveComposite(Z, cost)") == 1
    assert "ReserveExactWater" not in runtime_text and "ReserveComposite" not in runtime_text

    buildings = _building_rows()
    keys = ("arcology", "arcologyward", "arcologyterrace")
    assert all(key in buildings for key in keys)
    shell = buildings["arcology"]
    assert shell.get("Capital") == "yes" and shell.get("Megastructure") == "yes"
    assert shell.get("UpgradesTo", "") == ""
    for key, lot in paid_lots.items():
        row = buildings[key]
        assert row.get("Strata") == "arcology" and row.get("Capital") == "yes"
        assert int(row["Ticks"]) == lot["ticks"] and row["Carries"] == lot["supports"]
        assert row.get("HostedProducerBlueprint", "") == lot["producer"]
        assert int(row.get("HostedProducerCount", "0")) == lot["producers"]

    # The shell's catalogue price is already owned by Q4. Q14 prices only the two reachable
    # hosted commissions inside it, once each, from the XML rows BeginLot consumes.
    composite_materials = {key: 0 for key in MATERIAL_KEYS}
    composite_bits: dict[str, int] = {}
    composite_exotics: dict[str, int] = {}
    total_water = total_ticks = 0
    for key in paid_lots:
        row = buildings[key]
        total_water += int(row["Cost"])
        total_ticks += int(row["Ticks"])
        for material, amount in _material_amounts(row.get("Materials", "")).items():
            composite_materials[material] += amount
        for tier in row.get("Bits", ""):
            assert tier.isdigit()
            composite_bits[tier] = composite_bits.get(tier, 0) + 1
        for item in row.get("Exotics", "").split(","):
            if not item:
                continue
            exotic, amount = item.split(":")
            assert amount.isdigit() and int(amount) > 0
            composite_exotics[exotic] = composite_exotics.get(exotic, 0) + int(amount)
    assert (total_water, total_ticks) == (116, 16_800)
    assert {key: value for key, value in composite_materials.items() if value} == {
        "stone": 74, "mud": 8, "shapedstone": 26, "workedmetal": 20}
    assert composite_bits == {"0": 2, "3": 1, "4": 1}
    assert composite_exotics == {}
    print("  composite paid interior: ward + terrace (shell remains Q4-owned)")
    print(f"  {'lot':<20}{'water':>8}{'materials':>12}{'bits':>7}{'exotics':>10}"
        f"{'ticks':>9}{'crew':>7}")
    for key, lot in paid_lots.items():
        row = buildings[key]
        material_units = sum(_material_amounts(row.get("Materials", "")).values())
        bit_units = sum(1 for token in row.get("Bits", "") if token.isdigit())
        exotic_units = sum(int(item.split(":")[1])
            for item in row.get("Exotics", "").split(",") if item)
        print(f"  {key:<20}{int(row['Cost']):>8}{material_units:>12}{bit_units:>7}"
            f"{exotic_units:>10}{lot['ticks']:>9}{lot['crew']:>7}")
    print(f"  exact bill: water={total_water}; build ticks={total_ticks} "
        f"({total_ticks / SRC['TicksPerDay']:.1f} days summed, not a concurrency claim)")
    print("  materials: " + ",".join(key + ":" + str(composite_materials[key])
        for key in MATERIAL_KEYS if composite_materials[key]))
    print("  bits: " + ",".join("tier" + key + ":" + str(composite_bits[key])
        for key in sorted(composite_bits)) + "; exotics: "
        + (",".join(key + ":" + str(composite_exotics[key])
            for key in sorted(composite_exotics)) or "none"))

    def support_tally(raw: str) -> dict[str, int]:
        answer: dict[str, int] = {}
        for item in raw.split(",") if raw else ():
            kind, amount = item.split(":")
            assert amount.isdigit() and kind not in answer
            answer[kind] = int(amount)
        return answer

    def hosted_supports(operational: bool, active_lots: set[str], fresh_water: bool,
            effectiveness: int = 100):
        if not operational:
            return {"roof": 0, "food": 0, "order": 0, "luxury": 0}
        answer = support_tally(shell["Carries"])
        for key, lot in paid_lots.items():
            if key not in active_lots or (lot["water"] and not fresh_water):
                continue
            for kind, amount in support_tally(lot["supports"]).items():
                answer[kind] = answer.get(kind, 0) + amount
        return {kind: carried(answer.get(kind, 0), effectiveness)
            for kind in ("roof", "food", "order", "luxury")}

    support_cases = (
        ("dark shell", False, set(), False, (0, 0, 0, 0)),
        ("bare shell", True, set(), False, (60, 0, 4, 4)),
        ("ward active", True, {"arcologyward"}, False, (86, 0, 4, 6)),
        ("both, dry", True, set(paid_lots), False, (86, 0, 4, 6)),
        ("both, fresh", True, set(paid_lots), True, (86, 14, 4, 6)),
    )
    print(f"  {'state':<14}{'roof':>7}{'food':>7}{'order':>8}{'luxury':>9}")
    for label, operational, active, fresh, expected in support_cases:
        got = hosted_supports(operational, active, fresh)
        values = tuple(got[key] for key in ("roof", "food", "order", "luxury"))
        assert values == expected
        print(f"  {label:<14}{values[0]:>7}{values[1]:>7}{values[2]:>8}{values[3]:>9}")

    effectiveness_points = tuple(sorted({0, 100 - SRC["MaxWearPercent"],
        SRC["MaxWearPercent"], 100}))
    print("  both paid lots active; exact shell-effectiveness sensitivity:")
    print(f"  {'effectiveness':<15}{'dry food':>10}{'fresh roof':>12}"
        f"{'fresh food':>12}{'order':>8}{'luxury':>10}")
    for effectiveness in effectiveness_points:
        dry = hosted_supports(True, set(paid_lots), False, effectiveness)
        fresh = hosted_supports(True, set(paid_lots), True, effectiveness)
        assert dry["food"] == 0 and fresh["food"] <= 14
        assert hosted_supports(False, set(paid_lots), True, effectiveness) == {
            "roof": 0, "food": 0, "order": 0, "luxury": 0}
        print(f"  {effectiveness:<15}{dry['food']:>10}{fresh['roof']:>12}"
            f"{fresh['food']:>12}{fresh['order']:>8}{fresh['luxury']:>10}")
    condition_floor = 100 - SRC["MaxWearPercent"]
    assert hosted_supports(True, set(paid_lots), True, condition_floor)["food"] \
        == 14 * condition_floor // 100

    terrace = paid_lots["arcologyterrace"]
    hosted_rows = _hosted_crop_rows()
    physical_rows = terrace["producers"] * hosted_rows[terrace["producer"]]
    physical_food = physical_rows * SRC["YieldPerRow"] // SRC["CropDays"]
    fixture_count = visual_text.count('"r_KingdomArcologyGrowbed"')
    assert fixture_count == terrace["producers"]
    assert int(buildings["arcologyterrace"]["HostedProducerCount"]) == fixture_count
    assert physical_rows == 28 and physical_food == 14
    assert physical_rows * SRC["YieldPerRow"] == 14 * SRC["CropDays"]
    print(f"  terrace proof: {terrace['producers']} growbeds x "
        f"{hosted_rows[terrace['producer']]} rows = {physical_rows} rows -> food:{physical_food}")
    print("  water boundary: StoredWater=0 -> food:0; StoredWater>0 -> food:14")

    # Registration cap/cap+1. Current loader registers two paid rows and one read-only view.
    # Fill only a local integer model; never mutate the process-global C# registry from this tool.
    current_registered = len(paid_lots) + 1
    assert current_registered == 3 and current_registered <= max_lots
    occupancy = current_registered
    admitted = 0
    while occupancy < max_lots:
        occupancy += 1
        admitted += 1
    assert occupancy == max_lots and admitted == 13
    cap_plus_one_admitted = occupancy < max_lots
    assert not cap_plus_one_admitted
    print(f"  registry cap: 2 paid + 1 read-only + {admitted} extension slots = "
        f"{max_lots}; slot {max_lots + 1} refuses before publication")
    print("  authority cap: one exact current-realm shell; second fixed slot protects retained history")

    def advance_labor(remaining: int, last: int, now: int, effectiveness: int) -> int:
        if remaining <= 0:
            return 0
        if last <= 0 or now <= last:
            return remaining
        elapsed = min(now - last, catchup)
        effective = max(0, min(100, effectiveness))
        spent = elapsed * effective // 100
        return 0 if spent >= remaining else remaining - spent

    def advance_labor_after_master_edge(remaining: int, last: int, master_edge: int,
            now: int, effectiveness: int) -> int:
        return advance_labor(remaining, max(last, master_edge), now, effectiveness)

    ward_ticks = paid_lots["arcologyward"]["ticks"]
    clock_origin = SRC["TicksPerDay"]
    assert advance_labor(ward_ticks, clock_origin,
        clock_origin + ward_ticks, 100) == 0
    halfway = advance_labor(ward_ticks, clock_origin,
        clock_origin + ward_ticks // 2, 100)
    assert halfway == ward_ticks // 2
    assert advance_labor(halfway, clock_origin + ward_ticks // 2,
        clock_origin + ward_ticks, 100) == 0
    assert advance_labor(ward_ticks, clock_origin,
        clock_origin + catchup * 2, 0) == ward_ticks
    assert advance_labor(ward_ticks, clock_origin + SRC["TicksPerDay"],
        clock_origin, 100) == ward_ticks
    max_lot_ticks_match = re.search(r"D\.BuildTicks > ([0-9]+)L", rules_text)
    assert max_lot_ticks_match
    max_lot_ticks = int(max_lot_ticks_match.group(1))
    assert advance_labor(max_lot_ticks, clock_origin,
        clock_origin + catchup * 2, 100) == max_lot_ticks - catchup
    pause_edge = clock_origin + ward_ticks // 2
    assert advance_labor_after_master_edge(ward_ticks, clock_origin, pause_edge,
        pause_edge, 100) == ward_ticks
    assert advance_labor_after_master_edge(ward_ticks, clock_origin, pause_edge,
        pause_edge + ward_ticks // 2, 100) == ward_ticks // 2
    assert advance_labor_after_master_edge(ward_ticks, clock_origin, pause_edge,
        pause_edge + ward_ticks, 100) == 0
    assert advance_labor_after_master_edge(ward_ticks, clock_origin, 0,
        clock_origin + ward_ticks, 100) == 0
    need_literals = tuple(int(value) for value in re.findall(
        r"\bneed\s*=\s*([0-9]+);", runtime_text))
    assert need_literals == (0, 4)
    base_need = need_literals[-1]
    assert base_need + max(lot["crew"] for lot in paid_lots.values()) == 6
    assert all(lot["crew"] == 2 for lot in paid_lots.values())

    def passes_to_finish(build_ticks: int, effectiveness: int, elapsed: int) -> int | None:
        per_staffed_pass = min(elapsed, catchup) * effectiveness // 100
        if per_staffed_pass <= 0:
            return None
        # BeginLot freezes StaffingBasis=0: first observation advances no work.
        return 1 + math.ceil(build_ticks / per_staffed_pass)

    assert passes_to_finish(ward_ticks, 0, catchup) is None
    assert passes_to_finish(ward_ticks, 100, ward_ticks) == 2
    assert passes_to_finish(ward_ticks, condition_floor, catchup) == 2
    assert passes_to_finish(ward_ticks, 100, catchup) \
        == passes_to_finish(ward_ticks, 100, catchup * 2)
    assert min(catchup, catchup * 2) * condition_floor // 100 >= ward_ticks
    print(f"  staffing: shell {base_need}; one Working lot adds 2 -> transient peak 6; "
        "Active lots add 0")
    print(f"  interruption: prior staffing x elapsed; catch-up <= {catchup} ticks; "
        "first pass/zero crew/time regression = zero work")
    print("  master pause: physical labour freezes; MasterOptionTick discards pre-resume "
        "elapsed time, so paused labour is never repaid")
    print("  conservation: every paid lot reserves XML Cost + materials once; only Active adds support")
    print("  W6/W7/G1/G2/C4 accounting reuse: yes; hosted food is not a second crop or save budget")
    print("  native traversal/save/cardinality/water/appearance evidence: UNSIGNED")


def q15_routed_construction_inputs():
    """Structural closure for physical cross-zone construction funding.

    This models receipt shape, itinerary/custody checkpoints, exact reserve arithmetic, and
    conservation. It never credits city production, spends a live object, or claims native carrier
    evidence. W6/W7/G1/G2 and C4 remain the sole owners of their existing totals.
    """
    rule("Q15 Routed construction inputs: itinerary, custody, debit, rollback, recovery")
    rules_text = read_source(CONSTRUCTION_INPUT_RULES_CS)
    declarations_text = read_source(CONSTRUCTION_INPUT_DECLARATIONS_CS)
    plan_text = read_source(CONSTRUCTION_INPUT_PLAN_CS)
    plan_validation_text = read_source(CONSTRUCTION_INPUT_PLAN_VALIDATION_CS)
    state_text = read_source(CONSTRUCTION_INPUT_STATE_CS)
    transition_text = read_source(CONSTRUCTION_INPUT_TRANSITIONS_CS)
    recovery_text = read_source(CONSTRUCTION_INPUT_RECOVERY_CS)
    commit_text = read_source(CONSTRUCTION_INPUT_COMMIT_CS)
    drive_text = read_source(CONSTRUCTION_INPUT_DRIVE_CS)
    drive_family_text = read_source(
        source_family_paths("Growth", "KingdomConstruction.InputDrive"))
    cancellation_text = drive_family_text
    central_text = read_source(CENTRAL_CONSTRUCTION_RESERVATION_CS)
    logistics_text = read_source(LOGISTICS_RULES_CS)
    itinerary_text = read_source(ITINERARY_RULES_CS)
    global_recovery_text = read_source(CONSTRUCTION_INPUT_GLOBAL_RECOVERY_CS)
    route_text = read_source(CENTRAL_ROUTE_CS)
    leg_text = read_source(CENTRAL_LEGS_CS)

    cap = {
        "schema": read_const(CONSTRUCTION_INPUT_RULES_CS, "Schema"),
        "legacy_schema": read_const(CONSTRUCTION_INPUT_RULES_CS, "LegacySchema"),
        "sources": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxSourceLines"),
        "cargo": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxCargoLines"),
        "children": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxChildren"),
        "required": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxRequiredObjects"),
        "per_child": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxCargoPerChild"),
        "water_cask": read_const(CONSTRUCTION_INPUT_RULES_CS, "WaterCargoCapacity"),
        "reserve_days": read_const(CONSTRUCTION_INPUT_RULES_CS, "WaterReserveDays"),
        "scanned": read_const(CONSTRUCTION_INPUT_PLAN_VALIDATION_CS, "MaxScannedCandidates"),
        "carrier": read_const(LOGISTICS_RULES_CS, "CarrierCapacity"),
        "legs": read_const(CITY_MEMORY_RULES_CS, "MaxLegs"),
        "drive_steps": read_const(CONSTRUCTION_INPUT_DRIVE_CS,
            "MaxRoutedInputStepsPerPass"),
        "receipts_per_turn": read_const(CONSTRUCTION_INPUT_GLOBAL_RECOVERY_CS,
            "MaxGlobalInputReceiptsPerTurn"),
        "open_jobs": read_const(CITY_MEMORY_RULES_CS, "MaxOpenJobs"),
        "load_per_trip": read_const(PORTERS_OPENING_CS, "LoadPerTrip"),
        "zone_transit": read_const(DISTANCE_RULES_CS, "ZoneTransitCells"),
        "max_nodes": read_const(DISTANCE_RULES_CS, "MaxNodes"),
        "zone_width": read_const(JOB_DRAWS_CS, "ZoneWidth"),
        "zone_height": read_const(JOB_DRAWS_CS, "ZoneHeight"),
        "sinuosity_open": read_const(ITINERARY_RULES_CS, "SinuosityOpenPercent"),
        "sinuosity_built": read_const(ITINERARY_RULES_CS, "SinuosityBuiltPercent"),
        "road_discount": read_const(ITINERARY_RULES_CS, "RoadDiscountPercent"),
        "no_road_discount": read_const(ITINERARY_RULES_CS, "NoRoadDiscountPercent"),
        "walk_ticks": read_const(ITINERARY_RULES_CS, "WalkTicksPerCellDefault"),
        "payload_bytes": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxPayloadBytes"),
        "encoded_chars": read_const(CONSTRUCTION_INPUT_RULES_CS, "MaxEncodedChars"),
    }
    assert cap == {"schema": 3, "legacy_schema": 1,
        "sources": 64, "cargo": 64, "children": 16, "required": 8,
        "per_child": 12, "water_cask": 64, "reserve_days": 3, "scanned": 4096,
        "carrier": 12, "legs": 6, "drive_steps": 192, "receipts_per_turn": 8,
        "open_jobs": 16, "load_per_trip": 12, "zone_transit": 40, "max_nodes": 9,
        "zone_width": 80, "zone_height": 25, "sinuosity_open": 125,
        "sinuosity_built": 160, "road_discount": 60, "no_road_discount": 100,
        "walk_ticks": 1, "payload_bytes": 131072, "encoded_chars": 180000}, (
            "routed-construction structural caps moved")
    assert cap["per_child"] == cap["carrier"] == cap["load_per_trip"], (
        "parent packing, reservation, and physical porter capacity diverged")
    assert cap["children"] == cap["open_jobs"], (
        "one maximal routed receipt no longer meets the global job-table cap")

    source_pins = (
        (rules_text,
            "long value = (long)DailyUpkeep * WaterReserveDays;",
            "construction-input water reserve floor moved"),
        (rules_text,
            "Sources.Count < 1 || Sources.Count > MaxSourceLines",
            "construction-input parent source cap moved"),
        (plan_text,
            "int leg = Math.Min(left, KingdomConstructionInputRules.WaterCargoCapacity);",
            "construction-input water cask split moved"),
        (plan_text, "&& count < KingdomConstructionInputRules.MaxCargoPerChild",
            "construction-input child packing moved"),
        (plan_validation_text,
            "int compare = left.RouteCost.CompareTo(right.RouteCost);",
            "nearest routed-input candidate ordering moved"),
        (central_text,
            "sourceCount > KingdomLogisticsRules.CarrierCapacity",
            "central construction carrier cap moved"),
        (central_text,
            "deliveryCargoAuthority: KingdomDeliveryCargoAuthority.ConstructionInput",
            "central route no longer binds cargo to construction authority"),
        (itinerary_text, "count > legs.Length || count > MaxLegs",
            "construction itinerary leg cap moved"),
        (itinerary_text, "scaled = scaled / 100L;\n"
            "\t\t\tscaled = (scaled * roadDiscountPercent) / 100L;",
            "construction itinerary estimate changed its truncation order"),
        (route_text, "if (i + 1 < pathCount) cells++;",
            "construction route lost its non-final inter-zone cell"),
        (route_text, "legCount = pathCount + (claimedSource ? 0 : 1);",
            "construction route leg count moved"),
        (leg_text,
            "long duration = cells * KingdomItineraryRules.WalkTicksPerCellDefault;",
            "construction route duration moved away from physical cells"),
        (transition_text,
            "&& !SourcesAre(Receipt, SourceDebited)",
            "construction cargo can enter flight before exact source debit"),
        (state_text,
            "return receipt.TxPhase == KingdomConstructionInputTxPhase.SourcePending\n"
            "\t\t\t\t\t&& SourcesAre(receipt, SourceDebited) && CargoAre(receipt, CargoInFlight)",
            "construction parent can route without debited source/in-flight cargo"),
        (state_text,
            "p >= KingdomConstructionInputSourcePhase.Reserved && p <= KingdomConstructionInputSourcePhase.TransferIntent",
            "construction source rollback frontier moved"),
        (state_text,
            "p >= KingdomConstructionInputCargoPhase.Planned && p <= KingdomConstructionInputCargoPhase.PickupIntent",
            "construction cargo rollback frontier moved"),
        (state_text,
            "p >= KingdomConstructionInputCargoPhase.AtSource && p <= KingdomConstructionInputCargoPhase.DebitIntent",
            "construction cargo compensation frontier moved"),
        (state_text,
            "&& !CargoAny(receipt, KingdomConstructionInputCargoPhase.DebitIntent)",
            "construction manual cancellation can cross a debit intent"),
        (state_text,
            "&& !SourcesAny(receipt, KingdomConstructionInputSourcePhase.Spent)",
            "construction cancellation can refund a spent source"),
        (state_text,
            "&& !CargoAny(receipt, KingdomConstructionInputCargoPhase.Spent)",
            "construction cancellation can refund spent cargo"),
        (recovery_text,
            "if (FixedEquals(ObservedWitnessHash, AfterWitnessHash))\n"
            "\t\t\t\treturn KingdomConstructionInputDecision.Acknowledge;",
            "construction recovery no longer acknowledges exact after-state first"),
        (recovery_text,
            "return Paused ? KingdomConstructionInputDecision.WaitPaused\n"
            "\t\t\t\t: KingdomConstructionInputDecision.Apply;",
            "construction master pause no longer blocks a new physical callback"),
        (recovery_text,
            "start, elapsed, null, null, null);",
            "construction pause update no longer preserves source/cargo/child rows"),
        (recovery_text,
            "EffectiveArrivalTick = FrozenArrivalTick + PausedTicks;",
            "construction pause no longer shifts the frozen physical arrival exactly"),
        (recovery_text,
            "long total = source + flight + landed + spent + compensating + quarantined + lost;",
            "construction-input conservation buckets moved"),
        (commit_text,
            "Next.WaterSpent = (int)waterSpent;\n\t\t\tNext.WaterOutstanding = 0;",
            "routed water no longer closes through exact construction claims"),
        (drive_text, "for (int step = 0; step < MaxRoutedInputStepsPerPass; step++)",
            "routed-input recovery pass lost its bounded slice"),
        (drive_family_text,
            "KingdomConstructionInputCargoPhase.CreateIntent",
            "water cargo lost its physical cask-creation checkpoint"),
        (drive_family_text,
            "KingdomConstructionInputSourcePhase.SplitProved",
            "partial material lost its proved split checkpoint"),
        (drive_family_text,
            "KingdomConstructionInputTopology.CarrierInventory",
            "routed input lost explicit carrier custody evidence"),
        (drive_family_text,
            "KingdomConstructionInputTopology.LandingEscrow",
            "routed input lost explicit landing escrow evidence"),
        (drive_family_text,
            "KingdomConstructionInputTopology.Consumed",
            "routed input debit lost exact consumed evidence"),
        (global_recovery_text,
            "attended < MaxGlobalInputReceiptsPerTurn",
            "global routed-input recovery lost its per-turn owner cap"),
        (global_recovery_text,
            "!KingdomMaster.AutomaticWorkAllowed(system)",
            "master pause no longer freezes global routed-input recovery"),
        (cancellation_text,
            "Reconciles each line independently across its own physical boundary.",
            "mixed routed-input cancellation lost its physical boundary"),
        (cancellation_text, "if (item.Count != source.Before)",
            "material rollback no longer proves the restored whole count"),
        (cancellation_text, "item.Count = source.Before;",
            "partial material rollback no longer restores the frozen whole count"),
    )
    for source, needle, complaint in source_pins:
        assert needle in source, complaint

    def enum_wire(name: str) -> dict[str, int]:
        match = re.search(r"enum\s+" + name + r"\s*:\s*byte\s*\{(.*?)\}",
            declarations_text, re.S)
        assert match, "construction-input wire enum moved: " + name
        return {token: int(value) for token, value in re.findall(
            r"\b([A-Za-z]+)\s*=\s*([0-9]+)", match.group(1))}

    tx_values = enum_wire("KingdomConstructionInputTxPhase")
    source_values = enum_wire("KingdomConstructionInputSourcePhase")
    cargo_values = enum_wire("KingdomConstructionInputCargoPhase")
    assert tx_values == {
        "Invalid": 0, "ReservationPrepared": 1, "Reserved": 2, "SourcePending": 3,
        "Routing": 4, "LandedAwaitingOwner": 5, "DebitPending": 6, "Closing": 7,
        "Committed": 8, "RollbackPending": 9, "RolledBack": 10,
        "CompensationPending": 11, "Compensated": 12, "Quarantined": 13,
        "CancellationPending": 14, "Cancelled": 15}
    assert source_values == {
        "Invalid": 0, "Reserved": 1, "SplitIntent": 2, "SplitProved": 3,
        "TransferIntent": 4, "Debited": 5, "RestoreIntent": 6, "Restored": 7,
        "Spent": 8, "CompensationIntent": 9, "Compensated": 10,
        "Quarantined": 11}
    assert cargo_values == {
        "Invalid": 0, "Planned": 1, "CreateIntent": 2, "AtSource": 3,
        "PickupIntent": 4, "InFlight": 5, "Landed": 6, "DebitIntent": 7,
        "Spent": 8, "ReleaseIntent": 9, "Released": 10,
        "CompensationIntent": 11, "Compensated": 12, "Quarantined": 13}

    print("  source-pinned bounds:")
    print(f"    source/cargo lines {cap['sources']}/{cap['cargo']}; children "
        f"{cap['children']}; cargo/child {cap['per_child']}; required objects {cap['required']}")
    print(f"    scanned candidates {cap['scanned']}; itinerary legs {cap['legs']}; "
        f"drive steps/pass {cap['drive_steps']}")
    print(f"    recovery owners/turn {cap['receipts_per_turn']}; global jobs "
        f"{cap['open_jobs']}; schema {cap['schema']} (legacy {cap['legacy_schema']})")
    print(f"    receipt wire caps {cap['payload_bytes']} bytes / "
        f"{cap['encoded_chars']} chars (reported only; excluded from C4)")

    # One water source is split into physical casks. Source and cargo line caps bind first:
    # 64 full casks carry 4096 drams; one more dram needs a 65th exact line and refuses.
    def water_lines(amount: int) -> tuple[int, ...]:
        if amount <= 0:
            return ()
        rows = []
        left = amount
        while left > 0:
            take = min(left, cap["water_cask"])
            rows.append(take)
            left -= take
        return tuple(rows)

    water_at_cap = cap["sources"] * cap["water_cask"]
    water_per_carrier = min(cap["per_child"], cap["carrier"]) * cap["water_cask"]
    assert water_per_carrier == 768
    assert len(water_lines(water_at_cap)) == cap["sources"]
    assert sum(water_lines(water_at_cap)) == water_at_cap
    assert len(water_lines(water_at_cap + 1)) == cap["sources"] + 1
    print(f"  water-line cap: {water_at_cap} drams = {cap['sources']} x "
        f"{cap['water_cask']}; {water_at_cap + 1} drams needs {cap['sources'] + 1} and refuses")
    print(f"  water/carrier: {cap['per_child']} casks x {cap['water_cask']} = "
        f"{water_per_carrier} drams")

    # Exact consecutive-endpoint packing: same endpoint fills 12-cargo porters; a new endpoint
    # starts a new child. It proves both independent caps without inventing a traffic rate.
    def packed_children(endpoints: tuple[str, ...]) -> tuple[tuple[int, int], ...]:
        children = []
        start = 0
        while start < len(endpoints):
            count = 1
            while (start + count < len(endpoints) and count < cap["per_child"]
                    and endpoints[start + count] == endpoints[start]):
                count += 1
            children.append((start, count))
            start += count
        return tuple(children)

    same_endpoint = tuple("holder" for _ in range(cap["cargo"]))
    same_children = packed_children(same_endpoint)
    assert len(same_children) == math.ceil(cap["cargo"] / cap["per_child"])
    assert sum(count for _start, count in same_children) == cap["cargo"]
    assert len(packed_children(tuple("holder" for _ in range(cap["per_child"])))) == 1
    assert len(packed_children(tuple("holder" for _ in range(cap["per_child"] + 1)))) == 2
    sixteen_endpoints = tuple("holder-" + str(i) for i in range(cap["children"]))
    assert len(packed_children(sixteen_endpoints)) == cap["children"]
    seventeen_endpoints = tuple("holder-" + str(i) for i in range(cap["children"] + 1))
    assert len(packed_children(seventeen_endpoints)) == cap["children"] + 1
    print(f"  carrier packing: 64 same-holder cargo -> {len(same_children)} porters; "
        "16 endpoint groups fit; 17 refuse")
    assert len(packed_children(sixteen_endpoints)) == cap["open_jobs"]
    print("  global job boundary: 16 endpoint children fill all 16 rows; any second job refuses")
    def within_bound(count: int, maximum: int) -> bool:
        return 0 <= count <= maximum

    for key in ("required", "legs", "scanned"):
        assert within_bound(cap[key], cap[key])
        assert not within_bound(cap[key] + 1, cap[key])
    print("  other cap+1: required objects 8/9; itinerary legs 6/7; scanned candidates 4096/4097")

    # Reserve sensitivity uses only source values. Floor is three actual daily-upkeep units;
    # exact stock pays request + prior reservations + floor, and one less refuses.
    int_max = (1 << 31) - 1

    def reserve_floor(daily: int) -> int | None:
        if daily < 0 or daily * cap["reserve_days"] > int_max:
            return None
        return daily * cap["reserve_days"]

    daily_boundary = int_max // cap["reserve_days"]
    assert reserve_floor(0) == 0
    assert reserve_floor(1) == cap["reserve_days"]
    assert reserve_floor(daily_boundary) == daily_boundary * cap["reserve_days"]
    assert reserve_floor(daily_boundary + 1) is None
    daily_samples = []
    for _stage, floor_population, storage in STAGES:
        population = max(floor_population, SRC["FloorLevel"])
        daily = upkeep_per_day(population, storage)
        held = reserve_floor(daily)
        assert held is not None and held // cap["reserve_days"] == daily
        assert held % cap["reserve_days"] == 0
        daily_samples.append(daily)
    requested = cap["water_cask"]
    prior_reserved = cap["water_cask"]
    floor = reserve_floor(daily_samples[-1])
    assert floor is not None
    exact_stock = requested + prior_reserved + floor
    assert exact_stock - prior_reserved - floor == requested
    assert exact_stock - 1 - prior_reserved - floor == requested - 1
    print(f"  reserve sensitivity: floor=daily x {cap['reserve_days']}; daily "
        f"{daily_boundary} fits, {daily_boundary + 1} overflows/refuses")
    print("  G1 reuse: rung-floor daily upkeep " + "/".join(str(value)
        for value in daily_samples) + " round-trips through each three-day reserve")
    print(f"  stock boundary: {exact_stock} - prior:{prior_reserved} - floor:{floor} = "
        f"request:{requested}; stock -1 is insufficient")

    happy_tx = ("ReservationPrepared", "Reserved", "SourcePending", "Routing",
        "LandedAwaitingOwner", "DebitPending", "Closing", "Committed")
    for phase in happy_tx:
        assert "KingdomConstructionInputTxPhase." + phase in state_text
    source_material = ("Reserved", "SplitIntent", "SplitProved", "TransferIntent",
        "Debited", "Spent")
    cargo_material = ("Planned", "AtSource", "PickupIntent", "InFlight", "Landed",
        "DebitIntent", "Spent")
    cargo_water = ("Planned", "CreateIntent", "AtSource", "PickupIntent", "InFlight",
        "Landed", "DebitIntent", "Spent")
    for phase in set(source_material):
        assert "KingdomConstructionInputSourcePhase." + phase in transition_text
    for phase in set(cargo_material + cargo_water):
        assert "KingdomConstructionInputCargoPhase." + phase in transition_text
    print("  happy parent: " + " -> ".join(happy_tx))
    print("  partial material: " + " -> ".join(source_material))
    print("  water cargo: " + " -> ".join(cargo_water))

    def estimate_cells(chebyshev: int, sinuosity: int, road_discount: int) -> int:
        assert chebyshev >= 0 and sinuosity > 0 and 0 < road_discount <= 100
        scaled_cells = chebyshev * sinuosity // 100
        return scaled_cells * road_discount // 100

    def leg_ticks(cells: int) -> int:
        return max(cells, 1) * cap["walk_ticks"]

    def route_ticks(hops: int, built: bool, paved: bool) -> int:
        assert 1 <= hops <= cap["legs"]
        sinuosity = cap["sinuosity_built"] if built else cap["sinuosity_open"]
        discount = cap["road_discount"] if paved else cap["no_road_discount"]
        base = estimate_cells(cap["zone_transit"], sinuosity, discount)
        return sum(leg_ticks(base + (1 if index + 1 < hops else 0))
            for index in range(hops))

    assert leg_ticks(0) == cap["walk_ticks"]
    for hops in range(1, cap["legs"] + 1):
        assert route_ticks(hops, False, True) <= route_ticks(hops, False, False)
        assert route_ticks(hops, True, True) <= route_ticks(hops, True, False)

    # Counts are lengths of the exact durable-checkpoint walks traced above, not tuning knobs.
    tx_checkpoints = happy_tx[1:]
    material_checkpoints = ("adopt-object", "cargo-at-source", "source-transfer-intent",
        "cargo-pickup-intent", "move-before", "move-after", "carrier-custody",
        "source-debited", "cargo-in-flight", "cargo-landed", "cargo-debit-intent",
        "consume-before", "consume-after", "cargo-spent", "source-spent")
    split_extras = ("split-intent", "split-before", "split-after",
        "remainder-adopted", "split-proved")
    water_checkpoints = ("create-intent", "cask-adopted", "cask-custody", "at-source",
        "source-transfer-intent", "pickup-intent", "pour-before", "pour-after",
        "source-debited", "in-flight", "landed", "debit-intent", "consume-before",
        "consume-after", "spent", "source-spent")
    child_checkpoints = ("central-in-flight", "central-landed")
    assert (len(tx_checkpoints), len(material_checkpoints), len(split_extras),
        len(water_checkpoints), len(child_checkpoints)) == (7, 15, 5, 16, 2)

    def children_for(lines: int) -> int:
        if lines <= 0:
            return 0
        return math.ceil(lines / min(cap["per_child"], cap["carrier"]))

    def receipt_steps(material_lines: int, split_lines: int, water_line_count: int) -> int:
        lines = material_lines + split_lines + water_line_count
        assert 1 <= lines <= cap["cargo"]
        return (len(tx_checkpoints)
            + material_lines * len(material_checkpoints)
            + split_lines * (len(material_checkpoints) + len(split_extras))
            + water_line_count * len(water_checkpoints)
            + children_for(lines) * len(child_checkpoints))

    def passes_needed(steps: int) -> int:
        return math.ceil(steps / cap["drive_steps"])

    assert receipt_steps(1, 0, 0) == 24
    line_points = tuple(sorted({1, cap["per_child"], cap["cargo"] // 2, cap["cargo"]}))
    print("  deterministic open/unpaved itinerary ticks; cells/leg = ZoneTransitCells:")
    print(f"  {'cargo':<8}{'steps':>8}{'passes':>8}" + "".join(
        f"{('h' + str(hop)):>7}" for hop in range(1, cap["legs"] + 1)))
    for lines in line_points:
        steps = receipt_steps(lines, 0, 0)
        passes = passes_needed(steps)
        route_cells = [route_ticks(hops, False, False)
            for hops in range(1, cap["legs"] + 1)]
        delivered = [max(passes, ticks) for ticks in route_cells]
        print(f"  {lines:<8}{steps:>8}{passes:>8}" + "".join(
            f"{turns:>7}" for turns in delivered))

    max_whole_steps = receipt_steps(cap["cargo"], 0, 0)
    max_split_steps = receipt_steps(0, cap["cargo"], 0)
    max_water_steps = receipt_steps(0, 0, cap["cargo"])
    assert (max_whole_steps, max_split_steps, max_water_steps) == (979, 1299, 1043)
    assert tuple(passes_needed(value)
        for value in (max_whole_steps, max_split_steps, max_water_steps)) == (6, 7, 6)
    print("  step sensitivity at 64 lines: whole material 979/6 passes; "
        "split material 1299/7; water 1043/6")

    road_heading = "route ticks at " + str(cap["legs"]) + " legs"
    print(f"  {'road state':<20}{road_heading:>24}")
    road_cases = ((False, False, "open/unpaved"), (False, True, "open/paved"),
        (True, False, "built/unpaved"), (True, True, "built/paved"))
    road_values = {}
    for built, paved, label in road_cases:
        road_values[label] = route_ticks(cap["legs"], built, paved)
        print(f"  {label:<20}{road_values[label]:>24}")
    assert road_values["open/paved"] < road_values["open/unpaved"]
    assert road_values["built/paved"] < road_values["built/unpaved"]

    # Pure mirror of DecidePhysicalMutation. Pause may stop only a before-state callback; it
    # cannot hide an already-applied callback or a foreign third state.
    def decide(observed: str, paused: bool) -> str:
        before, after = "before", "after"
        if observed == after:
            return "Acknowledge"
        if observed != before:
            return "Quarantine"
        return "WaitPaused" if paused else "Apply"

    assert decide("before", False) == "Apply"
    assert decide("before", True) == "WaitPaused"
    assert decide("after", True) == "Acknowledge"
    assert decide("third", True) == "Quarantine"
    long_max = (1 << 63) - 1

    def effective_arrival(frozen: int, paused: int) -> int | None:
        if frozen < 0 or paused < 0 or frozen > long_max - paused:
            return None
        return frozen + paused

    assert effective_arrival(0, cap["drive_steps"]) == cap["drive_steps"]
    assert effective_arrival(long_max, 0) == long_max
    assert effective_arrival(long_max, 1) is None
    frozen_route = road_values["open/unpaved"]
    paused_route = effective_arrival(frozen_route, cap["drive_steps"])
    assert paused_route is not None
    unpaused_delivery = max(frozen_route, passes_needed(max_whole_steps))
    paused_delivery = max(paused_route, passes_needed(max_whole_steps))
    assert paused_delivery == unpaused_delivery + cap["drive_steps"]
    print("  interruption: before+paused waits; exact after acknowledges; third state quarantines")
    print(f"  pause timing: route {frozen_route} + pause {cap['drive_steps']} = "
        f"{paused_delivery}; long.MaxValue + 1 refuses, never wraps")
    print("  master pause: global physical recovery freezes; PausedTicks shifts arrival "
        "exactly and custody remains unchanged")

    # Conservation buckets reproduce TryDeriveConservation for one cargo. A physical unit is in
    # exactly one custody bucket plus proved loss. Over-spend/over-loss refuses.
    def conservation(amount: int, phase: str, spent: int = 0, lost: int = 0):
        if amount < 0 or spent < 0 or lost < 0 or spent + lost > amount:
            return None
        remainder = amount - spent - lost
        buckets = {"source": 0, "flight": 0, "landed": 0, "spent": spent,
            "compensating": 0, "quarantined": 0, "lost": lost}
        if phase == "InFlight":
            buckets["flight"] = remainder
        elif phase in ("Landed", "DebitIntent"):
            buckets["landed"] = remainder
        elif phase == "CompensationIntent":
            buckets["compensating"] = remainder
        elif phase == "Quarantined":
            buckets["quarantined"] = remainder
        else:
            buckets["source"] = remainder
        assert sum(buckets.values()) == amount
        return buckets

    amount = cap["water_cask"]
    def named_bucket(phase: str) -> str:
        if phase == "InFlight":
            return "flight"
        if phase in ("Landed", "DebitIntent"):
            return "landed"
        if phase == "Spent":
            return "spent"
        if phase == "CompensationIntent":
            return "compensating"
        if phase == "Quarantined":
            return "quarantined"
        return "source"

    for phase in (name for name in cargo_values if name != "Invalid"):
        spent = amount if phase == "Spent" else 0
        buckets = conservation(amount, phase, spent, 0)
        assert buckets is not None and sum(buckets.values()) == amount
        assert buckets[named_bucket(phase)] == amount
    before_pause_buckets = conservation(amount, "InFlight", 0, 0)
    after_pause_buckets = dict(before_pause_buckets) if before_pause_buckets else None
    assert after_pause_buckets == before_pause_buckets, "pause changed custody buckets"

    custody_cases = (
        ("AtSource", 0, 0, "source"),
        ("InFlight", 0, 0, "flight"),
        ("Landed", 0, 0, "landed"),
        ("Spent", amount, 0, "spent"),
        ("CompensationIntent", 0, 0, "compensating"),
        ("Quarantined", 0, cap["reserve_days"], "quarantined"),
    )
    print(f"  {'custody cut':<22}{'expected':>10}{'named bucket':>15}{'proved lost':>14}")
    for phase, spent, lost, named in custody_cases:
        buckets = conservation(amount, phase, spent, lost)
        assert buckets is not None
        expected_bucket = amount if named == "spent" else amount - spent - lost
        assert buckets[named] == expected_bucket
        print(f"  {phase:<22}{amount:>10}{buckets[named]:>15}{buckets['lost']:>14}")
    assert conservation(amount, "Spent", amount + 1, 0) is None
    assert conservation(amount, "Quarantined", amount, 1) is None

    rollback_sources = ("Reserved", "SplitIntent", "SplitProved", "TransferIntent")
    rollback_cargo = ("Planned", "CreateIntent", "AtSource", "PickupIntent")
    compensation_cargo = ("AtSource", "PickupIntent", "InFlight", "Landed", "DebitIntent")
    assert all(phase in source_values for phase in rollback_sources)
    assert all(phase in cargo_values for phase in rollback_cargo + compensation_cargo)
    assert max(source_values[phase] for phase in rollback_sources) == source_values["TransferIntent"]
    assert max(cargo_values[phase] for phase in rollback_cargo) == cargo_values["PickupIntent"]
    assert min(cargo_values[phase] for phase in compensation_cargo) == cargo_values["AtSource"]
    assert max(cargo_values[phase] for phase in compensation_cargo) == cargo_values["DebitIntent"]
    print("  rollback frontier: all sources Reserved..TransferIntent and cargo Planned..PickupIntent")
    print("  compensation frontier: a crossing parent has debited sources; cargo AtSource..DebitIntent")
    print("  manual cancellation frontier: DebitIntent/Spent refuse before physical recovery")
    print("  conservation: source + flight + landed + spent + compensating + quarantine + loss = expected")
    print("  W6/W7/G1/G2/C4 accounting reuse: yes; no production, transport aura, or save bytes added")
    print("  native cold-save/obstruction/carrier/custody/debit/recovery evidence: UNSIGNED")


def caveats():
    rule("Where this model is too crude to decide anything")
    print(f"""
1. OPEN WATER VOLUME IS STILL UNKNOWN, and it matters more with every pass. The uncapping made
   an absence ask the pool for the whole absence at once; Addendum 11(a) then took production
   away from Camp entirely, so the site's own pools are now the ONLY thing standing between a
   young settlement and the thirst ladder, and how much is standing there is a worldgen roll
   this model cannot see. The survey behind G1 confirms the pools do not refill: nothing in
   vanilla replenishes a plain `LiquidVolume`, and the only indefinite sources are producer
   carriers and the `LiquidFont` weeps. A camp's water is therefore a strictly finite dowry
   with a clock on it, and whether that clock is generous enough to reach Steading is the
   single most important thing a live playtest has to answer about this wave.

1b. THE ORIGINAL FORM OF THE SAME CAVEAT, kept because the arithmetic has not changed.
   `KingdomSurvey` counts only PURE fresh open water; vanilla Pond / SaltyWaterPuddle /
   brackish pools are mixtures and do not count. Under the cap, a site with a small pool
   behaved like a watered site because fetch never asked for more than {RETIRED_CAP_DAYS} days' worth.
   Uncapped, a 90-day absence asks the pool for 90 days of hauling in one go, and
   FetchableDrams clamps to whatever is actually standing there. So the "watered" column
   in Q1 is an UPPER BOUND that a real site may not reach. What HAS been settled since this
   was written is the refill question in 1 above: they do not. Measure the starting volume in
   a live game; do not wait for it to come back.

2. THE LEVEL'S INPUTS ARE STATIC IN Q6 TO Q8 AND CLOSED IN Q9. Those three sections assume
   every work runs at full, which is the level a settlement gets when everything is manned
   and sound. Q9 runs the same arithmetic with wear folded in and the ruin loop closed, and
   its answer is that the loop costs about one rung at worst - to every build now, staffless
   ones included, which is Addendum 10(b)'s doing. What is STILL not modelled is crew stretch
   from any cause other than ruin: `AssignCrew` fills works in placement order out of
   whatever the water detail left, so a settlement that is merely short-handed runs its
   crewed works below 100 for reasons this model never varies (see 3).

   Nor is the leak modelled INSIDE a slide. Q9's leak table prices one damaged store against
   one rung's daily bill, which is the decision the tuning needed; what it does not do is run
   a whole slide with the water economy of Q1 wired to the ruin of Q5, because the two
   sections keep time differently (Q1 is a daily balance, Q5 is a four-day step). The bound
   that makes that safe to defer is the ORDER inside the pass: upkeep is drawn before the leak
   is taken, so the leak can never be the reason a settlement goes dry - only the reason it
   has no cushion when something else is.

3. CREW ALLOCATION FOR WORKS IS NOT A PLAYER CHOICE - `AssignCrew` fills works in
   placement order. `System.WaterCrew` IS a real choice, which is why this model varies
   that and not the rest.

4. NO RAIDS, TRIBUTE, COMMISSIONS OR SALVAGE. Tribute {SRC["RaidTributeDrams"]}, plunder {SRC["RaidPlunderDrams"]} and salvage are real
   sinks omitted here; they are lumpy events, and averaging them per day would flatter the
   model.

5. FOOD IS A CYCLE NOW (Q11, G2), AND THREE THINGS ABOUT IT ARE STILL UNMODELLED HERE. The
   ripening cycle is no longer one of them: G2 proves the cycle pays exactly what the Carries
   promised over one crop's {SRC["CropDays"]} days and the runtime subtracts a sown field from the
   clocked make so it is paid once, which is why Q11's Carries-denominated columns are now the
   whole truth for a sown settlement rather than a lower bound. What is NOT modelled:
     - THE SEED GATE'S TIMING. G2's check 3 proves Camp and Steading are held without any seed at
       all, and Q11 proves every rung is holdable once seed is in the ground. What neither answers
       is how long a founder actually spends between the two - that is a play question about how
       often a trader carries seed and how near the nearest watervine is, and it is a playtest
       reading rather than an arithmetic one.
     - THE FOUNDER'S OWN HAND-HARVEST. A ripe row carries vanilla `Harvestable` for one day before
       the settlement's hands gather it, so a founder standing in a field can take up to
       `Rows x OnSuccessAmount` crop items into their own pack instead. That is a TRANSFER out of
       the settlement's yield and not an addition to it (the gathering counts rows still standing
       RIPE), so it cannot inflate any column here; what it can do is make a founder personally
       rich in vinewafers, which is not an economy this file models.
     - TRADE. The two shipped deal records declare water income only. The trade operation also
       has a keyed material-cargo lane, but neither current deal declares materials and neither
       lane carries food. The only ways into a larder are therefore the fields, the wild and the
       founder's own hands. Seed moves on the wares tables; food does not. This model assigns
       trade food zero as the current scope boundary, rather than pretending a future food deal
       already ships.

6. THE ARRIVAL TIMER, NOT WATER, IS STILL THE GROWTH GATE above about eight settlers:
   3 + pop/2 days per arrival means pop 50 waits 28 days for one. No water tuning touches
   that, and this model does not answer it.
""")


def q10_comfort():
    rule("Q10 Comfort: authored yard shade and physical reach; civic title excluded")
    yard_shades = re.findall(r'Shades="([^"]*)"', open(YARD_XML, encoding="utf-8-sig").read())
    yard_points = 0
    for raw in yard_shades:
        for part in raw.split(","):
            bits = part.split(":")
            if len(bits) == 2 and bits[1].strip().isdigit():
                yard_points += int(bits[1])
    print(f"""
Two authored inputs reach the permanent lift term, and physical reach reduces one of them.
Both are capped by `LiftCapPercent` = {SRC["LiftCapPercent"]}, so neither outruns the water.

  a yard trade         `Shades` on a <yardwork>, capped at {SRC["MaxShadePerWork"]} per house. The shipped four
                       declare {yard_points} points between them, and one house takes ONE trade.
  the reach            a work's lift now lands in proportion to the settlement's roofs it
                       covers (`KingdomReachRules.Landed`). Binding goods are untouched.

THE TITLE-ONLY FENCE. `KingdomSystem.Shade` reads MealShade only, and seat plus off-seat
normalization force the serialized legacy NotableShade field to zero. An office therefore
changes identity and fiction, never capacity, even before the player revisits an old save.
""")

    print("""
THE REACH, which is the half that TAKES. Before this wave every lifting work counted citywide
off its Carries; now it counts for the share of the settlement's roofs it covers, which is
what makes the temple quarter different ground from the tanners'. Bands come from the plot
tier (`KingdomReachRules.BandForSize`): S shades its own plot, M its quarter, L its zone, XL
the city. "gathered" is a founder who built the civic works among the houses; "scattered" is
one who put them out past the fields, where a quarter band reaches perhaps a third of the
roofs and a plot band reaches nobody who lives anywhere.
""")
    print(
        f"{'rung':<9}{'declared':>9}{'gathered':>9}{'scattered':>10}{'cap':>6}"
        f"{'was':>6}{'gathered':>9}{'scattered':>10}"
    )
    covers = {"plot": (0, 0), "quarter": (100, 33), "zone": (100, 100), "city": (100, 100)}
    for i, (name, floor, _cap) in enumerate(STAGES):
        homes = max(floor, SRC["FloorLevel"])
        declared = 0
        gathered = 0
        scattered = 0
        for design in CATALOGUE:
            if design.stage > i:
                continue
            lift = sum(v for k, v in design.carries.items() if k not in BINDING)
            if lift <= 0:
                continue
            near, far = covers[REACH_BAND.get(design.plot, "plot")]
            declared += lift
            gathered += landed(lift, homes * near // 100, homes)
            scattered += landed(lift, homes * far // 100, homes)
        cap = homes * SRC["LiftCapPercent"] // 100
        was = homes + min(declared, cap)
        now_near = homes + min(gathered, cap)
        now_far = homes + min(scattered, cap)
        print(
            f"{name:<9}{declared:>9}{gathered:>9}{scattered:>10}{cap:>6}"
            f"{was:>6}{now_near:>9}{now_far:>10}"
        )

    print("""
Reading. Those columns say something worth saying plainly: with one of EVERY lifting design a
rung can reach standing, the declared lift overruns the cap so far that scoping changes the
level at no rung above Camp. The cap was always the binding constraint for a fully built
settlement, and it still is. So this change does not tax the founder who builds everything.

Where it bites is the settlement that has built ONE civic work, which is every settlement on
its way up. Same arithmetic, one design at a time - the biggest lift each rung can reach:
""")
    print(f"{'rung':<9}{'design':<14}{'band':<9}{'declared':>9}{'gathered':>9}{'scattered':>10}")
    for i, (name, floor, _cap) in enumerate(STAGES):
        homes = max(floor, SRC["FloorLevel"])
        best = None
        for design in CATALOGUE:
            if design.stage > i:
                continue
            lift = sum(v for k, v in design.carries.items() if k not in BINDING)
            if lift > 0 and (best is None or lift > best[1]):
                best = (design, lift)
        if best is None:
            print(f"{name:<9}{'-':<14}{'-':<9}{0:>9}{0:>9}{0:>10}")
            continue
        design, lift = best
        band = REACH_BAND.get(design.plot, "plot")
        near, far = covers[band]
        print(
            f"{name:<9}{design.key:<14}{band:<9}{lift:>9}"
            f"{landed(lift, homes * near // 100, homes):>9}"
            f"{landed(lift, homes * far // 100, homes):>10}"
        )

    print(f"""
That is the mechanic Addendum 6 asked for: the same shrine is worth its whole amount among the
houses and a fraction of it out past the fields, and the founder can SEE which they built
(`KingdomReach.QuarterLine` names the ground in the status report).

Camp is the rung to watch. Every lift design a camp can reach is an S plot (fire, bench,
shrine, shrine garth, bookshelf, cairn, mill, toolshed), so a camp's lift is now ZERO however
many of them stand, and its level is its binding goods alone. That is the addendum read
literally - a wayside statue shades the ground it stands on, not the settlement - and it costs
a camp the whole {max(STAGES[0][1], SRC["FloorLevel"]) * SRC["LiftCapPercent"] // 100} settlers of headroom it used to get for free. It is not a stall: Camp is
floored at `FloorLevel` = {SRC["FloorLevel"]} regardless, every rung stays holdable on its binding goods, and
an office cannot put that headroom back. If Camp needs softening, the lever is a `Reach`
attribute on an authored design, not a hidden reward attached to a named resident.
""")



# --------------------------------------------------------------------------------------
# 6. Food, now that it is a flow. The water lane's mirror, and where the mirror breaks.
# --------------------------------------------------------------------------------------


def rations_per_day(pop: int) -> int:
    """KingdomRules.RationsPerDay: one ration a settler a day, at EVERY rung.

    Flat where `upkeep_per_day` is stage-scaled, and that is the load-bearing divergence:
    `KingdomSubsidenceRules.SupportedLevel` hands `Supports.Food` to `Equilibrium` undivided,
    so the food arm of the level IS the daily ration bill and a settlement standing at its own
    level makes exactly what it eats.
    """
    return max(pop, 0)


def foraged_rations(hands: int, days: int) -> int:
    """KingdomRules.ForagedRations: free hands off the land, under a flat daily ceiling.

    Foraging's `OpenWater` is a ceiling rather than a pool, because the wild does not care how
    many baskets you bring. Clamped on the RATE before the days multiply out.
    """
    if hands <= 0 or days <= 0:
        return 0
    return min(hands * SRC["ForageRationsPerHand"], SRC["MaxForagedRationsPerDay"]) * days


def resolve_hunger(streak: int, stage: int, pop: int) -> str:
    """KingdomRules.ResolveHunger, shaped exactly like ResolveThirst."""
    if streak <= 0:
        return "Fed"
    if streak >= SRC["HungryIntervalsToFamine"] and stage > 0:
        return "Famine"
    if streak >= SRC["HungryIntervalsToEmigrate"] and pop > SRC["LoyalCoreSettlers"]:
        return "Emigration"
    return "Warned"


def resolve_thirst(streak: int, stage: int, pop: int) -> str:
    """KingdomRules.ResolveThirst, for the composition table below."""
    if streak <= 0:
        return "Sustained"
    if streak >= SRC["DryIntervalsToWither"] and stage > 0:
        return "Withering"
    if streak >= SRC["DryIntervalsToEmigrate"] and pop > SRC["LoyalCoreSettlers"]:
        return "Emigration"
    return "Warned"


_BITE = {
    "Sustained": 0,
    "Fed": 0,
    "Warned": 1,
    "Emigration": 2,
    "Withering": 3,
    "Famine": 3,
}


def compose_scarcity(thirst: str, hunger: str) -> int:
    """KingdomRules.ComposeScarcity: the WORSE of the two ladders, never their sum."""
    return max(_BITE[thirst], _BITE[hunger])


def departures_of(bite: int) -> int:
    """Settlers one resolve at this bite costs. Terminal costs a departure and a mark, not two."""
    return 1 if bite >= 2 else 0


def _food_plan(stage: int, need: int, by):
    globals()["_KIND"] = "food"
    return _plan("food", stage, need, by)


def _passive_water_crew(stage: int, pop: int) -> int:
    """Hands the water detail needs once the rung's best passive water works are raised."""
    bill = pop * STAGE_PERCENT[stage] // 100
    passive = [
        d for d in CATALOGUE if "water" in d.carries and d.stage <= stage and d.staff == 0
    ]
    covered = 0
    if passive:
        best = max(passive, key=lambda d: d.carries["water"])
        covered = best.carries["water"] * math.ceil(bill / best.carries["water"])
    short = max(0, bill - covered)
    return math.ceil(short / SRC["FetchDramsPerSettler"])


def q11_food():
    rule("Q11 Food, now that it is a flow: what a rung grows against what it eats")
    print(f"""
THE DENOMINATION, and the one place it deliberately does not mirror water. One point of
`food` is ONE SETTLER FED FOR ONE DAY, at every rung. Water is billed {STAGE_PERCENT} per
hundred by stage and its Carries are divided back out by the same percentage; food is billed
flat and its Carries are handed to `Equilibrium` undivided, because (the catalogue's own
words) "a dinner and a bed are both counted in people, and neither is divided by the
settlement's own thirst the way a dram is".

That flatness buys the whole lane its central property, and it is worth stating as an
identity rather than a table:

    a settlement standing at its own supported level makes exactly the rations it eats.

`Supports.Food` is what the fields make in a day; `RationsPerDay(pop)` is what the people eat
in a day; the food arm of `Equilibrium` is `Supports.Food`. So food binds at exactly the
population it feeds, with nothing left over and nothing owed - and every column below is a
measure of the CUSHION around that identity, not of whether it holds.

THE CUSHION HAS TWO PARTS. Foraging ({SRC["ForageRationsPerHand"]} a hand a day, ceiling
{SRC["MaxForagedRationsPerDay"]} a day whoever walks the ground) and the larders. Foraging is
food's answer to the water detail, with one difference that matters: hauled water goes into a
cask and foraged food goes straight into a mouth, so a settlement that has dedicated NO larder
still eats. What stops that being an answer above a Camp is the ceiling, and the ceiling is
chosen: {SRC["MaxForagedRationsPerDay"]} is `FloorLevel`, and it is also the population ceiling
of the Camp rung ({STAGES[1][0]} opens at {STAGES[1][1]}). The wild feeds a camp and nothing
larger.
""")
    print(f"{'rung':<9}{'people':>8}{'eats/d':>8}{'hauling':>9}{'free':>6}{'forage':>8}   verdict")
    for i, (name, floor, _cap) in enumerate(STAGES):
        pop = max(floor, SRC["FloorLevel"])
        # The MANUAL phase deliberately: everybody the water bill wants is on the detail and
        # nothing passive is standing. It is the hardest case for foraging (fewest hands left)
        # and the one the Camp claim has to survive.
        crew = min(pop, math.ceil(pop * STAGE_PERCENT[i] / 100 / SRC["FetchDramsPerSettler"]))
        free = max(0, pop - crew)
        forage = foraged_rations(free, 1)
        eats = rations_per_day(pop)
        if forage >= eats:
            verdict = "FEEDS ITSELF off the land, with no field standing"
        elif free <= 0:
            verdict = "every hand is on the water; nothing is foraged and everything must be grown"
        else:
            verdict = f"the wild covers {forage / eats:.0%}; the rest must be grown"
        print(f"{name:<9}{pop:>8}{eats:>8}{crew:>9}{free:>6}{forage:>8}   {verdict}")
    print("""
CAMP SELF-SUSTAINS, which is the floor this lane had to clear. A camp of four with two of them
on the water detail has two hands left, and two hands forage exactly the four rations four
people eat. That is the same promise the water lane makes at the same rung - Q7's "Camp wants
half its people on water" - said in food's voice, and it is why a founder who has commissioned
nothing is never starved by this system.

Now the two ways a founder actually feeds a rung, on Q6's own terms. `staff` is what the food
plan takes off the free hands, which is why the forage column shrinks as the plan grows: hands
are spent once, and a settler in a field is not also out on the ridge.
""")
    for label, by in (
        (
            "cheapest",
            lambda d: (d.cost / max(d.carries.get("food", 1), 1), -d.carries.get("food", 0)),
        ),
        ("grandest", lambda d: -d.carries.get("food", 0)),
    ):
        print(f"  --- {label} ---")
        print(
            f"  {'rung':<9}{'people':>8}{'eats/d':>8}{'made/d':>8}{'forage':>8}{'spare':>7}   plan"
        )
        for i, (name, floor, _cap) in enumerate(STAGES):
            pop = max(floor, SRC["FloorLevel"])
            eats = rations_per_day(pop)
            got = _food_plan(i, eats, by)
            if got is None:
                print(f"  {name:<9}{pop:>8}{eats:>8}{'-':>8}{'-':>8}{'-':>7}   NO FOOD DESIGN")
                continue
            design, count = got
            made = design.carries["food"] * count
            crew = _passive_water_crew(i, pop)
            free = max(0, pop - crew - design.staff * count)
            forage = foraged_rations(free, 1)
            spare = made + forage - eats
            flag = "" if spare >= 0 else "   SHORT"
            print(
                f"  {name:<9}{pop:>8}{eats:>8}{made:>8}{forage:>8}{spare:>7}   "
                f"{design.key}x{count} ({design.staff * count} hands){flag}"
            )
        print()

    print("""
Every rung feeds itself both ways, with spare on top of the identity - the plans overshoot
because a whole number of fields rarely lands exactly on the bill, and the overshoot is where
the larders fill from. What did NOT change from Q6's reading: food never automates. Every rung
above the kitchen garden wants hands and the grand ones want them still; what scale buys is a
better rate, never a discharge.
""")

    rule("Q11b How deep the larders are, and what a bad year costs before the founder sees it")
    print(f"""
Food storage is declared on the blueprint (`r_KingdomLarderCapacity`) and never in the
catalogue, for the same reason a cistern's `MaxVolume` is: what a design adds to the LEVEL is a
catalogue fact and how much its vessel holds is a fact about the vessel. The ratio is the
cistern's own - a store holds about 32 days of what it carries - so the larder shed holds
{[d.larder for d in CATALOGUE if d.key == "larder"][0]} against `food:2` and the granary
{[d.larder for d in CATALOGUE if d.key == "granary"][0]} against `food:9`. A container the
founder dedicated by hand and that declares nothing gets {SRC["DefaultLarderCapacity"]}.

"blackout" below is the honest question: the fields stop entirely (ruined, unstaffed, a bad
season), foraging is all that is left, and this is how long the larders cover the difference.
""")
    pantries = [d for d in CATALOGUE if d.larder > 0]
    print(f"{'rung':<9}{'people':>8}  {'pantry plan':<16}{'held':>7}{'blackout':>11}   against water")
    for i, (name, floor, _cap) in enumerate(STAGES):
        pop = max(floor, SRC["FloorLevel"])
        reach = [d for d in pantries if d.stage <= i]
        best = max(reach, key=lambda d: d.larder) if reach else None
        if best is None:
            print(f"{name:<9}{pop:>8}  {'-':<16}{'-':>7}{'-':>11}   -")
            continue
        # One pantry a rung until the granary opens, then as many as the rung's own bill wants
        # a fortnight of. Both are plans a player plausibly builds.
        count = max(1, math.ceil(pop * 14 / best.larder))
        held = best.larder * count
        crew = _passive_water_crew(i, pop)
        forage = foraged_rations(max(0, pop - crew), 1)
        gap = rations_per_day(pop) - forage
        # The same blackout on the water side: the passive works stop and the casks are all
        # there is, against the rung's own daily bill.
        passive = [d for d in CATALOGUE if "water" in d.carries and d.stage <= i and d.staff == 0]
        wbill = max(1, pop * STAGE_PERCENT[i] // 100)
        wheld = 0
        if passive:
            wbest = max(passive, key=lambda d: d.carries["water"])
            wheld = wbest.capacity * math.ceil(wbill / max(1, wbest.carries["water"]))
        # A rung the wild already covers has no blackout to measure: the fields could all fall
        # and nobody would miss a meal, which is a truer sentence about a camp than a number is.
        blackout = "no gap" if gap <= 0 else f"{held // gap} d"
        print(
            f"{name:<9}{pop:>8}  {best.key + 'x' + str(count):<16}{held:>7}{blackout:>11}   "
            f"{wheld // wbill} d of water"
        )
    print(f"""
READING. The two cushions CROSS, and they cross at Town. Below it food is the safer of the two
- a camp cannot have a food blackout at all, because the wild already covers it, and a village
holds about as many days of bread as of water. Above it the lines part hard: a Town holds 27
days of food against 71 of water, and a City 18 against 109. So the food lane gets steadily
LESS forgiving as the settlement grows, which is exactly the shape the catalogue asks for -
"Food is the good that notices staffing. It is meant to be." A ruined reservoir costs a city
its cushion; a ruined home farm costs it dinners inside the month.

That crossing is not an accident of the numbers, it is the two bounds meeting. Foraging is
flat ({SRC["MaxForagedRationsPerDay"]} a day forever) so it is everything at a Camp and
nothing at a City, while water storage climbs faster than the water bill does because the
grand water designs are stores as much as makers. Food has no grand STORE - the granary is a
middling plot and the ladder stops there - so a city keeps its bread in several of the same
building rather than in one big one.

What bounds the IMMEDIATE hunger ladder is the RESOLVE, not the number of days one resolve
bills. A long span with no attended claimed ground advances it once when physical stores next
reconcile; while the founder remains on claimed ground, the stationary scheduler supplies one
resolve at each absolute daily boundary. Either way one resolve removes at most one settler
and `Emigrate` floors at {SRC["LoyalCoreSettlers"]}. Subsidence remains the independent
structural clock and can cash multiple elapsed steps in that same homecoming pass.
""")

    rule("Q11c The two ladders together: the composition rule, exhaustively")
    print(f"""
Both ladders run. Each keeps its own streak ({SRC["DryIntervalsToEmigrate"]} failed resolves to
a departure, {SRC["DryIntervalsToWither"]} to a mark, and the food ladder is the same shape at
{SRC["HungryIntervalsToEmigrate"]} and {SRC["HungryIntervalsToFamine"]}), each says its own
sentence, each sets its own mark. `KingdomRules.ComposeScarcity` decides what the resolve
actually costs, and the rule is:

    THE BITE IS THE WORSE OF THE TWO LADDERS, NEVER THEIR SUM.

Below is every pairing, with the departures each ladder would cost alone and what the pair
actually costs. The property that must hold in every row is `both <= max(alone, alone)`, which
is the "no death spiral" requirement: a dry AND starving city must never empty faster than the
worse of the two alone would.
""")
    thirsts = ["Sustained", "Warned", "Emigration", "Withering"]
    hungers = ["Fed", "Warned", "Emigration", "Famine"]
    print(f"  {'thirst':<12}{'hunger':<12}{'alone':>7}{'alone':>7}{'together':>10}   marks")
    worst = 0
    for t in thirsts:
        for h in hungers:
            bite = compose_scarcity(t, h)
            a, b = departures_of(_BITE[t]), departures_of(_BITE[h])
            both = departures_of(bite)
            assert both <= max(a, b), f"COMPOSITION BROKEN at {t}/{h}: {both} > {max(a, b)}"
            worst = max(worst, both)
            marks = []
            if t == "Withering":
                marks.append("withered")
            if h == "Famine":
                marks.append("famished")
            print(
                f"  {t:<12}{h:<12}{a:>7}{b:>7}{both:>10}   {' + '.join(marks) if marks else '-'}"
            )
    print(f"""
Sixteen rows, and the most any single resolve costs is {worst} settler. The bottom-right row is
the one the rule exists for: a city that is Withering AND Famishing loses ONE person and wears
BOTH marks - because a mark is a state and a departure is a cost, and only the cost is capped.

WHAT IS DELIBERATELY NOT COMPOSED. Subsidence runs underneath both of these and is not touched
by either. It is the STRUCTURAL consequence - a settlement standing above what its works carry
settles back toward them, on its own {SRC["StepDays"]}-day step - and this is the IMMEDIATE
one. A bad year is both sentences about the same year rather than one sentence counted twice:
the fields fail, people go hungry NOW and leave one at a time, and over the season the place
settles to the size the surviving fields honestly carry. The two are already composed in the
only way that matters, which is that the hunger ladder can never remove a settler the slide
was going to remove anyway - both go through `Emigrate`, both floor at
{SRC["LoyalCoreSettlers"]}, and neither mints a departure the population cannot pay for.
""")

# --------------------------------------------------------------------------------------
# 7. The water lane's own invariants (Wave G1, Addendum 11(a)).
#
# Three claims the catalogue makes about itself, checked against the source rather than
# asserted in prose. Every one of them FAILS THE RUN if it stops being true, because all
# three are things a later tuning pass could break silently.
# --------------------------------------------------------------------------------------


def _producer_rates() -> dict:
    """Blueprint -> mean LiquidProducer VariableRate, resolved through Inherits.

    A design's `Carries="water:N"` is not an authored opinion: it is 1200 / mean(rate) of the
    LiquidProducer standing on its own blueprint. Several producers inherit their part from a
    smaller rung and override only the rate, so the lookup has to walk the chain the way the
    engine's blueprint loader does.
    """
    text = open(BLUEPRINTS_XML, encoding="utf-8-sig").read()
    own, parent = {}, {}
    for block in re.split(r"<object\s+", text)[1:]:
        name = re.match(r'Name="([^"]+)"', block)
        if not name:
            continue
        inherits = re.match(r'Name="[^"]+"\s+Inherits="([^"]+)"', block)
        if inherits:
            parent[name.group(1)] = inherits.group(1)
        rate = re.search(
            r'<part\s+Name="LiquidProducer"[^>]*VariableRate="(\d+)-(\d+)"', block
        )
        if rate:
            own[name.group(1)] = (int(rate.group(1)) + int(rate.group(2))) / 2.0
    out = {}
    for name in own:
        out[name] = own[name]
    for name in list(parent):
        seen, walk = set(), name
        while walk and walk not in own and walk not in seen:
            seen.add(walk)
            walk = parent.get(walk)
        if walk in own:
            out[name] = own[walk]
    return out


def water_invariants():
    rule("G1  The water lane's invariants, re-derived from the XML")
    rates = _producer_rates()
    blueprints = {}
    for attrs in re.findall(r"<building\s+(.*?)/?>", open(BUILD_XML, encoding="utf-8-sig").read(), re.S):
        key = re.search(r'Key="([^"]+)"', attrs)
        bp = re.search(r'\sBlueprint="([^"]+)"', attrs)
        if key and bp:
            blueprints[key.group(1)] = bp.group(1)

    print(f"""
1. EVERY DECLARED DRAM IS A PART'S DRAM. `LiquidProducer.Rate` is turns per dram and
   `KingdomRules.TicksPerDay` is {SRC["TicksPerDay"]}, so a producer's honest daily output is
   {SRC["TicksPerDay"]} / mean(VariableRate). Addendum 11(a) asks that the economy number be defensible
   against what the part visibly does; this is that check, and it re-derives every water
   design's `Carries` from the blueprint rather than trusting the catalogue's own comment.
""")
    print(f"  {'design':<16}{'blueprint':<26}{'mean rate':>10}{'derived':>9}{'declared':>10}")
    producers = [d for d in CATALOGUE if "water" in d.carries]
    assert producers, "no design carries water at all"
    for d in sorted(producers, key=lambda d: d.carries["water"]):
        bp = blueprints[d.key]
        mean = rates.get(bp)
        assert mean, (
            f"{d.key} declares water:{d.carries['water']} but its blueprint {bp} carries no "
            "LiquidProducer VariableRate. A design that claims water must SHOW the water."
        )
        derived = int(SRC["TicksPerDay"] / mean)
        assert derived == d.carries["water"], (
            f"WATER CLAIM UNEARNED: {d.key} declares water:{d.carries['water']} but {bp}'s "
            f"LiquidProducer averages {mean} turns a dram, which is {derived} a day."
        )
        print(
            f"  {d.key:<16}{bp:<26}{mean:>10.0f}{derived:>9}{d.carries['water']:>10}"
        )

    print("""
2. NO STORE CONJURES. A design whose blueprint has a LiquidVolume and no LiquidProducer is a
   vessel, and a vessel may not declare `water` - that is the whole of Addendum 11(a)'s
   "storage stores; producers produce", and it is enforceable exactly because `Carries` is
   read as a FLOW as well as a level.
""")
    vessels = [
        d
        for d in CATALOGUE
        if d.capacity > 0 and blueprints.get(d.key) not in rates
    ]
    for d in sorted(vessels, key=lambda d: d.capacity):
        assert "water" not in d.carries, (
            f"STORE CONJURES: {d.key} holds {d.capacity} drams and declares "
            f"water:{d.carries['water']} without a producer on its blueprint."
        )
        print(f"  {d.key:<16}{blueprints[d.key]:<26}{d.capacity:>10} drams, carries no water")

    print("""
3. THE CAMP COSTS WATER, AND EVERY RUNG ABOVE IT IS STILL HOLDABLE. Addendum 11(a) puts
   production up the tree, so nothing in the water lane may be reachable at Camp; and the
   price of that ruling is that every rung from Steading up must still be holdable by both a
   cheap plan and a grand one, or the lane has simply been made impossible.
""")
    camp = [d for d in CATALOGUE if "water" in d.carries and d.stage == 0]
    assert not camp, (
        "CAMP CAN PRODUCE: " + ", ".join(d.key for d in camp) + " opens at Camp. Addendum "
        "11(a) puts every water producer behind resources, technology or effort."
    )
    print(f"  Camp     water designs reachable: {len(camp)} - the founder's stock, the detail, and a charter")
    for i, (name, floor, _cap) in enumerate(STAGES):
        if i == 0:
            continue
        need = math.ceil(floor * STAGE_PERCENT[i] / 100)
        for label, by in (
            ("cheapest", lambda d: (d.cost / max(d.carries.get("water", 1), 1), -d.carries.get("water", 0))),
            ("grandest", lambda d: -d.carries.get("water", 0)),
        ):
            globals()["_KIND"] = "water"
            got = _plan("water", i, need, by)
            assert got, f"RUNG IMPOSSIBLE: no water design is reachable at {name}"
            design, count = got
            made = design.carries["water"] * count
            assert level_from_water(made, i) >= floor, (
                f"RUNG IMPOSSIBLE: {name}'s {label} water plan ({design.key} x{count}) makes "
                f"{made} drams, which carries {level_from_water(made, i)} against a floor of {floor}."
            )
        print(
            f"  {name:<9}bill {need:>3} drams/day; cheapest and grandest plans both clear the "
            f"floor of {floor}"
        )

    print(f"""
4. AND THE FLOOR IS STILL A FLOOR. `KingdomCatalogueRules.Equilibrium` returns at least
   `FloorLevel` = {SRC["FloorLevel"]} whatever it is handed, so a settlement that raises nothing, loses
   everything, and produces not one dram still settles to a camp of {SRC["FloorLevel"]} rather than to zero.
   Removing production from Camp changes what a camp must DO; it cannot change what a camp IS.
""")
    assert equilibrium(0, 0, 0, 0, 0) == SRC["FloorLevel"], "the camp floor moved"
    assert equilibrium(0, 0, 0, 0, 4) == SRC["FloorLevel"], "the camp floor moved at City rates"
    print(f"  equilibrium(0, 0, 0) at every stage = {SRC['FloorLevel']}  (checked)")


# --------------------------------------------------------------------------------------
# 8. The food lane's own invariants (Wave G2, Addendum 11(b) and 11(b-ii)).
#
# The exact sibling of section 7, and it exists for the same reason: a `Carries` number that
# nobody can derive from what the object visibly DOES is an author's opinion wearing an
# economy's clothes. Water derives from a LiquidProducer's rate. Food now derives from a
# field's rows.
# --------------------------------------------------------------------------------------


def food_invariants():
    rule("G2  The food lane's invariants, re-derived from the XML")
    rows = _crop_rows()
    blueprints = {}
    for attrs in re.findall(r"<building\s+(.*?)/?>", open(BUILD_XML, encoding="utf-8-sig").read(), re.S):
        key = re.search(r'Key="([^"]+)"', attrs)
        bp = re.search(r'\sBlueprint="([^"]+)"', attrs)
        if key and bp:
            blueprints[key.group(1)] = bp.group(1)

    print(f"""
1. EVERY DECLARED SERVING IS A ROW'S SERVING. A crop stands {SRC["CropDays"]} days
   (`KingdomCropRules.CropDays`) and one row yields {SRC["YieldPerRow"]} servings when it is
   gathered (`YieldPerRow`), so a design standing R rows honestly makes
   R x {SRC["YieldPerRow"]} / {SRC["CropDays"]} servings a day. Addendum 11(b) asks that the farm
   BE the thing it is counted for; this re-derives every growing design's `Carries="food:N"` from
   the `r_KingdomCropRows` tag on its own blueprint rather than trusting the catalogue's comment.
""")
    print(f"  {'design':<12}{'blueprint':<24}{'rows':>6}{'derived':>9}{'declared':>10}   plot cells")
    cells = {"S": 5 * 4, "M": 8 * 6, "L": 12 * 9, "XL": 20 * 14}
    growers = [d for d in CATALOGUE if d.rows > 0]
    assert growers, "no design grows anything at all"
    for d in sorted(growers, key=lambda d: d.rows):
        derived = d.rows * SRC["YieldPerRow"] // SRC["CropDays"]
        declared = d.carries.get("food", 0)
        assert derived == declared, (
            f"FOOD CLAIM UNEARNED: {d.key} declares food:{declared} but {blueprints[d.key]} "
            f"stands {d.rows} rows, which is {derived} servings a day."
        )
        # The rows have to physically fit in the ground the design occupies, or the harvest is
        # quietly smaller than the level it was counted for. KingdomPlotRules' tier dimensions.
        room = cells.get(d.plot, 0)
        assert room >= d.rows, (
            f"FIELD OVERSOWN: {d.key} wants {d.rows} rows on a {d.plot} plot of {room} cells."
        )
        print(
            f"  {d.key:<12}{blueprints[d.key]:<24}{d.rows:>6}{derived:>9}{declared:>10}"
            f"   {d.rows}/{room} ({d.rows / room:.0%})"
        )

    print("""
2. NOTHING CARRIES FOOD IT NEITHER GROWS NOR KEEPS. The food half of "storage stores, producers
   produce". A design may declare `food` for exactly three reasons, and each is checkable off its
   own blueprint: it GROWS (a rows tag), it KEEPS (a larder-capacity tag - a granary makes a good
   year last, which is a real contribution and a cheaper one than growing), or it MAKES something
   that keeps out of what came in (it carries `craft` beside its `food`, which is the mill). A
   fourth reason would be a number nobody can derive.
""")
    for d in sorted((d for d in CATALOGUE if "food" in d.carries), key=lambda d: d.carries["food"]):
        if d.rows > 0:
            why = f"grows ({d.rows} rows)"
        elif d.larder > 0:
            why = f"keeps ({d.larder} servings)"
        elif "craft" in d.carries:
            why = "makes (a mill, and it carries craft to say so)"
        else:
            raise AssertionError(
                f"FOOD FROM NOWHERE: {d.key} declares food:{d.carries['food']} and its blueprint "
                f"{blueprints[d.key]} neither grows rows, holds servings, nor works a craft."
            )
        print(f"  {d.key:<12}food:{d.carries['food']:<4}{why}")

    print(f"""
3. THE SEED GATE DOES NOT WALL THE EARLY RUNGS. Addendum 11(b) makes a farm produce nothing
   until seed is committed, so an unsown field carries no food to the level and makes none in a
   day (`KingdomCrops.WithoutUnsownFood`, folded inside `KingdomSubsidence.Supports`). The price
   of that ruling would be unacceptable if a founder could be starved while hunting for seed - so
   the check is that the two rungs a founder reaches BEFORE they can plausibly have traded for any
   are held by the wild PLUS the designs that need no seed at all. Nothing that grows counts here;
   only foraging (unchanged by this wave) and the keepers, which make a good year last rather than
   making a year.
""")
    seedless = [d for d in CATALOGUE if "food" in d.carries and d.rows == 0]
    for i, (name, floor, _cap) in enumerate(STAGES[:2]):
        pop = max(floor, SRC["FloorLevel"])
        crew = min(pop, math.ceil(pop * STAGE_PERCENT[i] / 100 / SRC["FetchDramsPerSettler"]))
        free = max(0, pop - crew)
        forage = foraged_rations(free, 1)
        eats = rations_per_day(pop)
        gap = eats - forage
        # Cheapest per serving and then fewest hands: a founder who has not found seed yet has
        # not found much of anything, and a plan that needs a crew takes the very hands that are
        # out foraging. Picking the GRANDEST seedless design would flatter the check.
        reach = sorted(
            (d for d in seedless if d.stage <= i),
            key=lambda d: (d.cost / d.carries["food"], d.staff),
        )
        best = reach[0] if reach else None
        count = 0 if (gap <= 0 or best is None) else math.ceil(gap / best.carries["food"])
        made = 0 if best is None else best.carries["food"] * count
        forage = foraged_rations(max(0, free - (0 if best is None else best.staff * count)), 1)
        assert forage + made >= eats, (
            f"SEED GATE WALLS {name}: {pop} people eat {eats} a day, the wild gives {forage} with "
            f"every spare hand on it, and no seedless design closes the rest. A founder with no "
            "seed yet would starve."
        )
        plan = "the wild alone" if count == 0 else f"{best.key}x{count} (needs no seed) for {made}"
        print(f"  {name:<9}{pop} people eat {eats}/d; {free} hands forage {forage}/d; {plan}")
    print(f"""
  Foraging is UNCHANGED by this wave: {SRC["ForageRationsPerHand"]} a hand a day, ceiling
  {SRC["MaxForagedRationsPerDay"]}, and the ceiling is still `FloorLevel`. The gate binds where it
  should - a Village and up must actually farm - and nowhere a founder cannot answer it.

4. THE CYCLE PAYS WHAT THE CARRIES PROMISED, OVER A WHOLE CYCLE. The runtime does not add the
   two: a SOWN field's `food` is subtracted from the clocked daily make
   (`KingdomGrowth.FoodMadePerDay` less `KingdomCrops.CycledFoodPerDay`) and delivered physically
   by the gathering instead, so one field feeds the settlement exactly once. Below is that
   identity for every growing design: what the ledger would have credited over one crop's days
   against what the rows actually put in the larder.
""")
    print(f"  {'design':<12}{'rows':>6}{'carries/d':>11}{'over ' + str(SRC['CropDays']) + 'd':>10}{'one gathering':>15}")
    for d in sorted(growers, key=lambda d: d.rows):
        flow = d.carries["food"] * SRC["CropDays"]
        gathered = d.rows * SRC["YieldPerRow"]
        assert flow == gathered, (
            f"CYCLE DOES NOT PAY: {d.key} carries {d.carries['food']}/day = {flow} over "
            f"{SRC['CropDays']} days, but one gathering of {d.rows} rows is {gathered}."
        )
        print(f"  {d.key:<12}{d.rows:>6}{d.carries['food']:>11}{flow:>10}{gathered:>15}")

    print(f"""
5. AND EVERY RUNG IS STILL FOOD-HOLDABLE, WITH THE GATE ON. Section 7's water sibling asks
   whether a cheap plan and a grand one both clear the floor; this asks the same of food, with
   every field counted only if it is sown - which is what it is, because a founder who has
   committed seed is the only founder whose fields are in this plan at all.
""")
    for i, (name, floor, _cap) in enumerate(STAGES):
        pop = max(floor, SRC["FloorLevel"])
        eats = rations_per_day(pop)
        for label, by in (
            (
                "cheapest",
                lambda d: (d.cost / max(d.carries.get("food", 1), 1), -d.carries.get("food", 0)),
            ),
            ("grandest", lambda d: -d.carries.get("food", 0)),
        ):
            got = _food_plan(i, eats, by)
            assert got, f"RUNG IMPOSSIBLE: no food design is reachable at {name}"
            design, count = got
            made = design.carries["food"] * count
            crew = _passive_water_crew(i, pop)
            free = max(0, pop - crew - design.staff * count)
            assert made + foraged_rations(free, 1) >= eats, (
                f"RUNG IMPOSSIBLE: {name}'s {label} food plan ({design.key} x{count}) makes "
                f"{made} against a bill of {eats}, and the wild does not close it."
            )
        print(f"  {name:<9}bill {eats:>3} servings/day; cheapest and grandest plans both clear it")


def _switch_map(path: str | tuple[str, ...], function: str) -> dict:
    """The `case "X": return "Y";` pairs of one C# switch, read straight out of the source.

    Every derivation table printed below is read this way rather than restated here, for the
    reason every other number in this file is read out of the source: a table copied into the
    model is a table that drifts from the code the first time somebody retunes one end of it.
    """
    text = read_source(path)
    start = text.find("public static string " + function)
    if start < 0:
        raise SystemExit(f"{function} not found in {source_label(path)}")
    body = text[start : text.find("\n\t\t}", start)]
    out = {}
    for m in re.finditer(r'case\s+"([^"]*)":\s*\n\s*return\s+"([^"]*)";', body):
        out[m.group(1)] = m.group(2)
    return out


def _blueprint_blocks() -> dict:
    """Blueprint name -> its raw XML block, for asking whether a machine really carries a part."""
    text = open(BLUEPRINTS_XML, encoding="utf-8-sig").read()
    out = {}
    for block in re.split(r"<object\s+", text)[1:]:
        name = re.match(r'Name="([^"]+)"', block)
        if name:
            out[name.group(1)] = block
    return out


def meals_and_industry():
    rule("G3  Meals and industry, re-derived from the source and the XML")

    forms = _switch_map(RULES_CS, "DishFormFor")
    staples = _switch_map(RULES_CS, "PreservedStapleFor")
    crops = _switch_map(CROP_CS, "CropBlueprintForStyle")
    words = _switch_map(RULES_CS, "CropWordFor")
    blocks = _blueprint_blocks()
    growth = read_source(source_family_paths("Growth", "KingdomGrowth"))

    print(f"""
1. THE DISH IS DERIVED, NOT AUTHORED, AND IT IS TOTAL. Addendum 11(b) asks that residents eat
   FAVOURED MEALS. Vanilla's own home for that is the faction: `<waterritual Recipe=... RecipeText=...
   RecipeGenotype=.../>` parses onto `Faction.WaterRitualRecipe` and friends, which the Faction's
   own serializer writes and reads - so the runtime faction this mod already mints carries its
   favourite dish across save and load with no persistence of ours. Eight vanilla factions ship
   one; the realm makes nine.

   The derivation is two switches and a join: the CREED picks the form (borrowed from that
   faction's own dish), the GROUND picks the body (the crop the style grows), and every form word
   is one of `CookingRecipe.ingredientTileTypes` so vanilla's recipe-tile generator draws it.
""")
    tile_words = {
        "cake", "bread", "loaf", "slaw", "stew", "soup", "brisket", "borscht", "dip", "baklava",
        "compote", "hash", "porridge", "matz", "cookies", "yogurt", "goulash", "rice", "hummus",
        "knish", "broth", "kugel", "latkes", "schnitzel", "pancake", "roast", "shawarma",
        "flatbread", "meatballs", "pastry", "casserole", "dumpling", "doughnut", "tajine",
        "couscous", "dolma", "kebab", "fillet",
    }
    assert forms, "DishFormFor has no cases; the dish would be the same everywhere"
    print(f"  {'creed dish':<20}{'form':<12}vanilla tile word?")
    for creed, form in sorted(forms.items()):
        assert form in tile_words, (
            f"DISH FORM UNDRAWABLE: '{form}' is not one of CookingRecipe.ingredientTileTypes, so a "
            f"settlement whose people hold with {creed} would get a defaulted picture."
        )
        print(f"  {creed:<20}{form:<12}yes")
    default_form = "stew"
    assert default_form in tile_words
    print(f"  {'(nobody / unknown)':<20}{default_form:<12}yes")

    print(f"""
   And the body, per founding ground. Every style must reach a staple, or a settlement founded
   on that ground would raise a mill that grinds nothing while carrying a food number for it.
""")
    print(f"  {'style':<10}{'crop':<18}{'ingredient word':<18}{'preserved staple':<26}source")
    for style in ("common", "verdant", "fungal", "gyre", "eater"):
        crop = crops.get(style, crops.get("default", "Starapple"))
        staple = staples.get(crop)
        assert staple, f"NO STAPLE: style '{style}' grows {crop}, which nothing can bind to keep."
        ours = staple.startswith("r_Kingdom")
        if ours:
            assert staple in blocks, f"staple {staple} is not a blueprint this mod ships"
            inherits = re.match(r'Name="[^"]+"\s+Inherits="([^"]+)"', blocks[staple])
            assert inherits and not inherits.group(1).startswith("r_Kingdom"), (
                f"FILL-IN NOT GROUNDED: {staple} must inherit a shipped vanilla preserve, so it "
                "owes no art and needs no new cooking-ingredient plumbing."
            )
            source = f"ours, inherits {inherits.group(1)}"
        else:
            source = "vanilla PreservableItem"
        print(f"  {style:<10}{crop:<18}{words.get(crop, crop.lower()):<18}{staple:<26}{source}")
    print(f"""
   Two worked examples, in the register the rest of the game writes recipes in:
     - people who hold with Joppa (`AppleMatz`), founded in a marsh  ->  {words['Vinewafer']} {forms['AppleMatz']}
     - people who hold with the Barathrumites (`ThePorridge`), on flower fields  ->  {words['Starapple']} {forms['ThePorridge']}

2. THE MEAL IS A RENDERING OF THE RATION, NOT A SECOND BILL. The daily draw spends the same
   servings it always did; what changed is the ORDER it reaches in and what the day is worth
   afterwards. The order, stated once and deterministic:

     1. the settlement's own staple, larder by larder in survey order, item by item in inventory
        order - the thing the fields grew and the mill bound, which is the dish's first component;
     2. everything else that is food, same walk.

   Nothing is random, so the same larders drained in the same sequence give the same answer on
   every reload - Addendum 12(d)'s requirement of any draw that lands on real containers.

   A day counts as the settlement having eaten its own dish when a kitchen stands and at least
   {SRC["FavoredMealPercent"]}% of the bill came off that staple. "Kitchen" is asked of the OBJECT: any finished work
   carrying vanilla's `Campfire`, which the communal fire has done since the day it shipped.
""")
    fire = [d for d in CATALOGUE if d.key == "fire"]
    assert fire, "the communal fire has left the catalogue; the camp has nowhere to cook"
    assert "Campfire" in open(BUILD_XML, encoding="utf-8-sig").read(), "the fire no longer commissions vanilla Campfire"
    assert "r_KingdomOven" in blocks, "no settlement oven ships"
    assert re.search(r'<part\s+Name="Campfire"[^>]*PresetMeals="r_KingdomFavoredDish"', blocks["r_KingdomOven"]), (
        "THE OVEN COOKS NOTHING: r_KingdomOven must carry Campfire with PresetMeals naming the "
        "realm's dish, which is exactly how every named settlement's oven in vanilla works."
    )
    assert 'Inherits="Oven"' in blocks["r_KingdomOven"], "the oven must extend vanilla's Oven, not re-implement it"

    print(f"""
3. WHAT A FAVOURED MEAL IS WORTH, AND WHY IT CANNOT RUN AWAY. One settler, for exactly one day,
   re-earned every day - which is vanilla's own arithmetic, not a dial: a non-player eater's meal
   effect expires at StartTick + 1200 ticks (`ProceduralCookingEffect`), and `KingdomRules.TicksPerDay`
   is {SRC["TicksPerDay"]}. It rides the same capped lift term as authored spirit works, so
   `LiftCapPercent` = {SRC["LiftCapPercent"]}% binds it again on top of that.

   Below: the level a settlement holds at each rung with its binding supports level-pegged and no
   other lift standing, plain and then well fed. The delta is the whole of what this wave adds to
   the level, and it is one settler wherever the cap has room for it.
""")
    print(f"  {'rung':<10}{'binding least':>14}{'plain':>8}{'well fed':>10}{'delta':>7}   capped by")
    for i, (name, floor, _cap) in enumerate(STAGES):
        least = max(floor, SRC["FloorLevel"])
        water = least * STAGE_PERCENT[i] // 100 if STAGE_PERCENT[i] > 100 else least
        base = equilibrium(water, least, least, 0, i, 0)
        with_meal = equilibrium(water, least, least, 0, i, SRC["FavoredMealShade"])
        cap = max(0, min(level_from_water(water, i), least, least)) * SRC["LiftCapPercent"] // 100
        assert with_meal - base <= SRC["FavoredMealShade"], "a meal cannot be worth more than its shade"
        assert with_meal >= base, "a meal is never a penalty"
        why = "the lift cap" if cap <= SRC["FavoredMealShade"] else "nothing (room to spare)"
        print(f"  {name:<10}{least:>14}{base:>8}{with_meal:>10}{with_meal - base:>7}   {why}")

    print(f"""
4. THE MILL CONSERVES. Addendum 11(b)'s other half is food "used by industry to produce things",
   and per the survey the entire transformation surface in Qud is four parts. The one that fits a
   harvest is `Mill`, whose blank-target path runs `Campfire.PerformPreserve` - which is what
   vanilla's own `Millstone` does: a vinewafer becomes three vinewafer sheaves.

   Our mill books that same ratio, flat across styles for the same reason `CropDaysForStyle` is
   flat. The conservation law is one line and it is asserted here against the catalogue itself:
   what comes back is what went in TIMES the multiple, and the GAIN is the difference.
""")
    grind = [d for d in CATALOGUE if d.key == "grindmill"]
    assert grind, "the grinding mill has left the catalogue"
    grind = grind[0]
    declared = grind.carries.get("food", 0)
    crops_in = SRC["MillCropsPerDay"]
    out_units = crops_in * SRC["PreserveMultiple"]
    gain = out_units - crops_in
    assert gain == declared, (
        f"MILL DOES NOT PAY: {crops_in} crops at x{SRC['PreserveMultiple']} is {out_units} staples back, a net of "
        f"{gain}, but the grinding mill declares food:{declared}."
    )
    assert SRC["PreserveMultiple"] >= 1, "a mill that returns less than it takes is a bonfire"
    print(f"  {'in (crops/day)':<18}{'multiple':>10}{'out (staples)':>15}{'net gain':>10}{'declared food':>15}")
    print(f"  {crops_in:<18}{'x' + str(SRC['PreserveMultiple']):>10}{out_units:>15}{gain:>10}{declared:>15}")
    print(f"""
   x{SRC["PreserveMultiple"]} is the LEAST of the three vanilla numbers this mod's crops carry (starapple gives five,
   plump mushroom ten), so the settlement never books more than the thinnest preserve in the game
   actually gives. The physical machine is real either way: r_KingdomGrindMill carries `Mill`,
   `Container`, `Inventory` and a mechanical-power consumer, which is `Millstone`'s own
   configuration at `Millstone`'s own tier.
""")
    for part in ("Mill", "Container", "Inventory", "MechanicalPowerTransmission"):
        assert f'Name="{part}"' in blocks.get("r_KingdomGrindMill", ""), (
            f"THE MILL IS STILL A GLYPH: r_KingdomGrindMill declares no {part}, so its food:"
            f"{declared} is an assertion rather than a machine."
        )
    assert 'IsConsumer="true"' in blocks["r_KingdomGrindMill"], (
        "the mill must CONSUME mechanical power - it is the first consumer on a grid that had "
        "three producers and nothing to drive."
    )

    print(f"""
5. AND THE MILL IS PAID EXACTLY ONCE, AFTER THE RESIDENTS HAVE EATEN. Two source facts, both
   checked here rather than trusted:

     - the mill's `Carries` is SUBTRACTED from the clocked daily make, exactly as a sown field's
       is, because it now delivers its food physically instead of as a ledger credit. Without the
       subtraction a settlement would be fed twice out of one millstone.
     - the grinding runs AFTER the heartbeat has drawn the day's rations, and even then only on
       what stands above one more day's bill (`MillableStock`). Industry never eats before the
       residents do.
""")
    assert "KingdomCrops.MilledFoodPerDay(Survey)" in growth, (
        "FED TWICE: FoodMadePerDay no longer subtracts what the mills deliver physically."
    )
    order_rations = growth.find("bool heartbeatHealthy = ResolveHeartbeat(")
    order_mill = growth.find("GrindHarvest(System, survey, grownDays)")
    assert order_rations > 0 and order_mill > 0, "the pass no longer draws rations or grinds"
    assert order_mill > order_rations, (
        "INDUSTRY EATS FIRST: GrindHarvest runs before ResolveHeartbeat in the settlement pass. "
        "The residents' day must be drawn before the mill touches the larders."
    )
    print("  subtraction present: yes    grinding runs after the ration draw: yes")
    print(f"  reserve kept back:  one day's rations for the whole population, on top of that")


def v1_authority_capacity():
    """Static source-plus-retirement headroom for the activated experience authorities.

    This is not native save-size evidence. It places every independently declared active-source
    maximum beside the UTF-8 content C2 can add to the native Journal before the TAF carrier is
    cut, then charges both against the same 4 MiB comparison budget without compression. Native
    serialization has its own token table and framing, so every generated note also pays a
    deliberately conservative 128-byte framing allowance. Real save p50/p95/max remain unsigned.
    """
    rule("C4  Activated v1 authority capacity and concurrency boundaries")
    limit_names = (
        ("witness + recognition", "MaxCivicArtifactsBytes"),
        ("practice + services", "MaxCivicPracticeBytes"),
        ("body history", "MaxBodyHistoryBytes"),
        ("curiosity", "MaxCuriosityBytes"),
        ("civic leads", "MaxCivicLeadsBytes"),
        ("treaties", "MaxTreatyBytes"),
        ("communal rite", "MaxCommunalRiteBytes"),
        ("Guest's Feast", "MaxGuestFeastBytes"),
        ("village covenant", "MaxVillageCovenantBytes"),
    )
    sections = tuple(
        (name, read_const(CIVIC_MEMORY_LIMITS_CS, constant))
        for name, constant in limit_names
    )
    limits = read_source(CIVIC_MEMORY_LIMITS_CS)
    derivation = read_source(CIVIC_MEMORY_DERIVATION_CS)
    for _name, constant in limit_names:
        assert f"KingdomCivicMemoryLimits.{constant}" in derivation, (
            f"civic-memory derivation pin missing for {constant}"
        )
    assert "MaxSections = KnownSectionCount * 2" in limits
    assert "MaxEnvelopeBytes = EnvelopeOverheadBytes" in limits
    assert "+ MaxSections * SectionFramingBytes + MaxCumulativePayloadBytes" in limits

    known_sections = read_const(CIVIC_MEMORY_LIMITS_CS, "SectionVillageCovenant")
    envelope_overhead = read_const(CIVIC_MEMORY_LIMITS_CS, "EnvelopeOverheadBytes")
    section_framing = read_const(CIVIC_MEMORY_LIMITS_CS, "SectionFramingBytes")
    max_sections = known_sections * 2
    civic_payload = sum(size for _name, size in sections)
    civic_envelope = envelope_overhead + max_sections * section_framing + civic_payload

    experience_bytes = 24_576
    experience_codec = read_source(
        os.path.join(ROOT, "Experience", "KingdomExperienceCodec.cs")
    )
    assert "MaxEnvelopeBytes = 24 * 1024" in experience_codec

    experience_rules_path = source_family_paths("Experience", "KingdomExperienceRules")
    experience = read_source(experience_rules_path)
    for token in (
        "MaxSettlements = 3",
        "MaxTransientBodySlots = 16",
        "MaxBodiesPerReservation = 7",
        "MaxFirstFeastReceipts = MaxSettlements",
    ):
        assert token in experience, f"experience concurrency pin moved: {token}"
    count_pins = (
        ("Experience/KingdomWitnessWorkRules.cs", "MaxRows = 8"),
        ("Core/KingdomArtifactRecognitionRules.cs", "MaxRows = 8"),
        ("Core/KingdomSitePracticeRules.cs", "MaxRows = 8"),
        ("Core/KingdomVocationServiceRules.cs", "public const int MaxRows = 48;"),
        ("Core/KingdomBodyHistoryRules.cs", "MaxRows = 8"),
        ("Experience/KingdomCuriosityModels.cs", "MaxRows = 3"),
        ("Experience/KingdomCivicLeadModels.cs", "MaxRows = 8"),
        ("Treaty/KingdomTreatyModels.cs", "MaxPacts=16"),
        ("Experience/KingdomCommunalRiteRules.cs", "MaxRows = KingdomExperienceRules.MaxSettlements"),
        ("Experience/KingdomGuestFeastRules.cs", "MaxRows = KingdomExperienceRules.MaxSettlements"),
        ("Core/KingdomVillageCovenantModels.cs", "MaxRows = 48"),
    )
    for relative, token in count_pins:
        source = open(os.path.join(ROOT, relative), encoding="utf-8-sig").read()
        assert token in source, f"authority count pin moved: {relative}: {token}"
    witness_codec = read_source(os.path.join(ROOT, "Experience", "KingdomWitnessWorkCodec.cs"))
    recognition_codec = read_source(os.path.join(ROOT, "Core", "KingdomArtifactRecognitionCodec.cs"))
    covenant_codec = read_source(os.path.join(ROOT, "Core", "KingdomVillageCovenantCodec.cs"))
    assert "MaxRowEncodedBytes = 4096" in witness_codec
    assert "MaxRowEncodedBytes = 4096" in recognition_codec
    assert "MaxAuthoredRowBytes" in covenant_codec and "MaxRowBytes = 4096" in covenant_codec
    shop = read_source(os.path.join(ROOT, "Growth", "KingdomShopStockRules.cs"))
    shop_runtime = read_source(os.path.join(ROOT, "Growth", "KingdomGrowth.z18.StageAndShops.cs"))
    assert "MaximumTier = 8" in shop
    assert re.search(r"\.Chance\s*=\s*0;", shop_runtime)
    assert re.search(r"\.RestockFrequency\s*=\s*long\.MaxValue;", shop_runtime)
    assert not re.search(r"\.Chance\s*=\s*100;", shop_runtime)

    # C2 preserves exactly one native General note per known C18 section. At every section cap,
    # strict base64 is ASCII, so character and UTF-8 byte counts are identical. The archive cap
    # is deliberately one widest-section cap, not an old per-note chunk size.
    retirement_c18 = read_source(os.path.join(
        ROOT, "Core", "KingdomRemovalProjectionRuntime.CivicMemory.cs"))
    retirement_semantics = read_source(os.path.join(
        ROOT, "Core", "KingdomRemovalProjectionRuntime.CivicSemantics.cs"))
    retirement_text = read_source(os.path.join(
        ROOT, "Core", "KingdomRemovalProjectionRuntime.CivicNoteText.cs"))
    assert "MaxCivicMemorySectionArchiveChars" in retirement_c18
    assert "((KingdomCivicMemoryLimits.MaxTreatyBytes + 2) / 3) * 4" in retirement_c18
    assert "digest, 0, 1, payload" in retirement_c18
    assert "CivicMemoryChunkChars" not in retirement_c18
    assert "JournalObservation" in retirement_c18
    assert "JournalObservation" in retirement_semantics
    assert "JournalAPI.Observations" in retirement_semantics
    assert "History = \"\"" in retirement_semantics
    assert "NativeArchiveAttribute(wire)" in retirement_semantics
    assert "NativeArchiveAttributePrefix" in retirement_semantics
    assert "TryPrepareExperienceNotes(System, Tick, notes" in retirement_semantics
    for collection in ("Offices", "Remembrances", "Voices", "FirstFeasts"):
        assert ("ledger." + collection + "[i], Notes") in retirement_text, (
            "experience native-note publication moved: " + collection)
    assert "FieldInfo[] fields = Row.GetType().GetFields" in retirement_semantics
    assert "KingdomPresentation.Rich" in retirement_text
    assert "Append(Reading).Append(\"\\nRecord seal: \"" in retirement_text

    section_base64 = tuple(((size + 2) // 3) * 4 for _name, size in sections)
    c18_archive_chars = sum(section_base64)
    max_section_archive_chars = max(section_base64)

    # Every current founding path caps a city name at 256 UTF-16 code units. Four strict UTF-8
    # bytes per code unit is deliberately wider than Unicode can lawfully require, and paying it
    # independently in every note avoids relying on the native writer's string-token reuse.
    founding = read_source(os.path.join(ROOT, "Core", "KingdomFoundingTransaction.10Begin.cs"))
    assert "Name.Length > 256" in founding
    seat_name_utf8_upper = 256 * 4
    rich_text_multiplier = 2
    escaped_seat_name_upper = seat_name_utf8_upper * rich_text_multiplier
    native_note_framing_bytes = 128
    digest_chars = 64

    # JournalObservation serializes one time, six strings, weight, three flags, and an attribute
    # list. 128 bytes per note covers their scalar/type/list/token framing plus five bytes of
    # string-length/token allowance for every field even at the widest four-attribute C18 note.
    # String CONTENT is charged separately below; NotesByID is rebuilt from Observations on load,
    # so it is not a second serialized copy.
    c18_note_metadata = 0
    for section_id in range(1, known_sections + 1):
        hidden_archive = (len("taf:retired-archive:v1:") + len("taf-c18-v1|")
            + len(str(section_id)) + len("|present|0|1|"))
        text_bytes = (len("Before ") + escaped_seat_name_upper
            + len(" retired its charter, civic-memory section ") + len(str(section_id))
            + len(" was preserved exactly (part 1 of 1)."))
        note_id = len("taf-c18-") + len(str(section_id)) + 1 + digest_chars + len("-0")
        fixed_fields = (len("Retired realms") + len("civic history")
            + len("civic memory") + len("retired realm") + len("exact archive"))
        c18_note_metadata += (hidden_archive + text_bytes + note_id + fixed_fields
            + native_note_framing_bytes)

    # C2 turns each current Experience civic row into one visible note. A private serialized
    # attribute holds the canonical exact row; Text holds separately rendered player-facing prose.
    # codec rather than guessing average prose: pay the whole declared row budget once as canonical
    # string content and twice more for formatting-escaped readable fields, then add every reflected
    # field label/length separator and widest scalar rendering. Binary row framing is double-counted.
    def class_fields(relative: str, class_name: str):
        source = read_source(os.path.join(ROOT, relative))
        marker = "public sealed class " + class_name
        start = source.find(marker)
        assert start >= 0, "experience retirement row class moved: " + class_name
        opening = source.find("{", start)
        depth = 0
        closing = -1
        for at in range(opening, len(source)):
            if source[at] == "{": depth += 1
            elif source[at] == "}":
                depth -= 1
                if depth == 0:
                    closing = at
                    break
        assert closing > opening, "experience retirement row class is malformed: " + class_name
        return tuple(re.findall(
            r"^\s*public\s+([A-Za-z_][A-Za-z0-9_]*)\s+([A-Za-z_][A-Za-z0-9_]*)"
            r"(?:\s*=.*)?;", source[opening + 1:closing], re.MULTILINE))

    enum_sources = "\n".join(read_source(os.path.join(ROOT, relative)) for relative in (
        "Experience/KingdomExperienceState.Civic.cs",
        "Experience/KingdomCivicVoiceModels.cs",
        "Experience/KingdomFirstFeastModels.cs",
    ))

    def enum_text_upper(enum_name: str) -> int:
        marker = "enum " + enum_name
        start = enum_sources.find(marker)
        assert start >= 0, "experience retirement enum moved: " + enum_name
        opening = enum_sources.find("{", start)
        closing = enum_sources.find("}", opening)
        names = re.findall(r"\b([A-Za-z_][A-Za-z0-9_]*)\s*=\s*[0-9]+",
            enum_sources[opening + 1:closing])
        assert names, "experience retirement enum is empty: " + enum_name
        return max(len(name) for name in names)

    scalar_text_upper = {"int": 11, "long": 20, "bool": 5}
    assert "MaxFaultTextBytes = 256" in experience
    assert "MaxCivicTextBytes = 96" in experience
    civic_voice = read_source(os.path.join(ROOT, "Experience", "KingdomCivicVoiceRules.cs"))
    assert "MaxFactsBytes = 384" in civic_voice
    presentation = read_source(os.path.join(ROOT, "Core", "KingdomPresentation.cs"))
    assert "return ColorUtility.EscapeFormatting(Plain ?? \"\");" in presentation
    settlement_rows = read_const(experience_rules_path, "MaxSettlements")
    voice_rows = read_const(os.path.join(
        ROOT, "Experience", "KingdomCivicVoiceRules.cs"), "MaxReceipts")
    readable_fixed = {
        "office": (len(" was remembered as a predecessor of the vacant civic office in ")
            + len(".") + len("an unnamed citizen") + len("an unnamed settlement")),
        "remembrance": (len(" had a ") + len(" in ") + len(", witnessed by ") + len(".")
            + len("lost but still recorded memorial") + len("An unnamed citizen")
            + len("an unnamed settlement") + len("an unnamed mourner")),
        "voice": (len(" and ") + len(" remembered the ") + len(" of ") + len(": ")
            + len(".") + len("creed declaration") + len("One citizen")
            + len("another citizen") + len("their settlement") + len("the exact ruling")),
        "first-feast": (len(" proposed ") + len(" in ") + len(" after ")
            + len("; the proposal was ") + len(".")
            + len("adapted as a private practice") + len("One citizen")
            + len("a shared dish") + len("an unnamed settlement")
            + len("a remembered deed")),
    }
    # Voice rows identify their city but do not duplicate its display name. Retirement resolves
    # that name from exact topology before rendering, so charge one separately escaped city name.
    readable_external = {"office": 0, "remembrance": 0,
        "voice": escaped_seat_name_upper, "first-feast": 0}
    family_specs = (
        ("office", settlement_rows, "CivicRowByteBudget",
            "Experience/KingdomExperienceState.Civic.cs", "KingdomCivicOfficeReceipt"),
        ("remembrance", settlement_rows, "CivicRowByteBudget",
            "Experience/KingdomExperienceState.Civic.cs", "KingdomRemembranceReceipt"),
        ("voice", voice_rows, "VoiceRowByteBudget",
            "Experience/KingdomCivicVoiceModels.cs", "KingdomCivicVoiceReceipt"),
        ("first-feast", settlement_rows, "FirstFeastRowByteBudget",
            "Experience/KingdomFirstFeastModels.cs", "KingdomFirstFeastReceipt"),
    )
    experience_native_rows = []
    for label, count, budget_name, relative, class_name in family_specs:
        row_budget = read_const(experience_rules_path, budget_name)
        fields = class_fields(relative, class_name)
        assert fields, "experience retirement row has no public fields: " + class_name
        scalar_bytes = 0
        for field_type, _field_name in fields:
            if field_type == "string": continue
            scalar_bytes += (scalar_text_upper[field_type]
                if field_type in scalar_text_upper else enum_text_upper(field_type))
        # Canonical writes Name=Length:Value and one U+001E separator. Three decimal digits cover
        # every <=384-byte string and every <=20-character scalar representation.
        canonical_format = sum(len(name) + 5 for _kind, name in fields) + len(fields) - 1
        canonical_upper = row_budget + scalar_bytes + canonical_format
        readable_upper = (row_budget * rich_text_multiplier
            + readable_fixed[label] + readable_external[label])
        note_text_fixed = (len("Before ")
            + len(" put away its charter, it preserved one exact civic memory.\n")
            + len("\nRecord seal: ") + 12 + len("."))
        note_fixed_fields = (len("taf-civic-memory-") + digest_chars
            + len("Retired realms") + len("civic history")
            + len("civic memory") + len("retired realm")
            + len("taf:retired-archive:v1:")
            + native_note_framing_bytes)
        per_note = (escaped_seat_name_upper + note_text_fixed + canonical_upper
            + readable_upper + note_fixed_fields)
        experience_native_rows.append((label, count, per_note, count * per_note))
    experience_native_notes = sum(total for _label, _count, _each, total
        in experience_native_rows)
    experience_note_count = sum(count for _label, count, _each, _total
        in experience_native_rows)

    active_source = experience_bytes + civic_envelope
    native_retirement = c18_archive_chars + c18_note_metadata + experience_native_notes
    total = active_source + native_retirement
    budget = 4 * 1024 * 1024
    headroom = budget - total
    assert civic_payload == 839_860
    assert civic_envelope == 840_048
    assert c18_archive_chars == 1_119_828
    assert max_section_archive_chars == 321_848
    assert c18_note_metadata == 22_077
    assert experience_note_count == 12
    assert experience_native_notes == 86_811
    assert native_retirement == 1_228_716
    assert active_source == 864_624
    assert total == 2_093_340
    assert headroom == 2_100_964
    assert total * 4 < budget * 3, "activated authorities consume more than 75% of budget"

    print("C18 section caps, deliberately filled together at every lawful maximum:")
    print(f"  {'authority':<24}{'bytes':>12}{'KiB':>12}")
    for name, size in sections:
        print(f"  {name:<24}{size:>12,}{size / 1024:>12.1f}")
    print(f"  {'C18 payload':<24}{civic_payload:>12,}{civic_payload / 1024:>12.1f}")
    print(f"  {'C18 framed envelope':<24}{civic_envelope:>12,}{civic_envelope / 1024:>12.1f}")
    print(f"  {'experience v4':<24}{experience_bytes:>12,}{experience_bytes / 1024:>12.1f}")
    print(f"  {'ACTIVE SOURCE':<24}{active_source:>12,}{active_source / 1024:>12.1f}")
    print("Native retirement archive, charged concurrently before carrier removal:")
    print("  C2 shape: MaxCivicMemorySectionArchiveChars = 321,848; one General note per")
    print("            known section; CivicMemoryChunkChars absent")
    print(f"  {'C18 base64 content':<24}{c18_archive_chars:>12,}{c18_archive_chars / 1024:>12.1f}")
    print(f"  {'C18 note metadata':<24}{c18_note_metadata:>12,}{c18_note_metadata / 1024:>12.1f}")
    for label, count, each, family_total in experience_native_rows:
        print(f"  {('experience ' + label):<24}{family_total:>12,}{family_total / 1024:>12.1f}"
            f"   ({count} notes x {each:,})")
    print(f"  {'experience note total':<24}{experience_native_notes:>12,}"
        f"{experience_native_notes / 1024:>12.1f}")
    print(f"  {'NATIVE RETIREMENT':<24}{native_retirement:>12,}{native_retirement / 1024:>12.1f}")
    print(f"  {'ACTIVE + RETIREMENT':<24}{total:>12,}{total / 1024:>12.1f}")
    print(f"  {'4 MiB contract headroom':<24}{headroom:>12,}{headroom / 1024:>12.1f}")
    print(f"  worst-lawful share: {100.0 * total / budget:.2f}%")
    print("  native notes: 9 C18 sections + 12 Experience civic rows = 21")
    print("  bound kind: uncompressed UTF-8 structural content + conservative per-note framing")
    print("  concurrency: 3 audiences, 16 transient bodies, 7 bodies/request, 3 First Feasts")
    print("  rows: witness 8, recognition 8, practice 8, vocation-service 48 (16/city), body-history 8,")
    print("        curiosity 3, leads 8, treaties 16, rites 3, feasts 3, covenants 48")
    print("  row wire: witness/recognition accept <=4096 bytes; covenant authors <=4094 and accepts <=4096")
    print("  market stock: 8 one-shot tier consignments; periodic restock rate = 0")
    print("  cap+1 law: refuse before publication; no eviction, truncation, backlog, or catch-up")
    print("  native save size/p50/p95/max: UNSIGNED -- no native distribution is inferred")


if __name__ == "__main__":
    print("Constants read from source:")
    for k, v in SRC.items():
        print(f"    {k:<24} = {v}")
    print(f"    StageUpkeepPercent       = {STAGE_PERCENT}")
    print(f"    charters (drams, ticks)  = {CHARTERS}")
    q1_headline()
    q2_floor()
    q3_ladder()
    q4_refined()
    q4b_mending()
    q5_sensitivity()
    q6_level()
    q12_styles()
    q7_handover()
    q8_trajectories()
    q9_feedback()
    q10_comfort()
    q11_food()
    water_invariants()
    food_invariants()
    meals_and_industry()
    w6_production_and_logistics()
    w7_networks_and_power()
    fully_grafted_founder()
    q13_purpose_portfolio()
    q14_hosted_arcology()
    q15_routed_construction_inputs()
    v1_authority_capacity()
    caveats()
