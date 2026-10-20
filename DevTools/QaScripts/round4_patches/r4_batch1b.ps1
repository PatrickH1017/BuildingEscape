# Round 4, batch 1b: the roles test is part of the pure run, and Looks checks the suites. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep '            Round2Test();' @'
            Round2Test();
            RolesTest();
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep '                LookCheck($"{f} the doors of offices with a glass front are glass"' @'
                int suitesN = 0, suitesGlass = 0, serverN = 0;
                foreach (var rr in layout.rooms)
                {
                    if (rr.role == "server") serverN++;
                    if (rr.role != "suite") continue;
                    suitesN++;
                    foreach (var gb in gen.GlassRoomBounds) if (Mathf.Abs(gb.x0 - rr.bounds.x0) < 0.01f && Mathf.Abs(gb.z0 - rr.bounds.z0) < 0.01f) { suitesGlass++; break; }
                }
                LookCheck($"{f} every executive suite has a glass front, and the floor has one server room", suitesN >= 1 && suitesGlass == suitesN && serverN == 1, $"({suitesGlass} of {suitesN} suites with glass, {serverN} server room)");
                LookCheck($"{f} the doors of offices with a glass front are glass"
'@
Save-Src
Write-Output 'round 4 batch 1b patched'
