# T6 (2026-10-23) made the four consumables the vending machines' (the dealer no longer sells the coffee mug, entry 0): the Sneak scenario's "buying from inside the truck" bought the mug. It buys the crowbar now (entry 4, always on the dealer's shelf).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevSneak.cs')
Rep '            buy.Invoke(vendor, new object[] { hm, 0 });' '            buy.Invoke(vendor, new object[] { hm, 4 });          // (the crowbar: the coffee mug, entry 0, is a vending machine''s since T6)'
Save-Src
"Sneak buys the crowbar"
