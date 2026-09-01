# Supported API — The Thousand and First

Everything listed here is a supported contract: it changes only under the versioning rule
in [STANDARDS.md](../STANDARDS.md) §9 (no removals in a minor release; deprecations marked
`[Obsolete]` with a named replacement and kept working for at least one minor cycle).
**Anything not listed here is internal and may change without notice** — if you need
something that isn't here, open an issue and it can be promoted deliberately.

Most extension needs no code at all: see [MODDING.md](../MODDING.md) for the XML registries
(`KingdomBuildings.xml`, `KingdomDeals.xml`), which are the preferred extension path.

This is the supported programming contract, not a release-status ledger. Current implementation
evidence and unsigned native gates are in [STATUS.md](STATUS.md); runtime ownership and the
single-survey execution model are in [ARCHITECTURE.md](ARCHITECTURE.md).

Engine-evidence citations use `D/...` for paths in the local decompiled `Assembly-CSharp` review
corpus and `B/...` for paths in the locally installed `StreamingAssets/Base` corpus. These are
evidence locators, not repository paths. This project redistributes neither decompiled source nor
installed game data.

## Getting the system

```csharp
using ThousandAndFirst;

KingdomSystem kingdom = XRL.The.Game.RequireSystem<KingdomSystem>();
if (kingdom.Founded) { /* ... */ }
```

`RequireSystem` creates the system if absent, so it is always safe. Every property below is
readable at any time; a kingdom that has not been founded reports `Founded == false` and
neutral values.

## External ground-ownership provider protocol

The dependency-free core publishes `ThousandAndFirst.Api.IKingdomExternalOwnershipProvider` and
`KingdomExternalOwnershipProviderAttribute`. A provider is a read-only translator for one
external ownership system. Mark one public parameterless implementation; TAF discovers it through
Qud's mod-sensitive type catalogue. Provider IDs must be unique, stable, and no longer than 64
characters. Versions are stable strings up to 32 characters.

```csharp
[KingdomExternalOwnershipProvider]
public sealed class MyOwnershipProvider : IKingdomExternalOwnershipProvider
{
    public string ProviderId => "MySettlementMod";
    public string ProviderVersion => "1.4.0";

    public bool TryObserve(Zone activeZone,
        out KingdomExternalOwnershipObservation observation, out string failure)
    {
        // Read only the supplied exact active zone and your existing registries.
        // Return false/null/null when it is not yours.
    }
}
```

`TryObserve` receives the exact active zone. It must not load another zone, create/claim a
settlement, move or convert a person, publish a building, clear leadership, or alter either mod's
state. Return `false` with both outputs null for unowned ground. Return `true` with a complete
observation for owned ground. A nonempty `failure` means observation could not be proved and TAF
fails closed. Throwing has the same result.

| Observation member | Contract |
|---|---|
| `ProviderId`, `ProviderVersion` | Must exactly equal the provider properties. |
| `OwnerGuid` | Nonempty lowercase canonical `Guid` D text for the external settlement/owner. |
| `SectorGuid` | Lowercase canonical nonempty `Guid` D text when zone-level evidence exists; otherwise null/empty. |
| `Evidence` | Stable bounded evidence kind, not display prose. |
| `ZoneId` | Must exactly equal the supplied active zone ID. |
| `ParasangId` | Stable external ownership cell evidence. |

`KingdomExternalOwnershipRules` is the engine-free v1 validation, canonical codec, equality, and
binding-verdict surface. `Encode(None())` records an explicit unowned decision;
`Encode(Bind(observation))` records exact provider/version/owner/sector/zone/parasang evidence.
Both accepted modes remain permanent TAF-owned zone evidence after the claim commits, so a later
owner appearing on explicitly unbound ground is detectable. Bindings contain strings only, never
serialized foreign objects. Directly writing zone binding properties or invoking the internal
founding adapter is unsupported.

The shipped `Integrations/Hearthpyre223` provider implements this protocol only when exact enabled
Hearthpyre 2.2.3 loaded first. Other versions do not load the shard. Qud Industry 0.3 has no typed
API in the audited release and remains resolved-capability based.

## Foreign exact-footprint provider protocol

`ThousandAndFirst.Api.IKingdomForeignFootprintProvider` and
`KingdomForeignFootprintProviderAttribute` expose spatial evidence to explicit player adoption.
A footprint is never a civic designation: by itself it grants no building key, accepted kind, cap,
tag, or benefit. The founder must still adopt the exact room for a lawful TAF role, and live
furniture or technology must still supply every effective benefit.

`TryObserve` receives only the exact active loaded zone. It returns bounded
`KingdomForeignFootprint` rows with exact `ProviderId`, `ProviderVersion`, stable `Identity`, stable
`Revision`, `ZoneId`, optional `SectorId`, `DeclaredCount`, origin, and the complete unique in-zone
cell set. A revision must change whenever membership or its trusted foreign evidence changes.
Providers must not load a zone, mint an identity while observing, or mutate either system. A false
result with no failure means no footprint is present; a provider-wide failure quarantines only
that provider's current evidence. It does not erase healthy siblings from another provider or
block an ordinary disjoint TAF room. A provider's returned array is an ordered protocol surface:
use a stable canonical identity order. A roster above 512 rows, or bounded row cell counts whose
sum exceeds 65,536, is a provider-wide protocol fault proved from array/count metadata before any
exact cell is enumerated. Null, empty, or individually over-row cell arrays remain row-local and do
not enter that sum. Once every provider is independently bounded, TAF applies the global row/cell
limits by stable provider-and-row round robin so an early provider cannot consume the complete
observation budget.

Rows normalize independently. When exact cells are known but metadata is malformed or ambiguous,
TAF retains those cells as bounded refused evidence and preserves independent healthy sibling
rows. A row with no bounded unique in-zone cells cannot supply or quarantine ground; its bounded
diagnostic remains local to that provider. Providers should therefore return known cells with a
bounded nonempty `Refusal` whenever possible. Duplicate identities and same-provider overlaps
refuse only their implicated rows; cross-provider overlaps likewise refuse only known intersecting
rows. Registration, callback, row, and budget faults are sanitized and bounded.

When an explicit adoption's measured room exactly equals one accepted row, its durable receipt
binds provider, version, identity, revision, and cells. Every later active read re-observes and
re-proves all of them. Missing/disabled providers, version changes, moved cells, changed revision,
duplicate identity, or ambiguous overlap pause that designation. A fault in an unrelated provider
or disjoint sibling does not invalidate an already bound exact row. TAF never guesses a rectangle
or loads remote ground to keep it active.

The exact-2.2.3 Hearthpyre shard implements this seam from `Home.ID`, `Home.Sector`,
`Sector.Homes`, `RealmSystem.Homes`, `Home.Count`, `Home.Origin`, and the enumerated
`Location2D` membership. It cross-proves the global, zone, sector, and Home registries by reference
and calls no Hearthpyre constructor, mutator, lifecycle, or zone-loading API. Sector identity,
the bounded sector-list snapshot, or before/after roster churn remains a provider-wide failure
because no coherent snapshot exists. The snapshot also includes every globally indexed Home whose
backlink names that sector, even when `Sector.Homes` omits it. A single Home's global-key,
backlink, duplicate-custody, or membership failure is row-local: proved cells return refused
evidence, an unprovable cell set returns a no-cell diagnostic, and independent Homes remain
observable. Home overlap is normalized by the host's ordinary row-local ambiguity rule. Every
registry, reverse-registry, backlink, and cell-enumeration read across both proof passes spends one
shared 1,048,576-entry callback work budget. Exhaustion is a deterministic provider-wide fault and
publishes no partial Home array, so separately bounded registries cannot multiply into a hang.

## `KingdomSystem` — the game system

| Member | Contract |
|---|---|
| `bool Founded` | True once a kingdom exists. Guard every other use with this. |
| `string SeatName` | The seated city's own name; falls back to the realm's display name for saves written before cities had names apart from their realm. |
| `int SettlementCount` | Cities the realm holds, 0–3: one seat plus at most two non-seat cities. |
| `KingdomSettlementTopology SettlementTopology`; `NonSeatSettlements()` / `NonSeatSettlementAt(int)` | Authoritative immutable-id-ordered collection/API for up to two cities not occupying the active seat. |
| `KingdomSettlement Away` | Obsolete save/integration projection of the first immutable-id-ordered non-seat city. Runtime code must use the topology API. |
| `KingdomSettlement Capture()` / `void Restore(KingdomSettlement)` / `bool TrySeat(Zone)` | Move the seat. `TrySeat` runs from `ZoneActivatedEvent`; the others are for tools and tests. |
| `string KingdomFactionName` / `KingdomDisplayName` | The runtime faction's name and display name; null when unfounded. |
| `string Style` | Canonical city style key (`common`, `verdant`, `fungal`, `moonstair`, `eater`, or one your mod declares). The frozen pre-v1 key `gyre` is accepted as an alias and normalized in live settlement state; it never implies Gyre Wight or Girsh allegiance. Drives which building designs are offered. |
| `GrowthStage Stage` | `Camp`, `Steading`, `Village`, `Town`, `City`. **Moves in both directions.** `KingdomSubsidenceRules.StageWithHysteresis` is the only writer: it climbs on the reading and falls only on a clear shortfall (20% benefit of the doubt on both of `StageFor`'s inputs), one rung per reckoning, with `Camp` an absolute floor. Read it, never assume it — and never assume a rung already reached is kept. |
| `int SupportedLevel` | Settlers the settlement's finished works honestly carry, from `KingdomSubsidenceRules.SupportedLevel`. **Knowledge, not truth**: it is as fresh as the last pass that measured it, and `0` means no pass ever has. Consumers that refuse something on it must check for that. |
| `int NotableShade` | What the settlement's named notable is worth to that level (`KingdomCeremonyRules.NotableShade`: met tastes, the virtue net of the flaw, and met `Prefers`). Written when the office is filled or passes, so it is as stale as the last time it changed hands; `0` for a settlement that has named nobody. Never negative, and bound again by `KingdomCatalogueRules.LiftCapPercent` when the level reads it. |
| `string SubsidenceBinding` | Live binding is `water` or `roof`, or null before a measurement. Legacy `food` values remain wire-readable but normalize to no live cause and cannot drive subsidence prose or consequence. |
| `long LastWaterWorkTick` | **W6: the published mirror of the city model's `ProcessedThroughTick`, written by `KingdomCity.Stamp` and by nothing else.** It used to be the settlement pass's own checkpoint for water-works production; that arithmetic moved onto the model, per zone, off one clock, so that no day can be billed by two owners. What it still records is what it always said — the tick through which the settlement's works have been paid — and it is still what makes a catalogue `Carries="water:N"` a **flow** as well as a level. |
| `long LastFoodWorkTick` | The attended mills' checkpoint; the city model deliberately does not touch it. Fields own their crop-cycle stamps and mills take real crops from real shelves and put real staples back. Advancing this from the city reckon would suppress the next physical mill operation. |
| `int Population` | Living settler count. |
| `bool Withered` | True while a sustained thirst has suspended prosperity. Recoverable. |
| `int DryStreak` | Heartbeats in a row whose physical water bill went unpaid. Water alone owns the live scarcity ladder. |
| `int HungerStreak` / `bool Famished` | **Legacy save/wire compatibility only.** Normalization and every heartbeat clear these fields; new code never advances or acts on them. |
| `List<string> ClaimedZones` | Zone IDs the kingdom holds. |
| `Dictionary<string,string> ZoneDistricts` | Zone ID → district key. |
| `List<string> ChronicleEntries` / `OutsiderEntries` | The two registers, oldest first, capped. |
| `Dictionary<string,int> OriginCounts` | Settler origin → count (population composition). |
| `Dictionary<string,int> RegardForRealm` | Foreign faction → realm regard. This is the authoritative inbound civic direction used for offers and foreign attitudes toward realm citizens. Treat as read-only; use the named methods below. `Standings` remains the mutable compatibility field backing this same map. |
| `Dictionary<string,int> RealmPolicyToward` | Realm → foreign-faction policy, projected only onto the realm faction's exact edge. Absence means **unspecified** and writes no edge. Treat as read-only. |
| `Dictionary<string,int> RegardSpilloverRemainders` | Signed hundredths carried when permanent personal reputation spills into `RegardForRealm`. Treat as read-only. Standing plus carry is one canonical scaled pair: zero carry rows are absent, carry has the scaled total's sign, and saturated endpoints retain no outward debt. Equal weighted histories are partition/order independent only while no intermediate update clips at an integer endpoint. |
| `Dictionary<string,int> RegardSpilloverObservedReputation` | Last personal-reputation event poststate observed per eligible faction. This persisted advisory observation is neither civic regard nor civic policy and is never dedupe authority. Treat as read-only. |
| `KingdomPolityLedger PolityLedger` | Realm-scoped semantic polity authority. Treat as read-only host state: call `KingdomPolityRules.Usable` before reading rows and never treat a faction, creature, route body, dialogue, or map mark as authority over it. Unknown or quarantined state is inert. |
| `KingdomPolityRealmTransition PolityTransition` | Durable exile/return/refound receipt. It owns phase/revision CAS, exact-return escrow, bounded legacy facts, old owned-faction tombstone evidence, and fresh refound ids. Do not copy or mutate it directly. |
| `int GetRegardForRealm(string factionName)` | Read foreign-faction → realm regard. Returns 0 for an unspecified entry; check `RegardForRealm.ContainsKey` when that distinction matters. |
| `bool TrySetRegardForRealm(...)` / `SetRegardForRealm(...)` / `AdjustRegardForRealm(...)` | Set or adjust only foreign-faction → realm regard. Refuses an unfounded/pending realm, missing or reserved endpoint, malformed key, malformed standing/carry root, or full ledger. Absolute `Set` clears that edge's fractional carry in the same copy-on-write publication; `Adjust` adds whole points to the complete scaled pair and preserves canonical carry unless it clips. Successful writes optionally project only that edge. |
| `bool TryAdjustRegardForRealmBatch(IList<KeyValuePair<string,int>>, bool mirror = true)` | Validate pair canonicality, capacity, authority, uniqueness, and every foreign endpoint on private copies before one all-or-none standing-and-carry root publication. Allocation, arithmetic, or validation failure leaves both old roots exact. Each edge saturates without outward carry, then the method optionally projects only those exact edges. |
| `bool TryGetRealmPolicyToward(...)` / `int GetRealmPolicyToward(...)` | Read realm → foreign-faction policy. `TryGet` preserves unspecified versus explicit zero. |
| `bool TrySetRealmPolicyToward(...)` / `void AdjustRealmPolicyToward(...)` | Set or adjust only realm → foreign-faction policy and optionally project that exact edge. These methods never infer a matching inbound-regard change. |
| `void MirrorRegardForRealm(...)` / `MirrorRealmPolicyToward(...)` / `ReassertFeelings()` | Re-project only recorded, namespaced pair entries. Unknown factions are not created and unspecified entries write nothing. |
| `GetStanding` / `SetStanding` / `AdjustStanding` / `MirrorFeeling` | Compatibility aliases for the **inbound** `RegardForRealm` direction. New integrations should use the directional names so symmetry is never inferred accidentally. |
| `static void Guard(string step, Action work)` | Run work inside engine dispatch without letting exceptions escape. Use for any code the engine invokes. |

A clean new realm starts both civic directions, signed carry, and diagnostic observation empty; it
does not enumerate or inherit personal reputation. Permanent, non-transient
`AfterReputationChangeEvent` deltas spill only into
`RegardForRealm`; `RealmPolicyToward` and vanilla personal reputation remain separate. The rate
dampens from Camp to City; canonical signed fractional carry makes equal weighted histories
independent of event batching and order while no intermediate update clips. Saturation deliberately
discards overflow debt, so a clipped history is not promised reversible or reorderable. Transient and master-disabled events update only the advisory observation, so they
never become later catch-up.

Native Qud can also set personal reputation without emitting this event, so an observed poststate is
advisory and never suppresses a later permanent event as a presumed replay. Direct native
`Reputation.Set` writes do not spill. The engine event has no durable source ID; this coarse adapter
must not be treated as grievance evidence, while typed grievances and their dedupe receipts use
their own adapters.

Serialization-v8 load migration reconstructs outbound policy only from canonical explicit feelings
on the exactly owned realm faction and the phase-appropriate current polity ledger or validated
exile escrow. It never mirrors inbound regard, reads personal reputation, or seeds carry/observation.
Impossible phase pairs, missing or reserved endpoints, ambiguous feelings, and refounded `Rebound`
archives whose old-ledger envelope was deliberately destroyed quarantine rather than infer authority.

## Semantic polity authority

`KingdomPolityLedger` is bounded persisted meaning, not a background-world simulator. Its records
describe current/imported/authored/vanilla-reference polities, directional relations, immutable
fact-derived profile revisions, semantic routes, caused grievances and fronts, finite cohort plans,
scarce named figures, witnessed incident plans/conclusions, projection receipts, independent
import/presentation options, and declared compaction evidence. Engine factions, creatures,
dialogue, map marks, and encounters are reversible or witnessed projections with their own
receipts; they never become semantic authority by existing.

| Member | Contract |
|---|---|
| `KingdomPolityRules.Usable(ledger)` / `TryValidate(ledger, out failure)` | Whole-graph admission. Current compatible state is canonical, bounded, referentially complete, and owner-bound or the whole authority refuses. Unknown/future and quarantined state is never usable. |
| `KingdomPolityRules.Clone(ledger)` | Canonical deep copy through the current strict wire envelope. Invalid state throws before a copy is exposed. |
| `KingdomPolityRules.TrySetEmptyImportPolicy(...)` / `TryPublishFoundation(...)` / `TryObserveCurrentFoundation(...)` | Typed compare-and-swap foundation lane. It freezes import consent, publishes the current realm plus zero/one bounded legacy snapshot atomically, and later observes the frozen authority without re-reading mutable live facts. |
| `KingdomPolityRules.TryPrepareLegacyFaction(...)` / `TryCommitLegacyFaction(...)` / `TryPrepareLegacyFactionTombstone(...)` / `TryCommitLegacyFactionTombstone(...)` | Semantic half of owned faction projection and retirement. The Qud adapter must prove the exact prepared id/digest; it may recover missing owned state but never reuse an old faction id or overwrite divergent/foreign state. |
| `KingdomPolityRules.TryPrepareRealmExile(...)` / `TryMarkRealmExileTombstoned(...)` / `TryDetachRealmExile(...)` / `TryRestoreRealmReturn(...)` / `TryCommitRealmRefound(...)` / `TryCompleteRealmReturn(...)` | Typed current→legacy transaction. One CAS ends current/imported polities at the archive close tick; owned physical factions then tombstone before detach. Exact return restores the byte-identical source ledger. Refound requires fresh realm/polity/faction ids and destroys rollback escrow. Retry acknowledges only exact state; divergence refuses or quarantines. |
| `KingdomPolityProfileRules.TryCreateCurrent(...)` / `TryCreateLegacy(...)`; `KingdomPolityProfileExpressionCatalogue.Resolve(...)` / `TryMerge(...)`; `KingdomPolityNpcRules.TryResolve(...)` | Pure immutable profile and NPC expression. Current rules-v3 foundation bodies come only from exact positive species plus audited identity/body facts; later revisions rebuild from exact population-body facts. Unknown bodies freeze as `unresolved`, which current cohort resolution refuses cleanly. Origin, culture, style, creed, and architecture cannot manufacture biology. Technology comes from current zoning knowledge (`KingdomZoning.Tech` at foundation; sorted roster `TechPoints` later), never growth stage. Wire-v7 profiles carry canonical weighted cues with source/reason facts. Resolver pins a bounded level, preserves stock stats and natural mutations, then adds only admitted role skills and technology/role gear—no current direct ability-score package. Additive catalogues merge only through collision/bound validation. Older profile/resolver rules remain frozen compatibility lanes. |
| `KingdomPolityRules.CanEmitOptionalProjection(ledger, causeTick)` | Presentation option gate. Enabling starts at its recorded future-cause floor; disabled time never creates a backlog. Semantic causes remain recorded even when optional rendering is off. |
| `KingdomPolityManifestRules.TryCreateErrandProof(...)` / `TryCreateCargoProof(...)`; `KingdomPolityRouteRules.TryPlan(...)` / `TryDepart(...)` / `TryAdvance(...)` / `TryDeliverEntitlement(...)` / `TryReturn(...)` | Conserved route protocol. Errands prove zero cargo; cargo proofs bind the external custody owner and digest. Semantic travel changes endpoint phase only and never loads a zone or creates a walking actor. |
| `KingdomPolityCorrespondenceRules.TryCreateProof(...)` / `TryDescribe(...)` | Binds one message to an exact route/counterparty/digest and exposes a bounded phase/verb view without inventing a courier body. |
| `KingdomPolityCohortRules.TryPlan(...)` / `TryPrepareEndpointManifestation(...)` / `TryCommitEndpointManifestation(...)` / `TryConcludeEndpointCohort(...)` / `TryCommitEndpointCleanup(...)` | Freezes 1–7 exact resolved members and runs the prepare→commit→conclude→cleanup receipt protocol. At most one member is a named face. Physical adapters must preserve foreign objects and may not remint a missing committed body. |
| `KingdomPolityDiplomacyRules.TryOpenGrievance(...)` / `TryPlanTerms(...)` / `TryAnswerTerms(...)`; `KingdomPolityHospitalityRules.TryCreateProof(...)` | Caused diplomacy only. Standing/creed is not a grievance; answers are exact CAS choices. Hospitality is optional transaction evidence and empty food cannot block ordinary diplomacy. |
| `KingdomPolityRules.MaxPolities` / `MaxRoutes` / `MaxActiveFronts` / `MaxCohortMembers` | Hard attention bounds: four total polity records, eight routes, one active front, and 1–7 bodies in one cohort. The graph additionally permits at most one substantive live external polity and one named representative per cohort. |

Profile revisions are immutable once referenced. A cohort freezes exact profile id/revision and
resolved members before physical manifestation. A conflict plan freezes participants, stakes,
surfaces, interventions, and deterministic inputs, but never a winner, casualties, or conquest.
Only a fresh live scene may record observed facts; the separately consented escrow lane may apply
only its exact reserved stake and at most one reversible wound. Incident views and aftermath
rendering cannot author a conclusion.

The v3 codec is canonical and fail closed: wire v1 and v2 have independent migration fixtures,
future versions remain byte-exact opaque and inert, hostile lengths/trailing bytes refuse, and invalid
current graphs quarantine while preserving inspectable evidence. The host owns binds, transitions,
projection commits, and compaction. External code may read compatible state; direct list mutation
is unsupported.

## Assenting-moot authority

`KingdomAssentingMootReceipt` is copy-on-write city authority for one exact moot. Native zone and
body parts are reversible projections, never semantic authority. Receipt and phase numeric values
are append-only. Direct field/list mutation is unsupported: use the pure rules and publish the
returned copy through the owning city transaction.

| Member | Contract |
|---|---|
| `KingdomAssentingMootRules.ActivationEligible(...)` | Pure all-proofs gate for founded state, assent node, Chavvah rite, claimed surface, cardinal Moon Stair adjacency, and owner readiness. The runtime key remains derived and is never learned. |
| `StrengthFor(validAssents, grantedExemptions)` | Ten native stabilization strength per current valid assent, minus ten per durable exemption, clamped to 0–60. |
| `TryPrepare(...)` / `TryChangeMember(...)` / `TryRebind(...)` | Authenticated copy-on-write authority for exact realm/city/zone/building/lot and bounded named bodies. Six assents and six exemptions are hard caps. |
| `PrepareProjection(...)` / `Applied(...)` / `Suspended(...)` / `Quarantined(...)` | Append-only projection lifecycle. Suspension has zero strength; quarantine preserves bounded inert evidence. |
| `Validate(receipt, out failure)` | Admission gate for version, phase, authority hash, canonical membership fingerprint, bounds, dates, and strength. Invalid state must not project. |

The engine adapter uses one native `ZoneParts.AmbientStabilization` immediately beside its
namespaced owner marker and owns only effects stamped to the exact moot building. It refuses
pre-existing, duplicate, or foreign fields instead of adopting them. Exempt exact resident bodies
veto `ApplyAmbientRealityStabilized`; no NormCore or global faction mutation is part of this API.

## `KingdomFounding` — founding, territory, citizenship

| Member | Contract |
|---|---|
| `static Faction Found(string name)` | Founds the kingdom (idempotent — returns the existing faction if already founded). |
| `static bool ClaimZone(Zone z, bool force = false)` | Claims a zone; requires adjacency to existing ground unless forced. Adjacency includes the stratum directly above or below (`ZonesAdjacent`), so a cellar or a tower is a claim now, not only a founding-day accident. |
| `static bool EnrollCitizen(GameObject citizen)` | Makes a creature a citizen. Enrolled creatures are protected from kingdom-driven removal. |
| `static SecondFoundingVerdict JudgeSite(KingdomSystem, Zone)` | What the rite would do on this ground. |
| `static bool FoundSecond(string name, string vocation, Zone site, bool force = false)` | Historically named compatibility entry point that founds the next additional city: second or third, while a non-seat slot remains. `force` waives only the not-adjacent requirement. |
| `static KingdomZoningRules.ClaimVerdict JudgeClaim(KingdomSystem, Zone)` | What the founder's own claim on this ground would do — gathers the facts off the world (ours, the other city's, an exiled realm's, foreign, adjacent) and hands them to the pure verdict below. The engine-coupled half of `KingdomZoningRules` § *The claim*, below. |
| `static string StyleGroundClause(string style)` | Lower-case founder-facing clause naming what the ground promises for a city style ("common ground", "ground green enough to root a verdant city"). Presentation only — `KingdomData.StyleForSite` owns which style a site resolves to. |

Founder-basin publication is receipt-owned. A village charter stages a property-map write-ahead
effect binding transaction, authority, exact faction/display/site, and canonical standing/carry
before/after pairs before changing civic regard. `Prepared` plus the exact after pair is the single
supported save/exception cut before `Applied`; a raw standing threshold is never publication proof.
Pre-effect completed covenants remain readable from their exact archived covenant row, while an
unarchived legacy threshold cannot be adopted. The old direct `CharterVillage` entry point is an
inert compatibility stub; integrations must enter through the founder-basin transaction.

## `KingdomExileRules` — regard, expulsion, and return

Pure and engine-free. The realm's regard for its founder is the vanilla reputation cell for its own
faction, so there is no second economy: it falls from deeds and never from time.

| Member | Contract |
|---|---|
| `enum RealmRegard` | Beloved, Trusted, Doubted, Resented, Repudiated. Ordered best-first, so a larger value is a worse standing. |
| `ClassifyRegard(int)` / `RegardName` | Where a reputation value sits on the ladder, and its name. Agrees with vanilla's own thresholds — `kingdom:selftest` walks both directions to prove it. |
| `JudgeRegardStep(...)` | Whether a change of regard should speak. Has hysteresis: jitter across one threshold says nothing. |
| `JudgeExile(...)` / `JudgeReturn(...)` / `ShouldOfferReturn(...)` | Whether the realm puts the founder out, whether it would take them back, and whether to ask. Founding again shuts the door. |
| `ExileTelling` / `ExileRumour` / `ReturnTelling` / `ReturnRumour` | The two registers' accounts, which deliberately disagree. |

## `KingdomManifest` — one load of water between two cities

| Member | Contract |
|---|---|
| `KingdomSystem.Manifest` | The realm's one in-flight manifest, or null. Realm-level and never swapped; it addresses cities by name, because seat and Away exchange roles. |

Drams leave the origin's stores when it is loaded and arrive at the destination's **next attended
pass**: the load is physical and somebody has to be there to take delivery, which is a haulage
fact rather than a clock policy. The window it must arrive inside is real elapsed time. One
manifest may be in flight at a time; a lapsed window is written off once, in the chronicle.

## `KingdomCropRules` / `KingdomCrops` / `KingdomPlot` — seeds, rows, and the harvest cycle

A field starts as bare ground and produces nothing until the founder puts seed in it. That is
Addendum 11(b)'s gate, and it is one rule read in one place: an unsown field reports **no `food`
capacity at all** because `KingdomCrops.WithoutUnsownFood` strips the `food` entry out of its parsed
`Carries` inside `KingdomSubsidence.Supports`. Food never enters live population level either way.
Everything else the
design carries is untouched; a home farm's mill is built whether or not a row is in the ground.
An unsown field also drops out of `KingdomSurvey.Works`, so the staffing pass never sends anybody
to stand in it.

**The seed items.** Five, one per style's crop family, in `ObjectBlueprints.xml`. They are
ordinary items rather than `Food` — an edible seed could otherwise be spent by an explicit food
transaction instead of planted. Three honest sources: the tier-1/tier-2 wares tables (`PopulationTables.xml`), a
harvest returning its own on a counter-based draw (`KingdomCropRules.RollSeedReturn`), and
stripping a wild plant of the same species, once per plant, where vanilla ships one
(`r_KingdomWildSeed`, merged onto `Watervine`, `Starapple Tree`, `Godshroom` and `Dreadroot` with
`Load="MergeIfExists"`).

**The designation.** `r_KingdomSeed` offers a `Sow` inventory action. Standing anywhere in a
finished field's footprint, the founder is shown the crop, the rows, the wait and the water
(`SowConfirm`, the carry-sign's consent-before-cost shape) and confirms; one seed is spent,
`PlantWaterCostDrams` is drawn once, real crop plants are laid across the footprint, and the
planting is dated in both registers. `AssessSow` is the whole gate as one tabled decision, and
`SowRefusal` names the want for every way it can fail. The field itself offers `Withdraw Seed`,
which is the protection law made operable: a committed seed is the founder's designation, the
rows come up when they take it back, and nothing else can.

**The rows are real.** `r_KingdomRow*` blueprints inherit vanilla `Plant` (so `PlantProperties`
roots them) and carry vanilla `Harvestable` with the crop as `OnSuccess`. They go in green
(`StartRipeChance="0:1"`); what ripens them is the stamped cycle. A ripe row can be gathered by
hand for a real crop item, and the settlement's own gathering counts rows still standing **ripe**,
so what the founder took is not also credited to the city.

**The cycle** (Addendum 11(b-ii)). Planted tick → ripe after `CropDays`; the founder gets
`GatherDelayTicks` (one day) alone with it; then the settlement gathers, attended or not. The
harvest credits the ledger at once and the physical crop goes into a pantry in this zone if there
is one, onto the road to another of the city's zones if the sighting record says there is room
there (`KingdomCrops.LarderRoomElsewhere` / `KingdomSystem.PendingCrop`), and on the ground if
neither. The stamp restamps **from the harvest**, never from now, so the part-cycle already grown
is kept. `CyclesDue` is closed form: a season away resolves every completed cycle in one
reckoning, and a season of harvests tells **once, with a count** (`HarvestChronicle`).

| Member | Contract |
|---|---|
| `KingdomCropRules.CropDays` / `YieldPerRow` / `GrowTicks` / `GatherDelayTicks` | The whole denomination. A design standing R rows makes `R × YieldPerRow / CropDays` servings a day. |
| `KingdomCropRules.FoodPerDayForRows(int)` / `RowsForFoodPerDay(int)` | That derivation, both ways. `_notes/balance-sim.py` §G2 asserts it against the real catalogue and the real blueprints. |
| `KingdomCropRules.CropDaysForStyle(string)` | Every style answers `CropDays`, and the test table says so out loud: `Carries` is one number per design, so a per-style cycle would make the same field carry differently on different ground. |
| `KingdomCropRules.CyclesDue` / `LastRipeTick` / `RestampedRipeTick` / `MayGather` | The cycle, closed form and uncapped in time (bounded only by `MaxCyclesPerVisit` arithmetic). |
| `KingdomCropRules.HarvestYield(int rows, int effectivenessPercent)` | Rows × yield × what the field is running at — the same effectiveness `Supports` folds `Carries` by. |
| `KingdomCropRules.GatherableCycles(long, long, out bool holdsLast)` / `GatheredYield(int standing, int ripe, int cycles, bool countsRipeLast, int effectivenessPercent)` | The founder's-day rule and the credit that follows from it, both pure. Every cycle but the one the founder was actually looking at is credited at what **stands**; that one is credited at what stands **ripe**. |
| `KingdomCropRules.IrrigationTicksPerPulse` / `IrrigatedRipeTick(long, long)` | Vanilla's `AccelerateRipening` answered on our clock. `Hydraulic Irrigator` fires it on its own radius off its own charge; each pulse pulls the field's stamp ten ticks earlier, bounded at now, so an irrigated crop ripens in half its days. It does nothing to any plant the game ships, because none of them arms `RegenTime`. |
| `KingdomCropRules.SeedForCrop` / `CropForSeed` / `SeedForStyle` / `RowForCrop` / `SeedBlueprints` | The seed↔crop↔row maps. `Art/check_xml_refs.py` walks all of them against `ObjectBlueprints.xml` in both directions. |
| `KingdomCropRules.AssessSow` / `SowRefusal` / `SowConfirm` / `WantNote` / `FieldWant` | The gate and everything it says. STANDARDS §7b: no field stalls in silence. |
| `KingdomCropRules.RollSeedReturn` / `SeedReturned` | Whether a gathering hands back sowable seed. Counter-based on settlement, field and that cycle's own ordinal, so a reload never re-rolls it. |
| `KingdomCrops.RowsTag` (`r_KingdomCropRows`) | How many rows a design stands, declared on the **blueprint** for the reason a pantry's capacity is. |
| `KingdomCrops.WithoutUnsownFood` / `CycledFoodPerDay` | The support gate and the crop cycle's average-yield equivalence used for catalogue validation. `CycledFoodPerDay` is not an away-time item credit. |
| `KingdomCrops.AttemptSow` / `Withdraw` / `TakeWildSeed` / `LayRows` / `ClearRows` / `RowsOf` / `SetRipe` | The engine-coupled half. Only rows this file created and marked are ever destroyed. |
| `KingdomCrops.RecordLarders(KingdomSystem, Zone, KingdomSurvey, long)` / `LarderRoomElsewhere` / `DeliverPending` / `Deposit` | Cross-zone delivery. Both records now read and write the seated settlement's city book (`KingdomSettlement.City`) rather than the retired `r_TAF_Larders_*` game-state pair; the numbers are the same, the home is one. |
| `KingdomSystem.PendingCrop` / `PendingCropBlueprint` | One city's harvest still on the road, carried by the seat swap on its own name. |

## Food as a physical, positive transaction chain

Food and water share physical custody, but they are not mirrored upkeep systems. Water retains
its live physical bill and scarcity ladder. Food moves seed → crop row → physical harvest →
dedicated larder → explicit meal, recipe, industry, or trade transaction. Time and population
create no food debit.

| Member | Contract |
|---|---|
| `KingdomRules.RationsPerDay` / `RationsForElapsed` / `ForagedRations` | Legacy source-compatible projections. All return `0`; abstract ration billing and foraging are retired. |
| `KingdomRules.ResolveHunger(...)` / `HungerOutcome` | Legacy wire vocabulary. Every input projects `Fed`; no food state can warn, depart, mark, or kill. |
| `KingdomRules.ComposeScarcity(ThirstOutcome, HungerOutcome)` → `ScarcityVerdict` | Water-only composition. The legacy hunger argument is ignored; bite, health, withering, and departure come only from thirst. |
| `KingdomRules.ScarcityDepartureClause(bool, bool)` / `ScarcityDepartureNote(bool, bool)` | Water-departure wording. The legacy food flag is ignored. |
| `KingdomRules.LarderCapacityTag` / `DefaultLarderCapacity` / `LarderCapacity(int declared)` | How much a dedicated container holds. Declared on the **blueprint**, never in the catalogue. |
| `KingdomRules.CivicLarderBlueprints` / `IsCivicLarderBlueprint(string)` | Which commissioned designs auto-dedicate as pantries (STANDARDS §7's "commissioned storage auto-flags"). |
| `KingdomSurvey.FoodStored` / `FoodCapacity` / `FoodSpace` | The food side of `StoredWater` / `StorageCapacity` / `StorageSpace`. `FoodSpace` is **derived** from the other two, so a caller that puts food in by another road cannot leave it stale. |
| `KingdomSurvey.StoreFood(int, string blueprint)` / `ConsumeFood(int)` / `ConsumeFood(int, string preferred, out int fromPreferred)` / `ConsumeCrop(string, int)` / `AdoptLarder(GameObject)` | Explicit physical food transactions plus pantry dedication. They keep survey counters in step and return what actually moved. The three-argument `ConsumeFood` is the **meal-shaped** draw (below); `ConsumeCrop` is the mill's input half — one named blueprint only, so a mill never grinds the staple it just made. |
| `KingdomSurvey.TryDebitFoodFromExact(GameObject, int, out int)` | Exact destructive source debit for an explicit receipt-backed transfer. It reproves container, inventory/list, item identities/counts, spendable custody, topology, and counters around each callback. Wear, hunger, and elapsed catch-up never call it. |
| `KingdomSurvey.SpoilFrom(...)` / `TrySpoilFromExact(...)` | Obsolete source-compatible projections for retired passive spoilage. They return `0` / `false`, never inspect the container, and never mutate stock. |
| `KingdomCapabilityRuntime.Count(..., "taf:cooking", ...)` | Number of exact designations whose current physical snapshot credits cooking. Native `Campfire` is one generic Plot-scoped provider; an accepted tag, catalogue category, or finished root alone supplies nothing. |
| `KingdomGrowth.FoodMadePerDay(KingdomSurvey)` | Legacy city-rate seam; always `0`. Fields create physical crop objects through their harvest cycle and mills transform exact physical inputs, so away-time support rows cannot mint food. |
| `KingdomGrowth.ScarcityEnabled` / `ThirstEnabled` / `HungerEnabled` | `ScarcityEnabled` and `ThirstEnabled` read the water option. `HungerEnabled` is a legacy alias that returns `false`. |

**The conservation identity.** Catalogue `food` remains useful support/capacity metadata, but it
is not an away-time item rate and never becomes a household bill. Crop cycles create physical
objects; mills consume and replace exact physical objects; explicit consumers debit exact
spendable custody. A heartbeat and the city carry/reify model neither mint nor remove food.
Consequently no elapsed-time split, absence, load, or old hunger ordinal can change pantry stock,
population, or settlement state. Water scarcity remains unchanged and `ComposeScarcity` ignores
the legacy hunger argument rather than combining two ladders.

### Meals, not ticks — the favoured dish (Addendum 11(b))

The shared meal is an explicit Charter action, not a heartbeat. The whole chain is one thing:
fields grow the crop, the mill binds it into the staple, a currently capable physical kitchen
proves somewhere to cook it, and one confirmed transaction debits its disclosed ingredients.

| Member | Contract |
|---|---|
| `KingdomRules.DeriveDish(string realm, string creedRecipe, string crop)` → `FavoredDish` | Pure, total and deterministic. **Creed picks the form, ground picks the body.** `creedRecipe` is the dominant creed faction's own vanilla `WaterRitualRecipe`; runtime passes the crop from `KingdomData.CropForStyle`. No input is an error — a realm of mixed people eats a stew. |
| `KingdomRules.DishFormFor(string creedRecipe)` / `DefaultDishForm` | Vanilla's eight favourite dishes → a form word. Every word returned is one of `CookingRecipe.ingredientTileTypes`, which is what gets a derived dish a drawn tile instead of a defaulted one. |
| `KingdomRules.CropWordFor(string crop)` / `PreservedStapleFor(string crop)` | The crop as an ingredient, and what it becomes when it is bound to keep. Three staples are vanilla's own `PreservableItem Result`; two crops vanilla cannot preserve get mod blueprints that *inherit* the nearest shipped preserve. `PreservedStapleFor` returns null for a crop this build has no staple for, and `KingdomCrops.StapleFor` then falls back to the crop's own `PreservableItem`. |
| `KingdomRules.DishRecipeType` | `"r_KingdomFavoredDish"` — the one `CookingRecipe` subclass every realm's dish resolves to, in `XRL.World.Skills.Cooking`. It reads its display name and components off `KingdomSystem`, so one class serves every settlement. |
| `KingdomDish.Ensure(KingdomSystem, bool announce)` | Derives and stamps the dish onto the realm's **`Faction`** (`WaterRitualRecipe`, `WaterRitualRecipeText`) and onto `KingdomSystem`. Idempotent; called at founding and on every settlement pass, so a city whose creed drifts changes what it is known for and says so once. `RecipeGenotype` is deliberately never set — both gates it drives refuse somebody dinner. |
| `KingdomSystem.DishName` / `DishText` / `DishStaple` / `DishSource` | Realm state, not city state: a realm has one faction however many cities it holds. |
| `KingdomRules.JudgeMeal(int owed, int fromDish, int fromStores, bool hasKitchen, GrowthStage)` → `MealVerdict` | A complete exact debit plus kitchen yields `Plain`; when the entire disclosed cost came from the named staple it yields `Favored`. Missing kitchen or incomplete debit yields `None`. `Scraps` remains a legacy ordinal and is never produced. |
| `KingdomRules.CanHoldSharedMeal(food, population, cookingProviders)` | Runtime gate: residents, spendable physical ingredients, and at least one current `taf:cooking` provider are all required. |
| `KingdomRules.MealShadeFor(MealVerdict)` / `FavoredMealShade` | Legacy population-capacity projection; always `0`. Shared meals instead apply their existing bounded creed easing and cohabitation progress after exact completion. |
| `KingdomSystem.MealShade` / `ScrapsAnnounced` | Legacy save/wire fields, normalized to zero/false. `LastMeal` remains harmless historical evidence. |
| `KingdomSystem.Shade` | Returns `0`; food history never changes population support or later causes subsidence. |

Food in storage creates no indefinite passive aura. Empty stock or a missing kitchen reports that
the optional act is unavailable and spends nothing; it creates no hunger, penalty, catch-up, or
departure. A completed meal's positive effects are bounded by their own existing rules.

**The draw order, stated once.** Larder by larder in survey order, item by item in inventory
order: the staple first, then everything else that is food. Nothing is random, so the same
larders drained in the same sequence give the same answer on every reload — what Addendum 12(d)
asks of any draw that lands on containers a founder can open.

### Industry eats food — the mill (Addendum 11(b)/(c))

| Member | Contract |
|---|---|
| `KingdomCrops.IsMill(GameObject)` | Asked of the **object**, off vanilla's own `Mill` part, so a third party's millstone counts the moment it declares one. |
| `KingdomCrops.MilledFoodPerDay(KingdomSurvey)` | Bounded physical mill throughput requested by the attended settlement pass. It is never an away-time item credit. |
| `KingdomCrops.StapleFor(string crop)` | The stated staple, else the crop's own `PreservableItem.Result`, read off a sample. |
| `KingdomRules.PreserveMultiple` / `MillCropsPerDay` / `MilledGain(int)` / `CropsForGain(int)` | The conversion. **Two crops in, six staples back, a net of four** — which is exactly the grinding mill's declared `Carries="food:4"`. `_notes/balance-sim.py` §G3 asserts that identity against the catalogue XML. |
| `KingdomRules.MillableStock(int foodStored, int population)` | The nonnegative physical stock; population is retained only for source compatibility. `ConsumeCrop` narrows the actual debit to the exact raw-crop blueprint, and the operating mill bounds the request. There is no hidden household reserve or bill. |
| `KingdomLedger.Milled` | The gain only. The crops themselves were counted when they were gathered. |

×3 is vanilla's `Vinewafer` → `Vinewafer Sheaf` figure and the **least** of the three numbers this
mod's crops carry, so the settlement never books more than the thinnest preserve in the game
actually gives. It is flat across styles for the same reason `CropDaysForStyle` is flat: the
ground a settlement is founded on is not chosen by the founder.

**The machine and the accounting are different stock, on purpose.** `r_KingdomGrindMill` carries
the real `Mill`, `Container`, `Inventory` and a `MechanicalPowerTransmission` consumer — the
first consumer this mod has put on the mechanical grid, so a mill raised beside the settlement
water wheel is genuinely driven by it. That part grinds the *mill's own inventory* while you are
standing there, at vanilla's per-crop numbers; the settlement pass grinds the *larders* on the
settlement's clock. Nothing is counted twice.

**`TeachesDish` — classified, not silently omitted.** The survey lists it as a free carrier (no
vanilla blueprint uses it). It is a per-*creature* override that sits **above** the faction recipe
in `WaterRitualCookingRecipe`'s resolution order. The faction recipe is the realm's one shipped dish
authority, so using `TeachesDish` as a competing realm-dish authority is `REJECTED`. The reopened
named-cook feature is narrower: one exact city-local resident identity may teach one separately
authored, direct vanilla-component recipe. Its resident/body/recipe receipts, replacement/revocation,
save recovery, and water-rite resolution are active v1 work; display-name substitution and random
procedural recipe generation remain forbidden.

**No passive spoilage.** `KingdomWearRules.LeakKind.Food` keeps its persisted ordinal only to read
old saves. Food is opt-in positive play, so larder damage never destroys ingredients and no world-
day absence accrues a food loss. A valid old open food-loss receipt clears before validation or
continuation without inspecting inventory; obsolete `SpoilFrom` projections are inert. Water and
charge leaks remain live. Repair still restores a larder's reduced work effectiveness.

## Acting on its own judgment

`KingdomBounty` (posted prices: no escrow, completion-paid, deterministic taker draws; manning
binds one exact resident/work and advances only through ordinary reserved-crew serviced time;
realm option/master epochs and monotone resident/work availability epochs reanchor every interval
that cannot be proved continuous),
`KingdomRoads` (worn ground: traffic accrual from stored tick stamps, founder paving),
`KingdomYards` (one yard trade per small house, registry in `KingdomYardWorks.xml` — mergeable),
`KingdomGuestbook` (notable guests with hooks that decay into rumors, and the carry-sign's
distance-scaled hauls, one in flight, mirroring the water manifest's honesty rules).

A yard row with `Goods="Yes"` is the caravan lane, not equilibrium support. One exact eligible
built household paired to one exact standing fixture in its own yard adds one dram to each due
charter cycle, capped at four households per caravan. Missing or ambiguous physical evidence adds
nothing. `KingdomTrade` derives this from the already-maintained activation survey and freezes the
adjusted `IncomePerCycle` and total requested water in its ordinary durable operation before any
physical or domain mutation; release or registry changes cannot reprice an open receipt.

The luxury arrival is a distinct, inspectable path rather than a large-bed alias. A blueprint
tagged `r_TAF_LegendaryTrader` requires one sound, wholly vacant exact `finehouse` root on an M-or-
larger LotId plus a live staffed shop tier of at least 3. Success stores that exact LotId in the
ordinary `KingdomLodgingPlotId` and marks a finite native personal `VillageMerchant`. The required
`GenericInventoryRestocker` is sealed—empty tables, zero chance, effectively disabled frequency—and
exists only as Qud's empty-trade adapter. It does not generate or restock wares. The current
`ShopTier` is service standing/reach, never ware quality. Manors, terraces, aggregate spare beds,
and unstaffed shop numbers do not substitute.

The ordinary civic market has the same physical law. One accepted staffed `taf:market` provider on
designated ground plus its exact held office may open at Village/current standing 3 and may open
empty. Native TradeUI sale is the sole ordinary ingress: the exact sold item enters direct merchant
inventory with native `_stock`; purchase is the sole ordinary sink. TAF never population-rolls,
mints, consigns, replaces, remotely debits, or periodically restocks wares. `ShopTier` may rise or
fall to zero with current service; Chronicle receipts own historical reach. Exact office/legendary
handoff moves only receipted direct stock. Personal inventory is never stock by proximity.

If an item is bought, stolen, dropped, carried, container-held, foreign-held, or left on a corpse,
TAF retires only its receipt and owned protection. It does not reclaim, delete, move, recount, clear
native `_stock`, or alter foreign state. Unloaded ground may retain stale TAF marks until an exact
item event or attended observation. Completed or dormant legendary/native traders remain finite
personal merchants through civic loss and accession, while only the civic projection/marks retire;
an open prepared handoff endpoint alone is temporarily unavailable to succession. Vanilla assumed
trader water and pricing remain TradeUI conventions, never TAF stock, water, or resource output.

Both guest tracks run their arrival clock on real elapsed time (`KingdomRules.PassagesThrough`)
and report a run that came and went unwitnessed as one dated line rather than a queue standing
since spring. For plain travellers: `KingdomLocusRules.PassageWhen` phrases how long ago the last
of a run stood at the gate, `PassagesLedgerNote` / `PassagesChronicleLine` are the homecoming
ledger and chronicle tellings of the whole run, and `GuestLedgerNote` covers the one guest still
standing when the founder returns. For notables: `KingdomGuestRules.WhenPhrase` is the same dating
phrase, `PassedChronicleLine` / `PassedOutsiderRumor` / `PassedLedgerNote` / `PassedGuestbookLine`
tell a departed run across the chronicle, the outsider rumor register, the ledger and the
guestbook in turn, and `DepartedLedgerNote` is the single-notable case — in every one of these, a
notable's hook is never lost, only relocated into rumor.

## Reach, the chain, crews, and wear

`KingdomReach` / `KingdomReachRules`: reach derives from plot size × chain position
(plot/quarter/zone/city/realm, `Reach` attribute overriding); lifts shade residents in reach —
on one cell through `KingdomReach.CharacterAt` / `ShadedAt`, and on the settlement's own level
through `KingdomReachRules.Landed(amount, reached, homes)`, which lands a work's lift in
proportion to the roofs it covers and lands nothing at all for a work that reaches no home;
quarters are measured (ground within six cells of ground); an XL's city effect is live only while
the office machinery has named a head.

An attended zone stores city/realm Reach memory as one canonical
`r_TAF_ReachObservation_v1` zone-property receipt. It binds purpose, realm, settlement, zone,
faction owner, designation authority digest, fixed lift payload, source revision, and observation
tick. Every consumer re-proves the current identity/topology and raw string receipt; malformed,
future, foreign, disabled, or pre-receipt legacy state contributes zero. Receipt age is otherwise
unbounded until that exact zone is attended again. Disabling the option, secession, and exile
explicitly remove affected receipts and zero the retired `r_TAF_ReachCity_*` /
`r_TAF_ReachRealm_*` integers. Old saves are deliberately not promoted from those unbound values.

`KingdomMaterials` gains the refined tier (shaped timber /
shaped stone / worked metal via staffed yards), vanilla bits (`Bits=`) and exotic finds
(`Exotics=`) as high-craft prices, and the yard gates on L/XL construction. `KingdomCrews` /
`KingdomCrewRules`: capability from settler stats (`CrewNeeds="strength:16"`) or exact vanilla
skill presence (`skill.tinkering`, `skill.harvestry`, `skill.customs`, `skill.physic`,
`skill.wayfaring`, each at threshold `1`), ablest-first deterministic assignment, shortfalls slow
and named. Only the first positive demand selects the capability lane; `CrewNeeds` affects pace,
not building eligibility or an improvement trigger (`fieldrows` uses `skill.harvestry:1`).
`KingdomWear` / `KingdomWearRules`:
damage from raids, hard running and temperamental tech — never from the calendar — bounded,
mending auto-queued and holdable, costed from the chain. Hard running is counted in
**activity-days** (`KingdomRules.ActivityDays`), so a work that ran hard through an absence wore
for it and a work standing idle did not. `WorkEffectiveness` (Addendum 10(b)) is what ANY
finished work is worth this pass, crewed or not — a work that wants a crew runs at its crew
stretch reduced again by condition, a staffless one at its condition alone. The one exception to
"never from the calendar" is limited to already-damaged water and charge stores: their loss runs
on **world days** (`Leaked` / `LeakKind` / `LeakDaysToEmptyAtCeiling`) until mended. The frozen Food
kind always prices zero and old Food receipts retire inert.

### Construction presence and visual-state readings

`KingdomConstructionPresenceRules.Plan` is the pure oldest-only allocation rule. It receives frozen
`KingdomRaisingCandidate` values and returns one `KingdomRaisingPlan`: selected input index plus
bounded hands. Runtime `KingdomConstructionPresence` draws those hands from real unposted settlers
after water/running work, through the same capability and API-v2 identity-affinity allocator. Plot
and scaffold clocks read only their own stamped effectiveness. Queued and unstaffed intervals are
consumed, never banked. Named construction/visual properties are private receipts; extensions must
not author them.

`KingdomVisualStateRules.Resolve(KingdomVisualFacts)` is the engine-free priority resolver;
`Cue`, `GalleryReceipt`, and `GalleryHash` expose the color-independent legend and its versioned
SHA-256. Runtime facts come from exact construction assignment, strike/repair effort, wear, city
heart deprivation, power brownout, and staffing. `r_KingdomVisualState` only alternates a vanilla
render indicator and appends examine text; it creates no object and owns no saved cosmetic state.
`kingdom:visuallegend` prints the canonical receipt. `kingdom:visualaudit` reports actual current
ground rows in deterministic ground order for screenshot/human acceptance.

## How belief and affiliation move

`KingdomConversion` / `KingdomConversionRules` applies only where the merged creed definition is
theological: doctrine, cult, or an order with explicit opt-in. It provides osmosis (shared living under one roof, scaled by
closeness, accrued in **cohabitation-days** of real shared living rather than in visits), culture
(shared meals, capped), and the resented-pressure exit (warned once and pushed to the founder,
its window spent in world-days, emigrating through the ordinary machinery). A conversion about to
happen stops at a **brink**, the founder is told wherever they are, and eighteen days later it
happens whether or not they came back; see below. **`KingdomConversion.Convert` is the
one path a conversion may take** — it alone keeps the creed tallies, pressure entries, and the
two-register dispute honest. `KingdomFaith` (consecration; staffed shrines converting the neutral
of their zone; staffed scriptoria softening the grudge one band) and `KingdomWaterRite` (the rite
turned inward: consented, priced, refusal-with-reasons, the fourth asking shutting the question)
both route theological changes through it. `AdoptAffiliation` is the explicit, consented water-rite
lane for communities, peoples, polities, non-theological orders, and unknown third-party keys; it
shares tally/history custody but emits adoption/allegiance prose and is unreachable from passive
shrines, meals, osmosis, or pressure sources.

## The quality-of-life vocabulary, and lodging

`KingdomQolRules` / `KingdomQol`: one namespaced tag vocabulary. A building's merged catalogue
`Provides` attribute is only its accepted-tag ceiling; it never supplies a live tag. Current
furniture, equipment, liquid/state providers, and exact structural cover inside one proved
designation supply the effective tags. `KingdomQol.CatalogueOfferOf(key, ...)` is explicitly a
catalogue-preview/authoring query, while `KingdomQol.TryPhysicalOfferOf(root, survey/index, ...)`
is the live API and never mints identity. Structural sky/darkness follows exact designated cover
and stratum, not a promised roof string. Residents carry
Needs / Prefers / Refuses, derived first from vanilla parts (Robot, aquatic brains, LiveFungus,
PhotosyntheticSkin, Inorganic) and refined by `r_TAF_*` blueprint tags, with `-tag` removing a
derived entry. Unknown tags are inert. `KingdomLodging` / `KingdomLodgingRules` assign every
settler an address: Needs gate the home; housemates are gated by the closeness ladder — Packed shares only without
quarrel, Close refuses the ambient grudge, Roomed tolerates it, and open hostility (≥100, the
named fault lines) refuses any shared roof at every tier. `Refuses` tags are absolute. Closeness
derives from effective physical beds per exact designation extent; authored TAF architecture may
retain its explicit `Closeness` override, while adopted/external rooms cannot borrow one. Arrivals join only if a
home they would accept exists. Among those eligible homes, an ordinary/non-luxury resident takes
any non-`finehouse` before a fine house; only then does the ordinary fewest-free-beds/ordinal-plot
tiebreak run. This is a last-resort preference, not a hard reservation: when a fine house is the
only acceptable shelter it is used, existing assignments are never displaced, and projected
arrival occupancy calls the same chooser as the real settlement pass. The public
`KingdomLodgingRules.ChooseOrdinaryIndex` surface accepts the candidates plus parallel fine-house
flags; null or mismatched advisory flags fall back to `ChooseIndex`, so bad advisory input cannot
manufacture homelessness. `LodgingCandidate` retains its three-field ABI. A settler whose
acceptable housing is lost does not start a
countdown: they are recorded at a **roof brink** the moment they have nowhere, word is pushed to
the founder naming what would keep them, and they leave only once the brink's six **world-day**
window is spent — attended or not, dated to the day it ran out. An absence of any length still
arrives at the same brink, nobody is ever taken unwarned, and re-housing them at any point lifts
the brink and unsays it. A home stops counting as a roof at
all once wear crosses `KingdomLodgingRules.CondemnedWearPercent` (40 — derived from
`KingdomRules.RuinStandingCeilingPercent`, not chosen), judged by `KingdomLodgingRules.IsCondemned`
/ `KingdomLodging.IsCondemned`; the building itself is never touched, only stops housing anyone
until mended. `KingdomLodging.ResidentsOf` reads who a condemned home held, and
`RecordCondemnedRoofBrink` backdates their roof brink to the tick the condemnation actually
happened — a subsidence breakpoint days back, not the pass that notices — so the announcement
quotes the honest elapsed.
The former flat `KingdomQolRules.CohabitHostility` / `JudgeCohabitation` path is retired. Its
metadata remains only as an `[Obsolete(..., true)]` binary adapter at the pre-release boundary;
new source cannot compile against it. `KingdomLodgingRules.RefusalHostility` / `Conflicts`, with an
explicit closeness rung, are the supported contract.
Tastes and displacement tolerance query this same vocabulary.

The Charter's **The city in full → Inspect physical building benefits** browser is the player-facing
view of that same live index. It takes one non-migrating active-zone survey, lists every exact
authored, adopted, or extension-provided designation, and then lists the physical provider rows
inside it. The building view distinguishes active supply from catalogue caps and states explicitly
that an empty designation provides zero; it also names each still-unfilled amount and missing
accepted quality. Provider views expose nominal offer, current operating percentage, credited
amount/qualities, wrong-role supply, true cap saturation, and exact fault. Physical cover has its
own provider row rather than silently adding sky/dark. Unassigned providers and
designation-source faults remain visible in a separate fault list. The browser never loads remote
ground, mints an object identity, changes a designation, or commits governance work.

`KingdomBenefitInspection.Offered` / `Tags` are nominal declarations;
`Credited` / `CreditedTags` are the portion counted in this immutable reading.
`OperationPercent` records current operation. `OutsideDesignationContract` identifies a live offer
that does not fit this building role; `SaturatedByDesignation` identifies accepted supply whose
amount ceiling or singleton quality is already full. `LimitedByDesignation` remains their combined
compatibility projection. A provider that bound to a designation but then failed access, cover, or
operation stays attached to that building's inspection rows; only genuinely unassigned/source
faults appear globally.

The active-zone scan admits at most 4,096 provider evidence rows deterministically. Exact assigned
identities are considered before anonymous furniture; equal anonymous object anchors stay atomic,
so mutable part order cannot choose a winner. A tied group that cannot fit is quarantined while
later stable groups are still considered, and canonical native declarations may fill a remaining
prefix. Refused excess produces one visible `ObservationLimit` row. Crossing the bound therefore
never erases already proved providers or lets one oversized anonymous tie disable every building.

### Physical provider and adoption operation contract

`IKingdomBenefitProvider` declares bounded nominal supply. Optional
`IKingdomQuantitativeBenefitProvider.TryKingdomBenefitOperationPercent` returns current operation
from 0 through 100; the original boolean callback remains the compatibility fallback. Provider
condition, designation-root condition, staffing/power/sowing/custom operation, scope, access, and
caps are independent gates. Percent gates compose multiplicatively. Every description and custom
operation callback is contractually deterministic and observation-only for one benefit epoch: it
must not mutate its item, root, survey, zone, another provider, a designation source, or hidden
state. Descriptions may be called more than once and must normalize identically. The runtime
re-proves declarations, exact designation authority, roots, custody, assignment, routes, shells,
condition, and non-custom operation after callbacks and refuses detectable mutation.
A custom callback's arbitrary hidden state cannot be re-proved by a finite callback-free pass and
therefore is not supported authority; violating this contract is extension corruption, not a
credited state.
`Filled` is rejected because no typed relevant-contents contract ships yet.

Generated semantic fixtures carry optional `r_KingdomProviderBuildKey`. When present it must equal
the exact designation's `BuildingKey`; a forge fixture cannot satisfy a smithy merely because both
offer `craft`. An absent tag means a generic native capability such as `Bed`, `Shrine`,
`MarkovBookshelf`, `UniversalCharger`, or real fresh liquid, still bounded by scope and caps.

`KingdomAdoptabilityRules` is the shared load/menu/transaction proof boundary. Shipped adoption
currently exposes 51 curated roles: enclosed ordinary housing and work rooms, ordinary open
yards/grounds with exact catalogue-sized rectangles, plus `larder` on one exact dry container.
Every exposed benefit role has a stocked takeable provider route. A spatial target publishes an
exact signed cell receipt; staffed non-storage work additionally publishes a signed build key,
category, staff need, and manning rule. Neither receipt copies catalogue `Carries` or `Provides`.
Furniture supplies benefits; the catalogue only caps them. Housing and staffless targets publish
no staffing authority. Other storage works retain their typed authored production/capacity roots
rather than accepting an arbitrary vessel.

Room adoption has one live geometry authority. Its structural flood records exact membership,
shell, and safely openable door ingress separately; the signed d2 receipt stores canonical
membership plus whether the target is an open plot. Residents, creatures, dropped items, and
ordinary/provider furniture never change that membership. A second ingress-seeded flood derives current usable floor from native
passability: permanent solids, pits and unsafe navigation objects, and open liquid do not count.
The room must retain 4/12/24/40 reachable usable cells for S/M/L/XL housing or work at admission,
pre-commit, and every later benefit read. Falling below that role/tier minimum, losing safe
ingress, or changing membership pauses the designation without rewriting its receipt; restoring
the same ground resumes it. Collision tests walk only exact membership cells, never a room's
bounding rectangle, so concave gaps remain available to another lawful designation.

Open ordinary yards and grounds are supported; their full rectangle is Plot/Yard authority and
has no invented shell. Single-cell/network shapes, crop fields, power plants, mills, labs, fixed
creed installations, remote endpoints, civic Heart works, purpose/crown works, and hosted arcology
roles remain authored where their meaningful operation still belongs to root parts or exact
topology. These are current proof boundaries, not permanent category bans. Extensions may expose
a new ordinary room or open role with `Adoptable="yes"`; load still rejects unsafe geometry and
reserved authored roles. Release preflights designation authority, then clears signed designation,
operation, plot, staffing, and owned larder state without erasing pre-existing dedication.

## Layering, reserved lots, plans, and the trigger law

Catalogue files **layer** (`KingdomMergeRules`): merge-by-key on raw attributes inside the single
XML pass — named overrides, omitted survives, blank erases, skins append (same key replaces),
chains extend across files; the post-merge design is what the validator sees. `Plot` declares lot
eligibility, catalogue `Footprint="WxH"` declares exact physical building dimensions, and `Roof`
declares the shelter authority later frozen as `BaseRoof`; none generates geometry. Exact
geometry and the footprint's canonical origin come from the selected architecture map and palette.
`KingdomSocket` keeps a struck lot
as a rebuildable reservation. Three lanes remain distinct. An `UpgradesTo` tier ordinarily
resolves inside the standing receipt's same exact plan/binding/type/actual-size route. The two
bounded cross-size lanes are an adjacent civic-heart rung and an adjacent ordinary authored
`additive-expand`/`renovate-expand` lineage whose larger containing envelope passes live ground,
road, occupancy, ingress, and custody proofs. Both resolve the successor's exact frozen
`VariantKey` rather than current demographic selectors and preserve `LotId`; a missing matching
successor variant refuses before debit. A founder-selected plan
change exists only when `KingdomArchitectureTransitions` declares the exact directional
`(from, to, type, actual size)` delta. Same-set transitions preserve `LotId` only through an explicit
transition receipt. Retype or any resize without that exact adjacent expansion authority performs
fresh siting/restaking and mints a new lot identity. Skins change presentation metadata, not
topology.
The real automatic trigger gates are stage, style, holds, exact `Knowledge`/`MinTech`, free hands,
contents fit, water price plus reserve, `UpgradeMaterials`, one-work-at-a-time pacing, and frozen-lot
room. The predecessor stays live throughout construction; no temporary lodging, output outage,
held offer, or force-through-harm path exists. No trigger reads elapsed time.

## Reserved lots, occupied plans, materials, and gates

A plot is a **reserved lot**, not a building. `KingdomPlots` / `KingdomPlotRules` site and protect
S/M/L/XL rectangles. An occupied current-path lot freezes two related identities:

| Layer | Frozen authority |
|---|---|
| Lot | `LotId`, type/category, actual size, rectangle, exact binding, and cardinal pose selected by that binding's semantic frontage. |
| Occupied plan | Build key, plan, exact binding, tier, variant, palette, canonical snapshot/hash, physical footprint, `BaseRoof`, main cell, authored claimed cells, entrances, fixtures, and stateful anchors. |

Every architecture map is the exact canonical lot-sized map in north-facing coordinates. If its
catalogue tier declares `Footprint="WxH"`, the selected map must declare
`Footprint="X,Y,WxH"` with the same dimensions wholly inside the map. Without a catalogue
footprint, the resolved physical footprint is the full map; an optional map attribute can only
restate that full rectangle. Pose rotates the map, placements, and all four footprint corners as
one authority; east/west transpose the world extents. Authors provide one canonical map per real
architectural variant, not four directional copies. Heart- or road-frontage resolution freezes the
cardinal pose before payment, and the exact preview names both that semantic frontage and facing.
The semantic rule is not duplicated as a second mutable receipt field: the exact binding is frozen,
and its resolved cardinal pose is the durable spatial authority. Author separate directional
maps only when direction changes the architecture rather than its pose.

Fixture pose is an optional schema-1 registry extension, not a requirement on ordinary Qud
scenery. A palette blueprint with no `<pose>` record is invariant, matching vanilla single-cell
screen-space furniture. A cardinal record names one semantic base plus existing north/east/south/
west concrete siblings; a map glyph then supplies the local orientation on the matching Ground,
Structure, or Object layer. Compilation composes local orientation with lot facing and writes only
the concrete blueprint into the existing `a4` placement. Cardinal use without local orientation,
local orientation without cardinal authority, unknown siblings, and siblings outside the semantic
base inheritance family fail closed. Cardinal siblings also pass a bounded effective-blueprint
parity audit: only tile/glyph/color/H/V-flip Render fields may differ; parts, all nonvisual Render
truth, builders, mutations, skills, stats, properties, tags, extended tags, and inventory remain
identical. Malformed final pose rows poison a selected palette reference instead of degrading to
undeclared invariant behavior. Omission inherits merged optional fields; an exact empty sibling or
layer-orientation attribute clears it, while whitespace remains malformed. Exact-name basin and
stair fixtures cannot declare cardinal families because their blueprint is runtime identity. More
generally, every `r_Kingdom*` semantic base is prohibited unless explicitly admitted by the
source-reviewed visual-only identity allowlist (empty in the shipped corpus); vanilla stairs remain
prohibited. This protects exact-name gameplay consumers that are outside the blueprint parity
surface. Extension authors own the same exact-name audit for their non-TAF semantic bases.
`connected` and explicit `invariant` records reject local orientation and preserve the base
blueprint. The public compiler accepts no raw pose list; only the loader's bounded audited registry
can authorize concrete siblings. See
[MODDING.md](../MODDING.md#plots-reserved-lots-and-authored-buildings) for XML.

Cell claim and physical-footprint membership are separate axes. `Building`, `Yard`, and
`Unclaimed` record managed use/custody, while the rectangle records physical building ground. A
`Building` cell, `$building`, and `main` must be inside the rectangle. `Yard` and `Unclaimed` may
occur inside or outside it, including covered courts and service areas. The geometric yard remains
the lot minus the physical footprint.

Current compilation emits canonical `a4`, which freezes that rectangle, the three-state claims,
and catalogue `BaseRoof`. The decoder retains canonical `a1`-`a3` compatibility, but those wire
formats cannot provide all current claim/footprint/roof truth. They stay read-only architecture
authority except for named already-paid completion and legacy lanes: no new stamp or in-place
transition may infer or silently upgrade their missing state.

Preview freezes this authority before debit; commit re-proves the same snapshot rather than drawing
a variant twice. The stamper applies ordered ground, structure, and object layers. No current
commission stretches a smaller map, builds a generic rectangle, guesses a door, or scatters
furnishings. Missing exact bindings are filtered from the picker and refuse direct calls before
mutation. Automatic same-binding tiers preserve the standing variant identity; explicit
same-type/same-size transition receipts may select the target's current lawful variant only when
every declared target variant retains the source's stateful fabric. Both lanes
preserve `LotId`; retype, shrink, relocation, or resize without an exact adjacent expansion is a
fresh lot. Already-standing legacy work remains on its frozen compatibility path and is never
silently converted.

For a stateful non-root Object, the compiler freezes the sole `benefit:*` anchor as custody when
one is present; functional roles may coexist on that glyph. Without a benefit anchor, exactly one
non-main, non-entrance functional anchor supplies custody. A stateless provider is still physical
and inspectable, but an authored renovation may replace that empty fitting; protected contents or
foreign/live state still refuse removal in preflight.

All in-place additive and renovate deltas obey the same monotonic footprint rule in a common
main-relative frame: the successor rectangle must retain or contain the predecessor rectangle and
may not shrink or shift it. Only `additive-expand` and `renovate-expand` may also enlarge the lot
envelope. Shrink, recentering, or relocation requires strike and fresh siting/restake;
`replacement` refuses as an in-place delta and requires a fresh commission after strike.

Materials (`KingdomMaterials` / `KingdomMaterialRules`) come from clearance—never minted—and live
in dedicated stockpiles; building costs are water plus materials, and condemning returns half.
Architecture palettes must agree with paid material and technology: map structure/fixture slots
cannot smuggle in metal, power, filled vessels, free contents, or later craft.

Commissioning is gated by `KingdomZoning` (district, territory, known designs, derived craft
level, and the creed stack — who is standing here, which creed the design belongs to, and how much
of the city holds it) with every refusal naming its fix, except the one refusal with no fix: a
creed-work whose creed nobody here has ever held is **not shown at all** (`KingdomZoning.Offered`
/ `Visible`), which is Addendum 14's visibility law; designs improve through `KingdomUpgrade` chains
that carry every civic mark. `KingdomCatalogueRules` validates catalogue XML; the architecture
registry validates palettes, maps, plans, exact bindings, tiers, required roles, and variants.
Third-party XML can author the whole current path; see the complete example in
[MODDING.md](../MODDING.md#complete-minimal-authored-plot-extension).

## `KingdomZoningRules` — the seven gates, the tag idiom, the stratum, and the claim

Pure and engine-free (`KingdomZoning`, same folder, is the engine-coupled half: reading a real
zone's district, the founder's data disks, the certified machines, and the settlement's own
roster of peoples). Checks run in `ZoningVerdict` order, most fundamental lack first, district
last, and stop at the first refusal — the founder is told one thing to fix, not four.

| Member | Contract |
|---|---|
| `enum ZoningVerdict` | `Permitted`, `RefusedUnlearned`, `RefusedTechLevel`, `RefusedTerritory`, `RefusedStratum`, `RefusedDistrict`, and — appended, never renumbered — `RefusedUnaligned`, `RefusedCreedShare`, `RefusedBuilders`. The three creed verdicts are numbered **last** and checked **first**; `Judge` is the authority on the order, not the ordinals. |
| `readonly struct ZoneGate` | The OPTIONAL gates parsed off one `<building>` entry: `Districts`, `MinZones`, `Knowledge`, `MinTech`, plus the creed stack `Builders`, `Creed`, `CreedShare`, plus `Strata` (Addendum 15 — home stratum first, share-tags after, absent admits everywhere). `ZoneGate.Open` gates nothing, which is what an entry written before any of them existed parses to. `CreedShare` is `ZoneGate.ShareUnsaid` when unwritten; `EffectiveCreedShare` reads that as `KingdomCreedRules.DominantSharePercent`. All published constructors are kept — the older two chain into the strata one with `null`. |
| `readonly struct BuilderRoll` | Who lives in a city, as a gate must see them: `People` and three tallies — countries walked in from, creeds held now, creeds **held and left**. Lookups are case-insensitive. `BuilderRoll.Unknown` is the roll a caller could not supply and **permits every creed gate**. |
| `readonly struct ZoningJudgement` | `Verdict` plus `Detail`/`Note` — what's missing, in the settlement's own words, and the menu's short tag. `Allowed` for a design with nothing to prove. |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster)` | The four-gate verdict, stratum untested (equivalent to `Underground: false, RequiresSky: false`). |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster, bool underground, bool requiresSky)` | The same, with the stratum folded in. A design whose plot spec declares `Sky` is refused **`RefusedStratum`** (`Note: "wants open sky"`) on ground below `KingdomRules.SurfaceZLevel` — checked before the district, so the menu itself carries the tag at the moment the founder is choosing, rather than only once they've picked the design and `KingdomPlotRules.RefuseSky` turns them away at the plot. The other half of the stratum question — which SET a design belongs to — is the `Strata` gate below, and the nine-argument overload folds both in. |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster, bool underground, bool requiresSky, BuilderRoll roll, string groundStratum)` | The full verdict, stratum set included: after weather (asked first, because it is the half nothing can answer) the ground's stratum is asked against the gate's `Strata` list, and a design that does not stand here is refused **`RefusedStratum`** with `Detail` naming every stratum that *would* take it and `Note` naming where it lives. The eight-argument overload delegates with `StratumOfGround(underground)`, so every shipped caller gets the gate unchanged. |
| `static string HomeStratum(string strata)` / `static string StrataShared(string strata)` / `static bool StrataAdmits(string strata, string stratum)` / `static string StratumOfGround(bool underground)` / `static string StratumName(string stratum)` / `static string DescribeStrata(string strata)` | The strata vocabulary (Addendum 15): first welcomed token is home, the rest are share-tags, `all`/`!` spellings shared with `TagAccepts`, and the set is open — a third-party token names itself. `StrataAdmits` answers for `sky` by falling back to the *surface* answer unless the list mentions sky, which is the whole of "sky is a filtered subset of the surface": the sky set is never enumerated, it is the surface set minus what filters itself out with `!sky`. Constants `StratumSurface`/`StratumDeep`/`StratumSky`/`StratumArcology` name the shipped tokens. The built-in arcology lots declare only `arcology`; they are commissioned through their exact host and never enter surface offers. |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster, bool underground, bool requiresSky, BuilderRoll roll)` | The same again with the city's own people folded in. The three creed gates are checked **before** all five older ones, in Addendum 16's order — alignment, then amount, then hands — because alignment is the only gate a founder cannot answer by walking somewhere or carrying something home. Against `BuilderRoll.Unknown` this answers exactly as the overload above. |
| `static bool TagAccepts(string tags, string value)` | **The one tag idiom**, and what `KingdomRules.StyleAllows` now is. An empty list accepts everything; a matching `!`-negation refuses whatever else the list says; otherwise accept on `all`, on the value, or on a list of nothing but refusals ("everywhere except"). Case-folded both sides. `NegationPrefix` is `'!'`, matching vanilla's own `RecipeGenotype="!True Kin"`. |
| `static string DescribeTags(string tags)` | The list read back as prose, or null when it gates nothing. |
| `static bool Aligned(BuilderRoll, string creed)` / `static bool NoPathToCreed(BuilderRoll, string creed)` | Whether anybody here holds the creed **or has ever held it**; and its exact complement, which is the visibility law (Addendum 14) — a design whose creed no one has ever held is not shown in any menu. One rule, two readings, so "shown" and "buildable" cannot drift apart. |
| `static bool CreedShareMet(int holding, int people, int percent)` / `static int ShareHeld(int holding, int people)` | `KingdomCreedRules.DominantCreed`'s arithmetic minus the no-larger-rival clause: at least `MinBelievers`, and at least the asked share. A congregation big enough to raise its own work need not be the largest in town. `percent <= 0` asks for nothing, believers floor included. |
| `static bool HasBuilders(BuilderRoll, string requirement)` / `MissingBuilders` / `DescribeBuilder` / `DescribeBuilders` | One `Builders` token — `kind:name`, `kind:name:count`, or a bare name matching any kind. Kinds `KindOrigin`, `KindCreed`, `KindKept`. An unknown kind never matches and is named in the refusal. |
| `static bool StratumAccepts(bool underground, bool requiresSky)` | The one depth rule the catalogue can state today: `!(underground && requiresSky)` — weather does not reach under the rock. |
| `static string StratumName(bool underground)` | `"under the rock"` / `"open sky"`, for the sentence that names it. |
| `static int ZonesForStage(GrowthStage)` | How many zones a city of this stage may hold at most: Camp/Steading 1, Village 2, Town 3, City 4. Read off the catalogue's own `MinZones` pairs, not chosen separately — the two-zone designs are `MinStage="Village"`, the three-zone `Town`, the four-zone `City` — so a settlement reaches the ground a design wants at the same moment it reaches the stage that design wants. |

## Hosted-lot registration and read-only views

`KingdomHostedArcologyRules` owns a process-bounded, copy-on-read registry (maximum 16 definitions).
Paid v1 floors are a closed two-entry manifest: ward and terrace each require a fixed topology slot,
programme, and exact fixture plan, so no public API pretends a definition alone can create one.
`RegisterReadOnlyHostedLot(KingdomHostedLotDefinition, out string)` accepts only a read-only
definition with no material, duration, crew, support, water, or physical-producer mutation fields
and one `KnowledgeView` key. Duplicate/malformed keys refuse; paid input explicitly refuses.

`KingdomHostedArcology.RegisterKnowledgeView(string,
KingdomHostedReadOnlyEligibility, KingdomHostedKnowledgeView, out string)` binds a read-only key to:

- eligibility: `bool (KingdomSystem, Zone loadedHostZone, GameObject exactHostRoot,
  out string refusal)`; and
- rendering: `string (KingdomSystem)`.

The callback is invoked only from the loaded exact shell. It receives no research queue, budget,
timer, or mutation API; false/exception refuses safely. A provider may take one bounded survey of
the supplied zone but must never load another zone. Realm, root, and loaded-zone identity are
snapshotted and reproved after both callbacks, so a view whose context changes is discarded. The
built-in Great Archive uses this seam to
require the active crowned arcology plus local keeper's shelf and vellum press, then renders only
the realm knowledge DAG.

`StableChildId(rootId, role)` returns the deterministic `taf:arcology:v1:` SHA-256 identity for
host-owned fixtures. `AdvanceLabor` applies only prior-pass staffing and caps one loaded-ground
catch-up at 36,000 ticks. `CanReserveAt`/`TryReserve`/`BindAuthority` enforce one exact active or
reserved hosted-shell carrier for the realm and capital; ambiguous evidence is quarantined, never
adopted or destroyed. `AuthoritySlotForWrite(first, second, currentRealm, retainedRealm)` is the
engine-free selection law for two fixed save slots: it updates the current row, preserves the one
exact exiled archive row, and only replaces authority outside both retained identities.

`KingdomHostedArcologyTopology` is the engine-free physical authority for schema `TAFArcology`:
`AllCoordinates()` enumerates its 27 local zones; `ProgrammeAt`/`ProgrammeName` select one distinct
district programme per coordinate; `TryHorizontalNeighbour`, `HasStairsUp`, and `HasStairsDown`
define its bounded connected graph; `HostedLotAt` fixes terrace `(1,1,9)` and ward `(0,1,11)`; and
`StableRole` namespaces every threshold, stair, anchor, programme fixture, and paid fixture by the
surface root plus coordinate and semantic role.

`KingdomHostedArcologyProgrammeBuilder` is the internal physical plan authority. `FloorFor` and
`MaterialHistoryFor` select the three storey histories; `Build` selects one of nine route-safe
archetypes, stamps the inspectable plan signature, places four inert programme cues and six real
vanilla arc sconces, and refuses a coordinate/programme mismatch. `TryPaidFixtures(lotKey,
programme, ...)` exposes only the matching Hydroponic Terrace and Lodging Ward exact plans to
`KingdomHostedArcologyVisual`, keeping paid receipt realization and room architecture on one
auditable placement authority. Visual realization reproves physical-producer count, every stable
identity and destination, all obstructions, and every constructible output before its first zone
mutation. A failed placement rolls back only newly prepared fixtures and quarantines if exact
rollback cannot be proved.

`KingdomHostedArcologyDesignationProvider` exposes only an active, already-loaded paid floor. Its
exact stable zone anchor is the designation root; the receipt digest is the revision; and the one
80x25 programme zone is the bounded interior/covered cell set. It cross-proves exterior root ID,
native interior instance/zone ID, topology slot, programme property/name, active canonical lot
receipt, anchor, and realized fixture manifest. It never opens an interior or mints an ID on read.

At final `SuspendingEvent`, an anchor marked by an attended activation is consumed once. The
still-loaded event zone is visually reconciled and evaluated through an isolated exact designation
and `KingdomBenefitIndex`. Ward `roof` and `luxury` come only from current working providers and are
capped by the lot contract. Terrace food does not enter generic benefit projection: exact stable-ID
growbeds contribute current `r_TAF_HostedCropRows`, row/yield/day arithmetic is capped at the lot
contract, and exterior consumption still requires current fresh water in ordinary stores.

`r_KingdomArcology.LotObservations` persists at most one canonical copy-on-read row per paid lot:
exterior root ID, lot key, active receipt revision, canonical interior zone ID, exact anchor ID,
observed tick, physical amounts, and optional bounded fault. Copying a carrier clears the slate;
reload validates bounds and canonical rows. Duplicate, malformed, future-dated, over-cap,
wrong-lane, foreign-root, receipt-changed, zone-changed, or anchor-changed evidence fails closed
without loading remote ground. Shell inspection reports active amount/cap/missing state,
unobserved/fault state, and observation age. Catalogue `Supports` remains only the ceiling and work
contract; it is never projected as live hosted supply.

### Where knowledge lives, and the research work (`KingdomResearch` / `KingdomResearchRules`)

The rolls moved (Addendum 22 B-cluster): `KingdomZoning.Roster(System)` reads the **seat city's**
container, `KingdomZoning.RosterOf(KingdomSettlement)` reads any other city's, and the old
game-state key `r_TAF_KeepersRoster` is retired — read once by a named migration shim into the
seat, then blanked, never written again. `Learn` writes to the seat, so certification and teaching
are per-city; a seceding city walks out with its rolls and a returning one brings them back whole.
`Tech(System)` is likewise per-city, and a design's `MinTech` is judged against the city it is
being built in.

`KingdomResearchRules` (engine-free) carries the node record (`ResearchNode`: Key, Grants,
Requires, TaughtBy, SeededBy, Tier, Effort), the tier ladder (Int 10/14/18/22, hard at the
boundary), the accrual arithmetic (crew × condition × tier bonus × bench rung, any factor zero ⇒
zero), the seed constants (25% capped 50%), the shelf (8, least-advanced dropped deterministically
and said so), the citizen Int ceiling (`MaxHeadroomIntelligence = 1` — schooling may raise the cap
by one and nothing stacks it), and every player-facing sentence. Vanilla's
`WaterRitualStartEvent.Initial` is the rite source: first sharing water with a faction durably
records its `rite:<faction>` key in the founder-held ledger, including before founding or between
realms. The effective seated roster projects that ledger without writing it into the city's rolls;
matching `SeededBy` entries receive per-city receipts and recoverable 25% floors, capped at 50%.
Comma-separated sources can stack when they resolve to distinct concrete keys; `|` remains the
declared alternative-arm grammar. One rite may seed only one branch head, and no rite completes a
node. `KingdomResearch` is the registry and the ledger:
`Advance(system, tick, labStamp, crew, wear, rung, name)` is the whole bench
contract (`r_KingdomInquiry` rides it at rung 100; the lab wave's benches raise the rung),
discovery is `JournalObservation.Revealed` under `taf:node:<key>` (founder-held), and
`KnowledgeGateHeardOf` is the single visibility filter every menu, map row, and refusal funnels
through. `Train` writes `BaseValue` only — never `Statistic.Max`.

Grooming's maximum study proof is physical and city-local. `schooling` remains the city's tech
gate; the resident's `JobWorkId` must also resolve to exactly one current work row. On active
ground that exact work must resolve to one root at its recorded zone, anchor, and blueprint and
its exact designation must currently credit live `taf:education`. Unloaded ground contributes
only through the canonical dated `r_TAF_EducationPostObservation_v1` zone receipt, which binds
realm, settlement, zone, faction owner, work/root/designation identity and revision, anchor,
blueprint, source revision, and observation tick. Attended passes revoke before observing and
publish last; malformed, duplicate, ambiguous, foreign, future, or authority-mismatched bindings
contribute zero. Receipt age alone does not expire an otherwise exact observation.
Catalogue category and declared `Provides` values are never education evidence.

### The lab (`KingdomProcedureRules` / `KingdomLabRules` / `KingdomProcedures` / `KingdomLab`)

`KingdomProcedureRules` (engine-free) carries the procedure record and its whole law: schema
parse and registry validation, the anatomy-slot judgment (`Slots` against the player's own
`BodyPart.Type`s, `SlotCategories` against `BodyPartCategory` per-procedure), the
`Attach="body|weapon"` semantics (a weapon-event-only class must graft onto a natural weapon or
is refused at commit, by name), the preservation arithmetic (`Number × Count`, corrected
against `Campfire.PerformPreserve`), the mutation-source cap (levels 1–3, never the source's),
the once-ever latch for the named procedures, the ONE sanctioned kernel draw (the Chimeric
Confession's confessed gamble), and every refusal sentence. `KingdomLabRules` carries the rung
ladder, the megastructure cardinality (`JudgePurpose` — one purposeful megastructure per city,
re-keying the same design allowed), and the creed-friction prose. `KingdomProcedures` is the
registry, the discovery ledger, the anatomy census, and the three write paths
(stamp-at-butcher / rebuild-at-graft for parts; `AddPartAt` for limbs; `AddMutation` capped for
glands). `KingdomLab` is the four building parts and the two-level `Popup.PickOption` slate —
the golem's own screen idiom, no new screen class.

### The annexe (`KingdomAnnexeRules` / `KingdomAnnexe`)

The chrome half. `KingdomAnnexeRules` (engine-free) carries the enrolment verdicts
(`KingdomEnrolVerdict`), the rolls as `enrolled:<GeneID>` keys on the settlement container
(no new serialized field — the container's own carry is the fiction's teeth),
`AnswersTrueKin(Seeded, Held)` — raises and never lowers, so a lapsed roll can never un-Kin
the born — the price and disclosure prose (the whole reach of the answer named at the door,
including what lies past the nook), and the chrome-debt petition strings. `r_KingdomEnrolled`
answers vanilla's `IsTrueKinEvent` with a per-tick-cached live read across every city the
realm still holds; losing the rolls closes a door and never reaches into a body. The annexe
grants the True Kin genotype's own two cybernetics license points — the door would otherwise
open onto an empty room. Enrolment snapshots exact vessel receipt, roster, body record, licenses,
and both standing/carry roots; any core exception attempts every compensation axis, verifies exact
readback, and quarantines the realm if restoration cannot be proved. Standing cost publishes as one
copy-on-write batch. `GatheredYield`/`HarvestYield` gained additive `MethodPercent`
overloads, and `KingdomCityAdvanceable` a fourth constructor argument, for the method lane.

### The crown, the satellites, and the delve (`KingdomCrownRules` / `KingdomSatelliteRules` / `KingdomDelveRules`)

`KingdomCrownRules` (engine-free) carries the crown record format, `Resolve` — the capital
derivation (one game-state string, validated on every read against the city books; the halls
win and the record is repaired out loud when they disagree; ties break on name order, never
seat order) — and `JudgeTakeUp` with the move's full disclosure. `KingdomSatelliteRules`
carries the `Satellite=` judgment: parent asked realm-wide, outpost counted city-wide, the
verb slice enforced by the shipped parts a satellite blueprint carries rather than by new
code. `KingdomDelveRules` carries the shaft vocabulary: `IsShaftPair`/`ShaftJoins` (straight
down, one stratum, foot in rock), `ReachedZones` (the flood that separates ground a city OWNS
from ground it can WORK — the surface is always reached; rock spreads from a shaft's foot and
never through a corner), `JudgeDelve` and its refusals, and `ShaftHopCells` — a vertical hop
costs three level hops, and unbroken rock is not an edge at all. `KingdomLabRules.JudgePurpose`
gained the five-argument capital-aware overload; the three-argument shape is unchanged.

`KingdomDelveLink` is the engine-coupled proof behind that edge. `TryPreflight` derives the exact
landing from the frozen authored map and refuses before debit unless the claimed foot zone is
already built, exactly one stratum below in the same world column, and its paired cell is safe;
it checks `IsZoneBuilt` before `GetZone` and never generates refused ground. `TrySettle` keeps the
authored `r_KingdomDelveDown` in the head, places one owned `r_KingdomDelveUp` at the same x,y in
the foot, and publishes the two reciprocal native stair connections. The behavior root, both
endpoints, and global state carry one canonical bounded receipt through schema-last phase writes.
`PhysicalLinkStands` re-proves the already-built zones, exact IDs and coordinates, endpoint
wrappers, passable dry cells, and both connection records on every new-format reach read. Missing,
moved, corrupt, duplicated, or obstructed evidence therefore closes the rock edge; the legacy
`r_TAF_Delved:<zone>` integer is consulted only when no new physical-link state exists.
`TryPreflightStrike` and `TryFinishStrike` remove only that receipt's owned endpoint pair and
connections, then tombstone the physical state. `KingdomDelve.RecordShaft` remains the old-save
compatibility marker; it is not sufficient evidence for a newly authored shaft.

### The claim

What widens the ground every gate above is measured against.

| Member | Contract |
|---|---|
| `enum ClaimVerdict` | `Allowed`, `NothingFoundedYet`, `GroundIsAlreadyOurs`, `GroundIsAnotherCitys`, `GroundIsAnotherRealms`, `GroundIsForeign`, `GroundIsNotAdjacent`, `CityHoldsAllItCan` — ordered from the fact nothing can change to the one the founder can answer today. |
| `static ClaimVerdict JudgeClaim(bool founded, GrowthStage stage, int zonesHeld, bool groundIsOurs, bool groundIsAnotherCitys, bool groundIsAnotherRealms, bool groundIsForeign, bool groundIsAdjacent)` | Pure verdict on whether the seated city may take this ground. `KingdomFounding.JudgeClaim` gathers the booleans off the world and calls this. |
| `static string ClaimRefusal(ClaimVerdict, string seatName, GrowthStage stage)` | Founder-facing refusal; every branch names the lack and what lifts it (STANDARDS 7b). Empty for `Allowed`. |
| `static string ClaimedWallClause(int before, int after, string seatName)` | What the claim did to the wall line, in prose. Nothing standing is ever moved: an edge simply stops facing the world, so a wall raised from here afterward goes on the new outer line and the old line becomes an inner wall. Ground taken diagonally across a corner, or straight down into the rock, frees no edge — the clause says so, and that is the honest answer, not a bug. |
| `static int EdgeCount(KingdomRules.Frontier)` | How many of the four edges are set. |
| `static string ClaimHoldingLine(int held, int ceiling)` | "N held; room for M more at this rung" or "N held, which is all this rung answers for." |

The founder's own claim action is `KingdomCharterPart.ClaimGround` (Charter → **Works & ground**
(`w`) → **Claim this ground** (`l`)) — the first caller `KingdomFounding.ClaimZone` has ever had
outside the founding rite and two debug wishes. **It costs nothing**, which is a decision: the brief prices
founding and every building and names no price for a claim, because what a claim actually costs
is paid afterward and in kind — a new wall line to raise, a new budget of ground to lay, and a
stage that has to have been earned first. A claim that goes through reports the wall clause and
the holding line together, so the founder always knows how much more the rung allows.

## City plans — three ways a thing gets built

### Heart ring-call relocation

`KingdomRelocationRules` and `KingdomRelocationCodec` are public deterministic support types for the
internal Heart ring-call transaction. `KingdomRelocationRules.Valid` applies bounded identity,
geometry, sequential-phase, collision, architecture, and completion-parity law;
`LabourTicks`, `Days`, `Shift`, `Overlaps`, and `TotalTicks` expose the pure quote/geometry rules.
The codec accepts only schema 1 canonical base64 receipts, bounded UTF-8 text/collections, and exact
four-stake moves; unknown schema, duplicate IDs/LotIds, overlap, trailing bytes, malformed state, or
oversize payload fails closed. These types do not grant a general move API: runtime offers exist only
for settlement-raised plots explicitly marked yielding when the next Heart rung needs their ground,
and founder consent through the engine UI remains mandatory.

A settlement is laid out by a grammar, not scattered. All three paths end at the same building:
a single-cell design rises on an `r_KingdomScaffold` and a plot design rises through
`r_KingdomPlotWorks`' own staged raising, and **both close through
`KingdomCeremony.OnBuildingRaised`** — attended, the crew gathers, a measure of water is shared
and the chronicle names who was there; unattended, the homecoming tells it. A plan staked for
either kind carries the surveyor's words to that moment: `KingdomCeremony.TransferPlanQuote`
where the marker and its successor exist together, or `ReadPlanQuote` before the marker comes
down and `CarryPlanQuote` after the works stands, which is the order a plot must use because it
measures its rect out of the marker's own cell.

| Path | Member | Contract |
|---|---|---|
| Automatic | `KingdomLayout.ChooseCell(Zone, KingdomSystem, BuildEntry, out LayoutOutcome)` | Sites a commission by its `Category`: casks by the water, bunks clustered and off the wall line, craft and civic in the settled heart, plots in a ring past the last roof, walls closing gaps in the line. The founder's own ground wins ties — the plan picks the quarter, the founder picks the spot. |
| Planned | `KingdomPlanMarker.OnSettlementPass(...)`, `r_KingdomPlanMarker` | Stake a plan on claimed ground; nothing is spent. The settlement realises staked plans oldest-first when it can afford the water and has room. A plan it can never afford waits forever, without nagging or expiring. |
| Adopted | `KingdomAdopt.AdoptExisting / AdoptWork / Release` | Designate a structure **you** built as serving a civic role. Checks the space, never who made it, so Hearthpyre is never a dependency. A mark, never a transfer; reversible; a refusal names what is missing and touches nothing. |

`KingdomLayoutRules` holds the pure grammar (`PurposeOf`, `ScoreCell`, `Choose`, `HasOpinion`);
`KingdomPlanRules` the ordering and affordability; `KingdomAdoptRules` the role classification and a
bounded flood-fill enclosure test.

**One design is sited by a rule instead, ahead of all three paths.** A `<building>` keyed
`KingdomRoadRules.GatehouseKey` (`"gatehouse"`) belongs on the frontier wall, astride the road,
and nowhere else — `KingdomCommission.FindGateCell(Zone, KingdomSystem, BuildEntry)` is asked
before the automatic plan and puts it on the buildable frontier cell nearest the way out, the
same cell `KingdomRoadRules.TryGate` already names as where the settlement's own `HeartToGate`
road errand walks to. `KingdomRoadRules.SitesAtGate(string key)` is the case-folded check;
`NearestToGate(IList<int> xs, IList<int> ys, int gateX, int gateY)` picks the nearest candidate,
ties broken north then west, so the same settlement puts its gatehouse in the same place every
time it's asked, reload included. Null (and the ordinary plan) for every other design, for a
zone with no frontier left, and for a settlement with no heart yet to aim from.

## `KingdomCreed` — covenant, affiliation, and belief

A settler may carry `Creed`: the preserved public/save property for a real Qud faction, drawn from
factions the realm has dealt with and weighted by standings. It is an umbrella affiliation, not
necessarily theology. `KingdomCreeds.xml` assigns one typed semantic kind: `community`, `people`,
`polity`, `order`, `doctrine`, or `cult`. Unknown keys remain neutral/non-theological and are not
written back into old saves.

Dissent between two cities of one realm is read from **the engine's own faction feeling**
(`Faction.GetFeelingTowardsFaction`, which falls through to the faction's `"*"` wildcard) rather
than any table of ours — so it is correct for modded factions for free, and the zealous factions
that dislike strangers by default are exactly the ones that make a realm hard to hold together.

| Member | Contract |
|---|---|
| `KingdomCreedKind`; `KingdomCreedDraft`; `KingdomCreedDefinition` | Stable typed kind and merge DTOs. Same-name layers inherit omitted attributes; blank `Theology` clears an order opt-in; kind changes, malformed tokens, incoherent theology, and the 128-definition cap fail closed. |
| `KingdomData.TryGetCreedDefinition` / `CreedUsesTheology` | Runtime merged lookup and sole theological behavior gate. Missing definition returns false; no fallback fabricates belief. |
| `CreedOf` / `SeatCreed` / `AwayCreed` | The creed a city holds, or null. |
| `Draw` / `Record` / `Forget` | Creed at arrival, and its removal on death or departure. `Forget` takes the **whole person** out of both tallies — what they hold and what they have held before. |
| `const string CreedProperty = "KingdomCreed"`; `CreedPastProperty`; `PastOf` / `Aligns` / `RememberPast` | Save/API compatibility remains exact. History stores raw faction keys only; kind is resolved, never persisted. Both theological conversion and explicit affiliation adoption retain old alignment without inventing migration state. |
| `KingdomConversion.Convert` / `AdoptAffiliation` | `Convert` refuses every non-theological or unknown target. `AdoptAffiliation` refuses theological targets and is only called by explicit consent. Both use one private transition custody path for exact tallies/history/governance; their prose and mechanics stay distinct. |
| `RiteAvailable` / `HoldRite` / `EaseForMeal` | The founder's levers against dissent. `HoldRite` publishes water, rite tick, and dissent under one compensated governance reservation; false/exception restores exact snapshots before the action can commit. |
| `DeclarableCreeds` / `Declare` | Name the realm's creed: decisive, and costly across the world. Declaration snapshots both standing/carry roots, creed, and dissent, then compensates every injected or engine failure before governance commit. |
| `SecededHolds` / `Secede` / `TryRejoin` | A city may leave, keeping its ground, people and buildings. It can be asked back once the cause is gone. |

Dissent accrues on world time like everything else, uncapped. A realm does not fall apart while
nobody is playing for a different and better reason: the breaking point is a **city brink**, so
crossing it records the quarrel and stops; secession itself waits for the founder to be told —
word reaching them wherever they are, naming the rite — and then for nine **world-days** to run
from that warning, whether or not they come back. Mending the cause lifts it at any point.

## `KingdomLarder` — dedicated food, and what the settlement does with it

| Member | Contract |
|---|---|
| `static bool HoldSharedMeal(KingdomSystem, Zone, out string failure)` | Spends food from dedicated larders only and records the meal. Returns false with a reason when the larder cannot feed one; nothing is spent on failure. |

Food is counted from containers carrying the `KingdomLarder` int property, which the Charter's
dedication flow sets and which commissioned pantries set for themselves. Dedication is a mark, not
a transfer: nothing is moved, and an undedicated container — including the player's own pack — is
never read or spent.

The shared meal is no longer the only thing that empties a larder: since food became a flow the
settlement eats from it every day (see *Food as a flow*). What remains true is that an empty
larder is never itself a punishment — a settlement with no larder at all forages and lives, and
what the larders buy is a cushion rather than a licence.

`KingdomRules` carries the arithmetic: `PantryTier`, `PantryTierNames`, `ClassifyPantry(int)`,
`MealCost(PantryTier)`, and the `Pantry*Threshold` / `MealCost*` constants.

## `KingdomSettlement` — one city's state

The realm is the faction; a settlement is one of its cities. One is *seated* at a time — its
state lives in `KingdomSystem`'s own fields, which is what every consumer reads. Up to two other
cities wait in authoritative `KingdomSettlementTopology` until the founder walks into their
ground. `Away` is only the first-row compatibility projection.

| Member | Contract |
|---|---|
| `string SettlementName` / `string Vocation` | The city's own name and what it was founded for. A null vocation is the realm's first city, founded before there was a second to tell it from. |
| `const int MaxSettlements` | 3: one active seat plus at most two non-seat cities. |
| `static string[] Vocations` / `VocationBlurbs` | The fixed vocation set and its menu prose. |
| `static bool IsKnownVocation(string)` / `VocationClause` / `VocationSuffix` / `VocationBlurb` | Vocation validation and presentation; an unknown vocation degrades to the neutral one. |
| `static SecondFoundingVerdict JudgeSecondFounding(bool founded, int settlementsHeld, bool groundIsClaimed, bool groundIsAdjacent)` | Historically named pure rule for whether the rite may found the next additional city before the three-city cap. |
| `static string SecondFoundingRefusal(SecondFoundingVerdict, string realmName)` | Founder-facing refusal text; empty when the rite is allowed. |
| `void ReadFrom(object seat)` / `void WriteTo(object seat)` | Carry a city into or out of a seat by field name. Throws `KingdomSeatMismatchException` **before writing anything** if the seat cannot carry a field. |
| `static FieldInfo[] CarriedFields()` / `static List<string> SeatMismatches(Type)` | What a city holds, and what a seat cannot hold. |

## The city book — check-in, check-out, and one page per zone

> Design: `_notes/LIVING-CITY-ARCHITECTURE.md` §1 (the model), §3.1 (check-in), §3.4 (check-out),
> §3.9 (deficits drain real containers), §6.5 (the receipt).

**The city is a book, and a zone is a page of it that happens to be open.** One
`KingdomCityBook` hangs on every `KingdomSettlement` as `City`, carried by the seat swap on its own
name exactly as `Ledger` is. It replaced two families of `The.Game` game-state keys —
`r_TAF_Supports_<zoneID>_*` and `r_TAF_Larders_<zoneID>_*` — which were the right answer for five
ints that had to be readable without loading a zone, and the wrong answer for a hundred typed rows.
Every number the retired keys used to answer is answered by the book, and answered the same.

**Authority alternates, explicitly, and never overlaps.**

- While a zone is **attended**, the ground is authoritative and the book is a mirror. Pour water
  out of a cask by hand and the book agrees with the cask.
- While a zone is **suspended**, the book is authoritative and carries that zone's last-read
  numbers plus everything credited or drawn since.
- The handoff is a **check-out** (ground → book) and a **check-in** (reconcile, then reify).

**Check-in** runs at the settlement pass's `check-in` step, after `survey` and before `trade`, so
every step below it reads a ground the book has already made true. In order: advance the model to
now through the executor (§2.5 — one choke point, one `[TAF] perf reckon` receipt); pay this zone's
standing signed debt onto its real containers in **dedication order**; carry the city's own stock
to where the founder is standing if the seated zone cannot cover the day it is about to be billed
for; then let the ground overwrite the row, attributing and telling any difference rather than
silently repairing it.

**Check-out** runs twice, and only the second is load-bearing. The pass's own `check-out` step is
the cheap one that usually gets there first. `SuspendingEvent` is the true last read: it fires from
`SuspendZone` *before* `Suspended` is set, for any zone, while its objects are still in RAM —
unlike `ZoneDeactivatedEvent`, which fires while the zone still has up to forty turns of live
simulation ahead of it. **A missed check-out costs freshness, never correctness**, because check-in
reconciles against the ground either way.

**The signed counter, and what makes a deficit real.** Each zone row carries what it owes its own
containers, *per stock kind and signed*: positive lands, negative draws. One net figure is not
enough — a zone may still carry historical or explicit transfer evidence beside a water draw — so
the row retains three signed figures and the weighted `owed` §3.5 reports is derived from
them. A draw is spread across the zone's dedicated vessels **oldest dedication first**
(`KingdomCity.DedicationOrderProperty`, minted the first pass that counts a container as the
city's), which is deterministic without a draw and stable across a reload. What the containers
could not cover stays on the row and is told; it is never silently forgiven.

**Water works produce, and the day is billed once (W6).** A zone row's `WaterCarry` is what its
works make in a day, measured by the pass that read that ground. The model integrates it off its
one `ProcessedThroughTick`, in world-day boundaries so splitting a span pays the same days as one
whole run. `FoodCarry` remains a frozen row column but is ignored and normalized to zero: fields
and mills alone own physical food creation. Old row food credit/debt is grounded to the currently
observed containers before reify, so upgrading cannot mint or drain pantry stock.

Production raises the row's **level and its signed debt by the same amount**, which is invariant
I1 in one line: the ground has not changed, so `level − owed` — what the model says the ground
holds — has not changed either. What the works made is a claim on a vessel nobody has poured, and
§3.5's amortised reify is what pours it. `KingdomProductionRules.TryReconcile` re-derives the debt
from the ground on every check-in, so `level − owed == ground` holds **by construction** rather
than by care, and that equality is exactly what the audit line prints. A claim bigger than the room
the containers have is **spilled**, named in the log, and not carried — the same loss a harvest
with a full larder has always taken.

**Logistics: nearest holder, and one trip where one will do (W6).** A shortfall where the founder
is standing is met out of the city's **closest** quarter actually holding the resource, on the
level-1 zone graph (`KingdomCityRules.TryZoneGraph` — Floyd–Warshall over ≤ 9 nodes, composed from
zone ids alone and therefore safe to build at reckon), tie-broken on the lower row index. Inside a
quarter the oldest dedication still pays first: the two rules answer different questions and both
stay true. A crop load bound for a larder a porter is already walking to is **folded onto that
porter** rather than minting a second one — capacity-bound batching at the one moment it can
prevent the pathology. `KingdomLogisticsRules` holds both of §3.10's "never looks stupid" checks as
functions (`TryNoNearerHolder`, `TryNoTwoHalfEmptyTrips`) so the tests and the runtime ask one
implementation of them, and the nearest-neighbour + 2-opt trip planner with §3.10(4)'s hard caps
(16 jobs, 8 stops, 50 swap tests, **zero draws**).

| Member | Contract |
|---|---|
| `KingdomSettlement.City` / `KingdomSystem.City` | The settlement's book, and the flat field the seat swap carries it in. |
| `KingdomCityBook` | The serialized carrier: named-field `IComposite`, flat primitive columns, one row family per group of columns. `Normalize()` repairs nulls and bounded presentation rings, but **never cross-zips or truncates ragged resident evidence into invented people**. A complete legacy name/origin/arrival triple may seed a wholly empty resident book once as non-labouring `Abroad` claims; incomplete or conflicting legacy evidence is retained and fails closed for inspection. Current resident rows are sole authority and project outward to the obsolete ABI lists one way. The work cap is the full current four-zone City envelope (`4 × MaxBuildingsForStage(City) = 880`), not the retired flat forty-work proxy; a legal later work cannot disappear from the model because an earlier zone filled it first. |
| `KingdomCityBook.ZoneCount` / `WorkCount` / `ResidentCount` / `ToldCount` / `TryZoneRow(string, out int)` / `TryResidentRow(int, out int)` | What the book holds, and the two lookups every re-plumbed reader goes through — by zone id for a sighting, by `KingdomResidentId` for a person. |
| `KingdomCityBook.TryReadBrink(int, BrinkKind, …)` / `TryWriteBrink(int, BrinkKind, …)` | Where a settler's brink windows live since W2. Column reads keyed on the resident id, not a whole-model read: three consumers ask this once per settler per pass. |
| `KingdomCity.CheckIn(KingdomSystem, Zone, KingdomSurvey, long)` | The pass's first word with the book. See above for the order, which is load-bearing. |
| `KingdomCity.CheckOut(KingdomSystem, Zone, KingdomSurvey, long)` / `OnSuspending(KingdomSystem, Zone)` | The two readings. `OnSuspending` filters to zones the seated realm claims and takes its own survey. |
| `KingdomCity.RecordSupports(...)` / `RecordLarder(...)` | Where `KingdomSubsidence.RecordZone` and `KingdomCrops.RecordLarders` now write. |
| `KingdomCity.OtherZones(KingdomSystem, Zone)` / `LarderRoomElsewhere(KingdomSystem, Zone)` | Where `KingdomSubsidence.OtherZones` and `KingdomCrops.LarderRoomElsewhere` now read. `ZoneSighting` survives as the projection the rows hand the subsidence arithmetic, so that arithmetic did not change at all. |
| `KingdomCity.AuditLine(KingdomSystem, Zone, KingdomSurvey)` / `OwedThirds(KingdomSystem)` | The §3.9 audit as one greppable line — `model=`, `debt=`, `ground=` per stock kind, and `MISMATCH` when `model − debt != ground`, which is I1 in full — and everything the city still owes, in weighted thirds. |
| `KingdomProductionRules` | The W6 arithmetic, pure: `TryDaysBetween` (world-day boundaries, additive across every split), `TryProduce` (level and debt move together, clamped, spill reported), `TryReconcile` (the ground wins, the claim survives, the debt is re-derived so the audit is exact by construction). |
| `KingdomLogisticsRules` | §3.10 items 1 and 4, pure: `TryMeasure` / `TryNearestHolder` / `TryNoNearerHolder`, and `TryBatch` / `TryPlanTrip` / `TryNoTwoHalfEmptyTrips`. Every bound is a `KingdomBudgetRules.Planner*` constant and no path draws. |
| `KingdomCity.DedicationOrderProperty` (`KingdomDedicationOrder`) | Dedication order as a stored fact. Minted the first pass that counts a container as the city's, and never moved afterwards; an unstamped container sorts **last**. |
| `KingdomSystem.SimulationSeedHigh` / `SimulationSeedLow` / `MintSimulationSeed(int worldSeed, string realmName, long foundedTick)` | The realm's kernel seed, minted once at founding from the world seed, the realm's name and the tick the water was poured — deterministic across a reload, separated between realms, and refused rather than re-minted. |
| `KingdomSystem.Bindings` / `ResidentCounter` | The realm's binding registry and its id counter. **Realm-scope, never carried by a city** — see below. |

**The receipt.** Every reckoning goes through the executor seam and leaves one line in Player.log
behind the dev-log option, in the shape the log-watcher reads:

```
[TAF] perf reckon label=Kavvat steps=1 rows=232 ms=0.14
[TAF] perf BUDGET reckon label=Kavvat steps=64 rows=14848 ms=9.2 over=8
```

A figure that crosses a budget is prefixed `BUDGET` and names the budget it broke. The lanes and
their rungs live in `KingdomBudgetRules` and nowhere else.

## Residents as rows, and the binding registry

> Design: `_notes/LIVING-CITY-ARCHITECTURE.md` §1.2(d) (the resident row), §3.8 (one identity, at
> most one body), §8.3 (hard problem 2 — where a person lives, object or row).

**The row is primary and the body is a durable view bound by a stable id.** A settler's
`GameObject` carries `KingdomResidentId` and nothing else about them; their name, origin, creed,
home, standing and both brink windows are a **resident row** in their city's book, because a row
is what survives their zone going to disk and a property bag is not. The id is minted once per
settler off a realm-scope counter, in order, never reused and **never drawn** — identity is a
substrate, and a seeded id would make who-is-who depend on how many other things had been rolled
first.

Check-in reads the roster off the ground under the founder's feet: every settler standing here gets
an id, a row and a binding. Every row already bound to *this* zone whose body is **not** here is
witnessed and moved.

**The standing vocabulary** — three states and no fourth:

| Standing | Means | On the roll? | Labours? | Bound? |
|---|---|---|---|---|
| `Resident` | Lives here | yes | yes | yes, exactly one body |
| `Abroad` | The founder charmed, recruited or led them away | yes | **no** | no |
| `Dead` | Killed, with a cause | no | no | no |

Every non-`Resident` row carries a **cause** from its own family — the four death causes are
`KingdomOfficeRules.DeathCause`'s own, so the funeral the city already tells stays the *one*
telling. `Dead` is terminal: a dead row never transitions again, whatever the ground says next.
**W2 ships the vocabulary, the transitions and the reconciliation; placement and enforcement are
W3**, so no labour or population figure changed this wave.

**The binding registry** (`KingdomSystem.Bindings`) answers *one identity, at most one body* for
everything this mod mints — residents by `ResidentId` now, and the carriers W3 mints by `JobId`,
with the same rules and the same tests shipped today. It is **realm-scope and never carried by a
seat swap**: a bound body can be standing in the other city's ground or walked off the map
entirely, so a registry a city carried would answer for half the realm and lose the other half
every time the founder crossed a zone line.

**Check-before-mint is the only path to a body:**

| Registry says | Body resolves | Verdict |
|---|---|---|
| miss | — | **Mint**, and write the binding in the same publish |
| hit | live in *this* zone | **Move** it. Do not mint |
| hit | live in another resident zone | resident: **MoveAcross**. transient: **Refuse** |
| hit | does not resolve (its zone is on disk) | **Refuse**. The debt stays owed |

**An unresolvable binding is a refusal to mint, never a licence to mint.** A frozen body is
invisible; its binding is not, and the binding is what we consult — which is what makes this hold
across suspend, freeze, save, reload and crash. **A closed binding is evicted at once, so absence
from the registry *is* proof of closure**; there is no second list to keep in step, and the
eviction must name its cause or it is refused.

| Member | Contract |
|---|---|
| `KingdomResidents.ResidentIdProperty` (`KingdomResidentId`) | The settler's identity, and the only thing about a person their body carries. |
| `KingdomResidents.JobIdProperty` (`KingdomJobId`) | The exact job a transient porter or other carrier renders. Production binds it only after durable job publication; the stale-body sweep is keyed on it and removes a rendering whose model job already closed. |
| `KingdomResidents.IdOf(GameObject)` / `EnsureId(KingdomSystem, GameObject)` | Read an id; mint one if the body has none. |
| `KingdomResidents.TryLocate(...)` / `TryEnsureRow(...)` | Which book holds a body's row; and the same, enrolling a settler the roster has not reached yet. |
| `KingdomResidents.Judge(KingdomSystem, int, KingdomBindingKind, string zoneId)` | Check-before-mint at the edge, answered by the table above. |
| `KingdomResidents.Bind(...)` / `Unbind(..., KingdomUnbindCause)` | Write or move a binding; evict one, naming why. |
| `KingdomResidents.SweepVerdict(KingdomSystem, GameObject)` | Whether an object in a thawed or entered zone is a stale transient. The production sweep runs before carrier rendering so a closed job and a leftover body cannot expose the same cargo twice. |
| `KingdomResidents.AuditLine(KingdomSystem)` | Invariant I3 over the whole realm — no binding key ever resolves to two living bodies. Runs beside the §3.9 stock audit on every check-in. |
| `enum KingdomBindingKind` / `KingdomBindingVerdict` / `KingdomBodyPresence` / `KingdomUnbindCause` / `KingdomSweepVerdict` | The registry's vocabulary. |

## The city renders — the hour, the porter, and the pump

> Design: `_notes/LIVING-CITY-ARCHITECTURE.md` §3.2(b) (people, by role and by the hour), §3.5
> (amortised materialisation), §3.6 (the heartbeat), §3.7 (embodied arrivals and the itinerary),
> §3.10(2)(3) (the distance matrix and the roads discount), §6.4 (prefetch), §6.5 (the receipt).

**Everything the city owes the ground is paid on a per-turn budget, and never in one activation.**
`ZoneRepair` keeps a counter and applies its whole backlog in one loop
(`D/XRL/World/ZoneParts/ZoneRepair.cs:87-97`); we keep the counter and spend it eight weighted
units at a time, **visible cells first**. That single change is Addendum 12(b)'s *reification is
AMORTISED*. Entry costs O(budget) and never O(elapsed): a day away and a season away differ in an
integer, never in shape.

**The budget is per turn, not per call site.** The homecoming pass, the pump and the prefetch all
reify on the same turn, so the realm carries one allowance (`KingdomSystem.ReifyTick` /
`ReifyThirdsSpent` / `ReifyHeavySpent`, none of them serialized) and each call site takes what is
left. A unit leaves the debt at the instant it **lands**, so re-entering, reloading or
re-activating cannot pay the same debt twice — and a debt bigger than one container is still owed
afterwards, which is what the founder reads when a granary has more in its books than on its
shelves.

Container demand is measured from ground rows, not inferred once per stock kind. Visibility is the
first key, then stored dedication and stable id. One successful callback touches one container;
only its measured delta clears debt or charges budget. Current legal maximum is 220 commissioned
root containers plus 24 water and eight food manual dedications plus sixty bodies: 312 weighted
units, 39 turns. Plot furnishings are not implicit civic accounts; legacy marks are released in
place without moving contents or clearing signed debt.

| Member | Contract |
|---|---|
| `KingdomCity.SpendTurn(KingdomSystem, Zone, long)` | One turn's amortised spend against one zone's standing debt. Returns whether the turn's allowance is now exhausted. Surveys only when something is actually owed, so a caught-up zone costs nothing. |
| `KingdomCatchUpRules.TryPlanTurn(demand, out spend, out fault)` | The whole turn's budget: 8 weighted units, at most 4 heavy, visible cells before the rest. |
| `KingdomCatchUpRules.TryPlanTurn(demand, thirdsAvailable, heavyAvailable, …)` | The same plan against an allowance already partly spent. An allowance larger than the constitution's own is refused, never granted. |
| `KingdomContainerCatchUpRules.TryMeasure(...)` / `TrySettle(...)` | Exact post-survey medium demand and measured settlement. Supports mixed signed kinds, blocked remainder, callback failure, and save/reload continuation without duplicate application. |
| `KingdomHeartbeat.OnEndTurn(KingdomSystem)` | **The pump.** One `EndTurnEvent` handler on the game system — a single dispatch immediately before `ProcessSingleTurn` (`D/XRL/Core/ActionManager.cs:1644-1650`), not the 2,000-cell broadcast a live zone pays. Runs the slice, retires finished jobs, spends the turn's reify budget, and considers the prefetch. Returns immediately with no seated claimed zone and no debt. |
| `KingdomHeartbeat.OnThawed(KingdomSystem, Zone, long ticksFrozen)` | A zone off disk: the stale-transient sweep runs here, before anything looks at the ground. `TicksFrozen` is a cross-check, never a clock. |

**Placement by the hour rides vanilla's only daily-life surface.** There is no NPC scheduler in Qud
— no `GoToPartyLocation`, no `Schedule` class — and this adds none. A settler with
`Brain.Wanders = false` self-anchors to where it first stands (`D/XRL/World/Parts/Brain.cs:2056`),
`Brain.Stay(Cell)` moves that anchor (`:2507-2521`), and the `Bored` goal walks them back to it
forever (`D/XRL/World/AI/GoalHandlers/Bored.cs:126-140, 262-266`). The model therefore does not
*place* people so much as **move the anchor**, and vanilla's own AI does the walking. `Bored` does
nothing when the actor is not in the player's zone, so all of this is attended-only and costs
nothing per turn anywhere else.

`r_KingdomStation` rides `IdleQueryEvent` exactly as vanilla's `Bed` does
(`D/XRL/World/Parts/Bed.cs:187-224`): it is offered the idle actor, and **returning `false` claims
that actor's turn**. So it is selective — it claims only a settler posted to *this* work, only when
the hour actually wants them somewhere else, and at most once every 50 ticks, which is the same
cooldown and the same figure `Bed` keeps.

At an attended post, the station maps authoritative work/day shape to one short visible act:
tending rows, sorting stores, plying a craft, maintaining works, setting a construction piece,
keeping watch, or attending a shrine. The queued delegate re-proves the exact body, post, work,
zone, and proximity before showing an explicit-velocity particle cue. It consumes no simulation
RNG and grants no stock, progress, standing, skill, experience, or other gameplay state.

| Member | Contract |
|---|---|
| `KingdomPlacementRules.BandFor(long)` → `KingdomDayBand` | Which of five bands a tick falls in. The bands are unions of `Calendar.GetTime`'s own eight stretches, cut where the calendar already cuts (`D/XRL/World/Calendar.cs:296-352`). Total over every representable tick. |
| `KingdomPlacementRules.PostFor(KingdomDayShape, KingdomDayBand)` → `KingdomPost` | Where a day shape stands in a band. §3.2(b)'s table, and the only copy of it. The watch keeps its post in every band; market and shrine keep theirs through Hindsun; everybody else goes home. |
| `KingdomPlacementRules.MayClaim(long lastClaim, long now)` / `ClaimCooldownTicks` | One claim per station per in-game hour. |
| `KingdomStations.PostWorkProperty` (`KingdomPostWorkId`) / `PostKindProperty` (`KingdomPostWorkKind`) | The work a settler is posted to, stamped by `KingdomGrowth.AssignWork` from `KingdomCrewRules.CrewOutcome.SettlerIndices`. Until W3 crewing was a fact about a *work* only, so every resident row read `JobWorkId = 0`; now the row carries the post and the day shape derives from it. |
| `KingdomStations.Attend(KingdomSystem, Zone, KingdomSurvey)` | Gives every crewed work an `r_KingdomStation`. Added at render rather than in a blueprint, and picked up by `Bored`'s own zone-scoped `WantEvent` scan — no registration list to maintain. |
| `KingdomStations.Misplaced(...)` / `Place(...)` | Whether a settler's anchor disagrees with the hour, and the one heavy reify unit that fixes it. Asymmetric on purpose: wanting a post is *the anchor is not the post*; wanting a hearth is *the anchor is still the post*. |

**The porter: one effect, at most one rendering.** A load already in flight
(`KingdomSystem.PendingCrop`) is rendered **embodied** when the founder is standing in a claimed
zone with a larder that can take it, and by the plain path otherwise. Both consume the same load
from the same counter, so nothing is delivered twice — the rendering is chosen by attendance and is
deliberately **not a draw**, which is what keeps a reload from re-rolling a person.

The carrier is minted at the zone edge facing the source, walks by `Brain.PushGoal(new MoveTo(...))`,
deposits **real items** into a real `Inventory`, and leaves by the edge they came in by. The goods
carry vanilla's `_stock` mark — *"the simulation created this; the simulation may remove it"*
(`D/XRL/World/Parts/GenericInventoryRestocker.cs:229, 257`) — and anything that is not `_stock`, or
that answers `IsImportant()`, is dropped to the cell and never destroyed.

**A job is a timed itinerary, computed once, at creation**, and one pure function answers where the
carrier is and what is on them at any tick. Every zone renders that same answer, which is invariant
I5 and why the body never has to literally traverse anything. Leg endpoints are model truth; the
in-between is a redrawing. Path length at creation is `Chebyshev × Sinuosity × RoadDiscount` with
**zero zone access** — the estimate is a prior that reality corrects, never a pathfind at reckon.

| Member | Contract |
|---|---|
| `KingdomSystem.Jobs` (`KingdomJobRegistry`) | The realm's open itineraries. **Realm-scope, beside `Bindings`**, because a carrier's legs can cross into the other city's ground and every job row is paired one-to-one with a transient binding that already lives there. §0.0(c) prices job rows realm-wide and §3.8 caps them per realm, so this is where the constitution already put them. `MintJobId()` mints in order and never reuses; `Normalize()` drops a job whose declared legs are not all present, whole. |
| `KingdomJobRules.TryBuildLegs(plans, count, startTick, walkTicksPerCell, …)` | Waypoints to a dated itinerary. A journey wanting more than six legs is **refused at planning**, never truncated. No leg is instantaneous. |
| `KingdomJobRules.CargoAt(job, fix)` / `Deposited(job, tick)` | What is still on the carrier's back, and whether the deposit leg is finished. |
| `KingdomJobRules.Mirror(x, y, edge, w, h, …)` / `EdgeToward(here, source)` | The engine's own zone connection, and which wall faces the ground the load came from. **Facts, not draws**, so a handoff cannot disagree with where the founder comes out. |
| `KingdomJobRules.TryDrawEntryCell(...)` / `TryDrawOrigin(...)` | The only two draws a delivery gets, on `taf:stream:delivery` with the job id as the occurrence ordinal. Same seed, same journey. **Routing contains no draw at all** (`KingdomBudgetRules.PlannerMaxDraws` is zero). |
| `KingdomPorters.Embody(KingdomSystem, Zone, KingdomSurvey, source, blueprint, amount, tick)` | Puts a load in flight onto a real back in the attended zone. Returns what left the road; zero means the plain rendering keeps it, which is I2 and not a failure. |
| `KingdomPorters.Render(KingdomSystem, Zone, long)` | Places every open job's carrier at `At(job, now)` in the zone that has just become attended. Mint-or-move through the registry only. |
| `KingdomPorters.Sweep(KingdomSystem, Zone)` | **The stale-transient despawn.** W2 shipped the verdict; this lands it. Runs at `ZoneThawedEvent` *and* on the entry path, because a suspended-but-resident zone is entered with no thaw at all. |
| `KingdomPorters.Retire(KingdomSystem, long)` | Closes a job the model has outlived and puts what the carrier was holding back on the road (§3.8 t2). Never closes a job whose carrier is on resident ground — that carrier is walking, not outlived. |
| `KingdomPorters.LoadPerTrip` | One trip's load. A named stand-in for W6's capacity-bound batching, and a **reify** figure rather than a fiction about how much a person can lift. |
| `KingdomItineraryRules.TryReproject(...)` / `TryHasOverrun(...)` | **Only the unstarted remainder may move.** A leg already begun keeps its `DepartTick`; the current leg's `ArriveTick` and everything after shift by the same signed delta. Applied at check-in, where the ground already wins, and at most **once per leg** (`r_KingdomPorter.ReprojectedLeg`). A job whose elapsed exceeds twice its projected duration fails instead — so a founder who blocks a doorway forever produces a story, not an unbounded job set. On failure the load is **set down where it fell** and its `_stock` mark taken off it, which hands it to the founder for good. |
| `KingdomWord.Ambient(KingdomSystem, from, here, note)` | News that is neither a brink nor its aftermath — the heartbeat's one line an hour, a carrier who could not get through. The ordinary note lane plus the same one-line push, because the founder is by definition not standing where the news is. |

**The distance matrix is two-level, and never stores `works²`.** Level 1 is the zone graph — at most
nine nodes, all-pairs by Floyd–Warshall at exactly 9³ = 729 integer operations, a table of at most 81
entries. Level 2 is work-to-edge and same-zone pairs. Any cross-zone distance composes in O(1).
**Invalidation is by structure, never by time or by stock**: a dirty flag per zone, set only on work
placement, work removal or a road change, and a dirty slice **refuses to answer** rather than
answering stale.

| Member | Contract |
|---|---|
| `KingdomZoneGraph.TryBuild(nodes, count, hopCells, …)` | The level-1 all-pairs table plus the next-hop matrix the itinerary reads its zone path off. Reports its own operation count. Orthogonal edges only — deliberately narrower than `KingdomRules.CoordsAdjacent`, because a carrier cannot walk through a corner. |
| `KingdomDistanceMatrix.TryCreate/TryWriteZone/TryCompose/MarkDirty` | The two level-2 slices and their flags. Refuses to allocate past §0.0(c)'s entry budget. |
| `KingdomItineraryRules.RoadDiscountPercent` (**60**) | A paved leg costs 0.6 of the same distance unpaved, **applied identically to the estimate and to any measured length** so a road cannot make the two disagree. Laying a road visibly shortens every itinerary that uses it. |
| `KingdomDistanceRules.ZoneTransitCells` / `TryDiscount(...)` | What one hop costs the metric, and the discount applied to it. |

**Prefetch is a spike, not a promise.** `KingdomHeartbeat.PrefetchOption`
(`r_TAF_OptionPrefetch`) gates it; its experimental checkbox in `Options.xml` defaults to **No**.
The mechanism ships complete: at most one neighbour held, two
considered, only while a debt stands, skipped when the seated zone has saturated the turn's reify
budget, and released the moment the counter drains. *A prefetched zone the founder never enters is
indistinguishable from one that was never prefetched* — prefetch may change **when** work is done,
never **whether** or **how much**.

**The receipt grows.** `reify` and `thaw` join `reckon`, and the heartbeat writes `slice`:

```
[TAF] perf reckon label=Kavvat steps=1 rows=232 ms=0.14
[TAF] perf slice  label=Kavvat steps=1 rows=232 ms=0.09
[TAF] perf reify  label=JoppaWorld.11.22.1.0.10 owed=6 rows=3 thirds=12 units=4 ms=0.31
[TAF] perf thaw   label=JoppaWorld.11.22.1.1.10 reason=prefetch ms=31.2
[TAF] perf BUDGET reify label=... units=9 ms=2.4 over=2
```

On a `reify` line `rows` is how many of the units were visible-cells-first and `thirds` is the
weighted spend; `owed` is exact remaining weighted demand after that zone's post-mutation ground
survey, so 252 same-kind vessels report 252 units rather than one. A
figure that crosses a §0.0 budget is prefixed `BUDGET` and names the budget it broke.

**What remains deliberately narrow.** Central routing, nearest-holder proofs, and exact itineraries
are shared substrate. Existing food, water, carry, and purpose authorities mint their own receipts;
ordinary construction still lacks its one routed-input adapter and therefore may spend only exact
local custody. Per-zone water production rates are live: when runtime passes a null water override
array, `KingdomCityAdvanceable` reads each row's measured `WaterCarry`, then applies the realm's
method factor. `FoodRateOf` always returns zero; frozen food arrays/columns cannot mint stock.
Growth no longer credits those same water-production days; TESTING step 90r guards against double
billing. Capacity-bound batching remains an owning logistics decision where many
jobs compete over many holders; it does not grant another subsystem cargo authority.

## The city has a history — happenings, ambience, and what the creeds make of you

> Design: `_notes/LIVING-CITY-ARCHITECTURE.md` §7.4 W4 (happenings, the shared telling budget, the
> told-log ring), `_notes/BUILDING-CATALOGUE-BRIEF.md` Addendum 13 — **the mesh condition**: every
> lane is a *rendering* of model state through surfaces that already exist, and no lane builds
> parallel machinery.

**Nothing here is a new channel.** A happening reaches the founder through `KingdomWord`, reaches
the book through `KingdomChronicle` (which writes the outsider register too, so a feast entering
the world's rumour costs nothing but recording the feast), and is remembered in the told-log ring
`KingdomCityState` has carried since W0. Announce-once is the ring's job; there is no second
ledger. **Recording is unbudgeted and telling is not** — a wedding that happens while the founder
is being told about something else still happens, still reaches the chronicle, and is still counted
in the homecoming report.

**Four kinds, and each one is a row the model already keeps.**

| Kind | Trigger, in rows | Telling |
|---|---|---|
| **Wedding** | Two `Resident` rows sharing one `HomeWorkId`, both settled ≥ `CourtshipDays`, creed codes that agree; one draw per pair per reckoning | Chronicle + one push in a named settler's mouth (`VoiceOccasion.Wedding`) |
| **Funeral** | A row that went `Dead` with a cause the memory machinery can name | **The clause inside the death's own telling** — see below |
| **Festival** | Qud's own calendar crossed an anchor | Chronicle (with its own outsider clause) + one push naming the realm's dish |
| **Breakdown** | A work row worn past the condemned line, or a hands-needing work with nobody on it | Chronicle + one push; **unsaid** through `KingdomWord.Unsay` when it turns again |

**The funeral is not a second announcement.** `KingdomOffices.RecordDeath` is already the single
place a settler leaves the living roll and the single place the chronicle and the message queue
hear about it; W4 composes the rite *into* that line rather than beside it, and writes the ring row
in the same call. One telling per death is therefore structural, not guarded. A safety net in the
reckon catches the one case `RecordDeath` cannot see — a settler killed before the mod ever tagged
them — and it is gated on both the ring and `KingdomSystem.DeadNames`, so it cannot double.

**Festivals are anchored to vanilla's calendar and to nothing else.** A survey of
`D/XRL/World/Calendar.cs` found **no holiday machinery in the engine at all**: no `Holiday` type,
no `HolyDay`, no date-pinned event, and not one place in the game that branches on `GetMonth()` or
`GetDay()`. What Qud does have is two named days, and those are the only two anchors:

- **The festival of Ut yara Ux** — the five-day intercalary month at year-ticks 216001–222000
  (`Calendar.cs:87-89`, `:136`), and Qud's one canonical named festival:
  `D/Qud/API/JournalAPI.cs:467`, `D/XRL/World/Parts/GenerateFriendOrFoe.cs:54`,
  `B/Books.xml:499, 1323, 1368, 1631`.
- **The Ides** — the one day of the month Qud declines to number (`Calendar.cs:223` returns the
  literal `"Ides"` for the fifteenth). Twelve a year, and the six months after the intercalary are
  shifted 6,000 ticks because `Calendar.GetDay` subtracts Ut yara Ux's length back out
  (`Calendar.cs:160-163`).

The feast serves the realm's own dish — `Faction.WaterRitualRecipeText` as `KingdomDish` stamped
it — so the city eats what its creed already eats. The arithmetic is closed-form in both
directions, so a founder gone a season and one gone a decade cost the same O(13).

| Member | Contract |
|---|---|
| `KingdomHappenings.OnZoneActivated(KingdomSystem, Zone)` | The settlement pass's happenings step. Runs last of the resolvers, pushes at most two lines, then the regard, then the ambience only if neither spoke. |
| `KingdomHappenings.FuneralClause(KingdomSystem, string name, KingdomOfficeRules.DeathCause, Zone)` | The rite clause the death's own telling carries, and the ring row. Called from `RecordDeath` and nowhere else. |
| `KingdomHappenings.Digest(KingdomSystem, KingdomCityBook, long sinceTick)` | What the ring holds since the founder last stood here, as **one** ledger note. |
| `KingdomHappenings.Enabled` / `HappeningsOption` | Gate `r_TAF_OptionHappenings`, default **Yes**. Live the moment a checkbox for it lands in `Options.xml`. |
| `KingdomHappeningRules.TryNextFestival(from, out due, out anchor)` | The next feast strictly after a tick. O(13), no term contains the elapsed. |
| `KingdomHappeningRules.TryLastFestival(at, out due, out anchor)` | The most recent feast at or before a tick — the jump a long absence takes instead of a walk. |
| `KingdomHappeningRules.AnchorAt(yearTick)` | Which feast a position in the year is. The **one** definition; both searches label their answer with it. |
| `KingdomHappeningRules.Judge(KingdomWorkRow, bool believedBroken, long)` | What a work is worth saying, given what the city last told the founder about it. |
| `KingdomToldKind.Wedding` / `.Funeral` / `.Festival` | Appended to the ring's vocabulary at 11, 12, 13. Values are never reordered — the ring serializes as plain ints. |

**Ambience (lane 3): the attended zone breathes, once.** `KingdomAmbient.Speak` reads counts off
the work and resident rows — what is turning, what has stopped, whether anything cooked today, who
is at the shrine, whether the cisterns are empty — and picks **one** line for the hour's band
(`KingdomPlacementRules.BandFor`, the same bands the placement layer anchors people by). There is
no draw: the same city in the same state at the same hour says the same thing. Each line carries a
key, and a line repeats **across a day boundary and never inside one**. A stopped wheel outranks
every texture line, because silence where there was noise is the only ambient line that is also
news.

| Member | Contract |
|---|---|
| `KingdomAmbientRules.TryLine(reading, band, out line, out key)` | The city's one line for this hour, chosen and never rolled. |
| `KingdomAmbientRules.Speakable(key, lastKey, day, lastDay)` | Whether it may be said: a different line, or a different day. |
| `KingdomCityBook.AmbientKey` / `AmbientDayOrdinal` | What the city last said and when. On the carrier, not in the frozen model: it is a fact about the telling, not about the city. |

**The city reacts to what you ARE (lane 2), out of vanilla's own tables.** `KingdomAmbient.Regard`
reads the founder the same derived way `KingdomQol.TruthOf` reads a settler, and judges from two
surfaces the game already fills in:

- **`Faction.PartReputation`** (`D/XRL/World/Faction.cs:150`, loaded at
  `D/XRL/World/Factions.cs:664-670`, folded into every reputation read at
  `D/XRL/World/Reputation.cs:142-150`) — the game's own record of which factions admire or fear
  which bodies. Vanilla scores `MassMind` at **-200** for the Seekers of the Sightless Way
  (`B/Factions.xml:1397`) and `Wings` at **+300** for the birds (`:362`). **The sign is the whole
  judgement; this mod has no opinion of its own about any mutation.**
- **`Faction.Interests`** with the tag `cybernetics`, and vanilla's `Inverse` flag for a faction
  that defines itself *against* a thing — the Putus Templar list it both ways
  (`B/Factions.xml:1271-1272`), so the inverted reading wins.

A reaction is a **line**: a push in a named settler's mouth (`VoiceOccasion.FounderRegarded`) and a
clause in the chronicle. It never changes standing, refuses a settler, or alters production. Said
once per state-change, where a change is a different creed, part, sign, or chrome. A creed another
mod ships is answered correctly the day it loads, because that mod already filled those fields in
for its own reasons.

**Legendary notables (lane 5) — the narrowest viable slice of the hero machinery.** The engine's
hero machinery is `XRL.World.HeroMaker` (`D/XRL/World/HeroMaker.cs:8`), and `MakeHero` is cheap to
call — no zone, no cell, no `Render`, no worldgen. **It is not free.** Unconditionally it adds +1 to
all six stats, doubles hit points, multiplies level by 1.5, rolls **zero to four random mutations**,
and replaces `GivesRep`, which then rolls `1d3` random loved and hated factions
(`D/XRL/World/Parts/GivesRep.cs:271-299`). Those exist to make a village mayor something an
adventurer might have to fight; the founder's water-keeper is not that, and turning a settler the
player housed into a doubled-hit-point mutant with faction grudges the realm never chose would be a
mechanic where Addendum 13 asked for a name.

So `KingdomNotables` calls exactly `HeroMaker`'s own naming block
(`D/XRL/World/HeroMaker.cs:182-230`): `NameMaker.MakeHonorific` / `MakeEpithet`
(`D/XRL/Names/NameMaker.cs:24, 29`) under vanilla's `Special="Mayor"` scope
(`B/Naming.xml:4207-4420`), attached through the `Honorifics` and `Epithets` parts. `NameMaker` is
a pure static over `Naming.xml`: it mutates nothing, needs no game, and is mod-extensible by the
same key vanilla threads through every one of its own offices.

| Member | Contract |
|---|---|
| `KingdomNotables.Mint(KingdomSystem, GameObject)` | Gives the office holder an epithet and an honorific, once. Idempotent by `KingdomEpithet`. Returns empty when `Naming.xml` has no style that fits, which is not a failure. |
| `KingdomNotables.HolderName(KingdomSystem)` | The office holder as a happening should name them, read off `KingdomCityBook.OfficeEpithet` so it works while the body is on disk. |
| `KingdomNotables.OfficeNameScope` | `"Mayor"` — vanilla's own scope, borrowed rather than declared, so a mod extending `Mayor` extends ours. |

## `KingdomChronicle` — history

| Member | Contract |
|---|---|
| `static void Record(KingdomSystem, string text, bool accomplishment = false)` | Writes to both registers, dated. `text` is a lower-case clause with no trailing period, written from the founder's perspective. Pass `accomplishment: true` only for milestones. |

## `KingdomData` — the content registries

| Member | Contract |
|---|---|
| `static List<BuildEntry> Buildings` | All registered building designs, base plus third-party. |
| `static bool TryGetBuilding(string key, out BuildEntry)` | Look up one design. |
| `static List<DealEntry> Deals` | All registered trade charters. |
| `static bool TryGetDeal(string key, out DealEntry)` | Look up one charter. |
| `static List<string> Styles` | Canonical names from the live merged style registry, including third-party styles. |
| `static bool TryGetStyle(string name, out string canonical)` | Case-insensitive lookup in the live style registry. Use this instead of `KingdomRules.IsKnownStyle`, which covers only the five built-in compatibility keys. |
| `static string StyleForSite(string terrainBlueprint, string regionName, int zLevel)` | Resolve a founding site through the merged style selectors. Exact terrain evidence outranks region evidence; priority and declaration order break ties; an unmapped site resolves to `common`. |
| `static string StyleGroundClause(string style)` | Return the merged founder-facing ground clause, with a safe generic fallback. |
| `static string CropForStyle(string style)` | Return the style's declared crop, inheriting the `common` declaration for a selector-only legacy style. |
| `static string SeedForStyle(string style)` | Return the style's declared seed, with the same `common` fallback. |
| `static string CropRowForStyle(string style)` | Return the style's declared standing-row blueprint, with the same `common` fallback. |
| `static string CropForSeed(string seedBlueprint)` | Reverse the merged style mapping; null for an unregistered seed. |
| `static string SeedForCrop(string cropBlueprint)` | Reverse the merged style mapping; null for an unregistered crop. |
| `static string RowForCrop(string cropBlueprint)` | Return the registered standing-row blueprint; null for an unregistered crop. |
| `static bool TryStyleWallMaterial(string style, out KingdomMaterial material)` | Read a style's optional preferred wall material. Stock remains authoritative. |
| `static string TimberWallForStyle(string style)` | Return the style's timber wall blueprint, inheriting `common` when omitted. |
| `static void Reload()` | Re-read every registry. Called on game load; call it if you inject entries at runtime. |

## `KingdomXmlSchemaRules` — public registry format boundary

All six mergeable public registry roots declare `Schema="1"`: buildings, deals, yard works,
research, procedures, and raid profiles. `CurrentVersion` is `1`; `Judge` returns
`Compatible`, `LegacyUnversioned`, `Unsupported`, or `Malformed`, and `IsReadable` admits only the
first two. An absent attribute is the bounded backward-compatibility path for files written before
explicit versioning. A present noncanonical, malformed, or unsupported value rejects that entire
stream before any child can register; one bad stream cannot half-merge. Merge-by-key remains the
compatibility mechanism within a readable schema.

## How time is charged — the clock substrate

Every periodic system in this mod reads the same two calls, and none of them caps elapsed time.
`MaxUpkeepDaysCharged` and its successor holding pen `LegacyAbsenceCap` are both **retired**: an
absence of any length is charged in full, and what bounds the loss is subsidence toward the level
the works carry, never forgiveness. Serialization version 3 is the first version written under
this rule, and `KingdomSystem.Read` refuses an older layout by name rather than migrating it.

| Member | Contract |
|---|---|
| `static int ElapsedDays(long elapsedTicks)` | Whole days in a stretch, over `Simulation.Kernel.TickMath`. Saturating, never negative, never capped. |
| `static long AdvanceCheckpoint(long previousTick, long currentTick)` | The new checkpoint after charging: the previous one plus the whole days consumed, so the remainder is kept and never rounded away. Never re-anchors to now — that would forgive the remainder. |
| `static int ActivityDays(int days, int effectivenessPercent)` | Days scaled by how hard a thing was actually run. The labour term of Addendum 8 clause 2: idle days are not activity days. |
| `static long LabouredTicks(long elapsedTicks, int effectivenessPercent)` | The same idea in ticks, for callers spending a tick budget. |
| `const int ReserveDays` | 3. A cushion **depth** in days of upkeep, kept in hand before the settlement spends water on a planting, an upgrade or a manifest. A quantity, never a clock — it does not and must not bound elapsed time. |
| `static long RestampDeadline(long deadlineTick, long nowTick, long leadTicks, int witnessGraceDays)` | Where a repeating or one-shot deadline stands the moment the founder walks in on it: unchanged if not yet overrun or the overrun is still inside the witness grace band, otherwise pushed out to a fresh full window from now. Nothing is forgiven and nothing is banked — only the moment it lands moves. The one helper the manifest, the raid warning and the arrival queue all read instead of keeping their own copy. |
| `readonly struct Passages` / `static Passages PassagesThrough(long dueTick, long nowTick, long intervalTicks, long patienceTicks)` | Runs a repeating arrival clock forward over however long nobody was looking. `Departed` is how many turns came due and ran out of patience unwitnessed; `StandingSince` is the tick of the one still at the gate, at most one because an existing visitor blocks the next; `LastDepartedTick` dates the most recent departure for a caller telling the news. |
| `const int RaidWitnessGraceDays` | 1. Whole days past a raid's due tick the founder still counts as having been there to meet it, fed into `RestampDeadline` — raiders who arrive within the day still find somebody home; raiders who arrive a season early do not resolve in the dark. |

**Writing a periodic system against this**: read the elapsed with `ElapsedDays`, plant the
checkpoint before the first count if it is unset (an unplanted stamp reads as the age of the
world), gate the work on a labour term, spend the budget, and advance with `AdvanceCheckpoint`.
`KingdomWear.AdvanceRepair` is the reference shape.

## `KingdomSubsidenceRules` / `KingdomSubsidence` — the level, and settling back to it

Pure rules plus one engine-facing caller. `KingdomSubsidence.Supports(survey)` sums the
physically embodied benefit reading over every `KingdomBuilt` work in the zone — each reading
clamped by the catalogue's `Carries` ceiling and scaled by
`KingdomWearRules.WorkEffectiveness` (Addendum 10(b): a crewed work by its crew stretch reduced
again by condition, a staffless one by condition alone), plus the `Shades` of whatever yard trade
each household has taken up. `SupportedLevel(tally, stage, shade)` hands only water, roof, and
bounded lift to `KingdomCatalogueRules.PopulationEquilibrium`. Food remains visible physical-lane
metadata but never changes population support, binding cause, or subsidence.

`KingdomSubsidence.ScopedSupports(system, zone, survey)` is the same tally with **one** difference
and it is the one the level reads: `SupportTally.Lift` is scoped by reach (Addendum 6). Each
work's lift lands in proportion to the settlement's roofs it covers
(`KingdomReachRules.Landed`), the headed great works of the realm's other claimed zones arrive
whole out of `KingdomReach.CityShadeExcept`, and water/roof remain untouched citywide pools. Food
may still aggregate as compatibility/informational metadata, but it is not a live binding.
`Supports` remains the right call for a caller asking what the works make rather than what the
settlement holds — the water-works production pass is one.

**A city, not a zone.** `KingdomSubsidence.Reckon` writes down what THIS zone is holding —
`RecordZone`, into the seated settlement's city book, dated in whole days (`SeenStamp`) — and then folds
in every OTHER zone the seated city claims **as it was last seen** (`OtherZones`), never simulated
forward: a granary zone the founder hasn't walked into since spring goes on reporting spring's
granary until they walk back in. `CityTally` sums water, roof, and compatibility food metadata this
way (`Lift` passes
through unchanged — `ScopedSupports` has already summed it across the city through
`KingdomReach.CityShadeExcept`, Addendum 6); `CityStorage`/`CityStorageCapacity` does the same for
dedicated storage, which is what the stage ladder reads, so a city whose casks stand in the zone
next door is measured against all of them rather than demoting itself the moment the founder walks
in through the wrong side. `KingdomGrowth.UpdateStage` reads city storage **after** `Reckon` plants
this zone's own sighting, so this zone is never counted twice and never counted out. A reading that
folds in another zone's memory is dated for the founder — `SightingClause` — and
`KingdomReports.Status(KingdomSystem, Zone Z = null)` now takes the zone the pass is standing in and
appends it, shaded, right after the level: "carries 26  {{K|counting one parasang as you last saw
it 6 days ago}}".

| Member | Contract |
|---|---|
| `struct ZoneSighting` | One claimed zone's last-seen binding carries and storage: `Water`, `Food`, `Roof`, `StorageCapacity`, `SeenTick`. `Seen` is false — and never folded in — for a zone nobody has ever stood in. |
| `static SupportTally CityTally(SupportTally here, IList<ZoneSighting> others)` | This zone's tally plus every OTHER claimed zone's water/food/roof as last seen. |
| `static int CityStorage(int here, IList<ZoneSighting> others)` | The same, for dedicated storage. |
| `static long OldestSighting(IList<ZoneSighting> others)` / `static int SightedZones(IList<ZoneSighting> others)` | The oldest folded-in sighting's tick (zero if every claimed zone was counted today), and how many zones were folded in out of a sighting at all rather than counted from the ground. |
| `static string SightingClause(int zones, int days)` | The clause that dates a city reading, or null when there is nothing to date — a one-zone city, or one whose every zone was walked today. |
| `static void RecordZone(KingdomSystem, Zone, SupportTally, int storageCapacity, long timeTicks)` (on `KingdomSubsidence`) | The writer — rewritten from the ground every pass the zone is stood in, including down to zero. Since the city book landed it writes a zone row of `KingdomSettlement.City`; the `r_TAF_Supports_*` game-state key family it used to write is retired. |
| `static int SeenStamp(long timeTicks)` (on `KingdomSubsidence`) | The tick a sighting is dated in: whole days, clamped, because a day is the granularity everything downstream reads. |
| `static List<ZoneSighting> OtherZones(KingdomSystem, Zone)` (on `KingdomSubsidence`) | Every claimed zone of the seated city EXCEPT the one the pass is in, as each was last seen. |
| `static int CityStorageCapacity(KingdomSystem, Zone, int here)` (on `KingdomSubsidence`) | `CityStorage` fed from `OtherZones`. |
| `static string SightingClause(KingdomSystem, Zone, long timeTicks)` (on `KingdomSubsidence`) | The dated clause for THIS reading, ready for the status report. |

| Member | Contract |
|---|---|
| `SupportWater` / `SupportFood` / `SupportRoof` / `SupportWealth`; `BindingSupports` / `PopulationBindingSupports` / `LiftingSupports` (on `KingdomCatalogueRules`) | Frozen authoring vocabulary and stable presentation order retain `BindingSupports = water, food, roof` for source/XML compatibility. Live `PopulationBindingSupports = water, roof`; food is physical-lane metadata and cannot author pressure or subsidence. Lifts are `craft`, `spirit`, `learning`, `order`, `luxury`, `wealth`. A third-party unknown support is still accepted as a lift after these known kinds. Naming `wealth` changes diagnostics/order only: it enters the same capped lift arithmetic it already used as an unknown kind and never keys threats. |
| `static int Equilibrium(int water, int food, int roof, int lift, int shade)` / `PopulationEquilibrium(int water, int roof, int lift, int shade)` (on `KingdomCatalogueRules`) | `Equilibrium` is the frozen three-axis arithmetic retained for compatible callers. Live population calls `PopulationEquilibrium`, which reuses the same floor/lift-cap/overflow law with food held non-binding. Its base is the least of water and roof; `lift + shade` is capped at `LiftCapPercent` of that least. Each lift half is floored at zero before addition, so neither can eat the other and an unmet taste is never a penalty. |
| `static SupportTally FoldShade(SupportTally, List<KindAmount>, int percent)` (on `KingdomCatalogueRules`) | `FoldWork` without the work count, for a contribution that stands in somebody else's plot — a household's yard trade. |
| `static int LevelFromWater(int water, GrowthStage stage)` | Declared `water` is denominated at **camp rates**; this divides by `StageUpkeepPercent` before the equilibrium sees it. A design carrying eight in a camp carries three in a city. Since Addendum 11(a) only *producers* declare `water` — a cistern, a reservoir and the waterworks hold and carry nothing, because the same figure is also banked as a real daily flow (`LastWaterWorkTick`, above) and a vessel declaring it would be conjuring what it claims to store. A producer's figure is `KingdomRules.TicksPerDay / mean(VariableRate)` of the vanilla `LiquidProducer` on its own blueprint. |
| `static int SupportedLevel(SupportTally, GrowthStage, int shade = 0)` / `BindingSupportFor(...)` | Live population level from stage-adjusted water, roofs, and bounded lift; its binding answer is only `water` or `roof`. `SupportTally.Food` is ignored here. `shade` is `KingdomSystem.NotableShade`; it defaults to none because a settlement that has named nobody honestly has none. |
| `static Trajectory Slide(..., bool alreadySliding, int shade = 0)` | Carries the same shade through every step, so a slide converges on the level the founder was actually told. |
| `const int StartMarginPercent` / `static int SlideBeginsAbove(int level)` / `IsSubsiding` / `HasArrived` | The 20% band. A settlement inside it never moves; the slide stops the moment it arrives. |
| `const int StageFallMarginPercent` / `static GrowthStage StageWithHysteresis(...)` / `SettledStage(...)` | The ratchet, both ways. One rung per reckoning down, on a clear shortfall only, `Camp` an absolute floor. |
| `const int StepDays` / `SettlersPerStep(GrowthStage)` / `const int MaxSteps` / `struct Breakpoint` / `struct Trajectory` / `static Trajectory Slide(...)` | Closed-form convergence: the whole slide is computed at once from the elapsed, and its rung changes come back as dated breakpoints for the chronicle. |
| `const int RuinChancePercent` / `static int RuinChanceFor(GrowthStage from)` / `static int RuinIncrement(int roll)` / `RollRuin(..., GrowthStage from)` / `RolledRuinIncrement(...)` | What a lost rung does to standing works: **damage, never deletion**, bounded by `KingdomMaterialRules.MaxWearPercent`. No quota (Addendum 10(c)): every standing work is asked once, independently, at `RuinChanceFor(from)` — the LOST rung's own reach out of the widest there is (Camp 10% up to City 50%), so a wider rung reaches a strict superset of what a narrower one would, regardless of which work stood where. Drawn through the kernel's counter-random so a reload never re-rolls a collapse the chronicle already described. Player-placed objects are never touched. |
| `const int NamedRuinsPerBreakpoint` / `TellsRuin(int index)` / `RuinedWorkLine(...)` / `RuinSummary(...)` | The ruins of one rung, told the way its departures are: one named by line, the rest carried in a summary that counts them and names the worst wear reached — so a rung that leaves a dozen works the worse for it spends two chronicle entries, not a dozen. |
| `KingdomMaterialRules.ConditionAdjective(int wear)` / `ConditionLook(int wear)` / `const int BadlyUsedWearPercent` / `HalfWreckedWearPercent` | The reach rule's presentation half (Addendum 10(c)): a worn work's own NAME carries an adjective — `battered` / `half-ruined` / `ruined`, null for a sound work — on the same thresholds `ConditionWord` reads, and `ConditionLook` gives the sentence a founder reads standing in front of it. So a settlement that fell reads as a field of ruins, not pristine buildings with quiet arithmetic against them, and mending walks the name back down the same ladder it climbed. |
| `static string BeganNote / BeganChronicle / ArrestedNote / ArrestedChronicle / BreakpointChronicle / DepartureCause` | The prose. A slide announces once at awareness and unsays itself when arrested (STANDARDS 7b). |
| `const int NamedDeparturesPerSlide` / `TellsDeparture(int index, int departed)` / `NamedDepartures(int)` / `SlideDepartureSummary(...)` / `ChronicleEntriesFor(int departed, int rungs)` / `const int ChronicleBudgetPerSlide` | The chronicle's own budget. A long slide is a hundred small departures; the record keeps the first few by name, the last by name, and one line for everybody in between, so a City→Camp collapse cannot eat the register. Hold `ChronicleEntriesFor` against the budget in your own tests if you extend this. |

The slide runs on **world time** and would run identically under the founder's nose. What a
homecoming changes is that somebody is told. Turn the whole of it off with
`r_TAF_OptionSubsidence`.

## `KingdomBrinkRules` / `KingdomBrink` / `KingdomWord` — the last arrestable window

One shape for every irreversible consequence in the mod: a settler with nowhere to live
(`BrinkKind.Roof`), a settler one window short of another creed (`Creed`), and a realm whose two
cities have quarrelled to the breaking point (`City`). The resented-creed departure shares the
`Creed` window through `KingdomConversionRules.ResentedWindowDays`.

Five rules — Addendum 8 clause 3 as moderated by Addendum 10(a), *awareness is pushed*:

1. **Reaching the threshold does not fire it.** The accrual records who, what caused it, and the
   tick it was reached, and then **stops** (`HoldAtBrink`). A thousand-day absence and a ten-day
   absence arrive at the same place, because there is nowhere past the brink to arrive at.
2. **The pressure is a fact, re-derived every pass.** A brink whose cause has lifted is removed
   and its accrual restarts from nothing — so the window is arrested by *acting*, never by
   waiting, at any point up to the moment it fires.
3. **Word is pushed at the crossing, once, dated, and it coaches.** `KingdomWord` sends the
   warning to the founder wherever they stand, files it in the ledger's brink lane, and dates it
   in the chronicle. The line always names the **arrest** (`ArrestNote`), never only the doom.
   Standing in the city the news is about, the founder gets the plain announcement; anywhere else
   it arrives framed as `WordFrom` — word out of a named city, finding them. One line either way.
4. **The window runs in world-days from that delivery** (`WindowDays`), not in attended passes.
   Each length is its old attended-pass rope times `CohabitationDaysPerAttendedPass`, so a founder
   who comes home every third day walks exactly the road they always walked.
5. **Window spent with the cause standing → the consequence fires, attended or not.** The passes
   run on zone activation, so in practice the founder returns to find it **has happened**, at
   `ExpiryTick`, and the aftermath is dated to that tick (`FiredClause` / `FiredNote`) rather than
   to the homecoming. **Nothing irreversible ever fires unwarned**: `WindowSpent` is false for a
   brink at `Unwarned`, however old it is.

| Member | Contract |
|---|---|
| `enum BrinkKind` | `Roof = 1`, `Creed = 2`, `City = 3`. |
| `const int RoofBrinkWindowDays` / `CreedBrinkWindowDays` / `CityBrinkWindowDays` / `static int WindowDays(BrinkKind)` | 6 / 18 / 9 **world-days**, counted from the warning. |
| `const int RoofBrinkWindowPasses` / `CreedBrinkWindowPasses` / `CityBrinkWindowPasses` / `static int WindowPasses(BrinkKind)` | 2 / 6 / 3 — the pre-Addendum-10(a) ropes, kept as the INPUT to the derivation so each window shows its working. |
| `const int CohabitationDaysPerAttendedPass` / `static int InCohabitationDays(int passes)` | 3. The one exchange rate every migrated counter and every window uses — the retired forgiveness cap's honest successor. Thresholds calibrated in visits were scaled by exactly this, so an attentive founder's road is unchanged. |
| `const long Unwarned` / `static bool Warned(long warnedTick)` | Zero, and the only unwarned marker. A brink at `Unwarned` has no deadline. |
| `static bool WindowSpent(BrinkKind, long warnedTick, long nowTick)` / `long ExpiryTick(BrinkKind, long warnedTick)` / `int DaysLeft(...)` / `int DaysSinceWarning(...)` | The window, on the world's clock. `ExpiryTick` is the day it happens and the day the aftermath is dated to. |
| `static int HoldAtBrink(int value, int threshold)` | Rule 1 as arithmetic. Overflow past the line is discarded, never banked: a banked overflow is a debt the founder cannot see and cannot pay. |
| `static long CrossingTick(long startTick, long nowTick, int standing, int threshold, int perDay)` | When a steady per-day accrual actually crossed, on the day boundary rather than on the pass somebody noticed. Clamped to now. |
| `static int DaysStood(long reachedTick, long nowTick)` / `int DayNumber(long tick)` | The honest elapsed, uncapped; and the floored world-day, for the one counter that must live in an `int` store. |
| `static string ElapsedPhrase / WindowPhrase / ArrestNote / AnnounceNote / AnnounceTelling / LiftedNote / FiredPhrase / FiredClause / FiredNote / WordFrom` | The prose, all three surfaces. |
| `KingdomBrink.Of / Stands / Record / MarkWarned / Lift / WindowSpent` (per-settler) and `OfCity / CityStands / RecordCity / MarkCityWarned / LiftCity / CityWindowSpent` | The engine side. Per-settler brinks live in the settler's **resident row**, under their own `KingdomResidentId` (W2 — a frozen object's properties are unreachable, so a window kept there could not run while a zone was on disk). Still a fact about one person, still impossible for a seat swap to carry to the wrong city: the row travels with the book of the city whose roll they are on. The realm's brink lives in `IntGameState` / `StringGameState`. |
| `KingdomWord.StandsIn(Zone)` / `Warn(...)` / `Unsay(...)` / `Aftermath(...)` | The one push channel. Every brink speaks through it; nothing builds a second one. |

## Authored architecture identity context

Architecture identity is an additive selector surface in the schema-1 data lane, not a public
behavior callback. `ArchitectureSelector` carries existing `Styles`, `Creeds`, `Terrains`,
`Strata`, stage, and technology constraints plus set-valued `Cultures`, `Species`, `Genotypes`, and
`Bodies`. `ArchitectureSelectionContext` receives canonical, sorted, bounded positive facts from
the seated city's existing resident tallies. Set matching means any named positive may match, while
any matching explicit exclusion refuses. Variant choice remains deterministic: priority,
specificity, then ordinal key.

`KingdomResidentIdentityRules.BuiltInIdentityKeys` stores genotype and three vanilla-derived body
conditions in the existing exact per-body identity receipt: `body:robot`, `body:wet-bodied`
(aquatic and not flying), and `body:broad-bodied` (the `Gigantic` tag/property). Extension-owned
keys remain in their own namespaces in the same bounded tally. Culture/species still use their
separate live per-city count dictionaries, preserving Addendum 17's knowledge/body split.

`KingdomArchitectureRuntime.TrySelectionContext` is the only engine projection. Once
`TryPrepare`/`TryFreeze` records the selected variant's full snapshot, later resident changes do not
reselect or repaint the standing work, including during an in-place tier successor: the next tier
must contain the exact frozen `VariantKey` and retain its stateful placements. Maps/palettes remain subject to exact-lot, material,
technology, knowledge, power, topology, and protected-state checks. See
[MODDING.md](../MODDING.md#identity-aware-architecture-variants) for XML semantics and exact shipped
coverage; absence from that bounded list means fallback, not claimed handcrafted support.

## `KingdomRules` — pure rules (no engine dependencies)

Deterministic, side-effect-free, and fully unit-tested; safe to call from anywhere,
including your own tests. Notable members: `SpilloverDelta`, `UpkeepForElapsed`,
`ElapsedDays`, `AdvanceCheckpoint`, `ActivityDays`, `LabouredTicks`,
`StageFor`, `FetchableDrams`, `ResolveThirst`, `RationsPerDay`, `RationsForElapsed`,
legacy-neutral `ForagedRations`/`ResolveHunger`, water-only `ComposeScarcity`, `RaidSize`, `StyleAllows`, `DistrictName`,
`ZonesAdjacent`, `ComposeOutsider`, `ToThirdPerson`, plus the `BuildEntry` / `DealEntry`
records and their `TryParse*` validators.

District effects are `District*(string district)` for one district and `Districts*(IEnumerable<string> districts)`
for a whole kingdom's claimed ground: `DefenceBonus`, `UpkeepPercent`, `ShopTierBonus`,
`BuildPercent`, `PetitionIntervalPercent`, `DriftPercent`. `Styles`, `IsKnownStyle`, and
`StyleForSite(terrainBlueprint, regionName, zLevel)` are the five built-in compatibility surface.
New work must use the open `KingdomData` style registry above. `ProvokableFactions` and
`RaiderTableFor` are the five built-in compatibility table; live raids use the mergeable,
validated `KingdomRaidProfiles.xml` registry documented in `MODDING.md`.

Pre-release retired adapters are public only for already-compiled binary callers and are not
supported authoring surfaces: `KingdomRules.MaxBuildings` (use `MaxBuildingsForStage`),
`KingdomRules.TryAddSkin` (use keyed `KingdomMergeRules.TryMergeSkin`), and the flat cohabitation
members named above. Each carries `[Obsolete(..., true)]`; none has a live runtime caller.

## `ThousandAndFirst.Api` — the published extension contract (API version 3)

The behaviour lane. Opened at W5, after four waves shaped it, and dogfooded from the first commit:
the city's own asks go through `IKingdomAskSource` exactly as a third party's do. Worked examples,
the registration recipe, and the invariants are in [MODDING.md](../MODDING.md) — this section is
the surface list.

Option gate: `r_TAF_OptionExtensions`, default `Yes`. Off disables third-party C# only; the XML
data lane is unaffected. Durable namespaces and draw streams derive from the owning mod's immutable
manifest `id`, lowercased/slugged—not its mutable, non-unique display title. Changing a published
manifest `id` is therefore a save-breaking owner change; changing only the title is not. If two
installed manifest IDs collapse to the same bounded slug, every extension owned by both IDs is
refused by name; load order never chooses an owner for shared durable state.

| Member | Contract |
|---|---|
| `KingdomApiRules.Version` | The published version. `3`. Checked at registration; unsupported drift is refused by mod name. |
| `KingdomApiRules.MinSupportedVersion` | The oldest version still admitted. `1`. Moving it is a breaking change, and it is what makes STANDARDS §9's one-minor-cycle promise keepable. |
| `KingdomApiRules.BehaviourVersion` | First version containing durable resource, carrier/job, network, and work contracts. `3`; a type implementing any of them cannot declare v1/v2. |
| `[KingdomExtension]` | Marker attribute. The class needs a public parameterless constructor. |
| `IKingdomExtension.ApiVersion` | What the extension was built against. Return the constant, never a literal. |
| `IKingdomAskSource.Ask(city, draws)` | Returns asks for the Charter's asks board. Null for none; at most `KingdomApiRules.MaxAsksPerSource` kept. |
| `IKingdomHappeningSource.Happen(city, sinceTick, draws)` / `IHappeningGenerator` | Returns dated notices for the chronicle and word surface. `IHappeningGenerator` is the canonical §6.6 name and inherits the compatible v1 contract. Each immutable manifest-ID/exact-assembly/type tuple owns a durable cursor and receives `0` on its first logical call; an upgraded source already called through the retired aggregate lane starts at that retained legacy tick. Later windows are `(sinceTick, nowTick]`, and a fault advances only that source. At most 128 active source types run per city and `MaxNoticesPerSource` notices are kept. |
| `IKingdomIdentitySource.Keys(identity)` | Returns extra live roster keys for one frozen identity. At most `MaxIdentityKeysPerSource` valid distinct keys survive from the first `MaxIdentityKeyCandidatesPerSource` slots. Unqualified keys are filed under the owning mod; foreign namespaces are dropped. A fault contributes none, is logged, and is surfaced on screen once per owning mod and lane. |
| `IKingdomIdentitySource.Affinity(identity, workKind)` | Returns an existing work lane's percent; 100 is neutral. Per-source answers are clamped, their deltas summed, then the composed answer is clamped to 70–130. A fault is neutral, logged, and surfaced on screen once per owning mod and lane. No tier surface exists. |
| `KingdomIdentityReading` | Frozen bounded `(Culture, Species, Creed, Genotype)` projection. Exact Qud open-string identity, with no creature or city reference. |
| `IResourceKind.Resources(city, model, draws)` | Declares extension-owned resource rows with unit, capacity, optional dedicated-container property, network key, and liquid id. Level is durable and owner-qualified; four rows per owner, sixteen per city. |
| `ICarrierKind.Carriers(city, model, draws)` | Declares owner-local carrier blueprint, pace, and capacity for the current pass. A job freezes those values when it opens, so disabling or changing the source cannot rewrite a journey in flight. |
| `IJobKind.Jobs(city, model, draws)` | Opens exact-tick jobs paired to one carrier and one owned resource. A key permanently identifies one logical job: retries are idempotent while its open/recent terminal receipt is retained, but a retired key reused later is a new proposal and is forbidden by contract. Cargo reserves once; up to six held-zone legs determine duration; up to four completion changes commit atomically. Four open jobs and four recent terminal receipts per owner; sixteen of each city-wide. |
| `INetworkKind.Networks(city, model, draws)` | Declares generic held-zone topology: up to eight source/sink/relay nodes and twelve capacity edges per network. The host solves lower-numbered priority first, records flow/brownout, and integrates daily surplus into the owned resource. Four networks per owner, sixteen per city. |
| `IWorkBehaviour.Advance(city, model, draws)` | Advances opaque state on an exact existing `WorkId` to a strictly later breakpoint, with up to four atomic owned-resource changes and one explicit materialisation debt. An attended settlement pass lands at most one exact takeable, non-creature Qud object on that work's cell and only then acknowledges the debt. A generation receipt reconciles interruption between those two acts without replaying a stale object. Sixteen rows per owner, sixty-four per city. |
| `KingdomResourceDefinition` / `KingdomCarrierDefinition` / `KingdomJobPlan` / `KingdomNetworkPlan` / `KingdomWorkAdvance` | Frozen proposal values. Every supplied array is copied; bounded `Try*` accessors expose nested legs, changes, nodes, edges, and materialisations. |
| `KingdomBehaviourReading` and its resource/job/network/work rows | Frozen durable projection, exposed as `KingdomCityReading.Behaviour`. It includes bounded terminal job receipts and outstanding physical debt, but no mutable model or engine object. |
| `IKingdomDraws.TryBetween(lane, ordinal, low, high, out value)` | The kernel, keyed on `taf:ext:<manifest-id>:<lane>`. Deterministic across reloads and display-title changes. Returns false rather than substituting a different stream. |
| `KingdomCityReading` | Frozen projection of one city's book: stocks, `Behaviour`, and `TryZone` / `TryWork` / `TryResident` over copied rows. No setters, no route to the ground. Its original constructor remains for v1/v2 binary compatibility. |
| `KingdomZoneReading` / `KingdomWorkReading` / `KingdomResidentReading` / `KingdomStockReading` | The row projections. |
| `KingdomWorkClass` / `KingdomDayPlace` / `KingdomRollStanding` | Published vocabularies, MAPPED from the model's own rather than cast, so a model-side insertion cannot renumber them. `Construction` is appended as class 6; its row publishes exact resident-derived crew while the construction receipt remains progress authority. |
| `KingdomAsk` / `KingdomAskWeight` | One thing the city wants: kind, title, what would settle it, where, how badly. Kind/title/want are stripped and clamped; a `ZoneId` the city does not hold is read as none; an undefined weight is read as `Passing`, never `Grave`. |
| `KingdomNotice` | One dated thing that happened: kind, tick, chronicle telling, optional spoken line. **No place field** — neither surface a notice reaches takes a zone, so one would be a published input that went nowhere. |
| `KingdomExtensionVerdict` / `KingdomApiRules.Judge` / `.RefusalLine` / `.TryStream` / `.Slug` / `.Trim` / `.Kind` / identity clamps | Registration judgment and bounded hostile-input rules, pure and testable. |
| `KingdomExtensions.Version` / `.Enabled` / `.Admitted()` / `.Refusals()` | The registry, from outside. |

**Durable sidecar, not a decorative interface.** The four row-owning dimensions advance beside the
ordinary city model during attended check-in and unattended heartbeat. Resource levels, frozen
jobs, latest network solves, work states, and owed objects are encoded as one canonical bounded
sidecar in `KingdomCityBook`; nested settlement archive schema 7 carries it across exile and seat
exchange. Old archive writers remain byte-frozen and v1/v2 callbacks remain admitted. A malformed
sidecar is retained and named rather than reset to empty. In-flight jobs can complete from their
frozen receipt after the owning mod is disabled. Carrier blueprints are retained as job identity;
their v3 journey is resolved model-side, while work materialisations are the physical production
edge. Current decoded sidecar may not exceed 16,896 bytes. Legacy v1 input remains capped at
16,384 bytes; current bound adds exact worst-case headroom for one v2 generation receipt on all 64
work rows, so every valid legacy carrier can be rewritten without losing authority.

**Isolation.** Every extension call crosses `KingdomExecutor.Submit`: frozen reading in, frozen
result out, timed against the reckon lane. A source that throws or overruns its lane's budget
stalls its own job — no city state is published, the turn is unaffected, the failure is logged by
mod name, and every other extension still runs. Ask-source faults are additionally named on the
asks board; identity-key and identity-affinity faults are surfaced on screen once per owning mod
and lane, while the affected source contributes no keys or neutral affinity. Each callback gets at
most 32 kernel-draw attempts and each returned behaviour array inspects at most its first 32 slots;
malformed slots consume that bound. The thirty-third draw refuses that callback's publication as
over-budget. Per-owner and city caps above apply to durable rows, including bounded terminal job
receipts. **The budget is a
verdict, not a timeout**: the seam is synchronous, so it can refuse to publish a result that
overran but cannot interrupt one — an infinite loop in a third-party source still hangs the game,
exactly as one in ours would. Discovery uses the engine's cached attribute scan
(`ModManager.GetTypesWithAttribute`); construction is per-type and guarded, because the engine's
combined `GetInstancesWithAttribute` would let one class with no default constructor take down
every mod's extension at once.

## Reading surfaces — The city in full

All three live under Charter → **The city in full** (`c`) and are **readings**. Nothing on any of
them can be pressed; the verbs that answer them are the Charter's own.

| Entry | Route | Member |
|---|---|---|
| The book of the city | `c` → `b` | `ThousandAndFirst.Simulation.City.KingdomBookReport.Open(system)` — six chapters: the stores and what holds them, the works and what they wait on, the people and where their day puts them, the turn of the year, what has happened here, and who else writes in this book. |
| Where the keepers' craft could go | `c` → `k` | `ThousandAndFirst.KingdomTechMap.Draw(system)` — what each thing the keepers know opened, the nearest locked designs and what is in the way of each, and the ways of learning this city has never walked. A map, never a spend: gated on `r_TAF_OptionZoning`. |
| What the city is asking for | `c` → `a` | `ThousandAndFirst.KingdomAsks.Board(system)` — the standing petition, the city's own model-derived asks, and every extension's, worst first. Gated on `r_TAF_OptionPetitions`. |

## `KingdomCitizenRite` — your own settlers will share water with you

Lane 1 of the feel lanes. A citizen of the realm standing on claimed ground is made a host of
**vanilla's own water ritual**: `GivesRep` (which is the whole of what `WaterRitualChoice` tests),
and a greeting for a settler who had no conversation at all. Everything the rite then gives comes
off the runtime faction the founding mints — its reputation, and its `WaterRitualRecipe`, which
`KingdomDish` has been stamping since the food lane and which, until now, no living creature in Qud
belonged to the faction to hand over.

| Member | Contract |
|---|---|
| `KingdomCitizenRite.Enabled` | Reads `r_TAF_OptionCitizenRite`, default `Yes`. Its own gate, not the inward rite's. |
| `KingdomCitizenRite.Host(system, citizen)` | Makes one citizen a host; returns the `CitizenRiteVerdict` that stopped it. Idempotent, and repairs itself — the condition asked is the object's actual state, not a remembered flag. |
| `KingdomCitizenRite.OnSettlementPass(system, zone)` | Every citizen on this ground, once per pass. Called from `KingdomWaterRite.OnSettlementPass`, above that channel's own gate. |
| `KingdomCitizenRite.HostProperty` (`KingdomRiteHost`) | Int property marking a settler already made a host. |
| `CitizenRiteVerdict` | `Host`, `Unfounded`, `NotCitizen`, `NoBody`, `UnknownFaction`, `UnknownLiquid`. The last two are the engine's two documented hard failures, refused before the conversation can open, and reported once. |
| `KingdomCitizenRiteRules.TryTradableSecret(faction, outsiderLine, out id, out text)` / `SecretTags()` / `SecretCategory` | **W6: the chronicle becomes something you can trade.** One `JournalObservation` per settlement pass, carrying the **outsider register's** wording — what the roads say about your city, not what your own book says — tagged `gossip` and `settlement`, which are *vanilla's own* interest tags (seventeen shipped factions declare an interest in `settlement`, five in `gossip`). Everything after that is vanilla: `IWaterRitualSecretPart.ShuffleNotes` puts it in the ritual's bag, `Faction.GetInterestIn` decides who wants it, `WaterRitualSellSecret` pays for it. Filed **revealed**, so vanilla's own `CanSell`/`CanBuy` make it sellable and never buyable — you can tell the world about your city and nobody can sell it back to you. The id is derived from the realm and the words, so re-filing after a reload or a seat swap is a no-op. |

A settler another mod gave a conversation to keeps it: an XML conversation already inherits
`BaseConversation` and already carries the ritual choice, so replacing it would take away somebody
else's content to add something already there.

**Consequence, stated because it is new.** `GivesRep` is what opens the ritual and also what makes
killing a citizen cost reputation: vanilla's legendary-kill arithmetic against the creature's base
allegiance (the realm) plus one to three related factions, and the full water-ritual curse if the
founder had shared water with them first. Bounded to the realm and its related factions; not a
world-wide penalty. Turn `r_TAF_OptionCitizenRite` off and no *new* settler becomes a host — nothing
already added to a creature is taken back off it.

## Object properties (stable contract)

These are read and written across the mod and are part of the API:

| Property | Meaning |
|---|---|
| `KingdomCitizen` (int) | Creature belongs to the kingdom. |
| `KingdomBorn` (int) | Settler created by the growth engine; only these may emigrate. |
| `KingdomStores` (int) | Container is dedicated to the settlement's water stores. **Nothing without this flag is ever consumed.** |
| `KingdomLarder` (int) | Container is dedicated to the settlement's food stores. The same law: nothing without this flag is ever counted, filled, or eaten. Commissioned pantries (`KingdomRules.CivicLarderBlueprints`) set it themselves; anything else needs the Charter. |
| `KingdomBuilt` (int) | Object was raised by a commission. |
| `KingdomRaider` (int) | Hostile spawned by a raid. |
| `KingdomCaravan` (int) | Merchant spawned by a trade charter; despawned on later visits. |
| `KingdomOrigin` (string) | Settler's region of origin. |
| `KingdomResidentId` (int) | The settler's identity, minted once off the realm's counter and never reused. The **only** thing about a person their body carries; everything else is a resident row. Retired the `KingdomBrinkRoof*` / `KingdomBrinkCreed*` property family in W2. |
| `KingdomJobId` (int) | The job a transient body renders. Reserved by W2 for the stale-transient sweep; W3 mints them. |
| `KingdomCropSownTick` (int) / `KingdomCropRows` (int) / `KingdomCropCycles` (int) / `KingdomCropSeed` (string) / `KingdomCropSaid` (int) | One sown field's commitment: when the founder sowed it, how many rows went in, how many gatherings it has resolved (the kernel ordinal the seed-return draw is keyed on), which seed is in it, and the last want it announced. On the FIELD. Properties rather than part fields on purpose — `r_KingdomPlot` serializes positionally, and appending to it would put every already-built field's layout at risk. |
| `KingdomCropRow` (int) / `KingdomCropField` (string) | A standing crop plant this mod laid, and the field that laid it. The protection law's whole warrant for taking one up. |
| `KingdomWildSeedTaken` (int) | A wild plant already stripped of its seed. One plant is one seed, forever. |

`r_KingdomProperty` is the durable object-level property warrant. It records receipt version,
realm, settlement, faction, exact object ID, exact prior native owner, phase, ticks, and fault.
Only the Charter's explicit nearby-object action creates it; a ground claim never does. Active
receipts prevent stacking and replication. Release restores the exact prior `Physics.Owner` value
and removes the receipt; any third-party ownership change quarantines without overwrite.

## Guarantees

- **Inheritance carries the witnessed place, never the stash.** A current external seal freezes
  one loaded 80x25 seat's authored architecture receipts and connected relative street graph.
  Import uses those receipts through the ordinary architecture stamper, never today's mutable
  catalogue; missing optional fabric degrades one whole work to an empty memory marker. No old
  creature, item, liquid, charge, object identity, or founding-basin authority crosses runs.
  Schema-4 seals retain their bounded anchor-proxy path, while malformed current geometry fails
  closed before placement.
- **The protection law**: kingdom systems never consume, move, or destroy an object the
  player or another mod placed, unless the player explicitly dedicated it. Automatic
  placement only ever targets empty cells.
- **The world keeps time; awareness is pushed.** Processes run on elapsed time — crops, refining,
  construction, wear from hard running, osmosis, dissent, subsidence toward the supported level —
  and every rate is time × **labour** × infrastructure, never time alone, so idleness costs
  nothing and an unstaffed work produces nothing. **No clock in this mod caps or forgives elapsed
  time.** The irreversible ones stop at a `KingdomBrinkRules` brink, and word reaches the founder
  wherever they are with a named arrest and a fair span of world days; spend that span elsewhere
  and the thing happens, dated to the day it happened. An absence of any length still arrives at
  the same brink — nothing accrues past one — and no absence can ever deliver a loss the founder
  was not warned about.
- **Time never mints an unchosen debt.** What bounds an absence is subsidence toward the level
  the works honestly carry, floored at Camp's own equilibrium — not a forgiveness cap, and never
  a bill that grew while nobody could act on it. Anything a founder can still put right, they
  are given the chance to.
- **Failures degrade**: an exception in our code is logged and skipped, never propagated
  into the host game.
