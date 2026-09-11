# Execute the launcher's actual JSON helpers on Windows PowerShell 5.1 and PowerShell 7.
# No game, Steam connection, save, or live scenario profile is touched.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$launcher = Join-Path $PSScriptRoot 'run-scenario.ps1'
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $launcher, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'The scenario launcher does not parse.' }
$names = @('ConvertTo-TafRecordValue', 'Read-TafRunRecord', 'Write-TafRunRecord',
    'Assert-TafScenarioOutputVacant')
foreach ($name in $names) {
    $definitions = @($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq $name
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected one actual launcher helper: $name" }
    Invoke-Expression $definitions[0].Extent.Text
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('taf-run-record-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
try {
    $path = Join-Path $fixture 'run-record.json'
    $json = '{"role":"save-session","ownership":{"pid":4242,"startTicks":"639247202896713857","receiptSha256":"abc"},"inheritedVerified":false,"optional":null,"values":[null,false,2,"three"],"empty":[]}'
    [IO.File]::WriteAllText($path, $json)
    foreach ($kind in @('log', 'journal')) {
        Assert-TafScenarioOutputVacant -Path $path -Kind $kind -RecordingStop
        $refused = $false
        try { Assert-TafScenarioOutputVacant -Path $path -Kind $kind }
        catch { $refused = $_.Exception.Message -like "scenario $kind already exists:*" }
        if (-not $refused) { throw 'A new launch accepted existing output.' }
        Assert-TafScenarioOutputVacant -Path (Join-Path $fixture 'absent') -Kind $kind
    }
    if ([IO.File]::ReadAllText($path) -cne $json) { throw 'Output guard modified retained evidence.' }
    $record = Read-TafRunRecord -Path $path
    if ($record -isnot [hashtable] -or $record.ownership -isnot [hashtable]) {
        throw 'Top-level and nested ownership must be dictionaries.'
    }
    if (-not $record.ContainsKey('optional') -or $null -ne $record.optional -or
        $record.inheritedVerified -isnot [bool] -or $record.inheritedVerified -ne $false -or
        $record.ownership.pid -ne 4242 -or $record.ownership.startTicks -cne '639247202896713857' -or
        $record.values.Count -ne 4 -or $null -ne $record.values[0] -or
        $record.empty.Count -ne 0) { throw 'JSON types or values changed while reading.' }
    $record.launchId = 'pid-4242-20260911T100000Z'
    $record.stoppedUtc = '2026-09-11T10:05:00Z'
    $record.exitProvenance = 'owned-process-ended-exit-unobserved'
    Write-TafRunRecord -Path $path -Record $record
    $loaded = Read-TafRunRecord -Path $path
    if ($loaded.launchId -cne $record.launchId -or $loaded.ownership.pid -ne 4242 -or
        $loaded.ContainsKey('exitCode') -or $loaded.values.Count -ne 4 -or
        $loaded.ownership.startTicks -cne '639247202896713857') {
        throw 'Writing and rereading changed the launch/stop record.'
    }
    foreach ($invalid in @('null', '42', '[]', '{broken')) {
        [IO.File]::WriteAllText($path, $invalid)
        $refused = $false
        try { $null = Read-TafRunRecord -Path $path } catch { $refused = $true }
        if (-not $refused) { throw "Invalid record accepted: $invalid" }
    }
    Write-Output "SCENARIO RUN RECORD HELPERS CLEAN (PowerShell $($PSVersionTable.PSVersion))"
} finally {
    # Only this invocation's GUID-named fixture, never a scenario root.
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
