# Alpha 0.3.7 production delta review

## Refreshed housing and construction review

Codex root automated review, 2026-09-15. Current inventory: 3,106 production C# files,
440,877 physical lines, 1,450 direct XRL imports, no file at or above 300 lines.
Inventory SHA-256: `3dc69687e01f930bffc8f840a15f4a5a885c3744a6fc17049a8ce91c5d881e62`.
Compared every staged production path and byte against reviewed candidate `fbaaf549`:
3,076 are identical and 30 changed or added. Full comparison:
`/tmp/taf-alpha037-production-comparison-4.json`; canonical census:
`/tmp/taf-alpha037-structure-inventory-4.json`. The earlier review below is retained historical
context; its pending native statements and smaller inventory are not current claims.

| Changed responsibility | Boundary review |
| --- | --- |
| QuickstartRules.Shelter; PlotRules | Medium starting lots and Camp access follow real enclosed shelter geometry. Reserved approach coordinates remain deterministic and separate from founder/heart ground. No population gate or fabricated capacity. |
| Architecture.Drafts; Records; XmlRecords; RetainedBindings | Explicit retained-size bindings preserve old readers. Minimum-size validation stays centralized; current commissioning still applies the current catalogue minimum. No saved snapshot rewrite. |
| AdoptRules.Enclosure; LodgingRoomRules | Existing adoption bound remains unchanged; bounded designated buildings can supply their own maximum. Pure room measurement receives physical cells and sleeping providers, reserves all furniture/beds, and connects clear floor through usable doors to exterior approaches. Privacy uses density and separation; unusable/exposed rooms cannot gain privacy from catalogue area. |
| BenefitIndex; Build; Allocation; Evaluate; ProviderRows; Rooms; Lodging.HomesAndReporting | Engine adapters collect actual operative sleeping places before enrollment caps, cache measurements in one benefit index, and return copies. Physical furniture observation is separate from native movement. Authored privacy is limited by the physical reading, never granted from a label alone. |
| ArchitectureStamper.RenovationOccupants; UpgradeApplication; UpgradePreflight; Plot2.26e.EnvelopeClearance; PlotOccupantRules; Upgrade.20.HandOver | Pure passability comparison identifies newly blocked ground. Engine boundaries reuse protected-body classification, planned destinations, rollback and exact post-callback custody. Final application requires strict clearance. This extends lawful annex clearance to retained floors becoming walls; existing walls, unmapped ground and protected actors stay guarded. |
| ArchitectureStamper.PendingComponents; Verification | Temporary physical mismatch refuses without altering intact owner authority. Exact restored identity/custody is rechecked; malformed owner/upgrade quarantine remains. A floor exception requires the exact current paid job, source/output, ownership, frozen successor and phase-four receipt at the shared main cell. No foreign furniture whitelist or copied-receipt authority. |
| SocketTransitionRules; SocketTransitions; Helpers; History | Pure receipt selection separates current quotes from complete retained declarations keyed by digest. Engine lookup binds retained endpoints and typed lot before existing receipt/job/snapshot proofs. Registry bounded, reload clears both maps, and no new persistent field or receipt mutation. Legacy schema behavior remains explicit. |
| QuickstartBootstrap.Founders; QuickstartEmbarkModule; QuickstartEncounterScope | Only freshly allocated reversible grants receive initial local Brain anchors. Exact placement checks brain and anchor before commit. Encounter suppression is disposable and bound to initial game, manager, reserved profile and actual generation context; later worlds/encounters retain their path. No old citizen, faction, saved body or wildlife deletion. |

Production XML supplies deliberate enclosed canvas/hut programmes, explicit historical Small
readers, retained price declarations and material charges for added fixtures. These are authored
inputs to existing compiler/receipt protocols. Core/KingdomReleaseInfo.cs retains this candidate's
0.3.7 version; it is the only production C# difference from accepted housing pin `d5a0e96d`.
No serialized field layout or schema changes are introduced by this delta. Existing public
entry points are retained; the enclosure overload is an implementation API extension.

Evidence: run:34882424586; log:/tmp/taf-founder-anchor-gate-1.log;
log:/tmp/taf-paid-housing-licensed-integration-1.log (14,957/5,849 cases, zero skips);
receipt:sha256:aee3c0a96e2ecb8ba341ccc0261de5a62fa9ba35e83dc850c528b3ff2c22b635.
The native paid-housing warm/cold pair proves its exact d5a source and scoped fixture, not this
versioned package or all architecture. Earlier shared-room and paid-heart receipts retain their
own pins and limits. Historical geometry/quarantine, polity reconciliation, ecology breadth,
multi-map balance and full building/activity quality remain open issues. No required structural
finding remains from the reviewed delta. Candidate release and subscribed acceptance are separate.

## Earlier candidate review (historical)

Automated source review by Codex root, under the standing author authorization. This is
structural review, not native or delivery acceptance. Baseline: published `v0.3.6`, whose
review bound `e3af1fefe18489009c7d43d9dfae08b888c3cb29cf7fcbbfa9e2b60da7a9a719`.

Canonical staging enumerated 3100 C# files: 3082 byte-identical to that baseline and 18
changed or added. All remain below 300 physical lines. Complete path/hash/line inventory:
`/tmp/taf-alpha037-structure-inventory-3.json`. Inventory SHA-256: `1f35396060ae02596850e177647cc92bf8decd9dc144b97504241851ccf2df68`.

The following risk-weighted reads cover all changed production files. Existing source
responsibilities and contracts outside this delta inherit the prior review.

| Files | Responsibility and boundary review |
| --- | --- |
| Core/KingdomInheritanceSpatial.cs | The active-zone capture binds one local survey and disposes it with `using`. Inactive/malformed inputs keep the existing core validation. No persistent fields change. |
| Core/KingdomReleaseInfo.cs | Diagnostic version constant follows manifest 0.3.7; no state or branching change. |
| Growth/KingdomFoundingHeartUpgradePathRules.cs | Pure, bounded traversal of already validated paid registry. Rejects branches, cycles, wrong owner/zone, changed root coordinates, skipped rungs, cancelled jobs and unsettled completed jobs. Completed history and one pending edge are distinct outputs. |
| Growth/KingdomPlot2.07t.FoundingHeartUpgradePath.cs | Engine adapter binds the path to the exact terminal, claimed zone and realm. Retired bodies must be globally absent; final output must resolve uniquely at the paid coordinate. A pending receipt additionally requires the actual working improvement part. |
| Growth/KingdomPlot2.07s.FoundingHeartClimbedChain.cs; Growth/KingdomPlot2.07q.FoundingHeartRecordedRemoval.cs; Growth/KingdomPlot2.07t.FoundingHeartClimbHold.cs | Founding recovery uses the bounded chain, immediate predecessor corroboration and exact removal proof. Hold release names a subject in that authenticated path; it does not infer retirement from absence alone. |
| Growth/KingdomPlot2.07c.FoundingHeartMarks.cs | Survey stake admission requires complete founding plan, transaction, claimed zone, seal, exact slot identity and exact physical mark. No broad blueprint whitelist is added. |
| Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs; Growth/KingdomArchitectureStamper.UpgradePreflight.cs | Envelope proof retains protected body, liquid, connection and owned-ground checks. Preflight admits bodies on explicitly nonblocking successor placement slots, including retained ground; blocked annexed ground still requires the existing movement authority. Paid application still requires strict clearance. Authenticated founding stakes and exact unpaid tracks have narrow heart-only treatment. Occupied retained blocked/unknown slots and foreign objects remain protected. |
| Growth/KingdomRoadRules.cs; Growth/KingdomRoads.08.RoadReceiptHelpersAndStatus.cs | Pure wear classifier keeps invalid states and paving protected. Engine admission requires exact ordinary wear blueprint/state, unique floor, no ownership, no paid receipt, no plot evidence and no architecture component marker. |
| Growth/KingdomConstructionRules.ScaffoldWork.cs; Growth/KingdomScaffold.RemovalProof.cs | Outstanding retry is admitted only for improvements with committed removal proof and physical phase None. Existing immutable intent, registry currency, exact successor, global absence and receipt closure remain mandatory and are rechecked after publication. |
| Growth/KingdomUpgrade.20.HandOver.cs; Growth/KingdomUpgrade.24.HandoverContents.cs | Both first and replayed authored handover bind a survey, prove paid endpoints, clear eligible annexed occupants through existing destination/rollback rules, then reprove endpoints, content custody and strict ground before layout application. Existing retry/quarantine distinction and final custody/removal remain in charge. |

| Growth/KingdomConstructionRules.cs; Growth/KingdomPlot2.13.PayloadCodec.cs | The per-job payload bound rises from 8192 to 32768 characters to admit the existing architecture envelope plus wrapper. Writer/reader share the same cap; the 4 MiB registry limit, canonical formats, hashes and inner architecture binary cap remain unchanged. The plot codec change corrects its documentation only. |

The only production XML delta adds six shaped timber to the moot-to-court bill, matching
six added authored floors. No serialized field layout or schema changes. No foreign library
boundary changes. The new path classifier is internal; existing public entry points retain
signatures. The added road rule is an implementation helper, outside the supported API list.

Follow-up comparison enumerated every production path and hash against the prior candidate
inventory; only `KingdomArchitectureStamper.UpgradePreflight.cs` changed. Reviewed its complete
method and delta: placement passability uses the existing decoder and blocked-layer precedence;
no debit, movement, ownership or serialization authority is added. Full native retained-ground
probes require exact object restoration and preserve foreign-wall refusal. All other reviewed
production inputs remain byte-identical to the prior candidate review.

Relevant validation: fourteen focused source cases, 64 persona-validator tests, docs and four
engine compile modes passed at `78e8d097`. CI `34820124279` and Native27 are pending. Native26
identified an enrolled resident on a retained walkable predecessor floor and failed before
payment; its complete archive is recorded in STATUS.md. No full native paid-chain, higher-heart
cold-load or new subscribed-candidate acceptance is claimed by this structural review.
No required structural finding remains; native acceptance is independent.
