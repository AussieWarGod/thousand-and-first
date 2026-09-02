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

`Tools/check-structure.py --json` currently reports 2944 staged production C# files and 420,612
physical lines. Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict
cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`79d5be0222b22db9acc1ae0b36e9bea98e0b386c5f222c29e9b3b3b4a2a4bd3e`. The census reports
1373 files with direct `XRL` imports; 0 of those exceed the line limit.

No staged production source breaches the strict physical-line cap. This clears the mechanical
line debt; the exact-inventory semantic review required for release is supplied by `docs/STRUCTURE_REVIEW.json`, bound to the digest below.

The current hardening sequence semantically decomposed 144 additional oversized authorities,
bringing the cumulative total to 154. That is 25 more decompositions since checkpoint `2cb97fc`,
19 more than checkpoint `d3fc4b9`, 16 more than checkpoint `b049c17`, and 13 more than hosted
checkpoint `1c2d619`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. This is measurable progress, not release signoff: 0 line-cap
failures remain, and `docs/STRUCTURE_REVIEW.json` binds the exact-inventory semantic review to this
digest. Any staged source change invalidates this digest and requires a new census and review
binding.

Automation cannot decide whether a type owns one coherent responsibility or whether its engine,
serialization, public-API, and third-party seams use suitable protocols. Release mode therefore
also requires `docs/STRUCTURE_REVIEW.json`, bound to the exact staged source inventory digest.
Copy `docs/STRUCTURE_REVIEW.example.json` only after the review itself; replace every placeholder and
record concrete evidence in both notes fields. Schema 1 accepts no exceptions: exactly the
template keys, nothing else. Reviewer identity is 2–80 printable characters; each evidence note is
20–2,000 printable characters; completion uses a real second-precision UTC timestamp. Placeholder,
sentinel, example, TODO, TBD, UNKNOWN, and N/A reviewer/notes text is rejected. Changing that rule
requires an explicit author amendment to Addendum 9, not a tooling allowlist.

### Addendum 9 amendment — who may sign (author ruling, 2026-09-02)

The author ruled that the exact-inventory semantic review may be performed and signed by the AI
reviewer acting under the author's explicit authorization, recorded honestly as such in
`reviewedBy` (never as a forged human signature). The review remains real: every staged file is
enumerated, risk-weighted deep reads are recorded, findings carry file:line evidence, and every
Required finding is fixed before signing. The digest binding and the placeholder refusals are
unchanged; a fresh review binds to every new digest.

Decompose debt one owned state machine or transaction at a time. Preserve serialized names and
public contracts, add characterization and migration fixtures first, then rerun compile, pure,
source, native save/reload, and behavior gates appropriate to the moved boundary.
