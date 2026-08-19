# The Thousand and First — Engineering Standards

The bar: **could be upstreamed**. Code should read like Freehold wrote it; systems should be
as polished as the game's flagship features. These rules are binding for every slice.

## 1. Vanilla conformance

- **Serialization is law.** Part fields serialize positionally: never reorder, retype, or remove
  a serialized field on a shipped part. Systems use name-based field reflection: treat fields as
  append-only anyway. Every stateful class carries `SerializationVersion` from its first commit;
  when structure outgrows field reflection, switch to custom `Write`/`Read` with a magic marker
  (the TrophicRepertoire pattern). Player-critical parts get `[CallAfterGameLoaded]`
  `RequirePart` guarantees.
- **Events the engine's way.** Systems subscribe in `Register(XRLGame, IEventRegistrar)` via
  `Registrar.Register(Event.ID)` and override the matching `HandleEvent`; always
  `return base.HandleEvent(E)` unless deliberately consuming. Prefer pooled/typed events over
  string events; use string events only where vanilla only offers those.
- **Known engine traps, never re-learned:** `Brain.Factions` is write-only (read `Allegiance`);
  population-table edits are process-static (re-apply each load); `PinnedZones` hard cap 3
  (never pin — lazy catch-up); zone builders run once (visited zones get live mutation, the
  Reclamation pattern); mid-session mod rebuilds mint ghost assembly generations (full restart
  during dev; name-based purge for parts that survive saves).
- **Style is vanilla style.** PascalCase public fields for serialized state, `E` for event
  parameters, `The.Game`/`The.Player`/`The.ZoneManager` accessors, `GameObject.Validate` before
  using held references across turns, `{{C|...}}` color markup in player-facing strings,
  `Popup.Show` for modal moments and `MessageQueue` for ambient ones. No doc-comment banners,
  no narration comments — a comment exists only to state a constraint the code cannot show.
- **Prose bar applies to code output.** Any player-visible string is written in register:
  lowercase Qud-style object names, proper use of =subject= grammar helpers when addressing
  creatures, month/AR dating in chronicle text.

## 2. Structure

- **Folder-per-system** (`Core`, `Founding`, `Growth`, `Chronicle`, ... as slices land), matching
  the Creature Control layout. `Debug` holds the wish harness; anything in `Debug` must be
  side-effect-free on ordinary saves (reversible probes only).
- **Pure logic is engine-free.** Every rule that can be computed without the engine lives in a
  static class with zero `XRL` usings (`KingdomRules` is the template): coupling math, parsers,
  stage thresholds, name grammars. These classes are the unit-testable surface.
- **Data-driven where vanilla is.** Content goes in mergeable XML (`Options`, factions,
  conversations, population tables, embark modules) with our `r_` prefix; C# is for behavior
  only. Never overwrite a vanilla blueprint — merge or patch.

## 3. Feature gating

- Per-module option toggles for every major system (city, trade, chronicle, ruins), under the
  Mods options category with plain-language descriptions.
- Sifrah-flavored interactions are gated on the relevant `Options.Sifrah*` flag with a
  first-class plain resolution beside them (the becoming-nook hack precedent). Neither path is
  the afterthought.
- Optional integrations (Hearthpyre, Qud Industry) live behind manifest `Directories` gating
  and compile symbols; the core never references them.

## 4. Testing — three layers, all required

1. **Compile gate.** The harness rsp build must be clean (zero errors, zero new warnings)
   before any in-game run. The rsp mirrors the game's own compiler: all Managed refs minus
   never-loaded assemblies, `VERSION_*`/`BUILD_*`/`MOD_*` defines.
2. **Pure-logic tests.** NUnit `[TestCase]` tables (the engine's own `PopulationManagerTest`
   idiom) over the engine-free classes, kept outside the mod folder (the game compiles every
   `.cs` under the mod), run via `harness\tests\taf`. New pure logic lands with its table.
3. **Live-engine selftest.** The `kingdom:selftest` wish asserts invariants inside a real
   game: registration, mirror consistency, spillover math against actual engine deltas,
   claim coherence. Probes must restore state. Run after every in-game deploy, plus a
   save → quit → reload → `kingdom:status` pass whenever serialized state changed. Player.log
   is watched during test sessions; "Bad event bind" is a serialization regression, full stop.

## 5. The depth standard

No system ships shallow. Before a slice is called done, each of its systems passes the
thirst-ladder checklist:

1. A **physical resource** under genuine tension — never an abstract bar.
2. **Graduated states with hysteresis** — streaks and ladders, not single thresholds.
3. **Witnessed-only accounting** — absence can never stack consequences.
4. A **bounded consequence** per visit, with an **instant and total recovery path**.
5. **Permanent story residue** — state changes can leave chronicle lines, scars, artifacts.
6. **Pure-testable rules** — the math lives in engine-free classes with `[TestCase]` tables.
7. **Interlock** — the system consumes or feeds at least one other system's resource.
   Depth is coupling, not complexity.

The design doc's depth matrix (§2) is the authoritative per-system specification.

## 6. The extensibility law

Every content registry — buildings, styles, settler origins, raider tables, district
effects, and anything future — loads through `DataManager.YieldXMLStreamsWithRoot` so
third-party mods extend it by shipping a file with the matching root element. Key re-use
overrides by load order (retheming is a supported use case). A hardcoded catalog is a
defect. Registry schemas are documented in MODDING.md the same commit they change, and
parse/validation logic is pure and tabled.

## 7. The protection law

Nothing the player placed, built, or installed is ever consumed, moved, destroyed, or
overwritten by kingdom systems without explicit designation:

- City stores are **opt-in**: only containers carrying `KingdomStores=1` (commissioned
  storage auto-flags; anything else requires the Charter's dedicate action) are counted,
  filled, or drunk from. A player's dropped waterskin is inviolate.
- Automatic placement (scaffolds, settlers, raiders) targets **empty cells only**; growth
  never replaces an existing object, whether vanilla, ours, player-placed, or another
  mod's (Hearthpyre structures included — dedicating a Hearthpyre-built basin to the
  stores is the integration, and it is the player's choice).
- Kingdom systems may destroy only objects they created and marked (`KingdomCitizen`,
  `KingdomBuilt`, `KingdomRaider`); wounds to anything else come only from ordinary
  simulation (combat, fire), never from scripted deletion.

## 8. Balance invariants

Learned from comparables (Kenshi, Bannerlord, Banished, RimWorld, Terraria, Suikoden) and
binding on every economic system:

- **Threat must cost more than its remedy.** Tribute is always cheaper than the raid it
  averts (raiders plunder stores, so paying is strategy, not extortion); protection that
  changes nothing observable is annoyance.
- **Consumption scales with time, not with event cadence.** Any per-interval cost must be
  computed from elapsed ticks (with an absence cap), or it silently vanishes as the
  interval grows.
- **Ratchets gate on durable things.** Growth stages gate on capacity (infrastructure that
  must be built) rather than current stock (which can be lent and withdrawn).
- **Failure has a floor.** Decay stops at a loyal core; the settlement can always be clawed
  back, and every loss names its cause in the departure message and the chronicle.
- **Passive income stays sub-adventuring** and arrives as a lump event with a report, never
  a silent trickle; caravans are events, not spreadsheets.
- **One arrival per day, each attributable.** Growth events must name the player action
  that caused them.

## 9. Release discipline

- Ship in named arc slices with a paste-ready changelog file per release
  (Feature-Friday voice), `workshop.json` kept in sync.
- Grep for `TEMP-DIAG` and `Debug.Log` before any upload.
- Compatibility posture stated in the description and honored: XML merges, `Prepare()`-gated
  Harmony, no blueprint overwrites.
- Data files ship with a documented format header and a harness validator once they exist.
