#!/usr/bin/env python3
"""Reviewed physical provider fixtures for the settlement catalogue.

This is authoring data, not runtime supply.  ``KingdomBuildings.xml`` remains the cap source;
the declarations below name only objects which architecture actually stamps.  The generator and
the independent content audit both consume this file so a map cannot silently drift from its
fixture declaration.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Mapping, Tuple


# Player-facing prose lives beside the fixture declaration.  Material, role, and operating state
# are therefore reviewed content rather than stock sentences assembled by the generator.
PROVIDER_DESCRIPTIONS: Mapping[str, str] = {
    "r_KingdomFineHouseAmenity":
        "A veined-marble cabinet keeps folded blankets, scented rushes, and the small comforts of a quiet house behind fitted doors. Even shut, its deep drawers soften the room's clatter.",
    "r_KingdomManorAmenity":
        "Marble-fronted drawers and a broad service ledge hold the linens, cups, and guest goods of a manor. Everything has a handsome place, and nothing need trouble the household's welcome.",
    "r_KingdomCourtAmenity":
        "This dressed-stone cabinet has shallow cupboards on both faces so court attendants can serve a gathering without crossing it. Its polished ledge bears the scratches of many shared cups.",
    "r_KingdomWetUnderfloor":
        "A lashed-timber cradle hangs between the stilt-house joists, where a sealed vessel can cool the boards above. It is empty when raised and lends damp only while it holds real fresh water.",
    "r_KingdomLivingCapCondensate":
        "A ribbed timber cradle nestles under the living cap, catching its cool shade around a stoppered water vessel. Construction leaves it empty; fresh water must be kept inside before the room grows damp.",
    "r_KingdomGuestScreenAmenity":
        "A low stone coffer waits behind the guest screen with a cup, a folded wrap, and room for a traveller's dust-stained things. Its worn lid serves as both welcome and bedside table.",
    "r_KingdomHospiceRite":
        "A clean sheet of salvaged plate forms this tending station, with hooks for bandages and a shallow ledge for a healer's hands. A hospice tender works here beneath Resheph's remembered mercy.",
    "r_KingdomHospiceWashstand":
        "Reclaimed metal has been scrubbed bright and folded into a washstand beside the hospice basin. It begins empty and serves only when a tender keeps a vessel of fresh water in it.",
    "r_KingdomArcologyWardAmenity":
        "Labelled drawers, rounded corners, and a wipe-clean counter make this worked-metal station fit for a crowded arcology ward. Bedclothes and common comforts remain close without choking the aisle.",
    "r_KingdomReservoirAmenity":
        "Low fieldstone seats broaden the reservoir lip into a public promenade around its open bowl. The basin is dry at construction and becomes a cool gathering place only while it openly holds fresh water.",
    "r_KingdomWaterworksGauge":
        "A public gauge rises from a dressed-stone plinth, its face broad enough to read across the waterworks floor. The smooth rim invites neighbours to linger and compare the day's measure.",
    "r_KingdomCondenseryFinish":
        "A worked-metal gauge with a glassy sight tube shows each silver bead won from the air. Condensery hands keep its fittings bright, and onlookers gather to watch the measure climb.",
    "r_KingdomFungalBed":
        "A fieldstone trough cups loam beneath the fungal vault, scored with channels where wet roots may travel. It is dry and unsown when built; only the vault's deliberately sown root culture draws damp through it.",
    "r_KingdomFungalGalleryBed":
        "Dressed-stone beds run beneath the gallery lights, their shallow runnels ready for a living fungal root. They begin unsown and dry, and yield damp only after the gallery's cultivation has taken root.",
    "r_KingdomBrineNurseryBasin":
        "Salvaged plates and riveted ribs hold an open nursery pool sized for sVardym young. The basin is empty when installed; only a standing fill of true, non-fresh brine makes the habitat live.",
    "r_KingdomRotChapelRite":
        "Hammered scrap encloses a sealed vessel beneath the rot-chapel's witness marks. A keeper of the Girsh rite tends it, reading instruction in what softens, blooms, and returns.",
    "r_KingdomRotChapelVessel":
        "A scrap-bound offal vessel squats under the chapel seal, its joints packed against seepage. It is empty at construction and grows wet only when real corpse-stock and liquid are kept together within.",
    "r_KingdomWaterGauge":
        "Witness cuts crowd the brass, wire, and salvaged plate of this Water-Baron gauge. A sworn reader compares its line before the gauge-house company and makes the measure public.",
    "r_KingdomWaterGaugeWetwell":
        "A sealed scrap-metal well feeds the Water-Baron gauge through a narrow sight tube. It starts empty and speaks true only while fresh water rests inside and a sworn reader attends it.",
    "r_KingdomHomeFarmGradingTable":
        "A plated grading table is pierced with measured sieves and bordered by bins for seed, fibre, and spoil. Farm hands sort the home plots' harvest here before anything reaches a storehouse.",
    "r_KingdomToolCareBench":
        "The timber top is dark with oil and crosshatched by years of sharpening. Pegs, wedges, and a straight working edge make this modest bench the place where hand tools return to readiness.",
    "r_KingdomChargingCrank":
        "A salvaged flywheel, braided wire, and a long hand crank turn patient muscle into a little charge. A post keeper braces the frame and works the handle through its heavy stroke.",
    "r_KingdomSmithyAnvil":
        "This broad fieldstone has been chosen for a level face and the clean ring it gives beneath a hammer. A smith works hot metal across its blackened crown without pretending it is finer than stone.",
    "r_KingdomForgeAnvil":
        "A dense stone block bears a dressed striking face, hardy enough for the forge's longer heats. Hammer scars overlap like scales where a practiced smith draws and turns the work.",
    "r_KingdomForgeHallBankedForge":
        "Worked plate encloses a deep refractory bed whose heat can be banked between shifts. Forge-hall hands feed the draught, rake the coals, and bring billets to an even glow.",
    "r_KingdomForgeHallCastingAnvil":
        "A massive worked-metal anvil stands beside the casting floor, with hardy corners for gates, sprues, and cooled seams. Foundry hands turn each piece over its face before it leaves the hall.",
    "r_KingdomGrindingMillstone":
        "Two local stones meet along a patient, pecked face, the upper one notched for a grain hopper and turning staff. Mill hands keep the feed even while flour gathers around the curb.",
    "r_KingdomWorkshopBench":
        "Recovered plate caps this scarred workshop bench, while drawers of wire, fasteners, and curious fragments sit below. A craftsperson can test an idea here and leave its lesson visible in the work.",
    "r_KingdomSawyerTrestle":
        "Paired fieldstone sleepers hold a timber crossbar steady above the saw pit. Sawyers roll a trunk onto the notches and work along a clear, dangerous line.",
    "r_KingdomMasonBanker":
        "A waist-high banker of fitted stone presents one true edge and one sacrificial face. Masons dress blocks upon it, letting chips fall into the yard instead of underfoot indoors.",
    "r_KingdomSmeltingFurnace":
        "Salvaged plate cinches a clay-lined furnace around a narrow firebox and tapping mouth. Smelter hands judge the heat, skim the dross, and draw useful metal from the charge.",
    "r_KingdomRiteFire":
        "Charred stakes and laid fuelwood mark a communal fire kept for pot, story, and shared observance. Its hearth gives neighbours one warm centre at which to cook and remember together.",
    "r_KingdomRiteOven":
        "A low stone oven cups heat behind a soot-dark mouth and broad baking ledge. Bread, roots, and festival dishes pass through the same warmth, making ordinary cooking a communal rite.",
    "r_KingdomArgumentBench":
        "Two worn timber seats face one another across a deliberate handspan. Under an appointed moderator, neighbours sit here until a grievance has words, witnesses, and an answer.",
    "r_KingdomMootCharterStand":
        "A stone stand lifts the settlement charter above the moot floor, its reading face angled toward every seat. The speaker keeps one hand on the text while memory and judgment contend around it.",
    "r_KingdomMarketCounter":
        "A long stone counter bears tally grooves, cup rings, and a trader's honest working edge. Market hands weigh, reckon, and exchange goods across it beneath the noise of the bazaar.",
    "r_KingdomWorkingBath":
        "Dressed stone forms a deep public bath with broad steps and a ledge for the bath keeper's jars. It is dry when completed and welcomes bathers only with real fresh water and an attendant at work.",
    "r_KingdomFirstBasinRite":
        "This unadorned fieldstone stands beside the first basin, polished where founding hands laid witness upon it. Its place, more than its shape, remembers the city's first drink together.",
    "r_KingdomWaterstoneRite":
        "A fitted stone plinth keeps the retained first basin visible above the changed heart around it. Witnesses gather here to name old water, new obligations, and the city that joins them.",
    "r_KingdomMootRostrum":
        "Joined timbers rise into a speaking platform built through the old heart rather than merely beside it. A moot keeper faces the gathered city here while the founding ground remains underfoot.",
    "r_KingdomGreatCourtRostrum":
        "Worked-metal steps and a resonant speaking rail crown the transformed civic heart. From this rostrum, an appointed voice can address the whole great court and answer before it.",
    "r_KingdomShrineOffering":
        "A palm-sized stone cup rests before the shrine, darkened by water, oil, ash, and the touch of petitioners. Each offering makes devotion tangible upon the open garth.",
    "r_KingdomGarthOffering":
        "A low stone table gathers cups, keepsakes, and weathered tokens within the shrine-garth. Rain and dust share its surface with whatever worshippers choose to leave.",
    "r_KingdomTempleSanctum":
        "A dressed-stone lectern stands in the temple's lit sanctum, its face broad enough for text, relic, or teaching object. A temple keeper reads and interprets before the assembled seats.",
    "r_KingdomScriptoriumCopyDesk":
        "A carved stone desk resists spilled ink beneath a sloped copying face and shallow tool wells. Scribes compare texts here, preserving a lesson while questioning every uncertain line.",
    "r_KingdomWatchMuster":
        "A board of salvaged plate carries patrol pegs, route scratches, and a place for each watcher's mark. The watch keeper musters the shift here before anyone takes the road.",
    "r_KingdomBarracksMuster":
        "Joined timber boards divide the barracks roster by squad, watch, and duty. An officer moves the pegs at muster, making absence and readiness plain to every bunk.",
    "r_KingdomCairnMemorial":
        "Local stones rise in a hand-built cairn, each one chosen or carried by somebody who remembers. The names may fade, but the settlement still walks around the weight of its dead.",
    "r_KingdomCrankMillMachine":
        "A worn timber walking bar turns the mill through a stout central socket. Millers lean into its circle together, trading footsteps for meal and flour.",
    "r_KingdomWaterWheelMachine":
        "Recovered bearings and a riveted drive head take the waterwheel's slow force from the millrace. A mill hand watches the shudder of the shaft and keeps its teeth engaged.",
    "r_KingdomSailvaneMachine":
        "A salvaged bearing carries the sail-vane above a braced yard frame. Its keeper trims the sweep into the wind and listens for the scrape that foretells a failed race.",
    "r_KingdomSaltAccumulator":
        "A carved stone casing channels salt into a dry collecting throat around a buried charge sink. The accumulator works only while its capacitor holds power; dark stone gathers nothing.",
    "r_KingdomHindrenLoom":
        "A salvaged treadle stitcher braces a scratched metal C around needle and bobbin, while a foot bar drives each lockstitch. Hindren clothiers teach repair, cutting, and remembered patterns beside it.",
    "r_KingdomCaravanseraiCounter":
        "A broad stone counter gives caravan masters room for ledgers, cups, seals, and disputed bundles. The house factor receives travellers here and witnesses what enters or leaves the court.",
    "r_KingdomGraveGroveMemorial":
        "A living memorial tree has been trained around a low timber rail, with names cut where bark will slowly fold over them. Root and remembrance deepen together in the grave-grove soil.",
    "r_KingdomSacramentBasin":
        "Black marble rims a civic basin beneath branching crystal, giving every side an equal place to gather. It is dry when finished; fresh water in the bowl turns the crystal court into a cool meeting ground.",
    "r_KingdomDeepCutFace":
        "Fieldstone props bite into the opened deep-cut, holding back loose layers around a clear working face. Miners sound each brace before striking where the assigned cut continues.",
    "r_KingdomNicheTombMemorial":
        "A memorial niche is cut directly into dressed gallery stone, just deep enough for a name and a few retained things. Lamp smoke gathers above the carving while footsteps pass below.",
    "r_KingdomUnderBenchWorkface":
        "A dressed-stone workface runs over fitted parts lockers beneath the underbench. Delvers sort recovered mechanisms here, learning their joins before returning anything to use.",
    "r_KingdomReliquaryWitnessMachine":
        "Dressed stone embraces a retained machine without disguising its impossible seams. The reliquary asks visitors to witness the old device as it stands, half lesson and half holy object.",
    "r_KingdomFactorLedger":
        "A marble ledger desk presents a polished public face and a deep, lockable factor's drawer. Goods, promises, and caravan marks become one witnessed reckoning across its top.",
    "r_KingdomSpiceHearthWork":
        "A stone board beside the Kyakukyan hearth is stained gold, red, and green by generations of crushed spice. Hearth keepers grind, mix, and teach the fragrant order of each shared dish.",
    "r_KingdomEzraSpindleWork":
        "A hand-cut stone bearing steadies Ezra's spindle wheel beneath its shade. A practiced spinner turns local fibre into even thread while explaining every adjustment to waiting learners.",
    "r_KingdomListeningSlab":
        "This cragmensch-sized slab was chosen for the long, low note it returns through the stone garden. Those who sit against it listen through back and bone as much as through ears.",
    "r_KingdomRobotBayWorkRail":
        "A grounded rail of salvaged contacts runs the length of the robot charge-bay. Bay hands brace a chassis against it, clear corrosion, and keep each charging berth aligned.",
    "r_KingdomRobotServiceRail":
        "A dressed-stone inspection rail holds a robot level above the service trench, with shallow lockers close at hand. Mechanics work along its whole length without crowding the machine's joints.",
    "r_KingdomBaetylLedgerFrame":
        "Salvaged struts hold tablets and answer marks around the baetyl's audience ground. An attendant records each demand exactly, because a baetyl's words tolerate neither ornament nor convenient memory.",
    "r_KingdomCaravanMeasure":
        "A public stone measure stands under the dromad shade, worn smooth by water vessels and trade weights. A caravan factor works in full view so guest and host share the same reckoning.",
    "r_KingdomEntropyWitnessPlate":
        "A salvaged metal plate is scored with reference lines and set where the blind's basin can cast its changing signs. A patient observer records each deviation and teaches others how to doubt the pattern.",
    "r_KingdomHornChallengeStandard":
        "A tall stone standard closes the goatfolk challenge ring, with notches for horns, names, and settled claims. A ring keeper calls each contest from its foot and remembers the result.",
    "r_KingdomNaphtaaliWitnessAltar":
        "An altar of welded scrap presents its guarded face beneath Naphtaali tokens and hard-won adornments. A keeper receives offerings here while armed witnesses ring the court.",
    "r_KingdomTrollTollStone":
        "A blunt toll-stone stands where three bridge approaches become one question. Its troll keeper names the price, hears the traveller's answer, and lets the crossing remember both.",
    "r_KingdomRifleRest":
        "A long stone rest fixes the Issachari porch's view down a single open lane. A watch shooter settles a rifle here and scans the salt beyond without wavering the barrel.",
    "r_KingdomMoonJudgmentStone":
        "Pale marble catches the night above the Hindren court, its face divided by old judgment cuts. Under moonlight, speakers stand beside it while the gathered kin weigh word against memory.",
    "r_KingdomRefugeServingBoard":
        "A broad stone board bridges the space between the Mopango refuge's paired hearths. Kitchen hands portion each pot here so shelter, warmth, and supper follow one visible order.",
    "r_KingdomTemplarOrderedRack":
        "Straight bars of salvaged metal divide this Putus Templar rack by weapon, rank, and inspection mark. An arsenal keeper checks every empty hook before the company is dismissed.",
    "r_KingdomGyreAshStandard":
        "A dark stone standard rises among braziers of sifted ash, its grooves grey with repeated handling. Gyre Wight keepers gather at it to witness what the Gyre took and what still endures.",
    "r_KingdomTitheBasin":
        "Hammered scrap forms a shallow basin beneath Mamon's tally shelves, its rim bright where offerings pass. A tithe keeper displays each gift before lowering it into the cistern court's custody.",
    "r_KingdomListeningSeat":
        "A stone seat faces the quiet cell's bare listening wall, shaped to keep the body still through a long vigil. A Seeker attends the listener and gives disciplined inquiry a place to begin.",
    "r_KingdomQuietBaffle":
        "Porous stone panels interlock across the Seeker cell, breaking echoes into a hush without sealing out the world. Their angled faces make the fitted room measurably quieter.",
    "r_KingdomWardenSightlinePost":
        "A salvaged sighting post aligns the lodge window, watch bench, and road beyond. A warden checks the same narrow line at every change of watch and records what crosses it.",
    "r_KingdomPublicScale":
        "A beam scale balances on a stone pier in the middle of the weighing house. Merchant witnesses set both goods and counterweights in public, where no sleeve can hide the measure.",
    "r_KingdomRepairTeachingBench":
        "A salvaged-metal bench leaves its clamps, parts trays, and mistakes open to view. A Daughter of Exile repairs at one side while learners repeat the work at the other.",
    "r_KingdomTravellerTable":
        "A low stone table rests under the Yd Freehold's living vine bower, cool even at salt-noon. A host pours for travellers here while tendrils knot themselves through the trellis overhead.",
    "r_KingdomBoughSchoolDesk":
        "A stone writing ledge follows the living curve of Chavvah's bough without biting into its bark. Teacher and learners gather around the trunk, sharing questions beneath the leaves.",
    "r_KingdomButcherWorkSlab":
        "A dense stone slab slopes toward a catch groove, with basket room kept clear at the working end. The butcher dresses carcasses here under steady hands and an easily washed edge.",
    "r_KingdomPreservationTable":
        "A dressed-stone table sets knives, seals, and preserving jars in a clean line between the vat-house vessels. Its keeper prepares each cut before it enters long storage.",
    "r_KingdomPreservedOffalVat":
        "A dressed-stone vat is sealed around a deep preserved-stock chamber. It is empty when built and supplies wet offal only while real corpse-stock remains immersed in liquid inside.",
    "r_KingdomGraftingTable":
        "Worked metal forms a narrow procedure table with drains, restraint points, and labelled instrument ledges. A trained grafting crew works here where every cut and join can be reached.",
    "r_KingdomGraftingOffalStore":
        "A plated cabinet seals the grafting hall's preserved tissue behind lipped shelves. Construction leaves it empty; only real corpse-stock stored together with liquid gives the hall wet offal.",
    "r_KingdomChimericTable":
        "A seamless metal table branches into articulated rests for bodies that may not keep one familiar shape. Theatre specialists set tools and limbs within reach before the chimeric work begins.",
    "r_KingdomChimericOffalStore":
        "Worked plate encloses a deep vat beside the chimeric theatre, with clamps sized for an uneasy variety of stock. It begins empty and provides wet offal only when corpse-stock stands in liquid within.",
    "r_KingdomBecomingChair":
        "A worked-metal chair locks into the annexe floor beneath a halo of adjustable restraints and contact arms. Becoming attendants inspect every joint before seating anyone at its centre.",
    "r_KingdomBecomingCharger":
        "Worked-metal induction coils cradle cells and devices beside the becoming chair. Newly installed, its capacitor is dark; once supplied with charge, its real contacts can replenish what rests there.",
    "r_KingdomDeepBoreHead":
        "A worked-metal cutting head coils heavy teeth around a socket built for the great bore. Bore crews gauge its edges and keep it ready beside the machine that will bear its weight.",
    "r_KingdomGreatFoundryForge":
        "Layered metal and refractory brick form a forge broad enough for realm-scale castings. Foundry crews bank its heat in shifts, never leaving the bright mouth unwatched.",
    "r_KingdomGreatFoundryAnvil":
        "This worked-metal casting anvil spreads its mass through the foundry floor on branching feet. Crews dress gates, seams, and stubborn flaws across different faces of the same block.",
    "r_KingdomGreatFoundryFurnace":
        "A plated furnace climbs around a refractory shaft, feeding molten wealth toward the great foundry's casting floor. Its crew reads heat and flow through shielded ports before opening the tap.",
    "r_KingdomGranaryColossusLedger":
        "A worked-metal ledger desk faces the colossus controls, its drawers divided by settlement, season, and seal. Granary officers reconcile every great movement of stores beneath the machine's shadow.",
    "r_KingdomMirrorGateCore":
        "A worked-metal keying core rests cold between the mirror-gate lights. It is unpaired when built and wakes only when both keyed arches form a powered reciprocal link.",
    "r_KingdomCrownWitnessDais":
        "Worked-metal steps lift a witness chair into the crown hall's full view, with no screen between ruler and assembly. Heralds and petitioners meet the seated crown across this accountable distance.",
    "r_KingdomArcologyCouncilDais":
        "A sleek metal dais bends one council table around the arcology's central speaking place. Assigned councillors work face to face while the surrounding galleries look inward.",
    "r_KingdomHallSurgerySlab":
        "A dressed-stone procedure slab stands under the hall surgery's best light, cut with drains and reachable tool ledges. The surgery staff works around all four sides without blocking the ward aisle.",
    "r_KingdomHallSurgeryOffalStore":
        "A fitted stone vat closes beneath the hall surgery's preserved-stock shelves. It is empty on construction and provides wet offal only while corpse-stock and liquid are actually held together inside.",
    "r_KingdomRegistryDesk":
        "A dressed stone desk joins witness shelf, seal well, and writing face into one public station. The registrar records names and obligations here while their witnesses still stand close enough to object.",
    "r_KingdomHideRack":
        "A lashed timber frame stretches hides into sun and moving air, with pegs set for many shapes. Its open yard footing leaves the working face reachable from both sides.",
    "r_KingdomVellumPress":
        "A heavy timber screw drives two smooth press boards together over prepared sheets. Set in an open work yard, it gives patient pressure and drainage room to the vellum maker.",
}


@dataclass(frozen=True)
class ProviderFixture:
    building: str
    blueprint: str
    display: str
    carries: str = ""
    provides: str = ""
    scope: str = "Building"
    operation: str = "Present"
    component: str = "main"
    anchors: Tuple[str, ...] = ()
    state: str = ""
    portable: bool = True
    nonportable_reason: str = ""
    native_part: str = ""
    blank_only: bool = False
    stateful: bool = True
    material: str = ""
    natural: bool = False
    installed_anchor: str = ""
    description: str = ""


def f(building: str, blueprint: str, display: str, carries: str = "",
      provides: str = "", scope: str = "Building", operation: str = "Present",
      component: str = "main", anchors: Tuple[str, ...] = (), state: str = "",
      portable: bool = True, reason: str = "", native_part: str = "",
      blank_only: bool = False, stateful: bool = True, material: str = "",
      natural: bool = False, installed_anchor: str = "", description: str = "") -> ProviderFixture:
    authored_description = description or PROVIDER_DESCRIPTIONS[blueprint]
    return ProviderFixture(building, blueprint, display, carries, provides, scope, operation,
                           component, anchors, state, portable, reason, native_part, blank_only,
                           stateful, material, natural, installed_anchor, authored_description)


# One row means one exact installed object in every authored variant unless a comment says that
# native engine parts supply the rest.  Shrine residuals account for the root's real Shrine part;
# reliquary and the shrine-derived creed roots therefore never over-install spirit behind a cap.
BUILDING_FIXTURES: Tuple[ProviderFixture, ...] = (
    # Housing and hosted lodging. Beds remain native Bed parts on separate sleeping fixtures.
    f("finehouse", "r_KingdomFineHouseAmenity", "quiet-house amenity cabinet", "luxury:4",
      "taf:quiet", "Interior", anchors=("fixture:storage",)),
    f("manor", "r_KingdomManorAmenity", "manor hospitality cabinet", "luxury:9",
      scope="Interior", anchors=("fixture:storage",)),
    f("court", "r_KingdomCourtAmenity", "court hospitality cabinet", "luxury:2",
      scope="Interior", anchors=("fixture:storage",)),
    f("stiltrow", "r_KingdomWetUnderfloor", "stilt-house water cradle", provides="taf:damp",
      scope="Interior", operation="Custom", anchors=("fixture:storage",), state="HeldFreshWater"),
    f("caproof", "r_KingdomLivingCapCondensate", "living-cap condensate cradle",
      provides="taf:damp", scope="Interior", operation="Custom", anchors=("fixture:storage",),
      state="HeldFreshWater"),
    f("strangersguestscreen", "r_KingdomGuestScreenAmenity", "guest-screen welcome chest",
      "luxury:1", scope="Interior", anchors=("bed:private-alcove",),
      installed_anchor="fixture:welcome-chest"),
    f("reshephhospice", "r_KingdomHospiceRite", "hospice tending station", "spirit:2",
      scope="Interior", operation="Staffed", component="rite", anchors=("water:tending-basin",),
      portable=False, reason="Resheph hospice rite is a creed installation."),
    f("reshephhospice", "r_KingdomHospiceWashstand", "hospice clean-water stand",
      provides="taf:damp", scope="Interior", operation="Custom", component="water",
      anchors=("water:tending-basin",), state="HeldFreshWaterAndStaffed", portable=False,
      reason="The clean stand is commissioned as part of a staffed creed hospice."),
    f("arcologyward", "r_KingdomArcologyWardAmenity", "arcology ward amenity station",
      "luxury:2", scope="Interior", anchors=("fixture:storage",), portable=False,
      reason="The ward station is a fixed hosted-arcology service fixture."),

    # Wet works. Actual root LiquidVolume parts supply native damp/open-water where present.
	f("reservoir", "r_KingdomReservoirAmenity", "reservoir open promenade basin", "luxury:2",
	  "taf:damp,taf:openwater", scope="Plot", operation="Custom",
      anchors=("function:reservoir", "service:gauge"), state="OpenFreshWater"),
    f("waterworks", "r_KingdomWaterworksGauge", "waterworks public gauge", "luxury:2",
      anchors=("gauge", "control", "light"), installed_anchor="service:waterworks-gauge"),
    f("condensery", "r_KingdomCondenseryFinish", "condensery viewing gauge", "luxury:1",
      anchors=("gauge", "control", "light")),
    f("fungalvault", "r_KingdomFungalBed", "inoculated fungal bed", provides="taf:damp",
      scope="Interior", operation="Custom", anchors=("function:fungal", "storage"),
      state="RootSown", portable=False, reason="Cultivation state belongs to the sown vault."),
    f("vaultgalleries", "r_KingdomFungalGalleryBed", "inoculated gallery bed",
      provides="taf:damp", scope="Interior", operation="Custom",
      anchors=("function:fungal", "light", "storage"), state="RootSown", portable=False,
      reason="Cultivation state belongs to the sown gallery root."),
    f("svardymbrinenursery", "r_KingdomBrineNurseryBasin", "open brine nursery basin",
      provides="taf:damp,taf:openwater", scope="Yard", operation="Custom",
      anchors=("brine", "basin"), state="OpenBrine", portable=False,
      reason="An open brine nursery is a fixed creed habitat."),
    f("girshrotchapel", "r_KingdomRotChapelRite", "rot-chapel sacrament vessel",
      "spirit:3,learning:1", provides="taf:shrine", scope="Interior", operation="Staffed", component="rite",
      anchors=("shrine:sealed-vessel",), portable=False,
      reason="The Girsh rite is inseparable from its chapel."),
    f("girshrotchapel", "r_KingdomRotChapelVessel", "rot-chapel offal vessel",
      provides="taf:damp,taf:offal", scope="Interior", operation="Custom", component="offal",
      anchors=("shrine:sealed-vessel",), state="WetOffal", portable=False,
      reason="The sealed offal vessel is a fixed creed installation."),
    f("waterbaronsgaugehouse", "r_KingdomWaterGauge", "Water-Baron witness gauge", "order:2",
      operation="Staffed", component="gauge", anchors=("registry:witness-marks",),
      portable=False, reason="The witnessed gauge is calibrated to its creed house."),
    f("waterbaronsgaugehouse", "r_KingdomWaterGaugeWetwell", "Water-Baron wet gauge",
      provides="taf:damp", operation="Custom", component="water", anchors=("water:sealed-gauge",),
      state="HeldFreshWaterAndStaffed", portable=False,
      reason="The wet gauge is calibrated to its creed house."),

    # Ordinary production, civic, faith, archive, defence, and power.
    f("homefarm", "r_KingdomHomeFarmGradingTable", "home-farm grading table", "craft:2",
      operation="Staffed", anchors=("work:grading",)),
    f("toolshed", "r_KingdomToolCareBench", "tool-care bench", "craft:1",
      anchors=("work:bench",)),
    f("chargingpost", "r_KingdomChargingCrank", "charging-post hand crank", "craft:1",
      operation="Staffed", anchors=("service:crank-clearance",)),
    f("smithy", "r_KingdomSmithyAnvil", "smithy's striking block", "craft:3",
      operation="Staffed", anchors=("work:anvil",), stateful=False),
    f("forge", "r_KingdomForgeAnvil", "forge striking block", "craft:6",
      operation="Staffed", anchors=("work:anvil",), stateful=False),
    f("forgehall", "r_KingdomForgeHallBankedForge", "forge-hall banked forge", "craft:6",
      operation="Staffed", component="forge", anchors=("work:forge-face",)),
    f("forgehall", "r_KingdomForgeHallCastingAnvil", "forge-hall casting anvil", "craft:4",
      operation="Staffed", component="anvil", anchors=("work:casting-anvil",)),
    f("grindmill", "r_KingdomGrindingMillstone", "grain millstone", "craft:1",
      operation="Staffed", anchors=("power:input", "function:grinding")),
    f("workshop", "r_KingdomWorkshopBench", "settlement workshop bench", "craft:7,learning:1",
      operation="Staffed", anchors=("work:bench", "work-surface")),
    f("sawyeryard", "r_KingdomSawyerTrestle", "sawyer's trestle", "craft:2", scope="Yard",
      operation="Staffed", anchors=("hazard:saw-pit", "function:sawing")),
    f("masonyard", "r_KingdomMasonBanker", "mason's banker", "craft:2", scope="Yard",
      operation="Staffed", anchors=("work:banker",)),
    f("smelter", "r_KingdomSmeltingFurnace", "smelting furnace", "craft:3",
      operation="Staffed", anchors=("hot", "output", "function:smelting")),
	f("fire", "r_KingdomRiteFire", "communal rite fire", "spirit:1",
	  provides="taf:cooking", scope="Plot",
      anchors=("function:cooking", "main"), stateful=False),
    f("oven", "r_KingdomRiteOven", "settlement rite oven", "spirit:2",
      provides="taf:cooking",
      anchors=("fixture:oven-apron", "function:cooking")),
    f("bench", "r_KingdomArgumentBench", "argument bench", "spirit:2", scope="Yard",
      operation="Staffed", anchors=("seat",)),
    f("hall", "r_KingdomMootCharterStand", "moot charter stand", "spirit:3,order:2",
      scope="Interior", operation="Staffed", anchors=("fixture:charter", "archive:charter")),
	f("bazaar", "r_KingdomMarketCounter", "market reckoning counter", "luxury:4,craft:2",
	  provides="taf:market", scope="Plot", operation="Staffed",
	  anchors=("stall:table", "stall:counter")),
    f("bathhouse", "r_KingdomWorkingBath", "working public bath", "luxury:6,spirit:2",
      scope="Interior", operation="Custom", anchors=("liquid:hot-basin",),
      state="HeldFreshWaterAndStaffed"),
    f("heartbasin", "r_KingdomFirstBasinRite", "first-basin witness stone", "spirit:1",
      scope="Yard", portable=False, blank_only=True, stateful=False, material="stone",
      natural=True, installed_anchor="rite:first-basin-witness",
      reason="The witness stone is fixed beside the city's unique founding basin."),
    f("heartwaterstone", "r_KingdomWaterstoneRite", "waterstone witness plinth",
      "spirit:2,order:1", scope="Yard", portable=False, blank_only=True, stateful=False,
      installed_anchor="rite:waterstone-witness",
      reason="The witness plinth is fixed beside the retained first basin."),
    f("heartmoot", "r_KingdomMootRostrum", "heart-moot rostrum", "spirit:4,order:3",
      operation="Staffed", anchors=("function:settlement-heart", "seat"), portable=False,
      stateful=False, reason="The rostrum is a transformative civic-heart rung."),
    f("heartcourt", "r_KingdomGreatCourtRostrum", "great-court rostrum",
      "spirit:8,order:6,learning:2", operation="Staffed",
      anchors=("function:settlement-heart", "seat"), portable=False,
      stateful=False, reason="The rostrum is a transformative civic-heart rung."),
    # These two roots stand in authored open yards. Their real Shrine parts therefore do not
    # satisfy the native Interior adapter; the offering fixtures carry the full catalogue cap.
    f("shrine", "r_KingdomShrineOffering", "shrine offering cup", "spirit:2",
      provides="taf:shrine",
      scope="Yard", anchors=("offering", "basin"), stateful=False),
    f("shrinegarth", "r_KingdomGarthOffering", "shrine-garth offering table", "spirit:3",
      provides="taf:shrine",
      scope="Yard", anchors=("offering", "basin")),
    f("temple", "r_KingdomTempleSanctum", "temple sanctum lectern", "spirit:7,learning:1",
      provides="taf:shrine",
      scope="Interior", operation="Staffed", anchors=("sanctum", "seat", "light")),
    f("scriptorium", "r_KingdomScriptoriumCopyDesk", "scriptorium copy desk", "learning:4",
      provides="taf:education,taf:inquiry",
      scope="Interior", operation="Staffed", anchors=("copy", "desk", "table")),
    f("watchhouse", "r_KingdomWatchMuster", "watch muster board", "order:3",
      operation="Staffed", anchors=("muster", "registry", "table"), stateful=False),
    f("barracks", "r_KingdomBarracksMuster", "barracks muster board", "order:6",
      operation="Staffed", anchors=("muster", "registry", "table")),
    f("cairn", "r_KingdomCairnMemorial", "settler's memorial cairn", "spirit:1",
      scope="Yard", anchors=("function:memorial", "main"),
      installed_anchor="memorial:witness-cairn"),
    f("mill", "r_KingdomCrankMillMachine", "walking-bar crank mill", "craft:1",
      operation="Staffed", anchors=("work:walking-bar",)),
    f("waterwheel", "r_KingdomWaterWheelMachine", "waterwheel drive head", "craft:2",
      scope="Yard", operation="Staffed", anchors=("water:millrace",)),
    f("sailvane", "r_KingdomSailvaneMachine", "sail-vane bearing", "craft:2",
      scope="Yard", operation="Staffed", anchors=("clearance:sweep",)),
    f("saltstore", "r_KingdomSaltAccumulator", "powered salt accumulator", "craft:2",
      operation="Powered", anchors=("salt", "light", "function")),
    f("hindrenweavehall", "r_KingdomHindrenLoom", "Hindren treadle stitcher", "craft:3",
      operation="Staffed", anchors=("work:loom1",)),

    # Deep and foreign ordinary works.
    f("caravanserai", "r_KingdomCaravanseraiCounter", "caravanserai reckoning counter",
      "luxury:5,order:2", operation="Staffed", anchors=("stall:table", "storage:caravan")),
    f("gravegrove", "r_KingdomGraveGroveMemorial", "grave-grove memorial tree", "spirit:1",
      scope="Yard", anchors=("function:memorial", "main"), portable=False,
      installed_anchor="memorial:grave-tree",
      reason="A memorial tree is rooted in its named grove."),
    f("sacramentcourt", "r_KingdomSacramentBasin", "filled crystal-court basin",
      "spirit:4,luxury:4", operation="Custom", anchors=("liquid:meeting-basin1",),
      state="HeldFreshWater", portable=False,
      reason="The filled basin is a fixed civic meeting installation."),
    f("deepcut", "r_KingdomDeepCutFace", "propped deep-cut face", "craft:2",
      operation="Staffed", anchors=("work", "function:cut")),
    f("nichetomb", "r_KingdomNicheTombMemorial", "carved niche memorial", "spirit:1",
      scope="Interior", anchors=("function:memorial", "main"), portable=False,
      reason="The memorial is cut into its gallery wall."),
    f("underbench", "r_KingdomUnderBenchWorkface", "underbench parts face",
      "craft:6,learning:2", operation="Staffed", anchors=("work", "bench", "locker")),
    f("reliquary", "r_KingdomReliquaryWitnessMachine", "retained reliquary machine",
      "spirit:8,learning:2", provides="taf:shrine", scope="Interior", anchors=("relic", "machine"), portable=False,
      reason="The retained machine is the unique fixed reliquary witness."),
    f("factorhouse", "r_KingdomFactorLedger", "factor-house ledger desk", "luxury:4,order:3",
      scope="Interior", operation="Staffed", anchors=("ledger", "desk", "shelf")),

    # Creed-specific fixtures may not be detached from the practice which makes them meaningful.
    f("kyakukyaspicehearth", "r_KingdomSpiceHearthWork", "Kyakukyan spice-hearth board",
      "luxury:2", provides="taf:cooking", operation="Staffed", anchors=("spice", "hearth", "table"), portable=False,
      reason="Creed hearth practice is fixed to its authored court."),
    f("ezrawheelshade", "r_KingdomEzraSpindleWork", "Ezra spindle wheel",
      "craft:2,learning:1", scope="Yard", operation="Staffed", anchors=("spindle",),
      portable=False, reason="The wheel is a creed-practice installation."),
    f("cragmenschstonegarden", "r_KingdomListeningSlab", "cragmensch listening slab",
      "spirit:3", scope="Yard", anchors=("stone", "seat"), portable=False,
      reason="The listening stone belongs to its rooted garden."),
    f("robotchargebay", "r_KingdomRobotBayWorkRail", "robot charge-bay work rail", "craft:2",
      operation="Staffed", anchors=("contact", "bay"), portable=False, stateful=False,
      reason="The rail is transformatively grounded into a robot charge-bay."),
    f("robotservicebay", "r_KingdomRobotServiceRail", "robot service inspection rail", "craft:4",
      operation="Staffed", anchors=("contact", "service", "locker"), portable=False,
      reason="The rail is grounded into a robot service-bay."),
    f("baetylofferingframe", "r_KingdomBaetylLedgerFrame", "baetyl answer ledger-frame",
      "spirit:3,learning:2", provides="taf:shrine", scope="Yard", operation="Staffed",
      anchors=("altar", "table", "shelf"),
      portable=False, reason="The frame is calibrated around a baetyl audience."),
    f("dromadcaravanshade", "r_KingdomCaravanMeasure", "dromad public measure",
      "luxury:3,order:1", scope="Yard", operation="Staffed", anchors=("measure", "rack"),
      portable=False, reason="The public measure belongs to its caravan court."),
    f("entropyblind", "r_KingdomEntropyWitnessPlate", "entropy witness plate",
      "learning:3,spirit:2", provides="taf:education,taf:inquiry", operation="Staffed", anchors=("witness", "basin"),
      portable=False, reason="The plate only reads inside its authored blind."),
    f("goatfolkhornmoot", "r_KingdomHornChallengeStandard", "goatfolk challenge standard",
      "spirit:2,order:1", scope="Yard", operation="Staffed", anchors=("horn", "challenge"),
      portable=False, reason="The standard closes an authored challenge ring."),
    f("naphtaaliscrapaltar", "r_KingdomNaphtaaliWitnessAltar", "Naphtaali witness altar",
      "spirit:2,luxury:1", provides="taf:shrine", operation="Staffed", anchors=("altar",), portable=False,
      reason="The guarded altar is a fixed creed installation."),
    f("trollbridgecourt", "r_KingdomTrollTollStone", "troll toll-stone", "order:2",
      scope="Yard", operation="Staffed", anchors=("toll", "bridge"), portable=False,
      reason="The toll-stone takes meaning from its three fixed approaches."),
    f("issacharirifleporch", "r_KingdomRifleRest", "Issachari rifle rest", "order:1",
      scope="Yard", operation="Staffed", anchors=("rifle", "bench"), portable=False,
      reason="The sighted rest is fixed to its porch lane."),
    f("hindrenmooncourt", "r_KingdomMoonJudgmentStone", "Hindren moon-judgment stone",
      "spirit:3,luxury:2", scope="Yard", operation="Staffed", anchors=("moon", "stone"),
      portable=False, reason="The judgment stone belongs to its moon court."),
    f("mopangorefugekitchen", "r_KingdomRefugeServingBoard", "Mopango refuge serving board",
      "order:1", provides="taf:cooking", operation="Staffed", anchors=("table", "hearth"), portable=False,
      reason="The serving order belongs to the paired refuge hearths."),
    f("templarpurityarsenal", "r_KingdomTemplarOrderedRack", "Templar ordered arms rack",
      "order:3", operation="Staffed", anchors=("arms", "rack"), portable=False,
      reason="The inspected rack is fixed into the purity arsenal."),
    f("gyrewightashcourt", "r_KingdomGyreAshStandard", "gyre-wight ash standard", "spirit:4",
      provides="taf:shrine", scope="Yard", operation="Staffed", anchors=("brazier", "ash"), portable=False,
      reason="The standard is one fixed witness among the ash braziers."),
    f("mamontithecistern", "r_KingdomTitheBasin", "Mamon tithe witness basin",
      "spirit:2,luxury:2", provides="taf:shrine", operation="Staffed", anchors=("basin", "shelf"), portable=False,
      reason="The tithe witness is fixed to its cistern court."),
    f("seekersquietcell", "r_KingdomListeningSeat", "Seeker listening seat",
      "learning:4,spirit:1", provides="taf:education,taf:inquiry", scope="Interior", operation="Staffed", component="seat",
      anchors=("work:listening-seat",), portable=False,
      reason="The listening seat is tuned to its cell."),
    f("seekersquietcell", "r_KingdomQuietBaffle", "Seeker acoustic baffle",
      provides="taf:quiet", scope="Interior", component="quiet",
      anchors=("screen:quiet-baffle",), portable=False,
      reason="The baffle is fitted to the cell's exact shell."),
    f("wardenswatchlodge", "r_KingdomWardenSightlinePost", "warden sightline post", "order:3",
      scope="Interior", operation="Staffed", anchors=("sightline", "watch", "bench", "fire"), portable=False,
      reason="The post is sighted from its exact lodge ground."),
    f("merchantweighinghouse", "r_KingdomPublicScale", "merchant public scale",
      "luxury:3,order:3", operation="Staffed", anchors=("scale", "table"), portable=False,
      reason="The public scale is sealed to its witnessed floor."),
    f("daughtersrepairlodge", "r_KingdomRepairTeachingBench", "Daughters teaching bench",
      "craft:3,learning:1", operation="Staffed", anchors=("bench", "shelf"), portable=False,
      reason="The bench is a creed-practice teaching installation."),
    f("ydvinebower", "r_KingdomTravellerTable", "Yd Freehold traveller's table", "luxury:3",
      scope="Yard", operation="Staffed", anchors=("table", "trellis"), portable=False,
      reason="The traveller's table belongs among its living vines."),
    f("chavvahboughschool", "r_KingdomBoughSchoolDesk", "Chavvah bough-school desk",
      "learning:4,spirit:2", provides="taf:education,taf:inquiry", scope="Yard", operation="Staffed", anchors=("desk", "trunk"),
      portable=False, reason="The desk is fitted around a living bough."),

    # Laboratory, purpose, crown, and arcology fixtures.
    f("butcherslab", "r_KingdomButcherWorkSlab", "butcher's working slab", "craft:1",
      operation="Staffed", anchors=("work", "basket", "function:butchery")),
    f("vathouse", "r_KingdomPreservationTable", "vat-house preservation table", "craft:2",
      operation="Staffed", component="work", anchors=("work:preservation",)),
    f("vathouse", "r_KingdomPreservedOffalVat", "vat-house preserved-stock vat",
      provides="taf:damp,taf:offal", operation="Custom", component="offal",
      anchors=("storage:preserved",), state="WetOffal", portable=False,
      reason="The preserved-stock vat is fixed stateful plant."),
    f("graftinghall", "r_KingdomGraftingTable", "grafting procedure table",
      "craft:6,learning:2", scope="Interior", operation="Staffed", component="work",
      anchors=("work:grafting-table",), portable=False,
      reason="The procedure table is fixed surgical plant."),
    f("graftinghall", "r_KingdomGraftingOffalStore", "grafting preserved-stock cabinet",
      provides="taf:damp,taf:offal", scope="Interior", operation="Custom", component="offal",
      anchors=("storage:procedure",), state="WetOffal", portable=False,
      reason="The preserved-stock cabinet is fixed stateful plant."),
    f("chimerictheatre", "r_KingdomChimericTable", "chimeric procedure table",
      "craft:8,learning:3", scope="Interior", operation="Staffed", component="work",
      anchors=("work:chimeric-table",), portable=False,
      reason="The theatre table is fixed surgical plant."),
    f("chimerictheatre", "r_KingdomChimericOffalStore", "chimeric preserved-stock vat",
      provides="taf:damp,taf:offal", scope="Interior", operation="Custom", component="offal",
      anchors=("storage:preserved",), state="WetOffal", portable=False,
      reason="The preserved-stock vat is fixed stateful plant."),
    f("becomingannexe", "r_KingdomBecomingChair", "becoming procedure chair",
      "craft:8,order:3", scope="Interior", operation="Staffed", component="chair",
      anchors=("work:becoming-chair",), portable=False,
      reason="The restraint chair is fixed megastructure plant."),
    f("becomingannexe", "r_KingdomBecomingCharger", "becoming charge cradle",
      scope="Building", component="charge", anchors=("power:charge-bank",),
      portable=False, reason="The charge cradle is fixed megastructure plant.",
      native_part="UniversalCharger"),
    f("deepbore", "r_KingdomDeepBoreHead", "great-bore cutting head", "craft:8,wealth:3",
      operation="Staffed", portable=False, blank_only=True,
      installed_anchor="work:bore-spare-head",
      reason="The spare cutting head is fixed beside the runtime-owned bore machine."),
    f("greatfoundry", "r_KingdomGreatFoundryForge", "great-foundry forge", "craft:6",
      operation="Staffed", component="forge", anchors=("work:forge",), portable=False,
      reason="The forge is fixed purpose-work machinery."),
    f("greatfoundry", "r_KingdomGreatFoundryAnvil", "great-foundry casting anvil", "craft:4",
      operation="Staffed", component="anvil", anchors=("work:anvil",), portable=False,
      reason="The anvil is fixed purpose-work machinery."),
    f("greatfoundry", "r_KingdomGreatFoundryFurnace", "great-foundry furnace", "wealth:3",
      operation="Staffed", component="wealth", portable=False, blank_only=True,
      installed_anchor="work:casting-furnace",
      reason="The casting furnace is fixed beside the runtime-owned foundry machine."),
    f("realmgranary", "r_KingdomGranaryColossusLedger", "realm-granary colossus ledger",
      "wealth:2", operation="Staffed", anchors=("purpose:operator", "ledger"), portable=False,
      reason="The ledger governs one fixed realm-scale granary."),
    f("mirrorgate", "r_KingdomMirrorGateCore", "live mirror-gate keying core",
      "craft:5,luxury:4", operation="Custom", anchors=("light:keying",), state="MirrorPair",
      portable=False, reason="The core works only in a live reciprocal mirror-gate pair."),
    f("crownhall", "r_KingdomCrownWitnessDais", "crown-hall witness dais",
      "order:6,spirit:3", scope="Interior", operation="Staffed", anchors=("witness", "chair"),
      portable=False, reason="The dais is fixed crown infrastructure."),
    f("arcology", "r_KingdomArcologyCouncilDais", "arcology council dais",
      "order:4,luxury:4", scope="Interior", operation="Staffed", anchors=("council", "seat"),
      portable=False, reason="The dais is fixed arcology infrastructure."),
    f("hallsurgery", "r_KingdomHallSurgerySlab", "hall surgery procedure slab", "craft:2",
      scope="Interior", operation="Staffed", component="work", anchors=("work", "table"),
      portable=False, reason="The surgery slab is fixed arcology plant."),
    f("hallsurgery", "r_KingdomHallSurgeryOffalStore", "hall surgery preserved-stock vat",
      provides="taf:damp,taf:offal", scope="Interior", operation="Custom", component="offal",
      anchors=("vat", "storage"), state="WetOffal", portable=False,
      reason="The preserved-stock vat is fixed stateful plant."),
    f("registryoffice", "r_KingdomRegistryDesk", "registry witness desk", "order:3",
      scope="Interior", operation="Staffed", anchors=("registry", "desk", "shelf"),
      portable=False, reason="The registry desk is fixed arcology infrastructure."),
)


YARD_FIXTURES: Tuple[ProviderFixture, ...] = (
    f("yard:hiderack", "r_KingdomHideRack", "working hide rack", "craft:1", scope="Yard",
      anchors=("yard",)),
    f("yard:vellumpress", "r_KingdomVellumPress", "working vellum press", "learning:1",
      scope="Yard", anchors=("yard",)),
)


# Native capability routes needed by adopted and Hearth rooms.  These remain actual engine parts;
# none is a provider-name adapter.
NATIVE_PORTABLES = (
    ("r_KingdomFixtureBedrollCanvas", "r_KingdomPortableBedroll", 1),
    ("r_KingdomFixtureBedTimber", "r_KingdomPortableTimberBed", 2),
    ("r_KingdomFixtureBedMetal", "r_KingdomPortableMetalBed", 4),
    ("r_KingdomShrine", "r_KingdomPortableShrine", 1),
    ("r_KingdomReadableArchiveShelf", "r_KingdomPortableChronicleShelf", 2),
    ("r_KingdomChargingPost", "r_KingdomPortableChargingCradle", 3),
)
