# Round 11, batch G18: R7's two checks about the basement's boxes (the PVP room's few boxes; every box rests on something and stands square) look for the dock's clean boxes now, and the cardboard stacks stand perfectly square (the owner, 2026-10-18: "placed nicely").
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src 'C:\bqm\Assets\Scripts\DockDressing.cs'
Rep 'float crooked = ((seed * (i + 5)) % 5) - 2f;' 'float crooked = 0f;'
Save-Src

Open-Src "$Ed\NetDevDockLook.cs"
Rep 'int dockLookFails;' @'
int dockLookFails;

        /// <summary>A cardboard box of the dock's clean props (a box of a stack or of a pallet block).</summary>
        static bool IsDockBox(Transform t) => t && t.name == "Box" && t.parent != null && (t.parent.name == "BoxBlock" || t.parent.name == "BoxStack");
'@
Save-Src

Open-Src "$Ed\NetDevR7.cs"
Rep 't.name == "box"' 'IsDockBox(t)' 3
Rep 't.name == "pallet"' 't.name == "Pallet"'
Save-Src
