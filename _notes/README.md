# _notes — private working knowledge base

**Gitignored. Not published.** This is the reasoning behind the repo: research digests, ruling
records, current acceptance ledgers, historical attacks, and session handoffs. Public-facing docs
live in the repo root and `docs/`.

## Freshness and authority

Read current status in this order:

1. `../README.md` and `../docs/STATUS.md` — public scope, exact automated evidence, and open gates.
2. `../VISION.md` § “Canonical v1 polity scope matrix” — publishable `SHIP` /
   `AUTHOR-DEFERRED` / `REJECTED` disposition; `V1-POLITY-SCOPE.md` is its expanded private
   evidence/reopening worksheet.
3. `BRIEF-IMPLEMENTATION-AUDIT.md` — current brief-to-runtime acceptance matrix.
4. `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` — current high-risk runtime closure ledger.
5. `ARCHITECTURE-POLISH-CONTRACT.md` and `../TESTING.md` — visual/building law and executable
   native acceptance protocol.
6. `COORDINATION.md` — only the present work lane and handoff.

Files explicitly labelled **historical**, **research**, **review**, **draft**, **plan**, or
**superseded** retain the claims and line references that were true when written. They are attack
material, not current implementation status. Never repair history by rewriting its original
finding; add a current disposition to one of the ledgers above and link it. Undated status language
in an authoritative/current document is a defect.

## Contents

| File | What it is |
|---|---|
| `COORDINATION.md` | **Read first.** Claude ↔ Codex handoff: lanes, work in flight, open questions, recent exchanges |
| `BUILDING-CATALOGUE-BRIEF.md` | **The ruling record.** Author-approved design law for plots, materials, the catalogue, and every addendum since (layers/sockets, typed plots, the upgrade trigger law, the QoL vocabulary, housing binds, the closeness ladder and fault-line ceiling, creed conversion, reach, the material chain) plus the BACKLOG. Where it and an older note disagree, it wins. |
| `ARCHITECTURE-POLISH-CONTRACT.md` | Current authored-lot, architecture, material, furnishing, visual-state, gallery, and native acceptance law. |
| `V1-POLITY-SCOPE.md` | Expanded evidence/reopening worksheet for `../VISION.md`'s canonical public polity matrix. Every prior-kingdom, successor, rival, war, party, route, clash, and food/relationship idea retains an explicit disposition and owner. |
| `BRIEF-IMPLEMENTATION-AUDIT.md` | Current player-visible promise-to-code matrix. A code/content repair remains separate from native/human acceptance. |
| `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` | Current high-risk foundation/runtime gap ledger and remaining evidence gates. |
| `LIVING-CITY-ARCHITECTURE.md` | Adopted city-state, boundedness, physical-authority, happening, logistics, and extension architecture. |
| `CODEX-VALIDATION.md` | Codex's audit ledger and the pinned engine provenance (DLL hash, build ID) |
| `SESSION-LOG.md` | Blow-by-blow rundown of what was done, in order, with checkpoints to verify |
| `DECISIONS.md` | The decision ledger: every significant call, why, and what would reverse it |
| `QUESTION-BACKLOG.md` | Decisions still needing human taste/account authority plus historical provisionals; current engineering state belongs in the acceptance ledgers. |
| `RESEARCH.md` | Historical early research digest; useful hypotheses and engine notes, not current status or final comparable authority. |
| `RESEARCH-RERUN.md` and `COMPARABLES-RERUN.md` | Later corrections and rerun evidence. Read these before relying on an early comparable claim. |
| `POLITY-RECONCILIATION-FINAL-R3.md` and `POLITY-EXPANSION-RECONCILIATION.md` | Polity discovery/reconciliation inputs; `V1-POLITY-SCOPE.md` owns the current implementation disposition. |
| `FOOD-WATER-FINAL-REVIEW.md` | Historical research disposition; later author Addendum 11, current API/TESTING, and the brief audit own the shipped food/water mechanism. |
| `AGENT-PLAYBOOK.md` | Every investigation run, with prompts to repeat or extend them |
| `EXPOSURE.md` | What is public vs private, and why |
| `design/thousand-and-first.html` | The full design document (also published as a private artifact) |
| `design/skeleton.md` | The original pre-research design skeleton |

## For a reviewing model

Start with `COORDINATION.md` — it tells you what the other agent is touching right now, so you
don't spend a pass rediscovering a fix that landed an hour ago. Then `SESSION-LOG.md` (what
happened), `DECISIONS.md` (why), then the code.
`RESEARCH.md` is the early digest. Read its correction header, then the named reruns and current
scope/acceptance ledgers before treating a finding as authoritative. `AGENT-PLAYBOOK.md` lets you
re-run or extend an investigation rather than taking its conclusions on trust.

**The single most important evidence boundary:** portable and installed-assembly compilation,
pure/source tests, generators, and static architecture checks do not sign native behavior or human
appearance. The structural hardening revisions have no complete current-revision native protocol receipt.
Its current hardening checkpoint has 7,612 / 7,612 full and 171 / 171 portable cases with zero
skipped, 31 passing Python tool tests, 19 passing art tests, clean baseline/compatibility compilation, and unchanged
architecture evidence: 136 buildings, 126 plotted plans, 499 maps, 355 bindings, 403 tiers, 516
variants, and 2,064 goldens with zero issues plus three expected malformed-vanilla recovery
warnings. Lot realizations are current at 337 maps / 242 bindings / 277 tiers. Cold-install
inventory is 787 files; IPart ABI is 36 shipped classes / 3 contracts; art policy allowlists 0 local
tiles and verifies 55 vanilla paths. Its exact 763-source/242,999-line structural inventory is
`ce7e3de4e59985e4a8f2e12d85a54be89b19111b34e45639145e3159892df591`: 122 files exceed 300
physical lines, 0 are exactly 300, 56 exceed 1,000, 14 exceed 2,000, and 2 exceed 5,000; 191 files
import XRL directly, 82 at or over the line limit. This wave decomposed 49 additional oversized
authorities, 59 cumulative. Fan-in repaired the Itinerary method boundary, namespace wrappers in
Delve, Brink, Crews, and Stations, and two stale source readers before checkpoint. Exact-inventory
semantic review is still open. Clean commit `19fb8ee`
has the narrow founding/single-gallery/persistence result recorded
in `../TESTING.md`; the structural tree still needs a native rerun against its exact final commit. An
earlier revision also has a historical two-city loader/persistence receipt. None is the complete
protocol. Human gallery/usability, compatibility, dense-performance, exact-inventory semantic
review, structural release, and Steam subscription gates remain open; no
`../docs/STRUCTURE_REVIEW.json` exists. `../docs/STATUS.md` owns the exact latest command
results and unsigned gates.

## Environment

- **Live mod (the game loads this):** `C:\Users\Reegan\AppData\LocalLow\Freehold Games\CavesOfQud\Mods\ThousandAndFirst`
- **WSL clone (editing):** `/home/r/work/thousand-and-first` — not auto-synced. Use the verified
  `Tools/stage.sh deploy` workflow to update the live Mods copy; `git push` updates only the public
  remote and never deploys into Qud.
- **Public repo:** https://github.com/AussieWarGod/thousand-and-first
- **Decompiled game source (ground truth):** `/home/r/coq/qud_helper/game_base/decompiled/2.0.211.51-ilspy9.1/` — the durable archive (5,437 files; `PROVENANCE.md` inside records the DLL SHA and decompiler). **Cite this path, not a scratchpad copy.** The sibling `6000.0.41.4645959` tree is older (5,368 files) and must not be cited for 2.0.211.51 claims.
- **Game data XML:** `F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base`
- **Session transcript:** `C:\Users\Reegan\.claude\projects\C--Users-Reegan\3843e46d-4eb8-42bc-8816-206a195ad8f5.jsonl` (~14 MB)
- **Build gate:** `DevTests/build.ps1` · **Tests:** `DevTests/test.ps1` (both Windows-side; they need the game's Managed DLLs)
