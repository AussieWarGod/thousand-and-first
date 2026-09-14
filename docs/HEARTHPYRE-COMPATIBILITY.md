# Hearthpyre compatibility direction

[Issue #218](https://github.com/AussieWarGod/thousand-and-first/issues/218) tracks current-version
support and its native acceptance. This work is part of the Beta goal; it does not replace
Quickstart recovery, heart progression, or the [city growth balance requirements](https://github.com/AussieWarGod/thousand-and-first/issues/208).
The public 0.3.6 package still has the older exact-version integration. The changes described here
are development work, not a claim that an updated package has shipped.

## Runtime contract

TAF must not require players to downgrade Hearthpyre. The adapter is compiled unconditionally
without a foreign assembly reference. At runtime it locates the enabled `Hearthpyre` mod, proves
that it owns the resolved realm assembly, and binds the public registry, identity, home roster,
and cell-enumeration capabilities that TAF actually reads. Package version and relative load order
are not runtime admission conditions. Compatible newer implementations may therefore work without
another TAF release; no unknown future release is declared tested.

Only named public fields and readable properties are bound. Dictionary and list contracts may
expose mutable or read-only interfaces; TAF wraps either in lazy read-only views. Metadata binding
invokes no foreign constructors or getters. Observation reads the real registries and home cells,
retains object identity, and repeats the existing bounded custody checks. It never copies a stale
registry snapshot into authority, guesses a renamed capability, invokes a foreign write method,
loads remote ground, creates a foreign settlement, or converts foreign citizens.

Missing or disabled dependency means no optional provider is registered. An enabled dependency
with missing or incompatible capabilities remains a provider that refuses observation. A failed
read must not become a claim of unowned ground. Existing exact claim/adoption bindings still pause
when the required provider disappears or its evidence changes, and can resume after the same
evidence is restored. Capability support grants neither civic building credit nor permission to
change foreign objects; explicit adoption and physical benefit proofs remain required.

The directory/namespace `Integrations/Hearthpyre223` and persisted provider contract version
`2.2.3` retain their existing identities. That value identifies the evidence format, not the
installed package. GUIDs, footprint revisions, and receipt encodings are unchanged so a compatible
package upgrade does not itself invalidate an existing claim. Binding failures identify the actual
installed package version. Foreign objects and reflection metadata are never serialized into TAF
receipts; cached wrappers are invalidated with the engine's mod-sensitive cache.

## Shared development checks

Both Codex and Claude use the same commands from [DEVELOPMENT.md](DEVELOPMENT.md):

```bash
TAF_DOTNET=/home/r/.dotnet/dotnet Tools/dev-check.sh main KingdomHearthpyre
TAF_DOTNET=/home/r/.dotnet/dotnet Tools/dev-check.sh portable KingdomHearthpyre
Tools/dev-check.sh tools check_optional_bridge_test.py
Tools/gate.sh --keep
```

The normal gate audits the installed current Hearthpyre source (reviewed version 2.2.4), then
compiles ordinary and developer inventories under both baseline and compatibility symbols.
Baseline includes the complete adapter without any Hearthpyre reference. Compatibility uses the
reviewed 2.2.4 compile fixture; the installed source hashes independently bind that review.
`TAF_HEARTHPYRE_ROOT` selects another real installation. There is no older-build override or need
to obtain 2.2.3 for this route. Historical 2.2.3 fixtures remain evidence, not normal build inputs.

A source update that changes a review pin stops the installed-source audit for review. This
maintainer evidence check is deliberately separate from runtime capability admission. Refresh
reviewed fixture/source pins from the actual current release, exercise the binder and native
matrix, and document changed semantics; never edit a dependency's version to satisfy a pin.
The fixture-only route on machines without Hearthpyre proves no installed or native compatibility.

## Native acceptance still required

Pure tests and four compiler modes cannot establish in-game compatibility. Automate these complex
scenarios against current Hearthpyre in isolated profiles with complete dependency inventories,
strict logs, exact owned shutdown, and fresh save/load profiles. Preserve failures. Synthetic
foreign setup must be labelled and must use the real mod's own public setup operations.

- Absent, disabled, failed, and enabled-current startup; both relative load orders. Ordinary
  Quickstart/founding still works on genuinely unowned ground. Changed API shapes refuse cleanly.
- Current Hearthpyre settlement/sector overlap: cancel without debit or mutation, bind once,
  claim an adjacent local map for the same TAF city, and verify exact ownership on both maps.
- Exact Home adoption with real enclosed fabric and useful furniture: legitimate benefit,
  overlapping/ambiguous registry refusal, changed cells or missing furniture pause, repair and
  reproof resume, and no foreign lifecycle or object mutations by TAF.
- Save a bound/adopted city, quit, cold-load, reprove identities and physical state, then execute
  the next ordinary paid action. Check no duplicate charge, lost binding, or extra civic credit.
- Disable and re-enable the dependency through separate cold launches on copies; retained evidence
  pauses and recovers. Repeat with changed owner/sector/Home identities to prove refusal.
- A historical bound save upgraded to current compatible Hearthpyre retains the same contract
  version, revision and GUIDs. Synthetic version metadata is not a substitute for this save test.

No native case above is accepted by this development document. Keep #218 open until evidence is
recorded. Beta also still needs broader recovery, higher-heart, and same-city multi-map coverage.
