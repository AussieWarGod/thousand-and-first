# Windows integration regression, not a source-only proof or a Qud acceptance test.
# Creates two retained disposable profiles and one harmless, windowless sleeping executable.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This regression requires Windows PowerShell and real Windows process metadata.'
}
. (Join-Path $PSScriptRoot 'scenario-process.ps1')
$script:ProbeCases = 0
$script:ProbePassed = 0
$script:HeldProbes = [Collections.Generic.List[Diagnostics.Process]]::new()
$script:ProbeSentinels = @{}
$script:ProbeDirectories = [Collections.Generic.List[string]]::new()
$encoding = [Text.UTF8Encoding]::new($false, $true)

function Assert-Probe {
    param([bool]$Condition, [string]$Detail)
    if (-not $Condition) { throw $Detail }
}

function Invoke-ProbeCase {
    param([string]$Id, [scriptblock]$Body)
    $script:ProbeCases++
    try { & $Body }
    catch { Write-Host "FAIL $Id : $($_.Exception.Message)"; throw }
    $script:ProbePassed++
    Write-Host "PASS $Id"
}

function Assert-ProbeRefused {
    param([scriptblock]$Action, [string]$Message = '')
    $refusal = $null
    try { & $Action }
    catch { $refusal = $_.Exception.Message }
    Assert-Probe ($null -ne $refusal) 'The real operation unexpectedly accepted the fixture.'
    if ($Message) {
        Assert-Probe ($refusal.Contains($Message)) "Unexpected refusal: $refusal"
    }
}

function New-ProbeProfile {
    do { $path = 'C:\taf-scenario.' + [guid]::NewGuid().ToString('N') }
    while ((Test-Path -LiteralPath $path) -or (Test-Path -LiteralPath ($path + '.seal')))
    foreach ($directory in @($path, "$path\Save", "$path\Local", "$path\Synced", "$path.seal")) {
        [void][IO.Directory]::CreateDirectory($directory)
        $script:ProbeDirectories.Add($directory)
        $sentinel = Join-Path $directory 'fixture-sentinel.txt'
        $bytes = $encoding.GetBytes('probe fixture only: ' + [guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllBytes($sentinel, $bytes)
        $script:ProbeSentinels[$sentinel] = [Convert]::ToBase64String($bytes)
    }
    # These are sentinel bytes, deliberately not a seal capable of authorizing a game launch.
    foreach ($name in @('profile.sha256', 'request.txt')) {
        $sentinel = Join-Path "$path.seal" $name
        $bytes = $encoding.GetBytes('synthetic process regression; no game authority')
        [IO.File]::WriteAllBytes($sentinel, $bytes)
        $script:ProbeSentinels[$sentinel] = [Convert]::ToBase64String($bytes)
    }
    return $path
}

function Assert-ProbeFilesPreserved {
    foreach ($directory in $script:ProbeDirectories) {
        Assert-Probe (Test-Path -LiteralPath $directory -PathType Container) "Directory lost: $directory"
    }
    foreach ($path in $script:ProbeSentinels.Keys) {
        Assert-Probe (Test-Path -LiteralPath $path -PathType Leaf) "Sentinel lost: $path"
        Assert-Probe ([Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ceq
            $script:ProbeSentinels[$path]) "Sentinel changed: $path"
    }
}

function Assert-BothProbesLive {
    Assert-Probe (-not $owned.HasExited -and -not $decoy.HasExited) 'Owned process or decoy was stopped.'
    Assert-ProbeFilesPreserved
}

function Assert-OwnedProbeLookup {
    $observed = Get-TafOwnedScenarioProcess -Root $rootA -Game $game
    try {
        Assert-Probe ($null -ne $observed -and $observed.Id -eq $owned.Id) 'Owned lookup lost exact PID.'
    }
    finally { if ($null -ne $observed) { $observed.Dispose() } }
}

function Assert-ReceiptActionsRefused {
    param([string]$Root)
    Assert-ProbeRefused {
        $observed = Get-TafOwnedScenarioProcess -Root $Root -Game $game
        if ($null -ne $observed) { $observed.Dispose() }
    }
    Assert-BothProbesLive
    Assert-ProbeRefused { Stop-TafOwnedScenarioProcess -Root $Root -Game $game }
    Assert-BothProbesLive
}

function Invoke-ReceiptTamper {
    param([string]$Id, [scriptblock]$Change)
    Invoke-ProbeCase $Id {
        $record = $authText | ConvertFrom-Json
        $changed = & $Change $record
        try {
            [IO.File]::WriteAllText($receiptA, $changed, $encoding)
            Assert-ReceiptActionsRefused -Root $rootA
        }
        finally { [IO.File]::WriteAllBytes($receiptA, $authBytes) }
        Assert-OwnedProbeLookup
        Assert-BothProbesLive
    }
}

$rootA = $null
$rootB = $null
try {
    $rootA = New-ProbeProfile
    $rootB = New-ProbeProfile
    $probeName = 'TafScenarioProbe' + [guid]::NewGuid().ToString('N')
    $binaryDirectory = Join-Path $rootA 'probe binaries'
    [void][IO.Directory]::CreateDirectory($binaryDirectory)
    $game = Join-Path $binaryDirectory ($probeName + '.exe')
    $program = 'internal static class ' + $probeName +
        ' { private static void Main() { System.Threading.Thread.Sleep(600000); } }'
    Add-Type -TypeDefinition $program -Language CSharp -OutputAssembly $game -OutputType WindowsApplication

    # Only this test executable is launched. Hold its actual handles immediately for final cleanup.
    $owned = Start-TafOwnedScenarioProcess -Root $rootA -Game $game
    $script:HeldProbes.Add($owned)
    $null = $owned.Handle
    $expectedB = [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ExpectedArguments($rootB, $game)
    $decoy = Start-Process -FilePath $game -ArgumentList $expectedB[1..11] -PassThru
    $script:HeldProbes.Add($decoy)
    $null = $decoy.Handle
    $receiptA = Get-TafScenarioReceiptPath -Root $rootA
    $authBytes = [IO.File]::ReadAllBytes($receiptA)
    $authText = $encoding.GetString($authBytes)

    Invoke-ProbeCase 'real-owned-and-same-name-decoy-identities' {
        Assert-BothProbesLive
        Assert-Probe ($owned.Id -ne $decoy.Id -and
            $owned.ProcessName -ceq $decoy.ProcessName) 'Fixture must contain distinct same-name processes.'
        Assert-Probe ($owned.MainWindowHandle -eq [IntPtr]::Zero -and
            $decoy.MainWindowHandle -eq [IntPtr]::Zero) 'The probe must have no native window.'
        foreach ($pair in @(@($rootA, $owned), @($rootB, $decoy))) {
            $identity = Get-TafScenarioIdentity -Process $pair[1]
            $expected = [ThousandAndFirst.Tools.ScenarioProcessPolicy]::ExpectedArguments($pair[0], $game)
            Assert-Probe ($identity.Arguments.Count -eq 12) 'Native argv must have exactly twelve entries.'
            for ($i = 0; $i -lt $expected.Length; $i++) {
                Assert-Probe ($identity.Arguments[$i] -ceq $expected[$i]) "Native argv differs at index $i."
            }
            Assert-Probe ([ThousandAndFirst.Tools.ScenarioProcessPolicy]::ValidIdentity(
                $pair[0], $game, $identity)) 'Observed native identity must satisfy the shared policy.'
        }
        Assert-OwnedProbeLookup
    }
    Invoke-ProbeCase 'idle-refuses-without-stopping-either-process' {
        Assert-ProbeRefused { Assert-TafScenarioIdle -Game $game } 'refusing launch without stopping it'
        Assert-BothProbesLive
    }
    Invoke-ReceiptTamper 'receipt-pid-is-live-decoy' {
        param($record); $record.pid = $decoy.Id; $record | ConvertTo-Json -Compress
    }
    Invoke-ReceiptTamper 'receipt-start-ticks-mismatch' {
        param($record)
        $record.startTicks = ([long]::Parse($record.startTicks) + 1L).ToString(
            [Globalization.CultureInfo]::InvariantCulture)
        $record | ConvertTo-Json -Compress
    }
    Invoke-ReceiptTamper 'receipt-foreign-root' {
        param($record); $record.root = $rootB; $record | ConvertTo-Json -Compress
    }
    Invoke-ReceiptTamper 'receipt-foreign-executable' {
        param($record); $record.executable = "$rootB\Foreign.exe"; $record | ConvertTo-Json -Compress
    }
    foreach ($index in @(0, 2, 4, 6, 8)) {
        Invoke-ReceiptTamper "receipt-foreign-argument-path-$index" {
            param($record)
            $record.arguments[$index] = if ($index -eq 0) { "$rootB\Foreign.exe" } else { $expectedB[$index] }
            $record | ConvertTo-Json -Compress
        }
    }
    Invoke-ReceiptTamper 'receipt-extra-field' {
        param($record); $authText.Substring(0, $authText.Length - 1) + ',"extra":true}'
    }
    Invoke-ReceiptTamper 'receipt-duplicate-field' {
        param($record); $authText.Substring(0, $authText.Length - 1) + ',"pid":' + $owned.Id + '}'
    }
    Invoke-ProbeCase 'foreign-caller-root-cannot-borrow-owned-receipt' {
        $receiptB = Get-TafScenarioReceiptPath -Root $rootB
        [IO.File]::WriteAllBytes($receiptB, $authBytes)
        Assert-ReceiptActionsRefused -Root $rootB
        Assert-OwnedProbeLookup
    }
    Invoke-ProbeCase 'existing-receipt-refuses-before-executable-resolution' {
        Assert-ProbeRefused {
            Start-TafOwnedScenarioProcess -Root $rootA -Game "$rootA\must-not-launch.exe"
        } 'Ownership receipt already exists:'
        Assert-BothProbesLive
        Assert-Probe ([Convert]::ToBase64String([IO.File]::ReadAllBytes($receiptA)) -ceq
            [Convert]::ToBase64String($authBytes)) 'Refused launch changed the ownership receipt.'
    }
    Invoke-ProbeCase 'stop-owned-retains-decoy-and-all-sentinels' {
        $result = Stop-TafOwnedScenarioProcess -Root $rootA -Game $game
        Assert-Probe ($result.StartsWith("STOPPED pid=$($owned.Id) root=$rootA;")) 'Stop did not report exact ownership.'
        Assert-Probe ($owned.HasExited -and -not $decoy.HasExited) 'Stop must exit only the owned process.'
        Assert-ProbeFilesPreserved
        Assert-Probe ([Convert]::ToBase64String([IO.File]::ReadAllBytes($receiptA)) -ceq
            [Convert]::ToBase64String($authBytes)) 'Stop changed the retained ownership receipt.'
    }
    Invoke-ProbeCase 'repeated-stop-reports-already-exited' {
        # Release the original exited process handle before a PID lookup of the now-absent process.
        $owned.Dispose()
        [void]$script:HeldProbes.Remove($owned)
        $result = Stop-TafOwnedScenarioProcess -Root $rootA -Game $game
        Assert-Probe ($result -ceq "ALREADY_EXITED root=$rootA") 'Repeated stop must be a proved no-op.'
        Assert-Probe (-not $decoy.HasExited) 'Repeated stop touched the decoy.'
        Assert-ProbeFilesPreserved
    }
}
finally {
    $cleanupFailures = [Collections.Generic.List[string]]::new()
    foreach ($probe in $script:HeldProbes) {
        try {
            # Only direct fixture launch objects enter this list; no PID/name rediscovery or tree kill.
            if (-not $probe.HasExited) {
                $probe.Kill()
                if (-not $probe.WaitForExit(10000)) { throw 'Held fixture process did not exit.' }
            }
        }
        catch { $cleanupFailures.Add($_.Exception.Message) }
        finally { $probe.Dispose() }
    }
    Write-Host "Fixture profiles and seals retained: $rootA ; $rootB"
    if ($cleanupFailures.Count -ne 0) { throw ($cleanupFailures -join '; ') }
}
Assert-ProbeFilesPreserved
Assert-Probe ($script:ProbeCases -eq 17 -and $script:ProbePassed -eq 17) 'Native case inventory is incomplete.'
Write-Output "scenario-process-native cases=$script:ProbeCases passed=$script:ProbePassed failed=0; real-probe=true; game-launched=false; fixtures-retained=true"
