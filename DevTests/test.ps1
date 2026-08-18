# Pure-logic test suite (vanilla NUnit idiom, references the game's own nunit.framework.dll).
# Usage: powershell -File test.ps1
dotnet run --project (Join-Path $PSScriptRoot "TafTests.csproj") -v q --nologo
exit $LASTEXITCODE
