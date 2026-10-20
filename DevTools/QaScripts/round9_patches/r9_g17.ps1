# Round 9, batch G17: the built-player autopilot's clip purchase meets the three-clip limit - the pistol found in a safe holds its clip and two spare ones (three clips), so the autopilot's pistol first has one spare clip used up (on both machines: each keeps its own copy of the gun) before the safe's clip is taken.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

Open-Src "$Src\QaAutopilot.cs"
Rep @'
                        Check("client: the pistol is in my hand and fires - the shot goes to the host", cFired && cGc.ShotsFired == shotsBefore + 1, $"(fired {cFired})");
'@ @'
                        Check("client: the pistol is in my hand and fires - the shot goes to the host", cFired && cGc.ShotsFired == shotsBefore + 1, $"(fired {cFired})");
                        cGun.Magazine.SetReserve(8);                      // (the owner, 2026-10-19: three clips at most. The pistol of a safe holds its clip and two spare ones: one spare clip is used up before the safe's clip can be taken; the host does the same to its copy)
'@
Rep @'
                    int fresh = pewSpec.spareClips * pewSpec.magazine;
'@ @'
                    rGun.Magazine.SetReserve(8);                          // (the client uses one spare clip up, see the client's side; a pistol cannot hold a fourth clip)
                    int fresh = 8;
'@
Save-Src
