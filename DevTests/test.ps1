# Engine-free pure/source-contract and portable-kernel suites using the locked NUnit package.
# Usage: powershell -File test.ps1
if (-not [string]::IsNullOrWhiteSpace($env:TAF_TEST_FILTER)) {
    Write-Error "Release/full-suite runner refuses ambient TAF_TEST_FILTER=$($env:TAF_TEST_FILTER)"
    exit 2
}
$env:TAF_REPO_ROOT = Split-Path $PSScriptRoot -Parent
$env:TAF_FORBID_SKIPS = "1"
$fullProject = Join-Path $PSScriptRoot "TafTests.csproj"
$portableProject = Join-Path $PSScriptRoot "PortableTests.csproj"
$dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    Write-Error "Required dotnet application was not found on PATH. No tests ran."
    exit 127
}
$dotnet = $dotnetCommand.Source

# Named-leg propagation (issue #134): a leg's restore or run step must fail the whole script
# with a non-zero exit and name itself in the last log line, so a caller never has to infer
# which of the two legs (if either) actually ran from ALL GREEN line counting alone. This does
# not change the ALL GREEN lines the suites themselves print -- existing parsers still work.
function Invoke-LicensedLeg {
    param([string]$LegName, [string]$Project)

    & $dotnet restore $Project --locked-mode -v q --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Output "LICENSED_LEG_FAILED=$LegName rc=$LASTEXITCODE step=restore"
        exit $LASTEXITCODE
    }
    & $dotnet run --project $Project --no-restore -v q --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Output "LICENSED_LEG_FAILED=$LegName rc=$LASTEXITCODE step=run"
        exit $LASTEXITCODE
    }
}

Invoke-LicensedLeg -LegName "taf" -Project $fullProject
Invoke-LicensedLeg -LegName "portable" -Project $portableProject
exit 0
