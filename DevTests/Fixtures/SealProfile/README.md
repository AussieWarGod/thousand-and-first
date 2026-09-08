# Historical seal profile fixtures (v0.3.1 writer bytes)

These four files are checked-in seal text produced by seal writer code that is
byte-identical to the public Alpha tag `v0.3.1` (`a46b5ad`). The identity was proved on
this branch with

```
git diff a46b5ad HEAD -- Core/KingdomSealRecord.Writing.cs Core/KingdomSealFormat.cs \
    Core/KingdomSealRecord.cs
```

which is empty: the framing (`taf-seal <schema>` / `sha256` / `length` / strict-JSON
payload), the key set and the writer are unchanged since that tag. The only seal change
in this branch is the reader bound at `Core/KingdomSealRecord.Profile.cs:13`, which
widened `profile_schema` from `[0,1]` to `[0,2]`. Two digest inputs are also unchanged for
these files: `KingdomPolityRules.LegacySealPhenotypeDigest` still stamps `schema=1` for a
canonical body pool, and `KingdomPolityProfileRules.LegacyProfileProvenanceDigest` was not
touched.

They were written once by a throwaway `TafTests` case on that code and are never
regenerated. `DevTests/KingdomSealProfileHistoricalFixtureTests.cs` pins each SHA-256 as a
constant, so any rewrite fails the suite.

| File | Bytes | SHA-256 |
| --- | --- | --- |
| `schema0-0.3.1.seal` | 2243 | `cb7f08c053d06715c19fc7459640dea5c2c0060e9eba1eeed939881c59bcc8a9` |
| `schema1-0.3.1.seal` | 2388 | `3df0f48836f656b7ed4bc2d039557afea3c36a62e6f8b9287150bf9cf1a6d6fd` |
| `schema1-promoted-0.3.1.seal` | 2396 | `b9a1ca8c5f8eecee6d37fe24c733de89583e0bddfcd9083e6f9bd22c1ae23262` |
| `reserved-receipt-0.3.1.seal` | 225 | `50e27ae390c743d9586f88536f1fe5d0c68d7448ba653aff7259f29c137a549d` |

## What each file is

- `schema0-0.3.1.seal` - `profile_schema` 0, the unresolved institutional import: technology
  band 0, empty `canonical_body`, both provenance digests empty.
- `schema1-0.3.1.seal` - `profile_schema` 1, a living stage captured against a published
  foundation: technology band 6, `canonical_body` `["human","snapjaw"]`, real
  `source_profile_digest` and `profile_provenance_digest`.
- `schema1-promoted-0.3.1.seal` - the same record retired and promoted (`status` `promoted`,
  resolved), the shape a save carries as `LegacyText`.
- `reserved-receipt-0.3.1.seal` - the matching `Reserved` receipt for target game
  `target-game`, the shape a save carries as `ReceiptText`.

## What they prove, and what they do not

They prove the forward read is an identity: a 0.3.1 seal parses under the widened bound,
recomposes byte-for-byte (`Compose() == LegacyText`), keeps its schema, technology band,
body pool and both digests, survives a transition copy without being re-stamped, and
validates as a saved inheritance shape without repair.

They do not prove any native path: no save/load, no engine run, no Steam-installed build.
They also do not prove the backward direction - a `profile_schema` 2 record is not readable
by 0.3.1 at all, which is documented in `CHANGELOG.md` and `PLAYTESTING.md`.
