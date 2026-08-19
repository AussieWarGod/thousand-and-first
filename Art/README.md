# Art — hand-authored sprites

Tiles in this mod are written, not drawn. Each sprite is a text grid under `src/` that
compiles to a Caves of Qud tile with `build_tiles.py`. Sources are reviewable in a diff,
which a PNG is not: when a sprite changes, you can see *which pixels* and argue about it.

## The format, and why it makes this tractable

A Qud tile is a 16×24 RGBA PNG in two tones. The renderer recolours it at runtime — white
pixels take the object's `TileColor`, black pixels take its `DetailColor` — so a sprite is
not a picture but a mask. Every one of the 384 cells is one of three choices:

| Glyph | Meaning |
|---|---|
| `.` | transparent |
| `O` | main tone → white → recoloured to `TileColor` |
| `x` | detail tone → black → recoloured to `DetailColor` |

Lines beginning `#!` are notes and are ignored. Anything that is not exactly 24 rows of 16
columns is a hard error: a tile one row short still renders like a sprite, just shifted, and
that is the kind of defect that survives review.

## Working on a sprite

```bash
python3 Art/build_tiles.py --preview
```

Compiled tiles go to `Textures/ThousandAndFirst/`, which the game loads directly. Previews go
to `Art/preview/` (gitignored) and are the point of the exercise: each one shows the tile
twice, as played on the game's near-black field in representative colours, and beside it as a
pure silhouette.

**Look at the preview. Every time.** An earlier version of this script drew detail pixels as
mid grey on a grey transparency checkerboard, which hid every detail pixel and made a broken
sprite look finished. A preview that flatters the work is worse than no preview.

## What 16×24 teaches you, learned the hard way

- **Silhouette first.** Shape is what reads at this size; colour hides problems in it. That is
  why the preview shows the silhouette panel next to the coloured one.
- **Texture must never out-mass its subject.** The palisade's second draft filled the gaps
  between stakes with thorn detail. Past roughly half the cell, figure and ground swapped and
  the stakes started reading as holes.
- **Regular spacing reads as manufactured.** Evenly pitched horizontal ticks turned the same
  palisade into a trellis — which is a fence that invites climbing. Uneven stake heights and
  irregular upward spurs turned it back into something hostile.

## Referencing a tile

Paths in `ObjectBlueprints.xml` are relative to `Textures/`. Both `.png` and `.bmp` resolve;
use `.png`, which is what these files actually are.

```xml
<part Name="Render" Tile="ThousandAndFirst/r_KingdomPalisade.png"
      TileColor="&amp;y" DetailColor="K" />
```

`TileColor` takes an `&`-prefixed code; `DetailColor` takes a bare colour character.

## Extending

Third-party mods do not need this pipeline to retheme our objects — they override the
blueprint's `Tile` like any other. The pipeline exists so *our* sprites stay reviewable, and
it is offered as a pattern rather than a dependency.
