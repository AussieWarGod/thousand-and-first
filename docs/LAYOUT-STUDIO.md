# Layout studio

Local authoring and room review for [#234](https://github.com/AussieWarGod/thousand-and-first/issues/234).
It uses the existing architecture XML parser, palette/pose resolver and installed blueprint shapes.
It does not replace the architecture checker, material bills, live room mechanics or native tests.
The gameplay work is [#233](https://github.com/AussieWarGod/thousand-and-first/issues/233); all-building
design quality remains [#229](https://github.com/AussieWarGod/thousand-and-first/issues/229).

## Start from a building's program

Before drawing, write down who uses the building, their activities, how many people use each
space, where they enter, where goods arrive, and which activities need privacy or separation.
Arrange rooms, usable shared space, storage and work around those needs. Account for neighboring
uses and three-cell public streets. See [HOUSING-AND-POPULATION.md](HOUSING-AND-POPULATION.md).
Room area is evidence to design with, not a target that makes an arbitrary rectangle good.

## Interactive authoring

```bash
python3 Tools/layout-studio.py --qud-base '/path/to/Caves of Qud/CoQ_Data/StreamingAssets/Base'
```

Open the printed `http://127.0.0.1:PORT/` address. Stop the server with Ctrl+C in its terminal.
The browser edits drafts only; the server has no source-writing endpoint. Export before closing
the tab. Without `--qud-base`, unresolved physical properties are explicitly unproved and cannot
close a room just because XML declares a wall or cover.

1. Find a building/size/variant. Compare its source layout with the editable draft.
2. Paint existing glyphs, resize, or select two corners with the room tool. Add or edit glyph
   definitions in the ordinary XML when a new door, fixture or material reference is needed.
   Room drawing replaces the selected draft area; undo/redo retains earlier drafts.
3. Inspect physical boundaries, exposed interior/sleep places, free floor, room partitions,
   furniture roles and fixture access. Change facing to inspect the resolved pose. Coordinates
   in hover details stay tied to the unrotated source so editing a rotated view is unambiguous.
4. Export draft XML and its report. A bigger map reports the smallest suitable lot and whether
   it fits the selected binding. It does not silently enlarge the construction contract.
5. Apply the design to the authored source, palette, size binding and material/upgrade bill as
   one reviewed change. Generated maps must be changed through their source/generator, never
   by installing an exported generated map as an unexplained override.

The example `Tools/layout-examples/enclosed-canvas-room.xml` is a non-runtime 8×6 shared-room
draft: three sleep places, side storage, seating and a real timber door. It demonstrates the
editor and room test. It has no commissioned binding, paid bill or native acceptance and does
not establish privacy for three occupants sharing one room.

## Repeatable command-line review

```bash
python3 Tools/layout-studio.py --qud-base '/path/to/Base' \
  --map housing-tentrow-s1 --output-dir /tmp/tentrow-source-review

python3 Tools/layout-studio.py --qud-base '/path/to/Base' \
  --map housing-tentrow-s1 --draft Tools/layout-examples/enclosed-canvas-room.xml \
  --output-dir /tmp/canvas-room-draft-review

python3 Tools/layout-studio.py --qud-base '/path/to/Base' \
  --census-output /tmp/building-room-census.json
```

Outputs must be new. Selected review exports `plan.svg`, `review.json` and `draft.xml`. The
SVG is a vector plan with a semantic legend; no Qud artwork is copied. The census covers all
344 currently bound configurations and four poses each. Open sleep places flag required review,
not automatic condemnation of intentional outdoor/cultural accommodation.

Structural walls use resolved solid Structure-layer placements. Native doors separate spaces
while being assumed openable for draft circulation. Furniture may block access but does not
pretend to be enclosing fabric. Room area excludes boundary cells; clear floor also excludes
fixtures and blocked cells. Yard padding never increases interior space per sleep place.
Sleep places use the existing palette sleep-provider contract; live condition, actual occupancy,
body requirements, locks and terrain outside the draft still require engine behavior tests.
No score, privacy tier, mood or production bonus is granted by this tool.

`source_sha256` identifies normalized source map XML for the comparison. It is not a native
profile seal and does not bind the entire palette, catalogue, engine or saved city.

## Checks

```bash
Tools/dev-check.sh tools layout_studio_test.py

node Tools/layout-studio-browser-check.cjs /path/to/playwright-core \
  /path/to/chromium http://127.0.0.1:PORT/ /tmp/fresh-layout-browser-result
```

The browser check uses explicitly supplied local tools, opens only the owned loopback studio,
and closes its browser in `finally`. It exercises source/draft comparison, wall loss, undo/redo,
rotation, resize, room drawing, door/bed placement, XML download, malformed refusal and draft
retention. Its result is authoring acceptance, not Qud gameplay acceptance.

After applying a runtime design, run the existing generator freshness, architecture/material/
transition checks, relevant pure cases and engine gate. Then commission it through ordinary
in-game flows, prove the required activities and failure/recovery, and perform save/fresh load
where applicable. Keep costs, old jobs and existing citizen/root/storage identities intact.
