# The Thousand and First — Working-tree / v1.0 Test-Candidate Protocol

The manifest remains `0.2.0` while this protocol is unfinished. A v1.0 test-candidate claim requires
the current working tree—not the historical 0.2 receipt—to pass every applicable automated,
native, human, compatibility, structure, and subscribed-package gate named here.

**Dev diagnostics are OFF by default** in the release build. Enable **The Thousand and First:
write diagnostic lines to Player.log** in Options for a test session; the isolated smoke profile
enables it itself. If anything looks wrong, wish `kingdom:dump` for the full state readout — it
prints to screen and, while diagnostics are enabled, to the log for the log-watcher.

Fresh game launch (never mid-session approval — ghost assembly generations). Approve
**The Thousand and First** at the mod prompt, then load any save or start a new game.
Say when you're launching so the log watch can run alongside.

## Execution index

Pass labels are stable case identifiers, not a numeric running order. Execute the sections in this
file order; use the table as the top-level checklist.

| Done | Order | Passes | Scope |
|---|---:|---|---|
| ☐ | 1 | 0, 1, 2, 3, 3b, 3c, 3d, 3e | Consent, foundation, water, rite, Charter, style, second city, founding boundaries |
| ☐ | 2 | 5, 6, 6b, 8, 9, 10 | Districts, raids, fortification, homes, return, names and policy |
| ☐ | 3 | 7, 12, 13, 14 | Trade, intercity water, cultivation, exile and return |
| ☐ | 4 | 15–29 | Layout through meals and industry, in the section order below |
| ☐ | 5 | 33, 32, 34, 30, 31, 35, 36, 38 | Identity, receipts, lived day, absence, petitions, style/creed stack, succession, expeditions |
| ☐ | 6 | 4 | Final attitudes, save/reload, and persistence sweep |

## Current portable evidence boundary

Nine focused one-survey source-contract cases pass. The final integrated pure/source suite passes
7,586 / 7,586 cases against the configured licensed Qud base; the portable suite passes 171 / 171.
Both recorded runs report zero skipped. Public CI without installed Qud data permits exactly three
named installed-data-only skips instead of fabricating fixtures. TestMain rejects an unexpected or
missing allowlisted skip, and an explicitly configured incomplete base fails rather than skipping.
Canonical release `DevTests/test.ps1` forbids every skip, and `Tools/release-check.sh` injects the
exact Qud base.
`docs/STATUS.md` owns the exact latest receipts. None of this signs native Qud behavior,
appearance, accessibility, compatibility, or Steam installation.

## Latest retained automated native smoke — partial evidence, not protocol signoff

On 2026-08-27, repository-controlled automation deployed clean commit
`19fb8ee4f4600d93ff31b2de63e0628a1c0191df` and ran Caves of Qud 1.0.5/core 2.0.211.51 in a
fresh isolated profile. It founded **Smokehold**, seeded standings
from 141 live factions, passed all 17 `kingdom:selftest` checks, and materialised architecture
gallery case 1/2,064 through the production stamper: `airwellcourt`, Medium storage,
`eater-reuse`, North pose, `water-airwell-eater` palette. Inspection of the captured sample image
found a coherent solid shell, road-facing entrance, open court, air-well fixture, table, and
storage fixture. The
run saved, returned to the main menu, cold-loaded the same save, retained the gallery receipt, and
passed the same 17 checks again. Its captured `Player.log` passed `Tools/check-player-log.sh` with
no Thousand and First warning, error, or exception frame.

The disposable capture files were not committed as release evidence. This is a recorded result for
commit `19fb8ee`, not a durable human-review or release-evidence record.

This closes only that commit's loader, founding, one production architecture sample,
save/cold-load, self-test, and log smoke. Every later structural revision requires a new
compile/load/log pass against its exact commit and is not native-signed by this receipt. It does not check any
execution-index box. The complete native protocol, every other gallery case and reachable visual
state, lived-city observation, accessibility, representative compatibility stacks, dense
performance, and private Steam subscription remain unsigned.

## Prior automated smoke — historical evidence, not current signoff

On 2026-08-25, repository-controlled automation ran Qud 1.0.5/core 2.0.211.51 against an earlier
runtime revision in a fresh isolated profile. It founded Kavvat,
founded Sheol as a refuge on separate ground, proved `cities=2/2` with 109 carried settlement
fields and no seat mismatch, passed all 17 `kingdom:selftest` checks, saved three times, cold-loaded
twice, and proved both seat directions after restart. Each captured Player.log passed
`Tools/check-player-log.sh`. This evidence covers loader and persistence smoke only; it does not
describe materially changed later revisions, check any box above, or replace current-revision
native receipts, human observation, balance, usability, compatibility, or private Steam
subscription testing.

## Save-migration harness contract

Every durable schema bump keeps a test-only writer for the exact prior field envelope. A migration
test must write that frozen shape independently, decode it with the current reader, assert the
semantic projection or explicit quarantine, encode the current shape, decode it again, and prove
the second read is stable. Rewriting only a version integer on current bytes is hostile-input
coverage, not a migration fixture. Future, malformed, oversized, noncanonical, aliased, and
partially applied evidence must fail before publishing authority.

The current settlement-archive reader is **v12**. Independently frozen portable writers cover
archive v1-v11; current-reader tests exercise every historical field envelope, its version-specific
defaults, and stable v12 rewrite. Rewriting only the current version integer is still not a
migration fixture. Settlement v9 is an independently frozen **epoch marker** over the same reachable
field envelope as v8; it proves exact-version decode/rewrite, not central-logistics payload
migration. Exact job/logistics payload authority lives in `KingdomRealmArchive`, whose independent
realm Jobs v3/v4 fixtures own that proof. Lifecycle v6→v7 and staged legacy-growth transitions have
separate fixtures. Settlement v11 predates per-source happening cursors; its retained aggregate
tick seeds exact active-source receipts once, so an upgrade neither replays all history nor grants
one source another source's later window.
The engine-owned `KingdomSystem` named-field root begins at v8 and still needs a frozen native v8
save fixture before its first post-public schema bump. Keep that fixture outside the current writer,
cold-load it in Qud, save as current, cold-load again, compare identity/cities/receipts/registries,
and retain it with release evidence.

Before a native architecture session, run `python3 Tools/generate-lot-realizations.py --check`
and `python3 Tools/check-architecture.py --repo-root .`. The first proves the checked-in concrete
larger-lot records match their authored sources. The second exhaustively proves every
commissionable `(BuildKey, type, size)`, exact dimensions, topology, material/technology palette,
and all four poses, including exterior road ingress. These gates do not replace looking at the
buildings in Qud.

## Pass 0 — Cross-run inheritance consent

Use a profile with one promoted, eligible sealed realm. This pass deliberately creates two new
worlds; option is sampled during world creation, not changed retroactively inside a run.

| Step | Action | Expect |
|---|---|---|
| 0a | In Options, leave **import the latest eligible sealed realm** unchecked (its default), then create a new non-Tutorial, non-Daily world | No inherited-realm map note or site is installed. World creation never asks for or reserves a legacy |
| 0b | Quit, enable that option **before** creating another new world, then create it | Latest eligible realm now appears as inherited map note/site. Its appearance proves step 0a left seal eligible rather than silently declining or consuming it |
| 0c | Before retiring a current-schema realm, sketch the seated 80x25 ground: each authored work's exact lot, main cell and public entrance, plus the connected street cells from its boundary entry. Seal while standing on that ground, import, and compare | The inherited ground uses those frozen authored receipts through the normal architecture stamper. Whole lots, main cells, entrances, relative street graph, cairn and history match the witnessed seal; streets remain traversable from the boundary to every public entrance. It does not substitute whatever the current catalogue would choose |
| 0d | Repeat 0c with inheritance states **Held**, **Faded**, **Abandoned**, and **Ruins** (controlled test seals are acceptable) | Held keeps every evidenced whole work standing. Faded deterministically leaves some whole works derelict. Abandoned keeps every evidenced whole work intact and derelict. Ruins deterministically keeps 25–60% as whole derelict works and reduces the rest to memory/rubble; no state invents intact cells absent from the seal. The first basin is never copied or relocated |
| 0e | Inspect every restored root, component, container, liquid vessel and powered object, then walk the complete street graph. Save, quit, cold-load, and revisit twice | No citizen, old item, water/liquid, charge, or player inventory returns. Frozen structure and street geometry, vanilla passability, empty fixtures, cairn/history, and the single reconstruction marker survive reload; site and components exist once and never duplicate |
| 0f | Import a known schema-4 seal made before spatial receipts, then try a current-schema seal whose snapshot/hash or street graph was deliberately malformed | The schema-4 seal remains eligible and uses the bounded legacy proxy. Malformed current evidence is rejected/quarantined before placement; it is never repaired from today's catalogue or partially stamped |

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
| 7a | Save, note every due clock/report and a planted crop or running work, then disable **enable settlement simulation and new civic work** in Options | Realm, citizens, items, liquids, charge, clocks, open receipts, and reports remain. Repeated turns, zone exits/entries, world-map travel, save and cold-load create no civic prompt, scan-side effect, crop/power/research/lab progress, new petition/trade/guest/raid work, or physical commit |
| 7b | While paused, open every Charter chapter and inspect status, homecoming, chronicle, standings, roll, city book, technology map, asks, an accepted petition, and an existing threat answer | Readings and committed recovery remain available and clearly paused. New orders are labelled `[paused]`, refuse before governance/energy charging, and spend no stock |
| 7c | Re-enable on the exact tick immediately before, at, and immediately after a known due clock (three save copies), then take one wake | The transition itself wins in all three copies: no due work or physical effect runs on that wake. Every new automatic deadline is strictly future and the same resume token is applied once |
| 7d | Cold-load once while disabled, once with a resume pending, and once after resume was applied; revisit both cities | Disabled bytes stay stable. Pending resume applies once. Applied resume does not reapply or duplicate. Open receipt-backed recovery retains its exact identity; disabled time never becomes a catch-up burst |
| 7e | With the compatibility mods named in the release evidence enabled, commission/preview every placement that uses a campfire, bookshelf, torchpost, or hookah | Every placement resolves to the `r_KingdomCivic*` wrapper. Fire still cooks and extinguishes to vanilla remains; fixtures keep authored orientation and cannot be picked up/pushed/mirrored or turned into dice/tinker objects; bookshelf is not auto-stocked and hookah begins empty. Harmless merged sound/particle behavior may remain |
| 7f | Save/cold-load the four civic wrappers, extinguish the fire, use the bookshelf/container and hookah normally, then regenerate the same architecture previews | Final capabilities and stable vanilla tiles survive reload; no wrapper duplicates, changes material tier, adds free contents, or breaks ordinary vanilla interaction |

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
| 16f1 | Found on an ordinary overworld site (rite or wish) | The founding popup and the chronicle line name the ground — "founded on common ground", or a style clause if the site earned one. Founding never throws, whatever the terrain |
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
| 21h | Dedicate a chest as a stockpile, Charter → **Clear ground** (`q`), mark enough tree cells for 3 timber, and let the work finish | Three real timber items reach the stockpile; clearing spends no water |
| 22 | Charter → **Commission a building** → larder shed (Key `larder`; 4 drams and 3 timber available) | Stores −4 and stockpile −3 timber; scaffolding appears nearby; chronicle line |
| 23 | Wait 1200 ticks (or explore and return) | Scaffold becomes the larder shed; completion message + chronicle |
| 24 | At Steading+, commission the cistern court (Key `cistern`; 16 drams) and wait 3600 ticks | Stored capacity +256 when done; completion message + chronicle |
| 25 | Commission a communal bunk; watch settlers at night | A settler eventually sleeps in it (vanilla bed behavior) |
| 25a | On first reaching Steading | "A settler has taken up the trade" — the first stall opens; trade with them shows tier-1 stock |
| 25c | Charter → Commission | The design list is the merged, data-driven view of every loaded `<kingdombuildings>` stream. Base designs appear without a pinned count; third-party streams extend or override them by key automatically |
| 25d | Load one tiny third-party building stream with `Schema="1"`, then the same stream with the `Schema` attribute absent | Both versions contribute the entry. The absent form is the explicit pre-versioning compatibility path; neither logs a schema fault |
| 25e | Load otherwise-valid fixture streams with `Schema="2"`, `Schema="01"`, and `Schema="future"`; put a unique valid entry before a malformed entry in each | Each whole stream is ignored and logs one registry/schema fault. The early valid entry never appears: unsupported input cannot half-register |
| 25b | Browse vanilla tier-1 merchants elsewhere | The founder's basin occasionally appears for sale (8% per restock) — the legitimate acquisition path |

## Pass 6 — Raids and tribute

| Step | Action | Expect |
|---|---|---|
| 26 | Reach Steading; `kingdom:standing Snapjaws:-300`; leave and re-enter the claimed zone | **No raid warning appears.** Raw negative standing is not a grievance and cannot start a raid |
| 26a | In a claimed zone, enter `kingdom:raid` once | The wish explicitly mints one snapjaw **test grievance**. This first notice is only rumor: it freezes stable source, authored cause, target zone, 6-dram remedy, up-to-24-dram raid stake, and force profile, but has no delivered demand or due tick |
| 26b | Enter `kingdom:raid` again before answering; inspect inventory | It reports the same incident, delivers one faction-authored physical demand, and still shows no running due tick. No duplicate grievance or demand object exists |
| 26c | Use the demand's **Read and acknowledge** inventory action | Only now does the incident become Warned and show a future due tick. Escaping the answer menu chooses nothing and spends no governance action |
| 26d | Carry the acknowledged demand away from the seat, drop/destroy it, and take a wake; then take one more wake | Channel loss pauses the clock without choosing a default or removing an answer. One higher-revision physical replacement is delivered; read it to resume only the remaining answer window |
| 26e | After resolving the test incident, set guarded gates at Steading and take ordinary wakes | One natural snapjaw grievance may arise from denying local salt-road passage. It has a stable settlement source and cannot repeat after consumption; raw standing remains irrelevant |
| 27 | Through the carried live demand, **Pay tribute** (6 drams) while away from the seat; repeat locally using an exactly provable dedicated store | Remote tribute removes exactly 6 drams of pure water from loose, unsealed vessels directly carried by the player. Local tribute removes exactly 6 from dedicated stores. Each incident, chronicle, and ledger agree; standing does not change |
| 27a | Mint another `kingdom:raid`; set `kingdom:standing Snapjaws:250`; Charter → **Answer a threat** → **Send an envoy** | No water changes hands. The incident resolves with an obligation; the next explicit snapjaw grievance demands 12 drams, then discharges that obligation |
| 28 | Resolve any open test incident; enter `kingdom:raid`; Charter → **Answer a threat** → **Refuse and meet the warband**; remain here until its displayed due tick | The frozen profile spawns marked snapjaws only on passable zone-edge cells that have a path to the frozen named store. Spawn itself takes **no water** |
| 28a | Watch the marked raiders approach; compare dedicated water before contact and after one reaches the named store | Water stays unchanged until physical adjacency. At contact, at most the frozen raid stake (up to 24 drams, reduced by proved defence) leaves that exact fresh-water store and the incident records the proved amount |
| 28b | Repeat the fight and defeat every marked raider before any reaches the store | The death of the final marked raider resolves **RaidersDefeated** with zero plunder; raid bodies grant no XP |
| 28c | After a physically proved store plunder, Charter → raid recovery → **Accept** | One plain base-game quest appears with no custom manager, reward, reputation, or XP. The settlement watch is exactly one defence point weaker until recovery resolves |
| 28d | Defeat every surviving body marked for that exact raid, return to the exact seat, then Charter → raid recovery → **Turn in** | The quest becomes ready only after the exact band is gone and completes only at explicit seat turn-in. The one-point wound clears; save/reload neither duplicates nor auto-completes it |
| 28e | On a later proved plunder, decline recovery, then suffer another proved plunder | Decline leaves one persistent one-point scar with no expiry. Later losses do not compound it or mint a second concurrent recovery offer |
| 29 | After either outcome: `kingdom:chronicle`; save/reload and enter `kingdom:raid` | Chronicle and raid popup agree on the retired answer. Reload does not recreate the consumed source or duplicate the incident |

## Pass 6b — Fortification

Defence is a perimeter, not a damage number: it decides how much of the band gets past the wall
at all, and how much the ones who do carry off. Raids resolve where you can see them, so every
step here is done standing in the settlement.

| Step | Action | Expect |
|---|---|---|
| 29a | With no defences built, `kingdom:raid`; Charter → Answer a threat → **Refuse and meet the warband**; wait to the frozen due tick | The whole surviving band enters from reachable edge cells. Nothing is taken until a marked raider physically reaches the named store |
| 29b | Charter → Commission → **thorn palisade**; wait out the build; `kingdom:status` | Status shows the settlement's defence at 3, and the palisade stands on an edge of the zone that **faces ground you do not hold** — not in the middle of your own settlement |
| 29b1 | Raise several walls, then claim the zone that edge faces | The old line is not torn down or moved. It is an inner wall now, and the frontier has moved outward — new walls go on the new outer edge |
| 29b2 | Claim on every side until a zone is fully surrounded by your own ground, then commission a wall there | It has no frontier left, so the wall is raised where you stand instead. A zone in the middle of a city has no outward edge to defend |
| 29c | Resolve 29a; `kingdom:raid`; Charter → Answer a threat → **Muster named defensive works**; note every named work and assigned resident; save, cold-load, then wait to the due tick | The same work IDs, posts, resident semantic IDs, bound bodies, and defence scores are rechecked at arrival. No resident is reserved for two works. The watch turns back some of the band before entry; fewer raiders spawn than in 29a |
| 29c1 | Repeat 29c, but dismiss, kill, unbind, or move one reserved resident off the frozen work before the due tick | Muster fails safely instead of substituting another resident or retaining stale defence. No invisible fortification benefit applies |
| 29c2 | Repeat 29c, but remove, rebuild, or replace one frozen defensive work before the due tick | A replacement with the same display name does not satisfy the frozen work ID. Muster fails safely; raid remains physically resolvable |
| 29c3 | Load an old save whose Fortified incident predates exact work/crew reservations | The incident reopens at confrontation-ready with all four answers; migration invents no crew, applies no penalty, and does not silently resolve |
| 29d | Commission a **watchtower** with nobody spare to man it; `kingdom:status` | Defence unchanged: an unmanned tower is a platform |
| 29e | Grow the population until the watch is crewed; `kingdom:status` | Defence rises as the crew fills; a half crew gives half the tower's defence |
| 29f | Reach defence 12+ (palisade + crewed watchtower + rampart at Village); `kingdom:raid`; choose **Muster named defensive works** | If the frozen, revalidated works fully repel this band: "They break on the walls. The watch holds." Nothing spawns or is taken; chronicle records the exact result |
| 29i | At high defence, repeat: resolve, `kingdom:raid`, choose **Muster named defensive works**, wait to due | A band not fully repelled still sends survivors over the wall; defence never converts an entered body into invisible plunder |
| 29j | `kingdom:raid`, give no answer, leave past the displayed due tick, then return to the named zone | **Nothing resolves while absent.** The incident becomes confrontation-ready, takes nothing, and opens a fresh displayed answer window; it remains the same incident/source |

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
| 42 | Read an arrival line | It names the cause: "Word of the larder shed raised at Kavvat reached the hills — a settler has come." Founding, stage-ups, commissions, caravans and tribute all set the cause |
| 43 | Strike a charter, then stay away for several caravan intervals | Missed deliveries **bank**: "3 caravans of the villagers of Joppa came under charter: 18 drams." A caravan that came while the gate was shut is news, not a loss — and the banking cap is a cap on how much one homecoming can hand over at once, never a clock that forgives the rest |
| 44 | Return after a short walk (under a day) | No news line — the homecoming is for absences, not for stepping outside. The Charter entry still opens and says nothing has happened |
| 45 | Charter → Dedicate a vessel or larder → **Dedicate everything here** | All undedicated vessels join the stores in one action, up to the cap |
| 45a | Stand beside a chest or footlocker with food in it and dedicate it as a larder | It is marked a larder of the settlement. Nothing moves and nothing is taken — dedication is a mark, not a transfer |
| 45b | `kingdom:dump` | The pantry reads the food in dedicated larders only, as a count and a tier (Empty / Scant / Modest / Ample). Food in your own undedicated pack is never counted |
| 45c | Release the larder | The count drops back. What was inside is untouched |
| 45d | Commission a **larder shed** (Key `larder`; Charter → Commission), then put food in it | It counts as a larder without needing a chest of your own, and holds 64 servings |
| 45e | With the larder Scant or better, Charter → **Share a meal from the larder** | It **asks first**, naming what the meal will take and what the larders hold. Answer yes and the settlement eats: food is spent from the dedicated larders only, a settler from the roll speaks, and the chronicle records it. Word travels — the meal becomes the settlement's deed, so it draws settlers the way any deed does |
| 45f | With an empty larder, Charter → Share a meal | Refused plainly, and nothing is lost. The meal itself is still a bonus for engaging and never a penalty for abstaining |
| 45g | Check your own pack and any undedicated container after a meal | Untouched. Only dedicated food is ever spent |
| 45h | Found a camp, dedicate nothing, commission nothing, and let days pass | It **does not starve**. Free hands forage up to four rations a day off the ground and eat them hand to mouth, which is exactly a camp of four — the food mirror of "half a camp on the water detail covers a camp's drinking". Put a third hand on the water detail and it still forages four: the wild has a ceiling, not a pool |
| 45i | Grow past a camp with no field standing | Foraging stays at four a day however many people there are, so the shortfall is real and the ledger says so: "The larders are empty. Settlers will leave if the fields do not feed them." Commission a kitchen garden or a field and it stops |
| 45j | Commission a **granary**, then `kingdom:status` | It dedicates itself — the same law that auto-flags a commissioned larder shed. `Larder:` reads its capacity (288) in the denominator, and the `Fields:` line names what the works make against what the people eat |
| 45k | Let the fields make more than the larders can hold | Said once, by name, and never again until there is room: "The larders of X are full, and N of the harvest was left in the field." The homecoming ledger carries the same figure |
| 45l | Let a settlement be dry **and** hungry across the same homecoming | **One** settler leaves, not two, and the line names both reasons: "left X for water and bread both, and this place had neither." Both `(withered)` and `(famished)` may show on the Status stage line — a mark is a state, a departure is a cost, and only the cost is capped |
| 45m | Damage a granary (a raid, or a lost rung) and leave it unmended | It spoils what it holds on world days, exactly as a holed cistern leaks drams: announced once by name, unsaid when it is mended. It can never be the reason the settlement goes hungry — spoilage is drawn after the day's eating, never before |
| 46 | Charter → Status | The ledger's effects are visible: stores, shop tier, idle/shorthanded works, and the next-need line |

## Pass 10 — Names, policy, and answering a threat

| Step | Action | Expect |
|---|---|---|
| 47 | Let settlers arrive, then Charter → **The roll of settlers** | Real generated names, each with origin and the date they came. Look at a settler in the world — it carries that name |
| 48 | Charter → **Standing policy** → toggle gates to guarded, then stores to thrift | Chronicle lines; arrival and upkeep policy effects change. Neither toggle fabricates, cancels, or silently resolves a raid grievance |
| 49r | Set `kingdom:standing Snapjaws:-300` and verify silence; then enter `kingdom:raid`; Charter → **Answer a threat** | The explicit wish—not standing—mints the cause. Four answers appear: exact tribute, envoy (standing-gated), physical fight, or named-work muster. Title freezes cause, target, due tick, 6-dram remedy, and up-to-24-dram store stake |
| 50x | Choose **Refuse and meet the warband**; wait to the frozen due tick | The committed answer survives save/reload. Reachable-edge bodies use the frozen faction/tier roster, take nothing on spawn, and can plunder only after physical contact with the frozen store |
| 50x1 | On a fresh explicit grievance choose **Muster named defensive works**; remove or uncrew one named work before due | The exact commitment is revalidated. Missing/uncrewed proof contributes nothing; surviving works reduce or fully repel entry without inventing invisible damage or plunder |
| 51r | Raise Snapjaw standing to 250+, mint a fresh `kingdom:raid`, choose **Send an envoy** | No water or standing is spent. The incident resolves with one explicit obligation; the next authored snapjaw grievance demands 12 drams and discharges it instead of recursively minting raids |
| 52r | Mint `kingdom:raid`, give no answer, leave past the displayed due tick, then return | **No ambush at the gate, and no loss in the dark.** The same incident becomes confrontation-ready and receives one fresh answer window; source, cause, faction, target, objective, and stake remain frozen |

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
| 48w | With only one city, Charter → **Send a water manifest** | Refused: there is nowhere to send it |
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
| 51a | Walk well away from your dedicated vessels and commission a larder shed | As a storage design it rises **beside the stores**, not where you stand. The message names the ground it chose |
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
| 54b | Settle a zealous creed in one city and its opposite in the other | Dissent begins and follows world-days whether you attend or travel. Its warning ladder remains legible before the breaking brink |
| 54c | Leave for a season with dissent running, then return to either city | It has accrued for every elapsed world-day and clamps at the breaking brink. One warning names the actual crossing day and starts the full nine-world-day response window; absence before that warning cannot make the city leave |
| 54d | Pour a rite of shared water; call a shared meal | Dissent gives ground, slowly. These are levers, not decoration |
| 54e | Charter → declare the realm's creed | **It asks first**, naming what it will cost: every later settler leans that way, and those who stand against them hold it against the whole realm, everywhere |
| 54f | Let dissent run to the end | The unhappier city leaves — keeping its ground, its people, its buildings, its stores and its book. Nothing burns. Both registers record the day and **disagree about it** |
| 54g | Walk onto the ground of a city that left, having removed the cause | You may ask them back. Try it without removing the cause and they say so to your face |

## Pass 17 — Plots, materials, and the catalogue

| Step | Action | Expect |
|---|---|---|
| 55 | Commission a plot-sized design (a tent, a timber hut) | The settlement reserves a **typed lot**, not a generic rectangle: staked → cleared → framed → walled → done, watchable. The reserved typed lot is occupied by the design's exact authored map and authored tier; its authored entrance faces the declared frontage, authored fixtures occupy their named cells, and its material palette supplies the exact surfaces. There is never a row-major furnishing fallback. The surveyor's plan on the stakes reads as the finished building's description, framed as intention |
| 55a | Watch the clearing stage | Clearing **earns**: trees give timber, rock gives stone, ruin walls give scrap — carried to a stockpile you dedicated. No stockpile, and it says so once |
| 55b | Try to stake a plot over your own dropped gear, or open water | Refused, naming the cell and the thing standing on it. Nothing player-placed is ever cleared |
| 55c | Charter → **Clear ground** (`q`) | Spare hands work the rect down over days; the yield is itemised in the ledger |
| 55d | Charter → **Take down a building** (`z`) on something the settlement built | Condemned: crew works it off, half its material returns to the stockpiles, the plot frees, no water refunds, chronicled both ways |
| 55e | Check building costs in the commission list | Water **and** materials. A design naming no materials costs water alone |
| 55f | Reach Steading, then Town | M then L plots unlock — the city literally builds bigger as it grows. XL waits for City |
| 55f1 | At Camp, Steading, Town, and City, preview every size shown for one S design; repeat with one M and one L design | Every displayed S/M/L/XL choice resolves before payment to a distinct exact-size preview. Added cells read as yard, the authored building remains against its frontage, and confirm places the byte-identical preview. No shown choice ends in a missing-map refusal |
| 55f2 | Put durable road evidence against each side of a clear lot and preview a road-facing design at every offered size | Every accepted pose has each authored public entrance on the lot edge with road immediately outside it. Removing all road evidence refuses before debit; no pose reaches inward across yard or chooses a nearest road |
| 55f3 | In a controlled city, reconcile one Hindren-culture/hindren-species citizen while no Hindren creed is dominant; preview hut, house, and fine house | Floral Hindren topology appears from live culture/species fact alone. `kingdom:dump` need not report a Hindren creed. Remove the final matching body, reconcile, and preview a new work: fallback returns; anything already frozen remains unchanged |
| 55f4 | Make Kyakukya the dominant seat creed in that non-broad city, then preview the same housing | Kyakukya's market/courtyard plan wins by declared priority. Culture was not silently stored as creed, and neither identity change repaints a standing snapshot |
| 55f5 | Reconcile a robot citizen and preview/raise a charging post at every offered size | Two exterior approaches reach the crank/queue lanes and the paired robot-service bay; palette remains the paid salvage frame and dirt floor. Selection adds no charge, worked metal, liquid, or loose object. Save/reload preserves exact map and anchors |
| 55f6 | Reconcile a non-flying aquatic citizen and preview/raise L and XL reservoirs | Several exterior approaches join one continuous water-edge circulation loop and reach main/tap. No liquid or free drams appear—the reservoir's real behavior/stores remain sole water authority. A flying aquatic does not select this overlay |
| 55f7 | Reconcile a vanilla-`Gigantic` citizen and preview/raise M/L/XL house, housecourt, and fine house | Paired portals and a clear two-by-two turn are visible and traversable in every pose; sleep/storage/hearth/table requirements still work. S shelters honestly remain fallback rather than pretending a 5x4 lot contains broad turning room |
| 55f8 | With a developer fixture citizen carrying `Genotype="True Kin"`, preview/raise the becoming annexe | Two waiting wings converge on the lineage-scan axis before restraint, template, chair, power, and sealed-roll stations. Ordinary genotype-empty NPC cities use fallback. Capture native-scale screenshots of all 55f3–55f8 cases; automation proves topology, not visual quality |
| 55g | Visit and claim the zone one stratum down, return to the exact zone above, then try to commission an ordinary deep building before cutting a delve | Refused by name: claimed rock is owned but unreachable. No water/material is reserved. A crew can climb a cave stair; it cannot carry a civic work down one |
| 55g1 | At Steading+, connect a road to a clear M lot in the head zone and commission **delve** | Preview shows one authored Down landing, never an Up beside it. If the exact cell below contains a wall, liquid, creature, dropped item, container, existing stair, or other state, commission refuses before debit and names the lower landing |
| 55g2 | Finish the delve, then wish `kingdom:delvelink` in its head zone | `State: canonical`, `Physical proof: STANDS`; one root ID, one Down ID, one Up ID, and one shared x,y are printed. Descend: player arrives on the printed x,y in the claimed foot zone. Ascend: player returns to the same x,y in the head |
| 55g3 | Save during delve construction, quit fully, reload, and finish; then save at the foot, quit/reload, climb both ways | Exactly one Down and one Up after every reload. No same-map stair pair, duplicate endpoint, newly generated lower zone, moved landing, or boolean-only reach. An underground plot now uses its authored carved map: retained rock, paid supports/fixtures, and no generic shell |
| 55g4 | Empty both landing cells, order the delve struck, save/quit/reload once during removal and once after | Strike removes only the owned Down/Up pair and their two native connection records. `kingdom:delvelink HEAD_ZONE_ID` reports `struck/tombstoned` and `Physical proof: FAILS`; lower rock returns to owned-but-unreachable. Floors, nearby objects, containers, liquids, and third-party property remain untouched |
| 55h | Raise a building while standing there — a **house or a field**, not only a wall | The raising ceremony: crew gathers, water is shared, the chronicle names those present. Raised while away, the homecoming tells it plainly. Every design closes this way, whether it rose on a scaffold or as a staged plot |
| 55h2 | Stake a **plan** for a plot-sized design and let the settlement realise it | When it rises, the chronicle's raising line **quotes the plan** staked for it. A design commissioned directly has no plan and is chronicled without one |
| 55i | Order a house | It comes **furnished** — bunks, torchpost, and hookah occupy the exact fixture cells authored in that tier's map. Nobody commissions a hookah one at a time, and furnishings are never spread row-major |
| 55j | Check each completed charter delivery over several visits; when a **pattern-book** appears, inspect all choices, then Escape once | The offer contains at most three exact foreign designs. Escape declines this delivery without changing keeper knowledge, spending stock, or hiding any base-catalogue design |
| 55j1 | On a later offer, choose one pattern and inspect Charter → Teach/what the keepers know | That exact labelled pattern is merged once into the **currently seated delivery city's stored keeper roster**. Another city does not learn it; an already-known choice does not duplicate it |
| 55j2 | Save/quit/reload after a declined delivery and after a learned delivery; complete further charter visits | Neither offer rerolls or repeats. Learned knowledge, chronicle, and message dispositions remain settled; catalogue edits in a newer build cannot change the label/key frozen by an older open receipt |
| 55k | Appoint an office holder; read their description | One virtue, one flaw, and one or two stated **tastes**. Met tastes shade the settlement up; unmet means their default, never a penalty |
| 55k2 | Charter → Status after an office is filled | The level's own line carries **"+N for what its notable finds here"**, and the level is that much higher. Never negative, and never past half the binding level |

## Pass 17a — Named raising gangs and readable work state

Use native tiles first, then repeat the glyph checks in text mode. `kingdom:visuallegend` prints the
mod/Qud versions, canonical state rows, and the expected legend hash. Keep that popup in the same
capture as the map or copy it into the playtest receipt; automation checks drift, not readability.

| Step | Action | Expect |
|---|---|---|
| 55v1 | With at least two otherwise-free settlers, commission two plot/scaffold works before the first finishes | Only the oldest raising receives the bounded named gang. Those exact settlers walk to passable ground at or beside its frame; the newer work alternates to `=` and says once that the same hands cannot stand at two frames. Its labour stage does not rise while queued |
| 55v2 | Put all available hands on water or running works, let one construction interval pass, then release one hand | The selected frame alternates to `_`, says its real hands shortfall once, and banks no idle interval. After release it changes to `/`, names any real `CrewNeeds` shortfall, and advances only at its stamped headcount/capability/identity pace |
| 55v3 | Save and cold-load with both works active; leave the zone for a meaningful span and return; then let the oldest finish | Selection remains oldest-by-start/ground/id, no settler or state-reader object duplicates, and absence produces no invisible second gang. If elapsed labour completes the old frame on return, homecoming tells it instead of staging a fake ceremony. Otherwise the same named bodies are visible. The gang next takes the queued work; released builders walk to a normal post or hearth, never teleport |
| 55v4 | Improve a standing plotted work through one authored tier while watching | The old authored tier changes through its real staged construction, carries the `/`, `_`, or `=` sign matching assignment, and finishes as the exact previewed successor tier. The sign disappears on completion; no calendar-only tier jump or permanent scaffold remains |
| 55v5 | Produce real battered, half-ruined, and ruined works through damage/subsidence; begin mending one and condemn another | The three wear rungs alternate to distinct `\`, `%`, and `#` rubble silhouettes; active mending is `+`/toolbox and active salvage is `x`/broken-arrow. Examine text names the same state. Ruin remains owned, mendable or salvageable, and no cue replaces the building, mints rubble loot, or clears protected contents |
| 55v6 | Create one unstaffed work, one genuinely shorthanded work, and one powered sink in a real brownout; then restore hands/power | Idle `-`, shorthanded `?`, and dark `o`/power-cut are distinguishable without color or Status. Each clears immediately when its exact `KingdomEffectiveness`/`KingdomBrownout` fact recovers; an ever-powered or merely inactive machine does not fake a brownout |
| 55v7 | Let the city become withered, famished, and then both; inspect its exact heart marker after each state and after recovery | The heart alone shows `;`, `:`, then `!`, with matching examine labels. Ordinary works do not inherit a settlement-wide distress wallpaper. Recovery removes each sign from the same object |
| 55v8 | Run `kingdom:visualaudit`, capture the actual map and popup, save/quit/reload, and repeat | Receipt names mod version, Qud core version, zone, legend SHA-256, ground coordinates, work name, resolved state, glyph, and vanilla tile. Rows stay ground-order deterministic. Record a human pass/fail for native tiles and text mode; no automated green substitutes |

## Pass 18 — The posted price, worn ground, yard trades, and guests

| Step | Action | Expect |
|---|---|---|
| 56 | Charter → posted notices (`1`) → post a price to clear a rect | A notice stands at the heart. Nothing is escrowed. Someone takes it on their own judgment on a later pass — attempts, successes and refusals are chronicled **by name**, refusals citing the refuser's own flaw |
| 56a | Post a price the stores cannot cover | It stands, with the debt written down and named. Paid the day the work finishes, not before. No expiry, no nag; take it down free |
| 57 | Walk your settlement for several visits, then leave it to its people for a season | Routes people actually walk wear in: grass → trodden earth → true path. A season of settlers walking lays a season's worth — the ground does not wait for you. Ground **nobody walks on** stays grass however long you are gone, because what wears a path is feet and not the calendar |
| 57a | Charter → ground work (`q`) → pave a worn path | Asked first, priced per cell, paid in stone from the stockpiles. Refused by name with no stone, no worn ground, or nobody free |
| 57b | Check cells under your dropped gear | Never worn, never paved. Wearing only touches open ground |
| 58 | Charter → your works (`y`) → give a small house a yard trade | Vine lattice, hide rack, dye vat, or vellum press. The house's description and the roll of settlers say the household took up the trade. Letting it go is free |
| 58a | Strike a 6-dram charter; run one due delivery with one exact dye-vat household, then five; repeat after releasing one vat and after save/cold-load just before due | The receipt delivers 7 drams per due cycle with one and 10 with five: only four exact households count. Releasing one of the four counted vats removes one dram from the **next** prepared delivery, never an already-open one. A missing/moved/mismatched/duplicate fixture adds nothing; reload neither reprices nor repeats a delivery. Chronicle and ledger still report the exact total actually placed |
| 59 | Return within a notable's 2-day patience of their arrival, and read the roll | **Guests**: the notable is still standing at the gate, logged with one hook — a ruin, a machine, a debt in a named village. Lodge them in a bed of the right tier and they settle with a trade; ignore them and they leave a letter and the hook becomes a rumor — never lost |
| 59a | Leave long enough that a notable's 2 days of patience (a third of a day, for an ordinary traveller) has run out before you return | Nobody is standing at the gate — the roll instead reads one dated ledger note, "N notables came to the gate while you were away and found no bed offered", naming how many and how long ago the last of them stood there. Their hooks are rumor now, same as an ignored one, never lost |
| 59b | Meet a legendary dromad trader; try a manor, an ordinary large home, an occupied fine house, and a vacant fine house while shops are below/at tier 3 | Only the exact vacant fine house at M or larger qualifies, and only once a staffed tier-3 shop exists. On success the named trader binds to that exact LotId, becomes a real merchant with current-tier wares, and remains bound after save/reload. No generic large roof or settlement-wide spare-bed count substitutes |
| 59c | With one ordinary guest and then one notable open, save/quit/reload before arrival placement, after placement, before/after unattended removal, and after the dated sinks but before the next clock | The same serialized PlainGuest/NotableGuest receipt resumes. One body appears or leaves, dedicated water changes once, Chronicle/ledger/message/guestbook each publish at most once, the next due tick advances once, and the operation retires. Leave the zone for several intervals and repeat: passages are one dated aggregate plus at most one still-patient body, never a return-time queue |
| 59d | While 59c or legendary lodging is open, disable its option, exchange the seated city, save/reload, return, then re-enable | Open work reconciles under its owning settlement while disabled; the other seat cannot spend its receipt. Disabled absence creates no backlog, and re-enable restamps a fresh interval. For the trader, the frozen body, exact fine-house root/LotId, shop tier, roster row, merchant inventory function, water debit, and guestbook line survive without duplication |
| 60 | Buy a carry-sign from a merchant; plant it on a pile you own out in the world | Confirms exactly what it will take, then porters haul it home over distance-scaled days, one haul in flight. It lands in the stockpiles and is chronicled. If a raid warning or live raiders stand at the destination, the exact haul waits without loss and arrives on the first later safe settlement pass |

## Pass 19 — Layered catalogues, footprints, sockets, and the trigger law

| Step | Action | Expect |
|---|---|---|
| 61 | Ship a tiny second mod file re-declaring `<building Key="tent">` with just a new cost and a new skin | The tent keeps its place in the list, costs the new figure, offers the new skin. Attributes the file omitted survive. A standing tent is untouched — what was spent and cut into the ground never follows a mod update; what the settlement re-reads (name, gates, skins) does |
| 61a | Blank an attribute (`Contents=""`) vs omitting it | Blank erases; omitted keeps. The modder contract in MODDING.md says so |
| 62 | Commission a design whose authored tier map occupies less than its reserved lot | The exact authored entrance fronts the declared heart or road edge and the yard lies behind. Yard = reserved lot minus authored map, recomputed from the next exact tier as it grows |
| 62a | Let a tier grow onto ground a yard trade occupies | The improvement refuses **by name** — nothing in a yard ever comes down on its own |
| 62b | Author a tier whose footprint exceeds its plot | Refused at load with both spans named; refused again at improvement: "wants more ground than this plot holds — strike and stake larger, or leave it" |
| 63 | In a fresh common city, raise a tent; Charter → **Change what a plot is** (`2`) → mud-brick hut. Repeat tent→timber hut in a verdant/fungal/gyre city and tent→block hut in an eater ruin; repeat with tent-row and the matching yard/court | Only the style-applicable declared route appears. The row and confirmation quote only its water, materials, and ticks, all below strike plus fresh target. The popup renders the exact frozen target map and retained/removed/added cell counts. Confirm changes through one durable improvement handover: LotId, rect, facing/pose, founder marks, stable wear/hold state, contents, and retained canvas bed/storage fabric stay exact. No strike or salvage occurs |
| 63aa | Inspect the same menu for a target with no route; then remove a test declaration, reverse its endpoints without declaring the reverse route, or leave a wear/leak/repair receipt active and call the direct transition seam | No false `[change]` row is listed. Direct/stale attempts refuse before debit, naming the absent transition or unsettled protected-state receipt. No full-price fallback silently substitutes for it |
| 63ab | Choose a different type, or a design too large for the actual staked rect; inspect the preview, then save/reload once after payment and once after strike | Preview names the full fresh bill, newly frozen valid site and new LotId, and renders that exact target map. The predecessor is fully struck; the successor rises once at the frozen site with the previewed architecture. The old ground is bare, and reload duplicates neither lot, charge, salvage, nor interruption |
| 63ac | Finish taking down one plot work, choose **Build on this cleared plot**, inspect one target, then confirm | Before payment, the popup renders the exact production target for that socket. Confirm reproves that same frozen variant/pose/cells, charges once, and raises the byte-identical target on the existing cleared rect; no second resolver chooses another variant |
| 63ad | At each preview in 63, 63ab, and 63ac, cancel once with controller and once with keyboard/Escape; inspect stores, plot/socket, receipts, Chronicle, ledger, and map | Cancel is mutation-free. No water/material debit, strike, construction/improvement receipt, LotId change, log line, or cell/object mutation occurs. Reopen under unchanged facts and the same exact target is offered |
| 63ae | From fixture copies, interrupt a paid commission, plotted defensive plan, cleared-socket build, and improvement before projection; save/quit, then alter or remove the target catalogue row and change the founder's tinkering skills before cold-load | Current v4 receipts retain the exact paid plot/frontier classification and final defence shown at commitment; retry never consults the new row or skills, changes the cap charge, or pays/projects twice. An old unprojected v1-v3 affected receipt enters named inspection before another debit or destructive strike. An already-projected legacy scaffold resumes only when its exact durable marks agree |
| 63a | Charter → **Give a building a new look** (`3`) | Any registered skin — including one a mod added after the building was raised — for a tenth of build cost. No output change |
| 64 | Grow a house until its upgrade is earned, with residents and spare tolerable beds | It upgrades by itself: residents lodge to their own standard during the rebuild. An ordinary settler takes a bunk; the notable in the fine house will not take a tent — and the upgrade **waits** until lodging they'd accept exists |
| 64a | Earn an upgrade on a water work the city leans on; force it from the works screen, read the popup, cancel once, then repeat and confirm | **Held, not taken**: "ready to improve, and held — the city leans on it." The force popup renders the exact frozen successor map and actual cell delta, materials, water, ticks, assigned crew, output outage, and reserve margin before mutation. Cancel leaves stores/state/map unchanged. Confirm commits that same successor intent; completion matches the preview. Automatic unheld upgrades remain automatic and status names their target |
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
| 73a2 | Compare a grange with and without a real harvestry-skilled field hand, then compare a workshop with and without a real tinkering-skilled tinker | The matching specialist is assigned first and satisfies `skill.harvestry:1` or `skill.tinkering:1`; an unskilled body still works at the visible shortfall floor. Sprite or display name alone grants nothing |
| 73b | Check a high-craft design's price | Denominated partly in **vanilla tinkering bits**, drawn from the stockpiles — donate by putting scrap in. Great works may also want a rare find, and say so |
| 74 | Crew a demanding work with weak or unpractised hands | It builds **slower and says so once** — floors at a quarter pace, never stalls. The ablest or correctly skilled available hands go to demanding works first, deterministically |
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
| 78g | Save, reload, and read all three again | Causes, crossing ticks and **warning ticks** all survive. A per-settler brink is kept under the settler's own `KingdomResidentId`, in their city's book, so swapping seats never carries one to the wrong city |
| 78m | Warn a settler in one zone, then walk to a **second zone of the same city** and back | The same brink, the same anchor, the same days left. The window lives on the roll now, not on an object, so the zone the person is standing in has nothing to do with it |
| 78n | Warn a settler, then walk to your **other city** and stay away past the window | It fires on the day it ran out, dated to that day, exactly as 78c does. A city keeps its own roll and reckons it whether you are standing in it or not |
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
| 81d | **The other zone's stores moved while you were in this one.** With two zones held, fill the cistern in zone B, then live in zone A until A's own casks run dry and the day's bill cannot be paid out of them | The ledger says water came in from the city's other quarters, out of the oldest casks first — and A's casks now hold it. The city stopped going thirsty next to a full cistern in its own second parasang |
| 81e | Walk to zone B and open the cistern you drew from | It holds **exactly** what the book said was left: the drams that were carried to A are gone from B's vessel, taken from the oldest dedication first. Reload before walking over and repeat — the **same** vessel drained first |
| 81f | Between 81d and 81e, wish `kingdom:dump` and read the `[TAF] perf` lines in Player.log | `perf reckon` on every settlement pass. No line is prefixed `BUDGET`. See Pass 32 for how to read one |
| 81g | Pour a few drams out of a dedicated cask **by hand**, then leave the zone and come back | The ledger says the stores hold fewer drams than the books had and that the stores are right. Attributed and told — never silently repaired, and never a fault |

## Pass 27 — Underground honesty, and the gatehouse

| Step | Action | Expect |
|---|---|---|
| 82 | Claim ground a stratum down, Charter → Commission | A sky-wanting design (dew catchment, catchment bank, air-well court, air-well field, the condensing hall, sailvane) carries **[wants open sky]** in the list — the same tag every other blocked gate wears, so the catalogue never silently shortens |
| 82a | Try to commission one of them anyway | Refused by name: "The dew catchment wants weather, and there is none under the rock. Raise it under open sky." |
| 82a1 | Read what IS offered down there instead | The **weep-tap** and the **weep gallery** — the underground water lane, cut into a damp seam rather than hung under the sky — plus the salt-pan, which goes anywhere. A stratum down is a different water game, not a poorer one |
| 82b | Commission an **Open**-declared design underground instead (a tended plot, a salt-pan terrace) | It stays open: no walls, no door, no floor — a field cut into the rock, not a sealed chamber. It does not count as housing |
| 82c | Commission a design that declares no `Roof` (defaults Walled) underground | Still carved, exactly as before: the rock is the wall, no wall is raised, clearing costs double, paid back in stone |
| 83 | Reach **Town**, Charter → Commission → **gatehouse** | It rises on the exact `HeartToGate` frontier endpoint, not a random wall cell: a 3×3 road-aligned guard work with four sandstone walls, two usable timber watch benches, and an open three-cell centerline through its vanilla gate |
| 83a | Before commissioning, put the founder, a citizen, a loose item, a liquid, or another work on each kind of footprint cell in turn; record water/materials before and after | Each attempt refuses with the exact blocked coordinate and blocker **before any debit**. Nothing is cleared, moved, destroyed, or displaced. Removing the blocker permits the same deterministic footprint |
| 83b | Walk the founder through it in both directions; send a citizen and a porter across the same frontier route | All three traverse the outside approach, gate, guard throat, and inward approach. Walls and benches never occupy the centerline, and the gatehouse cannot seal the settlement's road graph |
| 83c | Close the gate with vanilla interaction, save/quit/reload, inspect it, then open and repeat | Closed stays closed and blocks until opened; open stays open and passes. Vanilla Door smart-use, render, sound, and state survive reload; the six owned guard pieces neither duplicate nor regenerate |
| 83d | Strike it, save/reload during the work and again after completion, then commission another | The frozen receipt removes the exact four stone walls, two benches, and Door root once. It leaves **no cleared plot/socket** because the gatehouse is a typed network, not a stakeable plot. The replacement returns to the same road endpoint and orientation |

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

## Pass 29 — Meals and industry

The food lane's other end. What the settlement grows now becomes a **meal with a name**, and the
mill is a real machine that eats the harvest and gives back something that keeps. As with the
water lane, the point is that what you can watch and what the ledger counts are the same thing.

| Step | Action | Expect |
|---|---|---|
| 89 | Found a realm, then run `kingdom:dump` | The realm has a **dish** — a name in Qud's own register, derived from your people and your ground: `starapple stew` on ordinary ground with nobody's creed dominant, `vinewafer matz` in a marsh once Joppa's people are the majority |
| 89a | Water-ritual with any citizen of your own realm | The dish is offered for reputation, in vanilla's own sentence: *"Would you teach me to cook &lt;realm&gt;'s favorite dish?"* Accepting adds a real recipe note to your journal |
| 89b | Let the roll drift until a different creed dominates, then walk back in | The kitchens change their minds. One line in the ledger and one in the chronicle, said once; the dish's name changes, and the old recipe you already learned is still yours |
| 89c | Reach **Village** and commission the **settlement oven** (it upgrades from the communal fire) | A real vanilla `Oven`. Walk up and cook at it: **Eat &lt;your dish&gt;** is at the top of the menu, alongside whip-up, cook-from-recipe, preserve, and the nostrum treatments — all of it vanilla's, none of it re-implemented |
| 89d | Cook at the plain **communal fire** instead | Every cooking action still works. It always did — the fire's blueprint has been vanilla's `Campfire` since it shipped, and what changed this wave is that the settlement *counts* it as a kitchen |
| 91 | Put a stack of your settlement's **preserved staple** in a dedicated larder (vinewafer sheaves, starapple jam, pickled mushrooms…), then leave for a few days | The ration draw reaches for the staple **first**. `kingdom:status` names the day: the settlement ate its own dish, and the ledger says so with the dish by name |
| 91a | Read `kingdom:dump` on that visit | `MealShade` is 1 and the supported level is one settler higher. Come back a day later with no staple left and it is 0 again — a settlement is well fed for the day it ate and no longer |
| 91b | At a Village or above, empty the larders completely and leave for a week | One line, once: the larders gave nothing and the settlement ate what it could find. Fill them and it is unsaid. Do the same at a **Camp** and nothing is said at all — living off the land is what a camp is |
| 92 | Raise a **grinding mill** at a Steading and `l`ook at it | A real millstone, not a glyph: it has a `Container` you can open and put things in, and it is a mechanical-power **consumer** |
| 92a | Put a handful of raw crops into the mill's own container and stand there with the mill on a powered gearbox line from a water wheel or crank mill | The stones turn (animated), and the crops become preserves at **vanilla's own per-crop numbers** — a vinewafer gives three sheaves, a starapple five jars, a plump mushroom ten pickles |
| 92b | Now leave the mill alone, keep crops in the **larders**, and leave for a week | Different stock, different clock: the settlement pass grinds two crops a day out of the larders into six staples, a net of four servings. `kingdom:status` reports it — *"N more won out of the millstone"* |
| 92c | Run the larders down to about one day's food and leave again | The mill grinds **nothing**. Industry never eats before the residents do: the day's rations are drawn first, and the mill may only touch what stands above one more day's bill |
| 92d | Compare the settlement's level with and without the mill standing | The mill's `food:4` is counted **once**. It is subtracted from the clocked daily make because it now delivers physically — a mill that fed the settlement twice would be the bug this step exists to catch |

## Pass 33 — One identity, at most one body

The anti-duplication substrate (`_notes/LIVING-CITY-ARCHITECTURE.md` §3.8, §8.3). W2 ships the
identity and the registry; **placement is W3**, so what a tester can falsify today is that the
book's account of who lives here matches the ground and never doubles anybody.

| Step | Action | Expect |
|---|---|---|
| 95 | Found a city, let several settlers arrive, then look at any settler in the wish debugger's object inspector | Each carries a `KingdomResidentId`, each one different, and the numbers only ever go up. Reload and they are the same numbers |
| 95a | Walk between the two zones of one city several times | Nobody is enrolled twice. The roll does not grow when you cross a line, and no log line reports a binding refusal |
| 95b | Grep the log for `city: check-in bindings` | **Nothing.** The line only appears when the registry has started answering one key with two bodies, and a line here is a bug report |
| 95c | Charm or recruit a settler and walk them out of the city. Come back and read the roll | They read **Abroad**: still on the roll, and the log says so by name. The person and everything you did to them is untouched — the model says where they are, it does not take them back |
| 95d | Walk them home again | They read **Resident** again, and the reason for having been away is gone rather than remembered |
| 95e | Found a second city and walk between the two several times | Ids never collide across cities: the counter is the realm's. One city's roll never appears on the other's |
| 95f | Save while standing in one city, reload, and read both rolls | Both survive with their bindings. A binding is realm state and is not carried by the seat swap — it is simply still there |
| 95g | Kill a settler yourself, then leave and come back | The row leaves the roll with the cause the city already tells it by, and the funeral reads exactly as it always did. Nothing about a death changed this wave except where the fact is kept |

## Pass 32 — What the city costs (the receipt)

The performance constitution (`_notes/LIVING-CITY-ARCHITECTURE.md` §0.0) is a table of budgets,
and this is how a tester falsifies it instead of taking the author's word for it. It grows with
every wave; these are the steps the city book's own wave can be read against.

**How to read a receipt.** Every line is one measurement of one lane:

```
[TAF] perf reckon label=Kavvat steps=1 rows=232 ms=0.14
```

`steps` is breakpoint passes, `rows` is row-visits (`steps × 2R`, where `R` is the live row count —
zones + works + settlers + clocks), `ms` is wall time. Four lanes write lines: `reckon` (one city,
one pass), `slice` (one micro-reckon, about one an in-game hour), `reify` (one turn's amortised
spend — there `rows` counts the units that were visible-cells-first, `thirds` is the weighted spend,
`units` is the figure judged against the budget of 8, and `owed=` is exact post-survey physical
demand, not one proxy unit per stock kind), and `thaw`
(one prefetch, timed and budgeted nowhere). **A count is a contract and a timing is
hardware**: on a slow machine `ms` will be larger and `steps`/`rows` will not. A figure that crossed
a budget is prefixed `BUDGET` and names the budget it broke — `[TAF] perf BUDGET reckon … over=8`.
**A `BUDGET` line in a playtest log is a bug report, not a note.**

| Step | Action | Expect |
|---|---|---|
| 90 | Found a city, hold two zones, raise works in both. Leave for a season. Come home and read the log | One `perf reckon` line for the pass. `ms` under 2. Nothing stutters as you walk in |
| 90u | On a dense City seat, make every semantic lane applicable at once: resident arrival/departure, two construction roots, plots/roads/crops, lab work, networks, visual damage, a porter, guest/faith/office work, and an upgrade. Enable diagnostics, cross the due boundary once, and retain the survey receipt plus `Player.log` | The due pass reports one maintained `KingdomSurvey` classification for that zone. Every later lane consumes its named index, and committed additions/removals are visible to later lanes in the same pass. There is no second whole-zone scan; only bounded exact-cell/object reproof appears. Repeat after save/cold-load and with duplicate resident/binding evidence: scan count stays one and ambiguity refuses without choosing by enumeration order |
| 90a | Do the same, but leave for **one day** | `steps` and `rows` are **identical to 90's**. Only the wall time differs. If they scale with the absence, a lane is charging per day and it is the lane that is wrong |
| 90b | Cross between your two zones several times in one session | One `perf reckon` line per crossing, each the same size. Nothing accumulates |
| 90g | Open the cistern and the larder in a zone the city drew from while you were elsewhere | The cistern holds **exactly** the book's remainder — not a full vessel and a ledger note. Reload and repeat: the **same** vessel drained first |
| 90m | Grep the log for `city: check-in audit` | `model=` minus `debt=` equals `ground=` for both water and food after an attended pass, and no line says `MISMATCH`. `debt=` is what this quarter's works have made that nobody has poured into a vessel yet; it is not drift, and it is why the two totals may legitimately differ. A mismatch is not a crash and is not repaired — it is named, and it is what you report |
| 90v | Watch the turns after a homecoming that owed something | `perf reify` lines, at most one a turn per zone, `units` never above 8, and the `owed=` figure in the label falling **monotonically** to zero. What you can **see** fills in first; the rest arrives behind you as you walk |
| 90n | Fill at least nine separately dedicated cisterns/larders, create a same-kind draw/landing across all of them, keep some visible, then return | First turn touches up to eight containers under the shared allowance. Every visible eligible container precedes every hidden one; within each half oldest dedication goes first. `owed=` counts actual remaining container units |
| 90o | Save after a partial 90n drain, reload, and continue; repeat once with one oldest container blocked/full/empty for the owed direction | Already-paid containers never move twice. Blocked quantity remains on the book and is named; later containers do not leapfrog a callback failure. Food harvest/loss totals change once by exact physical deltas |
| 90c | Walk out while `owed=` is still above zero, wander a week, come back | `owed=` resumes at the number it left at. Nothing lost, nothing landed twice, no harvest counted twice (**I1**) |
| 90d | **The porter.** Stand in a zone of your city that has a larder with room, while a farm in another of its zones finishes harvesting and the load goes on the road | A porter walks in **at the edge nearest that farm**, crosses to the larder beside you, puts the **real crop items** in it, says so once, and leaves by the edge they came in by. The homecoming report does **not** tell you about it afterwards (**I2**) |
| 90d2 | **Follow the porter.** Do 90d, then walk out of the zone *behind* them, following | You come out beside them, and they are just inside the entry edge, **a cell or two along** — not at the far wall and not standing on the boundary. Cross faster and you catch them at the edge; dawdle and they are further on. No pop, no teleport (**I5**) |
| 90d3 | Stand in the porter's way and keep them from the larder | They keep trying. Block them long enough — past twice the journey's projected length — and the job **fails**, is named in the register, and the crop is real items lying where they stood. Nothing is silently restored |
| 90e | Do 90d, then walk out mid-carry, wander until the model closes the job, and come back | The goods are in the city's books **once**. The porter is gone. No second load anywhere, and the ledger says the load you left on the road reached the store by another hand (**I3**) |
| 90h | Cross repeatedly between two zones of your own city | No reckoning at all inside vanilla's grace window. With the experimental neighbouring-zone prefetch checkbox off (`r_TAF_OptionPrefetch`, its default) there are **no** `perf thaw` lines at all; nothing is held, and a crossing costs the plain vanilla thaw it always did |
| 90w | Grep the log for `perf slice` | One line about every fifty ticks while you are in a city, with `steps` at 1 or 2 and never above 4. It appears whether you are walking, resting or standing still, and **not** during world-map travel — a founder on the world map is standing in no city zone and is owed no reification |
| 90p | Grep the log for `binding:` and `porter:` after a session with deliveries | Every `porter: job N carries` has exactly one closing line for the same N. No job id appears twice as open, and `city: check-in bindings` never appears at all |

**W6 — the works produce, and the carriers stop looking stupid.** These are the steps the
production wave adds. Before W6 a work in a zone you were not standing in made **nothing**; the
settlement's whole daily make was whatever the seated ground happened to carry.

| Step | Action | Expect |
|---|---|---|
| 90q | Raise an air-well field in zone B and dedicate a cistern there. Stand in zone A for several days, never entering B. Then walk into B and open its cistern | It has been filling the whole time, at exactly the drams the air-well field's `Carries` promises times the days you were away — not a full vessel, not an empty one, and not a ledger note. Amortised: the first turns after you walk in pour a vessel at a time (`perf reify`), and `owed=` falls to zero |
| 90q2 | Do 90q, then grep for `city: check-in audit` on entering B | No `MISMATCH`. While `owed=` is still above zero the line shows `debt=` covering the difference exactly. This is invariant **I1** with a rate running, which is the case that did not exist before W6 |
| 90q3 | Leave a whole four-zone city for a season and read the `perf reckon` line | `steps` no higher than `2 × zones + 1` and `rows` far under `64 × 2R`, however long you were gone. A season and a year cost the same reckoning; only the wall time differs |
| 90q4 | Fill one quarter's granary to the brim, then leave for a month | The granary is full and no fuller, and the log says `city: reconcile … spilled food=N`. The harvest that had nowhere to go was **left in the field**, exactly as it always was when you were standing there — not banked, not queued |
| 90r | Grep for `growth:` and `harvest:` lines on a pass | The settlement pass no longer credits the water works or the fields at all — those days are the model's now. The mill still grinds on its own stamp, still **after** the day's rations are drawn |
| 90j | Hold water in two quarters, one next door to where you are standing and one across the city, and let a shortfall pull | The **near** quarter is drawn on first, every time and after a reload, even when the far one holds more and was dedicated earlier. Inside a quarter the **oldest dedication** still pays first (**I4**), and between quarters distance decides (**I6**) |
| 90j2 | Queue three small crop loads for the same larder in one slice | **One** porter carries them, not three. The log shows `porter: job N takes M more, now carrying …` rather than a second `job … carries` line. Two carriers walking the same road half empty is the pathology, and it should never appear |
| 90s | Let the city do something worth a chronicle line, then share water with any faction in Qud and choose to share gossip | The line the roads are telling about **your city** is on the list, in its outsider register's own wording, and sharing it pays reputation. It can only be sold and never bought — you already know your own city's history. Grep for `citizen rite: filed a telling` to see it filed |

## Pass 34 — A day in the city

The living city's placement half. `_notes/LIVING-CITY-ARCHITECTURE.md` §3.2(b): **vanilla ships no
NPC scheduler**, and this adds none. The model decides where a person belongs at this hour, the
anchor moves, and vanilla's own `Bored` goal does the walking — so what you should see is people
*walking*, one at a time, and never anybody teleporting or standing in a doorway.

The day is read in the game's own register. `Calendar.GetTime` names the hours and these are its
own cuts: **The Shallows / Harvest Dawn** (rising, 151–450), **Salt Sun** (at post, 451–750),
**Hindsun** (winding down, 751–900), **Jeweled Dusk** (homeward, 901–1050), **Beetle Moon**
(1051–150). `kingdom:dump` and the status line both name the band you are standing in.

| Step | Action | Expect |
|---|---|---|
| 100 | Found a city, raise two or three works that want crew, and let enough settlers arrive to man them. Wait for **Salt Sun** | Crewed settlers stand at the works they were assigned to, not scattered. Nobody is standing on a wall or inside a building |
| 100a | Watch one settler across the boundary into **Hindsun** and then **Jeweled Dusk** | The field hands and the yard hands leave their posts and walk home, one at a time, on their own feet. The market and the shrine keep theirs through Hindsun and go home at dusk. A watch keeps its post all night |
| 100b | Stand in the market at dusk and just watch for a few dozen turns | The market empties itself and the hearths fill. Nobody sprints, nobody freezes, and no settler stands still for more than a few turns at a time |
| 100c | Read a settler in the wish debugger's object inspector | They carry `KingdomPostWorkId` and `KingdomPostWorkKind`. A settler the works have no room for carries **neither** — an unposted settler genuinely spends their day at home, and is never dragged to a workplace |
| 100d | Take a settler off a work (unstaff it, or strike the work) and come back next pass | The stamp is gone and they stop walking to it. A stale post would be a settler walking to a mill they were taken off |
| 100e | Charm or recruit a settler and walk them around the city | They follow you. The city never re-anchors somebody the founder is leading, and never claims their turn |
| 100f | At night, with beds standing, watch the settlers tagged `SleepOnBed` | Vanilla's own `Bed` sends them to sleep and nothing of ours fights it for the same turn. If somebody is being tugged back and forth between a bed and a workplace, that is the bug this step exists to catch |
| 100g | Grep the log for `perf reify` while you watch a band change | At most eight units a turn, at most four of them body moves. If a band change re-anchored forty settlers in one turn the budget line would say so |

## Pass 30 — What happened while you were gone

The happenings layer. `_notes/LIVING-CITY-ARCHITECTURE.md` §7.4 W4 and
`_notes/BUILDING-CATALOGUE-BRIEF.md` Addendum 13. Four things happen in a city and every one of
them is a **rendering of a row**. A bounded `HappeningModel` receipt may temporarily stage real
bodies and a fixture, but owns no parallel domain history and opens no message channel of its own.

**Read the calendar off the status bar, not off this file.** Festivals are anchored to Qud's own
days: the **Ides** (the fifteenth of any numbered month — the one day the game declines to number,
so the status bar prints "Ides of Kisu Ux") and **the festival of Ut yara Ux** (the five-day
intercalary month between Uulu Ut and Tishru i Ux). A feast that lands on a day the status bar
calls the 14th or the 16th is the bug this pass exists to catch. A new character starts on a
*random* day of the year, so `kingdom:dump` the tick and wish `advanceticks` rather than waiting.

| Step | Action | Expect |
|---|---|---|
| 110 | Found a city, house two settlers **in the same building**, and let ~18 world-days pass with both of them on the roll | Some pass, a wedding: one message in a named settler's mouth, one chronicle line naming both, and an outsider-register line. It never repeats for the same pair |
| 110a | Read `kingdom:chronicle` and then the outsider register | Both carry the wedding. The outsider version is third person and hedged — the founder's voice is never in it (**lane 6**) |
| 110b | House two settlers of **different declared creeds** together and wait | No wedding. A creed code is one-way, so the model can prove agreement and never disagreement, and it declines rather than guessing. Not a stall: nothing was blocked, there was simply nothing to say |
| 110c | Put the eligible pair and an authored bench/chair on owned ground, attend the wedding, and inspect both bodies before, during, and after | Those exact resident/object IDs walk by vanilla pathing to distinct reachable cells around the exact functional fixture, and one really sits through the chair part. While staged they carry `r_TAF_HappeningToken`, former-post/home/anchor and target/fixture receipts; the fixture carries the matching locus/use token. After the hold, both reach their exact former cells with the same home, post, anchor, wander/stay flags, name, equipment, and body ID before the durable restoration acknowledgements clear, and every temporary receipt is gone. One Chronicle/told/message occurrence, never an empty ledger note |
| 110d | Repeat 110c with the bench absent, a principal absent, every approach obstructed, the fixture removed while walking, and the founder leaving before Ready | No replacement body, clone, summon, teleport, or late UI ceremony appears. Before Ready, loss restores every body it can still resolve and produces only one Calendar-dated report; wholly absent evidence starts as that report without touching any body or fixture. Re-entry and repeated heartbeats do not restage or retell it |
| 111 | `advanceticks` until the status bar reads **Ides** of any month | The city keeps the feast: one line naming the day and the realm's dish, one chronicle entry, one outsider line. **Once** — the same Ides never fires twice |
| 111a | Advance past **Ut yara Ux** | The greater feast, named as such. Vanilla's only canonical festival, and the only two anchors that exist |
| 111b | Found a city in Tebet Ux and immediately `kingdom:dump` | **No backlog of feasts.** A city stamps the current tick the first time it looks: it did not miss the Ides of Nivvun Ut, it did not exist for them |
| 111c | Leave for a full in-game year and come home | The feasts are in the chronicle, feast by feast, and the homecoming note counts them. Grep the log for `happening: feast` — there is one line per feast and never one per day |
| 111d | Leave for **several** in-game years | The same shape and no worse: past sixteen feasts the catch-up jumps closed-form instead of walking. If the return took visibly longer than a one-season return, that is the §0.0(a) regression this step is for |
| 111e | In a fresh city, inspect generic travellers before three city feasts have been recorded | **No pilgrim.** Ordinary travellers and nomads may approach, but the ceremonial pilgrim never comes from the fixed guest clock. They stand at the rite-ground approach, not on a random distant cell |
| 111f | Keep the third qualifying feast while attending the city, advance one world-day, and return to the rite ground during the following patience window | Exactly **one named pilgrim** stands at or immediately around the heart. Their description and conversation cite that exact feast, city, and dish. Save before arrival, reload, cross the arrival tick twice, and revisit: the same sequence makes one body, never two |
| 111g | Repeat 111f with every passable cell in the rite cell's three-cell ring occupied or blocked, then clear one | No body is spent or placed in a wall. The opportunity waits. Clearing one coherent approach places the one pilgrim there; generic traffic never steps over the pending cause |
| 111h | Cause a pilgrim, leave for longer than travel plus patience without entering the heart zone, then return | No freshly manufactured pilgrim is waiting. One dated homecoming/chronicle line says a pilgrim came because of the frozen feast and went on unmet. Save/reload before return and open the city twice: one line, no body, no duplicate. Offering water during an attended visit records the greeted wording instead |
| 111i | Attend the next feast with an authored `r_KingdomOven` or other authored `Campfire`; save/cold-load once in Prepared, Walking, Holding, Ready, and Restoring | Up to four exact named residents path to distinct cells around the oven (preferred over another campfire), the exact fixture records use, and the semantic feast/pilgrim accrual happens only after Ready. Every load resumes the same event ID, bodies, fixture and cells. Chronicle, told ring, pilgrim effect, and optional push each settle once; schedules restore last |
| 111j | Repeat 111i without a functional campfire/oven or while away | One dated feast report and its ordinary feast effect; no body or fixture receives a staging property, no arrival is claimed, and returning later does not manufacture the missed gathering |
| 112 | Let a manned mill wear past 60% condition, or unstaff it, and stand somewhere else | One line, once: "The mill has stopped", with the condition. Not a line an hour — grep for `happening: breakdown` and count |
| 112a | Come home and mend it | The **unsaying**, in green, in the same lane the brink withdrawals use: "The mill turns again, at N parts in a hundred" |
| 112b | Leave it broken and cross zones for a while | Silence. The city said it once and does not nag |
| 113 | Kill one of your own settlers | **One** telling, not two: the mourning line as before, now carrying the rite — who spoke the water over them. No separate "funeral" message anywhere |
| 113a | Read the chronicle for that death | One entry. If there are two entries for one death, that is the bug this step exists to catch |
| 113b | Kill one resident while at least one other exact named resident and an authored functional `Shrine` remain on owned ground | Only living mourners walk to the shrine. The deceased body is never recreated, moved, or counted as an attendee. After the hold, one death/funeral Chronicle row and told receipt exist, the mourners restore their exact former schedules, and the dead roll still owns the same name/cause |
| 113c | Repeat 113b with no living bound mourner, no shrine, or during absence | The death still receives exactly one Calendar-dated report. No proxy mourner or late staged funeral appears when the ground is next loaded, and the safety-net scan cannot add a second semantic entry |
| 113d | Complete a building while its exact construction gang and the functional `r_KingdomFirstBasin` are present; inspect completion/outbox before and after Ready | The real builders path to the basin, whose real `LiquidVolume` and fixture-use receipt make it a functional raising locus. Construction's mode-4 outbox stays pending until exact Ready evidence, then its existing deed/Chronicle/ledger/message owner runs once and acknowledges the same event before restoration. The ceremony never substitutes nearby names |
| 113e | For wedding, feast, funeral, and raising, inject a save/quit after post attachment, after partial arrival, after fixture-use, before each sink acknowledgement, and during restoration; also remove the fixture or one body at each pre-Ready cut | Cold-load keeps one bounded operation and monotonic sequence. Inspectable Chronicle/told receipts retry safely; an interrupted uninspectable effect/ledger/message is marked lost rather than replayed. Ready evidence is not reinterpreted after later departure. Every living body reaches its exact former cell and schedule before its restoration acknowledgement; a body is treated as gone only after both its binding and resident row prove death. Repeating the heartbeat clears no unrelated post and emits no second semantic event |
| 113f | Let more than 32 other told events turn over after one wedding and one funeral, save/cold-load, then revisit the same pair/dead row; also leave a mode-4 raising at Ready for more than one world-day before resuming construction | The bounded permanent identity receipts still refuse the wedding, funeral, and raising before any body or fixture is leased. The expired external Ready degrades safely into the construction owner's one unattended telling; it never restages a late ceremony or strands the job |
| 113g | Keep one physical event active in each of two settlements, stand in the second, and let both heartbeat slices run; then kill another resident while that city's lifecycle is busy | Each city reconciles only tokens carrying its exact settlement prefix; the other city's body/fixture receipts survive. The busy death uses the original non-physical mourning fallback exactly once and does not steal, clear, or overwrite the active event |
| 113h | Block a participant's exact original cell only after Ready, then advance beyond half a world-day and reload; unblock it later | Restoration keeps the durable body receipt and uses vanilla pathing; timeout never authorizes a false return acknowledgement. Unblocking lets the same body reach the exact cell, restores its old post/anchor/AI flags, then and only then clears the lifecycle |
| 114 | Stand in the city through a whole day and watch the message log | At most one ambient line an in-game hour, and never the same line twice in one day. The mill's clatter at rising, bread-smell after something cooked, the shrine at Hindsun, the hearths at dusk |
| 114a | Stop every work in the zone and wait | The silence line outranks all of them — "Something has stopped turning. You can hear the water going past it." A texture line that beat it would bury the one thing you can act on |
| 114b | Drain the cisterns dry | "The cisterns knock hollow when somebody walks past them", before any hour-texture line |
| 115 | Play a founder with a mutation the city's creed has a number for (an esper founder under a fault-line creed; wings under a bird creed) and stand in the city | One line, once, in a settler's mouth, naming the creed **and** the thing they mind or admire. Then never again unless something about you changes |
| 115a | Have chrome installed and found a city whose creed lists `cybernetics` | The chrome line instead. A creed that lists it *inverted* mutters; one that lists it plainly admires |
| 115b | Play an unremarkable founder under an unremarkable creed | Nothing. Silence is the ordinary answer and is never a failure |
| 116 | Let the office pass to a settler and read them in the object inspector | They carry an epithet out of vanilla's own `Naming.xml`, and their stats are **unchanged** — no doubled hit points, no free mutations. The chronicle line names them by it. (**W5 amends this step:** every citizen now carries `GivesRep`, from lane 1's water ritual — that is a conversation gate, not a hero template, and the statistics are still untouched) |
| 116a | Kill the office holder's neighbour while the holder is on the roll | The rite clause names the holder **by their epithet** |
| 117 | Come home after a long absence and open the Charter | One line under the ledger counting what you missed: feasts kept, weddings, burials, works stopped. One line, not four — it must not push the settlement's own arithmetic off the end of the report |
| 118 | Grep the log for `perf slice` through a season of happenings | `steps` still at 1 or 2, never above 4. Happenings are generated inside the same slice and add no second pass |

## Pass 31 — The city asks

W5. `_notes/LIVING-CITY-ARCHITECTURE.md` §5 and §7.4 (engagement, and the API opens), §6.6 (the
published contract), `_notes/BUILDING-CATALOGUE-BRIEF.md` Addendum 12(i) and Addendum 13 lanes 1
and 8, and `_notes/DIVERSITY-AND-TECH-TREES.md` §2.8 (the tech map).

**Three readings now live together under Charter → The city in full (`c`): `b` the book of the
city, `k` where the keepers' craft could go, `a` what the city is asking for.** The Charter root
shows **Status and next need** first, then seven named chapters. All three city-in-full entries are
READINGS. The single thing this pass is most for: *find anything on any of them that can be
pressed.* If you can, that is the failure — the no-research-tree ruling and VISION's "not a second
job" pillar both turn on it.

**Hotkeys.** There are thirty-five action routes across the seven chapters. Keys are local to the
screen, every chapter ends with **Back to the Charter** (`x`), and controller cancel does the same
thing. A duplicated key within one screen silently picks whichever option comes first, and it has
bitten this file before — step 119 exists solely to catch it.

| Step | Action | Expect |
|---|---|---|
| 119 | Open the Charter and traverse the root and all seven chapters | Eight root rows: **Status and next need** first, then seven chapters. Every screen has unique keys; every chapter has explicit `x` Back and controller cancel. Entering, backing out, and cancelling spend no turn |
| 119a | Traverse every action route in every chapter, returning after each read-only screen | All thirty-five old actions appear exactly once and open the right screen. Readings return to their chapter; a committed action closes the Charter and spends exactly one turn |
| 120 | Charter → The city in full (`c`) → The book of the city (`b`) | Six chapters, and a headline naming the tick the model is carried through and how many days behind now that is. A city that has just been reckoned reads `(current)` |
| 120a | Read *the stores, and what holds them* with two zones claimed and a granary in the far one | The far zone's food is a real number, without walking there. A city that has dedicated no vessels reads **"nothing dedicated"**, never `0 of 0` — those are different sentences |
| 120b | Leave for a season, come home, and open the same chapter **before** walking to the far zone | A "the count and the vessels have not been squared here" clause with a signed figure. Walk there, come back, and it is gone. That is Addendum 12(d)'s debt, told rather than silently repaired |
| 120c | Read *the works, and what they are waiting on* | Every work in the **city**, not the zone. The count of "waiting on you" matches the works that actually are: a worn-past-the-line one, and a producer/refiner/power/active-construction work with no hands. A larder or a field with nobody on it is **not** waiting |
| 120c1 | Crew two works, save and cold-load, then exchange the seat to a second city and read both books | Each work reports the exact number of resident rows posted to its stable id on its bound zone. The count survives reload. The second city never borrows the first city's crew. An active frame reads as **construction**, while producer, refiner, power, store and growing works retain their own classes |
| 120d | Unstaff a mill and read both this chapter and the breakdown message from Pass 30 step 112 | They agree. The board and the news share one definition of stopped; if one calls it stopped and the other does not, that is the bug this step exists for |
| 120e | Read *the people, and where their day puts them* | Living, away-with-you, and buried counted apart; the day-shape spread; the office holder named **with their epithet**; and a closing line counting how many citizens here will share water |
| 120f | Read *the turn of the year* | Today's date as the status bar gives it, the next feast and how many days off, the realm's dish, and the heart's rung when you are standing on the rite ground |
| 120g | Open the book standing **outside** the city | It still reads. The rung line says nothing stands on the rite ground — a report must never load a parasang to write a heading, so the heart is only read where you stand |
| 121 | Charter → The city in full (`c`) → Where the keepers' craft could go (`k`) | The craft level and what the next rung costs, the same numbers the keepers' screen gives. If the two disagree, one of them is lying |
| 121a | Certify a solar still and re-read | Under *what they know, and what it opened*, the still now names the condensing hall. Before certifying it, the hall was in *what is nearly in reach* with "the keepers have never been taught solar condenser" against it |
| 121b | Read *what is nearly in reach* | Sorted nearest first, each with what is in the way in the gates' own refusal order: knowledge, then craft, then ground, then stage, then the district. A design gated **only** by a district counts as within reach — you answer it by standing somewhere else |
| 121c | Read *roads not taken* on a fresh city | All three named: no disk read, no machine certified, nobody arrived with a trade. Take in a settler with an origin and the third disappears |
| 121d | Turn `r_TAF_OptionZoning` off and re-open | One honest sentence saying every design is open and there is no map to draw. Not an empty screen |
| 122 | Charter → The city in full (`c`) → What the city is asking for (`a`) | Worst first. A dry cistern is `!!`, more people than roofs is `!`, an idle mill is `·`. **Every** line names what would settle it |
| 122a | Empty the cisterns to exactly zero, then put one dram back | The thirst ask appears and then goes. One dram is not an ask — the board fires on empty, never on "low", because it is forbidden a balance number of its own |
| 122b | Dedicate no vessels at all | **No** thirst ask. The city has no cisterns, which is a different thing, and the book's stores chapter says so on its own line |
| 122c | Fill one zone's larder to the lid while another zone has room | A haulage ask naming the full one. Empty the other larder's ceiling too (or claim only one zone) and it goes: a city with nowhere to put anything is asking for a larder, not for haulage |
| 122d | Have a settler waiting to speak and open the board | The petition is the **first** line, and it points at the Charter to hear them. The board does not hear it — it is a reading |
| 122d1 | Before hearing that petition, satisfy its displayed condition; run another settlement pass | It remains **Offered** by the same named body, with the same words, origin, target and event. No deed, accomplishment, petition-met increment or resolution is minted before explicit acceptance |
| 122d2 | Save, quit and cold-load while it is Offered; hear it at the Charter and accept; save/cold-load again; satisfy the frozen target; cross several passes and reopen the Charter | The exact requester body/name, source city, cause, target and event survive both loads. Acceptance is a separate durable action. Resolution, petition-met increment, deed, chronicle, ledger and message each occur exactly once |
| 122d3 | On a fresh offer choose Decline, then revisit the Charter and cross several passes short of the documented interval | The unanswered petition closes without penalty and cannot be reopened by reading it. No replacement appears before the interval; a later eligible offer has a new event and a real current-city requester |
| 122d4 | With one fresh petition Offered, turn `r_TAF_OptionPetitions` off and run a pass; turn it on again | The unaccepted offer closes. Re-enabling starts a future offer clock; it does not resurrect the closed event or emit its sinks again |
| 122d5 | Accept a fresh petition, note its remaining window, turn petitions off, wait beyond that duration, then turn them on | The accepted promise is paused, not declined, resolved or expired. Re-enabling resumes the same frozen undertaking with its saved remaining duration measured from now; it expires only after that future deadline |
| 122d6 | With two cities, accept a petition in the first, walk to the second and satisfy the first city's condition there, then return | The second city's state cannot fulfil, replace or reinterpret the first city's petition. Returning restores the first city's same requester/body/cause/target/event and only evidence on its own seated ground may resolve it |
| 122d7 | Load one pre-lifecycle save with a complete active petition, then one fixture with a missing/ambiguous requester or malformed origin/cause/target/event | Complete evidence adopts once as Offered or Accepted exactly as saved. Malformed evidence is visibly quarantined and retained for diagnosis; no field is cleared, repaired from current state or turned into a reward |
| 122e | Post notices at the heart and re-open | A closing count of your own notices. Nothing on the board takes them up |
| 122e2 | Install several ask sources and open the board | Never more than eight lines, worst first. The cap is a promise about the **screen**; ten installed mods must not turn it into a spreadsheet |
| 122f | Bring the city to full health — stores held, roofs enough, every work crewed | "Nothing else. The stores hold, the roofs are enough, and every work that wants hands has them" |
| 123 | Talk to one of your own settlers | A conversation, and **"share your water"** in it. Before W5 a settler could not be talked to at all |
| 123a | Share water with them | Vanilla's own ritual, for **your realm's faction**: the reputation, and the settlement's own favoured dish taught as a cooking recipe. That recipe has existed since the food lane and until now no creature in Qud belonged to the faction to hand it over |
| 123b | Cook the dish afterwards | It is the one the Charter's status names. If the ritual taught a different recipe, the faction stamp and the report disagree |
| 123c | After the outsider register gains a telling, share water with a faction interested in `gossip` or `settlement` and inspect secret trading; repeat with an uninterested faction and after save/reload | Vanilla offers the latest outsider telling for the founder to **sell** where that faction's own interests give it weight. It is never offered for the founder to buy back, because the observation is already revealed. An uninterested faction may show no row. Reload, seat exchange, and re-filing the same telling create no duplicate observation or second payment record |
| 123d | Talk to a settler another mod gave a conversation to | Their own conversation, unreplaced, with the water ritual choice in it (an XML conversation inherits it already). Taking away somebody else's content to add something already there would be the failure |
| 123e | Turn `r_TAF_OptionCitizenRite` off, then on, and let a pass run | Off, no new settler becomes a host; on, the next pass makes them one. Existing hosts are not stripped — nothing this mod added to a creature is taken back off it |
| 123f2 | Talk to a settler on the day they arrive, then again after a month of world-time in the city | The greeting **changes** — newcomer, then settling in, then settled. A conversation is a fixed string on the object, so a settler stamped once on arrival and never re-read would greet you as a stranger forever and two of the three lines would be dead. Only conversations this mod built are ever rebuilt |
| 123f | Save, reload, and talk to the same settler again | Still a host. If the conversation was lost to the round-trip, the next settlement pass repairs it, because the check asks the object's actual state rather than a remembered flag |
| 123g | Kill one of your own settlers **after** W5 has made them a host | Your standing with **your own realm** drops, by vanilla's own legendary-kill arithmetic, and one to three factions the realm has feelings about move with it. This is new in W5 and it is deliberate: `GivesRep` is what opens the ritual, and it is also what makes murdering your own citizen cost something. Bounded to the realm and its related factions — it is **not** a world-wide penalty |
| 123h | Share water with a settler and then kill them | The **water-ritual curse**: vanilla's own covenant violation, rep with every visible non-hostile faction. The exile lane already has prose for this deed (`WaterRitualCurse`), which is how you can tell it was expected rather than stumbled into |
| 123i | Share water with a vanilla **Barathrumite**, then Charter → **What the keepers know** | The founder's ledger now remembers `rite:barathrumites`; `arclight discipline` is visible and **begun** at one quarter in the seated city, never completed. Share water with another Barathrumite or reopen the same ritual: no second key and no extra quarter. A distinct matching source may bring a node only as far as its existing 50% cap. Save/reload, then repeat with a ritual performed **before founding**, after exile/refounding, and in city two: the rite follows this founder, while each city's seed receipt/progress remains its own. In Kingdom Mode, complete succession: the heir keeps the city's research standing but does **not** inherit the dead founder's rite ledger; a later body change still receives new ritual events exactly once. |
| 123j | From Charter → **What the keepers know**, try to change the current research subject; then walk to the seated city's staffed scriptorium/inquiry bench, interact with it, and choose one visible subject | The Charter remains a reading and offers no remote subject verb. The physical bench offers **set the city's research subject**, names every exact blocker, and the accepted subject belongs to the city whose claimed ground and bench the founder is touching. Repeat in city two: each city's subject and accrued work remain separate. |
| 124 | With no other mod installed, read *who else writes in this book* (the book's sixth chapter) | "Nothing is extending it. The city is entirely its own", and the API version |
| 124a | Install a mod with a `[KingdomExtension]` class built against **the wrong version** | It is **refused by mod name**, on screen and in the log, naming the version it wanted and the version we publish. It is never silently skipped. The refusal is listed in this chapter |
| 124b | Install one whose extension class **throws** in its constructor or in `Ask` | Only that one is lost. Every other extension still runs, the board still opens, the turn is unaffected, and the log names the mod. A constructor that throws is refused as **"threw"**, never as "declares no API version" — the two send a modder to different lines |
| 124b2 | With that mod installed, open the asks board | It says out loud that something stalled this reading, and names it. A log line is not a surface, and a board quietly showing fewer entries would read as a healthier city |
| 124c | Install a working ask source and open the board | Its asks are filed `<mod-slug>:<kind>`, at most four of them, with colour markup stripped. They are ranked **by weight beside the city's own** — a mod's grave ask outranks our passing one — and sort after ours only among equals |
| 124d | Install a happening source and let a settlement pass run | Its chronicle line lands; its spoken line is heard **only** if the city had nothing more pressing to say. Grep for `extension happening:` — the count matches |
| 124e | With the same happening source, note a spoken line, reload the save, and repeat the pass | The same draw and the same result. An extension's chance is as replayable as ours or the city is unfalsifiable |
| 124f | Enable two extension mods, then reorder them in the mod list | The same behaviour and the same draws. Registration is sorted by mod name, never left in scan order |
| 124f2 | Install a v1 ask/happening extension and a v2 identity source beside the API-v3 behaviour fixture | All three admit. The registry reports API 3 with minimum supported 1; the version bump does not retire genuine older-contract extensions. Change only the fixture's declaration to v2: its five durable lanes are refused together as requiring version 3, never half-loaded |
| 124f3 | Give the v2 source one owned key, one foreign `culture:` key, nine more keys, and affinity outside 70–130; then return 32 malformed keys, compose positive and negative sources in both orders, and make each method throw on a later call | Only the first eight distinct owned keys can enter the live roster; the foreign key never does; no slot beyond the first 32 is inspected. Mixed affinities give the same answer in either order and remain in 70–130. On the throwing calls that source contributes no keys and neutral affinity, every other source and the city continue, the log names the mod plus `identity keys` or `identity affinity`, and one on-screen warning appears per mod and faulted lane rather than every pass |
| 124f4 | Use two residents with different `GetSpecies()` values; give one blueprint `r_TAF_Refuses="species:<the other>"`; attempt shared housing, then remove the refusal | The exact species appears as a generic self-tag and blocks only the existing cohabitation match. Removing the authored refusal restores normal lodging. No species catalogue, hidden penalty, or research-tier change occurs |
| 124g | Turn `r_TAF_OptionExtensions` off | No third-party C# runs, and the chapter says so. Buildings, deals and settlers those mods merged by XML are **unaffected** |
| 124h | Corrupt or clear the city book (or catch a real fault) and open both the book and the asks board | Both say the book could not be read, in as many words. Neither shows a page of zeroes, and the asks board does **not** print "the stores hold, the roofs are enough" — reporting an unreadable book as a contented city is the report telling you the opposite of the truth |
| 124i | Cold-install a separate extension assembly implementing `IResourceKind`, `ICarrierKind`, `IJobKind`, `INetworkKind`, `IWorkBehaviour`, and `IHappeningGenerator`, referencing only `ThousandAndFirst.Api` | It registers by mod name at API 3. Its own reading/log shows one owner-qualified resource, paired carrier/job, network, work state, and happening. No core type, zone, object, clock, or mutable city row crosses a callback boundary |
| 124j | Let the fixture open one cargo job over a held-zone leg, save before due, reload, and pass its due tick | Cargo debits once at opening. Carrier blueprint, pace, route-derived due tick, and completion survive reload. All completion resource changes publish once together; repeating the stable job key does not reopen it |
| 124k | Give the fixture one source and two priority-ordered sinks behind bounded capacity, then advance one whole day twice from the same save | Both runs report identical flow and brownout. Lower-numbered priority is served first; only whole-day source surplus enters the owned resource and capacity clamps it. Reordering mods changes neither result nor draw |
| 124l | Bind the fixture work behaviour to one existing attended work and owe one takeable object, then obstruct its exact cell, reload, and clear it | Obstruction/creation failure leaves the same debt and mints no substitute. Once clear, one exact blueprint lands on the exact work cell and only then debt falls by one. Revisit/reload cannot duplicate it; creature or untakeable blueprints are refused |
| 124m | With resource/job/network/work rows live, exchange the seat or archive/exile the settlement, save, reload, and return | Current nested archive v12 restores canonical behavior, happenings, defensive reservations, semantic selections, and exact per-source happening cursors, while the realm archive restores exact job/logistics payload: level, frozen job receipt, network solve, work state, owed object, reserved work/resident identities, and office/person plan match. Settlement v6 defaults behavior empty; v7 retains behavior while defaulting later sidecars; v8 retains physical happenings; v9 independently decodes/re-writes the same reachable envelope as its required epoch marker; v10 retains defensive reservations; v11 retains semantic selections and migrates its aggregate happening tick into exact active-source receipts. Realm Jobs v3/v4 independently prove logistics migration. Every older shape rewrites stably, and every salvage/pilgrim/expedition field remains intact |
| 124n | Make each v3 callback throw in turn while a second extension remains healthy | Only that callback publishes nothing. Later owners and later phases continue, the existing sidecar is retained, and the log/on-screen fault names owner plus resource/carrier/job/network/work lane. An in-flight frozen job still settles without calling its disabled owner |
| 124o | In separate runs, attempt a thirty-third draw; return 32 malformed rows followed by a valid one; exceed each owner/city cap; use a foreign namespace; and include one invalid change in a multi-change work/job result | Thirty-third draw is `OverBudget` and publishes no callback result. Slot 32 is never inspected; malformed slots consume the bound. Caps stop new rows without disturbing old ones. Foreign rows and whole malformed atomic results publish nothing; another owner remains unaffected |
| 124p | Disable the fixture after one job opens, pass due, then re-enable it and repeat the original start-tick proposal | Frozen job completes or fails/restores cargo exactly once while disabled. No new proposal runs while off. Re-enable does not revive the retired start-tick receipt or duplicate its completion; four terminal receipts rotate without permanently closing the owner's job lane |
| 125 | Grep the log for `perf` through a season with extensions installed | Extension jobs appear on the reckon lane with a `ext:` label. Nothing else moved: the receipt is the regression test |

## Pass 35 — Styles that mean something, and the creed-gate stack

`_notes/BUILDING-CATALOGUE-BRIEF.md` Addendum 16, composing with Addendum 14's visibility law.
Two halves: the five city styles finally filter the catalogue, and a design may now be gated on
who the city's PEOPLE are rather than on where it stands.

**What to be suspicious of.** Every other gate in this mod leaves the design in the list wearing a
tag that names its key. The creed gate has ONE case that shows nothing at all, and if that case
ever swallows a design a founder could actually have raised, they have no way to learn it exists.
Steps 129–131 are that case from both sides.

| Step | Action | Expect |
|---|---|---|
| 126 | `kingdom:style common`, then Charter → Commission and read the whole list | No `timber hut`, no `hut and yard`, no `sawyer's yard`, no `thorn palisade`, no `water wheel`. **A `mud-brick hut` and a `caravanserai` that no other style has.** The salt flats have no trees and no river; the list says so by what is in it |
| 126a | `kingdom:style verdant`, same list | No `dew catchment`, no `catchment bank`, no `air-well court`, no `air-well field`, no `manor`, no `settler's cairn`. **A `stilt row` and a `grave-grove`.** The marsh has no dry night and no fieldstone |
| 126b | `kingdom:style fungal` / `gyre` / `eater`, same list | fungal loses the salt-pan lane, the sailvane and the granary and gains the `spore cellar` and the `cap-roof` (the only dwelling that offers `taf:damp`); gyre loses the pan lane, the wheel, the bazaar and the bathhouse and gains the `bone fold` and the `sacrament court`; eater loses the weep lane, the timber lane and the palisade and gains the `block hut`, its yard, and the `re-stood course` |
| 126c | In each of the five, check the water, food and housing lanes are all still there | Every style can still raise water, food and a roof at every stage. Losing a DESIGN is the pass; losing a LANE is a bug. `_notes/balance-sim.py` Q12 asserts the same thing off the same file |
| 127 | Raise a `mud-brick hut` in a common city and let it improve | It improves into the `mud-brick hut and court`, not into anything with timber in it |
| 128 | Load a save whose city has a `timber hut` standing and force `kingdom:style common` | The hut keeps working and is never touched (nothing regresses silently), and the improvement list reads *"hut and yard is not built in a city of this kind"* rather than offering it or dropping the line |
| 129 | In a fresh city with no Barathrumites, Mechanimists or Consortium in it, read the whole Commission list, the plan list, and Charter → the keepers' map | **The `under-bench`, the `reliquary` and the `factor's house` are not in any of them.** Not greyed, not tagged — absent. A city that has never held the creed has no path to the work and nothing to be told |
| 129a | `kingdom:dump` in the same city | `Creeds held:` and `once held:` are both `(none)` or name other creeds. That line is the evidence for what step 129 showed you |
| 130 | Grow until a settler arrives holding with the Mechanimists (or hold a water rite with them and convert one), then re-read the Commission list | The `reliquary` **appears**, tagged with whatever is still in its way — the share, the hands, or the keepers' learning. It appeared the moment one person aligned |
| 130a | Try to commission it anyway | Refused, and the refusal names ONE thing: how many hold with them against how many live here, or which hands are missing, or what the keepers were never taught. Never "cannot be raised here" |
| 131 | Convert that settler AWAY from the Mechanimists to something else, then re-read the list | **The reliquary is still there.** A creed somebody once held is still a path — they can be turned back. This is the whole reason a settler's creed history is recorded, and a design that vanished here would be the bug |
| 131a | `kingdom:dump` after step 131 | `once held:` now names Mechanimists. The record is on the person: it survives a save, a walk to the other city, and the seat swap |
| 131b | Let that settler die or emigrate, then re-read the list | The reliquary is gone from the menus again. The history left with the person who carried it |
| 132 | Stake a plan for a creed-work while the creed is held, then let the last believer leave before it is realised | **The plan still realises.** The gate is judged where the plan is STAKED — that is the cell and the moment the founder decided on — and the visibility law hides designs from a menu; it never reaches back and cancels a decision already made. A staked plan that quietly disappeared would be exactly the silent stall STANDARDS 7b exists against |
| 132a | With the same city, now open the Commission list again | The creed-work is gone from the list, and the one that was already staked is still standing in the plan. Those two facts are consistent: one is an offer, the other is a commitment |
| 133 | Ship a third-party `KingdomBuildings.xml` re-declaring `underbench` with `CreedShare="0"` and nothing else | Only the share changes; every other attribute the base catalogue wrote still stands, and one aligned builder is now enough. Grep the log for the merge note naming `CreedShare` |
| 134 | Ship one with `Styles="all,!eatr"` (deliberate typo) | The load log notes a style built for that no `<style>` declares. `python3 Art/check_xml_refs.py` catches the same thing before the game ever runs |
| 134a | On installed Qud 2.0.211.51, run `kingdom:creedcontent` | It derives **33 admitted creeds, 33 covered creed keys, 128 creed-work exact mappings**, then passes every one as mapped and behavior-bearing. It does not compare against a C# faction list. Chiliad adds no admitted creed under the same facts rule |
| 134b | In disposable cities, align enough builders with each name printed by 134a; reveal the work's other declared gates and preview it at every offered lot size | Every creed reveals one lore-specific, behavior-bearing design with a distinct usable topology and truthful bill. Every shown size resolves exactly. No entry is a palette/name-only rectangle, and a city with no live or past path to that creed still sees nothing |
| 134c | Add a third-party faction whose ordinary faction facts pass `CanBeCreed`, plus one ordinary creed-gated building, blueprint and architecture record; rerun 134a | Census and coverage rise together without recompiling core. Removing any of those data records produces a named runtime fault; it never silently borrows another creed's plan |

## Pass 36 — Succession, founder remains, and the legacy seal

Use disposable profiles: this pass deliberately kills player characters. Run the main branch in
the **Kingdom** game mode added by this mod; Classic and Roleplay must retain their own death rules.

| Step | Action | Expect |
|---|---|---|
| 135 | Start Kingdom mode, found a two-city realm, reveal several forgettable journal notes plus map notes and accomplishments, and admit at least two settlers. Record their arrival order and the cities' research standing; save | The roll gives an unambiguous senior heir. Kingdom mode's description promises character death with realm continuity before play begins |
| 135a | Die by an ordinary, non-vaporizing cause while standing on claimed ground; watch the heir's city rather than dismissing the result as a popup | The world clock reaches the exact due tick; **every** named, already-bound living resident whose exact body is present in the rite zone physically walks to the city's extant first basin/court/rite-ground/shrine fixture and the non-heirs walk back. There is no representative cap; residents physically standing in another quarter remain there and are not claimed as attendees. Their object ids, work posts, home plot ids, AI anchors, and prior goals are unchanged. A new non-takeable **shrine-marker of NAME** stands on frozen open ground. Only after that evidence exists does control pass to the exact senior body. One combined Chronicle row tells death, priced road, rite, marker, and accession; no earlier “word” message duplicates it |
| 135a1 | Before death, record the senior body's object id, cell, inventory, post/home properties and fixture id; after accession inspect them and `kingdom:dump` | The player is that exact object—not a clone or substitute—and stands at the rite cell beside that exact fixture. The fixture records this death token and attendee manifest. No attendee, inventory item, liquid, charge, food, water, or material was created by the ceremony |
| 135a2 | Repeat from an ordinary same-world remote zone at known global-zone/depth distance, then from claimed ground | Remote word costs `ceil((max(dx,dy)+shaft-multiplier*dz)/2)` world-days, capped by the authored 14-day rumour road; claimed ground costs zero. The clock changes before the procession; the body changes after it. No simulated NPC/world turns are claimed during the synchronous death callback |
| 135a3 | Repeat with a lit reciprocal mirror gate at the death locus answering the seat; then darken/break the answer and repeat from a disposable copy | Lit answering arch is same-hour `Arch`; broken/dark/non-answering arch pays the actual road or other-world rumour. Merely owning a gate elsewhere does not waive time |
| 135a4 | Inspect the founder marker before/after save → quit → reload | Its dedicated `r_KingdomFounderShrine` part retains exact founder name, death tick, cause clause, death token, city, fixture id, and epitaph/rite history. It is distinct from the cross-run cairn and remains exactly one object on the same cell |
| 135a5 | With the native-test harness, set `KingdomSuccession.InjectedCheckpoint` to snapshot at `Frozen`, `WordArrived`, `ProcessionComplete`, `ShrinePlaced`, and `BodyCrossed`; cold-load each snapshot, then run `kingdom:selftest` | Each load re-proves the frozen founder/heir/body/zone/fixture/attendees/post/home/shrine evidence and resumes the next adjacent checkpoint. A marker placed before the cut is adopted by token/object/cell, never placed twice; a crossed body publishes/repairs the same resident accession, never crosses again. Chronicle, modal, seal handoff, and marker remain exact-once |
| 135a6 | From copies, remove the exact heir, destroy every valid fixture, block the frozen rite/marker cells, duplicate a resident id/body, and corrupt one saved manifest/receipt field | Every case fails closed before any substitute or body crossover. Pre-placement failures make no marker; an already-proved marker is never silently destroyed. Ambiguous/corrupt cold-load state quarantines succession for that save and mints no replacement object or resource |
| 135b | Inspect the new player, Charter, roll, city book, research, inventory, and `kingdom:dump` | The heir has their own body, stats, and inventory plus the Charter; they are no longer duplicated on the resident roll. Both cities, claims, structures, stores, chronicles, and city research persist. The founder's kit remains with their corpse or drops; personal recipes, reputation, and rite ledger do not silently become the heir's. Map notes and accomplishments survive succession |
| 135c | `kingdom:selftest`, then save → quit → reload → repeat it | All 17 checks pass before and after reload. The same heir remains player; no second accession, duplicated resident, or repeated mourning line appears |
| 135d | Find the founder's generated corpse and use **Read founder's memory**; accept | Only eligible forgettable journal knowledge from that exact founder returns. The popup reports the truthful count; map notes and accomplishments remain intact, and no item, liquid, reputation, research completion, or body state is copied |
| 135e | Inspect the corpse again, then save/reload and inspect it once more | The read action is spent exactly once and does not return after reload. A different corpse cannot answer this death token |
| 135f | Repeat in a fresh Kingdom-mode realm with no eligible resident, then die | No substitute body is invented. The dynasty ends through Qud's normal terminal path and an eligible realm is sealed for the profile when its release conditions are met |
| 135g | From that profile, run Pass 0's opt-out world and then opt-in world | Opt-out consumes nothing. Opt-in imports the one promoted legacy once, with bounded layout/history only and no people, items, liquids, or charge |
| 135h | In a disposable Classic or Roleplay game, found and then die | No Kingdom-mode body transfer occurs. Qud's original death handling remains in force; the seal coordinator may record an eligible ended realm but never changes who the player controls |
| 135i | In a separate founded disposable save, open the Charter's chronicle/dynasty chapter and choose **Dynasty and retirement**; cancel the first confirmation | No generation is sealed, no retirement report appears, and the save remains playable and unchanged |
| 135j | Reopen retirement, accept the first confirmation, then cancel the second confirmation | The irreversible seal still does not occur. The current save remains playable and retirement can still be offered later |
| 135k | Reopen retirement and confirm both prompts | The success report says the immutable generation was written to the profile while this save continues. The city remains playable; reopening the chapter reports that this generation is already retired |
| 135l | Save, reload, and reopen retirement; then continue playing long enough to change the current city | The generation remains retired after reload and cannot be sealed again. Later changes remain in the playable save but do not rewrite the already retired generation |
| 135m | From that profile, run Pass 0's opt-out world and then its opt-in world | Opt-out consumes nothing. Opt-in imports the exact retired snapshot once under Pass 0's bounded inheritance rules |

## Pass 37 — Purposeful cities, exact cargo, and the honest arcology foundation

Set up two City-stage settlements joined by a reciprocal mirror-gate pair. One city must have
living-biome founding evidence, a local vat-house or grafting hall, and a lodged Intelligence-18
savant. The other must have ruin founding evidence, a local smelter and charging post,
Mechanimist/Templar present-or-past reach, and a lodged Intelligence-18 tinker/technician. Give
both gates current power and each destination ground one dedicated material stockpile. The present
scope is deliberately the two body purposes; this pass does not claim the deferred drill/foundry/
granary portfolio or hosted, zone-spanning arcology ground.

| Step | Action | Expect |
|---|---|---|
| 136 | Try to quote either body-megastructure before building/keying the mirror pair; then repeat with one arch dark and with one arch not visited for a world-day | Every attempt refuses before debit and names the repair: build/key the reciprocal pair, restore charge, or visit both arches to refresh their honest readings. Raw research or an away-city boolean never substitutes |
| 136a | At the ruin city's gate, choose **dispatch a purpose consignment** → graft-stock casket; read the confirmation, then cancel | It names exactly one casket, producing city and `vathouse/graftinghall` proof, destination city and stockpile, live route, 12 drams plus `brush:4,workedmetal:1`, use, and exact-object retry/inspection consequence. Cancel spends nothing and creates no cargo/job |
| 136b | Accept 136a; save/reload once before leaving either city, then inspect the destination stockpile | One non-stackable **sealed graft-stock casket** arrives, carrying one worked-metal unit and one stable consignment identity/provenance. Source stock and water pay once. Reload cannot mint a second object, switch destination, or charge again |
| 136c | At the living city's gate, dispatch an arclight roll-register to the ruin city in the same way | It requires **both** local smelter and charging post, costs exactly 16 drams plus `scrap:6,workedmetal:1`, and delivers one exact sealed register to the frozen ruin-city stockpile. Removing either producer before confirmation makes the second proof refuse without debit |
| 136d | In the living city, preview the chimeric theatre | The preview visibly freezes **flesh-city**, the exact casket object/consignment and producer city, live mirror route, living-biome + damp/offal site, lodged named savant, authored procedure output, ordinary build bill/time/map, and recovery rule before the confirmation choice |
| 136e | Cancel 136d; move the casket out of its dedicated stockpile; try again; return that exact object; try again | Cancel changes nothing. The moved object refuses by identity and says to return it; an ordinary worked-metal item cannot substitute. Returning the same object restores the exact preview |
| 136f | Confirm 136d and let construction finish, checking the material stock immediately after commitment | The exact casket is consumed as one already-declared worked-metal unit of the ordinary theatre bill—not an extra token—and no other same-kind item answers for it. The standing theatre retains its frozen purpose receipt and performs the existing authored chimeric procedures |
| 136g | In the ruin city, preview and commission the becoming annexe from its exact roll-register | Its proof is genuinely different: ruin evidence, smelter + charging post, Mechanimist/Templar reach, and a lodged named psyberneticist. The exact register is consumed once; the finished annexe performs the existing authored enrollment/becoming authorization |
| 136h | In either city after its first body work is funded but before it finishes, attempt to commission the other body work in the **same game tick**; repeat after completion, including in the capital | Refused as that city's already-kept purpose in both cases. A capital may keep its capital-specific foundation and one body purpose, never both theatre and annexe |
| 136i | With a debugger/fixture copy of the save, interrupt purpose cargo at each durable phase (`CargoOutputPending`, `CargoOutputSettled`, `CargoTransferPending`, `CargoDelivered`) and interrupt purpose-building funding before/after water and material callbacks | Retry either advances the same rooted object and exact economic receipt, rolls that object back to its source on a clean destination refusal, or enters inspection on ambiguity. It never duplicates, reroutes, substitutes, or charges twice; two-city seat order does not change the result |
| 136j | Commission and examine the capital arcology record, stacked ward, and lamp-terrace | Every menu and object calls the present work an **arcology foundation** or **surface prototype**. Nothing claims hosted streets, interior plots, separate zones, indoor residents, or zone-spanning ground exists yet |

## Pass 38 — Named salvage expedition

Use one city with a named resident bound to a visible body, dedicated fresh-water vessels, and
physical food in dedicated larders. Personally visit an unclaimed map destination, return home,
record resident object id and exact store contents, then save.

| Step | Action | Expect |
|---|---|---|
| 137 | Charter → Stores & routes → **Commission a salvage expedition**; select resident and visited destination, read preview, then cancel | Preview names exact resident/site, drams, provisions, world days, and due date. Cancel changes no body, job, water, food, Chronicle, or ledger |
| 137a | Reopen and confirm | Exactly quoted water and food leave dedicated stores once. Same object id moves to destination; roll says **on expedition**; realm job status/recall names resident, destination, and due world-time |
| 137b | Save/quit/reload immediately; exchange city seats twice; inspect both rolls and Charter | One job and one body exist. Job remains realm-wide and home city remains source; no second job can select that resident and no cost repeats |
| 137c | In debugger fixture, cut after prepared row publication, after body-receipt attachment, and after each partial food/water callback; cold-load each | Pre-receipt cut reconstructs only while no debit marker exists. Every partial count/volume continues from frozen before/after range. Unreachable body leaves same open row and performs no new charge; third physical values refuse inspection rather than guessing |
| 137d | Recall before due; repeat from pre-save and instead let world clock pass due while adventuring | Recall returns same body once and does not refund or recharge route stores. Due branch returns same body with frozen 0–4 vanilla scrap exactly once; picked-clean is cargo-free, never random offscreen death |
| 137e | For separate fixtures, kill exact body on recorded ground, remove it from that ground, and make it follow founder; for each, cold-save after the resident standing/binding changes but before the next settlement pass, then advance | Each real condition produces its own dated dead/astray/followed result. Engine death freezes terminal job authority before the death hook releases the binding; a prior/same-pass typed dead/astray/followed row with proved binding absence also resumes. Reload preserves the exact terminal date, ground, outcome, and any known death cause; no substitute body, arbitrary random death, duplicate telling, or reward appears |
| 137f | Read homecoming and Chronicle, save/reload, revisit/seat-swap, then commission same returned resident again | First result appears once on both surfaces and reward marker has one stack. Closed-job markers do not block second commission; second job has new id/receipt/outcome authority |

## Pass 4 — Attitudes and persistence

| Step | Action | Expect |
|---|---|---|
| 17 | With Snapjaws standing at 600, wish `snapjaw scavenger` near a citizen | It does NOT attack the citizen (it may still dislike *you* — that's the two ledgers) |
| 18 | Save → quit to menu → reload | `kingdom:status` + `kingdom:chronicle` intact; selftest still all-PASS |
| 19 | Player.log | No "Bad event bind", no exceptions from ThousandAndFirst |

## Current implementation limits and v1 scope gates

- No ownership stamping on claims (can't rob your own city — membership design pending).
- Settlers use vanilla farmer behavior between posts; the city moves their anchor by the hour and
  vanilla walks them, so a settler with no post keeps the hearth and looks exactly as they did.
- Inherited sites currently restore bounded structures, their founder's cairn, and history only.
  Prior people remain historical. Successor/namesake polity callbacks, one legacy rival,
  generalized diplomats/emissaries, visible polity route endpoints, and polity clashes are
  positive `AUTHOR-DEFERRED` targets in `VISION.md`'s canonical polity scope matrix, not current runtime or v1
  blockers. Exact old-actor continuation, persistent unloaded armies/caravans, automatic opposition
  war, offscreen conquest/casualties, and mass background simulation are rejected rather than
  missing.
- Stage moves in **both directions** now. It climbs on the reading and falls only on a clear
  shortfall, one rung per reckoning, with Camp an absolute floor. A city that subsides is the
  system working; a city that subsides while it is inside its 20% band is a bug worth filing.
  The heart's color-independent withered/famished signs are live and tested in steps 55v7-55v8;
  a deprivation sign appearing on an ordinary work is a bug.
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
- **Plumbing is declared, and a refusal is the system working.** Two mains carrying different
  liquids that end up beside each other will say so, once, by name — that is the LIQUID LAW, not a
  bug. Lay a crossing piece where two lines are only meant to pass. A tap is what puts a vessel on a
  line; standing a cistern next to a main joins nothing. A pass reporting "my brine main merged into
  my fresh main" is the one bug worth filing loudly. A line moves water only between quarters that
  are both on it, only downhill, and only until the two ends stand at the same **fraction** of their
  capacity — a small cistern and a great one come level at the same fill, not the same drams.
- **A brownout is not a breakdown.** A work that went quiet because its network ran short says
  "there is not enough to go round"; a work that broke says something else, and the remedy is
  different. The stated order is forges and workshops → refining yards → comfort and lodging → food
  works → water works → the watch, newest built first within a rung. A pass reporting "the watch
  went dark while the forge was still lit" is a bug. Recovery says **nothing** — a settlement that
  announced every recovery would be a settlement that never stops talking about itself — so a pass
  reporting "it never told me the power came back" is correct behaviour.
