#!/usr/bin/env python3
"""Compile hand-authored sprite sources into Caves of Qud tiles.

Qud tiles are 16x24 RGBA PNGs drawn in two tones. The renderer recolours them at
runtime: white pixels take the object's TileColor, black pixels take its DetailColor.
A sprite is therefore not a picture but a mask - every cell is one of three choices -
which is why the sources here are plain text grids that a person can read, diff, and
argue with.

Source format (Art/src/<name>.tile):
    lines beginning '#!' are metadata, everything else is 16 columns x 24 rows of
        '.'  transparent
        'O'  main tone   -> white  -> recoloured to TileColor
        'x'  detail tone -> black  -> recoloured to DetailColor

Usage:
    python3 build_tiles.py            compile every source to Textures/
    python3 build_tiles.py --preview  also write magnified previews to Art/preview/
"""

import os
import sys
import struct
import zlib

TILE_W = 16
TILE_H = 24

MAIN = (255, 255, 255, 255)
DETAIL = (0, 0, 0, 255)
CLEAR = (0, 0, 0, 0)

GLYPHS = {".": CLEAR, " ": CLEAR, "O": MAIN, "x": DETAIL}

HERE = os.path.dirname(os.path.abspath(__file__))
SRC_DIR = os.path.join(HERE, "src")
OUT_DIR = os.path.join(os.path.dirname(HERE), "Textures", "ThousandAndFirst")
PREVIEW_DIR = os.path.join(HERE, "preview")


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


def parse(path):
    """Read a .tile source into a pixel list, rejecting anything off-grid.

    Sloppy dimensions are a hard error rather than a silent pad: a tile that is
    one row short renders shifted, which is the kind of defect that survives
    review because it still looks like a sprite.
    """
    rows = []
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.rstrip("\n").rstrip("\r")
            if line.startswith("#!"):
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
            if glyph not in GLYPHS:
                raise ValueError(f"{os.path.basename(path)}: row {y + 1} col {x + 1}: unknown glyph {glyph!r}")
            pixels.append(GLYPHS[glyph])
    return pixels


GROUND = (12, 12, 16, 255)
SAMPLE_MAIN = (200, 160, 80, 255)
SAMPLE_DETAIL = (92, 64, 36, 255)
SILHOUETTE = (235, 235, 235, 255)
GUTTER = (40, 40, 48, 255)


def preview(pixels, scale=8, main=SAMPLE_MAIN, detail=SAMPLE_DETAIL):
    """Magnify two views side by side: the tile as played, and its silhouette.

    An earlier version of this drew detail pixels as mid grey on a grey
    transparency checkerboard, which hid every detail pixel and made a broken
    sprite look clean. A preview that flatters the work is worse than none, so
    this renders on the game's near-black play field in representative colours.
    The silhouette panel is beside it because shape is what reads at 16x24, and
    shape is the thing colour will hide problems in.
    """
    gutter = scale
    panel = TILE_W * scale
    width, height = panel * 2 + gutter, TILE_H * scale
    out = []
    for y in range(height):
        for x in range(width):
            if x >= panel and x < panel + gutter:
                out.append(GUTTER)
                continue
            col = (x if x < panel else x - panel - gutter) // scale
            px = pixels[(y // scale) * TILE_W + col]
            if px[3] == 0:
                out.append(GROUND)
            elif x < panel:
                out.append(detail if px == DETAIL else main)
            else:
                out.append(SILHOUETTE)
    return width, height, out


def main():
    want_preview = "--preview" in sys.argv
    os.makedirs(OUT_DIR, exist_ok=True)
    if want_preview:
        os.makedirs(PREVIEW_DIR, exist_ok=True)

    if not os.path.isdir(SRC_DIR):
        print(f"no sources at {SRC_DIR}")
        return 1

    built = 0
    for name in sorted(os.listdir(SRC_DIR)):
        if not name.endswith(".tile"):
            continue
        stem = name[:-5]
        pixels = parse(os.path.join(SRC_DIR, name))
        write_png(os.path.join(OUT_DIR, stem + ".png"), TILE_W, TILE_H, pixels)
        if want_preview:
            width, height, big = preview(pixels)
            write_png(os.path.join(PREVIEW_DIR, stem + ".png"), width, height, big)
        opaque = sum(1 for p in pixels if p[3] > 0)
        print(f"  {stem:<24} {opaque:>3}/{TILE_W * TILE_H} pixels")
        built += 1

    print(f"{built} tiles -> {OUT_DIR}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
