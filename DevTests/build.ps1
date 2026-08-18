# Compile gate: builds the mod's .cs exactly as the game's compiler would.
# Usage: powershell -File build.ps1
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $PSScriptRoot "out"
New-Item -ItemType Directory -Force $out | Out-Null
$sources = Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notlike "*\DevTests\*" } | ForEach-Object { '"' + $_.FullName + '"' }
$rsp = Join-Path $PSScriptRoot "build-generated.rsp"
$lines = @('@"' + (Join-Path $PSScriptRoot "refs.rsp") + '"', '-out:"' + (Join-Path $out "r_ThousandAndFirst.dll") + '"') + $sources
Set-Content -Path $rsp -Value $lines -Encoding utf8
dotnet exec "C:\Program Files\dotnet\sdk\9.0.306\Roslyn\bincore\csc.dll" "@$rsp"
if ($LASTEXITCODE -eq 0) { Write-Host "COMPILE CLEAN" } else { Write-Host "COMPILE FAILED ($LASTEXITCODE)" }
exit $LASTEXITCODE
