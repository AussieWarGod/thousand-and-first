# Engine-free pure/source-contract and portable-kernel suites using the locked NUnit package.
# Usage: powershell -File test.ps1
if (-not [string]::IsNullOrWhiteSpace($env:TAF_TEST_FILTER)) {
    Write-Error "Release/full-suite runner refuses ambient TAF_TEST_FILTER=$($env:TAF_TEST_FILTER)"
    exit 2
}
$env:TAF_REPO_ROOT = Split-Path $PSScriptRoot -Parent
$fullProject = Join-Path $PSScriptRoot "TafTests.csproj"
$portableProject = Join-Path $PSScriptRoot "PortableTests.csproj"
$dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    Write-Error "Required dotnet application was not found on PATH. No tests ran."
    exit 127
}
$dotnet = $dotnetCommand.Source
& $dotnet restore $fullProject --locked-mode -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project $fullProject --no-restore -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet restore $portableProject --locked-mode -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $dotnet run --project $portableProject --no-restore -v q --nologo
exit $LASTEXITCODE
