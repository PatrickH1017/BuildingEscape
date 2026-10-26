# Notes: the owner's priority message (2026-10-23): the computer look-around, the PvP battlefield and the vending machines are the lowest priority. Patches the REAL project's DEVELOPER_NOTES.md.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'; Open-Src $SC
Rep '"Questions are more than welcome please ask if necessary".' '"Questions are more than welcome please ask if necessary". PRIORITY (the owner, 2026-10-23, later): "The computer look-around, PvP battlefield, and vending machines are the lowest priority" - T16, T7 and T6 + T23 come LAST, after T1, T5, T12 and T29.'
Save-Src
"priority noted"
