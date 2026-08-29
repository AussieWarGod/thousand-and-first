# Session handoff — current v1.0 test-candidate work

> **Supersession banner — 2026-08-28.** This is a chronological handoff snapshot; “current” below
> means the snapshot date, not the working tree. Current runtime holds one seat plus two non-seat
> cities (third founding succeeds, fourth refuses; manifests offer both non-seat destinations). The
> narrow physical-food landing is code-complete with integrated/native proof open. Prior hosted-
> arcology implementation claims are superseded under active AMENDED 19+ review; production,
> XML/fabric, and tests are held. Use `../docs/STATUS.md`, `../docs/V1-UNDEFERRAL.md`, and
> `BRIEF-IMPLEMENTATION-AUDIT.md` for live status.

> **Status audit: 2026-08-27.** Use `../docs/STATUS.md` for exact current evidence,
> `BRIEF-IMPLEMENTATION-AUDIT.md` and
> `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` for open contract gates,
> `../TESTING.md` for native/human proof, and `../docs/RELEASING.md` for Steam. Historical
> candidate receipts below prove only their frozen bytes.

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
