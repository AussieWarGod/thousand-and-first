# SDK-free routing and early-refusal fixtures. Never execute an operational launcher mode.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$LauncherPath,
    [Parameter(Mandatory = $true)][string]$ExpectedSHA
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$script:Cases = 0; $script:Failures = 0; $script:SideEffects = 0
function Check([bool]$Value, [string]$Reason) { if (!$Value) { throw $Reason } }
function Equal([string]$Actual, [string]$Expected) {
    Check ([StringComparer]::Ordinal.Equals($Actual, $Expected)) 'Exact text mismatch.'
}
function Case([string]$Name, [scriptblock]$Body) {
    $script:Cases++
    try { & $Body; Write-Output ('PASS ' + $Name) }
    catch { $script:Failures++; Write-Output ('FAIL ' + $Name + ' (' + $_.Exception.GetType().Name + ')') }
}
function Refuses([scriptblock]$Body, [string]$Prefix) {
    $caught = $null
    try { $null = & $Body } catch { $caught = $_.Exception }
    Check ($null -ne $caught -and $caught.Message.StartsWith($Prefix, [StringComparison]::Ordinal)) 'Expected exact refusal.'
}
function Read-Source {
    $stream = [IO.File]::Open($LauncherPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        Check ($stream.Length -gt 0 -and $stream.Length -le 1048576) 'Source size refused.'
        $reader = [IO.BinaryReader]::new($stream)
        try {
            $bytes = $reader.ReadBytes([int]$stream.Length)
            Check ($bytes.Length -eq $stream.Length) 'Truncated source.'
            return ,$bytes
        } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}
function Check-SourceHash([byte[]]$Bytes) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { Equal ([BitConverter]::ToString($hash.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()) $ExpectedSHA }
    finally { $hash.Dispose() }
}
Check ($ExpectedSHA -cmatch '\A[0-9a-f]{64}\z') 'Expected lowercase SHA256.'
$LauncherPath = [IO.Path]::GetFullPath($LauncherPath)
$bytes = Read-Source; Check-SourceHash $bytes
$source = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
Check (@($errors).Count -eq 0 -and $null -ne $ast.EndBlock) 'Launcher parser errors.'
$top = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.FunctionDefinitionAst] })
$definitions = @()
foreach ($name in @('Get-ReleaseInvocation', 'Quote-Argument')) {
    $found = @($top | Where-Object { $_.Name -ieq $name })
    Check ($found.Count -eq 1 -and $found[0].Name -ceq $name -and !$found[0].IsFilter -and !$found[0].IsWorkflow) 'Helper definition refused.'
    $definitions += $found[0].Extent.Text
}
# Evaluate only the two exact top-level definitions, never the launcher's operational statements.
. ([scriptblock]::Create(($definitions -join [Environment]::NewLine)))
$fixed = 'C:\taf-workshop-state.dRBivM'
$inputs = @{ PlanFile = 'C:\candidate space\plan''s & $(literal).json'; PlanSHA = ('a' * 64)
    ItemId = '3796495680'; NoteFile = 'C:\notes space\change''s `tick & note.txt'
    FixedStateRoot = $fixed; ReceiptSHA = ('b' * 64) }
$switches = @('Submit', 'Inspect', 'Verify', 'Finalize')
$modes = @(
    @{ Switch = ''; Mode = 'check'; Delivery = $false },
    @{ Switch = 'Submit'; Mode = 'submit'; Delivery = $false },
    @{ Switch = 'Inspect'; Mode = 'inspect'; Delivery = $false },
    @{ Switch = 'Verify'; Mode = 'verify'; Delivery = $true },
    @{ Switch = 'Finalize'; Mode = 'finalize'; Delivery = $true }
)
foreach ($mode in $modes) {
    Case ('route ' + $mode.Mode) {
        $bound = $inputs.Clone()
        if ($mode.Switch) { $bound[$mode.Switch] = $true }
        $route = Get-ReleaseInvocation @bound
        Equal $route.Mode $mode.Mode
        if ($mode.Delivery) {
            Equal $route.Project 'WorkshopDelivery.csproj'; Equal $route.Assembly 'TafWorkshopDelivery.dll'
            $expected = @($mode.Mode, $inputs.PlanFile, $inputs.PlanSHA, $inputs.ItemId, $inputs.ReceiptSHA)
        } else {
            Equal $route.Project 'WorkshopUpload.csproj'; Equal $route.Assembly 'TafWorkshopUpload.dll'
            $expected = @($mode.Mode, $inputs.PlanFile, $inputs.PlanSHA, $inputs.ItemId, $inputs.NoteFile, $fixed, $inputs.ReceiptSHA)
        }
        Check ($route.Arguments.Count -eq $expected.Count) 'CLI argument count mismatch.'
        for ($i = 0; $i -lt $expected.Count; $i++) { Equal $route.Arguments[$i] $expected[$i] }
        $quoted = @($route.Arguments | ForEach-Object { Quote-Argument $_ })
        Equal $quoted[1] '"C:\candidate space\plan''s & $(literal).json"'
        Equal $quoted[2] ('"' + ('a' * 64) + '"'); Equal $quoted[3] '"3796495680"'
        Equal $quoted[$quoted.Count - 1] ('"' + ('b' * 64) + '"')
        if (!$mode.Delivery) { Equal $quoted[4] '"C:\notes space\change''s `tick & note.txt"'; Equal $quoted[5] ('"' + $fixed + '"') }
    }
}
$conflicts = @()
for ($mask = 1; $mask -lt 16; $mask++) {
    $flags = @{}
    for ($i = 0; $i -lt $switches.Count; $i++) { if (($mask -band (1 -shl $i)) -ne 0) { $flags[$switches[$i]] = $true } }
    if ($flags.Count -lt 2) { continue }
    $conflicts += ,$flags
    Case ('helper conflict mask ' + $mask) {
        $bound = $inputs.Clone(); foreach ($key in $flags.Keys) { $bound[$key] = $true }
        Refuses { Get-ReleaseInvocation @bound } 'Choose only one of -Submit, -Inspect, -Verify or -Finalize.'
    }
}
Check ($conflicts.Count -eq 11) 'Contradictory switch coverage incomplete.'
Case 'explicit false switches retain check' {
    $bound = $inputs.Clone(); foreach ($key in $switches) { $bound[$key] = $false }
    Equal (Get-ReleaseInvocation @bound).Mode 'check'
}
Case 'quoting preserves terminal backslashes' { Equal (Quote-Argument 'C:\space dir\') '"C:\space dir\\"' }
Case 'quotes and controls refuse before process argument construction' {
    foreach ($value in @('C:\bad"path', "C:\bad`npath", "C:\bad`rpath", "C:\bad`tpath", ('bad' + [char]0))) {
        Refuses { Quote-Argument $value } 'Unsafe process argument.'
    }
}
Case 'required shared parameters and state-root alias remain' {
    $mandatory = @('QudRoot', 'PlanPath', 'PlanSHA', 'ItemId', 'ChangeNotePath', 'ReceiptSHA', 'EvidenceRoot')
    foreach ($name in $mandatory) {
        $parameter = @($ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -ceq $name })
        Check ($parameter.Count -eq 1 -and $parameter[0].Extent.Text -match 'Mandatory\s*=\s*\$true') 'Shared required parameter changed.'
    }
    Check ($source.Contains("[Alias('AttemptRoot')][string]`$StateRoot = '$fixed'")) 'Fixed-root alias/default changed.'
    Check ($source.Contains('$Arguments = @((Join-Path $OutputDirectory $Invocation.Assembly)) + $Invocation.Arguments')) 'Actual launch does not consume tested arguments.'
    Check ($source.Contains("`$Project = Join-Path (Join-Path `$PSScriptRoot 'WorkshopSteam') `$Invocation.Project")) 'Actual build does not consume tested project.'
}

# Actual wrapper refusals below must occur before these first operational command boundaries.
function New-Object { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Get-Item { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Get-ChildItem { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Get-FileHash { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Get-Command { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Add-Type { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
function Start-Process { $script:SideEffects++; throw 'OPERATIONAL_CALL_FORBIDDEN' }
$wrapper = @{ QudRoot = 'C:\not-opened\Qud'; PlanPath = $inputs.PlanFile; PlanSHA = $inputs.PlanSHA
    ItemId = $inputs.ItemId; ChangeNotePath = $inputs.NoteFile; ReceiptSHA = $inputs.ReceiptSHA
    EvidenceRoot = 'C:\not-opened\evidence'; StateRoot = $fixed }
foreach ($mode in $modes) {
    foreach ($root in @('C:\unapproved-state', 'c:\taf-workshop-state.dRBivM', 'C:\taf-workshop-state.dRBivM\')) {
        Case ('wrapper root refusal ' + $mode.Mode + ' ' + $root) {
            $bound = $wrapper.Clone(); $bound.StateRoot = $root
            if ($mode.Switch) { $bound[$mode.Switch] = $true }
            Refuses { & $LauncherPath @bound } 'Only the fixed registry root '
            Check ($script:SideEffects -eq 0) 'Root refusal reached operational work.'
        }
    }
}
Case 'AttemptRoot alias cannot bypass fixed root' {
    $bound = $wrapper.Clone(); $bound.Remove('StateRoot'); $bound.AttemptRoot = 'C:\unapproved-state'
    Refuses { & $LauncherPath @bound } 'Only the fixed registry root '
    Check ($script:SideEffects -eq 0) 'Alias refusal reached operational work.'
}
$index = 0
foreach ($flags in $conflicts) {
    $index++
    Case ('wrapper switch conflict ' + $index) {
        $bound = $wrapper.Clone(); foreach ($key in $flags.Keys) { $bound[$key] = $true }
        Refuses { & $LauncherPath @bound } 'Choose only one of -Submit, -Inspect, -Verify or -Finalize.'
        Check ($script:SideEffects -eq 0) 'Conflict refusal reached operational work.'
    }
}
Check-SourceHash (Read-Source)
Write-Output ('Release launcher fixtures: passed=' + ($script:Cases - $script:Failures) + ' failed=' + $script:Failures + ' cases=' + $script:Cases + '; no build/SDK/native execution.')
if ($script:Cases -ne 47 -or $script:Failures -ne 0 -or $script:SideEffects -ne 0) { exit 1 }
exit 0
