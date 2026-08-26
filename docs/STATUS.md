# Current implementation and release evidence

**Snapshot:** 2026-08-27
**Target:** feature-complete v1.0 test candidate
**Current public version:** 0.2.0 work in progress

This file is the canonical short status. A green source, compile, or generator gate proves only
that layer. Native Caves of Qud behavior is signed only for the exact exercised native cases;
visual quality, accessibility, compatibility, and Steam subscription remain separate evidence and
are never inferred from source or static automation.

## Current automated evidence

| Layer | Latest result | Scope |
|---|---|---|
| Last clean release audit | **PARTIAL PASS** — 10 of 11 automated layers green at committed candidate `19fb8ee`; structural release layer red | Docs/hygiene, ABI, inventory, baseline/compatibility compile, 7,586-case full suite, 171-case portable suite, architecture/XML/art, balance, 43-case smoke harness, Workshop package, and deploy dry-run passed. This remains a bounded committed receipt, not proof for later structural revisions. |
| Staged runtime compile | **PASS** — 1307 sources, baseline and compatibility symbols | Exact current inventory selected by `Tools/stage.sh`; installed Qud 2.0.211.51 references. Runtime behavior still needs a native pass against the exact final commit; compile success does not close the compatibility behavior matrix. |
| Cold-install inventory | **PASS** — 1331 files | Canonical staged package inventory only; package shape does not sign native load, behavior, appearance, or private Steam subscription. |
| Architecture generator | **PASS** — 337 generated larger-lot maps, 242 larger-lot bindings, 277 copied predecessor tiers current | Generated larger-lot XML is byte-current. These are the generated subset, not the complete authored registry counted below. |
| Architecture checker | **PASS** — 136 buildings, 126 plotted buildings, 499 maps, 355 bindings, 403 tiers, 516 variants, 2,064 variant/pose goldens, zero issues; largest exact a2 receipt 6,324 bytes / 8,500 characters; 3 expected installed-base tolerant-recovery warnings | Static topology, entrances, fixtures, palette/material/technology constraints, typed-lot coverage, deterministic snapshots, and an independent mirror of the runtime's 8,192-byte / 11,264-character codec envelope. The warnings are the named recovery path for malformed vanilla `Creatures.xml`, `Furniture.xml`, and `Items.xml`; it does not judge native appearance. |
| One-survey focused tests | **PASS** — 9 focused source-contract cases | Maintained named indexes, active-pass consumers, mutation observation, and the absence of reachable second whole-zone scans. Dense native scan instrumentation remains open. |
| Addendum 9 structural census | **RELEASE BLOCKED** — 1307 staged C# files / 248,807 physical lines; 52 exceed 300 physical lines, 0 are exactly 300, therefore 52 fail the strict cap; 28 exceed 1,000, 9 exceed 2,000, and 0 exceed 5,000 | The hardening sequence has decomposed 119 additional oversized authorities, 129 cumulative. Direct `XRL` imports: 442 files, 49 over the line limit. Inventory SHA-256: `9c3713897b2fbf9b7db455247a0ac20d31e75f0eb00edf5f4c57d3a310b10b21`. Release still requires every source strictly under 300 and the currently missing `docs/STRUCTURE_REVIEW.json` exact-inventory human review. |
| Full Qud-referenced/source suite | **PASS** — 7,695 / 7,695 cases, 0 skipped | Final integrated hardening tree green against the configured licensed base. This does not sign native Qud behavior. |
| Portable suite | **PASS** — 173 / 173 cases, 0 skipped | Repository-only lane green. Public CI has an exact three-label allowlist for installed-data-only skips when licensed Qud data is absent; extra/missing skips and an explicit invalid base fail. Canonical release `test.ps1` forbids skips. |
| Tools tests | **PASS** — 33 tests | Repository tooling checks green. |
| Art tests | **PASS** — 19 tests | Art policy and wiring checks green; native appearance review remains open. |
| Latest retained native smoke | **PARTIAL PASS** — fresh profile, founding, 17/17 checks, one production gallery case, save/cold-load, repeat 17/17, clean log | Clean commit `19fb8ee` deployed against Qud 1.0.5/core 2.0.211.51. This signs only that commit's loader/founding/single-sample persistence smoke. Later structural revisions have no native compile/load/log receipt and are not covered by this row. |

Reproduce the current static results:

```bash
./Tools/gate.sh
dotnet run --project DevTests/TafTests.csproj --no-restore -v q --nologo
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py
python3 Tools/check-structure.py --report
```

## Implemented architecture boundary

- Plots reserve typed lots. They do not stand in for buildings.
- A building occupies a lot through a frozen authored map, exact size/type binding, pose,
  entrances, functional fixtures, material palette, technology minimum, and deterministic variant.
- Defence and siting are separate: only an unplotted defensive work is a cap-exempt frontier
  segment. Defensive plotted buildings retain their authored lot, category, cap cost, and base
  defence rating.
- Same-set transitions keep lot identity only through explicit directional transition records;
  retype/resize uses fresh siting and a new lot identity.
- Gatehouses use a traversable road-bound topology. Delves use paired physical travel endpoints.
- Inheritance freezes witnessed authored receipts and a connected street graph; it never carries
  items, liquids, charge, or mutable object identity between runs.
- Visual-state cues derive from real construction, staffing, wear, deprivation, and network state.
  Current runtime presentation is vanilla-art/glyph-first. Original assets—including disclosed
  generative-assisted drafts only after pixel-level human revision—are permitted through the
  provenance, rights, editable-source, wiring, fallback, package, and independent native-review
  policy in [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md).
- Food and water are separate physical flows. Dedicated vessels, water details, seeds, authored
  crop rows, physical larders, daily rations, favoured meals, mills, and cross-zone deliveries are
  implemented. A favoured meal supplies one bounded positive shade for one day; stock never grants
  an indefinite passive bonus merely by existing.

## v1 polity/world-presence boundary

The current v1 scope ships bounded inherited ruins/history, causal player-settlement raids,
purpose-specific raid/trade/guest/porter bodies, trade charters/caravans, and a conserved two-city
water manifest. It does **not** claim a living prior-realm successor polity, rival kingdom,
diplomats/emissaries, generalized visible inter-tile polity traffic, or polity-to-polity clashes.
Those brainstorming directions are positive `AUTHOR-DEFERRED` targets requiring explicit
reopening and owning adapters; exact old-actor continuation, automatic ideological war,
persistent unloaded actors, mass background simulation, and offscreen conquest/loss are
`REJECTED`. The canonical publishable disposition and evidence-owner matrix is in
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
- Every remaining limitation either repaired or explicitly moved out of v1.0 by an author ruling;
  no historical audit may be used as proof that a current gap is closed.
- Every current `SHIP` row in the v1 polity scope matrix remains truthful and proved at its stated
  boundary. `AUTHOR-DEFERRED` research/design prose must not be presented as current runtime.
- Addendum 9 structural release gate green: every staged C# file strictly under 300 physical lines,
  plus exact-inventory human evidence for one responsibility and protocols at boundaries. Current
  1307-file census is red and `docs/STRUCTURE_REVIEW.json` is missing; no enterprise-grade or v1.0
  release-quality claim is valid while it
  remains red.

Detailed current ledgers live in `_notes/BRIEF-IMPLEMENTATION-AUDIT.md` and
`_notes/CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md`. Release mechanics live in
[RELEASING.md](RELEASING.md); structural gate semantics live in [STRUCTURE.md](STRUCTURE.md).
