# Round 9, batch G03: the GunSheet scenario runs in the run scene, like the other picture scenarios (the harness must not wait for the menu).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'
Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep 'mode != "weaponsheet" && mode != "meleelook" && mode != "ui"' 'mode != "weaponsheet" && mode != "meleelook" && mode != "gunsheet" && mode != "ui"'
Save-Src
