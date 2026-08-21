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

- `Key` — unique id. Re-using an existing key **overrides** that entry (load order applies),
  so styles can retheme the base catalog.
- `Blueprint` — any object blueprint, yours or vanilla. Buildings are real objects: give
  them vanilla parts (`LiquidVolume` for storage, `Bed`, `Shrine`, `Campfire`...) and the
  kingdom's systems and the ambient AI use them with zero extra wiring. Storage capacity,
  for example, is literally `LiquidVolume MaxVolume`.
- `Cost` — drams of water drawn from the settlement's physical stores.
- `Ticks` — build time (1200 = one day).
- `Styles` — comma list of city styles this design belongs to, or `all`. The default
  city style is `common`; declared styles so far: `common`, `verdant`, `fungal`, `gyre`,
  `eater` (founding paths will select them — devotion cities, the fungal quarter, Eater
  restoration).
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
  day sustained, which is one settler's thirst at camp rates. A support name this mod does not know
  is accepted and lifts; a new *binding* good would make every catalogue that predates it
  unbuildable, so there will never be a sixth. Omitting the attribute adds nothing to the level,
  which is correct for a wall.

The whole merged catalogue is validated once, on load: a dangling `UpgradesTo`, a chain that rings,
an improvement onto a larger plot, a defence rating on a design that also claims a plot, and a
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
- The vocabulary is six keys: `mud`, `brush`, `timber`, `stone`, `marble`, `scrap`. `canvas` is
  accepted as an alias for `brush` and `scrap metal` for `scrap`. Anything else is a logged
  error and the whole attribute is rejected — never half-applied.
- A malformed value disables itself with a logged reason and leaves the design costing water
  alone. It never crashes the registry and never half-registers.

Materials are **never minted**. They come off ground somebody cleared, off a building somebody
struck (half of what it was made of), or out of a caravan. A settlement holds them as real items
in a container the founder dedicated as a **stockpile** — a mark, not a transfer, exactly like a
larder: what is inside stays where it is and stays the player's, and the settlement only counts it.

To make your own item count as one of the six, tag its blueprint:

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

### Gating a design (all optional)

Four more attributes decide whether a design may be raised, on top of `Styles` and `MinStage`.
**Every one is optional and an absent attribute gates nothing**, so an entry written before these
existed — ours or yours — behaves exactly as it always did. A malformed one is logged and ignored
rather than deleting the design: the worst case is a design that could have been harder to reach,
never one that becomes unreachable with no way to find out why.

| Attribute | What it wants |
|---|---|
| `Districts` | Comma list of district keys whose ground will take this design: `agrarian`, `market`, `craft`, `shrine`, `garrison`, `academy`, plus `none` for ground the founder has never named. `all` (or omitting it) accepts everywhere. A key we do not recognise is treated as somebody else's district, never as open ground. |
| `MinZones` | Claimed zones the realm must hold. |
| `Knowledge` | Comma list of things the settlement must know, **all** of them. A requirement written `kind:name` must match that kind exactly; one written as a bare name is satisfied by any kind. Kinds: `disk` (a design taught to the keepers from a data disk the founder carried home — the disk is read and handed back, never spent), `machine` (a machine hauled home and certified fit for the grid), `origin` (a trade the settlement holds because somebody from that country lives there, so it comes and goes with them). Invent your own kind freely; an unknown kind gates perfectly well and is worth no craft. |
| `MinTech` | Craft the settlement must have reached: `hands` (the start, gates nothing), `salvage`, `workshop`, `foundry`, `arclight`. |

A fifth kind is reserved by convention: `pattern` (a foreign design a chartered caravan
occasionally offers a choice of, never taught by any disk, machine, or origin — see
`Experience/KingdomCeremony.cs`). Write `Knowledge="pattern:some-name"` on an ordinary `<building>`
entry to enter it into that pool; the base catalogue never depends on the draw, so an entry gated
this way is purely additive.

Craft is **derived, never authored and never set**: a taught design is worth 1 and a certified
machine is worth 2, an origin is worth 0, and the level is read off the total. There is no research
tree and there will not be one.

Gating is **hard for where a structure may stand and soft for how well it works**. `Districts`
refuses placement; nothing anywhere gates the district *bonuses*, which stay realm-wide and
unconditional, so a design raised off its natural ground simply misses a bonus. Housing, storage,
and civic designs are additionally always accepted on undistricted ground, so a camp can never hit
a wall before the founder has learned what a district is.

### Designs that grow into other designs (all optional)

A design may name what it becomes. When the settlement has earned it, it raises the successor
itself through the same scaffold a commission uses, out of what the stores can spare, and carries
everything the old work held and everything the founder had marked on it across.

```xml
<building Key="cistern" DisplayName="cistern court (holds 256 drams)" Blueprint="r_KingdomGreatCistern"
          Cost="20" Ticks="3600" Styles="all" Category="storage" UpgradesTo="cisternvault" />
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
| `Open` | `No`. `Yes` makes an unroofed plot — a field, a yard, a salt-pan, a reservoir: same rect discipline, no walls, no door. |
| `Sky` | `No`. `Yes` means the design needs weather, and it is refused underground by name rather than sited somewhere useless. |
| `Contents` | Nothing. A population table the finished interior is furnished from, rolled once per S plot, twice per M, four times per L, six per XL. |

Plot size is gated by stage: a Camp lays S, a Steading and a Village M, a Town L, a City XL.
**Upgrades climb within a plot; sizes compete across plots** — there is no in-place S-to-M
metamorphosis, and small plots never obsolete.

Clearing the ground is how a settlement without a mine gets material: brush and trees yield timber,
shale and granite yield stone, a marble seam yields marble, somebody else's fallen walls yield
scrap. Effort scales with hardness, and it is part of how long the plot takes to raise. Underground
(any zone below the surface stratum) a plot is **carved** instead: the clearing costs twice as much,
it pays in stone, the plot's edge is left standing because that rock **is** the enclosure, and no
wall is ever raised.

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
ships. Re-declaring an entry replaces its whole skin list along with the rest of it.

Any building the settlement raised can also be given a name by the founder, from the Charter, and
that name is what the chronicle, the ledger, and the settlement's own messages call it from then
on — including after it grows into something else.

## The protection contract

Kingdom systems never touch what players place. Containers join the city stores only when
dedicated (`KingdomStores=1` — commissioned storage auto-dedicates; everything else is
opt-in via the Charter). If your mod's structures should participate in the water economy,
either set that property on your placed objects or leave it to the player's dedicate
action. Never rely on the kingdom consuming undedicated liquids — it won't.

The same contract covers food. A container joins the settlement's pantry only when it carries
`KingdomLarder=1`, which the Charter's dedicate action sets and a commissioned civic larder sets
for itself. Food inside a dedicated larder is counted, and may be spent by a shared meal the
founder calls; food anywhere else — including the player's own pack and any container they simply
left lying about — is never read and never spent. Dedication is a mark, not a transfer: nothing is
moved when a container joins the pantry.

## Power

The settlement's power is civic labour, not a wiring puzzle, and it extends by parts rather than
by a registry. Two parts are the whole contract:

- `<part Name="r_KingdomPowerWork" Source="Hands|Water|Wind" />` makes an object one of the
  settlement's power works. `Hands` is worth whatever fraction of its `Staff` the settlement
  crewed it with; `Water` also needs open water in or beside its cell (400 drams to turn at all,
  4000 for full output) and never counts a dedicated cistern; `Wind` reads the zone's own wind.
  An unknown `Source` disables the work rather than defaulting it.
- `<part Name="r_KingdomPowerStore" />` on any object that also carries a vanilla `Capacitor`
  makes it a store the settlement pours into and draws back from. Its `MaxCharge` is the capacity;
  set `ChargeRate="0"` and `MinimumChargeToExplode="0"` unless you intend otherwise.

What the works make is ordinary vanilla charge, delivered through vanilla's `ChargeAvailableEvent`.
Anything the settlement built (`KingdomBuilt=1`) or that the founder dedicated (`KingdomGrid=1`)
and that accepts charge is filled from it — a charging post, a kiln, your own machine. Nothing the
player merely left standing about is ever charged or read. The settlement does **not** use
vanilla's `IPowerTransmission` grid, so you do not need to run conduit to anything.

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
