# The Thousand and First

A kingdom-founding expansion for Caves of Qud. Found a faction with a water rite, claim
ground, and let a settlement grow through arrivals, thirst, and chronicle — the first
polity of the post-injunction age.

**Status: 0.2.0 release candidate.** On 2026-08-25, automated live smoke against Qud 1.0.5
(core 2.0.211.51) founded two cities in a fresh world, saved and cold-loaded twice, moved the
seat between cities, passed all 17 in-game self-checks, and produced clean mod logs. Runtime
code under test is commit `99133f6a1b24f3be652903e16576ddd7bb929230`. The full human protocol
in [TESTING.md](TESTING.md) and a private Steam subscription/install pass remain release gates;
automation is not a manual playtest.

Built so far: founding by rite (wish or the founder's basin), territorial claims with adjacency
including downward, citizen enrollment, the two-ledger reputation mirror, water-gated growth
with a founder-set water detail and upkeep, the thirst ladder (warnings → emigration →
withering → recovery), settlers with names, origins, homes and work, districts, city style read
from the ground, commissioned construction on staked or automatic plots, clearance-as-extraction
materials and a merge-by-key building catalogue with footprints and skins, lodging that asks who
will live beside whom, creeds and the five channels belief moves through, a world that keeps
time whether you are in it or not — works that run on days and labour rather than on visits, a
settlement that settles back toward the level its own works honestly carry, and the brink that
holds every irreversible consequence open until you are told about it — provoked raids with
three answers, trade charters and caravans, a second city and the water manifest between them,
exile and return, the dated two-register chronicle, a homecoming report, and the Charter ability
that fronts all of it.

Cross-run realm inheritance is explicit opt-in. Its Options checkbox defaults off and must be
enabled before creating a new world; leaving it off does not consume an eligible sealed realm.
When enabled, only layout and history cross worlds—never items, liquids, or charge.

- Design document: published artifact "The Thousand and First" (DLC-scale design, research-grounded).
- [VISION.md](VISION.md) — where this is going and the rules it holds itself to. The rules are
  the product, not guardrails: where these shapes exist in other games, they are the loved part.
- [STANDARDS.md](STANDARDS.md) — engineering charter (vanilla conformance, depth standard, three-layer testing).
- [TESTING.md](TESTING.md) — the current playtest protocol.
- [docs/RELEASING.md](docs/RELEASING.md) — frozen-package, private-item, subscribed-test, and
  public Workshop release procedure.
- `DevTests/` — compile gate + NUnit suite (hidden from the mod loader; `#if TAF_TESTS` guarded).
  Run `DevTests/build.ps1` and `DevTests/test.ps1`.

Requires Caves of Qud v1.0.5, core build 2.0.211.51. Newer builds are unverified until the
licensed integration checks pass again. No dependencies; Hearthpyre and Qud Industry are planned
optional enhancements, never requirements.

MIT licensed. Caves of Qud is a trademark of Freehold Games, LLC; this is an unofficial community project, not affiliated with or endorsed by Freehold.
