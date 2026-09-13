# First-guest citizenship save/cold-load check

Status: implemented; native execution pending. This supplements the historical recruitment
evidence in the coverage matrix. It does not change the frozen 0.3.4 release protocol.

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
