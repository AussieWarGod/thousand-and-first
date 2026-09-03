# v0.3 Alpha Freeze Plan

Target: one public Steam Workshop playtest numbered `0.3.0` and labelled **v0.3 Alpha** everywhere.
This is not the production-final release. Do not bump `manifest.json`, create the public tag, or
upload while this plan says pre-freeze.

## Current state

- `manifest.json` remains `0.2.0` by design.
- Working integration changes are not frozen or packageable.
- `workshop.json`, `docs/PRIVATE_PACKAGE_RECEIPT.sha256`, `docs/ALPHA_CANDIDATE.json`, and the
  annotated `v0.3.0` tag are expected to be absent before the Steam/private-candidate sequence.
- `docs/STRUCTURE_REVIEW.json` must be authored against the final staged C# inventory; a missing or
  stale review blocks Alpha packaging.
- `preview.png` has a native-capture provenance record in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
  Its exact final bytes must survive private candidate, public Alpha commit, and package receipt.

This list states protocol state, not implementation completeness. [STATUS.md](STATUS.md) remains
the authority for feature/test gaps until freeze.

## Freeze decision

Freeze only when all conditions are true:

- architecture, quickstart, runtime, save-format, documentation, and package-boundary owners have
  fanned in;
- no unresolved release-blocking test, native compile/load, source-map, missing-binding, asset,
  structure, or player-entry defect remains;
- supported Qud target is still v1.0.5/core 2.0.211.51, or every compatibility claim and native
  receipt has been rerun for a new target;
- final `preview.png` is truthful, native, provenance-complete, 512×512, under the Steam limit, and
  legible at thumbnail scale;
- public docs contain no stale census, historical test count presented as current proof, internal
  path needed by players, or unperformed-pass claim; and
- `git status --short` is empty at each immutable package boundary.

## One-time identity freeze

At freeze, change together in one reviewed commit:

- `manifest.json`: version `0.3.0`; keep title `The Thousand and First [ALPHA]`;
- `README.md`: keep one honest pre-release status line stating that public Alpha has not shipped;
- `CHANGELOG.md`: keep `0.3.0` under `[Unreleased]` without a dated release heading;
- Workshop metadata: canonical Alpha text and private visibility first; and
- any version-pinned tests/docs that intentionally bind current release identity.

Do not perform this bump early. A later code/content change invalidates the private receipt and
forces a new candidate. Write `**Status: 0.3.0 public Alpha playtest.**` and
`## [0.3.0] — YYYY-MM-DD (Alpha)` only after subscribed private validation, when RELEASING section
5A creates the public metadata/candidate commit.

## Required gates

Run serially from a clean checkout:

```bash
./Tools/portable-check.sh
./Tools/release-check.sh --test
```

`release-check.sh` requires licensed installed Qud data, exact native compilation, zero skipped
release tests, assets/references, package test, deploy dry run, smoke checks, and current structural
review. Automation is not a human playtest; Alpha truthfully defers the final signed/native-human
`docs/RELEASE_EVIDENCE.json` record.

Then follow [RELEASING.md](RELEASING.md): private item → subscribed private test → immutable receipt
binding → public Alpha metadata/record → annotated tag → `--alpha` package → upload → subscribed
public smoke.

## Alpha machine record

After private subscription testing, copy the exact package receipt to
`docs/PRIVATE_PACKAGE_RECEIPT.sha256` and commit it. That receipt-binding commit is
`candidateCommit`. Public Alpha then uses a separate machine-only record copied from
`ALPHA_CANDIDATE.example.json` to `docs/ALPHA_CANDIDATE.json`.

It binds only facts tools can prove: channel/version, private candidate commit, supported game
target, Workshop ID, preview hash, and private package receipt hash. It contains no tester name,
manual pass, subjective approval, or fabricated final evidence. `Tools/workshop-package.sh --alpha`
verifies the record from tagged `HEAD`, reconstructs the private candidate from Git, and refuses
runtime, inventory, mode, preview, Workshop ID, structure, tag, or receipt drift.

## Go/no-go and recovery

Go only when `--alpha` prints `WORKSHOP PACKAGE CLEAN`, mode `alpha`, version `0.3.0`, title with
`[ALPHA]`, and channel `v0.3 Alpha`. The command never authenticates or uploads.

Immediately before tagging/package creation, rerun `./Tools/release-check.sh --alpha` from the clean
public-candidate commit. `--test` is only the private lane; it cannot sign public visibility.

If any post-tag check fails, do not move or rewrite the tag. Fix forward, repeat the private
candidate sequence, and use the appropriate next semantic version. If a public build is unsafe,
make the Workshop item Private, preserve failed tag/receipts, and publish a new patch after proof.
