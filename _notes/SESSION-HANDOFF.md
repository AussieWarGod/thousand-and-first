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

1. **THE PLAYTEST** — TESTING.md is ~36+ passes; none ever run by the author. The receipts
   (`[TAF] perf`) are readable in-game. Now doubly urgent: Addendum 22 A1 confirms Design B
   "for trial with players" — the trial needs a playable build the author has actually seen.
2. **D2 additions** — the safe-part-class wishlist audit is dispatched; the author adds
   wanted classes to it after delivery ("i can add later based on player feedback").
3. Housekeeping: the author's own Creature Control mod shipped a TEMP-DIAG logging block in
   its 17 Aug Steam update.

**Everything else RULED 2026-08-22 — Addendum 22 (the great confirmation): all 26 board
questions confirmed to recommendation.** Q6 CLOSED (Design B + capital-as-hub + no
capital stacking + crown-is-a-building). Knowledge siting ruled (registry model, all eight).
Kingdom Mode ruled (all thirteen incl. C13 seat-cost default, delegated to orchestrator and
set ON). Lab blocklist sustained; four named procedures confirmed. Int ladder load-bearing
at top tier; schooling +1 cap. Riders minted work items — see Addendum 22 tail.

## Queued waves (updated after Addendum 22 — most gates now open)

**Unblocked, proposed order**: mirror-gate FIRST (free-standing + Design B's hard
prerequisite — the commute must exist before purposeful cities ship); then strata/WR-3
(Addendum 15); research-system build (B+E rulings in); lab wave (A1+D; LAB-CLASS-AUDIT
lands the derived-catalogue v1 list); becoming annexe (A1; fiction F1-F4 as ranked);
capital/crown wave (A2-A4). Deep delve behind strata. Kingdom Mode + succession wave behind
the quest-handling research (dispatched) and SUCCESSION-RESEARCH §8's four verification
debts; the seal/cross-run wave builds on DECISIONS.md + INHERITANCE-SEAMS.md (now tracked),
C10 confirmed orthogonal. Roster wave (N cities — 12(j)); heart rungs 5+ + relocation verb
(behind strata); feel-lane remainders (companions, market, tinker services, trophies);
map follow-ups (`_notes/CLOCK-REWORK-CHANGE-MAP.md` tail): bounty denominator, SealedVigour
zero-caller, level-2 distances, refined haulage, heart follow-ups (roads TryHeart weight,
socket second-heart refusal, rite-over-water re-attempt, `Heart="yes"` attribute).

**Research in flight (2026-08-22)**: LAB-CLASS-AUDIT.md (D2 wishlist) and
QUEST-HANDLING-RESEARCH.md (C5/C6 riders) — both dispatched, land as drafts for
orchestrator review + commit.

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
