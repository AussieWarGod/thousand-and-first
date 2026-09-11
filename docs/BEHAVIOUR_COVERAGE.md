# Behavioural coverage matrix

Machine-checkable inventory of in-game behavioural coverage per implemented system. A row's
"Driver" column names the exact runnable native seam (verb/persona/checker) or `NONE`. Source
pins (`DevTests/*SourceTests.cs`) never fill a behavioural cell — they only prove code shape,
not that a state was reached in a real running game. A state no driver can reach is recorded as
**DEFECT**, not silently omitted. Built by direct inspection of `Harness/*Provider.cs`
(`[KingdomScenarioVerbProvider]`), `Tools/personas/*.py`, and `Core/Growth/World/RuntimeData`
entrypoints; not a full enumeration of every production file, given the scope of one bounded
pass — treat unlisted systems as **unreviewed**, not covered.

| # | Behaviour | Prerequisites | Reachable transitions | Observable effects | Save/cold-load | Negative/interrupted | Driver (verb/persona/checker) | Last evidence | Missing coverage |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Founding (first city) | fresh camp, marsh profile | found -> claimed zone | `KingdomSystem.Founded`, `ClaimedZones` | via `quickstart-save`(owned)/`KingdomScenarioSaveChecks` | none observed here | `Harness/KingdomFirstGuestNativeProvider.cs:18` (setup founds), `KingdomNativeCampFounding.Found` (`Harness/KingdomNativeCampFounding.cs:26`) | see `docs/STATUS.md` native rows (not re-verified this pass) | interrupted-founding path |
| 2 | NPC join (first guest) | founded, no roof | correspondence opens -> resolved | message emission, ledger note | NOT covered | NOT covered | `first-guest-setup`/`first-guest-check` (`Harness/KingdomFirstGuestNativeProvider.cs:18-19`) | prior PR evidence, not re-run | save/cold-load, interrupted path |
| 3 | NPC join (Quickstart founders) | Quickstart | founders enrolled | roster count | owned lane | owned lane | camp-builder-owned (`Harness/KingdomQuickstartBootstrap.Founders*.cs`) | owned lane | out of this review's edit scope |
| 4 | Resident departure ("leave") | founded, enrolled resident | `KingdomResidentDepartureRuntime.TryBegin` (`Growth/KingdomResidentDepartureRuntime.Begin.cs:10`) | population/roster drop, capacity archive | NOT covered | NOT covered | **NONE** | **NONE** | **DEFECT: unreachable** — no `[KingdomScenarioVerbProvider]` exercises ordinary (non-death) departure at all |
| 5 | Resident death | founded, enrolled resident | death -> recovery | roster drop, chronicle | not verified | veto path exists | `KingdomRaidDeathNativeProvider.cs`, `KingdomRaidDeathVetoNativeProvider.cs`, `KingdomRaidRecoveryDeathNativeProvider.cs` | prior evidence, not re-run | non-raid death path |
| 6 | Building construction (plot) | founded, water+material stock | quote/CanPay/Commission -> `Working`/`PlotWorks` | job phase, works receipt, ground | owned lane (Quickstart build) | owned lane | camp-builder-owned `Harness/KingdomQuickstartBuild*.cs` for the Quickstart leg; **this pass's new** `teardown-setup` (`Harness/KingdomTeardownNativeProvider.cs:19`) independently exercises the same `KingdomCommission.Commission` for a non-Quickstart camp | this pass, unexecuted (native forbidden this turn) | none |
| 7 | Building teardown/removal | functionally-built building | `KingdomMaterials.OrderStrike` (`Growth/KingdomMaterials.08.StrikeOrdering.cs:23`) -> removal + salvage | works object gone, material raw count increase | **NOT wired this pass** (disclosed gap) | **covered**: second strike on absent building | **NEW**: `teardown-setup`/`teardown-check` (`Harness/KingdomTeardownNativeProvider.cs:18-19`) | this pass, unexecuted | save/cold-load leg |
| 8 | Changing/replacing buildings (upgrade tiers) | staged plot, funded | `KingdomUpgrade*`/`KingdomArchitectureStamper` transitions | phase, output identity | NOT covered | NOT covered | `KingdomUpgradeSourceProvider.cs`, `KingdomUpgradeStageProvider.cs` (names suggest partial coverage; not read in depth this pass) | not re-verified | **likely DEFECT** for non-heart tier changes — needs a follow-up pass to confirm |
| 9 | Heart expansion (rungs 1-4) | founded heart | envelope-authority transition (issue #141) | rung/plot rect | NOT covered | NOT covered | **NONE** merged to dev yet (fix lives on `fix/issue-141-heart-authority`, not this branch) | see `review-b1efe18-findings.md` | **DEFECT: unreachable on dev** until #141 merges; even then road-ingress gap is itself unresolved |
| 10 | Heart expansion (rung 4->5, arcology) | rung 4 heart | `TryAuthorizedTransition` heart branch | n/a | n/a | n/a | **NONE** | n/a | **DEFECT**: same-rect renovation is structurally refused by `Transitions.cs:56`'s `LotSize==rung` assumption (see `camp-authority-trace.md` Part 3 §3) — a real production defect, out of scope here |
| 11 | Raids (contact/launch/death/recovery) | founded, defence | contact -> launch -> resolution -> recovery | outbox, casualties | not verified | veto/recovery paths exist | `KingdomRaidContactNativeProvider.cs`, `KingdomRaidLaunchNativeProvider.cs`, `KingdomRaidMasterTurnNativeProvider.cs`, `KingdomRaidRecoveryGuardsNativeProvider.cs`, `KingdomRaidRecoveryTurnNativeProvider.cs`, `KingdomRaidRecoveryReturnNativeProvider.cs`, `KingdomRaidOutboxNativeProvider.cs` | prior evidence, not re-run | full multi-raid/multi-city interaction |
| 12 | Multiple cities | founded seat + second claim | second-city founding | `ClaimedZones.Count>1` | NOT covered | NOT covered | **NONE found** among the 30 `[KingdomScenarioVerbProvider]` classes on dev | **NONE** | **DEFECT: unreachable** — every native fixture surveyed founds exactly one city |
| 13 | Stockpile/materials (deposit, overflow, strike salvage) | dedicated stockpile | deposit -> overflow -> (this pass) strike salvage | raw census | not verified | overflow path covered | `KingdomDepositOverflowNativeProvider.cs` (deposit/overflow); **this pass** exercises strike-salvage return | prior + this pass, unexecuted | exact salvage material identity vs spent material not independently confirmed (see report limits) |
| 14 | Water (dedication, maintenance) | dedicated reservoir | fill -> maintenance cadence | volume, departure evidence | not verified | `KingdomWaterMaintenanceDepartureEvidence.cs` suggests a path | `KingdomWaterMaintenanceNativeProvider.cs` | prior evidence, not re-run | full scarcity/downgrade lane (see memory: partial native proof already exists) |
| 15 | Forage/camp | camp ground | forage -> stock | raw census | not verified | not verified | **NONE found** among the 30 providers (forage fixtures exist as DevTests-only per earlier session, not native-verb-gated) | **NONE** | **DEFECT/unreachable in-game**: forage's own native seam is not among the surveyed providers |
| 16 | Travel (present/away) | founded, pause | travel out -> pause -> resume | continuity receipt | not verified | pause fault paths exist | `KingdomScenarioTravelProvider.cs`, `KingdomScenarioPauseController.cs` | prior evidence (#122), not re-run | full away-leg economic drift (see `hotfix142` scope-fix chain) |
| 17 | Master settlement plan (resume) | paused master | resume verdict | `SemanticPassActive`/resume state | not verified | not verified | not found among surveyed providers as a dedicated verb (resume logic lives inside travel/pause seams above) | see `master-resume-semantic-defect` memory | **unreviewed this pass** — needs its own driver audit |
| 18 | Seal/inheritance (roadless spatial capture) | camp, no street connection | seal pending -> capture | seal state | not verified | fail-closed refusal paths | `KingdomSealRoadlessNativeProvider.cs` | prior evidence (#131), not re-run | full inheritance/legacy-carry lane |
| 19 | Subsidence | claimed zone, roof debt | rung step -> announcement | subsidence receipt | `KingdomSubsidenceRungSaveProvider.cs` exists (save-specific) | not verified | `KingdomSubsidenceNativeProvider.cs`, `KingdomSubsidenceRungNativeProvider.cs`, `KingdomSubsidenceRungSaveProvider.cs` | prior evidence, not re-run | interrupted-mid-step path |

## Summary counts

- **Rows**: 19 (named lanes from the assignment plus stockpile/water/subsidence/seal already
  implemented in production).
- **Has a real native driver (verb exists)**: 13 (#1,2,3(owned),5,6,7(new),11,13,14,16,18,19 —
  #3 and #6's Quickstart half are owned-lane, not independently re-verified this pass).
- **Source-pinned only, no driver**: 0 rows counted this way — pins were not substituted for
  behavioural coverage per the rule; where only pins exist they are noted as gaps, not credited.
- **DEFECT / unreachable**: 5 — #4 (resident departure/leave), #9 (heart rungs 1-4, not on
  dev), #10 (heart rung 4->5, structurally refused), #12 (multiple cities), #15 (forage, no
  native verb found).
- **Unreviewed (out of this pass's time budget)**: #8 (building tier changes beyond teardown),
  #17 (master resume as its own driver). Both need a dedicated follow-up pass, not assumed
  covered.

This matrix is a first bounded pass, not an exhaustive audit of every Core/Growth/World file;
rows 8 and 17 in particular need deeper reading than this session had time for.
