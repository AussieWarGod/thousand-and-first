# Contributing

Contributions are welcome — fixes, buildings, styles, whole systems. The bar for
everything is written in [STANDARDS.md](STANDARDS.md); the short version:

1. **Vanilla conformance.** Code reads like Freehold wrote it. Verify every engine call
   against decompiled source; never guess an API. The serialization rules are law.
2. **The depth standard.** No shallow systems — see STANDARDS §5 for the seven-point
   checklist (physical resources, hysteresis, witnessed-only accounting, bounded
   consequences, story residue, pure-testable rules, interlock).
3. **Three-layer testing.** `DevTests/build.ps1` must be clean and `DevTests/test.ps1`
   green before any PR; new pure logic lands with `[TestCase]` tables; in-game behavior
   changes extend TESTING.md and, where possible, the `kingdom:selftest` wish.
4. **Data-driven.** New content goes in mergeable XML registries (see MODDING.md), not
   hardcoded catalogs. New registries load via `DataManager.YieldXMLStreamsWithRoot`.
5. **The prose bar.** Player-facing text is written in Qud's register: little-poem
   descriptions, gospel-plain chronicle lines, "Live and drink."
6. **Compatibility.** XML merges, `Prepare()`-gated Harmony only, no blueprint overwrites,
   per-module option toggles for anything a player might not want.

Paths in `DevTests/*.ps1` assume a Steam install at
`F:\SteamLibrary\steamapps\common\Caves of Qud` and dotnet SDK 9 — adjust locally as
needed (a configurable paths file is a welcome first contribution).
