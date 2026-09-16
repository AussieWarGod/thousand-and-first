# Residence storage structural review

Automated review against `38dc21ed86b3dfbd98727343425b6573aa27848b`: all 3,116 production
paths compared, with 3,087 byte-identical, 25 changed, four added and none removed. Census:
441,275 physical lines, 1,454 direct XRL imports, zero files at or above 300 lines.
Inventory SHA-256: `27161ede9c5a702b8c80514469eeab77962e4da020895849ff53f9fed11bef03`.
Full comparison and census: local evidence archive
`home-map/residence-storage-20260915-2/production-comparison.json` and `census.json`.

| Responsibility | Reviewed boundary |
| --- | --- |
| `KingdomResidence` / `KingdomResidenceRules` | Pure decoded home/profile facts and canonical bounded wire. Map, plot and optional bed identity are independent of body/job location. Unknown legacy state differs from observed homelessness. Live bed claims must be unique within their map/plot identity. No engine objects, forced map loading or parallel population ledger. |
| `KingdomResidentRow` and city state | One new immutable string reference, preserved by every row clone. Create, single-row and whole-roster mutation validate claims. Equality includes residence. Existing row IDs and standing remain the population authority. |
| `KingdomCityBook` | One named column, published/read/cleared with the existing resident columns. Schema 5 migrates missing legacy facts without inferring a home; malformed current strings, shape or duplicate live bed claims refuse without dropping residents. Version-4 frozen subsidence loads preserve every existing field and pending rung. |
| Archive codec | Version 20 adds the column; schemas 1–19 keep their field shapes and city-header projections. Old authority hashes are re-proved only when no new residence facts would be discarded. New archives validate the column before accepting authority. |
| Death codec | `rd1` stays canonical when all before-rows have unknown residence. `rd2` includes residence for every row in a mixed journal. Row comparison retains old bytes for unknown facts and distinguishes observed homes. Malformed facts refuse. |
| Memory rules | 123 declared row bytes fit a 128-byte allowance. A 2048-character residence adds at most 4128 heap bytes; the full three-city model bound is 1,038,614 bytes, with 1152/1280-KiB advisory/ceiling rungs. Both field width and composed formula are tested. |

Validation is source/model coverage: 15,048 main and 5,934 portable cases, zero skips, including
historical golden archive bytes, named-field migration, frozen claims, mixed death receipts,
round trips, malformed storage and mutation/equality boundaries. It is not native gameplay
acceptance. Four engine compile modes are recorded separately in the local evidence archive.

Live housing capture, assignment, admission and remote occupancy still use the old paths. The
native home-map regression remains DEFECT. Unique physical bed allocation, actual home/work
travel, unloaded-map accounting, building replacement/removal and real save/cold-load scenarios
remain required in #230. Architecture and intended function for all buildings remain #229/#251.
