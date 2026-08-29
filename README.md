# The Thousand and First

A kingdom-founding expansion for Caves of Qud. Found a faction with a water rite, claim
ground, and let a settlement grow through arrivals, thirst, and chronicle — the first
polity of the post-injunction age.

**Status: 0.2.0 work in progress toward a v1.0 test candidate.** Every reopened positive v1
experience and polity row now has a bounded production owner or an expressly ruled stronger
supersession. Architecture's current static census is 141 buildings, 131 plotted building plans,
86 palettes, 514 maps, 356 plans, 359 exact bindings, 408 tiers, 530 variants, and 2,120 goldens;
runtime art uses 84 verified vanilla tile references and zero custom bitmap paths. Final serialized
integration gates remain pending until the tree freezes, so earlier suite counts remain historical
receipts rather than current-tree proof. The current snapshot stages 2480 production C# sources and
a 2507-file cold-install inventory; it has 357,421 physical lines, and 0 files still breach the
strict 300-line cap. The hardening sequence decomposed 154 oversized authorities. Its snapshot
digest must be regenerated after the last source freezes, and the required human
`docs/STRUCTURE_REVIEW.json` review is still open. Nine focused one-survey cases pass at source
scope. The last retained final 7,743-case Qud-referenced/source checkpoint and 173-case portable
suite do not sign later fan-in. Clean commit `19fb8ee` has the latest retained
native result: fresh-profile founding, 17/17 self-tests, one production architecture sample,
save/cold-load, repeat self-tests, and a clean log. That result does not sign later changes. Full
native protocol/gallery, current-revision compatibility/performance, exact structural review, final
native architecture preview, and private Steam subscription/install remain open. This tree is not
a release candidate. [Current
implementation and evidence](docs/STATUS.md) records exact results and remaining gates without
conflating automation with playtest proof.

Every formerly positive “later”, “post-v1”, or author-deferred direction was reopened on
2026-08-27. This changes the v1 work queue, not the evidence bar: accepted implementation and
manual gates stay explicit in
[docs/V1-UNDEFERRAL.md](docs/V1-UNDEFERRAL.md).
Exact old-actor continuation, actors walking unloaded ground, ideology-only war, strategic armies,
pooled stock, unwitnessed conquest/casualties/death, and invisible loss remain rejected product
shapes rather than hidden backlog.

Built so far: founding by rite (wish or the founder's basin), territorial claims with adjacency
including downward, citizen enrollment, the two-ledger reputation mirror, water-gated growth
with a founder-set water detail and upkeep, the thirst ladder (warnings → emigration →
withering → recovery), settlers with names, origins, homes and work, districts, city style read
from the ground, commissioned construction in reserved typed lots whose declared compatible plans use
authored maps and material/technology-gated tiers, clearance-as-extraction materials, lodging that
asks who will live beside whom, creeds and the five channels belief moves through, a world that keeps
time whether you are in it or not — works that run on days and labour rather than on visits, a
settlement that settles back toward the level its own works honestly carry, and the brink that
holds every irreversible consequence open until you are told about it — provoked raids with
four exact answers (tribute, envoy, physical fight, or named-work muster), seeds and authored crop
rows, physical larders, daily rations, favoured meals and food industry, trade charters and
caravans, a second and third owned city with exact local stores and bounded route authority,
exile and return, the dated two-register chronicle, a homecoming report, and the Charter ability
that fronts all of it. Reopened v1 code also owns two named civic voices, optional remembrance,
explicit offices, staffed loci, fixed witness works, First Guest choice/hosting, First Feast
practice, curiosity and civic leads, body history, artifact recognition, a manual communal rite,
evidence-derived site practices, vocation services, one finite integrated Guest's Feast, and an
attended prepare-save-for-removal transaction. Bounded prior/rival polities now have typed profile
revisions, finite guard/patrol/trader/envoy/courier/warband/migrant cohorts, three-city semantic
traffic, Trade-owned consignments, physical hospitality, caused diplomacy/conflict, exact escrow,
shared attention, visible loaded death gates, and fresh-id exile/return/refound authority. These
are code-scope claims; native behavior and play quality still require the numbered protocol.

Plots are reservations, not buildings. A reserved `LotId` can hold different compatible authored
plans; exact `(type, actual size)` bindings choose the map, palette, fixtures, pose, and tier.
There are three separate lanes: an `UpgradesTo` tier grows automatically only inside the standing
work's frozen exact binding; an authored same-type, same-size plan transition is an explicit
founder choice; retype or resize is a fresh siting. The first two preserve `LotId`; the third mints
a new one. See [the extension guide](MODDING.md#plots-reserved-lots-and-authored-buildings).

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
licensed integration checks pass again. There are no required mod dependencies. If exact
Hearthpyre 2.2.3 is enabled and loaded first, a dependency-gated typed shard discloses its
parasang/sector ownership and lets the founder reject or bind overlapping ground before any
water is spent. Other Hearthpyre versions leave the core unchanged. Qud Industry 0.3 remains
capability-based because the audited release exposes no typed API.

MIT licensed. Caves of Qud is a trademark of Freehold Games, LLC; this is an unofficial community project, not affiliated with or endorsed by Freehold.
