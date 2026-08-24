param(
    [Parameter(Mandatory = $true)]
    [string]$Root,
    [string]$Game = 'F:\SteamLibrary\steamapps\common\Caves of Qud\CavesOfQud.exe'
)

$ErrorActionPreference = 'Stop'
$rootPath = [IO.Path]::GetFullPath($Root)
$manifest = Join-Path $rootPath 'Local\Mods\ThousandAndFirst\manifest.json'
if (-not (Test-Path -LiteralPath $Game -PathType Leaf)) {
    throw "Caves of Qud executable not found: $Game"
}
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
    throw "Isolated TAF manifest not found: $manifest"
}
if ((Get-Content -LiteralPath $manifest -Raw) -notmatch '"id"\s*:\s*"r_ThousandAndFirst"') {
    throw "Isolated manifest has the wrong mod identity: $manifest"
}
if (Get-Process -Name 'CavesOfQud' -ErrorAction SilentlyContinue) {
    throw 'Caves of Qud is already running; refusing to mix smoke profiles.'
}

$arguments = @(
    '-savepath', (Join-Path $rootPath 'Save'),
    '-sharedpath', (Join-Path $rootPath 'Local'),
    '-syncedpath', (Join-Path $rootPath 'Synced'),
    'STEAM:NO',
    'GALAXY:NO'
)
$process = Start-Process -FilePath $Game -ArgumentList $arguments -PassThru
Write-Output "SMOKE STARTED: PID $($process.Id)"
Write-Output "Profile: $rootPath"
Write-Output "After quitting, copy the fresh Unity Player.log into the profile and run Tools/check-player-log.sh on it."
