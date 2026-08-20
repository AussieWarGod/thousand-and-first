# Changelog

All notable changes to The Thousand and First. Versions are semantic: patch for fixes,
minor for additive API and content, major for breaking changes. Supported API is defined in
[docs/API.md](docs/API.md).

## [Unreleased] — 0.2.0 in progress

Pre-release: the mod has not yet run in a live game. Nothing here is stable until the first
playtest passes.

### Added
- Founding by water rite: the founder's basin item (obtainable from tier-1 merchants), the
  Charter activated ability, and the kingdom faction with its own standings ledger.
- Growth engine: witnessed arrivals with settler provenance and conversations, water
  fetching from open sources into dedicated stores, growth stages Camp through City.
- The thirst ladder: upkeep, warnings, bounded emigration, the withered state, and full
  recovery when water returns.
- Territory: adjacency-gated claims, districts with Qudish names (vinelands, bazaar,
  forgeworks, sacred ground, watch, scriptorium).
- Commission construction: scaffolds that complete into real buildings over time.
- Raids: provocation-keyed, telegraphed, tribute-able, with plunder.
- Trade charters: standing-gated deals, caravan arrivals delivering water, real merchants.
- The chronicle in two registers, dated in Qud's calendar, feeding journal accomplishments.
- Extension registries — `KingdomBuildings.xml` and `KingdomDeals.xml` — loaded through the
  game's mergeable XML streams so any mod extends them without code.
- Diagnostics: `[TAF]` log lines and the `kingdom:dump` state readout.

### Added — a faction of more than one city
- A realm can hold **two cities**. Pour the rite on unclaimed ground that does not border what you
  already hold and the realm founds a second city: same faction, same standings, one shared
  chronicle, its own name and its own vocation. A third is refused.
- The city you are standing in is *seated* — its state lives in the fields every system already
  reads — and the other waits in `KingdomSystem.Away` until you walk into its ground, when the two
  exchange. Charter titles, the roll, petitions, policy and the homecoming report all name the city
  you are in; the chronicle and standings stay the realm's, because the realm is the faction and
  the cities are only where its history happened.
- A dormant city needs no clock and gets none. It carries its own tick stamps, so it catches up the
  moment it is seated — the same lazy tick-stamp idiom vanilla uses for zone repair — capped at
  three days of upkeep and three arrivals however long you were away.
- Ground the realm's other city holds cannot be claimed by this one, even forced.

### Added — the larder and the shared meal
- Containers can be dedicated as **larders**. A civic larder can also be commissioned. Food in a
  dedicated larder is counted for the settlement; dedication is a mark, not a transfer, so nothing
  is moved and an undedicated container is never read.
- **Share a meal from the larder**, from the Charter, when the larder can feed one. Food is spent
  from dedicated larders only, a settler from the roll speaks, and it becomes the settlement's
  deed, so word travels the way it does for any deed.
- An empty larder costs nothing: no hunger, no unhappiness, no decay. A player who never dedicates
  food plays exactly as they did before. Every food effect is a bonus for engaging, never a penalty
  for abstaining.

### Changed
- Districts are no longer flavour. Each of the six now changes something a player can measure:
  garrison adds defence across every claimed zone, agrarian bills upkeep at 90%, market adds a
  shop tier (keeping its faster arrivals), craft builds at 80% time, shrine shortens the petition
  interval, academy halves how far the outsider register drifts from the true record. Percent
  effects are best-wins and never stack; only garrison defence is additive.
- City style is chosen from the ground the rite was poured on. `StyleForSite` maps real terrain
  blueprints and regions to verdant, fungal, gyre, and eater, with a total fallback to common,
  and the founder is told in prose what kind of place they founded. Four of the five declared
  styles were previously unreachable because nothing ever set `Style`.
- Raids can name more than one enemy. Snapjaws, Baboons, Goatfolk, Cannibals, and Issachari each
  have a raider table, exposed as `ProvokableFactions`.
- **A raid is never resolved while you are away.** Raiders who arrive to find nobody home wait:
  nothing is taken, nobody is lost, and the threat is still live with a fresh window to pay,
  parley, or fight when you return. What accrues in absence is the news that they came.
- Fortification decides how much of a raiding band gets past the perimeter at all, and how much
  the ones who do carry off. A strong enough wall turns the whole band back; no wall ever turns
  back more than 60% of a band that is not fully repelled.
- The homecoming report is nonmodal. Walking home shows one line saying the settlement has news;
  the report itself is read from the Charter when the founder asks for it.
- The outsider register draws through the frozen `Simulation/Kernel` `CounterRandom` instead of an
  ordinary random roll, so the same event drifts the same way on reload. This is the kernel's
  first production call site.
- Dedication covers larders as well as vessels. A dedicated chest is counted, never emptied:
  dedication is a mark, not a transfer.
- `KingdomReports.Status` no longer under-reports upkeep when the stores policy is thrift, and
  now shows the settlement's defence and larder.

### Fixed
- `r_KingdomChargingPost` could detonate for up to 4,000 force when destroyed. The settlement's
  own crew fills its vanilla `Capacitor` past the engine's 1,000-charge explosion threshold, so a
  raid that destroyed a crewed post would have killed settlers and destroyed player-placed
  objects the mod is not allowed to touch.
- `r_KingdomChargingPost` carried a `Container` part with no `Inventory`. The engine dereferences
  the inventory after offering to store something, and `Furniture` carries none anywhere in its
  chain — so using the post the way its own description invites would have thrown. Vanilla's own
  Universal Charging Station pairs the two parts; it is also what `UniversalCharger` charges, so
  without it the cradle could never have filled a cell.
- Removed dead code that a player could never reach: `KingdomTrade.DeliverIncome`,
  `KingdomGrowth.FetchWater`, and `KingdomRules.RaidCasualtyChance`.

### Tooling
- `Tools/stage.sh` defines one canonical runtime set, and `Tools/gate.sh` compiles exactly that
  set — so the compile gate now asks the same question the game does. `DevTests/build.ps1`
  compiled the repo, while the game compiles the deployed folder, and the two disagreed.
  Deploying is dry-run by default, requires `--apply`, backs up first, verifies byte-equality,
  and refuses any folder that is not this mod.

### Notes for extenders
- The supported API is now documented in `docs/API.md`; anything absent from it is internal.
- All engine entry points are exception-guarded: our failures log and degrade, never
  propagate into the host game.

### Fixed
- Water accounting now measures actual liquid deltas, including exact vessel depletion and
  capacity-clamped fills.
- Kingdom state now uses versioned, named-field serialization. Exact petition-era development
  saves migrate once; older pre-release layouts require a fresh kingdom state.
