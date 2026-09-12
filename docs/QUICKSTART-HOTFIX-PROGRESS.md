# Quickstart housing hotfix — work in progress

The Steam report describes an opening deadlock: no citizens to build housing, no housing to
admit citizens, and an unexplained visiting NPC. The hotfix must prove playable Quickstart,
including housing-shortage recovery; it must not infer gameplay from boot or source tests.

Work continues from Claude's `dev` checkpoint `899f7d6` in branch
`codex/beta-goal-progression` at `/home/r/work/taf-beta-goal`. The original checkout remains
unchanged. This candidate integrates the pending spatial-capture load correction (#181) and
the lifecycle accounting/load-witness corrections (#183). Public 0.3.3 is unchanged.

## Current candidate validation

Production/harness changes through `bdde5cff` bind staged C# inventory
`edb507bb9f3fc5203e4fe3f83312f4c4c9adda7c9fbba99e6b2a73145a212205`.
All four canonical compile modes passed (3094/3098 ordinary, 3338/3342 harness).
Linux full suites passed 14772 main and 5732 portable cases, zero skips. Full
Python tooling passed 1066 cases with 12 platform skips; coverage tests passed 38.
The exact inventory structural review is in `STRUCTURE_REVIEW_0_3_4.md`.

Final native matrix is running at
`/home/r/work/taf-scratch/hotfix-quickstart.FIuLCD/final-matrix-1`.
Marsh/advisor-on PASSED: nine days retained the same four founders, all housed,
two complete shelters and six physical beds; the paid fire completed; real save
and cold load preserved those facts; another paid commission debited actual zone
timber 23 to 22. Both strict Player.log checks and receipt-owned stops passed.
The wall/liquid-carrier ingress negatives and missing-anchor refusal also passed.
Save `/mnt/c/taf-scenario.HSWSI8`, load `/mnt/c/taf-scenario.i1Mo8T`.
Remaining pairs and final-candidate housing stress are still pending.

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
  process was stopped and the stop recorded. Cold load preserved the same four citizens, six beds, homes and completed fire. A second paid commission debited actual zone timber 23 to 22. Both journals completed, but strict cold-load Player.log FAILED on two seal reconciliation MODERRORs. The run remains FAILED. The missing-stage branch bypassed #181; `e1d26d9` carries typed Pending there without clearing dirty state or advancing revision. The final marsh/advisor-on rerun passed; remaining combinations are tracked above.
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

Complete all six normal cold-load/next-action pairs. Historical delayed-housing evidence passed; rerun its strengthened Working-phase assertions on final bytes. The normal lifecycle waits 10800 turns (nine days), past the roof-departure deadline. Run the three Quickstart locations with both advisor
choices on final candidate bytes; test existing-save recovery without retroactive founder grants.
The guide now explains four default founders, clear starter shelter plots, and exact first-guest charter/interaction choices. Its text checks pass; native guest interaction coverage still needs review. Complete the automated release
gates and subscribed-install verification before publishing the next Alpha. The broader Beta goal
and combination-coverage backlog remain active; no Beta or release-readiness claim is made here.

Validation update: four final compile modes passed at production inventory `e2faa4877de8f08945de13b5cbda24573afecf36f73e15fffee78b74cdbe7066`. Full tooling initially failed because the new nine-day advance exceeded the per-verb 10000-turn limit and the persona census still expected 92. `439f980` splits it into 7200 + 3600, records 93 personas; all 60 persona-matrix tests and 25 lifecycle source contracts pass. The full tooling failure is retained in `/tmp/taf-hotfix-tools-final.log`; the corrected full rerun passed 1066 tests with 12 platform skips in `/tmp/taf-hotfix-tools-corrected.log`. Nine-day prepare attempts 1 and 2 launched no game (oversized advance, then a randomly generated underscore rejected by the exact profile-name grammar). Attempt 3 used the canonical mktemp profile allocator; its failure is retained below.

The first completed nine-day run (`439f980`, profile `/mnt/c/taf-scenario.6JYYg5`) FAILED. Both shelters and the paid fire finished, but 24 starter drams ran out: two original citizens emigrated for drought by day nine. One seal daily MODERROR also classified a physically obstructed exterior entrance at 24,17 as malformed building evidence. Candidate corrections: new worlds receive 48 finite drams; typed exterior-ingress obstruction defers seal capture only after all component and internal passability checks succeed. A new native probe temporarily blocks a completed home with an exact owned wall and liquid carrier, requires Pending with unchanged stage, removes each obstacle, and verifies the same home again. A missing entrance remains a structural refusal. Those corrections passed current compile and full suites, as recorded above. All earlier PASS claims remain bound to their stated bytes.

## Housing crisis cold-load extension — native run pending

`quickstart-housing-recovery-save.persona` repeats the full adverse housing scenario,
then freezes the two exact surviving IDs and two exact departed IDs before a real
save. Its separate load lane reads that evidence, requires the same original
Quickstart receipt, two citizen/roll/home assignments and two absent departed
bodies, then uses the existing paid-building/load/next-commission checks.
The normal lifecycle continues to require all four founders. The new lane is
claimed by its sealed housing-witness verb and requires its entire exact script;
it never edits citizens, housing or production receipts during load.

The source-only persona does not prove cold load by itself. Both sessions need
strict Player.log checks and receipt-owned stops. Four compile modes passed
(3094/3098 ordinary, 3339/3343 harness), 60 persona tests and 25 lifecycle source
contracts passed. Production inventory remains `edb507bb9f3fc5203e4fe3f83312f4c4c9adda7c9fbba99e6b2a73145a212205`.
Native housing-crisis cold-load evidence is still owed.

### Full-cask follow-up after canyon budget refusal

Four nine-day lifecycle chains passed (marsh/dunes, guide yes/no). Canyon/yes
retained all four founders under completed roofs but refused its unfinished fire:
the actual rocky-ground quote was 15000 ticks; 5401 ticks remained after 10800 turns.
Evidence: `final-matrix-1/canyon-yes` under the evidence root. Canyon/no was not run.
No failed receipt was changed or counted as PASS.

Starter water now fills the finite 64-dram cask, with matching guide and diagnostic.
Acceptance persona now advances 16800 turns (7200 + 9600), allowing that actual
quote and fourteen days of housing stability. Fresh native matrix remains pending.
Strike phase recovery from #182 is integrated; its native teardown proof remains
pending. Housing stress/cold-load is running against the frozen 48-dram predecessor
`d268081f`, and will remain attributed to those exact bytes.

The `d268081f` housing save/load source attempt refused at its first retained check:
two original citizens remained with no beds, but the paid fire had already finished.
Its new unfinished-work assertion correctly caught the earlier fixture's accidental
reliance on a wandering body blocking that plot. The revised fixture places owned
empty chests on the paid plot as well as both shelter lots, removes only those exact
empty objects after two retained checks, and still requires real completion afterward.
This is harness setup, not a production job-state change. The failed source remains
under `final-housing-load-1`; no cold load ran and no PASS is claimed.

Full-cask production checks: 14775 main and 5732 portable cases passed with zero
skips; all four compile modes passed (3094/3098 ordinary, 3339/3343 harness).
Logs: `/tmp/taf-full-cask-full-2.log`, `/tmp/taf-full-cask-portable.log`,
`/tmp/taf-full-cask-gate.log`. The subsequent deterministic paid-plot obstruction
needs its own harness compile and fresh native run.

### Full-cask housing recovery: gameplay passed, save harness refused

At `c1eb5e1f`, the revised 72-obstacle native setup proved two original roof
departures, two repeated checks retaining the same two citizens with zero beds,
and ordinary completion of both starter homes and the original paid fire after
removing only owned empty obstacles. The exact survivor/departure save witness
was recorded. The following real-save step correctly refused an ambiguous store:
the script omitted `lifecycle-open`, so no original stockpile had been bound before
the heart produced another dedicated store. No save or cold-load PASS is claimed.
Evidence: `full-cask-housing-load-1` under the evidence root.

The save persona now calls the existing production-read startup step before the
shortage setup, binding the exact original store. Its strict script witness includes
that step. This changes only the harness; the production digest remains
`43ae0c005513626488ce557061ad506898e9c6c26c3bd935639db5ecfff307b6`.
A fresh native save/load attempt is required. The fourteen-day canyon/yes lifecycle
is running separately from an immutable `c1eb5e1f` checkout.

### Canyon continuation exposed missed protected ground

The full-cask `c1eb5e1f` canyon/yes source passed fourteen days with all four
original citizens housed, two tent rows/six beds, the completed paid fire and a
real save. Cold load preserved those facts. The next commission correctly refused
an authored footprint over a zone connection at 29,12. The chain is FAIL, despite
its successful prior observations; all evidence remains in `full-cask-matrix-1/canyon-yes`.

Automatic site selection now shares the stamper's exact enumerated/cached connection
census and collects live stairs, rejecting only their intersection with authored
claimed cells/placements before scoring candidates. Final preflight still protects
those cells. Main 14775/portable 5732 tests and all four compile modes pass; native
rerun is owed. The corrected housing save/load script is currently running separately
on frozen `60f11aee` production, before this site-selection change.

### Housing recovery, real save, cold load and next action: PASS

The strengthened `60f11aee` scenario completed all 14 source verbs and the separate
cold-load/next-commission session. Two roof departures left the same two original
citizens through repeated zero-bed observations while the paid job remained Working.
After removing the 72 owned empty obstacles, those citizens occupied completed homes
and finished the original paid building. Cold load preserved both survivors, six usable
beds, the exact original receipt and output; both departed founders stayed absent.
Another commission debited one timber and two drams. Both strict game logs and owned
process stops passed. This is supplied stress coverage (400 real drams, 100 meals),
not ordinary-start or whole-current-inventory acceptance.

Result: `full-cask-housing-load-2/housing-load-result.json` under the evidence root.
Checked archive: `quickstart-housing/60f11aee/` under the behaviour-evidence root.
Production inventory was `43ae0c005513626488ce557061ad506898e9c6c26c3bd935639db5ecfff307b6`;
later protected-site selection and the release-version literal are separate changes.
Coverage rows 3/4/6/24 and combination C7 retain that exact scope and digest.

Private 0.3.4 candidate `b564eaed` (same content tree as `9e0a4dfd`) passes all four
compile modes and 14775 main/5732 portable cases with zero skips. Logs:
`/tmp/taf-alpha-034-gate.log`, `/tmp/taf-alpha-034-full.log`,
`/tmp/taf-alpha-034-portable.log`. Its inventory is
`73c87caced09ccdffc714a2475c170679e6e37a68b0fe42e92566d98c18d2907`.
No private or public upload has occurred; native site-selection and delivery checks remain pending.
