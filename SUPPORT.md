# Support

The Thousand and First is an unofficial community mod. Support is best-effort through GitHub; do
not contact Freehold Games for mod-specific problems.

## Player help

Read [the Alpha playtesting guide](PLAYTESTING.md) first. It covers supported Qud versions,
installation, duplicate-copy removal, first founding, upgrades, rollback, and uninstall.

Use the repository's [issue chooser](https://github.com/AussieWarGod/thousand-and-first/issues/new/choose):

- **Playtest feedback** for balance, readability, architecture, controls, pacing, or a session that
  felt wrong without one clear defect.
- **Bug report** for reproducible incorrect behavior.
- **Compatibility report** for another Qud build, platform, storefront, or mod interaction.
- **Feature proposal** for a player outcome not already covered by the vision.

Before reporting, restart Qud once, confirm the manifest version, and check that only one copy with
ID `r_ThousandAndFirst` is enabled. A report should name install source, Qud marketing/core build,
platform/store, profile/save kind, complete load order, numbered steps, expected/actual result, and
whether save/reload changes it.

## Sensitive reports

Do not put secrets, personal paths, account names, full saves/profiles, crash dumps, DLLs,
decompiled source, or copied game assets in public issues. Redact `Player.log` and screenshots.

Report code execution, file-system impact, exposed secrets, or other security-sensitive evidence
through a [private GitHub security advisory](https://github.com/AussieWarGod/thousand-and-first/security/advisories/new).
See [SECURITY.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/SECURITY.md) for scope and response expectations.

## Contributor help

Start with [CONTRIBUTING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/CONTRIBUTING.md).
Public checkout-only and engine-free checks do not
need proprietary Qud files. Licensed native checks require your own local game installation; game
files must never be committed or attached to a pull request.
