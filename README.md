# The Thousand and First

Found a faction in Caves of Qud, raise settlements from reserved ground, and govern what survives
after you leave. Water, food, labour, materials, roads, trade, threats, civic memory, and physical
works remain part of the same world instead of becoming a detached management screen.

**Status: 0.2.0 pre-release source; the v1.0 Alpha public playtest has not shipped.** This checkout
is under active integration and is not an installable release candidate. Current evidence and open
gates live in [docs/STATUS.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/STATUS.md);
historical test counts do not sign later changes.

## What the mod adds

- Founding by water rite, territorial claims, settlement stages, named citizens, homes, work,
  offices, creeds, civic choices, and a dated chronicle.
- Sims-Settlement-style plots: lots reserve typed space; authored buildings occupy them and can
  renovate, expand, or be replaced as materials, skills, technology, culture, and need change.
- Qud-readable architecture with distinct settlement styles, creed variants, functional rooms,
  roads, utilities, civic works, and tiered material law. Current runtime art uses verified vanilla
  tile references or intentional glyphs; no copied game art ships in this repository.
- Physical water, crops, food, meals, larders, trade cargo, routes, multiple cities, raids,
  diplomacy, rival/prior polities, and visible cohorts.
- World-time simulation with bounded catch-up, explicit pressure and recovery, save-safe receipts,
  and no requirement to visit on a fixed real-world schedule.
- Optional cross-world legacy. Layout and history may carry only when enabled before world
  creation; items, liquids, charge, and actor identity do not.

The mod remains single-player. It does not add multiplayer or a user-moderation surface.

## Supported environment

The release target is Caves of Qud **v1.0.5, core build 2.0.211.51**. Newer game builds are
unverified until licensed integration checks pass again. There are no required mod dependencies.

Hearthpyre **2.2.3** is an optional, exact-version integration when it loads first. Other
Hearthpyre versions leave core behavior unchanged. Qud Industry 0.3 has no typed integration in
the audited build.

## Install or playtest

Do not install arbitrary files from a source checkout. Use a tagged release package or the public
Steam Workshop item when one exists, then follow [PLAYTESTING.md](PLAYTESTING.md) for:

- Steam and manual installation;
- first-world and Kingdom Quickstart paths;
- save backups, upgrades, rollback, and uninstall;
- known Alpha limits; and
- a useful bug-report checklist.

Public Alpha is not published yet. Maintainers track its immutable version/package freeze in
[ALPHA-RELEASE-PLAN.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/ALPHA-RELEASE-PLAN.md).

## Save safety

Back up saves before every Alpha install or update. Keep exactly one enabled copy with manifest ID
`r_ThousandAndFirst`; a local copy and Workshop subscription together make the loaded source
ambiguous. Exit Qud before replacing or removing files. Removing the mod does not convert an
existing modded save into a mod-free save.

## Report and contribute

- Test feedback and bugs: [GitHub issue forms](https://github.com/AussieWarGod/thousand-and-first/issues/new/choose)
- Support and diagnostic checklist: [SUPPORT.md](SUPPORT.md)
- Contributor setup, checks, rights, and review: [CONTRIBUTING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/CONTRIBUTING.md)
- Building and plot extensions: [MODDING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/MODDING.md)
- Supported public API: [docs/API.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/API.md)
- Vulnerabilities or sensitive evidence: [private security advisory](https://github.com/AussieWarGod/thousand-and-first/security/advisories/new)

Do not publish saves, player profiles, DLLs, extracted game assets, decompiled source, account
identifiers, or unredacted logs. Contributions are accepted under the repository's MIT license;
see [LICENSE](LICENSE), [NOTICE](NOTICE), and [CODE_OF_CONDUCT.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/CODE_OF_CONDUCT.md).

## Maintainer references

- [VISION.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/VISION.md) — product direction and non-negotiable design laws.
- [STANDARDS.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/STANDARDS.md) — engineering and evidence contract.
- [docs/ARCHITECTURE.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/ARCHITECTURE.md) — state, identity, transaction, and extension boundaries.
- [TESTING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/TESTING.md) — authoritative native playtest protocol.
- [docs/RELEASING.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/RELEASING.md) — private candidate, Alpha, full release, and Steam procedure.
- [docs/ASSET_PROVENANCE.md](https://github.com/AussieWarGod/thousand-and-first/blob/main/docs/ASSET_PROVENANCE.md) — vanilla references, original art, and preview provenance.

Caves of Qud is a trademark of Freehold Games, LLC. This is an unofficial community project, not
affiliated with or endorsed by Freehold Games.
