# Structural release contract

## Current combined heart candidate census

3098 staged production C# files; 440,099 physical lines; zero at or above 300 lines.
1445 files with direct `XRL` imports; 3129 cold-install files. Exact inventory:
`73c87caced09ccdffc714a2475c170679e6e37a68b0fe42e92566d98c18d2907`.
Combines #141/#144/#139/#137 over the dev hotfix backmerge. No combined gate or native proof
is claimed yet. The inherited semantic review binds shipped main only and is stale here.
The current draft adds the surveyed-heart physical-ingress distinction; retained predecessor
compile/test and negative native results are recorded separately in `docs/STATUS.md`.

Addendum 9 of the binding building-catalogue brief requires services to stay strictly under 300
lines, own one responsibility, and communicate through protocols at boundaries before public
release. This repository does not weaken that rule by silently redefining a large file as a
non-service.

`Tools/check-structure.py` reads the exact staged production C# inventory from `Tools/stage.sh`.
Its physical-line census is a deliberately conservative proxy: every staged C# file at 300 lines
or more blocks release. Direct `XRL` imports are reported as a coupling signal, not treated as
proof of either good or bad dependency design.

```bash
# Development/CI census: reports debt, exits zero after a valid scan.
python3 Tools/check-structure.py --report

# Release gate: fails on line debt or absent/stale semantic review.
python3 Tools/check-structure.py --release
```

## Retained dev hotfix backmerge census

3080 staged production C# files; 437,405 physical lines; zero files at or above 300 lines.
1435 files with direct `XRL` imports; 3111 cold-install files. Exact inventory:
`c964d43923a5fa4284464d09029ef0b69a71bfe79b71185b3a25661c06149dca`. These figures include the
#141 heart envelope authority correction replayed onto this backmerge; against base `3a287e4` its
only production delta is `Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs`.
Combined-tree gates pending. The inherited semantic review binds the isolated main hotfix,
not this combined dev tree; no native or release acceptance is inferred.

## Retained isolated main stock-operation hotfix census

3069 staged production C# files; 435,617 physical lines; zero files at or above 300 lines.
1430 files with direct `XRL` imports; zero of these exceed the line cap. Exact inventory:
`249d3bb40ca34f57289770494e985a97e81cf42c2bae8f053fb6e14005b61d3d`.
One new local-operation scope helper and five modified production files over 0.3.2:
three scope callers, fresh starter-material identity allocation and the release version literal.
No saved fields or serialization formats change. The exact-inventory semantic review binds these main bytes;
see [0.3.3 delta review](STRUCTURE_REVIEW_0_3_3.md). This source review does not replace native
or release gates. Earlier censuses are retained checkpoints.

## Current #144 heart shared-XL transition census

3080 staged production C# files; 437,433 physical lines; zero files at or above 300 lines.
1435 files with direct `XRL` imports, none over the line cap. Exact inventory:
`24b4ab29aab9cd3c97bb30f5382df88937ee96eac565a6fb90ce52a0dd22c0a2`.
The production delta over the merged `dev` baseline `3a287e4` is the #144 heart shared-XL
transition fix and nothing else: two files, `Growth/KingdomPlotHeartRules.cs` for the pure
endpoint rule and `Growth/KingdomArchitectureStamper.Transitions.cs` for the call that replaces
the rung-number comparison. Everything else in this delta is DevTests and documentation.
Source-level only; no native rung 4 to 5 acceptance is claimed.

## Retained isolated draft census

The #129 dev-harness native checks merged over the roadless spatial capture correction — and so
also over the master semantic resume correction, the Kingdom Quickstart founding cohort, the
stockpile deposit custody fix, the Kingdom Quickstart shelter ingress, the render-only city sight,
the stockpile unit capacity, the first-basin water store and the Kingdom Quickstart tent rows —
report 3079 staged production C# files and 437,320
physical lines. The merge adds no production source over the roadless correction; the production
delta this branch carries is the unmerged #124 raw-delivery-overflow fix it is built on.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`f9a9469102be2f0ca119056e08c884a56014b31439e71695852026f63bd02d17`. The census reports
1434 files with direct `XRL` imports; 0 of those exceed the line limit.

The raw-delivery-overflow delta over the retained founding-cohort census below adds no production
source and removes none: it changes the deposit seam's two raw readings, both raw censuses, the
deposit law's three refusal branches and one rule, and the rest of its lines are comments and
documentation. The native-check delta on top of it adds no production source either — six
DEV-HARNESS shards, one persona and two DevTests suites — so the staged production census above is
byte-identical to the fix's own, and only the dev-harness inventory grows. All four
`Tools/gate.sh` modes compiled clean on these merged bytes through the canonical gate — staged
baseline (3075 sources), staged compatibility (3079), dev-harness baseline (3263) and dev-harness
compatibility (3267) — with the installed-Hearthpyre source and ABI step clean, and the generated
cold-install inventory contains 3110 files.

## Retained roadless spatial capture census

Heart rung settlement seam (#138): 3082 staged production C# files; 437,405 physical
lines; zero at or above 300. The census reports
1437 files with direct `XRL` imports; 0 of those exceed the line limit. Exact inventory SHA-256:
`40af1139cc9336140aca90a5db81c80aefb060a45635d57766b560c34c1753e4`.

## Retained camp integration checkpoint

Camp integration (#107 / #132 / #137) through dev `bcca3e5`: 3079 production files,
437,201 physical lines, 1434 XRL-importing files, zero at or above 300.
Inventory SHA-256: `304123d5ee1b31fa0b1369d5ad13c52888111f5c7563245f4fe4096c8ca38c70`.
Combined-tree four-mode compile and audit pass; main 14,341/portable 5,491 tests pass with zero
skips. Camp native proof and release semantic review remain pending.

## Retained roadless seal census

Roadless spatial capture correction (#131): 3079 staged production C# files; 437,201 physical
lines; 1434 direct `XRL` importing files; zero at or above 300. Exact inventory SHA-256:
`ab4cf22e665900bd82593ad4f9805861da4458e60b918c59b1d3a0bb88b74617`.
One small engine-free classifier is added. Existing internal capture/flush/reporting seams carry
a typed pending result without serialized state or format changes. Four compile modes, audit,
main 14,296/portable 5,460 tests pass (managed zero skips). The bounded roadless-camp native
check passes at `0cfd8e7`; exact-inventory release semantic review remains pending.
This census is not release approval.

## Retained master semantic resume census

The master semantic resume correction reports 3078 staged production C# files and 437,136
physical lines. Zero are at or above 300; direct `XRL` imports occur in 1434 files, none over
the cap. Exact staged source inventory digest:
`f533203f98ed26e0ab95970b54b13fce26d6281988e36c9e3d20f6f870a82bb9`.
Three existing production sources changed: the engine-free semantic clock rule, the master
settlement plan, and the access level of the system's existing required-step mask. No new
production source, serialized field, public API or compatibility dependency. At 76ab44d, all four
canonical compile modes pass (3074/3078/3254/3258 sources), repository audit is clean, and main
14,287/portable 5,451 tests pass with zero skips. Native component proof is recorded in STATUS;
the economic persona remains RED. Older evidence below is retained at its own inventory.

## Retained founding-cohort draft census

The Kingdom Quickstart founding cohort over the stockpile deposit custody fix, the Kingdom
Quickstart shelter ingress, the render-only city sight, the stockpile unit capacity, the
first-basin water store and the Kingdom Quickstart
tent rows reports 3078 staged production C# files and 437,114 physical lines.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`a918f8e8bab3c1ca44118211d0667ce027f1a0ef77610074f298e2b8b49cb396`. The census reports
1434 files with direct `XRL` imports; 0 of those exceed the line limit. This digest carries both
the camp heart's authored architecture, catalogue and blueprint bytes and the founding-cohort
sources merged from `dev`; no staged production C# source is added or removed by this branch.

The founding-cohort delta over the retained draft below is ten added production sources and no
removals: the quickstart rules' founders partial, the bootstrap's receipt partial split out of the
bootstrap itself, the three bootstrap founders partials, and the five the founder origin accounting
adds — its models, its bounded versioned codec, its host seam, its engine-free transaction, and the
one adapter that knows what a body and a settlement are. All four `Tools/gate.sh` modes compiled
clean on these bytes through the canonical gate itself — staged baseline (3074 sources), staged
compatibility (3078), dev-harness baseline (3241) and dev-harness compatibility (3245).

The shelter-ingress delta over the retained city-sight draft below is one added and one modified
production source and no
removals: the quickstart rules' shelter partial is the addition and the quickstart rules the
modification. Before the merge, all four `Tools/gate.sh` modes compiled clean on that delta's own
bytes under Roslyn on this machine — staged
baseline (3050 sources), staged compatibility (3054), dev-harness baseline (3204) and dev-harness
compatibility (3208) — with the installed-Hearthpyre source and ABI step, against the licensed
Managed references with warnings as errors. The six-profile native Quickstart boot matrix at seed
`#43101` passes on those bytes, as do `quickstart-save marsh yes` and its cold load; the Windows
gate did not run. On the merged tree the staged baseline (3058 sources) and staged compatibility
(3062 sources plus the tracked Hearthpyre 2.2.3 ABI stub) sets compile clean under Roslyn 9.0.306
on Linux; the two dev-harness modes, the installed-Hearthpyre source step, the Windows gate and any
native run did not run for the merged bytes. The exact-inventory semantic review in
`docs/STRUCTURE_REVIEW.json` binds an earlier digest and is therefore open for it.

The custody delta over that shelter-ingress draft is three added and four modified production
sources and no removals: the engine-free deposit law `Core/KingdomDepositEngine.cs`, its seam
`Core/IKingdomDepositHost.cs` and the GameObject implementation
`Growth/KingdomMaterials.StockpileDeposit.cs` are the additions; the stockpile room shard, the
material stock, the material-store rules and the settlement-pass yards are the modifications. On
those bytes the staged baseline (3064 sources) and staged compatibility (3068 sources plus the
tracked Hearthpyre 2.2.3 ABI stub) compile clean under Roslyn 9.0.306 on Linux; the two
dev-harness modes, the installed-Hearthpyre source step, the Windows gate and any native run did
not run for them.

## Retained city-sight census

The render-only city sight over the stockpile unit capacity, the first-basin water store, the
Kingdom Quickstart tent rows, the empty-camp legacy correction, the camp-guide topic tree, the
claimed-ground light and the first-settler legibility change together reported 3061 staged
production C# files and 434,296
physical lines.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`7147169b7ccb8d2142d9791bd5faec8405eb305e33bca7a9b9feb9c3948c5a1e`. The census reports
1425 files with direct `XRL` imports; 0 of those exceed the line limit.

The city-sight delta over the merged stockpile census is two added and two modified production
sources and no removals: the render-scope finalizer (`Growth/KingdomCitySightDrawScope.cs`) and the
render seam that owns the projection (`Growth/KingdomCitySightRenderSeam.cs`) are the additions;
the claimed-ground light part and the settlement event file are the modifications. Merging the
city-sight end-of-turn backstop with the basin-capacity zone-activation guard put
`Core/KingdomSystem.z20.Events.cs` at 305 physical lines, over the strict cap; the merge reflowed
those two comment blocks wider — every word and engine citation kept, no code and no statement
order changed, so every source test that pins ordering in that shard is untouched — and it is back
at 299. The staged baseline (3057 sources) and staged compatibility (3061 sources plus the tracked
Hearthpyre 2.2.3 ABI stub) sets compile clean under Roslyn 9.0.306 on Linux against the licensed
Managed references with warnings as errors; the two dev-harness modes, the installed-Hearthpyre
source step, the Windows gate and any native run did not run for it.

The stockpile delta beneath it was three added production sources — the
capacity constants (`Core/KingdomRules.MaterialStores.cs`), the survey's material-store reads
(`Growth/KingdomSurvey.11.MaterialStores.cs`) and the stockpile-room rules
(`Growth/KingdomMaterials.StockpileRoom.cs`, which owns the room, the intake that respects it and
that intake's proofs) — six modified (the delivery, the status line, the porter carry, the
clearance payout's destination choice, the strike salvage's destination choice and the yard's
nothing-landed fault line), and the regenerated removal-coverage roster. Each new file owns one
responsibility and no saved format, wire or public API changes for that delta. The staged baseline
(3055 sources) and staged compatibility (3059 sources plus the tracked Hearthpyre 2.2.3 ABI stub)
sets compile clean under Roslyn 9.0.306 on Linux against the licensed Managed references with
warnings as errors; the two dev-harness modes, the installed-Hearthpyre source step, the Windows
gate and any native run did not run for it.

The first-basin delta beneath it was three ADDED production sources
(`Growth/KingdomPlotHeartRules.Loader.cs`, `Growth/KingdomWaterDebit.OpenReservations.cs`,
`Growth/KingdomLab.Commission.Settle.cs`) and twenty-one modified ones. The first-basin water store
owns the three additions plus the founding-heart
identity and marks shards, the plot-effects furnishing shard, the zone-activation events shard,
the heart rules table, the civic-container envelope note, the survey capture sweep, the ground
reading, the ground-protection law, the four water-debit shards,
the generated removal-coverage table, and the seven water callers that can refund after their own
callbacks: construction funding, sowing, annexe enrolment, the lab commission, the lab retry
funding lane and its two removal lanes. Root and an independent AI reviewer read that delta and
every required finding it raised is addressed here. The seal lane beneath it contributed four
modified production sources and no additions or removals: the seal profile reader/writer, the
polity realm-legacy facts writer, the refound-import reader and the realm-exile rule. Root and
independent AI reviewer read that four-file delta and affected seal,profile,foundation,import and
exile boundaries. Explicit committed-unresolved
profile schema2 preserves real technology and provenance without inventing bodies; existing
immutable foundation observation owns exile admission. Old schema0/1 encodings remain unchanged.
New schema2 needs a new public version and is not downgrade-readable by0.3.1.
[Review evidence](STRUCTURE_REVIEW_0_3_1.md) records the reviewed reasoning; the exact-inventory
semantic review in `docs/STRUCTURE_REVIEW.json` binds an earlier digest and is therefore open for
this delta. Automated and native acceptance remain separately scoped. Public0.3.1 is unchanged.

## Retained Kingdom Quickstart tent-row census

The Kingdom Quickstart tent rows over the empty-camp legacy correction reported 3053 staged
production C# files and 432,593 physical lines.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`d0f0e0cc12d931557082d09ff97316fb3d8125ff8bd1f0aa6e1c60baff94cfb0`. The census reports
1418 files with direct `XRL` imports; 0 of those exceed the line limit.

The delta over the retained draft below was one added and six modified production sources and no
removals: the quickstart bootstrap's shelter partial is the addition; the quickstart rules, the
bootstrap, the camp builder, the generated removal coverage, the quickstart receipt model and its
wire codec are the modifications. The staged
baseline (3049 sources) and staged compatibility (3053 sources) sets compiled clean under Roslyn
9.0.306 on Linux against the licensed Managed references with warnings as errors; the two
dev-harness modes, the installed-Hearthpyre source step, the Windows gate and any native run did
not run for that delta. The exact-inventory semantic review in `docs/STRUCTURE_REVIEW.json` binds
an earlier digest and is therefore open for it.

## Retained empty-camp legacy census

The empty-camp legacy correction over the camp-guide topic tree, the claimed-ground light and the
first-settler legibility change together reported 3052 staged production C# files and 432,259
physical lines.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`c226862245f18d7b9fffadf7abc39b1d571462d1f26de6f665045f8ceaea412c`. The census reports
1417 files with direct `XRL` imports; 0 of those exceed the line limit.

## Retained camp-guide, claimed-ground and first-guest census

The camp-guide topic tree, the claimed-ground light and the first-settler legibility change
together report 3052 staged production C# files and 432,239 physical lines.
Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict cap; 0 exceed
1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`cf01fcc9993de9cee88d8ec6dc17dd8111eb37375f546d08850ac957fb372cad`. The census reports
1417 files with direct `XRL` imports; 0 of those exceed the line limit.

The delta over the retained draft below is three added production files &mdash; a mod-owned
`IZonePart` and its projection, and one engine-free words file for the optional Quickstart
guide &mdash; plus one modified event shard, one registry line, one option row, the guide's
root-option loop, four modified first-guest production shards and documentation. No saved format,
wire or public API is removed. The exact-inventory
semantic review in `docs/STRUCTURE_REVIEW.json` still binds the previous digest and is therefore
open for this delta; the human review it records has not been re-run here.

The camp-guide topic tree over the claimed-ground light alone reported 3052 files and 432,178
physical lines at inventory digest
`dcab3931d57df58aeaf3f0dee894acdec54d261a4e5f85d94cb369d8a1c73e96`; that digest no longer binds
the current bytes.

## Retained master-growth resume draft census

The master-growth resume correction reports3049 staged production C# files,431,893 physical lines,
zero at or above300,and1415 direct-XRL files. Exact inventory:
`a3a9c8dd8ea36962475266e7005ccc6fcdd352b3bfd3d9c4675beb47b51be2b9`.
Complete canonical parent/current comparison enumerates3044 unchanged,three modified,
two added,no removals. Root and independent AI reviewer read the complete five-file delta
and affected boundaries. A detached growth-resume protocol validates coherent clocks and
reproves exact source authority before the existing all-participant master publication.
Original children, health, field work and already-published arrival work remain retained;
no saved format or public API changes. [Review evidence](STRUCTURE_REVIEW_0_3_1.md)
binds this exact digest; native, managed and compile evidence remain separate.

## Retained recovery census

The recovery absence correction reports3047 staged production C# files,431,611 physical
lines,zero at or above300,and1415 files with direct `XRL` imports. Exact inventory:
`6dbd94092f57eeb9b79f5ff169105fa5a7ab2cc6702480d99ba98c92be17bff9`.
The complete canonical parent/current comparison enumerates3041 unchanged,five modified,
one added,no removals. Unchanged sources inherit review; root and independent AI reviewer
read the complete six-file delta and relevant boundaries. One ephemeral recovery decision
authority coordinates the existing custody survey and publication/quest protocols; no saved
fields or public API change. Any bound pass defers/refuses without a second classification.
See [review evidence](STRUCTURE_REVIEW_0_3_1.md). The semantic ledger binds this exact digest;
native/full-suite/compile evidence remains separately scoped in [STATUS](STATUS.md).

## Retained raid-death census

The preceding raid-death correction reports3046 staged production C# files,
431,474 physical lines, zero at or above300, and1414 direct `XRL` imports. Exact inventory:
`980afeb740331d69030591f1a6a27c575e9b61bd4448c05fe841f7b0cbeab6cb`.
One production file changes from the raid-contact checkpoint below. The exact semantic ledger
now binds this inventory through a complete canonical parent/current comparison: 3045 unchanged,
one modified, none added or removed. Unchanged sources inherit the retained review; root and an
independent AI reviewer inspected the complete changed file and affected death/wake boundaries.
Active attacks stay pending before cancellable removal; existing inspection owns finalization
after actual absence. Shared counting, recovery, saved fields and public APIs are unchanged.
See [review evidence](STRUCTURE_REVIEW_0_3_1.md). The strict structural release gate passed;
separate canonical compilation1886 then passed ordinary3042/3046 and developer3162/3166 inputs,
all120 Harness shards plus installed ABI. No native ordinary-turn, veto, recovery or save/load
acceptance is inferred.
Public0.3.1 installed content remains unchanged.

## Retained raid-contact census

The unreleased raid-contact checkpoint reports 3046 staged production C# files and 431,481 physical lines,
with 0 at or above the strict 300-line cap. Direct `XRL` imports remain in 1414 files;
none exceeds the line limit. Exact staged source inventory digest:
`3f0d1dde39c6a07cfd6cbad9f9888833b9ca0837bceec7918cb910260e1e9688`.
The canonical stage still contains 3077 files; public 0.3.1 installed bytes remain unchanged.

The [complete raid bridge](/tmp/taf-raid-contact-structure.boMu2H/raid-production-bridge.json)
enumerates every current path/hash and compares every actual parent Git blob at `cd6dedc`:
3044 unchanged, two modified, no additions or removals. Unchanged sources inherit the
retained stock review below, not a fresh deep read. Root and an independent AI reviewer
read both complete changed files and their affected debit/contact boundaries. The existing
water receipt retains active-survey authority, settlement-wide reserves and full accounting;
its internal exact-store selector cannot fall back to another vessel. Contact owns a real
survey scope through commit and compensation, and refuses failed reservations before mutation.
No saved fields, wire layout or public API changed. The current binding records this scoped
review, not ordinary play, save/load or unrelated raid acceptance.

## Retained initial-stock census

The released stock correction reported 3046 staged production C# files and 431,441 physical lines,
with 0 at or above the strict 300-line cap. It contains 1414 files with direct `XRL` imports;
0 of those exceed the line limit. Exact staged source inventory digest:
`9d9eb6416014c7257a26fa08178d8f738e44dd46a295ea3357f32b7739faf1b0`.
The generated staging list contains 3077 files; it does not prove installed or subscribed content.
The [complete stock bridge](/tmp/taf-quickstart-stock-structure.E4chRX/stock-production-bridge.md)
compares every then-current path against actual Git blobs from clean startup commit
`1c1c2bc54b4d11f0912f210b39baa89e635c157d`:3044 unchanged,one modified,one added,no removals.
Only the two stock production files received fresh scoped review; unchanged files inherit the
retained review chain, not a fresh deep read. A callback-free physical-child identity rule now
guards initial food/material totals; legitimate stacks, continuation and saved protocols remain
unchanged. Developer alias-fixture review remains separate from this production binding.

Retained startup digest `7e3fb7521f445e16b0740329966e369fa51d91fd768237bdffb5291d3540c651`
passed all six actual profile/advisor boot/save/cold-load pairs and all twelve strict save/load
checkers, with exact owned stops and independent idle checks. Its four-mode/ABI compile21734,
managed21635 13603/4990 zero skips and Tools69769 501 passes are prior-byte evidence, not stock
correction validation. This review claims no complete current stock compile, managed or native
pass. See [STATUS](STATUS.md) and [retained native evidence](/tmp/taf-quickstart-native.sYl4Dz/README.md).
Historical serializer evidence remains retained; ordinary rendered play, graceful Save-and-Quit
and desktop Quit, corrected subscribed delivery, full historical saves and public release remain
separate gates. The old stock binding is retained in Git; `docs/STRUCTURE_REVIEW.json` now binds
the raid correction above. [Scope and correction evidence](STRUCTURE_REVIEW_0_3_1.md) preserve inherited
review provenance and open functional limits. This is not the complete release gate.

## Retained beta hardening checkpoint — `7d331fe8`

At this earlier checkpoint, `Tools/check-structure.py --json` reported 2951 staged production C# files and 420,997
physical lines. Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict
cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`f9815fff2a1cf4389ecd42b733645b0611b31bbc8b58c96fae7d1636099e81b1`. The census reports
1376 files with direct `XRL` imports; 0 of those exceed the line limit.

The Claude launch-order foundation retains the reviewed `bf319c2` subsidence baseline. Its
one-file production diff exactly matches the independently reviewed artifact: launch prepares
and publishes; existing resume owns per-actor creation, placement, and activation. No new saved
field or custody authority is introduced. Two bounded native launch cases now prove multi-actor
callback ordering and different-blueprint replacement quarantine with retained evidence; ordinary
play, same-blueprint and interrupted-placement custody remain open. The roster XML correction and
six development Harness files do not change this production C# census or its retained semantic
review. Their separate source/native evidence is recorded in [STATUS.md](STATUS.md).
The earlier 2026-09-05 subsidence delta retained `b572ede` and its inherited
outbox, Quickstart, raid-zone, and polity reviews. Root and an independent reviewer inspected
the two production changes: the engine-free completion boundary and its actual Reckoning
callbacks. Existing bookkeeping and reached-rung work precede the guarded summary; required
work is never caught as presentation failure. No saved fields or wire layout changed.
Partial-step debt, committed-but-pending departures, and failures inside rung execution remain
open. Thirteen managed cases test this bounded completion contract, not native departure or
ruin effects. Development-only fixtures remain excluded; Claude's raid-custody patch stays
separate. These are source/engine-contract reviews, not native playtests.

At that checkpoint, no staged production source breached the strict physical-line cap. This cleared
its mechanical line debt; the semantic ledger then bound the exact-inventory review to its
`f9815fff…` digest above. That retained review is ancestry, not the current isolated draft binding.

That hardening sequence semantically decomposed 144 additional oversized authorities,
bringing the cumulative total to 154. That is 25 more decompositions since checkpoint `2cb97fc`,
19 more than checkpoint `d3fc4b9`, 16 more than checkpoint `b049c17`, and 13 more than hosted
checkpoint `1c2d619`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. That checkpoint was measurable progress, not release signoff:
0 line-cap failures remained, and the semantic ledger then bound its exact-inventory review
to that retained digest. Any staged source change invalidates the binding and requires a new census and review
binding.

## Release review contract

Automation cannot decide whether a type owns one coherent responsibility or whether its engine,
serialization, public-API, and third-party seams use suitable protocols. Release mode therefore
also requires `docs/STRUCTURE_REVIEW.json`, bound to the exact staged source inventory digest.
Copy `docs/STRUCTURE_REVIEW.example.json` only after the review itself; replace every placeholder and
record concrete evidence in both notes fields. Schema 1 accepts no exceptions: exactly the
template keys, nothing else. Reviewer identity is 2–80 printable characters; each evidence note is
20–2,000 printable characters; completion uses a real second-precision UTC timestamp. Placeholder,
sentinel, example, TODO, TBD, UNKNOWN, and N/A reviewer/notes text is rejected, as is any text
claiming automation is a human or that a human personally/physically performed the review. Per
the author ruling of 2026-09-11 ("no manual test gate for release, forever"), each evidence note
must also bind to a real, checkable artefact — a run id, a log/receipt path, or a digest, written
as `run:<id>`, `log:<path>`, or `sha256:<hex>`/`receipt:<hex>` inline in the note — so an honestly
labelled automated review is exactly as verifiable as a human one was. Changing that rule
requires an explicit author amendment to Addendum 9, not a tooling allowlist.

### Addendum 9 amendment — who may sign (author ruling, 2026-09-02)

The author ruled that the exact-inventory semantic review may be performed and signed by the AI
reviewer acting under the author's explicit authorization, recorded honestly as such in
`reviewedBy` (never as a forged human signature). The review remains real: every staged file is
enumerated, risk-weighted deep reads are recorded, findings carry file:line evidence, and every
Required finding is fixed before signing. The digest binding and the placeholder refusals are
unchanged; a fresh review binds to every new digest.

Decompose debt one owned state machine or transaction at a time. Preserve serialized names and
public contracts, add characterization and migration fixtures first, then rerun compile, pure,
source, native save/reload, and behavior gates appropriate to the moved boundary.
