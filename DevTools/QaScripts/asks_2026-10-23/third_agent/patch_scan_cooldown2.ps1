# patch_scan_cooldown.ps1 left one older pure check on the gap between pulses at 2.4 s (NetDevEtrTest.cs): it is the 2 s cooldown now.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevEtrTest.cs')
Rep 'then another every 2.4 s"' 'then another every 2 s (the cooldown)"'
Rep '!LootScanRules.NextPulse(true, 2.39f) && LootScanRules.NextPulse(true, 2.4f)' '!LootScanRules.NextPulse(true, 1.99f) && LootScanRules.NextPulse(true, 2f)'
Save-Src
"old scan gap check patched"
