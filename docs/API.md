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
| `long LastWaterWorkTick` | **W6: the published mirror of the city model's `ProcessedThroughTick`, written by `KingdomCity.Stamp` and by nothing else.** It used to be the settlement pass's own checkpoint for water-works production; that arithmetic moved onto the model, per zone, off one clock, so that no day can be billed by two owners. What it still records is what it always said — the tick through which the settlement's works have been paid — and it is still what makes a catalogue `Carries="water:N"` a **flow** as well as a level. |
| `long LastFoodWorkTick` | **W6: this one is now the MILLS' stamp, and the model deliberately does not touch it.** The fields' clocked make moved onto the model with the water works'; the mills did not, because a mill makes nothing out of the day — it takes real crops off real shelves and puts real staples back, on the ground where the shelves are, and `KingdomCrops.MilledFoodPerDay` is subtracted out of the model's rate precisely so the two can never both be paid. Advanced by the settlement pass with `KingdomRules.AdvanceCheckpoint`, as it always was. Writing it from the reckon would read *now* on every check-in and no mill would ever grind again. |
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

**Every zone's works produce, and the day is billed once (W6).** A zone row's `WaterCarry` and
`FoodCarry` are what its works make in a day, measured by the pass that read that ground —
`KingdomSubsidence.Supports(Survey).Water` and `KingdomGrowth.FoodMadePerDay(Survey)`, the same two
figures the level and the whole catalogue ladder are derived from. The model integrates them for
every zone off its **one** `ProcessedThroughTick`, in world-day boundaries so that splitting a span
at a breakpoint pays exactly the days running it whole would; the settlement pass credits nothing,
and `KingdomCity.Stamp` writes `LastWaterWorkTick`/`LastFoodWorkTick` **from** the model's tick, so
two owners of one day is not a bug to avoid but a state that cannot be reached.

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
| `KingdomCityBook` | The serialized carrier: named-field `IComposite`, flat primitive columns, one row family per group of columns. `Normalize()` repairs a book read from a save — a null column becomes empty, **ragged columns are truncated to the shortest** (a row half of whose fields are missing is not a row), rows past their cap are dropped, and an overlong told-log keeps its newest lines. |
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
| `KingdomResidents.JobIdProperty` (`KingdomJobId`) | The job a transient body renders. Nothing mints one until W3; the sweep is keyed on it. |
| `KingdomResidents.IdOf(GameObject)` / `EnsureId(KingdomSystem, GameObject)` | Read an id; mint one if the body has none. |
| `KingdomResidents.TryLocate(...)` / `TryEnsureRow(...)` | Which book holds a body's row; and the same, enrolling a settler the roster has not reached yet. |
| `KingdomResidents.Judge(KingdomSystem, int, KingdomBindingKind, string zoneId)` | Check-before-mint at the edge, answered by the table above. |
| `KingdomResidents.Bind(...)` / `Unbind(..., KingdomUnbindCause)` | Write or move a binding; evict one, naming why. |
| `KingdomResidents.SweepVerdict(KingdomSystem, GameObject)` | Whether an object in a thawed zone is a stale transient. **The verdict ships in W2; the despawn is W3.** |
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

| Member | Contract |
|---|---|
| `KingdomCity.SpendTurn(KingdomSystem, Zone, long)` | One turn's amortised spend against one zone's standing debt. Returns whether the turn's allowance is now exhausted. Surveys only when something is actually owed, so a caught-up zone costs nothing. |
| `KingdomCatchUpRules.TryPlanTurn(demand, out spend, out fault)` | The whole turn's budget: 8 weighted units, at most 4 heavy, visible cells before the rest. |
| `KingdomCatchUpRules.TryPlanTurn(demand, thirdsAvailable, heavyAvailable, …)` | The same plan against an allowance already partly spent. An allowance larger than the constitution's own is refused, never granted. |
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
(`r_TAF_OptionPrefetch`) gates it; with no line for it in `Options.xml` the gate reads
`PrefetchDefault` = **No**. The mechanism ships complete: at most one neighbour held, two
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
weighted spend; `owed` rides in the label so a tester can watch it fall monotonically to zero. A
figure that crosses a §0.0 budget is prefixed `BUDGET` and names the budget it broke.

**What W3 deliberately does not ship.** Job minting for anything but the delivery flow, nearest-holder
sourcing and capacity-bound batching are W6's by §7.4, *"because both only bite once many jobs
compete over many holders"*. And the model's per-zone production **rates** stay unwired: growth
already charges upkeep at the pass, so a rate the slice also integrated would bill the same day
twice and break I1. The heartbeat ships as the mechanism, on the cadence, through the executor,
with its receipt and its one told line an hour.

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

## `ThousandAndFirst.Api` — the published extension contract (API version 1)

The behaviour lane. Opened at W5, after four waves shaped it, and dogfooded from the first commit:
the city's own asks go through `IKingdomAskSource` exactly as a third party's do. Worked examples,
the registration recipe, and the invariants are in [MODDING.md](../MODDING.md) — this section is
the surface list.

Option gate: `r_TAF_OptionExtensions`, default `Yes`. Off disables third-party C# only; the XML
data lane is unaffected.

| Member | Contract |
|---|---|
| `KingdomApiRules.Version` | The published version. `1`. Checked at registration; drift is refused by mod name. |
| `KingdomApiRules.MinSupportedVersion` | The oldest version still admitted. `1`. Moving it is a breaking change, and it is what makes STANDARDS §9's one-minor-cycle promise keepable. |
| `[KingdomExtension]` | Marker attribute. The class needs a public parameterless constructor. |
| `IKingdomExtension.ApiVersion` | What the extension was built against. Return the constant, never a literal. |
| `IKingdomAskSource.Ask(city, draws)` | Returns asks for the Charter's asks board. Null for none; at most `KingdomApiRules.MaxAsksPerSource` kept. |
| `IKingdomHappeningSource.Happen(city, sinceTick, draws)` | Returns dated notices for the chronicle and the word surface. At most `KingdomApiRules.MaxNoticesPerSource` kept. |
| `IKingdomDraws.TryBetween(lane, ordinal, low, high, out value)` | The kernel, keyed on `taf:ext:<mod>:<lane>`. Deterministic across reloads. Returns false rather than substituting a different stream. |
| `KingdomCityReading` | Frozen projection of one city's book: stocks, and `TryZone` / `TryWork` / `TryResident` over copied rows. No setters, no route to the ground. |
| `KingdomZoneReading` / `KingdomWorkReading` / `KingdomResidentReading` / `KingdomStockReading` | The row projections. |
| `KingdomWorkClass` / `KingdomDayPlace` / `KingdomRollStanding` | Published vocabularies, MAPPED from the model's own rather than cast, so a model-side insertion cannot renumber them. |
| `KingdomAsk` / `KingdomAskWeight` | One thing the city wants: kind, title, what would settle it, where, how badly. Kind/title/want are stripped and clamped; a `ZoneId` the city does not hold is read as none; an undefined weight is read as `Passing`, never `Grave`. |
| `KingdomNotice` | One dated thing that happened: kind, tick, chronicle telling, optional spoken line. **No place field** — neither surface a notice reaches takes a zone, so one would be a published input that went nowhere. |
| `KingdomExtensionVerdict` / `KingdomApiRules.Judge` / `.RefusalLine` / `.TryStream` / `.Slug` / `.Trim` / `.Kind` | The registration judgment and the clamps, pure and testable. |
| `KingdomExtensions.Version` / `.Enabled` / `.Admitted()` / `.Refusals()` | The registry, from outside. |

**Deliberately not published**, and why: resource kinds (the stock row is a fixed three-pair
struct), job and carrier kinds (a job's cargo is one of those three and its kind is a closed enum),
network kinds (the graph is not built), and work behaviours (the run-state slot is one 16-byte
discriminated field, and publishing it would freeze that shape). A contract nothing honours is
worse than none. They open when the substrate does.

**Isolation.** Every extension call crosses `KingdomExecutor.Submit`: frozen reading in, frozen
result out, timed against the reckon lane. A source that throws or overruns its lane's budget
stalls its own job — no city state is published, the turn is unaffected, the failure is logged by
mod name and named on the asks board, and every other extension still runs. **The budget is a
verdict, not a timeout**: the seam is synchronous, so it can refuse to publish a result that
overran but cannot interrupt one — an infinite loop in a third-party source still hangs the game,
exactly as one in ours would. Discovery uses the engine's cached attribute scan
(`ModManager.GetTypesWithAttribute`); construction is per-type and guarded, because the engine's
combined `GetInstancesWithAttribute` would let one class with no default constructor take down
every mod's extension at once.

## Reading surfaces — the Charter's three W5 entries

All three are **readings**. Nothing on any of them can be pressed; the verbs that answer them are
the Charter's own.

| Entry | Hotkey | Member |
|---|---|---|
| The book of the city | `7` | `ThousandAndFirst.Simulation.City.KingdomBookReport.Open(system)` — six chapters: the stores and what holds them, the works and what they wait on, the people and where their day puts them, the turn of the year, what has happened here, and who else writes in this book. |
| Where the keepers' craft could go | `8` | `ThousandAndFirst.KingdomTechMap.Draw(system)` — what each thing the keepers know opened, the nearest locked designs and what is in the way of each, and the ways of learning this city has never walked. A map, never a spend: gated on `r_TAF_OptionZoning`. |
| What the city is asking for | `9` | `ThousandAndFirst.KingdomAsks.Board(system)` — the standing petition, the city's own model-derived asks, and every extension's, worst first. Gated on `r_TAF_OptionPetitions`. |

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
