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
| `GrowthStage Stage` | `Camp`, `Steading`, `Village`, `Town`, `City`. Monotonic — never regresses. |
| `int Population` | Living settler count. |
| `bool Withered` | True while a sustained thirst has suspended prosperity. Recoverable. |
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
| `static bool ClaimZone(Zone z, bool force = false)` | Claims a zone; requires adjacency to existing ground unless forced. |
| `static bool EnrollCitizen(GameObject citizen)` | Makes a creature a citizen. Enrolled creatures are protected from kingdom-driven removal. |
| `static SecondFoundingVerdict JudgeSite(KingdomSystem, Zone)` | What the rite would do on this ground. |
| `static bool FoundSecond(string name, string vocation, Zone site, bool force = false)` | Founds the realm's second city. `force` waives only the not-adjacent requirement. |
| `static string StyleGroundClause(string style)` | Lower-case founder-facing clause naming what the ground promises for a city style ("common ground", "ground green enough to root a verdant city"). Presentation only — `KingdomRules.StyleForSite` owns which style a site resolves to. |

## `KingdomLarder` — dedicated food, and what the settlement does with it

| Member | Contract |
|---|---|
| `static bool HoldSharedMeal(KingdomSystem, Zone, out string failure)` | Spends food from dedicated larders only and records the meal. Returns false with a reason when the larder cannot feed one; nothing is spent on failure. |

Food is counted from containers carrying the `KingdomLarder` int property, which the Charter's
dedication flow sets. Dedication is a mark, not a transfer: nothing is moved, and an undedicated
container — including the player's own pack — is never read or spent. An empty larder costs the
settlement nothing, by design: every food effect is a bonus for engaging, never a penalty for
abstaining.

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

## `KingdomRules` — pure rules (no engine dependencies)

Deterministic, side-effect-free, and fully unit-tested; safe to call from anywhere,
including your own tests. Notable members: `SpilloverDelta`, `UpkeepForElapsed`,
`StageFor`, `FetchableDrams`, `ResolveThirst`, `RaidSize`, `StyleAllows`, `DistrictName`,
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
| `KingdomBuilt` (int) | Object was raised by a commission. |
| `KingdomRaider` (int) | Hostile spawned by a raid. |
| `KingdomCaravan` (int) | Merchant spawned by a trade charter; despawned on later visits. |
| `KingdomOrigin` (string) | Settler's region of origin. |

## Guarantees

- **The protection law**: kingdom systems never consume, move, or destroy an object the
  player or another mod placed, unless the player explicitly dedicated it. Automatic
  placement only ever targets empty cells.
- **Absence is safe**: growth, thirst, trade, and raids only resolve while the player is
  present, and consequences are bounded per visit.
- **Failures degrade**: an exception in our code is logged and skipped, never propagated
  into the host game.
