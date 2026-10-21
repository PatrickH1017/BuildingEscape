# Round 12, batch G27 (after the first run `t10` of G26): two things the new layout showed.
#  (1) DockLook: the box stacks came to 8 (the test wants 10) and one prop stood in the way from the elevator to the bay: the stacks' count was left to luck (a target of 22 with 400 tries on a floor that is mostly rooms);
#      now 12 to 16 stacks are placed (2000 tries), the same density as the build the owner has seen (13-14); and the forklift's keep-clear radius is 2.0 m (its forks reach 1.9 m from its middle: a fork had reached into the way).
#  (2) Round12: the third-person camera check ran with the player 4 m behind the tape, 1.3 m from the break room's wall, which pulled the camera in to 1.08 m: the player goes back to the tape (open floor behind him) before the pictures and the camera tests.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$A\FloorGenerator.Dock.cs"
Rep 'if (!Free(x, z, 1.35f)) continue;' 'if (!Free(x, z, 2.0f)) continue;          // (the forks reach 1.9 m from its middle: its box must not reach into a keep-clear rectangle)'
Rep 'int stacks = 22;' 'int stacks = 12 + rng.Next(5);                                // (12 to 16 whatever the seed: the floor is mostly rooms, a bigger target left the count to luck)'
Rep 'for (int tries = 0; tries < 400 && stacks > 0; tries++)' 'for (int tries = 0; tries < 2000 && stacks > 0; tries++)'
Save-Src

Open-Src "$E\NetDevRound12.cs"
Rep @'
                R12Check("four metres further back the prompt is gone", !board.AtLine);
'@ @'
                R12Check("four metres further back the prompt is gone", !board.AtLine);
                player.TeleportTo(new Vector3(board.LineCentre.x, 1.1f, board.LineCentre.z), Quaternion.LookRotation(-board.Normal));          // (back at the tape, facing the board: open floor behind him for the pictures and the camera tests below)
                yield return new WaitForSeconds(0.4f);
'@
Save-Src
Write-Output 'round 12 batch G27 patched'
