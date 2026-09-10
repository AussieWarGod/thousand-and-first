# Genuine Quickstart boot and save/cold-load checks

These developer-only checks select the real `KingdomQuickstart` mode and its production starting
location. They do not select `TAFScenario`, run `KingdomScenarioAutoRunner`, strip the zone,
advance a synthetic receipt, invent a heart rung, or call the bootstrap a second time.

## Recorded execution and current scope

On 2026-09-07, retained6010 sources passed genuine `marsh yes` and `marsh no`, seed `#43101`,
through boot, actual SaveGame and separate cold load. Strict result checks, exact-owned stops
and idle checks pass. `canyon yes` refused before saving on valid `Garbage` at `(28,10)`.
A post-builder Rusty population is a possible origin, not a witnessed selection or proved cause.
The initial cold-load checker did not recognize the real exact enabled-mod list token; the
narrow recognition fix preserves all diagnostic scans, and both load checks now pass. Earlier
refusals remain retained. [Execution evidence](/tmp/taf-quickstart-native.sYl4Dz/README.md).

The current replacement runs one camp after full `GetZone` completion, before founder placement,
and refreshes only the native reachability cache. That checkpoint passed four compile modes+ABI,
13602/4989 managed cases and501 Tools tests, but canyon still refused normal founding's ingress
lane. Current correction includes missing endpoints `(40,16)` and `(41,16)` in preparation,
readiness and relocation exclusion; shipped-layout and exhaustive mask regressions are added.
Current compile21734 passes all four modes+ABI; managed21635 passes13603 Taf/4990 Portable,
zero skips, and Tools69769 passes501 tests. All six current selections at seed `#43101` passed
actual boot/save/cold-load milestones with unchanged heart/stock/IDs/clocks and no replay.
All six strict save checks and six strict load file checks pass by11:25UTC. All owned stops and
independent idle checks pass. Earlier native pairs are not
reused to sign these bytes. Ordinary play, graceful Quit, Steam delivery and QSB2–5 are not
closed. [Current status](../docs/STATUS.md).

| Location / advisor | Boot and save profile | Separate cold-load profile | Strict save / load |
|---|---|---|---|
| canyon / yes | `vrVt20` | `xQForF` | PASS / PASS |
| dunes / yes | `aQWw2S` | `qUJ2Qp` | PASS / PASS |
| canyon / no | `ETnPK2` | `kU3dbE` | PASS / PASS |
| dunes / no | `plhYgy` | `rgg8XJ` | PASS / PASS |
| marsh / yes | `PuTS15` | `FisZj9` | PASS / PASS |
| marsh / no | `n021IW` | `bUZ7Pr` | PASS / PASS |

Each profile remains at `/mnt/c/taf-scenario.<suffix>`, with its separate seal. Exact journals,
logs, save hashes, process-ownership/stop evidence and checker verdicts remain in the linked
execution evidence. All3158 source-profile C# inputs match the actual developer compatibility
compiler inputs. Later documentation edits are outside that C# proof: this is not an exact final
Workshop-package receipt or a multiple-seed gameplay acceptance claim.

## Prepare and run

First pass the canonical compile gate. Prepare a fresh dedicated profile for each combination of
`marsh|canyon|dunes` and `yes|no` advisor selection. For example:

```bash
TAF_REQUEST=founding-first-city \
TAF_SCENARIO_SCRIPT='quickstart-boot marsh yes' \
TAF_SCENARIO_QUICKSTART_ADVISOR=yes \
Tools/prepare-scenario.sh '' '#4242'
```

The explicit advisor override must match the command and generated `PlayerOptions.json`; both
are sealed before launch. The seed comes from the existing frozen descriptor, but that
descriptor's scenario request is not published into the real Quickstart game. Use the launcher
command printed by preparation only after the current game is safely stopped. Never close an
unapproved user session or reuse an already spent profile. Retain the executed profiles above;
prepare new ones for changed source or observer bytes.

The existing developer menu autostart selects the real mode, applies the ordinary pregen and
frozen seed, and chooses the actual production location after the last creation window.
Popup suppression is scoped to this exact boot and restored in a finalizer. The production
world extension, camp builder, bootstrap and all other boot handlers execute normally.

## Required evidence

`scenario-journal.tsv` must contain exactly one successful `QUICKSTART-BOOT-BEGIN`,
`QUICKSTART-BOOT-OBSERVED` and terminal `QUICKSTART-BOOT-COMPLETE`, with no refusal or exception
in the run. The observation occurs after the exact non-generic
`EmbarkInfo.fireBootEvent(string, XRLGame)` has finished `GAMESTARTING`. Completion is rechecked
after the rest of `EmbarkInfo.bootGame`, including later `EmbarkEvent("BootGame")` callbacks.

Checks cover one real world-extension/camp/bootstrap invocation: the camp sees the completed
Built zone before player placement, then bootstrap sees that same zone and the actual founder
at `(40,12)`. The observer records start-cell reachability before preparation and requires it
afterward; this is not proof of safe escape or general traversal. Other checks require an
initially absent Quickstart receipt, correct seed/profile/mode/options, exact founded heart,
canonical Complete receipt, no quarantine, no scenario runner/request, single physical grant
identities, and initial quantities:24 drams,12 meals,1 mud/3 brush/4 timber. Advisor decision must
match the sealed selection. Owner, receipt, quantities and terminal boot conditions are checked
again before the final positive. Treat an absent journal row as unproved, never success.

The `quickstart-boot` command proves boot only when actually executed. Save/cold-load uses the
separate command below; ordinary-play acceptance and subscriber-package checks remain separate.
The offline [readiness diagnostic](EngineQuickstartReady/README.md) remains blocked by Unity
initialization and is not substituted for this route.

## Saved-world continuation — genuine six-combination matrix

Prepare another fresh profile with `TAF_SCENARIO_SCRIPT='quickstart-save marsh yes'` and the same
explicit matching advisor override. All six profile/advisor combinations remain required.
This command runs the same actual boot checks. The exact `XRLCore.NewGame` postfix then captures
the game/seed/profile/advisor, four clocks, Complete receipt, raw heart/seal/terminal, seven exact
reservations, and existing founder IDs without allocating an ID or writing synthetic game state.
Physical grant IDs and quantities are proved by the production receipt and native observer.

The harness calls real `SaveGame("Primary")`, awaits its task when present, and independently
checks new artifacts, hashes and unchanged live state. Task completion alone is not success.
The actual cache destination is frozen before writing and checked again during serialization.
Only the exact owned new-game Core thread is parked before gameplay turns; failed owned boots
also park, including a null return from the engine. Other games are not paused or intercepted.

Require the three boot milestones followed by one successful `QUICKSTART-SAVE-BEGIN` and
`QUICKSTART-SAVE-COMPLETE`. Retain the external snapshot, the five-line save receipt, primary
save, metadata and log. A save-only receipt explicitly says `cold-load=false`.

After stopping the exact owned source process, use the existing `Tools/prepare-scenario-load.py`
transport to copy its snapshot and quiescent save/cache into a new sealed profile. Its dedicated
Continue route calls actual `XRLGame.LoadGame`, without restoring mods or replaying the new-game
script. A Quickstart-specific witness checks restored state before the player's `GameRestored`
and activation handlers, after `LoadGame` returns, and again after owned popup cleanup. This is
not a claim to observe before every earlier deserialization callback.

Require one successful `LOAD-BEGIN`, `QUICKSTART-LOAD-PREACTIVATION` and
`QUICKSTART-LOAD-COMPLETE`, one error-free primary reader, unchanged clocks/IDs/heart/stock and
zero bootstrap calls. The existing owned Continue barrier parks the loaded fixture before turns.
Any refusal, missing/duplicate/out-of-order milestone, changed sealed input or failed artifact
hash is a failed or incomplete run, never native acceptance.

The process-control tool stops its exact owned process by killing it, not by invoking the game's
Quit command. The current matrix exercises real save → owned process exit → cold reload;
graceful in-game Quit remains separate coverage. Any new observer requires fresh sealed source
and load profiles. The already prepared boot-only profile must not be silently modified.

## Read-only result check

After collecting the completed run, verify its exact profile and requested phase:

```bash
python3 Tools/check-quickstart-results.py /mnt/c/taf-scenario.EXAMPLE --phase save
```

Use `boot` for boot-only profiles and `load` for the separately imported cold-load profile.
The checker requires closed sealed Local content, matching script/options/seed, exact ordered
journal milestones and stable evidence reads. Save/load phases also bind snapshot identity and
artifact hashes; the save receipt does not bind the cache hash until stopped-source import.
Player.log is checked without any inherited `TAF_LOG_ALLOW` allowances. Optional exact leading
AUTOSTART is reported separately; duplicate, trailing, unrelated or refused rows fail.

Exit0 emits a scoped developer-evidence JSON verdict, never process-custody or release approval.
Exit2 refuses missing, malformed or contradictory evidence. The checker does not authenticate
fabricated journals or reimplement nested native heart/reservation semantics. Prove source
process ownership/exit separately using the existing receipt-owned runner. Its synthetic tests
prove only checker behavior; they are not actual boot or save/load tests.

## Quickstart build (issue #142) — a separate, later proof, not the boot proof

`quickstart-build <marsh|canyon|dunes> <yes|no>` is a third sibling of `quickstart-boot` and
`quickstart-save`, using the identical `Tools/prepare-scenario.sh` invocation and the identical
genuine production boot. It never redefines, reorders or relabels the boot proof: the build
phase begins only after `Harness/KingdomQuickstartBootTest.cs` has already journaled an
unmodified, unchanged `QUICKSTART-BOOT-COMPLETE` row.

```bash
TAF_REQUEST=founding-first-city \
TAF_SCENARIO_SCRIPT='quickstart-build marsh no' \
TAF_SCENARIO_QUICKSTART_ADVISOR=no \
Tools/prepare-scenario.sh '' '#4242'
```

After boot, the harness drives the exact three-call production commissioning sequence the
Charter UI itself drives for a plotted design (`Core/KingdomCharterPart.Commission.cs:70-105`):
`KingdomPlots.TryQuoteCommission`, then the UI's own water/`KingdomMaterials.CanPay`
pre-check, then `KingdomCommission.Commission` committing that exact quote — for the `"fire"`
design (a real plotted `RuntimeData/KingdomBuildings.xml` entry, `Materials="timber:1"`,
`Cost="2"`). No harness `BindPass`, no minted stock. A production refusal is journaled
as a distinct, attributed terminal outcome rather than a harness error. The test is designed
to catch the reported stock-scope defect; another refusal reason must be diagnosed on its own
evidence. A successful run requires both production calls and all post-payment proofs to pass.

Each step is its own journal row (`QUICKSTART-BUILD-QUOTE`, `QUICKSTART-BUILD-CANPAY`,
`QUICKSTART-BUILD-COMMISSION`), present only as far as the sequence actually reached, followed
by one terminal `QUICKSTART-BUILD-COMPLETE` naming which step (if any) refused. On success, the
harness re-proves by exact object reference: the starter materials chest's own timber row drops
from 4 to 3 raw units with every other row's reference and count unchanged, the receipted water
cask's own `LiquidVolume` drops by the `"fire"` entry's exact `CostDrams`, and a new
`KingdomConstructionJob` (absent before the call, present after, matched by exact
settlement/zone/route/target, exact paid water and material claims, exact `Working` phase, and
its own linked build output resolved by `FindExactId`) appears in the construction registry.
`IDIfAssigned` is read for reporting only; an unassigned starter child id is never itself a
refusal. No harness scope may be bound before or remain after any of the three calls.
Production itself must bind the scoped stock/commission operations while they execute.

Verify with the same checker, one more phase value:

```bash
python3 Tools/check-quickstart-results.py /mnt/c/taf-scenario.EXAMPLE --phase build
```

`quickstart-build` produces no save/load artifacts (same file-layout profile as `boot`). A
refused build is a valid, well-formed `REFUSED`-outcome terminal row, not a checker crash.
It still fails acceptance: the checker must return exit 2 and REFUSED, never PASS.

Paid-claim checks use `KingdomQuickstartBuildClaims.CleanFirstPayment`, executed by pure tests
in both test projects. Construction claims call physical net debit `Lost`, not additional
waste: a fresh exact payment requires `Requested == Spent == Lost`, with zero outstanding
water/material and `Exact == true`. Requiring zero Lost incorrectly rejected the first
native fire commission after its physical debit and placement succeeded. Prior/retried jobs
can legitimately have greater historical loss; this fixture accepts only a newly created job
whose exact before/after stock proves no extra debit.
