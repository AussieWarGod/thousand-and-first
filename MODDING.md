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
- `Category` — grouping for UI and future district logic: `storage`, `housing`, `civic`,
  `faith`, `craft`, `defense`, `knowledge` (free-form; default `civic`).
- `MinStage` — earliest growth stage the design appears at: `Camp` (default), `Steading`,
  `Village`, `Town`, `City`.

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
