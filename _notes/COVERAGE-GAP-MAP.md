# Coverage gap map — what the building-catalogue brief promised versus what the code delivers

> **Historical audit, not current status.** Every verdict below is frozen to commit `3f7f9d1` on
> 2026-08-21. Gatehouse siting, raisings, subsidence, physical materials, authored architecture,
> delve links, inheritance spatial receipts, and other named gaps have since changed. Use
> `BRIEF-IMPLEMENTATION-AUDIT.md`, `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`, and
> `../docs/STATUS.md` for current disposition. The old rows remain intact as reproducible attack
> history and must not be quoted as present implementation truth.
>
> **DO NOT TRIAGE THESE ROWS AS CURRENT GAPS.** They intentionally preserve findings from the old
> commit. For example, this snapshot says the raising ceremony had only one caller and the salvage
> expedition was missing; the current tree has the bound `KingdomPlot2` caller and
> `Experience/KingdomExpeditions`. Re-proving hundreds of historical source-line claims would erase
> the audit's reproducibility rather than improve freshness. Use the current acceptance ledgers
> above, and use `V1-POLITY-SCOPE.md` for polity/world-presence disposition.

Date: 2026-08-21. Scope: `_notes/BUILDING-CATALOGUE-BRIEF.md` (main brief + Addenda 1–10 + BACKLOG)
audited line-by-line against HEAD `3f7f9d1` (96 staged sources, 4149 test declarations green).
Read-only pass; no code was changed.

**Rule applied.** BUILT means the mechanic runs on a path a player can reach, and something pins it.
PARTIAL means a named piece of the promise is absent — an enum, a rule function, or a tested pure
helper with no runtime caller is *not* the mechanic. MISSING means no implementation. SUPERSEDED
means a later ruling replaced it.

**Docs were not trusted.** `docs/API.md`, `MODDING.md`, `TESTING.md` and `CHANGELOG.md` were used
as indexes only; every verdict below was re-derived from the C# or the XML. Two doc overstatements
were found and are recorded in the table.

## Verdict counts

| Verdict | Count |
|---|---|
| BUILT | 82 |
| PARTIAL | 27 |
| MISSING | 17 |
| OPEN (deferred by an explicit author ruling, not a gap) | 2 |
| SUPERSEDED | 3 |
| **Total commitments tracked** | **131** |

The headline: the brief is *substantially built*. The gaps are not a missing half of the design —
they are (a) one whole physical flow that was never wired, (b) a recurring pattern of **computed
numbers that are thrown away instead of applied**, (c) two unbuilt co-opted ideas, and (d) a small
number of call sites that were never joined, which is where the cheapest wins are.

**The pattern worth naming.** Six separate commitments fail the same way: a value is parsed or
derived, validated, tested as a pure rule, printed in prose — and then read by nothing that
matters. Notable-taste shade, leader-trait shade, `Prefers` shade, yard-trade `Shades`, four of
five reach-scoped lifts, and the whole merge-reconciliation guardrail are all in this state. It is
one habit, not six bugs, and it is the cheapest thing in this map to fix.

### Superseded

| Ruling | Superseded by | Status |
|---|---|---|
| "Absence accrues, never decays" (and its supply/social first correction) | Addendum 8 time doctrine + Addendum 10(a) | Applied; prose swept (`VISION.md:77-99`, `TESTING.md:452-461`) |
| The single flat `CohabitHostility` floor | Addendum 4c closeness ladder | Applied in law (`Growth/KingdomLodgingRules.cs:169-195` documents the promotion); **the old surface is still shipped** — see §12 |
| COHAB's "sleeps in the open and waits forever" | Addendum 4b housing binds | Applied (`Growth/KingdomGrowth.cs:328-337`) |

---

## 1. Plots are the unit of building (main brief)

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Tiers S / M / L / XL | Plots | BUILT | `Growth/KingdomPlotRules.cs:36-49` | — |
| Open (unroofed) plots: fields, yards, reservoirs, markets, salt-pans | Plots | BUILT | `Open="yes"` on 18 entries, `KingdomBuildings.xml`; parsed `Growth/KingdomPlotRules.cs:1697` | — |
| Plot size stage-gated (Camp S / Steading M / Town L / City XL) | Plots | BUILT | `Growth/KingdomPlotRules.cs:347-360`; enforced `Growth/KingdomPlot2.cs:923`, `:1105` | — |
| Budget per zone with roads reserved | Plots | BUILT | `Growth/KingdomPlotRules.cs:966-973` `WouldExceedBudget` = laid area + new tier vs `PlotAreaAllowance` net of `RoadBudgetPercent` | — (implemented as an area budget, not a per-tier count; the brief's "1 XL + 2–3 L…" is the same constraint expressed as a mix) |
| `MaxBuildingsForStage` counts plots, not furniture | Plots | BUILT | `Core/KingdomRules.cs:74-89`; `Growth/KingdomPlot2.cs:941` exempts walls and plot parts; contents furnished separately at `:1867` | — |
| Single-cell civic furniture demotes to plot contents, populated from tables | Plots | BUILT | `Contents=` on 21 entries; `Growth/KingdomPlot2.cs:1024`, `Furnish` `:1880` | — |
| **Gatehouse is a placement rule: on the frontier wall, astride a road** | Plots | **MISSING** | `"gatehouse"` appears in **zero** `.cs` files — only `KingdomBuildings.xml:459-463` and `ObjectBlueprints.xml:428`. It declares no `Plot`, so it is sited by `line.GetRandomElement()` like any palisade segment (`Growth/KingdomCommission.cs:255`) | The road already has a frontier terminus — `KingdomRoadRules.TryGate` (`Growth/KingdomRoadRules.cs:497`, called only from `Growth/KingdomRoads.cs:441`). Nothing joins the gatehouse design to that cell. The XML's own excuse ("until roads exist to meet") is now stale |
| `PlaceHut` never called; refuse rather than clear | Plots | BUILT | `Growth/KingdomPlot2.cs:128-132` (explicit never-call contract) | — |
| Upgrades climb within a plot; sizes compete across; no in-place S→M | Plots | BUILT | `UpgradesTo` chains; validator refuses improvement onto a larger plot, `Growth/KingdomCatalogueRules.cs:790-860` | — |

## 2. Axes with gates, not rungs

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Stage gate opens ranges | Axes | BUILT | `Core/KingdomCharterPart.cs:461`, `:550`; plot-size stage gate above | Nit: `MinStage` is enforced **only** in the two Charter menu filters. `KingdomCommission.Commission` never re-checks it, so non-menu paths (socket rebuild, plan resolution) rely on the plot-size gate — which does not cover the four single-cell wall designs |
| Materials gate | Axes | BUILT | `Growth/KingdomMaterials.cs:1066` `CanPay`; `Growth/KingdomCommission.cs:97` | — |
| **Certification gate ("recovered-and-certified rather than built")** | Axes | **PARTIAL** | Certifying is real and Charter-reachable: `Core/KingdomCharterPart.cs:1024-1047` → `Growth/KingdomSalvage.cs:134-137` → `KingdomZoning.RecordCertification` (`Growth/KingdomZoning.cs:173-180`) | **No design is gated on a certified machine.** The four `Knowledge=` uses are one `origin:` and two `pattern:`. The archetype — the commissionable solar condenser deliberately removed at `KingdomBuildings.xml:66-70` — was never replaced by a `Knowledge="machine:…"` design. Certification's only effect on buildability is +2 tech points |
| **Covenant standing as a gate** | Axes | **MISSING** | Standing gates trade charters only: `KingdomDeals.xml:3-4`, `Trade/KingdomTrade.cs:33`. No `<building>` attribute reads standing (parser: `Core/KingdomRules.cs:2039`) | An entire named gate axis has no building-side implementation |
| Technology / blueprint ACCESS progression, separate from stage | Axes; Add. 3, 7 | BUILT | `TechLevel` enum `Growth/KingdomZoningRules.cs:12-24`; points from a learned roster (disk 1, certified machine 2) `:490-525`; thresholds `{0,2,5,9,14}` `:226`; derived `Growth/KingdomZoning.cs:187`; gate `Growth/KingdomZoningRules.cs:681`; readout with distance-to-next `Growth/KingdomZoning.cs:196-212`. **The suspected gap is not real — this is built and named-refusal-legible** | — |
| …and the ladder is actually used | Axes | PARTIAL | Only `MinTech="salvage"` (×3) and `"workshop"` (×3) in 57 entries | **`Foundry` and `Arclight` gate nothing.** Two of five rungs are content-free; the whole ladder is exercised across 0–5 of 14 points |
| Crude end cheap/weak; far end of autonomy recovered-and-certified | Axes | PARTIAL | Ladder economics real in the catalogue | Same as the certification row — the "far end is recovered, not built" half has no destination design |
| Dry ground's early answers: salt-pan, collection, trade | Axes | BUILT | `saltpan`/`saltterrace`, `catchment`/`catchmentbank`, `KingdomDeals.xml` | — |

## 3. Materials — clearance IS extraction

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Clearing yields what stood: brush/trees→timber, rock→stone, marble→marble, ruins→scrap | Materials | BUILT | `Growth/KingdomPlotRules.cs:1282-1305` `YieldOf`; ground read `Growth/KingdomPlot2.cs:409-442` | — |
| Effort scales with hardness; removal earns | Materials | BUILT | `ClearEffort` (Rock 6); `UndergroundClearPercent = 200` `Growth/KingdomPlotRules.cs:1328`, `:1339-1355`; effort → ticks `:1434-1438` | — |
| Materials stockpile like food, dedicated mark, never minted | Materials | BUILT | `StockpileProperty` `Growth/KingdomMaterials.cs:85`; `MaxStockpiles = 8` `:171`; read `Stock(Zone)` `:931-971` with exclusive material/exotic/bits resolution `:947-953` | — |
| Arrive otherwise by trade or salvage | Materials | BUILT | `Deliver` `Growth/KingdomMaterials.cs:1313` ← `Trade/KingdomTrade.cs:94`; strike salvage `Growth/KingdomMaterials.cs:1850` | — |
| Building costs = water + materials | Materials | BUILT | `CanPay`/`Pay` `Growth/KingdomMaterials.cs:1066`, `:1214-1242`; drams `Growth/KingdomCommission.cs:89` | — |
| Material is the theme: wall material per settlement | Materials | BUILT | `WallBlueprintFor` `Growth/KingdomPlotRules.cs:1575`; stockpile taste `Growth/KingdomMaterialRules.cs:897`, `:929` | Both are hardcoded `switch`es over the five built-in styles (see the mod-style row) |
| The marble house needs marble | Materials | BUILT | `finehouse` `marble:8`, `manor` `marble:20`, `KingdomBuildings.xml:164`, `:173` | — |
| **Underground carving credits a stockpile** | Materials / Terrain | **PARTIAL** | Surface clearance credits the real stockpile (`Growth/KingdomMaterials.cs:1945`). Carving writes a **different, orphaned** counter: `KingdomPlots.CreditMaterials` → `The.Game.ModIntGameState("r_TAF_Material_…")` (`Growth/KingdomPlot2.cs:1677-1697`) | Its only reader, `KingdomPlots.MaterialsHeld` (`:1703-1710`), has **zero callers repo-wide, tests included**. Its own docstring says the slot was temporary "until a stockpile with a dedicated mark exists" — which now exists. Carved yields are silently lost relative to surface clearance |

## 4. Lifecycle: tent first, strike honestly

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Housing opens at a tent (cheap, low comfort, small upgrades) | Lifecycle | BUILT | `tent` `KingdomBuildings.xml:90` — Camp, cost 3, `canvas:2`, `Roof="Soft"`, `roof:2`, `UpgradesTo="tentrow"` | — |
| Ladder: timber hut → stone house → marble fine house, each with tiers | Lifecycle | BUILT | `hut:106`, `house:128`, `finehouse:164`, `manor:173`, `court:180`; tiered on both `Plot` and `MinStage` | — |
| Striking is founder-ordered | Lifecycle; Add. 1 | BUILT | Charter option 25 `Core/KingdomCharterPart.cs:81`, `:159`, `StrikeBuilding` `:314`; `OrderStrike` has exactly two callers, both founder-initiated (`Growth/KingdomMaterials.cs:1531`) | — |
| **…a chronicled *ceremony*** | Lifecycle | **PARTIAL** | Chronicled twice — condemnation `Growth/KingdomMaterials.cs:1568`, completion `:1862` | No ceremony hook: no attendance roll, no shared water, no witnesses — unlike the raising, which has all three (`Experience/KingdomCeremony.cs:96-131`) |
| Effort scales with what comes down | Lifecycle | BUILT | `StrikeEffort` `Growth/KingdomMaterialRules.cs:841` | — |
| Returns partial salvage | Lifecycle | BUILT | `StrikeSalvagePercent = 50` `:835`, `:853`; delivered `Growth/KingdomMaterials.cs:1852` with honest overflow spill `:1866` | — |
| Frees plot and crew; refunds no water | Lifecycle | BUILT | `KingdomSocket.OnCleared` `Growth/KingdomSocket.cs:337`; no-refund stated `Growth/KingdomMaterials.cs:1573` | — |
| Old buildings never stop working; nothing regresses silently | Lifecycle | BUILT | 7b idiom `Growth/KingdomGrowth.cs:454-461`, `Growth/KingdomPlot.cs:194`; STANDARDS §7b:193 | — |

## 5. The heart, and expansion

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Rite ground seeds the heart; the heart drifts toward the built centroid | Heart | BUILT | `Growth/KingdomPlotRules.cs:1004-1032`; walls excluded from the mean `Growth/KingdomLayoutRules.cs:258-296` | — |
| XL wants heart-adjacent ground | Heart | BUILT | `HeartPull` applied in `ScoreRect` `Growth/KingdomPlotRules.cs:1106` | — |
| A staked plan beats the grammar anywhere | Heart | BUILT | Charter option 17 → `ManagePlans`; `Growth/KingdomPlanRules.cs` | — |
| **Expansion direction is entirely the founder's — claims decide** | Heart | ~~MISSING~~ **BUILT** | `ClaimZone` (`Core/KingdomFounding.cs:293`) now has a fifth caller: `KingdomCharterPart.ClaimGround` (Charter → **Claim this ground**, hotkey `6`, `Core/KingdomCharterPart.cs:216`), gated through `KingdomFounding.JudgeClaim` / `KingdomZoningRules.JudgeClaim` and reporting the wall clause + holding line on success | **Landed 2026-08-21 (Wave C).** A player can now claim a second (and third, and fourth) zone in normal play, up to `KingdomZoningRules.ZonesForStage`. This closes the root cause named below for the terrain (§6) and cross-zone (§15) rows it pointed at |

## 6. Terrain

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Water cells refuse plots, never filled | Terrain | BUILT | `RefuseLiquid` `Growth/KingdomPlotRules.cs:1848-1850`; `Growth/KingdomPlot2.cs:840` | — |
| A river is an asset — wheel adjacency, collection | Terrain | BUILT | `waterwheel` design; open-water fetch `Growth/KingdomSurvey.cs:139-165` | — |
| Jungle: expensive clearing, timber-rich | Terrain | PARTIAL | Emergent: clearing cost is per ground object, so a treed zone is expensive and timber-rich in practice | No jungle-specific rule; `"Jungle"` in the C# only feeds style resolution and the wall bonus (`Core/KingdomRules.cs:2163`) |
| Underground carved plots: high clearing cost, yields stone | Terrain | BUILT | `RoofState.Carved` `Growth/KingdomPlotRules.cs:129-131`; set from depth `RoofOnGround` `:448-451`; `IsUnderground` `:1318-1321` vs `SurfaceZLevel = 10` | — |
| Enclosure free — the rock is the wall | Terrain | BUILT | `EnclosureTicks` returns 0 unless `RaisesWalls` `:1404-1411`; build-time `Growth/KingdomPlot2.cs:1750-1757` places no wall, still cuts the door `:1742` | — |
| No weather: sky-needing designs refused by name | Terrain; Add. 1 | BUILT | `RefuseSky` `Growth/KingdomPlotRules.cs:1862-1866`, reached from four player paths (`Growth/KingdomPlot2.cs:928`, `:1110`; `Growth/KingdomSocket.cs:209`, `:511`) | — |
| **Fungal crops instead (the fungal style's home)** | Terrain | **MISSING** as a depth mechanic | Crops key off `System.Style` only: `Growth/KingdomCropRules.cs:92-105`, consumed `Growth/KingdomPlot.cs:169`. Zero depth awareness in either file | Underground is not the fungal style's home: reaching `fungal` underground requires the *overlying surface parasang* to be fungal (`Core/KingdomRules.cs:2195-2198`) |
| **Natural chokepoints** | Terrain | **MISSING** | No terrain-derived chokepoint concept. The only spatial defence concept is `Frontier`, a flat 2-cell zone-edge band, stratum-blind (`Core/KingdomRules.cs:2563-2566`, `:2580-2604`) | — |
| **Different catalogue subset by stratum** | Terrain | ~~MISSING~~ **PARTIAL** | `KingdomZoningRules.Judge`'s depth overload (`Underground`, `RequiresSky`) now refuses `RefusedStratum` at commission-menu time, tagging a sky-wanting design **[wants open sky]** in the list itself rather than only turning it away once picked (`Growth/KingdomZoningRules.cs:706-734`, wired at `Growth/KingdomZoning.cs:395`) | **Landed 2026-08-21 (Wave C), partially.** The surface-only half of the filter is real: depth now narrows the offered catalogue at the moment of choosing. Still no way for a design to declare "I belong to the deep" — that authored half is unbuilt, per the doc comment on the overload itself |
| Vertical claims exist | Terrain | ~~PARTIAL~~ **BUILT** | Rules support it: `ZonesAdjacent(…, IncludeVertical: true)` `Core/KingdomFounding.cs:358`; `CoordsAdjacent` accepts `dz == 1` `Core/KingdomRules.cs:2620-2623`; founding captures `FoundingZLevel` `:247` | **Landed 2026-08-21 (Wave C).** Charter → **Claim this ground** reaches the vertical neighbour too — a cellar or a tower is claimable from a standing surface city, not only at a fresh founding. Same fix as §5 |
| — *(defect found)* `Open="yes"` designs forced `Carved` underground | Terrain | ~~PARTIAL~~ **FIXED** | `RoofOnGround` now returns `Declared` unchanged when it is `Open` (`Growth/KingdomPlotRules.cs:461-467`); only a design that declares an enclosure is carved underground | **Landed 2026-08-21 (Wave C).** An underground field, salt-pan, market square or reservoir stays open ground with stone around it — not a sealed chamber, and it does not count as shelter. The measured half of the rule (`RoofFromEnclosure`) already read it this way; the two now agree |

## 7. The luxury lane

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Fine houses are a different good (quality vs quantity) | Luxury | BUILT | `finehouse` `roof:4,luxury:4`; `manor` `roof:6,luxury:9`; `luxury` lifts and is capped at half the binding level `Growth/KingdomCatalogueRules.cs:233`, `:281` | — |
| **A legendary trader settles only when a vacant fine house of sufficient tier exists** | Luxury | **MISSING** | A tier-gated settling mechanic exists (`Experience/KingdomGuestRules.cs:104-115`, enforced `Experience/KingdomGuestbook.cs:302-310`) but every clause fails: no `legendary` in any `.cs`; the three lodged trades are scavenger/machinist/reckoner (`:117-134`), none a trader; `BestHousingTier` classifies by **plot rect area alone** (`Experience/KingdomGuestbook.cs:369-395`, `KingdomGuestRules.cs:387-407`) so a plain `terrace` scores as a `manor`; vacancy is settlement-wide `Population < Beds` (`Core/KingdomRules.cs:658`) | Luxury is never read by the housing gate; no per-house vacancy; no reservation |
| **…and the shop tier warrants** | Luxury | **MISSING** | `ShopTier` is real and live (`Core/KingdomRules.cs:634-649`; market district +1 `:1705`; applied `Growth/KingdomGrowth.cs:747-800`) | It is never read by guest or lodging code. The conjunction the brief names is written nowhere |
| Office holders and keepers may want the same | Luxury | MISSING | Holder = `System.RosterNames[0]`, longest-served, full stop (`Experience/KingdomOffices.cs:195-229`). Quarters are read only to compute a shade that is discarded | No housing precondition on holding office |
| S plots never obsolete — struck economy-huts become fine-house ground | Luxury | PARTIAL | Principle honoured in the tier fallback with a citing comment (`Experience/KingdomGuestbook.cs:346-348`); strike-and-restake works (`Growth/KingdomSocket.cs:439`) | **No S-tier fine house exists.** The cheapest luxury dwelling is `finehouse` at Plot M / MinStage Village, so a struck S hut cannot become fine-house ground |
| Fine houses held for notables rather than filled by the next settler | Luxury (implied) | MISSING | `Growth/KingdomLodging.cs` has no notable/priority/reservation concept; `ChooseIndex` fills **fewest-free-beds first** (`Growth/KingdomLodgingRules.cs:44-47`) | A fine house's low bed count makes it the *preferred* target for an ordinary settler |

## 8. Bindings

| Commitment | Source | Verdict | Evidence | What's missing |
|---|---|---|---|---|
| Output denominated in equilibrium contribution, not flow | Bindings | BUILT | `Carries` folded by `KingdomSubsidence.Supports` `Growth/KingdomSubsidence.cs:74-101` → `KingdomCatalogueRules.Equilibrium` `:273` | — |
| STANDARDS 7b: anything that stalls says why, once | Bindings | BUILT | STANDARDS.md:193; idiom `IdleWorksAnnounced` `Growth/KingdomGrowth.cs:454-461`, `NoLarderAnnounced` `Growth/KingdomPlot.cs:49`, `:194`; named refusals throughout | — |
| Protection law: nothing player-placed is ever cleared | Bindings | BUILT | `Growth/KingdomPlot2.cs:128-132`; plot sweep scoped to `PlotId` `Growth/KingdomSocket.cs:378` | — |
| **Extensibility: everything authorable from third-party XML** | Bindings; Add. 1 | **PARTIAL** | The load path is genuinely cross-mod: `Core/KingdomData.cs:127` `DataManager.YieldXMLStreamsWithRoot("KingdomBuildings")` — the engine's own stream enumerator, not a path read. Another mod's `KingdomBuildings.xml` **is** found and merged today. Same for `KingdomDeals` (`:145`) and `KingdomYardWorks` (`:161`) | **A mod cannot add a city style.** `<style>` is parsed into `KingdomData.Styles` (`Core/KingdomData.cs:414-422`) but that list is consumed only by the validator and a debug counter; a city's style is clamped to five hardcoded values (`Core/KingdomRules.cs:2119`, `Core/KingdomFounding.cs:250`). A mod may ship `<style Name="glass"/>` + `Styles="glass"` designs, pass validation, and have no city ever able to be `glass`. Also: `PlotSize` is a closed C# enum (`Growth/KingdomPlotRules.cs:36-49`) — no sixth size from XML |

## 9. The thirteen co-opted ideas

| Idea | Verdict | Evidence | What's missing |
|---|---|---|---|
| The posted price | BUILT | Charter 26 → `Quests/KingdomBounty.cs:166`; staked at the real heart `:359`, `:1141`; settlers decide on taste/virtue `Quests/KingdomBountyRules.cs:144-171`; **pays twice** — `StakeClearance` yields to the stockpile `Quests/KingdomBounty.cs:603`, drams separately `:799-825`. 109 tests | — |
| Worn ground | BUILT | Errands from real layout `Growth/KingdomRoads.cs:390-461`; ladder `Growth/KingdomRoadRules.cs:44-53`; paving Charter-reachable `Core/KingdomCharterPart.cs:157`; **paved in the wall material** `Growth/KingdomRoads.cs:723-751`, charged from stock `:773`. 167 tests | Routes are *inferred* from placed buildings, not sampled from tracked NPC footsteps |
| Yard trades | PARTIAL | Charter 22 → `Growth/KingdomYards.cs:399`, `:207-260`; S/M + free yard cell `:183`, `:128-170`; four works in `KingdomYardWorks.xml:22-30`. 40 tests | **`Shades` is dead.** Parsed, validated, capped, printed (`Growth/KingdomYardRules.cs:154-201`) — and read by nothing. Support tallies come only from a *building's* `Carries` (`Growth/KingdomSubsidence.cs:74-99`). A vine lattice's `food:1` never reaches equilibrium; nor `hiderack` `craft:1`, nor `vellumpress` `learning:1`. `Goods` is likewise unconsumed |
| The carry-sign | BUILT | Item `ObjectBlueprints.xml:782`; action `Experience/KingdomGuestbook.cs:734-759`; distance-scaled days `:429-444`; chronicled `:471`, `:595`; losable to the road `:589-598` | Brief says "crafted"; it is *purchased* (`PopulationTables.xml:37-42`, 20% into Tier1/2 wares). No recipe |
| Guests at the gate | BUILT | Logged between visits `Experience/KingdomGuestbook.cs:85-143`; three hook kinds `Experience/KingdomGuestRules.cs:28-97`; the letter `:255`; **rumor relocation** `:263-267` + test `DevTests/KingdomGuestRulesTests.cs:404`. 101 tests | The lodged notable's "trade" is prose only; mechanically an ordinary settler |
| Notable tastes | PARTIAL | Ten categories `Experience/KingdomCeremonyRules.cs:216-219`; deterministic draw `:306-345`; matched `:282-295`; chronicled `:358-371`; never phrased as a complaint `:348-355` | **"Met tastes shade equilibrium up" is not wired.** `Experience/KingdomCeremony.cs:208-209` computes `shade` and writes it **only to the debug log**. `Equilibrium(Water, Food, Roof, Lift)` (`Growth/KingdomCatalogueRules.cs:273`) has no shade argument and its one production caller passes only summed `Carries` |
| Leader traits | PARTIAL | One virtue + one flaw, deterministic, no reroll `Experience/KingdomCeremonyRules.cs:442-461`; chronicled `:474-479`; **do real work** in bounty decisions `Quests/KingdomBountyRules.cs:370-380`, `:744-748` | Same discard: `LeaderShade()`'s sole non-test call site is a log line at `Experience/KingdomCeremony.cs:197` |
| The surveyor's plan | PARTIAL | Real lookable object `ObjectBlueprints.xml:448-455`; the finished building's description written as intention `Experience/KingdomCeremony.cs:43-55` | **"The chronicle later quotes it" fires for 4 of 57 designs.** The quote only reaches the chronicle by riding the single-cell scaffold (`TransferPlanQuote` `Growth/KingdomPlanMarker.cs:207`). Any design with a `Plot=` attribute short-circuits at `Growth/KingdomPlanMarker.cs:194` into `StakeFromPlan`, which destroys the marker at `Growth/KingdomPlot2.cs:1113` without transferring the quote |
| Visible construction | PARTIAL | All five stages real and landing real world changes: `Staked/Cleared/Frame/Walls/Done` `Growth/KingdomPlotRules.cs:56-68`, applied `Growth/KingdomPlot2.cs:1586-1616` via `ClearGround`/`RaiseFrame`/`RaiseWalls`/`Finish`; honest lazy catch-up `:1557-1583`; announcements only when attended `:1610`; presence grants nothing | **"Crew standing at the plot while attended" does not exist.** No crew objects are created or moved. The only crew is the headcount integer `KingdomSystem.AssignedCrew` (`Core/KingdomSystem.cs:122`) |
| **The raising ceremony** | **PARTIAL — worst call-site gap in the mod** | Fully built and tested: gathers up to three named settlers `Experience/KingdomCeremony.cs:136-160`, shares water `:118`, chronicles those present `:120`, unattended defers to the homecoming `:125` | **`OnBuildingRaised` has exactly one caller: `Growth/KingdomScaffold.cs:252`, the single-cell scaffold.** The plot path — 53 of 57 designs — finishes at `Growth/KingdomPlot2.cs:1868-1874` with a bare `RecordDeed` + `KingdomChronicle.Record`. No house, work, larder or temple ever gathers a crew or shares water; only `palisade`/`rampart`/`watchtower`/`gatehouse` do |
| The pattern-book | BUILT | `Trade/KingdomTrade.cs` freezes the optional offer in `PrepareCharterDelivery` from exact `operation.SettlementId + operation.Sequence`, then `ContinuePatternBook` owns choice, stored-roster before/after CAS, idempotent chronicle, message disposition, quarantine recovery, and retirement ordering. `Trade/KingdomTradePatternRules.cs` bounds/detaches at most three exact keys/labels; `Trade/KingdomTradeState.cs` persists it in format 5/wire 4, reads exact wire 3, and preserves other/future wires opaque. `Experience/KingdomCeremony.cs` is now only the seated-roster/catalogue producer and frozen-offer UI; the unreachable tick-owned arrival path is gone. `DevTests/KingdomTradeRulesTests.cs` covers chance/no candidates, deterministic picks, decline, every mutation/sink reload cut, already-known/third-value CAS, bounds, immutable v3 golden, future opacity, seat/source wiring. **Never gates the base catalogue** — `pattern:` remains additive-only. | Native baseline + compatibility compile clean; ordinary in-game 55j playtest still required for presentation/feel |
| **The salvage commission** | **MISSING** | Neither `Growth/KingdomCommission.cs` (building commission) nor `Growth/KingdomSalvage.cs` (machine certification) dispatches anybody. Repo-wide grep for expedition/provision/dispatch/party returns nothing. No Charter entry among the 31; no `Options.xml` toggle | The whole idea: naming settlers, provisioning them, a destination the founder has personally seen, resolution into the homecoming report |
| **Pilgrims of the told story** | **MISSING** | The only artifact is a cosmetic guest blueprint `ObjectBlueprints.xml:47-49` at weight 25 in the ordinary traveller table (`PopulationTables.xml:19-22`). Guests spawn on a fixed interval onto a **random empty cell** (`Experience/KingdomLocus.cs:192-233`, `:291-333`) — not the heart | Nothing anywhere reads the outsider register's volume. `OutsiderEntries` is consumed only by reports, the chronicle and debug |

## 10. Addendum 1 — layers, sockets, footprints

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| Layer stack plot / building / skin, all three moddable | PARTIAL | Skins: BUILT (`Core/KingdomData.cs:397-411`); building layer: BUILT | Style layer is a dead end (see §8). `PlotSize` closed enum |
| Footprint belongs to the tier; footprint ≤ plot validator-checked and refused by name at upgrade | BUILT | `ValidateFootprint` at Fault severity `Growth/KingdomCatalogueRules.cs:759-788`; re-checked at the socket `Growth/KingdomSocketRules.cs:89` | — |
| Roof states `Open`/`Soft`/`Walled`/`Carved`; sky designs refuse walled by name; underground everything Carved | BUILT | `Growth/KingdomPlotRules.cs:129-131`, `:397-479`; `RefuseRoofSky` `Growth/KingdomPlot2.cs:1115` | — |
| Ceiling is a staking-time choice (stake big vs tight) | BUILT | `StakeableSizes` `Growth/KingdomPlot2.cs:1259`, `ForesightFor` `:1271`, offered at `Core/KingdomCharterPart.cs:490-493` | — |
| Yard = plot minus current footprint, recomputed per tier; one footprint per plot | BUILT | `KingdomPlots.YardRects`, consumed `Growth/KingdomYards.cs:128-170` | — |
| Merge-by-key: named override, omitted survive, skins append, same skin-key replaces, chains extend | BUILT | `Growth/KingdomMergeRules.cs:502-510`, `:115-120`, `:576`, `:567-574`; chain off the merged draft `Core/KingdomData.cs:262`; load order = mod order `Growth/KingdomMergeRules.cs:606-617` | — |
| **Guardrail: merges shape future commissions only; a standing building keeps its materialised state** | **PARTIAL** | The rules are complete and tested: `MergeReach` `Growth/KingdomMergeRules.cs:13-31`, `SpentAttributes`/`StampedAttributes` `:411`, `:414`, `Classify` `:422`, `Reconcile` `:651`, `StandingLine` `:699` | **Zero runtime callers.** `Reconcile`, `StandingWork`, `MergeOffer`, `StandingLine`, `Classify` are never constructed or called outside `DevTests/`. No as-raised draft snapshot is persisted anywhere. The 7b line at `:706` can never fire. **And a `Spent` attribute demonstrably leaks:** `OrderStrike` re-reads the *live* catalogue for a standing building (`Growth/KingdomMaterials.cs:1563-1567`, again `:1846`), so a mod re-pricing a design changes the strike effort and salvage of a hut that already stands. **`MODDING.md:568` — "it is `KingdomMergeRules` that enforces it" — is false as written** |
| The plot as socket: condemning keeps a re-buildable slot, keeping rect, lanes, door orientation | BUILT | `OnCleared` `Growth/KingdomSocket.cs:337` fires while the rect is still stamped; `LeaveSocket` `:409`; `RestakeOnRect` `:439` re-stakes the original rect unmodified; `BuildOnSocket` `:458` | — |
| Re-dress: any skin including one a mod added later, trivial cost, no I/O change | BUILT | Charter 28 → `Growth/KingdomSocket.cs:563`; reads the catalogue live `:590`; `RedressCostPercent = 10` `Growth/KingdomSocketRules.cs:170`; render-only `:618`; chronicled `:620` | — |

## 11. Addendum 2 — typed plots, (type × size) binding

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| Plot typed at staking; set key is (type × size); type maps to `Category` | BUILT | `Growth/KingdomSocketRules.cs:64` `ClassifyChange` keys on Category **and** PlotSize | — |
| Binding DECLARED on the record and validator-checked (better than SS2's positional) | BUILT | `Core/KingdomData.cs:371` → `KingdomCatalogueRules.Validate`; findings logged, Faults to `MetricsManager.LogError` `:372-382` | — |
| Footprint ≤ plot ENFORCED | BUILT | as §10 | — |
| Over-constrained/empty set REFUSES BY NAME (7b) | PARTIAL | Per-design refusals are named throughout (`Growth/KingdomZoning.cs:383-398`) | The **load-time** validator the addendum specifies — "every commissionable (type × size) reachable at any stage has ≥1 design, or the gap is named at load" — is implemented only as a **category-only, camp-only** check (`Growth/KingdomCatalogueRules.cs:648-659`). No size dimension, no per-stage sweep: a catalogue with no `housing` design at Large at Town produces no finding |
| Protection law stands (vs SS2's plan-swap scrapping) | BUILT | as §8 | — |
| Contents regenerate from tables (vs persistent per-stage item refs) | BUILT | `Furnish` `Growth/KingdomPlot2.cs:1880` | — |
| Two verbs: change the building (cheap) vs re-type the plot (full ceremony) | PARTIAL | Classified `Growth/KingdomSocketRules.cs:64`; **surfaced in the UI before commitment** — `[changed]`/`[re-typed]` tags `Growth/KingdomSocket.cs:736`, verb chosen in prose `Growth/KingdomSocketRules.cs:269`; one disclosed quote `:204-229` | **Identically priced.** `AssessConversion` `:242` takes no `ChangeKind`. Self-documented at `:198-202`: "Addendum 2 does not yet author a cheaper formula". The brief's cheap-vs-ceremony economics is not real |
| District zoning gates which plot TYPES may be laid | BUILT | `Districts=` on 21 entries; `Growth/KingdomZoningRules.Judge` | — |
| Time is labour, never maturation; labour duration real (tent 900 → 7200 ticks); scaffold carries nothing | BUILT | `Ticks=` on all 57 entries; guard test `DevTests/KingdomUpgradeRulesTests.cs:1118 NoTriggerPathReadsElapsedTimeAsACause` | — |

## 12. Addenda 3–8 — trigger law, QoL, closeness, conversion, reach, chain, time

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| Add. 3 — housing auto-trigger needs displacement-with-tolerance + materials + tech | BUILT | `CanDisplace` `Growth/KingdomUpgradeRules.cs:405`, `QuartersRefused` `:440` (routed through the QoL vocabulary per 4d), `CraftMet` `Growth/KingdomUpgrade.cs:641`, `CraftReaches` `:699-708` | — |
| Add. 3 — working buildings: reserve-covers-lost-output, else a 7b-legible OFFER, forceable from the Charter with the dip disclosed | BUILT | `HeldOffer` `Growth/KingdomUpgradeRules.cs:746`, `IsOffer` `:586`, `ForcedLine` `:619`; forcing prompt `Growth/KingdomUpgrade.cs:989` ("Raise it anyway, and go into the reserve") | — |
| Add. 4 — buildings declare `Provides` | BUILT | `Provides=` on 5 entries; `KingdomQol.ClearProvides` / `OfferOf` | Only five authored uses across 57 designs — thin content |
| Add. 4 — residents carry `Needs` (hard) and `Refuses` (hard-negative) | BUILT | `Core/KingdomQolRules.cs` `Judge`/`IsBlocked`; consumed by lodging and by the upgrade displacement check | — |
| **Add. 4 — `Prefers` (soft: tastes-style equilibrium shading)** | **MISSING as an effect** | `KingdomQol.PreferShade` is summed into the same discarded `shade` at `Experience/KingdomCeremony.cs:208-209` | The soft half of the vocabulary changes nothing. Same root cause as notable tastes and leader traits |
| Add. 4 — derive before authoring, from vanilla parts/tags | BUILT | `Core/KingdomQol.cs:145-152` reads real vanilla truth: `Robot`, `Brain.Aquatic`, `LiveFungus`, `PhotosyntheticSkin`, `Inorganic`, `Stomach`. A modded species is a correct resident before its author writes a tag | — |
| Add. 4 — authored `r_TAF_*` tags refine | BUILT | `Core/KingdomQol.cs:180-205` `Overlay` | — |
| Add. 4b — no acceptable home, no joining (assignment-level, not a bed tally) | BUILT | `HasRoomToHouse` is **gone** from the settler arrival gate; it survives only in a report line (`Core/KingdomReports.cs:151`) and the guest path (`Experience/KingdomGuestbook.cs:301`), which 4b explicitly leaves unchanged | — |
| Add. 4b — losing all acceptable housing → announced once by name, grace, then emigration in both registers | BUILT | `BrinkKind.Roof` `Core/KingdomBrinkRules.cs:8-22`; lodging brink path; condemned-roof recording (see Add. 10b) | — |
| Add. 4c — closeness ladder Packed/Close/Roomed/Private, derived from beds-per-footprint, `Closeness` overrides | BUILT | `Growth/KingdomLodgingRules.cs:208-219`, thresholds `:227-237`, derivation `:249-266`; `Closeness=` used on 2 entries | — |
| Add. 4d — the −100 fault lines refuse ANY shared roof at every tier including Private | BUILT | `Growth/KingdomLodgingRules.cs:169-195` — the old flat floor is documented as *promoted* to the Private rung; Roomed and Private both refuse the flat fault lines, parting company at 75 | — |
| Add. 4c/9 — the superseded `CohabitHostility` / `JudgeCohabitation` surfaces are retired | **OPEN (Add. 9 backlog)** | Both still present and still tested: `Core/KingdomQolRules.cs:637`, `:656`; `DevTests/KingdomQolRulesTests.cs:580` | Also still present: `TryAddSkin` (`Core/KingdomRules.cs:1990`) and the flat `MaxBuildings = 40` (`Core/KingdomRules.cs:51`) — all four named in Addendum 9's pre-release list |
| Add. 5 — five conversion channels (osmosis, shrine, education, culture, diplomacy) | BUILT | All five live, one path (`KingdomConversion.Convert` `Core/KingdomConversion.cs:312`): osmosis counted in shared-living days and scaled by closeness with **no conversion across a refusal** `:435-496`, `Core/KingdomConversionRules.cs:275-280`; shrine staffed + quarter-scoped + neutral-only `Experience/KingdomFaith.cs:160-212`, consecration Charter-reachable and chronicled `:465`, `:522-529`; education *softens* one band `Experience/KingdomFaithRules.cs:288-291`; shared meals capped at 50% `Core/KingdomConversion.cs:206-247`; water rite invited/consented/one-at-a-time `Experience/KingdomWaterRite.cs:168-239` | Nit: osmosis reads raw `KingdomLodging.QuartersOf` (`Core/KingdomConversion.cs:476`), not `KingdomFaith.EducatedCloseness` — so a staffed scriptorium softens the housing-assignment grudge but not the osmosis rate, despite `Experience/KingdomFaith.cs:401-403` claiming it serves both ladders |
| Add. 6 — reach derived from size × tier (S plot / M quarter / L zone / XL city-or-realm), `Reach` overrides, inherited by modded designs | BUILT | `ReachBand` `Growth/KingdomReachRules.cs:10-19`; `BandForSize` `:121-132`; tier shift to `Realm` `:147-155`; `Reach=` override parsed `:180` | — |
| **Add. 6 — binding needs stay citywide, lifts become reach-scoped so quarters gain real character** | **PARTIAL** | The rule is written: `ScopedByReach(Kind) => !IsBindingSupport(Kind)` `Growth/KingdomReachRules.cs:405-408`; quarter geometry is measured, not hand-waved `:287-303`, `:324`, `:377`; `KingdomReach.CityShadeExcept` `Growth/KingdomReach.cs:701-726` ~~is the only true cross-zone aggregate in the codebase~~ **is joined by a second one, landed 2026-08-21 (Wave C):** `KingdomSubsidence.OtherZones` / `KingdomSubsidenceRules.CityTally` / `CityStorage` fold every other claimed zone's binding carries and storage in **as last seen** | **Four of the five lift kinds are still scoped and then unread — unchanged by Wave C.** The only production consumers of the reach layer remain the shrine's quarter (`Experience/KingdomFaith.cs:191`), education (`:447`), and one chronicle line. `order`, `luxury`, `spirit` and `craft` are never applied to any resident. What Wave C fixed is the OTHER half of this row's claim: the three BINDING goods and storage now sum honestly across a multi-zone city instead of being overwritten by whichever zone was last visited (see §15) — the reach-scoped LIFT gap this row names is a separate, still-open seam (Wave A's own scope) |
| Add. 6 — XL special functions unlock through the office machinery; worker quality derives from who the settler is | BUILT | `KingdomMaterials.HeadedProbe = KingdomReach.IsHeaded` installed unconditionally at `Experience/KingdomOffices.cs:46` | — |
| Add. 7 — raw → refined → spent, via staffed sawyer's/mason's/smelter yards | BUILT | `Refines=` declares a yard (3 entries); `WorkYard` `Growth/KingdomMaterials.cs:1697-1802` takes raw at `RawPerRefined = 2` and puts refined `:1765-1774`; driven from the settlement pass `Growth/KingdomGrowth.cs:166` | `MaxRefinedPerDay = 8` (`Growth/KingdomMaterialRules.cs:1009`) remains untuned against grand-build costs, and the refined chain is still unmodellable in `_notes/balance-sim.py` (water economy only) |
| Add. 7 — infrastructure gates construction: L needs the yard standing and staffed, XL needs it headed | BUILT | `AllowsBuild` `Growth/KingdomMaterialRules.cs:1793-1830` with three distinct named refusals; `RequiresYard` `:1676`, `RequiresHeadedYard` `:1683`; which yard derived from the cost `:1700-1719`; checked before materials `Growth/KingdomMaterials.cs:1072` | — |
| Add. 7 — bits price high-craft builds and certified-tech repair | BUILT | Read off vanilla `TinkerItem` `Growth/KingdomMaterials.cs:104-110`; spent `:711-778`; **repair path real** `Growth/KingdomWear.cs:593-602` | Content is two entries (`waterworks`, `chargingpost`) |
| Add. 7 — exotic materials for XL specials | PARTIAL | `KingdomExotic` `Growth/KingdomMaterialRules.cs:72`; vanilla blueprints only, never made `Growth/KingdomMaterials.cs:154-166`; open extension tag `r_KingdomExotic` `:101`; bespoke refusal `:1110-1117` | **No size rule ties exotics to XL.** Unlike `RequiresYard`/`RequiresHeadedYard` (`Growth/KingdomMaterialRules.cs:1676`, `:1683`), there is no `RequiresExotics(PlotSize)` — `CanPay` reads whatever the design declared, at any size. And **one authored use in the whole catalogue**: `waterworks`. The other two XL designs (`court`, `homefarm`) cost nothing rare |
| **Add. 7 — crews have capability derived from settler stats** | **PARTIAL** | The mechanic is wired end to end and is genuinely derive-first: `CapabilityOf` reads real `Strength`/`Intelligence` `Growth/KingdomCrews.cs:138-148`; two kinds per the ruling `Growth/KingdomCrewRules.cs:36-46`; robot floor `:87-96`; shortfall slows, never stalls, floored at 25 `:74`; announced once `Growth/KingdomGrowth.cs:421-428`; ablest-first assignment `:396-397` | **Inert in shipped play: `grep -c "CrewNeeds=" KingdomBuildings.xml KingdomYardWorks.xml` → 0, 0.** With `CapabilityThreshold == 0` everywhere, `CapabilityEffectiveness` is always 100 and the shortfall line (guarded on `> 0` at `Growth/KingdomGrowth.cs:420`) can never fire. Reachable only by a mod that authors the attribute |
| Add. 7 — wear from EVENTS only; damaged works run reduced, named once, mendable | BUILT | `WearCause` (raid / hard running / temperamental tech) `Growth/KingdomWearRules.cs:66`; repair `Growth/KingdomWear.cs:586-602` | — |
| Add. 8 / BACKLOG — supply-carried level subsides in absence toward what infrastructure supports; bounded, chronicled, arrestable | BUILT | `Growth/KingdomSubsidence.cs` + `KingdomSubsidenceRules.cs`; `StageWithHysteresis` replaces the ratchet (both directions, Camp an absolute floor); breakpoints sampled into dated chronicle entries `Growth/KingdomSubsidence.cs:288`, `:375-387` | Note: `_notes/COORDINATION.md:177` still says "Not implemented: `UpdateStage` still only ratchets up" — **stale**, superseded by the landed wave |
| Add. 8 — prose swept to the doctrine | BUILT | `VISION.md:77-99` carries the pushed-awareness doctrine verbatim; `TESTING.md:452-461` restated; `MaxUpkeepDaysCharged` retired (`Core/KingdomRules.cs:260` marks where it lived) | — |

## 13. Addendum 10 — brink moderation, typed wear, ruins

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| 10a — warning reaches the player wherever they are; names the arrest action; window in world-days; fires in absence; arrest lifts it | BUILT | `Core/KingdomBrink.cs` + `KingdomBrinkRules.cs`; three brink kinds `:8-22`; `WindowDays`/`ExpiryTick`/`CrossingTick`; conversion brinks `Core/KingdomConversion.cs:523-590` | — |
| 10b — wear reduces **every** work's level contribution, staffed or not | BUILT — **the flagged defect is fixed** | `KingdomSubsidence.Supports` now folds `KingdomWear.EffectivenessOf(work)` unconditionally (`Growth/KingdomSubsidence.cs:99`). The staffed-only ternary the change map lists as open is gone | The `_notes/CLOCK-REWORK-CHANGE-MAP.md` "After P4" entry for this is stale |
| 10b — kind-appropriate damage: storage works leak, power works lose output | BUILT | `LeakKind { Water, Charge }` `Growth/KingdomWearRules.cs:389-393`; dispatch `Growth/KingdomWear.cs:655-670`; `Survey.LeakFrom` `Growth/KingdomSurvey.cs:290-314` | Food spoilage is deliberately absent pending food-as-flow — stated in code at `Growth/KingdomWearRules.cs:385-388` |
| **10c — collapse leaves ruins in stages: name, description AND appearance reflect the stage; ruination varies; mendable, salvageable, never auto-cleared** | **PARTIAL** | Name: `Growth/KingdomWear.cs:95-103` adds `ConditionAdjective` (battered / half-ruined / ruined), so a named building keeps its name. Description: `:108-121` → `ConditionLook` `Growth/KingdomMaterialRules.cs:1949-1959`. Varies across works: `RollRuin` `Growth/KingdomSubsidenceRules.cs:610-617`, chance scales with the rung lost `:446-488`. Damage never deletion `:442`. Mendable even when staffless `Growth/KingdomWear.cs:252-263`; salvageable at 50%. Condemned homes stop being roofs on the day it happened `Growth/KingdomSubsidence.cs:365-374` | Two pieces. **(1) Appearance never changes** — `r_KingdomWear` subscribes to exactly two events (`GetShortDescriptionEvent`, `GetDisplayNameEvent`, `Growth/KingdomWear.cs:78`); no `Render`/`ColorString`/`DetailColor`/`Tile` write exists in the wear, subsidence or material-rules files. A ruined granary renders identically to a sound one. **(2) The staged ruin ladder for a *collapsed settlement* is dead code**: `InheritedState { Held, Faded, Abandoned, Ruins }` + `ResolveInheritedState` (`Core/KingdomRules.cs:1299-1311`, `:1472`) have **no production caller** — every reference is `DevTests/KingdomRulesTests.cs:684-931`, plus `Growth/KingdomSubsidenceRules.cs:509` which borrows only the `StandingPercent` *number* and never calls the resolver. A city that actually falls produces wear on standing works and nothing else; `Abandoned`/`Faded`/`Held` are unreachable at runtime. **(3)** "Rebuilding on a ruined plot is the mend lane, not a fresh-ground stamp" holds only incidentally — the ruined work is never removed so the plot stays occupied; no explicit occupancy/ruin check exists in the commission or plot paths |
| Add. 9 — pre-release engineering pass | **OPEN** | Four superseded surfaces still present (above); the XML schema is not explicitly versioned; no save-migration harness | Explicitly deferred by the author to "before any public release" — listed here so it is not lost |

## 14. The food flow — the largest single gap

Split out because it is one hole with many faces, and because the brief's own Addendum 10(b)
predicted it ("food spoilage waits until food is a flow").

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| Food is a physically stored stock with a dedicated mark | BUILT | Real `GameObject`s in real inventories: survey counts `KingdomLarder == 1` containers and their `Food`/`PreparedCookingIngredient` items `Growth/KingdomSurvey.cs:128-138`; `MaxDedicatedLarders = 8` `Core/KingdomRules.cs:107`; dedication Charter-reachable (option 11) | — |
| Fields fill the larder | **PARTIAL** | Only the kitchen garden deposits: `KingdomPlot.Deposit` `Growth/KingdomPlot.cs:261-281`, on the settlement pass `:137-157`. Crop blueprint by style `Growth/KingdomCropRules.cs:91-106` | **`field`, `fieldrows`, `granary`, `grange`, `homefarm` produce nothing.** None carries the `r_KingdomPlot` part (`ObjectBlueprints.xml:291`, `:298`, `:305`, `:315`, `:321`). Their `food:8/18/9/26/40` figures are capacity only. `r_KingdomGranary` is a real `Chest` but the larder auto-mark keys on the blueprint string `r_KingdomLarder` (`Growth/KingdomScaffold.cs:182`, `:216-225`), so a commissioned granary is never automatically a larder |
| **Food is drawn down by the settlement living** | **MISSING** | `ConsumeFood` (`Growth/KingdomSurvey.cs:239-278`) has exactly **one** non-test caller: the founder-called shared meal (`Growth/KingdomLarder.cs:53`), whose cost is a tier lookup on the stock itself (`Core/KingdomRules.cs:208-212`) — `Population` appears nowhere in it | Water's counterpart exists and is the whole difference: `ResolveHeartbeat` bills `PolicyUpkeepForElapsed(Population, elapsed, …)` then `Survey.Consume(upkeep)` (`Growth/KingdomGrowth.cs:214-215`). There is no `RationsPerDay`, no `ResolveHunger`, no `HungerStreak`, no `LastFoodWorkTick`, no `StoreFood`, no `SpoilFrom`, no food in trade/manifest/raids |
| **Food works produce on the clock** | **MISSING** | `SupportTally.Water` is read in three places — twice as arithmetic and once as real production, `Growth/KingdomGrowth.cs:85` `survey.Store(Supports(survey).Water * madeDays)`. `SupportTally.Food` is read in only the two arithmetic places (`Growth/KingdomSubsidenceRules.cs:101`, `:113`) | **One missing line is the whole gap:** `survey.StoreFood(Supports(survey).Food * madeDays)` |
| Spoilage / famine / food hauling | MISSING | `LeakKind` has two members and can never return food (`Growth/KingdomWearRules.cs:389-393`); zero food references in `Trade/`, `Quests/`, `Raids/`, `KingdomDeals.xml` | Deliberate per Addendum 10(b), and recorded as a known gap at `TESTING.md:462-465` |

*Correction to the record:* `TESTING.md:462` says "nothing fills a larder from a field" — stale. The
kitchen garden does. What is true is that nothing **empties** one except a shared meal, and that the
whole upper food lane deposits nothing.

## 15. Cross-zone growth — a second single-root gap

| Commitment | Verdict | Evidence | What's missing |
|---|---|---|---|
| Plots and growth work in any claimed zone, not just the seat | BUILT (rules) / unreachable (in play) | Growth gates on `ClaimedZones.Contains(Z.ZoneID)` (`Growth/KingdomGrowth.cs:26`), as do commission (`Growth/KingdomCommission.cs:43`), plots, yards, sockets, faith, locus. Districts are a genuine per-zone map (`Core/KingdomSettlement.cs:270`) | Nothing to exercise it — one claimed zone per city (see §5) |
| Walls move outward as the city spans zones | PARTIAL | Wall siting is claim-aware and recomputed on every placement: `FrontierEdges(ZoneID, ClaimedZones)` `Core/KingdomRules.cs:2544-2578` → `IsOnFrontier` `:2589-2604`; claiming a neighbour genuinely stops that edge being frontier (test `DevTests/WallRulesTests.cs:106-120`) | **No wall geometry object.** A wall is N independent single-cell works placed randomly along a fixed 2-cell edge band; nothing recomputes, extends, connects or relocates a line. With one claimed zone the behaviour never fires |
| Multi-zone city lifecycle | PARTIAL | Seat swaps on `ZoneActivatedEvent` (`Core/KingdomSystem.cs:947`, `:563-573`); realm-vs-city field partition is reflection-checked (`Core/KingdomSettlement.cs:395`, `:429`, `SeatMismatches` `:466`) | **Aggregation defect:** settlement-wide numbers are recomputed from **one zone's** survey. `KingdomSubsidence.Reckon` writes `System.SupportedLevel`/`SubsidenceBinding` from `Supports(Survey)` for the visited zone (`Growth/KingdomSubsidence.cs:118-123`, `:204-205`); `UpdateStage` compares `Population` against that zone's `StorageCapacity` (`Growth/KingdomGrowth.cs:705-712`). In a two-zone city, walking from the granary zone into the mine zone would overwrite the city's supported level with the mine's figures |
| `MinZones` designs | BUILT (rules) / dead content | 8 entries gated `MinZones="2".."4"`, enforced `Growth/KingdomZoningRules.cs:685` against `ClaimedZones.Count` | Unreachable: `reservoir`, `waterworks`, `grange`, `homefarm`, `bazaar`, `bathhouse`, `temple`, `scriptorium` can never be commissioned in normal play |
| **Theme dissent between cities of one kingdom** | **MISSING** | Each city genuinely carries its own style, resolved from its own ground (`Core/KingdomSettlement.cs:75`; second city `Core/KingdomFounding.cs:170`), and style drives offered designs and wall material. But dissent has exactly **one** axis: creed, read from the vanilla faction feeling table (`Core/KingdomCreed.cs:299-301`, `:147`). Brink kinds are closed at Roof/Creed/City (`Core/KingdomBrinkRules.cs:8-22`) | A `verdant` city and an `eater` city with the same creed have zero friction |

## 16. Deferred / snoozed items found in `_notes`

| Item | Where | Status |
|---|---|---|
| Bounty manning's mixed PASS/RAW denominator | `CLOCK-REWORK-CHANGE-MAP.md` "After P4" | Open; low risk; fold into the next change touching `Quests/KingdomBounty.cs` |
| Staffless works never wear-reduce their contribution | same | **Closed** — verified fixed at `Growth/KingdomSubsidence.cs:99`. Note is stale |
| Food `Carries` are not a flow | same | Open — §14 |
| Uncapped mending can finish a large repair in one long-absence resolve | same | Open; bounded by the one-mending-settlement-wide gate; unmodelled |
| `MaxRefinedPerDay = 8` untuned; refined chain unmodellable in `balance-sim.py` | same | Open |
| Chronicle budget: a City→Camp collapse writes ~58 of the 200-entry cap | `CLOCK-REWORK-CHANGE-MAP.md` P4 additions | Open — consider coarser sampling for long slides |
| Food has no L or XL design | same | **Closed** as catalogue content (`grange`, `homefarm` exist) but they produce nothing — see §14 |
| Growth schema-5/6 admission machinery | `PASS-1-BUILD-PLAN.md` standing decisions | **Parked, not cancelled** — traced to have no dependency from the shipped growth loop |
| Withered overlay | `TESTING.md:451` | Designed, not built |
| Founder's basin acquisition quest | `TESTING.md:466` | Wish-obtainable only; slice 0.2 content |
| No ownership stamping on claims | `TESTING.md:446` | Membership design pending |
| Charging post `Capacitor` armed-or-inert | `COORDINATION.md` open questions | Open question to the other agent. *Note:* the blueprint now carries `MinimumChargeToExplode="0"` (`ObjectBlueprints.xml:513`) with a comment intending "never arm it" — worth confirming the engine reads 0 as never rather than always |
| Charging post animation | same | Open, cosmetic |
| Deploy hygiene: `build.ps1` compiles a different file set than the live mod folder ships | same | Answered; requires a reviewed staging manifest before release |
| `IDEA-INBOX.md` open rulings 1–7 | `IDEA-INBOX.md:561` | Author-facing rulings, not build commitments |

---

# Proposed wave plan

Four waves. File ownership is disjoint within each wave. Ordered by player impact per unit of risk.
Wave A is deliberately first because it is almost entirely **call-site joining and one-line
plumbing on machinery that already exists and is already tested** — the highest ratio of felt
change to new surface in the whole map.

## Wave A — "the numbers that are thrown away, and the ceremonies nobody attends"

*Cheap adjacencies of existing systems. Nothing new is designed; five things already built are
connected to the thing they were built for.* **≈5 files of new surface.**

1. **Route the plot completion through the raising ceremony.** `Growth/KingdomPlot2.cs:1868-1874`
   currently writes a bare deed + chronicle line; call `KingdomCeremony.OnBuildingRaised` instead.
   This alone takes the raising ceremony from 4 of 57 designs to all 57.
2. **Transfer the surveyor's plan quote on the plot path.** Add `KingdomCeremony.TransferPlanQuote`
   before the marker is destroyed at `Growth/KingdomPlot2.cs:1113`, mirroring
   `Growth/KingdomPlanMarker.cs:207`. With (1), the chronicle can finally quote a plan for a house.
3. **Apply the shade — one seam closes four commitments.** `Experience/KingdomCeremony.cs:197`,
   `:208-209` compute taste, leader-trait and `Prefers` shades and log them. Give
   `KingdomCatalogueRules.Equilibrium` a shade input (or a sibling applied at
   `Growth/KingdomSubsidenceRules.cs:97-101`) and persist the per-settlement shade. This turns
   notable tastes, leader traits and Addendum 4's `Prefers` from prose into mechanics at once.
4. **Feed yard-trade `Shades` into the same summation.** `Growth/KingdomYardRules.cs:56` is parsed,
   capped and printed but never read; `KingdomSubsidence.Supports` only walks `Survey.Built`.
5. **Give the reach-scoped lifts a consumer.** `ScopedByReach` already excludes the binding goods
   (`Growth/KingdomReachRules.cs:405-408`), but `Growth/KingdomSubsidence.cs:117` sums lifts
   citywide with no reach term, so `order`, `luxury`, `spirit` and `craft` are scoped and
   discarded. This is the same seam as (3) and should land in the same change — it is what makes
   Addendum 6's "quarters gain real character" true.
6. **Retire the orphan material counter.** Point `Growth/KingdomPlot2.cs:1677-1697` at
   `KingdomMaterials`'s real stockpile so carved yields stop vanishing, and delete the dead
   `MaterialsHeld` reader.
7. **Author `CrewNeeds` on the designs that want hands** (catalogue-only, zero code): the whole
   crew-capability mechanic is wired, tested and inert because no design declares a threshold.

*Files:* `Growth/KingdomPlot2.cs`, `Experience/KingdomCeremony.cs`, `Growth/KingdomCatalogueRules.cs`
(+ `KingdomSubsidenceRules.cs`/`KingdomSubsidence.cs` summation only), `Growth/KingdomYards.cs`,
`Core/KingdomSettlement.cs` (one carried field), `KingdomBuildings.xml`.
**Balance re-run: yes** — (3), (4), (5) and (7) all move equilibrium.

## Wave B — "food becomes physical"

*A real new system, but one with a complete, working template beside it: every function needed
already exists on the water side and can be mirrored.* **≈6 files.**

1. **Production.** Add `KingdomSurvey.StoreFood(int)` mirroring `Store` (`:319`), and the one
   missing line in the settlement pass: `survey.StoreFood(Supports(survey).Food * madeDays)`,
   clocked on a new `LastFoodWorkTick` exactly as `LastWaterWorkTick` is.
2. **The upper lane actually grows food.** Either give `field`/`fieldrows`/`grange`/`homefarm` the
   deposit part, or let (1) subsume them; and make a commissioned `granary` auto-mark as a larder
   (the blueprint-string check at `Growth/KingdomScaffold.cs:182` is the whole bug).
3. **Consumption.** `RationsPerDay(Population, Stage)` and a `ResolveHunger` beside
   `ResolveHeartbeat`, with a hunger streak and an emigration ladder mirroring thirst — bounded by
   the same floor, chronicled the same way.
4. **Spoilage.** Add `LeakKind.Food` and a `SpoilFrom`, closing Addendum 10(b)'s explicit deferral.
5. **Food in trade.** The manifest and charter paths carry drams only; food is the obvious second
   cargo and needs no new mechanism.
6. **Re-run the balance model**, which today greps clean for `refin|timber|stone|scrap|material` and
   models the water economy only.

*Files:* `Growth/KingdomSurvey.cs`, `Growth/KingdomGrowth.cs`, `Core/KingdomRules.cs`,
`Growth/KingdomWear.cs` + `KingdomWearRules.cs`, `Trade/KingdomTrade.cs`, `_notes/balance-sim.py`.
**Balance re-run: yes, mandatory** — food becomes a binding good that can actually bind.

## Wave C — "the city spans ground"

*One missing player verb unlocks five gaps at once, then two real fixes behind it.* **≈5 files.**

1. **A claim action on the Charter.** This is the single highest-leverage missing verb in the mod:
   it makes `MinZones` content reachable (8 designs), makes frontier walls move, makes districts
   mean something per-zone, and makes vertical claims (cellars, towers) playable rather than
   founding-only. The rules are already written and tested — only the action is absent.
2. **Fix the aggregation defect** exposed by (1): `SupportedLevel`, `SubsidenceBinding` and the
   stage reading must sum across `ClaimedZones` rather than being overwritten by the last-visited
   zone. `KingdomReach.CityShadeExcept` (`Growth/KingdomReach.cs:701-726`) is the existing pattern.
3. **Underground correctness:** stop `RoofOnGround` forcing `Open` designs to `Carved`
   (`Growth/KingdomPlotRules.cs:448-451`), and add a per-stratum catalogue filter so depth narrows
   the offer at commission time rather than only at founding.
4. **Site the gatehouse.** Join the `gatehouse` design to `KingdomRoadRules.TryGate`'s cell — a
   placement rule, as the brief says, not a size.

*Files:* `Core/KingdomCharterPart.cs`, `Core/KingdomFounding.cs`, `Growth/KingdomSubsidence.cs`,
`Growth/KingdomPlotRules.cs`, `Growth/KingdomCommission.cs`. **Balance re-run: yes** — (2) changes
what a city is measured as.

## Wave D — "the luxury lane, and the two unbuilt ideas"

*Genuinely new content and one new subsystem; lowest urgency because nothing is currently broken,
only unbuilt.* **≈6 files.**

1. **Make housing quality the gate it claims to be.** `BestHousingTier`
   (`Experience/KingdomGuestbook.cs:369-395`) classifies by plot area; have it read the design's
   `luxury` carry. Add per-house vacancy rather than settlement-wide `Population < Beds`, and a
   reservation so a fine house is not filled by the next ordinary settler
   (`Growth/KingdomLodgingRules.cs:44-47` currently prefers it).
2. **Conjoin the shop tier.** `ShopTier` exists and is live; read it in the settling gate.
3. **An S-tier fine house** so "struck economy-huts become fine-house ground" is buildable, and a
   `Knowledge="machine:…"`-gated design so certification has a destination — both catalogue-only.
4. **Office holders want quarters** — a housing precondition beside `RosterNames[0]`.
5. **The salvage commission** (a real new subsystem: named settlers, provisions, a seen
   destination, resolution into the homecoming) and **pilgrims of the told story** (cheap: gate the
   existing guest arrival on `OutsiderEntries` volume and place them at the heart rather than a
   random cell).
6. **Price the two socket verbs differently** (`Growth/KingdomSocketRules.cs:242` takes no
   `ChangeKind`), and **wire the merge guardrail** — persist an as-raised draft snapshot and call
   `KingdomMergeRules.Reconcile`, or amend `MODDING.md:568`, which currently claims an enforcement
   that does not exist.
7. **Finish the ruin ladder (Addendum 10c).** Give `r_KingdomWear` a render handler so a ruin
   *looks* ruined — it currently changes only name and description — and either give
   `KingdomRules.ResolveInheritedState` a production caller so a fallen city gets a terminal
   staged-ruin state, or mark the enum superseded per STANDARDS §9. Today it is tested dead code
   and `Abandoned`/`Faded`/`Held` are unreachable at runtime.
8. **Tie exotics to XL** — add a `RequiresExotics(PlotSize)` beside `RequiresYard`, and give the
   other two XL designs something rare to be finished in.

*Files:* `Experience/KingdomGuestbook.cs`, `Growth/KingdomLodging.cs` + `KingdomLodgingRules.cs`,
`Experience/KingdomOffices.cs`, `Growth/KingdomWear.cs`, `KingdomBuildings.xml`, one new
commission file. **Balance re-run: no** (except the catalogue additions in (3) and (8)).

## Not in a wave — carry forward

- **Theme dissent between cities** (§15) is a real design question, not an implementation gap: the
  brink taxonomy is deliberately closed at three kinds. It needs an author ruling on whether style
  divergence is a fourth axis before anyone builds it.
- **Mod-declared city styles** (§8) is the one place the extensibility promise is materially false.
  Cheap to fix (`IsKnownStyle` reads `KingdomData.Styles` instead of the hardcoded five) but the
  wall-material and crop `switch`es would need a default path for an unknown style.
- **Addendum 9's pre-release engineering pass** — retire `CohabitHostility`, `JudgeCohabitation`,
  `TryAddSkin`, `MaxBuildings`; version the XML schema; build the migration harness. Author has
  explicitly deferred this to before first release; it belongs in a release-prep wave, not here.

## Cheap adjacency vs real new system

| Cheap adjacency (existing machinery, missing seam) | Real new system |
|---|---|
| Raising ceremony on the plot path (A1) | Food consumption + famine ladder (B3) |
| Plan quote transfer (A2) | The salvage commission (D5) |
| Applying the computed shades and reach lifts (A3–A5) | A claim action and its aggregation fix (C1, C2) |
| Orphan material counter (A6) | Per-stratum catalogue (C3) |
| Authoring `CrewNeeds` — XML only (A7) | Reservation/vacancy in lodging (D1) |
| Granary auto-mark (B2) | Merge guardrail persistence (D6) |
| Gatehouse siting (C4) | A terminal collapse state for a fallen city (D7) |
| Pilgrims via the existing guest pass (D5b) | |
| S-tier fine house, machine-gated design, exotics rule (D3, D8) | |
| Ruin render handler (D7) | |


## Orchestrator log, post-Wave-C (2026-08-21)

Wave A landed (29f946e). Wave C landed on the working tree; WR-2 (dated sighting clause in the
status report) applied by the orchestrator. Outstanding at B's integration: WR-1 (UpdateStage
must read city storage AFTER Reckon — real bug until applied: stage can demote a multi-zone city
on the settling pass), WR-4 (sim re-pin: cross-zone summing + 8 MinZones designs reachable),
WR-5 (doc surface for C). WR-3 (authored `Strata="surface|deep|any"` + `Sited="gate"` attributes,
retiring `KingdomRoadRules.GatehouseKey`) → Wave D. Also for D, from C's flags: underground-only
designs (needs WR-3), fungal crops by depth (KingdomCropRules keys off Style), natural
chokepoints (real new system — needs an author decision on scope).

## Orchestrator log, doc pass for Wave C (2026-08-21)

WR-1 confirmed applied: `KingdomGrowth.UpdateStage` (`Growth/KingdomGrowth.cs:947-956`) now reads
`KingdomSubsidence.CityStorageCapacity` after `Reckon` runs, so the stage ladder is measured
against the whole city's storage rather than the last-visited zone's. WR-5 (doc surface for
Wave C) closed by this pass: `docs/API.md`, `MODDING.md`, `TESTING.md` and `CHANGELOG.md` now
carry the claim action, the per-stratum commission gate, the cross-zone memory (`ZoneSighting` /
`CityTally` / `CityStorage` / `SightingClause`), the `RoofOnGround` fix, and gatehouse siting.
Wave B's food-flow doc surface was written separately and is untouched here. §5, §6 and the
Add. 6 row of §12 above are updated to reflect Wave C landing; **§14 (food) and §15 (cross-zone
growth) still carry pre-Wave-B/-C verdicts and want their own pass** — §15 in particular is the
row most directly overtaken by (2) above (multi-zone aggregation) and by the claim action, and
was left alone here to keep this pass to its assigned scope. WR-4 (sim re-pin) and WR-3 (authored
`Strata`/`Sited` attributes, Wave D) remain outstanding.
