# Asset Provenance

## Current release boundary

Current runtime package contains no mod-authored bitmap sprites. Every `Tile=` value in shipped
XML is a path to art supplied by Caves of Qud; the referenced game art is not copied into this
repository or Workshop package. Objects may instead use an intentional text glyph.

Retired custom sprite drafts and their generated PNGs are absent from current source/runtime
inventory. Older private or Git history may contain drafts; history is not provenance for a new
submission and those files must not be restored.

Verify current boundary from repository root:

```bash
TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" \
  python3 Art/check_wiring.py
./Tools/stage.sh list | grep -E '\.(png|bmp)$' && exit 1 || true
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

Never include game screenshots in runtime staging. Screenshots used in issues or review may show
Qud for compatibility evidence, but must be cropped/redacted and are not reusable source assets.

## Future original assets

If maintainers first approve changing the no-bitmap policy, each original asset must have a
reviewable record naming creator, creation date, tools, editable source, licenses for every input,
reference material, transformations, output path, and contributor's rights attestation. Generated
output must be reproducible where practical. “Made by contributor” or a prompt alone is not a
provenance record.

No original or third-party asset may land until licensing, source, runtime wiring, dimensions,
palette, contrast, and live in-game readability are reviewed independently.

## Workshop preview

No Workshop preview image is currently committed. Its design, rights proof, in-game visual
review, and upload remain pending; runtime tile-reference checks do not make Workshop presentation
complete. Preview work must use separately documented, redistributable material and must not imply
Freehold Games endorsement.
