# Clock rework — change map

Date: 2026-08-21. Read-only audit; nothing in this pass was edited, compiled, or run.

**Spec:** `BUILDING-CATALOGUE-BRIEF.md` Addendum 8 (the time doctrine) plus the BACKLOG absence
split it absorbs. The doctrine in five clauses, as this map applies them:

1. **Processes happen as time passes.** Crops, refining, construction, wear-from-running, osmosis,
   dissent, subsidence. The settlement lives whether the founder is there or not.
2. **Rates are time × labour × infrastructure — never time alone.** Idleness wears nothing; an
   unstaffed yard shapes nothing.
3. **Consequences crystallise at awareness.** Irreversible ones — a settler leaving, a city
   seceding — wait at the brink for awareness rather than firing silently in the past, and the
   moment of realisation carries the last arrestable window the design promised.
4. **Time never mints unchosen debts. The floor is Camp's own equilibrium.** 7b speaks at
   awareness moments.
5. **Crops ripening on elapsed ticks are SANCTIONED.** Nature is a worker; the earlier open
   decision is closed.

Change classes used throughout:

| Class | Meaning |
|---|---|
| **A** | Already doctrine-compliant. No code change; some rows need only a prose correction. |
| **B** | Denominator swap. Mechanical: uncap a capped day count, or swap a per-pass counter to a per-activity-day one. Behaviour changes; shape does not. |
| **C** | Semantic redesign. New machinery or an inverted rule. |
| **D** | Retire. The construct itself goes, and every consumer needs a replacement. |

---

## 1. The three reconciliation findings (read these before the table)

### 1.1 `0a0c9bd` and `fc8c0cb` are prose-only. There is no hubris code to orphan.

`git show --stat` on both: one file each, `VISION.md`, 11 and 7 insertions. No `.cs`, no `.xml`,
no test. They are author rulings enshrined in the vision document. Both edits survive verbatim on
`main` at `VISION.md:92-100` and `:139-140`.

The premise that "the economy repivot orphaned it" is **inverted**. `git merge-base
--is-ancestor` puts `21776c6`, `4d0d9ca`, `fc7a710`, `fee94ae` **before** `fc8c0cb`; only
`da8bb92` and `6b53984` came after. The ruling was written in response to a world the repivot had
already made. Nothing bypassed it — it was never wired in.

### 1.2 The equilibrium arithmetic exists, is tested, and has zero production callers.

`6b53984` (the catalogue wave, *after* `fc8c0cb`) added the arithmetic the ruling implies, in
`Growth/KingdomCatalogueRules.cs`:

- `SupportWater`/`SupportFood`/`SupportRoof` (`:214`, `:216`, `:218`), `BindingSupports` (`:226`),
  `LiftingSupports` (`:234`), `LiftCapPercent = 50` (`:240`), `FloorLevel = 4` (`:247`)
- `Equilibrium(int Water, int Food, int Roof, int Lift)` (`:273`) — least of the three binding
  supports, lifted by comfort to a 50% cap, floored at 4
- `BindingSupport(int,int,int)` (`:301`) — *which* good is holding the level, for 7b
- `LimitLine(string,int)` (`:312`) — the sentence that says so

`FloorLevel`'s own doc comment is `fc8c0cb`'s commit message paraphrased into a constant: *"A camp
carries itself — four people, a fire, and whatever they walked in with — so the floor is the
smallest stage's own equilibrium rather than a special case bolted under the arithmetic."*

**Callers of `Equilibrium`, `BindingSupport`, `LimitLine`, `FloorLevel` outside
`DevTests/KingdomCatalogueRulesTests.cs`: none.** `IsBindingSupport` is the only member of the
family with production callers, and it validates catalogue attributes rather than computing a
level. The catalogue's `Carries` attribute is parsed in exactly three places
(`KingdomLodging.cs:616` for roofs, `KingdomReach.cs:777` for reach, and the validator) and is
**never summed across the settlement**.

So the equilibrium system is a three-layer partial: **the ruling exists**, **the arithmetic exists
and is pinned**, **the consumer does not**. The gap is between layers two and three, and the
missing piece is smaller than expected: a summation over `Survey.Works` producing
`(water, food, roof, lift)`.

### 1.3 The stage ratchet is the only writer of `System.Stage`, and it only climbs.

`Growth/KingdomGrowth*.cs`:

```csharp
GrowthStage stage = KingdomRules.StageFor(System.Population, (Survey != null) ? Survey.StorageCapacity : CountStorageCapacity(Z));
if (stage > System.Stage)
{
    System.Stage = stage;
```

`KingdomRules.StageFor` (`Core/KingdomRules*.cs`) is a pure population + liquid-storage-capacity
threshold table with no reference to supports. `KingdomGrowth*.cs` is the **only** assignment to
`System.Stage` in the mod outside the two `= GrowthStage.Camp` field initialisers. `StageFor`
returning `Camp` for a collapsed settlement is computed and then discarded by the `>` guard.

Population *can* fall (`Emigrate`, `KingdomGrowth*.cs`), so today a City can hold four people.

Already known and recorded in three places — `_notes/STALE-COMMENT-INVENTORY.md:242-244`,
`docs/API.md:36` and `:109`, `_notes/COORDINATION.md:180`. This map does not rediscover it; it
scopes it.

**The reconciliation verdict: the doctrine composes cleanly with the repivot.** `Carries` is
denominated in *sustainable level in settlers*, which is exactly 022a's fixed point E expressed in
population rather than in stage. Nothing needs undoing. What is missing is one summation, one
hysteresis, and the convergence between them.

---

## 2. The change map

Denominator legend: **RAW** = raw elapsed world ticks, uncapped · **HB** = capped heartbeat days
(`KingdomRules.HeartbeatDays`, 0–3) · **PASS** = attended zone-activation passes · **EVENT** =
keyed to an event ordinal · **NONE** = reads no clock.

### 2a. The clock substrate

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| `MaxUpkeepDaysCharged = 3` + `HeartbeatDays` + `HeartbeatCheckpoint` — `Core/KingdomRules.cs:260,295,313`. Caps elapsed at 3 days and *forgives* the rest by re-anchoring the checkpoint to `now`. | HB (the primitive) | **Retired.** Elapsed days run in full; the bound on loss is subsidence-to-equilibrium, not forgiveness. Replace with an uncapped `ElapsedDays`/`AdvanceCheckpoint` pair over `Simulation/Kernel/TickMath`. | **D** | **Highest.** 9 production consumers, ~40 doc comments, `balance-sim.py` import. First load of an existing save resolves the *real* elapsed since a stale stamp — see §4.1. |
| `Simulation/Kernel/TickMath.TryCountFixedPeriodDue` + `FixedPeriodToy` — checked, fail-closed, overflow-safe due-tick and banked-cycle math. Pinned by a BigInteger oracle over 100k triples (`DevTests/TickMathTests.cs:191`) and 1293 lines of `FixedPeriodToyTests.cs`. | n/a | The substrate the new helpers should be built on. Entirely `internal`, **zero production references outside `Simulation/Kernel/`**. Four subsystems hand-roll what it already does correctly. | **A** (opportunity) | Low. Adopting it is the cheapest correctness win in the wave. |

### 2b. Water, growth, and the settlement level

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| **Heartbeat / upkeep** — `ResolveHeartbeat` `Growth/KingdomGrowth*.cs`; bill at `:170` via `PolicyUpkeepForElapsed` (`Core/KingdomRules*.cs`). `System.LastHeartbeatTick`. | HB | Full elapsed days drawn against stores. No debt is minted: stores floor at zero and the *level* subsides instead. | **C** | High — this is where "time never mints unchosen debts" is either honoured or broken. |
| **Fetch clock** — `Growth/KingdomGrowth*.cs`; `FetchableDrams(hands, open, space, days)` = `Hands × FetchDramsPerSettler × Days` (`Core/KingdomRules*.cs`). `System.LastFetchTick`, gated on `System.WaterCrew`. | HB | **Already time × labour.** Uncap `Days`. This is the doctrine's own formula, shipped. | **B** | Medium — becomes the main absence income; must net against uncapped upkeep. Balance re-run. |
| **Thirst ladder** — `System.DryStreak++` at `Growth/KingdomGrowth*.cs`, fired **once per failed heartbeat resolve** regardless of whether `days` was 1, 2 or 3. `ResolveThirst` (`Core/KingdomRules*.cs`), `DryIntervalsToEmigrate=2`, `DryIntervalsToWither=3`. | **PASS** (the bill above it is HB — the sharpest denominator mismatch in the codebase: three 1-day visits cost the same drams and three times the ladder of one 3-day absence) | Dry **time**, not dry passes. And `Emigration` is irreversible, so it goes through the brink (§3). | **B** + **C** | High. No test pins the once-per-pass increment. |
| **Arrivals** — `System.NextArrivalTick`, `ArrivalIntervalTicks = 3600 + 600×Pop`, `MaxArrivalsPerVisit = 3`, catch-up clamp `Growth/KingdomGrowth*.cs` burns every overshoot. | RAW, per-pass capped | Population converging toward E from below (022a). The clamp becomes convergence, not a burn. | **C** | Medium — folds into subsidence; do not build twice. |
| **Stage ratchet** — `StageFor` (`Core/KingdomRules*.cs`) + `UpdateStage` (`Growth/KingdomGrowth*.cs`), `if (stage > System.Stage)`. | population + storage snapshot, monotone up | Hysteresis both ways against E: promotion on demonstrated feed, demotion on sustained failure, M comfortably larger than N so the reckoning is never a first-visit ambush. | **C** | **Top-three.** Only writer of `System.Stage`; zero tests pin subsidence, so a wrong answer fails silently. |
| **Subsidence** — **does not exist.** | — | New: sum `Carries` over standing works → `Equilibrium(w,f,r,lift)`; converge population/stage toward E in closed form over elapsed absence; ruin the overreach via the existing `StandingPercent` machinery; sample the computed curve's breakpoints into dated chronicle entries. Full spec at `_notes/IDEA-INBOX.md:378-400` (022a). | **C** (new build) | **Top-three.** Must ruin overbuilt ground under a protection law that forbids touching anything player-placed. |
| **Equilibrium arithmetic** — `Growth/KingdomCatalogueRules.cs:247,273,301,312`. Dead code, 6 tests. | n/a (pure) | Keep verbatim. Needs a **consumer**: a supports tally on `KingdomSurvey`. | **A** + consumer | Low. |
| **`Withered`** — `System.Withered`, set at `Growth/KingdomGrowth*.cs`, halves sealed vigour (`Core/KingdomRules*.cs`). | derived from `DryStreak` | Reconcile with subsidence: withering is *level loss*, so it should read the same fixed point rather than a parallel flag. | **B** | Medium — duplicate concept. |

### 2c. Labour and production (the `*WorkedProperty` family)

All five stamp a "last worked" tick, compute `HeartbeatDays` since, and re-anchor with
`HeartbeatCheckpoint`. All five already multiply days by hands. **The uncapping is the whole
change** — this family is the mechanical bulk of the wave.

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| **Refining** — `KingdomRefineWorked` (`Growth/KingdomMaterials*.cs`); `RefinedThisPass(Crew, Days, Capability, Refinable)` = `Crew × Days × EffortPerHandPerDay × capability / 100`, capped `MaxRefinedPerPass = 8` (`Growth/KingdomMaterialRules.cs:993,1117`). Staffing-gated at `:1702`. | HB × crew | Uncap `Days`. `MaxRefinedPerPass` becomes a yard's throughput ceiling per *day*, not per pass. | **B** | Medium. **Checkpoint is written at `:1700` before the staffing gate returns at `:1702`** — an unstaffed yard silently burns its day budget. Decide explicitly. |
| **Striking** — `KingdomStrikeWorked` (`Growth/KingdomMaterials*.cs`); `EffortWorked(Hands, Days)` = `min(hands,6) × Days × 10`. `FreeHands`-gated. | HB × hands | Uncap `Days`. | **B** | Low. |
| **Clearance** — `r_KingdomClearance.LastWorkedTick` (`Growth/KingdomMaterials*.cs`). Same shape. | HB × hands | Uncap `Days`. | **B** | Low. |
| **Mending / repair** — `KingdomRepairWorked` (`Growth/KingdomWear.03.Activation.cs:37`; advance `Growth/KingdomWear.13.RepairCompletion.cs:16-80`). `FreeHands` + materials gated; one mending settlement-wide at a time. | HB × hands | Uncap `Days`. | **B** | Low. The no-hands checkpoint precedes its return at `Growth/KingdomWear.13.RepairCompletion.cs:29-41`. |
| **"One gang, one job"** — `Growth/KingdomMaterials*.cs`: one strike *or* one clearance per attended pass, however many days elapsed. | PASS | Under the doctrine the gang works through the absence. Becomes a hands-availability constraint, not a per-pass one. | **B** | Medium — untested (engine-side). |
| **Road traffic** — `r_TAF_RoadsWalked` zone property (`Growth/KingdomRoads.cs:49,285-303`); `TrafficFor(Walkers, Days, Kind)` (`Growth/KingdomRoadRules.cs:255`). Population- and layout-gated. | HB × walkers | Uncap `Days`. Traffic is already walkers × days — the doctrine's formula. "Wear only ever climbs" stays right. | **B** | Low. `KingdomRoadRules.cs:21-25` asserts *"nothing here subsides"* — correct for ground, needs rewording once subsidence exists. |
| **Road per-pass bounds** — `MaxRoutesPerPass = 8`, `MaxFloorChangesPerPass = 8`, `MaxTrackedCells = 240`. | PASS | Loop guards, not forgiveness. Keep. | **A** | Low. |
| **Road rotation** — `RotationStart(TimeTicks, Count)` = `(ticks / TicksPerDay) % Count` (`Growth/KingdomRoadRules*.cs`). | RAW, index only | Already correct — a scheduling index, stable across reloads. | **A** | Low. |
| **Power works** — `r_KingdomPowerWork.LastResolvedTick`, one-day gate `Growth/KingdomPower.cs:59`, `CreditDays` `:309-317`, `MaxDaysCredited = MaxUpkeepDaysCharged` (`Growth/KingdomPowerRules.cs:98`). Staff- and effectiveness-gated. | HB × staffing | Uncap. Already fully labour-gated. | **B** | Low. |
| **Wind availability** — `WindAvailabilityPercent(SampledKph, Days)` = `(sampled + typical×(days−1)) / days` (`Growth/KingdomPowerRules.cs:208`). | HB | **The doctrine's exemplar already shipped**: the witnessed day counts at the read gust, unwitnessed days at the typical value. Uncap `Days` and it is finished. | **B** | Low. |
| **Molten-salt store** — `ThroughputForDays`, `Absorbable`, `Releasable`; `days` is the max across all works and stores. | HB | Uncap. Storage capacity is the natural anti-away-farming cap (022a says so explicitly). | **B** | Low. |
| **Crew assignment** — `AssignWork` (`Growth/KingdomGrowth*.cs`), recomputed from scratch each pass, carries no clock. | PASS | Correct as-is: it is the labour *input* every rate reads. | **A** | Low. |

### 2d. Construction and crops

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| **Plot stage ratchet** — `StageAt(Elapsed, Total)` (`Growth/KingdomPlotRules*.cs`), driven from `r_KingdomPlotWorks.StartTick`/`TotalTicks` (`Growth/KingdomPlot2.26.Labour.cs:25`). Uncapped, applies every crossed stage in order. | RAW | Already the doctrine's shape — a hundred days finds it finished, one finds it framed. | **A** | Low. |
| **Scaffold completion** — `r_KingdomScaffold.CompleteTick` (`Growth/KingdomScaffold.cs:22,33-38`), stamped at commission from `CraftBuildTicks` (`Growth/KingdomCommission.cs:133`). `TurnTick` only fires in an active zone, so completion lands on the first attended turn past the deadline. | RAW, **no labour term at all** | **FLAG FOR THE AUTHOR.** Addendum 2's corollary sanctions flat-per-design duration ("currently flat per design plus the craft-district discount"); Addendum 8 clause 2 says rates are never time alone. Today a scaffold in a settlement with zero population still raises itself, while a *mending* in the same settlement does not. That asymmetry is the one place the doctrine and the shipped code disagree in principle rather than in calibration. | **A**-with-flag, or **B** if crew must stand | Medium — a ruling, not a bug. |
| **`KingdomBuilt` gate** — `Growth/KingdomSurvey.cs:93`, `Growth/KingdomGrowth*.cs`, set once at `Growth/KingdomScaffold.cs:91`. A scaffold contributes no beds, no crew demand, no defence, no power work, no yard. | boundary, not a clock | Correct and load-bearing. It is what makes "a scaffold carries nothing until built" true, and it is what subsidence must read when summing `Carries`. | **A** | Low — but §2b's summation must respect it. |
| **Crop ripening** — `GrowTicks = 3 days`, `RipenTick`/`HasRipened` (`Growth/KingdomCropRules.cs:33,69,75`); ripen tick anchored at the pass that resolves the planting, never backdated. | RAW, re-anchored per plant | **Sanctioned by Addendum 8 clause 4.** No change. | **A** | Low. |
| **`MaxCyclesPerVisit = 3`** — `Growth/KingdomPlot.cs:172`. | PASS | A loop guard whose own rationale (a planting cannot be backdated, so extra cycles are unearnable anyway) survives the doctrine intact. Keep. | **A** | Low. |
| **`MaxPlansPerVisit = 3`** — `Growth/KingdomPlanRules.cs:59`. | PASS | Same shape. Keep, or lift to a materials/hands constraint. | **A**/**B** | Low. |
| **Crop planting reserve** — `UpkeepDrams(Population) × MaxUpkeepDaysCharged` (`Growth/KingdomCropRules.cs:64`). | cap-as-quantity | Needs a replacement basis when the constant retires. **Also: camp-rate**, while `KingdomUpgradeRules.ReserveDrams` (`:254`) is stage-scaled — at City the crop reserve is 2.2× too small relative to the upgrade one. | **D**-dependency | Medium — a live inconsistency the rework should settle. |
| **Upgrade reserve** — `ReserveDrams` = `UpkeepDrams(Pop, Stage) × MaxUpkeepDaysCharged` (`Growth/KingdomUpgradeRules*.cs`). | cap-as-quantity | Same. | **D**-dependency | Medium. |
| **`BuildDays` / `OutputLost` / `AbsorptionMargin`** — `Growth/KingdomUpgradeRules.cs:462,482,497`. Authored design duration, explicitly not the clock; guarded by the reflective test `NoTriggerPathReadsElapsedTimeAsACause` (`DevTests/KingdomUpgradeRulesTests.cs:1118`). | authored duration | Correct. Leave alone. | **A** | Low — but the reflective guard allowlists by exact parameter name; a new clock helper must not leak in. |
| **Improvement abandon grace** — `AbandonGraceTicks = 2400` (`Growth/KingdomUpgrade*.cs`). | RAW | Give-up timer for a successor that never appeared. Fine. | **A** | Low. |

### 2e. Social and political

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| **Osmosis / shared living** — `SharedLivingForConversion = 72`, `SharedLivingPerPass` (Close 3 / Roomed 2 / Private 1 / Packed 0) (`Core/KingdomConversionRules.cs:128,212`); `ConversionShared` + `ConversionToward` dicts. | **PASS**, closeness-scaled | **Cohabitation-time** × closeness. Addendum 8 clause 1 names osmosis explicitly. | **B** | **Top-three.** `KingdomConversionRules.cs:112-120` asserts the opposite verbatim: *"there is no code path anywhere in this file or its caller that turns elapsed time into progress, so a season away and three days away buy identically nothing."* The 72 is calibrated in visits ("a season of coming home"). Recalibrating passes→days without re-deriving 72 mass-converts a city on first load. |
| **Meal / culture channel** — `MealShared = 4`, `MealCeilingPercent = 50`. | EVENT (meals held) | Event-counted, ceilinged below the road. Correct. | **A** | Low. |
| **Shrine pull** — `ConversionPullThreshold = 30` (`Experience/KingdomFaithRules.cs:134`), GameObject int `KingdomShrinePull`. | PASS | Attended-days → then time. **And it fires a permanent creed change with no announce at all** — the only irreversible social consequence in the mod with no warning of any kind. | **B** + **C** | High. |
| **Water-rite shared living** — `ShouldCountPass` (`Experience/KingdomWaterRiteRules*.cs`) = one count per settler per *day*; `KingdomSharedPasses`/`KingdomSharedPassTick` GameObject properties; `MaxCountedPasses = 35`. | PASS with a raw one-day *forbidding* gate | Already half-migrated — the day gate means it is effectively attended-days. Swap to cohabitation-days; the constant barely moves. Doc at `:452-458` is the clearest statement in the repo of "a clock that only ever says 'not yet' is not a maturation timer" and stays true. | **B** | Low — lowest-risk of the four counter migrations. |
| **Water-rite refusal counter** — `RefusalsBeforeAskingCloses = 3` (`:505`), founder actions only. Delegates its exit to conversion pressure rather than building a second one (`:494-497`). | NONE | Correct, and the precedent the brink should generalise: **one exit, many feeders.** | **A** | Low. |
| **Lodging grace** — `GracePasses = 2`, `NoGrace = -1`, `GraceAfterPass`/`GraceRunOut` (`Growth/KingdomLodgingRules*.cs`); `LodgingGrace` dict, per-city, seat-swapped; announce flag on a GameObject property `KingdomLodgingUnhousedAnnounced`. Consequence: `Emigrate` — irreversible. | PASS | **Survives as-is in substance** (Addendum 4b's ruling that absence never spends the grace is exactly clause 3), but becomes one instance of the shared brink rather than its own implementation. | **C** (unify) | Medium — behaviour preserved, machinery moved. |
| **Resented-creed grace** — `ResentedPasses = 6` (= 3 × `GracePasses`, by explicit reference), `ResentmentAfterPass`/`ResentmentRunOut` (`Core/KingdomConversionRules*.cs`); `ConversionResented` dict, per-city, seat-swapped; **the map entry itself is the announce flag**. Pressure is re-derived every pass, never remembered (`Core/KingdomConversion.cs:9-15`). Consequence: `Emigrate`. | PASS | Same. Its **re-derive-every-pass contract is the one the shared brink should adopt** — it is what makes "take the pressure off and it goes away" a guarantee rather than a coincidence. | **C** (unify) | Medium. |
| **Dissent accrual** — `AccrueDissent(Dissent, hostility, days)`, `DissentPerDay`, `HostilityPerDissentPoint = 25`, `DissentBreaking = 100` (`Core/KingdomCreedRules.cs:120-124,352,369`); `System.LastDissentTick` via `HeartbeatDays`. Two-city realms only. | **HB** — the only social consumer of the heartbeat cap in the repo | Uncapped time × hostility. | **B** | Medium. |
| **Secession** — `Dissent >= 100` → `Secede` **on the same pass, with no grace** (`Core/KingdomCreed.cs:300-303`). Four-tier warning ladder with hysteresis (`DissentSpoken`), but the "window" between Rupture (70) and Breaking (100) is emergent from the accrual rate, not a named constant. | HB, no window | **The biggest brink gap.** A warning ladder with no arrestable window that has a name. Needs a named window constant so `TheLoudestWarningStandsForManyAttendedDaysBeforeTheCityLeaves` keeps meaning something once accrual is uncapped. | **C** | **Top-three (joint).** Uncapping dissent without adding the window makes secession *faster* in absence, which is the exact inversion of clause 3. |
| **Rite cooldowns** — `RiteCooldownDays = 3`, `RiteReady` (`Core/KingdomCreedRules.cs:139,478`); reused verbatim for the soul rite (`Experience/KingdomWaterRite.cs:520`). `LastRiteTick`, `LastSoulRiteTick`. | RAW | Gates only — running out *enables* a lever and never costs anything. Correct. The number is calibrated "matching the absence cap"; retiring the cap orphans the **rationale**, not the value. | **A** (+ comment) | Low. |
| **Exile regard ladder** — `Core/KingdomExileRules.cs`, whole file. Deed-driven, reads no time whatsoever. | NONE | Correct and deliberately so. | **A** | Low — but see §4.3: a reflection test bans `long` parameters and the substrings `tick`/`elapsed`/`day` in parameter names across the whole public surface of this file. **The shared brink API must not be reachable from it.** |
| **Guest arrival / departure** — `GuestIntervalTicks = 3d` / `GuestPatienceTicks = 1/3 d` (`Experience/KingdomLocusRules.cs:187,195`); `NotableGuestIntervalTicks = 7d` / `NotableGuestPatienceTicks = 2d` (`Experience/KingdomGuestRules.cs:135,140`). Raw ticks *observed* at pass granularity; no banking (an existing guest blocks the next). | RAW, observed attended-only | A 200-day absence and a 3-day absence currently produce the same single guest. Under clause 1, guests arrive and leave through the absence and are told at awareness — which is exactly what the "guests at the gate" co-opt already promises ("notable wanderers logged between visits… ignored, they leave a letter"). Consequences are explicitly non-lossy, so no brink is needed. | **B**/**C** | Low-medium. |
| **Ceremony `IsAttended`** — `NowTicks - CompleteTick < TicksPerDay` (`Experience/KingdomCeremonyRules.Raising.cs:23`). | RAW classifier | **The exemplar of awareness-crystallisation done right**: it changes prose, never outcome. Model the brink's announcement on it. | **A** | Low. |
| **Homecoming digest** — `elapsed = TimeTicks - LastVisitTick`, `HomecomingDays = elapsed / TicksPerDay`, `[NonSerialized]` (`Core/KingdomSystem.cs:36,1014-1026`). Runs **last** in the activation dispatch. | RAW, **uncapped** | Already the awareness moment, and already the only uncapped day count the player sees. It is where the brink presents (§3). Today it says "90 days" beside a 3-day bill; after the rework the two agree. | **A** (host) | Low — and it removes an existing incoherence. |

### 2f. Events, trade, and deadlines

| System / counter | Today's denominator | Doctrine denominator | Class | Risk |
|---|---|---|---|---|
| **Manifest deadline** — `ManifestWindowDays = 10`, `DeadlineTick`, `ManifestExpired` strictly-past (`Trade/KingdomManifest.cs:88,118,127`). Two-strike `ExpireManifestIfStale` (`Trade/KingdomTrade*.cs`, `ExpireManifestIfStale`): first expiry turns the load back and **re-stamps a fresh deadline at the moment of witnessing**; second sets the water down as a real pool. Never a debt. | RAW, re-stamped on observation | **Already brink-shaped and already correct.** Fold its re-stamp into the shared helper. | **A** / **B** (consolidate) | Low. The turn-back *path* is untested. |
| **Manifest reserve** — `ReserveUpkeepDays = 3` (`Trade/KingdomManifest.cs:80`), coupled to `MaxUpkeepDaysCharged` by comment only. | cap-as-quantity | Replacement basis needed. | **D**-dependency | Low. |
| **Charter deal cycles** — `DealNextTicks`, `BankedCycles`, `MaxBankedCycles = 3` (`Core/KingdomRules*.cs`; `Trade/KingdomTrade.cs:81,99`). The one clock where absence *credits* the player. | RAW, capped at 3 **cycles** (so a different wall-time per charter) | "Absence earns gifts, capped" is a surviving rule. But 022b says the cap should be **storage capacity**, not an arbitrary 3 — "credit then clamp", not "reset the backlog". | **B** | Low-medium. |
| **Raid cooldown / warning lead** — `RaidCooldownTicks = 8400`, `RaidWarningLeadTicks = 1200` (`Core/KingdomRules.cs:2045,2047`). | RAW due-ticks | Correct. | **A** | Low — both untested. |
| **Raid re-warn** — `Raids/KingdomRaids*.cs`. Overshoot ≤ 1 day → the raid fires; > 1 day → re-stamp a fresh warning window, nothing taken. **The only raw-tick clip in the mod that does not go through `HeartbeatDays`** — a hand-rolled `> KingdomRules.TicksPerDay` compared inline. | RAW with an inline grace band | Doctrine-correct in spirit (a raid waits at the gate for a witness), hand-rolled in form. Fold into the shared helper. | **A** / **B** (consolidate) | Low — untested grace band. |
| **Recent-raid window** — `RecentRaidWindowTicks = 2d` (`Experience/KingdomLocusRules.cs:34`), feeds keeper mood. | RAW | Correct. | **A** | Low. |
| **Bounty due-ticks** — `TakenTick`/`DueTick`, `WorkDays` (`Quests/KingdomBounty.Take.cs:85`; `Quests/KingdomBountyRules.WorkAndBlocking.cs:60`). Haul 1–5 d, Manning 30 d, Scouting 4 d, **Clearance 0 (no clock — read off the world)**. No expiry, ever. The `Quests/KingdomBounty*.cs` class doctrine remains unchanged. | RAW due-ticks | Correct. | **A** | Low. |
| **Bounty manning** — `ManOneWork` runs **every attended pass** while `now < DueTick`, then the due-tick ends the season (`Quests/KingdomBounty.WorkAndCarry.cs:14`, `Quests/KingdomBounty.CompletionAndScouting.cs:79`). | **PASS for the labour, RAW for the finish** — mixed denominators inside one case | Labour becomes time × hands like every other work. | **B** | Low. |
| **Bounty notice `Passes`** — `r_KingdomNotice.Passes`, `MaxPasses = 10000000`. | PASS | The true denominator for who reads a notice. Fine. | **A** | Low. |
| **Ticks-as-kernel-ordinals** — `r_KingdomNotice.PostedTick` (`Quests/KingdomBounty.cs:42`), `KingdomCarryHaul.PlantedTick` (`Experience/KingdomGuestbook.cs:590`), `Data.TakenTick` bridged at `:683`. Each is fed to `SemanticEventKey.TryCreate` as a `ulong` draw ordinal. | EVENT ordinal | **FLAG:** these are not clocks, but a rework that re-bases or re-anchors any stored tick **silently re-rolls every determinism draw keyed off it.** Any migration in §4.1 must leave these three fields untouched. | **A** (hazard) | Medium — a silent correctness trap. |
| **Petition cooldown / lifetime** — `PetitionCooldownTicks = 3600`, `PetitionLifetimeTicks = 24000` (`Core/KingdomRules.cs:533,535`). `IsPetitionMet` is checked **before** the expiry branch, so a petition satisfied during an absence is fulfilled rather than timed out. Expiry is a grey ledger note, no penalty. | RAW | Correct, and the ordering is load-bearing. | **A** | Low — both branches untested. |
| **Carry-sign haul** — `HaulDueTick` (`Experience/KingdomGuestRules.CarrySign.cs:44`), no expiry whatsoever. | RAW | Correct. | **A** | Low. |
| **Deed memory** — `DeedMemoryTicks = 12000` (`Core/KingdomRules*.cs`), flavour text only. | RAW | Correct. | **A** | Low. |

### 2g. Counts by change class

| Class | Rows | Notes |
|---|---|---|
| **A** — compliant | **24** | Most of the deadline/due-tick layer is already doctrine-shaped. Six of these need only a prose correction; three are flagged hazards rather than work. |
| **B** — denominator swap | **17** | The `*WorkedProperty` family, power, roads, fetch, thirst, dissent, and the four social counters. Mechanical, but four of them reinterpret a stored counter (§4.2). |
| **C** — semantic redesign | **8** | Subsidence, the stage hysteresis, arrivals-as-convergence, upkeep-without-debt, the brink and its three instances, secession's window, shrine-pull's missing announce. |
| **D** — retire | **1** (+5 dependents) | `MaxUpkeepDaysCharged` and the five reserve/alias constants that read it. |

---

## 3. The arrestable window — one shape for all of them

### 3.1 What exists today

**Two true implementations** of "announce once → grace → irreversible thing", structurally
identical down to byte-identical step and fire functions:

| | Lodging | Resented creed |
|---|---|---|
| Length | `GracePasses = 2` | `ResentedPasses = 6` |
| Store | `KingdomSystem.LodgingGrace` `Dictionary<string,int>` | `KingdomSystem.ConversionResented` `Dictionary<string,int>` |
| Announce flag | separate GameObject property `KingdomLodgingUnhousedAnnounced` | the map entry itself (`-1` sentinel) |
| Consequence | `Emigrate` — "for want of a roof they would live under" | `Emigrate` — "rather than take a creed they never chose" |
| If emigration refuses | grace stays spent, retried next pass | identical |

**Three near-relatives that are not this shape:** secession and exile use a tiered-hysteresis
warning ladder with **no grace at all** (secession fires on the same pass dissent hits 100); guest
patience has a deadline and departure with **no announce whatsoever** (and declares itself
non-lossy, correctly).

**One precedent worth generalising:** the water rite's closure refuses to build its own exit and
hands the settler to conversion's pressure surface — *"there is one exit in this mod and this file
does not build a second one"* (`Experience/KingdomWaterRiteRules*.cs`).

### 3.2 The proposal — **the brink**

**New engine-free rules file** `Core/KingdomBrinkRules.cs` (added to `DevTests/TafTests.csproj`,
which is what makes it testable) plus a thin shell `Core/KingdomBrink.cs`.

**Rule 1 — reaching the threshold does not fire it.** A process whose accrual crosses an
irreversible threshold records a **brink**: subject (roll name), cause key, and `ReachedTick`. The
accrual then **stops**. This is the bound that makes clause 3 true: a thousand-day absence and a
ten-day absence arrive at the same place, because nothing accrues past the brink.

**Rule 2 — the pressure is a fact, re-derived every pass.** Adopt conversion's contract
explicitly. A brink whose cause has lifted is removed silently and the accrual resets from zero.
That is what makes the window arrestable *by acting*, never by waiting.

**Rule 3 — it announces once, at awareness, with the honest elapsed.** The `digest` guard in
`KingdomSystem.OnZoneActivated` already runs last in the dispatch and already computes uncapped
`HomecomingDays`. Each pending brink speaks there, by name and cause, in both registers and the
ledger, quoting the real time since `ReachedTick`:

> *Aeru has had no roof she would live under since the 3rd of Nivvun — thirty-one days. She will
> go if nothing changes.*

**Rule 4 — the window is spent in attended passes only, at the length the owning design names.**
`GracePasses = 2` for a roof, `ResentedPasses = 6` for a creed, and a **new named constant for
secession** — proposed 3, one rung under the ≥7 attended days the Rupture→Breaking span is already
tested at, so `TheLoudestWarningStandsForManyAttendedDaysBeforeTheCityLeaves` keeps its meaning
once accrual is uncapped. Absence never spends a pass. The window is the founder's; it exists only
in their presence.

**Rule 5 — if the window runs out, the consequence fires exactly as it does today.** No new
outcomes; only a new gate in front of the old ones.

**Instances at landing:** settler leaving for want of a roof, settler leaving a resented creed,
city seceding, settler emigrating from thirst (currently fires with a one-pass `Warned` rung that
is a de-facto window), shrine-pull conversion (currently fires with **no** announce — this is the
one place the brink adds a warning that does not exist).

**Two sentences, for the report:** *A process that reaches an irreversible threshold does not fire
— it records a brink (subject, cause, the tick it was reached) and stops accruing there, so a
thousand-day absence and a ten-day absence arrive at the same place. At the next awareness the
brink announces once by name with the honest elapsed time, then spends its window in attended
passes only — two for a roof, six for a creed, three for a city — arrestable at any point by
removing the cause rather than by waiting it out.*

**Hard constraint:** `DevTests/ExileRulesTests.cs:383-390` asserts by reflection that no public
static member of `KingdomExileRules` takes a `long` parameter or a parameter whose name contains
`tick`, `elapsed`, or `day`. The brink API must not be reachable from that file, or the test fails.

---

## 4. Save compatibility

`KingdomSystem` uses named-field serialization (`WantFieldReflection => false`, magic +
version + `WriteNamedFields`, `Core/KingdomSystem.z19.PersistenceAndCallbacks.cs:133`). Unknown names are skipped, missing
names keep their default, and `Read` accepts any version from `FirstNamedSerializationVersion = 2`
through `CurrentSerializationVersion`. **So adding fields is free; reinterpreting one is not.**

### 4.1 The one migration that matters

Every `Last*Tick` and `*Worked` stamp is currently re-anchored to `now` whenever more than three
days have elapsed. Once the cap is retired, **the first load of an existing save resolves the whole
real elapsed since a stamp that may be hundreds of days stale** — a season of upkeep drawn in one
pass, a settlement emptied by a ladder that never fired before.

**Required:** bump `CurrentSerializationVersion` to 3, and on reading a version-2 save, re-anchor
every clock stamp to `The.Game.TimeTicks` exactly once. The stamps to re-anchor:

`LastHeartbeatTick`, `LastFetchTick`, `LastVisitTick`, `LastDissentTick`, `NextArrivalTick`, and
the per-object stamps `KingdomRefineWorked`, `KingdomStrikeWorked`, `KingdomRepairWorked`,
`r_KingdomClearance.LastWorkedTick`, `r_KingdomPowerWork.LastResolvedTick`,
`r_KingdomPowerStore.LastResolvedTick`, `r_TAF_RoadsWalked`.

**Do NOT touch:** `r_KingdomNotice.PostedTick`, `KingdomCarryHaul.PlantedTick`,
`r_KingdomNotice.TakenTick`. These are kernel draw ordinals, not clocks — re-anchoring them
re-rolls every determinism question already answered.

### 4.2 Counters whose meaning changes (each needs a migration note or a new field name)

| Field | Today | After | Recommendation |
|---|---|---|---|
| `KingdomHardRunStreak` (GameObject int) | consecutive full-stretch attended passes, threshold 8 | full-stretch activity-days | New property name; drop the old. Cheapest, and the old value is meaningless in the new unit. |
| `ConversionShared` values (`Dictionary<string,int>`) | attended passes toward a creed, threshold 72 | cohabitation-days × closeness | **Highest risk.** Either re-derive 72 in days *and* scale stored values, or clear the map on migration and let osmosis restart. Clearing is safer and costs the player nothing they can see. |
| `KingdomShrinePull` (GameObject int) | attended passes, threshold 30 | attended-days then time | Same choice; clearing is safe (the counter resets on any non-Neutral stance anyway). |
| `KingdomSharedPasses` (GameObject int) | attended passes, ≤1/day | cohabitation-days | Near-identity because of the existing one-day gate. Carry forward unchanged. |
| `System.DryStreak` (int) | failed heartbeat *passes* | dry *days* | Clear on migration. Two constants (`DryIntervalsToEmigrate=2`, `ToWither=3`) need re-deriving in days regardless. |
| `System.Dissent` (int) | point total accrued under a 3-day cap | point total, uncapped rate | Meaning unchanged — only the future rate. Carry forward. |

### 4.3 The seat-swap trap

`KingdomSettlement.ReadFrom`/`WriteTo` (`Core/KingdomSettlement.cs:354-410`) carries per-city state
by **reflection on same name + exact type**, and throws `KingdomSeatMismatchException` when a field
declared on `KingdomSettlement` has no counterpart on the seat. Therefore:

- Adding a per-city field to `KingdomSystem` **only** → silently not carried. The dormant city
  keeps the seated city's value. **No exception, no test failure, wrong behaviour.**
- Adding to `KingdomSettlement` **only** → throws at the next seat swap.
- **Every new per-city clock field must land on both, in the same commit.**
  `DevTests/SettlementSeatTests.cs:136 EverySettlementFieldIsCarried` pins one direction only.

The brink store is per-city (whose housing failed is a fact about one city, exactly as
`LodgingGrace` and `ConversionResented` already are). Realm-level state — `Dissent`,
`DissentSpoken`, `LastDissentTick`, `LastRiteTick`, `RegardSpoken` — must stay off
`KingdomSettlement`, and `DevTests/SettlementSeatTests.cs:126 NoRealmStateIsCarriedByACity` pins
that.

---

## 5. Tests that pin today's behaviour and will need re-pinning

3,710 `[Test]`/`[TestCase]` attributes across 49 files; every test file is engine-free (rules only).

**Directly asserting the cap — these fail by construction and must be rewritten, not adjusted:**

- `DevTests/KingdomRulesTests.cs:73 UpkeepForElapsed_ForgivesTimeBeyondTheAbsenceCap`, `:86
  HeartbeatDays`, `:98 HeartbeatCheckpoint`, `:64 UpkeepForElapsed`, `:185/194
  PolicyUpkeepForElapsed*`
- `DevTests/KingdomPowerRulesTests.cs:79 ClampDays_ForgivesAbsenceBeyondTheCap`, `:85
  MaxDaysCredited_IsTheSameBargainWaterKeeps`, `:172
  WindAvailabilityPercent_ForgivesAbsenceBeyondTheCap`, `:220
  ChargeForDays_ASeasonAwayIsWorthTheSameAsThreeDays`
- `DevTests/KingdomRoadRulesTests.cs:129 AbsenceIsCappedNotBanked`, `:131-134` (the inline
  comment asserts "if that ever stops being true here, wear stops being witnessed-only")
- `DevTests/KingdomCreedRulesTests.cs:260 AbsenceCannotOutrunPresence`, `:262-265`
- `DevTests/KingdomMaterialRulesTests.cs:692
  RefinedThisPass_IsCappedPerVisitHoweverLongTheFounderWasAway`, `:348
  EffortWorked_IsCappedSoOneVisitNeverClearsEverything`, `:694`
- `DevTests/KingdomCropRulesTests.cs:87 CanAffordPlanting_ReserveIsExactlyThreeDaysOfCurrentUpkeep`
- `DevTests/KingdomUpgradeRulesTests.cs:130 ReserveDrams_IsTheWholeAbsenceTheSettlementIsEverChargedFor`
- `DevTests/KingdomPowerRulesTests.cs:294 Store_AbsenceAccruesAndNeverDecays`

**Asserting attended-pass denominators — these need their unit changed:**

- `DevTests/KingdomLodgingRulesTests.cs:420-472` (the whole grace block, including
  `AbsenceNeverRunsTheGraceBecauseNothingButAPassAdvancesIt` — which **stays true** under clause 3
  and should be kept verbatim as the brink's own guarantee)
- `DevTests/KingdomConversionRulesTests.cs:37-131` (`SharedLivingPerPass_*`, `MealCeiling_*`),
  `:232-249` (`AtMilestone_*`, `Milestone_*`, `SharedLivingForConversion_IsASeasonOfComingHomeAndNotAWeekOfIt`),
  `:424-451` (the resented grace — same "keep verbatim" note as lodging)
- `DevTests/KingdomWearRulesTests.cs:102-172` (`AtHardRunMilestone_*`, `HardRunMilestone_*`,
  `RollHardRun_*`)
- `DevTests/KingdomWaterRiteRulesTests.cs:332-371` (`ShouldCountPass_*`, `PassesAfter_*`)
- `DevTests/KingdomFaithRulesTests.cs:100-115` (`PullAfterPass_*`, `ConversionReady_*`)
- `DevTests/KingdomCreedRulesTests.cs:247 DissentAccruesPerAttendedDayAndClamps`, `:319
  TheLoudestWarningStandsForManyAttendedDaysBeforeTheCityLeaves`

**Guards that must keep passing unchanged (treat as acceptance criteria for the wave):**

- `DevTests/ExileRulesTests.cs:383 NothingHereTakesATick` — reflection ban on tick parameters in
  `KingdomExileRules`
- `DevTests/KingdomUpgradeRulesTests.cs:1118 NoTriggerPathReadsElapsedTimeAsACause` — allowlists
  the one authored-duration parameter by exact name
- `DevTests/SettlementSeatTests.cs:126/136` — realm-vs-city field partition
- `DevTests/KingdomCatalogueRulesTests.cs:119-186` — the equilibrium arithmetic. **Do not touch.**
  Subsidence must be built to satisfy these, not the reverse.

**Existing coverage gaps the wave should close (nothing pins these today):** the once-per-pass
`DryStreak++`; `MaxArrivalsPerVisit` and the arrival catch-up clamp; the raid re-warn grace band;
raid cooldown and warning lead; petition cooldown and lifetime; the manifest turn-back path;
`KingdomWear.RollWear`'s streak increment; `AdvanceRepair`'s day accounting; `KingdomMaterials`'
three workers; `KingdomPower.CreditDays`; `r_KingdomScaffold.TurnTick`.

---

## 6. Prose that asserts the old rule

The doctrine supersedes not only the original "absence accrues, never decays" absolute but also
its **first correction**. `_notes/STALE-COMMENT-INVENTORY.md` was written under the supply/social
split; its own "corrected" texts for wear, dissent, and conversion are now stale again under
clause 1, which names osmosis, dissent, and wear-from-running as things that happen as time passes.

| File | What to change |
|---|---|
| `VISION.md:77-85` | The absence pillar still says *"What absence never moves is anything between people — dissent, conversion, damage to the works."* Clause 1 moves all three. |
| `VISION.md:92-99` | *Hubris subsides* is correct and stays. |
| `STANDARDS.md:133-137` (§5.3) | "Witnessed-only accounting" is the binding standard and is now superseded. §5.4's "bounded consequence per visit" becomes per awareness. |
| `docs/API.md:36, 80, 109, 115, 134, 198, 284-286` | The public modding contract. `:36` already warns the ratchet will move; `:284-286` states the old split. |
| `MODDING.md:138, 478-480` | "counted in attended passes and never in time". |
| `TESTING.md:79, 176-177, 197, 239, 262, 305, 316, 347, 380, 396-405` | Steps 16p, 43, 44, 52, 57, 66b, 75a and the *Known v0 limits* block. **66b stays correct** (the grace is still attended-only); **75a and 57 change**; **396-405 is the block that describes the gap this wave closes.** |
| ~40 code doc comments | The canonical phrasings are `Core/KingdomRules*.cs`, `Growth/KingdomPowerRules*.cs`, `Growth/KingdomRoadRules.cs:21-25` and `:247-249`, `Growth/KingdomWear.00.r_KingdomWear.cs:257-261`, `Growth/KingdomWearRules.cs:8-12`, `Growth/KingdomMaterials*.cs`, `Growth/KingdomMaterialRules*.cs`, `Core/KingdomConversionRules.cs:112-120`, `Growth/KingdomLodgingRules*.cs`. |
| `_notes/balance-sim.py:51` | `read_const(RULES_CS, "MaxUpkeepDaysCharged")` raises `SystemExit` when the constant is not found. **Retiring the constant breaks the balance model at import.** |
| `_notes/STALE-COMMENT-INVENTORY.md` | Re-issue under the doctrine. |

---

## 7. Work packages

Ownership is disjoint by file. The chokepoints — `Core/KingdomRules.cs`,
`Growth/KingdomGrowth*.cs`, `Core/KingdomSystem.cs` — cannot be shared, so P1 owns them and P2/P3
take their narrow slices only after P1 lands.

### P1 — the substrate and the uncapping · **blocks everything** · balance re-run: **YES**

**Owns:** `Core/KingdomRules.cs`, `Growth/KingdomGrowth*.cs` (heartbeat, fetch, thirst only),
`Core/KingdomSystem.cs` (version bump + re-anchor migration), `Growth/KingdomPower.cs`,
`Growth/KingdomPowerRules.cs`, `Growth/KingdomMaterials*.cs`, `Growth/KingdomMaterialRules.cs`,
`Growth/KingdomWear*.cs`, `Growth/KingdomRoads.cs`,
`Growth/KingdomRoadRules.cs`, `Growth/KingdomCropRules.cs`, `Growth/KingdomUpgradeRules.cs`
(reserve only), `Trade/KingdomManifest.cs` (reserve only), `DevTests/TafTests.csproj`.

Retires `MaxUpkeepDaysCharged`; replaces `HeartbeatDays`/`HeartbeatCheckpoint` with an uncapped
pair over `Simulation/Kernel/TickMath`; uncaps the whole `*WorkedProperty` family, power, and
roads; swaps wear's streak to activity-days; settles the two reserve formulas onto one basis;
resolves the checkpoint-before-gate question; ships the version-3 re-anchor.

### P2 — subsidence and the equilibrium consumer · **after P1** · balance re-run: **YES**

**Owns:** new `Growth/KingdomSubsidenceRules.cs` + `Growth/KingdomSubsidence.cs`,
`Growth/KingdomCatalogueRules.cs` (summation helpers only — the existing arithmetic is frozen),
`Growth/KingdomSurvey.cs`, `Growth/KingdomGrowth*.cs` `UpdateStage` **only** (hand-off from P1),
`Core/KingdomSettlement.cs` + the mirrored fields on `Core/KingdomSystem.cs` (hand-off from P1),
`Options.xml`, `KingdomBuildings.xml`.

Sums `Carries` over `KingdomBuilt` works into `(water, food, roof, lift)`; calls the existing
`Equilibrium`; replaces the `>` ratchet with hysteresis both ways; closed-form convergence toward E
in coarse per-stage steps; ruins the overreach through `StandingPercent`; samples the trajectory's
breakpoints into dated chronicle entries; arrests the slide on arrival.

### P3 — the brink · **parallel with P2** · balance re-run: **NO** (social recalibration only)

**Owns:** new `Core/KingdomBrinkRules.cs` + `Core/KingdomBrink.cs`, `Growth/KingdomLodging.cs`,
`Growth/KingdomLodgingRules.cs`, `Core/KingdomConversion.cs`, `Core/KingdomConversionRules.cs`,
`Core/KingdomCreed.cs`, `Core/KingdomCreedRules.cs`, `Experience/KingdomFaith.cs`,
`Experience/KingdomFaithRules.cs`, `Experience/KingdomWaterRite.cs`,
`Experience/KingdomWaterRiteRules.cs`, `Core/KingdomLedger.cs`.

**Must not touch** `Core/KingdomExileRules.cs`. Builds the one window shape; migrates osmosis,
shrine pull, and water-rite shared living to cohabitation-time; uncaps dissent; gives secession a
named window; gives shrine-pull conversion the announce it has never had.

### P4 — deadline consolidation and the prose sweep · **last** · balance re-run: **NO**

**Owns:** `Raids/KingdomRaids*.cs`, `Trade/KingdomTrade.cs`, `Experience/KingdomGuestbook.cs`,
`Experience/KingdomGuestRules.cs`, `Experience/KingdomLocus.cs`,
`Experience/KingdomLocusRules.cs`, `Quests/KingdomBounty.cs`, `Quests/KingdomPetitions.cs`, and
every prose surface in §6 including `_notes/balance-sim.py`.

Folds the three independent "re-stamp the deadline when the founder shows up" implementations
(manifest turn-back, raid re-warn, arrival catch-up clamp) into one helper; lets guests arrive and
leave through an absence; carries the doc sweep. The prose half can start on day one and land last.

**Sequence:** P1 → (P2 ‖ P3) → P4.

### The balance model

`_notes/balance-sim.py` reads its constants out of the C# and refuses to run if they have moved, so
P1 breaks it at import. It models the **water economy only** — a grep for `refin|timber|stone|scrap|material`
returns nothing. The refined-material rebalance is therefore not merely unmodelled but unmodellable
in the current script.

Three inputs feed one equilibrium and must be re-run together in the same pass, not separately:

1. **Level** — `Carries` in settlers, against `UpkeepDrams(Population, Stage)` and
   `StageUpkeepPercent = {100,120,150,180,220}`. These have never been checked against each other.
2. **Supply** — uncapped fetch (`Hands × 2 × Days`) against uncapped upkeep. The cap currently
   hides whether this binds at all.
3. **Refined costs** — the sawyer/mason/smelter chain and `Bits`, priced against
   `EffortPerHandPerDay`, `RefineEffortPerUnit`, and `RawPerRefined`.

P2 cannot be calibrated without all three, which is why the doctrine puts the rebalance in the same
pass as the implementation.

---

## 8. The three riskiest rows

1. **Retiring `MaxUpkeepDaysCharged`** (`Core/KingdomRules.cs:260`). Nine production consumers,
   ~40 doc comments, five derived constants, and the balance model's import. The real danger is
   not the arithmetic but the first load: without the §4.1 re-anchor, an existing save with a
   stale checkpoint bills a season of upkeep and runs the thirst ladder to withering in one pass —
   the exact "unchosen debt" clause 4 forbids, delivered by the change meant to honour it.

2. **Osmosis passes → cohabitation-time** (`Core/KingdomConversionRules.cs:112-128`). The file's
   own load-bearing doc comment asserts the opposite in so many words; the threshold of 72 is
   calibrated in visits ("a season of coming home"); and the consequence is a permanent creed
   change with no window today. Migrating the stored counters without re-deriving 72 in days
   converts a city's minority population on the first load after the update.

3. **Subsidence** (unbuilt). Needs a summation that does not exist, a hysteresis replacing the only
   writer of `System.Stage`, and ruination of overbuilt ground under a protection law that forbids
   touching anything player-placed. It is also the only row in this map with **zero existing tests
   to re-pin** — so unlike every other change in the wave, a wrong answer here fails silently.


## Integration notes after P2 ∥ P3 (orchestrator, 2026-08-21)

Applied: LodgingGrace retired both seats (P3 req 1); `LastWaterWorkTick` both seats + water-works
production in growth (P2 req 1, planted-before-count like `LastFetchTick`); status report shows
`carries N` (P2 req 2); arrivals stop at the band's edge, gated on a MEASURED level only —
`SupportedLevel <= 0` means no pass has measured, and an unmeasured settlement may not refuse
arrivals on knowledge it lacks (P2 req 3, guard added).

NOT applied — P3 req 3 (honest ruin-tick on a roof brink): lodging never judges wear, so a ruined
home still houses its people and the hook has no trigger path. Recording at ruin would create
false brinks on livable homes. Needs condemnation semantics first: a wear threshold past which a
home stops counting as a roof in lodging, THEN the ruin site pre-records the brink at the
breakpoint's own tick. → P4.

## P4 additions (beyond deadline consolidation + prose sweep)

- Wear-condemnation threshold for homes + honest ruin-tick roof brink (above).
- Food has no L or XL design — the only binding good whose big works want hands (P2). New
  catalogue entry/entries; re-run balance Q6-Q8 after.
- Ruin→effectiveness→level feedback loop is unmodelled in balance-sim (bounded by MaxWearPercent
  and FloorLevel, but unmeasured) — add to the model.
- STANDARDS §5.3/§8 still assert witnessed-only accounting "with an absence cap" (P1 flag).
- Chronicle budget: a City→Camp collapse writes ~58 of the 200-entry cap (P2 risk c) — consider
  coarser sampling for long slides.


## After P4 (orchestrator) — known-open, deliberate

- Bounty manning's mixed PASS/RAW denominator (§2f, class B, low risk) — left as-is by P4
  mechanics; smallest remaining swap, fold into the next mechanics change that touches
  `Quests/KingdomBounty.cs`.
- Staffless works never wear-reduce their level contribution (`KingdomSubsidence.Supports`
  staffed-only ternary): a ruined reservoir still carries its full drams. Deliberate-for-now,
  documented in API.md and balance-sim Q9. WANTS AN AUTHOR RULING.
- Food Carries are not a flow — nothing fills a larder from a field; water's half was wired this
  wave. Next candidate when food becomes physical.
- Uncapped mending can finish a large repair in one long-absence resolve; bounded by the
  one-mending-settlement-wide gate; unmodelled.
- `MaxRefinedPerDay = 8` untuned against grand-build costs; refined chain still unmodellable in
  balance-sim (map §7).


## Heart wave follow-ups (orchestrator, 2026-08-22)

- `Growth/KingdomRoads.cs:422` + `Growth/KingdomCommission*.cs` still call `TryHeart` at weight 1 —
  roads/single-cell siting keep the drifting centre while plots use the tier-scaled one.
- `KingdomSocket` re-build list can offer heart-rung keys and re-type the heart plot — needs the
  same second-heart refusal the commission/plan paths have.
- A rite poured over open water refuses rung 1 and leaves the survey with no heart plot — needs
  a re-attempt hook.
- Author-facing WR: a `Heart="yes"` catalogue attribute (KingdomData + KingdomMergeRules) so
  third parties can author new rungs; today the ladder is a fixed key list mirrored in the
  checker.
- W4 doc requests from the heart agent: MODDING (mergeable heart keys, new blueprints/props),
  API (heart surface on PlotRules/Plots/CeremonyHeart), CHANGELOG, TESTING step; book status
  line "the heart: <rung>, N plots marked to yield".
