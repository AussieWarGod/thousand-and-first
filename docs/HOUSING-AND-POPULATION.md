# Building architecture and population implementation direction

Accepted user requirements and source audit, 2026-09-14, baseline `a6e23f74`.
This is planned work, not native acceptance. Follow [STATUS.md](STATUS.md) for evidence and
[CITY-GROWTH-BALANCE.md](CITY-GROWTH-BALANCE.md) for the complete Beta and land-use direction.

## Work and order

| Issue | Required result |
| --- | --- |
| [#229](https://github.com/AussieWarGod/thousand-and-first/issues/229) | Architectural quality across every building layout; enclosed homes are the first concrete defect. |
| [#230](https://github.com/AussieWarGod/thousand-and-first/issues/230) | Correct physical citizens, unique usable beds and home/work/travel bindings across one multi-map city. |
| [#231](https://github.com/AussieWarGod/thousand-and-first/issues/231) | Coherent culture/body/name/occupation selection weighted by player reputation and evolving city standing. |
| [#233](https://github.com/AussieWarGod/thousand-and-first/issues/233) | Room size, usable furniture, crowding and quality affect actual citizen activities and building outcomes. |
| [#234](https://github.com/AussieWarGod/thousand-and-first/issues/234) | Practical visual layout authoring and physical room/space analysis using the existing XML contract. |
| [#251](https://github.com/AussieWarGod/thousand-and-first/issues/251) | Every building meets architectural and functional standards before Beta, with explicit configuration/behavior coverage and complex native proof. |

[BUILDING-BEHAVIOUR.md](BUILDING-BEHAVIOUR.md) provides the shared catalogue inventory and
coverage-link workflow. It exposes missing/stale mappings without treating static validity or
a generic construction scenario as proof that every building works.

Audit the whole building catalogue; deliver housing and Quickstart reliability first, then ordinary multi-map home/work behavior and
reputation-driven arrivals in reviewable Alpha slices. Preserve the original Beta goal and complex
in-game coverage. Hearthpyre runtime compatibility remains deferred until Beta; visual inspiration
does not reopen that work. No new manual testing or approval gate is introduced.

## What the current evidence establishes

Native30's full accepted archive is
`beta-heart-chain/a6e23f74/paid-court-renovation-chain-1/result.json`, SHA-256
`e2eeb784ada3394244c3d5683098902a2aa937155385b1b629bd6e6249381063`.
It proves the paid heart 1→2→3→4 chain, controlled construction refusals/recovery and a subsequent
ordinary day. The setup supplies 50 real resident bodies, 18 authored three-bed tentrows (54 beds),
eight synthetic legacy water works and extra supplies. Bare granary/water roots in that fixture
are synthetic support, not examples of fully commissioned buildings. This is not ordinary-play
population balance, a housing quality review, multi-map acceptance or higher-heart cold load.

The housing complaint is nevertheless a real authored defect. `housing-tent-s0` and
`housing-tentrow-s1` in `Architecture/KingdomArchitectures-HousingWater.xml` have open L-shaped
walls, an entrance marker without a physical door, and exterior storage. At that baseline the generator's compact
housing policy retained that small shelter on larger reservations. The current draft replaces new
M/L/XL canvas and hut conversion plans with enclosed shared rooms, chambers and courtyard
cabins; S remains a retained reader. All basic new hut families now also require M ground. See STATUS for current validation and unresolved save/paid-construction gaps. `housing_embodiment_test.py`
counts literal sleep providers and non-open cover, while `TryValidateEnclosure` exempts Soft
cover. Those checks cannot establish an enclosed tent. Audit the entire housing catalogue, its
variants and generated lots; changing only these two maps is not the complete task.

At the older audited baseline, `RuntimeData/PopulationTables.xml` supplied nine settler variants with fixed weights. Seven
BaseFarmer descendants have 90% of the total weight; Mechanimist and Snapjaw bodies have 5% each.
`KingdomSemanticSelection.TryPreparePerson` chooses blueprint, origin and shared-grammar name
separately. Appearance alone does not establish an occupation or a coherent culture. The development branch now adds a version-two founding snapshot in
`Core/KingdomFounding.02.FoundingStandings.cs`: new realms inherit current personal reputation
into their inbound standing ledger once, with outgoing policy separate. Existing realms and
version-one interrupted transactions retain their history. This foundation passes focused source/pure checks and a real founding, independent civic change,
native personal spillover, ordinary construction and cold-load chain. Historical interrupted
founding and recruitment weighting remain pending. Exact evidence and synthetic probe inputs
are recorded in STATUS.md; public 0.3.7 retains its original behavior.

`KingdomResidents.ReadRoster` already retains residents bound to other zones and binds observed
bodies to the current zone. Its local home lookup is not proof of complete multi-map household
behavior. Audit home, job, bound-body and current-zone identities through real travel and loading.

## Recruitment implementation in progress

New arrivals now select among 12 authored profiles across six native factions. Fixed base
weights retain common workers and rarer specialists; actual player and inbound city regard
multiply each base weight by two bounded factors. A channel contributes
`100 + clamp(regard, -249, 1000) / 5` (integer truncation); zero regard contributes 100.
Native hostile feeling in either channel excludes the profile, even if the other channel is
favorable. These are provisional Alpha balance values, not a population or diversity quota.
Missing factions are excluded; invalid catalogue metadata refuses before allocation. No eligible
profile means no body, payment or frozen person; the existing due debt can retry when relations
improve. Existing frozen arrivals and citizens do not reroll. Public 0.3.7 is unchanged.

Blueprint metadata binds an ordinary one-body recruit to a single native source faction and
origin. Native culture/species tags and anatomy remain inherited. Qud's `NameMaker` supplies
culture/faction names inside `Stat.PushState`/`PopState`, with a separately restored naming RNG
and event-derived seed; no sample actor is created to select a name. First guests use the same
merged table but only the 12 bodies accepted by their durable owned-body contract. Ordinary
extension recruits need the same explicit metadata and native allegiance checks.

Installed 2.0.211.51 sources inspected: `Reputation.GetFeeling`, `Stat` RNG stack, `NameStyles.Generate`,
`GameObject.GetCulture/GetSpecies`, and `GameObjectBlueprint.GetTag/GetPartParameter`.
`Creatures.xml` establishes the native BaseIssachari, HindrenVillager, Dromad, Snapjaw,
Mechanimist and BaseFarmer inheritance. `Naming.xml` uses explicit faction/culture/species
scopes for naming. Freehold's [press kit](https://cavesofqud.com/press-kit/) describes creatures
as retaining skills, equipment, faction allegiance and body parts; the implementation uses
those native bodies rather than recoloring farmers. The linked GDC village-generation PDF
exceeded the web reader's size limit; it has not supplied additional inspected claims here.

Pure/engine checks pass. Native six-faction coherence, hostile pool and frozen-correspondence
checks are pending; actual admission/home allocation, cold load, multi-map recruitment and
broader balance remain required under #231/#230. This implementation does not close those issues.

## References inspected

Installed Caves of Qud 2.0.211.51 is the primary technical reference. Existing source hashes and
named-map references are in `Tools/architecture-quality-reference.json`. The audit inspected:

- `ObjectBlueprints/Walls.xml`: CanvasWall inherits Wall and is solid and occluding. The open
  shelter silhouette is caused by our placement, not an assumption that canvas cannot enclose.
- `ObjectBlueprints/Furniture.xml`: Door uses the native Door part. An entrance anchor by itself
  does not create a door or establish its open/closed behavior.
- `PopulationTables.xml`, `Villages_BuildingContents_Dwelling_*Default`: interior corners,
  inside walls and outside front walls have distinct furnishing hints. Sleeping, storage,
  lighting, seating and specialist work are deliberate spatial uses.
- Decompiled `VillageMaker`: hut placement keeps buildings spaced apart, chooses a real
  perimeter door and furnishes through the normal hut path. Its exact historical numbers are
  reference observations, not a population limit for this mod.
- Decompiled `VillageBase.getBaseVillager`: village faction and region influence candidate
  selection, with an outlier chance and exclusions such as merchants and
  `ExcludeFromVillagePopulations`. This supports a coherent local population with exceptions;
  it is not an instruction to copy all native random draws or admit arbitrary creatures.

Visual references actually viewed: the creator's
[Hearthpyre Workshop gallery](https://steamcommunity.com/sharedfiles/filedetails/?id=1683847053)
and Sleeps-Under-Asphalt's
[Temple-Over-Brightsheol](https://www.reddit.com/r/cavesofqud/comments/17zetpn/my_latest_hearthpyre_settlement/).
The player build shows distinct furnished rooms around generous common ground and a central
temple. Take spatial principles from those images; do not bundle their screenshots or copy art.
Vanilla named village layouts, population composition and naming rules still need the fuller
comparative audit required by #231; inspecting the existing reference manifest is not that audit.

## Design constraints

The user further requires a city and life-simulation approach: citizens need occupied rooms,
places to mingle, useful furniture and workplaces, and room/building quality must affect their
experience and the building's impact. Room size, available personal/shared space and furnishings
are inputs to real activity and outcomes, not merely decoration or labels. #233 tracks those
mechanics and their complex native tests. [LAYOUT-STUDIO.md](LAYOUT-STUDIO.md) describes the
authoring tool that supports the redesign; completing a workbench does not complete the gameplay.

Research supports connecting layout to use. The official
[Sims 4 manual](https://eaassets-a.akamaihd.net/eahelp/manuals/the-sims-4-ps4-ukanz.pdf), pages 12–14,
links furniture to needs, stresses doors and unobstructed movement, and distinguishes usable
objects from broken ones. Ludeon's historical
[Alpha 12 room-system design](https://ludeon.com/blog/page/16/) derives room roles and effects
from contents and space; its [1.5 announcement](https://ludeon.com/blog/2024/03/anomaly-expansion-and-update-1-5-announced/)
connects bookcases to reading and research. Our design inference is to couple room function,
usable space, furniture and citizen activities through Qud's existing systems. These sources
do not supply a copied formula, universal luxury requirement or a reason to add unrelated needs.

The architectural quality requirement applies to **every building**, not only housing. Design
must follow the building's actual program: the people, activities, equipment, storage and open
space it serves. Give it coherent proportions, spatial hierarchy, thresholds and circulation.
Reserve every furniture footprint as occupied, regardless of native walkability. Keep beds, chairs
and storage out of doorways and circulation; provide adjacent reachable clear floor for use. This
architectural rule applies to all building types and does not change Qud physics.
Arrange furniture and lighting deliberately, use materials and cultural expression appropriate
to Qud, and distinguish public approaches from private rooms and hazardous/service work areas.
Relate entrances and fronts to useful streets, courts, parks and neighboring works. Farms,
quarries, defences, industry, storage, water and civic buildings need equally considered layouts.

Enclosure is a functional choice, not a universal box requirement. Open workyards and fields
need intentional boundaries and access; enclosed functions need real walls and doors. Larger
lots and later tiers should earn their land through useful rooms, activity, capacity or public
space. Empty padding, random containers, decorative spam and unexplained gaps do not meet the
bar. Upgrades must form coherent buildings with honest material and space costs.

Do not design around easy code, an unchanged generator, a convenient fixture size or fixed test
coordinates. Review the entire family/tier/style/rotation catalogue and representative ordinary
city compositions. Automated topology and behavior checks provide evidence; no metrics score,
two corrected tents or isolated screenshot can sign quality for every building. Record the
remaining catalogue gaps explicitly and keep useful three-cell streets and natural expansion.

Physical enclosure and roof quality are separate facts: a canvas room can be enclosed while
remaining Soft quality. An open court or camp must not gain enclosed-home status by a label.
Beds need actual access and a coherent interior. Preserve storage contents, existing dwelling
identities, frozen paid layouts and founder recovery when new plans change size or material bills.
Use the available plot space and adjacent maps. Do not shrink homes to make the old 50-body
fixture fit, or introduce a hard per-map resident/tier gate.

For migration, distinguish a person's permanent home, work destination and current location.
Residents can travel for work and errands; they must not teleport to the founder or be counted
twice. Unloaded ground cannot be treated as a fresh empty neighborhood. Full local housing and
spare housing elsewhere in the same city must have an explicit lawful arrival/relocation route.

For recruitment, choose a coherent eligible culture/faction and native body, then an appropriate
name and plausible profession. Workers can be common and specialists rarer without fixed diversity
quotas. More favorable player/city reputation should increase voluntary arrival weight; ordinary
friendly arrivals should exclude hostile factions. Deliberate recruitment or diplomatic exceptions
must remain explicit. Respect vanilla body/anatomy and naming conventions; do not clone unique
named NPCs or treat race, faction, profession and creed as interchangeable fields.

Freeze city reputation inheritance at founding and preserve later kingdom-driven differences.
Keep faction regard for the city distinct from outgoing city policy. Do not overwrite established
city standing with player reputation every pass/load. Existing cities need an explicit migration
that preserves their history; existing citizens keep their names, bodies and identities. Frozen
arrivals keep their original choices across reputation changes, retry and cold load. Weights,
thresholds and migration formula are not selected yet and need tests before becoming policy.

## Automated acceptance

1. Every building family/tier/variant/pose: functional program, reachable fixtures, coherent
   enclosure or intentional outdoor boundaries, circulation, material/custody correctness,
   useful larger plots and upgrade continuity. Exercise ordinary city arrangements as well as
   isolated buildings, including streets, neighboring uses and multiple claimed maps.
2. All housing tiers/variants/poses: literal reachable bed count, physical enclosure, real door
   behavior, usable storage and interior circulation. Include open shelter, missing wall/door,
   blocked entrance, destroyed bed, repair and occupied renovation failure/retry cases.
3. Ordinary Quickstart and paid commissioning: four founders remain recoverable, first citizen
   can be housed and construction can proceed. Capture the full native view, exercise actual
   use and save/quit/fresh load; a static geometry PASS alone is insufficient.
4. Several claimed maps of one city: real homes, work and support economy; exact bodies and bed
   assignments across ordinary day/night travel, absence, obstruction, relocation and cold load.
   Separate cities, unclaimed travel and synthetic stress populations do not satisfy this case.
5. Reputation selection: deterministic fixed-seed draws, monotonic weight changes, hostile and
   all-ineligible cases, missing factions, coherent names/bodies/cultures, specialist frequency,
   inherited baseline exactly once, later city effects and frozen identity through changed
   reputation and reload. Follow with actual native arrivals and home allocation.

Use the shared focused-check workflow before the relevant native scenarios. Record which data
is synthetic, exact failing predicates and the ordinary-play behaviors actually witnessed.

## Physical lodging implementation in progress

The `codex/physical-room-lodging` slice records operable sleeping-provider coordinates at benefit
allocation, measures rooms with adoption structural observations plus furniture clearance, and constrains
lodging closeness by real room separation and usable floor. A spacious shared room cannot become
Private by its footprint, and `Closeness` declarations can only reduce physical privacy. Adopted
floor-only receipts may use adjacent walls as boundaries without acquiring their ground. Missing
walls, locked entrances and obstructed floor have distinct physical consequences.

Pure tests cover shared and paired bedrooms, partition removal, blocked/locked/missing entrances,
furniture barriers, rotated plans, adopted floor-only authority, undesignated gaps, multiple places
on one provider and invalid capacities. This is a foundation for #233, not its completion. Usable
floor excludes every native Furniture-tagged object, bed and chair, including the settlement
marker. Sleeping providers also reserve their footprints even without a Furniture tag. Beds need
adjacent reachable floor, and furniture cannot provide ingress. This is not a decoration score.
The native twenty-case scenario at `765e8e59` passed the actual index/privacy route, door states,
furniture obstruction, trapped-bed access, wall/bed damage and repair, founder occupancy and
capped-arrival refusal. See STATUS.md for its full closed archive. This is one synthetic room;
the later native29 scenario at `dc65f086` adds shared-hall connectivity across a synthetic designated
building with native furniture and doors, then restores the adopted benefit authority. It covers
only the building and its immediate exterior approach. Street access beyond that boundary, ordinary
paid catalogue layouts, cold load and historical reservation/cost migration remain required work.
The modern marsh Quickstart chain has separate scoped evidence in STATUS. No release acceptance is claimed.

Privacy counts operable sleeping providers before enrollment-cap allocation, so an extra usable
bunk cannot disappear from shared-room measurements merely because its roof credit was capped.
The existing room-adoption limit remains 200 cells; lodging measures its bounded designated scope
(up to 4000 cells), including large halls. Neither rule is a resident-per-map limit.

An all-ineligible recruitment pool must retain its due head while staffing, industry, plot work,
lodging and roads continue. Charter must explain the reputation block alongside physical needs.
The expanded first-guest native scenario checks this through an ordinary hostile wait, unspent
water, no citizen, the final road-processing checkpoint and recovery of the same original debt.
An empty camp's road checkpoint is a control-flow regression witness, not productive construction
or whole-city balance acceptance. See STATUS for the currently executed scope.

Recruitment availability reports follow the configured civic-story policy: ordinal one uses the
owned story-guest catalogue only while civic stories are enabled. Ordinary extension recruits
remain available when stories are disabled. Status reads must not publish option epochs, freeze
candidates or mutate due debt. Native probes disclose temporary catalogue and option-read controls.

## Multi-map home ownership follow-through

Native729f2332 now reproduces the three failures described in STATUS: the same resident loses
projected occupancy, home-work identity and its home plot on a controlled cross-map visit.
The original home remains physically built. This is an actual failing regression, not a claim
that ordinary multi-map play passes.

The fix must distinguish persistent residence from current physical position and work location.
Retain a map-qualified stable home/plot identity and a unique usable sleeping-place reservation;
building replacement during paid upgrades must not make an absent owner homeless or free their
bed. A total bed count and a live occupant census cannot establish reservation ownership.

Keep residence authority with the existing city resident model. `KingdomCityBook` is an explicit
named-field composite and `KingdomCityState` is its immutable rules-side snapshot; inspect and
version that boundary before extending it. Do not introduce a second independent population
ledger or infer a new home merely from the player's current map. Legacy home facts need positive
physical evidence and a deliberate migration path, not guesses from matching coordinates.

Absent household members still reserve space and affect cohabitation. Their observed needs,
refusals and culture/creed facts must remain usable while their map is unloaded; no fabricated
placeholder actors, silent empty-bed assumption or forced loading of every city map for a census.
Audit all residence writers and consumers together: ordinary allocation, arrival admission,
lab-driven moves, departures, succession, subsidence, conversions/removal and room activities.
The broad #230/#233 requirements and ordinary multi-map/cold-load scenarios remain mandatory.
