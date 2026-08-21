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
  moment it is seated — the same lazy tick-stamp idiom vanilla uses for zone repair — and it
  catches up in full: every day of upkeep, and at most three arrivals in the one pass, which is
  a rule about the gate rather than about the calendar.
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
  when loaded and arrive at the destination's next attended pass, because somebody has to be
  there to take a physical load; the window it must land inside is real elapsed time. One
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
  material, asked first and paid in stone. A season of settlers walking lays a season's worth;
  ground nobody walks on stays grass.
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
  whose acceptable housing is lost stops at a brink and is warned about once, wherever the founder
  is; six world-days for them to act, then the ordinary emigration — chronicled by name and cause.
  Nothing accrues past the brink, so an absence of any length arrives at the same place.
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
  cohabitation-days of real shared living and scaled by the closeness of the quarters, a season
  not a week, deterministic
  on reload. Pulled two ways, a citizen converts to neither.
- **Culture**: every shared meal nudges the table toward its own majority, small and capped — a
  settlement cannot eat its way to a conversion.
- **Consecration**: a founder may consecrate a shrine to a creed; staffed, it draws the neutral of
  its zone — never the opposed, who resent it instead, and may take the road: warned once,
  eighteen world-days at the brink, then the ordinary emigration. Remove the shrine and it lifts.
- **Education softens**: a staffed scriptorium reads the ambient grudge one band gentler. It
  converts nobody.
- **The water rite turned inward**: share water with one named settler of another creed. They
  decide, from what actually stands between you; refusal costs the water anyway and names what
  would have to change; a fourth asking shuts the question. One rite, one soul. The covenant is
  drunk, never administered.
- Contested conversions are told twice, and the registers disagree.

### Added — reach, the material chain, capable crews, and honest wear
- **What a building gives carries as far as its ground earns**: a small plot shades where it
  stands, a middling one its quarter, a large one its zone, a great work the city — quarters are
  measured from what is built, never drawn, and the status report names the one you stand in.
  Water, food and roofs stay citywide pools.
- **A great work is an office.** Its citywide effect is live only while a named notable heads it —
  and you appoint nobody. Unheaded, it keeps its zone and says so once.
- **The chain gains its middle link**: sawyer's yard, mason's yard and smelter turn raw into
  shaped, worked by real crews at speeds read off who they are. Large works need the yard
  standing and staffed; grand ones need it headed. High-craft designs are priced partly in
  vanilla's own tinkering bits; great works may want a rare find.
- **Crews have capability**, derived from settler stats. A shortfall builds slower and names
  itself — never a silent stall.
- **Wear is event-driven, never calendar**: raid damage, hard running, temperamental salvage —
  bounded, named, running reduced, never destroyed. Mending is a visible, holdable job costed
  from the chain. Idleness wears nothing — a work standing unused is as sound in a year as it is
  today — and a work that ran hard through an absence wore for it, counted in activity-days.

### Added — the world keeps time
- **The three-day forgiveness cap is retired.** Upkeep, policy upkeep and water fetching all run
  the **full elapsed time** now, however long you were away, drawn from the settlement's own
  stores. What bounds an absence is no longer a clock that stops counting; it is the level the
  settlement's works honestly carry, and the floor under that is a camp.
- The cap's two jobs are separated. `ReserveDays` is what it always really was — a cushion three
  days deep, kept in hand before the settlement spends water on a planting, an upgrade or an
  outbound manifest — and it is a quantity rather than a clock. Nothing anywhere caps elapsed
  time any more.
- **A scaffold nobody works on does not rise.** Build ticks are spent at the pace the free hands
  actually justify; a shortfall is named once and unsays itself the moment hands are free; and a
  finished work is stamped to the day the work really finished, so the raising ceremony still
  tells attended from absent. A settlement with nobody in it raises nothing.
- Yards, clearing gangs and mending all read the same two gates before spending a day's budget —
  is there anyone there, and is there anything to work — and an idle yard says which, once.
- Every periodic system now shares one substrate: `ElapsedDays` and `AdvanceCheckpoint` over the
  frozen `Simulation/Kernel` tick maths, plus `ActivityDays` / `LabouredTicks` for the labour
  term. A checkpoint is planted before its first count, because an unplanted stamp reads as the
  age of the world.
- Serialization version 3. Pre-rework layouts are refused by name rather than migrated: no save
  has ever been written by a shipped build, and a silent migration here would have billed a
  season of upkeep in one pass on the first load.

### Added — subsidence, and a level something finally reads
- **The catalogue's `Carries` have a consumer.** Every finished work's contribution is summed
  into water, food, roof and lift — a crewed work scaled by how well it is actually running — and
  handed to the equilibrium arithmetic, which converts the water half out of drams into settlers
  at the settlement's own stage rate. The status report says what the city carries and which of
  the three is holding it down.
- **The stage ratchet moves both ways.** It climbs on the reading and falls only on a clear
  shortfall — a 20% benefit of the doubt on both readings, one rung per reckoning, Camp an
  absolute floor.
- **A settlement standing above what it carries settles back toward it, on world time.** One step
  every four days, shedding settlers by the rung, converging on the level and stopping there — a
  city whose works carry a town becomes that town, not a ruin. Rung changes land in the chronicle
  as dated entries, dated to when they happened rather than to when you read them. A hundred days
  and a thousand days write different chronicles and end at the same honest level.
- **Each lost rung reaches the whole settlement, not a fixed quota** (Addendum 10(c)). Every
  standing work is asked once, independently, at that rung's own scale — a City rung half the
  settlement, a Steading rung a fifth — so a deep collapse leaves most of the plots battered,
  half-ruined or ruined in name and in look, not a handful scuffed and the rest pristine; mending
  walks the name back down as it heals. Still **damage, bounded by the wear ceiling, never
  deletion**, drawn through the kernel's counter-random so a reload never re-rolls a collapse the
  chronicle already described. Nothing player-placed is touched.
- **Water works make water.** What a cistern or reservoir carries now arrives in the casks, day
  by day, on the same checkpoint discipline as fetching — including over an absence.
- Arrivals stop at the edge of the measured band rather than walking into a settlement that
  cannot hold them. A settlement no pass has ever measured may not refuse anyone on knowledge it
  does not have.
- New option: *a settlement standing above what its works carry settles back toward them*.

### Added — the brink
- **One shape for every irreversible consequence.** A settler with nowhere to live, a settler one
  window short of another creed, and a realm quarrelled to the breaking point all now stop at a
  brink: it records who, what caused it, and the tick it was reached, and then **stops accruing**.
  A thousand-day absence and a ten-day absence arrive at the same place, because there is nowhere
  past a brink to arrive at.
- **The word is pushed, once, dated, and it coaches.** When something reaches its brink the warning
  finds the founder wherever they are — framed as word out of the named city when they are standing
  somewhere else, and as the ordinary announcement when they are standing there, never both. It
  says how long it has really stood, and it names the ARREST rather than only the doom.
- **The window then runs in world-days from that delivery** — six for a roof, eighteen for a creed,
  nine for a city, each its old attended-pass rope times the three-day cadence a present founder
  was always assumed to keep. Spend those days elsewhere and the thing happens: the settler goes,
  the creed changes hands, the city walks, and the report dates it to the day the window ran out
  rather than to the homecoming that found it.
- **Nothing irreversible ever fires unwarned.** A brink nobody has been told about has no deadline
  at all, however old it is; the pass that discovers one can only warn about it, and the whole
  window starts there. Presence stopped being a shield; ignorance became one.
- **Arrest it by acting**, at any point up to the day it fires. Re-house the settler, break up the
  household, take the shrine off their quarter, mend the quarrel — the brink lifts, the warning is
  unsaid, and the accrual starts again from nothing. Waiting was never a strategy and now is not a
  mechanic either.
- Shared living, shrine pull and the water rite are counted in **cohabitation-days** of real
  shared life rather than in visits, through one exchange rate (three days to the pass) applied
  to every threshold, so an attentive founder's road is exactly as long as it was.
- Dissent runs on world time, uncapped — safely, because secession now waits at a named window,
  behind a warning ladder that has already been shouting for eight days before it opens.
- The old re-housing grace counter is gone from both seats. A settler's brink rides **the
  settler**, so swapping between cities can never carry one to the wrong place; the per-city
  resented-creed entry now carries the day the founder was warned rather than a count of visits.
- One push channel, `KingdomWord`, carries every brink's warning, unsaying and dated aftermath.
  Nothing builds a second one.

### Added — the food ladder reaches its big plots
- **`grange`** (large plot, from Town) and **`the home farm`** (grand plot, from City) fill the
  two rungs the food lane never had. They climb the same figures the housing lane climbs —
  twenty-six and forty — because a dinner and a bed are both counted in people, where a dram is
  divided by the settlement's own thirst.
- Food is deliberately the one binding good that **never automates**. Water and roof reach designs
  that want no crew at all; food reaches better rates for hands and stops there — four settlers
  fed to the hand at a field, six at ploughed fields, nearly nine at a grange, ten at the home
  farm. A town that has raised its reservoir keeps nobody hauling water and three people in its
  fields; a city keeps nobody hauling and eight.
- Which also makes them the only large binding works a bad season can reach, since a crewed work
  carries what it is actually running at and a staffless one carries its figure whatever happens
  to it. That is the price of the lane, and it is meant to be paid — until Addendum 10(b) closed
  it: every work now carries at its own condition, crewed or not, so ruin reaches the water and
  roof lanes too.
- **A home worn past condemnation stops being a roof.** It is not cleared, unbuilt or moved — the
  protection law is untouched — and putting the roof back on ends the condemnation. This is what
  gives a subsidence's ruin a real housing consequence, and everyone under a newly condemned home
  is recorded at a roof brink dated to the day the roof went, and warned about from there.
- A yard's throughput ceiling is denominated **per day** rather than per resolve, so a yard that
  ran for thirty days finishes thirty days of work and a yard walked past thirty times in one
  afternoon still finishes none.
- The three separate "the founder was not there when it came due" implementations — the manifest
  turn-back, the raid re-warning and the arrival catch-up — are one shared helper, so the fresh
  window a homecoming buys is computed the same way everywhere.
- Guests arrive at the gate and leave again through an absence, and the homecoming says who came,
  who waited, and how long ago.

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
