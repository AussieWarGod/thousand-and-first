# Quickstart readiness diagnostic (not release acceptance)

This runner calls the compiled mod's readiness method using installed Qud engine assemblies.
It constructs a minimal synthetic context; it does **not** run new-game boot, world generation,
normal founding, grant delivery, or save/load. It does not patch engine methods or replace parts
with stubs. The supplied DLL is loaded by exact path.

```powershell
dotnet run --project DevTests/EngineQuickstartReady/Probe.csproj -- MOD_DLL INSTALLED_MANAGED_DIRECTORY
```

Exit 0 means all 45 component cases passed; exit 1 means an assertion failed; exit 2 means
setup/engine execution was blocked. A blocked case is never a passing test.

## Observed limitation — 2026-09-06

The old-code diagnostic completed with **3 passed, 0 failed, 42 blocked**, exit 2. Empty-zone
controls executed, but every object-bearing case failed during actual `Physics` initialization:
`System.Security.SecurityException: ECall methods must be packaged into a system module.`
Consequently this run did not execute the founder assertions and is not a red/green regression
proof. A fresh Qud process with Unity initialized is required for those cases. Do not stub that
initialization or suppress its failure to obtain a green result.

Historical DLL used: `/tmp/taf-stage.CvBzBh/r_ThousandAndFirst-baseline.dll`. Its Quickstart
sources match the frozen 0.3.1 candidate, but its other sources include a separate raid draft;
this is not a claim that the DLL is the private Workshop package.
Logs: `/tmp/taf-quickstart-founder-proof.3xBBuF/ready-before.log` and
`ready-before-diagnostic.log` in the same directory.

The readiness defect has independent production-source evidence: Qud places the player before
`GAMESTARTING`; the old Quickstart readiness check then rejects that player's occupied cell.
Full acceptance must additionally exercise all three real Quickstart profiles with advisor on/off,
actual normal founding and starter grants, then save/reload without duplicated grants.
