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

The current unreleased raid-contact census reports 3046 staged production C# files and 431,481 physical lines,
with 0 at or above the strict 300-line cap. Direct `XRL` imports remain in 1414 files;
none exceeds the line limit. Exact staged source inventory digest:
`3f0d1dde39c6a07cfd6cbad9f9888833b9ca0837bceec7918cb910260e1e9688`.
The canonical stage still contains 3077 files; public 0.3.1 installed bytes remain unchanged.

The [complete raid bridge](/tmp/taf-raid-contact-structure.boMu2H/raid-production-bridge.json)
enumerates every current path/hash and compares every actual parent Git blob at `cd6dedc`:
3044 unchanged, two modified, no additions or removals. Unchanged sources inherit the
retained stock review below, not a fresh deep read. Root and an independent AI reviewer
read both complete changed files and their affected debit/contact boundaries. The existing
water receipt retains active-survey authority, settlement-wide reserves and full accounting;
its internal exact-store selector cannot fall back to another vessel. Contact owns a real
survey scope through commit and compensation, and refuses failed reservations before mutation.
No saved fields, wire layout or public API changed. The current binding records this scoped
review, not ordinary play, save/load or unrelated raid acceptance.

## Retained initial-stock census

The released stock correction reported 3046 staged production C# files and 431,441 physical lines,
with 0 at or above the strict 300-line cap. It contains 1414 files with direct `XRL` imports;
0 of those exceed the line limit. Exact staged source inventory digest:
`9d9eb6416014c7257a26fa08178d8f738e44dd46a295ea3357f32b7739faf1b0`.
The generated staging list contains 3077 files; it does not prove installed or subscribed content.
The [complete stock bridge](/tmp/taf-quickstart-stock-structure.E4chRX/stock-production-bridge.md)
compares every then-current path against actual Git blobs from clean startup commit
`1c1c2bc54b4d11f0912f210b39baa89e635c157d`:3044 unchanged,one modified,one added,no removals.
Only the two stock production files received fresh scoped review; unchanged files inherit the
retained review chain, not a fresh deep read. A callback-free physical-child identity rule now
guards initial food/material totals; legitimate stacks, continuation and saved protocols remain
unchanged. Developer alias-fixture review remains separate from this production binding.

Retained startup digest `7e3fb7521f445e16b0740329966e369fa51d91fd768237bdffb5291d3540c651`
passed all six actual profile/advisor boot/save/cold-load pairs and all twelve strict save/load
checkers, with exact owned stops and independent idle checks. Its four-mode/ABI compile21734,
managed21635 13603/4990 zero skips and Tools69769 501 passes are prior-byte evidence, not stock
correction validation. This review claims no complete current stock compile, managed or native
pass. See [STATUS](STATUS.md) and [retained native evidence](/tmp/taf-quickstart-native.sYl4Dz/README.md).
Historical serializer evidence remains retained; ordinary rendered play, graceful Save-and-Quit
and desktop Quit, corrected subscribed delivery, full historical saves and public release remain
separate gates. The old stock binding is retained in Git; `docs/STRUCTURE_REVIEW.json` now binds
the raid correction above. [Scope and correction evidence](STRUCTURE_REVIEW_0_3_1.md) preserve inherited
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
its mechanical line debt; the semantic ledger then bound the exact-inventory review to its
`f9815fff…` digest above. That retained review is ancestry, not the current isolated draft binding.

That hardening sequence semantically decomposed 144 additional oversized authorities,
bringing the cumulative total to 154. That is 25 more decompositions since checkpoint `2cb97fc`,
19 more than checkpoint `d3fc4b9`, 16 more than checkpoint `b049c17`, and 13 more than hosted
checkpoint `1c2d619`.
[ARCHITECTURE.md](ARCHITECTURE.md#split-authority-map) maps the logical authorities to
their current source families. Numeric lexical prefixes appear only where the canonical stage's
filename order must preserve original declaration, reflection, or serialized-metadata order; they
do not create a second authority. That checkpoint was measurable progress, not release signoff:
0 line-cap failures remained, and the semantic ledger then bound its exact-inventory review
to that retained digest. Any staged source change invalidates the binding and requires a new census and review
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
