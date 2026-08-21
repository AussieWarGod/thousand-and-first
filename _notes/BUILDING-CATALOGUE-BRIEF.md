# Building catalogue brief — the authoritative input for the plots/materials/catalogue wave

Date: 2026-08-20. Author-approved in session ("this looks pretty good"). Supersedes nothing;
composes with the author rulings already on `main` (`fc7a710` no wells, `fc8c0cb`/`0a0c9bd`
equilibrium, `5818727` the capability grammar, `78c0bc0` vision verbs/haulage ladder).

## Plots are the unit of building

Grounded in vanilla's own village generator (`Village_InitialStructureSegmentation`,
`PopulationTables.xml:14366`): huts 4–6², common blocks 2–8², large 8–15², rare up to 15–50 ×
8–20, `Full` = the whole zone. Wall material per settlement is vanilla's own pattern
(`Village_StructureWall_*Default`: Fulcrete, Marble, Limestone, Foamcrete, Verdigris,
BrinestalkWall).

- Tiers: **S** (~5×4), **M** (~8×6), **L** (~12×9), **XL** (~20×14). Not every plot is roofed:
  fields, yards, reservoirs, markets, salt-pans are open plots.
- **Plot size is stage-gated**: Camp lays S; Steading M; Town L; City XL. The city literally
  builds bigger as it grows. Composes with district/tech gates without touching them.
- Budget per zone (80×25, interior ~76×20, roads reserved): ~1 XL + 2–3 L + 4–5 M + 6–8 S
  mature. `MaxBuildingsForStage` counts plots, not furniture.
- Existing single-cell civic furniture (cask rack, bunk, bench, …) demotes to **contents** of
  plots, populated the way vanilla huts populate from tables.
- Gatehouse is a placement rule, not a size: on the frontier wall, astride a road. Vanilla
  `PlaceHut` CLEARS its rect — never call it on ground holding anything player-placed; refuse
  instead.
- **Upgrades climb within a plot; sizes compete across plots** (author: "agree, that's great").
  No in-place S→M metamorphosis. M-entry ≈ 1.5× mature-S per plot at much higher raise cost;
  M-ceiling ≈ 4× mature-S. The overlap band is deliberate: plot-starved goes M, water-starved
  stacks S, crew-starved waits.

## Axes with gates, not rungs (author correction)

The four-rung grammar becomes reference points on continuous axes: **output**, **autonomy**
(founder effort still required), **refinement** (comfort/quality). Gates — stage, materials,
certification, covenant standing — open *ranges* of an axis. Structural balance survives as
gradient properties: the crude end of every axis is cheap and weak, the far end of autonomy is
always recovered-and-certified rather than built (so the commissionable solar condenser moves to
the salvage/certify path; dry ground's early answers are salt-pan, collection, trade), covenant
always carries its own price.

## Materials — clearance IS extraction

One system: clearing a plot yields what stood on it. Brush → little; trees → **timber**;
shale/rock → **stone**; marble seam → **marble** (rare); ruins → **scrap metal**. Effort scales
with hardness; removal *earns*. Materials stockpile like food (dedicated mark), are never
minted, and arrive otherwise by trade or salvage. Building costs = water + materials.
**Material is the theme**: wall material per settlement (vanilla pattern), and the marble house
needs marble. Vocabulary grounded: BaseWallMud/Rock/Granite/Marble/Brick/Metal exist; 17 tree
blueprints; `Scrap Metal` is a real item.

## Lifecycle: tent first, strike honestly

Housing opens at a **tent** (canvas/brinestalk, cheap, low comfort, small upgrades), then
strike-and-rebuild: timber hut → stone house → marble fine house, each with tiers. Demolition
("striking") is founder-ordered, chronicled ceremony; effort scales with what comes down;
returns partial salvage; frees plot and crew; refunds no water. Old buildings never stop
working; nothing regresses silently.

## The heart, and expansion

The rite ground seeds the heart; the heart drifts toward the built centroid. XL wants
heart-adjacent ground; a heart full of early huts is cleared by striking (cost + salvage), which
is the system working. Expansion direction is entirely the founder's — claims decide, grammar
follows, a staked plan beats the grammar anywhere.

## Terrain

- **Rivers/liquids**: water cells refuse plots, never filled by default. A river is an asset —
  wheel adjacency, collection, the reason the site was chosen.
- **Jungle**: expensive clearing, timber-rich.
- **Underground** (vertical claims exist): carved plots — high clearing cost, **yields stone,
  enclosure free (the rock is the wall)**. No weather: no sailvane/condenser/catchment; fungal
  crops instead (the fungal style's home). Natural chokepoints. Different catalogue subset by
  stratum.

## The luxury lane

Fine houses are a different good (quality vs quantity), and notables are the cash-out: a
**legendary trader settles only when a vacant fine house of sufficient tier exists** and the
shop tier warrants; office holders and keepers may want the same. S plots never obsolete —
struck economy-huts become fine-house ground.

## Bindings

Equilibrium model (author refinement on `main`): a building's output is **what it adds to the
settlement's sustainable level**; catalogue numbers are denominated in equilibrium contribution,
not flow. STANDARDS 7b: any plot/upgrade/clearance that stalls says why, once. Extensibility:
everything authorable from third-party XML. Protection law: nothing player-placed is cleared,
ever. Pillars as always.

## Co-opted from the sweep (2026-08-20, filtered against the pillars; full lists with
rejections in the sweep transcript)

Thirteen survived the four-part filter. The catalogue wave builds the S-cost ones alongside the
catalogue; M/L ones are follow-on waves.

| Idea | From | Shape here | Cost |
|---|---|---|---|
| **The posted price** | Majesty reward flags | A notice staked at the heart: "ten drams to whoever clears the shale ridge." Settlers and notables attempt posted tasks on their own judgment. Indirect control in the mod's own currency; clearance bounties pay twice (drams + materials). | M |
| **Worn ground** | Foundation/Ostriv desire paths | Roads are never drawn: cells on real settler routes wear to trodden earth, heavy routes become paths, and the founder may pave a worn path in the settlement's wall material. Answers the roads question: they emerge, then get formalised. | M |
| **Yard trades** | Manor Lords burgage extensions | An S/M house plot with free yard cells takes one yard work — dye vat, hide rack, vellum press — and the household takes up that trade. The late-game life of S plots. | M |
| **The carry-sign** | SS Salvage Beacons | A crafted marker planted on a pile/container the founder owns, anywhere: porters route out and haul it home over distance-scaled days, chronicled, losable to the road. The sign IS the explicit designation, so the protection law holds. | M |
| **Guests at the gate** | SS2 unique settlers | Notable wanderers logged between visits, each with one outward-pointing hook (a ruin, a machine, a debt). Lodge them and they settle with a trade; ignored, they leave a letter and the hook becomes a rumor — never lost, only relocated. | M |
| **Notable tastes** | Terraria happiness (praise half only) | A settling notable states one or two tastes in prose — near the heart, marble walls, a fungal cellar. Met tastes shade equilibrium up; unmet merely means their default. The penalty half is rejected. | S |
| **Leader traits** | SS2 leader trait pairing | Every notable gets one virtue and one flaw from Qud-flavored tables, prose-first, small equilibrium shading, no reroll. No flawless notables — appointments are texture, not optimization. | S |
| **The surveyor's plan** | SS ASAM vision | Staking posts a lookable plan object whose description is the finished building's, framed as intention; the chronicle later quotes it. Pure data. | S |
| **Visible construction** | SS staged building | Staked → cleared → frame → walls → done, crew standing at the plot while attended, lazy pass advancing it honestly while not. Presence grants nothing. | M |
| **The raising ceremony** | SS upgrade announcements | Mirror of the striking ceremony: completion while attended gathers the crew, shares water, chronicles those present; unattended, the homecoming tells it. | S |
| **The pattern-book** | Against the Storm draft (offer half only) | A chartered caravan occasionally carries a choice of one foreign design from three — a Yd Freehold roofline, a hindren weave-hall. The base catalogue is never gated on draws; this only ever adds. | S |
| **The salvage commission** | CDDA companion missions | Charter named settlers with water and provisions to a destination the founder has personally seen; resolves while adventuring, lands in the homecoming report. | L |
| **Pilgrims of the told story** | Songs of Syx tourism | When the outsider register grows loud enough, visitors arrive at the heart — pilgrims to the rite ground. The chronicle's drift, already simulated, starts walking in. | M |

Notable rejections (the reasons matter): SS2 HQ/departments and the City Manager holotape (the
definitional second job / management screen); chapter-gated toolsets and AtS-style catalogue
draws (abstainers lose the toolset); taxes (absence-accrual already IS this, done better);
per-plot upkeep-else-shutdown (the equilibrium model owns this); Terraria's happiness price
penalties (penalty-for-abstaining; even Re-Logic softened it); DF noble mandates (penalty by
construction); Songs of Syx death spirals (loss writes chronicles, not game-overs); random
disasters (off-screen loss is a debt).


## Addendum, 2026-08-21 — layers, sockets, footprints (author-directed)

**The layer stack**: plot (ground envelope) / building (what it is: I/O, cost, staff, chain) /
skin (dress). All three moddable.

**Footprint belongs to the building's tier, never the plot** (author's own formulation). Each tier
declares its footprint and roof state (`Open` / `Soft` / `Walled` / `Carved`); the sole invariant
is footprint ≤ plot, validator-checked at load and refused by name at upgrade. The ceiling is a
staking-time choice: stake big for room to grow (more clearing, more yard meanwhile) or tight for
the yard-trade sooner, accepting the ceiling until struck and re-staked. Yard = plot minus current
footprint, recomputed per tier; one footprint per plot, always. Sky-needing designs refuse walled
footprints by name; underground everything is Carved. The adoption enclosure test doubles as the
roofed test.

**Merge-by-key** (SS-style multi-mod layering): a later `<building Key="X">` merges — named
attributes override, omitted survive, `<skin>` children append (same skin-key replaces), chains
extendable. Guardrail: merges shape future commissions only; a standing building keeps its
materialised state, so a mod update never rewrites a built city.

**The plot as socket**: condemning keeps the plot as a re-buildable slot; "change what this plot
is" is one ceremony (strike + rebuild, keeping rect, lanes, door orientation). **Re-dress**: apply
any skin — including one a mod added later — to a standing building, trivial material cost, no I/O
change.


## Addendum 2, 2026-08-21 — typed plots, (type × size) set binding (author-directed, PENDING
the SS architecture deep-dive before integration hardens it)

**VERIFIED against SS2's own addon-author documentation (deep-dive 2026-08-21, sources in the
research transcript).** The author's model is SS2's real structure: 7 plot types × 4 fixed sizes,
a building plan bound to exactly one (type × size). Adopted here:

- The plot is TYPED at staking and the type binds; **the set key is (type × size)**. Plot type
  maps to the catalogue's `Category`. SS2 also has a middle *Building Class* layer between type
  and plan — our building-key layer already is exactly that; no new layer needed.
- Two verbs: *change the building* (within the plot's type×size set, cheap) vs *re-type the plot*
  (the full strike-and-re-stake ceremony). District zoning gates which plot TYPES may be laid.
- **Five places we deliberately do better than SS2, verified against their model**: (1) binding is
  DECLARED on the record and validator-checked — theirs is positional (which formlist you drop
  into), a documented foot-gun; (2) footprint ≤ plot is ENFORCED — theirs is convention only, and
  overhanging plans are a classic addon complaint; (3) an over-constrained or empty set REFUSES BY
  NAME (7b) — theirs silently falls back to random; (4) the protection law stands — SS2's plan
  swaps and city-plan upgrades scrap player-built items and eat sunk cost, the direct inversion of
  ours; (5) contents regenerate from tables — their persistent per-stage item refs are their
  documented save-bloat vector.
- **Their scars, translated for Qud rather than copied (author caution: FO4 is a very
  different game and engine)**: (a) the policy/content SPLIT is genuine design wisdom — global-tunable
  policy, per-design content — but their POLICY IS NOT: SS2 upgrades on elapsed days and a
  happiness meter, and neither exists here. **Time is labour, never maturation** (author ruling):
  crew-ticks on a scaffold are honest work; "wait N days and it improves" is not, and is the same
  witnessed-timer shape already ruled out in fc8c0cb. Every upgrade threshold is keyed to things
  with Qud meaning — drams spared, materials quarried, hands free, stage, standings — and our
  global knobs are surplus margins and reserve floors, never durations or abstract meters.
  **The corollary (author):** labour duration is real and felt — you do not build a stone house
  overnight (catalogue spread: tent 900 ticks, "a roof by nightfall", to 7200 for the heaviest
  works), and you cannot live in a half-built house — a scaffold carries nothing (no roof, no
  beds, no equilibrium) until its last stage stamps it built, verified at KingdomSurvey.cs:93 and
  KingdomGrowth.cs:497. Future knob if playtest wants it: crew count shortening builds to a floor;
  currently flat per design plus the craft-district discount;
  (b) registry change-detection is NOT adopted — it solved Papyrus quest-scan slowness, and our
  registry is one fast XML parse per load; (c) taste/blacklist curation is PROPORTIONATE, not
  first-class — their overload came from thousands of plans in a barter window; our ~50-design
  catalogue with style-suggested defaults is fine, and tags are a future option only if the
  modded catalogue ever warrants them. Footprint enforcement, hard for them in freeform 3D, is a
  rectangle comparison on our grid — kept, but trivial.
- Load-time validator addition: every commissionable (type × size) reachable at any stage has ≥1
  design, or the gap is named at load.

Merge-by-key and per-tier footprints are unchanged. The layers wave integrates with the type×size
correction applied at the socket eligibility layer.


## Addendum 3, 2026-08-21 — the upgrade trigger law (author-directed)

**Auto-upgrade when the city can absorb the disruption; offer-don't-act when it cannot.** Never a
timer. Materials and tech/craft requirements gate everything.

- **Housing** auto-triggers when all three hold: (1) DISPLACEMENT — the residents have somewhere
  *tolerable* to live during the rebuild, judged by their own standard via the existing
  tastes/refinement surfaces (an ordinary settler tolerates a bunk; the notable in the fine house
  does not tolerate a tent); (2) materials in the stockpiles; (3) tech/craft met.
- **Working buildings**: materials + tech always; auto-trigger ONLY when the projected reserve
  covers the output lost for the build's duration — never automatically take offline something the
  city leans on. Below margin it becomes an OFFER, 7b-legible ("ready to improve, and held — the
  city leans on it"), forceable from the Charter with the dip disclosed.
- Composes with shipped law: out of surplus only, one work per visit, hold-as-is per work and per
  zone, scaffold carries nothing mid-build, labour duration real.

Implementation: KingdomUpgradeRules gains the two absorption checks (displacement-with-tolerance;
output-dependency margin) after the layers wave integrates.


## Addendum 4, 2026-08-21 — the quality-of-life tag vocabulary (author-directed)

One open vocabulary replacing three private systems (tastes, displacement tolerance, cohabitation):

- **Buildings declare `Provides`** — open namespaced strings on `<building>`, mergeable XML.
- **Residents carry `Needs` (hard: no move-in, no job), `Prefers` (soft: tastes-style equilibrium
  shading, never a penalty unmet), `Refuses` (hard-negative: no cohabitation).**
- **Derive before authoring** (the creed principle): the resident side reads vanilla truth first —
  Robot needs charge not food, Aquatic needs adjacent water, fungal wants damp-dark, photosynthetic
  needs sky — from parts/tags already on every creature, including other mods' creatures, so a
  modded species is a correct resident before its author writes one TAF tag. Authored
  `<tag Name="r_TAF_*">` on creature blueprints (vanilla's own mergeable mechanism) refines.
- **Cohabitation**: creed feelings (engine faction table) for the ideological cases, `Refuses`
  tags for the rest.
- **Pillar guards**: placement constraints, never meters. Unmet Needs = the match does not happen,
  named per 7b. Nothing decays; a mismatched city is one certain people pass through, not a
  punished one. Ties into genotype: what work they can do, what conditions they accept, whom they
  will live beside and with.
- Addendum 3's "tolerable displacement" is re-based onto this: tolerance = the Needs check against
  temporary quarters.

Dispatches as its own wave after the layers/trigger-law integration lands.


## Addendum 4b, 2026-08-21 — housing binds (author ruling, supersedes the wait-forever shape)

- **No acceptable home, no joining.** The arrival gate is assignment-level, not a bed count: a
  settler joins only if a home exists that THEY would accept — Needs met, no Refuses conflict with
  its occupants. (Replaces the plain HasRoomToHouse tally.)
- **Losing all acceptable housing means they leave.** Announced once (7b, by name), a short grace
  of attended passes while the founder can act (raise a bunk, stake a plan, re-house), then
  emigration through the existing machinery — chronicled by name and cause in both registers.
- **The grace is attended-pass-denominated.** Absence never runs it; nobody comes home to a city
  emptied over a house that burned the day they left. They leave because the founder did not act,
  never because the founder was away.
- Guests unchanged: they never join without lodging anyway.
- The COHAB agent's "sleeps in the open and waits forever" is superseded; the integrator applies
  this at integration.


## Addendum 4c, 2026-08-21 — feelings scale with closeness (author ruling)

How citizens feel about each other always influences whether they can live together — scaled by
the quarters. You cannot jam five different believers into one bunkhouse and have it be fine.

- **Closeness per design**: Packed (bunk row, tent — one open room), Close (hut), Roomed (stone
  house — walls between beds), Private (fine house, manor). DERIVED from beds-per-footprint
  density first, `Closeness` attribute to override.
- **Pairwise tolerance = f(closeness)**: Packed shares only without quarrel (same creed or truly
  neutral); Close refuses the ambient −50 grudge; Roomed tolerates ambient dislike, refuses open
  hostility; Private refuses only hatred. The single CohabitHostility floor is superseded by this
  ladder.
- Consequence, intended: a diverse city must build better housing to exist — belief diversity is
  a thing you build for, in stone. Composes with housing-binds (4b): the refused simply never
  join, named per 7b.
- Document beside both constants why city-dissent and cohabitation read the same feelings through
  different lenses: polity is not proximity.


## Addendum 4d, 2026-08-21 — the fault-line ceiling (author-directed)

Above the closeness ladder sits a hard ceiling: **open hostility (the named −100 fault lines)
refuses ANY shared roof, at every tier including Private.** Walls between beds answer prejudice
(the −50 ambient grudge, tolerated from Roomed up); they do not answer a creed war — those pairs
need separate buildings entirely. Private's pairwise tolerance equals Roomed's; its value remains
quality and notables, never a tool for housing enemies. Refuses tags stay absolute. Intended
emergent consequence: a two-creed city physically partitions into quarters through the layout
grammar, with no code knowing the word "quarter."


## Addendum 5, 2026-08-21 — creed conversion (author-directed: education, shrine, culture,
diplomacy, osmosis)

Conversion is the healing arc the fault-line ceiling requires. Five channels, each riding an
existing system; conversions are rare, chronicled by name in both registers, never metered, never
absence-driven.

- **Osmosis**: household/quarter majority pulls the minority, counted in SHARED LIVING (attended
  passes under one roof/quarter), scaled by the closeness ladder — no conversion across a refusal,
  so well-housed diversity blends while ghettoized diversity hardens. Intended.
- **Shrine**: consecrated to a creed (a chronicled act), staffed, converts the neutral toward it
  within its quarter.
- **Education**: a staffed scriptorium SOFTENS rather than converts — taught neighbours treat the
  ambient grudge one band gentler (precedent: academy halves outsider drift).
- **Culture**: witnessed shared meals nudge attendees toward the table's majority. Free rider on
  existing ceremonies.
- **Diplomacy**: the water ritual with one's own settler — invited, consented, one at a time,
  chronicled. The founder builds conditions and shares water; they never order a conversion.
- Composes into the arc: partition (4d) → stone housing + scriptorium + meals → conversions →
  quarters dissolve. Architecture manages difference; practice converts it; investment heals it.

Dispatches after the closeness/ceiling work commits.


## Addendum 6, 2026-08-21 — reach: size and tier decide effect and area (author concept)

**Reach is derived from plot size × tier**: S = its tile/plot; M = its quarter; L = its zone;
XL = the whole city, or the realm. Tier shifts within the band; `Reach` attribute overrides;
the ladder is inherited by every design including modded ones.

- **Binding needs stay citywide pools** (water, food, roofs — physically drawn). **Lifts become
  reach-scoped** (faith, order, learning, luxury shade residents IN REACH), so quarters gain
  real character — the temple quarter is different ground from the tanners' — and place-quality
  feeds tastes, osmosis, and lodging with no new code knowing "inequality."
- **XL special functions unlock through the office machinery**: a named notable heads the great
  work (keeper of rites for the temple, archivist for the great scriptorium) — worker quality
  derives from who the settler is (attributes/traits, derive-first), the founder assigns nobody,
  and factionwide effects ride the same rule.
- S is cheap, low-rung, any-hands, local — and stays worth building forever (the wayside statue).
  XL is dear, high-craft, led, and citywide. Maintenance = staff + the existing upkeep economy;
  NO per-building material drains (a chore timer in robes) — flagged, author may overrule for XL.
- Existing hand-authored scopes (shrine's quarter, scriptorium's zone, academy's city) re-base
  onto derived reach.

Dispatches after the conversion integration commits.


## Addendum 7, 2026-08-21 — the physical material chain (author-directed)

Materials are ITEMS, extracted, refined, physically stored, and spent — for build gates,
repairs, upgrades, and event-driven maintenance.

- **The chain**: raw (timber, stone, scrap metal — extracted by clearance/quarrying/salvage) →
  refined (shaped timber via sawyer's yard, shaped stone via mason's yard, worked metal via
  smelter — each a staffed processing work) → spent. Plus **bits** (vanilla's own tinkering
  items, derive-first) pricing high-craft builds and certified-tech repair, and **exotic
  materials** (rare finds) for XL specials.
- **Infrastructure gates construction**: L needs the relevant yard standing and staffed; XL needs
  it headed by its notable (composes with Addendum 6's office rule). "Buildings that support big
  infrastructure upgrades," literally.
- **Crews have capability**, derived from settler stats (Strength for stonework and haulage,
  Intelligence for certified machines) — read off who they are, never assigned by the founder.
- **Maintenance/wear translation (binding)**: wear comes from EVENTS ONLY — raids, hard running,
  temperamental certified tech — never calendar. Damaged works run at reduced effect, named once
  (7b), never die, never spiral. Repair is a materials-and-hands job through the ordinary pass.
  Time is labour, never decay.
- Composes with Addendum 6 (reach): XL's citywide functions sit atop this whole ladder — the
  cathedral needs shaped stone, a mason's yard, a capable crew, and its keeper.

Dispatches WITH Addendum 6 as one wave: reach, the chain, crews, wear.


## BACKLOG — the absence split (author flagged 2026-08-21; do not lose)

The ruling already on main (`0a0c9bd`/`fc8c0cb`, hubris subsides) refines the old absolute
"absence accrues never decays," and recent wave briefs have been quoting the stale absolute.
The governing split, to be written into VISION/STANDARDS and enforced:

- **Supply-carried level SUBSIDES in absence** toward what infrastructure and automation actually
  support — a top-tier city with no supply lines crumbles back, to Camp if that is all that
  stands. Bounded, chronicled, arrestable midway. Player choice made real: automate, or
  hand-supply and be present — and if neither, the city returns to what holds without you.
- **Social and event processes stay attended-only**: dissent, conversion, wear, grace clocks —
  these never move while away.

Work items:
1. Reconciliation audit: does the hubris/equilibrium code on main still compose with the economy
   repivot, fetch clock, water detail, and equilibrium-carries built AFTER those commits? Name
   what subsides (population? stage? both?) and verify the catalogue's Carries feed it.
2. Sweep wave-brief boilerplate and code comments for the stale absolute; replace with the split.
3. Sweep TESTING.md for steps asserting "nothing happened while away" where supply subsidence
   SHOULD happen (e.g. the raid pass 29j, manifest steps are fine; stage/population steps may not
   be).
4. Wear stays events-only per Addendum 7 — subsidence is level, not damage.


## Addendum 8, 2026-08-21 — the time doctrine (author ruling; supersedes the absolutes)

**The world runs on time; consequences are realised at awareness.**

1. Processes happen as time passes: crops, refining, construction, wear-from-running, osmosis,
   dissent, subsidence toward the supported equilibrium. The settlement lives whether the founder
   is there or not.
2. Rates are time × labour × infrastructure — never time alone. Idleness wears nothing; an
   unstaffed yard shapes nothing. ("Absence wears nothing" was the wrong cut; IDLENESS wears
   nothing is the right one.)
3. Consequences crystallise at awareness (the lazy catch-up as fiction, not just engine idiom):
   outcomes good and bad are told when the founder is made aware. IRREVERSIBLE consequences —
   a settler leaving, a city seceding — wait at the brink for awareness rather than firing
   silently in the past: however long the absence, the moment of realisation carries the last
   arrestable window the design promised ("arrestable if caught midway", generalised).
4. Surviving rules: time never mints unchosen debts; the floor is Camp's own equilibrium; 7b
   speaks at awareness moments. Crops ripening on elapsed ticks are hereby SANCTIONED (nature is
   a worker); the earlier open decision is closed.

**The clock rework (next major wave), absorbing Backlog #1:**
- Reconciliation audit first: every counter's denominator (wear's consecutive-attended-visits →
  activity-time; conversion's shared-living → cohabitation-time; grace clocks → crystallise-at-
  awareness with the arrestable window), the 3-day forgiveness cap RETIRED in favour of
  subsidence-to-equilibrium, and the hubris code on main reconciled with the economy repivot,
  fetch clock, water detail, and equilibrium carries.
- Then implement: subsidence, activity-time denominators, awareness crystallisation — with the
  balance model re-run against the refined material costs (the integrator's flagged rebalance)
  in the same pass, since level, supply, and refined costs feed one equilibrium.


## Addendum 9, 2026-08-21 — engineering posture (author-directed)

- **Save compatibility is waived pre-release** — the mod has never run; no migration machinery
  for saves that do not exist. Serialization version bumps stay clean and deliberate.
- **Long-term discipline is not waived**: docs/API.md is the contract surface; supersession
  markers before removal; the XML schema is public API for modders (merge-by-key is its
  stability mechanism); migration scaffolding is built when the first release ships, not before.
- **Backlog — pre-release engineering pass** (before any public release): retire superseded
  surfaces (`CohabitHostility`, `JudgeCohabitation`, `TryAddSkin`, the flat `MaxBuildings` if
  still present), establish the save-migration harness and its test pattern, version the XML
  schema explicitly, and sweep for enterprise-grade structure per STANDARDS (services under 300
  lines, one responsibility, protocols at boundaries).

## Clock rework — approved rulings (author: "this plan is good")

P1-P4 sequence approved. The two flagged calls proceed as recommended: **a scaffold nobody works
on does not rise** (labour term via the crew-capability machinery; a zero-population settlement
raises nothing), and **idle yards announce once** (7b) with honest checkpoint ordering — idle
time produces nothing and says so.


## Addendum 10, 2026-08-21 — brink moderation + typed wear consequences (author rulings)

**(a) The brink moderates — warned consequences may fire in absence.** The author: "we can
control player awareness, and I think with enough warning, coaching, and fair time to resolve
something, it would be fair if things happened while they are away." The doctrine shifts from
consequences-wait-for-awareness to awareness-is-pushed:

- **Warning at the crossing**: when a process reaches its brink, word REACHES the player wherever
  they are (Qud-honest fiction — a runner, word on the road). Announce once, honestly dated.
- **Coaching**: the warning names the arrest action ("mend one and X has a home again" is the
  model), never just the doom.
- **Fair time**: the window runs in WORLD-DAYS from the warning's delivery, not in attended
  passes. Convert existing windows through the established exchange rate
  (CohabitationDaysPerAttendedPass = 3): roof 2→6 days, creed 6→18 days, city 3→9 days.
- **Fires in absence**: window spent with the cause still standing → the consequence fires,
  attended or not, with its normal prose; the aftermath is dated to when it happened.
- **Arrest unchanged**: removing the cause lifts and unsays at any point. Presence is not a
  shield; ignorance is — nothing irreversible ever fires UNWARNED.

**(b) Damage degrades function — for every work, in its own kind.** "Ruined/damaged buildings
should have appropriate consequences, reservoirs leak, solar panels reduce power output."

- The staffless-immunity question is RULED: wear reduces every work's level contribution,
  staffed or not (the staffed-only ternary in KingdomSubsidence.Supports is wrong).
- Beyond the general effectiveness scale, damage has KIND-appropriate consequences: storage
  works leak their stored contents as wear climbs; power works lose output; the pattern extends
  per kind as kinds become physical (food spoilage waits until food is a flow).
- Mending restores function — the consequences are of damage, not of history.

**(c) Collapse leaves ruins, in stages.** The author: "a place that has gone from city back to a
few tents should have ruins on the plots that were previously buildings, in appropriate stages of
ruin."

- A collapsed settlement's former building plots read as RUINS, not as pristine-but-nerfed
  works: name, description, and (where art allows) appearance reflect the stage of ruin.
- "Appropriate stages" — ruination varies across the works (the breakpoint sampling already
  spreads wear unevenly; deep collapse means deeper ruin), and a longer/harder fall reads more
  ruined than a shallow one.
- Ruins are still the player's (protection law): mendable, salvageable, never auto-cleared.
  Rebuilding on a ruined plot is the mend/upgrade lane, not a fresh-ground stamp.


## Addendum 11, 2026-08-21 — grounded production (author ruling)

**(a) Production must have a lore-visible reason — water first, everything eventually.**
The author: "any water production should primarily come from vanilla or vanilla lore friendly
producers … we shouldn't have a random plot that just produces water without any logical reason
as to why the building on the plot is producing the water. this should apply to all buildings,
the buildings should use vanilla furniture where possible to indicate real production, and hook
into real vanilla parts where it can."

- Water-producing buildings are lore-friendly TECHNOLOGY, **not immediately accessible** — they
  sit up the tree, behind resources/technology/automation/effort.
- Early game: **the camp costs water to keep running** — the founder supplies it, or arrives
  with a decent starting stock; production comes later and earns its reason.
- Storage stores; producers produce; the two are not the same building. A reservoir holds water
  someone brought or piped — it does not conjure it.
- Buildings use **vanilla furniture where possible** to show real production, and hook into
  **real vanilla parts** where they can. Applies beyond water wherever it makes sense.
- Existing constraint stands: no wells (prior author ruling on main).

**(b) Food is a real chain: seeds → crops → stores → meals/industry.**
"we should need seeds or plantable crops to start crops growing in a farm, and they should
produce that food into real storage where it can be taken from or added to, and food gets
consumed for favored meals or recipes by the residents, or used by industry to produce things."

- Farms START from seeds or plantable crops — real items the player obtains and commits.
- Harvest lands in **real storage** (real items in real containers — the larder flow), takeable
  and addable by hand.
- Consumption is **meals**: residents favour meals/recipes (Qud's cooking vocabulary), not an
  abstract ration tick; industry can consume foodstuffs as inputs to produce things.

Method note (standing): derive-before-author — survey the decompile + game XML for the vanilla
parts, furniture, plants, seeds, and cooking systems FIRST; co-opt before inventing.

**(b-ii) The harvest cycle runs without the player — rinse and repeat** (author, 2026-08-21):
"we need to also handle food being harvested while the player is away and potentially delivered
to storage on a different tile, and reset the growth cycle somehow, rinse and repeat."

- **Growth on world-time**: planted-tick → ripe after the crop's own days. The engine cannot run
  clocks in suspended zones (VANILLA-PRODUCTION-TRUTH: ratified tick-stamp idiom), so the cycle
  is stamped, never simulated.
- **Harvest crystallises when due, attended or not** — a harvest is a gain, not a brink; it
  simply happens, dated to the tick it was due.
- **Delivery may cross zones**: the harvest credits the CITY's stores at once (the ledger and
  the survey counters are city-level knowledge); the physical crop items MATERIALISE into the
  destination larder when that larder's zone is next active — the same crystallise-at-awareness
  idiom the rest of the mod uses. No touching objects in unloaded zones.
- **The cycle restamps from the harvest tick** and repeats — multiple cycles can elapse in one
  long absence, each dated — until the seeds are withdrawn, the field is condemned, or nobody
  tends what the design says needs tending (idleness does nothing where labour is the gate).

**(c) Extend the real machines; fill in only where vanilla is empty** (author, 2026-08-21):
"we can probably extend some of the existing vanilla machines and stuff to actually do/produce
stuff, same for food or water stuff, and the rest we will need to fill in."

- Preference order for any producing/working building: (1) **inherit-and-extend a vanilla
  blueprint/part so the machine genuinely does the thing** (Air Well fills, Millstone grinds,
  Hydraulic Irrigator ripens, Campfire cooks its preset meals, Capacitor charges); (2) **wrap** —
  an r_ part that drives a vanilla part's real behaviour on our clock; (3) **fill in** — a
  mod-authored part in vanilla's idiom, only where the survey shows vanilla has nothing (seeds,
  fermenting/pressing, spoilage, pipe networks).
- The vanilla part's own knobs are the tuning surface where they exist — our numbers derive from
  what the machine visibly does, stated in the catalogue comment.


## Addendum 12, 2026-08-21 — the living city (author direction)

"we might need to greenfield/bootstrap a lot of the multi-zone simulation for larger cities and
work out optimised ways to make them work with or alongside the vanilla engine, keeping it as
close to vanilla as we can, but still enabling a 'living' city that produces and consumes items,
events, meaning, activities, engagement, resources, and changes state meaningfully."

- A larger city is a SIMULATION, not a set of decorated zones: it produces and consumes items,
  fires events, hosts activities, carries meaning and engagement, and changes state meaningfully
  over world-time — attended or not.
- **With or alongside the vanilla engine, as close to vanilla as we can**: suspended zones run
  no clocks (engine fact), so the city model runs mathematically on world-time
  (closed-form/checkpointed at reckonings — the established kernel idiom), and each zone
  MATERIALISES its share of what happened when it activates: vanilla objects, vanilla parts,
  dated events, people where their day puts them.
- **Optimised**: all cost at reckoning, none per-tick; bounded state; deterministic draws.
- Greenfield where the current lazy-pass substrate can't carry it; bootstrap from what stands
  (Simulation/Kernel, zone sightings, crystallise-at-awareness, the ledgers and chronicle).

**(a-ii) Camp water bootstrap — verified and bounded** (author, 2026-08-21): the camp must ship
enough EMPTY containers that the founder can pour a stock and leave for a while, and the player
can dedicate more containers of their own. Founding and camp NEVER spawn water — verified: no
liquid is created at founding, all vessels ship empty, the basin is filled by hand. Fresh water
is hard to come by: fetching draws only from pre-existing fresh water in the founded area (the
OpenWater survey — already true). Vessel/building storage sizes are balanced against the vanilla
container ladder (canteen, waterskin, the ~128-dram urns) — sensible multiples of real items,
stated in the catalogue comments. OPEN AUDIT (post-G2): camp kit container count for a
pour-and-leave buffer; the player-placed-container dedication path end to end; vessel-size
sanity pass against the vanilla ladder.

**(b) The performance constitution** (author, 2026-08-21 — "we need to make sure we don't run
out of memory, or lag the game, but we need a system that keeps latency down … map out changes
to a zone in almost real time for players … elegant and clever, but grounded in computer
science, mathematics, and vanilla engine/code"):

- **Model over pinning**: the city model is authoritative for suspended ground; we do NOT hold
  city zones live (`MarkActive`/suspendability veto) as a design basis — vanilla's `PinnedZones`
  caps at 3 and log-and-clears above it, and every live zone costs its full turn tick, its
  2000-cell EndTurnEvent broadcast, its RAM, and inline save bytes. The vanilla 40-turn grace on
  recently-left zones is ridden, never extended.
- **One clock**: `The.Game.TimeTicks` only. Turn counting under-counts world-map travel
  (300-900 ticks per parasang step, zero EndTurnEvents) — banned as a clock.
- **Reckon is O(model)**: closed-form between breakpoints, never O(days), never O(cells).
- **Reification is AMORTISED**: zone catch-up spreads over turns on a budget counter — the
  engine's own ZoneRepair idiom (`num = max(1, counter / TurnsPerObject)`) — visible-first, so
  entry never spikes and nearby change appears in almost-real-time. `ZoneActivatedEvent` AND
  `ZoneThawedEvent` are both reify hooks.
- **Memory bounded**: no per-cell city state; rows and logs capped; save-size respected — the
  model serializes as named fields on the settlement, zones stay evictable.
- **Measured, not assumed**: a perf receipt (timings logged in-game at reckon/reify) ships with
  the first living-city wave and every wave after; budgets stated in the architecture doc and
  regressions treated as failures.

**(c) Cities scale; cross-zone life is felt where you stand** (author, 2026-08-21): "a city
might end up being 9 zones or more, especially with verticality … if in one zone a generator,
building, etc wears down enough to stop producing, or meaningfully impact what you would find in
the zone you are actively in, that needs to be simulated and felt (ie, walking around in my
house in 1 zone, a farm finishes harvesting in another zone, a porter should come and put the
harvested goods in the storage that is in the zone i am walking around)."

- City size is unbounded by the ARCHITECTURE (O(rows), zone rows keyed by zone id incl.
  stratum); the current 4-zone cap is a stage-gate rules constant, not a limit.
- The heartbeat is a bounded periodic MICRO-RECKON while attended: every N ticks the city model
  advances by the elapsed delta and surfaces what changed — a producer failing in another zone
  is known and felt without moving.
- Flows whose destination is the attended zone arrive EMBODIED: porters walking real goods to
  real storage, repair crews, messengers — vanilla Brain goals, spawn at the edge, deposit,
  leave. Unattended destinations take the model credit and materialise later. One event, two
  renderings, no divergence.
- **The porter scenario is the canonical acceptance test** for the living city and becomes a
  named TESTING pass when the wave ships.

**(d) Two hard invariants** (author, 2026-08-21): "we need to make sure those NPC's don't
accidentally get duplicated across zones, and where water is deficit, the storage it's taken
from is updated accordingly, not just in a ledger."

- **One identity, at most one body.** Residents bind bodies by KingdomResidentId (mint-or-move,
  never duplicate). Transients (porters, messengers, crews) bind by JOB id: one job, at most one
  body, ever; the model is the single source of job completion; a thawed zone despawns any body
  whose job the model already closed (goods never doubled). One registry answers both, checked
  before any mint, across all zones.
- **Deficits drain real containers.** Model consumption applies to ground at reify,
  container-level: the cistern opened after a season holds exactly what the model says remains;
  the larder holds exactly the crops uneaten; cross-zone draws land on the remote zone's real
  containers when that zone next renders. Drain order is stated and deterministic (reloads
  deplete the same vessel first). The audit: model total == ground total after any attended
  pass; mismatches are attributed and told, never silently repaired.

**(e) Journey continuity** (author, 2026-08-21): a carrier fetching from storage to a producer
"needs to path to the *correct* zone … in the *correct* amount of time. If they come into my
zone, fetch water, i should be able to follow them back … or run into them taking it … they
should not only walk in the correct direction, but appear appropriately in other zones if i
enter them later, with the water on them, or in the place they were taking it to appropriately."

- **A job is a timed itinerary**: legs computed at creation (zones, entry/exit edge cells,
  in-zone path lengths, walk speed) with kernel draws, so for any TimeTicks the model answers
  "where is this carrier and what is on them" — one answer, every zone renders it.
- Rendering: attended zone → live actor with real cargo walking the true path; any zone entered
  mid-route at the right tick → the body at the interpolated position; after delivery → cargo in
  the destination container (12(d)), carrier on the return leg or at post. The body never
  literally traverses zones; consistent re-rendering is indistinguishable from following.
- Interference while attended: ground wins — delays update the job's real progress at check-in
  and the itinerary re-projects; death/robbery fails the job, attributed and told, cargo where
  it fell, never double-delivered, never silently restored.
- Composes with 12(d): one job, one body; container drains land where and when the itinerary
  says.

**(f) Logistics locality and optimal-enough routing** (author, 2026-08-21): "a building should
try to fetch stored resources from whatever building is holding it closest to them, and citizens
should path 'optimally' for pick up and delivery, something i know rimworld struggles with."

- **Nearest-holder sourcing**: input jobs bind to the closest container actually holding the
  resource, by real path distance through the claimed-zone graph; deterministic tie-break.
- **Central batch planning, not agent AI** — the structural answer to RimWorld: jobs are planned
  at reckon over a frozen snapshot and rendered as 12(e) itineraries; no pawn ever decides
  per-tick with local knowledge, so the distant-stack pickup and the zigzag cannot happen.
- **Precomputed distance matrix**: two-level (intra-zone path length + inter-zone crossings),
  O(works²) ≤ ~1600 entries, invalidated only on placement/removal/road change.
- **Roads discount the metric** — laying a road visibly shortens every itinerary using it;
  the road machinery becomes logistics infrastructure.
- **Capacity-bound batching**: route-overlapping jobs share a trip (nearest-neighbour + 2-opt
  over the few open jobs per slice); bounded, deterministic, never-looks-stupid is the bar.

**(g) Networks: pipes, conduits, electricals** (author, 2026-08-21): "can we simulate networks
of water, electricity, other liquids to enable buildings to work over multiple tiles and have
containers have actual proper numbers for the water or resource they are holding?" — YES, same
two-layer authority pattern:

- **Attended zone = vanilla transmission parts** (11(c) extend-first): the engine's
  IPowerTransmission family (electrical/hydraulic/mechanical) carries real charge/force along
  real conduits with real events; buildings span tiles natively. LiquidPump has no live vanilla
  carrier — liquid piping is fill-in, in vanilla's idiom.
- **Model = graph rows + closed-form flow solve at reckon**: nodes are works/containers, edges
  are player-placed conduit segments; topology changes only on placement (distance-matrix cache
  discipline); flow conservation netted per network, deterministic; zone boundaries invisible to
  the graph. Deficit = brownout EVENT — works stop in a stated priority order, felt and
  announced per 12(c).
- **Containers hold true numbers** — 12(d) applied: model allocations land on actual
  LiquidVolume drams / Capacitor charge at reify; check-in reads live parts back as ground
  truth. LiquidVolume is liquid-agnostic: stocks key by (network, liquid).

**(h) The executor seam** (author, 2026-08-21 — "should we use our own thread for this? future
proofing, would that make things materially harder or more fragile?"): build the SEAM now, run
the THREAD later. All model computation flows through one executor choke point —
submit(frozen snapshot) → result — synchronous today, contract enforced by test (immutable in,
immutable out, no engine types across the boundary; the *Rules.cs engine-free discipline already
guarantees purity). A threaded executor swaps in later without touching callers, and third-party
mod computations inherit budget/timeout/error isolation from the same contract — a misbehaving
job stalls itself, never the city or the turn. Threading eagerly with no workload is where
fragility lives; the seam costs nothing and is the opposite of fragile.

**(i) The model is a public API** (author, 2026-08-21: "we should also try to API this so other
mods can extend the model if they don't want to contribute directly to the mod"):

- Data lane stands (XML merge-by-key). The behaviour lane: model extension contracts — new
  resource kinds, job/carrier kinds, network kinds, happening generators, work behaviours —
  registered by ATTRIBUTE DISCOVERY (the engine's own idiom), no fork, no hard reference beyond
  the contract namespace.
- Extensions live under the same invariants as our systems, enforced not trusted: kernel draws
  through our API (no Random), frozen snapshots in / results out (the 12(h) executor contract),
  budget/timeout/error isolation (a broken extension stalls its own job, never the city), 7b
  telling through our surfaces.
- Contract VERSIONED: API version checked at registration, refusing loudly by mod name on
  drift — never silent misbehaviour.
- Architectural basis: the model is rows + pure rules + one executor; an extension is more rows
  and more pure functions under the same contract — the model does not distinguish ours from
  theirs.
