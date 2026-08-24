param(
    [Parameter(Mandatory = $true)]
    [string]$Launcher,
    [Parameter(Mandatory = $true)]
    [string]$ConfiguredGame,
    [Parameter(Mandatory = $true)]
    [string]$AssemblyCSharp,
    [string]$KnownGoodSaveFixture = ''
)

$ErrorActionPreference = 'Stop'
$script:Roots = [Collections.Generic.List[string]]::new()
$script:Junctions = [Collections.Generic.List[string]]::new()
$script:AuxiliaryFiles = [Collections.Generic.List[string]]::new()
$script:AuxiliaryDirectories = [Collections.Generic.List[string]]::new()
$script:CaseCount = 0

function Write-Utf8NoBom {
    param([string]$Path, [string]$Value)
    [IO.File]::WriteAllText($Path, $Value, [Text.UTF8Encoding]::new($false))
}

function New-TestRoot {
    do {
        $path = 'C:\taf-smoke.harness' + [guid]::NewGuid().ToString('N')
    } while ((Test-Path -LiteralPath $path) -or (Test-Path -LiteralPath ($path + '.seal')))
    [void][IO.Directory]::CreateDirectory($path)
    [void]$script:Roots.Add($path)
    return $path
}

function Write-StageSeal {
    param($Profile)

    $prefix = $Profile.StageRoot.TrimEnd('\') + '\'
    $rows = [Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $Profile.StageRoot -File -Recurse | Sort-Object FullName)) {
        $relative = $file.FullName.Substring($prefix.Length).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        [void]$rows.Add("$hash  $relative")
    }
    [IO.File]::WriteAllLines($Profile.Seal, $rows, [Text.UTF8Encoding]::new($false))
}

function New-TestProfile {
    $root = New-TestRoot
    $local = Join-Path $root 'Local'
    $save = Join-Path $root 'Save'
    $synced = Join-Path $root 'Synced'
    $stage = Join-Path $local 'Mods\ThousandAndFirst'
    foreach ($directory in @($stage, $save, (Join-Path $synced 'Saves'))) {
        [void][IO.Directory]::CreateDirectory($directory)
    }
    Write-Utf8NoBom (Join-Path $stage 'manifest.json') '{"id":"r_ThousandAndFirst"}'
    Write-Utf8NoBom (Join-Path $stage 'payload.txt') 'trusted-stage-byte'
    Write-Utf8NoBom (Join-Path $local 'ModSettings.json') '{}'
    Write-Utf8NoBom (Join-Path $local 'PlayerOptions.json') '{}'
    $sealDirectory = $root + '.seal'
    [void][IO.Directory]::CreateDirectory($sealDirectory)
    $profile = [pscustomobject]@{
        Root = $root
        Local = $local
        SaveRoot = $save
        Synced = $synced
        SyncedSaves = Join-Path $synced 'Saves'
        StageRoot = $stage
        SealDirectory = $sealDirectory
        Seal = Join-Path $sealDirectory 'stage.sha256'
        Save = $null
    }
    Write-StageSeal $profile
    return $profile
}

function Set-LittleEndianInt32 {
    param([byte[]]$Buffer, [int]$Offset, [int]$Value)
    [Array]::Copy([BitConverter]::GetBytes($Value), 0, $Buffer, $Offset, 4)
}

function Set-LittleEndianInt64 {
    param([byte[]]$Buffer, [int]$Offset, [long]$Value)
    [Array]::Copy([BitConverter]::GetBytes($Value), 0, $Buffer, $Offset, 8)
}

function Set-BigEndianInt32 {
    param([byte[]]$Buffer, [int]$Offset, [int]$Value)
    $Buffer[$Offset] = [byte](($Value -shr 24) -band 0xff)
    $Buffer[$Offset + 1] = [byte](($Value -shr 16) -band 0xff)
    $Buffer[$Offset + 2] = [byte](($Value -shr 8) -band 0xff)
    $Buffer[$Offset + 3] = [byte]($Value -band 0xff)
}

function Write-GzipPayload {
    param([string]$Path, [byte[]]$Payload)

    $memory = [IO.MemoryStream]::new()
    try {
        $gzip = [IO.Compression.GZipStream]::new(
            $memory, [IO.Compression.CompressionLevel]::Optimal, $true)
        try {
            $gzip.Write($Payload, 0, $Payload.Length)
        }
        finally {
            $gzip.Dispose()
        }
        [IO.File]::WriteAllBytes($Path, $memory.ToArray())
    }
    finally {
        $memory.Dispose()
    }
}

function Write-QudGzip {
    param([string]$Path, [int]$FileVersion = 408)

    $payload = New-Object byte[] 80
    Set-LittleEndianInt32 $payload 0 $FileVersion
    Set-LittleEndianInt64 $payload 4 68
    Set-LittleEndianInt64 $payload 20 69
    # Genuine empty event-registry sections can share this offset.
    Set-LittleEndianInt64 $payload 12 69
    Set-LittleEndianInt64 $payload 36 70
    Set-LittleEndianInt64 $payload 28 71
    Set-LittleEndianInt64 $payload 44 72
    Set-LittleEndianInt64 $payload 52 73
    Set-LittleEndianInt32 $payload 60 0
    Set-LittleEndianInt32 $payload 64 0
    Write-GzipPayload $Path $payload
}

function Write-SqliteStub {
    param([string]$Path)

    $page = New-Object byte[] 512
    $magic = [Text.Encoding]::ASCII.GetBytes("SQLite format 3`0")
    [Array]::Copy($magic, 0, $page, 0, $magic.Length)
    $page[16] = 2
    $page[17] = 0
    $page[18] = 1
    $page[19] = 1
    $page[20] = 0
    $page[21] = 64
    $page[22] = 32
    $page[23] = 32
    Set-BigEndianInt32 $page 28 1
    Set-BigEndianInt32 $page 44 4
    Set-BigEndianInt32 $page 56 1
    $page[100] = 13
    [IO.File]::WriteAllBytes($Path, $page)
}

function New-PrimaryData {
    param([string]$Id, [string[]]$Mods, [string]$GameVersion)

    return [ordered]@{
        InfoVersion = 1
        SaveVersion = 408
        GameVersion = $GameVersion
        ID = $Id
        Name = 'Smoke'
        Level = 1
        GenoSubType = ''
        GameMode = 'Classic'
        CharIcon = 'Creatures/sw_gunslinger.bmp'
        FColor = 103
        DColor = 121
        Location = 'Joppa'
        InGameTime = '00:01:00'
        Turn = 1
        SaveTime = 'Monday, August 24, 2026 at 1:00:00 PM'
        ModsEnabled = $Mods
    }
}

function Write-PrimaryData {
    param($Save)
    $json = $Save.Data | ConvertTo-Json -Depth 5
    Write-Utf8NoBom $Save.PrimaryJson $json
}

function Add-ResumeSave {
    param($Profile, [string]$GameVersion)

    $id = [guid]::NewGuid().ToString('D')
    $saveDirectory = Join-Path $Profile.SyncedSaves $id
    [void][IO.Directory]::CreateDirectory($saveDirectory)
    $save = [pscustomobject]@{
        Id = $id
        Directory = $saveDirectory
        PrimaryJson = Join-Path $saveDirectory 'Primary.json'
        PrimaryGzip = Join-Path $saveDirectory 'Primary.sav.gz'
        Cache = Join-Path $saveDirectory 'Cache.db'
        Data = New-PrimaryData $id @('r_ThousandAndFirst', 'FreeholdGames_DLC_PetsPack1') $GameVersion
    }
    Write-PrimaryData $save
    Write-QudGzip $save.PrimaryGzip
    Write-SqliteStub $save.Cache

    # Synthetic representatives of regular engine artifacts; not claimed as captured bytes.
    [void][IO.Directory]::CreateDirectory((Join-Path $Profile.SaveRoot 'Mods'))
    [void][IO.Directory]::CreateDirectory((Join-Path $Profile.SaveRoot 'Saves'))
    [void][IO.Directory]::CreateDirectory((Join-Path $Profile.SaveRoot 'ModAssemblies'))
    Write-Utf8NoBom (Join-Path $Profile.SaveRoot 'build_log.txt') 'fixture'
    Write-Utf8NoBom (Join-Path $Profile.SaveRoot 'ModAssemblies\r_ThousandAndFirst.dll') 'fixture'
    $Profile.Save = $save
}

function Add-TafStageJournal {
    param($Profile, [string]$RecordOrigin = '')

    if ($null -eq $Profile.Save) {
        throw 'TAF stage journal fixture needs a resume save.'
    }
    if ([string]::IsNullOrEmpty($RecordOrigin)) {
        $RecordOrigin = $Profile.Save.Id
    }
    $stages = Join-Path $Profile.Synced 'ThousandAndFirst\Stages'
    [void][IO.Directory]::CreateDirectory($stages)
    $lock = Join-Path $stages ".journal-$($Profile.Save.Id).lock"
    [IO.File]::WriteAllBytes($lock, [byte[]]@())
    $seal = Join-Path $stages "$($Profile.Save.Id).a.seal"
    $body = '{"kind":"record","origin":"' + $RecordOrigin + '"}'
    $bodyBytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($body)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = ([BitConverter]::ToString($sha.ComputeHash($bodyBytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
    $envelope = "taf-seal 4`nsha256 $hash`nlength $($bodyBytes.LongLength)`n$body`n"
    Write-Utf8NoBom $seal $envelope
    return [pscustomobject]@{
        Stages = $stages
        Lock = $lock
        Seal = $seal
    }
}

function Add-KnownGoodSaveFixture {
    param($Profile, [string]$FixturePath)

    $source = [IO.Path]::GetFullPath($FixturePath).TrimEnd('\')
    if ($source -match '^[A-Za-z]:\\taf-smoke\.' -or
        $source -match '\\AppData\\LocalLow\\Freehold Games\\CavesOfQud\\') {
        throw "Known-good save fixture must be a sanitized copy, not a smoke/live profile: $source"
    }
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Known-good save fixture not found: $source"
    }
    $cursor = Get-Item -LiteralPath $source -Force
    while ($null -ne $cursor) {
        if (($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Known-good save fixture cannot traverse a reparse point: $($cursor.FullName)"
        }
        $cursor = $cursor.Parent
    }
    $resolvedSource = [TafSmokeNative.FileLinks]::Resolve($source)
    if ($resolvedSource -match '^[A-Za-z]:\\taf-smoke\.' -or
        $resolvedSource -match '\\AppData\\LocalLow\\Freehold Games\\CavesOfQud\\') {
        throw "Known-good save fixture resolves to a smoke/live profile: $source -> $resolvedSource"
    }
    $sourceChildren = @(Get-ChildItem -LiteralPath $source -Force)
    foreach ($child in $sourceChildren) {
        if ($child.PSIsContainer -or
            ($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Known-good save fixture must contain regular files only: $($child.FullName)"
        }
        if ([TafSmokeNative.FileLinks]::Count($child.FullName) -ne 1) {
            throw "Known-good save fixture must not contain hard-linked files: $($child.FullName)"
        }
    }
    $sourceJson = Join-Path $source 'Primary.json'
    if (-not (Test-Path -LiteralPath $sourceJson -PathType Leaf)) {
        throw "Known-good save fixture lacks Primary.json: $source"
    }
    try {
        $id = (Get-Content -LiteralPath $sourceJson -Raw | ConvertFrom-Json).ID
    }
    catch {
        throw "Known-good save fixture has invalid Primary.json: $sourceJson"
    }
    $parsed = [guid]::Empty
    if (-not ($id -is [string]) -or -not [guid]::TryParseExact($id, 'D', [ref]$parsed)) {
        throw "Known-good save fixture has invalid ID: $sourceJson"
    }
    $destination = Join-Path $Profile.SyncedSaves $id
    [void][IO.Directory]::CreateDirectory($destination)
    foreach ($child in $sourceChildren) {
        [IO.File]::Copy($child.FullName, (Join-Path $destination $child.Name))
    }
    [void][IO.Directory]::CreateDirectory((Join-Path $Profile.SaveRoot 'Mods'))
    [void][IO.Directory]::CreateDirectory((Join-Path $Profile.SaveRoot 'Saves'))
}

function Invoke-ExpectedFailure {
    param(
        [string]$Name,
        $Profile,
        [string]$Expected,
        [switch]$Resume,
        [string]$Game = $script:MissingGame,
        [switch]$ValidateOnly
    )

    $parameters = @{ Root = $Profile.Root; Game = $Game }
    if ($Resume) { $parameters.Resume = $true }
    if ($ValidateOnly) { $parameters.ValidateOnly = $true }
    $caught = $null
    try {
        & $script:LauncherUnderTest @parameters | Out-Null
    }
    catch {
        $caught = $_.Exception.Message
    }
    if ($null -eq $caught) {
        throw "${Name}: launcher unexpectedly succeeded"
    }
    if ($caught -cne $Expected) {
        throw "${Name}: expected '$Expected'; got '$caught'"
    }
    if (Test-Path -LiteralPath $script:TripwireMarker) {
        throw "${Name}: game tripwire was launched"
    }
    $script:CaseCount++
    Write-Output "PASS: $Name"
}

function Invoke-KnownGoodFixtureExpectedFailure {
    param(
        [string]$Name,
        $Profile,
        [string]$Fixture,
        [string]$Expected
    )

    $caught = $null
    try {
        Add-KnownGoodSaveFixture $Profile $Fixture
    }
    catch {
        $caught = $_.Exception.Message
    }
    if ($null -eq $caught) {
        throw "${Name}: fixture import unexpectedly succeeded"
    }
    if ($caught -cne $Expected) {
        throw "${Name}: expected '$Expected'; got '$caught'"
    }
    $script:CaseCount++
    Write-Output "PASS: $Name"
}

function Invoke-ValidationSuccess {
    param([string]$Name, $Profile, [switch]$Resume)

    $parameters = @{
        Root = $Profile.Root
        Game = $script:TripwireGame
        ValidateOnly = $true
    }
    if ($Resume) { $parameters.Resume = $true }
    $output = @(& $script:LauncherUnderTest @parameters)
    $expected = "SMOKE VALIDATION CLEAN: $(if ($Resume) { 'resume' } else { 'fresh' })"
    if ($output.Count -ne 1 -or $output[0] -cne $expected) {
        throw "${Name}: expected '$expected'; got '$($output -join ' | ')'"
    }
    if (Test-Path -LiteralPath $script:TripwireMarker) {
        throw "${Name}: game tripwire was launched"
    }
    $script:CaseCount++
    Write-Output "PASS: $Name"
}

function Invoke-ConfiguredDefaultValidation {
    param($Profile)

    $priorRoot = $env:TAF_QUD_ROOT
    try {
        $env:TAF_QUD_ROOT = $script:SupportRoot
        $output = @(& $script:LauncherUnderTest -Root $Profile.Root -ValidateOnly)
    }
    finally {
        $env:TAF_QUD_ROOT = $priorRoot
    }
    if ($output.Count -ne 1 -or $output[0] -cne 'SMOKE VALIDATION CLEAN: fresh') {
        throw "configured TAF_QUD_ROOT default: unexpected output '$($output -join ' | ')'"
    }
    $script:CaseCount++
    Write-Output 'PASS: configured TAF_QUD_ROOT default selects CoQ.exe without launch'
}

function Invoke-FakeLaunchSuccess {
    param(
        [string]$Name,
        $Profile,
        [string]$Game,
        [string]$ExpectedProcessName,
        [switch]$Resume
    )

    $receipt = Join-Path $script:SupportRoot ('argv-' + [guid]::NewGuid().ToString('N') + '.txt')
    $pidReceipt = $receipt + '.pid'
    $priorReceipt = $env:TAF_SMOKE_FAKE_RECEIPT
    $launchedPid = $null
    try {
        $env:TAF_SMOKE_FAKE_RECEIPT = $receipt
        $parameters = @{ Root = $Profile.Root; Game = $Game }
        if ($Resume) { $parameters.Resume = $true }
        $output = @(& $script:LauncherUnderTest @parameters)
        if (-not (Test-Path -LiteralPath $pidReceipt -PathType Leaf)) {
            throw "${Name}: fake game did not write PID receipt"
        }
        $launchedPid = [int]([IO.File]::ReadAllText($pidReceipt))
        $expectedOutput = @(
            "SMOKE STARTED: PID $launchedPid",
            "Profile: $($Profile.Root)",
            "Mode: $(if ($Resume) { 'resume' } else { 'fresh' })",
            "Log: $(Join-Path $Profile.Root 'Player.log')",
            'After quitting, move Player.log outside the profile and run Tools/check-player-log.sh on it before -Resume.'
        )
        if (($output -join "`n") -cne ($expectedOutput -join "`n")) {
            throw "${Name}: unexpected launcher output '$($output -join ' | ')'"
        }
        $process = Get-Process -Id $launchedPid -ErrorAction Stop
        if ($process.ProcessName -cne $ExpectedProcessName -or $process.HasExited) {
            throw "${Name}: fake process identity/liveness mismatch"
        }
        $expectedArguments = @(
            '-savepath', $Profile.SaveRoot,
            '-sharedpath', $Profile.Local,
            '-syncedpath', $Profile.Synced,
            '-logFile', (Join-Path $Profile.Root 'Player.log'),
            'NOMETRICS', 'STEAM:NO', 'GALAXY:NO'
        )
        $actualArguments = @([IO.File]::ReadAllLines($receipt))
        if (($actualArguments -join "`n") -cne ($expectedArguments -join "`n")) {
            throw "${Name}: argv mismatch; got '$($actualArguments -join ' | ')'"
        }
        if (Test-Path -LiteralPath (Join-Path $Profile.Root 'Player.log')) {
            throw "${Name}: fake game unexpectedly created Unity log path"
        }
    }
    finally {
        $env:TAF_SMOKE_FAKE_RECEIPT = $priorReceipt
        if ($null -eq $launchedPid -and (Test-Path -LiteralPath $pidReceipt -PathType Leaf)) {
            $launchedPid = [int]([IO.File]::ReadAllText($pidReceipt))
        }
        if ($null -ne $launchedPid) {
            Stop-Process -Id $launchedPid -Force -ErrorAction SilentlyContinue
            $remaining = Get-Process -Id $launchedPid -ErrorAction SilentlyContinue
            if ($null -ne $remaining) { $remaining.WaitForExit() }
        }
    }
    if (Get-Process -Id $launchedPid -ErrorAction SilentlyContinue) {
        throw "${Name}: fake process survived cleanup"
    }
    foreach ($path in @($receipt, $pidReceipt)) {
        if (Test-Path -LiteralPath $path) { [IO.File]::Delete($path) }
    }
    $script:CaseCount++
    Write-Output "PASS: $Name"
}

function Remove-ControlledPaths {
    foreach ($junction in @($script:Junctions | Sort-Object Length -Descending)) {
        $item = Get-Item -LiteralPath $junction -Force -ErrorAction SilentlyContinue
        if ($null -ne $item) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
                throw "Harness refused non-reparse junction cleanup path: $junction"
            }
            [IO.Directory]::Delete($junction)
        }
        if ($null -ne (Get-Item -LiteralPath $junction -Force -ErrorAction SilentlyContinue)) {
            throw "Harness could not remove junction: $junction"
        }
    }
    foreach ($root in @($script:Roots)) {
        if ($root -notmatch '^C:\\taf-smoke\.harness[0-9a-f]{32}$') {
            throw "Harness refused unexpected root cleanup path: $root"
        }
        if (Test-Path -LiteralPath $root) {
            [IO.Directory]::Delete($root, $true)
        }
        $sealDirectory = $root + '.seal'
        if (Test-Path -LiteralPath $sealDirectory) {
            [IO.Directory]::Delete($sealDirectory, $true)
        }
    }
    foreach ($file in @($script:AuxiliaryFiles)) {
        if (Test-Path -LiteralPath $file) {
            [IO.File]::Delete($file)
        }
    }
    foreach ($directory in @($script:AuxiliaryDirectories | Sort-Object Length -Descending)) {
        if (Test-Path -LiteralPath $directory) {
            [IO.Directory]::Delete($directory, $true)
        }
    }
    if (Test-Path -LiteralPath $script:SupportRoot) {
        if ($script:SupportRoot -notmatch '^C:\\taf-smoke-harness\.[0-9a-f]{32}$') {
            throw "Harness refused unexpected support cleanup path: $script:SupportRoot"
        }
        [IO.Directory]::Delete($script:SupportRoot, $true)
    }
}

if (-not (Test-Path -LiteralPath $Launcher -PathType Leaf)) {
    throw "Smoke launcher not found: $Launcher"
}
if (-not (Test-Path -LiteralPath $ConfiguredGame -PathType Leaf)) {
    throw "Configured Caves of Qud executable not found: $ConfiguredGame"
}
if (-not (Test-Path -LiteralPath $AssemblyCSharp -PathType Leaf)) {
    throw "Assembly-CSharp not found: $AssemblyCSharp"
}
$configuredGameFull = [IO.Path]::GetFullPath($ConfiguredGame)
$configuredDataDirectory = [IO.Path]::GetFileNameWithoutExtension($configuredGameFull) + '_Data'
$configuredAssembly = Join-Path (Split-Path -Parent $configuredGameFull) `
    "$configuredDataDirectory\Managed\Assembly-CSharp.dll"
if (-not $configuredAssembly.Equals([IO.Path]::GetFullPath($AssemblyCSharp),
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "Configured Game and Assembly-CSharp do not share one game root: $configuredGameFull"
}
$tokens = $null
$parseErrors = $null
$launcherAst = [Management.Automation.Language.Parser]::ParseFile(
    $Launcher, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) {
    throw "Smoke launcher has parser errors: $($parseErrors -join ' | ')"
}
$parameterNames = @($launcherAst.ParamBlock.Parameters |
    ForEach-Object { $_.Name.VariablePath.UserPath })
foreach ($requiredParameter in @('Root', 'Game', 'Resume', 'ValidateOnly')) {
    if ($parameterNames -cnotcontains $requiredParameter) {
        throw "Smoke launcher lacks required parameter: $requiredParameter"
    }
}
$stringLiterals = @($launcherAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.StringConstantExpressionAst]
}, $true) | ForEach-Object Value)
foreach ($requiredLiteral in @('CoQ.exe', '-logFile', 'NOMETRICS', 'CoQ', 'CavesOfQud')) {
    if ($stringLiterals -cnotcontains $requiredLiteral) {
        throw "Smoke launcher lacks required launch/process literal: $requiredLiteral"
    }
}

$supportId = [guid]::NewGuid().ToString('N')
$script:SupportRoot = "C:\taf-smoke-harness.$supportId"
if (Test-Path -LiteralPath $script:SupportRoot) {
    throw "Harness support path unexpectedly exists: $script:SupportRoot"
}
[void][IO.Directory]::CreateDirectory($script:SupportRoot)
$quotedLauncherDirectory = Join-Path $script:SupportRoot "quote ' path"
[void][IO.Directory]::CreateDirectory($quotedLauncherDirectory)
$script:LauncherUnderTest = Join-Path $quotedLauncherDirectory 'run smoke.ps1'
[IO.File]::Copy($Launcher, $script:LauncherUnderTest)

$fakeTemplate = Join-Path $script:SupportRoot 'FakeSmokeGame.exe'
$fakeSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

internal static class FakeSmokeGame
{
    private static int Main(string[] args)
    {
        string receipt = Environment.GetEnvironmentVariable("TAF_SMOKE_FAKE_RECEIPT");
        if (String.IsNullOrWhiteSpace(receipt))
            return 3;
        File.WriteAllLines(receipt, args);
        File.WriteAllText(receipt + ".pid", Process.GetCurrentProcess().Id.ToString());
        Thread.Sleep(TimeSpan.FromMinutes(2));
        return 0;
    }
}
'@
Add-Type -TypeDefinition $fakeSource -Language CSharp -OutputAssembly $fakeTemplate `
    -OutputType WindowsApplication | Out-Null
foreach ($fakeName in @('CoQ', 'CavesOfQud')) {
    [IO.File]::Copy($fakeTemplate, (Join-Path $script:SupportRoot "$fakeName.exe"))
    $fakeManaged = Join-Path $script:SupportRoot "${fakeName}_Data\Managed"
    [void][IO.Directory]::CreateDirectory($fakeManaged)
    [IO.File]::Copy($AssemblyCSharp, (Join-Path $fakeManaged 'Assembly-CSharp.dll'))
}

$script:TripwireMarker = Join-Path $script:SupportRoot 'GAME-WAS-LAUNCHED'
$script:TripwireGame = Join-Path $script:SupportRoot 'tripwire.cmd'
Write-Utf8NoBom $script:TripwireGame "@echo off`r`n> `"$script:TripwireMarker`" echo launched`r`n"
$managed = Join-Path $script:SupportRoot 'tripwire_Data\Managed'
[void][IO.Directory]::CreateDirectory($managed)
[IO.File]::Copy($AssemblyCSharp, (Join-Path $managed 'Assembly-CSharp.dll'))
$script:GameVersion = [Reflection.AssemblyName]::GetAssemblyName($AssemblyCSharp).Version.ToString()
$script:MissingGame = Join-Path $script:SupportRoot 'guaranteed-missing\CoQ.exe'
$missingGameFull = [IO.Path]::GetFullPath($script:MissingGame)

try {
    $profile = New-TestProfile
    Invoke-ValidationSuccess 'fresh ValidateOnly does not launch tripwire' $profile

    $profile = New-TestProfile
    $liveFixture = 'C:\Users\TAF-Smoke-Harness\AppData\LocalLow\Freehold Games\CavesOfQud\Saves\fixture'
    Invoke-KnownGoodFixtureExpectedFailure 'known-good fixture rejects live-profile path' $profile $liveFixture "Known-good save fixture must be a sanitized copy, not a smoke/live profile: $liveFixture"

    $profile = New-TestProfile
    $knownTarget = Join-Path $script:SupportRoot 'known-fixture-target'
    $knownJunction = Join-Path $script:SupportRoot 'known-fixture-junction'
    [void][IO.Directory]::CreateDirectory($knownTarget)
    [void](New-Item -ItemType Junction -Path $knownJunction -Target $knownTarget)
    [void]$script:Junctions.Add($knownJunction)
    Invoke-KnownGoodFixtureExpectedFailure 'known-good fixture root junction' $profile `
        $knownJunction "Known-good save fixture cannot traverse a reparse point: $knownJunction"

    $profile = New-TestProfile
    $knownHardlinkDirectory = Join-Path $script:SupportRoot 'known-fixture-hardlink'
    [void][IO.Directory]::CreateDirectory($knownHardlinkDirectory)
    $knownHardlinkTarget = Join-Path $script:SupportRoot 'known-fixture-hardlink-target.json'
    Write-Utf8NoBom $knownHardlinkTarget '{}'
    $knownHardlink = Join-Path $knownHardlinkDirectory 'Primary.json'
    [void](New-Item -ItemType HardLink -Path $knownHardlink -Target $knownHardlinkTarget)
    Invoke-KnownGoodFixtureExpectedFailure 'known-good fixture hardlink' $profile `
        $knownHardlinkDirectory "Known-good save fixture must not contain hard-linked files: $knownHardlink"

    $profile = New-TestProfile
    Invoke-ConfiguredDefaultValidation $profile

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-QudGzip (Join-Path $profile.Save.Directory 'Primary.sav.gz.bak')
    Write-Utf8NoBom (Join-Path $profile.Save.Directory 'Cache.db-wal') 'allowed-sidecar'
    Write-Utf8NoBom (Join-Path $profile.Save.Directory 'Cache.db-shm') 'allowed-sidecar'
    Invoke-ValidationSuccess 'resume accepts synthetic shape, sidecars, and equal Qud offsets' $profile -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $journal = Add-TafStageJournal $profile
    Invoke-ValidationSuccess 'resume accepts exact TAF stage journal' $profile -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $journal = Add-TafStageJournal $profile
    $sealText = [IO.File]::ReadAllText($journal.Seal)
    Write-Utf8NoBom $journal.Seal $sealText.Replace("taf-seal 4`n", "taf-seal 3`n")
    Invoke-ExpectedFailure 'TAF stage seal malformed envelope' $profile `
        "Resume TAF stage seal has an invalid envelope: $($journal.Seal)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $journal = Add-TafStageJournal $profile
    $sealText = [IO.File]::ReadAllText($journal.Seal)
    $zeroHash = '0' * 64
    $sealText = [regex]::Replace(
        $sealText, '(?m)^sha256 [0-9a-f]{64}$', "sha256 $zeroHash")
    Write-Utf8NoBom $journal.Seal $sealText
    Invoke-ExpectedFailure 'TAF stage seal digest mismatch' $profile `
        "Resume TAF stage seal digest differs from its body: $($journal.Seal)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $foreignOrigin = [guid]::NewGuid().ToString('D')
    $journal = Add-TafStageJournal $profile $foreignOrigin
    Invoke-ExpectedFailure 'TAF stage seal foreign origin' $profile `
        "Resume TAF stage seal belongs to another origin: $($journal.Seal)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $journal = Add-TafStageJournal $profile
    Write-Utf8NoBom (Join-Path $journal.Stages 'unexpected.tmp') 'unexpected'
    Invoke-ExpectedFailure 'TAF stage journal extra entry' $profile `
        "Resume TAF stage journal has partial or unexpected entries: $($journal.Stages)" -Resume

    $syntheticFixture = Join-Path $script:SupportRoot 'synthetic-sanitized-save'
    [void][IO.Directory]::CreateDirectory($syntheticFixture)
    $syntheticId = [guid]::NewGuid().ToString('D')
    $syntheticSave = [pscustomobject]@{
        PrimaryJson = Join-Path $syntheticFixture 'Primary.json'
        Data = New-PrimaryData $syntheticId @('r_ThousandAndFirst') $script:GameVersion
    }
    Write-PrimaryData $syntheticSave
    Write-QudGzip (Join-Path $syntheticFixture 'Primary.sav.gz')
    Write-SqliteStub (Join-Path $syntheticFixture 'Cache.db')
    $profile = New-TestProfile
    Add-KnownGoodSaveFixture $profile $syntheticFixture
    Invoke-ValidationSuccess 'sanitized fixture importer accepts synthetic shape' $profile -Resume

    $profile = New-TestProfile
    Invoke-FakeLaunchSuccess 'successful CoQ.exe launch records exact argv and cleans process' `
        $profile (Join-Path $script:SupportRoot 'CoQ.exe') 'CoQ'

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Invoke-FakeLaunchSuccess 'successful CavesOfQud.exe resume records exact argv and cleans process' `
        $profile (Join-Path $script:SupportRoot 'CavesOfQud.exe') 'CavesOfQud' -Resume

    if (-not [string]::IsNullOrWhiteSpace($KnownGoodSaveFixture)) {
        $profile = New-TestProfile
        Add-KnownGoodSaveFixture $profile $KnownGoodSaveFixture
        Invoke-ValidationSuccess 'optional sanitized known-good save fixture' $profile -Resume
    }

    foreach ($guardName in @('CoQ', 'CavesOfQud')) {
        $profile = New-TestProfile
        $guardDirectory = Join-Path $script:SupportRoot ('guard-' + $guardName)
        [void][IO.Directory]::CreateDirectory($guardDirectory)
        $fakeExecutable = Join-Path $guardDirectory "$guardName.exe"
        [IO.File]::Copy('C:\Windows\System32\PING.EXE', $fakeExecutable)
        $fakeProcess = Start-Process -FilePath $fakeExecutable -ArgumentList @('-t', '127.0.0.1') `
            -WindowStyle Hidden -PassThru
        try {
            Start-Sleep -Milliseconds 150
            Invoke-ExpectedFailure "existing $guardName process guard" $profile `
                'Caves of Qud is already running; refusing to mix smoke profiles.' `
                -Game $script:TripwireGame
        }
        finally {
            Stop-Process -Id $fakeProcess.Id -Force -ErrorAction SilentlyContinue
            $fakeProcess.WaitForExit()
        }
    }

    $profile = New-TestProfile
    Invoke-ExpectedFailure 'fresh reaches guaranteed missing Game boundary' $profile `
        "Caves of Qud executable not found: $missingGameFull"

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Invoke-ExpectedFailure 'resume reaches guaranteed missing Game boundary' $profile `
        "Caves of Qud executable not found: $missingGameFull" -Resume

    $profile = New-TestProfile
    $extraDll = Join-Path $profile.StageRoot 'injected.dll'
    Write-Utf8NoBom $extraDll 'injected'
    Invoke-ExpectedFailure 'extra staged DLL' $profile `
        'Staged mod inventory differs from trusted manifest: unexpected injected.dll'

    $profile = New-TestProfile
    Write-Utf8NoBom (Join-Path $profile.StageRoot 'payload.txt') 'changed-stage-byte'
    Invoke-ExpectedFailure 'changed staged byte' $profile `
        'Staged mod hash differs from trusted manifest: payload.txt'

    $profile = New-TestProfile
    $junctionTarget = Join-Path $script:SupportRoot ('junction-target-' + [guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($junctionTarget)
    [void]$script:AuxiliaryDirectories.Add($junctionTarget)
    $junction = Join-Path $profile.StageRoot 'nested-junction'
    [void](New-Item -ItemType Junction -Path $junction -Target $junctionTarget)
    [void]$script:Junctions.Add($junction)
    Invoke-ExpectedFailure 'nested staged junction' $profile `
        "Refusing reparse point inside smoke profile: $junction"

    $profile = New-TestProfile
    $hardlinkSource = Join-Path $script:SupportRoot ('hardlink-source-' + [guid]::NewGuid().ToString('N'))
    Write-Utf8NoBom $hardlinkSource 'hardlink'
    [void]$script:AuxiliaryFiles.Add($hardlinkSource)
    $hardlink = Join-Path $profile.StageRoot 'hardlinked.dll'
    [void](New-Item -ItemType HardLink -Path $hardlink -Target $hardlinkSource)
    Invoke-ExpectedFailure 'staged hardlink' $profile `
        "Refusing hard-linked file inside smoke profile: $hardlink"

    $profile = New-TestProfile
    $sealHardlink = Join-Path $script:SupportRoot ('seal-hardlink-' + [guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType HardLink -Path $sealHardlink -Target $profile.Seal)
    [void]$script:AuxiliaryFiles.Add($sealHardlink)
    Invoke-ExpectedFailure 'hard-linked trusted seal' $profile `
        "Trusted stage manifest is hard-linked: $($profile.Seal)"

    $profile = New-TestProfile
    $logPath = Join-Path $profile.Root 'Player.log'
    Write-Utf8NoBom $logPath 'stale log'
    Invoke-ExpectedFailure 'pre-existing Unity log' $profile `
        "Smoke log file must not preexist: $logPath"

    $profile = New-TestProfile
    $logSource = Join-Path $script:SupportRoot ('log-hardlink-' + [guid]::NewGuid().ToString('N'))
    Write-Utf8NoBom $logSource 'aliased log'
    [void]$script:AuxiliaryFiles.Add($logSource)
    $logPath = Join-Path $profile.Root 'Player.log'
    [void](New-Item -ItemType HardLink -Path $logPath -Target $logSource)
    Invoke-ExpectedFailure 'hard-linked Unity log alias' $profile `
        "Refusing aliased smoke log path: $logPath"

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    [IO.File]::WriteAllBytes($profile.Save.PrimaryGzip, [byte[]]@(1, 2, 3, 4))
    Invoke-ExpectedFailure 'bad gzip magic' $profile `
        "Resume save gzip header is invalid: $($profile.Save.PrimaryGzip)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-GzipPayload $profile.Save.PrimaryGzip ([Text.Encoding]::ASCII.GetBytes('junk'))
    Invoke-ExpectedFailure 'valid gzip with no Qud header' $profile `
        "Resume save gzip lacks a Qud serialization header: $($profile.Save.PrimaryGzip)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-QudGzip $profile.Save.PrimaryGzip 407
    Invoke-ExpectedFailure 'wrong Qud serialization version' $profile `
        "Resume save gzip has an invalid Qud serialization header: $($profile.Save.PrimaryGzip)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    [IO.File]::WriteAllBytes($profile.Save.Cache, (New-Object byte[] 64))
    Invoke-ExpectedFailure 'short cache database' $profile `
        "Resume cache database is too short: $($profile.Save.Cache)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $badCache = [IO.File]::ReadAllBytes($profile.Save.Cache)
    $badCache[0] = 0
    [IO.File]::WriteAllBytes($profile.Save.Cache, $badCache)
    Invoke-ExpectedFailure 'bad SQLite magic' $profile `
        "Resume cache database header is invalid: $($profile.Save.Cache)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $badCache = [IO.File]::ReadAllBytes($profile.Save.Cache)
    $badCache[16] = 0
    $badCache[17] = 3
    [IO.File]::WriteAllBytes($profile.Save.Cache, $badCache)
    Invoke-ExpectedFailure 'bad SQLite page structure' $profile `
        "Resume cache database structure is invalid: $($profile.Save.Cache)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.Remove('Turn')
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'missing Primary.json field' $profile `
        "Resume Primary.json is missing required field Turn: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $id = $profile.Save.Data.ID
    $profile.Save.Data.Remove('ID')
    $profile.Save.Data.Add('id', $id)
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'wrong-case Primary.json field' $profile `
        "Resume Primary.json is missing required field ID: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.SaveVersion = 407
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'wrong Primary.json SaveVersion' $profile `
        "Resume Primary.json has out-of-range numeric fields: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.GameVersion = '9.9.9.9'
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'Primary.json GameVersion differs from installed build' $profile `
        "Resume Primary.json GameVersion does not match installed Caves of Qud: 9.9.9.9 != $script:GameVersion" `
        -Resume -Game $script:TripwireGame -ValidateOnly

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.ModsEnabled = @('r_ThousandAndFirst', 'unrelated_mod')
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'unrelated enabled mod' $profile `
        "Resume save enables unrelated mod unrelated_mod: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.ModsEnabled = @('r_ThousandAndFirst', 'r_ThousandAndFirst')
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'duplicate enabled mod' $profile `
        "Resume save repeats mod r_ThousandAndFirst: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    $profile.Save.Data.ModsEnabled = @('FreeholdGames_DLC_PetsPack1')
    Write-PrimaryData $profile.Save
    Invoke-ExpectedFailure 'missing TAF mod identity' $profile `
        "Resume save does not enable r_ThousandAndFirst: $($profile.Save.PrimaryJson)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-Utf8NoBom (Join-Path $profile.SaveRoot 'Saves\shadow') 'shadow'
    Invoke-ExpectedFailure 'shadow Save/Saves entry' $profile `
        "Resume profile contains shadow save entries: $(Join-Path $profile.SaveRoot 'Saves')" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-Utf8NoBom (Join-Path $profile.SaveRoot 'Mods\other-mod') 'shadow'
    Invoke-ExpectedFailure 'save-local mod entry' $profile `
        "Resume profile contains save-local mods: $(Join-Path $profile.SaveRoot 'Mods')" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    Write-Utf8NoBom (Join-Path $profile.Save.Directory 'Primary.sav.json') '{}'
    Invoke-ExpectedFailure 'aliased save-info file' $profile `
        "Resume save has partial or aliased entries: $($profile.Save.Directory)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    [void][IO.Directory]::CreateDirectory((Join-Path $profile.SyncedSaves ([guid]::NewGuid().ToString('D'))))
    Invoke-ExpectedFailure 'multiple save directories' $profile `
        "Resume needs exactly one isolated save directory: $($profile.SyncedSaves)" -Resume

    $profile = New-TestProfile
    Add-ResumeSave $profile $script:GameVersion
    [IO.File]::Delete($profile.Save.Cache)
    Invoke-ExpectedFailure 'partial save directory' $profile `
        "Resume save has partial or aliased entries: $($profile.Save.Directory)" -Resume

    Write-Output "SMOKE LAUNCHER HARNESS CLEAN ($script:CaseCount cases)"
}
finally {
    Remove-ControlledPaths
}
