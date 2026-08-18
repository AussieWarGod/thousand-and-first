# The Thousand and First — Test Session Protocol (v0.1.0)

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
| 8 | Drop 1–2 filled waterskins / pour water into a container on the ground in the claimed zone | `kingdom:status` shows drams stored |
| 9 | `kingdom:grow` | Settler arrives (with an origin), water −2 drams, chronicle line |
| 10 | Repeat 9 to 5 settlers with 16+ drams stored | **Steading** stage-up popup + journal accomplishment |
| 11 | Empty the stores (pick containers up), `kingdom:grow` | Thirst warning (red), chronicle "thirsted" line, streak 1 |
| 12 | `kingdom:grow` again, still dry | A settler **emigrates** ("left for wetter country"), pop −1 |
| 13 | Refill water, `kingdom:grow` | Streak resets; arrivals resume |
| 14 | If the zone has a fresh-water pool: `kingdom:status` before/after `kingdom:grow` | Settlers fetch pool water into containers (stored rises, open falls) |

## Pass 3 — The rite (no wishes)

| Step | Action | Expect |
|---|---|---|
| 15 | (On a save with no kingdom) wish `r_FounderBasin`, fill it with 8+ drams of water | Inventory action **found a settlement** appears |
| 16 | Use it, name the settlement | Ceremony popup; founded + zone claimed; basin drained by 8 |

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
