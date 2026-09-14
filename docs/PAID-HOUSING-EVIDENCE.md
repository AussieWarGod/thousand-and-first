# Paid housing development evidence

This history preserves failed runs and what was known at each source pin.
Pending statements below describe those historical runs; current acceptance belongs in STATUS.md.

## Fresh founder anchors: recurring ecology loss addressed, native verification pending

Two unchanged `5549a947` warm attempts lost original founders to glowpads before payment.
The first physicker died at turn 14,469; the next run lost a drifter at turn 515 (already at
73,5, far from the camp) and a tinker at 7,580. Both failed cohort checks and remain archived:
`paid-housing/5549a947/founder-lost-before-payment-1/result.json`, SHA-256
`7b1e939e188768c2e6d1d5d628186fbc6d0bc758c72cc11e296871f4a33eb9ba`, and
`paid-housing/5549a947/repeated-founder-loss-1/result.json`, SHA-256
`0c806a31942b013562b1ebf38aedb4305862998e99da7290b7f92abbc5ea4d1a`.
The second row's partial observed count is not a complete death census; two fatal events are logged.

Vanilla BaseFarmer disables wandering, whereas our settler blueprint enables it. The new cohort
had no initial civic anchor. Installed 2.0.211.51 Brain.Stay and Bored confirm that a non-wandering
NPC returns to its anchor while ordinary work/idle goals remain available. Existing KingdomStations
already uses that mechanism. Newly allocated Quickstart founders now start anchored on their
reserved approach cells inside the reversible grant, before any simulation turn. Placement verifies
the anchor; the native startup observer requires all four to have a local non-wandering anchor.
Existing citizens, citizenship allegiance, combat, idle tags and worker/sleep goals are untouched.
This is no immunity guarantee and does not repair historical unanchored founders or settle all #237.
Native housing/retry/save/cold continuation must run again on this changed production source.

## Paid housing warm/save/cold identity checks pass; next-action stock fixture corrected

At `eda49f65`, the full warm persona passed: real paid upgrade, controlled basket refusal and
Outstanding retry, foreign-furniture insertion probes, original four housed founders, exact
contents/payment and real save. Separate cold process re-proved the same paid job, frozen home,
basket contents, four original housed citizens, two rooms, six beds and 32 clear floor cells.
Its next fire commission correctly refused because the earlier conversion consumed all timber.
The follow-up action took about 94.5 seconds before that refusal, with no local survey bound around Next.

The fixture now supplies only the next design's missing material units after proving completed
housing and before saving; the cold action still performs ordinary quote, CanPay and exact debit.
That next action now shares a disposed local-operation survey. This is test stock and test scope;
production resource costs, housing capacity and gameplay verdicts remain unchanged.
Closed failed overall archive: `paid-housing/eda49f65/cold-next-refused-1/result.json`, SHA-256
`2d549204178b62674d015beffd1135daa1c16998903005ab54c381a15ae5b0b9`.
Both processes are proved stopped; 3,448 warm and 3,450 cold inputs match the sealed recipes.
Warm persona and both strict logs passed, while lifecycle continuation failed. The image shows
the converted room enclosed with a clear entrance aisle; the lower starter home is partly outside
the captured viewport, so this is not full-map framing or all-building visual acceptance.

## Full paid cohort passes; save-witness presence correction

At `9d5d2cf5`, actual paid completion, controlled Outstanding retry, preserved contents and
foreign-furniture probes all passed. The completed settlement retained all four original housed
citizens, two enclosed rooms, six usable beds and 32 clear floor cells. The subsequent harness
save-witness guard incorrectly used `GetStringGameState(key) == null` to prove absence; the
engine's absent-key default is not a presence test. It is now replaced by the existing shared
five-table observer/classifier, checked both before payment and before writing the witness.
Production is unchanged. No saved existing key is overwritten to make the test pass.
Closed failed archive: `paid-housing/9d5d2cf5/witness-absence-refused-1/result.json`, SHA-256
`b128ecf5c24dd921c81e6ebe0e7676ee8de826b878cdee1b85c1f11bd48700dd`.
Cold-load acceptance is still pending; the remaining save/load path has been checked against the
existing lifecycle contracts before replay.

## Compiled furniture-anchor witness correction

The follow-up at `8d776730` again completed the paid functional conversion and insertion probes,
then refused in the new test counter because compiled anchors carry coordinate identities
(`fixture:hearth@x,y`), rather than bare role strings. Closed failed archive:
`paid-housing/8d776730/floor-programme-refused-1/result.json`, SHA-256
`1f1f62b78948f5118340ff72c6ec24072f426f7a45196e5f53f8b1cc17ef7a6d`.
The pure harness counter now reads that identity form. A focused test compiles every current
Medium canvas/hut variant in every facing and checks the actual counter against the expected
17/16/15 clear cells. All five canvas-home cases pass, including existing funded-delta coverage.
The native scenario also checks the prepared target's room programme before payment. This is
a test correction; production remains the floor fix at `2e61ab42`. Native cohort/cold proof pending.

## Paid conversion completes; variant-aware room witness pending final native acceptance

At `2e61ab42`, the actual paid conversion passed retained-price selection, the controlled basket
handover refusal/Outstanding retry, and the new insertion guard's exact-predecessor/foreign-chest/
borrowed-receipt/recovery probes. Apply and stamp reached phase five successfully; completion
verified the exact paid functional hut and retained basket contents before the cohort assertion.
The run then refused because the test assumed every hut had at least sixteen clear floor cells.
Authored Medium variants with both hearth and table have fifteen: 24 interior cells minus three
beds, two seats, basket, main object, hearth and table. The basic hearth-only variant has sixteen.
The layout studio independently reports those counts with enclosed beds and clear fixture access.

Closed failed archive: `paid-housing/2e61ab42/floor-pass-cohort-refused-1/result.json`, SHA-256
`3759e554f47a8ae9dc4ee729ede04fa08e6202855578e628685a2e2f61c2d52c`.
Strict log passed; the overall persona failed, with no completed cohort or cold-load acceptance.
The native observer now binds its expected floor count to the frozen hearth/table placements.
It still requires three usable enclosed beds per home and all four original citizens housed;
no production layout or quality threshold was relaxed. Refusals retain live occupied-cell detail.

## Paid housing main-cell floor: exact blocker identified, repair pending acceptance

At `9dd1064b`, real payment, catalogue drift, physical restoration probes and the controlled
Outstanding retry passed. The first subsequent refusal was layout phase four: new ground slot
`g:06:01` encountered predecessor `676` (`r_KingdomTentRow`) on its own main cell `(19,8)`.
The predecessor was classified as protected Held state; this was not foreign furniture.
Closed failed archive: `paid-housing/9dd1064b/main-cell-floor-refused-1/result.json`, SHA-256
`bfc3f2c309e0a210a4fe8c1fb059a2e0fdaa78c3c8d49316b4ea72a70a2370cd`.
The earlier `cf76dab5` run already contained this exact category of foreign-slot diagnostic.

The placement guard now permits a nonblocking ground placement beneath only its exact paid
predecessor on the shared main cell. Current job identities, zone ownership, both receipts,
pending successor, frozen layout headers and the phase-four upgrade receipt are required.
Other protected objects remain obstructions. A native probe calls the actual placement guard
at this boundary, checks refusal for a foreign chest and the same chest with a borrowed paid
receipt, removes borrowed authority before destroying the fixture, and rechecks recovery.
Completed conversion, retained contents/founders and separate cold load remain mandatory;
no successful native acceptance for this repair is claimed yet.

## Restored fixture recovery: native regression found, repair pending acceptance

Issue #242 was reproduced at `8a95cdc1`: a genuinely paid seven-dram Medium housing conversion
accepted the retained declaration after catalogue drift and reached actual handover. Removing the
original basket caused a physical refusal and an Outstanding job; restoring it exactly left a
permanent owner fault. The job later required inspection and settlement staging logged errors.
Closed failed archive: `paid-housing/8a95cdc1/restored-basket-quarantine-1/result.json`, SHA-256
`5bcdc1ac23941f0d3f7c779d708483aa40a506efdfe1292cdd260d1319bc71b6`.
The 24,000-turn fixture also exhausted its initial water and lost citizens for that separate reason.

Physical output verification now refuses missing, moved, duplicated or changed evidence without
poisoning intact owner authority, so exact restoration can be rechecked. Owner/publication/upgrade
schema quarantine remains. No saved fault is erased and no missing object is recreated. The native
fixture adds missing/moved/wrong-token restoration probes before payment and transfers 32 existing
carried drams to the receipted cask for its extended wait. It still requires actual handover refusal,
ordinary paid retry, retained contents, four housed founders, save and separate cold load.
199 focused architecture cases and 66 persona checks pass; native acceptance is pending.
Already-quarantined historical saves and the effect of ongoing physical damage on city-wide staging
remain separate open work in #242.

At `cf76dab5`, one run stopped before payment when a glowpad killed the original tinker at turn
1,962 (recorded in #237; no water shortage). A further run on the same code conserved 32 carried
drams and passed all three restoration probes, real payment/history selection and the controlled
Outstanding handover. It still failed later with `InspectionRequired` and ground-slot `g:00:00`
verification errors. Closed failed archive: `paid-housing/cf76dab5/post-retry-endpoint-refused-1/result.json`,
SHA-256 `c4ac5bcec88f1bb784ff39c76c19305b3718eb7ff67ed135b11f48f239b7c69f`.
Read-only apply/stamp/handover diagnostics now preserve the first subsequent refusal, before a
later generic endpoint error can replace it. No completed conversion or cold-load acceptance yet.

## Paid transition price history: implementation pending native acceptance

Issue #239: same-set completion compared its saved declaration digest with current prices, so a
price edit could invalidate an exact paid receipt. A receipt-only history now retains complete
historical declarations by digest. New quotes and receipt binding still require current prices;
completion additionally permits a registered historical digest with matching endpoints and typed lot,
then reuses the existing exact key, before/after hashes, job and property-shape checks. No saved
price, layout or serialized field changes. Schema-one adoption retains its existing current path.

The three pre-enclosure Small shared-home conversion prices are retained from source `e61193ca`.
Nine new cases pass in both main and portable suites: actual old/new canvas prices, current and
legacy receipts, unknown/torn history, job/endpoint mismatch and refusal to use history as a fresh
quote. Thirty existing transition cases and all four engine compile modes also pass.
Actual paid construction under price change, obstruction/retry and separate cold load remain
unproved. Some old routes may already have failed material preflight; do not infer historical
commissionability from their XML. Issue #240 separately tracks deliberate enclosed renovation
layouts for pre-redesign roots/furniture; retaining a price does not solve that geometry change.

The `paid-housing-native-check` scenario now commissions an actually completed Quickstart Medium
tent row through the normal prepared conversion API and measures physical water/material debit.
It changes only the test registry's price from seven to eight drams after payment, requires refusal
without registered history and recovery with exact history, then interrupts actual handover by
temporarily removing the original storage basket. Ordinary retry must retain payment, basket and
contents. Separate lifecycle load checks the completed home. Missing materials and one brush
contents sentinel are disclosed synthetic inputs. Native execution is pending; 66 persona checks
and all four engine compile modes pass. This completed-save scenario does not cover a pending
job across versions, historical geometry, or ordinary city balance.

First native attempt at `d0d47e51` retained all four founders through 16,802 ordinary turns with
two enclosed homes, six beds and 34 clear cells, then refused before payment because the fixture
requested palette role `storage` instead of stateful anchor `fixture:storage`. Closed failed archive:
`paid-housing/d0d47e51/storage-anchor-refused-1/result.json`, SHA-256
`741538731705c229885a8feaf11ba0b655e174a35b5e0958e0c05d446a5fed67`.
The corrected fixture also uses a dedicated post-conversion cohort observer: the original
Quickstart observer intentionally requires two canvas homes. The new observer retains original
identities/assignments, three enclosed beds per home and useful floor space; a furnished hut can
spend one clear cell on its table. The initial preparation refusal for a missing advisor flag
launched no game and is retained separately under `prepare-missing-advisor`.

