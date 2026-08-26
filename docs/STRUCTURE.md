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

`Tools/check-structure.py --json` currently reports 1410 staged production C# files and 250,683
physical lines. Of those, 46 exceed 300 lines, 0 are exactly 300, and therefore 46 fail the strict
cap; 22 exceed 1,000, 3 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`f46df9b37ad71633925adba335768474d8a9b19acd3b980a03f5281b7d68e675`. The census reports
544 files with direct `XRL` imports; 43 of those exceed the line limit.

The current hardening sequence semantically decomposed 125 additional oversized authorities,
bringing the cumulative total to 135. That is six more decompositions since checkpoint `2cb97fc`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. This is measurable progress, not release signoff: 46 line-cap
failures and missing `docs/STRUCTURE_REVIEW.json` exact-inventory human review still
block release. Any staged source change invalidates this digest and requires a new census and
review binding.

Automation cannot decide whether a type owns one coherent responsibility or whether its engine,
serialization, public-API, and third-party seams use suitable protocols. Release mode therefore
also requires `docs/STRUCTURE_REVIEW.json`, bound to the exact staged source inventory digest.
Copy `docs/STRUCTURE_REVIEW.example.json` only after a human review; replace every placeholder and
record concrete evidence in both notes fields. Schema 1 accepts no exceptions. Changing that rule
requires an explicit author amendment to Addendum 9, not a tooling allowlist.

Decompose debt one owned state machine or transaction at a time. Preserve serialized names and
public contracts, add characterization and migration fixtures first, then rerun compile, pure,
source, native save/reload, and behavior gates appropriate to the moved boundary.
