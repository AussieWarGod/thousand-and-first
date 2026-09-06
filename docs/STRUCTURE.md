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

## Current isolated draft census

The current isolated draft census reports 3045 staged production C# files and 431,086 physical lines,
with 0 at or above the strict 300-line cap. It contains 1414 files with direct `XRL` imports;
0 of those exceed the line limit. Exact staged source inventory digest:
`ef84f9a05d894bdbc281e20aa1b5f02f45f4b0ca5a96771ffbeb3da903ad3f3f`.
The generated staging list contains 3076 files; it does not prove current
installed or subscribed content. Strict four-mode compilation and ABI checks pass against inputs
`987e8c0de7d08217ee256f9e30eeea4eb317f9fe27c988032c8e1bcc4969c2f6`;
full suites pass 13,498 Taf / 4,885 Portable cases with zero skips;440 Python Tools tests pass.
Historical nested archive and103 retained real serializer cases pass;
full historical-save and ordinary-gameplay acceptance remain open.
The exact-inventory semantic review is now bound in `docs/STRUCTURE_REVIEW.json`; the structure
release gate passes. [Scope and correction evidence](STRUCTURE_REVIEW_0_3_1.md) retain earlier
review provenance and open functional limits. This is not the complete release gate.

## Retained beta hardening checkpoint — `7d331fe8`

At this earlier checkpoint, `Tools/check-structure.py --json` reported 2951 staged production C# files and 420,997
physical lines. Of those, 0 exceed 300 lines, 0 are exactly 300, and therefore 0 fail the strict
cap; 0 exceed 1,000, 0 exceed 2,000, and 0 exceed 5,000. Exact staged source inventory digest:
`f9815fff2a1cf4389ecd42b733645b0611b31bbc8b58c96fae7d1636099e81b1`. The census reports
1376 files with direct `XRL` imports; 0 of those exceed the line limit.

The Claude launch-order foundation retains the reviewed `bf319c2` subsidence baseline. Its
one-file production diff exactly matches the independently reviewed artifact: launch prepares
and publishes; existing resume owns per-actor creation, placement, and activation. No new saved
field or custody authority is introduced. Two bounded native launch cases now prove multi-actor
callback ordering and different-blueprint replacement quarantine with retained evidence; ordinary
play, same-blueprint and interrupted-placement custody remain open. The roster XML correction and
six development Harness files do not change this production C# census or its retained semantic
review. Their separate source/native evidence is recorded in [STATUS.md](STATUS.md).
The earlier 2026-09-05 subsidence delta retained `b572ede` and its inherited
outbox, Quickstart, raid-zone, and polity reviews. Root and an independent reviewer inspected
the two production changes: the engine-free completion boundary and its actual Reckoning
callbacks. Existing bookkeeping and reached-rung work precede the guarded summary; required
work is never caught as presentation failure. No saved fields or wire layout changed.
Partial-step debt, committed-but-pending departures, and failures inside rung execution remain
open. Thirteen managed cases test this bounded completion contract, not native departure or
ruin effects. Development-only fixtures remain excluded; Claude's raid-custody patch stays
separate. These are source/engine-contract reviews, not native playtests.

At that checkpoint, no staged production source breached the strict physical-line cap. This cleared
its mechanical line debt; `docs/STRUCTURE_REVIEW.json` retains the exact-inventory semantic review
bound to its `f9815fff…` digest above, not the current isolated draft.

That hardening sequence semantically decomposed 144 additional oversized authorities,
bringing the cumulative total to 154. That is 25 more decompositions since checkpoint `2cb97fc`,
19 more than checkpoint `d3fc4b9`, 16 more than checkpoint `b049c17`, and 13 more than hosted
checkpoint `1c2d619`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. That checkpoint was measurable progress, not release signoff:
0 line-cap failures remained, and `docs/STRUCTURE_REVIEW.json` binds its exact-inventory semantic
review to that retained digest. Any staged source change invalidates the binding and requires a new census and review
binding.

## Release review contract

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
