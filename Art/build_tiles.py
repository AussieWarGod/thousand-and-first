#!/usr/bin/env python3
"""Compile hand-authored sprite sources into Caves of Qud tiles.

A Qud tile is a 16x24 PNG in two tones, recoloured at runtime: **black is the body
and takes TileColor; white is the highlight and takes DetailColor.**

That mapping is settled by the renderer, which sends source pixels with red < 0.5
to the foreground colour and > 0.5 to the detail colour. Two earlier versions of
this comment argued it from blueprint counts instead, and both were wrong:

    # first: "vanilla sets DetailColor zero times" - read from
    # Base/ObjectBlueprints.xml, which is a 67-byte empty stub
    # second: "2,311 times in Base/ObjectBlueprints/*.xml" - that figure is
    # every XML under StreamingAssets, DLC included, wearing the wrong scope

Counted exactly, from the StreamingAssets root:

    grep -rho 'DetailColor="[^"]*"' Base/ObjectBlueprints/*.xml | wc -l   # 2166, 11 files
    grep -rho 'DetailColor="[^"]*"' --include=*.xml Base/ | wc -l         # 2295
    grep -rho 'DetailColor="[^"]*"' --include=*.xml .     | wc -l         # 2311 (DLC adds 16)

The conclusion never depended on any of them. The renderer is the proof; the
counts only ever disproved a rationale that should not have been offered.

The proportions below *are* measured, from 6,741 cells of the game's atlases:
tiles are pure black and white, median coverage 59% of the cell, and only ~15% of
drawn pixels are white.

The glyphs below are therefore named for what they mean, not for the colour they
compile to. An earlier version of this pipeline used 'O' for white as the body
tone and produced five tiles that would all have rendered inside out.

Source format (Art/src/<name>.tile), 24 rows of 16 columns:
    '.'  transparent
    '#'  body      -> black -> recoloured to TileColor
    'o'  highlight -> white -> recoloured to DetailColor
    'd'  checker   -> body/highlight, an opaque mid-tone
    's'  stipple   -> body/transparent, a translucent half-tone
    'D'  'S'       -> the same two, offset one cell so adjacent surfaces differ

Both dithers were read out of the shipped atlases pixel by pixel. The checker is
how a third value is got from two colours; the stipple is how a surface is made
*lighter* without spending any highlight on it, and it is the one that matters -
one sampled vanilla wall cell is 49% covered with no white pixels at all. Missing
the stipple is what makes hand-made tiles read as flat and overbright.

Highlight is spent only on structure: unbroken edges, course tops, lit rims.
Never scattered, which reads as dirt.

Usage:
    python3 build_tiles.py [--preview] [--sheet]
"""

import os
import sys
import struct
import zlib

TILE_W = 16
TILE_H = 24

BODY = (0, 0, 0, 255)
HIGHLIGHT = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)

# Measured from the game's own atlases, per category, because the three draw
# nothing alike. Walls and furniture fill the cell and lean on dither; creatures
# are small solid silhouettes with a few bright accents; items are smaller still
# but spend far more on highlight, because an item is usually two materials - a
# pale blade on a dark haft, bright liquid in a dark flask.
#
# Getting this wrong is not a stylistic quibble. An item drawn at furniture
# coverage is three times the size of everything around it on the ground.
CATEGORY_TARGETS = {
    "wall": (59, 15),
    "furniture": (59, 15),
    "creature": (21, 10),
    "item": (16, 24),
}
DEFAULT_CATEGORY = "furniture"

HERE = os.path.dirname(os.path.abspath(__file__))
SRC_DIR = os.path.join(HERE, "src")
OUT_DIR = os.path.join(os.path.dirname(HERE), "Textures", "ThousandAndFirst")
PREVIEW_DIR = os.path.join(HERE, "preview")

GROUND = (14, 14, 20, 255)
SAMPLE_BODY = (150, 108, 62, 255)
SAMPLE_HIGHLIGHT = (238, 232, 220, 255)
SILHOUETTE = (235, 235, 235, 255)
GUTTER = (40, 40, 48, 255)


def write_png(path, width, height, pixels):
    """Write an 8-bit RGBA PNG without depending on an imaging library."""
    raw = b"".join(
        b"\x00" + b"".join(struct.pack("BBBB", *pixels[y * width + x]) for x in range(width))
        for y in range(height)
    )

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as handle:
        handle.write(png)


def resolve(glyph, x, y):
    if glyph in (".", " "):
        return CLEAR
    if glyph == "#":
        return BODY
    if glyph == "o":
        return HIGHLIGHT
    if glyph == "d":
        return BODY if (x + y) % 2 == 0 else HIGHLIGHT
    if glyph == "D":
        return HIGHLIGHT if (x + y) % 2 == 0 else BODY
    if glyph == "s":
        return BODY if (x + y) % 2 == 0 else CLEAR
    if glyph == "S":
        return CLEAR if (x + y) % 2 == 0 else BODY
    raise KeyError(glyph)


def parse(path):
    """Read a .tile source into a pixel list, rejecting anything off-grid.

    Sloppy dimensions are a hard error rather than a silent pad: a tile one row
    short renders shifted, which is the kind of defect that survives review
    because it still looks like a sprite.
    """
    rows = []
    category = DEFAULT_CATEGORY
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.rstrip("\n").rstrip("\r")
            if line.startswith("#!"):
                marker = line[2:].strip().lower()
                if marker.startswith("category:"):
                    named = marker.split(":", 1)[1].strip()
                    if named not in CATEGORY_TARGETS:
                        raise ValueError(f"{os.path.basename(path)}: unknown category {named!r}")
                    category = named
                continue
            if not line.strip() and not rows:
                continue
            rows.append(line)
    while rows and not rows[-1].strip():
        rows.pop()

    if len(rows) != TILE_H:
        raise ValueError(f"{os.path.basename(path)}: expected {TILE_H} rows, found {len(rows)}")

    pixels = []
    for y, row in enumerate(rows):
        row = row.ljust(TILE_W)
        if len(row) != TILE_W:
            raise ValueError(f"{os.path.basename(path)}: row {y + 1} is {len(row)} columns, expected {TILE_W}")
        for x, glyph in enumerate(row):
            try:
                pixels.append(resolve(glyph, x, y))
            except KeyError:
                raise ValueError(f"{os.path.basename(path)}: row {y + 1} col {x + 1}: unknown glyph {glyph!r}")
    return pixels, category


def measure(pixels, category):
    """Report coverage and highlight share, and how far they sit from the shipped
    art for this kind of object. Bands are proportional rather than absolute: plus
    or minus fifteen points means nothing to a wall and everything to an item."""
    opaque = sum(1 for p in pixels if p[3] > 0)
    highlight = sum(1 for p in pixels if p == HIGHLIGHT)
    coverage = 100 * opaque // (TILE_W * TILE_H)
    share = (100 * highlight // opaque) if opaque else 0
    want_cover, want_share = CATEGORY_TARGETS[category]
    flags = []
    if coverage < want_cover * 0.55 or coverage > want_cover * 1.6:
        flags.append(f"cover {coverage}% vs ~{want_cover}%")
    if share > want_share * 2.4:
        flags.append(f"highlight {share}% vs ~{want_share}%")
    return coverage, share, flags


def preview(pixels, scale=8):
    """Magnify two views side by side: the tile as played, and its silhouette.

    An earlier version drew highlight pixels as mid grey on a grey transparency
    checkerboard, which hid them and made a broken sprite look clean. A preview
    that flatters the work is worse than none, so this renders on the game's
    near-black field in representative colours. The silhouette panel sits beside
    it because shape is what reads at 16x24, and colour hides problems in shape.
    """
    gutter = scale
    panel = TILE_W * scale
    width, height = panel * 2 + gutter, TILE_H * scale
    out = []
    for y in range(height):
        for x in range(width):
            if panel <= x < panel + gutter:
                out.append(GUTTER)
                continue
            col = (x if x < panel else x - panel - gutter) // scale
            px = pixels[(y // scale) * TILE_W + col]
            if px[3] == 0:
                out.append(GROUND)
            elif x < panel:
                out.append(SAMPLE_HIGHLIGHT if px == HIGHLIGHT else SAMPLE_BODY)
            else:
                out.append(SILHOUETTE)
    return width, height, out


def sheet(entries, scale=6, pad=2):
    """Lay every tile out in one strip, coloured above, silhouette below.

    Sprites are judged against each other, not alone: a set only looks like one
    hand if stroke weight, ground line, and amount of detail agree across it.
    Reviewing tiles one at a time hides exactly that.
    """
    cell_w = TILE_W * scale + pad * 2
    cell_h = TILE_H * scale + pad * 2
    width = cell_w * len(entries)
    height = cell_h * 2
    out = [GROUND] * (width * height)

    for index, (_, pixels) in enumerate(entries):
        for y in range(TILE_H * scale):
            for x in range(TILE_W * scale):
                px = pixels[(y // scale) * TILE_W + (x // scale)]
                if px[3] == 0:
                    continue
                ox = index * cell_w + pad + x
                out[(pad + y) * width + ox] = SAMPLE_HIGHLIGHT if px == HIGHLIGHT else SAMPLE_BODY
                out[(cell_h + pad + y) * width + ox] = SILHOUETTE
    return width, height, out


def main():
    want_preview = "--preview" in sys.argv
    want_sheet = "--sheet" in sys.argv
    os.makedirs(OUT_DIR, exist_ok=True)

    if not os.path.isdir(SRC_DIR):
        print(f"no sources at {SRC_DIR}")
        return 1

    entries = []
    for name in sorted(os.listdir(SRC_DIR)):
        if not name.endswith(".tile"):
            continue
        stem = name[:-5]
        pixels, category = parse(os.path.join(SRC_DIR, name))
        write_png(os.path.join(OUT_DIR, stem + ".png"), TILE_W, TILE_H, pixels)
        if want_preview:
            os.makedirs(PREVIEW_DIR, exist_ok=True)
            width, height, big = preview(pixels)
            write_png(os.path.join(PREVIEW_DIR, stem + ".png"), width, height, big)
        entries.append((stem, pixels))

        coverage, share, flags = measure(pixels, category)
        note = ("   << " + "; ".join(flags)) if flags else ""
        print(f"  {stem:<24} {category:<9} cover {coverage:>3}%  highlight {share:>3}%{note}")

    if want_sheet and entries:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        width, height, strip = sheet(entries)
        write_png(os.path.join(PREVIEW_DIR, "_sheet.png"), width, height, strip)
        print("  sheet order: " + ", ".join(stem for stem, _ in entries))

    targets = ", ".join(f"{k} {v[0]}/{v[1]}" for k, v in sorted(set(CATEGORY_TARGETS.items())))
    print(f"{len(entries)} tiles -> {OUT_DIR}")
    print(f"  vanilla cover/highlight targets: {targets}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
