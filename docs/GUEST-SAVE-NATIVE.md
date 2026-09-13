# First-guest citizenship save/cold-load check

Status: native PASS for source `4c97470d66e845b9841d4abd74d20d6787fc0e6c`, with journal validator `b11e26fb`.
Both separate sessions preserve their complete profile seals and pass strict logs and owned
stop records. All 3353 C# inputs and runtime content bind to the source. Evidence:
`guest-save/4c97470d/carried-water-recovery-1/result.json`, SHA-256 `4efadb2941868f06022198414607f3260e018dfa0be0fd4b01692e5eb74fe000`.
Coverage row 28 and combination C8 record the measured scope. Public Alpha 0.3.4 remains unchanged.

`Tools/personas/guest-save-lifecycle-native-check.persona` drives genuine marsh Quickstart
with the advisor, four founding citizens, finite starter supplies and ordinary construction.
After 8400 real turns it defers and admits the exact first guest, exercises the actual body
action, then welcomes that same body once. Another 8400 turns permit ordinary settlement
activity before the completed campfire, original four founders and starter housing are checked.
Subsequent ordinary arrivals are allowed; the exact saved population must survive loading.

The first execution preserved the guest across load but failed the final paid commission:
five citizens had exhausted the finite starter water. That run remains a failed complete
scenario. The revised source requires the actual empty store and a refused, otherwise payable
two-dram campfire commission. The founder walks to the receipted cask and transfers eight
drams from an existing carried pure-water vessel using the engine's physical liquid transfer.
No vessels, supplies, citizens or housing are created for this recovery. Exact donor/store
deltas, adjacency, guest authority, materials, construction registry, clocks and upkeep
accounting must survive the refusal and refill. Missing carried water or an obstructed route
refuses the scenario. The full save/load witness still includes stored water; only the local
transfer comparison measures that intentionally changed quantity separately.

The revised run at `76baa664` stopped before recruitment: a giant dragonfly killed original
founder `516`; the other three founders and six completed beds remained. The full sealed
source and strict log passed custody checks, but the scenario failed. Retained evidence:
`guest-save/76baa664/dragonfly-founder-death-1/result.json`, SHA-256
`ceb240de0144a5957bfc1865b680998863d37fbd21f172dc16121e56679143be`.
This is a witnessed combat death, not evidence of another housing departure. Read-only
diagnostics now record the victim/killer goals and any civic posting while a founder has a
combat/flee goal. The harness does not suppress wildlife, alter combat, replace founders,
or waive the original-founder requirement. The causal role of civic AI remains unverified.

Run `4d207e8c` kept all four founders and the enrolled guest through fourteen days, with no
recorded civic combat interruption. It failed in the supply harness before pouring: the
material census incorrectly selected water-store markers instead of `KingdomMaterials.IsStockpile`.
The walk also caused the engine to add previously absent `OptionLookLocked=No` and rewrite its
options JSON format. The owned process stopped and strict log passed, but the stop record
correctly refused the changed profile seal. The failed profile was never repaired or resealed.
Evidence: `guest-save/4d207e8c/material-census-and-options-drift-1/result.json`, SHA-256
`4ccbb67acd67001fc17afcf37ffab8a1f4039cbf6f4ab4f00e9312ab40fd889e`.
The corrected census uses the production material predicate. New physical guest-walk profiles
author the initial look option and observed engine JSON format before sealing; all later
profile changes still refuse. Other scenario options retain their existing format.

The `guest-save-witness` verb captures the real guest's identity, living roll membership,
applied arrival citizenship, home plot, name and nullable creed, arrival domain receipts,
terminal first-guest receipt and opportunity fields, population, water and arrival accounting.
It writes only developer evidence. The existing `lifecycle-save` uses the real engine save.

The separate sealed load compares that witness before `AfterGameLoaded` and again after
activation. It requires the original four founders and functional housing through the existing
settlement observer. The guest must remain the same living citizen in the same home, and the
inventory event must no longer offer the first-guest action. Two stale action retries must
preserve the exact witness, including population, water and arrival accounting. The existing
lifecycle verifier then checks the original paid building and commissions another paid job.

## Automated execution and verdicts

Use a clean committed tree, one owned game at a time, and the existing sealed profile tools:

1. Prepare the persona's exact `REQUEST`, `SCRIPT` and `VERBS` with
   `TAF_SCENARIO_QUICKSTART_ADVISOR=yes`, `TAF_SCENARIO_ROLE=save-session`,
   `TAF_SCENARIO_TURN_BUDGET=17000`, `TAF_SCENARIO_TIMEOUT_SECONDS=1800` and seed `#43101`.
   `Tools/prepare-scenario.sh` records the source commit and seals the profile.
2. Launch with `Tools/run-scenario.ps1`, await the terminal journal, assert the persona,
   stop only its receipt-owned process with `Tools/scenario-process-control.ps1 -Mode stop`,
   and record the stop with `Tools/run-scenario.ps1 -StopRecord`.
3. Copy that stopped save to a fresh profile using `Tools/prepare-scenario-load.py`.
   Launch it with the same owned launcher, await the terminal journal, then stop and record
   its process. No new-game script replay, saved-state repair or human input is allowed.
4. Require both `Tools/check-player-log.sh` results to pass. Run both journal oracles:

```bash
python3 Tools/check-quickstart-lifecycle.py SAVE_JOURNAL LOAD_JOURNAL \
  --results LIFECYCLE_RESULT --run-record SAVE_RECORD,LOAD_RECORD
python3 Tools/guest_save_check.py SAVE_JOURNAL LOAD_JOURNAL --results GUEST_RESULT
```

Both oracles must pass. Neither alone proves the whole scenario. Retain journals, strict logs,
run records, stop receipts and closed profile seals; bind all production/harness C# bytes to
the recorded commit before adding typed native evidence. Keep the actual saves local.
Source-session completion alone does not cover guest save/load. Rendered UI, arbitrary old-save
migration and interrupted guest enrollment remain outside this scenario.

## Accepted execution

Source profile saved at turn 16803 after four original founders and guest 654 (Vadan) completed
the scenario. The empty civic store refused a two-dram commission without debit. Sixteen
ordinary movement steps reached the actual cask; carried water fell 32->24 while civic water
rose 0->8. No supplies were created. Separate cold load matched guest authority before
activation and afterward, refused two stale guest choices, retained the four original housed
founders and previous paid fire, then paid one timber and two drams for a distinct new job.

The source records commit 4c97470d. Its game/harness bytes are unchanged from fcab75c6; the
intervening correction affects only the journal prefix parser. Validator b11e26fb also accepts
the engine's valid empty ID on an existing carried vessel while requiring the field exactly
once. Transfer identity was checked in game through exact object/part and inventory-owner
references. No source profile, saved state, or retained journal was rewritten for these
parser corrections. Missing or repeated donor fields still refuse.
