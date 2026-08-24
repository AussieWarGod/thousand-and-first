# Release and Steam Workshop Procedure

This repository can build and verify a Workshop-shaped directory, but it never authenticates to
Steam, creates an item, accepts agreements, uploads content, or changes item visibility. Those
steps stay in Caves of Qud's signed-in Workshop UI.

The supported release target is Caves of Qud v1.0.5, core build 2.0.211.51. Re-run the licensed
integration checks against any newer game build before claiming compatibility.

## Release boundary

The Workshop content root is the mod root. `Tools/stage.sh` defines its exact inventory. Source
tests, tools, design notes, contributor documents, local game files, logs, saves, and generated
assemblies are excluded. The optional root `modconfig.json` is runtime metadata and is included.
Every selected path must also have one unambiguous Windows spelling: the stage gate rejects NTFS
reserved names, invalid Win32 characters, trailing dots/spaces, and case-fold collisions before
copying or packaging anything.

Current art policy is stricter than the package format: the runtime tree contains no mod-authored
bitmap sprites. Shipped XML names verified vanilla tile paths or intentional text glyphs. See
[ASSET_PROVENANCE.md](ASSET_PROVENANCE.md). `preview.png` is presentation media, not a runtime
sprite; it still needs its own rights and review record. Packaging rejects raster extensions
case-insensitively everywhere except the exact root path `preview.png`.

## 1. Build the private bootstrap

1. Update `manifest.json`, `CHANGELOG.md`, `README.md`, and `TESTING.md` together. Keep the manifest
   ID `r_ThousandAndFirst`; use numeric `major.minor.patch` versioning.
2. Supply `preview.png`: exactly 512 by 512 pixels, 8-bit RGB/RGBA non-interlaced PNG,
   under 1,000,000 bytes. Add the exact manifest field `"PreviewImage": "preview.png"`. Prefer a
   clean in-game screenshot of the tested build. Record capture date, game build, source save,
   crop/edit steps, and author in the release issue. Do not use AI-generated or
   generative-image-assisted material, copied/extracted game assets, or unlicensed art.
3. Run the portable checks on a clean checkout, then the licensed Windows/Qud checks:

   ```bash
   ./Tools/portable-check.sh
   ./Tools/release-check.sh
   ```

4. Complete the applicable live passes in [TESTING.md](../TESTING.md), including save, quit,
   reload, and current/old-save cases. Record failures as failures; automation is not a manual
   playtest.
5. Commit the bootstrap candidate. Build a new, non-existing private-test directory outside the
   repository. Every existing destination ancestor must be owned by the current user or root;
   group/world-writable ancestors must also be sticky (a normal `/tmp` qualifies). Prefer a
   private Linux/WSL build parent, then verify before copying to a Windows Mods directory:

   ```bash
   ./Tools/workshop-package.sh --test /absolute/path/TAF-0.2.0-private
   ```

The script refuses a dirty tree, materialises only committed blobs, then validates the manifest,
preview, and Workshop metadata from those HEAD-derived bytes. In release mode it also extracts
the evidence record from `HEAD` and validates it against the materialised release documents. It
rejects development files, links, Windows-ambiguous paths, and runtime rasters, and writes a
sibling `.sha256` inventory. The canonical title must be under 129 UTF-8 bytes and the canonical
description under 8000 even when a private bootstrap has no `workshop.json`. It does not make an
archive because Qud's uploader consumes a folder. This first package is only a bootstrap: it
intentionally has no `workshop.json` yet and therefore cannot be the receipt used to prove the
later subscribed item.

## 2. Create the Steam item privately

This is a one-time, account-authorized operation.

1. Copy or move the verified bootstrap package to a uniquely named direct child under Qud's local
   `Mods` directory. Before opening Qud, prove the transfer has no changed or extra files:

   ```bash
   ./Tools/stage.sh verify /absolute/path/to/Qud/Mods/TAF-0.2.0-bootstrap
   cd /absolute/path/to/Qud/Mods/TAF-0.2.0-bootstrap
   sha256sum -c /absolute/path/TAF-0.2.0-private.sha256
   ```

   Launch Qud fresh through Steam only after both checks pass.
2. Open **Modding Toolkit**, then **Workshop**. Select `r_ThousandAndFirst` and create the Workshop
   item. Steam returns a published-file ID; Qud writes it to `workshop.json` in that local mod
   folder. Accept the Steam Workshop legal agreement if Steam requests it.
3. Keep visibility **Private**. Set:

   - title: `The Thousand and First`
   - description: exactly the canonical text printed by `Tools/workshop-package.sh --copy`
   - tags: `Beta`, `Faction`, `Settlement`, `Script`
   - preview: an external, byte-identical copy of the reviewed source image

   Do not select the `preview.png` already inside the mod folder. Qud's selector copies the chosen
   file onto `<mod>/preview.png`; selecting the destination as its own source fails.

   Compare the external copy's SHA-256 with the committed `preview.png` before selecting it. Qud's
   selector must leave the destination byte-identical.
4. Set Qud's **Upload hidden files** toggle **On** and record that state. The verified package
   contains no hidden development material or links; On makes Qud hand Steam this exact directory
   instead of silently constructing an unreceipted filtered temporary copy.
5. Submit the bootstrap privately. Wait for Qud's success result; once submission begins Steam does not
   provide a cancellation operation.
6. Keep Qud's completed `workshop.json`. It must contain the intended
   published-file ID, title, canonical description, exact tags, visibility `"0"`, and image path
   `preview.png`. The file contains public Workshop metadata, not a credential.

Creating the item and submitting content are separate Steam operations. A returned item ID does
not prove that content uploaded or loads. The bootstrap receipt is now obsolete because Qud added
`workshop.json`; do not compare a subscription against it or call these bytes frozen.

## 3. Freeze and verify the private candidate

1. Copy Qud's completed visibility-`"0"` `workshop.json` into the repository root without changing
   its bytes. Confirm the external preview copy and repository `preview.png` are byte-identical.
   Atomically regenerate Qud's exact private fields while preserving its item ID, then validate:

   ```bash
   python3 Tools/workshop_metadata.py canonicalize test manifest.json workshop.json
   python3 Tools/workshop_metadata.py workshop test manifest.json workshop.json
   git diff --check
   ```

   Commit this private artifact, then build a fresh package and receipt at a new destination:

   ```bash
   ./Tools/workshop-package.sh --test /absolute/path/TAF-0.2.0-private-frozen
   ```

2. Move that exact package into exactly one direct child of a Qud local Mods root. Remove the
   bootstrap and every other local copy with manifest ID `r_ThousandAndFirst`. Before opening Qud,
   run both the exact-inventory check and receipt check shown in section 2 against this new path
   and the `private-frozen.sha256` receipt.
3. Open Qud's Workshop screen. Verify title, canonical description, tags, private visibility, and
   preview without editing them. Verify version `0.2.0` separately from the mod manifest. Enter and
   record a truthful private-validation changelist; Qud does not store or pre-populate this field
   in `workshop.json`. Keep **Upload hidden files** On. Submit the private item again.
4. After Qud reports success, rerun both the exact-inventory check and receipt check on the local
   source directory. Any byte change, extra file, parse error, or hash mismatch is a failed
   provenance check; do not continue from that upload.
5. Move the local package out of both local Mods directories (`%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods`
   and `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Local\Mods` on a default
   Windows install; launch-path overrides can relocate them), subscribe to the private item, and
   let Steam install it. Qud registers local mods before subscriptions and skips a later mod with
   the same manifest ID, so leaving a local copy present would test the wrong source.
6. Launch fresh. Confirm the mod manager reports version 0.2.0 from Steam, then repeat the loader,
   new-game, save/reload, and representative feature passes. Check `Player.log` with:

   ```bash
   ./Tools/check-player-log.sh /absolute/path/to/Player.log
   ```

7. Run the exact-inventory and receipt checks against the installed Workshop folder. Record the item
   URL, package receipt hash, game build, platform, mod load order, log result, and completed live
   passes in the release issue.
8. Copy the frozen private package's sibling receipt byte-for-byte to
   `docs/PRIVATE_PACKAGE_RECEIPT.sha256`; do not regenerate or edit it. Compare it with the source,
   then commit the receipt before creating the evidence record or making any release-only change:

   ```bash
   cp /absolute/path/TAF-0.2.0-private-frozen.sha256 \
     docs/PRIVATE_PACKAGE_RECEIPT.sha256
   cmp /absolute/path/TAF-0.2.0-private-frozen.sha256 \
     docs/PRIVATE_PACKAGE_RECEIPT.sha256
   git add docs/PRIVATE_PACKAGE_RECEIPT.sha256
   git commit -m "Bind private package receipt for 0.2.0"
   git rev-parse HEAD
   ```

   This receipt-binding commit is `candidateCommit`. `docs/` is outside runtime staging, so runtime
   staged paths, bytes, and Git modes remain identical to the package-source parent commit.

9. Only the human who performed the subscribed-item passes may create the structured release
   record. Copy `docs/RELEASE_EVIDENCE.example.json` to the exact path
   `docs/RELEASE_EVIDENCE.json`; replace every placeholder with observed values. Set
   `candidateCommit` to the full receipt-binding commit printed above and
   `privatePackageReceiptSha256` to the SHA-256 of its committed receipt. Update
   README/CHANGELOG so they no longer say live evidence is pending, then run:

   ```bash
   sha256sum docs/PRIVATE_PACKAGE_RECEIPT.sha256
   python3 Tools/workshop_metadata.py evidence manifest.json preview.png workshop.json \
     docs/RELEASE_EVIDENCE.json README.md CHANGELOG.md
   ```

   The command must print the receipt-binding candidate commit. The public package extracts this
   evidence record from `HEAD`, validates it against the HEAD-materialised runtime and release
   documents, and proves that commit is a real ancestor of the tagged release. Release packaging
   extracts the authoritative receipt from `candidateCommit`, requires `HEAD` to carry the same
   receipt blob and mode, verifies every recorded hash against the candidate commit, and requires
   matching release paths and Git modes. This remains valid if `Tools/stage.sh` changes after the
   private test. Do not create this record from automated tests or mark a live/manual field passed
   when a person did not perform it.

Do not reuse a save made while both local and subscribed copies were visible until the active
source has been proven.

The private candidate is not the final Git tag: its `workshop.json` truthfully says visibility
`"0"`. Final content differs only by reviewed release documentation and that field.

## 4. Freeze and publish the public artifact

Only after the subscribed private-item pass:

1. Update the release documents with the evidence just collected. Atomically regenerate the
   tracked public metadata while preserving its item ID, then validate it:

   ```bash
   python3 Tools/workshop_metadata.py canonicalize release manifest.json workshop.json
   python3 Tools/workshop_metadata.py workshop release manifest.json workshop.json
   ```

   This avoids editor newline/BOM drift and Qud's non-truncating rewrite edge. The validator
   requires Qud's exact field order/format and public visibility so its pre-upload save does not
   change the tagged bytes.
2. Commit the frozen public artifact. Create an annotated tag and build it at a new destination:

   ```bash
   git status --short
   git tag -a v0.2.0 -m "The Thousand and First 0.2.0"
   ./Tools/workshop-package.sh --release /absolute/path/TAF-0.2.0-release
   ```

3. Move the verified release directory into exactly one of Qud's two local Mods roots
   (`%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods` or
   `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Local\Mods` by default on
   Windows; launch-path overrides can relocate them) as one direct child. Remove or move every
   other local copy with manifest ID `r_ThousandAndFirst`; Qud does not
   enumerate arbitrary directories and duplicate IDs make source proof ambiguous. Before opening
   Qud, run `Tools/stage.sh verify` and `sha256sum -c` against the moved directory and release
   receipt, exactly as in section 2.
4. Launch Qud through Steam and open **Modding Toolkit** → **Workshop**. Verify the populated title,
   canonical description, tags, preview, and **Public** visibility without editing them. Verify
   manifest version `0.2.0` separately. Enter, review, and record a truthful release changelist;
   that field is manual submission metadata, not part of `workshop.json`. If a populated field or
   manifest version differs, exit and fix/re-tag before submitting. Keep **Upload hidden files**
   On so Steam receives the verified directory rather than Qud's filtered temporary copy.
5. Submit once. Qud saves `workshop.json` before handing the folder to Steam; the release validator
   has already proved that save should be byte-stable. After success, rerun both exact-inventory
   and receipt checks on the local source directory, then verify the public page while signed out
   or with a second account. Any mismatch is a failed provenance check even though an in-progress
   Steam submission cannot be cancelled.
6. Subscribe to the public item with no local duplicate present. Run `Tools/stage.sh verify` and
   `sha256sum -c` against the public Steam install and release receipt, then repeat a short
   loader/save smoke. Together these verify the publicly installed bytes, not merely the earlier
   private candidate.
7. Announce it as a playtest release. Link [TESTING.md](../TESTING.md), the issue tracker, known
   limitations, save-backup advice, and exact supported game build. Do not describe unperformed
   playtest passes as complete.

## Updating an existing item

Use the same bootstrap-free frozen-private then public-tag sequence. Update the manifest first,
then run `workshop_metadata.py canonicalize test` on the existing `workshop.json`: it preserves
the item ID while replacing every title/description/tag/visibility/image field with the new
canonical private bytes. Do not shorten metadata through an ad-hoc Qud UI save; this Qud build
opens the JSON without truncating it and can leave trailing bytes. Validate, commit, rebuild,
submit/test privately, then use `canonicalize release` before the public commit/tag. Never replace
`workshop.json` with another item's ID. Increment the manifest version before upload; Qud writes
`manifest_id` and `manifest_version` key-value tags when it submits the item.

Steam's current UGC flow is documented in the
[Workshop implementation guide](https://partner.steamgames.com/doc/features/workshop/implementation)
and [ISteamUGC reference](https://partner.steamgames.com/doc/api/isteamugc). The installed Qud
build remains authoritative for its own uploader UI and metadata shape.

## Recovery

- Package creation never overwrites its destination. Remove a rejected package manually only
  after resolving its exact path.
- `Tools/stage.sh deploy --apply` backs up the existing live local mod before mirroring. The
  Workshop packaging command does not modify the live mod.
- If Steam submission fails, keep the same published-file ID and retry only after reviewing the
  local `workshop.json`, package receipt, connectivity, and Steam result. Do not create a second
  item merely to escape a failed update.
- If a public build is bad, set the item Private while investigating, preserve the failed receipt
  and tag, then publish a new patch version. Do not move or rewrite an existing release tag.
