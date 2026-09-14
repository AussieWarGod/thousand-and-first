# Alpha 0.3.7 production delta review

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
