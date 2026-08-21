# The Thousand and First — Test Session Protocol (v0.1.0)

**Dev diagnostics are ON by default** in this build: every kingdom system writes `[TAF]`
lines to Player.log (toggleable in Options). If anything looks wrong, wish `kingdom:dump`
for the full state readout — it prints to screen and to the log for the log-watcher.

Fresh game launch (never mid-session approval — ghost assembly generations). Approve
**The Thousand and First** at the mod prompt, then load any save or start a new game.
Say when you're launching so the log watch can run alongside.

## Pass 1 — Foundation (wishes)

| Step | Wish / action | Expect |
|---|---|---|
| 1 | `kingdom:found Kavvat` | Founding popup; chronicle begins; standings seeded |
| 2 | `kingdom:status` | Stage Camp, pop 0, standings summary |
| 3 | `kingdom:rep Joppa:100` | Personal rep +100; spillover popup shows kingdom standing +50 |
| 4 | `kingdom:standing Snapjaws:600` | Their feeling toward the kingdom reads 100 |
| 5 | `kingdom:claim` | Zone claimed; chronicle line with the zone's prosaic name |
| 6 | `kingdom:citizen` (stand next to a creature) | It joins; message names it |
| 7 | `kingdom:selftest` | All checks PASS in one popup |

## Pass 2 — Growth, water, and thirst

| Step | Action | Expect |
|---|---|---|
| 8 | Drop 1–2 filled waterskins on the ground in the claimed zone | `kingdom:status` shows **0 stored** — undedicated vessels are personal and inviolate (the protection law) |
| 8a | Charter → **Dedicate a vessel to the stores** beside the waterskins | Vessels flip to [dedicated]; `kingdom:status` now counts their drams |
| 8b | Release one via the same menu | Its drams vanish from the stores; settlers will never drink it |
| 9 | `kingdom:grow` | Settler arrives (with an origin), water −2 drams, chronicle line |
| 10 | Repeat 9 to 5 settlers with 16+ drams stored | **Steading** stage-up popup + journal accomplishment |
| 11 | Empty the stores (pick containers up), `kingdom:grow` | Thirst warning (red), chronicle "thirsted" line, streak 1 |
| 12 | `kingdom:grow` again, still dry | A settler **emigrates** ("left for wetter country"), pop −1 |
| 13 | Refill water, `kingdom:grow` | Streak resets; arrivals resume |
| 14 | If the zone has a fresh-water pool: set a water detail (Charter → **The water detail**), then `kingdom:status` before/after `kingdom:grow` | The detail fetches pool water into dedicated containers (stored rises, open falls). With nobody on the detail, nothing is fetched — see 21w |
| 14a | Put an empty dedicated vessel beside a salty pool, then `kingdom:grow` | No brine is fetched or converted; both volumes remain unchanged, and `kingdom:selftest` reports the mixed-liquid checks PASS |
| 14b | At **Camp**, Charter → Commission and read the whole list | **Nothing in it makes water.** No salt-pan, no dew catchment: every water producer opens at Steading or above (Addendum 11(a)). A camp drinks the stock you arrived with, what the detail hauls out of the site's own pools, and what a charter pays |
| 14c | Run a camp with an empty detail and no charter until the casks are dry | It climbs the thirst ladder and stops at the loyal core. That is the intended pressure: the camp **costs** water, and the answer is hands on the detail or a rung up the ladder, never a building |
| 14d | Reach Steading (5 settlers, 16 drams of dedicated capacity), then Charter → Commission | The water lane opens all at once: salt-pan, salt-pan terrace, dew catchment, catchment bank, weep-tap. The first producer is a thing you **earned**, not a thing you started with |

## Pass 3 — The rite (no wishes)

| Step | Action | Expect |
|---|---|---|
| 15 | (On a save with no kingdom) wish `r_FounderBasin`, fill it with 8+ drams of water | Inventory action **found a settlement** appears |
| 15a | Fill the founder's basin with 8+ drams of brine instead | Founding is refused as “not pure water”; no liquid is spent and no settlement is created |
| 16 | Use it, name the settlement | Ceremony popup; founded + zone claimed; basin drained by 8 |

## Pass 3b — Charter, chronicle registers, and reset

| Step | Action | Expect |
|---|---|---|
| 16a | After founding: check the abilities menu | A **Charter** ability exists (Kingdom class); activating it opens Status / The Chronicle / As others tell it / Standings |
| 16b | Read "As others tell it" | Same events retold with rumor leads ("Travelers claim that...", "Some deny that...") |
| 16c | Talk to an arrived settler | Greeting + "Why did you come?" answer naming their origin |
| 16d | Walk one zone away (non-adjacent claim test: travel 2+ zones), `kingdom:claim` | Refused — claims must border existing ground (`kingdom:claimforce` overrides) |
| 16e | Let the settlement stay dry for 3+ growth passes at Steading, then refill water and grow | **Withered** flag in status + a chronicle line while dry; on refill, a recovery message and its own chronicle entry |
| 16f | `kingdom:reset` (confirm) | Kingdom dissolved; Charter ability gone; ready to re-test founding from scratch |

## Pass 3c — City style and the ground it was read from

| Step | Action | Expect |
|---|---|---|
| 16f | Found on an ordinary overworld site (rite or wish) | The founding popup and the chronicle line name the ground — "founded on common ground", or a style clause if the site earned one. Founding never throws, whatever the terrain |
| 16g | `kingdom:dump` | `Style:` shows the style and its clause; `Founding terrain:` shows the blueprint, region, and z the style was read from — the evidence, not just the conclusion |
| 16h | `kingdom:style` with no argument | Reports the current style, the recorded founding terrain, and every known style |
| 16i | `kingdom:style verdant`, then Charter → Commission | The design list changes to that style's catalogue. The recorded founding terrain is unchanged — forcing a style is a probe, not a rewrite of history |
| 16j | `kingdom:style nonsense` | Refused, and the known styles are listed. Nothing changes |

## Pass 3d — The second city and the seat

The realm is the faction; the cities are where its history happened. One city is *seated* at a
time — the one you are standing in — and the other keeps itself until you walk back into it.

| Step | Action | Expect |
|---|---|---|
| 16k | With a kingdom founded, refill the basin and use it in the founded zone | Refused: this ground is already the realm's. No liquid spent |
| 16l | Walk to a zone bordering the claim and use the basin | Refused: bordering ground is claimed, not founded — that would be expanding a city, not founding one. No liquid spent |
| 16m | Travel three or more zones away to unclaimed ground, use the basin, name the city, choose a vocation | A ceremony naming the city, the ground, and what it was founded for; the basin drained by 8; one founding line in each chronicle register naming the vocation |
| 16n | `kingdom:dump` | `Seated:` is the new city with its vocation; `Away:` is the first city with its own stage, population, claims and ticks; `seat mismatches: none` |
| 16o | Walk back into the first city's ground | The seat swaps on arrival. Charter Status, the roll of settlers, petitions and the ledger describe the first city again — not the second's |
| 16p | Stay away from the second city a season or more, then walk into it | It caught up on arrival from its own clock, in full: **every** day you were away is billed against its own stores, not three. Arrivals are still at most three in the one pass — that is a pacing rule about the gate, not forgiveness about the calendar. Nothing is doubled and nothing is written off |
| 16q | Charter → Status, in either city | The title names the city you are standing in, not the realm, and the report says the realm also holds the other city, which keeps itself until you stand in it |
| 16r | Charter → The Chronicle, in either city | One history for the realm. Both cities' events are in it — the chronicle belongs to the faction, not to the ground |
| 16s | `kingdom:found2 NAME:refuge`, then `kingdom:seat swap` | A tester reaches either city without the walk |
| 16t | Use the basin a third time on new unclaimed ground | Refused plainly: the realm holds two cities. No liquid spent |
| 16u | Save → quit → reload → `kingdom:seat` | Both cities intact, same seat, `seat mismatches: none`. The dormant city keeps its roster, ledger, claims and districts |
| 16v | `kingdom:selftest` | "seat carries all N settlement fields", "the two cities claim no ground in common", and "the realm holds no more than 2 cities" all pass |
| 16w | `kingdom:reset` (confirm) | Both cities dissolved; the Charter is gone; `kingdom:dump` reports unfounded |

## Pass 3e — Founding by ruin, asking a village, and claiming downward

| Step | Action | Expect |
|---|---|---|
| 16x | Pour the rite on overland ground whose terrain is a vanilla ruin (`TerrainRuins` / `TerrainBaroqueRuins`) | The popup and chronicle say the place was **reclaimed**, not founded, and any bed or shrine already standing there is credited to the settlement |
| 16y | Pour on ordinary ground | Unchanged "founded" wording. The ruin path never leaks into an ordinary founding |
| 16z | Walk into a real, populated vanilla village and use the basin | It **asks** — "stand with us?" — instead of founding or claiming. Whatever you answer, the village keeps its own faction, its own people, and its own zone. Nothing is annexed |
| 16aa | Try the same village while your reputation with it is below liked | Refused plainly, and **no water is spent**. The covenant is earned, not bought |
| 16ab | Raise reputation to liked or better and accept | Water is drained only on yes; the covenant is chronicled and the realm's standing with the village rises |
| 16ac | Charter the same village twice | Refused: the covenant is already sealed. No water spent |
| 16ad | Stand on a hostile lair or any other faction's ground and pour | Refused. Ground that answers to someone else is never quietly taken — this is the hazard the ecosystem audit flagged, and it is now closed |
| 16ae | From claimed ground, go one stratum directly down (a cellar) or up and claim it | Allowed. A settlement can hold the ground under and over itself |
| 16af | Try to claim a zone two tiles away, neither bordering nor directly above or below | Still refused. Vertical adjacency did not loosen horizontal adjacency |

## Pass 5 — Districts and commissions

| Step | Action | Expect |
|---|---|---|
| 20 | Charter → **Designate district** in a claimed zone → market | Chronicle line; status unchanged otherwise |
| 21 | `kingdom:status` | Market district shortens arrival intervals by 10% — verify the next arrival tick moved after the next growth pass |
| 21w | Found beside open water and walk away without touching the Charter | **Nothing is fetched.** The settlement drinks only what you poured in. Status says nobody is carrying water and points at the Charter |
| 21x | Charter → **Set the water detail** → 2 settlers | Two settlers walk to the water; status shows "2 of N carrying". The works now have N−2 to draw on, and a work that needed those hands reports itself shorthanded |
| 21y | Put every settler on the water | Every work goes idle. Hands are spent once — this is the trade, made visible |
| 21z | Stand the detail down | Buckets hung up, chronicled. The settlement is back to drinking what you bring it, and it does not die — it simply stops growing |
| 21a | Designate a second claimed zone **garrison**; `kingdom:status` | Defence is +2 above the crewed works alone. A garrison trains the whole watch, so it counts from any claimed zone, not just the one you are standing on |
| 21b | Designate **agrarian**; watch a full upkeep interval | Upkeep is billed at 90% of the population figure. Status shows the number actually charged |
| 21c | Designate **market**, then reach the next stage | Shops carry one tier above the stage's own tier |
| 21d | Designate **craft**, then commission anything | The scaffold completes in 80% of the design's build ticks |
| 21e | Designate **shrine**, then wait for petitions | Petitions come at 75% of the usual interval |
| 21f | Designate **academy**, then Charter → As others tell it | The outsider register embellishes less often than the true chronicle — most tellings now end plainly |
| 21g | Designate two zones the same district | The percent effects do **not** stack: a second vinelands feeds the same city, not twice. Only garrison defence is additive |
| 22 | Charter → **Commission a building** → cask rack (4 drams in stores needed) | Stores −4; scaffolding appears nearby; chronicle line |
| 23 | Wait ~1200 ticks (or explore and return) | Scaffold becomes the cask rack; completion message + chronicle |
| 24 | Commission the great cistern (16 drams) | Same cycle; stored capacity +256 when done |
| 25 | Commission a communal bunk; watch settlers at night | A settler eventually sleeps in it (vanilla bed behavior) |
| 25a | On first reaching Steading | "A settler has taken up the trade" — the first stall opens; trade with them shows tier-1 stock |
| 25c | Charter → Commission | The design list is loaded from KingdomBuildings.xml (13 entries); third-party mods shipping their own `<kingdombuildings>` file extend it automatically |
| 25b | Browse vanilla tier-1 merchants elsewhere | The founder's basin occasionally appears for sale (8% per restock) — the legitimate acquisition path |

## Pass 6 — Raids and tribute

| Step | Action | Expect |
|---|---|---|
| 26 | Reach Steading; `kingdom:standing Snapjaws:-300`; leave and re-enter the claimed zone | Warning: snapjaw scouts seen; chronicle line (needs raid cooldown elapsed — or use `kingdom:raid` to force) |
| 27 | Charter → **Pay tribute** (12 drams in stores) | Raid averted; snapjaw standing +50; chronicle line |
| 28 | Force again via `kingdom:raid`, don't pay, wait out the lead (or `kingdom:raid` again) | 2+ snapjaws spawn at the zone edge and attack citizens; chronicle records the raid |
| 29 | After the fight: `kingdom:status`, `kingdom:chronicle` | State coherent; raid recorded in both registers |
| 29g | `kingdom:standing Baboons:-300` (also try Goatfolk, Cannibals, Issachari) | Any of the five provokable factions can raid you — the warning, tribute, parley and chronicle lines all name the faction that is actually angry, not Snapjaws |
| 29h | Provoke two factions at once | The angriest one comes. The other stays provoked and waits its turn |

## Pass 6b — Fortification

Defence is a perimeter, not a damage number: it decides how much of the band gets past the wall
at all, and how much the ones who do carry off. Raids resolve where you can see them, so every
step here is done standing in the settlement.

| Step | Action | Expect |
|---|---|---|
| 29a | With no defences built, `kingdom:raid`, wait out the warning lead without leaving | The whole band spawns at the zone edge; drams are carried off |
| 29b | Charter → Commission → **thorn palisade**; wait out the build; `kingdom:status` | Status shows the settlement's defence at 3, and the palisade stands on an edge of the zone that **faces ground you do not hold** — not in the middle of your own settlement |
| 29b1 | Raise several walls, then claim the zone that edge faces | The old line is not torn down or moved. It is an inner wall now, and the frontier has moved outward — new walls go on the new outer edge |
| 29b2 | Claim on every side until a zone is fully surrounded by your own ground, then commission a wall there | It has no frontier left, so the wall is raised where you stand instead. A zone in the middle of a city has no outward edge to defend |
| 29c | `kingdom:raid` again, wait it out | The message says the watch turns back some of them at the wall; fewer raiders spawn than in 29a, and fewer drams are lost |
| 29d | Commission a **watchtower** with nobody spare to man it; `kingdom:status` | Defence unchanged: an unmanned tower is a platform |
| 29e | Grow the population until the watch is crewed; `kingdom:status` | Defence rises as the crew fills; a half crew gives half the tower's defence |
| 29f | Reach defence 12+ (palisade + crewed watchtower + rampart at Village); `kingdom:raid` | "They break on the walls. The watch holds." Nothing spawns, nothing is taken, the chronicle records it as an accomplishment, and the walls appear under deeds in `kingdom:status` |
| 29i | At high defence, raid repeatedly | Even a very strong wall never turns back more than 60% of a band that is not fully repelled — someone always climbs over. Being well-walled is not being spared |
| 29j | Provoke a raid, then leave for several days and return | **Nothing was resolved while you were gone.** The chronicle says raiders came and found no one to answer them; nothing was taken, nobody was lost, and the threat is still live with a fresh window to pay, parley, or fight |

## Pass 8 — Homes, work, and the first service

| Step | Action | Expect |
|---|---|---|
| 34 | Found and claim, then `kingdom:grow` before commissioning any bunk | **No settler arrives** — "finds no bed. Commission housing and they will stay." Announced once, not repeated |
| 35 | Charter → Commission → communal bunk, wait for it to finish, then `kingdom:grow` | A settler arrives; population 1. Beds are the population ceiling |
| 36 | Commission bunks until you have several, grow to 5+ settlers and 16+ dedicated capacity | Steading; "a settler has taken up the trade" |
| 37 | Trade with the settlement's trader, note the stock; grow to Village | "The traders have better wares to show you" — stock tier rises with the settlement (1 → 2 → 3 → 5 → 7) |
| 38 | Commission the **charging post** (12 drams, Steading+, needs 2 crew) with only 1 settler | Message: works "stand idle for want of hands" |
| 39 | Grow past 2 settlers, revisit | The post is crewed; set a depleted energy cell on it and wait — it charges |
| 40 | `kingdom:dump` | Heartbeat tick, idle works count, shop tier, and bed count all reported |

## Pass 9 — Coming home

| Step | Action | Expect |
|---|---|---|
| 41 | Found, claim, dedicate water, commission a bunk. Leave the zone, travel a day or more, return | **One nonmodal message line**, not a popup: the settlement "has news of the N days you were away", pointing at the Charter. Nothing interrupts the walk home |
| 41b | Charter → What happened while you were away | The report opens on request: the events as past-tense lines, and a ledger of drams drawn, delivered, drunk and lost — not a scroll of separate messages |
| 42 | Read an arrival line | It names the cause: "Word of the cask rack raised at Kavvat reached the hills — a settler has come." Founding, stage-ups, commissions, caravans and tribute all set the cause |
| 43 | Strike a charter, then stay away for several caravan intervals | Missed deliveries **bank**: "3 caravans of the villagers of Joppa came under charter: 18 drams." A caravan that came while the gate was shut is news, not a loss — and the banking cap is a cap on how much one homecoming can hand over at once, never a clock that forgives the rest |
| 44 | Return after a short walk (under a day) | No news line — the homecoming is for absences, not for stepping outside. The Charter entry still opens and says nothing has happened |
| 45 | Charter → Dedicate a vessel or larder → **Dedicate everything here** | All undedicated vessels join the stores in one action, up to the cap |
| 45a | Stand beside a chest or footlocker with food in it and dedicate it as a larder | It is marked a larder of the settlement. Nothing moves and nothing is taken — dedication is a mark, not a transfer |
| 45b | `kingdom:dump` | The pantry reads the food in dedicated larders only, as a count and a tier (Empty / Scant / Modest / Ample). Food in your own undedicated pack is never counted |
| 45c | Release the larder | The count drops back. What was inside is untouched |
| 45d | Commission a **civic larder** (Charter → Commission), then put food in it | It counts as a larder without needing a chest of your own, and holds 64 servings |
| 45e | With the larder Scant or better, Charter → **Share a meal from the larder** | It **asks first**, naming what the meal will take and what the larders hold. Answer yes and the settlement eats: food is spent from the dedicated larders only, a settler from the roll speaks, and the chronicle records it. Word travels — the meal becomes the settlement's deed, so it draws settlers the way any deed does |
| 45f | With an empty larder, Charter → Share a meal | Refused plainly, and nothing is lost. The meal itself is still a bonus for engaging and never a penalty for abstaining |
| 45g | Check your own pack and any undedicated container after a meal | Untouched. Only dedicated food is ever spent |
| 45h | Found a camp, dedicate nothing, commission nothing, and let days pass | It **does not starve**. Free hands forage up to four rations a day off the ground and eat them hand to mouth, which is exactly a camp of four — the food mirror of "half a camp on the water detail covers a camp's drinking". Put a third hand on the water detail and it still forages four: the wild has a ceiling, not a pool |
| 45i | Grow past a camp with no field standing | Foraging stays at four a day however many people there are, so the shortfall is real and the ledger says so: "The larders are empty. Settlers will leave if the fields do not feed them." Commission a kitchen garden or a field and it stops |
| 45j | Commission a **granary**, then `kingdom:status` | It dedicates itself — the same law that auto-flags a commissioned cask rack. `Larder:` reads its capacity (288) in the denominator, and the `Fields:` line names what the works make against what the people eat |
| 45k | Let the fields make more than the larders can hold | Said once, by name, and never again until there is room: "The larders of X are full, and N of the harvest was left in the field." The homecoming ledger carries the same figure |
| 45l | Let a settlement be dry **and** hungry across the same homecoming | **One** settler leaves, not two, and the line names both reasons: "left X for water and bread both, and this place had neither." Both `(withered)` and `(famished)` may show on the Status stage line — a mark is a state, a departure is a cost, and only the cost is capped |
| 45m | Damage a granary (a raid, or a lost rung) and leave it unmended | It spoils what it holds on world days, exactly as a holed cistern leaks drams: announced once by name, unsaid when it is mended. It can never be the reason the settlement goes hungry — spoilage is drawn after the day's eating, never before |
| 46 | Charter → Status | The ledger's effects are visible: stores, shop tier, idle/shorthanded works, and the next-need line |

## Pass 10 — Names, policy, and answering a threat

| Step | Action | Expect |
|---|---|---|
| 47 | Let settlers arrive, then Charter → **The roll of settlers** | Real generated names, each with origin and the date they came. Look at a settler in the world — it carries that name |
| 48 | Charter → **Standing policy** → toggle gates to guarded | Chronicle line; arrivals slow (~40%) and raids come less often. Toggle stores to thrift: upkeep drops a quarter, arrivals slow further |
| 49 | Provoke a raid (`kingdom:standing Snapjaws:-300`, `kingdom:raid`), then Charter → **Answer a threat** | Three exits offered: pay (with the current demand), send word (only if their standing ≥250 and you haven't stalled), or let them come |
| 50 | Choose "let them come", then face the next warning | The demand has grown by half. Stall again and it grows again, up to four times |
| 51 | Raise Snapjaw standing to 250+, force a fresh warning, choose **send word** | Raid averted with no water paid, chronicled as an accomplishment — goodwill spent instead of drams |
| 52 | Force a warning, then leave the area past the due tick and return | **No ambush at the gate, and no loss in the dark.** The raiders came, found no one to answer them, and waited: nothing taken, nobody lost, the threat unchanged. The homecoming re-stamps the deadline by the same lead the first warning used, so you get a fresh window to pay, parley, or stand there. The chronicle records that they came |

## Pass 7 — Trade charters and caravans

| Step | Action | Expect |
|---|---|---|
| 30 | `kingdom:standing Joppa:300`, then Charter → **Strike a trade charter** → water charter → Joppa | Charter struck; chronicle + journal accomplishment |
| 31 | Leave the claimed zone, wait ~3600 ticks (or explore), return | A dromad caravan stands at the zone edge; up to 6 drams delivered into **dedicated** stores (needs storage space — overflow is announced and wasted); Joppa standing +2 |
| 32 | Trade with the caravan dromad | It's a real merchant with real stock |
| 33 | `kingdom:dump` | Deal listed with its next tick; caravans-here count matches |

## Pass 12 — The water manifest

The one thing a realm of two cities can do that a realm of one cannot.

| Step | Action | Expect |
|---|---|---|
| 48 | With only one city, Charter → **Send a water manifest** | Refused: there is nowhere to send it |
| 48a | With two cities, standing in one with water to spare, send a manifest | The load is sized to what the **other city had room for when you last stood there** — never more. It **asks first**, naming the drams and the window. Answer no and nothing moves. Answer yes and a capped amount, only ever above a three-day reserve, leaves **this city's** stores now |
| 48b | Immediately try to send another | Refused, naming the one already on the road: origin, destination, drams, days left |
| 48c | Walk to the destination city | The water arrives on entry, into that city's stores, with a chronicle line and a ledger note. It arrives when you get there, not on a background clock |
| 48d | Send one and let the window lapse | **The carters turn back**, once. The load starts home and arrives at the origin the next time you stand there. Being elsewhere never costs you the water |
| 48e | Try to send when the other city had **no room** when you last stood in it | Refused, naming the city: water sent now would arrive with nowhere to go. Trade stops until you raise storage there or go and look for yourself |
| 48e1 | Send a load, then fill the destination's casks before the water arrives | **This is the rare case**: the carters arrive expecting room and find none. What fits goes in the casks, the rest is set down as a pool on the ground, the chronicle records it, and nobody is pleased. Overflow happens because the water level changed under a run already on the road — not as routine spillage |
| 48e2 | Ignore a turned-back load until its second window closes too | It is set down where you are standing, as a pool. A load is never carried forever and never evaporates — check the stores and the ground: the water is all still accounted for |
| 48e3 | Count the realm's water before and after a full round trip to a full cistern | Conserved. Storage still decides how much a settlement can **keep**; the cart is not a cask |
| 48f | Try to send a manifest standing outside either city | Refused: manifests are loaded on the kingdom's own ground |

## Pass 13 — Seeds, rows, and the harvest cycle

Nothing in the food lane grows until you put seed in the ground. Every rung of it — the kitchen
garden, the garden rows, the field, the ploughed fields, the grange, the home farm — carries the
same part, takes the same seed, and stands real plants you can walk into.

| Step | Action | Expect |
|---|---|---|
| 49 | Charter → Commission → **kitchen garden**; wait out the build, then read `kingdom:status` | A plot appears and its `food` contribution is **zero**. Bare ground carries nothing. The ledger says so once, by name: "is bare ground: nothing has been sown in it" |
| 49a | Get a seed — buy one off a trader, or walk up to a **watervine / starapple tree / godshroom / dreadroot** and **gather seed** | One seed, once per plant. A plant somebody owns refuses; a plant already stripped says so. Which seed a wild plant carries is its own species — you cannot get mushroom spawn off a vine |
| 49b | Stand in the garden and **Sow** the seed | You are shown the crop, the rows, the wait and the water before anything is spent. Confirm: the seed is gone, drams are drawn once, and **real plants appear across the plot**. Both registers date the sowing |
| 49b1 | Try to sow with the stores nearly dry | Refused by name, and nothing is spent. **A sowing can never be the reason the thirst ladder fires** — it may only spend what three days of upkeep does not need |
| 49b2 | Try to sow a field that is already sown, or ground the realm has not claimed, or a field worn past working | Each refuses with its own sentence. Nothing half-happens |
| 49c | Read `kingdom:status` again | The garden now carries its `food` figure. Sowing is the gate, and it is the same gate for the level and for the day |
| 49d | **Stand there and wait** six days | The rows go ripe — you can see the colour change on the plants themselves. A founder who never leaves must still see their field work |
| 49d1 | Gather a ripe row **by hand** before the settlement does (needs Harvestry — vanilla's own `AttemptHarvest` gate, not ours) | You get a real crop item in your pack, and that row is not in the settlement's harvest. You have a full day between ripening and the settlement's own hands arriving; what you take is genuinely taken |
| 49e | Wait out that day | The settlement gathers. The larder fills with the actual crop, and the ledger accounts for every serving: what went in, what is on the road, what was left in the field |
| 49f | Leave for a season and come back | Every cycle that came due resolves at once, dated — and the chronicle carries **one line with a count**, not one line per harvest. A farm does not get to eat the register |
| 49g | Sow a field in one zone and dedicate the only larder in **another** zone of the same city | The harvest credits the city at once, and the crop **materialises in that larder the next time you walk into its zone**. Nothing is touched in a zone nobody is standing in |
| 49h | Sow with no larder dedicated anywhere | It grows, and the harvest is lost for want of room — said once, by name. It will not put food anywhere you did not dedicate |
| 49i | Let the larders fill and keep farming | The overflow is lost, not queued, and the line says how much. A granary is what makes a good year last into a bad one |
| 49j | Farm for several cycles and watch the larder | Seed comes back out of the harvest sometimes. Save, reload, and resolve the same cycle again: **the same answer**. The draw is keyed to the field and that cycle, never re-rolled |
| 49k | **Withdraw Seed** from a sown field | The rows come up, the seed is handed back, and the field carries no food again. The seed was yours the whole time |
| 49l | Take the hands off a field that wants them (raise something else that eats the crew) | It stops gathering and says it wants hands — and the crop is still standing when you put somebody back on it. **Idleness costs a harvest's delay, never the harvest** |
| 49m | Raise a **field**, **grange** or **home farm** and count the plants it lays | 16, 52, 80 — the rows a design stands are its `Carries="food:N"` doubled, and `_notes/balance-sim.py` §G2 proves the two agree for every design in the catalogue |
| 49n | Drag a powered **Hydraulic Irrigator** next to a sown field and leave it running | The crop comes ripe in about **half** the time. Vanilla's own machine, on its own radius, off its own charge — it does nothing to any plant the game ships (none of them arms `RegenTime`), and it does something real to ours |
| 49o | Switch the irrigator off, or let it run out of charge | The cycle goes back to its own six days. Nothing is lost and nothing is owed; a machine shortens a wait and never conjures a harvest |

## Pass 14 — Exile, and being taken back

The realm's regard for its founder falls only from deeds, never from time. Absence never expelled
anyone.

| Step | Action | Expect |
|---|---|---|
| 50 | Found, claim, grow; `kingdom:status` | Opens with a regard line: the realm holds you **beloved** |
| 50a | `kingdom:regard 0` | One line, nonmodal, and one entry in each register. Regard reads **doubted** |
| 50b | `kingdom:regard -100` | **Silence.** That rung has already spoken |
| 50c | `kingdom:regard -300` | The charter is read aloud, and they stop a while at your name. Regard reads **resented** |
| 50d | `kingdom:regard -100` then `-300` again | Silence both times — jitter across one threshold says nothing. This is the hysteresis |
| 50e | `kingdom:regard 400`, then `0` | It speaks again. Only climbing back re-arms the ladder |
| 50f | `kingdom:regard -700` | The realm puts you out, naming the deed. **The Charter is gone from your abilities** |
| 50g | `kingdom:dump` | Founded: no — but the exiled realm is all there: its city, population, claims, clocks and standings intact. Nothing physical was touched |
| 50h | Walk onto its ground while still repudiated | Once, nonmodal: it will not hear it. Walk out and back in — **nothing is said a second time** |
| 50i | `kingdom:regard 0`, then walk out and back in | Now it puts the question to you in person. Answer **no** |
| 50j | Walk out and back in without changing anything | The question is **not** asked again. If it is, the no-nag rule is broken |
| 50k | `kingdom:regard 100`, walk back in, answer **yes** | The Charter returns and the realm is restored exactly as it was — population, claims, policy, standings |
| 50l | Charter → The Chronicle, then → As others tell it | Both days are in both books and **they do not agree**. Your book says the realm put you out; the roads say the tyrant ran |
| 50m | Get exiled again, then found a new realm somewhere far off | A new realm, a new faction. Walk back onto the old realm's ground: it says once that it has nothing to say to a founder who has somewhere to go back to. The door is shut, and says so |
| 50n | With the new realm founded, try the basin and `kingdom:claim` on the old realm's ground | Both refuse. The city you were put out of goes on without you — it is not yours to pour on or claim |
| 50o | `kingdom:found <the old realm's name>` | Refused by name, and **no water spent**. A faction name, once used, is used forever |
| 50p | Reputation screen (`r`) | Exactly one extra faction per realm founded. Never one per city |
| 50q | Save, reload, then fall a rung | Regard, tier and the last-spoken rung all survive. It speaks once, not again on the next load |
| 50r | Get exiled, then leave the game alone for many in-game days | Nothing has happened. Absence never lowered regard and never closed the door |

## Pass 15 — How a city lays itself out

| Step | Action | Expect |
|---|---|---|
| 51 | On empty ground, commission anything | It rises beside you, plainly. The plan has no opinion yet — your first building is the seed everything later is read against |
| 51a | Walk well away from your dedicated vessels and commission a cask rack | It rises **by the water**, not where you stand. The message names the ground it chose |
| 51b | Commission a bunk while standing on the wall line | The plan overrules you: people do not sleep on the wall |
| 51c | Commission a bunk a few cells from other bunks | You win. Within about four cells of the plan's best, your spot is the spot |
| 51d | Commission several walls along one edge | Each closes a gap in the line before extending an end. A wall grows into a wall |
| 52 | Charter → stake a plan on a cell, then walk away | Nothing was spent. Come back and it has been built, if the stores could afford it |
| 52a | Stake a plan you cannot afford, and leave it | It waits. Forever, without nagging, without expiring, and without taking water it cannot completely spend |
| 52b | Cancel a staked plan | Free, because nothing was ever spent |
| 53 | Build a room yourself — four walls and a door — and Charter → adopt it | Accepted if it qualifies. Nothing is moved, nothing is transferred: it is marked, exactly like dedicating a vessel |
| 53a | Try to adopt a space that does not qualify | Refused, and it says **what is missing** — a bed, a door, a container. The structure is untouched |
| 53b | Release an adoption | It stops serving. Nothing is lost |

## Pass 16 — What your cities believe

| Step | Action | Expect |
|---|---|---|
| 54 | Hold one city only | You never encounter any of this. Not a message, not a menu entry that does anything |
| 54a | Hold two cities and let settlers arrive | Most believe nothing in particular. Once a third of a city shares a creed, the Charter names what that city is |
| 54b | Settle a zealous creed in one city and its opposite in the other | Dissent begins — and only on days you were **there**. Four warnings arrive well before anything is lost |
| 54c | Leave for a season with dissent running | It has not moved. A realm cannot fall apart because nobody was playing |
| 54d | Pour a rite of shared water; call a shared meal | Dissent gives ground, slowly. These are levers, not decoration |
| 54e | Charter → declare the realm's creed | **It asks first**, naming what it will cost: every later settler leans that way, and those who stand against them hold it against the whole realm, everywhere |
| 54f | Let dissent run to the end | The unhappier city leaves — keeping its ground, its people, its buildings, its stores and its book. Nothing burns. Both registers record the day and **disagree about it** |
| 54g | Walk onto the ground of a city that left, having removed the cause | You may ask them back. Try it without removing the cause and they say so to your face |

## Pass 17 — Plots, materials, and the catalogue

| Step | Action | Expect |
|---|---|---|
| 55 | Commission a plot-sized design (a tent, a timber hut) | The settlement stakes a **rectangle**, not a cell: staked → cleared → framed → walled → done, watchable, door cut toward the heart. The surveyor's plan on the stakes reads as the finished building's description, framed as intention |
| 55a | Watch the clearing stage | Clearing **earns**: trees give timber, rock gives stone, ruin walls give scrap — carried to a stockpile you dedicated. No stockpile, and it says so once |
| 55b | Try to stake a plot over your own dropped gear, or open water | Refused, naming the cell and the thing standing on it. Nothing player-placed is ever cleared |
| 55c | Charter → **Clear ground** (`q`) | Spare hands work the rect down over days; the yield is itemised in the ledger |
| 55d | Charter → **Take down a building** (`z`) on something the settlement built | Condemned: crew works it off, half its material returns to the stockpiles, the plot frees, no water refunds, chronicled both ways |
| 55e | Check building costs in the commission list | Water **and** materials. A design naming no materials costs water alone |
| 55f | Reach Steading, then Town | M then L plots unlock — the city literally builds bigger as it grows. XL waits for City |
| 55g | Claim one stratum down and commission there | The plot is **carved**: double clearing, paid back in stone, no walls — the rock is the wall. A weather-dependent design refuses underground by name |
| 55h | Raise a building while standing there — a **house or a field**, not only a wall | The raising ceremony: crew gathers, water is shared, the chronicle names those present. Raised while away, the homecoming tells it plainly. Every design closes this way, whether it rose on a scaffold or as a staged plot |
| 55h2 | Stake a **plan** for a plot-sized design and let the settlement realise it | When it rises, the chronicle's raising line **quotes the plan** staked for it. A design commissioned directly has no plan and is chronicled without one |
| 55i | Order a house | It comes **furnished** — bunks, torchpost, hookah arrive as contents. Nobody commissions a hookah one at a time |
| 55j | Check a chartered caravan after several visits | Occasionally it carries a **pattern-book**: one foreign design chosen from three, merged into what the keepers know. The base catalogue is never gated on the draw |
| 55k | Appoint an office holder; read their description | One virtue, one flaw, and one or two stated **tastes**. Met tastes shade the settlement up; unmet means their default, never a penalty |
| 55k2 | Charter → Status after an office is filled | The level's own line carries **"+N for what its notable finds here"**, and the level is that much higher. Never negative, and never past half the binding level |

## Pass 18 — The posted price, worn ground, yard trades, and guests

| Step | Action | Expect |
|---|---|---|
| 56 | Charter → posted notices (`1`) → post a price to clear a rect | A notice stands at the heart. Nothing is escrowed. Someone takes it on their own judgment on a later pass — attempts, successes and refusals are chronicled **by name**, refusals citing the refuser's own flaw |
| 56a | Post a price the stores cannot cover | It stands, with the debt written down and named. Paid the day the work finishes, not before. No expiry, no nag; take it down free |
| 57 | Walk your settlement for several visits, then leave it to its people for a season | Routes people actually walk wear in: grass → trodden earth → true path. A season of settlers walking lays a season's worth — the ground does not wait for you. Ground **nobody walks on** stays grass however long you are gone, because what wears a path is feet and not the calendar |
| 57a | Charter → ground work (`q`) → pave a worn path | Asked first, priced per cell, paid in stone from the stockpiles. Refused by name with no stone, no worn ground, or nobody free |
| 57b | Check cells under your dropped gear | Never worn, never paved. Wearing only touches open ground |
| 58 | Charter → your works (`y`) → give a small house a yard trade | Vine lattice, hide rack, dye vat, or vellum press. The house's description and the roll of settlers say the household took up the trade. Letting it go is free |
| 59 | Return within a notable's 2-day patience of their arrival, and read the roll | **Guests**: the notable is still standing at the gate, logged with one hook — a ruin, a machine, a debt in a named village. Lodge them in a bed of the right tier and they settle with a trade; ignore them and they leave a letter and the hook becomes a rumor — never lost |
| 59a | Leave long enough that a notable's 2 days of patience (a third of a day, for an ordinary traveller) has run out before you return | Nobody is standing at the gate — the roll instead reads one dated ledger note, "N notables came to the gate while you were away and found no bed offered", naming how many and how long ago the last of them stood there. Their hooks are rumor now, same as an ignored one, never lost |
| 60 | Buy a carry-sign from a merchant; plant it on a pile you own out in the world | Confirms exactly what it will take, then porters haul it home over distance-scaled days, one haul in flight. It lands in the stockpiles, chronicled — or a raid threatening the settlement costs the load, chronicled, never silent |

## Pass 19 — Layered catalogues, footprints, sockets, and the trigger law

| Step | Action | Expect |
|---|---|---|
| 61 | Ship a tiny second mod file re-declaring `<building Key="tent">` with just a new cost and a new skin | The tent keeps its place in the list, costs the new figure, offers the new skin. Attributes the file omitted survive. A standing tent is untouched — what was spent and cut into the ground never follows a mod update; what the settlement re-reads (name, gates, skins) does |
| 61a | Blank an attribute (`Contents=""`) vs omitting it | Blank erases; omitted keeps. The modder contract in MODDING.md says so |
| 62 | Commission a design whose tier declares a footprint smaller than its plot | The building fronts the heart, the yard lies behind. Yard = plot minus footprint, recomputed as tiers grow |
| 62a | Let a tier grow onto ground a yard trade occupies | The improvement refuses **by name** — nothing in a yard ever comes down on its own |
| 62b | Author a tier whose footprint exceeds its plot | Refused at load with both spans named; refused again at improvement: "wants more ground than this plot holds — strike and stake larger, or leave it" |
| 63 | Charter → **Change what a plot is** (`2`) | Within the plot's own type×size set: cheap, one ceremony, one disclosed figure (strike effort + new cost − salvage). Re-typing is the full strike-and-re-stake |
| 63a | Charter → **Give a building a new look** (`3`) | Any registered skin — including one a mod added after the building was raised — for a tenth of build cost. No output change |
| 64 | Grow a house until its upgrade is earned, with residents and spare tolerable beds | It upgrades by itself: residents lodge to their own standard during the rebuild. An ordinary settler takes a bunk; the notable in the fine house will not take a tent — and the upgrade **waits** until lodging they'd accept exists |
| 64a | Earn an upgrade on a water work the city leans on | **Held, not taken**: "ready to improve, and held — the city leans on it." Nothing acts. Force it from the works screen and the dip is disclosed before your consent |
| 64b | Read the held offer, walk away for a season, return | Still held, unchanged. No trigger anywhere reads elapsed time as a cause |

## Pass 20 — Who lives where, and what they will accept

| Step | Action | Expect |
|---|---|---|
| 65 | Read the roll of settlers | Each named settler shows **where they sleep**, stable across visits. An in-place upgrade keeps everyone's address |
| 65a | Settle a robot (or any inorganic wanderer) | It needs charge, not food — derived from the engine's own parts, not authored. It lodges by the charging post and the larder never counts it |
| 65b | Settle two creeds the engine holds at open hostility | They are never assigned the same building. The standing −50 grudges of zealots toward strangers do **not** break households — only real hostility does |
| 66 | Fill every home, then let a settler arrive | **They do not join.** The announcement names the real reason: no home they would take — not "no bed", if the beds that exist fail their needs |
| 66a | Burn or condemn a settler's only acceptable home | Warned once, with **how long they have actually stood there** and what would keep them (raise a bunk, stake a plan). They are at the roof brink: the accrual stops there. Six **world-days** later, if nothing changed, they leave through the ordinary emigration, chronicled by name and cause in both registers |
| 66b | Trigger the brink, then stay away a season. Then trigger it again and stay away ten days | **The same brink both times** — nothing accrues past it, and the elapsed figure in the warning differs and is honest about it. Both times they are gone when you get back, and the report dates the leaving to the day the window ran out, not to your homecoming |
| 66c | Re-house them inside the window | The brink **lifts** and the warning is unsaid. Nothing is remembered against them, and the next loss starts the count from nothing |
| 67 | Ship a mod creature with `r_TAF_Needs` tags, or none at all | With none, its needs derive from its own parts correctly. A `-tag` removes a derived need. Unknown tags are inert, waiting for a consumer |
| 68 | House five settlers of mixed creeds and only a bunkhouse | **They do not all move in.** Packed quarters share only without quarrel; the announcement names the roomiest quarters that still refused |
| 68a | Raise a stone house (Roomed) for the same five | The ambient grudge tolerates walls between beds. The −50 zealot-toward-stranger pairs now share |
| 68b | Try to house a true fault-line pair (−100: esper and templar) in the marble fine house | **Refused at every tier.** Walls answer prejudice, not a creed war — they need separate buildings, and the city partitions into quarters by itself |
| 68c | Watch where housing rises in a two-creed city | Quarters emerge — the layout grammar clusters housing with housing, and no building can hold both creeds. No code knows the word "quarter" |

## Pass 21 — How belief moves

| Step | Action | Expect |
|---|---|---|
| 69 | House a creed-minority settler in a stone house with a majority household, and keep visiting | A **season of shared living**, then perhaps a conversion — chronicled by name, deterministic on reload. One open room converts nobody; quarters of one's own are slowest |
| 69a | Feed a citizen at a table of another creed while they sleep in a house of a third | Pulled two ways, they convert to **neither** — a second pull takes points off the first |
| 70 | Charter → **Consecrate a shrine** (`4`) to a creed the realm has dealt with | A chronicled ceremony. Staffed, the shrine slowly draws the *neutral* of its zone toward its creed — never the opposed, who instead begin to resent it |
| 70a | Let a rival shrine stand over a settler's quarter | Warned once, wherever you are, naming what to take back off them; eighteen world-days to do it; then they leave through the ordinary emigration, by name and cause, dated to the day the window ran out. **Remove the shrine at any point and the pressure genuinely lifts** |
| 70b | Unstaff the shrine or scriptorium | An unstaffed shrine is a stone; an unstaffed scriptorium is a room of vellum — each says so once |
| 70c | Staff a scriptorium in a mixed zone | The ambient grudge reads one band gentler for lodging and osmosis. It converts nobody |
| 71 | Charter → **Share water with a settler** (`5`) of another creed | The founding rite's own eight drams plus a measure for what stands between you — disclosed first, spent whichever way they answer |
| 71a | Be refused | Prose worth reading, naming what would have to be different. Not retryable until something real changes |
| 71b | Ask a fourth time | They answer by counting the askings out loud, and the question is shut from that night |
| 71c | Convert someone whose old creed ran hot against the new | Both registers carry it and **disagree**: your book says they took the rite; the roads say they were bought |

## Pass 22 — The full arc of a great work

| Step | Action | Expect |
|---|---|---|
| 72 | Charter → Status, standing in different parts of a built-up zone | It names **which quarter you are in and what shades it** — the ground around a temple reads differently from the ground around a workshop. Water, food and roofs stay citywide |
| 72a | Raise a small shrine, then a middling one, then a temple | Reach grows with the ground: plot, quarter, zone. The great work takes the city — once it is headed |
| 72b | Raise a middling civic work **among the houses**, then strike it and raise the same design out past the fields | The level moves with it: a lift lands in proportion to the roofs it covers. A small (S) work shades its own ground and does not move the level at all |
| 72c | Give a house a **yard trade** and watch the level | A vine lattice's `food:1` reaches the settlement's own pool; a hide rack's `craft:1` reaches the lift. Letting the trade go takes it back |
| 73 | Commission an L design with no mason's yard | Refused: "there is no mason's yard." Build one and leave it unstaffed: **"the mason's yard stands idle"** — the two refusals read differently |
| 73a | Run a yard | Two loads of raw become one of shaped, worked by whoever the staffing pass left, chronicled the first day the saws run. Crew speed reads off who they are — Strength at the saw-pit, a mind at the furnace |
| 73b | Check a high-craft design's price | Denominated partly in **vanilla tinkering bits**, drawn from the stockpiles — donate by putting scrap in. Great works may also want a rare find, and say so |
| 74 | Crew a demanding work with weak hands | It builds **slower and says so once** — floors at a quarter pace, never stalls. The ablest available hands go to demanding works first, deterministically |
| 75 | Let raiders past the wall | A work may stand damaged — bounded, named once, running reduced, never destroyed. Player-placed objects untouched, ever |
| 75a | Run a mill at full stretch for many days, including days you are not there | Hard running may wear it, and a mill that ran hard through an absence wore for it — the streak is counted in **activity-days**, not in visits. A mill standing idle never wears, however long the calendar runs: **idleness wears nothing** |
| 75b | Watch a damaged work | Mending auto-queues like an improvement — visible on the work, holdable, one mending at a time — and costs shaped stone or worked metal and bits from the chain |
| 76 | Head a great work | The office machinery names whoever among your settlers is actually suited — you appoint nobody. Unheaded, it keeps its zone and says so once; headed, **it takes the city back that day** |

## Pass 23 — Subsidence: the city settles back to what it carries

| Step | Action | Expect |
|---|---|---|
| 77 | Grow a Town, raise its air-well field and its terrace, then strike the works that carry it (or let a raid take them). `kingdom:status` | The report names the level and **what binds it**: "carries 25 — water" becomes "carries 9 — food". Nothing has happened yet; the settlement is simply standing above what it carries |
| 77a | Stay put and let four world days pass | The slide begins and says so **once**, naming the binding good and the level it is heading for. It does not repeat, and it does not nag |
| 77b | Watch it step | One step every four days, shedding (rung + 1) settlers a step — a City sheds five, a Steading two. Every departure names its cause |
| 77c | Let it cross a rung | The stage falls **one rung per reckoning**, and only on a clear shortfall (20% benefit of the doubt on both of the stage's readings). Each lost rung gets a dated chronicle line — dated to the day it actually happened, not to the day you read it |
| 77d | Look at the works after a rung is lost | Every standing work was asked, independently, at that rung's own reach (a City rung half the settlement, a Steading rung a fifth) — not a fixed two. Named ones read by line, the rest in one summary. **Damage, never deletion**: nothing passes the wear ceiling, nothing is unbuilt, and nothing player-placed is touched, ever |
| 77e | Raise the works back up midway | The slide **arrests**, says so, and unsays its warning. A settlement inside its 20% band never moves at all |
| 77f | Instead, leave for a hundred days. Then try it again and leave for a thousand | Both come home to the same honest level. The slide **converges and stops**: a City whose works carry a Town becomes that Town, not a Camp, because the bill per head falls with the rung as fast as the people do |
| 77g | Let a City with nothing standing run all the way down | It stops at **Camp** — floored, derelict, legible, and still yours. There is no rung below it |
| 77h | Save and reload mid-slide | The same works are the worse for it. A reload never re-rolls a collapse the chronicle has already described |
| 77i | Turn off *a settlement standing above what its works carry settles back* in options and repeat 77a | Nothing subsides. The level is still measured and still reported |
| 77j | Let a City collapse all the way to Camp, then walk its former plots | Most read as battered, half-ruined or ruined **in name and in look** — the adjective is on the building itself and its description matches — with a few sound survivors among them, not a uniform coat of scuffing. Mend one and the name walks back down the same ladder it climbed |

## Pass 24 — The brink: the last arrestable window

| Step | Action | Expect |
|---|---|---|
| 78 | Take away a settler's last acceptable home and read the warning | It names them, the cause, **how long they have actually stood there**, **what would stop it**, and how many days are left. Once |
| 78a | Reach the same brink and stay away ten days. Reach it again and stay away a thousand | **The same brink both times.** The accrual stopped at the line — there is nowhere past a brink to arrive at — and only the elapsed figure in the warning differs, which is the honest part |
| 78b | Re-house them inside the window | The brink lifts, the warning is unsaid, and the next one starts from nothing. Arrest by **acting**, at any point up to the day it fires; waiting is not a strategy and never was |
| 78c | Take the warning, then walk out of the settlement and stay away six days | You come back to a settler who is **already gone**, and a report that dates the leaving to the day the window ran out — not to your homecoming. Warned, coached, given fair time, and it happened anyway |
| 78d | House a creed-minority settler with a majority household and let real days of shared living pass | Conversion accrues in **cohabitation-days** — days they actually shared a roof, scaled by the quarters' closeness — never in visits. At the line it stops, the word finds you, and eighteen world-days start running |
| 78e | Break the household up, or pour the water rite | The creed brink lifts. Let the eighteen days run instead — home or away — and the conversion lands, chronicled in two registers that **disagree**, dated to the day it landed |
| 78f | Drive two cities' quarrel to the breaking point | The realm stands at a **city brink** with nine world-days, and the loudest tier of the warning ladder has already stood for eight days before it. Mend the cause and it lifts; let it run and the city secedes with its ground, people and buildings, whether or not you were there |
| 78g | Save, reload, and read all three again | Causes, crossing ticks and **warning ticks** all survive. A per-settler brink rides **the settler**, so swapping seats never carries one to the wrong city |
| 78k | Reach a brink you were never told about — a save from before the word went out — then come back after a year | Nothing has fired. You are warned on the pass that finds it, and the **whole** window starts from that day. Presence is not a shield; ignorance is |
| 78l | Take a warning while standing somewhere else entirely | It reaches you where you are, framed as word out of the named city, and it is **not** repeated at the seat. Standing in the settlement, you get the plain line and no second telling |
| 78h | Let a home wear past condemnation with people living in it | It stops counting as a roof — it is not cleared, unbuilt, or moved — and everyone under it is recorded at a roof brink dated to the day the roof went, not to the day you noticed |
| 78i | Let a subsidence slide half-wreck an OCCUPIED home, to wear ≥ 40 (`KingdomLodgingRules.CondemnedWearPercent`) | Its residents show unhoused on the roll, each by name: "sleeps in the open: every roof here has fallen in past living under. Mend one and [name] has a home again." The building itself still stands, untouched |
| 78j | Mend the wear back below 40 | The home counts as a roof again, and the next pass re-houses them |

## Pass 25 — What a season away actually costs

| Step | Action | Expect |
|---|---|---|
| 79 | Found, dedicate water, leave for a season, come home | **Every day** you were gone is billed, out of the settlement's own stores. Nothing was forgiven and nothing was doubled. The report says what was drunk, delivered and lost |
| 79a | Do the same with a nearly empty cistern | The thirst ladder runs off the same honest elapsed, and still stops at the loyal core: empty casks and one rung of the ladder, never an empty town |
| 79b | Raise an air-well field at a Town — it wants no crew — and leave for a season | It **made water while you were gone**: what it carries arrives in the casks day by day, on the same checkpoint discipline fetch uses. The `carries` figure in the status report and the drams actually stored agree |
| 79b1 | Raise a **reservoir** instead and leave for the same season | It made **nothing**, and never claimed to. A store holds; the stage gate it opens and the room it gives the detail are what it is for. If a vessel ever appears to make water, that is a bug worth filing |
| 79c | Compare the water detail against the bill at each rung | A camp wants half its people hauling with nothing it can build to help, and a Town over nine tenths, which is where hauling stops being a strategy. Two air-well fields cover a Town's whole bill **for no hands at all**. That is the handover |
| 79d | Hold a City on grand works and read who is standing where | Water and roofs want nobody. **Food still wants hands** — a grange takes three, the home farm four — and it is the only binding good whose big works do. Eight settlers of fifty feed the city, and that is the design rather than a gap |
| 79e | Let a slide ruin an air-well field, then read `kingdom:status` | The Charter's level falls with it: every work now carries at its own condition, crewed or not (Addendum 10(b)), so the field's `carries` figure reads under its catalogue number, not the full one — and the drams arriving in the casks fall by the same fraction, because the two are one number. The roof half is answered separately, by condemnation (78h) |
| 79f | Leave a staffed sawyer's yard to run for thirty days | Thirty days of shaping, held to the bench's own daily width — **not** eight units per homecoming. Leave the same yard **unstaffed** for thirty days and it shapes nothing, and says so once |
| 79g | Leave a scaffold with nobody free to work on it | It does not rise. The shortfall is named once and clears itself the moment hands are free; the raising ceremony still tells attended from absent, because completion is stamped to when the work actually finished |
| 79h | Leave a holed cistern (wear > 0) alone for a season | It is named **once** when the leak begins ("weeps down its east face, and what it holds runs away into the ground"). What it holds runs out on world days regardless of who is watching, proportional to the days away — lost to the ground, not pooled anywhere a founder can fetch it back |
| 79i | Mend that same cistern | The leak is unsaid ("is sealed again, and holds every dram it is given"), and it holds whatever is poured into it again — the same pass it is mended, not a season later |
| 79j | Let a slide ruin a staffless work — a cistern or a home — and check the mending queue | It stands in the queue the same as any crewed work: mending now walks every finished work (`Survey.Built`), not only the ones that ask for a crew, so a holed cistern is never damaged forever for want of ever being asked |

## Pass 26 — Claiming ground: gate refusals, the wall line, and a second zone's memory

The founder's own claim, at last. Everything behind it was already built and tested with nothing
to exercise it — this is the verb that reaches it.

| Step | Action | Expect |
|---|---|---|
| 80 | Grow to **Village** (two-zone ceiling; Camp and Steading both hold one), stand on ground that does **not** border the claim, Charter → **Claim this ground** | Refused by name: "A city grows outward from what it already holds. Stand on ground that borders X — beside it, or the stratum directly above or below it — and claim there." Nothing spent |
| 80a | Walk to ground that borders the claim and try again | Asked first — "Nothing is spent" — then claimed on yes: the message names the ground, states the wall clause, and states the holding line ("2 held, which is all this rung answers for") |
| 80b | Raise a wall on the original claim's outer edge **before** doing 80a, then raise another wall on the newly claimed zone afterward | The first wall stands exactly where it was — untouched, and now an inner wall. The second stands on the zone's new outer edge. Nothing already built is moved |
| 80c | Try to claim a third zone at Village | Refused by name: "X is a village, and a village holds 2 parasangs. Grow into a town and this ground is yours to take." |
| 80d | Claim ground that touches the held claim only diagonally, or straight down into the rock | Allowed, and the claim message says the wall line does **not** move — only an orthogonal neighbour in the same stratum frees an edge, and that is the honest answer, not a bug |
| 81 | With two zones held, raise water/food/roof works in **both**, stand in one, Charter → Status | "carries N" sums both zones — the one you're standing in counted live, the other **as it was last seen** |
| 81a | Leave the settlement for several days without visiting the second zone, return to the first, read Status | The level carries a dated clause: "counting one parasang as you last saw it N days ago" |
| 81b | On the same visit, read Status standing in the first zone, then walk into the second and read it again | The same "carries N" figure either way. Before this fix the level swung with whichever zone the founder walked in through — entering through the mine overwrote the granary |
| 81c | Build enough dedicated storage across both zones to cross a stage threshold, then walk in through the zone that alone would **not** cross it | The stage still reads correctly off the city's whole storage, not the one zone's — `UpdateStage` reads city storage after the pass records this zone's own sighting |

## Pass 27 — Underground honesty, and the gatehouse

| Step | Action | Expect |
|---|---|---|
| 82 | Claim ground a stratum down, Charter → Commission | A sky-wanting design (dew catchment, catchment bank, air-well court, air-well field, the condensing hall, sailvane) carries **[wants open sky]** in the list — the same tag every other blocked gate wears, so the catalogue never silently shortens |
| 82a | Try to commission one of them anyway | Refused by name: "The dew catchment wants weather, and there is none under the rock. Raise it under open sky." |
| 82a1 | Read what IS offered down there instead | The **weep-tap** and the **weep gallery** — the underground water lane, cut into a damp seam rather than hung under the sky — plus the salt-pan, which goes anywhere. A stratum down is a different water game, not a poorer one |
| 82b | Commission an **Open**-declared design underground instead (a tended plot, a salt-pan terrace) | It stays open: no walls, no door, no floor — a field cut into the rock, not a sealed chamber. It does not count as housing |
| 82c | Commission a design that declares no `Roof` (defaults Walled) underground | Still carved, exactly as before: the rock is the wall, no wall is raised, clearing costs double, paid back in stone |
| 83 | Reach **Town**, Charter → Commission → **gatehouse** | It rises astride the road: the buildable frontier cell nearest the settlement's own way out — the same cell `KingdomRoads` already walks a `HeartToGate` errand at — not a random gap in the wall line the way an ordinary wall segment sites |
| 83a | Strike it and commission another | It returns to the same cell, reload after reload — ties break north-then-west, so the same settlement always puts its gatehouse in the same place |

## Pass 28 — The water lane, re-grounded

Water production is a thing you can now WATCH, and the whole point of this pass is that what
you can watch and what the ledger counts are the same number. Every producer carries vanilla's
`LiquidProducer`; a design's `Carries="water:N"` is `1200 / mean(VariableRate)` of that part;
and no vessel declares water at all.

| Step | Action | Expect |
|---|---|---|
| 84 | Raise a **salt-pan** at a Steading and stand beside it for a few hundred turns | Its own basin fills, a dram at a time. `l`ook at it: the pan is a real `LiquidVolume` with water in it, and you can `Fill` a skin from it by hand |
| 84a | Leave the pan full and keep watching | It **stops**. `FillSelfOnly` makes the producer idle once its own tank is full and pure — a work nobody draws down is a work standing idle, and that reads correctly on the object |
| 84b | Watch any producer for a whole day (1200 turns) and count the drams it made | It matches the design's `Carries` figure: 2 for the salt-pan, 3 for the dew catchment, 5 for the catchment bank, 4 for the weep-tap, 12 for the weep gallery, 15 for the air-well court, 25 for the air-well field, 50 for the condensing hall. A producer that visibly out-makes or under-makes its catalogue number is a bug worth filing |
| 84c | Check the ground around any producer after a long watch | **No puddles.** A producer that overflowed onto the floor would mint open water the detail could then haul again — the same dram twice. Every producer fills only itself |
| 85 | Raise a **cistern court**, a **reservoir**, or the **waterworks**, then read `kingdom:status` | Its `carries` contribution is zero on the water line. It raised **stored capacity** instead, which is what opens the next rung (16 / 64 / 256 / 1024 drams for Steading / Village / Town / City) and what bounds how much the detail may haul |
| 85a | Raise a reservoir with an empty water detail and no producer, and leave for a season | The casks are as empty as you left them, minus the drinking. A reservoir holds; it has never conjured a dram, and after Addendum 11(a) it no longer claims to |
| 86 | Reach **Village**, then Charter → Commission | Two new middling rungs: the **air-well court** (domes of carved stone, no crew, wants open sky) and the **weep gallery** (a worked seam, one hand, goes underground). The dew lane and the underground lane are genuinely different answers to the same rung |
| 86a | Reach **Town** | The **air-well field** — 25 drams a day for no hands, on a large plot in an agrarian or craft quarter |
| 87 | At **City**, try to commission the **condensing hall** without a certified Solar Still | Refused by name, and the refusal says which knowledge is missing. It is the one water design that is not commissioned out of what the ground gave back |
| 87a | Drag a Solar Still home onto claimed ground and Charter → certify it, then look again | The gate opens (and certifying is worth two craft points toward the `foundry` level the hall also wants). The keepers took a dead still apart; what they build afterwards is theirs |
| 88 | Raise a **water wheel** on dry ground | It **turns**. The wheel digs itself a brackish race the way vanilla's own wooden water wheel does, so it never reports `HydrodynamicForceInsufficient` — but `kingdom:status` prices it at about two per cent of its rating |
| 88a | Try to drink or haul out of that race | Nothing. It is brine (water-600, salt-400), and the settlement's survey counts only pure water. The wheel brings a millrace, not a water supply |
| 88b | Raise a second wheel beside a real pool and compare | Up to a hundred per cent. Siting is still the whole game; what changed is that a badly sited wheel now fails **visibly and by degree** instead of silently and absolutely |

## Pass 4 — Attitudes and persistence

| Step | Action | Expect |
|---|---|---|
| 17 | With Snapjaws standing at 600, wish `snapjaw scavenger` near a citizen | It does NOT attack the citizen (it may still dislike *you* — that's the two ledgers) |
| 18 | Save → quit to menu → reload | `kingdom:status` + `kingdom:chronicle` intact; selftest still all-PASS |
| 19 | Player.log | No "Bad event bind", no exceptions from ThousandAndFirst |

## Known v0 limits (not bugs)

- No ownership stamping on claims (can't rob your own city — membership design pending).
- Settlers use vanilla farmer behavior; ambient roles come with the amenity work.
- Stage moves in **both directions** now. It climbs on the reading and falls only on a clear
  shortfall, one rung per reckoning, with Camp an absolute floor. A city that subsides is the
  system working; a city that subsides while it is inside its 20% band is a bug worth filing.
  The withered overlay is still designed and not yet built.
- **What time does and does not move.** Everything on the world clock moves whether you are
  there or not — crops, yards, scaffolds, wear from hard *running*, osmosis, dissent, the slide
  toward the supported level. Every one of them is gated on **labour**, so a pass asserting
  "untouched on return" is right only where nobody was working, and a pass asserting "at most
  three days were charged" is wrong everywhere: no clock in this mod caps elapsed time any
  more. Irreversible consequences stop at a brink and push the word to you wherever you are;
  their windows then run in **world-days** from that warning and spend whether or not you come
  back (Addendum 10(a)). A pass asserting "nothing irreversible can happen while I am away" is
  wrong now; a pass asserting "nothing irreversible happens that I was not warned about" is the
  one that holds.
- **Food is a flow now, and the two scarcity ladders share one bite.** Fields make their
  `Carries` into the larders on world time, the settlement eats one ration a settler a day, and
  a settlement that cannot pay climbs a hunger ladder shaped exactly like the thirst one. Both
  ladders run at once and each says its own sentence, but a failed resolve costs the **worse of
  the two, never their sum** — so a city that is dry *and* starving loses one settler for it, not
  two, and may wear both marks. A pass reporting "it lost two people in one homecoming for one
  bad year" is a bug worth filing. Trade still carries no food: the only ways into a larder are
  the fields, the garden, and your own hands.
- Founder's basin is wish-obtainable only; its acquisition quest is slice 0.2 content.
