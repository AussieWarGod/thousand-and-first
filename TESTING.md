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
| 14 | If the zone has a fresh-water pool: `kingdom:status` before/after `kingdom:grow` | Settlers fetch pool water into containers (stored rises, open falls) |
| 14a | Put an empty dedicated vessel beside a salty pool, then `kingdom:grow` | No brine is fetched or converted; both volumes remain unchanged, and `kingdom:selftest` reports the mixed-liquid checks PASS |

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
| 16e | Let the settlement stay dry for 3+ growth passes at Steading | **Withered** flag in status + chronicle line; refill water and grow | Recovery message and chronicle entry |
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
| 16p | Stay away from the second city three or more in-game days, then walk into it | It caught up on arrival from its own clock: at most three days of upkeep and at most three arrivals, however long you were gone |
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
| 29b | Charter → Commission → **thorn palisade**; wait out the build; `kingdom:status` | Status shows the settlement's defence at 3 |
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
| 43 | Strike a charter, then stay away for several caravan intervals | Missed deliveries **bank** (up to 3): "3 caravans of the villagers of Joppa came under charter: 18 drams." Absence accrues, never decays |
| 44 | Return after a short walk (under a day) | No news line — the homecoming is for absences, not for stepping outside. The Charter entry still opens and says nothing has happened |
| 45 | Charter → Dedicate a vessel or larder → **Dedicate everything here** | All undedicated vessels join the stores in one action, up to the cap |
| 45a | Stand beside a chest or footlocker with food in it and dedicate it as a larder | It is marked a larder of the settlement. Nothing moves and nothing is taken — dedication is a mark, not a transfer |
| 45b | `kingdom:dump` | The pantry reads the food in dedicated larders only, as a count and a tier (Empty / Scant / Modest / Ample). Food in your own undedicated pack is never counted |
| 45c | Release the larder | The count drops back. What was inside is untouched |
| 45d | Commission a **civic larder** (Charter → Commission), then put food in it | It counts as a larder without needing a chest of your own |
| 45e | With the larder Scant or better, Charter → **Share a meal from the larder** | It **asks first**, naming what the meal will take and what the larders hold. Answer yes and the settlement eats: food is spent from the dedicated larders only, a settler from the roll speaks, and the chronicle records it. Word travels — the meal becomes the settlement's deed, so it draws settlers the way any deed does |
| 45f | With an empty larder, Charter → Share a meal | Refused plainly, and nothing is lost. An empty larder costs the settlement nothing at all: no hunger, no unhappiness, no decay. A player who never dedicates food plays exactly as they do today |
| 45g | Check your own pack and any undedicated container after a meal | Untouched. Only dedicated food is ever spent |
| 46 | Charter → Status | The ledger's effects are visible: stores, shop tier, idle/shorthanded works, and the next-need line |

## Pass 10 — Names, policy, and answering a threat

| Step | Action | Expect |
|---|---|---|
| 47 | Let settlers arrive, then Charter → **The roll of settlers** | Real generated names, each with origin and the date they came. Look at a settler in the world — it carries that name |
| 48 | Charter → **Standing policy** → toggle gates to guarded | Chronicle line; arrivals slow (~40%) and raids come less often. Toggle stores to thrift: upkeep drops a quarter, arrivals slow further |
| 49 | Provoke a raid (`kingdom:standing Snapjaws:-300`, `kingdom:raid`), then Charter → **Answer a threat** | Three exits offered: pay (with the current demand), send word (only if their standing ≥250 and you haven't stalled), or let them come |
| 50 | Choose "let them come", then face the next warning | The demand has grown by half. Stall again and it grows again, up to four times |
| 51 | Raise Snapjaw standing to 250+, force a fresh warning, choose **send word** | Raid averted with no water paid, chronicled as an accomplishment — goodwill spent instead of drams |
| 52 | Force a warning, then leave the area for more than a day and return | **No ambush at the gate.** The raid resolved in your absence: the digest reports drams carried off and whether anyone was lost |

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

## Pass 13 — The tended plot

| Step | Action | Expect |
|---|---|---|
| 49 | Charter → Commission → **tended plot**; wait out the build | A plot appears. What it can grow was decided by the ground the rite was poured on — the style your founding recorded, not a fresh look at the dirt |
| 49a | Plant, then **stand there and wait** | It ripens while you watch. A founder who never leaves must still see their plot work |
| 49a2 | Plant, leave for days, come back | It resolves on arrival too. Both halves of the clock work: the tick it is due, and the visit that finds it overdue |
| 49a1 | Commission a plot with **no larder dedicated** | It does not plant. Water is never spent on a crop with nowhere to land |
| 49b | Watch the stores while it plants | It drinks, and the ledger says so — but only from what the day's upkeep and arrivals left behind. **A plot can never be the reason the thirst ladder fires** |
| 49c | Let the stores run low with a plot planted | The plot goes dormant and waits. It never dies, and it never punishes |
| 49d | Have a dedicated larder when it ripens | The harvest goes into the larder, and the chronicle records it. With no dedicated larder it waits — it will not put food anywhere you did not dedicate |

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

## Pass 4 — Attitudes and persistence

| Step | Action | Expect |
|---|---|---|
| 17 | With Snapjaws standing at 600, wish `snapjaw scavenger` near a citizen | It does NOT attack the citizen (it may still dislike *you* — that's the two ledgers) |
| 18 | Save → quit to menu → reload | `kingdom:status` + `kingdom:chronicle` intact; selftest still all-PASS |
| 19 | Player.log | No "Bad event bind", no exceptions from ThousandAndFirst |

## Known v0 limits (not bugs)

- No ownership stamping on claims (can't rob your own city — membership design pending).
- Settlers use vanilla farmer behavior; ambient roles come with the amenity work.
- Stage never regresses; the withered overlay is designed but not yet built.
- Founder's basin is wish-obtainable only; its acquisition quest is slice 0.2 content.
