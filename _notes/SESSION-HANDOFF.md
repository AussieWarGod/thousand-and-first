# Session handoff — the working covenant

> For the next session continuing this work. Read this + `_notes/BUILDING-CATALOGUE-BRIEF.md`
> (the ruling record, Addenda 1–20 — THE authority) before doing anything. This file is how the
> orchestrator behaves; the brief is what has been decided.

## Where the work stands (2026-08-22, HEAD 1a7025d)

- **Everything ships green**: 148 staged sources, **5,343 tests**, gate + both Art checkers +
  `_notes/balance-sim.py` all clean at every commit. Live install deployed at 168 files
  (receipt: `Tools/last-deploy-receipt.txt`). Suite grew 3,710 → 5,343 across the session.
- **Complete and deployed**: the clock rework (P1–P4, Addendum 8 doctrine end to end);
  coverage waves A–C; grounding waves G1–G3 (water/seeds/meals per Addendum 11); the
  Addendum 10 wave (brinks, typed wear, ruins); the heart wave (rungs 1–4, ghost survey,
  yielding); the FULL living-city arc W0–W7 (executor seam, city book, identity registry,
  materialisation + porters, happenings, engagement + public API v1, production one-accounting,
  networks); the styles + creed-gate wave.
- **Design corpus tracked in git** under `_notes/` (gitignore whitelist): the brief,
  VANILLA-PRODUCTION-TRUTH, LIVING-CITY-ARCHITECTURE, CLOCK-REWORK-CHANGE-MAP,
  COVERAGE-GAP-MAP, EVOLVING-HEART-RESEARCH, DIVERSITY-AND-TECH-TREES, RESEARCH-SYSTEM-DESIGN,
  END-STATE-CITIES-RESEARCH, CODEX-ENGINE-TRUTH-BATCH-1, balance-sim.py + output.

## Open on the author

1. **Q6 final ruling** — end-state megastructures. Research recommends **Design B** (flesh-city
   / chrome-city / arcology-capital, one purposeful megastructure per city, capital gets
   extras — the author's own capital ruling) with the mirror-gate as hard prerequisite (the
   Anno-vs-Fallout4 commute variable) and cross-megastructure dependency against the
   Songs-of-Syx saturation ceiling. Capital designation: the crown is a BUILDING, movable at
   real cost (never name it `Seat` — taken, KingdomSystem.cs:674). Chrome annexe fiction
   ranked: F1 the registry (IsTrueKinEvent — True Kin is a matter of record) + F2 the
   unspendable wedge, staffed by F3 the psyberneticist, F4 Mechanimist debt as friction.
2. **Research-design's three questions**: per-realm vs per-city research (hold against Q6's
   kingdom shape); how hard the Int ladder bites (are wild savants load-bearing or flavour);
   may `schooling` raise the Int cap (+1 proposed).
3. **Q8 deferred**: the four named lab procedures await author review (DIVERSITY §3.7).
4. **THE PLAYTEST** — TESTING.md is ~36+ passes; none ever run by the author. The receipts
   (`[TAF] perf`) are readable in-game.
5. Housekeeping: the author's own Creature Control mod shipped a TEMP-DIAG logging block in
   its 17 Aug Steam update.

## Queued waves (prerequisites noted, no order among exotics — Addendum 20)

Research-system build (behind Q-answers above); strata/WR-3 wave (Addendum 15 —
deep/arcology sets, `Strata` home+share-tags, `Sited="gate"`); deep delve; mirror-gate
(free-standing, now also Design B's prerequisite); the lab (behind Q6 + Q8); becoming annexe
(behind Q6); capital/crown wave; roster wave (N cities — 12(j)); heart rungs 5+ + relocation
verb (behind strata); feel-lane remainders (companions, market, tinker services, trophies);
map follow-ups (`_notes/CLOCK-REWORK-CHANGE-MAP.md` tail): bounty denominator, SealedVigour
zero-caller, level-2 distances, refined haulage, heart follow-ups (roads TryHeart weight,
socket second-heart refusal, rite-over-water re-attempt, `Heart="yes"` attribute).

## The working method (this is the "behaviour and attitude")

- **Wave protocol**: dispatch opus agents (background) with DISJOINT file ownership and
  spec-first briefs citing the brief's addenda by number; agents return wiring_requests for
  files they don't own; orchestrator integrates, then INDEPENDENTLY verifies (gate.sh, full
  suite via `cd /mnt/c && powershell.exe ... DevTests/test.ps1`, both Art checkers,
  balance-sim) before every commit; explicit-path staging (`git status --porcelain | awk | xargs git add`,
  NEVER `git add -A` while agents write); commit prose in the repo's own literary voice;
  `Tools/stage.sh deploy --apply` after each wave.
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

- Suite runs via PowerShell from /mnt/c (WSL). Deploy backs up automatically. `_notes/` is
  gitignored EXCEPT the whitelist — new research docs get an explicit `!` exception.
- Reddit: WebFetch/WebSearch are blocked. The author installed a Reddit MCP server — in a new
  session check ToolSearch for reddit tools and use the MCP route (it was not yet visible in
  the old session). Do NOT curl-scrape reddit with spoofed User-Agents — the MCP server is the
  sanctioned route. END-STATE-CITIES-RESEARCH's biggest named gap (Reddit sentiment on the
  chrome boundary) is the first thing worth re-running through it.
