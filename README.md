# The Thousand and First

A kingdom-founding expansion for Caves of Qud. Found a faction with a water rite, claim
ground, and let a settlement grow through arrivals, thirst, and chronicle â€” the first
polity of the post-injunction age.

**Status: slice 0.1â€“0.2 (pre-release).** Founding (wish or the founder's basin item),
territorial claims with adjacency, citizen enrollment, the two-ledger reputation mirror,
water-gated growth with fetching and upkeep, the thirst ladder (warnings â†’ emigration â†’
withering â†’ recovery), settler provenance, the dated two-register chronicle, and the
Charter ability (Status / Chronicle / As others tell it / Standings).

- Design document: published artifact "The Thousand and First" (DLC-scale design, research-grounded).
- [VISION.md](VISION.md) — where this is going and the rules it holds itself to. The rules are
  the product, not guardrails: where these shapes exist in other games, they are the loved part.
- [STANDARDS.md](STANDARDS.md) â€” engineering charter (vanilla conformance, depth standard, three-layer testing).
- [TESTING.md](TESTING.md) â€” the current playtest protocol.
- `DevTests/` â€” compile gate + NUnit suite (hidden from the mod loader; `#if TAF_TESTS` guarded).
  Run `DevTests/build.ps1` and `DevTests/test.ps1`.

Requires Caves of Qud v1.0.5+ (build 2.0.211.51 verified). No dependencies; Hearthpyre
and Qud Industry are planned optional enhancements, never requirements.

MIT licensed. Caves of Qud is a trademark of Freehold Games, LLC; this is an unofficial community project, not affiliated with or endorsed by Freehold.
