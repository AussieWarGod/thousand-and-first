# Building behavior coverage before Beta

Every shipped building must meet the architectural standard and perform its advertised
gameplay role before Beta. [#251](https://github.com/AussieWarGod/thousand-and-first/issues/251)
tracks functional completeness; [#229](https://github.com/AussieWarGod/thousand-and-first/issues/229)
tracks architecture. Enclosed functions need real rooms, furniture reserves occupied space even
when natively walkable, and halls, doors and landings stay clear. Open farms and workyards need
intentional boundaries and useful access. See [HOUSING-AND-POPULATION.md](HOUSING-AND-POPULATION.md).

## Shared inventory command

Codex, Claude and contributors use the existing behavioral coverage tool:

```bash
python3 Tools/coverage/check_coverage.py buildings --out /tmp/building-coverage.json
```

It selects the exact shipped XML paths through `Tools/stage.sh list`, identifies building,
architecture and household-yard streams by root, then uses the existing architecture parser.
New filenames and keyed extension declarations therefore remain visible. This is a source
catalogue inventory, not an observation of a loaded game or another installed mod's catalogue.
Source order is the staged path order; an actual loaded mod stack still needs its native census.

The initial inventory contains 144 buildings and four household yard works: 1,376 authored
poses, ten network works and four yard works, totaling 1,390 configurations. Seventy-two poses
are retained readers and need historical-save scenarios rather than new commissioning claims.
Every authored variant and all four supported poses are listed with selectors, tier, plot size,
facing rule, required roles, building attributes, map and palette. Non-plot works remain present.
A plot without an authored binding is reported as `unbound-plot`, never silently skipped.
Road surfaces are a separate network system under #209; this inventory does not enumerate them.

## Obligations per configuration

| Obligation | Required behavioral scope |
| --- | --- |
| `architecture` | Intentional spatial program; physical envelope, entrances, clear circulation, usable fixtures, material/cultural expression, and sensible relationship to neighboring works. Actual native visuals and interaction must agree with the plan. |
| `construction` | Ordinary discovery, siting/designation, prerequisites, exact payment and material custody, labour and completion. Retained readers instead need the supported saved-job recovery path. |
| `operation` | The promised benefit actually occurs: staffing, inputs, outputs, capacity, storage, citizen activities, or defensive/network effects as appropriate. Catalogue attributes alone prove none of these. |
| `failure-recovery` | Relevant shortages, full stores, competing jobs, blocked/locked access, lost beds, damage, late occupants and recovery. No duplicate payment/output, lost custody or unexplained permanent stall. |
| `upgrades-removal` | Supported paid upgrades/conversions, repair and removal preserve existing identities and contents. For a work without a particular action, verify its intentional absence rather than inventing that feature or skipping the whole obligation. |
| `save-cold-load` | Real save and fresh process retain buildings, paid jobs, contents, staffing and citizen bindings; the next ordinary action still works. |
| `multi-map` | Relevant home/work/travel, remote production/consumption, staffing and neighboring-network interactions in an ordinary multi-tile city. No duplication or dependence on repeatedly visiting a map. |

The initial 9,730 obligations are **unmapped**, not 9,730 demonstrated defects or proof that
no prior tests exist. Accepted housing, furniture, Quickstart and heart evidence retains its
original scope in [STATUS.md](STATUS.md). Import those links only after checking the exact
configuration and behavioral assertions; a broad construction test must not cover every work.

## Link evidence without widening its claims

`Tools/coverage/buildings.json` links configuration/obligation pairs to row IDs in the existing
`Tools/coverage/matrix.json`. Each binding has exactly these fields:

- `configuration`: exact `id` from the generated inventory; no wildcards.
- `definitionDigest`: that configuration's current SHA-256 from the inventory.
- `obligation`: one of the seven names above.
- `rows`: distinct existing behavioral row IDs.
- `reason`: concrete scenario and assertion scope, including any justified shared-runtime
  equivalence and the separate geometry/material coverage of this configuration.

Fingerprints include building declarations, style context, map, palette, pose definitions,
requirements and variant selection candidates. A relevant definition change reports
`STALE_MAPPING`; unrelated new building declarations do not invalidate existing definitions.
Removing or renaming a linked configuration, duplicate bindings, unknown row IDs and malformed
scope data refuse the report. Full source XML hashes remain in its `sources` list.

`MAPPED` means only that a link exists. The report separately exposes each linked row's status;
it never converts a missing, historical, unexecuted or failed scenario into native acceptance.
Definition identity does not establish production C#, engine, harness or scenario identity.
Existing typed evidence, archive byte/source bindings and actual assertions still govern proof.

```bash
python3 Tools/coverage/check_coverage.py buildings --require-mapped --out /tmp/building-coverage.json
```

This writes the report and exits 2 while any obligation is unmapped or stale. It is a coverage
mapping check, **not a Beta readiness gate**. Even complete mappings require the existing
native evidence audits and a requirement-by-requirement scope review. The release workflow
currently accepts Alpha lanes only; #251 remains open until this catalogue's actual behavior
and evidence satisfy Beta. No manual testing or approval gate is added.

Use broad deterministic catalogue/geometry tests, then native scenarios for each distinct
runtime path and complex composition. Shared fixtures may amortize setup, but must assert each
promised effect and relevant failure/recovery. Do not multiply identical long runs merely to
populate this table. A screenshot, test count or declaration is insufficient acceptance.

Focused tool validation:

```bash
Tools/dev-check.sh tools building_coverage_test.py
Tools/dev-check.sh tools check_coverage_test.py
```
