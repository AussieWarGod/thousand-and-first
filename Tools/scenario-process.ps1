# Shared Windows adapter. A receipt records launch ownership, not authority against a malicious
# account that can rewrite this script or start arbitrary processes. Never falls back to a name kill.
$ErrorActionPreference = 'Stop'
if (-not ('ThousandAndFirst.Tools.ScenarioProcessPolicy' -as [type])) {
    Add-Type -Path @((Join-Path $PSScriptRoot 'ScenarioProcessPolicy.cs'),
        (Join-Path $PSScriptRoot 'ScenarioProcessWindows.cs'))
}

function Assert-TafScenarioIdle {
    param([Parameter(Mandatory = $true)][string]$Game)
    $name = [IO.Path]::GetFileNameWithoutExtension($Game)
    $existing = @(Get-Process | Where-Object {
        [string]::Equals($_.ProcessName, $name, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($existing.Count -ne 0) {
        throw "Existing $name process detected; refusing launch without stopping it."
    }
}

function Get-TafScenarioIdentity {
    param([Parameter(Mandatory = $true)][Diagnostics.Process]$Process)
    # Cache the actual process handle before querying PID-based metadata. Keeping it open also
    # prevents a later Kill() from quietly acquiring a reused PID's process.
    $null = $Process.Handle
    $information = Get-CimInstance Win32_Process -Filter "ProcessId = $($Process.Id)"
    if ($null -eq $information -or $Process.HasExited) {
        throw 'Owned process exited before its identity could be observed.'
    }
    $identity = New-Object ThousandAndFirst.Tools.ScenarioProcessIdentity
    $identity.Pid = $Process.Id
    $identity.StartTicks = $Process.StartTime.ToUniversalTime().Ticks
    $identity.Executable = [ThousandAndFirst.Tools.ScenarioProcessWindows]::Executable(
        $Process.MainModule.FileName)
    $identity.Arguments = [ThousandAndFirst.Tools.ScenarioProcessWindows]::Arguments(
        $information.CommandLine)
    return $identity
}

function Get-TafScenarioReceiptPath {
    param([Parameter(Mandatory = $true)][string]$Root)
    if (-not [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ValidRoot($Root)) {
        throw "Invalid scenario ownership root: $Root"
    }
    return Join-Path $Root 'process-ownership.json'
}

function Write-TafScenarioReceipt {
    param([string]$Root, [ThousandAndFirst.Tools.ScenarioProcessIdentity]$Identity)
    $path = Get-TafScenarioReceiptPath -Root $Root
    $record = [pscustomobject][ordered]@{
        schema = [ThousandAndFirst.Tools.ScenarioProcessPolicy]::Schema
        root = $Root
        pid = $Identity.Pid
        startTicks = $Identity.StartTicks.ToString([Globalization.CultureInfo]::InvariantCulture)
        executable = $Identity.Executable
        arguments = $Identity.Arguments
    }
    $encoding = New-Object Text.UTF8Encoding($false, $true)
    $bytes = $encoding.GetBytes(($record | ConvertTo-Json -Compress))
    # CreateNew never overwrites a prior launch receipt. No reader can observe a partial write.
    $file = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write,
        [IO.FileShare]::None)
    try { $file.Write($bytes, 0, $bytes.Length); $file.Flush($true) }
    finally { $file.Dispose() }
}

function Read-TafScenarioReceipt {
    param([string]$Root, [string]$Game)
    $path = Get-TafScenarioReceiptPath -Root $Root
    $file = Get-Item -LiteralPath $path -Force
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
        $file.Length -lt 1 -or $file.Length -gt 16384) {
        throw 'Ownership receipt is not a bounded ordinary file.'
    }
    $encoding = New-Object Text.UTF8Encoding($false, $true)
    $text = [IO.File]::ReadAllText($path, $encoding)
    $record = $text | ConvertFrom-Json
    $names = @($record.PSObject.Properties.Name)
    if (($names -join ',') -cne 'schema,root,pid,startTicks,executable,arguments' -or
        $text -cne ($record | ConvertTo-Json -Compress) -or
        $record.schema -cne [ThousandAndFirst.Tools.ScenarioProcessPolicy]::Schema -or
        $record.root -isnot [string] -or
        -not [string]::Equals($record.root, $Root, [StringComparison]::OrdinalIgnoreCase) -or
        $record.pid -isnot [int] -or $record.startTicks -isnot [string] -or
        $record.executable -isnot [string] -or $record.arguments -isnot [Array]) {
        throw 'Ownership receipt is not an exact canonical launch record.'
    }
    # Canonical re-encoding also refuses duplicate fields, numeric aliases, and trailing JSON.
    $ticks = 0L
    if (-not [long]::TryParse($record.startTicks, [Globalization.NumberStyles]::None,
        [Globalization.CultureInfo]::InvariantCulture, [ref]$ticks) -or
        $ticks.ToString([Globalization.CultureInfo]::InvariantCulture) -cne $record.startTicks) {
        throw 'Ownership receipt has malformed start ticks.'
    }
    foreach ($argument in $record.arguments) {
        if ($argument -isnot [string]) { throw 'Ownership argument must be a string.' }
    }
    $identity = New-Object ThousandAndFirst.Tools.ScenarioProcessIdentity
    $identity.Pid = $record.pid
    $identity.StartTicks = $ticks
    $identity.Executable = $record.executable
    $identity.Arguments = [string[]]$record.arguments
    if (-not [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ValidIdentity($Root, $Game, $identity)) {
        throw 'Ownership receipt does not describe this exact scenario root and executable.'
    }
    return $identity
}

function Start-TafOwnedScenarioProcess {
    param([string]$Root, [string]$Game)
    $receipt = Get-TafScenarioReceiptPath -Root $Root
    if (Test-Path -LiteralPath $receipt) { throw "Ownership receipt already exists: $receipt" }
    $gamePath = [ThousandAndFirst.Tools.ScenarioProcessWindows]::Executable($Game)
    $arguments = [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ExpectedArguments($Root, $gamePath)
    if ($null -eq $arguments) { throw 'Scenario launch arguments are not canonical.' }
    $process = $null
    $published = $false
    try {
        $process = Start-Process -FilePath $gamePath -ArgumentList $arguments[1..11] -PassThru
        $null = $process.Handle
        $identity = Get-TafScenarioIdentity -Process $process
        if (-not [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ValidIdentity($Root, $gamePath, $identity)) {
            throw 'Newly launched process did not retain exact scenario arguments.'
        }
        Write-TafScenarioReceipt -Root $Root -Identity $identity
        $readback = Read-TafScenarioReceipt -Root $Root -Game $gamePath
        if ([ThousandAndFirst.Tools.ScenarioProcessPolicy]::Decide(
            $Root, $gamePath, $readback, $identity) -cne 'STOP_EXACT') {
            throw 'Ownership receipt did not read back exactly.'
        }
        $published = $true
        return $process
    }
    finally {
        if (-not $published -and $null -ne $process) {
            # This is the direct launch handle, not a PID rediscovered from an unproved receipt.
            if (-not $process.HasExited) {
                $process.Kill()
                if (-not $process.WaitForExit(10000)) { throw 'Failed launch process did not exit.' }
            }
            $process.Dispose()
        }
    }
}

function Get-TafOwnedScenarioProcess {
    param([string]$Root, [string]$Game)
    $gamePath = [ThousandAndFirst.Tools.ScenarioProcessWindows]::Executable($Game)
    $recorded = Read-TafScenarioReceipt -Root $Root -Game $gamePath
    $process = $null
    try { $process = [Diagnostics.Process]::GetProcessById($recorded.Pid) }
    catch [ArgumentException] {
        if ([ThousandAndFirst.Tools.ScenarioProcessPolicy]::Decide(
            $Root, $gamePath, $recorded, $null) -ceq 'ALREADY_EXITED') { return $null }
        throw
    }
    try {
        $live = Get-TafScenarioIdentity -Process $process
        if ([ThousandAndFirst.Tools.ScenarioProcessPolicy]::Decide(
            $Root, $gamePath, $recorded, $live) -cne 'STOP_EXACT') {
            throw 'Process ownership mismatch; no process will be stopped or captured.'
        }
        return $process
    }
    catch { $process.Dispose(); throw }
}

function Stop-TafOwnedScenarioProcess {
    param([string]$Root, [string]$Game)
    $process = Get-TafOwnedScenarioProcess -Root $Root -Game $Game
    if ($null -eq $process) { return "ALREADY_EXITED root=$Root" }
    try {
        $ownedId = $process.Id
        $process.Kill()
        if (-not $process.WaitForExit(10000)) { throw 'Owned scenario process did not exit.' }
        return "STOPPED pid=$ownedId root=$Root; profile and seal retained"
    }
    finally { $process.Dispose() }
}
