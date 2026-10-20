# Round 4, batch 19: NetDevHarness.CoreLooks also takes a picture of every emergency axe (up to 8 per floor, floors 1 and 4) from 2.8 m out in front of its wall, to judge by eye whether it hangs
# where an axe case would really be (the owner, 2026-10-17 night: "make sure the emergency axes are in places that look natural"). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRoomShots.cs')
Rep '                Log($"[Rooms] core looks of floor {floor}: {layout.deadDoors.Count} restroom doors, break room {(brk != null ? "yes" : "NO")}, {gen.StairCount} stairwells");' '                int axeNo = 0, axeAll = 0;
                foreach (var axe in layout.pieces.Where(pc => pc.key == "emergency_axe"))
                {
                    axeAll++;
                    if (axeNo >= 8) continue;
                    var front = new Vector3(Mathf.Sin(axe.yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(axe.yaw * Mathf.Deg2Rad));
                    var at = new Vector3(axe.x, axe.y + 0.45f, axe.z);
                    var inRoom = layout.rooms.FirstOrDefault(rm => rm.bounds.Contains(axe.x, axe.z));
                    Look("axe" + axeNo, at, at + front * 2.8f + Vector3.up * 0.95f);
                    Log($"[Rooms] axe {axeNo} of floor {floor}: in {(inRoom != null ? inRoom.theme : "?")} at ({axe.x:0.0}, {axe.z:0.0}), {axe.y:0.00} m up");
                    axeNo++;
                }
                Log($"[Rooms] core looks of floor {floor}: {layout.deadDoors.Count} restroom doors, break room {(brk != null ? "yes" : "NO")}, {gen.StairCount} stairwells, {axeAll} emergency axes");'
Save-Src
Write-Output 'round 4 batch 19 patched'
