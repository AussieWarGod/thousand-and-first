# Launch the isolated developer scenario profile.
#
# Deliberately a separate launcher from Tools/run-smoke.ps1. The smoke launcher asserts exactly one
# mod directory whose bytes match the stage seal, and Tools/release-check.sh depends on that
# assertion holding unchanged; a harness-bearing profile cannot satisfy it and must not be allowed
# to weaken it.
#
# The check here is a CLOSED inventory comparison in both directions against one whole-profile seal.
# Proving only that expected files exist and match would let an injected .cs or .xml compile while
# the seal stayed green, so extras, renames, duplicate normalizations, links, and reparse points are
# all rejected.
#
# This profile produces developer evidence, never release evidence.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$Game
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Game)) {
    $configuredGameRoot = $env:TAF_QUD_ROOT
    if ([string]::IsNullOrWhiteSpace($configuredGameRoot)) {
        $configuredGameRoot = 'F:\SteamLibrary\steamapps\common\Caves of Qud'
    }
    $Game = Join-Path $configuredGameRoot 'CoQ.exe'
}
if (-not (Test-Path -LiteralPath $Game -PathType Leaf)) {
    throw "Caves of Qud executable not found: $Game"
}

$rootPath = (Resolve-Path -LiteralPath $Root).Path
if ($rootPath -notmatch '^[A-Za-z]:\\taf-scenario\.[A-Za-z0-9]+$') {
    throw "Refusing a scenario root outside an exact <drive>:\taf-scenario.<id> path: $rootPath"
}

$localRoot = Join-Path $rootPath 'Local'
$modRoot = Join-Path $localRoot 'Mods\ThousandAndFirst'
$harnessRoot = Join-Path $modRoot 'Harness'
$sealDir = "$rootPath.seal"
$profileSeal = Join-Path $sealDir 'profile.sha256'
$requestSeal = Join-Path $sealDir 'request.txt'

foreach ($required in @($localRoot, $modRoot, $harnessRoot, $sealDir)) {
    if (-not (Test-Path -LiteralPath $required -PathType Container)) {
        throw "Scenario profile is missing directory: $required"
    }
}
foreach ($required in @($profileSeal, $requestSeal)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Scenario profile is missing seal: $required"
    }
}

$modEntries = @(Get-ChildItem -LiteralPath (Join-Path $localRoot 'Mods') -Force)
if ($modEntries.Count -ne 1 -or -not $modEntries[0].PSIsContainer -or
    $modEntries[0].Name -cne 'ThousandAndFirst') {
    throw "Scenario mod directory must contain only ThousandAndFirst"
}

function Get-NormalizedKey {
    param([Parameter(Mandatory = $true)][string]$Relative)
    return $Relative.Replace('\', '/').Normalize([Text.NormalizationForm]::FormC).ToLowerInvariant()
}

function Get-ProfileInventory {
    param([Parameter(Mandatory = $true)][string]$TreeRoot)
    $found = @{}
    $spellings = @{}
    $prefix = $TreeRoot.TrimEnd([char[]]@('\', '/')) + '\'
    foreach ($item in @(Get-ChildItem -LiteralPath $TreeRoot -Recurse -Force)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Profile tree contains a reparse point: $($item.FullName)"
        }
        if ($item.PSIsContainer) { continue }
        if (-not ($item -is [IO.FileInfo])) {
            throw "Profile tree contains a non-regular file: $($item.FullName)"
        }
        # .NET exposes no direct hard-link-count accessor; fsutil is the simplest robust source on
        # Windows. A hard link is a second name for the same sealed inode, so it carries the same
        # attributes and hash as the original and passes every other check in this loop - only the
        # link count catches it, mirroring Tools/scenario_profile.py's refuse_links (st_nlink != 1).
        $hardLinkNames = @(& fsutil hardlink list $item.FullName)
        if ($hardLinkNames.Count -gt 1) {
            throw "Profile tree contains a hard-linked file with $($hardLinkNames.Count) names: $($item.FullName)"
        }
        if (-not $item.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) {
            throw "Profile tree escaped its root: $($item.FullName)"
        }
        $relative = $item.FullName.Substring($prefix.Length)
        $key = Get-NormalizedKey -Relative $relative
        if ($spellings.ContainsKey($key)) {
            throw "Two profile paths normalize to one name: $($spellings[$key]) and $relative"
        }
        $spellings[$key] = $relative
        $found[$key] = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    if ($found.Count -eq 0) { throw "Profile tree holds no files: $TreeRoot" }
    return $found
}

function Assert-ClosedSeal {
    param(
        [Parameter(Mandatory = $true)][string]$TreeRoot,
        [Parameter(Mandatory = $true)][string]$SealPath
    )
    $lines = @(Get-Content -LiteralPath $SealPath)
    if ($lines.Count -lt 2 -or $lines[0] -cne 'taf-scenario-profile-seal-v1') {
        throw "Seal header is missing or unknown: $SealPath"
    }
    $expected = @{}
    foreach ($line in $lines[1..($lines.Count - 1)]) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '  ', 2
        if ($parts.Count -ne 2) { throw "Seal line is malformed: $line" }
        $digest = $parts[0].Trim().ToLowerInvariant()
        $key = $parts[1].Trim()
        if ($digest -notmatch '^[0-9a-f]{64}$') { throw "Seal line has a malformed digest: $line" }
        if ($expected.ContainsKey($key)) { throw "Seal repeats a path: $key" }
        $expected[$key] = $digest
    }
    if ($expected.Count -eq 0) { throw "Seal is empty: $SealPath" }

    $actual = Get-ProfileInventory -TreeRoot $TreeRoot
    # Closed in BOTH directions: an extra file is as fatal as a missing or modified one.
    $missing = @($expected.Keys | Where-Object { -not $actual.ContainsKey($_) })
    $extra = @($actual.Keys | Where-Object { -not $expected.ContainsKey($_) })
    $modified = @($expected.Keys | Where-Object {
        $actual.ContainsKey($_) -and $actual[$_] -cne $expected[$_] })
    if ($missing.Count -gt 0) { throw "Profile is missing sealed files: $($missing -join ', ')" }
    if ($extra.Count -gt 0) { throw "Profile carries unsealed extra files: $($extra -join ', ')" }
    if ($modified.Count -gt 0) { throw "Profile files differ from the seal: $($modified -join ', ')" }
    Write-Host "Profile matches its seal exactly ($($expected.Count) files, closed both ways)."
}

Assert-ClosedSeal -TreeRoot $localRoot -SealPath $profileSeal

# The dev manifest must select the harness; without it the overlay would sit there uncompiled.
$manifest = Get-Content -LiteralPath (Join-Path $modRoot 'manifest.json') -Raw
if ($manifest -notmatch '/Harness/') {
    throw "Scenario profile manifest does not select the harness directory"
}

# The overlay, the request, and the frozen seed are one sealed unit. If the embark module no longer
# carries exactly the sealed request, the seed the operator was told to enter is not the seed this
# profile would ask the gate to prove.
$sealedRequest = (Get-Content -LiteralPath $requestSeal -Raw).Trim()
if ($sealedRequest -notmatch ';seed=#[0-9]+$') {
    throw "Sealed scenario request carries no exact frozen seed: $sealedRequest"
}
$embark = Get-Content -LiteralPath (Join-Path $harnessRoot 'EmbarkModules.xml') -Raw
$marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
$start = $embark.IndexOf($marker)
if ($start -lt 0) { throw "Harness overlay declares no scenario request state" }
$start += $marker.Length
$end = $embark.IndexOf('"', $start)
if ($end -lt 0) { throw "Scenario request state is unterminated" }
$overlayRequest = $embark.Substring($start, $end - $start)
if ($overlayRequest -cne $sealedRequest) {
    throw "Harness overlay request '$overlayRequest' differs from the sealed request '$sealedRequest'"
}
# The engine's GetWorldSeed parses these digits with int.TryParse and returns the parsed value, so
# '#0' is a lawful exact world seed. The range is 0..2147483647; signs, whitespace, and overflow are
# still rejected by the exact syntax above.
$frozenSeed = $sealedRequest.Substring($sealedRequest.LastIndexOf('=') + 1)
$seedDigits = $frozenSeed.TrimStart('#')
if ($frozenSeed -notmatch '^#[0-9]+$' -or [int64]$seedDigits -lt 0 -or
    [int64]$seedDigits -gt 2147483647) {
    throw "Frozen seed is not an exact in-range Int32 seed: $frozenSeed"
}
Write-Host "Request and frozen seed match their seal (seed $frozenSeed)."

Write-Host ''
Write-Host 'SCENARIO PROFILE VERIFIED. This profile produces developer evidence only:'
Write-Host '  - a scenario-built state never signs native acceptance on its own;'
Write-Host '  - verdicts stay ineligible until curated ordinary-play anchor evidence exists.'
Write-Host ''
Write-Host "ENTER world seed $frozenSeed yourself at character creation. Qud exposes no launcher-side"
Write-Host 'seed injection, so this is manual operator entry; the gate refuses any other world.'
Write-Host ''

# -savelocation is the legacy switch and redirects SAVES ONLY: a game launched with it
# still loads mods from the default AppData profile, so the sealed profile's Harness
# overlay never reaches the engine while a stale default-profile mod copy silently
# supplies content. The modern path arguments below are the same set run-smoke.ps1
# uses and confine saves, shared data (including Mods), synced data, and the log to
# the sealed profile root. Proven live 2026-08-29: with -savelocation the game listed
# the AppData mod directories in RefreshModDirectory; with these four it lists only
# the profile's.
$logPath = Join-Path $rootPath 'Player.log'
if (Test-Path -LiteralPath $logPath) { throw "scenario log already exists: $logPath" }
$arguments = @(
    '-savepath', (Join-Path $rootPath 'Save'),
    '-sharedpath', $localRoot,
    '-syncedpath', (Join-Path $rootPath 'Synced'),
    '-logFile', $logPath,
    'NOMETRICS',
    'STEAM:NO',
    'GALAXY:NO'
)
Write-Host "Launching: $Game $($arguments -join ' ')"
& $Game @arguments
exit $LASTEXITCODE
