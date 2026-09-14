# Shared development workflow

This workflow applies to Codex, Claude, and human contributors. Use one implementation of
the commands below; do not reconstruct a new release driver or test procedure each session.
Read [STATUS.md](STATUS.md) for current acceptance, [../STANDARDS.md](../STANDARDS.md) for
code rules, and [RELEASING.md](RELEASING.md) when preparing a release. Historical narratives
and test counts do not establish current acceptance.

For city growth, plots, roads, support economy or multi-tile scenarios, also read
[CITY-GROWTH-BALANCE.md](CITY-GROWTH-BALANCE.md). It preserves the accepted user direction
and linked work for both agents: physical expansion around a roughly 20-resident benchmark,
three-cell roads, useful public space and layout rewards, without arbitrary population gates.
Keep the original Beta, Quickstart reliability and complex behavioral coverage goals in scope.

## Short edit loop

Choose the smallest check that exercises the change. Run it locally before pushing. Fix a
failure and rerun the affected check; do not run every suite after each edit. Unknown impact
requires broader checks, not an assumed documentation-only classification.

| Change | Development command | Additional work before acceptance |
| --- | --- | --- |
| README or release prose | `Tools/dev-check.sh docs` | Includes actual Workshop/Alpha metadata validation; inspect linked claims; required CI still runs. |
| Python or shell tool | `Tools/dev-check.sh tools 'run_personas_lifecycle_test.py'` | Select the actual related test module; syntax-check edited shell scripts. |
| C# rules or source contracts | `Tools/dev-check.sh main KingdomQuickstart` | Select the relevant fixture or method substring; include portable checks if that kernel changes. |
| Portable C# kernel | `Tools/dev-check.sh portable KingdomQuickstart` | Full suites at integration. |
| Repository-wide tooling | `Tools/dev-check.sh audit` | Required CI and any affected licensed or native checks. |
| Full local licensed source validation | `Tools/dev-check.sh licensed` | Both full suites, serially, with zero skips; does not replace Windows release or in-game gates. |
| Gameplay, persistence or engine integration | Focused C# checks, then `Tools/gate.sh` | Relevant real in-game behavioral scenarios, including complex failure/recovery and save/load where applicable. |

The examples are selectors, not a universal test list. The helper refuses empty C# selectors,
no-match tool patterns, and skipped selected C# cases. C# selection is a case-insensitive
substring; inspect the reported selected count. Focused results never count as full-suite PASS.
For public contributors without licensed data, select engine-free cases; installed-data cases
belong in the existing licensed lane, not a new skip override.

Use the exact SDK pinned by `global.json`. On the maintainer's WSL host:

```bash
TAF_DOTNET=/home/r/.dotnet/dotnet Tools/dev-check.sh main KingdomQuickstart
```

Prefer native Linux .NET for focused source checks on a Linux checkout. Windows accesses to
`\\wsl.localhost` can be expensive. This preference does not replace the release's Windows
engine compiles or hosted platform checks. Set `TAF_QUD_BASE` to the actual licensed Base
directory when needed.
For a full local licensed integration check, use `Tools/dev-check.sh licensed` with that SDK
and installed data. It refuses an ambient test filter, restores and runs both projects serially,
and stops at the first failure. This makes the same fast local route available to both agents;
the release source-test step uses this same command with the configured licensed Base directory.
It defaults to `$HOME/.dotnet/dotnet`; set `TAF_DOTNET` when the pinned SDK is installed elsewhere.
Keep restore beside its matching run. Never overlap .NET builds in one checkout or run this
helper against a checkout being tested by another process. Use an isolated worktree for
independent work. Never overlap native game scenarios or compete with the release Steam host.

## Integration and evidence

After the focused checks pass and the change is coherent, push once and let required CI run.
Do useful independent work while remote checks run. Do not duplicate a passing CI suite locally
without a reason such as licensed data, platform coverage, changed inputs, or investigating a
failure. Record the reason when a costly test needs repeating.

Keep existing automated integration and release gates. This document does not implement a
documentation-only CI shortcut or authorize bypassing protection. Optimize those separately
with tests proving their change classification and failure propagation.

Reuse accepted native evidence only when its recorded runtime, harness, scenario/options,
engine and validator bindings remain applicable. Compare the actual inputs, not commit labels
or test counts. Describe it as evidence from the original run on identical inputs; never claim
a new run. Changed inputs require the relevant scenarios again. New behavior needs a real
behavioral test, not only source-text assertions. Disclose synthetic setup and untested cases.
Preserve failed evidence, strict final logs, complete seals, and exact owned shutdown records.
Restore borrowed identities and ownership before destroying synthetic fault objects: native
graveyards retain tombstones. Prove production recovery before and after such probes; a live
object census alone cannot prove cleanup.

Before a long city scenario, check fixture calls against their production contracts. Authored
housing needs the normal plot-crowding and ground checks before typed plot-payload preflight;
a custody-only survey has no populated civic lists.
Use the existing local-operation survey scope when settlement APIs share physical-benefit reads,
and prove its disposal. Account for pending paid construction before supplying exact materials:
a newly finished building can spend them on its own next improvement. Keep unrelated test plots
and their reserved lanes outside the largest heart footprint being tested. Run available read-only
preflights before long waits, and retain both the verdict and detailed production reason on refusal.
These checks apply to both agents; they do not replace observing the actual paid transition.
For long population fixtures, preflight the production subsidence equilibrium at the highest tested
stage. Stored water and bed counts do not prove civic water support. Disclose any synthetic producer
roots. Verify founding recovery immediately after paying the next heart improvement, before spending
its full construction wait; a replaced receipt marker can block the whole construction lane.
Fixture placement must reserve the full persisted footprint of legacy works, even when only their
root object is synthetic. A bare cell is insufficient for a 12-by-9 air-well field. Check spatial
capture as well as subsidence support before a long wait. Prove the ordinary city book already
contains every fixture work; capturing an old smaller book can miss newly misplaced objects.
Retain the actual bound assessment's reason;
a later Ready result cannot explain an earlier NoGroundToGrow refusal.
When a shared failure sentence covers several predicates, record their actual boundary values and
caller before another long run. Journal unexpected production refusals immediately to avoid spending
the remainder of a construction wait on a job already requiring inspection.
Commissioning clearance does not prove completion-time clearance: resident movement during the
construction wait can repopulate future wall cells. Place the late resident at the actual paid
handover boundary, test protected occupants and failed displacement, and require the resulting
clearance and retry witnesses in the persona. Do not count an unrelated natural retry as a
controlled fault test or remove real residents merely to make a long fixture complete.
Validate changed personas with `python3 Tools/personas/persona_matrix.py fields PATH` before
native preparation. New observation rows need explicit host grammar and positional expectations;
test missing, duplicate and refused evidence. Repeated diagnostics may be non-positional only
when their failures still stop acceptance and required behavioral witnesses remain mandatory.
Scope repeated physical reads across the complete operation and verify disposal; component-by-component
unbound surveys can dominate runtime. Record actual timings before claiming a performance gain.
Reserved plot margins are not the whole entrance route: authored lane endpoints extend one cell
beyond them. Preserve the complete approaches to paid work and future expansions. Recheck physical
ingress after all fixture buildings and stores are placed, before costly enrollment or turn waits.
Before waiting through a paid upgrade chain, compare each authored delta's added material kinds
with its source upgrade bill across the tested facings and historical baselines. Include all
non-natural, non-existing-authority additions, not only pieces changed by the current feature.
Compare the complete authored snapshot plus plot wrapper against the construction payload cap,
not only the inner architecture codec cap. Include registry round trips before a long native chain.
Run the main-only `KingdomCampHeartTests` fixture for heart catalogue edits; an empty portable
selector is a failed check, not portable coverage. Keep native exact-payment expectations aligned
with the authored bill and retain the production material-claim refusal.
If work reaches its final stage but refuses physical completion, inspect the recorded construction
reason before increasing the wait. A blocked entrance does not improve with more time.
Readiness observations must use production staffing and competing-work inputs, not the fixture's
initial population and an assumed empty queue. Keep the last production announcement and relevant
ledger reason when a ready-looking action never starts. Explicitly identify synthetic stores before
expecting construction-input observations to reference them.
A cropped quiet-window capture cannot exclude a blocking prompt. Use the existing full native
window capture when the view is incomplete; never infer a completed turn from process responsiveness.
Preserve a changed profile as invalid evidence with expected and actual inventories, not a new seal.
Check the sealed options file early during a long native run. If it changes, retain the difference
and stop before further expensive setup: that run cannot establish acceptance. Input can reach the
game from a connected controller even while the script spends the founder's turns. A newly written
look option is not proof of harmless initialization; trace its writer before changing sealed defaults.
The paid-chain persona isolates `GameManager.UpdateInput` for its exact dedicated game through owned
shutdown. It verifies the actual patched call before its first setup step. This is disclosed test
isolation, not coverage of keyboard/controller interaction; ordinary games and other personas retain
their input path. Do not disable physical devices or change the operator's global input settings.

Use `Tools/scenario_advance_check.py` for guarded ordinary-wait accounting. The engine completes
on the next player action opportunity, so 1201 actual turns for 1200 requested is valid. Keep the
actual elapsed count; require the exact requested sequence, complete progress, paired founder guards,
and (for the paid chain) matching `Game.Turns` observations. The checker supplements the persona,
profile seals, owned shutdown and strict logs; its PASS alone is not native acceptance:

```bash
python3 Tools/scenario_advance_check.py /path/to/scenario-journal.tsv \
  --requested 1200 3600 1200 1200 1200 7200 1200 6600 6600 1200 \
  --chain-clocks --results /fresh/path/to/wait-accounting.json
```

## Release batch

Prepare version constants, manifest, Workshop lane, README, changelog and ledger together.
Run `Tools/dev-check.sh docs` plus the applicable metadata validator before the PR. Preserve
the private receipt, candidate lineage and frozen inputs required by the Alpha lane; do not
change TESTING.md between private binding and public promotion. Finish the existing tagged
workflow through verification and finalization. Do not restart or resubmit because output is
quiet; inspect the existing process or workflow handle. No manual test gate is required.

The shared `docs` command invokes the existing private/public Alpha metadata validators,
including the exact README status and candidate record. Public 0.3.5 was refused before upload
because a wording fix passed general documentation checks but broke the Alpha contract. Keep
the canonical status line and explain the package target/publication distinction beside it.
After any later release-document edit, rerun this fast preflight before tagging. This preflight
does not prove package reproduction, ancestry, subscribed delivery or final release acceptance.

Profile slow commands before changing release execution. Record wall time, platform, input
hashes, case identities, skips and exit status. Optimize duplicate work or filesystem access;
do not infer equivalent coverage from equal case counts. Stage performance changes separately
from an urgent release candidate.

Scenario profile inventory uses the shared `Tools/scenario_profile.py` reader with at most four
files outstanding. It retains every normalized path, SHA-256 and link/type check, and joins
sibling readers before propagating a failure. Keep source and destination inventories at their
existing validation boundaries; faster hashing does not permit skipping a reproof. Both agents
use this automatically through the existing preparation and verification commands.

The native source-test route was selected after comparing evaluated Linux and Windows inputs:
both projects have identical source lists, compiler symbols and target framework, and every
source byte matches the accepted Windows private 0.3.5 run. Both full licensed suites passed
on native Linux in 49 seconds with zero skips. Hosted Windows suites, installed engine binding,
all four Windows engine compile modes and native behavioral scenarios retain their own gates.
This is a change to where engine-free source tests run, not evidence of native gameplay.
Input comparison: `tooling/licensed-source-parity/917bb79d/result.json`, SHA-256
`c60dcbaf8890ae1e0a5f25d10ffc35ab7ba7cb58d56c9ee48dccb73198aa2dfd` in the local evidence archive.
It records all 2346 main and 1444 portable compiler inputs individually, rather than only counts.
The subsequent test-only change updates the release-source contract to require the native Base
handoff and shared full-suite command; gameplay test inputs and discovery rules are unchanged.

## Handoff between agents

Read and update the shared local note at the following path (the common Git directory makes
it available to every worktree without creating documentation commits for status updates):

```bash
printf '%s/taf-workstate.md\n' "$(git rev-parse --path-format=absolute --git-common-dir)"
```

Keep that note short, with: objective and priority; branch/worktree and exact head;
changed behavior; completed checks and evidence paths; outstanding failures/gaps; active process
or workflow IDs and ownership records; next concrete action. Update it before switching agents.
Inspect those authoritative handles first on resumption. Do not restart work from historical
notes, rerun a completed gate, or ask again for an authorization already recorded in the session.
