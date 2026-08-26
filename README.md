# The Thousand and First

A kingdom-founding expansion for Caves of Qud. Found a faction with a water rite, claim
ground, and let a settlement grow through arrivals, thirst, and chronicle — the first
polity of the post-injunction age.

**Status: 0.2.0 work in progress toward a v1.0 test candidate.** Nine focused one-survey cases, the
final 7,586-case engine-free/source suite, and static architecture gates are green.
Clean commit `19fb8ee` also has a narrow current-revision native result covering fresh-profile
founding, 17/17 self-tests, one production architecture sample, save/cold-load, repeat self-tests,
and a clean log. The full human protocol and gallery, compatibility checks, structural release
gate, and private Steam subscription/install pass remain open. This tree is not a release
candidate. [Current
implementation and evidence](docs/STATUS.md) records exact results and remaining gates without
conflating automation with playtest proof.

Built so far: founding by rite (wish or the founder's basin), territorial claims with adjacency
including downward, citizen enrollment, the two-ledger reputation mirror, water-gated growth
with a founder-set water detail and upkeep, the thirst ladder (warnings → emigration →
withering → recovery), settlers with names, origins, homes and work, districts, city style read
from the ground, commissioned construction in reserved typed lots whose interchangeable plans use
authored maps and material/technology-gated tiers, clearance-as-extraction materials, lodging that
asks who will live beside whom, creeds and the five channels belief moves through, a world that keeps
time whether you are in it or not — works that run on days and labour rather than on visits, a
settlement that settles back toward the level its own works honestly carry, and the brink that
holds every irreversible consequence open until you are told about it — provoked raids with
four exact answers (tribute, envoy, physical fight, or named-work muster), seeds and authored crop
rows, physical larders, daily rations, favoured meals and food industry, trade charters and
caravans, a second city and the water manifest between them,
exile and return, the dated two-register chronicle, a homecoming report, and the Charter ability
that fronts all of it.

Plots are reservations, not buildings. A reserved `LotId` can hold different compatible authored
plans; exact `(type, actual size)` bindings choose the map, palette, fixtures, pose, and tier.
Same-set growth preserves the lot through an explicit transition, while retype or resize is a new
siting. See [the extension guide](MODDING.md#plots-reserved-lots-and-authored-buildings) for the
complete lifecycle.

Cross-run realm inheritance is explicit opt-in. Its Options checkbox defaults off and must be
enabled before creating a new world; leaving it off does not consume an eligible sealed realm.
When enabled, only layout and history cross worlds—never items, liquids, or charge.

The top-level **enable settlement simulation and new civic work** option is a reversible pause,
not a reset. Turning it off preserves the realm and committed receipts, stops automatic civic
work and new orders, and leaves reports and recovery answers readable. Turning it back on consumes
the transition first and schedules fresh automatic deadlines in the future; disabled time is not
paid out as a surprise backlog.

## Install a test build

Test only the supported game build shown below and back up any save you care about first. Exit
Qud, extract the release so `manifest.json` is directly inside one folder under either
`%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Mods` or
`%USERPROFILE%\AppData\LocalLow\Freehold Games\CavesOfQud\Local\Mods`, then enable **The Thousand
and First** in Qud's Mods screen and restart the game. Use a new non-Tutorial, non-Daily world for
the full test protocol.

Before upgrading, exit Qud and replace the whole old mod folder; do not merge files. Keep exactly
one local or Workshop copy with manifest ID `r_ThousandAndFirst`, because duplicate IDs make the
loaded source ambiguous. Existing saves retain their own mod state, but a backup is the rollback
boundary. To uninstall, exit Qud, remove every local copy, unsubscribe from any Workshop copy, and
restart. Removing the mod does not rewrite an existing save into a mod-free save; retain the same
version if that save must remain playable. Maintainers and source-build testers should follow
[the exact staging and release procedure](docs/RELEASING.md), not copy arbitrary checkout files.

- Design document: published artifact "The Thousand and First" (DLC-scale design, research-grounded).
- [VISION.md](VISION.md) — where this is going and the rules it holds itself to. The rules are
  the product, not guardrails: where these shapes exist in other games, they are the loved part.
- [STANDARDS.md](STANDARDS.md) — engineering charter (vanilla conformance, depth standard, three-layer testing).
- [docs/STATUS.md](docs/STATUS.md) — current automated evidence, implementation boundary, and
  unsigned v1.0 test-candidate gates.
- [docs/STRUCTURE.md](docs/STRUCTURE.md) — Addendum 9 census, semantic-review evidence, and
  structural release gate.
- [TESTING.md](TESTING.md) — the current playtest protocol.
- [MODDING.md](MODDING.md) and [docs/API.md](docs/API.md) — data-first extensions and the supported
  programming surface.
- [CONTRIBUTING.md](CONTRIBUTING.md) — checkout, evidence, review, provenance, and pull-request
  rules. Report reproducible problems through the [issue tracker](https://github.com/AussieWarGod/thousand-and-first/issues).
- [docs/RELEASING.md](docs/RELEASING.md) — frozen-package, private-item, subscribed-test, and
  public Workshop release procedure.
- `DevTests/` — compile gate + NUnit suite (hidden from the mod loader; `#if TAF_TESTS` guarded).
  Run `DevTests/build.ps1` and `DevTests/test.ps1`.

Requires Caves of Qud v1.0.5, core build 2.0.211.51. Newer builds are unverified until the
licensed integration checks pass again. No dependencies; Hearthpyre and Qud Industry are planned
optional enhancements, never requirements.

MIT licensed. Caves of Qud is a trademark of Freehold Games, LLC; this is an unofficial community project, not affiliated with or endorsed by Freehold.
