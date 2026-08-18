# Extending The Thousand and First

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
  city style is `common`; future founding paths (devotion cities and friends) select others.

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
