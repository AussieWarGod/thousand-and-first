# Architecture polish contract

**Status:** implementation authority for the first full visual/architectural polish pass.
**Source:** the author's direct rulings of 2026-08-25, reconciled with the author-approved
`BUILDING-CATALOGUE-BRIEF.md`, `STANDARDS.md`, and the target Qud 2.0.211.51 data/source.

This contract closes one mistaken release shortcut: a plot-sized rectangle with a centre object
is not a building. Catalogue validity, source compilation, and a force-build that does not throw
are not evidence that a structure looks sensible, expresses its builders, or performs the job its
description promises. Earlier release-candidate and visual-coverage claims are withdrawn until
the gates below pass.

## 1. Authority and supersession

Where this contract conflicts with an older implementation or status note, this contract wins.
It does not weaken save ABI, protection, deterministic simulation, material conservation, or
error-containment rules in `STANDARDS.md`.

The following direct rulings are controlling:

1. A plot is a reserved lot, comparable to a Sim Settlements plot. It is not the building.
2. A typed lot may hold different authored plans from the same declared type-by-size set.
3. A building is behavior-bearing and has authored tier maps. It can grow as labour, materials,
   skills, knowledge, technology, and other declared triggers permit.
4. Same-set replacement keeps the lot. A different type or size is a real strike and fresh
   restake, not a relabelled build on the old rectangle.
5. Buildings, plots, roads, utilities, vertical works, landmarks, and megastructures must look and
   feel good in Qud, use plausible materials, place functional furniture, and remain traversable.
6. Architecture varies where appropriate by function, tier, technology, terrain, style, creed,
   builder culture/body, and retained site fabric.
7. Vanilla assets are preferred. Custom bitmaps are permitted only when they reach human,
   vanilla/mod quality and carry truthful source, licence, and provenance records.
8. Construction is time x labour x infrastructure. Elapsed time measures possible work; it never
   raises an unstaffed structure by itself.

Older blanket no-raster policy is superseded only to the extent needed by ruling 7. No custom
raster is required merely to make topology distinct; multi-cell vanilla composition is the first
choice. Older plot/building conflation, generic-wall fallback, raw-clock construction, and
settlement-wide material wallpaper are rejected.

## 2. Product model

The durable composition is:

`reserved lot -> plan family -> authored tier map -> context variant -> paid palette -> semantic decor`

### 2.1 Lot

A lot has durable identity:

`LotId + TypeKey + SizeClass + Rect + Facing/Pose + public frontage`

- Type and actual staked size bind when the lot is staked.
- `KingdomPlotId` remains the wire-compatible LotId; do not rename it in existing saves.
- The lot envelope and pose survive tier upgrades, ordinary strike to socket, and same-set plan
  changes.
- The lot owns no architecture. Empty and struck lots remain readable as typed sockets.
- Lot size is the actual binding staked, not the minimum size of whichever design happens to
  occupy it.

### 2.2 Occupancy

An occupied lot freezes:

`PlanKey + BindingKey + BuildKey/tier + VariantKey + PaletteKey + resolved layout snapshot`

- The behavior-bearing building root appears at exactly one authored `main` anchor.
- Other objects are fixtures, surfaces, or structural parts, not extra counted buildings.
- A compiled binding is one exact `(TypeKey, SizeClass)` set. No stretch or category fallback.
- Variant selection is deterministic and frozen. A later XML merge shapes future commissions; it
  cannot repaint, reprice, or reroll a standing building.

### 2.3 Plan changes

- **Tier upgrade:** same plan/binding/lot/rect/pose. Apply an exact authored delta after the
  existing material, knowledge, labour, displacement, and output-reserve gates pass.
- **Same-set plan change:** same type and actual size. Keep LotId/rect/pose; require an explicit
  cheap conversion quote and a preflighted map delta. Absence of a declared transition refuses.
- **Retype or resize:** full strike and fresh siting/restake with a new LotId. Never reuse the old
  rectangle under a renamed operation.
- **Redress:** visual treatment only; it never changes function, plan topology, set, or paid
  material receipt.

## 3. Authored layout schema

Runtime data uses the mergeable root `KingdomArchitectures`, schema 1. It contains palettes,
maps, plans, exact bindings, tiers, variants, glyph recipes, anchor requirements, and rows.

Every map declares or compiles these facts:

- canonical width and height, north-facing authoring pose, allowed rotations/mirroring;
- Ground, Structure, and Object layers, at most one placement per layer and cell;
- claim mask, cover state, passability, and adjacent-use requirements;
- exactly one `main` anchor and at least one `entrance:public` where the plan is entered;
- stable functional anchors such as `bed:1`, `work:forge`, `storage:grain`, `power:input`,
  `liquid:fresh-in`, `crop:row1`, `service:loading`, or `shrine:altar`;
- required anchor counts and clearance widths for the plan's function and likely bodies;
- palette slots with concrete blueprint, material class, craft rung, and any knowledge gate;
- explicit selector context and a mandatory eligible fallback;
- an exact, bounded, canonical snapshot and hash.

Maps are authored footprint/claim masks, not implicit shells. Courts and service yards may be
claimed open cells. Unclaimed lot cells are the yard. A carved structure uses retained natural
rock explicitly. Temporary frames are compiled construction operations and do not enter the
standing snapshot.

The loader merges keyed records after every `KingdomArchitectures` stream. Omitted attributes
survive; map row blocks replace atomically and never splice by row. All validation occurs after
the complete merge. Malformed, oversized, unresolved, material-incoherent, or unreachable plans
are named catalogue faults and are not commissionable. Shipped content has no generic rectangle
fallback.

## 4. Construction and durable receipts

No positional fields may be appended to `r_KingdomPlotWorks` after `DoorY` or to
`r_KingdomSocket` after `LastDesignKey`. New state uses named `GameObject` properties.

At minimum, works/root/socket carry the frozen lot type, size, facing, schema, plan, binding,
tier, variant, snapshot, and hash. Every managed component carries LotId plus a stable layout slot
such as `g:004`, `s:012`, or `o:007`. Standing works also freeze their paid water/material/work
receipt; strike and salvage never reread a mutable catalogue price.

New plot works carry required work, remaining work, last-work tick, and shortfall-told state as
named properties. Each pass consumes the elapsed interval whether or not anybody worked it. After
water duty and running works, one bounded gang of real named settlers takes the oldest active
raising; every other frame is explicitly queued. The selected root alone applies its stamped
headcount, relevant capability/identity, and construction infrastructure. Idle and queued intervals
do not bank. Visual stages derive from completed work, not the calendar.

While attended, those exact settlers receive a construction post beside the root and walk there
through vanilla pathing. Completion, reassignment, or disappearance releases the post and restores
a home anchor; construction never mints, clones, or teleports a body. Save state lives on named
object properties, not appended positional part fields.

Legacy in-flight plot works without the new schema finish through the old codec and clock. Legacy
standing generic plots are not background-rewritten. On their first normal upgrade/strike, the
runtime inventories the actual owned pieces into a bounded legacy snapshot or refuses by name if
identity is ambiguous.

## 5. Tier deltas and state protection

Both old and successor snapshots transform through the same frozen pose. The delta:

1. retains a placement only when world cell, layer, concrete blueprint, and stateful anchor agree;
2. removes old-only pieces in object -> structure -> ground order;
3. adds new pieces in ground -> structure -> object order;
4. updates claim, cover, anchors, and frontage only after exact placement succeeds;
5. preserves the same behavior root unless an explicitly reviewed handover applies.

Non-empty containers, liquids, residents, wear, names, player additions, and third-party state are
never silently deleted or rerolled. A changed stateful anchor needs a registered handover. A new
claimed cell occupied by a yard work or protected object refuses before debit. Main-anchor movement
turns an automatic upgrade into a manual rebuild unless the plan has a reviewed exception. The
evolving heart is the explicit accretion exception around its fixed rite anchor.

## 6. Qud architecture doctrine

Qud settlements vary topology first, material second, role contents third. They mix local and
retained fabrics. One settlement-wide wall blueprint is not Qud architecture.

- Marsh settlements use crop/water courts, brinestalk, stilts, bridges, and mixed rusted fabric.
- Jungle and fungal settlements use rounded or corner-cut grown cells, canopy, damp circulation,
  low furniture, and restrained lighting.
- Flower/Hindren work uses petal wood, open floral courts, cushions/bedrolls, and textile space.
- Ruin/Eater settlements preserve historical layers and exposed services, patching them with
  rubble, worn wood, or limited salvaged plate rather than replacing the site.
- Cave/Barathrumite work exposes pipes, wire, hydraulics, lockers, benches, rock, and practical
  circulation.
- Gyre work may use chitin, bone, transported stone accents, wind, ritual asymmetry, and hard
  survival spaces; it is not blanket marble.
- Monumental and ancient materials such as named Sultan fabrics, CatacombWall, Chavvah growth,
  Burnished Azzurum, Grit Gate fabric, and Yd fabric are retained-site/recovered materials, not
  ordinary manufacture.

Creed/body overlays must alter topology, use, access, or anchors where the people require it.
Colour alone is not a creed variant. Robot quarters need charge/service clearances rather than
beds; large-bodied residents need wider portals and turns; wet-bodied residents need appropriate
water circulation. Every supported creed needs at least one functioning, recognisable unique plan
or overlay before “creed architecture” is claimed complete.

## 7. Materials and craft rungs

Placed architecture must agree with the exact paid bill and available knowledge.

- **Hands:** mud, brush/canvas, hide, rough timber, thatch/plant, local grown fungus, dry rubble,
  carved local rock, dirt and truly local salt paths.
- **Salvage:** hands-built shell plus sparse intact ruin pieces, conduit, hardware, or a retained
  machine. Scrap does not become a complete verdigris building.
- **Workshop:** shaped timber, fired fabric, dressed masonry, iron hardware, workbenches, limited
  welded frames, ordinary pipes and doors.
- **Foundry:** coherent metal/concrete service fabric and powered machinery when their real inputs
  and knowledge exist.
- **Arclight:** fulcrete/ebon/cyber/crysteel/security/glass/ring-gate systems where research,
  inputs, power, and plan support them.

Vanilla loot tier is not a manufacturing permission. `Limestone` is natural rock, Fulcrete is not
raw shaped stone, Verdigris is not generic scrap, and MetalWall is welded sheet. Material palette,
road palette, and retained-site palette are separate decisions.

## 8. Furniture and function

Required functional fixtures are authored, deterministic, reachable, and paid. Optional decor
appears only at semantic slots using a LotId-keyed deterministic draw.

- Dwellings have the required sleeping/storage/heat relation and a clear entrance.
- Workshops have work surface, inputs, outputs, hazard clearance, and service/loading access.
- Civic rooms face seating and circulation toward their counter, dais, or focal object.
- Water works place collection, taps, empty vessels, inlet/outlet, and service access coherently.
- Storage separates dry, wet, secure, and loading roles.
- Farms use crop-row and path anchors rather than scatter.
- Large public or dangerous plans have adequate exits and width.

Every container and `LiquidVolume` blueprint is audited. A furnishing that spawns goods or water
cannot become settlement stock for free. Lighting follows entrances, corners, and work surfaces;
it is not a random blanket roll.

Map-state signs derive from gameplay authority, not cosmetic flags: assigned/queued construction,
strike and repair effort, the wear ladder, deprivation at the city heart, real power brownout, and
staff effectiveness. Each sign has a distinct glyph or vanilla silhouette and an examine label;
color is redundant. The versioned legend/hash plus a read-only audit of actual ground accompany
native screenshots. Automated uniqueness and source tests do not substitute for the human verdict.

## 9. Roads, networks, verticality, and inherited layouts

Roads target authored public/service entrances and remain a separate terrain + route-role + tech
palette. Mature-city solvers must not omit entrances through a small arbitrary plot cap. Markets,
caravan courts, gates, and monumental axes may require two-cell clearance.

Gatehouses are multi-cell wall connectors with a passable gate and guard space. Liquid crossings
preserve fresh/brine topology without accidental junctions. Network pieces author straight,
corner, tee, crossing, and termination forms. Water and brine are visually distinct.

Delves and shafts create safe paired travel endpoints and real player/porter traversal; recording
a below-zone string is not physical verticality. Deep plans have authored landing, work face,
support, spoil, light, and service topology.

Inheritance seals the plan, tier, frozen snapshot, roads, and stable layout facts needed to rebuild
bounded ruins. It reconstructs legible streets and authored structures with explicit degradation,
never old people, items, liquids, or charge. One centre object on blank ground is not inherited
layout fidelity.

## 10. Scope repair register

The companion `BRIEF-IMPLEMENTATION-AUDIT.md` owns evidence and prioritisation. These families are
release-blocking for this pass because public or locked briefs currently claim them:

1. authored building/tier maps and typed lots;
2. material/technology-true construction;
3. distinct style/creed architecture;
4. passable roads/gates and real vertical travel;
5. faithful inherited layout reconstruction;
6. honest, purposeful megastructures and commitment preview;
7. grievance-based raid incidents with no pre-contact plunder;
8. human lived-city, visual, UI/accessibility, performance, and compatibility proof.

P1 semantic debts remain required work after the substrate: culture/species research sources,
the promised extension API dimensions, exact fine-house/notable semantics,
physical succession rite/founder shrine, and gatherings that use authored loci. They must be fixed
or explicitly and publicly narrowed; they may not remain stronger in prose than in runtime.

The locked salvage expedition is now implemented: one founder-visited journal destination, one
named resident/body binding, exact dedicated water and food, a prepared body receipt before any
physical callback, bounded deterministic world-time resolution, and one dated homecoming/Chronicle
result. Its remaining gate is the native Pass 38 playtest, not another proxy implementation.

`VISION.md` owns the canonical world-presence boundary; `V1-POLITY-SCOPE.md` expands its evidence
and reopening gates. Inherited site/history, current causal
raids, existing bounded parties, caravans, and two-city manifests are `SHIP` only at their
implemented scope. Successor/namesake people, a bounded legacy rival, diplomats/emissaries,
generalized visible traffic/correspondence, witnessed polity clashes, a third city, and fully
hosted zone-spanning arcology ground are positive `AUTHOR-DEFERRED` targets. Exact old actors,
automatic ideological war, persistent unloaded parties, mass background simulation, and offscreen
conquest/loss are `REJECTED`. Current arcology and inheritance surfaces must still be named and
rendered honestly; deferred prose cannot make their absent expansions look present.

## 11. Acceptance gates

Release-candidate status cannot return until all applicable gates pass:

1. Every loaded commissionable catalogue key resolves to a behavior-bearing authored tier or an explicitly typed
   network piece. No shipped plot uses a generic shell fallback.
2. Every reachable type x size binding has a plan; every plan/tier/variant compiles within caps.
   The external gate mirrors the a2 binary codec rather than estimating XML text: current maximum
   is 6,324 bytes / 8,500 characters under the bounded 8,192 / 11,264 envelopes.
3. Static topology proves public/service ingress, main access, every required use cell, exit/width,
   layer exclusivity, roof/sky rules, and lot/frontage fit in every pose.
4. Static material proof ties every structural and required fixture blueprint to paid inputs,
   craft rung, knowledge, power, and retained-site authority.
5. Snapshot codecs are canonical, bounded, hashed, tamper-detecting, and fail closed on unknown
   versions. Upgrade deltas preserve stable state and touch only exact owned slots.
6. Plot labour tests cover no hands, partial/full hands, capability/infrastructure shortfall, long
   absence, clock reversal, and no idle banking. Legacy saves keep their old path.
7. Roads reach declared entrances; gate, conduit crossing, mirror gate, and vertical endpoints pass
   traversal and save/reload tests.
8. Every unique plan x tier x topology x facing has a deterministic golden. Every shipped visual
   variant has a force-build gallery receipt with mod/game version, snapshot hash, screenshot, and
   human verdict.
9. Terrain/style samples and every supported creed/body overlay are reviewed at native tile scale.
10. Cold-save tests interrupt every construction/delta/strike/restake phase. Protection tests use
    occupied yards, non-empty containers, liquids, residents, and player objects.
11. Canonical lived-day, dense-city, succession, inheritance, raid, and full UI/accessibility
    passes complete with logs and visual evidence. Dense 80x25 city stays within performance budget.
12. Exact native compile/tests, portable tests, XML/asset/ABI gates, representative mod stack,
    frozen Workshop package, cold subscribed install, and deployment receipt all pass.

ASCII goldens catch drift, not beauty. Automated smoke catches crashes, not usability. Both are
required; neither substitutes for human visual and lived-city acceptance.
