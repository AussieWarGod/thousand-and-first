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

## Answered

**QB-19 — DEFECT FIXED: underground `Open` plots no longer advertise sky.** Sky is
weather-reach; underground every tier now provides `taf:dark`, the open plot included.
`RoofOnGround`'s Open-stays-Open untouched (Open is a claim about walls, still true). Offer
cache split per stratum; lodging, upgrade, and the notable's shade-taste threaded with the
ground. Mutation-checked: reverting the rule fails 9 cases. Correction to the original entry:
`cairn` and `masonyard` were NOT reachable (they carry `Strata="all,!deep"`); the true
reachable set was wider — storage, civic, power, and craft `Open` designs with no `Strata`
exclusion, all now covered by the same fix.
