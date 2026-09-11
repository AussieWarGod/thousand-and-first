# Session handoff — current v1.0 test-candidate work

## Retained combined heart candidate before surveyed-ingress draft — 2026-09-11

Current draft census/evidence is in docs/STATUS.md; the figures below describe its predecessor.

3084 staged sources; 437,643 physical lines; 1438 direct-XRL files; zero cap failures;
3114 cold-install files. Inventory `31dfb1859670cbcef14e6a98288db376747dd8f3c1515c0703efd8f791bbb3cd`.
#141/#144/#139/#137 combined; compile, native and semantic review owed. Earlier rows are
retained checkpoints, not acceptance of this candidate. Public0.3.3 is finalized.

## Retained camp integration (#107 / #132 / #137)

Integrated through dev `bcca3e5`; compile/audit and main14341/portable5491 pass, zero managed
skips; native pending. Census3079/437201/1434,
zero cap failures; inventory `304123d5ee1b31fa0b1369d5ad13c52888111f5c7563245f4fe4096c8ca38c70`.
Earlier evidence below remains bound to its named checkpoints.

## Retained dev hotfix backmerge — 2026-09-11

3080 staged sources; 437,399 physical lines; 1435 direct-XRL files; zero at or above 300 lines;
3111 cold-install files. Inventory `6ebc095bd595e636c48079ba5c41c0cb3bb92f3af7741d99b05eeb8b81acc78d`.
Combined-tree gates pending; isolated main hotfix native evidence does not sign dev features.
The retained 0.3.3 semantic review binds main only, not this combined inventory.
See docs/STATUS.md for scoped release evidence. Public Workshop remains 0.3.2.

## Retained roadless seal candidate (#131)

3082 staged sources; 437,405 physical lines; 1437 direct-XRL; zero cap failures; 3113
cold-install files. Inventory `40af1139cc9336140aca90a5db81c80aefb060a45635d57766b560c34c1753e4`.
Four compile modes, audit, main 14,318/portable 5,470 tests pass (managed zero skips).
Roadless-camp native passes at `0cfd8e7`; see docs/STATUS.md for exact scope and retained timeout.

## Retained unreleased Kingdom Quickstart founding cohort

3078 staged sources; 437,114 physical lines; 1434 direct-XRL; zero cap failures; 3109 generated
cold-stage files. Exact inventory `e1ddecb76e357905d90bbc40ad8414b68efc38f2b532d19ec61d2df4a5053ce9`.
Kingdom Quickstart founds a world with four founding citizens on the approach at turn 1, raised in
one custody scope (zero-or-four), published by identity before any irreversible write, then enrolled
forward-only and idempotently under a new `Founding` reason. The receipt gains a durable founders
disposition and four ids on a sixteen-field wire; the old eleven-field form is byte-preserved and
terminal, so no world made before this change, and no world made with the new option off, can gain
founders. Each founder is counted into the shared per-profile origin tally by ADDING one under its
own durable identity-bound obligation, so five prior citizens plus four founders is nine; mixed
state after an interruption between the label and the increment is refused in the open, once, per
obligation, as a stated safety policy rather than a recovery claim.
Four compile modes, both engine-free suites (14,147 / 5,301, zero skips) and the Tools
suite (627) are green. The six-profile boot matrix at seed `#43101` is OWED before merge, along with
the `found-first-city`, `first-guest-native-check` and `guide-topics-native-check` personas.

## Retained unreleased stockpile deposit custody

3068 staged sources; 435,538 physical lines; 1429 direct-XRL; zero cap failures; 3099 generated
cold-stage files. Exact inventory `3cfe76c38704930c03d2923e400d05155cbcbf96b9ad8b31ad90304cc8fea6c0`.
Twenty-two custody findings from three review passes on PR #84 are fixed, over the two physical defects first found in the merged stockpile-capacity deposit. A bundle an
insertion callback moved elsewhere was preserved and counted as zero, so `MaterialStock.Put` made
the units a second time in the next store or on the ground; and a bundle a stack-count handler had
already carried off was obliterated whenever the stamp proof failed. The deposit law now lives
engine-free in `Core/KingdomDepositEngine.cs` behind `Core/IKingdomDepositHost.cs`: unproved
custody stops the whole `Put`, only what a store provably gained is credited, a body is withdrawn
only when proved held by nobody AND provably destroyed, custody is proved before every mutation, a vanished bundle is credited only by the destination's own gain IN THAT MATERIAL, the landing proof requires the destination to still be dedicated stock, the overflow path runs the same law, a throwing handler keeps what was proved, and every caller that writes a receipt reads the custody first. Reading is treated as a callback and every final proof reads RAW off Stacker.StackCount (Count repairs and dispatches; room and hold censuses walk and ask), every batch proves its count including a batch of one, the gain readers count only members whose own custody names the destination, the uncertainty saying cannot cost the delivery its proved units, and a clearance stake whose yield went unproved is durably held rather than merely announced. The founder is told once. Counting stays whole, intake is still the
only refusal (ruling 5), the catch-up envelope is untouched and old saves read as before. It sits over `dev` at `862f14d` (native observers, Workshop wording, Workshop CI, the Fetch
carry-completion fix and the cross-version persona REQUEST wording) and over the Kingdom Quickstart shelter ingress retained below. Structure, doc freshness and the Tools
suite (627) pass; engine-free suites pass 14,061 main / 5,215 Portable, zero skips, and Roslyn
9.0.306 on Linux compiles the staged baseline (3064) and compatibility (3068) sets clean. The two
new deposit regressions were confirmed to fail against the pre-fix behaviour. NOT run for these
bytes: the two dev-harness modes, the installed-ABI source step, the Windows gate, the native
Quickstart boot matrix, ordinary play, graceful Quit and Steam delivery. Public 0.3.1 unchanged.
Never direct-push main/tag, bypass, or self-approve.

## Retained unreleased Kingdom Quickstart shelter ingress

3062 staged sources; 434,436 physical lines; 1425 direct-XRL; zero cap failures; 3093 generated
cold-stage files. Exact inventory `ff13330463a990edbef95c2ae35e0e552691f2f8e0f86a525dc873bd61f7c202`.
The camp now bares each shelter lot's authored ingress route as well as the lot itself: the stake's
public-ingress preflight walks that route and refuses an unbared cell, which is why only the dunes
founded. Four cells are added — (23,8) and (23,7) north of lot A, (24,17) and (24,18) south of
lot B — and nothing else changes. Heart-ingress endpoints, lot rectangles, receipt wire and refusal
message are untouched. It sits over the render-only city sight, the stockpile unit capacity and the
first-basin water store retained below. On the merged tree structure, doc freshness and the Tools
suite (627) pass; engine-free suites pass 13,987 main / 5,199 Portable, zero skips, and
Roslyn 9.0.306 on Linux compiles the staged baseline (3058) and compatibility (3062) sets clean.
On the delta's own pre-merge bytes all four `Tools/gate.sh` modes plus the
installed-Hearthpyre source/ABI step compiled clean (3050/3054/3204/3208), the engine-free suites
passed13,905 main/5,193 Portable,zero skips, and the six-profile native
boot matrix at seed `#43101` passed with two `plot staked: tentrow` rows per boot, as did
`quickstart-save marsh yes` and its cold load. NOT run for the merged tree: the two dev-harness
modes, the installed-ABI source step, the Windows gate, ordinary play, graceful
Quit and Steam delivery. Public0.3.1 unchanged. Never direct-push main/tag, bypass, or self-approve.

## Retained unreleased city sight over the stockpile unit capacity

3061 staged sources; 434,296 physical lines; 1425 direct-XRL; zero cap failures; 3092 generated
cold-stage files. Exact inventory `7147169b7ccb8d2142d9791bd5faec8405eb305e33bca7a9b9feb9c3948c5a1e`.
City sight is taken at the engine's own `Zone.Render` call — a flag armed by a prefix on
`XRLCore.RenderBaseToBuffer` and spent by a prefix on `Zone.Render(ScreenBuffer)`
(`Growth/KingdomCitySightRenderSeam.cs`) — rather than from the part's own second-pass turn, so
the honest snapshot is read behind `Blackout`'s light removal, which `Zone.AddVisibility` gates
on. The seat before it, a Harmony postfix on the render dispatch's static entry, crashed the game
in unattended native runs: the re-hosted engine method threw `NullReferenceException` out of
itself on the first drawn frame in three of four launches (`Send_Patch1`, native dump naming the
walk over its own second-pass handler list). Evidence:
`Tools/PortableOutput/player-claimed-light-native-check*.log` and
`player-first-guest-native-check*.log`. That seat is now a forbidden string in the source
contract. Merging its end-of-turn restore backstop with the basin-capacity zone-activation guard
put `Core/KingdomSystem.z20.Events.cs` at 305 physical lines, over the strict cap; the merge
reflowed those two comment blocks wider, keeping every word and engine citation and moving no code
or statement order, and the shard is back at 299.
Structure, doc freshness, architecture and the Tools suite pass; engine-free suites pass 13,986
main / 5,199 Portable, zero skips on Linux .NET9.0.306, and Roslyn 9.0.306 on Linux compiles the
staged baseline (3057) and compatibility (3061) sets clean. NOT run:
the two dev-harness modes, the installed-ABI source step, the Windows gate, the developer boot
matrix and any native run.
The 1,700-tick raising figure is a reading of `KingdomPlotRules.RaiseTicks`, not of a plot clock.
Public0.3.1 unchanged. Never direct-push main/tag, bypass, or self-approve.

## Retained unreleased stockpile unit capacity over the first-basin water store

3059 staged sources; 433,954 physical lines; 1423 direct-XRL; zero cap failures; 3090 generated
cold-stage files. Exact inventory `5db8f7381ade172c6b0b34925a111f4d4c28f32da77cf0be266914aa53e77674`.
A dedicated stockpile now holds a stated number of material units (32 by default, off the new
`r_KingdomStockpileCapacity` blueprint tag when one is declared); counting stays whole and only
intake refuses, so no standing save reads lower than it did. The delivery remembers nothing across
an engine callback: creating the bundle, stamping its count (`Stacker.StackCount`, which sends
`StackCountChangedEvent`) and inserting it each run other people's handlers, so the destination and
its room are proved after the creation and again after the stamp, and the bundle is proved standing
in that exact store with its stamped count before a unit is counted. Three added production
sources, three modified, one regenerated removal-coverage roster; no receipt, wire, option, grant
or verifier change. Roslyn 9.0.306 on Linux compiles the staged baseline (3055) and compatibility
(3059) sets clean, warnings as errors, and both engine-free suites run green there (13,980 main /
5,193 Portable, zero skips). No native run, no dev-harness mode and no
exact-inventory human semantic review bind this digest.
## Retained unreleased first-basin water store over the Quickstart tent rows

3056 staged sources;433,308 physical lines;1421 direct-XRL;zero cap failures;3087 generated
cold-stage files. Exact inventory `5160ed08e19734f315ebe8c7fe2ab4e5e7e1bb6bc632ae97a0c511d1f40cd325`.
The founding heart's first basin becomes the settlement's first water store: dedicated in code at
the relic slot, capacity 16/48/160/512/1024 by rung, raised only and never lowered, skipped with
one ledger line while an open water debit is bound to that vessel, an unsettled arrival water leg
draws from it, or a routed-input lease holds it, and reconciled once on load and on zone activation
for worlds built before it. The catch-up resolves the basin only through the anchored-component
lookup, which needs an a3|/a4| managed layout receipt on the heart's owner: a heart whose owner
carries an a2 or absent snapshot is NOT reconciled until its next rung restamps the layout. The
reconciliation is asked only AFTER the seat exchange and only for ground the seated settlement
claims, and the reconciler refuses unclaimed ground itself, so a second, foreign, seceded or
exiled city's heart can never be dedicated into the wrong ledger or measured against the wrong
growth book. A committed water receipt keeps its per-vessel hold while its caller's declared
compensation window is open, and every caller that can refund AFTER its own callbacks now opens
that window before its commit and closes it in an enclosing finally: construction funding across
the material commit, sowing across the laid rows and the spent seed, annexe enrolment across the
roll and the standing batch, and the lab's commission and its two removal lanes. The window never
outlives the method that took it, so a finished rung is still free to widen the basin its funding
drained. No saved field, wire format, option or verifier predicate changes. Roslyn 9.0.306 on Linux
compiles the staged and dev-harness baseline and compatibility sets clean. No native run,
installed-ABI source step, Windows gate and no exact-inventory human semantic review bind this
digest.

## Retained unreleased Kingdom Quickstart tent rows

3053 staged sources;432,593 physical lines;1418 direct-XRL;zero cap failures;3084 generated
cold-stage files. Exact inventory `d0f0e0cc12d931557082d09ff97316fb3d8125ff8bd1f0aa6e1c60baff94cfb0`.
Quickstart stakes two `tentrow` lots at founding, west of the supply column at (21,9)-(26,12) and
(21,13)-(26,16); six beds between them, receiptless, free, and never debited. No receipt phase,
option, grant or verifier change. The receipt wire is versioned: a receipt this version mints
carries the shelter obligation under tag `q2`, the shipped `q1` shape is still written and read
byte for byte, and only a `q2` receipt owes a stake — so a pre-existing `q1` save resumed at any
phase, Reserved included, keeps old behaviour and never stakes on ground the old narrower mask
never bared. Codec and source-contract cases prove that; no native resumed save does.

## Retained unreleased camp-guide topic tree

3053 staged sources;432,178 physical lines;1417 direct-XRL;zero cap failures;3083 generated
cold-stage files. Exact inventory `dcab3931d57df58aeaf3f0dee894acdec54d261a4e5f85d94cb369d8a1c73e96`.
One engine-free words file (`Core/KingdomQuickstartGuideRules.cs`) and a five-topic root-option
loop on the optional Quickstart guide; no receipt, wire, option, grant or verifier change.
Engine-free suites pass13731 main/5109 Portable,zero skips on Linux .NET9.0.306, and Roslyn
9.0.306 on Linux compiles the staged baseline (3048) and compatibility (3052) sets clean. No
native run, no dev-harness mode and no exact-inventory human semantic review bind this digest;
the master-growth digest below keeps its own review. Existing saves keep the one-node guide.

## Retained unreleased claimed-ground light

3051 staged sources;432,024 physical lines;1417 direct-XRL;zero cap failures;3082 generated
cold-stage files. Exact inventory `fca337fa0b3642f0e4e485df3a015cbbd66b2a9c5d94cd8fd5d204c3e3f86f54`.
A mod-owned zone part lights the claimed zone the founder stands in, gated on the new
`r_TAF_OptionClaimedGroundLight` (default Yes) and revoked from the same activation guard.
Engine-free suites pass 13,720 main and 5,098 Portable cases, zero skips; doc-freshness,
structure, architecture and the Tools suites pass. Roslyn 9.0.306 on Linux compiles the staged
baseline and compatibility sets clean. No dev-harness mode, no native run and no Windows gate ran
for this delta.

## Retained unreleased master-growth resume correction

3049 staged sources;431,893 physical lines;1415 direct-XRL;zero cap failures;3080 generated
cold-stage files. Exact inventory `a3a9c8dd8ea36962475266e7005ccc6fcdd352b3bfd3d9c4675beb47b51be2b9`.
Complete five-source review and canonical comparison bind strict structure. Native11624
passes actual master pause/resume and later raid recovery; all3181 C# bytes match its
isolated profile. Focused38cases pass; full1814 passes13715 main/5093 Portable,zero skips;
four-mode53744 passes ordinary3045/3049,developer3177/3181 plus installed ABI.
Repository71120 passes501 tooling tests and all repository audits. Hosted checks remain.
Evidence `/mnt/c/taf-master-growth-native.sP00c6/README.md`. Public0.3.1 unchanged.
Main PR6 squash-merged to main as `be3f13a` at 2026-09-07T22:34:54Z; hosted run34167157170
passed repository-audit and the full pure/portable lanes on ubuntu-latest and windows-latest.
The pull-request review requirement was removed entirely (the sole collaborator can never
approve their own PR); PR-based integration is now policy, enforced by the required status
checks, linear history and `enforce_admins`, which is now ON. Required checks, linear history,
no force-push, no deletion and conversation resolution remain; merges stay squash-only with
delete-branch-on-merge, and no `dev` branch exists yet. Tag `v0.3.1`
still targets `a46b5ad` and main is one squash commit ahead. Never direct-push main/tag or
bypass required checks.

## Retained unreleased recovery correction

3047 staged sources;431,611 physical lines;1415 direct-XRL;zero cap failures;3078 generated
cold-stage files. Exact inventory `6dbd94092f57eeb9b79f5ff169105fa5a7ab2cc6702480d99ba98c92be17bff9`.
Complete canonical comparison retains3041 unchanged, five modified, one new source. Exact
six-file semantic review and strict structure pass. Four-mode compile51408 passes; native51235
passes real late-veto and returned-survivor cases with exact owned stops. Main suite27430
passes13661 main/5039 Portable cases, zero skips; repository71541 passes501 tests plus
inventory, documentation, XML, architecture and registration. Combined PR10/hosted gates remain.
Public0.3.1 unchanged. See docs/STATUS.md and `/mnt/c/taf-raid-recovery-fixed.FaVvXv`.

## Current publisher handoff — public 0.3.1 published and finalized

Public3794797472/version0.3.1/attempt0001: submit55498 TERMINAL0 at13:18 UTC reports
`SubmittedUnverified`, `metadataMatches=true`, `contentUnchanged=true`; finalize55266 TERMINAL0
reports `SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true`.
Evidence `/mnt/c/taf-031-public-release.cfu8DL`; exact bindings in
[STATUS](../docs/STATUS.md#public-031--published-and-finalized).
Annotated `v0.3.1` target `a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e`,
tag object `ed91d97b6d5d1b515933d144adb01f89303c5496`. Strict Alpha package68272 TERMINAL0:
3077 files/exact private binding; native copy54621 TERMINAL0. Signed-out HTML exposes title,
new hook and six tags; no pixel capture. One client only, `freshTransferVerified=false`, `releaseReady=false`.
Current Alpha publication complete, broad Beta unfinished. Main and annotated tag were pushed;
fresh remote refs match the exact release commit and tag object above.
GitHub unexpectedly accepted that push while reporting bypassed PR/three-required-check rules.
That push changed no protection settings; main's protection was deliberately updated later, as
recorded above. Do not repeat direct main pushes or bypass checks; the
post-publication documentation closeout uses a separate PR. Preserve the accepted release/tag.
Prior records and decisions follow.

## Corrected private publication and retained Alpha verification decision

Corrected private item3796495680 attempt `0002`: submit63022 TERMINAL0
`SubmittedUnverified`, `metadataMatches=true`, `contentUnchanged=true`; finalize20925 TERMINAL0
`SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true`.
Evidence: `/mnt/c/taf-031-corrected-release.LpixRX/{README.md,submit/upload.stdout,finalize/upload.stdout}`.
Plan SHA `0738dfdd55f06c91a3c39bc209b4f6db9cad9c674645ef501e3d0fd31fbab34d`;
receipt SHA `b12b6d31c7ea14ace123765199903114b5151f0d9405c2b6eef795fac8697359`;
inventory SHA `7274d19086813b076bf499fe5f391facd7174587b909bf0539e8b91419eb9e5c`;
finalization SHA `913a47b7f3edf292a320e847abb8faeec5e7c05ac23e4944e3c61706a1f43e17`.
Private receipt bound at `47a055254f09c1ac72733a3198101a66d47babf6`. One client only;
`freshTransferVerified=false`, `releaseReady=false`. All old0001 evidence remains immutable.
Public3794797472 is now0.3.1 as recorded above; the old broken private status below is historical
and superseded, not authority to reuse its package.

Canonical29200 TERMINAL0 completed all release-check stages on clean792270b; log SHA
`9d7eb43e64e61431a4336149034b369cc0f7bb70635508e43a612b4c7d07a3f9`.
Managed13625/5012 had zero skips. Full gate had three named environment bind-alias exclusions:
PACKAGE/COPY/BACKUP, foreign-owned `/tmp` in nested user namespaces; these are not PASS.
Root accepts those narrow gaps for this Alpha, not as a user waiver. Guards remain unchanged.

User explicitly waived manual startup/save/reload for this Alpha. Retained six genuine startup
boot/save/cold-load pairs and current stock native16 checks keep their separate source scopes;
no ordinary or graceful-Quit PASS. Root's one-release decision reused exact frozen-runtime
verification plus public-only delta checks and strict `--alpha` package
lineage/receipt/tag/structure binding. No second full `--alpha` release-check is claimed, and no
permanent/Beta gate changes. Root owns public metadata, packaging, commits and Steam operations.
[Current authority and limits](../docs/STATUS.md#corrected-private-031--installed-and-finalized).

## Retained publisher handoff — original private attempt finalized

Historical pre-correction-upload state; current record above supersedes its pending actions.

Active worktree `/tmp/taf-quickstart-founder.ZsbJsI`, stock checkpoint `b4c2d2d`; publisher
integration is now validated. Native94540 TERMINAL0 at
`/mnt/c/taf-publisher-integrated-native.ubIZAB`:47 launcher fixtures, all14 upload suites
(75 pure/source finalization,29 Windows history,17 package), both production helper builds,
installed-test build and14 installed-package cases pass.

Actual finalize43652 TERMINAL0 at `/mnt/c/taf-private-finalize.aix0Ji/upload.stdout` reports
`SubscribedInstallationVerified`, `reason=null`, `attemptFinalized=true` for original private
item3796495680 attempt0001. Finalization SHA:
`d06be0a4fbf6e1a29a98f03e18840bf13be4539c813b7e41a3afdb8cd8b783ab`;
installation SHA `871e28ce85f12b3388b64d72ff82dba8b891e1d9260a91f1a78b31aeea812446`;
inventory SHA `6bf2e248aa834cf52272f61e566f65a8463bddffac6d0e3599958ab729d45d58`.
Root freshly rechecked original attempt90011… and submission350bbb… unchanged; full hashes in
`docs/STATUS.md`. One client installation verified; `freshTransferVerified=false`, `releaseReady=false`.
No corrected-package upload occurred. The known-broken private0.3.1 remains unchanged; public0.3.0
unchanged. Finalization is operational completion, not gameplay acceptance or public promotion.

Next: close publisher checkpoint, run the clean private release gate, freeze the corrected
private package/plan/receipt, then use exact unseen-inventory admission for a separate submission.
Fresh subscribed bytes and ordinary acceptance remain required. Same-version private candidates
are supported; public versions must increase. Never clear/rewrite old attempts or bypass unknown
completion. Root remains sole native/Steam operator. [Current authority](../docs/STATUS.md).

## Current stock-hardening snapshot — 2026-09-07

Current worktree `/tmp/taf-quickstart-founder.ZsbJsI` contains 3046 staged sources,
431,441 physical lines,1414 direct-XRL imports and zero production cap failures. Its generated
package inventory contains3077 files, not proof of an installation or subscription. Production
digest: `9d9eb6416014c7257a26fa08178d8f738e44dd46a295ea3357f32b7739faf1b0`.
The stock reference-identity correction is integrated after startup checkpoint `1c1c2bc`;
bounded review covers its two production files, not a fresh whole-tree semantic review.
Compile73169 TERMINAL0 passes all four C#7.3 modes+ABI: ordinary3042/3046,
developer3156/3160 with114 Harness shards. Managed47358 TERMINAL0 passes13625 Taf/5012 Portable,
zero skips. Tools73664 TERMINAL0 passes501 tests and the exact structural `--release` gate passes.
Native49420 TERMINAL0 passes16 synthetic creator/custody groups, including duplicate-child
negatives inside the two creator groups, actual verifier refusal and healthy fixture restoration.
Exact-owned PID5068 stop, root idle check and canonical no-allowance Player.log check pass.
Prototype compile53305 passed before the later two-line native-fixture authority guard.
Isolated managed54638 ended with13624 pass/1 fail because ignored local
`_notes/CREED-KIND-EVIDENCE.md` was absent; Portable did not run. Neither result signs current
full validation. Current bounded stock correction is closed; component save/load is untested.
The six actual startup boot/save/cold-load pairs and twelve strict result
checks below remain evidence only for earlier `7e3fb752…` bytes. Ordinary play, graceful
in-game Quit, subscribed delivery of the correction and public release remain open.
Current validation: `/tmp/taf-quickstart-stock-validation.CM1dD9/README.md`.

## Retained startup handoff — `1c1c2bc` / `7e3fb752…`

Quickstart hotfix supersedes historical status below. User closed Qud; root now owns isolated
native tests only. Private0.3.1 unchanged build still contains reported failure; do not promote.
Worktree `/tmp/taf-quickstart-founder.ZsbJsI`; native execution baseline HEADdbad70c plus correction.
Current production digest7e3fb7521f445e16b0740329966e369fa51d91fd768237bdffb5291d3540c651,
3045 sources/431407 lines/1414 direct-XRL/zero cap failures. Exact-founder/staked-heart guards
remain. One camp now runs after full GetZone/beforeplacement, refreshes native reachability,
and prepares the two authored ingress endpoints40/41,16 missed by the original footprint.
Root+independent review clear; actual shipped architecture-route and pure-mask regressions added.

Retained6010 marshyes/no both passed actual boot/save/cold-load #43101, unchangedstate/no replay.
Samecheckpoint canyon failed Garbage28,10; actual Rusty biome selection was not witnessed.
Later e922 canyon jAvOVQ failed normal founding's physical ingress endpoint check after
world1/camp1/reachabilitytrue. Exactownedstop/idle passed; failedprofile/logs retained.
e922 compile99684 fourmodes+ABI and managed17349 13602/4989 passed; old greens not current.
Currentcompile21734 PASS fourmodes+ABI(stage3qcBKR/devxny5Tf), managed21635 PASS13603/4990,
zero skips; Tools69769 current501tests/104.836s PASS. All six current combinations atseed43101
passed actual boot/save/coldload milestones, exactheart/stock/IDs/clocks andno replay.
Sources vrVt20(canyonyes),aQWw2S(dunesyes),ETnPK2(canyonno),plhYgy(dunesno),PuTS15(marshyes),
n021IW(marshno); coldroots xQForF,qUJ2Qp,kU3dbE,rgg8XJ,FisZj9,bUZ7Pr respectively.
All6savecheckersPASS; loadcheckers88002/33964/31822/43934/15697/17465 TERMINAL0PASS by11:25UTC.
All6ownedloadstop+idlePASS; noQudrunning after54520 TERMINAL0 PID24732stop.
Never patch/reuse any spent or superseded sealed profile. Root proof and all handles:
`/tmp/taf-quickstart-native.sYl4Dz/README.md`; docs/STATUS.md owns acceptance scope.

Retained separate finding: duplicate stock-child references could counterfeit initial totals;
top-level absent heart/grants were refused. Not observed canyon cause or fixed by ingressdelta;
the later bounded stock correction and its current proof are closed above.
QSB2–5, ordinary play, graceful in-game Quit,
subscriber delivery and release gates remain open. Exact-owned Kill is not graceful Quit.
No new upload, Steam mutation, release claim or source export. Root sole Windows/native operator.
Retained stock prototype: /tmp/taf-quickstart-stock-alias.O5C189/CORRECTION.patch,22pure+11source
testsPASS; compile53305 TERMINAL0 precedes the final native-fixture guard. Managed54638
TERMINAL1 has13624 pass/1 missing ignored `_notes/CREED-KIND-EVIDENCE.md` failure in the clone;
Portable did not run. Current active-worktree validation above supersedes this failed fixture run
without waiving it. Beta objective remains unfinished.
Retained pre-integration publisher snapshot: /tmp/taf-release-tooling.h5W29F was being corrected
for privateequalversion+unseen canonicalinventory; noactiveintegration/finalization had occurred.
Old0001+submissionfreshreadback hashesmatchedpriornotes; notleasedSDKauthority. The current
publisher handoff above supersedes that status. ForegroundUI permission askedasynchronously;
noanswer/inputyet. Externalboundedinputhelper /tmp/taf-ordinary-foreground-input.dnwmse authored.

## Historical pre-upload handoff

Current isolated draft census — 2026-09-06: 3045 staged sources; 3076 generated package-inventory
files. D5 release intent/acknowledgement and sr1 migration now implemented; production driver,
codec and custody-source tests added. Strict four-mode compile and current suite evidence lives in
`docs/STATUS.md`. Announcements/named capacity/death journal now pass13494 Taf/4881 Portable and438 Tools;
strict4+ABI and exact hashes pass in k0DDMc. Death-journal recovery integrated; native acceptance open.
Archive103 real serializer cases remain retained for OZvP3Z inputs. Clock-guard
native save/cold-load pair tYyMoG is retained for its earlier bytes; ordinary and
historical-save acceptance remain open. Retained earlier full
founding-heart lifecycle passes 15/15 scoped developer cases in v6Kp54, including six native
retirement conflicts with exact owned restoration. Retained earlier completed-heart cold load
passes in47UPwt, with exact preactivation authority and later production recovery. Throwing-reader,
direct-pooling and exhaustive fallback-mutation coverage remain open. The draft is not integrated, semantically signed off,
installed or released. The new developer witness requires a completed heart; older staked-heart
save/load receipts do not sign it. Realm archive is now v9; fresh Destroy proof is unchanged.
The following beta addenda retain their earlier source/evidence scope, not today's inventory.

Beta checkpoint 2026-09-05: 2951 staged sources; 2982 package files. Quickstart fresh-custody
rollback, raid-zone guards, canonical polity proof validation, and receipt-owned persona lifecycle.
Raid outbox throws now quarantine before diagnostics; focused actual-lifecycle tests pass 34/34
in both managed projects, including wire persistence. Exact-source review current; STATUS.md owns
final suite/compile/native evidence. Native outbox persona now passes 6/6 actual sink cases with
synthetic interruptions; messages/UI effects retained, ordinary play and save/load unsigned.
C#7.3 lambda shadowing fixed mechanically; generator excludes non-shipping Harness sources.
Claude launch-order foundation7e296d20 integrated: all projections/leases published before
factory calls, then existing ResumeOpen. Five new rule/source tests; full11231/2794,zero skips.
Native launch callbacks remain unsigned. Same-blueprint replacement and interrupted-placement
custody remain OPEN; rejected custody revisions stay separate. Main stays v0.3.0.
Subsidence summary-only completion fix passes13/13 focused cases in both projects; full
managed11231/portable2794, zero skips; twelve additional native-fixture source contracts. Existing accounting and reached-rung invocation precede
summary output. Fractional-step debt, pending departure accounting, inner-rung interruption,
and ordinary/save-load subsidence evidence remain open; no parallel clock was introduced.
New disposable native fixture uses real founding, 50 actual enrolled/bound residents, physical
storage, five actual departures, exact summary-event fault and same-tick retry. Stage/elapsed
checkpoint are seeded; no breakpoint or world-turn claim. Native3/3 and fullrunnerPASS05:07:58UTC
in freshkvjIzc; exactfourdiagnostics, ninejournalrows, ownedcapture, PID36172stop and idlePASS.
Evidence /tmp/taf-native-subsidence.a9XJXf; STATUS.md owns final run boundaries.
Workshop uplift now in local canonical description/manifest/tags/serializer; independent factual
and code reviews pass. Five new metadata regressions cover copy, caveats, exact tags, private/public
identity preservation and absent-Workshop bootstrap. Isolated package harness passes with one
explicit nested-namespace bind-alias environment skip; not zero-skip release acceptance.
No new version, release tag, package receipt, Steam upload, or public-page change. Alpha update
still needs separate private stagingID/schema and fresh subscribed/save-reload evidence.
Tools338 passes exact literal/count diagnostics, actual-shell lifecycle and new Workshop regressions; raw logs
are retained. Earlier nativeprofiles7f9diy/oA5XdS eachpassed3checks but overall log expectations
remainRED. Finalrun uses four predeclared exact observed dev-title/DLC lines; no broadgate regex
is inherited in exactmode. Earlier reports were not backdated green.

Next accounting work is a durable settlement-local step tied to the exact global resident-departure
operation. Preserve inherited LastSubsidenceTick on prospective legacy admission; never invent
historical fractional credit or reset elapsed time merely because the new sidecar is absent.
Credit and operation acknowledgement must publish together and survive both journal-clear paths.
Named CityBook storage still requires explicit old/current schema, archive, master-option and
retirement integration. Reached rungs need per-work wear before/after proof and frozen resident
brink recipients; a cursor or deterministic roll cannot make repeated AddWear idempotent.
No part of that durable accounting protocol is implemented by this native-fixture checkpoint.

> **Supersession banner — 2026-09-01.** This is a chronological handoff snapshot; “current” below
> means the snapshot date, not the working tree. Current runtime holds one seat plus two non-seat
> cities (third founding succeeds, fourth refuses; manifests offer both non-seat destinations). The
> physical-food landing and hosted arcology are implemented at code/content scope. The arcology's
> full 27-zone native traversal, save/labour/water behavior, and human visual acceptance remain
> open; static integration is not that evidence. Use `../docs/STATUS.md`,
> `../docs/V1-UNDEFERRAL.md`, and `BRIEF-IMPLEMENTATION-AUDIT.md` for live status.

> **Status audit: 2026-08-27.** Use `../docs/STATUS.md` for exact current evidence,
> `BRIEF-IMPLEMENTATION-AUDIT.md` and
> `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` for open contract gates,
> `../TESTING.md` for native/human proof, and `../docs/RELEASING.md` for Steam. Historical
> candidate receipts below prove only their frozen bytes.

> **Current-truth note — 2026-09-01.** The frozen handoff state below is the 2026-08-27 snapshot,
> kept verbatim; the live frozen-source structural census is 2945 staged sources, 2857 cold-install
> files, and 410,378 physical lines with 0 at or above the 300-line cap, per `../docs/STATUS.md` and
> `../docs/STRUCTURE.md`. Current automated gates are green; native/human/Steam/preview evidence is open.

> **Market correction — 2026-09-01.** Historical tier-generated/restock wording below is
> superseded. Current civic service requires the accepted staffed provider plus exact office,
> opens empty, and recognizes only physical native TradeUI `_stock`. `ShopTier` is current
> standing/reach and may fall to zero. Completed/dormant personal traders survive civic
> loss/accession; only prepared handoff endpoints temporarily pause succession.

## Frozen handoff state (2026-08-27)

- Integrated feature/polish baseline is clean commit `19fb8ee`. Later structural/CI hardening
  changes are not signed by that receipt; preserve them and all later user changes rather than
  substituting an older historical candidate.
- `Tools/gate.sh`: 1575 staged sources compile clean under baseline and compatibility
  symbols. The exact inventory contains 1575 production C# files and 252,982 physical lines and has
  SHA-256 `736bb6fa198a3ed599ddc51302ffeccf7be0c01e7839cd9fbb7d11c9e79c1822`.
- Architecture: 136 buildings, 126 plotted building plans, 499 maps, 355 bindings, 403 tiers,
  516 variants, and 2,064 variant/pose goldens; generator/checker clean with zero issues plus three
  expected installed-base tolerant-recovery warnings for malformed vanilla Creatures/Furniture/Items XML.
  Lot realizations remain 337 maps, 242 bindings, and 277 tiers. Their 45,056 added cells now split
  into 33,772 yard, 8,933 visibly distinct path, 193 lawful sparse boundary, 1,549 unioned exact
  frontage-route, and 609 declared intentional-open cells; three hosted holds remain unchanged.
- Nine focused one-survey source-contract cases pass. The current integrated pure/source suite passes
  7,743 / 7,743 cases locally. Latest retained hosted checkpoint `d285129` has green repository-audit,
  Ubuntu source-suite, and Windows source-suite jobs for its exact bytes; later working-tree changes
  remain unsigned. The portable suite passes 173 / 173 with zero skipped; all 35 Python tool tests
  and all 19 art tests pass. Hosted Ubuntu exposed a legacy
  publication no-replace race; an exclusive legacy-folder publication lock plus deterministic
  contention test repair it. Public CI permits exactly three named
  installed-data skips when no
  Qud base is discoverable; any extra/missing skip or explicit invalid base fails. Release
  execution forbids skips and passes the exact licensed base explicitly.
- Current inventory gates report 1,599 cold-install files, 36 shipped IPart ABI classes and 3 ABI
  contracts. Art policy allowlists 0 local tiles and verifies 55 vanilla paths.
- Current native partial result remains the receipt from clean commit `19fb8ee`: clean deployment,
  fresh isolated profile, Smokehold founding,
  141 live faction standings, 17/17 self-tests, production gallery case 1/2,064, save/cold-load,
  repeat 17/17, and clean `Player.log`, all against Qud 1.0.5/core 2.0.211.51. Full human
  gallery/lived-city, numbered protocol, accessibility, compatibility, dense-performance, and
  private subscribed-install receipts remain open. Later structural revisions have no native
  receipt and need a compile/load/log rerun against their exact final commit.
- Structure checkpoint: 27 files exceed 300 physical lines, 0 are exactly 300, 3 exceed 1,000,
  0 exceed 2,000, and 0 exceed 5,000. Direct XRL imports total 689, with 25 at or over the line
  limit. Thirteen more oversized authorities were decomposed after hosted checkpoint `1c2d619`, bringing the current
  sequence to 144 additional families and 154 cumulative. Latest fan-in preserved exact logical
  source, persisted/public ABI, deterministic order, and source-reader coverage. The structural release gate remains red until
  every staged file is strictly under 300 and exact-inventory human semantic review exists in
  `../docs/STRUCTURE_REVIEW.json` with no exceptions.
- Current focus: checkpoint the current routed-input/purpose batch. Charter seniority,
  chosen-life/seat consequence, and activated groomed-designee configuration are implemented;
  their expanded native Pass 36 remains. Then rerun exact-revision native proof and complete every
  remaining semantic-review/human/compatibility/Steam gate.
- Reopened assenting-moot runtime is implemented in the working tree. Its activation key is a
  derived seated-city observation only; the exact building owns bounded named assent/exemption,
  native ambient stabilization, exact body veto, and lifecycle recovery. Focused portable and
  native filters pass 10/10 each; `Tools/gate.sh` compiles 1,818 staged sources clean under baseline
  and compatibility symbols. TESTING 136t-136w remain the unsigned live-Qud acceptance gate.
- **Superseded implementation claim:** this snapshot recorded hosted-arcology runtime as implemented.
  That status is now under active AMENDED 19+ review; production, XML/fabric, and tests are held.
  The snapshot's proposed fifth-heart design said the rung owns
  one realm/capital authority in two fixed current/exiled slots and an indestructible shell with persistent vanilla Interior atrium,
  ward, and terrace. Paid lots use exact composite debit/construction/root receipts, bounded prior-
  staffing labour, stable additive fixtures, and water-gated support; loss of capital leaves the
  intact shell dark. The generic read-only host seam receives only the exact loaded host zone/root;
  callback context is reproved before display; Great Archive uses it without queue/budget/timer
  mutation. Foreign shells stay inert, and no custom raster was added. TESTING
  136j–136j.5 remains unsigned native traversal/save/appearance evidence.

## Historical 0.2.0 candidate evidence

- Runtime candidate: `99133f6a1b24f3be652903e16576ddd7bb929230`.
- Release/provenance candidate: `ba3e49ed174c243917ecf5865e6c6dc1533d402d`.
- Exact historical gates: 6,873 native cases, 171 portable cases, 207 staged sources, 36 shipped
  IPart ABI classes, 11 art tests, 43 verified vanilla tile paths, and 222 cold-install files.
- Automated live Qud proof covered fresh two-city founding, three saves, two cold reloads, seat
  movement both ways, 109 carried fields, 17/17 checks, and clean logs. It predates material code
  and content changes and signs no current behavior or appearance.
- Frozen staged bytes were verified from clean commit `d3ec8d2`: 222 files at
  `/home/r/work/taf-package.otcUIn/TAF-0.2.0-private-bootstrap-d3ec8d2`, with sibling receipt
  SHA-256 `9584c89892410eaa4518d7411631932eb756cea3c68bb2a1763fdb6005187eac`.
  This handoff-only note is excluded from the staged Workshop inventory.
- The normal local Mods copy is an exact empty-diff deployment. Twenty-eight older scanned backup
  copies were moved intact under `CavesOfQud/TAF-ModBackups/`; none were deleted. The deploy itself
  made another automatic full backup named in `Tools/last-deploy-receipt.txt`.
- Historical art boundary used no bundled runtime raster sprites. Current law remains vanilla-first
  but permits original assets—including disclosed generative-assisted drafts only after pixel-level
  human revision—through the exact provenance, editable-source, wiring, fallback, package, rights,
  and independent native-review contract.
- Git publication is operational state and must be verified from the remote, not inferred from
  this note. The maintainer has explicitly authorized push; that authority does not authorize a tag, Steam
  upload, visibility change, or release claim. The live Mods copy is updated through the verified
  `Tools/stage.sh deploy` workflow, not by commit or push.

## Human-only gates

1. Finish the strict-under-300 decomposition and complete the exact-inventory human semantic
   responsibility/protocol review in `docs/STRUCTURE_REVIEW.json`.
2. Run the observational protocol in `TESTING.md`, especially Pass 36 succession/corpse/seal,
   plus full gallery, accessibility, dense-performance, and compatibility passes.
3. In Qud's signed-in Workshop UI, create the item privately, accept any agreement, and keep the
   returned `workshop.json`.
4. Freeze the private package, remove local duplicates, subscribe from Steam, verify installed
   bytes/logs, and author the truthful release evidence described in `docs/RELEASING.md`.
5. Only then canonicalize public metadata, tag the exact manifest version, publish, and
   subscription-verify public bytes. Never infer a manual or Steam pass from automation.

## Reopened polity/world-presence work

`VISION.md` owns the canonical public matrix; `V1-POLITY-SCOPE.md` is its expanded worksheet and
`docs/V1-UNDEFERRAL.md` is the live closure index. The author reopened every positive
direction on 2026-08-27. Successor/namesake polities, one bounded legacy rival/partner,
generalized diplomats/emissaries, visible polity traffic, route correspondence, and witnessed
polity clashes are active v1 implementation/evidence work under their owning adapters. Exact old
actors, automatic ideological war, persistent unloaded actors, mass background simulation, and
offscreen conquest/loss remain rejected. Balance/flavour
questions still worth real play data include Swarmer timing, deep-crop flavour, research gate
depth, annexe/removal prices, capital costs, and inherited-site discoverability.

## The working method (this is the "behaviour and attitude")

- **Wave protocol**: use bounded agents with disjoint ownership and spec-first briefs citing the
  ruling record; agents return wiring requests for files they do not own. Fan in through an
  independent review, then run `Tools/gate.sh`, native and portable suites, ABI, art, staging,
  smoke, and balance checks in proportion to the change. Stage only explicit paths—never
  `git add -A` in a shared worktree. Deploy only from a clean committed tree after a dry run.
- **Rulings are pinned IMMEDIATELY** as brief addenda, verbatim-faithful with the author's
  words quoted, then committed. Agents brief FROM the brief. Mid-flight rulings get
  SendMessage-forwarded to running agents.
- **Research-first for design questions**: praise-first comparables, negative space mined,
  four-part co-opt filter (Qud fiction / pillars / not-already-built / implementable),
  loud rejections, evidence cited file:line or URL. Comparables are quarries, not blueprints
  — Qud's register decides the shape. Engine claims verified in the decompile at
  `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`.
- **Derive-before-author**: vanilla parts/systems extended first (Addendum 11(c) preference
  order: inherit-extend / wrap / fill-in); numbers derive from what machinery visibly does.
- **The mesh condition** (Addendum 13): every feature is a rendering of model state through
  existing surfaces; new parallel machinery must earn it or comes back as a question.
- **Second readers** for load-bearing invariants (the W6 reconcile identity catch proves the
  pattern); adversarial briefs that attack, not admire.
- **STANDARDS 7b** (nothing stalls in silence), protection law (nothing player-placed
  destroyed without designation), Addendum 8 time doctrine, determinism via kernel draws,
  frozen models, engine-free `*Rules.cs` — non-negotiable in every brief.
- **House rules**: XML-named IParts live in `XRL.World.Parts`; object properties never
  appended serialized fields; seat fields on BOTH KingdomSystem and KingdomSettlement
  (named-field idiom); `Core/KingdomExileRules.cs` untouchable (reflection test); charter
  hotkeys full at 36 — new entries become chapters; save-compat waived pre-release
  (Addendum 9) but serialization bumps stay clean and named.
- **Questions the author must decide come back as questions, not code.** Scope discipline:
  agents flag material authority changes rather than invent them; every positive gap is tracked in
  the un-deferral ledger and hard external actions remain explicit gates.
- **Tone**: the author rules, fast and well, in plain words — quote them verbatim in pins.
  Caveman mode (user's global rules) governs reply style. Honest reporting always: failures,
  gaps, and unverified claims named, never papered.

## Environment notes

- Repository: `/home/r/work/thousand-and-first` under WSL. Licensed Qud install:
  `/mnt/f/SteamLibrary/steamapps/common/Caves of Qud`, marketing 1.0.5, core 2.0.211.51.
- Native suite runs through Windows PowerShell; portable checks run through the repository tools.
  Deploy backs up automatically. `_notes/` is ignored except its whitelist, so new tracked design
  records need an explicit whitelist entry.
- Engine claims are checked against
  `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/`. Revalidate against any newer
  licensed game build before changing the compatibility claim.
