# Published archive-root owner probe

After strict compilation, run the standalone net9/C#7.3 probe with the exact mod DLL hash:

```text
dotnet run --project DevTests/EngineArchiveOwner/Probe.csproj -- MOD_DLL INSTALLED_MANAGED_DIRECTORY EXPECTED_MOD_SHA256
```

Loads the supplied real mod assembly and installed engine dependencies. No game is created,
no Steam calls occur, and no save is modified. The probe requires `The.Game` to remain null and
checks the supplied mod hash before and after execution. Exit0 means every case passed;
exit1 means behavior failed; exit2 means setup or final verification refused.

46 cases execute the real native `Binding.Intact` predicate and the real hash-basis runtime
publication gates with controlled hashers. They cover exact-root controls, null/foreign roots
(including a replacement sharing all seven receipts), each replaced receipt, and substitutions
before or during graph/authority hashing. Refusals preserve every receipt field and the replaced
root. Positive cases preserve historical18 hashes and separate current19 settlement bases.

This is not live `PartAddedEvent`, return/exile, serializer, historical-save or gameplay evidence.
Keep baseline failure output, corrected output and exact source/DLL hashes separately.
