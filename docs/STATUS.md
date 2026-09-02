# Current implementation and release evidence

**Snapshot:** 2026-09-01
**Target:** feature-complete v1.0 test candidate
**Current public version:** 0.2.0 work in progress

This file is the canonical short status. A green source, compile, or generator gate proves only
that layer. Native Caves of Qud behavior is signed only for the exact exercised native cases;
visual quality, accessibility, compatibility, and Steam subscription remain separate evidence and
are never inferred from source or static automation.

## Current automated evidence

| Layer | Latest result | Scope |
|---|---|---|
| Last clean release audit | **PARTIAL PASS** — 10 of 11 automated layers green at committed candidate `19fb8ee`; structural release layer red | Docs/hygiene, ABI, inventory, baseline/compatibility compile, 7,586-case full suite, 171-case portable suite, architecture/XML/art, balance, 43-case smoke harness, Workshop package, and deploy dry-run passed. This remains a bounded committed receipt, not proof for later structural revisions. |
| Staged runtime inventory | **STATIC CENSUS CURRENT; FINAL MANAGED RERUN REQUIRED** | The current resolver selects 2945 sources, baseline and compatibility symbols. The retained 10,624-case run predates the final market fan-in, so it is not attributed to these exact sources. Compile success would still not close native compatibility behavior. |
| Cold-install inventory | **FROZEN SOURCE SNAPSHOT — 2975 files; release evidence still open** | Canonical staging enumerates 2857 files. Any source or package-inventory change invalidates this snapshot. Package shape does not sign native load, behavior, appearance, or private Steam subscription. |
| Optional ownership bridge | **INTEGRATED STATIC PASS; native matrix open** | Manifest cold-list model proves absent/exact-2.2.3/wrong/disabled/failed/bad-load-order selection and no dropped runtime C#/XML. Installed Hearthpyre 2.2.3 manifest plus three used source files match pinned hashes and the tracked ABI fixture; core foreign-type and bridge mutation/lifecycle bans pass. No native overlap/save/re-enable case is signed. |
| Architecture generator | **PASS** — 146 maps, 107 bindings, 122 tiers; 3,512 transformed fabric cells and 22,084 exterior site facts | Generated XML is byte-current. It composes 527 creed fixtures in 248 deliberate pairs across 744 programme regions, plus reviewed housing renovations and deep-end campuses. Unsupported spatial programmes remain absent instead of receiving stretched padding. These are the generated subset, not the complete registry counted below. |
| Architecture checker | **PASS** — 144 buildings, 134 plotted buildings, 89 palettes, 333 maps (187 source / 146 generated), 220 plans, 226 bindings, 262 tiers, 344 variants, 1,376 variant/pose goldens, zero issues; three expected installed-base tolerant-recovery warnings for malformed vanilla Creatures/Furniture/Items XML; reference-grounded quality audit 1,376 / 1,376 static pass, 0 fail; largest exact a4 receipt 7,798 bytes / 10,468 characters (`greatfoundry/craft-xl/templar/purpose-greatfoundry-templar-xl0+purpose-forge-foundry`) | Static topology, exact footprint/roof authority, bounded frontage routes in all poses, required-use circulation, transition route and custody proofs, fixtures, palette/material/technology constraints, typed-lot coverage, deterministic snapshots, exact purpose/exotic runtime anchors, hidden activation gates, and the runtime codec envelope are checked. All 1,376 poses still require native/human appearance and function acceptance. |
| Benefit-provider content | **PASS** — 114 catalogue rows, 187 authored variants, 105 unique explicit fixtures | Exact design/provider affinity, obtainable portable stock, fixed-installation reasons, caps, and absence of catalogue-as-supply fallbacks are checked. Native function and appearance remain separate evidence. |
| One-survey focused tests | **PASS** — 14 focused source-contract cases | Maintained named indexes, active-pass consumers, mutation observation, and the absence of reachable second whole-zone scans. Dense native scan instrumentation remains open. |
| Addendum 9 structural census | **FROZEN SOURCE SNAPSHOT LINE-CAP GREEN; EXACT-INVENTORY REVIEW SIGNED** | 2945 staged C# files / 420,767 physical lines: 0 exceed 300 physical lines, 0 are exactly 300, therefore 0 fail the strict cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Direct `XRL` imports: 1373 files, 0 over the line limit. Inventory SHA-256: `820a9560a3b0fc5f5b59bd91deedd0e1145a2d9fe3f82b623cd8dcd43fe3a409`. `docs/STRUCTURE_REVIEW.json` is signed against this exact digest by the AI reviewer under the author's Addendum 9 ruling of 2026-09-02 (see `docs/STRUCTURE.md`). |
| Full Qud-referenced/source suite | **LAST RETAINED PASS; FINAL RERUN REQUIRED** — 10,624 / 10,624 cases, 0 skipped | This receipt predates final market sources and does not sign current bytes. The final serialized installed-Qud run remains required; even then it will not prove native play, appearance, compatibility, or Steam installation. |
| Portable suite | **LAST RETAINED PASS; FINAL RERUN REQUIRED** — 2,325 / 2,325 cases, 0 skipped | This receipt predates final market sources and does not sign current bytes. Public CI keeps its installed-data skip allowlist; canonical release testing forbids skips when Qud data is present. |
| Tools tests | **PASS** — 296 / 296 tests | Repository tooling, generators, gallery census, documentation contracts, and package helpers are green at this checkpoint. |
| Art tests | **PASS** — 28 / 28 tests; semantic subset 4 / 4; 125 verified vanilla tile references; 0 custom runtime paths | Snapjaw caches now use ordinary Woven Basket art, Hindren textile works use the ordinary Sewing Machine as a treadle stitcher, and TAF faction emblems use a deterministic glyph-only projection instead of Joppa terrain art. Art policy, installed-path, reference, and architecture-declaration checks are green. Native original-scale appearance, emblem recognition, and a current preview remain open. |
| Latest retained native smoke | **PARTIAL PASS** — fresh profile, founding, 17/17 checks, one production gallery case, save/cold-load, repeat 17/17, clean log | Clean commit `19fb8ee` deployed against Qud 1.0.5/core 2.0.211.51. This signs only that commit's loader/founding/single-sample persistence smoke. Later structural revisions have no native compile/load/log receipt and are not covered by this row. |

Historical checkpoint `d285129` passed 7,743 / 7,743 cases and 173 / 173 cases in its two
managed suites; its then-current Tools suite passed 35 tests and its Art suite passed 23 tests.
Those numbers identify that old receipt only. The preceding decomposition ledger also recorded
144 additional oversized authorities, 154 cumulative, before the final plot-staking split above.
Current evidence is the larger working-tree census in the table.

Reproduce the current static results:

```bash
./Tools/gate.sh
./Tools/check-manifest-directories.py
./Tools/check-hearthpyre-abi.py
dotnet run --project DevTests/TafTests.csproj --no-restore -v q --nologo
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py
python3 Tools/check-structure.py --report
```

## Implemented architecture boundary

Static authored-map, binding, material/style, frontage, road, delve, inheritance, and preview
targets are complete at code/content/checker scope. Native gallery, accessibility, compatibility,
exact-inventory human review, and current preview evidence remain open. Hosted-arcology topology and authored programmes are
implemented at this same static boundary; their native traversal and visual acceptance remain open.

- Plots reserve typed lots. They do not stand in for buildings.
- A building occupies a lot through a frozen authored map, exact size/type binding, pose,
  entrances, functional fixtures, material palette, technology minimum, and deterministic variant.
- Defence and siting are separate: only an unplotted defensive work is a cap-exempt frontier
  segment. Defensive plotted buildings retain their authored lot, category, cap cost, and base
  defence rating.
- Automatic `UpgradesTo` tiers keep lot identity only inside the frozen exact binding; explicit
  directional same-type/same-size transitions keep it, and reviewed `additive-expand` or
  `renovate-expand` transitions may grow the envelope after proving adjacent ground and ingress.
  Retype, shrink, relocation, and replacement use strike plus fresh siting and a new lot identity.
- Gatehouses use a traversable road-bound topology. Delves use paired physical travel endpoints.
- The assenting moot has a complete XL floor, inert-safe vanilla-art fixtures, and a current
  runtime owner. Its commission key is derived for the seated city only while assent research,
  the founder's Chavvah rite, and claimed surface ground cardinally adjacent to Moon Stair terrain
  all remain true; the key is never permanent knowledge. One exact finished moot owns bounded
  named assent/exemption and a reversible native ambient-stabilization ward.
- Inheritance freezes witnessed authored receipts and a connected street graph; it never carries
  items, liquids, charge, or mutable object identity between runs.
- External ownership is a read-only provider protocol. Exact Hearthpyre 2.2.3 is the only shipped
  typed translator and is absent from the compile set in every other dependency state. First and
  later city receipts, plus ordinary ground claims, disclose observed overlap and offer free reject
  or exact bind. The chosen mode/evidence is inside the receipt digest before water debit and
  remains permanent TAF claim data; active-ground divergence gates load/turn/semantic and mutating
  Charter work without loading remote ground or taking a foreign lifecycle. Automated
  isolation/ABI/codec/CAS/source contracts are implemented; native
  reject/bind/save-disable-re-enable/divergence/log evidence remains open.
- Visual-state cues derive from real construction, staffing, wear, deprivation, and network state.
  Current runtime presentation is vanilla-art/glyph-first. Original assets—including disclosed
  generative-assisted drafts only after pixel-level human revision—are permitted through the
  provenance, rights, editable-source, wiring, fallback, package, and independent native-review
  policy in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
  The semantic fixture pass replaced Chiliad-specific basket art on Snapjaw caches with the
  ordinary Woven Basket, replaced Nacham's unique charged wire-extruder art on Hindren textile
  works with an ordinary Sewing Machine presented as a treadle stitcher, and replaced the
  Joppa-tile faction helper with a deterministic TAF-owned glyph-only emblem. No raster or
  architecture map was created or changed by that pass. Native tile-scale taste remains unsigned.
- Food and water are separate physical flows. Water retains dedicated vessels, water details,
  upkeep, and scarcity. Food runs seed → authored crop row → physical harvest → dedicated larder →
  explicit meal, mill, industry, or trade debit. A shared meal requires spendable ingredients and
  a currently capable physical cooking provider; completion grants bounded creed/cohabitation
  progress, never population capacity. Empty pantries and missing kitchens withhold the act and
  spend nothing. Abstract foraging, daily ration bills, hunger catch-up/marks/departure, passive
  food-rate minting, and stock auras are retired; legacy save/wire fields normalize inert. Food
  never binds population: live supported level and subsidence use water plus roofs, so zero food
  cannot shrink a settlement and additional food cannot raise its base population level. Damaged
  larders never passively spoil or debit pantry stock: identity and count survive arbitrary absence,
  and an open legacy food-loss receipt clears inert before callbacks.
- Archive-v17 fixed-rate arrival cadence and its lifecycle receipts are the sole current v1 arrival
  authority. The historical Growth-1B/schema-5 oracle froze only a hostile parser and terminal
  canonical validator; its own freeze receipt explicitly supplied no transitions, wire, C#, save
  root, caller, materialization, tuning, or gameplay. Porting it beside the current cadence would
  create a second arrival authority, so it is rejected as parallel architecture—not deferred v1
  debt. Replacing cadence later would require a new ruling, migration, and full evidence owner.
- Creed semantics now use a mergeable six-kind registry while preserving every public/save
  `Creed` key. Installed 2.0.211.51 mapping covers 33/33 admitted factions (4 community, 16 people,
  2 polity, 7 order, 2 doctrine, 2 cult). Only four shipped doctrine/cult keys can drive passive
  conversion or shrine consecration. Gyre Wights are conservatively a non-theological people:
  their exact affiliation still gates architecture and civic practice, but never shrine output.
  Unknown modded keys remain neutral affiliations, and explicit water rites use
  adoption/allegiance prose.
- The founding handbook is situated rather than canonicalized: Neseva Cask-Hand's Uru Ux 1000 AR
  copy belongs to the Open Basin fellowship and carries a marginal historical countervoice while
  retaining every actionable instruction. Exile and return separately freeze exact authored
  official/outsider entries, before/after list hashes, and a domain-separated pair fingerprint in
  TAF-local Chronicle receipts. New disputed transitions never write Sultan/world history or
  vanilla accomplishments/murals. Static content, migration, idempotence, and interruption
  contracts exist; native presentation and every-cut save proof remain open.
- Physical-building benefits separate exact designations from current providers. Catalogue values
  are caps only; furniture and native capabilities supply them, optional semantic build-key tags
  prevent cross-design substitution, and every operation scales with current root condition.
  Fifty-one load-validated roles support player designation: enclosed ordinary housing/work rooms,
  ordinary open yards/grounds with exact catalogue-sized rectangles, and one exact dry-container
  larder. Network, crop, power, laboratory, fixed-creed, Heart, remote, purpose/crown, and hosted
  machinery remain authored pending their own typed physical proof.
- The purposeful-megastructure portfolio implements exactly five symmetric compatible edges and
  ten directed recipes across Deep-Bore, Great Foundry, Granary-Colossus, chimeric theatre, and
  becoming annexe. One exact bootstrap funds the second shell; one exact return is consumed by a
  paid activation operation; later operations alternate, consume one exact input cargo plus frozen
  local water/material/food and any selected existing body service, then transport one exact output.
  Durable CAS receipts, ordered dual exact construction inputs, lease-safe debits, explicit
  dispatch/pickup/landing checkpoints, orphan recovery, and authored XL/creed floors are implemented.
  Automated gates are green for the purpose family; native Pass 37 remains acceptance evidence.
- Succession configuration implements Charter seniority, exact chosen-life selection and its
  optional seat climb. The activated groomed-successor law keeps realm-bound exact `ResidentId`
  plus monotonic service/schooling proofs; ready nominees inherit lawfully, while missing,
  departed, duplicate, or unfinished nominees fall back to seniority without chosen-life cost.
  Save/config migration and automated gates are implemented; native Pass 36 remains open.
- `KingdomSystem.PolityLedger` is the realm-scoped semantic authority for bounded polities,
  directional relations, immutable profiles, routes/fronts/grievances, finite cohorts, scarce
  figures, witnessed incident plans/conclusions, projection receipts, options, and compaction
  evidence. Its strict v7 envelope carries bounded typed phenotype cues, reads v1-v6 without
  inventing cues, and provides opaque inert future-payload preservation, canonical ordering,
  fail-closed quarantine, and hard capacities. Foundation now
  publishes the current realm plus at most one opted-in legacy partner/rival by typed CAS; owned
  faction projection/recovery, immutable profile→NPC resolution with exact source/reason
  traceability and deterministic weighted body/role/skill/mutation-or-cybernetic/gear/signature/
  cargo/dialogue expression, exact resident-successor bridge,
  finite endpoint bodies, no-backlog presentation, caused diplomacy, all seven cohort schedulers,
  three-city traffic, exact Trade consignments, loaded hospitality, witnessed intervention/death,
  consented escrow, deterministic direct records/aggregates, and shared W0 capacity are wired.
  Physical endpoints require distinct route-reachable cells, exact recursive custody and removal
  witnesses; visible death crosses `EarlyBeforeDeathRemoval` → `BeforeDestroy` → `OnDestroy`, and
  completed cleanup precedes the one W0 release.
  Current foundation body pools consume only exact positive species counts plus audited
  identity/body tags; subsequent revisions rebuild from exact per-city population-body facts.
  Origin, culture, style, creed, and architecture never choose current bodies, and unrecognized
  evidence stays `unresolved` so the current resolver refuses instead of inventing a human.
  Foundation technology is `KingdomZoning.Tech(System) * 2`; later facts derive each city's band
  from its sorted zoning roster through `TechPoints`/`LevelForPoints` and take the bounded maximum,
  never `Stage * 2`. Existing legacy profile/resolver rules remain frozen rather than reinterpreted.
  Exile/refound causally ends old semantic authority, tombstones exact owned factions, restores
  byte-identical authority on return, or imports only bounded institutional facts under fresh ids.
  Polity transaction closure is frozen at 61/61 focused cases; integrated automated and physical
  narrow checks are green. Pass 39 native behavior remains open.
- Reopened civic-experience code scope is complete: two named voices, optional remembrance,
  explicit offices, staffed loci, fixed witness works, First Guest choice/hosting, First Feast
  practice, curiosity and civic leads, body history, non-custodial artifact recognition, manual
  communal rite, joint civic view, integrated three-return Guest's Feast, site practice, named-cook
  vacancy/handoff, and bounded vocation services have separate exact owners. Civic market service
  now requires one accepted staffed `taf:market` provider on designated ground, the same exact held
  office receipt, Village, and current standing 3 or better. It may open empty. `ShopTier` is
  current operational standing/reach and may fall to zero; Chronicle receipts, not this field, own
  history. Native TradeUI sale/purchase is the only ordinary stock ingress/sink: TAF generates no
  wares, consignments, periodic restock, passive output, or remote debit. The sealed
  `GenericInventoryRestocker` is only an empty-trade adapter. Sold, bought, stolen, dropped,
  corpse-, player-, container-, or foreign-held goods retain physical identity, count, location,
  native `_stock`, and foreign state while only TAF receipts/guards retire. Completed or dormant
  legendary traders remain finite personal native merchants after civic loss/accession, but supply
  no civic authority without provider plus office; only open prepared handoff endpoints are
  temporarily succession-ineligible. Growth stage no longer promotes the first citizen, and
  production contains no generic `TakeOnRoleEvent` office notification.
  Integrated automated evidence is green; native office/service recovery and the human promotion
  protocol remain open.
- Explicit prepare-save-for-removal is implemented while the mod is present: it fences new work,
  plans exact visited-ground and global owned cleanup, reports unvisited locators, retires faction
  projections, persists an identity fence, and permits a fresh incarnation only through monotonic
  high water. It never promises that disabling/removing the mod first can clean a save.

## Remaining v1 evidence work

- **Research-alignment P0 fan-in.** The profile-body, zoning-technology, merchant-office,
  Gyre-kind, founding-book, and disputed-history/food findings in the
  [2026-09-01 audit](../_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md) now close at their stated
  code/content/static scopes. The audit's release-evidence P0 remains open: none of those repairs
  supplies current native full-loop, complete gallery, 27-zone arcology, exact-inventory human
  structure, current preview, compatibility, or cold Workshop-subscription proof.
- **Research disposition coverage.** That audit now crosswalks the complete current comparator,
  generative, early/system-design, succession/quest/lab/growth-oracle, polity, food/water,
  Qud-affordance A1–A13/R1–R6, lore P0–P2, and ecosystem EC-01–EC-14/L1–L12 finding families.
  Every row is either implemented at an explicitly narrow
  code/content/static scope, superseded or rejected with a reason, or retained as an exact
  native/human/release gate. Direct Landing Pads registration, Bethesda/Bethsaida or vanilla
  historic-site ownership, global Coda/Sultan history, generic dynamic quests, synthetic gossip,
  and original-artifact custody are not missing v1 owners: their unsafe proposed mechanisms are
  rejected. Open research outcomes remain voluntary mature return, person/deed recall, cost of
  presence, purpose/staff/state recognition, hauling/restoration/population tuning, Girsh/Nephilim
  route coexistence, semantic tile taste, and representative mod-stack behavior; automation must
  not claim those human/native results.
- **Frozen automated fan-in.** Production owners exist for every accepted positive row. Current
  baseline/compatibility/development compile, Qud-referenced, portable, tooling, provider,
  architecture, and 28/28 Art evidence is recorded at its exact frozen snapshot. Package, native
  persona/compatibility, human visual, exact-inventory semantic review, current preview, and Steam
  evidence remain; no static green substitutes for those receipts.

- **Routed construction-input evidence.** Ordinary construction now mints one centrally owned job
  for its exact water/material bill when lawful local custody is absent. The job freezes nearest
  holders, itinerary, carrier, landing, debit, rollback, recovery, and master-pause authority;
  remote stock is never direct spending permission. Integrated interruption/conservation is green;
  native traversal remains unsigned.
- **Hosted arcology native acceptance.** One exact root now owns schema `TAFArcology`: all 27 local
  zones across `x/y=0..2`, `z=9..11`, 27 purpose programmes, reciprocal district thresholds,
  matched stairs in every coordinate column, one civic surface exit, and designated paid
  terrace/ward anchors. Nine route-safe archetypes across foamcrete cultivation, inherited-marble
  civic, and rusted service strata now replace the former uniform floors; paid fixtures share that
  programme authority. Paid-floor output now uses an exact active full-floor designation and one
  canonical dated final-suspension observation: ward roof/luxury comes from current providers;
  terrace food comes only from exact growbed rows and requires current exterior fresh water.
  Receipt/root/zone/anchor mismatch and malformed/duplicate observations fail closed without remote
  loading. Focused hosted contracts and the frozen staged compile are green. TESTING 136j–136j.5
  still owns native traversal, save/cold-load, labour/water, provider loss, and human inspection of every zone.
  Yielding-lot relocation is separate.
- **Heart ring-call relocation native acceptance.** Runtime and automated contracts are implemented:
  only exact finished settlement-raised plots marked yielding can answer a Heart `NoGroundToGrow`
  offer; the founder sees and re-proves every source→destination before mutation; cost is labour and
  world-time only; one visible receiving frame advances at a time; original plot objects move under
  a bounded CAS receipt while their LotId, frozen architecture, contents, residents/home binding,
  staffing, held/work state, wear, and network declarations remain on those same objects. Exact-ID
  ambiguity, new obstruction, callback interruption, ownership loss, malformed/future receipts, and
  cold recovery fail closed or roll back. Focused relocation runs are green (22 portable, 35 native
  rules/source cases) and staged baseline/compatibility compilation is clean. TESTING
  136j.6–136j.10 remains the live-Qud behavior/save/appearance signature.
- **Assenting-moot native acceptance.** Runtime implementation is complete: six durable named
  assents, six durable exemptions, explicit add/remove UI, current-body strength, exact native
  `AmbientStabilization` ownership, per-body ambient-effect veto, and damage/destruction,
  absence/death/departure, strike, secession, thaw/activation/load recovery are wired. Ten focused
  cases pass in both runners and staged baseline/compatibility compilation is clean. TESTING
  136t-136w remain unsigned live-Qud behavior/save/appearance evidence.
- **Civic-experience promotion.** All O0–O11, D1–D12, and C1–C2 surviving bounded purposes now have
  code owners or stronger supersessions; C3 has structural simulation and telemetry but requires
  measured native evidence. TESTING Pass 40 owns integrated UI/save/accessibility/ablation proof.
- **Polity native acceptance.** Complete bounded semantic/physical adapters exist at code scope,
  including shared attention, direct-record fallback, exact recursive custody, and visible-death
  gates. TESTING Pass 39 owns native route, body, death, three-city, performance, compatibility,
  exile/return/refound, and anti-farm proof.
## Deliberate v1 boundaries

- Ground claims still preserve every existing object's ownership, inventory, and allegiance.
  Explicit realm property is implemented in the working tree: the Charter can designate or release
  one nearby founder-owned takeable object through `r_KingdomProperty`; its exact reversible receipt
  writes only native `Physics.Owner`, so vanilla warning/help behavior supplies theft consequences.
  It does not claim-stamp ground or nearby objects. Foreign ownership and receipt divergence fail
  closed. Six focused source/pure cases pass in both runners; native save/theft/release proof is
  open.
- Authored post/home day shape and attended cosmetic station activities are implemented: posted
  residents tend, sort, craft, maintain, build, watch, or attend a shrine without granting stock,
  progress, RNG effects, skill, experience, or standing. Eleven focused portable cases and staged
  compilation pass; native lived-day observation remains open.
- Within-realm physical food routing is implemented. Trade-owned polity consignments and loaded
  food/water hospitality now have exact intent/debit/custody/conclusion authorities at code scope;
  integrated automation is green and native conservation/UI proof remains open.
- Founder memory is implemented through the owned shrine, Chronicle, corpse-reading, and one
  durable TAF-local read-only projection reconstructed from its save receipt. Schema 2 never
  inserts an entity, event, or note into Qud's shared `sultanHistory`/journal pools. Exact schema-1
  objects are removed only after list/index/back-reference/payload proof; ambiguous legacy state is
  left inert and quarantined. The retired option is unnecessary because the existing Charter
  Chronicle already owns the visible telling. Focused pure/source/native-consumer cases cover
  migration, save reconstruction, no insertion, and fail-closed cleanup; live legacy-save cleanup
  remains a native acceptance item.
- The realm faction dish remains the realm authority and competing arbitrary `TeachesDish`
  overrides are `REJECTED`. One exact city-local named cook, separately authored alternate recipe,
  paid teaching, release, and recovery are implemented; native recipe/identity/save proof remains
  open.

## v1 polity/world-presence boundary

The author reopened every positive polity/world-presence direction for v1. Current source now owns
the complete bounded code shape: one optional latest legacy polity under fresh ids; typed immutable
profile revisions; exact faction/body/gear projection; causal named promotion; seven finite cohort
purposes; deterministic current/rival and three-city traffic; Trade-owned correspondence custody;
loaded hospitality; caused grievance/terms/truce/intervention; witnessed conclusion/death/aftermath;
consented escrow; shared W0 body/audience capacity; direct-record fallback/aggregates/fairness; and
exact exile/return/refound/retirement cleanup. Semantic travel never walks an unloaded actor, and
physical bodies exist only on eligible loaded ground under recursive custody and removal witnesses.

This is code-scope closure, not native proof. Pass 39 still owns save, accessibility, performance,
compatibility, recognition, anti-farm, three-city, death-order, and every-cut evidence. Exact
old-actor continuation, automatic war from creed/opposition alone, actors simulated on unloaded
tiles, persistent strategic armies, mass background war, and unwitnessed conquest/casualties remain
`REJECTED`. Canonical disposition/evidence owners are in
[VISION.md](../VISION.md#canonical-v1-polity-scope-matrix).

## Required before a v1.0 test-candidate claim

- Full pure/source suite green after the final code and documentation set; rerun after every later
  source or source-contract change.
- Exact release, ABI, XML/reference, architecture, package, and Workshop-package test gates green.
- Retain the clean `19fb8ee` deployment/native smoke as bounded evidence only. Repeat native
  compile/load and `Player.log` review against the exact final structural commit before claiming its
  runtime behavior.
- Numbered [TESTING.md](../TESTING.md) protocol completed for all changed high-risk lanes,
  including cold save/load cuts, dense city, multi-zone carriers, citizenship overlays, master and
  module resume, raids, succession, happenings, inheritance, and external API fixture.
- Native architecture galleries reviewed at tile and text scale for every loaded commissionable
  key and reachable state; controller/keyboard and color-independent readability signed.
- Representative compatibility matrix and private Steam subscribed-install receipt retained.
- Every reopened positive limitation is implemented or named as an active implementation/evidence
  gate. Only controlling hard rejects and actions requiring external account authority stay outside
  executable v1 work; no historical audit may prove a current gap closed.
- Every current `SHIP` row in the v1 polity scope matrix remains truthful at its stated
  implementation/evidence boundary. Accepted-but-open design must not be presented as current
  runtime; semantic ledger proof does not sign any physical adapter.
- Addendum 9 structural release gate is closed for the current digest: every staged C# file is
  strictly under 300 physical lines and `docs/STRUCTURE_REVIEW.json` binds the exact-inventory
  responsibility/protocol review to digest
  `820a9560a3b0fc5f5b59bd91deedd0e1145a2d9fe3f82b623cd8dcd43fe3a409`, signed by the AI reviewer
  under the author's Addendum 9 ruling of 2026-09-02 (fourteen ownership and four protocol faults
  were fixed before signing). Any staged source change reopens it. This is an ALPHA claim, not an
  enterprise-grade or v1.0 release-quality claim.

Detailed current ledgers live in `_notes/BRIEF-IMPLEMENTATION-AUDIT.md` and
`_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`. Release mechanics live in
[RELEASING.md](RELEASING.md); structural gate semantics live in [STRUCTURE.md](STRUCTURE.md).
