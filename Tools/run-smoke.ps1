param(
    [Parameter(Mandatory = $true)]
    [string]$Root,
    [string]$Game = '',
    [switch]$Resume,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Game)) {
    $configuredGameRoot = $env:TAF_QUD_ROOT
    if ([string]::IsNullOrWhiteSpace($configuredGameRoot)) {
        $configuredGameRoot = 'F:\SteamLibrary\steamapps\common\Caves of Qud'
    }
    $Game = Join-Path $configuredGameRoot 'CoQ.exe'
}

if (-not ('TafSmokeNative.FileLinks' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TafSmokeNative
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    public static class FileLinks
    {
        private const uint ShareAll = 0x00000007;
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;
        private const uint OpenReparsePoint = 0x00200000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file, out ByHandleFileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(
            SafeFileHandle file, StringBuilder path, uint pathLength, uint flags);

        public static uint Count(string path)
        {
            using (SafeFileHandle file = CreateFileW(path, 0, ShareAll, IntPtr.Zero,
                OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero))
            {
                if (file.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect file links: " + path);
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(file, out information))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot inspect file links: " + path);
                return information.NumberOfLinks;
            }
        }

        public static string Resolve(string path)
        {
            using (SafeFileHandle file = CreateFileW(path, 0, ShareAll, IntPtr.Zero,
                OpenExisting, BackupSemantics, IntPtr.Zero))
            {
                if (file.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot resolve final file path: " + path);
                uint capacity = 512;
                while (capacity <= 32768)
                {
                    StringBuilder resolved = new StringBuilder((int)capacity);
                    uint length = GetFinalPathNameByHandleW(file, resolved, capacity, 0);
                    if (length == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot resolve final file path: " + path);
                    if (length < capacity)
                    {
                        string value = resolved.ToString();
                        if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                            return @"\\" + value.Substring(8);
                        if (value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                            return value.Substring(4);
                        return value;
                    }
                    capacity = length + 1;
                }
                throw new InvalidOperationException("Resolved file path exceeds validation bound: " + path);
            }
        }
    }
}
'@
}

function Get-SafeTreeItems {
    param([Parameter(Mandatory = $true)][string]$TreeRoot)

    $pending = [Collections.Generic.Stack[string]]::new()
    $items = [Collections.Generic.List[System.IO.FileSystemInfo]]::new()
    $pending.Push($TreeRoot)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($child in @(Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop)) {
            if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing reparse point inside smoke profile: $($child.FullName)"
            }
            if (-not $child.PSIsContainer) {
                $links = [TafSmokeNative.FileLinks]::Count($child.FullName)
                if ($links -ne 1) {
                    throw "Refusing hard-linked file inside smoke profile: $($child.FullName)"
                }
            }
            [void]$items.Add($child)
            if ($child.PSIsContainer) {
                $pending.Push($child.FullName)
            }
        }
    }
    return $items.ToArray()
}

function Assert-StagedMod {
    param(
        [Parameter(Mandatory = $true)][string]$StageRoot,
        [Parameter(Mandatory = $true)][string]$SealPath
    )

    $sealDirectory = Split-Path -Parent $SealPath
    if (-not (Test-Path -LiteralPath $sealDirectory -PathType Container)) {
        throw "Trusted stage manifest directory is missing: $sealDirectory"
    }
    $sealDirectoryItem = Get-Item -LiteralPath $sealDirectory -Force
    if (($sealDirectoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Trusted stage manifest directory is a reparse point: $sealDirectory"
    }
    $sealEntries = @(Get-ChildItem -LiteralPath $sealDirectory -Force)
    if ($sealEntries.Count -ne 1 -or $sealEntries[0].Name -cne 'stage.sha256' -or
        $sealEntries[0].PSIsContainer) {
        throw "Trusted stage manifest directory has unexpected entries: $sealDirectory"
    }
    if (-not (Test-Path -LiteralPath $SealPath -PathType Leaf)) {
        throw "Trusted stage manifest is missing: $SealPath"
    }
    $sealItem = Get-Item -LiteralPath $SealPath -Force
    if (($sealItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Trusted stage manifest is a reparse point: $SealPath"
    }
    if ([TafSmokeNative.FileLinks]::Count($sealItem.FullName) -ne 1) {
        throw "Trusted stage manifest is hard-linked: $SealPath"
    }
    $expected = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($line in @(Get-Content -LiteralPath $SealPath)) {
        if ($line -notmatch '^([0-9a-f]{64})  (.+)$') {
            throw "Trusted stage manifest has an invalid row: $SealPath"
        }
        $hash = $Matches[1]
        $relative = $Matches[2]
        $segments = @($relative.Split('/'))
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains('\') -or
            $relative.Contains(':') -or
            $relative.StartsWith('/') -or $relative.EndsWith('/') -or
            $segments -contains '' -or $segments -contains '..' -or $segments -contains '.') {
            throw "Trusted stage manifest has an unsafe path: $relative"
        }
        if ($expected.ContainsKey($relative)) {
            throw "Trusted stage manifest repeats a path: $relative"
        }
        $expected.Add($relative, $hash)
    }
    if ($expected.Count -eq 0) {
        throw "Trusted stage manifest is empty: $SealPath"
    }

    $prefix = $StageRoot.TrimEnd([char[]]@('\', '/')) + '\'
    $actual = [Collections.Generic.Dictionary[string,System.IO.FileInfo]]::new([StringComparer]::Ordinal)
    foreach ($item in @(Get-SafeTreeItems -TreeRoot $StageRoot)) {
        if ($item.PSIsContainer) {
            continue
        }
        if (-not $item.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Staged mod path escaped its root: $($item.FullName)"
        }
        $relative = $item.FullName.Substring($prefix.Length).Replace('\', '/')
        if ($actual.ContainsKey($relative)) {
            throw "Staged mod repeats a path: $relative"
        }
        $actual.Add($relative, [System.IO.FileInfo]$item)
    }

    foreach ($relative in @($expected.Keys | Sort-Object)) {
        if (-not $actual.ContainsKey($relative)) {
            throw "Staged mod inventory differs from trusted manifest: missing $relative"
        }
    }
    foreach ($relative in @($actual.Keys | Sort-Object)) {
        if (-not $expected.ContainsKey($relative)) {
            throw "Staged mod inventory differs from trusted manifest: unexpected $relative"
        }
        $actualHash = (Get-FileHash -LiteralPath $actual[$relative].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -cne $expected[$relative]) {
            throw "Staged mod hash differs from trusted manifest: $relative"
        }
    }
}

function Assert-SmokeLogVacant {
    param([Parameter(Mandatory = $true)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if ($null -eq $item) {
        return
    }
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
        (-not $item.PSIsContainer -and [TafSmokeNative.FileLinks]::Count($item.FullName) -ne 1)) {
        throw "Refusing aliased smoke log path: $Path"
    }
    throw "Smoke log file must not preexist: $Path"
}

function Assert-GzipFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $file = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $magic = New-Object byte[] 3
        if ($file.Read($magic, 0, $magic.Length) -ne $magic.Length -or
            $magic[0] -ne 0x1f -or $magic[1] -ne 0x8b -or $magic[2] -ne 0x08) {
            throw "Resume save gzip header is invalid: $Path"
        }
        $file.Position = 0
        $gzip = New-Object IO.Compression.GZipStream($file, [IO.Compression.CompressionMode]::Decompress, $true)
        try {
            $buffer = New-Object byte[] 65536
            $header = New-Object byte[] 68
            $headerLength = 0
            [long]$expanded = 0
            while (($read = $gzip.Read($buffer, 0, $buffer.Length)) -gt 0) {
                if ($headerLength -lt $header.Length) {
                    $copy = [Math]::Min($read, $header.Length - $headerLength)
                    [Array]::Copy($buffer, 0, $header, $headerLength, $copy)
                    $headerLength += $copy
                }
                $expanded += $read
                if ($expanded -gt 1073741824) {
                    throw "Resume save gzip exceeds the validation bound: $Path"
                }
            }
            if ($headerLength -ne $header.Length) {
                throw "Resume save gzip lacks a Qud serialization header: $Path"
            }
            $fileVersion = [BitConverter]::ToInt32($header, 0)
            $gameObject = [BitConverter]::ToInt64($header, 4)
            $gameObjectReference = [BitConverter]::ToInt64($header, 12)
            $eventRegistry = [BitConverter]::ToInt64($header, 20)
            $tokenized = [BitConverter]::ToInt64($header, 28)
            $object = [BitConverter]::ToInt64($header, 36)
            $type = [BitConverter]::ToInt64($header, 44)
            $string = [BitConverter]::ToInt64($header, 52)
            $gameObjectCount = [BitConverter]::ToInt32($header, 60)
            $eventRegistryCount = [BitConverter]::ToInt32($header, 64)
            if ($fileVersion -ne 408 -or $gameObjectCount -lt 0 -or $eventRegistryCount -lt 0 -or
                $gameObject -lt 68 -or $eventRegistry -le $gameObject -or
                $gameObjectReference -lt $eventRegistry -or $object -le $gameObjectReference -or
                $tokenized -le $object -or $type -le $tokenized -or $string -le $type -or
                $string -ge $expanded) {
                throw "Resume save gzip has an invalid Qud serialization header: $Path"
            }
            return $fileVersion
        }
        finally {
            $gzip.Dispose()
        }
    }
    catch {
        if ($_.Exception.Message.StartsWith('Resume save gzip', [StringComparison]::Ordinal)) {
            throw
        }
        throw "Resume save gzip is corrupt: $Path"
    }
    finally {
        $file.Dispose()
    }
}

function Assert-SqliteFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $file = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($file.Length -lt 512) {
            throw "Resume cache database is too short: $Path"
        }
        $header = New-Object byte[] 101
        if ($file.Read($header, 0, $header.Length) -ne $header.Length -or
            [Text.Encoding]::ASCII.GetString($header, 0, 16) -cne "SQLite format 3`0") {
            throw "Resume cache database header is invalid: $Path"
        }
        $pageSizeCode = ([int]$header[16] -shl 8) -bor [int]$header[17]
        $pageSize = if ($pageSizeCode -eq 1) { 65536 } else { $pageSizeCode }
        $powerOfTwo = $pageSize -gt 0 -and (($pageSize -band ($pageSize - 1)) -eq 0)
        $pageCount = ([int64]$header[28] -shl 24) -bor ([int64]$header[29] -shl 16) -bor
            ([int64]$header[30] -shl 8) -bor [int64]$header[31]
        $schemaFormat = ([int64]$header[44] -shl 24) -bor ([int64]$header[45] -shl 16) -bor
            ([int64]$header[46] -shl 8) -bor [int64]$header[47]
        $textEncoding = ([int64]$header[56] -shl 24) -bor ([int64]$header[57] -shl 16) -bor
            ([int64]$header[58] -shl 8) -bor [int64]$header[59]
        $invalidFileFormatVersions = @(@($header[18], $header[19]) |
            Where-Object { $_ -notin @(1, 2) })
        if (-not $powerOfTwo -or $pageSize -lt 512 -or $pageSize -gt 65536) {
            throw "Resume cache database structure is invalid: $Path"
        }
        $actualPages = [int64]($file.Length / $pageSize)
        if ($file.Length % $pageSize -ne 0 -or
            $invalidFileFormatVersions.Count -ne 0 -or
            $header[20] -ge $pageSize -or $header[21] -ne 64 -or
            $header[22] -ne 32 -or $header[23] -ne 32 -or
            $pageCount -lt 1 -or $pageCount -gt $actualPages -or
            $schemaFormat -lt 1 -or $schemaFormat -gt 4 -or
            $textEncoding -lt 1 -or $textEncoding -gt 3 -or
            $header[100] -notin @(2, 5, 10, 13)) {
            throw "Resume cache database structure is invalid: $Path"
        }
    }
    finally {
        $file.Dispose()
    }
}

function Test-JsonInteger {
    param($Value)
    return ($Value -is [int] -or $Value -is [long])
}

function Assert-PrimaryJson {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$DirectoryId
    )

    try {
        $saveInfo = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Resume Primary.json is invalid JSON: $Path"
    }
    $required = @(
        'InfoVersion', 'SaveVersion', 'GameVersion', 'ID', 'Name', 'Level', 'GenoSubType',
        'GameMode', 'CharIcon', 'FColor', 'DColor', 'Location', 'InGameTime', 'Turn',
        'SaveTime', 'ModsEnabled'
    )
    $propertyNames = @($saveInfo.PSObject.Properties | ForEach-Object Name)
    foreach ($name in $required) {
        if ($propertyNames -cnotcontains $name) {
            throw "Resume Primary.json is missing required field ${name}: $Path"
        }
    }
    foreach ($name in @('GameVersion', 'ID', 'Name', 'GameMode', 'CharIcon', 'Location', 'InGameTime', 'SaveTime')) {
        $value = $saveInfo.PSObject.Properties[$name].Value
        if (-not ($value -is [string]) -or [string]::IsNullOrWhiteSpace($value)) {
            throw "Resume Primary.json has invalid field ${name}: $Path"
        }
    }
    if (-not ($saveInfo.GenoSubType -is [string])) {
        throw "Resume Primary.json has invalid field GenoSubType: $Path"
    }
    foreach ($name in @('InfoVersion', 'SaveVersion', 'Level', 'FColor', 'DColor', 'Turn')) {
        if (-not (Test-JsonInteger $saveInfo.PSObject.Properties[$name].Value)) {
            throw "Resume Primary.json has non-integer field ${name}: $Path"
        }
    }
    if ($saveInfo.InfoVersion -ne 1 -or $saveInfo.SaveVersion -ne 408 -or
        $saveInfo.Level -lt 1 -or $saveInfo.Turn -lt 0 -or
        $saveInfo.FColor -lt 0 -or $saveInfo.FColor -gt 65535 -or
        $saveInfo.DColor -lt 0 -or $saveInfo.DColor -gt 65535) {
        throw "Resume Primary.json has out-of-range numeric fields: $Path"
    }
    if ($saveInfo.GameVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Resume Primary.json has invalid field GameVersion: $Path"
    }
    $parsedId = [guid]::Empty
    if (-not [guid]::TryParseExact($saveInfo.ID, 'D', [ref]$parsedId) -or
        $saveInfo.ID -cne $DirectoryId) {
        throw "Resume Primary.json ID does not match its save directory: $Path"
    }
    if (-not ($saveInfo.ModsEnabled -is [System.Array])) {
        throw "Resume Primary.json ModsEnabled is not an array: $Path"
    }
    $allowedMods = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$allowedMods.Add('r_ThousandAndFirst')
    [void]$allowedMods.Add('FreeholdGames_DLC_PetsPack1')
    $seenMods = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($modId in @($saveInfo.ModsEnabled)) {
        if (-not ($modId -is [string]) -or [string]::IsNullOrWhiteSpace($modId)) {
            throw "Resume Primary.json contains an invalid mod ID: $Path"
        }
        if (-not $allowedMods.Contains($modId)) {
            throw "Resume save enables unrelated mod ${modId}: $Path"
        }
        if (-not $seenMods.Add($modId)) {
            throw "Resume save repeats mod ${modId}: $Path"
        }
    }
    if (-not $seenMods.Contains('r_ThousandAndFirst')) {
        throw "Resume save does not enable r_ThousandAndFirst: $Path"
    }
    return $saveInfo.GameVersion
}

function Assert-SmokeProfile {
    param(
        [Parameter(Mandatory = $true)][string]$ProfileRoot,
        [Parameter(Mandatory = $true)][bool]$IsResume
    )

    if (-not (Test-Path -LiteralPath $ProfileRoot -PathType Container)) {
        throw "Isolated smoke root not found: $ProfileRoot"
    }
    $rootItem = Get-Item -LiteralPath $ProfileRoot -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing reparse-point smoke root: $ProfileRoot"
    }
    $rootEntries = @(Get-ChildItem -LiteralPath $ProfileRoot -Force |
        Sort-Object Name | ForEach-Object Name)
    if (($rootEntries -join "`n") -ne (@('Local', 'Save', 'Synced') -join "`n")) {
        throw "Smoke root is not an exact prepare-smoke profile: $ProfileRoot"
    }
    $null = @(Get-SafeTreeItems -TreeRoot $ProfileRoot)

    $localPath = Join-Path $ProfileRoot 'Local'
    $savePath = Join-Path $ProfileRoot 'Save'
    $syncedPath = Join-Path $ProfileRoot 'Synced'
    foreach ($directory in @($localPath, $savePath, $syncedPath)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            throw "Smoke profile is missing directory: $directory"
        }
    }

    $localMods = Join-Path $localPath 'Mods'
    $stageRoot = Join-Path $localMods 'ThousandAndFirst'
    $modEntries = @(Get-ChildItem -LiteralPath $localMods -Force)
    if ($modEntries.Count -ne 1 -or -not $modEntries[0].PSIsContainer -or
        $modEntries[0].Name -cne 'ThousandAndFirst') {
        throw "Smoke mod directory must contain only ThousandAndFirst: $localMods"
    }
    $stageSeal = Join-Path ($ProfileRoot + '.seal') 'stage.sha256'
    Assert-StagedMod -StageRoot $stageRoot -SealPath $stageSeal

    $syncedSaves = Join-Path $syncedPath 'Saves'
    if (-not (Test-Path -LiteralPath $syncedSaves -PathType Container)) {
        throw "Smoke synced saves directory is missing: $syncedSaves"
    }
    $saveGameVersion = $null
    if (-not $IsResume) {
        $localEntries = @(Get-ChildItem -LiteralPath $localPath -Force |
            Sort-Object Name | ForEach-Object Name)
        if (($localEntries -join "`n") -ne (@('Mods', 'ModSettings.json', 'PlayerOptions.json') -join "`n")) {
            throw "Smoke Local directory is not fresh: $localPath"
        }
        if (@(Get-ChildItem -LiteralPath $savePath -Force).Count -ne 0) {
            throw "Smoke Save directory is not empty: $savePath"
        }
        $syncedEntries = @(Get-ChildItem -LiteralPath $syncedPath -Force |
            Sort-Object Name | ForEach-Object Name)
        if (($syncedEntries -join "`n") -ne 'Saves' -or
            @(Get-ChildItem -LiteralPath $syncedSaves -Force).Count -ne 0) {
            throw "Smoke Synced directory is not fresh: $syncedPath"
        }
    }
    else {
        foreach ($shadowName in @('Saves', 'Mods')) {
            $shadowPath = Join-Path $savePath $shadowName
            if (Test-Path -LiteralPath $shadowPath) {
                if (-not (Test-Path -LiteralPath $shadowPath -PathType Container) -or
                    @(Get-ChildItem -LiteralPath $shadowPath -Force).Count -ne 0) {
                    if ($shadowName -ceq 'Saves') {
                        throw "Resume profile contains shadow save entries: $shadowPath"
                    }
                    throw "Resume profile contains save-local mods: $shadowPath"
                }
            }
        }
        $syncedEntries = @(Get-ChildItem -LiteralPath $syncedPath -Force |
            Sort-Object Name | ForEach-Object Name)
        if (($syncedEntries -join "`n") -ne 'Saves') {
            throw "Resume Synced directory contains an unexpected entry: $syncedPath"
        }
        $saveEntries = @(Get-ChildItem -LiteralPath $syncedSaves -Force)
        if ($saveEntries.Count -ne 1 -or -not $saveEntries[0].PSIsContainer) {
            throw "Resume needs exactly one isolated save directory: $syncedSaves"
        }
        $saveItem = $saveEntries[0]
        $saveChildren = @(Get-ChildItem -LiteralPath $saveItem.FullName -Force | Sort-Object Name)
        $saveFiles = @($saveChildren | ForEach-Object Name)
        $requiredSaveFiles = @('Cache.db', 'Primary.json', 'Primary.sav.gz')
        $allowedSaveFiles = @('Cache.db', 'Cache.db-shm', 'Cache.db-wal', 'Primary.json',
            'Primary.sav.gz', 'Primary.sav.gz.bak')
        if (@($requiredSaveFiles | Where-Object { $saveFiles -cnotcontains $_ }).Count -ne 0 -or
            @($saveFiles | Where-Object { $allowedSaveFiles -cnotcontains $_ }).Count -ne 0 -or
            @($saveChildren | Where-Object PSIsContainer).Count -ne 0) {
            throw "Resume save has partial or aliased entries: $($saveItem.FullName)"
        }
        $primaryJson = Join-Path $saveItem.FullName 'Primary.json'
        $primarySave = Join-Path $saveItem.FullName 'Primary.sav.gz'
        $cacheDatabase = Join-Path $saveItem.FullName 'Cache.db'
        $null = Assert-GzipFile -Path $primarySave
        $primaryBackup = Join-Path $saveItem.FullName 'Primary.sav.gz.bak'
        if (Test-Path -LiteralPath $primaryBackup) {
            $null = Assert-GzipFile -Path $primaryBackup
        }
        Assert-SqliteFile -Path $cacheDatabase
        $saveGameVersion = Assert-PrimaryJson -Path $primaryJson -DirectoryId $saveItem.Name
    }

    $manifest = Join-Path $stageRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        throw "Isolated TAF manifest not found: $manifest"
    }
    try {
        $manifestJson = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    }
    catch {
        throw "Isolated manifest is invalid JSON: $manifest"
    }
    $manifestIds = @($manifestJson.PSObject.Properties | Where-Object { $_.Name -ceq 'id' })
    if ($manifestIds.Count -ne 1 -or $manifestIds[0].Value -isnot [string] -or
        $manifestIds[0].Value -cne 'r_ThousandAndFirst') {
        throw "Isolated manifest has the wrong mod identity: $manifest"
    }
    return [pscustomobject]@{
        LocalPath = $localPath
        SavePath = $savePath
        SyncedPath = $syncedPath
        SaveGameVersion = $saveGameVersion
    }
}

$rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]@('\', '/'))
if ($rootPath -cnotmatch '^C:\\taf-smoke\.[A-Za-z0-9]+$') {
    throw "Refusing smoke root outside a fresh C:\taf-smoke.<id> directory: $rootPath"
}
if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
    throw "Isolated smoke root not found: $rootPath"
}
$initialRootItem = Get-Item -LiteralPath $rootPath -Force
if (($initialRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Refusing reparse-point smoke root: $rootPath"
}
$logPath = Join-Path $rootPath 'Player.log'
Assert-SmokeLogVacant -Path $logPath
$profile = Assert-SmokeProfile -ProfileRoot $rootPath -IsResume ([bool]$Resume)
$gamePath = [IO.Path]::GetFullPath($Game)
if (-not (Test-Path -LiteralPath $gamePath -PathType Leaf)) {
    throw "Caves of Qud executable not found: $gamePath"
}
$dataDirectory = [IO.Path]::GetFileNameWithoutExtension($gamePath) + '_Data'
$assemblyPath = Join-Path (Split-Path -Parent $gamePath) "$dataDirectory\Managed\Assembly-CSharp.dll"
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Caves of Qud managed assembly not found: $assemblyPath"
}
try {
    $installedGameVersion = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath).Version.ToString()
}
catch {
    throw "Caves of Qud managed assembly identity is invalid: $assemblyPath"
}
if ($Resume -and $profile.SaveGameVersion -cne $installedGameVersion) {
    throw "Resume Primary.json GameVersion does not match installed Caves of Qud: $($profile.SaveGameVersion) != $installedGameVersion"
}
if (-not $ValidateOnly -and (Get-Process -Name 'CoQ', 'CavesOfQud' -ErrorAction SilentlyContinue)) {
    throw 'Caves of Qud is already running; refusing to mix smoke profiles.'
}

# Rerun every profile, stage, save, and identity check immediately before process creation.
$profile = Assert-SmokeProfile -ProfileRoot $rootPath -IsResume ([bool]$Resume)
Assert-SmokeLogVacant -Path $logPath
if ($Resume -and $profile.SaveGameVersion -cne $installedGameVersion) {
    throw "Resume Primary.json GameVersion does not match installed Caves of Qud: $($profile.SaveGameVersion) != $installedGameVersion"
}
if ($ValidateOnly) {
    Write-Output "SMOKE VALIDATION CLEAN: $(if ($Resume) { 'resume' } else { 'fresh' })"
    return
}

$localPath = $profile.LocalPath
$savePath = $profile.SavePath
$syncedPath = $profile.SyncedPath

$arguments = @(
    '-savepath', $savePath,
    '-sharedpath', $localPath,
    '-syncedpath', $syncedPath,
    '-logFile', $logPath,
    'NOMETRICS',
    'STEAM:NO',
    'GALAXY:NO'
)
$process = Start-Process -FilePath $gamePath -ArgumentList $arguments -PassThru
Start-Sleep -Milliseconds 750
$process.Refresh()
if ($process.HasExited) {
    throw "Caves of Qud exited during smoke-launch liveness check: $($process.ExitCode)"
}
$running = @(Get-Process -Name 'CoQ', 'CavesOfQud' -ErrorAction SilentlyContinue)
if ($running.Count -ne 1 -or $running[0].Id -ne $process.Id) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw 'Caves of Qud process identity changed during smoke launch; the isolated process was stopped.'
}

Write-Output "SMOKE STARTED: PID $($process.Id)"
Write-Output "Profile: $rootPath"
Write-Output "Mode: $(if ($Resume) { 'resume' } else { 'fresh' })"
Write-Output "Log: $logPath"
Write-Output "After quitting, move Player.log outside the profile and run Tools/check-player-log.sh on it before -Resume."
