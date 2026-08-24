## Outcome

Describe player/contributor outcome and link issue(s).

## Scope

- Changed:
- Deliberately unchanged:

## Risk declaration

Check every affected boundary:

- [ ] Save/wire format, serialized field, migration, archive, or realm seal
- [ ] Realm/settlement/city/object/operation/receipt identity
- [ ] Physical mutation, resource debit, transaction, retry, callback, lease, or outbox
- [ ] Published `ThousandAndFirst.Api` contract or behavior
- [ ] XML schema, key, default, or merge/load-order behavior
- [ ] Runtime tile reference, glyph, screenshot, or other asset/provenance
- [ ] None of the above

Explain each checked risk and compatibility strategy:

## Evidence

Record command, result, and environment. Mark unavailable checks honestly.

- [ ] `./Tools/stage.sh verify`
- [ ] `python3 Art/check_xml_refs.py`
- [ ] `dotnet run --project DevTests/TafTests.csproj -v q --nologo`
- [ ] `./Tools/gate.sh`
- [ ] `python3 Art/check_xml_refs.py --base "/path/to/CoQ_Data/StreamingAssets/Base"`
- [ ] `TAF_QUD_BASE="/path/to/CoQ_Data/StreamingAssets/Base" python3 Art/check_wiring.py`
- [ ] Relevant [TESTING.md](../TESTING.md) live passes, including save → quit → reload where needed
- [ ] `./Tools/release-check.sh` when release-facing

Automated result summary:

Manual proof (game marketing/core build, platform/store, fresh/existing save, mod load order,
steps, and redacted screenshots/logs):

## Persistence and compatibility

Describe old-save, future/unknown data, retry/reload, multi-city, option-off, and other-mod behavior
as applicable. State “not applicable” with reason; do not leave blank.

## Assets and provenance

For any visual change, give exact vanilla tile path or glyph/color values, source/rights record,
live readability evidence, and `Art/check_wiring.py` result. No copied/extracted game art or
AI-generated/generative-image-assisted raster art.

## Rights attestation

- [ ] I wrote this contribution or have documented permission to submit it.
- [ ] It contains no copied game/decompiled code, XML, art, audio, DLLs, or unlicensed third-party material.
- [ ] I license this contribution under the repository's MIT License.
- [ ] I redacted logs, screenshots, paths, saves, and profile data before attaching evidence.
