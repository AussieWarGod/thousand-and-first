# Architecture quality audit

**Audit snapshot:** `b2ade1aa7d91609d5da9a0a347acc51870f580cfdcb9dbb8db5ea343b20a44e4`
(digest of the building and architecture XML inputs). This is the current static release gate, not
a visual sign-off. Regenerate the ledger after any architecture edit.

## What “good” means here

The primary reference is the installed, running Caves of Qud **2.0.211.51** corpus. This audit
verified its assembly, population tables, object-blueprint corpus, and ten named settlement or
institutional maps
against `Tools/architecture-quality-reference.json`.

Vanilla supplies the minimum architectural grammar, not an aesthetic to copy:

- `Village_InitialStructureSegmentation_*Default` varies blocks, rings, towers, BSP partitions,
  and full-zone compositions instead of repeating one box;
- `Village_StructureWall_*Default` and `Village_VillageWallStyle_*Default` establish a broad,
  settlement-level material and silhouette vocabulary;
- `VillageBase`/`Village` place real perimeter doors, distinguish inside/outside/along-wall
  furnishing areas, place function-specific contents, carve cardinal paths to the center, and
  rebuild reachability after construction;
- `RoadBuilder` makes pathfinder-connected surfaces. A painted line that does not reach the
  threshold is not a road;
- Joppa, Kyakukya, Bey Lah, Ezra, Grit Gate, Yd Freehold, Mopango, Chavvah, the Six Day Stilt, and
  the Crematory Machine Room are the native comparison set for human review.

The secondary rubric is ordinary architectural legibility at Qud scale: public threshold,
circulation, purpose zoning, storage/service/hazard separation, coherent era and material history,
culture/body/terrain fit, meaningful renovation or addition, and readable road frontage. XML
validity never signs lighting, tile joins, affordance, composition, or beauty.

Each ledger case therefore has three independent verdicts:

1. `static_verdict`: schema, program, pathing, and exact dossier checks;
2. `native_view`: actual Qud rendering and interaction proof;
3. `human_acceptance`: a person judges the pose at play scale.

## Exact census and result

The machine ledger is `docs/release-evidence/architecture-quality-ledger.json`.

| Scope | Count |
|---|---:|
| Catalogue buildings | 144 |
| Plotted buildings | 134 |
| Palettes | 89 |
| Architecture maps | 333 |
| Authored/source maps | 187 |
| Generated maps | 146 |
| Plans | 220 |
| Bindings | 226 |
| Tiers | 262 |
| Resolved tier/variant configurations | 344 |
| Four-pose cases | 1,376 |
| Static pass poses | 1,376 |
| Static fail poses | 0 |
| Native-view required | 1,376 |
| Human acceptance pending | 1,376 |
| Auxiliary network/road/yard cases | 18 |

The architecture checker reports **0 issues**. Its largest exact `a4` receipt is **7,798 bytes /
10,468 characters** (`greatfoundry/craft-xl/templar/purpose-greatfoundry-templar-xl0` with
`purpose-forge-foundry`). With the installed Qud corpus it also reports three
non-blocking tolerant-parse notices for vanilla `Creatures.xml`, `Furniture.xml`, and `Items.xml`;
all 5,800 resolved blueprint names remain checked. The reference receipt is hash-verified.

The 18 auxiliaries are all ten non-plot catalogue works, four road surfaces, and four hosted yard
works. This auxiliary row is a declaration census, not a spatial or visual proof; all still require
native/human review. Hosted
arcology ward and terrace maps are in the 1,376-pose census. The separate multi-zone arcology
topology still needs its own in-game traversal receipt.

## Closed static blockers

The current corpus now passes the previously recorded release blockers:

- every bound map has its canonical plot dimensions: S 6x4, M 8x6, L 12x10, XL 20x18;
- generated realizations use the full lot as an authored, size-specific composition instead of
  parking a smaller core in blank padding;
- every generated public threshold has visible claimed circulation to a lot edge; the shared
  checker/audit contract keeps a tested legacy empty-approach fallback for intentional authored
  maps;
- exact required anchors are usable, including the arcology ward service core and condensery cold
  face;
- each becoming-annexe plan uses four radius-six clean-room sconces spaced at least four cells
  apart, with exactly one durable required light; the pinned vanilla Crematory Machine Room's five
  techlights supply the stricter-than-reference fixture cap rather than an arbitrary density;
- larders, butcheries, and smelters have real dry-goods, meat, and output storage;
- terraces provide one sleep fixture and household threshold for each declared household;
- bookshelf and scriptorium archive fixtures generate persistent readable contents;
- 105 semantic benefit fixtures use 33 role-readable vanilla render signatures rather than the
  former material-only table/cabinet flattening; no signature serves more than 15 fixtures, and
  tests pin distinct fire, oven, forge, anvil, furnace, bore, cut-face, mill, wheel, windmill,
  loom, mirror-gate, crown, and arcology silhouettes;
- Eater halls use recovered machine fabric and retained light, and Eater deep-bore and great-foundry
  variants are explicitly authored;
- Eater air-well courts and fields retain a live machine readout beside their salvaged service
  layer;
- field rows can grow into a larger grange through a shipped `renovate-expand` progression with a
  transition-specific physical material bill.
- the M watch-house and L barracks now form one `renovate-expand` garrison lineage: the containing
  envelope retains the exact root while duty rooms, mess/store/service circulation, and lit muster
  are renovated; palisade/re-stood-course -> rampart remains a separate non-plot wall lineage.

The ledger reports **zero case findings**: no static errors and no taste/function warnings. This
does not replace the native-view and human-acceptance gates below.

## Compact starter housing on larger reservations

Tent, timber-hut, mud-hut, and recovered-block families have exact 3x2–5x4 physical footprints
and an invariant material/labour bill. Their M/L/XL bindings reserve future housing land; they do
not authorize extra rooms, beds, stores, hearths, walls, or households. A terminal starter shelter
on an XL lot is therefore intentionally a small building in a large yard, not an XL building.
Players can later strike it for a paid stone house, terrace, manor, or housing court; a future
in-place estate successor would require its own catalogue bill and transition.

The first generated yards over-stated that investment with 60–74 cells of repeated formal paths.
The reviewed generator now uses 25–35 visibly distinct natural-ground cells at XL and smaller
proportional courts at M/L: canvas gets a compact dooryard, timber a return court, mud a drying
apron, and recovered block an angular swept corner. The one exact frontage route remains direct;
all original stateful fixtures and footprint cells remain byte-for-byte positioned; generated
housing still adds zero fixtures. Tests pin the bounded path density, multi-region legibility,
family-distinct XL silhouettes, and unchanged material custody. Native review must still decide
whether each court reads well at play scale.

## Creed-campus material and composition gate

Creed coverage contains 33 admitted factions, 34 designs, and 132 applicable `BuildKey`×size
records; Robots contribute two tiers. Not every design expands. The programmed larger-lot subset
reuses one exact `BuildKey` and material bill at every supported lot size. That bill pays for the
authored 6x4 sanctum once. A larger binding therefore does **not** stretch or repeat its walls,
containers, beds, fires, liquids, utilities, or stateful anchors. Doing so would mint structural or
functional fabric that was never paid for. Larger sites instead use plausible low-material ground
as a named creed campus: courts, work or cultivation bands, gardens, muster lanes, service pads,
and connected circulation furnished only with inert silhouettes derived from the sanctum's
practice roles.

`Tools/generate-lot-realizations.py` now treats taste as a generation failure, not a gallery note:

- exactly 30 reviewed and uniquely named creed programmes cover 31 source tier maps and produce
  93 generated M/L/XL maps;
- every programme chooses one of four legible station formations and bounded authored axis,
  court, handedness, and rhythm facts; incidental binding/receipt identity cannot reroll it;
- M/L/XL place exactly 2/5/10 inert fixtures: 527 fixtures in 248 deliberate pairs overall, with
  no copied functional anchor and no extra stateful object;
- fixtures are directly path-side, span both lateral halves at every size, span at least two depth
  bands at L and all three at XL, and occupy at least 2/4/5 programme regions respectively;
- a campus may reserve no more than 36 new site cells per fixture, so a tiny sanctum plus
  decorative scatter cannot justify an otherwise empty lot;
- yard and path resolve to visibly different lawful palette ground, remain connected to the exact
  public threshold, and use named category/creed geometry rather than hash-selected paving nubs.

All 90 larger-size bindings covered by those programmes meet that contract, so none is omitted.
The generator fails closed when a future creed lacks a reviewed programme or cannot compose the
required regions; such a larger binding must be deliberately omitted or newly authored rather
than padded. This static gate proves composition and material custody, not beauty at native tile
scale; the human gate remains open.

## What remains a human decision

All 1,376 poses remain `needs-native-view`, including static passes. Native review must judge wall
joins, door state, fixture identity, lighting contrast, material colour hierarchy, one-cell
silhouette, body-scale clearance, road arrival, and whether a larger realization reads as a real
renovation/addition rather than algorithmic fill. Named vanilla RPM files are hash-pinned reference
targets, but this tool does not pretend to visually parse their binary map format.

## Reproduction

```bash
python3 Tools/audit-architecture-quality.py \
  --repo-root . \
  --qud-base '/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base' \
  --output docs/release-evidence/architecture-quality-ledger.json

python3 -m unittest Tools.tests.architecture_quality_audit_test
```

The test pins all 144 catalogue entries, all 134 plotted entries, 89 palettes, 333 maps (187
authored/source and 146 generated), 220 plans, 226 bindings, 262 tiers, 344 configurations, four
poses per configuration, 1,376 total poses, all 18 auxiliary cases, zero static failures, verdict
consistency, and the exact 2.0.211.51 reference identity. Native and human acceptance remain
pending for every pose.
