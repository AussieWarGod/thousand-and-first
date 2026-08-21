# The Thousand and First — Engineering Standards

The bar: **could be upstreamed**. Code should read like Freehold wrote it; systems should be
as polished as the game's flagship features. These rules are binding for every slice.

## 1. Vanilla conformance

- **Serialization is law.** Parts and ordinary systems both use positional field reflection by
  default: never reorder, retype, remove, or casually append a serialized field. Durable systems
  opt out with `WantFieldReflection = false` and write a magic marker, schema version, and named
  fields. Named fields may be added, but existing names and types stay stable unless an explicit
  migration handles them. `[FieldSaveVersion(N)]` is checked against the engine's save-file
  version, not this mod's schema version; a bare `SerializationVersion` field is inert unless
  custom `Write`/`Read` consumes it.
  Player-critical parts get `[CallAfterGameLoaded]`
  `RequirePart` guarantees.
- **A failed load is silent unless you break the silence.** The engine reads every composite
  inside a length-framed block wrapped in `try`/`catch`: on exception it logs, seeks past the
  block, and hands back the half-built instance. Nothing crashes and nothing else is lost —
  which is why a throwing `Read` costs the player their entire kingdom with no message on
  screen. Throwing is still the *only* way to reach that recovery, so an unreadable save must
  throw; its entire custom read path must be wrapped so truncation, field assignment, and
  normalization failures all set a `[NonSerialized]` flag before rethrowing, then report on
  `AfterGameLoadedEvent`.
  And because named fields are self-describing — an unknown name is skipped, a missing one keeps
  its default — every schema version at or below the current one is readable and must be read.
  Refusing an older version turns a routine additive change into a save-wipe.
- **Events the engine's way.** Systems subscribe in `Register(XRLGame, IEventRegistrar)` via
  `Registrar.Register(Event.ID)` and override the matching `HandleEvent`; always
  `return base.HandleEvent(E)` unless deliberately consuming. Prefer pooled/typed events over
  string events; use string events only where vanilla only offers those.
- **Never trust a vanilla return value for accounting — measure the state change.** A boolean
  from an engine method reports whatever that method found convenient, not whether your
  operation succeeded, and the name is not a contract. `LiquidVolume.UseDrams` returns whether
  liquid *remains* and empties the vessel on an exact request; `AddDrams` silently clamps to the
  space available and returns `true` regardless. Code that read those booleans as success
  double-drained the settlement's stores while reporting half the loss. Wrap any engine call
  whose effect you need to count in an adapter that reads the before and after state and returns
  the actual delta (`KingdomLiquids.Drain`/`Fill` are the pattern), and never let a raw call of
  that kind survive in a system. Pre-clamping to a limit you measured yourself is not a defence;
  it is the same bug waiting for the limit to be wrong.
- **Fresh water means pure water, never water-primary liquid.** Qud's salt pools are
  `water-600,salt-400`, so `GetPrimaryLiquidID() == "water"` admits brine. Civic founding,
  storage, upkeep, fetching, tribute, and trade use `LiquidVolume.IsFreshWater()` through
  `KingdomLiquids`; an empty receiver uses `IsFreshWater(AllowEmpty: true)`. Never turn a
  mixture into pure water by draining it and refilling another vessel as `"water"`.
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

**An asset is not shipped until something proves it is reachable.** Four tiles once passed the
whole art pipeline — source grid, PNG, coverage measurement, preview, contact sheet — with no
`Tile=` attribute in any blueprint. Every check that existed was a check on the art, and the art
was correct; it simply rendered nowhere. Qud falls back to the ASCII glyph without logging
anything, so the failure is invisible in play and looks like a deliberate styling choice.

The rule generalises past tiles: wherever the mod produces an asset that some other file must
reference by name to reach — a texture, a blueprint, a population table entry, a conversation
node, a book ID — a check walks the reference in **both** directions. Unreferenced asset and
dangling reference are both errors, and neither is detectable by validating either file alone.
`Art/check_wiring.py` is the instance of this for tiles, and it is verified the only way such a
check can be: by confirming it fails against the commit that contained the defect.

`Art/check_xml_refs.py` is the instance for names the game resolves at load or roll time. Writing
it immediately found the population-table case the paragraph above predicted: an entry merging our
book into `DynamicObjectsTable:Books` — a table declared nowhere, fabricated on demand from a
blueprint tag that no blueprint in StreamingAssets carries, and rolled by no code in the game. It
had shipped looking like content.

That case carries a rule of its own, because the failure was not merely inert. A `Load="Merge"`
whose target does not exist does not merge — it **creates** the table
(`XRL/PopulationManager.cs:830-844`). For the fabricated `DynamicObjectsTable:` and
`StaticObjectsTable:` families, merely holding the name then suppresses fabrication entirely,
because `RequireTable` returns early once the key exists (`:189-192`). So a dead merge into a
fabricated table is aimed at whichever future build first tags a blueprint for it: ours would win,
silently, for the whole game. **Never declare a fabricated table name — tag the blueprint
instead.** And if nothing rolls the table, say so in a comment rather than shipping a line that
reads like it works.

## 5. The depth standard

No system ships shallow. Before a slice is called done, each of its systems passes the
thirst-ladder checklist:

1. A **physical resource** under genuine tension — never an abstract bar.
2. **Graduated states with hysteresis** — streaks and ladders, not single thresholds.
3. **Witnessed-only accounting** — absence can never stack punishments. Two things move in
   absence and nothing else does: supply-carried *level*, which subsides toward what the
   infrastructure honestly carries (bounded, chronicled, arrestable on return — down to Camp if
   that is all that stands), and the gifts and news an absence earns, both capped. Social and
   event processes — dissent, conversion, wear, grace clocks — move only while attended.
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

## 7b. Nothing stalls in silence

Any settlement process that can stop short must be able to say why, once, where the founder will
see it — the ledger, the status report, or a message line. A thing that will not progress and will
not explain itself is the single most common complaint levelled at building systems of this shape;
the comparables pass names it specifically against Sim Settlements, whose plots stall for reasons
the player cannot see. It is also the cheapest possible bug to introduce, because a silent stall is
usually a guard clause someone added for a good reason and returned from without a word.

Concretely, every early return in a per-pass resolver falls into one of two kinds:

- **Not applicable** — the module is off, the settlement is unfounded, this is not our ground.
  These say nothing, correctly.
- **Applicable but blocked** — no water, no hands, no room, nowhere to put the output. These must
  announce, and must announce **once** rather than every visit. The established idiom is a
  `bool ...Announced` flag on the object or the system, set when the reason is first given and
  cleared the moment the block lifts. See `r_KingdomPlot.NoLarderAnnounced` and
  `KingdomSystem.IdleWorksAnnounced`.

The rule exists because this was violated the same day it was written: a guard stopping a plot from
drinking water it had nowhere to spend was correct, and returned in silence, so the plot simply
never grew and nothing anywhere said why.

## 8. Balance invariants

Learned from comparables (Kenshi, Bannerlord, Banished, RimWorld, Terraria, Suikoden) and
binding on every economic system:

- **Threat must cost more than its remedy.** Tribute is always cheaper than the raid it
  averts (raiders plunder stores, so paying is strategy, not extortion); protection that
  changes nothing observable is annoyance.
- **Consumption scales with time, not with event cadence.** Any per-interval cost must be
  computed from elapsed ticks (with an absence cap), or it silently vanishes as the
  interval grows.
- **Time is labour, never maturation or decay.** Running costs are the only thing elapsed
  ticks meter. Nothing improves, ripens, or wears because the calendar moved: a build costs a
  duration because the work takes hands and days, wear comes from events (raids, hard running,
  temperamental salvage) and never from the clock, and no upgrade threshold anywhere is a
  timer. Every gate is keyed to something with meaning in Qud — drams spared, materials
  quarried, hands free, stage, standings.
- **Ratchets gate on durable things.** Growth stages gate on capacity (infrastructure that
  must be built) rather than current stock (which can be lent and withdrawn).
- **Failure has a floor.** Decay stops at a loyal core and subsidence stops at Camp; the
  settlement can always be clawed back, and every loss names its cause in the departure
  message and the chronicle.
- **Passive income stays sub-adventuring** and arrives as a lump event with a report, never
  a silent trickle; caravans are events, not spreadsheets.
- **One arrival per day, each attributable.** Growth events must name the player action
  that caused them.

## 9. Public API, documentation, and robustness

This mod is a platform: other people's mods and saves depend on it. That raises the bar
above "works on my machine" to library discipline. Note the deliberate split with §1 —
vanilla style governs *implementation*, library discipline governs *contracts*.

**API surface is explicit.** Every public type and member is either part of the supported
API or marked internal in intent. Supported API is listed in `docs/API.md`; anything absent
from that file may change without notice. Anything present changes only under the
versioning rule below.

**Documented contracts.** Every supported public member carries an XML doc comment stating
what it does, what its parameters mean, what it returns, and — critically — its
*preconditions, side effects, and failure mode* (does it return false, log, or throw?).
Implementation bodies stay comment-free in the vanilla style: document the contract at the
boundary, never narrate the mechanics inside.

**Errors never escape into the engine.** Our code runs inside the host game's event
dispatch. A handler that throws can break a player's save or hang a turn — including for
players who installed us as one of a hundred mods. Every entry point invoked by the engine
(system event handlers, part events, wish commands, registry loading) wraps its work so a
failure logs through `MetricsManager.LogError` and degrades to a no-op rather than
propagating. Failures are loud in the log and silent in the world.

**Hostile-input discipline at every boundary.** Third-party XML, third-party callers, and
save data from older versions are all untrusted: validate, clamp, and reject with a logged
reason. A malformed extension entry disables itself; it never crashes the kingdom, and it
never silently half-registers.

**Versioning and deprecation.** The mod version is semantic: patch for fixes, minor for
additive API and content, major for breaking changes. Supported API is never removed in a
minor release — it is marked `[Obsolete]` with the replacement named, kept working for at
least one minor cycle, and its removal is recorded in `CHANGELOG.md`. Serialized field
layouts follow §1 regardless of version.

**Documentation is part of the change, not after it.** A commit that changes supported API
updates `docs/API.md` and `CHANGELOG.md` in the same commit; a commit that changes a data
schema updates `MODDING.md` in the same commit. Documentation drift is treated as breakage.

**Test coverage of the contract.** Every supported public member with logic is covered by a
`[TestCase]` table or an in-game selftest assertion. Bug fixes land with the test that
would have caught them.

## 10. Release discipline

- Ship in named arc slices with a paste-ready changelog file per release
  (Feature-Friday voice), `workshop.json` kept in sync.
- Grep for `TEMP-DIAG` and `Debug.Log` before any upload.
- Compatibility posture stated in the description and honored: XML merges, `Prepare()`-gated
  Harmony, no blueprint overwrites.
- Data files ship with a documented format header and a harness validator once they exist.
