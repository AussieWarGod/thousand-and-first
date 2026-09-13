# Release and Steam Workshop Procedure

**Current public Alpha: 0.3.3, published and finalized.** Release run `34577908711` verified
one subscribed installation. [Exact public receipts and limits](STATUS.md#public-033--published-and-finalized).
Earlier release narratives below remain historical evidence, not current acceptance claims.

## Standing author ruling — 2026-09-11

The author instructed: **"yes, and forever, no manual test gate for release"**.
This applies to 0.3.3 and all subsequent Alpha, Beta, and production releases.
Automated tests and machine-verifiable release evidence are the release testing gates.
Manual startup/save/reload, gameplay, media review, and listing inspection are optional
supplementary checks, never prerequisites or recurring waiver requests. This ruling
supersedes every manual-testing or human-test-review requirement below, including
the historical 0.3.1-only waiver language. Historical evidence is not relabelled.

All automated checks, package custody, receipt/lineage binding, protected integration,
and subscribed-install verification remain required. Record actual coverage and gaps;
never invent a manual PASS, waive an automated failure, or claim all-subscriber delivery
from one client. Authentication, Steam Guard, and legal acceptance remain human actions;
`NeedsUser` still stops those operations. Legacy full-evidence validator identity fields
must be migrated to truthful automated provenance before that lane is used, not filled
with fictitious human reviewers. The existing Alpha lane already uses machine evidence.

### 0.3.3 automated evidence

Private staging run `34543738835` completed successfully at source
`15901a42c5d2202813c5227c31d94e76e0c416c9`: all release-check stages, submission,
subscribed-install verification, and finalization passed for item `3796495680`, attempt
`0004`. The bound private receipt SHA-256 is
`5c41283ea5dfaa499461db621488b25b0c6aefdbcb86dd04eb229ce7721fada2`.
Licensed suites passed 14112 and 5246 cases with zero skips. Automated native evidence
on identical production/harness bytes at `3cf2825ec94a8153c180faccb5c7bf33e464f4df`
includes fresh Quickstart paid construction and six biome/advisor save/cold-load pairs.
This does not prove old-save construction, building completion, or ordinary gameplay.
Private delivery proves one subscribed client only (`freshTransferVerified=false`,
`releaseReady=false`); public publication remains a separate verified operation.

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
bypassed main's pull-request and three-required-check rules. That push changed no protection
settings; main's protection was updated later — see
[current status](STATUS.md#current-repository-integration-state--pr-6-merged-main-protection-updated).
Do not repeat direct main pushes: follow-up integration must use a PR and its required checks.
Retain the accepted release history and immutable tag; do not rewrite them to hide this event.
Do not automate public releases until the private-item adoption gates below pass. Never automate authentication
or legal acceptance.

### Author ruling 2026-09-08 — automated release lane, and the doctrine it amends

The sentence immediately above is **retained as written and amended by author ruling of
2026-09-08** for exactly one artifact: `.github/workflows/release.yml`. Nothing else in this
document is relaxed, and the second sentence — never automate authentication or legal acceptance —
is unamended and unamendable: an exit-4 `NeedsUser` outcome stops the run for the operator.

The ruling admits, for that workflow only:

- a credentialed self-hosted runner attached to this public repository, contradicting the
  "Self-hosted runner security" text below, which is retained verbatim and marked as superseded
  for this one shape; and
- an automatic annotated-tag trigger, likewise retained-and-superseded below.

What the lane actually automates: lane resolution from the tag, tag/lineage/metadata/candidate
proofs, the Linux repository audit and both portable suites, the full licensed
`Tools/release-check.sh` run at the tagged commit, the immutable package and its verified native
copy, the upload plan, the publisher `check`, one `-Submit`, the polled `-Verify`, and — once that
reports `SubscribedInstallationVerified` — one `-Finalize` from the same run directory. What it
does not automate: Steam sign-in, Steam Guard, the Workshop legal agreement, the subscribed-byte
check, the signed-out listing inspection, the in-game smoke, and the announcement.

Mitigations in force for the accepted residual risk:

- The runner is registered at repository level and started by hand (`run.cmd`) only inside a
  release window, then stopped. Runner *groups* are unavailable on a personal-account repository,
  so label targeting remains the residual exposure and is stated here rather than hidden.
- Fork pull-request workflows require maintainer approval for **all** external contributors.
- A tag ruleset restricts creation, update and deletion of `v*` and `staging-v*` to repository
  admins, so no other credential can forge a trigger.
- Both GitHub Environments carry a deployment **tag** policy (`v*` and `staging-v*` respectively)
  and **no required reviewers**: pushing the annotated tag is the approval, and the admin-only tag
  ruleset above is what restricts who can push one. The environments still scope each lane's tag
  policy, which is why the workflow keeps them. The lane is therefore unattended end to end —
  `check`, `-Submit`, `-Verify`, `-Finalize` — and the operator's decision is made once, when the
  tag is pushed. The `verify` and `finalize` jobs declare no environment at all; `finalize` runs
  only after `verify` has reported `SubscribedInstallationVerified`.
- No Steam credential and no repository secret exist for the workflow to leak; the signed-in Steam
  client is ambient state of the operator's own desktop session.
- The privileged jobs use no third-party actions, check nothing out on the Steam host, upload
  nothing from it, and print only stage names, exit codes and SHA-256 digests to the public log.
- Every privileged step re-proves identity in-job: annotated tag object, tag commit equal to the
  `main` tip (public lane) or reachable from `dev` (staging lane), tag version equal to the
  manifest version, and a package receipt digest reproduced independently on a hosted runner.

Accepted deviation, recorded rather than hidden: the privileged job runs this repository's own
scripts under the Steam account, which the "Uploader boundary" guidance below would rather it did
not. The compensations are the fresh clone at a ruleset-protected, ancestry-proven tag and the
hosted-versus-host receipt digest cross-check.

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
These 0.3.1 waivers do not carry forward. The automated lane runs the complete `--alpha` or
`--test` release-check at the tagged commit on every run and requires its clean marker; it inherits
no waiver. Per the author ruling of 2026-09-11 ("no manual test gate for release, forever"),
the startup/save/reload proof is an automated-driver `results.json` artifact (every check
PASS, its process cleanly stopped) bound by hash in `docs/RELEASE_EVIDENCE.json`, never a
human tester's unverifiable word. Credential entry and legal/marketing approval remain human.

Supported target: Caves of Qud v1.0.5, core build 2.0.211.51. Re-run all licensed checks before
claiming compatibility with another build.

## Two public package lanes

`Tools/workshop-package.sh` has three mutually distinct modes:

| Mode | Purpose | Public proof required |
|---|---|---|
| `--test` | Private bootstrap/candidate; local `--test` tooling tolerates `workshop.json` absent | Clean committed package only |
| `--alpha` | Public `0.3.x`, labelled **v0.3 Alpha**; first version is exactly `0.3.0` | Private receipt binding, final preview, structure review, public metadata, annotated tag |
| `--release` | Evidence-complete later lane | Every Alpha gate plus `docs/RELEASE_EVIDENCE.json` and retained automated-driver/native artifacts |

The `workshop.json`-may-be-absent tolerance above is the **local** `--test` tooling only:
`Tools/workshop_metadata.py validate_workshop` returns `None` for a missing file when invoked
directly in `test` mode. The **automated staging lane** in `.github/workflows/release.yml` is
stricter: it requires `workshop.json` to already exist once its own `workshop` subcommand call
has run, and refuses the run naming the missing file (exit 1) rather than failing on an
unhandled `FileNotFoundError`.

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

Freeze `TESTING.md` before this binding commit. Public packaging compares its exact bytes with
the bound candidate and refuses later edits, even though the file is outside the Workshop payload.
Keep changing publication status in `docs/STATUS.md` and the package README; the test protocol
links to that ledger so promotion does not require rewriting the verified protocol.

## 5A. First public v0.3 Alpha — completed `v0.3.0`

This subsection records the one-time first-publication flow. Do not rerun it or recreate its tag;
later Alpha patches use **Updating Alpha**.

Do not create `docs/RELEASE_EVIDENCE.json` for Alpha. It would falsely imply completed final
automated-driver release passes. Instead:

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
Release listing and the automated native driver has recorded every claimed pass (author ruling
2026-09-11: no manual test gate for release, ever; credential entry and legal/marketing approval
remain human).

Copy `docs/RELEASE_EVIDENCE.example.json` to `docs/RELEASE_EVIDENCE.json`. Bind exact release
version, pre-evidence candidate commit, Qud marketing/core build, `Assembly-CSharp.dll` SHA-256,
Workshop ID, preview hash (with capture provenance bound by hash; a human aesthetic reviewer is
optional, never required — see `verification.previewReview` below), private receipt hash, the
native driver's `results.json` (all-PASS, every process cleanly stopped) bound by hash, and the
long-form behavioural scenario artefact (author/Codex addendum, 2026-09-11; schema tightened by
Codex root protocol review the same day) — one automated, reproducible, fixed-seed run through
the EXACT ORDERED chain `startup, quote, paid-commission, engine-turn-build, save, cold-load,
next-action` (never shuffled, duplicated, or missing a step), bound to the exercised
`candidateCommit`, the requested tree's own `Tools/check-structure.py --json` production
structural digest — supplied by the caller via `--inventory-digest`, never read from
`docs/STRUCTURE_REVIEW.json`, which an active candidate keeps stale by design and is checked
separately, on its own freshness terms — and the game build (a distinct, OPTIONAL
`harnessInventorySha256` may also record the launched dev-profile/harness inventory, never
conflated with the production digest), carrying one continuous `save-session` process
(startup..save) and one separate `cold-load-session` process (cold-load..next-action), each
with its own `profileName`/`profileSeal` (the SHA-256 of that session's own closed
`profile.sha256`, header `taf-scenario-profile-seal-v1`) — the two sessions' profiles are
never required to match each other, only each one's own presence and shape is checked —
recorded stopped via `started`/`stoppedUtc`, never a per-step stopped flag, which the schema
refuses outright — continuity identities (realm, city, job, building, plot, save, and the
engine-turn-build completed-receipt linkage `completedReceiptId`/`forJobId`) that are required,
not merely allowed to reappear, once their phase is reached, every step PASS with its own recorded `turnsUsed`/`elapsedSeconds`
at or under its own `turnBudget`/`timeoutSeconds`, and its driver log bound by SHA-256 (schema
documented next to `Tools/workshop_metadata.py`'s `_validate_longform_scenario_results`; the
JSON is parsed with a duplicate-key guard; missing reachability, wrong order, an over-budget
step, or any non-PASS step is unresolved, never waived) — every numbered TESTING pass or
reviewed waiver, and
retained artifacts below `docs/release-evidence/`. Identities and times must be real; placeholders,
a forged human-signature claim authored by automation, missing artifacts, hash drift, a non-PASS or
missing `processStopped` driver entry, unknown pass IDs, duplicate IDs, reordered IDs, or stale
`TESTING.md` all fail. Human names are no longer required anywhere: the public preview
screenshot requires bound non-generative capture provenance (schema documented next to
`Tools/workshop_metadata.py`'s preview-review validation), while `reviewedBy`/`completedUtc`
are optional and may be null under the same author ruling (no manual test gate for release,
ever; credential entry and legal/marketing approval remain human).

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

Author ruling 2026-09-11 extends the "no manual test gate for release, forever" policy to this
step: when the automated publisher (`.github/workflows/release.yml`, the self-hosted runner lane
from the 2026-09-08 ruling below) is used, its own recorded run id plus the `finalize` job's
receipt satisfy title/description/tags/preview/version/visibility confirmation, the post-success
inventory/receipt re-check, the subscribed-bytes verification, and the repeated loader/save-reload
smoke (via the bound `driverResultsRef`/`longFormScenario` artefacts) — none of that is a human
step anymore. A human is needed only where Steam itself prompts: sign-in, Steam Guard, or a terms
acceptance dialog, which the runner surfaces as an exit-4 `NeedsUser` outcome the operator clears.

The steps below remain the LOCAL/MANUAL fallback path (no automated publisher run) and are
unchanged for that path only:

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
git tag -a "v${VERSION}" \
  -m "The Thousand and First v${VERSION} Alpha" \
  -m "What changed for players in this update, in plain words."
./Tools/workshop-package.sh --alpha "/absolute/path/TAF-${VERSION}-alpha"
```

Upload only that new package, then repeat signed-out listing, subscribed-byte, and public-smoke
verification. Never reuse another item's ID, rewrite an existing tag, merge package folders, or
treat a prior receipt as proof of changed bytes. The tag must carry a body: a single `-m` makes
an empty message body, and the pipeline's own `-m "title" -m "changelist"` shape (see
"Updating Alpha — automated lane" below) is what an empty-body tag fails against.

The manual commands above remain valid for proving and packaging a release by hand, but they
are not a way to avoid the automated lane: `.github/workflows/release.yml` triggers on every
`v*`/`staging-v*` tag **push**, with no distinction between a "manual" and an "automated"
tag, so pushing `v${VERSION}` always starts the hosted pipeline too. If the Steam-host runner is
genuinely unavailable, the correct fallback is to let that run fail closed at the
runner-dependent stage, not to delete and re-push the tag around it — the tag ruleset makes
delete-and-retag admin-only precisely so a tag push cannot be quietly redone. The
automated-lane tag command below is the authority for what a real release tag looks like; this
manual block exists to reproduce the same package bytes by hand, not to bypass the pipeline.

### Updating Alpha — automated lane

`.github/workflows/release.yml` runs the same sequence from a tag. **Pushing the tag is the
trigger**, so push it only when the release is ready; there is no separate "start" button.

Tag grammar, and nothing else is accepted:

| Tag | Lane | Item | Package mode | Plan mode | Environment (tag policy) | Approval |
| --- | --- | --- | --- | --- | --- | --- |
| `staging-v<version>` | private staging | `3796495680` | `--test` | `--mode test` | `steam-workshop-staging` | the tag push |
| `staging-v<version>-<K>` | private staging, `K`th re-candidate | `3796495680` | `--test` | `--mode test` | `steam-workshop-staging` | the tag push |
| `v<version>` | public Alpha | `3794797472` | `--alpha` | `--mode alpha` | `steam-workshop` | the tag push |

Neither environment has a required reviewer. The run never waits for a human, so **pushing the tag
is the single irreversible decision**: the admin-only tag ruleset is what stands between anyone and
a release, and the environments survive only to scope each lane's deployment tag policy.

`<version>` is a `0.3.x` patch and never `0.3.0`. `<K>` is a positive integer; the private item
admits an equal-or-higher patch, so a re-candidate reuses the version and increments `K`. The
public lane requires a strictly higher patch. Any other tag shape is refused before any work.

The **change note is the annotated tag's message BODY**, not `CHANGELOG.md`. The tag *subject* is
the release title and is never sent to Steam. The body is validated on the hosted runner as
1–7999 bytes of strict UTF-8 with no control characters except line feed, then reproduced
byte-for-byte on the Steam host and cross-checked by SHA-256. A tag with an empty body fails
closed. `CHANGELOG.md` cannot be the note: its first section was 17,698 bytes at `v0.3.1` against
the publisher's 7999-byte ceiling. Write the body as the truthful player-facing changelist:

```bash
git tag -a "v${VERSION}" \
  -m "The Thousand and First v${VERSION} Alpha" \
  -m "What changed for players in this update, in plain words."
git push origin "v${VERSION}"
```

Order of a full 0.3.x release:

1. Bump `manifest.json` to the new patch, fold every `Unreleased` section into one
   `## [<version>] — YYYY-MM-DD (Alpha)` heading, and complete the private canonicalization
   commit on `dev` (`workshop.json` Visibility `"0"`, WorkshopId `3796495680`). If any C# source
   changed since the last review, commit a refreshed `docs/STRUCTURE_REVIEW.json` naming the author-authorized
   reviewer, so `python3 Tools/check-structure.py --release` exits 0. Both lanes run that command
   on the hosted runner within seconds of the tag push, and stage 11 of the licensed gate runs it
   again on the Steam host; a stale review stops the release before the licensed gate is burned.
2. Push `staging-v<version>` on that commit. Nothing waits for an approval. The pipeline gates,
   packages `--test`, plans, checks, submits, verifies and finalizes against the staging item.
3. Confirm the `finalize` job's recorded finalization SHA-256, run the section-4 automated
   startup/save/reload driver against the subscribed candidate (writing its all-PASS
   `results.json`), then bind the package receipt into `docs/PRIVATE_PACKAGE_RECEIPT.sha256`.
   That binding commit is `candidateCommit`. This Alpha lane still carries no
   `docs/RELEASE_EVIDENCE.json`; the driver artifact matters for 5B evidence-complete releases.
4. Public flip commit: canonicalize Alpha metadata, status line, changelog heading and a fresh
   `docs/ALPHA_CANDIDATE.json`. Open the release pull request from `dev` to `main` and merge it
   **with a merge commit** to preserve `candidateCommit` ancestry. Merge commits are enabled
   (verified 2026-09-10). For an isolated main-based hotfix, use the documented main PR and
   normal backmerge to dev; never pull unfinished dev features into the hotfix merely to make
   the candidate reachable. See "Release pull requests require a merge commit" below.
5. Tag the resulting `main` commit with an annotated `v<version>` carrying a body, and push it,
   **immediately** after the merge and before anything else lands on `main`: the public lane
   compares the tagged commit against the `origin/main` tip as the job reads it, not as it stood at
   trigger time, so a push to `main` while a release run is queued behind the concurrency group
   turns a legitimate release into a refusal. Never cancel a running `publish` or `finalize` job.
6. Once the `finalize` job is green, its run id and receipt satisfy the section-6 post-upload
   checklist automatically; a human is needed only if the run itself reports a `NeedsUser`
   Steam sign-in/Guard/terms prompt.

`SubmittedUnverified` is not delivery, so the pipeline does not stop there. A `verify` job polls
`-Verify` for up to 20 minutes, and **only** when it reports `SubscribedInstallationVerified` does
a `finalize` job run `-Finalize` — from the same run directory, with the plan path, `PLAN_SHA`,
item, change note and `RECEIPT_SHA` read back from the recorded `inputs\handoff.env`, never
rebuilt, into a fresh `evidence-finalize-<attempt>` directory. It requires exit 0,
`SubscribedInstallationVerified`, `attemptFinalized=true` and a non-null `finalizationSHA`; every
other outcome fails closed with its meaning from
[PUBLISHING.md](../Tools/WorkshopSteam/PUBLISHING.md). Finalization still writes immutable records
and still needs the identical plan, package and paths, and a bad finalization still poisons the
item's history — which is why the job never retries and why finalizing by hand from the retained
run directory (runbook step 15) remains the fallback when it cannot run.

If any privileged step fails, **do not re-run the workflow for that tag.** `submit` creates its
attempt directory before the SDK is initialised, so a refusal at or after that point retains an
attempt — possibly an empty one — and an empty or partial retained attempt fences the item for
every later `check`, `submit`, `-Verify` and `-Finalize`. No tool in this repository clears it.
Reconcile it by hand; the exit table and the retained-attempt rules are in
[PUBLISHING.md](../Tools/WorkshopSteam/PUBLISHING.md).

## Local automated-upload implementation and deployment design

**Corrected private and public0.3.1 upload/installation finalized; one client per invocation.**
Exact current evidence: [STATUS.md](STATUS.md). The privileged workflow
`.github/workflows/release.yml` is authored under the 2026-09-08 ruling above; the GitHub
Environments `steam-workshop-staging` and `steam-workshop` and the `v*`/`staging-v*` tag ruleset
are configured, and fork pull-request workflows require approval for all external contributors.
The `taf-steam` runner is **not registered yet**, so no pipeline release has run. Record the first
staging and first public pipeline run ids in
[current status](STATUS.md#automated-release-lane) as they happen. Local Alpha automation remains
authorized once the exact candidate and private-item checks pass; it does not depend on the
pipeline.

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

This is the branch model **in force**, not a proposal. `dev` exists on origin, is protected and is
the repository default branch; `main` is protected and release-only.

- `dev` is the default integration branch. Feature branches and isolated worktrees target `dev`;
  normal CI runs there and on pull requests. As verified through GitHub on 2026-09-10, protection
  requires `repository-audit` and `full pure + portable tests (ubuntu-latest)`, no force-push
  and no deletion. Linear history is not required; merge commits and squash are enabled.
  It is currently weaker than `main` in two respects — `enforce_admins` is off and the strict
  up-to-date requirement is off — which is a deliberate maintainer convenience, not an oversight.
- `main` stays protected and release-only, with the same required checks plus strict up-to-date
  and `enforce_admins` on. Linear history is not required. The release workflow still exercises
  both supported hosted operating systems; that is separate from the integration requirements.
- A release pull request moves one frozen, reviewed `dev` tree to `main` without changing package
  bytes. Direct pushes and force-pushes stay disabled.
- Tag the exact resulting `main` commit with an annotated `v<version>` tag. The public lane of the
  automated pipeline requires that the tagged commit **is** the `origin/main` tip, not merely an
  ancestor of it, so an older `main` commit carrying the new manifest version cannot be released by
  mistake. Staging tags must be reachable from `origin/dev`.
- Merge the tagged `main` commit back into `dev` before new integration work. A hotfix starts from
  the affected `main` tag, follows the complete patch-release proof, then returns to `dev`.
- Never treat a branch name, mutable artifact, or successful CI run as a release identity. Version,
  candidate commit, annotated tag, package receipt, Workshop ID, and subscribed bytes must agree.

Links that assume `main` — the `blob/main/...` URLs in `README.md`, `SUPPORT.md`, `CHANGELOG.md`
and the shipped `workshop.json` description — stay correct under this model, because `main` remains
the release-only branch and those links are meant to point at released documents. That audit
discharges the old precondition for flipping the default branch.

#### Release pull requests require a merge commit

A release pull request from
`dev` to `main` must preserve the exact commit SHAs of the frozen tree, because the packager's
`--alpha` mode refuses unless the candidate record's `candidateCommit` — the receipt-binding commit
created on `dev` — is an ancestor of the tagged `main` commit.

GitHub's squash and rebase merge methods both rewrite commit SHAs, so either one breaks that
ancestry. Verified on 2026-09-10: `allow_merge_commit=true`, `allow_squash_merge=true`,
`allow_rebase_merge=false`, and neither protected branch requires linear history. Use an ordinary
merge commit for release and hotfix promotion; do not squash away a candidate's receipt binding.
This records the existing repository configuration, not permission to alter its protection.

An isolated hotfix may be assembled through ordinary PRs onto `main`, then back-merged through
a PR into `dev` so its private-candidate commit is reachable there before a staging tag is pushed.
Bind private installed evidence in a later commit, then make the public-only metadata flip and
tag the exact resulting `main` tip. Every intermediate PR retains normal required checks.
Unfinished `dev` features must not be merged into the isolated hotfix merely to gain staging
ancestry; merge the hotfix outward instead. Never push a staging or public tag before that lane's
proof is complete, and never treat an upload callback as installed-delivery verification.

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
subscribed-byte receipt, the automated native load/save/reload driver evidence, the automated
signed-out page/listing inspection, or the preview-media provenance artefact of sections 4 and 6 —
all of which are automated-driver/artefact requirements, not human steps, since the 2026-09-11
ruling; a human is needed only for an actual Steam sign-in/Guard/terms prompt (`NeedsUser`).

### Self-hosted runner security

> **Amended 2026-09-08 by author ruling, for `.github/workflows/release.yml` only.** The
> prohibition in the next paragraph and the "never use ... an automatic tag trigger" clause below
> are retained verbatim as the standing rule for every other workflow, and are superseded only for
> that one file. The risk they describe is real and is accepted, not disproved: a repository-level
> runner can be targeted by any workflow in this repository, runner groups are unavailable on a
> personal account, and environment approval gates *when* a job runs, not *what* reaches the
> machine. The mitigations and the accepted deviation are listed under "Author ruling 2026-09-08"
> at the top of this document. Do not read this amendment as permission to attach a runner for any
> other purpose, to run it as a service, or to leave it running outside a release window.

The Steam session is a publication credential. A credentialed self-hosted runner must not be
attached to this public source repository: another workflow can target its labels without declaring
the protected environment. Environment approval alone is not isolation. GitHub warns that
self-hosted runners are persistent and can be compromised by untrusted workflow code.

- Prefer a private release-control repository. Restrict its runner group to that repository and the
  exact allowlisted upload workflow at a pinned SHA where the GitHub plan supports workflow-scoped
  access. Otherwise keep the Steam host offline/attended instead of registering it as an Actions
  runner. The workflow still declares an environment whose deployment tag policy scopes each lane.
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
- Require a distinct, recorded confirmation of what a Public release covers. On any mismatch,
  keep/move the item Private and follow Recovery; never retry by creating a new item.

How the deployed workflow satisfies the surviving bullets: it pins every third-party action by full
commit SHA and uses none at all on the Steam host; it holds `permissions: contents: read` and no
secrets; it exposes the Steam host to no fork artifact, because nothing is checked out or
downloaded there; its `workflow_dispatch` path requires the full candidate SHA as an input and
refuses unless that SHA is the tag's commit; and it implements the Public confirmation as a
separate hosted `public-confirm` job, which records the exact tag, commit, version and digests the
public lane is about to publish before any Steam-host work begins.

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

#### Gate status as of 2026-09-08

The gates are retained above exactly as written. Their status under the 2026-09-08 ruling:

| Gate | Status | Evidence or what is missing |
| --- | --- | --- |
| 1 — pinned SDK binaries and reproduced initialization | **evidenced** | `Tools/WorkshopSteam/sdk.lock.json` pins both DLL hashes against core build 2.0.211.51; the launcher refuses on mismatch, and the read-only probe reproduced `SteamAPI.Init` on the host. |
| 2 — sacrificial-item fault cases | **OPEN** | Timeout, rejected setter, wrong owner, legal-agreement, network-loss, duplicate key-value-tag and visibility-inversion cases are covered by the launcher and C# suites as *fixtures*, not against a sacrificial live item. The author must either declare this satisfied by the 0.3.1 private and public runs or leave it open. |
| 3 — both key-value tags verified, clean-client byte match | **evidenced** | Recorded for corrected private 0.3.1 and for public 0.3.1 in [STATUS.md](STATUS.md). |
| 4 — threat model and privileged-runner design review | **evidenced** | The design was written, adversarially reviewed and corrected before this workflow landed; the surviving risks are listed under "Author ruling 2026-09-08". |
| 5 — attended private update in parallel with the Qud UI path | **OPEN** | No such parallel comparison is recorded anywhere. The author must declare it satisfied or leave it open. |
| 6 — separate allowlisted Private staging item | **evidenced** | Item `3796495680` is established, allowlisted and used; the permitted staging-to-production delta is enforced by the packager and the plan builder. |
| 7 — uploader/workflow added in a separate reviewed change | **in progress** | This pull request is that change: it adds `.github/workflows/release.yml`, revises the opening statement, and records the runbook. It is complete when the pull request merges. |

Gates 2 and 5 are open by record. Closing them is an author decision, and the first staging
pipeline run is the natural place to gather the evidence.

### Steam host runner runbook

Preconditions on the gaming PC, all already proved by the 0.3.1 releases: a Windows desktop session
for the account that owns items `3794797472` and `3796495680`; the Steam client running and signed
in under that **same** Windows account; licensed Caves of Qud at
`F:\SteamLibrary\steamapps\common\Caves of Qud` at core build 2.0.211.51 with DLLs matching
`Tools/WorkshopSteam/sdk.lock.json`; .NET SDK 9.0.306 on the Windows PATH as `dotnet.exe`; a WSL2
Ubuntu default distro under that same Windows account holding the gate toolchain; the registry root
`C:\taf-workshop-state.dRBivM`; and both Workshop items subscribed on this client, because
`-Verify` and `-Finalize` read a subscribed installation. The runner account also needs **write
access at the `C:` drive root**: stage 5 of `Tools/release-check.sh` changes directory to `/mnt/c`,
and stage 8 creates and removes its boundary fixtures (`/mnt/c/taf-smoke.*` and
`/mnt/c/taf-smoke.junction*`) there. Do **not** create a service account and do not plan to run the
runner as a Windows service.

1. **Create the run roots.** In PowerShell: `New-Item -ItemType Directory -Path 'C:\taf-release'`.
   Inside WSL: `mkdir -p ~/taf-release`. The run root must sit on a local, non-network drive with
   no junction or symlink in its ancestry; the launcher refuses linked paths.
2. **Create the dry-run deployment target.** Stage 10 of the release check runs
   `Tools/stage.sh deploy` as a dry run, and that call validates a live deployment target: an
   ordinary directory whose `manifest.json` declares `r_ThousandAndFirst`. The least invasive
   precondition — and the one the workflow expects — is a dedicated target that is **not** the
   game's live Mods folder, so the gate never depends on a local mod copy competing with the
   subscribed Workshop item:

   ```powershell
   New-Item -ItemType Directory -Path 'C:\taf-release\live-mod\ThousandAndFirst' -Force
   Copy-Item '\\wsl.localhost\<distro>\<repo path>\manifest.json' `
     'C:\taf-release\live-mod\ThousandAndFirst\manifest.json'
   ```

   The workflow forwards that path as `TAF_LIVE_MOD` and fails closed in preflight, with the
   remedy in the message, if the directory or its manifest is missing. Never point it at the live
   Mods folder: that would resurrect the "remove local copies, subscribe" conflict in section 6.
3. **Verify WSL forwarding** from `cmd.exe`:
   `set TAF_PING=1 && set WSLENV=TAF_PING && wsl.exe -e bash -c "echo wsl-ok $TAF_PING; command -v python3 git sha256sum wslpath"`.
   Expect `wsl-ok 1` and four tool paths. `WSLENV` is how the workflow passes its `TAF_*` variables
   into bash; `WSL_UTF8=1` keeps `wsl.exe` output UTF-8.
4. **Download the runner** into a drive-root folder and verify its published SHA-256 *before*
   extracting. Take the version and hash from the GitHub Actions runner releases page, and record
   in the pull request which version was installed.
5. **Mint a one-hour registration token** from any machine with `gh`:
   `gh api -X POST repos/AussieWarGod/thousand-and-first/actions/runners/registration-token --jq .token`.
   Never store it anywhere durable.
6. **Configure the runner (non-interactive `config.cmd`, run from the desktop session):** the
   runner must **run** in the author's interactive desktop session — never as a Windows service —
   but this one-time `config.cmd` step itself is non-interactive: `--unattended` supplies every
   answer, with no `--runasservice` and no `--windowslogonaccount`:
   `.\config.cmd --url https://github.com/AussieWarGod/thousand-and-first --token <TOKEN> --name taf-steam-gamingpc --labels taf-steam --work _work --unattended --replace`.
   The `self-hosted`, `windows` and `x64` labels are added automatically; the workflow targets
   `[self-hosted, windows, taf-steam]`. `--ephemeral` would accept exactly one job, and a release
   run has three self-hosted jobs (`publish`, `verify` and `finalize`), so use it only if you
   accept re-registering between them.
7. **Start it by hand in the desktop session where Steam is running**, from a **non-elevated**
   console, under the same Windows account as the WSL distro: `cd C:\actions-runner ; .\run.cmd`.
   Elevation is a real failure mode, not a nicety: Valve lists a different administration access
   level as a `SteamAPI_Init` failure, and an administrator terminal is an easy operator slip.
   `wsl.exe` is per-user, so the runner and the provisioned distro must be the same account.
   Confirm with
   `gh api repos/AussieWarGod/thousand-and-first/actions/runners --jq '.runners[] | {name, status, busy}'`.
8. **Prove the toolchain before the first real run**, without mutating Steam: run
   `Tools/workshop-steam-probe.ps1` with `-QudRoot`, `-ItemId 3796495680` and a fresh empty
   `-EvidenceRoot`, expecting exit 0 with `ownerMatch` and `itemMatch` true; then run
   `Tools/test-workshop-upload.ps1 -EvidenceRoot <fresh empty directory>` for the SDK-free launcher
   and C# suites. This also pre-warms the NuGet cache, which matters because every launcher mode
   recompiles its helper inside a 210-second budget.
9. **Per release window:** start Steam and confirm the signed-in owner; start `run.cmd`; push the
   tag; then do the remaining human steps once the run is green. Nothing waits for an approval, so
   the tag push is the point of no return. Never cancel a running `publish` or `finalize` job and
   never press Ctrl+C in the runner window while one runs — a killed launcher child is an uncertain retained attempt. Disable sleep and hibernate
   for the window, and consider pausing Qud's Steam auto-update: a game update changes the SDK
   hashes and fails every run closed until `sdk.lock.json` is deliberately re-pinned.
10. **Both Workshop copies of `r_ThousandAndFirst` cannot be enabled together.** `-Verify` and
    `-Finalize` need the lane's item subscribed, so during a staging window both items may be
    subscribed at once. Disable one in the Qud mod manager before any in-game smoke test.
11. **Time the gate.** The licensed gate's wall time has never been recorded, so the workflow's
    240-minute job timeout is an unverified ceiling. Measure it on the first staging run and adjust
    the timeout before trusting it; a timeout that lands mid-submit is uncertain, not a clean
    failure.
12. **Stop the runner outside release windows.** Ctrl+C in the `run.cmd` window stops it. To remove
    it entirely, mint a remove-token
    (`gh api -X POST repos/AussieWarGod/thousand-and-first/actions/runners/remove-token --jq .token`)
    and run `.\config.cmd remove --token <TOKEN>`.
13. **Evidence rotation is manual.** Each run leaves `C:\taf-release\run-<id>-<attempt>\` (package,
    plan, note, logs, redacted artifact, evidence directories) and a clone under `~/taf-release` in
    WSL. Keep the run directory of every *submitted* attempt until its `-Finalize` has succeeded,
    because finalization needs the identical plan, package, receipt and paths — the `finalize` job
    reads them straight back out of `inputs\handoff.env`. Delete only fully finalized or
    never-submitted run directories. Never touch the registry root.
14. **The `verify` job may go red, and that is not a failed release.** It polls `-Verify` for up
    to 20 minutes after submit, but Steam builds and delivers the new bytes on its own schedule and
    may take longer. If it exhausts that budget, the submission still stands and no record was
    written. Re-run **only that job** — "Re-run failed jobs" — once Steam reports the update live.
    Never use "Re-run all jobs": that re-burns the multi-hour licensed gate and then stops at the
    retained-attempt fence. A re-run of `verify` alone carries `finalize` with it, because
    `finalize` gates on that job's reported status. Verifying and then finalizing by hand from the
    retained run directory is equally valid.
15. **Finalize by hand — the fallback only.** The `finalize` job does this automatically after a
    green `verify`, using exactly these inputs. Do it by hand only when that job could not run or
    failed closed, and only after reading its step summary: run the launcher named in
    `inputs\launcher-win.txt` with the same `-PlanPath`, `-PlanSHA`, `-ItemId`, `-ChangeNotePath`
    and `-ReceiptSHA` from `inputs\handoff.env`, a **new empty** `-EvidenceRoot`, and `-Finalize`.
    Success is exit 0 with `SubscribedInstallationVerified`, `attemptFinalized=true` and a non-null
    `finalizationSHA`. The next submit to that item is refused until this is done.

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
