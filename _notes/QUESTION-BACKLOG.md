# Question backlog — for the author's next check-in

> Standing instruction (author, 2026-08-22): "keep building, put any questions you come across
> in a backlog for me, and work autonomously with your agents until everything in the build and
> idea backlog is done, i will check in later and go through the question backlog."
>
> Format: each entry states the question, the context, the options, and — where work could not
> wait — the PROVISIONAL the orchestrator proceeded on (marked, reversible). Nothing here is
> pinned until the author rules it; provisionals are implementation posture, not doctrine.

## Open

**QB-1 — Mirror-gate v1 topology.** Addendum 22 A2 confirms the gate network is hubbed at the
capital — but the capital/crown wave has not shipped yet, so v1 has no capital to hub on.
Options: (a) hold the gate until the crown wave; (b) ship pairwise gates now, layer the hub
topology when the crown lands. **PROVISIONAL: (b)** — pairwise now; Addendum 20 rules exotics
free-standing, and §4.4 calls the gate the highest-priority item. The hub constraint will be
retrofitted as a re-keying when the capital exists (a gate re-dedication, not a data loss).

**QB-2 — The chart on succession (C5 refinement).** Vanilla's `JournalMapNote.Forgettable()`
returns false unconditionally (Qud/API/JournalMapNote.cs:305-308, verified) — the confirmed
"mass-forget" cannot reach map notes via the vanilla API. Options: (a) SOFTEN C5 — the heir
keeps the chart (the beloved Sunless Sea legacy shape: the map is the inheritance); (b) field
surgery on the journal store (fights vanilla, verification debt). **Rec + PROVISIONAL: (a)
soften — the chart survives succession.**

**QB-3 — Accomplishments exempt from the forget?** Murals read the accomplishment list
unfiltered; forgetting them would rewrite the founder's own history out of the walls. **Rec +
PROVISIONAL: exempt — accomplishments are the realm's record, not the founder's memory.**

**QB-4 — Zone fog (explored-map state).** Zone-scoped, untouched by any body swap; Amnesia
clears it per-zone as the vanilla template if we ever want it. **Rec + PROVISIONAL: leave
alone in v1.**

**QB-5 — The corpse-read verb.** Shape of the act that restores the founder's journal (a
read? a rite? the psychal-gland idiom — "someone else's memories seep into your own" — is the
vanilla register for reading the dead), and when the "(inherited)" quest relabel fires.
Needs a ruling on flavor; machinery is settled (giver-location map note per unfinished quest
via QuestGiverLocationZoneID).

**QB-6 — Is the founder's corpse honestly lossable** (burned, eaten, dissolved — journal
restoration gone with it) or protected? **Rec + PROVISIONAL: lossable — Qud does not
underwrite sentiment.**

**QB-7 — Confirm v1.5 quest polish = flavor-classes only** (chronicle lines + inherited
relabels via the Reclamation rename precedent); corpse-gated suspension and force-fail-all
stay rejected (verified: FailQuest lands quests in FinishedQuests, which SATISFIES every
IfFinishedQuest conversation gate — mass-fail is an unlock-everything button,
XRLGame.cs:1141-1153, 1206-1209). **Rec: confirm.**

**QB-8 — Inherited quest completion pays the heir** XP/rep as normal. **Rec + PROVISIONAL:
yes — the errand was real, whoever finishes it.**

**QB-9 — Quest popups during the interregnum** (C8): suppress and queue to the mourning
rite, or fire as they land? **Rec + PROVISIONAL: queue to the rite — the rite is the witness
surface.**

**QB-10 — Mirror Bug's 100% reflect (lab audit #6).** `ReflectDamage` sources range from
Quartz Baboon's 5% to Mirror Bug's 100%. Ship the class with: (a) a cap (e.g. the graft
grants the class at a fixed low magnitude regardless of source); (b) source-split records
(baboon-hide graft ≠ mirror-carapace graft, priced apart); (c) exclude Mirror Bug as a
source. **Rec + PROVISIONAL: (b) source-split, Mirror Bug's record priced as rung-3.**

**QB-11 — `TemperatureVenting` is Girsh-nephilim-only.** Shipping it as a derived record
gives the lab a second nephal product beside the Cold Regard named procedure. (a) ship as
II/3 with `rite:Girsh` gate; (b) shelf it — nephal bodies feed the Cold Regard only. **Rec +
PROVISIONAL: (b) shelf — one nephal product, and it is the named one.**

**QB-12 — Should the lab sell `Swarmer`?** Pack-synergy (+hit/+pen per ally adjacent to
target) is null solo but multiplies with the muster-hall companion lane later. Sell now,
hold for muster hall, or never? **Rec + PROVISIONAL: hold for the muster-hall wave —
synergy products ship beside the systems they multiply.**

**QB-13 — D2 wishlist delivered** (`_notes/LAB-CLASS-AUDIT.md`): 10 survivors ranked, 32
rejections cited, 3 borderline parked. Author adds/removes at check-in; the lab wave ships
the survivors minus QB-11/QB-12 holds under the provisionals above.

**QB-14 — The mirror-gate's brownout rung.** The demand tier is derived from the record's
`Category` (`KingdomFlowRules.TierOfCategory`), so choosing the category chooses when the arch
goes dark. `Category="craft"` puts it on **Industry** — the first rung, so a short city gives
up its crossing before its forge. The alternative is `civic` → **Amenity** (third): the city
shuts its forges before its gate. **PROVISIONAL: `craft`/Industry** — the ladder's own comment
says a city gives up what it is *doing* before what it *is*, and a felt brownout is the
design's whole tension. Reversible: one attribute.

**QB-15 — One keyed arch per city.** v1 refuses a second arch in a city that already keeps
one, by name, naming the arch in the way. Follows §4.4's "one of your cities to another" and
keeps the register at one row per city, which is what makes the hub re-key trivial.
**PROVISIONAL: keep.** If the capital wave wants a capital with several arches, the rule
relaxes to "one arch per (city, partner)" with no schema change.

**QB-16 — `Sited="gate"` is its own small wave, not strata vocabulary.** It turned out to be
a *placement* rule already sketched in `KingdomRoadRules.cs:544-556` (site the gatehouse at
the frontier gate cell astride the road; today a hardcoded key match). Ship the authored
attribute as its own one-line-plus-schema wave, or leave the gatehouse key-matched?
**PROVISIONAL: leave key-matched; the attribute waits for a second design that wants it.**

**QB-17 — Deep crops key on style, not stratum.** `KingdomCropRules.CropBlueprintForStyle`
reads style only, so a common-style city's fungal vault grows **Starapple in the dark**.
DIVERSITY:213 wanted fungal crops underground; Addendum 15's "behaviour may differ by stratum
only when the design says so" is the hook, and no machinery expresses it yet. Options: (a) a
stratum column in the crop table; (b) the vault records name their crop outright (a
`Crop=`-shaped declaration, design-says-so literally); (c) leave it — starapple grows in the
dark in Qud and nobody promised botany. **PROVISIONAL: none — vaults ship on the style table
as-is; the crop question rides to the author.** (Pure flavor today: yields are identical.)

**QB-18 — How does a SHARED design declare stratum-divergent behaviour?** Addendum 15 permits
it ("only when the design says so"); nothing expresses it. Per-stratum overrides are new
parallel machinery and must earn it (Addendum 13). No design needs it yet — the deep set went
separate-records instead. **Recorded so the want has a number when it arrives.**

**QB-20 — LATENT: the rest of the QoL read family is surface-only.** `KingdomQol.Judge` /
`WillLive` / `Tolerates` / `PreferFlags` / `FirstTolerable` still resolve offers by design key
alone. Zero live callers today, so no Zone overloads were minted (no speculative API). The day
one is called from a deep zone it will be wrong. Recorded so the decision is visible.

**QB-21 — LATENT: the housing/beds invariant is stated twice and enforced nowhere.** The
catalogue check flags "housing with nothing over it" only as a Note and only when a roof was
declared; `KingdomLodging.RoofCapacity` derives capacity from `Carries` roof amount, not roof
state. An `Open="yes"` housing design with `Carries="roof:N"` would house people under the
open sky. Nothing shipped does this; a third-party record could. Candidate: promote to a
catalogue Fault.

**QB-22 — The node-gate data pass over the shipped catalogue.** The tree mints 21 keys and
almost nothing gates on them yet. Applied now (safe, Village+): smelter ← `node:cruciblesteel`,
condensing hall ← `node:pressure` (second gate beside its machine). NOT applied — the research
wave's suggested list contained deadlocks: masonyard ← `node:kiln` (Steading-stage design gated
behind the Village-stage bench), bookshelf ← `node:notes` (the shelf would gate behind the bench
it precedes), moot-family gates (the hall reads the charter — gating it gates the charter).
Question: how deep should node-gating cut into the shipped catalogue, and should the lab wave's
new records carry the flesh-branch gates (`node:vat`, `node:graft`, `node:butchery`) instead —
**PROVISIONAL: yes, new records carry them; shipped records stay lightly gated pending your
balance eye.**

**QB-23 — Gate-grammar wants the design doc promised but verdict 1 forbids without a ruling:**
wildcard machines (`machine:*Furnace`) and counted kinds (`disk:×4`). The wave shipped only
satisfiable literal tokens. Rule the grammar in, or keep the literal vocabulary?
**PROVISIONAL: literal only.**

**QB-24 — `culture:`/`species:` are declared but nothing mints them** (needs per-city identity
tallies at settler intake — Addendum 17's four readers). The vat's identity arm ships as
rite-seeded instead. Minting wave wanted; when?

**QB-25 — First research bench is the scriptorium (Village).** Camp/Steading realms cannot
research at all. If the trunk should be walkable earlier, the lab wave wants a staffed S-plot
copyist's desk. **PROVISIONAL: leave Village-gated; research is a settled realm's luxury.**

**QB-26 — Three defensible readings the research wave took where the design doc conflicted
with itself** (recorded for your eye, all reversible): the tech map keeps four chapters
(visibility-filter reading of verdict 7, not §6.4's replace); the whole map gates on
`node:notes` with roots revealed at first keepers'-screen look; the bench ticks per turn but
charges once per world-day (the scaffold's own idiom vs §10.2's wording).

**QB-27 — Which moment mints a `rite:` key?** Vanilla ships NO water-ritual completion event —
only a start event (`WaterRitualStartEvent`, carrying `Initial` = first-ever share) and
per-choice transactions (secret bought, recipe learned). Both shipped rite lanes in the mod are
inward-facing and cannot mint vanilla-faction keys; every `SeededBy="rite:…"` in the tree is
inert until this is answered. Options: (a) mint on the FIRST SHARE — the covenant moment, one
handler; (b) mint on a grant actually taken — closer to DIVERSITY:158's "the founder's own
water-ritual grants, mirrored", more handlers. **PROVISIONAL: (a) first share** — sitting and
sharing water with a people is what a rite key says; the seed is 25% capped 50% either way.
Implementation pending (a small hook wave; the handler home is whichever lane owns the
founder's vanilla-ritual reads).

**QB-28 — The keepers' roster string is unpriced in the byte budget.** The receipt prices the
seven reference slots; the roster is a composed per-city heap string (the tree's own 21 grants
= 598 B, uncapped above that by disks/machines/patterns). Any width chosen moves the realm
total across or under the 56 KiB advisory rung: 640 B/city → 56,868 (under); 1,024 B/city →
57,636 (over, flipping the pinned verdict). Marked OPEN in the source. **PROVISIONAL: none —
the budget verdict is yours to re-pin; the advisory rung is not a ceiling and nothing breaks
meanwhile.**

**QB-34 — The enrolment answer is a blanket across ~30 vanilla systems.** `IsTrueKinEvent`
carries no asker context, so the enrolled mutant also gets True Kin tonic dosages, five
easier Sifrah boards, True-Kin-only baetyl rewards and conversation nodes — not just the
nook. Options: (a) unconditional + disclosed at the ceremony (**PROVISIONAL, shipped** — the
disclosure names the whole reach, §1.5's lesson); (b) narrow to terminal proximity; (c)
option toggle. The balance camp (§1.4, R-D) is who cares.

**QB-35 — Does the annexe get a rung ladder like the theatre's?** Shipped as one building
per the brief; §7.5 Q3 left it open. **PROVISIONAL: one building; rungs if the playtest
wants a slower door.**

**QB-36 — Annexe numbers are tabled but untuned**: `StandingPerCreed = 150` (3× the lab's),
`EnrolmentDrams = 180`. Same shape as QB-32. **PROVISIONAL: keep; balance pass.**

**QB-37 — Installed implants when the rolls lapse: nothing happens.** The lapse closes a
door and never reaches into a body — verified: no vanilla path re-checks `IsTrueKin` for a
fitted implant. **PROVISIONAL: confirmed shape, pin at check-in.**

**QB-38 — The annexe's keeper staffing** is `RosterNames[0]`, the grafting hall's own
crudeness. Wants the lodged-savant binding (a psyberneticist), not a global guest-table
entry. Rides the QB-33 follow-ups bundle.

**QB-39 — Cosmetic mismatch**: tonic RULES text reads raw genotype, so an enrolled mutant
gets True Kin effects with mutant blurbs. Fixing means writing the `Genotype` property —
exactly the data hack the registry fiction exists to avoid. **PROVISIONAL: leave; it is
even good fiction — the paperwork says one thing and the blood says another.**

**QB-40 — Each roll adds ~12 B to a city's roster string** — folds into QB-28's budget
re-pin when you make it.

**QB-30 — The `|` OR-grammar is declared in two places and implemented in one.** The
visibility layer splits knowledge alternatives on `|`; the GATE layer (`Knows`) compares whole
tokens — so any record written with a bar is invisible-and-unbuildable in two different ways.
The lab shipped literal comma tokens only and the theatre's §3.3 OR-gate
(`rite:Girsh|machine:*Regeneration Tank`) ships as `node:chimerism` instead — the flesh
branch's own T4 node, which already requires `node:graft`, arclight, and is rite-seeded, so it
expresses the intent through machinery that exists. Rule: (a) accept `node:chimerism`
(**PROVISIONAL**); (b) implement OR in `Knows`; (c) pick one literal.

**QB-31 — `Magnitude` is a schema field the brief never authorised by name.** QB-10's
source-split (quartz-hide 5% vs mirror-carapace 100% reflection) is unimplementable without a
discriminator, and a band on a FIELD is the only one that keeps "Grants names a class" intact.
One optional attribute, nothing clamped. **PROVISIONAL: keep; your eye at check-in.**

**QB-32 — Removal price is `Cost / 4` — invented.** §3.8 says only "costs less than the
graft". **PROVISIONAL: keep the quarter; tune at the balance pass.**

**QB-33 — Lab follow-ups bundle** (recorded, none blocking): the vat-house's staffed accrual
clock (arithmetic shipped, wants the power-work's `LastResolvedTick` idiom); §3.6 happenings
2–3 (the savant's price → notable tastes; somebody leaves → roof-brink) — surfaces exist,
wiring wave wanted; `AllowStaticRegistration` cache-key trace for seven conditional-register
classes (first in-game selftest); balance-sim run against a fully-grafted founder (§3.9 risk
3) before the playtest.

**QB-41 — The crown vs the heart ladder.** §5.3 proposed the crown as the heart's final rung;
A4 ruled it MOVABLE, and a heart rung cannot move without unbuilding the founding ground's
own history (`UpgradesTo` never un-upgrades). Shipped as its own XL civic hall; the one place
a shipped recommendation and a later ruling disagree. **PROVISIONAL: own hall.**

**QB-42 — What the hub's arch offers a 3+-city realm.** A row carries one partner (vanilla's
`DestinationKey` is one key), so the capital's arch answers the first spoke in register
order. At two cities a hub IS a pair and nothing is lost. Menu of spokes, round-robin, or
first-spoke? Supersedes QB-15's relaxation note — the hub kept one row per city with no
schema change, exactly as predicted. **PROVISIONAL: first-spoke until the roster wave brings
a third city.**

**QB-43 — The delve design fork not taken.** The stronger fiction is "the delve CLAIMS the
rock at its foot" — then `ClaimedZones` alone carries everything and no register exists. It
requires removing the free vertical claim, which MODDING.md and docs/API.md document as
public behaviour. Shipped: claim stays free, the shaft gates WORK. **Your ruling wanted.**

**QB-44 — The tree has no masonry/stonework node** — foundry is metallurgy. The delve gates
on `node:measuredwork` (survey before you sink — honest, Village-reachable, no deadlock). If
the delve should sit behind a stone lane instead, the tree needs a new node. **PROVISIONAL:
measuredwork.**

**QB-45 — Deep-lane follow-ups bundle** (recorded, none blocking): a deep zone has no
frontier edges, so no gate road ever wears there — the shaft foot IS its gate, wants a
delve-aware companion; `EdgeToward` still routes vertical hauls through the West wall (its
own comment concedes it; the shaft head is the honest entry cell); porters hardcode ≤3 legs
so multi-hop vertical haulage is unmodelled; `ShaftHopMultiplier = 3` is a design number
wanting a balance eye; wear on a delve — a ruined shaft still opens the deep at full
effectiveness; `KingdomReachRules` is stratum-blind (a shrine's spirit reaches through rock
— probably correct, belief is not haulage, but it should be a decision); the city-book zone
row carries no shaft field, so the delved set is recomputed per carry (≤9 game-state reads —
cheap, but the book could hold it if the byte budget is ever re-pinned); and
`KingdomDistanceMatrix` remains fully implemented with zero runtime callers — its Up/Down
slots now, at last, mean something.

**QB-46 — Capital numbers are tabled and untuned** (crown 110/13200; the arcology set), same
shape as QB-36. And a struck crown does NOT auto-promote a former crown hall — the realm
goes capital-less until the founder sets the crown down again, because a capital that moved
back on its own would be a capital nobody decided. **PROVISIONAL: keep both.**

**QB-47 — The arcology has no ground of its own yet.** The interior records ship
`Strata="arcology,surface"` so they are reachable today and move indoors free when the
hosted-plot carrier gives the arcology real interior ground (zone-spanning was deliberately
not built — the shipped plot vocabulary tops out at 20×14, and the arcology takes all of
it). Nothing gates the interior records on the arcology STANDING — capital-only is the
proxy until the stratum is real. And the registry office reads the SEAT's roster ("a copy
of a book kept somewhere else") — arguably correct, worth an eye.

**QB-48 — CLOSED/PINNED (2026-08-23): Seal/Succession engine authority.** Source verification
against 2.0.211.51 fixed one owner for every transition; no two systems race to interpret the
same death.

- **Live stage:** `KingdomSeal` is the profile coordinator in every mode. A semantic kingdom
  mutation marks it dirty; it journals the next coherent snapshot at the end of that action,
  with one-world-day polling as a missed-dirty backstop. `IGameSystem.BeforeSave` is the final
  synchronous flush. `AfterSave` is never called a commit: `XRLGame.SaveSystems` invokes it
  before the primary writer has finished. Founding and retirement flush immediately.
- **Death:** `AfterDieEvent` is the capture seam. It carries `Reason` / `ThirdPersonReason`; the
  dying body's `Physics.LastDeathCategory` is the category authority. The event fires before
  `GameObject.Die` rechecks `IsPlayer`, so `KingdomSuccession` alone owns the Kingdom-mode body
  swap. `KingdomSeal` observes deaths directly outside Kingdom Mode. Inside Kingdom Mode it
  never guesses from handler order: succession explicitly asks it for a terminal attempt only
  when no eligible/reachable heir remains. A successful accession writes a new living
  generation instead. Checkpoint/debug restoration can only leave an attempt, never a legacy.
- **C8 crossover:** the mourning/news interval is resolved synchronously inside the Kingdom
  `AfterDieEvent`; world time advances by the ruled road delay, the rite is told, then the real
  resident body becomes player before the engine's identity recheck. The old founder therefore
  follows vanilla's non-player corpse/removal path and keeps their kit where they fell.
- **Retirement:** a named Charter chapter action, two explicit confirmations. It snapshots and
  promotes an immutable retired legacy immediately, marks that exact `LegacyId` sealed in the
  still-live origin save, and never deletes or abandons the save. Further play cannot rewrite
  that generation; a later succession mints a new per-generation `LegacyId` under the stable
  dynasty `LineageId`.
- **Automatic promotion:** reconciliation happens only on a later boot/action. The sole
  automatic proof is an exact `ScoreEntry2.GameId == OriginGameId` and no standing
  Primary in either canonical `DataManager.SyncedPath("Saves")` or
  `DataManager.SavePath("Saves")` root. Both Qud loadable forms, `Primary.sav.gz` and the legacy
  `Primary.sav` fallback, count. Any present/ambiguous root or Primary fails closed as standing;
  score+save is checkpointed, save without score is living, neither is an orphan. Only a
  `Terminal` attempt may use this proof. `Retired` is the explicit path above; a merely `Living`
  stage never promotes.
- **Reservation/consume:** the new-game `[GameStateSingleton]` selects latest eligible by
  default and obtains an atomic exclusive claim on the unique `LegacyId`, bound to target
  `GameID`, before world generation. Joppa `OnAfterBuild` reserves one still-mutable site and
  copies the sanitized payload into target state. A fresh engine `Applied` result persists as
  `AppliedPendingDurability`; that initial `Applied` result is what proved and published the exact
  inherited objects and marker, while the external receipt remains `reserved`. It may advance
  monotonically to `committed` only after a later load proves the target Primary, its persisted
  target phase, the recomputed immutable marker, the exact loaded-zone marker, and the built
  target. It deliberately does not recheck current object contents or positions after player
  interaction. Primary presence alone never proves application and never commits. No primary is
  recoverable interrupted worldgen and releases only when no live OS claim is held; a primary
  with unapplied or unproved payload stays reserved and reacquires that claim. Placement and
  reconstruction key target marker and immutable reserved receipt tuple, so retries do not build
  twice. Site refusal releases the claim and leaves the legacy eligible. An explicit decline is
  an immutable spent receipt; silence/crash is not a decline.
- **Playtest candidate boundary:** build seniority succession, honest ledger reset, corpse
  memory recovery, persistent quests, one-seat/four-state inheritance, latest-eligible import,
  and retirement now. Chosen heir, groomed designee, climb mode, sultan-history rendering,
  multi-zone inheritance and clone templates remain separately ruled later/stretch work, not
  disguised as missing pieces of this first end-to-end candidate.

**QB-49 — Choose inherited-seat discoverability after playtest.** Current v1.5 provisional:
install one silently/immediately revealed map note at embark, then an exact priority-6100
`LocationFinder` with `Value=1` records the first physical arrival as a vanilla travel
accomplishment (and 1 XP). Decide whether final UX keeps the inherited chart as part of the
legacy, starts unrevealed behind a rumor/natural discovery, or uses another nonmodal reveal. This
choice changes only note/finder presentation; it must not change reservation, placement,
durability, or one-seat consumption.

## Answered

**QB-1 — CLOSED: the hub re-key landed exactly as the provisional promised.** Pairwise
shipped first; the capital wave re-keyed the register by rewriting one column — no arch
visited, no row lost, asserted column-by-column. Cities without arches untouched; a capital
keeping no arch leaves the register byte-identical and says so.

**QB-29 — FIXED: the refining yard applies its own wear.** One correction to the entry as
written: the fix rides the EFFORT percent, not the crew term — every yard stands two, and a
wear percent folded into a head count of two truncates to zero and reports "nobody is
standing at it" to a founder looking at two people. Pristine yards bit-identical
(mutation-guarded in the sim: reverting the line kills it by name). Wear ceiling = 40% of a
sound month; neglect costs 2.5× the schedule.

**QB-19 — DEFECT FIXED: underground `Open` plots no longer advertise sky.** Sky is
weather-reach; underground every tier now provides `taf:dark`, the open plot included.
`RoofOnGround`'s Open-stays-Open untouched (Open is a claim about walls, still true). Offer
cache split per stratum; lodging, upgrade, and the notable's shade-taste threaded with the
ground. Mutation-checked: reverting the rule fails 9 cases. Correction to the original entry:
`cairn` and `masonyard` were NOT reachable (they carry `Strata="all,!deep"`); the true
reachable set was wider — storage, civic, power, and craft `Open` designs with no `Strata`
exclusion, all now covered by the same fix.
