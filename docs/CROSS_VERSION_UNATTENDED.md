# Unattended cross-version developer evidence

This is the active v2 protocol. Every leg is sealed, scripted, owned and unfocused. No keyboard,
seed entry, wishes, Charter menu, manual polling, save or quit step is required. No native result
was produced while authoring this patch. Preparation, source pins and synthetic unit tests are
not native acceptance; the root must run the integrated candidate's real engine legs.

Old runtime is fixed at `v0.3.1`, commit
`a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e`; engine is `2.0.211.51`, save format 408.
All current runtime, recipes and overlay inputs come from one full immutable candidate commit.
Recipes use the existing persona grammar under `Tools/personas/cross-version/`, outside ordinary
matrix discovery: they require these dedicated sealed inputs and must not run through generic
current-runtime `run-personas.sh` preparation.
The PR #31 real-null marshalling correction is included in the authoring base `8a56181`.

## Gates before native execution

```bash
python3 -m unittest discover -s Tools/tests -p '*_test.py'
python3 Tools/check-harness-registration.py
dotnet run --project DevTests/TafTests.csproj -v q --nologo
dotnet run --project DevTests/PortableTests.csproj -v q --nologo
Tools/gate.sh
```

Current-only compilation does not establish old-overlay compatibility. Each old-source/reader
profile must compile its new-name overlays with the exact old production and original old Harness
at native startup, with no MODERROR/MODWARN or other unexpected diagnostics.

## Genuine old donor, Reserved save, current load

```bash
TAF_UPGRADE_PIN=FULL_40_CHARACTER_INTEGRATED_CANDIDATE_COMMIT
TAF_UPGRADE_GAME='/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ.exe'
TAF_UPGRADE_DONOR="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
TAF_UPGRADE_SOURCE="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
TAF_UPGRADE_TARGET="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"

python3 Tools/prepare-upgrade-profile.py source-donor --candidate "$TAF_UPGRADE_PIN" \
  --destination "$TAF_UPGRADE_DONOR" --seed '#1012037'
python3 Tools/run-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --root "$TAF_UPGRADE_DONOR" --game "$TAF_UPGRADE_GAME"

python3 Tools/prepare-upgrade-profile.py source --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_UPGRADE_DONOR" --destination "$TAF_UPGRADE_SOURCE" --game "$TAF_UPGRADE_GAME"
python3 Tools/run-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --root "$TAF_UPGRADE_SOURCE" --game "$TAF_UPGRADE_GAME"

python3 Tools/prepare-upgrade-profile.py upgrade --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_UPGRADE_SOURCE" --destination "$TAF_UPGRADE_TARGET" --game "$TAF_UPGRADE_GAME"
python3 Tools/run-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --root "$TAF_UPGRADE_TARGET" --source "$TAF_UPGRADE_SOURCE" --game "$TAF_UPGRADE_GAME"
```

The donor driver uses actual first founding, actual citizenship enrollment/census of one explicitly
synthetic human NPC, real seal opt-in, actual retirement/promotion, and real Primary save. Old
empty-camp sealing cannot resolve canonical phenotype, so the driver uses the real seal option
to opt in after the real census. It does not hide diagnostics or manufacture profile/receipt fields.

The second old profile receives the donor's **entire Synced history**, not only a promoted record.
It starts a fresh ordinary test world with import disabled. After proving a pristine Empty state,
the driver enables import and calls the unchanged production `Initialize()`. That authority
creates the canonical Reserved receipt and real lease. The driver then arms the original native
save observer and calls real `SaveGame("Primary")`. Donor linkage, actual snapshot, save hashes,
receipt, game IDs, selected stage and promoted bytes must all agree.

This is deliberately a **live Reserved developer fixture**, not normal inherited embark or site
installation. The old `ResumeAfterLoad` validates/reacquires its real lease without advancing a
plain Reserved state. Current native load must prove exact raw authority before repair/normalization
and after LoadGame, no repairs, canonical legacy, and actual retained stage readability.

`detached-transition` is not safely scripted in this slice. New preparation explicitly refuses it;
no attended fallback and no substituted Reserved test counted as detached coverage. The old initial
foundation profile versus later enrolled census needs a separate lawful fixture design.

## Current empty camp, actual old-reader rejection/fallback

```bash
TAF_UPGRADE_STAGE="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
TAF_UPGRADE_READER="$(mktemp -d /mnt/c/taf-scenario.XXXXXX)"
python3 Tools/prepare-upgrade-profile.py stage-source --candidate "$TAF_UPGRADE_PIN" \
  --destination "$TAF_UPGRADE_STAGE" --seed '#1012037'
python3 Tools/run-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --root "$TAF_UPGRADE_STAGE" --game "$TAF_UPGRADE_GAME"
TAF_UPGRADE_ORIGIN="$(sed -n '4p' "$TAF_UPGRADE_STAGE/upgrade-stage-receipt.txt")"
python3 Tools/prepare-upgrade-profile.py downgrade --candidate "$TAF_UPGRADE_PIN" \
  --source "$TAF_UPGRADE_STAGE" --destination "$TAF_UPGRADE_READER" \
  --origin "$TAF_UPGRADE_ORIGIN" --game "$TAF_UPGRADE_GAME"
python3 Tools/run-upgrade-profile.py --candidate "$TAF_UPGRADE_PIN" \
  --root "$TAF_UPGRADE_READER" --source "$TAF_UPGRADE_STAGE" --game "$TAF_UPGRADE_GAME"
```

The stage driver uses real founding, actual growth/raid options and `advance 2400`. Daily production
staging must produce a canonical living schema-2 empty-camp record before the actual Primary save.
Original realm/coordinator writers, both retained stage slots, save ownership and hashes are checked.
Pre-dispatch save/cache guards enforce profile confinement: a violation latches failure before
throwing, so a caught/retried boundary cannot produce accepted evidence. These are enforcement
guards, not passive observers; ordinary rendered UI behavior is not claimed.

The old-reader script is claimed at the real main menu before old auto-start can create a world.
It invokes the actual old parser/store, preserving each supplied slot. Schema 2 must be rejected at
the old `profile_schema` bound; an independently valid old sibling may remain readable. The report
and exact terminal log hash must agree. No newer world save is copied into or loaded by the reader.
No render-dependent observation is needed, so this protocol does not require `yield-frames`.

## Ownership, option writes and limits

`run-upgrade-profile.py` has a bounded wait and uses existing `run-scenario.ps1` quiet ownership and
the receipt-bound process controller. It stops only its exact owned process, then verifies native
evidence against retained profiles. This stop is **not graceful Quit evidence**. Cache.db is bound
only after owned-process exit; no driver closes/resets the engine cache. A partial output, timeout,
ownership uncertainty, refused row, changed receipt or native diagnostic remains failed evidence.

V2 retains the original closed **birth** Local seal. The engine's real `Options.SetOption` writes
PlayerOptions.json through `NameValueBag`; only exact mode-specific final values and native byte
format are admitted separately. Every other Local byte remains pinned; the observed options hash
and entire donor/source chain are rechecked. No reseal, blanket options exclusion or arbitrary
operational mutation is allowed. Historical v1 recipes reconstruct their original unchanged inputs.

Preserve all save/checkpoint/quick histories and backups, metadata, post-exit caches, all four seal
store folders and HighScores. The existing held-handle before/copy/readback/after hashes and full
membership checks remain mandatory. Unknown files, aliases, nonempty locks or mismatches refuse;
nothing is deleted or normalized to make a test pass. These are bounded developer save/reader
checks, not ordinary UI acceptance, full beta readiness or a public release authorization.
