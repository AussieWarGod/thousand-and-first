# Local Workshop publisher

Implemented locally; validation incomplete. Native SDK access, strict publisher compilation,
SDK-free protocol/package/attempt tests and a live read-only version-refusal check are verified.
Private staging item `3796495680` was created with user approval on 2026-09-06 and verified Private
through an uncached owner/app/item query. Only its staging title and visibility were submitted;
it contains no uploaded mod content. Public Alpha remains `3794797472` at `0.3.0`.
The publisher has not yet uploaded a private candidate. This is not release approval.
Follow [the release gates](../../docs/RELEASING.md) before any submission.

## Boundaries

- Windows, running signed-in Steam, licensed Qud, installed SDK matching `sdk.lock.json`.
- Existing items only. Public Alpha is `3794797472`; private staging is `3796495680`.
  Each invocation must freshly verify its exact lane/owner. No creation, login, subscription
  or download calls in this publisher.
- The Python package planner validates exact receipt-bound bytes and canonical Qud metadata;
  the Windows helper rechecks and holds files read-only through completion. A plan is not proof
  of a clean commit, release tag, game behavior or private playtest.
- Qud visibility `"0"` maps to Steam Private `2`; Qud `"2"` maps to Steam Public `0`.
  Existing remote visibility must already match the lane; this tool does not promote one item
  from private to public or hide production to test it.
- Every update replaces both `manifest_id` and `manifest_version` key-value tags, alongside
  content, title, description, normal tags and preview. Never edit Steam's local `.acf` manifests.

## Inputs

First obtain a fresh clean-candidate package through `Tools/workshop-package.sh` in the correct
lane. Put the immutable package and its external `.sha256` receipt on a native mounted drive.
Generate the plan with `Tools/workshop_upload_plan.py`, supplying exact package/receipt paths,
mode, existing item ID and manifest version. Keep the resulting plan and its SHA-256 together.

`Tools/workshop-steam-upload.ps1` requires `QudRoot`, `PlanPath`, `PlanSHA`, `ItemId`,
`ChangeNotePath`, `ReceiptSHA` and `EvidenceRoot`. `-StateRoot` (alias `-AttemptRoot`) is optional
and defaults to the compiled fixed root; any other value is refused by the launcher itself. Use
ordinary native Windows paths; the plan's package paths use `/mnt/<drive>/...`. Evidence must be a
fresh separate folder, outside package content and outside the registry root.
Use a truthful UTF-8 changelist. Do not place credentials in any input.

Without `-Submit`, the launcher only checks the leased package and remote authority. It does not
create an attempt, start an update or submit. After all release gates pass, `-Submit` explicitly
enables one attempt. A per-item active receipt records the exact version and is flushed before
submission, then retained through completion. The registry, not the caller, decides where that
receipt lives, and the per-item kernel mutex - not `CreateNew` alone - is what makes one attempt
exclusive. An existing receipt requires reconciliation, never an automatic retry. Preserve it as
immutable history when delivery is verified; do not clear it merely because submission returned
success.

## Fixed registry root

The publisher admits exactly one release-state root, compiled into
`WorkshopReleaseRegistry.StateRoot`: `C:\taf-workshop-state.dRBivM`. There is no caller-selected
state root. `ATTEMPT_ROOT` (`args[5]`) survives only as the strict seventh-argument shape: the
helper compares it **byte-exactly** - ordinal, unnormalised, no filesystem probe - against that
constant, and refuses `state_root_refused` before any SDK call, item mutex, marker initialization,
attempt lease or file read. The launcher passes the compiled literal verbatim and refuses any other
`-StateRoot` before it probes a path, verifies the SDK lock, builds, or starts the helper.
Changing roots used to be the way to bypass an unresolved attempt; that route is closed, not policed.

## Item mutex scope

Every invocation takes the per-item kernel mutex (`WorkshopItemLock`, `Global\taf-workshop-item-<item>`)
by opening the registry, and holds it for the whole run:

1. registry open and immutable marker initialization,
2. the retained-attempt decision,
3. port construction and `Prepare`/`Revalidate` (check), or the attempt directory, protocol and
   aftermath (submit),
4. port disposal, then package disposal, then registry disposal.

Cleanup runs those three disposals through `UploadCleanup` in that order, attempting each even when
an earlier one throws; the registry's disposal releases the mutex last. An observed abandoned mutex
is reported `lock_abandoned_uncertain` and stops the run: it records that some earlier holder ended
without releasing, which is uncertainty for reconciliation, never permission to submit again. The
publisher never infers liveness from a process id or an age.

## check, submit, inspect

`check` (the default) may initialize the immutable marker, names the attempt path the port would
use, and creates **no** attempt: it proves package readiness and fresh remote authority only.

`submit` creates the durable attempt directory through the registry **before** `UploadProtocol.Run`,
and retains it on every outcome - success, refusal, timeout, exception or cleanup failure. Nothing
in this tool deletes or moves an attempt.

`inspect` (`-Inspect`) prints held marker SHA-256 and physical directory identities, plus at most
64 immediate retained-entry names (each at most255 characters, strict UTF-8, no controls). Child
entries are never opened or followed; names prove no per-file identity, content or evidence chain. It
makes no SDK call and creates no attempt. It refuses `attempt_retained` when any attempt is
retained, and a retained attempt refuses `check` and `submit` the same way, without creating
anything.

The canonical `<item>.active.attempt.json` filename and the receipt's historical
`packagePath == contentPath` pairing are unchanged; only the directory holding them moved into the
registry, at `<registry root>\registry\<item>\attempts\0001\`.

## Statuses and exit codes

| Exit | Status | Meaning |
| --- | --- | --- |
| 0 | `checked_not_submitted`, `inspected`, `SubmittedUnverified` | check ready, inspection clean, or a submission Steam accepted with matching metadata. Never delivery. |
| 2 | `refused` | argument, note, plan, receipt or package refusal before any submission was possible. |
| 3 | `check_refused` | package or remote authority not ready. No attempt exists. |
| 4 | `NeedsUser` | the SDK needs the operator, for example the Workshop legal agreement. |
| 5 | `Uncertain`, `Rejected` | a submission may have reached Steam. The attempt is retained. |
| 6 | `state_root_refused` | `ATTEMPT_ROOT` is not the compiled fixed root. Nothing was read or written. |
| 7 | `attempt_retained` | an attempt is retained for this item. Reconcile it; this tool never retries. |
| 8 | `lock_abandoned_uncertain` | the item mutex was abandoned by an earlier holder. Uncertainty, not retry. |
| 10 | `cleanup_uncertain` | cleanup failed; the proven outcome is retained under `outcomeStatus`. |

Exit 9 stays the launcher's own "helper did not report" sentinel; the helper never returns it.
Every report carries `retryAuthorized: false` and `delivered: false`, and prints exception **type
names** only - never a message, native path, SDK diagnostic or account id.

## Not yet

- **No finalizer.** Nothing here advances an attempt to verified, installed, accepted or archived.
- **No later attempt after a retained one.** `attempts/0001` is the only attempt this code creates;
  a retained attempt is terminal for the tool and is resolved by an operator, not by a rerun.
- **No legacy import.** Receipts written under an older caller-selected root are not migrated, read
  or counted. Reconcile them by hand before trusting the registry's attempt count.
- No durable abandoned-lock record, no subscribed-delivery proof, no acceptance claim, and no
  authority granted by parsing any observation.

## Outcomes and tests

`SubmittedUnverified` is not delivery. Verify remote metadata, listing/preview, exact Steam-installed
receipt, and fresh native load/save/reload behavior. On a timeout or I/O failure, treat the attempt
as uncertain: it might already have succeeded. On a legal-agreement prompt, stop for the user.
Never erase an attempt receipt to make a retry pass.

Run `Tools/test-workshop-upload.ps1 -EvidenceRoot <fresh-native-directory>` for SDK-free Windows
tests. The separate read-only `workshop-steam-probe.ps1` can inspect an existing item's ownership
and manifest tags without changing anything. Keep build/test evidence local; SDK binaries are
copied only to local build output and are not distributed with the mod.

The test launcher runs `protocol`, `package`, `attempt`, `evidence`, `record`, `observation`,
`aftermath`, `cleanup`, `lock`, `lease`, `registry` and `cli` suites,
which are registered in `WorkshopUploadTests.csproj`, dispatched by `UploadTests.cs`, and runnable
directly as `dotnet TafWorkshopUploadTests.dll <suite>`. These cover canonical hostile-input parsing, legal-before-I/O
classification, concurrent callback/settlement latching, real Windows write-once file leases,
persistence-before-verification and independent cleanup. No suite initializes Steam.
Fresh integrated Windows tests and strict publisher compilation pass with zero warnings/errors;
exact evidence and remaining gates are linked from [current status](../../docs/STATUS.md).

After the protocol returns, the publisher writes the frozen outcome to
`<item>.active.attempt.json.submission.json`, bound to the exact held attempt bytes. It flushes,
reads back and leases that file before content or remote metadata checks. Callback absence remains
Unknown/TimedOut, not proof that Steam received nothing. Write failure preserves any partial file
and the active attempt; later failures never erase either. Final output is emitted only after
port and package cleanup; each cleanup action is attempted even if an earlier action throws.
These local tests do not prove an actual Steam callback or subscriber delivery.

Fixed-registry enforcement passes strict four-project compilation and all twelve Windows suites,
including14 CLI,11 item-lock,11 registry and12+9 lease groups. Delivery15package+14installed groups
and436 Python Tools tests pass. [Exact evidence](/tmp/taf-publisher-integration.w9HGSj/README.md).
No real registry or SDK access occurred in those tests. Finalization,
subscribed delivery and ordinary acceptance remain incomplete. A parsed observation never clears an
attempt or grants retry rights.

For a separate bytes-only check, run `Tools/workshop_installed_bytes.py` with `--installed-root`,
`--receipt`, `--receipt-sha`, `--mode`, `--expected-item` and `--expected-version`. The selected
folder must end in `steamapps/workshop/content/333640/<item>`; the approved receipt stays outside
that folder. The tool reuses the closed package validator, rejecting changed, missing or extra
files. It makes no SDK calls and never reads or changes `.acf` files. Its result always leaves
subscription, fresh-transfer and delivery verification false: a standard folder containing matching
cached bytes does not prove current Steam state. This check cannot clear an active attempt.

Creating a new staging item is a separate bootstrap operation. Valve's
[CreateItem API](https://partner.steamgames.com/doc/api/ISteamUGC#CreateItem) has no visibility
argument; initial privacy for an existing contributor is not guaranteed by its documentation.
Do not assume atomic-private creation or reuse the public item for bootstrap.
The approved one-time bootstrap has already completed for `3796495680`; reuse that identity,
not a second creation. Its persistent creation fence and receipts must be retained.
