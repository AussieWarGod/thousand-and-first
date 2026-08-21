# Supported API — The Thousand and First

Everything listed here is a supported contract: it changes only under the versioning rule
in [STANDARDS.md](../STANDARDS.md) §9 (no removals in a minor release; deprecations marked
`[Obsolete]` with a named replacement and kept working for at least one minor cycle).
**Anything not listed here is internal and may change without notice** — if you need
something that isn't here, open an issue and it can be promoted deliberately.

Most extension needs no code at all: see [MODDING.md](../MODDING.md) for the XML registries
(`KingdomBuildings.xml`, `KingdomDeals.xml`), which are the preferred extension path.

## Getting the system

```csharp
using ThousandAndFirst;

KingdomSystem kingdom = XRL.The.Game.RequireSystem<KingdomSystem>();
if (kingdom.Founded) { /* ... */ }
```

`RequireSystem` creates the system if absent, so it is always safe. Every property below is
readable at any time; a kingdom that has not been founded reports `Founded == false` and
neutral values.

## `KingdomSystem` — the game system

| Member | Contract |
|---|---|
| `bool Founded` | True once a kingdom exists. Guard every other use with this. |
| `string SeatName` | The seated city's own name; falls back to the realm's display name for saves written before cities had names apart from their realm. |
| `int SettlementCount` | Cities the realm holds, 0–2. |
| `KingdomSettlement Away` | The city the founder is not standing in, or null. |
| `KingdomSettlement Capture()` / `void Restore(KingdomSettlement)` / `bool TrySeat(Zone)` | Move the seat. `TrySeat` runs from `ZoneActivatedEvent`; the others are for tools and tests. |
| `string KingdomFactionName` / `KingdomDisplayName` | The runtime faction's name and display name; null when unfounded. |
| `string Style` | City style key (`common`, `verdant`, `fungal`, `gyre`, `eater`, or one your mod declares). Drives which building designs are offered. |
| `GrowthStage Stage` | `Camp`, `Steading`, `Village`, `Town`, `City`. **Moves in both directions.** `KingdomSubsidenceRules.StageWithHysteresis` is the only writer: it climbs on the reading and falls only on a clear shortfall (20% benefit of the doubt on both of `StageFor`'s inputs), one rung per reckoning, with `Camp` an absolute floor. Read it, never assume it — and never assume a rung already reached is kept. |
| `int SupportedLevel` | Settlers the settlement's finished works honestly carry, from `KingdomSubsidenceRules.SupportedLevel`. **Knowledge, not truth**: it is as fresh as the last pass that measured it, and `0` means no pass ever has. Consumers that refuse something on it must check for that. |
| `int NotableShade` | What the settlement's named notable is worth to that level (`KingdomCeremonyRules.NotableShade`: met tastes, the virtue net of the flaw, and met `Prefers`). Written when the office is filled or passes, so it is as stale as the last time it changed hands; `0` for a settlement that has named nobody. Never negative, and bound again by `KingdomCatalogueRules.LiftCapPercent` when the level reads it. |
| `string SubsidenceBinding` | Which of `water` / `food` / `roof` is the least of the three and therefore what holds the level down, or null before a measurement. |
| `long LastWaterWorkTick` | Checkpoint for water-works production. Planted on first read and advanced with `KingdomRules.AdvanceCheckpoint`; never a cap. This is what makes a catalogue `Carries="water:N"` a **flow** as well as a level, and therefore why only a design with a real producer part may declare one. |
| `long LastFoodWorkTick` | The same, for the fields. Its own stamp rather than a share of the water one, because the two producers are separately blockable — a settlement can have casks with room and no larder dedicated at all. |
| `int Population` | Living settler count. |
| `bool Withered` | True while a sustained thirst has suspended prosperity. Recoverable. |
| `int DryStreak` / `int HungerStreak` | Heartbeat resolves in a row the water bill and the ration bill went unpaid. Separate counters: both ladders run at once and each keeps its own memory. What stops them costing double is `KingdomRules.ComposeScarcity`. |
| `bool Famished` | The food mirror of `Withered`. **Both marks may stand at once** — a mark is a state, not a cost, and only the cost is capped. |
| `List<string> ClaimedZones` | Zone IDs the kingdom holds. |
| `Dictionary<string,string> ZoneDistricts` | Zone ID → district key. |
| `List<string> ChronicleEntries` / `OutsiderEntries` | The two registers, oldest first, capped. |
| `Dictionary<string,int> OriginCounts` | Settler origin → count (population composition). |
| `int GetStanding(string factionName)` | The kingdom's own standing with a faction — **separate from player reputation**. |
| `void SetStanding(string, int, bool mirror = true)` | Set standing and mirror the faction's feeling toward the kingdom. |
| `void AdjustStanding(string, int, bool mirror = true)` | Apply a delta. Prefer this over writing `Standings` directly. |
| `void MirrorFeeling(string factionName)` | Re-write one faction's feeling from its standing. Safe when unfounded. |
| `static void Guard(string step, Action work)` | Run work inside engine dispatch without letting exceptions escape. Use for any code the engine invokes. |

## `KingdomFounding` — founding, territory, citizenship

| Member | Contract |
|---|---|
| `static Faction Found(string name)` | Founds the kingdom (idempotent — returns the existing faction if already founded). |
| `static bool ClaimZone(Zone z, bool force = false)` | Claims a zone; requires adjacency to existing ground unless forced. Adjacency includes the stratum directly above or below (`ZonesAdjacent`), so a cellar or a tower is a claim now, not only a founding-day accident. |
| `static bool EnrollCitizen(GameObject citizen)` | Makes a creature a citizen. Enrolled creatures are protected from kingdom-driven removal. |
| `static SecondFoundingVerdict JudgeSite(KingdomSystem, Zone)` | What the rite would do on this ground. |
| `static bool FoundSecond(string name, string vocation, Zone site, bool force = false)` | Founds the realm's second city. `force` waives only the not-adjacent requirement. |
| `static KingdomZoningRules.ClaimVerdict JudgeClaim(KingdomSystem, Zone)` | What the founder's own claim on this ground would do — gathers the facts off the world (ours, the other city's, an exiled realm's, foreign, adjacent) and hands them to the pure verdict below. The engine-coupled half of `KingdomZoningRules` § *The claim*, below. |
| `static string StyleGroundClause(string style)` | Lower-case founder-facing clause naming what the ground promises for a city style ("common ground", "ground green enough to root a verdant city"). Presentation only — `KingdomRules.StyleForSite` owns which style a site resolves to. |

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
Addendum 11(b)'s gate, and it is one rule read in one place: an unsown field carries **no `food`
at all** — not to the level, not to the day — because `KingdomCrops.WithoutUnsownFood` strips the
`food` entry out of its parsed `Carries` inside `KingdomSubsidence.Supports`. Everything else the
design carries is untouched; a home farm's mill is built whether or not a row is in the ground.
An unsown field also drops out of `KingdomSurvey.Works`, so the staffing pass never sends anybody
to stand in it.

**The seed items.** Five, one per style's crop family, in `ObjectBlueprints.xml`. They are
ordinary items rather than `Food` — a seed the ration draw can eat is a seed corn that quietly
disappears. Three honest sources: the tier-1/tier-2 wares tables (`PopulationTables.xml`), a
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
| `KingdomCrops.WithoutUnsownFood` / `CycledFoodPerDay` | The gate, and the subtraction that keeps a sown field from being paid twice. |
| `KingdomCrops.AttemptSow` / `Withdraw` / `TakeWildSeed` / `LayRows` / `ClearRows` / `RowsOf` / `SetRipe` | The engine-coupled half. Only rows this file created and marked are ever destroyed. |
| `KingdomCrops.RecordLarders(KingdomSystem, Zone, KingdomSurvey, long)` / `LarderRoomElsewhere` / `DeliverPending` / `Deposit` | Cross-zone delivery. Both records now read and write the seated settlement's city book (`KingdomSettlement.City`) rather than the retired `r_TAF_Larders_*` game-state pair; the numbers are the same, the home is one. |
| `KingdomSystem.PendingCrop` / `PendingCropBlueprint` | One city's harvest still on the road, carried by the seat swap on its own name. |

## Food as a flow — what the fields make and the people eat

Food is physical, opt-in, and denominated in people, exactly as water is physical, opt-in and
denominated in drams. The two lanes are mirrors, and the three places they deliberately part
company are the interesting part.

| Member | Contract |
|---|---|
| `KingdomRules.RationsPerDay(int population)` | What the settlement eats in a day: **one ration a settler, at every rung**. No stage term — see the divergences below. |
| `KingdomRules.RationsForElapsed(int population, long elapsedTicks)` | The same over whole elapsed days. Uncapped and saturating, exactly like `PolicyUpkeepForElapsed`. A bill, never a debt. |
| `KingdomRules.ForagedRations(int hands, int days)` / `ForageRationsPerHand` / `MaxForagedRationsPerDay` | What free hands bring in off the land. Two a hand a day under a flat daily ceiling of four; the ceiling is applied to the **rate**, before the days multiply out. |
| `KingdomRules.ResolveHunger(int streak, GrowthStage, int population)` → `HungerOutcome` | The hunger ladder: `Fed` / `Warned` / `Emigration` / `Famine`. Rung for rung the same shape as `ResolveThirst`, with the same two floors — a Camp is never marked, and `LoyalCoreSettlers` never leave. |
| `KingdomRules.ComposeScarcity(ThirstOutcome, HungerOutcome)` → `ScarcityVerdict` | **The composition rule.** See below. |
| `KingdomRules.ScarcityDepartureClause(bool, bool)` / `ScarcityDepartureNote(bool, bool)` | The chronicle's and the ledger's words for a departure, naming whichever scarcities are actually true. |
| `KingdomRules.LarderCapacityTag` / `DefaultLarderCapacity` / `LarderCapacity(int declared)` | How much a dedicated container holds. Declared on the **blueprint**, never in the catalogue. |
| `KingdomRules.CivicLarderBlueprints` / `IsCivicLarderBlueprint(string)` | Which commissioned designs auto-dedicate as pantries (STANDARDS §7's "commissioned storage auto-flags"). |
| `KingdomSurvey.FoodStored` / `FoodCapacity` / `FoodSpace` | The food side of `StoredWater` / `StorageCapacity` / `StorageSpace`. `FoodSpace` is **derived** from the other two, so a caller that puts food in by another road cannot leave it stale. |
| `KingdomSurvey.StoreFood(int, string blueprint)` / `ConsumeFood(int)` / `ConsumeFood(int, string preferred, out int fromPreferred)` / `ConsumeCrop(string, int)` / `SpoilFrom(GameObject, int)` / `AdoptLarder(GameObject)` | The food mirrors of `Store` / `Consume` / `LeakFrom`, plus the dedication of a commissioned pantry. All keep the survey's counters in step; all return what actually moved rather than what was asked for. The three-argument `ConsumeFood` is the **meal-shaped** draw (below); `ConsumeCrop` is the mill's input half — one named blueprint only, so a mill never grinds the staple it just made. |
| `KingdomSurvey.Kitchens` | Finished works here carrying vanilla's `Campfire` — the communal fire, and the oven above it. A settlement with none cannot cook, however full its larders are. |
| `KingdomGrowth.FoodMadePerDay(KingdomSurvey)` | What the settlement's works bring in in a day *without growing it and without grinding it* — `KingdomSubsidence.Supports(survey).Food` less `KingdomCrops.CycledFoodPerDay(survey)` less `KingdomCrops.MilledFoodPerDay(survey)`, at exactly the effectiveness the level is summed at. A sown field's food is delivered physically by its own cycle and a mill's by its own grinding, so each feeds the settlement exactly once; an unsown field is already zero here, and not by subtraction. |
| `KingdomGrowth.ScarcityEnabled` / `ThirstEnabled` / `HungerEnabled` | One switch (`r_TAF_OptionThirst`) for both binding goods. A founder who turned scarcity off did not ask to keep half of it. |

**The identity the lane is built on.** One point of `food` is one settler fed for one day, and
`RationsPerDay` charges one ration a settler a day, so *a settlement standing at its own supported
level makes exactly the rations it eats*. That only holds because **every** food work is counted
in the flow at exactly the effectiveness it is counted at for the level — a design counted for
one and not the other would be a level a settlement could reach and then starve at. Since Wave G2
the growing designs are counted through their **cycle** rather than through the day, and the
identity survives because the cycle pays exactly what the `Carries` promised over one crop's days:
`rows × YieldPerRow == food × CropDays`, asserted per design in `_notes/balance-sim.py` §G2.

**Where food is not water's mirror, and why.**

1. **No stage rate.** Water is billed 100/120/150/180/220 per hundred by stage and its `Carries`
   are divided back out by the same percentage (`KingdomSubsidenceRules.LevelFromWater`). Food is
   billed flat and handed to `Equilibrium` undivided, because a dinner is counted in people. This
   is what makes the identity above true; a stage term here would invalidate every food figure in
   `KingdomBuildings.xml`.
2. **No stores policy, no district discount.** Thrift's own blurb says what it is ("the
   water-keepers ration"), and the agrarian district's upkeep discount is already spent on the
   water side. Neither is applied twice.
3. **Foraging is a ceiling, not a pool.** The water detail's haul is bounded by how much open
   water is actually standing there; foraging is bounded by a flat four a day whoever walks the
   ground. And foraged food is eaten hand to mouth rather than stored, so a settlement that has
   dedicated no larder still eats — which is why a Camp self-sustains with nothing commissioned,
   the same promise the water lane makes when half a camp is on the detail.

**The composition rule — no death spirals.** Both ladders run; each keeps its own streak, says its
own sentence, sets its own mark. What a failed resolve *costs* is
`ComposeScarcity`'s **maximum of the two, never their sum**: at most one departure per resolve
however many things are wrong, so a settlement that is dry *and* starving empties no faster than
the worse of the two alone would. A city may be `Withered` and `Famished` at once and still lose
exactly one settler for it. Subsidence is untouched underneath both — it is the *structural*
consequence of standing above what the works carry, and these are the *immediate* one.

### Meals, not ticks — the favoured dish (Addendum 11(b))

The day's rations are the same servings they always were; what changed is that they are now a
**meal**, drawn in a stated order and worth something afterwards. The whole chain is one thing:
the fields grow the crop, the mill binds it into the **staple**, the staple is the first
component of the settlement's own **dish**, and the ration draw reaches for it first.

| Member | Contract |
|---|---|
| `KingdomRules.DeriveDish(string realm, string creedRecipe, string crop)` → `FavoredDish` | Pure, total and deterministic. **Creed picks the form, ground picks the body.** `creedRecipe` is the dominant creed faction's own vanilla `WaterRitualRecipe`; `crop` is `KingdomCropRules.CropBlueprintForStyle`. No input is an error — a realm of mixed people eats a stew. |
| `KingdomRules.DishFormFor(string creedRecipe)` / `DefaultDishForm` | Vanilla's eight favourite dishes → a form word. Every word returned is one of `CookingRecipe.ingredientTileTypes`, which is what gets a derived dish a drawn tile instead of a defaulted one. |
| `KingdomRules.CropWordFor(string crop)` / `PreservedStapleFor(string crop)` | The crop as an ingredient, and what it becomes when it is bound to keep. Three staples are vanilla's own `PreservableItem Result`; two crops vanilla cannot preserve get mod blueprints that *inherit* the nearest shipped preserve. `PreservedStapleFor` returns null for a crop this build has no staple for, and `KingdomCrops.StapleFor` then falls back to the crop's own `PreservableItem`. |
| `KingdomRules.DishRecipeType` | `"r_KingdomFavoredDish"` — the one `CookingRecipe` subclass every realm's dish resolves to, in `XRL.World.Skills.Cooking`. It reads its display name and components off `KingdomSystem`, so one class serves every settlement. |
| `KingdomDish.Ensure(KingdomSystem, bool announce)` | Derives and stamps the dish onto the realm's **`Faction`** (`WaterRitualRecipe`, `WaterRitualRecipeText`) and onto `KingdomSystem`. Idempotent; called at founding and on every settlement pass, so a city whose creed drifts changes what it is known for and says so once. `RecipeGenotype` is deliberately never set — both gates it drives refuse somebody dinner. |
| `KingdomSystem.DishName` / `DishText` / `DishStaple` / `DishSource` | Realm state, not city state: a realm has one faction however many cities it holds. |
| `KingdomRules.JudgeMeal(int owed, int fromDish, int fromStores, bool hasKitchen, GrowthStage)` → `MealVerdict` | `None` / `Scraps` / `Plain` / `Favored`. Favoured wants a kitchen standing **and** `FavoredMealPercent` (50%) of the day off the staple. `Scraps` — the larders gave nothing — is only spoken from `ScrapsSpokenFrom` (Village) up, because living off the land *is* what a camp does. |
| `KingdomRules.MealShadeFor(MealVerdict)` / `FavoredMealShade` | One settler, for exactly one day. Never a penalty at any reading. |
| `KingdomSystem.MealShade` / `LastMeal` / `ScrapsAnnounced` | Carried by the seat swap like every other city field. `MealShade` is **re-drawn every heartbeat**, never accumulated. |
| `KingdomSystem.Shade` | What the level actually reads: `NotableShade + MealShade`, each floored. `KingdomSubsidence` reads this rather than either half, so the two can never disagree about which shades count. |

**Why one settler and why one day** — both are vanilla's arithmetic rather than dials. A
non-player eater's `ProceduralCookingEffect` expires at `StartTick + 1200` ticks and
`KingdomRules.TicksPerDay` is 1200; only one meal effect stands at a time. So a settlement is
well fed for the day it ate and no longer, and the lift rides the same term as a notable's shade
and a shrine's spirit — `KingdomCatalogueRules.LiftCapPercent` binds it again on top.

**The draw order, stated once.** Larder by larder in survey order, item by item in inventory
order: the staple first, then everything else that is food. Nothing is random, so the same
larders drained in the same sequence give the same answer on every reload — what Addendum 12(d)
asks of any draw that lands on containers a founder can open.

### Industry eats food — the mill (Addendum 11(b)/(c))

| Member | Contract |
|---|---|
| `KingdomCrops.IsMill(GameObject)` | Asked of the **object**, off vanilla's own `Mill` part, so a third party's millstone counts the moment it declares one. |
| `KingdomCrops.MilledFoodPerDay(KingdomSurvey)` | What the mills are counted for by the level, at the effectiveness the level counts them at. Subtracted from `FoodMadePerDay` for the same reason a sown field's is: the mill delivers its food physically. |
| `KingdomCrops.StapleFor(string crop)` | The stated staple, else the crop's own `PreservableItem.Result`, read off a sample. |
| `KingdomRules.PreserveMultiple` / `MillCropsPerDay` / `MilledGain(int)` / `CropsForGain(int)` | The conversion. **Two crops in, six staples back, a net of four** — which is exactly the grinding mill's declared `Carries="food:4"`. `_notes/balance-sim.py` §G3 asserts that identity against the catalogue XML. |
| `KingdomRules.MillableStock(int foodStored, int population)` | Everything above one day's rations for everybody living here. **Industry never eats before the residents do**: the grinding runs after the heartbeat has drawn the day, and even then only on the surplus. |
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

**`TeachesDish` — not taken, and why.** The survey lists it as a free carrier (no vanilla
blueprint uses it). It is a per-*creature* override that sits **above** the faction recipe in
`WaterRitualCookingRecipe`'s resolution order — so with the faction recipe set, every citizen of
the realm can already teach the dish, and `TeachesDish` would only let one named cook teach a
*different* one. That needs the citizen-spawn path and buys nothing this wave asked for. Named
here as a deliberate omission rather than an oversight.

**Spoilage.** `KingdomWearRules.LeakKind.Food` is Addendum 10(b)'s explicitly deferred third kind
("food spoilage waits until food is a flow"), now spent: a damaged larder loses servings on world
days exactly as a damaged cistern loses drams, through the same `Leaked` arithmetic, announced
once by name and unsaid when it is mended. Spoilage runs *after* the ration draw in the pass, so
it can never be the reason a settlement goes hungry — only the reason it has no cushion when
something else is.

## Acting on its own judgment

`KingdomBounty` (posted prices: no escrow, completion-paid, deterministic taker draws),
`KingdomRoads` (worn ground: traffic accrual from stored tick stamps, founder paving),
`KingdomYards` (one yard trade per small house, registry in `KingdomYardWorks.xml` — mergeable),
`KingdomGuestbook` (notable guests with hooks that decay into rumors, and the carry-sign's
distance-scaled hauls, one in flight, mirroring the water manifest's honesty rules).

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
the office machinery has named a head. `KingdomMaterials` gains the refined tier (shaped timber /
shaped stone / worked metal via staffed yards), vanilla bits (`Bits=`) and exotic finds
(`Exotics=`) as high-craft prices, and the yard gates on L/XL construction. `KingdomCrews` /
`KingdomCrewRules`: capability from settler stats (`CrewNeeds="strength:16"`), ablest-first
deterministic assignment, shortfalls slow and named. `KingdomWear` / `KingdomWearRules`:
damage from raids, hard running and temperamental tech — never from the calendar — bounded,
mending auto-queued and holdable, costed from the chain. Hard running is counted in
**activity-days** (`KingdomRules.ActivityDays`), so a work that ran hard through an absence wore
for it and a work standing idle did not. `WorkEffectiveness` (Addendum 10(b)) is what ANY
finished work is worth this pass, crewed or not — a work that wants a crew runs at its crew
stretch reduced again by condition, a staffless one at its condition alone, so ruin now reaches
the water and roof lanes too and not the food lane only. The one exception to "never from the
calendar": what an already-damaged STORE goes on losing runs on **world days**
(`Leaked` / `LeakKind` / `LeakDaysToEmptyAtCeiling`) until it is mended — the damage is still an
event, only its consequence is a clock, and mending unsays it.

## How belief moves

`KingdomConversion` / `KingdomConversionRules`: osmosis (shared living under one roof, scaled by
closeness, accrued in **cohabitation-days** of real shared living rather than in visits), culture
(shared meals, capped), and the resented-pressure exit (warned once and pushed to the founder,
its window spent in world-days, emigrating through the ordinary machinery). A conversion about to
happen stops at a **brink**, the founder is told wherever they are, and eighteen days later it
happens whether or not they came back; see below. **`KingdomConversion.Convert` is the
one path a conversion may take** — it alone keeps the creed tallies, pressure entries, and the
two-register dispute honest. `KingdomFaith` (consecration; staffed shrines converting the neutral
of their zone; staffed scriptoria softening the grudge one band) and `KingdomWaterRite` (the rite
turned inward: consented, priced, refusal-with-reasons, the fourth asking shutting the question)
both route through it.

## The quality-of-life vocabulary, and lodging

`KingdomQolRules` / `KingdomQol`: one namespaced tag vocabulary — buildings declare `Provides`
(catalogue attribute, merged like every other; roofs contribute their own), residents carry
Needs / Prefers / Refuses, derived first from vanilla parts (Robot, aquatic brains, LiveFungus,
PhotosyntheticSkin, Inorganic) and refined by `r_TAF_*` blueprint tags, with `-tag` removing a
derived entry. Unknown tags are inert. `KingdomLodging` / `KingdomLodgingRules` assign every
settler an address: Needs gate the home; housemates are gated by the closeness ladder — Packed shares only without
quarrel, Close refuses the ambient grudge, Roomed tolerates it, and open hostility (≥100, the
named fault lines) refuses any shared roof at every tier. `Refuses` tags are absolute. Closeness
derives from beds-per-footprint density, `Closeness` attribute overriding. Arrivals join only if a
home they would accept exists. A settler whose acceptable housing is lost does not start a
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
`KingdomQolRules.CohabitHostility` / `JudgeCohabitation` are the flat single floor the vocabulary
shipped with; the ladder above **supersedes** them and they are kept only until they are retired —
do not write new callers against them.
Tastes and displacement tolerance query this same vocabulary.

## Layering, footprints, sockets, and the trigger law

Catalogue files **layer** (`KingdomMergeRules`): merge-by-key on raw attributes inside the single
XML pass — named overrides, omitted survives, blank erases, skins append (same key replaces),
chains extend across files; the post-merge design is what the validator sees. A tier declares
`Footprint="WxH"` and `Roof="Open|Soft|Walled|Carved"` (absent = fills its plot, walled);
footprint ≤ plot is enforced at load and refused by name at improvement; yard = plot − footprint,
recomputed per tier. `KingdomSocket` keeps a struck plot as a re-buildable slot, converts within
the plot's type×size set for one disclosed figure, and re-dresses standing buildings with any
registered skin. The upgrade trigger law (`KingdomUpgradeRules`): housing auto-upgrades only when
residents can be displaced to their own `LodgingStandard`; working buildings additionally need the
reserve to cover the outage (`AbsorptionDemand`), else the verdict is a held offer (`IsOffer`),
forceable via `KingdomUpgrade.Force` with the dip disclosed. No trigger reads elapsed time.

## Plots, materials, and gates

The unit of building is the **plot** (`KingdomPlots` / `KingdomPlotRules`): S/M/L/XL rects,
stage-gated, sited by the layout grammar, raised in stages, carved underground. Materials
(`KingdomMaterials` / `KingdomMaterialRules`) come from clearance — never minted — and live in
dedicated stockpiles; building costs are water plus materials, and condemning returns half.
Commissioning is gated by `KingdomZoning` (district, territory, known designs, derived craft
level) with every refusal naming its fix; designs improve through `KingdomUpgrade` chains that
carry every civic mark. `KingdomCatalogueRules` validates the building XML schema; all of it —
plots, materials, gates, chains, skins, contents — is authorable from mergeable third-party XML.

## `KingdomZoningRules` — the four gates, the stratum, and the claim

Pure and engine-free (`KingdomZoning`, same folder, is the engine-coupled half: reading a real
zone's district, the founder's data disks, the certified machines, and the settlement's own
roster of peoples). Checks run in `ZoningVerdict` order, most fundamental lack first, district
last, and stop at the first refusal — the founder is told one thing to fix, not four.

| Member | Contract |
|---|---|
| `enum ZoningVerdict` | `Permitted`, `RefusedUnlearned`, `RefusedTechLevel`, `RefusedTerritory`, `RefusedStratum`, `RefusedDistrict`. |
| `readonly struct ZoneGate` | The four OPTIONAL gates parsed off one `<building>` entry: `Districts`, `MinZones`, `Knowledge`, `MinTech`. `ZoneGate.Open` gates nothing, which is what an entry written before these gates existed parses to. |
| `readonly struct ZoningJudgement` | `Verdict` plus `Detail`/`Note` — what's missing, in the settlement's own words, and the menu's short tag. `Allowed` for a design with nothing to prove. |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster)` | The four-gate verdict, stratum untested (equivalent to `Underground: false, RequiresSky: false`). |
| `static ZoningJudgement Judge(ZoneGate, string tileDistrict, string category, int claimedZones, IEnumerable<string> roster, bool underground, bool requiresSky)` | The same, with the stratum folded in. A design whose plot spec declares `Sky` is refused **`RefusedStratum`** (`Note: "wants open sky"`) on ground below `KingdomRules.SurfaceZLevel` — checked before the district, so the menu itself carries the tag at the moment the founder is choosing, rather than only once they've picked the design and `KingdomPlotRules.RefuseSky` turns them away at the plot. Only the surface-only half of a per-stratum catalogue is expressible from what a design declares today — nothing yet says "this design belongs to the deep". |
| `static bool StratumAccepts(bool underground, bool requiresSky)` | The one depth rule the catalogue can state today: `!(underground && requiresSky)` — weather does not reach under the rock. |
| `static string StratumName(bool underground)` | `"under the rock"` / `"open sky"`, for the sentence that names it. |
| `static int ZonesForStage(GrowthStage)` | How many zones a city of this stage may hold at most: Camp/Steading 1, Village 2, Town 3, City 4. Read off the catalogue's own `MinZones` pairs, not chosen separately — the two-zone designs are `MinStage="Village"`, the three-zone `Town`, the four-zone `City` — so a settlement reaches the ground a design wants at the same moment it reaches the stage that design wants. |

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

The founder's own claim action is `KingdomCharterPart.ClaimGround` (Charter → **Claim this
ground**, hotkey `6`) — the first caller `KingdomFounding.ClaimZone` has ever had outside the
founding rite and two debug wishes. **It costs nothing**, which is a decision: the brief prices
founding and every building and names no price for a claim, because what a claim actually costs
is paid afterward and in kind — a new wall line to raise, a new budget of ground to lay, and a
stage that has to have been earned first. A claim that goes through reports the wall clause and
the holding line together, so the founder always knows how much more the rung allows.

## City plans — three ways a thing gets built

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

## `KingdomCreed` — what a city believes, and what that costs a realm

A settler may carry a creed: a real Qud faction, drawn from factions the realm has dealt with and
weighted by its standings. A city's creed is the one its residents share; a mixed city has none.

Dissent between two cities of one realm is read from **the engine's own faction feeling**
(`Faction.GetFeelingTowardsFaction`, which falls through to the faction's `"*"` wildcard) rather
than any table of ours — so it is correct for modded factions for free, and the zealous factions
that dislike strangers by default are exactly the ones that make a realm hard to hold together.

| Member | Contract |
|---|---|
| `CreedOf` / `SeatCreed` / `AwayCreed` | The creed a city holds, or null. |
| `Draw` / `Record` / `Forget` | Creed at arrival, and its removal on death or departure. |
| `RiteAvailable` / `HoldRite` / `EaseForMeal` | The founder's levers against dissent. |
| `DeclarableCreeds` / `Declare` | Name the realm's creed: decisive, and costly across the world. |
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
state lives in `KingdomSystem`'s own fields, which is what every consumer reads — and the other
waits in `KingdomSystem.Away` until the founder walks into its ground.

| Member | Contract |
|---|---|
| `string SettlementName` / `string Vocation` | The city's own name and what it was founded for. A null vocation is the realm's first city, founded before there was a second to tell it from. |
| `const int MaxSettlements` | 2. A realm holds no more. |
| `static string[] Vocations` / `VocationBlurbs` | The fixed vocation set and its menu prose. |
| `static bool IsKnownVocation(string)` / `VocationClause` / `VocationSuffix` / `VocationBlurb` | Vocation validation and presentation; an unknown vocation degrades to the neutral one. |
| `static SecondFoundingVerdict JudgeSecondFounding(bool founded, int settlementsHeld, bool groundIsClaimed, bool groundIsAdjacent)` | Pure rule for whether the rite founds a second city. |
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
enough — a granary zone the city has been drinking out of owes a food landing and a water draw at
once — so the row carries three signed figures and the weighted `owed` §3.5 reports is derived from
them. A draw is spread across the zone's dedicated vessels **oldest dedication first**
(`KingdomCity.DedicationOrderProperty`, minted the first pass that counts a container as the
city's), which is deterministic without a draw and stable across a reload. What the containers
could not cover stays on the row and is told; it is never silently forgiven.

| Member | Contract |
|---|---|
| `KingdomSettlement.City` / `KingdomSystem.City` | The settlement's book, and the flat field the seat swap carries it in. |
| `KingdomCityBook` | The serialized carrier: named-field `IComposite`, flat primitive columns, one row family per group of columns. `Normalize()` repairs a book read from a save — a null column becomes empty, **ragged columns are truncated to the shortest** (a row half of whose fields are missing is not a row), rows past their cap are dropped, and an overlong told-log keeps its newest lines. |
| `KingdomCityBook.ZoneCount` / `WorkCount` / `ResidentCount` / `ToldCount` / `TryZoneRow(string, out int)` / `TryResidentRow(int, out int)` | What the book holds, and the two lookups every re-plumbed reader goes through — by zone id for a sighting, by `KingdomResidentId` for a person. |
| `KingdomCityBook.TryReadBrink(int, BrinkKind, …)` / `TryWriteBrink(int, BrinkKind, …)` | Where a settler's brink windows live since W2. Column reads keyed on the resident id, not a whole-model read: three consumers ask this once per settler per pass. |
| `KingdomCity.CheckIn(KingdomSystem, Zone, KingdomSurvey, long)` | The pass's first word with the book. See above for the order, which is load-bearing. |
| `KingdomCity.CheckOut(KingdomSystem, Zone, KingdomSurvey, long)` / `OnSuspending(KingdomSystem, Zone)` | The two readings. `OnSuspending` filters to zones the seated realm claims and takes its own survey. |
| `KingdomCity.RecordSupports(...)` / `RecordLarder(...)` | Where `KingdomSubsidence.RecordZone` and `KingdomCrops.RecordLarders` now write. |
| `KingdomCity.OtherZones(KingdomSystem, Zone)` / `LarderRoomElsewhere(KingdomSystem, Zone)` | Where `KingdomSubsidence.OtherZones` and `KingdomCrops.LarderRoomElsewhere` now read. `ZoneSighting` survives as the projection the rows hand the subsidence arithmetic, so that arithmetic did not change at all. |
| `KingdomCity.AuditLine(KingdomSystem, Zone, KingdomSurvey)` / `OwedThirds(KingdomSystem)` | The §3.9 audit as one greppable line — model total, ground total, and what stands between them — and everything the city still owes, in weighted thirds. |
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
| `KingdomResidents.JobIdProperty` (`KingdomJobId`) | The job a transient body renders. Nothing mints one until W3; the sweep is keyed on it. |
| `KingdomResidents.IdOf(GameObject)` / `EnsureId(KingdomSystem, GameObject)` | Read an id; mint one if the body has none. |
| `KingdomResidents.TryLocate(...)` / `TryEnsureRow(...)` | Which book holds a body's row; and the same, enrolling a settler the roster has not reached yet. |
| `KingdomResidents.Judge(KingdomSystem, int, KingdomBindingKind, string zoneId)` | Check-before-mint at the edge, answered by the table above. |
| `KingdomResidents.Bind(...)` / `Unbind(..., KingdomUnbindCause)` | Write or move a binding; evict one, naming why. |
| `KingdomResidents.SweepVerdict(KingdomSystem, GameObject)` | Whether an object in a thawed zone is a stale transient. **The verdict ships in W2; the despawn is W3.** |
| `KingdomResidents.AuditLine(KingdomSystem)` | Invariant I3 over the whole realm — no binding key ever resolves to two living bodies. Runs beside the §3.9 stock audit on every check-in. |
| `enum KingdomBindingKind` / `KingdomBindingVerdict` / `KingdomBodyPresence` / `KingdomUnbindCause` / `KingdomSweepVerdict` | The registry's vocabulary. |

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
| `static List<string> Styles` | Declared city styles. |
| `static void Reload()` | Re-read every registry. Called on game load; call it if you inject entries at runtime. |

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
catalogue's `Carries` over every `KingdomBuilt` work in the zone — every work scaled by
`KingdomWearRules.WorkEffectiveness` (Addendum 10(b): a crewed work by its crew stretch reduced
again by condition, a staffless one by condition alone), plus the `Shades` of whatever yard trade
each household has taken up — and `SupportedLevel(tally, stage, shade)` hands that to the frozen
`KingdomCatalogueRules.Equilibrium`.

`KingdomSubsidence.ScopedSupports(system, zone, survey)` is the same tally with **one** difference
and it is the one the level reads: `SupportTally.Lift` is scoped by reach (Addendum 6). Each
work's lift lands in proportion to the settlement's roofs it covers
(`KingdomReachRules.Landed`), the headed great works of the realm's other claimed zones arrive
whole out of `KingdomReach.CityShadeExcept`, and the binding three are untouched citywide pools.
`Supports` remains the right call for a caller asking what the works make rather than what the
settlement holds — the water-works production pass is one.

**A city, not a zone.** `KingdomSubsidence.Reckon` writes down what THIS zone is holding —
`RecordZone`, into the seated settlement's city book, dated in whole days (`SeenStamp`) — and then folds
in every OTHER zone the seated city claims **as it was last seen** (`OtherZones`), never simulated
forward: a granary zone the founder hasn't walked into since spring goes on reporting spring's
granary until they walk back in. `CityTally` sums the three BINDING goods this way (`Lift` passes
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
| `static int Equilibrium(int water, int food, int roof, int lift, int shade)` (on `KingdomCatalogueRules`) | The frozen arithmetic. The level is the least of the three binding goods, lifted by `lift + shade` up to `LiftCapPercent` of that least, floored at `FloorLevel`. Each of `lift` and `shade` is floored at zero on its own, so neither can eat the other and an unmet taste is never a penalty. |
| `static SupportTally FoldShade(SupportTally, List<KindAmount>, int percent)` (on `KingdomCatalogueRules`) | `FoldWork` without the work count, for a contribution that stands in somebody else's plot — a household's yard trade. |
| `static int LevelFromWater(int water, GrowthStage stage)` | Declared `water` is denominated at **camp rates**; this divides by `StageUpkeepPercent` before the equilibrium sees it. A design carrying eight in a camp carries three in a city. Since Addendum 11(a) only *producers* declare `water` — a cistern, a reservoir and the waterworks hold and carry nothing, because the same figure is also banked as a real daily flow (`LastWaterWorkTick`, above) and a vessel declaring it would be conjuring what it claims to store. A producer's figure is `KingdomRules.TicksPerDay / mean(VariableRate)` of the vanilla `LiquidProducer` on its own blueprint. |
| `static int SupportedLevel(SupportTally, GrowthStage, int shade = 0)` / `BindingSupportFor(...)` | The level, and which of `water` / `food` / `roof` is holding it down. `shade` is `KingdomSystem.NotableShade`; it defaults to none because a settlement that has named nobody honestly has none. |
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

## `KingdomRules` — pure rules (no engine dependencies)

Deterministic, side-effect-free, and fully unit-tested; safe to call from anywhere,
including your own tests. Notable members: `SpilloverDelta`, `UpkeepForElapsed`,
`ElapsedDays`, `AdvanceCheckpoint`, `ActivityDays`, `LabouredTicks`,
`StageFor`, `FetchableDrams`, `ResolveThirst`, `RationsPerDay`, `RationsForElapsed`,
`ForagedRations`, `ResolveHunger`, `ComposeScarcity`, `RaidSize`, `StyleAllows`, `DistrictName`,
`ZonesAdjacent`, `ComposeOutsider`, `ToThirdPerson`, plus the `BuildEntry` / `DealEntry`
records and their `TryParse*` validators.

District effects are `District*(string district)` for one district and `Districts*(IEnumerable<string> districts)`
for a whole kingdom's claimed ground: `DefenceBonus`, `UpkeepPercent`, `ShopTierBonus`,
`BuildPercent`, `PetitionIntervalPercent`, `DriftPercent`. City style is `Styles`,
`IsKnownStyle`, and `StyleForSite(terrainBlueprint, regionName, zLevel)`, which is total —
an unmapped site resolves to `common` rather than failing. `ProvokableFactions` lists every
faction `RaiderTableFor` answers for.

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

## Guarantees

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
