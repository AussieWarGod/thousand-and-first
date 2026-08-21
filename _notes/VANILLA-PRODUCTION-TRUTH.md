# Vanilla production truth — what actually exists to hook into

Date: 2026-08-21
Lane: engine-source + game-data survey. **Research only — no code, no branch, no tracked edit.**
Requested by: Addendum 11 of `_notes/BUILDING-CATALOGUE-BRIEF.md` (grounded production).

## Provenance and evidence standard

Ground truth is the archived decompile of build **2.0.211.51** at
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` (abbreviated **`D/`** below;
parts in `D/XRL/World/Parts/`), and the shipped game data at
`/home/r/coq/qud_helper/game_base/Base/` (abbreviated **`B/`**; blueprints in
`B/ObjectBlueprints/*.xml`, tables in `B/PopulationTables.xml`).

Same rigour as `_notes/CODEX-ENGINE-TRUTH-BATCH-1.md`: every claim cites `file:line` or an exact
blueprint name and file. Anything not read directly is marked **INFERRED** or **UNVERIFIED**. An
unsourced engine claim is a lead, not a fact.

Method note from Addendum 11 honoured: **derive before authoring**. This is the survey that must
precede any new production design.

---

## 0. The verdicts, up front

| Question | Verdict | Evidence |
|---|---|---|
| **Does it rain in Qud?** | **NO.** No rain, no precipitation, no storm, no sky-water. `grep -rniE 'precipitat'` over the whole decompile returns **0 hits**; no `Rain*`/`Storm*`/`Cloud*`/`WeatherSystem` class exists. "Glass storm" survives only as a flavour string (`D/XRL/World/Parts/GenerateFriendOrFoe.cs:81`). | filesystem + symbol sweep of `D/` |
| **Is there zone weather at all?** | **Wind only.** `Zone.CheckWeather` (`D/XRL/World/Zone.cs:7803`) is `if (HasWeather && TurnNumber > NextWindChange) WindChange(…)`. `WindChange` (`:7815-7883`) rolls `CurrentWindSpeed`/`CurrentWindDirection` and prints a line only if the player has Survival. **It creates no object and touches no `LiquidVolume`.** `HasWeather="true"` is set on exactly **3** vanilla zone defs, all skyboxes (`B/Worlds.xml:76,1573,2358`). | `D/XRL/World/Zone.cs:589,602,638,7803-7883` |
| **Can water be produced by a vanilla part?** | **Yes — `LiquidProducer`, already shipped as furniture.** `Air Well` (Tier 1, primitive, no power), `Solar Still` (Tier 3), `Solar Condenser` (Tier 4). Dew condensation, never rain. | `B/…/Furniture.xml:2485,2517,2544`; `D/…/LiquidProducer.cs` |
| **How many plantable seeds does vanilla have?** | **ZERO.** 28 blueprints carry seed/spore/tuber/pod semantics; **none is plantable**, none grows into anything. No `Plantable`/`Seed`/`Sow`/`Germinate` class, no `AddAction("Plant"…)` anywhere. `cutting` and `sapling` blueprints: **0**. | sweep of `B/ObjectBlueprints/*.xml` and `D/XRL/World/Parts/` |
| **Does vanilla grow anything over time?** | **NO.** `PlantProperties` is rootedness + effect immunity (`D/…/PlantProperties.cs:8,69-131`); `RipePlant` is a kill counter (`D/…/RipePlant.cs:17-21`). `Harvestable` has `bool Ripe` — two states — and its regrow timer is set on **zero** shipped blueprints. | `D/…/Harvestable.cs:41,45,47,363-381`; `grep RegenTime B/**/*.xml` → 0 hits |
| **Does vanilla food spoil?** | **NO.** No spoil/rot/decay/rancid mechanic. `PreservableItem` (`D/…/PreservableItem.cs:8-10`) is a two-field cooking marker (`Result`, `Number`), not a timer. "Fresh" is a naming convention for the un-preserved state. | exhaustive grep of `D/XRL/` and `B/` |
| **Does standing water evaporate or foul?** | **Only shallow, open, *mixed* puddles.** `CanEvaporate()` (`D/…/LiquidVolume.cs:6456-6471`) is false when the volume is closed, at wading depth (≥200 drams), **or a single pure liquid**. Liquid in a container never evaporates. | `D/…/LiquidVolume.cs:4165-4186,6456-6471` |
| **Do pools refill?** | **No.** No refill on a plain `LiquidVolume`. The only indefinite sources are `LiquidProducer` carriers and the `LiquidFont` mutation. (Confirms `_notes/IDEA-INBOX.md` ruling #8 against current source.) | `D/…/LiquidProducer.cs`; `D/…/Mutation/LiquidFont.cs` |
| **Is there a real power grid?** | **Yes.** Cardinal-adjacency flood fill, producer/consumer/conduit roles, capacity = weakest link. | `D/…/IPowerTransmission.cs:1089-1211`, capacity `:1172-1175` |
| **Are pipes and pumps real?** | **Pipes yes, liquid transport no.** `HydraulicPowerTransmission` pipes carry **joules**, not supply; their only liquid motion is `MingleLiquids` → `MingleAdjacent`, which equalises with *directly adjacent* volumes and routes nothing (`D/…/IPowerTransmission.cs:1619-1630`; `D/…/LiquidVolume.cs:5973`). `LiquidPump` exists as a class but **all three carrier blueprints are commented out** of `Furniture.xml`. | `D/…/LiquidPump.cs`; `B/…/Furniture.xml:1373-1402,2127,2210` |
| **Is there a "favourite dish" in vanilla?** | **Yes, at faction level** — `<waterritual Recipe=… RecipeText=… RecipeGenotype=…/>`, eight of them. This is the exact vocabulary Addendum 11(b) asks for. | `B/Factions.xml:154,187,219,1087,1117,1179,1777,1814`; `D/XRL/World/Faction.cs:72-76` |

### The single most important caveat for this mod

**Every vanilla producer is presence-gated.** `LiquidProducer`, `Harvestable`, `Mill`,
`ItemConvertor`, `FoodProcessor`, `LiquidFont` and `SolarArray` all run off `TurnTick` /
`EndTurnEvent`, and `ActionManager.ProcessSingleTurn` only ticks zones where
`!zone.Suspended && !zone.Stale` (`D/XRL/Core/ActionManager.cs:443-447`), dropping any live object
whose `CurrentZone.Suspended` (`ShouldRemove`, `:430-438`). **A vanilla producer standing in an
unvisited settlement produces nothing.**

The engine's own answer is `Temporary`, which handles `ZoneActivatedEvent` **and**
`ZoneThawedEvent` and reconciles `The.Game.Turns - LastTurn` on each
(`D/XRL/World/Parts/Temporary.cs:137-157`) — the exact tick-stamp catch-up idiom
`Growth/KingdomPlot.cs` and `Growth/KingdomScaffold.cs` already use. So the settled architecture is
confirmed vanilla-conformant, and the design rule falls out of the engine:

> **Vanilla parts are the visible face of production. The settlement's own tick-stamped pass is the
> accounting.** Hanging a `LiquidProducer` on a building makes it *visibly* work when the founder is
> standing there; it must never be the number the water economy reads.

---

## 1. WATER — vanilla hooks

### 1.1 Producers

| Blueprint / part | What it does | Knobs (defaults) | Lore register | Verdict |
|---|---|---|---|---|
| **`LiquidProducer`** (part) `D/…/LiquidProducer.cs` | `IPoweredPart`; every `Rate` turns adds a dram of `Liquid` to itself and/or nearby collectors. Optional `ConsumesLiquid` makes it a converter (`:136`). Distribution order: self → same-cell collectors → same-cell open volumes → **create a new `"Water"` object in the cell** → adjacent cells (`DistributeLiquid`, `:185-351`). | `Rate=1000` `:11`, `VariableRate` (dice, re-rolled per cycle `:140-143`) `:19`, `Liquid="water"` `:17`, `ChanceSkipSelf` `:13`, `ChanceSkipSameCell` `:15`, `PreferCollectors` `:21`, `PureOnFloor` `:23`, `FillSelfOnly` `:25`; ctor sets `ChargeUse=0`, `IsEMPSensitive=false`, `WorksOnSelf=true` (`:32-34`); plus `IActivePart`'s `ConsumesLiquid`/`LiquidConsumptionAmount`/`LiquidMustBePure`/`RequiresBodyPartCategory`/`WorksOnWearer`/`WorksOnEquipper` | depends on carrier | **USE-AS-IS.** Already carried by `r_KingdomSaltPan` and `r_KingdomCatchment` (`ObjectBlueprints.xml:237-241,257-260`). |
| **`Air Well`** `B/…/Furniture.xml:2485-2506` | Carved stone dome; dew condenses into a catch basin. `LiquidProducer Liquid="water" VariableRate="300-3000" PreferCollectors="true" IsTechScannable="false"` (`:2489`), `DeployWith Blueprint="Catchbasin" PreferredDirection="S"` (`:2490`), `<stag Name="Water"/>` (`:2503`). AV 30, HP 3000, `Class=infrastructure`, **Tier 1, no power at all**. | as above | **primitive** | **USE-AS-IS.** The single best water hook in the game: Tier-1, non-tech, lore-canonical, `CanBuild` not even relevant. `Underground Air Well` (`:2507`) is the sealed variant. |
| **`Catchbasin`** `B/…/Furniture.xml:2472-2484` | Stoneware; `LiquidVolume StartVolume="0" MaxVolume="64" Collector="true"` (`:2476`) + `LeakWhenBroken`. `Collector` is what makes an Air Well's output land in it. | `MaxVolume`, `Collector` | primitive | **USE-AS-IS.** Belongs in the water furnishing table. |
| **`Village Monument Amphora`** `B/…/Furniture.xml:5623-5629` | `Inherits="Village Monument"`; `LiquidVolume MaxVolume="256" Volume="256" StartVolume="0-32" InitialLiquid="water-1000" Collector="true"` + `LeakWhenBroken`. Description: *"The bellies of vessels are sacred, for they both carry the waters of life…"* | — | **primitive / civic** | **USE-AS-IS — the best single find for our furnishing tables.** A vanilla *civic* water vessel that is also a collector: exactly what a cistern court or a plaza should be furnished with. |
| **`Solar Still`** `B/…/Furniture.xml:2517-2543` | Brackish in, fresh out. `SolarArray ChargeRate="1"` + `Circuitry` + `LiquidVolume MaxVolume="64" InitialLiquid="water-600,salt-400"` + `LiquidProducer Liquid="water" ConsumesLiquid="water" VariableRate="20-40" ChargeUse="1" ChanceSkipSelf="100" ChanceSkipSameCell="100" PreferCollectors="true"`. **Tier 3**, `TinkerItem Bits="002" CanDisassemble="false" CanBuild="false"`. | — | **mechanical** — a found contraption | **INSPIRE-ONLY as a commission; USE-AS-IS as loot.** `CanBuild="false"` is vanilla saying *you do not make one of these*. ⚠ It declares `ConsumesLiquid="water"` with `LiquidMustBePure` left at its `true` default while its own tank is `water-600,salt-400` — whether it stalls depends on `UseDramsEvent` pass ordering, **UNVERIFIED at runtime**. Do not copy verbatim. |
| **`Solar Condenser`** `B/…/Furniture.xml:2544-2571` | `SolarArray ChargeRate="10"` + `Capacitor MaxCharge="2000"` + `TemperatureAdjuster` (cooling `-10`) + `LiquidProducer Liquid="water" VariableRate="100-400" ChargeUse="5" IsEMPSensitive="true"`. **Tier 4**, `CanBuild="false"`. | — | **high-tech** | **INSPIRE-ONLY.** Already removed from the catalogue on exactly this reasoning (`KingdomBuildings.xml:66-71`) — the survey ratifies that call. |
| **`LiquidFont`** (mutation) `D/…/Mutation/LiquidFont.cs` | Turn-ticked; on cooldown expiry fills every open `LiquidVolume` within `MaxRadius` (`:104,117`). The mechanism behind every "weep". | `Amount="500"` `:15`, `MaxAmount=800` `:17`, `MaxRadius=3` `:19`, `Cooldown="1d20"` `:21`, `Chance=100` `:11`, `Prefill=true` `:9` | **primitive / natural** | **WRAP.** It is a *mutation*, so its host must be mutation-bearing — in vanilla always a creature. |
| **`waterLichen`** — "giant water weep" `B/…/Creatures.xml:9935-9939` | `LiquidLichen` (`:9816`, `Inherits="BaseFungus"`) with `<mutation Name="LiquidFont" Liquid="water"/>`. Immobile, non-hostile, HP 300, Level 25, `SecretObject` category "Natural Features". `waterLichen Minor` (`:9940`): `Amount="5-8" MaxAmount="8" MaxRadius="2" Cooldown="3000-4000" Chance="50"`. | inherited | **primitive / natural** | **USE-AS-IS as a siting prize.** It is a creature: killable in a raid, losable. Spawns from `LiquidWeeps` (`B/PopulationTables.xml:5261`) / `MinorLiquidWeeps` (`:5280`). |
| **`saltLichen`** `B/…/Creatures.xml:9965-9969` | Same, `Liquid="salt"`. | — | primitive / natural | **USE-AS-IS.** The renewable feedstock that makes a salt-pan sustainable forever and gives the molten-salt store an honest input. |
| `Recycling Suit` `B/Items.xml:4413` | `LiquidVolume MaxVolume="8" InitialLiquid="water-1000"` + `LiquidProducer Liquid="water" Rate="1000" WorksOnWearer="true" WorksOnSelf="false"` (`:4419`). | — | high-tech (worn) | **INSPIRE-ONLY** — but it is the precedent for a producer that fills a *person*, not a place. |
| `Portable Beehive` `B/Items.xml:4728` | `LiquidProducer Liquid="honey" Rate="2000" WorksOnWearer="true"` (`:4735`). | — | primitive | Precedent for a **non-water** producer at a primitive register. |
| `Candelabra` / `Half Candelabra` `B/…/Furniture.xml:4136-4152` | `LiquidProducer Liquid="wax" VariableRate="50-100" ChanceSkipSameCell="50" PureOnFloor="true"`. | — | primitive | Precedent for furniture that quietly produces something other than water. |

**Plants do not yield liquid.** Watervine's `Harvestable` yields `Vinewafer`, a *food item* whose
`Food` part carries `Thirst="500"` (`B/…/Foods.xml:422`) — it quenches thirst through the eating
system and **never touches a `LiquidVolume`**. The `LiquidNative="water"` tag on Watervine
(`ZoneTerrain.xml:310` on Brinestalk, likewise elsewhere) is purely cosmetic: its only consumers are
smear rendering (`D/…/LiquidVolume.cs:4251`) and `D/XRL/World/Effects/LiquidCovered.cs:477-482`.

The one plant-borne liquid producer worth copying is a *weapon*: `Seed Slingshot`
(`B/…/Creatures.xml:2335`) and `Thistle Pitcher` (`:3287`) carry
`LiquidProducer Liquid="sap" FillSelfOnly="true" WorksOnEquipper="true" RequiresBodyPartCategory="Plant"`
— natural weapons that regenerate their own ammo.

**No creature in vanilla produces water.** All creature liquid output is ooze/goo/sludge/slime/
acid/lava/convalessence/blood/sap, via `LiquidBurst` (`D/…/LiquidBurst.cs:8-45`, on death, 80% per
adjacent cell), `LeavesTrail` (`D/…/LeavesTrail.cs:6-45`), or `SpawnWithLiquid`
(`D/…/SpawnWithLiquid.cs:7-84`, on `ZoneBuiltEvent`/`EnteredCellEvent`, then removes itself `:78`).

### 1.2 Pools, terrain, and ground liquid

Pools are ordinary `GameObject`s with `LiquidVolume MaxVolume="-1"` — `IsOpenVolume()` is literally
`MaxVolume == -1` (`D/…/LiquidVolume.cs:4542-4545`), the same test
`Growth/KingdomSurvey.cs:157-164` already uses. There is **no `LiquidPool` part, no `Geyser`, no
`Spring`.**

The base is `Water` (`B/ObjectBlueprints/PhysicalPhenomena.xml:57-68`), `<tag Name="Pool"/>`.
Fresh-water members: `FreshWaterPuddle` (10 drams, `:98`), `FreshWaterPool` (10d10, `:123`),
`FreshWaterPool300` / `500` (`:126,:129`), `DeepFreshWaterPool` (2000, `:132`). Salty and brackish
dominate: `SaltyWaterPuddle` (500, `water-600,salt-400`, `:74`), `SaltyWaterDeepPool` (4000, `:77`),
`BrackishWaterPuddle` (500, `water-900,salt-100`, `:95`).

**`Zone.GroundLiquid` defaults to `"salt-1000"`** (`D/XRL/World/Zone.cs:212`). Only three vanilla
zone defs override it, all to empty (the sky zones, `B/Worlds.xml:76,1573,2358`). This matters
twice: `LiquidVolume.CheckGroundLiquidMerge()` (`:6473-6506`) **obliterates** an open, shallow
(<200 dram) pool whose liquid is pure and equal to the cell's ground liquid — so spilled pure salt
soaks away; and `LiquidProducer`'s floor overflow mixes with the ground liquid unless
`PureOnFloor="true"` (`:250-257`).

Zone builders that place water: `LiquidPools` (`D/…/ZoneBuilders/LiquidPools.cs:12-86`, `Density`,
`PuddleObject`, `PlantReplacements`), `Watervine` (`Watervine.cs:42-187`, `SaltyWaterPuddle` where
noise ≥0.7 plus `Watervine` at 80% where noise ≥0.6), `Waterlogged`, `RiverBuilder`
(`RiverBuilder.cs:11,23-54`, default `SaltyWaterDeepPool`, region-switched), `Waterway`,
`OverlandWater`, `BathPools`, `LakeOfTheDamned`.

Vanilla's own idiom for an air-well room is worth copying verbatim: air wells along a wall,
`FreshWaterPuddle` beside them, `Flowers` around, `GrassPaint` underfoot, an occasional
`BrackishWaterPuddle` (`B/PopulationTables.xml:8109-8117`).

### 1.3 Storage, transfer, and containers

| Hook | What it does | Knobs | Verdict |
|---|---|---|---|
| **`LiquidVolume`** `D/…/LiquidVolume.cs` (6765 lines, `Priority => 90000` `:239`) | Everything liquid. `ComponentLiquids` is a `Dictionary<string,int>` of **parts per 1000** (`:89`). | `MaxVolume=-1` `:65`, `Volume` `:67`, `StartVolume=""` `:69`, `InitialLiquid` (write-only setter, `:301-338`), `AutoCollectLiquidType` `:75`, `NamePreposition` `:71`, `Primary`/`Secondary` `:77-79`; flag bits `Flowing` `:148`, **`Collector`** `:161`, `Sealed` `:174`, `ManualSeal` `:187`, `LiquidVisibleWhenSealed` `:200`, `ShowSeal` `:213`, `HasDrain` `:226`. Thresholds `SWIM_THRESHOLD=2000` `:40`, `WADE_THRESHOLD=200` `:42`. | **USE-AS-IS.** Already the mod's storage substrate. |
| **`InitialLiquid` grammar** | `"water-1000"`; comma = simultaneous components; **semicolon = pick one at random** (`:304`); bare `"water"` ⇒ 1000. | — | **USE-AS-IS.** |
| **Auto-collection** | `Collector="true"` + optional `AutoCollectLiquidType`. Collection is a five-pass preference ladder (`:1885`): pass 1 exact `AutoCollectLiquidType`, 2 `WantsLiquidCollection`, 3 pure match, 4 empty, 5 anything. | — | **USE-AS-IS.** `Catchbasin` and `Village Monument Amphora` are the worked examples. |
| **Freshness** | `IsFreshWater()` = `IsPureLiquid("water")` (`:1357-1360`) — **any contamination at all makes water non-fresh**. Pure water is worth 100× mixed (`LiquidWater.GetValuePerDram()=0.01f`, `GetPureLiquidValueMultipler()=100f`). | — | **USE-AS-IS.** `Core/KingdomLiquids.cs:13-21` already keys on exactly this. |
| **Evaporation / drying** | `CanEvaporate()` false when not open, at wading depth, **or a single pure liquid** (`:6456-6471`). `UseDramsByEvaporativity` removes drams in proportion to each component's `Evaporativity`, so water (`Evaporativity=2`, `D/XRL/Liquids/LiquidWater.cs:29`) boils out of salty water preferentially while salt (`Evaporativity=0`) stays — **that is Qud's own salt-pan chemistry, in the engine.** | — | **USE-AS-IS as lore.** A cistern's water is safe forever; if we ever want fouling it must be ours. |
| **Pour / fill / drink** | `LiquidVolume` registers `Drink` (`:3036`), `Pour` (`:3037` → `:6162`), `Fill` (`:3025` → `PerformFill` `:6509`), `FillFrom`, `Drain`, `CollectLiquid`, `AutoCollectLiquid`, `Seal`/`Unseal`. | — | **USE-AS-IS.** Already the founder's manual channel. |
| **`LiquidPump`** `D/…/LiquidPump.cs` | Moves liquid `FromDirection` → `ToDirection`. **Creates nothing** — invariant-safe by construction. | `Rate=1000` `:11`, `Liquid` `:15`, `VariableRate` `:17`, `PerTick` `:19`, `FromDirection`/`ToDirection` `:21-23`, `Sticky*` `:29-31`; `ChargeUse` inherits `IPoweredPart`'s **1** | **WRAP, with a warning.** Every carrier (`Combustion` / `Thermoelectric` / `Biodynamic Turbine`) is commented out, so the part ships with **no live user** and its directional fields are untested by any shipped content. |
| **Hydraulic pipes** | `BaseHydraulicPipe` (`B/…/Furniture.xml:857-869`): `HydraulicPowerTransmission IsConduit="true"` + a **sealed 8-dram** `LiquidVolume`. `GlassHydraulicPipe` (`:871`) `ChargeRate="40000"`, `TileAppendWhenPowered="_flowing"`. `HydraulicPowerTransmission` sets `Substance="power"`, `Unit="joule"`, `DependsOnLiquid="water"`, `WrongLiquidFactor=0.2`. | see §4 | **USE-AS-IS as *visible plumbing*, never as supply.** They carry joules. |
| **`MergeConduit`** `D/…/MergeConduit.cs:8-14,47-61` | A conduit blueprint dropped on an existing object grafts `ModPiping`/`ModWired`/`ModGearbox` onto the occupant and obliterates itself. | `ModPart`, `DestroyIfPart`, `SkipIfPart` | **USE-AS-IS.** The clean way to "plumb" or "wire" an already-built work without a second object. |

**Container capacities worth knowing** (all `B/ObjectBlueprints/`): `Waterskin` / `Canteen` /
`WaterContainer` 64 (`Items.xml:11944,12012,11930`); `Phial` and `Magnetic Bottle` 1 (`:12022,12168`);
`Gourd` 16 (`:12143`); `StorageTank` 128 (`:12127`); `Bottle` 16 (`Furniture.xml:4274`); `Vase` /
`Pitcher` / `Jug` 64 (`:4222,4345,4363`); `Ewer` / `Long Jug` 32 (`:4354,4387`); **`Village Monument
Amphora` 256, `Collector="true"`** (`:5626`); `Regen Tank` 128, `Collector` + `HasDrain` (`:2167`);
`Bubblething` 256 sealed (`:4875`). `PopulationLiquidFiller` (`D/…/PopulationLiquidFiller.cs:5-58`,
knobs `Table`, `Volume`) is how vanilla randomises a vessel's contents.

### 1.4 What water does *not* have — hard blockers

1. **No rain to hook.** A mod wanting rain must invent it wholesale.
2. **A mod cannot define a new liquid from XML.** `LiquidVolume.Init()` enumerates
   `ModManager.GetTypesWithAttribute(typeof(IsLiquid))` and `Activator.CreateInstance`s a compiled
   `BaseLiquid` subclass (`D/…/LiquidVolume.cs:1112-1132`). Data-only mods get the ~28 shipped
   liquids. (We only need `water` and `salt`, so this is not binding — but it is worth recording.)
3. **No liquid transport network.** No routing, no pressure, no direction. Pipes equalise with
   adjacent volumes only.
4. **The floor-spill blueprint is hardcoded to `"Water"`** in every overflow path
   (`LiquidProducer.cs:247,328`; `LiquidPump.cs:694`; `LeaksFluid.cs:244,325`;
   `LeakWhenBroken.cs:100`; `LiquidVolume.cs:5851`).
5. **The global autoget liquid is hardcoded to `"water"`** (`LiquidVolume.cs:6041-6052`); only
   per-item `AutoCollectLiquidType` can differ.
6. **No aqueduct, no well** — and none wanted (standing ruling `fc7a710`).

---

## 2. FOOD — vanilla hooks

### 2.1 Growing

| Hook | What it does | Knobs | Register | Verdict |
|---|---|---|---|---|
| **`Harvestable`** `D/…/Harvestable.cs` | The only ripeness mechanic. `bool Ripe` (`:41`) — **two states, no more**. `StartRipeChance` rolled once at `AfterObjectCreatedEvent` (`:183-194`). Yields `OnSuccess` × `OnSuccessAmount`; a leading `@` makes `OnSuccess` a population table (`:281-283`). Swaps Tile/RenderString/Colour between the `Ripe*` and `Unripe*` sets (`:53-124`). | `DestroyOnHarvest` `:13`, `OnSuccess="Vinewafer"` `:15`, `OnSuccessAmount="1"` `:17`, `StartRipeChance="1:1"` `:43`, **`RegenTimer=int.MaxValue`** `:45`, **`RegenTime=""`** `:47`, `RipeTimerChance="1:1"` `:49`, `HarvestVerb` `:51`, ten `Ripe*`/`Unripe*` appearance strings `:19-37` | primitive | **WRAP.** Free, polished harvest UX; two states only. |
| **`Harvestable` regrowth** | `Ripen()` (`:363-381`) decrements `RegenTimer` once per `EndTurnEvent` (`:150-154`), and re-ripens on `RipeTimerChance`. `RegenTimer` is armed only from `RegenTime` (`:59-62`), and **`RegenTime` is set on zero shipped blueprints** — so `Ripen()` returns at `:365` forever and **every vanilla harvestable is one-shot.** | `RegenTime`, `RipeTimerChance` | — | **A mod may set `RegenTime` and light the dead loop.** It serialises correctly (plain fields on `[Serializable] IPart`) but is unexercised by shipped content, and its clock stops when the zone suspends. |
| **`AccelerateRipening`** | A named `Event` `Harvestable` registers (`:203`) and answers by calling `Ripen()` (`:209-213`). **No C# fires it** — it is fired from **data**, by `Hydraulic Irrigator`'s `RadiusEventSender Event="AccelerateRipening" Radius="10" ChargeUse="5"` (`B/…/Furniture.xml:1193`). Because no vanilla blueprint sets `RegenTime`, **the irrigator currently does nothing to any vanilla plant.** | `Event`, `Radius`, `RealRadius`, `ChargeUse` (`D/…/RadiusEventSender.cs:9-13`) | mechanical | **USE-AS-IS — the best food hook found.** An irrigation work that visibly ripens *our* crops, using vanilla's own event, on vanilla's own sender part, at vanilla's own radius. |
| **`PlantProperties`** `D/…/PlantProperties.cs` | Rootedness, `+300` linear / `+200%` kinetic resistance, immunity to Prone/Wading/Swimming/CardiacArrest (`:69-131`). Un-roots on `AnimateEvent` (`:41`) or `LeftCellEvent` (`:88`). Nothing to do with growth. | `Rooted=true` `:8` | primitive | **USE-AS-IS** on crop objects so they read as plants to the rest of the engine. |
| **`Temporary`** `D/…/Temporary.cs` | `Duration` ticks down; on expiry the object becomes `TurnInto`. Compensates for zone suspension on `ZoneActivatedEvent` and `ZoneThawedEvent` (`:137-157`). | `Duration`, `TurnInto`, `LastTurn` | — | **INSPIRE-ONLY as a crop cycle** (it stamps `WontSell` `:191` and `Flags |= 8` `:196`), **USE-AS-IS as the catch-up pattern.** |
| `Butcherable` `D/…/Butcherable.cs:11` | Corpse → meat. Requires `CookingAndGathering_Butchery` (`:42`) unless a machine skips it. | `OnSuccess`, `OnSuccessAmount` `:13-15` | primitive | **USE-AS-IS.** |
| `SpawningEggSac` `D/…/SpawningEggSac.cs:22-58` | The only vanilla "object hatches into other objects on a timer" (`Turns="10-20"`, `SpawnBlueprint`, `SpawnCount`). Carrier: `Svardym Egg Sac` (`B/…/Creatures.xml:8519`). | — | biological | **INSPIRE-ONLY** — closest structural analogue to a maturing crop. |

**Seeds inventory — the deliverable.** Sweeping `B/ObjectBlueprints/*.xml` for
seed/spore/cutting/sapling/bulb/tuber/pod/sprout semantics returns **28 blueprints**:

- **Literal seed items: 5** — `Arsplice Seed` (`Foods.xml:558`, Tier-7 `Food` + `HealOnEat`, **zero
  code references anywhere**), `Rubber Tree Seed` (`Foods.xml:664`, flavour `Food`), `Sowers_Seed`
  (`Creatures.xml:4672`, a thrown `HEGrenade`), `ProjectileSpatSeed` (`Creatures.xml:2351`, ammo),
  `Seed Slingshot` (`Creatures.xml:2335`, a rifle).
- **Tubers: 2** — `Dreadroot Tuber` (`Foods.xml:426`), `Lagroot Tuber` (`Foods.xml:530`). Harvest
  yields; food; not plantable.
- **Spores: 8** — all `Inherits="Gas"` with `GasFungalSpores`; they infect creature **body parts**,
  not ground (`D/XRL/World/Effects/FungalSporeInfection.cs:40,164`).
- **Fungal-infection limb items: 6**; **bulb/pod misnomers: 6** (`PolypCache`, `Feral Lah Pod`,
  `Seedsprout Worm`, `Sprouting Orb`, …); **`TerrainSeedVault`: 1** (a North Sheva world tile,
  cosmetic, `HiddenObjects.xml:377`).

> **Blueprints with a part that makes them plantable: 0. Blueprints that grow into another object:
> 0. "Cutting" or "sapling" blueprints: 0. There is no `AddAction("Plant"…)` anywhere in `D/`.**
> Specifically, **there is no watervine seed and no watervine cutting.**

### 2.2 Farming precedent

| Hook | What it is | Verdict |
|---|---|---|
| **`Watervine`** `B/…/ZoneTerrain.xml:278-292` | `Inherits="Plant"`; `Harvestable OnSuccess="Vinewafer" StartRipeChance="1:20"`; `<tag Name="LiquidNative" Value="water"/>`; `DynamicObjectsTable:FarmablePlants`. Tile `Assets_Content_Textures_Tiles_sw_watervine1.bmp` — the exact string `r_KingdomPlot` already reuses (`ObjectBlueprints.xml:151`). | **USE-AS-IS** as the crop object of a verdant/marsh plot. |
| **`DynamicObjectsTable:FarmablePlants`** | 26 blueprints tagged farmable across `ZoneTerrain`/`Creatures`/`Walls`/`Widgets` — Watervine, Yuckwheat, Starapple Tree, Witchwood Tree, Dreadroot, Urberry Bush, Noisegrass, Farm Bop Sponge, Lagroot, Banana Tree, Dogthorn Tree, Fracti, Glitchwood Tree, Icosahedar, Finger Coral, Tunnel Sponge, and the regional spawners. Rolled by `VillageBase.getAGenericFarmPlantNearTier()` (`D/…/VillageBase.cs:1389-1405`), which falls back to `"Watervine"` after ten tries (`:1398`). Regional variants: `DynamicObjectsTable:<region>_FarmablePlants` (`:1407-1438`). | **USE-AS-IS.** Vanilla's own answer to "what does this ground grow", already tier- and region-aware — a better source than the hand-list in `KingdomCropRules.CropBlueprintForStyle`. |
| **Farm variants** | `Starapple Farm Tree` (`ZoneTerrain.xml:189`, `Physics Owner="Farmers"`), `Farm Bop Sponge` (`:748`). Vanilla ships *farmed* variants of wild plants. | **USE-AS-IS.** Precedent for our own `r_Kingdom…` crop reskins. |
| **What a village farm physically is** | `Villages_GreenspaceContents_*` rolls `garden` / `farm` / `aquaculture` (`B/PopulationTables.xml:15019-15047`). `garden`/`farm` lays one crop species in alternating **columns** or **rows**, with liquid puddles in the gaps 33% of the time, or a scatter at 20–98% density (`D/…/Village.cs:1265-1339`). `aquaculture` runs a 3×-scale backtracker maze: crop in the corridors, liquid pools in the walls (`:1163-1218`). Animal pens are a walled rect with `Villages_FarmAnimals` inside and `Villages_FarmHutContents` in the hut (`:1210-1263`). Ownership is one int: `obj.SetIntProperty("VillageDomesticated", 1)` (`D/…/VillageBase.cs:279`). | **USE-AS-IS.** Copy the *look*: rows of one crop, channels of standing water between them, a fence, a gate. |
| **`StarappleFarm`** `D/…/ZoneBuilders/StarappleFarm.cs` | 1–3 boxes 40–60 × 8–16, `Grassy.PaintCell` interior (`:81-84`), `"Starapple Farm Tree"` at 45% per strided cell (`:87-101`), `BrinestalkFence` perimeter with one gap (`:141-146`), 1–3 `Brinestalk Gate` cells (`:130-139`), then a `VillageMaker` pass. | **USE-AS-IS** as the visual recipe for the grange and home farm. |
| **Joppa** | `B/Joppa.rpm` holds **153 hand-placed `Watervine`** and 7 `WatervineFarmerJoppa`. | The canonical Qud farm is 153 grown plants on the ground. Nothing more. |
| **Hydroponics** | **Does not exist.** `B/Hydropon.rpm` contains zero tanks, vats, or planters; `HydroponSurface.cs` / `HydroponTerrain.cs` are pure reveal/journal logic. The nearest object is `Mushroom Case` (`B/…/Furniture.xml:5121-5133`, `Harvestable OnSuccess="Plump Mushroom" OnSuccessAmount="1-2" StartRipeChance="1:1"`) — a decoration that yields once. | **INSPIRE-ONLY.** |

### 2.3 Cooking, meals, and the favoured dish

| Hook | What it does | Knobs | Register | Verdict |
|---|---|---|---|---|
| **`Campfire`** (part) `D/…/Campfire.cs` (1951 lines) | The entire cooking system. Commands `CookFromRecipe`, `CookWhipUp`, `CookChooseIngredients`, `CookPreserve`, `CookPreserveExotic`, `CookPresetMeal:`, plus four `Physic_Nostrums` treatments (`:36-54`). UI is an `InventoryAction` menu built from `GetCookingActionsEvent` (`D/XRL/World/GetCookingActionsEvent.cs:6`), populated `:227-259`, shown `:1281-1302`. Hard gates: `CheckFrozen` `:1248`, **`AreHostilesNearby()` blocks cooking outright** `:1252`, active-part status `:1257`. Hunger gate `IsHungry` `:607-618` allows up to three "free" meals while sated. | `ExtinguishBlueprint` `:27`, **`PresetMeals`** `:29` | primitive | **USE-AS-IS.** The catalogue's `fire` design already commissions `Blueprint="Campfire"` — it is *already* a cooking site, and nothing in the mod says so. |
| **`PresetMeals`** | Comma list, each entry resolved as `Activator.CreateInstance(ModManager.ResolveType("XRL.World.Skills.Cooking." + text))` (`:203-214`). **A mod may ship its own `CookingRecipe` subclass in that namespace and name it here.** | — | — | **USE-AS-IS.** |
| **`Oven`** `B/…/Furniture.xml:4504-4519` | `MountedFurniture` + `Campfire` + `ThermalInsulation=700` + `<tag Name="CampfireHeatSelfOnly"/>` + `QuestableVerb="cook at"` / `QuestableEvent="CookedAt"`. HP 1200. | — | primitive / civic | **USE-AS-IS.** |
| **Per-settlement ovens** | **Every named settlement in Qud has its own oven with its own signature meal**: `JoppaOven`→`AppleMatz`, `KyakukyaOven`→`MushroomCider`, `EzraOven`→`GoatAndSweetLeaf`, `YdFreeholdOven`→`TongueAndCheek`, `MopangoOven`→`BoneBabka`, `StiltOven`→`HotandSpiny`, `BeyLahOven`→`MahLahSoup`, `GritGateOven`→`ThePorridge`, `ChavvahOven` (`B/…/Furniture.xml:4520-4585`). | — | — | **USE-AS-IS — the precedent is exact.** A settlement's oven carrying its own recipe is not an invention; it is how Qud identifies a town. |
| **`Faction.WaterRitualRecipe`** ⭐ | **Vanilla's actual "favourite dish" concept, at faction level.** Declared `<waterritual Recipe="X" RecipeText="…" RecipeGenotype="…"/>` on a `<faction>` (`B/Factions.xml`), parsed `D/XRL/World/Factions.cs:782-795` into `Faction.WaterRitualRecipe` / `…Text` / `…Genotype` (`D/XRL/World/Faction.cs:72-76`). **All three fields are serialised on the `Faction` itself** (`Faction.cs:286-288` write, `:362` read), so a runtime faction carries them across save/load. Consumed by `D/XRL/World/Conversations/Parts/WaterRitualCookingRecipe.cs`, resolution order: creature `SharesRecipe` tag → `TeachesDish` part → faction recipe (`:33-57`), reputation-priced (`:83`), genotype-gated (`:99-119`), finishing with `JournalAPI.AddRecipeNote` (`:123`). The eight vanilla dishes are at `Factions.xml:154,187,219,1087,1117,1179,1777,1814` — the Barathrumite one is literally *"Would you teach me to cook the Barathrumites' favorite dish?"* (`:1179`). | `Recipe`, `RecipeText`, `RecipeGenotype` | — | **USE-AS-IS — the single best answer to Addendum 11(b)'s "favored meals".** We already create a runtime `new Faction()` (`Core/KingdomFounding.cs:36`); giving it a `WaterRitualRecipe` makes the settlement's signature dish teachable through the water ritual, in vanilla's own vocabulary, with vanilla's own prose frame. **UNVERIFIED at runtime** that a runtime-set recipe survives the whole conversation path. |
| **`TeachesDish`** `D/…/TeachesDish.cs:7-21` | Per-creature dish override (`Text`, `Recipe`). **No vanilla blueprint carries it** — it is a pure mod/quest hook. | `Text`, `Recipe` | — | **USE-AS-IS.** The per-*settler* version: a named cook who teaches the settlement's dish. |
| `SharesRecipe` / `SharesRecipeText` / `SharesRecipeWithTrueKin` tags | Per-creature override, read `WaterRitualCookingRecipe.cs:35-45`. Vanilla users: the Stilt pilgrims (`B/…/Creatures.xml:13192-13193`) and one more at `:14284-14286`. | — | — | **USE-AS-IS.** |
| `CookingRecipe` `D/XRL/World/Skills/Cooking/CookingRecipe.cs:23` | `[Serializable]`; fields `Hidden` `:25`, **`Favorite`** `:27` (player-side pin/sort only, no gameplay effect), `DisplayName` `:32`, `ChefName` `:34`, `Components` `:36`, `Effects` `:38`, `Tile` `:43`. Explicit `Write`/`Read` `:59-80`. Stored in `CookingGameState.knownRecipies` (`:18`); `KnowsRecipe` matches on **display-name string** (`:69`). | — | — | **WRAP.** Recipes are C# classes, not XML data. Eleven hand-written ones ship; the rest are procedural via `FromIngredients` (`:333-457`). |
| **How a meal applies** | `ProceduralCookingEffect` (`D/XRL/World/Effects/ProceduralCookingEffect.cs:11`), ctor `Duration = 1` (`:22-25`), and `Effect.UseStandardDurationCountdown()` defaults **false** (`D/XRL/World/Effect.cs:620-623`) — **so a player's meal effect is hunger-gated, not time-gated**: `Duration = 0` on `BecameHungry`/`BecameFamished`/`ApplyWellFed`/`ClearFoodEffects` (`:184-200`). **Non-player** eaters expire on a real timer, `StartTick + 1200` ticks (`:212-223`, `Stomach.CalculateCookingIncrement()` `D/…/Stomach.cs:73-85`). One meal effect at a time (`Campfire.cs:740,1005,1218`). | — | — | **USE-AS-IS.** The non-player 1200-tick figure is exactly one settlement day (`KingdomRules.TicksPerDay = 1200`, `Core/KingdomRules.cs:445`) — a shared meal that buffs settlers for a day is *vanilla's own number*. |
| **Ingredient domains** | `PreparedCookingIngredient` (`D/…/PreparedCookingIngredient.cs:10`): `type` (CSV of domains, or `random`/`randomHighTier`) `:12`, `descriptionPostfix` (auto-written once at `ObjectCreatedEvent` `:73-87`) `:14`, `charges` `:16`. Domains resolve to 66 `ProceduralCookingIngredient_*` blueprints inheriting `IngredientMapping` (`B/ObjectBlueprints/Data.xml:225-720`) carrying `Units` / `Triggers` / `Actions` / `Description` / `RandomWeight` / `CookingDomain`. 48 distinct `type=` values ship. Liquids add ~14 more through `BaseLiquid.GetPreparedCookingIngredient()` (`D/XRL/Liquids/BaseLiquid.cs:192-195`) — e.g. salt ⇒ `tastyMinor`, honey ⇒ `medicinalMinor`. | — | — | **USE-AS-IS.** `ICookingRecipeComponent.getIngredientId()` returns a stable key `"blueprint-X"` / `"liquid-X"` / `"prepared-X"` — **a ready-made recipe-signature for a favour table.** |
| **`Food`** `D/…/Food.cs:9` | `Thirst=0` `:13`, `Satiation="None"` `:15` (`"Snack"` ⇒ `CookingCounter -= 200`, `"Meal"` ⇒ `= 0`), `Gross=false` `:17`, `IllOnEat=false` `:19`, `Healing="0"` `:21`, `Message` `:23`. Eating costs 1000 energy and **destroys the object** (`:211,215`). 110 blueprint declarations. | — | — | **USE-AS-IS.** Already the mod's larder classifier. |
| **`PreservableItem`** `D/…/PreservableItem.cs:6-10` | Two fields: `Result`, `Number`. A zero-event pure-data marker; conversion happens in `Campfire.PerformPreserve` (`:512-566`), which obliterates the source and hands over `Number × Count` results. 67 declarations. | `Result`, `Number` | primitive | **USE-AS-IS** — the honest grounding for a granary that "refuses to waste what came in". |
| **`<stag Name="Food"/>`** | `stag` nodes become tags prefixed `Semantic` (`D/XRL/World/GameObjectFactory.cs:1086-1105`), so this yields tag `SemanticFood`. Set on the `Food` base object (`B/…/Foods.xml:27`, inherited by every food) and on three furniture pieces: `Millstone` (`Furniture.xml:1043`), `Hydraulic Irrigator` (`:1215`), `Food Processor` (`:1525`). | — | — | **USE-AS-IS.** The cleanest single "this is food-domain" marker in vanilla. |
| `Cookbook` `D/…/Cookbook.cs:18` | `Tier` `:20` (`-1` resolves from zone tier), `NumberOfIngredients="2-4"` `:22`, `Style` `:26`, `ChefName` `:28`, per-page learn tracking `:30-32`. Reading a page calls `CookingGameState.LearnRecipe` (`:118`). | — | primitive | **USE-AS-IS.** A scriptorium or keeper's shelf that holds the settlement's own cookbook is free. |

**Food taxonomy.** `B/ObjectBlueprints/Foods.xml` is 1298 lines, **157 blueprints**: four bases
(`Food` `Satiation="Meal"` `:17`, `Organ` `"Snack"` `:29`, `Snack` `:35`, `Preservable`
`"None"` + `AlwaysStack` `:41`), **70 raw foods** (butcherables `:70-405`, harvestables `:406-670`)
and **83 cooking ingredients** (`:673-1298`, including 11 jerkies, 17 breather pastes, 9 congealed
tonics, and 11 liquid ingredients tagged `LiquidCookingIngredient`). **Vanilla never creates a
cooked-meal *item*** — cooking consumes ingredients and applies an effect straight to the eater.

**Two static-cache hazards for anything that scans containers**, both worth avoiding in our survey
code: `PreparedCookingIngredient.GetTypeOptions()` and `GetRandomTypeList()` return the **shared
static `RandomTypeList`** (`:19,125,187-203`) — always use the `IList`-fill overloads; and
`Campfire.CookFromIngredients` mutates `Count` and calls `ResetNameCache()` on every ingredient
while merely *listing* them (`Campfire.cs:818-851`) — do not copy that pattern. Reading `Food`,
`PreparedCookingIngredient.type/charges`, and `PreservableItem.Result/Number` is **pure and safe**;
`KingdomSurvey`'s existing `HasPart("Food") || HasPart("PreparedCookingIngredient")` test mutates
nothing.

---

## 3. INDUSTRY — vanilla hooks

Only **four** parts in the entire game transform matter, plus cooking.

| Part / blueprint | What it does | Knobs | Register | Verdict |
|---|---|---|---|---|
| **`Mill`** `D/…/Mill.cs:9` | `IPoweredPart`, `WorksOnInventory=true` (`:47-51`). One item per `EndTurn` while powered (`:135-142`): look up `obj.Blueprint` in `Transformations`, else by tag in `TagTransformations` (`:144-166`); non-empty target ⇒ `obj.ReplaceWith(transform)` (`:114`); **empty target ⇒ try `Butcherable.AttemptButcher(SkipSkill:true)` then `Campfire.PerformPreserve(…, Single:true)`** (`:82-101`). | `Transformations="From:To,…"` `:21`, `TagTransformations` `:34`, `ChargeUse=1` `:49` | mechanical | **USE-AS-IS.** Arbitrary recipes from XML alone. |
| **`Millstone`** `B/…/Furniture.xml:1015-1043` | `MechanicalPowerTransmission ChargeRate="100" IsConsumer="true"` (`:1019`) + `Mill ChargeUse="1" Transformations="Vinewafer,Lagroot Tuber,Mirror Shard,Psychal Gland,Voider Gland,Clump of Grave Moss,Compacted Bone Matter" TagTransformations="BreatherGland"` (`:1020`) + `Container`+`Inventory` (`:1026-1027`) + `<stag Food/>` (`:1043`) + animated tile. **Tier 2, `Bits="BB"`.** Every listed target is blank, so it butchers/preserves: **Vinewafer → Vinewafer Sheaf ×3, Psychal Gland → Psychal Gland Paste ×5**, automatically, while mechanically powered. Its tile `Items/sw_millstone_1.bmp` is **already the tile `r_KingdomMill` uses** (`ObjectBlueprints.xml:586`). | — | mechanical | **USE-AS-IS.** This is a genuine, shipped, food-in-ingredient-out production chain driven by mechanical power — exactly the thing the catalogue asserts and does not have. |
| **`FoodProcessor`** `D/…/FoodProcessor.cs:7` | `IPoweredPart`, `ChargeUse=500`, `WorksOnInventory=true`. Every `EndTurn`: `Butcherable.AttemptButcher(SkipSkill:true, IntoInventory:true)` — **butchers without the skill** — else `Campfire.PerformPreserve` on the **whole stack** (`:22-45`). Carrier `Food Processor` (`B/…/Furniture.xml:1497-1525`, `Capacitor MaxCharge="1000"` + electrical **and** hydraulic consumers, Tier 4, `CanBuild="false"`). | `ChargeUse` | high-tech | **INSPIRE-ONLY as a commission; USE-AS-IS as a certified machine.** |
| **`ItemConvertor`** `D/…/ItemConvertor.cs:8` | Converts inventory items carrying a named property/tag into the blueprint that tag names; `"Blueprint:count"` rolls a count (`:100-108`). | `ConversionTag` `:12`, `Verb`/`Preposition` `:14-16`, `Chance=100` `:18`, `AllowRandomMods` `:20`, `GiganticFactor=1` `:22`, `UseChargeEveryTurn` `:24`, `ChargeUse=500` `:31` | mechanical | **USE-AS-IS.** Both sides definable in mod XML — the cleanest yard/refinery hook in the game. Templates: `Wire Extruder` (`Furniture.xml:1822`, `ConversionTag="WireExtruderOutput" Verb="strip"`), `Rock Tumbler` (`:1322`, `Chance="5" UseChargeEveryTurn="true"`). |
| `ReclamationCist` `D/…/ReclamationCist.cs` | Corpses in, `ProduceBlueprint="Food Cube"` out; `RequireGenotype="True Kin"` (`:11`), `ChargeUse=500` (`:15`). | — | high-tech / sinister | **INSPIRE-ONLY.** |
| `FabricateFromSelf` `D/…/FabricateFromSelf.cs:10` | Spends the host's own HP to make items. | `FabricateBlueprint` `:12`, `BatchSize="1d6"` `:14`, `HitpointsPer="2d4"` `:16`, `Cooldown="5d10"` `:18` | biological | **INSPIRE-ONLY.** |
| **Tinkering** | `TinkerItem` `Bits`, `CanDisassemble` (default **true** `:19`), `CanBuild` (default **false** `:21`), `BuildTier=1` `:23`, `NumberMade=1` `:25`, `Ingredient` `:27`, `SubstituteBlueprint` `:29`, `RepairCost` `:31`. A blueprint becomes a buildable recipe only with `TinkerItem` + `CanBuild="true"` + no `SubstituteBlueprint` + not `NoDataDisk`/`BaseObject` (`:201-219`). Bit **types** are hardcoded C# (`D/XRL/World/Tinkering/BitType.cs:413-432`); `BitLocker` is player-only, attached at runtime (`Disassembly.cs:574`). | — | mechanical | **USE-AS-IS** for recipes; `CanBuild` is vanilla's own commissionable/recoverable line. |

**Traps.** `Kiln` (`:1106`), `Lathe` (`:1350`), `Glass Furnace` (`:1794`), `Glass Printer`
(`:2078`) and `Powered Orrery` (`:1086`) carry only `ChargeSink` — 27 lines whose entire body is
"consume charge on TurnTick" (`D/…/ChargeSink.cs:23-26`). They burn power and produce nothing; their
only observable effect is driving `AnimatedMaterialGeneric` and the `Interesting` marker. `Hydraulic
Press` (`:1599`) is a crushing **trap**, not a press. `InventoryItemConvertor` is documented dead
(`D/…/InventoryItemConvertor.cs:7`).

**No `Fermenter`, `Still`, `Distill`, `Smelt`, `Forge`, `Loom`, `Press`, `Grind`, `Refine`,
`Recycl*`, `Compost`, `Fabricator`, `Replicator` or `Assembl*` part exists.** The whole
transformation surface is the table above.

---

## 4. POWER — vanilla hooks

**The grid is real.** `IPowerTransmission.FindGrid()` (`D/…/IPowerTransmission.cs:1099-1211`) is a
BFS over **cardinal directions only** (`:1190`) across objects whose transmission **type string
matches** (`TypeMatches` `:1089-1098`) — electrical and hydraulic grids are separate networks even
when overlapping. **`GridCapacity` is the minimum `GetEffectiveChargeRate()` across every member**
(`:1172-1175`): one weak link throttles the whole network.

| Medium | Default `ChargeRate` | EMP | Notes |
|---|---|---|---|
| `ElectricalPowerTransmission` `D/…:6,10` | 500 | sensitive | `SparkWhenBrokenAndPowered=true` `:18` |
| `MechanicalPowerTransmission` `D/…:6,11` | 100 | **immune** `:10` | what our crank mill / wheel / vane already produce |
| `HydraulicPowerTransmission` `D/…:6,11` | 2000 | **immune** `:10` | `DependsOnLiquid="water"` `:19`, `WrongLiquidFactor=0.2` `:20` |
| `BiomechanicalPowerTransmission` `D/…:6,11` | 200 | immune | — |
| **`GenericPowerTransmission`** `D/…:6,8` | 100 | sensitive | **`public string Type = "generic"`** — a wholly private named network from XML alone |

Shared knobs (`:14-92`): `ChargeRate`, `IsProducer` `:38` / `IsConsumer` `:40` / `IsConduit` `:42`,
`ChanceBreakConnectedOnDestroy=50` `:16`, `ChanceBreakOnMove=100` `:20`, `DependsOnLiquid` `:32`,
`DamageDegrades=true` `:44`, `DischargeLiquidWhenBroken*` `:48-50`, `MingleLiquidsWhen*` `:52-54`,
plus a full `TileAnimateWhenPowered` cosmetic block `:58-92`.

### Generators

| Part | Fails when | Knobs | Register | Live carriers |
|---|---|---|---|---|
| **`SolarArray`** `D/…/SolarArray.cs` | cell blacked out, zone not outside/world-map, or **not `IsDay()`** (`:60-81`); status `"RadiationFluxInsufficient"` `:87`. `PrimePowerSystemsEvent` pre-charge only fires if `HasPropertyOrTag("Furniture")` (`:35-38`). | `ChargeRate=10` `:8` | mechanical → high-tech | `Electric Generator` (`B/…/Furniture.xml:1131`, **is a solar panel despite its name and its handcrank description**), `Solar Power Station` `:1436`, `Solar Pumping Station` `:1463`, `Solar Still` `:2517`, `Solar Condenser` `:2544`, `Solar Cell` (`B/Items.xml:10488`) |
| **`WindTurbine`** `D/…/WindTurbine.cs` | `zone.CurrentWindSpeed * ChargeRateFactor <= 0` (`:72`); status `"WindSpeedInsufficient"` `:77` | `ChargeRateFactor=0.2f` `:8` | primitive | **`Wooden Wind Turbine`** `B/…/Furniture.xml:983` (Tier 2, `Organic="true"`, `Bits="BB"`) |
| **`HydroTurbine`** `D/…/HydroTurbine.cs` | summed `LiquidVolume` of own + adjacent cells < `MinimumEffectiveVolume` (`:112-124`); status `"HydrodynamicForceInsufficient"` `:84`. Any liquid counts, not just water (`:132-135`). | `MinimumEffectiveVolume=1000` `:9`, `MaximumEffectiveVolume=6000` `:11`, `MaximumChargeRate=500` `:13` | primitive | **`Wooden Water Wheel`** `B/…/Furniture.xml:1052` (Tier 2, `Bits="BB"`, overrides to 400/4000/10 — **and carries `SpawnWithLiquid` at `:1083`**, so vanilla's water wheel brings its own puddle) |
| `FusionReactor` `D/…:6` | — | `ChargeRate=1000` `:8`, `ExplodeChance=50` `:10`, `ExplodeForce=10000` `:12` | high-tech | `Fusion Power Station` `:1943`, `Fusion Pumping Station` `:1986`, `Hyperbiotic Bed` `:3076` |
| `LiquidFueledPowerPlant` `D/…:10` | out of fuel | `Liquid` `:12`, `Liquids` (multi) `:14`, `ChargePerDram=10000` `:16`, `ChargeRate` `:20` | mechanical | **items only** — every furniture carrier is commented out |
| `ZeroPointEnergyCollector` `D/…:13` | world ∉ `World`, plane mismatch (`:77-104`) | `ChargeRate=10` `:15`, `World="JoppaWorld"` `:17`, `Plane="*"` `:19` | high-tech | `Gravchair` `:3378`, `Black Mote` (`B/Items.xml:6795`) |
| `BroadcastPowerTransmitter` / `Receiver` `D/…:6 / :9` | depth / occlusion | `TransmitRate`; `ChargeRate=10`, `MaxSatellitePowerDepth=12`, `SatelliteWorld="JoppaWorld"` | high-tech | `Broadcast Power Station` `:1907` |

### Storage and charging

| Part | Knobs | Carriers |
|---|---|---|
| `Capacitor` `D/…/Capacitor.cs:11-35` | `MaxCharge=10000`, `ChargeRate=5`, **`MinimumChargeToExplode=1000`**, `StartCharge`, `CatastrophicDisable`, `IsRechargeable`, `ChargeDisplayStyle`. Player-rechargeable with `Tinkering_Tinker1` (`:240-244`); **explodes on death** above the minimum (`:342-347`). | `Solar Condenser`, `Food Processor`, `Reclamation Cist` — and our `r_KingdomChargingPost` / `r_KingdomSaltStore`, both correctly carrying `MinimumChargeToExplode="0"` (`ObjectBlueprints.xml:522,634`) |
| `Circuitry` `D/…:11-27` | `MaxCharge`, `StartCharge`, `Description`, `NameForStatus` | `Wooden Wind Turbine` `:989`, `Wooden Water Wheel` `:1058` (both renamed `"Gearbox"`) — mirrored exactly by `r_KingdomSailvane` / `r_KingdomWaterWheel` |
| `UniversalCharger` `D/…:9,44` | `ChargeRate=10` (0 ⇒ unlimited passthrough) | `Universal Charging Station` `:1537` (`ChargeRate="300"`, Tier 4) — our post uses `150` |
| `InductionCharger` `D/…:9,18,44` | `ChargeRate=10`, works on inventory | `Induction Charging Station` `:1160` (`ChargeRate="500"`, Tier 3) |
| `EnergyCellRack` `D/…:9-11` | `SlotType`, `PreventOther` | **`Energy Cell Rack` is commented out** (`B/…/Furniture.xml:1849-1872`) — no live carrier |
| `Flywheel` | `Charge`, `MaxCharge`, `ChargeRate`, `StartCharge` | `Solar Power Station` `:1440`, `Solar Pumping Station` `:1467` |

### Commented-out blueprints — the trap

Verified by comment-state parse of `B/ObjectBlueprints/Furniture.xml`. These **do not exist at
runtime**: `Combustion Turbine` (1373), `Electric Crematorium` (1403), `Grit Gate Electric
Crematorium` (1428), `Energy Cell Rack` (1850), `Pumping Station` (1874), `Solar Broadcast Power
Station` (2102), `Thermoelectric Turbine` (2127), `Biodynamic Turbine` (2210), `Broadcast Power
Plant` (2240), `Fusion Broadcast Power Station` (2262).

Live and confirmed: `Electric Generator`, `Solar Power Station`, `Solar Pumping Station`, `Broadcast
Power Station`, `Universal Charging Station`, `Induction Charging Station`, `Hydraulic Turbine`,
`Millstone`, `Hydraulic Irrigator`, `Air Well`, `Solar Still`, `Solar Condenser`, `Wooden Water
Wheel`, `Wooden Wind Turbine`, `Fusion Power Station`, `Reclamation Cist`, `Hydraulic Press`.

Two name traps: **`Electric Generator` is a solar panel** and dies at night; **`Hydraulic Turbine`**
(`:1565`) is **not** water power but a hydraulic→electrical converter. The water-power blueprint is
`Wooden Water Wheel`.

---

## 5. FURNITURE that actually works

| Blueprint | Load-bearing part | What it does | Register | Verdict |
|---|---|---|---|---|
| `Campfire` `B/…/Furniture.xml:4465` | `Campfire`, `LightSource Radius="3"`, `Physics FlameTemperature="10000"`, `ThermalInsulation=500` | cooking + light + heat (heats the cell +150° at 10%/turn, `Campfire.cs:164-184`); doused by ≥10 drams of open liquid (`:1318-1326`) | primitive | **USE-AS-IS** (already commissioned) |
| `Oven` `:4504` | `Campfire`, `ThermalInsulation=700`, `CampfireHeatSelfOnly`, `QuestableVerb="cook at"` | cooking site with its own `PresetMeals`; does **not** heat the cell | primitive | **USE-AS-IS** |
| `Torchpost` (renders "torch sconce") `:4075` | `LightSource Radius="6"` + `AnimatedMaterialFire` | light | primitive | **USE-AS-IS** (already in every furnishing table) |
| `Brazier` `:4036` / `Tall Brazier` `:4054` / `Sconce` `:4070` / `Half Candelabra` `:4140` | `LightSource Radius="6"` (+ `LiquidProducer Liquid="wax"` on the candelabra) | light | primitive | **USE-AS-IS** — the shrine/temple/hall tables want these |
| `Techlight1-3` `:4092,4108,4118`; `Full-Spectrum Techlight` `:4129` (Radius **10**) | `LightSource` | light | high-tech | **USE-AS-IS** at high tiers |
| `Bedroll` `:2977` / `Hammock` `:3006` / `Waterbed` `:3042` / `Bedger` `:3057` / `Hyperbiotic Bed` `:3076` | `Bed` | see below | primitive → high-tech | **USE-AS-IS** |
| `Millstone` `:1015` | `Mill` + `MechanicalPowerTransmission` consumer | grinds / butchers / preserves | mechanical | **USE-AS-IS** |
| `Hydraulic Irrigator` `:1189` | `RadiusEventSender Event="AccelerateRipening" Radius="10"` + hydraulic consumer + `PowerSwitch SecurityClearance="1"` + `<stag Food/>` | ripens crops in radius 10 | mechanical | **USE-AS-IS** |
| `Wooden Water Wheel` `:1052` / `Wooden Wind Turbine` `:983` | `HydroTurbine` / `WindTurbine` + `MechanicalPowerTransmission` producer + `Circuitry` "Gearbox" (+ `SpawnWithLiquid` on the wheel) | mechanical power | primitive | **USE-AS-IS** — already mirrored by our two power works |
| `Wire Extruder` `:1822` / `Rock Tumbler` `:1322` | `ItemConvertor` | item transformation | mechanical | **USE-AS-IS as templates** |
| `Village Monument Amphora` `:5623` | `LiquidVolume MaxVolume="256" Collector="true" InitialLiquid="water-1000"` + `LeakWhenBroken` | civic water vessel that collects | primitive / civic | **USE-AS-IS** |
| `Preserved Food Basket` `:4001` | `Container`+`Inventory` + `<tag InventoryPopulationTable="IngredientsBasket"/>` | storage pre-stocked with 2–3 ingredients | primitive | **USE-AS-IS** — the only food-flavoured container in vanilla |
| `Chest` `:3509` / `Woven Basket` `:3986` / `Multicabinet` `:4004` | `Container` (`Preposition`, `OpenSound`, `CloseSound` — `D/…/Container.cs:11-15`) + `Inventory` | storage | primitive | **USE-AS-IS** — already the larder/granary base. **There is no larder, barrel, or crate part in vanilla; all food containers are generic.** |
| `PowerLine` `:830` / `HeavyPowerLine` `:849` / `BaseHydraulicPipe` `:858` / `GlassHydraulicPipe` `:871` / `WoodenMechanicalTransmission` `:910` | `*PowerTransmission IsConduit="true"` + `MergeConduit` | visible grid | primitive → high-tech | **USE-AS-IS** — the furniture that says "this place is wired and plumbed" |
| `Switch` `:117` / `DoorSwitch` `:122` / `PowerSwitch` (many) | `Switch` (`Enabled`) / `PowerSwitch` (`Active`, `SecurityClearance`, `KeyObject`, `FrequencyCode`, `ActivateVerb`…) | operable controls | mechanical | **USE-AS-IS** |

**`Bed` is a real, underused hook.** `D/…/Bed.cs` is an `IActivePart`. Sleep offers 150 / 375 / 600
turns (`:337-339`). The mechanical benefit is
`if (++HealCounter >= 10 - ParentObject.GetTier()) { Actor.Heal(1); }` (`:416-424`) — **heal rate
scales with the blueprint's `Tier` tag** — and the whole block is gated on `IsReady(UseCharge:true…)`
(`:418`). `SleepEvent1/2/3` with `SleepEventTurns/Level/SendSource` (`:15-39`, dispatched `:395-430`)
fire arbitrary named events on the sleeper: **periodic effects during sleep with zero C#**. Beds are
also AI-visible healing furniture (`PollForHealingLocationEvent` `:230-249`).

Our five housing designs all declare a bare `<part Name="Bed" />` and **no `Tier` tag**
(`ObjectBlueprints.xml:166,180,194,215,229`) — so tent and manor heal identically, and the fine
house's entire premise is unexpressed in the object.

**Vanilla's "same appliance, different power source" pattern** is directly copyable:
`Grid Quantum Rippler` / `GridNormCore1-3` (`B/…/Furniture.xml:2688,2736,2744,2752`) are the ordinary
objects with `<removepart Name="FusionReactor"/>` + `<removepart Name="BroadcastPowerReceiver"/>`
replaced by `ElectricalPowerTransmission IsConsumer="true"`.

---

## 6. Re-grounding map for the catalogue

Read against `KingdomBuildings.xml`. Each design below currently declares a `Carries` number whose
reason is not visible in its blueprint.

### 6.1 Water lane

| Design | Today | Grounded replacement story | Tier per Addendum 11 |
|---|---|---|---|
| `saltpan` / `saltterrace` (`:194-201`) | **Already grounded.** `r_KingdomSaltPan` carries `LiquidProducer Liquid="water" VariableRate="400-800" FillSelfOnly="true"` + a 32-dram `LiquidVolume` (`ObjectBlueprints.xml:237-241`). | Keep, and make the *chemistry* visible: `LiquidVolume`'s own evaporation removes water preferentially from a salt mixture and leaves the salt behind (`UseDramsByEvaporativity`, `LiquidVolume.cs:4821-4873`) — **the engine already models a salt pan.** Give the pan a starting `InitialLiquid="water-600,salt-400"` like `Solar Still`, yield salt as well as water, and let that salt be the molten-salt store's input. Siting affinity with `saltLichen` (`B/…/Creatures.xml:9965`) is the renewable-feedstock jackpot. | camp — correct as-is |
| `catchment` / `catchmentbank` (`:205-213`) | **Already grounded** (`LiquidProducer VariableRate="250-550"` / `160-320`). But `Sky="yes"` promises weather that does not exist. | Re-name the reason: this is **dew**, not rain, which is exactly what vanilla's `Air Well` description says. Furnish it with `Catchbasin` (`Collector="true"`) so the collection is visible, and make `Air Well` itself the top rung of this ladder — Tier 1, primitive, no power, and canonical. Keep `Sky="yes"` as a *siting* rule (open to the night sky), never as a weather read. | camp / steading — correct |
| `cistern` / `cisternvault` (`:216-225`) | Storage only (`LiquidVolume MaxVolume=256/640`) — **but `Carries="water:8"` / `water:18`**. | The honest hard case: buffering the dry stretch *is* a real contribution to how many a place carries. Keep the number, make the reason legible in the prose and the report ("carries N **provided the pans and catchments run**"), and **gate it behind at least one producer having been raised**. Furnish with `Village Monument Amphora` (256 drams, `Collector="true"`) so the court visibly holds water. It must never be the settlement's first water building. | steading / village, **behind a producer** |
| **`reservoir`** (`:228-230`) | **BIGGEST CASUALTY.** `Carries="water:26"` on a blueprint whose only parts are `Render`, `Description`, `LiquidVolume MaxVolume="1600"`, `Physics` (`ObjectBlueprints.xml:280-285`). The catalogue's own comment says "a reservoir holds what falls into it" — **nothing falls in Qud.** | Flip to **storage-only** and re-earn the number: either (a) it is filled by a visible channel — `GlassHydraulicPipe` / `BaseHydraulicPipe` furniture running from a pool the claim owns, with `LiquidPump` doing the moving (invariant-safe: it mints nothing); or (b) drop `Carries="water"` entirely and let it be pure capacity plus the `taf:openwater` amenity, with the water lane's carrying capped by producers. Option (a) is the lore-visible one and needs the `MinZones="3"` claim to actually contain open water. ⚠ `LiquidPump` has no live vanilla carrier — this is real, but untested by shipped content. | town, **behind a producer and a channel** |
| **`waterworks`** (`:232-235`) | **BIGGEST CASUALTY.** `Carries="water:52"` — the largest unearned number in the file — on `LiquidVolume MaxVolume="4000"` and nothing else (`ObjectBlueprints.xml:288-293`). Its own display name says "cistern, court, and **channel**". | Make the channel real: `HydraulicPowerTransmission` (conduit or consumer), a `PowerSwitch`, and a recovered `Solar Pumping Station` or a bank of `Air Well`s as its `Contents`. This is the design that most deserves `MinTech="foundry"` **and** a `Knowledge="machine:…"` certification gate, because a citywide waterworks is precisely the "recovered and certified, never commissioned" tier the catalogue already reserves. | city, **`MinTech` + certified machine** |
| — (missing rung) | The catalogue removed the solar condenser and put nothing in the recovered-machinery slot. | Restore the slot as a **certified machine**, not a commission. Vanilla itself marks the boundary with `CanBuild`: `Air Well` (Tier 1) is commissionable; `Solar Still` (Tier 3, `CanBuild="false"`) and `Solar Condenser` (Tier 4, `CanBuild="false"`) are hauled home and passed fit for the grid. | `Knowledge="machine:…"` |

### 6.2 Food lane

| Design | Today | Grounded replacement story | Tier |
|---|---|---|---|
| `plot` / `plotrows` (`:258-264`) | **Grounded.** `r_KingdomPlot` carries the mod's own part with a real Dormant→Growing→Ripe cycle, water-costed at 3 drams, yielding 4 food into real larders (`Growth/KingdomCropRules.cs:26,36`). The survey **ratifies owning this state**: vanilla has nothing with three stages. | Add the **seed** Addendum 11(b) asks for. Vanilla has no plantable seed, so the item is ours — but draw the crop it becomes from `DynamicObjectsTable:FarmablePlants` (tier- and region-aware) rather than the hand-list in `CropBlueprintForStyle`, and give the ripe plot a real `Harvestable` (with `RegenTime` set) so the harvest reads in vanilla's own verb through `CookingAndGathering_Harvestry`. Mark the crop `VillageDomesticated=1` the way vanilla marks a village's own plants. | camp — correct |
| **`field` / `fieldrows`** (`:266-279`) | `Carries="food:8"` / `food:18` on a blueprint with **no parts at all** beyond `Render`/`Description`/`Physics` (`ObjectBlueprints.xml:299-309`). A field that grows nothing. | Make it a **plot of plots**: the finished work furnishes its footprint with rows of a real crop object — vanilla's own pattern, one species in alternating columns with liquid channels between them (`D/…/Village.cs:1279-1310`) — each carrying the crop part. The `Carries` number then describes what those rows actually cycle. Fence it with `BrinestalkFence` and a `Brinestalk Gate` the way `StarappleFarm.cs:130-146` does. | steading / village |
| **`grange`** (`:309-310`) | `Carries="food:26"`; the blueprint inherits `r_KingdomField`, i.e. **inherits nothing that works**. | Field rows **plus the barn**: a `Chest`-derived store on the plot so "the barn at the end of them" is a real container, and a `Millstone` for the threshing floor. | town |
| **`homefarm`** (`:313-315`) | `Carries="food:40,craft:2"`, same empty inheritance. Its description names "a mill with metal in its gearing". | Same at scale, and **make the mill real**: `Millstone` (`Mill` + `MechanicalPowerTransmission` consumer) fed by the settlement's `r_KingdomWaterWheel` / `r_KingdomMill`, which already produce mechanical power. That single link turns `craft:2` from an assertion into a wired fact, and turns harvest into preserves automatically. Optionally a recovered `Hydraulic Irrigator` as the city rung's certified machine — vanilla's own irrigation, on vanilla's own event. | city |
| `larder` / `granary` (`:257,282-284`) | **Already grounded.** Both inherit `Chest` for real `Container`+`Inventory` and declare capacity by tag (`ObjectBlueprints.xml:137,311-318`); `KingdomSurvey` counts real `Food` / `PreparedCookingIngredient` items (`Growth/KingdomSurvey.cs:268`). | Keep. Make "refuses to waste what came in" the **`PreservableItem`** mechanic rather than a number: a granary keeper preserving raw yields into stackable ingredients is exactly `Campfire.PerformPreserve` (`:512-566`), and it is what `Millstone` does automatically. The `granary`'s `Carries="food:9"` on one keeper is the single honest storage-carries-food case, because preserving really does raise how many a harvest feeds. Consider `<tag InventoryPopulationTable="IngredientsBasket"/>` for a stocked larder, following `Preserved Food Basket`. | village |
| `grindmill` (`:338-341`) | `Carries="food:4,craft:1"` on a blueprint with **no parts**. | Carry `Mill` with a `Transformations` list over our crops, `Container`+`Inventory`, `MechanicalPowerTransmission IsConsumer="true"`, and `<stag Name="Food"/>`. Then `food:4` is "it turns the harvest into something that keeps" as a *fact* — and it is `Millstone`'s exact configuration, Tier 2, at the same register. | steading |
| `fire` (`:381-383`) | Commissions vanilla `Campfire` for `spirit:1`. | **Already the best-grounded design in the file, and nothing says so.** It is a real cooking site. Upgrade path: an `Oven` rung carrying the settlement's own `PresetMeals`. | camp → village |
| — (new, from the survey) | Nothing keys the shared meal to Qud's cooking vocabulary; `KingdomLarder.HoldSharedMeal` spends abstract "servings" (`Growth/KingdomLarder.cs:53`). | **The favoured meal has an exact vanilla home.** Set `Faction.WaterRitualRecipe` / `…Text` / `…Genotype` on the runtime faction the mod already creates (`Core/KingdomFounding.cs:36`); all three serialise on the `Faction` (`D/…/Faction.cs:286-288,362`). The settlement's dish then becomes teachable through the water ritual in vanilla's own frame — *"Would you teach me to cook the &lt;kingdom&gt;'s favorite dish?"* — and a named cook can carry `TeachesDish` (a part **no vanilla blueprint uses**, i.e. free). Non-player meal effects last 1200 ticks (`ProceduralCookingEffect.cs:220-223`), which is **exactly `KingdomRules.TicksPerDay`** — a shared meal that buffs settlers for a day is vanilla's own number. | village → town |
| `bathhouse` (`:404-407`) | `luxury:6,spirit:2`, no parts. | Wants water it can *spend*: a `LiquidVolume` the settlement fills, with the amenity conditioned on it being non-empty. `Regen Tank` (`Collector` + `HasDrain`, `Furniture.xml:2167`) is the vanilla shape. | town |

### 6.3 Power lane

Already the best-grounded lane. `r_KingdomWaterWheel` carries vanilla `HydroTurbine` +
`MechanicalPowerTransmission` + `Circuitry` matched to `Wooden Water Wheel`'s own numbers;
`r_KingdomSailvane` mirrors `Wooden Wind Turbine`; `r_KingdomChargingPost` carries
`UniversalCharger`; `r_KingdomSaltStore` carries `Capacitor` (`ObjectBlueprints.xml:595-634`).

Three gaps:

1. **`r_KingdomWaterWheel` has no water.** `HydroTurbine` needs ≥`MinimumEffectiveVolume` (our 400)
   drams summed across its own and adjacent cells (`D/…/HydroTurbine.cs:112-124`), and vanilla's
   `Wooden Water Wheel` solves that by carrying **`SpawnWithLiquid`** (`B/…/Furniture.xml:1083`,
   default `LiquidObject="SaltyWaterPuddle"`, `AdjacentPoolChance=25`, self-removing after it
   fires). Ours does not — so unless the founder sites it on standing water it reports
   `"HydrodynamicForceInsufficient"` forever. The catalogue's own display name says "raise it beside
   open water", so the intent is siting, not spawning; but the *check* should be a siting rule the
   commission enforces, not a silent failure.
2. **No solar rung.** `SolarArray` is the missing primitive-to-mechanical bridge and it is naturally
   metered — it fails at night, indoors, and in blacked-out cells (`D/…/SolarArray.cs:60-81`). A
   solar rung would also finally give `r_KingdomSaltStore` ("keeps the day's power for the night") a
   *day* to keep. `Electric Generator` (Tier 3) is the primitive-looking carrier.
3. **Nothing consumes the mechanical power.** Three producers, no consumer. `Millstone` on the grind
   mill closes the loop (§6.2), and `GridCapacity` being the network minimum
   (`D/…/IPowerTransmission.cs:1172-1175`) makes "the wooden axle throttles the whole grid" a real,
   legible constraint we can show the founder.

Caveat before wiring: `PrimePowerSystemsEvent` pre-charges a generator **only if
`ParentObject.HasPropertyOrTag("Furniture")`** (`D/…/SolarArray.cs:35-38`, mirrored in
`WindTurbine.cs:34`, `HydroTurbine.cs:51`). Our works inherit `Furniture`, so this should hold —
**UNVERIFIED at runtime.**

### 6.4 Designs that should flip from producer to storage-only

| Design | Reason |
|---|---|
| **`reservoir`** | Holds; does not make. Nothing falls from the sky to fill it. Pipe it, or drop the `water` carry. |
| **`waterworks`** | Same, at four times the scale. Its `Carries="water:52"` is the largest unearned number in the catalogue. |
| `cistern` / `cisternvault` | Legitimately carry — buffering is a real contribution — but only behind a producer. Gate them. |
| `field` / `fieldrows` / `grange` / `homefarm` | Not storage-only, but the **same defect**: a `Carries` number on an object with no parts. They stay producers; they must become producers *in the object*, not only in the ledger. |

Everything else in the water lane (`saltpan`, `saltterrace`, `catchment`, `catchmentbank`) is already
a producer with a real vanilla part, and the survey ratifies it.

---

## 7. The early game, as it already works

Addendum 11(a) asks that the camp **cost** water before automation. That machinery is already built,
and the survey confirms it is the right shape:

- **Upkeep.** `KingdomRules.UpkeepDrams(Population, Stage)` = `Population × StageUpkeepPercent / 100`,
  with `StageUpkeepPercent = {100, 120, 150, 180, 220}` by stage (`Core/KingdomRules.cs:508-521`).
  One dram per settler per day at camp; 2.2 at city.
- **Reserve.** `ReserveDays = 3` (`Core/KingdomRules.cs:483`) is a *quantity*, not a clock cap — the
  cushion held back before anything discretionary is spent. `KingdomCropRules.CanAffordPlanting`
  (`Growth/KingdomCropRules.cs:62-66`) is the worked example: a plot may only plant from what is left
  once three days of drinking is set aside.
- **Fetch.** `FetchableDrams(Hands, OpenWater, StorageSpace, Days)` = `Hands × 2 × Days`, clamped by
  open water actually standing in the zone and by room left in dedicated stores
  (`Core/KingdomRules.cs:1428-1444`; `FetchDramsPerSettler = 2` at `:441`). Hands are **named**: only
  settlers the founder put on the water detail walk to the water
  (`Core/KingdomCharterPart.cs:274-300`). An empty detail means the settlement drinks only what the
  founder pours in.
- **Pools are a finite dowry.** `KingdomSurvey.Take` counts `MaxVolume < 0` volumes as `Pools` and
  sums `OpenWater` (`Growth/KingdomSurvey.cs:157-164`); nothing in vanilla refills them. Draining a
  pool your own people hauled from mints nothing — it repositions water you could have scooped
  yourself. **Fetch is invariant-safe; production is the only channel that mints.**
- **The founder's own pour** is the other early channel, through `LiquidVolume`'s ordinary `"Pour"`
  action (`D/…/LiquidVolume.cs:3037,6162`) into a container dedicated with `KingdomStores`.
- **Freshness is unforgiving and already modelled**: `IsFreshWater()` is `IsPureLiquid("water")`
  (`D/…/LiquidVolume.cs:1357-1360`), so a settlement hauling from a `SaltyWaterPuddle`
  (`water-600,salt-400`) gets **nothing** — which is why the salt-pan is the dry-ground answer and
  not a nicety.

So the honest early sequence is: **arrive with a stock → put hands on the detail → drink down the
site's finite fresh pools → raise a salt-pan or a catchment before they run dry → and only much
later, behind tech and a recovered machine, does the settlement stop counting.** Nothing needs
inventing. What needs doing is making the *middle* of that sequence — cistern, reservoir, waterworks
— stop claiming to be the end of it.

---

## 8. What the engine simply cannot support — INSPIRE-ONLY lanes

These must stay abstract, or be built entirely in mod code with no vanilla hook to lean on.

| Lane | Why |
|---|---|
| **Rain, seasons, wet years, drought cycles** | No precipitation system at any level; wind is the only weather. A "bad season" must be our own state, never a weather read. |
| **Seeds as a vanilla item class** | Zero plantable blueprints, zero planting verb, zero seed→plant transformation. Everything about sowing is ours; only the *names* (Arsplice Seed, Rubber Tree Seed, the tubers) are borrowable. |
| **Multi-stage crop growth** | `Harvestable` is `bool Ripe`. Three stages is two more than the engine has. `r_KingdomPlot` is correct to own its state; the survey ratifies the decision and supplies the reason. |
| **Growth while unobserved** | Every vanilla clock is `TurnTick`/`EndTurnEvent` and stops when the zone suspends (`D/XRL/Core/ActionManager.cs:430-447`). Only `Temporary` compensates, and only for itself. Our tick-stamp pass is not a workaround; it is the engine's own idiom. |
| **Food spoilage** | No spoil/rot/decay part anywhere. If the mod ever wants larder loss, it is ours end to end. `PreservableItem` is a cooking input, not a timer. |
| **Fermenting, distilling, smelting, weaving, pressing** | No such part exists. `Mill` and `ItemConvertor` are the *entire* transformation surface; a still, a loom, or a smelter must be one of those two wearing a different name, or pure abstraction. The smelter and the two yards already are abstractions (`Refines=`), and that remains the honest answer. |
| **Water supply networks** | `HydraulicPowerTransmission` carries joules, not supply; its only liquid motion equalises with adjacent volumes and routes nothing. `LiquidPump` moves liquid but ships with **no live carrier**. A water network is buildable and untested — a real risk, not a free hook. |
| **New liquids** | A mod cannot define one from XML; `[IsLiquid]` requires a compiled `BaseLiquid` subclass (`D/…/LiquidVolume.cs:1112-1132`). We only need `water` and `salt`, so this does not bind — but it caps the lane. |
| **NPC/crew withdrawal from containers** | `Container.CanSmartUseEvent` returns false for non-players (`D/…/Container.cs:26-33`); nothing in `Container` grants NPC access. Whether any AI goal handler pulls from containers is **UNVERIFIED**. Our crews must move items in code. |
| **A resident preferring a *food item*** | Vanilla has no `FavoriteFood`, `PreferredFood`, or per-NPC item preference — zero hits across both trees. What it has is a **faction-level favourite *dish*** (§2.3) and dietary *restriction* (`Carnivorous`, `Food.Gross`, genotype gates). Preference beyond that is ours. |
| **New tinkering bit types** | Hardcoded in C# (`D/XRL/World/Tinkering/BitType.cs:413-432`). Our `Bits="0034"` currency must stay inside the twelve shipped types. |

---

## 9. Verified clean

Checked, and found to need no change:

- `r_KingdomSaltPan`, `r_KingdomSaltTerrace`, `r_KingdomCatchment`, `r_KingdomCatchmentBank` — real
  vanilla `LiquidProducer`, sensible rates, correctly `FillSelfOnly` and non-tech-scannable.
- `r_KingdomSailvane` — vanilla `WindTurbine` + `MechanicalPowerTransmission` + `Circuitry`, numbers
  matched to `Wooden Wind Turbine`, and wind genuinely exists as zone state.
- `r_KingdomChargingPost`, `r_KingdomSaltStore` — `MinimumChargeToExplode="0"` correctly defends
  against `Capacitor.HandleEvent(BeforeDeathRemovalEvent)` (`D/…/Capacitor.cs:342-347`).
- `r_KingdomLarder`, `r_KingdomGranary` — inherit `Chest` for real `Container`+`Inventory`, capacity
  by tag; `KingdomSurvey` classifies contents with vanilla `Food` / `PreparedCookingIngredient`, and
  **neither part mutates on inspection** (verified against both parts' field sets and event
  handlers).
- `Core/KingdomLiquids.cs` — `HasFreshWater` / `CanReceiveFreshWater` / `Drain` / `Fill` wrap
  `LiquidVolume` correctly, including the `UseDrams` return-value trap its own comments record, and
  its freshness test matches the engine's `IsPureLiquid("water")` exactly.
- `fire` commissioning vanilla `Campfire` — a genuine, working cooking site.
- `Growth/KingdomPlot.cs`'s decision to own its crop state and resolve absence from a stored tick —
  ratified: vanilla has no three-stage growth and no absence-safe plant clock, and `Temporary`
  (`D/…/Temporary.cs:137-157`) is the engine's own version of the same idiom.
- The `Contents=` furnishing tables place `Torchpost` (real `LightSource Radius="6"`) and
  `Bookshelf`; the pattern is sound — the tables are simply thin.

## 10. Open, and not answerable from source

- Whether a **runtime-set** `Faction.WaterRitualRecipe` survives the whole
  `WaterRitualCookingRecipe` conversation path for a faction created after worldgen
  (**UNVERIFIED**; the fields serialise, the conversation path was read, the combination was not
  observed).
- Whether setting `RegenTime` on a mod `Harvestable` behaves as the code reads — the loop is
  provably unexercised by any shipped blueprint (**UNVERIFIED in play**).
- Whether `Solar Still`'s `ConsumesLiquid="water"` with `LiquidMustBePure` left true actually
  functions against its own `water-600,salt-400` tank, or stalls (**UNVERIFIED**; matters only if we
  copy that configuration).
- Whether `PrimePowerSystemsEvent` pre-charge fires for our works at zone activation
  (**UNVERIFIED at runtime**; the `Furniture` tag condition was read, the behaviour was not
  observed).
- Whether any AI goal handler withdraws from a `Container` (**UNVERIFIED**; only a live playtest or
  a fuller `D/XRL/World/AI/GoalHandlers` sweep settles it).
