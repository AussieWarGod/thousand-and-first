# Changelog

All notable changes to The Thousand and First. Versions are semantic: patch for fixes,
minor for additive API and content, major for breaking changes. Supported API is defined in
[docs/API.md](docs/API.md).

## [Unreleased] — 0.2.0 in progress

Pre-release: the mod has not yet run in a live game. Nothing here is stable until the first
playtest passes.

### Fixed
- **Release art now uses only verified vanilla references or deliberate civic glyphs.** Fifteen
  pre-release bitmap drafts and their grid compiler are absent from the runtime tree; no game art
  is copied into the mod. The staged XML audit now checks every tile and animation frame against
  the installed base corpus, rejects local raster paths, and prevents `BaseFarmer.RandomTile`
  from silently replacing fixed settler and guest silhouettes.
- **New worlds no longer silently import a prior kingdom.** Cross-run realm import now defaults
  off, and its option says it must be enabled before world creation. Off exits before any legacy
  reservation and writes no decline, so latest eligible seal remains available for a later opted-in
  world; in-run seniority succession and seal capture are unchanged.
- **The refining yard applies its own wear** (QB-29 — a standing breach of the wear doctrine:
  crops and networks paid it, the yard never did). The condition rides the effort percent
  rather than the crew term, because every yard stands two and a percent folded into a head
  count of two truncates to a lie. A pristine yard is bit-identical; a neglected one now
  refines at the floor the doctrine always priced.
- **An `Open` plot underground no longer offers `taf:sky`.** Sky is weather-reach and weather
  reaches nothing under the rock: below the surface every tier now provides `taf:dark`, the
  open plot included. Offers are read against the ground a work stands on (the cache carries
  a stratum now); lodging assignment, the upgrade absorption read, and the settling notable's
  shade-taste all thread the ground through. Surface behaviour is unchanged, and the test
  that would have caught the defect lands with the fix.

### Added
- **The Charter is now a navigable book instead of a thirty-five-line wall.** Its root leads with
  status and the next need, then seven short chapters with local hotkeys, explicit Back, and
  controller cancel. Navigation spends no turn; every old action remains reachable exactly once;
  readings return to their chapter; a committed action closes the book and spends one turn. Raw
  founding ticks and ordinary trade internals no longer leak into the player report—the full
  diagnostic trade ledger remains available through the debug wish.
- **Water rituals now open research doors.** The first time a founder shares water with a
  faction, that faction becomes a permanent founder-held `rite:` source. The matching research head
  is revealed and seeded by 25% in each city, with durable per-city receipts, distinct-source
  stacking capped at 50%, and no path to automatic completion. Rituals performed before founding
  or between realms remain usable; Kingdom succession clears the dead founder's personal rite
  ledger while preserving the cities' own research standing.
- **The crown, the capital, and the arcology.** The crown is a building (never the seat — the
  seat walks with the founder, the capital does not): raise the crown hall and set the crown
  down, and the city is the capital; moving it means raising another and walking there, so
  the record's own cost is the price of a move. The mirror-gate network re-keys onto the
  capital — one column rewritten, no arch visited, no row lost — and gates do not go dark
  for politics: the cost of the move is that your crossings now land somewhere else, told
  before consent. Capital-specific works arrive on their own cardinality lane (`Capital=`,
  which never spends the city's purpose slot — the capital may still be the flesh-city or
  the chrome-city): the arcology and its first two interior records open the set. And the
  great works project outward: the hall's surgery and the registry office are cheap
  one-per-city satellites carrying the lower rungs only — the top rungs and the once-ever
  ceremonies stay where the colossus stands.
- **The delve — the way down is a built thing.** The distance file used to say it plainly:
  "verticality is free." It no longer is. Unbroken rock stops being a doorway: crews path
  between strata only where a shaft has been cut, and the climb costs three level hops with
  the load on your back. A claim below is ground you own and cannot work until the delve
  stands; the refusal names the shaft it waits for, and the shaft's completion names what it
  opened.
- **The becoming annexe — the city keeps its own rolls, and the old machines accept them.**
  The chrome half of the body doctrine (Addendum 22 A1; the registry fiction, F1). Enrolment
  is a once-ever ceremony that enters a citizen — or the founder — on the city's rolls as
  `enrolled:` keys on the settlement container, so a seceding city walks out with its own
  book, which is the fiction's teeth. The enrolled answer vanilla's own `IsTrueKinEvent` —
  raising, never lowering — so the Eaters' machines accept them unmodified, and the annexe
  grants the genotype's own two license points, derived not invented. The whole reach of the
  answer is disclosed at the door (it is wider than the nook — tonics, terminals, five
  Sifrah boards), the Mechanimist chrome-debt can be spoken about through the shipped
  petitions, and losing the rolls closes a door without ever reaching into a body. Gated
  behind the foundry's new capstone node, `the rolls`, which a Templar city never sees.
- **The method factor reaches the fields and the city book.** Harvests, gathers, and the
  book's food and water rates now carry the keepers' method bonus — never below baseline,
  never a tax, never scaling what a city consumes.
- **The lab, rungs 0 through 3 — the flesh half of the body doctrine.** The butcher's slab
  reads a body before it cuts it; the vat-house preserves what was read into permanent,
  tradeable parts on vanilla's own preservation arithmetic (corrected against the decompile:
  `Number × Count`, and the doc's third factor never existed); the grafting hall performs
  Class I riders and Class II defences from a thirteen-record catalogue where `Grants` names a
  part class and never a creature; the chimeric theatre — the mod's **first megastructure**,
  one purpose per city under Design B — changes anatomy outright and holds the four named
  procedures: the Weeping Graft, the Chimeric Confession, the Cold Regard, the Lantern Rib.
  Once each, ever, found in the world and never listed until found. Every rider class was
  verified against the engine's own event routing (`Attach="body|weapon"` — five of the nine
  planned riders would have been inert as body grafts and now are not), refusals name what is
  missing, creed friction rides the shipped machinery, and the one random procedure is a
  confessed gamble priced as one.
- **The keepers' research — a tree without a second job** (Addendum 14, RESEARCH-SYSTEM-DESIGN,
  Addendum 22 B+E clusters). Twenty-one nodes over three branches and a trunk, each minting an
  ordinary `node:` roster key — no parallel gate system, no meter, no currency. Research is a
  scaffold for an idea: a staffed knowledge bench (the scriptorium, until the lab wave) charges
  real elapsed labour against one named subject; idle time is spent and never banked. The tech
  map keeps its no-press law and gains two chapters, and its locked rows now honour the
  visibility law — computed over what this realm has actually heard of, which is a deliberate
  behavioural regression to the founder's convenience and the correct one. Research TIER is
  gated on the city's best researcher's Intelligence (10/14/18/22, hard at the boundary);
  `schooling` may raise a citizen's Int cap by exactly one, enforced in our own training code —
  `Statistic.Max` is never written.
- **Knowledge lives with the city that learned it** (Addendum 22 B1–B8). The keepers' roster
  moves off game state onto the settlement container — so secession walks out with what the
  city knew, exile carries doors and never rooms, and a returning city brings its rolls back
  whole and free, all with zero new code in any of the four paths. The realm reads the seat;
  teaching the other city is an act (a disk re-teaches freely, a machine re-certifies where it
  stands, a node is *set down* as a seed — reveal and head start, never completion). Craft is
  per-city and a design's `MinTech` is judged against the city being built in. Serialization
  bumps to v7, named; an old save's realm-wide roster folds into the seat once and the old key
  is retired.
- **The mirror-gate — step from one of your cities to another** (exotic #3,
  END-STATE-CITIES-RESEARCH §4.4). A `TeleporterPair` extension, so the crossing itself is
  vanilla's; ours adds the dedication rite on the arch, a standing draw of three
  charging-posts' worth a day on the 12(g) lane at **both** ends, and a brownout that closes
  it and says so once. Crossing costs nothing beyond the draw (Addendum 22 A2 — no second
  toll) and there is no cooldown (Addendum 8 — no timer of ours). Gated `MinTech="arclight"`
  (the catalogue's first) plus a certified `Reprogrammable Recoiler`. Pairing lives in a
  realm register in game state, not on either arch, so the capital-hub topology can be
  re-keyed later without a data loss (QUESTION-BACKLOG QB-1).
- **The deep is a building set, not a dimmed surface list** (Addendum 15). The `Strata`
  attribute lands on the zoning gate — home stratum first, share-tags after, absent admits
  everywhere, `all`/`!` spellings shared with `Styles`, sky answering as a filtered subset
  of the surface. Six deep records open the set: carved cell and carved gallery (the rock is
  three of the four walls), fungal vault and vault galleries (one keeper, no season,
  `taf:damp` for the people who want it), the deep cut (the yard the deep gets because spoil
  has nowhere to go), and the niche tomb (a cairn is stacked; a niche is cut). The weep-tap
  and weep gallery declare the deep their home out loud; the mason's yard and the cairn stay
  above ground and say so. Refusals name every stratum that would take the design, per
  STANDARDS 7b.
- **City styles finally mean something, and they are tags.** All five (`common`, `verdant`,
  `fungal`, `gyre`, `eater`) now filter the catalogue: nineteen designs are refused to the ground
  that has no business raising them — the dew lane out of the marsh, the salt-pan out of the
  spore-light and off the Moon Stair, the seep out of a ruin field, the timber lane off the salt
  flats — each with its reason written beside it in `KingdomBuildings.xml`. Twelve style-exclusive
  designs land in the same pass, one to three per style, so no style reads as pure removal.
  `_notes/balance-sim.py` Q12 asserts that every style still holds every rung.
- **The `Styles` mechanism is the general tag idiom** (`KingdomZoningRules.TagAccepts`): case
  folded, `all` for everywhere, and a leading `!` for "everywhere except" — vanilla's own
  negation operator, because the style set is open and enumeration goes stale the day somebody
  ships a sixth style. `KingdomRules.StyleAllows` keeps its published signature and delegates.
- **Creed-gated designs, and the whole gate stack behind them** (Addendum 16): `Builders` (who
  must be standing here, by kind and by count), `Creed` (raised only by builders who hold the
  creed **or have previously held it**), and `CreedShare` (how much of the city holds it, derived
  from `KingdomCreedRules.DominantCreed`'s own threshold). Three creed-unique works ship on it —
  the Barathrumite under-bench, the Mechanimist reliquary, the Consortium factor's house.
- **A settler's creed history is a recorded fact.** Leaving a creed writes it into a bounded,
  never-rotating record on the settler (`KingdomCreed.CreedPastProperty`, at the one conversion
  path) and into the city's own book and tally. The resident row grew eight bytes for it and
  LIVING-CITY-ARCHITECTURE §0.0(c) carries the same edit.
- **The visibility law reaches the catalogue.** A creed-gated design a city has no path to —
  nobody holds the creed and nobody living there ever has — does not appear in the commission
  list, the plan list, the settlement's own choices, or the keepers' map. Every other refusal is
  still spoken out loud and still names its fix.
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
- Both city books advance on the realm heartbeat, including the city the founder is not standing
  in. Its physical ground remains unloaded and untouched; production, upkeep, bounded arrivals,
  brinks, and news reconcile through the model, then the next fresh seated pass projects what the
  active zone can actually prove.
- Ground the realm's other city holds cannot be claimed by this one, even forced.

### Added — the larder and the shared meal
- Containers can be dedicated as **larders**. A civic larder can also be commissioned. Food in a
  dedicated larder is counted for the settlement; dedication is a mark, not a transfer, so nothing
  is moved and an undedicated container is never read.
- **Share a meal from the larder**, from the Charter, when the larder can feed one. Food is spent
  from dedicated larders only, a settler from the roll speaks, and it becomes the settlement's
  deed, so word travels the way it does for any deed.
- Calling a shared meal remains optional: an empty larder spends nothing on that action. Ordinary
  settlement upkeep is separate. Above the Camp foraging ceiling, people eat one ration each per
  day and a shortage advances the witnessed hunger ladder described below.

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
  distance-scaled days. The sign is the designation and one haul rides at a time. A raid warning or
  live raiders at the destination hold the exact load in escrow; the first later safe settlement
  pass delivers it. Absence never invents a road loss.

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

### Added — meals with a name, and a mill that eats the harvest

Addendum 11(b)'s last clause and 11(c). Food had a beginning and an end and nothing in between:
the settlement ate an abstract ration tick, and the one design whose whole claim was a
*transformation* was a glyph with a number on it. Both ends are grounded now, and both in
vanilla's own machinery rather than beside it.

- **The realm has a favourite dish, and vanilla already had a place to keep one.** A faction
  declares `<waterritual Recipe=… RecipeText=…/>`, which parses onto plain fields on `Faction`
  that the faction's own serializer writes and reads — so the runtime faction this mod mints
  carries its dish across save and load with no persistence of ours. Eight vanilla factions ship
  one. The realm makes nine.
- **It is derived, never authored: creed picks the form, ground picks the body.** Your people's
  dominant creed lends the *shape* they make food in, borrowed from that faction's own vanilla
  dish; the ground you founded on lends what it is made *of*. People who hold with Joppa, founded
  in a marsh, are known for **vinewafer matz**; people who hold with the Barathrumites, on flower
  fields, for **starapple porridge**. Hold with nobody and it is a stew, which is an answer and
  not a fallback. Every form word is one vanilla's own recipe-tile generator can draw.
- **A stranger can learn it.** The dish is offered through the water ritual in vanilla's own
  sentence — *"Would you teach me to cook &lt;realm&gt;'s favorite dish?"* — reputation-priced, and
  it lands in your journal as a real recipe. If the roll drifts and your people change their minds,
  the kitchens change with them, and the chronicle says so once.
- **The communal fire always was a real kitchen; now the settlement knows it.** `Campfire` is the
  entire cooking system in Qud, and the fire's blueprint has been vanilla's `Campfire` since it
  shipped. Any finished work carrying that part counts as somewhere to cook. Above it, the
  **settlement oven** — an upgrade from the fire, a real vanilla `Oven`, with the realm's own dish
  on its preset meals. Every named settlement in Qud has exactly this: its own oven, its own
  signature meal.
- **The daily draw is meal-shaped.** Same servings as before — a meal is a rendering of the ration,
  not a second bill — but it reaches for the settlement's own **staple** first, larder by larder
  and item by item in a stated, deterministic order, and then for everything else. A day where a
  kitchen stood and half the bill came off that staple is the settlement eating its own dish, and
  it is worth **one more settler for exactly one day**, re-earned every day. Both halves of that
  are vanilla's arithmetic: a non-player's meal effect lasts 1200 ticks, which is exactly one
  settlement day, and only one stands at a time. It rides the same capped lift term as a notable
  and a shrine, so nobody eats their way past their own water.
- **A city that ate scraps says so, once.** From a Village up: the larders gave nothing and the
  settlement lived off the ridge. Unsaid the moment it eats out of its own stores again. A Camp
  living off the land is a Camp working as designed and hears nothing.
- **The grinding mill is a millstone.** It carries vanilla's own `Mill`, a `Container` you can
  open, an `Inventory`, and a mechanical-power **consumer** — the first consumer this mod has put
  on the mechanical grid, so a mill raised beside the settlement water wheel is genuinely driven
  by it. Hand-feed it and it preserves at vanilla's per-crop numbers while you watch.
- **And industry eats food.** The settlement's own pass grinds two crops a day out of the larders
  into six preserved staples — a net of four servings, which is exactly what the design has always
  declared it carries. What comes back is what went in times three, vanilla's own
  vinewafer-to-sheaf figure and the thinnest of the three the mod's crops offer, so the mill can
  never book more than the game itself gives. The staple it makes is the staple the dish is made
  of: fields, mill and table are one chain.
- **Residents eat first, always.** The grinding runs after the day's rations are drawn, and even
  then only on what stands above one more day's bill. A settlement cannot go hungry because its
  mill was busy. And a mill's food is subtracted from the clocked daily make exactly as a sown
  field's is, so one millstone feeds the settlement once.
- Two new preserved staples for the two crops vanilla cannot preserve — **pickled godshroom** and
  **mashed dreadroot** — each inheriting the nearest shipped preserve, so neither owes new art nor
  new plumbing. `TeachesDish` was surveyed and deliberately not taken: with the faction recipe set,
  every citizen already teaches the dish, and the part would only let one named cook teach a
  different one.

### Added — seeds, real rows, and a harvest that runs without you

Addendum 11(b) and 11(b-ii). Food was a flow with no beginning: a field was a `Carries` number on
an object with no parts, and a settlement that raised one started eating out of nowhere. It starts
from seed now, it stands plants you can walk into, and it goes on feeding the city while you are a
hundred days away.

- **Nothing grows until you sow it.** A field carries **no `food` at all** — not to the level, not
  to the day — until the founder puts a seed in it, and the refusal names the want the first time
  and once. An unsown field also drops out of the staffing pass, so bare ground never takes the
  four hands a home farm's crew wants and turns them into nothing.
- **Five seed items, three honest ways to get one.** One per crop family, in Qud's own idiom
  (weight, value, a description that is about a seed and not about a system). Traders carry them;
  a working harvest returns its own on a deterministic draw; and where vanilla ships a wild plant
  of the species — watervine, starapple, godshroom, dreadroot — that plant gives up its seed once,
  to anybody who does not have to steal it. Seeds are deliberately **not** `Food`: a seed the
  ration draw can eat is seed corn that quietly disappears.
- **Sowing is a designation, and withdrawing it is yours.** Stand in a field, **Sow**, and you are
  shown the crop, the rows, the wait and the water before one dram or one seed is spent. The field
  offers **Withdraw Seed** for as long as it is sown: the rows come up, the seed comes back, and
  nothing else can take it.
- **The rows are real.** A sown field lays actual crop plants across its footprint — vanilla
  `Plant` for the rootedness, vanilla `Harvestable` for the ripe/unripe swap and the harvest verb.
  They go in green and ripen on the cycle. You get a full day alone with a ripe field before the
  settlement's own hands arrive, and every row you gather by hand is a row the settlement does not
  also get. The harvest that crystallises **is** those plants' yield.
- **The cycle runs in absence, and tells it once.** Planted tick → ripe after the crop's six days →
  gathered, attended or not, dated to the tick it was due → restamped **from the harvest** so the
  part-cycle already grown is kept → repeat. A season away resolves every completed cycle in one
  closed-form reckoning, and the chronicle carries **one line with a count** rather than one line
  per harvest. It stops when the seed is withdrawn, when the field is condemned, or when nobody is
  working what the design says needs working — and idleness costs the delay, never the crop.
- **A harvest can cross zones.** The city's stores are credited the moment it comes due; the
  physical crop goes into a pantry in this zone if there is one, and otherwise takes to the road
  and **materialises in a larder in another of the city's zones the next time you walk in there**.
  Which zones have room is read off a dated sighting record — the same crystallise-at-awareness
  idiom the rest of the mod runs on. Nothing is ever touched in a zone nobody is standing in.
- **The food figures are derived now, not authored.** A design's `Carries="food:N"` is
  `Rows × YieldPerRow / CropDays` off the `r_KingdomCropRows` tag on its own blueprint, exactly as
  a water design's is `1200 / mean(VariableRate)` off its `LiquidProducer`. `_notes/balance-sim.py`
  re-derives all six and fails the run if one drifts; it also proves the cycle pays exactly what
  the carry promised over one crop's days, so a sown field feeds the settlement once and not twice,
  and that nothing carries `food` it neither grows, keeps, nor mills.
- **The irrigator finally does something.** `Hydraulic Irrigator` ships a real
  `RadiusEventSender Event="AccelerateRipening"`, and vanilla `Harvestable` answers that event by
  calling `Ripen()` — which returns immediately on every blueprint in the game, because not one of
  them arms `RegenTime`. A powered irrigator standing beside one of our fields pulls its stamp
  forward each pulse, so an irrigated crop comes ripe in half its days: the machine's own radius,
  the machine's own charge, our clock.
- **The gate does not wall the early game.** Foraging is untouched, and the sim asserts that Camp
  and Steading are both held by the wild plus the designs that need no seed at all. A founder
  hunting for their first seed is never starved for it.

### Added — food becomes physical

The water lane had a producer, a consumer, a store and a ladder; the food lane had a catalogue
figure that bound the level and nothing that ever moved a ration. It is a mirror now, function
for function, and the three places it deliberately diverges are written down rather than left to
be inferred.

- **The fields make what they promise.** `KingdomGrowth` stores `Supports(survey).Food * days`
  into the larders on world time, off its own `LastFoodWorkTick`, planted before the first count
  exactly as the water works' stamp is. Its own checkpoint and not a share of the water one,
  because the two producers are separately blockable: a settlement can have casks with room and
  no larder dedicated at all, and a shared stamp would let whichever good was flowing spend the
  other's days.
- **The settlement eats.** One ration a settler a day, drawn in the heartbeat beside the water
  bill and before anything else can spend it. Uncapped over an absence and never a debt, like
  every other bill in the mod: what a settlement could not pay it simply did not eat.
- **No stage rate on food, deliberately.** Water is billed 100/120/150/180/220 per hundred by
  stage and its `Carries` are divided back out by the same percentage; food is billed flat and
  handed to `Equilibrium` undivided, because a dinner is counted in people. That flatness buys
  the lane its whole property: *a settlement standing at its own supported level makes exactly
  the rations it eats*. Thrift and the agrarian district are likewise left on the water side
  rather than spent twice.
- **Foraging, which is why a camp never starves.** Free hands bring in two rations a day each
  under a flat ceiling of four — the same figure as `FloorLevel` and as the Camp rung's own
  population ceiling — and it is eaten hand to mouth rather than stored, so a settlement that has
  dedicated no larder still eats. A camp of four with half its people on the water detail feeds
  itself off the ground with nothing commissioned, which is the food half of the promise the
  water lane already made at that rung. Nothing above a Camp can live on it.
- **The hunger ladder**, rung for rung the thirst ladder in food's own voice: a streak, a warning
  said every failed resolve, departures from the second, and a `famished` mark from the third,
  with the same two floors — a Camp is never marked and the loyal core never leaves.
- **One bite between the two ladders.** Both run, each keeps its own streak and says its own
  sentence, and what a failed resolve *costs* is `KingdomRules.ComposeScarcity`'s **maximum of
  the two, never their sum**. A city that is dry and starving loses one settler for it, not two,
  and may wear both marks: a mark is a state and a departure is a cost, and only the cost is
  capped. Subsidence is untouched underneath both — the structural consequence and the immediate
  one are two sentences about the same bad year, not one counted twice.
- **Larders hold a declared amount**, declared on the blueprint (`r_KingdomLarderCapacity`) and
  never in the catalogue, for the same reason a cistern's `MaxVolume` is: what a design adds to
  the level is a catalogue fact and how much its vessel holds is a fact about the vessel. The
  larder shed holds 64 and the granary 288 — about thirty-two days of what each carries, the
  cistern's own ratio. A container the founder dedicated by hand holds 32.
- **A commissioned granary dedicates itself**, which STANDARDS §7 already promised ("commissioned
  storage auto-flags") and only the larder shed was delivering. It is also a repair: a granary
  raised by an earlier build becomes a pantry the next time its city is walked into.
- **A harvest with nowhere to go is said once, by name** (7b), and unsaid the moment there is
  room — with different words for "no larder at all" and "the larders are full", and the figure
  in the homecoming ledger either way.
- **Food spoils in a damaged larder.** `LeakKind.Food` is Addendum 10(b)'s explicitly deferred
  third kind — "food spoilage waits until food is a flow" — and the deferral is spent: same
  `Leaked` arithmetic, same day-banking, same announce-once and unsay-on-mending. It is drawn
  after the day's eating, so it can never be the reason a settlement goes hungry, only the reason
  it has no cushion when something else is.
- The homecoming ledger carries the food side beside the water side in servings rather than
  drams, and says so; the Status report names the larder against its capacity, what the fields
  make against what the people eat, and the hunger streak beside the thirst streak.
- `r_TAF_OptionThirst` now switches **both** binding goods. The ID is unchanged so no save or
  settings file notices; only its display text moved. A founder who turned scarcity off did not
  ask to keep half of it.

**Not in this change, and named rather than assumed:** no charter carries food, so the only ways
into a larder remain the fields, the garden and your own hands. `_notes/balance-sim.py` Q11 models
the lane end to end — supply against consumption per rung, how deep the larders are against how
deep the casks are, and all sixteen pairings of the two ladders with the no-double-collapse
property asserted rather than argued.

### Added — the city spans ground

A realm could hold two cities from the start, but one city could still only ever hold the single
parasang the founding rite poured on. Everything downstream of a second zone — districts, walls,
the eight `MinZones` designs, vertical claims below or above the seat — was already built and
tested and had nothing to exercise it. This is the verb that reaches it, plus the two real fixes
it exposed.

- **Claim the ground you're standing on.** Charter → **Works & ground** → **Claim this ground** takes
  bordering ground into the seated city, including the stratum directly above or below what you
  already hold — a cellar or a tower is a claim now, not only a founding-day accident. It costs
  nothing, and that is a decision: the brief prices founding and every building and names no price
  for a claim, because what a claim actually costs is paid afterward and in kind — a new wall line
  to raise, a new budget of ground to lay, and a stage that has to have been earned first. Every
  refusal names the lack and what would lift it — no realm yet, ground already the seat's or the
  other city's or an exiled realm's or a stranger's, not bordering, or the rung's ceiling already
  reached — and a claim that goes through says what it did to the wall line, including when the
  honest answer is "nothing moved," which is the case for ground taken diagonally across a corner
  or straight down into the rock.
- **How much ground a rung answers for is read off the catalogue, not invented.** The eight
  `MinZones` designs already line up with stage — the two-zone designs are `Village`, three-zone
  `Town`, four-zone `City` — so the claim's own ceiling reads that pairing back
  (`KingdomZoningRules.ZonesForStage`): a settlement reaches the ground a design wants at the same
  moment it reaches the stage that design wants.
- **A multi-zone city's level was a memory of only whichever zone you last stood in.** Walk in
  through the mine and the granary vanished from the sum; the stage ladder read the same way. Now
  every claimed zone's binding carries and dedicated storage are recorded, dated, the moment the
  founder stands in it, and a reading folds in every OTHER claimed zone **as it was last seen**,
  never simulated forward. The Status report says how old that memory is — "counting one parasang
  as you last saw it 6 days ago" — and the stage ladder now reads the city's whole storage rather
  than one zone's, closing a real bug: a multi-zone city could demote itself on the settling pass
  for no reason but which of its two treasuries the founder happened to be standing beside.
- **Underground stopped lying about the sky.** The commission menu now gates by stratum at the
  moment the founder is choosing, not only once they've picked a design and the plot registry turns
  them away later: a sky-wanting design (the dew catchment, the catchment bank, the sailvane)
  carries **[wants open sky]** in the list, the same tag every other blocked gate wears, and refuses
  by name — "there is none under the rock" — instead of the catalogue silently shortening.
- **And underground stopped turning fields into sealed rooms.** A design declaring `Open="yes"` now
  stays open when it's carved into the rock: a salt-pan, a market square, a tended plot cut into a
  cellar is open ground with stone around it, not a chamber with a floor, a door and room for a bed.
  Carving still replaces the enclosure a design would otherwise have raised on the surface; it never
  roofs ground the design deliberately left unroofed.
- **The gatehouse finally sites itself where its own name says it stands.** It was the one design in
  the catalogue meant to be sited by a rule rather than by size, and the rule was never wired — it
  went wherever an ordinary wall segment happened to land. It now sites at the buildable frontier
  cell nearest the settlement's own worn way out, the same cell its road errand already walks to,
  ties broken north-then-west, so the same settlement puts its gatehouse in the same place every
  time it's asked, reload included.

### Changed — water is made, or it is carried; nothing conjures it

Addendum 11(a): *"we shouldn't have a random plot that just produces water without any logical
reason as to why the building on the plot is producing the water."* The survey behind this wave
(`_notes/VANILLA-PRODUCTION-TRUTH.md`) went looking for the reason and found the hard fact under
it first: **it does not rain in Caves of Qud.** There is no precipitation system at any level —
`Zone.CheckWeather` rolls wind and nothing else — so a catchment that filled from rain was
filling from a weather system the game has never had, and a reservoir that "holds what falls into
it" was holding nothing. The whole lane is re-grounded on things that do exist.

- **Storage stopped conjuring.** `cistern`, `cisternvault`, `reservoir` and `waterworks` no longer
  declare `water` at all. This was the largest unearned number in the catalogue and it was worse
  than a level figure: `Carries="water:N"` is banked as a real daily flow as well, so a vessel
  declaring it was minting the water it claimed only to be holding. What a store is paid in now is
  capacity, which is a stage gate in its own right (16 / 64 / 256 / 1024 drams of dedicated storage
  for Steading / Village / Town / City) and the clamp on how much the water detail may haul — and
  the four of them got cheaper rather than bigger, because what they stopped claiming they stopped
  charging for. Vessel sizes are now derived too: a holed store leaks its own capacity over fifty
  days, so every one is sized to keep that daily loss under the drinking bill of the rung it opens
  at, and the balance model asserts it rather than describing it.
- **Every producer's number is derived from its own part.** A design that declares `water` carries
  vanilla's `LiquidProducer`, and its figure is `KingdomRules.TicksPerDay / mean(VariableRate)` —
  stand next to one for a day and count the drams, and you get the number in the catalogue. All
  nine of them are re-derived from the XML on every run of `_notes/balance-sim.py`, which fails if
  one drifts. Every producer also declares `FillSelfOnly="true"`, without which `LiquidProducer`
  overflows by creating an open pool the water detail can then haul — the same dram minted twice.
- **The catchment lane is dew, not rain.** Renamed and rewritten around vanilla's own `Air Well`,
  which is Tier 1, needs no power of any kind, and says in its own description that what it
  catches is dew condensing on cold stone. `Sky="yes"` survives as a **siting** rule — this work
  wants the open night over it — and never as a weather read. Two new rungs continue it: the
  **air-well court** (middling plot, Village) and the **air-well field** (large plot, Town), which
  is the staffless 25-drams-a-day work a Town hands its water bill to.
- **The underground has a water lane of its own.** The **weep-tap** and the **weep gallery** are
  built on vanilla's water weep — the lichen carrying `LiquidFont`, which is how every indefinite
  underground water source in the game works. They tap a damp seam and case it, they want a crew
  because a weep left alone closes over, and they are the answer for a settlement that lives under
  the rock. (They express the tap with `LiquidProducer` rather than `LiquidFont` itself, because
  `LiquidFont` wets the floor rather than filling a vessel, and a floor full of fresh puddles is a
  seep paid for twice.)
- **The city rung is a certified machine, not a commission.** The **condensing hall** is gated on
  `MinTech="foundry"` and `Knowledge="machine:Solar Still"`: the founder drags a dead still home
  onto claimed ground, the keepers pass it fit for the grid, and what the settlement builds out of
  that knowledge afterwards is its own. Vanilla draws the same line itself with `TinkerItem
  CanBuild` — the Air Well is buildable, the Solar Still is not — and the catalogue now keeps to
  it. It carries a real `SolarArray` and `Circuitry`, which is `Solar Still`'s own configuration.
- **Nothing in the water lane opens at Camp.** The salt-pan and the dew catchment moved up to
  Steading. A camp drinks the stock the founder arrived with, what the detail hauls out of the
  site's own finite pools — which do not refill, and which count only if they are *pure* water —
  and what a charter pays in. The first producer is a thing you earn at five settlers and sixteen
  drams of storage. The camp costs water; that is the point of the camp.
- Every rung is still holdable by both a cheap plan and a grand one, and the balance model
  asserts that too rather than reporting it. Steading's grandest actually holds more than it did
  (8 against 6); Town holds the same 26; City slips from 70 to 68.

### Fixed — the water wheel stood still

`r_KingdomWaterWheel` carried vanilla's `HydroTurbine` but not the `SpawnWithLiquid` that vanilla's
own `Wooden Water Wheel` carries beside it. `HydroTurbine` sums the liquid in its own and adjacent
cells and reports `HydrodynamicForceInsufficient` under 400 drams, so a wheel raised anywhere but
on standing water reported nothing but that failure, forever, with the catalogue's own display name
as the only hint. It now digs itself a race the way vanilla's does — one 500-dram brackish puddle,
`AdjacentPoolChance="0"` so it never wets ground the settlement did not clear. The wheel turns
anywhere now, at about two per cent of its rating, and siting it beside real water is worth a
hundred: a badly sited wheel fails visibly and by degree instead of silently and absolutely.
Nobody drinks out of the race either — it is a salt mixture, and the settlement's survey counts
only pure water — so the fix mints not one dram.

### Added — the ceremonies everybody attends, and the numbers that were being thrown away
- **Every building is raised with a ceremony now, not four of fifty-seven.** A house, a field, a
  larder or a temple finishing while you stand there gathers whoever is nearby, shares a measure
  of water and chronicles who was present, exactly as a palisade already did; raised while you are
  away, the homecoming tells it plainly. The plot path used to write a bare line instead.
- **The chronicle can quote a plan for a house.** A plan staked for a plot-sized design now carries
  the surveyor's words through the works and into the raising, the way a single-cell plan always
  has. It used to be dropped the moment the plot measured its rect out of the marker's cell.
- **A notable is worth something to the settlement.** Met tastes, a leader's virtue net of their
  flaw, and the `Prefers` their quarters happen to meet are one shade on the settlement
  (`KingdomSystem.NotableShade`), and the level reads it — up to five settlers, bound by the same
  half-the-binding-level cap a shrine is bound by, and never a penalty when nothing is met. It was
  computed, printed in the chronicle, and read by nothing. The status report names it.
- **A household's yard trade feeds the settlement.** A vine lattice's `food:1`, a hide rack's
  `craft:1` and a vellum press's `learning:1` now reach the level. They were parsed, capped, listed
  in the Charter's own menu, and consumed by nothing.
- **Lifts land where they reach (Addendum 6).** Craft, faith, learning, order and luxury now lift
  the settlement in proportion to the roofs their work actually covers, instead of counting
  citywide off the catalogue. Water, food and roofs are unchanged — they are drawn and carried, so
  they stay citywide pools. Consequences worth knowing before you build: a quarter-band design
  among the houses is worth all of it and the same design out past the fields is worth a fraction;
  a headed great work in another claimed zone still carries whole; and an `S` design shades the
  ground it stands on rather than the population count, so a camp whose only civic works are a
  shrine and a fire pit no longer gets comfort headroom for them. Every rung is still holdable on
  its binding goods, and one named notable puts a camp's headroom back.

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
### Added — the city book: one home for what every zone holds
- **A settlement now carries its whole model.** `KingdomSettlement.City` is a named-field
  composite holding stocks, one row per claimed zone, one row per standing work, and what each
  zone still owes its own containers. It is carried by the seat swap on its own name, exactly as
  the ledger is.
- **Two families of game-state keys retired.** `r_TAF_Supports_<zoneID>_*` and
  `r_TAF_Larders_<zoneID>_*` are gone. `KingdomSubsidence.RecordZone` / `OtherZones`,
  `CityTally` / `CityStorage`, and `KingdomCrops.RecordLarders` / `LarderRoomElsewhere` all read
  and write zone rows instead. Same answers, one home; `ZoneSighting` survives as the projection
  the rows hand the subsidence arithmetic, so that arithmetic did not change at all.
- **Check-in and check-out.** The settlement pass gained a `check-in` step (after `survey`,
  before `trade`) and a `check-out` step at its end, and the system now listens for
  `SuspendingEvent` — the true last read, which fires before a zone suspends and while its
  objects are still in RAM. A missed check-out costs freshness, never correctness: check-in
  reconciles against the ground either way.
- **The city drinks from its own second parasang.** When the seated zone cannot cover the day the
  settlement is about to be billed for, the city's water and food are carried in from the zones
  that hold them, oldest dedication first. Nothing is created: what leaves one zone's row arrives
  as a debt against that zone's real vessels, paid the next time anybody opens them. A one-zone
  city, and a city whose seated zone can pay its own bill, are untouched.
- **Deficits drain real containers, in a stated order.** A zone's standing debt is applied to its
  dedicated vessels and larders in **dedication order** — oldest first — through
  `KingdomLiquids.Drain`'s measured delta, never a trusted return value. The order is a stored
  fact (`KingdomDedicationOrder`, minted the first pass that counts a container as the city's),
  so it is deterministic without a draw and the same vessel drains first after a reload. What the
  containers could not cover is named in the ledger, never silently forgiven.
- **The ground wins, and the difference is told.** A cask with less in it than the book expected
  means the founder poured some. Check-in attributes the difference and says so; it never
  silently repairs it.
- **The perf receipt.** Every reckoning goes through the executor seam and writes one
  `[TAF] perf reckon` line — breakpoint steps, row-visits, draws, milliseconds — judged against
  the lane's own budget, with a `BUDGET` prefix and the figure it broke when it crosses one.
  TESTING.md gains Pass 32 for reading them.
- **The realm's simulation seed** is minted once at founding, from the world seed, the realm's
  name and the tick the water was poured: deterministic across a reload, separated between
  realms, and refused rather than re-minted.

### Added — the city renders: the hour, the porter, and the pump
- **A settler's day has a shape and their body follows it.** The hours of the game's own calendar —
  The Shallows and Harvest Dawn, the Salt Sun, Hindsun, Jeweled Dusk, the Beetle Moon — decide where
  each crewed settler belongs, and their anchor moves to it. **Vanilla does the walking.** Stand in
  the market at dusk and it empties itself, one settler at a time, on their own feet; the watch keeps
  its post all night, and a settler the works have no room for genuinely spends the day at home.
  There is no scheduler here and there is none in Qud: this rides the one idle hook the engine has,
  the same one vanilla beds use to send villagers to sleep, and it costs nothing per turn in any
  zone you are not standing in.
- **`KingdomGrowth.AssignWork` writes down who it crewed.** Ablest-first assignment always knew which
  settlers it put on which work; until now it threw that away and only stamped the work. A settler
  now carries their post, and a settler taken off one stops walking to it.
- **A porter brings the harvest to you.** A load already on the road arrives **embodied** when you
  are standing where it is going: somebody walks in at the edge nearest the field it came from,
  crosses to the larder beside you, puts **real crops** in it, and goes back the way they came. If
  you are somewhere else it lands the way it always did. One effect, at most one rendering, and the
  rendering is chosen by where you are standing rather than rolled for — so a reload never re-rolls
  a person and nothing is ever delivered twice.
- **You can follow one.** A job is a timed itinerary computed once, so the model has one answer for
  where a carrier is at any tick and every zone draws that same answer. Walk out of a zone behind a
  porter and you come out beside them, a cell or two inside the edge — cross faster and you catch
  them at the boundary, dawdle and they are further on. Block one long enough and the job **fails**,
  is named, and the crop is real items lying where they stood.
- **The stale-transient sweep lands.** A carrier who froze into a zone with goods on their back while
  the model finished the job without them is removed when that ground is next opened, and what they
  were holding went back into the city's books before the pack could be looked in. It removes only
  what the simulation itself made; anything you put on them, or anything the game calls important,
  is set down on the ground and never destroyed.
- **Materialisation is amortised, from the first commit.** What a zone owes the ground is paid on a
  per-turn budget — eight weighted units, at most four of them bodies, **what you can see first** —
  and never in one activation. Walking home after a season no longer asks the game to do a season's
  work on one frame; you watch the granary fill as you walk. Walk out mid-catch-up and the debt
  resumes at exactly the figure it left at.
- **The city keeps time while you stand in it.** Every in-game hour the realm advances all its
  cities' models by the elapsed and says at most one line about it. It is the same advancement the
  homecoming reckoning runs, split — never a second clock — and it costs about four row-visits a
  turn per city.
- **Roads are logistics now.** A leg that follows a laid road costs 0.6 of the same distance
  unpaved, applied identically to the estimate and to any later measurement, so laying a road
  visibly shortens every itinerary that uses it. The two-level distance matrix behind that is
  all-pairs over the claimed zones in 729 integer operations and composes any cross-zone distance in
  constant time, at four zones exactly as at nine.
- **The receipt grows.** `perf reify`, `perf slice` and `perf thaw` join `perf reckon` in the
  `[TAF]` log, each with counts as well as milliseconds — because a timing is hardware and a count
  is a contract. A figure that crosses a budget is still prefixed `BUDGET` and names the budget it
  broke. New **Pass 34 — a day in the city**, and Pass 32 gains the porter, the follow, the
  amortised drain and the slice.
- Prefetching a neighbouring zone ahead of you ships complete and **switched off**: it buys
  smoothness rather than correctness, it is the one part of this design that is safe to cut, and it
  has no options checkbox yet.
- Job minting stays to this one flow; the city's other errands, nearest-holder sourcing and
  trip batching are a later wave's, because they only start to matter once many jobs compete over
  many stores.

### Added — the city gets plumbing, and the works share one accounting

- **Lay a main and your quarters share their water.** Water mains, brine mains, a crossing piece and
  taps are in the pattern book. A line laid between two quarters runs **downhill and stops level** —
  the fuller cistern gives to the emptier until both stand at the same fraction, no further, bounded
  by how much the narrowest length of it will pass in a day. It works while you are three zones
  away, which is the one thing the game's own hydraulics cannot do: a vanilla network cannot cross a
  zone boundary at all.
- **A line carries one liquid and says which.** Lay a brine main against a fresh main and they do
  not quietly become brine — the join is **refused, by name**, and the message tells you what to lay
  instead. That is the whole rule: *connection is declared, never inferred*. A crossing piece takes
  one line under another through a single tile without either touching the other, and it holds no
  liquid of its own, so it can never be the place two of them met. Mixtures are a later thing; this
  wave will not invent one behind your back.
- **A tap is an act, not a proximity.** Standing a cistern beside a main joins nothing to it. You
  tap it, and that is what puts it on the line.
- **And you can watch a main run.** Stand in the quarter and drams actually cross between real
  casks, a few at a time, on the game's own clock.
- **Power went onto one accounting.** The works, the salt store and everything that draws charge are
  now solved as a network — supply against demand, the salt spent before anything stops — off the
  city model's own clock instead of a second one of their own. Nothing about what a mill or a wheel
  or a vane makes has changed; what changed is that a day can no longer be counted twice by two
  systems that each thought they owned it.
- **When there is not enough to go round, the lights go down in a stated order.** Forges and
  workshops first, then the refining yards, then comfort and lodging, then the food works, then the
  water works, and the watch last of all — newest built before oldest within a rung. It is said
  once, by name, where you will see it, and when supply comes back it simply comes back: a city that
  announced every recovery would be a city that never stops talking about itself.

### Added — every quarter of the city works, and no carrier walks past a nearer store
- **The works of a zone you are not standing in now make what they were built to make.** Until now
  a reservoir in the next quarter produced nothing at all while you were elsewhere: the settlement
  pass credited only the ground under your feet, on a settlement-wide stamp, and the city model was
  deliberately shipped running at a rate of zero because two owners of one day is a day paid twice.
  There is now **one owner**. Every quarter's daily make is measured onto its own row and the model
  integrates all of them off its own single clock; the settlement pass credits nothing, and the
  settlement's two work stamps are written **from** the model's tick rather than beside it. Come
  back to a four-zone city and all four have been working.
- **And it arrives in real vessels, a vessel at a time.** What the works made is a claim against
  real containers, not a number in a ledger: walk into the quarter and the cistern fills over the
  next turns, oldest dedication first, exactly as a deficit has always drained. Open it and it holds
  precisely what the book says. What the containers had no room for is **left in the field** and
  said so — the same loss a full larder has always taken — never banked and never silently forgiven.
- **The audit says the whole truth now.** `city: check-in audit` prints `model=`, `debt=` and
  `ground=` per stock kind, and the identity it asserts is `model − debt == ground`. `debt=` is what
  the quarter's works have made that nobody has poured yet; it is not drift, and it is re-derived
  from the ground on every check-in, so the equality holds by construction rather than by care.
- **A shortfall is met from the nearest quarter that actually has it**, on the city's own zone
  graph, and it is the same answer after a reload. The quarter that was claimed first no longer
  gets drained first merely for being first in the book. Inside a quarter, the oldest dedicated
  vessel still pays first — the two rules answer different questions and both stay true.
- **Three small loads bound for the same larder become one porter, not three.** A load whose
  destination a carrier is already walking to is put on that carrier's back instead of minting a
  second one. The whole planner — nearest-neighbour, then 2-opt, inside hard caps of sixteen jobs,
  eight stops and fifty swap tests — is arithmetic with **no draw anywhere in it**.
- **Your city's history became something you can trade.** What the roads are saying about your
  settlement is filed as a journal observation, tagged in the game's *own* interest vocabulary, so
  the factions that already cared about settlements and gossip will buy it from you at any water
  ritual — including from your own settlers. It can only be sold, never bought: you already know
  your own city's history.

### Added — the city asks, and another mod can teach it what to ask for
- **The book of the city** (Charter, `7`). Four waves of model — stocks, zone rows, works,
  residents, clocks, the told-log — and until now the only way to see any of it was a performance
  receipt. Six chapters: what the city holds and where, every work in the **city** and what each is
  waiting on, who lives here and where their day puts them, the turn of the year with the next feast
  and the heart's rung, what has happened here, and who else is writing in the book. The far zone's
  granary is a number you can read without walking to it, and a city that has dedicated no vessels
  says "nothing dedicated" rather than "0 of 0", because those are different sentences.
- **Where the keepers' craft could go** (Charter, `8`). The catalogue has always contained a
  dependency graph — six kinds of gate, refused in a fixed order, every refusal already carrying
  prose — and nobody has ever seen it. Now it is drawn: what each thing your keepers know **opened**,
  the nearest locked designs with what is in the way of each, and the ways of learning this city has
  never walked. **It is a map and never a spend.** Nothing on it can be pressed, there is no queue,
  no budget, no percentage and no timer; every acquisition still happens out in the world, at a ruin
  or a merchant or somebody else's fire.
- **What the city is asking for** (Charter, `9`). Petitions used to be decorative. Against a model
  they are the city talking: a dry cistern, more people than roofs, a mill worn past mending itself,
  a larder full to the lid while the next zone has room. Worst first, and **every line names what
  would settle it**. No new balance number was invented for any of them — each fires on a structural
  fact, because a reading surface with an economy of its own would start by disagreeing with the
  ledger.
- **Your own settlers will share water with you.** Qud's founding social act, turned inward. A
  citizen of your realm becomes a host of **vanilla's own water ritual** — nothing here builds a
  ritual — so sharing water with one of your people pays in your realm's own standing and teaches
  your settlement's own favoured dish. That recipe has been stamped on the realm's faction since the
  food lane and **no living creature in Qud belonged to that faction to hand it over**. The basin's
  fiction, finished. A settler another mod gave a conversation to keeps it, untouched.
- **And killing one of your own now costs you something.** The part that opens the ritual is the
  same part that makes a death matter: murder a citizen and your standing with your own realm
  falls, by the game's own arithmetic, bounded to the realm and the one to three factions it has
  feelings about. Share water with them first and then kill them, and it is the covenant violation
  — which the exile lane has had prose for since before there was anybody to violate it with.
- **The model is a published API, at version 1.** Another mod can now teach the city a new thing to
  ask for, and a new thing to happen in it, with no fork and no hard reference beyond the contract
  namespace — registered by the same cached attribute scan the game uses for its own extension
  points. Extensions live under the invariants ours do, **enforced rather than trusted**: kernel
  draws only, so an extension's chance is as replayable as the city's; frozen reading in and frozen
  result out; and every call across the executor seam, so an extension that throws or overruns its
  budget **stalls its own job and nothing else** — no city state published, the turn unaffected,
  the failure logged by mod name, and the asks board saying out loud that something stalled. Version drift is **refused out loud, by mod name**, naming the version it wanted and the
  version we publish, because a silently inactive extension is worse than a refused one: the player
  blames us. The founder can read the whole registry, admitted and refused, in the book's last
  chapter.
- **Only what the substrate honours is published.** Two contracts, not five: asks and happenings.
  Resource kinds, job and carrier kinds, network kinds and work behaviours are named in MODDING.md
  as *not published*, with the reason for each, and they open when the model underneath them does. A
  contract nothing honours is worse than none.

### Added — the city has a history: weddings, feasts, funerals, and the sound of the place
- **Come home to a city that things happened in.** Two settlers who have shared a roof long enough
  get married, and somebody who lives there says so. A work that wears out or loses its hands
  **stops**, and you are told once, from wherever you are standing — and when you come home and mend
  it, the city takes it back in the same breath it uses to withdraw a brink warning. Every one of
  these is read off rows the city already kept; none of them is a new system running beside the
  city, and none of them opens a message channel of its own.
- **The feasts are Qud's, not ours.** The city keeps **the Ides** — the one day of the month the
  game declines to number — and **the festival of Ut yara Ux**, the five-day intercalary month
  Joppa's own apple matz is remembered from. Those are the only two named days the engine has, and
  the mod invents no third. The feast serves the realm's own favoured dish, so a city eats what its
  creed already eats. A city founded in Tebet Ux does not owe a backlog of feasts it was not alive
  for, and a founder gone a decade costs exactly what one gone a season costs.
- **A death is told once, and now the telling has a funeral in it.** The line that already named how
  a settler was lost now names who spoke the water over them — the settlement's own office holder,
  by the epithet the city knows them by. It is the same line, not a second one: there is exactly one
  place in the mod that learns of a death, and that is the place this was composed into.
- **The office holder gets a name worth remembering.** Whoever has served longest is given an
  epithet and an honorific out of the game's own naming grammar, under the same scope Qud names
  village mayors with — and **none** of the hero machinery's combat statistics. Your water-keeper
  does not quietly acquire double hit points, four random mutations, and grudges with factions you
  have never met.
- **The place makes a noise.** Stand in your city and it breathes: the mill takes up its clatter at
  rising, bread-smell gets over the wall after the ovens have run, somebody sings the hour at the
  shrine badly and all the way through, the hearths take one after another at dusk. At most one line
  an in-game hour, never the same line twice in a day, and a wheel that has stopped outranks every
  one of them — because silence where there was noise is the only ambient line that is also news.
- **The city has an opinion about what you ARE.** Creeds read the founder's own body off the game's
  own tables: the mutation scores factions already keep (the Seekers really do mind a mass mind; the
  birds really do admire wings) and the interests a faction is defined for and against. A fault-line
  creed mutters, a chrome-revering one asks twice whether it may look at the implant, and both name
  the thing they mean. It is a line and never a mechanic — nothing here moves standing, refuses a
  settler, or changes what the settlement makes — and it is said once per thing about you that
  changes.
- **The homecoming report counts what you missed** in one line, out of the told-log ring and nowhere
  else, so it can never push the settlement's own arithmetic off the end of the report.

### Added — residents become rows, and one identity gets at most one body
- **A settler is a row now, and their body is a view of it.** Every settler carries a stable
  `KingdomResidentId` and nothing else; their name, origin, creed, home, standing and both brink
  windows live in a **resident row** in their city's book. Check-in reads the roster off the ground
  and writes it down. The id is minted off the realm's own counter, in order, never reused and
  **never drawn** — identity is a substrate, not a happening.
- **`Abroad` and `Dead` are real states with real words.** A settler you charmed, recruited or led
  out of the city reads **Abroad**: still on the roll, contributing no labour, and honestly
  reported as such. One who died reads **Dead**, with a cause — and the cause is
  `KingdomOfficeRules.DeathCause`'s own, so the funeral the city already tells stays the one
  telling. `Dead` is terminal. Placement and enforcement are the next wave's; nothing about
  population or labour arithmetic moved.
- **The binding registry ships whole.** `KingdomSystem.Bindings` answers *one identity, at most one
  body* for everything this mod mints — residents by `ResidentId` today, and the carriers the next
  wave mints by `JobId`, with the same rules and the same tests shipped now rather than retrofitted
  later. Check-before-mint is the only path to a body: a body already here is **moved**, a body live
  in another resident zone is moved across (a person) or refused (a porter), and a body whose zone
  is on disk **refuses the mint** — the debt stays owed. An unresolvable binding is never a licence
  to mint, which is what makes this hold across suspend, freeze, save, reload and crash.
- **A closed binding is evicted at once**, so absence from the registry *is* proof of closure. There
  is no second list to keep in step with the first, and an eviction that will not name its cause is
  refused.
- **The stale-transient verdict.** A body found in a thawed zone carrying a job id whose binding the
  model already closed is named stale — the one instant its load could exist twice, in the larder
  and in a frozen pack. The verdict ships now; the sweep that acts on it lands with the carriers.
- **The registry is realm state and no city carries it.** A bound body can be standing in your other
  city's ground or walked off the map entirely, so a registry a seat swap carried would answer for
  half the realm and lose the other half every time you crossed a zone line.
- Invariant I3 — *no binding key ever resolves to two living bodies, in any zone, at any time* — is
  asserted directly, beside the stock audit, on every check-in.

### Changed — a settler's brink moved off their property bag
- **Brink windows are resident-row fields.** `KingdomBrinkRoofTick`, `KingdomBrinkCreedToward` and
  their siblings are gone. A frozen object's properties are unreachable, so a window kept there
  could not run while a zone was on disk — which is the whole thing the living city is for. It is
  still a fact about one person, kept under their own id, and a seat swap still cannot carry one to
  the wrong city. **Every rule, window and announcement is unchanged**; what swapped is the storage
  under six accessors, and the brink suite's expectations did not move.
- The city book's schema is version 2: the resident standing gains a cause, each brink window gains
  its own standing flag, and both warned columns are retyped from a flag to the **tick** the window
  is anchored on — which a flag could not carry and every consumer of a brink reads.
- Serialization version 5. Pre-release, so there is no migration: a version-4 save's settlers carry
  brinks nothing in this build reads, and it is refused by name rather than loaded as a city whose
  warnings have quietly lost their deadlines.

### Changed — the constitution's own numbers
- The model-in-RAM **warn** rung rises from 48 KiB to 56 KiB; the 64 KiB **ceiling** does not
  move. The design's honest resting total at today's caps is 52.3 KiB, which sat above the old
  advisory rung — a warning a design is permanently inside tells a tester nothing. Warn is
  advice; the ceiling is the contract.
- The zone row widens from 80 to 96 bytes: two carries, because the sighting projection reads
  carries rather than levels, and a **signed debt per stock kind** rather than one net figure,
  because one net counter cannot say that a zone owes a food landing and a water draw at once.
- Serialization version 4. Pre-release, so there is no migration: a version-3 save has no book
  and is refused by name rather than read as a city that has lost half its ground.

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
