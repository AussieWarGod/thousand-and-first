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

Current-candidate preview status: **OPEN** (the `final-native-preview-review` release gate is not
yet satisfied). Current-candidate preview: CAPTURED for the 1.0.0 candidate (native, commit
`a42041222ad2064b233c0d6ba5d0dedcb0a17cc1`); final-native-preview-review remains open and belongs
to the Beta/Release evidence lane, not Alpha. Do not edit the recorded bytes, crop, hashes, or
procedure without re-deriving them from a fresh capture.

The committed root `preview.png` is presentation media captured from the tested mod running in
Qud. It is not referenced by XML and is not a runtime sprite. It shows a mod-authored building —
the civic heart-court (`heartcourt|civic|Huge|fallback|North`) — staged by the architecture
gallery's production snapshot/stamper/rendering path in the harness's born-clean test zone, then
lit, revealed, and explored by the scenario's `light` verb (the `AmbientOmniscience` zone part) so
the sanctum interior renders. Generated art, a static gallery render, or a synthetic mock-up
cannot stand here; this is a native game screenshot of the current 1.0.0 candidate build. Final
human preview review (`final-native-preview-review`) remains open and is recorded in release
evidence at upload time.

| Field | Record |
|---|---|
| Capture operator | Repository maintainer's Claude-controlled local test automation; all source pixels were rendered by Qud (PrintWindow, PW_RENDERFULLCONTENT) |
| Captured | 2026-09-03 10:03:33 +10:00 (Australia/Sydney); user-consented attended window |
| Game | Caves of Qud marketing 1.0.5, core 2.0.211.51, Unity 6000.0.77f1 |
| Runtime commit | `a42041222ad2064b233c0d6ba5d0dedcb0a17cc1` |
| Procedure | Isolated sealed scenario profile via Tools/run-personas.sh, persona `arch-heartcourt-xl`, script `flatten;realize;advance 300;frame;light;status` (the runtime `fit` verb is dropped from this script — it destabilised the letterbox camera); sealed profile `OptionPlayScale=Fit` (was `Cover`, which over-scaled the stage and cropped the bottom zone rows on a high-DPI display); capture window `TAF_PERSONA_CAPTURE_WIDTH=1800`; dev start JoppaWorld.6.17.1.1.10; seed `#898783666`, plan digest `be395af604d5a91f34c06333e0346c8b56abd63cd47eafb697ffc28a65ab589a` (from `[TAF scenario] opened key=arch-heartcourt-xl` in the scenario player log); the `frame` verb framed heartcourt/fallback lot 31,4-50,21 from 30,12, native camera centred at 40,12, zoom 1 (from the `frame` row of the scenario journal); the `light` verb (`AmbientOmniscience` zone part) lit, revealed, and explored JoppaWorld.6.17.1.1.10, holding omniscient light for 50 turns, so the sanctum interior renders; zone rows 0..23 are visible in the capture and the court's own rows 4..21 are complete with ~1 row of yard below the bottom wall; capture the framebuffer |
| Source save | Throwaway scenario profile rooted at `C:\taf-scenario.EAXqvw` |
| Source evidence | `docs/release-evidence/preview-source.png`, 1800×1392 RGBA PNG, 1,625,293 bytes — outside runtime staging (docs/ never ships) |
| Source SHA-256 | `e4edae95c8d6e8e4c67af9ef895b2bdd79a1736c70f62ddb787702fb9246e489` |
| Transformation | Exact pixel crop x=550, y=752, width=640, height=640, then BOX-filter downscale (5:4) to 512×512. No redaction, overlay, retouching, color change, or generative transformation |
| Output | `preview.png`, 512×512, 8-bit RGBA, non-interlaced, 429,910 bytes |
| Output SHA-256 | `256814997366e8aef034a8ecc5222781b5700e4354613b13a11e853742b2ba4d` |
| Verification | Crop and downscale reproducible byte-exactly from the committed source evidence with Pillow (`crop((550,752,1190,1392)).resize((512,512), Image.BOX)`); human preview review pending in release evidence |

### Prior captures

The 2026-08-30 native capture (commit `e52270f4768879e7f4e6162d4237e945ee90fd22`, source SHA-256
`50389b00b12e1986c2272c41987b61dad0ebc446637899af5fa8ed64bf9b0ca3`, output SHA-256
`9b62b33f7f39ac00b842868abf06afd62de9a39dde09d473d2f284ca885602ba`) is superseded by the capture
above; preserved here as prior native evidence. The interim founding-popup capture (output
SHA-256 `498e85d0f6aba0024845bccece31a427b7b84f680087abd1d6588b8b30e00bad`) is also superseded;
`Tools/workshop_metadata.py` continues to refuse that interim hash at release.

No AI-generated or generative-image-assisted imagery was used. Automation sent input, captured
the game framebuffer, and performed the exact crop. Qud's presentation remains the property of
Freehold Games; this compatibility screenshot does not imply endorsement and may not be reused as
a source asset.
