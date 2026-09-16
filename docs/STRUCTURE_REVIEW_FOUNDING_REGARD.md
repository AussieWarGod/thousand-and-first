# Founding reputation structural review

Automated source review at production input `40477cb7d5b5664b89466cc0fb8c36cabc7adf69`,
compared with public `de8317dbab0357a4159e6228d31d9ccc8b789711`. All 3,107 production
paths were compared: 3,102 byte-identical, four changed, one added, none removed. Census:
440,850 physical lines; 1,450 direct XRL imports; zero files at or above 300 lines.
Inventory SHA-256: `8dd650d010f8dd998d2922f21cc99b37ff79e456a6c2a67880d908bdaf5f58b9`.

| Changed responsibility | Review |
| --- | --- |
| `KingdomFounding.00.FirstFoundingRegistration` | Removes the moved codec bound and corrects the founding contract comment; registration and identity authority stay intact. |
| `KingdomFounding.01.FirstPublication` | Carries the frozen format version from capture to publication. Existing exact transaction rechecks, step marker, and later feeling projection remain in order. |
| `KingdomFounding.02.FoundingStandings` | Engine adapter captures eligible native reputation once, refuses invalid polity authority, and resolves existing faction endpoints. Only matching prepared roots are published; outgoing policy is not inferred. |
| `KingdomFoundingRegardRules` | New engine-free codec/publication rules own canonical ordered v1/v2 snapshots, strict UTF-8, size/relationship bounds, exact subset validation and independent root copies. Legacy v1 remains observation-only. No fallback truncation or conflicting-state overwrite. |
| `KingdomPolityIncidentState` | Canonical constructor default for an existing option field. Wire reader still explicitly reads every saved value; historical zero-default normalization and conflicting-state quarantine are preserved. |

No saved field was added, reordered or retyped. The internal pending faction string property
admits a version-two payload; completed cities retain their existing serialized standing ledgers.
The new helper is not a public extension surface. Faction lookup, reputation reads, ownership
checks and projection remain at the engine boundary; pure tests execute codec and publication
counterexamples without engine stubs.

The original warm/cold native run proves real first founding, independent civic adjustment,
native personal spillover, ordinary construction, snapshot preservation, and another commission.
It uses disclosed synthetic reputation changes and two owned defensive-AI fixture NPCs. It does
not prove historical founding interruption or culture-weighted population selection. Structural
review does not broaden those behavioral claims. Exact native evidence is in STATUS.md.

Retained comparison and census: local evidence archive
`founding-regard/40477cb7/structural-review-1/result.json`. Full per-path hashes are retained,
including unchanged files; review is not inferred from counts alone.
