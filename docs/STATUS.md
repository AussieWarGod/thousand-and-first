# Current implementation and release evidence

**Snapshot:** 2026-08-29
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
| Staged runtime compile | **CURRENT STAGING SNAPSHOT — 2637 sources; final integrated compile pending** | The current resolver selects 2637 sources, baseline and compatibility symbols. The retained checkpoint compiled clean, but later fan-in means only the final serialized run may sign current bytes. Compile success never closes native compatibility behavior. |
| Cold-install inventory | **CURRENT SNAPSHOT — 2664 files; final frozen inventory pending** | Canonical staging currently enumerates 2664 files. Recompute after the last registered source and freeze the release candidate; package shape does not sign native load, behavior, appearance, or private Steam subscription. |
| Optional ownership bridge | **FOCUSED STATIC PASS; final integrated compile/native open** | Manifest cold-list model proves absent/exact-2.2.3/wrong/disabled/failed/bad-load-order selection and no dropped runtime C#/XML. Installed Hearthpyre 2.2.3 manifest plus three used source files match pinned hashes and the tracked ABI fixture; core foreign-type and bridge mutation/lifecycle bans pass. Exact current-tree staged compile must be repeated after concurrent structural work, and no native overlap/save/re-enable case is signed. |
| Architecture generator | **PASS** — 337 generated larger-lot maps, 242 larger-lot bindings, 277 copied predecessor tiers, 45,056 added cells exactly classified | Generated larger-lot XML is byte-current. Its 45,056 added cells comprise 33,753 yard (including 510 scaled creed work fixtures resolved through a closed inert-wrapper mapping), 8,984 path, 194 sparse-boundary, 1,524 frontage-route, and 601 explicitly reasoned open cells; 3 hosted-arcology maps remain declared holds for their separate authored redesign. These are the generated subset, not the complete registry counted below. |
| Architecture checker | **PASS** — 141 buildings, 131 plotted buildings, 86 palettes, 514 maps (177 source / 337 generated), 356 plans, 359 bindings, 408 tiers, 530 variants, 2,120 variant/pose goldens, zero issues; largest exact a2 receipt 7,105 bytes / 9,544 characters (`arcology/civic-xl/fallback/civic-arcology-xl4+civic-heart-court`); 3 expected installed-base tolerant-recovery warnings | Static topology, exact bounded frontage routes in all poses, independent same-binding `UpgradesTo` route proofs, fixtures, palette/material/technology constraints, typed-lot coverage, deterministic snapshots, exact purpose/exotic runtime anchors, hidden activation gates, and an independent mirror of the runtime's 8,192-byte / 11,264-character codec envelope. The warnings are the named recovery path for malformed vanilla `Creatures.xml`, `Furniture.xml`, and `Items.xml`; it does not judge native appearance. |
| One-survey focused tests | **PASS** — 9 focused source-contract cases | Maintained named indexes, active-pass consumers, mutation observation, and the absence of reachable second whole-zone scans. Dense native scan instrumentation remains open. |
| Addendum 9 structural census | **CURRENT SNAPSHOT LINE-CAP RED — 4 ADJUDICATED FAILURES; FINAL DIGEST/REVIEW OPEN** | 2637 staged C# files / 383,315 physical lines: 4 exceed 300 physical lines, 0 are exactly 300, therefore 4 fail the strict cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. The four are the Gatehouse family (`Growth/KingdomGatehouseRules.cs`, `KingdomGatehouse.ProjectionEvidence.cs`, `KingdomGatehouse.cs`, `KingdomGatehouse.Projection.cs`), docketed by the R3 registration sweep — docketed, not exempted; the gate still fails. Direct `XRL` imports: 1204 files, 3 over the line limit. Inventory SHA-256: `67a786670a85a30c36651a493069353355734701e33199d5397f5e21969b18ff`. The hardening sequence decomposed 144 additional oversized authorities, 154 cumulative. This is the current working-tree snapshot, not the final release digest: rerun after freeze. `docs/STRUCTURE_REVIEW.json` is absent and must be completed by a human against that final exact digest; automation cannot sign one responsibility or protocol quality. |
| Full Qud-referenced/source suite | **LAST RETAINED PASS** — 7,743 / 7,743 cases, 0 skipped; final working-tree run pending | Hosted checkpoint `d285129` has green repository-audit, Ubuntu source-suite, and Windows source-suite jobs for its exact bytes. Later un-deferral code remains unsigned until the serialized final run. This does not sign native Qud behavior. |
| Portable suite | **LAST RETAINED PASS** — 173 / 173 cases, 0 skipped; final working-tree run pending | Repository-only checkpoint. Public CI has an exact three-label allowlist for installed-data-only skips when licensed Qud data is absent; extra/missing skips and an explicit invalid base fail. Canonical release `test.ps1` forbids skips. |
| Tools tests | **PASS** — 35 tests | Repository tooling checks green. |
| Art tests | **PASS** — 23 / 23 tests; 84 verified vanilla tile references, 0 custom runtime paths | Art policy, local-path prohibition/provenance, bidirectional wiring, and installed-base vanilla references are green. Native appearance review remains open. |
| Latest retained native smoke | **PARTIAL PASS** — fresh profile, founding, 17/17 checks, one production gallery case, save/cold-load, repeat 17/17, clean log | Clean commit `19fb8ee` deployed against Qud 1.0.5/core 2.0.211.51. This signs only that commit's loader/founding/single-sample persistence smoke. Later structural revisions have no native compile/load/log receipt and are not covered by this row. |

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
and final frozen-digest evidence remain open; hosted-arcology production is excluded from this
completion statement while AMENDED 19+ review is active.

- Plots reserve typed lots. They do not stand in for buildings.
- A building occupies a lot through a frozen authored map, exact size/type binding, pose,
  entrances, functional fixtures, material palette, technology minimum, and deterministic variant.
- Defence and siting are separate: only an unplotted defensive work is a cap-exempt frontier
  segment. Defensive plotted buildings retain their authored lot, category, cap cost, and base
  defence rating.
- Automatic `UpgradesTo` tiers keep lot identity only inside the frozen exact binding; explicit
  directional same-type/same-size plan transitions also keep it. Retype/resize uses fresh siting
  and a new lot identity. The civic heart's adjacent authored rung is the sole exception.
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
- Food and water are separate physical flows. Dedicated vessels, water details, seeds, authored
  crop rows, bounded foraging, physical larders, daily rations, favoured meals, mills, and cross-zone
  deliveries are implemented. A favoured meal supplies one bounded positive shade for one day;
  stock never grants an indefinite passive bonus merely by existing.
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
  evidence. Its strict v5 envelope has independent v1-v4 migration fixtures, opaque inert future
  preservation, canonical ordering, fail-closed quarantine, and hard capacities. Foundation now
  publishes the current realm plus at most one opted-in legacy partner/rival by typed CAS; owned
  faction projection/recovery, immutable profile→NPC resolution, exact resident-successor bridge,
  finite endpoint bodies, no-backlog presentation, caused diplomacy, all seven cohort schedulers,
  three-city traffic, exact Trade consignments, loaded hospitality, witnessed intervention/death,
  consented escrow, deterministic direct records/aggregates, and shared W0 capacity are wired.
  Physical endpoints require distinct route-reachable cells, exact recursive custody and removal
  witnesses; visible death crosses `EarlyBeforeDeathRemoval` → `BeforeDestroy` → `OnDestroy`, and
  completed cleanup precedes the one W0 release.
  Exile/refound causally ends old semantic authority, tombstones exact owned factions, restores
  byte-identical authority on return, or imports only bounded institutional facts under fresh ids.
  Polity transaction closure is frozen at 61/61 focused cases; physical narrow checks are green.
  Final integrated runners and Pass 39 native behavior remain open.
- Reopened civic-experience code scope is complete: two named voices, optional remembrance,
  explicit offices, staffed loci, fixed witness works, First Guest choice/hosting, First Feast
  practice, curiosity and civic leads, body history, non-custodial artifact recognition, manual
  communal rite, joint civic view, integrated three-return Guest's Feast, site practice, named-cook
  vacancy/handoff, and bounded vocation services have separate exact owners. Focused evidence is
  lane-local; final integration and the native/human promotion protocol remain open.
- Explicit prepare-save-for-removal is implemented while the mod is present: it fences new work,
  plans exact visited-ground and global owned cleanup, reports unvisited locators, retires faction
  projections, persists an identity fence, and permits a fresh incarnation only through monotonic
  high water. It never promises that disabling/removing the mod first can clean a save.

## Active v1 closure work

- **Final serialized fan-in.** Production owners exist for every accepted non-arcology positive
  row. The narrow physical-food landing transaction is complete at code scope; the final
  Qud-referenced, portable, baseline/compatibility, package, and release-gate runs must execute
  after project registration/source freeze. Lane-local green counts are not substituted for that
  integrated receipt.

- **Routed construction-input evidence.** Ordinary construction now mints one centrally owned job
  for its exact water/material bill when lawful local custody is absent. The job freezes nearest
  holders, itinerary, carrier, landing, debit, rollback, recovery, and master-pause authority;
  remote stock is never direct spending permission. Integrated interruption/conservation and
  native traversal remain unsigned.
- **Hosted arcology design and implementation review.** The accepted one-capital shell, fixed
  current/exiled authority slots, persistent atrium/ward/terrace interiors, paid hosted lots,
  water-gated terrace, dark foreign/exiled shell, and no-remote-simulation boundary remain the v1
  target. The earlier implementation claim is superseded: implementation is under active AMENDED
  19+ review, with production, XML/fabric, and tests held. TESTING 136j–136j.5 is a proposed native
  acceptance protocol, not current runtime evidence. Yielding-lot relocation is separately
  implemented and must not be inferred from this row.
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
  final integration and native conservation/UI proof remain open.
- Public founder memory is implemented through the owned shrine, Chronicle, corpse-reading, and
  one optional custom `sultanHistory` entity/event plus an exact non-tradable, non-forgettable
  journal note. It is isolated from Sultan/cult/village/relic selection, capped at one per world,
  recoverable from its receipt, and quarantines divergent evidence. Seven focused cases pass in
  both runners; native journal/save/second-succession proof remains open.
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
- Addendum 9 structural release gate green: every staged C# file strictly under 300 physical lines,
  plus exact-inventory human evidence for one responsibility and protocols at boundaries. Fresh
  working-tree line scan is green, but final exact staging census/digest is pending and
  `docs/STRUCTURE_REVIEW.json` is missing; no enterprise-grade or v1.0 release-quality claim is
  valid until both final requirements close.

Detailed current ledgers live in `_notes/BRIEF-IMPLEMENTATION-AUDIT.md` and
`_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`. Release mechanics live in
[RELEASING.md](RELEASING.md); structural gate semantics live in [STRUCTURE.md](STRUCTURE.md).
