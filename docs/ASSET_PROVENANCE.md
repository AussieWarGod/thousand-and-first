# Asset Provenance

## Current release boundary

Current runtime asset trees contain no mod-authored bitmap sprites. Every `Tile=` value in shipped
XML is a path to art supplied by Caves of Qud; the referenced game art is not copied into this
repository or Workshop package. Objects may instead use an intentional text glyph. A separately
reviewed root `preview.png` may enter a release only as Workshop presentation media; XML may never
reference it and it is not a runtime sprite-policy exception.

Retired custom sprite drafts and their generated PNGs are absent from current source/runtime
inventory. Older private or Git history may contain drafts; history is not provenance for a new
submission and those files must not be restored.

Verify current boundary from repository root:

```bash
TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" \
  python3 Art/check_wiring.py
./Tools/stage.sh list | grep -Ei '\.(png|bmp|gif|jpe?g|webp|tga|tiff?|dds)$' \
  | grep -vx 'preview.png' && exit 1 || true
```

`Art/check_wiring.py` proves there are no bundled runtime rasters or local tile paths and that
each referenced vanilla path is named by installed base-game XML. It reads local installation
metadata; it does not extract or redistribute art.

## Contributions

Current policy accepts:

- a verified vanilla `Tile=` path already supplied by Qud, referenced as text only; or
- an intentional `RenderString`/color treatment consistent with vanilla behavior.

Do not submit copied, traced, recolored, edited, upscaled, or extracted Qud art. Do not submit
third-party art without an explicit compatible license and source record. Do not submit
AI-generated or generative-image-assisted raster art. Current release policy rejects bundled
runtime bitmap sprites even when independently authored; propose any policy change before making
assets.

When adding or changing a vanilla reference, record in the pull request:

- object/blueprint and exact tile path;
- installed Qud marketing version and core build used to verify it;
- why the silhouette fits the object's function at 16×24 presentation;
- `TileColor`, `DetailColor`, `RenderString`, and occlusion behavior;
- `python3 Art/check_wiring.py` result; and
- an in-game screenshot or explicit statement that live visual proof remains pending.

Never include game screenshots in runtime asset trees or reference them from XML. Screenshots used
in issues or review may show Qud for compatibility evidence and must be cropped/redacted. A root
Workshop preview is the sole release exception: it may be a purpose-captured screenshot of this
mod running in Qud, with the capture/build/save/crop record below. It may not be repurposed as a
sprite or general source asset.

## Future original assets

If maintainers first approve changing the no-bitmap policy, each original asset must have a
reviewable record naming creator, creation date, tools, editable source, licenses for every input,
reference material, transformations, output path, and contributor's rights attestation. Generated
output must be reproducible where practical. “Made by contributor” or a prompt alone is not a
provenance record.

No original or third-party asset may land until licensing, source, runtime wiring, dimensions,
palette, contrast, and live in-game readability are reviewed independently.

## Workshop preview

No Workshop preview image is currently committed. Until one lands, `manifest.json` must not name
one and Workshop packaging must fail closed. A release preview must be exactly 512 by 512 pixels,
8-bit RGB or RGBA non-interlaced PNG, and under 1,000,000 bytes.

Preferred source is a new in-game screenshot showing this mod's tested settlement UI and map, not
an extracted game asset or a collage of copied tiles. Record creator/captor, capture date, exact
Qud marketing/core build, source save or fresh-world procedure, mod commit, original screenshot
hash, crop/redaction/color-only transformations, output hash, and live readability review. Keep
the original evidence outside runtime staging. Do not use AI-generated or generative-image-assisted
material. Do not imply Freehold Games endorsement.
