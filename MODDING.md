# Extending The Thousand and First

> Adding content? Stay in this file — the XML registries need no code.
> Writing code against the mod? See [docs/API.md](docs/API.md) for the supported API and its
> stability guarantees.

The mod is a platform: its content registries load through the game's own mergeable XML
streams, so **any mod can add to them by shipping a file with the right root element** —
no code, no dependency declaration, no patching.

## Bridge an external ground owner

Use the supported provider protocol only when another mod has a real typed ownership registry.
Implement `ThousandAndFirst.Api.IKingdomExternalOwnershipProvider`, mark the class with
`[KingdomExternalOwnershipProvider]`, and return stable string/GUID evidence for the exact active
zone. See [docs/API.md](docs/API.md#external-ground-ownership-provider-protocol) for the complete
field and failure contract.

If the provider strongly types a third mod, place it alone in a sibling directory selected by an
exact manifest `Directories` dependency row. Do not put that row under a common recursive root:
Qud would compile the foreign references even when the dependency was absent. Compile core alone,
then compile the selected shard against a reviewed ABI fixture. Test missing, exact-present,
wrong-version, disabled, failed, and wrong-load-order states.

Providers observe; they do not broker. Never load remote zones, create or claim a foreign
settlement, publish TAF works through a foreign catalogue, convert/move settlers, clear
`PartyLeader`, or call a foreign parking lifecycle. Return unowned, exact evidence, or a failure.
TAF owns the reject/bind prompt, founding receipt, water barrier, claim projection, persistence,
and later divergence pause. It persists both an explicit-unowned mode and an exact bind; providers
must not write or clear those TAF-owned receipts.

The shipped Hearthpyre bridge is deliberately exact to 2.2.3 and read-only. Qud Industry 0.3 is
XML-only in the audited installation, so machinery integration remains final-resolved-capability
plus explicit designation rather than a fake typed bridge or blueprint-name allowlist.

## Add buildings and city styles

Ship a `KingdomBuildings.xml` in your mod root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<kingdombuildings Schema="1">
  <style Name="fungal" />
  <building Key="sporehut" DisplayName="spore hut" Blueprint="MyMod_SporeHut"
            Cost="6" Ticks="1800" Styles="fungal" />
  <building Key="mysterystone" DisplayName="mystery stone" Blueprint="MyMod_Stone"
            Cost="3" Ticks="600" Styles="all" />
</kingdombuildings>
```

Every public registry root declares `Schema="1"`: `kingdombuildings`, `kingdomdeals`,
`kingdomyardworks`, `kingdomresearch`, `kingdomprocedures`, `kingdomraidprofiles`,
`KingdomArchitectures`, and `KingdomArchitectureTransitions`. Schema is
the format of that root, not the mod version. Keep it at `1` while authoring against this guide.
Unversioned files from before this boundary remain readable; a present malformed or unsupported
Schema rejects that stream before any entry can half-register. Merge-by-key remains the
compatibility mechanism inside one readable schema.

- `Key` — unique id. Re-using an existing key **merges** into that entry rather than replacing it:
  attributes your file names win, attributes it omits keep whatever the earlier file said. Load
  order applies, so styles can retheme the base catalog. See
  [Layering](#layering-what-happens-when-two-mods-name-the-same-building).
- `Blueprint` — any object blueprint, yours or vanilla. Buildings are real objects: give
  them vanilla parts (`LiquidVolume` for storage, `Bed`, `Shrine`, `Campfire`...) and the
  kingdom's systems and the ambient AI use them with zero extra wiring. Storage capacity,
  for example, is literally `LiquidVolume MaxVolume`.
- `Cost` — drams of water drawn from the settlement's physical stores.
- `Ticks` — build time (1200 = one day).
- `Styles` — **a tag list.** Which city styles this design belongs to. The default city style is
  `common`; the styles this mod declares are `common`, `verdant`, `fungal`, `gyre`, and `eater`,
  each resolved from the ground a city was founded on. Three spellings, and they are the same
  three every tag list in this file uses:

  | Written | Means |
  |---|---|
  | `Styles="all"`, or omitting it | every style there is, including ones a mod adds after you |
  | `Styles="fungal,gyre"` | these and nowhere else — how a style-exclusive design is declared |
  | `Styles="all,!eater"` | everywhere **except** — how a restriction is declared |

  The `!` prefix is vanilla's own negation operator (`Factions.xml` ships
  `RecipeGenotype="!True Kin"` on Chavvah's water ritual), and it exists because **the style set
  is open**. A design that belongs everywhere but one place cannot say so by enumeration: the
  moment somebody ships a sixth style, a list that spelled "everywhere" as four names is quietly
  wrong about itself. A refusal stays right. A refusal beats a welcome for the same tag whichever
  order they are written in, and a list of nothing but refusals reads as "everywhere except" —
  never as "nowhere". Matching is case-folded, so `Styles="Verdant"` works.

  Declare your own style with `<style Name="mystyle" />` and designs tagged with it become
  buildable in any city of that style; `KingdomData.Styles` is the registry's live union.
  A selector-free style is available to supported code and the `kingdom:style mystyle` test wish,
  but does not hijack a founding site. To let terrain found it without C# changes, declare bounded
  selectors:

  ```xml
  <style Name="glass" Terrain="TerrainGlass,CrystalDunes" Region="Glass"
         Strata="surface" Priority="700"
	     GroundClause="ground bright enough to found a glass city"
	     Crop="Congealed love" Seed="MyMod_GlassSeed" CropRow="MyMod_GlassRow"
	     WallMaterial="shapedstone" TimberWall="MyMod_GlassLatticeWall" />
  ```

  `Terrain` and `Region` are comma-separated, case-insensitive substring tokens. Exact terrain
  blueprint evidence is tried before region evidence; inside either lane greater `Priority` wins
  and declaration order breaks a tie. `Strata` is `all` (default), `surface`, or `deep`.
  `GroundClause` is the short founding/report phrase. Re-declaring a style merges by name exactly
  like a building: omitted attributes survive and blank clears. Names and selector sets are
  bounded; malformed declarations are logged and leave the earlier valid definition intact.

  The same row owns behavior, so adding a style does not require a C# switch:

  | Attribute | Contract |
  |---|---|
  | `Crop` | Food-item blueprint stored by this style's fields and used in its realm dish. |
  | `Seed` | Non-food seed item carrying `r_KingdomSeed`. |
  | `CropRow` | Standing plant blueprint inheriting `Plant` and carrying vanilla `Harvestable`. |
  | `WallMaterial` | Preferred paid wall material: `mud`, `brush`, `timber`, `stone`, `marble`, `scrap`, `shapedtimber`, `shapedstone`, or `workedmetal` (documented aliases also work). Preference never creates stock; the builder falls back to the richest material actually held. |
  | `TimberWall` | Wall blueprint used when timber wins, allowing a style-specific plant, fungus, lattice, or ordinary timber shell without code. |

  `Crop`, `Seed`, and `CropRow` are one atomic reversible mapping: declare all three or none.
  Omitting the trio inherits `common`; conflicting reverse mappings, unknown blueprints, a crop
  that is not food, a seed without `r_KingdomSeed`, a row that is not a harvestable `Plant`, a
  non-solid timber wall, controls, and overlong values are refused by style name in the log.
  Architecture palettes and maps remain
  ordinary `KingdomArchitecture.xml` style variants, described below; together these two registries
  let a sixth style own founding, food, material taste, furnishings, and topology without core edits.
  Restricting a base design is a merge: re-declare the key with nothing but the new tag list, and
  every other attribute the base catalogue wrote still stands.
- `Category` — what the building is FOR, which is what the settlement's own plan sites it by
  (default `civic`). Recognised values and where each is raised:
  | Category | Where the settlement puts it |
  |---|---|
  | `storage` | Beside the water — the vessels already dedicated to the stores. |
  | `housing` | Clustered with other housing, and back from the frontier band. |
  | `civic`, `craft`, `faith`, `knowledge` | Toward the settled heart, with a lane kept around it. |
  | `food` | A ring out past the built-up ground; rows abut. |
  | `memorial` | Quieter ground further out still; rows abut. |
  | `defense` | The frontier: the edges of the zone facing ground the realm does not hold. A new segment closes a gap in the line before it extends an end. |
  | `power` | Nowhere in particular — a wheel wants moving water and a sailvane wants wind, neither of which the plan can see, so the founder's own ground is used. |
  Any other value (including one your mod invents) is treated the same as `power`: the plan
  declines to guess and builds where the founder is standing. `Defence` is an output, not a
  category override. A design with positive `Defence` and no `Plot` is a cap-exempt frontier
  work and receives terrain/knowledge wall bonuses. A plotted watch-lodge, shrine, court, or
  arsenal keeps its complete authored lot, counts once against the building cap, follows its
  `Category`, and contributes exactly its base `Defence`. A plotted `Category="defense"` design
  still asks the plan for defensive ground because of its category, but it remains a plot.
- `MinStage` — earliest growth stage the design appears at: `Camp` (default), `Steading`,
  `Village`, `Town`, `City`.
- `Carries` — **what this design adds to the settlement's sustainable level**, as a comma list of
  `support:settlers`. This is the denomination of the whole catalogue and it is *not* output per
  day: it is how many more people the place honestly carries once nobody is hauling anything in.
  Three supports **bind** — `water`, `food`, `roof` — and the level is the *least* of the three,
  because a town with water for ninety and bread for nine is a town of nine. Five **lift** —
  `craft`, `spirit`, `learning`, `order`, `luxury` — and are summed and then capped at half the
  binding level, so no quantity of shrines outruns the cistern. One point of `water` is one dram a
  day sustained, which is one settler's thirst at camp rates. **The binding three are citywide
  pools; a lift only lifts the people its work actually reaches** (`Reach`, below, and
  `KingdomReachRules.Landed`): a design lands the share of the settlement's roofs it covers, so
  the same shrine is worth its whole amount among the houses and a fraction of it out past the
  fields, and an `S` design — which shades its own plot — lifts the settlement's level by nothing
  while still shading the ground it stands on. A support name this mod does not know
  is accepted and lifts; a new *binding* good would make every catalogue that predates it
  unbuildable, so there will never be a sixth. Omitting the attribute adds nothing to the level,
  which is correct for a wall.

  **`water` is read twice, so declare it only on a design that makes water.** A water point is
  both a settler carried *and* a dram a day arriving in the casks: `KingdomGrowth` banks
  `KingdomSubsidence.Supports(survey).Water x days` on its own checkpoint, so a design declaring
  `Carries="water:8"` really does put eight drams a day into the settlement's stores whether or
  not anyone is watching. That makes it a **minting** attribute, and it is why the shipped
  catalogue's cisterns, reservoir and waterworks declare no `water` at all:

  > A design that declares `water` carries a real producer part on its blueprint — vanilla's
  > `LiquidProducer` — and its figure is `1200 / mean(VariableRate)`, which is
  > `KingdomRules.TicksPerDay` divided by turns-per-dram. A design that only *holds* water
  > declares no `water`; it is paid in `LiquidVolume MaxVolume`, which is a stage gate
  > (16 / 64 / 256 / 1024 drams of dedicated capacity for Steading / Village / Town / City) and
  > the clamp on how much the water detail may haul.

  Nothing enforces this at load — a third-party catalogue may declare what it likes — but a
  vessel that declares `water` is conjuring it, and there is no rain in Qud to conjure it from.
  Give any producer you write `FillSelfOnly="true"`: without it `LiquidProducer` overflows by
  creating an open `Water` object in its cell, which `KingdomSurvey` then counts as a pool the
  water detail can haul, and the same dram is minted twice. Size a vessel so that its capacity
  divided by `KingdomWearRules.LeakDaysToEmptyAtCeiling` (50) stays under the daily drinking
  bill of the rung it opens at — a holed store leaks at exactly that rate, and one that
  out-leaks its rung makes a single lost rung fatal.

The whole merged catalogue is validated once, on load: a dangling `UpgradesTo`, a chain that rings,
an improvement onto a larger plot, a footprint larger than the plot it stands on, housing or roof
capacity under an open effective roof, and a family no camp can ever reach are all reported to the
log. None of them ever unregisters an entry —
a design that is wrong about itself stays buildable and becomes visible, which is the only shape a
check on third-party content can honestly take.

### Physical delve architecture

A third-party skin or exact-size architecture binding for the shipped `delve` BuildKey must leave
vertical travel to the runtime transaction. In `<KingdomArchitectures Schema="1">`, its selected
palette maps the Down slot to `r_KingdomDelveDown`; its map contains exactly one Object placement
with `Anchors="travel:down"` and `Stateful="yes"`; and its tier requires that role exactly once.
Do not place `r_KingdomDelveUp`, raw `StairsDown`/`StairsUp`, or a decorative same-map pair. The
architecture checker rejects those as cosmetic rather than physical proof.

Before water or material is reserved, the runtime transforms that one authored Down through the
frozen lot pose. The cell at the same x,y in the canonical zone exactly one stratum below must be
claimed, already built, and free of wall, open liquid, stairs, creatures, carried or stateful
objects, and third-party property. Refusal never visits or generates the lower zone and moves
nothing. After the authored head finishes stamping, the runtime creates exactly one
`r_KingdomDelveUp` in that foot cell using Qud's reciprocal stair-connection convention. Do not
write `r_TAF_DelveLink*` or `r_TAF_DelveEndpoint*` properties yourself: they are schema-last,
bounded runtime receipts tied to the frozen snapshot, root, lot, coordinates, and exact endpoint
IDs.

New-format reach is present only while the behavior root, both physical wrappers, their shared
coordinates, passable dry cells, and both native connection records re-prove that receipt. A
missing, moved, corrupt, duplicated, or obstructed endpoint closes reach instead of falling back
to an integer registration. The old `r_TAF_Delved:<zone>` integer remains readable only for a save
with no new physical-link state. Striking a current delve preflights the exact owned pair, removes
that pair and its two connections without clearing either landing, and tombstones reach only after
absence is proved; foreign objects are a refusal, never cleanup targets.

### Paying in material (optional)

```xml
<building Key="hut" DisplayName="timber hut" Blueprint="r_KingdomHut"
          Cost="6" Ticks="1800" Styles="all" Category="housing"
          Materials="timber:6,brush:2" UpgradesTo="hutyard" UpgradeMaterials="timber:4,stone:2" />
```

- `Materials` — what the settlement must have in a dedicated stockpile, spent when the
  commission is issued. Format is `material:units`, comma-separated. **Absent means the design
  costs water alone**, which is what every design cost before this existed — no entry, ours or
  yours, changes behaviour by doing nothing.
- `UpgradeMaterials` — the same, for improving this design into its `UpgradesTo` successor.
  It belongs on the predecessor alongside `UpgradesTo`, so two different predecessors may pay
  different additions when they reach the same successor. Absent means the improvement costs
  water alone.
- The vocabulary is nine keys in two halves. **Raw**: `mud`, `brush`, `timber`, `stone`, `marble`,
  `scrap`. **Refined**: `shapedtimber`, `shapedstone`, `workedmetal` — what a yard makes and the
  only way to have any. `canvas` is accepted as an alias for `brush`, `scrap metal` for `scrap`,
  and the spaced spellings (`shaped timber`, `dressed stone`, `worked metal`) for the refined
  three. Anything else is a logged error and the whole attribute is rejected — never half-applied.
- A malformed value disables itself with a logged reason and leaves the design costing water
  alone. It never crashes the registry and never half-registers.

Materials are **never minted**. They come off ground somebody cleared, off a building somebody
struck (half of what it was made of), or out of a caravan. A settlement holds them as real items
in a container the founder dedicated as a **stockpile** — a mark, not a transfer, exactly like a
larder: what is inside stays where it is and stays the player's, and the settlement only counts it.

To make your own item count as one of the nine, tag its blueprint:

```xml
<object Name="MyMod_IronwoodBeam" Inherits="Item">
  <tag Name="r_KingdomMaterial" Value="timber" />
</object>
```

A charter may carry material as well as water, per caravan. Charter entries live under
`<kingdomdeals Schema="1">` in `KingdomDeals.xml`:

```xml
<deal Key="timberroute" DisplayName="timber charter (4 drams and timber per caravan)"
      MinStanding="400" Income="4" Interval="3600" Caravan="DromadTrader1"
      Materials="timber:4" />
```

### Yards, bits, and rare finds (all optional)

```xml
<building Key="masonyard" DisplayName="mason's yard" Blueprint="r_KingdomMasonYard"
          Cost="16" Ticks="3000" Plot="M" MinStage="Steading" Open="yes"
          Materials="stone:14,timber:6" Staff="2" Refines="shapedstone" />

<building Key="condensery" DisplayName="the condensing hall" Blueprint="r_KingdomCondensery"
          Cost="100" Ticks="12000" Plot="XL" MinStage="City"
          MinTech="foundry" Knowledge="machine:Solar Still"
          Materials="stone:80,shapedstone:16,workedmetal:12"
          Bits="0034" Exotics="ingot:1" Carries="water:50" />
```

| Attribute | What it wants |
| --- | --- |
| `Refines` | The refined material this design makes: `shapedtimber`, `shapedstone`, `workedmetal`, or the yard's own key (`sawyer`, `mason`, `smelter`). Declaring it is the whole of what makes a building a yard — your own sawmill counts exactly like ours. A raw material here is an error: a yard that made timber would mint it. |
| `Bits` | What a high-craft design costs in **the game's own tinkering bits**, written in bit tiers: `Bits="0034"` is two tier-zero bits and one each of tiers three and four. The game's bit colours (`BBbc`) are accepted too. Whitespace and commas are ignored. |
| `Exotics` | Rare finds, as `exotic:units`: `ingot` (bronze ingot), `silver`, `gold`, `gem` (any rough gemstone). Item names work too (`gold nugget:2`). |

**How a yard works.** A staffed yard converts `RawPerRefined` (two) loads of raw stock into one
refined unit at a time, out of the crew the staffing pass gave it and at a rate scaled by who those
settlers are — Strength for the saw-pit and the banker, Intelligence for the furnace, or an exact
practised skill where the design asks for one. It runs on
**days, not on visits**: a yard that ran for thirty days finishes thirty days of work whether or
not the founder watched, held to `MaxRefinedPerDay` (eight) for each of those days — the width of
the saw-pit, not a rule about homecomings. A yard that ran for thirty days with **nobody standing
in it** finishes nothing, and says so once: the rate is time × labour, and labour is the half that
was missing. Nothing wears out with the calendar; wear comes from raids, from hard running, and
from temperamental certified tech. The one rider: what an already-damaged work goes on LOSING
does run on world days — a holed cistern or battery empties whether or not the founder is
watching. The damage is still an event; only its consequence is a clock, and mending ends it.

**Where bits and rare finds come from.** Both are ordinary items in a container the founder
dedicated as a **stockpile**. Bits are read off vanilla's own `TinkerItem` — a fried processing core
is worth what the game says it disassembles into — and spending them breaks the cheapest thing on
the shelf that answers the price. An item already counted as one of the nine materials is never
also counted as bits (`Scrap Metal` builds walls, not machines), so donate ruin-scrap for bits. To
declare bits for an item vanilla knows nothing about, tag it `<tag Name="r_KingdomBit" Value="34" />`;
for a rare find, `<tag Name="r_KingdomExotic" Value="gem" />`.

**Infrastructure gates the big designs.** A design on an **L** plot needs the yard its material
implies standing and staffed; an **XL** one needs that yard headed by a named notable as well.
Which yard is *derived*: every refined material a design names points at its own yard, and a design
that names none is judged by what it is mostly made of — timber to the sawyer, stone and marble to
the mason, scrap to the smelter. A design of mud and brush, or one costing water alone, is never
gated, so an entry written before yards existed behaves as it always did unless it is L or XL. The
gate is asked when a work is **raised**, not when it is improved: an improvement climbs within a
plot the settlement already proved it could build on. Every refusal names the yard and its state.

**Wear is never the calendar.** Works are damaged by events — a raid, hard running, temperamental
certified tech — run at reduced effect, say so once, and never die. Mending one costs a share of
what it was built from (materials and bits both) plus hands, through the ordinary pass.

### Gating a design (all optional)

Catalogue gate attributes decide whether a design may be raised, on top of `Styles` and `MinStage`.
**Every one is optional and an absent attribute gates nothing**, so an entry written before these
existed — ours or yours — behaves exactly as it always did. Ordinary zoning-gate mistakes are
logged and dropped. The paired covenant identity is stricter: a malformed pair or unknown faction
rejects that merged declaration loudly, leaving an earlier valid declaration intact, because
silently dropping a diplomatic price would publish a different design than its author wrote.

| Attribute | What it wants |
|---|---|
| `Districts` | Comma list of district keys whose ground will take this design: `agrarian`, `market`, `craft`, `shrine`, `garrison`, `academy`, plus `none` for ground the founder has never named. `all` (or omitting it) accepts everywhere. A key we do not recognise is treated as somebody else's district, never as open ground. |
| `MinZones` | Claimed zones the realm must hold. The eight designs that declare 2/3/4 line up with `MinStage` `Village`/`Town`/`City` — see `KingdomZoningRules.ZonesForStage`, which is also the ceiling the founder's own claim is checked against, so a settlement reaches the ground a design wants at the same moment it reaches the stage that wants it. Reachable now: the founder claims bordering ground (including a stratum directly above or below) from the Charter's **Claim this ground** — but a claim below is ground you OWN and cannot WORK until a delve is sunk above it: the shaft is what turns owned rock into a place crews can reach. |
| `Knowledge` | Comma list of things the settlement must know, **all** of them. A requirement written `kind:name` must match that kind exactly; one written as a bare name is satisfied by any kind. Kinds: `disk` (a design taught to the keepers from a data disk the founder carried home — the disk is read and handed back, never spent), `machine` (a machine hauled home and certified fit for the grid), `origin` (a trade the settlement holds because somebody from that country lives there, so it comes and goes with them), `node` (a subject the keepers worked out at a bench — see the research schema below), `rite` (what a water ritual seeded), `book`/`savant`/`culture`/`species` (declared for the identity and lab lanes; an unknown kind gates perfectly well and is worth no craft). **Knowledge lives with the CITY that learned it** (Addendum 22 B-cluster): the seat's rolls answer for the seat, a disk re-teaches freely at the other city, a machine re-certifies where it stands, and a seceding city walks out with what it knew. One kind is not knowledge at all: `enrolled:` keys are the becoming annexe's rolls — who this city says may use the old machines — carried on the same container so the book goes where the city goes, worth no craft, and never satisfied by an unqualified requirement. Invent your own kind freely. |
| `MinTech` | Craft the settlement must have reached: `hands` (the start, gates nothing), `salvage`, `workshop`, `foundry`, `arclight`. |
| `Covenant` + `MinStanding` | A Qud faction key and the kingdom standing required with it (`-1000` through `1000`). Both must be written together: `Covenant="Mechanimists" MinStanding="250"`. This reads the realm's standing, never the founder's personal reputation. The row remains visible below threshold with an exact refusal; meeting the threshold opens it immediately. Unknown faction keys and half-pairs are load errors. To clear a covenant inherited through merge-by-key, write both attributes blank. |
| `Megastructure` | `yes` marks the design as a city's PURPOSE (Addendum 22 A1): one per city, refused by name where one already stands, re-keying the same design allowed. |
| `Capital` | `yes` marks a capital-specific design (Addendum 22 A3): it may rise only where the crown stands, and it does NOT spend the city's purpose slot. Judged before the purpose gate. |
| `Satellite` | The KEY of a great work this design is an outpost of (Addendum 22 A2): the parent must stand somewhere in the realm, and the outpost is one to a city. A third-party file ships an outpost of its own megastructure with no code of ours changing. |
| `Strata` | Which set of the catalogue the design lives in and which strata it may also stand in (Addendum 15). The **first** welcomed token is the home stratum; the rest are share-tags: `Strata="deep,surface"` lives in the deep and may stand on the surface. Same spellings as `Styles` (`all`, a leading `!` for "everywhere except"). Tokens today: `surface`, `deep`, `sky`, `arcology` — and the set is open, so a third-party stratum names itself. **Absent means everywhere**, which is why every record written before this attribute existed still stands wherever it stood. Sky is a filtered subset of the surface: a list that does not mention `sky` answers for sky ground exactly as it answers for the surface, so only `!sky` (or a sky home) separates them. `Sky="yes"` on the plot spec is a different question — wanting open weather — and is asked first. |

A fifth kind is reserved by convention: `pattern` (a foreign design a chartered caravan
occasionally offers a choice of, never taught by any disk, machine, or origin — see
logical `Experience/KingdomCeremony*.cs`, symbol `FreezePatternBook`). Write `Knowledge="pattern:some-name"` on an ordinary `<building>`
entry to enter it into that pool; the base catalogue never depends on the draw, so an entry gated
this way is purely additive.

Craft is **derived, never authored and never set**: a taught design is worth 1 and a certified
machine is worth 2, an origin is worth 0, and the level is read off the total — **per city**, judged
against the city a design is being built in. The research tree does not change this: a node MINTS a
roster key like any other kind and is worth no craft. Research TIER (what the keepers can take up)
is a second, orthogonal ladder gated on the city's best researcher's Intelligence, and neither
ladder ever substitutes for the other.

### Developer scenarios (`KingdomScenarios.xml`, root `<kingdomscenarios Schema="1">`)

Not an extension surface for shipped mods. The registry is documented here because it follows the
same registry law as the others, but it loads only from the excluded `Harness/` tree, which no
release artifact contains. Values are validated by one shared row validator, so registry load,
direct preflight, and digesting cannot disagree.

| Attribute | Meaning |
| --- | --- |
| `Key` | lowercase token, unique |
| `Family` | lowercase token grouping related scenarios |
| `AuthorityClass` | the production authority the scenario exercises; must declare a semantic key set |
| `Seed` | literal engine seed; a leading `#` pins the world exactly |
| `Synthetic` | exactly `true` or `false`, lowercase; anything else is a fault and the row cannot realize |
| `AnchorId` | the ordinary-play anchor this scenario leans on; empty until a reviewer curates one |

Child `<param Name Domain>` declares a closed `|`-separated domain. Child `<step Verb ...>` names a
verb from the closed set; each verb has a closed argument schema and an argument outside it is
refused. An argument value of `{name}` resolves from the bound parameter at preflight. **At most one
mutating verb per scenario, and it must be the last step** — that is what makes an attended run
atomic rather than merely careful.

Driving a live session by keystrokes is its own hazard: Unity's gameplay view ignores `SendKeys`,
while low-level scancode `keybd_event` input reaches menus and the wish console; numpad movement
works, but extended-key arrow input is NumLock-sensitive; and a blind, timed key-chain script can
desync when a popup eats one of its keystrokes. See the Developer scenario harness section in
[TESTING.md](TESTING.md) for the file-driven auto-runner this drove.

### Curated anchor evidence (`KingdomScenarioAnchors.xml`, root `<kingdomscenarioanchors Schema="1">`)

Written by a reviewer from a state ordinary play actually reached. The harness reads this store and
has **no path that writes one**; a scenario may never found its own anchor. A row must declare
`Reached="ordinary-play"` exactly, and must carry `AnchorId`, `AuthorityClass`, `Verbs`,
`KeySetDigest`, `DefinitionDigest`, `PlanDigest`, `ModVersion`, and `QudCoreVersion`. Acceptance
requires every one of those to match the state being judged; a mismatch is refused by field name.

The key set includes `architecture.realized.digest`, the shared production capture of the exact
realized lot (`Core/KingdomRealizedArchitectureCapture.cs`). Both an ordinary commission and the
review gallery call that same read-only implementation, so a build whose receipt matches but whose
ground, objects, or rendering differ fails the differential.

### Research nodes (`KingdomResearch.xml`, root `<kingdomresearch Schema="1">`)

A `<node>` is a catalogue record in the buildings' own idiom, merged by `Key` so a later file
overrides an earlier one. Each node names what it `Grants` (the `node:<key>` roster key it mints
when finished), what it `Requires` (a `Knowledge`-grammar list), what `TaughtBy` completes it
outright (a disk in the hand is a finished idea — never a `rite:`, which is a load-time schema
error), what `SeededBy` merely *begins* it (a rite seeds a quarter of the work, capped at half —
doors, never rooms), its `Tier` (1–4, gated on the best researcher's Intelligence: 10/14/18/22,
hard at the boundary), and its `Effort` in labour ticks. Accrual is the scaffold's own idiom: a
staffed knowledge bench charges real elapsed labour against ONE named subject, idle time is spent
and never banked, and an unstaffed bench says so once. Discovery is a journal fact
(`taf:node:<key>` observations): a design gated on a node nobody here has heard of is not shown in
any menu — write `Knowledge="node:your-key"` on an ordinary `<building>` and the visibility law
covers your record exactly as it covers ours.

### Procedures (`KingdomProcedures.xml`, root `<kingdomprocedures Schema="1">`)

A `<procedure>` is what the grafting hall or the chimeric theatre will do to a body, merged by
`Key` like every other record. `Grants` names a PART CLASS — never a creature, never "a
creature's power" — so a modded creature carrying the class is a valid source the day that mod
ships, with no entry of ours. `Slots` is checked against the player's OWN anatomy by
`BodyPart.Type` ("there is nowhere on you to put it"); `SlotCategories` gates on
`BodyPartCategory` per-procedure, which is how a True Kin, a robot, and a slime each read a
different legal set with no genotype list anywhere. `Source` is `part`, `limb`, or `mutation`
(mutation grafts cap at levels 1–3, never the source's level); `Attach` is `body` or `weapon` —
a class that only fires on weapon events must graft onto a natural weapon, and a record whose
part cannot fire on its new bearer is refused at commit, by name. `MinRung` places it on the
ladder (2 the hall, 3 the theatre); `Preserved` is how many kept parts it consumes; `Creeds`
carries the standing cost in the `-Faction` idiom. No procedure is ever random — the one
confessed gamble is priced as one. The only legal input is a part the vat-house preserved:
the hall will not open a body for a thing that was not kept.

Gating is **hard for where a structure may stand and soft for how well it works**. `Districts`
refuses placement; nothing anywhere gates the district *bonuses*, which stay realm-wide and
unconditional, so a design raised off its natural ground simply misses a bonus. Housing, storage,
and civic designs are additionally always accepted on undistricted ground, so a camp can never hit
a wall before the founder has learned what a district is.

### Creed-gated designs: the whole stack (all optional)

Three more attributes, on top of the four above, and together with them they are the contract for
a design that belongs to a *people* rather than to a place. Every one is optional, an absent
attribute gates nothing, and a malformed one is logged and ignored — same bargain as the rest.

| Attribute | What it wants |
|---|---|
| `Builders` | **Who must be standing here.** Comma list, **all** of them, in the same `kind:name` language `Knowledge` uses, optionally with a count: `Builders="origin:the rust wells:2,creed:Barathrumites"`. Kinds: `origin` (people who walked in from that country), `creed` (people holding that creed *today*), `kept` (people who hold it **or have ever held it** — the aligned). A token written as a bare name with no kind is satisfied by any of the three. A kind we do not know never matches, and the refusal names it, because unlike a `Knowledge` kind there is no `Learn` call a third party can make to supply one. |
| `Creed` | **Alignment.** One faction name. The design is raised only by builders who hold that creed **or have previously held it**. A settler's creed history is a recorded fact from the moment they leave a creed (`KingdomCreed.CreedPastProperty`, written at the one conversion path), bounded to `KingdomCreedRules.MaxKeptCreeds` names and first-in-wins — it never rotates, so a design a city could see yesterday cannot vanish today. |
| `CreedShare` | **The amount of it.** Whole percent of the city that must hold `Creed` *now*, floored at `KingdomCreedRules.MinBelievers` however small the city. Omit it and the design asks for `DominantSharePercent` — the same third a city's own creed is read at, so "a creed-work wants a creed city" is one rule and not two. Write `CreedShare="0"` to ask for no share at all: one aligned builder is enough. Writing it without a `Creed` is dropped and named. |

Use `Knowledge` and `MinTech` for the knowledge and technology halves of the stack; when the
research system lands, those keys are what get re-pointed, and your entry does not change.

**The visibility law.** A creed-gated design a city has **no path to** — nobody holds the creed and
nobody living there ever has — **does not appear in the commission list, the plan list, the
settlement's own choices, or the keepers' map at all.** Every other gate in this file leaves the
design in the list wearing a tag that says which key it wants, because a list that silently
shortens teaches nothing; this one is the only gate with no key anywhere, so naming it would be
noise dressed as guidance. The moment one person aligns — by arriving, by converting, or by having
converted *away* years ago — the design appears, tagged with whatever is still in its way. Every
other refusal in the stack is spoken out loud and names what would lift it.

**A worked example.** A third-party mod adding a creed-work for a creed the base mod never
mentions, using the whole stack and nothing but data:

```xml
<?xml version="1.0" encoding="utf-8"?>
<kingdombuildings Schema="1">
  <!-- The Seekers of the Sightless Way keep a walking-house: no windows, no lamps, and a
       floor cut so that the pattern of it can be read with the feet. -->
  <building Key="mymod_walkinghouse"
            DisplayName="walking-house (no windows, and a floor that can be read with the feet)"
            Blueprint="MyMod_WalkingHouse"
            Cost="24" Ticks="4800" Category="faith" Plot="M" MinStage="Village"
            Styles="all,!gyre"
            Districts="shrine,none"
            MinTech="salvage"
            Knowledge="origin:the desert canyons"
            Builders="creed:Seekers:2,origin:the desert canyons"
            Creed="Seekers"
            CreedShare="20"
            Materials="stone:18,timber:6"
            Carries="spirit:5,learning:1" />
</kingdombuildings>
```

Read it as the five separate questions it is:

1. **Where** — `Styles="all,!gyre"`: anywhere but the cold height, whose own sacrament court already
   answers for this.
2. **Who is here** — `Builders`: two people who hold with the Seekers *today* to keep the house, and
   one who grew up in the canyons and knows how the floor is cut.
3. **Alignment** — `Creed="Seekers"`: raised by somebody who holds with them, or once did. This is
   also the attribute the visibility law reads: a city that has never had a Seeker in it never sees
   this entry.
4. **How much of the city** — `CreedShare="20"`: a fifth of the town, and never fewer than
   `MinBelievers`.
5. **What is known** — `Knowledge` and `MinTech`, exactly as any other design declares them.

Nothing above is registered in C#. `Seekers` is a vanilla faction name and the creed derivation
admits it on its own terms (`KingdomCreed.CanBeCreed`); `origin:the desert canyons` is one of the
settler origins the base mod already counts; and `Builders`, `Creed` and `CreedShare` merge by
attribute like everything else, so another mod can re-declare `mymod_walkinghouse` with a
different `CreedShare` and change only that.

**Built-in breadth.** Against installed Qud 2.0.211.51, the ordinary `CanBeCreed` facts admit
exactly 33 shipped factions:

`Baetyls`, `Barathrumites`, `Chavvah`, `Consortium`, `Cragmensch`, `Daughters`, `Dromad`,
`Entropic`, `Ezra`, `Farmers`, `Girsh`, `Goatfolk`, `Gyre Wights`, `Hindren`, `Issachari`, `Joppa`,
`Kyakukya`, `Mamon`, `Mechanimists`, `Merchants`, `Mopango`, `Naphtaali`, `Resheph`, `Robots`,
`Seekers`, `Snapjaws`, `Strangers`, `Svardym`, `Templar`, `Trolls`, `Wardens`, `Water`, and
`YdFreehold`.

Each owns one built-in, behavior-bearing `Creed` design with its own lore, bill, blueprint, and
authored topology; **128** exact records cover every applicable S/M/L/XL lot. The runtime wish
`kingdom:creedcontent` derives this census from loaded factions and walks loaded catalogue and
architecture records. Chiliad's shipped faction file admits none under the same rule. This count
is a compatibility receipt, not a whitelist: a third-party faction whose own ordinary facts make
it a creed composes by adding an ordinary building, blueprint, and architecture record. No C#
switch or faction enum changes.

### Designs that grow into other designs (all optional)

A design may name what it becomes. When the settlement has earned it, it raises the successor
itself through the same scaffold a commission uses, out of what the stores can spare, and carries
everything the old work held and everything the founder had marked on it across.

```xml
<building Key="cistern" DisplayName="cistern court (holds 256 drams)" Blueprint="r_KingdomGreatCistern"
          Cost="16" Ticks="3600" Styles="all" Category="storage" UpgradesTo="cisternvault" />
```

| Attribute | Default when absent |
|---|---|
| `UpgradesTo` | Nothing. The design never changes, which is where every design starts. |
| `UpgradeCost` | The difference between the two designs' `Cost`, never free and never dearer than the successor's own price. |
| `UpgradeTicks` | 75% of building the successor from nothing, never less than one tick. |
| `UpgradeCrew` | The crew the successor needs to run, never fewer than one. |
| `UpgradeMinStage` | The successor's own `MinStage`. An override may only raise the gate. |

Pricing an improvement without naming one is an error, not a chain with a guessed successor.
Re-declaring an entry **without** `UpgradesTo` clears whatever chain an earlier file gave that key.
The settlement never improves a structure the player built or that was merely adopted, never
starts one that would leave the old work's contents nowhere to go, never draws the stores below the
reserve it lives on, and can be told to leave one work — or the whole ground — exactly as it is.

**When an improvement actually fires** is decided by present facts, never by how long anything has
stood — there is no maturation timer. The successor must resolve inside the standing work's frozen
exact binding. Stage, style, founder holds, exact `Knowledge`/`MinTech`, free hands, contents fit,
water price plus reserve, `UpgradeMaterials`, one-work-at-a-time pacing, and room on the frozen lot
are the real gates. Every actionable refusal is named; knowledge and technology repeat the exact
zoning detail. `CrewNeeds` is not an unlock or trigger: it changes raising/running pace after crew
assignment, while `UpgradeCrew`/successor `Staff` supplies the improvement's headcount gate.

The predecessor remains standing, staffed, and productive for the entire scaffold build. Contents,
residents, marks, wear, and protected state move only at successful handover. There is no guessed
water outage, temporary-lodging displacement, held offer, or force-through-harm branch.

### Plots: reserved lots and authored buildings

A `Plot` declaration reserves a typed rectangle. It is **not** a generated building recipe. New
construction first freezes a `LotId`, actual S/M/L/XL size, type/category, rectangle, and pose;
then an exact `KingdomArchitectures` binding selects an authored map, palette, tier, and variant.
That snapshot owns every claimed ground/structure/object cell, entrance, functional fixture, and
stateful anchor. The runtime never stretches a smaller map, invents a rectangle shell, cuts a
guessed door, or scatters row-major furnishings for a current authored commission.

A design with no `Plot` remains on the single-cell path. A design with `Plot` but no exact
`(BuildKey, Category, actual Size)` architecture binding is not offered at that size and direct
commission refuses before debit. Already-standing legacy plots retain their frozen legacy route;
that migration behavior is not an authoring fallback.

```xml
<building Key="hut" DisplayName="timber hut" Blueprint="r_KingdomHut"
          Cost="6" Ticks="1800" Styles="all" Category="housing"
          Plot="S" Footprint="3x3" Roof="Walled" MinTech="hands"
          Materials="timber:6,canvas:2" />
```

| Attribute | Default when absent |
|---|---|
| `Plot` | Not a plot. `S` (5x4), `M` (8x6), `L` (12x9), `XL` (20x14); long spellings `small`, `medium`, `large`, `huge` are accepted. This is the design's minimum reservable envelope, not its map. |
| `Footprint` | Fills the minimum plot for catalogue capacity/upgrade reasoning. `WxH` records how much of the lot this tier functionally occupies; the authored map remains exact authority over claimed cells. Larger than the minimum plot is an error. |
| `Roof` | `Walled`, or `Open` when `Open="yes"`. Catalogue-level shelter/sky/capacity semantics; authored map cover and palette must agree. `Open`, `Soft`, `Walled`, and `Carved` never generate walls themselves. |
| `Open` | `No`. `Yes` declares an unroofed use such as a field, yard, salt-pan, or reservoir. The authored map still supplies its exact ground, paths, fixtures, and entrances. |
| `Sky` | `No`. `Yes` means the design needs weather, and it is refused underground by name rather than sited somewhere useless — tagged **[wants open sky]** right in the commission list, the same tag every other blocked gate wears, before the founder has even picked it. |
| `Contents` | Nothing. Legacy semantic furnishing metadata only. Current authored maps place exact fixtures through palette slots; do not use `Contents` as a substitute for a map. |

Plot size is gated by stage: a Camp lays S, a Steading and a Village M, a Town L, a City XL.
**Upgrades climb within a plot; sizes compete across plots** — there is no in-place S-to-M
metamorphosis, and small plots never obsolete.

Keep these three lanes separate:

1. An `UpgradesTo` improvement is automatic settlement growth. It resolves the successor only in
   the standing receipt's same declared plan set, binding, type, and actual size, then preserves `LotId`,
   rectangle, pose, residents, wear, and protected state. The founding civic heart alone may move
   to its adjacent authored rung.
2. A `KingdomArchitectureTransitions` record is an explicit founder-selected plan change. It must
   be directional, same type, and same actual size; its previewed delta also preserves `LotId`.
3. Retype or resize is neither of those. It strikes the occupation, performs fresh siting/restake
   with a new `LotId` after preview and consent.

Clearing and paid material are reckoned from the frozen lot and authored claimed cells. A wider lot
may preserve intentional yard, but never stretches the structure. A design that needs weather
(`Sky="yes"`) is refused when catalogue roof semantics or authored cover contradict it. Palettes
must remain honest about material and technology: a hands-tier timber bill cannot introduce a metal
wall, powered fixture, filled vessel, free contents, or later-tech door.

### Exact authored lot realizations

`Plot` is a design's minimum envelope, not permission to stretch its map at runtime. Every size you
intend the picker to offer needs its own exact `(BuildKey, Category, Size)` `<binding>` in
`<KingdomArchitectures Schema="1">`. Missing sizes are filtered and direct calls refuse; preview
and commit never choose a nearest, smaller, or generic shell. Shipped content supplies every
reachable size from each minimum through XL. Third-party content may start with one exact size,
but only that bound size can be commissioned until larger bindings are added.

A larger binding must contain every predecessor tier needed for growth in that same plot. Its map
is the canonical lot's exact dimensions. The shipped generator preserves the complete source-map
coordinate block byte-for-byte, then classifies every added cell as palette-lawful yard, path,
sparse boundary, bounded frontage route, or an explicitly declared intentional opening. Category
chooses the court/service/crop grammar; plan, building, palette/style, and size seed its
deterministic phase. It never introduces a paid material or technology absent from the selected
palette. Heart-facing plans keep the source block against the canonical heart side. Road-facing
plans inset it by one frontage cell so every `entrance:public` has one exact authored unclaimed
route to an exterior cardinal step; rotation carries that same route through all four poses.

The shipped larger bindings are checked-in concrete XML, not runtime generation. After changing a
shipped source map or binding, refresh and prove them with:

```sh
python3 Tools/generate-lot-realizations.py --write
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py --repo-root .
```

`Architecture/KingdomArchitectures-LotRealizations.xml` is generated output and must not be
hand-edited. The independent checker enumerates every reachable exact pair, proves every ordinary
`UpgradesTo` successor exists in the predecessor's frozen exact binding, and compiles every
variant/pose golden with type and size in its identity, and rejects missing pairs, wrong lot
dimensions, inaccessible functions, material/technology mismatches, and absent or unbounded road
routes. Its report separates source from generated maps and names the largest compiled snapshot
with its byte and encoded-character counts. Every generated-map comment accounts for its yard,
path, boundary, route, and intentional-open cells. Hosted arcology ward/terrace realizations remain
explicitly held, unchanged, for their separate authored-floor redesign. The five civic-heart
records are rite-owned internal rungs, not commission sets, and therefore do not receive synthetic
larger choices.

Generated neutral yard carries no functional anchor. Its physical reach proof may cross only the
exact unclaimed cells selected by `entrance:public`'s bounded egress route. That route joins the
reserved exterior circulation lane, modelled as one virtual node connecting walkable boundary-yard
cells. Other `.` cells—including reasoned millrace, vane-sweep, planting-pocket, and catchment
openings—never prove a claimed yard connected; they cannot hide an island. Functional anchors still
use the stricter claimed-cell graph.

#### Complete minimal authored-plot extension

This pair is the smallest complete current-path example. The catalogue declares cost, behavior,
minimum lot, material, and technology. The architecture registry declares what actually appears.
Only S is offered because only S is bound; add authored M/L/XL maps and bindings to offer them.

```xml
<!-- KingdomBuildings.xml -->
<kingdombuildings Schema="1">
  <building Key="mymod_hut" DisplayName="reed hut" Blueprint="MyMod_ReedHut"
            Cost="6" Ticks="1800" Category="housing" Plot="S"
            Footprint="3x3" Roof="Walled" MinTech="hands"
            Materials="timber:6,canvas:2" Carries="roof:2" />
</kingdombuildings>
```

```xml
<!-- KingdomArchitecture.xml; filename is free, root name is not -->
<KingdomArchitectures Schema="1">
  <palette Key="mymod-reed-hands">
    <slot Key="ground" Blueprint="DirtFloor" Role="ground"
          Material="mud" MinTech="hands" Natural="yes" />
    <slot Key="floor" Blueprint="DirtPath" Role="floor"
          Material="mud" MinTech="hands" Natural="yes" />
    <slot Key="wall" Blueprint="MyMod_ReedWall" Role="wall"
          Material="timber" MinTech="hands" Natural="no" />
    <slot Key="door" Blueprint="MyMod_ReedDoor" Role="door"
          Material="timber" MinTech="hands" Natural="no" />
    <slot Key="bed" Blueprint="MyMod_Bedroll" Role="sleep"
          Material="canvas" MinTech="hands" Natural="no" />
    <slot Key="store" Blueprint="MyMod_EmptyBasket" Role="storage"
          Material="canvas" MinTech="hands" Natural="no" />
  </palette>

  <map Key="mymod-reed-hut-s0" Width="5" Height="4" DefaultCover="walled">
    <glyph Char="#" Ground="$floor" Structure="$wall" Claim="building"
           Pass="blocked" Cover="walled" />
    <glyph Char="+" Ground="$floor" Structure="$door" Claim="yard"
           Pass="walk" Cover="open" Anchors="entrance:public" />
    <glyph Char="i" Ground="$floor" Claim="building" Pass="walk" Cover="walled" />
    <glyph Char="b" Ground="$floor" Object="$bed" Claim="building"
           Pass="walk" Cover="walled" Anchors="fixture:sleep" Stateful="yes" />
    <glyph Char="s" Ground="$floor" Object="$store" Claim="building"
           Pass="walk" Cover="walled" Anchors="fixture:storage" Stateful="yes" />
    <glyph Char="@" Ground="$floor" Object="$building" Claim="building"
           Pass="walk" Cover="walled" Anchors="main,function:dwelling" Stateful="yes" />
    <row Cells="..+.." />
    <row Cells=".#i#." />
    <row Cells=".b@s." />
    <row Cells=".###." />
  </map>

  <plan Key="mymod-reed-hut">
    <binding Key="mymod-housing-s-reed" Type="housing" Size="S" Facing="road">
      <tier Key="mymod_hut" BuildKey="mymod_hut" Level="0"
            Map="mymod-reed-hut-s0" Palette="mymod-reed-hands">
        <require Role="main" Min="1" />
        <require Role="entrance:public" Min="1" />
        <require Role="function:dwelling" Min="1" />
        <require Role="fixture:sleep" Min="1" />
        <require Role="fixture:storage" Min="1" />
        <variant Key="fallback" Priority="0" />
      </tier>
    </binding>
  </plan>
</KingdomArchitectures>
```

The `MyMod_*` blueprints must exist and obey their declared passability, material, technology,
container, and takeability contracts. A palette is not cosmetic: changing it can change what a
wall blocks or what a fixture does. Run the generator only for this repository's checked-in base
content; third-party maps should be authored directly and tested in Qud.

### Identity-aware architecture variants

Identity uses the existing `<variant>` lane. It is not a second building catalogue and it does not
rewrite a standing building. Selection happens while preview/commission freezes the architecture
snapshot; that exact map, palette, anchors, placements, and facing then remain receipt authority
even if the last matching resident later leaves.

```xml
<tier Key="house" BuildKey="house" Level="0"
      Map="mymod-house" Palette="mymod-house-hands">
  <variant Key="fallback" Priority="0" />
  <variant Key="river-people" Priority="40" Cultures="River Folk"
           Map="mymod-house-river" />
  <variant Key="broad-access" Priority="60" Bodies="broad-bodied"
           Map="mymod-house-broad" />
</tier>
```

| Selector | Live settlement fact |
|---|---|
| `Cultures` | Positive `GetCulture()` tallies. Culture is knowledge/story, not creed. |
| `Species` | Positive `GetSpecies()` tallies. Species is body identity. |
| `Genotypes` | Positive `GetGenotype()` values carried in exact resident receipts. Most ordinary NPCs have no genotype, which is a valid empty set. |
| `Bodies` | Bounded vanilla-derived conditions: `robot`; `wet-bodied` for aquatic and non-flying bodies; `broad-bodied` for the vanilla `Gigantic` fact. |

These are comma tag expressions like `Styles`, `Creeds`, `Terrains`, and `Strata`. A positive token
matches when any live fact in that dimension equals it; any matching `!token` refuses the variant;
a pure exclusion means “any city carrying none of these”. Matching is case-insensitive. When
several variants match, higher `Priority`, then greater selector specificity, then ordinal `Key`
wins. `Creeds` retains its existing dominant-seat-creed meaning; do not put a culture name there
because its faction happens to share the spelling.

Every identity variant needs a distinct map which changes circulation, use, access, or anchors.
The checker rejects a palette-only style/creed/identity variant. An inherited palette is useful for
access overlays: a paired portal can reuse the paid stone/timber shell, but it cannot introduce a
metal door, liquid pool, charge source, or fixture the accepted bill and craft rung did not buy.
The exact-lot generator preserves all four identity selectors on concrete larger copies.

Shipped identity-overlay coverage is deliberately bounded: Hindren culture and hindren species
select floral housing; Kyakukya remains a creed selector; M-and-larger stone/fine housing has
broad-body portals and turns; charging posts have robot service approaches; reservoirs have a
wet-body water-edge circulation layout which creates no liquid; and the becoming annexe has a
True Kin registry axis. This does not narrow the separate creed-content promise above: every
currently admitted shipped creed owns a distinct behavior-bearing plan. Unknown or unhandled
culture, species, genotype, and body values take the authored fallback, and third-party
vocabularies compose by adding ordinary variants—no code enum or per-species table is required.

Clearing the ground is how a settlement without a mine gets material: brush and trees yield timber,
shale and granite yield stone, a marble seam yields marble, somebody else's fallen walls yield
scrap. Effort scales with hardness, and it is part of how long the plot takes to raise. Underground
(any zone below the surface stratum) a plot that would otherwise be enclosed is **carved** instead:
the clearing costs twice as much, it pays in stone, the plot's edge is left standing because that
rock **is** the enclosure, and no wall is ever raised.

**An `Open` plot is the exception, and it stays open underground.** Carving replaces the enclosure
a design would otherwise have raised; it does not roof ground the design deliberately left
unroofed. A field, a salt-pan, a market square or a reservoir taken underground is a field, a
salt-pan, a market square or a reservoir cut into the rock — open ground with stone around it, not
a sealed chamber — and it does not count as shelter. A design declaring `Walled` (or nothing,
which defaults to `Walled`) is carved exactly as before.

Ground the settlement may not take refuses the plot outright and says which cell and what is
standing in it — anything you placed, anything owned, any loose item, any of the settlement's own
works, open water, and anything the yield table cannot name. Water is never filled in. Every plot
also reserves a one-cell lane on all sides, and no more than sixty percent of a zone's interior is
ever plot. That clearance is circulation room, not the road system. Roads and network pieces are
explicit settlement-owned topology; every authored public entrance must reach exterior road/frontage
evidence in every pose. An interior entrance therefore needs a bounded, authored unclaimed route;
generic negative space and a nearest-road search are not frontage contracts.

Specs are keyed by building `Key` like every other registry: re-declaring an entry replaces its
whole plot spec, and re-declaring it **without** `Plot` returns that design to the single-cell path.
Declaring `Open`, `Sky`, or `Contents` without a `Plot` size is an error rather than a silent no-op.

Nothing already standing silently converts. Legacy single-cell and legacy-plot works retain their
old frozen path. New commissions, same-set plan transitions, and fresh retype/restake operations use
the authored architecture contract above.

### Fields that grow: seeds, rows, and the harvest cycle (all optional)

A design that GROWS food is not a `Carries="food:N"` number on an object with no parts. It is a
field that stands rows, and the number comes off the rows. Two things make one:

```xml
<!-- ObjectBlueprints.xml -->
<object Name="MyMod_Orchard" Inherits="Furniture">
  <part Name="r_KingdomPlot" />
  <tag Name="r_KingdomCropRows" Value="16" />
  <!-- Optional: omit this to accept every registered crop family. -->
  <tag Name="r_KingdomCropBlueprint" Value="Starapple" />
  ...
</object>
```

```xml
<!-- your buildings file -->
<building Key="orchard" DisplayName="orchard" Blueprint="MyMod_Orchard"
          Cost="14" Ticks="3600" Styles="all" Category="food" Plot="M" Open="yes"
          Staff="2" Manning="scaled" Carries="food:8" />
```

| Piece | What it does |
|---|---|
| `<part Name="r_KingdomPlot" />` | Makes the object a field: it takes seed, keeps the cycle, offers **Withdraw Seed**, and carries no `food` at all until the founder sows it. |
| `<tag Name="r_KingdomCropRows" Value="N" />` | How many crop plants physically stand in it. Declared on the **blueprint**, never in the catalogue — the same split a pantry's `r_KingdomLarderCapacity` keeps. |
| `<tag Name="r_KingdomCropBlueprint" Value="Crop" />` | Optional exact crop identity. When present, the field accepts only the seed mapped to that crop by the merged style registry; when absent, the founder may sow any registered crop. Use this for a specialized design such as a dark fungal vault, not for a whole stratum. It changes no cycle, water cost, rows, or yield. |

**`Carries="food:N"` is derived, not chosen.** `N` must equal
`Rows × KingdomCropRules.YieldPerRow / KingdomCropRules.CropDays` — with the shipped 3-per-row and
6-day cycle, `N = Rows / 2`. `_notes/balance-sim.py` §G2 re-derives every food design's carry from
its own blueprint's tag and fails the run if one has drifted, exactly as it re-derives every water
design's from its `LiquidProducer`. `Art/check_xml_refs.py` fails if a design carries
`r_KingdomPlot` and forgets the tag, or carries the tag on an object with no field part. The rows
must also fit inside the plot tier's own dimensions.

**A design may carry `food` without growing it**, and there are exactly two other honest reasons,
both checkable off the blueprint: it **keeps** (an `r_KingdomLarderCapacity` tag — a granary makes
a good year last), or it **makes** something that keeps out of what came in (it carries `craft`
beside its `food` — a mill). A `food` number with none of the three fails the sim by name.

**Seeds and crops.** The shipped crop families come from the settlement's merged style declaration
(`KingdomData.CropForStyle`), and each has a seed item and a standing-row blueprint mapped on the
same `<style>` row. A mod adding a style declares `Crop`, `Seed`, and `CropRow` together; the row is an
ordinary object inheriting vanilla `Plant` with a vanilla `Harvestable` on it, and
`Art/check_xml_refs.py` walks every one of those names in both directions. A seed item is any
object carrying `<part Name="r_KingdomSeed" />` whose blueprint the crop map names; it should
**not** be `Food`, because the settlement's ration draw eats anything that is.

**A wild plant can carry seed.** Merge one part onto a vanilla blueprint and it gives up its seed
once, to anybody who does not have to steal it:

```xml
<object Name="Watervine" Load="MergeIfExists">
  <part Name="r_KingdomWildSeed" Seed="r_KingdomSeedVinewafer" />
</object>
```

**Irrigation is vanilla's.** A powered `Hydraulic Irrigator` within its own radius pulls a growing
field's stamp forward each pulse, halving the wait. Nothing is declared for it: the field answers
the `AccelerateRipening` event the machine already fires.

**The cycle**, once sown: ripe after `CropDays`, one day alone with the founder (a ripe row carries
real `Harvestable`, so gathering it by hand takes it out of the settlement's share), then the
settlement gathers — attended or not, dated, and restamped from the harvest so multiple cycles
resolve in one reckoning. The harvest lands in a dedicated larder here, or travels to one in
another of the city's zones, or is lost for want of room, and says which.

### Kitchens, the settlement's dish, and mills (all optional)

**A kitchen is any finished work carrying vanilla's `Campfire` part.** That is not a new
mechanism — `Campfire` *is* the whole cooking system in Qud, and the communal fire has always
been one. Merge the part onto your own design and the settlement counts it:

```xml
<object Name="MyMod_Cookhouse" Inherits="Furniture">
  <part Name="Campfire" PresetMeals="r_KingdomFavoredDish" />
</object>
```

`PresetMeals` naming `r_KingdomFavoredDish` is what lets the founder eat *this realm's own dish*
at your building, exactly the way every named settlement's oven in vanilla carries its own signature
meal. Leave `PresetMeals` off and it is still a kitchen for the settlement's purposes and still a
full cooking site for the player.

**The dish itself is derived, never authored.** The realm's dominant creed picks the *form*
(borrowed from that faction's own vanilla `WaterRitualRecipe`) and the founding ground picks the
*body* (the style's crop). It is stamped onto the realm's `Faction.WaterRitualRecipe` and
`WaterRitualRecipeText`, so it is teachable through the water ritual in vanilla's own frame. A mod
that adds a style adds the dish body with the same `Crop` attribute; there is no second dish registry
and no C# edit.

**A mill is any finished work carrying vanilla's `Mill` part.**

```xml
<object Name="MyMod_Quern" Inherits="Furniture">
  <part Name="Mill" ChargeUse="1" Transformations="Vinewafer,Starapple" />
  <part Name="MechanicalPowerTransmission" ChargeRate="100" IsConsumer="true" />
  <part Name="Container" />
  <part Name="Inventory" />
  <stag Name="Food" />
</object>
```

```xml
<building Key="quern" DisplayName="hand quern" Blueprint="MyMod_Quern"
          Cost="18" Ticks="3600" Styles="all" Category="craft" Plot="M" MinStage="Steading"
          Staff="2" Manning="scaled" Carries="food:4,craft:1" />
```

| Piece | What it does |
|---|---|
| `<part Name="Mill" .../>` | Makes the object a mill. A **blank** transformation target falls through to vanilla's preserve path (a vinewafer becomes three sheaves); a `From:To` target is a straight one-for-one replacement. |
| `MechanicalPowerTransmission IsConsumer` | Optional, and the reason to want it: a mill on the grid is visibly driven by the settlement's water wheel or crank mill while you are standing there. |

**`Carries="food:N"` is derived here too.** The settlement grinds `KingdomRules.MillCropsPerDay`
crops a day at `KingdomRules.PreserveMultiple` back, so the honest `N` is
`MillCropsPerDay × (PreserveMultiple − 1)` — four with the shipped numbers. `_notes/balance-sim.py`
§G3 asserts it against the catalogue.

Two things worth knowing before you build one:

- **The part and the settlement grind different stock.** The `Mill` part works the mill's *own*
  inventory while a player is present, at vanilla's per-crop numbers. The settlement pass grinds
  the *larders*, on the settlement's clock, at the flat mod ratio. Neither counts the other's work.
- **Industry never eats first.** The grinding runs after the day's rations are drawn and only
  touches stock above one more day's bill, so a mill can never starve the people who built it.

### Yard trades: a house's own sideline (all optional)

A small or middling roofed house (`Plot="S"` or `"M"`, `Category="housing"`, not `Open`) with a
free cell inside its rect and outside its walls can take up ONE yard trade. The household living
there takes it up; letting one go is free and returns nothing.

```xml
<yardwork Key="hiderack" DisplayName="hide rack" Blueprint="r_KingdomHideRack"
          Trade="tanning" Shades="craft:1" />
```

| Attribute | Default when absent |
|---|---|
| `Key` | Required. |
| `DisplayName` | Required. |
| `Blueprint` | Required. The object placed in the yard. |
| `Trade` | The trade a household is said to take up. Falls back to `DisplayName`. |
| `Shades` | Nothing. A `support:amount` list in the same language `Carries` uses on a `<building>`, summed and capped small (`KingdomYardRules.MaxShadePerWork`) so a household sideline never competes with a purpose-built work. It lands in the settlement's own level beside the house's `Carries` — a `food:1` vine lattice feeds one more person — and unlike a building's lift it is **not** reach-scoped: a household's trade has no plot of its own to shade, so what it makes goes to the settlement. |
| `Goods` | `No`. `Yes` marks a trade whose output is a caravan good **instead of** anything the settlement's own equilibrium reads, so `Goods="Yes"` and nonempty `Shades` are mutually exclusive. Each exact built household with its one matching physical yard fixture adds one dram to every due charter cycle, capped at four households/four drams per caravan. Missing, moved, mismatched, duplicate, or released fixture evidence adds nothing. Adjusted per-cycle income is frozen in the ordinary trade receipt before water, caravan, standing, or schedule mutation; an open delivery never reprices on reload. |

Entries live in their own file with root `<kingdomyardworks Schema="1">` (`KingdomYardWorks.xml` ships the
first-pass set: vine lattice, hide rack, dye vat, vellum press) and are keyed by `Key` the same
way every other registry is: a later file re-using a Key owns that trade's whole spec.

### Quality of life: what a place provides, and who will live in it (all optional)

One open vocabulary joins the two halves of a settlement. **Buildings provide** tags; **residents**
need them, prefer them, or refuse them. Both sides are open namespaced strings — a tag this mod has
never heard of is never an error, and a tag nothing yet consumes simply waits for its consumer.

```xml
<building Key="chargingpost" DisplayName="charging post" Blueprint="r_KingdomChargingPost"
          Cost="12" Ticks="2400" Styles="all" Category="craft" Plot="S"
          Provides="taf:charge" />
```

| Attribute | Default when absent |
|---|---|
| `Provides` | Nothing declared. A comma list of namespaced tags. Case and whitespace are folded; repeats collapse. Merges by key like every other attribute, and reaches buildings that already stand — a mod that adds a tag today changes who will live in a house raised a year ago, and moves nothing. |
| `Closeness` | **Measured.** `Packed`, `Close`, `Roomed`, or `Private` — how much of a quarrel these quarters will hold (see [below](#how-close-the-quarters-are)). Case and surrounding whitespace are folded; any other word is logged and the design is measured instead. Merges and re-reads exactly like `Provides`. |
| `Reach` | **Derived.** `plot`, `quarter`, `zone`, `city`, or `realm` — how far what this design gives actually carries (see [below](#how-far-a-building-carries)). Case and surrounding whitespace are folded; any other word is logged and the design is derived instead. Merges and re-reads exactly like `Provides`. |
| `CrewNeeds` | Nothing demanded. A `kind:amount` list in `Carries`' own language (`strength:16`) naming the first positive capability used to rank a crew and set raising/running pace. Shipped kinds are `strength`, `intelligence`, `skill.tinkering`, `skill.harvestry`, `skill.customs`, `skill.physic`, and `skill.wayfaring`; skill thresholds are presence checks (`1`) against the settler's real vanilla skills. Unknown kinds remain mergeable and are logged, not fatal. A shortfall never blocks a tier or stalls a crewed work — it runs slower, floored, and says so once. `fieldrows` uses `skill.harvestry:1`; that skill affects pace, not eligibility or automatic improvement. |

#### Raising gangs and map-state signs

`CrewNeeds` is not only a finished-work check. After the water detail and running works take their
people, the settlement gives at most `KingdomRules.RaisingHandsWanted` real, named, unposted
settlers to one active raising. The oldest start tick wins; ties are north, west, then stable object
identity. Other frames are visibly queued and their elapsed interval is spent without progress, so
one pair of hands never raises two buildings and idle time never banks. Capability, built-in
culture/species affinity, and API-v2 affinity providers use the same frozen allocator for raising
and running.

Do not write the private `r_TAF_ConstructionCrew*` or `r_TAF_Visual*` properties. They are runtime
receipts, not extension switches. A design participates by using the ordinary data contract:
`CrewNeeds`, `KingdomStaffNeeded`, real power parts/brownout, `r_KingdomWear`, and the normal
construction/strike/repair routes. The stateless state reader then derives its sign from those
facts. It never trusts a decorative “broken” or “idle” flag and never creates an overlay object.

Shipped signs use distinct text glyphs plus existing Qud wrench, toolbox, power-cut, broken-arrow,
and rubble tiles; no vanilla bitmap is redistributed. Run `kingdom:visuallegend` for the versioned
canonical legend/hash and `kingdom:visualaudit` for the actual state rows on the current ground.
Any custom tile a mod supplies still follows the ordinary asset provenance, fallback, and native
scale review rules; color alone is not a readable state.

A plot also provides what its **roof** gives, whether its author thought about it or not: `Open` and
`Soft` tiers provide `taf:sky`, `Walled` and `Carved` tiers provide `taf:dark`. That is read from the
same `AdmitsSky` the rest of the plot code uses, so the two can never disagree — **on the surface**.
Underground there is no weather to reach anything, so **every** tier provides `taf:dark`, the open
plot included.

The tags this mod ships and promises to keep meaning the same thing:

| Tag | Means |
|---|---|
| `taf:charge` | Somewhere to draw charge. |
| `taf:openwater` | Open water at the door. |
| `taf:damp` | Damp: a cellar, a cistern room, a fungal bed. |
| `taf:dark` | Out of the sun. Derived from the roof — and from the ground: every tier underground is dark. |
| `taf:sky` | Open sky overhead. Derived from the roof, surface only — no roof admits sky under the rock. |
| `taf:quiet` | A room away from the noise of the day. |

A settling notable's stated taste uses the same namespace, one tag per building category —
`taf:food`, `taf:housing`, `taf:knowledge` and the rest of `BuildEntry.Category`'s ten names — so a
design of that category meets that taste by exactly the rule a `Provides` meets a `Needs` by.

`r_TAF_LegendaryTrader` is a stricter guest blueprint tag. It opts that guest into the shipped
luxury contract: an exact, sound, wholly vacant `finehouse` on at least an M lot and a live staffed
shop tier of 3. The settled body is bound through ordinary `KingdomLodgingPlotId` and must carry a
`GenericInventoryRestocker`; a manor or generic large roof never aliases the named fine-house
good.

Plain and notable visitors share the settlement's serialized lifecycle book. Population-table
entries provide bodies only; they do not own clocks, passage catch-up, placement, water, removal,
lodging, Chronicle, or guestbook writes. Extensions must not write `NextGuestTick`,
`GuestDepartTick`, `NextNotableGuestTick`, `NotableGuestDepartTick`, or the private lifecycle slots.
The tagged legendary route freezes the exact fine-house object and warranted shop tier before any
water or roster mutation, so replacing its blueprint must retain `GenericInventoryRestocker` and
the tag's fine-house contract.

Settler, plain-guest, notable-guest, and legacy furnishing tables are semantic catalogues. They must
use `Style="pickone"` and may contain only direct `<object>` rows with a real blueprint,
`Number="1"` (or no `Number`), and a positive fixed `Weight`. `Chance`, `Builder`, nested groups or
tables, `$CALL`, and `Dynamic...` tables are refused for these lanes. TAF reads Qud's already-merged
table, folds duplicate blueprint weights, sorts blueprint keys ordinally, and draws with the
settlement's versioned counter stream. This preserves third-party simple-row merges while making
load order, retry cadence, and unrelated engine rolls irrelevant. The chosen blueprint and every
dependent name/origin/profile/coordinate are frozen before object creation; changing a table affects
future events only and never rerolls a live receipt.

`r_KingdomGuestPilgrim` is deliberately **not** in `r_KingdomGuests`. Pilgrims are caused
visitors, not a skin another fixed traveller roll can choose: qualifying disputed city happenings
accrue in that city's book, and one threshold crossing freezes one cause/date/place/sequence for
`KingdomLocus` to render at the rite ground. Other mods may still merge ordinary travellers into
`r_KingdomGuests`; doing so does not create history-caused opportunities. There is no public
pilgrim-cause registry in API v2, so a mod must not write the private city columns directly.

Ship your own under your own namespace (`mymod:hearthfire`). Nothing is restricted to the list above.

#### How close the quarters are

How two people feel about each other always bears on whether they can live together; **how much it
bears is the quarters**. You cannot jam five different believers into one bunkhouse and have it be
fine, and the same five in a street of stone houses are neighbours who nod. So cohabitation is not
one threshold but a ladder of four rungs, and a design's rung is normally **measured** rather than
declared: the beds in its `Carries` against the ground its **tier** stands on (its `Footprint`, or
the whole `Plot` for a tier that declares none).

| Rung | Measured at | Refuses a creed hostility of | Shipped designs |
|---|---|---|---|
| `Packed` | under 4 cells a bed | **1** — any filed dislike at all | tent, staked tent-row |
| `Close` | under 6 cells a bed | **50** — the ambient grudge most factions hold toward strangers | timber hut, hut and yard |
| `Roomed` | under 10 cells a bed | **75** — open hostility, filed on purpose | stone house, housing court |
| `Private` | 10 cells a bed or more | **75** — the same as `Roomed`: walls between beds are the last tolerance architecture buys | fine house, manor |

Hostility is 0–100, read off the engine's own faction table both ways (the worse direction wins), and
same-creed always reads 0 — believers of one creed share anything, including a bunk row. An authored
`Refuses` tag is **not** on the ladder: it is an absolute refusal at every closeness, because it names
something about the other person that a wall does not fix.

The consequence is intended: **a diverse city has to build better housing to exist at all.** Belief
diversity is a thing you build for, in stone.

Declare `Closeness` only when the measurement reads your design's ground wrong. The base catalogue
declares it twice, both for the same shape — a design that raises *several dwellings at once* inside
one plot (`housecourt`, `terrace`) puts many beds on little ground and measures as one packed room
when what is really there is the stone house's own walls repeated.

#### How far a building carries

Reach is derived, so a design that says nothing is on the ladder correctly the day it is written:
an `S` plot shades the ground it stands on, `M` its own quarter, `L` its whole zone, and `XL` the
city — or the whole realm, if it is the last link of its own `UpgradesTo` chain. Tier also moves
the edge inside the band: each step along a chain carries two cells further into its quarter, to a
cap.

Only **lifts** are scoped. `water`, `food` and `roof` are drawn and carried, so they stay citywide
pools whatever supplies them; `craft`, `spirit`, `learning`, `order`, `luxury` and any good another
mod invents shade only what their work reaches.

Scoped in two places, and both matter to an author. What the ground *feels like* — the quarter's
name in the status report, whether a shrine reaches a home for creed conversion, whether a
scriptorium softens a household's grudge — is the reach test on one cell. What the settlement's
**level** gets is the same test summed: a work lands the share of the settlement's roofs it
covers, so a `zone` or `city` design lands its whole amount, a `quarter` design lands what its
cluster holds, and a `plot` design lands nothing on the level at all. An `S` design is therefore
worth building for its ground and never for the population count — which is what "the wayside
statue stays worth building forever" means here.

A **quarter** is measured, never declared: built ground within six cells of built ground is one
quarter, transitively, and a work's quarter is the cluster it stands in. Nothing is stored, so a
quarter that grows, splits, or is struck is simply measured differently next time.

An `XL`'s citywide effect is live only while a **named notable heads it** — the temple's keeper of
rites, the great scriptorium's archivist. Nobody is appointed: the office machinery names whichever
settler present is actually suited to the work, read off the attributes the game already gives
them. A great work nobody heads is not broken — it keeps its own zone and says so once.

Declare `Reach` only when the derivation reads your design wrong.

Note that this reads the same faction feelings that city **dissent** does, and answers differently on
purpose: *polity is not proximity.* Dissent asks whether two cities a day's walk apart can be one
realm, so it accrues slowly and a rite of shared water can still put it out. Cohabitation asks whether
two people sleep in one room tonight — no accrual, no countdown, just yes or no at the door — and what
it scales on is architecture, because a wall between two beds is a thing you can pay stone for.

#### The resident side is DERIVED first

Before any tag of ours is read, a creature's requirements are derived from what the game already
knows about it. **A creature from any mod is a correct resident before its author writes a single
tag.**

| Vanilla truth read | Derived |
|---|---|
| `Robot` part, or the `Robot` tag/property | needs `taf:charge`; eats and drinks nothing |
| no `Stomach` part | eats and drinks nothing |
| `Inorganic` part, or `Physics Organic="false"` | eats and drinks nothing |
| `Aquatic` part, or `Brain Aquatic="true"` — and not flying | needs `taf:openwater` |
| `LiveFungus` tag | needs `taf:damp`; prefers `taf:dark` |
| `PhotosyntheticSkin` mutation | needs `taf:sky` |

So a robot inheriting vanilla's `BaseRobot` needs a charging post and never touches the larder; a
fungal creature inheriting `BaseFungus` wants a damp cellar; a photosynthetic settler can live under
canvas (`Soft` admits sky) and not in a sealed stone house. Nothing derives a *refusal* — that is a
person's own line, and it is either authored or read from faction feelings.

#### Refining it on a creature blueprint

Four tags refine the derivation, using vanilla's own mergeable blueprint-tag mechanism:

```xml
<object Name="MyMod_Sporeling" Inherits="BaseFungus">
  <tag Name="r_TAF_Prefers" Value="taf:quiet" />
  <tag Name="r_TAF_Refuses" Value="taf:charge" />
</object>
```

| Tag | Means |
|---|---|
| `r_TAF_Needs` | **Hard.** A place that does not provide all of these is not a place they move into, and no job there is theirs. |
| `r_TAF_Prefers` | **Soft.** Met ones shade the settlement's equilibrium up by a small capped amount. An unmet one is never a penalty — it just means their default. |
| `r_TAF_Refuses` | **Hard and negative.** A place that provides any of these is refused however well it meets the needs. |
| `r_TAF_Provides` | What sharing a roof with them does to the room. Defaults to whatever they need — the fungal settler's cellar is damp — so you rarely write it. |

Every resident also presents `species:<GetSpecies()>` to the same cohabitation check. This is an
open derived self-tag, not a shipped species list: `r_TAF_Refuses="species:ooze"` therefore refuses
an ooze housemate, including an ooze added by another mod, while ordinary building offers remain
unchanged. Culture is knowledge-shaped and does not enter this body/QoL lane.

Authoring **adds** to the derivation. To argue with a derived tag, name it with a leading `-`:
`r_TAF_Needs="-taf:sky"` on a photosynthetic people who live indoors quite happily. Blueprint tags are
a dictionary, so a child blueprint that re-declares one of these overrides its parent's whole string
rather than appending to it; that is the game's own mechanism, and the `-` prefix is what makes it
workable.

#### What a mismatch does

The match does not happen, and it is **said out loud** — *"Vashti will not sleep beside the fungal
cellar."* Nobody is evicted, nothing is destroyed, no meter moves and nothing decays. Cohabitation
reads the engine's own faction feelings for the ideological cases — scaled by the home's own
[closeness](#how-close-the-quarters-are), so a tent refuses what a stone house carries — and these
tags for everything else, where a refusal is absolute at any closeness.

Housing does **bind**, though. A settler joins the settlement only if a home is already standing that
they would take — needs met, a bed free, and nobody in it they refuse — and the refusal names the
real reason rather than a bed count. A settler who *loses* every acceptable home does not start a
countdown. They are recorded at a **brink** — nowhere to live, the tick they reached it, and the
accrual stops there — and word is *pushed* to the founder wherever they happen to be, once, saying
how long it has really been and naming what would keep them (raise a bunk, stake a plan, re-house
them). From that warning they have six **world-days**, and if those days go by with the settler
still unroofed they leave, whether or not anybody was there to watch — reported afterward with the
day it happened on. Nothing accrues past the brink, so an absence of ten days and one of a thousand
still arrive at exactly the same brink; what cannot happen is losing somebody you were never told
about. Give them a roof at any point before the window runs out and the brink lifts, the warning is
unsaid, and nothing is remembered against them. Turn the whole of it off with
the **settlers are assigned to specific homes** option.

### Skins: what a design looks like

A `<building>` may carry `<skin>` children. Each is a `Render` override the founder is offered when
they commission the design, with the one matching the city's own `Style` suggested; a skin naming no
`Style` is offered everywhere and suggested nowhere. A blank field leaves the design's own value
alone.

```xml
<building Key="bunk" DisplayName="communal bunk" Blueprint="r_KingdomBunk"
          Cost="4" Ticks="1200" Styles="all" Category="housing">
  <skin Key="verdant" Style="verdant" ColorString="&amp;g" />
  <skin Key="bleached" ColorString="&amp;Y" />
</building>
```

`Key` is required and unique within one design; a skin overriding none of `ColorString`,
`DetailColor`, `RenderString`, or `Tile` is refused rather than accepted as a no-op. A skin only
ever **names** art — point `Tile` at a tile that already exists, vanilla or one your own blueprint
ships.

A repeated skin key in a **later file** replaces that skin where it already sits, so re-colouring
one skin never reorders the founder's list; the same key twice inside **one** `<building>` element
is still refused. Re-declaring an entry no longer discards the skins earlier files gave it — see
[Layering](#layering-what-happens-when-two-mods-name-the-same-building).

Any building the settlement raised can also be given a name by the founder, from the Charter, and
that name is what the chronicle, the ledger, and the settlement's own messages call it from then
on — including after it grows into something else.

### Layering: what happens when two mods name the same building

A `<building Key="X">` that a later file declares again **merges** into the design already loaded.
Load order is the game's own mod order, and later wins:

- attributes the later file **names** override;
- attributes it **omits** survive;
- `<skin>` children **append**, and a repeated skin key **replaces** the earlier skin in place;
- an `UpgradesTo` chain can be **extended** by a file that declared neither end of it.

Naming an attribute blank is not the same as omitting it. `Contents=""` erases an inherited
furnishing table; leaving `Contents` out keeps whatever the earlier file said.

The catalogue half of a mod that makes the fine house dearer, recolours it, and gives it a new
**same-size, same-function** tier is:

```xml
<?xml version="1.0" encoding="utf-8"?>
<kingdombuildings Schema="1">
  <!-- Merges into the base catalogue's finehouse. Blueprint, plot, materials, staff, category,
       gates and every skin it already had are untouched, because this file does not name them. -->
  <building Key="finehouse" Cost="24" UpgradesTo="mymod_lacqueredhouse">
    <skin Key="verdant" ColorString="&amp;G" />       <!-- replaces the base skin of that key -->
    <skin Key="mymod_lacquer" ColorString="&amp;m" /> <!-- appends -->
  </building>

  <!-- The new link the chain above points at, declared in full because nothing declared it. -->
  <building Key="mymod_lacqueredhouse" DisplayName="lacquered fine house"
            Blueprint="MyMod_LacqueredHouse" Cost="40" Ticks="4800"
            Styles="all" Category="housing" Plot="M" MinStage="Town"
            Materials="marble:12,shapedtimber:6,canvas:4" Carries="roof:4,luxury:4" />
</kingdombuildings>
```

That catalogue XML is not the whole route. `finehouse` can stand on actual M, L, or XL lots, so the
mod must also merge a Level 1 `mymod_lacqueredhouse` tier into each frozen exact binding below and
ship its three exact maps. Each successor tier repeats the predecessor tier's `require` set and
variant selector roster, and keeps every variant's `main` coordinate fixed:

| Actual size | Plan / binding to extend | Successor map size |
|---|---|---|
| M | `fine-house` / `housing-m-fine-house` | 8×6 |
| L | `lot-l-fine-house-housing-m-fine-house` / `lot-l-housing-m-fine-house` | 12×9 |
| XL | `lot-xl-fine-house-housing-m-fine-house` / `lot-xl-housing-m-fine-house` | 20×14 |

For example, the M merge starts with this real tier shape; the L and XL merges use the corresponding
plan/binding/map keys from the table:

```xml
<plan Key="fine-house">
  <binding Key="housing-m-fine-house">
    <tier Key="mymod-lacqueredhouse" BuildKey="mymod_lacqueredhouse" Level="1"
          Map="mymod-lacqueredhouse-m1" Palette="housing-marble-hands">
      <require Role="main" Min="1" />
      <require Role="entrance:public" Min="1" />
      <require Role="function:dwelling" Min="1" />
      <require Role="fixture:sleep" Min="1" />
      <require Role="fixture:storage" Min="1" />
      <require Role="fixture:hearth" Min="1" />
      <require Role="fixture:table" Min="1" />
      <variant Key="fallback" Priority="0" />
      <variant Key="broad-bodied" Priority="60" Bodies="broad-bodied" />
      <variant Key="hindren" Priority="40" Cultures="Hindren" />
      <variant Key="hindren-body" Priority="39" Species="hindren" />
    </tier>
  </binding>
</plan>
```

A catalogue-only link, an L-sized successor, or a successor tier in only one of the three bindings
is not an improvement route. The exact-route checker rejects it instead of allowing runtime to
rebind, resize, or guess a map.

A merge that names a key **nothing** declares is not an error: it simply becomes that key's first
declaration, exactly as a re-used key always has. It is reported to the log when it is too thin to
stand on its own — no `DisplayName`, `Blueprint`, `Cost` or `Ticks` — because that shape is nearly
always a mis-spelled key, and a mis-spelled key changes nothing at all.

**A merge never rewrites paid construction truth or a city that is already built.** What a
settlement already spent (`Cost`, `Ticks`, `Materials`), what it cut into the ground (`Blueprint`,
`Plot`, `Footprint`, `Roof`, `Open`, `Contents`), and its plot/frontier classification and final
`Defence` belong to the receipt accepted before the first debit. Your update does not refund,
re-charge, move, resize, or reclassify it—even when projection is still waiting across save/load.
Everything intentionally read as current policy does follow the merge: the name, `Carries`,
`Staff`, gates, chain, and skins. Thus an operating rebalance can land on standing works and a skin
added today can dress a house raised a year ago, while a changed defensive score applies only to a
future commission. That split is enforced by `KingdomMergeRules` and construction-registry v4;
unreconstructable unprojected v1-v3 defensive receipts quarantine rather than guess.

The same receipt law governs cross-run inheritance. A current seal made while its seat is loaded
copies each standing authored work's exact frozen `a2` snapshot — selected map, facing, anchors,
palette and placement blueprints — plus the connected zone-relative street graph. Reconstruction
does not ask the now-merged building or architecture catalogue what that old work should look like.
If one blueprint named by a third-party frozen snapshot is no longer installed, that whole work
becomes one empty, receipted cairn on its old main cell; the importer does not invent substitute
walls or discard the rest of the street. Contents, creatures, liquids and charge never cross. An
`ExistingAuthority` placement such as the founding basin becomes a memory marker and is never
duplicated in the successor world. Pre-spatial seals remain readable through the bounded legacy
anchor proxy.

## The protection contract

Kingdom systems never touch what players place. Containers join the city stores only when
dedicated (`KingdomStores=1` — commissioned storage auto-dedicates; everything else is
opt-in via the Charter). If your mod's structures should participate in the water economy,
either set that property on your placed objects or leave it to the player's dedicate
action. Never rely on the kingdom consuming undedicated liquids — it won't.

The same contract covers food. A container joins the settlement's pantry only when it carries
`KingdomLarder=1`, which the Charter's dedicate action sets and a commissioned pantry sets for
itself. Food inside a dedicated larder is counted, is filled by the settlement's own fields, and
is eaten by its people day by day; food anywhere else — including the player's own pack and any
container they simply left lying about — is never read and never spent. Dedication is a mark, not
a transfer: nothing is moved when a container joins the pantry.

**Dedication order is a stored fact, and the city draws in it.** The first settlement pass that
counts a container as the city's stamps `KingdomDedicationOrder` on it — an increasing realm-wide
number that never moves afterwards. When the city has to take drams or servings out of stores the
founder is not standing over, it takes them **oldest dedication first**, so the newest thing the
player dedicated is the reserve that outlives everything else. Do not set that property yourself:
an unstamped container sorts last, which is the right answer for something the city has not yet
counted, and a hand-written ordinal only lies about when the city learned of it.

**How much a pantry holds is declared on the blueprint**, not in `KingdomBuildings.xml`, for the
same reason a cistern's capacity is its `LiquidVolume MaxVolume` rather than a catalogue
attribute: `Carries` says what a design adds to the settlement's sustainable *level*, and how much
its vessel holds is a fact about the vessel.

```xml
<object Name="MyMod_ColdCellar" Inherits="Chest">
  <tag Name="r_KingdomLarderCapacity" Value="120" />
</object>
```

**Seed is the founder's designation, and the contract runs both ways.** A field grows nothing until
the player puts seed in it, the seed is theirs until they take it back out (**Withdraw Seed** on
the field returns it and lifts the rows), and a harvest never gathers anything they did not
dedicate — the crop goes into a `KingdomLarder=1` container or nowhere. The standing crop plants a
field lays are objects this mod created and marked (`KingdomCropRow=1`), which is the only class of
object a kingdom system may destroy; a plant you placed in a field's footprint is never taken up
and never counted. A wild plant carrying `r_KingdomWildSeed` gives its seed once and refuses
outright if it has a `Physics Owner` — a farmer's crop is a farmer's, and the mod will not help the
founder rob one.

A dedicated container that declares nothing gets `KingdomRules.DefaultLarderCapacity` (32) — never
zero, because a pantry that can hold nothing is a silent black hole for a harvest with no surface
anywhere to explain it. Which *commissioned* designs dedicate themselves is the named list
`KingdomRules.CivicLarderBlueprints`; anything else waits for the player's own dedicate action,
which is the protection law working exactly as intended.

Food is denominated in **people**, not in a per-day flow: one point of `food` in `Carries` is one
settler fed for one day, and the settlement eats one ration a settler a day at every rung. Unlike
`water`, food is **not** divided by the stage rate — a dinner is counted in people the way a bed
is. That is what makes the arithmetic check out on its face: a settlement standing at its own
supported level makes exactly what it eats, so a `food` figure you author is a promise about how
many more people the place feeds, not a number that means different things at different rungs.

## Power

The settlement's power is civic labour, not a wiring puzzle, and it extends by parts rather than
by a registry. Two parts are the whole contract:

- `<part Name="r_KingdomPowerWork" Source="Hands|Water|Wind" />` makes an object one of the
  settlement's power works. `Hands` is worth whatever fraction of its `Staff` the settlement
  crewed it with; `Water` also needs open water in or beside its cell (400 drams to turn at all,
  4000 for full output) and never counts a dedicated cistern; `Wind` reads the zone's own wind.
  An unknown `Source` disables the work rather than defaulting it. A `Water` work wants vanilla's
  `<part Name="SpawnWithLiquid" LiquidObject="SaltyWaterPuddle" AdjacentPoolChance="0" />` too —
  it is what vanilla's own wooden water wheel carries, it digs the work a 500-dram brackish race
  so the turbine never reports `HydrodynamicForceInsufficient` on dry ground, and because the race
  is a mixture rather than pure water the settlement can never drink or haul it. 500 drams is two
  per cent of the rating, so the wheel turns anywhere and is only worth siting beside real
  standing water.
- `<part Name="r_KingdomPowerStore" />` on any object that also carries a vanilla `Capacitor`
  makes it a store the settlement pours into and draws back from. Its `MaxCharge` is the capacity;
  set `ChargeRate="0"` and `MinimumChargeToExplode="0"` unless you intend otherwise.

What the works make is ordinary vanilla charge, delivered through vanilla's `ChargeAvailableEvent`.
Anything the settlement built (`KingdomBuilt=1`) or that the founder dedicated (`KingdomGrid=1`)
and that accepts charge is filled from it — a charging post, a kiln, your own machine. Nothing the
player merely left standing about is ever charged or read. The settlement does **not** use
vanilla's `IPowerTransmission` grid, so you do not need to run conduit to anything.

One more surface, for anything that spends more than its share: `KingdomDailyDraw` — an int
property any settlement-built object may carry to declare what it spends on the power lane in a
day. Absent or zero means one charging post's worth (`KingdomPowerRules.PostDailyNeedCharge`).
The mirror-gate declares three.

## Happenings, ambience, and what a creed thinks of the founder

The city's happenings are **renderings of its own rows** and there is no happenings registry to
extend. What you extend instead are the surfaces they read from — which means a faction, a
mutation, or a design your mod ships is already part of the city's life without your mod knowing
this one exists.

**A creed's opinion of the founder's body is vanilla's, not ours.** If your faction can be a city's
creed, give it the same two things Qud gives its own:

```xml
<faction Name="YourCreed" ...>
  <!-- The game's own table: which bodies this faction admires or fears. -->
  <partreputation About="Wings" Value="200" />
  <partreputation About="MassMind" Value="-200" />
  <interests>
    <!-- Plain: they revere chrome. Inverse="true": they define themselves against it. -->
    <interest Tags="cybernetics" />
  </interests>
</faction>
```

`About` is a **part class name** — mutations are parts, so `Wings`, `Horns`, `PhotosyntheticSkin`
and anything your own mod adds all work. The **sign** of `Value` is the whole judgement; this mod
has no opinion of its own about any mutation. The reaction is always a line and never a mechanic:
nothing about it moves standing, refuses a settler, or changes what the settlement produces.

**Breakdowns and ambience read work rows, so a design you ship participates for free.** A work is
"stopped" when it is worn past the condemned line (the same line the housing machinery condemns a
roof at) or when its kind needs hands and has none — producers, refiners and power works do; stores
and growing grounds do not, because a larder with nobody in it is still a larder.

**Festivals are anchored to Qud's calendar and there is no third anchor.** The engine ships no
holiday machinery at all, so the mod uses the only two named days that exist: the **Ides**
(`Calendar.GetDay` returns the literal string for the fifteenth) and the **festival of Ut yara Ux**
(the five-day intercalary month). The feast serves `Faction.WaterRitualRecipeText` — the realm's
own dish — so declaring a recipe on your creed faction changes what the city eats.

**Office-holder names come from `Naming.xml`.** The settlement's office holder is named through
`NameMaker` under vanilla's `Special="Mayor"` scope, so a `<namestyle>` you add for `Mayor` is a
name the founder's water-keeper can be given. None of `HeroMaker`'s statistics are applied — an
office holder is a person with a name, not a legendary combatant.

**Option gate.** All of it reads `r_TAF_OptionHappenings`, default `Yes`.

| Surface | What extending it does |
|---|---|
| `<partreputation About="X" Value="N"/>` on a creed faction | That creed admires (positive) or fears (negative) a founder carrying part `X` |
| `<interest Tags="cybernetics"/>`, with or without `Inverse` | That creed reveres or refuses chrome |
| `WaterRitualRecipe` / `WaterRitualRecipeText` on a creed faction | What the city serves at its feasts |
| `<namestyle Type="Epithet" Special="Mayor">` in `Naming.xml` | Epithets the settlement's office holder can be given |

## The behaviour lane: extending the model in C#

> Everything above this line is the **data lane** — XML merged by key, no code, no dependency. It
> is the lane most extensions want, and nothing here replaces it. This chapter is for the other
> case: a mod that wants the city to *do* something new.

Published at **API version 3**; version 1 ask/happening and version 2 identity sources remain
admitted. The durable resource/carrier/job/network/work family requires version 3. The contracts live
in the `ThousandAndFirst.Api` namespace, and a
third-party mod needs **no hard reference beyond that namespace** — not to our systems, not to our
carrier, not to our version of anything else.

Option gate: `r_TAF_OptionExtensions`, default `Yes`. With it off, the data lane is unaffected and
no third-party code runs against the city.

**The engine compiles your C# fresh, in-process, at mod load** — a clean desktop `csc`/IDE build does
not guarantee that. The in-game Roslyn host has flagged patterns desktop `csc` accepts: for example
CS0165 (definite assignment) on a conditional-access `TryGetValue` out-var chain
(`The.Game?.X.TryGetValue(k, out var v) == true`), where pre-declaring the out variable instead is
what the in-game compiler wants. The verdict is `Player.log`: `MODERROR`/`MODWARN` lines naming your
mod mean it failed or warned to compile; their absence means it loaded clean.

### What is published

| Contract | What it extends | State |
|---|---|---|
| `IKingdomAskSource` | what the city asks its founder for | **published** |
| `IKingdomHappeningSource` / `IHappeningGenerator` | what happens in the city, told through our surfaces; the latter is the canonical §6.6 name and inherits the compatible v1 contract | **published** |
| `IKingdomIdentitySource` | extra live identity keys and work affinity for open culture/species vocabularies | **published in v2** |
| `IResourceKind` | extension-owned civic goods, their capacities, optional container metadata, and optional network/liquid identity | **published in v3; durable** |
| `ICarrierKind` + `IJobKind` | paired carrier definitions and exact-tick, cargo-reserving, timed jobs | **published in v3; jobs durable** |
| `INetworkKind` | bounded held-zone source/sink/relay graphs, capacity solve, brownout, and daily surplus | **published in v3; durable solve state** |
| `IWorkBehaviour` | opaque run state and atomic owned-resource/output advances on an existing city work | **published in v3; durable and physically consumed** |

These are live model contracts. Their resource levels, frozen in-flight jobs, latest network solves,
work state, and owed physical outputs share one bounded sidecar in the city book. Check-in and
heartbeat advance it. Settlement archive v7 is the first schema to carry that behavior sidecar;
every schema from v7 through current v17 preserves it through exile and seat exchange, while frozen
v6 defaults it empty and v8–v16 add later sidecars independently. An attended pass lands owed work
objects on the exact work cell. A malformed sidecar is retained and reported, never reinterpreted
as empty. Disabling an owner prevents new proposals but does not stop the host from settling a job
whose carrier, route, cargo, and completion were already frozen.

### How registration works

Discovery is the engine's own idiom: a cached attribute scan over every active assembly
(`ModManager.GetTypesWithAttribute`), the same mechanism the game uses for `IWorldBuilderExtension`,
wishes and debug commands. Mark the class, implement a contract, declare the version:

```csharp
using ThousandAndFirst.Api;

[KingdomExtension]
public sealed class SaltCultAsks : IKingdomAskSource
{
    public int ApiVersion => KingdomApiRules.Version;

    public KingdomAsk[] Ask(KingdomCityReading City, IKingdomDraws Draws)
    {
        // Read the frozen city. Nothing here may write to the world.
        for (int i = 0; i < City.WorkCount; i++)
        {
            KingdomWorkReading work;
            if (!City.TryWork(i, out work) || work.Class != KingdomWorkClass.Power)
            {
                continue;
            }
            if (work.Progress > 0)
            {
                continue;
            }
            // A deterministic draw, on this mod's own stream. Never System.Random.
            int flavour;
            Draws.TryBetween("flat-cell", (uint)work.WorkId, 0, 1, out flavour);
            return new KingdomAsk[1]
            {
                new KingdomAsk(
                    Kind: "flat-cell",
                    Title: (flavour == 0)
                        ? "The salt-cult's array is flat."
                        : "The array has nothing left in it.",
                    Want: "Charge it, or set a crew on a still that can.",
                    ZoneId: work.ZoneId,
                    Weight: KingdomAskWeight.Pressing)
            };
        }
        return null;
    }
}
```

The class needs a **public parameterless constructor** — the scan builds it with
`Activator.CreateInstance`. Your asks appear on the Charter's *What the city is asking for* board,
filed under `<your-manifest-id-slug>:<your-kind>`, after the city's own. The owner is your immutable
manifest `id`, never the display title; changing that ID after publishing orphans the old namespace.

A happening source is the same shape:

```csharp
[KingdomExtension]
public sealed class SaltCultRites : IKingdomHappeningSource
{
    public int ApiVersion => KingdomApiRules.Version;

    public KingdomNotice[] Happen(KingdomCityReading City, long SinceTick, IKingdomDraws Draws)
    {
        if (SinceTick <= 0L || City.LivingCount < 4)
        {
            return null;
        }
        return new KingdomNotice[1]
        {
            new KingdomNotice(
                Kind: "salt-vigil",
                Tick: City.ProcessedThroughTick,
                Telling: "the salt-cult of " + City.CityName + " kept a vigil, and nobody slept",
                Notice: "Nobody slept last night.")
        };
    }
}
```

An identity source extends the two places Addendum 17 opens to behaviour code, without receiving a
mutable creature:

```csharp
[KingdomExtension]
public sealed class MyPeopleIdentity : IKingdomIdentitySource
{
    public int ApiVersion => KingdomApiRules.Version;

    public string[] Keys(KingdomIdentityReading Identity)
    {
        return string.Equals(Identity.Culture, "My People",
            StringComparison.OrdinalIgnoreCase)
            ? new[] { "glassworking" }
            : null;
    }

    public int Affinity(KingdomIdentityReading Identity, string WorkKind)
    {
        return string.Equals(Identity.Species, "My Species",
            StringComparison.OrdinalIgnoreCase)
            && WorkKind == "craft" ? 115 : 100;
    }
}
```

`KingdomIdentityReading` is bounded frozen `(Culture, Species, Creed, Genotype)`. Unqualified keys
are filed as `<your-manifest-id-slug>:<key>`; a qualified key in another namespace is dropped. At most eight
distinct keys per source and identity survive, from at most the first 32 proposed slots; malformed
entries consume that inspection budget. Affinity is neutral at 100; each answer and the composed
result are clamped to 70–130, with all source deltas summed before the final clamp. Keys may open ordinary `Requires`/`Knowledge` gates and
affinity may shade existing work. Neither interface contains a tier operation: researcher
Intelligence remains the only tier gate.

### Standalone external-fixture recipe

Prove the public seam with a separate cold-installed mod, not another in-tree test class. Create a
sibling mod directory with a permanent unique manifest ID and put all fixture C# beneath one
dependency-gated directory:

```text
taf-api-v3-fixture/
├── manifest.json
└── Source/
    └── Fixture.cs
```

For the current installed manifest, the fixture manifest is:

```json
{
  "id": "taf_api_v3_fixture",
  "title": "TAF API v3 fixture",
  "version": "0.0.1",
  "author": "local test fixture",
  "Directories": [
    {
      "Path": "/Source/",
      "Dependencies": {
        "r_ThousandAndFirst": "0.2.0"
      }
    }
  ]
}
```

Keep the dependency value equal to the exact installed TAF manifest version. Do not place
`Fixture.cs` at the recursive mod root: the dependency-gated directory is what prevents Qud from
compiling its API references when TAF is absent, disabled, wrong-version, failed, or ordered too
late.

In `Fixture.cs`, use only `System` and `ThousandAndFirst.Api`. Give every marked type a public
parameterless constructor, `[KingdomExtension]`, and
`ApiVersion => KingdomApiRules.Version`. Across one or several types, implement all five durable v3
lanes—`IResourceKind`, `ICarrierKind`, `IJobKind`, `INetworkKind`, and `IWorkBehaviour`—plus
`IHappeningGenerator`. Start from the callback examples in this chapter. Use one stable owner-local
key per lane, one held-zone job leg, one priority-ordered source/sink network, one work debt for a
takeable non-creature blueprint, and one dated happening. Do not import or reflect over Core,
`XRL.World`, a zone, an object, the clock, or mutable city state.

Cold-install both mods into a disposable profile. Confirm the Charter registry names
`taf_api_v3_fixture`, then execute TESTING 124i–124p: open/save/reload a job, prove network ordering,
obstruct/retry the work debt, exchange/archive the settlement, disable the fixture while the frozen
job settles, and re-enable the same manifest ID. Retain the save and run
`./Tools/check-player-log.sh /absolute/path/to/Player.log`; record the exact fixture bytes, installed
TAF bytes, manifest IDs/versions, and checked log path with the receipt.

### API-v3 durable behaviour

Every v3 callback receives the same frozen `KingdomCityReading`, the current frozen
`KingdomBehaviourReading`, and its owner's deterministic draw handle. Read durable extension rows
through `City.Behaviour` or the supplied `Model`; both expose only counts plus bounded `Try*`
accessors. Return canonical lowercase owner-local keys such as `ore` or `ore-grid`. The host files
them as `<your-manifest-id-slug>:<key>`. A key already qualified to another owner is refused, not
rewritten. Distinct installed manifest IDs that collapse to one bounded owner slug are both
refused, so load order cannot transfer durable state between them.

Kinds are evaluated in a fixed order so references have one meaning:

| Phase | Contract | Host consumer |
|---|---|---|
| 1 | `IResourceKind.Resources` | creates or reconciles durable owned levels/capacities |
| 2 | `ICarrierKind.Carriers` | validates this pass's blueprint, pace, and capacity definitions |
| 3 | `INetworkKind.Networks` | solves held-zone topology and integrates whole-day surplus |
| 4 | `IWorkBehaviour.Advance` | atomically publishes owned-resource changes, next state, and explicit physical debt |
| 5 | `IJobKind.Jobs` | reserves cargo and freezes the paired carrier plus route for host-owned completion |

This is a minimal resource and paired carrier/job. It omits proposals once a receipt already
exists. A job key is the permanent identity of one logical job, not display text: the host
deduplicates it while the open row or one of the bounded recent terminal receipts remains, and
treats reuse after receipt retirement as a new proposal. Never recycle a job key.

```csharp
[KingdomExtension]
public sealed class CopperHaul : IResourceKind, ICarrierKind, IJobKind
{
    public int ApiVersion => KingdomApiRules.Version; // v3 is required

    public KingdomResourceDefinition[] Resources(
        KingdomCityReading City, KingdomBehaviourReading Model, IKingdomDraws Draws)
    {
        return new[]
        {
            new KingdomResourceDefinition(
                Key: "ore", Unit: "lump", ContainerProperty: "MyModOreStore",
                NetworkKey: "", LiquidId: "", InitialLevel: 8, Capacity: 100)
        };
    }

    public KingdomCarrierDefinition[] Carriers(
        KingdomCityReading City, KingdomBehaviourReading Model, IKingdomDraws Draws)
    {
        return new[] { new KingdomCarrierDefinition("mule", "Dromad Merchant", 2, 8) };
    }

    public KingdomJobPlan[] Jobs(
        KingdomCityReading City, KingdomBehaviourReading Model, IKingdomDraws Draws)
    {
        if (Model.JobCount != 0 || City.ZoneCount == 0)
            return null;
        KingdomZoneReading zone;
        if (!City.TryZone(0, out zone))
            return null;
        return new[]
        {
            new KingdomJobPlan(
                Key: "first-haul", CarrierKey: "mule", CargoResourceKey: "ore",
                CargoAmount: 4, StartTick: City.ProcessedThroughTick,
                Legs: new[] { new KingdomExtensionLeg(zone.ZoneId, 1, 1, 4, 1) },
                CompletionChanges: new[] { new KingdomResourceChange("ore", 6) })
        };
    }
}
```

Opening reserves four `ore` once. The host computes the due tick from each leg's Chebyshev distance
and the frozen carrier pace. At that tick all completion changes commit together; if they cannot,
the job fails and the host restores reserved cargo where capacity permits. The carrier blueprint is
retained in the public job reading as exact journey identity, but v3 resolves that extension journey
model-side; it does not promise a transient visible carrier body.

`INetworkKind` returns `KingdomNetworkPlan`: one owned resource, at most eight held-zone nodes and
twelve undirected capacity edges. Nodes are `Source`, `Sink`, or `Relay`; smaller `Priority` values
win brownout allocation. The host records `LastFlowPerDay` and `LastBrownoutPerDay` and banks only
whole-day source surplus, clamped to the resource's room. A resource's nonempty `NetworkKey` must
match the plan. `ContainerProperty` and `LiquidId` are frozen integration metadata; v3 does not
claim an automatic ground-container scan for arbitrary third-party properties.

`IWorkBehaviour` returns `KingdomWorkAdvance` only for an existing `WorkId`. Its `NextState` is one
opaque `long`; `NextTick` is an absolute breakpoint strictly later than this pass, so two check-ins
at one model tick cannot replay one advance. Up to four nonzero
changes to the same owner's resources publish atomically. One result may also add one
`KingdomMaterialisation` debt. On an attended pass the host creates at most one exact blueprint,
refuses creatures and untakeable objects, proves that exact object landed on the exact work cell,
then acknowledges one unit of debt. Failed creation or placement leaves debt durable for retry.

Bounds are part of the contract: first 32 returned slots inspected per callback; four resources,
four carrier definitions, four open jobs, four recent terminal job receipts, and four networks per
owner; sixteen work-behaviour rows per owner. City-wide limits are sixteen resources, sixteen open
jobs, sixteen terminal receipts, sixteen networks, and sixty-four work rows. One job has at most six
legs and four completion changes. Current decoded sidecar is at most 16,896 bytes; legacy v1 input
remains capped at 16,384 bytes, with exact headroom for all 64 appended v2 generation receipts.
Invalid slots consume the
inspection budget, and a malformed row never partially publishes its surrounding atomic result.

`Telling` goes to the chronicle (both registers). `Notice` is the line a settler says out loud, and
it is spoken **only if the settlement pass has a line to spare** — the city's own news outranks it.

Date a notice inside the window you were given: anything after the pass's own tick, or at or before
`SinceTick`, is dropped rather than filed with a wrong date. Each exact
manifest-ID/exact-assembly/type tuple has its own durable cursor and receives zero on its first
logical call (an upgraded source already called through the retired aggregate lane resumes from
that retained tick). A fault advances only that source on the documented at-most-once policy. The city does not report
the future or re-report the closed edge of the previous window. `City.ProcessedThroughTick` is the
safe default when it is strictly newer than `SinceTick`. At most 128 active happening source types
are called per city; over-cap sources fault loudly rather than sharing another source's cursor.

### The invariants you inherit, enforced rather than trusted

1. **Kernel draws only.** `IKingdomDraws` is the counter-based kernel wearing a published face,
   keyed on `taf:ext:<your-mod>:<your-lane>`. Same city, same lane, same ordinal, same answer —
   across reloads. `System.Random` in an extension is a contract violation and makes the city
   unreplayable.
2. **Frozen in, frozen out.** `KingdomCityReading` is a projection with no setters and no route to
   the ground, the clock, or another extension's rows. Your method returns an array; we copy it.
3. **Budget and error isolation.** Every call crosses `KingdomExecutor.Submit`. A source that
   throws or runs past its lane's budget **stalls its own job and nothing else** — no city state is
   published, the turn is unaffected, and the failure is logged **by your mod's name**. Ask-source
   faults are also named on the asks board. Identity-key and identity-affinity faults are logged
   every time and surfaced on screen once per owning mod and lane; the city keeps running with no
   keys or neutral affinity from that source. **The budget is a verdict, not a timeout**: the seam
   is synchronous, so it can refuse to publish a result that overran but cannot interrupt one. An
   infinite loop in your `Ask` hangs the game, exactly as one in ours would. Return.
4. **Telling through our surfaces.** Ledger, chronicle and `KingdomWord`, under the shared telling
   budget. An extension cannot flood the register any more than we can.
5. **Clamped, including durable rows.** At most 4 asks, 2 notices, and 8 live identity keys from 32
   inspected candidates per source per call; every callback gets at most 32 kernel-draw attempts,
   with the thirty-third refusing that callback as over-budget; identity affinity remains inside
   70–130; and the
   whole board is trimmed to 8 lines after sorting, so ten installed mods cannot turn it into a
   spreadsheet. Every string is stripped of colour markup and control characters and cut to 200
   characters on a word boundary. A `ZoneId` naming ground the city does not hold is read as none,
   and a weight outside the three rungs is read as `Passing` — the mildest — because a malformed
   weight is not a claim of urgency. Clamping is never a refusal: the ask behind an over-long line
   is still real. API-v3 durable rows count against their per-owner, city-wide, candidate, and
   encoded-size ceilings. A source that throws or exceeds the compute/draw budget publishes none of
   that callback's proposal; other owners and later phases continue.

### Versioning, and being refused out loud

`KingdomApiRules.Version` is checked at registration against the window
`[KingdomApiRules.MinSupportedVersion, KingdomApiRules.Version]`. Outside it the extension is
**refused by mod name**, in the log and in the message queue, naming the version it wanted and the
version we publish — never silently skipped, because a player attributes missing behaviour to *us*.
The same refusal fires for a marked class that implements no contract, for one whose constructor or
`ApiVersion` getter throws, and for one whose owning mod cannot be named. The window is what makes
STANDARDS §9's promise keepable: a version bump does not refuse every extension in the world on the
same day. Compatibility is contract-sensitive: a genuine v1 ask/happening type and v2 identity type
still admit, but any type implementing `IResourceKind`, `ICarrierKind`, `IJobKind`, `INetworkKind`,
or `IWorkBehaviour` must declare version 3. A v2 type cannot half-load a v3 stateful lane.

The founder can see the whole registry: **Charter → The book of the city → Who else writes in this
book** lists what is admitted and what was refused, with the reason.

Return `KingdomApiRules.Version`, not a literal. Recompiling against a newer copy of the mod is what
re-admits your extension.

## Raid profiles

Ship `KingdomRaidProfiles.xml` with root `<kingdomraidprofiles Schema="1">`. One profile names a
Qud faction and the creature blueprints its provoked warbands draw from:

```xml
<kingdomraidprofiles Schema="1">
  <profile Key="mymod-glass-raiders" Faction="My Mod Glass Clan"
           Reach="glass singing on the road"
           Steading="MyMod Glass Scout,MyMod Glass Scout"
           Village="MyMod Glass Scout,MyMod Glass Warrior"
           Town="MyMod Glass Warrior" City="MyMod Glass Champion" />
</kingdomraidprofiles>
```

`Key`, `Faction`, `Reach`, and a nonempty `Steading` list are required. Later stage lists are
optional and fall back toward Steading. Re-declaring a faction replaces its profile by load order;
the key belongs to the frozen incident identity, so changing it cancels an already-disclosed force
instead of silently reshaping that force. Every member must be an existing non-base, non-unique,
dynamic-encounter-eligible creature blueprint with a Brain. A profile admits at most 16 comma-
separated members and the merged registry at most 64 factions; malformed entries log and do not
replace an earlier valid profile.

## Conventions

### Civic-experience and polity authority is internal

The v1 civic-experience and polity systems are extensible through their **inputs**, not by editing
their receipts. Buildings/styles/creeds/research/procedures/raid profiles remain mergeable data;
the behavior API remains the supported code seam; exact external ground ownership uses the typed
provider described at the top of this guide. `KingdomExperienceLedger`, civic-memory sections,
First Guest/First Feast, witness/recognition/body-history/vocation books, polity dispatch/profile/
route/cohort/conflict records, and realm-retirement receipts are internal transaction authorities.

Do not set their `r_TAF_*` object properties, write their encoded game-state keys, reflect into
their stores, or treat a presentation marker as permission to mutate the semantic owner. Such state
is authenticated, capacity-bounded, compare-and-swap protected, and may deliberately quarantine a
foreign or future shape. A third-party feature that needs a new durable civic act should register
ordinary public behavior/data and let TAF observe the resulting supported fact; it must not reuse an
internal receipt ID or body reservation. Propose a new public protocol before depending on an
internal type. This keeps other mods independently unloadable and prevents two systems from owning
the same cargo, body, choice, or cleanup lifecycle.

- Water stores are containers (`MaxVolume > 0`) holding `water`; open pools (`MaxVolume < 0`)
  are supply that settlers fetch from. Anything you add that holds water participates
  automatically.
- Citizens are creatures with int property `KingdomCitizen=1` and allegiance to the kingdom
  faction; enroll your own via `ThousandAndFirst.KingdomFounding.EnrollCitizen`.
- The chronicle is append-only prose: call
  `ThousandAndFirst.KingdomChronicle.Record(system, "text...")` from your systems and it
  lands in both registers, dated.
- Deeper integration (typed references to `KingdomSystem`) works the same way any mod
  references another's assembly: the game chain-loads mod assemblies in load order.

More registries (settler origins and districts) are migrating to the same
XML pattern; anything still hardcoded is a defect by our own standards — file an issue.

## Same-set architecture transitions

`Architecture/KingdomArchitectureTransitions.xml` is a mergeable, schema-1 registry for cheap
changes between two building plans that occupy the same typed, actual-sized lot. Each declaration
is directional and must name every part of its economic delta:

```xml
<KingdomArchitectureTransitions Schema="1">
  <transition Key="my-old-to-new-m" From="myold" To="mynew"
    Type="craft" Size="M" Water="12" Materials="scrap:2" Ticks="900" />
</KingdomArchitectureTransitions>
```

`Key`, `From`, `To`, and `Type` are bounded whitespace-free keys. `Size` is `S`, `M`, `L`, or
`XL`; `Water` is a non-negative integer; `Materials` uses the normal material tally syntax; and
`Ticks` is 1 through 100,000,000. Both endpoints must resolve to exact architecture mappings for
that type and actual size. Duplicate routes are refused. Reverse travel needs its own declaration.
If no exact route exists, the action refuses before debit; the engine never derives a price from
the two full build costs.

The built-in early-housing routes show the retained-fabric rule in practice. `tent` changes to
`hut` in verdant/fungal/gyre cities, `mudhut` in common cities, or `blockhut` in eater ruins;
`tentrow` changes to the matching `hutyard`, `mudhutcourt`, or `blockyard`. Every route is declared
separately at S/M/L/XL because actual lot size is part of identity. Their bill keeps the existing
canvas bed/storage fabric and prices only the permanent shell, so it is below strike plus fresh
commission. There is deliberately no hut-to-tent reverse: taking sound permanent walls back to
canvas is demolition, not retained craft.

The founder-facing picker lists a same-set target only when its exact directional route and target
architecture mapping both exist for the standing lot's actual size and current style. Its row and
confirmation show the route's water/material/tick delta, not the target's full build price. The
production target (variant, pose, cells, fixtures, anchors, and delta) is prepared once, rendered
before confirmation, and re-proved unchanged at commit. Retype and build-on-cleared-socket use the
same prepare/preview/commit discipline with their own fresh target. Cancelling any preview writes
no debit, strike, receipt, or map mutation.
