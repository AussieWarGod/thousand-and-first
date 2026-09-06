# SteamProbe

Read-only Workshop probe; proves SDK access.

## Prerequisites

- Windows host (not WSL/Linux).
- Steam running, logged into an account owning 333640.
- Built by `WorkshopSteam.csproj`; `QudManaged` = installed `Managed` dir.
- Launcher sets `SteamAppId`/`SteamGameId`=333640, adds `<Qud>/CoQ_Data/Plugins/x86_64` to `PATH` for `steam_api64.dll`.

## Usage

    TafWorkshopSteam <itemId>

One canonical decimal id; no sign or leading zeros.

## Output

One JSON line; the SDK may also print native lines; take the last line parsing as a JSON object with `status`.

Read-only; 15 s bound; `ownerMatch` only, no raw ids; null `manifest_id`/`manifest_version` means absent - inspection only, never publish readiness.

## Exit codes

0 ok, 2 usage, 3 uninitialized, 4 login/license, 5 app mismatch, 6 query failure, 7 not found, 9 exception.
