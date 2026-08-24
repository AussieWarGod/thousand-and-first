# Contributing

The Thousand and First welcomes bug reports, compatibility evidence, documentation,
tests, and carefully scoped code or XML changes. Read [STANDARDS.md](STANDARDS.md)
before changing behavior and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before
changing state.

## Set up a checkout

```bash
git clone https://github.com/AussieWarGod/thousand-and-first.git
cd thousand-and-first
git switch -c type/short-topic
```

No submodules or vendored game files are required for documentation and checkout-only checks.
Python 3 is needed for XML/art audits. Licensed integration checks need the local game and tools
described below.

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
python3 Art/check_xml_refs.py
```

`Tools/stage.sh verify` is checkout-only. `Art/check_xml_refs.py` always checks internal XML; it
also verifies vanilla references when the script's default Qud install is present or `--base` is
given. Public contributors can also run the locked, checkout-only portable kernel and repository
locator slice (currently 171 cases):

```bash
dotnet restore DevTests/PortableTests.csproj --locked-mode
dotnet run --project DevTests/PortableTests.csproj --no-restore -v q --nologo
```

This is a deliberately small subset, not a claim that the full native suite is portable or that it
compiles the mod runtime. `DevTests/TafTests.csproj` still references Qud's copy of
`nunit.framework.dll` at the configured Windows install. Running the full suite therefore requires
a licensed local Caves of Qud installation and .NET 9:

```powershell
dotnet run --project DevTests/TafTests.csproj -v q --nologo
```

Equivalent from WSL repository root:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$(wslpath -w "$PWD/DevTests/test.ps1")"
```

Current compile and integration tooling assumes WSL plus Windows PowerShell, .NET SDK
`9.0.306`, and Caves of Qud at
`F:\SteamLibrary\steamapps\common\Caves of Qud`. Those paths are presently encoded in
`DevTests/refs.rsp` and `DevTests/TafTests.csproj`.

```bash
./Tools/gate.sh
TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" \
  python3 Art/check_wiring.py
python3 Art/check_xml_refs.py \
  --base "/path/to/CoQ_Data/StreamingAssets/Base"
```

Full release-candidate check, including exact staged compile, test suite, asset/reference
audits, smoke-launcher harness, and deploy dry run:

```bash
./Tools/release-check.sh
```

This requires the configured licensed game files and WSL/Windows bridge. It does not replace
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
- Preserve `r_` prefixes, deterministic ordering, bounded collections, and explicit failure
  reporting.
- Write player-facing text in Qud's register and use the established grammar/color helpers.
- Follow [docs/ASSET_PROVENANCE.md](docs/ASSET_PROVENANCE.md) for every visual reference.

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
