[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Game
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scenario-process.ps1')
# Reading the exact receipt must succeed even if its PID is absent. A reused PID refuses.
$process = Get-TafOwnedScenarioProcess -Root $Root -Game $Game
if ($null -ne $process) {
    $process.Dispose()
    throw 'The source scenario process is still alive; no save will be copied.'
}
Assert-TafScenarioIdle -Game $Game
Write-Output "VERIFIED_STOPPED root=$Root; original receipt and artifacts retained"
