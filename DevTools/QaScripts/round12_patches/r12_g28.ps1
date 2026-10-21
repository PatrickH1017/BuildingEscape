# Round 12, batch G28 (after `t11`): the lane test found a box stack at x 18.4 in front of... behind the dart board's wall: the scatter did not know the wall between the two halves of the docks (x 17.8..18.2, its south stub z 0..7), so a stack
# (or a shelf on the south wall) could stand half inside it. The stub is a keep-clear rectangle of its own now: nothing is placed through that wall.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\FloorGenerator.Dock.cs"
Rep 'new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),' @'
new Vector4(cellSize - 0.2f, 0f, cellSize + 0.2f, (cellSize - doorWidth) / 2f),                       // the wall between the two halves of the docks (its south stub, the dart board's wall): nothing stands half inside it
                new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),
'@
Save-Src
Write-Output 'round 12 batch G28 patched'
