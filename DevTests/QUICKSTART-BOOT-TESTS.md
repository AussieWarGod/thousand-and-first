# Genuine Quickstart boot and save/cold-load checks

These developer-only checks select the real `KingdomQuickstart` mode and its production starting
location. They do not select `TAFScenario`, run `KingdomScenarioAutoRunner`, strip the zone,
advance a synthetic receipt, invent a heart rung, or call the bootstrap a second time.

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
unapproved user session or reuse an already spent profile. No profile has been launched for
this new route yet.

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

Checks cover one real world-extension/camp/bootstrap invocation, the already placed founder and
initially absent Quickstart receipt, correct seed/profile/mode/options, exact founded heart,
canonical Complete receipt, no quarantine, no scenario runner/request, single physical grant
identities, and initial quantities:24 drams,12 meals,1 mud/3 brush/4 timber. Advisor decision must
match the sealed selection. Owner, receipt, quantities and terminal boot conditions are checked
again before the final positive. Treat an absent journal row as unproved, never success.

The `quickstart-boot` command proves boot only when actually executed. Save/cold-load uses the
separate command below; ordinary-play acceptance and subscriber-package checks remain separate.
The offline [readiness diagnostic](EngineQuickstartReady/README.md) remains blocked by Unity
initialization and is not substituted for this route.

## Saved-world continuation — implemented and compiled, not run

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
Quit command. A completed test would prove real save → owned process exit → cold reload;
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
