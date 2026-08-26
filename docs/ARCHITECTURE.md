# Architecture

The Thousand and First is a Caves of Qud scripting mod. Qud streams root XML and compiles shipped
C#; no standalone executable or server exists. `Tools/stage.sh` is the canonical inventory of
what reaches the game and Workshop package. [STATUS.md](STATUS.md) is the canonical short evidence
snapshot; this document describes design, not release signoff.

## Shape

```text
root XML registries ──> loaders/catalogues ──> pure *Rules decisions
                                                  │
Qud events/objects ──> systems, parts, adapters ──┼──> physical mutations
                                                  ├──> persisted books/receipts
                                                  └──> Charter, reports, chronicle, log
```

Engine-free decisions should not acquire `XRL` dependencies. Engine-facing code observes Qud,
asks a rule for a decision, applies a bounded effect, and records enough evidence to resume or
refuse a retry safely.

## Source map

| Path | Responsibility |
| --- | --- |
| `Core/` | Player-bound realm authority (`KingdomSystem`), settlement snapshots, founding/governance, identity, ledgers, reports, save archives, and cross-run realm seals. |
| `Founding/` | Founder basin interaction and founding entry point. |
| `Growth/` | Construction, plots, zoning, crops, food, water/material debit, lodging, power, research, works, wear, and subsidence; engine adapters sit beside pure `*Rules` classes. |
| `Simulation/Kernel/` | Deterministic engine-free encoding, time, counter draws, option latches, and transition primitives. |
| `Simulation/City/` | City state/rules, semantic clocks, execution, bindings, residents, jobs, logistics, networks, production, happenings, and engine-facing city parts/systems. |
| `Experience/` | Rites, belief, offices, notables, succession, and persisted lifecycle/carry/growth transaction books and wire codec. |
| `Chronicle/` | Realm history and receipt rules. |
| `Trade/`, `Quests/`, `Raids/` | Trade manifests/routes, asks/petitions/bounties, and witnessed raid behavior. |
| `World/` | Explicit opt-in cross-run inheritance world hooks and inherited-site builders. |
| `Api/` | Versioned third-party extension contracts, immutable readings, admission, and clamping. Public contract is documented in [API.md](API.md). |
| `Debug/` | Wishes and reversible diagnostic probes; never ordinary-save behavior. |
| Root `*.xml` | Mergeable content registries, blueprints, options, books, procedures, research, and population data. |
| `DevTests/` | Engine-free rule/source-contract NUnit runner. It is excluded from runtime staging. |
| `Tools/` | Canonical staging, compile/ABI/release gates, isolated smoke profile, and log checks. |
| `Art/` | Runtime tile/reference policy and XML cross-reference audits; excluded from staging. |

### Split-authority map

Large logical types keep their public, reflected, and serialized identities while source files are
split by responsibility. Search these families instead of assuming one monolithic filename:

| Logical authority | Current source family | Boundary owned by the split |
| --- | --- | --- |
| Lifecycle state | Lifecycle/carry/growth/raid declaration files under `Experience/`; `Experience/KingdomLifecycleWireCodec*.cs` | Persisted books, operations, receipts, leases, outboxes, declarations, and wire read/write/upgrade lanes. |
| Lifecycle rules | `Experience/KingdomLifecycleRules.cs`, `Experience/KingdomLifecycle*Rules.cs`, and `Experience/KingdomGrowthLifecycle*Rules.cs` | Validation, normalization, conservation, transitions, recovery, callbacks, and trusted physical observations. |
| Realm archive | `Core/KingdomRealmArchive.*.cs` plus `Core/KingdomRealmArchivePhase.cs` and `Core/KingdomRealmCallback*.cs` | Capture, authority hash, bounded validation, graph matching, exact clone, callback/job/delivery evidence, and wire registry. |
| Founding transaction | `Core/KingdomFoundingTransaction.*.cs` | Reservation, staging, first/second-city publication, receipt recovery, faction/chronicle projection, and engine projection. |
| Laboratory runtime | `Growth/KingdomLab.cs`, `Growth/KingdomLab.*.cs`, and the moved `Growth/r_Kingdom*.cs` laboratory IParts | Candidate selection, funding, commission/application/removal state, vats, governance, recovery, and XML-resolved part identities. |
| Plot rules | `Growth/KingdomPlotRules.cs`, `Growth/KingdomPlot*Rules.cs`, and `Growth/KingdomPlotDeclarations.cs` | Typed-lot bounds, siting, ground/roof/heart evidence, stages, refusal, transition chain, and codec. |
| Architecture rules | `Growth/KingdomArchitectureRules.cs`, `Growth/KingdomArchitecture*Rules.cs`, `Growth/KingdomArchitectureEnums.cs`, `Growth/KingdomArchitectureDrafts.cs`, and `Growth/KingdomArchitectureSnapshots.cs` | Selection, validation, compiler/draft, codec/decode, delta, labour, declarations, and snapshots. |
| Zoning rules | `Growth/KingdomZoningRules.cs`, `Growth/KingdomZoning*Rules.cs`, `Growth/KingdomZoningGateParser.cs`, and declaration/gate files | District, stratum, technology, builder/roster, judgement, refusal text, and gate parsing. |
| Material rules | `Growth/KingdomMaterialRules.cs`, `Growth/KingdomMaterialRules.*.cs`, and eponymous material/tally declarations | Clearance, walls, refining, capabilities, bits, exotics, infrastructure, wear, and tally state. |
| Trade rules | `Trade/KingdomTradeRules.cs`, `Trade/KingdomTradeRules.*.cs`, `Trade/KingdomTradeExactLookup.cs`, and `Trade/KingdomTradeOptionAction.cs` | Identity, state, normalization, accounting, exile, lifecycle, authority, proofs, validation, and outbox. |

Each family remains one logical authority only where Qud ABI or transaction identity requires it.
New behavior belongs in the smallest owning shard; a partial class is not permission to create
cross-family state or bypass the documented protocol seams.

## Runtime and authority

`KingdomSystem : IPlayerSystem` is the main player-save authority. It owns realm-wide state and
the seated/away settlement relationship. `KingdomSettlement` is the bounded settlement snapshot
used by seat, archive, exile, and inheritance flows. Do not create a second authority for the
same fact.

City simulation uses deterministic state and books under `Simulation/City`; engine-facing code
dispatches semantic work and projects results into existing realm surfaces. Physical resources
remain game objects or liquids. Abstract ledgers are evidence and reports, not permission to mint
or delete physical stock.

### Resident authority

`KingdomCityBook` resident rows are the living-roll authority. Production code reads and mutates
them through `KingdomResidents` and pure `KingdomResidentRules`; work assignment intersects
labouring rows with surveyed bodies by `ResidentId`. Arrival, notable-guest admission, emigration,
death, office succession, reports, and archives all use that same seam.

`KingdomSystem` and `KingdomSettlement` still expose `RosterNames`, `RosterOrigins`, and
`RosterArrived` because Qud named-field/reflection compatibility forbids casual removal. They are
obsolete one-way projections, not input. Load normalization may adopt one complete legacy roll
into an empty book as `Abroad` claims; real bodies rebind those exact claims. Ragged legacy
evidence is retained and reported instead of truncated or cross-zipped. `Population`, origin
counts, and crew clamps are compatibility aggregates republished from rows; they never choose a
resident.

### Semantic selection and materialization

`Simulation/Kernel/KingdomSemanticSelectionRules.cs` owns bounded, versioned counter draws,
canonical weighted catalogues, deterministic names, and fixed coordinate probes. Engine adapters
may inspect Qud's merged population graph, but admit only direct fixed-count `PopulationObject`
rows. They never ask `PopulationManager` to roll or generate content. Duplicate blueprint weights
are folded and stable keys sorted before `CounterRandom` receives the settlement ID, immutable
stream ID, event kind, sequence, and draw index.

Growth arrivals, guests, causal pilgrims, lodge creeds, commissioned fallback ground, and legacy
furnishings freeze final semantic payload before their first dependent mutation. Growth candidate
wire v3 persists rules/stream/kind, blueprint, origin, creed, name, arrival date, and coordinate;
v1/v2 candidates stage one fail-closed migration before creation. Archive schema v11 adds the same
candidate fields while retaining v1-v10 shapes. Retry and reload consume frozen payloads rather than
re-entering Qud RNG or population generation.

Lifecycle, carry, and growth operations persist plans, resource leases, physical receipts,
outboxes, and retry state through the lifecycle declaration/codec family and specialized lifecycle
rule shards listed above. Public and serialized type names stay unchanged across that source split.
A retry must prove whether a physical effect happened; a return value or intended delta is not
proof.

### One classified active-seat survey

`KingdomSystem.AttendSeatedSemantics` takes one `KingdomSurvey` for a due loaded seat and binds it
for the ordered semantic pass. The survey classifies the zone once and publishes named indexes for
citizens/resident bodies, stores, larders, works, plots, construction roots, layout marks, crop
rows, network pieces, laboratory jobs, visual roots, bindings, and transient job bodies. Consumers
must ask those indexes, not call `Zone.GetObjects()` through a private helper.

Physical commits keep the snapshot coherent through `ObserveAddedToActive` and
`ObserveRemovedFromActive`. A lane may re-prove one exact object, binding, or cell immediately
before mutation; it may not perform a second whole-zone classification. Duplicate semantic or
physical identity is ambiguity and fails closed rather than selecting by enumeration order.
Reports, wishes, heartbeat recovery, and explicit actions outside the bound pass may take a fresh
survey because they have a separate wake and evidence boundary. Dense native instrumentation in
`TESTING.md` is the final proof that no active branch smuggles in another full scan.

### Realm master pause

`Core/KingdomMasterRules.cs` owns the engine-free three-state latch and exactly-once resume token;
`Core/KingdomMaster.cs` observes it before automatic handlers allocate a guard delegate or inspect
a zone. Disabled wakes preserve serialized state and return immediately. A disable, initialization,
or resume edge consumes that wake. Resume stages seated/away city, growth, lifecycle, guest, trade,
and renderer clocks before publishing the applied token. Object-local turn ticks independently
gate before mutation and re-anchor their own rate stamp on their first enabled wake, so a loaded
bench, vat, mirror gate, field, power work, or legacy scaffold cannot turn paused time into work.

`NewWorkAllowed` is for explicit producer entry points. Reports and named committed-recovery
surfaces use separate read/recovery paths. `AutomaticWorkAllowed` is stricter: an unobserved,
disabled, pending, or same-transition-tick latch cannot run. Add every new game-system event,
turn-tick part, and public producer to the master-gate source tests; a downstream call from
`KingdomSystem` is not sufficient evidence for an independently callable path.

### Civic fixture wrappers

Settlement-authored placements use `r_KingdomCivicCampfire`, `r_KingdomCivicBookshelf`,
`r_KingdomCivicTorchpost`, and `r_KingdomCivicHookah`, never the raw merge-target blueprints.
They inherit vanilla art and ordinary function, then reassert only civic ownership: stable authored
orientation, non-takeable placement, no destructive lifetime/push/mirror/tinker/dice additions,
and no manufactured books or hookah water. The campfire remains a real vanilla cooking site.
Source tests prove all shipped XML placements route through these wrappers; final merged live
capabilities still require the compatibility pass in `TESTING.md`.

## Data and extension boundaries

Root XML registries load with Qud's mergeable-stream idiom. Reusing a documented key merges by
load order; a new content record should normally require no C# catalog edit. Schemas and examples
live in [MODDING.md](../MODDING.md).

The supported C# surface is only `ThousandAndFirst.Api` as documented in [API.md](API.md).
Everything else is internal even where C# visibility is public for Qud integration. Extensions
receive frozen readings, deterministic draw lanes, bounded output, and explicit version refusal.
Durable owner namespaces use immutable manifest IDs and refuse lossy-slug collisions. Behaviour
callbacks commit through a per-callback final-size transaction so one oversized owner cannot roll
back host completion or another owner. Happening windows use bounded per-manifest-ID/exact-type
cursors; the cursor publishes before third-party code under the documented advance-on-fault policy.

## High-risk boundaries

### Persistence and wire format

- Never reorder, remove, rename, or retype reflected public instance fields on shipped parts or
  systems.
- Custom formats need magic, explicit version, bounds, migration/future-version behavior, and
  byte/golden fixtures. Current examples include `KingdomArchivedSettlementCodec`, realm seal
  formats, city/binding/job books, and `KingdomLifecycleWireCodec`.
- A load failure can leave a half-built engine object. Preserve the established quarantine and
  post-load reporting behavior.
- Any persistence change needs old-save fixtures and save → quit → reload proof in Qud.

Run `./Tools/check-ipart-abi.sh`; it protects deployed positional layouts but does not prove a
new custom migration correct.

### Identity

Realm, settlement, city, zone, object, operation, lease, event, and receipt identifiers connect
otherwise separate stores. Identity changes can cause cross-city mutation, duplicate replay, or
wrong-save inheritance. Use existing canonical constructors and binding rules; do not fall back
to display names, scan order, mutable coordinates, or `GetHashCode()`.

### Transactions and retries

Founding, construction, growth, water/material debit, carry, callbacks, and outbox publication
cross persistence and physical Qud state. Preserve plan-before-effect, exact before/after
observation, one bounded external effect per resumable step, ownership/marker checks, deterministic
ordering, and terminal evidence. Never “repair” an uncertain state by repeating a debit or
destruction.

### Death-time succession

Qud 2.0.211.51 has one safe crossover seam. `GameObject.Die` sends `AfterDieEvent`, then
immediately re-evaluates `IsPlayer()`; the callback cannot yield a world turn or defer the body
change to a later system tick without letting vanilla terminalize the founder. Succession therefore
does not claim a genuinely asynchronous interregnum. It freezes the exact heir, resident bodies,
owned city zone, extant civic fixture, standing cells, and shrine cell; advances the world clock by
the priced news interval; makes those existing bodies walk through ordinary movement; restores
non-heirs' prior positions while never rewriting their posts, homes, anchors, or goal stack; places
or adopts one tokened in-run founder shrine; and only then crosses player control inside the same
`AfterDieEvent` callback.

`KingdomSuccession` schema 2 persists every adjacent rite checkpoint plus exact body/fixture/shrine
receipts. Native Qud cannot normally save between those callback checkpoints, but injected/native
test snapshots and cold loads must re-prove physical evidence or quarantine the succession slice.
They never infer completion from an intended return value, clone an attendee, substitute another
heir, or place a second marker. Version-1 completed saves remain valid but are marked honestly as
predating physical-rite evidence; a version-1 in-flight clock-only rite cannot be upgraded by
inventing a locus and is quarantined without invalidating the enclosing save.

### Public API and XML merge semantics

`Api/` version, contract fields, ordering, limits, and refusal behavior affect other mods. XML
keys, defaults, case folding, negation, and merge rules are also public interfaces. Update
documentation and compatibility tests in the same change; do not silently reinterpret old data.

## Testing layers and current limitations

1. Checkout-only inventory check: `./Tools/stage.sh verify`. `python3 Art/check_xml_refs.py`
   always checks internal XML and auto-adds vanilla resolution when its default Qud install is
   present; `--base` selects another licensed installation.
2. Full engine-free pure/source-contract suite: `dotnet restore
   DevTests/TafTests.csproj --locked-mode` then `dotnet run --project
   DevTests/TafTests.csproj --no-restore -v q --nologo`. Hosted CI permits exactly three named
   installed-data-only skips when no licensed Qud base is discoverable; the workflow pins their
   labels and TestMain rejects any other/missing skip. An explicitly configured incomplete base is
   a failure. Release `test.ps1` forbids every skip and `release-check.sh` supplies the exact base.
3. Portable kernel and repository-locator slice: `dotnet restore
   DevTests/PortableTests.csproj --locked-mode` then `dotnet run --project
   DevTests/PortableTests.csproj --no-restore -v q --nologo`. Neither engine-free lane is a
   runtime compile or live-game gate.
4. Exact staged compile, base-game XML/tile verification, ABI, release harness, and the binding
   [structural release contract](STRUCTURE.md): `./Tools/release-check.sh`. Current scripts require
   a locally owned Qud install, WSL/Windows PowerShell, and configured Windows paths. Incremental
   CI runs the structural census in report mode; only release mode fails on the unresolved cap or
   missing exact-inventory semantic review.
5. Controlled in-game passes: [TESTING.md](../TESTING.md), including isolated load, player-log
   review, and save/reload scenarios.

Game assemblies, decompiled sources, and extracted assets cannot be redistributed to make hosted
CI self-contained. Future tooling may make more pure tests repository-only; until then, record
which licensed gates you could run and never imply an unavailable gate passed.
