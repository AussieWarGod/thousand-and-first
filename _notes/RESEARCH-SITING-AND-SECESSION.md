# Where research knowledge sits — realm, city, or founder — under secession and exile

> **Mandate.** The author, on the research system's biggest open question (2026-08-22): *"we
> should dig into where research should sit, especially with the abilities for cities to secede,
> or kick you out."* This is that dig. It answers `_notes/RESEARCH-SYSTEM-DESIGN.md` §12 Q1
> (per-realm vs per-city research) with the secession/exile machinery now shipped, and it comes
> back — per the house method — as options and a recommendation, never as code.

## Provenance and evidence standard

- **`T/…`** — this mod's tree at `/home/r/work/thousand-and-first/`, cited `file:line`.
- **`D/…`** — the Caves of Qud decompile at
  `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`, cited `file:line`. Every
  vanilla claim below was re-read there for this document.
- Sentiment claims carry URLs; Reddit quotes carry `comment_url` per the archive's citation rule.
- Standing rulings are cited by addendum number from `_notes/BUILDING-CATALOGUE-BRIEF.md` and are
  not contradicted anywhere below.

---

## 0. The verdicts, up front

1. **The code truth is not what any document says it is.** The keepers' roster — every `disk:`,
   `machine:`, and `pattern:` key, and therefore the whole craft ladder — is stored in a single
   **game-global** string (`The.Game.GetStringGameState("r_TAF_KeepersRoster")`,
   `T/Growth/KingdomZoning.cs:39,664-669`). Not on the settlement. Not on `KingdomSystem`. On the
   save. Three consequences follow, and only one of them was chosen on purpose (§1.3).
2. **Secession already answers the question for half the knowledge kinds, correctly, for free.**
   Everything that lives in the `KingdomSettlement` container — the people-tallies behind
   `origin:`, `creed:`, `kept:`, and the planned `culture:`/`species:` — moves whole into
   `System.Seceded` and comes back whole on rejoin, because `Secede` moves the container and
   touches nothing else (`T/Core/KingdomCreed.cs:750-819`). **The state that sits in the right
   place is handled by the shipped machinery with zero secession-specific code.** That is the
   strongest implementability argument in this document and it points one way (§3).
3. **Exile currently hands the founder the entire tech base.** `KingdomSystem.Exile` captures the
   realm into `Exiled*` fields and clears the flat settlement state
   (`T/Core/KingdomSystem.cs:1020-1042`) — but never touches the roster, because the roster is
   not the realm's field to clear. A founder exiled at arclight who founds a new camp founds it
   **already holding every taught design, every certification, and the craft level they earned**
   (`Tech` derives from the same store, `T/Growth/KingdomZoning.cs:201-204`). The exile modal
   says *"The charter is taken from you"* (`T/Core/KingdomExileRules.cs:394`); the knowledge
   walks out the gate with them. Nobody ruled this.
4. **The comparables are close to unanimous, and the negative space is loud**: no well-liked game
   found *subtracts researched knowledge stock* when a territory leaves. Losing a place costs
   **flow** (the labs, the researchers, the output) and **access** (the buildings, the
   enrolment), never **stock** (the things already known) — and where a splinter state appears,
   it *copies* the parent's tech rather than taking it (§2). Sentiment: recoverable, in-world
   loss is a beloved story; erased spent effort is the rage-quit class.
5. **Recommendation (§3.5): Option D — a founder-held ledger of *leads* over city-held *holdings*
   and *access* — implemented as Option C's mapping**: each knowledge kind sited where it
   naturally lives, with the stored roster moved off the game onto the `KingdomSettlement`
   record. Secession and exile then handle knowledge **by construction**, through `Seceded` /
   `Exiled*` / `Capture`/`Restore` — the mesh condition (Addendum 13) satisfied rather than
   strained — and Addendum 18's seed-not-ceiling clause generalises cleanly to exile: the founder
   walks out with seeds, never with holdings.

---

## 1. Code truth — where every kind of knowledge sits today, and what secession and exile do to it

### 1.1 The kinds, and their stores

| Kind | Minted by | Store | Effective scope today |
|---|---|---|---|
| `disk:` | teaching a data disk (`T/Growth/KingdomZoning.cs:643-652`) | `r_TAF_KeepersRoster` game-state string (`:39,664-669`) | **game-global** |
| `machine:` | certifying a hauled machine — `RecordCertification` → `Learn` (`T/Growth/KingdomZoning.cs:187-196`, called from `T/Growth/KingdomSalvage.cs:137`) | same string | **game-global** (the certified *machine* also carries a per-object property flag, `T/Growth/KingdomSalvage.cs:17` — the physical half stays with the machine) |
| `pattern:` | ceremonies (logical `T/Experience/KingdomCeremony*.cs`, symbol `FreezePatternBook`; kind in `T/Experience/KingdomCeremonyRules*.cs`, symbol `PatternKnowledgeKind`) | same string | **game-global** |
| `origin:` | read **live** off `System.OriginCounts`, never stored (`T/Growth/KingdomZoning.cs:118-138`) | `KingdomSettlement` container (`T/Core/KingdomSettlement.cs:240`) | **per-city** (the seat's), leaves with the people — by design: *"a trade the settlement holds only because somebody from that country lives here should leave with them"* (`:113-115`) |
| `creed:` / `kept:` | read live off `CreedCounts`/`CreedPastCounts` via `BuilderRollOf` (`T/Growth/KingdomZoning.cs:240-247`) | settlement container (`T/Core/KingdomSettlement.cs:246,255`) | **per-city** |
| planned `node:` (held) | `KingdomZoning.Learn` on node completion | *"appended to the existing roster string. No new key, no new format"* (`RESEARCH-SYSTEM-DESIGN.md` §10.4) | **game-global as designed** |
| planned discovery bit | `JournalObservation.Revealed` (`RESEARCH-SYSTEM-DESIGN.md` §6.2) | vanilla journal — fields verified at `D/Qud/API/IBaseJournalEntry.cs:10-31` | **founder-held** (the player's save; survives secession, exile, refounding) |
| planned `rite:` | founder's water-ritual history | *"permanent (a thing learned is learned)"* (`RESEARCH-SYSTEM-DESIGN.md` §5.5b) | **founder-held** |
| planned `culture:`/`species:` | live off the people (`RESEARCH-SYSTEM-DESIGN.md` §5.6 dial 3) | settlement tallies | **per-city** |
| planned `savant:` | a lodged notable, held only while they stay (§5.5 arm 5) | live | **per-city, revocable** |

The gate machinery itself is siting-agnostic: `Knows(IEnumerable<string> Roster, string
Requirement)` takes whatever roster it is handed (`T/Growth/KingdomZoningRules.cs:748-773`), and
every caller funnels through `KingdomZoning.Roster(System)` — stored string **plus** the seat's
live `origin:` keys (`T/Growth/KingdomZoning.cs:118-138`; callers at `:203,218,495,556,643`,
`T/Core/KingdomTechMap.cs:53`, logical `T/Experience/KingdomCeremony*.cs` (`FreezePatternBook`),
`T/Experience/KingdomCeremonyRules.cs:614`). **Re-siting the store is a change to two private
functions (`Stored`/`Store`, `:663-670`) and to nothing in the gate vocabulary.**

### 1.2 What secession does to each kind

`Secede` moves the leaving city's **whole `KingdomSettlement` container** into `System.Seceded`
and touches nothing else — *"Nothing physical is touched … no zone is stripped … They simply are
not yours"* (`T/Core/KingdomCreed.cs:756-766,794-807`). So today:

- **`origin:`/`creed:`/`kept:` (and the planned `culture:`/`species:`/`savant:`)** — the leaving
  city takes its own, keeps them while it is gone, and brings them back whole on rejoin
  (`TryRejoin` moves the container back, `:841-880`). The Charter's own prose already promises
  this: *"it still keeps everything it kept"* (`:910-912`). **Correct by construction — no
  secession code mentions knowledge at all.**
- **`disk:`/`machine:`/`pattern:` (and planned `node:`)** — untouched. The seceding city walks
  away with **zero** stored knowledge, whatever was taught, certified, or (in future) researched
  on its ground; the realm keeps every key, including certifications whose physical machines now
  stand on seceded ground (the machine goes with the ground; its knowledge stays in the string —
  the shipped one-way rule, *"taking the machine back off the grid later returns the machine to
  the founder, not the knowledge to nobody"*, `T/Growth/KingdomZoning.cs:178-183`).
- **Craft (`TechLevel`)** — derived from the global string (`:201-204`), so secession cannot move
  it in either direction.

### 1.3 What exile does to each kind

`Exile` is *"secession, realm-scoped"* (`T/Core/KingdomExileRules.cs:49-53`): the realm's
identity, standings, and **both settlement containers** are captured into `Exiled*` fields, the
flat fields are cleared by seating a blank settlement, and the Charter ability is removed
(`T/Core/KingdomSystem.cs:1020-1046`). So today:

- **Container-held kinds** — go with the realm into `ExiledSeat`/`ExiledAway`, restored intact by
  `TryReturn` (`:1067-1114`). Correct by construction, again.
- **The roster string — every `disk:`/`machine:`/`pattern:` key** — is on `The.Game`, which exile
  does not and cannot touch. Confirmed by exhaustive grep: `r_TAF_KeepersRoster` is read and
  written in exactly one file and **never cleared by anything** — not exile, not secession, not
  refounding. Consequences:
  1. An exiled founder who **returns** finds the knowledge intact — fine, same realm.
  2. An exiled founder who **founds again** starts the new realm holding the old realm's entire
     roster and craft level. `Learn`'s announcement will greet the new camp's first taught disk
     with whatever `TechLevel` the old realm had earned. The exile modal's *"The charter is taken
     from you. The ground is not yours, the stores are not yours"* (`T/Core/KingdomExileRules.cs:394`)
     is true of everything **except the tech base**, which follows the founder like a rite key.
  3. The `ReturnVerdict.FoundedAgain` door-shut rule (`:206-209`) means a founder cannot hold
     both — but the *knowledge* effectively held both all along.

### 1.4 Three prose-vs-store mismatches, on the record

These are the documentation debts any siting ruling must clear in the same commit
(documentation rule: update the doc in the same PR as the behaviour):

| Standing text | The store's truth |
|---|---|
| *"Realm-wide on purpose — what the keepers learned travels with the founder's own people to the founder's other city"* (`T/Growth/KingdomZoning.cs:36-38`) | The store is game-wide, not realm-wide. The sentence was written before exile existed; exile makes the difference observable (§1.3.2). |
| MODDING.md's published contract: *"Comma list of things **the settlement** must know"* (`T/MODDING.md:230`) | The settlement knows nothing; the save does. Third-party `Learn` callers were promised settlement semantics. |
| The keepers' map is titled with the **seat city's** name — `Header(System.SeatName, …)` (`T/Core/KingdomTechMap.cs:57`) — and `DecodeRoster`'s own doc says *"the settlement's stored roster"* (`T/Growth/KingdomZoningRules.cs:697-703`) | Both render realm/game-global state under a per-city name. `RESEARCH-SYSTEM-DESIGN.md` §6.2 continues the pattern: *"held-ness is a property of the CITY"* — cited to the game-global store. The words have always wanted city-siting; only the store disagrees. |

### 1.5 What the planned research system adds to the stakes

- **v1**: a node's whole effect is minting a roster key (`RESEARCH-SYSTEM-DESIGN.md` §0.1) — so
  wherever the roster sits, *all* research holdings sit.
- **v5 / Addendum 18**: the research **tier is checked against the CITY's researchers, never the
  founder** (§0.5, §5.5b) — the *doing* of research is already emphatically per-city; only the
  *keeping* is up for ruling.
- **§6.2's split** is already the right doctrine, stated: *"held-ness is a property of the CITY
  and revealed-ness is a property of the founder's knowledge of the world"* — discovery in the
  founder's journal (vanilla's own player-held ledger: `Revealed`, `LearnedFrom`, `Tradable`,
  `D/Qud/API/IBaseJournalEntry.cs:10-31`), holdings in the roster. The journal side survives
  secession, exile, and refounding *by vanilla's own design*, and that is a feature (§2.4).
- **Addendum 12(j)** already schedules the generalisation of the seat+Away pair to an N-city
  **roster of settlement records** — the container this document proposes to site knowledge on
  is the one container that scales with that wave for free.
- Vanilla's own "known recipes" are game-held statics (`[GameBasedStaticCache]`,
  `D/XRL/World/Tinkering/TinkerData.cs:39-42`, saved via `TinkerItem.SaveGlobals`,
  `D/XRL/World/Parts/TinkerItem.cs:239`) — i.e. vanilla sites *the player's personal* knowledge
  exactly where our *city* knowledge accidentally sits. The accident imitates the wrong
  precedent: the founder's knowledge belongs there; the keepers' does not.

---

## 2. Comparables — praise first, negative space mined

The question asked of the corpus: **when a game takes a territory that held your
technology/institutions, what exactly is lost, and how do players receive it?** Distinguishing
three losable things — **stock** (what is already known), **flow** (research output), **access**
(the standing institutions and the right to use them).

### 2.1 CK3 — knowledge sited on a PEOPLE, and praised for it

CK3 moved technology off realms and provinces entirely: innovations belong to the **culture**,
shared by every realm of that culture, and — the load-bearing datapoint — **regional innovations
"remain unlocked even if said territory is lost"**
([CK3 wiki, Innovation](https://ck3.paradoxwikis.com/Innovation)). The redesign away from CK2's
province-tech is received as a flat improvement
([GameWatcher guide](https://www.gamewatcher.com/news/crusader-kings-3-culture-and-innovations)).
**Transfer:** knowledge that lives with *who your people are* rather than *what ground you hold*
is a shipped, liked shape — and it is exactly the shape the mod's live people-keys
(`origin:`/`creed:`/`culture:`) already have. CK3 says: keep siting knowledge on people and their
records, and territory loss stops being a knowledge event at all.

### 2.2 Stellaris — stock is never subtracted; the rage attaches elsewhere

Stellaris technology is empire-held and permanent; a seceding/rebelling planet never removes a
researched tech — the splinter **copies** tech into its new empire (players report rebels
spawning with the parent's or even a Fallen Empire's tech:
[reddit 209↑, 1.00](https://www.reddit.com/r/Stellaris/comments/98o8fb/rebels_made_a_regular_one_planet_empire_with/)).
What players actually rage about in revolt threads is **unfair flow/asset loss and lost agency**,
never knowledge: losing a fleet to a scripted flip
([303↑, 0.98](https://www.reddit.com/r/Stellaris/comments/15qj6og/i_lost_a_massive_fleet_and_all_my_planetary/)
— author reloads), implausible rebel fleets
([907↑, 0.97](https://www.reddit.com/r/Stellaris/comments/17dp0qy/why_are_rebellions_still_broken/)),
and things happening *"with no notification for me"*
([1359↑, 0.99](https://www.reddit.com/r/Stellaris/comments/111qxay/why_did_my_vassal_suddenly_lose_half_its_systems/)).
**Transfer:** (a) the genre's splinter idiom is *copy, never take* — a seceding city that keeps
what its own people learned, without stripping the realm, is the expected shape; (b) whatever is
ruled, the loss must be **announced and legible** — which the brink ladder already guarantees
(`T/Core/KingdomCreed.cs:445-461`).

### 2.3 RimWorld — stock is faction-global; the loss of a PLACE is a beloved story

Research is faction-wide across colonies
([RimWorld wiki, Research](https://rimworldwiki.com/wiki/Research);
[gamepressure multi-colony guide](https://www.gamepressure.com/newsroom/rimworld-how-to-build-multiple-colonies-tips-and-tricks/z54f92));
losing a settlement never subtracts it. And colony loss itself is a celebrated genre —
"story time" threads at
[287↑, 1.00](https://www.reddit.com/r/RimWorld/comments/flb5ke/how_my_colony_went_from_thriving_to_completely/)
and [462↑, 0.98](https://www.reddit.com/r/RimWorld/comments/1djf5rt/what_was_your_quickest_colony_wipe_in_rimworld/).
Inside the recover-from-loss thread
([144↑, 1.00](https://www.reddit.com/r/RimWorld/comments/b9yuph/how_do_you_mentally_recover_from_having_a_colony/)),
the top answer is *chronicle it*: "Write a nice story chronicling their adventures … Remember
them fondly" (90↑,
[comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek7v2ll/)); "Losing a base you
worked hard on is your right of passage" (11↑,
[comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek87fng/)); "Losing is the most
fun part" (7↑, [comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek7uyv5/)). The
split is honestly present — "once a few of my favorite colonists get killed … you best believe
I'm gunna reload" (11↑, [comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek8cab3/);
also 5↑ [comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek80l3y/)) — and the
single most transferable comment names the variable that decides which side of the split a loss
lands on:

> "The one thing I miss about DF in Rimworld is the world persistence. In DF, losing a fortress
> doesn't feel too bad, cause A. You can resettle the ruins … B. Investigate the ruins as an
> adventurer, and try and recover the most valuable artifacts. When you lose in Rimworld, the
> whole world gets deleted." — 6↑,
> [comment](https://www.reddit.com/r/RimWorld/comments/b9yuph/_/ek8aphc/)

**Transfer:** loss is a story when the lost thing **persists in the world and can be walked back
to** — which is precisely the mod's shipped secession ("Stand on their ground and ask, when what
split you is no longer true", `T/Core/KingdomCreed.cs:912`) and exile ("Walk back and it will
still be there", `T/Core/KingdomExileRules.cs:395`). A knowledge ruling that keeps the knowledge
*somewhere real* (the seceded city's own rolls, a rejoin that restores whole) rides this
sentiment; one that erases it fights it.

### 2.4 Against the Storm — the sovereign ledger is what makes losable places loved

The whole design is disposable specialised settlements over a permanent Smoldering-City ledger;
each settlement is self-contained and deleted when you move on, and the loop is praised
precisely because the *meta-knowledge* survives every abandonment
([Rogueliker review](https://rogueliker.com/against-the-storm-review/);
[Checkpoint review](https://checkpointgaming.net/reviews/2025/07/against-the-storm-review-rogue-settlements/);
[Adrian Hon's essay](https://adrianhon.substack.com/p/against-the-storm)). **Transfer:** a
founder-held ledger of *what has been learned about the world* (the §6.2 journal) is the thing
that makes losing a city survivable and even attractive. It must survive secession and exile —
and vanilla's journal already guarantees it does.

### 2.5 Dwarf Fortress — knowledge as objects and people, the mod's own idiom vindicated

DF sites knowledge in **scholars** (who know topics) and **written works** (quires, codices,
scrolls — physical objects, recorded permanently in Legends;
[DF wiki: Library](https://dwarffortresswiki.org/index.php/Library),
[Book](https://dwarffortresswiki.org/index.php/DF2014:Book),
[Scholar](https://dwarffortresswiki.org/index.php/DF2014:Scholar)). A fallen fortress's books
remain in the world to be reclaimed — the exact persistence the RimWorld commenter above pines
for. **Transfer:** the mod's acquisition shape (disks carried, machines hauled, peoples arriving
— DIVERSITY §2's artifacts-and-people thesis) is already DF's shape; siting the *records* of
that knowledge on the places and people that hold them is the same doctrine finished.

### 2.6 Anno 1800 / Songs of Syx / EU4 — flow loss, and the complaint register

- **Anno**: losing a specialised island in war costs the supply chain, never the unlocked
  blueprints; the response is retake-or-rebuild, and the complaint register is AI fairness, not
  loss of knowledge ([Steam: "About War and Islands"](https://steamcommunity.com/app/916440/discussions/0/1680315447979966831/)).
  `END-STATE-CITIES-RESEARCH.md` §3/§7.2 already established the deeper lesson (specialised
  sites are loved when the connection is cheap); this dig adds: they are also loved because
  **losing one never takes the empire's knowledge with it**.
- **Songs of Syx**: conquered-region collapse threads are logistics-and-opacity complaints
  ("still collapses every 2 days if i dont have an army there … nothing I can find that tells me
  why", [comment thread](https://www.reddit.com/r/songsofsyx/comments/1adqgd4/conquered_cities_collapsing/))
  — thin on knowledge, but consistent: the resented part of losing a place is *unexplained*
  loss, not loss.
- **EU4** institutions: embraced-institution permanence under province loss is asserted in
  guides but I could not pin an authoritative wording (§4); recorded as unverified rather than
  leaned on.

### 2.7 The negative space, stated

**Nobody ships "the seceding province subtracts your researched tech," and the searches for
players *wanting* it come back empty.** Paradox-forum proposals for "technological regression"
exist as suggestions (the thread at
[forum.paradoxplaza.com/…/technological-regression.897627](https://forum.paradoxplaza.com/forum/threads/technological-regression.897627/)
was unfetchable — bot wall; recorded in §4) — but no shipped, liked implementation was found in
any comparable. The genre treats **stock as sovereign-held and monotonic** (CK3 culture,
Stellaris empire, RimWorld faction, Civ, Anno), makes **flow and access** the losable things,
and where two polities split, **both keep a copy**. The one loved family of exceptions proves
the rule by shape: knowledge sited in **objects and people that persist in the world** (DF
books/scholars; Souls-like recoverable drops) — loss that is really *displacement*, walkable-to,
reclaimable. Design consequence: if the mod wants secession to bite research at all — and Design
B's purposeful cities give it reason to — the bite must be **access/flow-shaped or
displacement-shaped, never erasure-shaped**.

---

## 3. The options, question-shaped

Scored against: (i) the shipped secession/exile machinery — no new parallel state (the mesh
condition, Addendum 13); (ii) Design B's kingdom of purposeful cities
(`END-STATE-CITIES-RESEARCH.md` §7.1, capital ruling); (iii) the sentiment evidence (§2);
(iv) implementability under derive-before-author (Addendum 11(c): inherit-extend / wrap /
fill-in — here applied to our own machinery: extend the container that already carries state
through secession and exile, rather than authoring bespoke knowledge-handling into either).

### 3.A Realm-held (roster on `KingdomSystem`)

The intended reading of the shipped comment ("realm-wide on purpose"). One store per realm; all
labs push one subject; knowledge is shared realm-wide.

- **Secession**: costless to research. The leaver takes only its live people-keys; every taught
  design, certification, and node stays with the realm — including knowledge earned entirely on
  the leaver's ground. The flesh-city walks out and the realm still "knows" flesh; only the
  people-keys and the standing works are gone.
- **Exile**: takes *everything* from the founder — the realm keeps the roster in its `Exiled*`
  capture; a refound starts from nothing. (Requires actually moving the store onto the system;
  today's game-global store gives the **opposite** — §1.3.)
- For: cheapest delta from today (one field move + exile capture); keeps RESEARCH Q1's "one
  subject, no queue" trivially true; zero interaction surface.
- Against: makes Design B's specialised cities **cosmetically** specialised for research — the
  flesh-city is where flesh was *learned*, never where flesh is *known*; §1.4's per-city prose
  stays a lie; and "research where-it-was-done matters" (the author's stated interest in this
  dig) is answered "it doesn't." Exile-takes-all is also the *harshest* option for the founder,
  with no genre precedent — even Stellaris's splinters copy (§2.2), and the founder's journal
  would sit there remembering nodes their new realm must rediscover from zero.

### 3.B City-held (roster on `KingdomSettlement`; realm reads the seat)

Knowledge belongs to the city whose keepers learned it. The seceding city walks off with its
branch.

- **Secession**: the leaver's container carries its roster out — the realm *loses stock* for
  everything only the leaver knew. Rejoin restores it whole, free, because the container comes
  back (`T/Core/KingdomCreed.cs:862-864`). Exactly Design B's fiction: the flesh-city IS where
  flesh is known, and losing it means losing flesh until you mend the quarrel.
- **Exile**: both containers go into `Exiled*`; the founder keeps only journal leads and rite
  keys. A refound re-walks the tree, faster (leads + seeds). The exile modal becomes true.
- For: the strongest identity read; the settlement record is **the container the shipped
  machinery already carries** — secession, rejoin, exile, and return all handle it with zero
  knowledge-specific code (§1.2-1.3), and it scales to the 12(j) N-city roster for free. The
  by-name field carry means adding `Roster` to `KingdomSettlement` is picked up by
  `Capture`/`Restore`/`ReadFrom` automatically (`T/Core/KingdomSettlement.cs:24-44,358-366`).
- Against: **raw stock subtraction is the genre's negative space** (§2.7) — a realm that
  "forgets" crucible steel because a city left is erasure-shaped loss, the rage class; it also
  complicates "what the keepers learned travels with the founder's own people" — teaching the
  second city needs a defined vector; and it doubles the balance surface RESEARCH Q1 warned
  about *if* it is read as per-city trees (it need not be — one registry, per-city holdings).

### 3.C Split — every kind sited where it naturally lives

Not a compromise between A and B; a reading of the table in §1.1, which already splits:

| Kind | Natural home | Why |
|---|---|---|
| `disk:` (taught), `pattern:`, `node:` (held) | **the city whose keepers were taught** — settlement record | teaching is an act at a place; the disk itself stays a founder-carried object (deliberately not consumed, `T/Growth/KingdomZoning.cs:181` doc), so the founder can teach the *other* city by walking there — the travel story made literal |
| `machine:` (certification) | **the city that certified it** — settlement record; the machine's own property flag already sits on the object (`T/Growth/KingdomSalvage.cs:17`) | certification is per-machine-per-place already in its physical half; the knowledge half joins it |
| `origin:`, `creed:`, `kept:`, `culture:`, `species:`, `savant:` | **the city, live off its people** — already there | shipped; correct under secession today |
| discovery (`Revealed`), `rite:`, quest state | **the founder** — journal / permanent keys / `The.Game` quests | vanilla's own player-held ledgers (`D/Qud/API/IBaseJournalEntry.cs:10-31`; `D/XRL/XRLGame.cs` quest API); survives everything, and *should* |
| research subject + accrued ticks + shelf | **the city running the lab** (city model rows, per RESEARCH §10.1) | the lab is a work in a place; spent effort must never be erased by a border moving (§2.7) — it sits with the lab that spent it |

Under C, secession and exile fall out of the containers with no further rules — but C alone does
not answer *what the realm and the founder are left holding* when a city walks. That is D.

### 3.D Founder-held ledger + city-held ACCESS — the registry model (recommended doctrine)

C's mapping, plus one doctrine on top, taken from the mod's own record. The end-state research
found the community's spontaneous fiction for Qud's deepest knowledge-gate is **enrolment**: the
becoming nook checks *a record*, not a body — "a specific marker gene that says 'this person is
an aristocrat'", the machines checking a credential, "WELCOME ARISTOCRAT" as caste recognition
(`END-STATE-CITIES-RESEARCH.md` §1.7 R-B, with comment URLs). Knowledge, in this mod's register,
is **rolls a city keeps and a ledger a founder carries**:

- **The founder holds the ledger of the world**: discovery (journal `Revealed` + `LearnedFrom`),
  `rite:` keys, quest state. It is never taken — not by secession, not by exile. This is
  Addendum 18's clause generalised: *the founder opens the door; the city walks through* — so
  what a founder carries out of exile is **doors, never rooms**: every node their old realm held
  is already *revealed* in their journal, and (proposal) seeds its head node in a new realm with
  the same `SeedFraction` head-start machinery rites use (RESEARCH §5.5b — one mechanism, no new
  state). A refounded realm re-walks the tree at speed; it does not start holding it. The tier
  clause already guarantees the founder can never *be* the tree (§5.5b: "checked against the
  CITY's researchers, never the founder").
- **Each city holds its rolls**: what its keepers were taught, certified, completed — C's
  settlement-record roster — plus its live people-keys. Secession takes the leaver's rolls with
  the leaver (they are its rolls); rejoin brings them home whole. And the rhyme the author's
  brief names lands for free: **a seceded chrome-city has struck the founder from its rolls** —
  the annexe still stands, its keepers still know, and none of it is *yours* — access revoked,
  knowledge intact, exactly the enrolment fiction.
- **The realm holds nothing directly** — it *reads*. `Roster(System)` becomes a read over member
  cities' rolls (see Q4 for the two candidate reads), and the realm-level facts (craft level,
  the map) are derived, like every other realm readout.
- **The secession bite, priced by the doctrine**: what only the leaver knew stops being *held*
  by the realm — but it does not vanish from the founder's ledger. Every such node/design stays
  **revealed** with its provenance line (*"— learned in Sotham's Rest"*), re-walkable at a
  seed's head start, and restored whole on rejoin. Loss is displacement, not erasure —
  the shape §2.3/§2.5/§2.7 found players love. The Stellaris copy-idiom is honoured on both
  sides: the leaver keeps what it knows; the realm keeps the memory of it.

**Scoring D:** (i) mesh — no new state kinds at all: it re-sites one string onto the container
the machinery already carries, and reuses the journal and seed machinery the research design
already commits to; secession/exile/rejoin/return handle knowledge with **zero** new code in any
of the four paths. (ii) Design B — the specialised city is genuinely the place that knows its
craft; "research where-it-was-done matters" is answered *yes, and it shows*. (iii) Sentiment —
stock is never erased (founder ledger + rejoin-whole + seeds); loss is access/flow/displacement;
the loss is announced by the brink ladder that already exists. (iv) Implementability — one field
on `KingdomSettlement` (auto-carried by the by-name exchange), two private functions retargeted,
`Roster(System)` unioned or seat-read, and the §1.4 doc lines rewritten. The delta is small
because the machinery was built container-first; this is the mesh condition *rewarding* the
architecture.

### 3.5 Recommendation

**Adopt D, implemented as C's mapping.** Concretely, in shipping order:

1. Move the stored roster from `The.Game` onto `KingdomSettlement` (new field, auto-carried;
   one-time migration folds the game-global string into the current seat's record — Addendum 9
   waives pre-release migration niceties, but the fold is one read).
2. `Roster(System)` reads per Q4's answer; `Learn` writes to the seat (the keepers being taught
   are the keepers in front of the founder).
3. Exile: nothing to do — the containers already go. Delete nothing; the founder's journal and
   `rite:` keys are already theirs. Add the refound-seeding rule (Q3) when the research wave
   lands.
4. Rewrite the three §1.4 prose sites in the same commit.
5. The research wave then lands its `node:` keys into an already-correct store.

---

## 4. What I could not find

- **The Paradox "Technological Regression" thread** (and Paradox forums generally) — bot-walled
  to WebFetch; player reception of *proposed* losable tech is asserted nowhere above.
- **EU4 embraced-institution permanence under province loss** — widely stated in guides, not
  pinned to wiki/dev wording in the time spent; used only as colour, marked unverified (§2.6).
- **Songs of Syx secession sentiment at depth** — the corpus surfaced is thin (small subreddit);
  the logistics-complaint reading rests on few threads.
- **A shipped, liked "seceding region subtracts researched stock" mechanic** — searched for
  directly (Stellaris, Paradox titles, 4X broadly) and not found; §2.7's negative-space claim is
  absence-of-evidence and is stated as such.
- **Frostpunk 2 colony-loss knowledge sentiment** — not dug; FP2's lock-in sentiment is already
  covered in `END-STATE-CITIES-RESEARCH.md` §3.4 and nothing suggested a knowledge-siting angle.
- **Reddit scores** come from the Arctic Shift archive and may lag live Reddit (tool's own
  caveat).

---

## 5. Questions only the author can rule

**Q1 — The siting ruling itself.** A / B / C / D (§3). D is recommended. This supersedes
RESEARCH-SYSTEM-DESIGN §12 Q1's framing ("per-realm vs per-city") with a finer split: the
*doing* is already per-city (v5), the *discovery* is already founder (§6.2); this rules the
*keeping*.

**Q2 — What does the realm keep when a city secedes with knowledge only it held?**
(a) nothing — pure B; (b) **leads**: every such node/design stays revealed in the founder's
journal with provenance, re-walkable at a seed head start — recommended; (c) a full copy — the
Stellaris idiom, secession costless to research (collapses D toward A for the realm's half).

**Q3 — What does an exiled founder carry to a refound?** (a) everything — today's accidental
behaviour; (b) nothing but `rite:` keys; (c) **leads + seeds**: journal reveals survive (they
must — vanilla owns that store) and each formerly-held node seeds its head node once in the new
realm, on the rite machinery — recommended (Addendum 18 generalised: doors, never rooms).

**Q4 — What does `Roster(System)` read?** (a) **seat-only**: knowledge is where it was taught,
and teaching the other city is an act — recommended for the fiction, and it makes the founder
the vector ("what the keepers learned travels with the founder's own people" becomes literally
true: carry the disk, walk, teach); (b) union of seat+Away (and later, all member cities):
realm-wide sharing preserved, secession still bites, but "where-it-was-done" stops mattering
inside the realm. If (a): is the cross-city teaching automatic on arrival (free, invisible), a
disk-teaching-shaped act per design, or carried by a person (a keeper on the road)? The disk's
not-consumed rule (`T/Growth/KingdomZoning.cs:181`) already makes disks re-teachable; machines
and nodes need the ruling.

**Q5 — Certification's knowledge half under Q4(a).** The certified machine's flag is on the
object; the `machine:` key would sit with the certifying city. Confirm: a machine *re-certified*
in the second city re-mints there (idempotent per city), and the one-way rule (knowledge
survives the machine's removal) stays per-city.

**Q6 — Rejoin restores the leaver's rolls whole and free** (the container already does this).
Confirm no re-learning tax — recommended: the Charter's "it still keeps everything it kept"
should hold for knowledge too, in both directions.

**Q7 — Craft (`TechLevel`) becomes per-city under any of B/C/D.** The map's header finally tells
the truth (§1.4), and each city has its own craft rung. Confirm the consequence: a design's
`MinTech` is judged against the city being built in — which is almost certainly what Addendum
15's per-stratum divergence wanted anyway.

**Q8 — `pattern:` keys (ceremonies)** — same siting as `disk:` (the city where the ceremony was
held)? Recommended yes; they are already minted through `Learn`.

---

## 6. Loud rejections

**LR1 — Erasure-shaped loss. Refused.** No path — secession, exile, machine removal, savant
departure — may delete a fact from every store at once. The founder's journal reveal is
monotonic (vanilla's `Reveal`/`Forget` notwithstanding, we never call `Forget` on node
observations). §2.7: spent effort and known facts vanishing is the one loss shape the corpus
shows players punishing. (The `savant:` *holding* lapsing while they are gone is access, not
erasure — the node returns with them; RESEARCH §5.5 arm 5 stands.)

**LR2 — Patching exile by clearing the game-global roster. Refused.** It answers §1.3 by
erasure (LR1), breaks `TryReturn` (the restored realm would come back ignorant —
`T/Core/KingdomSystem.cs:1067-1114` restores containers, and a cleared global store is in none
of them), and leaves the store in the wrong place for the 12(j) N-city wave. The fix is siting,
not clearing.

**LR3 — Per-city research TREES. Refused.** One registry, one visibility law, one tier ladder;
what is per-city is *holdings*, *subjects*, and *rolls*. Forked trees are the "doubles the
surface" cost RESEARCH Q1 warned about, and nothing in the author's ask requires them.

**LR4 — A knowledge-transfer screen, caravan, or sync meter. Refused.** If Q4(a) is chosen, the
transfer verb is the founder walking and teaching — an act at a building, like every decision in
this mod (RESEARCH §0.3). A "share knowledge" management surface is the second job returning in
a librarian's robe.

**LR5 — Founder-held *holdings*. Refused, already.** Addendum 18's tier clause and RESEARCH
§5.5b: the founder seeds and reveals, never completes and never carries completion. Today's
game-global store is this rejection violated by accident; the ruling should end it.

---

## Appendix — what was read

**This mod:** `Growth/KingdomZoning.cs`, `Growth/KingdomZoningRules.cs`,
`Growth/KingdomSalvage.cs`, `Core/KingdomCreed.cs`, `Core/KingdomExileRules.cs`,
`Core/KingdomSystem.cs`, `Core/KingdomSettlement.cs`, `Core/KingdomTechMap.cs`,
`Core/KingdomTechMapRules.cs`, logical `Experience/KingdomCeremony*.cs` (grep),
`Experience/KingdomCeremonyRules.cs` (grep), `MODDING.md:174-344`;
`_notes/RESEARCH-SYSTEM-DESIGN.md` (whole), `_notes/BUILDING-CATALOGUE-BRIEF.md` (Addenda 11-20,
capital ruling), `_notes/END-STATE-CITIES-RESEARCH.md` §§1.7, 3, 4.5-4.7, 5.1, 6-7.

**Decompile:** `Qud/API/IBaseJournalEntry.cs:10-31` (journal entry fields — re-verified),
`XRL/World/Tinkering/TinkerData.cs:16,39-42` (`[GameBasedStaticCache]` — re-verified),
`XRL/World/Parts/TinkerItem.cs:239` (`SaveGlobals` — re-verified).

**Web:** CK3 wiki (Innovation, Culture), GameWatcher CK3 guide, RimWorld wiki (Research,
Colony), gamepressure multi-colony guide, DF wiki (Library, Book, Scholar), Against the Storm
reviews (Rogueliker, Checkpoint, Metacritic, Adrian Hon), Anno 1800 Steam war/island threads,
Stellaris Steam/PDX threads (titles only where unfetchable). **Reddit** (via the Arctic Shift
MCP archive; comment URLs inline): r/Stellaris ×7 threads, r/RimWorld ×7 threads with one
20-comment extraction, r/songsofsyx ×4 threads.
