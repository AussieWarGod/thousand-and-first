# Compile the exact runtime set defined by Tools/stage.sh.
# Usage: powershell -File build.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$distro = if ($env:WSL_DISTRO_NAME) { $env:WSL_DISTRO_NAME } else { "Ubuntu" }
$wslRoot = $null

# A script launched from WSL normally arrives as one of these UNC forms. Resolve it
# without asking another shell to reinterpret backslashes or spaces.
if ($root -match '^\\\\wsl(?:\.localhost)?\\([^\\]+)(\\.*)$') {
	$distro = $Matches[1]
	$wslRoot = $Matches[2].Replace('\', '/')
}
else {
	$converted = & wsl.exe -d $distro -- wslpath -u -a -- $root
	if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($converted)) {
		throw "Could not resolve the repository path inside WSL: $root"
	}
	$wslRoot = $converted.Trim()
}

& wsl.exe -d $distro -- bash "$wslRoot/Tools/gate.sh"
$result = $LASTEXITCODE
if ($result -eq 0) {
	Write-Host "COMPILE CLEAN (canonical staged runtime set)"
}
else {
	Write-Host "COMPILE FAILED ($result)"
}
exit $result
