# Quickstart housing hotfix — work in progress

The Steam report describes an opening deadlock: no citizens to build housing, no housing to
admit citizens, and an unexplained visiting NPC. The hotfix must prove playable Quickstart,
including housing-shortage recovery; it must not infer gameplay from boot or source tests.

Work continues from Claude's `dev` checkpoint `899f7d6` in branch
`codex/beta-goal-progression` at `/home/r/work/taf-beta-goal`. The original checkout remains
unchanged. This candidate integrates the pending spatial-capture load correction (#181) and
the lifecycle accounting/load-witness corrections (#183). Public 0.3.3 is unchanged.

## Recorded evidence

- `4bcf552` adds read-only original-citizen and housing checks to the construction lifecycle.
  Its C# bytes passed all four canonical compile modes: staged 3094/3098, harness 3335/3339.
  Tooling passed 1066 tests with 12 platform skips. These are not native gameplay evidence.
- The first Windows full-suite attempt found a source-contract false positive: the new observer
  reads `EnrollmentReason.Founding`, while the old guard treated every reference as enrollment.
  That failed run was stopped, retained, and corrected at `7cb11cf`: the exact observer is allowed
  only with comparison present and enrollment/property-write APIs absent. Full rerun is pending.
- Genuine marsh/advisor-on Quickstart at seed `#43101`, on `7cb11cf`, reached startup with four
  citizens, zero completed shelters and zero beds. After 7200 ordinary turns it retained the
  same four citizens, both reserved tent rows were functionally built, six physical beds were
  counted, and all four citizens were assigned homes. The paid fire completed and a real save
  was written. Session 1's strict Player.log check and persona assertions passed; its owned
  process was stopped and the stop recorded. Cold-load preparation is still running.
- Evidence root: `/home/r/work/taf-scratch/hotfix-quickstart.FIuLCD/marsh-yes-2`.
  Save profile: `/mnt/c/taf-scenario.v4248bnb`; load profile: `/mnt/c/taf-scenario.qptkbr2x`.
  The earlier `marsh-yes` attempt failed during profile preparation because the temporary host
  runner omitted the required advisor binding. No game launched for that attempt.

## Housing shortage gate

`quickstart-housing-recovery.persona` is authored, parsed, and **not compiled or executed yet**.
It starts with the real four founding citizens. Physical empty chests block both starter housing
lots for 12400 ordinary turns, with explicitly synthetic water and meals to isolate roof pressure.
Two real roof departures must leave the same two citizens across repeated passes. After removing
only those exact obstacles, ordinary turns must yield usable housing for the survivors and a
completed physical output for the paid construction job. Population, clocks, work completion,
departures and home assignments are never set by the fixture.

Production already intends to retain `KingdomRules.LoyalCoreSettlers == 2` on voluntary departure.
This must be proved on the housing path, including actual construction recovery. Also inspect the
ordering of the population check in `KingdomGrowth.EmigrateCore` against pending-departure recovery
inside `KingdomResidentDepartureRuntime.TryBegin`: recovery may change the roll after that first
check. This is a source-review concern, not a reproduced defect or a completed fix.

## Remaining hotfix work

Finish the cold-load/next-action chain and the delayed-housing recovery scenario. Extend normal
observations past the roof-departure deadline. Run the three Quickstart locations with both advisor
choices on final candidate bytes; test existing-save recovery without retroactive founder grants.
Clarify the guide's building, housing, and first-guest instructions. Complete the automated release
gates and subscribed-install verification before publishing the next Alpha. The broader Beta goal
and combination-coverage backlog remain active; no Beta or release-readiness claim is made here.
