# Architecture

The Thousand and First is a Caves of Qud scripting mod. Qud streams root XML and compiles shipped
C#; no standalone executable or server exists. `Tools/stage.sh` is the canonical inventory of
what reaches the game and Workshop package.

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

## Runtime and authority

`KingdomSystem : IPlayerSystem` is the main player-save authority. It owns realm-wide state and
the seated/away settlement relationship. `KingdomSettlement` is the bounded settlement snapshot
used by seat, archive, exile, and inheritance flows. Do not create a second authority for the
same fact.

City simulation uses deterministic state and books under `Simulation/City`; engine-facing code
dispatches semantic work and projects results into existing realm surfaces. Physical resources
remain game objects or liquids. Abstract ledgers are evidence and reports, not permission to mint
or delete physical stock.

Lifecycle, carry, and growth operations persist plans, resource leases, physical receipts,
outboxes, and retry state through `Experience/KingdomLifecycleState.cs` and
`KingdomLifecycleRules.cs`. A retry must prove whether a physical effect happened; a return value
or intended delta is not proof.

## Data and extension boundaries

Root XML registries load with Qud's mergeable-stream idiom. Reusing a documented key merges by
load order; a new content record should normally require no C# catalog edit. Schemas and examples
live in [MODDING.md](../MODDING.md).

The supported C# surface is only `ThousandAndFirst.Api` as documented in [API.md](API.md).
Everything else is internal even where C# visibility is public for Qud integration. Extensions
receive frozen readings, deterministic draw lanes, bounded output, and explicit version refusal.

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

### Public API and XML merge semantics

`Api/` version, contract fields, ordering, limits, and refusal behavior affect other mods. XML
keys, defaults, case folding, negation, and merge rules are also public interfaces. Update
documentation and compatibility tests in the same change; do not silently reinterpret old data.

## Testing layers and current limitations

1. Checkout-only inventory check: `./Tools/stage.sh verify`. `python3 Art/check_xml_refs.py`
   always checks internal XML and auto-adds vanilla resolution when its default Qud install is
   present; `--base` selects another licensed installation.
2. Pure rule/source-contract suite: `dotnet run --project DevTests/TafTests.csproj -v q --nologo`.
   Rule sources are engine-free, but current runner references Qud's licensed NUnit DLL and is not
   a checkout-only portable test harness.
3. Exact staged compile, base-game XML/tile verification, ABI and release harness:
   `./Tools/release-check.sh`. Current scripts require a locally owned Qud install, WSL/Windows
   PowerShell, and configured Windows paths.
4. Controlled in-game passes: [TESTING.md](../TESTING.md), including isolated load, player-log
   review, and save/reload scenarios.

Game assemblies, decompiled sources, and extracted assets cannot be redistributed to make hosted
CI self-contained. Future tooling may make more pure tests repository-only; until then, record
which licensed gates you could run and never imply an unavailable gate passed.
