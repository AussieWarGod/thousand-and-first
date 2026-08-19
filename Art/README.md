# Art — hand-authored sprites

Tiles in this mod are written, not drawn. Each sprite is a text grid under `src/` that
compiles to a Caves of Qud tile with `build_tiles.py`. Sources are reviewable in a diff,
which a PNG is not: when a sprite changes you can see *which pixels* and argue about it.

## The format

A Qud tile is a 16×24 RGBA PNG in two tones, recoloured at runtime. **Black is the body and
takes `TileColor`; white is the highlight and takes `DetailColor`.** The renderer settles
this: source pixels with red < 0.5 go to the foreground colour, > 0.5 to the detail colour.

So a sprite is not a picture but a mask. Every one of the 384 cells is one of these:

| Glyph | Meaning |
|---|---|
| `.` | transparent |
| `#` | body → black → recoloured to `TileColor` |
| `o` | highlight → white → recoloured to `DetailColor` |
| `d` / `D` | checker dither, body↔highlight — an opaque mid-tone (two phases) |
| `s` / `S` | stipple dither, body↔transparent — a translucent half-tone (two phases) |

Lines beginning `#!` are notes. `#! category: wall|furniture|creature|item` picks which
measured targets the tile is checked against. Anything that is not exactly 24 rows of 16
columns is a hard error: a tile one row short still renders like a sprite, just shifted, and
that is the kind of defect that survives review.

## Working on a sprite

```bash
python3 Art/build_tiles.py --preview --sheet
```

Compiled tiles go to `Textures/ThousandAndFirst/`, which the game loads directly. Previews go
to `Art/preview/` (gitignored) and are the point of the exercise: each shows the tile as
played on the game's near-black field beside its pure silhouette, and `_sheet.png` lays the
whole set out together, because a set only looks like one hand if stroke weight and detail
agree across it.

**Look at the preview. Every time.** An early version of this script drew highlight pixels as
mid grey on a grey transparency checkerboard, which hid every one of them and made a broken
sprite look finished. A preview that flatters the work is worse than no preview.

## Measured targets

From 6,741 cells of the game's own atlases. The compiler reports every tile against its
category and flags outliers.

| Category | Coverage | Highlight share |
|---|---|---|
| wall / furniture | ~59% | ~15% |
| creature | ~21% | ~10% |
| item | ~16% | ~24% |

The three draw nothing alike. Walls fill the cell and lean on dither; creatures are small
solid silhouettes with a few bright accents; items are smaller still but spend far more on
highlight, because an item is usually two materials — a pale blade on a dark haft. An item
drawn at furniture coverage is three times the size of everything else on the ground.

## What 16×24 teaches you, learned the hard way

- **Silhouette first.** Shape is what reads at this size; colour hides problems in it.
- **Texture must never out-mass its subject.** The palisade's second draft filled the gaps
  between stakes with thorn detail. Past roughly half the cell, figure and ground swapped and
  the stakes started reading as holes.
- **Stipple is how a surface gets lighter**, not highlight. One sampled vanilla wall cell is
  49% covered with *no white pixels at all*. Missing this is what makes hand-made tiles read
  flat and overbright — the cistern was a solid brown boulder until its water was stippled.
- **Highlight is for structure only** — course tops, lit rims, the edge of a stake. Scattered
  single highlight pixels read as dirt.
- **Regular spacing reads as manufactured.** Evenly pitched ticks turned the palisade into a
  trellis, which is a fence that invites climbing.
- **Draw the thing, not a thing near it.** The shrine's hollow sat *on top* of the stone and
  read as an urn with a lid until it was cut into the crown.

## Reuse before you draw

Most objects need no art. Resolve the blueprint's inheritance chain first — the bunk and bench
already land on `sw_bed` and `sw_chair`, settlers reach `sw_farmer` through `BaseFarmer`, and
the founder's basin uses `Items/sw_catchbasin.bmp`, which is exactly the object it is. Draw
only what the game has no equivalent for.

## Referencing a tile

Paths are relative to `Textures/`. Both `.png` and `.bmp` resolve; use `.png`, which is what
these files are.

```xml
<part Name="Render" Tile="ThousandAndFirst/r_KingdomPalisade.png"
      TileColor="&amp;y" DetailColor="w" />
```

`TileColor` takes an `&`-prefixed code; `DetailColor` takes a bare colour character. Since
white is the highlight, `DetailColor` wants a *light* colour — setting it dark inverts the
lighting and flattens the tile.

## Extending

Third-party mods do not need this pipeline to retheme our objects — they override the
blueprint's `Tile` like any other. It exists so *our* sprites stay reviewable, and is offered
as a pattern rather than a dependency.
