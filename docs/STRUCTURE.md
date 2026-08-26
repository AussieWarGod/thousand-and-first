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

`Tools/check-structure.py --json` currently reports 763 staged production C# files and 242,999
physical lines. Of those, 122 exceed 300 lines, 0 are exactly 300, 56 exceed 1,000, 14 exceed
2,000, and 2 exceed 5,000. Exact staged source inventory digest:
`ce7e3de4e59985e4a8f2e12d85a54be89b19111b34e45639145e3159892df591`. The census reports
191 files with direct `XRL` imports; 82 of those are at or over the line limit.

The current wave semantically decomposed 49 additional oversized authorities, bringing the
cumulative total to 59. [ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical
authorities to their current source families. This is measurable progress, not release signoff:
122 line-cap failures and missing `docs/STRUCTURE_REVIEW.json` exact-inventory human review still
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
