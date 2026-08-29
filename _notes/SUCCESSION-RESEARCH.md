# Succession, death, and the sandbox spectrum — research return for Addendum 21

**Status:** research return for `BUILDING-CATALOGUE-BRIEF.md` Addendum 21 (death, succession, the
sandbox spectrum). Research-first, no code. Options + recommendation for the author's ruling.
DRAFT — not committed.

**Method:** praise-first, evidence-cited. Every load-bearing claim carries a `file:line`, an exact
class/field name, or a URL. `T/` = this repo. `D/` = the decompile at
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1`. `B/` = game XML at
`/home/r/coq/qud_helper/game_base/Base/`. Reddit quotes carry their comment_url (served via the
Arctic Shift archive; reddit.com itself is fetch-blocked). Runtime behaviour is never asserted from
source alone; unverified steps are marked.

**What this answers.** The author's question — "on death you can pick up as one of your citizens
and continue a run?" — expanded mid-research into the pinned Addendum 21
(`T/_notes/BUILDING-CATALOGUE-BRIEF.md:1047-1085`): a **spectrum** of modes/optionals mirroring
vanilla's Classic/Roleplay posture; a **default as roguelike and Qudlike as possible**; a binding
**succession honesty rule** ("as if you started a new game as this citizen, only blueprints this
citizen would have known, their skills, their attributes, their body"); three succession shapes
(chosen citizen / the realm's law picks / the climb from farmer back to sultan); the **clone-vat**
as the endgame answer; and **cross-run persistence** ("the city persists across new games with
slight degradation and some lore/history (we already have this)").

The **Addendum 21 extension** (`BUILDING-CATALOGUE-BRIEF.md:1087-1099`) then named the frame:
**KINGDOM MODE**, "a 'kingdom mode' between default mode and 'roleplay' mode" — a named game mode
on vanilla's own ladder: Classic (death loses everything) / **Kingdom** (death loses the PERSON —
permanent, unreloadable, witnessed — but the run continues through the kingdom via succession) /
Roleplay (death undone by checkpoint). The succession honesty rule is the mode's *definition*:
character-level permadeath, kingdom-level continuity. This document's options are therefore
ranked as **configurations within Kingdom Mode**, not competing modes (§4, §6).

---

## 0. The verdicts, up front

| Question | Verdict |
|---|---|
| Should the kingdom survive the founder's death? | **Yes — and the mod is already built as if it will.** The founder is positional (`The.Player`), never a stored identity (§2.1); exile already models "the realm without the founder's claim" (§2.2); the mod already hooks the death pipeline for citizens (§2.3); and a complete cross-run inheritance system is **already designed and seam-verified** in `DECISIONS.md` + `INHERITANCE-SEAMS.md` (§2.7). This document is gap analysis, not invention. |
| What is the purist default? | **Classic, untouched — with the seal: death is death; the kingdom crosses to the next run as degraded world-state.** The seal is the only continuation shape that adds stakes rather than removing them, it is the DF/bones shape the genre's purists themselves love (§3.2), and it is the shape the repo already designed. §4.1. |
| Is Kingdom Mode implementable as a real mode? | **Yes, cheaply, and Classic stays untouched by construction.** Vanilla modes are data: Roleplay is Classic + one gamestate + a mode system (`B/EmbarkModules.xml:37-38`, `QudGamemodeModule.bootGame`, §1.3). Kingdom Mode = one embark entry + a gamestate + an `IPlayerSystem` death hook (§1.2 surface 3). It fills the exact middle vanilla leaves empty — and the middle the community's own "middle-ground between permadeath and no permadeath" thread asked for (§3.7). |
| Is in-run succession implementable without new machinery? | **Yes — the engine ships the whole trick.** `AfterDieEvent` + `The.Game.Player.Body` reassignment is the Vehicle part's own death behaviour (`D/XRL/World/Parts/Vehicle.cs:438-452`), and `Domination.Metempsychosis` (`D/.../Domination.cs:209-233`) is the shipped template for making a non-original body cleanly playable. §1.2, §1.4. |
| Is the honesty rule implementable? | **Yes, but it has named enemies: the game-scoped survivors.** Reputation, journal, quests, and known tinker recipes are keyed to `XRLGame`, not the body, and survive a body swap untouched (§1.6). A naive swap hands the heir the founder's entire diplomatic ledger and recipe book. Each needs an explicit siting decision (§5.1). |
| Does succession cheapen death? | **Not if it is priced, and fiction alone is not a price.** BioShock's Vita-Chambers are lore-perfect and canonically despised; EVE's cloning is accepted because implants and ships burn (§3.5). The honesty rule **is** the price: the founder's body, build, grafts, journal, and reputation die. The corpse keeps the kit, where it fell (§5.2). |
| Would Qud's community accept it? | **Warmly, as an opt-in.** The two most-upvoted mode threads found are pro-Roleplay manifestos (200+ upvotes, 0.96+ ratios) with gatekeeping shouted down; permanent Domination is a beloved subgenre of play; and the community has already hand-rigged exactly this mechanic and asked for exactly this world-persistence (§3.7). |
| The clone-vat? | **Endgame content, not a mode.** Vanilla Classic already contains in-fiction death-evasion as content (`RestoreOnDeath`, `QuantumFugue` — §1.2), so a priced, tech-gated vat does not violate the purist default. The engine pattern exists: `DeepCopy` + `ZoneManager.CachedObjects` is how ThinWorld already parks a body for later (§1.5). Staleness is the natural cost. §4.5. |
| Cross-run? | **Proceed on the existing design.** `DECISIONS.md:109-249` + `INHERITANCE-SEAMS.md` already specify seal, interregnum, four inherited states, the no-item law, and the engine seams — including the one hard timing fact this research re-confirms: Classic deletes the save at `D/XRL/Core/XRLCore.cs:3051-3057`, so export must complete before that line runs. §2.7, §4.1. |

---

## 1. Code truth, vanilla

### 1.1 The death pipeline, step by step

`GameObject.Die()` (`D/XRL/World/GameObject.cs:15013`) runs, in order:

1. Re-entrancy guard (`Dying`, :15015-15022).
2. **`BeforeDieEvent.Check(...)` (:15023) — the only cancellable gate.** Two phases, either can
   veto: the registered-string `"BeforeDie"` event, then the MinEvent `HandleEvent(BeforeDieEvent)`
   (`D/XRL/World/BeforeDieEvent.cs:57-103`). A part returning false = the target survives.
3. **`AfterDieEvent.Send(...)` (:15025)** — fires before the player/non-player branch splits.
4. **`if (IsPlayer())` (:15050)** — and `IsPlayer()` is pure identity, `this ==
   XRLCore.Core.Game.Player.Body` (`:10976-10983`), **re-evaluated here, after AfterDieEvent has
   run**. This ordering is the entire interception surface (§1.2).
5. Player branch: `KilledPlayerEvent.Send` (:15064, dispatched on the killer; can rewrite the
   death reason, not prevent it), then **`CheckpointingSystem.ShowDeathMessage(...)` (:15066) —
   the Roleplay exit** (returns true → `Die()` returns without ending the game), then the debug
   gate, then `DeathReason` assignment and **`Running = false` (:15080) — game over**. The player
   branch never sends removal events and never destroys the body: **the player GameObject
   survives its own death.**
6. Non-player branch: death message, `KilledEvent` + XP (:15149-15152), then
   `EarlyBeforeDeathRemovalEvent` → `BeforeDeathRemovalEvent` → `OnDeathRemovalEvent` →
   `DeathEvent` (:15159-15162, all uncancellable Sends), then `Destroy()` (:15163).

After `Running = false`, `XRLCore.RunGame` exits its loop and runs the terminal block
(`D/XRL/Core/XRLCore.cs:3051-3057`): if `DeathReason != "<nodeath>"` and not `forceNoDeath`,
`BuildScore()` (score screen, scoreboard append at :3355) and — **unless
`Options.DisablePermadeath` — `DataManager.DeleteSaveDirectory(...)`.** There is no vanilla
resurrection path after :15080; once `Running` falls, only score and exit follow.

**Consequence for cross-run export:** the export must be durably staged **before**
`XRLCore.cs:3054-3057` deletes the save directory. `INHERITANCE-SEAMS.md` already rules this
correctly (stage during play, promote on a later boot from the score/no-save signature); this
research independently re-confirms the line numbers.

### 1.2 The interception surface, ranked

1. **Veto death**: a part on the player handling `BeforeDieEvent` and returning false. Shipped
   precedents — `RestoreOnDeath` (`D/XRL/World/Parts/RestoreOnDeath.cs:28-51`: heal, `return
   false`), `RebornOnDeathInThinWorld` (`:42-58`), the `Invulnerable` effect, `QuantumFugue`.
   **Vanilla Classic already contains in-fiction death-evasion as content** — load-bearing for
   the clone-vat's mode question (§4.5).
2. **Swap the body after death, before game over**: handle `AfterDieEvent` (:15025) and reassign
   `The.Game.Player.Body`; when execution reaches `IsPlayer()` at :15050 it is false, the dying
   founder takes the ordinary creature branch (removal events fire, corpse drops), and the
   game-over block never runs. **The Vehicle part ships exactly this dance**
   (`D/XRL/World/Parts/Vehicle.cs:438-452`: on the vehicle's death, eject the pilot and restore
   `Player.Body` before the check). This is the succession hook.
3. **Game-system-level registration**: an `IPlayerSystem.RegisterPlayer(GameObject,
   IEventRegistrar)` (pattern at `D/XRL/WanderSystem.cs:57-60`) — a save-scoped handler with no
   part on the body. The right home for a mode-gated hook.
4. The mod's own precedent for hook idiom: `r_KingdomCitizenLegacy : IPart` already handles
   `BeforeDeathRemovalEvent` for citizen deaths (`T/Experience/KingdomOffices.cs:272-288`).

### 1.3 Game modes — the spectrum already has a shape

- Mode is a game-state string: `XRLGame.gameMode` ⇒ `GetStringGameState("GameMode")`
  (`D/XRL/XRLGame.cs:245-255`), set at embark by `QudGamemodeModule.bootGame()`
  (`D/XRL/CharacterBuilds/Qud/QudGamemodeModule.cs:341-364`) from **data**:
  `B/EmbarkModules.xml` — Classic (:29), Roleplay (:37-38, adds `Checkpointing=Enabled`), Wander
  (:49-50), Daily (:59). Modes apply XML-declared gamestates and add game systems. **A mod mode
  is an XML entry plus a system — the spectrum's delivery mechanism is data-driven and shipped.**
- Roleplay's death flow: `CheckpointingSystem.ShowDeathMessage` (`D/XRL/CheckpointingSystem.cs:85-154`)
  offers Reload / View messages / Retire / Quit (:114); Reload issues a queued **full save-game
  load of the "Checkpoint" slot** (`:152` → `:63` `XRLGame.LoadCurrentGame("Checkpoint")`);
  Retire falls through to the classic game-over path. Checkpoint saves are written on zone
  transition when the checkpoint key changes (:236-259 → `XRLGame.SaveGame("Checkpoint", ...)`,
  `D/XRL/XRLGame.cs:2023-2026`). Freehold's non-permadeath is literally *reload machinery*, not
  continuation — which is exactly why an in-world succession is not redundant with it: Roleplay
  rewinds the world; succession lets the world keep the death.

### 1.4 Body/control transfer — the engine already changes players mid-run

- **The API**: `GamePlayer.SetBody(GameObject Body, ...)` (`D/XRL/GamePlayer.cs:80-106`) — swap
  `_Body`, activate its zone, clear its goals, fire `AfterPlayerBodyChangeEvent` (:105). There is
  **no PlayerControlled flag**; player-ness is identity (`GameObject.cs:10976-10983`). Callers do
  their own cleanup — which is what Metempsychosis is the template for.
- **Domination**: take control = `Dominated` effect + `The.Game.Player.Body = defender`
  (`D/XRL/World/Parts/Mutation/Domination.cs:310-332`). Host dies → the effect's `"BeforeDie"`
  handler restores the dominator *during the cancellable phase* (`D/XRL/World/Effects/Dominated.cs:204-215`)
  — no game over. **Original body dies while dominating** → `Dominating` catches its
  `BeforeDeathRemovalEvent` (`D/XRL/World/Effects/Dominating.cs:86-90`) and fires
  **`Domination.Metempsychosis` (`Domination.cs:209-233`)**: popup *"Your mind is stranded
  here."*, journal accomplishment, `The.Game.PlayerName = Subject.Render.DisplayName` (:217),
  `Brain.Factions = ""`, `Allegiance["Player"] = 100`, `FactionFeelings.Clear()`,
  `RemovePart<GivesRep>()` (:219-223). **The run continues in the new body, permanently.** This is
  vanilla's own "continue as a different creature after your body dies" — succession's clean-swap
  checklist already written.
- **Vehicle/golem piloting**: mounting sets `The.Game.Player.Body = ParentObject` (the vehicle,
  `Vehicle.cs:204`); dismount restores the pilot or the `"OriginalPlayer"` cached object
  (:187-197). The Tomb/Spindle golem *is* a Vehicle (`D/XRL/World/Quests/GolemQuest/GolemQuestSelection.cs:198,314`).
- **ThinWorld** (Tomb death-realm): deep-copies the player (`oldPlayer.DeepCopy(CopyEffects:
  true, CopyID: true)`, `D/XRL/World/Parts/ThinWorld.cs:299`), swaps the copy in (:303), and
  **parks the real body in `ZoneManager.CachedObjects`** under a gamestate key (:420-428) for
  later restoration (:358-414). The "keep a body for later" pattern, shipped.
- Also: Metamorphosis (`D/.../Metamorphosis.cs:240`, revert `Metamorphed.cs:75-87`),
  Transmutation (`D/.../Transmutation.cs:129-132`), the debug wish `swap`
  (`D/.../Wishing.cs:4185-4200` — raw reassignment, no cleanup, and the engine tolerates it).

### 1.5 Cloning machinery

- `D/XRL/World/Capabilities/Cloning.cs`: contexts `CloningDraught`/`Cloneling`/`Budding`
  (:11-18); `CanBeCloned` gate (:20-39); **`GenerateClone` (:77-105)** = `Original.DeepCopy(...)`
  + `PostprocessClone` (:41-75): strip inventory unless `DuplicateGear`, restore pristine health,
  **remove the `"OriginalPlayerBody"` property (:50)**, mark `IsClone`/`CloneOf`/`CloneOfGenes`,
  rename, and — for a player clone — `SetAlliedLeader<AllyClone>(Original)` (:69-72).
- `GameObject.DeepCopy` (`D/XRL/World/GameObject.cs:4154+`) copies all statistics, properties,
  optionally effects, and **every IPart** — mutations, skills, equipment slots are parts, so a
  deep copy is a full playable duplicate at current level. That is the snapshot half of a vat.
- **There is no serialize-a-creature-template API.** The shipped "keep for later" pattern is a
  live object parked in `ZoneManager.CachedObjects` (`D/XRL/World/ZoneManager.cs:73`, `:318`),
  riding normal save serialization — exactly ThinWorld's stash of the real body. A clone-vat
  template = `DeepCopy()` at the vat + `CacheObject()` + on death, `Player.Body` to it. All
  vanilla surfaces; a data-only template would be a new serialization surface (refuse).
- Vanilla polices copies: Domination **refuses to dominate your own copy** —
  `Target.HasCopyRelationship(ParentObject)` (`Domination.cs:345-349`). Fiction-adjacent
  precedent for the vat's one-body invariant (§4.5).

### 1.6 What the game carries vs what the body carries — the honesty-rule ledger

The succession honesty rule (Addendum 21) demands the heir start "as if a new game began as that
citizen." The body-scoped half is free: stats, XP, mutations, skills, effects, inventory are parts
and statistics of the GameObject and swap with it. The **game-scoped half survives a body swap
untouched** and must be explicitly resited:

| State | Where it lives | Survives body swap? | Cite |
|---|---|---|---|
| Reputation (all factions) | `XRLGame.PlayerReputation`, `Reputation.ReputationValues` | **Yes — untouched.** Nothing in `SetBody` or Metempsychosis touches it | `D/XRL/XRLGame.cs:130`; `D/XRL/World/Reputation.cs:29`; `Domination.cs:219-223` |
| Journal (secrets, map notes, accomplishments, recipes) | JournalAPI game state | **Yes** — but `IBaseJournalEntry.Forget()` can un-reveal per entry | `D/Qud/API/IBaseJournalEntry.cs:167-191` (via `RESEARCH-SYSTEM-DESIGN.md:451-457`) |
| Quests | `XRLGame.Quests` / `FinishedQuests` | **Yes** | `D/XRL/XRLGame.cs:145,148` |
| Known tinker recipes | **static** `TinkerData.KnownRecipes`, saved via `TinkerItem.SaveGlobals`/`LoadGlobals` | **Yes** | `D/XRL/World/Tinkering/TinkerData.cs:43`; `D/XRL/World/Parts/TinkerItem.cs:239-259` |
| Game states, `PlayerName` | `XRLGame` | **Yes** (Metempsychosis rewrites `PlayerName` deliberately, `Domination.cs:217`) | `D/XRL/XRLGame.cs:46` |
| Sultan history | `XRLGame.sultanHistory` | Yes (world fact, *should* survive) | `D/XRL/XRLGame.cs:118,1810,2319` |
| Stats / XP / mutations / skills / effects / kit | parts + statistics on the GameObject | **No — swaps with the body** (the honest half) | `D/XRL/World/GameObject.cs:4154-4249` (DeepCopy inventory of what a body is) |

Reputation thresholds confirmed 600/250/-250/-600 (`D/XRL/Rules/RuleSettings.cs:25-31`) — the
mirrors in `T/Core/KingdomExileRules.cs:68-77` are correct.

### 1.7 Cross-run surfaces

- Profile-level files outside any save, via `DataManager.SavePath`/`SyncedPath`
  (`D/XRL/DataManager.cs:409-411,421-426`): `UserPrefs.json`, `Achievements.json`,
  `HighScores.json`, `BuildLibrary.json` etc. (:249-254). High scores append on every death
  (`XRLCore.cs:3355` → `D/XRL/Core/Scoreboard2.cs:52,88-90`). **Vanilla has no bones mechanic**
  — no run's world state ever materializes in another run (searched: nothing).
- **The overworld is fixed**: `B/Worlds.xml:71` `<world Name="JoppaWorld" ... Map="QudWorldMap.rpm">`,
  applied verbatim by the map builder (`D/XRL/World/ZoneManager.cs:3275-3280` via
  `WorldFactory.cs:291-353`). Per-run randomness is seeded zone *contents* and mutable encounters
  (`XRLGame.GetWorldSeed`, `D/XRL/XRLGame.cs:1505-1511`; `JoppaWorldBuilder.BuildMutableEncounters`).
  **A dead kingdom re-materializing on the same ground next run is compatible with the engine's
  own fixed-map assumption.**

---

## 2. Code truth, this mod — how much of succession is already built

### 2.1 The founder is positional, not nominal

Nothing in the mod stores who the founder *is*. `FounderName()` is `The.Player?.
BaseDisplayNameStripped ?? "the founder"` (`T/Chronicle/KingdomChronicle.cs:132-135`).
`FounderRegard()` reads `The.Game.PlayerReputation.Get(...)` (`T/Core/KingdomSystem.cs:954,1221`).
`Exile` strips the Charter from `The.Player` (`:1046`), `TryReturn` re-grants it to `The.Player`
(`:1113`), founding attaches it to `The.Player` (`T/Core/KingdomFounding.cs:114,231`), and the
loader **re-attaches it to whoever the player is on every load**
(`T/Core/KingdomLoader.cs:9-17`). "The founder is one person" appears exactly once, as a comment
about rite pacing (`T/Core/KingdomSystem.cs:814`). **The founder is a role the player occupies,
already.** A body swap followed by a save/load cycle would re-seat the Charter on the heir with
zero mod changes — the design question is entirely *what else should change*, not *how to move
the crown*.

### 2.2 The exile seam is proto-succession

Exile is "secession, realm-scoped: the whole realm expels the founder... The cities go on — which
is the entire point" (`T/Core/KingdomExileRules.cs:46-59`). The state it builds is precisely "a
realm that exists without the founder's claim": whole `KingdomSettlement` containers parked in
`ExiledSeat`/`ExiledAway` with their own standings ledger (`T/Core/KingdomSystem.cs:741,744,753`),
dormant cities needing no clock (`:670-692` doctrine), and a deed-keyed, never time-keyed regard
ladder. `TryReturn` (`:1067`) already implements *"walk back and it will still be there, with its
own opinion of you"* (`Core/KingdomExileRules*.cs`): gates = cast out, not refounded, ground
remembered, standing on it, regard above Repudiated (`JudgeReturn`, `:200-223`), and restoration
floors regard at indifference, never love (`RegardOnReturn`, `:245-248`).

**How much of succession is this?** Structurally, most of the climb (§4.4) and the frame for all
of it: the realm holding itself while no one holds the charter is a state the mod already
serializes, announces, and recovers from. What exile does *not* do: it never changes who the
player is (the founder lives on outside the gate), and its regard read is the *founder's* — but
since reputation is game-scoped (§1.6), after a body swap the same read simply becomes "the
realm's regard for whoever now stands in the player's boots," which is exactly the heir question
(§5.1).

### 2.3 The mod already hooks death — for citizens

`r_KingdomCitizenLegacy` is attached to every grown settler and handles
`BeforeDeathRemovalEvent` → `KingdomOffices.RecordDeath` (`T/Experience/KingdomOffices.cs:272-288`):
death cause classified, chronicle mourning line, memorial cairns raised in mint order
(`:115-188`). **Citizen death is modeled, witnessed, and memorialized; founder death is the one
death the mod has no opinion about.** The founder-death hook is the same idiom pointed at the
player (§1.2 surfaces 2-3).

### 2.4 The heir pool exists: rows are primary, bodies are views

Residents are rows bound to bodies by a minted id — "the row is primary and the body is a durable
view" (`T/Simulation/City/KingdomResidents.*.cs`, especially the identity and binding shards; LIVING-CITY W2,
`T/_notes/LIVING-CITY-ARCHITECTURE.md:1983`). A `KingdomResidentRow`
(`T/Simulation/City/KingdomCityState.cs:568-629`) carries: id, **Name**, **OriginCode** (index
into `KingdomRules.Origins`, `T/Simulation/City/KingdomResidentRules.cs:365-379`), **CreedCode**,
ArrivedTick (tenure), HomeWorkId, **JobWorkId + JobRole + DayShape**, standing + cause, bound
zone, both brink windows, **CreedToward/CreedChannel**, **KeptCreeds** (creeds held and left,
Addendum 16). It does *not* carry genotype or stats — those live on the body, which is a real
GameObject minted from the settler blueprint table (`KingdomGrowth.SettlerBlueprint()`,
`T/Growth/KingdomGrowth*.cs`). **The heir pool is therefore already real: named people
with bodies, jobs, tenures, creeds, and grudges.** One delta succession needs: the binding
registry's "one identity, at most one body" law (Addendum 12,
`T/_notes/BUILDING-CATALOGUE-BRIEF.md:613`) means the chosen heir's resident row must be retired
(standing + cause exist for exactly this kind of transition) the moment the body becomes the
player — otherwise the roster double-books it. Small, nameable, and the enums already exist.

### 2.5 "Blueprints this citizen would have known" — derivable, no new storage

The honesty rule's knowledge clause maps onto existing records without any per-citizen knowledge
store (mesh condition satisfied):

| The heir knows | Derived from | Cite |
|---|---|---|
| the designs of the work they served | JobWorkId → work row → DesignKey → that design's Knowledge keys | `Simulation/City/KingdomCityState*.cs`; work rows `:521-548` |
| their homeland's trades | OriginCode → `origin:` keys | `Simulation/City/KingdomResidentRules.DayShapeAndOrigins.cs:49` (`OriginCode`); siting table `T/_notes/RESEARCH-SITING-AND-SECESSION.md:354` |
| their creed's rites, and the ones they left | CreedCode, KeptCreeds → `creed:`/`kept:` keys | `KingdomCityState.cs:577,629` |
| what the city held while they lived in it | ArrivedTick against the keepers' roster (city-held holdings) | `RESEARCH-SITING-AND-SECESSION.md:49-55` (holdings are city-held — the heir walks among them) |
| their own city's ground | re-reveal the realm's own journal secrets only (§5.1) | `IBaseJournalEntry.Reveal`, `D/Qud/API/IBaseJournalEntry.cs:167-191` |

The registry model's slogan — the founder carries **doors, never rooms**
(`RESEARCH-SITING-AND-SECESSION.md:369-373`) — inverts cleanly for succession: **the heir
inherits the rooms (the city's holdings, which never left) and none of the founder's doors (the
journal dies with the founder — Addendum 21 says so in terms).**

### 2.6 The office already has a succession law

`KingdomOffices.UpdateOffice`: the office holder is "always `RosterNames[0]`, the settler who has
served longest" (`T/Experience/KingdomOffices.cs:196-207`). **Config B's "whichever citizen would
have become the mayor" has a shipped answer: seniority.** Whether seniority is the *right* law for
an heir is an author question (§7 Q3) — but the pattern of "the realm computes its own successor
from rows" is established code, not a proposal.

### 2.7 The designed-but-unbuilt inheritance system — gap analysis, not reinvention

The author's parenthetical "(we already have this)" is literally true. Two documents carry a
complete cross-run design:

- **`T/_notes/DECISIONS.md:109-249` — "Inheritance: what a settlement becomes when you are
  gone."** Pinned: the settlement is sealed on death/retirement into one bounded number
  (`SealedVigour`, :167-172); one capped fortune draw from immutable legacy data only
  (`InterregnumRoll`, ±40, :174-186); four inherited states Held/Faded/Abandoned/Ruins with a
  swept archetype table (:117-149); **no clock — user ruling 2026-08-19**, judged on "how well it
  was left, never on how long ago" (:151-165); **the chronicle survives every state** — "a state
  that erases the chronicle is a defect" (:188-193); inhabitants are **descendants, not the old
  roll walking** (:201-206); **no item inheritance, ever** — "a settlement that returned your
  stash would turn permadeath into a bank" (:208-213); MVP = one seat zone (:222-227); a
  founder's cairn names the dead character (:229-231). Status: "designed, unbuilt, mechanism
  unverified" (:270).
- **`T/_notes/INHERITANCE-SEAMS.md`** — the engine contract, decompile-cited: cross-run storage
  via `SyncedPath` (Coda and HighScores as vanilla precedent, :30-43); the conservative
  final-death eligibility matrix (score exists + no primary save, :45-76); import before worldgen
  via `[GameStateSingleton]` (:78-89); the `[JoppaWorldBuilderExtension]` placement seam with the
  remaining-mutable-site allocator (:91-118); the strict JSON DTO envelope, two-slot journal, and
  what may/may not cross (:120-245); reconstruction contract and the full automated/live gate
  list (:247-330).

**What Addendum 21 adds that this design does not already answer:**

1. **In-run succession (all three shapes)** — the inheritance design starts at "the run ends."
   Continuing the *same* run in a citizen body is new, and is exactly what §1.2/§1.4's engine
   machinery plus §2.2's exile seam supply.
2. **The clone-vat** — new; §4.5.
3. **The degradation dial's tuning evidence** — the design fixed the *mechanism* (SealedVigour +
   capped roll); §3.8 now supplies the comparables' tuning constraint (economy first, legibility
   always).
4. **The dead founder entering the story as a sultan-like figure** — the design has the cairn and
   the apocryphal-echo chronicle; the shrine/mural rendering question is new (§5.6).
5. One friction to resolve: `DECISIONS.md:112-114` says the sealed settlement "can appear in the
   next playthrough" — per-seal opt-in; Addendum 21 says "the kingdom standing in the NEXT game's
   world" as a spectrum position. Same shape, but the *default import policy* (`Off` vs latest
   eligible, `INHERITANCE-SEAMS.md:195-198`) is now a mode-spectrum question (§7 Q10).

### 2.8 Pins that bind this design

- **Addendum 21 itself** (`BUILDING-CATALOGUE-BRIEF.md:1047-1085`): spectrum; purist default;
  honesty rule; three shapes; vat endgame; cross-run in scope.
- **Addendum 10(c)** (:452-462): collapse leaves **ruins in stages**, mendable, never
  auto-cleared — the degradation vocabulary for inherited states already ruled.
- **Addendum 13** (:706-712): the **mesh condition** — renderings of existing model state, no
  parallel machinery. Every option below is assessed against it. Also :776-790: the megastructure
  shell "endures as Qud's own ruins endure" — end-state cities already expect to outlive their
  founders.
- **Addendum 12** (:613): one identity, at most one body — the heir's row retirement (§2.4).
- The comparables graveyard already rejects "Songs of Syx death spirals (loss writes chronicles,
  not game-overs)" (`BUILDING-CATALOGUE-BRIEF.md:118`) — the mod's loss philosophy predates this
  question and points the same way.
- **Named procedures are once, ever, per character** (`T/_notes/DIVERSITY-AND-TECH-TREES.md:986-997`) — §5.3.
- **The graft blocklist** (:901-906): `Cloneling`, `SplitOnDeath`, `Twinner`, "every
  self-replication or duplication part" refused; "cloning as a dominant-exploit vector." Binds
  the vat's shape: **one template, never a second live copy** (§4.5).
- **`LORE-SPIRIT-AUDIT.md` via `INHERITANCE-SEAMS.md:174-177`**: never `PlayerCult`,
  `CodaSultan`, `CodaVillage`, period 7. Binds §5.6.

---

## 3. Comparables — praise first

### 3.1 The institution outlives the vessel (CK3, Kenshi, Medieval Dynasty, XCOM)

CK3 is the genre's proof that succession *deepens* rather than cheapens: "death is not the end…
your land, gold, and men-at-arms passing down to one of your family members, who becomes the new
player character" ([PC Gamer](https://www.pcgamer.com/crusader-kings-3-review/)); the design lead:
"you'll be playing these characters for centuries"
([Newsweek](https://www.newsweek.com/crusader-kings-3-ck3-history-personal-playground-gameplay-1529271));
community wisdom: "Losing half your kingdom is not always failure. Sometimes it creates the next
great story" ([guide](https://sevenswords.uk/succession-crusader-kings-3-guide/)). The criticism
corpus attacks partition's *opacity*, never succession existing
([Paradox forum](https://forum.paradoxplaza.com/forum/threads/partition-is-a-problem.1530005/)).
Kenshi: "Even after your main character dies, the game will just keep on rolling"
([Steam](https://steamcommunity.com/app/233860/discussions/0/2183537632749454091/)) — no
individual is mandatory, so any death is a wound to the collective, never a fail screen. Medieval
Dynasty is the clean natural experiment for *this mod's exact frustration*: a village-builder with
a mandatory protagonist that **solved founder-death with heirs** — "when you die… you will begin
playing as [your son]," praised as "amazing idea… continue your Dynasty"
([Steam](https://steamcommunity.com/app/1129580/discussions/0/4263206669445926057/)); its residual
sharp edge is dying *heirless* over a living village
([The End of the Dynasty](https://steamcommunity.com/app/1129580/discussions/0/3050609385669189677/)).
XCOM's memorial bar shows ceremony converting loss into meaning
([wiki](https://xcom.fandom.com/wiki/Bar/Memorial);
[Kotaku](https://kotaku.com/remembering-the-fallen-and-the-decisions-for-which-the-5916627)).

### 3.2 The world is the save file; your former self becomes content (DF, NetHack, CDDA, Real Ruins, Terraria)

DF's "Losing is Fun!" works because loss is never deletion: reclaim mode
([wiki](https://dwarffortresswiki.org/index.php/Reclaim_fortress_mode)), adventurers walking their
own dead fort ([wiki](https://dwarffortresswiki.org/index.php/Adventurer_mode)), ghosts of your
dwarves ([wiki](https://dwarffortresswiki.org/index.php/v0.34:Ghost)), Legends as chronicle;
Boatmurdered as the canon of collapse-as-story
([Wikipedia](https://en.wikipedia.org/wiki/Boatmurdered)). NetHack bones: "levels on which a
previous character has died are loaded into a new game… complete with the ghost of the former
adventurer and their belongings" ([wiki](https://nethackwiki.com/wiki/Bones)); the community built
Hearse purely to trade bones files ([argon.org](http://www.argon.org/~roderick/hearse/)); John
Harris lists bones among the genre's great ideas
([Game Developer](https://www.gamedeveloper.com/game-platforms/analysis-the-eight-rules-of-roguelike-design)).
CDDA: "it's even possible to fight the zombified version of your previous character"
([TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/Cataclysm)) — retrieving your old
kit by killing what you became is a self-authored quest. **RimWorld's Real Ruins is the massive
revealed preference for exactly Addendum 21's cross-run shape: 203,121 subscribers, 5,026 ratings
at 5 stars** for "new maps generate ruins from real dead colonies"
([Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1552146295)). Terraria's
world-persists/character-dies split is the community's soft landing for hardcore: "it will not
feel like a waste of time since the world, base, items and progress is still there"
([wiki guide](https://terraria.fandom.com/wiki/Guide:Hardcore)).

### 3.3 Imperfect heirs are the hook (Rogue Legacy, Wildermyth)

Rogue Legacy's heir screen is the loved part — "Colorblind? The world is washed out in black and
white… vertigo… turned the world upside down"
([PlayStation LifeStyle](https://www.playstationlifestyle.net/review/330177-rogue-legacy-review-ps3ps4vita/));
"a character's genetic traits are more memorable than their name or class"
([review](https://dagondogs.com/2015/08/22/rogue-legacy-late-bird-review)). **The flaw is the
characterization** — directly transferable: the heir who is a worse fighter than the founder is
not a downgrade, they are a *person*. Wildermyth's mortal choice (die gloriously vs survive
maimed, the maiming persisting across campaigns) is "the perfect mechanical representation of…
tough choices" ([Indie Game Website](https://www.indiegamewebsite.com/2021/06/24/wildermyth-review/));
its legacy heroes return as folklore, "a younger version of their past self… the way folktales of
a character will put them in a hundred places"
([essay](https://austinkucera.com/blog/Wildermyth-Legacy.html)).

### 3.4 The churn-rate warning (Massive Chalice)

The praise case exists ("You will get attached to lines as a whole,"
[Quarter to Three](https://www.quartertothree.com/fp/2015/06/11/the-best-thing-about-massive-chalice-might-be-the-thing-you-hate-about-massive-chalice/))
but the dominant sentiment is the failure mode: "heroes die too quick… When they die I don't
particularly remember which fights they've been in"
([Steam](https://steamcommunity.com/app/246110/discussions/0/616198900645224283/));
"they've generally got six battles in them"
([GamesRadar](https://www.gamesradar.com/massive-chalice-review/)). **Lesson: attachment must
transfer to the lineage faster than the vessels churn.** For this mod the risk is low — Qud runs
are days long — but it rules out any design where heirs die cheaply and often by construction.

### 3.5 Fiction does not rescue costless death; cost does (Vita-Chambers vs EVE)

BioShock's Vita-Chambers are fully diegetic and canonically despised: they "remove the
consequence of 'death'… turning it into an abstraction," teaching that anything "can be overcome
with your basic wrench if you have enough patience"
([Critical Gaming](https://critical-gaming.squarespace.com/blog/tag/bioshock?currentPage=9));
BioShock 2's director had to rework them
([Kotaku](https://kotaku.com/bioshock-2-director-explains-vita-chamber-changes-back-5447213)).
EVE's cloning is the same fiction *accepted* — because death burns implants, ship, and cargo, and
jump clones carry ISK costs and cooldowns
([EVE forums](https://forums.eveonline.com/t/eve-lore-faq-clones-poddings-crew-deaths-and-backups/139064);
[EVE University](https://wiki.eveuniversity.org/Jump_clones)). Borderlands' New-U wrapper is
praised as fiction but not credited with changing death's psychology
([Game Developer](https://www.gamedeveloper.com/design/death-and-resurrection-in-borderlands-breaking-the-4th-wall-)).
SOMA shows players will engage seriously with a successor-self that is explicitly *not you* — the
discontinuity can be the point
([Simply Put Psych](https://simplyputpsych.co.uk/gaming-psych/the-horror-of-continuity-soma-identity-and-the-ship-of-theseus)).
**Conclusion, load-bearing for every option below: the fiction's job is to make the cost
coherent, never to replace it. The honesty rule is the cost; the succession fiction narrates it.**

### 3.6 Death as fast travel — the exploit family and its counter-measures

Speedrunning's death-warp ("taking an intentional death to skip travel,"
[jsrfspeedruns](https://www.jsrfspeedruns.com/general-techniques/death-warp-abuse)); Elite
Dangerous' "suicidewinder" galaxy-teleport, enabled by cheap insured respawn
([Frontier forums](https://forums.frontier.co.uk/threads/does-the-suicidewinder-still-work.417900/));
Minecraft's `/kill`-plus-`keepInventory` spawn teleport (the item drop *is* the price,
[forum](https://www.minecraftforum.net/forums/minecraft-java-edition/survival-mode/3173490-using-beds-to-de-facto-teleport));
Ultima Online priced death with full corpse loot and the ghost walk back
([UOGuide](https://www.uoguide.com/Death)); EVE cooldowns jump clones explicitly to stop free
teleporting ([EVE University](https://wiki.eveuniversity.org/Jump_clones)). **The common
counter-measure: the respawn point must be non-optimal, or the transition expensive relative to
legitimate travel.** For succession: the heir wakes at home, so the transition cost must live
elsewhere — the founder's kit stays on the corpse where it fell, and the founder's build,
knowledge, and reputation are gone (§5.2).

### 3.7 Caves of Qud's own community — the mode spectrum is morally neutral, and the demand is documented

**Roleplay vs Classic: checkpointing is accepted, recommended, near-zero stigma.** The two
heavyweight threads are pro-Roleplay manifestos at 209 and 202 upvotes (0.96/0.97 ratios):

- "This whole idea that it's not a 'true' CoQ experience is just gatekeeping nonsense." (125 pts)
  — https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1j1840/
- "Qud is not actually well designed for classic permadeath mechanics… I recommend new players…
  try to experience the game through checkpointing." (50 pts) —
  https://www.reddit.com/r/cavesofqud/comments/1i33f36/_/m7jp7sb/
- "It's kind of always been an open secret that many people who play on Classic alt f4 on death…
  Might as well play on Roleplay at that point." —
  https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1jendi/
- The purist counter-voice is real and polite: "the lack of consequences makes everything I do
  feel temporary and meaningless." —
  https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1k2063/ — and self-stigma exists even
  where gatekeeping doesn't: "I felt guilty about starting roleplay but wow I regret not doing it
  sooner." — https://www.reddit.com/r/cavesofqud/comments/1i33f36/_/m7k1pp1/

**The exact mechanic has been hand-rigged and asked for.** "Dwarf Fortress style 'soft'
Permadeath" (42 pts, 2020, https://www.reddit.com/r/cavesofqud/comments/itdg77/): the OP
manually wish-`swap`ped into a stored waterbonded follower on death and walked the old body into
lava. Replies proposed ToME-style limited lives / "a bioshock style vita-chamber that has a
limited number of uses" (29 pts, https://www.reddit.com/r/cavesofqud/comments/itdg77/_/g5dyh2c/)
and "a pheonix themed mutation, or an implant for True Kin"
(https://www.reddit.com/r/cavesofqud/comments/itdg77/_/g5eo7nh/). The one skeptic objects on
build-integrity, not principle: "the skills, stats, and MT's they have aren't going to line up
with a build I see being viable for late-game… To each his own."
(https://www.reddit.com/r/cavesofqud/comments/itdg77/_/g5i4dcd/) — an argument *for* the honesty
rule, not against succession.

**The pain succession answers is voiced, loudly.** "Runs that can easily stretch across days and
even weeks… dying to some random event you simply couldn't know about is insanely frustrating."
(46 pts, https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1j2yx0/). Place-attachment is
real: "I get fond of those procgen villages in Qud, y'know?"
(https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1js2z6/). And the cross-run money quote,
from a self-described permadeath traditionalist:

> "I love the traditional permadeath, but something about wiping out the villages, sultans, bey
> lah, mercheants, and all of the history after every single death doesn't sit right with me. I
> would really like my worlds of Qud to survive long enough for reading the lore to be
> worthwhile." — https://www.reddit.com/r/cavesofqud/comments/itdg77/_/g5gwlfw/

The same thread fondly proposes NetHack bones for Qud
(https://www.reddit.com/r/cavesofqud/comments/itdg77/_/g5fhvfg/).

**Permanent body-transfer is beloved vanilla canon, not a lore risk.** The permanent-Domination
threads treat Metempsychosis as an endgame toy and challenge-run generator: "the challenge was to
win the game as Ctesiphus! [a cat]"
(https://www.reddit.com/r/cavesofqud/comments/yknnna/_/iuubya8/); "domination + piloting a
disposable thrall… is way better insurance against death than precog alone."
(https://www.reddit.com/r/cavesofqud/comments/1hbuh2c/_/m1j14ij/). "You are not your first body"
is established Qud.

**Steam corroboration.** The game's own forum hosts a broadly-supported ask for non-terminal
defeat with permanent consequences, modeled on Outward
(https://steamcommunity.com/app/333640/discussions/2/3108018050536504410/). And the Workshop's
"new body" niche is **empty**: Playable Golem was a chargen genotype, not a death-transfer, and is
currently removed (https://steamcommunity.com/sharedfiles/filedetails/?id=3270006554); "Recur —
New Game EX" is New-Game-Plus export, not in-world succession
(https://www.nexusmods.com/cavesofqud/mods/5). **Demand visible; supply zero.**

### 3.8 The degradation dial

The evidence runs almost entirely on the **too-little-decay** side: Real Ruins' chief complaint is
undecayed ruins as loot piñatas that "catapult players to post-game immediately," answered with
wealth/tech/threat sliders
([discussion](https://steamcommunity.com/app/294100/discussions/0/4943253385012821631/);
[Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1552146295)). The
"decay-deleted-my-work" pole was searched for and **not found** — plausibly under-documented
rather than nonexistent. What survives heavy decay is the emotional payload, provided
**authorship stays legible**: DF players treat their own haunted ruin as the feature
([ghosts](https://dwarffortresswiki.org/index.php/v0.34:Ghost); pilgrimage-logistics threads,
[Bay12](http://www.bay12forums.com/smf/index.php?topic=71678.0)). This lands squarely on pins the
mod already holds: ruins-in-stages, recognizable streets, the chronicle surviving every state
(`DECISIONS.md:188-193,233-242`), and **no item inheritance** — which is precisely Real Ruins'
missing law. The dial should be tuned for economy first (the inherited works' restored condition
cap, already flagged open at `INHERITANCE-SEAMS.md:243-245`) and poignancy second, trusting
legible authorship (name, layout, cairn, chronicle) to carry the feeling through deep decay.

### 3.9 Negative space summary

- **Meta-progression cheapening death**: permanent upgrades "make the entire game feel like a
  grind… directly counter to both randomized level generation and permadeath"
  ([zenorogue](https://zenorogue.medium.com/are-roguelikes-bad-de3a9856a3e5);
  [ResetEra](https://www.resetera.com/threads/do-you-like-meta-progression-in-your-roguelikes-roguelites.1341955/)).
  The honesty rule and the no-item law are the mod's standing defenses: succession must never be
  a stat-drip, and death must never be profitable. `DECISIONS.md:167-172` already caught its own
  first exploit here (the water-per-capita seal term that rewarded letting settlers die).
- **Founder death ends a thriving base**: Medieval Dynasty's heirless game-over over a living
  village (§3.1) is the exact frustration Addendum 21 designs against; Massive Chalice's churn is
  the overcorrection (§3.4).
- **Death as a resource**: §3.6. The counter-costs are structural, not tunable constants.

---

## 4. The spectrum, question-shaped — Kingdom Mode and its configurations

The Addendum 21 extension fixes the frame: **the mode ladder is Classic / Kingdom / Roleplay**,
and Kingdom Mode's definition is the honesty rule — character-level permadeath, kingdom-level
continuity. Implementation rides the engine's own mode idiom (§1.3): modes are data, so Kingdom
Mode is an `EmbarkModules.xml`-style entry + a gamestate + an `IPlayerSystem` registering the
death hook (§1.2) — **Classic is untouched by construction**, which is what preserves the purist
default. The three succession shapes below are therefore **configurations within Kingdom Mode**,
not competing modes; the seal (§4.1) is not a mode at all but the floor under all of them; the
clone-vat (§4.5) is in-world tech usable in any mode.

The constraint set every configuration must satisfy: the **honesty rule** (the mode's
definition); **no death-as-fast-travel profit** (§3.6); **knowledge siting** (§2.5, §5.1); the
**mesh condition** (Addendum 13 — exile/TryReturn and the shipped registries are the machinery;
no parallel systems).

### 4.1 The seal — the floor under every mode: the kingdom crosses as world-state

On final death the realm seals; the next run's world carries it, degraded by the interregnum, on
the same fixed ground (§1.7 — the engine's own fixed-map assumption supports it). **This is the
already-designed system** (§2.7), and it should be the Classic-mode default because it is the
only shape that *adds* stakes to permadeath: the purist keeps a true death AND gains what the
traditionalist in §3.7 asked for — a world where the history survives long enough to be worth
reading. It is DF-reclaim/bones-shaped, the single most consistently delighting pattern in the
corpus (§3.2), with Real Ruins' 203k subscribers as revealed preference. Costs: the
export-before-deletion hook discipline (§1.1); the placement/ownership/reconstruction work
already gated in `INHERITANCE-SEAMS.md:284-330`. Exploit surface: already policed by the pinned
no-item law and the monotonic seal.

**Assessment: build first, floor everywhere — and orthogonal to the mode ladder.** In Classic
the seal fires on first death; in Kingdom Mode it fires when the *line* runs out (no eligible
heir, or the player declines) or on deliberate retirement; in Roleplay, checkpoint deaths never
seal — and the conservative eligibility matrix in `INHERITANCE-SEAMS.md:66-76` (score present +
no primary save) already produces exactly this mode-awareness by construction, because a
checkpoint-restored run still has its save. So cross-run legacy is an **orthogonal import
toggle** (`Off`/latest, `INHERITANCE-SEAMS.md:195-198`), valid alongside every mode; Kingdom
Mode only changes *when* a realm seals, never *whether* the machinery exists.

### 4.2 Configuration A — chosen citizen: on death, pick any citizen and continue

CK3's shape, community-endorsed (§3.7's itdg77 thread is this, hand-rigged). Engine cost is
startlingly low: AfterDieEvent hook → `Player.Body` to the citizen's body → Metempsychosis-style
cleanup (§1.2 surface 2, §1.4) → resident row retired (§2.4) → Charter re-seats itself on next
load anyway (§2.1). The real cost is the honesty rule's game-scoped ledger (§5.1) — that work is
this option's substance. Dangers: **choice invites optimization** (players pick the
statistically-best body, the Reddit skeptic's build-integrity complaint inverted into
min-maxing); mitigated by the heir pool being real people with real jobs rather than a stat menu
— and by Rogue Legacy's lesson that the imperfect heir is the *hook* (§3.3), which argues for
presenting heirs as people (name, origin, creed, tenure, grudge) and never as stat blocks.

### 4.3 Configuration B — the realm's law picks: you get the would-be mayor

The realm computes its heir; you continue as whoever the city itself would have raised. The
office's seniority law already does this computation (`RosterNames[0]`, §2.6); richer laws
(office holder; the creed's own preference; the founder's named designee — a groomed-heir verb)
are renderings over existing rows. This is the *honest* configuration: no body-shopping, the
realm's own opinion made mechanical, and the heir's identity arrives with built-in story (the
senior settler who buried three founders). Degraded continuation is automatic: a realm with no
eligible citizen (empty roster) has no heir, and the seal (§4.1) catches it — Medieval Dynasty's
heirless edge (§3.1) lands softly here by construction.

### 4.4 Configuration C — the climb: wake as a lowly farmer or porter and work back up

The author's third shape, and the one the mod's own machinery loves most. The heir is *not*
handed the charter: they wake as a citizen among citizens — subject to the city's own hierarchy,
jobs, and creeds — and must earn the realm back. **This is the exile state with a different
entrance**: a player with no claim, standing on a realm's ground, facing a regard ladder and a
gate that opens on deeds (`TryReturn` gates, §2.2 — "walk back and it will still be there, with
its own opinion of you" was written for exactly this feeling). The climb's rungs are renderings
of existing state: serve a work (JobWorkId), hold the office (seniority/notability, §2.6), then
stand where the basin poured and take the charter up (the `TryReturn`/`EnsureAbility` seam,
`Core/KingdomSystem.z04.Identity.Read.cs:80`, `TryCaptureSealIdentity`). The regard the realm holds for *the heir* is the same game-scoped
reputation cell the exile ladder already reads (§5.1). Delta over B: the charter re-grant must be
gated on climb progress instead of immediate — a rule-table change in the exile-rules idiom, not
new machinery. This is the prestige variant: slowest, most Qud-registered ("nobody embraces you.
Live and drink."), and the natural home for the dead founder's shrine fiction (§5.6).

### 4.5 The clone-vat: endgame tech, not a mode — usable on any rung of the ladder

The author's endgame answer, pre-shaped by pins: rung 4 already carries
`Knowledge="rite:Girsh|machine:*Regeneration Tank"` and the Girsh ritual pays in
`Liquid="cloning"` (`DIVERSITY-AND-TECH-TREES.md:489,996`) — the vat's gate and catalyst are
already in the tree. Engine shape (§1.5): template = `DeepCopy` at the vat + `CachedObjects`
park (ThinWorld's own pattern); on death, AfterDieEvent hook wakes the template at the vat.
**Cost structure per §3.5's law**: staleness is the price — everything since the last vat visit
(levels, kit acquired, journal since) is gone; the corpse keeps what it carried; refreshing the
template costs real inputs (cloning liquid is an exotic — Addendum 20 portfolio). **Mode
question answered by vanilla precedent**: Classic already contains `RestoreOnDeath` and
`QuantumFugue` as *content* (§1.2), so a priced vat can exist in Classic without violating the
purist default — it is a thing you built, not a setting you toggled. **Pin compliance**: the
graft blocklist (§2.8) demands the one-template invariant — never two live copies; the vat is
dead-man's-switch only, and `HasCopyRelationship` (§1.5) is vanilla's own nod that copies are
policed. The vat and Kingdom Mode compose: a realm may hold a vat *and* a succession law; the
vat preempts (you wake as yourself-stale), succession catches vat-less deaths — and in Classic
the vat is simply the one continuation the purist ladder-rung offers, on the `RestoreOnDeath`
precedent.

### 4.6 The ladder, assembled

| Mode ladder | Death means | Machinery | Honesty-rule exposure |
|---|---|---|---|
| **Classic (vanilla, untouched — purist DEFAULT)** | everything is lost; with the seal toggle on, the realm crosses to the next world, degraded | §2.7's designed system | none (next run's chargen is literal) |
| **KINGDOM MODE — config B (recommended default config)** | the person dies, permanently, witnessed; the run continues as the realm's own heir | §1.2 hook + §2.6 + §5.1 | full ledger reset |
| Kingdom Mode — config A (sandbox toggle) | as B, but you choose the citizen | §1.2 + §2.4 + §5.1 | full ledger reset |
| Kingdom Mode — config C (the climb, opt-in variant) | as B, but you wake low: charter withheld until earned back | §2.2 seam + §5.1 | full reset + charter withheld |
| Roleplay (vanilla, untouched) | death undone by checkpoint reload | shipped (§1.3) | n/a — the world forgets the death; Kingdom Mode remembers it |

Orthogonal to the ladder: the **clone-vat** (in-world tech, any mode, priced — §4.5) and the
**cross-run seal toggle** (§4.1). Every line of heirs ends at the seal eventually. The mode is
chosen once at embark, exactly the posture the community already treats as morally neutral
(§3.7); Kingdom Mode fills the exact middle vanilla leaves empty — and it is the middle the
community's own "middle-ground between permadeath and no permadeath" thread asked for
(https://steamcommunity.com/app/333640/discussions/2/3108018050536504410/), whose ask (defeat as
permanent, story-bearing setback rather than reset) maps most closely onto **config C**, while
the itdg77 hand-rig (§3.7) is config A built by wish.

---

## 5. Cross-cutting assessments

### 5.1 The honesty rule vs the game-scoped survivors

The engine's default body swap violates Addendum 21 four ways (§1.6). Per-state ruling needed:

- **Reputation** — the sharpest conflict. `PlayerReputation` survives untouched, so a naive heir
  inherits the founder's entire diplomatic ledger. The honest read: reputation is the *person's*,
  not the crown's — reset to chargen baseline on succession (re-deriving the initial modifiers
  the heir's own body carries, e.g. `GivesRep`-adjacent part effects — verification debt). **The
  one cell with a better answer than zero: the realm's own faction.** The exile machinery already
treats that cell as "how the realm holds the one who leads it" (`FounderRegard`,
`Core/KingdomSystem.z08.Settlements.cs:74`); for an heir it should be *initialized from the heir's own record*
  (standing, creed alignment with the realm's declared creed, tenure — all on the row, §2.4), and
  the `RegardFloorOnReturn` idiom (`KingdomExileRules.cs:84`, indifference-not-love) is the
  shipped shape for "the gate opens, and nobody smiles." The climb (§4.4) *is* this cell used as
  the ladder.
- **Journal** — Addendum 21 rules it in terms: "the founder's journal dies with the founder."
  Mechanism exists: `Forget()` per entry (§1.6). But wholesale forgetting is dishonest in the
  other direction — the heir *lived here*; re-reveal the realm's own ground (its zones, its
  chronicle-known sites) from city records (§2.5), leave the founder's wider map dark.
- **Tinker recipes** — clear `TinkerData.KnownRecipes` and re-derive the citizen's set (§2.5).
  Mechanically trivial (`TinkerItem.LoadGlobals` shows the list is just rebuilt on load, §1.6).
- **Quests** — honesty says the founder's undertakings are not the heir's; engine safety says
  force-abandoning vanilla quests mid-step is untested ground (`ForceFinishQuest` inserts blank
  quests, `RESEARCH-SYSTEM-DESIGN.md:495-500` — the traps are documented). **Verification debt
  before ruling** (§8). The conservative option: quests persist (the game abstraction leaks a
  little) with the heir's chronicle noting the inheritance of unfinished business.
- What must NOT reset: the world's own facts — `sultanHistory`, the realm's containers, the
  city book, standings between factions. The realm remembers everything; only the *person*
  forgets.

### 5.2 The death-as-fast-travel cost shape

Dying far from home wakes the heir at home — a free teleport unless priced (§3.6). The prices
that fall out of the honesty rule, no tuning constants needed: **the founder's kit stays on the
corpse where it fell** (the player branch never destroys the body, §1.1 — vanilla hands us the
corpse for free; CDDA proves walking back to loot your own corpse is a *delight*, §3.2); the
founder's build, grafts, journal, recipes, and reputation are gone (§5.1); and the heir is who
they are — a farmer, not a founder-shaped respawn. Arriving home naked, in a weaker body, with
the realm's cool regard, is not a travel exploit anyone speedruns. The one residual watch-item:
late-game players with stashed home equipment could still treat death as a discount caravan;
worth watching in play, not pre-tuning (UX-PACING posture).

### 5.3 Named procedures × succession

"Once, ever, per character" (`DIVERSITY-AND-TECH-TREES.md:986-997`) resets per heir — and that is
a **feature, already priced by the world**: each procedure is tied to a specific creature that
must be "found, killed, and carried" (:994), so a new character's fresh eligibility costs a new
expedition, and nothing *accumulates* — the graft died on the founder's body, per the honesty
rule. Death is never an upgrade here; at worst it is a re-roll of the Chimeric Confession's
gamble at the price of the entire founder. No action needed beyond confirming the reading (§7 Q11).

### 5.4 The genotype/summit hook

Succession changes what the *player's body* can reach: the chrome ladder rides
`CyberneticsLicensePoints` (True Kin 2, Mutated Human absent —
`DIVERSITY-AND-TECH-TREES.md:252-253`), so a mutant founder's death and a True Kin heir's
accession genuinely opens the becoming-annexe's summit to *the player in person* — and vice versa
for the theatre's mutant-leaning procedures. Under Design B (one megastructure per city,
`END-STATE-CITIES-RESEARCH.md:1045-1076`) the city-level portfolio is untouched by who reigns —
megastructures are city-held — so the hook is **delight, not exploit**: "the kingdom becomes an
argument about bodies" (`:1063-1065`) gains a time axis, and a realm's history can be told as
"the mutant who raised the theatre, and the True Kin heir who finally walked into the annexe."
Nothing accumulates on any single body (honesty rule), so serial suicide harvests nothing but
account-level experience. Watch-item: heir *selection* by genotype (config A) is exactly the
body-shopping §4.2 flags — one more reason B/C are the honest default configurations.

### 5.5 The heir's creed standing — your own citizen may not love you

The row carries the heir's creed, the creeds they left, and any live creed brink
(`KingdomCityState.cs:577,601-629`). An heir whose creed is not the realm's declared creed
(`Core/KingdomSystem.z03.State.Realm.cs:205`, `DeclaredCreed`), or who once seceded and rejoined (KeptCreeds, Addendum 16), starts with
texture no invented mechanic could buy: the realm's regard initialization (§5.1) can read it, the
chronicle can say it, and lane 2 of Addendum 13 (the city reacts to what you ARE) renders it. This
is Rogue Legacy's colorblind knight in Qud's register — the flaw is the characterization (§3.3).

### 5.6 The dead founder enters the story

The cairn is pinned (`DECISIONS.md:229-231`); the chronicle already writes mural-weighted
accomplishments through vanilla's own surface (`JournalAPI.AddAccomplishment(...,
MuralCategory.CreatesSomething, MuralWeight.Medium, ...)`,
`T/Chronicle/KingdomChronicle.cs:122`) — the same data vanilla's Tomb machinery renders as the
player's sultan-like mural history. Sultan shrines resolve their content from save-scoped history
(`SultanShrine.RevealedEvent` → `The.Game.sultanHistory.GetEvent(id)`,
`D/XRL/World/Parts/SultanShrine.cs:15-45`; the history is per-save data, `D/XRL/XRLGame.cs:118`),
so a city shrine to the dead founder is *plausibly* a rendering through existing machinery — but
whether appending a mod-authored entity/event to `sultanHistory` at runtime is safe for every
consumer (murals, Kindle, relic gen) is **unverified** (§8), and the hard constraint stands:
never `PlayerCult`/`CodaSultan`/period 7 (`INHERITANCE-SEAMS.md:174-177`). The safe floor
already designed: the namespaced apocryphal echo (`DECISIONS.md:195-199`) — the founder enters
the *city's own* registers (chronicle, cairn, outsider rumour), which the mod fully owns, and
the sultan-machinery question stays a stretch goal.

---

## 6. Recommendation

**Ship KINGDOM MODE as a named game mode on vanilla's own ladder — Classic / Kingdom / Roleplay
— with the honesty rule as its definition, config B as its default, and the seal under
everything.**

1. **Anchor on the ladder.** Classic: death loses everything (untouched — the purist default
   preserved by construction, since Kingdom Mode is an additive `EmbarkModules.xml` entry + a
   gamestate + an `IPlayerSystem` death hook, §1.3/§1.2 — exactly how Roleplay itself is built).
   **Kingdom: death permanently loses the PERSON — no reload, witnessed by the realm's own
   mourning machinery (§2.3), remembered by the chronicle — but the run continues through the
   kingdom via succession.** Roleplay: death undone by checkpoint (untouched). Kingdom Mode
   fills the middle vanilla leaves empty, and it is the exact middle the community asked for
   (§3.7, §4.6): character-level permadeath, kingdom-level continuity.
2. **Within Kingdom Mode, rank the configurations: B default, C the flagged variant, A the
   sandbox toggle.** B (the realm's law picks — seniority is already shipped code, §2.6) is the
   default because it is zero-decision at the moment of death, immune to body-shopping, and its
   heirless edge degrades into the seal by construction. **C (the climb) is the best fit for the
   community's middle-ground demand** — defeat as permanent, story-bearing setback — and the
   most Qud-registered shape, built openly on the exile seam ("walk back and it will still be
   there, with its own opinion of you"); it costs one extra gate rule (charter withheld until
   the TryReturn-shaped threshold) and should ship as the advertised way to play Kingdom Mode
   deep. A (chosen citizen) is the sandbox's freedom position, presented as people, never stat
   blocks (§4.2).
3. **Build the seal first regardless** — it is designed, seam-verified, pinned (§2.7), it is
   the floor every configuration ends on, and it is orthogonal to the ladder (an import toggle,
   §4.1): Classic + seal is the purist's *gain* from this whole feature (their death still
   ends everything — and the world finally keeps the receipts).
4. **The clone-vat stays in-world tech usable in any mode** — priced machinery, not a mode
   (§4.5): staleness + exotic inputs + the one-template invariant per the cloning blocklist pin;
   in Classic it rides the `RestoreOnDeath` precedent.
5. The engine work for Kingdom Mode is small and all shipped-pattern: the §1.2 hook, the
   Metempsychosis cleanup checklist, the §5.1 ledger sitings, the row retirement (§2.4). No new
   parallel machinery anywhere — heir pool = resident registry, regard = the reputation cell
   the exile ladder already reads, knowledge = derived from rows (§2.5), mourning = the
   citizen-death machinery pointed at the founder.

The one-sentence version: **the kingdom was always built to outlive the founder — exile proved
it; Kingdom Mode just adds a door the realm opens from the inside, and the seal makes even a
true ending into ground the next life walks on.**

---

## 7. Questions for the author

1. **Confirm the ladder (§4.6):** Classic untouched (seal as orthogonal toggle, no in-run
   succession); KINGDOM MODE as the named middle; Roleplay untouched; the vat as content in
   every mode. (Recommended: yes to all — the vat-in-Classic case rides the `RestoreOnDeath`
   precedent, §1.2.)
2. **Kingdom Mode's in-fiction rendering:** the embark entry sets the mode; should the charter
   ALSO carry a declared succession law at the moot ("name the realm's custom on the founder's
   death") as the diegetic rendering of the chosen configuration — and is the configuration
   changeable in-fiction mid-run, or fixed at embark? Recommended: charter law as rendering,
   changeable by charter verb (it is the realm's custom, not the player's setting).
3. **Which law for config B?** Seniority is shipped (§2.6); alternatives: office holder, the
   declared creed's preference, or a groomed designee (a new charter verb, ties into the
   schooling/Int ladder from RESEARCH-SYSTEM-DESIGN if research raises citizen stats).
   Recommended: seniority now; designee as the first succession *verb* later.
   **Implementation status, 2026-08-27:** the author later activated that verb. It now uses an
   exact realm-bound resident ID and monotonic service plus city-schooling/knowledge-post proof;
   unfinished or invalid proof falls back to seniority without the chosen-life seat consequence.
4. **Reputation siting (§5.1):** full reset with realm-cell initialization from the heir's row —
   accept? And is the realm cell floored at indifference (`RegardFloorOnReturn` idiom) or
   derived richer (creed alignment, tenure)?
5. **Journal siting:** mass-Forget plus re-reveal of the realm's own ground only — accept the
   scope of "the realm's own ground" (claimed zones + chronicle-known sites)?
6. **Vanilla quest state on succession:** persist (conservative, slightly dishonest) or abandon
   (honest, needs the §8 verification first)? Recommended: persist in v1, revisit.
7. **The corpse law:** founder's kit stays on the corpse where it fell, lootable by the heir —
   confirm as the death-warp price (§5.2). (Interacts with nothing pinned; the no-item law is
   cross-run only.)
8. **The interregnum within a run:** is succession instant (you wake as the heir at the moment
   of death) or does it pass through a happening (the mourning rite, the roll read aloud)?
   Recommended: a happening — ceremony converts loss into meaning (§3.2, §3.7's memorial
   evidence), and the chronicle is the mod's witness surface. Deed-keyed, never a time cost
   (Addendum 8).
9. **The climb's rungs:** work service → office → charter, read from existing rows — confirm,
   and rule whether the climb gates the charter on *office* or on *regard* (the TryReturn
   threshold). Recommended: regard, because it is the seam that already exists.
10. **Cross-run default import policy** (`INHERITANCE-SEAMS.md:195-198`): `Off` or
    latest-eligible? §4.1 argues it is orthogonal to the mode ladder (Kingdom Mode only changes
    *when* a realm seals) — confirm, or gate imports to Kingdom Mode. Also: Held-state ownership (fun
    hypothesis 3 — "will they recognize your claim?") is still the best open design question in
    the inheritance doc; the climb (§4.4) is arguably its in-run answer and could share the
    machinery.
11. **Named procedures reset per heir** — confirm the §5.3 reading (feature, priced by world
    scarcity; no cap needed).
12. **The founder's shrine:** owned registers plus the reopened public-history view are now
    implemented. The safe shape is one custom non-Sultan entity/event and one journal category;
    it deliberately does not attach vanilla shrine/mural/cult/relic machinery.

---

## 8. What I could not find / verification debts

**Engine (decompile searches, all negative):**
- Any cancellable event after `BeforeDieEvent` — all later death events are void Sends (§1.1).
- Any vanilla resurrection path after `Running = false`.
- A serialize-a-creature-template API (closest: DeepCopy + CachedObjects, §1.5).
- Any bones-file / cross-run world-state carryover in vanilla.
- A dedicated "promote companion to player" function — Metempsychosis is the nearest template.

**Unverified (runtime verification needed before build):**
- Native UI/save/interruption behavior of the implemented isolated `taf-founder-memory` entity,
  event, and custom journal note. Static engine audit proves it is absent from `isCandidate`,
  `type=sultan`, `type=village`, statue, cult, mural, and relic selectors; TESTING 135a4h-135a4j
  still require live proof.
- Whether force-abandoning vanilla quests mid-step is safe — §5.1 (the documented
  `ForceFinishQuest` traps suggest not).
- The heir's post-reset reputation baseline: how chargen-derived initial reputation modifiers
  would be re-derived for a non-chargen body — §5.1.
- Full Metempsychosis-checklist sufficiency for a *settler* body (vanilla runs it on arbitrary
  dominated creatures, which is encouraging, but our settlers carry mod properties —
  `KingdomResidentId`, `KingdomCitizen` — whose interaction with player-hood is untested).

**Comparables:**
- First-person CDDA corpse-retrieval testimonials outside Reddit.
- Direct quotes of the "decay deleted my work" failure pole (§3.8) — may be under-documented.
- A designer essay directly A/B-ing "cloning fiction vs menu toggle" for death acceptance.
- The Quarter to Three Massive Chalice article body (geo-blocked; quote via search index).

**Reddit (failed queries, per the sentiment agent):** "domination metempsychosis permanently
become dominated creature" (retry succeeded), "caves of qud previous character corpse new run
bones", "caves of qud reincarnation keep playing after death same save", "qud mod play as your
companion when your character dies succession", explore-queries "bones previous character world
persist" and "continue playing after death" (r/cavesofqud). Volume caveats: Roleplay/Classic and
long-run-lament sentiment is broad and well-replicated; the direct succession/bones demand rests
on one 42-upvote 2020 thread plus scattered single comments — a clear signal from a small sample,
not a groundswell. Fixed-map-as-strength sentiment was not found as threads; treat as plausible,
Reddit-unevidenced.
