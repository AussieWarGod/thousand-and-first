# Extending The Thousand and First

> Adding content? Stay in this file — the XML registries need no code.
> Writing code against the mod? See [docs/API.md](docs/API.md) for the supported API and its
> stability guarantees.

The mod is a platform: its content registries load through the game's own mergeable XML
streams, so **any mod can add to them by shipping a file with the right root element** —
no code, no dependency declaration, no patching.

## Add buildings and city styles

Ship a `KingdomBuildings.xml` in your mod root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<kingdombuildings>
  <style Name="fungal" />
  <building Key="sporehut" DisplayName="spore hut" Blueprint="MyMod_SporeHut"
            Cost="6" Ticks="1800" Styles="fungal" />
  <building Key="mysterystone" DisplayName="mystery stone" Blueprint="MyMod_Stone"
            Cost="3" Ticks="600" Styles="all" />
</kingdombuildings>
```

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
  declines to guess and builds where the founder is standing. A `Defence` rating overrides the
  category — a thing with a defence value is sited as a wall whatever else it is filed under.
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
an improvement onto a larger plot, a footprint larger than the plot it stands on, a defence
rating on a design that also claims a plot, and a
family no camp can ever reach are all reported to the log. None of them ever unregisters an entry —
a design that is wrong about itself stays buildable and becomes visible, which is the only shape a
check on third-party content can honestly take.

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
- `UpgradeMaterials` — the same, for improving *into* this design. Absent means the improvement
  costs water alone.
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

A charter may carry material as well as water, per caravan:

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
settlers are — Strength for the saw-pit and the banker, Intelligence for the furnace. It runs on
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

Four more attributes decide whether a design may be raised, on top of `Styles` and `MinStage`.
**Every one is optional and an absent attribute gates nothing**, so an entry written before these
existed — ours or yours — behaves exactly as it always did. A malformed one is logged and ignored
rather than deleting the design: the worst case is a design that could have been harder to reach,
never one that becomes unreachable with no way to find out why.

| Attribute | What it wants |
|---|---|
| `Districts` | Comma list of district keys whose ground will take this design: `agrarian`, `market`, `craft`, `shrine`, `garrison`, `academy`, plus `none` for ground the founder has never named. `all` (or omitting it) accepts everywhere. A key we do not recognise is treated as somebody else's district, never as open ground. |
| `MinZones` | Claimed zones the realm must hold. The eight designs that declare 2/3/4 line up with `MinStage` `Village`/`Town`/`City` — see `KingdomZoningRules.ZonesForStage`, which is also the ceiling the founder's own claim is checked against, so a settlement reaches the ground a design wants at the same moment it reaches the stage that wants it. Reachable now: the founder claims bordering ground (including a stratum directly above or below) from the Charter's **Claim this ground**. |
| `Knowledge` | Comma list of things the settlement must know, **all** of them. A requirement written `kind:name` must match that kind exactly; one written as a bare name is satisfied by any kind. Kinds: `disk` (a design taught to the keepers from a data disk the founder carried home — the disk is read and handed back, never spent), `machine` (a machine hauled home and certified fit for the grid), `origin` (a trade the settlement holds because somebody from that country lives there, so it comes and goes with them), `node` (a subject the keepers worked out at a bench — see the research schema below), `rite` (what a water ritual seeded), `book`/`savant`/`culture`/`species` (declared for the identity and lab lanes; an unknown kind gates perfectly well and is worth no craft). **Knowledge lives with the CITY that learned it** (Addendum 22 B-cluster): the seat's rolls answer for the seat, a disk re-teaches freely at the other city, a machine re-certifies where it stands, and a seceding city walks out with what it knew. Invent your own kind freely. |
| `MinTech` | Craft the settlement must have reached: `hands` (the start, gates nothing), `salvage`, `workshop`, `foundry`, `arclight`. |
| `Strata` | Which set of the catalogue the design lives in and which strata it may also stand in (Addendum 15). The **first** welcomed token is the home stratum; the rest are share-tags: `Strata="deep,surface"` lives in the deep and may stand on the surface. Same spellings as `Styles` (`all`, a leading `!` for "everywhere except"). Tokens today: `surface`, `deep`, `sky`, `arcology` — and the set is open, so a third-party stratum names itself. **Absent means everywhere**, which is why every record written before this attribute existed still stands wherever it stood. Sky is a filtered subset of the surface: a list that does not mention `sky` answers for sky ground exactly as it answers for the surface, so only `!sky` (or a sky home) separates them. `Sky="yes"` on the plot spec is a different question — wanting open weather — and is asked first. |

A fifth kind is reserved by convention: `pattern` (a foreign design a chartered caravan
occasionally offers a choice of, never taught by any disk, machine, or origin — see
`Experience/KingdomCeremony.cs`). Write `Knowledge="pattern:some-name"` on an ordinary `<building>`
entry to enter it into that pool; the base catalogue never depends on the draw, so an entry gated
this way is purely additive.

Craft is **derived, never authored and never set**: a taught design is worth 1 and a certified
machine is worth 2, an origin is worth 0, and the level is read off the total — **per city**, judged
against the city a design is being built in. The research tree does not change this: a node MINTS a
roster key like any other kind and is worth no craft. Research TIER (what the keepers can take up)
is a second, orthogonal ladder gated on the city's best researcher's Intelligence, and neither
ladder ever substitutes for the other.

### Research nodes (`KingdomResearch.xml`, root `<KingdomResearch>`)

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
<kingdombuildings>
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

**When an improvement actually fires** is decided by what the city can absorb, never by how long
anything has stood — there is no maturation timer anywhere in this mod. Material and craft gate
every improvement: an entry whose `UpgradeMaterials` the stockpiles do not cover, or whose
`Knowledge`/`MinTech` the settlement has not reached, is refused by name and waits.

Beyond that the two families are judged differently, and both read numbers you author in `Carries`:

- **Housing** (`Category="housing"`) improves when the people living in it have somewhere they would
  tolerate sleeping meanwhile. The `roof` it carries is how many live there; the spare `roof`
  standing elsewhere is where they go; and the `luxury` the design lifts by is *whose* standard the
  lodging is judged against. No luxury houses settlers, who will take a bunk under canvas. From
  `luxury:1` the resident is a notable, who will not take canvas at all. From `luxury:3` they are a
  notable whose stated taste is housing, and they will not be moved below their own roof. A house
  nobody can be moved out of says so and waits for you to build the lodging.
- **Working buildings** improve only when the stores can go without what the work puts out for as
  long as the labour takes: the `water` the design carries is one dram a day sustained, `UpgradeTicks`
  is how many days, and the two together are the loss the reserve must still cover. Below that
  margin the settlement does not act — it makes an **offer** ("ready to improve, and held — the city
  leans on it") which the founder can force from the Charter, with the whole dip disclosed before
  they consent. A design carrying no `water` is never held.

### Plots: designs that take ground (all optional)

A design may declare how much ground it wants. Instead of one object in one cell, the settlement
stakes out a rectangle, clears what stands on it, frames it, walls it in its own material, cuts a
door on the side facing the heart, and furnishes the inside. A design that declares no `Plot` size
is raised exactly as it always was.

```xml
<building Key="hut" DisplayName="timber hut" Blueprint="r_KingdomHut"
          Cost="6" Ticks="1800" Styles="all" Category="housing"
          Plot="S" Contents="r_KingdomFurnishings_Dwelling" />
```

| Attribute | Default when absent |
|---|---|
| `Plot` | Not a plot. `S` (5x4), `M` (8x6), `L` (12x9), `XL` (20x14); the long spellings `small`, `medium`, `large`, `huge` are accepted. Anything else is an error. |
| `Footprint` | Fills the plot. `WxH` — the ground this **tier** stands on inside the plot, as in `Footprint="3x2"`. The plot is only the envelope; the footprint is the building. Larger than the plot is an error. |
| `Roof` | `Walled`, or `Open` when `Open="yes"`. `Open` (no roof, no walls), `Soft` (canvas: shelter enough to sleep under, raised as the design's own object, and the sky still reaches under it), `Walled` (the settlement's own material, a floor, a door), `Carved` (underground; the rock is the enclosure). `Open` and `Roof` must agree if both are given. |
| `Open` | `No`. `Yes` makes an unroofed plot — a field, a yard, a salt-pan, a reservoir: same rect discipline, no walls, no door. |
| `Sky` | `No`. `Yes` means the design needs weather, and it is refused underground by name rather than sited somewhere useless — tagged **[wants open sky]** right in the commission list, the same tag every other blocked gate wears, before the founder has even picked it. |
| `Contents` | Nothing. A population table the finished interior is furnished from, rolled once per S plot, twice per M, four times per L, six per XL. |

Plot size is gated by stage: a Camp lays S, a Steading and a Village M, a Town L, a City XL.
**Upgrades climb within a plot; sizes compete across plots** — there is no in-place S-to-M
metamorphosis, and small plots never obsolete.

The footprint is what grows. **Improvements climb within a plot**, so every tier of a chain shares one
envelope and each declares its own footprint inside it; the yard is the plot minus the current tier,
recomputed every time it is asked. Staking is the choice: stake wide for room to grow (more clearing,
more material, more yard meanwhile) or tight and take the ceiling. A tier that outgrows its plot is
refused by name at improvement time and waits until the ground is struck and staked again. A yard
trade standing where the larger building needs to go refuses it too, by name — nothing in a yard ever
comes down on its own.

Clearing is reckoned on the whole plot and walls only on the footprint, so a wide stake costs more to
clear, pays more material, and never costs a longer wall. A design that needs weather (`Sky="yes"`) is
refused by a tier that **declares** itself `Walled` or `Carved`; a design that declares no `Roof` has
claimed nothing to contradict and is raised exactly as it always was.

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
ever plot, so roads are never drawn — they are what is left between the plots.

Specs are keyed by building `Key` like every other registry: re-declaring an entry replaces its
whole plot spec, and re-declaring it **without** `Plot` returns that design to the single-cell path.
Declaring `Open`, `Sky`, or `Contents` without a `Plot` size is an error rather than a silent no-op.

Nothing already standing converts. A settlement raised before plots existed is a scatter of
single-cell works and stays exactly that, working exactly as it did; plots begin with the next thing
built.

### Fields that grow: seeds, rows, and the harvest cycle (all optional)

A design that GROWS food is not a `Carries="food:N"` number on an object with no parts. It is a
field that stands rows, and the number comes off the rows. Two things make one:

```xml
<!-- ObjectBlueprints.xml -->
<object Name="MyMod_Orchard" Inherits="Furniture">
  <part Name="r_KingdomPlot" />
  <tag Name="r_KingdomCropRows" Value="16" />
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

**Seeds and crops.** The five shipped crop families come from the settlement's founding style
(`KingdomCropRules.CropBlueprintForStyle`), and each has a seed item and a standing-row blueprint
mapped beside it. A mod adding a style adds a crop, a row and a seed together; the row is an
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
that adds a style adds the dish with it, by adding to the two switches in `KingdomRules` — there is
no XML surface for the dish, because there is no choice in it.

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
| `Goods` | `No`. `Yes` marks a trade whose output is a caravan good rather than anything the settlement's own equilibrium reads. |

Entries live in their own file with root `<kingdomyardworks>` (`KingdomYardWorks.xml` ships the
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
| `CrewNeeds` | Nothing demanded. A `kind:amount` list in `Carries`' own language (`strength:16`) naming what a crew must be capable of to raise and run this design at full pace. A crew that falls short never stalls the work — it runs slower, floored, and says so once. |

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

So a mod that wants the fine house to cost more, look different, and grow into something new ships
this and nothing else:

```xml
<?xml version="1.0" encoding="utf-8"?>
<kingdombuildings>
  <!-- Merges into the base catalogue's finehouse. Blueprint, plot, materials, staff, category,
       gates and every skin it already had are untouched, because this file does not name them. -->
  <building Key="finehouse" Cost="24" UpgradesTo="mymod_manor">
    <skin Key="verdant" ColorString="&amp;G" />       <!-- replaces the base skin of that key -->
    <skin Key="mymod_lacquer" ColorString="&amp;m" /> <!-- appends -->
  </building>

  <!-- The new link the chain above points at, declared in full because nothing declared it. -->
  <building Key="mymod_manor" DisplayName="lacquered manor" Blueprint="MyMod_Manor"
            Cost="40" Ticks="4800" Category="housing" Plot="L" Carries="roof:6,luxury:2" />
</kingdombuildings>
```

A merge that names a key **nothing** declares is not an error: it simply becomes that key's first
declaration, exactly as a re-used key always has. It is reported to the log when it is too thin to
stand on its own — no `DisplayName`, `Blueprint`, `Cost` or `Ticks` — because that shape is nearly
always a mis-spelled key, and a mis-spelled key changes nothing at all.

**A merge never rewrites a city that is already built.** What a settlement already spent (`Cost`,
`Ticks`, `Materials`) and what it already cut into the ground (`Blueprint`, `Plot`, `Footprint`,
`Roof`, `Open`, `Contents`) belong to the standing work from the day it was raised: your update does
not refund it, re-charge it, move it, or resize it. Everything the settlement reads again each pass
does follow the merge — the name, `Carries`, `Staff`, `Defence`, the gates, the chain and the skins
— so a rebalance lands on buildings that already stand, and a skin your mod adds today can be
applied by the founder's re-dress action to a house raised a year ago. That split is the guardrail,
and it is `KingdomMergeRules` that enforces it.

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

Published at **API version 1**. The contracts live in the `ThousandAndFirst.Api` namespace, and a
third-party mod needs **no hard reference beyond that namespace** — not to our systems, not to our
carrier, not to our version of anything else.

Option gate: `r_TAF_OptionExtensions`, default `Yes`. With it off, the data lane is unaffected and
no third-party code runs against the city.

### What is published, and what is not

| Contract | What it extends | State |
|---|---|---|
| `IKingdomAskSource` | what the city asks its founder for | **published** |
| `IKingdomHappeningSource` | what happens in the city, told through our surfaces | **published** |
| resource kinds | the three civic stocks (water, food, materials) | **not published** — the model's stock row is a fixed three-pair struct; a contract for a fourth would be a contract nothing honours |
| job and carrier kinds | the transient/itinerary lane | **not published** — a job's cargo is one of the same three stock kinds and its kind is a closed enum |
| network kinds | pipes, conduits, electricals | **not published** — the graph itself is not built yet |
| work behaviours | a work row's run-state advance | **not published** — the run-state slot is one discriminated 16-byte field, and opening it would freeze that shape as API |

The four unpublished rows are named rather than omitted on purpose: **a contract nothing honours is
worse than none**, because a modder writes against it and gets silence. They open when the
substrate underneath them does.

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
filed under `<your-mod-slug>:<your-kind>`, after the city's own.

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

`Telling` goes to the chronicle (both registers). `Notice` is the line a settler says out loud, and
it is spoken **only if the settlement pass has a line to spare** — the city's own news outranks it.

Date a notice inside the window you were given: anything after the pass's own tick, or before
`SinceTick` on a pass that has one, is dropped rather than filed with a wrong date. The city does
not report the future and does not re-report what it has already told. `City.ProcessedThroughTick`
is always inside the window and is the safe default.

### The invariants you inherit, enforced rather than trusted

1. **Kernel draws only.** `IKingdomDraws` is the counter-based kernel wearing a published face,
   keyed on `taf:ext:<your-mod>:<your-lane>`. Same city, same lane, same ordinal, same answer —
   across reloads. `System.Random` in an extension is a contract violation and makes the city
   unreplayable.
2. **Frozen in, frozen out.** `KingdomCityReading` is a projection with no setters and no route to
   the ground, the clock, or another extension's rows. Your method returns an array; we copy it.
3. **Budget and error isolation.** Every call crosses `KingdomExecutor.Submit`. A source that
   throws or runs past its lane's budget **stalls its own job and nothing else** — no city state is
   published, the turn is unaffected, the failure is logged **by your mod's name**, and the asks
   board says out loud that something stalled. **The budget is a verdict, not a timeout**: the seam
   is synchronous, so it can refuse to publish a result that overran but cannot interrupt one. An
   infinite loop in your `Ask` hangs the game, exactly as one in ours would. Return.
4. **Telling through our surfaces.** Ledger, chronicle and `KingdomWord`, under the shared telling
   budget. An extension cannot flood the register any more than we can.
5. **Clamped, and holding no rows.** At most 4 asks and 2 notices per source per call, and the
   whole board is trimmed to 8 lines after sorting, so ten installed mods cannot turn it into a
   spreadsheet. Every string is stripped of colour markup and control characters and cut to 200
   characters on a word boundary. A `ZoneId` naming ground the city does not hold is read as none,
   and a weight outside the three rungs is read as `Passing` — the mildest — because a malformed
   weight is not a claim of urgency. Clamping is never a refusal: the ask behind an over-long line
   is still real. The
   architecture's fifth clause ("an extension's rows count against the model's memory ceiling, and
   the receipt reports them by mod name") has nothing to enforce **at version 1**, because no
   row-owning contract is published: an extension reads the model and returns prose, and owns no
   part of the book. That clause arrives with the first contract that lets one keep state.

### Versioning, and being refused out loud

`KingdomApiRules.Version` is checked at registration against the window
`[KingdomApiRules.MinSupportedVersion, KingdomApiRules.Version]`. Outside it the extension is
**refused by mod name**, in the log and in the message queue, naming the version it wanted and the
version we publish — never silently skipped, because a player attributes missing behaviour to *us*.
The same refusal fires for a marked class that implements no contract, for one whose constructor or
`ApiVersion` getter throws, and for one whose owning mod cannot be named. The window is what makes
STANDARDS §9's promise keepable: a version bump does not refuse every extension in the world on the
same day.

The founder can see the whole registry: **Charter → The book of the city → Who else writes in this
book** lists what is admitted and what was refused, with the reason.

Return `KingdomApiRules.Version`, not a literal. Recompiling against a newer copy of the mod is what
re-admits your extension.

## Conventions

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

More registries (settler origins, raider tables, districts) are migrating to the same
XML pattern; anything still hardcoded is a defect by our own standards — file an issue.
