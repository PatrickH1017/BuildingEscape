# Round 4, batch 2: the roles test leaves out the toy 3 x 2 floor (it has no numbered rooms), and an executive suite always gets its glass front (the pictures on that bit of wall come down). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep 'foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6), (3, 2) })' 'foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6) })       // (a floor of 3 x 2 cells has no numbered room at all: no suite is possible there, and the game never makes one)'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Glass.cs')
Rep '                    if (hung) continue;' @'
                    if (hung && room.role == OfficePlanner.RoleSuite)
                    {
                        L.pieces.RemoveAll(p => p.mount && near.Overlaps(p.rc));              // an executive suite always gets its glass front: the pictures and boards on that bit of wall come down
                        hung = false;
                        foreach (var sgn in signs) if (r.Grow(0.3f).Contains(sgn.x, sgn.z)) { hung = true; break; }      // (a direction sign on the corridor side stays: look for another wall)
                    }
                    if (hung) continue;
'@
Save-Src
Write-Output 'round 4 batch 2 patched'
