# Quest handling across succession — research return for Addendum 22 riders C5/C6

> DRAFT — research return, not yet reviewed by the author. Do not treat any recommendation
> here as pinned. Scope: what happens to vanilla quest state, journal state, and world state
> across a Kingdom Mode succession, and what the founder's corpse restores.
>
> Confirmed ground this must compose with (Addendum 22, BUILDING-CATALOGUE-BRIEF.md:1141-1149):
> **C5** — journal mass-forget on succession, re-reveal of the realm's own ground; rider: the
> corpse is a journal-restore point, quest handling needs research. **C6** — quests persist in
> v1; rider: needs revisit + research to properly scope. **C7** — kit stays where it fell.
> **C8** — crossover at the mourning rite (interregnum).
>
> Citation prefixes: `D/` = decompile root
> `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`; `B/` = game XML
> `/home/r/coq/qud_helper/game_base/Base/`; `T/` = this repo.

---

## 0. Verdicts, up front

1. **C6 (persist in v1) is not merely conservative — it is what the engine is built for.**
   Quest state lives entirely on `XRLGame`, never on the body, and every quest system is an
   `IPlayerSystem` that *automatically re-registers its player-object event handlers when the
   player body changes* (`D/XRL/IPlayerSystem.cs:42-53`). Domination, Vehicle piloting, and the
   Thin World already swap the player mid-quest constantly. Persisting quests across a
   succession body-swap requires **zero code** and carries zero engine risk.
2. **There IS a shipped whole-quest fail path** — `XRLGame.FailQuest` (`D/XRL/XRLGame.cs:1141-1153`)
   — but it has a trap that kills force-abandon as a policy: a failed quest is added to
   `FinishedQuests` (:1148), and `HasFinishedQuest` is a bare `FinishedQuests.ContainsKey`
   (:1206-1209), so **failing a quest satisfies every downstream `IfFinishedQuest` conversation
   gate** (`D/XRL/World/Conversations/ConversationDelegates.cs:222-224`). Mass-failing the
   founder's quests on succession would fling open dialogue gated on their completion. Loud
   rejection below (§7).
3. **C5's "mass-Forget" cannot reach the map.** `JournalMapNote.Forgettable()` returns `false`
   unconditionally (`D/Qud/API/JournalMapNote.cs:305-308`), so the vanilla `Forget()` API
   (`D/Qud/API/IBaseJournalEntry.cs:167-175`) is a no-op on every map note. The confirmed C5
   mechanism, as worded, forgets secrets, rumors, recipes and (most) sultan lore — and leaves
   the founder's entire chart intact. The author must re-rule this cell (§9 Q2).
4. **The corpse already is an identity-bearing restore point in vanilla.** The `Corpse` part
   stamps the corpse object with `CreatureName`, `SourceID` (the dead GameObject's ID),
   `SourceBlueprint`, `KillerID`, `DeathReason`, and `FromGenotype`
   (`D/XRL/World/Parts/Corpse.cs`, `ProcessCorpseDrop`, :140-175). And under C7, the founder's
   *quest items* (the prism, the wire, the knickknack, the data disk) physically lie on/around
   the corpse — **the corpse is already the quest-restore point, through C7, with no new
   machinery**. What the corpse-read adds is the journal (§3).
5. **"State of the world in regards to quest handling" resolves cleanly: the world carries on;
   quests are a ledger over it.** Quest-spawned dungeons, NPCs, battles, and settlements are
   world/zone state that persists regardless of quest bookkeeping (§5). Persist-all keeps
   ledger and world consistent; every abandon/suspend scheme desyncs them.

---

## 1. Code truth — vanilla quest machinery end to end

### 1.1 The ledger

- `XRLGame.Quests` and `XRLGame.FinishedQuests` are `StringMap<Quest>` on the game object
  (`D/XRL/XRLGame.cs:145,148`). Nothing quest-shaped lives on the player GameObject.
- A `Quest` (`D/XRL/World/Quest.cs:11-56`) carries: ID/Name, `Level`, `Finished`,
  reward fields (`Factions`, `Reputation`, `Accomplishment`/`Hagiograph`/`Gospel`,
  `Achievement`), **`QuestGiverName` / `QuestGiverLocationName` / `QuestGiverLocationZoneID`**
  (auto-captured at StartQuest from the player's current zone, `D/XRL/XRLGame.cs:557-576`),
  `StepsByID`, an optional `QuestManager` IPart (serialized with the quest, :296-304), an
  optional `IQuestSystem` type, and free-form `Properties`/`IntProperties`.
- `QuestStep` (`D/XRL/World/QuestStep.cs:8-35`): `Finished`, `Failed`, `Awarded` (XP given),
  `Optional`, `Hidden`, `XP`.
- Dynamic village quests are blueprint-copied out of `DynamicQuestsGameState`
  (`D/XRL/World/DynamicQuestsGameState.cs:15-21`), then live in the same `XRLGame.Quests` map.

### 1.2 Every mutation point

| Mutation | Where | What it does |
|---|---|---|
| `StartQuest(Quest/string)` | `D/XRL/XRLGame.cs:548-597` | Dedupes via `TryGetQuest` (:550-553 — a persisted quest can never double-start), copies from `QuestLoader`/dynamic state (:592-594), records giver location, fires `QuestStartedEvent` (:580) |
| `FinishQuestStep` | :758-836 | Sets step `Finished=true, Failed=false`; **awards XP to `The.Player`** — whoever holds the body at completion time (:814); fires `QuestStepFinishedEvent` (:820); auto-checks quest completion (:822-825) |
| `FailQuestStep` | :871-901 | Sets `Finished=false, Failed=true`; no quest-level consequence by itself |
| `CheckQuestFinishState` | :836-857 | All non-optional steps finished → `FinishQuest` |
| `FinishQuest` | :661-756 | Sets `Finished=true`, adds to `FinishedQuests` (stays in `Quests` too), stamps `QuestFinishedTime_` gamestate (:670), runs system `Finish()` + dynamic reward, **writes the journal accomplishment/hagiograph** (:693-726), **modifies `PlayerReputation`** (:727-734), unlocks achievement, fires `QuestFinishedEvent` (:740) |
| `ForceFinishQuest` | :628-637 | If not started: inserts a **blank `new Quest()`** with null ID/StepsByID into `FinishedQuests` (:636) — anything dereferencing it throws (trap already documented, `T/_notes/RESEARCH-SYSTEM-DESIGN.md` §2.10) |
| `FailQuest` | :1141-1153 | Whole-quest fail: `FinishedQuests.Add` + `Quests.Remove` + popup + `Quest.Fail()` → `IQuestSystem.Fail()` + system removal if `RemoveWithQuest` (`D/XRL/World/Quest.cs:227-237`, `D/XRL/IQuestSystem.cs:34,50`). **Does NOT set `Finished=true` and does NOT stamp `QuestFinishedTime_`** — the only way to tell a failed quest from a finished one afterward |
| Raw removal | `D/XRL/World/Quests/LandingPadsSystem.cs:533-559` | `ResetSlynthQuest` (wish/debug): removes from **both** maps + removes the finish-time gamestate + manually resets every system field + respawns the slynth. The shipped precedent for total quest erasure — note how much manual system-state repair it needs |

There is **no `QuestFailedEvent`** and no quest-removed event (searched `D/XRL/World/` — only
`QuestStartedEvent`, `QuestStepFinishedEvent`, `QuestFinishedEvent`, `OnQuestAddedEvent`).

### 1.3 Fail/abandon truth

- Whole-quest failure ships but is used **exactly twice**: "Kith and Kin" when a critical
  Hindren NPC dies (`D/XRL/World/Parts/HindrenMysteryCriticalNPC.cs:23-31`), and Reclamation's
  retry loop — which is not even a real fail: `FailAttempt` shows the fail popup, **renames the
  live quest** ("attempt #N") and re-shows the start popup, keeping all state
  (`D/XRL/World/Quests/ReclamationSystem.cs:583-590`). That rename is a shipped precedent for
  relabeling a live quest without touching its state (§7, option T).
- There is **no player-facing abandon verb anywhere** — the quest log (`D/Qud/UI/QuestsStatusScreen.cs:59-60,111-112`,
  `D/Qud/API/QuestsAPI.cs:18-24` — iterates `Game.Quests` only) offers no removal.
- The fail-satisfies-finished-gates trap (§0.2) is real and unguarded: `GetQuestGiverState`
  also returns 2 ("complete" indicator over the giver's head) for failed quests
  (`D/XRL/XRLGame.cs:1177-1204`). Vanilla dodges it because Kith and Kin's downstream logic
  keys off gamestates (`HindrenMysteryCriticalNPCKilled`, :25), not `IfFinishedQuest`.

### 1.4 What breaks if a quest is force-removed mid-flight

- **Dangling system**: `IQuestSystem.Quest` resolves lazily from `The.Game.Quests`
  (`D/XRL/IQuestSystem.cs:18-28`, `_Quest` is `[NonSerialized]`) — after a raw
  `Quests.Remove`, any system event handler dereferencing `Quest` gets null → NRE. Going
  through `FailQuest` avoids this only because `RemoveWithQuest` (default true) tears the
  system down with it. Systems with live world machinery (LandingPads day counters :540-544,
  Reclamation waves) need manual field resets — that is why `ResetSlynthQuest` is 25 lines.
- **Re-offer semantics**: `TryGetQuest` checks both maps (:1155-1162); remove from both and
  the quest giver's conversation will offer the quest afresh, `StartQuest` will happily
  restart it — and `Quest.Copy()` produces fresh steps with `Awarded=false`
  (`D/XRL/World/Quest.cs:370-388`), so **re-done steps re-award XP**. Removal-then-restart is
  an XP faucet priced at one founder per pull.
- **Conversation gates flip**: `IfHaveActiveQuest` → `HasUnfinishedQuest` (:1224-1231) goes
  false; `IfFinishedQuest` behavior depends on which map you removed from. Every dialogue in
  `B/Conversations.xml` written against these predicates changes meaning.
- **What does NOT break**: quest-spawned world state (dungeons, NPCs, items, battles) — it
  was never owned by the quest object (§5).

### 1.5 The engine already survives the player changing bodies mid-quest

`IPlayerSystem.HandleEvent(AfterPlayerBodyChangeEvent)` unregisters the quest system's
handlers from the old body and re-registers them on the new one
(`D/XRL/IPlayerSystem.cs:42-53`); every `IQuestSystem` inherits this
(`D/XRL/IQuestSystem.cs:8`). `GamePlayer.SetBody` fires exactly that event
(`D/XRL/World/GamePlayer.cs:105`). Kingdom Mode's succession swap rides the same rails as
Domination and Vehicle piloting — mid-quest, the machinery genuinely does not care who the
player is. This is the code-truth foundation under C6.

Two person-shaped residues do sit outside the game ledger:

- **Body-bound prerequisites.** The Mark of Death is an int property on the GameObject
  (`D/XRL/World/GameObject.cs:18127-18135`); the Tomb's guardians check the *body*
  (`D/XRL/World/Parts/AIMarkOfDeathGuardian.cs:24`). But the mark's *sigil* is a game-scoped
  gamestate (`GetStringGameState("MarkOfDeath")`, `D/XRL/World/GameObject.cs:18145`), so an
  heir can re-inscribe and continue the Tomb arc. Quest persists; body redoes its part.
- **NPC-side relationship state has no player identity.** `WaterRitualRecord` is a part on
  the NPC holding only depletion state — `secretsRemaining`, `giftedItem`, `numGifts`
  (`D/XRL/World/Parts/WaterRitualRecord.cs:11-39`, attached via
  `The.Speaker.RequirePart<WaterRitualRecord>()`,
  `D/XRL/World/Conversations/Parts/WaterRitual.cs:36`). The engine **cannot** tell an NPC the
  player is a different person now. Consequences: (a) the fiction leak under persist-all is
  bounded to dialogue tone — NPCs treat the heir as the founder; (b) a happy side effect:
  water-ritual gift/secret pools stay depleted, so dying is not a ritual-farming exploit.

---

## 2. Code truth — the journal

### 2.1 The store

`JournalAPI` holds **static** lists, serialized with the save
(`D/Qud/API/JournalAPI.cs:27-41,112-133`): `Accomplishments`, `Observations`, `MapNotes`,
`RecipeNotes`, `GeneralNotes`, `SultanNotes`, `VillageNotes`, plus `NotesByID`. Every entry
(`D/Qud/API/IBaseJournalEntry.cs:17-31`) carries `Revealed` (the discovery bit),
`LearnedFrom` (provenance string), `History` (appendable lines — vanilla stamps
"-learned from <faction>", `D/XRL/World/Conversations/ConversationDelegates.cs:1198-1213`),
`Tradable`, and a free-form `Attributes` list. The journal UI counts only `Revealed` entries
(`JournalAPI.cs:49`).

### 2.2 Forget machinery — and the map-note surprise

- `Forget()` un-reveals an entry **iff `Forgettable()`** and fires
  `SecretVisibilityChangedEvent` (`D/Qud/API/IBaseJournalEntry.cs:167-175`); `Reveal()` is its
  inverse (:177-186). Baseline `Forgettable()` is true (:188-191).
- **`JournalMapNote.Forgettable()` → `false`, unconditionally**
  (`D/Qud/API/JournalMapNote.cs:305-308`). Map notes, once revealed, are engine-permanent.
- `JournalSultanNote.Forgettable()` is conditional: notes whose historic event has
  `revealsRegion` / `revealsItem` / `revealsItemLocation` refuse to be forgotten
  (`D/Qud/API/JournalSultanNote.cs:91-111`) — i.e. even sultan lore that functions as *map
  knowledge* is protected.
- The vanilla mass-forgetter is the **Amnesia** defect, and it is the exact template for C5's
  two halves (`D/XRL/World/Parts/Mutation/Amnesia.cs`): secrets half — on secret-learn proc,
  forget a random known note, *gated on `Forgettable()`* (:61-75, forget at :95); layout half
  — on re-entering a stale zone, `zone.ClearExploredMap()` (:102-116). Second vanilla
  precedent: `EatMemoriesOnHit` (`D/XRL/World/Parts/EatMemoriesOnHit.cs:48`).
- Forgetting an **accomplishment** only hides it from the journal UI: the Tomb's mural
  builder reads `JournalAPI.Accomplishments` filtered on `MuralText` only, **not** on
  `Revealed` (`D/XRL/World/Parts/PlayerMuralController.cs:232-233`).

**What "mass-forget" would actually touch, kind by kind:**

| Kind | `Forget()` works? | Note |
|---|---|---|
| Observations (secrets, gossip) | Yes | The intended core of C5 |
| General notes, village notes | Yes | |
| Recipe notes (cooking) | Yes | Carbide-chef recipes; tinker recipes are separate (`TinkerData.KnownRecipes`, already ruled — re-derive per citizen, `T/_notes/SUCCESSION-RESEARCH.md` §5.1) |
| Sultan notes | Mostly | Region/item-revealing ones refuse |
| **Map notes** | **No** | Needs direct `Revealed=false` field writes + cache invalidation (`_mapNoteCategories`, `Tracked` pins) — fights engine intent, runtime-unverified (§10) |
| Accomplishments | Yes, but | UI-hide only; murals/gospel machinery unaffected; these are the run's *history*, not the person's knowledge |

### 2.3 Explored terrain is zone state, not player state

`Zone.ExploredMap` is a `bool[]` on each zone, serialized with the zone
(`D/XRL/World/Zone.cs:272,2967,3038`). A body swap touches none of it. So the heir "knowing"
every dungeon layout the founder ever walked is the **default**, and un-knowing it means
calling `ClearExploredMap()` (:4443-4446 — note :4448's obsolete method is even *named* for
legacy amnesia behavior) per zone, or lazily on entry as Amnesia does. Re-revealing the
realm's own ground on the world map is `AddExplored` on the world zone (:5056).

### 2.4 Provenance — can a corpse-restore distinguish the founder's discoveries?

Not out of the box — there is no per-entry "who learned this" field beyond the free-text
`LearnedFrom` (overwritten by each `Reveal`). But the extension surface is shipped:
`Attributes` is a per-entry string list already used semantically ("gossip", "sultan",
"village" — consumed by `Amnesia.AffectsSecret`, `Amnesia.cs:61-75`). The mod can stamp
forgotten entries with a namespaced attribute (e.g. `taf:founder:<n>`) at succession time and
the corpse-read reveals exactly that set. `History` keeps the audit line
(`AppendHistory`, `IBaseJournalEntry.cs:154`).

### 2.5 The reveal-verb family — reading the dead is already a Qud verb

Vanilla precedents for "an object/act reveals another mind's knowledge", all in
`D/XRL/World/Parts/`: `RevealNoteOnLook`, `RevealObservationOnLook`,
`RevealObservationOnRead`, `RevealVillageHistoryOnLook`, `SecretRevealer`,
`VillageHistoryBook`, `SultanMural`, `RachelsTombstone` (a *tombstone* that reveals journal
entries on look — including the Mark of Death secret, `RachelsTombstone.cs:99`),
`LocationFinder`, `DromadCaravan` (buy secrets), and the crown jewel: **the psychal gland** —
`SecretsOnEat` reveals 2-3 random secrets when eaten
(`D/XRL/World/Parts/SecretsOnEat.cs:22-35`, blueprint `B/ObjectBlueprints/Foods.xml:261-270`),
with flavor text "Someone else's memories seep into your own." The journal is already
physical-ish in Qud's fiction; "read the founder's journal at their corpse" invents no new
ontology.

---

## 3. The corpse as restore point (the C5 rider's new mechanic)

### 3.1 What vanilla hands us

When the founder's body takes the non-player death branch (which it will, under any
swap-before-death Kingdom hook — `T/_notes/SUCCESSION-RESEARCH.md` §1.1-1.2), the `Corpse`
part drops a corpse object stamped with `CreatureName`, **`SourceID`**, `SourceBlueprint`,
`KillerID`, `DeathReason`, `FromGenotype` (`D/XRL/World/Parts/Corpse.cs`, `ProcessCorpseDrop`
:101-180). A named, identifiable, linkable founder corpse — free. Kit drops with it (C7).
Frozen-zone caching preserves it indefinitely while the zone sleeps.

### 3.2 What "re-population of journal" can mean — the three options

- **(a) Wholesale**: corpse interaction ("read the founder's journal" — a deliberate verb,
  not on-look) reveals every founder-stamped entry (§2.4) via `Reveal(LearnedFrom: "the
  founder's journal")`. One pass over `JournalAPI.GetAllNotes`, filtered on the succession
  attribute. Diegetic, mechanically trivial, precedented five ways (§2.5).
- **(b) Partial** (map + secrets, not accomplishments): under the recommended journal siting
  (§8), accomplishments never get forgotten (they are the realm's history and the mural feed,
  §2.2) — so (b) and (a) converge: what was forgotten IS "map + secrets". The distinction
  only survives if the author overrules the accomplishment exemption.
- **(c) Quest continuation**: quest *state* never left (C6) — so the corpse's quest
  contribution is already physical: **the quest items are on the corpse** (C7 — the
  amaranthine prism, the 200 feet of wire, the knickknack, the data disk). Fetch-quests whose
  MacGuffin the founder carried literally require the corpse-run to continue. On top, one
  cheap concrete mechanic: every started quest records `QuestGiverLocationZoneID`
  (`D/XRL/XRLGame.cs:569-576`; the modern quest UI already navigates by it,
  `D/Qud/UI/QuestsStatusScreen.cs:164`) — the corpse-read can drop a revealed map note per
  unfinished quest at its giver's zone: *"the founder's journal marks where the undertaking
  began."* That is the author's "quest updates", rendered concretely, with zero quest-state
  surgery.

### 3.3 Corpse loss

Corpses are ordinary world items — butcherable, burnable, eatable by scavengers, and a burnt
or vaporized death substitutes a lesser blueprint (`Corpse.cs:114-131`). If the corpse is
destroyed before the heir arrives, the founder's personal journal is gone for good; the
realm's floor (chronicle, city records, the C5 re-reveal of realm ground) is the un-losable
minimum. Recommendation: accept the loss honestly (it is the roguelike answer, and the fear
is the fun — see Hollow Knight's shade, §6); do not spawn protection. Author call at §9 Q5.

---

## 4. The shipped quest list, classified for transferability

All 26 static quests (`B/Quests.xml`) plus dynamic village quests. Classes:

- **A — world-errand** (fetch/kill/visit with world state intact; a stranger continues it
  without any fiction strain beyond "the NPC doesn't blink"):
  What's Eating the Watervine?, Raising Indrix, More Than a Willing Spirit, Decoding the
  Signal, The Earl of Omonporch, Grave Thoughts, The Buried Watchers, Fraying Favorites,
  Landing Pads, Return to the Hydropon, We Are Starfreight, Reclamation, and effectively all
  dynamic village quests (find-a-site / find-an-item / interact / negotiate templates,
  `D/XRL/World/*DynamicQuestTemplate.cs`).
- **B — relationship / person-tinted** (mechanically identical to A; the *fiction* names the
  founder — an apprenticeship, an invitation, an appointment, a personal pilgrimage or
  vision): Fetch Argyve a Knickknack (+ Another), Weirdwire Conduit (Argyve's apprentice), A
  Canticle for Barathrum / A Signal in the Noise (Argyve's letter vouches for *you*), O
  Glorious Shekhinah! (a pilgrimage), The Assessment (the Barathrumites assess *the person*),
  Pax Klanq I Presume? (the god's-flesh vision), If Then Else, Petals on the Wind / Find
  Eskhind / Love and Fear / Kith and Kin (the appointed investigator; also the only quest
  with a shipped death-triggered fail, §1.3).
- **C — body-bound prerequisite** (quest persists; the heir must redo a body-scoped step,
  and *can*, because the game-scoped half survives): Tomb of the Eaters (Mark of Death on the
  body, sigil in gamestate, §1.5), The Golem (the golem is a Vehicle world-object; piloting
  binds per body — `T/_notes/SUCCESSION-RESEARCH.md` §1.4), A Call to Arms mid-battle (the
  assault is zone state; see §5).

**Finding: no shipped quest is mechanically impossible for a stranger to continue.** The
whole cost of persist-all is class B's fiction leak — NPCs address the heir as if they were
the founder — and the engine has no per-player identity anywhere in conversation or
water-ritual state to hang a fix on (§1.5). The leak is bounded to dialogue tone, invisible
in class A, and *repairable by flavor* (§7, option T) rather than by state surgery.

## 5. World state vs quests — the other half of the rider

Quest bookkeeping and world state are separate stores, and only the first is in question:

- Sultan dungeons, ruins, and their populations exist from worldgen; the quest layer merely
  attaches managers (`D/HistoryKit/HistoricEvent.cs:482,538`).
- Dynamic-quest targets are placed by zone builders at build time
  (`D/XRL/World/ZoneBuilders/FindASiteDynamicQuestManager.cs` et al.) and persist as ordinary
  objects whatever the ledger says.
- Scripted sequences are zone/system state: the Grit Gate assault runs whether or not a
  player is mid-quest; the slynth settle into their chosen zone on day counters held by the
  system (`D/XRL/World/Quests/LandingPadsSystem.cs:540-544`); Reclamation's waves are system
  state with a built-in retry (:583-590); the golem stands in the world as a Vehicle.
- Therefore: **persist keeps the ledger true to the world.** Force-abandon does not undo the
  world — it only makes the ledger lie about it (the assault happened; no quest remembers).
  C8's interregnum sharpens this: world time passes between death and the rite, so
  day-counter quests can advance/resolve *during* the interregnum — a handoff note for the
  C8 spec work item, not a quest-policy problem.
- Cross-run is already ruled and orthogonal: the seal's export **forbids quests** explicitly
  (`T/_notes/INHERITANCE-SEAMS.md:237`). Everything in this doc is in-run succession only.

---

## 6. Comparables — praise first

- **Sunless Sea (Failbetter)** — the genre's beloved succession-of-knowledge design. Legacies
  let the successor inherit a *chosen slice* of the dead captain's knowledge — the
  Correspondent legacy carries the **entire discovered chart** (and half the Pages); by
  default the map is forgotten ([Legacy — Sunless Sea Wiki](https://sunlesssea.fandom.com/wiki/Legacy)).
  Praise: it proves map knowledge is the inheritance players *care* about most — and it prices
  the choice. Directly on-register for the C5 chart question (§9 Q2): the loved half of
  dying in Sunless Sea is keeping the chart.
- **CK3 (Paradox)** — the canonical transferability-classes design: realm-scoped undertakings
  (wars) continue through succession with the heir; person-scoped ones (schemes, personal
  claims) end with the person — claim wars invalidate when the claimant dies
  ([Casus belli — CK3 Wiki](https://ck3.paradoxwikis.com/Casus_belli)). Praise: fifteen years
  of Crusader Kings players find "the crown's business continues, the person's dies" totally
  natural — our class A/B split reads the same way without explanation.
- **Hollow Knight / souls-likes** — the mainstream's most-loved death mechanic is literally
  the author's rider: *find your old dead body to restore what the person carried* (shade /
  bloodstain). Praise: it converts death into an immediate, legible goal, gives the heir a
  first quest for free, and makes the death site a place of meaning. (General knowledge, not
  freshly verified this pass.)
- **CDDA corpse-runs** — already evidenced as a delight in
  `T/_notes/SUCCESSION-RESEARCH.md` §3.2; the corpse-as-journal extends the loot-run into a
  knowledge-run.
- **NetHack bones / DF adventurer succession** — the genre default the rider improves on:
  world and corpse persist as content, but the dead's *undertakings* simply evaporate;
  nobody has ever praised the evaporation. (General knowledge.)
- **Wildermyth** — mid-mission objectives are party/campaign-scoped and survive any hero's
  death or maiming; the loss is expressed in the *people*, never by orphaning the objective.
  Praise: continuity-of-undertaking plus personal cost is exactly Kingdom Mode's C6+C4 shape.
  (General knowledge.)

No fresh Reddit pass was run for this return — the succession-sentiment base and its volume
caveats are in `T/_notes/SUCCESSION-RESEARCH.md` §3.7/§8 and stand unchanged.

---

## 7. Options, question-shaped — the v1.5 quest policy

**Option P — persist-all (v1, confirmed C6).** Quests stay in `XRLGame.Quests`; the
succession swap rides `IPlayerSystem` re-registration (§1.5); the chronicle notes the
inheritance of unfinished business. Cost: class-B fiction leak. Risk: zero. Code: zero.

**Option T — transferability classes as *flavor*, not state surgery (recommended v1.5).**
Keep option P's state model wholesale, and add: (1) a chronicle line per open quest at
succession ("the founder died with X undone"); (2) relabel class-B quests via the shipped
live-rename precedent (`ReclamationSystem.FailAttempt`, §1.3) — e.g. "Weirdwire Conduit…
Eureka! (inherited)" — so the log itself tells the succession story; (3) the corpse-read's
per-quest giver map notes (§3.2c). Optionally, class-B NPC greetings gain one
succession-aware line where the mod already owns dialogue. No quest state is ever moved,
failed, or hidden.

**Option S — corpse-gated suspension (the strong reading of "quest updates").** On
succession, quests leave the visible log and return when the corpse is read. **REJECTED,
loudly**, on three engine grounds from §1.4: (1) raw removal leaves `IQuestSystem.Quest`
dereferences null mid-flight (NRE) or forces LandingPads-scale manual system surgery per
quest; (2) removal flips conversation gates — givers re-offer, `Quest.Copy()` resets
`Awarded`, and re-done steps re-award XP (death becomes an XP faucet); (3) failing instead of
removing satisfies `IfFinishedQuest` gates (§0.2) — the *worst* possible outcome, silently
unlocking completion-gated content realm-wide. Vanilla ships no hide flag at quest level, so
suspension would be a new UI+state surface bought entirely to deepen a fiction leak that
option T papers over for free.

**Force-fail-all** (not even an option, recorded for the record): rejected by §0.2 — the
`IfFinishedQuest` trap makes mass `FailQuest` on succession an unlock-everything button.

---

## 8. Recommendation

**v1 (confirmed C6, now code-grounded): persist-all — option P.** The ledger is game-scoped,
the systems are body-swap-proof by design, and every alternative fights shipped machinery.
**The corpse restores the journal, not the quests — because the quests never left**; what the
corpse adds to questing it adds physically (C7 kit = the MacGuffins) and navigationally (§3.2c
giver map notes). **v1.5: option T** — transferability classes expressed as chronicle lines,
inherited-relabels (Reclamation rename precedent), and the corpse-read moment; re-open state
surgery only if playtests show the class-B leak actually stings.

**Journal siting under C5, refined by code truth:** mass-`Forget()` the forgettable kinds
(observations, general/village/recipe notes, non-protective sultan notes), stamping each
forgotten entry with a founder-provenance attribute (§2.4); **exempt accomplishments** (the
realm's history, the mural feed — §2.2); **the chart survives** (map notes are
engine-unforgettable, and Sunless Sea says the chart is the beloved inheritance — §6), with
C5's "re-reveal the realm's own ground" then needed only for world-map explored overlay and
any realm-site notes the founder never personally revealed; leave zone `ExploredMap` fog
alone in v1 (§2.3). The corpse-read reveals the founder-stamped set wholesale — option (a),
which under this siting equals option (b). All four cells go back to the author below,
because two of them (chart, accomplishments) *narrow* the confirmed C5 wording.

---

## 9. Questions for the author

1. **The chart (C5 re-ruling required).** Vanilla marks map notes permanently unforgettable
   (§2.2). Options: (i) *soften C5* — the chart survives succession as the realm's chart;
   re-reveal only tops up; (ii) *force it* — direct `Revealed=false` field surgery on
   non-realm map notes, accepting engine-intent friction and unverified runtime behavior
   (Tracked pins, caches — §10); (iii) forget only notes outside realm ground (surgery,
   scoped). **Recommended: (i)**, on engine grain + Sunless Sea evidence. Does C5 soften?
2. **Accomplishments.** Exempt from mass-forget (recommended — they are the run's history,
   feed murals unfiltered, and "the realm remembers everything; only the person forgets"),
   or forget them too (UI-hide only; murals unaffected either way)?
3. **Explored-terrain fog.** Leave all zone `ExploredMap`s alone in v1 (recommended), or
   lazily clear non-realm zones on first heir entry (Amnesia's shipped pattern, §2.2)?
4. **The corpse-read verb.** A deliberate interaction ("read the founder's journal") that
   reveals the founder-stamped journal set and drops one giver-location map note per
   unfinished quest (§3.2) — confirm shape? And should reading also fire the inherited-relabel
   moment (option T's rename), or does the relabel happen at succession?
5. **Corpse destructibility.** Honest-lossable (recommended — scavengers, fire, and acid are
   real; the realm's records are the floor) or protected?
6. **v1.5 = option T** (chronicle lines + inherited-relabels + corpse moment; no state
   surgery) — confirm as the scoped v1.5 target, with option S formally closed?
7. **XP and reputation on inherited quests.** Steps the heir completes award XP to the heir
   (`D/XRL/XRLGame.cs:814`) and completion reputation lands on the heir's post-C4 ledger
   (:727-734) — accept as-is (recommended; no code, consistent with C4)?
8. **Kith-and-Kin-style death-fails during the interregnum.** If a critical NPC dies while
   the realm has no sovereign (C8), the fail fires normally with popups addressed to a player
   who is between bodies. Cosmetic, but: suppress/defer quest popups during the interregnum
   (recommended — queue them to the rite), or let them fire?

## 10. Could not verify / debts

**Engine (decompile-verified negatives):**
- No `QuestFailedEvent`, no quest-removed event, no quest-level Hidden flag, no player-facing
  abandon verb (§1.2-1.3).
- No per-player identity in conversation, water-ritual, or quest-giver state (§1.5).

**Unverified — runtime verification needed before build:**
- Direct `Revealed=false` writes on map notes: behavior of `Tracked` world-map pins, the
  `_mapNoteCategories`/`mapNotesByZone` caches, and the autoget/landmark systems that read
  revealed map notes. Only needed if §9 Q1 chooses surgery.
- Whether re-`Reveal` of previously-known secrets double-fires any reward listeners
  (WanderSystem grants WXU on secrets — Kingdom Mode is a sibling mode; interaction untested).
- Whether corpse items decay, despawn, or get scavenged in practice over long frozen-zone
  spans (the Corpse blueprint chain's food/butchery tags were not audited this pass).
- The mourning-rite window's interaction with day-counter quest systems (LandingPads,
  Reclamation) — flagged to the C8 spec work item (Addendum 22 work item 3).
- `Quest.Manager` parts of in-flight *dynamic* quests across a body swap: `QuestManager` is
  quest-serialized, not body-attached (`D/XRL/World/Quest.cs:296-304`), so it should be
  swap-safe, but no runtime test was run.

**Comparables:** CK3 succession specifics beyond the claim-war invalidation line, Wildermyth,
NetHack/DF/CDDA transfer behavior, and Hollow Knight/souls corpse-runs are stated from general
knowledge, not fresh sources, and are marked as such in §6. Sunless Sea legacy details are
wiki-verified; CK3 claim-war invalidation is wiki-verified. No fresh Reddit pass (§6).
