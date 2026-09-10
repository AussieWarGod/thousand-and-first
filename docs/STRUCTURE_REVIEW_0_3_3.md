# 0.3.3 stockpile hotfix: structural review

Codex with independent Claude source reviews, under the author's 2026-09-02
Addendum 9 amendment. This is an AI review, not a human signature.

## Inventory and inherited review

SHA-256: `249d3bb40ca34f57289770494e985a97e81cf42c2bae8f053fb6e14005b61d3d`.
3069 staged C# files, 435,617 physical lines, 1430 direct-XRL files; none at or
above 300 lines. Every canonical staged C# path was enumerated and its Git blob
compared with shipped main `d02608a`: 3063 unchanged, five modified, one added,
no removal. Machine-readable local evidence: `hotfix142-inventory-comparison.json`.

Unchanged sources inherit `d02608a:docs/STRUCTURE_REVIEW.json`, inventory
`93cec174fdb61a025dca0f8982f01f62e52e8ce80ff9479be2d8c3c50552aaaa`.
That chain's explicit coverage limitations remain. This is not a new deep read
of every unchanged file or native acceptance.

## Risk-weighted delta reads

| Source | Responsibility and boundary |
| --- | --- |
| `Growth/KingdomSurvey.LocalOperation.cs:11` | New local-operation entry requires exact active zone; reuses same survey; refuses conflicting authority before capture, rechecks after capture, returns existing disposable PassScope. No saved state. |
| `Growth/KingdomMaterials.05.StockpileAndPaymentGates.cs:19` | Stock and exact-container reads hold a short scope through tally; existing lease and custody predicates remain. Container restriction cannot broaden its selected store. Returned tally is not durable debit authority. |
| `Growth/KingdomCommission.cs:59` | Final overload holds scope through reserve, funding, projection and exits. Existing ownership, design, zoning and preview checks remain; null System now refuses. No scope crosses the UI popup. |
| `Growth/KingdomPlot2.10.Commission.cs:17` | Direct plot callers hold complete transaction scope. Nested calls reuse one survey with balanced depth. Exact-quote, purpose-cargo and payment outcome contracts unchanged. |
| `World/KingdomQuickstartBootstrap.Materials.cs:73` | One allocating engine identity read on each fresh private stack before insertion; non-allocating readback before and after insertion. Existing grant owner handles rollback. No old-stock repair. |
| `Core/KingdomReleaseInfo.cs:9` | Only 0.3.2 to 0.3.3 version literal; no behavioral or serialization boundary. |

Deep reads included changed transaction bodies, existing PassScope lifecycle,
survey capture/reuse, lease/custody predicates, debit commit revalidation, fresh
grant creation and rollback. Independent Claude reviews cover scope `ad0dd90`
and fresh identity `3500035`; their production bytes are unchanged here. Full
local reports: `hotfix142-scope-draft-review.md`, `review-3500035-findings.md`.

No Required production finding remains. The inaccurate identity test-summary
attribution was corrected in `ebbb919`. Short preview versus full commit scope
is intentional: no global pass crosses user input. Review shorthand describing
a 298-line cap was incorrect; the contract is strictly under 300 physical lines.
Source mutation checks prove wiring, not executed engine behavior. Combined
gates, actual debit/projection, save/load and Steam delivery require separate
receipts and are not signed by this structural review.
