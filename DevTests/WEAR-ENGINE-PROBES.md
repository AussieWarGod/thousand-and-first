# Direct wear-load regression probes

These two BCL-only net9/C#7.3 executables load a supplied compiled ordinary mod DLL and the installed
game's real managed assemblies by exact identity. No XRL stubs, game launch, save mutation, Steam
call or global game replacement. Keep them outside the mod package; `Tools/stage.sh` excludes
`DevTests`. Each invocation is a separate process; the wire probe verifies `The.Game` is null
before serialization.

Run on Windows after the ordinary strict compile gate, using that gate's exact DLL:

```powershell
dotnet run --project DevTests/EngineWearRead/Probe.csproj -c Release -- 'C:\candidate\ordinary-baseline.dll' 'F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed'
dotnet run --project DevTests/EngineWearWire/Probe.csproj -c Release -- 'C:\candidate\ordinary-baseline.dll' 'F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\Managed'
```

Replace paths with the candidate DLL and actual installed Managed directory. Retain source/DLL
hashes and both raw outputs with the candidate evidence. Exit0 means all selected cases passed;
exit1 is a behavioral failure; exit2 means setup/dependency or runtime failure blocks that result.
Caught loader faults are not waived into successful cases. Never use an older passing DLL to
sign a changed candidate.

- Read probe:4 cases against public Read/ReadError, original exception type, private sticky
  failure after public quarantine reset, and unchanged surviving raw receipt fields.
- Wire probe:7 cases using private real FastSerialization caches/readers/writers. Named v1 and
  seven-field positional legacy roundtrip; malformed version/type, assigned-prefix preservation,
  and named/legacy payload truncations. Errors retain the same first-chance exception object;
  failed parts refuse a subsequent healthy reader before consuming it.

Truncations remove the final payload byte after real token-table initialization. This does not
exercise whole-file admission, the framed IPart.Load recovery path, AfterGameLoaded delivery,
historical game saves or ordinary play. No FinalizeRead call: it clears global load bindings.
Private writer setup proves its seeded player entry is null before clearing that local rack.

Recorded implementation evidence and remaining gates: [current status](../docs/STATUS.md).
