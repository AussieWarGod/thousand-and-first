# Structural release contract

Addendum 9 of the binding building-catalogue brief requires services to stay strictly under 300
lines, own one responsibility, and communicate through protocols at boundaries before public
release. This repository does not weaken that rule by silently redefining a large file as a
non-service.

`Tools/check-structure.py` reads the exact staged production C# inventory from `Tools/stage.sh`.
Its physical-line census is a deliberately conservative proxy: every staged C# file at 300 lines
or more blocks release. Direct `XRL` imports are reported as a coupling signal, not treated as
proof of either good or bad dependency design.

```bash
# Development/CI census: reports debt, exits zero after a valid scan.
python3 Tools/check-structure.py --report

# Release gate: fails on line debt or absent/stale semantic review.
python3 Tools/check-structure.py --release
```

## Current hardening checkpoint

`Tools/check-structure.py --json` currently reports 2637 staged production C# files and 383,381
physical lines. Of those, 4 exceed 300 lines, 0 are exactly 300, and therefore 4 fail the strict
cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`d5fb01d63260dde70a994f8e17c8d282eee71686cf9f9d8d93c4eeee26e29de3`. The census reports
1204 files with direct `XRL` imports; 3 of those exceed the line limit.

The four breaching files are the Gatehouse family — `Growth/KingdomGatehouseRules.cs` (682),
`Growth/KingdomGatehouse.ProjectionEvidence.cs` (522), `Growth/KingdomGatehouse.cs` (517), and
`Growth/KingdomGatehouse.Projection.cs` (326) — docketed and adjudicated by the R3 registration
sweep. Docketed is not exempted: the release gate still fails on them, and this file states that
failure honestly rather than reporting a stale zero. The socket-transition pair that previously
also breached (`Growth/KingdomSocketTransitions.cs`, `Growth/KingdomSocketTransitionRules.cs`) was
split under the size law by repair shard S2 and no longer appears in the over-cap list.

The current hardening sequence semantically decomposed 144 additional oversized authorities,
bringing the cumulative total to 154. That is 25 more decompositions since checkpoint `2cb97fc`,
19 more than checkpoint `d3fc4b9`, 16 more than checkpoint `b049c17`, and 13 more than hosted
checkpoint `1c2d619`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. This is measurable progress, not release signoff: 4 line-cap
failures and missing `docs/STRUCTURE_REVIEW.json` exact-inventory human review still
block release. Any staged source change invalidates this digest and requires a new census and
review binding.

Automation cannot decide whether a type owns one coherent responsibility or whether its engine,
serialization, public-API, and third-party seams use suitable protocols. Release mode therefore
also requires `docs/STRUCTURE_REVIEW.json`, bound to the exact staged source inventory digest.
Copy `docs/STRUCTURE_REVIEW.example.json` only after a human review; replace every placeholder and
record concrete evidence in both notes fields. Schema 1 accepts no exceptions: exactly the
template keys, nothing else. Reviewer identity is 2–80 printable characters; each evidence note is
20–2,000 printable characters; completion uses a real second-precision UTC timestamp. Placeholder,
sentinel, example, TODO, TBD, UNKNOWN, and N/A reviewer/notes text is rejected. Changing that rule
requires an explicit author amendment to Addendum 9, not a tooling allowlist.

Decompose debt one owned state machine or transaction at a time. Preserve serialized names and
public contracts, add characterization and migration fixtures first, then rerun compile, pure,
source, native save/reload, and behavior gates appropriate to the moved boundary.
