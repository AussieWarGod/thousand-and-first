# Local Workshop publisher

## Standing release rule — 2026-09-11

The author permanently removed manual test gates for every release, including 0.3.3,
Beta, and production. Automated checks and truthful machine-verifiable evidence remain
mandatory; manual testing is optional and never requires a recurring waiver. This
supersedes historical manual-test requirements and one-release waiver limits below.
Authentication, Steam Guard, legal acceptance, immutable custody, and subscribed-byte
verification are unchanged. Never invent human approvals or claim untested coverage.
See [the standing ruling and current evidence](../../docs/RELEASING.md#standing-author-ruling--2026-09-11).

Public Alpha `0.3.1` is published to item `3794797472`: submit55498 TERMINAL0 at13:18 UTC
reports `SubmittedUnverified`, `metadataMatches=true`, `contentUnchanged=true`, attempt `0001`.
Finalizer55266 TERMINAL0 reports `SubscribedInstallationVerified`, `reason=null`,
`attemptFinalized=true`. Strict Alpha package68272 and native copy54621 passed.
One client only; `freshTransferVerified=false`, `releaseReady=false`. No ordinary gameplay or
all-subscriber claim. [Public evidence](/mnt/c/taf-031-public-release.cfu8DL/README.md) and
[exact bindings](../../docs/STATUS.md#public-031--published-and-finalized).

Corrected private `0.3.1` is now installed and finalized for staging item `3796495680`.
Submit63022 TERMINAL0 reports `SubmittedUnverified`, `metadataMatches=true`,
`contentUnchanged=true`, attempt `0002`; finalize20925 TERMINAL0 reports
`SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true`.
[Submission](/mnt/c/taf-031-corrected-release.LpixRX/submit/upload.stdout) and
[finalization](/mnt/c/taf-031-corrected-release.LpixRX/finalize/upload.stdout) bind the corrected
package, not the earlier broken private build. Original `0001` records remain immutable.
One client only: `freshTransferVerified=false`, `releaseReady=false`; no gameplay or all-subscriber
claim. The separately bound public publication is recorded above.
See [current status](../../docs/STATUS.md) for exact evidence and integration verification.
Follow [the release gates](../../docs/RELEASING.md) before any submission.

For this Alpha only, the user waived manual startup/save/reload. Root separately accepts the
three named PACKAGE/COPY/BACKUP environment bind-alias test gaps, retaining their exclusions
rather than claiming zero full-gate skips. Root reused the exact completed frozen-runtime gate
with public-only delta checks and strict `--alpha` package binding, not another full `--alpha`
release-check. These decisions do not weaken production guards or the permanent release procedure.
[Decision and evidence limits](../../docs/STATUS.md#one-release-alpha-verification-decision).

## Boundaries

- Windows, running signed-in Steam, licensed Qud, installed SDK matching `sdk.lock.json`.
- Existing items only. Public Alpha is `3794797472`; private staging is `3796495680`.
  Remote operations freshly verify their exact lane/owner. No creation, login or subscription calls.
  Upload modes do not download; verification/finalization request Steam installation verification
  and may download the subscribed item. Neither changes subscription or edits `.acf` files.
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

Without an action switch, the launcher only checks the leased package and remote authority. It does not
create an attempt, start an update or submit. After all release gates pass, `-Submit` explicitly
enables one attempt. A per-item active receipt records the exact version and is flushed before
submission, then retained through completion. The registry, not the caller, decides where that
receipt lives, and the per-item kernel mutex - not `CreateNew` alone - is what makes one attempt
exclusive. An unresolved receipt requires reconciliation, never an automatic retry. Explicit
`-Finalize` can retain fresh installed-delivery evidence for an exact successful submission;
only a complete finalized chain admits a separately approved `0.3.x` package whose canonical
file inventory is absent from every retained attempt. Private staging permits an equal or higher
patch; public Alpha requires a strictly higher patch. An equal-version private recandidate is a
new package and new attempt, never a retry of the old attempt. Changing only package/plan paths,
receipt row order or equivalent receipt spelling cannot make unchanged content new.
No record is cleared or overwritten. `-Submit`, `-Inspect`, `-Verify` and `-Finalize` are mutually
exclusive. Shared launcher inputs, including `ChangeNotePath`, remain required in every mode.

## Invocation from release.yml

`.github/workflows/release.yml` drives this launcher on the attended Steam host. What it does is
fixed, so a reader of an evidence directory can tell a pipeline attempt from a hand-run one.

Layout per run, under `C:\taf-release\run-<run id>-<attempt>\`:

| Path | Contents |
| --- | --- |
| `inputs\plan.json`, `inputs\change-note.md` | `-PlanPath` and `-ChangeNotePath` |
| `inputs\handoff.env` | `PLAN_SHA` and `RECEIPT_SHA` for `-PlanSHA` and `-ReceiptSHA` |
| `package\TAF-<version>-<mode>` | the native copy the plan's `contentPath` names |
| `evidence-probe`, `evidence-probe2`, `evidence-check`, `evidence-submit`, `evidence-verify-<n>` | one fresh empty `-EvidenceRoot` each |
| `logs\`, `artifact\` | raw child console output, and a redacted copy of it |

The package is built on WSL ext4 and then copied to that native path, because the packager refuses
every `/mnt/<drive>` destination on this host: DrvFs reports mode 777 without metadata, so the
packager's shared-writable-ancestor, `700` scratch and `644`/`755` file-mode assertions cannot
hold there. The copy is verified — receipt digest, sorted inventory equality and a full
`sha256sum -c` over the copied tree — before the plan is generated against it. That is what the
0.3.1 release did by hand.

The launcher is invoked as
`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File <\\wsl.localhost\...\Tools\workshop-steam-upload.ps1>`,
so `$PSScriptRoot` — and therefore `WorkshopSteam\sdk.lock.json` and the helper projects — resolves
inside the run's fresh WSL clone at the tagged commit. Plan paths stay canonical
`/mnt/<lowercase drive>/...`, as `Convert-PlanPath` requires.

Sequence and the pipeline's acceptance rules:

1. A read-only `workshop-steam-probe.ps1` run **before** the multi-hour licensed gate, so an absent
   or wrong Steam session fails in seconds and never reaches an attempt.
2. Launcher with **no** action switch. The pipeline requires exit 0 and `checked_not_submitted`.
3. A second `steam.exe` process check and a second read-only probe, immediately before submission.
4. Launcher with `-Submit`. The pipeline requires exit 0, `SubmittedUnverified` and
   `metadataMatches=true`. Every other exit code is mapped to its meaning from the table below and
   fails the job.
5. A separate job polls `-Verify` afterwards. It writes no records, so re-running it is safe.
6. A final job runs `-Finalize`, and **only** if step 5 reported exit 0 with
   `SubscribedInstallationVerified`. It reuses the same run directory: the plan path, `PLAN_SHA`,
   item, change note and `RECEIPT_SHA` are read back from that run's recorded handoff file and
   passed verbatim — never rebuilt, re-planned or recomputed — and the retained plan is re-hashed
   only to prove its bytes did not move. Evidence goes to a fresh `evidence-finalize-<attempt>`
   directory. The pipeline requires exit 0, `SubscribedInstallationVerified`,
   `attemptFinalized=true` and a non-null `finalizationSHA`; every other exit code is mapped to its
   meaning from the table below and fails the job.

The pipeline never retries. Finalization is automatic because the tag push is the approval, but it
remains one-shot: the job never retries, a cancelled run is never safe, and running `-Finalize` by
hand from the retained run directory is the fallback when that job cannot run or fails closed.
`-Inspect` is not used by the pipeline: its exit 7 is informational.

Two consequences worth stating plainly. **CI attempts consume the same 64-attempt lifetime ceiling
per item** as hand-run ones. And **a Qud update that changes either SDK DLL fails every pipeline
run closed** until `sdk.lock.json` is deliberately re-pinned and the release check's preflight is
re-verified.

## Fixed registry root

The publisher admits exactly one release-state root, compiled into
`WorkshopReleaseRegistry.StateRoot`: `C:\taf-workshop-state.dRBivM`. There is no caller-selected
state root. `ATTEMPT_ROOT` (`args[5]`) survives only in the publisher's seven-argument shape: the
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
makes no SDK call and creates no attempt. It reports `attempt_retained`/exit7 whenever any attempt
exists, including finalized history; inspection is not admission to another release. `check` and
`submit` require every retained attempt to be finalized before applying that lane's version rule
and the all-history unseen-inventory check. Admission derives the existing
`taf-installed-inventory-v1` digest from the still-held closed package, not a caller-supplied digest.

The canonical `<item>.active.attempt.json` filename and the receipt's historical
`packagePath == contentPath` pairing are unchanged; only the directory holding them moved into the
registry, at `<registry root>\registry\<item>\attempts\0001\`. Later attempts use contiguous
`0002` through `0064`; there is no eviction or rollover after the bound.

## verify and finalize

Both modes build `WorkshopDelivery.csproj` and invoke `TafWorkshopDelivery.dll` with exactly
`verify|finalize PLAN PLAN_SHA ITEM RECEIPT_SHA`. They do not use the publisher's seven arguments.
The launcher retains its fixed-root guard, SDK hashes, separate fresh evidence, child-only Steam
environment, redacted logs and owned-child cleanup in all modes.

`-Verify` observes one exact subscribed installation. It does not write installation or finalization
records. Its null `finalizationSHA` and false `attemptFinalized` mean this invocation did not finalize
or attest history, not that previously finalized history is absent.

`-Finalize` selects the latest exact retained submission, verifies the original package/plan/receipt
and content path, then performs fresh Steam-installed verification. Only after SDK/installed-file
cleanup succeeds does it create immutable `.installation.json` and `.finalization.json` siblings.
All original package files, plan, receipt and paths must remain available unchanged. Finalization
binds the submission, registry marker, physical attempt directory, installed inventory and previous
finalization hash. Already-finalized history is verified without replacing its records.

No-history finalization is installation verification only. Successful finalization requires exit0,
`status=SubscribedInstallationVerified`, `attemptFinalized=true` and a matching non-null
`finalizationSHA`. A later cleanup refusal can retain that hash as a recorded fact; nonzero exit or
`Refused` is never a successful invocation. Both modes report
`operation`, one-client scope, `freshTransferVerified=false` and `releaseReady=false`; gameplay,
all subscribers, public promotion and a new network transfer are not proved. Verification refusal
returns2 or3. Neither operation can turn an unknown submission into a successful one.

A timeout, unknown callback, I/O failure, legal prompt, rejected submission, partial record, changed
identity, extra file, version regression or noncanonical history remains a fence. A crash between
installation and finalization retains a partial fence for operator investigation. No repair, retry,
deletion, alternate root, legacy import or automatic history cleanup is implemented.

## Publisher statuses and exit codes

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

## Remaining limits

- **No ordinary-play or all-subscriber acceptance.** Corrected private attempt `0002` is finalized
  with one exact installed inventory. The current Alpha manual-test waiver and bounded gate
  decision above do not turn that delivery result into gameplay, fresh-transfer or Beta evidence.
- **No uncertain-attempt reconciliation.** Partial or unsuccessful histories cannot admit a later
  attempt; an operator must investigate without deleting the fence or switching registry roots.
- **No legacy import.** Receipts written under an older caller-selected root are not migrated, read
  or counted. Reconcile them by hand before trusting the registry's attempt count.
- No durable abandoned-lock record, no all-subscriber or gameplay acceptance claim, and no
  authority granted by parsing any observation.

## Outcomes and tests

`SubmittedUnverified` is not delivery. Verify remote metadata, listing/preview, exact Steam-installed
receipt, and fresh native load/save/reload behavior. On a timeout or I/O failure, treat the attempt
as uncertain: it might already have succeeded. On a legal-agreement prompt, stop for the user.
Never erase an attempt receipt to make a retry pass.

Run `Tools/test-workshop-upload.ps1 -EvidenceRoot <fresh-native-directory>` for SDK-free Windows
tests. It first runs47 launcher routing/refusal fixtures against the exact launcher source hash,
then the registered C# suites. The separate read-only `workshop-steam-probe.ps1` can inspect an existing item's ownership
and manifest tags without changing anything. Keep build/test evidence local; SDK binaries are
copied only to local build output and are not distributed with the mod.

The test launcher runs `protocol`, `package`, `attempt`, `evidence`, `record`, `observation`,
`aftermath`, `cleanup`, `lock`, `lease`, `registry`, `cli`, `finalization` and `finalization-windows` suites,
which are registered in `WorkshopUploadTests.csproj`, dispatched by `UploadTests.cs`, and runnable
directly as `dotnet TafWorkshopUploadTests.dll <suite>`. These cover canonical hostile-input parsing, legal-before-I/O
classification, concurrent callback/settlement latching, real Windows write-once file leases,
persistence-before-verification and independent cleanup. No suite initializes Steam.
On 2026-09-07, Windows run41807 exited0:47 launcher fixtures and all14 registered suites passed,
including75 pure/source finalization cases,29 synthetic Windows history cases and17 package cases.
The directory regression observed native write-open results0/32/0 before/held/after disposal;
rename was blocked with sharing error32, so same-name directory replacement was not exercised
on that run. The test project compiled successfully. These results bind the exact held tooling
sources now copied into the active worktree; they are not an active-worktree rerun or an actual
Steam finalization. [Retained Windows evidence](/mnt/c/taf-package-directory-green.iJDY7u/).
Exact evidence and remaining gates are linked from [current status](../../docs/STATUS.md).

The active-worktree rerun94540 subsequently exited0: all47 launcher fixtures and14 upload suites,
both production helper builds, the installed-test build and14 installed-package cases passed.
[Integrated native evidence](/mnt/c/taf-publisher-integrated-native.ubIZAB/).
Actual `-Finalize` run43652 then exited0 with `status=SubscribedInstallationVerified`, `reason=null`
and `attemptFinalized=true`; its finalization SHA is
`d06be0a4fbf6e1a29a98f03e18840bf13be4539c813b7e41a3afdb8cd8b783ab`.
[Finalizer output](/mnt/c/taf-private-finalize.aix0Ji/upload.stdout).
This invocation verified one subscribed client installation; `freshTransferVerified=false` and
`releaseReady=false`. Original attempt/submission readback and all record hashes are in
[STATUS.md](../../docs/STATUS.md#publisher-integration--original-private-attempt-finalized).

After the protocol returns, the publisher writes the frozen outcome to
`<item>.active.attempt.json.submission.json`, bound to the exact held attempt bytes. It flushes,
reads back and leases that file before content or remote metadata checks. Callback absence remains
Unknown/TimedOut, not proof that Steam received nothing. Write failure preserves any partial file
and the active attempt; later failures never erase either. Final output is emitted only after
port and package cleanup; each cleanup action is attempted even if an earlier action throws.
These local tests do not prove an actual Steam callback or subscriber delivery.

The retained first-attempt checkpoint passed strict four-project compilation and all twelve Windows suites,
including14 CLI,11 item-lock,11 registry and12+9 lease groups. Delivery15package+14installed groups
and436 Python Tools tests pass. [Exact evidence](/tmp/taf-publisher-integration.w9HGSj/README.md).
No real registry or SDK access occurred in those tests; native finalization and ordinary
acceptance were incomplete at that checkpoint. The later finalization above does not establish
ordinary acceptance. A parsed observation never clears an attempt or grants retry rights.

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
