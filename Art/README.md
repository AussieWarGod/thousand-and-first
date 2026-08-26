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
| arcology | `Terrain/sw_historic_houses.bmp` | dense built skyline |

Colour remains mod-owned metadata (`TileColor` and `DetailColor`), so the same vanilla silhouette
can still carry a settlement's identity without bundling a derivative image.
