# The living city — simulation architecture

Date: 2026-08-21. Mandate: `BUILDING-CATALOGUE-BRIEF.md` **Addendum 12**, composing with
Addenda 8 (the time doctrine), 10 (brink moderation, typed wear consequences, ruins), and 11
(grounded production, the harvest cycle, extend-the-real-machines). Head at writing: `4b128cb`.

Status: **design, not a build card.** Nothing here is implemented. The wave plan in §7 is the
order the build cards should be cut in. No code was edited to write this.

> The author's ask, in one line: *a larger city is a SIMULATION, not a set of decorated zones —
> it produces and consumes items, fires events, hosts activities, carries meaning and engagement,
> and changes state meaningfully over world-time, attended or not; built with or alongside the
> vanilla engine, as close to vanilla as we can, and optimised.*

---

## 0. Ground truth — what stands, and what the engine will not do

### 0.1 The engine constraint that decides the whole design

Four facts, verified in the decompile at
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` (call it `D/`), pinned here
because everything below is a consequence of them:

| Fact | Evidence |
|---|---|
| **A zone stays live for 40 turns after you leave it, and keeps simulating.** `Zone.GetSuspendabilityTurns()` returns `40` (`5` with `Options.CacheEarly`). Inside that window the zone is in `CachedZones`, not `Suspended`, and `ProcessSingleTurn` **does** tick it and broadcast `EndTurnEvent` into it. There is a real ~40-turn shadow-simulation window, and it is the only free simulation the engine gives. | `D/XRL/World/Zone.cs:7413-7420`; `D/XRL/Core/ActionManager.cs:439-467` |
| **Past that, it suspends — and everything in it stops.** `SuspendZone` fires `SuspendingEvent`, strips that zone's actors out of the `ActionQueue`, and flips `Suspended = true`. Nothing is serialized and nothing is dropped: **the whole object graph stays in RAM.** But `ProcessSingleTurn` skips suspended zones, and `ShouldRemove` drops any live object whose `CurrentZone.Suspended`. Every vanilla producer (`LiquidProducer`, `Harvestable`, `Mill`, `ItemConvertor`, `FoodProcessor`, `SolarArray`) runs off `TurnTick`/`EndTurnEvent` and therefore stops dead. | `D/XRL/World/ZoneManager.cs:1682-1712`; `D/XRL/Core/ActionManager.cs:430-467`; `VANILLA-PRODUCTION-TRUTH.md` §0 |
| **On the very next turn it is frozen to disk and released.** `Zone.GetFreezabilityTurns()` returns **`0`**, and `GetPendingZoneFreezeThreshold()` returns **`1`** on ordinary hardware — so one suspendable zone triggers an immediate Brotli-compressed SQLite write, `Zone.Release()` (every cell cleared, every object pooled) and a forced GC. | `D/XRL/World/Zone.cs:7467-7470`; `D/XRL/World/ZoneManager.cs:883-966, 976-1040` |
| **Keeping zones live is possible and expensive.** `zone.MarkActive()` each turn, or returning non-`Suspendable` from `GetZoneSuspendabilityEvent`, or `SetCachedZone(zone)`, will hold a zone resident and simulating. The price: full per-turn CPU (its `TurnTick` plus an `EndTurnEvent` broadcast across 2,000 cells), full RAM, its actors back in the `ActionQueue`, **and every live zone written inline into the save file** (`ZoneManager.Save` writes `CachedZones` in full; frozen zones are ids only). | `D/XRL/World/Zone.cs:2304`; `D/XRL/World/ZoneManager.cs:1726-1780, 468-520` |
| **Pinning is not an escape.** `PinnedZones` has a hard cap of 3, and *exceeding it logs an exception and clears the entire set* — a silent, total loss of whatever the pins were protecting. STANDARDS §1 already forbids it ("never pin — lazy catch-up"). | `D/XRL/World/Zone.cs:7440-7449`; `STANDARDS.md` §1 |

**The consequence, stated once:** a four-zone City gets 40 turns of grace and then has at most one
zone alive. Holding the other three resident is *technically* available and costs three extra
zone-ticks a turn, three extra `EndTurnEvent` broadcasts across 6,000 cells, three zones' worth of
actors in the action queue, and three zones inline in every save — for a city that is meant to be
one of several the player founds. That is not "optimised", and it fails Addendum 12's own word.
**So: the city is a model, and a zone is a view of it.** Every "keep it warm" variant — pinning,
`MarkActive` spam, a `GetZoneSuspendabilityEvent` veto, `Options.DisableZoneCaching2` — is priced
here and rejected, and should not be re-proposed without new evidence.

### 0.2 The engine hooks that make the design cheap

Every `ZoneXxxEvent.Send` calls `The.Game.HandleEvent(E)` **and then** `Zone.HandleEvent(E)`, so a
game system and an object part can both hear the same event. None of the zone events sets cascade
bit 32 (`CASCADE_STOP_AT_ZONE`), so all of them reach every object in the zone.

| Hook | Contract | Evidence |
|---|---|---|
| `ZoneActivatedEvent` | Fires from `Zone.Activated()` for the **one** zone becoming active — on transition, and again on game load. Wrapped in the engine's own try/catch. `PrimePowerSystemsEvent` follows immediately. `KingdomSystem` already registers it. | `D/XRL/World/ZoneActivatedEvent.cs`; `D/XRL/World/Zone.cs:7775-7791`; `D/XRL/XRLGame.cs:1959` |
| `ZoneDeactivatedEvent` | Fires for the **outgoing** active zone, immediately before the incoming one activates. Free, unused by this mod today. | `D/XRL/World/ZoneDeactivatedEvent.cs`; `D/XRL/World/ZoneManager.cs:1904-1905` |
| **`SuspendingEvent`** | Fires from `SuspendZone` **before** `Suspended = true`, for **any** zone as it suspends — not only the outgoing active one. **This, not deactivation, is the true last-read moment**, because a deactivated zone goes on simulating for up to 40 more turns (§0.1). | `D/XRL/World/SuspendingEvent.cs`; `D/XRL/World/ZoneManager.cs:1682-1712` |
| `ZoneThawedEvent` | Carries `TicksFrozen`, the engine's own measure of time on disk. **Fires only on thaw from SQLite, never on wake-from-suspend** — so it is a useful cross-check and never a sufficient clock. | `D/XRL/World/ZoneManager.cs:863-878`; `D/XRL/World/Zone.cs:7772-7774` |
| `Before/ZoneBuilt/AfterZoneBuiltEvent` | Fire at generation for **any** zone, including ones the player never enters. Relevant to claiming and founding. | `D/XRL/World/ZoneManager.cs:3202-3206, 3283-3286, 3584-3588` |
| `Calendar` | `TurnsPerDay = 1200`, `TurnsPerHour = 50`; `CurrentDaySegment = (TimeTicks % 1200) * 10`; `IsDay()` is segment 2500–9123; `GetTime(int)` names eight bands. **The day-shape vocabulary already exists, in the game's own register.** | `D/XRL/World/Calendar.cs:11-35, 296-352` |
| **`IdleQueryEvent`** | **Vanilla's entire daily-life surface.** The `Bored` goal collects every object in the zone that wants the event, shuffles, and offers each the idle actor; **returning `false` claims that actor's turn** (the caller then spends 1000 energy and stops). Gate tag `AllowIdleBehavior`, veto tag `PreventIdleBehavior`. `Bored` bails out entirely when the actor is not in the player's zone. | `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330` |
| `Brain.Stay(Cell)` / `Brain.StartingCell` | With `Wanders = false`, `WandersRandomly = false` and no `NoStay` tag, an NPC **self-anchors** to where it first stands, and `Bored` walks it back forever. Zero code, zero per-turn cost of ours. | `D/XRL/World/Parts/Brain.cs:2056, 2507` |
| `ZoneRepair` (`IZonePart`) | **The engine's own accumulate-and-apply-N-units catch-up**, and a better precedent than `Temporary` for production: `BuildCounter += TimeTicks - LastTurn; LastTurn = TimeTicks; num = Max(1, BuildCounter / TurnsPerObject)`, then apply `num` units and self-remove when done. Driven off `ZoneActivatedEvent`. | `D/XRL/World/ZoneParts/ZoneRepair.cs` |
| `GenericInventoryRestocker` | Vanilla's canonical "things changed while you were away": `long num = TimeTick - LastRestockTick`, one roll amplified by the number of whole missed periods, plus the **item-protection protocol** — `_stock` ("I made this, I may destroy it"), `norestock` ("never touch"), `IsImportant()` (never destroy). | `D/XRL/World/Parts/GenericInventoryRestocker.cs:12-146` |
| `IGameSystem` | `Register(XRLGame, IEventRegistrar)` is the only live registration hook, re-run on load via `ApplyRegistrar` (so it must be idempotent). Dispatch is O(handlers for that ID). | `D/XRL/IGameSystem.cs:2877-2941`; `D/XRL/XRLGame.cs:357-384, 1944-1962` |

**Three traps worth naming, because each would have cost a wave to find in play:**

1. **`Turns` and `TimeTicks` diverge, and `EndTurnEvent` does not fire during world-map travel.**
   `TerrainTravel` advances `game.TimeTicks` directly — 300 to 900 ticks for a single parasang
   step, six to eighteen in-game hours — and increments `game.Turns` **not at all**, running one
   batched `ProcessTurnTick` instead of per-turn `EndTurnEvent`s. **Any clock that counts turns or
   counts `EndTurnEvent`s silently under-counts exactly the activity that generates absence.**
   `The.Game.TimeTicks` is the only correct clock. (The mod already uses it everywhere; this is why
   it must keep doing so, and why `EndTurnEvent` is rejected as the city clock in §2.1.)
   — `D/XRL/World/Parts/TerrainTravel.cs:161, 221-264`; `D/XRL/Core/ActionManager.cs:1640-1662`
2. **`IGameSystem`'s convenience overrides are dead code.** `ZoneActivated(Zone)`, `EndTurn()`,
   `NewZoneGenerated(Zone)`, `GetPriority()`, `SaveGame`/`LoadGame` are all `[Obsolete]` **and have
   no call site anywhere in the engine.** Only registered event IDs fire. `KingdomSystem` already
   does this correctly and must not be "simplified" toward the overrides.
   — `D/XRL/IGameSystem.cs:2954-3070`
3. **`The.Game.GetSystem<T>()` is a linear scan with a `GetType()` call per element.** Never call
   it inside a per-object or per-row loop; resolve once per pass into a local.
   — `D/XRL/XRLGame.cs:286-300`

### 0.3 What stands in the mod, and what it is worth to this design

- **`Simulation/Kernel/`** — `CounterRandom` (a draw is a pure function of `(seed, SemanticEventKey,
  drawIndex)`; no stream object, no cursor, so retries/logging/batching advance nothing),
  `SemanticEventKey` (`rulesVersion × settlementId × eventStreamId × kindCode × ordinal`),
  `TickMath.TryCountFixedPeriodDue` (closed-form due count, **deliberately uncapped**, with the
  standing instruction that consumers fold it into bounded aggregates rather than looping),
  `FixedPeriodToyState` (immutable by construction: copy, validate, compute into locals, publish
  one new instance — "nothing is ever partially incremented, so a fault leaves the caller's state
  byte-identical"). **This is the model's substrate, unchanged.** It was built for exactly this.
- **The attended pass** — `KingdomSystem.HandleEvent(ZoneActivatedEvent)`: seat → exile → seceded
  → *claim guard* → survey → trade → growth → improvement → bounties → raids → wear → offices →
  reach → locus → guestbook → creed → faith → digest. Every step wrapped in `Guard(label, action)`.
  The order is load-bearing and documented step by step. **This is the reckoning's host.**
- **Zone sightings** — `KingdomSubsidence.RecordZone` writes five ints per claimed zone into
  `The.Game` game-state (`r_TAF_Supports_<zoneID>_{water,food,roof,storage,seen}`, dated in whole
  days by `SeenStamp`); `OtherZones` reads them back as `ZoneSighting`s; `CityTally` /
  `CityStorage` fold them into one city. Its own doc says the quiet part: *"Nothing here simulates
  an unvisited zone forward — a sighting is dated, stays exactly as old as it is."* **This is the
  city model in embryo, five ints deep. The design generalises it; it does not replace its
  discipline.**
- **Crystallise-at-awareness** — Addendum 11(b-ii) already rules the harvest cycle: growth on
  world-time from a stamped planted-tick, harvest crystallises when due attended or not, the city's
  stores are credited at once while the **physical crop items materialise into the destination
  larder when that larder's zone is next active**, and the cycle restamps and repeats. That ruling
  is this architecture's materialisation contract, written before the architecture was.
  Its engine precedent is now doubly confirmed: `Temporary` for a one-shot deadline, and
  **`ZoneRepair`** for accumulated work applied in a batch on `ZoneActivatedEvent`
  (`BuildCounter += TimeTicks - LastTurn; num = Max(1, BuildCounter / TurnsPerObject)`) — the closer
  analogue for production, and the shape a work's run-state advance should be written in.
- **Brink windows** — `KingdomBrink` + `KingdomBrinkRules` + `KingdomWord`: warn at the crossing,
  coach the arrest, run the window in world-days, fire in absence, unsay on arrest. `KingdomWord`'s
  own doc-comment predicts this wave: *"If a future wave gives the realm a pass that runs without
  the founder, this is the one place that has to learn to hold a letter."*
- **The closed-form idioms already shipped** — `KingdomRules.PassagesThrough` (a repeating arrival
  clock run forward over an arbitrary absence, returning counts and one standing visitor, never a
  queue), `KingdomRules.RestampDeadline`, `ElapsedDays` / `AdvanceCheckpoint` (remainder kept,
  never re-anchored), `ActivityDays` / `LabouredTicks`, and — the reference implementation for
  everything in §2 — `KingdomSubsidenceRules.Slide`, *"closed-form convergence: the whole slide is
  computed at once from the elapsed, and its rung changes come back as dated breakpoints for the
  chronicle."*
- **Ledgers and chronicle** — `KingdomLedger` (per-visit arithmetic, brink lane first, ≤12 notes,
  ≤8 brink lines), `KingdomChronicle` (200 entries, disputed two-register recording),
  `KingdomReports`, and the telling budget the slide already keeps (`NamedDeparturesPerSlide`,
  `ChronicleEntriesFor`, `ChronicleBudgetPerSlide`).
- **The physical half** — `KingdomSurvey.Take(Zone, System)` walks a zone and reads *real* objects:
  `LiquidVolume`s, `Container`/`Inventory` larders, settlers, works, beds, defences. Stocks are the
  zone's own objects and always have been. `KingdomLiquids.Drain`/`Fill` measure the state delta
  rather than trusting a vanilla return value.

### 0.4 The three places the standing architecture cannot carry Addendum 12

1. **Stocks live in the zone.** `KingdomSurvey` reads the ground under the founder's feet.
   A granary in the zone next door is five ints old. A city that *produces and consumes* across
   four zones cannot be measured that way.
2. **People live in the zone.** `KingdomGrowth.SpawnSettler` mints a real `GameObject`, and
   per-settler mod state — the roof brink's `KingdomBrinkRoofTick`, the creed brink's window,
   origin, name — is stored **as properties on that object** (`KingdomBrink.RoofTickProperty` and
   siblings). A settler in a suspended zone is therefore unreadable and unmovable: their window
   cannot run, their work cannot be counted, their wedding cannot happen.
3. **The Charter is full.** 32 options, every letter `a`–`z` and the digits `1`–`6` spoken for
   (`KingdomCharterPart.OpenMenu`, and the comment above it says so). **Engagement cannot add a
   parallel surface. It must deepen the ones that exist.** This is a hard constraint on §5, and
   a good one.

---

## 1. The model

### 1.1 The governing idea

> **The city is a book. A zone is a page of it that happens to be open.**

Authority alternates, explicitly, and never overlaps:

- **While a zone is attended, the ground is authoritative** and the model is a mirror. The player
  may pour water out of a cask by hand and the model must agree with the cask.
- **While a zone is suspended, the model is authoritative** and carries that zone's last-read
  numbers plus everything credited or drawn since.
- The handoff is a **check-out** (ground → model) and a **check-in** (reconcile, then materialise).

This is not new doctrine. It is `RecordZone`'s existing discipline — *"rewritten from the ground
every time, including down to zero: a reservoir that was struck stops counting toward the city the
pass the founder sees the empty plot, and never before"* — widened from five ints to the whole
city, and given the other half (the model runs forward between sightings) that Addendum 12 asks for.

### 1.2 What the model owns

One `KingdomCityState` per settlement. Proposed home: `Simulation/City/`, beside the kernel,
because it is simulation substrate rather than a feature.

**(a) Stocks — the civic share only.**

| Row | Contents |
|---|---|
| `Water` | Drams in **dedicated** vessels, city-wide. |
| `Food` | Servings in **dedicated** larders, city-wide, with the `PantryTier` classification the survey already computes. |
| `Materials` | The refined tiers `KingdomMaterials` already names (shaped timber / shaped stone / worked metal), plus raw. |
| `Capacity` | Per-kind ceiling, summed from the zone rows. |

Player-carried and player-placed-but-undedicated stock stays purely physical and **outside the
model**, exactly as the survey already classifies it. This is what keeps the protection law simple:
the model only ever speaks for things the founder designated as the city's.

**(b) Zone rows — one per claimed zone, at most four.**

`ZoneId`, `District`, `LastReadTick`, and the stocks/capacity/roofs/defence read at that
check-out. Replaces the `r_TAF_Supports_*` game-state keys wholesale. `ZoneSighting` survives as
the projection these rows hand to `KingdomSubsidenceRules.CityTally`, so the subsidence arithmetic
does not change at all — it simply stops reading a dictionary of ints.

**(c) Works — one row per standing work, bounded by `KingdomRules.MaxBuildings` (40).**

`WorkId` (stable, minted at raising), `ZoneId`, `Anchor` (cell x/y, for materialisation and for
the ruins rule), `DesignKey`, `Condition` (the wear percent `KingdomWear` already owns),
`CrewAssigned`, `RanThroughTick`, and one small discriminated slot for kind-specific run-state:

- growing ground → `PlotStage` + `NextStageTick` + `CropBlueprint` (today's `r_KingdomPlot` fields,
  moved off the object);
- store → nothing beyond condition (the leak rule is already a function of condition and days);
- producer / refiner → `ProgressTicks` and the output kind;
- power → charge.

The rule that makes this bounded and honest: **a work's row carries state the engine cannot carry
for it, and nothing else.** Appearance, name, tile, contents stay on the `GameObject`. The row is
not a second copy of the building.

**(d) Residents — one row per settler, bounded by `KingdomRules.MaxPopulation` (60).**

`ResidentId` (stable), `Name`, `Origin`, `Creed`, `Arrived`, `HomeWorkId`, `Job` (`WorkId` + role),
`DayShape` (below), `Standing` (`Resident` / `Abroad` / `Dead`), `BoundZoneId`, and the brink
window fields that today live as object properties (`RoofTick`/`RoofWarned`,
`CreedTick`/`CreedWarned`/`CreedToward`/`CreedChannel`).

`DayShape` is a small enum naming where a person's day puts them —
`Field`, `Yard`, `Market`, `Craft`, `Watch`, `Shrine`, `Hearth` — derived from `Job` and the
settlement's standing policy, never authored per-settler. It is read against `Calendar`'s own
bands (§3.2). It is **not** a schedule object and holds no times: the mapping from band to place
is a pure function in `KingdomCityRules`.

**(e) Clocks — a handful of named `(kind, nextDueTick, ordinal)` triples.**

Harvest per growing work, arrival, guest, notable guest, festival, market day, delivery, raid.
Most of these already exist as `long` fields on `KingdomSettlement` (`NextArrivalTick`,
`NextGuestTick`, `NextNotableGuestTick`, `RaidDueTick`); they consolidate here and gain an
**ordinal**, which is what makes their draws reproducible (§2.4).

**(f) The told-log — a bounded ring of the last K happenings.**

`(Kind, Tick, SubjectIds, PlaceZoneId, Outcome)`, K ≈ 32. Feeds the "As others tell it" register,
the guestbook, and the told-once guarantee. **Not a queue of pending work** — every entry in it has
already happened. This distinction is the kernel's own, stated in `FixedPeriodToy`: *"This is
historical identity proof, not a due-job queue: current scheduling lives in `NextDueTick`."*

### 1.3 Where it lives, and how it is frozen

`KingdomSettlement` is already an `IComposite` with `WantFieldReflection => false` and named-field
serialization — the durable-system opt-out STANDARDS §1 requires. The model is one field on it:

```
KingdomSettlement.City : KingdomCityState        // named-field composite, versioned
```

Not `The.Game` game-state string keys. Those were the right answer for five ints that had to be
readable without loading a zone; they are the wrong answer for a hundred typed rows, and they
retire in Wave 1.

**Frozen-model doctrine, in the shape this codebase already uses.** The house rule is not "make the
serialized carrier immutable" — Qud's named-field reader must assign fields. It is the
`FixedPeriodToyState` rule:

- The **rules** layer (`KingdomCityRules`, engine-free, `Simulation/City/`) takes an immutable
  snapshot in and returns a new immutable snapshot out. `readonly struct` rows, `sealed` state,
  every `Try*` total over representable input, publishing nothing on a fault.
- The **carrier** (`KingdomCityState : IComposite`) is written by exactly **one** publisher, in one
  assignment, after the rules have succeeded. Nothing is ever partially incremented; a fault leaves
  the settlement byte-identical and the pass's `Guard` logs it.
- State transitions are copy-on-write. There is no in-place mutation of a row anywhere outside the
  publisher.

This is the same contract the kernel already keeps and the same reason it keeps it: a partially
advanced model that survives into a save is a wrong answer that outlives the bug.

### 1.4 How it stays bounded

Every dimension has a cap that already exists or is added here, and **no dimension grows with
elapsed time**:

| Dimension | Cap | Source |
|---|---|---|
| Cities | 2 | `KingdomSettlement.MaxSettlements` |
| Zones per city | 4 | `KingdomZoningRules.ZonesForStage` (Camp/Steading 1 … City 4) |
| Works | 40 | `KingdomRules.MaxBuildings` |
| Residents | 60 | `KingdomRules.MaxPopulation` |
| Clocks | ~12 | fixed set, named |
| Told-log | 32 | new, ring |
| Breakpoints per reckoning | 64 | new, with an honest overflow (§2.3) |
| Chronicle | 200 | `KingdomChronicle.MaxEntries` |
| Ledger notes / brink lines | 12 / 8 | `KingdomLedger` |

Worst-case model: ~2 × (4 + 40 + 60 + 12 + 32) rows ≈ **300 rows, a few kilobytes**, inside a block
the save already writes.

---

## 2. The clock

### 2.1 Where it runs

One new step, `reckon`, in the existing guarded pass order, placed **after `survey` and before
`trade`**:

```
seat → exile → seceded → [claim guard] → survey → checkin → RECKON → trade → growth →
improvement → bounties → raids → wear → offices → reach → locus → guestbook → creed → faith →
MATERIALISE → digest
```

The placement is load-bearing for the same reason `trade` runs before `growth` is: what the city
made while the founder was away has to be in the stores **before** upkeep is drawn from them, or a
harvest that landed on the day of the drought would arrive one step too late to stop the emigration
it prevented. `materialise` runs last-but-one, after every system has settled its numbers, so the
zone renders a decided state rather than an intermediate one.

**The realm reckons all its cities at any settlement pass, not just the seated one.** The founder
walking into city A reckons city B as well. Cost is trivially bounded (2 cities), it is what makes
the second city alive instead of frozen, and it is what Addendum 10(a) already implies — *word
reaches the player wherever they are*. Only the *seated* city materialises; the other is reckoned
and told, never rendered. This preserves `KingdomWord`'s send-not-outbox property exactly: word is
still only ever made at a moment when there is a founder standing somewhere to hear it.

**Why not a per-turn hook.** `EndTurnEvent` reaches every `IGameSystem` every turn regardless of
which zones are loaded, and looks like the obvious home for a city clock. It is the wrong one, for
a reason that only shows up in play: **it does not fire during world-map travel.** `TerrainTravel`
advances `The.Game.TimeTicks` by 300–900 per parasang step and runs one batched `ProcessTurnTick`
instead. A city clock on `EndTurnEvent` would therefore stop precisely while the founder is doing
the thing that creates the absence. Reckon-at-activation off `The.Game.TimeTicks` deltas has no
such blind spot, costs nothing per turn, and is what the mod already does everywhere else.

### 2.2 The shape of an advancement

```
KingdomCityRules.TryAdvance(snapshot, nowTick, out next, out trajectory, out fault)
```

Pure, engine-free, total. `trajectory` carries dated breakpoints and a bounded happening list for
the telling layer; `next` is the new snapshot. `KingdomReckon` (engine-facing) calls it once per
city per pass, publishes on success, and logs on fault through `Guard`.

Idempotent by construction: the snapshot carries `ProcessedThroughTick`, advanced with
`KingdomRules.AdvanceCheckpoint` (previous + whole units consumed, remainder kept, **never
re-anchored to now** — re-anchoring forgives the remainder and the clock rework already retired
that). Calling `TryAdvance` twice at the same tick is a no-op, which is what makes a save/reload
mid-pass safe.

### 2.3 O(model), not O(days) — breakpoint integration

The naive shape — loop a day at a time — is forbidden: a season away is 90 iterations of a whole
city and grows without bound. The correct shape is the one `KingdomSubsidenceRules.Slide` already
uses and this generalises:

> **Between two consecutive breakpoints, every rate in the model is constant.** So integrate
> linearly to the next breakpoint, apply it, and repeat — and the number of breakpoints is bounded
> by the *model*, not by the *elapsed*.

A breakpoint is any moment a rate can change:

- a stock hits empty or full (a solvable linear crossing, computed, not searched);
- a crop's `NextStageTick`;
- a periodic clock's next due tick;
- a brink window's expiry;
- a subsidence rung change (`Slide`'s existing `Breakpoint`);
- a stage change, which changes upkeep and therefore every rate at once.

Each of those is *computed* as "the tick at which this will happen at the current rates", the
minimum is taken, and the model steps there. Because every step consumes at least one structural
change, and the model has a bounded number of structural changes available, the loop terminates in
O(model). The 64-cap is belt-and-braces, and its overflow is honest rather than silent: on hitting
it, the model **jumps to the fixed point** — the equilibrium `KingdomCatalogueRules.Equilibrium`
already computes — and dates the remainder as settled. That is not a forgiveness cap in disguise;
it is the same convergence the slide already promises, reached by arithmetic instead of by steps.

Two shapes carry most of the work and both are already shipped:

- **Fixed-period lanes** (harvest, arrivals, guests, festivals, deliveries) use
  `TickMath.TryCountFixedPeriodDue` — O(1) for a count of any size — folded into a bounded
  aggregate by the consumer, exactly as its doc-comment demands. `KingdomRules.PassagesThrough` is
  the shipped example of a consumer doing it right: *a run that came and went unwitnessed is one
  dated line, not a queue standing since spring.*
- **Rate lanes** (wear, conversion, dissent, road traffic) use `ActivityDays` / `LabouredTicks`:
  days scaled by how hard a thing was actually run. One multiply per work or resident. Addendum 8
  clause 2 — *rates are time × labour × infrastructure, never time alone* — is enforced here and in
  exactly one place.

### 2.4 Where determinism draws anchor

One `KernelSeed128` per realm, minted at founding and stored on `KingdomSystem`, domain-separated
on realm incarnation (the kernel's own instruction: *"live seed generation belongs to the founding
slice"*). Every draw in the reckoning is:

```
CounterRandom.TryDrawBelow(seed, SemanticEventKey(rulesVersion, settlementId, streamId, kindCode, ordinal), drawIndex, bound)
```

- `streamId` is per-lane and per-source: `taf:stream:field.7`, `taf:stream:happening`,
  `taf:stream:arrival` — a distinct stream per source, never a stream-global counter, because
  `SemanticEventKey`'s own doc says two routes at ordinal zero must not collide.
- `ordinal` is the **occurrence index within that stream**. This is the whole trick: the seventh
  harvest of field 3 draws the same numbers whether it is resolved on the day it fell or six cycles
  later inside one reckoning, and a reload reproduces it. It is why the counter-random has no
  cursor.
- `rulesVersion` is frozen into the key at creation, so a rules upgrade cannot retroactively
  re-roll a harvest the chronicle already described. `KingdomSubsidenceRules.RollRuin` already
  relies on exactly this property.

**Nothing in the reckoning may draw per day.** Draws are per *happening*, per *occurrence* — never
per unit of elapsed time. This is both a determinism rule and the performance rule (a SHA-256 per
day per resident would be the only thing here that scales with absence).

---

## 3. Materialisation

### 3.1 Check-in — reconcile before rendering

On `ZoneActivatedEvent` for a claimed zone, after `survey` and before `reckon`:

1. Read the ground. `KingdomSurvey.Take` already does this and does not change.
2. Compare against the zone row's last check-out.
3. **The ground wins for anything physical.** A cask with less water in it than the model expected
   means the founder poured some; a struck work means it is gone; a dead settler means they died.
4. **The difference is attributed and told, never silently repaired.** Signed against a plausible
   cause: a withdrawal the founder made, a raid's plunder already accounted, or unexplained loss.
   The mod's own voice on this is already written, about the manifest: *"water arrives with nowhere
   to go, and that is a story rather than a bug."*
5. Set the zone row's `LastReadTick` to now.

The invariant to test: **model total == ground total immediately after an attended pass of a
fully-visited city**, and a hand-moved dram neither mints nor destroys a dram.

### 3.2 Render this zone's share

After the whole pass has settled, `materialise` renders the seated zone. Three kinds of output,
each capped per activation.

**(a) Items into containers.** The harvest that fell while the founder was away credited the city's
stores at the moment it was due (Addendum 11(b-ii)); the *physical* crop items are created into the
destination larder's real `Inventory` now, on the pass that opens that larder's zone. Same for
refined materials into stockpiles and for water into dedicated `LiquidVolume`s via
`KingdomLiquids.Fill` — which measures the delta rather than trusting `AddDrams`, per STANDARDS §1.
Capped per activation; the remainder stays credited in the model and materialises next time, and
the overflow line the ledger already has (*"left in the field for want of a larder"*) is the shape
for anything that genuinely cannot land.

**Adopt vanilla's item-protection protocol verbatim.** `GenericInventoryRestocker.PerformRestock`
is the engine's own answer to *which items may I destroy*, and it is exactly the discipline the
mod's protection law wants:

- `_stock` — "the simulation created this; the simulation may remove it";
- `norestock` — "never touch, whoever put it here";
- `IsImportant()` — never destroy, no exceptions.

Materialised items carry `_stock`. Anything the player added to a larder by hand does not, and is
therefore untouchable by anything we do. This gives the reconciliation in §3.1 a clean rule for
*who moved what* at no design cost, and it is a vanilla convention rather than an invention.
(`D/XRL/World/Parts/GenericInventoryRestocker.cs:148-200`.)

**(b) People, by role and by the hour.** Vanilla ships **no** NPC scheduler — no
`GoToPartyLocation`, no `Schedule` class, no calendar-driven villager behaviour anywhere. What it
ships is one hook, and it is enough:

- **The anchor is free.** A settler with `Brain.Wanders = false`, `WandersRandomly = false` and no
  `NoStay` tag **self-anchors** to the cell it first stands in (`Brain.StartingCell`, set on the
  first `EnteredCellEvent`), and the `Bored` goal walks it back there forever. `Brain.Stay(cell)`
  sets it explicitly. Materialisation therefore does not *place* people so much as **move the
  anchor** — and vanilla's own AI does the walking. (`D/XRL/World/Parts/Brain.cs:2056, 2507`.)
- **The daily life is `IdleQueryEvent`.** `Bored` gathers every object in the zone that wants the
  event, shuffles them, and offers each one the idle actor; **returning `false` claims that actor's
  turn.** This is *literally* how vanilla beds send villagers to sleep at night — `Bed` gates on
  `IsNight()`, pushes `MoveTo(bed)` then a `DelegateGoal` that sleeps on arrival, and returns
  `false`. There is no other mechanism in the game. (`D/XRL/World/Parts/Bed.cs:187-224`;
  `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330`.)

So the design is: **the model decides where a person belongs at this hour; the anchor is set at
activation; and an `r_` part on the workplace claims them through `IdleQueryEvent` while the
founder watches.** Concretely — one small part, `r_KingdomStation`, on each work, handling
`IdleQueryEvent`: if the actor's `KingdomResidentId` is rostered to this work, and the current
`Calendar` band matches the role's, push `MoveTo(this)` plus a `DelegateGoal` for the flavour
(tend, haul, pray, keep watch) and return `false`.

| `Calendar` band | Ticks (of 1200) | Where a `DayShape` puts a person |
|---|---|---|
| The Shallows / Harvest Dawn | 151–450 | rising: hearth → workplace |
| Waxing / High / Waning Salt Sun | 451–750 | `Field`, `Yard`, `Craft`, `Market`, `Watch` at post |
| Hindsun | 751–900 | trades wind down; `Market` and `Shrine` busiest |
| Jeweled Dusk | 901–1050 | homeward; `Hearth` fills |
| Waxing Beetle Moon → Zenith → Waning | 1051–150 | `Hearth`, except `Watch` — and vanilla `Bed` already does this one for us, free, for any settler tagged `SleepOnBed` |

This is the whole reason to ride the hook rather than teleport people: **the founder standing in
the city at dusk sees the market empty itself and the hearths fill, one settler at a time, walking**
— and it costs us one `IdleQueryEvent` handler and zero per-turn work. Settlers need
`AllowIdleBehavior` and `SleepOnBed`; vanilla's `NPC` blueprint already grants both, and the
`r_KingdomSettlers` population table is where that is arranged.

Three constraints the hook carries, and all three are fine:
1. `Bored` does nothing when the actor is not in the player's zone — idle behaviour is
   attended-only, which is exactly the division of labour this architecture wants.
2. Returning `false` costs the actor its turn, so a station must be selective (vanilla's own
   handlers gate on `1.in100()` / `1.in10000()` plus a per-object cooldown) or the settlement
   stands around doing one thing.
3. The `IdleObjects` cache is zone-scoped and rebuilt on `IdleDirty`; a station added mid-play is
   picked up by `WantEvent`, so no registration list to maintain.

**The bodies.** A resident's `GameObject` is a **view bound by id**, never the state. See §8, hard
problem 2 — the short form is: materialisation *mints* a body only for a resident with no living
bound body in this zone, *moves* an existing one, and **never removes one**. The protection law is
not weakened; it is extended to our own people.

**(c) Dated events, told once.** Happenings the reckoning generated surface through the ledger
digest (pull, at the seat), `KingdomWord` (push, for brinks and irreversibles), and the chronicle —
each dated to when it happened, not to the pass that found it, which is already how
`KingdomWord.Aftermath` is specified. Told-once is a `TellingsThroughTick` stamp in the model, so
re-entering the zone does not retell.

### 3.3 Vanilla does the visible work (Addendum 11(c) order)

Materialisation's job is to **set the ground up so that vanilla parts then do the thing**, never to
puppet what a vanilla part would do while attended:

1. **Inherit-and-extend** — the work's blueprint genuinely is a `LiquidProducer` / `Mill` /
   `ItemConvertor` / `FoodProcessor` / `SolarArray` / `Capacitor`, and while the founder stands
   there it visibly works on the engine's own clock. Its output is *not* the number the economy
   reads (VANILLA-PRODUCTION-TRUTH's settled rule: *"Vanilla parts are the visible face of
   production. The settlement's own tick-stamped pass is the accounting."*)
2. **Wrap** — an `r_` part driving a vanilla part's real behaviour on our clock, the way
   `r_KingdomPlot` does today (absolute-tick comparison in `TurnTick`, so missing ticks costs
   nothing, unlike vanilla's dead `Harvestable.RegenTimer`).
3. **Fill in** — only where the survey proved vanilla is empty: seeds, spoilage, multi-stage
   growth, pressing/fermenting, inter-zone haulage. All four are already on
   VANILLA-PRODUCTION-TRUTH §8's inspire-only list, with the reason.

### 3.4 Check-out — what un-materialises

**Nothing is destroyed.** Un-materialisation is a *read*, not a teardown. Three hooks, in the order
they fire, and only the last is load-bearing:

1. **`ZoneDeactivatedEvent`** — the founder walked out. Useful as a *hint* (stamp "left at tick T"),
   but **not** the moment to take the numbers, because the zone goes on simulating for up to 40 more
   turns (§0.1): vanilla producers keep producing, effects keep ticking, and a reading taken here
   would be wrong by whatever happened in the grace window.
2. **`SuspendingEvent`** — the true last read. Fires from `SuspendZone` *before* `Suspended = true`,
   for **any** zone as it suspends, and reaches `The.Game`. At this instant the zone is still fully
   in RAM and nothing further will happen in it. **Take the numbers here.** This is the single most
   useful hook the survey turned up and the mod does not use it today.
3. **Lazy check-in reconcile — the correctness guarantee.** Neither event fires on save-and-quit, on
   a crash, or on any path that bypasses the transition. So the model must *also* be correct when
   check-out never happened — which it is, because check-in reconciles against the ground anyway
   (§3.1). **Design so that a missed check-out costs freshness, never correctness.** Everything
   above is an optimisation on top of that.

Bodies and items stay in the zone and are frozen to disk with it (`FreezeZone` → Brotli → SQLite →
`Zone.Release()`). `ZoneThawedEvent.TicksFrozen` is available as a cross-check on how long they were
gone, and is deliberately **not** used as a clock: it measures frozen time only, and says nothing
about suspended-but-resident time.

## 4. Events with meaning

### 4.1 What a happening is

A happening is **derived**, never authored ad hoc:

```
Happening = (Kind, Tick, SubjectIds, PlaceZoneId/WorkId, Outcome)
```

generated inside `TryAdvance` from model state plus kernel draws on the `taf:stream:happening`
lane. Every kind binds to machinery that already exists, so the happenings layer is a *generator
and a budget*, not a second simulation:

| Kind | Generated when | Rides |
|---|---|---|
| **Wedding** | two residents' cohabitation closeness crosses a band | `KingdomLodgingRules.Closeness`, `KingdomConversionRules` (cohabitation-days), `KingdomCeremony` |
| **Funeral** | a resident's `Standing` becomes `Dead` | the `DeadNames`/`DeadOrigins`/`DeadCauses`/`MemorialsRaised` roll that already exists |
| **Feast** | the festival clock, or the founder calling one | `KingdomLarder.HoldSharedMeal` → `KingdomCreed.EaseForMeal` + `KingdomConversion.OnSharedMeal` |
| **Festival** | a creed's own calendar | the faction-level `<waterritual Recipe= RecipeText= RecipeGenotype=/>` — vanilla's *actual* favourite-dish vocabulary, eight of them in `Factions.xml`, and exactly what Addendum 11(b) asked for. A creed's festival wants its dish; the larder holds it or it does not, and the second is a grievance. |
| **Quarrel** | creed pressure / resented conversion crossing its band | `KingdomConversion.Convert` — the **one** path a conversion may take — and the `Creed` brink |
| **Delivery** | a haulage clock between two zones or two cities | `KingdomManifest`, `KingdomTrade`, and the carry-sign's distance-scaled hauls |
| **Breakdown** | condition crossing a typed threshold | `KingdomWear` + Addendum 10(b): stores leak, power works lose output, in the work's own kind |
| **Raising** | a scaffold/plot completing on world-time | `KingdomCeremony.OnBuildingRaised` — which already tells itself two ways, attended (the crew gathers, water is shared, the chronicle names who was there) and unattended (the homecoming tells it) |

The rule that keeps this from sprawling: **a happening kind may not own state.** It reads the
model, draws, and writes an outcome through an existing system's one true path. If a kind needs a
new field, that field belongs to the system that owns the concept, not to the happenings layer.

### 4.2 The budget

A season away can generate a hundred happenings. The register holds 200 entries total. So the
**telling** is budgeted, using the shape the slide already ships and generalising it into one
shared `KingdomTellingBudget`:

- the first few by name, the last by name, one summary line for everybody in between
  (`NamedDeparturesPerSlide` / `SlideDepartureSummary` is the template, and
  `NamedRuinsPerBreakpoint` / `RuinSummary` is the second instance of the same pattern);
- per-lane caps that sum to the ledger's own ceilings (≤12 notes, ≤8 brink lines);
- a hard chronicle budget per reckoning, so a City→Camp collapse plus a season of weddings cannot
  eat the book — the existing `ChronicleEntriesFor` already holds the line against this for one
  lane and should hold it for all of them;
- the told-log ring keeps the rest for the outsider register and the guestbook, where a line that
  did not make the chronicle can still be *heard about*.

**Generation is not telling.** The model may know about a hundred happenings; the founder is told
about a dozen. Everything else is in the ring, in the counters, and in the state that changed.

### 4.3 Surfacing

- **Ledger digest** (pull) — the homecoming report, brink lane first. Unchanged shape.
- **`KingdomWord`** (push) — brinks and irreversibles, wherever the founder is standing, framed by
  whether they are in the city the news is about. Unchanged contract.
- **Chronicle** — what the book should hold, with the disputed two-register recording for anything
  an outsider would tell differently.
- **`KingdomReports`** — status gains the model's flows ("the fields make 7 a day; the settlement
  eats 9"), which is the single most useful thing a living city can say and which today it cannot.

---

## 5. Engagement — what the player does that they cannot now

**Constraint first: the Charter is full** (32 options, every hotkey taken). Everything below
deepens an existing surface. No new top-level entry, no parallel system.

| Existing surface | What it becomes |
|---|---|
| **"Your works, and what they become"** (`y`) | The **works board**: every work in the *city*, not the zone — its run-state, crew, output per day, condition, and what it is waiting on. This is where the simulation becomes legible. Without it the model is invisible and the whole wave is worthless. |
| **"Set the crew on the ground"** (`x`) | The **labour dial**, city-wide. Fields vs yards vs watch vs haulage. The single most meaningful lever a living city offers, and the one the mod's own crew machinery (`KingdomCrews`, `CrewNeeds`, ablest-first assignment) was built for. |
| **"Standing policy"** (`p`) | Gains the **day shape**: market days, the festival calendar, whether the watch stands at night. Model-only, cheap, and it changes what the founder *sees* when they walk in — which is the point. |
| **Petitions** (`h`) | Petitions issue **from model state**: the granary is full and nothing hauls it; the shrine has no keeper; the road is unpaved. Today petitions are decorative; against a model they are the city talking. |
| **Bounties** (`1`) | The city posts what it **cannot do itself**: haul 200 drams to the second city, mend the reservoir, bring seed for a new crop. The founder becomes a contractor to their own city — the strongest engagement available, needing no new surface at all. |
| **Ceremonies** | Occasions come from happenings (raising, wedding, funeral, festival), and **attending is worth more than not** — the raising already distinguishes attended from unattended, and every other occasion should. This is what makes being present a choice rather than a chore. |
| **Guests / guestbook** (`j`, `o`) | Guests arrive **because** of what the city did: a festival draws pilgrims, a breakdown draws a tinker, a wedding draws kin from a named origin. The hook-decays-into-rumour machinery already exists. |
| **The rite, the shrine, the creed** | Festivals give the faith machinery a calendar instead of only a lever. A creed's favourite dish, held or missed, is the cheapest meaningful stake in the mod. |

**The new thing, in one sentence:** the founder can leave on purpose, come back to a city that is
measurably different, walk in at Jeweled Dusk and find the market shut and the hearths full, read
what happened and who it happened to, and act on a list of things the city itself is asking for.
None of that is possible today.

---

## 6. Vanilla proximity and the cost model

### 6.1 Where we ride vanilla

| Concern | Vanilla surface |
|---|---|
| System lifecycle | `IGameSystem`, `Register(XRLGame, IEventRegistrar)`, named-field `IComposite` serialization |
| Pass trigger | `ZoneActivatedEvent` (already), `SuspendingEvent` (new — the true last read), `ZoneDeactivatedEvent` (hint), `ZoneThawedEvent.TicksFrozen` (cross-check) |
| Absence catch-up | `ZoneRepair`'s accumulate-and-apply-N-units (`BuildCounter / TurnsPerObject`) and `GenericInventoryRestocker`'s stamp-and-compare — both engine-authored |
| Item ownership | `_stock` / `norestock` / `IsImportant()`, vanilla's own protection protocol |
| Daily life | `IdleQueryEvent` + `Brain.Stay` / `StartingCell` + the `Bored` goal — the entire vanilla surface, and the same one `Bed` uses |
| Absence idiom | `Temporary`'s tick-stamp catch-up — the engine's own answer, ratified |
| Stocks | real `LiquidVolume`, real `Container`/`Inventory`, via `KingdomLiquids`' measure-the-delta adapters |
| Visible production | `LiquidProducer`, `Mill`, `ItemConvertor`, `FoodProcessor`, `SolarArray`, `Capacitor`, and the real power grid (`IPowerTransmission` cardinal flood-fill) |
| Time-of-day | `Calendar.IsDay()`, `CurrentDaySegment`, `GetTime(int)` — the game's own eight bands |
| People | `GameObject.Create`, `NameMaker`, `ConversationsAPI`, `PopulationManager` (`r_KingdomSettlers` table, mergeable) |
| Festival dishes | faction `<waterritual Recipe=…/>` |
| Surfaces | `Popup`, `MessageQueue`, `JournalAPI` accomplishments |

### 6.2 Where we greenfield, and why

Each of these is on VANILLA-PRODUCTION-TRUTH §8's proven-empty list, or is simply not a thing an
engine provides:

the city model and its rows; closed-form breakpoint advancement; the happenings generator and the
shared telling budget; the resident day-shape and placement; seeds and planting; multi-stage crop
growth; spoilage; inter-zone haulage; the reconciliation protocol. Nine things, all of them
arithmetic and bookkeeping, none of them fighting the engine.

### 6.3 The cost model

**Per zone activation — O(1) in elapsed time.**

| Step | Cost |
|---|---|
| survey | one zone walk — **already paid today**, unchanged |
| check-in reconcile | O(objects surveyed) on the *same* walk's results — no second walk |
| reckon | see below |
| materialise | O(residents bound to this zone + items minted), both capped |

**Per reckoning — O(model), independent of days.**

For the worst case in the mandate — a 4-zone City, 60 residents, 40 works, one season (90 days)
away:

- breakpoint loop: ≤ 64 steps, each O(works + residents) ≈ 100 → **≤ ~6,400 row-visits**, plain
  integer arithmetic;
- fixed-period lanes: O(1) each via `TryCountFixedPeriodDue`, ~12 of them;
- draws: one per *happening*, not per day — a season of a busy city is tens, not thousands, of
  SHA-256 blocks;
- telling: capped at ~12 ledger notes, ≤8 brink lines, ~6 chronicle entries.

That is sub-millisecond, once, on the pass the founder walks in. **The number to hold ourselves
to: the reckoning cost of a season away must equal the reckoning cost of a day away, plus a bounded
constant.** If any future lane makes that false, it is the lane that is wrong.

**Memory and save size**: ~300 rows per realm, a few kilobytes, inside a block the save already
writes. Contrast the rejected alternative: every zone held live is written **inline into the save
file** (`ZoneManager.Save` serialises `CachedZones` in full), so three extra live zones is three
zones' worth of save bytes and save latency on every autosave. The model is cheaper than the
engine's own answer by two orders of magnitude.

**The free 40 turns.** The shadow window (§0.1) is a gift, not a problem: a founder who steps into
the next zone of their own city and back again finds the first zone still live and still ticking, so
short local movement inside a city needs no reckoning at all. The reckoning is for real absence.

**One micro-rule with teeth**: resolve `The.Game.GetSystem<KingdomSystem>()` **once per pass** into
a local. It is a linear scan over `XRLGame.Systems` with a `GetType()` call per element
(`D/XRL/XRLGame.cs:286-300`); called per row over 100 rows it is the only hot spot this design
has.

**What we do not do**: pin zones (cap 3, clears on overflow); veto suspension; keep a zone cache
warm; walk an unloaded zone's objects; run any per-turn clock over city state. All four are either
forbidden by STANDARDS or refuted in §0.1.

---

## 7. Migration path

### 7.1 Bootstrap — what becomes a model input unchanged

`Simulation/Kernel/` entire; the clock helpers (`ElapsedDays`, `AdvanceCheckpoint`, `ActivityDays`,
`LabouredTicks`, `RestampDeadline`, `PassagesThrough`); the catalogue (`Carries`, `Shades`,
`Refines`, `Equilibrium`, `LiftCapPercent`, `FloorLevel`); `KingdomSubsidenceRules` in full — the
model feeds it better numbers, it does not change; the brink shape; `KingdomWord`; the chronicle and
its budgets; `KingdomReports`; the option gates; `KingdomLiquids`; the crew machinery; the wear
machinery; the reach machinery.

**This is most of the mod, and it is the point.** The living city is a substrate change under
systems that already work, not a rewrite of them.

### 7.2 Refactor

| Today | Becomes |
|---|---|
| `KingdomSurvey.Take` | unchanged as *read the ground*; gains a sibling *reconcile against the model* |
| `KingdomSubsidence.RecordZone` / `OtherZones` / game-state keys | zone rows on `KingdomCityState`; `ZoneSighting` survives as the projection handed to `CityTally` |
| `r_KingdomPlot`'s stage + `NextStageTick` on the object | work-row run-state; the part keeps only appearance and the cheap due-tick nudge |
| `KingdomBrink`'s `KingdomBrinkRoofTick` &co. as object properties | resident-row fields |
| `KingdomGrowth.AssignWork` reading `Survey.Settlers` | reading the resident roster |
| `RosterNames` / `RosterOrigins` / `RosterArrived` parallel lists | resident rows |
| `LastKnownStorageSpace` | a zone row's capacity + `LastReadTick` |
| `NextArrivalTick` / `NextGuestTick` / `NextNotableGuestTick` / `RaidDueTick` | named clocks with ordinals |

### 7.3 Retire

The `r_TAF_Supports_*` game-state key family; the parallel roster lists; per-object brink
properties; any remaining reading of city storage from one zone only.

### 7.4 The wave plan

Disjoint ownership. Each wave ships. Each makes the city more alive. No wave edits an existing
TESTING.md pass except to *add* assertions; new behaviour arrives as new passes.

**W0 — Foundations and the seed.** Mint and persist the realm `KernelSeed128` at founding. Create
`Simulation/City/` with `KingdomCityRules` (pure) and `KingdomCityState` (carrier), fully
unit-tested, **wired to nothing**. Write the check-in/check-out contract and the model schema into
`docs/API.md`. Serialization version bump, clean and deliberate (Addendum 9 waives migration
pre-release). *Playtest baseline: byte-identical. Nothing visible ships.*

**W1 — The city book: stocks, zones, works.** `KingdomCityState` on `KingdomSettlement`; zone rows
replace the game-state keys; check-in reconcile at the survey step; `SuspendingEvent`
check-out (with the lazy reconcile as the correctness guarantee); closed-form breakpoint integration for stocks, reusing `Slide`'s shape. Subsidence,
city storage, and the stage ladder read the model. *Visible: a two-zone city's numbers stop being
five stale ints; a granary in the next zone actually fills and empties. Pass 26 gains "the other
zone's stores moved while you were in this one."*

**W2 — Residents become rows.** Roster → typed resident records; brink windows move off
`GameObject` properties; `AssignWork` reads the roster; bodies bound by `ResidentId`; check-in
rebinds, reads back deaths and departures. *Visible: a settler in the zone next door can lose their
roof, be warned, and leave. Highest-risk wave — see §8, hard problem 2. Pass 24 (the brink) gains
a cross-zone case.*

**W3 — Materialisation by the hour.** Anchors set by `DayShape` against `Calendar`'s bands;
`r_KingdomStation` riding `IdleQueryEvent` so settlers *walk* to their post the way vanilla sends
them to bed; items minted into real containers under the `_stock`/`norestock` protocol, including
the cross-zone harvest delivery Addendum 11(b-ii) already ruled; told-once dating. *Visible: walk in at Jeweled Dusk and the market is shut and the hearths
are full. **New Pass 29 — "A day in the city."***

**W4 — Happenings.** The generator, the shared `KingdomTellingBudget`, the told-log ring. Weddings,
funerals, feasts, festivals (creed dish), quarrels, breakdowns, deliveries, raisings. Surfaced
through ledger / `KingdomWord` / chronicle / guestbook. *Visible: come home to a city that has a
history. **New Pass 30 — "What happened while you were gone."***

**W5 — Engagement.** Works board, city-wide crew dial, day-shape policy, petitions from model
state, city-posted bounties, ceremonies from happenings, guests drawn by events — all inside
existing Charter entries. *Visible: the city asks you for things. **New Pass 31 — "The city asks."***

**W6 — Production depth and the optimisation receipt.** Extend the real machines in Addendum 11(c)
order; the food chain end to end (seeds → crops → stores → meals/industry); inter-zone haulage
crews; and the measured worst-case reckoning — a season away, 4 zones, 60 residents, timed and
written down, not asserted. *Visible: the economy is physical end to end.*

**Ordering rationale.** W1 before W2 because stocks reconcile against objects that already exist,
while residents reconcile against objects whose identity we are changing — do the easy handoff
first and learn the protocol on it. W3 after W2 because you cannot place people you do not have.
W4 after W3 because a happening the founder cannot see happen is only a log line. W5 after W4
because engagement needs something to engage with. W6 last because it is the only wave that is
pure depth rather than substrate, and it is the one that can be cut if the playtest says stop.

---

## 8. Risks, and the two hard problems

### 8.1 Risks, ranked

1. **The playtest gate has never been run** (COORDINATION.md standing agreement: *"Nothing is
   'done' until it has been."*). This design assumes a baseline that is asserted, not observed. W0
   should not start until at least Passes 1–3 and 23–26 have been run once against `main`.
2. **The model and the ground disagree in a way that reads as a bug.** Mitigated by attribution and
   telling (§3.1), but the *prose* has to carry it. Any reconciliation the founder can see must
   name a cause.
3. **Telling-budget starvation** — a season away in which the one line that mattered is the one
   that got summarised. Mitigate by ranking: brinks and irreversibles are never summarised, ever.
4. **A lane that draws per day.** The only thing here that can scale with absence. Enforce it as a
   test, not a convention: assert draw counts are identical for a 1-day and a 90-day reckoning of
   the same model.
5. **Collision with the water lane in flight.** `KingdomSurvey` is the shared seam. W0 touches
   nothing; W1 must rebase on the water lane's landed state, not race it.
6. **Serialization churn across six waves.** Waived pre-release, but each bump stays clean,
   deliberate, and named (`FirstNamedSerializationVersion` moves with it), per Addendum 9.

### 8.2 Hard problem 1 — dual books: who owns a dram

**The problem.** The model must own stocks while a zone is suspended, or nothing happens while the
founder is away. The player must be able to walk up to a cask and pour water out of it by hand, or
Addendum 11's physicality is a lie. Both naive answers fail: model-only makes the containers
decorative; ground-only makes the simulation impossible.

**Recommendation: two-phase ownership, lazy check-out, attributed reconciliation.**

- While attended, **the ground is authoritative** and the model mirrors it at the end of the pass.
- While suspended, **the model is authoritative**, carrying the zone's last-read numbers plus
  everything credited or drawn since.
- At check-in, the ground's actual number **wins** for anything physical, and the difference is
  attributed to a cause and told as news — never silently repaired, never treated as a fault.
- The eager `ZoneDeactivatedEvent` check-out is an accuracy optimisation only. **Missing a
  check-out must cost freshness, never correctness**, because save-and-quit and crash paths will
  miss it.

Why this is the right answer here specifically: it is not a new rule. It is `RecordZone`'s shipped
discipline — *rewritten from the ground every time, including down to zero* — widened, so the
codebase already contains the precedent, the reviewers already accept the shape, and the failure
mode is one the mod already has a voice for.

**The invariants to test** (both are cheap and both are mutation-resistant):
1. After any attended pass of a fully-visited city, model total == ground total, per stock kind.
2. Over any sequence of hand moves and passes, total drams in the world is conserved: a
   reconciliation may reclassify a dram between civic and personal, but may never mint or destroy
   one.

### 8.3 Hard problem 2 — where a person lives: object or row

**The problem.** Today a settler *is* a `GameObject`, with their brink windows, origin, name, and
creed stored as properties on it (`KingdomBrink.RoofTickProperty` and siblings). Sixty people must
exist and change while their zone is on disk — impossible for object properties. But minting and
striking bodies risks the protection law, destroys conversations and relationships the player
built, and can duplicate a settler the player charmed, recruited, carried off, or killed.

**Recommendation: model-row primary, body as a durable view bound by a stable id.**

- Every resident has a `ResidentId`. The body carries **only** that id as a property
  (`KingdomResidentId`) and is otherwise a view.
- Bodies persist in their saved zone with everything the player did to them. Check-in **rebinds by
  id**, it does not re-create.
- Materialisation may **mint** a body for a resident with no living bound body in this zone, and
  may **move** an existing one. It may **never remove one.** The protection law, extended to our
  own people.
- A body the player killed reads back at check-in as `Dead`, with a cause, and gets a funeral.
- A body the player took away — charmed, recruited, followed them out — reads as `Abroad`: still on
  the roll, contributing no labour, and honestly reported as such. This is the same staleness voice
  the mod already uses about a sighting.
- Sequencing: **W2 ships the rows and the binding but not the placement.** Placement is W3. Doing
  identity and movement in one wave is how a settler ends up in two places.

**The invariants to test:**
1. No `ResidentId` ever has two living bound bodies.
2. Materialisation obliterates nothing, ever — asserted directly, in the live selftest.
3. `Population` == count of `Resident` rows == live bindings + `Abroad`; `Dead` rows reconcile with
   the `DeadNames` roll.

**The honest residual risk.** A player who charms half the settlement and walks them across Qud
leaves the model describing a city whose people are elsewhere. The design's answer is to *say so*
rather than to prevent it — `Abroad` is a real state with real consequences (no labour, the works
go idle, the level subsides toward what stands). That is the mod's doctrine working, not a hole in
it. But it wants a Pass-31 step of its own, because it is the shape a tester will find first.

---

## Appendix — the evidence this design rests on

| Claim | Where |
|---|---|
| Suspendability grace = 40 turns (5 with `CacheEarly`) | `D/XRL/World/Zone.cs:7413-7420` |
| A cached, non-suspended zone **is** ticked and gets `EndTurnEvent` — the 40-turn shadow window | `D/XRL/Core/ActionManager.cs:439-467` |
| Suspend keeps the object graph in RAM; only `FreezeZone` → `Zone.Release()` frees it | `D/XRL/World/ZoneManager.cs:1682-1712, 883-966`; `D/XRL/World/Zone.cs:2282` |
| Every **live** zone is written inline into the save; frozen zones are ids only | `D/XRL/World/ZoneManager.cs:468-520` |
| `SuspendingEvent` fires before `Suspended = true`, for any zone, and reaches `The.Game` | `D/XRL/World/SuspendingEvent.cs`; `D/XRL/World/ZoneManager.cs:1690` |
| `ZoneThawedEvent` fires only on thaw from disk, never on wake-from-suspend | `D/XRL/World/ZoneManager.cs:863-878` |
| World-map travel adds 300–900 `TimeTicks` per parasang step and fires **no** `EndTurnEvent` | `D/XRL/World/Parts/TerrainTravel.cs:161, 221-264` |
| `IGameSystem.ZoneActivated` / `EndTurn` / `NewZoneGenerated` / `GetPriority` are obsolete **and dead** | `D/XRL/IGameSystem.cs:2954-3070` |
| `The.Game.GetSystem<T>()` is a linear scan with `GetType()` per element | `D/XRL/XRLGame.cs:286-300` |
| `IdleQueryEvent` is vanilla's entire daily-life surface; returning `false` claims the actor's turn | `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330` |
| `Bed` is the **only** time-of-day NPC behaviour vanilla ships | `D/XRL/World/Parts/Bed.cs:187-224` |
| An NPC with `Wanders=false` self-anchors to its first cell and `Bored` returns it | `D/XRL/World/Parts/Brain.cs:2056, 2507` |
| `ZoneRepair` is the engine's own accumulate-and-apply-N-units catch-up | `D/XRL/World/ZoneParts/ZoneRepair.cs` |
| `_stock` / `norestock` / `IsImportant()` is vanilla's item-protection protocol | `D/XRL/World/Parts/GenericInventoryRestocker.cs:148-200` |
| One parasang = 3×3 zones, 50 Z-layers, surface Z=10, each zone 80×25 cells | `D/Definitions.cs`; `D/XRL/World/ZoneID.cs:12-27`; `D/XRL/World/ZoneManager.cs:3268` |
| Freezability turns = 0; freeze threshold = 1 | `D/XRL/World/Zone.cs:7467-7470`; `D/XRL/World/ZoneManager.cs:1036-1041` |
| `CheckCached` suspends then freezes, driven from `ZoneManager.Tick` | `D/XRL/World/ZoneManager.cs:976-1060` |
| `PinnedZones` cap 3, **clears the set** on overflow | `D/XRL/World/Zone.cs:7440-7449` |
| Suspended zones are not ticked; live objects in them are dropped | `D/XRL/Core/ActionManager.cs:430-447` |
| `ZoneDeactivatedEvent` fires on the outgoing zone, to `The.Game` | `D/XRL/World/ZoneDeactivatedEvent.cs`; `D/XRL/World/ZoneManager.cs:1904-1905` |
| `ZoneThawedEvent` carries `TicksFrozen` | `D/XRL/World/ZoneManager.cs:863-865` |
| `Zone.Activated()` wraps the event in try/catch; `ePrimePowerSystems` fires right after | `D/XRL/World/Zone.cs:7775-7791` |
| Game systems receive game-level events via `RegisteredEvents.Dispatch` | `D/XRL/XRLGame.cs:357-384` |
| `TurnsPerDay = 1200`; `IsDay()` = segment 2500–9123; eight named bands | `D/XRL/World/Calendar.cs:13, 296-352` |
| `Temporary` is the engine's own tick-stamp catch-up | `D/XRL/World/Parts/Temporary.cs:137-157` |
| No rain, no spoilage, no plantable seeds, no multi-stage growth, no liquid network | `VANILLA-PRODUCTION-TRUTH.md` §0, §8 |
| Faction-level favourite dish via `<waterritual Recipe=…/>`, eight shipped | `B/Factions.xml`; `VANILLA-PRODUCTION-TRUTH.md` §2.3 |
| Charter is at 32/32 hotkeys | `Core/KingdomCharterPart.cs:85` and its comment |
| Zones per city: Camp/Steading 1 … City 4 | `KingdomZoningRules.ZonesForStage` |
| Caps: 2 cities, 60 population, 40 buildings, 200 chronicle entries | `KingdomSettlement.MaxSettlements`, `KingdomRules.MaxPopulation`, `MaxBuildings`, `KingdomChronicle.MaxEntries` |
