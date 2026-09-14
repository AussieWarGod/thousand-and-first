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
walls, an entrance marker without a physical door, and exterior storage. The generator's compact
housing policy retains that small shelter on larger reservations. `housing_embodiment_test.py`
counts literal sleep providers and non-open cover, while `TryValidateEnclosure` exempts Soft
cover. Those checks cannot establish an enclosed tent. Audit the entire housing catalogue, its
variants and generated lots; changing only these two maps is not the complete task.

`RuntimeData/PopulationTables.xml` supplies nine settler variants with fixed weights. Seven
BaseFarmer descendants have 90% of the total weight; Mechanimist and Snapjaw bodies have 5% each.
`KingdomSemanticSelection.TryPreparePerson` chooses blueprint, origin and shared-grammar name
separately. Appearance alone does not establish an occupation or a coherent culture. Founding
currently freezes an empty relationship set in `Core/KingdomFounding.02.FoundingStandings.cs`.
The latest inheritance requirement supersedes that old design; implement it with the existing
directional standings and transaction contracts, not a competing ledger.

`KingdomResidents.ReadRoster` already retains residents bound to other zones and binds observed
bodies to the current zone. Its local home lookup is not proof of complete multi-map household
behavior. Audit home, job, bound-body and current-zone identities through real travel and loading.

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

The architectural quality requirement applies to **every building**, not only housing. Design
must follow the building's actual program: the people, activities, equipment, storage and open
space it serves. Give it coherent proportions, spatial hierarchy, thresholds and circulation.
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
