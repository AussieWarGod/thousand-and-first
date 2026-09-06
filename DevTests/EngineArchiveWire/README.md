# Real archive serializer probe

After strict compilation, run on Windows:

```powershell
dotnet run --project DevTests/EngineArchiveWire/Probe.csproj -c Release -- 'C:\candidate\ordinary-baseline.dll' 'F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed'
```

Uses the supplied real mod assembly and installed serializer, with isolated memory streams.
No source stubs, game launch, save modification, Steam calls or global game replacement.
Retain source/DLL hashes and raw output. Exit0 means every selected case passed; exit1
means behavior failed; exit2 means setup or dependent evidence remains blocked.

103 cases cover real callback and fourteen-value tail roundtrips, no-tail v2-v8 reads,
all56 truncation cuts, invalid bases/receipt shapes, full v9 public envelope, a synthetic
v8 counterpart, and poison/reset/original-rethrow behavior. Public tail-failure cases
require the full-envelope control to pass first.

Full fixtures are codec-valid but quarantined/non-authoritative. Synthetic v8 is not a
historical save. This does not execute actual return callbacks, outer composite recovery,
whole-file admission, FinalizeRead, visible messages or ordinary gameplay.
