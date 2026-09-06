# Genuine Quickstart boot checks

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

This route proves boot only when actually executed. It does not yet automate save/quit/reload;
that remains required separately, as do ordinary-play acceptance and subscriber-package checks.
The offline [readiness diagnostic](EngineQuickstartReady/README.md) remains blocked by Unity
initialization and is not substituted for this route.

## Next boundary: saved-world continuation (not implemented)

Use the successful terminal boot observation to capture exact game/seed/profile/advisor,
clock, Complete receipt, heart reservations and existing founder/grant IDs. Await the real
`SaveGame("Primary")`, then independently prove new save artifacts: its return/task completion
alone is not a success receipt. Hold the exact owned test world before ordinary turns can change
the saved state or cache. Do not attach AutoRunner or rerun Bootstrap.

The existing `Tools/prepare-scenario-load.py` transport can copy the snapshot and quiescent
save/cache into a new sealed profile after exact source-process absence. The Harness load entry
and witness still need a distinct Quickstart branch: inspect actual restored state before
player activation handlers, then again after `LoadGame` returns. Prove unchanged receipt/IDs,
single physical stock and no Bootstrap replay; do not substitute subsidence/scenario witnesses.

The process-control tool stops its exact owned process by killing it, not by invoking the game's
Quit command. Such a completed test would prove real save → owned process exit → cold reload;
graceful in-game Quit remains separate coverage. Any new observer requires fresh sealed source
and load profiles. The already prepared boot-only profile must not be silently modified.
