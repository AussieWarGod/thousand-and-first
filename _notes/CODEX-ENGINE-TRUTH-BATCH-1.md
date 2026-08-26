# Codex engine-truth batch 1 — everything the pass-1 build cannot answer for itself

Date: 2026-08-20
Lane: engine-source audit. **Research only — no code, no branch, no tracked edit.**
Requested by: Claude (pass-1 build lane, claimed in `COORDINATION.md` **In flight**).

> **Status, 2026-08-20 17:5x: Codex's usage pool ran out before this brief could be run.** The
> lane is reassigned in-house against the same archived decompile and the same evidence standard
> (quote file:line, mark UNVERIFIED, publish a verified-clean list). Q1-Q3 dispatched immediately
> because the multi-city wave is blocked on them; Q4-Q6 follow. The brief stays as written so that
> if the pool returns, Codex can re-run any question as an independent second opinion — which is
> what its lane was actually worth, and what in-house answers do not replace.

## Why this is one brief and not five

This batches every remaining question that genuinely needs the engine-source lane, so it is
answered once, in one place. Everything else in the pass-1 build has been resolved from the
archived decompile directly.

**Answer in one artifact**, `_notes/CODEX-ENGINE-TRUTH-BATCH-1-ANSWERS.md`. Answer Q1 and Q2
first — the multi-city wave is blocked on them and nothing else here is.

## Ground rules (unchanged from the playbook)

Ground truth is the archived decompile of build 2.0.211.51 at
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` (5,437 files; `PROVENANCE.md`
inside records the DLL SHA and decompiler). Game data XML is
`/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base`.

Verify every claim by reading that source. Quote the file, symbol, and line you relied on. Mark
anything you could not verify **UNVERIFIED** rather than inferring it, and finish each question
with a short "verified clean" list of what you checked and found fine. An unsourced engine claim
is a lead, not a fact.

## Q1 — What fires for a settlement whose zone is never activated? *(blocks the multi-city wave)*

Today the whole simulation hangs off `ZoneActivatedEvent` (`Core/KingdomSystem.cs:225`), which
fires only for the single active zone. That is sufficient for one settlement, because the player
must stand in it. It is not obviously sufficient for **two** settlements, where city B must keep
its own clock while the player is in city A or a thousand parasangs away.

- (a) Which engine events does an `IGameSystem` actually receive on a cadence independent of the
  active zone — `EndTurnEvent`, `BeforeRenderEvent`, an hourly/daily hook, `IGameSystem` virtuals
  we are not overriding? For each: exact registration surface, firing frequency, and whether it
  fires while the player is in the world map / a menu / a cutscene.
- (b) What is the real per-turn cost of the cheapest such hook, and what does vanilla itself use
  for the same shape of problem (a thing that must advance while unobserved)?
- (c) `ZoneRepair`-style lazy tick-stamp catch-up is the idiom we recorded in investigation 1. Is
  that still the engine's own answer here — i.e. is it safe and vanilla-conformant for city B to
  advance nothing until *something* touches it, then advance semantically from a stored tick?
- (d) Does any of this change if the two settlements are on different world-map tiles, different
  strata, or different world levels?

## Q2 — Reading a zone's terrain without generating it *(blocks terrain-at-founding and city siting)*

`_notes/TERRAIN-FOOD-INDEPENDENT-AUDIT.md` records that `cell.ParentZone.GetTerrainObject()`
reaches the terrain object for the **active** zone.

- (a) Exact behaviour of that lookup on a zone that is *not* built/thawed: does it return null,
  return a stub, or force zone generation? Quote the path.
- (b) Is there a read that answers "what terrain is at world position X" from world-map data
  alone, with no zone build — and what exactly does it cost?
- (c) If a mod reads terrain for a site the player has never visited, does anything observable
  change for the player (generation side effects, RNG stream advance, save growth)? The RNG
  question matters most: an accidental draw from a shared stream changes worldgen determinism.

## Q3 — Creating a second runtime faction, and living with it forever

Founding again after exile creates a **new** faction while the old one persists with its grudge.

- (a) Exact supported API for creating a faction at runtime, and exactly which of its fields
  survive save/load. Investigation 1 recorded "runtime factions persist" — re-verify it, because
  that finding predates the current build and two of its siblings turned out false.
- (b) Is a faction removable, renamable, or mergeable at runtime? If not (expected), what is the
  permanent cost of each extra faction — iteration cost, save size, UI listings, and anything in
  vanilla that walks all factions and would now walk ours.
- (c) Does any vanilla system assume a closed faction set, such that adding one mid-game is
  observable as a bug (reputation UI, water-ritual partner selection, quest givers, mural or
  gospel generation)?

## Q4 — Destructive side effects on the parts our own blueprints carry

Precedent, found this session: `r_KingdomChargingPost` carried a vanilla `Capacitor`, our crew
code fills it (`Growth/KingdomGrowth*.cs`), and vanilla `Capacitor.HandleEvent(
BeforeDeathRemovalEvent)` (`XRL/World/Parts/Capacitor.cs:340-347`) detonates for the whole stored
charge when the object dies — up to 4,000 force inside the settlement, destroying player-placed
objects the mod is forbidden to touch. Now fixed with `MinimumChargeToExplode="0"`.

That was found by accident. Do it systematically: for **every** part named in
`ObjectBlueprints.xml` and every blueprint referenced by `KingdomBuildings.xml` (including the
vanilla blueprints we commission rather than define), list any behaviour that on death, damage,
EMP, rust, liquid contact, or zone unload **destroys, moves, consumes, or spawns** anything.
Flag each against the standing agreement: *player-placed objects are never consumed, moved, or
destroyed without explicit designation.* A "verified clean" list is as valuable as a finding.

## Q5 — What vanilla already has for growing food, before we invent a crop cycle

The food/terrain wave wants a plot that cycles Dormant → Growing → Ripe.

- (a) Enumerate the vanilla surfaces for this — `Harvestable`, seed/planting parts, `Hydropon`,
  watervine, any farming in villages — with what each actually does and what drives its clock.
- (b) For each: is it usable by a mod on a mod-defined object, and does it persist correctly?
- (c) The recorded reason to own our own state was determinism. Does any vanilla surface give a
  deterministic, save-safe cycle we could reuse instead — and if we reuse one, what do we give up?
- (d) Confirm the exact part names for classifying food already in containers (`Food`,
  `PreparedCookingIngredient`, others?) and whether any of them mutate on inspection.

## Q6 — Should petitions become real quests? *(long-standing open investigation)*

`Quests/KingdomPetitions.cs` runs its own lifecycle. `AGENT-PLAYBOOK.md` lists this as an
investigation nobody has run: whether petitions should graduate to real `Quests.xml` entries with
journal integration, or stay in our own system. Needed: the exact cost of a base `Quest` with a
null custom manager, its turn-in and failure surfaces, its save behaviour, and what recovery
looks like when the player dies or the zone unloads mid-quest. A recommendation with the
reasoning, not just a capability list.

## What is explicitly NOT wanted

No design opinions on the mechanics themselves, no schema/wire/codec work, no review of the
Growth schema-5/6 track (parked as future architecture for the pass-1 build), and no edits to any
tracked file. If a question turns out to be answerable only by running the game, say so and stop —
the live playtest is a separate gate and it is the author's, not an agent's.
