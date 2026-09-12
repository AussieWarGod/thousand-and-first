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
  only with comparison present and enrollment/property-write APIs absent. The rerun passed 14769 main cases with zero skips, then the portable project failed to compile its missing new dependencies. Those dependencies and unused engine imports were corrected; current Linux full suites pass 14771 main and 5731 portable cases, zero skips.
- Genuine marsh/advisor-on Quickstart at seed `#43101`, on `7cb11cf`, reached startup with four
  citizens, zero completed shelters and zero beds. After 7200 ordinary turns it retained the
  same four citizens, both reserved tent rows were functionally built, six physical beds were
  counted, and all four citizens were assigned homes. The paid fire completed and a real save
  was written. Session 1's strict Player.log check and persona assertions passed; its owned
  process was stopped and the stop recorded. Cold load preserved the same four citizens, six beds, homes and completed fire. A second paid commission debited actual zone timber 23 to 22. Both journals completed, but strict cold-load Player.log FAILED on two seal reconciliation MODERRORs. The run remains FAILED. The missing-stage branch bypassed #181; `e1d26d9` carries typed Pending there without clearing dirty state or advancing revision. Native rerun is owed.
- Evidence root: `/home/r/work/taf-scratch/hotfix-quickstart.FIuLCD/marsh-yes-2`.
  Save profile: `/mnt/c/taf-scenario.v4248bnb`; load profile: `/mnt/c/taf-scenario.qptkbr2x`.
  The earlier `marsh-yes` attempt failed during profile preparation because the temporary host
  runner omitted the required advisor binding. No game launched for that attempt.

## Housing shortage gate

`quickstart-housing-recovery.persona` compiled in all four modes at `44bd72f`; its native run at `e1d26d9` PASSED (`/mnt/c/taf-scenario.enTgCl`), including strict Player.log and receipt-owned stop. The 206-row journal retained the same two citizens at turns 10002 and 12402, then proved usable homes and the completed paid building at turn 17202.
It starts with the real four founding citizens. Physical empty chests block both starter housing
lots for 12400 ordinary turns, with explicitly synthetic water and meals to isolate roof pressure.
Two real roof departures must leave the same two citizens across repeated passes. After removing
only those exact obstacles, ordinary turns must yield usable housing for the survivors and a
completed physical output for the paid construction job. Population, clocks, work completion,
departures and home assignments are never set by the fixture.

Production already intends to retain `KingdomRules.LoyalCoreSettlers == 2` on voluntary departure.
`e1d26d9` adds a fresh settlement census after pending/orphan recovery and before admitting a new departure, closing the stale pre-recovery floor check. The exact recovery interleaving still needs native fault-injection evidence. The housing stress run proved two real roof departures, repeated retention, and physical construction recovery. It does not yet prove a cold load with departed founders.

## Remaining hotfix work

Finish the normal cold-load/next-action chain. The delayed-housing scenario has passed. The normal lifecycle now waits 10800 turns (nine days), past the roof-departure deadline; that extended native run is owed. Run the three Quickstart locations with both advisor
choices on final candidate bytes; test existing-save recovery without retroactive founder grants.
The guide now explains four default founders, clear starter shelter plots, and exact first-guest charter/interaction choices. Its text checks pass; native guest interaction coverage still needs review. Complete the automated release
gates and subscribed-install verification before publishing the next Alpha. The broader Beta goal
and combination-coverage backlog remain active; no Beta or release-readiness claim is made here.

Validation update: four final compile modes passed at production inventory `e2faa4877de8f08945de13b5cbda24573afecf36f73e15fffee78b74cdbe7066`. Full tooling initially failed because the new nine-day advance exceeded the per-verb 10000-turn limit and the persona census still expected 92. `439f980` splits it into 7200 + 3600, records 93 personas; all 60 persona-matrix tests and 25 lifecycle source contracts pass. The full tooling failure is retained in `/tmp/taf-hotfix-tools-final.log`; a full corrected rerun is owed. Nine-day prepare attempts 1 and 2 launched no game (oversized advance, then a randomly generated underscore rejected by the exact profile-name grammar). Attempt 3 uses the canonical mktemp profile allocator and is running.

The first completed nine-day run (`439f980`, profile `/mnt/c/taf-scenario.6JYYg5`) FAILED. Both shelters and the paid fire finished, but 24 starter drams ran out: two original citizens emigrated for drought by day nine. One seal daily MODERROR also classified a physically obstructed exterior entrance at 24,17 as malformed building evidence. Candidate corrections: new worlds receive 48 finite drams; typed exterior-ingress obstruction defers seal capture only after all component and internal passability checks succeed. A new native probe temporarily blocks a completed home with an exact owned wall and liquid carrier, requires Pending with unchanged stage, removes each obstacle, and verifies the same home again. A missing entrance remains a structural refusal. Current compile and full-suite runs are pending. All earlier PASS claims remain bound to their stated bytes.
