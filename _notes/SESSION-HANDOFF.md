# Session handoff — 0.2.0 release candidate

> **Status audit: 2026-08-25.** The old “in flight” and “still queued” wave list was stale and
> has been removed. Use `README.md` for product status, `TESTING.md` for observed proof, and
> `docs/RELEASING.md` for the Steam boundary. The design corpus under `_notes/` remains a ruling
> record, not an instruction to rerun old waves.

## Current state

- Runtime candidate: `99133f6a1b24f3be652903e16576ddd7bb929230`.
- Release/provenance candidate: `ba3e49ed174c243917ecf5865e6c6dc1533d402d`.
- Exact gates: 6,873 native cases, 171 portable cases, 207 staged sources, 36 shipped IPart
  ABI classes, 11 art tests, 43 verified vanilla tile paths, and 222 cold-install files.
- Automated live Qud 1.0.5/core 2.0.211.51 proof: fresh two-city founding, three saves, two cold
  reloads, seat movement both ways, 109 carried fields, 17/17 in-game checks, and clean logs.
  A second isolated launch of the release/provenance candidate founded a realm, passed 17/17,
  saved, and passed resume validation. This is automation, not manual playtesting.
- A verified private-bootstrap package was produced for the release/provenance candidate. Because
  `TESTING.md` changed during the final handoff audit, rebuild once from the resulting clean commit
  before private Workshop upload; record that exact path and receipt in the release run.
- The normal local Mods copy is an exact empty-diff deployment. Twenty-eight older scanned backup
  copies were moved intact under `CavesOfQud/TAF-ModBackups/`; none were deleted. The deploy itself
  made another automatic full backup named in `Tools/last-deploy-receipt.txt`.
- Art boundary: no bundled runtime raster sprites. All shipped XML art is verified vanilla or an
  intentional glyph. Root `preview.png` is a pixel-exact, native-resolution gameplay crop with a
  repository provenance record.
- Remote publication remains pending: the release-candidate commits have not been pushed to
  `origin/main`; pushing is an external publication action requiring explicit maintainer authority.

## Human-only gates

1. Run the observational protocol in `TESTING.md`, especially Pass 36 succession/corpse/seal.
2. In Qud's signed-in Workshop UI, create the item privately, accept any agreement, and keep the
   returned `workshop.json`.
3. Freeze the private package, remove local duplicates, subscribe from Steam, verify installed
   bytes/logs, and author the truthful release evidence described in `docs/RELEASING.md`.
4. Only then canonicalize public metadata, tag `v0.2.0`, publish, and subscription-verify public
   bytes. Never infer a manual or Steam pass from automation.

## Deferred after playtest, not release blockers

The v0 boundary deliberately excludes autonomous rival polities, prior-realm citizens, world-map
warbands, and offscreen war. Balance/flavour questions still worth real play data include Swarmer
timing, deep-crop flavour, research gate depth, annexe/removal prices, capital costs, and inherited-
site discoverability. `_notes/QUESTION-BACKLOG.md` records their history; its provisionals are the
adopted 0.2 defaults unless a new issue reopens one.

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
