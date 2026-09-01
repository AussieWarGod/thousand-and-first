# _notes — private working knowledge base

**Gitignored. Not published.** This is the reasoning behind the repo: research digests, ruling
records, current acceptance ledgers, historical attacks, and session handoffs. Read
`COORDINATION.md` for current work; `COORDINATION.md.original.md` is the preserved pre-2026-09-01
archive and should not be ingested by default. Public-facing docs live in the repo root and `docs/`.

## Freshness and authority

Read current status in this order:

1. `COORDINATION.md` — live goal, ownership, blockers, and current evidence.
2. `../README.md` and `../docs/STATUS.md` — public scope, exact automated evidence, and open gates.
3. `../docs/V1-UNDEFERRAL.md` — live index for every reopened positive implementation
   and evidence gap. `../VISION.md` owns the Canonical v1 polity scope matrix and publishable
   `SHIP` versus hard `REJECTED` disposition; `V1-POLITY-SCOPE.md` is its expanded private
   evidence/reopening worksheet and
   `POLITY-ACTIVATION-RECONCILIATION-2026-08-27.md` owns the exact polity consumer diff.
4. `BRIEF-IMPLEMENTATION-AUDIT.md` — current brief-to-runtime acceptance matrix.
5. `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` — dated reconciliation/attack ledger; its top
   supersession banner controls later status conflicts.
6. `ARCHITECTURE-POLISH-CONTRACT.md` and `../TESTING.md` — accepted visual/building law and the
   current executable native acceptance protocol; implementation status comes from the ledgers above.

Files explicitly labelled **historical**, **research**, **review**, **draft**, **plan**, or
**superseded** retain the claims and line references that were true when written. They are attack
material, not current implementation status. Never repair history by rewriting its original
finding; add a current disposition to one of the ledgers above and link it. Undated status language
in an authoritative/current document is a defect.

For chronological notes, the dated supersession banner at the top controls current interpretation;
preserve the historical body rather than deleting or silently modernizing its old claims.

## Contents

| File | What it is |
|---|---|
| `COORDINATION.md` | **Read first.** Claude ↔ Codex handoff: lanes, work in flight, open questions, recent exchanges |
| `BUILDING-CATALOGUE-BRIEF.md` | **The ruling record.** Author-approved design law for plots, materials, the catalogue, and every addendum since (layers/sockets, typed plots, the upgrade trigger law, the QoL vocabulary, housing binds, the closeness ladder and fault-line ceiling, creed conversion, reach, the material chain) plus the BACKLOG. Where it and an older note disagree, it wins. |
| `ARCHITECTURE-POLISH-CONTRACT.md` | Accepted authored-lot, architecture, material, furnishing, visual-state, gallery, and native acceptance law; its top banner defers implementation status to current ledgers. |
| `FIXTURE-POSE-CENSUS-2026-09-01.md` | Current effective-render and pose census: one canonical building map, runtime aspect rotation, vanilla fixed-screen fixture policy, connected-wall set, corrected semantic art substitutions, and the exact native taste queue. |
| `RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md` | Current source-complete comparable/mod/Qud-lore disposition crosswalk: each researched direction is implemented with its evidence boundary, expressly rejected with rationale, or retained as an honest native/human/release gate. |
| `../docs/V1-UNDEFERRAL.md` | Live closure index after the author reopened every positive direction; hard rejects and external manual gates remain distinct. |
| `V1-POLITY-SCOPE.md` | Expanded evidence/reopening worksheet for `../VISION.md`'s canonical public polity matrix. Every prior-kingdom, successor, rival, war, party, route, clash, and food/relationship idea retains an explicit disposition and owner. |
| `POLITY-ACTIVATION-RECONCILIATION-2026-08-27.md` | Current polity foundation ledger: what semantic authority exists, every exact remaining runtime consumer, hard rejected shapes, and the evidence boundary after all positive polity scope reopened. |
| `BRIEF-IMPLEMENTATION-AUDIT.md` | Current player-visible promise-to-code matrix. A code/content repair remains separate from native/human acceptance. |
| `CONTRACT-RUNTIME-RECONCILIATION-2026-08-25.md` | Dated high-risk foundation/runtime reconciliation and attack ledger; current deltas live in its top supersession banner and the public status ledger. |
| `SESSION-HANDOFF.md` | Frozen chronological handoff; never use its “current” snapshot wording as present working-tree truth. |
| `LIVING-CITY-ARCHITECTURE.md` | Adopted city-state, boundedness, physical-authority, happening, logistics, and extension architecture. |
| `CODEX-VALIDATION.md` | Codex's audit ledger and the pinned engine provenance (DLL hash, build ID) |
| `SESSION-LOG.md` | Blow-by-blow rundown of what was done, in order, with checkpoints to verify |
| `DECISIONS.md` | The decision ledger: every significant call, why, and what would reverse it |
| `QUESTION-BACKLOG.md` | Decisions still needing human taste/account authority plus historical provisionals; current engineering state belongs in the acceptance ledgers. |
| `RESEARCH.md` | Historical early research digest; useful hypotheses and engine notes, not current status or final comparable authority. |
| `RESEARCH-RERUN.md` and `COMPARABLES-RERUN.md` | Later corrections and rerun evidence. Read these before relying on an early comparable claim. |
| `POLITY-RECONCILIATION-FINAL-R3.md` and `POLITY-EXPANSION-RECONCILIATION.md` | Historical polity discovery/reconciliation inputs; the activation reconciliation and `V1-POLITY-SCOPE.md` own current disposition. |
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

**Frozen evidence snapshot (2026-08-27; not current counts).** The single most important evidence boundary: portable and installed-assembly compilation,
pure/source tests, generators, and static architecture checks do not sign native behavior or human
appearance. The structural hardening revisions have no complete current-revision native protocol receipt.
That snapshot's full suite passed 7,743 / 7,743 cases locally. Latest retained hosted checkpoint
`d285129` has green repository-audit, Ubuntu source-suite, and Windows source-suite jobs for its
exact bytes; later working-tree changes remain unsigned. The portable suite remains 173 / 173 with
zero skipped, with 35 passing Python tool tests, 19 passing art
tests, clean baseline/compatibility compilation, and unchanged
architecture evidence: 136 buildings, 126 plotted plans, 499 maps, 355 bindings, 403 tiers, 516
variants, and 2,064 goldens with zero issues plus three expected malformed-vanilla recovery
warnings. Lot realizations in that snapshot were 337 maps / 242 bindings / 277 tiers. Cold-install
inventory is 1,599 files; IPart ABI is 36 shipped classes / 3 contracts; art policy allowlists 0 local
tiles and verifies 55 vanilla paths. Its exact 1,575-source/252,982-line structural inventory is
`736bb6fa198a3ed599ddc51302ffeccf7be0c01e7839cd9fbb7d11c9e79c1822`: 27 files exceed 300
physical lines, 0 are exactly 300, 3 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000; 689 files
import XRL directly, 25 at or over the line limit. Thirteen more oversized authorities were decomposed
after hosted checkpoint `1c2d619`, bringing the then-current hardening sequence to 144 additional families and 154 cumulative.
Fan-in preserved exact logical source bodies, persisted/public ABI, namespace wrappers, and source-reader
families. Hosted Ubuntu exposed a legacy-publication no-replace race; an exclusive legacy-folder
publication lock and deterministic contention test repair it. Exact-inventory
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
