# Contributing

The Thousand and First welcomes bug reports, compatibility evidence, documentation,
tests, and carefully scoped code or XML changes. Read [STANDARDS.md](STANDARDS.md)
before changing behavior and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before
changing state. [docs/STATUS.md](docs/STATUS.md) is the current evidence/gap boundary; do not infer
release status from an older audit or test count.

Players can contribute without building code. Use the [Alpha playtest form](https://github.com/AussieWarGod/thousand-and-first/issues/new/choose)
for pacing, UI, architecture, balance, or session feedback; use bug/compatibility forms for a
reproduction. [SUPPORT.md](SUPPORT.md) lists safe diagnostics. Good first code work includes docs,
pure rule tests, and focused diagnostics; persistence and transaction changes require prior design
review.

## Set up a checkout

Fork the repository on GitHub, then clone your fork and retain this repository as `upstream`:

```bash
git clone https://github.com/YOUR-ACCOUNT/thousand-and-first.git
cd thousand-and-first
git remote add upstream https://github.com/AussieWarGod/thousand-and-first.git
git fetch upstream
git switch -c type/short-topic upstream/main
```

Replace `YOUR-ACCOUNT` with your GitHub account. Never force-push another contributor's branch or
rewrite shared release tags.

No submodules or vendored game files are required for documentation and checkout-only checks.
Python 3 is needed for XML/art audits. Code tests use the exact .NET SDK `9.0.306` pinned by
`global.json`; roll-forward is disabled so a different local SDK cannot silently sign the result.
The hosted workflow installs that exact SDK. Licensed integration checks need the local game and
tools described below.

## Before opening a pull request

1. Open or find an issue for behavior changes. Describe player outcome before design.
2. Keep one concern per pull request. Avoid incidental formatting or generated-file churn.
3. Add pure rule tests for every boundary and failure mode the change introduces.
4. Extend [TESTING.md](TESTING.md) when behavior needs in-game proof.
5. Run every applicable check below and record exact results in the pull request.
6. Rebase or merge current `main`; do not resolve state-schema conflicts by choosing one
   side wholesale.

## Local checks

Baseline checks, from repository root:

```bash
./Tools/stage.sh verify
python3 Tools/generate-lot-realizations.py --check
python3 Tools/check-architecture.py --repo-root .
python3 Art/check_xml_refs.py --no-base
```

`Tools/portable-check.sh` composes these deterministic repository checks with documentation,
structure, staging, package-reference, XML, and Python-unit audits. `Tools/stage.sh verify` is
checkout-only. `Art/check_xml_refs.py --no-base` always checks only internal XML, even when a
developer's default Qud install is present; the licensed lane below adds exact vanilla resolution.
Run the composition with `./Tools/portable-check.sh` after the focused commands pass.
Public contributors can run the locked, engine-free full pure/source suite plus the small
repository-locator slice without owning or installing Qud:

```bash
dotnet restore DevTests/TafTests.csproj --locked-mode
TAF_ALLOWED_SKIPS='KingdomCreedContentTests.Installed21151CensusIsAnExactThirtyThreeAndChiliadAddsNone;KingdomGatehouseNativeTests.GateRootRetainsVanillaDoorPartAndOwnsOnlyTopology;KingdomInheritanceSpatialNativeTests.ReconstructedStreetUsesVanillaPassableDirtPath' \
  dotnet run --project DevTests/TafTests.csproj --no-restore -v q --nologo
dotnet restore DevTests/PortableTests.csproj --locked-mode
dotnet run --project DevTests/PortableTests.csproj --no-restore -v q --nologo
```

Run these restore/build/test commands serially. Every `TafTests` configuration writes shared mutable
NuGet/MSBuild state under `DevTests/obj/` (including `project.assets.json`), and repository scripts
may invoke that project too; concurrent invocations can overwrite another run's restore inputs.
`PortableTests` redirects its intermediates to `Tools/PortableOutput/obj/`, but do not overlap it
with the documented full-suite scripts. Keep each restore immediately beside its matching
`--no-restore` run.

These suites prove engine-free rules and source contracts; they do not compile or execute the mod
inside Qud. They use the locked NUnit package and .NET 9. When licensed installed data is absent,
the three installed-data-only cases must match the exact workflow skip allowlist; an extra or
missing skip fails, as does an explicitly configured incomplete `TAF_QUD_BASE`. Never replace them
with guessed base content. A release run accepts no skips: `DevTests/test.ps1` sets the zero-skip
policy, and `Tools/release-check.sh` supplies the exact base. Licensed runtime checks still require
a local Caves of Qud installation:

```powershell
dotnet run --project DevTests/TafTests.csproj -v q --nologo
```

Equivalent from WSL repository root:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/DevTests/test.ps1")"
```

Current compile and integration tooling assumes WSL plus Windows PowerShell and .NET SDK
`9.0.306`. It defaults to Caves of Qud at
`F:\SteamLibrary\steamapps\common\Caves of Qud`; set `TAF_QUD_ROOT` to another licensed install
root before running native/release tools.
`Tools/stage.sh` derives the default Windows account's local Mods path when WSL interop is
available. Set `TAF_LIVE_MOD` to the exact existing mod directory for a custom profile or any
non-default location; deploy dry runs and applies fail closed when that target cannot be proved.

```bash
./Tools/gate.sh
TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" \
  python3 Art/check_wiring.py
python3 Art/check_xml_refs.py \
  --base "/path/to/CoQ_Data/StreamingAssets/Base"
python3 Tools/check-structure.py --report
```

Structure report is diagnostic during incremental work. Binding Addendum 9 remains a release
gate: every staged production C# file must be strictly under 300 physical lines, and a human must
review one-responsibility and protocol boundaries against the exact inventory. See
[docs/STRUCTURE.md](docs/STRUCTURE.md). Do not add exceptions or relabel debt to make the number
green.

Full release-candidate check, including exact staged compile, test suite, asset/reference
audits, smoke-launcher harness, deploy dry run, and structural release contract:

```bash
./Tools/release-check.sh --test
```

`--test` is the private candidate lane; maintainers use explicit `--alpha` or `--release` only at
the matching public metadata boundary. This requires the configured licensed game files and
WSL/Windows bridge. It does not replace
the controlled live-game passes in [TESTING.md](TESTING.md). Never report a manual pass you did
not perform. Maintainers follow [docs/RELEASING.md](docs/RELEASING.md) for package, private
subscription, and public Workshop transitions.

## Change risk

Ask for design review before editing any of these boundaries:

- save or wire formats, serialized public fields, migrations, realm seals, or archived cities;
- settlement, realm, object, operation, receipt, or replay identity;
- transactional resource debits, physical mutations, callbacks, leases, outboxes, retry, or
  rollback behavior;
- published types under `ThousandAndFirst.Api` or contracts in [docs/API.md](docs/API.md);
- XML registry merge semantics, keys, or load-order behavior.

These areas need compatibility fixtures, malformed/future/legacy input tests, and explicit
save → quit → reload evidence as applicable. See
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#high-risk-boundaries).

Good starter work stays outside those boundaries: clarify player-facing documentation, add
table-driven tests around an existing pure rule, improve internal XML diagnostics, reduce a
minimal compatibility reproduction, or document a verified game-build difference. Do not use
“good first issue” as a reason to change persistence, identity, transactions, or public API.

## Code, XML, and prose

- Keep engine-free computation in `*Rules.cs`; engine calls belong in adapters/parts/systems.
- Verify engine behavior against the installed build. Decompilation may be used locally as API
  evidence, but do not copy decompiled source, comments, identifiers in bulk, or XML into a
  contribution.
- Extend content through mergeable root XML registries. Never overwrite a vanilla blueprint.
- A plotted building needs both catalogue metadata and authored architecture. Supply exact maps,
  palettes, plans, bindings, tiers, functional anchors, and every actual size you intend to offer;
  a plot declaration alone is only a reserved lot. Follow the [complete extension
  example](MODDING.md#complete-minimal-authored-plot-extension) and run
  `python3 Tools/check-architecture.py --repo-root .`.
- Preserve `r_` prefixes, deterministic ordering, bounded collections, and explicit failure
  reporting.
- Write player-facing text in Qud's register and use the established grammar/color helpers.
- Follow [docs/ASSET_PROVENANCE.md](docs/ASSET_PROVENANCE.md) for every visual reference. Vanilla
  is preferred; original art needs an editable source, exact manifest/hash/fallback, rights, and
  independent native readability review before it is wired.
- Generative-image assistance is neither silently accepted nor categorically banned. Disclose the
  tool and lawful inputs in `method`; retain editable source; complete pixel-level human revision;
  and obtain independent native tile/text-scale review. A prompt or generated draft is not
  provenance, authorship evidence, or a quality receipt.

## Rights and inbound license

By submitting a pull request, you attest that:

- you wrote the contribution or have documented permission to submit it;
- it contains no copied Caves of Qud code, decompiled source, XML, art, audio, DLLs, or other
  game files;
- it contains no unlicensed third-party material;
- all provenance and license notices are accurate; and
- you license your contribution to this project under its [MIT License](LICENSE).

Do not commit Qud assemblies, game saves, full player profiles, crash dumps, proprietary test
fixtures, or extracted game assets. Reference locally installed game paths; never vendor their
contents.

## Reports and review

Use issue forms for ordinary bugs, compatibility reports, and proposals. Redact logs and paths
as directed by [SECURITY.md](SECURITY.md). Report vulnerabilities or sensitive conduct matters
through a [private GitHub security advisory](https://github.com/AussieWarGod/thousand-and-first/security/advisories/new),
not a public issue.

All contributions must follow [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
