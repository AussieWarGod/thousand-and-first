# Art policy and provenance

The release build ships **no original runtime bitmap sprites**. Every `Tile=` in this mod
references an asset already supplied by Caves of Qud; the game art itself is never copied into
this repository or the Workshop package. Objects without a tile use an intentional text glyph,
as many vanilla objects do.

This is a hard release boundary, not a claim about old commits. Pre-release history contains
custom sprite drafts made during assisted development. Those source grids and compiled PNGs were
retired before public packaging and are absent from the staged inventory. `check_wiring.py`
enforces that boundary and also proves every vanilla path occurs in the installed base game's XML
corpus, catching misspellings without unpacking or redistributing its art.

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
