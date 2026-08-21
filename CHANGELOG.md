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

### Added — leaving, and being taken back
- A realm's **regard for its founder** is the vanilla reputation cell for its own faction, so there
  is no second economy to learn. It falls from deeds and never from time, and speaks once per rung
  with real hysteresis: crossing a threshold back and forth says nothing.
- At the bottom rung the realm **puts you out**. Exile is secession, realm-scoped — one faction,
  never one per city. The realm, both its cities and its whole standings ledger are kept intact,
  the Charter is taken, and nothing physical is touched: no allegiance key, no claim, no dedicated
  vessel. The city goes on without you, which is the entire point.
- The unchanged rite then founds a **new realm with a new faction**, and the old one keeps its own
  opinion of you.
- While you have not founded again, walking onto the old realm's ground puts the question to you in
  person. Refuse and it stays silent until you have actually changed their mind. Found again and the
  door shuts — and says so, rather than going quiet.
- Both registers record the day, and they **disagree**: your book says the realm put you out for a
  named deed; the roads say the tyrant ran.

### Added — founding paths, and ground under ground
- **Founding by reclaiming a ruin.** Pour on ruined ground and the rite restores rather than
  founds; structures already standing are credited to the settlement.
- **Asking a living village.** Standing in a real village, the rite *asks* instead of taking, gated
  on the village's own opinion of you. It never re-flags a villager, never overwrites a zone's
  faction, and never annexes. This deliberately stops short of a full charter rather than shipping
  something that quietly steals a village.
- **Vertical claims.** A settlement can claim the ground directly below or above what it holds —
  cellars and towers — without loosening horizontal adjacency.
- **The water manifest.** Two cities can send water to each other: drams leave the origin's stores
  when loaded and arrive at the destination's next attended pass, never on a background clock. One
  in flight at a time; a lapsed window is written off once, in the chronicle.
- **A tended plot.** Commissioned like any building, it cycles on the settlement's own clock and
  deposits its harvest into a dedicated larder. It draws water only after upkeep and arrivals, so it
  can never be the reason the thirst ladder fires, and with no water it simply waits.

### Added — city plans, three ways
- A settlement is now **laid out**, not scattered. A commissioned building is sited by what it is
  for, read off what the settlement already has standing: casks gather by the water, bunks cluster
  and keep off the wall line, craft and civic thicken the heart, plots ring the last roof, and each
  new wall closes a gap in the line before extending an end. The founder still wins ties — the plan
  picks the quarter, the founder picks the spot — and on empty ground the plan has no opinion at
  all, so your first building is the seed everything later is read against.
- **Stake a plan** on any claimed cell and the settlement builds it when it can afford to. Nothing
  is spent when you stake it, plans are realised oldest-first, and one that can never be afforded
  waits forever without nagging or expiring. Cancelling is free.
- **Adopt what you built yourself.** Designate a structure as serving a civic role and the
  settlement accepts it if the space qualifies — a bed for housing, containers for storage, four
  walls and a door for a work. It checks the space, never who made it, so this works for a
  Hearthpyre house, a cleared ruin, or walls you laid by hand, and Hearthpyre stays optional.
  Adoption is a mark, never a transfer, always reversible, and a refusal names what is missing and
  touches nothing.

### Added — what a city believes
- Settlers may carry a **creed**: a real Qud faction, drawn from factions the realm has actually
  dealt with. Most believe nothing in particular. Once enough of a city shares one, that city holds
  with them, and the Charter says so.
- Two cities of one realm whose creeds clash fall into **dissent** — read from the engine's own
  faction feelings rather than any table of ours, so it is right for modded factions for free, and
  the factions that dislike strangers by default are exactly the zealous ones. Dissent accrues only
  on days the founder was present, with four warnings well before anything is lost.
- Three levers, none of them waiting: a rite of shared water, a shared meal, or **declaring the
  realm's creed** — fast and decisive, and it now names its price before you pay it, because it
  moves a faction's regard for the realm across the whole world.
- If it runs to the end, the unhappier city **leaves**, keeping its ground, its people, its
  buildings, its stores and its book. Nothing burns and nobody is driven out. Both chronicle
  registers record the day and disagree about it. You can ask them back once the cause is gone.

### Added — plots, materials, and the catalogue rebuilt
- **Buildings take ground.** Designs declare a plot — S, M, L, XL, stage-gated — and rise over it
  in watchable stages, walls in the settlement's own material, the door cut toward the heart.
  Underground plots are carved: double the clearing, paid back in stone, and the rock is the wall.
  Every plot reserves a lane; a settlement can never tile itself solid.
- **Clearance is extraction.** Clearing ground yields what stood on it — timber, stone, marble,
  scrap — as real items in a stockpile you dedicate. Building costs are water and materials.
  Buildings can be condemned: half the material returns, the plot frees, nothing refunds.
- **The catalogue is rewritten around plots**: fifty designs across ten families, every entry
  denominated in how many more people the settlement honestly carries. Water, food and roofs bind;
  craft, faith, learning, order and luxury lift, capped. The commissionable solar condenser is
  gone — water that arrives with no crew is recovered and certified, never ordered. Houses come
  furnished.
- **Four gates on what may be commissioned** — the district the ground carries (hard for placement,
  soft for effect), territory held, designs the keepers have learned, and a derived craft level
  raised only by teaching data disks and certifying salvage. Blocked designs stay visible, tagged
  with the one thing in the way.
- **Improvements**: a design may name what it grows into; the settlement raises the successor
  itself, out of surplus, carrying every civic mark and the founder's given name across. Any work
  or a whole zone can be held as-is forever.
- **Ceremony**: the surveyor's plan, the raising ceremony, notable tastes, leader traits (one
  virtue, one flaw, no reroll), and the caravan pattern-book.

### Added — the settlement acts on its own judgment
- **The posted price.** Stake a notice at the heart offering drams for a named job — clearing
  ground, hauling a marked pile, manning an idle work, scouting the frontier. Nothing is escrowed;
  the price is paid the day the work is done, a price the stores cannot cover stands with the debt
  written down, and who takes a notice is theirs to decide — attempts and refusals chronicled by
  name, refusals citing the refuser's own flaw.
- **Worn ground.** Roads are never drawn: the routes people actually walk wear from grass to
  trodden earth to true path, and the founder may pave a worn path in the settlement's own wall
  material, asked first and paid in stone. A season away lays exactly what three days lay.
- **Yard trades.** A small or middling house can take up one trade in its yard — vine lattice,
  hide rack, dye vat, vellum press — and the household takes it up, visibly, in the roll and in
  the house's own description.
- **Guests at the gate.** Notable travellers arrive between visits, each carrying one hook. Lodged,
  they settle with a trade; ignored, they leave a letter and the hook becomes a standing rumor —
  never lost, only relocated.
- **The carry-sign.** A cheap marker planted on a pile you own, anywhere: porters haul it home over
  distance-scaled days. The sign is the designation, one haul rides at a time, and a road cut by a
  raid costs the load in the chronicle, never in silence.

### Added — catalogues that layer, buildings with their own ground, and the trigger law
- **Catalogue files layer.** A later `<building>` with a known key merges: named attributes win,
  omitted ones survive, skins append, chains extend across files — so mods can re-cost, re-skin and
  extend each other's designs with no code. What a standing work already spent and cut into the
  ground never follows a mod update; what the settlement re-reads each pass does.
- **A tier owns its footprint and its roof.** Buildings front the heart with the yard behind;
  footprint never outgrows the plot, refused at load and at improvement by name; sky-needing
  designs refuse walled tiers; underground everything is carved. Growing in place never takes a
  yard trade down without asking.
- **The plot is a socket.** Change what a plot is within its own type×size set as one ceremony
  with one disclosed figure; re-dress a standing building with any skin, including one added by a
  mod after it was raised.
- **The upgrade trigger law.** Housing improves itself only when its residents can lodge
  elsewhere *to their own standard* — an ordinary settler takes a bunk, a notable will not take a
  tent. A working building the city leans on is never taken offline automatically: below margin
  the upgrade is a held offer, forceable with the dip disclosed first. No trigger anywhere reads
  elapsed time as a cause — time is labour, never maturation.

### Added — what a resident needs, prefers, and refuses
- **One open tag vocabulary** joins buildings to the people in them: buildings declare `Provides`,
  residents carry Needs (hard), Prefers (soft, never a penalty unmet), Refuses (no cohabitation).
  Derived from vanilla truth before anything is authored — a robot needs charge and eats nothing
  because the engine's own Robot removes the stomach; the water-bound, the fungal, and the
  photosynthetic likewise. A creature from any mod is a correct resident before its author writes
  one tag; roofs give their tags free.
- **Lodging is assignment, not a tally.** Every settler has an address, stable across visits and
  upgrades. Two residents who refuse each other never share a building; only real hostility breaks
  households, not the zealots' standing grudge toward strangers.
- **Housing binds** (author ruling): nobody joins without a home *they* would accept, and a settler
  whose acceptable housing is lost is named once, waits two attended passes for the founder to
  act, then leaves through the ordinary emigration — chronicled by name and cause. Absence never
  runs the clock.
- Tastes and the upgrade trigger's displacement tolerance now speak this same vocabulary — three
  private systems became one, moddable end to end.

### Added — closeness, and the fault-line ceiling
- **How citizens feel about each other decides who shares a roof, scaled by the quarters.**
  Packed (tent, bunk row) shares only without quarrel; Close (hut) refuses the ambient grudge;
  Roomed (stone house) tolerates it — walls between beds answer prejudice; and a true fault line
  (the −100 creeds) refuses **any** shared roof, marble included. Closeness derives from
  beds-per-footprint density, with an authored override where a design raises several dwellings
  at once.
- Consequences, intended: a diverse city must build in stone to exist, and a two-creed city
  physically partitions into quarters through the layout grammar — no code knows the word
  "quarter." Architecture manages difference; only the covenant heals it.

### Added — how belief moves
- **Osmosis**: a household's majority slowly pulls the minority under its roof — counted in
  attended passes and scaled by the closeness of the quarters, a season not a week, deterministic
  on reload. Pulled two ways, a citizen converts to neither.
- **Culture**: every shared meal nudges the table toward its own majority, small and capped — a
  settlement cannot eat its way to a conversion.
- **Consecration**: a founder may consecrate a shrine to a creed; staffed, it draws the neutral of
  its zone — never the opposed, who resent it instead, and may take the road: named once, six
  attended passes of grace, then the ordinary emigration. Remove the shrine and the pressure lifts.
- **Education softens**: a staffed scriptorium reads the ambient grudge one band gentler. It
  converts nobody.
- **The water rite turned inward**: share water with one named settler of another creed. They
  decide, from what actually stands between you; refusal costs the water anyway and names what
  would have to change; a fourth asking shuts the question. One rite, one soul. The covenant is
  drunk, never administered.
- Contested conversions are told twice, and the registers disagree.

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

- **Claiming ground never overwrites another faction's zone again.** `ClaimZone` and the second
  founding used to write the kingdom's faction over whatever a zone already answered to — the exact
  hazard the ecosystem-compatibility audit flagged. Both now refuse rather than overwrite, and the
  refusal cannot be forced away for foreign ground.
- Founding under a name a faction already holds is refused before anything is spent. A runtime
  faction can never be removed or renamed, and `Factions.AddNewFaction` is a dictionary add that
  throws on a duplicate — so after an expulsion the old realm's name is taken forever.
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
