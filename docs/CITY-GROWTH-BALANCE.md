# City growth and land-use direction

Accepted user direction, 2026-09-14. Tracking issue:
[#208](https://github.com/AussieWarGod/thousand-and-first/issues/208).
This document records requirements and planned acceptance work, not completed mechanics.
Current implementation evidence remains in [STATUS.md](STATUS.md).

## Objective and priorities

Get Thousand and First to Beta, develop mod features, and cover all features and behavior
with automated in-game tests exercising complex scenarios. Further Alpha releases are allowed.
City balance is part of that goal. It must not displace working Quickstart, construction,
recruitment, save compatibility or recovery from failures.

Protect the starter path against the reported housing/citizenship/construction deadlock and
against all four starter citizens leaving for lack of beds. Retain regression coverage for
full claimed-local-map sight, including line-of-sight occlusion and cold load. The reported
heart-tent appearance (a row of canvas rather than an enclosed building) remains a player
clarity concern: building appearance and tutorial descriptions must communicate its actual
function. Do not treat an open camp structure as proof of enclosed housing.

## Accepted design requirements

| Requirement | Intended behavior |
| --- | --- |
| Natural outward expansion | Additional useful buildings and their land needs encourage expansion into adjacent claimed maps. |
| Roughly 20 residents per tile | A planning and balance benchmark arising from plots, buildings and the supporting economy. It is not a hard resident cap. |
| A viable 50-resident, highest-tier city | Requires more than one local map because its buildings and public spaces need that land, rather than a newly added tile-count gate. |
| Roads at least 3 cells wide | Actual connected, traversable local-map footprints, including turns, intersections and approaches. A width label is insufficient. |
| Courtyards and parks | Preserve usable space for these in ordinary layouts; leftover inaccessible fragments do not meet the intent. |
| A complete urban economy | Allow land for farms, quarries, industry, defences, water, storage and civic functions as well as housing and circulation. |
| Rewards for good layouts | Give useful, understandable benefits for attractive, well-spaced, connected cities. Preserve freedom to choose layouts. |
| Real multi-tile coverage | Exercise several claimed local maps belonging to the same city, including travel, construction, economy and persistence. |

Here, a tile means the claimed local map/zone under discussion; road width is measured in
individual cells inside that map. Do not silently equate a local map with an entire parasang
or count separate cities as one multi-tile city.

The later physical-space clarification supersedes interpreting the earlier “20 residents per
tile” answer as a hard gate. Do not add a recruitment cap, hidden population clamp or new
minimum claimed-zone check merely to force the target. Tune physical requirements and useful
infrastructure instead. Review existing population, tier and minimum-zone gates too; retaining
an arbitrary gate unchanged does not satisfy this direction. Audit existing stage-based claim
access so players can expand before land pressure prevents reaching the prerequisite for expansion.

## Baseline and decisions still needed

At source `043d0562`, plot reservations use a one-cell road margin and a percentage area
budget; road clearance supports widths one and two. These are not proof of three-cell
streets, connected public space or balanced land use. Review `KingdomPlotBoundsRules`,
`KingdomRoadClearanceRules`, road width persistence, and authored building/plot footprints
together. Existing saves and extension-authored roads need an explicit compatible transition.

Measure actual viable layouts before choosing footprint or support-demand changes. Compare
stages, terrain and relevant styles, with physical beds, workers, consumption, production,
storage, entrances and circulation all accounted for. Housing-only packing is insufficient.
Do not shrink authored footprints or bypass normal commissioning to achieve a test target.

Reward mechanisms and numerical bonuses are not yet selected. Movement or trade benefits
from broad connected roads, and resident benefits from accessible parks or courtyards, are
candidates to evaluate. A benefit must explain itself in gameplay and produce observable
value. Test disconnected road strips, inaccessible public spaces, overlapping claims on the
same space and decorative spam so rewards follow usable layouts rather than counters.

## Delivery and behavioral acceptance

1. [#209: Streets and public space](https://github.com/AussieWarGod/thousand-and-first/issues/209)
   covers actual three-cell routes, plot/entrance coordination, constrained terrain,
   obstructions, boundary connections and compatibility with existing works.
2. [#210: Land-use balance and rewards](https://github.com/AussieWarGod/thousand-and-first/issues/210)
   measures and tunes the complete economy, selects useful layout rewards, and proves a
   reachable expansion path without a new bootstrap deadlock.
3. [#211: Multi-tile behavioral scenarios](https://github.com/AussieWarGod/thousand-and-first/issues/211)
   proves same-city claims, physical housing and staffing, resource conservation, construction,
   travel/unloaded reconciliation, disruption/recovery, and real save/quit/cold load followed
   by the next ordinary action on both claimed maps.

Use normal claim and commissioning flows for balance acceptance. Run multi-day scenarios
that challenge housing, water, food, material supply and routes. Define and test which
benefits are local and which are city-wide. Verify that cross-boundary travel and load do not
duplicate or lose residents, goods, money, work identities or paid receipts. Preserve a
recoverable settlement when support is disrupted; explain blocked progress to the player.

Existing away-travel scenarios explicitly visit an unclaimed destination. They do not prove
a multi-claimed-map city. The dense 50-resident paid-heart fixture uses synthetic prerequisites,
including legacy water-producer roots; it is a construction stress diagnostic, not evidence
that an ordinary player can fit or sustain that city on one map. Full paid heart progression
and its persistence remain separate gaps tracked by
[#159](https://github.com/AussieWarGod/thousand-and-first/issues/159),
[#160](https://github.com/AussieWarGod/thousand-and-first/issues/160),
[#162](https://github.com/AussieWarGod/thousand-and-first/issues/162) and
[#144](https://github.com/AussieWarGod/thousand-and-first/issues/144).
The current chain only targets rungs 1 through 4; the fifth arcology rung and its real
save/cold-load continuation remain required. Existing rung-two cold-load evidence does not
cover later hearts. Paid progression blockers remain linked through #212–#215, including a
resident occupying future wall ground during handover; they do not replace the balance work.

Follow [DEVELOPMENT.md](DEVELOPMENT.md) for the shared Codex/Claude edit loop: focused checks,
early fixture preflights, then relevant native scenarios. Preserve failed evidence and exact
acceptance scope. Record measured timings before claiming optimizations. Update issues,
this document when decisions change, STATUS for evidence, and the common Git-directory
handoff for live process ownership. Planning checkboxes are not behavioral acceptance.
