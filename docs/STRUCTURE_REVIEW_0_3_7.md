# Alpha 0.3.7 production delta review

Automated source review by Codex root, under the standing author authorization. This is
structural review, not native or delivery acceptance. Baseline: published `v0.3.6`, whose
review bound `e3af1fefe18489009c7d43d9dfae08b888c3cb29cf7fcbbfa9e2b60da7a9a719`.

Canonical staging enumerated 3100 C# files: 3082 byte-identical to that baseline and 18
changed or added. All remain below 300 physical lines. Complete path/hash/line inventory:
`/tmp/taf-alpha037-structure-inventory-2.json`. Inventory SHA-256: `e84f0e9aca18ce55811ecbf7e3528cd2cda79c63209b80423a3273a67685ac3c`.

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
| Growth/KingdomArchitectureStamper.EnvelopeGrowth.cs; Growth/KingdomArchitectureStamper.UpgradePreflight.cs | Envelope proof retains protected body, liquid, connection and owned-ground checks. Preflight admits only bodies already proved on newly annexed ground. Paid application still requires strict clearance. Authenticated founding stakes and exact unpaid tracks have narrow heart-only treatment. Retained-ground occupants remain protected. |
| Growth/KingdomRoadRules.cs; Growth/KingdomRoads.08.RoadReceiptHelpersAndStatus.cs | Pure wear classifier keeps invalid states and paving protected. Engine admission requires exact ordinary wear blueprint/state, unique floor, no ownership, no paid receipt, no plot evidence and no architecture component marker. |
| Growth/KingdomConstructionRules.ScaffoldWork.cs; Growth/KingdomScaffold.RemovalProof.cs | Outstanding retry is admitted only for improvements with committed removal proof and physical phase None. Existing immutable intent, registry currency, exact successor, global absence and receipt closure remain mandatory and are rechecked after publication. |
| Growth/KingdomUpgrade.20.HandOver.cs; Growth/KingdomUpgrade.24.HandoverContents.cs | Both first and replayed authored handover bind a survey, prove paid endpoints, clear eligible annexed occupants through existing destination/rollback rules, then reprove endpoints, content custody and strict ground before layout application. Existing retry/quarantine distinction and final custody/removal remain in charge. |

| Growth/KingdomConstructionRules.cs; Growth/KingdomPlot2.13.PayloadCodec.cs | The per-job payload bound rises from 8192 to 32768 characters to admit the existing architecture envelope plus wrapper. Writer/reader share the same cap; the 4 MiB registry limit, canonical formats, hashes and inner architecture binary cap remain unchanged. The plot codec change corrects its documentation only. |

The only production XML delta adds six shaped timber to the moot-to-court bill, matching
six added authored floors. No serialized field layout or schema changes. No foreign library
boundary changes. The new path classifier is internal; existing public entry points retain
signatures. The added road rule is an implementation helper, outside the supported API list.

Relevant validation: source regressions and CI run `34808115103`; the eighteen changed C#
inputs are the same gameplay delta plus candidate version. Native24 at `1729bae6` completed rung three then refused the old payload bound. Main heart17,
registry65, main/portable boundary4 each and four engine compile modes passed after repair at
`8ceed37e`; Native25 tests that runtime with eighteen synthetic homes. Its result and
the separate higher-heart cold-load gap must be recorded in STATUS.md before release claims.
No required structural finding remains; native acceptance is independent.
