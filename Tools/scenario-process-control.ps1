[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('idle', 'stop')][string]$Mode,
    [string]$Root,
    [Parameter(Mandatory = $true)][string]$Game
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scenario-process.ps1')
if ($Mode -eq 'idle') {
    Assert-TafScenarioIdle -Game $Game
    Write-Output 'No existing game process; launch permitted.'
} else {
    Stop-TafOwnedScenarioProcess -Root $Root -Game $Game
}
