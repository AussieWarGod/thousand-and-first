# Security Policy

## Supported versions

The project is pre-release. Security fixes target current `main` and the newest published
pre-release or Workshop build once one exists.

| Version | Security support |
| --- | --- |
| Current `main` | Yes |
| Newest published pre-release/Workshop build | Best effort |
| Older commits, local snapshots, or modified packages | No |

No Workshop publication should be inferred from this policy; it describes support after a build
is published.

## Report a vulnerability privately

Open a
[private GitHub security advisory](https://github.com/AussieWarGod/thousand-and-first/security/advisories/new).
Include affected commit/version, game build, impact, prerequisites, minimal reproduction, and a
safe proof of concept. Do not open a public issue until maintainers agree disclosure is safe.

Use the same private channel for exposed secrets or private data found in repository artifacts.
Report vulnerabilities in Caves of Qud itself to its developer through the game's official
channels; this repository cannot remediate the base game.

## Sensitive evidence

Player logs, saves, screenshots, profile folders, and crash reports may expose account names,
filesystem paths, mod lists, world seeds, character names, chat overlays, or other private data.

- Reproduce in a fresh isolated profile when possible.
- Remove names, tokens, account IDs, local paths, and unrelated mod information.
- Keep enough structure to diagnose the issue; replace secrets consistently rather than deleting
  whole control-flow sections.
- Do not attach a save or full profile publicly. Offer it through the private advisory only when
  needed and state what it may contain.
- Never attach game DLLs, extracted assets, or decompiled source.

## Mod privilege model

Caves of Qud scripting mods compile and execute C# in the game process. They are not a security
sandbox and can act with the privileges available to that process. Install mods and development
builds only from sources you trust; inspect changes before running them; keep backups of valuable
saves; and do not treat a Workshop listing as a code-safety guarantee.

Security-relevant project areas include staging/deployment path validation, untrusted XML or save
input, serialization bounds, extension discovery/execution, log handling, and any file or process
operation. Ordinary balance bugs and save-compatibility regressions belong in public issue forms
unless their reproduction contains sensitive data or enables code/file-system impact.

## Disclosure

Maintainers will acknowledge a complete report when available, validate it against supported
code, coordinate a fix and regression test, and agree on public disclosure timing with the
reporter. Please avoid destructive testing against other people, public profiles, or data you do
not own.
