# Alpha Playtesting Guide

This guide is for tagged release packages and the Steam Workshop build. The repository's current
`0.2.0` working tree is not a release package. Supported target: Caves of Qud v1.0.5, core build
2.0.211.51.

## Before installing

1. Exit Caves of Qud.
2. Back up saves you care about. On default Windows installs, back up the relevant profile/save
   data below `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud`.
3. Keep the package receipt or download that produced the save. Alpha rollback means restoring
   both the save backup and the exact mod version that wrote it.
4. Remove or disable duplicate copies. Exactly one enabled mod may use manifest ID
   `r_ThousandAndFirst`.

Never upload a full save, profile, or unredacted log to a public issue.

## Steam Workshop install

Once the public Alpha item exists:

1. Subscribe to **The Thousand and First [ALPHA]** in Steam Workshop.
2. Remove any manually installed copy of The Thousand and First.
3. Launch Qud, open **Mods**, enable the mod, and restart Qud when prompted.
4. Confirm the Mods screen shows the expected manifest version. The first public Alpha must show
   `0.3.0`.

If the item is subscribed but absent, restart Steam and Qud before reporting it. Include operating
system, storefront, Qud marketing/core version, and whether any local copy remains.

## Manual release install

Manual installs are for a tagged release package supplied by a maintainer, not a Git checkout.

1. Exit Qud.
2. Extract the complete package as one direct child of either default Windows mod root:

   - `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods`
   - `%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Local\Mods`

3. Verify `manifest.json`, `LICENSE`, and `preview.png` sit directly inside that one child folder;
   they must not be nested under a second package folder.
4. Remove older local copies and unsubscribe from the Workshop copy while testing manual bytes.
5. Enable the mod in Qud and restart.

Do not merge a new package over old files. Replace the whole folder while Qud is closed.

## Start a useful first test

Use a fresh, non-Tutorial, non-Daily world for the first report.

### Kingdom Quickstart

The v0.3 Alpha target adds a separate **Kingdom Quickstart** game mode:

1. Choose **New Game** → **Kingdom Quickstart**.
2. Build or select a character through Qud's normal character flow.
3. Choose Reedwake (salt marsh), Riftside (desert canyon), or Saltwake (salt dunes), then embark.
4. Confirm a small civic heart is founded through the normal founding transaction and finite,
   physical charter supplies are present. They are starter objects, not free production or a
   citizen grant.
5. Open the **Charter** ability and follow its current offers. Save, quit to desktop, reload, and
   revisit the heart before expanding the test.

An optional passive charter advisor is controlled by a Mods option before world creation. The
advisor grants no labour, civic support, defence, or loot. Changing that option later does not
retroactively spawn or remove one. Kingdom Quickstart never imports a prior realm.

If the tagged v0.3 Alpha does not show this mode after the required restart, report a loader or
package bug instead of using debug wishes to conceal it.

### Ordinary founding

Ordinary Qud game modes remain available. In an ordinary world, a founder's basin can occasionally
appear at vanilla tier-1 merchants. Put at least 8 drams of pure fresh water in it, use its
**found a settlement** inventory action, and complete the rite. Brine or another liquid is refused
without spending it. This slower route is useful for economy and compatibility testing.

## What to test first

- Founding and the Charter surfaces with controller and keyboard.
- One save → quit → desktop → reload cycle immediately after founding.
- Water warnings and recovery without intentionally destroying the only test save.
- One S plot, then one upgrade or plan transition; confirm the plot reserves space while the
  building supplies function and visible architecture.
- Food storage or crops, one named citizen's home/work relationship, and one road connection.
- A second city, trade route, rival cohort, inherited realm, or hosted arcology only after the
  small founding loop is stable.

[TESTING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/TESTING.md) is the
exhaustive maintainer protocol. Alpha testers may submit a
focused observation without completing it.

## Upgrade, rollback, and uninstall

For every update: exit Qud, back up the save, and replace the whole manual folder or let Steam
finish the Workshop update before launching. Never load a valued save while two copies are
enabled.

To roll back, restore the backed-up save and its matching mod package together. Loading a newer
save with older mod code is not a supported rollback.

To uninstall:

1. Exit Qud.
2. Unsubscribe from the Workshop item and remove every local copy.
3. Restart Qud and confirm the mod is absent.

Uninstalling does not rewrite existing saves into mod-free saves. Retain a matching package if
those saves must remain playable.

## Report useful evidence

Choose the [playtest feedback](https://github.com/AussieWarGod/thousand-and-first/issues/new/choose),
bug, or compatibility form. Include:

- TAF manifest version and install source: Workshop or manual package;
- Qud marketing version and core build;
- operating system/storefront;
- fresh world, existing save, or upgraded save;
- complete enabled-mod list and load order;
- smallest numbered reproduction, expected result, actual result, and frequency;
- whether save/reload changes the result; and
- a redacted `Player.log` or screenshot when safe.

State unavailable evidence as unavailable. Do not guess a pass. Use a
[private security advisory](https://github.com/AussieWarGod/thousand-and-first/security/advisories/new)
for code execution, file-system impact, secrets, or sensitive personal data.

## Alpha expectations

Alpha means systems and save contracts are testable, not that balance, decoration, every
compatibility pairing, or final native human review is complete. The Workshop listing and package
must say Alpha. No Alpha package may be described as the production-final release.
