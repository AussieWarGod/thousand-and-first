# Asset Provenance

## Current release boundary

Current runtime asset trees contain zero project-authored bitmap sprites: all 125 shipped tile
paths presently resolve to art supplied by Caves of Qud, and the game art is not copied into this
repository or Workshop package. Objects may instead use an intentional text glyph. This is the
current inventory, not a permanent ban. A project-authored runtime sprite may ship only through
the allowlisted provenance contract below. A separately reviewed root `preview.png` is Workshop
presentation media; XML may never reference it.

Retired custom sprite drafts and their generated PNGs are absent from current source/runtime
inventory. Older private or Git history may contain drafts; history is not provenance for a new
submission and those files must not be restored.

Verify current boundary from repository root:

```bash
TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" \
  python3 Art/check_wiring.py
```

`Art/check_wiring.py` proves every local tile/file/source/hash/provenance row is exact and wired in
both directions, and that each referenced vanilla path is named by installed base-game XML. It
reads local installation metadata; it does not extract or redistribute art. Workshop packaging
independently permits only `preview.png` plus exact allowlisted runtime paths.

## Contributions

Policy accepts:

- a verified vanilla `Tile=` path already supplied by Qud, referenced as text only; or
- an intentional `RenderString`/color treatment consistent with vanilla behavior; or
- an original project-owned raster whose need, provenance, editable source, exact bytes, fallback,
  wiring, native readability, and rights have all been reviewed.

Do not submit copied, traced, recolored, edited, upscaled, or extracted Qud art. Do not submit
third-party art without an explicit compatible license and source record. Generative-image-assisted
work is not silently represented as hand-drawn: disclose the tool and complete transformation in
`method`, retain a lawful editable source, and require pixel-level human revision plus independent
native review. A prompt is never provenance or quality evidence.

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

## Original runtime asset manifest

`Art/runtime-assets.json` is schema 1. Its `assets` array is empty while the vanilla set remains
sufficient. Each future row has exactly these nonempty string fields:

| Field | Contract |
|---|---|
| `tile` | Exact staged XML path under `ThousandAndFirst/`. |
| `path` | Exact matching repository path under `Textures/`; no alternate mapping. |
| `sha256` | Lowercase SHA-256 of the shipped raster. |
| `creator`, `created`, `license` | Rights holder, ISO date, and compatible license. |
| `source` | Existing non-runtime editable source path in the repository. |
| `method` | Tools, lawful inputs/references, and all transformations; disclose assistance. |
| `fallback` | Intentional one-byte Qud text glyph used when tile rendering is unavailable. |
| `review` | Human reviewer, native game/build, tile/text scales, contrast/palette/function verdict, and evidence reference. |

Every manifest asset must be referenced by staged XML; every raster under `Textures/` must be in
the manifest; names are case-collision-safe; file and editable source must be regular non-links;
and the hash must match. No original or third-party asset may land until licensing, source,
dimensions, palette, contrast, function, and live in-game readability are independently reviewed.

## Workshop preview

Current-candidate preview status: **OPEN**. The provenance record below is authentic for commit
`e52270f`, but later heart-court geometry and content changes make it stale for the frozen source
snapshot. Preserve it as prior native evidence; recapture the current full footprint and complete
`final-native-preview-review` before any public upload. Do not edit the recorded bytes, crop, hashes,
or procedure to imply current-candidate coverage.

The committed root `preview.png` is presentation media captured from the tested mod running in
Qud. It is not referenced by XML and is not a runtime sprite. It shows a mod-authored building —
the civic heart-court (`heartcourt|civic|Huge|fallback|North`) — staged by the architecture
gallery's production snapshot/stamper/rendering path in the harness's born-clean test zone.
Generated art, a static gallery render, or a synthetic mock-up cannot stand here; this is a
native game screenshot of the recorded older build. Final human preview review
(`final-native-preview-review`) remains open and is recorded in release evidence at upload time.

| Field | Record |
|---|---|
| Capture operator | Repository maintainer's Claude-controlled local test automation; all source pixels were rendered by Qud (PrintWindow, PW_RENDERFULLCONTENT) |
| Captured | 2026-08-30 19:06 +10:00 (Australia/Sydney); user-consented attended window |
| Game | Caves of Qud marketing 1.0.5, core 2.0.211.51, Unity 6000.0.77f1 |
| Runtime commit | `e52270f4768879e7f4e6162d4237e945ee90fd22` |
| Procedure | Isolated sealed scenario profile (`arch-gallery-slice;facing=north`, dev start 6.17@40,12); [Dev] Test Game; born-clean test zone; wish `kingdom:archgallery 853` staged the heart-court via the production staging path (receipt `ag1-88ae1e2a95af1c521a2e64d7`, layout snapshot `4ccd30a051663854308edde41c13a1a4f75409f9e4228338fffa3fff3e51bb99`, zone JoppaWorld.6.17.1.1.10, rect 31,7,50,20); capture the framebuffer |
| Source save | `05b00330-5280-4cac-915d-24f2336c4e96` (throwaway dev profile) |
| Source evidence | `docs/release-evidence/preview-source.png`, 2575×1407 RGBA PNG, 2,489,849 bytes — outside runtime staging (docs/ never ships) |
| Source SHA-256 | `50389b00b12e1986c2272c41987b61dad0ebc446637899af5fa8ed64bf9b0ca3` |
| Transformation | Exact pixel crop x=1256, y=383, width=1024, height=1024, then exact 2:1 box-filter downscale to 512×512. No redaction, overlay, retouching, color change, or generative transformation |
| Output | `preview.png`, 512×512, 8-bit RGBA, non-interlaced, 394,895 bytes |
| Output SHA-256 | `9b62b33f7f39ac00b842868abf06afd62de9a39dde09d473d2f284ca885602ba` |
| Verification | Crop and downscale reproducible byte-exactly from the committed source evidence with Pillow (`crop((1256,383,2280,1407)).resize((512,512), Image.BOX)`); human preview review pending in release evidence |

The prior interim preview (founding-popup capture, output SHA-256
`498e85d0f6aba0024845bccece31a427b7b84f680087abd1d6588b8b30e00bad`) is superseded by this
capture; `Tools/workshop_metadata.py` continues to refuse that interim hash at release.

No AI-generated or generative-image-assisted imagery was used. Automation sent input, captured
the game framebuffer, and performed the exact crop. Qud's presentation remains the property of
Freehold Games; this compatibility screenshot does not imply endorsement and may not be reused as
a source asset.
