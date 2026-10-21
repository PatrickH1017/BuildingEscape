# Round 12, batch G29 (after `t12`): DockLook found 5 pallet blocks (it wants 6): the scattered pallets (target 4, 200 tries) were left to luck on a floor that is mostly rooms, and the forklift's keep-clear radius of 2.0 m (G27) took too
# much of what is left. The forklift is now checked where it really reaches: its middle with 1.35 m and the tip of its forks (1.5 m along its forward direction) with a small radius, so that its forks never reach into a keep-clear rectangle;
# the pallets get 1200 tries, and the drums too.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\FloorGenerator.Dock.cs"
RepRx '(?m)^[ \t]*float x = F\(1\.6f, width - 1\.6f\), z = F\(1\.6f, depth - 1\.6f\);\r?\n[ \t]*if \(!Free\(x, z, 2\.0f\)\) continue;[^\r\n]*\r?\n[ \t]*DockDressing\.Forklift\(dockHolder, x, z, F\(0f, 360f\)\);\r?\n[ \t]*placed\.Add\(new Vector3\(x, z, 1\.35f\)\);' @'
                float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f), yaw = F(0f, 360f);
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;                                              // (the forks point along +z and reach 1.9 m from its middle: their tip must not reach into a keep-clear rectangle either)
                float tipX = x + fwd.x * 1.5f, tipZ = z + fwd.z * 1.5f;
                if (!Free(x, z, 1.35f) || !Free(tipX, tipZ, 0.45f)) continue;
                DockDressing.Forklift(dockHolder, x, z, yaw);
                placed.Add(new Vector3(x, z, 1.35f));
                placed.Add(new Vector3(tipX, tipZ, 0.45f));
'@
Rep 'for (int tries = 0, pallets = 4; tries < 200 && pallets > 0; tries++)' 'for (int tries = 0, pallets = 4; tries < 1200 && pallets > 0; tries++)'
Rep 'for (int tries = 0, groups = 3; tries < 600 && groups > 0; tries++)' 'for (int tries = 0, groups = 3; tries < 1200 && groups > 0; tries++)'
Save-Src
Write-Output 'round 12 batch G29 patched'
