> Also posted on issue #57 (2026-09-09). Decisions for the author are listed at the end.

# Beta gap report — issue #57

**Final.** Re-read against the shipped 0.3.2 bytes on 2026-09-09. Baselines: tag `v0.3.2` = `8c80a0b`;
integration `origin/dev` = `6f5d6a4`; the candidate the native proofs were taken on is `6e67fb3`
("chore(release): prepare 0.3.2 Alpha", PR #88), and `docs/ALPHA_CANDIDATE.json` names
`candidateCommit` `d00a532` — the later receipt-binding commit, a descendant of the proved bytes.
Every claim cites a file, a persona, a `TESTING.md` row ID, an issue, or a run ID. "Unverified" marks
anything not checkable read-only.

## What 0.3.2 changed since the draft

- **Tagged and published.** Staging run `34327428688` finalized green; the public Workshop run
  `34341601300` ("Release 0.3.2 Alpha", PR #94) was **still in progress** at the time of writing
  (10:46Z snapshot) — its conclusion was **unverified**. Finalize-after-verify was fixed by PR #92;
  the public flip landed in PR #93.
  **Post-release update (2026-09-09, later than the snapshot above):** public run `34341601300`
  completed with automatic finalization — `-Verify` 12:26Z reports `SubscribedInstallationVerified`,
  `-Finalize` 12:36Z exits `0` with `status=SubscribedInstallationVerified`, `attemptFinalized=true`;
  public item 3794797472 retained attempt `0002` (`docs/STATUS.md`, "Public 0.3.2 — published and
  finalized").
- **Native proofs on the candidate bytes.** Six-profile Quickstart boot/save/cold-load matrix **6/6**
  at seed `#43101` (`DevTests/QUICKSTART-BOOT-TESTS.md`, rows canyon/dunes/marsh × advisor yes/no);
  five unattended personas green — `found-first-city`, `claimed-light-native-check`,
  `bounty-fetch-native-check`, `guide-topics-native-check`, `first-guest-native-check`.
- **Issues closed today:** #54 (Fetch delivery never completes, PR #32), #55 (unattended observers
  for light, guide topics and first guest), #56 (exact custody for stockpile deposits, PR #84).
- **New 0.3.3 Alpha issues opened:** #89 (boot-matrix cold-load transport ~16 min/profile), #90
  (harness flake: `Harness/KingdomScenarioTestGround.cs` `Strip` removes objects but not the worldgen
  `faction` zone property, so a seed-dependent village on the shared start parasang makes production
  correctly refuse the founding — fixture defect, not player-facing), #91 (run the Windows
  pure/portable CI leg only on `main` and release tags).
- **Nothing in 0.3.2 moved a Beta gate to closed.** Confirmed by re-listing: **#58–#70 are all still
  OPEN** in the Beta milestone.

## Headline

**The protocol for the whole ladder is written. The automation reaches the first rung of five, and it
is handed that rung rather than climbing to it.** The ladder is `Camp=0, Steading=1, Village=2,
Town=3, City=4` (`Core/KingdomRules.cs`; the same words are the `stagedigest` vocabulary in
`TESTING.md`), gated on exactly two numbers — population and dedicated water capacity
(`Core/KingdomRules.TradeAndGrowth.cs`: Steading 5/16, Village 12/64, Town 25/256, City 50/1024).
There is no "hamlet", and **arcology is not a rung**: it is heart rung 5 and a megastructure needing
City stage, four claimed zones, `arclight` and a crowned capital (`RuntimeData/KingdomBuildings.xml`;
`TESTING.md` row `136j`). The furthest rung any unattended persona reaches natively is **Camp**, and
there is exactly one `stage=` expectation in the whole matrix: `found-first-city.persona:14` binds
`stagedigest:OK~stage=Camp`, its own comment explaining this is deterministic because nothing
advances between `realize` and the digest. Every stage above Camp is **assigned, not played** —
`Harness/KingdomSubsidenceNativeFixture.cs` writes `System.Stage = stage` under the comment
"Synthetic setup derives this field; it does not execute production stage advancement", and
`DevTests/KingdomSubsidenceNativeHarnessSourceTests.cs` pins that literal text. **No test anywhere —
engine-free or native — calls `KingdomGrowth.UpdateStage`**: re-verified on `6f5d6a4`, the only
source references are its declaration (`Growth/KingdomGrowth.z18.StageAndShops.cs:49`), its single
caller (`Growth/KingdomGrowth.z01.Activation.cs:182`) and doc comments. Source tests reach higher
only as arithmetic (`DevTests/KingdomSubsidenceRulesTests.cs`, `DevTests/KingdomRulesTests.cs`,
`DevTests/KingdomHeartBasinCapacityTests.cs`) — all engine-free. The only stage *transition* any
persona observes is downward from an injected City (`subsidence-rung-native-checks.persona`),
disclaimed at `TESTING.md:662` as seeded setup that "crosses no stage breakpoint."
**For #68 the honest starting number is 1 rung of 5, 1 city of 3, 1 zone of 4, and zero away-legs.**

## Gate table — Beta gates #58–#70 (all OPEN)

| Gate | What it demands | Evidence that exists | Gap | Size |
|---|---|---|---|---|
| **#58** numbered TESTING protocol | Every numbered ID passed or waived, waivers ≤64 and named | 740 step rows exactly (re-counted on `6f5d6a4`), plus QSB1–QSB6 at `TESTING.md:842-847`. Six-profile boot matrix **6/6** at seed `#43101` on the 0.3.2 candidate; QSB2's water-capacity case passed natively 2026-09-07 (`TESTING.md:849`) | **No per-row status ledger** — one wholly-unchecked execution index (`TESTING.md:484`), everything else prose. QSB1 open (`:195`, `:222`), QSB3–QSB5 open (`:855`). API v3 rows **124i–124p are at `TESTING.md:2455-2462`, not 2309-2316 as #58's body states** — correct the issue | **XL** — the umbrella with no ledger to report into |
| **#59** evidence record at schema 4 | Schema-4 record, genuine human names/UTC, eight artifacts committed | `docs/RELEASE_EVIDENCE.example.json` defines all eight slots | `docs/ALPHA_CANDIDATE.json` is **still `schemaVersion` 2** after 0.3.2; `docs/release-evidence/` holds **two** files (`architecture-quality-ledger.json`, `preview-source.png`) against eight required | **M** — mechanical once inputs exist |
| **#60** subscriber play + fresh Steam transfer | Attended subscribe → play → graceful quit → relaunch → load, on a client that never held the bytes | None. The public 0.3.2 upload (run `34341601300`) is a *publish*, not a subscriber-play proof. `docs/CROSS_VERSION_UNATTENDED.md` states the owned-process stop "is **not** graceful Quit evidence" | Graceful-quit and fresh-transfer are missing *capabilities*; #58's audit note says track them as tooling gaps | **L** — part genuinely human |
| **#61** final native preview review | In-game screenshot, generative false, hash bound into candidate and record | `docs/release-evidence/preview-source.png`; `previewSha256` is bound in `docs/ALPHA_CANDIDATE.json`; `Tools/capture-game-window.ps1` exists | Provenance artifact unauthored; open decision on recapturing once #41/#42 change the opening screen | **S** |
| **#62** clean-checkout release gate, zero skips | Portable audit + release check in test and release mode, zero skips | `Tools/release-check.sh`, `Tools/test-release-check.sh`, `Tools/portable-check.sh`; Portable-checks CI green on `dev` (`34341316057`) and on the release branch (`34340938429`) | Three package fixtures skip on a foreign-owned temp dir inside nested user namespaces (0.3.1-only acceptance; not re-tested for 0.3.2 — **unverified**). Same 124i–124p external-assembly caveat. #91 will narrow where the Windows leg runs, which changes *when* this gate is exercised | **M** |
| **#63** duplicate public/private mod-ID warning | A candidate pass with no duplicate-ID warning, by an approved route | `docs/ALPHA_CANDIDATE.json` records both `workshopId` 3794797472 and `privateWorkshopId` 3796495680 — the colliding pair, unchanged by 0.3.2 | Blocked on a **ruling**, not code: temporary unsubscribe vs a second machine/account. Never answered | **S** — a decision plus a written route |
| **#64** durable home for evidence bundles | No committed doc cites a scratch or host-mount path; every cited artifact resolves | `docs/release-evidence/` is the durable location | `docs/STATUS.md` and `TESTING.md:854` still cite temp-dir and Windows-mount scratch locations, and `_notes/RESEARCH-ALIGNMENT-AUDIT-2026-09-01.md` — cited by `docs/STATUS.md` — is **untracked** (`_notes/*` ignored, not allow-listed) | **M** — copying is mechanical; the untracked-ledger ruling is the judgement |
| **#65** raid open set | Same-blueprint custody, swallowed throws, interrupted placement, ordinary raids, save/reload across a raid, durable body recovery, **away-heartbeat raids** | 11 raid personas (`raid-launch-native-a`/`-b1`, `raid-contact`/`-death`/`-death-veto`, `raid-outbox`, four `raid-recovery-*`, `raid-master-turn`) — re-counted on `6f5d6a4`; `docs/STATUS.md` lists the same open set verbatim | All seven open. "Heartbeat coverage for raids while the player is away" is the *same* missing away-leg #68 needs — build once, spend twice | **L** — plus the Beta-vs-v1 ruling |
| **#66** subsidence durable accounting | Settlement-local accounting, fractional debt, committed-but-pending departures, mid-rung interruption, driver, keyed reports, native partial quota, save/load across a step | 5 subsidence personas, two tagged `save-load`; `Tools/verify-scenario-rung-load.py` is the D5 load witness; `TESTING.md` row `77h` (`:2103`) is the one row testing save/reload mid-slide | All open. `TESTING.md:617` names the gate; nearby rows still list partial 1-of-5 steps and carrier-committed departures as known **BUGS**. The issue warns the earlier artifact is unreachable from this object store | **L** |
| **#67** archive migration repair | Intent / attempting / settled readable across the version step, or a Beta-covering waiver | Cross-version legs 2026-09-09: donor **PASS**, stage-source **PASS**, downgrade **PASS** (#53 comment); personas `upgrade-source-donor`, `upgrade-stage-source`, `upgrade-downgrade-check`, `upgrade-source-reserved` | Repair unstarted; #87 still leaves the 0.3.1-save + Reserved-inheritance leg refused. Real decision: does the Alpha-to-Alpha waiver extend to Beta | **M** repaired / **S** waived |
| **#68** end-game reachability suite | Camp→arcology, multi-tile, multi-city, attended **and** away, save/cold-load between rungs, all unattended | Reuse exists: `yield-frames` (cap 240), load witnesses (`Tools/prepare-scenario-load.py`, `verify-scenario-load.py`, `verify-scenario-rung-load.py`), the cross-version copier. Protocol fully written — rows `10`, `55f`, `80`/`80c`, `16m`/`16t`/`16t1`, Pass 25 (`79`–`79j`), Pass 30, `136j`–`136j.5` | Everything above Camp unautomated, and five capabilities absent: an away leg, a second-zone leg, a second-city leg, a persona-expressible reload (`TESTING.md:770`), and a sealable `arcology` verb — `Tools/scenario_profile.py` marks `arcology` **deliberately unsealable** because "fresh scenario profiles cannot own those facts" | **XL** — the Beta headline |
| **#69** channel, version, private candidate lane | Eight ordered steps from channel ruling to public release | `.github/workflows/release.yml`; tag→publish pipeline landed (#24, #40) and proved end-to-end by 0.3.2 (`34327428688` staging, `34341601300` public), with the finalize-condition fix in PR #92; `Tools/workshop_upload_plan.py`, `Tools/workshop-steam-upload.ps1` | Nothing in the tree names a Beta version string or channel; the publisher still hard-codes two item IDs; the private Beta item does not exist | **L** — the 0.3.2 run de-risks the mechanics; the ID/channel work is untouched |
| **#70** four named source findings | Chronicle receipt registry, register loss on begin/arrest telling, negative/future checkpoint validation, pending rung blocking durable death accounting | `docs/STATUS.md` still names checkpoint validation and the pending-rung death-accounting finding as live. #84 hardened deposit custody but is a *different* finding | No cross-walk against #7–#13 has been done, so how many of the four survive is **unverified** | **M** — the cross-walk is cheap |

## Ladder coverage matrix

Cells name **unattended personas** first; where only a written, unrun `TESTING.md` row exists, the
cell says so. "present" = a leg that hands the engine its render loop (`yield-frames`); "away" = a leg
that leaves the zone and returns through the catch-up path.

| Rung | Single-tile, present | Single-tile, away | Multi-tile | Multi-city |
|---|---|---|---|---|
| **Camp** | `found-first-city` (`stage=Camp`, pop 0); `claimed-light-native-check`; `quickstart-native-checks`; `guide-topics-native-check`; `first-guest-native-check`; `bounty-fetch-native-check`; `founding-heart-lifecycle`/`-native-checks`; `water-maintenance-native-check`; six-profile boot matrix 6/6 | none — protocol only (`79`, `45h`) | **impossible at this rung**: Camp holds 1 zone (`Growth/KingdomClaimRules.cs`) | none — protocol only (`16m`) |
| **Steading** | none — protocol only (rows `10`, `14d`, `36`) | none — protocol only (`16e`) | **impossible at this rung**: Steading holds 1 zone | none |
| **Village** | none — protocol only (`21c`, `25a`, `86`, `89c`) | none — protocol only | none — protocol only (`80`, `80a`–`80d`, `81`–`81g`; 2-zone ceiling) | none |
| **Town** | reached only *downward* from an injected City: `subsidence-rung-native-checks`, `subsidence-rung-save-native-check` | none — protocol only (`79b`, `79f`, `117`) | none — protocol only (3-zone ceiling) | none — protocol only (`48a`, `48a1`) |
| **City** | injected only (`Harness/KingdomSubsidenceNativeFixture.cs`); the real precondition is `136j` (crowned capital, great court, four claimed zones) — unrun | none — protocol only (`77f`, `79d`, `90q3`) | none — protocol only (`136j` four claimed zones) | none — protocol only (`16t`, `16t1`, `16u`, `16v`, `138c`) |
| **Arcology / beyond** | none. Source contracts and staged compile only (`Growth/KingdomHostedArcology.*`); the `arcology` harness verb exists but is **unsealable** in a persona; the only arcology persona is a photograph (`arch-endgame-arcology-atrium.persona`) | none — protocol only (`136j.3`, `136r`) | none — protocol only (`136j.2`) | none — protocol only (`136j.4`) |

**Away is empty at every rung.** `advance` deliberately keeps the engine out of `XRLCore.PlayerTurn`
(`Tools/scenario_profile.py`) and `yield-frames` hands back the render loop (cap 240). Neither is
*leaving the zone and coming back*. A grep for "catch-up"/"catchup" over `Harness/` and `Tools/`
returns nothing, and no `away` verb or request exists. The governing behaviour is written in
`TESTING.md` ("no clock in this mod caps elapsed time any more") and `Growth/KingdomSubsidence.cs`
("The settlement lives whether the founder is there or not… what changes at a homecoming is only that
somebody is told"). Pass 25 and Pass 30 are hand-run.

**Multi-tile and multi-city are downstream of the ladder, not parallel to it.**
`Growth/KingdomClaimRules.cs` gives Camp and Steading one zone, Village two, Town three, City four;
row `80c` is the refusal text. The harness cannot express a second zone anyway: `START` is one
parasang plus one cell with z fixed at 10 (`Tools/scenario_profile.py`), and every founded harness
check asserts `ClaimedZones.Count == 1` and `NonSeatSettlementCount == 0`. There is also a known
aggregation defect waiting at the far end: `_notes/COVERAGE-GAP-MAP.md` records that settlement-wide
numbers are recomputed from one zone's survey, so in a two-zone city walking into the mine zone would
overwrite the city's supported level — row `81c` is exactly the test that would catch it.

## Blocking dependencies

**On the path to #68:**

- **#42 four founders at turn 1** — the sharpest dependency. Camp→Steading needs population ≥ 5 and
  16 drams, and the founding basin already supplies the 16 (`Growth/KingdomPlotHeartRules.cs`), so
  **five settlers is the only missing input for the first rung**. Today no unattended run gets them:
  the harness founds through `founding-first-city`, not Quickstart, so the camp has **no roof** and
  nobody joins (`Harness/KingdomFirstGuestNativeChecks.cs` asserts exactly that bedless camp and a
  guest still awaiting an answer); and the two personas that advance a real clock switch growth off
  outright (`Harness/KingdomUpgradeStageChecks.cs`, `Harness/KingdomWaterMaintenanceNativeChecks.cs`
  both set `r_TAF_OptionGrowth=No`). Four founders at turn 1 sidesteps both.
- **#90 harness founding flake** — now a hard prerequisite, not a nuisance: a rung suite is many long
  founding runs, and a seed-dependent worldgen village on the shared start parasang refuses the
  founding outright. Fix `Strip` to clear the `faction` zone property before any suite is written.
- **#41 rung-1 canvas camp** — the store is retained at every heart rung at the same rite-relative
  offset, so all five heart maps move in one PR. A suite written first binds to maps about to change.
- **#43 storehouse family and rung-scaled dedication cap** — City needs capacity ≥ 1024 and the heart
  basin only reaches that at rung 5 (16/48/160/512/1024, `Growth/KingdomPlotHeartRules.cs`), while
  heart rung 4 itself requires City. A commissionable storehouse family is how a run buys capacity.
- **#48 dry containers at the moot and the court** — fixes the per-rung heart store totals the
  suite's assertions bind to.
- **#89 cold-load transport ~16 min per profile** and **#79 same-process scenario cycling** — not
  correctness, but this suite is many long runs with a cold-load between every rung; at today's
  transport cost the suite is impractical before both land.
- **#78 founding-time arcology-obstruction warning** — forces the question the suite must answer
  anyway: the arcology's actual current footprint, never reconciled against the placeholder.
- **#82 / #87 (cross-version)** — gate the cold-load-across-versions leg and share the profile copier
  the suite reuses. #87 leaves the 0.3.1-save + Reserved-inheritance load UNKNOWN.

**Not on the path (do not block #68):** **#44**/**#47** forage, **#46** store filters and auto-sort,
**#49** embodied sort porter, **#45** parking the chest proposal, **#50** doc sweep — camp content and
UX, no rung gate. **#74** causal imported diplomacy, **#76** imported-polity endpoint carrier, **#77**
smith-anvil rung lint, **#80** Charter readability, **#81** state-aware Reed guide, **#86** verdict
label wording, **#91** CI leg placement — real work, no bearing on reachability. **#83 (D01–D15)** —
none is a rung gate. **#75 migrant petition → resident admission** is the closest call: a real
resident consumer would be an honest second way to grow population, but the ordinary arrival cadence
(`3600 + 600 × Population` ticks, `Core/KingdomRules.TradeAndGrowth.cs`) suffices.

## Proposed Beta suite shape

Adding a verb is cheap: providers are discovered by the `[KingdomScenarioVerbProvider]` attribute
through the engine's own type scan (`Harness/KingdomScenarioVerbProvider.cs`,
`Harness/KingdomScenarioVerbRegistry.cs`), so no harness table needs editing — a persona names its
verbs in `VERBS=`. Two constraints shape everything below: a sealed third-party verb **takes no
argument** (`TESTING.md:1466`), and `advance` is "a real wait, not a fast-forward"
(`Harness/KingdomScenarioAdvance.cs`) capped at 10000 turns a call.

0. **A reload leg a persona can express** — prerequisite for everything after it. `TESTING.md:770` is
   explicit that the load witnesses are "a separate two-profile operator route… not something a
   `.persona` can express", and advance state is deliberately non-durable across a reload
   (`Harness/KingdomScenarioAdvance.cs`). #68 demands cold-load *between rungs*. **New seam, and the
   highest-leverage item in this report.**
1. **`climb-steading-native-check`** — the first *earned* transition: found, run the clock with growth
   ON, assert `stagedigest:OK~stage=Steading`. The write path already works stationary — `advance`
   fires `EndTurnEvent` → `KingdomHeartbeat.OnEndTurn` → `KingdomSemanticDispatcher` →
   `KingdomGrowth.OnZoneActivated` → `UpdateStage`, once per 1200-tick city day — and the stage-up
   `Popup.Show` is safe because the sealed script raises `Popup.Suppress` for the whole run
   (`Harness/KingdomScenarioAutoRunner.cs`). *Missing:* a roofed founding and a way for arrivals to
   join unattended — see #42. **New seam unless #42 lands first.**
2. **`depart-return-native-check`** — the first away-leg: leave the claimed zone, let world time pass
   unloaded, return, assert the catch-up bill equals honest elapsed (`TESTING.md` rows `79`, `16p`,
   `90q2`'s `debt=` invariant). *Missing:* a leave/return verb. **New seam.** This one verb also
   unblocks #65's away-heartbeat raids and #66's absence cases.
3. **`climb-rung-ladder-native-check` ×3** (Steading→Village→Town→City) — one persona per rung, each
   loading the previous rung's sealed profile via item 0 and ending in a save. This discharges "no
   rung is only ever proved in a single continuous session". *Missing:* a **generic** rung witness —
   `Tools/verify-scenario-rung-load.py` hard-codes one persona and its expected strings.
4. **`claim-second-zone-native-check`** — multi-tile, legal only from Village. Prove
   `CityStorageCapacity` sums across zones and that the ladder reads the city's casks, not the
   walked-in zone's (`Growth/KingdomGrowth.z18.StageAndShops.cs`; rows `81b`, `81c`) — this is the
   test for the aggregation defect named above. *Missing:* a second-zone claim verb and a
   `START`/request shape admitting more than one zone; `Harness/EmbarkModules.xml` already anticipates
   "the 3×3 region a future arcology persona needs". **New seam.**
5. **`found-second-city-native-check`** — multi-city under `MaxOwnedSettlements = 3`
   (`Core/KingdomSettlementTopologyRules.cs`), then a seat swap and the fourth-founding refusal (rows
   `16t`, `16t1`). *Missing:* a second-founding step in `Harness/KingdomScenarios.xml` and a per-city
   digest. **New seam.**
6. **`arcology-reserve-native-check`**, then **`arcology-traverse-native-check`** — heart rung 5 at a
   crowned City capital with four claimed zones and `arclight` (`136j`), then the 27-zone interior
   (`136j.2`). *Missing:* the `arcology` verb is **unsealable by rule** (`Tools/scenario_profile.py`)
   — reaching this rung by play is exactly what removes the objection, so the rule needs revisiting
   once items 1–5 land. `136j.5` (inspect all 27 districts at native tile and text scales) is human
   review no persona can discharge: scope it out explicitly. **New seam plus a rule change.**
7. **`arcology-away-native-check`** — `136r`: leave each active pair and its waiting cargo unvisited
   for several world-days. Depends on item 2.

Alternating attended/away and cold-load between rungs are **properties of the manifests above**, not
extra personas: each rung persona runs once with a `yield-frames` leg and once through the away verb,
and each ends in a sealed profile the next one loads.

## Open questions for the author

1. **May the suite seed, or must it play?** Camp→City is roughly 760 in-world days at the shipped
   arrival cadence — about 912,000 turns, ~92 maxed `advance` calls — and `advance` is a real wait
   (`Harness/KingdomScenarioAdvance.cs`). Playing it honestly is likely hours of wall clock per run,
   on top of #89's ~16 min cold-load per profile. Seeding population and proving only the
   *transitions* is minutes, but it is the same move `TESTING.md:662` currently disclaims as
   "explicitly seeded test setup". **This is the one decision that shapes every ticket below.**
2. **How should an unattended run get its first five settlers?** The harness founds a roofless camp
   and nobody joins without a roof; the first guest then waits on an answer
   (`Harness/KingdomFirstGuestNativeChecks.cs`). A settler-seed verb, a roofed harness founding, an
   auto-admit option, or #42's four founders — which is lawful?
3. **#65 and #66 — which items are Beta-blocking and which are v1?** Both issues say the ruling shapes
   the scope and should be made before anything is re-derived. Nothing here can be sized until it is.
4. **#67 — does the Alpha-to-Alpha save waiver extend to Beta?** Waived is S, repaired is M, and Beta
   is the first release where players carry worlds forward in numbers.
5. **#63 — which route for the duplicate mod-ID warning?** Temporary unsubscribe with verified
   restoration, or a second machine/account. The permission request has never been answered, and
   0.3.2 shipped with the colliding pair still recorded.
6. **#69 — Beta version string and channel.** Nothing in the tree names one, and #59/#61/#62 all bind
   to candidate bytes that do not exist until this is settled. The 0.3.2 run proved the pipeline; only
   the ID/channel work remains.

## Recommended first three Beta tickets, in order

1. **#90 harness founding flake** (S) — a rung suite is many founding runs; fix `Strip` first.
2. **Persona-expressible reload leg** (new ticket under #68, item 0 above) — every rung persona
   depends on it, and #66's save/load cases reuse it.
3. **#42 four founders at turn 1** (XL, already filed) — turns the first *earned* rung climb from
   impossible into a one-settler problem, and unlocks `climb-steading-native-check`.
