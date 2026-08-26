# Asset Provenance

## Current release boundary

Current runtime asset trees contain zero project-authored bitmap sprites: all 55 shipped `Tile=`
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

The committed root `preview.png` is presentation media captured from the tested mod running in
Qud. It is not referenced by XML and is not a runtime sprite.

| Field | Record |
|---|---|
| Capture operator | Repository maintainer's Codex-controlled local test automation; all source pixels were rendered by Qud |
| Captured | 2026-08-25 03:50:19 +10:00 (Australia/Sydney) |
| Game | Caves of Qud marketing 1.0.5, core 2.0.211.51, Steam build 24626113 |
| Runtime commit | `99133f6a1b24f3be652903e16576ddd7bb929230` |
| Procedure | Isolated fresh profile; quickstart; found Kavvat; travel to unclaimed ground; wish `kingdom:found2 Sheol:refuge`; capture the resulting in-game founding popup |
| Source save | `401ec47e-a410-4e92-b932-d4e9283e48e6` |
| Source evidence | `09-second-founded.png`, 2560×1440 RGBA PNG, kept outside runtime staging |
| Source SHA-256 | `3fc76737b80f81dbf95fa6c4fff8173e8aa3c72da2e9a3501bddbac43ebdf0ee` |
| Transformation | Exact pixel crop only: source rectangle x=1120, y=500, width=512, height=512. No scaling, redaction, overlay, retouching, color change, or generative transformation |
| Output | `preview.png`, 512×512, 8-bit RGBA, non-interlaced, 122,055 bytes |
| Output SHA-256 | `498e85d0f6aba0024845bccece31a427b7b84f680087abd1d6588b8b30e00bad` |
| Verification | Pixel-by-pixel comparison against the source rectangle passed; the final view preserves field context and Qud's complete “Sheol is founded here as Kavvat.” line at native resolution |

No AI-generated or generative-image-assisted imagery was used. Automation sent input, captured
the game framebuffer, and performed the exact crop. Qud's presentation remains the property of
Freehold Games; this compatibility screenshot does not imply endorsement and may not be reused as
a source asset.
