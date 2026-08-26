# Session handoff — current v1.0 test-candidate work

> **Status audit: 2026-08-27.** Use `../docs/STATUS.md` for exact current evidence,
> `BRIEF-IMPLEMENTATION-AUDIT.md` and
> `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` for open contract gates,
> `../TESTING.md` for native/human proof, and `../docs/RELEASING.md` for Steam. Historical
> candidate receipts below prove only their frozen bytes.

## Current state

- Integrated feature/polish baseline is clean commit `19fb8ee`. Later structural/CI hardening
  changes are not signed by that receipt; preserve them and all later user changes rather than
  substituting an older historical candidate.
- `Tools/gate.sh`: 1481 staged sources compile clean under baseline and compatibility
  symbols. The exact inventory contains 1481 production C# files and 251,704 physical lines and has
  SHA-256 `b4019ef667c178c9d46da4e3ad380059ea3dfe114dd69b66c7bb6cb394fbc979`.
- Architecture: 136 buildings, 126 plotted building plans, 499 maps, 355 bindings, 403 tiers,
  516 variants, and 2,064 variant/pose goldens; generator/checker clean with zero issues plus three
  expected installed-base tolerant-recovery warnings for malformed vanilla Creatures/Furniture/Items XML.
  Lot realizations are current at 337 maps, 242 bindings, and 277 tiers.
- Nine focused one-survey source-contract cases pass. The current integrated pure/source suite passes
  7,714 / 7,714 cases locally. Latest retained hosted checkpoint `b049c17` has green repository-audit,
  Ubuntu source-suite, and Windows source-suite jobs for its exact bytes; later working-tree changes
  remain unsigned. The portable suite passes 173 / 173 with zero skipped; all 34 Python tool tests
  and all 19 art tests pass. Hosted Ubuntu exposed a legacy
  publication no-replace race; an exclusive legacy-folder publication lock plus deterministic
  contention test repair it. Public CI permits exactly three named
  installed-data skips when no
  Qud base is discoverable; any extra/missing skip or explicit invalid base fails. Release
  execution forbids skips and passes the exact licensed base explicitly.
- Current inventory gates report 1,505 cold-install files, 36 shipped IPart ABI classes and 3 ABI
  contracts. Art policy allowlists 0 local tiles and verifies 55 vanilla paths.
- Current native partial result remains the receipt from clean commit `19fb8ee`: clean deployment,
  fresh isolated profile, Smokehold founding,
  141 live faction standings, 17/17 self-tests, production gallery case 1/2,064, save/cold-load,
  repeat 17/17, and clean `Player.log`, all against Qud 1.0.5/core 2.0.211.51. Full human
  gallery/lived-city, numbered protocol, accessibility, compatibility, dense-performance, and
  private subscribed-install receipts remain open. Later structural revisions have no native
  receipt and need a compile/load/log rerun against their exact final commit.
- Structure checkpoint: 40 files exceed 300 physical lines, 0 are exactly 300, 16 exceed 1,000,
  0 exceed 2,000, and 0 exceed 5,000. Direct XRL imports total 612, with 37 at or over the line
  limit. Three more oversized authorities were decomposed after `b049c17`, bringing the current
  sequence to 131 additional families and 141 cumulative. Latest fan-in preserved exact logical
  source, persisted/public ABI, deterministic order, and source-reader coverage. The structural release gate remains red until
  every staged file is strictly under 300 and exact-inventory human semantic review exists in
  `../docs/STRUCTURE_REVIEW.json` with no exceptions.
- Current focus: checkpoint each green hardening batch; decompose CentralLogistics and other
  oversized authorities; implement routed construction inputs, the two-city reciprocal purposeful
  portfolio, and Charter succession configuration; then rerun exact-revision native proof and
  complete every remaining semantic-review/human/compatibility/Steam gate.

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

## Author-deferred polity/world-presence targets

`VISION.md` owns the canonical public matrix; `V1-POLITY-SCOPE.md` is its expanded private
worksheet. Existing inherited ruins/history, causal raids, bounded current
parties, caravans, and two-city manifests ship at their implemented scope. Successor/namesake
polities, any legacy rival, generalized diplomats/emissaries, visible polity traffic, route
correspondence, and polity clashes are positive `AUTHOR-DEFERRED` targets needing explicit
reopening and owning adapters. Exact old actors, automatic ideological war, persistent unloaded
actors, mass background simulation, and offscreen conquest/loss are rejected. Balance/flavour
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
  agents told to flag rather than grow scope; deferrals recorded with reasons.
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
