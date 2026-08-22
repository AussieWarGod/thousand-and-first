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

## Answered

(none yet)
