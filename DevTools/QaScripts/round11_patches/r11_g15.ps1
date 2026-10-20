# Round 11, batch G15: the briefcases are meant to be in the vendor's truck now (they are no longer among the old props the dock scenario looks for), and a group of drums keeps the room round its first drum clear (it was reduced too far).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevDockLook.cs'
Rep ', "chair_std", "chair_simple", "briefcase" };' ', "chair_std", "chair_simple" };          // (the briefcases are back in the vendor''s truck: the owner liked them)'
Save-Src

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep 'if (!Free(x, z, 0.7f)) continue;' 'if (!Free(x, z, 0.95f)) continue;'
Save-Src
