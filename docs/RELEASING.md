# Release and Steam Workshop Procedure

The package builder remains offline: it never authenticates, creates an item, accepts agreements,
uploads, or changes visibility. A separate local Steam-client publisher is implemented under the
maintainer's authorization for autonomous, ready Alpha releases. Corrected private0.3.1 attempt
`0002` was submitted (63022 TERMINAL0) and finalized after exact subscribed-install verification
(20925 TERMINAL0). Original attempt `0001` remains immutable. This proves one client only;
`freshTransferVerified=false` and `releaseReady=false`. Public Alpha0.3.1 is now published:
submit55498 TERMINAL0 at13:18 UTC, then finalizer55266 TERMINAL0 verified one subscribed client
and finalized public item3794797472 attempt `0001`. Strict3077-file Alpha package68272 and
native copy54621 passed. Annotated `v0.3.1` binds commit
`a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e`; broader Beta acceptance is not claimed.
Exact plan/receipt/inventory/finalization hashes and evidence are in
[current status](STATUS.md#public-031--published-and-finalized).
The release source/tag push was accepted with a GitHub warning that the existing credentials
bypassed main's pull-request and three-required-check rules. No protection settings changed.
Do not repeat direct main pushes: follow-up integration must use a PR and its required checks.
Retain the accepted release history and immutable tag; do not rewrite them to hide this event.
Do not automate public releases until the private-item adoption gates below pass. Never automate authentication
or legal acceptance.

### Current 0.3.1 Alpha decision — not a standing gate change

The user waived manual startup/save/reload for this Alpha; no ordinary-play or graceful-Quit PASS
is claimed. Existing six genuine startup boot/save/cold-load pairs and current stock native16
checks keep their distinct source scopes. Canonical release-check29200 completed all stages on
clean792270b with13625/5012 managed cases and zero managed skips, but three explicit environmental
bind-alias exclusions (PACKAGE/COPY/BACKUP). Root accepts those narrow gaps for this Alpha,
not as a user waiver or zero-skip full-gate result; ownership and alias guards remain unchanged.
Root's one-release decision reused this exact frozen-runtime verification, checked the public-only
delta, and required strict `--alpha` package lineage, receipt, tag and structure binding. The two
README freshness assertions were updated; 501 Tools tests and final document checks passed.
No additional complete `--alpha` release-check run is claimed. The permanent
procedure below and Beta/Release evidence requirements remain unchanged.
[Exact decision and gate log identity](STATUS.md#one-release-alpha-verification-decision).

Supported target: Caves of Qud v1.0.5, core build 2.0.211.51. Re-run all licensed checks before
claiming compatibility with another build.

## Two public package lanes

`Tools/workshop-package.sh` has three mutually distinct modes:

| Mode | Purpose | Public proof required |
|---|---|---|
| `--test` | Private bootstrap/candidate; `workshop.json` may be absent | Clean committed package only |
| `--alpha` | Public `0.3.x`, labelled **v0.3 Alpha**; first version is exactly `0.3.0` | Private receipt binding, final preview, structure review, public metadata, annotated tag |
| `--release` | Evidence-complete later lane | Every Alpha gate plus `docs/RELEASE_EVIDENCE.json` and retained human/native artifacts |

Alpha deliberately does not invent final human evidence. It uses the machine-only
`docs/ALPHA_CANDIDATE.json` record instead. Beta and production Release are separate Workshop
listings and must restore the full evidence lane. Current metadata constants are intentionally
Alpha-specific; changing channel requires a reviewed metadata/tool/test change, not an ad-hoc UI
edit.

[ALPHA-RELEASE-PLAN.md](ALPHA-RELEASE-PLAN.md) records the completed first `0.3.0` freeze. Use
**Updating Alpha** below for every later patch; never rerun the literal `v0.3.0` commands.

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
`docs/STRUCTURE_REVIEW.json` must bind the responsibility/protocol review (author-authorized reviewer) to the exact staged C#
inventory.
Missing or stale structural review is a failed release, including Alpha.

Run relevant [TESTING.md](../TESTING.md) live passes. Record failure as failure. Automation, a
source test, and an old native receipt are never a substitute for the current behavior they do not
exercise.

For first Alpha, perform the one-time private-candidate identity freeze from
[ALPHA-RELEASE-PLAN.md](ALPHA-RELEASE-PLAN.md): manifest `0.3.0`, Alpha title, honest pre-release
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
copy and skip a later duplicate, making source proof meaningless. The live dev deploy (`Tools/stage.sh
deploy`) must still exist for `Tools/release-check.sh --test`'s deployment boundary step; remove it
only afterwards, immediately before this Workshop step, and restore it again before any later
release-check.

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

## 5A. First public v0.3 Alpha — completed `v0.3.0`

This subsection records the one-time first-publication flow. Do not rerun it or recreate its tag;
later Alpha patches use **Updating Alpha**.

Do not create `docs/RELEASE_EVIDENCE.json` for Alpha. It would falsely imply completed final human
release passes. Instead:

1. Change root Workshop metadata from private to public:

   ```bash
   python3 Tools/workshop_metadata.py canonicalize alpha manifest.json workshop.json
   python3 Tools/workshop_metadata.py workshop alpha manifest.json workshop.json
   ```

2. Replace pre-release README/CHANGELOG status with exact final Alpha claims:

   - `**Status: 0.3.0 public Alpha playtest.**`
   - `## [0.3.0] — YYYY-MM-DD (Alpha)` as first changelog version heading.

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
   git tag -a v0.3.0 -m "The Thousand and First v0.3 Alpha"
   ./Tools/workshop-package.sh --alpha /absolute/path/TAF-0.3.0-alpha
   ```

`--alpha` requires a canonical `0.3.x` version, public Alpha metadata, final non-interim preview,
current structural review, committed candidate record and receipt, unchanged staged runtime/modes
since the private candidate, matching Workshop ID, a matching annotated `v<version>` tag at `HEAD`,
and a clean tree. The one-time plan fixes the first version at exactly `0.3.0`; later proved recovery
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

Do not reuse the first-publication same-item Private flow now that `3794797472` is public.
Candidate schema 2 now requires a positive `privateWorkshopId` distinct from public `workshopId`;
the package gate verifies both IDs and the exact Private/Public visibility delta against committed
metadata. Historical schema 1 remains readable only for `0.3.0`. A separate owned Private staging
item still must be established and verified; no procedure authorizes hiding production or exposing
unverified candidate bytes to subscribers.

Increment the manifest to a new `0.3.x` patch before any candidate upload.
Using the staging identity, repeat private canonicalization, immutable package, private
subscription, and receipt binding. Then, in this
order: canonicalize `workshop.json` to Public; replace the public status/changelog with
`**Status: <version> public Alpha playtest.**` and
`## [<version>] — YYYY-MM-DD (Alpha)`; create a fresh `docs/ALPHA_CANDIDATE.json` from those public
fields; and validate it:

```bash
VERSION="$(python3 Tools/workshop_metadata.py fields manifest.json | sed -n '1p')"
test "$VERSION" != "0.3.0"
python3 Tools/workshop_metadata.py canonicalize alpha manifest.json workshop.json
python3 Tools/workshop_metadata.py workshop alpha manifest.json workshop.json
python3 Tools/workshop_metadata.py alpha-candidate \
  manifest.json preview.png workshop.json docs/ALPHA_CANDIDATE.json \
  README.md CHANGELOG.md
```

Commit only the exact reviewed public metadata, status, changelog, and candidate-record files.
From that clean public-candidate commit, run the gates, create its new annotated tag, and package:

```bash
./Tools/portable-check.sh
./Tools/release-check.sh --alpha
git status --short
git tag -a "v${VERSION}" -m "The Thousand and First v${VERSION} Alpha"
./Tools/workshop-package.sh --alpha "/absolute/path/TAF-${VERSION}-alpha"
```

Upload only that new package, then repeat signed-out listing, subscribed-byte, and public-smoke
verification. Never reuse another item's ID, rewrite an existing tag, merge package folders, or
treat a prior receipt as proof of changed bytes.

## Local automated-upload implementation and deployment design

**Corrected private and public0.3.1 upload/installation finalized; one client per invocation.**
No privileged CI workflow has been deployed. Exact current evidence: [STATUS.md](STATUS.md).
The branch/runner design below remains a proposal, not permission to change repository protection
or attach a credentialed runner. Local Alpha automation is authorized once the exact candidate and
private-item checks pass; it does not require an invented CI deployment first.

Verified local pieces:

- Retained active-worktree run94540 passed47 launcher fixtures, all14 upload suites, both production
  helper builds, the installed-test build and14 installed-package cases. Actual finalizer43652
  exited0 for private attempt `0001`: `SubscribedInstallationVerified`, `reason=null`,
  `attemptFinalized=true`, one client, `freshTransferVerified=false`, `releaseReady=false`.
  Original attempt/submission bytes remain unchanged. Corrected attempt `0002` has since been
  submitted and finalized, replacing the installed private package without rewriting history.
  [Current records and scope](STATUS.md#corrected-private-031--installed-and-finalized).
- `workshop_metadata.py` and the actual package harness enforce the separate staging/public schema.
- `workshop_upload_plan.py` checks a closed package receipt, canonical metadata, exact item/version,
  no linked files, bounded inventory and Windows-safe paths. Its JSON is a plan, not release authority.
- `workshop-steam-probe.ps1` builds the installed Steamworks.NET reference without redistributing it
  and runs a read-only client probe. On 2026-09-05 it compiled with zero warnings/errors and confirmed
  app `333640`, owned public item `3794797472`, `manifest_id=r_ThousandAndFirst` and
  `manifest_version=0.3.0`. Evidence: `/mnt/c/taf-workshop-probe.ZNjYbg`.
- Retained initial publisher checkpoint: compilation passed with zero warnings/errors, as did30
  protocol cases, 15 package groups and 9 active-attempt groups. Its default check mode queried
  the real Alpha item and refused synthetic `0.3.0` as not newer, without creating an attempt or
  submitting. Evidence: `/tmp/taf-workshop-automation.vgLRAO/README.md`. This is not a private
  upload, subscriber receipt or release-candidate acceptance test.

The publisher is separate from the packager. It defaults to checking only; explicit submission
must bind a freshly gated package and digest, an existing allowlisted item, a truthful changelist,
and a persistent attempt directory. One unresolved attempt per item records the exact version and
is created before Submit. Explicit finalization can bind a clean successful submission to fresh
installed bytes; only complete immutable history admits another package whose canonical file
inventory is absent from all prior attempts. Private staging permits an equal or higher `0.3.x`
patch; public Alpha requires a strictly higher patch. A same-version private recandidate must have
different package bytes, not merely different plan paths or receipt spelling, and claims a new
attempt without changing the old one. Ordinary `-Verify` does not finalize. Preserve every original
plan/package/path and record; no attempt retry, history deletion or root switching is supported.
A timeout is **uncertain**.
The exact bounded history, five-argument delivery modes and result checks are documented in
[PUBLISHING.md](../Tools/WorkshopSteam/PUBLISHING.md). Corrected private delivery is now proved
for one client; ordinary acceptance is unclaimed and its manual gate waived only as recorded above.
Even a successful callback plus matching remote metadata is **submitted, unverified**, never proof
that subscribers received the exact files. Steam-installed receipt verification remains mandatory.

Qud's uploader sets Steam key-value tags `manifest_id` and `manifest_version` in addition to uploading
`manifest.json`; its mod manager reads `manifest_version` for update indication. The local publisher
must preserve that behavior. SteamCMD alone has not been verified for this Qud-specific contract.

### Branch model

- Make `dev` the protected default integration branch. Feature branches and isolated worktrees
  target `dev`; normal CI runs there and on pull requests.
- Keep `main` protected and release-only. A release PR moves one frozen, reviewed `dev` tree to
  `main` without squashing or changing package bytes. Direct pushes and force-pushes stay disabled.
- Require the portable/test gates, metadata review, and protected-environment approval before
  merge. Tag the exact resulting `main` commit with an annotated `v<version>` tag, then run the
  exact tagged release-mode gate before any public upload.
- Merge the tagged `main` commit back into `dev` before new integration work. A hotfix starts from
  the affected `main` tag, follows the complete patch-release proof, then returns to `dev`.
- Never treat a branch name, mutable artifact, or successful CI run as a release identity. Version,
  candidate commit, annotated tag, package receipt, Workshop ID, and subscribed bytes must agree.

Changing the repository default branch and protection rules is a maintainer-admin operation. Do it
only after `dev` exists remotely, current CI targets both lanes, and links/scripts that assume
`main` have been audited.

### Uploader boundary

Build a small pinned .NET command-line tool around Steamworks.NET. "Headless" here means no Qud
UI; it does not mean Steam-free. Valve requires a running Steam client, a licensed signed-in user,
the same operating-system user context, and successful `SteamAPI.Init`. The tool must pump
`SteamAPI.RunCallbacks` until the asynchronous result arrives. Launch with an explicit runtime
AppID of `333640` and refuse after initialization unless `SteamUtils.GetAppID()` returns exactly
`333640`.

The first version accepts only an existing item ID and a frozen package. It must refuse item
creation, deletion, arbitrary paths, unknown fields, and identity mismatches. A Private job binds
an exact protected `dev` commit SHA, test-package digest, and separately allowlisted Private staging
item. A Public job binds the final annotated `main` tag, public-package digest, and production item
`3794797472`. The two lanes must never share a target item. Its transaction is:

1. Validate locally the exact commit/tag identity for the requested lane, release mode, immutable package
   receipt, `manifest.json`, `workshop.json`, preview hash, existing Workshop ID, requested
   visibility, and changelist.
2. Before `StartItemUpdate`, query the remote item and refuse unless its ID is allowlisted for this
   lane and its owner SteamID, consumer AppID `333640`, current visibility, expected title, and
   `manifest_id` baseline all match frozen configuration. A locally consistent wrong item ID must
   fail before any mutation.
3. Map visibility semantically, never by numeric cast. Qud stores UI indices: JSON `"0"` means
   Steam `k_ERemoteStoragePublishedFileVisibilityPrivate`, while JSON `"2"` means Steam
   `k_ERemoteStoragePublishedFileVisibilityPublic`; the Steam enum assigns those numeric values in
   the opposite direction. Refuse every value outside the requested lane.
4. Call `StartItemUpdate`, then check every result from `SetItemTitle`, `SetItemDescription`,
   `SetItemTags`, `SetItemVisibility`, `SetItemContent`, and `SetItemPreview`.
5. Remove then add the exact searchable key-value tags
   `manifest_id=r_ThousandAndFirst` and `manifest_version=<manifest version>`. Fail if either API
   call fails; never append a second version value.
6. Call `SubmitItemUpdate` once, pump callbacks with a bounded timeout, and require no I/O failure,
   `EResult.OK`, the expected published-file ID, and no outstanding legal-agreement prompt. Valve
   does not provide cancellation after submission begins.
7. Query the item by ID with long description and key-value tags enabled. Verify owner/AppID, title,
   description, normal tags, visibility, preview, `manifest_id`, and `manifest_version` against the
   frozen inputs. Preserve a redacted machine receipt and Steam transfer logs.

Private upload and public promotion remain two distinct, independently gated jobs. The Private job
runs from the exact `dev` SHA against the staging item; its subscribed proof and receipt-binding
commits precede the release PR. The Public job runs against production only from the final annotated
`main` tag under the maintainer's Alpha-release authorization. Tooling must prove runtime content identity while allowing only
the frozen staging-to-production `WorkshopId` and visibility metadata delta, and record both package
digests. Never hide or mutate the public item for routine candidate validation. If the receipt
schema cannot express that separation safely, stop promotion. A successful API response does not replace the
subscribed-byte receipt, native load/save/reload smoke, signed-out page inspection, or human media
review in sections 4 and 6.

### Self-hosted runner security

The Steam session is a publication credential. A credentialed self-hosted runner must not be
attached to this public source repository: another workflow can target its labels without declaring
the protected environment. Environment approval alone is not isolation. GitHub warns that
self-hosted runners are persistent and can be compromised by untrusted workflow code.

- Prefer a private release-control repository. Restrict its runner group to that repository and the
  exact allowlisted upload workflow at a pinned SHA where the GitHub plan supports workflow-scoped
  access. Otherwise keep the Steam host offline/attended instead of registering it as an Actions
  runner. The workflow still uses a protected environment with required human reviewers.
- `workflow_dispatch` selects a branch or tag, not a raw SHA. Dispatch only the trusted protected
  workflow ref, require a full candidate SHA as input, and verify that exact SHA/annotated tag and
  artifact digest before privileged work. Never use `pull_request`, `pull_request_target`, or an
  automatic tag trigger.
- Run ordinary build/test/package work on an unprivileged hosted runner. The privileged job may
  consume only an immutable, digest-verified package and a preinstalled, hash-pinned uploader; it
  must not execute package content or arbitrary repository scripts under the Steam account.
- Pin third-party actions by full commit SHA. Grant the workflow read-only repository permissions
  unless an explicit narrower write is required. Do not expose the Steam host to fork artifacts.
- Bootstrap Steam and Steam Guard interactively under a dedicated account that owns Qud and the
  item. Do not put a Steam password or guard code in GitHub secrets, workflow arguments, logs, or
  the repository. Expired login becomes a fail-closed attended maintenance event.
- Snapshot/reimage the runner between release windows where practical, isolate it from developer
  machines and private data, and retain only redacted receipts. Disable the runner outside release
  windows.
- Require a second approval for Public visibility. On any mismatch, keep/move the item Private and
  follow Recovery; never retry by creating a new item.

### Adoption gates

1. Pin the Steamworks.NET and native Steamworks binaries, record their hashes and licences, and
   reproduce the exact Steam API initialization on the isolated host.
2. Prove the transaction against a sacrificial Private item, including timeout, rejected setter,
   wrong owner, legal-agreement, network-loss, duplicate key-value-tag, and JSON/Steam visibility
   inversion cases. A test that asks for Private and observes Public must fail before submission.
   Prove the pre-mutation wrong-item/AppID/owner refusal too.
3. Query and verify both key-value tags, then subscribe from a clean client and match package bytes.
4. Threat-model workflow/ref/artifact substitution and have the privileged runner design reviewed.
5. Run one attended private TAF update in parallel with the current Qud UI path. Compare metadata,
   transfer logs, subscribed bytes, and native behavior.
6. Add and manually establish the separately allowlisted Private staging item. Define and test the
   only permitted staging-to-production metadata delta; never use production as the candidate item.
7. Only after all gates pass, add the uploader/workflow, tests, rollback drill, and retained receipt
   schema in a separate reviewed change. Then revise the opening statement of this document.

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
[Workshop implementation guide](https://partner.steamgames.com/doc/features/workshop/implementation),
[Steamworks API initialization guide](https://partner.steamgames.com/doc/sdk/api), and
[ISteamUGC reference](https://partner.steamgames.com/doc/api/isteamugc). Steamworks.NET requires a
regular callback pump in its
[getting-started guide](https://steamworks.github.io/gettingstarted/). GitHub documents the
[risk of compromised runners](https://docs.github.com/en/actions/concepts/security/compromised-runners)
and [self-hosted runner access controls](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/manage-access).
Installed Qud remains authoritative for its uploader UI and metadata serializer.
