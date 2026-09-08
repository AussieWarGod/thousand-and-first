# Native cross-version developer evidence

This protocol separates old-save provenance, exact transport, actual current-reader observation,
and old-reader fallback. Preparation does **not** prove compatibility. No tool publishes a release,
launches a game during preparation, repairs a source, or deletes/reseals a partial destination.

Pinned source production: tag `v0.3.1`, commit
`a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e`. Native observer ordering is restricted to engine
`2.0.211.51` / save format408. Candidate must be a full immutable commit containing these tools
and observers. A dirty worktree or a manifest version string is not runtime provenance.

## Root checks before native runs

Run from the integrated candidate checkout. The authoring handoff ran NONE of these checks.

```bash
python3 -m unittest discover -s Tools/tests -p '*_test.py'
python3 Tools/check-harness-registration.py
dotnet run --project DevTests/TafTests.csproj -v q --nologo
dotnet run --project DevTests/PortableTests.csproj -v q --nologo
Tools/gate.sh
```

The gate compiles current production and current Harness. Old-compatible overlays additionally
meet the actual old runtime compiler at native startup; require no MODERROR/MODWARN. Do not
call current-only compilation old-runtime compatibility evidence. The native copy helper's Win32
leases also need actual Windows validation; source/grammar tests do not exercise those APIs.

Freeze these variables once. Substitute the actual integrated candidate's full commit for the pin.

```bash
TAF_UPGRADE_PIN=FULL_40_CHARACTER_CANDIDATE_COMMIT
TAF_UPGRADE_GAME='/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ.exe'
TAF_UPGRADE_SOURCE="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
TAF_UPGRADE_TARGET="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
```

Preparation uses each named commit's literal `Tools/stage.sh` exclusion/metadata/asset arrays and
regular Git blobs. Source runtime and its original Harness remain old. Only seven explicitly named
new source-observer files overlay it; only three reader-observer files overlay an old-reader profile.
Upgrade runtime and full Harness come from CURRENT candidate bytes. Derived manifest/request,
seed, diagnostics options, and disabled optional Pets pack are independently reproducible inputs.
The Pets pack is disabled before sealing to meet the raw-log no-warning contract, not filtered later.

## Actual old save → current load

Run twice with fresh profile pairs: `inheritance` and `detached-transition`.

```bash
python3 Tools/prepare-upgrade-profile.py source \
  --candidate "$TAF_UPGRADE_PIN" --case inheritance \
  --destination "$TAF_UPGRADE_SOURCE" --seed '#1012037'
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/Tools/run-scenario.ps1")" \
  -Root "$(wslpath -w "$TAF_UPGRADE_SOURCE")" \
  -Game "$(wslpath -w "$TAF_UPGRADE_GAME")" -OwnAttended
```

Root creates the state using real old-version gameplay in ordinary Kingdom mode, not a fabricated
serializer roundtrip or current constructor. Keep the first world's complete save/store/death
history while selecting inheritance in a later world. For the detached case, use actual realm
return/exile that leaves a populated `PolityTransition.Legacy` in Detached phase. `LegacyText`
and this nested legacy are separate authorities and need separate evidence.

Once the desired genuine state exists, invoke `kingdom:upgrade-arm`, then save normally to Primary.
The wish only arms observation of the next real SaveGame; it does not create/advance/repair state
or call SaveGame itself. Stable supported inheritance phases may be observed, but their exact raw
phase must survive the load; an activation that legitimately changes it is not an unchanged-state
PASS. A literal Reserved pre-world transient is not automatically a playable saved-world fixture.

Quit the source game normally. Preserve `process-ownership.json`, the original log, all history,
`upgrade-save-snapshot.txt`, and `upgrade-save-receipt.txt`. Any
`upgrade-save-failure.txt` refuses. Cache.db remains untouched by the observer; its receipt uses
`cache-bind-after-quit`, and the host binds the cache only after exact owned-process exit.
Endpoint process absence alone does not claim graceful quit or uninterrupted global exclusion.

```bash
python3 Tools/prepare-upgrade-profile.py upgrade \
  --candidate "$TAF_UPGRADE_PIN" --source "$TAF_UPGRADE_SOURCE" \
  --destination "$TAF_UPGRADE_TARGET" --game "$TAF_UPGRADE_GAME"
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/Tools/run-scenario.ps1")" \
  -Root "$(wslpath -w "$TAF_UPGRADE_TARGET")" \
  -Game "$(wslpath -w "$TAF_UPGRADE_GAME")"
```

The fourth existing Continue route loads the exact copied Primary through the real LoadGame
barrier; it starts no new world. Require exactly three successful journal rows: LOAD-BEGIN,
UPGRADE-PREACTIVATION, SCRIPT-COMPLETE. Pre-activation observes original inheritance fields
before repair and selected transition/nested-Legacy identities before Normalize. The completed
LoadGame must still match the old snapshot. Actual canonical stage read must succeed with its
matching origin/legacy/lineage. No repair/disable calls or serialization errors are acceptable.

After completion, root may stop only the existing owned process using the existing controller:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/Tools/scenario-process-control.ps1")" -Mode stop \
  -Root "$(wslpath -w "$TAF_UPGRADE_TARGET")" -Game "$(wslpath -w "$TAF_UPGRADE_GAME")"
python3 Tools/verify-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_UPGRADE_SOURCE" --root "$TAF_UPGRADE_TARGET" --game "$TAF_UPGRADE_GAME"
```

Expected scoped verdict: `NATIVE UPGRADE PASS: case=...; game-id=...; candidate=...`.
The verifier revalidates retained old runtime/probes, source native capture, exact destination
request/snapshot, candidate Local inventory, ownership absence and raw native diagnostics.
An unexecuted run, success-shaped snapshot alone, missing source, or transport receipt alone fails.
Source observer pin defaults to the candidate, never the source's self-declaration. To reuse a
retained old-native save against a later candidate, root must explicitly approve its original
observer commit with `--source-probe-pin FULL_COMMIT` on both upgrade preparation and verification.

## Actual current empty camp → old-reader observation

```bash
TAF_STAGE_SOURCE="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
TAF_OLD_READER="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
python3 Tools/prepare-upgrade-profile.py stage-source --candidate "$TAF_UPGRADE_PIN" \
  --destination "$TAF_STAGE_SOURCE" --seed '#1012037'
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/Tools/run-scenario.ps1")" -OwnAttended \
  -Root "$(wslpath -w "$TAF_STAGE_SOURCE")" -Game "$(wslpath -w "$TAF_UPGRADE_GAME")"
```

Root creates a genuine current empty-camp stage, then quits normally. Read its origin from the
actual `Synced/ThousandAndFirst/Stages/<origin>.[ab].seal` filenames; do not compose a synthetic
replacement, strip fields, delete a valid sibling, or rewrite its schema.

```bash
TAF_STAGE_ORIGIN=ACTUAL_SOURCE_ORIGIN
python3 Tools/prepare-upgrade-profile.py downgrade --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_STAGE_SOURCE" --destination "$TAF_OLD_READER" \
  --origin "$TAF_STAGE_ORIGIN" --game "$TAF_UPGRADE_GAME"
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/Tools/run-scenario.ps1")" -OwnAttended \
  -Root "$(wslpath -w "$TAF_OLD_READER")" -Game "$(wslpath -w "$TAF_UPGRADE_GAME")"
```

Stay at the main menu: no new game, Continue, or newer save. Native MainMenu.Show postfix reads
the actual old parser/store before any world reconciliation. Require the report AND the terminal
`native-downgrade-reader cases=1 passed=1 failed=0` log with its exact report SHA256 after cleanup.
Then stop the reader through `scenario-process-control.ps1 -Mode stop` as above and run:

```bash
python3 Tools/verify-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_STAGE_SOURCE" --root "$TAF_OLD_READER" --game "$TAF_UPGRADE_GAME"
```

Expected result is source-dependent, not blanket stage absence. At the old pin,
`Core/KingdomSealRecord.Profile.cs:10–14` bounds profile_schema to0..1;
`KingdomSealRecord.Utilities.cs` reports OutOfBounds for2. `KingdomSealStore.Stage.cs` ReadSlot
returns null for that input, while ReadStage retains an independently valid old sibling through
Best. Thus both rejected/absent slots mean absent; a valid old sibling means that sibling survives.
The probe also requires actual empty-camp fields: schema6/profile_schema2, living, stage0, people0,
canonical_body=[unresolved], valid technology and provenance commitments. It proves stored shape
and actual old-reader behavior, not that a newer saved world can safely be downgraded.

## Exact transport scope and limitations

Copy ALL `Synced/Saves/<GameID>/` histories: Primary/Checkpoint/Quick gzip saves, their `.bak`
files, JSON metadata, Cache.db and named cache copies. Preserve all four seal-store folders
(Stages, Legacies, Receipts, Claims), including empty folders and empty lock/live markers, plus
Synced/HighScores.json. The last is real death evidence (`XRL/Core/Scoreboard2.cs:52`), not clutter.
These paths follow `XRLGame.cs:1630,2050,2111,2337` and `Core/KingdomSealStore.Paths.cs`.
Local/Session backups are not read by this explicit LoadGame(Session:false) route; source Local
runtime/compiled caches/options are never transplanted into the upgrade. Unknown Synced files,
WAL/SHM, partial writes, aliases, nonempty locks, mismatches or unsupported native APIs refuse
without deleting anything. Never discard history to make a copy pass.

Native copy holds ordinary single-link input handles and directory identities, hashes every file
before/copy/readback/after, and rechecks whole membership. Two endpoint checks include both CoQ
and CavesOfQud. Native helper has a120-second caller timeout; failed partial destinations and
plan files stay available for diagnosis. Neither hashes nor a leftover `.live` confer process
ownership. The single closed Local seal is written only after all inputs and state are complete.
These are bounded developer save/reader checks, not ordinary UI acceptance or a release gate.
