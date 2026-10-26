# T7: DockSeeds wanted at least 6 cardboard blocks in the docks; two of them stood in the PVP room (the owner, 2026-10-23: the room is a battlefield now, PvpBattlefieldRules), so the least is 4 while the battlefield is on. Needs patch_t7_battlefield.ps1.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevDockSeeds.cs')
Rep 'blocks >= 6 &&' 'blocks >= (PvpBattlefieldRules.On ? 4 : 6) /* two cardboard blocks of the PVP room made way for the battlefield (T7) */ &&'
Save-Src
"DockSeeds threshold patched"
