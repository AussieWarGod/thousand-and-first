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

## Pass 5 — Districts and commissions

| Step | Action | Expect |
|---|---|---|
| 20 | Charter → **Designate district** in a claimed zone → market | Chronicle line; status unchanged otherwise |
| 21 | `kingdom:status` | (Market district shortens arrival intervals by 10% — verify next arrival tick moved after next growth pass) |
| 22 | Charter → **Commission a building** → cask rack (4 drams in stores needed) | Stores −4; scaffolding appears nearby; chronicle line |
| 23 | Wait ~1200 ticks (or explore and return) | Scaffold becomes the cask rack; completion message + chronicle |
| 24 | Commission the great cistern (16 drams) | Same cycle; stored capacity +256 when done |
| 25 | Commission a communal bunk; watch settlers at night | A settler eventually sleeps in it (vanilla bed behavior) |
| 25a | On first reaching Steading | "A settler has taken up the trade" — the first stall opens; trade with them shows tier-1 stock |
| 25c | Charter → Commission | The design list is loaded from KingdomBuildings.xml (5 entries); third-party mods shipping their own `<kingdombuildings>` file extend it automatically |
| 25b | Browse vanilla tier-1 merchants elsewhere | The founder's basin occasionally appears for sale (8% per restock) — the legitimate acquisition path |

## Pass 6 — Raids and tribute

| Step | Action | Expect |
|---|---|---|
| 26 | Reach Steading; `kingdom:standing Snapjaws:-300`; leave and re-enter the claimed zone | Warning: snapjaw scouts seen; chronicle line (needs raid cooldown elapsed — or use `kingdom:raid` to force) |
| 27 | Charter → **Pay tribute** (12 drams in stores) | Raid averted; snapjaw standing +50; chronicle line |
| 28 | Force again via `kingdom:raid`, don't pay, wait out the lead (or `kingdom:raid` again) | 2+ snapjaws spawn at the zone edge and attack citizens; chronicle records the raid |
| 29 | After the fight: `kingdom:status`, `kingdom:chronicle` | State coherent; raid recorded in both registers |

## Pass 6b — Fortification

| Step | Action | Expect |
|---|---|---|
| 29a | With no defences built, `kingdom:raid`, then leave the zone and come back | Homecoming digest reports drams carried off, possibly a casualty |
| 29b | Charter → Commission → **thorn palisade**; wait out the build; `kingdom:status` | Status shows the settlement's defence at 3 |
| 29c | `kingdom:raid`, leave and return | Fewer drams taken than in 29a — the wall reduced the loss, it did not stop it |
| 29d | Commission a **watchtower** with nobody spare to man it; `kingdom:status` | Defence unchanged: an unmanned tower is a platform |
| 29e | Grow the population until the watch is crewed; `kingdom:status` | Defence rises as the crew fills; a half crew gives half the tower's defence |
| 29f | Reach defence 12+ (palisade + crewed watchtower + rampart at Village); `kingdom:raid`, leave and return | Digest: raiders turned back, nothing taken; chronicle records it as an accomplishment; the walls appear under deeds in `kingdom:status` |

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
| 41 | Found, claim, dedicate water, commission a bunk. Leave the zone, travel a day or more, return | **One popup**: "while you were away", the events as past-tense lines, and a ledger of drams drawn, delivered, drunk and lost — not a scroll of separate messages |
| 42 | Read an arrival line | It names the cause: "Word of the cask rack raised at Kavvat reached the hills — a settler has come." Founding, stage-ups, commissions, caravans and tribute all set the cause |
| 43 | Strike a charter, then stay away for several caravan intervals | Missed deliveries **bank** (up to 3): "3 caravans of the villagers of Joppa came under charter: 18 drams." Absence accrues, never decays |
| 44 | Return after a short walk (under a day) | No digest popup — the digest is for absences, not for stepping outside |
| 45 | Charter → Dedicate a vessel → **Dedicate every vessel here** | All undedicated vessels join the stores in one action, up to the cap |
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
