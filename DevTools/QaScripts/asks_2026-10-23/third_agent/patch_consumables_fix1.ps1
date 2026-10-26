# Test fixes after the first run of the consumables: two older vendor tests still said the dealer's list has 17 entries, and the floor part of the scenario must clear the effects of the docks first (the invisible can drunk there still hid the player).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Dry) { Copy-Item (Join-Path $here 'consumables\NetDevConsumables.cs') ($A + 'Editor\NetDev\NetDevConsumables.cs') -Force; Write-Output 'copied NetDevConsumables.cs' }
Open-Src ($A + 'Editor\NetDev\NetDevDemoTest.cs')
Rep 'c.Length == 17 && Vendor.PewIndex == 7' 'c.Length == 17 + ConsumableRules.Count && Vendor.PewIndex == 7'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevGunsTest.cs')
Rep 'bool ammoRows = cat.Length == 17;' 'bool ammoRows = cat.Length == 17 + ConsumableRules.Count;'
Save-Src
"consumables fix 1 patched"
