# The Thousand and First

A kingdom-founding expansion for Caves of Qud. Found a faction with a water rite, claim
ground, and let a settlement grow through arrivals, thirst, and chronicle — the first
polity of the post-injunction age.

**Status: slice 0.1–0.2 (pre-release).** Founding (wish or the founder's basin item),
territorial claims with adjacency, citizen enrollment, the two-ledger reputation mirror,
water-gated growth with fetching and upkeep, the thirst ladder (warnings → emigration →
withering → recovery), settler provenance, the dated two-register chronicle, and the
Charter ability (Status / Chronicle / As others tell it / Standings).

- Design document: published artifact "The Thousand and First" (DLC-scale design, research-grounded).
- [STANDARDS.md](STANDARDS.md) — engineering charter (vanilla conformance, depth standard, three-layer testing).
- [TESTING.md](TESTING.md) — the current playtest protocol.
- `DevTests/` — compile gate + NUnit suite (hidden from the mod loader; `#if TAF_TESTS` guarded).
  Run `DevTests/build.ps1` and `DevTests/test.ps1`.

Requires Caves of Qud v1.0.5+ (build 2.0.211.51 verified). No dependencies; Hearthpyre
and Qud Industry are planned optional enhancements, never requirements.
