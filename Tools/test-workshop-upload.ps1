param([Parameter(Mandatory = $true)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# SDK-free tests: never initialize Steam or upload. Use a fresh caller-owned native output folder.
$Directory = Get-Item -LiteralPath $EvidenceRoot -Force
if (!$Directory.PSIsContainer -or $Directory.FullName.StartsWith('\\') -or
    $Directory.FullName -eq [IO.Path]::GetPathRoot($Directory.FullName) -or
    (Get-ChildItem -LiteralPath $Directory.FullName -Force | Measure-Object).Count -ne 0) {
    throw 'Expected a fresh, empty local evidence directory.'
}
$Cursor = $Directory
while ($null -ne $Cursor) {
    if (($Cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked evidence path.' }
    $Cursor = $Cursor.Parent
}
$Launcher = Join-Path $PSScriptRoot 'workshop-steam-upload.ps1'
$LauncherSHA = (Get-FileHash -LiteralPath $Launcher -Algorithm SHA256).Hash.ToLowerInvariant()
& (Get-Command powershell.exe -CommandType Application).Source -NoProfile -NonInteractive -ExecutionPolicy Bypass `
    -File (Join-Path $PSScriptRoot 'test-workshop-release-launcher.ps1') `
    -LauncherPath $Launcher -ExpectedSHA $LauncherSHA *> (Join-Path $Directory.FullName 'launcher.log')
if ($LASTEXITCODE -ne 0) { throw 'Release launcher routing/refusal tests failed; see launcher.log.' }
$Dotnet = (Get-Command dotnet.exe -CommandType Application).Source
$OutputDirectory = Join-Path $Directory.FullName 'out'
$IntermediateDirectory = (Join-Path $Directory.FullName 'obj') + '/'
$Project = Join-Path $PSScriptRoot 'WorkshopSteam\WorkshopUploadTests.csproj'
& $Dotnet build $Project --nologo --configuration Release --output $OutputDirectory `
    "-p:BaseIntermediateOutputPath=$IntermediateDirectory" --ignore-failed-sources --disable-build-servers '-m:1' '-nr:false' `
    *> (Join-Path $Directory.FullName 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'SDK-free test compilation failed; see build.log.' }
$Assembly = Join-Path $OutputDirectory 'TafWorkshopUploadTests.dll'
foreach ($Suite in @('protocol', 'package', 'attempt', 'evidence', 'record', 'observation', 'aftermath', 'cleanup',
    'lock', 'lease', 'registry', 'finalization', 'finalization-windows', 'cli')) {
    & $Dotnet $Assembly $Suite *> (Join-Path $Directory.FullName ($Suite + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "Workshop $Suite tests failed; see retained log." }
}
Write-Output 'WORKSHOP UPLOAD TESTS CLEAN (SDK-free; no Steam operations).'
