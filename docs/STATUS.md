# Current implementation and release evidence

**Snapshot:** 2026-09-08
**Target:** Beta preparation; current public lane remains v0.3 Alpha
**Current public version:** 0.3.1 public Alpha playtest, published and installed verification complete
**Published manifest:** 0.3.1; one subscribed client verified, broader Beta work remains open

The public Alpha is [Steam Workshop item
3794797472](https://steamcommunity.com/sharedfiles/filedetails/?id=3794797472). Rows marked retained
or frozen identify exact earlier checkpoints; their old counts do not sign later bytes. A green
source, compile, or generator gate proves only that layer. Native Caves of Qud behavior is signed
only for the exact exercised native cases; visual quality,
accessibility, compatibility, and Steam subscription remain separate evidence and are never
inferred from source or static automation.

## Current repository integration state — PR #6 merged, main protection updated

Documentation/hardening PR #6, "Post-0.3.1 Alpha hardening and release closeout", was
squash-merged to `main` as `be3f13a` at 2026-09-07T22:34:54Z by the author. Its branch
`codex/release-0.3.1-closeout` was deleted on origin under the repository's
delete-branch-on-merge policy; merges remain squash-only and no rulesets exist. Hosted run
34167157170 on `be3f13a` passed repository-audit and the full pure and portable test lanes on
`ubuntu-latest` and `windows-latest`.

Main's branch protection changed the same day. The pull-request review requirement was removed
entirely (the sole collaborator can never approve their own PR); PR-based integration is now
policy, enforced by the required status checks, linear history and `enforce_admins`, which is now
ON. Required checks (repository-audit, plus the full pure and portable test lanes on
`ubuntu-latest` and `windows-latest`, strict), linear history, no force-push, no deletion and
required conversation resolution all remain. The `dev` integration branch now exists on origin, is the repository default branch, created at `6f73974` and currently at `736d28c`. It is protected with the same three required checks, linear history, required conversation resolution and no force-push/deletion, but with `enforce_admins` off and without the strict up-to-date requirement, so the branch model in
[RELEASING.md](RELEASING.md#branch-model) is in force rather than proposed: feature work targets `dev` and `main` receives release merges. Earlier sections below record the pre-merge state at their own checkpoints and are not restated here.

Annotated `v0.3.1` still targets `a46b5ad`; `main` is now one squash commit ahead of that tag.
Public0.3.1 and its published bytes are unchanged. Windows and native lanes are being re-run for
the current bytes; those receipts are pending and are not claimed here.

## Automated release lane

Workflow **authored, not yet exercised.** `.github/workflows/release.yml` adds a tag-triggered
Steam Workshop release lane under the author ruling of 2026-09-08 recorded in
[RELEASING.md](RELEASING.md#author-ruling-2026-09-08--automated-release-lane-and-the-doctrine-it-amends).
No pipeline release has run; every claim below is a configuration fact, not a release result.

Verified against the GitHub API on 2026-09-08:

| Setting | State |
| --- | --- |
| Environment `steam-workshop` | required reviewer + branch/tag policy; deployment tag pattern `v*` |
| Environment `steam-workshop-staging` | required reviewer + branch/tag policy; deployment tag pattern `staging-v*` |
| Tag ruleset "release tags" | active; restricts creation, update and deletion of `refs/tags/v*` and `refs/tags/staging-v*`; bypass limited to the repository admin role |
| Fork pull-request workflows | approval required for all external contributors |
| Workflow permissions | read-only; pull-request approval by Actions disabled |
| Repository secrets for the lane | none, by design; no Steam credential exists in GitHub |
| Self-hosted runners | **0 registered.** The `taf-steam` runner is not installed yet, so the pipeline cannot run |

Two environment settings are still open: `can_admins_bypass` is `true` on both environments and
should be turned off so the approval cannot be skipped, and the "prevent self-review" option must
stay off, because the sole collaborator both pushes the tag and approves the deployment.

Open before the first pipeline release: register and start the `taf-steam` runner per the runbook
in [RELEASING.md](RELEASING.md#steam-host-runner-runbook); rule on the merge method for release
pull requests, since the currently enabled squash-only merge rewrites the receipt-binding commit
that the packager requires as an ancestor of the tagged `main` commit; and run the first
`staging-v0.3.2` release, recording its run id, attempt number and finalization SHA here.

## Unreleased stockpile unit capacity

A dedicated stockpile now declares how many material units it holds — 32 for a chest the founder
dedicated by hand, off the new `r_KingdomStockpileCapacity` blueprint tag for anything that
declares one. Counting is unchanged and stays whole: `Stock()` and `StockForExactContainer()`
never read a capacity, so the settlement ledger and every purpose-local debit view agree by
construction and an over-cap stockpile standing in an old save reads exactly what it read before.
What the capacity changes is intake only: a delivery fills the first store with room, walks on to
the next, and spills the remainder to the ground exactly as it already did when no stockpile
existed. A full store says so once and stops saying it when it has room again. Nothing is
remembered across a callback: creating the bundle, stamping its count (which is
`Stacker.StackCount`, and sends `StackCountChangedEvent`) and inserting it each run other
people's handlers, so the destination and its room are proved after the creation and again after
the stamp, and the bundle is proved standing in that exact store with the count it was stamped
with before a single unit is counted. No saved field, wire format, option or public API changes;
the new state is one object int property.

Census after the stockpile-capacity change:3055 staged C# files;432,723 physical lines;3086 files
in the generated cold-install inventory. Staged compilation covers3055 sources, baseline and
compatibility symbols, run here by Roslyn 9.0.306 on Linux against the licensed Managed references
with warnings as errors (baseline compiles3051 of them; the optional-mod bridge is
compatibility-only, and the tracked Hearthpyre 2.2.3 ABI stub compiles clean first). The
dev-harness modes and the Windows gate did not run for this census.
Direct `XRL` imports: 1419 files, 0 over the line limit.
Inventory SHA-256: `fb245d82f14dc9963f1a2291d3eec1dc8e4a47ae98134cb3d9dbbc3f1524764a`.
No native game run was made for this change; every player-facing claim above is unproven natively.

## Retained unreleased empty-camp legacy and native water regression

An actual empty-camp heartbeat exposed rejected automatic legacy staging: no living body
evidence exists yet. Explicit committed-unresolved profile schema2 now retains real technology
and provenance without inventing species,gear or NPC authority. The same refusal applied to
any realm whose residents map to no canonical body,not only a population-0 camp: a settled
population of only non-canonical species reached the identical refusal. New exile also
proves the original foundation receipt independently of the later current-profile revision,
which unblocks exile for any realm at profile revision2 or above. Schema0/1
bytes remain unchanged; older0.3.1 readers reject schema2,so any next public package needs
a new version. Public0.3.1/main/tag are unchanged.

Current census after merging `dev`:3052 staged C# files;432,259 physical lines;3083 files in
the generated cold-install inventory. Direct `XRL` imports: 1417 files, 0 over the line limit.
Inventory SHA-256: `c226862245f18d7b9fffadf7abc39b1d571462d1f26de6f665045f8ceaea412c`.
Complete canonical parent comparison of this branch's own delta proves3045 unchanged/four
modified/no additions or removals against integration parent2be6b00; the three added and
seven modified C# sources plus one option row merged from `dev` carry their own review chain. Root and
independent reviewer read the complete four-file delta and affected boundaries; the exact
structural release gate passes and the exact-inventory human semantic review is open against
this merged digest. This is source review,not functional acceptance.

Focused38898 passed149 cases,zero skips. That receipt predates the seventh
KingdomWaterMaintenanceNativeSourceTests case and is retained as measured. The branch adds
91 cases in total:73 seal/schema/exile regressions,7 native-source wiring cases,9
historical seal-fixture cases and2 exile cases. Four seals written by writer code
byte-identical to tag `v0.3.1` are checked in at `DevTests/Fixtures/SealProfile` with
pinned SHA-256s; they prove the forward read is an identity — schema0/1 parse,recompose
byte-for-byte,survive a transition copy and validate as a saved reservation shape. Earlier
full managed44659 was intentionally superseded after two imported-cohort fixture failures;
it has no full-suite verdict. Earlier four-mode10882 passes only its earlier source bytes.
Final licensed Windows suites on the merged tree passed13,826 main and5,116 Portable cases,
zero skips,up from13,735/5,109 on the `dev` integration branch;
both normal Rebuilds had zero warnings/errors. Canonical39198 TERMINAL0 passed all four
C#7.3 modes:ordinary3045/3049,developer3182/3186,137 Harness files,plus installed Hearthpyre
2.2.3 source/ABI on the pre-merge bytes; the seal-lane production sources are unchanged since
that run,and the sources merged from `dev` carry `dev`'s own compile receipts. Exact comparison
proved every ordinary/developer source matched the bytes it ran on. Repository25818 passed501 tests/89.477s plus cold inventory,docs,architecture and XML.
The first10865 managed run retained13794 passes/one Harness line-limit failure; removing
one blank line closed it before the final native and full-suite reruns. No guard was weakened.

Native16504,seed1012037,profileiyqatG,proved an actual automatic schema2 empty-camp stage,
canonical record roundtrip,partial physical upkeep and the original drought departure body,
roster,tally and journal retirement. It failed the summary-note assertion:12 ordinary notes
already occupied the bounded list,so exact departure notes=0. The12-entry cap is intentional;
the corrected fixture proves exact durable Chronicle delivery separately. This diagnostic
does not sign the later whole-run pass. Original failed attempts and receipt-owned stops are retained
in [diagnostic evidence](/mnt/c/taf-water-departure-diagnostic.pdomed/README.md).
Final native45930 TERMINAL0 passed at21:15:32.107UTC,seed1012037,profile4r3WC1:
actual automatic empty-camp schema2 stage/roundtrip;4800 observed EndTurns;three dry bills,
one exact original departure,two loyal residents;actual16-dram donor transfer then paid
recovery. Exact canonical Chronicle receipt proves Delivered official/outsider and Skipped
journal. All3186 current C# files match that profile; strict raw log/96journalrows and exact
receipt-ownedPID21008 stop pass. Synthetic dedication/enrollment remain declared. This does
not test carried inventory,current Chronicle-list membership or ordinary rendered play.
[Final evidence and retained failures](/mnt/c/taf-water-final-lines.lRva1h/README.md).
Open visibility gap: ordinary summary saturation can hide the departure there; a Chronicle
receipt does not prove founder notification. No ordinary-play or save/load acceptance.

## Unreleased first-settler legibility

Publishing the first-guest correspondence now writes one player message naming the kingdom and
pointing at the Charter. A standing candidate makes the next arrival pass return before it
reaches that publication, so the message is said once per opportunity and never repeated.

The durable half is presentation, not a ledger note: an unanswered first guest is now said by the
Charter/Status next-need line, alongside the settlement's ordinary want rather than instead of it,
so a deferred guest cannot silence a settlement running out of water. One rules-layer predicate,
`KingdomLifecycleRules.GrowthFirstGuestAwaitsAnswer`, backs the Charter label, the next-need line
and the correspondence guard, and it binds both the candidate phase and the choice state, so no
surface can name a debt the rules would refuse to settle. The stale housing advice is corrected:
with no roof at all the line names the settler's tent and its bill, and promises only what a roof
actually buys, because the first guest's citizenship gate never reads lodging. No saved fields,
formats, options or arrival intervals change; public0.3.1 is unchanged.

Current census:3052 staged C# files;432,239 physical lines;3083 files in the generated
cold-install inventory. Staged compilation covers3052 sources, baseline and compatibility symbols,
run here by Roslyn 9.0.306 on Linux against the licensed Managed references with warnings as errors
(baseline compiles 3048 of them; the optional-mod bridge is compatibility-only, and the
tracked Hearthpyre 2.2.3 ABI stub compiles clean first). The dev-harness modes and the Windows gate did
not run for this census.
Inventory SHA-256: `cf01fcc9993de9cee88d8ec6dc17dd8111eb37375f546d08850ac957fb372cad`.
Direct `XRL` imports: 1417 files, 0 over the line limit.
Linux dotnet 9.0.306 against the licensed install passes the full source suite at 13,735 cases and
the portable kernel at 5,109 cases, zero skips in both, and passes the doc, structure and
tooling audits. No native in-game run and no human semantic review bind this digest.

## Unreleased master-growth resume correction, case28d.5

Automated native diagnostic reproduced a real master-resume defect: fresh growth retained
interval0 but received a positive arrival deadline, invalidating its enclosing lifecycle.
Established cadence also required its deadline mirror to remain consistent with recorded
debt/opportunities/open leases. A new detached engine-free resume protocol now validates
the whole proposed state and exact original graph before all-participant publication.
Health evidence, field work and subsidence checkpoints remain owned by their existing lanes.
No saved fields or formats change; public0.3.1 is unchanged.

Native11624 passed at19:15:56.180UTC, seed1012036, sealed profileLN1Xkb: two actual paused
EndTurns/no raid wakes, one resume token at377129, then one Ready at377130; explicit quest
completion once and unchanged repeat. Strict raw log/15journalrows and receipt-ownedPID35424
stop pass. Comparison83480 matches all3181 production/Harness C# bytes. This is actual
engine-turn coverage in a synthetic fixture, not ordinary play or save/load acceptance.

Retained camp-guide census:3052 staged C# files;432,178 physical lines;3083 files in the generated
cold-install inventory. Staged compilation covered3052 sources, baseline and compatibility symbols,
run here by Roslyn 9.0.306 on Linux against the licensed Managed references with warnings as errors
(baseline compiled 3048 of them; the optional-mod bridge is compatibility-only). The dev-harness
modes and the Windows gate did not run for that census.
Inventory SHA-256: `dcab3931d57df58aeaf3f0dee894acdec54d261a4e5f85d94cb369d8a1c73e96`.
Direct `XRL` imports: 1417 files, 0 over the line limit.

Retained master-growth census:3049 staged C# files;431,893 physical lines;3080 files in the
generated cold-install inventory. Canonical compilation covers3049 sources, baseline and
compatibility symbols. Inventory SHA-256:
`a3a9c8dd8ea36962475266e7005ccc6fcdd352b3bfd3d9c4675beb47b51be2b9`. Every native, Windows-suite,
four-mode compile and review result in this section binds that digest, not the current one.

Focused86313 passes38 engine-free cases, including modern/historical open-arrival clock
cuts through retirement, candidate continuation, canonical round-trips, exact child ownership,
recorded debt, pause overlap and arithmetic refusal. Candidate fixtures use supported
semantic version1; no production guard was relaxed to pass them. Four source-wiring cases
support the75th persona. Structural release gate passes exact3052-source digest
`dcab3931d57df58aeaf3f0dee894acdec54d261a4e5f85d94cb369d8a1c73e96`.
Independent source/native/test review found no Required issue. Full licensed Windows1814
passed13,715 main and5,093 Portable cases,zero skips; normal rebuilds had zero warnings/errors.
Canonical53744 passed all four C#7.3 modes: ordinary3045/3049,developer3177/3181,132 Harness
files,plus installed Hearthpyre source/ABI. Repository71120 passed501 tooling tests/48.083s
and cold-install inventory,docs,structure,architecture,XML and registration checks.
Hosted integration checks remain separate; no new public-release or broad Beta claim.
[Evidence and retained failures](/mnt/c/taf-master-growth-native.sP00c6/README.md).

## Retained real-turn recovery, case28d.4

Development-only regression now exercises the existing sealed `advance 1` through the
actual engine loop. Actual contact/acceptance and three original deaths retain Active;
pass-through EndTurn/OnWorldWake observers prove the first heartbeat earns Ready once.
Actual post-turn explicit completion finishes the quest, clears the wound, and refuses
repeat without changing the observed clock or settled authority. Legitimate world effects
are retained, not restored. No clock, energy, recovery state or master latch is forced.

Native59966 TERMINAL0 passed at18:22:00.039UTC, seed#1012035, fresh sealed
`/mnt/c/taf-scenario.Zt98Jg`: one dispatch/one wake, turns2->3, ticks103527->103528,
actions37->51,216water/24plunder/three mints and original graves retained. Strict raw log
and12journalrows pass; exact receipt-ownedPID44480 stopped. Comparison61572 proves all3177
current production/Harness C# bytes match that profile. The observer permits bounded
multi-dispatch overshoot and additional wakes, but this seed does not execute those variants.

Full licensed Windows1901 TERMINAL0 passes13,673 main and5,051 Portable cases, zero skips;
normal rebuilds have zero warnings/errors. Canonical7819 TERMINAL0 passes all four C#7.3
modes: ordinary3043/3047, developer3173/3177,130 Harness files, plus installed Hearthpyre
source/ABI. Production remains3047sources with the unchanged current6dbd structural binding.
Four new source-wiring tests and74personas are registered;56persona parser tests pass.
Final repository and hosted integration gates remain separate. This is controlled real-turn
evidence, not ordinary rendered play, paused resume, save/load or full Beta acceptance.
[Retained evidence](/mnt/c/taf-raid-recovery-turn-native.X0VMU8/README.md).

## Retained native bound/paused recovery guards, case28d.3

Recovery checkpoint25959e7 normally rebased to6d70a03 on integration1ce9960; only the
already-reviewed PR10 test/doc delta was added, production digest6dbd remains unchanged.
New developer-only guard provider/checks calibrate actual custody/GetObjects entry observers,
exercise same/foreign bound refusals with finally-disposal, then request the actual master
pause and attempt explicit completion. Four source-only wiring cases and a73rd persona are
registered;56 persona tests pass. Combined compilation, full suites and scoped native cases
pass as recorded below. Final repository/hosted checks are separate integration gates.
No new public release is claimed.

Subsequent actual guard case passed at17:52:27.241UTC, seed#1012034, fresh sealed
`/mnt/c/taf-scenario.3lgp4w`. Each same/foreign positive control observed one custody capture
and one native GetObjects entry; all four bound refusal windows observed zero. Actual
bindings disposed with no residue and unchanged Ready/quest/wound/water. Real option pause
and disabled wake observed zero captures; explicit paused completion observed one fresh
capture, resolved once, removed the wound and left repeat unchanged. Strict raw log passed;
receipt-ownedPID41892 stopped. Combined veto case also passed at17:54:57.244UTC in fresh
`/mnt/c/taf-scenario.vnBL4b`: exact live veto/pending authority, actual retry removal, one
RaidersDefeated result,240water/zero plunder/three mints and unchanged repeat/clock. Strict
log and receipt-ownedPID43796 stop pass; native runner2155 TERMINAL0 covers both cases.
Compile56568 TERMINAL0 passes ordinary3043/3047 and developer3171/3175 with128 Harness
shards, all four C#7.3 modes plus installed source/ABI checks. Full licensed Windows93430
TERMINAL0 passes13,669 main and5,047 Portable cases, zero skips. Exact source comparisons
33144/61340 prove all3175 current production/Harness bytes match both sealed native profiles.
Final repository audit65692 passed501tests/46.709s; all6hosted checks passed and PR11
normally merged to integration2f6051c. Its full tree matches tested25d1f49. Main PR6's
six checks also passed; it then still required an eligible approval. No bypass was used.
Evidence `/mnt/c/taf-raid-recovery-guards-native.6Dnfup`. This signs controlled native API
seams, not an ordinary heartbeat, rendered popup, resume, save/load or full Beta acceptance.

## Unreleased recovery absence correction, cases28d.1–28d.2

Current candidate makes RaiderDying inert, moves automatic readiness to an unbound
normal seat wake, and rechecks fresh complete physical absence before explicit turn-in.
Response drafts precede that observation; exact game/system/book/recovery and quest
reproofs precede publication. Any bound survey defers/refuses without a second classified
scan. Explicit committed recovery remains available while automation is paused.
No saved fields/public API changes; shared active-attack counting remains unchanged.

Focused raid suites22822 passed124 main/95Portable cases, zero skips. Independent complete six-file delta review found no
Required issue. Canonical comparison38299 passed:3047 current sources,3041 unchanged,
five modified, one added, none removed;431,611 physical lines,1415 direct-XRL,zero cap debt.
Digest `6dbd94092f57eeb9b79f5ff169105fa5a7ab2cc6702480d99ba98c92be17bff9`.
Exact semantic review now binds this digest through the complete canonical comparison.
Strict structural gate51073 passed for3047 staged C# files; its following documentation
check found stale census text, now being corrected rather than waived.
Direct `XRL` imports: 1415 files, 0 over the line limit.
Inventory SHA-256: `6dbd94092f57eeb9b79f5ff169105fa5a7ab2cc6702480d99ba98c92be17bff9`.
Canonical compile51408 TERMINAL0 passed 3047 sources, baseline and compatibility symbols,
ordinary3043/3047 and developer3167/3171 with124 Harness shards, plus installed ABI.
The generated cold-stage inventory contains 3078 files, not new subscribed-install evidence.
Full licensed Windows27430 TERMINAL0 passed13,661 main and5,039 Portable cases, zero skips.
Repository audit71541 TERMINAL0 passed501 tests in46.558s, exact3078-file cold-install
inventory, current documentation, XML, architecture and Harness registration. Independent
closeout review found no Required issue. Combined PR10 integration and hosted CI remain
separate next gates; main PR6 then still required an eligible approving review.

Baseline native92856 TERMINAL1 reproduced premature recovery readiness at
2026-09-07T16:57:49.982Z, seed#1012031, fresh profile `/mnt/c/taf-scenario.3VC3J1`.
Real contact earned StoresPlundered/Offered (240→216water,24plunder); actual acceptance
earned Active. Two original deaths preserved Active. Final-original RaiderDying changed
Active→Ready before destruction; a real BeforeDestroy veto retained that exact live
original in the same cell and outside both graveyards. Assertion correctly failed:
`expected recovery Active; observed Ready`. This retained baseline precedes the current fix.

Evidence: `/mnt/c/taf-raid-recovery-red.uaan5G`. Strict raw log passed; the persona failed
honestly. Only receipt-ownedPID44992 stopped. New Harness has independent source review;
four source-wiring cases pass in each project, zero skips; all56 persona tests pass.
Those source tests do not sign native behavior. The corrected native receipt below now
covers retry, readiness and turn-in; ordinary turns and save/load remain open. Sealed-runner popup suppression
also excludes rendered quest-popup acceptance. Public0.3.1 remains unchanged.

Independent baseline29267 reproduced case28d.2 at17:13:51.390UTC, seed#1012032,
profile `/mnt/c/taf-scenario.i78bCs`. Actual foreign absence earned Ready; the exact
survivor returned alive. Immediate turn-in wrongly publishedResolved, finished its
quest and cleared the wound while that original remained alive on the target tile.
Exact assertion failed, raw log passed, only ownedPID34812 stopped. Evidence:
`/mnt/c/taf-raid-recovery-return-red.z4lP7I`. No lifecycle state or body removal was forced.
Corrected native51235 TERMINAL0 passed both cases at17:23:51.409UTC and17:26:18.640UTC,
seed#1012033, fresh profiles `/mnt/c/taf-scenario.eTNGEi` and `/mnt/c/taf-scenario.UamLRQ`.
The real final-destruction veto preserves Active and exact wire/quest state through another
activation; disarmed actual removal then earns Ready and explicit turn-in resolves once.
The returned exact live original refuses turn-in with Ready/quest/wound/wire unchanged;
actual final removal then permits one completion. Both retain216drams,24plunder,three
original mints and unchanged repeats. Both strict raw logs passed; receipt-ownedPIDs40344
and8108 stopped, with profiles/seals retained. Evidence:
`/mnt/c/taf-raid-recovery-fixed.FaVvXv`. This is synthetic component evidence only.
These two cases do not cover paused completion or bound refusal; case28d.3 above has its
own later native receipt and is not inferred from this earlier two-case result.

## Retained unreleased native destruction-veto regression, case28b.3

No further production changes. Dependency tree equals tested death fix87c4992, normally
merged by PR9 into integration a4d61a9. Public0.3.1 remains unchanged; main PR6 then still
required an eligible approving review. New developer-only veto provider/checks and persona
have independent source review and four source-wiring tests passing in each managed project;
all56 persona tests pass. Full licensed Windows84737 TERMINAL0 then passed13,654 main
and5,032 Portable cases with zero skips, after normal Windows rebuild of both projects.
Canonical compile59060 TERMINAL0 passed ordinary3042/3046 and developer3164/3168 inputs,
all122 Harness shards, plus installed Hearthpyre source/ABI checks.

Native13639 TERMINAL0 passed at16:26:23UTC, seed#1012030, fresh profile
`/mnt/c/taf-scenario.EWGCdc`. Two originals died normally. The final original's real
BeforeDestroyObjectEvent was vetoed after observed RaiderDying entry/exit; exact body,
cell, marker, IDs and pending authority remained, with no originating/global graveyard
membership. Actual activation preserved the pending attack. After disarming the retained
veto part, actual Die retry removed that original into its originating graveyard; actual
activation produced one RaidersDefeated proof and repeat left authority unchanged.
All four Die calls retained240drams, zero plunder, three total mints and unchanged clock.
Strict raw log and nine expected journal rows passed; only ownedPID39884 was stopped.
Evidence: `/mnt/c/taf-raid-veto-proof.iKyt6v`. Native profile hashes match frozen sources.

This is controlled same-process developer evidence, with synthetic graveyard retention
headroom256→272 and native drops/unequips retained. It does not prove ordinary turn-driven
play, earlier BeforeDie veto paths, recovery, save/load or general scene immutability.
Final repository audit11966 TERMINAL0 passed501 tests in47.967s, canonical3077-file
cold-install verification, documentation and unchanged structural binding. Normal PR
integration remains open. Current production bytes and exact structural binding are
unchanged; neither native case signs full Beta acceptance.

## Retained unreleased raid-death correction, case28b.1

Actual native death run12835 exposed final-raider quarantine: `RaiderDying` advanced
the attack before engine removal, then terminal inspection counted that same still-present
body. Foreign-zone and first target-zone deaths preserved exact authority and240drams;
all three real deaths and originating-graveyard removals were proved. Retained failure:
`/mnt/c/taf-raid-death-retention.m1iKE0`.

One production file now leaves the matching active attack pending during the cancellable
pre-removal callback. Existing normal settlement wake/zone activation observes actual
absence and resolves afterward. Shared counting, recovery and saved fields are unchanged.
The new native case explicitly requires unchanged pending state after final removal,
then one `RaidersDefeated` result and unchanged repeat through real activation.

Retained strict structural census:3046 sources/431,474 physical lines/1414 direct-XRL/zero cap failures;
Inventory SHA-256: `980afeb740331d69030591f1a6a27c575e9b61bd4448c05fe841f7b0cbeab6cb`.
Exact semantic release binding passed through the complete canonical comparison:3045 unchanged
production files inherit review, one modified file received scoped independent review.
Canonical compile1886 TERMINAL0 passed ordinary3042/3046 and developer3162/3166 inputs,
all120 Harness shards, plus installed Hearthpyre source/ABI checks. Older receipts below
are not reassigned to this delta. Focused native source fixtures6/6 in both projects and runtime-source
fixtures14/14 pass. Corrected native90794 TERMINAL0 passed at15:47:52UTC, seed#1012029,
profile `/mnt/c/taf-scenario.T6OIGl`: all three real deaths/six callbacks, exact unchanged
pending authority through final removal, one post-removal activation result,240drams,
zero plunder, one attack proof and unchanged repeat. Strict raw Player.log passed;
ownedPID34372 stopped, profile/seal retained. Full Windows24858 TERMINAL0 passed
13,650 main and5,028 Portable cases with zero skips after normal generated-output rebuild.
Evidence remains under `/mnt/c/taf-raid-death-fixed.nf0IMW`; final repository audit48595
TERMINAL0 passed all501 tooling tests in47.005s plus inventory, XML, architecture, registration
and documentation checks. Normal PR/CI integration remains pending. Main PR6 then still required
an eligible approving review.
The earlier missing-generated-DLL launch and both native
failures are retained. No new version, package or Workshop update is claimed.
This is synthetic developer evidence; ordinary play, recovery and save/load stay unsigned.

## Retained unreleased raid-contact correction — native case28b.2

Two production files change: `Growth/KingdomWaterDebit.cs` and
`Raids/KingdomRaids.06.AttackResolutionAndOutbox.cs`. Actual contact now takes or reuses the
active survey and holds its scope through debit and compensation. Exact-store reservation
cannot fall back to another vessel; settlement-wide counters, routed-water floors, custody
and rollback guards remain intact. Failed reservations refuse before `Commit`, without the
previous bogus quarantine over untouched water. No saved fields or public API change.

Retained raid-contact census:3046 sources/431,481 physical lines/1414 direct-XRL/zero cap failures;
Inventory SHA-256: `3f0d1dde39c6a07cfd6cbad9f9888833b9ca0837bceec7918cb910260e1e9688`.
The exact bridge retains3044 unchanged production files, two modified, none added or removed.
The derived cold-stage inventory remains3077 files, not new installed/subscribed evidence.
[Production bridge](/tmp/taf-raid-contact-structure.boMu2H/raid-production-bridge.json).
Canonical compile52808 has passed all four modes: ordinary3042/3046, developer3160/3164,
with118 Harness shards. Focused Linux source contracts8/8 and adapter7/7 pass, zero skips.
Full licensed Windows rerun88369 TERMINAL0 passes13644 main and5022 Portable cases, zero skips.
Initial run35799 retains13641 passed/3 failed from stale source pins and a Harness line-count
fixture; those failures were corrected without changing the native-tested runtime bytes.

Native14492 TERMINAL0 proves foreign-zone contact leaves240 drams, zero plunder and exact wire
unchanged; restored contact takes24 drams, leaves216, records StoresPlundered and one attack
terminal proof, with no quarantine. The extra96-dram store and repeated-contact state remain
unchanged. Exact nonfirst selection, null/insufficient-selector and anonymous-survey refusals
also pass. Strict logs and exact-owned PID40412 shutdown pass. This is synthetic component
evidence only; ordinary play and save/load remain untested. Earlier native failures remain
retained. The receipt keeps its original Harness hash; later blank-line cleanup is not a
second native run. [Native evidence](/mnt/c/taf-raid-contact-fixed.Dcjzs3/README.md).
Public0.3.1 remains the separately published checkpoint below; this correction is unreleased.

## Public 0.3.1 — published and finalized

Public item3794797472 submission55498 TERMINAL0 at13:18 UTC on2026-09-07 reports
`SubmittedUnverified`, `metadataMatches=true`, `contentUnchanged=true`, version0.3.1, attempt `0001`.
Finalizer55266 TERMINAL0 then reports `SubscribedInstallationVerified`, `reason=null`,
`attemptFinalized=true`. [Public evidence](/mnt/c/taf-031-public-release.cfu8DL/README.md).

| Binding | SHA-256 |
| --- | --- |
| Public upload plan | `948657ea1e143117e56093bd3933f50deb44c6782dead877e49b27f94025efdc` |
| Public package receipt | `7bdea6d07b6d2285ad2dbc28e2358c8ea1e7d690c1661cbbdbdef2b41153bd88` |
| Installed canonical inventory | `56deb51aab223b5bbaf2c04d1581fe56b0508b305e586e1ab3e4561d13f30373` |
| Public attempt `0001` finalization | `60f57049d906e8a7a5c038bcc2343326143ac8feaeb45d92951756d8263cbb46` |

Annotated `v0.3.1` points to commit `a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e`,
tag object `ed91d97b6d5d1b515933d144adb01f89303c5496`. Strict Alpha package68272 TERMINAL0
proved3077 files and exact private-candidate binding; native copy54621 TERMINAL0 retained the
same receipt and inventory. One public subscribed client was verified;
`freshTransferVerified=false` and `releaseReady=false` remain explicit, not gameplay acceptance.

After the web tool failed to open the listing, a separate signed-out HTTPS fetch succeeded.
Retained `public-page.html` SHA-256
`eb0a134d1786066e1fe4f3ab99976dbcf4ca1bb2eb37fcb3d72aa57bc8e5dd38` exposes the Alpha title,
new hook and all six tags. This is HTML inspection, not a pixel capture. The requested current
Alpha publication is complete; broader Beta work and the evidence limits below are unchanged.

## Corrected private 0.3.1 — installed and finalized

Corrected private item3796495680 submission63022 TERMINAL0 reports `SubmittedUnverified`,
`metadataMatches=true`, `contentUnchanged=true`, attempt `0002`. Finalizer20925 TERMINAL0
then reports `SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true`.
[Submission](/mnt/c/taf-031-corrected-release.LpixRX/submit/upload.stdout),
[finalization](/mnt/c/taf-031-corrected-release.LpixRX/finalize/upload.stdout),
[retained chronology](/mnt/c/taf-031-corrected-release.LpixRX/README.md).
The private receipt is bound at commit `47a055254f09c1ac72733a3198101a66d47babf6`.

| Binding | SHA-256 |
| --- | --- |
| Upload plan | `0738dfdd55f06c91a3c39bc209b4f6db9cad9c674645ef501e3d0fd31fbab34d` |
| Private package receipt | `b12b6d31c7ea14ace123765199903114b5151f0d9405c2b6eef795fac8697359` |
| Installed canonical inventory | `7274d19086813b076bf499fe5f391facd7174587b909bf0539e8b91419eb9e5c` |
| Attempt `0002` finalization | `913a47b7f3edf292a320e847abb8faeec5e7c05ac23e4944e3c61706a1f43e17` |

This proves one client's corrected subscribed bytes, not all subscribers or gameplay acceptance.
`freshTransferVerified=false` and `releaseReady=false` remain explicit. Original attempt `0001`
and its evidence remain immutable. The separately bound public0.3.1 publication is recorded above.

### One-release Alpha verification decision

Canonical release-check29200 TERMINAL0 completed all stages on clean
`792270bd228973f22421b36c611af5599796246a`; log SHA-256
`9d7eb43e64e61431a4336149034b369cc0f7bb70635508e43a612b4c7d07a3f9`.
Managed13625 Taf/5012 Portable passed with zero skips. The full gate was **not zero-skip**:
the PACKAGE, COPY and BACKUP bind-alias fixtures stopped at foreign-owned `/tmp` in nested
user namespaces. Root accepts these three narrow environmental test gaps for this Alpha;
they are not PASS and were not waived by the user. Production ownership/alias guards stay intact.

The user explicitly waived manual startup/save/reload for this Alpha. Retained six genuine
boot/save/cold-load pairs sign startup checkpoint `1c1c2bc`; retained stock native16 and managed
checks retain their separate scopes. No ordinary subscriber play or graceful Quit PASS follows.
Root reused the exact frozen-runtime gate, checked the public-only delta, and required strict
`--alpha` package lineage/receipt/tag/structure binding. The two stale README freshness assertions
were updated; 501 Tools tests and final document checks passed. No second full
`--alpha` release-check is claimed. This is a one-release verification decision, not a permanent
gate relaxation or Beta/Release acceptance. Public packaging, upload and finalization are complete.

## Publisher integration — original private attempt finalized

Retained pre-correction-upload checkpoint, superseded by the current delivery record above.
The publisher extension was integrated over stock checkpoint `b4c2d2d`, without then changing the
subscribed private package. Active-worktree native run94540 TERMINAL0 passed47 launcher fixtures
and all14 upload suites, including75 pure/source finalization groups,29 Windows history groups
and17 package groups. Both production helpers and the installed-test project compiled; all14
installed-package cases passed. [Integrated evidence](/mnt/c/taf-publisher-integrated-native.ubIZAB/).

Actual finalizer43652 TERMINAL0 completed original private item3796495680 attempt `0001`:
`status=SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true`.
[Exact output](/mnt/c/taf-private-finalize.aix0Ji/upload.stdout).
This invocation verified one subscribed client installation. It retained immutable installation
and finalization siblings; root's fresh readback confirmed the original attempt/submission bytes
unchanged. Exact SHA-256 identities:

| Record | SHA-256 |
| --- | --- |
| Original attempt `0001`, unchanged | `90011b42fffc6d570b87873392a5eb99aa781430407d9706b3c3e88f920794d1` |
| Original submission, unchanged | `350bbb61eb1b601bf7882e42ffeed06c86800eb3a000dd18efea82994868e5bc` |
| Installation observation | `871e28ce85f12b3388b64d72ff82dba8b891e1d9260a91f1a78b31aeea812446` |
| Finalization | `d06be0a4fbf6e1a29a98f03e18840bf13be4539c813b7e41a3afdb8cd8b783ab` |
| Installed canonical inventory | `6bf2e248aa834cf52272f61e566f65a8463bddffac6d0e3599958ab729d45d58` |

`freshTransferVerified=false` and `releaseReady=false`. That old private `0.3.1` contained
the reported Quickstart failure; its finalization neither replaced it nor approved promotion.
Corrected attempt `0002` now supersedes those installed bytes, as recorded above.
Private same-version admission requires canonical inventory unseen across all prior
attempts; public versions must increase. Unknown/partial/failed histories remain fenced, and no
record is deleted or retried. [Publisher contract](../Tools/WorkshopSteam/PUBLISHING.md).

## Quickstart startup correction — six native combinations exercised

The user reproduced an empty Quickstart site with the apron/path refusal in private item
3796495680. Its subscribed 0.3.1 bytes were previously verified; this does not prove gameplay.
That failed private build was not promoted; corrected private and public0.3.1 delivery and the
current Alpha decision are recorded above.

The old readiness check rejects the player it requires at (40,12), after ordinary boot places
that player. An isolated correction exempts only the exact founder while retaining foreign
obstruction checks. Follow-on source audit found a second mismatch: Quickstart expects completed
heart rung1 immediately after normal founding, which only stakes/seals construction. The corrected
verification proves that actual authority without inventing completion. Earlier combined compile
and managed greens are retained; the native results below identify the later bytes actually run.

Existing pure/source checks and native grant-component fixtures did not execute genuine
Quickstart startup. A new offline engine-readiness diagnostic reports3 empty controls passed,
42 object cases BLOCKED at Unity Physics initialization; no founder assertion ran and no
red/green behavior proof is claimed. [Diagnostic scope](../DevTests/EngineQuickstartReady/README.md).
Required acceptance: all three real profiles, advisor on/off, normal founding, single physical
grants, and save/reload without replay. The current automated results below sign only their stated
layers; older native receipts do not sign this correction.
[Working proof](/tmp/taf-quickstart-founder-proof.3xBBuF/README.md).

The developer-only [genuine boot and save/cold-load route](../DevTests/QUICKSTART-BOOT-TESTS.md)
has executed. The retained6010 scheduling checkpoint passes `marsh yes` and `marsh no`, both
seed `#43101`: actual boot, SaveGame and separate cold load preserve heart/stock/IDs/clocks with
zero bootstrap replay. Strict result checks, exact-owned stops and independent idle checks pass.
`canyon yes` at the same checkpoint refuses before saving: `Garbage` at `(28,10)`, Physics present
and not in the graveyard. Post-builder Rusty-biome population can create Garbage, but the actual
biome selection and camp-call count were not witnessed; that origin remains unproved.

The first marsh/yes cold-load checker rejected only missing recognized TAF evidence despite the
real `INFO - Enabled mods: The Thousand and First [ALPHA] [DEV SCENARIO HARNESS], Pets of Harvest Dawn`
line. Exact list-token recognition is corrected; diagnostic scans and no-allowance checks remain
unchanged. Both cold-load results now pass the strict checker. Initial bootstrap refusals and
the initial checker failure remain retained, not waived. These pairs sign only their frozen6010
sources, not the replacement below. [Execution evidence](/tmp/taf-quickstart-native.sYl4Dz/README.md).

Current source prepares one camp after full `GetZone` completion and before founder placement,
reproves the exact completed zone/owner, then refreshes the native wall-connectivity/start-placement
cache without clearing existing bits or changing physical terrain. Physical readiness remains
separate; no safe-escape, unchanged RNG, or whole-zone clearance claim follows. Source review is
clear for this bounded correction. That `e922…` checkpoint passed all four compile modes+ABI
(99684), full managed13602/4989 with zero skips (17349), and501 Tools tests. Its genuine canyon
boot still refused: camp1/world1 succeeded, start reachability stayed true, but normal founding
rejected the authored ingress lane. Both required endpoints `(40,16)` and `(41,16)` lie outside
the old prepared mask. Their actual blocking objects remain unobserved.

### Retained startup checkpoint — `1c1c2bc` / `7e3fb752…`

The startup correction adds only those two cells to one shared engine-free preparation mask;
preparation, readiness and relocation exclusion use it. Exhaustive mask tests and the actual
shipped architecture compiler/entrance-route test cover the mismatch. Source review is clear.
Retained census3045 sources/431,407 physical lines/1414 direct-XRL/zero production cap failures;
Inventory SHA-256: `7e3fb7521f445e16b0740329966e369fa51d91fd768237bdffb5291d3540c651`.
Compile21734 passes all four modes+ABI; managed21635 passes13603 Taf/4990 Portable, zero skips.
Tools69769 passes501 tests. All six sealed profile/advisor combinations at seed `#43101`
passed actual boot, SaveGame and separate cold-load milestones. Each retained the exact heart,
stock, IDs and clocks with zero bootstrap replay. All six strict save checks and all six strict
load file-verification checks passed by11:25UTC.
All exact-owned game stops and independent idle checks passed. All3158 developer C# inputs in
the six source profiles match the actual developer compatibility compiler input set.
[Execution and source evidence](/tmp/taf-quickstart-native.sYl4Dz/README.md).

Earlier interrupted compile/managed jobs and the MSB3027/MSB3021 output-lock failure remain
retained; they are not green results. No current native PASS is inferred from old marsh pairs.
The retained six-combination developer matrix is green. QSB2–5, ordinary play, graceful in-game Quit,
Steam delivery of the correction and public release remain separate open gates. The developer
route parks only its owned game before turns and uses exact-owned Kill, not the game's Quit.

Retained pre-native extension evidence: compile30189 passed four modes+ABI (ordinary3041/3045,
developer3154/3158,113 Harness) at digest
`6cc05140593e77bebbe23c38ee4269b4b71ceb98c53d25187c81a611b6d9c128`.
After one stale source assertion, unfiltered rerun64122 passed13,600 Taf/4,987 Portable, zero
skipped; Tools46388 passed496. [Retained extension proof](/tmp/taf-quickstart-roundtrip-proof.RUDnOR/README.md).
The retained6010 compile25503 passed all four modes+ABI and managed95634 passed13,601 Taf/4,988
Portable, zero skipped. None of these old greens signs current `7e3fb752…` bytes. Integrated staging
hash batching retains all three independent reads; the measured3076-row manifest3.97s→0.08s
comparison is not whole-build timing.

The [tracked structural review](STRUCTURE_REVIEW_0_3_1.md) binds the current production bridge;
its exact inventory gate passes. Structural approval is not native or release approval.
Retained startup oracle finding: repeated stock-child references could counterfeit initial food
or material totals. Top-level absent heart/grants were refused; the separate stock correction
below closes this gap. It was not the observed canyon failure.

### Retained stock-hardening checkpoint — 2026-09-07

Startup evidence above is frozen at `1c1c2bc`, production digest `7e3fb752…`. Later stock
hardening adds a callback-free reference-identity guard before both initial quantity loops.
It retains normal stack quantities, existing role/receipt/custody checks, noninitial continuation
and save formats. Independent review cleared the two production files and corrected one native
fixture safety gap: material classification can invoke contents callbacks, so exact captured
authority is rechecked immediately before injecting the owned duplicate rows/raw count.

Retained canonical census:3046 sources/431,441 physical lines/1414 direct-XRL/zero cap failures;
Inventory SHA-256: `9d9eb6416014c7257a26fa08178d8f738e44dd46a295ea3357f32b7739faf1b0`.
The generated staging inventory contains 3077 files, including 3046 staged C# files; this is
not installed or subscribed-package evidence. Compile73169 TERMINAL0 passes all four C#7.3 modes
and ABI for 3046 sources, baseline and compatibility symbols: ordinary3042/3046,
developer3156/3160 with114 Harness shards. Managed47358 TERMINAL0 passes13625 Taf/5012 Portable,
zero skips. Tools73664 TERMINAL0 passes501 tests and the exact structural `--release` gate passes.
Against the complete startup inventory,3044 production files are unchanged, one modified and one
added. No earlier whole-game receipt is relabeled. Isolated prototype passed22 executable identity
cases,11 source-wiring cases and all four compile modes+ABI (53305), but that compile preceded the
two-line native-fixture guard. Prototype full suite54638 failed only missing local ignored
`_notes/CREED-KIND-EVIDENCE.md`:13624 pass/1 fail, Portable not run. That failed run remains retained.
Native49420 TERMINAL0 passes all16 synthetic creator/custody groups, including duplicate-child
negatives inside the larder/material creator groups, actual verifier refusal and healthy fixture
restoration. Exact-owned PID5068 stop and root idle check pass; canonical Player.log check passes
without allowances. This closes the bounded stock correction, not a rerun of the six startup
pairs against later bytes or current ordinary subscriber acceptance; component save/load remains
untested. [Retained stock validation](/tmp/taf-quickstart-stock-validation.CM1dD9/README.md).

## Retained pre-upload checkpoint — witnessed-death recovery integrated

Begin/arrest notices now freeze their text and tick before changing the announced flag. A saved
queue intent prevents blind replay after interruption; Homecoming explicitly acknowledges an
unconfirmed attempt without claiming it was displayed. Current ss5 embeds monotone transition
identity while canonical ss1–ss4 readers preserve historical bytes.

Named departures now retain exact Chronicle-capacity warnings before retirement, with no receipt
eviction or invented publication. The warning archive follows its owner across seat changes;
Homecoming clears only the exact owner rows it showed. Callback barriers compare every frozen
departure field and nested role snapshot. Independent bounded source reviews cleared both changes.

Full suites pass **13498 Taf / 4885 Portable**, zero skips; **440 Python Tools** tests pass.
Strict four-mode C#7.3 plus ABI pass. Current census:3045 staged C# files /431,086 physical lines /
1414 direct `XRL` imports /zero line-cap failures. Ordinary inputs3041/3045, developer3146/3150
with105 Harness shards. The resolver covers 3045 sources, baseline and compatibility symbols
selecting the ordinary inputs above. Direct `XRL` imports: 1414 files, 0 over the line limit.
Staging list:3076 files, not an installation. Inventory SHA-256: `ef84f9a05d894bdbc281e20aa1b5f02f45f4b0ca5a96771ffbeb3da903ad3f3f`; compiler
`987e8c0de7d08217ee256f9e30eeea4eb317f9fe27c988032c8e1bcc4969c2f6`, exact before/after.
[Integration evidence](/tmp/taf-031-owner-death.EsP03W/README.md).

Current D5 envelope v2 and both native witness/verifier contracts require ss5. Historical D5
v1/ss4 structural roundtrip remains covered; the current dispatcher refuses it before game loading.
This change does not relabel old native evidence or claim current gameplay acceptance.

Witnessed-death recovery now journals exact engine witnesses before row, role, tally and telling
updates.118 additional cases pass. Review corrections retain existing expedition results and
reject mismatched role/body claims. Final archive-owner correction passes46 compiled-code cases;
the exact [structural semantic review](STRUCTURE_REVIEW_0_3_1.md) is now bound and its gate passes.
A fresh ss5 save/cold-load pair passed at the preceding checkpoint, before the final owner/version
corrections; it is not retroactively relabeled. Final version-aligned native Prepared-roof death
smoke passes6/6: two actual engine deaths, exact accounting and unchanged roof/parent state,
then two no-replay recoveries. All3150 C# launch inputs match; owned game stopped and independent
idle verification passes. [Native evidence](/tmp/taf-031-death-native.mTKWRK/README.md).
Clean private packaging and actual upload/subscribed-content verification still block0.3.1.
Repeat-release finalization is a separate tooling lane;
first-attempt publishing need not wait for it. No upload occurred; public remains0.3.0/private empty.

## Retained Chronicle capacity refusal verification

Historical callback hashes now retain their own settlement-schema basis; new settlement cuts
record a separate basis. The outer realm archive is v9 with a fourteen-integer tail; v2-v8
remain readable. Existing hash text is never rewritten. The real beta7d v18 fixture and fifteen
authority/dependency regressions now run in the permanent Taf suite.

Retained archive-wave installed-engine serialization exposed an existing mismatch: dynamic nested writers had
generic readers. The reader now consumes the historical type token, requires the exact expected
type and refuses newly recorded nested errors. Writer bytes stay unchanged. Initial full-control
failure is retained; the corrected direct-archive probe passes **103/103**, including full v9,
synthetic v8, tail atomicity and poison/reset/original rethrow. Fixtures are quarantined and
codec-valid, not authoritative realm returns or historical whole saves.
[Probe instructions](../DevTests/EngineArchiveWire/README.md).

Full suites pass **13238 Taf / 4625 Portable**, zero skips. Strict four-mode C#7.3 and ABI pass;
production/compiler inventories match before and after. Current census:3026 staged C# files,
428,926 physical lines, zero line-cap failures. The resolver covers 3026 sources, baseline and compatibility symbols
selecting3022/3026 ordinary inputs; developer3125/3129 with103 Harness shards.
Direct `XRL` imports: 1407 files, 0 over the line limit. Generated package inventory:3057 files, not an
installation. Inventory SHA-256: `8864f0b058665f47640f42fc423c5a11bdeca42f89b7b77776ac9888a0aaef48`;
compiler `b7db683ae14e1c8b2ea5ff7647b3f78a0aecc358fd69495b2a89bc2a6b2256d1`.
[Current capacity preflight](/tmp/taf-capacity-integration.WXd14l/README.md).

A full canonical Chronicle registry now records explicit capacity refusal without evicting replay
receipts or pretending delivery. Reports settle ledger work first and retain visible warning evidence.
Current st3 report wire preserves canonical st1/st2 readers. New fixtures add95 cases; capacity-specific
native gameplay was not exercised. Named-departure capacity was a separate open finding at this checkpoint.

Installed-content verifier and fixed item lock are integrated in the tools tree; strict Windows
builds and15 package/14 installed-file groups pass without Steam calls. Fixed-root publisher CLI
now passes four strict project builds and all12 Windows suites, including14 CLI and real mutex/
lease groups;436 Python Tools tests pass. No actual registry or SDK access occurred.
[Publisher integration evidence](/tmp/taf-publisher-integration.w9HGSj/README.md).
Safe repeat-release finalization and actual subscribed-content verification remained pending
at this retained checkpoint; the current publisher section above records their later evidence.
[Delivery scope](/tmp/taf-delivery-integration-fixed.sj5Jfo/README.md).
Remaining Required gameplay fixes, fresh ordinary play and exact-inventory semantic review still
block release. Public Alpha remains0.3.0; private staging is unchanged. No upload occurred.

## Retained execution-clock and native save/load checkpoint

Subsidence execution now requires the actual world tick and a raw saved checkpoint justified by
its retained step/rung/option evidence. Invalid clocks refuse before option, pass or checkpoint
publication; structural Homecoming report acknowledgement remains separate. No saved field changed.
Full suites pass **13005 Taf / 4527 Portable**, zero skips; strict C#7.3 four modes and Hearthpyre
ABI pass. A Portable-only test dependency failure was corrected without production changes or
removing any clock assertion. [Clock preflight](/tmp/taf-clock-integration-preflight.jXd2Oa/README.md).

Fresh isolated native save and cold load pass against these exact inputs: four real clock-refusal
probes preserve captured state,15 real departures reach35/Town, and a saved interrupted release
recovers without replay. Recovery uses explicit production prepass, not native zone activation.
Both receipt-owned games are stopped; strict logs and3121-C# source seals pass. This remains
synthetic developer evidence, not ordinary or historical-save acceptance.
[Native save/load evidence](/tmp/taf-clock-native.tYyMoG/README.md).

At this clock checkpoint:3018 files /427,814 physical lines, zero line-cap failures.
The resolver covers 3018 sources, baseline and compatibility symbols selecting3014/3018 ordinary
inputs;103 Harness shards bring developer inputs to3117/3121. These are 3018 staged C# files.
Direct `XRL` imports: 1404 files, 0 over the line limit. The freshly generated cold-install
inventory contains 3049 files; this is not subscribed-content evidence.
Inventory SHA-256: `5db84b29e025b4310beb9667b11c2062860210d96af33f877782bb2d2c4836d1`.
Compiler inventory `6cda0d99191f58087151e53dc24f340d128cbb3997cd074c8836a2e0c40ae8f5` matches before/after.
Historical archive migration, remaining Required review fixes, ordinary play and Workshop delivery
still block release. The separate fixed-root registry prototype passes11 Windows groups, and its
new file-lease seam passes12 plus9 existing groups; neither is integrated with the CLI or finalizer.
[Registry scope](/tmp/taf-workshop-registry-fixed.A5kvkM/README.md). No Steam submission occurred.

## Retained wear-load quarantine verification

Wear now latches the entire custom read path and the engine's separate `ReadError` hook, retains
surviving receipt fields, and refuses later damage, leaks and repair continuation. The after-load
handler attempts one bounded player warning. Callback guards prevent later phase advancement
after naming, telling or removal callbacks quarantine the held part. Existing named fields/types
and the seven-field legacy reader order remain unchanged.

Real installed-engine method probes reproduce four failures in the original build, then pass all
four against both the corrected draft and integrated DLL, preserving all40 preset receipt fields.
These execute `Read`/`ReadError`, not framed `IPart.Load` or the visible warning. A separate real
direct-part wire probe now passes7/7: named v1, positional legacy, malformed versions/field type,
and two payload truncations. Original baseline passes healthy2 and fails the5 quarantine cases.
Original exceptions and assigned prefixes survive; failed parts refuse healthy retry bytes before
consuming them. Truncations occur after token-table initialization, not at full-file admission.
Framed-load, visible-warning and ordinary-play gates remain open. Twenty-two source cases also pass.
Both executable probes are retained byte-exact in `DevTests/EngineWearRead` and
`DevTests/EngineWearWire`; [rerun instructions](../DevTests/WEAR-ENGINE-PROBES.md).
Full suites pass **12934 Taf / 4456 Portable**, zero skips; strict C#7.3 four modes and Hearthpyre
ABI pass. The initial full run retained one stale logical-source helper failure. Its correction
includes the new partial while retaining exact declaration, reader/writer and field-order checks.
Evidence: [wear-load preflight](/tmp/taf-wear-load-preflight.IXP5Wr/README.md).
Current census:3016 files /427,683 physical lines, zero line-cap failures.
The resolver covers 3016 sources, baseline and compatibility symbols selecting3012/3016 ordinary
inputs;101 Harness shards bring developer inputs to3113/3117. These are 3016 staged C# files.
Direct `XRL` imports: 1403 files, 0 over the line limit. The freshly generated cold-install
inventory contains 3047 files; this is a staging manifest, not installed/subscribed-byte evidence.
Inventory SHA-256: `e294f7b7a205bdf37ce772a14ba800582ddcef2debbe28f8a039317d4d342666`.
Compiler inventory `6c21e405be2bbd8430e4a719307b81757b9f0d9a9cdf0c6240b11b0ab550ac77`
and source/project hashes match before/after. No game or Steam action in this wave.

## Retained CityBook/Homecoming verification

Homecoming now refuses a pending or malformed realm departure receipt at every existing exact-owner
barrier, including before display and immediately before ledger reset. It preserves the receipt,
counters and unread report instead of reconstructing completion. Its16 focused cases pass (14 real
receipt-protocol cases and two explicit source contracts); the pre-fix baseline failed the missing
guard source contract. Full suites pass **12912 Taf / 4434 Portable**, zero skips, with strict C#7.3
four modes and Hearthpyre ABI. This does not execute the native display/reset fault cut.
Evidence: [Homecoming preflight](/tmp/taf-homecoming-guard-preflight.jwORLY/README.md).
That census:3015 files /427,543 physical lines, zero line-cap failures.
That inventory SHA-256: `20cf5bda43e4171038916c99d6adc457939dcc228ba0ebe2d77d9025301e5ffd`.
Compiler inventory `7d7431f8bf2af6fcc93d1e0446c44e02713deb1ac00bdc18ba983d67f92f65d3`
and source/project hashes match before/after. No fresh native/game/Steam action in this wave.

The preceding CityBook correction passes **91 focused cases**, then **12896 Taf / 4418 Portable**
cases with zero skips, plus strict C#7.3 ordinary/developer baseline/compatibility and Hearthpyre ABI
compilation. Twenty-two new regressions cover refused normalization, failed-load latches, frozen
ragged columns, healthy fast paths and the separate named-load exact projection capability.
Independent review found no further Required issue in the two-file correction. Source/project
hashes and compiler inputs match before/after. That production digest is
`6c22450b57745e28f421dce5e7df456347e2d91107039391099ce57dac3f95f2`;
compiler digest is `537bbbd4b5be1c6e3bc135b2d7413d56778f11594ff831bdc8b9fcd28e3b6f9e`.
Evidence: [CityBook preflight](/tmp/taf-citybook-guard-preflight.cNStwc/README.md).
These are direct-source checks, not a committed candidate or new native acceptance. The older
native pair below binds production `1e09f0b7…`, not these changed CityBook bytes.

A historical archive compatibility regression is now executed: real beta `7d331fe8` version18
code produced retained bytes; the version19 consumer fails three exact authority-hash preservation
cases (Intent, Attempting, Settled), while three controls pass. This is the actual archive hash
component, not a full engine callback or a native historical-save test. Migration repair remains
open. Evidence: [historical archive regression](/tmp/taf-archive-authority-version.SI524N/README.md).

Claude's separate versioned-projection and hash-basis selector helpers pass57 injected tests,
with independent scoped review clear. Root additionally executes exact positive projection,
current re-save and changed-field controls for all18 historical test-writer schemas, plus the
actual beta7d-produced v18 payload:19 cases pass. The initial actual-artifact case mistakenly
fed its outer fixture envelope to the settlement decoder; failed source/log are retained, and
only fixture unpacking changed. These helpers remain scratch-only; runtime hashing, persisted
basis provenance and complete historical callback recovery are not yet integrated or proved.
Evidence: [archive basis seam](/tmp/taf-archive-basis-preflight.8A0BWO/README.md).

Previous native pair `/tmp/taf-rung-forecast-native.kiQuK7` passed actual save, cold preactivation,
exact recovery reports/remaining writes and same-tick no-replay. **Overall native gate FAIL**:
strict logs caught `RunGame: InvalidOperationException` while enumerating faction heirlooms,
before save import completed. Pinned engine source shows the harness bypassed vanilla's
core-thread Continue wait and exposed a half-loaded running game. A harness-only correction
routes through that Continue wait, retains a terminal task until owned shutdown, and leaves
production factions, saved Running and serialization untouched. Final direct-source checks pass
**12874 Taf / 4396 Portable**, zero skips, plus strict C#7.3 four modes and Hearthpyre ABI.
Sixteen new concurrency cases and two dispatcher source cases cover this seam; exact journal
format remains unchanged. Compiler digest `46addd06fe80c042e3db4180a143d196de798df36d4b6b10f702466cd9c089d8`
matches before/after; 101 Harness sources, developer3112/3116. Evidence:
[load-barrier preflight](/tmp/taf-load-barrier-sealed.4TZOfh/README.md).
Fresh corrected native pair now **PASS**: actual save04:45:55.968 UTC and cold load05:04:51.358 UTC,
same GameID65f23899-c2fb-4b93-a65f-7cf98f4b1a80, exact35 loaded bodies and interrupted release
recovery/report/no-replay. Both strict journals and raw logs pass, including post-stop recheck.
Recovery used the **explicit production prepass**, not native zone activation. Owned processes
stopped, independent idle checks pass, and all3116 C# inputs match each launch seal. The cold
profile contains3156 sealed input files. Evidence: [fresh native pair](/tmp/taf-load-barrier-native.KeMv91/README.md).
This developer witness does not sign ordinary gameplay or historical-save compatibility.

SDK-free publisher-launcher parser fixtures pass **109**; actual Windows guard fixtures now pass
**26/26**, after reproducing and correcting evidence rename/reparse, input write and descendant-stop
failures. Corrections are isolated outside this source tree; not integrated or release-ready.
The separate upload-attempt directory lease defect is now reproduced by actual Windows access
probes: original8/9 groups, corrected9/9 with exact sharing violations and before/after access.
That fix, legal-before-I/O callback classification (**384** cases), and Claude's corrected
submission-observation codec (**83** wire cases + **9** probes; independent8 +3 +6 checks) are
now integrated into this source tree. Integrated Windows suites pass **30 protocol /15 package
/9 attempt** groups plus all evidence cases above. Strict C#7.3/net9 publisher and test-project
builds pass with **zero warnings/errors**, using the installed pinned Steamworks reference;
the publisher was compiled, not executed. All test inputs match before/after hashes, and scoped
independent integration review is clear. Evidence: [publisher regression and integration](/tmp/taf-attempt-lease-fix.mdgItU/README.md).
The subsequent durable-record wave is now integrated and freshly verified on Windows. Claude's
corrected single-attempt latch passes183 checks, including14 race aggregates; the unchanged root
32-trial fixture now reports zero conflicting observations (original17/32 failure retained).
Real Windows observation-write fixtures pass14 cases/9 groups; post-upload ordering41 and cleanup17
cases pass. All earlier publisher suites pass again. Both strict C#7.3/net9/x64 test and publisher
builds have zero warnings/errors. The exact34 publisher source/project/launcher inputs match before
and after both runs; installed Steamworks hashes match the pinned lock. No SDK call was made.
Evidence: [durable-record integration](/tmp/taf-submission-record-corrected.SaDhC8/README.md),
native outputs `C:\taf-upload-integrated.W8DbXv`.

The publisher durably records callback/outcome facts before later content and metadata checks,
retains every active/partial receipt, and emits success only after cleanup. Review caught and fixed
previously swallowed cleanup failures; all cleanup actions now run before aggregate failure escapes.
This does not finalize a release. Fixed-registry enforcement is still required because the current
CLI accepts an attempt-root argument. Harness bytes remain closed; CityBook, Homecoming and wear are
the subsequent production corrections, recorded above.
Private upload, subscribed-installed bytes, ordinary play and safe repeat-release finalization
remain unsigned. No public update was made.

Root reproduced a fresh hash-bound production review inventory: all3015 sources enumerated,
64 new/75 modified/2876 byte-unchanged against beta7d331fe8, zero deletions. Six disjoint packets
cover all139 changed files. Heart/graveyard packet01 found no Required defect in its19 files.
Reports packet04 and serialization/CityBook packet05 each found two Required issues. Packet02 adds
failed-read wear quarantine; packet03 adds Homecoming's pending-departure reset and executable raw
checkpoint validation. Packet06 adds lost death accounting while a pending rung refuses CityBook
publication. CityBook, Homecoming and wear guards are corrected above, with scoped native fault
coverage still open; the other five source findings need correction. All six packets cover139 changed files at
their recorded hashes, not approval of later edits. This is not whole-inventory approval. The existing structure ledger
still binds the older inventory. Evidence: [review packets](/tmp/taf-production-review.mwV2fq/README.md).

The retained pre-fix CityBook regression had2 passes/5 failures; the extended baseline had4 passes/
11 failures. Those failed logs remain intact after the correction. Other source findings still
need executable regression/correction: a full Chronicle receipt registry can strand a pending
aggregate report; begin/arrest telling can lose one register across a display callback;
negative/future checkpoint validation is absent
from execution; a pending rung can prevent durable death accounting. The archive finding has
the separately scoped executed component regression above.

Separate Workshop staging item **3796495680** was created with user approval and independently
verified Private. Public Alpha **3794797472 / 0.3.0** remains unchanged. No candidate content,
preview or manifest tags have been uploaded to staging; no subscriber-delivery claim.
The guarded client-API uploader preserves Qud's `manifest_id` and `manifest_version` tags,
holds a durable per-item attempt fence, and refuses automatic retries after uncertain submission.
Windows protocol/package/attempt checks pass **30 / 15 / 9** groups. The installed-byte checker
proves only exact local package bytes, never subscription or fresh Steam transfer.
Authority and remaining delivery gates: [publishing guide](../Tools/WorkshopSteam/PUBLISHING.md).

The isolated draft adds a separate developer save/cold-load witness for the second actual
release-receipt write. The previous witness checkpoint passed strict C#7.3 ordinary/developer
baseline/compatibility and Hearthpyre ABI compilation, with independent codec review.
Retained full suites: **12786 Taf / 4308 Portable**, zero skips; **436 Tools PASS**. The snapshot codec
has 167 focused passing cases. An initial Harness line-cap failure was fixed by removing three
comment lines; no behavior or test guard changed. First fresh native run reached the two-write
cut but refused before saving: the witness incorrectly required nonnull incident text on a fresh
receipt. The witness now compares exact nullable text and expects only unfinished writes;
176 release-focused tests pass, including both null and retained-text cut/recovery traces.
The second fresh native run also refused before saving, at its combined work-plan assertion.
Source investigation found a one-work assumption despite the actual completed heart remaining
eligible for production's independent ruin selection. The failed log does not expose the decoded
plan, so the selected-heart explanation is code-derived rather than directly logged.
Actual Claude's correction is now source-closed and independently reviewed: preserve both built
objects and verify the exact selected set, typed designation/pose and frozen terminal authority.
A new pure shape validator passes **47 focused cases**, covering selected and unselected hearts
and refusing omitted, injected or changed rows. The passive release observer separately tracks
the exact companion's writes and durable publications. The heart proof precedes the successful
cold preactivation journal. Both new source-contract checks pass in the full suites below.

Retained heart-witness preflight passes **12835 Taf / 4357 Portable**, zero skips, and **436 Tools**.
Strict C#7.3 ordinary/developer baseline/compatibility plus Hearthpyre ABI compilation passes;
only the retained CS2023 response-file warning appears. Managed runs also report NU1900 because
the vulnerability-data service is unavailable; no tests were skipped. All compiler, ABI, reference
and selected XML input digests match before/after. That Harness inventory: **99** sources;
ordinary **3011/3015**, developer **3110/3114**. Compiler digest
`95ae4279816b077a2a3ff81a4f04940917f87f2af79a8d907d8f31f83595478c`;
production remains `1e09f0b742b7ceb5005af889e7cdd411dcdcfabf4aff6d83e7bb3123bbb845bf`.
These are direct-source checks, not clean-candidate release receipts.
Fresh native SAVE now passes, including the real completed heart and two-write durable release
cut. Cold load passed preactivation but failed the exact report-text expectation before the
recovery success row or same-tick retry. Root and actual Claude independently traced a harness
expectation defect: the saved batch has 10 retired departures plus 5 in the active step; the
helper expected the summary for only 10, while production reports the final 15. The correction
forecasts the immutable closing batch without advancing release/report state or weakening exact
report/no-replay checks. Its **21 focused cases pass**, zero skips; fresh full suites pass
**12856 Taf / 4378 Portable**, zero skips. Strict C#7.3 four-mode and Hearthpyre ABI compilation
also passes, with exact before/after input digests. The next native pair passed its journal but
failed strict startup diagnostics as recorded above; the corrected-barrier native pair now passes
with the exact scope recorded above. All earlier failed evidence remains
retained. The older 49-survivor save scenario stays separate. Production and serialization layout
are unchanged. Retained forecast inventory added one Harness source (100 total); current barrier
adds one further source (101 total).
Retained pre-correction Harness inventory: 96 sources; ordinary 3011/3015, developer 3107/3111. Compiler digest
`caebc152eb6ddd3401ae222b2b83082841dd3ebcbe79247fad83ff5dafca2c57`, unchanged before/after;
production digest remains `1e09f0b742b7ceb5005af889e7cdd411dcdcfabf4aff6d83e7bb3123bbb845bf`.
Evidence: [latest heart preflight](/tmp/taf-rung-heart-preflight.obaVSD/README.md),
[closing-forecast preflight](/tmp/taf-rung-forecast-preflight.TNiK48/README.md),
[heart save and failed cold-load evidence](/tmp/taf-rung-heart-native.yEX7p4/README.md),
[retained nullable-line preflight](/tmp/taf-rung-null-line-preflight.IAAoTC/README.md),
[retained first native failure](/tmp/taf-rung-save-native.6xXqFV/README.md),
[retained second native failure](/tmp/taf-rung-null-line-native.SbEMtw/README.md).
Earlier compiler/native receipts below retain their exact older Harness scope; they do not sign
this reopened slice. Clean integration, candidate gates, ordinary native acceptance and Steam delivery
remain required before another Alpha release. Beta is not complete.

## Isolated subsidence integration — not a release candidate

### Retained D5 production checkpoint

The resolver inventory covers 3015 sources, baseline and compatibility symbols selecting
3011/3015 ordinary inputs; the 89 Harness shards bring developer inputs to 3100/3104.
Frozen production census: 3015 staged C# files / 427,499 physical lines; no source reaches
300 lines. Direct `XRL` imports: 1402 files, 0 over the line limit.
Inventory SHA-256: `1e09f0b742b7ceb5005af889e7cdd411dcdcfabf4aff6d83e7bb3123bbb845bf`.
The generated cold-install inventory contains 3046 files; this is not installed-package or
Steam subscription proof. Authority: `/tmp/taf-rung-release-preflight.WsuyRe/census.json`.
This line-cap result does not bind exact-inventory semantic review or sign a release. The
matching compiler, suite and scoped developer-native evidence follows; historical snapshots
below retain their original counts and hashes.

Current D5 durable work-receipt release passes strict C#7.3 four modes plus compatibility ABI.
Full suites pass **12614 Taf / 4136 Portable**, zero skips; **376 Tools** pass.
Matched native rung regression passes **6/6**; release-specific cold-load coverage remains open.
Compiler inventory before/after:
`94935dedc4342a39cf12078a79233f1f33940e616b843844db700605695f04e3`.
Evidence: [release preflight README](/tmp/taf-rung-release-preflight.WsuyRe/README.md).
The production-used release driver persists Intent before four individually measured receipt
writes, then Released. Only Released stops consulting the former carrier. Exact identity,
designation, unique attachment and competing-job checks remain required before acknowledgement;
movement after physical proof is permitted. Canonical sr1 loads as Pending, never Released.
Already-published reports remain readable; new reporting and retirement require durable release.
No wear-part save fields or version changed. These new bytes do not inherit older native receipts.

Fresh `/mnt/c/taf-scenario.CQTr4p` passes the six-group rung persona at
**2026-09-05 23:13:18.860 UTC**: fifteen real departures, interrupted capture and actual wear,
production roof/release/report/retirement recovery, then same-tick no replay. Fixture work/home
and elapsed checkpoint are synthetic; world time is unchanged. Nine journal rows, strict
diagnostics, all3104 C# inputs matching the closed seal, owned PID18320 stopped and post-idle PASS.
Evidence: [D5 native README](/tmp/taf-rung-release-native.khkfGl/README.md).
This does not execute release-specific native write cuts, Released-carrier disappearance, ordinary
play or sr2 release save/load. The full-suite/Tools counts above bind the D5 snapshot; subsequent
Workshop automation tooling is under development and needs its own verification.

Retained preceding strict four-mode/ABI PASS and full suites **12430 Taf / 3952 Portable**,
zero skips; **376 Tools**. Compiler inventory before/after:
`81831757aa11e06adbf920fb47868721e5703fcf62bd09846400bc52ecaa416c`.
Evidence: [retirement-negative preflight README](/tmp/taf-retirement-negative-preflight.1xIHMU/README.md).
Its production inventory was `79fd124abf8bd9b51486a78df905a9f59c104d7317b9667840d142b16748187b`.

Retained preceding full suites passed **12420 Taf / 3942 Portable**, zero skips, with compiler
`825b40adaf89d1c1dac25f1d37e98c61ec5f388006035f9a2dc8945716b762bf`.
That final full-suite receipt superseded the earlier 12416-Taf run in the same directory.
Evidence: [recorded-removal preflight README](/tmp/taf-heart-recorded-removal-preflight.ws81AS/README.md).

Durable founding-heart retirement now distinguishes an absent native tombstone from conflicting,
duplicate or unreadable evidence. Only exact canonical Removed-or-later terminal authority,
placed final identity/shape, typed mirror and removal marker, phase-appropriate all-table roots,
and live predecessor absence can substitute for an expired tombstone. The new `.07q` proof is
observational: it does not invoke the full component validator, which may quarantine broken
architecture. The later terminal driver still performs that audit. Fresh Destroy measurements
and exact-reference tombstone proof are unchanged; existing same-binding zone/final mirror repair
is also unchanged. This is not a universal no-repair claim.

Retained earlier matched native `/mnt/c/taf-scenario.HWkU08` passed **15/15 lifecycle groups** at
**2026-09-05 22:26:12.983 UTC**, with nine journal rows and strict diagnostics PASS. The prior nine
real founding, callback-refusal, terminal-completion and no-replay groups are followed by six
observational retirement negatives: duplicate tombstone, null collection, an actual 65,537-entry
collection, wrong owner, wrong slot and live predecessor conflict. Full retired authority refused
each fault; exact scoped restoration and restored authority passed. All 3099 C# inputs match the
closed launch seal. Owned PID36372 stopped and post-idle PASS. Supplied completion tick 5727;
world tick 5127 remained unchanged.
Evidence: [retirement-negative native README](/tmp/taf-retirement-negative-native.v6Kp54/README.md).
This does not prove ordinary turns, save/load, throwing readers, direct pooling, unloaded zones,
generic plots or root-after-write refusal. A null collection is not a throwing reader; these six
cases do not exhaust every mutation of the persisted fallback authority.

Retained earlier native `/mnt/c/taf-scenario.fktnsa` passed **9/9 lifecycle groups** at
**2026-09-05 21:34:28.389 UTC**, including actual terminal completion and recovery without replay.
All 3096 C# inputs match the closed 3134-file launch seal; strict diagnostics PASS. Owned PID30176
stopped and post-idle PASS. Supplied completion tick 196527; world tick 195927 remained unchanged.
Evidence: [recorded-removal native README](/tmp/taf-heart-recorded-removal-native.A20gbZ/README.md).
These synthetic-fault/future-tick groups do not prove tombstone pooling, cold load, ordinary turns,
generic plot completion, root-after-write refusal, or release readiness.

**Retained combined completed-heart and partial-step persistence PASS** in
[save/load evidence](/tmp/taf-completed-heart-save-load.47UPwt/README.md).
Source `/mnt/c/taf-scenario.RdCle9`, GameID `b62ba856-74ec-4886-b096-68776077437f`, completed the
heart through production before synthetic 50-resident setup. Its real Primary save passed at
**2026-09-05 21:41:31.493 UTC** after one of five departures, leaving 49 residents; eight rich-record
roundtrips also passed. Owned PID39308 stopped and post-idle PASS before exact import into fresh
`/mnt/c/taf-scenario.vOeZAW`.

At **21:56:38.181 UTC**, the load witness matched v2 terminal/effects, final mirror/removal marker,
final geometry, frozen architecture and absent saved roots, plus raw polity, seven typed heart
reservations, ss4, clock, autoonce and all 49 body/row/binding identities. This is before
AfterGameLoaded handlers and zone activation; engine AfterLoad callbacks had already run.
The observational digest does not independently rerun architecture component simulation.
At **21:56:40.342 UTC**, the production heart-reservation audit and explicit production prepass
proved the remaining four departures, population 45, original-step retirement and no replay.
All four journal rows and strict raw-load verifier PASS. Owned PID37172 stopped and post-idle
PASS. Both profiles' 3096 C# inputs match their closed launch seals: source 3134 files, destination
3136. These are the earlier `825b40ad` compiler bytes, not the current 3099-input developer tree.

Developer authority v2's baseline remains inside externally hashed Primary; the external 49-body
ss4 snapshot does not independently commit heart fields. Production save schema and exact
stopped-source import protocol are unchanged. This signs one synthetic completed-heart/partial-step
cold-load boundary, not ordinary or historical saves, mid-callback loads, generic plots, direct
pooling faults, native negative fallback coverage or release readiness. Claude TASK31 found no
Required production issue. The later six negative cases above separately exercise bounded parts
of the [next-coverage design](/tmp/taf-heart-recorded-removal-artifact.mLWR4E/next-native-negative-coverage.md);
they do not broaden this earlier cold-load receipt.
Older staked-heart pairs remain retained and do not sign current bytes.

Retained terminal-completion follow-up fixed source-proved blockers. The completion-name
reader now agrees with the writer's plain frozen building name; the physical works object keeps
its separate `plot:` label. A detached final object is validated with the existing stamped-rect
reader, exact frozen plan rectangle and explicit owner-zone bounds, rather than a placed-object
reader that necessarily refuses an object with no current zone. Precise terminal refusal logs
identify the failing step without relaxing allocation, custody or publication guards.
The next native run placed the final building but exposed global-only tombstone lookup: Qud
routes placed-object destruction to the originating zone's graveyard. The correction inspects
bounded global and already-loaded zone graveyards, retaining duplicate-ID and exact-reference
proof. It neither loads remote zones nor infers destruction from absence or recycled objects.
Ordinary plot outputs also had detached rectangle and footprint readers that required a current
zone. Their pre-placement proof now checks frozen typed geometry and captured/reproved live
predecessor-zone bounds. Placed-object reader contracts remain unchanged. Five regression source
contracts cover each correction; these do not stand in for native ordinary-plot completion.

The current fifteen-group lifecycle persona drives the full same-name/site founding transaction through
four callback refusals, then clean founding and one scenario commit. It walks the founder clear
using bounded native movement before real construction stages. The completion-time argument is
explicitly synthetic; world time is unchanged. Two final-allocation fault cases must preserve
original/foreign identities, and clean completion must prove terminal effects, the exact
predecessor tombstone and no replay. Six subsequent observational retirement faults require refusal
and exact restoration; no destructive pooling or unknown-evidence cleanup is performed.
The fixture also re-resolves the live faction and requires
its exact explicit integer-zero pending flag, not a missing-key default or stale captured object.

Retained strict C#7.3 four modes plus compatibility ABI PASS: 3009 production files, 84 Harness
shards; ordinary 3005/3009, developer 3089/3093. Full suites **12368 Taf / 3890 Portable**, zero
skips; **376 Tools**. Production census: 426978 physical lines, 1400 direct XRL imports, none at
or above 300 lines. Compiler `b7b2ae43c4173a26a86b5c381e9e1ecd6649603a979ead7e310d07c6b45b9ade`;
production `c496acbfbd45fbc48928154d108a6346f313e5ddffd25fdf2b5e27bbd6718aa0`.
Evidence: `/tmp/taf-heart-zone-tombstone-preflight.wNad0I/`. Initial main-suite stale source-pin
failure is retained; full rerun passes. Matched native `/tmp/taf-heart-zone-tombstone-native.IysjGn/`
passes **9/9 lifecycle groups**: full founding recovery, all six callback refusals, terminal
completion with exact originating-zone tombstone, and real recovery/audit without replay.
All 3093 C# inputs match the closed 3131-file launch seal; strict log gate PASS, owned process
stopped and post-idle verified. Supplied completion tick 92127, unchanged world tick 91527.
This is scoped developer evidence, not ordinary-turn, generic-plot, pooling or save/load proof.
The documentation checker no longer crashes on a missing
ignored research note; present local notes and required tracked documents remain audited.

Retained staked-heart partial-step persistence passed real save/owned quit/import/cold load in
`/tmp/taf-current-heart-save-load-retry.kMrxZ2/`: source Fu0nJD saves one-of-five departure plus
eight rich-record roundtrips; fresh tNVSDb restores exact ss4/clock/autoonce, 49 body/row/bindings,
raw polity and seven typed heart reservations before AfterGameLoaded handlers and zone activation.
Explicit production prepass completes the remaining four and proves no replay. Both3093-C# seals,
strict logs, owned stops and post-idle PASS. Setup remains synthetic, not ordinary progression or
historical-save compatibility. Its heart remains staked, so this does not prove completed-heart
persistence. Earlier pKRVmX preparation correctly refused a concurrent changelog edit before launch;
all staged bytes were frozen before the successful fresh retry. Full suites rerun unchanged:
12368 Taf /3890 Portable, zero skips;376 Tools. Documentation freshness was CLEAN at that checkpoint.

The subsequent source-proved blocker was: after a completed heart's finite engine tombstone is
pooled or omitted from a cold load, post-Removed recovery still demands it and aborts construction
passes. The durable-retirement correction above addresses that dependency without accepting
conflicting, missing or malformed authority. The separate current completed-heart cold-load pass
is recorded above; this retained staked-heart pair itself does not establish that result.

Retained strict C#7.3 four modes plus compatibility ABI PASS: 3008 production files,84 Harness
shards; ordinary3004/3008, developer3088/3092. Full suites **12358 Taf /3880 Portable**, zero skips.
Compiler inventory `92dadb768e145eb57fdb3e8965ea2443c9efbc88d86519b80999b500df9d839e`;
production `940b6c6d808a6e2782bea8120021e4fec298aec5acf24084d19001aa4f20cfbc`.
Evidence: `/tmp/taf-heart-detached-final-preflight.rAUh6I/`. Matched native completion validation
in `/tmp/taf-heart-detached-final-native.imsSNV/` passed 7/9, then refused at RemovalAttempting.
Its 3092 C# inputs matched the launch seal before the graveyard correction; owned process stopped
and post-idle verified. These earlier receipts do not sign the later correction or its terminal pass.

Earlier lifecycle attempts are retained: YFeK9A passed5/9 but the founder occupied the footprint;
arMlNA and cl6mc6 passed7/9 but clean finalization failed. Em7zrZ also passed7/9 and its precise
log identified the prepared-final-shape refusal, leading to the detached-rectangle fix. The
separate arMlNA reservation regression passed7/7 on its pre-fix sealed bytes. Their owned games
were stopped and post-idle verified. No old profile was reset or repaired to obtain a pass.
Ordinary game-turn progression, mid-callback save/load, remaining native retirement fault cuts,
root-after-write refusal and remaining semantic/release/subscription gates are still open.
Public Alpha remains0.3.0.

Retained checkpoint at **2026-09-05 19:18 UTC** hardens founding-heart reservations: strict UTF8,
exact slot spellings, all five typed state tables, seven-key preflight and add-only publication.
Malformed/null/empty, wrong-table and colliding rows refuse without being replaced. Factory guards
pin the game, tables, transaction, plan and zone; only the exact pre-event factory reference may
receive a heart ID. Foreign replacements are not adopted or cleaned up. Final-output cleanup also
requires proof that no saved root exists, including after a rooting call returns false.

The developer save observer now requires a canonical profile immediately before hashing and
reproves that exact profile/revision afterward, without reconciliation in the observation path.
Strict C#7.3 four modes plus compatibility ABI pass for 3008 production files and 80 Harness shards
(ordinary 3004/3008, developer 3084/3088). Compiler inventory
`e2593980a33b7ed381a81545f3681656077c82517fe6bd598b516cbd4130bff0`;
production inventory `0200f24bec6290af2e55272af3bfabb230ecf577128a9455a7d84e8861231391`.
Full suites: **12344 Taf / 3866 Portable**, zero skips; **373 Tools tests PASS**.
Census: 426923 physical lines, 1399 direct XRL imports, no production shard reaches 300 lines.
Evidence: `/tmp/taf-heart-reservation-probe-preflight.2acEzL/`.
The first seven-group native attempt in `/tmp/taf-heart-reservation-native.rYy7v9/` failed: three
probe XML merges ran before this mod's blueprint definitions and were discarded as MODERROR.
Real founding completed, but groups2–7 did not execute; owned PID27048 stopped, post-idle passed.
The corrected harness adds a probe part to three exact loaded blueprints, only after fresh-profile
eligibility, preserving all original part-map references. The fresh retry **passed all seven
groups** at 19:17:32.562 UTC in `/mnt/c/taf-scenario.99mr6S`, seed `#612726967`: nine journal rows
and exact diagnostics passed; no diagnostic allowance was added. All 3088 C# inputs match the
launcher-verified closed seal. Owned PID35900 stopped; authoritative post-idle passed. Evidence:
`/tmp/taf-heart-reservation-retry-native.R2H1OK/`.
The new persona's scope is real founding, 246 synthetic typed-state setups and direct shared-guard calls with
native callbacks; it does not establish terminal completion, root-after-write refusal or ordinary-save compatibility.
Earlier save/load and subsidence receipts below do not sign these changed bytes. No release claim.

Retained checkpoint at **2026-09-05 18:25 UTC** fixes a concrete founding-heart reservation
format mismatch: the writer emits `hs1-` plus 64 lowercase hex digits, but the reader demanded
bare 64 hex digits. Runtime encoding and reading now share a pure, tested `hr1` codec; no bare-hex
migration or completion-seal format change was introduced. Sixty-three focused executable cases
cover writer/reader agreement, frozen bytes, roles, malformed seals and identity mismatches.

The developer fixture now explicitly assigns human species and runs normal resident-identity
reconciliation over its 50 surveyed residents. Survey alone did not persist species counts;
the later profile reconciliation could therefore replace the fixture's valid human profile with
unresolved provenance. A new observer hashes the raw polity envelope and all seven exact typed
heart reservations, plus their zone/system identity. It checks that authority before save, after
serialization and before AfterGameLoaded handlers and zone activation; its baseline lives inside the signed Primary, not an
independent external commitment. Canonical profile and real heart audit checks remain mandatory.

Fresh `/mnt/c/taf-scenario.WyO1pV` passed the save check and eight rich-record checks at
17:57:36.936 UTC: nine journal rows, exact diagnostics, one of five departures completed and 49
residents retained. Owned PID26088 exited. Its exact stopped save was imported into fresh
`/mnt/c/taf-scenario.ZxmPxO`. **Cold-load PASS**, including strict raw-log verification:
at 18:17:18.415 UTC, exact polity/heart authority, partial-step bytes, clock, autoonce and all 49
resident identities matched before AfterGameLoaded handlers and zone activation. Canonical profile
and real heart audit passed after return. An explicit production prepass completed the remaining
four departures at 18:17:20.316 UTC and proved same-tick no replay. All four journal rows passed;
no diagnostic allowance was added. Owned PID10080 stopped; authoritative post-idle PASS. Evidence:
`/tmp/taf-save-load-authority-native.USnpdH/`. No ordinary player save was selected. This signs the
synthetic partial-step boundary, not ordinary progression, archive exchange or historical saves.

Read-only review at that checkpoint found follow-ups: malformed/null/empty or wrong-table reservations could
be misclassified by runtime getters; alternate slot spellings and lossy UTF8 remained accepted. The
developer canonical-profile check also needed repeating at capture, after departure; no intervening
demotion was demonstrated. Current-byte native regression reruns passed below. These findings
are addressed in the later continuation above, not covered by the old valid-state persistence case.

Retained regression rerun: **14 / 14 native checks PASS**, seed `#612726967` in two fresh sealed
profiles. Six rung cases passed at 18:22:14.848 UTC in `/mnt/c/taf-scenario.3w8yVi`; eight
report-loss/homecoming cases passed at 18:24:41.887 UTC in `/mnt/c/taf-scenario.H0eaDH`. Both
nine-row journals and exact diagnostics passed. All 3078 C# inputs match each launcher-verified
closed seal; this is source-to-seal proof, not execution of every source file. Owned PID35584 and
PID36596 stopped; authoritative post-idle PASS. Evidence:
`/tmp/taf-subsidence-authority-native.PlZOr6/`. The earlier sandbox-only attempt in `t8q4ac`
failed on WSL interop before any game launch and remains retained, not counted as native execution.
Synthetic setup and case limitations are unchanged from the retained rung/report descriptions below.

Retained full suites: **12240 Taf / 3762 Portable**, zero skips; **373 tool tests PASS**.
Strict direct-source C#7.3 four-mode compilation plus compatibility ABI passes. Compiler inventory
before/after `fd87ff7caf28406091436bfaedea325939c1aec3f7943196816b0664d4c476b6`;
production inventory `35fbecce72bc3c145998a4fed27d16af550bd8e80fa4e087ad02785a638065df`.
Ordinary 3001/3005, developer 3074/3078 sources, 73 Harness files. Census: 3005 production files,
426653 physical lines, 1397 direct XRL imports, none at or above 300 lines. Evidence:
`/tmp/taf-save-load-authority-preflight.RT5qTW/`. This is not a canonical release gate;
exact-inventory semantic review remains unbound. Earlier native receipts do not sign these bytes.

Retained save/load checkpoint at **2026-09-05 17:24 UTC**: four field-preserving named-record adapters
remove the binary formatter fallbacks exposed by the first native save. Empty inheritance now
accepts zero reconstruction only when its complete no-authority shape is valid; payload and
negative-reconstruction states still refuse. Actual rich-record engine checks pass all eight
named and trusted fixture-generated legacy-format cases. Historical-save compatibility is unproved.

Corrected pair `/mnt/c/taf-scenario.nAELtC` → `/mnt/c/taf-scenario.J0IMnA` saved one-of-five and
reloaded exact pending bytes, clock, autoonce and all 49 residents. The production prepass completed
the remaining four credits without replay. **Overall load FAIL**: AfterGameLoaded raised an engine
dispatch exception and skipped later activation. Four OK journal rows do not override that error.
Both owned processes stopped; raw evidence `/tmp/taf-save-load-fixed-native.lFsf13/` is retained.

The dev-only witness now observes the player's `GameRestored` event immediately before the
unpatched singleton event dispatch, testing whether the previous Harmony hook caused the failure.
Cause remains unproved. Fresh source `/mnt/c/taf-scenario.aEPDXT` passed save plus all eight record
checks at 17:04:48 UTC; owned PID39220 stopped. Fresh identical-content load
`/mnt/c/taf-scenario.lBgjFn` at 17:19:27 UTC restored the exact49 bodies and saved state, then
completed the remaining four credits and no-replay retry through an explicit production prepass.
The BCL dispatch exception did not recur. **Overall load still FAIL**: downstream seal loader
reconciliation rejects canonical phenotype provenance; a founding-heart identity refusal is also
logged. No diagnostic allowance was added. Owned PID34072 stopped; authoritative post-idle PASS.
Production versus synthetic-fixture cause is under investigation. No ordinary player save was selected.

Retained checkpoint suites: **12174 Taf / 3697 Portable**, zero skips; **373 tool tests PASS**.
Strict direct-source C#7.3 four-mode
compilation plus compatibility ABI passes; compiler inventory before/after
`f29b0d3ee4e31ca36cdba64c7920288850392b8a155cb0364be42ddab6d4f5cf`, production inventory
`604ed7bb6e49fb89c471c934ae7e6a0eb37d6da8a99fe6df360db2934c91f5a1`.
Ordinary 3000/3004, developer 3072/3076 sources, 72 Harness files. Evidence:
`/tmp/taf-save-load-hook-preflight.7TKAey/`, `/tmp/taf-save-load-hook-native.5H7Ksw/`.
Earlier native rung/report-loss evidence below remains valid only for its frozen bytes.

The current isolated draft is based on beta `7d331fe8`; it is not committed, packaged, or
published. The old proportional batch loop has been replaced by a durable step driver. Actual
resident-departure receipts retain partial credits; only completed/cancelled step retirement
adds those credits to the frozen slide batch. Work/roof effects and dated ledger/Chronicle
reporting must settle before the step retires. A saved option transition preserves its original
checkpoint; malformed or wrong-table evidence is retained, not reinitialized.

Retained rung evidence at **2026-09-05 15:00:26 UTC**: **6 / 6 native checks PASS** in fresh sealed
`/mnt/c/taf-scenario.zlZnS7`. Fifteen actual departures cross City→Town; a native work-name callback
interrupts capture before damage. Real capture freezes one pre-worn hut and its exact surviving
resident. A synthetic parent wear intent and injected authority cut leave the real wear write at
MutationIntent; production pre-pass recovery credits it once, dates the condemned-roof record,
releases the wear receipt, tells the exact stage/work/departure lines and retires all three steps.
Recovery and retry reprove full live identities, the unique attached wear part, the complete roof
tuple and released receipt; both register prefixes survive. Owned PID37100 stopped; profile and
effects retained. All **3068** executed C# files match the reviewed draft. Work, initial home,
City stage and elapsed checkpoint are synthetic setup, not construction or lodging acceptance.
Evidence: `/tmp/taf-subsidence-rung-final-native.QNwGKu/`.

The same reviewed bytes also passed the existing **8 / 8 report-loss checks** at
**2026-09-05 15:02:54 UTC** in separate fresh sealed `/mnt/c/taf-scenario.JgCcan`.
Owned PID10096 stopped; all 3068 executed C# files match. Both personas completed nine journal
rows and passed exact diagnostic checks. Combined retained result: **14 / 14**, two profiles.

Retained report-loss evidence at **2026-09-05 14:07:16 UTC**: **8 / 8 native checks PASS** in fresh sealed
`/mnt/c/taf-scenario.GxrgLc`. The earlier five departure/retry cases below were rerun; three added
cases exercise seeded terminal Chronicle-loss recovery, a guarded empty-ledger reset with no
reappend, and retention of news changed during the homecoming display callback. Full failed
witnesses survive retirement until explicitly read. Terminal sink dispositions are synthetic
cut inputs, not an actual sink callback interruption; the homecoming adapter uses a captured
display callback, not attended UI. Owned PID14108 stopped; profile/effects retained. Exact expected
logs pass, and all **3064** executed C# files match the draft. Native evidence:
`/tmp/taf-subsidence-loss-native.3FcoM3/README.md`.

Retained rung-checkpoint automated evidence: **12,001 / 12,001 managed**, **3,552 / 3,552 portable**, zero skips;
53 persona-tool tests pass. Strict C#7.3 warning-enforced four-mode compilation and ABI fixture pass
(ordinary **3000/3004**, developer **3064/3068** sources; 64 Harness shards). Compiler before/after:
`74c216187f107535107fb702c1b029813b83912dfc75b4796da27247bd6d6d0d`.
Runtime: `adfea8c109413395b349a43561a580185219ee06d4ad81794045e946c4397931`.
Census **3004** production files / **426548** physical lines / **1393** direct XRL imports /
zero at or above 300 lines. Direct-source preflight, not a canonical release gate; semantic review
remains unbound. Evidence: `/tmp/taf-subsidence-rung-verified-preflight.apKn69/README.md`.

At the earlier **2026-09-05 13:02:50 UTC** checkpoint, a fresh sealed native profile passed **5 / 5** checks:
real founding and 50 residents; one departure with four still owed at the original anchor;
the remaining four in the same step; an interrupted summary declaration; same-tick summary
recovery and no replay. The script produced nine journal rows and passed exact expected-log
checks (one injected Chronicle refusal and two installed Pets DLC warnings). Profile
`/mnt/c/taf-scenario.oIvpEt`, its seal and all effects are retained; owned PID16308 stopped.
Evidence: `/tmp/taf-subsidence-driver-native.xIYznZ/`.

These checkpoints are synthetic native seam evidence with a seeded elapsed checkpoint and unchanged world
time. They do **not** sign ordinary progression, interrupted resident carriers, general work/roof
callback cuts, absent completed works, option/master callback cuts, archive exchange, or save/load
at those rung/report boundaries. The newer partial-step save/load pair above does not close those
distinct cases. They remain required before release; exact-inventory semantic review is still unbound.

The current follow-up implements explicit terminal reporting loss, full failed-witness retention
in `ss4`, canonical `st1`→`st2` report migration, and guarded homecoming acknowledgment. Physical
retirement may proceed with retained failed telling, never a delivery claim. Reading homecoming
settles an interrupted ledger intent before clearing, checks all news and owner state across the
popup, and preserves newly arrived news. Missing works now refuse receipt release; moved live works
require exact identity and unique own receipt proof. Unknown missing/unloaded/destroyed works still
need native recovery work. The five-case receipt above signs none of these later changes.

Earlier driver automated evidence: **11,947 / 11,947 managed** and **3,498 / 3,498 portable** cases,
zero skips. Strict C#7.3, warning-enforced native-reference compilation passed all four modes:
ordinary 2,997/3,001 and developer 3,056/3,060 (baseline/compatibility), plus the optional
integration ABI fixture. Compiler before/after:
`996be40fdd2af3febfee6e6c08ced25892a5d5cbfda0f07b2ee951cbff0e1c34`.
Evidence: `/tmp/taf-subsidence-driver-native-preflight.DGKkMJ/README.md`.

Census: 3,001 production C# files, 425,927 physical lines, 1,393 direct XRL imports, zero files
at or above 300 lines. Runtime digest:
`8d1d8983e64d373f1cbe9bd9654bbb7b65a9e47aa0a9ad63a6861941a654b3a0`.
This is direct-source preflight, not a canonical release gate. The earlier 12:07 option-only
checkpoint and the beta counts below remain historical evidence for their own bytes.

## Retained beta automated evidence — `7d331fe8`

Claude's reviewed raid launch-order foundation is integrated: frozen projections and leases publish
before all factory effects, then the existing resume path creates, places, and activates each actor.
Two bounded native launch cases now pass with clean TAF diagnostics, including published authority and prior placed actors
inside later creation callbacks, and second-mint different-blueprint substitution with retained
evidence, quarantine, and no retry mint. The native loader also exposed two shipped roster defects:
Snapjaw base templates and excluded Missile Cannibal prevented those profiles from loading. The
data fix uses spawnable numbered Snapjaws and eligible cannibals; all five profiles now load in
both native cases. Same-blueprint custody, swallowed callback throws, interrupted placement,
ordinary raids, and save/reload remain open. The next Alpha also requires
a separately allowlisted private staging item, two-item receipt support, a new patch version and
fresh subscription/save-reload evidence; current 0.3.0 receipts cannot sign these changed bytes.
The canonical resolver selects 2951 sources, baseline and compatibility symbols.

| Layer | Latest result | Scope |
|---|---|---|
| Beta full managed suite, 2026-09-05 | **PASS** — 11,246 / 11,246 cases, 0 skipped | Fifteen additional raid fixture/data source contracts cover exact probe variants, 2:1 Steading weight, all-five-profile native gate, callback-entry snapshots, retained evidence, and disarm-only exit. Existing Quickstart, raid, lifecycle, discovery, subsidence completion and process-ownership coverage retained. SDK 9.0.306; installed base configured, skips forbidden. Existing ignored creed-kind evidence remains byte-identical to the release workspace. Both projects are engine-free; these source contracts are not installed blueprint eligibility or native play proof. Final managed and Tools logs retained at `/tmp/taf-raid-warning-clean-tests.Jn3NvJ/`; earlier `GDPMoP` logs remain historical. |
| Beta portable suite, 2026-09-05 | **PASS** — 2,809 / 2,809 cases, 0 skipped | The same fifteen additional raid fixture/data contracts plus existing production rule and discovery/process-ownership coverage. Native seam runs do not close full embark/save-load acceptance in TESTING QSB1–QSB5 or raid displacement 28b.1–28b.2. The separate unintegrated durable-subsidence draft is not included in these beta counts. |
| Native raid launch and shipped roster eligibility, 2026-09-05 | **PASS — 2 / 2; TAF DIAGNOSTICS CLEAN** | Fresh `raid-launch-native-a` at 08:55:05 UTC in sealed `X0BWSf` proves three real per-actor Create/Add/Activate sequences, exact published projections during callbacks, earlier actors physically placed at later callbacks, and final unique IDs/markers. Fresh `raid-launch-native-b1` at 08:57:32 UTC in `ldZwJE` substitutes a Chest at the second mint, retains the first actor and original/substitute evidence, quarantines the published operation, and proves no remint on a second activation. Both load all five profiles and pass the corrected raw-log checker. Model wire round trip only, not game save/load. Both use synthetic founding/store/incident/Steading/party setup and unchanged world time; ordinary acceptance is false. Journals/raw logs/profile references/owned-stop logs are retained in `/tmp/taf-native-raid-warning-clean.zqlWbN/`; actual profiles and ownership receipts remain under `/mnt/c/taf-scenario.X0BWSf` and `/mnt/c/taf-scenario.ldZwJE`. Owned PID38272/2740 stopped; profiles retained. Earlier `qoZCFq` gameplay passes remain rejected diagnostic evidence because of the missed CS0114 warning; `NHEbfz` remains failed before minting. See [TESTING.md](../TESTING.md#beta-raid-launch-ordering--bounded-native-evidence-and-open-acceptance). |
| Subsidence post-loop summary ordering, 2026-09-05 | **PASS — 13 / 13 managed completion cases, 12 / 12 harness source contracts, and 3 / 3 bounded native checks** | At 05:07:58 UTC, sealed `kvjIzc` ran actual founding/enrollment, five physical departures (50→45), an exact native summary-event fault after bookkeeping, and same-tick no-replay. Nine journal rows, four exact diagnostic counts, remaining-log check, owned capture/PID36172 stop and subsequent idle all pass. Raw logs and profiles retained at `/tmp/taf-native-subsidence.a9XJXf`; earlier `7f9diy`/`oA5XdS` reports remain RED for incomplete diagnostic lists despite their 3/3 gameplay checks. Stage/elapsed checkpoint are synthetic setup; no world-turn, breakpoint, ordinary-play, or save/load acceptance. Fractional debt, committed-but-pending departures and inner-rung interruption remain open. See [TESTING.md](../TESTING.md#beta-subsidence-completion--bounded-evidence-and-open-recovery). |
| Raid outbox exception recovery, 2026-09-05 | **PASS — 34 / 34 focused cases in each managed project** | Actual published raid operation, exact sink authority, five sink masks, successful/false/throwing callbacks, hostile diagnostics, quarantine recovery fence, and exact lifecycle wire round trip. No serialized fields changed. These engine-free tests do not prove native notification callbacks, ordinary raids, or game save/load. |
| Test discovery fail-closed probes, 2026-09-05 | **PASS — 14 cases in each managed suite; five separate executable probes on final TestMain** | Seven classifier tests plus seven actual-runner cases use tiny separately emitted assemblies to reject TestCaseSource and TestFixtureSource before eligibility/filtering, report both, and never execute poisoned getters/constructors/methods. Ordinary unmatched control refuses; matched Test/TestCase controls pass two tests. Final standalone entry-point probes pass 5/5, retain earlier artifacts, and bind TestMain SHA-256 `2eb01fccf669e33ef370d9f3203d54a0fa18e1f8d2ffe5397e1fa543e792122b` in `/tmp/taf-discovery-poison.r34Ud9/final-verification-results.json`. |
| Windows process-ownership regression, 2026-09-05 | **PASS — 17 / 17 real-process cases** | Exact held process handle, PID/start ticks/executable/twelve parsed argv tokens; tampered and borrowed receipts refuse, only the owned process stops, same-name decoy survives, profiles/seals/sentinels retained. Harmless windowless probes, not Qud gameplay or screenshot acceptance. Persona stop and capture require the same receipt; no global kill or profile wipe remains. |
| Owned persona lifecycle, 2026-09-05 01:38 UTC | **PASS — one real Qud persona, 16 / 16 native cases** | Fresh sealed `CSxdek`, seed `#1012026`; journal assertions, archived Player.log, receipt-owned native PNG, exact PID 35464 shutdown, and subsequent idle check passed. All profiles/seals retained. Evidence `/tmp/taf-owned-persona.zjhpE6`; not full-matrix, image-quality, ordinary-play, or save/load acceptance. |
| Native Quickstart seam persona, 2026-09-05 | **PASS — 16 / 16 cases; journal expectations and Player.log clean** | Fresh sealed profile, seed `#1012026`, marsh zone `JoppaWorld.8.22.1.1.10`. Real creators and same-reference recovery, foreign obstruction, native inventory/placement/destruction through injected adapter delegates, retry fences, and five-table quarantine presence. The realm remains unfounded before and after. Synthetic fixtures only: not complete Quickstart embark, native event-handler fault injection, save/load, ordinary acceptance, compatibility behavior, or a rerun of the full persona matrix. See [TESTING.md](../TESTING.md#beta-quickstart-cleanup--native-seams-and-open-acceptance). |
| Native raid-outbox seam persona, 2026-09-05 | **PASS — 6 / 6 cases at 03:18:46 UTC; journal, Player.log, capture, and owned stop clean** | Actual local Chronicle/ledger/message/deed delivery, successful idempotence and retirement, Chronicle refusal/retry, and four real effects followed by synthetic interruptions. Quarantine retains Intent and exact lifecycle bytes through recovery/replay/retirement attempts. Fresh sealed `zTzq5k`, seed `#1012026`; realm unfounded before/after. Native messages and UI effects retained, not rewound. Evidence `/tmp/taf-native-raid.XMFQ5q`; profile/seal retained; idle check passed. Not ordinary raids, founded identity, engine-raised faults, diagnostic delivery, game save/load, or visual-quality acceptance. See [TESTING.md](../TESTING.md#beta-raid-outbox--protocol-evidence-and-open-acceptance). |
| Last clean release audit | **PARTIAL PASS** — 10 of 11 automated layers green at committed candidate `19fb8ee`; structural release layer red | Docs/hygiene, ABI, inventory, baseline/compatibility compile, 7,586-case full suite, 171-case portable suite, architecture/XML/art, balance, 43-case smoke harness, Workshop package, and deploy dry-run passed. This remains a bounded committed receipt, not proof for later structural revisions. |
| Staged runtime inventory | **PASS — all four modes with warnings enforced, 2026-09-05 09:13:18 UTC** | Canonical `Tools/gate.sh --keep` exited 0 in `/tmp/taf-raid-warning-clean-gate.TnECdB/`: ordinary2947/2951 and dev3006/3010, 59 Harness files; retained stages `yMry4n`/`Yt0E9H`. Current receipt binds compiler `4d5dfd0ac22fbd8e58017c5b5b2e3fbd533016f5a5b8209ffbab6696329e6bab`; warning level4 and warnings-as-errors, only CS2023 exempt. Raw gate log SHA-256 `5a17e6ea10fd945f99fa3427a86b6ae955c689bcd16c2438635c33421cee750b`. Production C# remains `f9815fff2a1cf4389ecd42b733645b0611b31bbc8b58c96fae7d1636099e81b1`; XML is separately bound by the direct-source preflight and matched native profiles. Earlier `GDPMoP`/`Qu2bt9`/`ffFmkx` results remain pre-correction `-warn:0` evidence, not current acceptance. |
| Strict C# 7.3 direct-source preflight | **PASS — all four modes with warnings enforced, 2026-09-05** | Ordinary2947/2951 and dev3006/3010, 59 Harness files. `/tmp/taf-raid-warning-clean-preflight.UQ6WII/` binds unchanged before/after compiler `4d5dfd0ac22fbd8e58017c5b5b2e3fbd533016f5a5b8209ffbab6696329e6bab`, production C# `f9815fff2a1cf4389ecd42b733645b0611b31bbc8b58c96fae7d1636099e81b1`, ABI fixture and both XML inputs. Reference template `58b709bf…` enables warning level4 and warnings-as-errors, except the existing CS2023 response-file notice. The preserved pre-fix probe fails an actual negative-control compile with CS0114 under this policy. This is direct-source proof, not a canonical stage or fresh native receipt. |
| Cold-install inventory | **CURRENT SOURCE SNAPSHOT — 2982 files; package/native release evidence open** | Canonical staging enumerates 2982 files. The tagged Alpha's 2976-file receipt does not cover this beta tree. Package shape does not sign native load, behavior, appearance, or Steam subscription. |
| Optional ownership bridge | **INTEGRATED STATIC PASS; native matrix open** | Manifest cold-list model proves absent/exact-2.2.3/wrong/disabled/failed/bad-load-order selection and no dropped runtime C#/XML. Installed Hearthpyre 2.2.3 manifest plus three used source files match pinned hashes and the tracked ABI fixture; core foreign-type and bridge mutation/lifecycle bans pass. No native overlap/save/re-enable case is signed. |
| Architecture generator | **PASS** — 146 maps, 107 bindings, 122 tiers; 3,512 transformed fabric cells and 22,084 exterior site facts | Generated XML is byte-current. It composes 527 creed fixtures in 248 deliberate pairs across 705 programme regions, plus reviewed housing renovations and deep-end campuses. Unsupported spatial programmes remain absent instead of receiving stretched padding. These are the generated subset, not the complete registry counted below. |
| Architecture checker | **PASS** — 144 buildings, 134 plotted buildings, 89 palettes, 333 maps (187 source / 146 generated), 220 plans, 226 bindings, 262 tiers, 344 variants, 1,376 variant/pose goldens, zero issues; three expected installed-base tolerant-recovery warnings for malformed vanilla Creatures/Furniture/Items XML; reference-grounded quality audit 1,376 / 1,376 static pass, 0 fail; largest exact a4 receipt 7,798 bytes / 10,468 characters (`greatfoundry/craft-xl/templar/purpose-greatfoundry-templar-xl0+purpose-forge-foundry`) | Static topology, exact footprint/roof authority, bounded frontage routes in all poses, required-use circulation, transition route and custody proofs, fixtures, palette/material/technology constraints, typed-lot coverage, deterministic snapshots, exact purpose/exotic runtime anchors, hidden activation gates, and the runtime codec envelope are checked. All 1,376 poses still require native/human appearance and function acceptance. |
| Benefit-provider content | **PASS** — 114 catalogue rows, 187 authored variants, 105 unique explicit fixtures | Exact design/provider affinity, obtainable portable stock, fixed-installation reasons, caps, and absence of catalogue-as-supply fallbacks are checked. Native function and appearance remain separate evidence. |
| One-survey focused tests | **PASS** — 14 focused source-contract cases | Maintained named indexes, active-pass consumers, mutation observation, and the absence of reachable second whole-zone scans. Dense native scan instrumentation remains open. |
| Addendum 9 structural census | **CURRENT SOURCE SNAPSHOT LINE-CAP GREEN; EXACT-INVENTORY REVIEW SIGNED** | 2951 staged C# files / 420,997 physical lines: 0 exceed 300 physical lines, 0 are exactly 300, therefore 0 fail the strict cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Direct `XRL` imports: 1376 files, 0 over the line limit. Inventory SHA-256: `f9815fff2a1cf4389ecd42b733645b0611b31bbc8b58c96fae7d1636099e81b1`. `docs/STRUCTURE_REVIEW.json` binds the retained subsidence baseline plus independently reviewed one-file Claude raid launch-order delta to this exact digest under the author's Addendum 9 ruling of 2026-09-02 (see `docs/STRUCTURE.md`). |
| Pre-market full source suite | **HISTORICAL PASS** — 10,624 / 10,624 cases, 0 skipped | Superseded by the beta full managed run above. This older receipt does not sign current bytes. |
| Pre-market portable suite | **HISTORICAL PASS** — 2,325 / 2,325 cases, 0 skipped | Superseded by the beta portable run above. Public CI keeps its installed-data skip allowlist; canonical release testing forbids skips when Qud data is present. |
| Tools tests | **PASS — 345 / 345 tests** | Six new actual-shell lifecycle/log-checker methods cover exact base/Alpha/dev/Alpha-dev title warnings and errors, the real CS0114 false-green regression, foreign-warning scope, retained raw evidence, and owned stop. One reference-renderer regression pins warnings-as-errors with only CS2023 exempt in both modes. Full run29.708s; focused lifecycle18/18 and renderer4/4. Both old native raid logs are now rejected by the corrected checker. |
| Workshop uplift preparation, 2026-09-05 | **LOCAL METADATA INTEGRATED; NOT PUBLISHED** | Independent factual and code review passed. Canonical short/long copy and six browse tags are synchronized; focused metadata 16/16 and full Tools 338/338 pass. Isolated package harness exits 0 with one explicit bind-alias environment skip; this is not zero-skip release acceptance or an uploaded/subscribed package. Version remains 0.3.0; next Alpha still needs a new patch, separate private staging identity/schema, fresh subscription/save-reload evidence and publication. |
| Art tests | **PASS** — 28 / 28 tests; semantic subset 4 / 4; 125 verified vanilla tile references; 0 custom runtime paths | Snapjaw caches now use ordinary Woven Basket art, Hindren textile works use the ordinary Sewing Machine as a treadle stitcher, and TAF faction emblems use a deterministic glyph-only projection instead of Joppa terrain art. Art policy, installed-path, reference, and architecture-declaration checks are green. Native original-scale appearance, emblem recognition, and a current preview remain open. |
| Latest retained native smoke | **PARTIAL PASS** — fresh profile, founding, 17/17 checks, one production gallery case, save/cold-load, repeat 17/17, clean log | Clean commit `19fb8ee` deployed against Qud 1.0.5/core 2.0.211.51. This signs only that commit's loader/founding/single-sample persistence smoke. Later structural revisions have no native compile/load/log receipt and are not covered by this row. |

Historical checkpoint `d285129` passed 7,743 / 7,743 cases and 173 / 173 cases in its two
managed suites; its then-current Tools suite passed 35 tests and its Art suite passed 23 tests.
Those numbers identify that old receipt only. The preceding decomposition ledger also recorded
144 additional oversized authorities, 154 cumulative, before the final plot-staking split above.
Current evidence is the larger working-tree census in the table.

Reproduce the current static results:

```bash
./Tools/gate.sh
./Tools/check-manifest-directories.py
./Tools/check-hearthpyre-abi.py
dotnet run --project DevTests/TafTests.csproj --no-restore -v q --nologo
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py
python3 Tools/check-structure.py --report
```

## Implemented architecture boundary

Static authored-map, binding, material/style, frontage, road, delve, inheritance, and preview
targets are complete at code/content/checker scope. Native gallery, accessibility, compatibility,
exact-inventory human review, and current preview evidence remain open. Hosted-arcology topology and authored programmes are
implemented at this same static boundary; their native traversal and visual acceptance remain open.

- Plots reserve typed lots. They do not stand in for buildings.
- A building occupies a lot through a frozen authored map, exact size/type binding, pose,
  entrances, functional fixtures, material palette, technology minimum, and deterministic variant.
- Defence and siting are separate: only an unplotted defensive work is a cap-exempt frontier
  segment. Defensive plotted buildings retain their authored lot, category, cap cost, and base
  defence rating.
- Automatic `UpgradesTo` tiers keep lot identity only inside the frozen exact binding; explicit
  directional same-type/same-size transitions keep it, and reviewed `additive-expand` or
  `renovate-expand` transitions may grow the envelope after proving adjacent ground and ingress.
  Retype, shrink, relocation, and replacement use strike plus fresh siting and a new lot identity.
- Gatehouses use a traversable road-bound topology. Delves use paired physical travel endpoints.
- The assenting moot has a complete XL floor, inert-safe vanilla-art fixtures, and a current
  runtime owner. Its commission key is derived for the seated city only while assent research,
  the founder's Chavvah rite, and claimed surface ground cardinally adjacent to Moon Stair terrain
  all remain true; the key is never permanent knowledge. One exact finished moot owns bounded
  named assent/exemption and a reversible native ambient-stabilization ward.
- Inheritance freezes witnessed authored receipts and a connected street graph; it never carries
  items, liquids, charge, or mutable object identity between runs.
- External ownership is a read-only provider protocol. Exact Hearthpyre 2.2.3 is the only shipped
  typed translator and is absent from the compile set in every other dependency state. First and
  later city receipts, plus ordinary ground claims, disclose observed overlap and offer free reject
  or exact bind. The chosen mode/evidence is inside the receipt digest before water debit and
  remains permanent TAF claim data; active-ground divergence gates load/turn/semantic and mutating
  Charter work without loading remote ground or taking a foreign lifecycle. Automated
  isolation/ABI/codec/CAS/source contracts are implemented; native
  reject/bind/save-disable-re-enable/divergence/log evidence remains open.
- Visual-state cues derive from real construction, staffing, wear, deprivation, and network state.
  Current runtime presentation is vanilla-art/glyph-first. Original assets—including disclosed
  generative-assisted drafts only after pixel-level human revision—are permitted through the
  provenance, rights, editable-source, wiring, fallback, package, and independent native-review
  policy in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
  The semantic fixture pass replaced Chiliad-specific basket art on Snapjaw caches with the
  ordinary Woven Basket, replaced Nacham's unique charged wire-extruder art on Hindren textile
  works with an ordinary Sewing Machine presented as a treadle stitcher, and replaced the
  Joppa-tile faction helper with a deterministic TAF-owned glyph-only emblem. No raster or
  architecture map was created or changed by that pass. Native tile-scale taste remains unsigned.
- Food and water are separate physical flows. Water retains dedicated vessels, water details,
  upkeep, and scarcity. Food runs seed → authored crop row → physical harvest → dedicated larder →
  explicit meal, mill, industry, or trade debit. A shared meal requires spendable ingredients and
  a currently capable physical cooking provider; completion grants bounded creed/cohabitation
  progress, never population capacity. Empty pantries and missing kitchens withhold the act and
  spend nothing. Abstract foraging, daily ration bills, hunger catch-up/marks/departure, passive
  food-rate minting, and stock auras are retired; legacy save/wire fields normalize inert. Food
  never binds population: live supported level and subsidence use water plus roofs, so zero food
  cannot shrink a settlement and additional food cannot raise its base population level. Damaged
  larders never passively spoil or debit pantry stock: identity and count survive arbitrary absence,
  and an open legacy food-loss receipt clears inert before callbacks.
- Archive-v17 fixed-rate arrival cadence and its lifecycle receipts are the sole current v1 arrival
  authority. The historical Growth-1B/schema-5 oracle froze only a hostile parser and terminal
  canonical validator; its own freeze receipt explicitly supplied no transitions, wire, C#, save
  root, caller, materialization, tuning, or gameplay. Porting it beside the current cadence would
  create a second arrival authority, so it is rejected as parallel architecture—not deferred v1
  debt. Replacing cadence later would require a new ruling, migration, and full evidence owner.
- Creed semantics now use a mergeable six-kind registry while preserving every public/save
  `Creed` key. Installed 2.0.211.51 mapping covers 33/33 admitted factions (4 community, 16 people,
  2 polity, 7 order, 2 doctrine, 2 cult). Only four shipped doctrine/cult keys can drive passive
  conversion or shrine consecration. Gyre Wights are conservatively a non-theological people:
  their exact affiliation still gates architecture and civic practice, but never shrine output.
  Unknown modded keys remain neutral affiliations, and explicit water rites use
  adoption/allegiance prose.
- The founding handbook is situated rather than canonicalized: Neseva Cask-Hand's Uru Ux 1000 AR
  copy belongs to the Open Basin fellowship and carries a marginal historical countervoice while
  retaining every actionable instruction. Exile and return separately freeze exact authored
  official/outsider entries, before/after list hashes, and a domain-separated pair fingerprint in
  TAF-local Chronicle receipts. New disputed transitions never write Sultan/world history or
  vanilla accomplishments/murals. Static content, migration, idempotence, and interruption
  contracts exist; native presentation and every-cut save proof remain open.
- Physical-building benefits separate exact designations from current providers. Catalogue values
  are caps only; furniture and native capabilities supply them, optional semantic build-key tags
  prevent cross-design substitution, and every operation scales with current root condition.
  Fifty-one load-validated roles support player designation: enclosed ordinary housing/work rooms,
  ordinary open yards/grounds with exact catalogue-sized rectangles, and one exact dry-container
  larder. Network, crop, power, laboratory, fixed-creed, Heart, remote, purpose/crown, and hosted
  machinery remain authored pending their own typed physical proof.
- The purposeful-megastructure portfolio implements exactly five symmetric compatible edges and
  ten directed recipes across Deep-Bore, Great Foundry, Granary-Colossus, chimeric theatre, and
  becoming annexe. One exact bootstrap funds the second shell; one exact return is consumed by a
  paid activation operation; later operations alternate, consume one exact input cargo plus frozen
  local water/material/food and any selected existing body service, then transport one exact output.
  Durable CAS receipts, ordered dual exact construction inputs, lease-safe debits, explicit
  dispatch/pickup/landing checkpoints, orphan recovery, and authored XL/creed floors are implemented.
  Automated gates are green for the purpose family; native Pass 37 remains acceptance evidence.
- Succession configuration implements Charter seniority, exact chosen-life selection and its
  optional seat climb. The activated groomed-successor law keeps realm-bound exact `ResidentId`
  plus monotonic service/schooling proofs; ready nominees inherit lawfully, while missing,
  departed, duplicate, or unfinished nominees fall back to seniority without chosen-life cost.
  Save/config migration and automated gates are implemented; native Pass 36 remains open.
- `KingdomSystem.PolityLedger` is the realm-scoped semantic authority for bounded polities,
  directional relations, immutable profiles, routes/fronts/grievances, finite cohorts, scarce
  figures, witnessed incident plans/conclusions, projection receipts, options, and compaction
  evidence. Its strict v7 envelope carries bounded typed phenotype cues, reads v1-v6 without
  inventing cues, and provides opaque inert future-payload preservation, canonical ordering,
  fail-closed quarantine, and hard capacities. Foundation now
  publishes the current realm plus at most one opted-in legacy partner/rival by typed CAS; owned
  faction projection/recovery, immutable profile→NPC resolution with exact source/reason
  traceability and deterministic weighted body/role/skill/mutation-or-cybernetic/gear/signature/
  cargo/dialogue expression, exact resident-successor bridge,
  finite endpoint bodies, no-backlog presentation, caused diplomacy, all seven cohort schedulers,
  three-city traffic, exact Trade consignments, loaded hospitality, witnessed intervention/death,
  consented escrow, deterministic direct records/aggregates, and shared W0 capacity are wired.
  Physical endpoints require distinct route-reachable cells, exact recursive custody and removal
  witnesses; visible death crosses `EarlyBeforeDeathRemoval` → `BeforeDestroy` → `OnDestroy`, and
  completed cleanup precedes the one W0 release.
  Current foundation body pools consume only exact positive species counts plus audited
  identity/body tags; subsequent revisions rebuild from exact per-city population-body facts.
  Origin, culture, style, creed, and architecture never choose current bodies, and unrecognized
  evidence stays `unresolved` so the current resolver refuses instead of inventing a human.
  Foundation technology is `KingdomZoning.Tech(System) * 2`; later facts derive each city's band
  from its sorted zoning roster through `TechPoints`/`LevelForPoints` and take the bounded maximum,
  never `Stage * 2`. Existing legacy profile/resolver rules remain frozen rather than reinterpreted.
  Exile/refound causally ends old semantic authority, tombstones exact owned factions, restores
  byte-identical authority on return, or imports only bounded institutional facts under fresh ids.
  Polity transaction closure is frozen at 61/61 focused cases; integrated automated and physical
  narrow checks are green. Pass 39 native behavior remains open.
- Reopened civic-experience code scope is complete: two named voices, optional remembrance,
  explicit offices, staffed loci, fixed witness works, First Guest choice/hosting, First Feast
  practice, curiosity and civic leads, body history, non-custodial artifact recognition, manual
  communal rite, joint civic view, integrated three-return Guest's Feast, site practice, named-cook
  vacancy/handoff, and bounded vocation services have separate exact owners. Civic market service
  now requires one accepted staffed `taf:market` provider on designated ground, the same exact held
  office receipt, Village, and current standing 3 or better. It may open empty. `ShopTier` is
  current operational standing/reach and may fall to zero; Chronicle receipts, not this field, own
  history. Native TradeUI sale/purchase is the only ordinary stock ingress/sink: TAF generates no
  wares, consignments, periodic restock, passive output, or remote debit. The sealed
  `GenericInventoryRestocker` is only an empty-trade adapter. Sold, bought, stolen, dropped,
  corpse-, player-, container-, or foreign-held goods retain physical identity, count, location,
  native `_stock`, and foreign state while only TAF receipts/guards retire. Completed or dormant
  legendary traders remain finite personal native merchants after civic loss/accession, but supply
  no civic authority without provider plus office; only open prepared handoff endpoints are
  temporarily succession-ineligible. Growth stage no longer promotes the first citizen, and
  production contains no generic `TakeOnRoleEvent` office notification.
  Integrated automated evidence is green; native office/service recovery and the human promotion
  protocol remain open.
- Explicit prepare-save-for-removal is implemented while the mod is present: it fences new work,
  plans exact visited-ground and global owned cleanup, reports unvisited locators, retires faction
  projections, persists an identity fence, and permits a fresh incarnation only through monotonic
  high water. It never promises that disabling/removing the mod first can clean a save.

## Remaining v1 evidence work

- **Research-alignment P0 fan-in.** The profile-body, zoning-technology, merchant-office,
  Gyre-kind, founding-book, and disputed-history/food findings in the
  [2026-09-01 audit](../_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md) now close at their stated
  code/content/static scopes. The audit's release-evidence P0 remains open: none of those repairs
  supplies current native full-loop, complete gallery, 27-zone arcology, exact-inventory human
  structure, current preview, compatibility, or cold Workshop-subscription proof.
- **Research disposition coverage.** That audit now crosswalks the complete current comparator,
  generative, early/system-design, succession/quest/lab/growth-oracle, polity, food/water,
  Qud-affordance A1–A13/R1–R6, lore P0–P2, and ecosystem EC-01–EC-14/L1–L12 finding families.
  Every row is either implemented at an explicitly narrow
  code/content/static scope, superseded or rejected with a reason, or retained as an exact
  native/human/release gate. Direct Landing Pads registration, Bethesda/Bethsaida or vanilla
  historic-site ownership, global Coda/Sultan history, generic dynamic quests, synthetic gossip,
  and original-artifact custody are not missing v1 owners: their unsafe proposed mechanisms are
  rejected. Open research outcomes remain voluntary mature return, person/deed recall, cost of
  presence, purpose/staff/state recognition, hauling/restoration/population tuning, Girsh/Nephilim
  route coexistence, semantic tile taste, and representative mod-stack behavior; automation must
  not claim those human/native results.
- **Frozen automated fan-in.** Production owners exist for every accepted positive row. Current
  baseline/compatibility/development compile, Qud-referenced, portable, tooling, provider,
  architecture, and 28/28 Art evidence is recorded at its exact frozen snapshot. Package, native
  persona/compatibility, human visual, exact-inventory semantic review, current preview, and Steam
  evidence remain; no static green substitutes for those receipts.

- **Routed construction-input evidence.** Ordinary construction now mints one centrally owned job
  for its exact water/material bill when lawful local custody is absent. The job freezes nearest
  holders, itinerary, carrier, landing, debit, rollback, recovery, and master-pause authority;
  remote stock is never direct spending permission. Integrated interruption/conservation is green;
  native traversal remains unsigned.
- **Hosted arcology native acceptance.** One exact root now owns schema `TAFArcology`: all 27 local
  zones across `x/y=0..2`, `z=9..11`, 27 purpose programmes, reciprocal district thresholds,
  matched stairs in every coordinate column, one civic surface exit, and designated paid
  terrace/ward anchors. Nine route-safe archetypes across foamcrete cultivation, inherited-marble
  civic, and rusted service strata now replace the former uniform floors; paid fixtures share that
  programme authority. Paid-floor output now uses an exact active full-floor designation and one
  canonical dated final-suspension observation: ward roof/luxury comes from current providers;
  terrace food comes only from exact growbed rows and requires current exterior fresh water.
  Receipt/root/zone/anchor mismatch and malformed/duplicate observations fail closed without remote
  loading. Focused hosted contracts and the frozen staged compile are green. TESTING 136j–136j.5
  still owns native traversal, save/cold-load, labour/water, provider loss, and human inspection of every zone.
  Yielding-lot relocation is separate.
- **Heart ring-call relocation native acceptance.** Runtime and automated contracts are implemented:
  only exact finished settlement-raised plots marked yielding can answer a Heart `NoGroundToGrow`
  offer; the founder sees and re-proves every source→destination before mutation; cost is labour and
  world-time only; one visible receiving frame advances at a time; original plot objects move under
  a bounded CAS receipt while their LotId, frozen architecture, contents, residents/home binding,
  staffing, held/work state, wear, and network declarations remain on those same objects. Exact-ID
  ambiguity, new obstruction, callback interruption, ownership loss, malformed/future receipts, and
  cold recovery fail closed or roll back. Focused relocation runs are green (22 portable, 35 native
  rules/source cases) and staged baseline/compatibility compilation is clean. TESTING
  136j.6–136j.10 remains the live-Qud behavior/save/appearance signature.
- **Assenting-moot native acceptance.** Runtime implementation is complete: six durable named
  assents, six durable exemptions, explicit add/remove UI, current-body strength, exact native
  `AmbientStabilization` ownership, per-body ambient-effect veto, and damage/destruction,
  absence/death/departure, strike, secession, thaw/activation/load recovery are wired. Ten focused
  cases pass in both runners and staged baseline/compatibility compilation is clean. TESTING
  136t-136w remain unsigned live-Qud behavior/save/appearance evidence.
- **Civic-experience promotion.** All O0–O11, D1–D12, and C1–C2 surviving bounded purposes now have
  code owners or stronger supersessions; C3 has structural simulation and telemetry but requires
  measured native evidence. TESTING Pass 40 owns integrated UI/save/accessibility/ablation proof.
- **Polity native acceptance.** Complete bounded semantic/physical adapters exist at code scope,
  including shared attention, direct-record fallback, exact recursive custody, and visible-death
  gates. TESTING Pass 39 owns native route, body, death, three-city, performance, compatibility,
  exile/return/refound, and anti-farm proof.
## Deliberate v1 boundaries

- Ground claims still preserve every existing object's ownership, inventory, and allegiance.
  Explicit realm property is implemented in the working tree: the Charter can designate or release
  one nearby founder-owned takeable object through `r_KingdomProperty`; its exact reversible receipt
  writes only native `Physics.Owner`, so vanilla warning/help behavior supplies theft consequences.
  It does not claim-stamp ground or nearby objects. Foreign ownership and receipt divergence fail
  closed. Six focused source/pure cases pass in both runners; native save/theft/release proof is
  open.
- Authored post/home day shape and attended cosmetic station activities are implemented: posted
  residents tend, sort, craft, maintain, build, watch, or attend a shrine without granting stock,
  progress, RNG effects, skill, experience, or standing. Eleven focused portable cases and staged
  compilation pass; native lived-day observation remains open.
- Within-realm physical food routing is implemented. Trade-owned polity consignments and loaded
  food/water hospitality now have exact intent/debit/custody/conclusion authorities at code scope;
  integrated automation is green and native conservation/UI proof remains open.
- Founder memory is implemented through the owned shrine, Chronicle, corpse-reading, and one
  durable TAF-local read-only projection reconstructed from its save receipt. Schema 2 never
  inserts an entity, event, or note into Qud's shared `sultanHistory`/journal pools. Exact schema-1
  objects are removed only after list/index/back-reference/payload proof; ambiguous legacy state is
  left inert and quarantined. The retired option is unnecessary because the existing Charter
  Chronicle already owns the visible telling. Focused pure/source/native-consumer cases cover
  migration, save reconstruction, no insertion, and fail-closed cleanup; live legacy-save cleanup
  remains a native acceptance item.
- The realm faction dish remains the realm authority and competing arbitrary `TeachesDish`
  overrides are `REJECTED`. One exact city-local named cook, separately authored alternate recipe,
  paid teaching, release, and recovery are implemented; native recipe/identity/save proof remains
  open.

## v1 polity/world-presence boundary

The author reopened every positive polity/world-presence direction for v1. Current source now owns
the complete bounded code shape: one optional latest legacy polity under fresh ids; typed immutable
profile revisions; exact faction/body/gear projection; causal named promotion; seven finite cohort
purposes; deterministic current/rival and three-city traffic; Trade-owned correspondence custody;
loaded hospitality; caused grievance/terms/truce/intervention; witnessed conclusion/death/aftermath;
consented escrow; shared W0 body/audience capacity; direct-record fallback/aggregates/fairness; and
exact exile/return/refound/retirement cleanup. Semantic travel never walks an unloaded actor, and
physical bodies exist only on eligible loaded ground under recursive custody and removal witnesses.

This is code-scope closure, not native proof. Pass 39 still owns save, accessibility, performance,
compatibility, recognition, anti-farm, three-city, death-order, and every-cut evidence. Exact
old-actor continuation, automatic war from creed/opposition alone, actors simulated on unloaded
tiles, persistent strategic armies, mass background war, and unwitnessed conquest/casualties remain
`REJECTED`. Canonical disposition/evidence owners are in
[VISION.md](../VISION.md#canonical-v1-polity-scope-matrix).

## Required before a v1.0 test-candidate claim

- Full pure/source suite green after the final code and documentation set; rerun after every later
  source or source-contract change.
- Exact release, ABI, XML/reference, architecture, package, and Workshop-package test gates green.
- Retain the clean `19fb8ee` deployment/native smoke as bounded evidence only. Repeat native
  compile/load and `Player.log` review against the exact final structural commit before claiming its
  runtime behavior.
- Numbered [TESTING.md](../TESTING.md) protocol completed for all changed high-risk lanes,
  including cold save/load cuts, dense city, multi-zone carriers, citizenship overlays, master and
  module resume, raids, succession, happenings, inheritance, and external API fixture.
- Native architecture galleries reviewed at tile and text scale for every loaded commissionable
  key and reachable state; controller/keyboard and color-independent readability signed.
- Representative compatibility matrix and private Steam subscribed-install receipt retained.
- Every reopened positive limitation is implemented or named as an active implementation/evidence
  gate. Only controlling hard rejects and actions requiring external account authority stay outside
  executable v1 work; no historical audit may prove a current gap closed.
- Every current `SHIP` row in the v1 polity scope matrix remains truthful at its stated
  implementation/evidence boundary. Accepted-but-open design must not be presented as current
  runtime; semantic ledger proof does not sign any physical adapter.
- Addendum 9 structural release gate is closed for the current digest: every staged C# file is
  strictly under 300 physical lines and `docs/STRUCTURE_REVIEW.json` binds the exact-inventory
  responsibility/protocol review to digest
  `a3a9c8dd8ea36962475266e7005ccc6fcdd352b3bfd3d9c4675beb47b51be2b9`, honestly signed by Codex
  with independent AI reviews under the author's Addendum 9 ruling of 2026-09-02. Complete
  canonical comparison enumerates every source; unchanged files inherit retained review and
  the five changed sources received fresh scoped review. Any staged source change reopens it.
  This structural verdict is not complete Beta or v1.0 functional acceptance.

Detailed current ledgers live in `_notes/BRIEF-IMPLEMENTATION-AUDIT.md` and
`_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`. Release mechanics live in
[RELEASING.md](RELEASING.md); structural gate semantics live in [STRUCTURE.md](STRUCTURE.md).
