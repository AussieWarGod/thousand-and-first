# The living city — simulation architecture

Date: 2026-08-21. Mandate: `BUILDING-CATALOGUE-BRIEF.md` **Addendum 12 (a)–(h)** — the living
city, the performance constitution, scale and cross-zone life, the two hard invariants, journey
continuity, logistics locality, networks, and the executor seam — composing with Addenda 8 (the
time doctrine), 10 (brink moderation, typed wear consequences, ruins), and 11 (grounded production,
the harvest cycle, extend-the-real-machines). Head at writing: `4b128cb`.

> **Current-bound correction (2026-08-25).** This document was costed against the retired flat
> forty-work proxy. The live City envelope is four zones times 220 plots: `W=880`, `R=956`, and
> `64 × 2R = 122,368` maximum reckon row-visits. The composed current-realm model estimate is
> 197,796 bytes after the resident-authority carrier correction; current advisory/ceiling rungs
> are 208/256 KiB. Every later `40 works`, `R=116`,
> `14,848`, 56/64-KiB, or flat-`MaxBuildings` figure in this historical design is superseded by
> those bounds and `KingdomCityMemoryRules`. Catch-up is now re-derived from 220 legal physical
> root containers, the 24+8 manual dedication allowances, and sixty bodies: **312 weighted units,
> 39 turns at 8/turn**. Runtime
> demand is counted per eligible surveyed container, not once per stock kind. Fixed distance
> slices and native/human dense-city receipts remain open gates.

**Performance is not an appendix to this design; it is §0.0, and every later section is answerable
to its table.**

Status: **adopted design with later implementation; current proof remains incomplete.** The wave
plan in §7 records the original order. Current code/tests, `TESTING.md`, and
`BRIEF-IMPLEMENTATION-AUDIT.md` own implementation and evidence status.

> The author's ask, in one line: *a larger city is a SIMULATION, not a set of decorated zones —
> it produces and consumes items, fires events, hosts activities, carries meaning and engagement,
> and changes state meaningfully over world-time, attended or not; built with or alongside the
> vanilla engine, as close to vanilla as we can, and optimised.*

---

## 0. Ground truth — the budgets, what stands, and what the engine will not do

### 0.0 The performance constitution — the numbers this design is held to

Addendum 12(b) rules that the living city must not run out of memory, must not lag the game, must
keep latency down, and must *map out changes to a zone in almost real time*. That is not a
sentiment, it is a set of budgets, and they are written here as numbers so that a later wave can be
**shown** to have broken one. **A regression against this table is a failure, not a trade-off.**

| Lane | Budget | Warn | Fail | Where measured |
|---|---|---|---|---|
| **Reckon** — one city, one pass | ≤ 14,848 row-visits, ≤ 512 draws, **no term in the elapsed** | > 2 ms | > 8 ms per realm pass | the `reckon` step (§2.1) |
| **Reify** — per turn, while a debt stands | **8 units**, of which **≤ 4 body mints**, visible cells first | > 1 ms | > 2 ms, or over budget at all | the per-turn spend (§3.5) |
| **Heartbeat** — one micro-reckon slice, every 50 ticks | ≤ 4 breakpoint steps, ≤ 2R row-visits, ≤ 1 told line | > 0.3 ms | > 0.5 ms, or > 4 steps | the slice (§3.6) |
| **Heartbeat, amortised** | ≤ 2R/50 ≈ **5 row-visits per turn per city** | > 10 | > 20 | the slice (§3.6) |
| **Catch-up drain** | worst backlog ≤ 312 **weighted** units (§0.0(b)) → **≤ 39 turns** at 8/turn | > 40 turns | never reaches zero | ground-survey demand (§3.5) |
| **Model in RAM** | model + registry + itineraries + distance matrix → **≤ 64 KiB per realm** at today's caps (≈ 88 KiB at nine zones — **the formula is the contract, not the constant**, §0.0(f)) | > 56 KiB | > 64 KiB, or over the formula | `kingdom:selftest` (§6.5) |
| **Model in the save** | ≈ 20 KiB per realm | > 32 KiB | > 96 KiB | the write path (§6.5) |
| **Route planning** — per slice | ≤ 16 open jobs, ≤ 8 stops a trip, 2-opt ≤ 50 swap tests → ≲ 1,000 int ops, **zero draws** | > 2,000 int ops | any draw in the planner | the planner (§3.10) |
| **Network solve** — per city, per reckon | ≤ 4 networks x (≤ 32 nodes + 48 edges); one re-solve per network breakpoint → **≤ 5,120 node-visits** | > 8,000 | > 12,000, or a topology walk at reckon | the solve (§3.11) |
| **Zones we hold resident** | ≤ 1 beyond the seated zone, self-releasing | 2 | > 2, or held with no debt | §6.4 |
| **Executor** — any submitted computation | budget and timeout owned by the seam; an abandoned job publishes nothing | — | a job that publishes on fault, or **any engine type across the boundary** | the reflection test (§2.5) |

**(a) Reckon — the arithmetic, counted rather than asserted.** The worst case in the mandate is a
4-zone City, 40 works, 60 residents, one season (90 days) away:

```
rows  R  = 4 zone rows + 40 work rows + 60 resident rows + 12 clocks    =    116
breakpoints B                                                  (§2.3 cap) =     64
per step = one propose pass (every row emits its next candidate tick)
         + one apply   pass (every row integrates to the chosen tick)   = 2R = 232
row-visits                                              = B x 2R        = 14,848
each row-visit                                    <= 20 integer operations
worst case                                                        ~ 297,000 int ops
fixed-period lanes         12 x O(1)   (TickMath.TryCountFixedPeriodDue)
draws       <= 4 per happening x 128 happenings per reckoning     <= 512 per city
```

**The identity that matters: not one term on that page contains the elapsed.** `R` is capped by
§1.4, `B` is capped at 64 with an honest overflow to the fixed point (§2.3), and draws are per
*happening*, never per day. A day away and a season away have the same worst case — which is what
turns risk §8.1(4) from a convention into a test: **assert that a 1-day and a 90-day reckoning of
the same model perform the same row-visit count and the same draw count.** Two cities double every
figure and nothing else.

Why 8 ms is the right ceiling rather than a cautious one: reckon runs **once, on entry**, on the
same frame the engine has already spent a SQLite read, a Brotli decompress and a forced full GC
(§6.4), behind its own "Thawing zone…" task. Eight milliseconds is half a 60 Hz frame in a
turn-based game, at a moment that is already the most expensive in the loop.

**(b) Reify — eight units a turn, visible cells first.** A *unit* is one of: mint or move one
resident body; land one item stack into one container; reconcile one work row against its object.
Eight per turn, of which at most four may be body mints — `GameObject.Create` plus a population
table plus a name is the heaviest unit by an order of magnitude. Two things make 8 a number rather
than a guess:

- **It is well under what the engine already does in a frame.** `ZoneRepair` applies its *entire*
  accumulated backlog in one activation — `num = Math.Max(1L, BuildCounter / TurnsPerObject)`, then
  a loop of `num` `Cell.AddObject` calls (`D/XRL/World/ZoneParts/ZoneRepair.cs:57, 87-97`). We are
  deliberately slower than vanilla's own catch-up, because our unit is heavier than a cell add.
- **It drains inside the grace window.** Units are weighted, because they are not the same size:

| Weight | Unit | Per-turn effect |
|---|---|---|
| **1**, and ≤ 4 a turn | **heavy** — mint or move a body (`GameObject.Create` + population table + name) | ≤ 4 |
| **1** | **medium** — one item stack into one container; one work row reconciled | ≤ 8 |
| **⅓** | **light** — one plant or prop object into a cell: exactly `ZoneRepair`'s own unit | ≤ 24 |

  The light tier is not a convenience, it is forced by what G2 landed: **a home farm stands 80
  plant objects**, and a large city could reach a few hundred per zone — Joppa itself ships 153.
  Growing those plants costs nothing (stage advance is `r_KingdomPlot`'s cheap due-tick nudge on an
  object vanilla is already activating, §3.3 tier 2, and a harvest owes the larder *item stacks*,
  not eighty separate units). But **raising** a farm owes its eighty plants as light units, and a
  city's worth of props is a few hundred. At ⅓ each, twenty-four a turn: a farm materialises in
  ⌈80/24⌉ = **4 turns**, a Joppa-scale 300-object backlog in **13**.

  The live construction rail permits 220 commissioned works on one City zone. Every root may be a
  civic container; 24 water plus eight food vessels may be manually dedicated before those roots
  are raised; sixty residents may need bodies moved: **312 weighted units**. Plot furnishings are
  not extra civic accounts: authored components never carry store authority; legacy population
  furnishings have their mark released without moving/destroying vessel or contents. Ground survey
  counts exact eligible vessels/larders needed by room or contents; one stock kind spread over 252
  containers is 252 units, not one. ⌈312/8⌉ = **39 turns** — inside vanilla's 40-turn
  grace (§0.1). A founder who walks in and stands still watches the city
  finish catching up before the engine would even have suspended the zone they came from.

**(c) Memory — `KingdomCityState`, byte by byte.** Every row is a `readonly struct` in a flat
array (§1.3), so there is no per-row object header; the only heap strings are one name per
resident, and zone ids and design keys are shared references.

| Row | Width | Count | Bytes |
|---|---|---|---|
| Zone | id ref 8 + district 4 + `LastReadTick` 8 + 6 stock/capacity longs 48 + roofs 4 + defence 4 + water carry 4 + food carry 4 + 3 signed owed figures 12 = **96** | 4 | 384 |
| Work | id 4 + zone ref 8 + anchor 4 + design ref 8 + condition 4 + crew 4 + `RanThroughTick` 8 + run-state 16 + pad 8 = **64** | 40 | 2,560 |
| Resident | 120 of fields (ids, ticks, enums, refs, two brink windows, creed history, exact origin and frozen arrival refs) + one unique name string ~64 = **184** | 60 | 11,040 |
| Clock | kind 4 + `NextDueTick` 8 + ordinal 4 = **16** | 12 | 192 |
| Told-log | kind 4 + tick 8 + 2 subject ids 8 + place ref 8 + outcome 4 = **32** | 32 | 1,024 |
| Arrays + carrier headers | — | — | 256 |
| **Per city** | | | **15,456 B ≈ 15.1 KiB** |
| **Per realm** (2 cities) | | | **30,912 B ≈ 30.2 KiB** |
| Binding registry, realm-scope (§3.8) | key 4 + kind 1 + zone ref 8 + object ref 8 + minted tick 8 + pad 3 = **32** | 120 residents + ≤ 16 open jobs + headers | 4,480 B ≈ 4.4 KiB |
| Job rows with itineraries (§3.7) | header 64 + ≤ 6 legs x (zone ref 8 + enter 4 + exit 4 + length 4 + depart 8 + arrive 8 = 36) = **280** | ≤ 16 open jobs, realm-wide | 4,480 B ≈ 4.4 KiB |
| Distance matrix (§3.10) | `ushort` per entry = **2** | works→edges ≤ 540 + same-zone pairs ≤ 900 + zone all-pairs ≤ 81, **per city** | 2 x 1,521 ≈ 3.0 KiB per city, 6.0 KiB per realm |
| Network graphs (§3.11) | node 16 + edge 16 + **traversal 2/node**; per network 32 x 16 + 48 x 16 + 32 x 2 + header **64** = **1,408** | ≤ 4 networks per city | 5,632 B ≈ 5.5 KiB per city, 11.0 KiB per realm |
| The keepers' state (research, Addendum 22 B1) | header = the seven named settlement fields, **measured off the type, budget 48** | per city | 48 B per city |
| Research shelf | key 4 + accrued 8 = **12** | ≤ 8 per city (`KingdomResearchRules.ShelfRows`) | 96 B per city |
| **Per realm, all of it** | | | **57,508 B ≈ 56.2 KiB** in the historical envelope; current live envelope and 208/256-KiB rungs are authoritative |
| *the same, at nine zones and caps scaled with them* | | | *92,948 B ≈ 90.8 KiB — over today's ceiling by design, still under a tenth of a megabyte* |

**Seven corrections and one ruling, from the four waves that had to evaluate this table.**
W0 built the formula (`KingdomCityMemoryRules`), W1 wired it, and W7 built the network rows the
table had been pricing sight-unseen; between them they falsified six of the figures above. All six
are corrected in place, because *the formula is the contract* and a table that disagrees with it is
the thing that is wrong:

1. **The realm total was 53,104 B; the formula composes 53,572 B.** The original was the sum of
   this table's own *rounded KiB* column rather than of its byte column, and rounding twice is how
   a contract loses half a kilobyte. The byte figures are now the authority and the KiB column is
   derived from them.
2. **The nine-zone figure was quoted twice, as 77 KiB in the lane table and 88 KiB here.** The
   formula at `Z=9, W=90, P=135` gives **89,732 B ≈ 87.6 KiB**. Both quotations now read the same
   number, and both are the formula's.
3. **The zone row is 96 bytes, not 80.** W1 could not ship §1.2(b)'s promise inside the original
   width. Two carries (`WaterCarry`, `FoodCarry`) were needed because the `ZoneSighting` the
   subsidence arithmetic reads is a projection of *carries* and not of levels, so a row that could
   not answer for them could not replace the game-state keys it retires; and the signed debt is
   **per stock kind** rather than one net figure, because *one net counter cannot say that a zone
   owes a food landing and a water draw at once* — which is the ordinary case for a granary zone
   the city has been drinking out of, and W0's own open finding. The weighted thirds §3.5 reports
   as `owed` are derived from the per-kind figures (`KingdomCityRules.CounterFor`), so there is one
   debt and one home for it.
4. **The warn rung moves from 48 KiB to 56 KiB. The ceiling does not move.** W0's memory test
   recorded, rather than hid, that the composed realm total sat *above* the table's own warn rung
   at today's caps — the design shipped permanently inside its own warning, which tells a tester
   nothing. **Warn is advisory and the ceiling is the contract**, so the advisory rung is raised to
   sit above the design's honest resting figure and below the ceiling it must never reach. The
   64 KiB ceiling, and "or over the formula", are unchanged: those are what a regression is
   measured against. `kingdom:selftest` still checks the *measured* byte count against the formula
   evaluated at the **live** caps, never against 54,340 or any other frozen figure.
5. **The network header was 32 bytes and is 64.** It was priced before anything had been built to
   sit in it, and the row that shipped holds four array references (nodes, edges, order, parent
   edges) plus an id, a kind, a liquid reference, a topology stamp and the stock pair the
   `(network, liquid)` key carries. Four references alone are 32 bytes.
6. **Each network stores a traversal order: two bytes a node, and it buys the budget it costs.**
   §3.11 prices the solve at `O(nodes + edges) ≤ 80`, which a walk that has to find each node's
   neighbours by scanning the edge array *cannot honour* — that is `nodes × edges` = 1,536,
   nineteen times the ceiling. So the traversal order is computed once when the topology is laid,
   off the ground and never at reckon (`O(nodes × edges)` paid on a placement), and the solve is
   then one linear pass over it. Two bytes a node — a node index is at most 31, an edge index at
   most 47, and 255 is free as the no-parent sentinel — against 162 for a full adjacency index.
   **The realm total moves 768 bytes and stays under the advisory rung; the ceiling has not moved.**

7. **The resident row first became 104 bytes, then 120 when the parallel roster authority was
   retired.** BUILDING-CATALOGUE-BRIEF Addendum 16 makes a
   settler's creed HISTORY a recorded fact — the ALIGNMENT gate is satisfied by a builder who
   holds a creed *or has previously held it*, which no tally of present belief can answer. The
   record is bounded (`KingdomCreedRules.MaxKeptCreeds`, and it never rotates: first in wins, so a
   design a city could see yesterday cannot vanish today) and rides the row as **one shared string
   reference** — the very string the settler's own property bag carries, so the heap grows by
   nothing and the row grows by eight. Ninety-one declared plus eight is ninety-nine, and
   ninety-six had five bytes of headroom, so the budget moved with the field rather than the field
   being squeezed to fit a number. **The realm total moves 960 bytes to 55,300 B ≈ 54.0 KiB, still
   under the then-current 56 KiB advisory rung. W2 authority reconciliation then required exact
   open origin and frozen arrival presentation evidence to survive without consulting
   `RosterOrigins`/`RosterArrived`. Those two shared references bring the declared row to 115
   bytes and its budget to 120. The current 208/256-KiB rungs in the correction above absorb the
   1,920-byte realm increase; no unique second string heaps are created because carriers, body
   properties, and compatibility projections share the same values.**

Serialized it is smaller: no references, `WriteOptimizedString` dedupes zone ids and design keys
across every row, ticks go out optimized — **≈ 5.2 KiB per city, ≈ 10.4 KiB per realm**, plus
≈ 2.7 KiB of registry: **≈ 13 KiB per realm**, inside blocks `KingdomSettlement` and
`KingdomSystem` already write. Contrast the rejected alternative: `ZoneManager.Save`
writes every resident zone into the save **in full and uncompressed**
(`D/XRL/World/ZoneManager.cs:468-475`), on every save, so three extra held zones is three zones of
save bytes. The model is cheaper than the engine's own answer by orders of magnitude — and §6.5
*measures* the byte count on the write path rather than trusting this table.

**(d) The non-goals, stated so they cannot be re-proposed.**

- **No pinning.** `PinnedZones` caps at `> 3` and clears the whole list on overflow (§0.1).
- **No turn counting as a clock**, ever. `The.Game.TimeTicks` only (§2.1).
- **No per-cell city state.** The model's dimensions are rows, and none of them is a cell.
- **No suspendability veto and no holding a city zone *live*** — §6.4 answers that question
  directly, with the numbers, because it is the obvious idea and deserves a real refutation.
- **No per-day loop and no per-day draw** anywhere in the reckoning (§2.3, §2.4).
- **No second turn loop, no timer, no queue drained on a schedule.**

**(e) One distinction the whole design turns on: a clock is not a pump.** Game-level
`EndTurnEvent.Send(game)` fires once per ten segments, immediately before `ProcessSingleTurn`
(`D/XRL/Core/ActionManager.cs:1644-1650`) — **one** dispatch, not the 2,000-cell broadcast a live
zone pays. It does not fire during world-map travel, which is exactly why §2.1 bans it as the
city's *clock*. But a founder on the world map is standing in no city zone and is owed no
reification, so the same blind spot is harmless in a *pump*: how much work is owed is always
derived from `The.Game.TimeTicks` deltas; the pump only decides **when a slice of it is spent**.
One handler, one virtual call a turn, returning immediately when there is no seated claimed zone
and no debt. That is the only per-turn cost this design adds anywhere.

**(f) City size is bounded by the rules, never by the architecture.** Addendum 12(c): *"a city
might end up being 9 zones or more, especially with verticality."* Nothing above changes when it
does, and that is a property to state rather than to hope for.

- **The model is O(rows).** A zone is one row keyed by its `ZoneID`, and `ZoneID` already carries
  the stratum — `Assemble(...).Append(ZoneZ)` (`D/XRL/World/ZoneID.cs:12-24`). A city three
  parasangs wide and three strata deep is the same arithmetic as a flat one. **Verticality is
  free**, and the claim verb already allows claiming straight up or down (§7.4 W1, TESTING 80d).
- **The 4-zone figure is a stage-gate rules constant** (`KingdomZoningRules.ZonesForStage`), not an
  architectural limit. Raising it raises `R` linearly and changes nothing else in this section.
- **The reckon budget is a formula, not a number:**

```
R          = Z zone rows + W work rows + P resident rows + C clocks
row-visits = B x 2R,   B <= 64

Z=4, W=40, P=60,  C=12  ->  R = 116  ->  14,848      the City as the rules cap it today
Z=9, W=90, P=135, C=12  ->  R = 246  ->  31,488      a full 3x3 parasang, caps scaled with it
```

  Nine zones is one whole parasang (`D/XRL/World/ZoneManager.cs:3268`) — the author's own figure.
  It costs **2.1x** the reckon of a 4-zone city and is still a fraction of a millisecond of integer
  arithmetic. The receipt (§6.5) checks the row-visit count against the **live** `R`, not against
  14,848, so the assertion survives the cap moving.
- **Nothing else scales with city size at all.** Reify is a fixed per-turn budget, independent of
  how much is owed. The heartbeat is one slice on a cadence, independent of `Z` except through `R`.
  Prefetch is bounded by **the neighbours of the zone the founder is standing in** — at most six in
  the engine's own topology (four orthogonal, plus the stratum above and below), of which we
  consider two and hold one (§6.4) — never by the size of the city. A founder in a thirty-zone city
  pays exactly what a founder in a two-zone city pays.

**(g) The four invariants this design is answerable for.** Each has exactly one home, one test,
and no second statement anywhere else in this document:

| # | Invariant | Home | Proved by |
|---|---|---|---|
| **I1** | **Catch-up.** model total == ground total + counter-owed, per stock kind, at every instant | §3.5 | `kingdom:selftest`; Pass 32 step 90c |
| **I2** | **One event, two renderings.** One *effect*, applied once at its dated tick; at most one *rendering*, chosen by attendance and never drawn for | §3.7 | Pass 32 step 90d |
| **I3** | **One identity, at most one body**, in any zone at any time — residents by `ResidentId`, transients by `JobId`, one registry answering both | §3.8 | `kingdom:selftest`, asserted directly |
| **I4** | **Deficits drain real containers**, in a stated deterministic order; every mismatch attributed and told, never silently repaired | §3.9 | Pass 32 step 90g; §8.2 |
| **I5** | **Journey continuity.** For any `TimeTicks`, the model gives **one** answer to *where is this carrier and what is on them*, and every zone renders that same answer | §3.7 | Pass 32 steps 90d, 90d2 |
| **I6** | **Locality.** No carrier is ever routed past a nearer holder, and no two under-capacity trips run where one would do | §3.10 | `kingdom:selftest`; Pass 32 step 90j |

I1 is the general form of §8.2's conservation invariant; I3 is §8.3's, widened from residents to
everything we mint. **Nothing else in this design may claim an invariant without adding a row
here.**

**§3.7 to §3.11 are one contract, not five features.** Addendum 12(c) gives the city's flows their
*embodiment*, 12(d) their *uniqueness and their effect on real containers*, 12(e) their
*continuity*, 12(f) their *routing*, and 12(g) their *infrastructure*. They compose into one
sentence, and every invariant above is a clause of it: **the model commits to a plan once — a job,
a route, a flow — and a body, a container or a live vanilla part is how a zone draws that plan
while somebody is watching.** Carriers and conduits are the same idea at two speeds: a carrier is
a flow that walks, a conduit is a flow that does not.

The measurement plan that makes this table falsifiable in play is §6.5.

### 0.1 The engine constraint that decides the whole design

Four facts, verified in the decompile at
`/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` (call it `D/`), pinned here
because everything below is a consequence of them:

| Fact | Evidence |
|---|---|
| **A zone stays live for 40 turns after you leave it, and keeps simulating.** `Zone.GetSuspendabilityTurns()` returns `40` (`5` with `Options.CacheEarly`). Inside that window the zone is in `CachedZones`, not `Suspended`, and `ProcessSingleTurn` **does** tick it and broadcast `EndTurnEvent` into it. There is a real ~40-turn shadow-simulation window, and it is the only free simulation the engine gives. | `D/XRL/World/Zone.cs:7413-7420`; `D/XRL/Core/ActionManager.cs:439-467` |
| **Past that, it suspends — and everything in it stops.** `SuspendZone` fires `SuspendingEvent`, strips that zone's actors out of the `ActionQueue`, and flips `Suspended = true`. Nothing is serialized and nothing is dropped: **the whole object graph stays in RAM.** But `ProcessSingleTurn` skips suspended zones, and `ShouldRemove` drops any live object whose `CurrentZone.Suspended`. Every vanilla producer (`LiquidProducer`, `Harvestable`, `Mill`, `ItemConvertor`, `FoodProcessor`, `SolarArray`) runs off `TurnTick`/`EndTurnEvent` and therefore stops dead. | `D/XRL/World/ZoneManager.cs:1682-1712`; `D/XRL/Core/ActionManager.cs:430-467`; `VANILLA-PRODUCTION-TRUTH.md` §0 |
| **On the very next turn it is frozen to disk and released.** `Zone.GetFreezabilityTurns()` returns **`0`**, and `GetPendingZoneFreezeThreshold()` returns **`1`** on ordinary hardware — so one suspendable zone triggers an immediate Brotli-compressed SQLite write, `Zone.Release()` (every cell cleared, every object pooled) and a forced GC. | `D/XRL/World/Zone.cs:7467-7470`; `D/XRL/World/ZoneManager.cs:883-966, 976-1040` |
| **Keeping zones live is possible and expensive.** `zone.MarkActive()` each turn, or returning non-`Suspendable` from `GetZoneSuspendabilityEvent`, or `SetCachedZone(zone)`, will hold a zone resident and simulating. The price: full per-turn CPU (its `TurnTick` plus an `EndTurnEvent` broadcast across 2,000 cells), full RAM, its actors back in the `ActionQueue`, **and every live zone written inline into the save file** (`ZoneManager.Save` writes `CachedZones` in full; frozen zones are ids only). | `D/XRL/World/Zone.cs:2304`; `D/XRL/World/ZoneManager.cs:1726-1780, 468-520` |
| **Pinning is not an escape.** The cap is `PinnedZones.Count > 3`, and it is not checked when you pin — it is checked lazily, inside `GetSuspendability`, and on overflow it logs a `ZonePins` exception and calls **`PinnedZones.Clear()`**: a silent, total loss of every pin, at an arbitrary later moment, not just the offending one. A 4-zone City is exactly one over. STANDARDS §1 already forbids it ("never pin — lazy catch-up"). | `D/XRL/World/Zone.cs:7441-7448`; `STANDARDS.md` §1 |

**The consequence, stated once:** a four-zone City gets 40 turns of grace and then has at most one
zone alive. Holding the other three resident is *technically* available and costs three extra
zone-ticks a turn, three extra `EndTurnEvent` broadcasts across 6,000 cells, three zones' worth of
actors in the action queue, and three zones inline in every save — for a city that is meant to be
one of several the player founds. That is not "optimised", and it fails Addendum 12's own word.
**So: the city is a model, and a zone is a view of it.** Every "keep it warm" variant — pinning,
`MarkActive` spam, a `GetZoneSuspendabilityEvent` veto, `Options.DisableZoneCaching2` — is priced
here and rejected, and should not be re-proposed without new evidence.

### 0.2 The engine hooks that make the design cheap

Every `ZoneXxxEvent.Send` calls `The.Game.HandleEvent(E)` **and then** `Zone.HandleEvent(E)`, so a
game system and an object part can both hear the same event. None of the zone events sets cascade
bit 32 (`CASCADE_STOP_AT_ZONE`), so all of them reach every object in the zone.

| Hook | Contract | Evidence |
|---|---|---|
| `ZoneActivatedEvent` | Fires from `Zone.Activated()` for the **one** zone becoming active — on transition, and again on game load. Wrapped in the engine's own try/catch. `PrimePowerSystemsEvent` follows immediately. `KingdomSystem` already registers it. | `D/XRL/World/ZoneActivatedEvent.cs`; `D/XRL/World/Zone.cs:7775-7791`; `D/XRL/XRLGame.cs:1959` |
| `ZoneDeactivatedEvent` | Fires for the **outgoing** active zone, immediately before the incoming one activates. Free, unused by this mod today. | `D/XRL/World/ZoneDeactivatedEvent.cs`; `D/XRL/World/ZoneManager.cs:1904-1905` |
| **`SuspendingEvent`** | Fires from `SuspendZone` **before** `Suspended = true`, for **any** zone as it suspends — not only the outgoing active one. **This, not deactivation, is the true last-read moment**, because a deactivated zone goes on simulating for up to 40 more turns (§0.1). | `D/XRL/World/SuspendingEvent.cs`; `D/XRL/World/ZoneManager.cs:1682-1712` |
| `ZoneThawedEvent` | Carries `TicksFrozen` (`TimeTicks − FrozenTick`), the engine's own measure of time on disk. Sent from `Zone.Thawed` at the end of `TryThawZone`, for **any** zone coming off disk — active or not — after `Zone.Load`, `AddCachedZone` and `ForceCollect()`. **Fires only on thaw from SQLite, never on wake-from-suspend**: a useful cross-check, never a sufficient clock. **The second reify hook** — it binds debt intake where `ZoneActivatedEvent` binds the pass (§3.5). | `D/XRL/World/ZoneManager.cs:864-865`; `D/XRL/World/Zone.cs:7772-7774`; `D/XRL/World/ZoneThawedEvent.cs:39-46` |
| `Before/ZoneBuilt/AfterZoneBuiltEvent` | Fire at generation for **any** zone, including ones the player never enters. Relevant to claiming and founding. | `D/XRL/World/ZoneManager.cs:3202-3206, 3283-3286, 3584-3588` |
| `Calendar` | `TurnsPerDay = 1200`, `TurnsPerHour = 50`; `CurrentDaySegment = (TimeTicks % 1200) * 10`; `IsDay()` is segment 2500–9123; `GetTime(int)` names eight bands. **The day-shape vocabulary already exists, in the game's own register.** | `D/XRL/World/Calendar.cs:11-35, 296-352` |
| **`IdleQueryEvent`** | **Vanilla's entire daily-life surface.** The `Bored` goal collects every object in the zone that wants the event, shuffles, and offers each the idle actor; **returning `false` claims that actor's turn** (the caller then spends 1000 energy and stops). Gate tag `AllowIdleBehavior`, veto tag `PreventIdleBehavior`. `Bored` bails out entirely when the actor is not in the player's zone. | `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330` |
| `Brain.Stay(Cell)` / `Brain.StartingCell` | With `Wanders = false`, `WandersRandomly = false` and no `NoStay` tag, an NPC **self-anchors** to where it first stands, and `Bored` walks it back forever. Zero code, zero per-turn cost of ours. | `D/XRL/World/Parts/Brain.cs:2056, 2507` |
| `ZoneRepair` (`IZonePart`) | **The engine's own accumulate-and-apply-N-units catch-up**, and a better precedent than `Temporary` for production: `BuildCounter += TimeTicks - LastTurn; LastTurn = TimeTicks; num = Math.Max(1L, BuildCounter / TurnsPerObject)`, then apply `num` units and `RemovePart(this)` when the backlog empties. Note what it does **not** do: it applies the whole batch in one activation, and it binds `ZoneActivatedEvent` only, so the engine's own catch-up misses thaw entirely. §3.5 keeps the counter and amortises the spend. | `D/XRL/World/ZoneParts/ZoneRepair.cs:30-43, 51-58, 87-97, 99-102` |
| `GenericInventoryRestocker` | Vanilla's canonical "things changed while you were away": `long num = TimeTick - LastRestockTick`, one roll amplified by the number of whole missed periods, plus the **item-protection protocol** — `_stock` ("I made this, I may destroy it"), `norestock` ("never touch"), `IsImportant()` (never destroy). | `D/XRL/World/Parts/GenericInventoryRestocker.cs:12-146` |
| `IGameSystem` | `Register(XRLGame, IEventRegistrar)` is the only live registration hook, re-run on load via `ApplyRegistrar` (so it must be idempotent). Dispatch is O(handlers for that ID). | `D/XRL/IGameSystem.cs:2877-2941`; `D/XRL/XRLGame.cs:357-384, 1944-1962` |

**Three traps worth naming, because each would have cost a wave to find in play:**

1. **`Turns` and `TimeTicks` diverge, and `EndTurnEvent` does not fire during world-map travel.**
   `TerrainTravel` advances `game.TimeTicks` directly — 300 to 900 ticks for a single parasang
   step, six to eighteen in-game hours — and increments `game.Turns` **not at all**, running one
   batched `ProcessTurnTick` instead of per-turn `EndTurnEvent`s. **Any clock that counts turns or
   counts `EndTurnEvent`s silently under-counts exactly the activity that generates absence.**
   `The.Game.TimeTicks` is the only correct clock. (The mod already uses it everywhere; this is why
   it must keep doing so, and why `EndTurnEvent` is rejected as the city clock in §2.1.)
   — `D/XRL/World/Parts/TerrainTravel.cs:161, 221-264`; `D/XRL/Core/ActionManager.cs:1640-1662`
2. **`IGameSystem`'s convenience overrides are dead code.** `ZoneActivated(Zone)`, `EndTurn()`,
   `NewZoneGenerated(Zone)`, `GetPriority()`, `SaveGame`/`LoadGame` are all `[Obsolete]` **and have
   no call site anywhere in the engine.** Only registered event IDs fire. `KingdomSystem` already
   does this correctly and must not be "simplified" toward the overrides.
   — `D/XRL/IGameSystem.cs:2954-3070`
3. **`The.Game.GetSystem<T>()` is a linear scan with a `GetType()` call per element.** Never call
   it inside a per-object or per-row loop; resolve once per pass into a local.
   — `D/XRL/XRLGame.cs:286-300`

### 0.3 What stands in the mod, and what it is worth to this design

- **`Simulation/Kernel/`** — `CounterRandom` (a draw is a pure function of `(seed, SemanticEventKey,
  drawIndex)`; no stream object, no cursor, so retries/logging/batching advance nothing),
  `SemanticEventKey` (`rulesVersion × settlementId × eventStreamId × kindCode × ordinal`),
  `TickMath.TryCountFixedPeriodDue` (closed-form due count, **deliberately uncapped**, with the
  standing instruction that consumers fold it into bounded aggregates rather than looping),
  `FixedPeriodToyState` (immutable by construction: copy, validate, compute into locals, publish
  one new instance — "nothing is ever partially incremented, so a fault leaves the caller's state
  byte-identical"). **This is the model's substrate, unchanged.** It was built for exactly this.
- **The attended pass** — `KingdomSystem.HandleEvent(ZoneActivatedEvent)`: seat → exile → seceded
  → *claim guard* → survey → trade → growth → improvement → bounties → raids → wear → offices →
  reach → locus → guestbook → creed → faith → digest. Every step wrapped in `Guard(label, action)`.
  The order is load-bearing and documented step by step. **This is the reckoning's host.**
- **Zone sightings** — `KingdomSubsidence.RecordZone` writes five ints per claimed zone into
  `The.Game` game-state (`r_TAF_Supports_<zoneID>_{water,food,roof,storage,seen}`, dated in whole
  days by `SeenStamp`); `OtherZones` reads them back as `ZoneSighting`s; `CityTally` /
  `CityStorage` fold them into one city. Its own doc says the quiet part: *"Nothing here simulates
  an unvisited zone forward — a sighting is dated, stays exactly as old as it is."* **This is the
  city model in embryo, five ints deep. The design generalises it; it does not replace its
  discipline.** *(Retired in W1: `RecordZone` and `OtherZones` now write and read a zone row of
  `KingdomSettlement.City`, and `KingdomCrops`' parallel `r_TAF_Larders_*` pair with them. The
  arithmetic downstream is unchanged — `ZoneSighting` survives as the projection the rows hand it.)*
- **Crystallise-at-awareness** — Addendum 11(b-ii) already rules the harvest cycle: growth on
  world-time from a stamped planted-tick, harvest crystallises when due attended or not, the city's
  stores are credited at once while the **physical crop items materialise into the destination
  larder when that larder's zone is next active**, and the cycle restamps and repeats. That ruling
  is this architecture's materialisation contract, written before the architecture was.
  Its engine precedent is now doubly confirmed: `Temporary` for a one-shot deadline, and
  **`ZoneRepair`** for accumulated work applied in a batch on `ZoneActivatedEvent`
  (`BuildCounter += TimeTicks - LastTurn; num = Max(1, BuildCounter / TurnsPerObject)`) — the closer
  analogue for production, and the shape a work's run-state advance should be written in.
- **Brink windows** — `KingdomBrink` + `KingdomBrinkRules` + `KingdomWord`: warn at the crossing,
  coach the arrest, run the window in world-days, fire in absence, unsay on arrest. `KingdomWord`'s
  own doc-comment predicts this wave: *"If a future wave gives the realm a pass that runs without
  the founder, this is the one place that has to learn to hold a letter."*
- **The closed-form idioms already shipped** — `KingdomRules.PassagesThrough` (a repeating arrival
  clock run forward over an arbitrary absence, returning counts and one standing visitor, never a
  queue), `KingdomRules.RestampDeadline`, `ElapsedDays` / `AdvanceCheckpoint` (remainder kept,
  never re-anchored), `ActivityDays` / `LabouredTicks`, and — the reference implementation for
  everything in §2 — `KingdomSubsidenceRules.Slide`, *"closed-form convergence: the whole slide is
  computed at once from the elapsed, and its rung changes come back as dated breakpoints for the
  chronicle."*
- **Ledgers and chronicle** — `KingdomLedger` (per-visit arithmetic, brink lane first, ≤12 notes,
  ≤8 brink lines), `KingdomChronicle` (200 entries, disputed two-register recording),
  `KingdomReports`, and the telling budget the slide already keeps (`NamedDeparturesPerSlide`,
  `ChronicleEntriesFor`, `ChronicleBudgetPerSlide`).
- **The physical half** — `KingdomSurvey.Take(Zone, System)` walks a zone and reads *real* objects:
  `LiquidVolume`s, `Container`/`Inventory` larders, settlers, works, beds, defences. Stocks are the
  zone's own objects and always have been. `KingdomLiquids.Drain`/`Fill` measure the state delta
  rather than trusting a vanilla return value.

### 0.4 The three places the standing architecture cannot carry Addendum 12

1. **Stocks live in the zone.** `KingdomSurvey` reads the ground under the founder's feet.
   A granary in the zone next door is five ints old. A city that *produces and consumes* across
   four zones cannot be measured that way.
2. **People live in the zone.** `KingdomGrowth.SpawnSettler` mints a real `GameObject`, and
   per-settler mod state — the roof brink's `KingdomBrinkRoofTick`, the creed brink's window,
   origin, name — is stored **as properties on that object** (`KingdomBrink.RoofTickProperty` and
   siblings). A settler in a suspended zone is therefore unreadable and unmovable: their window
   cannot run, their work cannot be counted, their wedding cannot happen.
3. **The Charter is full.** 32 options, every letter `a`–`z` and the digits `1`–`6` spoken for
   (`KingdomCharterPart.OpenMenu`, and the comment above it says so). **Engagement cannot add a
   parallel surface. It must deepen the ones that exist.** This is a hard constraint on §5, and
   a good one.

---

## 1. The model

### 1.1 The governing idea

> **The city is a book. A zone is a page of it that happens to be open.**

Authority alternates, explicitly, and never overlaps:

- **While a zone is attended, the ground is authoritative** and the model is a mirror. The player
  may pour water out of a cask by hand and the model must agree with the cask.
- **While a zone is suspended, the model is authoritative** and carries that zone's last-read
  numbers plus everything credited or drawn since.
- The handoff is a **check-out** (ground → model) and a **check-in** (reconcile, then materialise).

This is not new doctrine. It is `RecordZone`'s existing discipline — *"rewritten from the ground
every time, including down to zero: a reservoir that was struck stops counting toward the city the
pass the founder sees the empty plot, and never before"* — widened from five ints to the whole
city, and given the other half (the model runs forward between sightings) that Addendum 12 asks for.

### 1.2 What the model owns

One `KingdomCityState` per settlement. Proposed home: `Simulation/City/`, beside the kernel,
because it is simulation substrate rather than a feature.

**(a) Stocks — the civic share only.**

| Row | Contents |
|---|---|
| `Water` | Drams in **dedicated** vessels, city-wide. |
| `Food` | Servings in **dedicated** larders, city-wide, with the `PantryTier` classification the survey already computes. |
| `Materials` | The refined tiers `KingdomMaterials` already names (shaped timber / shaped stone / worked metal), plus raw. |
| `Capacity` | Per-kind ceiling, summed from the zone rows. |

**Stocks are city-level, not zone-level, and that is the point.** Water raised by an air-well in
the mine, food grown on the terrace above it, and power made in the yard are all one set of rows;
consumption anywhere draws on the same rows. So a generator wearing out in a zone the founder has
not seen for a week **moves the city's numbers at the very next slice** (§3.6), and the founder
standing in their house reads a lower level, hears the shortfall, and can act on it without
walking anywhere. Cross-zone flow is not a feature bolted onto the model — it is what having one
book instead of four sightings *means*.

Player-carried and player-placed-but-undedicated stock stays purely physical and **outside the
model**, exactly as the survey already classifies it. This is what keeps the protection law simple:
the model only ever speaks for things the founder designated as the city's.

**(b) Zone rows — one per claimed zone, at most four.**

`ZoneId`, `District`, `LastReadTick`, and the stocks/capacity/roofs/defence read at that
check-out. Replaces the `r_TAF_Supports_*` game-state keys wholesale. `ZoneSighting` survives as
the projection these rows hand to `KingdomSubsidenceRules.CityTally`, so the subsidence arithmetic
does not change at all — it simply stops reading a dictionary of ints.

**(c) Works — one row per standing work, bounded by `KingdomRules.MaxBuildings` (40).**

`WorkId` (stable, minted at raising), `ZoneId`, `Anchor` (cell x/y, for materialisation and for
the ruins rule), `DesignKey`, `Condition` (the wear percent `KingdomWear` already owns),
`CrewAssigned`, `RanThroughTick`, and one small discriminated slot for kind-specific run-state:

`CrewAssigned` is derived on check-in from authoritative resident rows whose exact `JobWorkId`
and `BoundZoneId` match the work. Object staffing flags and legacy roster projections are not crew
authority; a seat exchange therefore cannot lend one city's hands to another.

- growing ground → `PlotStage` + `NextStageTick` + `CropBlueprint` (today's `r_KingdomPlot` fields,
  moved off the object);
- store → nothing beyond condition (the leak rule is already a function of condition and days);
- producer / refiner → `ProgressTicks` and the output kind;
- power → charge.
- active construction → the construction kind and resident-derived crew only; its durable
  construction receipt remains the sole owner of progress.

The rule that makes this bounded and honest: **a work's row carries state the engine cannot carry
for it, and nothing else.** Appearance, name, tile, contents stay on the `GameObject`. The row is
not a second copy of the building.

**(d) Residents — one row per settler, bounded by `KingdomRules.MaxPopulation` (60).**

`ResidentId` (stable), `Name`, `Origin`, `Creed`, `Arrived`, `HomeWorkId`, `Job` (`WorkId` + role),
`DayShape` (below), `Standing` (`Resident` / `Abroad` / `Dead`), `BoundZoneId`, and the brink
window fields that today live as object properties (`RoofTick`/`RoofWarned`,
`CreedTick`/`CreedWarned`/`CreedToward`/`CreedChannel`).

`DayShape` is a small enum naming where a person's day puts them —
`Field`, `Yard`, `Market`, `Craft`, `Watch`, `Shrine`, `Hearth` — derived from `Job` and the
settlement's standing policy, never authored per-settler. It is read against `Calendar`'s own
bands (§3.2). It is **not** a schedule object and holds no times: the mapping from band to place
is a pure function in `KingdomCityRules`.

**(e) Clocks — a handful of named `(kind, nextDueTick, ordinal)` triples.**

Harvest per growing work, arrival, guest, notable guest, festival, market day, delivery, raid.
Most of these already exist as `long` fields on `KingdomSettlement` (`NextArrivalTick`,
`NextGuestTick`, `NextNotableGuestTick`, `RaidDueTick`); they consolidate here and gain an
**ordinal**, which is what makes their draws reproducible (§2.4).

**(f) The told-log — a bounded ring of the last K happenings.**

`(Kind, Tick, SubjectIds, PlaceZoneId, Outcome)`, K ≈ 32. Feeds the "As others tell it" register,
the guestbook, and the told-once guarantee. **Not a queue of pending work** — every entry in it has
already happened. This distinction is the kernel's own, stated in `FixedPeriodToy`: *"This is
historical identity proof, not a due-job queue: current scheduling lives in `NextDueTick`."*

### 1.3 Where it lives, and how it is frozen

`KingdomSettlement` is already an `IComposite` with `WantFieldReflection => false` and named-field
serialization — the durable-system opt-out STANDARDS §1 requires. The model is one field on it:

```
KingdomSettlement.City : KingdomCityState        // named-field composite, versioned
```

Not `The.Game` game-state string keys. Those were the right answer for five ints that had to be
readable without loading a zone; they are the wrong answer for a hundred typed rows, and they
retire in Wave 1.

**Frozen-model doctrine, in the shape this codebase already uses.** The house rule is not "make the
serialized carrier immutable" — Qud's named-field reader must assign fields. It is the
`FixedPeriodToyState` rule:

- The **rules** layer (`KingdomCityRules`, engine-free, `Simulation/City/`) takes an immutable
  snapshot in and returns a new immutable snapshot out. `readonly struct` rows, `sealed` state,
  every `Try*` total over representable input, publishing nothing on a fault.
- The **carrier** (`KingdomCityState : IComposite`) is written by exactly **one** publisher, in one
  assignment, after the rules have succeeded. Nothing is ever partially incremented; a fault leaves
  the settlement byte-identical and the pass's `Guard` logs it.
- State transitions are copy-on-write. There is no in-place mutation of a row anywhere outside the
  publisher.

This is the same contract the kernel already keeps and the same reason it keeps it: a partially
advanced model that survives into a save is a wrong answer that outlives the bug.

### 1.4 How it stays bounded

Every dimension has a cap that already exists or is added here, and **no dimension grows with
elapsed time**:

| Dimension | Cap | Source |
|---|---|---|
| Cities | 2 | `KingdomSettlement.MaxSettlements` |
| Zones per city | 4 | `KingdomZoningRules.ZonesForStage` (Camp/Steading 1 … City 4) |
| Works | 40 | `KingdomRules.MaxBuildings` |
| Residents | 60 | `KingdomRules.MaxPopulation` |
| Clocks | ~12 | fixed set, named |
| Told-log | 32 | new, ring |
| Breakpoints per reckoning | 64 | new, with an honest overflow (§2.3) |
| Chronicle | 200 | `KingdomChronicle.MaxEntries` |
| Ledger notes / brink lines | 12 / 8 | `KingdomLedger` |

Worst-case model: ~2 × (4 + 40 + 60 + 12 + 32) rows ≈ **300 rows, a few kilobytes**, inside a block
the save already writes.

---

## 2. The clock

### 2.1 Where it runs

One new step, `reckon`, in the existing guarded pass order, placed **after `survey` and before
`trade`**:

```
seat → exile → seceded → [claim guard] → survey → checkin → RECKON → trade → growth →
improvement → bounties → raids → wear → offices → reach → locus → guestbook → creed → faith →
MATERIALISE → digest
```

The placement is load-bearing for the same reason `trade` runs before `growth` is: what the city
made while the founder was away has to be in the stores **before** upkeep is drawn from them, or a
harvest that landed on the day of the drought would arrive one step too late to stop the emigration
it prevented. `materialise` runs last-but-one, after every system has settled its numbers, so the
zone renders a decided state rather than an intermediate one.

**The realm reckons all its cities at any settlement pass, not just the seated one.** The founder
walking into city A reckons city B as well. Cost is trivially bounded (2 cities), it is what makes
the second city alive instead of frozen, and it is what Addendum 10(a) already implies — *word
reaches the player wherever they are*. Only the *seated* city materialises; the other is reckoned
and told, never rendered. This preserves `KingdomWord`'s send-not-outbox property exactly: word is
still only ever made at a moment when there is a founder standing somewhere to hear it.

**Why not a per-turn hook.** `EndTurnEvent` reaches every `IGameSystem` every turn regardless of
which zones are loaded, and looks like the obvious home for a city clock. It is the wrong one, for
a reason that only shows up in play: **it does not fire during world-map travel.** `TerrainTravel`
advances `The.Game.TimeTicks` by 300–900 per parasang step and runs one batched `ProcessTurnTick`
instead. A city clock on `EndTurnEvent` would therefore stop precisely while the founder is doing
the thing that creates the absence. Reckon-at-activation off `The.Game.TimeTicks` deltas has no
such blind spot, costs nothing per turn, and is what the mod already does everywhere else.

### 2.2 The shape of an advancement

```
KingdomCityRules.TryAdvance(snapshot, nowTick, out next, out trajectory, out fault)
```

Pure, engine-free, total. `trajectory` carries dated breakpoints and a bounded happening list for
the telling layer; `next` is the new snapshot. `KingdomReckon` (engine-facing) calls it once per
city per pass, publishes on success, and logs on fault through `Guard`.

Idempotent by construction: the snapshot carries `ProcessedThroughTick`, advanced with
`KingdomRules.AdvanceCheckpoint` (previous + whole units consumed, remainder kept, **never
re-anchored to now** — re-anchoring forgives the remainder and the clock rework already retired
that). Calling `TryAdvance` twice at the same tick is a no-op, which is what makes a save/reload
mid-pass safe.

### 2.3 O(model), not O(days) — breakpoint integration

The naive shape — loop a day at a time — is forbidden: a season away is 90 iterations of a whole
city and grows without bound. The correct shape is the one `KingdomSubsidenceRules.Slide` already
uses and this generalises:

> **Between two consecutive breakpoints, every rate in the model is constant.** So integrate
> linearly to the next breakpoint, apply it, and repeat — and the number of breakpoints is bounded
> by the *model*, not by the *elapsed*.

A breakpoint is any moment a rate can change:

- a stock hits empty or full (a solvable linear crossing, computed, not searched);
- a crop's `NextStageTick`;
- a periodic clock's next due tick;
- a brink window's expiry;
- a subsidence rung change (`Slide`'s existing `Breakpoint`);
- a stage change, which changes upkeep and therefore every rate at once.

Each of those is *computed* as "the tick at which this will happen at the current rates", the
minimum is taken, and the model steps there. Because every step consumes at least one structural
change, and the model has a bounded number of structural changes available, the loop terminates in
O(model). The 64-cap is belt-and-braces, and its overflow is honest rather than silent: on hitting
it, the model **jumps to the fixed point** — the equilibrium `KingdomCatalogueRules.Equilibrium`
already computes — and dates the remainder as settled. That is not a forgiveness cap in disguise;
it is the same convergence the slide already promises, reached by arithmetic instead of by steps.

Two shapes carry most of the work and both are already shipped:

- **Fixed-period lanes** (harvest, arrivals, guests, festivals, deliveries) use
  `TickMath.TryCountFixedPeriodDue` — O(1) for a count of any size — folded into a bounded
  aggregate by the consumer, exactly as its doc-comment demands. `KingdomRules.PassagesThrough` is
  the shipped example of a consumer doing it right: *a run that came and went unwitnessed is one
  dated line, not a queue standing since spring.*
- **Rate lanes** (wear, conversion, dissent, road traffic) use `ActivityDays` / `LabouredTicks`:
  days scaled by how hard a thing was actually run. One multiply per work or resident. Addendum 8
  clause 2 — *rates are time × labour × infrastructure, never time alone* — is enforced here and in
  exactly one place.

### 2.4 Where determinism draws anchor

One `KernelSeed128` per realm, minted at founding and stored on `KingdomSystem`, domain-separated
on realm incarnation (the kernel's own instruction: *"live seed generation belongs to the founding
slice"*). Every draw in the reckoning is:

```
CounterRandom.TryDrawBelow(seed, SemanticEventKey(rulesVersion, settlementId, streamId, kindCode, ordinal), drawIndex, bound)
```

- `streamId` is per-lane and per-source: `taf:stream:field.7`, `taf:stream:happening`,
  `taf:stream:arrival` — a distinct stream per source, never a stream-global counter, because
  `SemanticEventKey`'s own doc says two routes at ordinal zero must not collide.
- `ordinal` is the **occurrence index within that stream**. This is the whole trick: the seventh
  harvest of field 3 draws the same numbers whether it is resolved on the day it fell or six cycles
  later inside one reckoning, and a reload reproduces it. It is why the counter-random has no
  cursor.
- `rulesVersion` is frozen into the key at creation, so a rules upgrade cannot retroactively
  re-roll a harvest the chronicle already described. `KingdomSubsidenceRules.RollRuin` already
  relies on exactly this property.

**Nothing in the reckoning may draw per day.** Draws are per *happening*, per *occurrence* — never
per unit of elapsed time. This is both a determinism rule and the performance rule (a SHA-256 per
day per resident would be the only thing here that scales with absence).

---

### 2.5 The executor seam — build it now, thread it later

The author's question: *"should we use our own thread for this? future proofing, would that make
things materially harder or more fragile?"* The answer that survives both futures is **build the
seam now, run the thread later.**

**One choke point.** Every piece of model computation in this design — `TryAdvance` (§2.2), the
micro-reckon slice (§3.6), the route plan (§3.10), the network solve (§3.11) — goes through one
call, and there is never a second path:

```
KingdomExecutor.Submit<TIn, TOut>(TIn snapshot, IComputation<TIn, TOut> job) -> Result<TOut>
```

**Synchronous today.** It invokes the job inline and returns. That is the whole implementation, and
it earns its place immediately regardless of threading: it is where the timers (§6.5), the budget
checks (§0.0) and `Guard`'s fault handling stop being copied to four call sites.

**The contract is the actual deliverable:**

1. **Immutable in, immutable out.** The input is a frozen model value; the output is a new frozen
   value. Nothing is mutated across the boundary.
2. **No engine type crosses it.** No `GameObject`, no `Zone`, no `Cell`, no `The.Game`. The
   `*Rules.cs` engine-free discipline already guarantees this for every rules module this design
   names — the seam is where it becomes **checkable** instead of merely conventional.
3. **Budget and timeout belong to the seam, not the job.** A computation that exceeds its budget is
   abandoned and logged, and the caller's state is byte-identical because nothing was published
   (§1.3).
4. **A job may not read the clock.** `nowTick` is an input, never an ambient read — which is also
   what makes a job replayable in a test.

**Enforcement is a reflection test, not a review habit.** One unit test walks the type closure of
every `IComputation`'s input and output and fails the build on: a type from a Qud assembly, a
mutable public field on a rules type, or a non-`readonly` static. That is the idiom this codebase
already uses to keep `*Rules.cs` pure, promoted from a convention into a build failure — one test,
and it is the only thing standing between this design and a second computation path growing
quietly beside the first.

**The swap path, and why it is genuinely cheap.** A threaded executor replaces `Submit`'s body with
a queue and a worker. The input is already immutable, so there is nothing to lock; the result is
already published in one assignment on the caller's side (§1.3), so publication stays on the main
thread where Qud's serialization expects it; and **no call site changes.** If the workload never
justifies a thread — which §0.0's numbers strongly suggest it will not — the seam has still paid
for itself in timers, budgets and fault isolation.

**Third-party computations inherit all of it.** A mod that wants the city to run a lane of its own
submits through the same seam and gets the same budget, timeout and isolation: **a misbehaving job
stalls itself, never the city and never the turn.** That is a property no amount of documentation
could give a direct call.

**Threading eagerly, with no workload, is rejected as the fragile option** — a worker thread over a
sub-millisecond job buys latency it did not have and a class of bug it did not have either. The
seam costs nothing and is the opposite of fragile.

## 3. Materialisation

### 3.1 Check-in — reconcile before rendering

On `ZoneActivatedEvent` for a claimed zone, after `survey` and before `reckon`:

1. Read the ground. `KingdomSurvey.Take` already does this and does not change.
2. Compare against the zone row's last check-out.
3. **The ground wins for anything physical.** A cask with less water in it than the model expected
   means the founder poured some; a struck work means it is gone; a dead settler means they died.
4. **The difference is attributed and told, never silently repaired.** Signed against a plausible
   cause: a withdrawal the founder made, a raid's plunder already accounted, or unexplained loss.
   The mod's own voice on this is already written, about the manifest: *"water arrives with nowhere
   to go, and that is a story rather than a bug."*
5. Set the zone row's `LastReadTick` to now.

The invariant to test: **model total == ground total immediately after an attended pass of a
fully-visited city**, and a hand-moved dram neither mints nor destroys a dram.

### 3.2 Render this zone's share

After the whole pass has settled, `materialise` renders the seated zone. Three kinds of output —
and **none of them is rendered all at once.** What follows is *what* a zone owes; §3.5 is *how much
of it is paid per turn*. Read the two together: the figures below are debts, not spikes.

**(a) Items into containers.** The harvest that fell while the founder was away credited the city's
stores at the moment it was due (Addendum 11(b-ii)); the *physical* crop items are created into the
destination larder's real `Inventory` now, on the pass that opens that larder's zone. Same for
refined materials into stockpiles and for water into dedicated `LiquidVolume`s via
`KingdomLiquids.Fill` — which measures the delta rather than trusting `AddDrams`, per STANDARDS §1.
Landed out of the per-turn budget (§3.5), never in one activation; the remainder stays credited
in the model and lands on later turns, and the overflow line the ledger already has (*"left in the
field for want of a larder"*) is the shape for anything that genuinely cannot land at all. Where
the delivery's destination is the zone the founder is standing in, it may arrive **embodied** —
carried in by a porter the founder watches (§3.7).

**Adopt vanilla's item-protection protocol verbatim.** `GenericInventoryRestocker.PerformRestock`
is the engine's own answer to *which items may I destroy*, and it is exactly the discipline the
mod's protection law wants:

- `_stock` — "the simulation created this; the simulation may remove it";
- `norestock` — "never touch, whoever put it here";
- `IsImportant()` — never destroy, no exceptions.

Materialised items carry `_stock`. Anything the player added to a larder by hand does not, and is
therefore untouchable by anything we do. This gives the reconciliation in §3.1 a clean rule for
*who moved what* at no design cost, and it is a vanilla convention rather than an invention.
(`D/XRL/World/Parts/GenericInventoryRestocker.cs:148-200`.)

**(b) People, by role and by the hour.** Vanilla ships **no** NPC scheduler — no
`GoToPartyLocation`, no `Schedule` class, no calendar-driven villager behaviour anywhere. What it
ships is one hook, and it is enough:

- **The anchor is free.** A settler with `Brain.Wanders = false`, `WandersRandomly = false` and no
  `NoStay` tag **self-anchors** to the cell it first stands in (`Brain.StartingCell`, set on the
  first `EnteredCellEvent`), and the `Bored` goal walks it back there forever. `Brain.Stay(cell)`
  sets it explicitly. Materialisation therefore does not *place* people so much as **move the
  anchor** — and vanilla's own AI does the walking. (`D/XRL/World/Parts/Brain.cs:2056, 2507`.)
- **The daily life is `IdleQueryEvent`.** `Bored` gathers every object in the zone that wants the
  event, shuffles them, and offers each one the idle actor; **returning `false` claims that actor's
  turn.** This is *literally* how vanilla beds send villagers to sleep at night — `Bed` gates on
  `IsNight()`, pushes `MoveTo(bed)` then a `DelegateGoal` that sleeps on arrival, and returns
  `false`. There is no other mechanism in the game. (`D/XRL/World/Parts/Bed.cs:187-224`;
  `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330`.)

So the design is: **the model decides where a person belongs at this hour; the anchor is set at
activation; and an `r_` part on the workplace claims them through `IdleQueryEvent` while the
founder watches.** Concretely — one small part, `r_KingdomStation`, on each work, handling
`IdleQueryEvent`: if the actor's `KingdomResidentId` is rostered to this work, and the current
`Calendar` band matches the role's, push `MoveTo(this)` plus a `DelegateGoal` for the flavour
(tend, haul, pray, keep watch) and return `false`.

| `Calendar` band | Ticks (of 1200) | Where a `DayShape` puts a person |
|---|---|---|
| The Shallows / Harvest Dawn | 151–450 | rising: hearth → workplace |
| Waxing / High / Waning Salt Sun | 451–750 | `Field`, `Yard`, `Craft`, `Market`, `Watch` at post |
| Hindsun | 751–900 | trades wind down; `Market` and `Shrine` busiest |
| Jeweled Dusk | 901–1050 | homeward; `Hearth` fills |
| Waxing Beetle Moon → Zenith → Waning | 1051–150 | `Hearth`, except `Watch` — and vanilla `Bed` already does this one for us, free, for any settler tagged `SleepOnBed` |

This is the whole reason to ride the hook rather than teleport people: **the founder standing in
the city at dusk sees the market empty itself and the hearths fill, one settler at a time, walking**
— and it costs us one `IdleQueryEvent` handler and zero per-turn work. Settlers need
`AllowIdleBehavior` and `SleepOnBed`; vanilla's `NPC` blueprint already grants both, and the
`r_KingdomSettlers` population table is where that is arranged.

Three constraints the hook carries, and all three are fine:
1. `Bored` does nothing when the actor is not in the player's zone — idle behaviour is
   attended-only, which is exactly the division of labour this architecture wants.
2. Returning `false` costs the actor its turn, so a station must be selective (vanilla's own
   handlers gate on `1.in100()` / `1.in10000()` plus a per-object cooldown) or the settlement
   stands around doing one thing. §3.6 gives that constraint a number.
3. The `IdleObjects` cache is zone-scoped and rebuilt on `IdleDirty`; a station added mid-play is
   picked up by `WantEvent`, so no registration list to maintain.

**The bodies.** A resident's `GameObject` is a **view bound by id**, never the state. See §8, hard
problem 2 — the short form is: materialisation *mints* a body only for a resident with no living
bound body in this zone, *moves* an existing one, and **never removes one**. The protection law is
not weakened; it is extended to our own people.

**(c) Dated events, told once.** Happenings the reckoning generated surface through the ledger
digest (pull, at the seat), `KingdomWord` (push, for brinks and irreversibles), and the chronicle —
each dated to when it happened, not to the pass that found it, which is already how
`KingdomWord.Aftermath` is specified. Told-once is a `TellingsThroughTick` stamp in the model, so
re-entering the zone does not retell.

### 3.3 Vanilla does the visible work (Addendum 11(c) order)

Materialisation's job is to **set the ground up so that vanilla parts then do the thing**, never to
puppet what a vanilla part would do while attended:

1. **Inherit-and-extend** — the work's blueprint genuinely is a `LiquidProducer` / `Mill` /
   `ItemConvertor` / `FoodProcessor` / `SolarArray` / `Capacitor`, and while the founder stands
   there it visibly works on the engine's own clock. Its output is *not* the number the economy
   reads (VANILLA-PRODUCTION-TRUTH's settled rule: *"Vanilla parts are the visible face of
   production. The settlement's own tick-stamped pass is the accounting."*)
2. **Wrap** — an `r_` part driving a vanilla part's real behaviour on our clock, the way
   `r_KingdomPlot` does today (absolute-tick comparison in `TurnTick`, so missing ticks costs
   nothing, unlike vanilla's dead `Harvestable.RegenTimer`).
3. **Fill in** — only where the survey proved vanilla is empty: seeds, spoilage, multi-stage
   growth, pressing/fermenting, inter-zone haulage. All four are already on
   VANILLA-PRODUCTION-TRUTH §8's inspire-only list, with the reason.

### 3.4 Check-out — what un-materialises

**Nothing is destroyed.** Un-materialisation is a *read*, not a teardown. Three hooks, in the order
they fire, and only the last is load-bearing:

1. **`ZoneDeactivatedEvent`** — the founder walked out. Useful as a *hint* (stamp "left at tick T"),
   but **not** the moment to take the numbers, because the zone goes on simulating for up to 40 more
   turns (§0.1): vanilla producers keep producing, effects keep ticking, and a reading taken here
   would be wrong by whatever happened in the grace window.
2. **`SuspendingEvent`** — the true last read. Fires from `SuspendZone` *before* `Suspended = true`,
   for **any** zone as it suspends, and reaches `The.Game`. At this instant the zone is still fully
   in RAM and nothing further will happen in it. **Take the numbers here.** This is the single most
   useful hook the survey turned up and the mod does not use it today.
3. **Lazy check-in reconcile — the correctness guarantee.** Neither event fires on save-and-quit, on
   a crash, or on any path that bypasses the transition. So the model must *also* be correct when
   check-out never happened — which it is, because check-in reconciles against the ground anyway
   (§3.1). **Design so that a missed check-out costs freshness, never correctness.** Everything
   above is an optimisation on top of that.

Bodies and items stay in the zone and are frozen to disk with it (`FreezeZone` → Brotli → SQLite →
`Zone.Release()`). `ZoneThawedEvent.TicksFrozen` is available as a cross-check on how long they were
gone, and is deliberately **not** used as a clock: it measures frozen time only, and says nothing
about suspended-but-resident time.

### 3.5 Amortised materialisation — the counter, the budget, and the invariant

§3.2 says *what* a zone renders. This says *how much of it, when* — and it is the difference
between a design that satisfies Addendum 12(b) and one that merely claims to. **Entry must cost
O(budget), never O(elapsed).**

**The idiom is the engine's own, generalised.** `ZoneRepair` keeps a counter, adds the elapsed to
it at every activation, and converts it to a unit count by integer division:

```
BuildCounter += The.Game.TimeTicks - LastTurn;              // ZoneRepair.cs:51-52
LastTurn      = The.Game.TimeTicks;
if (BuildCounter < TurnsPerObject) return;                  // :53   nothing owed yet
long num = Math.Max(1L, BuildCounter / TurnsPerObject);     // :57   units owed, floor of 1
BuildCounter = 0L;                                          // :58   debt cleared on read
... apply num units ...                                     // :87-97
if (ToBuild.Count == 0) ParentZone.RemovePart(this);        // :99-102  self-remove when drained
```

Three properties are worth taking outright, and one is worth refusing:

- **Take:** the counter is a *quantity of owed work*, never a queue of dated jobs — so a season of
  absence and a day of absence differ in an integer, never in shape.
- **Take:** `Math.Max(1, …)` guarantees forward progress; a debt cannot stall on a rounding edge.
- **Take:** the part removes itself the instant its backlog empties, so a caught-up zone costs
  literally nothing.
- **Refuse:** `ZoneRepair` applies the whole `num` in one loop inside the activation (`:87-97`).
  Its unit is a `Cell.AddObject` and its backlog is one map file, so the spike is invisible. Ours
  is body mints and container fills against a sixty-resident roster. **We keep the counter and
  spend it on a per-turn budget instead** — and that single change is the whole of Addendum 12(b)'s
  *reification is AMORTISED*.

**Two hooks, two different jobs.** Both are reify hooks. They bind different steps because
different state is available at each.

| Hook | Fires | State available there | Our step |
|---|---|---|---|
| `ZoneThawedEvent` | from `Zone.Thawed(FrozenTicks)` at the end of `TryThawZone`, after `Zone.Load`, `AddCachedZone`, `ForceCollect()` and `PaintWalls`/`PaintWater` — for **any** zone coming off disk, active or not | the full object graph, in RAM; `TicksFrozen = TimeTicks − FrozenTick`; the zone is **not** active, its objects are **not** live, and `Suspended` is still whatever was saved (it is a serialized field) | **debt intake only.** Compute this zone's catch-up counter from the model's `ProcessedThroughTick` against the zone row's `LastReadTick`. Render nothing. `TicksFrozen` is a cross-check on the counter, never its source (§3.4). |
| `ZoneActivatedEvent` | from `Zone.Activated()` inside `SetActiveZone` — **after** `MarkActive()` and the outgoing `Deactivated()`, but **before** `ActivateObjects()` and `Suspended = false`; and again on game load, with no thaw at all | the zone is `ActiveZone`; its objects are **not yet live or active**; `Suspended` is still `true` | **the pass** (seat → survey → check-in → reckon → … → materialise) and the **first budget spend**. Mutating the ground here is correct and cheap: we place, and the engine makes live what we placed one line later — the same order zone generation already uses. |

`ZoneActivatedEvent` is the only one of the two guaranteed on the entry path: a suspended-but-
resident zone is entered with no thaw, and a game load activates with no thaw. So intake is
**recomputed idempotently at activation** too — which costs nothing, because the counter is
*derived from two stamps* rather than accumulated from events.

**The spend.** Each turn, while a claimed zone is seated and its counter is non-zero, the pump
(§0.0(e)) spends up to the budget:

1. **Visible cells first.** Units whose anchor cell is in the player's current field of view go
   first. This is what makes the guarantee *perceptual* rather than merely amortised — what the
   founder is looking at catches up first, and the rest fills in behind them as they walk.
2. **Then the rest,** in stable row order (works by `WorkId`, residents by `ResidentId`) — stable
   so a save and reload mid-catch-up resumes in exactly the same place.
3. **Stop at the budget.** Not at the debt.

For container debt, these are literal physical rows: visible containers first, then stored
dedication ordinal and stable object id. One unit touches one container and may move only that
container's measured room/contents. Up to all eight medium units may therefore drain eight vessels
of the same stock kind in one turn. Mutation callbacks are measured before debt and budget move;
failure leaves the unpaid quantity on the serialized zone row.

**What happens if the founder leaves before catch-up finishes.** Nothing special, and that is the
whole point.

> **The catch-up invariant.** *For every zone, at every instant: the model is authoritative for
> exactly the part of the debt that has not been reified, the ground is authoritative for exactly
> the part that has, and the counter is the boundary between them.*

Three consequences fall straight out of it:

- **No loss.** The counter is a field on the zone row, serialized with the model — not a transient.
  It survives suspend, freeze, save and reload. A founder who walks out at unit 40 of 132 walks
  back in owing 92, whenever that is and wherever they went.
- **No double-apply.** A unit leaves the debt at the instant it *lands*, not the instant it is
  scheduled — `ZoneRepair`'s own "debt cleared on read" (`:58`), applied per unit instead of per
  batch. Re-entering, reloading or re-activating cannot re-land a unit, because the model no longer
  owes it.
- **No wrong answer, only a stale view.** The un-reified remainder is still in the model, still
  counted by `KingdomReports`, still spendable by upkeep and trade. It is invisible on the ground
  and nowhere else. That is the staleness voice the mod already uses about a sighting, and the
  ledger says it in the founder's register when a zone walks in owing: *the granary has more in its
  books than on its shelves, and the hands are still carrying it in.*

Testable in one line: **model total == ground total + counter-owed, per stock kind, at every
instant.** That strengthens §8.2's first invariant rather than replacing it — the old one is this
one with the counter at zero.

### 3.6 The heartbeat — a bounded micro-reckon while the founder stands in the city

Addendum 12(c) rules what "almost real time" actually has to mean, and it is more than
presentation: *"if in one zone a generator, building, etc wears down enough to stop producing, or
meaningfully impact what you would find in the zone you are actively in, that needs to be simulated
and felt."* A city whose model only advances at the door is not living; it is merely reconciled.

**So the heartbeat is a micro-reckon, not a display.** Every `N` ticks while a claimed zone is
seated, the realm advances its cities' models by the elapsed delta and surfaces what changed.
Three distances are covered by three different mechanisms, and only the middle one is new:

| Distance | Mechanism | Cost to us |
|---|---|---|
| **The zone the founder stands in** | vanilla, live. Its producers tick, its actors act, its `EndTurnEvent` fires. The engine doing its job, already paid for | **zero** |
| **A zone just left** | vanilla's 40-turn grace: still cached, not suspended, still ticked (`D/XRL/Core/ActionManager.cs:443-449`). Stepping one zone over and back needs no reckoning at all (§6.3) | **zero** |
| **The rest of the city, however large** | the micro-reckon, below | **≈ 5 row-visits a turn** |

**The cadence: `N` = 50 ticks — one in-game hour, `Calendar.TurnsPerHour`.** Chosen, not guessed:

- **It is the game's own unit.** The day-shape bands the founder actually perceives are hour-scale
  (§3.2b); nothing finer than an hour is legible to a player, so nothing finer is worth computing.
- **It is far below the model's breakpoint density.** Crops advance in days, wear in days, clocks
  in hours to days. A 50-tick slice therefore resolves in **one or two** breakpoint steps, and the
  budget caps it at four; a slice that wants more has a pathological model and says so in the log.
- **It amortises to nothing.** One slice is one propose pass plus one or two apply passes ≈ `2R`
  row-visits, once per 50 turns: **≈ 5 row-visits per turn per city, ≈ 10 for the realm.** That is
  cheaper than the cursor the presentation-only version of this idea would have needed.
- **It has no special case for travel.** A slice advances by *whatever elapsed*, so several `N`
  boundaries crossed at once (a world-map step, a long rest) is one slightly larger slice — still
  closed-form, still one propose pass. `N` decides how often we bother, never how much we advance.

**It is the same `TryAdvance`, not a second code path.** A micro-reckon calls
`KingdomCityRules.TryAdvance(snapshot, nowTick, …)` exactly as the homecoming reckon does, and
advances `ProcessedThroughTick` by the same `AdvanceCheckpoint` discipline (§2.2). So a run of
micro-reckons followed by a homecoming reckon **is one advancement, split** — remainder kept, never
re-anchored, idempotent at a repeated tick. There is no path by which a slice and a pass can both
apply the same day. If the micro-reckon were a second implementation of the clock, this whole
section would be a bug factory; it is one call site more on a total function.

**All cities, not just the seated one.** Two cities is 10 row-visits a turn, and it is what makes
the second city's generator failure reach a founder who is standing in the first (§2.1 already
rules that the realm reckons all its cities at any pass — this simply makes it continuous).

**Surfacing, and its budget.** A slice's happenings are dispatched by destination:

- **Destination is the attended zone** → the happening becomes reify debt and arrives **embodied**
  (§3.7), out of the ordinary 8-unit budget. A porter is two units.
- **Destination is anywhere else** → the happening takes its model credit and is told, or held for
  the digest, under §4.2's existing telling budget.
- **≤ 1 told line per slice** — at most one ambient message an in-game hour, city-wide. A shortfall
  that has just begun says itself once and then lives in the status report, which is what
  `KingdomWord`'s send-not-outbox contract already requires.

**What it may not do.** It may not decide anything the reckoning would not have decided, it may not
draw outside the lanes §2.4 names, and it may not touch a zone that is not resident. It is the
clock running while somebody is watching — nothing more, and nothing less.

### 3.7 Embodied arrivals and journey continuity — the porter, and the itinerary

Addendum 12(c)'s canonical image: *"walking around in my house in 1 zone, a farm finishes
harvesting in another zone, a porter should come and put the harvested goods in the storage that is
in the zone i am walking around."* This is the moment the whole architecture exists to produce, and
it is nearly free, because every piece of it already stands.

**The shape.** A happening carries a destination (`DeliverTo(WorkId)`, `MendAt(WorkId)`,
`WordFor(ResidentId)`). When the destination's zone is the attended one at the moment the unit is
reified:

1. **Mint the carrier at the edge** — the zone edge nearest the source zone, or the gatehouse if
   one stands there. One reify unit, one body mint.
2. **Let vanilla walk them.** `Brain.PushGoal(MoveTo(cell))` to the destination work, then a
   `DelegateGoal` that does the thing on arrival — the identical construction `Bed` uses to send a
   villager to sleep (§3.2b). We write no pathfinding, no stepping, no animation.
3. **Deposit the real goods** into the real `Inventory` or `LiquidVolume`, under the
   `_stock`/`norestock`/`IsImportant()` protocol (§3.2a). One reify unit.
4. **Leave.** `MoveTo(edge)`, then despawn — the carrier is a visitor, not a resident, and never
   joins the roster or the population count.

Repair crews and messengers are the same four steps with a different `DelegateGoal`. Total cost:
**two reify units and a walk vanilla was going to do anyway.**

> **The one-event-two-renderings invariant.** *A happening has exactly one **effect** — applied
> once, at its dated tick, by `TryAdvance` — and at most one **rendering**: embodied if its
> destination is attended at the moment the unit is reified, credited-and-materialised-later
> otherwise. A rendering never re-applies the effect. It only moves goods the model already owes
> from the debt onto the ground.*

Which means, precisely:

- **No divergence.** Both renderings consume the same units from the same counter (§3.5). The
  stores were credited at the dated tick, as Addendum 11(b-ii) already rules; the porter is
  carrying goods that are *already the city's*.
- **No double-apply.** Choosing a rendering is a function of attendance, not a decision about what
  happened. A save reloaded with the founder standing somewhere else renders differently and
  **rolls nothing again**.
- **An interrupted porter is a story, not a fault.** If the carrier is killed or robbed en route,
  the goods are real items lying in a real zone under the protection law, the debt is already
  spent, and the next check-in (§3.1) finds the ground short of the model and attributes it —
  exactly the machinery §3.1(4) was written for.

**Where the porter's determinism draws anchor.** The `taf:stream:delivery` lane, with `ordinal` =
the delivery clock's occurrence index and `rulesVersion` frozen at creation (§2.4). So the same
delivery yields the same carrier — name, origin, which edge they enter by — whether the founder
watches it or reads about it afterwards. **The rendering choice is deliberately not a draw**, which
is what keeps a reload from re-rolling a person.

**A job is a timed itinerary, computed once, at creation.** Addendum 12(e): a carrier *"needs to
path to the correct zone … in the correct amount of time. If they come into my zone, fetch water,
i should be able to follow them back."* That is a stronger requirement than arrival, and it is met
by making the **model** answer the question — never the body.

**Where the state lives: on the job row**, in `KingdomCityState`, bounded by the same ≤ 16 open
jobs the registry caps (§3.8):

**Named-resident specialization — salvage expeditions.** An expedition is not a transient porter
or an offscreen menu roll. Its realm job row names one resident, one exact bound body, one
founder-visited journal destination, the quoted water/provisions, the frozen due world-tick and the
frozen bounded result. The prepared row publishes before any physical debit; an exact per-object
receipt on that body makes each water and provision leg resumable by compare-and-set after any save
cut. Paid and dispatched are monotone phases on the same job. The binding moves with the same body
and an unresolved or ambiguous binding is always a refusal to mint or charge. At due time the same
body returns once; a real death, disappearance, or decision to follow the founder instead closes as
a dated cause, while mere zone unavailability leaves the job open. Chronicle, homecoming line and
salvage are keyed by job id, so absence, seat exchange and reload cannot redraw or duplicate them.
This is the permanent-resident form of I2 and I5, not a second journey simulator.

```
Job = (JobId, Kind, Cargo, SourceWorkId, DestWorkId, StartTick, WalkTicksPerCell, Status,
       Legs[<= 6])
Leg = (ZoneId, EnterCell, ExitCell, PathLength, DepartTick, ArriveTick)
```

Six legs is the cap: a nine-zone city's diameter is four or five zone steps, and a job that wants
more than six is refused at planning and told. From these rows one pure function answers
everything:

```
KingdomItinerary.At(job, tick) -> (ZoneId, Cell, CargoRemaining, Phase)
```

A linear scan of ≤ 6 legs for the one containing `tick`, then interpolation along it. **One answer,
and every zone renders that same answer** — which is I5, and which is why the body never has to
literally traverse anything. Consistent re-rendering is *indistinguishable from* following, and
costs a fraction of what following would.

**What is stored, and what is re-derived.** Storing a full cell path would be up to eighty entries
a leg. So the leg stores its **endpoints and its length**, and the in-between is re-derived at
render time by walking the *live* zone's real pathfinder from `EnterCell` toward `ExitCell` for
`floor(progress × PathLength)` steps. **The endpoints are model truth; the in-between is a
redrawing that may differ by a cell or two if the ground changed** — a wall raised across the route
moves the carrier's drawn position and not their arrival. That is the right trade, and it is
self-correcting because the endpoint is the thing that is stored.

**Path length at creation: estimated, never pathfound.** Creation happens at *reckon*, over a
frozen model — most route zones are on disk, and **reckon may not touch a zone** (§0.0(d)). So:

- **At creation**, `PathLength = Chebyshev(EnterCell, ExitCell) × Sinuosity`, where `Sinuosity` is a
  named rules constant per district (open ground ≈ 1.25, built-up ≈ 1.6) in `KingdomCityRules`.
  Cost: O(legs) integer ops, and **zero zone access**. That is the cost bound, and it is absolute.
- **At render**, the first time a leg's zone is resident, the real path length is measured once and
  the leg re-projects (below). The estimate is a prior; reality corrects it, and only for legs the
  founder actually witnesses.

**The re-projection rule, after a live delay or a corrected length:**

> **Only the unstarted remainder of an itinerary may move.** A leg already begun keeps its
> `DepartTick`; the current leg's `ArriveTick` and every later leg shift by the same signed delta;
> `StartTick` and completed legs are immutable.

So a porter the founder body-blocks for ten turns arrives ten turns later and everything downstream
shifts by ten — no rubber-banding, no catch-up sprint, no time travel. It is computed at check-in
(§3.1), where the ground already wins: read the body's actual cell, compare against `At(job, now)`,
convert the difference to a tick delta on the current leg. One subtraction. Bounded: **at most one
re-projection per leg**, and a job whose elapsed exceeds twice its projected duration **fails** and
is told — so a founder who blocks a doorway forever produces a story, not an unbounded job set.

**Interference while attended: the ground wins, and failure is honest.** Death or robbery **fails
the job** — the binding is evicted (§3.8), the cargo stays **where it fell** as real items under
the protection law, and the city's stores are **debited back** by the lost amount, because
Addendum 11(b-ii) credited them at the dated tick. That is the one place a credit is ever reversed,
and it is therefore the one place that must always be told. **Never double-delivered, never
silently restored.**

**The edge handoff, and the timing that stops it popping.** At Speed 100 an actor is granted 100
energy per segment (`D/XRL/Core/ActionManager.cs:740`) and acts at 1,000
(`:741, 755`); a move costs 1,000 by default (`D/XRL/World/Parts/Physics.cs:3801`); ten segments
make one turn and one `TimeTicks` (`D/XRL/Core/ActionManager.cs:1644-1655`). **So at Speed 100 an
actor covers exactly one cell per tick** — carrier and founder alike. Transients are minted at
Speed 100 and `WalkTicksPerCell = 1`, so `PathLength` cells is `PathLength` ticks and a founder
walking beside a porter neither outpaces them nor falls behind. Where a carrier's blueprint has a
different speed, `WalkTicksPerCell` must equal its real per-cell tick cost — and if it does not,
check-in's re-projection corrects it every leg, so the constant being wrong costs accuracy, never
consistency.

Following one across an edge, step by step:

1. Leg `k` ends at `ExitCell` on zone `Z_k`'s edge at `ArriveTick`.
2. Leg `k+1` begins at the `EnterCell` **the engine's own zone connection maps that exit cell to**.
   It is not a choice, so it needs no draw and cannot disagree with where the founder comes out.
3. The founder crosses and activates `Z_{k+1}`. Materialisation places the carrier at
   `At(job, now)` — and because `now ≈ ArriveTick` with the leg just begun, that is **just inside
   the entry edge, a cell or two along.** They are where the founder expects them.
4. Cross slower and the porter is further along; cross faster and they are right at the edge. Both
   are correct renderings of the same one answer, which is the whole point of I5.

**Two acceptance tests, not one.** *Deliver-to-my-zone* (§6.5, Pass 32 step 90d) is the author's
canonical scenario. *Follow-the-porter* (step 90d2) is its continuity twin, and the harder of the
two: a handoff that pops is a visible failure of I5 even when every number is right.

### 3.8 One identity, at most one body — the binding registry

Addendum 12(d): *"we need to make sure those NPC's don't accidentally get duplicated across
zones."* §8.3 answers this for residents — model row primary, body a durable view bound by
`ResidentId`. It must now be answered the same way for everything else we mint, because §3.7 mints
porters, and a porter can freeze into a zone with the goods still on their back.

**One registry, realm-scope, answering both.** `KingdomBindingRegistry` lives on `KingdomSystem`
beside the realm seed — not on a settlement, because a bound body can be in another city's zone or
walked off the map entirely. One row per binding:

```
(BindingKey, Kind, ZoneId, ObjectRef, MintedTick)
     Kind = Resident   ->  BindingKey is a ResidentId
     Kind = Transient  ->  BindingKey is a JobId       (delivery, mend, message)
```

Bounded like everything else: ≤ 60 residents x 2 cities, plus **≤ 16 open jobs per realm**.
**A closed job is evicted at once, so absence from the registry *is* proof of closure** — there is
no second "closed" list to keep in step with the first.

**Check-before-mint, and it is the only path to a body:**

```
TryBind(key, kind, zone):
  registry.TryGet(key):
    hit, ObjectRef resolves live in THIS zone     ->  MOVE it. Do not mint.
    hit, resolves live in another RESIDENT zone   ->  resident: move across.  transient: refuse.
    hit, does not resolve (its zone is on disk)   ->  REFUSE THE MINT. The debt stays owed.
    miss                                          ->  mint, and write the binding in the SAME
                                                      copy-on-write publish as the debt decrement
```

The rule with the teeth: **an unresolvable binding is a refusal to mint, never a licence to mint.**
A frozen body is invisible; its binding is not, and the binding is what we consult. That single
line is the whole anti-duplication argument, and it holds across suspend, freeze, save, reload and
crash — because the registry is serialized with the system, and the mint and the binding are
published together or not at all (§1.3).

**The nasty case, walked through.** *The founder leaves mid-walk; the porter freezes into the zone
holding the goods; the model completes the job while they are away; the founder returns and thaws.*

| | What happens | Why it is safe |
|---|---|---|
| **t0** | Job `J` opens; body `B` is minted at the edge; binding `(J → B, Z)` written; the mint spends one reify unit; the **deposit** unit is still owed | one job, one body, one binding |
| **t1** | The founder leaves. `Z` suspends, then freezes; `B` goes to disk with the goods. The binding persists — it lives on the system, not in the zone | the registry outlives the ground it describes |
| **t2** | The model reaches `J`'s completion tick. `TryAdvance` closes `J`, **evicts the binding**, and re-attributes the outstanding deposit unit from the porter to the ordinary materialisation path (§3.2a) | the stores were credited at the dated tick either way (Addendum 11(b-ii)) — only the *rendering* changed, which is exactly what I2 permits |
| **t3** | The founder returns; `Z` thaws. At `ZoneThawedEvent`, **before intake and before any reify**, the **stale-transient sweep** runs: any object carrying a `KingdomJobId` with no open binding is despawned | this is the one instant the goods could exist twice — in the larder and in a frozen pack — and it is closed before anything can look |

**The sweep's licence, and its limits.** What it removes is `_stock` — items the simulation made
and may remove, vanilla's own protocol (§3.2a) — and numerically the same drams and servings the
model already delivered. That is **deduplication, not destruction of property.** Strictly:

- Anything on the body that is **not** `_stock`, or that answers `IsImportant()`, is **dropped to
  the cell first and never destroyed.** The protection law is not bent for our convenience.
- The sweep is licensed for **transients only.** A transient is a *rendering of a job*, and jobs
  close; a resident is a *person*, and §8.3's *materialisation may never remove a body* stands
  untouched — a resident whose row went `Dead` or `Abroad` keeps their body and everything the
  player did to it, and reads back at check-in.
- One ledger line when it fires, in register: *the load you left on the road reached the store by
  another hand.*

**Asserted directly in `kingdom:selftest`, not inferred:** *no `BindingKey` ever resolves to two
living bodies, in any zone, at any time* (I3).

### 3.9 Deficits drain real containers — reconciliation runs both ways

Addendum 12(d): *"where water is deficit, the storage it's taken from is updated accordingly, not
just in a ledger."* This is the half of reconciliation §3.1 did not cover, and stating it plainly
closes a real gap:

- **Check-in is ground → model** (§3.1). The ground wins for anything physical; differences are
  attributed and told.
- **Reify is model → ground** (§3.5). The model's *consumption* is applied to real containers, at
  container level, out of the same per-turn budget.

**So the counter is signed.** A catch-up unit is either `+land` — crops into the larder, water into
the cistern — or `−draw`: a season's drinking taken out of the vessels it was actually drunk from.
Same budget, same visible-first ordering, same invariant. Food landing during reify records only
what physically reached a named larder; deferred/failed portions remain debt and are never counted
again as harvest or harvest loss.
The founder who opens the cistern after a season finds **exactly the model's remainder**, and the
larder holds **exactly the crops nobody ate**.

**Cross-zone draws land where the vessel is.** Upkeep is a city-level draw against city-level
stocks (§1.2a), but a dram is drunk out of a particular urn. A draw apportioned to a zone the
founder is not standing in becomes a **negative unit on that zone's counter**, landing on that
zone's real containers the next time it renders — the cross-zone harvest delivery, run backwards.
Nothing new, and no unloaded zone is ever touched.

**The drain order, chosen and justified: oldest dedication first.** The city drinks from the vessel
it was given first; the founder's newest dedication is the reserve that outlives everything else.

- **Deterministic without a draw, and stable under reload**, because dedication order is a *stored
  fact* rather than a ranking recomputed from contents. "Smallest first" is *not* stable — the
  smallest *remaining* vessel changes as the drain proceeds, so a reload resuming from a slightly
  different intermediate state can pick a different urn. Ranking by *capacity* is stable but ties
  constantly (ten identical urns), and its tiebreak would have to be dedication order anyway. So
  dedication order is the primitive, and the only one needed.
- **Legible.** A player can plan around *the oldest cask goes first*; nobody can plan around an
  order that depends on the arithmetic of the last upkeep.
- **It makes the camp bootstrap behave.** Addendum 12(a-ii)'s pour-and-leave buffer drains the
  camp's own kit before it touches a waterskin the player dedicated afterwards.

Every drain goes through `KingdomLiquids.Drain` — measure the delta, never trust the return value
(STANDARDS §1) — and respects `IsFreshWater`, so a drain can never launder brine into the books.

**The audit invariant, in both directions.** After any attended pass of a fully-visited city with
the counter at zero, **model total == ground total, per stock kind**; mid-catch-up, the general
form is I1. A mismatch is attributed to a cause and told (§3.1 step 4) — **never silently
repaired**, and never treated as a fault. That is `RecordZone`'s shipped discipline, now running in
both directions instead of one.

### 3.10 Logistics — central batch planning, not agent AI

Addendum 12(f): *"a building should try to fetch stored resources from whatever building is holding
it closest to them, and citizens should path 'optimally' for pick up and delivery, something i know
rimworld struggles with."*

**The structural answer, stated first, because it is the whole reason this works.** RimWorld's
hauling pathologies — the pawn that walks the length of the map past a nearer stack, the two
half-empty trips, the zigzag — are not tuning failures. They are the *consequence of the
architecture*: each pawn decides, per tick, with local knowledge, in a world that is changing while
it decides. **We have the opposite architecture and get the opposite property for free.** Jobs are
planned **at reckon, over a frozen snapshot, with global knowledge**, and committed as itineraries
(§3.7). No carrier ever decides anything. **The pathologies are not mitigated here; they are
unrepresentable**, because there is no per-pawn decision to make them in.

**(1) Nearest-holder sourcing.** An input job binds to the closest container **actually holding**
the resource — not the nearest container of the right kind — by real path distance over the
claimed-zone graph. The model knows which container holds what, because §3.9's dedication-ordered
container index is exactly that fact. Ties break on **lower `WorkId`**: stored, stable, no draw.

**(2) The two-level distance matrix, and what we deliberately do not store.**

```
Dist(a, b) = IntraZone(a -> exitEdge) + Σ EdgeCrossing + IntraZone(entryEdge -> b)
```

- **Level 1 — the zone graph.** Nodes are claimed zones, edges are adjacency (orthogonal in the
  same stratum, plus the stratum above and below). ≤ 9 nodes. All-pairs by Floyd–Warshall is
  9³ = **729 integer ops**, and the table is ≤ 81 entries.
- **Level 2 — within a zone.** Work→edge lengths (≤ 6 edges), and same-zone work pairs.

**We never store `works²`.** We store work→edge (≤ 540 entries) plus same-zone pairs (≤ 900) plus
the ≤ 81-entry zone all-pairs table, and **compose any cross-zone distance in O(1)** from them.
That is what "two-level" buys: the `O(works²) ≤ ~1600` figure the addendum quotes is the bound we
stay *under*, and this decomposition is how — ≈ 1,521 `ushort` entries, **≈ 3.0 KiB per city**
(§0.0(c)), at nine zones as at four.

**Invalidation is by structure, never by time or by stock.** A dirty flag per zone, set only on
work placement, work removal, or a road change; the zone's slice (≤ 100 entries) is recomputed the
next time that zone renders. **Never at reckon** — recomputing needs the ground, and reckon may not
touch it (§0.0(d)).

**(3) Roads discount the metric.** A leg following a laid road is scaled by
`KingdomCityRules.RoadDiscountPercent` — a named rules constant, proposed at **60** (a paved leg
costs 0.6 of the same distance unpaved) — applied identically to the estimate and to the measured
length, so a road cannot make the two disagree. The consequence the player actually sees is the
point: **laying a road visibly shortens every itinerary that uses it.** Porters arrive sooner, more
jobs fit into one trip, and the works board's *waiting on* figures fall. `KingdomRoads` stops being
decoration and becomes logistics infrastructure, which is what the addendum asks for.

**(4) Capacity-bound batching.** Per slice (§3.6), over the open jobs:

- group by carrier capacity and route overlap;
- construct by **nearest-neighbour** seeded from the lowest `JobId`;
- improve by **2-opt**, in a fixed scan order, to a hard iteration cap.

Bounds, all of them constants: **≤ 16 jobs considered, ≤ 8 stops a trip, ≤ 50 swap tests** —
≲ 1,000 integer ops a slice, inside the slice budget (§0.0). **Deterministic, with no draw
anywhere in the planner**: routing is arithmetic, not chance. Draws remain only for flavour — which
settler carries, and what they are called — on `taf:stream:delivery` (§2.4).

**The bar is "never looks stupid", and it is asserted rather than hoped for.** Two checks, both in
`kingdom:selftest` and both in Pass 32 step 90j:

1. **No carrier crosses the city past a nearer holder.** For every completed fetch, no container
   holding that resource had a strictly smaller `Dist` at plan time.
2. **No two half-empty trips where one would do.** No two trips planned in the same slice share a
   route prefix while both run under capacity.

### 3.11 Networks — pipes, conduits, and the flow solve

Addendum 12(g): *"can we simulate networks of water, electricity, other liquids to enable buildings
to work over multiple tiles and have containers have actual proper numbers for the water or
resource they are holding?"* Yes — and it is the same two-layer pattern as everything else in §3,
which is the reason it is affordable at all.

**Attended: ride the vanilla transmission family, unchanged.** The engine ships an abstract
`IPowerTransmission : IPoweredPart` with five concrete families —
`ElectricalPowerTransmission`, `HydraulicPowerTransmission`, `MechanicalPowerTransmission`,
`BiomechanicalPowerTransmission`, `GenericPowerTransmission`
(`D/XRL/World/Parts/IPowerTransmission.cs:12` and siblings). It already does the hard part:

- **Network discovery is a cardinal-only BFS over cells**, collecting `Producers`, `Consumers` and
  a `GridCapacity` as it goes (`:1121-1195`, `Cell.DirectionListCardinalOnly` at `:1190-1193`).
- **Charge walks the network by event**: `ChargeAvailableEvent` and `FinishChargeAvailableEvent`
  dispatch into `Process(E)` (`:383-393`); demand is gathered by `QueryChargeEvent` /
  `TestChargeEvent`, each carrying a `GridMask` that is OR-ed with the part's `GridBit` before
  recursing (`:322-341`) — the engine's own re-entrancy guard, which is what makes a **cyclic**
  network terminate. We do not need to reinvent any of that, and per Addendum 11(c) we must not.

**The one engine fact that decides the model layer.** The flood-fill walks with
`GetLocalCellFromDirection`, which is `GetCellFromDirectionGlobal(..., bLocalOnly: true, ...)`
(`D/XRL/World/Cell.cs:8051-8054`). **A vanilla network cannot cross a zone boundary.** So for a
city that spans zones, the model graph is not an optimisation of vanilla's network — it is the only
way a multi-zone network exists at all. Vanilla renders the part of the network the founder is
standing in; the model owns the whole of it.

**The liquid carrier, as W7 shipped it — the LIQUID LAW made of parts.** Connection is DECLARED,
never inferred, and that law is four pieces and one refusal:

- **Typed mains.** `r_KingdomLiquidConduit` carries a `Liquid` (a vanilla liquid id) and a `Joins`
  face mask. Two segments meet only when **both** declare toward each other *and* agree on the
  liquid; an untyped line joins nothing, including another untyped line, because a blank
  declaration is not a declaration. A misspelt mask joins **nothing** rather than everything — the
  dangerous default is the permissive one, and a silent merge is the single thing the law forbids
  outright.
- **The crossing piece.** `r_KingdomLiquidCrossover` pairs opposite faces — north to south, east to
  west — and pairs nothing else. It carries **no liquid of its own**, deliberately: a piece that
  typed anything could be the place two liquids met, and this one holds no declaration to disagree
  with. Half a crossing is a dead end, not a corner.
- **The tap.** `r_KingdomLiquidTap` is the declared join between a main and a vessel, so the
  founder's act of *tapping* a cistern is what puts it on the line and standing near one is not.
- **The refusal.** A cross-liquid join returns `RefusedLiquid` and is told **by name** — *"the water
  line will not join the salt line… lay a crossover if they are meant to pass"* — once per piece,
  and once per composition on the founder's own register (7b: a sentence they will see, not twenty
  identical ones). Mixtures remain a future **mixing work** consuming typed lines and emitting a
  mixture-typed line; nothing here mixes anything.

**The one verb, and it runs downhill.** `KingdomFlowRules.TryChooseDownhill` moves the amount that
levels the two ends' *fill fractions*, solved rather than stepped —
`m = (C_t·L_f − C_f·L_t) / (C_f + C_t)` — bounded by the line's own bottleneck over the span, ends
chosen by cross-multiplication so no division rounds a choice. A founder watching a main run
between two cisterns sees them come level and stop, which is what a main does; it cannot overshoot
into an inverted pair, and it has no draw in it anywhere.

**And it is a carry, so I1 holds by construction.** `KingdomNetworkRules.TryPostTransfer` lowers the
giving row's level *and* its debt by the same amount and raises the taking row's by the same, so
`level − owed` — the ground — is untouched on both, the city's totals of both are unchanged, and
§3.5's amortised reify is what later opens the real vessels in `KingdomDrainRules`' dedication
order. Nothing is poured at reckon.

**The arcology decision, and it is a schema shape rather than machinery.** An edge names its two
endpoints and **nothing about who provided it** — no conduit id, no cell, no object. That is
deliberate: the backlog's arcology spine (*"riser taps on every floor — interior network segments
join the spine's 12(g) graph edges for free"*) needs a network whose edges a **building** declares,
and because provenance is absent from the row a shell can declare edges between its floors' nodes
with no schema change and no second edge kind. Removal needs no provenance either — it bumps the
topology stamp and the graph is rebuilt from the ground. **No hosted plots are built here**, and
nothing in this wave should be read as having started them; the fact recorded is the negative one,
pinned by a test that an edge carries no reference field.

**Liquid piping is fill-in, and this is on the record.** `HydraulicPowerTransmission` pipes carry
**joules, not supply**; their only liquid motion is `MingleLiquids` → `MingleAdjacent`, which
equalises with *directly adjacent* volumes and routes nothing. `LiquidPump` exists as a class but
**every carrier blueprint that would use it is commented out of `Furniture.xml`** — it ships with
no live user, and its directional fields are untested by any shipped content
(`VANILLA-PRODUCTION-TRUTH.md` §0, §8). So liquid piping is tier 3 of Addendum 11(c) — *fill in, in
vanilla's idiom* — and `LiquidPump` is a **wrap with a warning**, not a free hook.

**Model: one graph row per network.**

```
Network = (NetworkId, Kind, TopologyStamp, Nodes[<= 32], Edges[<= 48])
Node    = (WorkId, Role: Source | Sink | Store, Capacity, Rate)          16 B
Edge    = (NodeA, NodeB, ConduitCapacity, Condition)                     16 B
Kind    = Electrical | Hydraulic | Mechanical | Biomechanical | Liquid(liquidId)
```

**Stocks key by `(NetworkId, LiquidId)`** — 12(g)'s explicit ask, and it is forced by the engine:
`LiquidVolume` is liquid-agnostic, so a cistern on the fresh main and a cistern on a brine main are
different stock rows despite being the same part. Keying by network alone would let brine into the
city's water figure, which STANDARDS §1 already forbids by another route.

**Topology changes only on placement**, never on time and never on stock — the identical cache
discipline the distance matrix keeps (§3.10). `TopologyStamp` is bumped on a conduit or node being
placed, removed or destroyed; the graph is rebuilt from the ground the next time that zone renders.
**Never at reckon**, because rebuilding needs the ground and reckon may not touch it (§0.0(d)).

**The solve: closed-form flow conservation, netted per network.** Between two breakpoints every
rate is constant (§2.3), so a network's behaviour over an interval is arithmetic:

```
surplus = Σ source rates (each scaled by its own condition, Addendum 10(b))
        − Σ sink demands
surplus >= 0 -> stores charge, capped by headroom over the interval
surplus <  0 -> stores discharge; when they empty, BROWNOUT
```

**Throughput uses the bottleneck relaxation, deliberately not max-flow.** We take the minimum edge
capacity along the traversal tree to each node — O(nodes + edges) ≤ 80 per network. Player-laid
conduit is essentially a tree; a true max-flow is O(V·E²), buys nothing a player can perceive, and
the relaxation is **conservative** — it can understate throughput, never overstate it, so it can
never manufacture supply. That is the right direction for an error to point. It is also *vanilla's
own* answer: `FindGrid` reduces `GridCapacity` to the weakest link on the grid
(`D/XRL/World/Parts/IPowerTransmission.cs:1172-1175`) and hands that one figure to every member
(`:1201-1210`). We are narrower than vanilla — per path rather than per grid — and never wider.

**W7 correction: one traversal, not one per source, and it is precomputed.** The line above used to
read *"for each source"*, which is not `O(nodes + edges)` at all — it is that per source, and it
also needs an adjacency index the table did not budget. What ships instead: the traversal is seeded
from **every source *and every store*** at once (a store that holds something feeds the line exactly
as a wheel does, which is the whole point of a bed of molten salt on a night with no wind), its
**order is computed when the topology is laid** — off the ground, `O(nodes × edges)` on a placement
and never at reckon — and the solve is one linear pass over that stored order. Non-tree edges never
contribute, which is conservative in the same direction. The order and the parent edge cost two
bytes a node and §0.0(c) carries that edit as its sixth correction.

**Cost, bounded by the same argument §2.3 uses.** A network is re-solved only when one of *its*
breakpoints falls — a source crossing a wear threshold, a store filling or emptying, a topology
stamp changing — and every breakpoint consumes at least one structural change. So across a whole
reckoning the re-solves are bounded by `B`, not by `B × networks`: **≤ 64 × 80 = 5,120 node-visits
per city**, on top of §0.0(a)'s row-visits. Trivial, and it does not scale with the elapsed.

**Deficit is a brownout *event*, not a silent zero.** It is a happening (§4.1): dated, told once,
and surfaceable by the heartbeat within the in-game hour (§3.6), so *"the mill went quiet on the
sixth of Ut"* reaches a founder standing three zones away. Works stop in a **stated deterministic
priority order**, lowest first:

> **industry → refining → amenity → food → water → defence and watch**

with ties broken by **higher `WorkId` stopping first**. That order is not invented here: it is the
mod's existing *stop at the loyal core* discipline (the thirst ladder's "empty casks and one rung of
the ladder, never an empty town", DECISIONS' *"failure has a floor"*), applied to charge instead of
drams. A city gives up what it is *doing* before it gives up what it *is*.

**Where lodging sits, stated because it is the question the order is judged on.** Lodging is
**amenity** — the middle rung, not the top and not the bottom. A roof needs no charge to keep the
rain off; what a dwelling draws power for is comfort, and whether a household keeps its home is the
roof brink's question (`KingdomBrinkRules`, `KingdomLodgingRules`) and not the grid's. Putting
lodging *last* would let a brownout condemn a home, which belongs to one system and would then
belong to two; putting it *first* would say a settlement stops housing people before it stops
smelting, which is the opposite of everything else the mod says about a city. Food, water and the
watch are last, in that order, because a dark hungry city recovers and a city whose watch went dark
on the night raiders came does not.

**W7 correction to the tie-break's *justification*, not to the tie-break.** The rule stands —
higher `WorkId` first — but the claim that it means *"the newest-built work goes quiet before the
oldest"* does not: a `WorkId` is `KingdomCityRules.StableId(work.ID)`, a written-out hash of the
engine's object id, chosen so that an id survives a restart. It is stable, stored, reload-proof and
needs no draw, and among two works of the same tier it is **arbitrary** — which is honest, because a
tie means the city has no principled reason to prefer either. When a work row later carries a raised
tick (the heart/relocation lane will want one), the tie-break becomes literal build order with no
change to the ladder and no change to any caller.

**Containers hold true numbers, both directions.** This is §3.9 applied to networks, and the
handoff is stated in both directions because both are needed:

- **Ground → model, at check-in** (§3.1): read each node's live part — `LiquidVolume` volume *per
  liquid*, `Capacitor` charge — and seed the network's stock rows. The ground wins.
- **Model → ground, at reify** (§3.5, §3.9): the model's allocation lands on the real parts as
  ordinary signed counter units, through delta-measuring adapters — `KingdomLiquids.Fill`/`Drain`
  for liquids and its equivalent for charge. **Never a raw vanilla call whose return value is
  trusted** (STANDARDS §1).

The shipped precedent for the charge half is already in the mod: `KingdomPowerRules` measures
everything in the charging post's cradle unit (4,000), and `KingdomPower`'s own comment about
needing *"a fence between a windmill and a charging post to get anywhere — a wiring puzzle"* is
exactly the problem a network graph dissolves.

**W7: the power lane migrated onto the solve, and the migration is what "one accounting" means.**
Before it, `KingdomPower` counted its own days per work off `ElapsedDays` and a remainder-keeping
checkpoint, summed its own charge, applied its own store clamp and ran its own delivery — a second
accounting standing beside the model's, which is the thing W6 made *unrepresentable* for production.
Three things changed and nothing else did:

1. **One clock.** Days are world-day boundaries through `KingdomProductionRules.TryDaysBetween`, so
   `Days(a,b) + Days(b,c) == Days(a,c)`: a founder who walks in twice in one day is not paid twice
   and a horizon falling mid-day does not drop the remainder. *This is a legitimate ladder change*
   — the old count was `elapsed / TicksPerDay` with the remainder carried on the part — and it is
   the reason the power lane can now be split by a breakpoint at all.
2. **One span.** The network's resolved-through tick is the oldest planted stamp among its nodes;
   a node with no stamp is planted at now and credited nothing, so a work still never pays out for
   the day it was raised, and every stamp leaves the pass equal so the span cannot fray.
3. **One netting.** `KingdomFlowRules.TrySolve` does the summing, the store clamp, the deficit and
   the stop list. `KingdomPowerRules` keeps the *rates* — `RatedChargePerDay`, `DailyOutput`, the
   two availability curves — and its `ChargeForDays` / `Absorbable` / `Releasable` are now the
   **named forms of what the solve produces**, asserted equal in test rather than separately
   computed. No power-rules test needed re-pinning: the arithmetic did not move, only its one
   caller did.

The proof is an identity rather than a promise: `Generated + Discharged == Delivered + Charged +
Spilled`, asserted in every branch, so there is no fourth destination for a charge and nothing
arrives from a fifth source.

## 4. Events with meaning

### 4.1 What a happening is

A happening is **derived**, never authored ad hoc:

```
Happening = (Kind, Tick, SubjectIds, PlaceZoneId/WorkId, Outcome)
```

generated inside `TryAdvance` from model state plus kernel draws on the `taf:stream:happening`
lane. Every kind binds to machinery that already exists, so the happenings layer is a *generator
and a budget*, not a second simulation:

| Kind | Generated when | Rides |
|---|---|---|
| **Wedding** | two residents' cohabitation closeness crosses a band | `KingdomLodgingRules.Closeness`, `KingdomConversionRules` (cohabitation-days), `KingdomCeremony` |
| **Funeral** | a resident's `Standing` becomes `Dead` | the `DeadNames`/`DeadOrigins`/`DeadCauses`/`MemorialsRaised` roll that already exists |
| **Feast** | the festival clock, or the founder calling one | `KingdomLarder.HoldSharedMeal` → `KingdomCreed.EaseForMeal` + `KingdomConversion.OnSharedMeal` |
| **Festival** | a creed's own calendar | the faction-level `<waterritual Recipe= RecipeText= RecipeGenotype=/>` — vanilla's *actual* favourite-dish vocabulary, eight of them in `Factions.xml`, and exactly what Addendum 11(b) asked for. A creed's festival wants its dish; the larder holds it or it does not, and the second is a grievance. |
| **Quarrel** | creed pressure / resented conversion crossing its band | `KingdomConversion.Convert` — the **one** path a conversion may take — and the `Creed` brink |
| **Delivery** | a haulage clock between two zones or two cities | `KingdomManifest`, `KingdomTrade`, and the carry-sign's distance-scaled hauls |
| **Breakdown** | condition crossing a typed threshold | `KingdomWear` + Addendum 10(b): stores leak, power works lose output, in the work's own kind |
| **Raising** | a scaffold/plot completing on world-time | `KingdomCeremony.OnBuildingRaised` — which already tells itself two ways, attended (the crew gathers, water is shared, the chronicle names who was there) and unattended (the homecoming tells it) |

The rule that keeps this from sprawling: **a happening kind may not own state.** It reads the
model, draws, and writes an outcome through an existing system's one true path. If a kind needs a
new field, that field belongs to the system that owns the concept, not to the happenings layer.

The physical rendering does need **temporary authority**, which is not domain state. One bounded
`HappeningModel` operation freezes an exact authored fixture and up to four already-bound named
resident bodies, their reachable cells and their former post/home/AI state. It advances through
Prepared → Walking → Holding → Ready → Restoring, with monotonic sink receipts, and survives a
save or an interrupted callback. Bodies move only through vanilla `MoveTo`; the runtime never
mints, substitutes, clones, summons, or teleports an attendee. Ready is the durable evidence that
the exact bodies arrived and the functional fixture accepted both its real vanilla-part action and
its use receipt. The operation then publishes through the existing owner and restores each body to
its exact prior cell and schedule before acknowledging restoration. Clearing retains only bounded,
canonical delivery tombstones for once-only weddings, funerals, and construction raisings. They
carry no prose, outcome, body, fixture, or second domain history; they exist because the 32-line
told ring is not permanent authority.

### 4.2 The budget

A season away can generate a hundred happenings. The register holds 200 entries total. So the
**telling** is budgeted, using the shape the slide already ships and generalising it into one
shared `KingdomTellingBudget`:

- the first few by name, the last by name, one summary line for everybody in between
  (`NamedDeparturesPerSlide` / `SlideDepartureSummary` is the template, and
  `NamedRuinsPerBreakpoint` / `RuinSummary` is the second instance of the same pattern);
- per-lane caps that sum to the ledger's own ceilings (≤12 notes, ≤8 brink lines);
- a hard chronicle budget per reckoning, so a City→Camp collapse plus a season of weddings cannot
  eat the book — the existing `ChronicleEntriesFor` already holds the line against this for one
  lane and should hold it for all of them;
- the told-log ring keeps the rest for the outsider register and the guestbook, where a line that
  did not make the chronicle can still be *heard about*.

**Generation is not telling.** The model may know about a hundred happenings; the founder is told
about a dozen. Everything else is in the ring, in the counters, and in the state that changed.

### 4.3 Surfacing

- **Physical attendance** — while the founder stands on coherent owned ground, wedding, funeral,
  feast, and raising surfaces wait for exact named bodies to reach a functional authored locus.
  Only the Ready receipt may call the existing semantic owner. Losing the founder, a body, the
  path, or the fixture before Ready restores the temporary posts and degrades to a dated report;
  it never replaces a person or stages a UI-only ceremony. A wholly unattended occasion starts as
  that dated report and acquires no body/fixture receipts.
- **Ledger digest** (pull) — the homecoming report, brink lane first. Unchanged shape.
- **`KingdomWord`** (push) — brinks and irreversibles, wherever the founder is standing, framed by
  whether they are in the city the news is about. Unchanged contract.
- **Chronicle** — what the book should hold, with the disputed two-register recording for anything
  an outsider would tell differently.
- **`KingdomReports`** — status gains the model's flows ("the fields make 7 a day; the settlement
  eats 9"), which is the single most useful thing a living city can say and which today it cannot.

---

## 5. Engagement — what the player does that they cannot now

**Constraint first: the Charter is full** (32 options, every hotkey taken). Everything below
deepens an existing surface. No new top-level entry, no parallel system.

| Existing surface | What it becomes |
|---|---|
| **"Your works, and what they become"** (`y`) | The **works board**: every work in the *city*, not the zone — its run-state, crew, output per day, condition, and what it is waiting on. This is where the simulation becomes legible. Without it the model is invisible and the whole wave is worthless. |
| **"Set the crew on the ground"** (`x`) | The **labour dial**, city-wide. Fields vs yards vs watch vs haulage. The single most meaningful lever a living city offers, and the one the mod's own crew machinery (`KingdomCrews`, `CrewNeeds`, ablest-first assignment) was built for. |
| **"Standing policy"** (`p`) | Gains the **day shape**: market days, the festival calendar, whether the watch stands at night. Model-only, cheap, and it changes what the founder *sees* when they walk in — which is the point. |
| **Petitions** (`h`) | Petitions issue **from model state**: the granary is full and nothing hauls it; the shrine has no keeper; the road is unpaved. Today petitions are decorative; against a model they are the city talking. |
| **Bounties** (`1`) | The city posts what it **cannot do itself**: haul 200 drams to the second city, mend the reservoir, bring seed for a new crop. The founder becomes a contractor to their own city — the strongest engagement available, needing no new surface at all. |
| **Ceremonies** | Occasions come from happenings (raising, wedding, funeral, festival), and **attending is worth more than not** — the raising already distinguishes attended from unattended, and every other occasion should. This is what makes being present a choice rather than a chore. |
| **Guests / guestbook** (`j`, `o`) | Guests arrive **because** of what the city did: a festival draws pilgrims, a breakdown draws a tinker, a wedding draws kin from a named origin. The hook-decays-into-rumour machinery already exists. |
| **The rite, the shrine, the creed** | Festivals give the faith machinery a calendar instead of only a lever. A creed's favourite dish, held or missed, is the cheapest meaningful stake in the mod. |

**The new thing, in one sentence:** the founder can leave on purpose, come back to a city that is
measurably different, walk in at Jeweled Dusk and find the market shut and the hearths full, read
what happened and who it happened to, and act on a list of things the city itself is asking for.
None of that is possible today.

---

## 6. Vanilla proximity and the cost model

### 6.1 Where we ride vanilla

| Concern | Vanilla surface |
|---|---|
| System lifecycle | `IGameSystem`, `Register(XRLGame, IEventRegistrar)`, named-field `IComposite` serialization |
| Pass trigger | `ZoneActivatedEvent` (already), `SuspendingEvent` (new — the true last read), `ZoneDeactivatedEvent` (hint), `ZoneThawedEvent.TicksFrozen` (cross-check) |
| Absence catch-up | `ZoneRepair`'s accumulate-and-apply-N-units (`BuildCounter / TurnsPerObject`) and `GenericInventoryRestocker`'s stamp-and-compare — both engine-authored. §3.5 keeps the counter and amortises the spend |
| Per-turn **pump** (never a clock) | game-level `EndTurnEvent.Send(game)` — one dispatch a turn, no zone broadcast (§0.0(e)) |
| Holding a zone **resident but not ticked** | `GetZone` (thaw) + `Zone.Suspended` as saved + `MarkActive()` each turn — all vanilla API, bounded to one zone and self-releasing (§6.4) |
| Carrier movement | `Brain.PushGoal(MoveTo)` + `DelegateGoal`, the construction `Bed` already uses; Speed 100 = one cell per tick (§3.7) |
| Item ownership | `_stock` / `norestock` / `IsImportant()`, vanilla's own protection protocol |
| Daily life | `IdleQueryEvent` + `Brain.Stay` / `StartingCell` + the `Bored` goal — the entire vanilla surface, and the same one `Bed` uses |
| Absence idiom | `Temporary`'s tick-stamp catch-up — the engine's own answer, ratified |
| Stocks | real `LiquidVolume`, real `Container`/`Inventory`, via `KingdomLiquids`' measure-the-delta adapters |
| Visible production | `LiquidProducer`, `Mill`, `ItemConvertor`, `FoodProcessor`, `SolarArray`, `Capacitor`, and the real power grid (`IPowerTransmission` cardinal flood-fill) |
| Time-of-day | `Calendar.IsDay()`, `CurrentDaySegment`, `GetTime(int)` — the game's own eight bands |
| People | `GameObject.Create`, `NameMaker`, `ConversationsAPI`, `PopulationManager` (`r_KingdomSettlers` table, mergeable) |
| Festival dishes | faction `<waterritual Recipe=…/>` |
| Surfaces | `Popup`, `MessageQueue`, `JournalAPI` accomplishments |

### 6.2 Where we greenfield, and why

Each of these is on VANILLA-PRODUCTION-TRUTH §8's proven-empty list, or is simply not a thing an
engine provides:

the city model and its rows; closed-form breakpoint advancement; the happenings generator and the
shared telling budget; the resident day-shape and placement; seeds and planting; multi-stage crop
growth; spoilage; inter-zone haulage; the reconciliation protocol. Nine things, all of them
arithmetic and bookkeeping, none of them fighting the engine.

### 6.3 The cost model

**Per zone activation — O(1) in elapsed time.**

| Step | Cost |
|---|---|
| survey | one zone walk — **already paid today**, unchanged |
| check-in reconcile | O(objects surveyed) on the *same* walk's results — no second walk |
| reckon | see below |
| materialise | **O(budget) — 8 units, flat**, whatever is owed (§3.5). This row used to be the design's worst spike; it is now its flattest line |

**Per reckoning — O(model), independent of days.**

For the worst case in the mandate — a 4-zone City, 60 residents, 40 works, one season (90 days)
away:

- breakpoint loop: ≤ 64 steps over `R` = 116 rows, propose and apply → **≤ 14,848 row-visits** of
  plain integer arithmetic. §0.0(a) counts it out in full and §0.0(f) gives the formula for a city
  of any size;
- fixed-period lanes: O(1) each via `TryCountFixedPeriodDue`, ~12 of them;
- draws: one per *happening*, not per day — a season of a busy city is tens, not thousands, of
  SHA-256 blocks;
- telling: capped at ~12 ledger notes, ≤8 brink lines, ~6 chronicle entries.

That is sub-millisecond, once, on the pass the founder walks in. **The number to hold ourselves
to: the reckoning cost of a season away must equal the reckoning cost of a day away, plus a bounded
constant.** If any future lane makes that false, it is the lane that is wrong.

**Memory and save size**: ~300 rows per realm, a few kilobytes, inside a block the save already
writes. Contrast the rejected alternative: every zone held live is written **inline into the save
file** (`ZoneManager.Save` serialises `CachedZones` in full), so three extra live zones is three
zones' worth of save bytes and save latency on every autosave. The model is cheaper than the
engine's own answer by two orders of magnitude.

**The free 40 turns.** The shadow window (§0.1) is a gift, not a problem: a founder who steps into
the next zone of their own city and back again finds the first zone still live and still ticking, so
short local movement inside a city needs no reckoning at all. The reckoning is for real absence.

**One micro-rule with teeth**: resolve `The.Game.GetSystem<KingdomSystem>()` **once per pass** into
a local. It is a linear scan over `XRLGame.Systems` with a `GetType()` call per element
(`D/XRL/XRLGame.cs:286-300`); called per row over 100 rows it is the only hot spot this design
has.

**What we do not do**: pin zones (cap `> 3`, clears the whole list on overflow); veto suspension;
hold any zone **live** (ticked); walk an unloaded zone's objects; run any per-turn *clock* over city
state. All five are forbidden by STANDARDS or refuted in §0.1. There is exactly one bounded
exception, and §6.4 prices it: holding **one** neighbour *resident and suspended* while it still
owes catch-up — which costs nothing per turn, because a suspended zone is not ticked.

---

### 6.4 Why not keep the city's zones live — and what we do instead

The author's question, asked directly and owed a numeric answer rather than a doctrinal one:
*would it be worth it, and possible computationally, to activate the zones that need
repair/catch-up for a whole city while you are inside the bounds of that city?*

**It is possible.** `SetCachedZone(Zone)` does exactly that in three lines — `MarkActive()`,
`ActivateObjects()`, `Suspended = false` (`D/XRL/World/ZoneManager.cs:1771-1776`) — and calling
`MarkActive()` every turn holds a zone against both suspension and freezing (`GetSuspendability`:
`currentTurn - LastActive <= 40`; `GetFreezability`: `<= 0` —
`D/XRL/World/Zone.cs:7451-7453, 7472-7483`).

**It is wrong, for four reasons, and the fourth is decisive:**

1. **Four times the per-turn cost.** `ProcessSingleTurn` ticks and broadcasts `EndTurnEvent` into
   every cached zone that is not suspended (`D/XRL/Core/ActionManager.cs:443-449`). A 4-zone City
   held live is four zone ticks and four broadcasts across 8,000 cells **every turn**, plus every
   settler back in the `ActionQueue` taking a real turn. At nine zones (§0.0(f)) it is nine.
2. **Every held zone inline in every save.** `ZoneManager.Save` writes all of `CachedZones` in full
   and uncompressed, suspended or not (`:468-475`); only *frozen* zones are ids. And a zone held by
   per-turn `MarkActive()` is not freezable, so even the force-freeze that save-and-quit runs
   before `SaveGame` (`D/XRL/Core/XRLCore.cs:1138-1139, 1740-1741`) cannot clear it.
3. **Pinning is not the way and never was** — cap `> 3`, checked lazily inside `GetSuspendability`,
   clearing the entire list on overflow (§0.1). A 4-zone City is exactly one over.
4. **A live-but-unattended zone does not run our machinery at all.** Every step of the settlement
   pass binds `ZoneActivatedEvent` — *activation*, not ticking. A zone held live by `SetCachedZone`
   is never activated, so it never reckons, never checks in, never materialises. It pays the full
   vanilla cost of being awake and advances **nothing** of the simulation the author is asking for.
   It buys idling NPCs in an empty room, at four times the price.

**What we do instead — three mechanisms, and only the middle one is new.**

**(1) Ride the 40-turn grace.** Free, and already true (§6.3): a founder moving between the zones of
their own city re-enters ground that never went to sleep.

**(2) Prefetch-thaw the neighbour, and spend its counter before the founder crosses.** The insight
that makes this cheap: **we never needed a zone to be *live*; we need it to be *resident*.** A
suspended-but-resident zone has its whole object graph in RAM — suspend serializes nothing and
drops nothing (§0.1) — so `KingdomSurvey` can read it and materialisation can write into it,
exactly as zone generation writes into a zone that has never been activated. And a suspended zone
is skipped outright by `ProcessSingleTurn` (the `!zone2.Suspended` guard, `:445`), so it costs
**zero per turn**.

All of the mechanism is vanilla API:

- `The.ZoneManager.GetZone(id)` thaws a frozen zone (`:2062-2097`). `Zone.Suspended` is a plain
  **serialized** field (`D/XRL/World/Zone.cs:199-204`), so a zone suspended before freezing comes
  back **suspended** — resident, and not ticked.
- `CheckCached` calls `SuspendZone` only on zones that are *not already* suspended (`:998-1009`),
  so nothing re-wakes it.
- `MarkActive()` each turn keeps `GetFreezability(0)` at `TooRecentlyActive`, so it is not written
  straight back to disk. One long assignment a turn (`D/XRL/World/Zone.cs:2304-2307`).
- The stale-transient sweep (§3.8) runs at its `ZoneThawedEvent` like any other thaw. **A prefetch
  is not a special path through the invariants, and may not become one.**

**Bounds, and they are strict:**

- **At most one prefetched zone**, beyond the seated one. Two resident city zones, never more.
- **Only a zone the founder could reach next**: an orthogonal neighbour in the same stratum, or the
  stratum directly above or below. The engine's topology gives at most six; we **consider two**
  (ranked by debt) and **hold one**. **This is O(neighbours), never O(city)** — a founder in a
  thirty-zone city pays exactly what a founder in a two-zone city pays (§0.0(f)).
- **Only while a debt stands.** *The hold lives exactly as long as the debt.* When the counter
  drains we stop calling `MarkActive()` and the zone freezes itself at the next `CheckCached`. A
  caught-up zone is never held — so a founder who settles in for a long stay ends up holding
  nothing, and the save-size exposure of reason 2 above is bounded to one zone for at most the ~29
  turns a full backlog takes to drain (§0.0(b)).
- **Skipped under load, never queued:** none on the turn a zone was thawed or activated (the engine
  has just run `ForceCollect()` → `MemoryHelper.GCCollectMax()`, `:829, 728-731`); none while
  `ProcessingZones` is non-empty; none when the seated zone has already saturated the reify budget;
  none at all after a thaw that blew its timing budget. A skipped prefetch costs the founder a
  normal vanilla thaw at the boundary — what they would have paid anyway.
- **One thaw is not free, and must be counted**: a SQLite read, a Brotli decompress, a `Zone.Load`
  and a forced full GC. Prefetch pays that cost *early*, on a turn the founder is walking, instead
  of *at* the crossing. It does not avoid it. That is the entire benefit and it should not be
  oversold.

> **The prefetch invariant.** *A prefetched zone the founder never enters is indistinguishable from
> one that was never prefetched* — the model stayed authoritative for the un-reified remainder, the
> counter persisted with it, and the zone froze back to disk carrying whatever landed. Prefetch may
> change **when** work is done, never **whether** or **how much**. Anything that would not also be
> true after a plain cold entry may not be done inside a prefetch.

**(3) The heartbeat** (§3.6) — which is what actually answers the want *underneath* the question.
The reason to activate a whole city was *so that what happens elsewhere is felt here*; the
micro-reckon delivers that **without loading a single extra zone.** The model advances every 50
ticks whether its zones are on disk or not, and the consequence arrives as a falling number in the
status report, a line in the register, or a porter at the door (§3.7).

**Status: prefetch is a spike, not a promise.** Every link above is verified in the decompile, but
the *combination* — write into a suspended-resident zone, hold it with `MarkActive`, let it freeze
on drain — is untested in play. W3 ships it behind the option gate with the receipt attached, and
it is the one thing in this design that is safe to cut: without it, a boundary crossing costs a
plain vanilla thaw and an entry that is already amortised to O(budget). The feature is
**smoothness, not correctness.**

### 6.5 The perf receipt — what is timed, where it lands, how it is read in play

Addendum 12(b)'s last clause: *measured, not assumed.* The receipt is the smallest thing that makes
§0.0's table falsifiable by a tester instead of by an author.

**What is timed.** Five timers, each started and stopped inside the existing `Guard(label, action)`
wrapper, so no call site changes and nothing is timed twice:

| Timer | Scope | Recorded |
|---|---|---|
| `reckon` | one city, one pass | ms, row-visits, breakpoint steps, draws, elapsed days, live `R` |
| `slice` | one micro-reckon (§3.6) | ms, steps, row-visits, whether anything surfaced |
| `reify` | one turn's budget spend | ms, units spent, split `+land`/`−draw`, units still owed, how many were visible-first |
| `thaw` | one prefetch | ms, zone id, or the reason it was skipped |
| `bytes` | model + registry, on the write path | bytes serialized, per city and per realm |

**Counters, not only milliseconds — a timing is hardware and a count is a contract.** A tester on a
slow machine can still prove that a 90-day reckoning did the same row-visits as a 1-day one, which
is the assertion §0.0(a) actually makes. And every count is checked against the **live** `R`, not
against a constant, so the assertions survive the zone cap moving (§0.0(f)).

**Where it lands.** `KingdomLog.Log` (`Core/KingdomLog.cs`), which already writes `[TAF]` lines to
Player.log behind the `r_TAF_OptionDevLog` option TESTING.md says is on by default in this build.
One greppable line per event, in the shape the log-watcher already reads:

```
[TAF] perf reckon city=Kavvat days=90 R=116 steps=41 rows=4756 draws=118 ms=1.4
[TAF] perf slice  city=Kavvat steps=1 rows=232 surfaced=delivery ms=0.09
[TAF] perf reify  zone=JoppaWorld.11.22.1.0.10 land=5 draw=3 visible=3 owed=92 ms=0.6
[TAF] perf thaw   zone=... ms=31.2 reason=prefetch
[TAF] perf BUDGET reify ms=2.4 over=2.0          <- a FAIL line, and it says so
```

A figure that crosses a §0.0 budget is prefixed `BUDGET` and names the budget it broke, so a
failure is legible without the tester holding the table in their head. Session worsts (worst
reckon, worst reify turn, peak owed, model bytes) append to `kingdom:dump`; `kingdom:selftest`
asserts the memory ceiling against the **measured** byte count rather than §0.0(c)'s estimate, and
asserts I3 directly.

**How it is read in play.** One new pass, in TESTING.md's own shape. **Step 90d is the canonical
acceptance test for the living city** — the author's porter scenario, named as such:

> **Pass 32 — What the city costs, and what it delivers**
>
> | Step | Action | Expect |
> |---|---|---|
> | 90 | Found a City, hold 4 zones, 40 works, 60 settlers. Leave for a season. Come home | One `perf reckon` line: `ms` under 2, `rows` under `64 x 2R`. Nothing stutters as you walk in |
> | 90a | Do the same, but leave for **one day** | `rows` and `draws` are **identical to 90's**. Only `days` differs. If they scale with the absence, a lane is drawing per day and it is the lane that is wrong |
> | 90b | Watch the turns after the homecoming | `perf reify` lines, one a turn, `land+draw` never above 8, `owed` falling monotonically to zero. What you can **see** fills in first; the rest arrives behind you as you walk |
> | 90c | Walk out while `owed` is still above zero, wander a week, come back | `owed` resumes at the number it left at. Nothing lost, nothing landed twice, no harvest counted twice (**I1**) |
> | 90d | **The porter.** Stand in your house in zone A while a farm in zone B finishes harvesting | A porter walks in at the edge, crosses to the larder beside you, puts the real crop items in it, and leaves. The homecoming report does **not** tell you about it afterwards (**I2**) |
> | 90d2 | **Follow the porter.** Do 90d, then walk out of the zone *behind* them, following | They exit by the correct edge cell; you come out beside them, and they are just inside the entry edge, a cell or two along — not at the far wall and not standing on the boundary. Walk faster and you catch them at the edge; dawdle and they are further on. No pop, no teleport (**I5**) |
> | 90d3 | Stand in the porter's way for ten turns, then let them past | They arrive ten turns late and everything after shifts by ten. No sprint to catch up. Block them indefinitely and the job **fails**, is named, and the cargo is where it fell |
> | 90e | Do 90d, then walk out mid-carry, wander until the model completes the delivery, and come back | The goods are in the larder **once**. The porter is gone. No second load anywhere, and the ledger says the load reached the store by another hand (**I3**) |
> | 90f | Stand in zone A and let a generator in zone B wear out | Within an in-game hour the status report's figure falls and the shortfall says itself once — **without you moving** |
> | 90g | Open the cistern and the larder after a season away | The cistern holds **exactly** the model's remainder and the larder **exactly** the uneaten crops — not a full vessel and a ledger note. Reload and repeat: the **same** vessel drained first (**I4**) |
> | 90h | Cross repeatedly between two zones of your own city | No reckoning at all inside the grace window; at most one `perf thaw reason=prefetch` line per crossing; never two zones held at once |
> | 90i | `kingdom:selftest` | Measured model bytes under the ceiling **and under the formula for the live `R`**, and no `BindingKey` with two living bodies. Each check names its figure rather than asserting a bare pass |
> | 90j | Hold the same resource in two stores, one near a workshop and one across the city, and let the workshop pull | The carrier goes to the **near** one. Every time, and after a reload. Then queue three small jobs along one route: **one** trip serves them, not three (**I6**) |
> | 90k | Lay a road along a carrier's route | The itinerary visibly shortens — the same delivery arrives sooner, and the works board's *waiting on* figures fall. Pull the road up and it lengthens again |

**What constitutes failure.** A `BUDGET` line in a playtest log is a bug report, not a note. Any
reckon over 8 ms per realm pass; any turn's reify over 2 ms or over budget; a slice over 0.5 ms or
4 steps; measured bytes over 40 KiB in RAM or 64 KiB written; a counter that never reaches zero; or
a `rows`/`draws` figure that differs between a 1-day and a 90-day reckoning of the same model. Each
is a row in §0.0's table, and each is **counted, not judged**.

### 6.6 The model as a public extension API

The author: *"we should also try to API this so other mods can extend the model if they don't want
to contribute directly to the mod."* The architecture makes this nearly free, and it is worth
saying why before saying how: **the model is rows plus pure rules plus one executor. An extension
is more rows and more pure functions under the same contract.** There is no place in §2 or §3 where
the model asks whether a row is ours.

**The data lane already stands and is the precedent.** Buildings, deals, yard works and population
tables are XML merged by key — a mod can add a building today without a line of C# and without a
fork. §6.6 extends that lane to *behaviour*, in the same spirit: additive, keyed, and never
requiring the extender to touch our source.

**The contract set — five extension points, one per model dimension:**

| Contract | Extends | Shape |
|---|---|---|
| `IResourceKind` | stocks (§1.2a) | a new civic good: unit, container predicate, `(network, liquid)` key if it flows |
| `IJobKind` / `ICarrierKind` | transients (§3.7) | a job's legs, cargo and completion; a carrier's blueprint and `WalkTicksPerCell` |
| `INetworkKind` | networks (§3.11) | node roles, edge capacity semantics, the brownout priority tier |
| `IHappeningGenerator` | happenings (§4.1) | reads a frozen snapshot, returns dated happenings; may not own state (§4.1's own rule) |
| `IWorkBehaviour` | work rows (§1.2c) | run-state advance between breakpoints, and what the work owes materialisation |

**Discovery rides the engine's own idiom, not an invention of ours.** `ModManager` ships a cached
attribute scan over every active assembly — `GetTypesWithAttribute`, `GetMethodsWithAttribute`,
`GetInstancesWithAttribute<T>` (`D/XRL/ModManager.cs:1186-1216`) — and the engine's *own* public
extension points use exactly the marker-attribute-plus-interface pattern we want:
`ModManager.GetInstancesWithAttribute<IWorldBuilderExtension>(typeof(WorldBuilderExtension))`
(`D/XRL/World/WorldFactory.cs:108`; the Joppa builder does the same at
`D/XRL/World/WorldBuilders/JoppaWorldBuilder.cs:3655`), as do wishes
(`D/XRL/Wish/WishManager.cs:43`), debug commands and conversation delegates. So:

```
[KingdomExtension]                     // marker attribute, in the contract namespace
public sealed class MyHappenings : IHappeningGenerator { ... }
```

and one call at registration collects every one of them. **A third-party mod needs no hard
reference beyond the contract namespace** — no dependency on our systems, our carrier, or our
version of anything else. The registration cache is marked `[ModSensitiveStaticCache]` so a mod
list change resets it the way `ModManager.ResetModSensitiveStaticCaches` already resets the
engine's own (`D/XRL/ModManager.cs:340-355`).

**Extensions live under the same invariants, enforced rather than trusted.** This is the part that
makes the API safe to publish at all, and every clause of it already exists for our own code:

1. **Kernel draws through our API only** — `CounterRandom` with a `SemanticEventKey` on the
   extension's own stream (§2.4). `System.Random` in an extension is a contract violation, caught
   by the same reflection test as §2.5's purity check.
2. **Frozen snapshot in, frozen result out**, through `KingdomExecutor.Submit` (§2.5). An extension
   cannot reach the ground, the clock, or another extension's rows.
3. **Budget, timeout and error isolation per job** — the seam's, not the extension's. **A broken
   extension stalls its own job and nothing else**: no city state is published on fault, the turn
   is unaffected, and the failure is logged by mod name.
4. **Telling goes through our surfaces** — ledger, `KingdomWord`, chronicle, guestbook — under
   §4.2's shared budget. An extension cannot flood the register any more than we can.
5. **Rows are capped like ours.** An extension's rows count against §0.0(c)'s ceiling, and the
   receipt reports them by mod name, so a memory regression has an owner.

**Versioned, and refusing loudly.** `KingdomApiVersion` is checked at registration. On drift the
extension is **refused by mod name, on screen and in the log**, with the version it wanted and the
version we are — never silently skipped and never half-loaded. A silently-inactive extension is
worse than a refused one, because the player attributes the missing behaviour to us.

**When it opens: the contracts are authored from W1 and published at W5, not before.** They firm up
as the dimensions land — resources and works at W1, jobs and carriers at W3, happenings at W4 — but
they stay internal until W5. The reason is specific rather than cautious: **a contract with exactly
one implementation is indistinguishable from that implementation's accidents.** W6 (production
depth, and the logistics planner) and W7 (networks) are the second implementations that tell the
two apart, and they should be written *against the published contract*, as its first
external-shaped consumers. Opening at W1 would freeze the first draft of five interfaces; opening
at W5 and dogfooding through W6–W7 publishes contracts that have already survived a second author.

## 7. Migration path

### 7.1 Bootstrap — what becomes a model input unchanged

`Simulation/Kernel/` entire; the clock helpers (`ElapsedDays`, `AdvanceCheckpoint`, `ActivityDays`,
`LabouredTicks`, `RestampDeadline`, `PassagesThrough`); the catalogue (`Carries`, `Shades`,
`Refines`, `Equilibrium`, `LiftCapPercent`, `FloorLevel`); `KingdomSubsidenceRules` in full — the
model feeds it better numbers, it does not change; the brink shape; `KingdomWord`; the chronicle and
its budgets; `KingdomReports`; the option gates; `KingdomLiquids`; the crew machinery; the wear
machinery; the reach machinery.

**This is most of the mod, and it is the point.** The living city is a substrate change under
systems that already work, not a rewrite of them.

### 7.2 Refactor

| Today | Becomes |
|---|---|
| `KingdomSurvey.Take` | unchanged as *read the ground*; gains a sibling *reconcile against the model* |
| `KingdomSubsidence.RecordZone` / `OtherZones` / game-state keys | zone rows on `KingdomCityState`; `ZoneSighting` survives as the projection handed to `CityTally` |
| `r_KingdomPlot`'s stage + `NextStageTick` on the object | work-row run-state; the part keeps only appearance and the cheap due-tick nudge |
| `KingdomBrink`'s `KingdomBrinkRoofTick` &co. as object properties | resident-row fields |
| `KingdomGrowth.AssignWork` reading `Survey.Settlers` | reading the resident roster |
| `RosterNames` / `RosterOrigins` / `RosterArrived` parallel lists | resident rows |
| `LastKnownStorageSpace` | a zone row's capacity + `LastReadTick` |
| `NextArrivalTick` / `NextGuestTick` / `NextNotableGuestTick` / `RaidDueTick` | named clocks with ordinals |

### 7.3 Retire

The `r_TAF_Supports_*` game-state key family *(retired in W1, with `r_TAF_Larders_*` beside it)*;
the parallel roster lists; per-object brink properties; any remaining reading of city storage from
one zone only.

### 7.4 The wave plan

Disjoint ownership. Each wave ships. Each makes the city more alive. No wave edits an existing
TESTING.md pass except to *add* assertions; new behaviour arrives as new passes.

**W0 — Foundations, the seed, and the executor seam.** Mint and persist the realm `KernelSeed128`
at founding. Create `Simulation/City/` with `KingdomCityRules` (pure) and `KingdomCityState`
(carrier), fully unit-tested, **wired to nothing**. **Constitution addition: `KingdomExecutor` and
its reflection test ship here** (§2.5) — synchronous, no threading, but the choke point exists from
the first commit so that no wave after it can grow a second computation path. A seam retrofitted
across six waves is a rewrite; a seam laid first is one call. Write the check-in/check-out contract and the model schema into
`docs/API.md`. Serialization version bump, clean and deliberate (Addendum 9 waives migration
pre-release). *Playtest baseline: byte-identical. Nothing visible ships.*

**W1 — The city book: stocks, zones, works.** `KingdomCityState` on `KingdomSettlement`; zone rows
replace the game-state keys; check-in reconcile at the survey step; `SuspendingEvent`
check-out (with the lazy reconcile as the correctness guarantee); closed-form breakpoint
integration for stocks, reusing `Slide`'s shape. Subsidence, city storage, and the stage ladder
read the model. **The constitution adds three things to this wave, none of them optional:** the
**signed** catch-up counter and **container-level drains in dedication order** (§3.9 — W1 is the
first wave that depletes a store the founder is not standing in, so it is the wave that must do it
on real vessels rather than in a ledger); the **`reckon` timer and its counters** (§6.5 — the
receipt begins where reckon begins); and the 1-day-vs-90-day equality assertion (§0.0(a)).
*Visible: a two-zone city's numbers stop being five stale ints; a granary in the next zone actually
fills and empties, and the cistern you open holds exactly what the model says. Pass 26 gains "the
other zone's stores moved while you were in this one."*

**W2 — Residents become rows.** **Implemented authority cut (2026-08-25):** roster reports,
arrivals, notable guest lodging, work assignment, emigration, death/offices, conversion pruning,
reach, bounties, salvage, lab naming, and seal archives now consume resident rows through the
bounded `KingdomResidents`/`KingdomResidentRules` façade. `RosterNames`, `RosterOrigins`, and
`RosterArrived` remain public only as obsolete save-ABI projections: complete old rolls seed an
empty city book once as non-labouring `Abroad` claims; ragged evidence is retained rather than
cross-zipped; every successful row mutation projects outward one way. Roster → typed resident
records; brink windows move off
`GameObject` properties; `AssignWork` reads the roster; bodies bound by `ResidentId`; check-in
rebinds, reads back deaths and departures. **Constitution addition: the `KingdomBindingRegistry`
ships here in full** (§3.8) — residents *and* the transient-by-`JobId` contract, plus
check-before-mint and the stale-transient sweep, even though the first transient does not appear
until W3. Identity is a substrate, and shipping half of it is how a settler ends up in two places. *Visible: a settler in the zone next door can lose their
roof, be warned, and leave. Highest-risk wave — see §8, hard problem 2. Pass 24 (the brink) gains
a cross-zone case.*

**W3 — Materialisation by the hour, amortised from the first commit.** Anchors set by `DayShape`
against `Calendar`'s bands; `r_KingdomStation` riding `IdleQueryEvent` so settlers *walk* to their
post the way vanilla sends them to bed; items minted into real containers under the
`_stock`/`norestock` protocol, including the cross-zone harvest delivery Addendum 11(b-ii) already
ruled; told-once dating.

**The constitution changes this wave's shape more than any other, and all of it is day one, not a
follow-up:**

- **Amortised from the first commit** (§3.5) — the counter, the 8-unit per-turn budget, visible-
  cells-first ordering, the two hooks. *There is no interim wave in which materialisation spikes.*
  A batch-at-activation version is not a smaller first step; it is a different design that would
  have to be removed.
- **The heartbeat** (§3.6) — the 50-tick micro-reckon, all cities, and its surfacing budget.
- **Embodied arrivals and the itinerary** (§3.7) — porters, legs, `At(job, tick)`, re-projection at
  check-in, the edge handoff. Plus the **distance matrix** and the **roads discount** (§3.10 items
  2 and 3), because an itinerary *is* a route and cannot ship without a metric.
- **The `reify`, `slice` and `thaw` timers and Pass 32** (§6.5). The receipt is not a W6 deliverable
  any more; it ships with the behaviour it measures.
- **Prefetch-thaw** (§6.4), behind the option gate, explicitly cuttable — it buys smoothness, not
  correctness.

*Visible: walk in at Jeweled Dusk and the market is shut and the hearths are full; stand in your
house while a farm two zones away finishes, and a porter walks in with the crop.* ***New Pass 29 —
"A day in the city." New Pass 32 — "What the city costs, and what it delivers."***

**W4 — Happenings.** The generator, the shared `KingdomTellingBudget`, the told-log ring. Weddings,
funerals, feasts, festivals (creed dish), quarrels, breakdowns, deliveries, raisings. Surfaced
through ledger / `KingdomWord` / chronicle / guestbook. *Visible: come home to a city that has a
history. **New Pass 30 — "What happened while you were gone."***

**W5 — Engagement, and the API opens.** Works board, city-wide crew dial, day-shape policy,
petitions from model state, city-posted bounties, ceremonies from happenings, guests drawn by
events — all inside existing Charter entries. **Constitution addition: the extension contracts are
published here** (§6.6) — five interfaces, the marker attribute, `KingdomApiVersion` and the
refuse-loudly path — after four waves have shaped them and before W6/W7 exercise them as their
first consumers. *Visible: the city asks you for things, and another mod can teach it a new thing
to ask for.* ***New Pass 31 — "The city asks."***

**W6 — Production depth, and logistics that never look stupid. [SHIPPED]** Extend the real machines
in Addendum 11(c) order; the food chain end to end (seeds → crops → stores → meals/industry);
inter-zone haulage crews. **Constitution additions: nearest-holder sourcing and capacity-bound
batching** (§3.10 items 1 and 4) land here rather than in W3, because both only bite once many jobs
compete over many holders — which is precisely what the food chain and haulage crews create.
Shipping a 2-opt over a single open job would be optimising an empty room. The measured worst case
— a season away, timed and written down — is no longer this wave's job either: it has been running
since W1 as the receipt. *Visible: the economy is physical end to end, and no carrier ever walks
past a nearer store.*

> **What landed, and the one design decision worth naming.** W1 shipped the integration at a net
> rate of zero and said exactly why: the settlement pass credited the seated zone's works for the
> whole elapsed off `KingdomGrowth`'s **settlement-wide** `LastWaterWorkTick`, so a model that also
> credited them would pay the same day twice. W6 did not add a second accounting beside that one —
> it **moved** it, and left one owner:
>
> - **One clock.** Every zone's per-day make is measured onto its own row (`WaterCarry`/`FoodCarry`,
>   from the *same* `Supports` tally the ladder is derived from) and integrated off the model's
>   single `ProcessedThroughTick`. The settlement pass credits nothing. `KingdomCity.Stamp` writes
>   `LastWaterWorkTick` **from** the model's tick, so two owners of one day is a state that cannot
>   be reached rather than a bug to avoid. Days are counted as **world-day
>   boundaries crossed**, which is additive across every split — a heartbeat slice cannot cost the
>   city a day and cannot let the homecoming pay one twice.
> - **One ledger, and I1 by construction.** Production raises a row's level *and* its signed debt by
>   the same clamped amount, so `level − owed` — what the model claims the ground holds — never
>   moves for anything the works did unwatched. `KingdomProductionRules.TryReconcile` **re-derives**
>   the debt from the ground at every check-in, which makes `level − owed == ground` true by
>   construction; the audit line prints that identity and MISMATCHes on it. (Before W6 the reconcile
>   wrote the ground over the level and left the debt where it was — correct only while the debt was
>   zero, which a producing rate ends.)
> - **The mill kept its own clock, and had to.** A mill is not production: it takes real crops off
>   real shelves and puts real staples back, where the shelves are, and `MilledFoodPerDay` is
>   already subtracted out of the model's rate. It keeps `LastFoodWorkTick`'s elapsed. One clock
>   each; neither can spend the other's days.
> - **§3.10(1) and (4) live.** The carry draws on the nearest quarter actually holding the resource
>   (level-1 zone graph, tie-broken on row index); inside a quarter the oldest dedication still pays
>   first, so I4 and I6 are both true and answer different questions. A load bound for a larder a
>   porter is already walking to is folded onto that porter — batching at the one moment it can
>   prevent the pathology rather than detect it. Both "never looks stupid" checks are functions in
>   `KingdomLogisticsRules`, so the tests and the runtime ask one implementation.
> - **The receipt.** `KingdomWorstCaseReceiptTests` runs step 90's own city — 4 zones, 40 works, 60
>   settlers, a season away, a standing backlog — and pins every count against its §0.0 lane. Measured:
>   `R=104 steps=9 rows=1872/13312 planops=148 graphops=64 worstdrain=39turns model=13824B
>   realm=53572B`, and a year and a decade cost the same reckoning as the season.
> - **Deliberately not shipped, with the reason.** A second job *kind*. §7.4's own rule applies: the
>   refined-materials lane has no cross-zone flow to carry yet (stockpiles are per zone and nothing
>   moves between them), so minting a haulage kind for it would be the empty room this wave was told
>   not to optimise. The level-**2** distance slices (work→edge, same-zone pairs) also remain
>   unwired — they need the ground and W3 left them pure; the carry is sourced at the granularity
>   the model has holders at, which is the quarter.

**What the constitution changed about the plan itself.** Four things, worth naming so nobody
re-derives them: (0) two things ship *before* anything uses them — the **executor seam** at W0
(§2.5) and the **binding registry** at W2 (§3.8) — because both are substrate, and substrate
retrofitted across six waves is a rewrite; (1) performance work is **not a wave** — the receipt ships with W1 and grows with
every wave after, because a perf wave at the end can only discover that six waves need reworking;
(2) W3 has no un-amortised interim, because the amortised design is not an optimisation of the
batch design but a replacement for it; (3) W6 lost the receipt and gained the planner, W7 (networks) was added, and the extension API
opens at W5 so that W6 and W7 can be its first consumers — leaving W6 and W7 as the two waves that
can be cut if the playtest says stop.

**W7 — Networks.** Graph rows per network, the closed-form flow solve, brownouts in the stated
priority order, the seeding handoff both directions (§3.11). **Its own wave rather than part of
W6**, for three reasons: it is the only remaining piece that adds a *new model dimension* — a graph
— rather than deepening an existing one, and folding it into W6 would make W6 both the depth wave
and a substrate wave; it depends on W6's producers actually producing something worth carrying,
since a power network with one solar array and one charging post is the wiring puzzle
`KingdomPower`'s own comment already complains about, not a network; and it is the most cuttable
thing left after prefetch, which putting it last keeps true. *Visible: a cistern in the mine fills
from a pump three zones away, and when the array wears out the mill goes quiet — and you hear about
it standing in your house.*

**Ordering rationale.** W1 before W2 because stocks reconcile against objects that already exist,
while residents reconcile against objects whose identity we are changing — do the easy handoff
first and learn the protocol on it. W3 after W2 because you cannot place people you do not have.
W4 after W3 because a happening the founder cannot see happen is only a log line. W5 after W4
because engagement needs something to engage with. W6 and W7 last because they are the only waves that are pure depth rather than substrate, and they
are the two that can be cut if the playtest says stop — in that order, W7 first.

---

## 8. Risks, and the two hard problems

### 8.1 Risks, ranked

1. **The playtest gate has never been run** (COORDINATION.md standing agreement: *"Nothing is
   'done' until it has been."*). This design assumes a baseline that is asserted, not observed. W0
   should not start until at least Passes 1–3 and 23–26 have been run once against `main`.
2. **The model and the ground disagree in a way that reads as a bug.** Mitigated by attribution and
   telling (§3.1), but the *prose* has to carry it. Any reconciliation the founder can see must
   name a cause.
3. **Telling-budget starvation** — a season away in which the one line that mattered is the one
   that got summarised. Mitigate by ranking: brinks and irreversibles are never summarised, ever.
4. **A lane that draws per day.** The only thing here that can scale with absence. Enforced as a
   test rather than a convention, and now also as a *receipt*: §6.5's `perf reckon` line carries
   `rows` and `draws`, and Pass 32 step 90a asserts they are identical for a 1-day and a 90-day
   reckoning of the same model.
5. **Collision with the water lane in flight.** `KingdomSurvey` is the shared seam. W0 touches
   nothing; W1 must rebase on the water lane's landed state, not race it.
6. **Serialization churn across the waves.** Waived pre-release, but each bump stays clean,
   deliberate, and named (`FirstNamedSerializationVersion` moves with it), per Addendum 9.
7. **Scope, honestly.** Addendum 12 grew from (a) to (h) while this document was being written, and
   §3 now carries five sections that did not exist in the first draft. The mitigations are
   structural rather than hopeful: every new dimension is a **row** with a **cap** in §0.0(c), every
   new claim is an **invariant with a home and a test** in §0.0(g), and everything computed goes
   through **one seam** (§2.5). If a later wave cannot state its cost in §0.0's table, that is the
   signal to stop, not to add a footnote.

### 8.2 Hard problem 1 — dual books: who owns a dram

**The problem.** The model must own stocks while a zone is suspended, or nothing happens while the
founder is away. The player must be able to walk up to a cask and pour water out of it by hand, or
Addendum 11's physicality is a lie. Both naive answers fail: model-only makes the containers
decorative; ground-only makes the simulation impossible.

**Recommendation: two-phase ownership, lazy check-out, attributed reconciliation.**

- While attended, **the ground is authoritative** and the model mirrors it at the end of the pass.
- While suspended, **the model is authoritative**, carrying the zone's last-read numbers plus
  everything credited or drawn since.
- At check-in, the ground's actual number **wins** for anything physical, and the difference is
  attributed to a cause and told as news — never silently repaired, never treated as a fault.
- The eager `ZoneDeactivatedEvent` check-out is an accuracy optimisation only. **Missing a
  check-out must cost freshness, never correctness**, because save-and-quit and crash paths will
  miss it.

Why this is the right answer here specifically: it is not a new rule. It is `RecordZone`'s shipped
discipline — *rewritten from the ground every time, including down to zero* — widened, so the
codebase already contains the precedent, the reviewers already accept the shape, and the failure
mode is one the mod already has a voice for.

**The invariants to test** (both are cheap and both are mutation-resistant; §0.0(g) I1 is the
general form, and §3.9 is the direction this section originally left out):
1. After any attended pass of a fully-visited city, model total == ground total, per stock kind.
2. Over any sequence of hand moves and passes, total drams in the world is conserved: a
   reconciliation may reclassify a dram between civic and personal, but may never mint or destroy
   one.

### 8.3 Hard problem 2 — where a person lives: object or row

**The problem.** Today a settler *is* a `GameObject`, with their brink windows, origin, name, and
creed stored as properties on it (`KingdomBrink.RoofTickProperty` and siblings). Sixty people must
exist and change while their zone is on disk — impossible for object properties. But minting and
striking bodies risks the protection law, destroys conversations and relationships the player
built, and can duplicate a settler the player charmed, recruited, carried off, or killed.

**Recommendation: model-row primary, body as a durable view bound by a stable id.**

- Every resident has a `ResidentId`. The body carries **only** that id as a property
  (`KingdomResidentId`) and is otherwise a view.
- Bodies persist in their saved zone with everything the player did to them. Check-in **rebinds by
  id**, it does not re-create.
- Materialisation may **mint** a body for a resident with no living bound body in this zone, and
  may **move** an existing one. It may **never remove one.** The protection law, extended to our
  own people.
- A body the player killed reads back at check-in as `Dead`, with a cause, and gets a funeral.
- A body the player took away — charmed, recruited, followed them out — reads as `Abroad`: still on
  the roll, contributing no labour, and honestly reported as such. This is the same staleness voice
  the mod already uses about a sighting.
- Sequencing: **W2 ships the rows and the binding but not the placement.** Placement is W3. Doing
  identity and movement in one wave is how a settler ends up in two places.

**The invariants to test** (§0.0(g) I3 is the general form, widened in §3.8 to everything we mint):
1. No `ResidentId` ever has two living bound bodies.
2. Materialisation obliterates nothing, ever — asserted directly, in the live selftest.
3. `Population` == count of `Resident` rows == live bindings + `Abroad`; `Dead` rows reconcile with
   the `DeadNames` roll.

**The honest residual risk.** A player who charms half the settlement and walks them across Qud
leaves the model describing a city whose people are elsewhere. The design's answer is to *say so*
rather than to prevent it — `Abroad` is a real state with real consequences (no labour, the works
go idle, the level subsides toward what stands). That is the mod's doctrine working, not a hole in
it. But it wants a Pass-31 step of its own, because it is the shape a tester will find first.

---

## Appendix — the evidence this design rests on

| Claim | Where |
|---|---|
| Suspendability grace = 40 turns (5 with `CacheEarly`) | `D/XRL/World/Zone.cs:7413-7420` |
| A cached, non-suspended zone **is** ticked and gets `EndTurnEvent` — the 40-turn shadow window | `D/XRL/Core/ActionManager.cs:439-467` |
| Suspend keeps the object graph in RAM; only `FreezeZone` → `Zone.Release()` frees it | `D/XRL/World/ZoneManager.cs:1682-1712, 883-966`; `D/XRL/World/Zone.cs:2282` |
| Every **live** zone is written inline into the save; frozen zones are ids only | `D/XRL/World/ZoneManager.cs:468-520` |
| `SuspendingEvent` fires before `Suspended = true`, for any zone, and reaches `The.Game` | `D/XRL/World/SuspendingEvent.cs`; `D/XRL/World/ZoneManager.cs:1690` |
| `ZoneThawedEvent` fires only on thaw from disk, never on wake-from-suspend | `D/XRL/World/ZoneManager.cs:863-878` |
| World-map travel adds 300–900 `TimeTicks` per parasang step and fires **no** `EndTurnEvent` | `D/XRL/World/Parts/TerrainTravel.cs:161, 221-264` |
| `IGameSystem.ZoneActivated` / `EndTurn` / `NewZoneGenerated` / `GetPriority` are obsolete **and dead** | `D/XRL/IGameSystem.cs:2954-3070` |
| `The.Game.GetSystem<T>()` is a linear scan with `GetType()` per element | `D/XRL/XRLGame.cs:286-300` |
| `IdleQueryEvent` is vanilla's entire daily-life surface; returning `false` claims the actor's turn | `D/XRL/World/AI/GoalHandlers/Bored.cs:262-330` |
| `Bed` is the **only** time-of-day NPC behaviour vanilla ships | `D/XRL/World/Parts/Bed.cs:187-224` |
| An NPC with `Wanders=false` self-anchors to its first cell and `Bored` returns it | `D/XRL/World/Parts/Brain.cs:2056, 2507` |
| `_stock` / `norestock` / `IsImportant()` is vanilla's item-protection protocol | `D/XRL/World/Parts/GenericInventoryRestocker.cs:148-200` |
| One parasang = 3×3 zones, 50 Z-layers, surface Z=10, each zone 80×25 cells | `D/Definitions.cs`; `D/XRL/World/ZoneID.cs:12-27`; `D/XRL/World/ZoneManager.cs:3268` |
| Freezability turns = 0; freeze threshold = 1 | `D/XRL/World/Zone.cs:7467-7470`; `D/XRL/World/ZoneManager.cs:1036-1041` |
| `CheckCached` suspends then freezes, driven from `ZoneManager.Tick` | `D/XRL/World/ZoneManager.cs:976-1060` |
| `PinnedZones` cap is `> 3`, checked **lazily inside `GetSuspendability`**; overflow logs `ZonePins` and calls `PinnedZones.Clear()` — losing every pin, not just the offending one | `D/XRL/World/Zone.cs:7441-7448` |
| `ZoneRepair` = `BuildCounter += TimeTicks - LastTurn` → `Math.Max(1L, BuildCounter / TurnsPerObject)` → apply the batch → `RemovePart(this)` when drained; debt cleared on read | `D/XRL/World/ZoneParts/ZoneRepair.cs:51-58, 87-97, 99-102` |
| `ZoneRepair` binds `ZoneActivatedEvent` **only** — the engine's own catch-up misses thaw entirely | `D/XRL/World/ZoneParts/ZoneRepair.cs:30-43` |
| `ZoneThawedEvent` is sent from `Zone.Thawed` at the end of `TryThawZone`, after `Zone.Load`, `AddCachedZone` and `ForceCollect()`; `TicksFrozen = TimeTicks − FrozenTick` | `D/XRL/World/ZoneManager.cs:864-865`; `D/XRL/World/ZoneThawedEvent.cs:39-46` |
| `ZoneActivatedEvent` fires **before** `ActivateObjects()` and `Suspended = false` — our reify runs on objects that are not yet live, the same order zone generation uses | `D/XRL/World/ZoneManager.cs:1904-1907`; `D/XRL/World/Zone.cs:7776-7791` |
| `Zone.Suspended` is a plain **serialized** field (`Stale` is `[NonSerialized]`) — a thawed zone comes back suspended, i.e. resident and not ticked | `D/XRL/World/Zone.cs:199-204` |
| `ProcessSingleTurn` ticks and broadcasts only into cached zones with `!Suspended && !Stale` | `D/XRL/Core/ActionManager.cs:443-449` |
| `CheckCached` suspends only zones that are **not already** suspended, then freezes whatever `GetFreezability` allows | `D/XRL/World/ZoneManager.cs:998-1011` |
| `GetFreezability(0)` is `TooRecentlyActive` while `currentTurn - LastActive <= 0` — per-turn `MarkActive()` holds a zone resident, and omitting it releases the hold next turn | `D/XRL/World/Zone.cs:7472-7483`; `:2304-2307` |
| Thawing runs `ForceCollect()` → `MemoryHelper.GCCollectMax()` — one forced full GC per thaw | `D/XRL/World/ZoneManager.cs:829, 728-731` |
| Save-and-quit runs `CheckCached(AllowFreeze: true, ForceFreeze: true)` **before** `SaveGame` — but `ForceFreeze` bypasses the threshold, not the freezability test | `D/XRL/Core/XRLCore.cs:1138-1139, 1740-1741` |
| `SetCachedZone` = `MarkActive` + `ActivateObjects` + `Suspended = false` — the "keep it live" API, priced and refused in §6.4 | `D/XRL/World/ZoneManager.cs:1771-1776` |
| Game-level `EndTurnEvent.Send(game)` fires once per 10 segments, immediately before `ProcessSingleTurn` — a valid **pump**, never a valid clock | `D/XRL/Core/ActionManager.cs:1644-1655` |
| An actor is granted `Speed` energy per segment and acts at 1,000; a move costs 1,000 — **Speed 100 = one cell per tick** | `D/XRL/Core/ActionManager.cs:740, 741, 755`; `D/XRL/World/Parts/Physics.cs:3801` |
| `ZoneID` carries the stratum (`Assemble(...).Append(ZoneZ)`) — verticality costs the model nothing | `D/XRL/World/ZoneID.cs:12-24` |
| `IPowerTransmission` is abstract with five concrete families (electrical, hydraulic, mechanical, biomechanical, generic) | `D/XRL/World/Parts/IPowerTransmission.cs:12` and siblings |
| Network discovery is a **cardinal-only** BFS over cells collecting `Producers`/`Consumers`/`GridCapacity` | `D/XRL/World/Parts/IPowerTransmission.cs:1121-1195` |
| Charge walks by `ChargeAvailableEvent`/`FinishChargeAvailableEvent` → `Process(E)`; `QueryChargeEvent`/`TestChargeEvent` carry a `GridMask` OR-ed with `GridBit`, which is what makes a cyclic network terminate | `D/XRL/World/Parts/IPowerTransmission.cs:322-341, 383-393` |
| The flood-fill walks `GetLocalCellFromDirection` = `GetCellFromDirectionGlobal(..., bLocalOnly: true, ...)` — **a vanilla network cannot cross a zone boundary** | `D/XRL/World/Cell.cs:8051-8054` |
| `LiquidPump` ships with **no live carrier** (every carrier blueprint commented out); hydraulic pipes carry joules, not supply | `D/XRL/World/Parts/LiquidPump.cs`; `VANILLA-PRODUCTION-TRUTH.md` §0, §8 |
| Suspended zones are not ticked; live objects in them are dropped | `D/XRL/Core/ActionManager.cs:430-447` |
| `ZoneDeactivatedEvent` fires on the outgoing zone, to `The.Game` | `D/XRL/World/ZoneDeactivatedEvent.cs`; `D/XRL/World/ZoneManager.cs:1904-1905` |
| `Zone.Activated()` wraps the event in try/catch; `ePrimePowerSystems` fires right after | `D/XRL/World/Zone.cs:7775-7791` |
| Game systems receive game-level events via `RegisteredEvents.Dispatch` | `D/XRL/XRLGame.cs:357-384` |
| `TurnsPerDay = 1200`; `IsDay()` = segment 2500–9123; eight named bands | `D/XRL/World/Calendar.cs:13, 296-352` |
| `Temporary` is the engine's own tick-stamp catch-up | `D/XRL/World/Parts/Temporary.cs:137-157` |
| No rain, no spoilage, no plantable seeds, no multi-stage growth, no liquid network | `VANILLA-PRODUCTION-TRUTH.md` §0, §8 |
| Faction-level favourite dish via `<waterritual Recipe=…/>`, eight shipped | `B/Factions.xml`; `VANILLA-PRODUCTION-TRUTH.md` §2.3 |
| Charter is at 32/32 hotkeys | `Core/KingdomCharterPart.cs:85` and its comment |
| Zones per city: Camp/Steading 1 … City 4 | `KingdomZoningRules.ZonesForStage` |
| Caps: 2 cities, 60 population, 40 buildings, 200 chronicle entries | `KingdomSettlement.MaxSettlements`, `KingdomRules.MaxPopulation`, `MaxBuildings`, `KingdomChronicle.MaxEntries` |
