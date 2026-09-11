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
    [string]$Game,
    [switch]$OwnAttended,
    # Writes the stop half of this session's run record and exits. Run it after the owned process
    # has actually ended, so stoppedUtc is when the session ended rather than when it was asked to.
    [switch]$StopRecord,
    [int]$ExitCode = 0
)

$ErrorActionPreference = 'Stop'

# --- this session's run record ---------------------------------------------------------------
# Only the halves a launcher can honestly witness: who launched, when it started, when it
# stopped, and the game build string the run's own log states. Everything about the exercised
# tree was written at preparation time by Tools/scenario_run_record.py and is never rewritten
# here. The file is replaced whole, so a half-written record can never be read as a whole one.
function Read-TafRunRecord {
    param([Parameter(Mandatory = $true)][string]$Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
}

function Write-TafRunRecord {
    param([Parameter(Mandatory = $true)][string]$Path,
          [Parameter(Mandatory = $true)][hashtable]$Record)
    $partial = "$Path.partial"
    ($Record | ConvertTo-Json -Depth 12) | Set-Content -LiteralPath $partial -Encoding UTF8
    Move-Item -LiteralPath $partial -Destination $Path -Force
}

function Get-TafGameBuildId {
    param([Parameter(Mandatory = $true)][string]$LogPath)
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { return $null }
    foreach ($line in Get-Content -LiteralPath $LogPath -TotalCount 400) {
        if ($line -match '(?i)(version|build)' -and $line -match '(\d+\.\d+\.\d+\.\d+)') {
            return $Matches[1]
        }
    }
    return $null
}

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

# Compiled once, then called in process for every exact Windows file handle.
$trustSource = Join-Path $PSScriptRoot 'ScenarioFileTrust.cs'
if (-not (Test-Path -LiteralPath $trustSource -PathType Leaf)) {
    throw "Scenario file-trust helper is missing: $trustSource"
}
Add-Type -Path $trustSource

function Get-ProfileInventory {
    param([Parameter(Mandatory = $true)][string]$TreeRoot)
    # Get-ChildItem reports descendants, not its starting item. Refuse a Local/ junction here or
    # every descendant could hash correctly while the whole sealed tree lives outside the profile.
    $tree = Get-Item -LiteralPath $TreeRoot -Force
    if (($tree.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Profile tree root is a reparse point: $TreeRoot"
    }
    if (-not ($tree -is [IO.DirectoryInfo])) {
        throw "Profile tree root is not a directory: $TreeRoot"
    }
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
        if (-not $item.FullName.StartsWith($prefix, [StringComparison]::Ordinal)) {
            throw "Profile tree escaped its root: $($item.FullName)"
        }
        $relative = $item.FullName.Substring($prefix.Length)
        $key = Get-NormalizedKey -Relative $relative
        if ($spellings.ContainsKey($key)) {
            throw "Two profile paths normalize to one name: $($spellings[$key]) and $relative"
        }
        $spellings[$key] = $relative
        # Read sharing admits the hasher and other readers but denies writers/deletion. Both link
        # counts and the digest use this one open identity; no path-only fsutil result is trusted.
        $stream = [IO.File]::Open($item.FullName, [IO.FileMode]::Open,
            [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $hardLinkCount = [ThousandAndFirst.Tools.ScenarioFileTrust]::GetLinkCount(
                $stream.SafeFileHandle)
            if ($hardLinkCount -ne 1) {
                throw "Profile tree contains a hard-linked file with $hardLinkCount names: $($item.FullName)"
            }
            $sha256 = [Security.Cryptography.SHA256]::Create()
            try {
                $digest = $sha256.ComputeHash($stream)
            } finally {
                $sha256.Dispose()
            }
            $hardLinkCountAfterHash = [ThousandAndFirst.Tools.ScenarioFileTrust]::GetLinkCount(
                $stream.SafeFileHandle)
            if ($hardLinkCountAfterHash -ne 1) {
                throw "Profile file gained hard links while hashing ($hardLinkCountAfterHash names): $($item.FullName)"
            }
            $found[$key] = ([BitConverter]::ToString($digest)).Replace('-', '').ToLowerInvariant()
        } finally {
            $stream.Dispose()
        }
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

$scriptPath = Join-Path $localRoot 'scenario-script.txt'
$scriptVerbs = @()
if (Test-Path -LiteralPath $scriptPath -PathType Leaf) {
    $scriptVerbs = @(Get-Content -LiteralPath $scriptPath |
        Where-Object { $_.Trim() -ne '' -and -not $_.Trim().StartsWith('#') })
}
# Boot, save and build remain whole-script commands; only the lifecycle variant may be followed
# by sealed AutoRunner lines, and even then its own command must be the first line.
$quickstartBanner = ($scriptVerbs.Count -eq 1 -and
    $scriptVerbs[0] -cmatch '\Aquickstart-(boot|save|build) (marsh|canyon|dunes) (yes|no)\z') -or
    ($scriptVerbs.Count -ge 1 -and
    $scriptVerbs[0] -cmatch '\Aquickstart-(lifecycle) (marsh|canyon|dunes) (yes|no)\z')
if ($quickstartBanner) {
    $quickstartPhase = $Matches[1]; $quickstartProfile = $Matches[2]; $quickstartAdvisor = $Matches[3]
}
Write-Host ''
if ($quickstartBanner) {
    Write-Host 'QUICKSTART PROFILE VERIFIED. Developer evidence only, not ordinary-play or release acceptance.'
    Write-Host "Mode: KingdomQuickstart; profile=$quickstartProfile; advisor=$quickstartAdvisor; frozen seed=$frozenSeed."
    if (Test-Path -LiteralPath (Join-Path $localRoot 'scenario-load.txt') -PathType Leaf) {
        Write-Host 'Sealed cold-load request present: the runtime loads its exact saved Quickstart; no new boot or AutoRunner.'
        Write-Host 'No manual input is required; the exact owned process must be stopped separately after its verdict.'
    } else {
        Write-Host 'The sealed developer request selects the real mode, location and seed before generation; no manual input or AutoRunner.'
        if ($quickstartPhase -ceq 'save') {
            Write-Host 'Requests a real Primary save after genuine boot checks, then parks before the first turn.'
            Write-Host 'The exact owned process must be stopped separately; cold-load is a separate check.'
        } elseif ($quickstartPhase -ceq 'build') {
            Write-Host 'After genuine boot checks, drives one real production commissioning call from starter stock.'
            Write-Host 'A production refusal is a distinct, expected terminal outcome, not a harness error; no save/load proof.'
        } else {
            Write-Host 'Boot-only observation of production founding and grants; no save/load proof.'
        }
    }
} else {
    Write-Host 'SCENARIO PROFILE VERIFIED. This profile produces developer evidence only:'
    Write-Host '  - a scenario-built state never signs native acceptance on its own;'
    Write-Host '  - verdicts stay ineligible until curated ordinary-play anchor evidence exists.'
    Write-Host ''
    Write-Host "ENTER world seed $frozenSeed yourself at character creation. Qud exposes no launcher-side"
    Write-Host 'seed injection, so this is manual operator entry; the gate refuses any other world.'
    Write-Host ''
    if (Test-Path -LiteralPath $scriptPath -PathType Leaf) {
        Write-Host "Sealed auto-runner script ($($scriptVerbs.Count) verb(s)): $($scriptVerbs -join ', ')"
        Write-Host 'It runs itself on your first turn in the world. No further keyboard input is needed.'
    } else {
        Write-Host 'No sealed auto-runner script; drive kingdom:scenario by hand.'
    }
}
Write-Host "Journal: $(Join-Path $rootPath 'scenario-journal.tsv')"
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
# The scenario journal is POST-SEAL OUTPUT, exactly like Player.log above. Assert-ClosedSeal ran
# once, before launch, over $localRoot only - the sealed launcher INPUTS. Both this file and the
# log sit beside that tree in $rootPath, so nothing a run writes is inside the inventory the seal
# closed, and no assertion here re-reads the profile after the game starts. Refused when it already
# exists for the same reason the log is: the journal is appended to, and two runs' rows in one file
# cannot be told apart.
$journalPath = Join-Path $rootPath 'scenario-journal.tsv'
if (Test-Path -LiteralPath $journalPath) {
    throw "scenario journal already exists: $journalPath"
}
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
. (Join-Path $PSScriptRoot 'scenario-process.ps1')
Assert-TafScenarioIdle -Game $Game
# Cross-version source/old-reader probes must retain exact process ownership even though no
# AutoRunner script is lawful there. This option never bypasses the closed seal above, starts
# no world, and is limited to the dedicated, sealed observer markers.
if ($OwnAttended) {
    $sourceMarker = Test-Path -LiteralPath (Join-Path $localRoot 'upgrade-save-request.txt') -PathType Leaf
    $readerMarker = Test-Path -LiteralPath (Join-Path $localRoot 'taf-downgrade-request.txt') -PathType Leaf
    $stageMarker = Test-Path -LiteralPath (Join-Path $localRoot 'upgrade-stage-source.txt') -PathType Leaf
    if ((@($sourceMarker, $readerMarker, $stageMarker) | Where-Object { $_ }).Count -ne 1 -or
        (Test-Path -LiteralPath (Join-Path $localRoot 'scenario-script.txt'))) {
        throw 'OwnAttended requires exactly one sealed cross-version observer marker and no script.'
    }
    if (@(Get-Process | Where-Object { $_.ProcessName -in @('CoQ', 'CavesOfQud') }).Count -ne 0) {
        throw 'A Qud process is already running; owned attended launch refused.'
    }
}
if ($StopRecord) {
    # The stop half, written after the owned process has ended. It never invents a stop for a
    # session that was never launched, and never overwrites one already recorded.
    $recordPath = Join-Path $rootPath 'run-record.json'
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) {
        throw 'This scenario root carries no run record to stop.'
    }
    $record = Read-TafRunRecord -Path $recordPath
    if (-not $record.ContainsKey('launchId')) { throw 'This run record names no launch to stop.' }
    if ($record.ContainsKey('stoppedUtc')) { throw 'This run record is already stopped.' }
    if (@(Get-Process | Where-Object { $_.ProcessName -in @('CoQ', 'CavesOfQud') }).Count -ne 0) {
        throw 'A Qud process is still running; stop it before recording the stop.'
    }
    $record['stoppedUtc'] = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $record['exitCode'] = $ExitCode
    $build = Get-TafGameBuildId -LogPath (Join-Path $rootPath 'Player.log')
    if ($build) { $record['gameBuildId'] = $build }
    Write-TafRunRecord -Path $recordPath -Record $record
    Write-Host "Run record stopped: $($record['stoppedUtc'])"
    exit 0
}

# A scripted profile runs itself, so its window must never steal the operator's focus.
# Minimizing is NOT safe - Unity may pause a minimized player and stall the runner - so the
# window is instead launched detached and, once it exists, moved to the bottom-right screen
# edge WITHOUT activation. It keeps rendering and the script keeps running; the operator's
# foreground window keeps the focus. Attended profiles (no sealed script) launch normally.
$scriptPath = Join-Path $localRoot 'scenario-script.txt'
if ((Test-Path -LiteralPath $scriptPath) -or $OwnAttended) {
    $process = Start-TafOwnedScenarioProcess -Root $rootPath -Game $Game
    $handedOff = $false
    try {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QuietWindow {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after,
        int x, int y, int w, int hh, uint flags);
}
'@
    Add-Type -AssemblyName System.Windows.Forms
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        if ($process.HasExited) { exit $process.ExitCode }
        if ($process.MainWindowHandle -ne 0) {
            $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
            # HWND_BOTTOM keeps it under every other window; SWP_NOACTIVATE (0x10) never takes
            # focus; a small edge placement keeps a sliver visible so rendering continues.
            [QuietWindow]::SetWindowPos($process.MainWindowHandle, [IntPtr]1,
                $area.Right - 480, $area.Bottom - 270, 480, 270, 0x10) | Out-Null
            break
        }
        Start-Sleep -Milliseconds 500
    }
    # The launch half of this session's run record, if preparation sealed one. The launch
    # identity is this process's own identity -- the launched PID and its start time -- so two
    # sessions can never be mistaken for one, and the record says when the window really opened
    # rather than when the operator typed the command.
    $recordPath = Join-Path $rootPath 'run-record.json'
    if (Test-Path -LiteralPath $recordPath -PathType Leaf) {
        $record = Read-TafRunRecord -Path $recordPath
        if ($record.ContainsKey('launchId')) { throw 'This run record already names a launch.' }
        $utcStart = $process.StartTime.ToUniversalTime()
        $record['launchId'] = "pid-$($process.Id)-$($utcStart.ToString('yyyyMMddTHHmmssZ'))"
        $record['started'] = $utcStart.ToString('yyyy-MM-ddTHH:mm:ssZ')
        Write-TafRunRecord -Path $recordPath -Record $record
        Write-Host "Run record launch: $($record['launchId'])"
    }
    Write-Host "Scenario game launched quietly (PID $($process.Id)); it will not take focus."
    Write-Host "Ownership receipt: $(Get-TafScenarioReceiptPath -Root $rootPath)"
    $handedOff = $true
    exit 0
    } finally {
        if (-not $handedOff -and -not $process.HasExited) {
            $process.Kill()
            if (-not $process.WaitForExit(10000)) { throw 'Failed scenario launch did not exit.' }
        }
        $process.Dispose()
    }
}
& $Game @arguments
exit $LASTEXITCODE
