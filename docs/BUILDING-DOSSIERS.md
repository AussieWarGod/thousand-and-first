# Building Dossiers — the catalogue, mapped

> Method (author ruling, 2026-08-30): map every building out in an understandable form — what it
> DOES, who visits and how many, what resources it holds, what materials its rung and culture
> demand, what furniture follows from all of that — THEN build the fixes in. This document is
> that map, assembled from three full-catalogue research passes over the Architecture XMLs,
> [RuntimeData/KingdomBuildings.xml](../RuntimeData/KingdomBuildings.xml) (Cost/Staff/Carries),
> [RuntimeData/ObjectBlueprints.xml](../RuntimeData/ObjectBlueprints.xml), vanilla Qud data, and
> [the polish contract](../_notes/ARCHITECTURE-POLISH-CONTRACT.md) §§6–8.
>
> Reading key: VISITORS = Staff/CrewNeeds (who works it) plus Carries (the population-lift the
> game actually counts). "Contents=" is the one bonus furnishing rolled onto a finished plot
> from a population table. Rung ladder: hands → salvage → workshop → foundry → arclight
> (formal enum in [KingdomZoningDeclarations.cs](../Growth/KingdomZoningDeclarations.cs)).

## Family theme laws

| Family | Theme law |
|---|---|
| Housing | Shelter scales honestly canvas → arcology; every tier keeps sleep + storage (+ heat once crewed); table/seat are status furniture gated to the marble tier. |
| Water | Storage stores, producers produce; three honest lanes (salt-pan sun, sky/dew, underground seep) climb hands → foundry; the condensery is the catalogue's model top tier. |
| Food | Growing and dry-keeping split from cooking; nothing automates — the ladder buys a better hand-rate, never the hands' discharge. |
| Craft | The one real hands→foundry production spine — its buildings must wear their own products (a smithy in a worked-metal city forges on worked metal, not a stone anvil forever). |
| Storage (type) | An overloaded socket (water / larder / trade / regulatory) — same-set replacement is contract-legal but function drift must stay visible. |
| Power / Defense | Working and watching happen at night: these families carry lights or they are lying about their function. |
| Civic / Faith | Rooms face seating and circulation toward the counter, dais, basin, or altar; the heart chain retains each rung's fabric inside the next (the court wraps the preserved moot; the basin never moves off the rite). |
| Memorial | Markers, not workplaces: open-air, hands-rung, unlit by design (a deliberately cold brazier is idiom; a missing lamp is not). |
| Knowledge | Archives face a lit reading/copy surface and hold inspectable content — an empty shelf is scenery. |
| Creeds (30) | Topology/use/access vary, never colour alone — horn rings, moon courts, paired hearths, wet nurseries, travelling frames. All currently hands/salvage with no tier progression. |
| Eater / Reopened | METAL WALLS, METAL FLOORS, LIGHTS, TECH (author ruling). Templates that already comply: mirror-gate (all variants), becoming-annexe, condensery, chimeric theatre. |

## Housing

| Building (tier, size) | Purpose | Visitors | Materials @ rung | Furniture | Gaps |
|---|---|---|---|---|---|
| tent (S, hands) | first-night shelter | household 1–2 | canvas wall, dirt floor, no light | bedroll, basket | none — the reference starting building |
| hut → hutyard (S) | first crewed household | 2–3 | brinestalk | + hearth | none |
| house → housecourt (M) | settled household(s); court = 3 families | 8 → 18 | sandstone | bed×2, hearth, storage | court: 3 households share one hearth, no per-family storage |
| terrace (L, workshop, Town) | a whole street raised at once | dozens, 4 doors | wood floor + sandstone | 2 beds, 1 storage, 1 hearth TOTAL | worst furniture/visitor mismatch in housing |
| finehouse (M, Village) | a notable's dwelling | 1 household | marble floor | + table/seat (status, correctly gated) | none |
| manor (L) / court (XL, City) | gated estate / a quarter under one roof | guests / dozens | marble/stone | full sets (court per wing) | none |
| mudhut, caproof, bonefold, blockhut(+yard), stiltrow, ydroofline | style-exclusive S shelters (mud / fungal / gyre / eater / verdant / yd) | households | each culture's hands fabric (deliberate: no lamp in the cap, no hearth on the bonefold) | sleep + storage | none of these six ever rolls the Contents furnishing every mainline tier gets |
| carvedcell/gallery (S/M, deep) | cut rock is the wall | households | limestone (natural) | full set + Contents | none |
| arcologyward (L, arclight) | sealed vertical lodging | dozens (roof:26) | concrete + security door + Techlight1 | metal bed/shelf | service-core anchor is a bare wall blueprint — invisible utility point |
| strangersguestscreen / reshephhospice (creed S) | anonymous lodging / open hospice | transients / the sick | canvas screens | pallets (+tending basins) | no Contents; hospice borrows water anchors without water naming |

## Water

| Building | Purpose | Resources | Materials | Gaps |
|---|---|---|---|---|
| saltpan → saltterrace (S, hands) | brine evaporation | water 2→4/day | half-stone + basin | no Contents roll at the ladder's entry |
| catchment(bank) (S, salvage) | dew | 3→5 | worn wood + scrap basin | none |
| airwellcourt/field (M/L, workshop) | domed air-wells | 15→25 | sandstone dome, brick trim | eater variant is a rubble/salvage downgrade with no light — thin for the Eater ruling |
| weeptap/gallery (S/M) | tapped seam | 4→12 | limestone + casing, drain anchor | underground, zero authored light |
| cistern(vault) (M) / reservoir (L) | capacity only — never claims production | 256→768 drams / open water | sandstone | vault enclosed and unlit; reservoir no Contents |
| waterworks (XL, workshop) | cistern, court, channel, one thing | capacity + luxury | brick + torchpost | none — first lit water tier |
| condensery (XL, foundry) | recovered solar-still machine | water 50/day, best in game | metal cold-face + concrete + Techlight1 | Techlight1 gated foundry here, arclight everywhere else (unify) |
| waterbaronsgaugehouse (creed S) | sealed measures, sluice lock | order only — zero water | sandstone | a regulatory office wearing the storage type; no Contents |

## Food

| Building | Purpose | Resources | Materials | Gaps |
|---|---|---|---|---|
| plot(rows) / field(rows) (S/M) | kitchen patch / eating on purpose | crop rows, no store | dirt + brinestalk stakes | none — correctly bare |
| larder (S) | the settlement's dry chest | food:2 capacity | slatted/vented timber | ZERO interior fixtures in a building whose function is storage |
| granary (M) | a good year kept into a bad one | food:9 | brinestalk + staddle stones | none |
| grange (L) / homefarm (XL) | threshing + barn / the quarter the city eats from | food 26 / 40 | wood threshing floor / mill metal + grading table | none — grading table is the family's only work surface |
| sporecellar / fungalvault / vaultgalleries | fungal & deep runs | food 9/6/18 | limestone, no lamp (doctrine) / one torchpost per 12-cell run | second light when runs grow |
| realmgranary (XL megastructure) | raised stores + crop court + mill | food:12 + wealth | wood + metal screens, in/out baskets, fresh cistern | creed variants are layout-true |
| arcologyterrace (M, arclight) | sealed hydroponics | food:14, growbeds ×14 | hex metal floor + growth Techlight1 | none |
| creed kitchens (joppa, kyakukya, svardym, mopango, farmers, yd) | seed/spice/brine/refuge/commons/bower | food 3–5 | hands fabrics, hearths real | one basket blueprint plays seed bin, spice jars, and four "labelled" bins |
| — family-wide | | | | no r_KingdomFurnishings_Food population table exists — every food building ships without the bonus roll all other families get |

## Craft / Power / Defense

| Building | Purpose | Materials | Gaps |
|---|---|---|---|
| toolshed / grindmill / workshop / yards | hands→salvage production | culture-correct | zero light in the entire family |
| smithy → forge (M) | the settlement's smithing | anvil declared stone/hands at BOTH tiers | never worked metal even after the smelter exists; no foundry smithy anywhere |
| smelter (M) | makes workedmetal | sandstone + salvage shield | owns none of its product as fabric; NO output store |
| butcherslab | butchery | timber + gutter | no meat storage anchor |
| vathouse / hallsurgery | preserving / surgery | walls hands under a "workshop" palette name | under-reach |
| graftinghall (L, foundry) | body grafting | genuinely foundry metal | light is an unlit sconce mesh |
| chimerictheatre / becomingannexe / mirrorgate (XL, arclight) | surgery / becoming rite / interdiction gate | metal + security doors + Techlight1 | none — the positive exemplars |
| deepbore / greatfoundry (XL, foundry) | drilling / casting | metal machinery in masonry shells | zero authored light (furnace glow only); no eater variants |
| power family (mill, waterwheel, sailvane, saltstore, robotchargebay) | five buildings, one tier each | matches rungs | no progression, no lights; robot bay scrap forever |
| barracks (L, Town) + 4 creed watch-huts | garrison + watches | hands/salvage | barracks has NO light and NO heat with 4 bunks; every watch-themed building is unlit; no M garrison, no fortification line |

## Civic / Faith / Memorial / Knowledge

| Building | Purpose | Materials | Gaps |
|---|---|---|---|
| fire → oven (S) | where the meal happens | hands, half-stone oven | XL realizations add pure yard padding, zero furniture |
| bench / hall (S/M) | argument ground / charter read aloud | hands; eater hall variant exists | hall unlit; eater hall = rubble + archway, ZERO metal/tech/light (worst Eater violation — the starter hut's eater upgrade carries 57% scrap, the civic hall none) |
| bazaar (M) / bathhouse (L) | market court / the town's showpiece | awnings + counters / brick + marble, hot & cold basins | both unlit — the bathhouse is the file's worst lighting gap |
| heart chain: basin → waterstone → moot → court → arcology(stub) | the founding rite ground, wrapped rung by rung | retained fabric law holds cell-for-cell | ceremonial centre of the whole settlement: unlit at every rung |
| caravanserai (L) | one gate wide enough for a laden dromad | canvas awnings, trough | common style only — no variant for anyone else |
| registryoffice / factorhouse / crownhall (Deep) | clerk / counting room / dais of the kingdom | registry lit, crownhall lit; factorhouse dark | the counting room is the civic building that most needs a lit desk |
| shrine → shrinegarth / temple (S/L) | offerings / seen from the road | kerb + marble; fungal variant has a named dark-ambulatory | the offering anchor is a bare ground tile — no altar, no bowl, nothing held; temple unlit so the authored darkness reads as oversight |
| reliquary (L, Mechanimist covenant) | one machine, cased and lit | salvage + relic light | case AND relic are the same generic wall blueprint |
| cairn / grave-grove / nichetomb | markers | fieldstone / saplings / cut rock | correctly unlit; no inspectable lore (vanilla RevealVillageHistoryOnLook idiom unused) |
| bookshelf / scriptorium | archive / two registers side by side | timber shelves | shelves are empty (no MarkovBookshelf content) and both reading rooms are unlit — the clearest work-surface light violation in scope |
| assentingmoot / stasisvault (XL, sealed) | consent congregation / body custody | tech props genuine; WALL RUNG TAGS WRONG (HalfStone inflated to workshop; ConcreteWall deflated two rungs below its own catalogue consensus) | both arclight-gated buildings totally unlit |

## Creeds (30) — all topology-true, none a recolour; standing gaps

All 30 verified distinct in topology and anchors; two carry real light (kyakukya spice-hearth,
mopango paired hearths); 14 walled interiors are unlit. Named idiom gaps: **dromad** "travelling
frames" has zero structure (the canvas awning slot already exists in trade-caravan-hands);
**baetyl** "offering frame" has no frame; **gyre** bone/chitin exists nowhere in the materials
vocabulary; **goatfolk** generator filler reuses the anchored horn-post unanchored, faking extra
challenge rings; **chavvah** bough-school is an open yard where vanilla's living-wall segments
could make a true grown cell; **mechanimists** have no small building — their processional
great-foundry ring variant is doctrine-correct, and vanilla's unused Six Day Stilt pantheon
stands ready if one is ever wanted.

## Master gap list (build order)

**B1 — data bugs and the lighting backbone (cross-cutting):**
1. Rung-tag bugs: reopened-stasis-arclight/wall (ConcreteWall two rungs under catalogue
   consensus), reopened-assent-arclight/wall (HalfStone inflated), production-smith-salvage/anvil,
   craft underbench DoorMetal, Techlight1 foundry-vs-arclight unification. These are lintable —
   a blueprint-rung-consistency checker floor lands with the fix.
2. The lighting law, catalogue-wide: hands interiors with working fire = Campfire-derived glow;
   workshop+ interiors/entrances/work surfaces = torchpost; foundry/arclight = Techlight1.
   Priority: heart chain, bathhouse, temple (a lit counterpoint makes the fungal dark-ambulatory
   deliberate), scriptorium/bookshelf, factorhouse, barracks (+ heat), both reopened halls, all
   watch posts, the three dark XL megastructures.

**B2 — Eater fidelity (author ruling):** civic-hall-eater to scrap/metal + metal door + light;
stasisvault and assentingmoot wall retags + lights; crownhall-eater concrete/metal + Techlight1;
airwell-eater retained-machine relic + light; template = mirror-gate/becoming-annexe/condensery.

**B3 — furniture answers the four questions:** terrace per-door sets; housecourt hearths/storage;
larder shelving; smelter output store; butcher meat store; the nine housing + three water
Contents gaps; the missing Food furnishings population table; container differentiation (sealed
jars, distinct tap, relic case, dromad awning, baetyl frame, shrine altar/bowl — vanilla
compositions first); knowledge shelves gain readable content; arcologyward service-core marker;
goatfolk generator filler switched to inert practice props.

**B4 — expansions (docketed, next increment, custom art queue):** foundry smithy tier; creed
Level-1 progressions (robot charge bay first); defense line (M garrison, fortifications); gyre
bone/chitin material + screen blueprint; goatfolk trophy rack (no vanilla precedent — genuinely
new art, queued for the author's art review per the custom-furniture ruling); eater variants for
deepbore/greatfoundry; memorial lore-on-look.

## Custom furniture policy (author ruling, 2026-08-30)

Custom fixtures are permitted when the art is good: vanilla-quality raster, truthful
source/licence/method rows in [Art/runtime-assets.json](../Art/runtime-assets.json), a text
glyph fallback, and the author's review before landing (see
[ASSET_PROVENANCE.md](ASSET_PROVENANCE.md)). Vanilla-tile composition remains the first choice;
everything in B3 ships that way. B4's genuinely-new props (trophy rack, bone screen) enter the
art review queue.
