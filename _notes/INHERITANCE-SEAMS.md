# Cross-save inheritance — engine seam and safety contract

Checked against installed Caves of Qud `2.0.211.51`, Steam build `24626113`, DLL SHA-256
`8c3b0fc371eaffc85e3dc971e8ffcb21dfc6b05fb25b92398fbd474260598000` on
2026-08-19. `D/` below means a fresh local decompile of that pinned assembly; its filesystem
location is deliberately contributor-specific.

## Ruling and verdict

The user settled the timing question:

- a living save never languishes because the player has not visited;
- visits do not reset a hidden clock;
- wall-clock time is never the default authority;
- death or explicit retirement seals one immutable condition;
- a later save applies one fictional intergenerational transition once.

**Verdict: feasible without importing an old Qud save, replacing Joppa's world builder, or
touching vanilla Coda identity.** The safe route is:

`live semantic snapshot -> staged synced envelope -> final-run proof -> immutable legacy ->
new-game singleton -> remaining mutable world site -> one-time reconstruction receipt`

This is an engine-seam verdict, not permission to build it yet. The sealed data model,
interregnum rules, reconstruction grammar, ownership behavior, and play value still require
joint design and tests.

## Exact engine evidence

### Cross-run storage exists

- `DataManager.SyncedPath` is explicitly the read/write path saved or synced to the cloud
  (`D/XRL/DataManager.cs:421-426`).
- Vanilla Coda uses this exact pattern. `EndGame.SaveCoda` saves `coda.sav.gz`, creates
  `SyncedPath("Codas/")`, and copies it to `coda-<GameID>.sav.gz`
  (`D/XRL/World/Conversations/Parts/EndGame.cs:542-551`).
- `Scoreboard2` stores `HighScores.json` in `SyncedPath`, and `ScoreEntry2` persists the
  `GameId`, mode, name, level, turns, and score (`D/XRL/Core/Scoreboard2.cs:12-100`;
  `D/XRL/Core/ScoreEntry2.cs:8-55`).

Therefore a namespaced `SyncedPath("ThousandAndFirst/Legacies/")` is a valid engine-shaped
location. It is not evidence that arbitrary writes are atomic; the mod must supply its own
journaling, validation, and recovery.

### Final death can be distinguished from a live/checkpoint save

- A game's save root is `SyncedPath("Saves/" + GameID)`. `GetCacheDirectory` creates that
  directory even before a primary save exists (`D/XRL/XRLGame.cs:1630-1650`). Check for a
  valid `Primary.sav.gz`, not merely directory existence.
- After the game loop ends, `XRLCore.RunGame` calls `BuildScore` when `DeathReason` is not
  `<nodeath>` and `forceNoDeath` is false. It then deletes the save directory when permadeath
  is enabled (`D/XRL/Core/XRLCore.cs:3003-3058`).
- `BuildScore` records the current `Game.GameID`; checkpoint modes replace a prior entry with
  the same ID rather than losing identity (`D/XRL/Core/XRLCore.cs:3267-3356`;
  `D/XRL/Core/Scoreboard2.cs:23-43`). Replays do not add a real score.
- Checkpoint/roleplay death handling happens after early death events and may restore play.
  `AfterDieEvent` fires before `KilledPlayerEvent`, `CheckpointingSystem.ShowDeathMessage`,
  debug cancellation, final `DeathReason` assignment, and `Running=false`
  (`D/XRL/World/GameObject.cs:15013-15095`). An `AfterDie` or `KilledPlayer` hook may stage
  cause text, but may never by itself promote a legacy.
- Save-and-quit and checkpoint quit set `<nodeath>` / `forceNoDeath` and therefore do not
  produce the final-death sequence (`D/XRL/Core/XRLCore.cs:1005-1145,1725-1800`).
- With `DisablePermadeath`, a score may exist while the save remains. Score alone is not final
  proof.

Automatic death eligibility is therefore conservative:

| Score for origin `GameID` | Valid origin `Primary.sav.gz` | Meaning | Automatic import |
|---|---|---|---|
| no | yes | live/save-and-quit/checkpoint | no |
| yes | yes | checkpoint, no-permadeath, or interrupted cleanup | no |
| yes | no | engine-confirmed ended run | yes, if envelope validates |
| no | no | manual deletion, corruption, or orphan | never automatic; recovery UI only |

The score/save test is performed on a staged semantic record, never by loading the old save.
Cleared scoreboards and cloud conflicts degrade to `orphan`, not guessed death.

### New-game import can run before world generation

- New games receive `GameID = Guid.NewGuid()` before systems, history, or worlds initialize
  (`D/XRL/CharacterBuilds/Qud/QudGameBootModule.cs:229-282`).
- `[GameStateSingleton]` types are constructed, added to `ObjectGameState`, and initialized
  before history/world building (`QudGameBootModule.cs:256-275`). They receive subsequent
  `EmbarkEvent` boot phases (`D/XRL/World/EmbarkEvent.cs:6-50`).
- `IGameStateSingleton` is an `IComposite`, so a small explicit import state can persist in the
  target save (`D/XRL/IGameStateSingleton.cs:5-14`; `D/XRL/XRLGame.cs:1605-1627`).

A namespaced singleton can validate/select an eligible legacy, bind a reservation to the new
target `GameID`, and store the sanitized reconstruction payload before Joppa is built.

### Joppa has a supported extension point and remaining-site allocator

- `[JoppaWorldBuilderExtension]` discovers `IJoppaWorldBuilderExtension` implementations and
  calls `OnBeforeBuild`, `OnBeforeMutableInit`, `OnAfterMutableInit`, and `OnAfterBuild`
  (`D/XRL/World/WorldBuilders/IJoppaWorldBuilderExtension.cs:1-21`;
  `D/XRL/World/WorldBuilders/JoppaWorldBuilder.cs:143-239,3653-3702`).
- `OnAfterBuild` occurs after `BuildMutableEncounters`, so vanilla has already reserved its
  villages, lairs, historic sites, merchants, and other encounters.
- The builder exposes `mutableMap`, `terrainTypes`, `terrainComponents`, `worldInfo`, and
  `WorldZone`. `MutabilityMap.GetMutable` and `RemoveMutableLocation` allow deterministic
  reservation without calling the RNG-shuffling `pop*` helpers
  (`JoppaWorldBuilder.cs:24-52`; `D/XRL/World/MutabilityMap.cs:62-155`).
- Zone builder collections are serialized explicitly with the save. A registered custom
  postbuilder therefore remains attached until first generation
  (`D/XRL/World/ZoneManager.cs:536-609`; `D/XRL/World/ZoneBuilderCollection.cs:228-301`).
- `JoppaWorldBuilder.AddSecret`, `AddLocationFinder`, and `SetZoneName` show the normal route
  for a discoverable generated place (`JoppaWorldBuilder.cs:1978-2014,2673-2684`; custom code
  should use public APIs rather than copy implementation).

Do **not** merge a replacement `JoppaWorldBuilder` through `Worlds.xml`. When a mod world node
adds a builder with the same class, `WorldFactory.LoadWorldsNode` removes the existing builder
and adds the new one (`D/XRL/World/WorldFactory.cs:286-329`). That is replacement, not an
extension, and creates maximum conflict with world-generation mods.

Extension order follows active module priority and is not a final-exclusive lock
(`D/XRL/ModManager.cs:86-118,1185-1218`). Removing a chosen remaining mutable location protects
against cooperative later extensions, not hostile builders. Validate the reservation again at
`BOOTEVENT_AFTERINITIALIZEWORLDS`; fail closed rather than overwrite another location.

## Required external record model

Never copy `Primary.sav.gz`, serialize `GameObject`, use `BinaryFormatter`, or persist a CLR
type name. Cross-run data is a strict JSON DTO with only bounded primitives and semantic IDs.

Minimum envelope:

- `SchemaVersion`, `WriterModVersion`, engine version/fingerprint for diagnostics;
- `LegacyID` (lineage GUID), `OriginGameID`, generation, revision, status;
- sealed founder display snapshot and bounded terminal-cause snapshot;
- settlement display name and stable semantic settlement ID;
- sealed health inputs and already-resolved inherited state;
- bounded relative plan of **mod-owned, explicitly inheritable** works only;
- citizen/culture summaries and roll entries, never live objects;
- pinned chronicle milestones plus a bounded incident digest;
- payload length, canonical SHA-256, and creation provenance;
- reservation/consumption receipts keyed by both `LegacyID` and target `GameID`.

Strict reader rules:

- validate file name independently; never derive a path from JSON;
- reject unsupported schema, checksum mismatch, duplicate semantic IDs, out-of-range counts,
  oversized strings, impossible coordinates/stages, and unknown required fields;
- strip or safely preserve Qud markup from player-controlled names; never let names become
  faction IDs, blueprint IDs, class names, paths, or format templates;
- map allowlisted semantic building kinds to current blueprints in code;
- missing optional-mod content becomes a bounded rubble/memory placeholder, not a load error;
- cap total payload and each list. Chronicle permanence means pinned retention, not unlimited
  growth;
- keep the last known-good slot after any failed write or parse.

Use a two-slot journal per origin (`stage-A`, `stage-B`) with monotonic revision and checksum.
Write an inactive temporary slot, close it, re-read/validate it, then rename to the slot. The
reader validates both and selects the highest complete revision. A torn slot never destroys
the other. Final legacies and receipts are immutable files; do not maintain one fragile global
JSON object.

## Seal, promotion, reservation, and receipt state machine

### 1. Live stage

- Update the stage after meaningful semantic settlement mutations and before saves.
- It contains no eligible result and creates no behavior in another save.
- `IGameSystem.AfterSave` is **not** a whole-file-commit hook: `XRLGame.SaveSystems` calls it
  while the in-memory writer is still near the start of serialization
  (`D/XRL/XRLGame.cs:1578-1589,2249-2390`). Do not label an external write committed there.
- A crash may leave a stage newer than the last primary save. That is harmless while no final
  proof exists.

### 2. Terminal attempt

- A player-death event may add bounded cause/category/turn information to the staged envelope.
- It remains an attempt. Checkpoint restore, precognition/debug cancellation, or continued play
  invalidates/overwrites it.
- Same-run Coda/endgame integration seals at most once before vanilla's +1000-year Coda
  transition. Coda then consumes only authored `GospelText`; the external lineage remains
  namespaced and read-only. Never use `PlayerCult`, `CodaSultan`, `CodaVillage`, or period 7;
  see `LORE-SPIRIT-AUDIT.md`.

### 3. Promotion

- On a later boot, promote an automatic-death stage only when the scoreboard has the exact
  origin `GameID` and no valid origin primary save exists.
- Resolve the fictional interregnum once from immutable seal data and a stable seed derived
  from lineage/origin/generation/revision. Do not include target world seed, current calendar,
  last visit, system time, or an RNG stream the player can reroll.
- Store the resolved state in the immutable legacy. Retrying world generation must reproduce
  it byte-for-byte.
- Explicit retirement is a separate, strongly confirmed charter action. It can seal an
  immutable legacy without deleting a save, but the originating save is then permanently
  marked sealed and cannot rewrite that generation. Continuing to play does not mutate the
  exported legacy. Never silently abandon/delete the player's save.

### 4. Target reservation

- A new-game singleton selects at most one eligible, unconsumed legacy under the configured
  policy and writes a reservation bound to the new `GameID`.
- Default MVP policy should be explicit and simple (`Off` or latest eligible); multiple-lineage
  selection is later UI, not random hidden behavior.
- During Joppa `OnAfterBuild`, deterministically rank still-mutable, allowed surface candidates
  from `worldInfo.terrainLocations`, filter by tier/terrain/distance and `GetMutable`, reserve
  one with `RemoveMutableLocation`, register the custom builder/name/map note, then validate
  after all worlds initialize.
- If no safe site remains, record a visible import refusal and leave the immutable legacy
  unconsumed. Never steal a named village, lair, quest site, water cell, or another mod's
  reservation.

### 5. Consumption

- World placement stores the full sanitized import payload and receipt in the target game's
  singleton; the zone builder reads that target state, not the external stage.
- External receipt begins `reserved`. `GetCacheDirectory` creates empty directories, so only a
  valid target `Primary.sav.gz` can commit it.
- Reconcile reservation after a later action/boot: valid target primary -> immutable
  `committed`; no primary -> recoverable interrupted worldgen; committed stays committed even
  after the target later dies and its save is deleted.
- A target save carries `AppliedLegacyID` and reconstruction version. The zone builder is
  idempotent: built marker/receipt -> no second placement, partial marker -> visible repair or
  safe abort, never another ruin pass.

## What may and may not cross runs

Crossing runs preserves meaning, not a stash.

Allowed, after schema/bounds review:

- settlement and founder names as presentation snapshots;
- a normalized relative street plan of TAF-owned structures;
- structure kind/condition, never contents, charge, liquids, mods, inventories, or cached
  object state;
- culture/provenance summaries and the named roll;
- authored/pinned chronicle milestones and founder cairn facts;
- a bounded health summary used to resolve Held/Faded/Abandoned/Ruins.

Forbidden:

- loose or contained items, trade goods, bits, cybernetics, relics, water quantities, stored
  charge, temporary effects, player stats/skills/mutations, quests, reputation values, faction
  registry keys, object IDs, `GameObjectReference`, raw zones, or arbitrary mod objects;
- exact replay of old creatures. Held/Faded inhabitants are safe descendants/successors built
  from allowlisted provenance; the old named roll remains history. This avoids cloning a
  modded creature graph into a different generated history.

Open design decision: inherited works are a real benefit. Their restored condition, service
readiness, and any starting civic supplies must be normalized/capped so “no items” cannot be
circumvented by banking water or stocked machines.

## Reconstruction contract

- MVP reconstructs one seat zone only. Multi-district geometry waits until realm/settlement
  state and cross-zone transfer are proven.
- Coordinates are relative to a normalized settlement anchor and fitted into the new `80x25`
  zone. Validate every footprint against bounds, connection cells, stairs, player/native
  objects, terrain, reachability, and duplicate occupancy before placing anything.
- Preserve recognizable streets at all states. `Abandoned` is intact/derelict, not explosive.
  `Ruins` uses a purpose-built deterministic transform on the new empty reconstruction canvas,
  never `Ruiner` against a live/frozen authored zone.
- Held is an autonomous polity, not automatically the successor player's property. Faded,
  Abandoned, and Ruins each need a different relationship/recovery loop, not merely fewer
  objects.
- Use a new namespaced faction/settlement identity in the target history. Never import the old
  registry key or mutate vanilla `PlayerCult`.
- Add a stable hidden map note and location finder through public APIs. Whether it is revealed
  at embark, learned as a rumor, or discovered naturally is a playtestable invitation choice;
  no repeated modal reminders.
- Unknown/corrupt optional records fail locally. The rest of world generation must complete.

## Fun hypotheses to test

1. Finding one's prior settlement is emotionally stronger than receiving inherited items.
2. A recognizable street plan plus names/chronicle is enough continuity even when inhabitants
   are descendants rather than resurrected objects.
3. Held should create a relationship problem (“will they recognize your claim?”), not a free
   fully owned town.
4. Faded should offer one legible recovery objective; Abandoned should invite reclamation;
   Ruins should preserve story and silhouette while allowing a new occupant/problem.
5. Ignoring the site for the entire successor run must be harmless: no global debuff, nagging,
   or further unseen decay.
6. The import option and one-time receipt must make the feature feel chosen, not like the mod
   silently rewrote a fresh world.

No state thresholds, restoration discounts, interregnum lengths, population carry ratios, or
site count are validated yet. They require simulation and live play rather than lore analogy.

## Automated gates before engine connection

1. Pure interregnum: no visit/wall-clock/calendar input; deterministic same seal; boundary,
   overflow, monotonic resilience, exact-state, and invalid-enum cases.
2. Envelope: canonical roundtrip; schema upgrade; checksum, truncation, duplicate IDs,
   malicious path/name/markup, oversize lists/strings, unknown blueprint, missing optional mod.
3. Journal: either slot torn; both valid; stale/new revisions; cloud-style divergent stages;
   last-good preservation.
4. Eligibility matrix: all four score/save cases; replay; checkpoint; permadeath disabled;
   cleared score; manual deletion; terminal attempt followed by resumed play.
5. Reservation: two new games race; failed worldgen; no safe site; first save failure;
   committed target later dies; exact one consumer.
6. Placement: deterministic candidate without global RNG consumption; all candidates occupied;
   later extension collision; unsupported world; alternate start; full bounds/connectivity.
7. Reconstruction: each state; duplicate receipt; partial marker; save/reload before first visit;
   unload/reload; missing optional blueprint; zero loose inventory/liquid/charge inheritance.
8. Compatibility: Joppa/Coda/endgame, worldgen extensions, Hearthpyre, alternate-start mods,
   full-stack enabled and legacy option off.

## Mandatory live gates

1. Classic death -> score appears -> origin primary disappears -> exactly one later fresh game
   imports the sealed state.
2. Roleplay/Wander checkpoint death -> save resumes -> no legacy promotion.
3. Save-and-quit, crash, debug-cancelled death, precognition, permadeath disabled, and replay ->
   no automatic promotion.
4. Explicit retirement confirmation -> origin remains recoverable, export becomes immutable,
   successor imports once.
5. Kill during Coda/endgame path -> external seal once; vanilla Coda remains intact and uses no
   TAF identity.
6. Fail a write, corrupt one slot, clear scores, and interrupt worldgen -> visible recovery,
   no lost last-good legacy, no broken new world.
7. Generate with several worldgen mods -> no named-site collision, deterministic site,
   ordinary vanilla quests still reachable.
8. Visit each state, inspect every inherited container/machine/citizen, save/unload/reload, and
   verify no item/currency/power duplication and no second transform.

## Build gate

Do not connect storage or world generation until all are true:

- Claude's pure rule/API rewrite no longer mentions last-visit time or explosive Abandoned;
- the private v3 realm/settlement/citizen authority is implemented and migrated;
- pinned chronicle and semantic building/citizen records exist;
- one-seat reconstruction schema is frozen;
- user-facing import/retirement/normalization choices are decided;
- exact-engine compile plus the automated gates pass in an isolated branch.
