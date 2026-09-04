# v0.3 Alpha Freeze Record

The first public Steam Workshop playtest, `0.3.0` / **v0.3 Alpha**, completed on 2026-09-03 at
commit `999b1e4` and annotated tag `v0.3.0`. It is published as
[Workshop item 3794797472](https://steamcommunity.com/sharedfiles/filedetails/?id=3794797472).
This is a historical identity/package record, not production-final evidence and not instructions
to retag `0.3.0`.

## Completed state

- `manifest.json` is `0.3.0`; later beta-tree changes do not alter the tagged release bytes.
- `workshop.json`, `docs/PRIVATE_PACKAGE_RECEIPT.sha256`, `docs/ALPHA_CANDIDATE.json`, and the
  annotated `v0.3.0` tag record the completed private-to-public sequence.
- `docs/STRUCTURE_REVIEW.json` was bound to the tagged staged C# inventory; any later production
  source change requires a new census and review binding before another package.
- `preview.png` has a native-capture provenance record in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
  Its recorded bytes survived private candidate, public Alpha commit, and package receipt.

This list states protocol state, not implementation completeness. [STATUS.md](STATUS.md) remains
the authority for current feature/test gaps.

## Historical freeze criteria

The one-time freeze was governed by these conditions:

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

## Completed one-time identity freeze

The freeze changed these identities together through the reviewed private/public sequence:

- `manifest.json`: version `0.3.0`, title `The Thousand and First [ALPHA]`;
- `README.md`: current public-Alpha status and Workshop link;
- `CHANGELOG.md`: dated `0.3.0` Alpha release heading;
- Workshop metadata: canonical Alpha text, Private candidate first, then Public; and
- any version-pinned tests/docs that intentionally bind current release identity.

That sequence is complete. A later code/content change does not inherit its receipt: use a new
semantic patch version and repeat the current [RELEASING.md](RELEASING.md) update flow. Never move
or recreate `v0.3.0`.

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
