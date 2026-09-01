# Building Dossiers — the catalogue, mapped

> Method (author ruling, 2026-08-30): map every building out in an understandable form — what it
> DOES, who visits and how many, what resources it holds, what materials its rung and culture
> demand, what furniture follows from all of that — THEN build the fixes in. This document is
> that map, assembled from three full-catalogue research passes over the Architecture XMLs,
> [RuntimeData/KingdomBuildings.xml](../RuntimeData/KingdomBuildings.xml) (Cost/Staff/Carries),
> [RuntimeData/ObjectBlueprints.xml](../RuntimeData/ObjectBlueprints.xml), vanilla Qud data, and
> [the polish contract](../_notes/ARCHITECTURE-POLISH-CONTRACT.md) §§6–8.
>
> Reading key: VISITORS = Staff/CrewNeeds (who works it) plus Carries (the population-lift ceiling;
> exact physical providers determine the live amount the game actually counts). `Contents=` is
> legacy semantic metadata: current authored maps place exact fixtures and skip the roll, while
> compatible legacy/third-party plots without a current realization retain the table fallback.
> Rung ladder: hands → salvage → workshop → foundry → arclight
> (formal enum in [KingdomZoningDeclarations.cs](../Growth/KingdomZoningDeclarations.cs)).
>
> **2026-08-31 geometry/transition update:** this remains the purpose-and-furnishing baseline, but
> its old universal core-retention assumption is superseded. S/M/L/XL are now 6x4, 8x6, 12x10,
> and 20x18. Every later tier declares additive, additive-expand, renovate, renovate-expand, or
> replacement intent. Only protected/state-bearing objects are universally retained. Larger-lot
> maps have been re-censused after full-lot regeneration: 144 buildings, 134 plotted buildings,
> 89 palettes, and 333 maps (187 source / 146 generated). A gap below is not closed until the live
> map and this dossier agree.

## Family theme laws

| Family | Theme law |
|---|---|
| Housing | Shelter scales honestly canvas → arcology; every tier keeps sleep + storage (+ heat once crewed); table/seat are status furniture gated to the marble tier. |
| Water | Storage stores, producers produce; three honest lanes (salt-pan sun, sky/dew, underground seep) climb hands → foundry; the condensery is the catalogue's model top tier. |
| Food | Growing and dry-keeping split from cooking; nothing automates — the ladder buys a better hand-rate, never the hands' discharge. |
| Craft | The one real hands→foundry production spine — its buildings must wear their own products (a smithy in a worked-metal city forges on worked metal, not a stone anvil forever). |
| Storage (type) | An overloaded socket (water / larder / trade / regulatory) — same-set replacement is contract-legal but function drift must stay visible. |
| Power / Defense | Working and watching happen at night: these families carry lights or they are lying about their function. |
| Civic / Faith | Rooms face seating and circulation toward the counter, dais, basin, or altar; the heart keeps its exact founding basin/protected state while each authored tier may renovate replaceable fabric or expand. |
| Memorial | Markers, not workplaces: open-air, hands-rung, unlit by design (a deliberately cold brazier is idiom; a missing lamp is not). |
| Knowledge | Archives face a lit reading/copy surface and hold inspectable content — an empty shelf is scenery. |
| Creed practices | Topology/use/access vary, never colour alone — horn rings, moon courts, paired hearths, wet nurseries, travelling frames. Each creed owns one complete practice; Robots alone have an authored charge-bay -> service-bay successor. Larger campuses are size realizations, not free tiers. |
| Eater / Reopened | METAL WALLS, METAL FLOORS, LIGHTS, TECH (author ruling). Templates that already comply: mirror-gate (all variants), becoming-annexe, condensery, chimeric theatre. |

## Player-designated building boundary

The shipped catalogue marks 51 designs adoptable. This is a physical-proof set, not a convenience
allowlist:

- Every ordinary enclosed housing tier and culture shelter may be designated. Real `Bed` objects
  provide roof capacity, capped by the chosen role; extra quiet/damp/luxury still needs its exact
  stocked fixture.
- Enclosed ordinary work rooms are toolshed, charging post, smithy, forge, forge-hall, workshop,
  oven, moot hall, bathhouse, shrine, shrine-garth, temple, keeper's shelf, scriptorium,
  watch-house, barracks, Hindren weave-hall, caravanserai, under-bench, and factor's house. Their
  benefit furniture has a takeable merchant-stock route. Staffed rooms use a signed current crew
  contract; a room name never supplies the benefit.
- Larder is the sole adopted storage role. Its root is the exact dry container, and its actual
  inventory remains food authority. Liquid vessels do not pass as larders.
- Ordinary open yards/grounds use exact catalogue-sized rectangular membership and no invented
  shell. Network, crop, power-plant, refinery, laboratory, fixed-creed, Heart, remote,
  purpose/crown, and hosted roles remain authored until their real machinery/topology has a typed
  designation contract. This records today's proof boundary, not a permanent family ban.

Semantic fixtures are design-bound: a tagged forge object cannot fill a smithy cap. Untagged
native capabilities remain reusable where physically true (beds, shrines, readable shelves,
chargers, and liquid). The catalogue is a maximum; removing or breaking furniture, losing crew,
breaking the designation root, or changing the room immediately scales or removes live supply.

## Housing

| Building (tier, size) | Purpose | Visitors | Materials @ rung | Furniture | Gaps |
|---|---|---|---|---|---|
| tent → tentrow (S footprint; S/M/L/XL reservation) | first-night shelter / widened canvas bay | household 1–3 | canvas, then one timber brace; no light | bedroll(s), basket | larger reservations keep the paid compact shelter and add only a restrained natural dooryard; they do not pretend to be larger buildings |
| hut → hutyard (S footprint; S/M/L/XL reservation) | first crewed household | 2–5 | brinestalk/timber, then stone edging | + hearth and terminal yard seat | larger reservations use a short return court; a fuller in-place estate would need a new paid successor |
| house → housecourt (M) | settled household(s); court = 3 families | 8 → 18 | sandstone | bed×2, hearth, storage | court: 3 households share one hearth, no per-family storage |
| terrace (L, workshop, Town) | a whole street raised at once | 4 households, 4 doors | wood floor + sandstone | 4 beds, shared storage + hearth | each household threshold now has its own sleep place; shared services remain deliberate |
| finehouse (M, Village) | a notable's dwelling | 1 household | marble floor | + table/seat (status, correctly gated) | none |
| manor (L) / court (XL, City) | gated estate / a quarter under one roof | guests / dozens | marble/stone | full sets (court per wing) | none |
| mudhut(+court), caproof, stairfold house (`bonefold` legacy key), blockhut(+yard), stiltrow, ydroofline | style-exclusive compact shelters (mud / fungal / Moon Stair / eater / verdant / yd) | households | each ground's hands fabric (deliberate: no lamp in the cap; the stairfold uses canvas, marble sill, and local crystal ribs) | sleep + storage | mud and block larger reservations now use distinct compact apron/corner grammar without unpriced furniture; none of these six carries legacy Contents metadata, and every current map owns its exact fixtures |
| carvedcell/gallery (S/M, deep) | cut rock is the wall | households | limestone (natural) | full authored set; legacy Contents metadata retained for compatibility | none |
| arcologyward (L, arclight) | sealed vertical lodging | eight installed beds (roof ceiling 8) | concrete + security door + Techlight1 | metal bed/shelf | service-core anchor is adjacent-usable but still an intentionally understated wall fixture |
| strangersguestscreen / reshephhospice (creed S) | anonymous lodging / open hospice | transients / the sick | canvas screens | pallets (+tending basins) | no legacy Contents metadata; hospice borrows water anchors without water naming |

## Water

| Building | Purpose | Resources | Materials | Gaps |
|---|---|---|---|---|
| saltpan → saltterrace (S, hands) | brine evaporation | water 2→4/day | half-stone + basin | no legacy Contents metadata at the ladder's entry |
| catchment(bank) (S, salvage) | dew | 3→5 | worn wood + scrap basin | none |
| airwellcourt/field (M/L, workshop) | domed air-wells | 15→25 | sandstone dome, brick trim | Eater reuse has rubble domes, rust trim, scrap basins, and a live retained-machine Techlight1 readout |
| weeptap/gallery (S/M) | tapped seam | 4→12 | limestone + casing, drain anchor | underground, zero authored light |
| cistern(vault) (M) / reservoir (L) | capacity only — never claims production | 256→768 drams / open water | sandstone | vault enclosed and unlit; reservoir has no legacy Contents metadata |
| waterworks (XL, workshop) | cistern, court, channel, one thing | capacity + luxury | brick + torchpost | none — first lit water tier |
| condensery (XL, foundry) | recovered solar-still machine | water 50/day, best in game | metal cold-face + concrete + Techlight1 | none — its foundry Techlight1 gate is now the catalogue-wide law (B1) |
| waterbaronsgaugehouse (creed S) | sealed measures, sluice lock | order only — zero water | sandstone | a regulatory office wearing the storage type; no legacy Contents metadata |

## Food

| Building | Purpose | Resources | Materials | Gaps |
|---|---|---|---|---|
| plot(rows) / field(rows) (S/M) | kitchen patch / eating on purpose | crop rows, no store | dirt + brinestalk stakes | none — correctly bare |
| larder (S) | the settlement's dry chest | food:2 capacity | slatted/vented timber | real empty dry-goods shelving; no phantom starting inventory |
| granary (M) | a good year kept into a bad one | food:9 | brinestalk + staddle stones | none |
| grange (L) / homefarm (XL) | threshing + barn / the quarter the city eats from | food 26 / 40 | wood threshing floor / mill metal + grading table | none — grading table is the family's only work surface |
| sporecellar / fungalvault / vaultgalleries | fungal & deep runs | food 9/6/18 | limestone, no lamp (doctrine) / one torchpost per 12-cell run | second light when runs grow |
| realmgranary (XL megastructure) | raised stores + crop court + mill | food:12 + wealth | wood + metal screens, in/out baskets, fresh cistern | creed variants are layout-true |
| arcologyterrace (M, arclight) | sealed hydroponics | food:14, growbeds ×14 | hex metal floor + growth Techlight1 | none |
| creed kitchens (joppa, kyakukya, svardym, mopango, farmers, yd) | seed/spice/brine/refuge/commons/bower | food 3–5 | hands fabrics, hearths real | Joppa hamper, Kyakukya fired jar, snapjaw hide-bound cache, and farmers' timber bins are distinct usable containers; all begin empty and generated campuses use matching sealed silhouettes, never cloned storage |
| — family-wide | | | | `r_KingdomFurnishings_Food` exists as an empty-surface legacy/third-party fallback; current authored food maps place exact fixtures and deliberately skip the roll |

## Craft / Power / Defense

| Building | Purpose | Materials | Gaps |
|---|---|---|---|
| toolshed / grindmill / workshop / yards | hands→salvage production | culture-correct | unlit, and honestly so under the lighting law (hands/salvage, no fire function); any later lit successor needs its own fiction, gate, bill, and authored programme rather than a generic ladder |
| smithy → forge → forge-hall (M) | the settlement's smithing | the first two rungs retain the paid stone striking block; the foundry renovation adds worked-metal banked forge, casting anvil, quench basin, output rack, fire-door, dressed floor, and work light | complete at static/content scope; native heat-zone and silhouette review remains |
| smelter (M) | makes workedmetal | sandstone + salvage shield | dedicated empty scrap-output locker in the loading bay |
| butcherslab | butchery | timber + gutter | dedicated empty meat basket/store beside the work face |
| vathouse / hallsurgery | preserving / surgery | walls hands under a "workshop" palette name | under-reach |
| graftinghall (L, foundry) | body grafting | genuinely foundry metal | light is an unlit sconce mesh |
| chimerictheatre / becomingannexe / mirrorgate (XL, arclight) | surgery / becoming rite / interdiction gate | metal + security doors + Techlight1; annexe uses four spaced sconces with one durable anchor | none — the positive exemplars |
| deepbore / greatfoundry (XL, foundry) | drilling / casting | metal machinery in masonry shells | Techlight1 at work face/hall in every map; explicit Eater retained-machine variants now ship |
| power family (mill, waterwheel, sailvane, saltstore, robot charge-bay → service-bay) | four one-tier works plus one two-tier robot chain | matches rungs | robot service-bay renovates the open scrap charging rail into an enclosed workshop inspection bay; saltstore tap is torch-lit, while the open-air hands/salvage works remain honestly unlit |
| watch-house (M, Village) -> barracks (L, Town) + 4 creed watch-huts | garrison ground + watches | salvage -> workshop | explicit `renovate-expand`: two duty slings, mess table, hearth, and occupied scrap locker retain exact root-relative cells; the empty roster board is replaced in place while two slings, divided rooms, service door, and lit barracks muster are added. These plots grant order, not wall Defence |
| palisade / re-stood course -> rampart; watchtower; gatehouse | physical frontier fabric | hands -> stone Village fortification | the two cheap wall idioms pay explicit stone deltas into the shared rampart; watchtower and road-straddling gatehouse remain separate staffed network works |

## Civic / Faith / Memorial / Knowledge

| Building | Purpose | Materials | Gaps |
|---|---|---|---|
| fire → oven (S) | where the meal happens | hands, half-stone oven | XL realizations add pure yard padding, zero furniture |
| bench / hall (S/M) | argument ground / charter read aloud | hands; eater hall variant exists | hall carries a gathering hearth; Eater hall uses recovered metal fabric, a metal door, and retained Techlight1 |
| bazaar (M) / bathhouse (L) | market court / the town's showpiece | awnings + counters / brick + marble, hot & cold basins | ~~bathhouse unlit~~ torch-lit at corridor and entrance (B1); the open-air hands bazaar stays honestly unlit |
| heart chain: basin → waterstone → moot → court → arcology gateway | the founding rite ground transformed around one protected basin | S/M/L/XL plans use authored renovation/expansion; protected basin stays exact, static fabric need not | basin/waterstone are honestly unlit at hands; later civic lighting is tier-appropriate. The gateway is only the surface root of the separately specified 27-zone arcology |
| caravanserai (L) | one gate wide enough for a laden dromad | canvas awnings, trough | common style only — no variant for anyone else |
| registryoffice / factorhouse / crownhall (Deep) | clerk / counting room / dais of the kingdom | registry and crownhall lit; factorhouse uses a hands-gated counting hearth | complete: the counting room has a real campfire light without falsely raising its commission technology |
| shrine → shrinegarth / temple (S/L) | offerings / seen from the road | kerb + marble; fungal variant has a named dark-ambulatory | shrine and renovated shrinegarth now retain a real stone offering bowl; temple sanctum is torch-lit, so the fungal dark-ambulatory reads as designed contrast |
| reliquary (L, Mechanimist covenant) | one machine, cased and lit | salvage + relic light | case is a native open display cabinet; relic is a separate inert machine/plinth silhouette; neither contains loot, power, or machine behavior |
| cairn / grave-grove / nichetomb / cragmensch stone-garden | markers | fieldstone / saplings / cut rock / listening slabs | correctly unlit; each now uses Qud's monument look idiom (`RulesDescription` + `SmartuseLooks` + `Interesting`) for distinct inspectable lore. `RevealVillageHistoryOnLook` remains reserved for registered fixed-history IDs, not invented realm secrets |
| bookshelf / scriptorium | archive / two registers side by side | timber shelves | dedicated archive/register shelves now generate persistent readable Markov books; rooms remain deliberately unlit at the hands gate |
| assentingmoot / stasisvault (XL, sealed) | consent congregation / body custody | tech props genuine; wall rung tags corrected to catalogue consensus (HalfStone stone/hands, ConcreteWall shapedstone/foundry) | ~~totally unlit~~ Techlight1 at thresholds and work surfaces (B1) |

## Creed and affiliation works (33) — all topology-true, none a recolour

All 33 affiliations have a distinct complete work (34 designs because Robots own a two-tier
chain), each verified in topology and anchors; two carry real light (kyakukya spice-hearth,
mopango paired hearths), and the four watch posts now carry watch-fires (B1). The remaining
walled interiors are unlit and stay so honestly: at hands/salvage the lighting law grants fire
only where the function includes it, and none of their functions does. Named idioms are physical
and contract-bearing: **dromad** has four canvas awning-frame points and two open caravan exits;
**baetyl** has a four-upright recovered-metal measuring gantry; **Gyre Wights** have four native connected
bone-wall screens, described truthfully as vowed bone/chitin over its paid fieldstone spine;
**goatfolk** keeps the four anchored challenge posts only in the authored ring while generated
campuses extend the witness line with inert canvas pennons; **chavvah** encloses its school in
native crystalline-trunk fabric around two reachable teaching trunks and a double entrance.
**Mechanimists deliberately receive no small proxy:** their one admitted-creed work is the L,
Town/workshop-gated reliquary. Reducing machine curation to an early decorative shrine would
duplicate the vanilla Six Day Stilt pantheon and erase the covenant's material/standing choice.

## Master gap list (build order)

**B1 — data bugs and the lighting backbone (cross-cutting):**
1. ~~Rung-tag bugs: reopened-stasis-arclight/wall (ConcreteWall two rungs under catalogue
   consensus), reopened-assent-arclight/wall (HalfStone inflated), Techlight1
   foundry-vs-arclight unification.~~ DONE (B1 build wave). Still open, contested at root:
   production-smith-salvage/anvil and craft underbench DoorMetal — either retag raises the
   owning building's commission gate (smithy is hands-gated, underbench workshop-gated in
   KingdomBuildings.xml; the checker's palette.tech-underdeclared floor refuses a used slot
   above the building's declared MinTech). The blueprint-rung-consistency lint floor is also
   still owed (Tools/ is root-owned).
2. ~~The lighting law, catalogue-wide~~ — B1 landed: heart chain (tier-appropriate torchposts and
   court/gateway lights; later redesign may renovate their exact positions),
   bathhouse, hall hearth, temple sanctum (the fungal dark-ambulatory stays dark and now reads
   as designed contrast), barracks (torchpost + hearth), saltstore tap, both reopened halls
   (Techlight1), all four creed watch posts (watch-fires: snapjaw trail-den, Issachari
   rifle-porch, Wardens' lodge, entropy blind), the three dark XL megastructures (Techlight1 in
   every variant map). Still honestly unlit under the law's own rungs — a hands-tier room with
   no fire function cannot carry a workshop light slot without raising its commission gate:
   scriptorium, bookshelf, factorhouse counting desk, toolshed, grindmill (hands); workshop,
   smelter, butcherslab (salvage, non-watch); the walled creed interiors whose functions exclude
   fire. These remain truthful at their current gate; a future lit successor must independently
   justify its fiction, higher technology/material bill, provider programme, and authored map.

**B2 — Eater fidelity (closed static gate):** civic hall now uses scrap/metal, a metal door, and
retained light; stasis vault, assenting moot, and crown hall carry their recovered fabric and
lights; air-well reuse has a salvage-gated live machine readout; deep bore and great foundry have
explicit Eater variants. Native appearance review remains required.

**B3 — furniture answers the four questions:** closed exact gaps include terrace per-door sleep,
larder shelving, smelter output storage, butcher meat storage, readable knowledge shelves, and
usable arcology service/cold-face anchors. Creed food-container differentiation is also closed;
no empty wrapper is represented as stocked inventory and no generated campus clones a container.
The 105 semantic benefit fixtures now resolve to 33 role-readable vanilla render signatures (largest
shared class 15) instead of one material table/cabinet idiom; representative industrial, ritual,
water, memorial, crown, and megastructure roles are exact-test pinned. No custom raster was needed.

**B4 — progression reconciliation (closed static/content gate):** smithy->forge->forge-hall and
robot charge-bay->service-bay ship as explicit renovate chains. Watch-house->barracks now ships as
an M->L `renovate-expand` lineage, separate from palisade/re-stood-course->rampart frontier fabric.
The other 32 creed works are intentionally complete standalone practices: Addendum 16 promises one
unique building per creed, not an invented generic Level-1. Future successors require their own
fiction, bill, gate, provider programme, and authored map. Native transition appearance remains
required.

## Custom furniture policy (author ruling, 2026-08-30)

Custom fixtures are permitted when the art is good: vanilla-quality raster, truthful
source/licence/method rows in [Art/runtime-assets.json](../Art/runtime-assets.json), a text
glyph fallback, and the author's review before landing (see
[ASSET_PROVENANCE.md](ASSET_PROVENANCE.md)). Vanilla-tile composition remains the first choice;
everything in this pass ships that way. No custom raster was needed: goatfolk uses the native
banner silhouette as a fixed challenge pennon, the Gyre Wight work uses Qud's connected bone-wall paint, and
Chavvah uses the native crystalline-trunk wall idiom.
