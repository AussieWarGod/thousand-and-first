param(
    [Parameter(Mandatory = $true)][string]$QudRoot,
    [Parameter(Mandatory = $true)][string]$ItemId,
    [Parameter(Mandatory = $true)][string]$EvidenceRoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require-OrdinaryPath([string]$Path, [bool]$Directory) {
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
    return $Entry.FullName
}

[UInt64]$ParsedItem = 0
if ($ItemId -notmatch '^[1-9][0-9]{0,19}$' -or
    ![UInt64]::TryParse($ItemId, [ref]$ParsedItem)) { throw 'Invalid Workshop item ID.' }
$GameDirectory = Require-OrdinaryPath $QudRoot $true
$EvidenceDirectory = Require-OrdinaryPath $EvidenceRoot $true
if ($EvidenceDirectory -eq [IO.Path]::GetPathRoot($EvidenceDirectory) -or
    (Get-ChildItem -LiteralPath $EvidenceDirectory -Force | Measure-Object).Count -ne 0) {
    throw 'Evidence directory must be a fresh empty directory, not a drive root.'
}
$ManagedDirectory = Require-OrdinaryPath (Join-Path $GameDirectory 'CoQ_Data\Managed') $true
$NativeDirectory = Require-OrdinaryPath (Join-Path $GameDirectory 'CoQ_Data\Plugins\x86_64') $true
$null = Require-OrdinaryPath (Join-Path $ManagedDirectory 'com.rlabrecque.steamworks.net.dll') $false
$null = Require-OrdinaryPath (Join-Path $NativeDirectory 'steam_api64.dll') $false
$Project = Join-Path $PSScriptRoot 'WorkshopSteam\WorkshopSteam.csproj'
$OutputDirectory = Join-Path $EvidenceDirectory 'out'
$IntermediateDirectory = (Join-Path $EvidenceDirectory 'obj') + '/'
$Dotnet = (Get-Command dotnet.exe -CommandType Application).Source
& $Dotnet build $Project --nologo --configuration Release --output $OutputDirectory `
    "-p:QudManaged=$ManagedDirectory" "-p:BaseIntermediateOutputPath=$IntermediateDirectory" `
    --ignore-failed-sources *> (Join-Path $EvidenceDirectory 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Probe compilation failed; retained build.log.' }

# Steam context is process-local. Never log credentials, start Steam, or answer authentication prompts.
$Start = New-Object Diagnostics.ProcessStartInfo
$Start.FileName = $Dotnet
$Start.Arguments = '"' + (Join-Path $OutputDirectory 'TafWorkshopSteam.dll') + '" ' + $ItemId
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
$ProbeExitCode = 9
try {
    $Started = $Process.Start()
    if (!$Started) { throw 'Probe did not start.' }
    $OutputTask = $Process.StandardOutput.ReadToEndAsync()
    $ErrorTask = $Process.StandardError.ReadToEndAsync()
    if (!$Process.WaitForExit(45000)) { throw 'Probe timed out.' }
    if (!$OutputTask.Wait(3000) -or !$ErrorTask.Wait(3000)) { throw 'Probe output did not close.' }
    $ProbeExitCode = $Process.ExitCode
    Write-Output $OutputTask.GetAwaiter().GetResult()
}
finally {
    try {
        # Only this exact child was created here; never stop Steam or Qud.
        if ($Started -and !$Process.HasExited) {
            $Process.Kill()
            if (!$Process.WaitForExit(5000)) { Write-Warning 'Owned probe process did not stop within five seconds.' }
        }
    }
    finally {
        try {
            foreach ($Capture in @(@('probe.stdout', $OutputTask), @('probe.stderr', $ErrorTask))) {
                $Task = $Capture[1]
                if ($null -ne $Task -and $Task.Wait(3000)) {
                    $Stream = [IO.File]::Open((Join-Path $EvidenceDirectory $Capture[0]),
                        [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
                    try {
                        $Bytes = [Text.Encoding]::UTF8.GetBytes($Task.GetAwaiter().GetResult())
                        $Stream.Write($Bytes, 0, $Bytes.Length)
                    }
                    finally { $Stream.Dispose() }
                }
            }
        }
        finally { $Process.Dispose() }
    }
}
exit $ProbeExitCode
