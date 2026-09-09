[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Mode,
    [Parameter(Mandatory = $true)][string]$Plan
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$session = $null
$result = $null
$failure = $null
$cleanupFailure = $null
try {
    if ($Mode -cnotin @('Inspect', 'Copy', 'Slots')) { throw 'unsupported_mode' }
    if ($env:OS -cne 'Windows_NT') { throw 'native_windows_required' }
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false, $true)
    # Caller timeout may leave a partial destination; it proves no continuing custody after kill.
    # No game launch/stop. This process must be separately bounded by its caller.
    Add-Type -Path (Join-Path $PSScriptRoot 'UpgradeProfileTrust.cs')
    $session = New-Object ThousandAndFirst.Tools.UpgradeProfileTrust($Plan)
    # Compact, fixed-order shape makes duplicate/unknown keys impossible before JSON parsing.
    # Host writes json.dumps(..., separators=(',', ':')); selected=[] except Slots.
    $stringToken = '"(?:[^"\\\x00-\x1f]|\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4}))*"'
    $integerToken = '(?:0|[1-9][0-9]*)'
    $strings = '\[(?:' + $stringToken + '(?:,' + $stringToken + ')*)?\]'
    $row = '\{"path":' + $stringToken + ',"size":' + $integerToken + ',"sha256":' + $stringToken + '\}'
    $rows = '\[(?:' + $row + '(?:,' + $row + ')*)?\]'
    $planPattern = '\A\{"schema":' + $stringToken + ',"source":' + $stringToken + ',"destination":(?:null|' + $stringToken + '),"roots":' + $strings + ',"files":' + $rows + ',"directories":' + $strings + ',"selected":' + $strings + '\}\z'
    if (-not [regex]::IsMatch($session.PlanText, $planPattern,
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant, [TimeSpan]::FromSeconds(2))) {
        throw 'unsupported_plan_shape'
    }
    $parsed = ConvertFrom-Json -InputObject $session.PlanText
    [string[]]$roots = @($parsed.roots)
    [string[]]$directories = @($parsed.directories)
    [string[]]$selected = @($parsed.selected)
    [object[]]$rowsRead = @($parsed.files)
    if ($rowsRead.Length -gt 16384 -or $directories.Length -gt 16384) { throw 'plan_entry_budget' }
    $paths = New-Object 'System.Collections.Generic.List[string]'
    $sizes = New-Object 'System.Collections.Generic.List[long]'
    $hashes = New-Object 'System.Collections.Generic.List[string]'
    foreach ($rowRead in $rowsRead) {
        $paths.Add([string]$rowRead.path)
        $sizes.Add([long]$rowRead.size)
        $hashes.Add([string]$rowRead.sha256)
    }
    # PowerShell marshals a bare $null into a .NET [string] parameter as "", so Inspect (which
    # REQUIRES a null destination) was refused as destination_invalid. [NullString]::Value is the
    # documented way to pass a genuine null string across that boundary.
    $destination = [NullString]::Value
    if ($null -ne $parsed.destination) { $destination = [string]$parsed.destination }
    $result = $session.Run($Mode, [string]$parsed.schema, [string]$parsed.source, $destination,
        $roots, $paths.ToArray(), $sizes.ToArray(), $hashes.ToArray(), $directories, $selected)
} catch {
    $failure = $_.Exception.GetType().FullName
} finally {
    if ($null -ne $session) {
        try { $session.Dispose() } catch { $cleanupFailure = $_.Exception.GetType().FullName }
    }
}
if ($null -ne $failure -or $null -ne $cleanupFailure) {
    [ordered]@{
        schema = 'taf-upgrade-copy-proof-v1'; status = 'refused'; mode = $Mode
        errorType = $failure; cleanupErrorType = $cleanupFailure
        partialDestinationMayExist = ($Mode -ceq 'Copy' -or $Mode -ceq 'Slots')
        partialDestinationDeleted = $false; continuousIdleVerified = $false
        gracefulQuitVerified = $false; sourceVersionVerified = $false; saveCompatibilityVerified = $false
    } | ConvertTo-Json -Depth 6 -Compress
    exit 2
}
$result.cleanupComplete = $true
$result | ConvertTo-Json -Depth 6 -Compress
exit 0
