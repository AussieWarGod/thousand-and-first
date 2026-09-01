# Art policy and provenance

The current build uses verified vanilla tiles and intentional glyphs; it contains zero custom
runtime rasters. Vanilla-first is a quality choice, not a blanket ban. A future original sprite
must be listed in `runtime-assets.json` with exact creator/date/license/source/method/fallback/
review metadata and SHA-256, wired from staged XML, packaged at the exact path, and independently
reviewed in Qud at tile and text scale. Copied, extracted, traced, or edited Qud art never ships.

Pre-release history contains custom drafts which failed the present quality/provenance boundary.
They remain retired. `check_wiring.py` enforces both the local allowlist and vanilla reference
corpus without unpacking or redistributing game art.

Run the audit from the repository root:

```bash
python3 Art/check_wiring.py
```

Set `TAF_QUD_BASE` when the game is not installed in the default WSL location. It must name the
game's `CoQ_Data/StreamingAssets/Base` directory.

## Retired-draft replacements

| Mod object | Vanilla tile reference | Visual role |
|---|---|---|
| scaffolding | `Items/sw_fence_gates_2_open.bmp` | spare timber frame |
| cask rack | intentional `≡` glyph | stacked casks in a rack |
| great cistern | `Items/sw_catchbasin.bmp` | open water vessel |
| shrine stone | `Terrain/tile_tombstone1.png` | shrine stone |
| charging post | `Items/sw_universal_station.bmp` | charging cradle |
| thorn palisade | `Walls/wall_brinestalk-00000000.png` | lashed organic wall |
| watchtower | `Terrain/sw_historic_tower.bmp` | tower silhouette |
| stone rampart | `Tiles/wall_rock-00000000.bmp` | rough stone wall |
| founding book | `Items/sw_book_1.bmp` | bound codex |
| vat-house | `Items/sw_alchemist_table.bmp` | vessels and work surface |
| grafting hall | `Items/sw_bed_medical.bmp` | surgical bed |
| chimeric theatre | `Items/sw_table_cylinder.bmp` | central round table |
| becoming annexe | `Items/sw_regen_tank.bmp` | transformation chamber |
| crown hall | `Items/sw_chair_throne.bmp` | throne |
| arcology | `Tiles/sw_arch.png` | monumental local atrium gate |
| creed spindle-wheel | `Items/sw_waterwheel_1.bmp` | low-tech hand wheel silhouette |
| creed dry contact | `Items/sw_copper_wire.bmp` | fixed coil and severed, inert charging leads |
| creed horn post | `Terrain/sw_monument1.bmp` | standing challenge marker |
| creed scrap altar | `Terrain/sw_monument7.bmp` | wired chrome altar shape |
| creed arms rack | `Items/sw_weapons_rack.bmp` | empty ordered weapon rests |
| creed cold brazier | `Items/sw_firepan.bmp` | unlit ash pan |
| creed vine trellis | `Tiles/sw_watervine2.bmp` | trained fruiting vine |
| creed teaching trunk | `Terrain/sw_bigtree1.bmp` | central living trunk |
| generated creed practice hamper | `Items/sw_basket.bmp` | stitched-shut handling marker; no storage |
| generated creed practice board | `Items/sw_table_low.bmp` | bare work marker; no container |
| generated creed cold hearth | `Items/sw_campfire_noflame.png` | stone spacing marker; no fire or light |
| generated creed practice rack | `Items/sw_bookshelf1.bmp` | empty rack silhouette; no inventory |
| generated creed practice slab/rail | `Items/sw_bench.bmp` | position marker; no chair behavior |
| generated creed arms frame | `Items/sw_fence_gates_2_open.bmp` | bare timber peg-frame; no rack or inventory behavior |
| generated creed dry basin | `Items/sw_regen_tank_broken2.bmp` | breached shell; no liquid or collection |
| generated creed rolled pallet | `Items/sw_scroll1.bmp` | bound canvas roll; no bed behavior |
| timber/stone civic lecterns | `Items/sw_table_low_drawers.bmp`, `Items/sw_table_cylinder.bmp` | fixed reading surface or geometric sanctum stand; never the similarly named beak mutation |
| great-court rostrum | `Items/sw_table_ornate_1.bmp` | worked civic speaking table; never the similarly named beak mutation |
| deep-bore cutting head | `Creatures/natural-weapon-drill.bmp` | exposed drill cone in an inert installed collar; never the portable spiral-borer satchel |
| stilling projector | `Items/sw_forceprojector.bmp` | installed field-projector chassis with all vanilla field and power behavior omitted |

Colour remains mod-owned metadata (`TileColor` and `DetailColor`), so the same vanilla silhouette
can still carry a settlement's identity without bundling a derivative image.
