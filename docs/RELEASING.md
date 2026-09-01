# Release and Steam Workshop Procedure

This repository can build and verify Workshop-shaped directories. It never authenticates to
Steam, creates an item, accepts agreements, uploads, or changes visibility. Those actions remain
explicit, attended steps in Caves of Qud's signed-in Workshop UI.

Supported target: Caves of Qud v1.0.5, core build 2.0.211.51. Re-run all licensed checks before
claiming compatibility with another build.

## Two public package lanes

`Tools/workshop-package.sh` has three mutually distinct modes:

| Mode | Purpose | Public proof required |
|---|---|---|
| `--test` | Private bootstrap/candidate; `workshop.json` may be absent | Clean committed package only |
| `--alpha` | Public `1.0.x`, labelled **v1.0 Alpha**; first version is exactly `1.0.0` | Private receipt binding, final preview, structure review, public metadata, annotated tag |
| `--release` | Evidence-complete later lane | Every Alpha gate plus `docs/RELEASE_EVIDENCE.json` and retained human/native artifacts |

Alpha deliberately does not invent final human evidence. It uses the machine-only
`docs/ALPHA_CANDIDATE.json` record instead. Beta and production Release are separate Workshop
listings and must restore the full evidence lane. Current metadata constants are intentionally
Alpha-specific; changing channel requires a reviewed metadata/tool/test change, not an ad-hoc UI
edit.

Follow [ALPHA-RELEASE-PLAN.md](ALPHA-RELEASE-PLAN.md) before the first version bump.

## Package boundary

`Tools/stage.sh` defines the exact Workshop inventory. The package includes runtime source/XML,
allowlisted runtime assets, `README.md`, `PLAYTESTING.md`, `SUPPORT.md`, `LICENSE`, `NOTICE`,
`CHANGELOG.md`, `manifest.json`, optional `modconfig.json`, `preview.png`, and `workshop.json` when
present. It excludes `.git`,
`.github`, `_notes`, `DevTests`, `Harness`, `Tools`, `Art`, `docs`, saves, logs, assemblies, project
files, and contributor-only material.

The packager:

- refuses dirty/untracked input, mutable destinations, symlinks, special files, unsafe ancestors,
  repository overlap, Windows-invalid names, and case-fold collisions;
- materializes exact ordinary blobs from `HEAD`, not mutable worktree bytes;
- validates manifest, preview, Workshop serializer bytes, package inventory, modes, raster allowlist,
  and SHA-256 receipt;
- never overwrites an existing destination or receipt; and
- creates a folder because Qud's uploader consumes a folder, not an archive.

`preview.png` must be a 512×512, 8-bit RGB/RGBA, non-interlaced PNG below 1,000,000 bytes. Final
source/capture/rights/edit history belongs in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md). A debug
overlay, founding popup over empty ground, synthetic mock-up, known interim image, copied asset, or
unlicensed image cannot sign a public package.

Runtime image packaging permits only `preview.png` and exact allowlisted runtime raster paths.

## 1. Pre-freeze checks

Finish implementation fan-in. On a clean checkout, run serially:

```bash
./Tools/portable-check.sh
./Tools/release-check.sh --test
```

Public CI is portability evidence only. Its exact installed-data skip allowlist does not sign a
release. `release-check.sh` requires licensed installed Qud data, zero release skips, exact native
compile/test, asset/reference checks, smoke/deploy checks, package harness, and the structural
release contract. Every staged production C# file must be below 300 physical lines, and
`docs/STRUCTURE_REVIEW.json` must bind human responsibility/protocol review to the exact staged C#
inventory.
Missing or stale structural review is a failed release, including Alpha.

Run relevant [TESTING.md](../TESTING.md) live passes. Record failure as failure. Automation, a
source test, and an old native receipt are never a substitute for the current behavior they do not
exercise.

For first Alpha, perform the one-time private-candidate identity freeze from
[ALPHA-RELEASE-PLAN.md](ALPHA-RELEASE-PLAN.md): manifest `1.0.0`, Alpha title, honest pre-release
README/CHANGELOG wording, current preview, and synchronized tests/docs. Do not write public status or
a dated release heading until the subscribed private candidate passes section 4.

## 2. Build private bootstrap

Commit frozen source. Build into a new absolute destination outside the repository:

```bash
VERSION="$(python3 Tools/workshop_metadata.py fields manifest.json | sed -n '1p')"
./Tools/workshop-package.sh --test "/absolute/path/TAF-${VERSION}-bootstrap"
```

Normal `/tmp` is acceptable when its sticky/ownership protection is intact. The command creates a
sibling `.sha256` receipt. A first bootstrap may omit `workshop.json`; it exists only to create the
private Steam item and cannot be the later subscription receipt.

Move/copy the folder to one unique direct child of Qud's local Mods root. Before launching Qud:

```bash
./Tools/stage.sh verify "/absolute/path/to/Qud/Mods/TAF-${VERSION}-bootstrap"
cd "/absolute/path/to/Qud/Mods/TAF-${VERSION}-bootstrap"
sha256sum -c "/absolute/path/TAF-${VERSION}-bootstrap.sha256"
```

Remove every other local or subscribed copy with ID `r_ThousandAndFirst`. Qud can prefer a local
copy and skip a later duplicate, making source proof meaningless.

## 3. Create or update private Steam item

In Qud, open **Modding Toolkit** → **Workshop**, select `r_ThousandAndFirst`, and create/update the
item. Keep visibility **Private**. Use canonical fields printed by:

```bash
./Tools/workshop-package.sh --copy
```

Use an external byte-identical copy when Qud asks for a preview source; selecting the destination
`preview.png` as its own source can fail. Compare hashes before and after. Enable **Upload hidden
files** so Qud submits the already-audited folder rather than an unreceipted filtered copy. Accept
Steam's agreement only through Steam's UI.

After successful private submission, preserve Qud's `workshop.json`. It carries the published-file
ID and public metadata, not a credential. Item creation alone does not prove an upload or load.

## 4. Freeze subscribed private candidate

Copy Qud's completed visibility-`"0"` `workshop.json` into repository root. Canonicalize it while
preserving its item ID:

```bash
python3 Tools/workshop_metadata.py canonicalize test manifest.json workshop.json
python3 Tools/workshop_metadata.py workshop test manifest.json workshop.json
git diff --check
git add workshop.json
git commit -m "Freeze private Workshop metadata"
./Tools/release-check.sh --test
./Tools/workshop-package.sh --test "/absolute/path/TAF-${VERSION}-private"
```

Upload that exact folder to the private item with **Upload hidden files** enabled. Then close Qud,
remove local copies, subscribe through Steam, and launch fresh. Confirm Steam-installed manifest
version, exact inventory/receipt, loader, new game, save → desktop → reload, representative Alpha
flow, and redacted `Player.log`. A local duplicate invalidates this proof.

Copy the frozen package receipt byte-for-byte into the repository and commit it:

```bash
cp "/absolute/path/TAF-${VERSION}-private.sha256" docs/PRIVATE_PACKAGE_RECEIPT.sha256
cmp "/absolute/path/TAF-${VERSION}-private.sha256" docs/PRIVATE_PACKAGE_RECEIPT.sha256
git add docs/PRIVATE_PACKAGE_RECEIPT.sha256
git commit -m "Bind private package receipt for ${VERSION}"
git rev-parse HEAD
```

That full receipt-binding commit is `candidateCommit`. `docs/` is outside runtime staging, so the
staged candidate remains byte-identical to the subscribed package.

## 5A. Public v1.0 Alpha

Do not create `docs/RELEASE_EVIDENCE.json` for Alpha. It would falsely imply completed final human
release passes. Instead:

1. Change root Workshop metadata from private to public:

   ```bash
   python3 Tools/workshop_metadata.py canonicalize alpha manifest.json workshop.json
   python3 Tools/workshop_metadata.py workshop alpha manifest.json workshop.json
   ```

2. Replace pre-release README/CHANGELOG status with exact final Alpha claims:

   - `**Status: 1.0.0 public Alpha playtest.**`
   - `## [1.0.0] — YYYY-MM-DD (Alpha)` as first changelog version heading.

3. Copy [ALPHA_CANDIDATE.example.json](ALPHA_CANDIDATE.example.json) to exact path
   `docs/ALPHA_CANDIDATE.json`. Replace every sentinel with observed values:

   ```bash
   git rev-parse HEAD
   python3 Tools/workshop_metadata.py workshop-id workshop.json
   sha256sum preview.png docs/PRIVATE_PACKAGE_RECEIPT.sha256
   ```

   `candidateCommit` is the receipt-binding commit from section 4, not the later public commit.

4. Validate before commit:

   ```bash
   python3 Tools/workshop_metadata.py alpha-candidate \
     manifest.json preview.png workshop.json docs/ALPHA_CANDIDATE.json \
     README.md CHANGELOG.md
   ```

   Success prints candidate commit and private-receipt SHA-256. The record contains no human
   approval or manual-pass claim.

5. Commit public metadata/record, rerun clean gates, tag, and package at a new destination:

   ```bash
   ./Tools/portable-check.sh
   ./Tools/release-check.sh --alpha
   git status --short
   git tag -a v1.0.0 -m "The Thousand and First v1.0 Alpha"
   ./Tools/workshop-package.sh --alpha /absolute/path/TAF-1.0.0-alpha
   ```

`--alpha` requires a canonical `1.0.x` version, public Alpha metadata, final non-interim preview,
current structural review, committed candidate record and receipt, unchanged staged runtime/modes
since the private candidate, matching Workshop ID, a matching annotated `v<version>` tag at `HEAD`,
and a clean tree. The one-time plan fixes the first version at exactly `1.0.0`; later proved recovery
or update builds use a new patch version. This lane does not accept or silently fall back to full
release evidence.

## 5B. Evidence-complete later release

Use this lane only after metadata/tool constants are intentionally changed for the separate Beta or
Release listing and a human has performed every claimed pass.

Copy `docs/RELEASE_EVIDENCE.example.json` to `docs/RELEASE_EVIDENCE.json`. Bind exact release
version, pre-evidence candidate commit, Qud marketing/core build, `Assembly-CSharp.dll` SHA-256,
Workshop ID, preview hash, private receipt hash, subscription results, every numbered TESTING pass
or reviewed waiver, and retained artifacts below `docs/release-evidence/`. Human names/times must be
real; placeholders, automation-authored human claims, missing artifacts, hash drift, unknown pass
IDs, duplicate IDs, reordered IDs, or stale `TESTING.md` fail.

Validate:

```bash
python3 Tools/workshop_metadata.py testing-pass-ids TESTING.md
python3 Tools/workshop_metadata.py evidence \
  manifest.json preview.png workshop.json docs/RELEASE_EVIDENCE.json \
  README.md CHANGELOG.md
```

Only then run clean gates, create the annotated version tag, and package:

```bash
./Tools/portable-check.sh
./Tools/release-check.sh --release
git status --short
git tag -a "v${VERSION}" -m "The Thousand and First ${VERSION}"
./Tools/workshop-package.sh --release "/absolute/path/TAF-${VERSION}-release"
```

## 6. Upload and verify public bytes

Move the verified public folder into exactly one Qud Mods root. Verify `Tools/stage.sh verify` and
its `.sha256` receipt before opening Qud. In Workshop UI, confirm title, description, tags, preview,
manifest version, and **Public** visibility without editing package fields. Enter a truthful manual
changelist, keep **Upload hidden files** enabled, and submit once.

After success:

1. rerun inventory and receipt checks on upload source;
2. inspect public page while signed out or from another account;
3. remove local copy, subscribe to public item, and verify Steam-installed bytes/receipt;
4. repeat loader and save/reload smoke; and
5. announce exact supported build, Alpha status, backup warning,
   [PLAYTESTING.md](PLAYTESTING.md), known limitations, and issue tracker.

An in-progress Steam submission cannot be cancelled. Never describe unperformed tests as passed.

## Updating Alpha

Increment semantic version before upload. Repeat private canonicalization, immutable package,
private subscription, receipt binding, public Alpha record, tag, and public verification. Never
reuse another item's ID, rewrite an existing tag, merge package folders, or treat a prior receipt as
proof of changed bytes.

## Recovery

- Package creation never overwrites its destination. Remove a rejected artifact manually only
  after resolving its exact path.
- If upload fails, preserve item ID and receipts. Review local `workshop.json`, connectivity, Qud's
  result, and exact package before retrying; do not create a second item to escape failure.
- If a public build is unsafe, make the item Private, preserve failed tag/receipt, investigate on a
  copy, and publish a new patch version after repeating proof.
- `Tools/stage.sh deploy --apply` backs up an existing live local mod before mirroring. Workshop
  packaging does not touch live mods.

Steam's general UGC flow is documented by Valve's
[Workshop implementation guide](https://partner.steamgames.com/doc/features/workshop/implementation)
and [ISteamUGC reference](https://partner.steamgames.com/doc/api/isteamugc). Installed Qud remains
authoritative for its uploader UI and metadata serializer.
