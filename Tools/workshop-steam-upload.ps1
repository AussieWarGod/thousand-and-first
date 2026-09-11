param(
    [Parameter(Mandatory = $true)][string]$QudRoot,
    [Parameter(Mandatory = $true)][string]$PlanPath,
    [Parameter(Mandatory = $true)][string]$PlanSHA,
    [Parameter(Mandatory = $true)][string]$ItemId,
    [Parameter(Mandatory = $true)][string]$ChangeNotePath,
    [Alias('AttemptRoot')][string]$StateRoot = 'C:\taf-workshop-state.dRBivM',
    [Parameter(Mandatory = $true)][string]$ReceiptSHA,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot,
    [switch]$Submit,
    [switch]$Inspect,
    [switch]$Verify,
    [switch]$Finalize
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The publisher compiles in one registry root. The launcher refuses every alternate here, before
# any path probe, SDK check, build or child process. Reconciliation is never a different root.
$FixedStateRoot = 'C:\taf-workshop-state.dRBivM'
if (!$StateRoot.Equals($FixedStateRoot, [StringComparison]::Ordinal)) {
    throw "Only the fixed registry root $FixedStateRoot is admitted; alternate state roots are refused."
}
function Get-ReleaseInvocation {
    param([switch]$Submit, [switch]$Inspect, [switch]$Verify, [switch]$Finalize,
        [string]$PlanFile, [string]$PlanSHA, [string]$ItemId, [string]$NoteFile,
        [string]$FixedStateRoot, [string]$ReceiptSHA)
    if (([int][bool]$Submit + [int][bool]$Inspect + [int][bool]$Verify + [int][bool]$Finalize) -gt 1) {
        throw 'Choose only one of -Submit, -Inspect, -Verify or -Finalize.'
    }
    $Mode = 'check'
    if ($Submit) { $Mode = 'submit' }
    elseif ($Inspect) { $Mode = 'inspect' }
    elseif ($Verify) { $Mode = 'verify' }
    elseif ($Finalize) { $Mode = 'finalize' }
    $Project = 'WorkshopUpload.csproj'; $Assembly = 'TafWorkshopUpload.dll'
    $Arguments = @($Mode, $PlanFile, $PlanSHA, $ItemId, $NoteFile, $FixedStateRoot, $ReceiptSHA)
    if ($Verify -or $Finalize) {
        $Project = 'WorkshopDelivery.csproj'; $Assembly = 'TafWorkshopDelivery.dll'
        $Arguments = @($Mode, $PlanFile, $PlanSHA, $ItemId, $ReceiptSHA)
    }
    return [pscustomobject]@{ Mode = $Mode; Project = $Project; Assembly = $Assembly; Arguments = $Arguments }
}
$null = Get-ReleaseInvocation -Submit:$Submit -Inspect:$Inspect -Verify:$Verify -Finalize:$Finalize

function Require-OrdinaryPath([string]$Path, [bool]$Directory) {
    if ($Path -cnotmatch '^[A-Za-z]:[\\/]' -or $Path -match '[\p{Cc}"]' -or
        $Path.Substring(2).Contains(':')) { throw 'Expected an ordinary local Windows path.' }
    $Drive = New-Object IO.DriveInfo -ArgumentList ($Path.Substring(0, 3))
    if ($Drive.DriveType -eq [IO.DriveType]::Network) { throw 'Network drives are not admitted.' }
    $Entry = Get-Item -LiteralPath $Path -Force
    if ($Entry.PSIsContainer -ne $Directory) { throw 'Unexpected file type.' }
    $Cursor = $Entry
    while ($null -ne $Cursor) {
        if (($Cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Linked paths are not admitted.'
        }
        if ($Cursor -is [IO.DirectoryInfo]) { $Cursor = $Cursor.Parent }
        else { $Cursor = $Cursor.Directory }
    }
    return [IO.Path]::GetFullPath($Entry.FullName)
}

function Convert-PlanPath([string]$Path) {
    if ($Path -cnotmatch '^/mnt/([a-z])/(.+)$' -or $Path.Contains('\')) {
        throw 'Plan paths must use canonical /mnt/<lowercase drive>/ paths.'
    }
    $Drive = $Matches[1]
    $Tail = $Matches[2]
    foreach ($Segment in $Tail.Split('/')) {
        if (!$Segment -or $Segment -in @('.', '..') -or $Segment -match '[. ]$') {
            throw 'Noncanonical plan path.'
        }
    }
    return $Drive + ':\' + $Tail.Replace('/', '\')
}

function Contains-Directory([string]$Parent, [string]$Child) {
    $Prefix = $Parent.TrimEnd([char[]]'\/')
    return $Child.Equals($Prefix, [StringComparison]::OrdinalIgnoreCase) -or
        $Child.StartsWith($Prefix + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Quote-Argument([string]$Value) {
    if ($Value -match '[\p{Cc}"]') { throw 'Unsafe process argument.' }
    return '"' + [regex]::Replace($Value, '(\\+)$', '$1$1') + '"'
}

function Protect-SdkOutput([string]$Value) {
    return [regex]::Replace($Value, '(?i)(\bSteam[ \t]{0,8}ID[ \t]{0,8}[:=][ \t]{0,8})[0-9]{1,20}(?![0-9])', '${1}[REDACTED]')
}

function Save-ChildLog($Task, [string]$Path) {
    if ($null -eq $Task) { return }
    if (!$Task.Wait(3000)) { throw 'Child output did not close.' }
    $Stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
    try {
        $Bytes = [Text.Encoding]::UTF8.GetBytes((Protect-SdkOutput $Task.GetAwaiter().GetResult()))
        $Stream.Write($Bytes, 0, $Bytes.Length)
    }
    finally { $Stream.Dispose() }
}

[UInt64]$ParsedItem = 0
if ($ItemId -cnotmatch '\A[1-9][0-9]{0,19}\z' -or
    ![UInt64]::TryParse($ItemId, [ref]$ParsedItem)) { throw 'Invalid Workshop item ID.' }
if ($PlanSHA -cnotmatch '\A[0-9a-f]{64}\z' -or $ReceiptSHA -cnotmatch '\A[0-9a-f]{64}\z') {
    throw 'Expected canonical lowercase SHA-256 values.'
}
$GameDirectory = Require-OrdinaryPath $QudRoot $true
$PlanFile = Require-OrdinaryPath $PlanPath $false
$NoteFile = Require-OrdinaryPath $ChangeNotePath $false
$AttemptDirectory = Require-OrdinaryPath $FixedStateRoot $true
if (!$AttemptDirectory.Equals($FixedStateRoot, [StringComparison]::Ordinal)) {
    throw 'The fixed registry root did not resolve to its compiled path.'
}
$EvidenceDirectory = Require-OrdinaryPath $EvidenceRoot $true
if ($AttemptDirectory -eq [IO.Path]::GetPathRoot($AttemptDirectory) -or
    $EvidenceDirectory -eq [IO.Path]::GetPathRoot($EvidenceDirectory) -or
    (Get-ChildItem -LiteralPath $EvidenceDirectory -Force | Measure-Object).Count -ne 0) {
    throw 'Evidence must be fresh and empty; state and evidence must not be drive roots.'
}
$PlanStream = [IO.File]::Open($PlanFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
try {
    if ($PlanStream.Length -le 0 -or $PlanStream.Length -gt 4194304) { throw 'Plan size is outside bounds.' }
    $PlanBytes = New-Object byte[] ([int]$PlanStream.Length)
    $Offset = 0
    while ($Offset -lt $PlanBytes.Length) {
        $Read = $PlanStream.Read($PlanBytes, $Offset, $PlanBytes.Length - $Offset)
        if ($Read -le 0) { throw 'Truncated plan.' }
        $Offset += $Read
    }
}
finally { $PlanStream.Dispose() }
$Hasher = [Security.Cryptography.SHA256]::Create()
try { $ActualSHA = [BitConverter]::ToString($Hasher.ComputeHash($PlanBytes)).Replace('-', '').ToLowerInvariant() }
finally { $Hasher.Dispose() }
if ($ActualSHA -cne $PlanSHA) { throw 'Plan SHA-256 mismatch.' }
$StrictUTF8 = New-Object Text.UTF8Encoding -ArgumentList $false, $true
$Plan = $StrictUTF8.GetString($PlanBytes) | ConvertFrom-Json
if ($Plan.contentPath -isnot [string] -or $Plan.receiptPath -isnot [string]) { throw 'Missing plan paths.' }
$PackageDirectory = Require-OrdinaryPath (Convert-PlanPath $Plan.contentPath) $true
$ReceiptFile = Require-OrdinaryPath (Convert-PlanPath $Plan.receiptPath) $false
foreach ($Protected in @($PackageDirectory, $AttemptDirectory, $GameDirectory, $PSScriptRoot,
    [IO.Path]::GetDirectoryName($PlanFile), [IO.Path]::GetDirectoryName($NoteFile), [IO.Path]::GetDirectoryName($ReceiptFile))) {
    if ((Contains-Directory $Protected $EvidenceDirectory) -or (Contains-Directory $EvidenceDirectory $Protected)) {
        throw 'Evidence must be separate from package, state, source and input directories.'
    }
}
$ManagedDirectory = Require-OrdinaryPath (Join-Path $GameDirectory 'CoQ_Data\Managed') $true
$NativeDirectory = Require-OrdinaryPath (Join-Path $GameDirectory 'CoQ_Data\Plugins\x86_64') $true
$null = Require-OrdinaryPath (Join-Path $ManagedDirectory 'com.rlabrecque.steamworks.net.dll') $false
$null = Require-OrdinaryPath (Join-Path $NativeDirectory 'steam_api64.dll') $false
$LockEntry = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'WorkshopSteam\sdk.lock.json') -Force
if ($LockEntry.PSIsContainer) { throw 'SDK lock must be an ordinary file.' }
$Cursor = $LockEntry
while ($null -ne $Cursor) {
    if (($Cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked SDK lock refused.' }
    if ($Cursor -is [IO.DirectoryInfo]) { $Cursor = $Cursor.Parent } else { $Cursor = $Cursor.Directory }
}
$LockStream = $LockEntry.OpenRead()
try {
    if ($LockStream.Length -le 0 -or $LockStream.Length -gt 16384) { throw 'SDK lock size is outside bounds.' }
    $LockReader = New-Object IO.BinaryReader -ArgumentList $LockStream
    try { $LockBytes = $LockReader.ReadBytes([int]$LockStream.Length) } finally { $LockReader.Dispose() }
}
finally { $LockStream.Dispose() }
$Lock = $StrictUTF8.GetString($LockBytes) | ConvertFrom-Json
if (($Lock.schema -isnot [int] -and $Lock.schema -isnot [long]) -or $Lock.schema -ne 1 -or
    $Lock.managedFile -cne 'com.rlabrecque.steamworks.net.dll' -or $Lock.nativeFile -cne 'steam_api64.dll' -or
    $Lock.managedSHA256 -isnot [string] -or $Lock.managedSHA256 -cnotmatch '\A[0-9a-f]{64}\z' -or
    $Lock.nativeSHA256 -isnot [string] -or $Lock.nativeSHA256 -cnotmatch '\A[0-9a-f]{64}\z') {
    throw 'Invalid SDK lock schema or identity.'
}
$ManagedHash = (Get-FileHash -LiteralPath (Join-Path $ManagedDirectory $Lock.managedFile) -Algorithm SHA256).Hash.ToLowerInvariant()
$NativeHash = (Get-FileHash -LiteralPath (Join-Path $NativeDirectory $Lock.nativeFile) -Algorithm SHA256).Hash.ToLowerInvariant()
if ($ManagedHash -cne $Lock.managedSHA256 -or $NativeHash -cne $Lock.nativeSHA256) { throw 'Installed SDK differs from lock.' }
$LockCopy = [IO.File]::Open((Join-Path $EvidenceDirectory 'sdk.lock.json'),
    [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try { $LockCopy.Write($LockBytes, 0, $LockBytes.Length) } finally { $LockCopy.Dispose() }
$Invocation = Get-ReleaseInvocation -Submit:$Submit -Inspect:$Inspect -Verify:$Verify -Finalize:$Finalize `
    -PlanFile $PlanFile -PlanSHA $PlanSHA -ItemId $ItemId -NoteFile $NoteFile `
    -FixedStateRoot $FixedStateRoot -ReceiptSHA $ReceiptSHA
$Project = Join-Path (Join-Path $PSScriptRoot 'WorkshopSteam') $Invocation.Project
$OutputDirectory = Join-Path $EvidenceDirectory 'out'
$IntermediateDirectory = (Join-Path $EvidenceDirectory 'obj') + '/'
$Dotnet = (Get-Command dotnet.exe -CommandType Application).Source
# Refs #151: a shared-compilation VBCSCompiler.dll keepalive process survives this build and
# is the only surviving descendant after the launcher's own process exits, so the runner's
# Start-Process -Wait on the outer launcher invocation (release.yml) never returns. Disable
# shared compilation and MSBuild node reuse for this one build so nothing outlives it.
$PriorUseMsBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$PriorDisableNodeReuse = $env:MSBUILDDISABLENODEREUSE
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
try {
    & $Dotnet build $Project --nologo --configuration Release --output $OutputDirectory `
        "-p:QudManaged=$ManagedDirectory" "-p:BaseIntermediateOutputPath=$IntermediateDirectory" `
        "-p:UseSharedCompilation=false" "-nodeReuse:false" `
        --ignore-failed-sources *> (Join-Path $EvidenceDirectory 'build.log')
} finally {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = $PriorUseMsBuildServer
    $env:MSBUILDDISABLENODEREUSE = $PriorDisableNodeReuse
}
if ($LASTEXITCODE -ne 0) { throw 'Upload helper compilation failed; retained build.log.' }

# Publisher argv retains the fixed-root literal; delivery uses its compiled root directly.
$Arguments = @((Join-Path $OutputDirectory $Invocation.Assembly)) + $Invocation.Arguments
$Start = New-Object Diagnostics.ProcessStartInfo
$Start.FileName = $Dotnet
$Start.Arguments = ($Arguments | ForEach-Object { Quote-Argument $_ }) -join ' '
$Start.WorkingDirectory = $EvidenceDirectory
$Start.UseShellExecute = $false
$Start.CreateNoWindow = $true
$Start.RedirectStandardOutput = $true
$Start.RedirectStandardError = $true
$Start.EnvironmentVariables['SteamAppId'] = '333640'
$Start.EnvironmentVariables['SteamGameId'] = '333640'
$Start.EnvironmentVariables['PATH'] = $NativeDirectory + ';' + $Start.EnvironmentVariables['PATH']
$Process = New-Object Diagnostics.Process
$Process.StartInfo = $Start
$Started = $false
$OutputTask = $null
$ErrorTask = $null
$UploadExitCode = 9
try {
    $Started = $Process.Start()
    if (!$Started) { throw 'Upload helper did not start.' }
    $OutputTask = $Process.StandardOutput.ReadToEndAsync()
    $ErrorTask = $Process.StandardError.ReadToEndAsync()
    if (!$Process.WaitForExit(210000)) { throw 'Upload helper timed out; reconcile retained attempt state before any retry.' }
    if (!$OutputTask.Wait(3000) -or !$ErrorTask.Wait(3000)) { throw 'Upload helper output did not close.' }
    $UploadExitCode = $Process.ExitCode
    Write-Output (Protect-SdkOutput $OutputTask.GetAwaiter().GetResult())
}
finally {
    try {
        # Only this held child belongs to the launcher; never stop Steam or Qud.
        if ($Started -and !$Process.HasExited) {
            $Process.Kill()
            if (!$Process.WaitForExit(5000)) { throw 'Owned upload helper did not stop within five seconds.' }
        }
    }
    finally {
        try {
            try { Save-ChildLog $OutputTask (Join-Path $EvidenceDirectory 'upload.stdout') }
            finally { Save-ChildLog $ErrorTask (Join-Path $EvidenceDirectory 'upload.stderr') }
        }
        finally { $Process.Dispose() }
    }
}
exit $UploadExitCode
