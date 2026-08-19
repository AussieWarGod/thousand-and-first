$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$xmlFiles = Get-ChildItem $root -File -Filter *.xml

foreach ($file in $xmlFiles) {
    [xml](Get-Content -Raw $file.FullName) | Out-Null
}

[xml]$blueprints = Get-Content -Raw (Join-Path $root "ObjectBlueprints.xml")
$post = $blueprints.SelectSingleNode('/objects/object[@Name="r_KingdomChargingPost"]')
if ($null -eq $post) {
    throw "Missing r_KingdomChargingPost blueprint."
}

foreach ($part in @("UniversalCharger", "r_KingdomHandCrank", "Container", "Inventory")) {
    if ($null -eq $post.SelectSingleNode("part[@Name='$part']")) {
        throw "Charging post is missing required part $part."
    }
}

$charger = $post.SelectSingleNode('part[@Name="UniversalCharger"]')
if ([int]$charger.ChargeRate -ne 150) {
    throw "Charging post UniversalCharger rate must match the 150-charge hand crank."
}
if ($null -ne $post.SelectSingleNode('part[@Name="Capacitor"]')) {
    throw "Charging post must not use Capacitor as a charge source."
}

Write-Host "XML VALID: $($xmlFiles.Count) files; charging post structure valid"
